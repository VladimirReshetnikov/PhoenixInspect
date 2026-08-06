using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Selects how one static-field member lookup admits or refuses a physical declaration.</summary>
/// <remarks>
/// The mode is a caller-declared certificate rather than a derived fact. Qualified inspection deliberately
/// bypasses every accessibility rule so that a dump inspector can name any physical declaration, while the use-site
/// certificate reproduces the subset of ECMA-335 accessibility that a definition-only phase can honestly decide.
/// </remarks>
public enum StaticFieldV2AccessibilityMode
{
    /// <summary>Every physical declaration is admitted and the bypass is retained as a declared boundary.</summary>
    QualifiedInspectionBypass = 1,

    /// <summary>Only declarations reachable from the supplied requesting assembly are admitted.</summary>
    UseSiteCertificate = 2,
}

/// <summary>Classifies one definition-side static-field member-lookup answer.</summary>
/// <remarks>
/// <see cref="Exact"/>, <see cref="Absent"/>, <see cref="Ambiguous"/>, <see cref="Partial"/>, and
/// <see cref="HiddenByUnsupportedMember"/> are complete derived answers that retain their per-level evidence.
/// <see cref="NonExact"/>, <see cref="Invalid"/>, and <see cref="Unsupported"/> are prefix-free stops that retain no
/// level evidence, no candidate, and no ancestry terminal.
/// </remarks>
public enum StaticFieldV2MemberLookupResultKind
{
    /// <summary>Exactly one accessible static field declaration was selected.</summary>
    Exact = 1,

    /// <summary>Every level of one complete bounded base chain was consulted and declared no such name.</summary>
    Absent = 2,

    /// <summary>One level declared two or more accessible static fields with the requested name.</summary>
    Ambiguous = 3,

    /// <summary>The reached levels declared no such name and the base chain could not prove absence.</summary>
    Partial = 4,

    /// <summary>The nearest level declaring the name declared it as a member this profile does not own.</summary>
    HiddenByUnsupportedMember = 5,

    /// <summary>A prerequisite or a declared bound prevented any complete answer.</summary>
    NonExact = 6,

    /// <summary>Complete supplied evidence contradicted the ancestry portfolio or itself.</summary>
    Invalid = 7,

    /// <summary>The requested owner selects a later route that this definition-side binder does not own.</summary>
    Unsupported = 8,
}

/// <summary>Identifies the deterministic issue for one static-field member-lookup outcome.</summary>
/// <remarks>This issue catalog keeps prerequisite, vector-shape, owner, bound, and derived answers distinct.</remarks>
public enum StaticFieldV2MemberLookupIssue
{
    /// <summary>No issue applies to an exact outcome.</summary>
    None = 0,

    /// <summary>The ancestry authority portfolio prerequisite was non-exact.</summary>
    AncestryPortfolioNonExact = 1,

    /// <summary>The ancestry authority portfolio prerequisite was invalid.</summary>
    AncestryPortfolioInvalid = 2,

    /// <summary>The supplied FieldDef catalog vector was default rather than explicitly initialized.</summary>
    FieldCatalogVectorUninitialized = 3,

    /// <summary>The supplied FieldDef catalog vector reached the shared module bound plus one.</summary>
    FieldCatalogModuleCountBoundReached = 4,

    /// <summary>The supplied FieldDef catalog vector was shorter than the exact ancestry portfolio.</summary>
    FieldCatalogSlotsIncomplete = 5,

    /// <summary>The supplied FieldDef catalog vector was longer than the exact ancestry portfolio.</summary>
    FieldCatalogSlotCountConflict = 6,

    /// <summary>An initialized FieldDef catalog vector slot contained no catalog.</summary>
    FieldCatalogMissing = 7,

    /// <summary>One supplied FieldDef catalog stopped non-exactly.</summary>
    FieldCatalogNonExact = 8,

    /// <summary>One supplied FieldDef catalog retained contradictory evidence.</summary>
    FieldCatalogInvalid = 9,

    /// <summary>More than one supplied FieldDef catalog named the same exact metadata module.</summary>
    DuplicateFieldCatalogModule = 10,

    /// <summary>A supplied FieldDef catalog named no module in the exact ancestry portfolio.</summary>
    FieldCatalogModuleNotInPortfolio = 11,

    /// <summary>A FieldDef catalog's source ends differed from its module's authority source ends.</summary>
    FieldCatalogSourceEndsMismatch = 12,

    /// <summary>A FieldDef catalog's definition authority differed from its module's portfolio authority.</summary>
    FieldCatalogAuthorityMismatch = 13,

    /// <summary>The requested owner module is absent from the exact ancestry portfolio.</summary>
    OwnerModuleNotInPortfolio = 14,

    /// <summary>The requested owner token was not issued as a TypeDef by that module's authority.</summary>
    OwnerTypeDefinitionNotIssued = 15,

    /// <summary>The requested owner is the ECMA module pseudo-type, which declares no source-addressable member.</summary>
    ModulePseudoTypeOwner = 16,

    /// <summary>The accessibility decision count reached the declared cap plus one.</summary>
    AccessibilityCheckBoundReached = 17,

    /// <summary>Every reached level declared no such name and the bounded base chain was incomplete.</summary>
    AncestryIncomplete = 18,

    /// <summary>Every level of one complete bounded base chain declared no such name.</summary>
    DeclarationAbsent = 19,

    /// <summary>The nearest declaring level declared the name as a method this profile does not own.</summary>
    HiddenByDeclaredMethod = 20,

    /// <summary>The nearest declaring level declared the name as an instance field rather than a static field.</summary>
    HiddenByInstanceField = 21,

    /// <summary>The nearest declaring level declared two or more accessible static fields with the name.</summary>
    AmbiguousStaticDeclarations = 22,

    /// <summary>The supplied interface-implementation authority portfolio prerequisite was non-exact.</summary>
    InterfaceImplementationPortfolioNonExact = 23,

    /// <summary>The supplied interface-implementation authority portfolio prerequisite was invalid.</summary>
    InterfaceImplementationPortfolioInvalid = 24,

    /// <summary>A generic base hop needed a token-resolution catalog the request did not supply exactly.</summary>
    GenericBaseTokenResolutionUnavailable = 25,

    /// <summary>A retained generic base TypeSpec decoded as malformed or names a form invalid in a base position.</summary>
    GenericBaseSignatureInvalid = 26,

    /// <summary>A retained generic base TypeSpec names valid metadata outside the admitted closed base grammar.</summary>
    GenericBaseSignatureUnsupported = 27,

    /// <summary>A generic base TypeSpec references an owner variable the current construction does not bind.</summary>
    GenericBaseArgumentUnbound = 28,

    /// <summary>A generic base head resolved to a definition without an exact authority classification.</summary>
    GenericBaseClassificationAbsent = 29,

    /// <summary>The accumulated constructed base chain reached the declared ancestry depth bound.</summary>
    GenericBaseDepthBoundReached = 30,

    /// <summary>The constructed base chain revisited one physical TypeDef coordinate.</summary>
    GenericBaseCycleDetected = 31,

    /// <summary>A consulted level declared a property owning the requested name.</summary>
    HiddenByDeclaredProperty = 32,

    /// <summary>No Property catalog vector was supplied at all.</summary>
    PropertyCatalogVectorUninitialized = 33,

    /// <summary>The supplied Property catalog vector exceeded the declared module bound.</summary>
    PropertyCatalogModuleCountBoundReached = 34,

    /// <summary>The supplied Property catalog vector held fewer slots than the portfolio declares modules.</summary>
    PropertyCatalogSlotsIncomplete = 35,

    /// <summary>The supplied Property catalog vector held more slots than the portfolio declares modules.</summary>
    PropertyCatalogSlotCountConflict = 36,

    /// <summary>One supplied Property catalog slot was null.</summary>
    PropertyCatalogMissing = 37,

    /// <summary>One supplied Property catalog stopped non-exactly.</summary>
    PropertyCatalogNonExact = 38,

    /// <summary>One supplied Property catalog retained contradictory evidence.</summary>
    PropertyCatalogInvalid = 39,

    /// <summary>More than one supplied Property catalog named the same exact metadata module.</summary>
    DuplicatePropertyCatalogModule = 40,

    /// <summary>A supplied Property catalog named no module in the exact ancestry portfolio.</summary>
    PropertyCatalogModuleNotInPortfolio = 41,

    /// <summary>A Property catalog's source ends differed from its module's authority source ends.</summary>
    PropertyCatalogSourceEndsMismatch = 42,

    /// <summary>A Property catalog's definition authority differed from its module's portfolio authority.</summary>
    PropertyCatalogAuthorityMismatch = 43,

    /// <summary>
    /// A walked ancestry level's module is declared with applied edit generations no composed authority covers, so
    /// the lookup refuses the level's base-image members rather than selecting from potentially stale rows.
    /// </summary>
    BaseModuleEditedGenerationsNotComposed = 44,
}

/// <summary>Classifies the physical storage shape of one selected static-field declaration.</summary>
/// <remarks>The shape is a pure FieldAttributes decoding and asserts nothing about runtime storage.</remarks>
public enum StaticFieldV2FieldStorageShape
{
    /// <summary>The declaration is an ordinary stored static slot with no literal value and no field RVA.</summary>
    StoredSlot = 1,

    /// <summary>The declaration carries FieldAttributes.Literal, which also implies a Constant row.</summary>
    MetadataLiteral = 2,

    /// <summary>The declaration carries FieldAttributes.HasFieldRVA and therefore names module image bytes.</summary>
    ModuleRvaCandidate = 3,
}

/// <summary>Identifies one declared coverage boundary retained by a static-field member-lookup outcome.</summary>
/// <remarks>
/// Every boundary is an informational fact rather than an error. A boundary states what this definition-side
/// phase deliberately does not model, so a consumer can never mistake a silent gap for a proven negative.
/// </remarks>
public enum StaticFieldV2MemberLookupCoverageBoundary
{
    // Value 1, PropertyAndEventTablesNotModeled, is retired: the Property table is modeled now, so the claim is
    // no longer true as stated. The number is left as a hole. Renumbering a narrower boundary onto it would keep
    // every already-recorded digest byte-identical while silently changing what the recorded evidence asserts.

    /// <summary>An interface owner examined only itself because no interface-implementation portfolio was supplied.</summary>
    InterfaceAncestryNotModeled = 2,

    /// <summary>The physical InternalsVisibleTo CustomAttribute table is not modeled; friend grants are supplied.</summary>
    FriendAssemblyAttributesNotModeled = 3,

    /// <summary>Qualified inspection bypassed every accessibility rule for every examined declaration.</summary>
    AccessibilityBypassApplied = 4,

    /// <summary>The physical Event and EventMap tables are not modeled, so a same-name event cannot block.</summary>
    EventTablesNotModeled = 5,

    /// <summary>
    /// MethodSemantics is not modeled, so a same-name property blocks without any test of whether its accessors are
    /// reachable from the use site.
    /// </summary>
    PropertyAccessorSemanticsNotModeled = 6,
}

/// <summary>Freezes one caller-supplied friend-assembly grant of a single defining assembly identity.</summary>
/// <remarks>
/// The physical InternalsVisibleTo CustomAttribute table is not modeled by this slice, so friendship is a
/// caller-supplied certificate: one defining assembly plus the ordered assembly definitions it declares as friends.
/// The grant asserts nothing about strong-name keys, because no public-key comparison is performed here.
/// </remarks>
public sealed class StaticFieldV2FriendAssemblyGrantIdentity :
    IEquatable<StaticFieldV2FriendAssemblyGrantIdentity>
{
    private const string CanonicalDomain = "static-field-v2-friend-assembly-grant";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldAssemblyDefinitionIdentity> declaredFriendAssemblies;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2FriendAssemblyGrantIdentity(
        StaticFieldContainingAssemblyIdentity definingAssembly,
        ImmutableArray<StaticFieldAssemblyDefinitionIdentity> declaredFriendAssemblies)
    {
        DefiningAssembly = definingAssembly;
        this.declaredFriendAssemblies = declaredFriendAssemblies;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(definingAssembly.Sha256, nameof(definingAssembly));
        writer.WriteInt32(declaredFriendAssemblies.Length);
        foreach (var friend in declaredFriendAssemblies)
        {
            writer.WriteSha256(friend.Sha256, nameof(declaredFriendAssemblies));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact defining assembly whose declared friendships this grant carries.</summary>
    public StaticFieldContainingAssemblyIdentity DefiningAssembly { get; }

    /// <summary>Gets a defensive declaration-order copy of the assemblies this defining assembly befriends.</summary>
    public ImmutableArray<StaticFieldAssemblyDefinitionIdentity> DeclaredFriendAssemblies =>
        ExpressionV2ContractEncoding.Copy(declaredFriendAssemblies);

    /// <summary>Gets a defensive copy of the fixed-reference canonical grant bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical grant.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one caller-supplied friend-assembly grant for a single defining assembly.</summary>
    /// <param name="definingAssembly">The exact defining assembly that declares the friendships.</param>
    /// <param name="declaredFriendAssemblies">
    /// The ordered assembly definitions declared as friends, bounded by the shared friend-declaration cap.
    /// </param>
    /// <returns>A sealed immutable grant with defensively copied declarations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definingAssembly"/> is null.</exception>
    /// <exception cref="ArgumentException">The declaration array is default, over cap, or contains nulls.</exception>
    public static StaticFieldV2FriendAssemblyGrantIdentity Create(
        StaticFieldContainingAssemblyIdentity definingAssembly,
        ImmutableArray<StaticFieldAssemblyDefinitionIdentity> declaredFriendAssemblies)
    {
        ArgumentNullException.ThrowIfNull(definingAssembly);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            declaredFriendAssemblies,
            nameof(declaredFriendAssemblies),
            StaticFieldV2Limits.MaximumFriendAssemblyDeclarationCount);
        return new StaticFieldV2FriendAssemblyGrantIdentity(definingAssembly, copied);
    }

    /// <summary>Tests whether this grant befriends one exact assembly definition.</summary>
    /// <param name="candidate">The candidate requesting assembly definition.</param>
    /// <returns><see langword="true"/> only when the grant declares that exact assembly definition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> is null.</exception>
    public bool Declares(StaticFieldAssemblyDefinitionIdentity candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        foreach (var friend in declaredFriendAssemblies)
        {
            if (friend.Equals(candidate))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Tests canonical equality between two friend-assembly grants.</summary>
    /// <param name="other">The other grant.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2FriendAssemblyGrantIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests friend-assembly grant equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a grant with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2FriendAssemblyGrantIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical grant content.</summary>
    /// <returns>A hash code for this canonical grant.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one examined same-name static or instance field candidate.</summary>
/// <remarks>
/// The candidate is minted only by <see cref="StaticFieldV2MemberLookupOutcome"/>. It retains the physical FieldDef
/// row, the authority-issued declaring TypeDef, the zero-based base-chain level that declared it, and the complete
/// accessibility decision that admitted or refused it. No generic substitution and no storage read occurred.
/// </remarks>
public sealed class StaticFieldV2MemberCandidateIdentity : IEquatable<StaticFieldV2MemberCandidateIdentity>
{
    private const string CanonicalDomain = "static-field-v2-member-candidate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2MemberCandidateIdentity(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int levelIndex,
        bool isAccessible,
        MetadataFieldAccessibility effectiveAccessibility,
        StaticFieldV2FieldStorageShape storageShape,
        MetadataClosedTypeIdentity? declaringConstruction)
    {
        FieldRow = fieldRow;
        DeclaringTypeDefinition = declaringTypeDefinition;
        LevelIndex = levelIndex;
        IsAccessible = isAccessible;
        EffectiveAccessibility = effectiveAccessibility;
        StorageShape = storageShape;
        DeclaringConstruction = declaringConstruction;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(fieldRow.Sha256, nameof(fieldRow));
        writer.WriteSha256(declaringTypeDefinition.Sha256, nameof(declaringTypeDefinition));
        writer.WriteInt32(levelIndex);
        writer.WriteBoolean(isAccessible);
        writer.WriteInt32((int)effectiveAccessibility);
        writer.WriteInt32((int)storageShape);

        // The declaring construction is appended only when a constructed generic base level declared the candidate,
        // so every candidate declared on a definition-only level keeps its exact version-1 bytes and frozen digest.
        if (declaringConstruction is not null)
        {
            writer.WriteBoolean(true);
            writer.WriteSha256(declaringConstruction.Sha256, nameof(declaringConstruction));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact physical FieldDef row this candidate names.</summary>
    public MetadataFieldDefinitionTableRowIdentity FieldRow { get; }

    /// <summary>Gets the authority-issued TypeDef that physically declared this candidate.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the zero-based bounded base-chain level that declared this candidate.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets whether the requested accessibility mode admitted this candidate.</summary>
    public bool IsAccessible { get; }

    /// <summary>Gets the effective accessibility decided from the field and its declaring nested-type chain.</summary>
    public MetadataFieldAccessibility EffectiveAccessibility { get; }

    /// <summary>Gets the physical storage shape decoded from this candidate's FieldAttributes.</summary>
    public StaticFieldV2FieldStorageShape StorageShape { get; }

    /// <summary>Gets the exact closed construction of the declaring generic base level, otherwise null.</summary>
    /// <remarks>
    /// A candidate declared on the requested owner or on a definition-only named base level retains null here,
    /// because the spelled owner construction already carries its constructed identity. A non-null value names the
    /// exact substituted construction of the generic TypeSpec base level that physically declared this candidate,
    /// and later storage selection must target that construction rather than the spelled owner.
    /// </remarks>
    public MetadataClosedTypeIdentity? DeclaringConstruction { get; }

    /// <summary>Gets the exact physical FieldDef token of this candidate.</summary>
    public int FieldDefinitionToken => FieldRow.FieldDefinitionToken;

    /// <summary>Gets whether the physical row carries FieldAttributes.Static.</summary>
    public bool IsStatic => FieldRow.IsStatic;

    /// <summary>Gets a defensive copy of the fixed-reference canonical candidate bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical candidate.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two examined member candidates.</summary>
    /// <param name="other">The other candidate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2MemberCandidateIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests examined member candidate equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a candidate with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberCandidateIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical candidate content.</summary>
    /// <returns>A hash code for this canonical candidate.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2MemberCandidateIdentity Create(
        object mintCapability,
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int levelIndex,
        bool isAccessible,
        MetadataFieldAccessibility effectiveAccessibility,
        StaticFieldV2FieldStorageShape storageShape,
        MetadataClosedTypeIdentity? declaringConstruction)
    {
        if (!StaticFieldV2MemberLookupOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A member candidate requires the lookup outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(fieldRow);
        ArgumentNullException.ThrowIfNull(declaringTypeDefinition);
        ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
        ExpressionV2ContractEncoding.RequireDefined(effectiveAccessibility, nameof(effectiveAccessibility));
        ExpressionV2ContractEncoding.RequireDefined(storageShape, nameof(storageShape));
        if (fieldRow.DeclaringTypeDefinitionToken != declaringTypeDefinition.TypeDefinitionToken)
        {
            throw new ArgumentException(
                "A member candidate must retain the physical declaring TypeDef of its FieldDef row.",
                nameof(declaringTypeDefinition));
        }
        if (declaringConstruction is not null &&
            (declaringConstruction.FinalClassification is not { } finalClassification ||
             !finalClassification.TypeDefinition.Equals(declaringTypeDefinition)))
        {
            throw new ArgumentException(
                "A declaring construction must name the exact declaring TypeDef of its FieldDef row.",
                nameof(declaringConstruction));
        }

        return new StaticFieldV2MemberCandidateIdentity(
            fieldRow,
            declaringTypeDefinition,
            levelIndex,
            isAccessible,
            effectiveAccessibility,
            storageShape,
            declaringConstruction);
    }
}

/// <summary>Freezes one consulted bounded base-chain level of a static-field member-lookup answer.</summary>
/// <remarks>
/// The level is minted only by <see cref="StaticFieldV2MemberLookupOutcome"/>. It proves which physical declaring
/// type was consulted and how many declared FieldDef and MethodDef rows were examined there, so provenance can
/// establish that the answer consulted every nearer level before any farther one.
/// </remarks>
public sealed class StaticFieldV2MemberLookupLevelIdentity : IEquatable<StaticFieldV2MemberLookupLevelIdentity>
{
    private const string CanonicalDomain = "static-field-v2-member-lookup-level";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2MemberLookupLevelIdentity(
        int levelIndex,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int examinedFieldCount,
        int examinedMethodCount,
        int examinedPropertyCount,
        int accessibleCandidateCount)
    {
        LevelIndex = levelIndex;
        DeclaringTypeDefinition = declaringTypeDefinition;
        ExaminedFieldCount = examinedFieldCount;
        ExaminedMethodCount = examinedMethodCount;
        ExaminedPropertyCount = examinedPropertyCount;
        AccessibleCandidateCount = accessibleCandidateCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(levelIndex);
        writer.WriteSha256(declaringTypeDefinition.Sha256, nameof(declaringTypeDefinition));
        writer.WriteInt32(examinedFieldCount);
        writer.WriteInt32(examinedMethodCount);
        writer.WriteInt32(examinedPropertyCount);
        writer.WriteInt32(accessibleCandidateCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based level index, where zero is the requested owner itself.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets the authority-issued TypeDef consulted at this bounded base-chain level.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the count of declared FieldDef rows examined at this level.</summary>
    public int ExaminedFieldCount { get; }

    /// <summary>Gets the count of declared MethodDef rows examined at this level.</summary>
    public int ExaminedMethodCount { get; }

    /// <summary>Gets the count of declared Property rows examined at this level.</summary>
    public int ExaminedPropertyCount { get; }

    /// <summary>Gets the count of accessible same-name field, method, and property declarations at this level.</summary>
    public int AccessibleCandidateCount { get; }

    /// <summary>Gets the exact TypeDef token of the declaring type consulted at this level.</summary>
    public int DeclaringTypeDefinitionToken => DeclaringTypeDefinition.TypeDefinitionToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical level bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical level.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two consulted levels.</summary>
    /// <param name="other">The other level.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2MemberLookupLevelIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests consulted level equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a level with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberLookupLevelIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical level content.</summary>
    /// <returns>A hash code for this canonical level.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2MemberLookupLevelIdentity Create(
        object mintCapability,
        int levelIndex,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int examinedFieldCount,
        int examinedMethodCount,
        int examinedPropertyCount,
        int accessibleCandidateCount)
    {
        if (!StaticFieldV2MemberLookupOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A consulted lookup level requires the lookup outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(declaringTypeDefinition);
        ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(examinedFieldCount);
        ArgumentOutOfRangeException.ThrowIfNegative(examinedMethodCount);
        ArgumentOutOfRangeException.ThrowIfNegative(examinedPropertyCount);
        ArgumentOutOfRangeException.ThrowIfNegative(accessibleCandidateCount);
        return new StaticFieldV2MemberLookupLevelIdentity(
            levelIndex,
            declaringTypeDefinition,
            examinedFieldCount,
            examinedMethodCount,
            examinedPropertyCount,
            accessibleCandidateCount);
    }
}

/// <summary>Freezes one complete definition-side static-field member-lookup request.</summary>
/// <remarks>
/// The request names one owner module, one owner TypeDef token, one decoded field identifier, one exact ancestry
/// portfolio, one FieldDef catalog per ancestry-portfolio module, the caller-declared accessibility
/// certificate, and an optional interface-implementation portfolio. It carries no runtime, no storage, no
/// scoped evaluation context, and no PDB evidence, and it constructs no generic instantiation: substitution onto a
/// closed construction is a separately owned later slice.
/// <para>
/// The interface-implementation portfolio is optional because it is a separately acquired authority. When it is
/// absent an interface owner examines only itself and the declared
/// <see cref="StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled"/> boundary is retained; when it
/// is present the interface owner's bounded transitive closure supplies the later levels.
/// </para>
/// </remarks>
public sealed class StaticFieldV2MemberLookupRequest : IEquatable<StaticFieldV2MemberLookupRequest>
{
    private const string CanonicalDomain = "static-field-v2-member-lookup-request";
    private const int CanonicalSchemaVersion = 3;
    private const int TokenResolutionCatalogsFieldTag = 1;
    private const int OwnerClosedConstructionFieldTag = 2;
    private const int ModuleEditDeclarationsFieldTag = 3;
    private readonly ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs;
    private readonly ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs;
    private readonly ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants;
    private readonly ImmutableArray<MetadataSignatureTokenResolutionCatalog> tokenResolutionCatalogs;
    private readonly ImmutableArray<StaticFieldV2ModuleEditDeclaration> moduleEditDeclarations;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2MemberLookupRequest(
        StaticFieldMetadataModuleIdentity ownerModule,
        int ownerTypeDefinitionToken,
        DumpExpressionIdentifier fieldName,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs,
        StaticFieldV2AccessibilityMode accessibilityMode,
        StaticFieldContainingAssemblyIdentity? requestingAssembly,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio,
        ImmutableArray<MetadataSignatureTokenResolutionCatalog> tokenResolutionCatalogs,
        MetadataClosedTypeIdentity? ownerClosedConstruction,
        ImmutableArray<StaticFieldV2ModuleEditDeclaration> moduleEditDeclarations)
    {
        OwnerModule = ownerModule;
        OwnerTypeDefinitionToken = ownerTypeDefinitionToken;
        FieldName = fieldName;
        AncestryPortfolio = ancestryPortfolio;
        this.fieldCatalogs = fieldCatalogs;
        this.propertyCatalogs = propertyCatalogs;
        AccessibilityMode = accessibilityMode;
        RequestingAssembly = requestingAssembly;
        this.friendAssemblyGrants = friendAssemblyGrants;
        InterfaceImplementationPortfolio = interfaceImplementationPortfolio;
        this.tokenResolutionCatalogs = tokenResolutionCatalogs;
        OwnerClosedConstruction = ownerClosedConstruction;
        this.moduleEditDeclarations = moduleEditDeclarations;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(ownerModule.Sha256, nameof(ownerModule));
        writer.WriteInt32(ownerTypeDefinitionToken);
        writer.WriteSha256(fieldName.Sha256, nameof(fieldName));
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        writer.WriteInt32(fieldCatalogs.IsDefault ? -1 : fieldCatalogs.Length);
        if (!fieldCatalogs.IsDefault)
        {
            foreach (var catalog in fieldCatalogs)
            {
                ExpressionV2ContractEncoding.WriteOptionalDigest(writer, catalog?.Sha256);
            }
        }
        writer.WriteInt32(propertyCatalogs.IsDefault ? -1 : propertyCatalogs.Length);
        if (!propertyCatalogs.IsDefault)
        {
            foreach (var catalog in propertyCatalogs)
            {
                ExpressionV2ContractEncoding.WriteOptionalDigest(writer, catalog?.Sha256);
            }
        }
        writer.WriteInt32((int)accessibilityMode);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, requestingAssembly?.Sha256);
        writer.WriteInt32(friendAssemblyGrants.Length);
        foreach (var grant in friendAssemblyGrants)
        {
            writer.WriteSha256(grant.Sha256, nameof(friendAssemblyGrants));
        }
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, interfaceImplementationPortfolio?.Sha256);

        // The generic-base inputs are appended only when supplied, each behind its distinct field tag, so every
        // request created without them keeps its exact version-2 byte content and its frozen digest unchanged.
        if (!tokenResolutionCatalogs.IsDefault)
        {
            writer.WriteInt32(TokenResolutionCatalogsFieldTag);
            writer.WriteInt32(tokenResolutionCatalogs.Length);
            foreach (var catalog in tokenResolutionCatalogs)
            {
                ExpressionV2ContractEncoding.WriteOptionalDigest(writer, catalog?.Sha256);
            }
        }
        if (ownerClosedConstruction is not null)
        {
            writer.WriteInt32(OwnerClosedConstructionFieldTag);
            writer.WriteSha256(ownerClosedConstruction.Sha256, nameof(ownerClosedConstruction));
        }
        if (!moduleEditDeclarations.IsDefault)
        {
            writer.WriteInt32(ModuleEditDeclarationsFieldTag);
            writer.WriteInt32(moduleEditDeclarations.Length);
            foreach (var declaration in moduleEditDeclarations)
            {
                writer.WriteSha256(declaration.Sha256, nameof(moduleEditDeclarations));
            }
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module that owns the requested TypeDef.</summary>
    public StaticFieldMetadataModuleIdentity OwnerModule { get; }

    /// <summary>Gets the exact non-nil owner TypeDef token whose declarations are searched.</summary>
    public int OwnerTypeDefinitionToken { get; }

    /// <summary>Gets the decoded field identifier compared ordinally against physical FieldDef names.</summary>
    public DumpExpressionIdentifier FieldName { get; }

    /// <summary>Gets the ancestry authority portfolio supplying classification and bounded base chains.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets a defensive copy of the supplied per-module FieldDef catalog vector, default when absent.</summary>
    public ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs =>
        fieldCatalogs.IsDefault ? default : ExpressionV2ContractEncoding.Copy(fieldCatalogs);

    /// <summary>Gets whether the supplied FieldDef catalog vector was explicitly initialized.</summary>
    public bool IsFieldCatalogVectorInitialized => !fieldCatalogs.IsDefault;

    /// <summary>Gets a defensive copy of the supplied per-module Property catalog vector, default when absent.</summary>
    public ImmutableArray<MetadataPropertyTableCatalogIdentity> PropertyCatalogs =>
        propertyCatalogs.IsDefault ? default : ExpressionV2ContractEncoding.Copy(propertyCatalogs);

    /// <summary>Gets whether the supplied Property catalog vector was explicitly initialized.</summary>
    public bool IsPropertyCatalogVectorInitialized => !propertyCatalogs.IsDefault;

    /// <summary>Gets the caller-declared accessibility certificate that governs every admission decision.</summary>
    public StaticFieldV2AccessibilityMode AccessibilityMode { get; }

    /// <summary>Gets the requesting assembly for the use-site certificate, or null for qualified inspection.</summary>
    public StaticFieldContainingAssemblyIdentity? RequestingAssembly { get; }

    /// <summary>Gets a defensive declaration-order copy of the caller-supplied friend-assembly grants.</summary>
    public ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrants =>
        ExpressionV2ContractEncoding.Copy(friendAssemblyGrants);

    /// <summary>Gets the optional interface-implementation portfolio, or null when none was supplied.</summary>
    public MetadataInterfaceImplementationPortfolioIdentity? InterfaceImplementationPortfolio { get; }

    /// <summary>Gets a defensive copy of the optional per-module token-resolution catalogs, default when absent.</summary>
    /// <remarks>
    /// The catalogs let this lookup decode a retained generic base TypeSpec and continue the walk through its
    /// exact substituted construction. When they are absent the walk keeps its previous behavior and a generic
    /// base terminal remains the incomplete-ancestry partial answer.
    /// </remarks>
    public ImmutableArray<MetadataSignatureTokenResolutionCatalog> TokenResolutionCatalogs =>
        tokenResolutionCatalogs.IsDefault ? default : ExpressionV2ContractEncoding.Copy(tokenResolutionCatalogs);

    /// <summary>Gets the optional exact closed construction of the requested owner, or null when absent.</summary>
    /// <remarks>
    /// The construction supplies the ordered closed arguments that bind owner variables inside the requested
    /// owner's own generic base TypeSpec. A null value admits only fully ground base TypeSpecs on the first hop.
    /// </remarks>
    public MetadataClosedTypeIdentity? OwnerClosedConstruction { get; }

    /// <summary>Gets a defensive copy of the caller-declared module edit states, or default when none were supplied.</summary>
    public ImmutableArray<StaticFieldV2ModuleEditDeclaration> ModuleEditDeclarations =>
        moduleEditDeclarations.IsDefault
            ? default
            : ExpressionV2ContractEncoding.Copy(moduleEditDeclarations);

    internal ImmutableArray<StaticFieldV2ModuleEditDeclaration> ModuleEditDeclarationsCore => moduleEditDeclarations;

    /// <summary>Gets a defensive copy of the fixed-reference canonical request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete definition-side static-field member-lookup request.</summary>
    /// <param name="ownerModule">The exact metadata module owning the requested TypeDef.</param>
    /// <param name="ownerTypeDefinitionToken">The exact non-nil owner TypeDef token.</param>
    /// <param name="fieldName">The decoded identifier compared ordinally against physical FieldDef names.</param>
    /// <param name="ancestryPortfolio">The ancestry authority portfolio prerequisite.</param>
    /// <param name="fieldCatalogs">
    /// Exactly one FieldDef catalog per ancestry-portfolio module in any order. A default array is admitted here and
    /// becomes a typed vector-shape stop rather than an exception, so callers can replay malformed input.
    /// </param>
    /// <param name="propertyCatalogs">
    /// Exactly one Property catalog per ancestry-portfolio module in any order, on the same terms as
    /// <paramref name="fieldCatalogs"/>. The vector is required: without it a level cannot prove that no property
    /// owns the requested name, and answering as though none did would be a fallback.
    /// </param>
    /// <param name="accessibilityMode">The caller-declared accessibility certificate.</param>
    /// <param name="requestingAssembly">
    /// The requesting assembly, required exactly for <see cref="StaticFieldV2AccessibilityMode.UseSiteCertificate"/>.
    /// </param>
    /// <param name="friendAssemblyGrants">
    /// The caller-supplied friend grants, admitted only for the use-site certificate and bounded by the shared cap.
    /// </param>
    /// <param name="interfaceImplementationPortfolio">
    /// The optional interface-implementation portfolio. Omitting it keeps the declared interface-ancestry
    /// coverage boundary and makes an interface owner examine only itself; supplying it walks the owner's bounded
    /// transitive interface closure after the owner itself.
    /// </param>
    /// <param name="tokenResolutionCatalogs">
    /// Optional per-module signature token-resolution catalogs. Supplying them lets the walk decode a retained
    /// generic base TypeSpec and continue through its exact substituted construction; omitting them keeps the
    /// previous incomplete-ancestry behavior at a generic base terminal.
    /// </param>
    /// <param name="ownerClosedConstruction">
    /// The optional exact closed construction of the requested owner, supplying the ordered closed arguments
    /// that bind owner variables inside the owner's own generic base TypeSpec.
    /// </param>
    /// <param name="moduleEditDeclarations">
    /// Optional caller-declared per-module edit states; a walked ancestry level in a declared edited module
    /// refuses with its typed stop before its rows are read.
    /// </param>
    /// <returns>A sealed immutable request with defensively copied evidence.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The owner token is not a TypeDef token or the mode is undefined.</exception>
    /// <exception cref="ArgumentException">The requesting assembly or grants disagree with the declared mode.</exception>
    public static StaticFieldV2MemberLookupRequest Create(
        StaticFieldMetadataModuleIdentity ownerModule,
        int ownerTypeDefinitionToken,
        DumpExpressionIdentifier fieldName,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs,
        StaticFieldV2AccessibilityMode accessibilityMode,
        StaticFieldContainingAssemblyIdentity? requestingAssembly = null,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants = default,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio = null,
        ImmutableArray<MetadataSignatureTokenResolutionCatalog> tokenResolutionCatalogs = default,
        MetadataClosedTypeIdentity? ownerClosedConstruction = null,
        ImmutableArray<StaticFieldV2ModuleEditDeclaration> moduleEditDeclarations = default)
    {
        ArgumentNullException.ThrowIfNull(ownerModule);
        ArgumentNullException.ThrowIfNull(fieldName);
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        CanonicalReplayEncoding.ValidateMetadataToken(ownerTypeDefinitionToken, 0x02, nameof(ownerTypeDefinitionToken));
        ExpressionV2ContractEncoding.RequireDefined(accessibilityMode, nameof(accessibilityMode));

        var useSite = accessibilityMode == StaticFieldV2AccessibilityMode.UseSiteCertificate;
        if (useSite == (requestingAssembly is null))
        {
            throw new ArgumentException(
                "A requesting assembly is required exactly for the use-site accessibility certificate.",
                nameof(requestingAssembly));
        }

        var grants = ExpressionV2ContractEncoding.CopyRequired(
            friendAssemblyGrants.IsDefault
                ? ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity>.Empty
                : friendAssemblyGrants,
            nameof(friendAssemblyGrants),
            StaticFieldV2Limits.MaximumFriendAssemblyDeclarationCount);
        if (!useSite && !grants.IsEmpty)
        {
            throw new ArgumentException(
                "Qualified inspection bypasses accessibility and therefore admits no friend-assembly grant.",
                nameof(friendAssemblyGrants));
        }

        var resolutionCatalogs = tokenResolutionCatalogs.IsDefault
            ? default
            : ExpressionV2ContractEncoding.CopyRequired(
                tokenResolutionCatalogs,
                nameof(tokenResolutionCatalogs),
                StaticFieldV2Limits.MaximumModuleCount);

        var catalogs = fieldCatalogs.IsDefault
            ? default
            : ImmutableArray.CreateRange(fieldCatalogs);
        var properties = propertyCatalogs.IsDefault
            ? default
            : ImmutableArray.CreateRange(propertyCatalogs);
        return new StaticFieldV2MemberLookupRequest(
            ownerModule,
            ownerTypeDefinitionToken,
            fieldName,
            ancestryPortfolio,
            catalogs,
            properties,
            accessibilityMode,
            requestingAssembly,
            grants,
            interfaceImplementationPortfolio,
            resolutionCatalogs,
            ownerClosedConstruction,
            moduleEditDeclarations.IsDefault
                ? default
                : ExpressionV2ContractEncoding.Copy(moduleEditDeclarations));
    }

    /// <summary>Tests canonical equality between two member-lookup requests.</summary>
    /// <param name="other">The other request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2MemberLookupRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests member-lookup request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberLookupRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical request content.</summary>
    /// <returns>A hash code for this canonical request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogsCore => fieldCatalogs;

    internal ImmutableArray<MetadataPropertyTableCatalogIdentity> PropertyCatalogsCore => propertyCatalogs;

    internal ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrantsCore =>
        friendAssemblyGrants;

    internal ImmutableArray<MetadataSignatureTokenResolutionCatalog> TokenResolutionCatalogsCore =>
        tokenResolutionCatalogs;
}

/// <summary>Freezes the complete outcome of one definition-side static-field member lookup.</summary>
/// <remarks>
/// This sealed outcome is the sole issuer of every examined candidate and consulted level it retains. The
/// selection walks the owner's bounded base chain most-derived-first, applies the declared accessibility certificate
/// before hiding is decided, and stops at the first level that declares an accessible same-name member.
/// <para>
/// A complete derived answer - <see cref="StaticFieldV2MemberLookupResultKind.Exact"/>,
/// <see cref="StaticFieldV2MemberLookupResultKind.Absent"/>,
/// <see cref="StaticFieldV2MemberLookupResultKind.Ambiguous"/>,
/// <see cref="StaticFieldV2MemberLookupResultKind.Partial"/>, or
/// <see cref="StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember"/> - retains its consulted levels and
/// its bounded ancestry terminal because those are the derivation itself. A stop -
/// <see cref="StaticFieldV2MemberLookupResultKind.NonExact"/>,
/// <see cref="StaticFieldV2MemberLookupResultKind.Invalid"/>, or
/// <see cref="StaticFieldV2MemberLookupResultKind.Unsupported"/> - is prefix-free and retains no level, candidate,
/// or terminal, because a partial derivation is not evidence about the requested name.
/// </para>
/// <para>
/// Declared coverage boundaries are informational facts retained by every outcome. A same-name property does block
/// here, from the module's complete Property table, but MethodSemantics is not modeled, so nothing tests whether its
/// accessors are reachable from the use site. The physical Event and EventMap tables are not modeled at all, so a
/// same-name event cannot block; the physical InternalsVisibleTo CustomAttribute table is likewise not modeled, so
/// friendship is caller-supplied.
/// </para>
/// </remarks>
public sealed class StaticFieldV2MemberLookupOutcome : IEquatable<StaticFieldV2MemberLookupOutcome>
{
    /// <summary>Gets the shared accessibility-decision cap applied by one complete lookup.</summary>
    public const int MaximumAccessibilityCheckCount = StaticFieldV2Limits.MaximumAccessibilityCheckCount;

    private const string CanonicalDomain = "static-field-v2-member-lookup-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2MemberLookupLevelIdentity> levels;
    private readonly ImmutableArray<StaticFieldV2MemberCandidateIdentity> rejectedCandidates;
    private readonly ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2MemberLookupOutcome(
        StaticFieldV2MemberLookupResultKind resultKind,
        StaticFieldV2MemberLookupIssue issue,
        StaticFieldV2MemberLookupRequest request,
        StaticFieldV2MemberCandidateIdentity? selectedCandidate,
        ImmutableArray<StaticFieldV2MemberLookupLevelIdentity> levels,
        ImmutableArray<StaticFieldV2MemberCandidateIdentity> rejectedCandidates,
        MetadataAncestryChainTerminalKind? ancestryTerminal,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        SelectedCandidate = selectedCandidate;
        this.levels = ExpressionV2ContractEncoding.Copy(levels);
        this.rejectedCandidates = ExpressionV2ContractEncoding.Copy(rejectedCandidates);
        AncestryTerminal = ancestryTerminal;
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedCandidate?.Sha256);
        writer.WriteInt32(levels.Length);
        foreach (var level in levels)
        {
            writer.WriteSha256(level.Sha256, nameof(levels));
        }
        writer.WriteInt32(rejectedCandidates.Length);
        foreach (var candidate in rejectedCandidates)
        {
            writer.WriteSha256(candidate.Sha256, nameof(rejectedCandidates));
        }
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, ancestryTerminal);
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this lookup is exact, absent, ambiguous, partial, hidden, or a typed stop.</summary>
    public StaticFieldV2MemberLookupResultKind ResultKind { get; }

    /// <summary>Gets the typed lookup issue, or none for an exact outcome.</summary>
    public StaticFieldV2MemberLookupIssue Issue { get; }

    /// <summary>Gets the complete request that produced this outcome.</summary>
    public StaticFieldV2MemberLookupRequest Request { get; }

    /// <summary>Gets the single selected static-field candidate, or null for anything but an exact answer.</summary>
    public StaticFieldV2MemberCandidateIdentity? SelectedCandidate { get; }

    /// <summary>Gets a defensive most-derived-first copy of every consulted level, empty for a stop.</summary>
    public ImmutableArray<StaticFieldV2MemberLookupLevelIdentity> Levels =>
        ExpressionV2ContractEncoding.Copy(levels);

    /// <summary>Gets a defensive examination-order copy of every examined but unselected candidate.</summary>
    public ImmutableArray<StaticFieldV2MemberCandidateIdentity> RejectedCandidates =>
        ExpressionV2ContractEncoding.Copy(rejectedCandidates);

    /// <summary>Gets the bounded ancestry terminal of the consulted chain, or null for a prefix-free stop.</summary>
    public MetadataAncestryChainTerminalKind? AncestryTerminal { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this answer.</summary>
    public ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the declared bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>
    /// Gets the count of same-name FieldDef and MethodDef declarations examined for a complete answer, or the
    /// propagated prerequisite, supplied, or cap-plus-one observation for a typed stop.
    /// </summary>
    public int ObservedCount { get; }

    /// <summary>Gets the metadata token related to this answer or stop, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two member-lookup outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2MemberLookupOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests member-lookup outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberLookupOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical outcome content.</summary>
    /// <returns>A hash code for this canonical outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2MemberCandidateIdentity IssueCandidate(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int levelIndex,
        bool isAccessible,
        MetadataFieldAccessibility effectiveAccessibility,
        StaticFieldV2FieldStorageShape storageShape,
        MetadataClosedTypeIdentity? declaringConstruction = null) =>
        StaticFieldV2MemberCandidateIdentity.Create(
            RowMintCapability,
            fieldRow,
            declaringTypeDefinition,
            levelIndex,
            isAccessible,
            effectiveAccessibility,
            storageShape,
            declaringConstruction);

    internal static StaticFieldV2MemberLookupLevelIdentity IssueLevel(
        int levelIndex,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int examinedFieldCount,
        int examinedMethodCount,
        int examinedPropertyCount,
        int accessibleCandidateCount) =>
        StaticFieldV2MemberLookupLevelIdentity.Create(
            RowMintCapability,
            levelIndex,
            declaringTypeDefinition,
            examinedFieldCount,
            examinedMethodCount,
            examinedPropertyCount,
            accessibleCandidateCount);

    internal static StaticFieldV2MemberLookupOutcome IssueComplete(
        StaticFieldV2MemberLookupResultKind resultKind,
        StaticFieldV2MemberLookupIssue issue,
        StaticFieldV2MemberLookupRequest request,
        StaticFieldV2MemberCandidateIdentity? selectedCandidate,
        ImmutableArray<StaticFieldV2MemberLookupLevelIdentity> levels,
        ImmutableArray<StaticFieldV2MemberCandidateIdentity> rejectedCandidates,
        MetadataAncestryChainTerminalKind ancestryTerminal,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> declaredCoverageBoundaries,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            request,
            selectedCandidate,
            levels,
            rejectedCandidates,
            ancestryTerminal,
            declaredCoverageBoundaries,
            null,
            observedCount,
            relatedMetadataToken);

    internal static StaticFieldV2MemberLookupOutcome IssueStop(
        StaticFieldV2MemberLookupResultKind resultKind,
        StaticFieldV2MemberLookupIssue issue,
        StaticFieldV2MemberLookupRequest request,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            request,
            null,
            [],
            [],
            null,
            declaredCoverageBoundaries,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}

/// <summary>Selects one static-field declaration over an owner's bounded base chain from definitions alone.</summary>
/// <remarks>
/// This binder owns definition-kind-specific member lookup only. It walks the owner's bounded ancestry chain
/// most-derived-first, applies the declared accessibility certificate before hiding is decided, refuses to skip a
/// blocking same-name method or instance field, and never claims absence over an incomplete chain.
/// <para>
/// Declared coverage boundaries of this slice: a same-name property blocks by name alone, because MethodSemantics is
/// not modeled and nothing can ask whether its accessors are reachable from the use site; the physical Event and
/// EventMap tables are not modeled, so a same-name event cannot block at all; and the physical InternalsVisibleTo
/// CustomAttribute table is not modeled, so friend grants are caller-supplied certificates. Interface ancestry is not a class base chain:
/// an interface owner walks its bounded transitive interface closure when the request supplies an
/// interface-implementation portfolio, and otherwise examines only itself under the declared interface-ancestry
/// boundary. An interface closure whose terminal is not complete can never produce an absent answer, exactly as an
/// incomplete class chain cannot.
/// </para>
/// <para>
/// Because no use-site type exists in a definition-only phase, the use-site certificate refuses every private,
/// family, and family-and-assembly declaration, and refuses compiler-controlled declarations outright. Those
/// refusals are recorded per candidate rather than silently skipped, and qualified inspection admits all of them.
/// </para>
/// </remarks>
public static class StaticFieldV2MemberLookup
{
    private const int TypeVisibilityMask = 0x0000_0007;
    private const int MemberAccessMask = 0x0000_0007;

    /// <summary>Selects exactly one static field declaration for one owner TypeDef and one field identifier.</summary>
    /// <param name="request">The complete definition-side member-lookup request.</param>
    /// <remarks>
    /// The walk is exhaustive within each consulted level: every declared FieldDef and MethodDef row of the level's
    /// declaring type is examined before any answer is formed, so no first-match heuristic can influence the result.
    /// A level that declares only inaccessible same-name members hides nothing under the use-site certificate, while
    /// qualified inspection makes every such declaration accessible and therefore hiding.
    /// </remarks>
    /// <returns>A sealed immutable outcome that is either one complete answer or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2MemberLookupOutcome SelectStaticField(StaticFieldV2MemberLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = BaseBoundaries(request);
        var portfolio = request.AncestryPortfolio;
        if (portfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.NonExact)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.NonExact,
                StaticFieldV2MemberLookupIssue.AncestryPortfolioNonExact,
                boundaries,
                null,
                portfolio.ObservedCount,
                null);
        }
        if (portfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.Invalid,
                StaticFieldV2MemberLookupIssue.AncestryPortfolioInvalid,
                boundaries,
                null,
                portfolio.ObservedCount,
                null);
        }

        if (request.InterfaceImplementationPortfolio is { } interfacePortfolio &&
            interfacePortfolio.ResultKind != MetadataInterfaceImplementationPortfolioResultKind.Exact)
        {
            var invalid = interfacePortfolio.ResultKind ==
                MetadataInterfaceImplementationPortfolioResultKind.Invalid;
            return Stop(
                request,
                invalid
                    ? StaticFieldV2MemberLookupResultKind.Invalid
                    : StaticFieldV2MemberLookupResultKind.NonExact,
                invalid
                    ? StaticFieldV2MemberLookupIssue.InterfaceImplementationPortfolioInvalid
                    : StaticFieldV2MemberLookupIssue.InterfaceImplementationPortfolioNonExact,
                boundaries,
                interfacePortfolio.ReachedBound,
                interfacePortfolio.ObservedCount,
                null);
        }

        var vector = ValidateCatalogVector(request, boundaries, out var catalogsByModule, out var authorityByModule);
        if (vector is not null)
        {
            return vector;
        }

        var propertyVector = ValidatePropertyCatalogVector(
            request,
            boundaries,
            authorityByModule,
            out var propertyCatalogsByModule);
        if (propertyVector is not null)
        {
            return propertyVector;
        }

        if (!authorityByModule.ContainsKey(request.OwnerModule))
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.Invalid,
                StaticFieldV2MemberLookupIssue.OwnerModuleNotInPortfolio,
                boundaries,
                null,
                authorityByModule.Count,
                request.OwnerTypeDefinitionToken);
        }

        var classification = portfolio.ExactClassificationOrDefault(
            request.OwnerModule,
            request.OwnerTypeDefinitionToken);
        var chain = portfolio.ExactAncestryChainOrDefault(
            request.OwnerModule,
            request.OwnerTypeDefinitionToken);
        if (classification is null || chain is null)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.Invalid,
                StaticFieldV2MemberLookupIssue.OwnerTypeDefinitionNotIssued,
                boundaries,
                null,
                0,
                request.OwnerTypeDefinitionToken);
        }
        if (classification.Role == MetadataTypeDefinitionSemanticRole.ModulePseudoType)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.Unsupported,
                StaticFieldV2MemberLookupIssue.ModulePseudoTypeOwner,
                boundaries,
                null,
                0,
                request.OwnerTypeDefinitionToken);
        }
        var isInterfaceOwner = classification.Role == MetadataTypeDefinitionSemanticRole.Interface;
        if (isInterfaceOwner && request.InterfaceImplementationPortfolio is null)
        {
            boundaries = Add(boundaries, StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled);
        }

        var plan = isInterfaceOwner && request.InterfaceImplementationPortfolio is { } closurePortfolio
            ? InterfaceLevelPlan(request, chain, closurePortfolio, catalogsByModule)
            : ClassLevelPlan(chain);
        return Walk(
            request,
            boundaries,
            chain,
            plan,
            catalogsByModule,
            propertyCatalogsByModule,
            authorityByModule,
            allowGenericBaseContinuation: !isInterfaceOwner && !request.TokenResolutionCatalogsCore.IsDefault);
    }

    private static LevelPlan ClassLevelPlan(MetadataTypeDefinitionAncestryChainIdentity chain)
    {
        var levels = ImmutableArray.CreateBuilder<LookupLevel>(chain.Edges.Length + 1);
        levels.Add(new LookupLevel(chain.SourceModule, chain.Subject));
        foreach (var edge in chain.Edges)
        {
            levels.Add(new LookupLevel(edge.SourceModule, edge.Owner));
        }
        var complete = chain.TerminalKind is MetadataAncestryChainTerminalKind.SystemObjectReached
            or MetadataAncestryChainTerminalKind.InterfaceRoot
            or MetadataAncestryChainTerminalKind.ModulePseudoTypeRoot;
        return new LevelPlan(levels.MoveToImmutable(), complete);
    }

    private static LevelPlan InterfaceLevelPlan(
        StaticFieldV2MemberLookupRequest request,
        MetadataTypeDefinitionAncestryChainIdentity chain,
        MetadataInterfaceImplementationPortfolioIdentity portfolio,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataFieldDefinitionTableCatalogIdentity> catalogsByModule)
    {
        var closure = portfolio.ExactImplementedInterfacesOrDefault(
            request.OwnerModule,
            request.OwnerTypeDefinitionToken);
        var levels = ImmutableArray.CreateBuilder<LookupLevel>();
        levels.Add(new LookupLevel(chain.SourceModule, chain.Subject));
        if (closure is null)
        {
            return new LevelPlan(levels.ToImmutable(), false);
        }

        var complete = closure.TerminalKind == MetadataInterfaceImplementationClosureTerminalKind.Complete;
        foreach (var edge in closure.InterfaceEdges)
        {
            if (edge.TargetModule is not { } module ||
                edge.TargetTypeDefinition is not { } definition ||
                !catalogsByModule.ContainsKey(module))
            {
                complete = false;
                continue;
            }
            levels.Add(new LookupLevel(module, definition));
        }
        return new LevelPlan(levels.ToImmutable(), complete);
    }

    private static StaticFieldV2MemberLookupOutcome Walk(
        StaticFieldV2MemberLookupRequest request,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> boundaries,
        MetadataTypeDefinitionAncestryChainIdentity chain,
        LevelPlan plan,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataFieldDefinitionTableCatalogIdentity> catalogsByModule,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataPropertyTableCatalogIdentity> propertyCatalogsByModule,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataDefinitionAuthorityCatalogIdentity> authorityByModule,
        bool allowGenericBaseContinuation)
    {
        var name = request.FieldName.DecodedText;
        var levels = ImmutableArray.CreateBuilder<StaticFieldV2MemberLookupLevelIdentity>();
        var examined = new List<StaticFieldV2MemberCandidateIdentity>();
        var winningStaticFields = new List<StaticFieldV2MemberCandidateIdentity>();
        StaticFieldV2MemberCandidateIdentity? winningInstanceField = null;
        var winningMethodToken = 0;
        var winningPropertyToken = 0;
        var decisionCount = 0;
        var observedCount = 0;
        var resolved = false;

        var walkLevels = new List<LookupLevel>(plan.Levels);
        var complete = plan.IsComplete;
        var currentChain = chain;
        var continuationIssue = StaticFieldV2MemberLookupIssue.AncestryIncomplete;
        int? continuationToken = null;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var level in walkLevels)
        {
            visited.Add(LevelKey(level.SourceModule, level.DeclaringTypeDefinition.TypeDefinitionToken));
        }

        for (var levelIndex = 0; levelIndex < walkLevels.Count; levelIndex++)
        {
            var walkModule = walkLevels[levelIndex].SourceModule;

            // A walked level in a module declared with applied edit generations refuses before its rows are read:
            // the owner module was already refused at construction, so this guards the cross-module ancestry case
            // where a base level lives in a different, edited module.
            if (!request.ModuleEditDeclarationsCore.IsDefault &&
                request.ModuleEditDeclarationsCore.Any(declaration =>
                    declaration.EditState.HasAppliedEdits && declaration.MetadataModule.Equals(walkModule)))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.NonExact,
                    StaticFieldV2MemberLookupIssue.BaseModuleEditedGenerationsNotComposed,
                    boundaries,
                    null,
                    observedCount,
                    walkLevels[levelIndex].DeclaringTypeDefinition.TypeDefinitionToken);
            }

            var walkType = walkLevels[levelIndex].DeclaringTypeDefinition;
            var walkConstruction = walkLevels[levelIndex].DeclaringConstruction;
            var catalog = catalogsByModule[walkModule];
            var authority = authorityByModule[walkModule];
            var fieldRows = catalog.RowsForDeclaringTypeOrEmpty(walkType);
            var methodTokens = walkType.MethodDefinitionTokens;
            var propertyRows = propertyCatalogsByModule[walkModule].RowsForDeclaringTypeOrEmpty(walkType);
            var accessibleCandidateCount = 0;
            var levelStaticFields = new List<StaticFieldV2MemberCandidateIdentity>();
            StaticFieldV2MemberCandidateIdentity? levelInstanceField = null;
            var levelMethodToken = 0;
            var levelPropertyToken = 0;

            foreach (var fieldRow in fieldRows)
            {
                if (!string.Equals(fieldRow.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                observedCount++;
                if (++decisionCount > StaticFieldV2MemberLookupOutcome.MaximumAccessibilityCheckCount)
                {
                    return AccessibilityBoundStop(request, boundaries);
                }

                var effective = EffectiveAccessibility(fieldRow.DeclaredAccessibility, walkType, authority);
                var accessible = IsAccessible(request, effective, walkModule);
                var candidate = StaticFieldV2MemberLookupOutcome.IssueCandidate(
                    fieldRow,
                    walkType,
                    levelIndex,
                    accessible,
                    effective,
                    StorageShape(fieldRow),
                    walkConstruction);
                examined.Add(candidate);
                if (!accessible)
                {
                    continue;
                }

                accessibleCandidateCount++;
                if (fieldRow.IsStatic)
                {
                    levelStaticFields.Add(candidate);
                }
                else
                {
                    levelInstanceField ??= candidate;
                }
            }

            foreach (var methodToken in methodTokens)
            {
                var method = authority.ExactMethodDefinitionOrDefault(methodToken);
                if (method is null ||
                    !string.Equals(method.TableRow.Observation.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                observedCount++;
                if (++decisionCount > StaticFieldV2MemberLookupOutcome.MaximumAccessibilityCheckCount)
                {
                    return AccessibilityBoundStop(request, boundaries);
                }

                var declared = DecodeAccessibility(method.TableRow.Observation.Attributes & MemberAccessMask);
                var effective = EffectiveAccessibility(declared, walkType, authority);
                if (!IsAccessible(request, effective, walkModule))
                {
                    continue;
                }

                accessibleCandidateCount++;
                if (levelMethodToken == 0)
                {
                    levelMethodToken = methodToken;
                }
            }

            // The third pass. A property blocks by name alone: MethodSemantics is not modeled, so nothing here can
            // ask whether its accessors are reachable, and the declared PropertyAccessorSemanticsNotModeled boundary
            // states exactly that. The pass charges no accessibility decision, because it makes none - charging the
            // decision counter would make the accessibility-check bound assert work that never happened. A property's
            // accessors are named get_P and set_P and so can never have matched the method pass above: this is
            // genuinely new blocking, not a second reading of evidence already consulted.
            foreach (var propertyRow in propertyRows)
            {
                if (!string.Equals(propertyRow.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                observedCount++;
                accessibleCandidateCount++;
                if (levelPropertyToken == 0)
                {
                    levelPropertyToken = propertyRow.PropertyToken;
                }
            }

            levels.Add(StaticFieldV2MemberLookupOutcome.IssueLevel(
                levelIndex,
                walkType,
                fieldRows.Length,
                methodTokens.Length,
                propertyRows.Length,
                accessibleCandidateCount));

            if (accessibleCandidateCount > 0)
            {
                winningStaticFields.AddRange(levelStaticFields);
                winningInstanceField = levelInstanceField;
                winningMethodToken = levelMethodToken;
                winningPropertyToken = levelPropertyToken;
                resolved = true;
                break;
            }

            // At the last known level, one generic-base continuation may extend the walk before it concludes. The
            // continuation engages only when the caller supplied token-resolution catalogs, so every request without
            // them keeps the previous incomplete-ancestry behavior at a generic base terminal.
            if (levelIndex == walkLevels.Count - 1 &&
                allowGenericBaseContinuation &&
                !complete &&
                currentChain.TerminalKind == MetadataAncestryChainTerminalKind.GenericBaseReached)
            {
                var extension = ExtendThroughGenericBase(
                    request,
                    currentChain,
                    walkLevels,
                    visited,
                    catalogsByModule,
                    authorityByModule);
                if (extension.Issue != StaticFieldV2MemberLookupIssue.None)
                {
                    continuationIssue = extension.Issue;
                    continuationToken = extension.RelatedMetadataToken;
                    break;
                }

                walkLevels.AddRange(extension.Levels);
                currentChain = extension.NextChain!;
                complete = currentChain.TerminalKind is MetadataAncestryChainTerminalKind.SystemObjectReached
                    or MetadataAncestryChainTerminalKind.InterfaceRoot
                    or MetadataAncestryChainTerminalKind.ModulePseudoTypeRoot;
            }
        }

        var consultedLevels = levels.ToImmutable();
        if (!resolved)
        {
            return StaticFieldV2MemberLookupOutcome.IssueComplete(
                complete
                    ? StaticFieldV2MemberLookupResultKind.Absent
                    : StaticFieldV2MemberLookupResultKind.Partial,
                complete
                    ? StaticFieldV2MemberLookupIssue.DeclarationAbsent
                    : continuationIssue,
                request,
                null,
                consultedLevels,
                [.. examined],
                currentChain.TerminalKind,
                boundaries,
                observedCount,
                continuationToken);
        }

        if (winningMethodToken != 0)
        {
            return StaticFieldV2MemberLookupOutcome.IssueComplete(
                StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember,
                StaticFieldV2MemberLookupIssue.HiddenByDeclaredMethod,
                request,
                null,
                consultedLevels,
                [.. examined],
                currentChain.TerminalKind,
                boundaries,
                observedCount,
                winningMethodToken);
        }
        if (winningInstanceField is not null)
        {
            return StaticFieldV2MemberLookupOutcome.IssueComplete(
                StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember,
                StaticFieldV2MemberLookupIssue.HiddenByInstanceField,
                request,
                null,
                consultedLevels,
                [.. examined],
                currentChain.TerminalKind,
                boundaries,
                observedCount,
                winningInstanceField.FieldDefinitionToken);
        }
        if (winningStaticFields.Count > 1)
        {
            return StaticFieldV2MemberLookupOutcome.IssueComplete(
                StaticFieldV2MemberLookupResultKind.Ambiguous,
                StaticFieldV2MemberLookupIssue.AmbiguousStaticDeclarations,
                request,
                null,
                consultedLevels,
                [.. examined],
                currentChain.TerminalKind,
                boundaries,
                observedCount,
                winningStaticFields[0].FieldDefinitionToken);
        }

        // Last, deliberately. HiddenByDeclaredMethod, HiddenByInstanceField, AmbiguousStaticDeclarations, Absent and
        // Partial all keep their exact issue values; the only answer this branch changes is a level where a property
        // owns the name and nothing nearer claimed it. A level declaring both an accessible static field and a
        // same-name property is illegal in C# but legal in IL, and there the property is reported: the conservative
        // answer, since the field cannot be shown to be the one a use site would bind.
        if (winningPropertyToken != 0)
        {
            return StaticFieldV2MemberLookupOutcome.IssueComplete(
                StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember,
                StaticFieldV2MemberLookupIssue.HiddenByDeclaredProperty,
                request,
                null,
                consultedLevels,
                [.. examined],
                currentChain.TerminalKind,
                boundaries,
                observedCount,
                winningPropertyToken);
        }

        var selected = winningStaticFields[0];
        var rejected = ImmutableArray.CreateBuilder<StaticFieldV2MemberCandidateIdentity>();
        foreach (var candidate in examined)
        {
            if (!ReferenceEquals(candidate, selected))
            {
                rejected.Add(candidate);
            }
        }
        return StaticFieldV2MemberLookupOutcome.IssueComplete(
            StaticFieldV2MemberLookupResultKind.Exact,
            StaticFieldV2MemberLookupIssue.None,
            request,
            selected,
            consultedLevels,
            rejected.ToImmutable(),
            currentChain.TerminalKind,
            boundaries,
            observedCount,
            selected.FieldDefinitionToken);
    }

    private static StaticFieldV2MemberLookupOutcome? ValidatePropertyCatalogVector(
        StaticFieldV2MemberLookupRequest request,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> boundaries,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataDefinitionAuthorityCatalogIdentity> authorityByModule,
        out Dictionary<StaticFieldMetadataModuleIdentity, MetadataPropertyTableCatalogIdentity> propertyCatalogsByModule)
    {
        propertyCatalogsByModule = [];

        var entries = request.AncestryPortfolio.Entries;
        var catalogs = request.PropertyCatalogsCore;
        if (catalogs.IsDefault)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.NonExact,
                StaticFieldV2MemberLookupIssue.PropertyCatalogVectorUninitialized,
                boundaries,
                null,
                0,
                null);
        }
        if (catalogs.Length > StaticFieldV2Limits.MaximumModuleCount)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.NonExact,
                StaticFieldV2MemberLookupIssue.PropertyCatalogModuleCountBoundReached,
                boundaries,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ModuleCountBoundName,
                    StaticFieldV2Limits.MaximumModuleCount),
                StaticFieldV2Limits.MaximumModuleCount + 1,
                null);
        }
        if (catalogs.Length < entries.Length)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.NonExact,
                StaticFieldV2MemberLookupIssue.PropertyCatalogSlotsIncomplete,
                boundaries,
                null,
                catalogs.Length,
                null);
        }
        if (catalogs.Length > entries.Length)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.Invalid,
                StaticFieldV2MemberLookupIssue.PropertyCatalogSlotCountConflict,
                boundaries,
                null,
                catalogs.Length,
                null);
        }
        foreach (var catalog in catalogs)
        {
            if (catalog is null)
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.NonExact,
                    StaticFieldV2MemberLookupIssue.PropertyCatalogMissing,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
        }
        foreach (var catalog in catalogs)
        {
            if (catalog.ResultKind == MetadataPropertyTableResultKind.NonExact)
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.NonExact,
                    StaticFieldV2MemberLookupIssue.PropertyCatalogNonExact,
                    boundaries,
                    catalog.ReachedBound,
                    catalog.ObservedCount,
                    catalog.RelatedMetadataToken);
            }
        }
        foreach (var catalog in catalogs)
        {
            if (catalog.ResultKind == MetadataPropertyTableResultKind.Invalid)
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.PropertyCatalogInvalid,
                    boundaries,
                    null,
                    catalog.ObservedCount,
                    catalog.RelatedMetadataToken);
            }
        }

        foreach (var catalog in catalogs)
        {
            var module = catalog.DeclaredMemberSourceEnds.SourceModule;
            if (!propertyCatalogsByModule.TryAdd(module, catalog))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.DuplicatePropertyCatalogModule,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
            if (!authorityByModule.TryGetValue(module, out var authority))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.PropertyCatalogModuleNotInPortfolio,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
            if (!catalog.DeclaredMemberSourceEnds.DefinitionSourceEnds.Equals(authority.SourceEnds))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.PropertyCatalogSourceEndsMismatch,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
            if (!catalog.DefinitionAuthority.Equals(authority))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.PropertyCatalogAuthorityMismatch,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
        }

        return null;
    }

    private static StaticFieldV2MemberLookupOutcome? ValidateCatalogVector(
        StaticFieldV2MemberLookupRequest request,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> boundaries,
        out Dictionary<StaticFieldMetadataModuleIdentity, MetadataFieldDefinitionTableCatalogIdentity> catalogsByModule,
        out Dictionary<StaticFieldMetadataModuleIdentity, MetadataDefinitionAuthorityCatalogIdentity> authorityByModule)
    {
        catalogsByModule = [];
        authorityByModule = [];

        var entries = request.AncestryPortfolio.Entries;
        foreach (var entry in entries)
        {
            authorityByModule[entry.SourceModule] =
                entry.ResolutionEntry.ChainEntry.ChainCatalog.DefinitionAuthority;
        }

        var catalogs = request.FieldCatalogsCore;
        if (catalogs.IsDefault)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.NonExact,
                StaticFieldV2MemberLookupIssue.FieldCatalogVectorUninitialized,
                boundaries,
                null,
                0,
                null);
        }
        if (catalogs.Length > StaticFieldV2Limits.MaximumModuleCount)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.NonExact,
                StaticFieldV2MemberLookupIssue.FieldCatalogModuleCountBoundReached,
                boundaries,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ModuleCountBoundName,
                    StaticFieldV2Limits.MaximumModuleCount),
                StaticFieldV2Limits.MaximumModuleCount + 1,
                null);
        }
        if (catalogs.Length < entries.Length)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.NonExact,
                StaticFieldV2MemberLookupIssue.FieldCatalogSlotsIncomplete,
                boundaries,
                null,
                catalogs.Length,
                null);
        }
        if (catalogs.Length > entries.Length)
        {
            return Stop(
                request,
                StaticFieldV2MemberLookupResultKind.Invalid,
                StaticFieldV2MemberLookupIssue.FieldCatalogSlotCountConflict,
                boundaries,
                null,
                catalogs.Length,
                null);
        }
        foreach (var catalog in catalogs)
        {
            if (catalog is null)
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.NonExact,
                    StaticFieldV2MemberLookupIssue.FieldCatalogMissing,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
        }
        foreach (var catalog in catalogs)
        {
            if (catalog.ResultKind == MetadataFieldDefinitionTableResultKind.NonExact)
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.NonExact,
                    StaticFieldV2MemberLookupIssue.FieldCatalogNonExact,
                    boundaries,
                    catalog.ReachedBound,
                    catalog.ObservedCount,
                    catalog.RelatedMetadataToken);
            }
        }
        foreach (var catalog in catalogs)
        {
            if (catalog.ResultKind == MetadataFieldDefinitionTableResultKind.Invalid)
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.FieldCatalogInvalid,
                    boundaries,
                    null,
                    catalog.ObservedCount,
                    catalog.RelatedMetadataToken);
            }
        }

        foreach (var catalog in catalogs)
        {
            var module = catalog.SourceEnds.SourceModule;
            if (!catalogsByModule.TryAdd(module, catalog))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.DuplicateFieldCatalogModule,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
            if (!authorityByModule.TryGetValue(module, out var authority))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.FieldCatalogModuleNotInPortfolio,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
            if (!catalog.SourceEnds.Equals(authority.SourceEnds))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.FieldCatalogSourceEndsMismatch,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
            if (!catalog.DefinitionAuthority.Equals(authority))
            {
                return Stop(
                    request,
                    StaticFieldV2MemberLookupResultKind.Invalid,
                    StaticFieldV2MemberLookupIssue.FieldCatalogAuthorityMismatch,
                    boundaries,
                    null,
                    catalogs.Length,
                    null);
            }
        }

        return null;
    }

    private static StaticFieldV2MemberLookupOutcome AccessibilityBoundStop(
        StaticFieldV2MemberLookupRequest request,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> boundaries) =>
        Stop(
            request,
            StaticFieldV2MemberLookupResultKind.NonExact,
            StaticFieldV2MemberLookupIssue.AccessibilityCheckBoundReached,
            boundaries,
            new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.AccessibilityCheckCountBoundName,
                StaticFieldV2MemberLookupOutcome.MaximumAccessibilityCheckCount),
            StaticFieldV2MemberLookupOutcome.MaximumAccessibilityCheckCount + 1,
            null);

    private static StaticFieldV2MemberLookupOutcome Stop(
        StaticFieldV2MemberLookupRequest request,
        StaticFieldV2MemberLookupResultKind resultKind,
        StaticFieldV2MemberLookupIssue issue,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> boundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        StaticFieldV2MemberLookupOutcome.IssueStop(
            resultKind,
            issue,
            request,
            boundaries,
            reachedBound,
            observedCount,
            relatedMetadataToken);

    private static ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> BaseBoundaries(
        StaticFieldV2MemberLookupRequest request) =>
        request.AccessibilityMode == StaticFieldV2AccessibilityMode.UseSiteCertificate
            // Ascending, which DeclaredCoverageBoundaries documents and Add preserves by sorting. The two new
            // values are numerically above the mode-specific one, so they are appended after it rather than
            // before, or one boundary set would be recorded in a different byte order than the same set reached
            // through the interface-ancestry path.
            ? [
                StaticFieldV2MemberLookupCoverageBoundary.FriendAssemblyAttributesNotModeled,
                StaticFieldV2MemberLookupCoverageBoundary.EventTablesNotModeled,
                StaticFieldV2MemberLookupCoverageBoundary.PropertyAccessorSemanticsNotModeled,
            ]
            : [
                StaticFieldV2MemberLookupCoverageBoundary.AccessibilityBypassApplied,
                StaticFieldV2MemberLookupCoverageBoundary.EventTablesNotModeled,
                StaticFieldV2MemberLookupCoverageBoundary.PropertyAccessorSemanticsNotModeled,
            ];

    private static ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> Add(
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> boundaries,
        StaticFieldV2MemberLookupCoverageBoundary boundary)
    {
        if (boundaries.Contains(boundary))
        {
            return boundaries;
        }

        var extended = new List<StaticFieldV2MemberLookupCoverageBoundary>(boundaries) { boundary };
        extended.Sort();
        return [.. extended];
    }

    private static StaticFieldV2FieldStorageShape StorageShape(MetadataFieldDefinitionTableRowIdentity row) =>
        row.IsLiteral
            ? StaticFieldV2FieldStorageShape.MetadataLiteral
            : row.HasFieldRva
                ? StaticFieldV2FieldStorageShape.ModuleRvaCandidate
                : StaticFieldV2FieldStorageShape.StoredSlot;

    private static bool IsAccessible(
        StaticFieldV2MemberLookupRequest request,
        MetadataFieldAccessibility effective,
        StaticFieldMetadataModuleIdentity definingModule)
    {
        if (request.AccessibilityMode == StaticFieldV2AccessibilityMode.QualifiedInspectionBypass)
        {
            return true;
        }

        return effective switch
        {
            MetadataFieldAccessibility.Public => true,
            MetadataFieldAccessibility.Assembly or MetadataFieldAccessibility.FamilyOrAssembly =>
                HasAssemblyAccess(request, definingModule),
            _ => false,
        };
    }

    private static bool HasAssemblyAccess(
        StaticFieldV2MemberLookupRequest request,
        StaticFieldMetadataModuleIdentity definingModule)
    {
        var requesting = request.RequestingAssembly!;
        var defining = definingModule.ContainingAssembly;
        if (requesting.Equals(defining))
        {
            return true;
        }

        foreach (var grant in request.FriendAssemblyGrantsCore)
        {
            if (grant.DefiningAssembly.Equals(defining) && grant.Declares(requesting.AssemblyDefinition))
            {
                return true;
            }
        }
        return false;
    }

    private static MetadataFieldAccessibility EffectiveAccessibility(
        MetadataFieldAccessibility declared,
        MetadataTypeDefinitionAuthorityIdentity declaringType,
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var effective = declared;
        var current = declaringType;
        for (var depth = 0; depth <= StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth; depth++)
        {
            effective = Intersect(
                effective,
                DecodeTypeVisibility(current.TableRow.Observation.TypeAttributes & TypeVisibilityMask));
            if (current.EnclosingTypeDefinitionToken is not { } enclosing ||
                authority.ExactTypeDefinitionOrDefault(enclosing) is not { } parent)
            {
                break;
            }
            current = parent;
        }
        return effective;
    }

    private static MetadataFieldAccessibility DecodeTypeVisibility(int visibility) => visibility switch
    {
        0x0000 => MetadataFieldAccessibility.Assembly,
        0x0001 => MetadataFieldAccessibility.Public,
        0x0002 => MetadataFieldAccessibility.Public,
        0x0003 => MetadataFieldAccessibility.Private,
        0x0004 => MetadataFieldAccessibility.Family,
        0x0005 => MetadataFieldAccessibility.Assembly,
        0x0006 => MetadataFieldAccessibility.FamilyAndAssembly,
        _ => MetadataFieldAccessibility.FamilyOrAssembly,
    };

    private static MetadataFieldAccessibility DecodeAccessibility(int access) => access switch
    {
        0x0000 => MetadataFieldAccessibility.CompilerControlled,
        0x0001 => MetadataFieldAccessibility.Private,
        0x0002 => MetadataFieldAccessibility.FamilyAndAssembly,
        0x0003 => MetadataFieldAccessibility.Assembly,
        0x0004 => MetadataFieldAccessibility.Family,
        0x0005 => MetadataFieldAccessibility.FamilyOrAssembly,
        _ => MetadataFieldAccessibility.Public,
    };

    private static MetadataFieldAccessibility Intersect(
        MetadataFieldAccessibility left,
        MetadataFieldAccessibility right)
    {
        if (left == MetadataFieldAccessibility.CompilerControlled ||
            right == MetadataFieldAccessibility.CompilerControlled)
        {
            return MetadataFieldAccessibility.CompilerControlled;
        }
        if (left == MetadataFieldAccessibility.Private || right == MetadataFieldAccessibility.Private)
        {
            return MetadataFieldAccessibility.Private;
        }
        if (left == MetadataFieldAccessibility.Public)
        {
            return right;
        }
        if (right == MetadataFieldAccessibility.Public)
        {
            return left;
        }
        if (left == right)
        {
            return left;
        }
        if (left == MetadataFieldAccessibility.FamilyAndAssembly ||
            right == MetadataFieldAccessibility.FamilyAndAssembly)
        {
            return MetadataFieldAccessibility.FamilyAndAssembly;
        }
        if (left == MetadataFieldAccessibility.FamilyOrAssembly)
        {
            return right;
        }
        if (right == MetadataFieldAccessibility.FamilyOrAssembly)
        {
            return left;
        }
        return MetadataFieldAccessibility.FamilyAndAssembly;
    }

    private static string LevelKey(StaticFieldMetadataModuleIdentity module, int typeDefinitionToken) =>
        module.Sha256 + "|" + typeDefinitionToken.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);

    private static MetadataSignatureTokenResolutionCatalog? FindTokenResolutionCatalog(
        StaticFieldV2MemberLookupRequest request,
        StaticFieldMetadataModuleIdentity module)
    {
        foreach (var catalog in request.TokenResolutionCatalogsCore)
        {
            if (catalog is not null && catalog.SourceModule.Equals(module))
            {
                return catalog;
            }
        }
        return null;
    }

    private static GenericBaseExtension ExtendThroughGenericBase(
        StaticFieldV2MemberLookupRequest request,
        MetadataTypeDefinitionAncestryChainIdentity currentChain,
        List<LookupLevel> walkLevels,
        HashSet<string> visited,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataFieldDefinitionTableCatalogIdentity> catalogsByModule,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataDefinitionAuthorityCatalogIdentity> authorityByModule)
    {
        var edges = currentChain.Edges;
        var edge = edges.IsEmpty ? currentChain.SubjectEdge : edges[^1];
        if (edge.Kind != MetadataImmediateBaseEdgeKind.TypeSpecification || edge.GenericBaseRow is not { } baseRow)
        {
            return GenericBaseExtension.Stopped(
                StaticFieldV2MemberLookupIssue.GenericBaseSignatureInvalid,
                edge.ExtendsMetadataToken);
        }

        var edgeModule = edge.SourceModule;
        var tokenCatalog = FindTokenResolutionCatalog(request, edgeModule);
        if (tokenCatalog is null ||
            tokenCatalog.ResultKind != MetadataSignatureTokenResolutionCatalogResultKind.Exact)
        {
            return GenericBaseExtension.Stopped(
                StaticFieldV2MemberLookupIssue.GenericBaseTokenResolutionUnavailable,
                baseRow.TypeSpecificationToken);
        }

        var reference = MetadataTypeSpecificationRowReferenceIdentity.Create(
            edgeModule,
            baseRow.TypeSpecificationToken);
        var decode = MetadataTypeSignatureDecoder.DecodeTypeSpecification(
            reference,
            baseRow.Observation.SignatureBytes,
            tokenCatalog);
        if (decode.Kind != MetadataSignatureDecodeResultKind.Exact || decode.Root is null)
        {
            var decodeIssue = decode.Kind == MetadataSignatureDecodeResultKind.Invalid
                ? StaticFieldV2MemberLookupIssue.GenericBaseSignatureInvalid
                : decode.ReachedBound is not null
                    ? StaticFieldV2MemberLookupIssue.GenericBaseSignatureUnsupported
                    : StaticFieldV2MemberLookupIssue.GenericBaseTokenResolutionUnavailable;
            return GenericBaseExtension.Stopped(decodeIssue, baseRow.TypeSpecificationToken);
        }

        // The owner variables inside this base TypeSpec bind to the construction of the exact level that owns the
        // physical Extends column: the requested owner's own supplied construction on the first hop, a previous hop's
        // substituted construction on a deeper hop, and no arguments under a definition-only named level.
        var owningLevel = walkLevels[^1];
        var arguments = owningLevel.DeclaringConstruction is { } owningConstruction
            ? owningConstruction.FlattenedArguments
            : walkLevels.Count == 1 && request.OwnerClosedConstruction is { } ownerConstruction
                ? ownerConstruction.FlattenedArguments
                : ImmutableArray<MetadataClosedTypeIdentity>.Empty;

        var issue = StaticFieldV2MemberLookupIssue.None;
        var projected = ProjectBaseType(decode.Root, arguments, ref issue);
        if (projected is null)
        {
            return GenericBaseExtension.Stopped(issue, baseRow.TypeSpecificationToken);
        }
        if (projected.Kind != MetadataClosedTypeKind.Named || projected.FinalClassification is not { } target)
        {
            return GenericBaseExtension.Stopped(
                StaticFieldV2MemberLookupIssue.GenericBaseSignatureInvalid,
                baseRow.TypeSpecificationToken);
        }

        var targetModule = target.SourceModule;
        var targetToken = target.TypeDefinition.TypeDefinitionToken;
        var targetClassification = request.AncestryPortfolio.ExactClassificationOrDefault(targetModule, targetToken);
        var targetChain = request.AncestryPortfolio.ExactAncestryChainOrDefault(targetModule, targetToken);
        if (targetClassification is not { Role: not null } || targetChain is null ||
            !catalogsByModule.ContainsKey(targetModule) || !authorityByModule.ContainsKey(targetModule))
        {
            return GenericBaseExtension.Stopped(
                StaticFieldV2MemberLookupIssue.GenericBaseClassificationAbsent,
                targetToken);
        }
        if (targetClassification.Role == MetadataTypeDefinitionSemanticRole.ModulePseudoType)
        {
            return GenericBaseExtension.Stopped(
                StaticFieldV2MemberLookupIssue.GenericBaseSignatureInvalid,
                targetToken);
        }

        var appended = ImmutableArray.CreateBuilder<LookupLevel>(targetChain.Edges.Length + 1);
        if (!visited.Add(LevelKey(targetModule, targetToken)))
        {
            return GenericBaseExtension.Stopped(
                StaticFieldV2MemberLookupIssue.GenericBaseCycleDetected,
                targetToken);
        }
        appended.Add(new LookupLevel(
            targetModule,
            targetClassification.TypeDefinition,
            targetClassification.TypeDefinition.TotalGenericArity > 0 ? projected : null));
        foreach (var chainEdge in targetChain.Edges)
        {
            if (!visited.Add(LevelKey(chainEdge.SourceModule, chainEdge.Owner.TypeDefinitionToken)))
            {
                return GenericBaseExtension.Stopped(
                    StaticFieldV2MemberLookupIssue.GenericBaseCycleDetected,
                    chainEdge.Owner.TypeDefinitionToken);
            }
            appended.Add(new LookupLevel(chainEdge.SourceModule, chainEdge.Owner));
        }

        if (walkLevels.Count + appended.Count > ExpressionV2ContractLimits.MaximumConstructedAncestryDepth + 1)
        {
            return GenericBaseExtension.Stopped(
                StaticFieldV2MemberLookupIssue.GenericBaseDepthBoundReached,
                targetToken);
        }

        return GenericBaseExtension.Extended(appended.ToImmutable(), targetChain);
    }

    private static MetadataClosedTypeIdentity? ProjectBaseType(
        MetadataTypeSignatureNode node,
        ImmutableArray<MetadataClosedTypeIdentity> ownerArguments,
        ref StaticFieldV2MemberLookupIssue issue)
    {
        switch (node.Kind)
        {
            case MetadataTypeSignatureNodeKind.Primitive:
                return MetadataClosedTypeIdentity.Primitive(node.PrimitiveKind!.Value);

            case MetadataTypeSignatureNodeKind.OwnerTypeParameter:
                var index = node.VariableIndex!.Value;
                if (index < 0 || index >= ownerArguments.Length)
                {
                    issue = StaticFieldV2MemberLookupIssue.GenericBaseArgumentUnbound;
                    return null;
                }
                return ownerArguments[index];

            case MetadataTypeSignatureNodeKind.MethodTypeParameter:
            case MetadataTypeSignatureNodeKind.TypeSpecificationIndirection:
            case MetadataTypeSignatureNodeKind.Void:
            case MetadataTypeSignatureNodeKind.TypedByReference:
                issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureInvalid;
                return null;

            case MetadataTypeSignatureNodeKind.Named:
                if (node.ContainsNonExactGenericMapping)
                {
                    issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureUnsupported;
                    return null;
                }
                if (node.ContainsUnclassifiedTypeDefinition)
                {
                    issue = StaticFieldV2MemberLookupIssue.GenericBaseClassificationAbsent;
                    return null;
                }
                if (node.NamedClassification!.TypeDefinition.TotalGenericArity != 0)
                {
                    issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureUnsupported;
                    return null;
                }
                return ProjectClosedOrStop(
                    () => MetadataClosedTypeIdentity.FromGroundSignature(node),
                    ref issue);

            case MetadataTypeSignatureNodeKind.GenericInstantiation:
                if (node.ContainsNonExactGenericMapping)
                {
                    issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureUnsupported;
                    return null;
                }
                var chain = node.NamedDefinitionChain;
                foreach (var classification in chain)
                {
                    if (classification.Role is null)
                    {
                        issue = StaticFieldV2MemberLookupIssue.GenericBaseClassificationAbsent;
                        return null;
                    }
                }
                var childNodes = node.Children;
                var argumentsBuilder = ImmutableArray.CreateBuilder<MetadataClosedTypeIdentity>(childNodes.Length);
                foreach (var child in childNodes)
                {
                    var argument = ProjectBaseType(child, ownerArguments, ref issue);
                    if (argument is null)
                    {
                        return null;
                    }
                    argumentsBuilder.Add(argument);
                }
                var flattened = argumentsBuilder.MoveToImmutable();
                return ProjectClosedOrStop(
                    () => MetadataClosedTypeIdentity.ConstructNamed(chain, flattened),
                    ref issue);

            case MetadataTypeSignatureNodeKind.SzArray:
            case MetadataTypeSignatureNodeKind.MultidimensionalArray:
                var element = ProjectBaseType(node.Children[0], ownerArguments, ref issue);
                if (element is null)
                {
                    return null;
                }
                var arrayNode = node;
                return ProjectClosedOrStop(
                    () => arrayNode.Kind == MetadataTypeSignatureNodeKind.SzArray
                        ? MetadataClosedTypeIdentity.SzArray(element)
                        : MetadataClosedTypeIdentity.MultidimensionalArray(
                            element,
                            arrayNode.ArrayRank!.Value,
                            arrayNode.ArraySizes,
                            arrayNode.ArrayLowerBounds),
                    ref issue);

            default:
                issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureUnsupported;
                return null;
        }
    }

    private static MetadataClosedTypeIdentity? ProjectClosedOrStop(
        Func<MetadataClosedTypeIdentity> factory,
        ref StaticFieldV2MemberLookupIssue issue)
    {
        try
        {
            return factory();
        }
        catch (MetadataClosedTypeBoundException)
        {
            issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureUnsupported;
            return null;
        }
        catch (ArgumentException)
        {
            issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureInvalid;
            return null;
        }
        catch (OverflowException)
        {
            issue = StaticFieldV2MemberLookupIssue.GenericBaseSignatureInvalid;
            return null;
        }
    }

    private readonly record struct GenericBaseExtension(
        ImmutableArray<LookupLevel> Levels,
        MetadataTypeDefinitionAncestryChainIdentity? NextChain,
        StaticFieldV2MemberLookupIssue Issue,
        int? RelatedMetadataToken)
    {
        internal static GenericBaseExtension Extended(
            ImmutableArray<LookupLevel> levels,
            MetadataTypeDefinitionAncestryChainIdentity nextChain) =>
            new(levels, nextChain, StaticFieldV2MemberLookupIssue.None, null);

        internal static GenericBaseExtension Stopped(
            StaticFieldV2MemberLookupIssue issue,
            int? relatedMetadataToken) =>
            new([], null, issue, relatedMetadataToken);
    }

    private readonly record struct LookupLevel(
        StaticFieldMetadataModuleIdentity SourceModule,
        MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition,
        MetadataClosedTypeIdentity? DeclaringConstruction = null);

    private readonly record struct LevelPlan(ImmutableArray<LookupLevel> Levels, bool IsComplete);
}
