using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Bundles the six complete physical draft reference tables for one metadata module.</summary>
/// <remarks>
/// The set retains guarded catalogs only. It proves that every member catalog names the same exact draft reference
/// source ends; member exactness stays a portfolio-level disposition rather than a bundling precondition.
/// </remarks>
public sealed class MetadataModuleReferenceTableSetIdentity :
    IEquatable<MetadataModuleReferenceTableSetIdentity>
{
    private const string CanonicalDomain = "metadata-v2-module-reference-table-set";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataModuleReferenceTableSetIdentity(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataTypeReferencePhysicalTableCatalogIdentity typeReferences,
        MetadataModuleReferencePhysicalTableCatalogIdentity moduleReferences,
        MetadataTypeSpecificationPhysicalTableCatalogIdentity typeSpecifications,
        MetadataAssemblyReferencePhysicalTableCatalogIdentity assemblyReferences,
        MetadataAssemblyFilePhysicalTableCatalogIdentity files,
        MetadataExportedTypePhysicalTableCatalogIdentity exportedTypes)
    {
        SourceEnds = sourceEnds;
        TypeReferences = typeReferences;
        ModuleReferences = moduleReferences;
        TypeSpecifications = typeSpecifications;
        AssemblyReferences = assemblyReferences;
        Files = files;
        ExportedTypes = exportedTypes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteSha256(typeReferences.Sha256, nameof(typeReferences));
        writer.WriteSha256(moduleReferences.Sha256, nameof(moduleReferences));
        writer.WriteSha256(typeSpecifications.Sha256, nameof(typeSpecifications));
        writer.WriteSha256(assemblyReferences.Sha256, nameof(assemblyReferences));
        writer.WriteSha256(files.Sha256, nameof(files));
        writer.WriteSha256(exportedTypes.Sha256, nameof(exportedTypes));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact draft reference source ends shared by every member catalog.</summary>
    public MetadataReferenceSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the exact metadata module shared by every member draft catalog.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => SourceEnds.SourceModule;

    /// <summary>Gets the complete physical TypeRef draft table.</summary>
    public MetadataTypeReferencePhysicalTableCatalogIdentity TypeReferences { get; }

    /// <summary>Gets the complete physical ModuleRef draft table.</summary>
    public MetadataModuleReferencePhysicalTableCatalogIdentity ModuleReferences { get; }

    /// <summary>Gets the complete physical TypeSpec draft table.</summary>
    public MetadataTypeSpecificationPhysicalTableCatalogIdentity TypeSpecifications { get; }

    /// <summary>Gets the complete physical AssemblyRef draft table.</summary>
    public MetadataAssemblyReferencePhysicalTableCatalogIdentity AssemblyReferences { get; }

    /// <summary>Gets the complete physical File draft table.</summary>
    public MetadataAssemblyFilePhysicalTableCatalogIdentity Files { get; }

    /// <summary>Gets the complete physical ExportedType draft table.</summary>
    public MetadataExportedTypePhysicalTableCatalogIdentity ExportedTypes { get; }

    /// <summary>Gets whether all six member draft catalogs are exact.</summary>
    public bool AllTablesExact =>
        TypeReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Exact &&
        ModuleReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Exact &&
        TypeSpecifications.ResultKind == MetadataReferencePhysicalTableResultKind.Exact &&
        AssemblyReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Exact &&
        Files.ResultKind == MetadataReferencePhysicalTableResultKind.Exact &&
        ExportedTypes.ResultKind == MetadataReferencePhysicalTableResultKind.Exact;

    /// <summary>Gets whether any member draft catalog retained contradictory evidence.</summary>
    public bool AnyTableInvalid =>
        TypeReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid ||
        ModuleReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid ||
        TypeSpecifications.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid ||
        AssemblyReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid ||
        Files.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid ||
        ExportedTypes.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft set bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft set bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete same-source draft reference-table set.</summary>
    /// <param name="sourceEnds">The exact draft reference source ends every member catalog must name.</param>
    /// <param name="typeReferences">The complete physical TypeRef table for the same source ends.</param>
    /// <param name="moduleReferences">The complete physical ModuleRef table for the same source ends.</param>
    /// <param name="typeSpecifications">The complete physical TypeSpec table for the same source ends.</param>
    /// <param name="assemblyReferences">The complete physical AssemblyRef table for the same source ends.</param>
    /// <param name="files">The complete physical File table for the same source ends.</param>
    /// <param name="exportedTypes">The complete physical ExportedType table for the same source ends.</param>
    /// <returns>A fixed-reference draft set whose members all name one exact source.</returns>
    public static MetadataModuleReferenceTableSetIdentity Create(
        MetadataReferenceSourceEndIdentity sourceEnds,
        MetadataTypeReferencePhysicalTableCatalogIdentity typeReferences,
        MetadataModuleReferencePhysicalTableCatalogIdentity moduleReferences,
        MetadataTypeSpecificationPhysicalTableCatalogIdentity typeSpecifications,
        MetadataAssemblyReferencePhysicalTableCatalogIdentity assemblyReferences,
        MetadataAssemblyFilePhysicalTableCatalogIdentity files,
        MetadataExportedTypePhysicalTableCatalogIdentity exportedTypes)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(typeReferences);
        ArgumentNullException.ThrowIfNull(moduleReferences);
        ArgumentNullException.ThrowIfNull(typeSpecifications);
        ArgumentNullException.ThrowIfNull(assemblyReferences);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(exportedTypes);
        if (!typeReferences.SourceEnds.Equals(sourceEnds) ||
            !moduleReferences.SourceEnds.Equals(sourceEnds) ||
            !typeSpecifications.SourceEnds.Equals(sourceEnds) ||
            !assemblyReferences.SourceEnds.Equals(sourceEnds) ||
            !files.SourceEnds.Equals(sourceEnds) ||
            !exportedTypes.SourceEnds.Equals(sourceEnds))
        {
            throw new ArgumentException(
                "A draft reference-table set requires all six catalogs to name one exact source-end identity.",
                nameof(sourceEnds));
        }

        return new MetadataModuleReferenceTableSetIdentity(
            sourceEnds,
            typeReferences,
            moduleReferences,
            typeSpecifications,
            assemblyReferences,
            files,
            exportedTypes);
    }

    /// <summary>Tests canonical equality between two draft reference-table sets.</summary>
    /// <param name="other">The other draft set.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataModuleReferenceTableSetIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft reference-table set equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a set with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataModuleReferenceTableSetIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this draft reference-table set.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Classifies one complete per-row TypeRef draft resolution disposition.</summary>
/// <remarks>
/// Every value is a complete derived row fact. A catalog containing non-resolved rows remains an exact complete
/// projection; only cross-input contradictions stop the issuing portfolio.
/// </remarks>
public enum MetadataTypeReferenceResolutionDispositionKind
{
    /// <summary>The row resolves to exactly one authority-issued TypeDef in the portfolio.</summary>
    Resolved = 1,

    /// <summary>The row's ResolutionScope names a ModuleRef netmodule, which W8 does not resolve.</summary>
    ModuleReferenceScopeUnsupported = 2,

    /// <summary>No portfolio module's assembly definition matches the row's AssemblyRef identity.</summary>
    TargetModuleAbsent = 3,

    /// <summary>More than one portfolio module's assembly definition matches the row's AssemblyRef identity.</summary>
    TargetModuleAmbiguous = 4,

    /// <summary>The target module contains no matching TypeDef and no matching top-level forwarder.</summary>
    TargetTypeAbsent = 5,

    /// <summary>The target module contains more than one matching TypeDef or top-level forwarder.</summary>
    TargetTypeAmbiguous = 6,

    /// <summary>The only matching ExportedType rows use File or enclosing-ExportedType implementations.</summary>
    ForwarderImplementationUnsupported = 7,

    /// <summary>The combined nested-parent and forwarder traversal reached the declared depth bound plus one.</summary>
    ResolutionDepthBoundReached = 8,

    /// <summary>A nested-parent or forwarder traversal revisited one physical coordinate.</summary>
    ResolutionCycleDetected = 9,

    /// <summary>The row's enclosing TypeRef parent did not itself resolve.</summary>
    ParentReferenceUnresolved = 10,
}

/// <summary>Freezes one guarded authority-derived TypeRef draft resolution row.</summary>
/// <remarks>
/// A resolved row retains the exact target module and authority-issued TypeDef plus every traversed forwarder row.
/// A non-resolved row retains its typed disposition and related evidence; no display-name or first-match fact enters
/// the draft result.
/// </remarks>
public sealed class MetadataTypeReferenceResolutionIdentity :
    IEquatable<MetadataTypeReferenceResolutionIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typeref-resolution-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataExportedTypePhysicalRowIdentity> traversedForwarders;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeReferenceResolutionIdentity(
        MetadataTypeReferencePhysicalRowIdentity referenceRow,
        MetadataTypeReferenceResolutionDispositionKind disposition,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        ImmutableArray<MetadataExportedTypePhysicalRowIdentity> traversedForwarders,
        int traversalStepCount,
        EvaluationDeterministicBound? reachedBound,
        int? relatedMetadataToken)
    {
        ReferenceRow = referenceRow;
        Disposition = disposition;
        TargetModule = targetModule;
        TargetTypeDefinition = targetTypeDefinition;
        this.traversedForwarders = ExpressionV2ContractEncoding.Copy(traversedForwarders);
        TraversalStepCount = traversalStepCount;
        ReachedBound = reachedBound;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(referenceRow.Sha256, nameof(referenceRow));
        writer.WriteInt32((int)disposition);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetModule?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetTypeDefinition?.Sha256);
        writer.WriteInt32(traversedForwarders.Length);
        foreach (var forwarder in traversedForwarders)
        {
            writer.WriteSha256(forwarder.Sha256, nameof(traversedForwarders));
        }
        writer.WriteInt32(traversalStepCount);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged guarded physical TypeRef draft row being resolved.</summary>
    public MetadataTypeReferencePhysicalRowIdentity ReferenceRow { get; }

    /// <summary>Gets the exact non-nil physical TypeRef token.</summary>
    public int TypeReferenceToken => ReferenceRow.TypeReferenceToken;

    /// <summary>Gets the complete typed draft resolution disposition for this row.</summary>
    public MetadataTypeReferenceResolutionDispositionKind Disposition { get; }

    /// <summary>Gets the exact target metadata module, or null for a non-resolved row.</summary>
    public StaticFieldMetadataModuleIdentity? TargetModule { get; }

    /// <summary>Gets the exact authority-issued target TypeDef, or null for a non-resolved row.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? TargetTypeDefinition { get; }

    /// <summary>Gets a defensive ordered copy of every traversed top-level forwarder draft row.</summary>
    public ImmutableArray<MetadataExportedTypePhysicalRowIdentity> TraversedForwarders =>
        ExpressionV2ContractEncoding.Copy(traversedForwarders);

    /// <summary>Gets the exact combined nested-parent and forwarder traversal step count.</summary>
    public int TraversalStepCount { get; }

    /// <summary>Gets the declared resolution depth bound only after a cap-plus-one traversal.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the disposition-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft resolution bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft resolution bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two TypeRef draft resolution rows.</summary>
    /// <param name="other">The other draft resolution row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeReferenceResolutionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests TypeRef draft resolution equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a resolution with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeReferenceResolutionIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this TypeRef draft resolution row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeReferenceResolutionIdentity Create(
        object mintCapability,
        MetadataTypeReferencePhysicalRowIdentity referenceRow,
        MetadataTypeReferenceResolutionDispositionKind disposition,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        ImmutableArray<MetadataExportedTypePhysicalRowIdentity> traversedForwarders,
        int traversalStepCount,
        EvaluationDeterministicBound? reachedBound,
        int? relatedMetadataToken)
    {
        if (!MetadataTypeReferenceResolutionPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A TypeRef resolution row requires the resolution portfolio's private mint capability.",
                nameof(mintCapability));
        }
        var resolved = disposition == MetadataTypeReferenceResolutionDispositionKind.Resolved;
        if (resolved == (targetModule is null) || resolved == (targetTypeDefinition is null))
        {
            throw new ArgumentException(
                "A TypeRef resolution row retains a target exactly when its disposition is resolved.",
                nameof(disposition));
        }

        return new MetadataTypeReferenceResolutionIdentity(
            referenceRow,
            disposition,
            targetModule,
            targetTypeDefinition,
            traversedForwarders,
            traversalStepCount,
            reachedBound,
            relatedMetadataToken);
    }
}

/// <summary>Freezes one guarded per-module entry of the TypeRef draft resolution portfolio.</summary>
/// <remarks>The entry retains its module's chain-portfolio lineage and one complete RID-ordered resolution vector.</remarks>
public sealed class MetadataTypeReferenceResolutionModuleIdentity :
    IEquatable<MetadataTypeReferenceResolutionModuleIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typeref-resolution-module";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataTypeReferenceResolutionIdentity> resolutions;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeReferenceResolutionModuleIdentity(
        MetadataNamedTypeDefinitionChainPortfolioEntryIdentity chainEntry,
        MetadataModuleReferenceTableSetIdentity referenceTables,
        ImmutableArray<MetadataTypeReferenceResolutionIdentity> resolutions)
    {
        ChainEntry = chainEntry;
        ReferenceTables = referenceTables;
        this.resolutions = ExpressionV2ContractEncoding.Copy(resolutions);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(chainEntry.Sha256, nameof(chainEntry));
        writer.WriteSha256(referenceTables.Sha256, nameof(referenceTables));
        writer.WriteInt32(resolutions.Length);
        foreach (var resolution in resolutions)
        {
            writer.WriteSha256(resolution.Sha256, nameof(resolutions));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact named-TypeDef-chain portfolio entry retained as module lineage.</summary>
    public MetadataNamedTypeDefinitionChainPortfolioEntryIdentity ChainEntry { get; }

    /// <summary>Gets the complete draft reference-table set consumed for this module.</summary>
    public MetadataModuleReferenceTableSetIdentity ReferenceTables { get; }

    /// <summary>Gets the exact metadata module owning every resolved TypeRef row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => ReferenceTables.SourceModule;

    /// <summary>Gets a defensive RID-order copy of every complete draft resolution row.</summary>
    public ImmutableArray<MetadataTypeReferenceResolutionIdentity> Resolutions =>
        ExpressionV2ContractEncoding.Copy(resolutions);

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft entry bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft entry bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Finds one complete TypeRef draft resolution row by token.</summary>
    /// <param name="typeReferenceToken">The non-nil physical TypeRef token.</param>
    /// <returns>The complete draft row, or null when the token is not an issued TypeRef.</returns>
    public MetadataTypeReferenceResolutionIdentity? FindResolution(int typeReferenceToken)
    {
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(typeReferenceToken, 0x01))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeReferenceToken);
        return rowId > 0 && rowId <= resolutions.Length ? resolutions[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two per-module draft resolution entries.</summary>
    /// <param name="other">The other draft entry.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeReferenceResolutionModuleIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests per-module draft resolution entry equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an entry with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeReferenceResolutionModuleIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this per-module draft resolution entry.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeReferenceResolutionModuleIdentity Create(
        object mintCapability,
        MetadataNamedTypeDefinitionChainPortfolioEntryIdentity chainEntry,
        MetadataModuleReferenceTableSetIdentity referenceTables,
        ImmutableArray<MetadataTypeReferenceResolutionIdentity> resolutions)
    {
        if (!MetadataTypeReferenceResolutionPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A per-module resolution entry requires the resolution portfolio's private mint capability.",
                nameof(mintCapability));
        }
        return new MetadataTypeReferenceResolutionModuleIdentity(chainEntry, referenceTables, resolutions);
    }
}

/// <summary>Classifies one normalized multi-module TypeRef draft resolution portfolio.</summary>
/// <remarks>Every stop is prefix-free; exact entries use the chain portfolio's deterministic module order.</remarks>
public enum MetadataTypeReferenceResolutionPortfolioResultKind
{
    /// <summary>Every chain-portfolio module has one complete RID-ordered resolution vector.</summary>
    Exact = 1,

    /// <summary>A prerequisite, missing vector slot, or non-exact member table prevented complete resolution.</summary>
    NonExact = 2,

    /// <summary>Complete draft inputs contradicted the chain portfolio or one another.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one TypeRef draft resolution portfolio.</summary>
/// <remarks>The issue values keep prerequisite, vector-shape, member-table, and module-correlation stops distinct.</remarks>
public enum MetadataTypeReferenceResolutionPortfolioIssue
{
    /// <summary>No issue applies to an exact draft portfolio.</summary>
    None = 0,

    /// <summary>The named-TypeDef-chain portfolio prerequisite was non-exact.</summary>
    ChainPortfolioNonExact = 1,

    /// <summary>The named-TypeDef-chain portfolio prerequisite was invalid.</summary>
    ChainPortfolioInvalid = 2,

    /// <summary>The supplied reference-table-set vector was default rather than explicitly initialized.</summary>
    TableSetVectorUninitialized = 3,

    /// <summary>The supplied reference-table-set vector reached the shared module bound plus one.</summary>
    ModuleCountBoundReached = 4,

    /// <summary>The supplied reference-table-set vector was shorter than the exact chain portfolio.</summary>
    TableSetSlotsIncomplete = 5,

    /// <summary>The supplied reference-table-set vector was longer than the exact chain portfolio.</summary>
    TableSetSlotCountConflict = 6,

    /// <summary>An initialized reference-table-set vector slot contained no set.</summary>
    TableSetMissing = 7,

    /// <summary>A member reference-table catalog stopped non-exactly.</summary>
    MemberTableNonExact = 8,

    /// <summary>A member reference-table catalog retained contradictory evidence.</summary>
    MemberTableInvalid = 9,

    /// <summary>More than one supplied reference-table set named the same exact metadata module.</summary>
    DuplicateSourceModule = 10,

    /// <summary>A supplied reference-table set named no module in the exact chain portfolio.</summary>
    SourceModuleNotInPortfolio = 11,

    /// <summary>A reference-table set's definition source ends differed from its module's authority source ends.</summary>
    ReferenceSourceEndsMismatch = 12,
}

/// <summary>Resolves every physical TypeRef row across one exact named-TypeDef-chain draft portfolio.</summary>
/// <remarks>
/// The portfolio is the sole issuer of draft resolution rows. Same-module lookup follows the exact Module scope,
/// nested lookup follows resolved TypeRef parents, and cross-module lookup matches AssemblyRef identity against
/// portfolio assembly definitions before following top-level AssemblyRef-implemented forwarders. Display names,
/// enumeration order, and first matches never select a target.
/// </remarks>
public sealed class MetadataTypeReferenceResolutionPortfolioIdentity :
    IEquatable<MetadataTypeReferenceResolutionPortfolioIdentity>
{
    /// <summary>Gets the shared module-count draft cap.</summary>
    public const int MaximumModuleCount = ExpressionV2ContractLimits.MaximumModuleCount;

    /// <summary>Gets the shared combined nested-parent and forwarder traversal draft cap.</summary>
    public const int MaximumResolutionDepth = ExpressionV2ContractLimits.MaximumTypeReferenceResolutionDepth;

    private const string CanonicalDomain = "metadata-v2-typeref-resolution-portfolio";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataTypeReferenceResolutionModuleIdentity> entries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeReferenceResolutionPortfolioIdentity(
        MetadataTypeReferenceResolutionPortfolioResultKind resultKind,
        MetadataTypeReferenceResolutionPortfolioIssue issue,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        ImmutableArray<MetadataTypeReferenceResolutionModuleIdentity> entries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        string? relatedModuleSha256)
    {
        ResultKind = resultKind;
        Issue = issue;
        ChainPortfolio = chainPortfolio;
        this.entries = ExpressionV2ContractEncoding.Copy(entries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedModuleSha256 = relatedModuleSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(chainPortfolio.Sha256, nameof(chainPortfolio));
        writer.WriteInt32(entries.Length);
        foreach (var entry in entries)
        {
            writer.WriteSha256(entry.Sha256, nameof(entries));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, relatedModuleSha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this normalized draft resolution portfolio is exact, non-exact, or invalid.</summary>
    public MetadataTypeReferenceResolutionPortfolioResultKind ResultKind { get; }

    /// <summary>Gets the typed draft portfolio issue, or none for an exact portfolio.</summary>
    public MetadataTypeReferenceResolutionPortfolioIssue Issue { get; }

    /// <summary>Gets the retained named-TypeDef-chain draft portfolio prerequisite.</summary>
    public MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio { get; }

    /// <summary>Gets the exact portfolio snapshot digest, or null for an empty portfolio or any stop.</summary>
    public string? SnapshotSha256 =>
        ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.Exact
            ? ChainPortfolio.SnapshotSha256
            : null;

    /// <summary>Gets a defensive module-order copy of exact draft entries, or an empty array for a stop.</summary>
    public ImmutableArray<MetadataTypeReferenceResolutionModuleIdentity> Entries =>
        ExpressionV2ContractEncoding.Copy(entries);

    /// <summary>Gets the propagated or module-count draft bound, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied, prerequisite, or cap-plus-one observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the issue-related metadata-module digest, otherwise null.</summary>
    public string? RelatedModuleSha256 { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft portfolio.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft portfolio.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one normalized multi-module TypeRef draft resolution portfolio.</summary>
    /// <param name="chainPortfolio">The exact named-TypeDef-chain portfolio defining modules and authority.</param>
    /// <param name="referenceTableSets">One initialized reference-table set for every chain-portfolio module.</param>
    /// <returns>An exact complete draft portfolio or a prefix-free typed stop.</returns>
    public static MetadataTypeReferenceResolutionPortfolioIdentity Create(
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        ImmutableArray<MetadataModuleReferenceTableSetIdentity> referenceTableSets)
    {
        ArgumentNullException.ThrowIfNull(chainPortfolio);
        if (chainPortfolio.ResultKind == MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact)
        {
            return Stopped(
                MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
                MetadataTypeReferenceResolutionPortfolioIssue.ChainPortfolioNonExact,
                chainPortfolio,
                chainPortfolio.ReachedBound,
                chainPortfolio.ObservedCount,
                chainPortfolio.RelatedModuleSha256);
        }
        if (chainPortfolio.ResultKind == MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid)
        {
            return Stopped(
                MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
                MetadataTypeReferenceResolutionPortfolioIssue.ChainPortfolioInvalid,
                chainPortfolio,
                chainPortfolio.ReachedBound,
                chainPortfolio.ObservedCount,
                chainPortfolio.RelatedModuleSha256);
        }
        if (referenceTableSets.IsDefault)
        {
            return Stopped(
                MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
                MetadataTypeReferenceResolutionPortfolioIssue.TableSetVectorUninitialized,
                chainPortfolio);
        }
        if (referenceTableSets.Length > MaximumModuleCount)
        {
            return Stopped(
                MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
                MetadataTypeReferenceResolutionPortfolioIssue.ModuleCountBoundReached,
                chainPortfolio,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ModuleCountBoundName,
                    MaximumModuleCount),
                MaximumModuleCount + 1);
        }

        var expectedCount = chainPortfolio.Entries.Length;
        if (referenceTableSets.Length < expectedCount)
        {
            return Stopped(
                MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
                MetadataTypeReferenceResolutionPortfolioIssue.TableSetSlotsIncomplete,
                chainPortfolio,
                observedCount: referenceTableSets.Length);
        }
        if (referenceTableSets.Length > expectedCount)
        {
            return Stopped(
                MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
                MetadataTypeReferenceResolutionPortfolioIssue.TableSetSlotCountConflict,
                chainPortfolio,
                observedCount: referenceTableSets.Length);
        }
        if (referenceTableSets.IsEmpty)
        {
            return Exact(chainPortfolio, []);
        }
        if (referenceTableSets.Any(static set => set is null))
        {
            return Stopped(
                MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
                MetadataTypeReferenceResolutionPortfolioIssue.TableSetMissing,
                chainPortfolio,
                observedCount: referenceTableSets.Length);
        }

        var ordered = referenceTableSets.ToArray();
        Array.Sort(ordered, static (left, right) =>
        {
            var moduleComparison = left.SourceModule.CanonicalBytes.AsSpan().SequenceCompareTo(
                right.SourceModule.CanonicalBytes.AsSpan());
            return moduleComparison != 0
                ? moduleComparison
                : StringComparer.Ordinal.Compare(left.Sha256, right.Sha256);
        });
        foreach (var set in ordered)
        {
            if (!set.AllTablesExact && !set.AnyTableInvalid)
            {
                return Stopped(
                    MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
                    MetadataTypeReferenceResolutionPortfolioIssue.MemberTableNonExact,
                    chainPortfolio,
                    observedCount: referenceTableSets.Length,
                    relatedModuleSha256: set.SourceModule.Sha256);
            }
        }
        foreach (var set in ordered)
        {
            if (set.AnyTableInvalid)
            {
                return Stopped(
                    MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
                    MetadataTypeReferenceResolutionPortfolioIssue.MemberTableInvalid,
                    chainPortfolio,
                    observedCount: referenceTableSets.Length,
                    relatedModuleSha256: set.SourceModule.Sha256);
            }
        }

        var setsByModule = new Dictionary<StaticFieldMetadataModuleIdentity, MetadataModuleReferenceTableSetIdentity>();
        foreach (var set in ordered)
        {
            if (!setsByModule.TryAdd(set.SourceModule, set))
            {
                return Stopped(
                    MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
                    MetadataTypeReferenceResolutionPortfolioIssue.DuplicateSourceModule,
                    chainPortfolio,
                    observedCount: referenceTableSets.Length,
                    relatedModuleSha256: set.SourceModule.Sha256);
            }
            var chainEntry = chainPortfolio.Entries.FirstOrDefault(entry =>
                entry.SourceModule.Equals(set.SourceModule));
            if (chainEntry is null)
            {
                return Stopped(
                    MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
                    MetadataTypeReferenceResolutionPortfolioIssue.SourceModuleNotInPortfolio,
                    chainPortfolio,
                    observedCount: referenceTableSets.Length,
                    relatedModuleSha256: set.SourceModule.Sha256);
            }
            if (!set.SourceEnds.DefinitionSourceEnds.Equals(chainEntry.ChainCatalog.SourceEnds))
            {
                return Stopped(
                    MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
                    MetadataTypeReferenceResolutionPortfolioIssue.ReferenceSourceEndsMismatch,
                    chainPortfolio,
                    observedCount: referenceTableSets.Length,
                    relatedModuleSha256: set.SourceModule.Sha256);
            }
        }

        var resolver = new PortfolioResolver(chainPortfolio, setsByModule);
        var entries = ImmutableArray.CreateBuilder<MetadataTypeReferenceResolutionModuleIdentity>(expectedCount);
        foreach (var chainEntry in chainPortfolio.Entries)
        {
            var set = setsByModule[chainEntry.SourceModule];
            var rows = set.TypeReferences.Rows;
            var resolutions = ImmutableArray.CreateBuilder<MetadataTypeReferenceResolutionIdentity>(rows.Length);
            foreach (var row in rows)
            {
                resolutions.Add(resolver.ResolveRow(RowMintCapability, chainEntry.SourceModule, row));
            }
            entries.Add(MetadataTypeReferenceResolutionModuleIdentity.Create(
                RowMintCapability,
                chainEntry,
                set,
                resolutions.MoveToImmutable()));
        }
        return Exact(chainPortfolio, entries.MoveToImmutable());
    }

    /// <summary>Looks up one complete draft resolution row by metadata module and non-nil TypeRef token.</summary>
    /// <param name="sourceModule">The exact loaded metadata module.</param>
    /// <param name="typeReferenceToken">The exact TypeRef token within that module.</param>
    /// <returns>The complete draft row, or null when the portfolio stopped, module is absent, or token was not issued.</returns>
    public MetadataTypeReferenceResolutionIdentity? ExactResolutionOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeReferenceToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        if (ResultKind != MetadataTypeReferenceResolutionPortfolioResultKind.Exact)
        {
            return null;
        }
        var entry = entries.FirstOrDefault(candidate => candidate.SourceModule.Equals(sourceModule));
        return entry?.FindResolution(typeReferenceToken);
    }

    /// <summary>Tests canonical equality between two TypeRef draft resolution portfolios.</summary>
    /// <param name="other">The other draft portfolio.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeReferenceResolutionPortfolioIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests TypeRef draft resolution portfolio equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a portfolio with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeReferenceResolutionPortfolioIdentity);

    /// <summary>Computes a deterministic hash code from immutable draft portfolio content.</summary>
    /// <returns>A hash code for this canonical draft portfolio.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);

    private static MetadataTypeReferenceResolutionPortfolioIdentity Exact(
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        ImmutableArray<MetadataTypeReferenceResolutionModuleIdentity> entries) =>
        new(
            MetadataTypeReferenceResolutionPortfolioResultKind.Exact,
            MetadataTypeReferenceResolutionPortfolioIssue.None,
            chainPortfolio,
            entries,
            null,
            0,
            null);

    private static MetadataTypeReferenceResolutionPortfolioIdentity Stopped(
        MetadataTypeReferenceResolutionPortfolioResultKind resultKind,
        MetadataTypeReferenceResolutionPortfolioIssue issue,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        EvaluationDeterministicBound? reachedBound = null,
        int observedCount = 0,
        string? relatedModuleSha256 = null) =>
        new(
            resultKind,
            issue,
            chainPortfolio,
            [],
            reachedBound,
            observedCount,
            relatedModuleSha256);

    private sealed class PortfolioResolver
    {
        private readonly Dictionary<StaticFieldMetadataModuleIdentity, ModuleIndex> indexesByModule = new();
        private readonly Dictionary<(string ModuleSha, int Token), ResolutionState> statesByRow = new();

        internal PortfolioResolver(
            MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
            Dictionary<StaticFieldMetadataModuleIdentity, MetadataModuleReferenceTableSetIdentity> setsByModule)
        {
            foreach (var chainEntry in chainPortfolio.Entries)
            {
                indexesByModule.Add(
                    chainEntry.SourceModule,
                    new ModuleIndex(chainEntry, setsByModule[chainEntry.SourceModule]));
            }
        }

        internal MetadataTypeReferenceResolutionIdentity ResolveRow(
            object mintCapability,
            StaticFieldMetadataModuleIdentity sourceModule,
            MetadataTypeReferencePhysicalRowIdentity row)
        {
            var state = ResolveState(sourceModule, row.TypeReferenceToken, new HashSet<(string, int)>());
            return MetadataTypeReferenceResolutionIdentity.Create(
                mintCapability,
                row,
                state.Disposition,
                state.TargetModule,
                state.TargetTypeDefinition,
                state.TraversedForwarders,
                state.TraversalStepCount,
                state.ReachedBound,
                state.RelatedMetadataToken);
        }

        private ResolutionState ResolveState(
            StaticFieldMetadataModuleIdentity sourceModule,
            int typeReferenceToken,
            HashSet<(string, int)> inProgress)
        {
            var key = (sourceModule.Sha256, typeReferenceToken);
            if (statesByRow.TryGetValue(key, out var memoized))
            {
                return memoized;
            }
            if (!inProgress.Add(key))
            {
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.ResolutionCycleDetected,
                    null,
                    null,
                    [],
                    inProgress.Count,
                    null,
                    typeReferenceToken);
            }

            var index = indexesByModule[sourceModule];
            var row = index.ReferenceTables.TypeReferences.FindRow(typeReferenceToken)!;
            var observation = row.Observation;
            var scope = observation.ResolutionScopeMetadataToken;
            ResolutionState state;
            if (CanonicalReplayEncoding.IsMetadataTokenForTable(scope, 0x1A))
            {
                state = new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.ModuleReferenceScopeUnsupported,
                    null,
                    null,
                    [],
                    0,
                    null,
                    scope);
            }
            else if (CanonicalReplayEncoding.IsMetadataTokenForTable(scope, 0x00))
            {
                state = ResolveTopLevelOrForwarded(
                    index,
                    observation.NamespaceName,
                    observation.TypeName,
                    stepCount: 0,
                    []);
            }
            else if (CanonicalReplayEncoding.IsMetadataTokenForTable(scope, 0x23))
            {
                state = ResolveThroughAssemblyReference(
                    index,
                    scope,
                    observation.NamespaceName,
                    observation.TypeName,
                    stepCount: 0,
                    []);
            }
            else
            {
                var parentState = ResolveState(sourceModule, scope, inProgress);
                if (parentState.Disposition == MetadataTypeReferenceResolutionDispositionKind.ResolutionCycleDetected)
                {
                    state = new ResolutionState(
                        MetadataTypeReferenceResolutionDispositionKind.ResolutionCycleDetected,
                        null,
                        null,
                        [],
                        parentState.TraversalStepCount,
                        null,
                        scope);
                }
                else if (parentState.Disposition != MetadataTypeReferenceResolutionDispositionKind.Resolved)
                {
                    state = new ResolutionState(
                        MetadataTypeReferenceResolutionDispositionKind.ParentReferenceUnresolved,
                        null,
                        null,
                        [],
                        parentState.TraversalStepCount,
                        parentState.ReachedBound,
                        scope);
                }
                else
                {
                    var stepCount = parentState.TraversalStepCount + 1;
                    if (stepCount > MaximumResolutionDepth)
                    {
                        state = DepthBoundReached(stepCount, typeReferenceToken);
                    }
                    else
                    {
                        var targetIndex = indexesByModule[parentState.TargetModule!];
                        state = targetIndex.ResolveNested(
                            parentState.TargetTypeDefinition!.TypeDefinitionToken,
                            observation.NamespaceName,
                            observation.TypeName,
                            stepCount,
                            parentState.TraversedForwarders);
                    }
                }
            }

            inProgress.Remove(key);
            statesByRow[key] = state;
            return state;
        }

        private ResolutionState ResolveThroughAssemblyReference(
            ModuleIndex index,
            int assemblyReferenceToken,
            string namespaceName,
            string typeName,
            int stepCount,
            ImmutableArray<MetadataExportedTypePhysicalRowIdentity> traversedForwarders)
        {
            var assemblyReference = index.AssemblyReference(assemblyReferenceToken);
            var matches = indexesByModule.Values
                .Where(candidate => StaticFieldExportedTypeForwarderIdentity.AssemblyReferenceMatchesDefinition(
                    assemblyReference,
                    candidate.SourceModule.ContainingAssembly.AssemblyDefinition))
                .ToArray();
            if (matches.Length == 0)
            {
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.TargetModuleAbsent,
                    null,
                    null,
                    traversedForwarders,
                    stepCount,
                    null,
                    assemblyReferenceToken);
            }
            if (matches.Length > 1)
            {
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.TargetModuleAmbiguous,
                    null,
                    null,
                    traversedForwarders,
                    stepCount,
                    null,
                    assemblyReferenceToken);
            }
            return ResolveTopLevelOrForwarded(matches[0], namespaceName, typeName, stepCount, traversedForwarders);
        }

        private ResolutionState ResolveTopLevelOrForwarded(
            ModuleIndex index,
            string namespaceName,
            string typeName,
            int stepCount,
            ImmutableArray<MetadataExportedTypePhysicalRowIdentity> traversedForwarders)
        {
            var definitions = index.TopLevelDefinitions(namespaceName, typeName);
            if (definitions.Length == 1)
            {
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.Resolved,
                    index.SourceModule,
                    definitions[0],
                    traversedForwarders,
                    stepCount,
                    null,
                    null);
            }
            if (definitions.Length > 1)
            {
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.TargetTypeAmbiguous,
                    null,
                    null,
                    traversedForwarders,
                    stepCount,
                    null,
                    definitions[0].TypeDefinitionToken);
            }

            var forwarders = index.TopLevelForwarders(namespaceName, typeName);
            if (forwarders.Length > 1)
            {
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.TargetTypeAmbiguous,
                    null,
                    null,
                    traversedForwarders,
                    stepCount,
                    null,
                    forwarders[0].ExportedTypeToken);
            }
            if (forwarders.Length == 1)
            {
                var nextStepCount = stepCount + 1;
                if (nextStepCount > MaximumResolutionDepth)
                {
                    return DepthBoundReached(nextStepCount, forwarders[0].ExportedTypeToken);
                }
                return ResolveThroughAssemblyReference(
                    index,
                    forwarders[0].Observation.ImplementationMetadataToken,
                    namespaceName,
                    typeName,
                    nextStepCount,
                    traversedForwarders.Add(forwarders[0]));
            }
            if (index.HasUnsupportedForwarder(namespaceName, typeName, out var unsupportedToken))
            {
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.ForwarderImplementationUnsupported,
                    null,
                    null,
                    traversedForwarders,
                    stepCount,
                    null,
                    unsupportedToken);
            }
            return new ResolutionState(
                MetadataTypeReferenceResolutionDispositionKind.TargetTypeAbsent,
                null,
                null,
                traversedForwarders,
                stepCount,
                null,
                null);
        }

        private static ResolutionState DepthBoundReached(int stepCount, int relatedMetadataToken) =>
            new(
                MetadataTypeReferenceResolutionDispositionKind.ResolutionDepthBoundReached,
                null,
                null,
                [],
                stepCount,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.TypeReferenceResolutionDepthBoundName,
                    MaximumResolutionDepth),
                relatedMetadataToken);

        private sealed record ResolutionState(
            MetadataTypeReferenceResolutionDispositionKind Disposition,
            StaticFieldMetadataModuleIdentity? TargetModule,
            MetadataTypeDefinitionAuthorityIdentity? TargetTypeDefinition,
            ImmutableArray<MetadataExportedTypePhysicalRowIdentity> TraversedForwarders,
            int TraversalStepCount,
            EvaluationDeterministicBound? ReachedBound,
            int? RelatedMetadataToken);

        private sealed class ModuleIndex
        {
            private const int ModulePseudoTypeToken = 0x0200_0001;
            private readonly Dictionary<(string, string), ImmutableArray<MetadataTypeDefinitionAuthorityIdentity>>
                topLevelByName;
            private readonly Dictionary<(int, string, string), ImmutableArray<MetadataTypeDefinitionAuthorityIdentity>>
                nestedByParent;
            private readonly Dictionary<(string, string), ImmutableArray<MetadataExportedTypePhysicalRowIdentity>>
                assemblyForwardersByName;
            private readonly Dictionary<(string, string), int> unsupportedForwardersByName;

            internal ModuleIndex(
                MetadataNamedTypeDefinitionChainPortfolioEntryIdentity chainEntry,
                MetadataModuleReferenceTableSetIdentity referenceTables)
            {
                SourceModule = chainEntry.SourceModule;
                ReferenceTables = referenceTables;
                var topLevel = new Dictionary<(string, string), List<MetadataTypeDefinitionAuthorityIdentity>>();
                var nested = new Dictionary<(int, string, string), List<MetadataTypeDefinitionAuthorityIdentity>>();
                foreach (var typeDefinition in chainEntry.ChainCatalog.DefinitionAuthority.TypeDefinitions)
                {
                    if (typeDefinition.TypeDefinitionToken == ModulePseudoTypeToken)
                    {
                        continue;
                    }
                    if (typeDefinition.EnclosingTypeDefinitionToken is { } parentToken)
                    {
                        var nestedKey = (parentToken, typeDefinition.NamespaceName, typeDefinition.TypeName);
                        if (!nested.TryGetValue(nestedKey, out var nestedList))
                        {
                            nestedList = [];
                            nested.Add(nestedKey, nestedList);
                        }
                        nestedList.Add(typeDefinition);
                    }
                    else
                    {
                        var key = (typeDefinition.NamespaceName, typeDefinition.TypeName);
                        if (!topLevel.TryGetValue(key, out var list))
                        {
                            list = [];
                            topLevel.Add(key, list);
                        }
                        list.Add(typeDefinition);
                    }
                }
                topLevelByName = topLevel.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToImmutableArray());
                nestedByParent = nested.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToImmutableArray());

                var assemblyForwarders =
                    new Dictionary<(string, string), List<MetadataExportedTypePhysicalRowIdentity>>();
                var unsupportedForwarders = new Dictionary<(string, string), int>();
                foreach (var exported in referenceTables.ExportedTypes.Rows)
                {
                    var key = (exported.Observation.NamespaceName, exported.Observation.TypeName);
                    if (CanonicalReplayEncoding.IsMetadataTokenForTable(
                        exported.Observation.ImplementationMetadataToken,
                        0x23))
                    {
                        if (!assemblyForwarders.TryGetValue(key, out var list))
                        {
                            list = [];
                            assemblyForwarders.Add(key, list);
                        }
                        list.Add(exported);
                    }
                    else if (!unsupportedForwarders.ContainsKey(key))
                    {
                        unsupportedForwarders.Add(key, exported.ExportedTypeToken);
                    }
                }
                assemblyForwardersByName = assemblyForwarders.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToImmutableArray());
                unsupportedForwardersByName = unsupportedForwarders;
            }

            internal StaticFieldMetadataModuleIdentity SourceModule { get; }

            internal MetadataModuleReferenceTableSetIdentity ReferenceTables { get; }

            internal ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> TopLevelDefinitions(
                string namespaceName,
                string typeName) =>
                topLevelByName.TryGetValue((namespaceName, typeName), out var definitions) ? definitions : [];

            internal ImmutableArray<MetadataExportedTypePhysicalRowIdentity> TopLevelForwarders(
                string namespaceName,
                string typeName) =>
                assemblyForwardersByName.TryGetValue((namespaceName, typeName), out var forwarders)
                    ? forwarders
                    : [];

            internal bool HasUnsupportedForwarder(string namespaceName, string typeName, out int exportedTypeToken) =>
                unsupportedForwardersByName.TryGetValue((namespaceName, typeName), out exportedTypeToken);

            internal StaticFieldAssemblyReferenceIdentity AssemblyReference(int assemblyReferenceToken)
            {
                var row = ReferenceTables.AssemblyReferences.FindRow(assemblyReferenceToken)!;
                var observation = row.Observation;
                return StaticFieldAssemblyReferenceIdentity.Create(
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
            }

            internal ResolutionState ResolveNested(
                int parentTypeDefinitionToken,
                string namespaceName,
                string typeName,
                int stepCount,
                ImmutableArray<MetadataExportedTypePhysicalRowIdentity> traversedForwarders)
            {
                var key = (parentTypeDefinitionToken, namespaceName, typeName);
                if (!nestedByParent.TryGetValue(key, out var definitions) || definitions.IsEmpty)
                {
                    return new ResolutionState(
                        MetadataTypeReferenceResolutionDispositionKind.TargetTypeAbsent,
                        null,
                        null,
                        traversedForwarders,
                        stepCount,
                        null,
                        parentTypeDefinitionToken);
                }
                if (definitions.Length > 1)
                {
                    return new ResolutionState(
                        MetadataTypeReferenceResolutionDispositionKind.TargetTypeAmbiguous,
                        null,
                        null,
                        traversedForwarders,
                        stepCount,
                        null,
                        definitions[0].TypeDefinitionToken);
                }
                return new ResolutionState(
                    MetadataTypeReferenceResolutionDispositionKind.Resolved,
                    SourceModule,
                    definitions[0],
                    traversedForwarders,
                    stepCount,
                    null,
                    null);
            }
        }
    }
}
