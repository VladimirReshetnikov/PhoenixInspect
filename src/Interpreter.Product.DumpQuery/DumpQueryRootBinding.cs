using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies the evidence available for one host-provided dump-query root name.</summary>
public enum DumpQueryRootBindingStatus
{
    /// <summary>One exact object was selected from exhaustive, compatible evidence.</summary>
    ExactObject,

    /// <summary>An exhaustive search completed and proved that no matching object exists.</summary>
    ExhaustiveAbsence,

    /// <summary>The search ended with partial evidence and therefore cannot select a unique exact object.</summary>
    Partial,

    /// <summary>The required root evidence was unavailable.</summary>
    Unavailable,

    /// <summary>Available evidence was ambiguous or incompatible.</summary>
    Conflict,

    /// <summary>Captured evidence violated a supported structural invariant.</summary>
    Invalid,
}

/// <summary>
/// Carries one named root-binding outcome without collapsing exhaustive absence, partial traversal, conflict, or
/// invalid evidence into a nullable object reference.
/// </summary>
/// <remarks>
/// This is a draft W2 contract for the deliberately single-root grammar. A non-exact binding never exposes one of a
/// partial search's matches as though it were uniquely selected. Raw evidence and applied search bounds remain
/// available so preparation failures can preserve the explanation produced before query binding began.
/// </remarks>
public sealed class DumpQueryRootBinding
{
    private static readonly string MaximumHandlesBoundName = "root-selection.maximum-handles";
    private static readonly string MaximumMatchesBoundName = "root-selection.maximum-matches";

    private DumpQueryRootBinding(
        string? name,
        ClrmdSnapshotIdentity snapshot,
        DumpQueryRootBindingStatus status,
        ClrmdHeapObjectInfo? root,
        ClrmdValueIssue issue,
        ImmutableArray<MemoryReadResult> evidence,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds)
    {
        Name = name;
        Snapshot = snapshot;
        Status = status;
        Root = root;
        Issue = issue;
        Evidence = evidence;
        AppliedBounds = NormalizeBounds(appliedBounds);
    }

    /// <summary>
    /// Gets the configured root identifier. Invalid or missing names are retained so the parser can return its stable
    /// admission diagnostic rather than a constructor exception.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the immutable dump identity from which this root-selection evidence was obtained, including when the
    /// outcome contains no object. Preparation rejects a binding from any other snapshot before member selection.
    /// </summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the typed root-selection outcome.</summary>
    public DumpQueryRootBindingStatus Status { get; }

    /// <summary>
    /// Gets the exact selected object when <see cref="Status"/> is
    /// <see cref="DumpQueryRootBindingStatus.ExactObject"/>; otherwise, gets <see langword="null"/>.
    /// </summary>
    public ClrmdHeapObjectInfo? Root { get; }

    /// <summary>
    /// Gets the adapter issue explaining a partial, unavailable, conflicting, or invalid search. Exact selection and
    /// exhaustive absence use <see cref="ClrmdValueIssue.None"/>.
    /// </summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>Gets ordered raw reads retained by the root-selection operation.</summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }

    /// <summary>Gets only deterministic root-selection bounds that the producing operation actually applied.</summary>
    public ImmutableArray<EvaluationDeterministicBound> AppliedBounds { get; }

    /// <summary>Creates an exact binding for an object already selected by the host.</summary>
    /// <param name="name">The case-sensitive identifier assigned to the object, or invalid input for parser diagnostics.</param>
    /// <param name="root">The exact dump object selected by the host.</param>
    /// <param name="appliedBounds">
    /// Bounds actually applied while selecting <paramref name="root"/>. A default array means that no upstream bound
    /// is claimed; intended but unenforced limits must not be supplied.
    /// </param>
    /// <returns>An exact binding retaining the object's root-selection reads and supplied bounds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="appliedBounds"/> contains a null or duplicate name.</exception>
    public static DumpQueryRootBinding FromExactObject(
        string? name,
        ClrmdHeapObjectInfo root,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new DumpQueryRootBinding(
            name,
            root.Snapshot,
            DumpQueryRootBindingStatus.ExactObject,
            root,
            ClrmdValueIssue.None,
            root.Evidence,
            appliedBounds.IsDefault ? ImmutableArray<EvaluationDeterministicBound>.Empty : appliedBounds);
    }

    /// <summary>
    /// Converts one bounded strong-handle search into a unique-root binding without treating a partial match prefix as
    /// exhaustive or arbitrarily choosing among multiple matches.
    /// </summary>
    /// <param name="name">The case-sensitive identifier assigned to the requested root.</param>
    /// <param name="search">The completed adapter search to classify.</param>
    /// <returns>
    /// An exact object only for one match from an exact search; exact zero-match evidence becomes exhaustive absence,
    /// exact multiple matches become conflict, and every non-exact search preserves its status, issue, reads, and caps.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="search"/> is <see langword="null"/>.</exception>
    public static DumpQueryRootBinding FromSearchResult(string? name, ClrmdHeapObjectSearchResult search)
    {
        ArgumentNullException.ThrowIfNull(search);
        var bounds = ImmutableArray.Create(
            new EvaluationDeterministicBound(MaximumHandlesBoundName, search.MaximumHandlesScanned),
            new EvaluationDeterministicBound(MaximumMatchesBoundName, search.MaximumMatches));

        if (search.Status == ClrmdEvidenceStatus.Exact)
        {
            return search.Matches.Length switch
            {
                0 => new DumpQueryRootBinding(
                    name,
                    search.Snapshot,
                    DumpQueryRootBindingStatus.ExhaustiveAbsence,
                    null,
                    ClrmdValueIssue.None,
                    search.Evidence,
                    bounds),
                1 => new DumpQueryRootBinding(
                    name,
                    search.Snapshot,
                    DumpQueryRootBindingStatus.ExactObject,
                    search.Matches[0],
                    ClrmdValueIssue.None,
                    search.Evidence,
                    bounds),
                _ => new DumpQueryRootBinding(
                    name,
                    search.Snapshot,
                    DumpQueryRootBindingStatus.Conflict,
                    null,
                    ClrmdValueIssue.AmbiguousMatch,
                    search.Evidence,
                    bounds),
            };
        }

        var status = search.Status switch
        {
            ClrmdEvidenceStatus.Partial => DumpQueryRootBindingStatus.Partial,
            ClrmdEvidenceStatus.Unavailable => DumpQueryRootBindingStatus.Unavailable,
            ClrmdEvidenceStatus.Conflict => DumpQueryRootBindingStatus.Conflict,
            ClrmdEvidenceStatus.Invalid => DumpQueryRootBindingStatus.Invalid,
            _ => throw new ArgumentOutOfRangeException(nameof(search)),
        };
        return new DumpQueryRootBinding(
            name,
            search.Snapshot,
            status,
            null,
            search.Issue,
            search.Evidence,
            bounds);
    }

    internal static DumpQueryRootBinding CreateUnavailable(
        string? name,
        ClrmdSnapshotIdentity snapshot) => new(
        name,
        snapshot,
        DumpQueryRootBindingStatus.Unavailable,
        null,
        ClrmdValueIssue.ObjectUnavailable,
        ImmutableArray<MemoryReadResult>.Empty,
        ImmutableArray<EvaluationDeterministicBound>.Empty);

    private static ImmutableArray<EvaluationDeterministicBound> NormalizeBounds(
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        var normalized = bounds.IsDefault
            ? ImmutableArray<EvaluationDeterministicBound>.Empty
            : bounds;
        if (normalized.Any(static bound => bound is null))
        {
            throw new ArgumentException("Applied bounds cannot contain null entries.", nameof(bounds));
        }

        normalized = normalized.OrderBy(static bound => bound.Name, StringComparer.Ordinal).ToImmutableArray();
        for (var index = 1; index < normalized.Length; index++)
        {
            if (string.Equals(normalized[index - 1].Name, normalized[index].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"The applied bound name '{normalized[index].Name}' occurs more than once.",
                    nameof(bounds));
            }
        }

        return normalized;
    }
}
