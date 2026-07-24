using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Acquires the additive selected-frame and Portable-PDB evidence available for one immutable dump snapshot.
/// </summary>
/// <remarks>
/// Frame and PDB observations retain independent typed dispositions: an exact frame with unavailable PDB remains a
/// useful current-namespace context, and fully qualified binding need consult neither. This context contains no
/// paths, live runtime or metadata readers, exception objects, syntax candidates, or selected symbol declaration.
/// </remarks>
public sealed class DumpExpressionBindingContext : IEquatable<DumpExpressionBindingContext>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpExpressionBindingContext(
        ClrmdSnapshotIdentity snapshot,
        DumpSelectedFrameObservation selectedFrame,
        DumpPortablePdbObservation portablePdb)
    {
        Snapshot = snapshot;
        SelectedFrame = selectedFrame;
        PortablePdb = portablePdb;
        var writer = new CanonicalReplayEncoding.Writer("dump-expression-binding-context", 1);
        DumpContextContractEncoding.WriteSnapshot(writer, snapshot);
        writer.WriteLengthPrefixedBytes(selectedFrame.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(portablePdb.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the immutable snapshot shared by every source and exact payload in this context.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the independently typed selected-frame observation.</summary>
    public DumpSelectedFrameObservation SelectedFrame { get; }

    /// <summary>Gets the independently typed module-debug, artifact, scope, and import observation.</summary>
    public DumpPortablePdbObservation PortablePdb { get; }

    /// <summary>Gets a defensive copy of the complete acquired-context canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Acquires a validated additive binding context from independently typed source observations.</summary>
    /// <param name="snapshot">The immutable snapshot to which the resulting context is scoped.</param>
    /// <param name="selectedFrame">The complete selected-frame disposition for this snapshot.</param>
    /// <param name="portablePdb">The complete PDB acquisition disposition for this snapshot.</param>
    /// <returns>An immutable additive context suitable for later selective consultation.</returns>
    /// <exception cref="ArgumentException">
    /// A source refers to another snapshot, a module source disagrees with an exact frame, exact PDB facts disagree
    /// with the frame, or PDB acquisition advanced past module correlation when no exact frame exists.
    /// </exception>
    public static DumpExpressionBindingContext Acquire(
        ClrmdSnapshotIdentity snapshot,
        DumpSelectedFrameObservation selectedFrame,
        DumpPortablePdbObservation portablePdb)
    {
        DumpContextContractEncoding.ValidateSnapshot(snapshot, nameof(snapshot));
        ArgumentNullException.ThrowIfNull(selectedFrame);
        ArgumentNullException.ThrowIfNull(portablePdb);
        if (selectedFrame.Selector.Snapshot != snapshot)
        {
            throw new ArgumentException(
                "The selected-frame observation belongs to another dump snapshot.",
                nameof(selectedFrame));
        }

        if (portablePdb.Source.Snapshot != snapshot)
        {
            throw new ArgumentException(
                "The Portable-PDB observation belongs to another dump snapshot.",
                nameof(portablePdb));
        }

        if (selectedFrame.Frame is not { } exactFrame)
        {
            if (portablePdb.Facts is not null || portablePdb.Source.ModuleDebugIdentity is not null)
            {
                throw new ArgumentException(
                    "Selected-frame PDB acquisition cannot advance to a module when no exact frame exists.",
                    nameof(portablePdb));
            }

            return new DumpExpressionBindingContext(snapshot, selectedFrame, portablePdb);
        }

        if (portablePdb.Source.ModuleDebugIdentity is { } sourceModule &&
            (!sourceModule.RuntimeModule.Equals(exactFrame.RuntimeModule) ||
             !sourceModule.ModuleContent.Equals(exactFrame.ModuleContent)))
        {
            throw new ArgumentException(
                "The Portable-PDB source and exact selected frame identify different modules.",
                nameof(portablePdb));
        }

        if (portablePdb.Facts is { } facts && !facts.SelectedFrame.Equals(exactFrame))
        {
            throw new ArgumentException(
                "Exact Portable-PDB facts must be selected by the exact frame in this context.",
                nameof(portablePdb));
        }

        return new DumpExpressionBindingContext(snapshot, selectedFrame, portablePdb);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The acquired binding context to compare.</param>
    /// <returns><see langword="true"/> when snapshot and both complete observations are equal.</returns>
    public bool Equals(DumpExpressionBindingContext? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionBindingContext);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>
/// Identifies only the selected-frame and Portable-PDB facts actually consulted by one binding attempt.
/// </summary>
/// <remarks>
/// A fully qualified lookup uses <see cref="ForFullyQualified"/> and is consequently independent of frame/PDB poison.
/// Current-namespace consultation retains only the frame observation and omits PDB evidence. Import consultation
/// retains PDB source/provenance plus only the supplied exact import facts. An exact consulted empty import set is
/// distinct from an unconsulted set. Non-exact PDB evidence retains status, source, issue, and bounds but can carry no
/// import candidate expansion.
/// </remarks>
public sealed class DumpConsultedBindingContextIdentity : IEquatable<DumpConsultedBindingContextIdentity>
{
    private readonly ImmutableArray<DumpPortablePdbImportFact> consultedImports;
    private readonly ImmutableArray<EvaluationDeterministicBound> importReachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpConsultedBindingContextIdentity(
        ClrmdSnapshotIdentity snapshot,
        bool currentNamespaceConsulted,
        DumpSelectedFrameObservation? consultedFrameEvidence,
        bool importsConsulted,
        DumpContextEvidenceStatus? importEvidenceStatus,
        DumpContextEvidenceIssue? importEvidenceIssue,
        DumpPortablePdbEvidenceSource? importEvidenceSource,
        ImmutableArray<EvaluationDeterministicBound> importReachedBounds,
        DumpPortablePdbContextFacts? exactPdbFacts,
        ImmutableArray<DumpPortablePdbImportFact> consultedImports)
    {
        if ((currentNamespaceConsulted || importsConsulted) != (consultedFrameEvidence is not null))
        {
            throw new ArgumentException(
                "Every contextual namespace/import consultation must retain its selected-frame observation.",
                nameof(consultedFrameEvidence));
        }

        Snapshot = snapshot;
        CurrentNamespaceConsulted = currentNamespaceConsulted;
        ConsultedFrameEvidence = consultedFrameEvidence;
        ImportsConsulted = importsConsulted;
        ImportEvidenceStatus = importEvidenceStatus;
        ImportEvidenceIssue = importEvidenceIssue;
        ImportEvidenceSource = importEvidenceSource;
        this.importReachedBounds = CanonicalReplayEncoding.Copy(importReachedBounds);
        this.consultedImports = CanonicalReplayEncoding.Copy(consultedImports);

        var writer = new CanonicalReplayEncoding.Writer("dump-consulted-binding-context-identity", 1);
        DumpContextContractEncoding.WriteSnapshot(writer, snapshot);
        writer.WriteBoolean(consultedFrameEvidence is not null);
        if (consultedFrameEvidence is not null)
        {
            writer.WriteLengthPrefixedBytes(consultedFrameEvidence.CanonicalBytes.AsSpan());
        }

        writer.WriteBoolean(currentNamespaceConsulted);
        writer.WriteBoolean(importsConsulted);
        if (importsConsulted)
        {
            writer.WriteInt32((int)importEvidenceStatus!.Value);
            writer.WriteInt32((int)importEvidenceIssue!.Value);
            writer.WriteLengthPrefixedBytes(importEvidenceSource!.CanonicalBytes.AsSpan());
            DumpContextContractEncoding.WriteBounds(writer, importReachedBounds);
            writer.WriteBoolean(exactPdbFacts is not null);
            if (exactPdbFacts is not null)
            {
                exactPdbFacts.WriteCanonical(writer, includeImports: false);
            }

            writer.WriteInt32(consultedImports.Length);
            foreach (var import in consultedImports)
            {
                writer.WriteLengthPrefixedBytes(import.CanonicalBytes.AsSpan());
            }
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the immutable snapshot searched by the associated symbol binding attempt.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets whether the declaring current namespace was actually consulted.</summary>
    public bool CurrentNamespaceConsulted { get; }

    /// <summary>
    /// Gets exact or non-exact selected-frame evidence when current namespace or imports were consulted; otherwise
    /// gets null for a fully qualified lookup.
    /// </summary>
    public DumpSelectedFrameObservation? ConsultedFrameEvidence { get; }

    /// <summary>Gets whether the active Portable-PDB import set was actually consulted.</summary>
    public bool ImportsConsulted { get; }

    /// <summary>Gets the consulted PDB status, or null when imports were not consulted.</summary>
    public DumpContextEvidenceStatus? ImportEvidenceStatus { get; }

    /// <summary>Gets the consulted PDB issue, or null when imports were not consulted.</summary>
    public DumpContextEvidenceIssue? ImportEvidenceIssue { get; }

    /// <summary>Gets the consulted PDB source boundary, or null when imports were not consulted.</summary>
    public DumpPortablePdbEvidenceSource? ImportEvidenceSource { get; }

    /// <summary>Gets a defensive copy of PDB bounds only when import evidence was consulted.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ImportReachedBounds =>
        CanonicalReplayEncoding.Copy(importReachedBounds);

    /// <summary>
    /// Gets a defensive copy of only exact import facts actually consulted. Non-exact evidence always returns empty.
    /// </summary>
    public ImmutableArray<DumpPortablePdbImportFact> ConsultedImports =>
        CanonicalReplayEncoding.Copy(consultedImports);

    /// <summary>Gets a defensive copy of the versioned consulted-fact identity bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Creates the context-independent consulted identity for fully qualified binding.
    /// </summary>
    /// <param name="snapshot">The immutable snapshot whose bounded module catalog is searched.</param>
    /// <returns>
    /// An identity containing the snapshot and explicit unconsulted tags, but no acquired frame or PDB observation.
    /// </returns>
    public static DumpConsultedBindingContextIdentity ForFullyQualified(ClrmdSnapshotIdentity snapshot)
    {
        DumpContextContractEncoding.ValidateSnapshot(snapshot, nameof(snapshot));
        return new DumpConsultedBindingContextIdentity(
            snapshot,
            currentNamespaceConsulted: false,
            consultedFrameEvidence: null,
            importsConsulted: false,
            importEvidenceStatus: null,
            importEvidenceIssue: null,
            importEvidenceSource: null,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            exactPdbFacts: null,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
    }

    /// <summary>
    /// Projects only the acquired frame and import facts actually consulted by a contextual binding attempt.
    /// </summary>
    /// <param name="context">The validated additive context from which facts were consulted.</param>
    /// <param name="currentNamespaceConsulted">
    /// True when declaring namespace expansion was attempted; this retains only selected-frame evidence.
    /// </param>
    /// <param name="importsConsulted">
    /// True when active imports were attempted; this retains PDB provenance and the exact selected subset.
    /// </param>
    /// <param name="consultedImports">
    /// An explicitly initialized array containing only exact import facts used by candidate expansion. Repeated facts
    /// are rejected. It must be empty when imports were not consulted or PDB evidence was non-exact, and every
    /// non-empty fact must occur in the acquired exact PDB context.
    /// </param>
    /// <returns>A canonical identity containing no unconsulted context poison.</returns>
    /// <exception cref="ArgumentException">
    /// Neither source was consulted, the import array is default, imports are supplied for an unconsulted/non-exact
    /// source, or a supplied fact is not present in the exact acquired context.
    /// </exception>
    public static DumpConsultedBindingContextIdentity FromAcquiredContext(
        DumpExpressionBindingContext context,
        bool currentNamespaceConsulted,
        bool importsConsulted,
        ImmutableArray<DumpPortablePdbImportFact> consultedImports)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!currentNamespaceConsulted && !importsConsulted)
        {
            throw new ArgumentException(
                "A contextual consulted identity must consult current namespace, imports, or both.",
                nameof(currentNamespaceConsulted));
        }

        if (consultedImports.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized consulted-import array is required.", nameof(consultedImports));
        }

        if (!importsConsulted && !consultedImports.IsEmpty)
        {
            throw new ArgumentException("Unconsulted imports cannot contribute exact facts.", nameof(consultedImports));
        }

        var normalizedImports = ImmutableArray<DumpPortablePdbImportFact>.Empty;
        DumpPortablePdbContextFacts? exactFacts = null;
        if (importsConsulted)
        {
            exactFacts = context.PortablePdb.Facts;
            if (exactFacts is null && !consultedImports.IsEmpty)
            {
                throw new ArgumentException(
                    "Non-exact Portable-PDB evidence cannot expose exact candidate imports.",
                    nameof(consultedImports));
            }

            if (exactFacts is not null)
            {
                var available = exactFacts.Imports;
                var ordered = consultedImports
                    .Select(static fact => fact ?? throw new ArgumentException(
                        "Consulted imports cannot contain null entries.",
                        nameof(consultedImports)))
                    .OrderBy(static fact => fact.ImportScopeToken)
                    .ThenBy(static fact => fact.Ordinal)
                    .ThenBy(static fact => fact.Sha256, StringComparer.Ordinal)
                    .ToArray();
                var normalized = new List<DumpPortablePdbImportFact>(ordered.Length);
                foreach (var fact in ordered)
                {
                    if (!available.Any(candidate => candidate.Equals(fact)))
                    {
                        throw new ArgumentException(
                            "Every consulted exact import must occur in the acquired PDB context.",
                            nameof(consultedImports));
                    }

                    if (normalized.Count > 0 && normalized[^1].Equals(fact))
                    {
                        throw new ArgumentException(
                            "A consulted exact import cannot occur more than once.",
                            nameof(consultedImports));
                    }

                    normalized.Add(fact);
                }

                normalizedImports = ImmutableArray.CreateRange(normalized);
            }
        }

        var pdb = context.PortablePdb;
        return new DumpConsultedBindingContextIdentity(
            context.Snapshot,
            currentNamespaceConsulted,
            context.SelectedFrame,
            importsConsulted,
            importsConsulted ? pdb.Status : null,
            importsConsulted ? pdb.Issue : null,
            importsConsulted ? pdb.Source : null,
            importsConsulted ? pdb.ReachedBounds : ImmutableArray<EvaluationDeterministicBound>.Empty,
            importsConsulted ? exactFacts : null,
            normalizedImports);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The consulted-fact identity to compare.</param>
    /// <returns><see langword="true"/> when and only when the same source facts were actually consulted.</returns>
    public bool Equals(DumpConsultedBindingContextIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpConsultedBindingContextIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}
