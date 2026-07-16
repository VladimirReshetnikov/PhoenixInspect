using System.Buffers.Binary;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Freezes the complete receiver and field evidence needed by one dump-grounded counterfactual execution after the
/// dump session has been detached.
/// </summary>
/// <remarks>
/// This is a semantic validation memory, not a CLR heap emulator. It contains one exact non-null receiver and a
/// canonical vector of ordinary instance Int32 observations. It owns no ClrMD object, native handle, path, stream, or
/// callback and performs no target-memory reads. Exact cells expose values; partial and unavailable cells retain only
/// their copied prefix evidence. The state is deeply immutable, so <see cref="Fork"/> returns this instance.
/// </remarks>
public sealed class CounterfactualDumpMemory :
    IPersistentMemoryState<CounterfactualDumpMemory>,
    IEquatable<CounterfactualDumpMemory>
{
    /// <summary>Gets the canonical detached-memory schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    internal const long ReceiverReferenceId = 1;

    private readonly ImmutableArray<CounterfactualFieldObservation> observations;
    private readonly ImmutableDictionary<FieldHandle, CounterfactualFieldObservation> observationsByField;
    private readonly ImmutableArray<byte> canonicalBytes;

    internal CounterfactualDumpMemory(
        TypeSig receiverType,
        string rootEvidenceSha256,
        ImmutableArray<CounterfactualFieldObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(receiverType);
        if (!receiverType.IsMetadataTypeDefinition)
        {
            throw new ArgumentException(
                "Detached dump memory requires one exact metadata TypeDef receiver.",
                nameof(receiverType));
        }

        RootEvidenceSha256 = CounterfactualCanonical.ValidateSha256(
            rootEvidenceSha256,
            nameof(rootEvidenceSha256));
        if (observations.IsDefaultOrEmpty || observations.Any(static item => item is null))
        {
            throw new ArgumentException(
                "Detached dump memory requires an initialized non-empty field-observation vector.",
                nameof(observations));
        }

        var copied = CounterfactualCanonical.Copy(observations);
        var lookup = ImmutableDictionary.CreateBuilder<FieldHandle, CounterfactualFieldObservation>();
        string? sourceSha256 = null;
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            sourceSha256 ??= observation.SourceSha256;
            if (observation.DependencyOrdinal != index ||
                observation.Field.DeclaringType != receiverType ||
                !string.Equals(observation.ImportedObjectSha256, RootEvidenceSha256, StringComparison.Ordinal) ||
                !string.Equals(observation.SourceSha256, sourceSha256, StringComparison.Ordinal) ||
                !lookup.TryAdd(observation.Field.Handle, observation))
            {
                throw new ArgumentException(
                    "Detached observations must be ordinal, unique, same-receiver cells from one evidence source.",
                    nameof(observations));
            }
        }

        SchemaVersion = CanonicalSchemaVersion;
        ReceiverType = receiverType;
        this.observations = copied;
        observationsByField = lookup.ToImmutable();
        canonicalBytes = EncodeCanonical();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the canonical detached-memory schema version carried by this instance.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the exact structural TypeDef of the sole imported receiver.</summary>
    public TypeSig ReceiverType { get; }

    /// <summary>Gets the complete digest of the bounded root-selection evidence imported as the receiver.</summary>
    public string RootEvidenceSha256 { get; }

    /// <summary>Gets the number of distinct immutable field cells retained by this state.</summary>
    public int FieldCount => observations.Length;

    /// <summary>
    /// Gets a defensive copy of the domain-separated canonical replay bytes. These bytes contain target-derived
    /// addresses and field prefixes and therefore are replay material rather than telemetry-safe display text.
    /// </summary>
    public ImmutableArray<byte> CanonicalBytes => CounterfactualCanonical.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <inheritdoc />
    public CounterfactualDumpMemory Fork() => this;

    /// <inheritdoc />
    public bool Equals(CounterfactualDumpMemory? other) =>
        ReferenceEquals(this, other) ||
        other is not null && canonicalBytes.AsSpan().SequenceEqual(other.canonicalBytes.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CounterfactualDumpMemory);

    /// <inheritdoc />
    public override int GetHashCode() =>
        BinaryPrimitives.ReadInt32BigEndian(Convert.FromHexString(Sha256));

    internal bool MatchesReceiver(ProvenanceConcreteValue receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return receiver.SemanticValue.Kind == ConcreteValueKind.ObjectReference &&
            receiver.SemanticValue.StaticType == ReceiverType &&
            receiver.SemanticValue.TryGetReferenceId(out var referenceId) &&
            referenceId == ReceiverReferenceId &&
            !receiver.TryGetLineageRoot(out _);
    }

    internal bool TryGetObservation(
        ResolvedField field,
        out CounterfactualFieldObservation? observation)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (observationsByField.TryGetValue(field.Handle, out var candidate) && candidate.Field == field)
        {
            observation = candidate;
            return true;
        }

        observation = null;
        return false;
    }

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("Interpreter.CounterfactualDumpMemory");
        writer.WriteInt32(SchemaVersion);
        writer.WriteType(ReceiverType);
        writer.WriteInt64(ReceiverReferenceId);
        writer.WriteDigest(RootEvidenceSha256);
        writer.WriteInt32(observations.Length);
        foreach (var observation in observations)
        {
            writer.WriteBytes(observation.CanonicalBytes.AsSpan());
        }

        return writer.ToImmutableArray();
    }
}

internal sealed class CounterfactualDumpMemoryModel :
    IMemoryModel<ProvenanceConcreteValue, CounterfactualDumpMemory>
{
    private const string BindingMismatchCode = "W4.DumpMemory.BindingMismatch";
    private readonly ProvenanceConcreteDomain domain;
    private readonly CounterfactualDumpMemory memory;

    internal CounterfactualDumpMemoryModel(
        ProvenanceConcreteDomain domain,
        CounterfactualDumpMemory memory)
    {
        this.domain = domain ?? throw new ArgumentNullException(nameof(domain));
        this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    /// <inheritdoc />
    public bool CanAllocate => false;

    /// <inheritdoc />
    public (ProvenanceConcreteValue objRef, CounterfactualDumpMemory mem) NewObject(
        CounterfactualDumpMemory mem,
        TypeSig type) => throw Unsupported();

    /// <inheritdoc />
    public (ProvenanceConcreteValue arrRef, CounterfactualDumpMemory mem) NewArray(
        CounterfactualDumpMemory mem,
        TypeSig elemType,
        ProvenanceConcreteValue length) => throw Unsupported();

    /// <inheritdoc />
    public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
        CounterfactualDumpMemory mem,
        ProvenanceConcreteValue objRef,
        ResolvedField field)
    {
        ArgumentNullException.ThrowIfNull(mem);
        ArgumentNullException.ThrowIfNull(objRef);
        ArgumentNullException.ThrowIfNull(field);
        if (!ReferenceEquals(mem, memory) ||
            !memory.MatchesReceiver(objRef) ||
            !memory.TryGetObservation(field, out var observation))
        {
            return Mismatch();
        }

        return observation!.EvidenceStatus switch
        {
            EvaluationEvidenceStatus.Exact => MemoryLoadResult<ProvenanceConcreteValue>.Exact(
                domain.ConstInt32(observation.ExactInt32!.Value)),
            EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable =>
                MemoryLoadResult<ProvenanceConcreteValue>.FromFieldEvidence(
                    observation.RuntimeFieldEvidence!),
            EvaluationEvidenceStatus.Conflict => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                MemoryLoadKind.Conflict,
                observation.ReasonCode!),
            EvaluationEvidenceStatus.Invalid => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                MemoryLoadKind.Invalid,
                observation.ReasonCode!),
            _ => Mismatch(),
        };
    }

    /// <inheritdoc />
    public CounterfactualDumpMemory StoreField(
        CounterfactualDumpMemory mem,
        ProvenanceConcreteValue objRef,
        ResolvedField field,
        ProvenanceConcreteValue value) => throw Unsupported();

    /// <inheritdoc />
    public ProvenanceConcreteValue LoadElement(
        CounterfactualDumpMemory mem,
        ProvenanceConcreteValue arrRef,
        ProvenanceConcreteValue index) => throw Unsupported();

    /// <inheritdoc />
    public CounterfactualDumpMemory StoreElement(
        CounterfactualDumpMemory mem,
        ProvenanceConcreteValue arrRef,
        ProvenanceConcreteValue index,
        ProvenanceConcreteValue value) => throw Unsupported();

    private static MemoryLoadResult<ProvenanceConcreteValue> Mismatch() =>
        MemoryLoadResult<ProvenanceConcreteValue>.NonExact(MemoryLoadKind.Invalid, BindingMismatchCode);

    private static NotSupportedException Unsupported() =>
        new("Detached dump memory is read-only and admits only the frozen receiver's instance-field loads.");
}
