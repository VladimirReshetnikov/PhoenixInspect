using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Host.ExternalWorker;
using Interpreter.Product.DumpQuery;
using Microsoft.Win32.SafeHandles;

namespace Interpreter.Host.ExternalWorker.Runner;

internal static class Program
{
    private const string PinnedDacSha256 = "7ceec1cc943dfb27362b06b70fec336eecde3a563e9e12f3566a867869fc6ac3";
    private static ExternalWorkerScratchStatus _scratchStatus = ExternalWorkerScratchStatus.EnvironmentUnavailable;

    internal static int Main(string[] args)
    {
        _scratchStatus = EstablishPrivateScratch();
        var privateScratchEstablished = _scratchStatus == ExternalWorkerScratchStatus.Established;
        if (!TryParseHandles(args, out var artifactHandle, out var requestHandle, out var responseHandle))
        {
            return 2;
        }

        using var requestPipe = new FileStream(
            new SafeFileHandle(requestHandle, ownsHandle: true),
            FileAccess.Read,
            bufferSize: 4096,
            isAsync: false);
        using var responsePipe = new FileStream(
            new SafeFileHandle(responseHandle, ownsHandle: true),
            FileAccess.Write,
            bufferSize: 4096,
            isAsync: false);

        ExternalDumpQueryResponse response;
        try
        {
            var request = WorkerProtocol.ReadRequest(requestPipe);
            var networkDenied = ProbeNetworkDenied(request.LoopbackProbePort);
            response = Execute(artifactHandle, request.Query, networkDenied, privateScratchEstablished);
        }
        catch (Exception exception) when (IsNormalizedWorkerFailure(exception))
        {
            response = ExternalDumpQueryResponse.Failure(
                ExternalWorkerOutcome.WorkerFailure,
                "WORKER_INTERNAL_FAILURE",
                "The external worker could not complete the bounded request.",
                ObserveContainment(
                    artifactReadOnly: false,
                    trustedDacPinned: false,
                    networkDenied: false,
                    privateScratchEstablished));
        }

        try
        {
            WorkerProtocol.WriteResponse(responsePipe, response);
            return 0;
        }
        catch
        {
            return 3;
        }
    }

    private static ExternalDumpQueryResponse Execute(
        IntPtr artifactHandle,
        ExternalDumpQueryRequest request,
        bool networkDenied,
        bool privateScratchEstablished)
    {
        if (!RequestIsValid(request))
        {
            return ExternalDumpQueryResponse.Failure(
                ExternalWorkerOutcome.InvalidRequest,
                "WORKER_REQUEST_INVALID",
                "The external worker request violates the fixed query bounds.",
                ObserveContainment(
                    artifactReadOnly: true,
                    trustedDacPinned: false,
                    networkDenied,
                    privateScratchEstablished));
        }

        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return ExternalDumpQueryResponse.Failure(
                ExternalWorkerOutcome.ContainmentUnavailable,
                "WORKER_PLATFORM_UNSUPPORTED",
                "The external worker requires 64-bit Windows containment.",
                ObserveContainment(
                    artifactReadOnly: true,
                    trustedDacPinned: false,
                    networkDenied,
                    privateScratchEstablished));
        }

        var dacPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscordaccore.dll");
        FileStream? dacLease = null;
        var trustedDacPinned = false;
        try
        {
            dacLease = new FileStream(dacPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var observedHash = SHA256.HashData(dacLease);
            dacLease.Position = 0;
            if (!CryptographicOperations.FixedTimeEquals(observedHash, Convert.FromHexString(PinnedDacSha256)))
            {
                return ExternalDumpQueryResponse.Failure(
                    ExternalWorkerOutcome.TrustedDacRejected,
                    "WORKER_DAC_TRUST_REJECTED",
                    "The runtime-adjacent DAC does not match the compiled trust pin.",
                    ObserveContainment(
                        artifactReadOnly: true,
                        trustedDacPinned: false,
                        networkDenied,
                        privateScratchEstablished));
            }

            trustedDacPinned = true;

            var artifactStream = new FileStream(
                new SafeFileHandle(artifactHandle, ownsHandle: true),
                FileAccess.Read,
                bufferSize: 1024 * 1024,
                isAsync: false);
            var open = ClrmdDumpSession.OpenBrokered(artifactStream, "brokered-dump", dacPath);
            if (open.Status != ClrmdEvidenceStatus.Exact || open.Value is null)
            {
                return ExternalDumpQueryResponse.Failure(
                    ExternalWorkerOutcome.ArtifactRejected,
                    "WORKER_ARTIFACT_REJECTED",
                    "The inherited dump artifact is unavailable, invalid, unsupported, or exceeds a bound.",
                    ObserveContainment(
                        artifactReadOnly: true,
                        trustedDacPinned: true,
                        networkDenied,
                        privateScratchEstablished));
            }

            using var session = open.Value;
            if (!session.UsesExplicitDac || !session.IsOfflineLocatorInstalled || !session.IsBoundedDumpCachePolicyEnforced)
            {
                return ExternalDumpQueryResponse.Failure(
                    ExternalWorkerOutcome.ContainmentUnavailable,
                    "WORKER_ADAPTER_POLICY_REJECTED",
                    "The dump adapter did not retain the required offline explicit-DAC policy.",
                    ObserveContainment(
                        artifactReadOnly: true,
                        trustedDacPinned: true,
                        networkDenied,
                        privateScratchEstablished));
            }

            var roots = session.FindStrongHandleObjectsByTypeName(
                request.RootTypeName,
                ExternalWorkerPolicy.MaximumRootMatches,
                ExternalWorkerPolicy.MaximumHandlesScanned);
            var root = roots.Status == ClrmdEvidenceStatus.Exact && roots.Matches.Length == 1
                ? roots.Matches[0]
                : null;
            var result = DumpQueryEngine.Evaluate(session, request.Expression, request.RootName, root);
            var value = result.Value is null
                ? null
                : new ExternalDumpQueryValue(
                    result.Value.Kind.ToString(),
                    result.Value.Int32Value,
                    result.Value.StringValue);
            var diagnostics = result.Diagnostics
                .Select(static item => new ExternalWorkerDiagnostic(item.Code, item.Message))
                .ToArray();
            var snapshotIdentity = new ExternalDumpSnapshotIdentity(
                session.Snapshot.Sha256,
                session.Snapshot.MemorySourceId);
            ExternalDumpModuleIdentity? moduleIdentity = null;
            if (root is not null)
            {
                var identity = root.Module.Identity;
                moduleIdentity = new ExternalDumpModuleIdentity(
                    identity.Snapshot.Sha256,
                    identity.AppDomainAddress,
                    identity.ModuleAddress,
                    identity.ImageBase,
                    identity.ImageSize);
            }

            return new ExternalDumpQueryResponse(
                ExternalWorkerOutcome.Completed,
                "WORKER_QUERY_RESULT",
                "The constrained worker returned one bounded query result.",
                result.SemanticMode.ToString(),
                result.Completion.ToString(),
                result.Completeness.ToString(),
                result.Evidence.ToString(),
                result.Effects.ToString(),
                snapshotIdentity,
                moduleIdentity,
                "DumpMemory",
                ExternalWorkerPolicy.AppliedBounds,
                "None",
                value,
                result.Provenance.Length,
                diagnostics,
                ObserveContainment(
                    artifactReadOnly: !artifactStream.CanWrite,
                    trustedDacPinned: true,
                    networkDenied,
                    privateScratchEstablished));
        }
        catch (Exception exception) when (IsNormalizedWorkerFailure(exception))
        {
            return ExternalDumpQueryResponse.Failure(
                ExternalWorkerOutcome.WorkerFailure,
                "WORKER_INTERNAL_FAILURE",
                "The external worker could not complete the bounded request.",
                ObserveContainment(
                    artifactReadOnly: true,
                    trustedDacPinned,
                    networkDenied,
                    privateScratchEstablished));
        }
        finally
        {
            dacLease?.Dispose();
        }
    }

    private static ExternalWorkerContainmentAttestation ObserveContainment(
        bool artifactReadOnly,
        bool trustedDacPinned,
        bool networkDenied,
        bool privateScratchEstablished)
    {
        var inJob = WindowsNative.IsProcessInJob(
            WindowsNative.GetCurrentProcess(),
            IntPtr.Zero,
            out var observedInJob) && observedInJob;
        var jobLimitFlags = 0u;
        var jobActiveProcessLimit = 0u;
        var jobProcessMemoryBytes = 0L;
        var jobMemoryBytes = 0L;
        var jobProcessUserTimeTicks = 0L;
        if (inJob && WindowsNative.QueryInformationJobObject(
                IntPtr.Zero,
                WindowsNative.JobObjectExtendedLimitInformation,
                out var jobLimits,
                checked((uint)Marshal.SizeOf<WindowsNative.ExtendedLimitInformation>()),
                out _))
        {
            jobLimitFlags = jobLimits.BasicLimitInformation.LimitFlags;
            jobActiveProcessLimit = jobLimits.BasicLimitInformation.ActiveProcessLimit;
            jobProcessMemoryBytes = checked((long)jobLimits.ProcessMemoryLimit);
            jobMemoryBytes = checked((long)jobLimits.JobMemoryLimit);
            jobProcessUserTimeTicks = jobLimits.BasicLimitInformation.PerProcessUserTimeLimit;
        }

        var isAppContainer = false;
        var zeroCapabilities = false;
        if (WindowsNative.OpenProcessToken(WindowsNative.GetCurrentProcess(), 0x0008, out var token))
        {
            using (token)
            {
                isAppContainer = WindowsNative.GetTokenInformation(
                    token,
                    WindowsNative.TokenIsAppContainer,
                    out var value,
                    sizeof(int),
                    out _) && value != 0;
                zeroCapabilities = TokenHasZeroCapabilities(token);
            }
        }

        var diagnosticsDisabled =
            Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics") == "0" &&
            Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics_IPC") == "0" &&
            Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics_Debugger") == "0" &&
            Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics_Profiler") == "0";
        var clearedEnvironment =
            privateScratchEstablished &&
            Environment.GetEnvironmentVariable("INTERPRETER_EXTERNAL_WORKER_ENVIRONMENT") == "1" &&
            Environment.GetEnvironmentVariable("OPENAI_API_KEY") is null &&
            Environment.GetEnvironmentVariable("GITHUB_TOKEN") is null &&
            Environment.GetEnvironmentVariable("GH_TOKEN") is null &&
            Environment.GetEnvironmentVariable("USERPROFILE") is null &&
            Environment.GetEnvironmentVariable("PATH") is null &&
            Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS") is null &&
            Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH") is null;
        return new ExternalWorkerContainmentAttestation(
            isAppContainer,
            inJob,
            jobLimitFlags,
            jobActiveProcessLimit,
            jobProcessMemoryBytes,
            jobMemoryBytes,
            jobProcessUserTimeTicks,
            zeroCapabilities,
            false,
            false,
            ProbeChildProcessDenied(),
            diagnosticsDisabled,
            _scratchStatus,
            clearedEnvironment,
            networkDenied,
            HeadlessProcessPolicy.IsApplied(),
            artifactReadOnly,
            trustedDacPinned);
    }

    private static bool ProbeChildProcessDenied()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) || !Path.IsPathFullyQualified(processPath))
        {
            return false;
        }

        var start = new ProcessStartInfo(processPath, "--child-denial-probe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ErrorDialog = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        try
        {
            using var child = Process.Start(start);
            if (child is null)
            {
                return false;
            }

            if (!child.WaitForExit(milliseconds: 10_000))
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit();
            }

            return false;
        }
        catch (Win32Exception)
        {
            return true;
        }
    }

    private static ExternalWorkerScratchStatus EstablishPrivateScratch()
    {
        var scratch = Environment.GetEnvironmentVariable("INTERPRETER_EXTERNAL_WORKER_SCRATCH");
        var profile = Environment.GetEnvironmentVariable("INTERPRETER_EXTERNAL_WORKER_PROFILE");
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrWhiteSpace(scratch) ||
            string.IsNullOrWhiteSpace(profile) ||
            string.IsNullOrWhiteSpace(localAppData))
        {
            return ExternalWorkerScratchStatus.EnvironmentUnavailable;
        }

        if (!Path.IsPathFullyQualified(scratch) ||
            !Path.IsPathFullyQualified(profile) ||
            !Path.IsPathFullyQualified(localAppData))
        {
            return ExternalWorkerScratchStatus.InvalidPath;
        }

        if (!Directory.Exists(scratch))
        {
            return ExternalWorkerScratchStatus.ScratchDirectoryUnavailable;
        }

        var relativeScratch = Path.GetRelativePath(
            Path.GetFullPath(profile),
            Path.GetFullPath(scratch));
        if (Path.IsPathFullyQualified(relativeScratch) ||
            relativeScratch.Equals("..", StringComparison.Ordinal) ||
            relativeScratch.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return ExternalWorkerScratchStatus.OutsideProfile;
        }

        try
        {
            Environment.CurrentDirectory = scratch;
            Environment.SetEnvironmentVariable("TEMP", scratch);
            Environment.SetEnvironmentVariable("TMP", scratch);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ExternalWorkerScratchStatus.EstablishmentRejected;
        }

        var established = string.Equals(
                   Path.GetFullPath(Environment.CurrentDirectory),
                   Path.GetFullPath(scratch),
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Environment.GetEnvironmentVariable("TEMP"), scratch, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Environment.GetEnvironmentVariable("TMP"), scratch, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   Environment.GetEnvironmentVariable("INTERPRETER_EXTERNAL_WORKER_PROFILE"),
                   profile,
                   StringComparison.OrdinalIgnoreCase);
        return established
            ? ExternalWorkerScratchStatus.Established
            : ExternalWorkerScratchStatus.VerificationFailed;
    }

    private static bool TokenHasZeroCapabilities(SafeFileHandle token)
    {
        _ = WindowsNative.GetTokenInformationBuffer(
            token,
            WindowsNative.TokenCapabilities,
            IntPtr.Zero,
            0,
            out var requiredBytes);
        if (requiredBytes < sizeof(int) || requiredBytes > 64 * 1024)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal(requiredBytes);
        try
        {
            return WindowsNative.GetTokenInformationBuffer(
                       token,
                       WindowsNative.TokenCapabilities,
                       buffer,
                       requiredBytes,
                       out var returnedBytes) &&
                   returnedBytes >= sizeof(int) &&
                   Marshal.ReadInt32(buffer) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool ProbeNetworkDenied(int loopbackProbePort)
    {
        if (loopbackProbePort is <= 0 or > ushort.MaxValue)
        {
            return false;
        }

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var deadline = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(ExternalWorkerPolicy.MaximumNetworkProbeMilliseconds));
        try
        {
            socket.ConnectAsync(IPAddress.Loopback, loopbackProbePort, deadline.Token)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            return false;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return true;
        }
    }

    private static bool RequestIsValid(ExternalDumpQueryRequest request) =>
        !string.IsNullOrWhiteSpace(request.RootTypeName) &&
        request.RootTypeName.Length <= ExternalWorkerPolicy.MaximumRootTypeNameCharacters &&
        !string.IsNullOrWhiteSpace(request.RootName) &&
        request.RootName.Length <= ExternalWorkerPolicy.MaximumRootNameCharacters &&
        request.Expression is not null &&
        request.Expression.Length <= ExternalWorkerPolicy.MaximumExpressionCharacters;

    private static bool TryParseHandles(
        string[] args,
        out IntPtr artifactHandle,
        out IntPtr requestHandle,
        out IntPtr responseHandle)
    {
        artifactHandle = IntPtr.Zero;
        requestHandle = IntPtr.Zero;
        responseHandle = IntPtr.Zero;
        if (args.Length != 7 || !string.Equals(args[0], "--worker", StringComparison.Ordinal))
        {
            return false;
        }

        return TryReadHandle(args, 1, "--artifact-handle", out artifactHandle) &&
               TryReadHandle(args, 3, "--request-handle", out requestHandle) &&
               TryReadHandle(args, 5, "--response-handle", out responseHandle) &&
               artifactHandle != requestHandle && artifactHandle != responseHandle && requestHandle != responseHandle;
    }

    private static bool TryReadHandle(string[] args, int index, string name, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (!string.Equals(args[index], name, StringComparison.Ordinal) ||
            !long.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value <= 0)
        {
            return false;
        }

        handle = new IntPtr(value);
        return true;
    }

    private static bool IsNormalizedWorkerFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or
            BadImageFormatException or InvalidOperationException or ArgumentException or
            NotSupportedException or OverflowException or CryptographicException or ExternalException;
}
