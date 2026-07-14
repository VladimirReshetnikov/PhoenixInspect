using System.Diagnostics;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises bounded, payload-safe failure cleanup in the generated-dump target harness.</summary>
public sealed class DumpTestInfrastructureTests
{
    /// <summary>Checks that a platform start failure is normalized and removes its owned directory.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Unstartable_target_has_stable_failure_and_transactional_cleanup()
    {
        var isolatedDirectory = NewIsolatedDirectoryPath();
        var missingExecutable = Path.Combine(
            Path.GetTempPath(),
            $"missing-interpreter-target-{Guid.NewGuid():N}.exe");

        var failure = Assert.Throws<TestTargetHarnessException>(() =>
            TestTargetRunner.StartAndWaitReady(
                missingExecutable,
                Array.Empty<string>(),
                isolatedDirectory));

        AssertFailure(
            failure,
            "HARNESS_TARGET_START_FAILED",
            "The dump test target could not be started.",
            isolatedDirectory);
        Assert.Null(failure.TargetProcessId);
        Assert.DoesNotContain(missingExecutable, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks that an invalid readiness marker terminates the still-running target without echoing output.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Invalid_readiness_marker_is_payload_safe_and_leaves_no_orphan()
    {
        var isolatedDirectory = NewIsolatedDirectoryPath();
        var executablePath = TestTargetPaths.ResolveExecutable();

        var failure = Assert.Throws<TestTargetHarnessException>(() =>
            TestTargetRunner.StartAndWaitReady(
                executablePath,
                ["--harness-invalid-readiness"],
                isolatedDirectory));

        AssertFailure(
            failure,
            "HARNESS_TARGET_READY_INVALID",
            "The dump test target reported an invalid readiness marker.",
            isolatedDirectory);
        Assert.DoesNotContain("secret-readiness-marker-canary", failure.Message, StringComparison.Ordinal);
        AssertNoLiveProcess(failure.TargetProcessId);
    }

    /// <summary>Checks that an early target exit has a stable code and never exposes redirected stderr.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Early_exit_is_payload_safe_and_leaves_no_process_or_directory()
    {
        var isolatedDirectory = NewIsolatedDirectoryPath();
        var executablePath = TestTargetPaths.ResolveExecutable();

        var failure = Assert.Throws<TestTargetHarnessException>(() =>
            TestTargetRunner.StartAndWaitReady(
                executablePath,
                ["--harness-exit-before-ready"],
                isolatedDirectory));

        AssertFailure(
            failure,
            "HARNESS_TARGET_EARLY_EXIT",
            "The dump test target exited before reporting readiness.",
            isolatedDirectory);
        Assert.DoesNotContain("secret-readiness-stderr-canary", failure.Message, StringComparison.Ordinal);
        AssertNoLiveProcess(failure.TargetProcessId);
    }

    /// <summary>Checks that a target which never reports readiness is bounded, classified, and terminated.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Readiness_timeout_is_stable_and_leaves_no_orphan()
    {
        var isolatedDirectory = NewIsolatedDirectoryPath();
        var executablePath = TestTargetPaths.ResolveExecutable();

        var failure = Assert.Throws<TestTargetHarnessException>(() =>
            TestTargetRunner.StartAndWaitReady(
                executablePath,
                ["--harness-never-ready"],
                isolatedDirectory,
                readinessTimeout: TimeSpan.FromMilliseconds(250)));

        AssertFailure(
            failure,
            "HARNESS_TARGET_READY_TIMEOUT",
            "The dump test target did not report readiness within the bounded startup window.",
            isolatedDirectory);
        AssertNoLiveProcess(failure.TargetProcessId);
    }

    /// <summary>Checks that a readiness-channel fault is normalized without disclosing exception payload.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Readiness_channel_failure_is_payload_safe_and_leaves_no_orphan()
    {
        var isolatedDirectory = NewIsolatedDirectoryPath();
        var executablePath = TestTargetPaths.ResolveExecutable();

        var failure = Assert.Throws<TestTargetHarnessException>(() =>
            TestTargetRunner.StartAndWaitReady(
                executablePath,
                ["--harness-never-ready"],
                isolatedDirectory,
                readReadinessLineAsync: static _ =>
                    Task.FromException<string?>(new IOException("secret-read-channel-canary"))));

        AssertFailure(
            failure,
            "HARNESS_TARGET_READY_FAILED",
            "The dump test target readiness channel failed before reporting readiness.",
            isolatedDirectory);
        Assert.DoesNotContain("secret-read-channel-canary", failure.Message, StringComparison.Ordinal);
        AssertNoLiveProcess(failure.TargetProcessId);
    }

    private static string NewIsolatedDirectoryPath() =>
        Path.Combine(Path.GetTempPath(), $"interpreter-harness-negative-{Guid.NewGuid():N}");

    private static void AssertFailure(
        TestTargetHarnessException failure,
        string expectedCode,
        string expectedMessage,
        string isolatedDirectory)
    {
        Assert.Equal(expectedCode, failure.Code);
        Assert.Equal(expectedMessage, failure.Message);
        Assert.Equal(Path.GetFullPath(isolatedDirectory), failure.IsolatedDirectory);
        Assert.False(Directory.Exists(isolatedDirectory));
    }

    private static void AssertNoLiveProcess(int? processId)
    {
        Assert.NotNull(processId);
        try
        {
            using var process = Process.GetProcessById(processId.Value);
            Assert.True(process.HasExited, $"Target process {processId.Value} remained alive after harness failure.");
        }
        catch (ArgumentException)
        {
            // The operating system has already reaped the process, which is the expected no-orphan outcome.
        }
    }
}
