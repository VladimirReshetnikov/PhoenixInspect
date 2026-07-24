using System.Collections.Immutable;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>
/// Binds one immutable counterfactual request and its plan-relative field evidence to the private runtime
/// capabilities required for preparation and execution.
/// </summary>
/// <typeparam name="TMemory">The persistent memory-snapshot type used by the bound memory model.</typeparam>
/// <remarks>
/// This is a W4.8 product boundary, not a general interpreter activation API. Public construction admits only
/// synthetic evidence, one exact non-null receiver, and the request's evidence-declared arguments. Argument domain
/// values are intentionally absent: the runner materializes exact or explained-unknown arguments from the canonical
/// request in its own provenance domain. Resolver, domain, memory, registry, and target-memory objects are retained
/// only in an internal typed bundle and have no public property or canonical identity role.
/// </remarks>
public sealed class CounterfactualMethodExecutionInput<TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    private readonly ImmutableArray<CounterfactualFieldObservation> fieldObservations;

    private CounterfactualMethodExecutionInput(CounterfactualRuntimeBundle<TMemory> runtimeBundle)
    {
        RuntimeBundle = runtimeBundle;
        Request = runtimeBundle.Request;
        fieldObservations = runtimeBundle.FieldObservations;
    }

    /// <summary>Gets the exact immutable request bound to this operational input.</summary>
    public CounterfactualMethodRequest Request { get; }

    /// <summary>
    /// Gets a defensive copy of the request-correlated field observations in prospective graph dependency order.
    /// </summary>
    public ImmutableArray<CounterfactualFieldObservation> FieldObservations =>
        CounterfactualCanonical.Copy(fieldObservations);

    /// <summary>
    /// Creates one synthetic-only runtime binding without resolving metadata, reading memory, selecting a model, or
    /// activating an interpreter machine.
    /// </summary>
    /// <param name="request">The exact canonical synthetic request to bind.</param>
    /// <param name="resolver">The metadata and method-body capability reserved for graph preparation.</param>
    /// <param name="domain">The fresh provenance-aware concrete domain reserved for this evaluation.</param>
    /// <param name="memoryModel">The persistent memory model reserved for prepared execution.</param>
    /// <param name="initialMemory">The exact immutable memory snapshot from which execution will begin.</param>
    /// <param name="receiver">
    /// The exact non-null object-reference domain value corresponding structurally to the request receiver.
    /// </param>
    /// <param name="fieldObservations">
    /// One initialized, non-null, ordinal-aligned vector of request/source/receiver-correlated field observations.
    /// Final completeness and graph correlation are checked after graph preparation.
    /// </param>
    /// <param name="modelRegistry">
    /// The structural pure-model registry required when the request names a required model target. An interpreted
    /// request may bind an optional registry, but its default graph preparation must not query that capability.
    /// </param>
    /// <returns>
    /// An immutable public product input whose operational capabilities remain private and whose arguments will be
    /// materialized later from <paramref name="request"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">A required binding object is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The request is not synthetic, receiver or field evidence disagrees with it, or a required-model request has
    /// no registry.
    /// </exception>
    public static CounterfactualMethodExecutionInput<TMemory> CreateSynthetic(
        CounterfactualMethodRequest request,
        IResolutionServices resolver,
        ProvenanceConcreteDomain domain,
        IMemoryModel<ProvenanceConcreteValue, TMemory> memoryModel,
        TMemory initialMemory,
        ProvenanceConcreteValue receiver,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations,
        IPureCallModelRegistry? modelRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EvidenceSource != EvaluationEvidenceSourceKind.Synthetic)
        {
            throw new ArgumentException(
                "Public counterfactual execution input currently admits only synthetic evidence.",
                nameof(request));
        }

        return CreateValidated(
            request,
            resolver,
            domain,
            memoryModel,
            initialMemory,
            receiver,
            fieldObservations,
            modelRegistry);
    }

    internal static CounterfactualMethodExecutionInput<TMemory> CreateValidated(
        CounterfactualMethodRequest request,
        IResolutionServices resolver,
        ProvenanceConcreteDomain domain,
        IMemoryModel<ProvenanceConcreteValue, TMemory> memoryModel,
        TMemory initialMemory,
        ProvenanceConcreteValue receiver,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations,
        IPureCallModelRegistry? modelRegistry)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EvidenceSource is not (
                EvaluationEvidenceSourceKind.Synthetic or EvaluationEvidenceSourceKind.DumpSnapshot))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Only synthetic and internally validated dump-snapshot evidence are reserved by schema v1.");
        }

        return new CounterfactualMethodExecutionInput<TMemory>(
            new CounterfactualRuntimeBundle<TMemory>(
                request,
                resolver,
                domain,
                memoryModel,
                initialMemory,
                receiver,
                fieldObservations,
                modelRegistry));
    }

    internal CounterfactualRuntimeBundle<TMemory> RuntimeBundle { get; }

    internal static ImmutableArray<CounterfactualFieldObservation> ValidateObservationBinding(
        CounterfactualMethodRequest request,
        ImmutableArray<CounterfactualFieldObservation> observations)
    {
        if (observations.IsDefault || observations.Any(static observation => observation is null))
        {
            throw new ArgumentException(
                "Field observations require an initialized non-null vector.",
                nameof(observations));
        }

        var sourceIdentity = request.EvidenceSource switch
        {
            EvaluationEvidenceSourceKind.Synthetic => request.SyntheticEvidenceId,
            EvaluationEvidenceSourceKind.DumpSnapshot => request.SnapshotIdentity.SourceId,
            _ => null,
        };
        if (sourceIdentity is null)
        {
            throw new ArgumentException(
                "The request has no supported canonical evidence-source identity.",
                nameof(request));
        }

        var expectedSourceSha256 = CounterfactualCanonical.Hash(Encoding.UTF8.GetBytes(sourceIdentity));
        var copied = CounterfactualCanonical.Copy(observations);
        var distinctFields = new HashSet<FieldHandle>();
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation.DependencyOrdinal != index ||
                observation.Field.Handle.Module != request.RootMethod.Module ||
                observation.Field.DeclaringType != request.Receiver.StaticType ||
                !distinctFields.Add(observation.Field.Handle) ||
                !string.Equals(observation.SourceSha256, expectedSourceSha256, StringComparison.Ordinal) ||
                !string.Equals(
                    observation.ImportedObjectSha256,
                    request.Receiver.EvidenceSha256,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Field observations must be distinct, ordinal-aligned, same-root structural dependencies " +
                    "correlated to the request evidence source and imported receiver.",
                    nameof(observations));
            }
        }

        return copied;
    }
}

internal sealed class CounterfactualRuntimeBundle<TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    private readonly ImmutableArray<CounterfactualFieldObservation> fieldObservations;
    private readonly ImmutableArray<ProvenanceConcreteValue> rootArguments;

    internal CounterfactualRuntimeBundle(
        CounterfactualMethodRequest request,
        IResolutionServices resolver,
        ProvenanceConcreteDomain domain,
        IMemoryModel<ProvenanceConcreteValue, TMemory> memoryModel,
        TMemory initialMemory,
        ProvenanceConcreteValue receiver,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations,
        IPureCallModelRegistry? modelRegistry)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(memoryModel);
        ArgumentNullException.ThrowIfNull(initialMemory);
        ArgumentNullException.ThrowIfNull(receiver);

        if (receiver.SemanticValue.Kind != ConcreteValueKind.ObjectReference ||
            receiver.SemanticValue.StaticType != request.Receiver.StaticType ||
            !receiver.SemanticValue.TryGetReferenceId(out var referenceId) ||
            referenceId <= 0 ||
            receiver.TryGetLineageRoot(out _))
        {
            throw new ArgumentException(
                "The runtime receiver must be one exact non-null object reference with the request's static type.",
                nameof(receiver));
        }

        if (request.RequiredModelTarget.HasValue && modelRegistry is null)
        {
            throw new ArgumentException(
                "A request requiring one modeled target must bind a model registry.",
                nameof(modelRegistry));
        }

        Request = request;
        Resolver = resolver;
        Domain = domain;
        MemoryModel = memoryModel;
        InitialMemory = initialMemory;
        Receiver = receiver;
        this.fieldObservations = CounterfactualMethodExecutionInput<TMemory>.ValidateObservationBinding(
            request,
            fieldObservations);
        ModelRegistry = modelRegistry;
        rootArguments = default;
    }

    private CounterfactualRuntimeBundle(
        CounterfactualRuntimeBundle<TMemory> source,
        ImmutableArray<ProvenanceConcreteValue> rootArguments)
    {
        Request = source.Request;
        Resolver = source.Resolver;
        Domain = source.Domain;
        MemoryModel = source.MemoryModel;
        InitialMemory = source.InitialMemory;
        Receiver = source.Receiver;
        fieldObservations = source.FieldObservations;
        ModelRegistry = source.ModelRegistry;
        this.rootArguments = CounterfactualCanonical.Copy(rootArguments);
    }

    internal CounterfactualMethodRequest Request { get; }

    internal IResolutionServices Resolver { get; }

    internal ProvenanceConcreteDomain Domain { get; }

    internal IMemoryModel<ProvenanceConcreteValue, TMemory> MemoryModel { get; }

    internal TMemory InitialMemory { get; }

    internal ProvenanceConcreteValue Receiver { get; }

    internal ImmutableArray<CounterfactualFieldObservation> FieldObservations =>
        CounterfactualCanonical.Copy(fieldObservations);

    internal IPureCallModelRegistry? ModelRegistry { get; }

    internal ImmutableArray<ProvenanceConcreteValue> RootArguments =>
        rootArguments.IsDefault ? default : CounterfactualCanonical.Copy(rootArguments);

    internal bool HasMaterializedRootArguments => !rootArguments.IsDefault;

    internal CounterfactualRuntimeBundle<TMemory> MaterializeRootArguments()
    {
        if (HasMaterializedRootArguments)
        {
            throw new InvalidOperationException("Root arguments were already materialized for this runtime binding.");
        }

        var arguments = Request.Arguments;
        foreach (var argument in arguments)
        {
            var valid = argument.Kind switch
            {
                CounterfactualInputEvidenceKind.ExactInt32 =>
                    argument.StaticType == TypeSig.Int32 &&
                    argument.EvidenceStatus == EvaluationEvidenceStatus.Exact &&
                    argument.ExactInt32.HasValue,
                CounterfactualInputEvidenceKind.ExplainedUnknownInt32 =>
                    argument.StaticType == TypeSig.Int32 &&
                    argument.EvidenceStatus is (
                        EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable) &&
                    !argument.ExactInt32.HasValue,
                _ => false,
            };
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Canonical request argument evidence is inconsistent with its declared input kind.");
            }
        }

        var materialized = ImmutableArray.CreateBuilder<ProvenanceConcreteValue>(arguments.Length + 1);
        materialized.Add(Receiver);
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument.ExactInt32 is { } exact)
            {
                materialized.Add(Domain.ConstInt32(exact));
                continue;
            }

            var reason = argument.EvidenceStatus == EvaluationEvidenceStatus.Partial
                ? "W4.Unknown.RequestArgument.Partial"
                : "W4.Unknown.RequestArgument.Unavailable";
            materialized.Add(Domain.CreateInputUnknown(new ProvenanceInputOrigin(
                ProvenanceInputKind.RequestArgument,
                index,
                argument.EvidenceStatus,
                new ProvenanceSourceKey(argument.EvidenceSha256),
                reason,
                TypeSig.Int32)));
        }

        return new CounterfactualRuntimeBundle<TMemory>(this, materialized.MoveToImmutable());
    }
}
