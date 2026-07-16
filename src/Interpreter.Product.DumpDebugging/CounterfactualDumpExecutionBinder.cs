using System.Collections.Immutable;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Carries one validated dump-evidence adaptation into the common rooted counterfactual preparation boundary.
/// </summary>
/// <remarks>
/// The binding owns detached immutable memory and canonical field observations. Its candidate intentionally keeps the
/// resolver, domain, memory capability, receiver, and optional model registry private, preserving the W4.8 product
/// boundary while allowing the ordinary runner to remain the sole plan issuer.
/// </remarks>
public sealed class CounterfactualDumpExecutionBinding
{
    private readonly ImmutableArray<CounterfactualFieldObservation> fieldObservations;

    internal CounterfactualDumpExecutionBinding(
        CounterfactualMethodPreparationCandidate<CounterfactualDumpMemory> candidate,
        CounterfactualDumpMemory memory,
        string rootSelectionId,
        string rootEvidenceSha256,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations)
    {
        Candidate = candidate;
        Memory = memory;
        RootSelectionId = rootSelectionId;
        RootEvidenceSha256 = rootEvidenceSha256;
        this.fieldObservations = CounterfactualCanonical.Copy(fieldObservations);
    }

    /// <summary>Gets the complete dump-snapshot candidate to submit to one counterfactual runner.</summary>
    public CounterfactualMethodPreparationCandidate<CounterfactualDumpMemory> Candidate { get; }

    /// <summary>Gets the deeply immutable, session-detached memory retained by the candidate.</summary>
    public CounterfactualDumpMemory Memory { get; }

    /// <summary>Gets the versioned path-independent identity of the complete bounded root selection.</summary>
    public string RootSelectionId { get; }

    /// <summary>Gets the digest of the canonical bounded root-selection facts and raw reads.</summary>
    public string RootEvidenceSha256 { get; }

    /// <summary>Gets a defensive copy of graph-canonically ordered exact or explained-missing field observations.</summary>
    public ImmutableArray<CounterfactualFieldObservation> FieldObservations =>
        CounterfactualCanonical.Copy(fieldObservations);
}

/// <summary>
/// Converts issuer-validated ClrMD root and Int32 field evidence into a detached product preparation candidate.
/// </summary>
/// <remarks>
/// Evidence identities, receiver identity, field order, raw bytes, and the persistent memory capability are derived
/// here rather than accepted as caller assertions. Policy, limits, assumptions, and optional model selection remain
/// caller policy and are intentionally left for the common nonthrowing runner to validate. The returned binding owns
/// no live dump session and never opens a target-reported path.
/// </remarks>
public static class CounterfactualDumpExecutionBinder
{
    private const string PartialReason = "W4.Field.Partial";
    private const string UnavailableReason = "W4.Field.Unavailable";

    /// <summary>
    /// Binds one exact rooted receiver and all required Int32 field observations to the common counterfactual runner.
    /// </summary>
    /// <param name="resolver">The atomic counted-metadata and interpreted-body graph for the dump snapshot.</param>
    /// <param name="fieldEvidence">
    /// Issuer-validated field evidence for one owner and root method. Input order is ignored; fields are sorted by
    /// structural FieldDef identity to match the frozen graph's canonical dependency vector.
    /// </param>
    /// <param name="policyId">The proposed immutable counterfactual policy identity.</param>
    /// <param name="policyVersion">The proposed exact policy semantic version.</param>
    /// <param name="instructionLimit">The proposed non-negative instruction-unit limit.</param>
    /// <param name="logicalDepthLimit">The proposed bounded logical call-depth limit.</param>
    /// <param name="traversalLimit">The proposed bounded graph-traversal limit.</param>
    /// <param name="modelCatalogId">The proposed model-catalog identity.</param>
    /// <param name="modelCatalogVersion">The proposed exact model-catalog version.</param>
    /// <param name="requiredModelTarget">Optional same-module target that must remain body-free and use a pure model.</param>
    /// <param name="assumptions">Proposed ordered canonical assumption identities.</param>
    /// <param name="modelRegistry">Optional pure-model registry required by a modeled request.</param>
    /// <returns>
    /// A complete detached binding whose candidate can be prepared and run after every ClrMD session has been disposed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Field evidence is absent, duplicated, non-executable, belongs to a different resolver/root/owner, or carries
    /// different canonical root-selection evidence.
    /// </exception>
    public static CounterfactualDumpExecutionBinding Bind(
        ClrmdDumpExecutionResolver resolver,
        ImmutableArray<ClrmdInt32FieldExecutionEvidence> fieldEvidence,
        string? policyId,
        PureCallModelVersion policyVersion,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit,
        string? modelCatalogId,
        PureCallModelVersion modelCatalogVersion,
        MethodHandle? requiredModelTarget,
        ImmutableArray<string> assumptions,
        IPureCallModelRegistry? modelRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        if (fieldEvidence.IsDefaultOrEmpty || fieldEvidence.Any(static item => item is null))
        {
            throw new ArgumentException(
                "Dump binding requires an initialized non-empty vector of correlated field evidence.",
                nameof(fieldEvidence));
        }

        var first = fieldEvidence[0];
        var rootBytes = EncodeRootSelection(first);
        var rootEvidenceSha256 = CounterfactualCanonical.Hash(rootBytes.AsSpan());
        var rootSelectionId = $"clrmd-root-selection:v1:{rootEvidenceSha256}";
        var distinctFields = new HashSet<FieldHandle>();
        foreach (var evidence in fieldEvidence)
        {
            var candidateRootBytes = EncodeRootSelection(evidence);
            if (evidence.RuntimeModule != resolver.Module.Identity ||
                evidence.RootMethod != resolver.RootMethodDefinition ||
                evidence.Owner.Address != first.Owner.Address ||
                evidence.Owner.MethodTable != first.Owner.MethodTable ||
                evidence.Field.Handle.Module != resolver.ModuleHandle ||
                evidence.Field.DeclaringType != resolver.RootMethodDefinition.Signature.DeclaringType ||
                evidence.Status is not (
                    ClrmdEvidenceStatus.Exact or
                    ClrmdEvidenceStatus.Partial or
                    ClrmdEvidenceStatus.Unavailable) ||
                !distinctFields.Add(evidence.Field.Handle) ||
                !candidateRootBytes.AsSpan().SequenceEqual(rootBytes.AsSpan()))
            {
                throw new ArgumentException(
                    "Every field row must describe one resolver root, owner selection, and distinct executable field.",
                    nameof(fieldEvidence));
            }
        }

        var sourceSha256 = CounterfactualCanonical.Hash(
            Encoding.UTF8.GetBytes(resolver.Module.Identity.Snapshot.MemorySourceId));
        var orderedEvidence = fieldEvidence
            .OrderBy(static item => item.Field.Handle.MetadataToken)
            .ToImmutableArray();
        var observations = ImmutableArray.CreateBuilder<CounterfactualFieldObservation>(orderedEvidence.Length);
        for (var ordinal = 0; ordinal < orderedEvidence.Length; ordinal++)
        {
            var evidence = orderedEvidence[ordinal];
            var memory = evidence.Observation.Memory;
            observations.Add(evidence.Status == ClrmdEvidenceStatus.Exact
                ? CounterfactualFieldObservation.CreateExactInt32(
                    ordinal,
                    evidence.Field,
                    sourceSha256,
                    rootEvidenceSha256,
                    memory.Address,
                    memory.RequestedLength,
                    memory.Bytes.AsSpan())
                : CounterfactualFieldObservation.CreateNonExactInt32(
                    ordinal,
                    evidence.Field,
                    evidence.Status == ClrmdEvidenceStatus.Partial
                        ? EvaluationEvidenceStatus.Partial
                        : EvaluationEvidenceStatus.Unavailable,
                    evidence.Status == ClrmdEvidenceStatus.Partial ? PartialReason : UnavailableReason,
                    sourceSha256,
                    rootEvidenceSha256,
                    memory.Address,
                    memory.RequestedLength,
                    memory.Bytes.AsSpan()));
        }

        var frozenObservations = observations.MoveToImmutable();
        var receiverType = resolver.RootMethodDefinition.Signature.DeclaringType;
        var domain = new ProvenanceConcreteDomain();
        var receiver = domain.ObjectReference(CounterfactualDumpMemory.ReceiverReferenceId, receiverType);
        var memoryState = new CounterfactualDumpMemory(
            receiverType,
            rootEvidenceSha256,
            frozenObservations);
        var memoryModel = new CounterfactualDumpMemoryModel(domain, memoryState);
        var receiverEvidence = CounterfactualInputEvidence.CreateExactNonNullReceiver(
            receiverType,
            rootSelectionId,
            rootEvidenceSha256);
        var candidate = CounterfactualMethodPreparationCandidate<CounterfactualDumpMemory>.CreateValidated(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            syntheticEvidenceId: null,
            EvaluationEvidenceIdentity.CreateAvailable(resolver.Module.Identity.Snapshot.MemorySourceId),
            EvaluationEvidenceIdentity.CreateAvailable(resolver.Module.Identity.SourceId),
            rootSelectionId,
            rootEvidenceSha256,
            resolver.RootMethod,
            receiverEvidence,
            ImmutableArray<CounterfactualInputEvidence>.Empty,
            policyId,
            policyVersion,
            instructionLimit,
            logicalDepthLimit,
            traversalLimit,
            modelCatalogId,
            modelCatalogVersion,
            requiredModelTarget,
            assumptions,
            resolver,
            domain,
            memoryModel,
            memoryState,
            receiver,
            frozenObservations,
            modelRegistry);

        return new CounterfactualDumpExecutionBinding(
            candidate,
            memoryState,
            rootSelectionId,
            rootEvidenceSha256,
            frozenObservations);
    }

    private static ImmutableArray<byte> EncodeRootSelection(ClrmdInt32FieldExecutionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var search = evidence.OwnerSearch;
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("Interpreter.ClrmdRootSelection");
        writer.WriteInt32(1);
        writer.WriteString(search.Snapshot.MemorySourceId);
        writer.WriteString(evidence.RuntimeModule.SourceId);
        writer.WriteString(search.TypeNameSelector);
        writer.WriteInt32((int)search.Status);
        writer.WriteInt32((int)search.Issue);
        writer.WriteInt32(search.HandlesScanned);
        writer.WriteInt32(search.MaximumHandlesScanned);
        writer.WriteInt32(search.MaximumMatches);
        writer.WriteBoolean(search.MatchLimitReached);
        writer.WriteInt32(search.Matches.Length);
        foreach (var match in search.Matches)
        {
            writer.WriteString(match.Snapshot.MemorySourceId);
            writer.WriteUInt64(match.Address);
            writer.WriteString(match.TypeName);
            writer.WriteInt32(match.TypeMetadataToken);
            writer.WriteUInt64(match.MethodTable);
            writer.WriteUInt64(match.RootAddress);
            writer.WriteString(match.RootKind);
            writer.WriteString(match.Module.Identity.SourceId);
            WriteReads(writer, match.Evidence);
        }

        WriteReads(writer, search.Evidence);
        return writer.ToImmutableArray();
    }

    private static void WriteReads(
        CounterfactualCanonicalWriter writer,
        ImmutableArray<MemoryReadResult> reads)
    {
        writer.WriteInt32(reads.Length);
        foreach (var read in reads)
        {
            writer.WriteString(read.SourceId);
            writer.WriteUInt64(read.Address);
            writer.WriteInt32(read.RequestedLength);
            writer.WriteInt32((int)read.Status);
            writer.WriteBytes(read.Bytes.AsSpan());
        }
    }
}
