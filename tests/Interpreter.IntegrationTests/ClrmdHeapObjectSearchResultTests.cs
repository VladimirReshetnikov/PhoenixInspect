using System.Collections.Immutable;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Verifies snapshot attribution for bounded heap-root searches even when they retain no object.</summary>
public sealed class ClrmdHeapObjectSearchResultTests
{
    /// <summary>Checks that an empty unavailable result still identifies the immutable snapshot it describes.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Empty_search_result_retains_snapshot_identity()
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
        var result = new ClrmdHeapObjectSearchResult(
            snapshot,
            ClrmdEvidenceStatus.Unavailable,
            ClrmdValueIssue.ObjectUnavailable,
            handlesScanned: 0,
            maximumHandlesScanned: 10,
            maximumMatches: 1,
            matchLimitReached: false,
            matches: ImmutableArray<ClrmdHeapObjectInfo>.Empty,
            evidence: ImmutableArray<MemoryReadResult>.Empty);

        Assert.Equal(snapshot, result.Snapshot);
        Assert.Empty(result.Matches);
        Assert.Empty(result.Evidence);
    }
}
