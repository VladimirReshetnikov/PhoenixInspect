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
/// partial search's matches as though it were uniquely selected. Search-backed bindings retain the exact ordinal type
/// predicate, adapter status, traversal counters, raw evidence, and applied bounds so every preparation or evaluation
/// result can preserve the explanation produced before query binding began.
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
        ClrmdHeapObjectSearchResult? search,
        DumpObjectBinding? objectBinding,
        ImmutableArray<MemoryReadResult> evidence,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds)
    {
        Name = name;
        Snapshot = snapshot;
        Status = status;
        Root = root;
        Issue = issue;
        TypeNameSelector = search?.TypeNameSelector;
        SearchStatus = search?.Status;
        ObjectBinding = objectBinding;
        HandlesScanned = search?.HandlesScanned;
        MaximumHandlesScanned = search?.MaximumHandlesScanned;
        MaximumMatches = search?.MaximumMatches;
        MatchesRetained = search?.MatchesRetained;
        MatchLimitReached = search?.MatchLimitReached;
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
    /// Gets the authoritative typed object/source binding when this exact compatibility root was projected from a
    /// common object binding; legacy host and handle selection paths return null.
    /// </summary>
    public DumpObjectBinding? ObjectBinding { get; }

    /// <summary>
    /// Gets the adapter issue explaining a partial, unavailable, conflicting, or invalid search. Exact selection and
    /// exhaustive absence use <see cref="ClrmdValueIssue.None"/>.
    /// </summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>
    /// Gets the exact ordinal runtime type-name predicate when this binding was classified from a bounded adapter
    /// search; otherwise, gets <see langword="null"/> for a directly supplied object or compatibility-only miss.
    /// </summary>
    /// <remarks>
    /// The predicate is retained without case folding or display-name substitution so non-object outcomes remain
    /// attributable to the selection request that produced them.
    /// </remarks>
    public string? TypeNameSelector { get; }

    /// <summary>
    /// Gets the adapter search status before it was mapped to <see cref="DumpQueryRootBindingStatus"/>, or
    /// <see langword="null"/> when this binding did not originate from a search.
    /// </summary>
    public ClrmdEvidenceStatus? SearchStatus { get; }

    /// <summary>
    /// Gets the number of runtime handles inspected by the originating search, or <see langword="null"/> when no
    /// search produced this binding.
    /// </summary>
    public int? HandlesScanned { get; }

    /// <summary>
    /// Gets the enforced handle-inspection cap from the originating search, or <see langword="null"/> when no search
    /// produced this binding.
    /// </summary>
    public int? MaximumHandlesScanned { get; }

    /// <summary>
    /// Gets the enforced retained-match cap from the originating search, or <see langword="null"/> when no search
    /// produced this binding.
    /// </summary>
    public int? MaximumMatches { get; }

    /// <summary>
    /// Gets the number of validated candidates retained by the originating search before unique-root classification,
    /// or <see langword="null"/> when no search produced this binding.
    /// </summary>
    public int? MatchesRetained { get; }

    /// <summary>
    /// Gets whether the originating traversal observed a candidate beyond its retained-match cap, or
    /// <see langword="null"/> when no search produced this binding.
    /// </summary>
    public bool? MatchLimitReached { get; }

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
    /// <returns>
    /// An exact binding retaining the object's root-selection reads and supplied bounds. Because no adapter search is
    /// supplied, the search predicate, status, and counter properties are <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="appliedBounds"/> contains a null or duplicate name.</exception>
    public static DumpQueryRootBinding FromExactObject(
        string? name,
        ClrmdHeapObjectInfo root,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.SelectionKind == ClrmdHeapObjectSelectionKind.TypedObjectBinding)
        {
            throw new ArgumentException(
                "A typed object projection requires FromObjectBinding so its authoritative source is not discarded.",
                nameof(root));
        }
        return new DumpQueryRootBinding(
            name,
            root.Snapshot,
            DumpQueryRootBindingStatus.ExactObject,
            root,
            ClrmdValueIssue.None,
            search: null,
            objectBinding: null,
            root.Evidence,
            appliedBounds.IsDefault ? ImmutableArray<EvaluationDeterministicBound>.Empty : appliedBounds);
    }

    /// <summary>
    /// Creates an exact compatibility root for an independently typed common object binding without treating its
    /// source as a CLR handle.
    /// </summary>
    /// <param name="name">The bounded case-sensitive compatibility identifier used by an existing instance engine.</param>
    /// <param name="root">The direct-address Host projection marked as a typed object binding.</param>
    /// <param name="objectBinding">The authoritative source-agnostic identity and complete typed selection provenance.</param>
    /// <returns>
    /// An exact binding whose legacy root fields support unchanged instance-member code while
    /// <see cref="ObjectBinding"/> preserves the actual non-handle source.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The Host projection is not typed, the intrinsic object facts disagree, or the compatibility name is invalid.
    /// </exception>
    public static DumpQueryRootBinding FromObjectBinding(
        string name,
        ClrmdHeapObjectInfo root,
        DumpObjectBinding objectBinding)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(objectBinding);
        if (string.IsNullOrWhiteSpace(name) || name.Length > DumpQueryParser.MaximumIdentifierLength)
        {
            throw new ArgumentException("A bounded compatibility root identifier is required.", nameof(name));
        }
        var identity = objectBinding.Identity;
        if (root.SelectionKind != ClrmdHeapObjectSelectionKind.TypedObjectBinding ||
            root.RootAddress != 0 ||
            root.Snapshot != identity.Snapshot ||
            root.Address != identity.Address ||
            root.MethodTable != identity.MethodTable ||
            root.TypeMetadataToken != identity.TypeMetadataToken ||
            root.Module.Identity != identity.RuntimeModule ||
            !string.Equals(root.TypeName, identity.TypeName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The compatibility projection and authoritative common object binding disagree.",
                nameof(root));
        }
        return new DumpQueryRootBinding(
            name,
            root.Snapshot,
            DumpQueryRootBindingStatus.ExactObject,
            root,
            ClrmdValueIssue.None,
            search: null,
            objectBinding,
            root.Evidence,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
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
    /// The exact ordinal type-name predicate and traversal counters are retained for deterministic explanation even
    /// when the search contains no selected object.
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
                    search,
                    objectBinding: null,
                    search.Evidence,
                    bounds),
                1 => new DumpQueryRootBinding(
                    name,
                    search.Snapshot,
                    DumpQueryRootBindingStatus.ExactObject,
                    search.Matches[0],
                    ClrmdValueIssue.None,
                    search,
                    objectBinding: null,
                    search.Evidence,
                    bounds),
                _ => new DumpQueryRootBinding(
                    name,
                    search.Snapshot,
                    DumpQueryRootBindingStatus.Conflict,
                    null,
                    ClrmdValueIssue.AmbiguousMatch,
                    search,
                    objectBinding: null,
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
            search,
            objectBinding: null,
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
        search: null,
        objectBinding: null,
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
