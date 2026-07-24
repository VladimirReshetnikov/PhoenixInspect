using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Freezes one raw runtime instance-field row used to describe specialized nullable value storage.</summary>
public sealed class ClrmdStaticNullableRuntimeFieldIdentity :
    IEquatable<ClrmdStaticNullableRuntimeFieldIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticNullableRuntimeFieldIdentity(
        ClrmdStaticRuntimeTypeIdentity declaringRuntimeType,
        int fieldDefinitionToken,
        string name,
        int offset,
        int size,
        ClrmdStaticRuntimeTypeIdentity observedType)
    {
        DeclaringRuntimeType = declaringRuntimeType;
        FieldDefinitionToken = fieldDefinitionToken;
        Name = name;
        Offset = offset;
        Size = size;
        ObservedType = observedType;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-nullable-runtime-field-identity", 1);
        writer.WriteLengthPrefixedBytes(declaringRuntimeType.CanonicalBytes.AsSpan());
        writer.WriteInt32(fieldDefinitionToken);
        writer.WriteString(name);
        writer.WriteInt32(offset);
        writer.WriteInt32(size);
        writer.WriteLengthPrefixedBytes(observedType.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact constructed runtime value type whose complete field catalog supplied this row.</summary>
    public ClrmdStaticRuntimeTypeIdentity DeclaringRuntimeType { get; }

    /// <summary>Gets the non-nil runtime-reported FieldDef token.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the exact bounded ordinal runtime field name without assigning a semantic nullable role.</summary>
    public string Name { get; }

    /// <summary>Gets the nonnegative offset relative to the boxed value payload after its method-table header.</summary>
    public int Offset { get; }

    /// <summary>Gets the positive runtime-reported child storage width.</summary>
    public int Size { get; }

    /// <summary>Gets the detached exact runtime type reported for this child field.</summary>
    public ClrmdStaticRuntimeTypeIdentity ObservedType { get; }

    /// <summary>Gets a defensive copy of the versioned canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    internal static ClrmdStaticNullableRuntimeFieldIdentity Create(
        ClrmdStaticRuntimeTypeIdentity declaringRuntimeType,
        int fieldDefinitionToken,
        string name,
        int offset,
        int size,
        ClrmdStaticRuntimeTypeIdentity observedType)
    {
        ArgumentNullException.ThrowIfNull(declaringRuntimeType);
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        ClrmdStaticRuntimeMappingCanonical.ValidateDecodedName(name, nameof(name));
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentNullException.ThrowIfNull(observedType);
        if (declaringRuntimeType.Snapshot != observedType.Snapshot ||
            declaringRuntimeType.PointerWidth != observedType.PointerWidth)
        {
            throw new ArgumentException("Nullable child and owner runtime types must share one snapshot and architecture.");
        }
        return new ClrmdStaticNullableRuntimeFieldIdentity(
            declaringRuntimeType,
            fieldDefinitionToken,
            name,
            offset,
            size,
            observedType);
    }

    /// <inheritdoc />
    public bool Equals(ClrmdStaticNullableRuntimeFieldIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticNullableRuntimeFieldIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>
/// Retains the complete bounded raw field catalog and payload extent of one constructed nullable runtime type.
/// </summary>
/// <remarks>
/// Host assigns no semantic HasValue or value role. Product must correlate these raw rows with the exact resolved
/// System.Nullable`1 metadata FieldDefs and core-library type anchors before it creates a physical decoder request.
/// </remarks>
public sealed class ClrmdStaticNullableRuntimeLayoutIdentity :
    IEquatable<ClrmdStaticNullableRuntimeLayoutIdentity>
{
    /// <summary>Gets the maximum complete runtime instance-field catalog admitted for nullable layout proof.</summary>
    public const int MaximumRuntimeFields = 64;

    /// <summary>Gets the deterministic-bound name for the nullable runtime field catalog.</summary>
    public const string MaximumRuntimeFieldCountBoundName = "static-field.nullable-layout.runtime-fields";

    private readonly ImmutableArray<ClrmdStaticNullableRuntimeFieldIdentity> fields;
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticNullableRuntimeLayoutIdentity(
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping,
        int storageSize,
        ImmutableArray<ClrmdStaticNullableRuntimeFieldIdentity> fields)
    {
        RuntimeMapping = runtimeMapping;
        StorageSize = storageSize;
        this.fields = fields;
        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-nullable-runtime-layout-identity", 1);
        writer.WriteLengthPrefixedBytes(runtimeMapping.CanonicalBytes.AsSpan());
        writer.WriteString(MaximumRuntimeFieldCountBoundName);
        writer.WriteInt32(MaximumRuntimeFields);
        writer.WriteInt32(storageSize);
        writer.WriteInt32(fields.Length);
        foreach (var field in fields)
        {
            writer.WriteLengthPrefixedBytes(field.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact outer static-field runtime declaration mapping.</summary>
    public ClrmdStaticRuntimeDeclarationMappingIdentity RuntimeMapping { get; }

    /// <summary>Gets the complete specialized value payload extent after the boxed method-table header.</summary>
    public int StorageSize { get; }

    /// <summary>Gets a defensive copy of every runtime field in its exact enumeration order.</summary>
    public ImmutableArray<ClrmdStaticNullableRuntimeFieldIdentity> Fields =>
        CanonicalReplayEncoding.Copy(fields);

    /// <summary>Gets the exact exhausted runtime field catalog cardinality.</summary>
    public int RuntimeFieldCount => fields.Length;

    /// <summary>Gets the fixed runtime field catalog bound.</summary>
    public static EvaluationDeterministicBound DeclaredRuntimeFieldCountBound =>
        new(MaximumRuntimeFieldCountBoundName, MaximumRuntimeFields);

    /// <summary>Gets a defensive copy of the versioned canonical layout bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    internal static ClrmdStaticNullableRuntimeLayoutIdentity Create(
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping,
        int storageSize,
        ImmutableArray<ClrmdStaticNullableRuntimeFieldIdentity> fields)
    {
        ArgumentNullException.ThrowIfNull(runtimeMapping);
        if (runtimeMapping.Field.ExpectedDecoderKind != ClrmdStaticExpectedDecoderKind.NullableInt32 ||
            !runtimeMapping.Field.ObservedFieldType.IsValueType ||
            runtimeMapping.Field.ObservedFieldType.IsPrimitive)
        {
            throw new ArgumentException("A constructed nullable runtime mapping is required.", nameof(runtimeMapping));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(storageSize);
        if (fields.IsDefault || fields.IsEmpty || fields.Length > MaximumRuntimeFields)
        {
            throw new ArgumentException(
                $"An initialized complete catalog of one through {MaximumRuntimeFields} fields is required.",
                nameof(fields));
        }

        var tokens = new HashSet<int>();
        foreach (var field in fields)
        {
            if (field is null ||
                !field.DeclaringRuntimeType.Equals(runtimeMapping.Field.ObservedFieldType) ||
                field.Offset > storageSize || field.Size > storageSize - field.Offset ||
                !tokens.Add(field.FieldDefinitionToken))
            {
                throw new ArgumentException(
                    "Every nullable runtime field must be distinct, directly owned, and completely inside the payload extent.",
                    nameof(fields));
            }
        }

        return new ClrmdStaticNullableRuntimeLayoutIdentity(
            runtimeMapping,
            storageSize,
            ImmutableArray.CreateRange(fields));
    }

    /// <inheritdoc />
    public bool Equals(ClrmdStaticNullableRuntimeLayoutIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticNullableRuntimeLayoutIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}
