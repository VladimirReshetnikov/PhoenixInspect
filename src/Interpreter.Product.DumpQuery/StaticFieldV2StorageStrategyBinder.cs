using System.Buffers.Binary;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Freezes the public storage discriminator of one selected static-field draft declaration.</summary>
/// <remarks>
/// The draft discriminator contains exactly the four storage branches W8.1 admitted for a static field. Context-relative
/// storage is non-admitted: the pinned runtime exposes one ordinary static slot and no attributable context identity, so
/// no <c>ContextRelativeSlot</c> value exists here and a caller-supplied context marker becomes a typed non-admission
/// rather than a strategy.
/// </remarks>
public enum StaticFieldV2StorageStrategy
{
    /// <summary>The value lives in one exact runtime construction's static slot.</summary>
    ConstructedSlot = 1,

    /// <summary>The value lives in one exact selected thread's slot of one exact owner construction.</summary>
    ThreadRelativeSlot = 2,

    /// <summary>The value lives in module image bytes named by the physical FieldRVA row.</summary>
    ModuleRva = 3,

    /// <summary>The value lives entirely in metadata and is decoded from the physical Constant row.</summary>
    MetadataLiteral = 4,
}

/// <summary>Names one physical capability that a static-field draft storage strategy may require.</summary>
/// <remarks>The draft capability catalog is the vocabulary of the frozen per-strategy requirement vectors.</remarks>
public enum StaticFieldV2StorageCapability
{
    /// <summary>Acquisition of one exact runtime construction for the declaring type.</summary>
    RuntimeConstruction = 1,

    /// <summary>Acquisition of one exact selected-thread identity.</summary>
    ThreadIdentity = 2,

    /// <summary>Acquisition of exact module content, MVID, and mapped image geometry.</summary>
    ModuleContent = 3,

    /// <summary>Acquisition of one exact static storage slot address.</summary>
    StaticSlotAcquisition = 4,

    /// <summary>A counted raw read of copied dump bytes at an exact address.</summary>
    MemoryRead = 5,
}

/// <summary>Classifies whether one capability is required by a static-field draft storage strategy.</summary>
/// <remarks>The draft requirement is a frozen contract fact, not an observation of any performed call.</remarks>
public enum StaticFieldV2CapabilityRequirement
{
    /// <summary>The strategy provably never needs the capability.</summary>
    NotRequired = 1,

    /// <summary>The strategy cannot produce an exact value without the capability.</summary>
    Required = 2,
}

/// <summary>Classifies one static-field storage-strategy draft classification answer.</summary>
/// <remarks>
/// <see cref="Exact"/> is the only draft answer that exposes a strategy. <see cref="Unsupported"/> is a prefix-free
/// stop that exposes no strategy and marks every capability as not required.
/// </remarks>
public enum StaticFieldV2StorageStrategyResultKind
{
    /// <summary>Exactly one admitted storage strategy was classified from physical flags.</summary>
    Exact = 1,

    /// <summary>The declaration selects a route this draft discriminator does not admit.</summary>
    Unsupported = 2,
}

/// <summary>Identifies the deterministic issue of one storage-strategy draft classification.</summary>
/// <remarks>This draft issue catalog keeps the two non-admitted declarations distinct from every admitted branch.</remarks>
public enum StaticFieldV2StorageStrategyIssue
{
    /// <summary>No issue applies to an exact draft classification.</summary>
    None = 0,

    /// <summary>The supplied FieldDef row is an instance declaration rather than a static declaration.</summary>
    InstanceFieldNotStatic = 1,

    /// <summary>A caller-supplied context-relative marker names storage W8.1 did not admit.</summary>
    ContextRelativeStorageNotAdmitted = 2,
}

/// <summary>Identifies one declared coverage boundary retained by a static-field storage draft outcome.</summary>
/// <remarks>
/// Every boundary is an informational draft fact rather than an error. A boundary states what this metadata-only phase
/// deliberately does not model, so a consumer can never mistake a silent gap for a proven negative.
/// </remarks>
public enum StaticFieldV2StorageCoverageBoundary
{
    /// <summary>The physical CustomAttribute table is not modeled by this draft slice.</summary>
    CustomAttributeTableNotModeled = 1,

    /// <summary>The thread-relative marker was a caller-supplied decoded fact rather than a decoded row.</summary>
    ThreadStaticAttributeSuppliedByCaller = 2,

    /// <summary>The context-relative marker was a caller-supplied decoded fact rather than a decoded row.</summary>
    ContextStaticAttributeSuppliedByCaller = 3,

    /// <summary>The physical Constant row was supplied by the caller rather than decoded from a modeled table.</summary>
    ConstantTableSuppliedByCaller = 4,

    /// <summary>An enum underlying type was derived from the declared instance <c>value__</c> field alone.</summary>
    EnumUnderlyingDerivedFromInstanceValueField = 5,
}

/// <summary>Classifies one exact static-field metadata-literal draft value.</summary>
/// <remarks>The draft kind names the decoded Constant encoding; <c>decimal</c> deliberately has no member here.</remarks>
public enum StaticFieldV2LiteralValueKind
{
    /// <summary>One CLI <c>bool</c> encoded as a single zero or one byte.</summary>
    Boolean = 1,

    /// <summary>One CLI <c>char</c> encoded as two little-endian bytes.</summary>
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

    /// <summary>One UTF-16 string payload.</summary>
    String = 13,

    /// <summary>The exact ECMA null encoding: Constant type <c>CLASS</c> with an all-zero four-byte value.</summary>
    Null = 14,
}

/// <summary>Classifies one static-field metadata-literal draft projection answer.</summary>
/// <remarks>Only <see cref="Exact"/> exposes a decoded value; every other alternative is a prefix-free draft stop.</remarks>
public enum StaticFieldV2LiteralValueResultKind
{
    /// <summary>One admitted literal encoding was decoded exactly from metadata alone.</summary>
    Exact = 1,

    /// <summary>A declared draft bound or a missing prerequisite prevented an exact decode.</summary>
    NonExact = 2,

    /// <summary>Complete supplied evidence contradicted the signature or the Constant encoding.</summary>
    Invalid = 3,

    /// <summary>The declaration or encoding selects a route this metadata-only projector does not own.</summary>
    Unsupported = 4,
}

/// <summary>Identifies the deterministic issue of one metadata-literal draft projection.</summary>
/// <remarks>This draft issue catalog separates disagreement, malformed bytes, bounds, and non-admitted encodings.</remarks>
public enum StaticFieldV2LiteralValueIssue
{
    /// <summary>No issue applies to an exact draft projection.</summary>
    None = 0,

    /// <summary>The supplied FieldDef row does not carry FieldAttributes.Literal.</summary>
    FieldNotLiteral = 1,

    /// <summary>The complete FieldDef signature failed the shared bounded Core draft grammar.</summary>
    SignatureInvalid = 2,

    /// <summary>The signature type and the physical Constant type code name different types.</summary>
    LiteralTypeDisagreement = 3,

    /// <summary>The Constant value blob was malformed, truncated, or the wrong width for its type code.</summary>
    LiteralBlobInvalid = 4,

    /// <summary>The Constant type code or the signature form is not an admitted literal encoding.</summary>
    LiteralEncodingUnsupported = 5,

    /// <summary>The decoded string reached the declared static-string draft cap plus one.</summary>
    StringCharacterCountBoundReached = 6,

    /// <summary>No exact evidence established the named signature type's underlying literal encoding.</summary>
    NamedLiteralTypeEvidenceUnavailable = 7,

    /// <summary>The named value type is not an enum, so its constant is compiler-attribute encoded.</summary>
    AttributeEncodedLiteralNotModeled = 8,
}

/// <summary>Freezes the per-capability requirement vector of one static-field storage draft strategy.</summary>
/// <remarks>
/// The vector is minted only by <see cref="StaticFieldV2StorageStrategyOutcome"/>. It is the frozen contract the later
/// runtime draft slice must obey: a capability marked <see cref="StaticFieldV2CapabilityRequirement.NotRequired"/> may
/// never be acquired for that strategy, and a prefix-free stop marks every capability as not required.
/// </remarks>
public sealed class StaticFieldV2CapabilityRequirementVector :
    IEquatable<StaticFieldV2CapabilityRequirementVector>
{
    private const string CanonicalDomain = "static-field-v2-capability-requirement-vector";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2CapabilityRequirementVector(
        StaticFieldV2CapabilityRequirement runtimeConstruction,
        StaticFieldV2CapabilityRequirement threadIdentity,
        StaticFieldV2CapabilityRequirement moduleContent,
        StaticFieldV2CapabilityRequirement staticSlotAcquisition,
        StaticFieldV2CapabilityRequirement memoryRead)
    {
        RuntimeConstruction = runtimeConstruction;
        ThreadIdentity = threadIdentity;
        ModuleContent = moduleContent;
        StaticSlotAcquisition = staticSlotAcquisition;
        MemoryRead = memoryRead;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)runtimeConstruction);
        writer.WriteInt32((int)threadIdentity);
        writer.WriteInt32((int)moduleContent);
        writer.WriteInt32((int)staticSlotAcquisition);
        writer.WriteInt32((int)memoryRead);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the draft requirement for acquiring one exact runtime construction.</summary>
    public StaticFieldV2CapabilityRequirement RuntimeConstruction { get; }

    /// <summary>Gets the draft requirement for acquiring one exact selected-thread identity.</summary>
    public StaticFieldV2CapabilityRequirement ThreadIdentity { get; }

    /// <summary>Gets the draft requirement for acquiring exact module content and image geometry.</summary>
    public StaticFieldV2CapabilityRequirement ModuleContent { get; }

    /// <summary>Gets the draft requirement for acquiring one exact static storage slot address.</summary>
    public StaticFieldV2CapabilityRequirement StaticSlotAcquisition { get; }

    /// <summary>Gets the draft requirement for performing one counted raw memory read.</summary>
    public StaticFieldV2CapabilityRequirement MemoryRead { get; }

    /// <summary>Gets a defensive ascending copy of every capability this draft strategy requires.</summary>
    public ImmutableArray<StaticFieldV2StorageCapability> RequiredCapabilities
    {
        get
        {
            var required = ImmutableArray.CreateBuilder<StaticFieldV2StorageCapability>(5);
            foreach (var capability in AllCapabilities)
            {
                if (For(capability) == StaticFieldV2CapabilityRequirement.Required)
                {
                    required.Add(capability);
                }
            }
            return required.ToImmutable();
        }
    }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft vector bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft vector.</summary>
    public string Sha256 { get; }

    internal static ImmutableArray<StaticFieldV2StorageCapability> AllCapabilities =>
    [
        StaticFieldV2StorageCapability.RuntimeConstruction,
        StaticFieldV2StorageCapability.ThreadIdentity,
        StaticFieldV2StorageCapability.ModuleContent,
        StaticFieldV2StorageCapability.StaticSlotAcquisition,
        StaticFieldV2StorageCapability.MemoryRead,
    ];

    /// <summary>Projects the draft requirement of one named capability.</summary>
    /// <param name="capability">The capability whose draft requirement is requested.</param>
    /// <returns>The frozen draft requirement recorded for that capability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capability"/> is not a declared value.</exception>
    public StaticFieldV2CapabilityRequirement For(StaticFieldV2StorageCapability capability) => capability switch
    {
        StaticFieldV2StorageCapability.RuntimeConstruction => RuntimeConstruction,
        StaticFieldV2StorageCapability.ThreadIdentity => ThreadIdentity,
        StaticFieldV2StorageCapability.ModuleContent => ModuleContent,
        StaticFieldV2StorageCapability.StaticSlotAcquisition => StaticSlotAcquisition,
        StaticFieldV2StorageCapability.MemoryRead => MemoryRead,
        _ => throw new ArgumentOutOfRangeException(nameof(capability), "A declared draft capability is required."),
    };

    /// <summary>Tests canonical equality between two capability-requirement draft vectors.</summary>
    /// <param name="other">The other draft vector.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2CapabilityRequirementVector? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests capability-requirement draft vector equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a vector with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2CapabilityRequirementVector);

    /// <summary>Computes a deterministic hash code from immutable canonical draft vector content.</summary>
    /// <returns>A hash code for this canonical draft vector.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2CapabilityRequirementVector Create(
        object mintCapability,
        StaticFieldV2CapabilityRequirement runtimeConstruction,
        StaticFieldV2CapabilityRequirement threadIdentity,
        StaticFieldV2CapabilityRequirement moduleContent,
        StaticFieldV2CapabilityRequirement staticSlotAcquisition,
        StaticFieldV2CapabilityRequirement memoryRead)
    {
        if (!StaticFieldV2StorageStrategyOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A capability-requirement vector requires the strategy outcome's private mint capability.",
                nameof(mintCapability));
        }

        ExpressionV2ContractEncoding.RequireDefined(runtimeConstruction, nameof(runtimeConstruction));
        ExpressionV2ContractEncoding.RequireDefined(threadIdentity, nameof(threadIdentity));
        ExpressionV2ContractEncoding.RequireDefined(moduleContent, nameof(moduleContent));
        ExpressionV2ContractEncoding.RequireDefined(staticSlotAcquisition, nameof(staticSlotAcquisition));
        ExpressionV2ContractEncoding.RequireDefined(memoryRead, nameof(memoryRead));
        return new StaticFieldV2CapabilityRequirementVector(
            runtimeConstruction,
            threadIdentity,
            moduleContent,
            staticSlotAcquisition,
            memoryRead);
    }
}

/// <summary>Counts capability calls performed while producing one static-field draft value.</summary>
/// <remarks>
/// The ledger is minted only by <see cref="StaticFieldV2LiteralValueOutcome"/>. Metadata-literal projection performs no
/// runtime, thread, module-content, slot, or memory call, so every counter is zero. When the caller supplies a
/// <see cref="ExpressionV2CapabilityProbeSet"/>, the counters are that probe set's own accounting rather than a
/// constant, which makes the W8.1-frozen literal no-call rule executable instead of merely documented.
/// </remarks>
public sealed class StaticFieldV2CapabilityCallLedger : IEquatable<StaticFieldV2CapabilityCallLedger>
{
    private const string CanonicalDomain = "static-field-v2-capability-call-ledger";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2CapabilityCallLedger(
        int runtimeConstructionCallCount,
        int threadIdentityCallCount,
        int moduleContentCallCount,
        int staticSlotAcquisitionCallCount,
        int memoryReadCallCount)
    {
        RuntimeConstructionCallCount = runtimeConstructionCallCount;
        ThreadIdentityCallCount = threadIdentityCallCount;
        ModuleContentCallCount = moduleContentCallCount;
        StaticSlotAcquisitionCallCount = staticSlotAcquisitionCallCount;
        MemoryReadCallCount = memoryReadCallCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(runtimeConstructionCallCount);
        writer.WriteInt32(threadIdentityCallCount);
        writer.WriteInt32(moduleContentCallCount);
        writer.WriteInt32(staticSlotAcquisitionCallCount);
        writer.WriteInt32(memoryReadCallCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the counted runtime-construction acquisitions of this draft projection.</summary>
    public int RuntimeConstructionCallCount { get; }

    /// <summary>Gets the counted selected-thread acquisitions of this draft projection.</summary>
    public int ThreadIdentityCallCount { get; }

    /// <summary>Gets the counted module-content acquisitions of this draft projection.</summary>
    public int ModuleContentCallCount { get; }

    /// <summary>Gets the counted static-slot acquisitions of this draft projection.</summary>
    public int StaticSlotAcquisitionCallCount { get; }

    /// <summary>Gets the counted raw memory reads of this draft projection.</summary>
    public int MemoryReadCallCount { get; }

    /// <summary>Gets the summed counted capability calls of this draft projection.</summary>
    public int TotalCallCount =>
        RuntimeConstructionCallCount +
        ThreadIdentityCallCount +
        ModuleContentCallCount +
        StaticSlotAcquisitionCallCount +
        MemoryReadCallCount;

    /// <summary>Gets whether this draft projection performed no capability call at all.</summary>
    public bool IsZero => TotalCallCount == 0;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft ledger bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft ledger.</summary>
    public string Sha256 { get; }

    /// <summary>Projects the counted calls of one named capability.</summary>
    /// <param name="capability">The capability whose counted draft calls are requested.</param>
    /// <returns>The counted calls recorded for that capability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capability"/> is not a declared value.</exception>
    public int CallCount(StaticFieldV2StorageCapability capability) => capability switch
    {
        StaticFieldV2StorageCapability.RuntimeConstruction => RuntimeConstructionCallCount,
        StaticFieldV2StorageCapability.ThreadIdentity => ThreadIdentityCallCount,
        StaticFieldV2StorageCapability.ModuleContent => ModuleContentCallCount,
        StaticFieldV2StorageCapability.StaticSlotAcquisition => StaticSlotAcquisitionCallCount,
        StaticFieldV2StorageCapability.MemoryRead => MemoryReadCallCount,
        _ => throw new ArgumentOutOfRangeException(nameof(capability), "A declared draft capability is required."),
    };

    /// <summary>Tests canonical equality between two capability-call draft ledgers.</summary>
    /// <param name="other">The other draft ledger.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2CapabilityCallLedger? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests capability-call draft ledger equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a ledger with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2CapabilityCallLedger);

    /// <summary>Computes a deterministic hash code from immutable canonical draft ledger content.</summary>
    /// <returns>A hash code for this canonical draft ledger.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2CapabilityCallLedger Create(
        object mintCapability,
        int runtimeConstructionCallCount,
        int threadIdentityCallCount,
        int moduleContentCallCount,
        int staticSlotAcquisitionCallCount,
        int memoryReadCallCount)
    {
        if (!StaticFieldV2LiteralValueOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A capability-call ledger requires the literal outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(runtimeConstructionCallCount);
        ArgumentOutOfRangeException.ThrowIfNegative(threadIdentityCallCount);
        ArgumentOutOfRangeException.ThrowIfNegative(moduleContentCallCount);
        ArgumentOutOfRangeException.ThrowIfNegative(staticSlotAcquisitionCallCount);
        ArgumentOutOfRangeException.ThrowIfNegative(memoryReadCallCount);
        return new StaticFieldV2CapabilityCallLedger(
            runtimeConstructionCallCount,
            threadIdentityCallCount,
            moduleContentCallCount,
            staticSlotAcquisitionCallCount,
            memoryReadCallCount);
    }
}

/// <summary>Carries caller-owned capability probes that a draft projection would call if it needed them.</summary>
/// <remarks>
/// The probe set is a deliberately mutable accounting instrument and therefore takes no part in canonical draft
/// identity beyond its presence. Every call routed through <see cref="Invoke"/> increments the corresponding counter
/// before the caller-owned delegate runs, so a probe set whose delegates throw proves non-invocation executably.
/// </remarks>
public sealed class ExpressionV2CapabilityProbeSet
{
    private readonly Action? runtimeConstruction;
    private readonly Action? threadIdentity;
    private readonly Action? moduleContent;
    private readonly Action? staticSlotAcquisition;
    private readonly Action? memoryRead;
    private int runtimeConstructionCallCount;
    private int threadIdentityCallCount;
    private int moduleContentCallCount;
    private int staticSlotAcquisitionCallCount;
    private int memoryReadCallCount;

    private ExpressionV2CapabilityProbeSet(
        Action? runtimeConstruction,
        Action? threadIdentity,
        Action? moduleContent,
        Action? staticSlotAcquisition,
        Action? memoryRead)
    {
        this.runtimeConstruction = runtimeConstruction;
        this.threadIdentity = threadIdentity;
        this.moduleContent = moduleContent;
        this.staticSlotAcquisition = staticSlotAcquisition;
        this.memoryRead = memoryRead;
    }

    /// <summary>Gets the counted runtime-construction acquisitions routed through this draft probe set.</summary>
    public int RuntimeConstructionCallCount => runtimeConstructionCallCount;

    /// <summary>Gets the counted selected-thread acquisitions routed through this draft probe set.</summary>
    public int ThreadIdentityCallCount => threadIdentityCallCount;

    /// <summary>Gets the counted module-content acquisitions routed through this draft probe set.</summary>
    public int ModuleContentCallCount => moduleContentCallCount;

    /// <summary>Gets the counted static-slot acquisitions routed through this draft probe set.</summary>
    public int StaticSlotAcquisitionCallCount => staticSlotAcquisitionCallCount;

    /// <summary>Gets the counted raw memory reads routed through this draft probe set.</summary>
    public int MemoryReadCallCount => memoryReadCallCount;

    /// <summary>Gets the summed counted capability calls routed through this draft probe set.</summary>
    public int TotalCallCount =>
        runtimeConstructionCallCount +
        threadIdentityCallCount +
        moduleContentCallCount +
        staticSlotAcquisitionCallCount +
        memoryReadCallCount;

    /// <summary>Creates one caller-owned draft probe set with an optional delegate per capability.</summary>
    /// <param name="runtimeConstruction">The runtime-construction probe, or null for no delegate.</param>
    /// <param name="threadIdentity">The selected-thread probe, or null for no delegate.</param>
    /// <param name="moduleContent">The module-content probe, or null for no delegate.</param>
    /// <param name="staticSlotAcquisition">The static-slot probe, or null for no delegate.</param>
    /// <param name="memoryRead">The raw memory-read probe, or null for no delegate.</param>
    /// <returns>A draft probe set whose counters all start at zero.</returns>
    public static ExpressionV2CapabilityProbeSet Create(
        Action? runtimeConstruction = null,
        Action? threadIdentity = null,
        Action? moduleContent = null,
        Action? staticSlotAcquisition = null,
        Action? memoryRead = null) =>
        new(runtimeConstruction, threadIdentity, moduleContent, staticSlotAcquisition, memoryRead);

    /// <summary>Counts and performs one capability call of this draft probe set.</summary>
    /// <param name="capability">The capability being acquired.</param>
    /// <remarks>The counter advances before the caller-owned delegate runs, so a throwing probe is still counted.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capability"/> is not a declared value.</exception>
    public void Invoke(StaticFieldV2StorageCapability capability)
    {
        switch (capability)
        {
            case StaticFieldV2StorageCapability.RuntimeConstruction:
                runtimeConstructionCallCount++;
                runtimeConstruction?.Invoke();
                return;
            case StaticFieldV2StorageCapability.ThreadIdentity:
                threadIdentityCallCount++;
                threadIdentity?.Invoke();
                return;
            case StaticFieldV2StorageCapability.ModuleContent:
                moduleContentCallCount++;
                moduleContent?.Invoke();
                return;
            case StaticFieldV2StorageCapability.StaticSlotAcquisition:
                staticSlotAcquisitionCallCount++;
                staticSlotAcquisition?.Invoke();
                return;
            case StaticFieldV2StorageCapability.MemoryRead:
                memoryReadCallCount++;
                memoryRead?.Invoke();
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(capability),
                    "A declared draft capability is required.");
        }
    }

    /// <summary>Projects the counted calls of one named capability of this draft probe set.</summary>
    /// <param name="capability">The capability whose counted draft calls are requested.</param>
    /// <returns>The counted calls routed through this probe set for that capability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capability"/> is not a declared value.</exception>
    public int CallCount(StaticFieldV2StorageCapability capability) => capability switch
    {
        StaticFieldV2StorageCapability.RuntimeConstruction => runtimeConstructionCallCount,
        StaticFieldV2StorageCapability.ThreadIdentity => threadIdentityCallCount,
        StaticFieldV2StorageCapability.ModuleContent => moduleContentCallCount,
        StaticFieldV2StorageCapability.StaticSlotAcquisition => staticSlotAcquisitionCallCount,
        StaticFieldV2StorageCapability.MemoryRead => memoryReadCallCount,
        _ => throw new ArgumentOutOfRangeException(nameof(capability), "A declared draft capability is required."),
    };
}

/// <summary>Freezes one complete static-field storage-strategy draft classification request.</summary>
/// <remarks>
/// The request names one physical FieldDef row, its authority-issued declaring TypeDef, and the two caller-supplied
/// decoded storage markers. The physical CustomAttribute table is not modeled by this draft slice, so the
/// thread-relative and context-relative markers are explicit caller-declared boolean facts rather than decoded rows.
/// </remarks>
public sealed class StaticFieldV2StorageStrategyRequest : IEquatable<StaticFieldV2StorageStrategyRequest>
{
    private const string CanonicalDomain = "static-field-v2-storage-strategy-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StorageStrategyRequest(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        bool threadStaticAttributeSuppliedByCaller,
        bool contextStaticAttributeSuppliedByCaller)
    {
        FieldRow = fieldRow;
        DeclaringTypeDefinition = declaringTypeDefinition;
        ThreadStaticAttributeSuppliedByCaller = threadStaticAttributeSuppliedByCaller;
        ContextStaticAttributeSuppliedByCaller = contextStaticAttributeSuppliedByCaller;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(fieldRow.Sha256, nameof(fieldRow));
        writer.WriteSha256(declaringTypeDefinition.Sha256, nameof(declaringTypeDefinition));
        writer.WriteBoolean(threadStaticAttributeSuppliedByCaller);
        writer.WriteBoolean(contextStaticAttributeSuppliedByCaller);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact physical FieldDef draft row whose storage strategy is classified.</summary>
    public MetadataFieldDefinitionTableRowIdentity FieldRow { get; }

    /// <summary>Gets the authority-issued TypeDef that physically declared the draft row.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the caller-supplied decoded thread-relative custom-attribute presence draft fact.</summary>
    public bool ThreadStaticAttributeSuppliedByCaller { get; }

    /// <summary>Gets the caller-supplied decoded context-relative custom-attribute presence draft fact.</summary>
    public bool ContextStaticAttributeSuppliedByCaller { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete static-field storage-strategy draft classification request.</summary>
    /// <param name="fieldRow">The exact physical FieldDef row selected by definition-side member lookup.</param>
    /// <param name="declaringTypeDefinition">The authority-issued TypeDef that physically declared the row.</param>
    /// <param name="threadStaticAttributeSuppliedByCaller">
    /// The caller-decoded presence of the thread-relative marker custom attribute. The CustomAttribute table is not
    /// modeled here, so this is an explicit caller-declared fact and is retained as a declared coverage boundary.
    /// </param>
    /// <param name="contextStaticAttributeSuppliedByCaller">
    /// The caller-decoded presence of the context-relative marker custom attribute. W8.1 did not admit context-relative
    /// storage, so a true value produces a typed non-admission rather than any strategy.
    /// </param>
    /// <returns>A sealed immutable draft request.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentException">The declaring TypeDef is not the row's physical declaring type.</exception>
    public static StaticFieldV2StorageStrategyRequest Create(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        bool threadStaticAttributeSuppliedByCaller = false,
        bool contextStaticAttributeSuppliedByCaller = false)
    {
        ArgumentNullException.ThrowIfNull(fieldRow);
        ArgumentNullException.ThrowIfNull(declaringTypeDefinition);
        if (fieldRow.DeclaringTypeDefinitionToken != declaringTypeDefinition.TypeDefinitionToken)
        {
            throw new ArgumentException(
                "A storage-strategy request must retain the physical declaring TypeDef of its FieldDef row.",
                nameof(declaringTypeDefinition));
        }

        return new StaticFieldV2StorageStrategyRequest(
            fieldRow,
            declaringTypeDefinition,
            threadStaticAttributeSuppliedByCaller,
            contextStaticAttributeSuppliedByCaller);
    }

    /// <summary>Tests canonical equality between two storage-strategy draft requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2StorageStrategyRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests storage-strategy draft request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StorageStrategyRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete draft outcome of one static-field storage-strategy classification.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of the capability-requirement vector it retains. An exact answer names
/// exactly one admitted strategy and its frozen requirement vector; a typed non-admission names no strategy, marks
/// every capability as not required, and retains the frozen diagnostic code of the refused branch.
/// </remarks>
public sealed class StaticFieldV2StorageStrategyOutcome : IEquatable<StaticFieldV2StorageStrategyOutcome>
{
    /// <summary>Gets the frozen diagnostic code retained by a context-relative draft non-admission.</summary>
    public const string ContextIdentityNotAttributableCode = "W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE";

    /// <summary>Gets the frozen diagnostic code retained by an instance-declaration draft non-admission.</summary>
    public const string InstanceFieldNotStaticCode = "W8_STATIC_FIELD_INSTANCE_DECLARATION";

    private const string CanonicalDomain = "static-field-v2-storage-strategy-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StorageStrategyOutcome(
        StaticFieldV2StorageStrategyResultKind resultKind,
        StaticFieldV2StorageStrategyIssue issue,
        StaticFieldV2StorageStrategyRequest request,
        StaticFieldV2StorageStrategy? strategy,
        StaticFieldV2CapabilityRequirementVector capabilityRequirements,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries,
        string? diagnosticCode)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        Strategy = strategy;
        CapabilityRequirements = capabilityRequirements;
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        DiagnosticCode = diagnosticCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, strategy);
        writer.WriteSha256(capabilityRequirements.Sha256, nameof(capabilityRequirements));
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft classification is exact or a typed non-admission.</summary>
    public StaticFieldV2StorageStrategyResultKind ResultKind { get; }

    /// <summary>Gets the typed draft classification issue, or none for an exact outcome.</summary>
    public StaticFieldV2StorageStrategyIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2StorageStrategyRequest Request { get; }

    /// <summary>Gets the single admitted draft storage strategy, or null for a typed non-admission.</summary>
    public StaticFieldV2StorageStrategy? Strategy { get; }

    /// <summary>Gets the frozen per-capability requirement draft vector of this outcome.</summary>
    public StaticFieldV2CapabilityRequirementVector CapabilityRequirements { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2StorageCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the frozen diagnostic code of a typed non-admission, otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the exact physical FieldDef token this draft outcome classified.</summary>
    public int FieldDefinitionToken => Request.FieldRow.FieldDefinitionToken;

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two storage-strategy draft outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2StorageStrategyOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests storage-strategy draft outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StorageStrategyOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2CapabilityRequirementVector IssueVector(
        StaticFieldV2CapabilityRequirement runtimeConstruction,
        StaticFieldV2CapabilityRequirement threadIdentity,
        StaticFieldV2CapabilityRequirement moduleContent,
        StaticFieldV2CapabilityRequirement staticSlotAcquisition,
        StaticFieldV2CapabilityRequirement memoryRead) =>
        StaticFieldV2CapabilityRequirementVector.Create(
            RowMintCapability,
            runtimeConstruction,
            threadIdentity,
            moduleContent,
            staticSlotAcquisition,
            memoryRead);

    internal static StaticFieldV2StorageStrategyOutcome IssueExact(
        StaticFieldV2StorageStrategyRequest request,
        StaticFieldV2StorageStrategy strategy,
        StaticFieldV2CapabilityRequirementVector capabilityRequirements,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries) =>
        new(
            StaticFieldV2StorageStrategyResultKind.Exact,
            StaticFieldV2StorageStrategyIssue.None,
            request,
            strategy,
            capabilityRequirements,
            declaredCoverageBoundaries,
            null);

    internal static StaticFieldV2StorageStrategyOutcome IssueStop(
        StaticFieldV2StorageStrategyIssue issue,
        StaticFieldV2StorageStrategyRequest request,
        StaticFieldV2CapabilityRequirementVector capabilityRequirements,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries,
        string diagnosticCode)
    {
        ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        return new StaticFieldV2StorageStrategyOutcome(
            StaticFieldV2StorageStrategyResultKind.Unsupported,
            issue,
            request,
            null,
            capabilityRequirements,
            declaredCoverageBoundaries,
            diagnosticCode);
    }
}

/// <summary>Freezes one complete static-field metadata-literal draft projection request.</summary>
/// <remarks>
/// The request names one physical FieldDef row and the physical Constant row supplied as a type code plus a raw value
/// blob. The Constant table is not modeled by this draft slice, so the row is a caller-supplied physical fact retained
/// as a declared coverage boundary. An optional FieldDef catalog supplies the evidence needed to derive an enum's
/// underlying primitive from that enum's declared instance <c>value__</c> field.
/// </remarks>
public sealed class StaticFieldV2LiteralProjectionRequest : IEquatable<StaticFieldV2LiteralProjectionRequest>
{
    /// <summary>Gets the maximum admitted Constant value blob byte count of one draft projection request.</summary>
    public const int MaximumConstantValueByteCount = 2 * (StaticFieldV2Limits.MaximumStringCharacterCount + 1);

    private const string CanonicalDomain = "static-field-v2-literal-projection-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> constantValueBlob;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2LiteralProjectionRequest(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        int constantTypeCode,
        ImmutableArray<byte> constantValueBlob,
        MetadataFieldDefinitionTableCatalogIdentity? namedLiteralTypeCatalog,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        FieldRow = fieldRow;
        ConstantTypeCode = constantTypeCode;
        this.constantValueBlob = constantValueBlob;
        NamedLiteralTypeCatalog = namedLiteralTypeCatalog;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(fieldRow.Sha256, nameof(fieldRow));
        writer.WriteInt32(constantTypeCode);
        writer.WriteLengthPrefixedBytes(constantValueBlob.AsSpan());
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, namedLiteralTypeCatalog?.Sha256);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact physical FieldDef draft row whose literal value is projected.</summary>
    public MetadataFieldDefinitionTableRowIdentity FieldRow { get; }

    /// <summary>Gets the physical Constant table type code supplied for the draft row.</summary>
    public int ConstantTypeCode { get; }

    /// <summary>Gets a defensive copy of the raw physical Constant value blob of the draft row.</summary>
    public ImmutableArray<byte> ConstantValueBlob => ExpressionV2ContractEncoding.Copy(constantValueBlob);

    /// <summary>Gets the FieldDef catalog supplying named-type draft evidence, or null when none was supplied.</summary>
    public MetadataFieldDefinitionTableCatalogIdentity? NamedLiteralTypeCatalog { get; }

    /// <summary>Gets the caller-owned capability probes this draft projection must never invoke.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete static-field metadata-literal draft projection request.</summary>
    /// <param name="fieldRow">The exact physical FieldDef row selected by definition-side member lookup.</param>
    /// <param name="constantTypeCode">The physical Constant table type code, an unsigned eight-bit value.</param>
    /// <param name="constantValueBlob">The raw physical Constant value blob, bounded by the admitted byte count.</param>
    /// <param name="namedLiteralTypeCatalog">
    /// The FieldDef catalog of the module declaring a named signature type, used only to derive an enum's underlying
    /// primitive from its declared instance <c>value__</c> field.
    /// </param>
    /// <param name="capabilityProbes">
    /// Caller-owned probes that this metadata-only projection never invokes; their counters become the outcome ledger.
    /// </param>
    /// <returns>A sealed immutable draft request with a defensively copied Constant blob.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fieldRow"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The Constant type code is outside its physical byte column.</exception>
    /// <exception cref="ArgumentException">The Constant value blob is default or over the admitted byte count.</exception>
    public static StaticFieldV2LiteralProjectionRequest Create(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        int constantTypeCode,
        ImmutableArray<byte> constantValueBlob,
        MetadataFieldDefinitionTableCatalogIdentity? namedLiteralTypeCatalog = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(fieldRow);
        if (constantTypeCode is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(constantTypeCode),
                "The physical Constant type code must fit its unsigned eight-bit column.");
        }
        if (constantValueBlob.IsDefault || constantValueBlob.Length > MaximumConstantValueByteCount)
        {
            throw new ArgumentException(
                $"An initialized Constant value blob of at most {MaximumConstantValueByteCount} bytes is required.",
                nameof(constantValueBlob));
        }

        return new StaticFieldV2LiteralProjectionRequest(
            fieldRow,
            constantTypeCode,
            ExpressionV2ContractEncoding.Copy(constantValueBlob),
            namedLiteralTypeCatalog,
            capabilityProbes);
    }

    /// <summary>Tests canonical equality between two metadata-literal draft projection requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2LiteralProjectionRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests metadata-literal draft projection request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2LiteralProjectionRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<byte> ConstantValueBlobCore => constantValueBlob;
}

/// <summary>Freezes the complete draft outcome of one static-field metadata-literal projection.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of the capability-call ledger it retains. An exact answer decodes one
/// admitted Constant encoding from metadata alone and retains the enum definition whenever the signature named an enum;
/// every other alternative is a prefix-free stop that exposes no decoded value.
/// </remarks>
public sealed class StaticFieldV2LiteralValueOutcome : IEquatable<StaticFieldV2LiteralValueOutcome>
{
    /// <summary>Gets the shared static-string draft cap applied by one complete literal projection.</summary>
    public const int MaximumStringCharacterCount = StaticFieldV2Limits.MaximumStringCharacterCount;

    private const string CanonicalDomain = "static-field-v2-literal-value-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2LiteralValueOutcome(
        StaticFieldV2LiteralValueResultKind resultKind,
        StaticFieldV2LiteralValueIssue issue,
        StaticFieldV2LiteralProjectionRequest request,
        StaticFieldV2LiteralValueKind? valueKind,
        MetadataTypeDefinitionAuthorityIdentity? enumDefinition,
        long? signedValue,
        ulong? unsignedValue,
        ulong? floatingBitPattern,
        string? stringValue,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        ValueKind = valueKind;
        EnumDefinition = enumDefinition;
        SignedValue = signedValue;
        UnsignedValue = unsignedValue;
        FloatingBitPattern = floatingBitPattern;
        StringValue = stringValue;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, valueKind);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, enumDefinition?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalInt64(writer, signedValue);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, unsignedValue);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, floatingBitPattern);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, stringValue);
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft projection is exact, non-exact, invalid, or a typed non-admission.</summary>
    public StaticFieldV2LiteralValueResultKind ResultKind { get; }

    /// <summary>Gets the typed draft projection issue, or none for an exact outcome.</summary>
    public StaticFieldV2LiteralValueIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2LiteralProjectionRequest Request { get; }

    /// <summary>Gets the decoded draft literal kind, or null for every stop.</summary>
    public StaticFieldV2LiteralValueKind? ValueKind { get; }

    /// <summary>Gets the enum TypeDef named by the draft signature, or null when the signature named no enum.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? EnumDefinition { get; }

    /// <summary>Gets whether this exact draft value is one enum's decoded underlying value.</summary>
    public bool IsEnumUnderlyingValue => EnumDefinition is not null;

    /// <summary>Gets the decoded signed draft value for a signed integer kind, otherwise null.</summary>
    public long? SignedValue { get; }

    /// <summary>Gets the decoded unsigned draft value for an unsigned, char, or boolean kind, otherwise null.</summary>
    public ulong? UnsignedValue { get; }

    /// <summary>Gets the exact floating draft bit pattern for a single or double kind, otherwise null.</summary>
    public ulong? FloatingBitPattern { get; }

    /// <summary>Gets the decoded single-precision draft value reinterpreted from its exact bit pattern.</summary>
    public float? SingleValue => ValueKind == StaticFieldV2LiteralValueKind.Single && FloatingBitPattern is { } bits
        ? BitConverter.UInt32BitsToSingle((uint)bits)
        : null;

    /// <summary>Gets the decoded double-precision draft value reinterpreted from its exact bit pattern.</summary>
    public double? DoubleValue => ValueKind == StaticFieldV2LiteralValueKind.Double && FloatingBitPattern is { } bits
        ? BitConverter.UInt64BitsToDouble(bits)
        : null;

    /// <summary>Gets the decoded draft string payload for a string kind, otherwise null.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the capability-call draft ledger proving which capability calls this projection performed.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2StorageCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the decoded draft character count, supplied byte count, or cap-plus-one observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two metadata-literal draft outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2LiteralValueOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests metadata-literal draft outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2LiteralValueOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2CapabilityCallLedger IssueLedger(ExpressionV2CapabilityProbeSet? probes) =>
        StaticFieldV2CapabilityCallLedger.Create(
            RowMintCapability,
            probes?.RuntimeConstructionCallCount ?? 0,
            probes?.ThreadIdentityCallCount ?? 0,
            probes?.ModuleContentCallCount ?? 0,
            probes?.StaticSlotAcquisitionCallCount ?? 0,
            probes?.MemoryReadCallCount ?? 0);

    internal static StaticFieldV2LiteralValueOutcome IssueExact(
        StaticFieldV2LiteralProjectionRequest request,
        StaticFieldV2LiteralValueKind valueKind,
        MetadataTypeDefinitionAuthorityIdentity? enumDefinition,
        long? signedValue,
        ulong? unsignedValue,
        ulong? floatingBitPattern,
        string? stringValue,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries,
        int observedCount) =>
        new(
            StaticFieldV2LiteralValueResultKind.Exact,
            StaticFieldV2LiteralValueIssue.None,
            request,
            valueKind,
            enumDefinition,
            signedValue,
            unsignedValue,
            floatingBitPattern,
            stringValue,
            capabilityCallLedger,
            declaredCoverageBoundaries,
            null,
            observedCount);

    internal static StaticFieldV2LiteralValueOutcome IssueStop(
        StaticFieldV2LiteralValueResultKind resultKind,
        StaticFieldV2LiteralValueIssue issue,
        StaticFieldV2LiteralProjectionRequest request,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
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
            capabilityCallLedger,
            declaredCoverageBoundaries,
            reachedBound,
            observedCount);
}

/// <summary>Classifies static-field storage and projects exact metadata literals from definitions alone.</summary>
/// <remarks>
/// This draft binder owns the frozen public storage discriminator and the one storage branch that needs no address:
/// <see cref="StaticFieldV2StorageStrategy.MetadataLiteral"/>. The three address-backed branches are classified here
/// together with their frozen per-capability requirement facts, but their acquisition belongs to the later runtime
/// draft slice.
/// <para>
/// Declared coverage boundaries of this draft slice: the physical CustomAttribute table is not modeled, so the
/// thread-relative and context-relative markers are caller-supplied decoded facts; the physical Constant table is not
/// modeled, so the literal row is a caller-supplied physical fact; and an enum's underlying primitive is derived from
/// its declared instance <c>value__</c> field alone rather than from a verified <c>System.Enum</c> base chain.
/// </para>
/// <para>
/// W8.1 froze context-relative storage as non-admitted, so no <c>ContextRelativeSlot</c> exists in any draft surface
/// here and a caller-supplied context marker becomes a typed non-admission carrying
/// <see cref="StaticFieldV2StorageStrategyOutcome.ContextIdentityNotAttributableCode"/>. W8.1 likewise froze
/// <c>decimal</c> as a compiler-emitted attribute encoding rather than a Constant-table type, so <c>decimal</c> has no
/// literal draft kind, no <see cref="MetadataPrimitiveTypeKind"/>, and no admitted Constant type code; a named value
/// type without a declared instance <c>value__</c> field therefore stops as attribute-encoded rather than decoding.
/// </para>
/// </remarks>
public static class StaticFieldV2StorageStrategyBinder
{
    private const int ElementTypeBoolean = 0x02;
    private const int ElementTypeChar = 0x03;
    private const int ElementTypeInt8 = 0x04;
    private const int ElementTypeUInt8 = 0x05;
    private const int ElementTypeInt16 = 0x06;
    private const int ElementTypeUInt16 = 0x07;
    private const int ElementTypeInt32 = 0x08;
    private const int ElementTypeUInt32 = 0x09;
    private const int ElementTypeInt64 = 0x0A;
    private const int ElementTypeUInt64 = 0x0B;
    private const int ElementTypeSingle = 0x0C;
    private const int ElementTypeDouble = 0x0D;
    private const int ElementTypeString = 0x0E;
    private const int ElementTypeClass = 0x12;

    private const string EnumUnderlyingValueFieldName = "value__";

    private static readonly BoundedEcmaSignatureLimits SignatureLimits = new(
        StaticFieldV2Limits.MaximumTypeSpecificationByteCount,
        StaticFieldV2Limits.MaximumTypeSpecificationDepth,
        StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
        StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
        StaticFieldV2Limits.MaximumGenericParameterCount,
        StaticFieldV2Limits.MaximumParameterCount,
        StaticFieldV2Limits.MaximumLocalCount,
        StaticFieldV2Limits.MaximumArrayRank);

    /// <summary>Classifies the storage strategy and frozen capability requirements of one selected declaration.</summary>
    /// <param name="request">The complete storage-strategy draft classification request.</param>
    /// <remarks>
    /// The decision reads physical FieldAttributes plus the two caller-supplied markers only. A non-static declaration
    /// and a context-relative marker are the two typed non-admissions; among admitted branches a literal wins over a
    /// field RVA, which wins over a thread-relative marker, which wins over an ordinary constructed slot.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is either one admitted strategy or one typed non-admission.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2StorageStrategyOutcome ClassifyStrategy(StaticFieldV2StorageStrategyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = StrategyBoundaries(request);
        var row = request.FieldRow;
        if (!row.IsStatic)
        {
            return StaticFieldV2StorageStrategyOutcome.IssueStop(
                StaticFieldV2StorageStrategyIssue.InstanceFieldNotStatic,
                request,
                NoCapabilityRequired(),
                boundaries,
                StaticFieldV2StorageStrategyOutcome.InstanceFieldNotStaticCode);
        }
        if (request.ContextStaticAttributeSuppliedByCaller)
        {
            return StaticFieldV2StorageStrategyOutcome.IssueStop(
                StaticFieldV2StorageStrategyIssue.ContextRelativeStorageNotAdmitted,
                request,
                NoCapabilityRequired(),
                boundaries,
                StaticFieldV2StorageStrategyOutcome.ContextIdentityNotAttributableCode);
        }

        var strategy = row.IsLiteral
            ? StaticFieldV2StorageStrategy.MetadataLiteral
            : row.HasFieldRva
                ? StaticFieldV2StorageStrategy.ModuleRva
                : request.ThreadStaticAttributeSuppliedByCaller
                    ? StaticFieldV2StorageStrategy.ThreadRelativeSlot
                    : StaticFieldV2StorageStrategy.ConstructedSlot;
        return StaticFieldV2StorageStrategyOutcome.IssueExact(
            request,
            strategy,
            RequirementVector(strategy),
            boundaries);
    }

    /// <summary>Projects one exact static-field literal value from metadata alone.</summary>
    /// <param name="request">The complete metadata-literal draft projection request.</param>
    /// <remarks>
    /// The projection decodes the FieldDef signature with the shared bounded Core draft grammar, requires the signature
    /// type and the physical Constant type code to name the same type, and decodes the raw blob with exact width and
    /// signedness. Floating values are reinterpreted from their exact bit patterns and never from text. The exact null
    /// encoding is ECMA's Constant type <c>CLASS</c> with an all-zero four-byte value. No runtime, thread,
    /// module-content, slot, or memory capability is consulted, which the retained draft ledger records.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is either one decoded value or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2LiteralValueOutcome ProjectLiteral(StaticFieldV2LiteralProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = ImmutableArray.Create(
            StaticFieldV2StorageCoverageBoundary.CustomAttributeTableNotModeled,
            StaticFieldV2StorageCoverageBoundary.ConstantTableSuppliedByCaller);
        var row = request.FieldRow;
        if (!row.IsLiteral)
        {
            return Stop(
                request,
                StaticFieldV2LiteralValueResultKind.Unsupported,
                StaticFieldV2LiteralValueIssue.FieldNotLiteral,
                boundaries,
                null,
                0);
        }
        if (!IsAdmittedConstantTypeCode(request.ConstantTypeCode))
        {
            return Stop(
                request,
                StaticFieldV2LiteralValueResultKind.Unsupported,
                StaticFieldV2LiteralValueIssue.LiteralEncodingUnsupported,
                boundaries,
                null,
                request.ConstantTypeCode);
        }
        if (!TryProjectSignatureRoot(row.Observation.SignatureBytesCore, out var root))
        {
            return Stop(
                request,
                StaticFieldV2LiteralValueResultKind.Invalid,
                StaticFieldV2LiteralValueIssue.SignatureInvalid,
                boundaries,
                null,
                row.Observation.SignatureBytesCore.Length);
        }

        MetadataTypeDefinitionAuthorityIdentity? enumDefinition = null;
        int expectedTypeCode;
        switch (root.Kind)
        {
            case BoundedEcmaSignatureNodeKind.Primitive when IsAdmittedConstantTypeCode(root.ElementType):
                expectedTypeCode = root.ElementType;
                break;
            case BoundedEcmaSignatureNodeKind.String:
                expectedTypeCode = request.ConstantTypeCode == ElementTypeClass
                    ? ElementTypeClass
                    : ElementTypeString;
                break;
            case BoundedEcmaSignatureNodeKind.Object:
            case BoundedEcmaSignatureNodeKind.Class:
            case BoundedEcmaSignatureNodeKind.SzArray:
            case BoundedEcmaSignatureNodeKind.MultidimensionalArray:
                expectedTypeCode = ElementTypeClass;
                break;
            case BoundedEcmaSignatureNodeKind.ValueType:
                var resolution = ResolveNamedValueType(request, root.MetadataToken);
                if (resolution.Issue != StaticFieldV2LiteralValueIssue.None)
                {
                    return Stop(
                        request,
                        resolution.ResultKind,
                        resolution.Issue,
                        Add(boundaries, StaticFieldV2StorageCoverageBoundary.EnumUnderlyingDerivedFromInstanceValueField),
                        null,
                        root.MetadataToken);
                }
                enumDefinition = resolution.EnumDefinition;
                expectedTypeCode = resolution.UnderlyingTypeCode;
                boundaries = Add(
                    boundaries,
                    StaticFieldV2StorageCoverageBoundary.EnumUnderlyingDerivedFromInstanceValueField);
                break;
            default:
                return Stop(
                    request,
                    StaticFieldV2LiteralValueResultKind.Unsupported,
                    StaticFieldV2LiteralValueIssue.LiteralEncodingUnsupported,
                    boundaries,
                    null,
                    root.ElementType);
        }

        if (expectedTypeCode != request.ConstantTypeCode)
        {
            return Stop(
                request,
                StaticFieldV2LiteralValueResultKind.Invalid,
                StaticFieldV2LiteralValueIssue.LiteralTypeDisagreement,
                boundaries,
                null,
                request.ConstantTypeCode);
        }

        return Decode(request, enumDefinition, boundaries);
    }

    private static StaticFieldV2LiteralValueOutcome Decode(
        StaticFieldV2LiteralProjectionRequest request,
        MetadataTypeDefinitionAuthorityIdentity? enumDefinition,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> boundaries)
    {
        var blob = request.ConstantValueBlobCore;
        var code = request.ConstantTypeCode;
        if (code == ElementTypeString)
        {
            if ((blob.Length & 1) != 0)
            {
                return Stop(
                    request,
                    StaticFieldV2LiteralValueResultKind.Invalid,
                    StaticFieldV2LiteralValueIssue.LiteralBlobInvalid,
                    boundaries,
                    null,
                    blob.Length);
            }

            var characterCount = blob.Length / 2;
            if (characterCount > StaticFieldV2Limits.MaximumStringCharacterCount)
            {
                return Stop(
                    request,
                    StaticFieldV2LiteralValueResultKind.NonExact,
                    StaticFieldV2LiteralValueIssue.StringCharacterCountBoundReached,
                    boundaries,
                    new EvaluationDeterministicBound(
                        ExpressionV2ContractLimits.StaticStringCharacterCountBoundName,
                        StaticFieldV2Limits.MaximumStringCharacterCount),
                    StaticFieldV2Limits.MaximumStringCharacterCount + 1);
            }

            var characters = new char[characterCount];
            for (var index = 0; index < characterCount; index++)
            {
                characters[index] = (char)(blob[index * 2] | (blob[(index * 2) + 1] << 8));
            }
            return Exact(
                request,
                StaticFieldV2LiteralValueKind.String,
                enumDefinition,
                null,
                null,
                null,
                new string(characters),
                boundaries,
                characterCount);
        }

        var expectedWidth = ConstantWidth(code);
        if (blob.Length != expectedWidth)
        {
            return Stop(
                request,
                StaticFieldV2LiteralValueResultKind.Invalid,
                StaticFieldV2LiteralValueIssue.LiteralBlobInvalid,
                boundaries,
                null,
                blob.Length);
        }

        var bytes = blob.AsSpan();
        switch (code)
        {
            case ElementTypeBoolean:
                if (bytes[0] > 1)
                {
                    return Stop(
                        request,
                        StaticFieldV2LiteralValueResultKind.Invalid,
                        StaticFieldV2LiteralValueIssue.LiteralBlobInvalid,
                        boundaries,
                        null,
                        bytes[0]);
                }
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Boolean,
                    enumDefinition,
                    null,
                    bytes[0],
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeChar:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Char,
                    enumDefinition,
                    null,
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeInt8:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Int8,
                    enumDefinition,
                    (sbyte)bytes[0],
                    null,
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeUInt8:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.UInt8,
                    enumDefinition,
                    null,
                    bytes[0],
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeInt16:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Int16,
                    enumDefinition,
                    BinaryPrimitives.ReadInt16LittleEndian(bytes),
                    null,
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeUInt16:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.UInt16,
                    enumDefinition,
                    null,
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeInt32:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Int32,
                    enumDefinition,
                    BinaryPrimitives.ReadInt32LittleEndian(bytes),
                    null,
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeUInt32:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.UInt32,
                    enumDefinition,
                    null,
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes),
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeInt64:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Int64,
                    enumDefinition,
                    BinaryPrimitives.ReadInt64LittleEndian(bytes),
                    null,
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeUInt64:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.UInt64,
                    enumDefinition,
                    null,
                    BinaryPrimitives.ReadUInt64LittleEndian(bytes),
                    null,
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeSingle:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Single,
                    enumDefinition,
                    null,
                    null,
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes),
                    null,
                    boundaries,
                    expectedWidth);
            case ElementTypeDouble:
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Double,
                    enumDefinition,
                    null,
                    null,
                    BinaryPrimitives.ReadUInt64LittleEndian(bytes),
                    null,
                    boundaries,
                    expectedWidth);
            default:
                if (bytes[0] != 0 || bytes[1] != 0 || bytes[2] != 0 || bytes[3] != 0)
                {
                    return Stop(
                        request,
                        StaticFieldV2LiteralValueResultKind.Invalid,
                        StaticFieldV2LiteralValueIssue.LiteralBlobInvalid,
                        boundaries,
                        null,
                        blob.Length);
                }
                return Exact(
                    request,
                    StaticFieldV2LiteralValueKind.Null,
                    enumDefinition,
                    null,
                    null,
                    null,
                    null,
                    boundaries,
                    expectedWidth);
        }
    }

    private static NamedValueTypeResolution ResolveNamedValueType(
        StaticFieldV2LiteralProjectionRequest request,
        int metadataToken)
    {
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(metadataToken, 0x02) ||
            request.NamedLiteralTypeCatalog is not { } catalog ||
            catalog.ResultKind != MetadataFieldDefinitionTableResultKind.Exact ||
            catalog.DefinitionAuthority.ExactTypeDefinitionOrDefault(metadataToken) is not { } definition)
        {
            return NamedValueTypeResolution.Unavailable();
        }

        foreach (var fieldToken in definition.FieldDefinitionTokens)
        {
            var candidate = catalog.FindRow(fieldToken);
            if (candidate is null ||
                candidate.IsStatic ||
                !string.Equals(candidate.Name, EnumUnderlyingValueFieldName, StringComparison.Ordinal))
            {
                continue;
            }
            if (!TryProjectSignatureRoot(candidate.Observation.SignatureBytesCore, out var underlyingRoot) ||
                underlyingRoot.Kind != BoundedEcmaSignatureNodeKind.Primitive ||
                !IsAdmittedEnumUnderlyingTypeCode(underlyingRoot.ElementType))
            {
                return NamedValueTypeResolution.Unavailable();
            }
            return NamedValueTypeResolution.EnumUnderlying(definition, underlyingRoot.ElementType);
        }
        return NamedValueTypeResolution.AttributeEncoded();
    }

    private static bool TryProjectSignatureRoot(
        ImmutableArray<byte> signatureBytes,
        out BoundedEcmaSignatureNodeEvent root)
    {
        root = default;
        var sink = new SignatureNodeSink();
        var outcome = BoundedEcmaSignatureProjection.Decode(
            signatureBytes.AsSpan(),
            BoundedEcmaSignatureForm.Field,
            SignatureLimits,
            sink);
        if (outcome.Kind != BoundedEcmaSignatureDecodeKind.Exact)
        {
            return false;
        }

        var events = sink.ToImmutable();
        var ordinal = -1;
        for (var index = 0; index < events.Length; index++)
        {
            var node = events[index];
            if (node.NodeOrdinal != index)
            {
                return false;
            }
            if (node.ParentNodeOrdinal != -1)
            {
                continue;
            }
            if (ordinal >= 0)
            {
                return false;
            }
            ordinal = index;
        }
        if (ordinal < 0)
        {
            return false;
        }

        for (var depth = 0; depth <= StaticFieldV2Limits.MaximumTypeSpecificationDepth; depth++)
        {
            var node = events[ordinal];
            if (node.Kind is not (BoundedEcmaSignatureNodeKind.RequiredModifier or
                BoundedEcmaSignatureNodeKind.OptionalModifier))
            {
                root = node;
                return true;
            }

            var child = -1;
            for (var index = 0; index < events.Length; index++)
            {
                if (events[index].ParentNodeOrdinal != ordinal)
                {
                    continue;
                }
                if (child >= 0)
                {
                    return false;
                }
                child = index;
            }
            if (child < 0)
            {
                return false;
            }
            ordinal = child;
        }
        return false;
    }

    private static StaticFieldV2CapabilityRequirementVector RequirementVector(
        StaticFieldV2StorageStrategy strategy) => strategy switch
        {
            StaticFieldV2StorageStrategy.MetadataLiteral => StaticFieldV2StorageStrategyOutcome.IssueVector(
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.Required,
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.NotRequired),
            StaticFieldV2StorageStrategy.ModuleRva => StaticFieldV2StorageStrategyOutcome.IssueVector(
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.Required,
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.Required),
            StaticFieldV2StorageStrategy.ThreadRelativeSlot => StaticFieldV2StorageStrategyOutcome.IssueVector(
                StaticFieldV2CapabilityRequirement.Required,
                StaticFieldV2CapabilityRequirement.Required,
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.Required,
                StaticFieldV2CapabilityRequirement.Required),
            _ => StaticFieldV2StorageStrategyOutcome.IssueVector(
                StaticFieldV2CapabilityRequirement.Required,
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.NotRequired,
                StaticFieldV2CapabilityRequirement.Required,
                StaticFieldV2CapabilityRequirement.Required),
        };

    private static StaticFieldV2CapabilityRequirementVector NoCapabilityRequired() =>
        StaticFieldV2StorageStrategyOutcome.IssueVector(
            StaticFieldV2CapabilityRequirement.NotRequired,
            StaticFieldV2CapabilityRequirement.NotRequired,
            StaticFieldV2CapabilityRequirement.NotRequired,
            StaticFieldV2CapabilityRequirement.NotRequired,
            StaticFieldV2CapabilityRequirement.NotRequired);

    private static ImmutableArray<StaticFieldV2StorageCoverageBoundary> StrategyBoundaries(
        StaticFieldV2StorageStrategyRequest request)
    {
        var boundaries = ImmutableArray.Create(
            StaticFieldV2StorageCoverageBoundary.CustomAttributeTableNotModeled);
        if (request.ThreadStaticAttributeSuppliedByCaller)
        {
            boundaries = Add(
                boundaries,
                StaticFieldV2StorageCoverageBoundary.ThreadStaticAttributeSuppliedByCaller);
        }
        if (request.ContextStaticAttributeSuppliedByCaller)
        {
            boundaries = Add(
                boundaries,
                StaticFieldV2StorageCoverageBoundary.ContextStaticAttributeSuppliedByCaller);
        }
        return boundaries;
    }

    private static ImmutableArray<StaticFieldV2StorageCoverageBoundary> Add(
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> boundaries,
        StaticFieldV2StorageCoverageBoundary boundary)
    {
        if (boundaries.Contains(boundary))
        {
            return boundaries;
        }

        var extended = new List<StaticFieldV2StorageCoverageBoundary>(boundaries) { boundary };
        extended.Sort();
        return [.. extended];
    }

    private static bool IsAdmittedConstantTypeCode(int typeCode) =>
        typeCode is >= ElementTypeBoolean and <= ElementTypeString or ElementTypeClass;

    private static bool IsAdmittedEnumUnderlyingTypeCode(int typeCode) =>
        typeCode is ElementTypeBoolean or ElementTypeChar or
            (>= ElementTypeInt8 and <= ElementTypeUInt64);

    private static int ConstantWidth(int typeCode) => typeCode switch
    {
        ElementTypeBoolean or ElementTypeInt8 or ElementTypeUInt8 => 1,
        ElementTypeChar or ElementTypeInt16 or ElementTypeUInt16 => 2,
        ElementTypeInt32 or ElementTypeUInt32 or ElementTypeSingle or ElementTypeClass => 4,
        _ => 8,
    };

    private static StaticFieldV2LiteralValueOutcome Exact(
        StaticFieldV2LiteralProjectionRequest request,
        StaticFieldV2LiteralValueKind valueKind,
        MetadataTypeDefinitionAuthorityIdentity? enumDefinition,
        long? signedValue,
        ulong? unsignedValue,
        ulong? floatingBitPattern,
        string? stringValue,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> boundaries,
        int observedCount) =>
        StaticFieldV2LiteralValueOutcome.IssueExact(
            request,
            valueKind,
            enumDefinition,
            signedValue,
            unsignedValue,
            floatingBitPattern,
            stringValue,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            boundaries,
            observedCount);

    private static StaticFieldV2LiteralValueOutcome Stop(
        StaticFieldV2LiteralProjectionRequest request,
        StaticFieldV2LiteralValueResultKind resultKind,
        StaticFieldV2LiteralValueIssue issue,
        ImmutableArray<StaticFieldV2StorageCoverageBoundary> boundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        StaticFieldV2LiteralValueOutcome.IssueStop(
            resultKind,
            issue,
            request,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            boundaries,
            reachedBound,
            observedCount);

    private readonly record struct NamedValueTypeResolution(
        StaticFieldV2LiteralValueResultKind ResultKind,
        StaticFieldV2LiteralValueIssue Issue,
        MetadataTypeDefinitionAuthorityIdentity? EnumDefinition,
        int UnderlyingTypeCode)
    {
        internal static NamedValueTypeResolution EnumUnderlying(
            MetadataTypeDefinitionAuthorityIdentity definition,
            int underlyingTypeCode) =>
            new(
                StaticFieldV2LiteralValueResultKind.Exact,
                StaticFieldV2LiteralValueIssue.None,
                definition,
                underlyingTypeCode);

        internal static NamedValueTypeResolution Unavailable() =>
            new(
                StaticFieldV2LiteralValueResultKind.NonExact,
                StaticFieldV2LiteralValueIssue.NamedLiteralTypeEvidenceUnavailable,
                null,
                0);

        internal static NamedValueTypeResolution AttributeEncoded() =>
            new(
                StaticFieldV2LiteralValueResultKind.Unsupported,
                StaticFieldV2LiteralValueIssue.AttributeEncodedLiteralNotModeled,
                null,
                0);
    }

    private sealed class SignatureNodeSink : IBoundedEcmaSignatureNodeSink
    {
        private readonly ImmutableArray<BoundedEcmaSignatureNodeEvent>.Builder nodes =
            ImmutableArray.CreateBuilder<BoundedEcmaSignatureNodeEvent>();

        public void Add(in BoundedEcmaSignatureNodeEvent node) => nodes.Add(node);

        internal ImmutableArray<BoundedEcmaSignatureNodeEvent> ToImmutable() => nodes.ToImmutable();
    }
}
