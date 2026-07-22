using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Selects how one draft static-field member lookup admits or refuses a physical declaration.</summary>
/// <remarks>
/// The draft mode is a caller-declared certificate rather than a derived fact. Qualified inspection deliberately
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

/// <summary>Classifies one definition-side static-field member-lookup draft answer.</summary>
/// <remarks>
/// <see cref="Exact"/>, <see cref="Absent"/>, <see cref="Ambiguous"/>, <see cref="Partial"/>, and
/// <see cref="HiddenByUnsupportedMember"/> are complete derived draft answers that retain their per-level evidence.
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

    /// <summary>The nearest level declaring the name declared it as a member this draft profile does not own.</summary>
    HiddenByUnsupportedMember = 5,

    /// <summary>A prerequisite or a declared draft bound prevented any complete answer.</summary>
    NonExact = 6,

    /// <summary>Complete supplied draft evidence contradicted the ancestry portfolio or itself.</summary>
    Invalid = 7,

    /// <summary>The requested owner selects a later draft route that this definition-side binder does not own.</summary>
    Unsupported = 8,
}

/// <summary>Identifies the deterministic issue for one static-field member-lookup draft outcome.</summary>
/// <remarks>This draft issue catalog keeps prerequisite, vector-shape, owner, bound, and derived answers distinct.</remarks>
public enum StaticFieldV2MemberLookupIssue
{
    /// <summary>No issue applies to an exact draft outcome.</summary>
    None = 0,

    /// <summary>The ancestry authority portfolio prerequisite was non-exact.</summary>
    AncestryPortfolioNonExact = 1,

    /// <summary>The ancestry authority portfolio prerequisite was invalid.</summary>
    AncestryPortfolioInvalid = 2,

    /// <summary>The supplied FieldDef catalog vector was default rather than explicitly initialized.</summary>
    FieldCatalogVectorUninitialized = 3,

    /// <summary>The supplied FieldDef catalog vector reached the shared module draft bound plus one.</summary>
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

    /// <summary>The accessibility decision count reached the declared draft cap plus one.</summary>
    AccessibilityCheckBoundReached = 17,

    /// <summary>Every reached level declared no such name and the bounded base chain was incomplete.</summary>
    AncestryIncomplete = 18,

    /// <summary>Every level of one complete bounded base chain declared no such name.</summary>
    DeclarationAbsent = 19,

    /// <summary>The nearest declaring level declared the name as a method this draft profile does not own.</summary>
    HiddenByDeclaredMethod = 20,

    /// <summary>The nearest declaring level declared the name as an instance field rather than a static field.</summary>
    HiddenByInstanceField = 21,

    /// <summary>The nearest declaring level declared two or more accessible static fields with the name.</summary>
    AmbiguousStaticDeclarations = 22,
}

/// <summary>Classifies the physical storage shape of one selected static-field draft declaration.</summary>
/// <remarks>The draft shape is a pure FieldAttributes decoding and asserts nothing about runtime storage.</remarks>
public enum StaticFieldV2FieldStorageShape
{
    /// <summary>The declaration is an ordinary stored static slot with no literal value and no field RVA.</summary>
    StoredSlot = 1,

    /// <summary>The declaration carries FieldAttributes.Literal, which also implies a Constant row.</summary>
    MetadataLiteral = 2,

    /// <summary>The declaration carries FieldAttributes.HasFieldRVA and therefore names module image bytes.</summary>
    ModuleRvaCandidate = 3,
}

/// <summary>Identifies one declared coverage boundary retained by a static-field member-lookup draft outcome.</summary>
/// <remarks>
/// Every boundary is an informational draft fact rather than an error. A boundary states what this definition-side
/// phase deliberately does not model, so a consumer can never mistake a silent gap for a proven negative.
/// </remarks>
public enum StaticFieldV2MemberLookupCoverageBoundary
{
    /// <summary>The physical Property and Event tables are not modeled, so same-name accessors cannot block.</summary>
    PropertyAndEventTablesNotModeled = 1,

    /// <summary>Interface ancestry is not a class base chain and the InterfaceImpl table is not modeled here.</summary>
    InterfaceAncestryNotModeled = 2,

    /// <summary>The physical InternalsVisibleTo CustomAttribute table is not modeled; friend grants are supplied.</summary>
    FriendAssemblyAttributesNotModeled = 3,

    /// <summary>Qualified inspection bypassed every accessibility rule for every examined declaration.</summary>
    AccessibilityBypassApplied = 4,
}

/// <summary>Freezes one caller-supplied friend-assembly grant of a single defining assembly draft identity.</summary>
/// <remarks>
/// The physical InternalsVisibleTo CustomAttribute table is not modeled by this draft slice, so friendship is a
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

    /// <summary>Gets the exact defining assembly whose declared friendships this draft grant carries.</summary>
    public StaticFieldContainingAssemblyIdentity DefiningAssembly { get; }

    /// <summary>Gets a defensive declaration-order copy of the assemblies this defining assembly befriends.</summary>
    public ImmutableArray<StaticFieldAssemblyDefinitionIdentity> DeclaredFriendAssemblies =>
        ExpressionV2ContractEncoding.Copy(declaredFriendAssemblies);

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft grant bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft grant.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one caller-supplied friend-assembly draft grant for a single defining assembly.</summary>
    /// <param name="definingAssembly">The exact defining assembly that declares the friendships.</param>
    /// <param name="declaredFriendAssemblies">
    /// The ordered assembly definitions declared as friends, bounded by the shared friend-declaration draft cap.
    /// </param>
    /// <returns>A sealed immutable draft grant with defensively copied declarations.</returns>
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

    /// <summary>Tests whether this draft grant befriends one exact assembly definition.</summary>
    /// <param name="candidate">The candidate requesting assembly definition.</param>
    /// <returns><see langword="true"/> only when the draft grant declares that exact assembly definition.</returns>
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

    /// <summary>Tests canonical equality between two friend-assembly draft grants.</summary>
    /// <param name="other">The other draft grant.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2FriendAssemblyGrantIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests friend-assembly draft grant equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a grant with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2FriendAssemblyGrantIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft grant content.</summary>
    /// <returns>A hash code for this canonical draft grant.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one examined same-name static or instance field draft candidate.</summary>
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
        StaticFieldV2FieldStorageShape storageShape)
    {
        FieldRow = fieldRow;
        DeclaringTypeDefinition = declaringTypeDefinition;
        LevelIndex = levelIndex;
        IsAccessible = isAccessible;
        EffectiveAccessibility = effectiveAccessibility;
        StorageShape = storageShape;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(fieldRow.Sha256, nameof(fieldRow));
        writer.WriteSha256(declaringTypeDefinition.Sha256, nameof(declaringTypeDefinition));
        writer.WriteInt32(levelIndex);
        writer.WriteBoolean(isAccessible);
        writer.WriteInt32((int)effectiveAccessibility);
        writer.WriteInt32((int)storageShape);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact physical FieldDef draft row this candidate names.</summary>
    public MetadataFieldDefinitionTableRowIdentity FieldRow { get; }

    /// <summary>Gets the authority-issued TypeDef that physically declared this draft candidate.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the zero-based bounded base-chain level that declared this draft candidate.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets whether the requested draft accessibility mode admitted this candidate.</summary>
    public bool IsAccessible { get; }

    /// <summary>Gets the effective accessibility decided from the field and its declaring nested-type chain.</summary>
    public MetadataFieldAccessibility EffectiveAccessibility { get; }

    /// <summary>Gets the physical storage shape decoded from this draft candidate's FieldAttributes.</summary>
    public StaticFieldV2FieldStorageShape StorageShape { get; }

    /// <summary>Gets the exact physical FieldDef token of this draft candidate.</summary>
    public int FieldDefinitionToken => FieldRow.FieldDefinitionToken;

    /// <summary>Gets whether the physical draft row carries FieldAttributes.Static.</summary>
    public bool IsStatic => FieldRow.IsStatic;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft candidate bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft candidate.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two examined member draft candidates.</summary>
    /// <param name="other">The other draft candidate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2MemberCandidateIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests examined member draft candidate equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a candidate with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberCandidateIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft candidate content.</summary>
    /// <returns>A hash code for this canonical draft candidate.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2MemberCandidateIdentity Create(
        object mintCapability,
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int levelIndex,
        bool isAccessible,
        MetadataFieldAccessibility effectiveAccessibility,
        StaticFieldV2FieldStorageShape storageShape)
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

        return new StaticFieldV2MemberCandidateIdentity(
            fieldRow,
            declaringTypeDefinition,
            levelIndex,
            isAccessible,
            effectiveAccessibility,
            storageShape);
    }
}

/// <summary>Freezes one consulted bounded base-chain level of a static-field member-lookup draft answer.</summary>
/// <remarks>
/// The level is minted only by <see cref="StaticFieldV2MemberLookupOutcome"/>. It proves which physical declaring
/// type was consulted and how many declared FieldDef and MethodDef rows were examined there, so provenance can
/// establish that the answer consulted every nearer level before any farther one.
/// </remarks>
public sealed class StaticFieldV2MemberLookupLevelIdentity : IEquatable<StaticFieldV2MemberLookupLevelIdentity>
{
    private const string CanonicalDomain = "static-field-v2-member-lookup-level";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2MemberLookupLevelIdentity(
        int levelIndex,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int examinedFieldCount,
        int examinedMethodCount,
        int accessibleCandidateCount)
    {
        LevelIndex = levelIndex;
        DeclaringTypeDefinition = declaringTypeDefinition;
        ExaminedFieldCount = examinedFieldCount;
        ExaminedMethodCount = examinedMethodCount;
        AccessibleCandidateCount = accessibleCandidateCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(levelIndex);
        writer.WriteSha256(declaringTypeDefinition.Sha256, nameof(declaringTypeDefinition));
        writer.WriteInt32(examinedFieldCount);
        writer.WriteInt32(examinedMethodCount);
        writer.WriteInt32(accessibleCandidateCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based level index, where zero is the requested owner itself.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets the authority-issued TypeDef consulted at this bounded base-chain draft level.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the count of declared FieldDef rows examined at this draft level.</summary>
    public int ExaminedFieldCount { get; }

    /// <summary>Gets the count of declared MethodDef rows examined at this draft level.</summary>
    public int ExaminedMethodCount { get; }

    /// <summary>Gets the count of accessible same-name field and method declarations found at this level.</summary>
    public int AccessibleCandidateCount { get; }

    /// <summary>Gets the exact TypeDef token of the declaring type consulted at this draft level.</summary>
    public int DeclaringTypeDefinitionToken => DeclaringTypeDefinition.TypeDefinitionToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft level bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft level.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two consulted draft levels.</summary>
    /// <param name="other">The other draft level.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2MemberLookupLevelIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests consulted draft level equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a level with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberLookupLevelIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft level content.</summary>
    /// <returns>A hash code for this canonical draft level.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2MemberLookupLevelIdentity Create(
        object mintCapability,
        int levelIndex,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int examinedFieldCount,
        int examinedMethodCount,
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
        ArgumentOutOfRangeException.ThrowIfNegative(accessibleCandidateCount);
        return new StaticFieldV2MemberLookupLevelIdentity(
            levelIndex,
            declaringTypeDefinition,
            examinedFieldCount,
            examinedMethodCount,
            accessibleCandidateCount);
    }
}

/// <summary>Freezes one complete definition-side static-field member-lookup draft request.</summary>
/// <remarks>
/// The request names one owner module, one owner TypeDef token, one decoded field identifier, one exact ancestry
/// draft portfolio, one FieldDef catalog per ancestry-portfolio module, and the caller-declared accessibility
/// certificate. It carries no runtime, no storage, no scoped evaluation context, and no PDB evidence, and it
/// constructs no generic instantiation: substitution onto a closed construction is a separately owned later slice.
/// </remarks>
public sealed class StaticFieldV2MemberLookupRequest : IEquatable<StaticFieldV2MemberLookupRequest>
{
    private const string CanonicalDomain = "static-field-v2-member-lookup-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs;
    private readonly ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2MemberLookupRequest(
        StaticFieldMetadataModuleIdentity ownerModule,
        int ownerTypeDefinitionToken,
        DumpExpressionIdentifier fieldName,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        StaticFieldV2AccessibilityMode accessibilityMode,
        StaticFieldContainingAssemblyIdentity? requestingAssembly,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants)
    {
        OwnerModule = ownerModule;
        OwnerTypeDefinitionToken = ownerTypeDefinitionToken;
        FieldName = fieldName;
        AncestryPortfolio = ancestryPortfolio;
        this.fieldCatalogs = fieldCatalogs;
        AccessibilityMode = accessibilityMode;
        RequestingAssembly = requestingAssembly;
        this.friendAssemblyGrants = friendAssemblyGrants;

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
        writer.WriteInt32((int)accessibilityMode);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, requestingAssembly?.Sha256);
        writer.WriteInt32(friendAssemblyGrants.Length);
        foreach (var grant in friendAssemblyGrants)
        {
            writer.WriteSha256(grant.Sha256, nameof(friendAssemblyGrants));
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

    /// <summary>Gets the ancestry authority draft portfolio supplying classification and bounded base chains.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets a defensive copy of the supplied per-module FieldDef catalog vector, default when absent.</summary>
    public ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs =>
        fieldCatalogs.IsDefault ? default : ExpressionV2ContractEncoding.Copy(fieldCatalogs);

    /// <summary>Gets whether the supplied FieldDef catalog vector was explicitly initialized.</summary>
    public bool IsFieldCatalogVectorInitialized => !fieldCatalogs.IsDefault;

    /// <summary>Gets the caller-declared accessibility certificate that governs every admission decision.</summary>
    public StaticFieldV2AccessibilityMode AccessibilityMode { get; }

    /// <summary>Gets the requesting assembly for the use-site certificate, or null for qualified inspection.</summary>
    public StaticFieldContainingAssemblyIdentity? RequestingAssembly { get; }

    /// <summary>Gets a defensive declaration-order copy of the caller-supplied friend-assembly draft grants.</summary>
    public ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrants =>
        ExpressionV2ContractEncoding.Copy(friendAssemblyGrants);

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete definition-side static-field member-lookup draft request.</summary>
    /// <param name="ownerModule">The exact metadata module owning the requested TypeDef.</param>
    /// <param name="ownerTypeDefinitionToken">The exact non-nil owner TypeDef token.</param>
    /// <param name="fieldName">The decoded identifier compared ordinally against physical FieldDef names.</param>
    /// <param name="ancestryPortfolio">The ancestry authority draft portfolio prerequisite.</param>
    /// <param name="fieldCatalogs">
    /// Exactly one FieldDef catalog per ancestry-portfolio module in any order. A default array is admitted here and
    /// becomes a typed vector-shape draft stop rather than an exception, so callers can replay malformed input.
    /// </param>
    /// <param name="accessibilityMode">The caller-declared accessibility certificate.</param>
    /// <param name="requestingAssembly">
    /// The requesting assembly, required exactly for <see cref="StaticFieldV2AccessibilityMode.UseSiteCertificate"/>.
    /// </param>
    /// <param name="friendAssemblyGrants">
    /// The caller-supplied friend grants, admitted only for the use-site certificate and bounded by the shared cap.
    /// </param>
    /// <returns>A sealed immutable draft request with defensively copied evidence.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The owner token is not a TypeDef token or the mode is undefined.</exception>
    /// <exception cref="ArgumentException">The requesting assembly or grants disagree with the declared mode.</exception>
    public static StaticFieldV2MemberLookupRequest Create(
        StaticFieldMetadataModuleIdentity ownerModule,
        int ownerTypeDefinitionToken,
        DumpExpressionIdentifier fieldName,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        StaticFieldV2AccessibilityMode accessibilityMode,
        StaticFieldContainingAssemblyIdentity? requestingAssembly = null,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants = default)
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

        var catalogs = fieldCatalogs.IsDefault
            ? default
            : ImmutableArray.CreateRange(fieldCatalogs);
        return new StaticFieldV2MemberLookupRequest(
            ownerModule,
            ownerTypeDefinitionToken,
            fieldName,
            ancestryPortfolio,
            catalogs,
            accessibilityMode,
            requestingAssembly,
            grants);
    }

    /// <summary>Tests canonical equality between two member-lookup draft requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2MemberLookupRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests member-lookup draft request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberLookupRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogsCore => fieldCatalogs;

    internal ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrantsCore =>
        friendAssemblyGrants;
}

/// <summary>Freezes the complete draft outcome of one definition-side static-field member lookup.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of every examined candidate and consulted level it retains. The
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
/// Declared coverage boundaries are informational facts retained by every outcome. The physical Property and Event
/// tables are not modeled by this draft slice, so a same-name property or event can never block a field here; the
/// physical InternalsVisibleTo CustomAttribute table is likewise not modeled, so friendship is caller-supplied.
/// </para>
/// </remarks>
public sealed class StaticFieldV2MemberLookupOutcome : IEquatable<StaticFieldV2MemberLookupOutcome>
{
    /// <summary>Gets the shared accessibility-decision draft cap applied by one complete lookup.</summary>
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

    /// <summary>Gets whether this draft lookup is exact, absent, ambiguous, partial, hidden, or a typed stop.</summary>
    public StaticFieldV2MemberLookupResultKind ResultKind { get; }

    /// <summary>Gets the typed draft lookup issue, or none for an exact outcome.</summary>
    public StaticFieldV2MemberLookupIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2MemberLookupRequest Request { get; }

    /// <summary>Gets the single selected static-field draft candidate, or null for anything but an exact answer.</summary>
    public StaticFieldV2MemberCandidateIdentity? SelectedCandidate { get; }

    /// <summary>Gets a defensive most-derived-first copy of every consulted draft level, empty for a stop.</summary>
    public ImmutableArray<StaticFieldV2MemberLookupLevelIdentity> Levels =>
        ExpressionV2ContractEncoding.Copy(levels);

    /// <summary>Gets a defensive examination-order copy of every examined but unselected draft candidate.</summary>
    public ImmutableArray<StaticFieldV2MemberCandidateIdentity> RejectedCandidates =>
        ExpressionV2ContractEncoding.Copy(rejectedCandidates);

    /// <summary>Gets the bounded ancestry terminal of the consulted chain, or null for a prefix-free stop.</summary>
    public MetadataAncestryChainTerminalKind? AncestryTerminal { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>
    /// Gets the count of same-name FieldDef and MethodDef declarations examined for a complete draft answer, or the
    /// propagated prerequisite, supplied, or cap-plus-one observation for a typed stop.
    /// </summary>
    public int ObservedCount { get; }

    /// <summary>Gets the metadata token related to this draft answer or stop, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two member-lookup draft outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2MemberLookupOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests member-lookup draft outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2MemberLookupOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2MemberCandidateIdentity IssueCandidate(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int levelIndex,
        bool isAccessible,
        MetadataFieldAccessibility effectiveAccessibility,
        StaticFieldV2FieldStorageShape storageShape) =>
        StaticFieldV2MemberCandidateIdentity.Create(
            RowMintCapability,
            fieldRow,
            declaringTypeDefinition,
            levelIndex,
            isAccessible,
            effectiveAccessibility,
            storageShape);

    internal static StaticFieldV2MemberLookupLevelIdentity IssueLevel(
        int levelIndex,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        int examinedFieldCount,
        int examinedMethodCount,
        int accessibleCandidateCount) =>
        StaticFieldV2MemberLookupLevelIdentity.Create(
            RowMintCapability,
            levelIndex,
            declaringTypeDefinition,
            examinedFieldCount,
            examinedMethodCount,
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
/// This draft binder owns definition-kind-specific member lookup only. It walks the owner's bounded ancestry chain
/// most-derived-first, applies the declared accessibility certificate before hiding is decided, refuses to skip a
/// blocking same-name method or instance field, and never claims absence over an incomplete chain.
/// <para>
/// Declared coverage boundaries of this draft slice: the physical Property and Event tables are not modeled, so a
/// same-name property or event cannot block a field here; interface ancestry is not a class base chain and the
/// InterfaceImpl table is not walked, so an interface owner examines only itself; and the physical
/// InternalsVisibleTo CustomAttribute table is not modeled, so friend grants are caller-supplied certificates.
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
    /// <param name="request">The complete definition-side member-lookup draft request.</param>
    /// <remarks>
    /// The walk is exhaustive within each consulted level: every declared FieldDef and MethodDef row of the level's
    /// declaring type is examined before any answer is formed, so no first-match heuristic can influence the result.
    /// A level that declares only inaccessible same-name members hides nothing under the use-site certificate, while
    /// qualified inspection makes every such declaration accessible and therefore hiding.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is either one complete answer or one prefix-free typed stop.</returns>
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

        var vector = ValidateCatalogVector(request, boundaries, out var catalogsByModule, out var authorityByModule);
        if (vector is not null)
        {
            return vector;
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
        if (classification.Role == MetadataTypeDefinitionSemanticRole.Interface)
        {
            boundaries = Add(boundaries, StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled);
        }

        return Walk(request, boundaries, chain, catalogsByModule, authorityByModule);
    }

    private static StaticFieldV2MemberLookupOutcome Walk(
        StaticFieldV2MemberLookupRequest request,
        ImmutableArray<StaticFieldV2MemberLookupCoverageBoundary> boundaries,
        MetadataTypeDefinitionAncestryChainIdentity chain,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataFieldDefinitionTableCatalogIdentity> catalogsByModule,
        Dictionary<StaticFieldMetadataModuleIdentity, MetadataDefinitionAuthorityCatalogIdentity> authorityByModule)
    {
        var name = request.FieldName.DecodedText;
        var levels = ImmutableArray.CreateBuilder<StaticFieldV2MemberLookupLevelIdentity>();
        var examined = new List<StaticFieldV2MemberCandidateIdentity>();
        var winningStaticFields = new List<StaticFieldV2MemberCandidateIdentity>();
        StaticFieldV2MemberCandidateIdentity? winningInstanceField = null;
        var winningMethodToken = 0;
        var decisionCount = 0;
        var observedCount = 0;
        var resolved = false;

        var walkModule = chain.SourceModule;
        var walkType = chain.Subject;
        var edges = chain.Edges;
        for (var levelIndex = 0; levelIndex <= edges.Length; levelIndex++)
        {
            if (levelIndex > 0)
            {
                walkModule = edges[levelIndex - 1].SourceModule;
                walkType = edges[levelIndex - 1].Owner;
            }

            var catalog = catalogsByModule[walkModule];
            var authority = authorityByModule[walkModule];
            var fieldRows = catalog.RowsForDeclaringTypeOrEmpty(walkType);
            var methodTokens = walkType.MethodDefinitionTokens;
            var accessibleCandidateCount = 0;
            var levelStaticFields = new List<StaticFieldV2MemberCandidateIdentity>();
            StaticFieldV2MemberCandidateIdentity? levelInstanceField = null;
            var levelMethodToken = 0;

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
                    StorageShape(fieldRow));
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

            levels.Add(StaticFieldV2MemberLookupOutcome.IssueLevel(
                levelIndex,
                walkType,
                fieldRows.Length,
                methodTokens.Length,
                accessibleCandidateCount));

            if (accessibleCandidateCount > 0)
            {
                winningStaticFields.AddRange(levelStaticFields);
                winningInstanceField = levelInstanceField;
                winningMethodToken = levelMethodToken;
                resolved = true;
                break;
            }
        }

        var consultedLevels = levels.ToImmutable();
        if (!resolved)
        {
            var complete = chain.TerminalKind is MetadataAncestryChainTerminalKind.SystemObjectReached
                or MetadataAncestryChainTerminalKind.InterfaceRoot
                or MetadataAncestryChainTerminalKind.ModulePseudoTypeRoot;
            return StaticFieldV2MemberLookupOutcome.IssueComplete(
                complete
                    ? StaticFieldV2MemberLookupResultKind.Absent
                    : StaticFieldV2MemberLookupResultKind.Partial,
                complete
                    ? StaticFieldV2MemberLookupIssue.DeclarationAbsent
                    : StaticFieldV2MemberLookupIssue.AncestryIncomplete,
                request,
                null,
                consultedLevels,
                [.. examined],
                chain.TerminalKind,
                boundaries,
                observedCount,
                null);
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
                chain.TerminalKind,
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
                chain.TerminalKind,
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
                chain.TerminalKind,
                boundaries,
                observedCount,
                winningStaticFields[0].FieldDefinitionToken);
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
            chain.TerminalKind,
            boundaries,
            observedCount,
            selected.FieldDefinitionToken);
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
            ? [
                StaticFieldV2MemberLookupCoverageBoundary.PropertyAndEventTablesNotModeled,
                StaticFieldV2MemberLookupCoverageBoundary.FriendAssemblyAttributesNotModeled,
            ]
            : [
                StaticFieldV2MemberLookupCoverageBoundary.PropertyAndEventTablesNotModeled,
                StaticFieldV2MemberLookupCoverageBoundary.AccessibilityBypassApplied,
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
}
