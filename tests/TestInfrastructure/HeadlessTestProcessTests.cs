using Xunit;

namespace PhoenixInspect.Tests.Infrastructure;

/// <summary>Verifies that each managed test host is unable to display Windows failure-reporting dialogs.</summary>
public sealed class HeadlessTestProcessTests
{
    /// <summary>Verifies the execution-boundary policy, per-process WER suppression, and .NET host policy.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Test_host_disables_failure_dialogs()
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("DOTNET_DISABLE_GUI_ERRORS"));
        Assert.True(HeadlessTestFramework.IsExecutionPolicyInitialized);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var errorMode = HeadlessTestProcess.GetCurrentErrorMode();
        Assert.Equal(
            HeadlessTestProcess.SuppressedErrorModeMask,
            errorMode & HeadlessTestProcess.SuppressedErrorModeMask);

        var werFlags = HeadlessTestProcess.GetCurrentWerFlags();
        Assert.Equal(HeadlessTestProcess.WerNoUi, werFlags & HeadlessTestProcess.WerNoUi);
        Assert.Equal(0u, werFlags & HeadlessTestProcess.WerAlwaysShowUi);
    }
}
