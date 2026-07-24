using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one physical-metadata acquisition outcome for a single module.</summary>
/// <remarks>
/// The producer never repairs, defaults, or fabricates a row. Every non-exact or invalid outcome mirrors the
/// disposition of the already-declared catalog that stopped, or names the physical read that could not be completed.
/// </remarks>
public enum MetadataModuleAcquisitionResultKind
{
    /// <summary>Every per-module catalog was issued exactly from the module's physical tables.</summary>
    Exact = 1,

    /// <summary>A required physical source or catalog stopped before an exact per-module result.</summary>
    NonExact = 2,

    /// <summary>The physical rows or a derived catalog contradicted an already-declared invariant.</summary>
    Invalid = 3,
}

/// <summary>Names the exact acquisition step that produced a non-exact or invalid module outcome.</summary>
/// <remarks>
/// The stage identifies WHICH catalog or physical table stopped. It is deliberately one-to-one with the catalogs the
/// producer issues, so a later host never has to infer the stop site from a message.
/// </remarks>
public enum MetadataModuleAcquisitionStage
{
    /// <summary>No stage applies to an exact acquisition.</summary>
    None = 0,

    /// <summary>The caller-supplied metadata reader seam could not surface a readable metadata image.</summary>
    MetadataImage = 1,

    /// <summary>The exhaustive module-search fact or its derived physical source ends were rejected.</summary>
    SourceEnds = 2,

    /// <summary>The FieldPtr/MethodPtr member-pointer catalog stopped.</summary>
    MemberPointerTable = 3,

    /// <summary>The complete TypeDef table catalog stopped.</summary>
    TypeDefinitionTable = 4,

    /// <summary>The complete NestedClass table catalog stopped.</summary>
    NestedClassTable = 5,

    /// <summary>The complete physical GenericParam table catalog stopped.</summary>
    GenericParameterTable = 6,

    /// <summary>The complete MethodDef table catalog stopped.</summary>
    MethodDefinitionTable = 7,

    /// <summary>The cross-catalog definition-authority join stopped.</summary>
    DefinitionAuthority = 8,

    /// <summary>The GenericParam owner-authority catalog stopped.</summary>
    GenericParameterAuthority = 9,

    /// <summary>The complete physical GenericParamConstraint table catalog stopped.</summary>
    GenericParameterConstraintTable = 10,

    /// <summary>The complete FieldDef table catalog stopped.</summary>
    FieldDefinitionTable = 11,

    /// <summary>The compiler-name mapping catalog stopped.</summary>
    CompilerNameMappingCatalog = 12,

    /// <summary>The W7 TypeDef compatibility catalog stopped.</summary>
    TypeDefinitionCompatibilityCatalog = 13,

    /// <summary>The named-TypeDef chain catalog stopped.</summary>
    NamedTypeDefinitionChainCatalog = 14,

    /// <summary>The reference-table source-end extension was rejected.</summary>
    ReferenceSourceEnds = 15,

    /// <summary>The physical TypeRef table catalog stopped.</summary>
    TypeReferenceTable = 16,

    /// <summary>The physical ModuleRef table catalog stopped.</summary>
    ModuleReferenceTable = 17,

    /// <summary>The physical TypeSpec table catalog stopped.</summary>
    TypeSpecificationTable = 18,

    /// <summary>The physical AssemblyRef table catalog stopped.</summary>
    AssemblyReferenceTable = 19,

    /// <summary>The physical File table catalog stopped.</summary>
    AssemblyFileTable = 20,

    /// <summary>The physical ExportedType table catalog stopped.</summary>
    ExportedTypeTable = 21,

    /// <summary>The six-table module reference table set stopped.</summary>
    ModuleReferenceTableSet = 22,
}

/// <summary>Identifies why one module acquisition stopped at its named stage.</summary>
/// <remarks>
/// The values stay prefix-free: an unreadable image, a physically rejected row, and a catalog-declared stop are three
/// distinct dispositions and never overlap.
/// </remarks>
public enum MetadataModuleAcquisitionIssue
{
    /// <summary>No issue applies to an exact acquisition.</summary>
    None = 0,

    /// <summary>The reader seam threw a physical image fault before any row could be observed.</summary>
    MetadataImageUnreadable = 1,

    /// <summary>An already-declared physical row contract rejected the exact bytes read from the module.</summary>
    PhysicalRowRejected = 2,

    /// <summary>The named catalog returned its own non-exact disposition.</summary>
    CatalogNonExact = 3,

    /// <summary>The named catalog returned its own invalid disposition.</summary>
    CatalogInvalid = 4,
}

/// <summary>Classifies one multi-module authority portfolio acquisition outcome.</summary>
/// <remarks>The portfolio outcome never invents a module; it mirrors the first composed portfolio that stopped.</remarks>
public enum MetadataAuthorityPortfolioResultKind
{
    /// <summary>Every module acquired exactly and every composed portfolio is exact.</summary>
    Exact = 1,

    /// <summary>A module acquisition or a composed portfolio stopped before an exact result.</summary>
    NonExact = 2,

    /// <summary>A module acquisition or a composed portfolio retained contradictory evidence.</summary>
    Invalid = 3,
}

/// <summary>Names the exact composition step that produced a non-exact or invalid portfolio outcome.</summary>
/// <remarks>The stage is one-to-one with the already-declared portfolio contracts the producer composes.</remarks>
public enum MetadataAuthorityPortfolioStage
{
    /// <summary>No stage applies to an exact portfolio.</summary>
    None = 0,

    /// <summary>One requested module stopped during its own per-module acquisition.</summary>
    ModuleAcquisition = 1,

    /// <summary>The multi-module W7 definition-compatibility portfolio stopped.</summary>
    DefinitionCompatibilityPortfolio = 2,

    /// <summary>The multi-module named-TypeDef chain portfolio stopped.</summary>
    NamedTypeDefinitionChainPortfolio = 3,

    /// <summary>The multi-module TypeRef resolution portfolio stopped.</summary>
    TypeReferenceResolutionPortfolio = 4,

    /// <summary>The multi-module ancestry authority portfolio stopped.</summary>
    AncestryAuthorityPortfolio = 5,

    /// <summary>The multi-module constraint-target resolution portfolio stopped.</summary>
    ConstraintTargetResolutionPortfolio = 6,
}

/// <summary>Identifies why one authority portfolio stopped at its named stage.</summary>
/// <remarks>The values stay prefix-free across request-shape faults, module stops, and portfolio dispositions.</remarks>
public enum MetadataAuthorityPortfolioIssue
{
    /// <summary>No issue applies to an exact portfolio.</summary>
    None = 0,

    /// <summary>The supplied request vector was default rather than explicitly initialized.</summary>
    RequestVectorUninitialized = 1,

    /// <summary>An initialized request vector slot contained no request.</summary>
    RequestMissing = 2,

    /// <summary>More than one request named the same exact metadata module.</summary>
    DuplicateSourceModule = 3,

    /// <summary>One requested module stopped during acquisition, so no portfolio was composed.</summary>
    ModuleAcquisitionStopped = 4,

    /// <summary>The named portfolio returned its own non-exact disposition.</summary>
    PortfolioNonExact = 5,

    /// <summary>The named portfolio returned its own invalid disposition.</summary>
    PortfolioInvalid = 6,
}

/// <summary>Identifies one module whose physical metadata tables the producer should acquire.</summary>
/// <remarks>
/// The request pairs one already-established metadata-module identity with a caller-supplied reader seam. The seam is
/// a delegate rather than a path so a host can drive the producer from an open image, a mapped dump range, or a
/// file on disk without this contract owning artifact discovery, file lifetime, or disposal.
/// </remarks>
public sealed class MetadataModuleAcquisitionRequest
{
    private MetadataModuleAcquisitionRequest(
        StaticFieldMetadataModuleIdentity metadataModule,
        Func<MetadataReader> metadataReaderAccessor)
    {
        MetadataModule = metadataModule;
        MetadataReaderAccessor = metadataReaderAccessor;
    }

    /// <summary>Gets the exact metadata module whose physical tables are acquired.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the caller-owned seam returning a reader over that module's complete metadata image.</summary>
    public Func<MetadataReader> MetadataReaderAccessor { get; }

    /// <summary>Creates one single-module acquisition request.</summary>
    /// <param name="metadataModule">
    /// The exact metadata module identity, including its complete counted metadata content, Module row, and containing
    /// assembly. The producer does not re-derive this identity and never mutates it.
    /// </param>
    /// <param name="metadataReaderAccessor">
    /// A caller-owned delegate returning a reader over exactly the metadata image named by
    /// <paramref name="metadataModule"/>. The delegate is invoked once per acquisition and may throw; a throwing seam
    /// becomes an image-unreadable typed stop rather than an exception escaping the producer.
    /// </param>
    /// <returns>A sealed immutable request.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static MetadataModuleAcquisitionRequest Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        Func<MetadataReader> metadataReaderAccessor)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        ArgumentNullException.ThrowIfNull(metadataReaderAccessor);
        return new MetadataModuleAcquisitionRequest(metadataModule, metadataReaderAccessor);
    }
}

/// <summary>Freezes the per-module result of acquiring every W8.2 authority catalog from physical tables.</summary>
/// <remarks>
/// This sealed outcome retains only already-declared catalog identities. It holds no row vector of its own, so
/// every stop is prefix-free by construction: the catalogs reached before the stop are complete identities, the
/// stopping catalog carries its own typed issue, and every later slot is null rather than defaulted.
/// </remarks>
public sealed class MetadataModuleAcquisitionOutcome : IEquatable<MetadataModuleAcquisitionOutcome>
{
    private const string CanonicalDomain = "metadata-v2-module-acquisition-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataModuleAcquisitionOutcome(
        MetadataModuleAcquisitionResultKind resultKind,
        MetadataModuleAcquisitionStage stage,
        MetadataModuleAcquisitionIssue issue,
        StaticFieldMetadataModuleIdentity metadataModule,
        ModuleDraft draft)
    {
        ResultKind = resultKind;
        Stage = stage;
        Issue = issue;
        MetadataModule = metadataModule;
        SourceEnds = draft.SourceEnds;
        MemberPointers = draft.MemberPointers;
        TypeDefinitions = draft.TypeDefinitions;
        NestedClasses = draft.NestedClasses;
        GenericParameters = draft.GenericParameters;
        MethodDefinitions = draft.MethodDefinitions;
        DefinitionAuthority = draft.DefinitionAuthority;
        GenericParameterAuthority = draft.GenericParameterAuthority;
        GenericParameterConstraints = draft.GenericParameterConstraints;
        FieldDefinitions = draft.FieldDefinitions;
        CompilerNameMappings = draft.CompilerNameMappings;
        Compatibility = draft.Compatibility;
        ChainCatalog = draft.ChainCatalog;
        ReferenceSourceEnds = draft.ReferenceSourceEnds;
        TypeReferences = draft.TypeReferences;
        ModuleReferences = draft.ModuleReferences;
        TypeSpecifications = draft.TypeSpecifications;
        AssemblyReferences = draft.AssemblyReferences;
        AssemblyFiles = draft.AssemblyFiles;
        ExportedTypes = draft.ExportedTypes;
        ReferenceTables = draft.ReferenceTables;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        WriteOptionalDigest(writer, SourceEnds?.Sha256);
        WriteOptionalDigest(writer, MemberPointers?.Sha256);
        WriteOptionalDigest(writer, TypeDefinitions?.Sha256);
        WriteOptionalDigest(writer, NestedClasses?.Sha256);
        WriteOptionalDigest(writer, GenericParameters?.Sha256);
        WriteOptionalDigest(writer, MethodDefinitions?.Sha256);
        WriteOptionalDigest(writer, DefinitionAuthority?.Sha256);
        WriteOptionalDigest(writer, GenericParameterAuthority?.Sha256);
        WriteOptionalDigest(writer, GenericParameterConstraints?.Sha256);
        WriteOptionalDigest(writer, FieldDefinitions?.Sha256);
        WriteOptionalDigest(writer, CompilerNameMappings?.Sha256);
        WriteOptionalDigest(writer, Compatibility?.Sha256);
        WriteOptionalDigest(writer, ChainCatalog?.Sha256);
        WriteOptionalDigest(writer, ReferenceSourceEnds?.Sha256);
        WriteOptionalDigest(writer, TypeReferences?.Sha256);
        WriteOptionalDigest(writer, ModuleReferences?.Sha256);
        WriteOptionalDigest(writer, TypeSpecifications?.Sha256);
        WriteOptionalDigest(writer, AssemblyReferences?.Sha256);
        WriteOptionalDigest(writer, AssemblyFiles?.Sha256);
        WriteOptionalDigest(writer, ExportedTypes?.Sha256);
        WriteOptionalDigest(writer, ReferenceTables?.Sha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this per-module acquisition is exact, non-exact, or invalid.</summary>
    public MetadataModuleAcquisitionResultKind ResultKind { get; }

    /// <summary>Gets the acquisition stage that stopped, or none for an exact acquisition.</summary>
    public MetadataModuleAcquisitionStage Stage { get; }

    /// <summary>Gets the typed acquisition issue, or none for an exact acquisition.</summary>
    public MetadataModuleAcquisitionIssue Issue { get; }

    /// <summary>Gets the exact metadata module named by the originating request.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the physically derived source ends, or null when acquisition stopped earlier.</summary>
    public MetadataSourceEndIdentity? SourceEnds { get; }

    /// <summary>Gets the member-pointer catalog, or null when acquisition stopped earlier.</summary>
    public MetadataMemberPointerTableCatalogIdentity? MemberPointers { get; }

    /// <summary>Gets the TypeDef table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataTypeDefinitionTableCatalogIdentity? TypeDefinitions { get; }

    /// <summary>Gets the NestedClass table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataNestedClassTableCatalogIdentity? NestedClasses { get; }

    /// <summary>Gets the physical GenericParam table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataGenericParameterPhysicalTableCatalogIdentity? GenericParameters { get; }

    /// <summary>Gets the MethodDef table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataMethodDefinitionTableCatalogIdentity? MethodDefinitions { get; }

    /// <summary>Gets the definition-authority catalog, or null when acquisition stopped earlier.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity? DefinitionAuthority { get; }

    /// <summary>Gets the GenericParam owner-authority catalog, or null when acquisition stopped earlier.</summary>
    public MetadataGenericParameterAuthorityCatalogIdentity? GenericParameterAuthority { get; }

    /// <summary>Gets the GenericParamConstraint table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataGenericParameterConstraintPhysicalTableCatalogIdentity? GenericParameterConstraints { get; }

    /// <summary>Gets the FieldDef table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataFieldDefinitionTableCatalogIdentity? FieldDefinitions { get; }

    /// <summary>Gets the compiler-name mapping catalog, or null when acquisition stopped earlier.</summary>
    public MetadataCompilerNameMappingCatalogIdentity? CompilerNameMappings { get; }

    /// <summary>Gets the candidate-free W7 compatibility catalog, or null when acquisition stopped earlier.</summary>
    public MetadataW7TypeDefinitionCompatibilityCatalogIdentity? Compatibility { get; }

    /// <summary>Gets the named-TypeDef chain catalog, or null when acquisition stopped earlier.</summary>
    public MetadataNamedTypeDefinitionChainCatalogIdentity? ChainCatalog { get; }

    /// <summary>Gets the reference-table source ends, or null when acquisition stopped earlier.</summary>
    public MetadataReferenceSourceEndIdentity? ReferenceSourceEnds { get; }

    /// <summary>Gets the physical TypeRef table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataTypeReferencePhysicalTableCatalogIdentity? TypeReferences { get; }

    /// <summary>Gets the physical ModuleRef table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataModuleReferencePhysicalTableCatalogIdentity? ModuleReferences { get; }

    /// <summary>Gets the physical TypeSpec table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataTypeSpecificationPhysicalTableCatalogIdentity? TypeSpecifications { get; }

    /// <summary>Gets the physical AssemblyRef table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataAssemblyReferencePhysicalTableCatalogIdentity? AssemblyReferences { get; }

    /// <summary>Gets the physical File table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataAssemblyFilePhysicalTableCatalogIdentity? AssemblyFiles { get; }

    /// <summary>Gets the physical ExportedType table catalog, or null when acquisition stopped earlier.</summary>
    public MetadataExportedTypePhysicalTableCatalogIdentity? ExportedTypes { get; }

    /// <summary>Gets the six-table module reference set, or null when acquisition stopped earlier.</summary>
    public MetadataModuleReferenceTableSetIdentity? ReferenceTables { get; }

    /// <summary>Gets whether every per-module catalog was issued exactly.</summary>
    public bool IsExact => ResultKind == MetadataModuleAcquisitionResultKind.Exact;

    /// <summary>Gets a defensive copy of the versioned canonical outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical outcome bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two per-module acquisition outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataModuleAcquisitionOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests per-module acquisition equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataModuleAcquisitionOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical outcome content.</summary>
    /// <returns>A hash code for this canonical outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataModuleAcquisitionOutcome Create(
        MetadataModuleAcquisitionResultKind resultKind,
        MetadataModuleAcquisitionStage stage,
        MetadataModuleAcquisitionIssue issue,
        StaticFieldMetadataModuleIdentity metadataModule,
        ModuleDraft draft) =>
        new(resultKind, stage, issue, metadataModule, draft);

    private static void WriteOptionalDigest(CanonicalReplayEncoding.Writer writer, string? sha256)
    {
        writer.WriteBoolean(sha256 is not null);
        if (sha256 is not null)
        {
            writer.WriteSha256(sha256, nameof(sha256));
        }
    }

    internal sealed class ModuleDraft
    {
        internal MetadataSourceEndIdentity? SourceEnds { get; set; }
        internal MetadataMemberPointerTableCatalogIdentity? MemberPointers { get; set; }
        internal MetadataTypeDefinitionTableCatalogIdentity? TypeDefinitions { get; set; }
        internal MetadataNestedClassTableCatalogIdentity? NestedClasses { get; set; }
        internal MetadataGenericParameterPhysicalTableCatalogIdentity? GenericParameters { get; set; }
        internal MetadataMethodDefinitionTableCatalogIdentity? MethodDefinitions { get; set; }
        internal MetadataDefinitionAuthorityCatalogIdentity? DefinitionAuthority { get; set; }
        internal MetadataGenericParameterAuthorityCatalogIdentity? GenericParameterAuthority { get; set; }
        internal MetadataGenericParameterConstraintPhysicalTableCatalogIdentity? GenericParameterConstraints
        {
            get;
            set;
        }

        internal MetadataFieldDefinitionTableCatalogIdentity? FieldDefinitions { get; set; }
        internal MetadataCompilerNameMappingCatalogIdentity? CompilerNameMappings { get; set; }
        internal MetadataW7TypeDefinitionCompatibilityCatalogIdentity? Compatibility { get; set; }
        internal MetadataNamedTypeDefinitionChainCatalogIdentity? ChainCatalog { get; set; }
        internal MetadataReferenceSourceEndIdentity? ReferenceSourceEnds { get; set; }
        internal MetadataTypeReferencePhysicalTableCatalogIdentity? TypeReferences { get; set; }
        internal MetadataModuleReferencePhysicalTableCatalogIdentity? ModuleReferences { get; set; }
        internal MetadataTypeSpecificationPhysicalTableCatalogIdentity? TypeSpecifications { get; set; }
        internal MetadataAssemblyReferencePhysicalTableCatalogIdentity? AssemblyReferences { get; set; }
        internal MetadataAssemblyFilePhysicalTableCatalogIdentity? AssemblyFiles { get; set; }
        internal MetadataExportedTypePhysicalTableCatalogIdentity? ExportedTypes { get; set; }
        internal MetadataModuleReferenceTableSetIdentity? ReferenceTables { get; set; }
    }
}

/// <summary>Freezes the multi-module result of composing every W8.2 authority portfolio from real modules.</summary>
/// <remarks>
/// The outcome retains one per-module acquisition outcome for every request plus the already-declared portfolio
/// identities in composition order. Ancestry and constraint-target resolution are siblings over the same exact TypeRef
/// resolution portfolio, so both are retained whenever that prerequisite is exact, and the named stage reports the
/// first sibling that stopped. Every earlier stop leaves later slots null instead of defaulting them.
/// </remarks>
public sealed class MetadataAuthorityPortfolioOutcome : IEquatable<MetadataAuthorityPortfolioOutcome>
{
    private const string CanonicalDomain = "metadata-v2-authority-portfolio-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataModuleAcquisitionOutcome> moduleOutcomes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAuthorityPortfolioOutcome(
        MetadataAuthorityPortfolioResultKind resultKind,
        MetadataAuthorityPortfolioStage stage,
        MetadataAuthorityPortfolioIssue issue,
        ImmutableArray<MetadataModuleAcquisitionOutcome> moduleOutcomes,
        MetadataDefinitionCompatibilityPortfolioIdentity? compatibilityPortfolio,
        MetadataNamedTypeDefinitionChainPortfolioIdentity? chainPortfolio,
        MetadataTypeReferenceResolutionPortfolioIdentity? resolutionPortfolio,
        MetadataAncestryAuthorityPortfolioIdentity? ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity? constraintTargetPortfolio,
        string? relatedModuleSha256)
    {
        ResultKind = resultKind;
        Stage = stage;
        Issue = issue;
        this.moduleOutcomes = ExpressionV2ContractEncoding.Copy(moduleOutcomes);
        CompatibilityPortfolio = compatibilityPortfolio;
        ChainPortfolio = chainPortfolio;
        ResolutionPortfolio = resolutionPortfolio;
        AncestryPortfolio = ancestryPortfolio;
        ConstraintTargetPortfolio = constraintTargetPortfolio;
        RelatedModuleSha256 = relatedModuleSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)issue);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            this.moduleOutcomes,
            static outcome => outcome.CanonicalBytes);
        WriteOptionalDigest(writer, compatibilityPortfolio?.Sha256);
        WriteOptionalDigest(writer, chainPortfolio?.Sha256);
        WriteOptionalDigest(writer, resolutionPortfolio?.Sha256);
        WriteOptionalDigest(writer, ancestryPortfolio?.Sha256);
        WriteOptionalDigest(writer, constraintTargetPortfolio?.Sha256);
        WriteOptionalDigest(writer, relatedModuleSha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this multi-module acquisition is exact, non-exact, or invalid.</summary>
    public MetadataAuthorityPortfolioResultKind ResultKind { get; }

    /// <summary>Gets the composition stage that stopped, or none for an exact portfolio.</summary>
    public MetadataAuthorityPortfolioStage Stage { get; }

    /// <summary>Gets the typed composition issue, or none for an exact portfolio.</summary>
    public MetadataAuthorityPortfolioIssue Issue { get; }

    /// <summary>Gets a defensive request-order copy of every per-module acquisition outcome.</summary>
    public ImmutableArray<MetadataModuleAcquisitionOutcome> ModuleOutcomes =>
        ExpressionV2ContractEncoding.Copy(moduleOutcomes);

    /// <summary>Gets the multi-module W7 compatibility portfolio, or null when composition stopped earlier.</summary>
    public MetadataDefinitionCompatibilityPortfolioIdentity? CompatibilityPortfolio { get; }

    /// <summary>Gets the multi-module named-TypeDef chain portfolio, or null when composition stopped earlier.</summary>
    public MetadataNamedTypeDefinitionChainPortfolioIdentity? ChainPortfolio { get; }

    /// <summary>Gets the multi-module TypeRef resolution portfolio, or null when composition stopped earlier.</summary>
    public MetadataTypeReferenceResolutionPortfolioIdentity? ResolutionPortfolio { get; }

    /// <summary>Gets the multi-module ancestry portfolio, or null when its prerequisite stopped earlier.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity? AncestryPortfolio { get; }

    /// <summary>Gets the multi-module constraint-target portfolio, or null when its prerequisite stopped earlier.</summary>
    public MetadataConstraintTargetResolutionPortfolioIdentity? ConstraintTargetPortfolio { get; }

    /// <summary>Gets the digest of the module whose acquisition stopped, otherwise null.</summary>
    public string? RelatedModuleSha256 { get; }

    /// <summary>Gets whether every module and every composed portfolio is exact.</summary>
    public bool IsExact => ResultKind == MetadataAuthorityPortfolioResultKind.Exact;

    /// <summary>Gets a defensive copy of the versioned canonical portfolio-outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical portfolio-outcome bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two multi-module portfolio outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataAuthorityPortfolioOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests multi-module portfolio equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAuthorityPortfolioOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical portfolio content.</summary>
    /// <returns>A hash code for this canonical portfolio outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataAuthorityPortfolioOutcome Create(
        MetadataAuthorityPortfolioResultKind resultKind,
        MetadataAuthorityPortfolioStage stage,
        MetadataAuthorityPortfolioIssue issue,
        ImmutableArray<MetadataModuleAcquisitionOutcome> moduleOutcomes,
        MetadataDefinitionCompatibilityPortfolioIdentity? compatibilityPortfolio = null,
        MetadataNamedTypeDefinitionChainPortfolioIdentity? chainPortfolio = null,
        MetadataTypeReferenceResolutionPortfolioIdentity? resolutionPortfolio = null,
        MetadataAncestryAuthorityPortfolioIdentity? ancestryPortfolio = null,
        MetadataConstraintTargetResolutionPortfolioIdentity? constraintTargetPortfolio = null,
        string? relatedModuleSha256 = null) =>
        new(
            resultKind,
            stage,
            issue,
            moduleOutcomes,
            compatibilityPortfolio,
            chainPortfolio,
            resolutionPortfolio,
            ancestryPortfolio,
            constraintTargetPortfolio,
            relatedModuleSha256);

    private static void WriteOptionalDigest(CanonicalReplayEncoding.Writer writer, string? sha256)
    {
        writer.WriteBoolean(sha256 is not null);
        if (sha256 is not null)
        {
            writer.WriteSha256(sha256, nameof(sha256));
        }
    }
}

/// <summary>Issues every W8.2 authority catalog for real modules straight from their physical metadata tables.</summary>
/// <remarks>
/// <para>
/// This producer is an acquisition seam and nothing else. It reads exact table row counts from a
/// <see cref="MetadataReader"/>, emits one physical observation per RID in physical order, converts every ECMA coded
/// index back to its raw metadata token, and hands the vectors to the already-declared guarded <c>Create</c> entry
/// points. It performs no binding, no name interpretation, no ancestry walking, and no runtime construction; every
/// disposition, bound, and issue is decided by the catalogs themselves.
/// </para>
/// <para>
/// A producer defect therefore surfaces as an already-declared typed catalog stop or as a typed acquisition stop that
/// names the failing stage. No step substitutes a default row, skips a row, or continues with a partial vector.
/// </para>
/// <para>
/// Three physical columns are not exposed directly by <see cref="MetadataReader"/> and are reconstructed from the
/// ECMA-335 encoding that defines them. The TypeDef FieldList/MethodList and MethodDef ParamList starts are recovered
/// by walking the run-length encoded child ranges in RID order, which reproduces the stored column exactly because an
/// empty run is defined to store the following run's start. The NestedClass rows are emitted by walking TypeDef RIDs
/// ascending, which is the table's required sort order. The File Flags column is reproduced from the single declared
/// ContainsNoMetaData bit. Every other column is read verbatim, and every coded index - ResolutionScope, Extends,
/// Implementation, TypeOrMethodDef owner, and TypeDefOrRef constraint - is converted to its raw metadata token.
/// </para>
/// </remarks>
public static class MetadataAuthorityProducer
{
    /// <summary>Acquires every per-module authority catalog for one real module.</summary>
    /// <param name="request">
    /// The request naming the exact metadata module and the caller-owned reader seam over its metadata image.
    /// </param>
    /// <returns>
    /// An exact per-module outcome retaining every catalog, or the first typed stop naming the stage that could
    /// not be completed exactly.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static MetadataModuleAcquisitionOutcome AcquireModule(MetadataModuleAcquisitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var draft = new MetadataModuleAcquisitionOutcome.ModuleDraft();
        var module = request.MetadataModule;
        MetadataReader reader;
        try
        {
            reader = request.MetadataReaderAccessor();
        }
        catch (Exception exception) when (IsImageFault(exception))
        {
            return Stopped(
                MetadataModuleAcquisitionResultKind.NonExact,
                MetadataModuleAcquisitionStage.MetadataImage,
                MetadataModuleAcquisitionIssue.MetadataImageUnreadable,
                module,
                draft);
        }

        if (reader is null)
        {
            return Stopped(
                MetadataModuleAcquisitionResultKind.NonExact,
                MetadataModuleAcquisitionStage.MetadataImage,
                MetadataModuleAcquisitionIssue.MetadataImageUnreadable,
                module,
                draft);
        }

        var stage = MetadataModuleAcquisitionStage.SourceEnds;
        try
        {
            return AcquireModuleCore(module, reader, draft, ref stage);
        }
        catch (Exception exception) when (IsImageFault(exception))
        {
            return Stopped(
                MetadataModuleAcquisitionResultKind.NonExact,
                stage,
                MetadataModuleAcquisitionIssue.MetadataImageUnreadable,
                module,
                draft);
        }
        catch (Exception exception) when (IsRowFault(exception))
        {
            return Stopped(
                MetadataModuleAcquisitionResultKind.Invalid,
                stage,
                MetadataModuleAcquisitionIssue.PhysicalRowRejected,
                module,
                draft);
        }
    }

    /// <summary>Acquires every requested module and composes every multi-module authority portfolio.</summary>
    /// <param name="requests">
    /// The explicitly initialized request vector. Each entry names one distinct exact metadata module; the composition
    /// order of every portfolio follows this vector.
    /// </param>
    /// <returns>
    /// An exact portfolio outcome retaining the compatibility, chain, resolution, ancestry, and constraint-target
    /// portfolios plus every per-module outcome, or the first typed stop naming the stage that could not be composed.
    /// </returns>
    public static MetadataAuthorityPortfolioOutcome AcquirePortfolio(
        ImmutableArray<MetadataModuleAcquisitionRequest> requests)
    {
        if (requests.IsDefault)
        {
            return MetadataAuthorityPortfolioOutcome.Create(
                MetadataAuthorityPortfolioResultKind.NonExact,
                MetadataAuthorityPortfolioStage.ModuleAcquisition,
                MetadataAuthorityPortfolioIssue.RequestVectorUninitialized,
                []);
        }
        if (requests.Any(static request => request is null))
        {
            return MetadataAuthorityPortfolioOutcome.Create(
                MetadataAuthorityPortfolioResultKind.NonExact,
                MetadataAuthorityPortfolioStage.ModuleAcquisition,
                MetadataAuthorityPortfolioIssue.RequestMissing,
                []);
        }

        var seenModules = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            if (!seenModules.Add(request.MetadataModule.Sha256))
            {
                return MetadataAuthorityPortfolioOutcome.Create(
                    MetadataAuthorityPortfolioResultKind.Invalid,
                    MetadataAuthorityPortfolioStage.ModuleAcquisition,
                    MetadataAuthorityPortfolioIssue.DuplicateSourceModule,
                    [],
                    relatedModuleSha256: request.MetadataModule.Sha256);
            }
        }

        var outcomes = ImmutableArray.CreateBuilder<MetadataModuleAcquisitionOutcome>(requests.Length);
        foreach (var request in requests)
        {
            outcomes.Add(AcquireModule(request));
        }

        var moduleOutcomes = outcomes.MoveToImmutable();
        foreach (var outcome in moduleOutcomes)
        {
            if (!outcome.IsExact)
            {
                return MetadataAuthorityPortfolioOutcome.Create(
                    outcome.ResultKind == MetadataModuleAcquisitionResultKind.Invalid
                        ? MetadataAuthorityPortfolioResultKind.Invalid
                        : MetadataAuthorityPortfolioResultKind.NonExact,
                    MetadataAuthorityPortfolioStage.ModuleAcquisition,
                    MetadataAuthorityPortfolioIssue.ModuleAcquisitionStopped,
                    moduleOutcomes,
                    relatedModuleSha256: outcome.MetadataModule.Sha256);
            }
        }

        var compatibilityPortfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [.. moduleOutcomes.Select(static outcome => outcome.Compatibility!)]);
        if (compatibilityPortfolio.ResultKind != MetadataDefinitionCompatibilityPortfolioResultKind.Exact)
        {
            return MetadataAuthorityPortfolioOutcome.Create(
                compatibilityPortfolio.ResultKind == MetadataDefinitionCompatibilityPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioResultKind.Invalid
                    : MetadataAuthorityPortfolioResultKind.NonExact,
                MetadataAuthorityPortfolioStage.DefinitionCompatibilityPortfolio,
                compatibilityPortfolio.ResultKind == MetadataDefinitionCompatibilityPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioIssue.PortfolioInvalid
                    : MetadataAuthorityPortfolioIssue.PortfolioNonExact,
                moduleOutcomes,
                compatibilityPortfolio);
        }

        var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [.. moduleOutcomes.Select(static outcome => outcome.ChainCatalog!)]);
        if (chainPortfolio.ResultKind != MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact)
        {
            return MetadataAuthorityPortfolioOutcome.Create(
                chainPortfolio.ResultKind == MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioResultKind.Invalid
                    : MetadataAuthorityPortfolioResultKind.NonExact,
                MetadataAuthorityPortfolioStage.NamedTypeDefinitionChainPortfolio,
                chainPortfolio.ResultKind == MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioIssue.PortfolioInvalid
                    : MetadataAuthorityPortfolioIssue.PortfolioNonExact,
                moduleOutcomes,
                compatibilityPortfolio,
                chainPortfolio);
        }

        var resolutionPortfolio = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            chainPortfolio,
            [.. moduleOutcomes.Select(static outcome => outcome.ReferenceTables!)]);
        if (resolutionPortfolio.ResultKind != MetadataTypeReferenceResolutionPortfolioResultKind.Exact)
        {
            return MetadataAuthorityPortfolioOutcome.Create(
                resolutionPortfolio.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioResultKind.Invalid
                    : MetadataAuthorityPortfolioResultKind.NonExact,
                MetadataAuthorityPortfolioStage.TypeReferenceResolutionPortfolio,
                resolutionPortfolio.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioIssue.PortfolioInvalid
                    : MetadataAuthorityPortfolioIssue.PortfolioNonExact,
                moduleOutcomes,
                compatibilityPortfolio,
                chainPortfolio,
                resolutionPortfolio);
        }

        var ancestryPortfolio = MetadataAncestryAuthorityPortfolioIdentity.Create(resolutionPortfolio);
        var constraintTargetPortfolio = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            resolutionPortfolio,
            [.. moduleOutcomes.Select(static outcome => outcome.GenericParameterConstraints!)]);

        if (ancestryPortfolio.ResultKind != MetadataAncestryAuthorityPortfolioResultKind.Exact)
        {
            return MetadataAuthorityPortfolioOutcome.Create(
                ancestryPortfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioResultKind.Invalid
                    : MetadataAuthorityPortfolioResultKind.NonExact,
                MetadataAuthorityPortfolioStage.AncestryAuthorityPortfolio,
                ancestryPortfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioIssue.PortfolioInvalid
                    : MetadataAuthorityPortfolioIssue.PortfolioNonExact,
                moduleOutcomes,
                compatibilityPortfolio,
                chainPortfolio,
                resolutionPortfolio,
                ancestryPortfolio,
                constraintTargetPortfolio);
        }
        if (constraintTargetPortfolio.ResultKind != MetadataConstraintTargetResolutionPortfolioResultKind.Exact)
        {
            return MetadataAuthorityPortfolioOutcome.Create(
                constraintTargetPortfolio.ResultKind == MetadataConstraintTargetResolutionPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioResultKind.Invalid
                    : MetadataAuthorityPortfolioResultKind.NonExact,
                MetadataAuthorityPortfolioStage.ConstraintTargetResolutionPortfolio,
                constraintTargetPortfolio.ResultKind == MetadataConstraintTargetResolutionPortfolioResultKind.Invalid
                    ? MetadataAuthorityPortfolioIssue.PortfolioInvalid
                    : MetadataAuthorityPortfolioIssue.PortfolioNonExact,
                moduleOutcomes,
                compatibilityPortfolio,
                chainPortfolio,
                resolutionPortfolio,
                ancestryPortfolio,
                constraintTargetPortfolio);
        }

        return MetadataAuthorityPortfolioOutcome.Create(
            MetadataAuthorityPortfolioResultKind.Exact,
            MetadataAuthorityPortfolioStage.None,
            MetadataAuthorityPortfolioIssue.None,
            moduleOutcomes,
            compatibilityPortfolio,
            chainPortfolio,
            resolutionPortfolio,
            ancestryPortfolio,
            constraintTargetPortfolio);
    }

    /// <summary>Acquires one manifest metadata-module identity from physical Module and Assembly rows.</summary>
    /// <param name="moduleInstance">
    /// The caller-owned physical placement of the loaded module. A file on disk carries no runtime placement, so the
    /// producer never invents one.
    /// </param>
    /// <param name="metadataReader">A reader over exactly the metadata image described by the placement.</param>
    /// <param name="metadataImageBytes">
    /// The complete ECMA-335 metadata image beginning at its metadata root, hashed to derive the counted content
    /// identity together with the physical Module row MVID.
    /// </param>
    /// <returns>An exact manifest-module identity derived only from physical Module and Assembly rows.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="moduleInstance"/> or <paramref name="metadataReader"/> is null.</exception>
    /// <exception cref="ArgumentException">The image bytes are empty or a physical row failed its declared contract.</exception>
    public static StaticFieldMetadataModuleIdentity AcquireManifestModuleIdentity(
        StaticFieldModuleInstanceIdentity moduleInstance,
        MetadataReader metadataReader,
        ImmutableArray<byte> metadataImageBytes)
    {
        ArgumentNullException.ThrowIfNull(moduleInstance);
        ArgumentNullException.ThrowIfNull(metadataReader);
        if (metadataImageBytes.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "An initialized non-empty metadata image is required.",
                nameof(metadataImageBytes));
        }

        var moduleRow = metadataReader.GetModuleDefinition();
        var mvid = metadataReader.GetGuid(moduleRow.Mvid);
        var content = ModuleContentIdentity.FromMetadata(mvid, metadataImageBytes.AsSpan());
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            moduleRow.Generation,
            metadataReader.GetString(moduleRow.Name),
            mvid,
            metadataReader.GetGuid(moduleRow.GenerationId),
            metadataReader.GetGuid(moduleRow.BaseGenerationId));
        var assemblyRow = metadataReader.GetAssemblyDefinition();
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            metadataReader.GetString(assemblyRow.Name),
            assemblyRow.Version.Major,
            assemblyRow.Version.Minor,
            assemblyRow.Version.Build,
            assemblyRow.Version.Revision,
            metadataReader.GetString(assemblyRow.Culture),
            (int)assemblyRow.Flags,
            (int)assemblyRow.HashAlgorithm,
            ReadBlob(metadataReader, assemblyRow.PublicKey));
        return StaticFieldMetadataModuleIdentity.ForManifestModule(
            moduleInstance,
            content,
            moduleDefinition,
            StaticFieldContainingAssemblyIdentity.Create(
                moduleInstance,
                content,
                moduleDefinition,
                assemblyDefinition));
    }

    private static MetadataModuleAcquisitionOutcome AcquireModuleCore(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        MetadataModuleAcquisitionOutcome.ModuleDraft draft,
        ref MetadataModuleAcquisitionStage stage)
    {
        var typeDefinitionRowCount = reader.GetTableRowCount(TableIndex.TypeDef);
        var fieldDefinitionRowCount = reader.GetTableRowCount(TableIndex.Field);
        var methodDefinitionRowCount = reader.GetTableRowCount(TableIndex.MethodDef);
        var typeReferenceRowCount = reader.GetTableRowCount(TableIndex.TypeRef);
        var typeSpecificationRowCount = reader.GetTableRowCount(TableIndex.TypeSpec);
        var assemblyReferenceRowCount = reader.GetTableRowCount(TableIndex.AssemblyRef);
        var moduleReferenceRowCount = reader.GetTableRowCount(TableIndex.ModuleRef);
        var fileRowCount = reader.GetTableRowCount(TableIndex.File);
        var exportedTypeRowCount = reader.GetTableRowCount(TableIndex.ExportedType);
        var nestedClassRowCount = reader.GetTableRowCount(TableIndex.NestedClass);
        var genericParameterRowCount = reader.GetTableRowCount(TableIndex.GenericParam);
        var genericParameterConstraintRowCount = reader.GetTableRowCount(TableIndex.GenericParamConstraint);
        var fieldPointerRowCount = reader.GetTableRowCount(TableIndex.FieldPtr);
        var methodPointerRowCount = reader.GetTableRowCount(TableIndex.MethodPtr);
        var parameterPointerRowCount = reader.GetTableRowCount(TableIndex.ParamPtr);

        stage = MetadataModuleAcquisitionStage.SourceEnds;
        var searchFact = StaticFieldModuleSearchFact.Exact(
            module: module.Module,
            moduleContent: module.ModuleContent,
            typeDefinitionsExamined: typeDefinitionRowCount,
            fieldDefinitionsExamined: fieldDefinitionRowCount,
            typeDefinitionRowCount: typeDefinitionRowCount,
            fieldDefinitionRowCount: fieldDefinitionRowCount,
            typeReferenceRowCount: typeReferenceRowCount,
            typeSpecificationRowCount: typeSpecificationRowCount,
            assemblyReferenceRowCount: assemblyReferenceRowCount,
            methodDefinitionRowCount: methodDefinitionRowCount,
            parameterDefinitionRowCount: reader.GetTableRowCount(TableIndex.Param),
            propertyDefinitionRowCount: reader.GetTableRowCount(TableIndex.Property),
            eventDefinitionRowCount: reader.GetTableRowCount(TableIndex.Event),
            moduleDefinitionRowCount: reader.GetTableRowCount(TableIndex.Module),
            assemblyDefinitionRowCount: reader.GetTableRowCount(TableIndex.Assembly),
            interfaceImplementationRowCount: reader.GetTableRowCount(TableIndex.InterfaceImpl),
            memberReferenceRowCount: reader.GetTableRowCount(TableIndex.MemberRef),
            customAttributeRowCount: reader.GetTableRowCount(TableIndex.CustomAttribute),
            moduleReferenceRowCount: moduleReferenceRowCount,
            fileRowCount: fileRowCount,
            exportedTypeRowCount: exportedTypeRowCount,
            nestedClassRowCount: nestedClassRowCount,
            genericParameterRowCount: genericParameterRowCount,
            genericParameterConstraintRowCount: genericParameterConstraintRowCount,
            fieldPointerRowCount: fieldPointerRowCount,
            methodPointerRowCount: methodPointerRowCount,
            parameterPointerRowCount: parameterPointerRowCount);
        var sourceEnds = MetadataSourceEndIdentity.Create(module, searchFact);
        draft.SourceEnds = sourceEnds;

        stage = MetadataModuleAcquisitionStage.MemberPointerTable;
        var memberPointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        draft.MemberPointers = memberPointers;
        if (memberPointers.ResultKind != MetadataMemberPointerTableResultKind.Exact)
        {
            return CatalogStopped(
                memberPointers.ResultKind == MetadataMemberPointerTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.TypeDefinitionTable;
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            ReadTypeDefinitionRows(module, reader, typeDefinitionRowCount),
            memberPointers);
        draft.TypeDefinitions = typeDefinitions;
        if (typeDefinitions.ResultKind != MetadataTypeDefinitionTableResultKind.Exact)
        {
            return CatalogStopped(
                typeDefinitions.ResultKind == MetadataTypeDefinitionTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.NestedClassTable;
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            ReadNestedClassRows(module, reader, typeDefinitionRowCount));
        draft.NestedClasses = nestedClasses;
        if (nestedClasses.ResultKind != MetadataNestedClassTableResultKind.Exact)
        {
            return CatalogStopped(
                nestedClasses.ResultKind == MetadataNestedClassTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.GenericParameterTable;
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            ReadGenericParameterRows(module, reader, genericParameterRowCount));
        draft.GenericParameters = genericParameters;
        if (genericParameters.ResultKind != MetadataGenericParameterPhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                genericParameters.ResultKind == MetadataGenericParameterPhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.MethodDefinitionTable;
        var methodDefinitions = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            ReadMethodDefinitionRows(module, reader, methodDefinitionRowCount));
        draft.MethodDefinitions = methodDefinitions;
        if (methodDefinitions.ResultKind != MetadataMethodDefinitionTableResultKind.Exact)
        {
            return CatalogStopped(
                methodDefinitions.ResultKind == MetadataMethodDefinitionTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.DefinitionAuthority;
        var definitionAuthority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions);
        draft.DefinitionAuthority = definitionAuthority;
        if (definitionAuthority.ResultKind != MetadataDefinitionAuthorityResultKind.Exact)
        {
            return CatalogStopped(
                definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.GenericParameterAuthority;
        var genericParameterAuthority =
            MetadataGenericParameterAuthorityCatalogIdentity.Create(definitionAuthority);
        draft.GenericParameterAuthority = genericParameterAuthority;
        if (genericParameterAuthority.ResultKind != MetadataGenericParameterProofResultKind.Exact)
        {
            return CatalogStopped(
                genericParameterAuthority.ResultKind == MetadataGenericParameterProofResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.GenericParameterConstraintTable;
        var constraints = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            genericParameterAuthority,
            ReadGenericParameterConstraintRows(module, reader, genericParameterConstraintRowCount));
        draft.GenericParameterConstraints = constraints;
        if (constraints.ResultKind != MetadataGenericParameterConstraintPhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                constraints.ResultKind == MetadataGenericParameterConstraintPhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.FieldDefinitionTable;
        var fieldDefinitions = MetadataFieldDefinitionTableCatalogIdentity.Create(
            definitionAuthority,
            ReadFieldDefinitionRows(module, reader, fieldDefinitionRowCount));
        draft.FieldDefinitions = fieldDefinitions;
        if (fieldDefinitions.ResultKind != MetadataFieldDefinitionTableResultKind.Exact)
        {
            return CatalogStopped(
                fieldDefinitions.ResultKind == MetadataFieldDefinitionTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.CompilerNameMappingCatalog;
        var compilerNameMappings = MetadataCompilerNameMappingCatalogIdentity.Create(definitionAuthority);
        draft.CompilerNameMappings = compilerNameMappings;
        if (compilerNameMappings.ResultKind != MetadataCompilerNameMappingCatalogResultKind.Exact)
        {
            return CatalogStopped(
                compilerNameMappings.ResultKind == MetadataCompilerNameMappingCatalogResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.TypeDefinitionCompatibilityCatalog;
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            definitionAuthority,
            CandidateFreeSlots(definitionAuthority.TypeDefinitions.Length));
        draft.Compatibility = compatibility;
        if (compatibility.ResultKind != MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact)
        {
            return CatalogStopped(
                compatibility.ResultKind == MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.NamedTypeDefinitionChainCatalog;
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            compatibility,
            compilerNameMappings);
        draft.ChainCatalog = chainCatalog;
        if (chainCatalog.ResultKind != MetadataNamedTypeDefinitionChainCatalogResultKind.Exact)
        {
            return CatalogStopped(
                chainCatalog.ResultKind == MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.ReferenceSourceEnds;
        var referenceSourceEnds = MetadataReferenceSourceEndIdentity.Create(sourceEnds);
        draft.ReferenceSourceEnds = referenceSourceEnds;

        stage = MetadataModuleAcquisitionStage.TypeReferenceTable;
        var typeReferences = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            referenceSourceEnds,
            ReadTypeReferenceRows(module, reader, typeReferenceRowCount));
        draft.TypeReferences = typeReferences;
        if (typeReferences.ResultKind != MetadataReferencePhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                typeReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.ModuleReferenceTable;
        var moduleReferences = MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
            referenceSourceEnds,
            ReadModuleReferenceRows(module, reader, moduleReferenceRowCount));
        draft.ModuleReferences = moduleReferences;
        if (moduleReferences.ResultKind != MetadataReferencePhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                moduleReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.TypeSpecificationTable;
        var typeSpecifications = MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
            referenceSourceEnds,
            ReadTypeSpecificationRows(module, reader, typeSpecificationRowCount));
        draft.TypeSpecifications = typeSpecifications;
        if (typeSpecifications.ResultKind != MetadataReferencePhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                typeSpecifications.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.AssemblyReferenceTable;
        var assemblyReferences = MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(
            referenceSourceEnds,
            ReadAssemblyReferenceRows(module, reader, assemblyReferenceRowCount));
        draft.AssemblyReferences = assemblyReferences;
        if (assemblyReferences.ResultKind != MetadataReferencePhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                assemblyReferences.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.AssemblyFileTable;
        var assemblyFiles = MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
            referenceSourceEnds,
            ReadAssemblyFileRows(module, reader, fileRowCount));
        draft.AssemblyFiles = assemblyFiles;
        if (assemblyFiles.ResultKind != MetadataReferencePhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                assemblyFiles.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.ExportedTypeTable;
        var exportedTypes = MetadataExportedTypePhysicalTableCatalogIdentity.Create(
            referenceSourceEnds,
            ReadExportedTypeRows(module, reader, exportedTypeRowCount));
        draft.ExportedTypes = exportedTypes;
        if (exportedTypes.ResultKind != MetadataReferencePhysicalTableResultKind.Exact)
        {
            return CatalogStopped(
                exportedTypes.ResultKind == MetadataReferencePhysicalTableResultKind.Invalid,
                stage,
                module,
                draft);
        }

        stage = MetadataModuleAcquisitionStage.ModuleReferenceTableSet;
        var referenceTables = MetadataModuleReferenceTableSetIdentity.Create(
            referenceSourceEnds,
            typeReferences,
            moduleReferences,
            typeSpecifications,
            assemblyReferences,
            assemblyFiles,
            exportedTypes);
        draft.ReferenceTables = referenceTables;
        if (!referenceTables.AllTablesExact)
        {
            return CatalogStopped(referenceTables.AnyTableInvalid, stage, module, draft);
        }

        return MetadataModuleAcquisitionOutcome.Create(
            MetadataModuleAcquisitionResultKind.Exact,
            MetadataModuleAcquisitionStage.None,
            MetadataModuleAcquisitionIssue.None,
            module,
            draft);
    }

    private static ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> ReadTypeDefinitionRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(rowCount);
        var nextFieldListRowId = 1;
        var nextMethodListRowId = 1;
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var typeDefinition = reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(rowId));
            var fieldListRowId = ReadListStart(typeDefinition.GetFields(), ref nextFieldListRowId);
            var methodListRowId = ReadListStart(typeDefinition.GetMethods(), ref nextMethodListRowId);
            builder.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(MetadataTokens.TypeDefinitionHandle(rowId)),
                fieldListRowId,
                methodListRowId,
                reader.GetString(typeDefinition.Namespace),
                reader.GetString(typeDefinition.Name),
                (int)typeDefinition.Attributes,
                typeDefinition.BaseType.IsNil ? null : MetadataTokens.GetToken(typeDefinition.BaseType)));
        }

        return builder.MoveToImmutable();
    }

    private static int ReadListStart(FieldDefinitionHandleCollection fields, ref int nextRowId)
    {
        var start = nextRowId;
        var count = 0;
        foreach (var handle in fields)
        {
            if (count == 0)
            {
                start = MetadataTokens.GetRowNumber(handle);
            }
            count++;
        }

        nextRowId = checked(start + count);
        return start;
    }

    private static int ReadListStart(MethodDefinitionHandleCollection methods, ref int nextRowId)
    {
        var start = nextRowId;
        var count = 0;
        foreach (var handle in methods)
        {
            if (count == 0)
            {
                start = MetadataTokens.GetRowNumber(handle);
            }
            count++;
        }

        nextRowId = checked(start + count);
        return start;
    }

    private static int ReadListStart(ParameterHandleCollection parameters, ref int nextRowId)
    {
        var start = nextRowId;
        var count = 0;
        foreach (var handle in parameters)
        {
            if (count == 0)
            {
                start = MetadataTokens.GetRowNumber(handle);
            }
            count++;
        }

        nextRowId = checked(start + count);
        return start;
    }

    private static ImmutableArray<MetadataNestedClassRowObservationIdentity> ReadNestedClassRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int typeDefinitionRowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataNestedClassRowObservationIdentity>();
        for (var rowId = 1; rowId <= typeDefinitionRowCount; rowId++)
        {
            var handle = MetadataTokens.TypeDefinitionHandle(rowId);
            var declaringType = reader.GetTypeDefinition(handle).GetDeclaringType();
            if (declaringType.IsNil)
            {
                continue;
            }

            builder.Add(MetadataNestedClassRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(MetadataTokens.Handle(
                    TableIndex.NestedClass,
                    checked(builder.Count + 1))),
                MetadataTokens.GetToken(handle),
                MetadataTokens.GetToken(declaringType)));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<MetadataGenericParameterRowObservationIdentity> ReadGenericParameterRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.GenericParameterHandle(rowId);
            var genericParameter = reader.GetGenericParameter(handle);
            builder.Add(MetadataGenericParameterRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                genericParameter.Index,
                (int)genericParameter.Attributes,
                MetadataTokens.GetToken(genericParameter.Parent),
                reader.GetString(genericParameter.Name)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity>
        ReadGenericParameterConstraintRows(
            StaticFieldMetadataModuleIdentity module,
            MetadataReader reader,
            int rowCount)
    {
        var builder =
            ImmutableArray.CreateBuilder<MetadataGenericParameterConstraintRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.GenericParameterConstraintHandle(rowId);
            var constraint = reader.GetGenericParameterConstraint(handle);
            builder.Add(MetadataGenericParameterConstraintRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                MetadataTokens.GetToken(constraint.Parameter),
                MetadataTokens.GetToken(constraint.Type)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataMethodDefinitionRowObservationIdentity> ReadMethodDefinitionRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataMethodDefinitionRowObservationIdentity>(rowCount);
        var nextParameterListRowId = 1;
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.MethodDefinitionHandle(rowId);
            var methodDefinition = reader.GetMethodDefinition(handle);
            var parameterListRowId = ReadListStart(methodDefinition.GetParameters(), ref nextParameterListRowId);
            var signature = reader.GetBlobBytes(methodDefinition.Signature);
            var prefixLength = Math.Min(
                signature.Length,
                MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount + 1);
            builder.Add(MetadataMethodDefinitionRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                methodDefinition.RelativeVirtualAddress,
                (int)methodDefinition.ImplAttributes,
                (int)methodDefinition.Attributes,
                reader.GetString(methodDefinition.Name),
                ImmutableArray.Create(signature, 0, prefixLength),
                signature.Length,
                parameterListRowId));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataFieldDefinitionRowObservationIdentity> ReadFieldDefinitionRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataFieldDefinitionRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.FieldDefinitionHandle(rowId);
            var fieldDefinition = reader.GetFieldDefinition(handle);
            builder.Add(MetadataFieldDefinitionRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                (int)fieldDefinition.Attributes,
                reader.GetString(fieldDefinition.Name),
                ReadBlob(reader, fieldDefinition.Signature)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataTypeReferenceRowObservationIdentity> ReadTypeReferenceRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataTypeReferenceRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.TypeReferenceHandle(rowId);
            var typeReference = reader.GetTypeReference(handle);
            builder.Add(MetadataTypeReferenceRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                reader.GetString(typeReference.Namespace),
                reader.GetString(typeReference.Name),
                MetadataTokens.GetToken(typeReference.ResolutionScope)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataModuleReferenceRowObservationIdentity> ReadModuleReferenceRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataModuleReferenceRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.ModuleReferenceHandle(rowId);
            builder.Add(MetadataModuleReferenceRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                reader.GetString(reader.GetModuleReference(handle).Name)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataTypeSpecificationRowObservationIdentity> ReadTypeSpecificationRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataTypeSpecificationRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.TypeSpecificationHandle(rowId);
            builder.Add(MetadataTypeSpecificationRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                ReadBlob(reader, reader.GetTypeSpecification(handle).Signature)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity> ReadAssemblyReferenceRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataAssemblyReferenceRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.AssemblyReferenceHandle(rowId);
            var assemblyReference = reader.GetAssemblyReference(handle);
            builder.Add(MetadataAssemblyReferenceRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                reader.GetString(assemblyReference.Name),
                assemblyReference.Version.Major,
                assemblyReference.Version.Minor,
                assemblyReference.Version.Build,
                assemblyReference.Version.Revision,
                reader.GetString(assemblyReference.Culture),
                (int)assemblyReference.Flags,
                ReadBlob(reader, assemblyReference.PublicKeyOrToken),
                ReadBlob(reader, assemblyReference.HashValue)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataAssemblyFileRowObservationIdentity> ReadAssemblyFileRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        const int containsNoMetadataFlag = 0x0000_0001;
        var builder = ImmutableArray.CreateBuilder<MetadataAssemblyFileRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.AssemblyFileHandle(rowId);
            var assemblyFile = reader.GetAssemblyFile(handle);
            builder.Add(MetadataAssemblyFileRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                assemblyFile.ContainsMetadata ? 0 : containsNoMetadataFlag,
                reader.GetString(assemblyFile.Name),
                ReadBlob(reader, assemblyFile.HashValue)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataExportedTypeRowObservationIdentity> ReadExportedTypeRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataReader reader,
        int rowCount)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataExportedTypeRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var handle = MetadataTokens.ExportedTypeHandle(rowId);
            var exportedType = reader.GetExportedType(handle);
            builder.Add(MetadataExportedTypeRowObservationIdentity.Create(
                module,
                MetadataTokens.GetToken(handle),
                (int)exportedType.Attributes,
                exportedType.GetTypeDefinitionId(),
                reader.GetString(exportedType.Namespace),
                reader.GetString(exportedType.Name),
                MetadataTokens.GetToken(exportedType.Implementation)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<byte> ReadBlob(MetadataReader reader, BlobHandle handle) =>
        handle.IsNil ? ImmutableArray<byte>.Empty : [.. reader.GetBlobBytes(handle)];

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity?> CandidateFreeSlots(int count)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity?>(count);
        for (var index = 0; index < count; index++)
        {
            builder.Add(null);
        }

        return builder.MoveToImmutable();
    }

    private static MetadataModuleAcquisitionOutcome CatalogStopped(
        bool isInvalid,
        MetadataModuleAcquisitionStage stage,
        StaticFieldMetadataModuleIdentity module,
        MetadataModuleAcquisitionOutcome.ModuleDraft draft) =>
        MetadataModuleAcquisitionOutcome.Create(
            isInvalid
                ? MetadataModuleAcquisitionResultKind.Invalid
                : MetadataModuleAcquisitionResultKind.NonExact,
            stage,
            isInvalid
                ? MetadataModuleAcquisitionIssue.CatalogInvalid
                : MetadataModuleAcquisitionIssue.CatalogNonExact,
            module,
            draft);

    private static MetadataModuleAcquisitionOutcome Stopped(
        MetadataModuleAcquisitionResultKind resultKind,
        MetadataModuleAcquisitionStage stage,
        MetadataModuleAcquisitionIssue issue,
        StaticFieldMetadataModuleIdentity module,
        MetadataModuleAcquisitionOutcome.ModuleDraft draft) =>
        MetadataModuleAcquisitionOutcome.Create(resultKind, stage, issue, module, draft);

    private static bool IsImageFault(Exception exception) =>
        exception is BadImageFormatException or IOException or ObjectDisposedException
            or UnauthorizedAccessException or NotSupportedException;

    private static bool IsRowFault(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or OverflowException
            or IndexOutOfRangeException or KeyNotFoundException;
}
