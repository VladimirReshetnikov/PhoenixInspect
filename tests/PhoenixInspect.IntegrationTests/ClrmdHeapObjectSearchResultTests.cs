using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Verifies predicate and snapshot attribution for bounded heap-root searches that retain no object.</summary>
public sealed class ClrmdHeapObjectSearchResultTests
{
    /// <summary>Checks that an empty unavailable result still identifies its exact predicate, counters, and snapshot.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Empty_search_result_retains_snapshot_identity()
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
        var result = new ClrmdHeapObjectSearchResult(
            snapshot,
            "Missing.Namespace.Probe",
            ClrmdEvidenceStatus.Unavailable,
            ClrmdValueIssue.ObjectUnavailable,
            handlesScanned: 0,
            maximumHandlesScanned: 10,
            maximumMatches: 1,
            matchLimitReached: false,
            matches: ImmutableArray<ClrmdHeapObjectInfo>.Empty,
            evidence: ImmutableArray<MemoryReadResult>.Empty);

        Assert.Equal(snapshot, result.Snapshot);
        Assert.Equal("Missing.Namespace.Probe", result.TypeNameSelector);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, result.Status);
        Assert.Equal(0, result.HandlesScanned);
        Assert.Equal(10, result.MaximumHandlesScanned);
        Assert.Equal(1, result.MaximumMatches);
        Assert.Equal(0, result.MatchesRetained);
        Assert.False(result.MatchLimitReached);
        Assert.Empty(result.Matches);
        Assert.Empty(result.Evidence);
    }
}
