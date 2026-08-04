using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Freezes the process-discovery surface the Processes pane renders: a running .NET target is detected with
/// evidence naming how, the inspector's own process is excluded, unmanaged processes are not offered, and the
/// filter contract matches by name or id.
/// </summary>
public sealed class ProcessDiscoveryTests
{
    /// <summary>Proves the filter admits by name or id, and a blank filter admits everything.</summary>
    /// <param name="filter">The filter text.</param>
    /// <param name="expected">Whether the sample candidate matches.</param>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("contoso", true)]
    [InlineData("CONTOSO", true)]
    [InlineData("4088", true)]
    [InlineData("408", true)]
    [InlineData("notepad", false)]
    [InlineData("9999", false)]
    public void Filter_matches_name_or_process_id(string? filter, bool expected)
    {
        var candidate = new ProcessCandidate(
            ProcessId: 4088,
            Name: "Contoso.OrderService",
            Evidence: ManagedRuntimeEvidence.DiagnosticsEndpoint,
            StartedAtUtc: DateTime.UtcNow,
            ExecutablePath: @"C:\app\Contoso.OrderService.exe",
            IsArchitectureCompatible: true);
        Assert.Equal(expected, ProcessDiscoveryService.Matches(candidate, filter));
    }

    /// <summary>Proves attachability and its stated reason follow the evidence and the bitness.</summary>
    [Fact]
    public void Attachability_follows_evidence_and_architecture()
    {
        var managed = new ProcessCandidate(1, "a", ManagedRuntimeEvidence.DiagnosticsEndpoint, null, null, true);
        Assert.True(managed.IsAttachable);
        Assert.Contains("diagnostics endpoint", managed.Note, StringComparison.Ordinal);

        var byModule = new ProcessCandidate(2, "b", ManagedRuntimeEvidence.LoadedRuntimeModule, null, null, true);
        Assert.True(byModule.IsAttachable);
        Assert.Contains("runtime module", byModule.Note, StringComparison.Ordinal);

        var wrongBitness = new ProcessCandidate(3, "c", ManagedRuntimeEvidence.DiagnosticsEndpoint, null, null, false);
        Assert.False(wrongBitness.IsAttachable);
        Assert.Contains("bitness", wrongBitness.Note, StringComparison.Ordinal);

        var unmanaged = new ProcessCandidate(4, "d", ManagedRuntimeEvidence.None, null, null, true);
        Assert.False(unmanaged.IsAttachable);
        Assert.Contains("No managed runtime", unmanaged.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves enumeration finds a real running .NET process, names how it was recognised, and never offers the
    /// inspector's own process as a target.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Enumeration_detects_a_running_dotnet_target()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        using var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null);

        var candidates = ProcessDiscoveryService.ListCandidates();
        Assert.DoesNotContain(candidates, candidate => candidate.ProcessId == Environment.ProcessId);

        var found = Assert.Single(candidates.Where(candidate => candidate.ProcessId == target.Pid));
        Assert.NotEqual(ManagedRuntimeEvidence.None, found.Evidence);
        Assert.True(found.IsAttachable);
        Assert.Equal("Contoso.OrderService", found.Name);

        // Every listed candidate carries managed-runtime evidence: unmanaged processes are not offered at all.
        Assert.All(candidates, static candidate => Assert.NotEqual(ManagedRuntimeEvidence.None, candidate.Evidence));

        // Attachable candidates sort ahead of unattachable ones, so the useful rows are at the top.
        var firstUnattachable = candidates.ToList().FindIndex(static candidate => !candidate.IsAttachable);
        var lastAttachable = candidates.ToList().FindLastIndex(static candidate => candidate.IsAttachable);
        if (firstUnattachable >= 0 && lastAttachable >= 0)
        {
            Assert.True(lastAttachable < firstUnattachable);
        }
    }
}
