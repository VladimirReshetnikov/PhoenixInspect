using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.NETCore.Client;

namespace Interpreter.IntegrationTests;

internal static class TestTargetPaths
{
    public static string ResolveExecutable()
    {
        var testsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var configuration = ResolveBuildConfiguration();
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var executableFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Interpreter.TestTarget.exe"
            : "Interpreter.TestTarget";

        return Path.Combine(
            testsRoot,
            "Interpreter.TestTarget",
            "bin",
            configuration,
            targetFramework,
            executableFileName);
    }

    public static string ResolveAssembly(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException("Could not determine the target executable directory.");
        return Path.Combine(directory, "Interpreter.TestTarget.dll");
    }

    private static string ResolveBuildConfiguration()
    {
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (baseDirectory is not null)
        {
            if (string.Equals(baseDirectory.Name, "Debug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(baseDirectory.Name, "Release", StringComparison.OrdinalIgnoreCase))
            {
                return baseDirectory.Name;
            }

            baseDirectory = baseDirectory.Parent;
        }

        return "Debug";
    }
}

internal sealed class TestTargetHarnessException : InvalidOperationException
{
    internal TestTargetHarnessException(
        string code,
        string message,
        string isolatedDirectory,
        int? targetProcessId)
        : base(message)
    {
        Code = code;
        IsolatedDirectory = isolatedDirectory;
        TargetProcessId = targetProcessId;
    }

    internal string Code { get; }

    internal string IsolatedDirectory { get; }

    internal int? TargetProcessId { get; }
}

internal sealed class TestTargetRunner : IDisposable
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);
    private readonly Process _process;
    private readonly Task<string> _stderrTask;
    private readonly string _isolatedDirectory;
    private int _disposed;

    private TestTargetRunner(Process process, Task<string> stderrTask, string isolatedDirectory)
    {
        _process = process;
        _stderrTask = stderrTask;
        _isolatedDirectory = isolatedDirectory;
    }

    public int Pid => _process.Id;

    public static TestTargetRunner StartAndWaitReady(string executablePath) =>
        StartAndWaitReady(executablePath, Array.Empty<string>(), isolatedDirectory: null);

    internal static TestTargetRunner StartAndWaitReady(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? isolatedDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        foreach (var argument in arguments)
        {
            ArgumentNullException.ThrowIfNull(argument);
        }

        var ownedDirectory = CreateIsolatedDirectory(isolatedDirectory);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = ownedDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.Environment["INTERPRETER_TEST_SECRET_CANARY"] =
            "must-not-enter-the-full-dump";
        ConfigureIsolatedEnvironment(process.StartInfo, ownedDirectory);

        bool started;
        try
        {
            started = process.Start();
        }
        catch (Exception exception) when (IsProcessStartFailure(exception))
        {
            throw CreateFailureAfterCleanup(
                process,
                stderrTask: null,
                ownedDirectory,
                started: false,
                targetProcessId: null,
                "HARNESS_TARGET_START_FAILED",
                "The dump test target could not be started.");
        }

        if (!started)
        {
            throw CreateFailureAfterCleanup(
                process,
                stderrTask: null,
                ownedDirectory,
                started: false,
                targetProcessId: null,
                "HARNESS_TARGET_START_FAILED",
                "The dump test target could not be started.");
        }

        var targetProcessId = process.Id;
        var stderrTask = process.StandardError.ReadToEndAsync();
        string? line;
        Exception? readinessError = null;
        try
        {
            line = process.StandardOutput.ReadLineAsync().WaitAsync(ReadyTimeout).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            line = null;
            readinessError = exception;
        }

        if (string.Equals(line?.Trim(), "READY", StringComparison.Ordinal))
        {
            return new TestTargetRunner(process, stderrTask, ownedDirectory);
        }

        var (code, message) = ClassifyReadinessFailure(line, readinessError);
        throw CreateFailureAfterCleanup(
            process,
            stderrTask,
            ownedDirectory,
            started: true,
            targetProcessId,
            code,
            message);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var targetProcessId = TryGetProcessId(_process);
        if (!Cleanup(_process, _stderrTask, _isolatedDirectory, started: true))
        {
            throw new TestTargetHarnessException(
                "HARNESS_TARGET_CLEANUP_FAILED",
                "The dump test target could not be cleaned up completely.",
                _isolatedDirectory,
                targetProcessId);
        }
    }

    private static void ConfigureIsolatedEnvironment(ProcessStartInfo startInfo, string isolatedDirectory)
    {
        startInfo.Environment.Clear();
        CopyEnvironmentVariableIfPresent(startInfo, "SystemRoot");
        CopyEnvironmentVariableIfPresent(startInfo, "WINDIR");
        startInfo.Environment["TEMP"] = isolatedDirectory;
        startInfo.Environment["TMP"] = isolatedDirectory;
        startInfo.Environment["DOTNET_EnableDiagnostics"] = "1";
    }

    private static void CopyEnvironmentVariableIfPresent(ProcessStartInfo startInfo, string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(value))
        {
            startInfo.Environment[name] = value;
        }
    }

    private static string CreateIsolatedDirectory(string? requestedDirectory)
    {
        var candidate = NormalizeIsolatedDirectory(
            requestedDirectory ?? Path.Combine(
                Path.GetTempPath(),
                $"interpreter-dump-target-{Guid.NewGuid():N}"));
        if (Directory.Exists(candidate))
        {
            throw new ArgumentException("The isolated target directory must not already exist.", nameof(requestedDirectory));
        }

        Directory.CreateDirectory(candidate);
        return candidate;
    }

    private static (string Code, string Message) ClassifyReadinessFailure(
        string? readinessLine,
        Exception? readinessError)
    {
        if (readinessError is TimeoutException)
        {
            return (
                "HARNESS_TARGET_READY_TIMEOUT",
                "The dump test target did not report readiness within the bounded startup window.");
        }

        if (readinessError is not null)
        {
            return (
                "HARNESS_TARGET_READY_FAILED",
                "The dump test target readiness channel failed before reporting readiness.");
        }

        return readinessLine is null
            ? (
                "HARNESS_TARGET_EARLY_EXIT",
                "The dump test target exited before reporting readiness.")
            : (
                "HARNESS_TARGET_READY_INVALID",
                "The dump test target reported an invalid readiness marker.");
    }

    private static bool IsProcessStartFailure(Exception exception) =>
        exception is Win32Exception or InvalidOperationException or ObjectDisposedException or PlatformNotSupportedException;

    private static TestTargetHarnessException CreateFailureAfterCleanup(
        Process process,
        Task<string>? stderrTask,
        string isolatedDirectory,
        bool started,
        int? targetProcessId,
        string code,
        string message)
    {
        if (!Cleanup(process, stderrTask, isolatedDirectory, started))
        {
            return new TestTargetHarnessException(
                "HARNESS_TARGET_CLEANUP_FAILED",
                "The dump test target could not be cleaned up completely.",
                isolatedDirectory,
                targetProcessId);
        }

        return new TestTargetHarnessException(code, message, isolatedDirectory, targetProcessId);
    }

    private static bool Cleanup(
        Process process,
        Task<string>? stderrTask,
        string isolatedDirectory,
        bool started)
    {
        var processStopped = !started || TryTerminate(process);
        if (stderrTask is not null)
        {
            CompleteStandardError(stderrTask);
        }

        try
        {
            process.Dispose();
        }
        catch
        {
            processStopped = false;
        }

        var directoryDeleted = TryDeleteIsolatedDirectory(isolatedDirectory);
        return processStopped && directoryDeleted;
    }

    private static bool TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return process.WaitForExit((int)ExitTimeout.TotalMilliseconds);
        }
        catch
        {
            return false;
        }
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return null;
        }
    }

    private static void CompleteStandardError(Task<string> stderrTask)
    {
        try
        {
            _ = stderrTask.WaitAsync(ExitTimeout).GetAwaiter().GetResult();
        }
        catch
        {
            // Stderr is drained only to release redirected-pipe resources. It is never copied into failure output.
        }
    }

    private static bool TryDeleteIsolatedDirectory(string isolatedDirectory)
    {
        try
        {
            var candidate = NormalizeIsolatedDirectory(isolatedDirectory);

            if (Directory.Exists(candidate))
            {
                Directory.Delete(candidate, recursive: true);
            }

            return !Directory.Exists(candidate);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeIsolatedDirectory(string isolatedDirectory)
    {
        var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(isolatedDirectory));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var tempPrefix = tempRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(tempPrefix, comparison))
        {
            throw new InvalidOperationException("Refusing to use a target directory outside the temporary root.");
        }

        return candidate;
    }
}

internal static class DumpWriter
{
    public static void WriteFullDump(int pid, string dumpPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);
        var diagnosticsClient = new DiagnosticsClient(pid);
        diagnosticsClient.WriteDump(DumpType.Full, dumpPath, logDumpGeneration: false);
    }
}
