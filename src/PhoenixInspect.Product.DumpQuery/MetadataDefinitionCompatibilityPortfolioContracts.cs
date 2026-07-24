using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one normalized multi-module draft compatibility portfolio.</summary>
/// <remarks>
/// Exact means every retained module has one exact W7 comparison catalog in one snapshot. Non-exact and invalid
/// results retain no entry prefix.
/// </remarks>
public enum MetadataDefinitionCompatibilityPortfolioResultKind
{
    /// <summary>The initialized draft vector normalized into one complete module portfolio.</summary>
    Exact = 1,

    /// <summary>A missing prerequisite or declared draft bound prevented complete normalization.</summary>
    NonExact = 2,

    /// <summary>Complete draft inputs contradicted one another.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one multi-module draft compatibility portfolio.</summary>
/// <remarks>
/// Issue precedence is vector initialization, module count, missing entry, non-exact catalog, invalid catalog,
/// snapshot disagreement, then duplicate module. This precedence is independent of caller order.
/// </remarks>
public enum MetadataDefinitionCompatibilityPortfolioIssue
{
    /// <summary>No issue applies to an exact draft portfolio.</summary>
    None = 0,

    /// <summary>The supplied compatibility-catalog vector was default rather than explicitly initialized.</summary>
    CatalogVectorUninitialized = 1,

    /// <summary>The supplied vector reached the shared module-count draft bound plus one.</summary>
    ModuleCountBoundReached = 2,

    /// <summary>An initialized vector slot did not contain a compatibility catalog.</summary>
    CompatibilityCatalogMissing = 3,

    /// <summary>At least one compatibility catalog stopped non-exactly.</summary>
    CompatibilityCatalogNonExact = 4,

    /// <summary>At least one compatibility catalog was invalid.</summary>
    CompatibilityCatalogInvalid = 5,

    /// <summary>Exact compatibility catalogs name more than one dump snapshot.</summary>
    SnapshotMismatch = 6,

    /// <summary>More than one exact compatibility catalog names the same exact metadata module.</summary>
    DuplicateSourceModule = 7,
}

/// <summary>Freezes one guarded fixed-reference entry in a normalized draft compatibility portfolio.</summary>
/// <remarks>
/// The entry retains the exact catalog for later row lookup, but its canonical form contains only fixed-size snapshot,
/// module, definition-authority, and compatibility-catalog digests. It neither copies nor reinterprets row outcomes.
/// Only a complete portfolio can mint entries.
/// </remarks>
public sealed class MetadataDefinitionCompatibilityPortfolioEntryIdentity :
    IEquatable<MetadataDefinitionCompatibilityPortfolioEntryIdentity>
{
    private const string CanonicalDomain = "metadata-v2-definition-compatibility-portfolio-entry";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataDefinitionCompatibilityPortfolioEntryIdentity(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog)
    {
        CompatibilityCatalog = compatibilityCatalog;
        DefinitionAuthority = compatibilityCatalog.DefinitionAuthority;
        SourceEnds = DefinitionAuthority.SourceEnds;
        SourceModule = SourceEnds.SourceModule;
        SnapshotSha256 = SourceModule.Module.SnapshotSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(SnapshotSha256, nameof(SnapshotSha256));
        writer.WriteSha256(SourceModule.Sha256, nameof(SourceModule));
        writer.WriteSha256(DefinitionAuthority.Sha256, nameof(DefinitionAuthority));
        writer.WriteSha256(CompatibilityCatalog.Sha256, nameof(CompatibilityCatalog));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact draft W7 comparison catalog retained without row reinterpretation.</summary>
    public MetadataW7TypeDefinitionCompatibilityCatalogIdentity CompatibilityCatalog { get; }

    /// <summary>Gets the exact draft definition authority referenced by the compatibility catalog.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets the exact draft physical source ends shared by the authority and its issued rows.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the exact draft metadata module used as the portfolio key.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the lowercase complete dump-snapshot digest shared by the draft portfolio.</summary>
    public string SnapshotSha256 { get; }

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft entry.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of this canonical draft entry.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two fixed-reference draft portfolio entries.</summary>
    /// <param name="other">The other draft portfolio entry.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataDefinitionCompatibilityPortfolioEntryIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft portfolio-entry equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an entry with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataDefinitionCompatibilityPortfolioEntryIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft entry content.</summary>
    /// <returns>A hash code for this fixed-reference draft entry.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataDefinitionCompatibilityPortfolioEntryIdentity Create(
        object mintCapability,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog)
    {
        if (!MetadataDefinitionCompatibilityPortfolioIdentity.OwnsEntryMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A compatibility portfolio entry requires the complete portfolio's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(compatibilityCatalog);
        if (compatibilityCatalog.ResultKind != MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact)
        {
            throw new ArgumentException(
                "A compatibility portfolio entry requires one exact comparison catalog.",
                nameof(compatibilityCatalog));
        }

        return new MetadataDefinitionCompatibilityPortfolioEntryIdentity(compatibilityCatalog);
    }
}

/// <summary>Normalizes exact per-module comparison catalogs into one snapshot-scoped draft portfolio.</summary>
/// <remarks>
/// Caller order is discarded in favor of exact metadata-module canonical order. Every entry retains its original
/// comparison catalog, including compatible, absent, and mismatched row outcomes. The portfolio requires catalog
/// exactness but makes no new row-level claim. Every stop is prefix-free.
/// </remarks>
public sealed class MetadataDefinitionCompatibilityPortfolioIdentity :
    IEquatable<MetadataDefinitionCompatibilityPortfolioIdentity>
{
    private const string CanonicalDomain = "metadata-v2-definition-compatibility-portfolio";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object EntryMintCapability = new();
    private readonly ImmutableArray<MetadataDefinitionCompatibilityPortfolioEntryIdentity> entries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataDefinitionCompatibilityPortfolioIdentity(
        MetadataDefinitionCompatibilityPortfolioResultKind resultKind,
        MetadataDefinitionCompatibilityPortfolioIssue issue,
        string? snapshotSha256,
        ImmutableArray<MetadataDefinitionCompatibilityPortfolioEntryIdentity> entries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken,
        string? relatedModuleSha256,
        string? relatedCatalogSha256)
    {
        ResultKind = resultKind;
        Issue = issue;
        SnapshotSha256 = snapshotSha256;
        this.entries = ExpressionV2ContractEncoding.Copy(entries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;
        RelatedModuleSha256 = relatedModuleSha256;
        RelatedCatalogSha256 = relatedCatalogSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, snapshotSha256);
        writer.WriteInt32(entries.Length);
        foreach (var entry in entries)
        {
            writer.WriteSha256(entry.Sha256, nameof(entries));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, relatedModuleSha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, relatedCatalogSha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the shared draft module-count cap.</summary>
    public const int MaximumModuleCount = ExpressionV2ContractLimits.MaximumModuleCount;

    /// <summary>Gets whether this normalized draft portfolio is exact, non-exact, or invalid.</summary>
    public MetadataDefinitionCompatibilityPortfolioResultKind ResultKind { get; }

    /// <summary>Gets the typed draft portfolio issue, or none for an exact portfolio.</summary>
    public MetadataDefinitionCompatibilityPortfolioIssue Issue { get; }

    /// <summary>Gets the exact portfolio's common snapshot digest, or null for an empty portfolio or any stop.</summary>
    public string? SnapshotSha256 { get; }

    /// <summary>Gets a defensive module-canonical-order copy of exact draft entries, or an empty array for a stop.</summary>
    public ImmutableArray<MetadataDefinitionCompatibilityPortfolioEntryIdentity> Entries =>
        ExpressionV2ContractEncoding.Copy(entries);

    /// <summary>Gets the shared module-count draft bound only for a cap-plus-one result.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied or cap-plus-one draft observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a prerequisite issue-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets an issue-related metadata-module digest, otherwise null.</summary>
    public string? RelatedModuleSha256 { get; }

    /// <summary>Gets an issue-related compatibility-catalog digest, otherwise null.</summary>
    public string? RelatedCatalogSha256 { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft portfolio bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft portfolio bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one normalized snapshot-scoped draft compatibility portfolio.</summary>
    /// <param name="compatibilityCatalogs">
    /// An explicitly initialized vector containing at most one exact comparison catalog per exact metadata module.
    /// </param>
    /// <returns>
    /// An exact module-canonical draft portfolio, a prefix-free non-exact stop, or a prefix-free invalid result.
    /// </returns>
    public static MetadataDefinitionCompatibilityPortfolioIdentity Create(
        ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity> compatibilityCatalogs)
    {
        if (compatibilityCatalogs.IsDefault)
        {
            return Stopped(
                MetadataDefinitionCompatibilityPortfolioResultKind.NonExact,
                MetadataDefinitionCompatibilityPortfolioIssue.CatalogVectorUninitialized,
                observedCount: 0);
        }
        if (compatibilityCatalogs.Length > MaximumModuleCount)
        {
            return Stopped(
                MetadataDefinitionCompatibilityPortfolioResultKind.NonExact,
                MetadataDefinitionCompatibilityPortfolioIssue.ModuleCountBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ModuleCountBoundName,
                    MaximumModuleCount),
                MaximumModuleCount + 1);
        }
        if (compatibilityCatalogs.IsEmpty)
        {
            return Exact(snapshotSha256: null, []);
        }

        for (var index = 0; index < compatibilityCatalogs.Length; index++)
        {
            if (compatibilityCatalogs[index] is null)
            {
                return Stopped(
                    MetadataDefinitionCompatibilityPortfolioResultKind.NonExact,
                    MetadataDefinitionCompatibilityPortfolioIssue.CompatibilityCatalogMissing,
                    observedCount: compatibilityCatalogs.Length);
            }
        }

        var pending = new PendingCatalog[compatibilityCatalogs.Length];
        for (var index = 0; index < compatibilityCatalogs.Length; index++)
        {
            var catalog = compatibilityCatalogs[index];
            pending[index] = new PendingCatalog(
                catalog,
                catalog.DefinitionAuthority.SourceEnds.SourceModule.CanonicalBytes);
        }
        Array.Sort(pending, PendingCatalogComparer.Instance);

        foreach (var item in pending)
        {
            if (item.Catalog.ResultKind == MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact)
            {
                return PrerequisiteStopped(
                    MetadataDefinitionCompatibilityPortfolioResultKind.NonExact,
                    MetadataDefinitionCompatibilityPortfolioIssue.CompatibilityCatalogNonExact,
                    item.Catalog);
            }
        }
        foreach (var item in pending)
        {
            if (item.Catalog.ResultKind == MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Invalid)
            {
                return PrerequisiteStopped(
                    MetadataDefinitionCompatibilityPortfolioResultKind.Invalid,
                    MetadataDefinitionCompatibilityPortfolioIssue.CompatibilityCatalogInvalid,
                    item.Catalog);
            }
        }

        var snapshotSha256 = pending[0].Catalog.DefinitionAuthority.SourceEnds.SourceModule.Module.SnapshotSha256;
        for (var index = 1; index < pending.Length; index++)
        {
            var catalog = pending[index].Catalog;
            if (!StringComparer.Ordinal.Equals(
                    snapshotSha256,
                    catalog.DefinitionAuthority.SourceEnds.SourceModule.Module.SnapshotSha256))
            {
                return CatalogConflict(
                    MetadataDefinitionCompatibilityPortfolioResultKind.Invalid,
                    MetadataDefinitionCompatibilityPortfolioIssue.SnapshotMismatch,
                    compatibilityCatalogs.Length,
                    catalog);
            }
        }
        for (var index = 1; index < pending.Length; index++)
        {
            if (pending[index - 1].Catalog.DefinitionAuthority.SourceEnds.SourceModule.Equals(
                    pending[index].Catalog.DefinitionAuthority.SourceEnds.SourceModule))
            {
                return CatalogConflict(
                    MetadataDefinitionCompatibilityPortfolioResultKind.Invalid,
                    MetadataDefinitionCompatibilityPortfolioIssue.DuplicateSourceModule,
                    compatibilityCatalogs.Length,
                    pending[index].Catalog);
            }
        }

        var entries = ImmutableArray.CreateBuilder<MetadataDefinitionCompatibilityPortfolioEntryIdentity>(
            pending.Length);
        foreach (var item in pending)
        {
            entries.Add(MetadataDefinitionCompatibilityPortfolioEntryIdentity.Create(
                EntryMintCapability,
                item.Catalog));
        }
        return Exact(snapshotSha256, entries.MoveToImmutable());
    }

    /// <summary>Tests canonical equality between two normalized draft compatibility portfolios.</summary>
    /// <param name="other">The other draft portfolio.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataDefinitionCompatibilityPortfolioIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft compatibility-portfolio equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a portfolio with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataDefinitionCompatibilityPortfolioIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft portfolio content.</summary>
    /// <returns>A hash code for this normalized draft portfolio.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataDefinitionCompatibilityPortfolioEntryIdentity? ExactEntryOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        return ResultKind == MetadataDefinitionCompatibilityPortfolioResultKind.Exact
            ? entries.FirstOrDefault(entry => entry.SourceModule.Equals(sourceModule))
            : null;
    }

    internal MetadataDefinitionCompatibilityPortfolioEntryIdentity? ExactEntryOrDefault(
        MetadataSourceEndIdentity sourceEnds)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        return ResultKind == MetadataDefinitionCompatibilityPortfolioResultKind.Exact
            ? entries.FirstOrDefault(entry => entry.SourceEnds.Equals(sourceEnds))
            : null;
    }

    internal static bool OwnsEntryMintCapability(object? capability) =>
        ReferenceEquals(capability, EntryMintCapability);

    private static MetadataDefinitionCompatibilityPortfolioIdentity Exact(
        string? snapshotSha256,
        ImmutableArray<MetadataDefinitionCompatibilityPortfolioEntryIdentity> entries) =>
        new(
            MetadataDefinitionCompatibilityPortfolioResultKind.Exact,
            MetadataDefinitionCompatibilityPortfolioIssue.None,
            snapshotSha256,
            entries,
            null,
            0,
            null,
            null,
            null);

    private static MetadataDefinitionCompatibilityPortfolioIdentity PrerequisiteStopped(
        MetadataDefinitionCompatibilityPortfolioResultKind resultKind,
        MetadataDefinitionCompatibilityPortfolioIssue issue,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity catalog) =>
        Stopped(
            resultKind,
            issue,
            catalog.ReachedBound,
            catalog.ObservedCount,
            catalog.RelatedMetadataToken,
            catalog.DefinitionAuthority.SourceEnds.SourceModule.Sha256,
            catalog.Sha256);

    private static MetadataDefinitionCompatibilityPortfolioIdentity CatalogConflict(
        MetadataDefinitionCompatibilityPortfolioResultKind resultKind,
        MetadataDefinitionCompatibilityPortfolioIssue issue,
        int observedCount,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity catalog) =>
        Stopped(
            resultKind,
            issue,
            observedCount: observedCount,
            relatedModuleSha256: catalog.DefinitionAuthority.SourceEnds.SourceModule.Sha256,
            relatedCatalogSha256: catalog.Sha256);

    private static MetadataDefinitionCompatibilityPortfolioIdentity Stopped(
        MetadataDefinitionCompatibilityPortfolioResultKind resultKind,
        MetadataDefinitionCompatibilityPortfolioIssue issue,
        EvaluationDeterministicBound? reachedBound = null,
        int observedCount = 0,
        int? relatedMetadataToken = null,
        string? relatedModuleSha256 = null,
        string? relatedCatalogSha256 = null) =>
        new(
            resultKind,
            issue,
            null,
            [],
            reachedBound,
            observedCount,
            relatedMetadataToken,
            relatedModuleSha256,
            relatedCatalogSha256);

    private readonly record struct PendingCatalog(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Catalog,
        ImmutableArray<byte> ModuleCanonicalBytes);

    private sealed class PendingCatalogComparer : IComparer<PendingCatalog>
    {
        internal static PendingCatalogComparer Instance { get; } = new();

        public int Compare(PendingCatalog left, PendingCatalog right)
        {
            var moduleComparison = left.ModuleCanonicalBytes.AsSpan().SequenceCompareTo(
                right.ModuleCanonicalBytes.AsSpan());
            return moduleComparison != 0
                ? moduleComparison
                : StringComparer.Ordinal.Compare(left.Catalog.Sha256, right.Catalog.Sha256);
        }
    }
}
