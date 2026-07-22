using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies one authority-derived named-TypeDef-chain draft catalog.</summary>
/// <remarks>Every non-exact or invalid result is prefix-free and exposes no chain identities.</remarks>
public enum MetadataNamedTypeDefinitionChainCatalogResultKind
{
    /// <summary>Every authority TypeDef has one complete outer-to-inner draft chain.</summary>
    Exact = 1,

    /// <summary>A prerequisite or declared draft bound prevented complete issuance.</summary>
    NonExact = 2,

    /// <summary>Complete draft inputs contradicted one another.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one named-TypeDef-chain draft catalog.</summary>
/// <remarks>
/// Compiler spelling and C# addressability dispositions are retained row facts, not catalog failures. The issue
/// values describe missing prerequisites, cross-catalog disagreement, or an impossible authority graph.
/// </remarks>
public enum MetadataNamedTypeDefinitionChainCatalogIssue
{
    /// <summary>No issue applies to an exact draft catalog.</summary>
    None = 0,

    /// <summary>The retained W7 compatibility-catalog prerequisite was non-exact.</summary>
    CompatibilityCatalogNonExact = 1,

    /// <summary>The retained W7 compatibility-catalog prerequisite was invalid.</summary>
    CompatibilityCatalogInvalid = 2,

    /// <summary>The retained compiler-name mapping-catalog prerequisite was non-exact.</summary>
    CompilerMappingCatalogNonExact = 3,

    /// <summary>The retained compiler-name mapping-catalog prerequisite was invalid.</summary>
    CompilerMappingCatalogInvalid = 4,

    /// <summary>The compatibility and compiler-name catalogs retained different definition authorities.</summary>
    DefinitionAuthorityMismatch = 5,

    /// <summary>The authority view did not contain exactly the authority-issued TypeDefs in RID order.</summary>
    AuthorityViewShapeMismatch = 6,

    /// <summary>An authority TypeDef named an enclosing TypeDef absent from the complete authority view.</summary>
    EnclosingTypeDefinitionMissing = 7,

    /// <summary>An enclosing-TypeDef traversal revisited one physical TypeDef token.</summary>
    EnclosingTypeDefinitionCycle = 8,

    /// <summary>An enclosing-TypeDef traversal reached the declared depth bound plus one.</summary>
    EnclosingTypeDefinitionDepthBoundReached = 9,

    /// <summary>A derived parent position disagreed with the authority row's declared parent token or depth.</summary>
    EnclosingTypeDefinitionRelationMismatch = 10,

    /// <summary>The complete compiler-name mapping catalog did not contain the authority TypeDef's mapping row.</summary>
    CompilerMappingMissing = 11,

    /// <summary>A compiler-name mapping row named a different authority TypeDef or source.</summary>
    CompilerMappingAuthorityMismatch = 12,
}

/// <summary>Freezes one authority and compiler-name segment of a named-TypeDef draft chain.</summary>
/// <remarks>
/// This guarded draft identity retains physical namespace and metadata-name text even when its compiler projection is
/// noncanonical, not addressable, or non-exact. Its canonical form contains fixed SHA-256 references to the source
/// authority and mapping rows rather than recursively embedding them.
/// </remarks>
public sealed class MetadataNamedTypeDefinitionChainSegmentIdentity :
    IEquatable<MetadataNamedTypeDefinitionChainSegmentIdentity>
{
    private const string CanonicalDomain = "metadata-v2-named-typedef-chain-segment";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNamedTypeDefinitionChainSegmentIdentity(
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        MetadataCompilerNameMappingIdentity compilerNameMapping)
    {
        TypeDefinition = typeDefinition;
        CompilerNameMapping = compilerNameMapping;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(typeDefinition.Sha256, nameof(typeDefinition));
        writer.WriteSha256(compilerNameMapping.Sha256, nameof(compilerNameMapping));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact authority-issued TypeDef retained by this draft chain segment.</summary>
    public MetadataTypeDefinitionAuthorityIdentity TypeDefinition { get; }

    /// <summary>Gets the exact token-correlated compiler-name draft mapping retained by this segment.</summary>
    public MetadataCompilerNameMappingIdentity CompilerNameMapping { get; }

    /// <summary>Gets the exact metadata source ends shared by the segment's authority and mapping rows.</summary>
    public MetadataSourceEndIdentity SourceEnds => TypeDefinition.SourceEnds;

    /// <summary>Gets the exact non-nil TypeDef token.</summary>
    public int TypeDefinitionToken => TypeDefinition.TypeDefinitionToken;

    /// <summary>Gets the exact enclosing TypeDef token, or null for a top-level segment.</summary>
    public int? EnclosingTypeDefinitionToken => TypeDefinition.EnclosingTypeDefinitionToken;

    /// <summary>Gets the exact parent-link count from this segment to its top-level root.</summary>
    public int NestingDepth => TypeDefinition.NestingDepth;

    /// <summary>Gets the unchanged decoded physical namespace, including an empty namespace.</summary>
    public string RawNamespaceName => TypeDefinition.NamespaceName;

    /// <summary>Gets the unchanged decoded physical metadata name.</summary>
    public string RawMetadataName => TypeDefinition.TypeName;

    /// <summary>Gets the complete physical GenericParam arity, including redeclared enclosing positions.</summary>
    public int TotalGenericArity => TypeDefinition.TotalGenericArity;

    /// <summary>Gets the parent-relative arity, or null when the retained mapping could not derive it.</summary>
    public int? IntroducedGenericArity => CompilerNameMapping.IntroducedGenericArity;

    /// <summary>Gets the Roslyn projection, or null when the retained mapping stopped non-exactly.</summary>
    public MetadataRoslynNameProjectionIdentity? RoslynProjection => CompilerNameMapping.RoslynProjection;

    /// <summary>Gets the C# simple-name disposition, or null when the retained mapping stopped non-exactly.</summary>
    public MetadataCSharpSimpleNameAddressabilityIdentity? CSharpAddressability =>
        CompilerNameMapping.CSharpAddressability;

    /// <summary>Gets whether this exact authority row is the ECMA module pseudo-type.</summary>
    public bool IsModulePseudoType => TypeDefinitionToken == 0x0200_0001;

    /// <summary>
    /// Gets whether this non-module draft segment has an exact ordinary C# simple-name spelling.
    /// </summary>
    public bool CanAppearInCSharpNamedType =>
        !IsModulePseudoType &&
        CompilerNameMapping.ResultKind == MetadataCompilerNameMappingResultKind.Exact &&
        CSharpAddressability?.IsAddressable == true;

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft segment.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft segment.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two named-TypeDef-chain draft segments.</summary>
    /// <param name="other">The other draft segment.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNamedTypeDefinitionChainSegmentIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests named-TypeDef-chain draft segment equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a segment with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataNamedTypeDefinitionChainSegmentIdentity);

    /// <summary>Computes a deterministic hash code from immutable named-TypeDef-chain draft segment content.</summary>
    /// <returns>A hash code for this canonical draft segment.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataNamedTypeDefinitionChainSegmentIdentity Create(
        object mintCapability,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        MetadataCompilerNameMappingIdentity compilerNameMapping)
    {
        if (!MetadataNamedTypeDefinitionChainCatalogIdentity.OwnsSegmentMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A named-TypeDef-chain segment requires the complete chain catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(typeDefinition);
        ArgumentNullException.ThrowIfNull(compilerNameMapping);
        if (!typeDefinition.Equals(compilerNameMapping.TypeDefinition) ||
            !typeDefinition.SourceEnds.Equals(compilerNameMapping.TypeDefinition.SourceEnds))
        {
            throw new ArgumentException(
                "A named-TypeDef-chain segment requires one exact authority-correlated mapping row.",
                nameof(compilerNameMapping));
        }

        return new MetadataNamedTypeDefinitionChainSegmentIdentity(typeDefinition, compilerNameMapping);
    }
}

/// <summary>Freezes one complete outer-to-inner named-TypeDef draft chain.</summary>
/// <remarks>
/// The chain is issued only by a complete catalog. Segment order, parent relations, arities, and the final TypeDef are
/// all authority-derived. The caller supplies none of those scalar claims.
/// </remarks>
public sealed class MetadataNamedTypeDefinitionChainIdentity :
    IEquatable<MetadataNamedTypeDefinitionChainIdentity>
{
    private const string CanonicalDomain = "metadata-v2-named-typedef-chain";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataNamedTypeDefinitionChainSegmentIdentity> segments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNamedTypeDefinitionChainIdentity(
        ImmutableArray<MetadataNamedTypeDefinitionChainSegmentIdentity> segments)
    {
        this.segments = ExpressionV2ContractEncoding.Copy(segments);
        FinalSegment = segments[^1];

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(segments.Length);
        foreach (var segment in segments)
        {
            writer.WriteSha256(segment.Sha256, nameof(segments));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets a defensive outer-to-inner copy of all authority-derived draft segments.</summary>
    public ImmutableArray<MetadataNamedTypeDefinitionChainSegmentIdentity> Segments =>
        ExpressionV2ContractEncoding.Copy(segments);

    /// <summary>Gets the innermost draft segment, which determines the catalog lookup key.</summary>
    public MetadataNamedTypeDefinitionChainSegmentIdentity FinalSegment { get; }

    /// <summary>Gets the final authority-issued TypeDef derived from the last segment.</summary>
    public MetadataTypeDefinitionAuthorityIdentity FinalTypeDefinition => FinalSegment.TypeDefinition;

    /// <summary>Gets the exact metadata source ends shared by every segment.</summary>
    public MetadataSourceEndIdentity SourceEnds => FinalSegment.SourceEnds;

    /// <summary>Gets the final non-nil TypeDef token used by exact catalog lookup.</summary>
    public int FinalTypeDefinitionToken => FinalSegment.TypeDefinitionToken;

    /// <summary>Gets whether this chain identifies the module pseudo-type rather than a named source type.</summary>
    public bool IsModulePseudoType => FinalSegment.IsModulePseudoType;

    /// <summary>Gets whether every segment has an exact C# simple-name spelling and the final row is not the module row.</summary>
    public bool CanAppearInCSharpNamedType =>
        !IsModulePseudoType && segments.All(static segment => segment.CanAppearInCSharpNamedType);

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft chain.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft chain.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two complete named-TypeDef draft chains.</summary>
    /// <param name="other">The other draft chain.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNamedTypeDefinitionChainIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete named-TypeDef draft chain equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a chain with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataNamedTypeDefinitionChainIdentity);

    /// <summary>Computes a deterministic hash code from immutable named-TypeDef draft chain content.</summary>
    /// <returns>A hash code for this canonical draft chain.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataNamedTypeDefinitionChainIdentity Create(
        object mintCapability,
        ImmutableArray<MetadataNamedTypeDefinitionChainSegmentIdentity> segments)
    {
        if (!MetadataNamedTypeDefinitionChainCatalogIdentity.OwnsChainMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A named-TypeDef chain requires the complete chain catalog's private mint capability.",
                nameof(mintCapability));
        }
        if (segments.IsDefaultOrEmpty || segments.Any(static segment => segment is null))
        {
            throw new ArgumentException("A named-TypeDef chain requires initialized segments.", nameof(segments));
        }

        var sourceEnds = segments[0].SourceEnds;
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var expectedParent = index == 0 ? (int?)null : segments[index - 1].TypeDefinitionToken;
            if (!segment.SourceEnds.Equals(sourceEnds) ||
                segment.NestingDepth != index ||
                segment.EnclosingTypeDefinitionToken != expectedParent)
            {
                throw new ArgumentException(
                    "Named-TypeDef chain segments must retain one exact outer-to-inner authority path.",
                    nameof(segments));
            }
        }

        return new MetadataNamedTypeDefinitionChainIdentity(segments);
    }
}

/// <summary>Freezes all authority-derived named-TypeDef draft chains for one metadata module.</summary>
/// <remarks>
/// The exact catalog retains W7 row comparison outcomes only as lineage. Candidate-absent and mismatch outcomes do
/// not override physical authority. Compiler mapping rows are selected by the catalog from the same exact authority.
/// </remarks>
public sealed class MetadataNamedTypeDefinitionChainCatalogIdentity :
    IEquatable<MetadataNamedTypeDefinitionChainCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-named-typedef-chain-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object SegmentMintCapability = new();
    private static readonly object ChainMintCapability = new();
    private readonly ImmutableArray<MetadataNamedTypeDefinitionChainIdentity> chains;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNamedTypeDefinitionChainCatalogIdentity(
        MetadataNamedTypeDefinitionChainCatalogResultKind resultKind,
        MetadataNamedTypeDefinitionChainCatalogIssue issue,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog,
        MetadataCompilerNameMappingCatalogIdentity compilerNameMappingCatalog,
        ImmutableArray<MetadataNamedTypeDefinitionChainIdentity> chains,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        CompatibilityCatalog = compatibilityCatalog;
        CompilerNameMappingCatalog = compilerNameMappingCatalog;
        this.chains = ExpressionV2ContractEncoding.Copy(chains);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(compatibilityCatalog.Sha256, nameof(compatibilityCatalog));
        writer.WriteSha256(compilerNameMappingCatalog.Sha256, nameof(compilerNameMappingCatalog));
        writer.WriteInt32(chains.Length);
        foreach (var chain in chains)
        {
            writer.WriteSha256(chain.Sha256, nameof(chains));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this complete named-TypeDef-chain draft catalog is exact, non-exact, or invalid.</summary>
    public MetadataNamedTypeDefinitionChainCatalogResultKind ResultKind { get; }

    /// <summary>Gets the typed draft catalog issue, or none for an exact catalog.</summary>
    public MetadataNamedTypeDefinitionChainCatalogIssue Issue { get; }

    /// <summary>Gets the retained per-TypeDef W7 comparison draft lineage.</summary>
    public MetadataW7TypeDefinitionCompatibilityCatalogIdentity CompatibilityCatalog { get; }

    /// <summary>Gets the retained authority-bound compiler-name draft mapping catalog.</summary>
    public MetadataCompilerNameMappingCatalogIdentity CompilerNameMappingCatalog { get; }

    /// <summary>Gets the definition authority shared by both exact draft prerequisites.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority =>
        CompatibilityCatalog.DefinitionAuthority;

    /// <summary>Gets the exact metadata source ends retained by the definition authority.</summary>
    public MetadataSourceEndIdentity SourceEnds => DefinitionAuthority.SourceEnds;

    /// <summary>Gets a defensive TypeDef-RID-order copy of all exact draft chains, or an empty array for a stop.</summary>
    public ImmutableArray<MetadataNamedTypeDefinitionChainIdentity> Chains =>
        ExpressionV2ContractEncoding.Copy(chains);

    /// <summary>Gets the exact one-segment module pseudo-type draft chain, or null for a stopped catalog.</summary>
    public MetadataNamedTypeDefinitionChainIdentity? ModulePseudoTypeChain =>
        ResultKind == MetadataNamedTypeDefinitionChainCatalogResultKind.Exact && !chains.IsEmpty
            ? chains[0]
            : null;

    /// <summary>Gets the declared depth bound only when a defensive traversal reached cap plus one.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related draft row, chain-depth, or prerequisite observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the issue-related TypeDef token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft catalog.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft catalog.</summary>
    public string Sha256 { get; }

    /// <summary>Creates every outer-to-inner named-TypeDef draft chain for one exact authority.</summary>
    /// <param name="compatibilityCatalog">The exact W7 comparison catalog retained only as lineage.</param>
    /// <param name="compilerNameMappingCatalog">The complete compiler-name mappings for the same authority.</param>
    /// <returns>An exact complete draft catalog or a prefix-free typed stop.</returns>
    public static MetadataNamedTypeDefinitionChainCatalogIdentity Create(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog,
        MetadataCompilerNameMappingCatalogIdentity compilerNameMappingCatalog)
    {
        ArgumentNullException.ThrowIfNull(compatibilityCatalog);
        ArgumentNullException.ThrowIfNull(compilerNameMappingCatalog);
        var nodes = compatibilityCatalog.DefinitionAuthority.TypeDefinitions.Select(static typeDefinition =>
            new MetadataNamedTypeDefinitionChainAuthorityNode(
                typeDefinition,
                typeDefinition.EnclosingTypeDefinitionToken,
                typeDefinition.NestingDepth)).ToImmutableArray();
        return CreateCore(compatibilityCatalog, compilerNameMappingCatalog, nodes);
    }

    /// <summary>Looks up one exact named-TypeDef draft chain by its non-nil TypeDef token.</summary>
    /// <param name="typeDefinitionToken">The exact TypeDef token in this catalog's module.</param>
    /// <returns>The exact chain, or null when the catalog stopped or the token is not an issued TypeDef.</returns>
    public MetadataNamedTypeDefinitionChainIdentity? ExactChainOrDefault(int typeDefinitionToken)
    {
        if (ResultKind != MetadataNamedTypeDefinitionChainCatalogResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(typeDefinitionToken, 0x02))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinitionToken);
        return rowId > 0 && rowId <= chains.Length ? chains[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two named-TypeDef-chain draft catalogs.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNamedTypeDefinitionChainCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests named-TypeDef-chain draft catalog equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataNamedTypeDefinitionChainCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable named-TypeDef-chain draft catalog content.</summary>
    /// <returns>A hash code for this canonical draft catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataNamedTypeDefinitionChainCatalogIdentity CreateWithAuthorityNodes(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog,
        MetadataCompilerNameMappingCatalogIdentity compilerNameMappingCatalog,
        ImmutableArray<MetadataNamedTypeDefinitionChainAuthorityNode> authorityNodes)
    {
        ArgumentNullException.ThrowIfNull(compatibilityCatalog);
        ArgumentNullException.ThrowIfNull(compilerNameMappingCatalog);
        return CreateCore(compatibilityCatalog, compilerNameMappingCatalog, authorityNodes);
    }

    internal static bool OwnsSegmentMintCapability(object? capability) =>
        ReferenceEquals(capability, SegmentMintCapability);

    internal static bool OwnsChainMintCapability(object? capability) =>
        ReferenceEquals(capability, ChainMintCapability);

    private static MetadataNamedTypeDefinitionChainCatalogIdentity CreateCore(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog,
        MetadataCompilerNameMappingCatalogIdentity compilerNameMappingCatalog,
        ImmutableArray<MetadataNamedTypeDefinitionChainAuthorityNode> authorityNodes)
    {
        if (compatibilityCatalog.ResultKind == MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact)
        {
            return PrerequisiteStopped(
                MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
                MetadataNamedTypeDefinitionChainCatalogIssue.CompatibilityCatalogNonExact,
                compatibilityCatalog,
                compilerNameMappingCatalog,
                compatibilityCatalog.ReachedBound,
                compatibilityCatalog.ObservedCount,
                compatibilityCatalog.RelatedMetadataToken);
        }
        if (compatibilityCatalog.ResultKind == MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Invalid)
        {
            return PrerequisiteStopped(
                MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                MetadataNamedTypeDefinitionChainCatalogIssue.CompatibilityCatalogInvalid,
                compatibilityCatalog,
                compilerNameMappingCatalog,
                compatibilityCatalog.ReachedBound,
                compatibilityCatalog.ObservedCount,
                compatibilityCatalog.RelatedMetadataToken);
        }
        if (compilerNameMappingCatalog.ResultKind == MetadataCompilerNameMappingCatalogResultKind.NonExact)
        {
            return PrerequisiteStopped(
                MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
                MetadataNamedTypeDefinitionChainCatalogIssue.CompilerMappingCatalogNonExact,
                compatibilityCatalog,
                compilerNameMappingCatalog,
                compilerNameMappingCatalog.ReachedBound,
                compilerNameMappingCatalog.ObservedCount,
                compilerNameMappingCatalog.RelatedMetadataToken);
        }
        if (compilerNameMappingCatalog.ResultKind == MetadataCompilerNameMappingCatalogResultKind.Invalid)
        {
            return PrerequisiteStopped(
                MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                MetadataNamedTypeDefinitionChainCatalogIssue.CompilerMappingCatalogInvalid,
                compatibilityCatalog,
                compilerNameMappingCatalog,
                compilerNameMappingCatalog.ReachedBound,
                compilerNameMappingCatalog.ObservedCount,
                compilerNameMappingCatalog.RelatedMetadataToken);
        }
        if (!compatibilityCatalog.DefinitionAuthority.Equals(compilerNameMappingCatalog.DefinitionAuthority))
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                MetadataNamedTypeDefinitionChainCatalogIssue.DefinitionAuthorityMismatch,
                compatibilityCatalog,
                compilerNameMappingCatalog,
                observedCount: compilerNameMappingCatalog.Mappings.Length);
        }

        var authorityTypes = compatibilityCatalog.DefinitionAuthority.TypeDefinitions;
        if (authorityNodes.IsDefault || authorityNodes.Length != authorityTypes.Length)
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                MetadataNamedTypeDefinitionChainCatalogIssue.AuthorityViewShapeMismatch,
                compatibilityCatalog,
                compilerNameMappingCatalog,
                observedCount: authorityNodes.IsDefault ? 0 : authorityNodes.Length);
        }

        var nodesByToken = new Dictionary<int, MetadataNamedTypeDefinitionChainAuthorityNode>(authorityNodes.Length);
        for (var index = 0; index < authorityNodes.Length; index++)
        {
            var node = authorityNodes[index];
            if (node.TypeDefinition is null ||
                !node.TypeDefinition.Equals(authorityTypes[index]) ||
                !nodesByToken.TryAdd(node.TypeDefinition.TypeDefinitionToken, node))
            {
                return Stopped(
                    MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                    MetadataNamedTypeDefinitionChainCatalogIssue.AuthorityViewShapeMismatch,
                    compatibilityCatalog,
                    compilerNameMappingCatalog,
                    observedCount: authorityNodes.Length,
                    relatedMetadataToken: node.TypeDefinition?.TypeDefinitionToken);
            }
        }

        var chains = ImmutableArray.CreateBuilder<MetadataNamedTypeDefinitionChainIdentity>(authorityNodes.Length);
        foreach (var finalNode in authorityNodes)
        {
            var reversedNodes = new List<MetadataNamedTypeDefinitionChainAuthorityNode>();
            var visitedTokens = new HashSet<int>();
            var current = finalNode;
            while (true)
            {
                if (!visitedTokens.Add(current.TypeDefinition.TypeDefinitionToken))
                {
                    return Stopped(
                        MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                        MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionCycle,
                        compatibilityCatalog,
                        compilerNameMappingCatalog,
                        observedCount: visitedTokens.Count,
                        relatedMetadataToken: current.TypeDefinition.TypeDefinitionToken);
                }

                reversedNodes.Add(current);
                if (reversedNodes.Count > StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1)
                {
                    return Stopped(
                        MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
                        MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionDepthBoundReached,
                        compatibilityCatalog,
                        compilerNameMappingCatalog,
                        new EvaluationDeterministicBound(
                            ExpressionV2ContractLimits.NestedTypeDefinitionDepthBoundName,
                            StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth),
                        StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1,
                        finalNode.TypeDefinition.TypeDefinitionToken);
                }

                if (!current.EnclosingTypeDefinitionToken.HasValue)
                {
                    break;
                }
                if (!nodesByToken.TryGetValue(current.EnclosingTypeDefinitionToken.Value, out current))
                {
                    return Stopped(
                        MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                        MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionMissing,
                        compatibilityCatalog,
                        compilerNameMappingCatalog,
                        observedCount: reversedNodes.Count,
                        relatedMetadataToken: reversedNodes[^1].EnclosingTypeDefinitionToken);
                }
            }

            reversedNodes.Reverse();
            var segments = ImmutableArray.CreateBuilder<MetadataNamedTypeDefinitionChainSegmentIdentity>(
                reversedNodes.Count);
            for (var index = 0; index < reversedNodes.Count; index++)
            {
                var node = reversedNodes[index];
                var expectedParentToken = index == 0
                    ? (int?)null
                    : reversedNodes[index - 1].TypeDefinition.TypeDefinitionToken;
                if (node.NestingDepth != index || node.EnclosingTypeDefinitionToken != expectedParentToken)
                {
                    return Stopped(
                        MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                        MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionRelationMismatch,
                        compatibilityCatalog,
                        compilerNameMappingCatalog,
                        observedCount: reversedNodes.Count,
                        relatedMetadataToken: node.TypeDefinition.TypeDefinitionToken);
                }

                var mapping = compilerNameMappingCatalog.CompleteMappingOrDefault(
                    node.TypeDefinition.TypeDefinitionToken);
                if (mapping is null)
                {
                    return Stopped(
                        MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
                        MetadataNamedTypeDefinitionChainCatalogIssue.CompilerMappingMissing,
                        compatibilityCatalog,
                        compilerNameMappingCatalog,
                        observedCount: compilerNameMappingCatalog.Mappings.Length,
                        relatedMetadataToken: node.TypeDefinition.TypeDefinitionToken);
                }
                if (!mapping.TypeDefinition.Equals(node.TypeDefinition) ||
                    !mapping.TypeDefinition.SourceEnds.Equals(node.TypeDefinition.SourceEnds))
                {
                    return Stopped(
                        MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                        MetadataNamedTypeDefinitionChainCatalogIssue.CompilerMappingAuthorityMismatch,
                        compatibilityCatalog,
                        compilerNameMappingCatalog,
                        observedCount: compilerNameMappingCatalog.Mappings.Length,
                        relatedMetadataToken: node.TypeDefinition.TypeDefinitionToken);
                }

                segments.Add(MetadataNamedTypeDefinitionChainSegmentIdentity.Create(
                    SegmentMintCapability,
                    node.TypeDefinition,
                    mapping));
            }

            if (!segments[^1].TypeDefinition.Equals(finalNode.TypeDefinition))
            {
                return Stopped(
                    MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
                    MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionRelationMismatch,
                    compatibilityCatalog,
                    compilerNameMappingCatalog,
                    observedCount: reversedNodes.Count,
                    relatedMetadataToken: finalNode.TypeDefinition.TypeDefinitionToken);
            }
            chains.Add(MetadataNamedTypeDefinitionChainIdentity.Create(
                ChainMintCapability,
                segments.MoveToImmutable()));
        }

        return new MetadataNamedTypeDefinitionChainCatalogIdentity(
            MetadataNamedTypeDefinitionChainCatalogResultKind.Exact,
            MetadataNamedTypeDefinitionChainCatalogIssue.None,
            compatibilityCatalog,
            compilerNameMappingCatalog,
            chains.MoveToImmutable(),
            null,
            0,
            null);
    }

    private static MetadataNamedTypeDefinitionChainCatalogIdentity PrerequisiteStopped(
        MetadataNamedTypeDefinitionChainCatalogResultKind resultKind,
        MetadataNamedTypeDefinitionChainCatalogIssue issue,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog,
        MetadataCompilerNameMappingCatalogIdentity compilerNameMappingCatalog,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        Stopped(
            resultKind,
            issue,
            compatibilityCatalog,
            compilerNameMappingCatalog,
            reachedBound,
            observedCount,
            relatedMetadataToken);

    private static MetadataNamedTypeDefinitionChainCatalogIdentity Stopped(
        MetadataNamedTypeDefinitionChainCatalogResultKind resultKind,
        MetadataNamedTypeDefinitionChainCatalogIssue issue,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity compatibilityCatalog,
        MetadataCompilerNameMappingCatalogIdentity compilerNameMappingCatalog,
        EvaluationDeterministicBound? reachedBound = null,
        int observedCount = 0,
        int? relatedMetadataToken = null) =>
        new(
            resultKind,
            issue,
            compatibilityCatalog,
            compilerNameMappingCatalog,
            [],
            reachedBound,
            observedCount,
            relatedMetadataToken);
}

/// <summary>Classifies one normalized multi-module named-TypeDef-chain draft portfolio.</summary>
/// <remarks>Every stop is prefix-free; exact entries use the compatibility portfolio's module order.</remarks>
public enum MetadataNamedTypeDefinitionChainPortfolioResultKind
{
    /// <summary>Every compatibility-portfolio module has one exact draft chain catalog.</summary>
    Exact = 1,

    /// <summary>A prerequisite, missing vector slot, or declared draft bound prevented normalization.</summary>
    NonExact = 2,

    /// <summary>Complete draft inputs contradicted the compatibility portfolio.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one named-TypeDef-chain draft portfolio.</summary>
/// <remarks>The issue values keep prerequisite, vector-shape, module, and lineage failures distinct.</remarks>
public enum MetadataNamedTypeDefinitionChainPortfolioIssue
{
    /// <summary>No issue applies to an exact draft portfolio.</summary>
    None = 0,

    /// <summary>The definition-compatibility portfolio prerequisite was non-exact.</summary>
    CompatibilityPortfolioNonExact = 1,

    /// <summary>The definition-compatibility portfolio prerequisite was invalid.</summary>
    CompatibilityPortfolioInvalid = 2,

    /// <summary>The supplied chain-catalog vector was default rather than explicitly initialized.</summary>
    CatalogVectorUninitialized = 3,

    /// <summary>The supplied chain-catalog vector reached the shared module bound plus one.</summary>
    ModuleCountBoundReached = 4,

    /// <summary>The supplied chain-catalog vector was shorter than the exact compatibility portfolio.</summary>
    CatalogSlotsIncomplete = 5,

    /// <summary>The supplied chain-catalog vector was longer than the exact compatibility portfolio.</summary>
    CatalogSlotCountConflict = 6,

    /// <summary>An initialized chain-catalog vector slot contained no catalog.</summary>
    CatalogMissing = 7,

    /// <summary>One supplied named-TypeDef-chain catalog stopped non-exactly.</summary>
    CatalogNonExact = 8,

    /// <summary>One supplied named-TypeDef-chain catalog was invalid.</summary>
    CatalogInvalid = 9,

    /// <summary>More than one supplied chain catalog named the same exact metadata module.</summary>
    DuplicateSourceModule = 10,

    /// <summary>A supplied chain catalog named no module in the exact compatibility portfolio.</summary>
    SourceModuleNotInPortfolio = 11,

    /// <summary>A chain catalog retained different W7 compatibility lineage for the same portfolio module.</summary>
    CompatibilityCatalogMismatch = 12,
}

/// <summary>Freezes one guarded fixed-reference entry in a named-TypeDef-chain draft portfolio.</summary>
/// <remarks>The entry is issued only after exact module and compatibility-lineage correlation.</remarks>
public sealed class MetadataNamedTypeDefinitionChainPortfolioEntryIdentity :
    IEquatable<MetadataNamedTypeDefinitionChainPortfolioEntryIdentity>
{
    private const string CanonicalDomain = "metadata-v2-named-typedef-chain-portfolio-entry";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNamedTypeDefinitionChainPortfolioEntryIdentity(
        MetadataDefinitionCompatibilityPortfolioEntryIdentity compatibilityEntry,
        MetadataNamedTypeDefinitionChainCatalogIdentity chainCatalog)
    {
        CompatibilityEntry = compatibilityEntry;
        ChainCatalog = chainCatalog;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(compatibilityEntry.Sha256, nameof(compatibilityEntry));
        writer.WriteSha256(chainCatalog.Sha256, nameof(chainCatalog));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact normalized compatibility draft entry retained as module lineage.</summary>
    public MetadataDefinitionCompatibilityPortfolioEntryIdentity CompatibilityEntry { get; }

    /// <summary>Gets the exact complete named-TypeDef-chain draft catalog for this module.</summary>
    public MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog { get; }

    /// <summary>Gets the exact metadata module used as the normalized draft portfolio key.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => CompatibilityEntry.SourceModule;

    /// <summary>Gets a defensive copy of this fixed-reference canonical draft entry.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft entry.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two named-TypeDef-chain draft portfolio entries.</summary>
    /// <param name="other">The other draft entry.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNamedTypeDefinitionChainPortfolioEntryIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests named-TypeDef-chain draft portfolio-entry equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an entry with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataNamedTypeDefinitionChainPortfolioEntryIdentity);

    /// <summary>Computes a deterministic hash code from immutable named-TypeDef-chain draft entry content.</summary>
    /// <returns>A hash code for this canonical draft entry.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataNamedTypeDefinitionChainPortfolioEntryIdentity Create(
        object mintCapability,
        MetadataDefinitionCompatibilityPortfolioEntryIdentity compatibilityEntry,
        MetadataNamedTypeDefinitionChainCatalogIdentity chainCatalog)
    {
        if (!MetadataNamedTypeDefinitionChainPortfolioIdentity.OwnsEntryMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A named-TypeDef-chain portfolio entry requires the portfolio's private mint capability.",
                nameof(mintCapability));
        }
        ArgumentNullException.ThrowIfNull(compatibilityEntry);
        ArgumentNullException.ThrowIfNull(chainCatalog);
        if (chainCatalog.ResultKind != MetadataNamedTypeDefinitionChainCatalogResultKind.Exact ||
            !compatibilityEntry.SourceModule.Equals(chainCatalog.SourceEnds.SourceModule) ||
            !compatibilityEntry.CompatibilityCatalog.Equals(chainCatalog.CompatibilityCatalog))
        {
            throw new ArgumentException(
                "A named-TypeDef-chain portfolio entry requires exact module-correlated compatibility lineage.",
                nameof(chainCatalog));
        }

        return new MetadataNamedTypeDefinitionChainPortfolioEntryIdentity(compatibilityEntry, chainCatalog);
    }
}

/// <summary>Normalizes per-module named-TypeDef-chain catalogs across one exact compatibility draft portfolio.</summary>
/// <remarks>
/// Caller order is discarded. Exact entries follow the compatibility portfolio's deterministic module order and each
/// retained chain catalog keeps TypeDef RID order. Default and explicit-empty vectors remain distinct.
/// </remarks>
public sealed class MetadataNamedTypeDefinitionChainPortfolioIdentity :
    IEquatable<MetadataNamedTypeDefinitionChainPortfolioIdentity>
{
    private const string CanonicalDomain = "metadata-v2-named-typedef-chain-portfolio";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object EntryMintCapability = new();
    private readonly ImmutableArray<MetadataNamedTypeDefinitionChainPortfolioEntryIdentity> entries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataNamedTypeDefinitionChainPortfolioIdentity(
        MetadataNamedTypeDefinitionChainPortfolioResultKind resultKind,
        MetadataNamedTypeDefinitionChainPortfolioIssue issue,
        MetadataDefinitionCompatibilityPortfolioIdentity compatibilityPortfolio,
        ImmutableArray<MetadataNamedTypeDefinitionChainPortfolioEntryIdentity> entries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken,
        string? relatedModuleSha256)
    {
        ResultKind = resultKind;
        Issue = issue;
        CompatibilityPortfolio = compatibilityPortfolio;
        this.entries = ExpressionV2ContractEncoding.Copy(entries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;
        RelatedModuleSha256 = relatedModuleSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(compatibilityPortfolio.Sha256, nameof(compatibilityPortfolio));
        writer.WriteInt32(entries.Length);
        foreach (var entry in entries)
        {
            writer.WriteSha256(entry.Sha256, nameof(entries));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, relatedModuleSha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the shared module-count draft cap.</summary>
    public const int MaximumModuleCount = ExpressionV2ContractLimits.MaximumModuleCount;

    /// <summary>Gets whether this normalized named-TypeDef-chain draft portfolio is exact, non-exact, or invalid.</summary>
    public MetadataNamedTypeDefinitionChainPortfolioResultKind ResultKind { get; }

    /// <summary>Gets the typed draft portfolio issue, or none for an exact portfolio.</summary>
    public MetadataNamedTypeDefinitionChainPortfolioIssue Issue { get; }

    /// <summary>Gets the retained definition-compatibility draft portfolio prerequisite.</summary>
    public MetadataDefinitionCompatibilityPortfolioIdentity CompatibilityPortfolio { get; }

    /// <summary>Gets the exact portfolio snapshot digest, or null for an empty portfolio or any stop.</summary>
    public string? SnapshotSha256 =>
        ResultKind == MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact
            ? CompatibilityPortfolio.SnapshotSha256
            : null;

    /// <summary>Gets a defensive module-order copy of exact draft entries, or an empty array for a stop.</summary>
    public ImmutableArray<MetadataNamedTypeDefinitionChainPortfolioEntryIdentity> Entries =>
        ExpressionV2ContractEncoding.Copy(entries);

    /// <summary>Gets the propagated or module-count draft bound, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied, prerequisite, or cap-plus-one observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the issue-related TypeDef token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets the issue-related metadata-module digest, otherwise null.</summary>
    public string? RelatedModuleSha256 { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft portfolio.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft portfolio.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one normalized multi-module named-TypeDef-chain draft portfolio.</summary>
    /// <param name="compatibilityPortfolio">The normalized compatibility portfolio that defines exact module lineage.</param>
    /// <param name="chainCatalogs">One initialized exact chain catalog for every compatibility-portfolio module.</param>
    /// <returns>An exact normalized draft portfolio or a prefix-free typed stop.</returns>
    public static MetadataNamedTypeDefinitionChainPortfolioIdentity Create(
        MetadataDefinitionCompatibilityPortfolioIdentity compatibilityPortfolio,
        ImmutableArray<MetadataNamedTypeDefinitionChainCatalogIdentity> chainCatalogs)
    {
        ArgumentNullException.ThrowIfNull(compatibilityPortfolio);
        if (compatibilityPortfolio.ResultKind == MetadataDefinitionCompatibilityPortfolioResultKind.NonExact)
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
                MetadataNamedTypeDefinitionChainPortfolioIssue.CompatibilityPortfolioNonExact,
                compatibilityPortfolio,
                compatibilityPortfolio.ReachedBound,
                compatibilityPortfolio.ObservedCount,
                compatibilityPortfolio.RelatedMetadataToken,
                compatibilityPortfolio.RelatedModuleSha256);
        }
        if (compatibilityPortfolio.ResultKind == MetadataDefinitionCompatibilityPortfolioResultKind.Invalid)
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
                MetadataNamedTypeDefinitionChainPortfolioIssue.CompatibilityPortfolioInvalid,
                compatibilityPortfolio,
                compatibilityPortfolio.ReachedBound,
                compatibilityPortfolio.ObservedCount,
                compatibilityPortfolio.RelatedMetadataToken,
                compatibilityPortfolio.RelatedModuleSha256);
        }
        if (chainCatalogs.IsDefault)
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
                MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogVectorUninitialized,
                compatibilityPortfolio);
        }
        if (chainCatalogs.Length > MaximumModuleCount)
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
                MetadataNamedTypeDefinitionChainPortfolioIssue.ModuleCountBoundReached,
                compatibilityPortfolio,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ModuleCountBoundName,
                    MaximumModuleCount),
                MaximumModuleCount + 1);
        }

        var expectedCount = compatibilityPortfolio.Entries.Length;
        if (chainCatalogs.Length < expectedCount)
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
                MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogSlotsIncomplete,
                compatibilityPortfolio,
                observedCount: chainCatalogs.Length);
        }
        if (chainCatalogs.Length > expectedCount)
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
                MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogSlotCountConflict,
                compatibilityPortfolio,
                observedCount: chainCatalogs.Length);
        }
        if (chainCatalogs.IsEmpty)
        {
            return Exact(compatibilityPortfolio, []);
        }
        if (chainCatalogs.Any(static catalog => catalog is null))
        {
            return Stopped(
                MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
                MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogMissing,
                compatibilityPortfolio,
                observedCount: chainCatalogs.Length);
        }

        var pending = chainCatalogs.ToArray();
        Array.Sort(pending, ChainCatalogComparer.Instance);
        foreach (var catalog in pending)
        {
            if (catalog.ResultKind == MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact)
            {
                return CatalogStopped(
                    MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
                    MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogNonExact,
                    compatibilityPortfolio,
                    catalog);
            }
        }
        foreach (var catalog in pending)
        {
            if (catalog.ResultKind == MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid)
            {
                return CatalogStopped(
                    MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
                    MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogInvalid,
                    compatibilityPortfolio,
                    catalog);
            }
        }

        var catalogsByModule = new Dictionary<StaticFieldMetadataModuleIdentity,
            MetadataNamedTypeDefinitionChainCatalogIdentity>();
        foreach (var catalog in pending)
        {
            var sourceModule = catalog.SourceEnds.SourceModule;
            if (!catalogsByModule.TryAdd(sourceModule, catalog))
            {
                return Stopped(
                    MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
                    MetadataNamedTypeDefinitionChainPortfolioIssue.DuplicateSourceModule,
                    compatibilityPortfolio,
                    observedCount: chainCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }

            var compatibilityEntry = compatibilityPortfolio.ExactEntryOrDefault(sourceModule);
            if (compatibilityEntry is null)
            {
                return Stopped(
                    MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
                    MetadataNamedTypeDefinitionChainPortfolioIssue.SourceModuleNotInPortfolio,
                    compatibilityPortfolio,
                    observedCount: chainCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
            if (!compatibilityEntry.CompatibilityCatalog.Equals(catalog.CompatibilityCatalog))
            {
                return Stopped(
                    MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
                    MetadataNamedTypeDefinitionChainPortfolioIssue.CompatibilityCatalogMismatch,
                    compatibilityPortfolio,
                    observedCount: chainCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
        }

        var entries = ImmutableArray.CreateBuilder<MetadataNamedTypeDefinitionChainPortfolioEntryIdentity>(
            expectedCount);
        foreach (var compatibilityEntry in compatibilityPortfolio.Entries)
        {
            entries.Add(MetadataNamedTypeDefinitionChainPortfolioEntryIdentity.Create(
                EntryMintCapability,
                compatibilityEntry,
                catalogsByModule[compatibilityEntry.SourceModule]));
        }
        return Exact(compatibilityPortfolio, entries.MoveToImmutable());
    }

    /// <summary>Looks up one exact draft chain by metadata module and non-nil TypeDef token.</summary>
    /// <param name="sourceModule">The exact loaded metadata module.</param>
    /// <param name="typeDefinitionToken">The exact TypeDef token within that module.</param>
    /// <returns>The exact draft chain, or null when the portfolio stopped, module is absent, or token was not issued.</returns>
    public MetadataNamedTypeDefinitionChainIdentity? ExactChainOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        if (ResultKind != MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact)
        {
            return null;
        }

        var entry = entries.FirstOrDefault(candidate => candidate.SourceModule.Equals(sourceModule));
        return entry?.ChainCatalog.ExactChainOrDefault(typeDefinitionToken);
    }

    /// <summary>Tests canonical equality between two named-TypeDef-chain draft portfolios.</summary>
    /// <param name="other">The other draft portfolio.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataNamedTypeDefinitionChainPortfolioIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests named-TypeDef-chain draft portfolio equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a portfolio with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataNamedTypeDefinitionChainPortfolioIdentity);

    /// <summary>Computes a deterministic hash code from immutable named-TypeDef-chain draft portfolio content.</summary>
    /// <returns>A hash code for this canonical draft portfolio.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsEntryMintCapability(object? capability) =>
        ReferenceEquals(capability, EntryMintCapability);

    private static MetadataNamedTypeDefinitionChainPortfolioIdentity Exact(
        MetadataDefinitionCompatibilityPortfolioIdentity compatibilityPortfolio,
        ImmutableArray<MetadataNamedTypeDefinitionChainPortfolioEntryIdentity> entries) =>
        new(
            MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact,
            MetadataNamedTypeDefinitionChainPortfolioIssue.None,
            compatibilityPortfolio,
            entries,
            null,
            0,
            null,
            null);

    private static MetadataNamedTypeDefinitionChainPortfolioIdentity CatalogStopped(
        MetadataNamedTypeDefinitionChainPortfolioResultKind resultKind,
        MetadataNamedTypeDefinitionChainPortfolioIssue issue,
        MetadataDefinitionCompatibilityPortfolioIdentity compatibilityPortfolio,
        MetadataNamedTypeDefinitionChainCatalogIdentity catalog) =>
        Stopped(
            resultKind,
            issue,
            compatibilityPortfolio,
            catalog.ReachedBound,
            catalog.ObservedCount,
            catalog.RelatedMetadataToken,
            catalog.SourceEnds.SourceModule.Sha256);

    private static MetadataNamedTypeDefinitionChainPortfolioIdentity Stopped(
        MetadataNamedTypeDefinitionChainPortfolioResultKind resultKind,
        MetadataNamedTypeDefinitionChainPortfolioIssue issue,
        MetadataDefinitionCompatibilityPortfolioIdentity compatibilityPortfolio,
        EvaluationDeterministicBound? reachedBound = null,
        int observedCount = 0,
        int? relatedMetadataToken = null,
        string? relatedModuleSha256 = null) =>
        new(
            resultKind,
            issue,
            compatibilityPortfolio,
            [],
            reachedBound,
            observedCount,
            relatedMetadataToken,
            relatedModuleSha256);

    private sealed class ChainCatalogComparer : IComparer<MetadataNamedTypeDefinitionChainCatalogIdentity>
    {
        internal static ChainCatalogComparer Instance { get; } = new();

        public int Compare(
            MetadataNamedTypeDefinitionChainCatalogIdentity? left,
            MetadataNamedTypeDefinitionChainCatalogIdentity? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left is null)
            {
                return -1;
            }
            if (right is null)
            {
                return 1;
            }

            var sourceComparison = left.SourceEnds.SourceModule.CanonicalBytes.AsSpan().SequenceCompareTo(
                right.SourceEnds.SourceModule.CanonicalBytes.AsSpan());
            return sourceComparison != 0
                ? sourceComparison
                : StringComparer.Ordinal.Compare(left.Sha256, right.Sha256);
        }
    }
}

internal readonly record struct MetadataNamedTypeDefinitionChainAuthorityNode(
    MetadataTypeDefinitionAuthorityIdentity TypeDefinition,
    int? EnclosingTypeDefinitionToken,
    int NestingDepth);
