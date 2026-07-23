using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies one physical Portable-PDB import definition projected into the scoped draft view.</summary>
/// <remarks>
/// The draft categories mirror the retained W7 import facts one to one. A category states only what the physical
/// import row is; whether the import can contribute a bound head is a separate retained target disposition.
/// </remarks>
public enum StaticFieldV2ScopedImportKind
{
    /// <summary>An ordinary or assembly-qualified namespace import.</summary>
    NamespaceImport = 1,

    /// <summary>A named alias whose target is a type token.</summary>
    TypeAlias = 2,

    /// <summary>A named alias whose target is namespace text.</summary>
    NamespaceAlias = 3,

    /// <summary>A <c>using static</c> import whose owner is a type token.</summary>
    UsingStatic = 4,

    /// <summary>An extern alias naming one AssemblyRef row.</summary>
    ExternAlias = 5,

    /// <summary>A raw import payload whose kind this draft slice does not interpret.</summary>
    UnsupportedRaw = 6,
}

/// <summary>Identifies how one projected draft import's physical target was or was not resolved.</summary>
/// <remarks>
/// Only <see cref="NotApplicable"/>, <see cref="TypeDefinitionResolved"/>, <see cref="TypeReferenceResolved"/>, and
/// <see cref="AssemblyReferenceResolved"/> are exact. Every other value keeps the physical import retained as typed
/// non-exact evidence, so a retained import can never silently vanish from the draft projection.
/// </remarks>
public enum StaticFieldV2ScopedImportTargetDisposition
{
    /// <summary>The import form carries no target token, so no target resolution was attempted.</summary>
    NotApplicable = 1,

    /// <summary>A TypeDef target token named one authority-issued chain in the selected module.</summary>
    TypeDefinitionResolved = 2,

    /// <summary>A TypeRef target token resolved through the exact resolution portfolio.</summary>
    TypeReferenceResolved = 3,

    /// <summary>A TypeRef target token did not resolve to one authority-issued TypeDef.</summary>
    TypeReferenceUnresolved = 4,

    /// <summary>A TypeSpec target row is retained physically and deliberately not decoded by this draft slice.</summary>
    TypeSpecificationNotDecoded = 5,

    /// <summary>A TypeDef target token names no authority-issued chain in the selected module.</summary>
    TypeDefinitionNotIssued = 6,

    /// <summary>An AssemblyRef target row exists and identity-binds exactly one portfolio module.</summary>
    AssemblyReferenceResolved = 7,

    /// <summary>The import form names no AssemblyRef row this draft slice can retain.</summary>
    AssemblyReferenceRowAbsent = 8,

    /// <summary>No portfolio module's assembly definition identity-binds the named AssemblyRef row.</summary>
    AssemblyReferenceTargetModuleAbsent = 9,

    /// <summary>More than one portfolio module's assembly definition identity-binds the AssemblyRef row.</summary>
    AssemblyReferenceTargetModuleAmbiguous = 10,

    /// <summary>The raw import payload was retained without any semantic interpretation.</summary>
    RawImportNotInterpreted = 11,
}

/// <summary>Classifies one projected scoped-context draft view.</summary>
/// <remarks>Every stop is prefix-free and retains neither scope levels nor namespace levels.</remarks>
public enum StaticFieldV2ScopedContextResultKind
{
    /// <summary>The complete active import chain and namespace levels were projected.</summary>
    Exact = 1,

    /// <summary>A prerequisite or a declared draft bound prevented a complete projection.</summary>
    NonExact = 2,

    /// <summary>Complete draft inputs contradicted one another.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one scoped-context draft projection.</summary>
/// <remarks>The values keep prerequisite, correlation, chain-shape, and bound stops distinct.</remarks>
public enum StaticFieldV2ScopedContextIssue
{
    /// <summary>No issue applies to an exact draft projection.</summary>
    None = 0,

    /// <summary>The ancestry authority draft portfolio prerequisite was non-exact.</summary>
    AncestryPortfolioNonExact = 1,

    /// <summary>The ancestry authority draft portfolio prerequisite was invalid.</summary>
    AncestryPortfolioInvalid = 2,

    /// <summary>The TypeRef resolution draft portfolio prerequisite was non-exact.</summary>
    ResolutionPortfolioNonExact = 3,

    /// <summary>The TypeRef resolution draft portfolio prerequisite was invalid.</summary>
    ResolutionPortfolioInvalid = 4,

    /// <summary>The supplied resolution portfolio is not the one the ancestry portfolio consumed.</summary>
    ResolutionPortfolioMismatch = 5,

    /// <summary>The selected metadata module is absent from the exact draft portfolio.</summary>
    SelectedModuleNotInPortfolio = 6,

    /// <summary>The selected declaring TypeDef has no authority-issued chain in the selected module.</summary>
    SelectedTypeDefinitionNotIssued = 7,

    /// <summary>The supplied import-scope vector was default rather than explicitly initialized.</summary>
    ImportScopeChainUninitialized = 8,

    /// <summary>The supplied import scopes are not one contiguous outer-to-inner parent chain.</summary>
    ImportScopeChainNotOuterToInner = 9,

    /// <summary>The active import-scope depth reached the declared draft cap plus one.</summary>
    ImportScopeDepthBoundReached = 10,

    /// <summary>The cumulative import-fact count reached the declared draft cap plus one.</summary>
    ImportFactCountBoundReached = 11,

    /// <summary>The derived namespace-declaration level count reached the declared draft cap plus one.</summary>
    NamespaceLevelCountBoundReached = 12,
}

/// <summary>Identifies one declared coverage boundary retained by a scoped-context draft projection.</summary>
/// <remarks>
/// Every boundary is an informational draft fact rather than an error. A boundary states what this scoped-context
/// phase deliberately does not model, so a consumer can never mistake a silent gap for a proven negative.
/// </remarks>
public enum StaticFieldV2ScopedContextCoverageBoundary
{
    /// <summary>A TypeSpec alias target is retained physically and never becomes a bound head in this draft slice.</summary>
    AliasTypeSpecTargetNotDecoded = 1,

    /// <summary>Retained <c>using static</c> imports are consumed by the separately owned bare-member draft slice.</summary>
    UsingStaticConsumedByLaterSlice = 2,

    /// <summary>At least one malformed or unsupported raw import payload is retained without interpretation.</summary>
    RetainedUnsupportedImport = 3,

    /// <summary>Route selection between the metadata-global and contextual routes belongs to a later draft slice.</summary>
    MetadataGlobalRouteSelectionDeferred = 4,

    /// <summary>Import scopes pair with namespace-declaration levels innermost-first by declared draft convention.</summary>
    ImportScopeToNamespaceLevelPairingDeclared = 5,

    /// <summary>At least one retained import target did not resolve to one exact physical head.</summary>
    UnresolvedImportTargetRetained = 6,
}

/// <summary>Identifies which scoped draft source contributed one contextual candidate.</summary>
/// <remarks>The source is retained per candidate so import order can be proven never to select an answer.</remarks>
public enum StaticFieldV2ContextualCandidateSource
{
    /// <summary>The candidate is declared in the level's own namespace text.</summary>
    NamespaceLevelDeclaration = 1,

    /// <summary>The candidate head came from a type alias declared at the level.</summary>
    TypeAlias = 2,

    /// <summary>The candidate namespace prefix came from a namespace alias declared at the level.</summary>
    NamespaceAlias = 3,

    /// <summary>The candidate namespace prefix came from a namespace import declared at the level.</summary>
    NamespaceImport = 4,

    /// <summary>The candidate module restriction came from an extern alias named by the expression.</summary>
    ExternAlias = 5,
}

/// <summary>Identifies why one examined draft chain did not match a contextual owner name.</summary>
/// <remarks>
/// The reasons are ordered by how far the examined chain progressed through the fixed comparison sequence, so a mere
/// shape disagreement never hides real name evidence. Every value is a retained per-level draft fact.
/// </remarks>
public enum StaticFieldV2ContextualRejectionReason
{
    /// <summary>The chain segment count disagreed with the examined namespace/type split.</summary>
    SegmentCountMismatch = 1,

    /// <summary>The chain identifies the ECMA module pseudo-type, which is never a named source type.</summary>
    ModulePseudoType = 2,

    /// <summary>The chain's outermost namespace text differed from the examined namespace split.</summary>
    NamespaceMismatch = 3,

    /// <summary>The chain does not extend the alias-supplied enclosing head.</summary>
    EnclosingHeadMismatch = 4,

    /// <summary>One segment's compiler-name mapping was non-exact and can never spell a source name.</summary>
    NonExactCompilerMapping = 5,

    /// <summary>One projected simple name differed from the corresponding syntax identifier.</summary>
    SimpleNameMismatch = 6,

    /// <summary>One introduced generic arity differed from the corresponding syntax arity.</summary>
    ArityMismatch = 7,

    /// <summary>One segment has no ordinary C# simple-name spelling.</summary>
    NotCSharpAddressable = 8,

    /// <summary>A matching alias was found but its retained target can never become a bound draft head.</summary>
    AliasTargetNotBindable = 9,
}

/// <summary>Classifies one examined declaration level of the contextual draft search.</summary>
/// <remarks>The disposition proves that the first viable level stops the outward search.</remarks>
public enum StaticFieldV2ContextualLevelDisposition
{
    /// <summary>The level produced a viable non-empty candidate set and stopped the outward draft search.</summary>
    Selected = 1,

    /// <summary>The level produced no candidate, so the draft search continued outward.</summary>
    NoCandidateAtLevel = 2,
}

/// <summary>Classifies one draft contextual owner-name binding answer.</summary>
/// <remarks>
/// <see cref="Exact"/>, <see cref="Absent"/>, and <see cref="Ambiguous"/> are complete derived draft answers that
/// retain their per-partition level evidence. <see cref="NonExact"/>, <see cref="Invalid"/>, and
/// <see cref="Unsupported"/> are prefix-free stops that retain no partition evidence and no selected candidate.
/// </remarks>
public enum StaticFieldV2ContextualBindingResultKind
{
    /// <summary>Exactly one physical draft candidate group was derived.</summary>
    Exact = 1,

    /// <summary>Every examined level rejected every occurrence over an exhaustive draft context.</summary>
    Absent = 2,

    /// <summary>Two or more distinct physical draft candidate groups were derived.</summary>
    Ambiguous = 3,

    /// <summary>A prerequisite, a declared draft bound, or retained non-exact evidence prevented an answer.</summary>
    NonExact = 4,

    /// <summary>A complete draft prerequisite contradicted itself.</summary>
    Invalid = 5,

    /// <summary>The expression selects the separately owned explicit draft route.</summary>
    Unsupported = 6,
}

/// <summary>Identifies the deterministic issue for one contextual owner-name binding draft outcome.</summary>
/// <remarks>This draft-phase issue catalog keeps prerequisite, route, bound, and derived answers distinct.</remarks>
public enum StaticFieldV2ContextualBindingIssue
{
    /// <summary>No issue applies to an exact draft outcome.</summary>
    None = 0,

    /// <summary>The scoped-context draft projection prerequisite was non-exact.</summary>
    ContextNonExact = 1,

    /// <summary>The scoped-context draft projection prerequisite was invalid.</summary>
    ContextInvalid = 2,

    /// <summary>A <c>global::</c> or metadata-global spelling belongs to the explicit draft route.</summary>
    ExplicitRouteNotSupported = 3,

    /// <summary>The descriptor retained no qualified-owner partition for the contextual draft route.</summary>
    NoQualifiedOwnerPartition = 4,

    /// <summary>The alias qualifier names no alias declared by the projected draft context.</summary>
    NamedAliasAbsent = 5,

    /// <summary>One winning level derived two or more physical draft candidate groups.</summary>
    AmbiguousWithinLevel = 6,

    /// <summary>Two or more partitions derived different physical draft candidate groups.</summary>
    AmbiguousAcrossPartitions = 7,

    /// <summary>Every examined level rejected every occurrence over an exhaustive draft context.</summary>
    CandidateAbsent = 8,

    /// <summary>Retained non-exact import evidence forbids claiming a complete absent draft answer.</summary>
    AbsenceNotClaimableOverRetainedEvidence = 9,

    /// <summary>The examined candidate-occurrence count reached the declared draft cap plus one.</summary>
    CandidateOccurrenceBoundReached = 10,

    /// <summary>The grouped physical-candidate count reached the declared draft cap plus one.</summary>
    CandidateGroupBoundReached = 11,
}

/// <summary>Freezes one physical Portable-PDB import definition projected into the scoped draft view.</summary>
/// <remarks>
/// The projection is minted only by <see cref="StaticFieldV2ScopedContextOutcome"/>. It retains the unchanged W7
/// import fact, the scope level that declared it, the typed target disposition, and every exactly resolved target.
/// A non-exact target keeps the import retained rather than dropping it, and an inner alias that hides this one is
/// recorded through <see cref="IsShadowed"/> instead of removing the outer declaration.
/// </remarks>
public sealed class StaticFieldV2ScopedImportProjection : IEquatable<StaticFieldV2ScopedImportProjection>
{
    private const string CanonicalDomain = "static-field-v2-scoped-import-projection";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ScopedImportProjection(
        DumpPortablePdbImportFact fact,
        int scopeLevelIndex,
        StaticFieldV2ScopedImportKind kind,
        StaticFieldV2ScopedImportTargetDisposition targetDisposition,
        StaticFieldMetadataModuleIdentity? targetTypeModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeSpecificationPhysicalRowIdentity? targetTypeSpecificationRow,
        MetadataAssemblyReferencePhysicalRowIdentity? assemblyReferenceRow,
        StaticFieldMetadataModuleIdentity? assemblyReferenceTargetModule,
        bool isShadowed)
    {
        Fact = fact;
        ScopeLevelIndex = scopeLevelIndex;
        Kind = kind;
        TargetDisposition = targetDisposition;
        TargetTypeModule = targetTypeModule;
        TargetTypeDefinition = targetTypeDefinition;
        TargetTypeSpecificationRow = targetTypeSpecificationRow;
        AssemblyReferenceRow = assemblyReferenceRow;
        AssemblyReferenceTargetModule = assemblyReferenceTargetModule;
        IsShadowed = isShadowed;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(fact.Sha256, nameof(fact));
        writer.WriteInt32(scopeLevelIndex);
        writer.WriteInt32((int)kind);
        writer.WriteInt32((int)targetDisposition);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetTypeModule?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetTypeDefinition?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetTypeSpecificationRow?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, assemblyReferenceRow?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, assemblyReferenceTargetModule?.Sha256);
        writer.WriteBoolean(isShadowed);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged exact W7 Portable-PDB import draft fact behind this projection.</summary>
    public DumpPortablePdbImportFact Fact { get; }

    /// <summary>Gets the zero-based outer-to-inner scope level that declared this draft import.</summary>
    public int ScopeLevelIndex { get; }

    /// <summary>Gets the projected draft import category.</summary>
    public StaticFieldV2ScopedImportKind Kind { get; }

    /// <summary>Gets the typed physical target disposition of this draft import.</summary>
    public StaticFieldV2ScopedImportTargetDisposition TargetDisposition { get; }

    /// <summary>Gets the metadata module owning the resolved target TypeDef, otherwise null.</summary>
    public StaticFieldMetadataModuleIdentity? TargetTypeModule { get; }

    /// <summary>Gets the authority-issued target TypeDef of a type-bearing draft import, otherwise null.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? TargetTypeDefinition { get; }

    /// <summary>Gets the retained undecoded physical TypeSpec target row, otherwise null.</summary>
    public MetadataTypeSpecificationPhysicalRowIdentity? TargetTypeSpecificationRow { get; }

    /// <summary>Gets the exact physical AssemblyRef row this draft import names, otherwise null.</summary>
    public MetadataAssemblyReferencePhysicalRowIdentity? AssemblyReferenceRow { get; }

    /// <summary>Gets the single portfolio module the named AssemblyRef identity-binds, otherwise null.</summary>
    public StaticFieldMetadataModuleIdentity? AssemblyReferenceTargetModule { get; }

    /// <summary>Gets whether an inner same-name alias hides this retained outer draft alias.</summary>
    public bool IsShadowed { get; }

    /// <summary>Gets the decoded simple alias of this draft import, otherwise null.</summary>
    public string? Alias => Fact.Alias;

    /// <summary>Gets the decoded namespace or type target text of this draft import, otherwise null.</summary>
    public string? Target => Fact.Target;

    /// <summary>Gets whether this draft import's target resolved exactly.</summary>
    public bool IsTargetExact =>
        TargetDisposition is StaticFieldV2ScopedImportTargetDisposition.NotApplicable or
            StaticFieldV2ScopedImportTargetDisposition.TypeDefinitionResolved or
            StaticFieldV2ScopedImportTargetDisposition.TypeReferenceResolved or
            StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceResolved;

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft projection.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft projection.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two projected draft imports.</summary>
    /// <param name="other">The other draft projection.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ScopedImportProjection? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests projected draft import equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a projection with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ScopedImportProjection);

    /// <summary>Computes a deterministic hash code from immutable canonical draft projection content.</summary>
    /// <returns>A hash code for this canonical draft projection.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ScopedImportProjection Create(
        object mintCapability,
        DumpPortablePdbImportFact fact,
        int scopeLevelIndex,
        StaticFieldV2ScopedImportKind kind,
        StaticFieldV2ScopedImportTargetDisposition targetDisposition,
        StaticFieldMetadataModuleIdentity? targetTypeModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeSpecificationPhysicalRowIdentity? targetTypeSpecificationRow,
        MetadataAssemblyReferencePhysicalRowIdentity? assemblyReferenceRow,
        StaticFieldMetadataModuleIdentity? assemblyReferenceTargetModule,
        bool isShadowed)
    {
        if (!StaticFieldV2ScopedContextOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A scoped import projection requires the context outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(fact);
        ArgumentOutOfRangeException.ThrowIfNegative(scopeLevelIndex);
        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        ExpressionV2ContractEncoding.RequireDefined(targetDisposition, nameof(targetDisposition));
        if ((targetTypeDefinition is null) != (targetTypeModule is null))
        {
            throw new ArgumentException(
                "A resolved target TypeDef and its owning module must be retained together.",
                nameof(targetTypeDefinition));
        }

        return new StaticFieldV2ScopedImportProjection(
            fact,
            scopeLevelIndex,
            kind,
            targetDisposition,
            targetTypeModule,
            targetTypeDefinition,
            targetTypeSpecificationRow,
            assemblyReferenceRow,
            assemblyReferenceTargetModule,
            isShadowed);
    }
}

/// <summary>Freezes one retained draft fact that an inner alias hides an outer same-name alias.</summary>
/// <remarks>
/// The row is minted only by <see cref="StaticFieldV2ScopedContextOutcome"/>. Both declarations remain retained in
/// their own scope levels; this row states which one hides which so provenance shows the hiding rather than losing
/// the outer declaration.
/// </remarks>
public sealed class StaticFieldV2ScopedAliasShadowingIdentity :
    IEquatable<StaticFieldV2ScopedAliasShadowingIdentity>
{
    private const string CanonicalDomain = "static-field-v2-scoped-alias-shadowing";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ScopedAliasShadowingIdentity(
        string aliasName,
        StaticFieldV2ScopedImportProjection hidingImport,
        StaticFieldV2ScopedImportProjection shadowedImport)
    {
        AliasName = aliasName;
        HidingImport = hidingImport;
        ShadowedImport = shadowedImport;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(aliasName);
        writer.WriteSha256(hidingImport.Sha256, nameof(hidingImport));
        writer.WriteSha256(shadowedImport.Sha256, nameof(shadowedImport));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact decoded alias name declared by both retained draft imports.</summary>
    public string AliasName { get; }

    /// <summary>Gets the inner draft import whose alias declaration hides the outer one.</summary>
    public StaticFieldV2ScopedImportProjection HidingImport { get; }

    /// <summary>Gets the retained outer draft import whose alias declaration is hidden.</summary>
    public StaticFieldV2ScopedImportProjection ShadowedImport { get; }

    /// <summary>Gets the zero-based scope level of the hiding draft declaration.</summary>
    public int HidingScopeLevelIndex => HidingImport.ScopeLevelIndex;

    /// <summary>Gets the zero-based scope level of the shadowed draft declaration.</summary>
    public int ShadowedScopeLevelIndex => ShadowedImport.ScopeLevelIndex;

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft shadowing row.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft shadowing row.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two draft alias-shadowing rows.</summary>
    /// <param name="other">The other draft shadowing row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ScopedAliasShadowingIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft alias-shadowing equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ScopedAliasShadowingIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft shadowing content.</summary>
    /// <returns>A hash code for this canonical draft shadowing row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ScopedAliasShadowingIdentity Create(
        object mintCapability,
        string aliasName,
        StaticFieldV2ScopedImportProjection hidingImport,
        StaticFieldV2ScopedImportProjection shadowedImport)
    {
        if (!StaticFieldV2ScopedContextOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An alias-shadowing row requires the context outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(hidingImport);
        ArgumentNullException.ThrowIfNull(shadowedImport);
        ExpressionV2ContractEncoding.RequireText(
            aliasName,
            nameof(aliasName),
            StaticFieldV2Limits.MaximumIdentifierCharacterCount);
        if (hidingImport.ScopeLevelIndex <= shadowedImport.ScopeLevelIndex)
        {
            throw new ArgumentException(
                "A hiding alias declaration must be strictly inner to the shadowed declaration.",
                nameof(hidingImport));
        }

        return new StaticFieldV2ScopedAliasShadowingIdentity(aliasName, hidingImport, shadowedImport);
    }
}

/// <summary>Freezes one projected active ImportScope draft level and its own projected imports.</summary>
/// <remarks>
/// The row is minted only by <see cref="StaticFieldV2ScopedContextOutcome"/>. Level zero is the outermost active
/// scope. Imports contribute candidates only at their own level, so this row is the complete import evidence of one
/// declaration level rather than an accumulated set.
/// </remarks>
public sealed class StaticFieldV2ScopedScopeLevelIdentity : IEquatable<StaticFieldV2ScopedScopeLevelIdentity>
{
    private const string CanonicalDomain = "static-field-v2-scoped-scope-level";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2ScopedImportProjection> imports;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ScopedScopeLevelIdentity(
        int levelIndex,
        DumpPortablePdbImportScopeIdentity scope,
        ImmutableArray<StaticFieldV2ScopedImportProjection> imports)
    {
        LevelIndex = levelIndex;
        Scope = scope;
        this.imports = ExpressionV2ContractEncoding.Copy(imports);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(levelIndex);
        writer.WriteSha256(scope.Sha256, nameof(scope));
        writer.WriteInt32(imports.Length);
        foreach (var import in imports)
        {
            writer.WriteSha256(import.Sha256, nameof(imports));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based outer-to-inner draft level index of this active scope.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets the unchanged exact active ImportScope draft row projected by this level.</summary>
    public DumpPortablePdbImportScopeIdentity Scope { get; }

    /// <summary>Gets the non-nil ImportScope token of this draft level.</summary>
    public int ImportScopeToken => Scope.ImportScopeToken;

    /// <summary>Gets a defensive physical-ordinal copy of every draft import declared at this level.</summary>
    public ImmutableArray<StaticFieldV2ScopedImportProjection> Imports =>
        ExpressionV2ContractEncoding.Copy(imports);

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft scope level.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft scope level.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two projected draft scope levels.</summary>
    /// <param name="other">The other draft scope level.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ScopedScopeLevelIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests projected draft scope-level equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a level with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ScopedScopeLevelIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft scope-level content.</summary>
    /// <returns>A hash code for this canonical draft scope level.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<StaticFieldV2ScopedImportProjection> ImportsCore => imports;

    internal static StaticFieldV2ScopedScopeLevelIdentity Create(
        object mintCapability,
        int levelIndex,
        DumpPortablePdbImportScopeIdentity scope,
        ImmutableArray<StaticFieldV2ScopedImportProjection> imports)
    {
        if (!StaticFieldV2ScopedContextOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A scoped scope level requires the context outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            imports,
            nameof(imports),
            StaticFieldV2Limits.MaximumImportFactCount + 1);
        return new StaticFieldV2ScopedScopeLevelIdentity(levelIndex, scope, copied);
    }
}

/// <summary>Freezes one derived namespace-declaration draft level of the selected declaring type.</summary>
/// <remarks>
/// The row is minted only by <see cref="StaticFieldV2ScopedContextOutcome"/>. Level zero is the innermost declared
/// namespace and the final level is always the global namespace, spelled as empty text.
/// </remarks>
public sealed class StaticFieldV2ScopedNamespaceLevelIdentity :
    IEquatable<StaticFieldV2ScopedNamespaceLevelIdentity>
{
    private const string CanonicalDomain = "static-field-v2-scoped-namespace-level";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ScopedNamespaceLevelIdentity(int levelIndex, string namespaceName)
    {
        LevelIndex = levelIndex;
        NamespaceName = namespaceName;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(levelIndex);
        writer.WriteString(namespaceName);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based innermost-first draft level index of this namespace declaration.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets the exact namespace text of this draft level, empty for the global namespace.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets whether this draft level is the global namespace.</summary>
    public bool IsGlobalNamespace => NamespaceName.Length == 0;

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft namespace level.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft namespace level.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two derived draft namespace levels.</summary>
    /// <param name="other">The other draft namespace level.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ScopedNamespaceLevelIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests derived draft namespace-level equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a level with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ScopedNamespaceLevelIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft namespace-level content.</summary>
    /// <returns>A hash code for this canonical draft namespace level.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ScopedNamespaceLevelIdentity Create(
        object mintCapability,
        int levelIndex,
        string namespaceName)
    {
        if (!StaticFieldV2ScopedContextOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A scoped namespace level requires the context outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
        ExpressionV2ContractEncoding.RequireText(
            namespaceName,
            nameof(namespaceName),
            StaticFieldV2Limits.MaximumExpressionCharacterCount,
            allowEmpty: true);
        return new StaticFieldV2ScopedNamespaceLevelIdentity(levelIndex, namespaceName);
    }
}

/// <summary>Freezes one complete scoped-context draft projection request.</summary>
/// <remarks>
/// The request names one selected metadata module, the exact ordered outer-to-inner active ImportScope chain, the
/// selected declaring type's authority TypeDef, and the exact ancestry and TypeRef resolution draft portfolios. It
/// carries no runtime, no storage, no expression, and no member evidence.
/// </remarks>
public sealed class StaticFieldV2ScopedContextRequest : IEquatable<StaticFieldV2ScopedContextRequest>
{
    private const string CanonicalDomain = "static-field-v2-scoped-context-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ScopedContextRequest(
        StaticFieldMetadataModuleIdentity selectedModule,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes,
        MetadataTypeDefinitionAuthorityIdentity selectedTypeDefinition,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio)
    {
        SelectedModule = selectedModule;
        this.importScopes = importScopes;
        SelectedTypeDefinition = selectedTypeDefinition;
        AncestryPortfolio = ancestryPortfolio;
        ResolutionPortfolio = resolutionPortfolio;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(selectedModule.Sha256, nameof(selectedModule));
        writer.WriteInt32(importScopes.IsDefault ? -1 : importScopes.Length);
        if (!importScopes.IsDefault)
        {
            foreach (var scope in importScopes)
            {
                ExpressionV2ContractEncoding.WriteOptionalDigest(writer, scope?.Sha256);
            }
        }
        writer.WriteSha256(selectedTypeDefinition.Sha256, nameof(selectedTypeDefinition));
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        writer.WriteSha256(resolutionPortfolio.Sha256, nameof(resolutionPortfolio));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact selected metadata module whose scoped draft context is projected.</summary>
    public StaticFieldMetadataModuleIdentity SelectedModule { get; }

    /// <summary>Gets a defensive outer-to-inner copy of the supplied ImportScope chain, default when absent.</summary>
    public ImmutableArray<DumpPortablePdbImportScopeIdentity> ImportScopes =>
        importScopes.IsDefault ? default : ExpressionV2ContractEncoding.Copy(importScopes);

    /// <summary>Gets whether the supplied ImportScope chain vector was explicitly initialized.</summary>
    public bool IsImportScopeChainInitialized => !importScopes.IsDefault;

    /// <summary>Gets the selected declaring type's authority-issued draft TypeDef.</summary>
    public MetadataTypeDefinitionAuthorityIdentity SelectedTypeDefinition { get; }

    /// <summary>Gets the ancestry authority draft portfolio prerequisite.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets the TypeRef resolution draft portfolio used to resolve import target tokens.</summary>
    public MetadataTypeReferenceResolutionPortfolioIdentity ResolutionPortfolio { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete scoped-context draft projection request.</summary>
    /// <param name="selectedModule">The exact selected metadata module.</param>
    /// <param name="importScopes">
    /// The exact active ImportScope chain in outer-to-inner order. A default array is admitted here and becomes a
    /// typed draft stop rather than an exception, so a caller can replay malformed input.
    /// </param>
    /// <param name="selectedTypeDefinition">The selected declaring type's authority-issued draft TypeDef.</param>
    /// <param name="ancestryPortfolio">The ancestry authority draft portfolio prerequisite.</param>
    /// <param name="resolutionPortfolio">The TypeRef resolution draft portfolio prerequisite.</param>
    /// <returns>A sealed immutable draft request with defensively copied evidence.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentException">An initialized scope vector contains a null entry.</exception>
    public static StaticFieldV2ScopedContextRequest Create(
        StaticFieldMetadataModuleIdentity selectedModule,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes,
        MetadataTypeDefinitionAuthorityIdentity selectedTypeDefinition,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio)
    {
        ArgumentNullException.ThrowIfNull(selectedModule);
        ArgumentNullException.ThrowIfNull(selectedTypeDefinition);
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        ArgumentNullException.ThrowIfNull(resolutionPortfolio);

        var scopes = default(ImmutableArray<DumpPortablePdbImportScopeIdentity>);
        if (!importScopes.IsDefault)
        {
            foreach (var scope in importScopes)
            {
                if (scope is null)
                {
                    throw new ArgumentException(
                        "An initialized import-scope chain cannot contain null entries.",
                        nameof(importScopes));
                }
            }
            scopes = ImmutableArray.CreateRange(importScopes);
        }

        return new StaticFieldV2ScopedContextRequest(
            selectedModule,
            scopes,
            selectedTypeDefinition,
            ancestryPortfolio,
            resolutionPortfolio);
    }

    /// <summary>Tests canonical equality between two scoped-context draft requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ScopedContextRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests scoped-context draft request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ScopedContextRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<DumpPortablePdbImportScopeIdentity> ImportScopesCore => importScopes;
}

/// <summary>Freezes the complete draft projection of one exact active ImportScope chain into a scoped view.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of every import projection, alias-shadowing row, scope level, and
/// namespace level it retains. An exact projection retains the complete ordered semantic view; a stop is prefix-free
/// and retains nothing, because a partial projection is not evidence about the active context.
/// <para>
/// Declaration levels pair innermost-first: declaration level zero pairs the innermost namespace level with the
/// innermost active import scope. That pairing is a declared draft convention recorded through
/// <see cref="StaticFieldV2ScopedContextCoverageBoundary.ImportScopeToNamespaceLevelPairingDeclared"/>.
/// </para>
/// </remarks>
public sealed class StaticFieldV2ScopedContextOutcome : IEquatable<StaticFieldV2ScopedContextOutcome>
{
    /// <summary>Gets the maximum active import-scope draft depth.</summary>
    public const int MaximumImportScopeDepth = StaticFieldV2Limits.MaximumImportScopeDepth;

    /// <summary>Gets the maximum cumulative import-fact draft count.</summary>
    public const int MaximumImportFactCount = StaticFieldV2Limits.MaximumImportFactCount;

    /// <summary>Gets the maximum semantic namespace-level draft count.</summary>
    public const int MaximumNamespaceLevelCount = StaticFieldV2Limits.MaximumNamespaceLevelCount;

    private const string CanonicalDomain = "static-field-v2-scoped-context-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2ScopedScopeLevelIdentity> scopeLevels;
    private readonly ImmutableArray<StaticFieldV2ScopedNamespaceLevelIdentity> namespaceLevels;
    private readonly ImmutableArray<StaticFieldV2ScopedAliasShadowingIdentity> aliasShadowings;
    private readonly ImmutableArray<StaticFieldV2ScopedContextCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ScopedContextOutcome(
        StaticFieldV2ScopedContextResultKind resultKind,
        StaticFieldV2ScopedContextIssue issue,
        StaticFieldV2ScopedContextRequest request,
        ImmutableArray<StaticFieldV2ScopedScopeLevelIdentity> scopeLevels,
        ImmutableArray<StaticFieldV2ScopedNamespaceLevelIdentity> namespaceLevels,
        ImmutableArray<StaticFieldV2ScopedAliasShadowingIdentity> aliasShadowings,
        ImmutableArray<StaticFieldV2ScopedContextCoverageBoundary> declaredCoverageBoundaries,
        bool isExhaustive,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        this.scopeLevels = ExpressionV2ContractEncoding.Copy(scopeLevels);
        this.namespaceLevels = ExpressionV2ContractEncoding.Copy(namespaceLevels);
        this.aliasShadowings = ExpressionV2ContractEncoding.Copy(aliasShadowings);
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        IsExhaustive = isExhaustive;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteInt32(scopeLevels.Length);
        foreach (var level in scopeLevels)
        {
            writer.WriteSha256(level.Sha256, nameof(scopeLevels));
        }
        writer.WriteInt32(namespaceLevels.Length);
        foreach (var level in namespaceLevels)
        {
            writer.WriteSha256(level.Sha256, nameof(namespaceLevels));
        }
        writer.WriteInt32(aliasShadowings.Length);
        foreach (var shadowing in aliasShadowings)
        {
            writer.WriteSha256(shadowing.Sha256, nameof(aliasShadowings));
        }
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        writer.WriteBoolean(isExhaustive);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft projection is exact, non-exact, or invalid.</summary>
    public StaticFieldV2ScopedContextResultKind ResultKind { get; }

    /// <summary>Gets the typed draft projection issue, or none for an exact projection.</summary>
    public StaticFieldV2ScopedContextIssue Issue { get; }

    /// <summary>Gets the complete scoped-context draft request that was projected.</summary>
    public StaticFieldV2ScopedContextRequest Request { get; }

    /// <summary>Gets a defensive outer-to-inner copy of projected draft scope levels, empty for a stop.</summary>
    public ImmutableArray<StaticFieldV2ScopedScopeLevelIdentity> ScopeLevels =>
        ExpressionV2ContractEncoding.Copy(scopeLevels);

    /// <summary>Gets a defensive innermost-first copy of derived draft namespace levels, empty for a stop.</summary>
    public ImmutableArray<StaticFieldV2ScopedNamespaceLevelIdentity> NamespaceLevels =>
        ExpressionV2ContractEncoding.Copy(namespaceLevels);

    /// <summary>Gets a defensive copy of every retained draft alias-shadowing row.</summary>
    public ImmutableArray<StaticFieldV2ScopedAliasShadowingIdentity> AliasShadowings =>
        ExpressionV2ContractEncoding.Copy(aliasShadowings);

    /// <summary>Gets a defensive ascending copy of every declared draft coverage boundary.</summary>
    public ImmutableArray<StaticFieldV2ScopedContextCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets whether every retained draft import projected an exact physical target.</summary>
    public bool IsExhaustive { get; }

    /// <summary>Gets the count of declaration levels searched by the contextual draft route.</summary>
    public int DeclarationLevelCount => Math.Max(scopeLevels.Length, namespaceLevels.Length);

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Gets every active non-shadowed draft alias declaration for one decoded alias name.</summary>
    /// <param name="aliasName">The exact decoded alias name to look up.</param>
    /// <remarks>
    /// Only the innermost scope level that declares the name contributes; outer declarations remain retained but are
    /// marked shadowed. Two active declarations at the same level are returned together so the contextual draft route
    /// can report ambiguity instead of choosing by import order.
    /// </remarks>
    /// <returns>A defensive ordered draft array, empty when no active declaration exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aliasName"/> is null.</exception>
    public ImmutableArray<StaticFieldV2ScopedImportProjection> ActiveAliasProjections(string aliasName)
    {
        ArgumentNullException.ThrowIfNull(aliasName);
        var matches = ImmutableArray.CreateBuilder<StaticFieldV2ScopedImportProjection>();
        foreach (var level in scopeLevels)
        {
            foreach (var import in level.ImportsCore)
            {
                if (!import.IsShadowed &&
                    import.Alias is { } alias &&
                    string.Equals(alias, aliasName, StringComparison.Ordinal))
                {
                    matches.Add(import);
                }
            }
        }
        return matches.ToImmutable();
    }

    /// <summary>Tests canonical equality between two scoped-context draft projections.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ScopedContextOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests scoped-context draft projection equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ScopedContextOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal ImmutableArray<StaticFieldV2ScopedScopeLevelIdentity> ScopeLevelsCore => scopeLevels;

    internal ImmutableArray<StaticFieldV2ScopedNamespaceLevelIdentity> NamespaceLevelsCore => namespaceLevels;

    internal static StaticFieldV2ScopedImportProjection IssueImport(
        DumpPortablePdbImportFact fact,
        int scopeLevelIndex,
        StaticFieldV2ScopedImportKind kind,
        StaticFieldV2ScopedImportTargetDisposition targetDisposition,
        StaticFieldMetadataModuleIdentity? targetTypeModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeSpecificationPhysicalRowIdentity? targetTypeSpecificationRow,
        MetadataAssemblyReferencePhysicalRowIdentity? assemblyReferenceRow,
        StaticFieldMetadataModuleIdentity? assemblyReferenceTargetModule,
        bool isShadowed) =>
        StaticFieldV2ScopedImportProjection.Create(
            RowMintCapability,
            fact,
            scopeLevelIndex,
            kind,
            targetDisposition,
            targetTypeModule,
            targetTypeDefinition,
            targetTypeSpecificationRow,
            assemblyReferenceRow,
            assemblyReferenceTargetModule,
            isShadowed);

    internal static StaticFieldV2ScopedAliasShadowingIdentity IssueShadowing(
        string aliasName,
        StaticFieldV2ScopedImportProjection hidingImport,
        StaticFieldV2ScopedImportProjection shadowedImport) =>
        StaticFieldV2ScopedAliasShadowingIdentity.Create(
            RowMintCapability,
            aliasName,
            hidingImport,
            shadowedImport);

    internal static StaticFieldV2ScopedScopeLevelIdentity IssueScopeLevel(
        int levelIndex,
        DumpPortablePdbImportScopeIdentity scope,
        ImmutableArray<StaticFieldV2ScopedImportProjection> imports) =>
        StaticFieldV2ScopedScopeLevelIdentity.Create(RowMintCapability, levelIndex, scope, imports);

    internal static StaticFieldV2ScopedNamespaceLevelIdentity IssueNamespaceLevel(
        int levelIndex,
        string namespaceName) =>
        StaticFieldV2ScopedNamespaceLevelIdentity.Create(RowMintCapability, levelIndex, namespaceName);

    internal static StaticFieldV2ScopedContextOutcome IssueExact(
        StaticFieldV2ScopedContextRequest request,
        ImmutableArray<StaticFieldV2ScopedScopeLevelIdentity> scopeLevels,
        ImmutableArray<StaticFieldV2ScopedNamespaceLevelIdentity> namespaceLevels,
        ImmutableArray<StaticFieldV2ScopedAliasShadowingIdentity> aliasShadowings,
        ImmutableArray<StaticFieldV2ScopedContextCoverageBoundary> declaredCoverageBoundaries,
        bool isExhaustive,
        int observedCount) =>
        new(
            StaticFieldV2ScopedContextResultKind.Exact,
            StaticFieldV2ScopedContextIssue.None,
            request,
            scopeLevels,
            namespaceLevels,
            aliasShadowings,
            declaredCoverageBoundaries,
            isExhaustive,
            null,
            observedCount);

    internal static StaticFieldV2ScopedContextOutcome IssueStop(
        StaticFieldV2ScopedContextResultKind resultKind,
        StaticFieldV2ScopedContextIssue issue,
        StaticFieldV2ScopedContextRequest request,
        ImmutableArray<StaticFieldV2ScopedContextCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            resultKind,
            issue,
            request,
            [],
            [],
            [],
            declaredCoverageBoundaries,
            false,
            reachedBound,
            observedCount);
}

/// <summary>Freezes one accepted contextual draft match between an owner name and an authority-issued chain.</summary>
/// <remarks>
/// The candidate is minted only by <see cref="StaticFieldV2ContextualBindingOutcome"/>. It retains the declaration
/// level that produced it, which scoped source contributed it, and the exact import projection responsible when one
/// exists, so import order can be proven never to have selected the answer.
/// </remarks>
public sealed class StaticFieldV2ContextualCandidateIdentity :
    IEquatable<StaticFieldV2ContextualCandidateIdentity>
{
    private const string CanonicalDomain = "static-field-v2-contextual-candidate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ContextualCandidateIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataNamedTypeDefinitionChainIdentity chain,
        int levelIndex,
        StaticFieldV2ContextualCandidateSource source,
        StaticFieldV2ScopedImportProjection? contributingImport,
        int namespaceSegmentCount,
        int partitionIndex)
    {
        SourceModule = sourceModule;
        Chain = chain;
        LevelIndex = levelIndex;
        Source = source;
        ContributingImport = contributingImport;
        NamespaceSegmentCount = namespaceSegmentCount;
        PartitionIndex = partitionIndex;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteSha256(chain.Sha256, nameof(chain));
        writer.WriteInt32(levelIndex);
        writer.WriteInt32((int)source);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, contributingImport?.Sha256);
        writer.WriteInt32(namespaceSegmentCount);
        writer.WriteInt32(partitionIndex);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module whose draft chain catalog produced this candidate.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact authority-derived outer-to-inner named-TypeDef draft chain that matched.</summary>
    public MetadataNamedTypeDefinitionChainIdentity Chain { get; }

    /// <summary>Gets the zero-based innermost-first declaration level that produced this candidate.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets which scoped draft source contributed this candidate at that level.</summary>
    public StaticFieldV2ContextualCandidateSource Source { get; }

    /// <summary>Gets the exact import projection that contributed this candidate, otherwise null.</summary>
    public StaticFieldV2ScopedImportProjection? ContributingImport { get; }

    /// <summary>Gets the count of owner segments this candidate consumed as extra namespace text.</summary>
    public int NamespaceSegmentCount { get; }

    /// <summary>Gets the zero-based descriptor partition index that produced this candidate.</summary>
    public int PartitionIndex { get; }

    /// <summary>Gets the final authority-issued TypeDef named by the matched draft chain.</summary>
    public MetadataTypeDefinitionAuthorityIdentity FinalTypeDefinition => Chain.FinalTypeDefinition;

    /// <summary>Gets the non-nil final TypeDef token used as half of the physical draft group key.</summary>
    public int FinalTypeDefinitionToken => Chain.FinalTypeDefinitionToken;

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft candidate.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft candidate.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two contextual draft candidates.</summary>
    /// <param name="other">The other draft candidate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ContextualCandidateIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests contextual draft candidate equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a candidate with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ContextualCandidateIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft candidate content.</summary>
    /// <returns>A hash code for this canonical draft candidate.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ContextualCandidateIdentity Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataNamedTypeDefinitionChainIdentity chain,
        int levelIndex,
        StaticFieldV2ContextualCandidateSource source,
        StaticFieldV2ScopedImportProjection? contributingImport,
        int namespaceSegmentCount,
        int partitionIndex)
    {
        if (!StaticFieldV2ContextualBindingOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A contextual candidate requires the binding outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(chain);
        ExpressionV2ContractEncoding.RequireDefined(source, nameof(source));
        ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(namespaceSegmentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(partitionIndex);
        if (chain.IsModulePseudoType)
        {
            throw new ArgumentException(
                "The module pseudo-type can never be an accepted contextual candidate.",
                nameof(chain));
        }

        return new StaticFieldV2ContextualCandidateIdentity(
            sourceModule,
            chain,
            levelIndex,
            source,
            contributingImport,
            namespaceSegmentCount,
            partitionIndex);
    }
}

/// <summary>Freezes one physical contextual draft candidate: a metadata module and one final authority TypeDef.</summary>
/// <remarks>
/// Candidates that name the same physical pair converge into exactly one group even when different scoped sources
/// contributed them, which is what proves that import order never selects an answer. Distinct pairs remain distinct
/// groups and are therefore ambiguous.
/// </remarks>
public sealed class StaticFieldV2ContextualCandidateGroup : IEquatable<StaticFieldV2ContextualCandidateGroup>
{
    private const string CanonicalDomain = "static-field-v2-contextual-candidate-group";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ContextualCandidateGroup(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity finalTypeDefinition,
        ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates)
    {
        SourceModule = sourceModule;
        FinalTypeDefinition = finalTypeDefinition;
        this.candidates = ExpressionV2ContractEncoding.Copy(candidates);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteSha256(finalTypeDefinition.Sha256, nameof(finalTypeDefinition));
        writer.WriteInt32(candidates.Length);
        foreach (var candidate in candidates)
        {
            writer.WriteSha256(candidate.Sha256, nameof(candidates));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module that owns this physical draft candidate.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact final authority-issued TypeDef of this physical draft candidate.</summary>
    public MetadataTypeDefinitionAuthorityIdentity FinalTypeDefinition { get; }

    /// <summary>Gets the non-nil final TypeDef token of this physical draft candidate.</summary>
    public int FinalTypeDefinitionToken => FinalTypeDefinition.TypeDefinitionToken;

    /// <summary>Gets a defensive copy of every converged draft candidate in examination order.</summary>
    public ImmutableArray<StaticFieldV2ContextualCandidateIdentity> Candidates =>
        ExpressionV2ContractEncoding.Copy(candidates);

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft group.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft group.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two physical contextual draft groups.</summary>
    /// <param name="other">The other draft group.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ContextualCandidateGroup? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical contextual draft group equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a group with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ContextualCandidateGroup);

    /// <summary>Computes a deterministic hash code from immutable canonical draft group content.</summary>
    /// <returns>A hash code for this canonical draft group.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ContextualCandidateGroup Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity finalTypeDefinition,
        ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates)
    {
        if (!StaticFieldV2ContextualBindingOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A contextual candidate group requires the binding outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(finalTypeDefinition);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            candidates,
            nameof(candidates),
            StaticFieldV2Limits.MaximumNameCandidateOccurrenceCount + 1);
        if (copied.IsEmpty)
        {
            throw new ArgumentException("A physical candidate group requires at least one candidate.", nameof(candidates));
        }
        foreach (var candidate in copied)
        {
            if (!candidate.SourceModule.Equals(sourceModule) ||
                !candidate.FinalTypeDefinition.Equals(finalTypeDefinition))
            {
                throw new ArgumentException(
                    "Every converged candidate must name one identical physical module and TypeDef pair.",
                    nameof(candidates));
            }
        }

        return new StaticFieldV2ContextualCandidateGroup(sourceModule, finalTypeDefinition, copied);
    }
}

/// <summary>Freezes one examined declaration level of the contextual draft search for one partition.</summary>
/// <remarks>
/// The row is minted only by <see cref="StaticFieldV2ContextualBindingOutcome"/>. Every level examined before the
/// winning one is retained with its own typed rejection evidence, so the first-viable-level rule is visible rather
/// than implied.
/// </remarks>
public sealed class StaticFieldV2ContextualLevelResult : IEquatable<StaticFieldV2ContextualLevelResult>
{
    private const string CanonicalDomain = "static-field-v2-contextual-level-result";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ContextualLevelResult(
        int levelIndex,
        string namespaceName,
        bool hasNamespaceLevel,
        int? scopeLevelIndex,
        StaticFieldV2ContextualLevelDisposition disposition,
        ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates,
        int examinedOccurrenceCount,
        StaticFieldV2ContextualRejectionReason? firstRejectionReason)
    {
        LevelIndex = levelIndex;
        NamespaceName = namespaceName;
        HasNamespaceLevel = hasNamespaceLevel;
        ScopeLevelIndex = scopeLevelIndex;
        Disposition = disposition;
        this.candidates = ExpressionV2ContractEncoding.Copy(candidates);
        ExaminedOccurrenceCount = examinedOccurrenceCount;
        FirstRejectionReason = firstRejectionReason;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(levelIndex);
        writer.WriteString(namespaceName);
        writer.WriteBoolean(hasNamespaceLevel);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, scopeLevelIndex);
        writer.WriteInt32((int)disposition);
        writer.WriteInt32(candidates.Length);
        foreach (var candidate in candidates)
        {
            writer.WriteSha256(candidate.Sha256, nameof(candidates));
        }
        writer.WriteInt32(examinedOccurrenceCount);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, firstRejectionReason);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based innermost-first declaration level index examined by this draft row.</summary>
    public int LevelIndex { get; }

    /// <summary>Gets the namespace text searched at this draft level, empty for the global namespace.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets whether this draft level pairs with a derived namespace-declaration level.</summary>
    public bool HasNamespaceLevel { get; }

    /// <summary>Gets the outer-to-inner scope level paired with this draft level, otherwise null.</summary>
    public int? ScopeLevelIndex { get; }

    /// <summary>Gets whether this draft level was selected or produced no candidate.</summary>
    public StaticFieldV2ContextualLevelDisposition Disposition { get; }

    /// <summary>Gets a defensive copy of every candidate this draft level produced.</summary>
    public ImmutableArray<StaticFieldV2ContextualCandidateIdentity> Candidates =>
        ExpressionV2ContractEncoding.Copy(candidates);

    /// <summary>Gets the exact count of chain occurrences examined at this draft level.</summary>
    public int ExaminedOccurrenceCount { get; }

    /// <summary>Gets the retained typed draft rejection reason, or null when nothing was rejected.</summary>
    public StaticFieldV2ContextualRejectionReason? FirstRejectionReason { get; }

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft level result.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft level result.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two contextual draft level results.</summary>
    /// <param name="other">The other draft level result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ContextualLevelResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests contextual draft level-result equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a level result with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ContextualLevelResult);

    /// <summary>Computes a deterministic hash code from immutable canonical draft level-result content.</summary>
    /// <returns>A hash code for this canonical draft level result.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ContextualLevelResult Create(
        object mintCapability,
        int levelIndex,
        string namespaceName,
        bool hasNamespaceLevel,
        int? scopeLevelIndex,
        StaticFieldV2ContextualLevelDisposition disposition,
        ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates,
        int examinedOccurrenceCount,
        StaticFieldV2ContextualRejectionReason? firstRejectionReason)
    {
        if (!StaticFieldV2ContextualBindingOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A contextual level result requires the binding outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(examinedOccurrenceCount);
        ExpressionV2ContractEncoding.RequireDefined(disposition, nameof(disposition));
        ExpressionV2ContractEncoding.RequireText(
            namespaceName,
            nameof(namespaceName),
            StaticFieldV2Limits.MaximumExpressionCharacterCount,
            allowEmpty: true);
        if (scopeLevelIndex is { } scopeIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(scopeIndex, nameof(scopeLevelIndex));
        }
        if (firstRejectionReason.HasValue)
        {
            ExpressionV2ContractEncoding.RequireDefined(firstRejectionReason.Value, nameof(firstRejectionReason));
        }
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            candidates,
            nameof(candidates),
            StaticFieldV2Limits.MaximumNameCandidateOccurrenceCount + 1);
        if (copied.IsEmpty != (disposition == StaticFieldV2ContextualLevelDisposition.NoCandidateAtLevel))
        {
            throw new ArgumentException(
                "A level disposition must be derived from whether that level produced any candidate.",
                nameof(disposition));
        }

        return new StaticFieldV2ContextualLevelResult(
            levelIndex,
            namespaceName,
            hasNamespaceLevel,
            scopeLevelIndex,
            disposition,
            copied,
            examinedOccurrenceCount,
            firstRejectionReason);
    }
}

/// <summary>Freezes the complete contextual draft answer derived for one qualified-owner partition.</summary>
/// <remarks>
/// The row is minted only by <see cref="StaticFieldV2ContextualBindingOutcome"/>. It retains every examined level in
/// innermost-to-outermost order, the winning level index when one exists, and the physical groups derived from that
/// winning level alone.
/// </remarks>
public sealed class StaticFieldV2ContextualPartitionResult : IEquatable<StaticFieldV2ContextualPartitionResult>
{
    private const string CanonicalDomain = "static-field-v2-contextual-partition-result";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2ContextualLevelResult> levels;
    private readonly ImmutableArray<StaticFieldV2ContextualCandidateGroup> groups;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ContextualPartitionResult(
        int partitionIndex,
        StaticFieldV2CandidateKind candidateKind,
        int ownerSegmentCount,
        StaticFieldV2ContextualBindingResultKind resultKind,
        ImmutableArray<StaticFieldV2ContextualLevelResult> levels,
        int? selectedLevelIndex,
        ImmutableArray<StaticFieldV2ContextualCandidateGroup> groups)
    {
        PartitionIndex = partitionIndex;
        CandidateKind = candidateKind;
        OwnerSegmentCount = ownerSegmentCount;
        ResultKind = resultKind;
        this.levels = ExpressionV2ContractEncoding.Copy(levels);
        SelectedLevelIndex = selectedLevelIndex;
        this.groups = ExpressionV2ContractEncoding.Copy(groups);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(partitionIndex);
        writer.WriteInt32((int)candidateKind);
        writer.WriteInt32(ownerSegmentCount);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32(levels.Length);
        foreach (var level in levels)
        {
            writer.WriteSha256(level.Sha256, nameof(levels));
        }
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, selectedLevelIndex);
        writer.WriteInt32(groups.Length);
        foreach (var group in groups)
        {
            writer.WriteSha256(group.Sha256, nameof(groups));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based descriptor partition index described by this draft result.</summary>
    public int PartitionIndex { get; }

    /// <summary>Gets the descriptor candidate kind, which is always a qualified owner for this draft route.</summary>
    public StaticFieldV2CandidateKind CandidateKind { get; }

    /// <summary>Gets the count of leading expression segments interpreted as the owner name.</summary>
    public int OwnerSegmentCount { get; }

    /// <summary>Gets whether this partition derived exactly one, no, or several physical draft candidates.</summary>
    public StaticFieldV2ContextualBindingResultKind ResultKind { get; }

    /// <summary>Gets a defensive innermost-first copy of every declaration level examined for this partition.</summary>
    public ImmutableArray<StaticFieldV2ContextualLevelResult> Levels =>
        ExpressionV2ContractEncoding.Copy(levels);

    /// <summary>Gets the winning declaration level index, or null when no level produced a candidate.</summary>
    public int? SelectedLevelIndex { get; }

    /// <summary>Gets a defensive copy of every physical draft group derived from the winning level.</summary>
    public ImmutableArray<StaticFieldV2ContextualCandidateGroup> Groups =>
        ExpressionV2ContractEncoding.Copy(groups);

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft partition result.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft partition result.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two contextual draft partition results.</summary>
    /// <param name="other">The other draft partition result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ContextualPartitionResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests contextual draft partition-result equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a partition result with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ContextualPartitionResult);

    /// <summary>Computes a deterministic hash code from immutable canonical draft partition-result content.</summary>
    /// <returns>A hash code for this canonical draft partition result.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2ContextualPartitionResult Create(
        object mintCapability,
        int partitionIndex,
        StaticFieldV2CandidateKind candidateKind,
        int ownerSegmentCount,
        StaticFieldV2ContextualBindingResultKind resultKind,
        ImmutableArray<StaticFieldV2ContextualLevelResult> levels,
        int? selectedLevelIndex,
        ImmutableArray<StaticFieldV2ContextualCandidateGroup> groups)
    {
        if (!StaticFieldV2ContextualBindingOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A contextual partition result requires the binding outcome's private mint capability.",
                nameof(mintCapability));
        }

        ExpressionV2ContractEncoding.RequireDefined(candidateKind, nameof(candidateKind));
        ExpressionV2ContractEncoding.RequireDefined(resultKind, nameof(resultKind));
        ArgumentOutOfRangeException.ThrowIfNegative(partitionIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(ownerSegmentCount);
        if (selectedLevelIndex is { } selected)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(selected, nameof(selectedLevelIndex));
        }
        var copiedLevels = ExpressionV2ContractEncoding.CopyRequired(
            levels,
            nameof(levels),
            StaticFieldV2Limits.MaximumImportScopeDepth + StaticFieldV2Limits.MaximumNamespaceLevelCount + 1);
        var copiedGroups = ExpressionV2ContractEncoding.CopyRequired(
            groups,
            nameof(groups),
            StaticFieldV2Limits.MaximumGroupedPhysicalCandidateCount + 1);
        var expected = copiedGroups.Length switch
        {
            0 => StaticFieldV2ContextualBindingResultKind.Absent,
            1 => StaticFieldV2ContextualBindingResultKind.Exact,
            _ => StaticFieldV2ContextualBindingResultKind.Ambiguous,
        };
        if (resultKind != expected)
        {
            throw new ArgumentException(
                "A partition result kind must be derived from its retained physical group count.",
                nameof(resultKind));
        }
        if (copiedGroups.IsEmpty != (selectedLevelIndex is null))
        {
            throw new ArgumentException(
                "A winning level index must be retained exactly when the partition derived a candidate.",
                nameof(selectedLevelIndex));
        }

        return new StaticFieldV2ContextualPartitionResult(
            partitionIndex,
            candidateKind,
            ownerSegmentCount,
            resultKind,
            copiedLevels,
            selectedLevelIndex,
            copiedGroups);
    }
}

/// <summary>Freezes the complete draft outcome of contextual owner-name binding through a scoped view.</summary>
/// <remarks>
/// This sealed draft outcome is the sole issuer of every candidate, physical group, level result, and partition
/// result it retains. A complete derived answer - <see cref="StaticFieldV2ContextualBindingResultKind.Exact"/>,
/// <see cref="StaticFieldV2ContextualBindingResultKind.Absent"/>, or
/// <see cref="StaticFieldV2ContextualBindingResultKind.Ambiguous"/> - retains its partition and level evidence
/// because those are the derivation itself. A stop -
/// <see cref="StaticFieldV2ContextualBindingResultKind.NonExact"/>,
/// <see cref="StaticFieldV2ContextualBindingResultKind.Invalid"/>, or
/// <see cref="StaticFieldV2ContextualBindingResultKind.Unsupported"/> - is prefix-free and retains neither partition
/// results nor a selected candidate.
/// </remarks>
public sealed class StaticFieldV2ContextualBindingOutcome : IEquatable<StaticFieldV2ContextualBindingOutcome>
{
    /// <summary>Gets the maximum examined contextual candidate-occurrence draft count.</summary>
    public const int MaximumCandidateOccurrenceCount = StaticFieldV2Limits.MaximumNameCandidateOccurrenceCount;

    /// <summary>Gets the maximum grouped physical-candidate draft count.</summary>
    public const int MaximumGroupedPhysicalCandidateCount =
        StaticFieldV2Limits.MaximumGroupedPhysicalCandidateCount;

    private const string CanonicalDomain = "static-field-v2-contextual-binding-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2ContextualPartitionResult> partitionResults;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ContextualBindingOutcome(
        StaticFieldV2ContextualBindingResultKind resultKind,
        StaticFieldV2ContextualBindingIssue issue,
        StaticFieldV2ExpressionDescriptor expression,
        StaticFieldV2ScopedContextOutcome context,
        ImmutableArray<StaticFieldV2ContextualPartitionResult> partitionResults,
        StaticFieldV2ContextualCandidateGroup? selectedCandidate,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        Expression = expression;
        Context = context;
        this.partitionResults = ExpressionV2ContractEncoding.Copy(partitionResults);
        SelectedCandidate = selectedCandidate;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(expression.Sha256, nameof(expression));
        writer.WriteSha256(context.Sha256, nameof(context));
        writer.WriteInt32(partitionResults.Length);
        foreach (var partitionResult in partitionResults)
        {
            writer.WriteSha256(partitionResult.Sha256, nameof(partitionResults));
        }
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedCandidate?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft binding is exact, absent, ambiguous, non-exact, invalid, or unsupported.</summary>
    public StaticFieldV2ContextualBindingResultKind ResultKind { get; }

    /// <summary>Gets the typed contextual draft binding issue, or none for an exact outcome.</summary>
    public StaticFieldV2ContextualBindingIssue Issue { get; }

    /// <summary>Gets the admitted detached V2 syntax draft descriptor that was bound.</summary>
    public StaticFieldV2ExpressionDescriptor Expression { get; }

    /// <summary>Gets the projected scoped-context draft view every level was searched through.</summary>
    public StaticFieldV2ScopedContextOutcome Context { get; }

    /// <summary>Gets a defensive copy of the per-partition draft results, or an empty array for a stop.</summary>
    public ImmutableArray<StaticFieldV2ContextualPartitionResult> PartitionResults =>
        ExpressionV2ContractEncoding.Copy(partitionResults);

    /// <summary>Gets the single converged physical draft candidate, or null for anything but an exact outcome.</summary>
    public StaticFieldV2ContextualCandidateGroup? SelectedCandidate { get; }

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the examined-occurrence total, the propagated prerequisite count, or the cap-plus-one count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two contextual draft binding outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ContextualBindingOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests contextual draft binding outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ContextualBindingOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2ContextualCandidateIdentity IssueCandidate(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataNamedTypeDefinitionChainIdentity chain,
        int levelIndex,
        StaticFieldV2ContextualCandidateSource source,
        StaticFieldV2ScopedImportProjection? contributingImport,
        int namespaceSegmentCount,
        int partitionIndex) =>
        StaticFieldV2ContextualCandidateIdentity.Create(
            RowMintCapability,
            sourceModule,
            chain,
            levelIndex,
            source,
            contributingImport,
            namespaceSegmentCount,
            partitionIndex);

    internal static StaticFieldV2ContextualCandidateGroup IssueGroup(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity finalTypeDefinition,
        ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates) =>
        StaticFieldV2ContextualCandidateGroup.Create(
            RowMintCapability,
            sourceModule,
            finalTypeDefinition,
            candidates);

    internal static StaticFieldV2ContextualLevelResult IssueLevelResult(
        int levelIndex,
        string namespaceName,
        bool hasNamespaceLevel,
        int? scopeLevelIndex,
        ImmutableArray<StaticFieldV2ContextualCandidateIdentity> candidates,
        int examinedOccurrenceCount,
        StaticFieldV2ContextualRejectionReason? firstRejectionReason) =>
        StaticFieldV2ContextualLevelResult.Create(
            RowMintCapability,
            levelIndex,
            namespaceName,
            hasNamespaceLevel,
            scopeLevelIndex,
            candidates.IsEmpty
                ? StaticFieldV2ContextualLevelDisposition.NoCandidateAtLevel
                : StaticFieldV2ContextualLevelDisposition.Selected,
            candidates,
            examinedOccurrenceCount,
            firstRejectionReason);

    internal static StaticFieldV2ContextualPartitionResult IssuePartitionResult(
        int partitionIndex,
        StaticFieldV2CandidateKind candidateKind,
        int ownerSegmentCount,
        ImmutableArray<StaticFieldV2ContextualLevelResult> levels,
        int? selectedLevelIndex,
        ImmutableArray<StaticFieldV2ContextualCandidateGroup> groups) =>
        StaticFieldV2ContextualPartitionResult.Create(
            RowMintCapability,
            partitionIndex,
            candidateKind,
            ownerSegmentCount,
            groups.Length switch
            {
                0 => StaticFieldV2ContextualBindingResultKind.Absent,
                1 => StaticFieldV2ContextualBindingResultKind.Exact,
                _ => StaticFieldV2ContextualBindingResultKind.Ambiguous,
            },
            levels,
            selectedLevelIndex,
            groups);

    internal static StaticFieldV2ContextualBindingOutcome IssueComplete(
        StaticFieldV2ContextualBindingResultKind resultKind,
        StaticFieldV2ContextualBindingIssue issue,
        StaticFieldV2ExpressionDescriptor expression,
        StaticFieldV2ScopedContextOutcome context,
        ImmutableArray<StaticFieldV2ContextualPartitionResult> partitionResults,
        StaticFieldV2ContextualCandidateGroup? selectedCandidate,
        int observedCount) =>
        new(
            resultKind,
            issue,
            expression,
            context,
            partitionResults,
            selectedCandidate,
            null,
            observedCount);

    internal static StaticFieldV2ContextualBindingOutcome IssueStop(
        StaticFieldV2ContextualBindingResultKind resultKind,
        StaticFieldV2ContextualBindingIssue issue,
        StaticFieldV2ExpressionDescriptor expression,
        StaticFieldV2ScopedContextOutcome context,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            resultKind,
            issue,
            expression,
            context,
            [],
            null,
            reachedBound,
            observedCount);
}

/// <summary>Projects one exact active ImportScope chain and binds contextual owner names through it.</summary>
/// <remarks>
/// This draft binder owns the contextual route only: an absent alias qualifier or a named alias qualifier. The exact
/// <c>global::</c> qualifier stops with a typed unsupported disposition because the explicit route is separately
/// owned and must remain lazy - a metadata-global spelling never consults this projection at all.
/// <para>
/// The W7 metadata-global dot-qualified compatibility rule is deliberately not re-implemented here. Route selection
/// between the metadata-global and contextual routes belongs to the later composition slice and is recorded through
/// <see cref="StaticFieldV2ScopedContextCoverageBoundary.MetadataGlobalRouteSelectionDeferred"/>.
/// </para>
/// <para>
/// Retained <c>using static</c> imports are projected with their resolved owners but are consumed by the separately
/// owned bare-member draft slice; this binder never derives a member from them. A TypeSpec alias target is retained
/// physically and never decoded, so it can never become a bound head here.
/// </para>
/// </remarks>
public static class StaticFieldV2ScopedContextBinder
{
    private const byte TypeReferenceTable = 0x01;
    private const byte TypeDefinitionTable = 0x02;
    private const byte TypeSpecificationTable = 0x1B;

    /// <summary>Projects one physical active ImportScope chain into the ordered scoped semantic draft view.</summary>
    /// <param name="request">The complete scoped-context draft projection request.</param>
    /// <remarks>
    /// Every retained import is projected, including malformed, unsupported, and unresolvable ones, so a retained
    /// import can never silently vanish. Namespace levels are derived innermost-first from the selected declaring
    /// type's outermost enclosing namespace text and always end with the global namespace.
    /// </remarks>
    /// <returns>A sealed immutable draft projection that is either exact or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2ScopedContextOutcome ProjectContext(StaticFieldV2ScopedContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseBoundaries = ImmutableArray.Create(
            StaticFieldV2ScopedContextCoverageBoundary.UsingStaticConsumedByLaterSlice,
            StaticFieldV2ScopedContextCoverageBoundary.MetadataGlobalRouteSelectionDeferred,
            StaticFieldV2ScopedContextCoverageBoundary.ImportScopeToNamespaceLevelPairingDeclared);

        var ancestry = request.AncestryPortfolio;
        if (ancestry.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.NonExact)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.NonExact,
                StaticFieldV2ScopedContextIssue.AncestryPortfolioNonExact,
                request,
                baseBoundaries,
                null,
                ancestry.ObservedCount);
        }
        if (ancestry.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.Invalid,
                StaticFieldV2ScopedContextIssue.AncestryPortfolioInvalid,
                request,
                baseBoundaries,
                null,
                ancestry.ObservedCount);
        }

        var resolution = request.ResolutionPortfolio;
        if (resolution.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.NonExact)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.NonExact,
                StaticFieldV2ScopedContextIssue.ResolutionPortfolioNonExact,
                request,
                baseBoundaries,
                resolution.ReachedBound,
                resolution.ObservedCount);
        }
        if (resolution.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.Invalid)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.Invalid,
                StaticFieldV2ScopedContextIssue.ResolutionPortfolioInvalid,
                request,
                baseBoundaries,
                null,
                resolution.ObservedCount);
        }
        if (!ancestry.ResolutionPortfolio.Equals(resolution))
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.Invalid,
                StaticFieldV2ScopedContextIssue.ResolutionPortfolioMismatch,
                request,
                baseBoundaries,
                null,
                0);
        }

        MetadataTypeReferenceResolutionModuleIdentity? selectedEntry = null;
        foreach (var entry in resolution.Entries)
        {
            if (entry.SourceModule.Equals(request.SelectedModule))
            {
                selectedEntry = entry;
                break;
            }
        }
        if (selectedEntry is null)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.Invalid,
                StaticFieldV2ScopedContextIssue.SelectedModuleNotInPortfolio,
                request,
                baseBoundaries,
                null,
                resolution.Entries.Length);
        }

        var selectedCatalog = selectedEntry.ChainEntry.ChainCatalog;
        var selectedChain = selectedCatalog.ExactChainOrDefault(request.SelectedTypeDefinition.TypeDefinitionToken);
        if (selectedChain is null || !selectedChain.FinalTypeDefinition.Equals(request.SelectedTypeDefinition))
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.Invalid,
                StaticFieldV2ScopedContextIssue.SelectedTypeDefinitionNotIssued,
                request,
                baseBoundaries,
                null,
                request.SelectedTypeDefinition.TypeDefinitionToken);
        }

        if (!request.IsImportScopeChainInitialized)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.Invalid,
                StaticFieldV2ScopedContextIssue.ImportScopeChainUninitialized,
                request,
                baseBoundaries,
                null,
                0);
        }

        var scopes = request.ImportScopesCore;
        if (scopes.Length > StaticFieldV2ScopedContextOutcome.MaximumImportScopeDepth)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.NonExact,
                StaticFieldV2ScopedContextIssue.ImportScopeDepthBoundReached,
                request,
                baseBoundaries,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ImportScopeDepthBoundName,
                    StaticFieldV2ScopedContextOutcome.MaximumImportScopeDepth),
                StaticFieldV2ScopedContextOutcome.MaximumImportScopeDepth + 1);
        }

        var totalFactCount = 0;
        foreach (var scope in scopes)
        {
            totalFactCount += scope.Imports.Length;
        }
        if (totalFactCount > StaticFieldV2ScopedContextOutcome.MaximumImportFactCount)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.NonExact,
                StaticFieldV2ScopedContextIssue.ImportFactCountBoundReached,
                request,
                baseBoundaries,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ImportFactCountBoundName,
                    StaticFieldV2ScopedContextOutcome.MaximumImportFactCount),
                StaticFieldV2ScopedContextOutcome.MaximumImportFactCount + 1);
        }

        for (var index = 0; index < scopes.Length; index++)
        {
            var scope = scopes[index];
            var expectedParent = index == 0 ? null : (int?)scopes[index - 1].ImportScopeToken;
            if (scope.NestingDepth != index || scope.ParentImportScopeToken != expectedParent)
            {
                return Stop(
                    StaticFieldV2ScopedContextResultKind.Invalid,
                    StaticFieldV2ScopedContextIssue.ImportScopeChainNotOuterToInner,
                    request,
                    baseBoundaries,
                    null,
                    index);
            }
        }

        var declaringNamespace = selectedChain.Segments[0].RawNamespaceName;
        var namespaceParts = declaringNamespace.Length == 0
            ? []
            : declaringNamespace.Split('.');
        var namespaceLevelCount = namespaceParts.Length + 1;
        if (namespaceLevelCount > StaticFieldV2ScopedContextOutcome.MaximumNamespaceLevelCount)
        {
            return Stop(
                StaticFieldV2ScopedContextResultKind.NonExact,
                StaticFieldV2ScopedContextIssue.NamespaceLevelCountBoundReached,
                request,
                baseBoundaries,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.NamespaceLevelCountBoundName,
                    StaticFieldV2ScopedContextOutcome.MaximumNamespaceLevelCount),
                StaticFieldV2ScopedContextOutcome.MaximumNamespaceLevelCount + 1);
        }

        var namespaceLevels = ImmutableArray.CreateBuilder<StaticFieldV2ScopedNamespaceLevelIdentity>(
            namespaceLevelCount);
        for (var index = 0; index < namespaceLevelCount; index++)
        {
            var take = namespaceParts.Length - index;
            var text = take <= 0 ? string.Empty : string.Join('.', namespaceParts, 0, take);
            namespaceLevels.Add(StaticFieldV2ScopedContextOutcome.IssueNamespaceLevel(index, text));
        }

        var pending = new List<PendingImport>(totalFactCount);
        for (var scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
        {
            foreach (var fact in scopes[scopeIndex].Imports)
            {
                pending.Add(ProjectImport(fact, scopeIndex, selectedEntry, resolution));
            }
        }

        var innermostAliasLevel = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in pending)
        {
            if (item.Fact.Alias is not { } alias)
            {
                continue;
            }
            if (!innermostAliasLevel.TryGetValue(alias, out var level) || item.ScopeLevelIndex > level)
            {
                innermostAliasLevel[alias] = item.ScopeLevelIndex;
            }
        }

        var projections = new List<StaticFieldV2ScopedImportProjection>(pending.Count);
        foreach (var item in pending)
        {
            var shadowed = item.Fact.Alias is { } alias &&
                innermostAliasLevel[alias] > item.ScopeLevelIndex;
            projections.Add(StaticFieldV2ScopedContextOutcome.IssueImport(
                item.Fact,
                item.ScopeLevelIndex,
                item.Kind,
                item.TargetDisposition,
                item.TargetTypeModule,
                item.TargetTypeDefinition,
                item.TargetTypeSpecificationRow,
                item.AssemblyReferenceRow,
                item.AssemblyReferenceTargetModule,
                shadowed));
        }

        var scopeLevels = ImmutableArray.CreateBuilder<StaticFieldV2ScopedScopeLevelIdentity>(scopes.Length);
        var cursor = 0;
        for (var scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
        {
            var count = scopes[scopeIndex].Imports.Length;
            var levelImports = ImmutableArray.CreateBuilder<StaticFieldV2ScopedImportProjection>(count);
            for (var offset = 0; offset < count; offset++)
            {
                levelImports.Add(projections[cursor + offset]);
            }
            cursor += count;
            scopeLevels.Add(StaticFieldV2ScopedContextOutcome.IssueScopeLevel(
                scopeIndex,
                scopes[scopeIndex],
                levelImports.MoveToImmutable()));
        }

        var shadowings = ImmutableArray.CreateBuilder<StaticFieldV2ScopedAliasShadowingIdentity>();
        foreach (var shadowedImport in projections)
        {
            if (!shadowedImport.IsShadowed || shadowedImport.Alias is not { } alias)
            {
                continue;
            }
            foreach (var hidingImport in projections)
            {
                if (hidingImport.ScopeLevelIndex == innermostAliasLevel[alias] &&
                    hidingImport.Alias is { } hidingAlias &&
                    string.Equals(hidingAlias, alias, StringComparison.Ordinal))
                {
                    shadowings.Add(StaticFieldV2ScopedContextOutcome.IssueShadowing(
                        alias,
                        hidingImport,
                        shadowedImport));
                }
            }
        }

        var boundaries = new SortedSet<StaticFieldV2ScopedContextCoverageBoundary>(baseBoundaries);
        var exhaustive = true;
        foreach (var projection in projections)
        {
            if (projection.TargetDisposition ==
                StaticFieldV2ScopedImportTargetDisposition.TypeSpecificationNotDecoded)
            {
                boundaries.Add(StaticFieldV2ScopedContextCoverageBoundary.AliasTypeSpecTargetNotDecoded);
            }
            if (projection.Kind == StaticFieldV2ScopedImportKind.UnsupportedRaw)
            {
                boundaries.Add(StaticFieldV2ScopedContextCoverageBoundary.RetainedUnsupportedImport);
            }
            if (!projection.IsTargetExact)
            {
                exhaustive = false;
                if (projection.TargetDisposition !=
                        StaticFieldV2ScopedImportTargetDisposition.RawImportNotInterpreted &&
                    projection.TargetDisposition !=
                        StaticFieldV2ScopedImportTargetDisposition.TypeSpecificationNotDecoded)
                {
                    boundaries.Add(StaticFieldV2ScopedContextCoverageBoundary.UnresolvedImportTargetRetained);
                }
            }
        }

        return StaticFieldV2ScopedContextOutcome.IssueExact(
            request,
            scopeLevels.MoveToImmutable(),
            namespaceLevels.MoveToImmutable(),
            shadowings.ToImmutable(),
            [.. boundaries],
            exhaustive,
            totalFactCount);
    }

    /// <summary>Binds every qualified-owner partition of one descriptor through the scoped draft context.</summary>
    /// <param name="expression">The admitted detached V2 syntax draft descriptor.</param>
    /// <param name="context">The exact scoped-context draft projection to search.</param>
    /// <remarks>
    /// A named alias qualifier consults only the named alias or extern alias path. An ordinary spelling evaluates
    /// declaration levels innermost to outermost and, at each level, examines aliases declared at that level, then
    /// declarations in that level's namespace, then candidates contributed by namespace imports declared at that
    /// level. The first level that produces a viable non-empty candidate set stops the outward draft search even
    /// when an outer level would also match; imports accumulate only among imports at that same level, equal
    /// physical candidates converge, and distinct surviving candidates are ambiguous rather than ordered.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome that is either one complete answer or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="context"/> is null.</exception>
    public static StaticFieldV2ContextualBindingOutcome BindContextualRoute(
        StaticFieldV2ExpressionDescriptor expression,
        StaticFieldV2ScopedContextOutcome context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);

        if (expression.AliasQualifier.Kind == StaticFieldV2AliasKind.Global)
        {
            return StaticFieldV2ContextualBindingOutcome.IssueStop(
                StaticFieldV2ContextualBindingResultKind.Unsupported,
                StaticFieldV2ContextualBindingIssue.ExplicitRouteNotSupported,
                expression,
                context,
                null,
                0);
        }
        if (context.ResultKind == StaticFieldV2ScopedContextResultKind.NonExact)
        {
            return StaticFieldV2ContextualBindingOutcome.IssueStop(
                StaticFieldV2ContextualBindingResultKind.NonExact,
                StaticFieldV2ContextualBindingIssue.ContextNonExact,
                expression,
                context,
                context.ReachedBound,
                context.ObservedCount);
        }
        if (context.ResultKind == StaticFieldV2ScopedContextResultKind.Invalid)
        {
            return StaticFieldV2ContextualBindingOutcome.IssueStop(
                StaticFieldV2ContextualBindingResultKind.Invalid,
                StaticFieldV2ContextualBindingIssue.ContextInvalid,
                expression,
                context,
                null,
                context.ObservedCount);
        }

        var partitions = expression.Partitions;
        var qualifiedCount = 0;
        foreach (var partition in partitions)
        {
            if (partition.CandidateKind == StaticFieldV2CandidateKind.QualifiedOwner)
            {
                qualifiedCount++;
            }
        }
        if (qualifiedCount == 0)
        {
            return StaticFieldV2ContextualBindingOutcome.IssueStop(
                StaticFieldV2ContextualBindingResultKind.Unsupported,
                StaticFieldV2ContextualBindingIssue.NoQualifiedOwnerPartition,
                expression,
                context,
                null,
                0);
        }

        var named = expression.AliasQualifier.Kind == StaticFieldV2AliasKind.Named;
        var aliasProjections = named
            ? context.ActiveAliasProjections(expression.AliasQualifier.Alias!.DecodedText)
            : [];
        var state = new BindingState(context);
        var results = ImmutableArray.CreateBuilder<StaticFieldV2ContextualPartitionResult>(qualifiedCount);

        for (var partitionIndex = 0; partitionIndex < partitions.Length; partitionIndex++)
        {
            var partition = partitions[partitionIndex];
            if (partition.CandidateKind != StaticFieldV2CandidateKind.QualifiedOwner)
            {
                continue;
            }

            var partitionResult = BindPartition(
                expression,
                context,
                state,
                partitionIndex,
                partition,
                named,
                aliasProjections);
            if (partitionResult is null)
            {
                return StaticFieldV2ContextualBindingOutcome.IssueStop(
                    StaticFieldV2ContextualBindingResultKind.NonExact,
                    state.BoundIssue,
                    expression,
                    context,
                    state.ReachedBound,
                    state.ObservedCount);
            }
            results.Add(partitionResult);
        }

        return Select(expression, context, results.ToImmutable(), state.ExaminedTotal, named, aliasProjections);
    }

    private static StaticFieldV2ContextualBindingOutcome Select(
        StaticFieldV2ExpressionDescriptor expression,
        StaticFieldV2ScopedContextOutcome context,
        ImmutableArray<StaticFieldV2ContextualPartitionResult> results,
        int examinedTotal,
        bool named,
        ImmutableArray<StaticFieldV2ScopedImportProjection> aliasProjections)
    {
        foreach (var result in results)
        {
            if (result.ResultKind == StaticFieldV2ContextualBindingResultKind.Ambiguous)
            {
                return StaticFieldV2ContextualBindingOutcome.IssueComplete(
                    StaticFieldV2ContextualBindingResultKind.Ambiguous,
                    StaticFieldV2ContextualBindingIssue.AmbiguousWithinLevel,
                    expression,
                    context,
                    results,
                    null,
                    examinedTotal);
            }
        }

        StaticFieldV2ContextualCandidateGroup? selected = null;
        var distinctKeys = new HashSet<(string ModuleSha256, int TypeDefinitionToken)>();
        foreach (var result in results)
        {
            if (result.ResultKind != StaticFieldV2ContextualBindingResultKind.Exact)
            {
                continue;
            }
            var group = result.Groups[0];
            distinctKeys.Add((group.SourceModule.Sha256, group.FinalTypeDefinitionToken));
            selected ??= group;
        }

        if (distinctKeys.Count > 1)
        {
            return StaticFieldV2ContextualBindingOutcome.IssueComplete(
                StaticFieldV2ContextualBindingResultKind.Ambiguous,
                StaticFieldV2ContextualBindingIssue.AmbiguousAcrossPartitions,
                expression,
                context,
                results,
                null,
                examinedTotal);
        }
        if (distinctKeys.Count == 1)
        {
            return StaticFieldV2ContextualBindingOutcome.IssueComplete(
                StaticFieldV2ContextualBindingResultKind.Exact,
                StaticFieldV2ContextualBindingIssue.None,
                expression,
                context,
                results,
                selected,
                examinedTotal);
        }

        if (!context.IsExhaustive)
        {
            var retained = 0;
            foreach (var level in context.ScopeLevelsCore)
            {
                foreach (var import in level.ImportsCore)
                {
                    if (!import.IsTargetExact)
                    {
                        retained++;
                    }
                }
            }
            return StaticFieldV2ContextualBindingOutcome.IssueStop(
                StaticFieldV2ContextualBindingResultKind.NonExact,
                StaticFieldV2ContextualBindingIssue.AbsenceNotClaimableOverRetainedEvidence,
                expression,
                context,
                null,
                retained);
        }

        var issue = named && aliasProjections.IsEmpty
            ? StaticFieldV2ContextualBindingIssue.NamedAliasAbsent
            : StaticFieldV2ContextualBindingIssue.CandidateAbsent;
        return StaticFieldV2ContextualBindingOutcome.IssueComplete(
            StaticFieldV2ContextualBindingResultKind.Absent,
            issue,
            expression,
            context,
            results,
            null,
            examinedTotal);
    }

    private static StaticFieldV2ContextualPartitionResult? BindPartition(
        StaticFieldV2ExpressionDescriptor expression,
        StaticFieldV2ScopedContextOutcome context,
        BindingState state,
        int partitionIndex,
        StaticFieldV2StructuralPartition partition,
        bool named,
        ImmutableArray<StaticFieldV2ScopedImportProjection> aliasProjections)
    {
        var segments = expression.Segments;
        var ownerSegmentCount = partition.FieldSegmentIndex;
        var levels = ImmutableArray.CreateBuilder<StaticFieldV2ContextualLevelResult>();
        var winning = new List<StaticFieldV2ContextualCandidateIdentity>();
        int? selectedLevelIndex = null;

        if (named)
        {
            var levelIndex = aliasProjections.IsEmpty ? 0 : InnerLevelOf(context, aliasProjections[0]);
            var collector = new LevelCollector(levelIndex, partitionIndex);
            foreach (var aliasProjection in aliasProjections)
            {
                CollectThroughAlias(
                    state,
                    collector,
                    aliasProjection,
                    segments,
                    0,
                    ownerSegmentCount,
                    allowExternAlias: true);
                if (state.Stopped)
                {
                    return null;
                }
            }
            levels.Add(StaticFieldV2ContextualBindingOutcome.IssueLevelResult(
                levelIndex,
                string.Empty,
                false,
                aliasProjections.IsEmpty ? null : aliasProjections[0].ScopeLevelIndex,
                [.. collector.Candidates],
                collector.Examined,
                collector.FirstRejectionReason));
            if (collector.Candidates.Count > 0)
            {
                selectedLevelIndex = levelIndex;
                winning.AddRange(collector.Candidates);
            }
        }
        else
        {
            var namespaceLevels = context.NamespaceLevelsCore;
            var scopeLevels = context.ScopeLevelsCore;
            var levelCount = Math.Max(namespaceLevels.Length, scopeLevels.Length);
            for (var levelIndex = 0; levelIndex < levelCount; levelIndex++)
            {
                var hasNamespaceLevel = levelIndex < namespaceLevels.Length;
                var namespaceName = hasNamespaceLevel
                    ? namespaceLevels[levelIndex].NamespaceName
                    : string.Empty;
                int? scopeLevelIndex = levelIndex < scopeLevels.Length
                    ? scopeLevels.Length - 1 - levelIndex
                    : null;
                var collector = new LevelCollector(levelIndex, partitionIndex);

                if (scopeLevelIndex is { } scopeIndex)
                {
                    foreach (var import in scopeLevels[scopeIndex].ImportsCore)
                    {
                        if (import.IsShadowed ||
                            import.Alias is not { } alias ||
                            !string.Equals(alias, segments[0].Identifier.DecodedText, StringComparison.Ordinal) ||
                            segments[0].Arity != 0 ||
                            import.Kind == StaticFieldV2ScopedImportKind.ExternAlias)
                        {
                            continue;
                        }
                        CollectThroughAlias(
                            state,
                            collector,
                            import,
                            segments,
                            1,
                            ownerSegmentCount - 1,
                            allowExternAlias: false);
                        if (state.Stopped)
                        {
                            return null;
                        }
                    }
                }

                if (collector.Candidates.Count == 0 && hasNamespaceLevel)
                {
                    CollectPrefixed(
                        state,
                        collector,
                        namespaceName,
                        null,
                        StaticFieldV2ContextualCandidateSource.NamespaceLevelDeclaration,
                        null,
                        segments,
                        0,
                        ownerSegmentCount);
                    if (state.Stopped)
                    {
                        return null;
                    }
                }

                if (collector.Candidates.Count == 0 && scopeLevelIndex is { } importScopeIndex)
                {
                    foreach (var import in scopeLevels[importScopeIndex].ImportsCore)
                    {
                        if (import.Kind != StaticFieldV2ScopedImportKind.NamespaceImport ||
                            import.Target is not { } importedNamespace)
                        {
                            continue;
                        }
                        if (import.Fact.AssemblyReferenceToken is not null &&
                            import.TargetDisposition !=
                                StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceResolved)
                        {
                            continue;
                        }
                        CollectPrefixed(
                            state,
                            collector,
                            importedNamespace,
                            import.AssemblyReferenceTargetModule,
                            StaticFieldV2ContextualCandidateSource.NamespaceImport,
                            import,
                            segments,
                            0,
                            ownerSegmentCount);
                        if (state.Stopped)
                        {
                            return null;
                        }
                    }
                }

                levels.Add(StaticFieldV2ContextualBindingOutcome.IssueLevelResult(
                    levelIndex,
                    namespaceName,
                    hasNamespaceLevel,
                    scopeLevelIndex,
                    [.. collector.Candidates],
                    collector.Examined,
                    collector.FirstRejectionReason));
                if (collector.Candidates.Count > 0)
                {
                    selectedLevelIndex = levelIndex;
                    winning.AddRange(collector.Candidates);
                    break;
                }
            }
        }

        var groups = GroupCandidates(winning);
        if (groups.Length > StaticFieldV2ContextualBindingOutcome.MaximumGroupedPhysicalCandidateCount)
        {
            state.StopWithGroupBound();
            return null;
        }

        return StaticFieldV2ContextualBindingOutcome.IssuePartitionResult(
            partitionIndex,
            partition.CandidateKind,
            ownerSegmentCount,
            levels.ToImmutable(),
            groups.IsEmpty ? null : selectedLevelIndex,
            groups);
    }

    private static int InnerLevelOf(
        StaticFieldV2ScopedContextOutcome context,
        StaticFieldV2ScopedImportProjection projection)
    {
        var scopeCount = context.ScopeLevelsCore.Length;
        return scopeCount == 0 ? 0 : scopeCount - 1 - projection.ScopeLevelIndex;
    }

    private static void CollectThroughAlias(
        BindingState state,
        LevelCollector collector,
        StaticFieldV2ScopedImportProjection alias,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int start,
        int count,
        bool allowExternAlias)
    {
        switch (alias.Kind)
        {
            case StaticFieldV2ScopedImportKind.TypeAlias:
                if (alias.TargetTypeDefinition is null || alias.TargetTypeModule is null)
                {
                    collector.Reject(StaticFieldV2ContextualRejectionReason.AliasTargetNotBindable);
                    return;
                }
                CollectHeadExtension(
                    state,
                    collector,
                    alias,
                    segments,
                    start,
                    count);
                return;
            case StaticFieldV2ScopedImportKind.NamespaceAlias:
                if (alias.Target is not { } aliasedNamespace)
                {
                    collector.Reject(StaticFieldV2ContextualRejectionReason.AliasTargetNotBindable);
                    return;
                }
                if (alias.Fact.AssemblyReferenceToken is not null &&
                    alias.TargetDisposition !=
                        StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceResolved)
                {
                    collector.Reject(StaticFieldV2ContextualRejectionReason.AliasTargetNotBindable);
                    return;
                }
                CollectPrefixed(
                    state,
                    collector,
                    aliasedNamespace,
                    alias.AssemblyReferenceTargetModule,
                    StaticFieldV2ContextualCandidateSource.NamespaceAlias,
                    alias,
                    segments,
                    start,
                    count);
                return;
            case StaticFieldV2ScopedImportKind.ExternAlias:
                if (!allowExternAlias ||
                    alias.TargetDisposition !=
                        StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceResolved)
                {
                    collector.Reject(StaticFieldV2ContextualRejectionReason.AliasTargetNotBindable);
                    return;
                }
                CollectPrefixed(
                    state,
                    collector,
                    string.Empty,
                    alias.AssemblyReferenceTargetModule,
                    StaticFieldV2ContextualCandidateSource.ExternAlias,
                    alias,
                    segments,
                    start,
                    count);
                return;
            default:
                collector.Reject(StaticFieldV2ContextualRejectionReason.AliasTargetNotBindable);
                return;
        }
    }

    private static void CollectHeadExtension(
        BindingState state,
        LevelCollector collector,
        StaticFieldV2ScopedImportProjection alias,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int start,
        int count)
    {
        var module = alias.TargetTypeModule!;
        var view = state.ViewFor(module);
        if (view is null)
        {
            collector.Reject(StaticFieldV2ContextualRejectionReason.AliasTargetNotBindable);
            return;
        }

        var headChain = view.Catalog.ExactChainOrDefault(alias.TargetTypeDefinition!.TypeDefinitionToken);
        if (headChain is null || headChain.IsModulePseudoType)
        {
            collector.Reject(StaticFieldV2ContextualRejectionReason.AliasTargetNotBindable);
            return;
        }

        if (count == 0)
        {
            if (!headChain.CanAppearInCSharpNamedType)
            {
                collector.Reject(StaticFieldV2ContextualRejectionReason.NotCSharpAddressable);
                return;
            }
            collector.Accept(StaticFieldV2ContextualBindingOutcome.IssueCandidate(
                module,
                headChain,
                collector.LevelIndex,
                StaticFieldV2ContextualCandidateSource.TypeAlias,
                alias,
                0,
                collector.PartitionIndex));
            return;
        }

        var headSegments = headChain.Segments;
        foreach (var chain in view.Chains)
        {
            if (!state.CountExamined(collector))
            {
                return;
            }

            var chainSegments = chain.Segments;
            if (chainSegments.Length != headSegments.Length + count)
            {
                collector.Reject(StaticFieldV2ContextualRejectionReason.SegmentCountMismatch);
                continue;
            }
            if (chain.IsModulePseudoType)
            {
                collector.Reject(StaticFieldV2ContextualRejectionReason.ModulePseudoType);
                continue;
            }

            var prefixMatches = true;
            for (var index = 0; index < headSegments.Length; index++)
            {
                if (chainSegments[index].TypeDefinitionToken != headSegments[index].TypeDefinitionToken)
                {
                    prefixMatches = false;
                    break;
                }
            }
            if (!prefixMatches)
            {
                collector.Reject(StaticFieldV2ContextualRejectionReason.EnclosingHeadMismatch);
                continue;
            }

            var reason = ExamineSegments(chainSegments, headSegments.Length, segments, start, count);
            if (reason is { } value)
            {
                collector.Reject(value);
                continue;
            }
            collector.Accept(StaticFieldV2ContextualBindingOutcome.IssueCandidate(
                module,
                chain,
                collector.LevelIndex,
                StaticFieldV2ContextualCandidateSource.TypeAlias,
                alias,
                0,
                collector.PartitionIndex));
        }
    }

    private static void CollectPrefixed(
        BindingState state,
        LevelCollector collector,
        string namespacePrefix,
        StaticFieldMetadataModuleIdentity? restrictedModule,
        StaticFieldV2ContextualCandidateSource source,
        StaticFieldV2ScopedImportProjection? contributingImport,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int start,
        int count)
    {
        if (count <= 0)
        {
            return;
        }

        for (var namespaceSegmentCount = 0; namespaceSegmentCount < count; namespaceSegmentCount++)
        {
            if (namespaceSegmentCount > 0 && segments[start + namespaceSegmentCount - 1].Arity != 0)
            {
                break;
            }

            var namespaceText = CombineNamespace(namespacePrefix, segments, start, namespaceSegmentCount);
            var expectedChainLength = count - namespaceSegmentCount;
            foreach (var view in state.Views)
            {
                if (restrictedModule is not null && !view.Module.Equals(restrictedModule))
                {
                    continue;
                }
                foreach (var chain in view.Chains)
                {
                    if (!state.CountExamined(collector))
                    {
                        return;
                    }

                    var chainSegments = chain.Segments;
                    if (chainSegments.Length != expectedChainLength)
                    {
                        collector.Reject(StaticFieldV2ContextualRejectionReason.SegmentCountMismatch);
                        continue;
                    }
                    if (chain.IsModulePseudoType)
                    {
                        collector.Reject(StaticFieldV2ContextualRejectionReason.ModulePseudoType);
                        continue;
                    }
                    if (!string.Equals(chainSegments[0].RawNamespaceName, namespaceText, StringComparison.Ordinal))
                    {
                        collector.Reject(StaticFieldV2ContextualRejectionReason.NamespaceMismatch);
                        continue;
                    }

                    var reason = ExamineSegments(
                        chainSegments,
                        0,
                        segments,
                        start + namespaceSegmentCount,
                        expectedChainLength);
                    if (reason is { } value)
                    {
                        collector.Reject(value);
                        continue;
                    }
                    collector.Accept(StaticFieldV2ContextualBindingOutcome.IssueCandidate(
                        view.Module,
                        chain,
                        collector.LevelIndex,
                        source,
                        contributingImport,
                        namespaceSegmentCount,
                        collector.PartitionIndex));
                }
            }
        }
    }

    private static StaticFieldV2ContextualRejectionReason? ExamineSegments(
        ImmutableArray<MetadataNamedTypeDefinitionChainSegmentIdentity> chainSegments,
        int chainStart,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int segmentStart,
        int count)
    {
        for (var index = 0; index < count; index++)
        {
            var chainSegment = chainSegments[chainStart + index];
            var syntaxSegment = segments[segmentStart + index];
            if (chainSegment.RoslynProjection is not { } projection ||
                chainSegment.IntroducedGenericArity is not { } introducedArity)
            {
                return StaticFieldV2ContextualRejectionReason.NonExactCompilerMapping;
            }
            if (!string.Equals(
                    projection.ProjectedSimpleName,
                    syntaxSegment.Identifier.DecodedText,
                    StringComparison.Ordinal))
            {
                return StaticFieldV2ContextualRejectionReason.SimpleNameMismatch;
            }
            if (introducedArity != syntaxSegment.Arity)
            {
                return StaticFieldV2ContextualRejectionReason.ArityMismatch;
            }
            if (!chainSegment.CanAppearInCSharpNamedType)
            {
                return StaticFieldV2ContextualRejectionReason.NotCSharpAddressable;
            }
        }
        return null;
    }

    private static string CombineNamespace(
        string prefix,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int start,
        int count)
    {
        if (count == 0)
        {
            return prefix;
        }

        var parts = new string[count];
        for (var index = 0; index < count; index++)
        {
            parts[index] = segments[start + index].Identifier.DecodedText;
        }
        var joined = string.Join('.', parts);
        return prefix.Length == 0 ? joined : $"{prefix}.{joined}";
    }

    private static ImmutableArray<StaticFieldV2ContextualCandidateGroup> GroupCandidates(
        List<StaticFieldV2ContextualCandidateIdentity> accepted)
    {
        if (accepted.Count == 0)
        {
            return [];
        }

        var order = new List<(string ModuleSha256, int TypeDefinitionToken)>();
        var buckets = new Dictionary<
            (string ModuleSha256, int TypeDefinitionToken),
            List<StaticFieldV2ContextualCandidateIdentity>>();
        foreach (var candidate in accepted)
        {
            var key = (candidate.SourceModule.Sha256, candidate.FinalTypeDefinitionToken);
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = [];
                buckets.Add(key, bucket);
                order.Add(key);
            }
            bucket.Add(candidate);
        }

        var groups = ImmutableArray.CreateBuilder<StaticFieldV2ContextualCandidateGroup>(order.Count);
        foreach (var key in order)
        {
            var bucket = buckets[key];
            groups.Add(StaticFieldV2ContextualBindingOutcome.IssueGroup(
                bucket[0].SourceModule,
                bucket[0].FinalTypeDefinition,
                [.. bucket]));
        }
        return groups.MoveToImmutable();
    }

    private static PendingImport ProjectImport(
        DumpPortablePdbImportFact fact,
        int scopeLevelIndex,
        MetadataTypeReferenceResolutionModuleIdentity selectedEntry,
        MetadataTypeReferenceResolutionPortfolioIdentity resolution)
    {
        var kind = fact.Kind switch
        {
            DumpPortablePdbImportKind.Namespace => StaticFieldV2ScopedImportKind.NamespaceImport,
            DumpPortablePdbImportKind.TypeAlias => StaticFieldV2ScopedImportKind.TypeAlias,
            DumpPortablePdbImportKind.NamespaceAlias => StaticFieldV2ScopedImportKind.NamespaceAlias,
            DumpPortablePdbImportKind.UsingStatic => StaticFieldV2ScopedImportKind.UsingStatic,
            DumpPortablePdbImportKind.ExternAlias => StaticFieldV2ScopedImportKind.ExternAlias,
            _ => StaticFieldV2ScopedImportKind.UnsupportedRaw,
        };

        if (kind == StaticFieldV2ScopedImportKind.UnsupportedRaw)
        {
            return new PendingImport(
                fact,
                scopeLevelIndex,
                kind,
                StaticFieldV2ScopedImportTargetDisposition.RawImportNotInterpreted,
                null,
                null,
                null,
                null,
                null);
        }

        if (fact.TargetTypeToken is { } typeToken)
        {
            return ProjectTypeTarget(fact, scopeLevelIndex, kind, typeToken, selectedEntry);
        }

        if (fact.AssemblyReferenceToken is { } assemblyReferenceToken)
        {
            return ProjectAssemblyTarget(
                fact,
                scopeLevelIndex,
                kind,
                assemblyReferenceToken,
                selectedEntry,
                resolution);
        }

        var disposition = kind == StaticFieldV2ScopedImportKind.ExternAlias
            ? StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceRowAbsent
            : StaticFieldV2ScopedImportTargetDisposition.NotApplicable;
        return new PendingImport(fact, scopeLevelIndex, kind, disposition, null, null, null, null, null);
    }

    private static PendingImport ProjectTypeTarget(
        DumpPortablePdbImportFact fact,
        int scopeLevelIndex,
        StaticFieldV2ScopedImportKind kind,
        int typeToken,
        MetadataTypeReferenceResolutionModuleIdentity selectedEntry)
    {
        if (CanonicalReplayEncoding.IsMetadataTokenForTable(typeToken, TypeDefinitionTable))
        {
            var chain = selectedEntry.ChainEntry.ChainCatalog.ExactChainOrDefault(typeToken);
            return chain is null || chain.IsModulePseudoType
                ? new PendingImport(
                    fact,
                    scopeLevelIndex,
                    kind,
                    StaticFieldV2ScopedImportTargetDisposition.TypeDefinitionNotIssued,
                    null,
                    null,
                    null,
                    null,
                    null)
                : new PendingImport(
                    fact,
                    scopeLevelIndex,
                    kind,
                    StaticFieldV2ScopedImportTargetDisposition.TypeDefinitionResolved,
                    selectedEntry.SourceModule,
                    chain.FinalTypeDefinition,
                    null,
                    null,
                    null);
        }

        if (CanonicalReplayEncoding.IsMetadataTokenForTable(typeToken, TypeSpecificationTable))
        {
            return new PendingImport(
                fact,
                scopeLevelIndex,
                kind,
                StaticFieldV2ScopedImportTargetDisposition.TypeSpecificationNotDecoded,
                null,
                null,
                selectedEntry.ReferenceTables.TypeSpecifications.FindRow(typeToken),
                null,
                null);
        }

        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(typeToken, TypeReferenceTable))
        {
            return new PendingImport(
                fact,
                scopeLevelIndex,
                kind,
                StaticFieldV2ScopedImportTargetDisposition.TypeReferenceUnresolved,
                null,
                null,
                null,
                null,
                null);
        }

        var resolutionRow = selectedEntry.FindResolution(typeToken);
        return resolutionRow is { Disposition: MetadataTypeReferenceResolutionDispositionKind.Resolved } resolved
            ? new PendingImport(
                fact,
                scopeLevelIndex,
                kind,
                StaticFieldV2ScopedImportTargetDisposition.TypeReferenceResolved,
                resolved.TargetModule,
                resolved.TargetTypeDefinition,
                null,
                null,
                null)
            : new PendingImport(
                fact,
                scopeLevelIndex,
                kind,
                StaticFieldV2ScopedImportTargetDisposition.TypeReferenceUnresolved,
                null,
                null,
                null,
                null,
                null);
    }

    private static PendingImport ProjectAssemblyTarget(
        DumpPortablePdbImportFact fact,
        int scopeLevelIndex,
        StaticFieldV2ScopedImportKind kind,
        int assemblyReferenceToken,
        MetadataTypeReferenceResolutionModuleIdentity selectedEntry,
        MetadataTypeReferenceResolutionPortfolioIdentity resolution)
    {
        var row = selectedEntry.ReferenceTables.AssemblyReferences.FindRow(assemblyReferenceToken);
        if (row is null)
        {
            return new PendingImport(
                fact,
                scopeLevelIndex,
                kind,
                StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceRowAbsent,
                null,
                null,
                null,
                null,
                null);
        }

        var observation = row.Observation;
        var reference = StaticFieldAssemblyReferenceIdentity.Create(
            observation.AssemblyReferenceToken,
            observation.Name,
            observation.MajorVersion,
            observation.MinorVersion,
            observation.BuildNumber,
            observation.RevisionNumber,
            observation.Culture,
            observation.Flags,
            observation.PublicKeyOrToken,
            observation.HashValue);

        StaticFieldMetadataModuleIdentity? match = null;
        var matchCount = 0;
        foreach (var entry in resolution.Entries)
        {
            if (StaticFieldExportedTypeForwarderIdentity.AssemblyReferenceMatchesDefinition(
                    reference,
                    entry.SourceModule.ContainingAssembly.AssemblyDefinition))
            {
                match = entry.SourceModule;
                matchCount++;
            }
        }

        var disposition = matchCount switch
        {
            0 => StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceTargetModuleAbsent,
            1 => StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceResolved,
            _ => StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceTargetModuleAmbiguous,
        };
        return new PendingImport(
            fact,
            scopeLevelIndex,
            kind,
            disposition,
            null,
            null,
            null,
            row,
            matchCount == 1 ? match : null);
    }

    private static StaticFieldV2ScopedContextOutcome Stop(
        StaticFieldV2ScopedContextResultKind resultKind,
        StaticFieldV2ScopedContextIssue issue,
        StaticFieldV2ScopedContextRequest request,
        ImmutableArray<StaticFieldV2ScopedContextCoverageBoundary> boundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        StaticFieldV2ScopedContextOutcome.IssueStop(
            resultKind,
            issue,
            request,
            boundaries,
            reachedBound,
            observedCount);

    private static int ProgressRank(StaticFieldV2ContextualRejectionReason reason) => reason switch
    {
        StaticFieldV2ContextualRejectionReason.SegmentCountMismatch => 1,
        StaticFieldV2ContextualRejectionReason.ModulePseudoType => 2,
        StaticFieldV2ContextualRejectionReason.NamespaceMismatch => 3,
        StaticFieldV2ContextualRejectionReason.EnclosingHeadMismatch => 4,
        StaticFieldV2ContextualRejectionReason.NonExactCompilerMapping => 5,
        StaticFieldV2ContextualRejectionReason.SimpleNameMismatch => 6,
        StaticFieldV2ContextualRejectionReason.ArityMismatch => 7,
        StaticFieldV2ContextualRejectionReason.NotCSharpAddressable => 8,
        _ => 9,
    };

    private sealed class LevelCollector(int levelIndex, int partitionIndex)
    {
        internal int LevelIndex { get; } = levelIndex;

        internal int PartitionIndex { get; } = partitionIndex;

        internal List<StaticFieldV2ContextualCandidateIdentity> Candidates { get; } = [];

        internal int Examined { get; private set; }

        internal StaticFieldV2ContextualRejectionReason? FirstRejectionReason { get; private set; }

        private int retainedRank;

        internal void CountExamined() => Examined++;

        internal void Accept(StaticFieldV2ContextualCandidateIdentity candidate) => Candidates.Add(candidate);

        internal void Reject(StaticFieldV2ContextualRejectionReason reason)
        {
            var rank = ProgressRank(reason);
            if (rank > retainedRank)
            {
                retainedRank = rank;
                FirstRejectionReason = reason;
            }
        }
    }

    private sealed class BindingState
    {
        internal BindingState(StaticFieldV2ScopedContextOutcome context)
        {
            var entries = context.Request.ResolutionPortfolio.Entries;
            var views = ImmutableArray.CreateBuilder<ModuleChainView>(entries.Length);
            foreach (var entry in entries)
            {
                var catalog = entry.ChainEntry.ChainCatalog;
                views.Add(new ModuleChainView(entry.SourceModule, catalog, catalog.Chains));
            }
            Views = views.MoveToImmutable();
        }

        internal ImmutableArray<ModuleChainView> Views { get; }

        internal int ExaminedTotal { get; private set; }

        internal bool Stopped { get; private set; }

        internal StaticFieldV2ContextualBindingIssue BoundIssue { get; private set; }

        internal EvaluationDeterministicBound? ReachedBound { get; private set; }

        internal int ObservedCount { get; private set; }

        internal ModuleChainView? ViewFor(StaticFieldMetadataModuleIdentity module)
        {
            foreach (var view in Views)
            {
                if (view.Module.Equals(module))
                {
                    return view;
                }
            }
            return null;
        }

        internal bool CountExamined(LevelCollector collector)
        {
            ExaminedTotal++;
            collector.CountExamined();
            if (ExaminedTotal <= StaticFieldV2ContextualBindingOutcome.MaximumCandidateOccurrenceCount)
            {
                return true;
            }

            Stopped = true;
            BoundIssue = StaticFieldV2ContextualBindingIssue.CandidateOccurrenceBoundReached;
            ReachedBound = new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.NameCandidateOccurrenceCountBoundName,
                StaticFieldV2ContextualBindingOutcome.MaximumCandidateOccurrenceCount);
            ObservedCount = StaticFieldV2ContextualBindingOutcome.MaximumCandidateOccurrenceCount + 1;
            return false;
        }

        internal void StopWithGroupBound()
        {
            Stopped = true;
            BoundIssue = StaticFieldV2ContextualBindingIssue.CandidateGroupBoundReached;
            ReachedBound = new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.GroupedPhysicalCandidateCountBoundName,
                StaticFieldV2ContextualBindingOutcome.MaximumGroupedPhysicalCandidateCount);
            ObservedCount = StaticFieldV2ContextualBindingOutcome.MaximumGroupedPhysicalCandidateCount + 1;
        }
    }

    private sealed record ModuleChainView(
        StaticFieldMetadataModuleIdentity Module,
        MetadataNamedTypeDefinitionChainCatalogIdentity Catalog,
        ImmutableArray<MetadataNamedTypeDefinitionChainIdentity> Chains);

    private sealed record PendingImport(
        DumpPortablePdbImportFact Fact,
        int ScopeLevelIndex,
        StaticFieldV2ScopedImportKind Kind,
        StaticFieldV2ScopedImportTargetDisposition TargetDisposition,
        StaticFieldMetadataModuleIdentity? TargetTypeModule,
        MetadataTypeDefinitionAuthorityIdentity? TargetTypeDefinition,
        MetadataTypeSpecificationPhysicalRowIdentity? TargetTypeSpecificationRow,
        MetadataAssemblyReferencePhysicalRowIdentity? AssemblyReferenceRow,
        StaticFieldMetadataModuleIdentity? AssemblyReferenceTargetModule);
}
