using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;

namespace PhoenixInspect.Product.DumpDebugging;

internal sealed class CounterfactualRecordingMemoryModel<TMemory> :
    IMemoryModel<ProvenanceConcreteValue, TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    internal const string ObservationMismatchCode = "W4.Evidence.FieldObservationMismatch";

    private readonly IMemoryModel<ProvenanceConcreteValue, TMemory> inner;
    private readonly ProvenanceConcreteValue rootedReceiver;
    private readonly ProvenanceConcreteDomain domain;
    private readonly ImmutableArray<CounterfactualFieldObservation> observations;
    private readonly Dictionary<ResolvedField, int> ordinalsByField;
    private readonly bool[] reached;
    private readonly List<int> reachedLoadOrdinals = [];

    internal CounterfactualRecordingMemoryModel(
        IMemoryModel<ProvenanceConcreteValue, TMemory> inner,
        ProvenanceConcreteValue rootedReceiver,
        ProvenanceConcreteDomain domain,
        ImmutableArray<CounterfactualFieldObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(rootedReceiver);
        ArgumentNullException.ThrowIfNull(domain);
        if (domain.GetPrecision(rootedReceiver) != ValuePrecisionKind.Exact ||
            rootedReceiver.SemanticValue.Kind != ConcreteValueKind.ObjectReference ||
            !rootedReceiver.SemanticValue.TryGetReferenceId(out var referenceId) ||
            referenceId <= 0 ||
            rootedReceiver.TryGetLineageRoot(out _))
        {
            throw new ArgumentException(
                "Counterfactual execution requires one exact rooted object reference without lineage.",
                nameof(rootedReceiver));
        }

        if (observations.IsDefault)
        {
            throw new ArgumentException(
                "Plan-relative field observations must be initialized.",
                nameof(observations));
        }

        var copied = observations.IsEmpty
            ? ImmutableArray<CounterfactualFieldObservation>.Empty
            : ImmutableArray.CreateRange(observations.AsSpan().ToArray());
        var lookup = new Dictionary<ResolvedField, int>(copied.Length);
        string? sourceSha256 = null;
        string? importedObjectSha256 = null;
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index] ?? throw new ArgumentException(
                "Plan-relative field observations cannot contain null values.",
                nameof(observations));
            if (observation.DependencyOrdinal != index ||
                observation.Field.DeclaringType != rootedReceiver.SemanticValue.StaticType ||
                !lookup.TryAdd(observation.Field, index))
            {
                throw new ArgumentException(
                    "Field observations must be unique, ordinal-aligned, and declared by the rooted receiver type.",
                    nameof(observations));
            }

            sourceSha256 ??= observation.SourceSha256;
            importedObjectSha256 ??= observation.ImportedObjectSha256;
            if (!string.Equals(sourceSha256, observation.SourceSha256, StringComparison.Ordinal) ||
                !string.Equals(
                    importedObjectSha256,
                    observation.ImportedObjectSha256,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "All plan-relative observations must describe one evidence source and imported receiver.",
                    nameof(observations));
            }
        }

        this.inner = inner;
        this.rootedReceiver = rootedReceiver;
        this.domain = domain;
        this.observations = copied;
        ordinalsByField = lookup;
        reached = new bool[copied.Length];
    }

    /// <inheritdoc />
    public bool CanAllocate => false;

    internal ImmutableArray<CounterfactualFieldObservation> ReachedObservations
    {
        get
        {
            var builder = ImmutableArray.CreateBuilder<CounterfactualFieldObservation>();
            for (var index = 0; index < observations.Length; index++)
            {
                if (reached[index])
                {
                    builder.Add(observations[index]);
                }
            }

            return builder.ToImmutable();
        }
    }

    internal ImmutableArray<int> ReachedLoadOrdinals => reachedLoadOrdinals.ToImmutableArray();

    /// <inheritdoc />
    public (ProvenanceConcreteValue objRef, TMemory mem) NewObject(TMemory mem, TypeSig type) =>
        throw Unsupported();

    /// <inheritdoc />
    public (ProvenanceConcreteValue arrRef, TMemory mem) NewArray(
        TMemory mem,
        TypeSig elemType,
        ProvenanceConcreteValue length) =>
        throw Unsupported();

    /// <inheritdoc />
    public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
        TMemory mem,
        ProvenanceConcreteValue objRef,
        ResolvedField field)
    {
        ArgumentNullException.ThrowIfNull(mem);
        ArgumentNullException.ThrowIfNull(objRef);
        ArgumentNullException.ThrowIfNull(field);
        if (objRef != rootedReceiver || !ordinalsByField.TryGetValue(field, out var ordinal))
        {
            return Mismatch();
        }

        reached[ordinal] = true;
        reachedLoadOrdinals.Add(ordinal);
        var actual = inner.LoadField(mem, objRef, field);
        var observation = observations[ordinal];
        return observation.EvidenceStatus switch
        {
            EvaluationEvidenceStatus.Exact => ValidateExact(actual, observation),
            EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable =>
                ValidateDegraded(actual, observation),
            EvaluationEvidenceStatus.Conflict =>
                ValidateFailure(actual, observation, MemoryLoadKind.Conflict),
            EvaluationEvidenceStatus.Invalid =>
                ValidateFailure(actual, observation, MemoryLoadKind.Invalid),
            _ => Mismatch(),
        };
    }

    /// <inheritdoc />
    public TMemory StoreField(
        TMemory mem,
        ProvenanceConcreteValue objRef,
        ResolvedField field,
        ProvenanceConcreteValue value) =>
        throw Unsupported();

    /// <inheritdoc />
    public ProvenanceConcreteValue LoadElement(
        TMemory mem,
        ProvenanceConcreteValue arrRef,
        ProvenanceConcreteValue index) =>
        throw Unsupported();

    /// <inheritdoc />
    public TMemory StoreElement(
        TMemory mem,
        ProvenanceConcreteValue arrRef,
        ProvenanceConcreteValue index,
        ProvenanceConcreteValue value) =>
        throw Unsupported();

    private MemoryLoadResult<ProvenanceConcreteValue> ValidateExact(
        MemoryLoadResult<ProvenanceConcreteValue> actual,
        CounterfactualFieldObservation observation)
    {
        if (actual.Kind != MemoryLoadKind.Exact ||
            !domain.TryGetConstInt32(actual.Value, out var scalar) ||
            scalar != observation.ExactInt32)
        {
            return Mismatch();
        }

        return actual;
    }

    private static MemoryLoadResult<ProvenanceConcreteValue> ValidateDegraded(
        MemoryLoadResult<ProvenanceConcreteValue> actual,
        CounterfactualFieldObservation observation)
    {
        var expectedKind = observation.EvidenceStatus == EvaluationEvidenceStatus.Partial
            ? MemoryLoadKind.Partial
            : MemoryLoadKind.Unavailable;
        var expectedEvidence = observation.RuntimeFieldEvidence;
        if (actual.Kind != expectedKind ||
            actual.FieldEvidence is not { } actualEvidence ||
            expectedEvidence is null ||
            !actualEvidence.CanonicalBytes.AsSpan().SequenceEqual(expectedEvidence.CanonicalBytes.AsSpan()) ||
            !string.Equals(actualEvidence.Sha256, expectedEvidence.Sha256, StringComparison.Ordinal))
        {
            return Mismatch();
        }

        var freshEvidence = new FieldLoadEvidence(
            observation.DependencyOrdinal,
            observation.Field,
            observation.EvidenceStatus,
            observation.ReasonCode!,
            observation.SourceSha256,
            observation.ImportedObjectSha256,
            observation.Address,
            observation.RequestedLength,
            observation.ObservedBytes.AsSpan());
        return MemoryLoadResult<ProvenanceConcreteValue>.FromFieldEvidence(freshEvidence);
    }

    private static MemoryLoadResult<ProvenanceConcreteValue> ValidateFailure(
        MemoryLoadResult<ProvenanceConcreteValue> actual,
        CounterfactualFieldObservation observation,
        MemoryLoadKind expectedKind) =>
        actual.Kind == expectedKind &&
        string.Equals(actual.FailureCode, observation.ReasonCode, StringComparison.Ordinal)
            ? MemoryLoadResult<ProvenanceConcreteValue>.NonExact(expectedKind, observation.ReasonCode!)
            : Mismatch();

    private static MemoryLoadResult<ProvenanceConcreteValue> Mismatch() =>
        MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
            MemoryLoadKind.Invalid,
            ObservationMismatchCode);

    private static NotSupportedException Unsupported() =>
        new("The read-only counterfactual execution profile does not admit this memory operation.");
}
