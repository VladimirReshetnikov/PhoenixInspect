using System.Collections.Immutable;
using System.Reflection;
using Interpreter.Core.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Freezes one detached ClrMD runtime-type projection used to correlate counted metadata with runtime static fields.
/// </summary>
/// <remarks>
/// This draft W7 identity records physical runtime coordinates and exact metadata content without retaining a live
/// ClrMD object. The optional method table is evidence reported by the runtime adapter; absence does not invent a
/// method table or weaken the module, TypeDef, and name identity that remain mandatory.
/// </remarks>
public sealed class ClrmdStaticRuntimeTypeIdentity : IEquatable<ClrmdStaticRuntimeTypeIdentity>
{
    /// <summary>Gets the maximum exact ClrMD runtime type or field name length copied into mapping evidence.</summary>
    public const int MaximumRuntimeNameCharacters = 2_048;

    /// <summary>
    /// Gets the maximum exact, non-nested constructed generic arguments retained by this closed W7 projection.
    /// </summary>
    public const int MaximumGenericArgumentCount = 1;

    /// <summary>Gets the canonical bound name shared by runtime type and field names.</summary>
    public const string RuntimeNameCharacterBoundName = "static-field.runtime-mapping.name-characters";

    /// <summary>Gets the canonical bound name for exact constructed generic arguments.</summary>
    public const string GenericArgumentCountBoundName = "static-field.runtime-mapping.generic-arguments";

    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly ImmutableArray<ClrmdStaticRuntimeTypeIdentity> genericArguments;

    private ClrmdStaticRuntimeTypeIdentity(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        int typeDefinitionToken,
        string fullName,
        ulong? methodTable,
        bool isValueType,
        bool isPrimitive,
        bool isArray,
        bool isInterface,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity> genericArguments)
    {
        Snapshot = snapshot;
        PointerWidth = pointerWidth;
        RuntimeModule = runtimeModule;
        ModuleContent = moduleContent;
        TypeDefinitionToken = typeDefinitionToken;
        FullName = fullName;
        MethodTable = methodTable;
        IsValueType = isValueType;
        IsPrimitive = isPrimitive;
        IsArray = isArray;
        IsInterface = isInterface;
        this.genericArguments = genericArguments;

        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-runtime-type-identity", 2);
        ClrmdStaticRuntimeMappingCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteInt32(pointerWidth);
        ClrmdStaticRuntimeMappingCanonical.WriteRuntimeModule(writer, runtimeModule);
        ClrmdStaticRuntimeMappingCanonical.WriteModuleContent(writer, moduleContent);
        writer.WriteInt32(typeDefinitionToken);
        writer.WriteString(RuntimeNameCharacterBoundName);
        writer.WriteInt32(MaximumRuntimeNameCharacters);
        writer.WriteString(fullName);
        writer.WriteBoolean(methodTable.HasValue);
        if (methodTable.HasValue)
        {
            writer.WriteUInt64(methodTable.Value);
        }
        writer.WriteBoolean(isValueType);
        writer.WriteBoolean(isPrimitive);
        writer.WriteBoolean(isArray);
        writer.WriteBoolean(isInterface);
        writer.WriteString(GenericArgumentCountBoundName);
        writer.WriteInt32(MaximumGenericArgumentCount);
        writer.WriteInt32(genericArguments.Length);
        foreach (var genericArgument in genericArguments)
        {
            writer.WriteLengthPrefixedBytes(genericArgument.CanonicalBytes.AsSpan());
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot containing the runtime type.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the exact target pointer width, in bytes.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the physical runtime module that supplied the type.</summary>
    public ClrmdRuntimeModuleIdentity RuntimeModule { get; }

    /// <summary>Gets the complete counted metadata-content identity containing the TypeDef.</summary>
    public ModuleContentIdentity ModuleContent { get; }

    /// <summary>Gets the non-nil TypeDef token correlated with the runtime type.</summary>
    public int TypeDefinitionToken { get; }

    /// <summary>Gets the exact ordinal full name reported for the runtime type.</summary>
    public string FullName { get; }

    /// <summary>Gets the nonzero method table when ClrMD supplied one, or <see langword="null"/> when unavailable.</summary>
    public ulong? MethodTable { get; }

    /// <summary>Gets whether ClrMD classified the runtime type as a value type.</summary>
    public bool IsValueType { get; }

    /// <summary>Gets whether ClrMD classified the runtime type as a primitive.</summary>
    public bool IsPrimitive { get; }

    /// <summary>Gets whether ClrMD classified the runtime type as an array.</summary>
    public bool IsArray { get; }

    /// <summary>Gets whether ClrMD classified the runtime type as an interface.</summary>
    public bool IsInterface { get; }

    /// <summary>Gets a defensive copy of the exact bounded constructed generic runtime type arguments.</summary>
    public ImmutableArray<ClrmdStaticRuntimeTypeIdentity> GenericArguments =>
        CanonicalReplayEncoding.Copy(genericArguments);

    /// <summary>Gets the fixed canonical runtime name-character bound applied before copying names.</summary>
    public static EvaluationDeterministicBound DeclaredRuntimeNameCharacterBound =>
        new(RuntimeNameCharacterBoundName, MaximumRuntimeNameCharacters);

    /// <summary>Gets the fixed canonical constructed-generic-argument bound applied before canonical encoding.</summary>
    public static EvaluationDeterministicBound DeclaredGenericArgumentCountBound =>
        new(GenericArgumentCountBoundName, MaximumGenericArgumentCount);

    /// <summary>Gets a defensive copy of the versioned canonical runtime-type bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one immutable runtime-type identity from detached ClrMD and counted-metadata facts.</summary>
    /// <param name="snapshot">The immutable dump identity in which the runtime type was observed.</param>
    /// <param name="pointerWidth">The target pointer width; exactly four or eight bytes.</param>
    /// <param name="runtimeModule">The snapshot-scoped physical runtime module containing the type.</param>
    /// <param name="moduleContent">The complete counted metadata identity for that module.</param>
    /// <param name="typeDefToken">The non-nil TypeDef token correlated with the runtime type.</param>
    /// <param name="fullName">The exact non-empty ordinal full runtime type name.</param>
    /// <param name="methodTable">An optional nonzero method table that fits the target pointer width.</param>
    /// <param name="isValueType">Exact ClrMD value-type classification.</param>
    /// <param name="isPrimitive">Exact ClrMD primitive classification.</param>
    /// <param name="isArray">Exact ClrMD array classification.</param>
    /// <param name="isInterface">Exact ClrMD interface classification.</param>
    /// <param name="genericArguments">
    /// Initialized exact constructed generic arguments; this closed projection admits at most one.
    /// </param>
    /// <returns>A detached, immutable, content-equal runtime-type identity suitable for canonical replay.</returns>
    /// <exception cref="ArgumentException">
    /// Snapshot, module, metadata content, or exact name evidence is absent or mutually inconsistent.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The pointer width, TypeDef token, runtime coordinate, or optional method table is invalid.
    /// </exception>
    public static ClrmdStaticRuntimeTypeIdentity Create(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        int typeDefToken,
        string fullName,
        ulong? methodTable,
        bool isValueType,
        bool isPrimitive,
        bool isArray,
        bool isInterface,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity> genericArguments)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Sha256))
        {
            throw new ArgumentException("A complete dump snapshot identity is required.", nameof(snapshot));
        }

        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        ClrmdStaticRuntimeMappingCanonical.ValidateRuntimeModule(runtimeModule, pointerWidth, nameof(runtimeModule));
        if (runtimeModule.Snapshot != snapshot)
        {
            throw new ArgumentException("The runtime module belongs to a different dump snapshot.", nameof(runtimeModule));
        }

        ArgumentNullException.ThrowIfNull(moduleContent);
        CanonicalReplayEncoding.ValidateMetadataToken(typeDefToken, 0x02, nameof(typeDefToken));
        ArgumentNullException.ThrowIfNull(fullName);
        if (fullName.Length > MaximumRuntimeNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fullName),
                $"A runtime type name cannot exceed {MaximumRuntimeNameCharacters} characters.");
        }
        ClrmdStaticRuntimeMappingCanonical.ValidateDecodedName(fullName, nameof(fullName));
        if (methodTable.HasValue)
        {
            CanonicalReplayEncoding.ValidatePointerValue(
                methodTable.Value,
                pointerWidth,
                allowZero: false,
                nameof(methodTable));
        }

        if (isPrimitive && (!isValueType || isArray || isInterface) ||
            isArray && (isValueType || isPrimitive || isInterface) ||
            isInterface && (isValueType || isPrimitive || isArray))
        {
            throw new ArgumentException(
                "ClrMD primitive, array, interface, and value-type classifications are internally contradictory.");
        }

        if (genericArguments.IsDefault || genericArguments.Length > MaximumGenericArgumentCount)
        {
            throw new ArgumentException(
                $"An initialized generic-argument array of at most {MaximumGenericArgumentCount} item is required.",
                nameof(genericArguments));
        }

        if ((isPrimitive || isArray || isInterface) && !genericArguments.IsEmpty)
        {
            throw new ArgumentException(
                "Primitive, array, and interface projections cannot carry constructed generic arguments.",
                nameof(genericArguments));
        }

        var normalizedGenericArguments = ImmutableArray.CreateBuilder<ClrmdStaticRuntimeTypeIdentity>(
            genericArguments.Length);
        foreach (var genericArgument in genericArguments)
        {
            if (genericArgument is null ||
                genericArgument.Snapshot != snapshot ||
                genericArgument.PointerWidth != pointerWidth ||
                !genericArgument.GenericArguments.IsEmpty)
            {
                throw new ArgumentException(
                    "Every generic argument must be a non-nested exact runtime type in the same snapshot and target architecture.",
                    nameof(genericArguments));
            }

            normalizedGenericArguments.Add(genericArgument);
        }

        return new ClrmdStaticRuntimeTypeIdentity(
            snapshot,
            pointerWidth,
            runtimeModule,
            moduleContent,
            typeDefToken,
            fullName,
            methodTable,
            isValueType,
            isPrimitive,
            isArray,
            isInterface,
            normalizedGenericArguments.MoveToImmutable());
    }

    /// <summary>Determines whether another runtime type has the same complete canonical identity.</summary>
    /// <param name="other">The runtime type to compare.</param>
    /// <returns><see langword="true"/> only when every versioned canonical fact is equal.</returns>
    public bool Equals(ClrmdStaticRuntimeTypeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticRuntimeTypeIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Identifies the closed Product-supplied decoder expectation carried into runtime field mapping.</summary>
/// <remarks>
/// Host does not infer this tag from a ClrMD type name or shape. Product derives it from exact FieldSig and resolved
/// metadata, while Host separately retains the raw observed ClrMD type identity for later Product correlation.
/// </remarks>
public enum ClrmdStaticExpectedDecoderKind
{
    /// <summary>Product expects the intrinsic four-byte CLI <see cref="int"/> decoder.</summary>
    Int32 = 1,

    /// <summary>Product expects the exact <see cref="Nullable{T}"/> specialized-with-Int32 decoder.</summary>
    NullableInt32 = 2,

    /// <summary>Product expects the exact <see cref="string"/> decoder.</summary>
    String = 3,

    /// <summary>Product expects one exact non-generic concrete managed-reference decoder.</summary>
    ConcreteReference = 4,
}

/// <summary>Freezes one runtime static field and its exact declaring-type and resolved-type projections.</summary>
/// <remarks>
/// The identity represents every mapped runtime static field, including runtime-reported thread-local and context-local
/// shapes and raw literal or RVA attributes. Product owns every semantic interpretation and storage-admission decision;
/// retaining the raw observations here lets unsupported or conflicting evidence remain truthful during composition.
/// <see cref="ExpectedDecoderKind"/> is a Product-supplied expectation, not a Host classification. Exact Product
/// composition must correlate it with FieldSig and resolved metadata/corelib identities; <see cref="ObservedFieldType"/>
/// retains the raw physical ClrMD facts and may truthfully contradict that expectation.
/// </remarks>
public sealed class ClrmdStaticRuntimeFieldIdentity : IEquatable<ClrmdStaticRuntimeFieldIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticRuntimeFieldIdentity(
        ClrmdStaticRuntimeTypeIdentity declaringType,
        int fieldDefinitionToken,
        string name,
        FieldAttributes attributes,
        bool runtimeReportsThreadStatic,
        bool runtimeReportsContextStatic,
        ClrmdStaticExpectedDecoderKind expectedDecoderKind,
        ClrmdStaticRuntimeTypeIdentity observedFieldType)
    {
        DeclaringType = declaringType;
        FieldDefinitionToken = fieldDefinitionToken;
        Name = name;
        Attributes = attributes;
        RuntimeReportsThreadStatic = runtimeReportsThreadStatic;
        RuntimeReportsContextStatic = runtimeReportsContextStatic;
        ExpectedDecoderKind = expectedDecoderKind;
        ObservedFieldType = observedFieldType;

        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-runtime-field-identity", 2);
        writer.WriteLengthPrefixedBytes(declaringType.CanonicalBytes.AsSpan());
        writer.WriteInt32(fieldDefinitionToken);
        writer.WriteString(ClrmdStaticRuntimeTypeIdentity.RuntimeNameCharacterBoundName);
        writer.WriteInt32(ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters);
        writer.WriteString(name);
        writer.WriteInt32((int)attributes);
        writer.WriteBoolean(runtimeReportsThreadStatic);
        writer.WriteBoolean(runtimeReportsContextStatic);
        writer.WriteInt32(Tag(expectedDecoderKind));
        writer.WriteLengthPrefixedBytes(observedFieldType.CanonicalBytes.AsSpan());

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact runtime declaring type that owns the FieldDef projection.</summary>
    public ClrmdStaticRuntimeTypeIdentity DeclaringType { get; }

    /// <summary>Gets the non-nil FieldDef token correlated with the runtime static field.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the exact ordinal field name reported by counted metadata and ClrMD.</summary>
    public string Name { get; }

    /// <summary>Gets the complete raw FieldDef attributes, including topology excluded by later storage admission.</summary>
    public FieldAttributes Attributes { get; }

    /// <summary>Gets the raw Boolean reported by ClrMD for thread-local static storage.</summary>
    public bool RuntimeReportsThreadStatic { get; }

    /// <summary>Gets the raw Boolean reported by ClrMD for context-local static storage.</summary>
    public bool RuntimeReportsContextStatic { get; }

    /// <summary>
    /// Gets the closed Product-supplied decoder expectation. Host never derives this tag from raw ClrMD type facts.
    /// </summary>
    public ClrmdStaticExpectedDecoderKind ExpectedDecoderKind { get; }

    /// <summary>Gets the exact raw ClrMD field type observed independently of the expected decoder tag.</summary>
    public ClrmdStaticRuntimeTypeIdentity ObservedFieldType { get; }

    /// <summary>Gets the fixed canonical 2048-character bound applied before copying the exact field name.</summary>
    public static EvaluationDeterministicBound DeclaredRuntimeNameCharacterBound =>
        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound;

    /// <summary>Gets a defensive copy of the versioned canonical runtime-field bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact mapped runtime static-field identity, including retained excluded topology.</summary>
    /// <param name="declaringType">The detached runtime type that directly owns the field.</param>
    /// <param name="fieldDefToken">The non-nil FieldDef token correlated with the runtime field.</param>
    /// <param name="name">The exact non-empty ordinal field name.</param>
    /// <param name="attributes">The complete raw FieldDef attributes.</param>
    /// <param name="runtimeReportsThreadStatic">The raw thread-local storage Boolean reported by ClrMD.</param>
    /// <param name="runtimeReportsContextStatic">The raw context-local storage Boolean reported by ClrMD.</param>
    /// <param name="expectedDecoderKind">The closed Product-supplied expected decoder tag.</param>
    /// <param name="observedFieldType">
    /// Exact raw ClrMD field type retained independently of the Product expectation.
    /// </param>
    /// <returns>An immutable, content-equal runtime static-field identity.</returns>
    /// <exception cref="ArgumentException">
    /// The field is not a runtime static field, has invalid raw access bits, the expected tag is outside the closed
    /// enum, or the observed type belongs to another snapshot/architecture. Semantic disagreement remains Product work.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The FieldDef token or field-type discriminator is invalid.</exception>
    public static ClrmdStaticRuntimeFieldIdentity Create(
        ClrmdStaticRuntimeTypeIdentity declaringType,
        int fieldDefToken,
        string name,
        FieldAttributes attributes,
        bool runtimeReportsThreadStatic,
        bool runtimeReportsContextStatic,
        ClrmdStaticExpectedDecoderKind expectedDecoderKind,
        ClrmdStaticRuntimeTypeIdentity observedFieldType)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefToken, 0x04, nameof(fieldDefToken));
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                $"A runtime field name cannot exceed {ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters} characters.");
        }
        ClrmdStaticRuntimeMappingCanonical.ValidateDecodedName(name, nameof(name));

        var access = attributes & FieldAttributes.FieldAccessMask;
        if ((int)access > (int)FieldAttributes.Public)
        {
            throw new ArgumentException("The FieldDef has an invalid member-access mask.", nameof(attributes));
        }

        if ((attributes & FieldAttributes.Static) == 0)
        {
            throw new ArgumentException(
                "A mapped ClrStaticField must retain the FieldDef Static attribute.",
                nameof(attributes));
        }

        if (!Enum.IsDefined(expectedDecoderKind))
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDecoderKind));
        }

        ArgumentNullException.ThrowIfNull(observedFieldType);
        if (observedFieldType.Snapshot != declaringType.Snapshot ||
            observedFieldType.PointerWidth != declaringType.PointerWidth)
        {
            throw new ArgumentException(
                "The observed ClrMD field type must belong to the same snapshot and target architecture as its owner.",
                nameof(observedFieldType));
        }

        return new ClrmdStaticRuntimeFieldIdentity(
            declaringType,
            fieldDefToken,
            name,
            attributes,
            runtimeReportsThreadStatic,
            runtimeReportsContextStatic,
            expectedDecoderKind,
            observedFieldType);
    }

    /// <summary>Determines whether another runtime field has the same complete canonical identity.</summary>
    /// <param name="other">The runtime field to compare.</param>
    /// <returns><see langword="true"/> only when every versioned canonical fact is equal.</returns>
    public bool Equals(ClrmdStaticRuntimeFieldIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticRuntimeFieldIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static int Tag(ClrmdStaticExpectedDecoderKind value) => value switch
    {
        ClrmdStaticExpectedDecoderKind.Int32 => 1,
        ClrmdStaticExpectedDecoderKind.NullableInt32 => 2,
        ClrmdStaticExpectedDecoderKind.String => 3,
        ClrmdStaticExpectedDecoderKind.ConcreteReference => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}

/// <summary>Records exact bounded ClrMD scans and retained matches for one runtime declaration mapping.</summary>
/// <remarks>
/// This draft identity is the physical runtime-to-metadata mapping traversal, not product symbol search. Its factory
/// creates only a successful unique mapping: both complete catalogs were exhausted under cap, both scan counts are
/// positive, and each retained-match count is exactly one. The four fixed declared bounds are embedded in canonical
/// bytes so replay cannot silently change a traversal cap or reinterpret an early prefix as uniqueness.
/// </remarks>
public sealed class ClrmdStaticRuntimeDeclarationMappingCounters :
    IEquatable<ClrmdStaticRuntimeDeclarationMappingCounters>
{
    /// <summary>Gets the maximum number of runtime types examined.</summary>
    public const int MaximumRuntimeTypesExamined = 4_096;

    /// <summary>Gets the maximum number of runtime static fields examined on the selected type.</summary>
    public const int MaximumRuntimeStaticFieldsExamined = 256;

    /// <summary>Gets the maximum retained declaring-type matches needed to distinguish unique from ambiguous.</summary>
    public const int MaximumDeclaringTypeMatchesRetained = 2;

    /// <summary>Gets the maximum retained static-field matches needed to distinguish unique from ambiguous.</summary>
    public const int MaximumStaticFieldMatchesRetained = 2;

    /// <summary>Gets the canonical runtime-type scan bound name.</summary>
    public const string RuntimeTypesExaminedBoundName = "static-field.runtime-mapping.runtime-types-examined";

    /// <summary>Gets the canonical runtime-static-field scan bound name.</summary>
    public const string RuntimeStaticFieldsExaminedBoundName =
        "static-field.runtime-mapping.runtime-static-fields-examined";

    /// <summary>Gets the canonical retained declaring-type match bound name.</summary>
    public const string DeclaringTypeMatchesRetainedBoundName =
        "static-field.runtime-mapping.declaring-type-matches-retained";

    /// <summary>Gets the canonical retained static-field match bound name.</summary>
    public const string StaticFieldMatchesRetainedBoundName =
        "static-field.runtime-mapping.static-field-matches-retained";

    private static readonly ImmutableArray<EvaluationDeterministicBound> FixedCanonicalBounds =
        CanonicalReplayEncoding.NormalizeBounds(
            ImmutableArray.Create(
                new EvaluationDeterministicBound(
                    RuntimeTypesExaminedBoundName,
                    MaximumRuntimeTypesExamined),
                new EvaluationDeterministicBound(
                    RuntimeStaticFieldsExaminedBoundName,
                    MaximumRuntimeStaticFieldsExamined),
                new EvaluationDeterministicBound(
                    DeclaringTypeMatchesRetainedBoundName,
                    MaximumDeclaringTypeMatchesRetained),
                new EvaluationDeterministicBound(
                    StaticFieldMatchesRetainedBoundName,
                    MaximumStaticFieldMatchesRetained)),
            "fixedCanonicalBounds");

    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticRuntimeDeclarationMappingCounters(
        int runtimeTypesExamined,
        int runtimeStaticFieldsExamined,
        int declaringTypeMatchesRetained,
        int staticFieldMatchesRetained,
        bool runtimeTypeSearchExhaustive,
        bool runtimeStaticFieldSearchExhaustive)
    {
        RuntimeTypesExamined = runtimeTypesExamined;
        RuntimeStaticFieldsExamined = runtimeStaticFieldsExamined;
        DeclaringTypeMatchesRetained = declaringTypeMatchesRetained;
        StaticFieldMatchesRetained = staticFieldMatchesRetained;
        RuntimeTypeSearchExhaustive = runtimeTypeSearchExhaustive;
        RuntimeStaticFieldSearchExhaustive = runtimeStaticFieldSearchExhaustive;

        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-runtime-declaration-mapping-counters", 1);
        writer.WriteInt32(runtimeTypesExamined);
        writer.WriteInt32(runtimeStaticFieldsExamined);
        writer.WriteInt32(declaringTypeMatchesRetained);
        writer.WriteInt32(staticFieldMatchesRetained);
        writer.WriteBoolean(runtimeTypeSearchExhaustive);
        writer.WriteBoolean(runtimeStaticFieldSearchExhaustive);
        ClrmdStaticRuntimeMappingCanonical.WriteBounds(writer, FixedCanonicalBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the positive number of runtime types in the completely exhausted catalog under the fixed cap.</summary>
    public int RuntimeTypesExamined { get; }

    /// <summary>Gets the positive number of static fields in the exhausted selected-type catalog under the fixed cap.</summary>
    public int RuntimeStaticFieldsExamined { get; }

    /// <summary>Gets the exact uniquely retained declaring-type match count; accepted counters report one.</summary>
    public int DeclaringTypeMatchesRetained { get; }

    /// <summary>Gets the exact uniquely retained static-field match count; accepted counters report one.</summary>
    public int StaticFieldMatchesRetained { get; }

    /// <summary>Gets whether the complete runtime-type catalog was exhausted; exact mappings always report true.</summary>
    public bool RuntimeTypeSearchExhaustive { get; }

    /// <summary>Gets whether the selected type's complete static-field catalog was exhausted; exact mappings are true.</summary>
    public bool RuntimeStaticFieldSearchExhaustive { get; }

    /// <summary>Gets defensive copies of all four fixed canonical traversal bounds in ordinal name order.</summary>
    public ImmutableArray<EvaluationDeterministicBound> CanonicalBounds =>
        CanonicalReplayEncoding.Copy(FixedCanonicalBounds);

    /// <summary>Gets a defensive copy of the versioned canonical counter and bound bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates exact counters for one successful, unique runtime declaration mapping.</summary>
    /// <param name="runtimeTypesExamined">Positive complete runtime-type catalog count, at most 4096.</param>
    /// <param name="runtimeStaticFieldsExamined">Positive complete static-field catalog count, at most 256.</param>
    /// <param name="declaringTypeMatchesRetained">Retained declaring-type matches; exact mapping requires one.</param>
    /// <param name="staticFieldMatchesRetained">Retained static-field matches; exact mapping requires one.</param>
    /// <param name="runtimeTypeSearchExhaustive">Whether the runtime-type catalog was completely exhausted.</param>
    /// <param name="runtimeStaticFieldSearchExhaustive">Whether the selected type's static fields were exhausted.</param>
    /// <returns>Immutable exact counters with the four fixed canonical bounds embedded.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A scan count is zero, negative, or exceeds its fixed cap.</exception>
    /// <exception cref="ArgumentException">
    /// A catalog was not exhausted, or a retained-match count is not exactly one or exceeds its scan count.
    /// </exception>
    public static ClrmdStaticRuntimeDeclarationMappingCounters Create(
        int runtimeTypesExamined,
        int runtimeStaticFieldsExamined,
        int declaringTypeMatchesRetained,
        int staticFieldMatchesRetained,
        bool runtimeTypeSearchExhaustive,
        bool runtimeStaticFieldSearchExhaustive)
    {
        ValidatePositiveScan(
            runtimeTypesExamined,
            MaximumRuntimeTypesExamined,
            nameof(runtimeTypesExamined));
        ValidatePositiveScan(
            runtimeStaticFieldsExamined,
            MaximumRuntimeStaticFieldsExamined,
            nameof(runtimeStaticFieldsExamined));
        if (!runtimeTypeSearchExhaustive || !runtimeStaticFieldSearchExhaustive)
        {
            throw new ArgumentException(
                "An exact mapping cannot infer uniqueness from an unexhausted runtime type or static-field prefix.");
        }

        if (declaringTypeMatchesRetained != 1 ||
            declaringTypeMatchesRetained > MaximumDeclaringTypeMatchesRetained ||
            declaringTypeMatchesRetained > runtimeTypesExamined)
        {
            throw new ArgumentException(
                "An exact mapping must retain exactly one declaring-type match from the examined prefix.",
                nameof(declaringTypeMatchesRetained));
        }

        if (staticFieldMatchesRetained != 1 ||
            staticFieldMatchesRetained > MaximumStaticFieldMatchesRetained ||
            staticFieldMatchesRetained > runtimeStaticFieldsExamined)
        {
            throw new ArgumentException(
                "An exact mapping must retain exactly one static-field match from the examined prefix.",
                nameof(staticFieldMatchesRetained));
        }

        return new ClrmdStaticRuntimeDeclarationMappingCounters(
            runtimeTypesExamined,
            runtimeStaticFieldsExamined,
            declaringTypeMatchesRetained,
            staticFieldMatchesRetained,
            runtimeTypeSearchExhaustive,
            runtimeStaticFieldSearchExhaustive);
    }

    /// <summary>Determines whether another counter set has the same complete canonical counts and fixed bounds.</summary>
    /// <param name="other">The counter set to compare.</param>
    /// <returns><see langword="true"/> only when every versioned canonical fact is equal.</returns>
    public bool Equals(ClrmdStaticRuntimeDeclarationMappingCounters? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticRuntimeDeclarationMappingCounters);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidatePositiveScan(int value, int maximum, string parameterName)
    {
        if (value <= 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"An exact mapping scan must be positive and cannot exceed {maximum}.");
        }
    }
}

/// <summary>Correlates one uniquely selected runtime declaring type with one directly owned runtime static field.</summary>
/// <remarks>
/// This draft identity is deliberately detached from live ClrMD objects. It is an exact physical mapping witness,
/// not a declaration search result or a static-slot/value observation. Later stages may compose it with independently
/// validated metadata and storage evidence without repeating or weakening the runtime selection boundary.
/// </remarks>
public sealed class ClrmdStaticRuntimeDeclarationMappingIdentity :
    IEquatable<ClrmdStaticRuntimeDeclarationMappingIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private ClrmdStaticRuntimeDeclarationMappingIdentity(
        ClrmdStaticRuntimeTypeIdentity declaringType,
        ClrmdStaticRuntimeFieldIdentity field,
        ClrmdStaticRuntimeDeclarationMappingCounters counters)
    {
        DeclaringType = declaringType;
        Field = field;
        Counters = counters;

        var writer = new CanonicalReplayEncoding.Writer("clrmd-static-runtime-declaration-mapping-identity", 1);
        writer.WriteLengthPrefixedBytes(declaringType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(field.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(counters.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the uniquely retained runtime declaring type.</summary>
    public ClrmdStaticRuntimeTypeIdentity DeclaringType { get; }

    /// <summary>Gets the uniquely retained runtime static field, including any exactly retained excluded topology.</summary>
    public ClrmdStaticRuntimeFieldIdentity Field { get; }

    /// <summary>Gets the exact bounded runtime scans and unique retained-match counts.</summary>
    public ClrmdStaticRuntimeDeclarationMappingCounters Counters { get; }

    /// <summary>Gets a defensive copy of the versioned canonical runtime declaration-mapping bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact unique runtime declaration mapping.</summary>
    /// <param name="declaringType">The uniquely retained runtime declaring type.</param>
    /// <param name="field">The uniquely retained runtime static field directly owned by the declaring type.</param>
    /// <param name="counters">Exhaustive positive bounded catalogs with exactly one retained type and field match.</param>
    /// <returns>An immutable, content-equal mapping suitable for later declaration and storage correlation.</returns>
    /// <exception cref="ArgumentException">
    /// The field embeds a different runtime declaring type or the supplied counters are not exact unique mapping
    /// counters.
    /// </exception>
    public static ClrmdStaticRuntimeDeclarationMappingIdentity Create(
        ClrmdStaticRuntimeTypeIdentity declaringType,
        ClrmdStaticRuntimeFieldIdentity field,
        ClrmdStaticRuntimeDeclarationMappingCounters counters)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(counters);
        if (!field.DeclaringType.Equals(declaringType))
        {
            throw new ArgumentException(
                "The runtime field must embed the exact supplied declaring-type identity.",
                nameof(field));
        }

        if (counters.RuntimeTypesExamined <= 0 ||
            counters.RuntimeStaticFieldsExamined <= 0 ||
            counters.DeclaringTypeMatchesRetained != 1 ||
            counters.StaticFieldMatchesRetained != 1 ||
            !counters.RuntimeTypeSearchExhaustive ||
            !counters.RuntimeStaticFieldSearchExhaustive)
        {
            throw new ArgumentException(
                "An exact runtime declaration mapping requires exhausted positive catalogs and one retained match at each stage.",
                nameof(counters));
        }

        return new ClrmdStaticRuntimeDeclarationMappingIdentity(declaringType, field, counters);
    }

    /// <summary>Determines whether another mapping has the same complete canonical runtime evidence.</summary>
    /// <param name="other">The runtime declaration mapping to compare.</param>
    /// <returns><see langword="true"/> only when every versioned canonical fact is equal.</returns>
    public bool Equals(ClrmdStaticRuntimeDeclarationMappingIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClrmdStaticRuntimeDeclarationMappingIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

internal static class ClrmdStaticRuntimeMappingCanonical
{
    internal static void WriteSnapshot(
        CanonicalReplayEncoding.Writer writer,
        ClrmdSnapshotIdentity snapshot) =>
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

    internal static void ValidateRuntimeModule(
        ClrmdRuntimeModuleIdentity module,
        int pointerWidth,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(module.Snapshot.Sha256) ||
            module.AppDomainAddress == 0 ||
            module.ModuleAddress == 0)
        {
            throw new ArgumentException(
                "A complete snapshot-scoped runtime module and application-domain identity is required.",
                parameterName);
        }

        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if (module.AppDomainAddress > maximum ||
            module.ModuleAddress > maximum ||
            module.ImageBase > maximum ||
            module.ImageSize > maximum ||
            (module.ImageBase != 0 &&
             module.ImageSize != 0 &&
             module.ImageSize - 1 > maximum - module.ImageBase))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Runtime module coordinates and the mapped-image range must fit the target pointer width.");
        }
    }

    internal static void ValidateDecodedName(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Any(char.IsControl))
        {
            throw new ArgumentException("A non-empty decoded ordinal metadata name is required.", parameterName);
        }
    }
}
