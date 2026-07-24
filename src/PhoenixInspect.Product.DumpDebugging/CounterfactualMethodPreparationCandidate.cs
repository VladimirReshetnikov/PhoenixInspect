using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>
/// Snapshots host-supplied request fields and operational bindings for nonthrowing counterfactual
/// preparation.
/// </summary>
/// <typeparam name="TMemory">The persistent memory-snapshot type proposed by the host binding.</typeparam>
/// <remarks>
/// This draft W4.8 boundary deliberately performs no semantic or capability validation. Default arrays, null
/// elements, invalid structural handles, incompatible receiver roles, invalid limits, and missing operational
/// bindings remain representable so <see cref="CounterfactualMethodRunner{TMemory}.Prepare"/> can turn ordinary host
/// mistakes into stable multi-axis failures. Initialized arrays are snapshotted defensively without normalizing a
/// default array to empty. Public construction remains synthetic-only; dump candidates are reserved for an internal
/// evidence adapter that has already established the validated dump-input boundary. Caveat: other input shapes are
/// outside this prototype contract.
/// </remarks>
public sealed class CounterfactualMethodPreparationCandidate<TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    private readonly ImmutableArray<CounterfactualInputEvidence> arguments;
    private readonly ImmutableArray<string> assumptions;
    private readonly ImmutableArray<CounterfactualFieldObservation> fieldObservations;

    private CounterfactualMethodPreparationCandidate(
        EvaluationEvidenceSourceKind evidenceSource,
        string? syntheticEvidenceId,
        EvaluationEvidenceIdentity? snapshotIdentity,
        EvaluationEvidenceIdentity? moduleIdentity,
        string? rootSelectionId,
        string? rootEvidenceSha256,
        MethodHandle rootMethod,
        CounterfactualInputEvidence? receiverEvidence,
        ImmutableArray<CounterfactualInputEvidence> arguments,
        string? policyId,
        PureCallModelVersion policyVersion,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit,
        string? modelCatalogId,
        PureCallModelVersion modelCatalogVersion,
        MethodHandle? requiredModelTarget,
        ImmutableArray<string> assumptions,
        IResolutionServices? resolver,
        ProvenanceConcreteDomain? domain,
        IMemoryModel<ProvenanceConcreteValue, TMemory>? memoryModel,
        TMemory? initialMemory,
        ProvenanceConcreteValue? receiver,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations,
        IPureCallModelRegistry? modelRegistry)
    {
        EvidenceSource = evidenceSource;
        SyntheticEvidenceId = syntheticEvidenceId;
        SnapshotIdentity = snapshotIdentity;
        ModuleIdentity = moduleIdentity;
        RootSelectionId = rootSelectionId;
        RootEvidenceSha256 = rootEvidenceSha256;
        RootMethod = rootMethod;
        ReceiverEvidence = receiverEvidence;
        this.arguments = Snapshot(arguments);
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        InstructionLimit = instructionLimit;
        LogicalDepthLimit = logicalDepthLimit;
        TraversalLimit = traversalLimit;
        ModelCatalogId = modelCatalogId;
        ModelCatalogVersion = modelCatalogVersion;
        RequiredModelTarget = requiredModelTarget;
        this.assumptions = Snapshot(assumptions);
        Resolver = resolver;
        Domain = domain;
        MemoryModel = memoryModel;
        InitialMemory = initialMemory;
        Receiver = receiver;
        this.fieldObservations = Snapshot(fieldObservations);
        ModelRegistry = modelRegistry;
    }

    /// <summary>Gets the unvalidated top-level evidence-source discriminator.</summary>
    public EvaluationEvidenceSourceKind EvidenceSource { get; }

    /// <summary>Gets the unvalidated synthetic evidence identity, which may be null or malformed.</summary>
    public string? SyntheticEvidenceId { get; }

    /// <summary>Gets the unvalidated snapshot identity outcome, which may be null.</summary>
    public EvaluationEvidenceIdentity? SnapshotIdentity { get; }

    /// <summary>Gets the unvalidated module identity outcome, which may be null.</summary>
    public EvaluationEvidenceIdentity? ModuleIdentity { get; }

    /// <summary>Gets the unvalidated rooted-object selection identity, which may be null or malformed.</summary>
    public string? RootSelectionId { get; }

    /// <summary>Gets the unvalidated rooted-object evidence digest, which may be null or incomplete.</summary>
    public string? RootEvidenceSha256 { get; }

    /// <summary>Gets the proposed structural root MethodDef, including a possible default handle.</summary>
    public MethodHandle RootMethod { get; }

    /// <summary>Gets the proposed typed receiver evidence, which may be null or have the wrong input role.</summary>
    public CounterfactualInputEvidence? ReceiverEvidence { get; }

    /// <summary>
    /// Gets a defensive snapshot of proposed metadata-ordered argument evidence while preserving a default array as
    /// an invalid candidate outcome.
    /// </summary>
    public ImmutableArray<CounterfactualInputEvidence> Arguments => Snapshot(arguments);

    /// <summary>Gets the unvalidated immutable-policy identity, which may be null or malformed.</summary>
    public string? PolicyId { get; }

    /// <summary>Gets the proposed immutable-policy semantic version.</summary>
    public PureCallModelVersion PolicyVersion { get; }

    /// <summary>Gets the proposed instruction-unit limit, including invalid negative values.</summary>
    public long InstructionLimit { get; }

    /// <summary>Gets the proposed logical call-depth limit, including out-of-range values.</summary>
    public int LogicalDepthLimit { get; }

    /// <summary>Gets the proposed graph-traversal limit, including out-of-range values.</summary>
    public int TraversalLimit { get; }

    /// <summary>Gets the unvalidated model-catalog identity, which may be null or malformed.</summary>
    public string? ModelCatalogId { get; }

    /// <summary>Gets the proposed model-catalog semantic version.</summary>
    public PureCallModelVersion ModelCatalogVersion { get; }

    /// <summary>Gets the optional structural target that the candidate proposes to model.</summary>
    public MethodHandle? RequiredModelTarget { get; }

    /// <summary>
    /// Gets a defensive snapshot of proposed assumption identities while preserving a default array and null entries.
    /// </summary>
    public ImmutableArray<string> Assumptions => Snapshot(assumptions);

    /// <summary>
    /// Gets a defensive snapshot of proposed field observations while preserving a default array and null entries.
    /// </summary>
    public ImmutableArray<CounterfactualFieldObservation> FieldObservations => Snapshot(fieldObservations);

    /// <summary>
    /// Snapshots one unvalidated synthetic preparation candidate without consulting any supplied capability.
    /// </summary>
    /// <param name="syntheticEvidenceId">Proposed stable synthetic evidence identity.</param>
    /// <param name="rootSelectionId">Proposed stable rooted-object selection identity.</param>
    /// <param name="rootEvidenceSha256">Proposed complete rooted-object evidence digest.</param>
    /// <param name="rootMethod">Proposed root MethodDef.</param>
    /// <param name="receiverEvidence">Proposed exact non-null receiver evidence.</param>
    /// <param name="arguments">Proposed metadata-ordered argument evidence.</param>
    /// <param name="policyId">Proposed immutable-policy identity.</param>
    /// <param name="policyVersion">Proposed immutable-policy semantic version.</param>
    /// <param name="instructionLimit">Proposed non-negative instruction-unit limit.</param>
    /// <param name="logicalDepthLimit">Proposed bounded logical call-depth limit.</param>
    /// <param name="traversalLimit">Proposed bounded graph-traversal limit.</param>
    /// <param name="modelCatalogId">Proposed model-catalog identity.</param>
    /// <param name="modelCatalogVersion">Proposed model-catalog semantic version.</param>
    /// <param name="requiredModelTarget">Optional proposed target that must use one pure model.</param>
    /// <param name="assumptions">Proposed ordered assumption identities.</param>
    /// <param name="resolver">Nullable metadata/body resolver proposed for graph preparation.</param>
    /// <param name="domain">Nullable fresh provenance domain proposed for argument materialization.</param>
    /// <param name="memoryModel">Nullable persistent memory capability reserved for later execution.</param>
    /// <param name="initialMemory">Nullable initial persistent memory snapshot reserved for later execution.</param>
    /// <param name="receiver">Nullable exact operational receiver proposed for activation.</param>
    /// <param name="fieldObservations">Proposed plan-relative field-observation vector.</param>
    /// <param name="modelRegistry">Nullable registry required only when a modeled target is requested.</param>
    /// <returns>
    /// A defensive raw snapshot. Creation succeeding does not imply that any request field or binding is valid.
    /// </returns>
    public static CounterfactualMethodPreparationCandidate<TMemory> CreateSynthetic(
        string? syntheticEvidenceId,
        string? rootSelectionId,
        string? rootEvidenceSha256,
        MethodHandle rootMethod,
        CounterfactualInputEvidence? receiverEvidence,
        ImmutableArray<CounterfactualInputEvidence> arguments,
        string? policyId,
        PureCallModelVersion policyVersion,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit,
        string? modelCatalogId,
        PureCallModelVersion modelCatalogVersion,
        MethodHandle? requiredModelTarget,
        ImmutableArray<string> assumptions,
        IResolutionServices? resolver,
        ProvenanceConcreteDomain? domain,
        IMemoryModel<ProvenanceConcreteValue, TMemory>? memoryModel,
        TMemory? initialMemory,
        ProvenanceConcreteValue? receiver,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations,
        IPureCallModelRegistry? modelRegistry = null) =>
        new(
            EvaluationEvidenceSourceKind.Synthetic,
            syntheticEvidenceId,
            EvaluationEvidenceIdentity.NotApplicable,
            EvaluationEvidenceIdentity.NotApplicable,
            rootSelectionId,
            rootEvidenceSha256,
            rootMethod,
            receiverEvidence,
            arguments,
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
            initialMemory,
            receiver,
            fieldObservations,
            modelRegistry);

    internal static CounterfactualMethodPreparationCandidate<TMemory> CreateValidated(
        EvaluationEvidenceSourceKind evidenceSource,
        string? syntheticEvidenceId,
        EvaluationEvidenceIdentity? snapshotIdentity,
        EvaluationEvidenceIdentity? moduleIdentity,
        string? rootSelectionId,
        string? rootEvidenceSha256,
        MethodHandle rootMethod,
        CounterfactualInputEvidence? receiverEvidence,
        ImmutableArray<CounterfactualInputEvidence> arguments,
        string? policyId,
        PureCallModelVersion policyVersion,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit,
        string? modelCatalogId,
        PureCallModelVersion modelCatalogVersion,
        MethodHandle? requiredModelTarget,
        ImmutableArray<string> assumptions,
        IResolutionServices? resolver,
        ProvenanceConcreteDomain? domain,
        IMemoryModel<ProvenanceConcreteValue, TMemory>? memoryModel,
        TMemory? initialMemory,
        ProvenanceConcreteValue? receiver,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations,
        IPureCallModelRegistry? modelRegistry) =>
        new(
            evidenceSource,
            syntheticEvidenceId,
            snapshotIdentity,
            moduleIdentity,
            rootSelectionId,
            rootEvidenceSha256,
            rootMethod,
            receiverEvidence,
            arguments,
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
            initialMemory,
            receiver,
            fieldObservations,
            modelRegistry);

    internal IResolutionServices? Resolver { get; }

    internal ProvenanceConcreteDomain? Domain { get; }

    internal IMemoryModel<ProvenanceConcreteValue, TMemory>? MemoryModel { get; }

    internal TMemory? InitialMemory { get; }

    internal ProvenanceConcreteValue? Receiver { get; }

    internal IPureCallModelRegistry? ModelRegistry { get; }

    private static ImmutableArray<T> Snapshot<T>(ImmutableArray<T> values) =>
        values.IsDefault
            ? default
            : ImmutableArray.CreateRange(values.AsSpan().ToArray());
}
