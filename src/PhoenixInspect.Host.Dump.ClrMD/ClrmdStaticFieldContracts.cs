using System.Buffers.Binary;
using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Identifies the fixed physical decoder selected by an exact Product declaration.</summary>
public enum ClrmdStaticFieldValueShape
{
    /// <summary>A four-byte little-endian CLI Int32.</summary>
    Int32 = 1,

    /// <summary>A specialized Nullable&lt;Int32&gt; decoded through exact physical child offsets.</summary>
    NullableInt32 = 2,

    /// <summary>A managed reference whose target is decoded as System.String.</summary>
    String = 3,

    /// <summary>
    /// A managed reference whose non-null target is retained exactly for Product-owned declared-type assignability.
    /// </summary>
    ObjectReference = 4,
}

/// <summary>Identifies one exact terminal produced by a fixed static-field decoder.</summary>
public enum ClrmdStaticFieldTerminalKind
{
    /// <summary>A null managed reference.</summary>
    Null = 1,

    /// <summary>An exact Int32.</summary>
    Int32 = 2,

    /// <summary>An exact Nullable&lt;Int32&gt; with HasValue false.</summary>
    NullableInt32NoValue = 3,

    /// <summary>An exact Nullable&lt;Int32&gt; with HasValue true.</summary>
    NullableInt32Value = 4,

    /// <summary>An exact bounded System.String value.</summary>
    String = 5,

    /// <summary>An exact non-null object reference validated from its raw method-table header.</summary>
    ObjectReference = 6,
}

/// <summary>Classifies whether a physical static-field observation is exact or where it stopped.</summary>
public enum ClrmdStaticFieldObservationStatus
{
    /// <summary>The complete fixed decoder produced one exact terminal.</summary>
    Exact = 1,

    /// <summary>A deterministic cap or partial read stopped the decoder.</summary>
    Partial = 2,

    /// <summary>A required runtime catalog, slot, or memory range was unavailable.</summary>
    Unavailable = 3,

    /// <summary>Exact physical evidence contradicted the Product-supplied request.</summary>
    Conflict = 4,

    /// <summary>Addresses or bytes violated the fixed physical layout.</summary>
    Invalid = 5,

    /// <summary>The runtime could not perform the requested ordinary-static operation.</summary>
    Unsupported = 6,
}

/// <summary>Retains only the physical offsets needed by the specialized Nullable&lt;Int32&gt; decoder.</summary>
public sealed class ClrmdStaticNullableInt32Layout : IEquatable<ClrmdStaticNullableInt32Layout>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticNullableInt32Layout(int storageSize, int hasValueOffset, int valueOffset)
    {
        StorageSize = storageSize;
        HasValueOffset = hasValueOffset;
        ValueOffset = valueOffset;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-nullable-int32-layout", 2);
        writer.WriteInt32(storageSize);
        writer.WriteInt32(hasValueOffset);
        writer.WriteInt32(valueOffset);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete specialized value storage size.</summary>
    public int StorageSize { get; }

    /// <summary>Gets the exact byte offset of the one-byte Boolean HasValue child.</summary>
    public int HasValueOffset { get; }

    /// <summary>Gets the exact byte offset of the four-byte Int32 value child.</summary>
    public int ValueOffset { get; }

    /// <summary>Gets a defensive copy of the versioned canonical physical layout bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates the fixed physical layout after Product has proved exact Nullable child ownership and types.</summary>
    /// <param name="storageSize">The complete specialized storage size.</param>
    /// <param name="hasValueOffset">The exact one-byte Boolean child offset.</param>
    /// <param name="valueOffset">The exact four-byte Int32 child offset.</param>
    /// <returns>A detached immutable physical decoder layout.</returns>
    internal static ClrmdStaticNullableInt32Layout Create(
        int storageSize,
        int hasValueOffset,
        int valueOffset)
    {
        if (storageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storageSize));
        }
        ValidateRange(storageSize, hasValueOffset, sizeof(byte), nameof(hasValueOffset));
        ValidateRange(storageSize, valueOffset, sizeof(int), nameof(valueOffset));
        var hasValueEnd = hasValueOffset + sizeof(byte);
        var valueEnd = valueOffset + sizeof(int);
        if (hasValueOffset < valueEnd && valueOffset < hasValueEnd)
        {
            throw new ArgumentException("Nullable child storage ranges cannot overlap.");
        }
        return new ClrmdStaticNullableInt32Layout(storageSize, hasValueOffset, valueOffset);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdStaticNullableInt32Layout? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticNullableInt32Layout);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateRange(int storageSize, int offset, int length, string parameterName)
    {
        if (offset < 0 || offset > storageSize || length > storageSize - offset)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The child range must fit completely in nullable storage.");
        }
    }
}

/// <summary>
/// Carries the minimal canonical physical request produced by Product after one exact semantic declaration binding.
/// </summary>
/// <remarks>
/// Host does not resolve metadata, classify names, or select a semantic type. The request embeds Product's expected
/// decoder tag in the detached runtime mapping and, only for Nullable&lt;Int32&gt;, the exact specialized child offsets.
/// Its factory is internal to the Host assembly and explicitly exposed only to Product and synthetic contract tests.
/// </remarks>
public sealed class ClrmdStaticFieldEvaluationRequest : IEquatable<ClrmdStaticFieldEvaluationRequest>
{
    private readonly ImmutableArray<EvaluationDeterministicBound> canonicalBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticFieldEvaluationRequest(
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping,
        ClrmdStaticNullableInt32Layout? nullableInt32Layout)
    {
        RuntimeMapping = runtimeMapping;
        NullableInt32Layout = nullableInt32Layout;
        canonicalBounds = BuildRuntimeMappingBounds(runtimeMapping);
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-field-evaluation-request", 2);
        writer.WriteLengthPrefixedBytes(runtimeMapping.CanonicalBytes.AsSpan());
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, nullableInt32Layout?.CanonicalBytes);
        ClrmdStaticPhysicalCanonical.WriteBounds(writer, canonicalBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exhaustive detached physical runtime declaration mapping selected by Product.</summary>
    public ClrmdStaticRuntimeDeclarationMappingIdentity RuntimeMapping { get; }

    /// <summary>Gets the specialized physical nullable layout only for the Nullable&lt;Int32&gt; decoder.</summary>
    public ClrmdStaticNullableInt32Layout? NullableInt32Layout { get; }

    /// <summary>Gets the immutable dump snapshot containing mapping, storage, and future reads.</summary>
    public ClrmdSnapshotIdentity Snapshot => RuntimeMapping.DeclaringType.Snapshot;

    /// <summary>Gets the target architecture pointer width.</summary>
    public int PointerWidth => RuntimeMapping.DeclaringType.PointerWidth;

    /// <summary>Gets the owning application-domain address used for exact slot acquisition.</summary>
    public ulong ApplicationDomainAddress => RuntimeMapping.DeclaringType.RuntimeModule!.Value.AppDomainAddress;

    /// <summary>Gets Product's closed expected physical decoder shape.</summary>
    public ClrmdStaticFieldValueShape ValueShape => RuntimeMapping.Field.ExpectedDecoderKind switch
    {
        ClrmdStaticExpectedDecoderKind.Int32 => ClrmdStaticFieldValueShape.Int32,
        ClrmdStaticExpectedDecoderKind.NullableInt32 => ClrmdStaticFieldValueShape.NullableInt32,
        ClrmdStaticExpectedDecoderKind.String => ClrmdStaticFieldValueShape.String,
        ClrmdStaticExpectedDecoderKind.ManagedReference => ClrmdStaticFieldValueShape.ObjectReference,
        _ => throw new InvalidOperationException("The mapping contains an unknown expected decoder tag."),
    };

    /// <summary>Gets the complete fixed storage size needed at the acquired static slot.</summary>
    public int StorageSize => ValueShape switch
    {
        ClrmdStaticFieldValueShape.Int32 => sizeof(int),
        ClrmdStaticFieldValueShape.NullableInt32 => PointerWidth,
        ClrmdStaticFieldValueShape.String or ClrmdStaticFieldValueShape.ObjectReference => PointerWidth,
        _ => throw new InvalidOperationException("The request contains an unknown decoder shape."),
    };

    /// <summary>Gets the exact runtime type physically observed from ClrStaticField.Type.</summary>
    public ClrmdStaticRuntimeTypeIdentity ObservedFieldType => RuntimeMapping.Field.ObservedFieldType;

    /// <summary>Gets defensive copies of all runtime mapping bounds necessarily reached by request construction.</summary>
    public ImmutableArray<EvaluationDeterministicBound> CanonicalBounds =>
        CanonicalReplayEncoding.Copy(canonicalBounds);

    /// <summary>Gets a defensive copy of the versioned canonical request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a physical request after Product has completed exact semantic binding and parity checks.</summary>
    /// <param name="runtimeMapping">The exact exhaustive runtime type and static-field mapping.</param>
    /// <param name="nullableInt32Layout">The exact specialized child layout only for Nullable&lt;Int32&gt;.</param>
    /// <returns>A minimal immutable physical evaluation request.</returns>
    internal static ClrmdStaticFieldEvaluationRequest Create(
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping,
        ClrmdStaticNullableInt32Layout? nullableInt32Layout = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeMapping);
        var isNullable = runtimeMapping.Field.ExpectedDecoderKind == ClrmdStaticExpectedDecoderKind.NullableInt32;
        if (isNullable != (nullableInt32Layout is not null))
        {
            throw new ArgumentException(
                "Only the Nullable<Int32> decoder requires one exact specialized physical layout.",
                nameof(nullableInt32Layout));
        }
        return new ClrmdStaticFieldEvaluationRequest(runtimeMapping, nullableInt32Layout);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdStaticFieldEvaluationRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticFieldEvaluationRequest);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ImmutableArray<EvaluationDeterministicBound> BuildRuntimeMappingBounds(
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping)
    {
        var bounds = runtimeMapping.Counters.CanonicalBounds
            .Add(ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound)
            .Add(ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphDepthBound)
            .Add(ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphNodeCountBound);
        var reachedTypeDefinition = false;
        var reachedArray = false;
        Visit(runtimeMapping.DeclaringType);
        Visit(runtimeMapping.Field.ObservedFieldType);
        if (reachedTypeDefinition)
        {
            bounds = bounds.Add(ClrmdStaticRuntimeTypeIdentity.DeclaredGenericArgumentCountBound);
        }
        if (reachedArray)
        {
            bounds = bounds
                .Add(ClrmdStaticRuntimeTypeIdentity.DeclaredArrayRankBound)
                .Add(ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeInterfaceTypeCountBound);
        }
        return CanonicalReplayEncoding.NormalizeBounds(bounds, "runtimeMappingBounds");

        void Visit(ClrmdStaticRuntimeTypeIdentity type)
        {
            if (type.Kind == ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition)
            {
                reachedTypeDefinition = true;
                foreach (var argument in type.GenericArguments)
                {
                    Visit(argument);
                }
                return;
            }

            reachedArray = true;
            Visit(type.ComponentType!);
            Visit(type.BaseType!);
            foreach (var interfaceType in type.InterfaceTypes)
            {
                Visit(interfaceType);
            }
        }
    }
}

/// <summary>Classifies the exact declaration-to-static-slot acquisition boundary.</summary>
public enum ClrmdStaticStorageAcquisitionKind
{
    /// <summary>An exhaustive domain catalog produced one unique domain and a valid nonzero static slot.</summary>
    SlotAddressAcquired = 1,

    /// <summary>An exhaustive domain catalog contained no entry with the requested address.</summary>
    ApplicationDomainUnavailable = 2,

    /// <summary>An exhaustive domain catalog contained multiple entries with the requested address.</summary>
    ApplicationDomainAmbiguous = 3,

    /// <summary>The unique exact domain was selected but the runtime returned no static slot.</summary>
    SlotAddressUnavailable = 4,

    /// <summary>The runtime could not perform the ordinary-static slot operation.</summary>
    RuntimeStorageUnsupported = 5,

    /// <summary>The runtime returned a nonzero slot whose fixed storage range is structurally invalid.</summary>
    InvalidSlotAddress = 6,

    /// <summary>The domain catalog exceeded the fixed cap; no absence, uniqueness, or selected domain is claimed.</summary>
    ApplicationDomainCatalogLimitReached = 7,
}

/// <summary>Identifies why a nonzero returned slot cannot contain the fixed request storage.</summary>
public enum ClrmdStaticStorageInvalidSlotReason
{
    /// <summary>No invalid-slot reason applies.</summary>
    None = 0,

    /// <summary>The closed fixed-width storage range exceeds the target address space.</summary>
    StorageRangeOverflow = 1,
}

/// <summary>Retains exhaustive-or-capped domain catalog topology and the exact static-slot outcome.</summary>
public sealed class ClrmdStaticStorageAcquisitionEvidence :
    IEquatable<ClrmdStaticStorageAcquisitionEvidence>
{
    /// <summary>Gets the maximum domain catalog prefix examined by W7.</summary>
    public const int MaximumApplicationDomains = 16;

    /// <summary>Gets the deterministic-bound name for the domain catalog count.</summary>
    public const string MaximumApplicationDomainCountBoundName = "static-field.application-domain-count";

    private readonly ImmutableArray<int> matchingApplicationDomainOrdinals;
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticStorageAcquisitionEvidence(
        ClrmdStaticStorageAcquisitionKind kind,
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainsExamined,
        bool catalogExhaustive,
        ImmutableArray<int> matchingApplicationDomainOrdinals,
        ulong? returnedSlotAddress,
        int? attemptedStorageSize,
        ClrmdStaticStorageInvalidSlotReason invalidSlotReason)
    {
        Kind = kind;
        PointerWidth = pointerWidth;
        RequestedApplicationDomainAddress = requestedApplicationDomainAddress;
        ApplicationDomainsExamined = applicationDomainsExamined;
        CatalogExhaustive = catalogExhaustive;
        this.matchingApplicationDomainOrdinals = matchingApplicationDomainOrdinals;
        ReturnedSlotAddress = returnedSlotAddress;
        AttemptedStorageSize = attemptedStorageSize;
        InvalidSlotReason = invalidSlotReason;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-storage-acquisition-evidence", 4);
        writer.WriteInt32((int)kind);
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(requestedApplicationDomainAddress);
        writer.WriteInt32(applicationDomainsExamined);
        writer.WriteBoolean(catalogExhaustive);
        writer.WriteInt32(matchingApplicationDomainOrdinals.Length);
        foreach (var ordinal in matchingApplicationDomainOrdinals)
        {
            writer.WriteInt32(ordinal);
        }
        writer.WriteBoolean(returnedSlotAddress.HasValue);
        if (returnedSlotAddress.HasValue)
        {
            writer.WriteUInt64(returnedSlotAddress.Value);
        }
        writer.WriteBoolean(attemptedStorageSize.HasValue);
        if (attemptedStorageSize.HasValue)
        {
            writer.WriteInt32(attemptedStorageSize.Value);
        }
        writer.WriteInt32((int)invalidSlotReason);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the typed acquisition outcome.</summary>
    public ClrmdStaticStorageAcquisitionKind Kind { get; }

    /// <summary>Gets the target pointer width used for domain and slot coordinates.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the exact Product-requested application-domain address.</summary>
    public ulong RequestedApplicationDomainAddress { get; }

    /// <summary>Gets the exact number of catalog entries examined, including the cap-sized prefix on a cap stop.</summary>
    public int ApplicationDomainsExamined { get; }

    /// <summary>Gets whether the complete catalog cardinality equals <see cref="ApplicationDomainsExamined"/>.</summary>
    public bool CatalogExhaustive { get; }

    /// <summary>Gets the exact catalog cardinality when exhausted, otherwise null.</summary>
    public int? ApplicationDomainCatalogCardinality => CatalogExhaustive ? ApplicationDomainsExamined : null;

    /// <summary>Gets a defensive sorted copy of matching catalog ordinals.</summary>
    public ImmutableArray<int> MatchingApplicationDomainOrdinals =>
        CanonicalReplayEncoding.Copy(matchingApplicationDomainOrdinals);

    /// <summary>Gets the unique matching ordinal for post-domain outcomes, otherwise null.</summary>
    public int? MatchingApplicationDomainOrdinal =>
        CatalogExhaustive && matchingApplicationDomainOrdinals.Length == 1
            ? matchingApplicationDomainOrdinals[0]
            : null;

    /// <summary>Gets the runtime-returned slot for acquired or invalid-slot outcomes.</summary>
    public ulong? ReturnedSlotAddress { get; }

    /// <summary>Gets the fixed attempted storage width for acquired or invalid-slot outcomes.</summary>
    public int? AttemptedStorageSize { get; }

    /// <summary>Gets the exact invalid-slot reason only for <see cref="ClrmdStaticStorageAcquisitionKind.InvalidSlotAddress"/>.</summary>
    public ClrmdStaticStorageInvalidSlotReason InvalidSlotReason { get; }

    /// <summary>Gets the fixed domain catalog bound.</summary>
    public static EvaluationDeterministicBound DeclaredApplicationDomainCountBound =>
        new(MaximumApplicationDomainCountBoundName, MaximumApplicationDomains);

    /// <summary>Gets a defensive copy of the versioned canonical acquisition bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact exhausted-catalog unique-domain and valid-slot witness.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <param name="applicationDomainCatalogCardinality">The exact exhausted catalog cardinality.</param>
    /// <param name="matchingApplicationDomainOrdinal">The unique matching zero-based catalog ordinal.</param>
    /// <param name="slotAddress">The valid nonzero returned static slot.</param>
    /// <param name="storageSize">The complete fixed storage width required at the slot.</param>
    /// <returns>An exact slot-acquired witness.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence Acquired(
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainCatalogCardinality,
        int matchingApplicationDomainOrdinal,
        ulong slotAddress,
        int storageSize)
    {
        ValidateCommon(pointerWidth, requestedApplicationDomainAddress, applicationDomainCatalogCardinality);
        ValidateOrdinal(matchingApplicationDomainOrdinal, applicationDomainCatalogCardinality);
        ValidateSlotRange(slotAddress, storageSize, pointerWidth, requireOverflow: false);
        return new ClrmdStaticStorageAcquisitionEvidence(
            ClrmdStaticStorageAcquisitionKind.SlotAddressAcquired,
            pointerWidth,
            requestedApplicationDomainAddress,
            applicationDomainCatalogCardinality,
            catalogExhaustive: true,
            ImmutableArray.Create(matchingApplicationDomainOrdinal),
            slotAddress,
            storageSize,
            ClrmdStaticStorageInvalidSlotReason.None);
    }

    /// <summary>Creates an exhaustive catalog witness proving the requested domain was absent.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <param name="applicationDomainCatalogCardinality">The exact exhausted catalog cardinality.</param>
    /// <returns>An exact domain-absence stop.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence DomainUnavailable(
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainCatalogCardinality)
    {
        ValidateCommon(pointerWidth, requestedApplicationDomainAddress, applicationDomainCatalogCardinality);
        return new ClrmdStaticStorageAcquisitionEvidence(
            ClrmdStaticStorageAcquisitionKind.ApplicationDomainUnavailable,
            pointerWidth,
            requestedApplicationDomainAddress,
            applicationDomainCatalogCardinality,
            catalogExhaustive: true,
            ImmutableArray<int>.Empty,
            returnedSlotAddress: null,
            attemptedStorageSize: null,
            ClrmdStaticStorageInvalidSlotReason.None);
    }

    /// <summary>Creates an exhaustive catalog witness proving multiple entries share the requested address.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <param name="applicationDomainCatalogCardinality">The exact exhausted catalog cardinality.</param>
    /// <param name="matchingApplicationDomainOrdinals">At least two distinct sorted matching ordinals.</param>
    /// <returns>An exact ambiguous-domain stop.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence DomainAmbiguous(
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainCatalogCardinality,
        ImmutableArray<int> matchingApplicationDomainOrdinals)
    {
        ValidateCommon(pointerWidth, requestedApplicationDomainAddress, applicationDomainCatalogCardinality);
        var normalized = NormalizeOrdinals(matchingApplicationDomainOrdinals, applicationDomainCatalogCardinality);
        if (normalized.Length < 2)
        {
            throw new ArgumentException("Ambiguity requires at least two exact matching ordinals.", nameof(matchingApplicationDomainOrdinals));
        }
        return new ClrmdStaticStorageAcquisitionEvidence(
            ClrmdStaticStorageAcquisitionKind.ApplicationDomainAmbiguous,
            pointerWidth,
            requestedApplicationDomainAddress,
            applicationDomainCatalogCardinality,
            catalogExhaustive: true,
            normalized,
            returnedSlotAddress: null,
            attemptedStorageSize: null,
            ClrmdStaticStorageInvalidSlotReason.None);
    }

    /// <summary>Creates a unique-domain exhausted-catalog stop where the runtime returned no slot.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <param name="applicationDomainCatalogCardinality">The exact exhausted catalog cardinality.</param>
    /// <param name="matchingApplicationDomainOrdinal">The unique matching catalog ordinal.</param>
    /// <returns>A slot-unavailable stop.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence SlotUnavailable(
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainCatalogCardinality,
        int matchingApplicationDomainOrdinal) =>
        UniqueDomainStop(
            ClrmdStaticStorageAcquisitionKind.SlotAddressUnavailable,
            pointerWidth,
            requestedApplicationDomainAddress,
            applicationDomainCatalogCardinality,
            matchingApplicationDomainOrdinal);

    /// <summary>Creates a unique-domain stop where the runtime cannot perform ordinary-static storage lookup.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <param name="applicationDomainCatalogCardinality">The exact exhausted catalog cardinality.</param>
    /// <param name="matchingApplicationDomainOrdinal">The unique matching catalog ordinal.</param>
    /// <returns>A runtime-operation unsupported stop.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence RuntimeUnsupported(
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainCatalogCardinality,
        int matchingApplicationDomainOrdinal) =>
        UniqueDomainStop(
            ClrmdStaticStorageAcquisitionKind.RuntimeStorageUnsupported,
            pointerWidth,
            requestedApplicationDomainAddress,
            applicationDomainCatalogCardinality,
            matchingApplicationDomainOrdinal);

    /// <summary>Creates a unique-domain witness retaining a nonzero returned slot whose fixed range overflows.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <param name="applicationDomainCatalogCardinality">The exact exhausted catalog cardinality.</param>
    /// <param name="matchingApplicationDomainOrdinal">The unique matching catalog ordinal.</param>
    /// <param name="returnedSlotAddress">The nonzero raw returned slot.</param>
    /// <param name="storageSize">The complete fixed storage width that overflows.</param>
    /// <returns>An invalid-slot stop.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence InvalidSlot(
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainCatalogCardinality,
        int matchingApplicationDomainOrdinal,
        ulong returnedSlotAddress,
        int storageSize)
    {
        ValidateCommon(pointerWidth, requestedApplicationDomainAddress, applicationDomainCatalogCardinality);
        ValidateOrdinal(matchingApplicationDomainOrdinal, applicationDomainCatalogCardinality);
        ValidateSlotRange(returnedSlotAddress, storageSize, pointerWidth, requireOverflow: true);
        return new ClrmdStaticStorageAcquisitionEvidence(
            ClrmdStaticStorageAcquisitionKind.InvalidSlotAddress,
            pointerWidth,
            requestedApplicationDomainAddress,
            applicationDomainCatalogCardinality,
            catalogExhaustive: true,
            ImmutableArray.Create(matchingApplicationDomainOrdinal),
            returnedSlotAddress,
            storageSize,
            ClrmdStaticStorageInvalidSlotReason.StorageRangeOverflow);
    }

    /// <summary>Creates the first-failure cap stop for a domain catalog known to contain more than sixteen entries.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <returns>A non-exhaustive cap-sized prefix with no selected domain or slot.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence CatalogLimitReached(
        int pointerWidth,
        ulong requestedApplicationDomainAddress) =>
        CatalogLimitReached(
            pointerWidth,
            requestedApplicationDomainAddress,
            ImmutableArray<int>.Empty);

    /// <summary>Creates a cap stop while retaining every requested-domain match in the examined prefix.</summary>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="requestedApplicationDomainAddress">The exact requested domain address.</param>
    /// <param name="matchingApplicationDomainOrdinals">Every distinct match ordinal in the sixteen-entry prefix.</param>
    /// <returns>A non-exhaustive cap-sized prefix that selects no domain or slot.</returns>
    public static ClrmdStaticStorageAcquisitionEvidence CatalogLimitReached(
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        ImmutableArray<int> matchingApplicationDomainOrdinals)
    {
        ValidateCommon(pointerWidth, requestedApplicationDomainAddress, MaximumApplicationDomains);
        var normalized = NormalizeOrdinals(
            matchingApplicationDomainOrdinals,
            MaximumApplicationDomains);
        return new ClrmdStaticStorageAcquisitionEvidence(
            ClrmdStaticStorageAcquisitionKind.ApplicationDomainCatalogLimitReached,
            pointerWidth,
            requestedApplicationDomainAddress,
            MaximumApplicationDomains,
            catalogExhaustive: false,
            normalized,
            returnedSlotAddress: null,
            attemptedStorageSize: null,
            ClrmdStaticStorageInvalidSlotReason.None);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdStaticStorageAcquisitionEvidence? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticStorageAcquisitionEvidence);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ClrmdStaticStorageAcquisitionEvidence UniqueDomainStop(
        ClrmdStaticStorageAcquisitionKind kind,
        int pointerWidth,
        ulong requestedApplicationDomainAddress,
        int applicationDomainCatalogCardinality,
        int matchingApplicationDomainOrdinal)
    {
        ValidateCommon(pointerWidth, requestedApplicationDomainAddress, applicationDomainCatalogCardinality);
        ValidateOrdinal(matchingApplicationDomainOrdinal, applicationDomainCatalogCardinality);
        return new ClrmdStaticStorageAcquisitionEvidence(
            kind,
            pointerWidth,
            requestedApplicationDomainAddress,
            applicationDomainCatalogCardinality,
            catalogExhaustive: true,
            ImmutableArray.Create(matchingApplicationDomainOrdinal),
            returnedSlotAddress: null,
            attemptedStorageSize: null,
            ClrmdStaticStorageInvalidSlotReason.None);
    }

    private static void ValidateCommon(int pointerWidth, ulong requestedAddress, int examined)
    {
        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        CanonicalReplayEncoding.ValidatePointerValue(requestedAddress, pointerWidth, allowZero: false, nameof(requestedAddress));
        if (examined < 0 || examined > MaximumApplicationDomains)
        {
            throw new ArgumentOutOfRangeException(nameof(examined));
        }
    }

    private static void ValidateOrdinal(int ordinal, int count)
    {
        if (ordinal < 0 || ordinal >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }
    }

    private static ImmutableArray<int> NormalizeOrdinals(ImmutableArray<int> ordinals, int count)
    {
        if (ordinals.IsDefault)
        {
            throw new ArgumentException("An initialized ordinal array is required.", nameof(ordinals));
        }
        var normalized = ordinals.Order().ToImmutableArray();
        if (normalized.Distinct().Count() != normalized.Length)
        {
            throw new ArgumentException("Matching ordinals must be distinct.", nameof(ordinals));
        }
        foreach (var ordinal in normalized)
        {
            ValidateOrdinal(ordinal, count);
        }
        return normalized;
    }

    private static void ValidateSlotRange(ulong address, int storageSize, int pointerWidth, bool requireOverflow)
    {
        if (storageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storageSize));
        }
        CanonicalReplayEncoding.ValidatePointerValue(address, pointerWidth, allowZero: false, nameof(address));
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        var overflows = (ulong)(storageSize - 1) > maximum - address;
        if (overflows != requireOverflow)
        {
            throw new ArgumentException(
                requireOverflow
                    ? "The supplied raw slot does not overflow its fixed storage range."
                    : "The supplied static slot overflows its fixed storage range.",
                nameof(address));
        }
    }
}

/// <summary>Classifies the completeness of one detached raw memory read.</summary>
public enum ClrmdRawMemoryStatus
{
    /// <summary>Every requested byte was read.</summary>
    Exact = 1,

    /// <summary>A non-empty strict prefix was read.</summary>
    Partial = 2,

    /// <summary>No requested bytes were available.</summary>
    Unavailable = 3,
}

/// <summary>Freezes one detached raw memory read without retaining a reader or stream.</summary>
public sealed class ClrmdRawMemoryEvidence : IEquatable<ClrmdRawMemoryEvidence>
{
    /// <summary>Gets the largest single raw read admitted by any fixed decoder.</summary>
    public const int MaximumReadBytes = ClrmdExactStringValue.MaximumCharacters * sizeof(char);

    /// <summary>Gets the canonical bound name for one requested or retained raw byte sequence.</summary>
    public const string MaximumReadBytesBoundName = "static-field.raw-read-bytes";

    private readonly ImmutableArray<byte> bytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdRawMemoryEvidence(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        int requestedLength,
        ClrmdRawMemoryStatus status,
        ImmutableArray<byte> bytes)
    {
        Snapshot = snapshot;
        Address = address;
        RequestedLength = requestedLength;
        Status = status;
        this.bytes = CanonicalReplayEncoding.Copy(bytes);
        var writer = new CanonicalReplayEncoding.Writer("clrmd-raw-memory-evidence", 2);
        ClrmdStaticPhysicalCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteString(MaximumReadBytesBoundName);
        writer.WriteInt32(MaximumReadBytes);
        writer.WriteUInt64(address);
        writer.WriteInt32(requestedLength);
        writer.WriteInt32((int)status);
        writer.WriteLengthPrefixedBytes(this.bytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot from which bytes were requested.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the nonzero start address.</summary>
    public ulong Address { get; }

    /// <summary>Gets the positive requested byte count.</summary>
    public int RequestedLength { get; }

    /// <summary>Gets the exact read completeness.</summary>
    public ClrmdRawMemoryStatus Status { get; }

    /// <summary>Gets a defensive copy of bytes actually read.</summary>
    public ImmutableArray<byte> Bytes => CanonicalReplayEncoding.Copy(bytes);

    /// <summary>Gets whether every requested byte is present.</summary>
    public bool IsExact => Status == ClrmdRawMemoryStatus.Exact;

    /// <summary>Gets the fixed per-read byte-count bound embedded in every raw-read identity.</summary>
    public static EvaluationDeterministicBound DeclaredReadByteCountBound =>
        new(MaximumReadBytesBoundName, MaximumReadBytes);

    /// <summary>Gets a defensive copy of the versioned canonical read bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact raw read.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="address">The nonzero start address.</param>
    /// <param name="bytes">The initialized non-empty complete bytes.</param>
    /// <returns>An exact detached read.</returns>
    public static ClrmdRawMemoryEvidence Exact(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefault)
        {
            throw new ArgumentException("An initialized byte array is required.", nameof(bytes));
        }
        return Create(snapshot, address, bytes.Length, ClrmdRawMemoryStatus.Exact, bytes);
    }

    /// <summary>Creates a partial raw read containing a non-empty strict prefix.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="address">The nonzero start address.</param>
    /// <param name="requestedLength">The positive complete requested length.</param>
    /// <param name="bytes">The initialized non-empty strict prefix.</param>
    /// <returns>A detached partial read.</returns>
    public static ClrmdRawMemoryEvidence Partial(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        int requestedLength,
        ImmutableArray<byte> bytes) =>
        Create(snapshot, address, requestedLength, ClrmdRawMemoryStatus.Partial, bytes);

    /// <summary>Creates a raw read for which no requested bytes were available.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="address">The nonzero start address.</param>
    /// <param name="requestedLength">The positive requested length.</param>
    /// <returns>A detached unavailable read.</returns>
    public static ClrmdRawMemoryEvidence Unavailable(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        int requestedLength) =>
        Create(snapshot, address, requestedLength, ClrmdRawMemoryStatus.Unavailable, ImmutableArray<byte>.Empty);

    /// <inheritdoc />
    public bool Equals(ClrmdRawMemoryEvidence? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdRawMemoryEvidence);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ClrmdRawMemoryEvidence Create(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        int requestedLength,
        ClrmdRawMemoryStatus status,
        ImmutableArray<byte> bytes)
    {
        ClrmdStaticPhysicalCanonical.ValidateSnapshot(snapshot, nameof(snapshot));
        if (address == 0 || requestedLength <= 0 || requestedLength > MaximumReadBytes ||
            (ulong)(requestedLength - 1) > ulong.MaxValue - address)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedLength),
                $"A nonzero closed requested range of at most {MaximumReadBytes} bytes is required.");
        }
        if (bytes.IsDefault)
        {
            throw new ArgumentException("An initialized byte array is required.", nameof(bytes));
        }
        var valid = status switch
        {
            ClrmdRawMemoryStatus.Exact => bytes.Length == requestedLength,
            ClrmdRawMemoryStatus.Partial => bytes.Length > 0 && bytes.Length < requestedLength,
            ClrmdRawMemoryStatus.Unavailable => bytes.IsEmpty,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("Read status, requested length, and retained bytes disagree.");
        }
        return new ClrmdRawMemoryEvidence(snapshot, address, requestedLength, status, bytes);
    }
}

/// <summary>Classifies the raw-header-first target boundary reached after a nonzero reference slot.</summary>
public enum ClrmdStaticTargetEvidenceKind
{
    /// <summary>The raw method-table header and subsequent direct runtime-type projection agree structurally.</summary>
    Matched = 1,

    /// <summary>The raw method-table header read was partial or unavailable.</summary>
    HeaderUnavailable = 2,

    /// <summary>The exact raw method table was read, but post-header runtime lookup returned no type.</summary>
    RuntimeTypeUnavailable = 3,

    /// <summary>The exact raw method table and returned runtime type projection disagree.</summary>
    RuntimeTypeConflict = 4,

    /// <summary>The target header range is structurally invalid before any read.</summary>
    InvalidStructure = 5,
}

/// <summary>Identifies a structural target failure detected before a high-level type lookup.</summary>
public enum ClrmdStaticTargetStructureIssue
{
    /// <summary>No structural issue applies.</summary>
    None = 0,

    /// <summary>The pointer-width raw header range exceeds the target address space.</summary>
    HeaderAddressOverflow = 1,

    /// <summary>The exact raw object header contains a zero method-table pointer.</summary>
    NullMethodTable = 2,
}

/// <summary>
/// Retains raw method-table header evidence before the optional post-header direct runtime-type projection.
/// </summary>
public sealed class ClrmdStaticTargetEvidence : IEquatable<ClrmdStaticTargetEvidence>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticTargetEvidence(
        ClrmdStaticTargetEvidenceKind kind,
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ulong? rawMethodTable,
        ClrmdRawMemoryEvidence? headerEvidence,
        ClrmdStaticRuntimeTypeIdentity? headerRuntimeType,
        ClrmdStaticTargetStructureIssue structureIssue)
    {
        Kind = kind;
        Snapshot = snapshot;
        PointerWidth = pointerWidth;
        TargetAddress = targetAddress;
        RawMethodTable = rawMethodTable;
        HeaderEvidence = headerEvidence;
        HeaderRuntimeType = headerRuntimeType;
        StructureIssue = structureIssue;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-target-evidence", 2);
        writer.WriteInt32((int)kind);
        ClrmdStaticPhysicalCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(targetAddress);
        writer.WriteBoolean(rawMethodTable.HasValue);
        if (rawMethodTable.HasValue)
        {
            writer.WriteUInt64(rawMethodTable.Value);
        }
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, headerEvidence?.CanonicalBytes);
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, headerRuntimeType?.CanonicalBytes);
        writer.WriteInt32((int)structureIssue);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the typed target boundary.</summary>
    public ClrmdStaticTargetEvidenceKind Kind { get; }

    /// <summary>Gets the immutable dump snapshot.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the target pointer width.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the nonzero target address decoded from the static slot.</summary>
    public ulong TargetAddress { get; }

    /// <summary>Gets the exact nonzero method table decoded from the raw header when available.</summary>
    public ulong? RawMethodTable { get; }

    /// <summary>Gets the attempted raw pointer-width header read, except for a pre-read structural failure.</summary>
    public ClrmdRawMemoryEvidence? HeaderEvidence { get; }

    /// <summary>Gets the detached runtime type returned by direct runtime lookup after the raw header when available.</summary>
    public ClrmdStaticRuntimeTypeIdentity? HeaderRuntimeType { get; }

    /// <summary>Gets the exact structural issue only for <see cref="ClrmdStaticTargetEvidenceKind.InvalidStructure"/>.</summary>
    public ClrmdStaticTargetStructureIssue StructureIssue { get; }

    /// <summary>Gets a defensive copy of the versioned canonical target bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a matched raw header and post-header runtime-type projection.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="targetAddress">The nonzero target address.</param>
    /// <param name="headerEvidence">The exact pointer-width raw header read.</param>
    /// <param name="headerRuntimeType">The directly projected type matching the decoded method table.</param>
    /// <returns>A structurally matched target witness; Product still proves semantic declared-type parity.</returns>
    public static ClrmdStaticTargetEvidence Matched(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ClrmdRawMemoryEvidence headerEvidence,
        ClrmdStaticRuntimeTypeIdentity headerRuntimeType)
    {
        var rawMethodTable = ValidateExactHeader(snapshot, pointerWidth, targetAddress, headerEvidence);
        ValidateRuntimeType(snapshot, pointerWidth, rawMethodTable, headerRuntimeType, requireMatch: true);
        return new ClrmdStaticTargetEvidence(
            ClrmdStaticTargetEvidenceKind.Matched,
            snapshot,
            pointerWidth,
            targetAddress,
            rawMethodTable,
            headerEvidence,
            headerRuntimeType,
            ClrmdStaticTargetStructureIssue.None);
    }

    /// <summary>Creates a target boundary stopped by a partial or unavailable raw header read.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="targetAddress">The nonzero target address.</param>
    /// <param name="headerEvidence">The non-exact pointer-width header read.</param>
    /// <returns>A raw-header-unavailable boundary with no runtime type lookup.</returns>
    public static ClrmdStaticTargetEvidence HeaderUnavailable(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ClrmdRawMemoryEvidence headerEvidence)
    {
        ValidateHeaderCoordinates(snapshot, pointerWidth, targetAddress, headerEvidence);
        if (headerEvidence.IsExact)
        {
            throw new ArgumentException("An exact header cannot be classified unavailable.", nameof(headerEvidence));
        }
        return new ClrmdStaticTargetEvidence(
            ClrmdStaticTargetEvidenceKind.HeaderUnavailable,
            snapshot,
            pointerWidth,
            targetAddress,
            rawMethodTable: null,
            headerEvidence,
            headerRuntimeType: null,
            ClrmdStaticTargetStructureIssue.None);
    }

    /// <summary>Creates a boundary where the raw header was exact but GetTypeByMethodTable returned no type.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="targetAddress">The nonzero target address.</param>
    /// <param name="headerEvidence">The exact pointer-width raw header read.</param>
    /// <returns>A post-header runtime-type-unavailable boundary.</returns>
    public static ClrmdStaticTargetEvidence RuntimeTypeUnavailable(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ClrmdRawMemoryEvidence headerEvidence)
    {
        var rawMethodTable = ValidateExactHeader(snapshot, pointerWidth, targetAddress, headerEvidence);
        return new ClrmdStaticTargetEvidence(
            ClrmdStaticTargetEvidenceKind.RuntimeTypeUnavailable,
            snapshot,
            pointerWidth,
            targetAddress,
            rawMethodTable,
            headerEvidence,
            headerRuntimeType: null,
            ClrmdStaticTargetStructureIssue.None);
    }

    /// <summary>Creates a boundary retaining the conflicting runtime type returned for an exact raw method table.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="targetAddress">The nonzero target address.</param>
    /// <param name="headerEvidence">The exact pointer-width raw header read.</param>
    /// <param name="headerRuntimeType">The structurally conflicting returned runtime type.</param>
    /// <returns>A typed post-header conflict boundary.</returns>
    public static ClrmdStaticTargetEvidence RuntimeTypeConflict(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ClrmdRawMemoryEvidence headerEvidence,
        ClrmdStaticRuntimeTypeIdentity headerRuntimeType)
    {
        var rawMethodTable = ValidateExactHeader(snapshot, pointerWidth, targetAddress, headerEvidence);
        ValidateRuntimeType(snapshot, pointerWidth, rawMethodTable, headerRuntimeType, requireMatch: false);
        return new ClrmdStaticTargetEvidence(
            ClrmdStaticTargetEvidenceKind.RuntimeTypeConflict,
            snapshot,
            pointerWidth,
            targetAddress,
            rawMethodTable,
            headerEvidence,
            headerRuntimeType,
            ClrmdStaticTargetStructureIssue.None);
    }

    /// <summary>Creates a pre-read structural stop for a target whose header range overflows.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="targetAddress">The nonzero target address.</param>
    /// <returns>A header-address-overflow boundary with no read or runtime lookup.</returns>
    public static ClrmdStaticTargetEvidence InvalidHeaderAddress(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress)
    {
        ClrmdStaticPhysicalCanonical.ValidateSnapshot(snapshot, nameof(snapshot));
        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        CanonicalReplayEncoding.ValidatePointerValue(targetAddress, pointerWidth, allowZero: false, nameof(targetAddress));
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((ulong)(pointerWidth - 1) <= maximum - targetAddress)
        {
            throw new ArgumentException("The supplied target has an addressable raw header.", nameof(targetAddress));
        }
        return new ClrmdStaticTargetEvidence(
            ClrmdStaticTargetEvidenceKind.InvalidStructure,
            snapshot,
            pointerWidth,
            targetAddress,
            rawMethodTable: null,
            headerEvidence: null,
            headerRuntimeType: null,
            ClrmdStaticTargetStructureIssue.HeaderAddressOverflow);
    }

    /// <summary>Creates a structural stop for an exact raw object header whose method table is zero.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="pointerWidth">The target pointer width.</param>
    /// <param name="targetAddress">The nonzero target address.</param>
    /// <param name="headerEvidence">The exact pointer-width all-zero raw header.</param>
    /// <returns>A typed invalid-method-table witness retaining the exact header bytes.</returns>
    public static ClrmdStaticTargetEvidence InvalidMethodTable(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ClrmdRawMemoryEvidence headerEvidence)
    {
        ValidateHeaderCoordinates(snapshot, pointerWidth, targetAddress, headerEvidence);
        if (!headerEvidence.IsExact ||
            ClrmdStaticPhysicalCanonical.DecodePointer(headerEvidence.Bytes.AsSpan(), pointerWidth) != 0)
        {
            throw new ArgumentException(
                "Invalid-method-table evidence requires an exact all-zero pointer-width header.",
                nameof(headerEvidence));
        }
        return new ClrmdStaticTargetEvidence(
            ClrmdStaticTargetEvidenceKind.InvalidStructure,
            snapshot,
            pointerWidth,
            targetAddress,
            rawMethodTable: null,
            headerEvidence,
            headerRuntimeType: null,
            ClrmdStaticTargetStructureIssue.NullMethodTable);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdStaticTargetEvidence? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticTargetEvidence);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ulong ValidateExactHeader(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ClrmdRawMemoryEvidence headerEvidence)
    {
        ValidateHeaderCoordinates(snapshot, pointerWidth, targetAddress, headerEvidence);
        if (!headerEvidence.IsExact)
        {
            throw new ArgumentException("An exact pointer-width raw header is required.", nameof(headerEvidence));
        }
        var methodTable = ClrmdStaticPhysicalCanonical.DecodePointer(headerEvidence.Bytes.AsSpan(), pointerWidth);
        CanonicalReplayEncoding.ValidatePointerValue(methodTable, pointerWidth, allowZero: false, nameof(headerEvidence));
        return methodTable;
    }

    private static void ValidateHeaderCoordinates(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong targetAddress,
        ClrmdRawMemoryEvidence headerEvidence)
    {
        ArgumentNullException.ThrowIfNull(headerEvidence);
        ClrmdStaticPhysicalCanonical.ValidateSnapshot(snapshot, nameof(snapshot));
        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        CanonicalReplayEncoding.ValidatePointerValue(targetAddress, pointerWidth, allowZero: false, nameof(targetAddress));
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((ulong)(pointerWidth - 1) > maximum - targetAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(targetAddress));
        }
        if (headerEvidence.Snapshot != snapshot || headerEvidence.Address != targetAddress ||
            headerEvidence.RequestedLength != pointerWidth)
        {
            throw new ArgumentException("The raw header read must cover this exact target pointer.", nameof(headerEvidence));
        }
    }

    private static void ValidateRuntimeType(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong rawMethodTable,
        ClrmdStaticRuntimeTypeIdentity runtimeType,
        bool requireMatch)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        var structurallyMatches = runtimeType.Snapshot == snapshot &&
            runtimeType.PointerWidth == pointerWidth &&
            runtimeType.MethodTable == rawMethodTable;
        if (structurallyMatches != requireMatch)
        {
            throw new ArgumentException(
                requireMatch
                    ? "The returned runtime type must carry this exact raw method table, snapshot, and pointer width."
                    : "Conflict evidence requires a returned runtime type that structurally disagrees with the raw header.",
                nameof(runtimeType));
        }
    }
}

/// <summary>Retains one exact non-null reference from a slot pointer, raw header, and post-header type lookup.</summary>
public sealed class ClrmdExactObjectReference : IEquatable<ClrmdExactObjectReference>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdExactObjectReference(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong address,
        ulong methodTable,
        ClrmdRawMemoryEvidence headerEvidence,
        ClrmdStaticRuntimeTypeIdentity headerRuntimeType)
    {
        Snapshot = snapshot;
        PointerWidth = pointerWidth;
        Address = address;
        MethodTable = methodTable;
        HeaderEvidence = headerEvidence;
        HeaderRuntimeType = headerRuntimeType;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-exact-object-reference", 2);
        ClrmdStaticPhysicalCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(address);
        writer.WriteUInt64(methodTable);
        writer.WriteLengthPrefixedBytes(headerEvidence.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(headerRuntimeType.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the target pointer width.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the exact nonzero object address.</summary>
    public ulong Address { get; }

    /// <summary>Gets the exact nonzero method table decoded from raw memory.</summary>
    public ulong MethodTable { get; }

    /// <summary>Gets the exact pointer-width raw header read.</summary>
    public ClrmdRawMemoryEvidence HeaderEvidence { get; }

    /// <summary>Gets the exact detached runtime type returned by GetTypeByMethodTable after the header.</summary>
    public ClrmdStaticRuntimeTypeIdentity HeaderRuntimeType { get; }

    /// <summary>Gets a defensive copy of the versioned canonical object-reference bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact object reference from a matched raw-header-first target witness.</summary>
    /// <param name="targetEvidence">Matched header and GetTypeByMethodTable evidence.</param>
    /// <returns>An immutable exact physical object reference without extent or high-level object facts.</returns>
    public static ClrmdExactObjectReference Create(ClrmdStaticTargetEvidence targetEvidence)
    {
        ArgumentNullException.ThrowIfNull(targetEvidence);
        if (targetEvidence.Kind != ClrmdStaticTargetEvidenceKind.Matched ||
            targetEvidence.RawMethodTable is not { } methodTable ||
            targetEvidence.HeaderEvidence is not { } headerEvidence ||
            targetEvidence.HeaderRuntimeType is not { } headerRuntimeType)
        {
            throw new ArgumentException("An exact object requires a matched raw-header-first target witness.", nameof(targetEvidence));
        }
        return new ClrmdExactObjectReference(
            targetEvidence.Snapshot,
            targetEvidence.PointerWidth,
            targetEvidence.TargetAddress,
            methodTable,
            headerEvidence,
            headerRuntimeType);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdExactObjectReference? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdExactObjectReference);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Retains an exact bounded string from raw header, length, and UTF-16 character reads.</summary>
public sealed class ClrmdExactStringValue : IEquatable<ClrmdExactStringValue>
{
    /// <summary>Gets the maximum admitted UTF-16 character count.</summary>
    public const int MaximumCharacters = 4096;

    /// <summary>Gets the deterministic-bound name for the character cap.</summary>
    public const string CharacterLimitBoundName = "query.observed-string.characters";

    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdExactStringValue(
        ClrmdExactObjectReference objectReference,
        string value,
        ClrmdRawMemoryEvidence lengthEvidence,
        ClrmdRawMemoryEvidence? characterEvidence)
    {
        ObjectReference = objectReference;
        Value = value;
        LengthEvidence = lengthEvidence;
        CharacterEvidence = characterEvidence;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-exact-string-value", 2);
        writer.WriteLengthPrefixedBytes(objectReference.CanonicalBytes.AsSpan());
        writer.WriteString(value);
        writer.WriteLengthPrefixedBytes(lengthEvidence.CanonicalBytes.AsSpan());
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, characterEvidence?.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact non-null object reference and raw header evidence.</summary>
    public ClrmdExactObjectReference ObjectReference { get; }

    /// <summary>Gets the exact bounded managed string.</summary>
    public string Value { get; }

    /// <summary>Gets the exact four-byte little-endian length read.</summary>
    public ClrmdRawMemoryEvidence LengthEvidence { get; }

    /// <summary>Gets exact UTF-16 bytes for a non-empty value, otherwise null.</summary>
    public ClrmdRawMemoryEvidence? CharacterEvidence { get; }

    /// <summary>Gets the fixed character-count bound.</summary>
    public static EvaluationDeterministicBound DeclaredCharacterLimitBound =>
        new(CharacterLimitBoundName, MaximumCharacters);

    /// <summary>Gets a defensive copy of the versioned canonical string bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact bounded string after raw-header-first target validation.</summary>
    /// <param name="objectReference">The exact matched System.String physical reference.</param>
    /// <param name="value">The exact decoded UTF-16 value.</param>
    /// <param name="lengthEvidence">The exact four-byte length read.</param>
    /// <param name="characterEvidence">Exact UTF-16 bytes for a non-empty value; null for empty.</param>
    /// <returns>An immutable exact raw-memory string value.</returns>
    public static ClrmdExactStringValue Create(
        ClrmdExactObjectReference objectReference,
        string value,
        ClrmdRawMemoryEvidence lengthEvidence,
        ClrmdRawMemoryEvidence? characterEvidence)
    {
        ArgumentNullException.ThrowIfNull(objectReference);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(lengthEvidence);
        if (value.Length > MaximumCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        var lengthAddress = ClrmdStaticPhysicalCanonical.AddOffset(
            objectReference.Address,
            objectReference.PointerWidth,
            sizeof(int),
            objectReference.PointerWidth,
            "string length");
        ValidateExactRead(lengthEvidence, objectReference.Snapshot, lengthAddress, sizeof(int), nameof(lengthEvidence));
        var decodedLength = BinaryPrimitives.ReadInt32LittleEndian(lengthEvidence.Bytes.AsSpan());
        if (decodedLength != value.Length)
        {
            throw new ArgumentException("The exact raw string length disagrees with the decoded value.", nameof(lengthEvidence));
        }
        if (value.Length == 0)
        {
            if (characterEvidence is not null)
            {
                throw new ArgumentException("An empty string must not fabricate a character read.", nameof(characterEvidence));
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(characterEvidence);
            var byteCount = checked(value.Length * sizeof(char));
            var characterAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                objectReference.Address,
                objectReference.PointerWidth + sizeof(int),
                byteCount,
                objectReference.PointerWidth,
                "string characters");
            ValidateExactRead(characterEvidence, objectReference.Snapshot, characterAddress, byteCount, nameof(characterEvidence));
            if (!System.Text.Encoding.Unicode.GetBytes(value).AsSpan().SequenceEqual(characterEvidence.Bytes.AsSpan()))
            {
                throw new ArgumentException("The raw UTF-16 bytes disagree with the decoded string.", nameof(characterEvidence));
            }
        }
        return new ClrmdExactStringValue(objectReference, value, lengthEvidence, characterEvidence);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdExactStringValue? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdExactStringValue);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateExactRead(
        ClrmdRawMemoryEvidence read,
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        int length,
        string parameterName)
    {
        if (!read.IsExact || read.Snapshot != snapshot || read.Address != address || read.RequestedLength != length)
        {
            throw new ArgumentException("The exact read does not cover the required string range.", parameterName);
        }
    }
}

/// <summary>Represents one exact terminal from a fixed physical static-field decoder.</summary>
public sealed class ClrmdStaticFieldValue : IEquatable<ClrmdStaticFieldValue>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticFieldValue(
        ClrmdStaticFieldTerminalKind kind,
        int? int32Value,
        ClrmdExactStringValue? stringValue,
        ClrmdExactObjectReference? objectReference)
    {
        Kind = kind;
        Int32Value = int32Value;
        StringValue = stringValue;
        ObjectReference = objectReference;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-field-value", 2);
        writer.WriteInt32((int)kind);
        writer.WriteBoolean(int32Value.HasValue);
        if (int32Value.HasValue)
        {
            writer.WriteInt32(int32Value.Value);
        }
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, stringValue?.CanonicalBytes);
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, objectReference?.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact terminal discriminator.</summary>
    public ClrmdStaticFieldTerminalKind Kind { get; }

    /// <summary>Gets Int32 for scalar, Nullable value, otherwise null.</summary>
    public int? Int32Value { get; }

    /// <summary>Gets the exact string payload only for a string terminal.</summary>
    public ClrmdExactStringValue? StringValue { get; }

    /// <summary>Gets the exact physical object only for an object-reference terminal.</summary>
    public ClrmdExactObjectReference? ObjectReference { get; }

    /// <summary>Gets a defensive copy of the versioned canonical terminal bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact null managed reference.</summary>
    /// <returns>The canonical null terminal.</returns>
    public static ClrmdStaticFieldValue NullReference() =>
        new(ClrmdStaticFieldTerminalKind.Null, null, null, null);

    /// <summary>Creates an exact scalar Int32 terminal.</summary>
    /// <param name="value">The exact decoded value.</param>
    /// <returns>An Int32 terminal.</returns>
    public static ClrmdStaticFieldValue ExactInt32(int value) =>
        new(ClrmdStaticFieldTerminalKind.Int32, value, null, null);

    /// <summary>Creates an exact Nullable&lt;Int32&gt; terminal with HasValue false.</summary>
    /// <returns>A no-value nullable terminal.</returns>
    public static ClrmdStaticFieldValue NullableInt32NoValue() =>
        new(ClrmdStaticFieldTerminalKind.NullableInt32NoValue, null, null, null);

    /// <summary>Creates an exact Nullable&lt;Int32&gt; terminal with HasValue true.</summary>
    /// <param name="value">The exact decoded Int32 child.</param>
    /// <returns>A nullable-value terminal.</returns>
    public static ClrmdStaticFieldValue NullableInt32Value(int value) =>
        new(ClrmdStaticFieldTerminalKind.NullableInt32Value, value, null, null);

    /// <summary>Creates an exact bounded string terminal.</summary>
    /// <param name="value">The exact raw-memory string value.</param>
    /// <returns>A string terminal.</returns>
    public static ClrmdStaticFieldValue ExactString(ClrmdExactStringValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ClrmdStaticFieldValue(ClrmdStaticFieldTerminalKind.String, null, value, null);
    }

    /// <summary>Creates an exact non-null object-reference terminal.</summary>
    /// <param name="value">The exact raw-header-first object reference.</param>
    /// <returns>An object-reference terminal.</returns>
    public static ClrmdStaticFieldValue ExactObjectReference(ClrmdExactObjectReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ClrmdStaticFieldValue(ClrmdStaticFieldTerminalKind.ObjectReference, null, null, value);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdStaticFieldValue? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticFieldValue);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>
/// Freezes exact acquisition topology, ordered raw reads, and the terminal or first physical failure of one request.
/// </summary>
public sealed class ClrmdStaticFieldValueObservation : IEquatable<ClrmdStaticFieldValueObservation>
{
    /// <summary>Gets the maximum raw read count used by any fixed W7 decoder.</summary>
    public const int MaximumRawReadCount = 4;

    /// <summary>Gets the deterministic-bound name for the ordered raw read count.</summary>
    public const string MaximumRawReadCountBoundName = "static-field.raw-read-count";

    private readonly ImmutableArray<ClrmdRawMemoryEvidence> reads;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticFieldValueObservation(
        ClrmdSnapshotIdentity snapshot,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue,
        ClrmdStaticFieldEvaluationRequest? request,
        ulong? slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldValue? value,
        ClrmdStaticTargetEvidence? targetEvidence,
        ClrmdStaticStorageAcquisitionEvidence? storageAcquisitionEvidence,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        Snapshot = snapshot;
        Status = status;
        Issue = issue;
        Request = request;
        SlotAddress = slotAddress;
        this.reads = reads;
        Value = value;
        TargetEvidence = targetEvidence;
        StorageAcquisitionEvidence = storageAcquisitionEvidence;
        this.reachedBounds = reachedBounds;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-field-value-observation", 3);
        ClrmdStaticPhysicalCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, request?.CanonicalBytes);
        writer.WriteBoolean(slotAddress.HasValue);
        if (slotAddress.HasValue)
        {
            writer.WriteUInt64(slotAddress.Value);
        }
        writer.WriteInt32(reads.Length);
        foreach (var read in reads)
        {
            writer.WriteLengthPrefixedBytes(read.CanonicalBytes.AsSpan());
        }
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, value?.CanonicalBytes);
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, targetEvidence?.CanonicalBytes);
        ClrmdStaticPhysicalCanonical.WriteOptionalCanonical(writer, storageAcquisitionEvidence?.CanonicalBytes);
        ClrmdStaticPhysicalCanonical.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot containing all request and raw evidence.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the exact observation disposition.</summary>
    public ClrmdStaticFieldObservationStatus Status { get; }

    /// <summary>Gets the typed first issue, or None for exact.</summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>Gets Product's physical request, or null only for a failure before exact request construction.</summary>
    public ClrmdStaticFieldEvaluationRequest? Request { get; }

    /// <summary>Gets the acquired valid nonzero slot after successful acquisition, otherwise null.</summary>
    public ulong? SlotAddress { get; }

    /// <summary>Gets a defensive execution-ordered copy of all raw reads attempted by the fixed decoder.</summary>
    public ImmutableArray<ClrmdRawMemoryEvidence> Reads => CanonicalReplayEncoding.Copy(reads);

    /// <summary>Gets the exact terminal only when <see cref="Status"/> is exact.</summary>
    public ClrmdStaticFieldValue? Value { get; }

    /// <summary>Gets the raw-header-first target boundary for a non-exact nonzero reference.</summary>
    public ClrmdStaticTargetEvidence? TargetEvidence { get; }

    /// <summary>Gets exact acquisition topology for every request that reached domain lookup.</summary>
    public ClrmdStaticStorageAcquisitionEvidence? StorageAcquisitionEvidence { get; }

    /// <summary>Gets a defensive ordinally ordered copy of fixed operation bounds actually reached.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds => CanonicalReplayEncoding.Copy(reachedBounds);

    /// <summary>Gets the fixed raw-read count bound.</summary>
    public static EvaluationDeterministicBound DeclaredRawReadCountBound =>
        new(MaximumRawReadCountBoundName, MaximumRawReadCount);

    /// <summary>Gets a defensive copy of the versioned canonical observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact observation with mandatory unique-domain/slot acquisition evidence.</summary>
    /// <param name="request">The Product-created fixed physical request.</param>
    /// <param name="acquisitionEvidence">Exact exhausted-catalog unique-domain and acquired-slot evidence.</param>
    /// <param name="reads">Every exact raw read used by the decoder.</param>
    /// <param name="value">The exact terminal derived from those reads.</param>
    /// <param name="reachedBounds">Only fixed operation bounds actually reached.</param>
    /// <returns>An immutable exact observation.</returns>
    public static ClrmdStaticFieldValueObservation Exact(
        ClrmdStaticFieldEvaluationRequest request,
        ClrmdStaticStorageAcquisitionEvidence acquisitionEvidence,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldValue value,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(acquisitionEvidence);
        ArgumentNullException.ThrowIfNull(value);
        if (acquisitionEvidence.Kind != ClrmdStaticStorageAcquisitionKind.SlotAddressAcquired ||
            acquisitionEvidence.ReturnedSlotAddress is not { } slotAddress)
        {
            throw new ArgumentException("Exact evaluation requires exact acquired-slot evidence.", nameof(acquisitionEvidence));
        }
        ValidateAcquisition(request, acquisitionEvidence, slotAddress);
        var normalizedReads = NormalizeReads(reads, request.Snapshot);
        if (normalizedReads.IsEmpty || normalizedReads.Any(static read => !read.IsExact))
        {
            throw new ArgumentException("Exact evaluation requires non-empty exact raw reads.", nameof(reads));
        }
        var normalizedBounds = ValidateBounds(
            reachedBounds,
            acquisitionRequired: true,
            readsRequired: true,
            stringCapRequired: request.ValueShape == ClrmdStaticFieldValueShape.String &&
                value.Kind == ClrmdStaticFieldTerminalKind.String);
        ValidateExactDecoder(request, slotAddress, normalizedReads, value);
        return new ClrmdStaticFieldValueObservation(
            request.Snapshot,
            ClrmdStaticFieldObservationStatus.Exact,
            ClrmdValueIssue.None,
            request,
            slotAddress,
            normalizedReads,
            value,
            targetEvidence: null,
            acquisitionEvidence,
            normalizedBounds);
    }

    /// <summary>Creates a partial observation with no exact terminal.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="issue">The exact partial-stop issue.</param>
    /// <param name="request">The physical request when constructed.</param>
    /// <param name="slotAddress">The acquired slot when reached.</param>
    /// <param name="reads">Ordered raw reads attempted before the stop.</param>
    /// <param name="reachedBounds">Only fixed operation bounds actually reached.</param>
    /// <param name="targetEvidence">Optional raw-header-first target boundary.</param>
    /// <param name="storageAcquisitionEvidence">Exact acquisition or cap-stop topology.</param>
    /// <returns>An immutable partial observation.</returns>
    public static ClrmdStaticFieldValueObservation Partial(
        ClrmdSnapshotIdentity snapshot,
        ClrmdValueIssue issue,
        ClrmdStaticFieldEvaluationRequest? request,
        ulong? slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticTargetEvidence? targetEvidence = null,
        ClrmdStaticStorageAcquisitionEvidence? storageAcquisitionEvidence = null) =>
        NonExact(snapshot, ClrmdStaticFieldObservationStatus.Partial, issue, request, slotAddress, reads,
            reachedBounds, targetEvidence, storageAcquisitionEvidence);

    /// <summary>Creates an unavailable observation with no exact terminal.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="issue">The exact unavailable-stop issue.</param>
    /// <param name="request">The physical request when constructed.</param>
    /// <param name="slotAddress">The acquired slot when reached.</param>
    /// <param name="reads">Ordered raw reads attempted before the stop.</param>
    /// <param name="reachedBounds">Only fixed operation bounds actually reached.</param>
    /// <param name="targetEvidence">Optional raw-header-first target boundary.</param>
    /// <param name="storageAcquisitionEvidence">Exact acquisition topology.</param>
    /// <returns>An immutable unavailable observation.</returns>
    public static ClrmdStaticFieldValueObservation Unavailable(
        ClrmdSnapshotIdentity snapshot,
        ClrmdValueIssue issue,
        ClrmdStaticFieldEvaluationRequest? request,
        ulong? slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticTargetEvidence? targetEvidence = null,
        ClrmdStaticStorageAcquisitionEvidence? storageAcquisitionEvidence = null) =>
        NonExact(snapshot, ClrmdStaticFieldObservationStatus.Unavailable, issue, request, slotAddress, reads,
            reachedBounds, targetEvidence, storageAcquisitionEvidence);

    /// <summary>Creates a conflict observation with no exact terminal.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="issue">The exact conflict issue.</param>
    /// <param name="request">The Product-created physical request.</param>
    /// <param name="slotAddress">The acquired slot.</param>
    /// <param name="reads">Ordered exact or incomplete raw reads.</param>
    /// <param name="reachedBounds">Only fixed operation bounds actually reached.</param>
    /// <param name="targetEvidence">The typed conflicting target evidence.</param>
    /// <param name="storageAcquisitionEvidence">Exact acquired-slot topology.</param>
    /// <returns>An immutable conflict observation.</returns>
    public static ClrmdStaticFieldValueObservation Conflict(
        ClrmdSnapshotIdentity snapshot,
        ClrmdValueIssue issue,
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticTargetEvidence targetEvidence,
        ClrmdStaticStorageAcquisitionEvidence storageAcquisitionEvidence) =>
        NonExact(snapshot, ClrmdStaticFieldObservationStatus.Conflict, issue, request, slotAddress, reads,
            reachedBounds, targetEvidence, storageAcquisitionEvidence);

    /// <summary>Creates a post-slot physical conflict that does not describe a managed-reference target.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="issue">The exact conflicting physical fact.</param>
    /// <param name="request">The Product-created scalar or nullable physical request.</param>
    /// <param name="slotAddress">The exact acquired slot.</param>
    /// <param name="reads">Ordered raw reads ending at the conflicting fact.</param>
    /// <param name="reachedBounds">Only fixed operation bounds actually reached.</param>
    /// <param name="storageAcquisitionEvidence">Exact acquired-slot topology.</param>
    /// <returns>An immutable conflict with no managed-target projection.</returns>
    public static ClrmdStaticFieldValueObservation Conflict(
        ClrmdSnapshotIdentity snapshot,
        ClrmdValueIssue issue,
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticStorageAcquisitionEvidence storageAcquisitionEvidence) =>
        NonExact(
            snapshot,
            ClrmdStaticFieldObservationStatus.Conflict,
            issue,
            request,
            slotAddress,
            reads,
            reachedBounds,
            targetEvidence: null,
            storageAcquisitionEvidence);

    /// <summary>Creates an exhaustive no-slot conflict for an ambiguous requested application domain.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="request">The exact Product-created physical request.</param>
    /// <param name="reachedBounds">Only the reached application-domain catalog bound.</param>
    /// <param name="storageAcquisitionEvidence">The exhaustive ambiguous-domain acquisition topology.</param>
    /// <returns>An immutable conflict proving storage was not selected or read.</returns>
    public static ClrmdStaticFieldValueObservation Conflict(
        ClrmdSnapshotIdentity snapshot,
        ClrmdStaticFieldEvaluationRequest request,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticStorageAcquisitionEvidence storageAcquisitionEvidence) =>
        NonExact(
            snapshot,
            ClrmdStaticFieldObservationStatus.Conflict,
            ClrmdValueIssue.AmbiguousMatch,
            request,
            slotAddress: null,
            ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
            reachedBounds,
            targetEvidence: null,
            storageAcquisitionEvidence);

    /// <summary>Creates an invalid physical-layout observation with no exact terminal.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="issue">The exact invalid-data issue.</param>
    /// <param name="request">The physical request when constructed.</param>
    /// <param name="slotAddress">The acquired slot when valid.</param>
    /// <param name="reads">Ordered raw reads attempted before the stop.</param>
    /// <param name="reachedBounds">Only fixed operation bounds actually reached.</param>
    /// <param name="targetEvidence">Optional structural target failure.</param>
    /// <param name="storageAcquisitionEvidence">Exact acquisition topology.</param>
    /// <returns>An immutable invalid observation.</returns>
    public static ClrmdStaticFieldValueObservation Invalid(
        ClrmdSnapshotIdentity snapshot,
        ClrmdValueIssue issue,
        ClrmdStaticFieldEvaluationRequest request,
        ulong? slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticTargetEvidence? targetEvidence,
        ClrmdStaticStorageAcquisitionEvidence storageAcquisitionEvidence) =>
        NonExact(snapshot, ClrmdStaticFieldObservationStatus.Invalid, issue, request, slotAddress, reads,
            reachedBounds, targetEvidence, storageAcquisitionEvidence);

    /// <summary>Creates an unsupported runtime storage-operation observation.</summary>
    /// <param name="snapshot">The immutable dump identity.</param>
    /// <param name="request">The Product-created physical request.</param>
    /// <param name="reachedBounds">Only the reached domain catalog bound.</param>
    /// <param name="storageAcquisitionEvidence">The exact unique-domain runtime-unsupported stop.</param>
    /// <returns>An immutable unsupported observation.</returns>
    public static ClrmdStaticFieldValueObservation Unsupported(
        ClrmdSnapshotIdentity snapshot,
        ClrmdStaticFieldEvaluationRequest request,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticStorageAcquisitionEvidence storageAcquisitionEvidence) =>
        NonExact(snapshot, ClrmdStaticFieldObservationStatus.Unsupported, ClrmdValueIssue.RuntimeUnsupported,
            request, null, ImmutableArray<ClrmdRawMemoryEvidence>.Empty, reachedBounds, null, storageAcquisitionEvidence);

    /// <summary>Creates a typed stop when an exact detached request cannot be rebound before storage acquisition.</summary>
    /// <param name="snapshot">The immutable active dump identity.</param>
    /// <param name="status">The non-exact mapping or rebind status.</param>
    /// <param name="issue">The exact non-None reason storage was not consulted.</param>
    /// <returns>A pre-request-shaped observation carrying no slot, memory, or acquisition evidence.</returns>
    /// <remarks>
    /// This internal seam deliberately omits the supplied request: a failed live-object rebind has not re-established
    /// the physical declaration needed to authorize storage access. The canonical request remains available to the
    /// caller, while the observation truthfully proves that no storage operation occurred.
    /// </remarks>
    internal static ClrmdStaticFieldValueObservation PreStorageRebindFailure(
        ClrmdSnapshotIdentity snapshot,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue) =>
        NonExact(
            snapshot,
            status,
            issue,
            request: null,
            slotAddress: null,
            ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            targetEvidence: null,
            acquisitionEvidence: null);

    /// <inheritdoc />
    public bool Equals(ClrmdStaticFieldValueObservation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticFieldValueObservation);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ClrmdStaticFieldValueObservation NonExact(
        ClrmdSnapshotIdentity snapshot,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue,
        ClrmdStaticFieldEvaluationRequest? request,
        ulong? slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ClrmdStaticTargetEvidence? targetEvidence,
        ClrmdStaticStorageAcquisitionEvidence? acquisitionEvidence)
    {
        ClrmdStaticPhysicalCanonical.ValidateSnapshot(snapshot, nameof(snapshot));
        if (status is ClrmdStaticFieldObservationStatus.Exact || issue == ClrmdValueIssue.None)
        {
            throw new ArgumentException("A non-exact observation requires a non-None issue and non-exact status.");
        }
        if (request is null)
        {
            if (slotAddress.HasValue || !reads.IsDefaultOrEmpty || targetEvidence is not null || acquisitionEvidence is not null)
            {
                throw new ArgumentException("A pre-request failure cannot retain acquisition, slot, target, or memory evidence.");
            }
            var preRequestBounds = ValidateBounds(reachedBounds, false, false, false);
            return new ClrmdStaticFieldValueObservation(snapshot, status, issue, null, null,
                ImmutableArray<ClrmdRawMemoryEvidence>.Empty, null, null, null, preRequestBounds);
        }
        if (request.Snapshot != snapshot)
        {
            throw new ArgumentException("Request and observation snapshots disagree.", nameof(request));
        }
        ArgumentNullException.ThrowIfNull(acquisitionEvidence);
        var normalizedReads = NormalizeReads(reads, snapshot);
        if (slotAddress.HasValue)
        {
            if (acquisitionEvidence.Kind != ClrmdStaticStorageAcquisitionKind.SlotAddressAcquired)
            {
                throw new ArgumentException("Every post-slot observation requires acquired-slot evidence.", nameof(acquisitionEvidence));
            }
            ValidateAcquisition(request, acquisitionEvidence, slotAddress.Value);
            if (normalizedReads.IsEmpty)
            {
                throw new ArgumentException("A post-slot physical failure must retain its attempted raw read.", nameof(reads));
            }
            ValidatePostSlotFailure(request, slotAddress.Value, normalizedReads, status, issue, targetEvidence, reachedBounds);
        }
        else
        {
            if (!normalizedReads.IsEmpty || targetEvidence is not null)
            {
                throw new ArgumentException("A no-slot acquisition stop cannot retain reads or target evidence.");
            }
            ValidateNoSlotDisposition(request, acquisitionEvidence, status, issue);
        }
        var normalizedBounds = ValidateBounds(
            reachedBounds,
            acquisitionRequired: true,
            readsRequired: !normalizedReads.IsEmpty,
            stringCapRequired: RequiresStringCap(request, slotAddress, normalizedReads));
        return new ClrmdStaticFieldValueObservation(snapshot, status, issue, request, slotAddress,
            normalizedReads, null, targetEvidence, acquisitionEvidence, normalizedBounds);
    }

    private static void ValidateAcquisition(
        ClrmdStaticFieldEvaluationRequest request,
        ClrmdStaticStorageAcquisitionEvidence evidence,
        ulong slotAddress)
    {
        if (evidence.PointerWidth != request.PointerWidth ||
            evidence.RequestedApplicationDomainAddress != request.ApplicationDomainAddress ||
            !evidence.CatalogExhaustive ||
            evidence.MatchingApplicationDomainOrdinal is null ||
            evidence.ReturnedSlotAddress != slotAddress ||
            evidence.AttemptedStorageSize != request.StorageSize)
        {
            throw new ArgumentException(
                "Acquisition pointer width, requested domain, exhaustive unique ordinal, and slot must match the request.",
                nameof(evidence));
        }
        ValidateSlot(request, slotAddress);
    }

    private static void ValidateSlot(ClrmdStaticFieldEvaluationRequest request, ulong slotAddress)
    {
        CanonicalReplayEncoding.ValidatePointerValue(slotAddress, request.PointerWidth, allowZero: false, nameof(slotAddress));
        var maximum = request.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((ulong)(request.StorageSize - 1) > maximum - slotAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(slotAddress));
        }
    }

    private static void ValidateExactDecoder(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldValue value)
    {
        switch (request.ValueShape)
        {
            case ClrmdStaticFieldValueShape.Int32:
            {
                if (reads.Length != 1)
                {
                    throw new ArgumentException("Int32 decoding requires exactly one slot read.", nameof(reads));
                }
                var read = RequireExactReadAt(reads, 0, slotAddress, sizeof(int), "Int32 slot");
                if (value.Kind != ClrmdStaticFieldTerminalKind.Int32 ||
                    value.Int32Value != BinaryPrimitives.ReadInt32LittleEndian(read.Bytes.AsSpan()))
                {
                    throw new ArgumentException("Int32 terminal and exact slot bytes disagree.", nameof(value));
                }
                return;
            }
            case ClrmdStaticFieldValueShape.NullableInt32:
            {
                var layout = request.NullableInt32Layout!;
                var slotRead = RequireExactReadAt(
                    reads,
                    0,
                    slotAddress,
                    request.PointerWidth,
                    "Nullable storage reference slot");
                var targetAddress = ClrmdStaticPhysicalCanonical.DecodePointer(
                    slotRead.Bytes.AsSpan(),
                    request.PointerWidth);
                CanonicalReplayEncoding.ValidatePointerValue(
                    targetAddress,
                    request.PointerWidth,
                    allowZero: false,
                    nameof(reads));
                var headerRead = RequireExactReadAt(
                    reads,
                    1,
                    targetAddress,
                    request.PointerWidth,
                    "Nullable storage target header");
                var methodTable = ClrmdStaticPhysicalCanonical.DecodePointer(
                    headerRead.Bytes.AsSpan(),
                    request.PointerWidth);
                if (request.ObservedFieldType.MethodTable is not { } expectedMethodTable ||
                    methodTable != expectedMethodTable)
                {
                    throw new ArgumentException(
                        "Nullable storage must be reached through the exact raw constructed-type method table.",
                        nameof(reads));
                }
                var valueStorageAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                    targetAddress,
                    request.PointerWidth,
                    layout.StorageSize,
                    request.PointerWidth,
                    "Nullable value storage");
                var hasValueAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                    valueStorageAddress,
                    layout.HasValueOffset,
                    sizeof(byte),
                    request.PointerWidth,
                    "Nullable HasValue");
                var hasValueRead = RequireExactReadAt(reads, 2, hasValueAddress, sizeof(byte), "Nullable HasValue");
                var rawHasValue = hasValueRead.Bytes[0];
                if (rawHasValue > 1)
                {
                    throw new ArgumentException("Nullable HasValue must be canonical Boolean 0 or 1.", nameof(reads));
                }
                if (rawHasValue == 0)
                {
                    if (reads.Length != 3 || value.Kind != ClrmdStaticFieldTerminalKind.NullableInt32NoValue)
                    {
                        throw new ArgumentException("Nullable no-value terminal must stop after the exact false flag.", nameof(value));
                    }
                    return;
                }
                var valueAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                    valueStorageAddress,
                    layout.ValueOffset,
                    sizeof(int),
                    request.PointerWidth,
                    "Nullable value");
                var valueRead = RequireExactReadAt(reads, 3, valueAddress, sizeof(int), "Nullable value");
                if (reads.Length != 4 || value.Kind != ClrmdStaticFieldTerminalKind.NullableInt32Value ||
                    value.Int32Value != BinaryPrimitives.ReadInt32LittleEndian(valueRead.Bytes.AsSpan()))
                {
                    throw new ArgumentException("Nullable value terminal and exact child bytes disagree.", nameof(value));
                }
                return;
            }
            case ClrmdStaticFieldValueShape.String:
            case ClrmdStaticFieldValueShape.ObjectReference:
                ValidateExactReference(request, slotAddress, reads, value);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private static void ValidateExactReference(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldValue value)
    {
        var slotRead = RequireExactReadAt(reads, 0, slotAddress, request.PointerWidth, "reference slot");
        var targetAddress = ClrmdStaticPhysicalCanonical.DecodePointer(slotRead.Bytes.AsSpan(), request.PointerWidth);
        if (targetAddress == 0)
        {
            if (reads.Length != 1 || value.Kind != ClrmdStaticFieldTerminalKind.Null)
            {
                throw new ArgumentException("A null pointer must produce only the exact null terminal.", nameof(value));
            }
            return;
        }

        var exactObject = request.ValueShape == ClrmdStaticFieldValueShape.String
            ? value.StringValue?.ObjectReference
            : value.ObjectReference;
        var expectedKind = request.ValueShape == ClrmdStaticFieldValueShape.String
            ? ClrmdStaticFieldTerminalKind.String
            : ClrmdStaticFieldTerminalKind.ObjectReference;
        if (value.Kind != expectedKind || exactObject is null || exactObject.Snapshot != request.Snapshot ||
            exactObject.PointerWidth != request.PointerWidth || exactObject.Address != targetAddress)
        {
            throw new ArgumentException("The non-null reference terminal disagrees with the exact slot pointer.", nameof(value));
        }
        var headerRead = RequireExactReadAt(reads, 1, targetAddress, request.PointerWidth, "target header");
        if (!headerRead.Equals(exactObject.HeaderEvidence))
        {
            throw new ArgumentException("The exact terminal must reuse the retained raw header read.", nameof(reads));
        }
        if (request.ValueShape == ClrmdStaticFieldValueShape.ObjectReference)
        {
            if (reads.Length != 2)
            {
                throw new ArgumentException("An exact object reference adds only slot and raw-header reads.", nameof(reads));
            }
            return;
        }

        var exactString = value.StringValue!;
        var expectedReadCount = exactString.Value.Length == 0 ? 3 : 4;
        if (reads.Length != expectedReadCount ||
            !reads[2].Equals(exactString.LengthEvidence) ||
            exactString.CharacterEvidence is { } characters &&
                (reads.Length != 4 || !reads[3].Equals(characters)))
        {
            throw new ArgumentException("The exact string must reuse exactly slot, header, length, and optional character reads.", nameof(reads));
        }
    }

    private static void ValidateNoSlotDisposition(
        ClrmdStaticFieldEvaluationRequest request,
        ClrmdStaticStorageAcquisitionEvidence evidence,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue)
    {
        if (evidence.PointerWidth != request.PointerWidth ||
            evidence.RequestedApplicationDomainAddress != request.ApplicationDomainAddress)
        {
            throw new ArgumentException("No-slot acquisition evidence must retain the request width and domain.", nameof(evidence));
        }
        var expected = evidence.Kind switch
        {
            ClrmdStaticStorageAcquisitionKind.ApplicationDomainUnavailable =>
                (ClrmdStaticFieldObservationStatus.Unavailable, ClrmdValueIssue.FieldUnavailable, true),
            ClrmdStaticStorageAcquisitionKind.ApplicationDomainAmbiguous =>
                (ClrmdStaticFieldObservationStatus.Conflict, ClrmdValueIssue.AmbiguousMatch, true),
            ClrmdStaticStorageAcquisitionKind.SlotAddressUnavailable =>
                (ClrmdStaticFieldObservationStatus.Unavailable, ClrmdValueIssue.FieldUnavailable, true),
            ClrmdStaticStorageAcquisitionKind.RuntimeStorageUnsupported =>
                (ClrmdStaticFieldObservationStatus.Unsupported, ClrmdValueIssue.RuntimeUnsupported, true),
            ClrmdStaticStorageAcquisitionKind.InvalidSlotAddress =>
                (ClrmdStaticFieldObservationStatus.Invalid, ClrmdValueIssue.InvalidData, true),
            ClrmdStaticStorageAcquisitionKind.ApplicationDomainCatalogLimitReached =>
                (ClrmdStaticFieldObservationStatus.Partial, ClrmdValueIssue.LimitExceeded, false),
            _ => throw new ArgumentException("A no-slot observation requires a no-slot acquisition kind.", nameof(evidence)),
        };
        if (expected.Item1 != status || expected.Item2 != issue || evidence.CatalogExhaustive != expected.Item3)
        {
            throw new ArgumentException("Acquisition kind, exhaustion, status, and issue disagree.", nameof(evidence));
        }
        if (evidence.Kind == ClrmdStaticStorageAcquisitionKind.InvalidSlotAddress)
        {
            if (evidence.ReturnedSlotAddress is not { } invalidSlot ||
                evidence.AttemptedStorageSize != request.StorageSize)
            {
                throw new ArgumentException(
                    "Invalid-slot evidence must retain the exact request storage width and raw returned slot.",
                    nameof(evidence));
            }
            var maximum = request.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
            if ((ulong)(request.StorageSize - 1) <= maximum - invalidSlot)
            {
                throw new ArgumentException(
                    "The raw returned slot does not overflow this request's exact storage range.",
                    nameof(evidence));
            }
        }
        else if (evidence.AttemptedStorageSize.HasValue)
        {
            throw new ArgumentException("A no-slot non-invalid stop cannot retain a storage width.", nameof(evidence));
        }
    }

    private static void ValidatePostSlotFailure(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue,
        ClrmdStaticTargetEvidence? targetEvidence,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        _ = reachedBounds;
        switch (request.ValueShape)
        {
            case ClrmdStaticFieldValueShape.Int32:
                if (targetEvidence is not null || reads.Length != 1)
                {
                    throw new ArgumentException("An Int32 stop retains only its first slot read.", nameof(reads));
                }
                ValidateIncompleteRead(
                    RequireReadAt(reads, 0, slotAddress, sizeof(int), "Int32 slot"),
                    status,
                    issue);
                return;
            case ClrmdStaticFieldValueShape.NullableInt32:
                if (targetEvidence is not null)
                {
                    throw new ArgumentException("Nullable value decoding never carries object-target evidence.");
                }
                ValidateNullableFailure(request, slotAddress, reads, status, issue);
                return;
            case ClrmdStaticFieldValueShape.String:
            case ClrmdStaticFieldValueShape.ObjectReference:
                ValidateReferenceFailure(
                    request,
                    slotAddress,
                    reads,
                    status,
                    issue,
                    targetEvidence);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private static void ValidateNullableFailure(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue)
    {
        var layout = request.NullableInt32Layout!;
        var slot = RequireReadAt(
            reads,
            0,
            slotAddress,
            request.PointerWidth,
            "Nullable storage reference slot");
        if (!slot.IsExact)
        {
            if (reads.Length != 1)
            {
                throw new ArgumentException(
                    "An incomplete Nullable storage slot is the first and final attempted read.",
                    nameof(reads));
            }
            ValidateIncompleteRead(slot, status, issue);
            return;
        }

        var targetAddress = ClrmdStaticPhysicalCanonical.DecodePointer(slot.Bytes.AsSpan(), request.PointerWidth);
        if (targetAddress == 0)
        {
            if (reads.Length != 1 ||
                status != ClrmdStaticFieldObservationStatus.Invalid ||
                issue != ClrmdValueIssue.InvalidData)
            {
                throw new ArgumentException("An initialized Nullable storage reference cannot be null.", nameof(reads));
            }
            return;
        }

        var maximum = request.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((ulong)(request.PointerWidth - 1) > maximum - targetAddress)
        {
            if (reads.Length != 1 ||
                status != ClrmdStaticFieldObservationStatus.Invalid ||
                issue != ClrmdValueIssue.InvalidData)
            {
                throw new ArgumentException("An unaddressable Nullable target header must stop after the slot.");
            }
            return;
        }

        var header = RequireReadAt(
            reads,
            1,
            targetAddress,
            request.PointerWidth,
            "Nullable storage target header");
        if (!header.IsExact)
        {
            if (reads.Length != 2)
            {
                throw new ArgumentException(
                    "An incomplete Nullable target header is the final attempted read.",
                    nameof(reads));
            }
            ValidateIncompleteRead(header, status, issue);
            return;
        }

        var methodTable = ClrmdStaticPhysicalCanonical.DecodePointer(header.Bytes.AsSpan(), request.PointerWidth);
        if (request.ObservedFieldType.MethodTable is not { } expectedMethodTable || methodTable != expectedMethodTable)
        {
            if (reads.Length != 2 ||
                status != ClrmdStaticFieldObservationStatus.Conflict ||
                issue != ClrmdValueIssue.TypeMismatch)
            {
                throw new ArgumentException(
                    "A conflicting Nullable target method table must stop after the exact header.",
                    nameof(reads));
            }
            return;
        }

        ulong valueStorageAddress;
        try
        {
            valueStorageAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                targetAddress,
                request.PointerWidth,
                layout.StorageSize,
                request.PointerWidth,
                "Nullable value storage");
        }
        catch (ArgumentOutOfRangeException)
        {
            if (reads.Length != 2 ||
                status != ClrmdStaticFieldObservationStatus.Invalid ||
                issue != ClrmdValueIssue.InvalidData)
            {
                throw new ArgumentException("Unaddressable Nullable value storage must stop after the exact header.");
            }
            return;
        }

        var hasValueAddress = ClrmdStaticPhysicalCanonical.AddOffset(
            valueStorageAddress,
            layout.HasValueOffset,
            sizeof(byte),
            request.PointerWidth,
            "Nullable HasValue");
        var hasValue = RequireReadAt(reads, 2, hasValueAddress, sizeof(byte), "Nullable HasValue");
        if (!hasValue.IsExact)
        {
            if (reads.Length != 3)
            {
                throw new ArgumentException("An incomplete Nullable flag is the first and final attempted read.", nameof(reads));
            }
            ValidateIncompleteRead(hasValue, status, issue);
            return;
        }

        var rawHasValue = hasValue.Bytes[0];
        if (rawHasValue > 1)
        {
            if (reads.Length != 3 ||
                status != ClrmdStaticFieldObservationStatus.Invalid ||
                issue != ClrmdValueIssue.InvalidData)
            {
                throw new ArgumentException("A noncanonical Nullable Boolean is the first invalid stage.", nameof(reads));
            }
            return;
        }
        if (rawHasValue == 0)
        {
            throw new ArgumentException("An exact false Nullable flag is decoder-complete and cannot be non-exact.");
        }

        var valueAddress = ClrmdStaticPhysicalCanonical.AddOffset(
            valueStorageAddress,
            layout.ValueOffset,
            sizeof(int),
            request.PointerWidth,
            "Nullable value");
        var value = RequireReadAt(reads, 3, valueAddress, sizeof(int), "Nullable value");
        if (value.IsExact)
        {
            throw new ArgumentException("Exact true Nullable flag and exact value are decoder-complete.");
        }
        if (reads.Length != 4)
        {
            throw new ArgumentException("An incomplete Nullable value must be the final attempted read.", nameof(reads));
        }
        ValidateIncompleteRead(value, status, issue);
    }

    private static void ValidateReferenceFailure(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue,
        ClrmdStaticTargetEvidence? targetEvidence)
    {
        var slotRead = RequireReadAt(reads, 0, slotAddress, request.PointerWidth, "reference slot");
        if (!slotRead.IsExact)
        {
            if (reads.Length != 1 || targetEvidence is not null)
            {
                throw new ArgumentException("An incomplete reference slot is the first and final decoder stage.", nameof(reads));
            }
            ValidateIncompleteRead(slotRead, status, issue);
            return;
        }

        var targetAddress = ClrmdStaticPhysicalCanonical.DecodePointer(slotRead.Bytes.AsSpan(), request.PointerWidth);
        if (targetAddress == 0)
        {
            throw new ArgumentException("An exact null slot is decoder-complete and cannot be non-exact.");
        }
        ArgumentNullException.ThrowIfNull(targetEvidence);
        if (targetEvidence.Snapshot != request.Snapshot ||
            targetEvidence.PointerWidth != request.PointerWidth ||
            targetEvidence.TargetAddress != targetAddress)
        {
            throw new ArgumentException("Target evidence must describe the exact nonzero slot pointer.", nameof(targetEvidence));
        }

        switch (targetEvidence.Kind)
        {
            case ClrmdStaticTargetEvidenceKind.InvalidStructure:
                var validInvalidStructure = targetEvidence.StructureIssue switch
                {
                    ClrmdStaticTargetStructureIssue.HeaderAddressOverflow =>
                        reads.Length == 1 && targetEvidence.HeaderEvidence is null,
                    ClrmdStaticTargetStructureIssue.NullMethodTable =>
                        reads.Length == 2 &&
                        targetEvidence.HeaderEvidence is { } invalidHeader &&
                        invalidHeader.Equals(reads[1]) &&
                        reads[1].IsExact,
                    _ => false,
                };
                if (!validInvalidStructure ||
                    status != ClrmdStaticFieldObservationStatus.Invalid ||
                    issue != ClrmdValueIssue.InvalidData)
                {
                    throw new ArgumentException(
                        "Invalid target structure must stop at its exact overflow or null-method-table prefix.");
                }
                return;
            case ClrmdStaticTargetEvidenceKind.HeaderUnavailable:
            {
                var header = RequireReadAt(reads, 1, targetAddress, request.PointerWidth, "target header");
                if (!header.Equals(targetEvidence.HeaderEvidence) || reads.Length != 2)
                {
                    throw new ArgumentException("An incomplete header must be the second and final read.", nameof(reads));
                }
                ValidateIncompleteRead(header, status, issue);
                return;
            }
            case ClrmdStaticTargetEvidenceKind.RuntimeTypeUnavailable:
            {
                var header = RequireExactReadAt(reads, 1, targetAddress, request.PointerWidth, "target header");
                if (!header.Equals(targetEvidence.HeaderEvidence) || reads.Length != 2 ||
                    status != ClrmdStaticFieldObservationStatus.Unavailable ||
                    issue != ClrmdValueIssue.TypeUnavailable)
                {
                    throw new ArgumentException("A missing runtime type stops immediately after the exact raw header.");
                }
                return;
            }
            case ClrmdStaticTargetEvidenceKind.RuntimeTypeConflict:
            {
                var header = RequireExactReadAt(reads, 1, targetAddress, request.PointerWidth, "target header");
                if (!header.Equals(targetEvidence.HeaderEvidence) || reads.Length != 2 ||
                    status != ClrmdStaticFieldObservationStatus.Conflict ||
                    issue != ClrmdValueIssue.TypeMismatch)
                {
                    throw new ArgumentException("A conflicting runtime type stops immediately after the exact raw header.");
                }
                return;
            }
            case ClrmdStaticTargetEvidenceKind.Matched:
            {
                var header = RequireExactReadAt(reads, 1, targetAddress, request.PointerWidth, "target header");
                if (!header.Equals(targetEvidence.HeaderEvidence))
                {
                    throw new ArgumentException("Matched target evidence must reuse the second raw header read.", nameof(reads));
                }
                if (request.ValueShape == ClrmdStaticFieldValueShape.ObjectReference)
                {
                    throw new ArgumentException("A matched object target is physically decoder-complete and must be exact.");
                }
                ValidateNonExactString(request, reads, status, issue, targetEvidence);
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(targetEvidence));
        }
    }

    private static void ValidateNonExactString(
        ClrmdStaticFieldEvaluationRequest request,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue,
        ClrmdStaticTargetEvidence targetEvidence)
    {
        ulong lengthAddress;
        try
        {
            lengthAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                targetEvidence.TargetAddress,
                request.PointerWidth,
                sizeof(int),
                request.PointerWidth,
                "string length");
        }
        catch (ArgumentOutOfRangeException)
        {
            if (reads.Length != 2 ||
                status != ClrmdStaticFieldObservationStatus.Invalid ||
                issue != ClrmdValueIssue.InvalidData)
            {
                throw new ArgumentException("An unaddressable string length stops after the matched header.");
            }
            return;
        }

        var lengthRead = RequireReadAt(reads, 2, lengthAddress, sizeof(int), "string length");
        if (!lengthRead.IsExact)
        {
            if (reads.Length != 3)
            {
                throw new ArgumentException("An incomplete string length is the final attempted read.", nameof(reads));
            }
            ValidateIncompleteRead(lengthRead, status, issue);
            return;
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(lengthRead.Bytes.AsSpan());
        if (count < 0)
        {
            if (reads.Length != 3 ||
                status != ClrmdStaticFieldObservationStatus.Invalid ||
                issue != ClrmdValueIssue.InvalidData)
            {
                throw new ArgumentException("A negative string length is the first invalid stage.");
            }
            return;
        }
        if (count > ClrmdExactStringValue.MaximumCharacters)
        {
            if (reads.Length != 3 ||
                status != ClrmdStaticFieldObservationStatus.Partial ||
                issue != ClrmdValueIssue.LimitExceeded)
            {
                throw new ArgumentException("An over-cap string stops before a character read.");
            }
            return;
        }
        if (count == 0)
        {
            throw new ArgumentException("Matched header plus exact zero length is decoder-complete and must be exact.");
        }

        var byteCount = checked(count * sizeof(char));
        ulong characterAddress;
        try
        {
            characterAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                targetEvidence.TargetAddress,
                request.PointerWidth + sizeof(int),
                byteCount,
                request.PointerWidth,
                "string characters");
        }
        catch (ArgumentOutOfRangeException)
        {
            if (reads.Length != 3 ||
                status != ClrmdStaticFieldObservationStatus.Invalid ||
                issue != ClrmdValueIssue.InvalidData)
            {
                throw new ArgumentException("An unaddressable string character range stops after the exact length.");
            }
            return;
        }

        var characterRead = RequireReadAt(reads, 3, characterAddress, byteCount, "string characters");
        if (characterRead.IsExact)
        {
            throw new ArgumentException("Exact in-cap string characters are decoder-complete.");
        }
        if (reads.Length != 4)
        {
            throw new ArgumentException("An incomplete character range is the final attempted read.", nameof(reads));
        }
        ValidateIncompleteRead(characterRead, status, issue);
    }

    private static void ValidateIncompleteRead(
        ClrmdRawMemoryEvidence read,
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue)
    {
        if (read.IsExact ||
            status != (read.Status == ClrmdRawMemoryStatus.Partial
                ? ClrmdStaticFieldObservationStatus.Partial
                : ClrmdStaticFieldObservationStatus.Unavailable) ||
            issue != ClrmdValueIssue.MemoryUnavailable)
        {
            throw new ArgumentException("The first incomplete raw read, observation status, and issue disagree.");
        }
    }

    private static bool RequiresStringCap(
        ClrmdStaticFieldEvaluationRequest request,
        ulong? slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads)
    {
        if (request.ValueShape != ClrmdStaticFieldValueShape.String ||
            !slotAddress.HasValue || reads.Length < 3 ||
            reads[0].Address != slotAddress.Value ||
            reads[0].RequestedLength != request.PointerWidth ||
            !reads[0].IsExact)
        {
            return false;
        }
        var targetAddress = ClrmdStaticPhysicalCanonical.DecodePointer(reads[0].Bytes.AsSpan(), request.PointerWidth);
        if (targetAddress == 0)
        {
            return false;
        }
        try
        {
            var lengthAddress = ClrmdStaticPhysicalCanonical.AddOffset(
                targetAddress, request.PointerWidth, sizeof(int), request.PointerWidth, "string length");
            var read = reads[2];
            if (read.Address != lengthAddress || read.RequestedLength != sizeof(int))
            {
                return false;
            }
            return read is { IsExact: true } && BinaryPrimitives.ReadInt32LittleEndian(read.Bytes.AsSpan()) >= 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ImmutableArray<ClrmdRawMemoryEvidence> NormalizeReads(
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdSnapshotIdentity snapshot)
    {
        if (reads.IsDefault || reads.Length > MaximumRawReadCount || reads.Any(static read => read is null) ||
            reads.Any(read => read.Snapshot != snapshot))
        {
            throw new ArgumentException("An initialized bounded same-snapshot raw read set is required.", nameof(reads));
        }
        return ImmutableArray.CreateRange(reads);
    }

    private static ImmutableArray<EvaluationDeterministicBound> ValidateBounds(
        ImmutableArray<EvaluationDeterministicBound> bounds,
        bool acquisitionRequired,
        bool readsRequired,
        bool stringCapRequired)
    {
        var normalized = CanonicalReplayEncoding.NormalizeBounds(bounds, nameof(bounds));
        ValidateBound(normalized,
            ClrmdStaticStorageAcquisitionEvidence.MaximumApplicationDomainCountBoundName,
            ClrmdStaticStorageAcquisitionEvidence.MaximumApplicationDomains,
            acquisitionRequired);
        ValidateBound(normalized, MaximumRawReadCountBoundName, MaximumRawReadCount, readsRequired);
        ValidateBound(normalized, ClrmdExactStringValue.CharacterLimitBoundName,
            ClrmdExactStringValue.MaximumCharacters, stringCapRequired);
        var expectedCount = (acquisitionRequired ? 1 : 0) +
            (readsRequired ? 1 : 0) +
            (stringCapRequired ? 1 : 0);
        if (normalized.Length != expectedCount)
        {
            throw new ArgumentException(
                "Reached bounds must be exactly the fixed operation-bound set for this decoder prefix.",
                nameof(bounds));
        }
        return normalized;
    }

    private static void ValidateBound(
        ImmutableArray<EvaluationDeterministicBound> bounds,
        string name,
        long value,
        bool required)
    {
        var bound = bounds.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
        if (required ? bound is null || bound.Value != value : bound is not null)
        {
            throw new ArgumentException(required
                ? $"The operation must retain the exact reached bound '{name}'."
                : $"The operation cannot retain unused bound '{name}'.", nameof(bounds));
        }
    }

    private static ClrmdRawMemoryEvidence RequireReadAt(
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        int index,
        ulong address,
        int length,
        string stage)
    {
        if ((uint)index >= (uint)reads.Length ||
            reads[index].Address != address ||
            reads[index].RequestedLength != length)
        {
            throw new ArgumentException($"Decoder stage {index} must be the exact {stage} range.", nameof(reads));
        }
        return reads[index];
    }

    private static ClrmdRawMemoryEvidence RequireExactReadAt(
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        int index,
        ulong address,
        int length,
        string stage)
    {
        var read = RequireReadAt(reads, index, address, length, stage);
        if (!read.IsExact)
        {
            throw new ArgumentException($"Decoder stage {index} requires one complete {stage} read.", nameof(reads));
        }
        return read;
    }
}

internal static class ClrmdStaticPhysicalCanonical
{
    internal static void WriteSnapshot(CanonicalReplayEncoding.Writer writer, ClrmdSnapshotIdentity snapshot) =>
        writer.WriteSha256(snapshot.Sha256, nameof(snapshot));

    internal static void WriteRuntimeModule(
        CanonicalReplayEncoding.Writer writer,
        ClrmdRuntimeModuleIdentity module)
    {
        WriteSnapshot(writer, module.Snapshot);
        writer.WriteUInt64(module.AppDomainAddress);
        writer.WriteUInt64(module.ModuleAddress);
        writer.WriteUInt64(module.ImageBase);
        writer.WriteUInt64(module.ImageSize);
    }

    internal static void WriteModuleContent(
        CanonicalReplayEncoding.Writer writer,
        ModuleContentIdentity module)
    {
        writer.WriteRawBytes(module.Mvid.ToByteArray());
        writer.WriteInt32(module.MetadataLength);
        writer.WriteSha256(module.MetadataSha256, nameof(module));
    }

    internal static void WriteBounds(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        writer.WriteInt32(bounds.Length);
        foreach (var bound in bounds)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }

    internal static void WriteOptionalCanonical(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<byte>? bytes)
    {
        writer.WriteBoolean(bytes.HasValue);
        if (bytes.HasValue)
        {
            writer.WriteLengthPrefixedBytes(bytes.Value.AsSpan());
        }
    }

    internal static void ValidateSnapshot(ClrmdSnapshotIdentity snapshot, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Sha256))
        {
            throw new ArgumentException("A complete immutable dump snapshot identity is required.", parameterName);
        }
    }

    internal static void ValidateRuntimeModule(
        ClrmdRuntimeModuleIdentity module,
        int pointerWidth,
        string parameterName)
    {
        ValidateSnapshot(module.Snapshot, parameterName);
        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        CanonicalReplayEncoding.ValidatePointerValue(module.AppDomainAddress, pointerWidth, allowZero: false, parameterName);
        CanonicalReplayEncoding.ValidatePointerValue(module.ModuleAddress, pointerWidth, allowZero: false, parameterName);
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if (module.ImageBase > maximum || module.ImageSize > maximum ||
            module.ImageBase != 0 && module.ImageSize != 0 && module.ImageSize - 1 > maximum - module.ImageBase)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Runtime module coordinates exceed the target address space.");
        }
    }

    internal static void ValidateDecodedName(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"A non-empty decoded ordinal name of at most {ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters} characters is required.",
                parameterName);
        }
    }

    internal static ulong DecodePointer(ReadOnlySpan<byte> bytes, int pointerWidth) => pointerWidth switch
    {
        sizeof(uint) when bytes.Length == sizeof(uint) => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        sizeof(ulong) when bytes.Length == sizeof(ulong) => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
        _ => throw new ArgumentException("Complete bytes matching the target pointer width are required.", nameof(bytes)),
    };

    internal static ulong AddOffset(
        ulong address,
        int offset,
        int length,
        int pointerWidth,
        string stage)
    {
        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        if (offset < 0 || length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if (address > maximum || (ulong)offset > maximum - address)
        {
            throw new ArgumentOutOfRangeException(nameof(address), $"The {stage} address overflows the target address space.");
        }
        var result = address + (ulong)offset;
        if ((ulong)(length - 1) > maximum - result)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"The {stage} range overflows the target address space.");
        }
        return result;
    }
}

// Runtime mapping is a separate physical contract file retained from the same Host assembly. This small forwarding
// surface prevents either file from acquiring metadata semantics while sharing one canonical coordinate/name policy.
internal static class ClrmdStaticCanonical
{
    internal static void WriteSnapshot(CanonicalReplayEncoding.Writer writer, ClrmdSnapshotIdentity snapshot) =>
        ClrmdStaticPhysicalCanonical.WriteSnapshot(writer, snapshot);

    internal static void WriteRuntimeModule(
        CanonicalReplayEncoding.Writer writer,
        ClrmdRuntimeModuleIdentity module) =>
        ClrmdStaticPhysicalCanonical.WriteRuntimeModule(writer, module);

    internal static void WriteModuleContent(
        CanonicalReplayEncoding.Writer writer,
        ModuleContentIdentity module) =>
        ClrmdStaticPhysicalCanonical.WriteModuleContent(writer, module);

    internal static void WriteBounds(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<EvaluationDeterministicBound> bounds) =>
        ClrmdStaticPhysicalCanonical.WriteBounds(writer, bounds);

    internal static void ValidateRuntimeModule(
        ClrmdRuntimeModuleIdentity module,
        int pointerWidth,
        string parameterName) =>
        ClrmdStaticPhysicalCanonical.ValidateRuntimeModule(module, pointerWidth, parameterName);

    internal static void ValidateDecodedName(string value, string parameterName) =>
        ClrmdStaticPhysicalCanonical.ValidateDecodedName(value, parameterName);
}
