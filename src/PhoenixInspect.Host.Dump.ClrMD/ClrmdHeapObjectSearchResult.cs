using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Reports the exact type predicate and outcome of a bounded strong-handle search without implying that a truncated
/// traversal was exhaustive.
/// </summary>
public sealed class ClrmdHeapObjectSearchResult
{
    internal ClrmdHeapObjectSearchResult(
        ClrmdSnapshotIdentity snapshot,
        string typeNameSelector,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        int handlesScanned,
        int maximumHandlesScanned,
        int maximumMatches,
        bool matchLimitReached,
        ImmutableArray<ClrmdHeapObjectInfo> matches,
        ImmutableArray<MemoryReadResult> evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeNameSelector);
        Snapshot = snapshot;
        TypeNameSelector = typeNameSelector;
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
    /// Gets the exact ordinal runtime type-name predicate used to select candidates.
    /// </summary>
    /// <remarks>
    /// The adapter preserves the validated caller input without case folding or display-name substitution. Retaining
    /// this predicate keeps exhaustive absence and bounded partial results attributable even when no object carries
    /// the requested type name into <see cref="Matches"/>.
    /// </remarks>
    public string TypeNameSelector { get; }

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
    /// Gets the number of validated candidates retained in <see cref="Matches"/>.
    /// </summary>
    public int MatchesRetained => Matches.Length;

    /// <summary>
    /// Gets bounded matches sorted by object address and then root address.
    /// </summary>
    public ImmutableArray<ClrmdHeapObjectInfo> Matches { get; }

    /// <summary>
    /// Gets ordered raw handle-slot and object-header reads used to validate candidate root references and types.
    /// </summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }
}
