using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies the physical form of one resolved constraint-target draft row.</summary>
/// <remarks>Every value is a complete derived row fact rather than a catalog failure.</remarks>
public enum MetadataConstraintTargetKind
{
    /// <summary>The Constraint column names a TypeDef in the same module.</summary>
    TypeDefinition = 1,

    /// <summary>The Constraint column names a TypeRef whose portfolio resolution is exact.</summary>
    TypeReference = 2,

    /// <summary>The Constraint column names a TypeRef whose portfolio resolution retained a typed stop.</summary>
    TypeReferenceUnresolved = 3,

    /// <summary>The Constraint column names a TypeSpec retained for later constructed-constraint substitution.</summary>
    TypeSpecification = 4,
}

/// <summary>Freezes one guarded authority-derived constraint-target draft row.</summary>
/// <remarks>
/// The row joins one authority-issued GenericParamConstraint row to its exact named target or retained TypeSpec.
/// It performs no constraint semantics; substituted validation stays with the later constructed-constraint work.
/// </remarks>
public sealed class MetadataConstraintTargetResolutionIdentity :
    IEquatable<MetadataConstraintTargetResolutionIdentity>
{
    private const string CanonicalDomain = "metadata-v2-constraint-target-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataConstraintTargetResolutionIdentity(
        MetadataGenericParameterConstraintTableRowIdentity constraintRow,
        MetadataConstraintTargetKind kind,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeReferenceResolutionIdentity? referenceResolution,
        MetadataTypeSpecificationPhysicalRowIdentity? typeSpecificationRow)
    {
        ConstraintRow = constraintRow;
        Kind = kind;
        TargetModule = targetModule;
        TargetTypeDefinition = targetTypeDefinition;
        ReferenceResolution = referenceResolution;
        TypeSpecificationRow = typeSpecificationRow;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(constraintRow.Sha256, nameof(constraintRow));
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetModule?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetTypeDefinition?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, referenceResolution?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, typeSpecificationRow?.Sha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged authority-issued GenericParamConstraint draft row.</summary>
    public MetadataGenericParameterConstraintTableRowIdentity ConstraintRow { get; }

    /// <summary>Gets the exact GenericParamConstraint row token.</summary>
    public int GenericParameterConstraintToken => ConstraintRow.GenericParameterConstraintToken;

    /// <summary>Gets the validated raw physical TypeDefOrRef Constraint token.</summary>
    public int ConstraintMetadataToken => ConstraintRow.ConstraintMetadataToken;

    /// <summary>Gets the complete typed physical form of this draft target.</summary>
    public MetadataConstraintTargetKind Kind { get; }

    /// <summary>Gets the exact target module for a named resolved draft target, otherwise null.</summary>
    public StaticFieldMetadataModuleIdentity? TargetModule { get; }

    /// <summary>Gets the exact authority-issued target TypeDef for a named resolved draft target, otherwise null.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? TargetTypeDefinition { get; }

    /// <summary>Gets the retained TypeRef draft resolution row for a reference-formed target, otherwise null.</summary>
    public MetadataTypeReferenceResolutionIdentity? ReferenceResolution { get; }

    /// <summary>Gets the retained physical TypeSpec draft row for a signature-formed target, otherwise null.</summary>
    public MetadataTypeSpecificationPhysicalRowIdentity? TypeSpecificationRow { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two constraint-target draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataConstraintTargetResolutionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constraint-target draft row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataConstraintTargetResolutionIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this constraint-target draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataConstraintTargetResolutionIdentity Create(
        object mintCapability,
        MetadataGenericParameterConstraintTableRowIdentity constraintRow,
        MetadataConstraintTargetKind kind,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeReferenceResolutionIdentity? referenceResolution,
        MetadataTypeSpecificationPhysicalRowIdentity? typeSpecificationRow)
    {
        if (!MetadataConstraintTargetResolutionPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A constraint-target row requires the constraint-target portfolio's private mint capability.",
                nameof(mintCapability));
        }
        var named = kind is MetadataConstraintTargetKind.TypeDefinition or MetadataConstraintTargetKind.TypeReference;
        if (named == (targetModule is null) || named == (targetTypeDefinition is null))
        {
            throw new ArgumentException(
                "A constraint-target row retains a named target exactly when its kind is a resolved definition.",
                nameof(kind));
        }
        return new MetadataConstraintTargetResolutionIdentity(
            constraintRow,
            kind,
            targetModule,
            targetTypeDefinition,
            referenceResolution,
            typeSpecificationRow);
    }
}

/// <summary>Freezes one guarded per-module entry of the constraint-target draft portfolio.</summary>
/// <remarks>The entry retains its complete constraint-catalog lineage and one RID-ordered target vector.</remarks>
public sealed class MetadataConstraintTargetResolutionModuleIdentity :
    IEquatable<MetadataConstraintTargetResolutionModuleIdentity>
{
    private const string CanonicalDomain = "metadata-v2-constraint-target-module";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataConstraintTargetResolutionIdentity> targets;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataConstraintTargetResolutionModuleIdentity(
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity constraintCatalog,
        MetadataTypeReferenceResolutionModuleIdentity resolutionEntry,
        ImmutableArray<MetadataConstraintTargetResolutionIdentity> targets)
    {
        ConstraintCatalog = constraintCatalog;
        ResolutionEntry = resolutionEntry;
        this.targets = ExpressionV2ContractEncoding.Copy(targets);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(constraintCatalog.Sha256, nameof(constraintCatalog));
        writer.WriteSha256(resolutionEntry.Sha256, nameof(resolutionEntry));
        writer.WriteInt32(targets.Length);
        foreach (var target in targets)
        {
            writer.WriteSha256(target.Sha256, nameof(targets));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the retained complete physical GenericParamConstraint draft catalog.</summary>
    public MetadataGenericParameterConstraintPhysicalTableCatalogIdentity ConstraintCatalog { get; }

    /// <summary>Gets the retained per-module TypeRef draft resolution entry lineage.</summary>
    public MetadataTypeReferenceResolutionModuleIdentity ResolutionEntry { get; }

    /// <summary>Gets the exact metadata module owning every constraint row in this draft entry.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => ResolutionEntry.SourceModule;

    /// <summary>Gets a defensive RID-order copy of every constraint-target draft row.</summary>
    public ImmutableArray<MetadataConstraintTargetResolutionIdentity> Targets =>
        ExpressionV2ContractEncoding.Copy(targets);

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft entry bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft entry bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Finds one constraint-target draft row by GenericParamConstraint token.</summary>
    /// <param name="genericParameterConstraintToken">The non-nil GenericParamConstraint token.</param>
    /// <returns>The complete draft row, or null when the token is not an issued constraint row.</returns>
    public MetadataConstraintTargetResolutionIdentity? FindTarget(int genericParameterConstraintToken)
    {
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(genericParameterConstraintToken, 0x2C))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(genericParameterConstraintToken);
        return rowId > 0 && rowId <= targets.Length ? targets[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two per-module constraint-target draft entries.</summary>
    /// <param name="other">The other draft entry.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataConstraintTargetResolutionModuleIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests per-module constraint-target draft entry equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an entry with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataConstraintTargetResolutionModuleIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this per-module constraint-target draft entry.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataConstraintTargetResolutionModuleIdentity Create(
        object mintCapability,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity constraintCatalog,
        MetadataTypeReferenceResolutionModuleIdentity resolutionEntry,
        ImmutableArray<MetadataConstraintTargetResolutionIdentity> targets)
    {
        if (!MetadataConstraintTargetResolutionPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A per-module constraint-target entry requires the portfolio's private mint capability.",
                nameof(mintCapability));
        }
        return new MetadataConstraintTargetResolutionModuleIdentity(constraintCatalog, resolutionEntry, targets);
    }
}

/// <summary>Classifies one normalized multi-module constraint-target draft portfolio.</summary>
/// <remarks>Every stop is prefix-free; exact entries use the resolution portfolio's module order.</remarks>
public enum MetadataConstraintTargetResolutionPortfolioResultKind
{
    /// <summary>Every module has one complete RID-ordered constraint-target vector.</summary>
    Exact = 1,

    /// <summary>A prerequisite, missing vector slot, or non-exact constraint catalog prevented completion.</summary>
    NonExact = 2,

    /// <summary>Complete draft inputs contradicted the resolution portfolio or one another.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one constraint-target draft portfolio.</summary>
/// <remarks>The issue values keep prerequisite, vector-shape, catalog, and lineage stops distinct.</remarks>
public enum MetadataConstraintTargetResolutionPortfolioIssue
{
    /// <summary>No issue applies to an exact draft portfolio.</summary>
    None = 0,

    /// <summary>The TypeRef resolution portfolio prerequisite was non-exact.</summary>
    ResolutionPortfolioNonExact = 1,

    /// <summary>The TypeRef resolution portfolio prerequisite was invalid.</summary>
    ResolutionPortfolioInvalid = 2,

    /// <summary>The supplied constraint-catalog vector was default rather than explicitly initialized.</summary>
    CatalogVectorUninitialized = 3,

    /// <summary>The supplied constraint-catalog vector reached the shared module bound plus one.</summary>
    ModuleCountBoundReached = 4,

    /// <summary>The supplied constraint-catalog vector was shorter than the exact resolution portfolio.</summary>
    CatalogSlotsIncomplete = 5,

    /// <summary>The supplied constraint-catalog vector was longer than the exact resolution portfolio.</summary>
    CatalogSlotCountConflict = 6,

    /// <summary>An initialized constraint-catalog vector slot contained no catalog.</summary>
    CatalogMissing = 7,

    /// <summary>A supplied constraint catalog stopped non-exactly.</summary>
    ConstraintCatalogNonExact = 8,

    /// <summary>A supplied constraint catalog retained contradictory evidence.</summary>
    ConstraintCatalogInvalid = 9,

    /// <summary>More than one supplied constraint catalog named the same exact metadata module.</summary>
    DuplicateSourceModule = 10,

    /// <summary>A supplied constraint catalog named no module in the exact resolution portfolio.</summary>
    SourceModuleNotInPortfolio = 11,

    /// <summary>A constraint catalog's source ends differed from its module's authority source ends.</summary>
    ConstraintSourceEndsMismatch = 12,
}

/// <summary>Joins every authority-issued constraint row to its exact named target or retained TypeSpec.</summary>
/// <remarks>
/// The portfolio is the sole issuer of constraint-target draft rows. Same-module TypeDef targets select their
/// authority row directly, TypeRef targets consume the exact resolution portfolio, and TypeSpec targets retain the
/// guarded signature row for the later constructed-constraint substitution work.
/// </remarks>
public sealed class MetadataConstraintTargetResolutionPortfolioIdentity :
    IEquatable<MetadataConstraintTargetResolutionPortfolioIdentity>
{
    /// <summary>Gets the shared module-count draft cap.</summary>
    public const int MaximumModuleCount = ExpressionV2ContractLimits.MaximumModuleCount;

    private const string CanonicalDomain = "metadata-v2-constraint-target-portfolio";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataConstraintTargetResolutionModuleIdentity> entries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataConstraintTargetResolutionPortfolioIdentity(
        MetadataConstraintTargetResolutionPortfolioResultKind resultKind,
        MetadataConstraintTargetResolutionPortfolioIssue issue,
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio,
        ImmutableArray<MetadataConstraintTargetResolutionModuleIdentity> entries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        string? relatedModuleSha256)
    {
        ResultKind = resultKind;
        Issue = issue;
        ResolutionPortfolio = resolutionPortfolio;
        this.entries = ExpressionV2ContractEncoding.Copy(entries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedModuleSha256 = relatedModuleSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(resolutionPortfolio.Sha256, nameof(resolutionPortfolio));
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

    /// <summary>Gets whether this normalized constraint-target draft portfolio is exact, non-exact, or invalid.</summary>
    public MetadataConstraintTargetResolutionPortfolioResultKind ResultKind { get; }

    /// <summary>Gets the typed draft portfolio issue, or none for an exact portfolio.</summary>
    public MetadataConstraintTargetResolutionPortfolioIssue Issue { get; }

    /// <summary>Gets the retained TypeRef draft resolution portfolio prerequisite.</summary>
    public MetadataTypeReferenceResolutionPortfolioIdentity ResolutionPortfolio { get; }

    /// <summary>Gets the exact portfolio snapshot digest, or null for an empty portfolio or any stop.</summary>
    public string? SnapshotSha256 =>
        ResultKind == MetadataConstraintTargetResolutionPortfolioResultKind.Exact
            ? ResolutionPortfolio.SnapshotSha256
            : null;

    /// <summary>Gets a defensive module-order copy of exact draft entries, or an empty array for a stop.</summary>
    public ImmutableArray<MetadataConstraintTargetResolutionModuleIdentity> Entries =>
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

    /// <summary>Creates one normalized multi-module constraint-target draft portfolio.</summary>
    /// <param name="resolutionPortfolio">The exact TypeRef resolution portfolio defining modules and authority.</param>
    /// <param name="constraintCatalogs">One complete constraint catalog for every resolution-portfolio module.</param>
    /// <returns>An exact complete draft portfolio or a prefix-free typed stop.</returns>
    public static MetadataConstraintTargetResolutionPortfolioIdentity Create(
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio,
        ImmutableArray<MetadataGenericParameterConstraintPhysicalTableCatalogIdentity> constraintCatalogs)
    {
        ArgumentNullException.ThrowIfNull(resolutionPortfolio);
        if (resolutionPortfolio.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.NonExact)
        {
            return Stopped(
                MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
                MetadataConstraintTargetResolutionPortfolioIssue.ResolutionPortfolioNonExact,
                resolutionPortfolio,
                resolutionPortfolio.ReachedBound,
                resolutionPortfolio.ObservedCount,
                resolutionPortfolio.RelatedModuleSha256);
        }
        if (resolutionPortfolio.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.Invalid)
        {
            return Stopped(
                MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
                MetadataConstraintTargetResolutionPortfolioIssue.ResolutionPortfolioInvalid,
                resolutionPortfolio,
                resolutionPortfolio.ReachedBound,
                resolutionPortfolio.ObservedCount,
                resolutionPortfolio.RelatedModuleSha256);
        }
        if (constraintCatalogs.IsDefault)
        {
            return Stopped(
                MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
                MetadataConstraintTargetResolutionPortfolioIssue.CatalogVectorUninitialized,
                resolutionPortfolio);
        }
        if (constraintCatalogs.Length > MaximumModuleCount)
        {
            return Stopped(
                MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
                MetadataConstraintTargetResolutionPortfolioIssue.ModuleCountBoundReached,
                resolutionPortfolio,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ModuleCountBoundName,
                    MaximumModuleCount),
                MaximumModuleCount + 1);
        }

        var resolutionEntries = resolutionPortfolio.Entries;
        if (constraintCatalogs.Length < resolutionEntries.Length)
        {
            return Stopped(
                MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
                MetadataConstraintTargetResolutionPortfolioIssue.CatalogSlotsIncomplete,
                resolutionPortfolio,
                observedCount: constraintCatalogs.Length);
        }
        if (constraintCatalogs.Length > resolutionEntries.Length)
        {
            return Stopped(
                MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
                MetadataConstraintTargetResolutionPortfolioIssue.CatalogSlotCountConflict,
                resolutionPortfolio,
                observedCount: constraintCatalogs.Length);
        }
        if (constraintCatalogs.IsEmpty)
        {
            return Exact(resolutionPortfolio, []);
        }
        if (constraintCatalogs.Any(static catalog => catalog is null))
        {
            return Stopped(
                MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
                MetadataConstraintTargetResolutionPortfolioIssue.CatalogMissing,
                resolutionPortfolio,
                observedCount: constraintCatalogs.Length);
        }

        var ordered = constraintCatalogs.ToArray();
        Array.Sort(ordered, static (left, right) =>
        {
            var moduleComparison = left.SourceEnds.SourceModule.CanonicalBytes.AsSpan().SequenceCompareTo(
                right.SourceEnds.SourceModule.CanonicalBytes.AsSpan());
            return moduleComparison != 0
                ? moduleComparison
                : StringComparer.Ordinal.Compare(left.Sha256, right.Sha256);
        });
        foreach (var catalog in ordered)
        {
            if (catalog.ResultKind == MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact)
            {
                return Stopped(
                    MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
                    MetadataConstraintTargetResolutionPortfolioIssue.ConstraintCatalogNonExact,
                    resolutionPortfolio,
                    observedCount: constraintCatalogs.Length,
                    relatedModuleSha256: catalog.SourceEnds.SourceModule.Sha256);
            }
        }
        foreach (var catalog in ordered)
        {
            if (catalog.ResultKind == MetadataGenericParameterConstraintPhysicalTableResultKind.Invalid)
            {
                return Stopped(
                    MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
                    MetadataConstraintTargetResolutionPortfolioIssue.ConstraintCatalogInvalid,
                    resolutionPortfolio,
                    observedCount: constraintCatalogs.Length,
                    relatedModuleSha256: catalog.SourceEnds.SourceModule.Sha256);
            }
        }

        var catalogsByModule = new Dictionary<StaticFieldMetadataModuleIdentity,
            MetadataGenericParameterConstraintPhysicalTableCatalogIdentity>();
        foreach (var catalog in ordered)
        {
            var sourceModule = catalog.SourceEnds.SourceModule;
            if (!catalogsByModule.TryAdd(sourceModule, catalog))
            {
                return Stopped(
                    MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
                    MetadataConstraintTargetResolutionPortfolioIssue.DuplicateSourceModule,
                    resolutionPortfolio,
                    observedCount: constraintCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
            var resolutionEntry = resolutionEntries.FirstOrDefault(entry =>
                entry.SourceModule.Equals(sourceModule));
            if (resolutionEntry is null)
            {
                return Stopped(
                    MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
                    MetadataConstraintTargetResolutionPortfolioIssue.SourceModuleNotInPortfolio,
                    resolutionPortfolio,
                    observedCount: constraintCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
            if (!catalog.SourceEnds.Equals(resolutionEntry.ChainEntry.ChainCatalog.SourceEnds))
            {
                return Stopped(
                    MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
                    MetadataConstraintTargetResolutionPortfolioIssue.ConstraintSourceEndsMismatch,
                    resolutionPortfolio,
                    observedCount: constraintCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
        }

        var entries = ImmutableArray.CreateBuilder<MetadataConstraintTargetResolutionModuleIdentity>(
            resolutionEntries.Length);
        foreach (var resolutionEntry in resolutionEntries)
        {
            var catalog = catalogsByModule[resolutionEntry.SourceModule];
            var typeDefinitions = resolutionEntry.ChainEntry.ChainCatalog.DefinitionAuthority.TypeDefinitions;
            var rows = catalog.Rows;
            var targets = ImmutableArray.CreateBuilder<MetadataConstraintTargetResolutionIdentity>(rows.Length);
            foreach (var row in rows)
            {
                targets.Add(ResolveTarget(resolutionEntry, typeDefinitions, row));
            }
            entries.Add(MetadataConstraintTargetResolutionModuleIdentity.Create(
                RowMintCapability,
                catalog,
                resolutionEntry,
                targets.MoveToImmutable()));
        }
        return Exact(resolutionPortfolio, entries.MoveToImmutable());
    }

    /// <summary>Looks up one constraint-target draft row by module and non-nil GenericParamConstraint token.</summary>
    /// <param name="sourceModule">The exact loaded metadata module.</param>
    /// <param name="genericParameterConstraintToken">The exact GenericParamConstraint token within that module.</param>
    /// <returns>The complete draft row, or null when the portfolio stopped, module is absent, or token was not issued.</returns>
    public MetadataConstraintTargetResolutionIdentity? ExactTargetOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule,
        int genericParameterConstraintToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        if (ResultKind != MetadataConstraintTargetResolutionPortfolioResultKind.Exact)
        {
            return null;
        }
        var entry = entries.FirstOrDefault(candidate => candidate.SourceModule.Equals(sourceModule));
        return entry?.FindTarget(genericParameterConstraintToken);
    }

    /// <summary>Tests canonical equality between two constraint-target draft portfolios.</summary>
    /// <param name="other">The other draft portfolio.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataConstraintTargetResolutionPortfolioIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests constraint-target draft portfolio equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a portfolio with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataConstraintTargetResolutionPortfolioIdentity);

    /// <summary>Computes a deterministic hash code from immutable draft portfolio content.</summary>
    /// <returns>A hash code for this canonical draft portfolio.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);

    private static MetadataConstraintTargetResolutionIdentity ResolveTarget(
        MetadataTypeReferenceResolutionModuleIdentity resolutionEntry,
        ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> typeDefinitions,
        MetadataGenericParameterConstraintTableRowIdentity row)
    {
        var token = row.ConstraintMetadataToken;
        if (CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x02))
        {
            var target = typeDefinitions[CanonicalReplayEncoding.MetadataTokenRowId(token) - 1];
            return MetadataConstraintTargetResolutionIdentity.Create(
                RowMintCapability,
                row,
                MetadataConstraintTargetKind.TypeDefinition,
                resolutionEntry.SourceModule,
                target,
                null,
                null);
        }
        if (CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x01))
        {
            var resolution = resolutionEntry.FindResolution(token)!;
            return resolution.Disposition == MetadataTypeReferenceResolutionDispositionKind.Resolved
                ? MetadataConstraintTargetResolutionIdentity.Create(
                    RowMintCapability,
                    row,
                    MetadataConstraintTargetKind.TypeReference,
                    resolution.TargetModule,
                    resolution.TargetTypeDefinition,
                    resolution,
                    null)
                : MetadataConstraintTargetResolutionIdentity.Create(
                    RowMintCapability,
                    row,
                    MetadataConstraintTargetKind.TypeReferenceUnresolved,
                    null,
                    null,
                    resolution,
                    null);
        }
        var typeSpecificationRow = resolutionEntry.ReferenceTables.TypeSpecifications.FindRow(token)!;
        return MetadataConstraintTargetResolutionIdentity.Create(
            RowMintCapability,
            row,
            MetadataConstraintTargetKind.TypeSpecification,
            null,
            null,
            null,
            typeSpecificationRow);
    }

    private static MetadataConstraintTargetResolutionPortfolioIdentity Exact(
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio,
        ImmutableArray<MetadataConstraintTargetResolutionModuleIdentity> entries) =>
        new(
            MetadataConstraintTargetResolutionPortfolioResultKind.Exact,
            MetadataConstraintTargetResolutionPortfolioIssue.None,
            resolutionPortfolio,
            entries,
            null,
            0,
            null);

    private static MetadataConstraintTargetResolutionPortfolioIdentity Stopped(
        MetadataConstraintTargetResolutionPortfolioResultKind resultKind,
        MetadataConstraintTargetResolutionPortfolioIssue issue,
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio,
        EvaluationDeterministicBound? reachedBound = null,
        int observedCount = 0,
        string? relatedModuleSha256 = null) =>
        new(
            resultKind,
            issue,
            resolutionPortfolio,
            [],
            reachedBound,
            observedCount,
            relatedModuleSha256);
}
