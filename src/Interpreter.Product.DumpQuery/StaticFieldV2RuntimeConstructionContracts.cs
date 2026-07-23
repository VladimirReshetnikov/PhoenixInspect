using System.Buffers.Binary;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies one exact constructed-runtime draft selection answer.</summary>
/// <remarks>
/// Only <see cref="Exact"/> exposes one selected construction. Every other draft alternative is a prefix-free stop:
/// <see cref="Absent"/> and <see cref="Ambiguous"/> are the two exactness failures W8.1 froze, <see cref="NonExact"/>
/// is the declared candidate bound, and <see cref="NotRequired"/> is the frozen no-call answer of a strategy whose
/// capability vector marks runtime construction as not required.
/// </remarks>
public enum StaticFieldV2RuntimeConstructionSelectionKind
{
    /// <summary>Exactly one complete construction identity matched the bound metadata construction.</summary>
    Exact = 1,

    /// <summary>No complete construction identity matched the bound metadata construction.</summary>
    Absent = 2,

    /// <summary>Two or more distinct complete construction identities matched.</summary>
    Ambiguous = 3,

    /// <summary>A declared draft bound stopped the bounded candidate set before any grouping.</summary>
    NonExact = 4,

    /// <summary>The frozen capability vector marks runtime construction as not required for this strategy.</summary>
    NotRequired = 5,
}

/// <summary>Identifies the deterministic issue of one constructed-runtime draft selection.</summary>
/// <remarks>This draft issue catalog keeps absence, ambiguity, the declared bound, and no-call answers distinct.</remarks>
public enum StaticFieldV2RuntimeConstructionIssue
{
    /// <summary>No issue applies to an exact draft selection.</summary>
    None = 0,

    /// <summary>The strategy provably never acquires a runtime construction, so none was enumerated.</summary>
    ConstructionNotRequiredForStrategy = 1,

    /// <summary>Every bounded candidate group disagreed with the bound metadata construction.</summary>
    ConstructionAbsent = 2,

    /// <summary>Two or more distinct simultaneously loaded constructions matched the same metadata construction.</summary>
    ConstructionAmbiguous = 3,

    /// <summary>The supplied candidate count reached the declared runtime-construction cap plus one.</summary>
    CandidateCountBoundReached = 4,
}

/// <summary>Classifies one static-slot draft acquisition answer.</summary>
/// <remarks>Only <see cref="Exact"/> exposes a slot; every other draft alternative is a prefix-free stop.</remarks>
public enum StaticFieldV2StaticSlotResultKind
{
    /// <summary>One exact static slot was acquired for the classified strategy.</summary>
    Exact = 1,

    /// <summary>Complete supplied evidence contradicted the strategy's frozen slot requirements.</summary>
    Invalid = 2,

    /// <summary>The classified strategy owns no static slot at all.</summary>
    Unsupported = 3,
}

/// <summary>Identifies the deterministic issue of one static-slot draft acquisition.</summary>
/// <remarks>
/// The draft catalog names each frozen per-strategy requirement and each frozen per-strategy rejection separately so a
/// caller can never confuse a missing fact with a fact the strategy is forbidden to carry.
/// </remarks>
public enum StaticFieldV2StaticSlotIssue
{
    /// <summary>No issue applies to an exact draft acquisition.</summary>
    None = 0,

    /// <summary>The strategy requires one exact owner construction selection and none was supplied.</summary>
    ConstructionSelectionRequired = 1,

    /// <summary>The strategy marks runtime construction as not required, so no selection may be supplied.</summary>
    ConstructionSelectionNotPermitted = 2,

    /// <summary>The strategy requires one exact selected-thread identity and none was supplied.</summary>
    ThreadIdentityRequired = 3,

    /// <summary>The strategy owns no thread-relative storage, so no selected thread may be supplied.</summary>
    ThreadIdentityNotPermitted = 4,

    /// <summary>The strategy requires complete module-content, FieldRVA row, and mapped address geometry.</summary>
    ModuleGeometryRequired = 5,

    /// <summary>The strategy owns no module image geometry, so none may be supplied.</summary>
    ModuleGeometryNotPermitted = 6,

    /// <summary>The strategy requires one exact static slot address and none was supplied.</summary>
    SlotAddressRequired = 7,

    /// <summary>The strategy marks slot acquisition as not required, so no slot address may be supplied.</summary>
    SlotAddressNotPermitted = 8,

    /// <summary>A metadata literal lives entirely in metadata and therefore produces no slot.</summary>
    MetadataLiteralHasNoSlot = 9,
}

/// <summary>Classifies one exact static-field runtime draft value decoded from copied raw bytes.</summary>
/// <remarks>The draft kind names the decoded shape; the payload primitive of an enum or nullable is retained apart.</remarks>
public enum StaticFieldV2RuntimeValueKind
{
    /// <summary>One CLI <c>bool</c> decoded from a single zero or one byte.</summary>
    Boolean = 1,

    /// <summary>One CLI <c>char</c> decoded from two little-endian bytes.</summary>
    Char = 2,

    /// <summary>One CLI signed eight-bit integer.</summary>
    Int8 = 3,

    /// <summary>One CLI unsigned eight-bit integer.</summary>
    UInt8 = 4,

    /// <summary>One CLI signed sixteen-bit integer.</summary>
    Int16 = 5,

    /// <summary>One CLI unsigned sixteen-bit integer.</summary>
    UInt16 = 6,

    /// <summary>One CLI signed thirty-two-bit integer.</summary>
    Int32 = 7,

    /// <summary>One CLI unsigned thirty-two-bit integer.</summary>
    UInt32 = 8,

    /// <summary>One CLI signed sixty-four-bit integer.</summary>
    Int64 = 9,

    /// <summary>One CLI unsigned sixty-four-bit integer.</summary>
    UInt64 = 10,

    /// <summary>One CLI single-precision value decoded from its exact four-byte pattern.</summary>
    Single = 11,

    /// <summary>One CLI double-precision value decoded from its exact eight-byte pattern.</summary>
    Double = 12,

    /// <summary>One target-width native signed integer.</summary>
    NativeInt = 13,

    /// <summary>One target-width native unsigned integer.</summary>
    NativeUnsignedInt = 14,

    /// <summary>One enum's decoded underlying integral value.</summary>
    EnumUnderlying = 15,

    /// <summary>One admitted nullable whose exact hasValue flag byte is zero.</summary>
    NullableAbsent = 16,

    /// <summary>One admitted nullable whose exact hasValue flag byte is one, with its decoded payload.</summary>
    NullablePresent = 17,

    /// <summary>One exact all-zero managed reference.</summary>
    NullReference = 18,

    /// <summary>One non-null managed object reference whose target address is retained undereferenced.</summary>
    ObjectReference = 19,

    /// <summary>One non-null managed string reference whose target address is retained undereferenced.</summary>
    StringReference = 20,
}

/// <summary>Classifies one static-field runtime draft value decoding answer.</summary>
/// <remarks>Only <see cref="Exact"/> exposes a decoded draft value; every alternative is a prefix-free stop.</remarks>
public enum StaticFieldV2RuntimeValueResultKind
{
    /// <summary>One admitted shape was decoded exactly from the copied raw bytes.</summary>
    Exact = 1,

    /// <summary>Complete supplied evidence contradicted the declared width or the admitted encoding.</summary>
    Invalid = 2,

    /// <summary>The declared closed type selects a shape this draft decoder does not admit.</summary>
    Unsupported = 3,
}

/// <summary>Identifies the deterministic issue of one static-field runtime draft value decoding.</summary>
/// <remarks>This draft catalog separates width disagreement, malformed encodings, and non-admitted shapes.</remarks>
public enum StaticFieldV2RuntimeValueIssue
{
    /// <summary>No issue applies to an exact draft decoding.</summary>
    None = 0,

    /// <summary>The copied raw byte count disagrees with the declared width of the closed type.</summary>
    RawByteCountDisagreesWithDeclaredWidth = 1,

    /// <summary>The declared closed type is not one of the admitted draft value shapes.</summary>
    UnsupportedValueShape = 2,

    /// <summary>The declared type names an enum but no exact underlying primitive fact was supplied.</summary>
    EnumUnderlyingEvidenceUnavailable = 3,

    /// <summary>The declared type is nullable but no exact physical layout fact was supplied.</summary>
    NullableLayoutEvidenceUnavailable = 4,

    /// <summary>The supplied nullable layout disagrees with the element's exact decoded width.</summary>
    NullableLayoutInvalid = 5,

    /// <summary>The exact <c>bool</c> or nullable hasValue byte is neither zero nor one.</summary>
    FlagEncodingInvalid = 6,
}

/// <summary>Identifies one declared coverage boundary retained by a constructed-runtime draft answer.</summary>
/// <remarks>
/// Each boundary is an informational draft fact naming physical evidence this slice accepts from the caller instead of
/// acquiring itself, so a consumer can never mistake a supplied fact for an independently proven one.
/// </remarks>
public enum StaticFieldV2RuntimeCoverageBoundary
{
    /// <summary>Every runtime construction candidate was supplied by the caller rather than enumerated here.</summary>
    RuntimeConstructionEvidenceSuppliedByCaller = 1,

    /// <summary>The exact selected-thread identity was supplied by the caller rather than acquired here.</summary>
    SelectedThreadEvidenceSuppliedByCaller = 2,

    /// <summary>The module content, FieldRVA row, and mapped geometry were supplied by the caller.</summary>
    ModuleRvaGeometrySuppliedByCaller = 3,

    /// <summary>The decoded raw bytes were copied by the caller; this slice performs no memory read.</summary>
    RawValueBytesCopiedByCaller = 4,

    /// <summary>The exact nullable physical layout was supplied by the caller rather than measured here.</summary>
    NullableLayoutSuppliedByCaller = 5,

    /// <summary>The exact enum underlying primitive was supplied by the caller rather than derived here.</summary>
    EnumUnderlyingKindSuppliedByCaller = 6,

    /// <summary>The exact target pointer width was supplied by the caller and never assumed.</summary>
    TargetPointerWidthSuppliedByCaller = 7,
}

/// <summary>Freezes one exact selected-thread identity required by thread-relative draft storage.</summary>
/// <remarks>
/// W8.1 froze <c>ThreadRelativeSlot</c> as requiring an exact selected thread in addition to an exact owner
/// construction. This sealed draft identity carries only counted physical thread coordinates; no display name, no
/// enumeration ordinal, and no runtime text takes part in it.
/// </remarks>
public sealed class StaticFieldV2SelectedThreadIdentity : IEquatable<StaticFieldV2SelectedThreadIdentity>
{
    private const string CanonicalDomain = "static-field-v2-selected-thread-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2SelectedThreadIdentity(
        int pointerWidth,
        ulong threadObjectAddress,
        int operatingSystemThreadId,
        int managedThreadId)
    {
        PointerWidth = pointerWidth;
        ThreadObjectAddress = threadObjectAddress;
        OperatingSystemThreadId = operatingSystemThreadId;
        ManagedThreadId = managedThreadId;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(threadObjectAddress);
        writer.WriteInt32(operatingSystemThreadId);
        writer.WriteInt32(managedThreadId);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact target pointer width in bytes of the draft thread's snapshot.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the nonzero target address of the selected draft thread's runtime structure.</summary>
    public ulong ThreadObjectAddress { get; }

    /// <summary>Gets the positive operating-system thread identifier of the selected draft thread.</summary>
    public int OperatingSystemThreadId { get; }

    /// <summary>Gets the positive managed thread identifier of the selected draft thread.</summary>
    public int ManagedThreadId { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft thread bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft thread identity.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact selected-thread draft identity from counted physical coordinates.</summary>
    /// <param name="pointerWidth">The exact target pointer width in bytes, either four or eight.</param>
    /// <param name="threadObjectAddress">The nonzero runtime thread structure address.</param>
    /// <param name="operatingSystemThreadId">The positive operating-system thread identifier.</param>
    /// <param name="managedThreadId">The positive managed thread identifier.</param>
    /// <returns>A sealed immutable draft thread identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A width, address, or identifier is outside its admitted range.</exception>
    public static StaticFieldV2SelectedThreadIdentity Create(
        int pointerWidth,
        ulong threadObjectAddress,
        int operatingSystemThreadId,
        int managedThreadId)
    {
        CanonicalReplayEncoding.ValidatePointerValue(
            threadObjectAddress,
            pointerWidth,
            allowZero: false,
            nameof(threadObjectAddress));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operatingSystemThreadId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(managedThreadId);
        return new StaticFieldV2SelectedThreadIdentity(
            pointerWidth,
            threadObjectAddress,
            operatingSystemThreadId,
            managedThreadId);
    }

    /// <summary>Tests canonical equality between two selected-thread draft identities.</summary>
    /// <param name="other">The other draft thread identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2SelectedThreadIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests selected-thread draft identity equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2SelectedThreadIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft thread content.</summary>
    /// <returns>A hash code for this canonical draft thread identity.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact physical nullable storage layout supplied as a draft fact.</summary>
/// <remarks>
/// The admitted nullable draft forms are decoded from the exact hasValue flag byte plus the payload at the exact
/// supplied offset. This slice measures no layout itself, so the geometry is a caller-supplied physical fact retained
/// as a declared coverage boundary.
/// </remarks>
public sealed class StaticFieldV2NullableLayoutFact : IEquatable<StaticFieldV2NullableLayoutFact>
{
    /// <summary>Gets the maximum admitted nullable storage byte count of one draft layout fact.</summary>
    public const int MaximumStorageByteCount = StaticFieldV2RuntimeValueRequest.MaximumRawValueByteCount;

    private const string CanonicalDomain = "static-field-v2-nullable-layout-fact";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2NullableLayoutFact(
        int storageByteCount,
        int hasValueOffset,
        int valueOffset,
        int valueByteCount)
    {
        StorageByteCount = storageByteCount;
        HasValueOffset = hasValueOffset;
        ValueOffset = valueOffset;
        ValueByteCount = valueByteCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(storageByteCount);
        writer.WriteInt32(hasValueOffset);
        writer.WriteInt32(valueOffset);
        writer.WriteInt32(valueByteCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete specialized nullable draft storage size in bytes.</summary>
    public int StorageByteCount { get; }

    /// <summary>Gets the exact byte offset of the one-byte hasValue draft flag.</summary>
    public int HasValueOffset { get; }

    /// <summary>Gets the exact byte offset of the nullable draft payload.</summary>
    public int ValueOffset { get; }

    /// <summary>Gets the exact byte count of the nullable draft payload.</summary>
    public int ValueByteCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft layout bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft layout fact.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact nullable physical layout draft fact.</summary>
    /// <param name="storageByteCount">The complete specialized storage size, at least two bytes.</param>
    /// <param name="hasValueOffset">The nonnegative offset of the one-byte hasValue flag.</param>
    /// <param name="valueOffset">The nonnegative offset of the payload.</param>
    /// <param name="valueByteCount">The positive payload byte count.</param>
    /// <returns>A sealed immutable draft layout fact whose two child ranges are disjoint and in range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A count or offset is outside its admitted range.</exception>
    /// <exception cref="ArgumentException">The two child ranges overlap or leave the specialized storage.</exception>
    public static StaticFieldV2NullableLayoutFact Create(
        int storageByteCount,
        int hasValueOffset,
        int valueOffset,
        int valueByteCount)
    {
        if (storageByteCount is < 2 or > MaximumStorageByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(storageByteCount),
                $"A nullable storage size of two through {MaximumStorageByteCount} bytes is required.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(hasValueOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(valueOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valueByteCount);
        if (hasValueOffset >= storageByteCount ||
            valueByteCount > storageByteCount - valueOffset ||
            valueOffset >= storageByteCount)
        {
            throw new ArgumentException(
                "Both nullable child ranges must fit completely inside the specialized storage.",
                nameof(storageByteCount));
        }
        if (hasValueOffset < valueOffset + valueByteCount && valueOffset <= hasValueOffset)
        {
            throw new ArgumentException(
                "The nullable hasValue flag and payload ranges must be disjoint.",
                nameof(hasValueOffset));
        }
        if (valueOffset <= hasValueOffset && hasValueOffset < valueOffset + valueByteCount)
        {
            throw new ArgumentException(
                "The nullable hasValue flag and payload ranges must be disjoint.",
                nameof(valueOffset));
        }
        return new StaticFieldV2NullableLayoutFact(
            storageByteCount,
            hasValueOffset,
            valueOffset,
            valueByteCount);
    }

    /// <summary>Tests canonical equality between two nullable draft layout facts.</summary>
    /// <param name="other">The other draft layout fact.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2NullableLayoutFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests nullable draft layout fact equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a fact with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2NullableLayoutFact);

    /// <summary>Computes a deterministic hash code from immutable canonical draft layout content.</summary>
    /// <returns>A hash code for this canonical draft layout fact.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one caller-supplied complete constructed-runtime draft candidate identity.</summary>
/// <remarks>
/// The complete draft identity W8.1 froze is exactly this row: definition module and TypeDef token, loader module,
/// containing assembly, loader-allocator address, load-context address, enclosing construction, the ordered recursive
/// closed argument vector keyed to this candidate, the runtime type handle, and the method table. Nothing else is
/// identity: enumeration order, runtime display text, a parsed type name, a global name lookup, and a first matching
/// token may never select or distinguish a candidate, so none of them appears here.
/// <para>
/// This slice does not open a dump, so a candidate is caller-supplied physical evidence exactly like the observation
/// rows elsewhere. Its canonical digest is the complete construction identity used for grouping.
/// </para>
/// </remarks>
public sealed class StaticFieldV2RuntimeConstructionCandidate :
    IEquatable<StaticFieldV2RuntimeConstructionCandidate>
{
    /// <summary>Gets the maximum admitted ordered closed argument count of one draft candidate.</summary>
    public const int MaximumClosedArgumentCount = StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount;

    private const string CanonicalDomain = "static-field-v2-runtime-construction-candidate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataClosedTypeIdentity> closedArguments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeConstructionCandidate(
        ulong runtimeTypeHandleAddress,
        ulong methodTableAddress,
        StaticFieldMetadataModuleIdentity definitionModule,
        int typeDefinitionToken,
        StaticFieldMetadataModuleIdentity loaderModule,
        StaticFieldContainingAssemblyIdentity assembly,
        ulong loaderAllocatorAddress,
        ulong loadContextAddress,
        StaticFieldV2RuntimeConstructionCandidate? enclosingConstruction,
        ImmutableArray<MetadataClosedTypeIdentity> closedArguments)
    {
        RuntimeTypeHandleAddress = runtimeTypeHandleAddress;
        MethodTableAddress = methodTableAddress;
        DefinitionModule = definitionModule;
        TypeDefinitionToken = typeDefinitionToken;
        LoaderModule = loaderModule;
        Assembly = assembly;
        LoaderAllocatorAddress = loaderAllocatorAddress;
        LoadContextAddress = loadContextAddress;
        EnclosingConstruction = enclosingConstruction;
        this.closedArguments = closedArguments;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteUInt64(runtimeTypeHandleAddress);
        writer.WriteUInt64(methodTableAddress);
        writer.WriteSha256(definitionModule.Sha256, nameof(definitionModule));
        writer.WriteInt32(typeDefinitionToken);
        writer.WriteSha256(loaderModule.Sha256, nameof(loaderModule));
        writer.WriteSha256(assembly.Sha256, nameof(assembly));
        writer.WriteUInt64(loaderAllocatorAddress);
        writer.WriteUInt64(loadContextAddress);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, enclosingConstruction?.Sha256);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            closedArguments,
            static argument => argument.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the nonzero runtime type handle address of this draft candidate.</summary>
    public ulong RuntimeTypeHandleAddress { get; }

    /// <summary>Gets the nonzero method-table address of this draft candidate.</summary>
    public ulong MethodTableAddress { get; }

    /// <summary>Gets the exact module that physically declared this draft candidate's TypeDef.</summary>
    public StaticFieldMetadataModuleIdentity DefinitionModule { get; }

    /// <summary>Gets the exact TypeDef token this draft candidate constructs.</summary>
    public int TypeDefinitionToken { get; }

    /// <summary>Gets the exact loader module that owns this draft candidate's construction.</summary>
    public StaticFieldMetadataModuleIdentity LoaderModule { get; }

    /// <summary>Gets the exact containing assembly of this draft candidate's construction.</summary>
    public StaticFieldContainingAssemblyIdentity Assembly { get; }

    /// <summary>Gets the nonzero loader-allocator address of this draft candidate.</summary>
    public ulong LoaderAllocatorAddress { get; }

    /// <summary>Gets the nonzero load-context address of this draft candidate.</summary>
    public ulong LoadContextAddress { get; }

    /// <summary>Gets the exact enclosing draft construction, or null for a top-level construction.</summary>
    public StaticFieldV2RuntimeConstructionCandidate? EnclosingConstruction { get; }

    /// <summary>Gets a defensive copy of the ordered recursive closed draft arguments of this candidate.</summary>
    public ImmutableArray<MetadataClosedTypeIdentity> ClosedArguments =>
        ExpressionV2ContractEncoding.Copy(closedArguments);

    /// <summary>Gets the ordered closed draft argument count of this candidate.</summary>
    public int ClosedArgumentCount => closedArguments.Length;

    /// <summary>Gets the exact target pointer width in bytes of this draft candidate's snapshot.</summary>
    public int PointerWidth => DefinitionModule.Module.PointerWidth;

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft candidate bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest that is this draft candidate's complete construction identity.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one caller-supplied complete constructed-runtime draft candidate.</summary>
    /// <param name="runtimeTypeHandleAddress">The nonzero runtime type handle address.</param>
    /// <param name="methodTableAddress">The nonzero method-table address.</param>
    /// <param name="definitionModule">The exact module physically declaring the constructed TypeDef.</param>
    /// <param name="typeDefinitionToken">The exact non-nil TypeDef token being constructed.</param>
    /// <param name="loaderModule">The exact loader module owning the construction.</param>
    /// <param name="assembly">The exact containing assembly of the construction.</param>
    /// <param name="loaderAllocatorAddress">The nonzero loader-allocator address.</param>
    /// <param name="loadContextAddress">The nonzero load-context address.</param>
    /// <param name="closedArguments">
    /// The ordered recursive closed arguments already resolved for this candidate. The vector is keyed to this
    /// candidate's own runtime type identity; it is never shared across candidates by position alone.
    /// </param>
    /// <param name="enclosingConstruction">The exact enclosing construction, or null for a top-level construction.</param>
    /// <returns>A sealed immutable draft candidate with a defensively copied argument vector.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An address or token is outside its admitted range.</exception>
    /// <exception cref="ArgumentException">The argument vector or module snapshots disagree.</exception>
    public static StaticFieldV2RuntimeConstructionCandidate Create(
        ulong runtimeTypeHandleAddress,
        ulong methodTableAddress,
        StaticFieldMetadataModuleIdentity definitionModule,
        int typeDefinitionToken,
        StaticFieldMetadataModuleIdentity loaderModule,
        StaticFieldContainingAssemblyIdentity assembly,
        ulong loaderAllocatorAddress,
        ulong loadContextAddress,
        ImmutableArray<MetadataClosedTypeIdentity> closedArguments,
        StaticFieldV2RuntimeConstructionCandidate? enclosingConstruction = null)
    {
        ArgumentNullException.ThrowIfNull(definitionModule);
        ArgumentNullException.ThrowIfNull(loaderModule);
        ArgumentNullException.ThrowIfNull(assembly);
        CanonicalReplayEncoding.ValidateMetadataToken(typeDefinitionToken, 0x02, nameof(typeDefinitionToken));

        var pointerWidth = definitionModule.Module.PointerWidth;
        if (loaderModule.Module.PointerWidth != pointerWidth ||
            !string.Equals(
                loaderModule.Module.SnapshotSha256,
                definitionModule.Module.SnapshotSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A construction candidate requires one snapshot and one target pointer width across its modules.",
                nameof(loaderModule));
        }
        CanonicalReplayEncoding.ValidatePointerValue(
            runtimeTypeHandleAddress,
            pointerWidth,
            allowZero: false,
            nameof(runtimeTypeHandleAddress));
        CanonicalReplayEncoding.ValidatePointerValue(
            methodTableAddress,
            pointerWidth,
            allowZero: false,
            nameof(methodTableAddress));
        CanonicalReplayEncoding.ValidatePointerValue(
            loaderAllocatorAddress,
            pointerWidth,
            allowZero: false,
            nameof(loaderAllocatorAddress));
        CanonicalReplayEncoding.ValidatePointerValue(
            loadContextAddress,
            pointerWidth,
            allowZero: false,
            nameof(loadContextAddress));

        var copiedArguments = ExpressionV2ContractEncoding.CopyRequired(
            closedArguments,
            nameof(closedArguments),
            MaximumClosedArgumentCount);
        if (enclosingConstruction is not null &&
            enclosingConstruction.PointerWidth != pointerWidth)
        {
            throw new ArgumentException(
                "An enclosing construction must share the exact target pointer width.",
                nameof(enclosingConstruction));
        }
        return new StaticFieldV2RuntimeConstructionCandidate(
            runtimeTypeHandleAddress,
            methodTableAddress,
            definitionModule,
            typeDefinitionToken,
            loaderModule,
            assembly,
            loaderAllocatorAddress,
            loadContextAddress,
            enclosingConstruction,
            copiedArguments);
    }

    /// <summary>Tests canonical equality between two constructed-runtime draft candidates.</summary>
    /// <param name="other">The other draft candidate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeConstructionCandidate? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constructed-runtime draft candidate equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a candidate with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeConstructionCandidate);

    /// <summary>Computes a deterministic hash code from immutable canonical draft candidate content.</summary>
    /// <returns>A hash code for this canonical draft candidate.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<MetadataClosedTypeIdentity> ClosedArgumentsCore => closedArguments;
}

/// <summary>Freezes one complete constructed-runtime draft selection request.</summary>
/// <remarks>
/// The request names the bound metadata construction, the classified storage strategy whose frozen capability vector
/// the selection must obey, and the complete bounded candidate set. Presenting the candidates in any order produces
/// the identical draft selection, so the array is evidence and never a preference.
/// </remarks>
public sealed class StaticFieldV2RuntimeConstructionRequest : IEquatable<StaticFieldV2RuntimeConstructionRequest>
{
    /// <summary>Gets the maximum admitted supplied candidate count, which is the declared cap plus one.</summary>
    public const int MaximumSuppliedCandidateCount = StaticFieldV2Limits.MaximumRuntimeConstructionCount + 1;

    private const string CanonicalDomain = "static-field-v2-runtime-construction-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> candidates;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeConstructionRequest(
        StaticFieldV2ClosedConstructionOutcome metadataConstruction,
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> candidates,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        MetadataConstruction = metadataConstruction;
        StorageStrategy = storageStrategy;
        this.candidates = candidates;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(metadataConstruction.Sha256, nameof(metadataConstruction));
        writer.WriteSha256(storageStrategy.Sha256, nameof(storageStrategy));
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            candidates,
            static candidate => candidate.CanonicalBytes);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact bound metadata construction this draft selection must match.</summary>
    public StaticFieldV2ClosedConstructionOutcome MetadataConstruction { get; }

    /// <summary>Gets the classified draft storage strategy whose frozen capability vector governs acquisition.</summary>
    public StaticFieldV2StorageStrategyOutcome StorageStrategy { get; }

    /// <summary>Gets a defensive copy of the complete bounded draft candidate set as supplied.</summary>
    public ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> Candidates =>
        ExpressionV2ContractEncoding.Copy(candidates);

    /// <summary>Gets the supplied draft candidate count before any grouping.</summary>
    public int SuppliedCandidateCount => candidates.Length;

    /// <summary>Gets the caller-owned capability probes this draft selection routes every acquisition through.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete constructed-runtime draft selection request.</summary>
    /// <param name="metadataConstruction">One exact bound owner construction carrying a named final classification.</param>
    /// <param name="storageStrategy">One exact classified storage strategy carrying its frozen capability vector.</param>
    /// <param name="candidates">
    /// Every bounded same-TypeDef candidate supplied by the caller, in any order. The array may hold one entry beyond
    /// the declared cap so the selection can report the exact cap-plus-one observation.
    /// </param>
    /// <param name="capabilityProbes">Caller-owned probes whose counters become the retained draft ledger.</param>
    /// <returns>A sealed immutable draft request with a defensively copied candidate set.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentException">A supplied outcome is not exact or the candidate set is malformed.</exception>
    public static StaticFieldV2RuntimeConstructionRequest Create(
        StaticFieldV2ClosedConstructionOutcome metadataConstruction,
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> candidates,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(metadataConstruction);
        ArgumentNullException.ThrowIfNull(storageStrategy);
        if (metadataConstruction.ResultKind != StaticFieldV2ClosedConstructionResultKind.Exact ||
            metadataConstruction.OwnerConstruction is not { } owner ||
            owner.FinalClassification is null)
        {
            throw new ArgumentException(
                "A runtime selection requires one exact bound owner construction with a named final classification.",
                nameof(metadataConstruction));
        }
        if (storageStrategy.ResultKind != StaticFieldV2StorageStrategyResultKind.Exact ||
            storageStrategy.Strategy is null)
        {
            throw new ArgumentException(
                "A runtime selection requires one exact classified storage strategy.",
                nameof(storageStrategy));
        }

        var copied = ExpressionV2ContractEncoding.CopyRequired(
            candidates,
            nameof(candidates),
            MaximumSuppliedCandidateCount);
        return new StaticFieldV2RuntimeConstructionRequest(
            metadataConstruction,
            storageStrategy,
            copied,
            capabilityProbes);
    }

    /// <summary>Tests canonical equality between two constructed-runtime draft selection requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeConstructionRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constructed-runtime draft selection request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeConstructionRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> CandidatesCore => candidates;
}

/// <summary>Freezes the complete draft outcome of one constructed-runtime selection.</summary>
/// <remarks>
/// The draft selection groups every supplied candidate by its complete construction identity, keeps only the groups
/// whose definition module, TypeDef token, and ordered closed arguments agree with the bound metadata construction,
/// and requires exactly one such group. Enumeration order, runtime display text, a parsed type name, a global name
/// lookup, and a first matching token never take part.
/// </remarks>
public sealed class StaticFieldV2RuntimeConstructionSelection :
    IEquatable<StaticFieldV2RuntimeConstructionSelection>
{
    /// <summary>Gets the frozen diagnostic code retained by an absent-construction draft stop.</summary>
    public const string ConstructionAbsentCode = "W8_RUNTIME_CONSTRUCTION_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by an ambiguous-construction draft stop.</summary>
    public const string ConstructionAmbiguousCode = "W8_RUNTIME_CONSTRUCTION_AMBIGUOUS";

    /// <summary>Gets the frozen diagnostic code retained by a not-required construction draft answer.</summary>
    public const string ConstructionNotRequiredCode = "W8_RUNTIME_CONSTRUCTION_NOT_REQUIRED";

    /// <summary>Gets the frozen diagnostic code retained by a candidate cap-plus-one draft stop.</summary>
    public const string ConstructionCountBoundReachedCode = "W8_RUNTIME_CONSTRUCTION_COUNT_BOUND";

    /// <summary>Gets the shared runtime-construction draft cap applied by one complete selection.</summary>
    public const int MaximumRuntimeConstructionCount = StaticFieldV2Limits.MaximumRuntimeConstructionCount;

    private const string CanonicalDomain = "static-field-v2-runtime-construction-selection";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeConstructionSelection(
        StaticFieldV2RuntimeConstructionSelectionKind resultKind,
        StaticFieldV2RuntimeConstructionIssue issue,
        StaticFieldV2RuntimeConstructionRequest request,
        StaticFieldV2RuntimeConstructionCandidate? selectedCandidate,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int distinctConstructionCount,
        int matchingConstructionCount,
        int observedCount,
        string? diagnosticCode)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        SelectedCandidate = selectedCandidate;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        ReachedBound = reachedBound;
        DistinctConstructionCount = distinctConstructionCount;
        MatchingConstructionCount = matchingConstructionCount;
        ObservedCount = observedCount;
        DiagnosticCode = diagnosticCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedCandidate?.Sha256);
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(distinctConstructionCount);
        writer.WriteInt32(matchingConstructionCount);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft selection is exact, absent, ambiguous, bounded, or not required.</summary>
    public StaticFieldV2RuntimeConstructionSelectionKind ResultKind { get; }

    /// <summary>Gets the typed draft selection issue, or none for an exact outcome.</summary>
    public StaticFieldV2RuntimeConstructionIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2RuntimeConstructionRequest Request { get; }

    /// <summary>Gets the single selected draft construction, or null for every prefix-free stop.</summary>
    public StaticFieldV2RuntimeConstructionCandidate? SelectedCandidate { get; }

    /// <summary>Gets the capability-call draft ledger proving which acquisitions this selection performed.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the distinct complete construction identity count of the grouped draft candidate set.</summary>
    public int DistinctConstructionCount { get; }

    /// <summary>Gets the count of distinct draft groups that matched the bound metadata construction.</summary>
    public int MatchingConstructionCount { get; }

    /// <summary>Gets the grouped, matching, or cap-plus-one draft observation of this answer.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the frozen diagnostic code of a typed draft stop, otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the classified draft storage strategy this selection obeyed.</summary>
    public StaticFieldV2StorageStrategy Strategy => Request.StorageStrategy.Strategy!.Value;

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft selection bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft selection.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two constructed-runtime draft selections.</summary>
    /// <param name="other">The other draft selection.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeConstructionSelection? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constructed-runtime draft selection equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a selection with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeConstructionSelection);

    /// <summary>Computes a deterministic hash code from immutable canonical draft selection content.</summary>
    /// <returns>A hash code for this canonical draft selection.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2RuntimeConstructionSelection IssueExact(
        StaticFieldV2RuntimeConstructionRequest request,
        StaticFieldV2RuntimeConstructionCandidate selectedCandidate,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        int distinctConstructionCount) =>
        new(
            StaticFieldV2RuntimeConstructionSelectionKind.Exact,
            StaticFieldV2RuntimeConstructionIssue.None,
            request,
            selectedCandidate,
            capabilityCallLedger,
            declaredCoverageBoundaries,
            null,
            distinctConstructionCount,
            1,
            distinctConstructionCount,
            null);

    internal static StaticFieldV2RuntimeConstructionSelection IssueStop(
        StaticFieldV2RuntimeConstructionSelectionKind resultKind,
        StaticFieldV2RuntimeConstructionIssue issue,
        StaticFieldV2RuntimeConstructionRequest request,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int distinctConstructionCount,
        int matchingConstructionCount,
        int observedCount,
        string diagnosticCode)
    {
        ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        return new StaticFieldV2RuntimeConstructionSelection(
            resultKind,
            issue,
            request,
            null,
            capabilityCallLedger,
            declaredCoverageBoundaries,
            reachedBound,
            distinctConstructionCount,
            matchingConstructionCount,
            observedCount,
            diagnosticCode);
    }
}

/// <summary>Freezes one exact static-slot draft identity for one classified storage strategy.</summary>
/// <remarks>
/// The sealed draft identity is minted only by <see cref="StaticFieldV2StaticSlotOutcome"/>. A constructed slot and a
/// thread-relative slot carry an exact owner construction and an exact slot address; a thread-relative slot also
/// carries its exact selected thread. A module RVA slot carries module content, its FieldRVA row, and mapped
/// RVA/address geometry while carrying no construction and no slot address at all.
/// </remarks>
public sealed class StaticFieldV2StaticSlotIdentity : IEquatable<StaticFieldV2StaticSlotIdentity>
{
    private const string CanonicalDomain = "static-field-v2-static-slot-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StaticSlotIdentity(
        StaticFieldV2StorageStrategy strategy,
        StaticFieldV2RuntimeConstructionSelection? ownerConstruction,
        int fieldDefinitionToken,
        ulong? slotAddress,
        int readWidth,
        StaticFieldV2SelectedThreadIdentity? selectedThread,
        ModuleContentIdentity? moduleContent,
        int? fieldRvaRowToken,
        uint? mappedRelativeVirtualAddress,
        ulong? mappedAddress)
    {
        Strategy = strategy;
        OwnerConstruction = ownerConstruction;
        FieldDefinitionToken = fieldDefinitionToken;
        SlotAddress = slotAddress;
        ReadWidth = readWidth;
        SelectedThread = selectedThread;
        ModuleContent = moduleContent;
        FieldRvaRowToken = fieldRvaRowToken;
        MappedRelativeVirtualAddress = mappedRelativeVirtualAddress;
        MappedAddress = mappedAddress;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)strategy);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, ownerConstruction?.Sha256);
        writer.WriteInt32(fieldDefinitionToken);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, slotAddress);
        writer.WriteInt32(readWidth);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedThread?.Sha256);
        writer.WriteBoolean(moduleContent is not null);
        if (moduleContent is not null)
        {
            writer.WriteString(moduleContent.Mvid.ToString("N"));
            writer.WriteInt32(moduleContent.MetadataLength);
            writer.WriteSha256(moduleContent.MetadataSha256, nameof(moduleContent));
        }
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, fieldRvaRowToken);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(
            writer,
            mappedRelativeVirtualAddress.HasValue ? mappedRelativeVirtualAddress.Value : null);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, mappedAddress);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the classified draft storage strategy that owns this slot.</summary>
    public StaticFieldV2StorageStrategy Strategy { get; }

    /// <summary>Gets the exact owner construction selection, or null when the strategy requires none.</summary>
    public StaticFieldV2RuntimeConstructionSelection? OwnerConstruction { get; }

    /// <summary>Gets the exact physical FieldDef token whose draft storage this slot names.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the exact static slot address, or null when the strategy acquires no slot.</summary>
    public ulong? SlotAddress { get; }

    /// <summary>Gets the exact counted read width in bytes of this draft slot.</summary>
    public int ReadWidth { get; }

    /// <summary>Gets the exact selected thread, or null for every non thread-relative draft strategy.</summary>
    public StaticFieldV2SelectedThreadIdentity? SelectedThread { get; }

    /// <summary>Gets the exact module content identity, or null for every non module-RVA draft strategy.</summary>
    public ModuleContentIdentity? ModuleContent { get; }

    /// <summary>Gets the exact FieldRVA row token, or null for every non module-RVA draft strategy.</summary>
    public int? FieldRvaRowToken { get; }

    /// <summary>Gets the exact mapped relative virtual address, or null for every non module-RVA draft strategy.</summary>
    public uint? MappedRelativeVirtualAddress { get; }

    /// <summary>Gets the exact mapped image address, or null for every non module-RVA draft strategy.</summary>
    public ulong? MappedAddress { get; }

    /// <summary>Gets the exact address the counted raw draft read must use for this slot.</summary>
    public ulong EffectiveAddress => SlotAddress ?? MappedAddress!.Value;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft slot bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft slot identity.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two static-slot draft identities.</summary>
    /// <param name="other">The other draft slot identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2StaticSlotIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests static-slot draft identity equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a slot with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StaticSlotIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft slot content.</summary>
    /// <returns>A hash code for this canonical draft slot identity.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2StaticSlotIdentity Create(
        object mintCapability,
        StaticFieldV2StorageStrategy strategy,
        StaticFieldV2RuntimeConstructionSelection? ownerConstruction,
        int fieldDefinitionToken,
        ulong? slotAddress,
        int readWidth,
        StaticFieldV2SelectedThreadIdentity? selectedThread,
        ModuleContentIdentity? moduleContent,
        int? fieldRvaRowToken,
        uint? mappedRelativeVirtualAddress,
        ulong? mappedAddress)
    {
        if (!StaticFieldV2StaticSlotOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A static-slot identity requires the slot outcome's private mint capability.",
                nameof(mintCapability));
        }

        ExpressionV2ContractEncoding.RequireDefined(strategy, nameof(strategy));
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(readWidth);
        return new StaticFieldV2StaticSlotIdentity(
            strategy,
            ownerConstruction,
            fieldDefinitionToken,
            slotAddress,
            readWidth,
            selectedThread,
            moduleContent,
            fieldRvaRowToken,
            mappedRelativeVirtualAddress,
            mappedAddress);
    }
}

/// <summary>Freezes one complete static-slot draft acquisition request.</summary>
/// <remarks>
/// The request carries the classified strategy plus every physical fact any admitted strategy could need. The
/// acquisition validates each strategy's frozen requirements and rejections against these facts, so a caller can never
/// smuggle a thread identity into a constructed slot or a construction into a module RVA.
/// </remarks>
public sealed class StaticFieldV2StaticSlotRequest : IEquatable<StaticFieldV2StaticSlotRequest>
{
    /// <summary>Gets the maximum admitted counted read width in bytes of one draft slot request.</summary>
    public const int MaximumReadWidth = StaticFieldV2RuntimeValueRequest.MaximumRawValueByteCount;

    private const string CanonicalDomain = "static-field-v2-static-slot-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StaticSlotRequest(
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        StaticFieldV2RuntimeConstructionSelection? constructionSelection,
        ulong? slotAddress,
        int readWidth,
        StaticFieldV2SelectedThreadIdentity? selectedThread,
        ModuleContentIdentity? moduleContent,
        int? fieldRvaRowToken,
        uint? mappedRelativeVirtualAddress,
        ulong? mappedAddress,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        StorageStrategy = storageStrategy;
        ConstructionSelection = constructionSelection;
        SlotAddress = slotAddress;
        ReadWidth = readWidth;
        SelectedThread = selectedThread;
        ModuleContent = moduleContent;
        FieldRvaRowToken = fieldRvaRowToken;
        MappedRelativeVirtualAddress = mappedRelativeVirtualAddress;
        MappedAddress = mappedAddress;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(storageStrategy.Sha256, nameof(storageStrategy));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, constructionSelection?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, slotAddress);
        writer.WriteInt32(readWidth);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedThread?.Sha256);
        writer.WriteBoolean(moduleContent is not null);
        if (moduleContent is not null)
        {
            writer.WriteString(moduleContent.Mvid.ToString("N"));
            writer.WriteInt32(moduleContent.MetadataLength);
            writer.WriteSha256(moduleContent.MetadataSha256, nameof(moduleContent));
        }
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, fieldRvaRowToken);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(
            writer,
            mappedRelativeVirtualAddress.HasValue ? mappedRelativeVirtualAddress.Value : null);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, mappedAddress);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact classified draft storage strategy whose slot is acquired.</summary>
    public StaticFieldV2StorageStrategyOutcome StorageStrategy { get; }

    /// <summary>Gets the supplied owner construction draft selection, or null when none was supplied.</summary>
    public StaticFieldV2RuntimeConstructionSelection? ConstructionSelection { get; }

    /// <summary>Gets the supplied exact static slot address, or null when none was supplied.</summary>
    public ulong? SlotAddress { get; }

    /// <summary>Gets the supplied exact counted read width in bytes.</summary>
    public int ReadWidth { get; }

    /// <summary>Gets the supplied exact selected-thread draft identity, or null when none was supplied.</summary>
    public StaticFieldV2SelectedThreadIdentity? SelectedThread { get; }

    /// <summary>Gets the supplied exact module content identity, or null when none was supplied.</summary>
    public ModuleContentIdentity? ModuleContent { get; }

    /// <summary>Gets the supplied exact FieldRVA row token, or null when none was supplied.</summary>
    public int? FieldRvaRowToken { get; }

    /// <summary>Gets the supplied exact mapped relative virtual address, or null when none was supplied.</summary>
    public uint? MappedRelativeVirtualAddress { get; }

    /// <summary>Gets the supplied exact mapped image address, or null when none was supplied.</summary>
    public ulong? MappedAddress { get; }

    /// <summary>Gets the caller-owned capability probes this draft acquisition routes every call through.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets the exact physical FieldDef token this draft acquisition names.</summary>
    public int FieldDefinitionToken => StorageStrategy.FieldDefinitionToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete static-slot draft acquisition request.</summary>
    /// <param name="storageStrategy">One exact classified storage strategy.</param>
    /// <param name="readWidth">The positive counted read width in bytes of the slot's value.</param>
    /// <param name="constructionSelection">The supplied owner construction selection, or null.</param>
    /// <param name="slotAddress">The supplied exact static slot address, or null.</param>
    /// <param name="selectedThread">The supplied exact selected-thread identity, or null.</param>
    /// <param name="moduleContent">The supplied exact module content identity, or null.</param>
    /// <param name="fieldRvaRowToken">The supplied exact FieldRVA row token, or null.</param>
    /// <param name="mappedRelativeVirtualAddress">The supplied exact mapped relative virtual address, or null.</param>
    /// <param name="mappedAddress">The supplied exact mapped image address, or null.</param>
    /// <param name="capabilityProbes">Caller-owned probes whose counters become the retained draft ledger.</param>
    /// <returns>A sealed immutable draft request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="storageStrategy"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The read width or a supplied token is out of range.</exception>
    /// <exception cref="ArgumentException">The strategy is not exact or a supplied selection disagrees with it.</exception>
    public static StaticFieldV2StaticSlotRequest Create(
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        int readWidth,
        StaticFieldV2RuntimeConstructionSelection? constructionSelection = null,
        ulong? slotAddress = null,
        StaticFieldV2SelectedThreadIdentity? selectedThread = null,
        ModuleContentIdentity? moduleContent = null,
        int? fieldRvaRowToken = null,
        uint? mappedRelativeVirtualAddress = null,
        ulong? mappedAddress = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(storageStrategy);
        if (storageStrategy.ResultKind != StaticFieldV2StorageStrategyResultKind.Exact ||
            storageStrategy.Strategy is null)
        {
            throw new ArgumentException(
                "A static-slot acquisition requires one exact classified storage strategy.",
                nameof(storageStrategy));
        }
        if (readWidth is <= 0 or > MaximumReadWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readWidth),
                $"A counted read width of one through {MaximumReadWidth} bytes is required.");
        }
        if (fieldRvaRowToken is { } rvaToken &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(rvaToken, 0x1D))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fieldRvaRowToken),
                "A non-nil FieldRVA metadata token is required when one is supplied.");
        }
        if (constructionSelection is not null &&
            !constructionSelection.Request.StorageStrategy.Equals(storageStrategy))
        {
            throw new ArgumentException(
                "A supplied construction selection must retain the exact same classified storage strategy.",
                nameof(constructionSelection));
        }
        if (slotAddress == 0 || mappedAddress == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotAddress),
                "A supplied slot or mapped address must be nonzero.");
        }

        return new StaticFieldV2StaticSlotRequest(
            storageStrategy,
            constructionSelection,
            slotAddress,
            readWidth,
            selectedThread,
            moduleContent,
            fieldRvaRowToken,
            mappedRelativeVirtualAddress,
            mappedAddress,
            capabilityProbes);
    }

    /// <summary>Tests canonical equality between two static-slot draft acquisition requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2StaticSlotRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests static-slot draft acquisition request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StaticSlotRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete draft outcome of one static-slot acquisition.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of the static-slot identity it retains. An exact answer names one slot
/// whose facts satisfy every frozen per-strategy requirement and violate no frozen per-strategy rejection; every other
/// alternative is a prefix-free stop that exposes no slot.
/// </remarks>
public sealed class StaticFieldV2StaticSlotOutcome : IEquatable<StaticFieldV2StaticSlotOutcome>
{
    /// <summary>Gets the frozen diagnostic code retained by a metadata-literal draft slot non-admission.</summary>
    public const string MetadataLiteralHasNoSlotCode = "W8_METADATA_LITERAL_HAS_NO_SLOT";

    /// <summary>Gets the frozen diagnostic code retained by a contradicted slot-geometry draft stop.</summary>
    public const string SlotGeometryContradictedCode = "W8_STATIC_SLOT_GEOMETRY_CONTRADICTED";

    private const string CanonicalDomain = "static-field-v2-static-slot-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StaticSlotOutcome(
        StaticFieldV2StaticSlotResultKind resultKind,
        StaticFieldV2StaticSlotIssue issue,
        StaticFieldV2StaticSlotRequest request,
        StaticFieldV2StaticSlotIdentity? slot,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        string? diagnosticCode)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        Slot = slot;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        DiagnosticCode = diagnosticCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, slot?.Sha256);
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft acquisition is exact, invalid, or a typed non-admission.</summary>
    public StaticFieldV2StaticSlotResultKind ResultKind { get; }

    /// <summary>Gets the typed draft acquisition issue, or none for an exact outcome.</summary>
    public StaticFieldV2StaticSlotIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2StaticSlotRequest Request { get; }

    /// <summary>Gets the single acquired draft slot, or null for every prefix-free stop.</summary>
    public StaticFieldV2StaticSlotIdentity? Slot { get; }

    /// <summary>Gets the capability-call draft ledger proving which acquisitions this outcome performed.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the frozen diagnostic code of a typed draft stop, otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the classified draft storage strategy this acquisition validated.</summary>
    public StaticFieldV2StorageStrategy Strategy => Request.StorageStrategy.Strategy!.Value;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two static-slot draft outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2StaticSlotOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests static-slot draft outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StaticSlotOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2StaticSlotIdentity IssueSlot(
        StaticFieldV2StaticSlotRequest request,
        StaticFieldV2StorageStrategy strategy) =>
        StaticFieldV2StaticSlotIdentity.Create(
            RowMintCapability,
            strategy,
            request.ConstructionSelection,
            request.FieldDefinitionToken,
            request.SlotAddress,
            request.ReadWidth,
            request.SelectedThread,
            request.ModuleContent,
            request.FieldRvaRowToken,
            request.MappedRelativeVirtualAddress,
            request.MappedAddress);

    internal static StaticFieldV2StaticSlotOutcome IssueExact(
        StaticFieldV2StaticSlotRequest request,
        StaticFieldV2StaticSlotIdentity slot,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries) =>
        new(
            StaticFieldV2StaticSlotResultKind.Exact,
            StaticFieldV2StaticSlotIssue.None,
            request,
            slot,
            capabilityCallLedger,
            declaredCoverageBoundaries,
            null);

    internal static StaticFieldV2StaticSlotOutcome IssueStop(
        StaticFieldV2StaticSlotResultKind resultKind,
        StaticFieldV2StaticSlotIssue issue,
        StaticFieldV2StaticSlotRequest request,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        string diagnosticCode)
    {
        ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        return new StaticFieldV2StaticSlotOutcome(
            resultKind,
            issue,
            request,
            null,
            capabilityCallLedger,
            declaredCoverageBoundaries,
            diagnosticCode);
    }
}

/// <summary>Freezes one complete static-field runtime draft value decoding request.</summary>
/// <remarks>
/// The request carries the field's exact closed type, the raw bytes already copied out of the dump, the exact target
/// pointer width, and the two facts this slice never derives: an enum's underlying primitive and a nullable's physical
/// layout. Nothing here is read from a live runtime; decoding is pure over the supplied bytes.
/// </remarks>
public sealed class StaticFieldV2RuntimeValueRequest : IEquatable<StaticFieldV2RuntimeValueRequest>
{
    /// <summary>Gets the maximum admitted copied raw byte count of one draft value request.</summary>
    public const int MaximumRawValueByteCount = 32;

    private const string CanonicalDomain = "static-field-v2-runtime-value-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> rawBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeValueRequest(
        MetadataClosedTypeIdentity declaredType,
        ImmutableArray<byte> rawBytes,
        int targetPointerWidth,
        MetadataPrimitiveTypeKind? enumUnderlyingKind,
        StaticFieldV2NullableLayoutFact? nullableLayout,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        DeclaredType = declaredType;
        this.rawBytes = rawBytes;
        TargetPointerWidth = targetPointerWidth;
        EnumUnderlyingKind = enumUnderlyingKind;
        NullableLayout = nullableLayout;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(declaredType.Sha256, nameof(declaredType));
        writer.WriteLengthPrefixedBytes(rawBytes.AsSpan());
        writer.WriteInt32(targetPointerWidth);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, enumUnderlyingKind);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, nullableLayout?.Sha256);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact closed draft type of the static field whose value is decoded.</summary>
    public MetadataClosedTypeIdentity DeclaredType { get; }

    /// <summary>Gets a defensive copy of the raw bytes copied out of the dump by the caller.</summary>
    public ImmutableArray<byte> RawBytes => ExpressionV2ContractEncoding.Copy(rawBytes);

    /// <summary>Gets the copied raw draft byte count supplied for decoding.</summary>
    public int RawByteCount => rawBytes.Length;

    /// <summary>Gets the exact target pointer width in bytes, supplied as a fact and never assumed.</summary>
    public int TargetPointerWidth { get; }

    /// <summary>Gets the exact enum underlying primitive draft fact, or null when none was supplied.</summary>
    public MetadataPrimitiveTypeKind? EnumUnderlyingKind { get; }

    /// <summary>Gets the exact nullable physical layout draft fact, or null when none was supplied.</summary>
    public StaticFieldV2NullableLayoutFact? NullableLayout { get; }

    /// <summary>Gets the caller-owned capability probes this pure draft decoding never invokes.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete static-field runtime draft value decoding request.</summary>
    /// <param name="declaredType">The exact closed type of the static field.</param>
    /// <param name="rawBytes">The bytes already copied out of the dump, bounded by the admitted byte count.</param>
    /// <param name="targetPointerWidth">The exact target pointer width in bytes, either four or eight.</param>
    /// <param name="enumUnderlyingKind">The exact enum underlying primitive fact, or null when inapplicable.</param>
    /// <param name="nullableLayout">The exact nullable physical layout fact, or null when inapplicable.</param>
    /// <param name="capabilityProbes">Caller-owned probes that this pure decoding never invokes.</param>
    /// <returns>A sealed immutable draft request with a defensively copied byte payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declaredType"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The pointer width is neither four nor eight.</exception>
    /// <exception cref="ArgumentException">The raw byte payload is default or over the admitted byte count.</exception>
    public static StaticFieldV2RuntimeValueRequest Create(
        MetadataClosedTypeIdentity declaredType,
        ImmutableArray<byte> rawBytes,
        int targetPointerWidth,
        MetadataPrimitiveTypeKind? enumUnderlyingKind = null,
        StaticFieldV2NullableLayoutFact? nullableLayout = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        CanonicalReplayEncoding.ValidatePointerWidth(targetPointerWidth);
        if (rawBytes.IsDefault || rawBytes.Length > MaximumRawValueByteCount)
        {
            throw new ArgumentException(
                $"An initialized copied byte payload of at most {MaximumRawValueByteCount} bytes is required.",
                nameof(rawBytes));
        }
        if (enumUnderlyingKind is { } kind)
        {
            ExpressionV2ContractEncoding.RequireDefined(kind, nameof(enumUnderlyingKind));
        }

        return new StaticFieldV2RuntimeValueRequest(
            declaredType,
            ExpressionV2ContractEncoding.Copy(rawBytes),
            targetPointerWidth,
            enumUnderlyingKind,
            nullableLayout,
            capabilityProbes);
    }

    /// <summary>Tests canonical equality between two runtime draft value decoding requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeValueRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests runtime draft value decoding request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeValueRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<byte> RawBytesCore => rawBytes;
}

/// <summary>Freezes the complete draft outcome of one static-field runtime value decoding.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of the capability-call ledger it retains. Decoding is pure over the
/// copied bytes, so every counter of that ledger is zero. An exact answer names one admitted shape and its decoded
/// payload; a byte count disagreeing with the declared width is invalid and an unadmitted shape is unsupported.
/// </remarks>
public sealed class StaticFieldV2RuntimeValueOutcome : IEquatable<StaticFieldV2RuntimeValueOutcome>
{
    private const string CanonicalDomain = "static-field-v2-runtime-value-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeValueOutcome(
        StaticFieldV2RuntimeValueResultKind resultKind,
        StaticFieldV2RuntimeValueIssue issue,
        StaticFieldV2RuntimeValueRequest request,
        StaticFieldV2RuntimeValueKind? valueKind,
        MetadataPrimitiveTypeKind? payloadKind,
        long? signedValue,
        ulong? unsignedValue,
        ulong? floatingBitPattern,
        ulong? referenceAddress,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        int declaredWidth,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        ValueKind = valueKind;
        PayloadKind = payloadKind;
        SignedValue = signedValue;
        UnsignedValue = unsignedValue;
        FloatingBitPattern = floatingBitPattern;
        ReferenceAddress = referenceAddress;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        DeclaredWidth = declaredWidth;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, valueKind);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, payloadKind);
        ExpressionV2ContractEncoding.WriteOptionalInt64(writer, signedValue);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, unsignedValue);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, floatingBitPattern);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, referenceAddress);
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        writer.WriteInt32(declaredWidth);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft decoding is exact, invalid, or a typed non-admission.</summary>
    public StaticFieldV2RuntimeValueResultKind ResultKind { get; }

    /// <summary>Gets the typed draft decoding issue, or none for an exact outcome.</summary>
    public StaticFieldV2RuntimeValueIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2RuntimeValueRequest Request { get; }

    /// <summary>Gets the decoded draft value shape, or null for every prefix-free stop.</summary>
    public StaticFieldV2RuntimeValueKind? ValueKind { get; }

    /// <summary>Gets the underlying draft primitive of an enum or nullable payload, otherwise null.</summary>
    public MetadataPrimitiveTypeKind? PayloadKind { get; }

    /// <summary>Gets the decoded signed draft value for a signed integral shape, otherwise null.</summary>
    public long? SignedValue { get; }

    /// <summary>Gets the decoded unsigned draft value for an unsigned, char, or boolean shape, otherwise null.</summary>
    public ulong? UnsignedValue { get; }

    /// <summary>Gets the exact floating draft bit pattern for a single or double shape, otherwise null.</summary>
    public ulong? FloatingBitPattern { get; }

    /// <summary>Gets the retained non-null draft target address of a managed reference, otherwise null.</summary>
    public ulong? ReferenceAddress { get; }

    /// <summary>Gets the decoded single-precision draft value reinterpreted from its exact bit pattern.</summary>
    public float? SingleValue => ValueKind == StaticFieldV2RuntimeValueKind.Single && FloatingBitPattern is { } bits
        ? BitConverter.UInt32BitsToSingle((uint)bits)
        : null;

    /// <summary>Gets the decoded double-precision draft value reinterpreted from its exact bit pattern.</summary>
    public double? DoubleValue => ValueKind == StaticFieldV2RuntimeValueKind.Double && FloatingBitPattern is { } bits
        ? BitConverter.UInt64BitsToDouble(bits)
        : null;

    /// <summary>Gets whether this exact draft answer decoded a present nullable payload.</summary>
    public bool HasNullableValue => ValueKind == StaticFieldV2RuntimeValueKind.NullablePresent;

    /// <summary>Gets the capability-call draft ledger proving this decoding performed no capability call.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the declared draft width in bytes the closed type required, or zero when none applies.</summary>
    public int DeclaredWidth { get; }

    /// <summary>Gets the supplied copied byte count observed by this draft decoding.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two runtime draft value outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeValueOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests runtime draft value outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeValueOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2RuntimeValueOutcome IssueExact(
        StaticFieldV2RuntimeValueRequest request,
        StaticFieldV2RuntimeValueKind valueKind,
        MetadataPrimitiveTypeKind? payloadKind,
        long? signedValue,
        ulong? unsignedValue,
        ulong? floatingBitPattern,
        ulong? referenceAddress,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        int declaredWidth) =>
        new(
            StaticFieldV2RuntimeValueResultKind.Exact,
            StaticFieldV2RuntimeValueIssue.None,
            request,
            valueKind,
            payloadKind,
            signedValue,
            unsignedValue,
            floatingBitPattern,
            referenceAddress,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            declaredCoverageBoundaries,
            declaredWidth,
            request.RawByteCount);

    internal static StaticFieldV2RuntimeValueOutcome IssueStop(
        StaticFieldV2RuntimeValueResultKind resultKind,
        StaticFieldV2RuntimeValueIssue issue,
        StaticFieldV2RuntimeValueRequest request,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> declaredCoverageBoundaries,
        int declaredWidth) =>
        new(
            resultKind,
            issue,
            request,
            null,
            null,
            null,
            null,
            null,
            null,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            declaredCoverageBoundaries,
            declaredWidth,
            request.RawByteCount);
}

/// <summary>Selects one exact runtime construction and its static slot from caller-supplied physical draft evidence.</summary>
/// <remarks>
/// This draft binder owns the two frozen W8.1 rules that map one bound metadata construction onto physical storage:
/// exactly one complete runtime construction identity must match, and the selected slot must satisfy every per-strategy
/// requirement while violating no per-strategy rejection.
/// <para>
/// Acquisition obeys the frozen capability-requirement vector executably. A capability marked
/// <see cref="StaticFieldV2CapabilityRequirement.NotRequired"/> is never routed through the caller's probe set, so a
/// poisoned probe proves non-invocation rather than merely documenting it. A metadata literal short-circuits before
/// every probe, and a module RVA never touches the construction or slot probes.
/// </para>
/// <para>
/// Declared coverage boundaries of this draft slice: this slice opens no dump, so every candidate, selected thread, and
/// mapped image geometry is caller-supplied physical evidence, and the counted raw read that produces value bytes is
/// performed by the caller rather than here.
/// </para>
/// </remarks>
public static class StaticFieldV2RuntimeConstructionBinder
{
    /// <summary>Selects the single exact runtime construction matching one bound metadata draft construction.</summary>
    /// <param name="request">The complete constructed-runtime draft selection request.</param>
    /// <remarks>
    /// Every supplied candidate is grouped by its complete draft construction identity. A group matches only when its
    /// definition module, TypeDef token, and ordered closed arguments all agree with the bound metadata construction;
    /// exactly one matching group is exact, none is absent, and two or more are ambiguous. The grouping is keyed by
    /// canonical digest, so presenting the same candidates in any order yields the identical draft answer.
    /// </remarks>
    /// <returns>A sealed immutable draft selection that is either one construction or one prefix-free stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2RuntimeConstructionSelection SelectConstruction(
        StaticFieldV2RuntimeConstructionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = ImmutableArray.Create(
            StaticFieldV2RuntimeCoverageBoundary.RuntimeConstructionEvidenceSuppliedByCaller);
        var probes = request.CapabilityProbes;
        if (request.StorageStrategy.CapabilityRequirements.RuntimeConstruction ==
            StaticFieldV2CapabilityRequirement.NotRequired)
        {
            return StaticFieldV2RuntimeConstructionSelection.IssueStop(
                StaticFieldV2RuntimeConstructionSelectionKind.NotRequired,
                StaticFieldV2RuntimeConstructionIssue.ConstructionNotRequiredForStrategy,
                request,
                StaticFieldV2LiteralValueOutcome.IssueLedger(probes),
                boundaries,
                null,
                0,
                0,
                0,
                StaticFieldV2RuntimeConstructionSelection.ConstructionNotRequiredCode);
        }

        var candidates = request.CandidatesCore;
        if (candidates.Length > StaticFieldV2Limits.MaximumRuntimeConstructionCount)
        {
            return StaticFieldV2RuntimeConstructionSelection.IssueStop(
                StaticFieldV2RuntimeConstructionSelectionKind.NonExact,
                StaticFieldV2RuntimeConstructionIssue.CandidateCountBoundReached,
                request,
                StaticFieldV2LiteralValueOutcome.IssueLedger(probes),
                boundaries,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.RuntimeConstructionCountBoundName,
                    StaticFieldV2Limits.MaximumRuntimeConstructionCount),
                0,
                0,
                StaticFieldV2Limits.MaximumRuntimeConstructionCount + 1,
                StaticFieldV2RuntimeConstructionSelection.ConstructionCountBoundReachedCode);
        }

        probes?.Invoke(StaticFieldV2StorageCapability.RuntimeConstruction);

        var groups = new SortedDictionary<string, StaticFieldV2RuntimeConstructionCandidate>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            groups.TryAdd(candidate.Sha256, candidate);
        }

        var owner = request.MetadataConstruction.OwnerConstruction!;
        var classification = owner.FinalClassification!;
        var definitionModule = classification.SourceModule;
        var typeDefinitionToken = classification.TypeDefinition.TypeDefinitionToken;
        var metadataArguments = owner.FlattenedArguments;

        StaticFieldV2RuntimeConstructionCandidate? matched = null;
        var matchingCount = 0;
        foreach (var candidate in groups.Values)
        {
            if (!Matches(candidate, definitionModule, typeDefinitionToken, metadataArguments))
            {
                continue;
            }
            matchingCount++;
            matched ??= candidate;
        }

        if (matchingCount == 0)
        {
            return StaticFieldV2RuntimeConstructionSelection.IssueStop(
                StaticFieldV2RuntimeConstructionSelectionKind.Absent,
                StaticFieldV2RuntimeConstructionIssue.ConstructionAbsent,
                request,
                StaticFieldV2LiteralValueOutcome.IssueLedger(probes),
                boundaries,
                null,
                groups.Count,
                0,
                groups.Count,
                StaticFieldV2RuntimeConstructionSelection.ConstructionAbsentCode);
        }
        if (matchingCount > 1)
        {
            return StaticFieldV2RuntimeConstructionSelection.IssueStop(
                StaticFieldV2RuntimeConstructionSelectionKind.Ambiguous,
                StaticFieldV2RuntimeConstructionIssue.ConstructionAmbiguous,
                request,
                StaticFieldV2LiteralValueOutcome.IssueLedger(probes),
                boundaries,
                null,
                groups.Count,
                matchingCount,
                matchingCount,
                StaticFieldV2RuntimeConstructionSelection.ConstructionAmbiguousCode);
        }

        return StaticFieldV2RuntimeConstructionSelection.IssueExact(
            request,
            matched!,
            StaticFieldV2LiteralValueOutcome.IssueLedger(probes),
            boundaries,
            groups.Count);
    }

    /// <summary>Acquires the single exact static slot of one classified draft storage strategy.</summary>
    /// <param name="request">The complete static-slot draft acquisition request.</param>
    /// <remarks>
    /// A constructed slot requires one exact construction selection and a slot address and rejects a selected thread; a
    /// thread-relative slot requires both plus a slot address; a module RVA requires complete module content, FieldRVA
    /// row, and mapped geometry while rejecting any construction selection or slot address; and a metadata literal
    /// produces no slot at all and performs no capability call whatsoever.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is either one slot or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2StaticSlotOutcome AcquireStaticSlot(StaticFieldV2StaticSlotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var strategy = request.StorageStrategy.Strategy!.Value;
        var boundaries = SlotBoundaries(strategy);
        var probes = request.CapabilityProbes;
        if (strategy == StaticFieldV2StorageStrategy.MetadataLiteral)
        {
            return Stop(
                StaticFieldV2StaticSlotResultKind.Unsupported,
                StaticFieldV2StaticSlotIssue.MetadataLiteralHasNoSlot,
                request,
                boundaries,
                StaticFieldV2StaticSlotOutcome.MetadataLiteralHasNoSlotCode);
        }

        var hasModuleGeometry =
            request.ModuleContent is not null ||
            request.FieldRvaRowToken is not null ||
            request.MappedRelativeVirtualAddress is not null ||
            request.MappedAddress is not null;
        if (strategy == StaticFieldV2StorageStrategy.ModuleRva)
        {
            if (request.ConstructionSelection is not null)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.ConstructionSelectionNotPermitted,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
            if (request.SelectedThread is not null)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.ThreadIdentityNotPermitted,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
            if (request.SlotAddress is not null)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.SlotAddressNotPermitted,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
            if (request.ModuleContent is null ||
                request.FieldRvaRowToken is null ||
                request.MappedRelativeVirtualAddress is null ||
                request.MappedAddress is null)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.ModuleGeometryRequired,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
        }
        else
        {
            if (hasModuleGeometry)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.ModuleGeometryNotPermitted,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
            if (strategy == StaticFieldV2StorageStrategy.ConstructedSlot && request.SelectedThread is not null)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.ThreadIdentityNotPermitted,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
            if (request.ConstructionSelection is not { } selection ||
                selection.ResultKind != StaticFieldV2RuntimeConstructionSelectionKind.Exact)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.ConstructionSelectionRequired,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
            if (strategy == StaticFieldV2StorageStrategy.ThreadRelativeSlot && request.SelectedThread is null)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.ThreadIdentityRequired,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
            if (request.SlotAddress is null)
            {
                return Stop(
                    StaticFieldV2StaticSlotResultKind.Invalid,
                    StaticFieldV2StaticSlotIssue.SlotAddressRequired,
                    request,
                    boundaries,
                    StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode);
            }
        }

        var vector = request.StorageStrategy.CapabilityRequirements;
        if (vector.ThreadIdentity == StaticFieldV2CapabilityRequirement.Required)
        {
            probes?.Invoke(StaticFieldV2StorageCapability.ThreadIdentity);
        }
        if (vector.ModuleContent == StaticFieldV2CapabilityRequirement.Required)
        {
            probes?.Invoke(StaticFieldV2StorageCapability.ModuleContent);
        }
        if (vector.StaticSlotAcquisition == StaticFieldV2CapabilityRequirement.Required)
        {
            probes?.Invoke(StaticFieldV2StorageCapability.StaticSlotAcquisition);
        }

        return StaticFieldV2StaticSlotOutcome.IssueExact(
            request,
            StaticFieldV2StaticSlotOutcome.IssueSlot(request, strategy),
            StaticFieldV2LiteralValueOutcome.IssueLedger(probes),
            boundaries);
    }

    private static bool Matches(
        StaticFieldV2RuntimeConstructionCandidate candidate,
        StaticFieldMetadataModuleIdentity definitionModule,
        int typeDefinitionToken,
        ImmutableArray<MetadataClosedTypeIdentity> metadataArguments)
    {
        if (candidate.TypeDefinitionToken != typeDefinitionToken ||
            !candidate.DefinitionModule.Equals(definitionModule))
        {
            return false;
        }

        var candidateArguments = candidate.ClosedArgumentsCore;
        if (candidateArguments.Length != metadataArguments.Length)
        {
            return false;
        }
        for (var index = 0; index < candidateArguments.Length; index++)
        {
            if (!candidateArguments[index].Equals(metadataArguments[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> SlotBoundaries(
        StaticFieldV2StorageStrategy strategy) => strategy switch
        {
            StaticFieldV2StorageStrategy.ConstructedSlot =>
                [StaticFieldV2RuntimeCoverageBoundary.RuntimeConstructionEvidenceSuppliedByCaller],
            StaticFieldV2StorageStrategy.ThreadRelativeSlot =>
            [
                StaticFieldV2RuntimeCoverageBoundary.RuntimeConstructionEvidenceSuppliedByCaller,
                StaticFieldV2RuntimeCoverageBoundary.SelectedThreadEvidenceSuppliedByCaller,
            ],
            StaticFieldV2StorageStrategy.ModuleRva =>
                [StaticFieldV2RuntimeCoverageBoundary.ModuleRvaGeometrySuppliedByCaller],
            _ => [],
        };

    private static StaticFieldV2StaticSlotOutcome Stop(
        StaticFieldV2StaticSlotResultKind resultKind,
        StaticFieldV2StaticSlotIssue issue,
        StaticFieldV2StaticSlotRequest request,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> boundaries,
        string diagnosticCode) =>
        StaticFieldV2StaticSlotOutcome.IssueStop(
            resultKind,
            issue,
            request,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            boundaries,
            diagnosticCode);
}

/// <summary>Decodes one exact static-field draft value from copied raw bytes and the field's closed type.</summary>
/// <remarks>
/// Every address-backed W8.1 value is decoded from copied raw bytes; a high-level runtime read is a late oracle only
/// and never appears here. This draft decoder therefore consults no capability at all, which the retained draft ledger
/// records, and it never assumes a pointer size: the target width is a supplied fact.
/// <para>
/// The admitted draft shapes are every CLI fixed-width integer with exact signedness and width, <c>bool</c>,
/// <c>char</c>, single and double from exact bit patterns, target-width native integers, an enum's underlying value,
/// both nullable forms over a supplied exact layout, the exact all-zero null reference, a non-null object reference
/// whose target address is retained undereferenced, and a string reference. A named non-enum value type, a pointer,
/// and every other topology are typed non-admissions rather than silent guesses.
/// </para>
/// </remarks>
public static class StaticFieldV2ValueDecoder
{
    /// <summary>Decodes one static-field draft value purely from the supplied copied bytes.</summary>
    /// <param name="request">The complete static-field runtime draft value decoding request.</param>
    /// <remarks>
    /// The declared width is derived from the closed draft type alone; a supplied byte count that disagrees with it is
    /// invalid. Floating values are reinterpreted from their exact bit patterns and never from text, and a managed
    /// reference retains its target address without any dereference.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is either one decoded value or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2RuntimeValueOutcome DecodeValue(StaticFieldV2RuntimeValueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = ImmutableArray.Create(
            StaticFieldV2RuntimeCoverageBoundary.RawValueBytesCopiedByCaller,
            StaticFieldV2RuntimeCoverageBoundary.TargetPointerWidthSuppliedByCaller);
        var declaredType = request.DeclaredType;
        return declaredType.Kind switch
        {
            MetadataClosedTypeKind.Primitive => DecodePrimitive(
                request,
                declaredType.PrimitiveKind!.Value,
                boundaries,
                request.RawBytesCore,
                enumUnderlying: false),
            MetadataClosedTypeKind.SzArray or MetadataClosedTypeKind.MultidimensionalArray =>
                DecodeReference(request, boundaries, request.RawBytesCore, isString: false),
            MetadataClosedTypeKind.Named => DecodeNamed(request, boundaries),
            _ => DecodeNullable(request, boundaries),
        };
    }

    private static StaticFieldV2RuntimeValueOutcome DecodeNamed(
        StaticFieldV2RuntimeValueRequest request,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> boundaries)
    {
        var role = request.DeclaredType.FinalClassification?.Role;
        if (role is MetadataTypeDefinitionSemanticRole.Class or
            MetadataTypeDefinitionSemanticRole.Interface or
            MetadataTypeDefinitionSemanticRole.Delegate)
        {
            return DecodeReference(request, boundaries, request.RawBytesCore, isString: false);
        }
        if (role != MetadataTypeDefinitionSemanticRole.Enum)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Unsupported,
                StaticFieldV2RuntimeValueIssue.UnsupportedValueShape,
                request,
                boundaries,
                0);
        }

        var enumBoundaries = Add(boundaries, StaticFieldV2RuntimeCoverageBoundary.EnumUnderlyingKindSuppliedByCaller);
        if (request.EnumUnderlyingKind is not { } underlying || !IsIntegralPrimitive(underlying))
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Unsupported,
                StaticFieldV2RuntimeValueIssue.EnumUnderlyingEvidenceUnavailable,
                request,
                enumBoundaries,
                0);
        }
        return DecodePrimitive(request, underlying, enumBoundaries, request.RawBytesCore, enumUnderlying: true);
    }

    private static StaticFieldV2RuntimeValueOutcome DecodeNullable(
        StaticFieldV2RuntimeValueRequest request,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> boundaries)
    {
        var nullableBoundaries = Add(boundaries, StaticFieldV2RuntimeCoverageBoundary.NullableLayoutSuppliedByCaller);
        var element = request.DeclaredType.ElementType!;
        var elementRole = element.FinalClassification?.Role;
        MetadataPrimitiveTypeKind payloadKind;
        if (element.Kind == MetadataClosedTypeKind.Primitive &&
            IsIntegralOrFloatingPrimitive(element.PrimitiveKind!.Value))
        {
            payloadKind = element.PrimitiveKind!.Value;
        }
        else if (element.Kind == MetadataClosedTypeKind.Named &&
                 elementRole == MetadataTypeDefinitionSemanticRole.Enum)
        {
            nullableBoundaries = Add(
                nullableBoundaries,
                StaticFieldV2RuntimeCoverageBoundary.EnumUnderlyingKindSuppliedByCaller);
            if (request.EnumUnderlyingKind is not { } underlying || !IsIntegralPrimitive(underlying))
            {
                return StaticFieldV2RuntimeValueOutcome.IssueStop(
                    StaticFieldV2RuntimeValueResultKind.Unsupported,
                    StaticFieldV2RuntimeValueIssue.EnumUnderlyingEvidenceUnavailable,
                    request,
                    nullableBoundaries,
                    0);
            }
            payloadKind = underlying;
        }
        else
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Unsupported,
                StaticFieldV2RuntimeValueIssue.UnsupportedValueShape,
                request,
                nullableBoundaries,
                0);
        }

        if (request.NullableLayout is not { } layout)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Unsupported,
                StaticFieldV2RuntimeValueIssue.NullableLayoutEvidenceUnavailable,
                request,
                nullableBoundaries,
                0);
        }

        var payloadWidth = PrimitiveWidth(payloadKind, request.TargetPointerWidth);
        if (request.RawByteCount != layout.StorageByteCount)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Invalid,
                StaticFieldV2RuntimeValueIssue.RawByteCountDisagreesWithDeclaredWidth,
                request,
                nullableBoundaries,
                layout.StorageByteCount);
        }
        if (layout.ValueByteCount != payloadWidth)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Invalid,
                StaticFieldV2RuntimeValueIssue.NullableLayoutInvalid,
                request,
                nullableBoundaries,
                layout.StorageByteCount);
        }

        var bytes = request.RawBytesCore.AsSpan();
        var flag = bytes[layout.HasValueOffset];
        if (flag > 1)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Invalid,
                StaticFieldV2RuntimeValueIssue.FlagEncodingInvalid,
                request,
                nullableBoundaries,
                layout.StorageByteCount);
        }
        if (flag == 0)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueExact(
                request,
                StaticFieldV2RuntimeValueKind.NullableAbsent,
                payloadKind,
                null,
                null,
                null,
                null,
                nullableBoundaries,
                layout.StorageByteCount);
        }

        var payload = bytes.Slice(layout.ValueOffset, layout.ValueByteCount);
        DecodeIntegralOrFloating(
            payloadKind,
            payload,
            out var signedValue,
            out var unsignedValue,
            out var floatingBitPattern);
        return StaticFieldV2RuntimeValueOutcome.IssueExact(
            request,
            StaticFieldV2RuntimeValueKind.NullablePresent,
            payloadKind,
            signedValue,
            unsignedValue,
            floatingBitPattern,
            null,
            nullableBoundaries,
            layout.StorageByteCount);
    }

    private static StaticFieldV2RuntimeValueOutcome DecodePrimitive(
        StaticFieldV2RuntimeValueRequest request,
        MetadataPrimitiveTypeKind primitiveKind,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> boundaries,
        ImmutableArray<byte> rawBytes,
        bool enumUnderlying)
    {
        if (primitiveKind is MetadataPrimitiveTypeKind.String or MetadataPrimitiveTypeKind.Object)
        {
            return DecodeReference(
                request,
                boundaries,
                rawBytes,
                primitiveKind == MetadataPrimitiveTypeKind.String);
        }

        var declaredWidth = PrimitiveWidth(primitiveKind, request.TargetPointerWidth);
        if (rawBytes.Length != declaredWidth)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Invalid,
                StaticFieldV2RuntimeValueIssue.RawByteCountDisagreesWithDeclaredWidth,
                request,
                boundaries,
                declaredWidth);
        }

        var bytes = rawBytes.AsSpan();
        if (primitiveKind == MetadataPrimitiveTypeKind.Boolean && bytes[0] > 1)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Invalid,
                StaticFieldV2RuntimeValueIssue.FlagEncodingInvalid,
                request,
                boundaries,
                declaredWidth);
        }

        DecodeIntegralOrFloating(
            primitiveKind,
            bytes,
            out var signedValue,
            out var unsignedValue,
            out var floatingBitPattern);
        return StaticFieldV2RuntimeValueOutcome.IssueExact(
            request,
            enumUnderlying ? StaticFieldV2RuntimeValueKind.EnumUnderlying : ValueKind(primitiveKind),
            enumUnderlying ? primitiveKind : null,
            signedValue,
            unsignedValue,
            floatingBitPattern,
            null,
            boundaries,
            declaredWidth);
    }

    private static StaticFieldV2RuntimeValueOutcome DecodeReference(
        StaticFieldV2RuntimeValueRequest request,
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> boundaries,
        ImmutableArray<byte> rawBytes,
        bool isString)
    {
        var declaredWidth = request.TargetPointerWidth;
        if (rawBytes.Length != declaredWidth)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueStop(
                StaticFieldV2RuntimeValueResultKind.Invalid,
                StaticFieldV2RuntimeValueIssue.RawByteCountDisagreesWithDeclaredWidth,
                request,
                boundaries,
                declaredWidth);
        }

        var bytes = rawBytes.AsSpan();
        var address = declaredWidth == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        if (address == 0)
        {
            return StaticFieldV2RuntimeValueOutcome.IssueExact(
                request,
                StaticFieldV2RuntimeValueKind.NullReference,
                null,
                null,
                null,
                null,
                null,
                boundaries,
                declaredWidth);
        }
        return StaticFieldV2RuntimeValueOutcome.IssueExact(
            request,
            isString
                ? StaticFieldV2RuntimeValueKind.StringReference
                : StaticFieldV2RuntimeValueKind.ObjectReference,
            null,
            null,
            null,
            null,
            address,
            boundaries,
            declaredWidth);
    }

    private static void DecodeIntegralOrFloating(
        MetadataPrimitiveTypeKind primitiveKind,
        ReadOnlySpan<byte> bytes,
        out long? signedValue,
        out ulong? unsignedValue,
        out ulong? floatingBitPattern)
    {
        signedValue = null;
        unsignedValue = null;
        floatingBitPattern = null;
        switch (primitiveKind)
        {
            case MetadataPrimitiveTypeKind.Boolean:
            case MetadataPrimitiveTypeKind.UInt8:
                unsignedValue = bytes[0];
                return;
            case MetadataPrimitiveTypeKind.Int8:
                signedValue = (sbyte)bytes[0];
                return;
            case MetadataPrimitiveTypeKind.Char:
            case MetadataPrimitiveTypeKind.UInt16:
                unsignedValue = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.Int16:
                signedValue = BinaryPrimitives.ReadInt16LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.UInt32:
                unsignedValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.Int32:
                signedValue = BinaryPrimitives.ReadInt32LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.UInt64:
                unsignedValue = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.Int64:
                signedValue = BinaryPrimitives.ReadInt64LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.Single:
                floatingBitPattern = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.Double:
                floatingBitPattern = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
                return;
            case MetadataPrimitiveTypeKind.NativeUInt:
                unsignedValue = bytes.Length == sizeof(uint)
                    ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
                    : BinaryPrimitives.ReadUInt64LittleEndian(bytes);
                return;
            default:
                signedValue = bytes.Length == sizeof(uint)
                    ? BinaryPrimitives.ReadInt32LittleEndian(bytes)
                    : BinaryPrimitives.ReadInt64LittleEndian(bytes);
                return;
        }
    }

    private static StaticFieldV2RuntimeValueKind ValueKind(MetadataPrimitiveTypeKind primitiveKind) =>
        primitiveKind switch
        {
            MetadataPrimitiveTypeKind.Boolean => StaticFieldV2RuntimeValueKind.Boolean,
            MetadataPrimitiveTypeKind.Char => StaticFieldV2RuntimeValueKind.Char,
            MetadataPrimitiveTypeKind.Int8 => StaticFieldV2RuntimeValueKind.Int8,
            MetadataPrimitiveTypeKind.UInt8 => StaticFieldV2RuntimeValueKind.UInt8,
            MetadataPrimitiveTypeKind.Int16 => StaticFieldV2RuntimeValueKind.Int16,
            MetadataPrimitiveTypeKind.UInt16 => StaticFieldV2RuntimeValueKind.UInt16,
            MetadataPrimitiveTypeKind.Int32 => StaticFieldV2RuntimeValueKind.Int32,
            MetadataPrimitiveTypeKind.UInt32 => StaticFieldV2RuntimeValueKind.UInt32,
            MetadataPrimitiveTypeKind.Int64 => StaticFieldV2RuntimeValueKind.Int64,
            MetadataPrimitiveTypeKind.UInt64 => StaticFieldV2RuntimeValueKind.UInt64,
            MetadataPrimitiveTypeKind.Single => StaticFieldV2RuntimeValueKind.Single,
            MetadataPrimitiveTypeKind.Double => StaticFieldV2RuntimeValueKind.Double,
            MetadataPrimitiveTypeKind.NativeInt => StaticFieldV2RuntimeValueKind.NativeInt,
            _ => StaticFieldV2RuntimeValueKind.NativeUnsignedInt,
        };

    private static int PrimitiveWidth(MetadataPrimitiveTypeKind primitiveKind, int targetPointerWidth) =>
        primitiveKind switch
        {
            MetadataPrimitiveTypeKind.Boolean or
                MetadataPrimitiveTypeKind.Int8 or
                MetadataPrimitiveTypeKind.UInt8 => 1,
            MetadataPrimitiveTypeKind.Char or
                MetadataPrimitiveTypeKind.Int16 or
                MetadataPrimitiveTypeKind.UInt16 => 2,
            MetadataPrimitiveTypeKind.Int32 or
                MetadataPrimitiveTypeKind.UInt32 or
                MetadataPrimitiveTypeKind.Single => 4,
            MetadataPrimitiveTypeKind.NativeInt or
                MetadataPrimitiveTypeKind.NativeUInt => targetPointerWidth,
            _ => 8,
        };

    private static bool IsIntegralPrimitive(MetadataPrimitiveTypeKind primitiveKind) =>
        primitiveKind is MetadataPrimitiveTypeKind.Boolean or
            MetadataPrimitiveTypeKind.Char or
            MetadataPrimitiveTypeKind.Int8 or
            MetadataPrimitiveTypeKind.UInt8 or
            MetadataPrimitiveTypeKind.Int16 or
            MetadataPrimitiveTypeKind.UInt16 or
            MetadataPrimitiveTypeKind.Int32 or
            MetadataPrimitiveTypeKind.UInt32 or
            MetadataPrimitiveTypeKind.Int64 or
            MetadataPrimitiveTypeKind.UInt64 or
            MetadataPrimitiveTypeKind.NativeInt or
            MetadataPrimitiveTypeKind.NativeUInt;

    private static bool IsIntegralOrFloatingPrimitive(MetadataPrimitiveTypeKind primitiveKind) =>
        IsIntegralPrimitive(primitiveKind) ||
        primitiveKind is MetadataPrimitiveTypeKind.Single or MetadataPrimitiveTypeKind.Double;

    private static ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> Add(
        ImmutableArray<StaticFieldV2RuntimeCoverageBoundary> boundaries,
        StaticFieldV2RuntimeCoverageBoundary boundary)
    {
        if (boundaries.Contains(boundary))
        {
            return boundaries;
        }

        var extended = new List<StaticFieldV2RuntimeCoverageBoundary>(boundaries) { boundary };
        extended.Sort();
        return [.. extended];
    }
}
