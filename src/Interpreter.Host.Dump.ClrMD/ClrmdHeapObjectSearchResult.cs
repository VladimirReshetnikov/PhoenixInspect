using System.Collections.Immutable;
using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Reports a bounded strong-handle search without implying that a truncated traversal was exhaustive.
/// </summary>
public sealed class ClrmdHeapObjectSearchResult
{
    internal ClrmdHeapObjectSearchResult(
        ClrmdSnapshotIdentity snapshot,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        int handlesScanned,
        int maximumHandlesScanned,
        int maximumMatches,
        bool matchLimitReached,
        ImmutableArray<ClrmdHeapObjectInfo> matches,
        ImmutableArray<MemoryReadResult> evidence)
    {
        Snapshot = snapshot;
        Status = status;
        Issue = issue;
        HandlesScanned = handlesScanned;
        MaximumHandlesScanned = maximumHandlesScanned;
        MaximumMatches = maximumMatches;
        MatchLimitReached = matchLimitReached;
        Matches = matches;
        Evidence = evidence;
    }

    /// <summary>
    /// Gets the immutable dump identity whose handle catalog produced this result, including an exhaustive or
    /// truncated result with no retained matches.
    /// </summary>
    /// <remarks>
    /// Hosts must validate this identity before reusing absence, ambiguity, or truncation evidence in another
    /// evaluation session; an empty match set does not otherwise carry object-level snapshot identity.
    /// </remarks>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>
    /// Gets whether handle traversal was exhaustive, budget-truncated, or invalidated by corrupt runtime evidence.
    /// </summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>
    /// Gets the stable reason for a non-exact search.
    /// </summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>
    /// Gets the number of runtime handles inspected by this operation.
    /// </summary>
    public int HandlesScanned { get; }

    /// <summary>
    /// Gets the caller-supplied upper bound on inspected runtime handles.
    /// </summary>
    public int MaximumHandlesScanned { get; }

    /// <summary>
    /// Gets the caller-supplied upper bound on retained matches.
    /// </summary>
    public int MaximumMatches { get; }

    /// <summary>
    /// Gets whether traversal found an additional match beyond the retained-match bound.
    /// </summary>
    public bool MatchLimitReached { get; }

    /// <summary>
    /// Gets bounded matches sorted by object address and then root address.
    /// </summary>
    public ImmutableArray<ClrmdHeapObjectInfo> Matches { get; }

    /// <summary>
    /// Gets ordered raw handle-slot and object-header reads used to validate candidate root references and types.
    /// </summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }
}
