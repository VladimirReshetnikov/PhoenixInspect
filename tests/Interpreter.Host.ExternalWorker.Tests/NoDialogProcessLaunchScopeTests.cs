using System.Diagnostics;
using Interpreter.Host.ExternalWorker;
using Xunit;

namespace Interpreter.Host.ExternalWorker.Tests;

/// <summary>Verifies the inherited no-dialog policy used by external-worker process creation.</summary>
public sealed class NoDialogProcessLaunchScopeTests
{
    /// <summary>Requires the native Win32, thread, and WER policy to be observable while the scope is active.</summary>
    [Fact]
    public void ScopeAppliesEffectiveWin32ThreadAndWerPolicy()
    {
        Assert.True(OperatingSystem.IsWindows());

        using var scope = NoDialogProcessLaunchScope.Enter();

        Assert.True(NoDialogProcessLaunchScope.IsCurrentPolicyApplied());
        Assert.Equal(
            NoDialogProcessLaunchScope.RequiredErrorMode,
            WindowsNative.GetErrorMode() & NoDialogProcessLaunchScope.RequiredErrorMode);
        Assert.Equal(
            NoDialogProcessLaunchScope.RequiredErrorMode,
            WindowsNative.GetThreadErrorMode() & NoDialogProcessLaunchScope.RequiredErrorMode);
        Assert.Equal(0, WindowsNative.WerGetFlags(WindowsNative.GetCurrentProcess(), out var werFlags));
        Assert.NotEqual(0u, werFlags & WindowsNative.WerFaultReportingNoUi);
        Assert.Equal(0u, werFlags & WindowsNative.WerFaultReportingAlwaysShowUi);
    }

    /// <summary>Requires a pre-runtime .NET host failure to exit without waiting for interactive error UI.</summary>
    [Fact]
    public void IncompleteDotnetAppHostFailsWithoutWaitingForInteractiveUi()
    {
        Assert.True(OperatingSystem.IsWindows());

        var sourceAppHost = Path.Combine(AppContext.BaseDirectory, "testhost.exe");
        Assert.True(File.Exists(sourceAppHost), "The Windows testhost apphost is required for this regression.");
        var directory = Path.Combine(Path.GetTempPath(), $"interpreter-headless-apphost-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var isolatedAppHost = Path.Combine(directory, "incomplete-testhost.exe");
            File.Copy(sourceAppHost, isolatedAppHost);
            var start = new ProcessStartInfo(isolatedAppHost)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                ErrorDialog = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = directory,
            };
            start.Environment.Clear();
            start.Environment["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot")!;
            start.Environment["WINDIR"] = Environment.GetEnvironmentVariable("WINDIR")!;
            start.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";

            using var scope = NoDialogProcessLaunchScope.Enter();
            using var process = Process.Start(start);
            Assert.NotNull(process);
            if (!process.WaitForExit(milliseconds: 10_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.True(
                    process.WaitForExit(milliseconds: 10_000),
                    "The killed incomplete apphost did not reach a terminal state.");
                Assert.Fail("An incomplete .NET apphost waited for interactive error UI.");
            }

            Assert.NotEqual(0, process.ExitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
