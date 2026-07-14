using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Interpreter.Host.ExternalWorker;

/// <summary>
/// Launches one Windows AppContainer worker for one staged dump query and then tears the containment boundary down.
/// </summary>
/// <remarks>
/// This draft W1 broker is intentionally Windows-only and fail-closed. It copies the caller artifact into
/// broker-private staging, passes only a reopened read-only handle, and uses one atomic <c>STARTUPINFOEX</c> launch
/// containing the Job, exact handle-list, and AppContainer attributes. The Job admits one active process, forbids
/// breakaway, and is paired with a runner-observed child-denial probe. This is not a reusable process hosting
/// framework.
/// </remarks>
public sealed class WindowsExternalWorkerBroker
{
    private const string ContainmentProfile = "windows-appcontainer-job-v1";
    private const uint WorkerTerminationExitCode = 0xE0010001;
    private static readonly TimeSpan WallDeadline =
        TimeSpan.FromMilliseconds(ExternalWorkerPolicy.MaximumWallDurationMilliseconds);
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(5);
    private readonly string _runnerExecutablePath;

    /// <summary>Creates a broker for one trusted, installed runner executable.</summary>
    /// <param name="runnerExecutablePath">
    /// Fully qualified path selected by the trusted product host. This path is never accepted from a worker request.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="runnerExecutablePath"/> is not fully qualified.</exception>
    public WindowsExternalWorkerBroker(string runnerExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runnerExecutablePath);
        if (!Path.IsPathFullyQualified(runnerExecutablePath))
        {
            throw new ArgumentException("The trusted runner path must be fully qualified.", nameof(runnerExecutablePath));
        }

        _runnerExecutablePath = Path.GetFullPath(runnerExecutablePath);
    }

    /// <summary>Evaluates one bounded W2 query in a fresh constrained worker.</summary>
    /// <param name="artifactPath">Caller-selected dump path consumed only by the trusted staging broker.</param>
    /// <param name="request">Bounded root-selection and query request sent through the inherited request pipe.</param>
    /// <returns>
    /// The authorized query response plus a separate payload-free telemetry projection. Host, parser, containment,
    /// and resource failures are normalized to stable codes without copying paths or exception payloads.
    /// </returns>
    public ExternalWorkerExecutionResult Evaluate(string artifactPath, ExternalDumpQueryRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArgumentNullException.ThrowIfNull(request);
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return Failure(
                ExternalWorkerOutcome.ContainmentUnavailable,
                "WORKER_PLATFORM_UNSUPPORTED",
                "The external worker requires 64-bit Windows containment.");
        }

        if (!File.Exists(_runnerExecutablePath))
        {
            return Failure(
                ExternalWorkerOutcome.ContainmentUnavailable,
                "WORKER_RUNNER_UNAVAILABLE",
                "The trusted external worker executable is unavailable.");
        }

        if (!IsRequestStructurallyBounded(request))
        {
            return Failure(
                ExternalWorkerOutcome.InvalidRequest,
                "WORKER_REQUEST_INVALID",
                "The external worker request violates the fixed query bounds.");
        }

        var artifactStaged = false;
        try
        {
            using var stagedArtifact = StagedArtifact.Create(artifactPath);
            artifactStaged = true;
            using var appContainer = AppContainerContext.Create();
            var deployedRunnerPath = appContainer.DeployRunner(_runnerExecutablePath);
            using var job = CreateConstrainedJob();
            using var requestPipes = InheritedPipe.Create(childReads: true);
            using var responsePipes = InheritedPipe.Create(childReads: false);
            using var networkProbe = LoopbackNetworkProbe.Create();
            using var attributes = new CreationAttributeList(3);

            attributes.AddHandleList(
                stagedArtifact.Handle.DangerousGetHandle(),
                requestPipes.ChildHandle.DangerousGetHandle(),
                responsePipes.ChildHandle.DangerousGetHandle());
            attributes.AddJobList(job.DangerousGetHandle());
            attributes.AddSecurityCapabilities(appContainer.Sid);

            var startup = new WindowsNative.StartupInfoEx
            {
                StartupInfo = new WindowsNative.StartupInfo
                {
                    Size = Marshal.SizeOf<WindowsNative.StartupInfoEx>(),
                },
                AttributeList = attributes.Pointer,
            };
            using var environment = BuildEnvironment(
                appContainer.LocalAppDataDirectory,
                appContainer.ScratchDirectory);
            var commandLine = BuildCommandLine(
                deployedRunnerPath,
                stagedArtifact.Handle,
                requestPipes.ChildHandle,
                responsePipes.ChildHandle);
            WindowsNative.ProcessInformation processInformation;
            bool processCreated;
            using (NoDialogProcessLaunchScope.Enter())
            {
                processCreated = WindowsNative.CreateProcessW(
                    deployedRunnerPath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    inheritHandles: true,
                    WindowsNative.ExtendedStartupInfoPresent |
                    WindowsNative.CreateUnicodeEnvironment |
                    WindowsNative.CreateNoWindow,
                    environment.Pointer,
                    appContainer.ScratchDirectory,
                    ref startup,
                    out processInformation);
            }

            if (!processCreated)
            {
                return Failure(
                    ExternalWorkerOutcome.ContainmentUnavailable,
                    "WORKER_LAUNCH_REJECTED",
                    "Windows rejected the required external-worker containment profile.");
            }

            using var process = new SafeFileHandle(processInformation.Process, ownsHandle: true);
            using var thread = new SafeFileHandle(processInformation.Thread, ownsHandle: true);
            requestPipes.CloseChildHandle();
            responsePipes.CloseChildHandle();

            try
            {
                WorkerProtocol.WriteRequest(requestPipes.ParentStream, request, networkProbe.Port);
                requestPipes.ParentStream.Dispose();
            }
            catch
            {
                if (!TerminateAndWait(job, process))
                {
                    return TerminationFailure();
                }

                return Failure(
                    ExternalWorkerOutcome.WorkerFailure,
                    "WORKER_REQUEST_CHANNEL_FAILED",
                    "The bounded worker request channel failed.");
            }

            var responseTask = Task.Run(() => WorkerProtocol.ReadResponse(responsePipes.ParentStream));
            var completed = Task.WhenAny(responseTask, Task.Delay(WallDeadline)).GetAwaiter().GetResult();
            if (!ReferenceEquals(completed, responseTask))
            {
                responsePipes.ParentStream.Dispose();
                if (!TerminateAndWait(job, process))
                {
                    return TerminationFailure();
                }

                ObserveCompletedResponseTask(responseTask);
                return Failure(
                    ExternalWorkerOutcome.ResourceLimit,
                    "WORKER_WALL_LIMIT",
                    "The external worker exceeded its host responsiveness boundary.",
                    ExternalWorkerResourceBucket.LimitReached);
            }

            ExternalDumpQueryResponse response;
            try
            {
                response = responseTask.GetAwaiter().GetResult();
                if (!ExternalWorkerResponseValidator.IsValid(response))
                {
                    throw new InvalidDataException("The worker response violates the authorized result contract.");
                }
            }
            catch
            {
                if (!TerminateAndWait(job, process))
                {
                    return TerminationFailure();
                }

                return Failure(
                    ExternalWorkerOutcome.WorkerFailure,
                    "WORKER_RESPONSE_INVALID",
                    "The external worker did not return one valid bounded response.");
            }

            var wait = WindowsNative.WaitForSingleObject(process, checked((uint)ExitGrace.TotalMilliseconds));
            if (wait != WindowsNative.WaitObject0)
            {
                if (!TerminateAndWait(job, process))
                {
                    return TerminationFailure();
                }

                return Failure(
                    ExternalWorkerOutcome.WorkerFailure,
                    "WORKER_ONE_SHOT_EXIT_FAILED",
                    "The external worker did not exit after its single response.");
            }

            if (!WindowsNative.GetExitCodeProcess(process, out var exitCode) || exitCode != 0)
            {
                return Failure(
                    ExternalWorkerOutcome.WorkerFailure,
                    "WORKER_ONE_SHOT_EXIT_INVALID",
                    "The external worker exited without completing its one-shot protocol.");
            }

            try
            {
                if (responsePipes.ParentStream.ReadByte() != -1)
                {
                    return Failure(
                        ExternalWorkerOutcome.WorkerFailure,
                        "WORKER_RESPONSE_TRAILING_DATA",
                        "The external worker returned data after its single bounded response.");
                }
            }
            catch
            {
                return Failure(
                    ExternalWorkerOutcome.WorkerFailure,
                    "WORKER_RESPONSE_CHANNEL_FAILED",
                    "The bounded worker response channel failed after process exit.");
            }

            var attestation = response.Attestation with
            {
                ExactHandleListLaunch = true,
                AtomicJobLaunch = true,
            };
            if (networkProbe.ConnectionObserved || !AttestationIsComplete(attestation, response.Outcome))
            {
                return Failure(
                    ExternalWorkerOutcome.ContainmentUnavailable,
                    "WORKER_ATTESTATION_FAILED",
                    "The external worker could not attest every required containment property.");
            }

            response = response with { Attestation = attestation };
            return new ExternalWorkerExecutionResult(
                response,
                new ExternalWorkerTelemetry(
                    ExternalWorkerOperation.DumpQuery,
                    response.Outcome,
                    ExternalWorkerResourceBucket.WithinLimits,
                    ContainmentProfile));
        }
        catch (ExternalArtifactLimitException)
        {
            return Failure(
                ExternalWorkerOutcome.ArtifactRejected,
                "WORKER_ARTIFACT_LIMIT",
                "The caller-selected artifact exceeds the external-worker admission bound.",
                ExternalWorkerResourceBucket.LimitReached);
        }
        catch (ExternalWorkerCleanupException)
        {
            return Failure(
                ExternalWorkerOutcome.WorkerFailure,
                "WORKER_CLEANUP_FAILED",
                "The external worker could not remove its private request data.");
        }
        catch (Exception exception) when (IsNormalizedBoundaryFailure(exception))
        {
            return artifactStaged
                ? Failure(
                    ExternalWorkerOutcome.ContainmentUnavailable,
                    "WORKER_CONTAINMENT_UNAVAILABLE",
                    "The operating system could not establish the complete worker boundary.")
                : Failure(
                    ExternalWorkerOutcome.ArtifactRejected,
                    "WORKER_ARTIFACT_UNAVAILABLE",
                    "The caller-selected artifact could not be staged.");
        }
    }

    private static bool IsRequestStructurallyBounded(ExternalDumpQueryRequest request) =>
        !string.IsNullOrWhiteSpace(request.RootTypeName) &&
        request.RootTypeName.Length <= ExternalWorkerPolicy.MaximumRootTypeNameCharacters &&
        !string.IsNullOrWhiteSpace(request.RootName) &&
        request.RootName.Length <= ExternalWorkerPolicy.MaximumRootNameCharacters &&
        request.Expression is not null &&
        request.Expression.Length <= ExternalWorkerPolicy.MaximumExpressionCharacters;

    private static bool AttestationIsComplete(
        ExternalWorkerContainmentAttestation value,
        ExternalWorkerOutcome outcome) =>
        value.AppContainerToken && value.JobMembership && JobLimitsAreComplete(value) && value.ZeroCapabilityLaunch &&
        value.ExactHandleListLaunch && value.AtomicJobLaunch && value.ChildProcessDenied &&
        value.DiagnosticsDisabled && value.ScratchStatus == ExternalWorkerScratchStatus.Established &&
        value.EnvironmentCleared && value.NetworkDenied &&
        value.HeadlessErrorPolicy && value.ArtifactReadOnly &&
        (value.TrustedDacPinned || outcome == ExternalWorkerOutcome.TrustedDacRejected);

    private static bool JobLimitsAreComplete(ExternalWorkerContainmentAttestation value)
    {
        const uint requiredFlags =
            WindowsNative.JobObjectLimitProcessTime |
            WindowsNative.JobObjectLimitActiveProcess |
            WindowsNative.JobObjectLimitProcessMemory |
            WindowsNative.JobObjectLimitJobMemory |
            WindowsNative.JobObjectLimitDieOnUnhandledException |
            WindowsNative.JobObjectLimitKillOnJobClose;
        const uint forbiddenFlags =
            WindowsNative.JobObjectLimitBreakawayOk |
            WindowsNative.JobObjectLimitSilentBreakawayOk;
        return (value.JobLimitFlags & requiredFlags) == requiredFlags &&
               (value.JobLimitFlags & forbiddenFlags) == 0 &&
               value.JobActiveProcessLimit == 1 &&
               value.JobProcessMemoryBytes == ExternalWorkerPolicy.MaximumProcessMemoryBytes &&
               value.JobMemoryBytes == ExternalWorkerPolicy.MaximumProcessMemoryBytes &&
               value.JobProcessUserTimeTicks == ExternalWorkerPolicy.MaximumProcessUserTimeTicks;
    }

    private static bool IsNormalizedBoundaryFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or Win32Exception or
            ExternalException or InvalidOperationException or NotSupportedException or OverflowException;

    private static ExternalWorkerExecutionResult Failure(
        ExternalWorkerOutcome outcome,
        string code,
        string message,
        ExternalWorkerResourceBucket resourceBucket = ExternalWorkerResourceBucket.Unknown)
    {
        var response = ExternalDumpQueryResponse.Failure(outcome, code, message);
        return new ExternalWorkerExecutionResult(
            response,
            new ExternalWorkerTelemetry(
                ExternalWorkerOperation.DumpQuery,
                outcome,
                resourceBucket,
                ContainmentProfile));
    }

    private static SafeFileHandle CreateConstrainedJob()
    {
        var job = WindowsNative.CreateJobObjectW(IntPtr.Zero, name: null);
        if (job.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var limits = new WindowsNative.ExtendedLimitInformation
        {
            BasicLimitInformation = new WindowsNative.BasicLimitInformation
            {
                PerProcessUserTimeLimit = ExternalWorkerPolicy.MaximumProcessUserTimeTicks,
                ActiveProcessLimit = 1,
                LimitFlags =
                    WindowsNative.JobObjectLimitProcessTime |
                    WindowsNative.JobObjectLimitActiveProcess |
                    WindowsNative.JobObjectLimitProcessMemory |
                    WindowsNative.JobObjectLimitJobMemory |
                    WindowsNative.JobObjectLimitDieOnUnhandledException |
                    WindowsNative.JobObjectLimitKillOnJobClose,
            },
            ProcessMemoryLimit = checked((nuint)ExternalWorkerPolicy.MaximumProcessMemoryBytes),
            JobMemoryLimit = checked((nuint)ExternalWorkerPolicy.MaximumProcessMemoryBytes),
        };
        if (!WindowsNative.SetInformationJobObject(
                job,
                WindowsNative.JobObjectExtendedLimitInformation,
                ref limits,
                checked((uint)Marshal.SizeOf<WindowsNative.ExtendedLimitInformation>())))
        {
            var error = Marshal.GetLastWin32Error();
            job.Dispose();
            throw new Win32Exception(error);
        }

        return job;
    }

    private static EnvironmentBlock BuildEnvironment(
        string localAppDataDirectory,
        string scratchDirectory)
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot")
            ?? throw new InvalidOperationException("Windows did not expose its system root.");
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = systemRoot,
            ["WINDIR"] = systemRoot,
            ["LOCALAPPDATA"] = localAppDataDirectory,
            ["TEMP"] = scratchDirectory,
            ["TMP"] = scratchDirectory,
            ["INTERPRETER_EXTERNAL_WORKER_SCRATCH"] = scratchDirectory,
            ["INTERPRETER_EXTERNAL_WORKER_PROFILE"] = localAppDataDirectory,
            ["INTERPRETER_EXTERNAL_WORKER_ENVIRONMENT"] = "1",
            ["DOTNET_EnableDiagnostics"] = "0",
            ["DOTNET_EnableDiagnostics_IPC"] = "0",
            ["DOTNET_EnableDiagnostics_Debugger"] = "0",
            ["DOTNET_EnableDiagnostics_Profiler"] = "0",
            ["DOTNET_DefaultDiagnosticPortSuspend"] = "0",
            ["DOTNET_EnableEventPipe"] = "0",
            ["DOTNET_DISABLE_GUI_ERRORS"] = "1",
            ["COMPlus_EnableDiagnostics"] = "0",
        };
        var builder = new StringBuilder();
        foreach (var pair in values)
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
        }

        builder.Append('\0');
        return new EnvironmentBlock(builder.ToString());
    }

    private static StringBuilder BuildCommandLine(
        string executablePath,
        SafeFileHandle artifact,
        SafeFileHandle request,
        SafeFileHandle response)
    {
        var command = string.Create(
            CultureInfo.InvariantCulture,
            $"\"{executablePath}\" --worker --artifact-handle {artifact.DangerousGetHandle().ToInt64()} " +
            $"--request-handle {request.DangerousGetHandle().ToInt64()} " +
            $"--response-handle {response.DangerousGetHandle().ToInt64()}");
        return new StringBuilder(command);
    }

    private static bool TerminateAndWait(SafeFileHandle job, SafeFileHandle process)
    {
        return WindowsNative.TerminateJobObject(job, WorkerTerminationExitCode) &&
               WindowsNative.WaitForSingleObject(process, 5_000) == WindowsNative.WaitObject0;
    }

    private static void ObserveCompletedResponseTask(Task<ExternalDumpQueryResponse> responseTask)
    {
        if (!responseTask.IsCompleted)
        {
            return;
        }

        try
        {
            _ = responseTask.GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    private static ExternalWorkerExecutionResult TerminationFailure() => Failure(
        ExternalWorkerOutcome.WorkerFailure,
        "WORKER_TERMINATION_FAILED",
        "The external worker could not be synchronously terminated.");

    private sealed class EnvironmentBlock : IDisposable
    {
        internal EnvironmentBlock(string value) => Pointer = Marshal.StringToHGlobalUni(value);

        internal IntPtr Pointer { get; }

        public void Dispose() => Marshal.FreeHGlobal(Pointer);
    }

    private sealed class LoopbackNetworkProbe : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellation;
        private readonly Task<TcpClient> _acceptTask;

        private LoopbackNetworkProbe(TcpListener listener)
        {
            _listener = listener;
            _cancellation = new CancellationTokenSource();
            _acceptTask = listener.AcceptTcpClientAsync(_cancellation.Token).AsTask();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        internal int Port { get; }

        internal bool ConnectionObserved =>
            _acceptTask.IsCompletedSuccessfully ||
            (!_acceptTask.IsCompleted && _listener.Server.Poll(0, SelectMode.SelectRead) && _listener.Pending());

        internal static LoopbackNetworkProbe Create()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start(backlog: 1);
                return new LoopbackNetworkProbe(listener);
            }
            catch
            {
                listener.Stop();
                throw;
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _listener.Stop();
            if (_acceptTask.IsCompletedSuccessfully)
            {
                _acceptTask.Result.Dispose();
            }

            _cancellation.Dispose();
        }
    }

    private sealed class InheritedPipe : IDisposable
    {
        private SafeFileHandle? _childHandle;

        private InheritedPipe(SafeFileHandle childHandle, SafeFileHandle parentHandle, FileAccess parentAccess)
        {
            _childHandle = childHandle;
            ParentStream = new FileStream(parentHandle, parentAccess, bufferSize: 4096, isAsync: false);
        }

        internal SafeFileHandle ChildHandle => _childHandle
            ?? throw new ObjectDisposedException(nameof(InheritedPipe));

        internal FileStream ParentStream { get; }

        internal static InheritedPipe Create(bool childReads)
        {
            var attributes = new WindowsNative.SecurityAttributes
            {
                Length = Marshal.SizeOf<WindowsNative.SecurityAttributes>(),
                InheritHandle = true,
            };
            if (!WindowsNative.CreatePipe(out var read, out var write, ref attributes, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var child = childReads ? read : write;
            var parent = childReads ? write : read;
            if (!WindowsNative.SetHandleInformation(parent, WindowsNative.HandleFlagInherit, 0))
            {
                var error = Marshal.GetLastWin32Error();
                read.Dispose();
                write.Dispose();
                throw new Win32Exception(error);
            }

            return new InheritedPipe(child, parent, childReads ? FileAccess.Write : FileAccess.Read);
        }

        internal void CloseChildHandle()
        {
            _childHandle?.Dispose();
            _childHandle = null;
        }

        public void Dispose()
        {
            _childHandle?.Dispose();
            try
            {
                ParentStream.Dispose();
            }
            catch (IOException)
            {
                // A peer that already exited can close a pipe during FileStream's final flush.
            }
        }
    }

    private sealed class CreationAttributeList : IDisposable
    {
        private readonly List<IntPtr> _allocations = [];

        internal CreationAttributeList(int attributeCount)
        {
            nuint size = 0;
            _ = WindowsNative.InitializeProcThreadAttributeList(IntPtr.Zero, attributeCount, 0, ref size);
            Pointer = Marshal.AllocHGlobal(checked((nint)size));
            if (!WindowsNative.InitializeProcThreadAttributeList(Pointer, attributeCount, 0, ref size))
            {
                var error = Marshal.GetLastWin32Error();
                Marshal.FreeHGlobal(Pointer);
                throw new Win32Exception(error);
            }
        }

        internal IntPtr Pointer { get; }

        internal void AddHandleList(params IntPtr[] handles) =>
            AddPointerList(WindowsNative.ProcThreadAttributeHandleList, handles);

        internal void AddJobList(params IntPtr[] handles) =>
            AddPointerList(WindowsNative.ProcThreadAttributeJobList, handles);

        internal void AddSecurityCapabilities(IntPtr appContainerSid)
        {
            var capabilities = new WindowsNative.SecurityCapabilities
            {
                AppContainerSid = appContainerSid,
                Capabilities = IntPtr.Zero,
                CapabilityCount = 0,
                Reserved = 0,
            };
            AddStructure(WindowsNative.ProcThreadAttributeSecurityCapabilities, capabilities);
        }

        internal void AddUInt32(nuint attribute, uint value)
        {
            var pointer = Marshal.AllocHGlobal(sizeof(uint));
            Marshal.WriteInt32(pointer, unchecked((int)value));
            _allocations.Add(pointer);
            Update(attribute, pointer, sizeof(uint));
        }

        public void Dispose()
        {
            WindowsNative.DeleteProcThreadAttributeList(Pointer);
            foreach (var allocation in _allocations)
            {
                Marshal.FreeHGlobal(allocation);
            }

            Marshal.FreeHGlobal(Pointer);
        }

        private void AddPointerList(nuint attribute, IntPtr[] handles)
        {
            var size = checked(handles.Length * IntPtr.Size);
            var pointer = Marshal.AllocHGlobal(size);
            for (var index = 0; index < handles.Length; index++)
            {
                Marshal.WriteIntPtr(pointer, index * IntPtr.Size, handles[index]);
            }

            _allocations.Add(pointer);
            Update(attribute, pointer, size);
        }

        private void AddStructure<T>(nuint attribute, T value)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var pointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(value, pointer, fDeleteOld: false);
            _allocations.Add(pointer);
            Update(attribute, pointer, size);
        }

        private void Update(nuint attribute, IntPtr value, int size)
        {
            if (!WindowsNative.UpdateProcThreadAttribute(
                    Pointer,
                    0,
                    attribute,
                    value,
                    checked((nuint)size),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}
