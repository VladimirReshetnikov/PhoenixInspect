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

internal sealed class TestTargetRunner : IDisposable
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);
    private readonly Process _process;
    private readonly Task<string> _stderrTask;
    private readonly string _isolatedDirectory;

    private TestTargetRunner(Process process, Task<string> stderrTask, string isolatedDirectory)
    {
        _process = process;
        _stderrTask = stderrTask;
        _isolatedDirectory = isolatedDirectory;
    }

    public int Pid => _process.Id;

    public static TestTargetRunner StartAndWaitReady(string executablePath)
    {
        var isolatedDirectory = Path.Combine(
            Path.GetTempPath(),
            $"interpreter-dump-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(isolatedDirectory);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = isolatedDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.Environment["INTERPRETER_TEST_SECRET_CANARY"] =
            "must-not-enter-the-full-dump";
        ConfigureIsolatedEnvironment(process.StartInfo, isolatedDirectory);

        if (!process.Start())
        {
            process.Dispose();
            DeleteIsolatedDirectory(isolatedDirectory);
            throw new InvalidOperationException($"Failed to start test target process '{executablePath}'.");
        }

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
            return new TestTargetRunner(process, stderrTask, isolatedDirectory);
        }

        Terminate(process);
        var stderr = CompleteStandardError(stderrTask);
        process.Dispose();
        DeleteIsolatedDirectory(isolatedDirectory);

        var detail = line is null
            ? "no readiness line was received"
            : $"received '{line}'";
        throw new InvalidOperationException(
            $"Timed out or failed waiting for READY from target process ({detail}). Stderr: {stderr}",
            readinessError);
    }

    public void Dispose()
    {
        Terminate(_process);
        _ = CompleteStandardError(_stderrTask);
        _process.Dispose();
        DeleteIsolatedDirectory(_isolatedDirectory);
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

    private static void Terminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit((int)ExitTimeout.TotalMilliseconds);
        }
        catch
        {
            // Best-effort cleanup for a test child process.
        }
    }

    private static string CompleteStandardError(Task<string> stderrTask)
    {
        try
        {
            return stderrTask.WaitAsync(ExitTimeout).GetAwaiter().GetResult();
        }
        catch
        {
            return "<stderr unavailable>";
        }
    }

    private static void DeleteIsolatedDirectory(string isolatedDirectory)
    {
        try
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var candidate = Path.GetFullPath(isolatedDirectory);
            if (!candidate.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate, tempRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a target directory outside the temporary root.");
            }

            if (Directory.Exists(candidate))
            {
                Directory.Delete(candidate, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for an isolated, non-sensitive test directory.
        }
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
