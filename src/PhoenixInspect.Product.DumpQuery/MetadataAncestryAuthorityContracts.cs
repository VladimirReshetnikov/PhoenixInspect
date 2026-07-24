using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies the physical form of one authority-derived immediate-base draft edge.</summary>
/// <remarks>Every value is a complete derived row fact rather than a catalog failure.</remarks>
public enum MetadataImmediateBaseEdgeKind
{
    /// <summary>The TypeDef row has a nil Extends column.</summary>
    None = 1,

    /// <summary>The Extends column names a TypeDef in the same module.</summary>
    TypeDefinition = 2,

    /// <summary>The Extends column names a TypeRef whose portfolio resolution is exact.</summary>
    TypeReference = 3,

    /// <summary>The Extends column names a TypeRef whose portfolio resolution retained a typed stop.</summary>
    TypeReferenceUnresolved = 4,

    /// <summary>The Extends column names a TypeSpec generic instantiation retained for later construction.</summary>
    TypeSpecification = 5,
}

/// <summary>Freezes one guarded authority-derived immediate-base draft edge.</summary>
/// <remarks>
/// The edge is derived from the exact physical Extends column and the resolution portfolio only. A generic TypeSpec
/// base retains its complete signature row without decoding; role semantics never flow through it.
/// </remarks>
public sealed class MetadataImmediateBaseEdgeAuthorityIdentity :
    IEquatable<MetadataImmediateBaseEdgeAuthorityIdentity>
{
    private const string CanonicalDomain = "metadata-v2-immediate-base-edge";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataImmediateBaseEdgeAuthorityIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity owner,
        MetadataImmediateBaseEdgeKind kind,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeReferenceResolutionIdentity? referenceResolution,
        MetadataTypeSpecificationPhysicalRowIdentity? genericBaseRow)
    {
        SourceModule = sourceModule;
        Owner = owner;
        Kind = kind;
        TargetModule = targetModule;
        TargetTypeDefinition = targetTypeDefinition;
        ReferenceResolution = referenceResolution;
        GenericBaseRow = genericBaseRow;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteSha256(owner.Sha256, nameof(owner));
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetModule?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetTypeDefinition?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, referenceResolution?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, genericBaseRow?.Sha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module owning the physical Extends column.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the authority-issued TypeDef owning this draft edge.</summary>
    public MetadataTypeDefinitionAuthorityIdentity Owner { get; }

    /// <summary>Gets the complete typed physical form of this draft edge.</summary>
    public MetadataImmediateBaseEdgeKind Kind { get; }

    /// <summary>Gets the exact raw Extends token, or null for a nil column.</summary>
    public int? ExtendsMetadataToken => Owner.TableRow.Observation.ExtendsMetadataToken;

    /// <summary>Gets the exact base target module for a named resolved draft edge, otherwise null.</summary>
    public StaticFieldMetadataModuleIdentity? TargetModule { get; }

    /// <summary>Gets the exact authority-issued base TypeDef for a named resolved draft edge, otherwise null.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? TargetTypeDefinition { get; }

    /// <summary>Gets the retained TypeRef draft resolution row for a reference-formed edge, otherwise null.</summary>
    public MetadataTypeReferenceResolutionIdentity? ReferenceResolution { get; }

    /// <summary>Gets the retained physical TypeSpec draft row for a generic base, otherwise null.</summary>
    public MetadataTypeSpecificationPhysicalRowIdentity? GenericBaseRow { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft edge bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft edge bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two immediate-base draft edges.</summary>
    /// <param name="other">The other draft edge.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataImmediateBaseEdgeAuthorityIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests immediate-base draft edge equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an edge with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataImmediateBaseEdgeAuthorityIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this immediate-base draft edge.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataImmediateBaseEdgeAuthorityIdentity Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity owner,
        MetadataImmediateBaseEdgeKind kind,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeReferenceResolutionIdentity? referenceResolution,
        MetadataTypeSpecificationPhysicalRowIdentity? genericBaseRow)
    {
        if (!MetadataAncestryAuthorityPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An immediate-base edge requires the ancestry portfolio's private mint capability.",
                nameof(mintCapability));
        }
        var named = kind is MetadataImmediateBaseEdgeKind.TypeDefinition or MetadataImmediateBaseEdgeKind.TypeReference;
        if (named == (targetModule is null) || named == (targetTypeDefinition is null))
        {
            throw new ArgumentException(
                "An immediate-base edge retains a named target exactly when its kind is a resolved definition.",
                nameof(kind));
        }

        return new MetadataImmediateBaseEdgeAuthorityIdentity(
            sourceModule,
            owner,
            kind,
            targetModule,
            targetTypeDefinition,
            referenceResolution,
            genericBaseRow);
    }
}

/// <summary>Classifies one prerequisite stop or completion state for the core-role draft selection.</summary>
/// <remarks>Selection issues stop the issuing portfolio because later role semantics depend on them.</remarks>
public enum MetadataCoreRoleSelectionIssue
{
    /// <summary>No issue applies to an exact core-role draft selection.</summary>
    None = 0,

    /// <summary>No portfolio module defines a nil-extends non-interface top-level System.Object.</summary>
    CoreModuleAbsent = 1,

    /// <summary>More than one portfolio module defines a top-level nil-extends System.Object.</summary>
    CoreModuleAmbiguous = 2,

    /// <summary>A required role definition is absent from the exact core module.</summary>
    RoleDefinitionAbsent = 3,

    /// <summary>A required role definition has more than one top-level match in the core module.</summary>
    RoleDefinitionAmbiguous = 4,

    /// <summary>A role definition's immediate base does not resolve to its required parent role.</summary>
    RoleBaseEdgeMismatch = 5,
}

/// <summary>Freezes the exact runtime core-role draft selection for one resolution portfolio.</summary>
/// <remarks>
/// The five role definitions are physical selections: exactly one top-level System.Object with a nil Extends column,
/// and exactly one System.ValueType, System.Enum, System.Delegate, and System.MulticastDelegate whose immediate bases
/// resolve to Object, ValueType, Object, and Delegate respectively in the same core module. The role definitions
/// themselves classify as classes.
/// </remarks>
public sealed class MetadataCoreRoleSelectionIdentity :
    IEquatable<MetadataCoreRoleSelectionIdentity>
{
    private const string CanonicalDomain = "metadata-v2-core-role-selection";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataCoreRoleSelectionIdentity(
        StaticFieldMetadataModuleIdentity coreModule,
        MetadataTypeDefinitionAuthorityIdentity systemObject,
        MetadataTypeDefinitionAuthorityIdentity systemValueType,
        MetadataTypeDefinitionAuthorityIdentity systemEnum,
        MetadataTypeDefinitionAuthorityIdentity systemDelegate,
        MetadataTypeDefinitionAuthorityIdentity systemMulticastDelegate)
    {
        CoreModule = coreModule;
        SystemObject = systemObject;
        SystemValueType = systemValueType;
        SystemEnum = systemEnum;
        SystemDelegate = systemDelegate;
        SystemMulticastDelegate = systemMulticastDelegate;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(coreModule.Sha256, nameof(coreModule));
        writer.WriteSha256(systemObject.Sha256, nameof(systemObject));
        writer.WriteSha256(systemValueType.Sha256, nameof(systemValueType));
        writer.WriteSha256(systemEnum.Sha256, nameof(systemEnum));
        writer.WriteSha256(systemDelegate.Sha256, nameof(systemDelegate));
        writer.WriteSha256(systemMulticastDelegate.Sha256, nameof(systemMulticastDelegate));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module defining every selected core role.</summary>
    public StaticFieldMetadataModuleIdentity CoreModule { get; }

    /// <summary>Gets the exact authority-issued System.Object draft definition.</summary>
    public MetadataTypeDefinitionAuthorityIdentity SystemObject { get; }

    /// <summary>Gets the exact authority-issued System.ValueType draft definition.</summary>
    public MetadataTypeDefinitionAuthorityIdentity SystemValueType { get; }

    /// <summary>Gets the exact authority-issued System.Enum draft definition.</summary>
    public MetadataTypeDefinitionAuthorityIdentity SystemEnum { get; }

    /// <summary>Gets the exact authority-issued System.Delegate draft definition.</summary>
    public MetadataTypeDefinitionAuthorityIdentity SystemDelegate { get; }

    /// <summary>Gets the exact authority-issued System.MulticastDelegate draft definition.</summary>
    public MetadataTypeDefinitionAuthorityIdentity SystemMulticastDelegate { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft selection bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft selection bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests whether one authority TypeDef in one module is one of the five selected role definitions.</summary>
    /// <param name="module">The exact module containing the candidate definition.</param>
    /// <param name="typeDefinition">The authority-issued candidate definition.</param>
    /// <returns><see langword="true"/> only for the exact selected core-role draft definitions.</returns>
    public bool IsRoleDefinition(
        StaticFieldMetadataModuleIdentity module,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(typeDefinition);
        return module.Equals(CoreModule) &&
            (typeDefinition.Equals(SystemObject) ||
             typeDefinition.Equals(SystemValueType) ||
             typeDefinition.Equals(SystemEnum) ||
             typeDefinition.Equals(SystemDelegate) ||
             typeDefinition.Equals(SystemMulticastDelegate));
    }

    /// <summary>Tests canonical equality between two core-role draft selections.</summary>
    /// <param name="other">The other draft selection.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataCoreRoleSelectionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests core-role draft selection equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a selection with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataCoreRoleSelectionIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this core-role draft selection.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataCoreRoleSelectionIdentity Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity coreModule,
        MetadataTypeDefinitionAuthorityIdentity systemObject,
        MetadataTypeDefinitionAuthorityIdentity systemValueType,
        MetadataTypeDefinitionAuthorityIdentity systemEnum,
        MetadataTypeDefinitionAuthorityIdentity systemDelegate,
        MetadataTypeDefinitionAuthorityIdentity systemMulticastDelegate)
    {
        if (!MetadataAncestryAuthorityPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A core-role selection requires the ancestry portfolio's private mint capability.",
                nameof(mintCapability));
        }
        return new MetadataCoreRoleSelectionIdentity(
            coreModule,
            systemObject,
            systemValueType,
            systemEnum,
            systemDelegate,
            systemMulticastDelegate);
    }
}

/// <summary>Classifies the authority-derived semantic role of one TypeDef draft row.</summary>
/// <remarks>
/// Value-type, enum, and delegate semantics come only from an exact immediate base-role edge. The selected role
/// definitions themselves are classes, and no role is derived through an intermediate base.
/// </remarks>
public enum MetadataTypeDefinitionSemanticRole
{
    /// <summary>The row is the ECMA module pseudo-type.</summary>
    ModulePseudoType = 1,

    /// <summary>The row is an ordinary reference class, including every selected role definition.</summary>
    Class = 2,

    /// <summary>The row's immediate base is the exact System.ValueType definition and the row is not System.Enum.</summary>
    ValueType = 3,

    /// <summary>The row carries the interface flag with a nil Extends column.</summary>
    Interface = 4,

    /// <summary>The row's immediate base is the exact System.Enum definition.</summary>
    Enum = 5,

    /// <summary>The row's immediate base is the exact System.MulticastDelegate definition.</summary>
    Delegate = 6,
}

/// <summary>Identifies the complete typed disposition of one semantic-classification draft row.</summary>
/// <remarks>Row-level stops are retained facts; the issuing portfolio remains an exact complete projection.</remarks>
public enum MetadataSemanticClassificationIssue
{
    /// <summary>No issue applies; the row has an exact semantic role.</summary>
    None = 0,

    /// <summary>The row's immediate base is a TypeRef whose portfolio resolution retained a typed stop.</summary>
    BaseReferenceUnresolved = 1,

    /// <summary>A non-interface named row other than System.Object has a nil Extends column.</summary>
    BaseEdgeMissing = 2,

    /// <summary>An interface-flagged row has a non-nil Extends column.</summary>
    InterfaceExtendsInvalid = 3,
}

/// <summary>Freezes one guarded authority-derived semantic-classification draft row.</summary>
/// <remarks>The row retains its immediate-base draft edge so no consumer re-derives role semantics from names.</remarks>
public sealed class MetadataTypeDefinitionSemanticClassificationIdentity :
    IEquatable<MetadataTypeDefinitionSemanticClassificationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-semantic-classification-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefinitionSemanticClassificationIdentity(
        MetadataImmediateBaseEdgeAuthorityIdentity baseEdge,
        MetadataTypeDefinitionSemanticRole? role,
        MetadataSemanticClassificationIssue issue)
    {
        BaseEdge = baseEdge;
        Role = role;
        Issue = issue;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(baseEdge.Sha256, nameof(baseEdge));
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, role is { } presentRole ? (int)presentRole : null);
        writer.WriteInt32((int)issue);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the retained immediate-base draft edge this classification consumed.</summary>
    public MetadataImmediateBaseEdgeAuthorityIdentity BaseEdge { get; }

    /// <summary>Gets the exact metadata module owning the classified row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => BaseEdge.SourceModule;

    /// <summary>Gets the authority-issued TypeDef this draft row classifies.</summary>
    public MetadataTypeDefinitionAuthorityIdentity TypeDefinition => BaseEdge.Owner;

    /// <summary>Gets the exact semantic role, or null when the typed issue prevented classification.</summary>
    public MetadataTypeDefinitionSemanticRole? Role { get; }

    /// <summary>Gets the complete typed draft classification issue for this row.</summary>
    public MetadataSemanticClassificationIssue Issue { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two semantic-classification draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeDefinitionSemanticClassificationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests semantic-classification draft row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataTypeDefinitionSemanticClassificationIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this semantic-classification draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeDefinitionSemanticClassificationIdentity Create(
        object mintCapability,
        MetadataImmediateBaseEdgeAuthorityIdentity baseEdge,
        MetadataTypeDefinitionSemanticRole? role,
        MetadataSemanticClassificationIssue issue)
    {
        if (!MetadataAncestryAuthorityPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A semantic-classification row requires the ancestry portfolio's private mint capability.",
                nameof(mintCapability));
        }
        if ((role is null) == (issue == MetadataSemanticClassificationIssue.None))
        {
            throw new ArgumentException(
                "A semantic-classification row has a role exactly when it has no typed issue.",
                nameof(role));
        }
        return new MetadataTypeDefinitionSemanticClassificationIdentity(baseEdge, role, issue);
    }
}

/// <summary>Classifies the terminal of one bounded authority-derived ancestry draft chain.</summary>
/// <remarks>Every terminal is a complete derived row fact including cycles and reached bounds.</remarks>
public enum MetadataAncestryChainTerminalKind
{
    /// <summary>The chain reached the exact System.Object definition.</summary>
    SystemObjectReached = 1,

    /// <summary>The subject is an interface with no base chain.</summary>
    InterfaceRoot = 2,

    /// <summary>The subject is the module pseudo-type with no base chain.</summary>
    ModulePseudoTypeRoot = 3,

    /// <summary>The chain reached a generic TypeSpec base retained for later construction.</summary>
    GenericBaseReached = 4,

    /// <summary>The chain reached a TypeRef base whose portfolio resolution retained a typed stop.</summary>
    UnresolvedReferenceReached = 5,

    /// <summary>The chain reached a named non-interface row with a nil Extends column other than System.Object.</summary>
    MissingBaseInvalid = 6,

    /// <summary>The chain revisited one physical TypeDef coordinate.</summary>
    CycleDetected = 7,

    /// <summary>The chain reached the declared ancestry depth bound plus one.</summary>
    DepthBoundReached = 8,
}

/// <summary>Freezes one guarded bounded authority-derived ancestry draft chain.</summary>
/// <remarks>
/// The chain follows resolved named immediate-base edges across modules from the subject toward System.Object and
/// stops at the first typed terminal. It performs no generic substitution; a TypeSpec base is a terminal fact.
/// </remarks>
public sealed class MetadataTypeDefinitionAncestryChainIdentity :
    IEquatable<MetadataTypeDefinitionAncestryChainIdentity>
{
    private const string CanonicalDomain = "metadata-v2-ancestry-chain";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> edges;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefinitionAncestryChainIdentity(
        MetadataImmediateBaseEdgeAuthorityIdentity subjectEdge,
        ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> edges,
        MetadataAncestryChainTerminalKind terminalKind,
        EvaluationDeterministicBound? reachedBound,
        int? relatedMetadataToken)
    {
        SubjectEdge = subjectEdge;
        this.edges = ExpressionV2ContractEncoding.Copy(edges);
        TerminalKind = terminalKind;
        ReachedBound = reachedBound;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(subjectEdge.Sha256, nameof(subjectEdge));
        writer.WriteInt32(edges.Length);
        foreach (var edge in edges)
        {
            writer.WriteSha256(edge.Sha256, nameof(edges));
        }
        writer.WriteInt32((int)terminalKind);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the subject's own immediate-base draft edge.</summary>
    public MetadataImmediateBaseEdgeAuthorityIdentity SubjectEdge { get; }

    /// <summary>Gets the exact metadata module owning the subject row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => SubjectEdge.SourceModule;

    /// <summary>Gets the authority-issued subject TypeDef of this draft chain.</summary>
    public MetadataTypeDefinitionAuthorityIdentity Subject => SubjectEdge.Owner;

    /// <summary>Gets a defensive subject-to-root copy of the traversed immediate-base draft edges.</summary>
    public ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> Edges =>
        ExpressionV2ContractEncoding.Copy(edges);

    /// <summary>Gets the complete typed terminal of this bounded draft chain.</summary>
    public MetadataAncestryChainTerminalKind TerminalKind { get; }

    /// <summary>Gets the declared ancestry depth bound only after a cap-plus-one traversal.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the terminal-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft chain bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft chain bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two ancestry draft chains.</summary>
    /// <param name="other">The other draft chain.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeDefinitionAncestryChainIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests ancestry draft chain equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a chain with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeDefinitionAncestryChainIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this ancestry draft chain.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeDefinitionAncestryChainIdentity Create(
        object mintCapability,
        MetadataImmediateBaseEdgeAuthorityIdentity subjectEdge,
        ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> edges,
        MetadataAncestryChainTerminalKind terminalKind,
        EvaluationDeterministicBound? reachedBound,
        int? relatedMetadataToken)
    {
        if (!MetadataAncestryAuthorityPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An ancestry chain requires the ancestry portfolio's private mint capability.",
                nameof(mintCapability));
        }
        return new MetadataTypeDefinitionAncestryChainIdentity(
            subjectEdge,
            edges,
            terminalKind,
            reachedBound,
            relatedMetadataToken);
    }
}

/// <summary>Freezes one guarded per-module entry of the ancestry draft portfolio.</summary>
/// <remarks>The entry retains RID-ordered base edges, semantic classifications, and bounded ancestry chains.</remarks>
public sealed class MetadataAncestryAuthorityModuleIdentity :
    IEquatable<MetadataAncestryAuthorityModuleIdentity>
{
    private const string CanonicalDomain = "metadata-v2-ancestry-module";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> baseEdges;
    private readonly ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> classifications;
    private readonly ImmutableArray<MetadataTypeDefinitionAncestryChainIdentity> ancestryChains;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAncestryAuthorityModuleIdentity(
        MetadataTypeReferenceResolutionModuleIdentity resolutionEntry,
        ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> baseEdges,
        ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> classifications,
        ImmutableArray<MetadataTypeDefinitionAncestryChainIdentity> ancestryChains)
    {
        ResolutionEntry = resolutionEntry;
        this.baseEdges = ExpressionV2ContractEncoding.Copy(baseEdges);
        this.classifications = ExpressionV2ContractEncoding.Copy(classifications);
        this.ancestryChains = ExpressionV2ContractEncoding.Copy(ancestryChains);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(resolutionEntry.Sha256, nameof(resolutionEntry));
        writer.WriteInt32(baseEdges.Length);
        foreach (var edge in baseEdges)
        {
            writer.WriteSha256(edge.Sha256, nameof(baseEdges));
        }
        writer.WriteInt32(classifications.Length);
        foreach (var classification in classifications)
        {
            writer.WriteSha256(classification.Sha256, nameof(classifications));
        }
        writer.WriteInt32(ancestryChains.Length);
        foreach (var chain in ancestryChains)
        {
            writer.WriteSha256(chain.Sha256, nameof(ancestryChains));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the retained per-module TypeRef draft resolution entry lineage.</summary>
    public MetadataTypeReferenceResolutionModuleIdentity ResolutionEntry { get; }

    /// <summary>Gets the exact metadata module owning every row in this draft entry.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => ResolutionEntry.SourceModule;

    /// <summary>Gets a defensive RID-order copy of every immediate-base draft edge.</summary>
    public ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> BaseEdges =>
        ExpressionV2ContractEncoding.Copy(baseEdges);

    /// <summary>Gets a defensive RID-order copy of every semantic-classification draft row.</summary>
    public ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> Classifications =>
        ExpressionV2ContractEncoding.Copy(classifications);

    /// <summary>Gets a defensive RID-order copy of every bounded ancestry draft chain.</summary>
    public ImmutableArray<MetadataTypeDefinitionAncestryChainIdentity> AncestryChains =>
        ExpressionV2ContractEncoding.Copy(ancestryChains);

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft entry bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft entry bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Finds one immediate-base draft edge by TypeDef token.</summary>
    /// <param name="typeDefinitionToken">The non-nil TypeDef token.</param>
    /// <returns>The complete draft edge, or null when the token is not an issued TypeDef.</returns>
    public MetadataImmediateBaseEdgeAuthorityIdentity? FindBaseEdge(int typeDefinitionToken) =>
        FindRow(baseEdges, typeDefinitionToken);

    /// <summary>Finds one semantic-classification draft row by TypeDef token.</summary>
    /// <param name="typeDefinitionToken">The non-nil TypeDef token.</param>
    /// <returns>The complete draft row, or null when the token is not an issued TypeDef.</returns>
    public MetadataTypeDefinitionSemanticClassificationIdentity? FindClassification(int typeDefinitionToken) =>
        FindRow(classifications, typeDefinitionToken);

    /// <summary>Finds one bounded ancestry draft chain by TypeDef token.</summary>
    /// <param name="typeDefinitionToken">The non-nil TypeDef token.</param>
    /// <returns>The complete draft chain, or null when the token is not an issued TypeDef.</returns>
    public MetadataTypeDefinitionAncestryChainIdentity? FindAncestryChain(int typeDefinitionToken) =>
        FindRow(ancestryChains, typeDefinitionToken);

    /// <summary>Tests canonical equality between two per-module ancestry draft entries.</summary>
    /// <param name="other">The other draft entry.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAncestryAuthorityModuleIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests per-module ancestry draft entry equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an entry with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAncestryAuthorityModuleIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical draft content.</summary>
    /// <returns>A hash code for this per-module ancestry draft entry.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataAncestryAuthorityModuleIdentity Create(
        object mintCapability,
        MetadataTypeReferenceResolutionModuleIdentity resolutionEntry,
        ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> baseEdges,
        ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> classifications,
        ImmutableArray<MetadataTypeDefinitionAncestryChainIdentity> ancestryChains)
    {
        if (!MetadataAncestryAuthorityPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A per-module ancestry entry requires the ancestry portfolio's private mint capability.",
                nameof(mintCapability));
        }
        return new MetadataAncestryAuthorityModuleIdentity(
            resolutionEntry,
            baseEdges,
            classifications,
            ancestryChains);
    }

    private static T? FindRow<T>(ImmutableArray<T> rows, int typeDefinitionToken)
        where T : class
    {
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(typeDefinitionToken, 0x02))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinitionToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }
}

/// <summary>Classifies one normalized multi-module ancestry draft portfolio.</summary>
/// <remarks>Every stop is prefix-free; exact entries use the resolution portfolio's module order.</remarks>
public enum MetadataAncestryAuthorityPortfolioResultKind
{
    /// <summary>Every module has complete RID-ordered edges, classifications, and bounded chains.</summary>
    Exact = 1,

    /// <summary>A prerequisite or core-role selection prevented complete derivation.</summary>
    NonExact = 2,

    /// <summary>Complete draft inputs contradicted the resolution portfolio or the core-role shape.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one ancestry draft portfolio.</summary>
/// <remarks>The issue values keep prerequisite and core-role selection stops distinct.</remarks>
public enum MetadataAncestryAuthorityPortfolioIssue
{
    /// <summary>No issue applies to an exact draft portfolio.</summary>
    None = 0,

    /// <summary>The TypeRef resolution portfolio prerequisite was non-exact.</summary>
    ResolutionPortfolioNonExact = 1,

    /// <summary>The TypeRef resolution portfolio prerequisite was invalid.</summary>
    ResolutionPortfolioInvalid = 2,

    /// <summary>No portfolio module defines the required nil-extends top-level System.Object.</summary>
    CoreModuleAbsent = 3,

    /// <summary>More than one portfolio module defines a top-level nil-extends System.Object.</summary>
    CoreModuleAmbiguous = 4,

    /// <summary>A required role definition is absent from the exact core module.</summary>
    RoleDefinitionAbsent = 5,

    /// <summary>A required role definition has more than one top-level match in the core module.</summary>
    RoleDefinitionAmbiguous = 6,

    /// <summary>A role definition's immediate base does not resolve to its required parent role.</summary>
    RoleBaseEdgeMismatch = 7,
}

/// <summary>Derives immediate-base edges, core roles, semantic classification, and bounded ancestry.</summary>
/// <remarks>
/// The portfolio is the sole issuer of ancestry draft rows. It consumes one exact TypeRef resolution portfolio,
/// selects the exact runtime core roles, derives every module's RID-ordered immediate-base edges from physical
/// Extends columns, classifies each TypeDef only from its exact immediate base-role edge, and walks bounded
/// cross-module ancestry chains without generic substitution. Names never confer role semantics.
/// </remarks>
public sealed class MetadataAncestryAuthorityPortfolioIdentity :
    IEquatable<MetadataAncestryAuthorityPortfolioIdentity>
{
    /// <summary>Gets the shared bounded ancestry depth draft cap.</summary>
    public const int MaximumAncestryDepth = ExpressionV2ContractLimits.MaximumConstructedAncestryDepth;

    private const string CanonicalDomain = "metadata-v2-ancestry-portfolio";
    private const int CanonicalSchemaVersion = 1;
    private const int InterfaceFlag = 0x0000_0020;
    private const int ModulePseudoTypeToken = 0x0200_0001;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataAncestryAuthorityModuleIdentity> entries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataAncestryAuthorityPortfolioIdentity(
        MetadataAncestryAuthorityPortfolioResultKind resultKind,
        MetadataAncestryAuthorityPortfolioIssue issue,
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio,
        MetadataCoreRoleSelectionIdentity? coreRoles,
        ImmutableArray<MetadataAncestryAuthorityModuleIdentity> entries,
        int observedCount,
        string? relatedModuleSha256)
    {
        ResultKind = resultKind;
        Issue = issue;
        ResolutionPortfolio = resolutionPortfolio;
        CoreRoles = coreRoles;
        this.entries = ExpressionV2ContractEncoding.Copy(entries);
        ObservedCount = observedCount;
        RelatedModuleSha256 = relatedModuleSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(resolutionPortfolio.Sha256, nameof(resolutionPortfolio));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, coreRoles?.Sha256);
        writer.WriteInt32(entries.Length);
        foreach (var entry in entries)
        {
            writer.WriteSha256(entry.Sha256, nameof(entries));
        }
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, relatedModuleSha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this normalized ancestry draft portfolio is exact, non-exact, or invalid.</summary>
    public MetadataAncestryAuthorityPortfolioResultKind ResultKind { get; }

    /// <summary>Gets the typed draft portfolio issue, or none for an exact portfolio.</summary>
    public MetadataAncestryAuthorityPortfolioIssue Issue { get; }

    /// <summary>Gets the retained TypeRef draft resolution portfolio prerequisite.</summary>
    public MetadataTypeReferenceResolutionPortfolioIdentity ResolutionPortfolio { get; }

    /// <summary>Gets the exact core-role draft selection, or null for any stop.</summary>
    public MetadataCoreRoleSelectionIdentity? CoreRoles { get; }

    /// <summary>Gets the exact portfolio snapshot digest, or null for an empty portfolio or any stop.</summary>
    public string? SnapshotSha256 =>
        ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Exact
            ? ResolutionPortfolio.SnapshotSha256
            : null;

    /// <summary>Gets a defensive module-order copy of exact draft entries, or an empty array for a stop.</summary>
    public ImmutableArray<MetadataAncestryAuthorityModuleIdentity> Entries =>
        ExpressionV2ContractEncoding.Copy(entries);

    /// <summary>Gets the issue-related observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the issue-related metadata-module digest, otherwise null.</summary>
    public string? RelatedModuleSha256 { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft portfolio.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft portfolio.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one normalized multi-module ancestry draft portfolio.</summary>
    /// <param name="resolutionPortfolio">The exact TypeRef resolution portfolio supplying all inputs.</param>
    /// <returns>An exact complete draft portfolio or a prefix-free typed stop.</returns>
    public static MetadataAncestryAuthorityPortfolioIdentity Create(
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio)
    {
        ArgumentNullException.ThrowIfNull(resolutionPortfolio);
        if (resolutionPortfolio.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.NonExact)
        {
            return Stopped(
                MetadataAncestryAuthorityPortfolioResultKind.NonExact,
                MetadataAncestryAuthorityPortfolioIssue.ResolutionPortfolioNonExact,
                resolutionPortfolio,
                resolutionPortfolio.ObservedCount,
                resolutionPortfolio.RelatedModuleSha256);
        }
        if (resolutionPortfolio.ResultKind == MetadataTypeReferenceResolutionPortfolioResultKind.Invalid)
        {
            return Stopped(
                MetadataAncestryAuthorityPortfolioResultKind.Invalid,
                MetadataAncestryAuthorityPortfolioIssue.ResolutionPortfolioInvalid,
                resolutionPortfolio,
                resolutionPortfolio.ObservedCount,
                resolutionPortfolio.RelatedModuleSha256);
        }

        var resolutionEntries = resolutionPortfolio.Entries;
        var edgesByModule =
            new Dictionary<StaticFieldMetadataModuleIdentity, ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity>>();
        foreach (var resolutionEntry in resolutionEntries)
        {
            edgesByModule.Add(resolutionEntry.SourceModule, DeriveBaseEdges(resolutionEntry));
        }

        var coreSelection = SelectCoreRoles(resolutionEntries, edgesByModule, out var coreIssue, out var coreRelated);
        if (coreSelection is null)
        {
            var (resultKind, issue) = coreIssue switch
            {
                MetadataCoreRoleSelectionIssue.CoreModuleAbsent =>
                    (MetadataAncestryAuthorityPortfolioResultKind.NonExact,
                     MetadataAncestryAuthorityPortfolioIssue.CoreModuleAbsent),
                MetadataCoreRoleSelectionIssue.CoreModuleAmbiguous =>
                    (MetadataAncestryAuthorityPortfolioResultKind.Invalid,
                     MetadataAncestryAuthorityPortfolioIssue.CoreModuleAmbiguous),
                MetadataCoreRoleSelectionIssue.RoleDefinitionAbsent =>
                    (MetadataAncestryAuthorityPortfolioResultKind.NonExact,
                     MetadataAncestryAuthorityPortfolioIssue.RoleDefinitionAbsent),
                MetadataCoreRoleSelectionIssue.RoleDefinitionAmbiguous =>
                    (MetadataAncestryAuthorityPortfolioResultKind.Invalid,
                     MetadataAncestryAuthorityPortfolioIssue.RoleDefinitionAmbiguous),
                _ =>
                    (MetadataAncestryAuthorityPortfolioResultKind.Invalid,
                     MetadataAncestryAuthorityPortfolioIssue.RoleBaseEdgeMismatch),
            };
            return Stopped(resultKind, issue, resolutionPortfolio, resolutionEntries.Length, coreRelated);
        }

        var entries = ImmutableArray.CreateBuilder<MetadataAncestryAuthorityModuleIdentity>(resolutionEntries.Length);
        foreach (var resolutionEntry in resolutionEntries)
        {
            var edges = edgesByModule[resolutionEntry.SourceModule];
            var classifications =
                ImmutableArray.CreateBuilder<MetadataTypeDefinitionSemanticClassificationIdentity>(edges.Length);
            var chains =
                ImmutableArray.CreateBuilder<MetadataTypeDefinitionAncestryChainIdentity>(edges.Length);
            foreach (var edge in edges)
            {
                classifications.Add(ClassifyRow(edge, coreSelection));
                chains.Add(WalkAncestry(edge, edgesByModule, coreSelection));
            }
            entries.Add(MetadataAncestryAuthorityModuleIdentity.Create(
                RowMintCapability,
                resolutionEntry,
                edges,
                classifications.MoveToImmutable(),
                chains.MoveToImmutable()));
        }
        return new MetadataAncestryAuthorityPortfolioIdentity(
            MetadataAncestryAuthorityPortfolioResultKind.Exact,
            MetadataAncestryAuthorityPortfolioIssue.None,
            resolutionPortfolio,
            coreSelection,
            entries.MoveToImmutable(),
            0,
            null);
    }

    /// <summary>Looks up one semantic-classification draft row by module and non-nil TypeDef token.</summary>
    /// <param name="sourceModule">The exact loaded metadata module.</param>
    /// <param name="typeDefinitionToken">The exact TypeDef token within that module.</param>
    /// <returns>The complete draft row, or null when the portfolio stopped, module is absent, or token was not issued.</returns>
    public MetadataTypeDefinitionSemanticClassificationIdentity? ExactClassificationOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        return ExactEntryOrDefault(sourceModule)?.FindClassification(typeDefinitionToken);
    }

    /// <summary>Looks up one immediate-base draft edge by module and non-nil TypeDef token.</summary>
    /// <param name="sourceModule">The exact loaded metadata module.</param>
    /// <param name="typeDefinitionToken">The exact TypeDef token within that module.</param>
    /// <returns>The complete draft edge, or null when the portfolio stopped, module is absent, or token was not issued.</returns>
    public MetadataImmediateBaseEdgeAuthorityIdentity? ExactBaseEdgeOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        return ExactEntryOrDefault(sourceModule)?.FindBaseEdge(typeDefinitionToken);
    }

    /// <summary>Looks up one bounded ancestry draft chain by module and non-nil TypeDef token.</summary>
    /// <param name="sourceModule">The exact loaded metadata module.</param>
    /// <param name="typeDefinitionToken">The exact TypeDef token within that module.</param>
    /// <returns>The complete draft chain, or null when the portfolio stopped, module is absent, or token was not issued.</returns>
    public MetadataTypeDefinitionAncestryChainIdentity? ExactAncestryChainOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        return ExactEntryOrDefault(sourceModule)?.FindAncestryChain(typeDefinitionToken);
    }

    /// <summary>Tests canonical equality between two ancestry draft portfolios.</summary>
    /// <param name="other">The other draft portfolio.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataAncestryAuthorityPortfolioIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests ancestry draft portfolio equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a portfolio with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataAncestryAuthorityPortfolioIdentity);

    /// <summary>Computes a deterministic hash code from immutable draft portfolio content.</summary>
    /// <returns>A hash code for this canonical draft portfolio.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);

    private MetadataAncestryAuthorityModuleIdentity? ExactEntryOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule) =>
        ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Exact
            ? entries.FirstOrDefault(entry => entry.SourceModule.Equals(sourceModule))
            : null;

    private static ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity> DeriveBaseEdges(
        MetadataTypeReferenceResolutionModuleIdentity resolutionEntry)
    {
        var module = resolutionEntry.SourceModule;
        var typeDefinitions = resolutionEntry.ChainEntry.ChainCatalog.DefinitionAuthority.TypeDefinitions;
        var edges = ImmutableArray.CreateBuilder<MetadataImmediateBaseEdgeAuthorityIdentity>(typeDefinitions.Length);
        foreach (var typeDefinition in typeDefinitions)
        {
            var extendsToken = typeDefinition.TableRow.Observation.ExtendsMetadataToken;
            if (extendsToken is not { } token)
            {
                edges.Add(MetadataImmediateBaseEdgeAuthorityIdentity.Create(
                    RowMintCapability,
                    module,
                    typeDefinition,
                    MetadataImmediateBaseEdgeKind.None,
                    null,
                    null,
                    null,
                    null));
            }
            else if (CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x02))
            {
                var target = typeDefinitions[CanonicalReplayEncoding.MetadataTokenRowId(token) - 1];
                edges.Add(MetadataImmediateBaseEdgeAuthorityIdentity.Create(
                    RowMintCapability,
                    module,
                    typeDefinition,
                    MetadataImmediateBaseEdgeKind.TypeDefinition,
                    module,
                    target,
                    null,
                    null));
            }
            else if (CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x01))
            {
                var resolution = resolutionEntry.FindResolution(token)!;
                if (resolution.Disposition == MetadataTypeReferenceResolutionDispositionKind.Resolved)
                {
                    edges.Add(MetadataImmediateBaseEdgeAuthorityIdentity.Create(
                        RowMintCapability,
                        module,
                        typeDefinition,
                        MetadataImmediateBaseEdgeKind.TypeReference,
                        resolution.TargetModule,
                        resolution.TargetTypeDefinition,
                        resolution,
                        null));
                }
                else
                {
                    edges.Add(MetadataImmediateBaseEdgeAuthorityIdentity.Create(
                        RowMintCapability,
                        module,
                        typeDefinition,
                        MetadataImmediateBaseEdgeKind.TypeReferenceUnresolved,
                        null,
                        null,
                        resolution,
                        null));
                }
            }
            else
            {
                var genericBaseRow = resolutionEntry.ReferenceTables.TypeSpecifications.FindRow(token)!;
                edges.Add(MetadataImmediateBaseEdgeAuthorityIdentity.Create(
                    RowMintCapability,
                    module,
                    typeDefinition,
                    MetadataImmediateBaseEdgeKind.TypeSpecification,
                    null,
                    null,
                    null,
                    genericBaseRow));
            }
        }
        return edges.MoveToImmutable();
    }

    private static MetadataCoreRoleSelectionIdentity? SelectCoreRoles(
        ImmutableArray<MetadataTypeReferenceResolutionModuleIdentity> resolutionEntries,
        Dictionary<StaticFieldMetadataModuleIdentity, ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity>>
            edgesByModule,
        out MetadataCoreRoleSelectionIssue issue,
        out string? relatedModuleSha256)
    {
        var objectCandidates = new List<(StaticFieldMetadataModuleIdentity Module,
            MetadataTypeDefinitionAuthorityIdentity Definition)>();
        foreach (var resolutionEntry in resolutionEntries)
        {
            foreach (var typeDefinition in resolutionEntry.ChainEntry.ChainCatalog.DefinitionAuthority.TypeDefinitions)
            {
                if (typeDefinition.TypeDefinitionToken != ModulePseudoTypeToken &&
                    typeDefinition.EnclosingTypeDefinitionToken is null &&
                    typeDefinition.TableRow.Observation.ExtendsMetadataToken is null &&
                    (typeDefinition.TableRow.Observation.TypeAttributes & InterfaceFlag) == 0 &&
                    typeDefinition.NamespaceName == "System" &&
                    typeDefinition.TypeName == "Object")
                {
                    objectCandidates.Add((resolutionEntry.SourceModule, typeDefinition));
                }
            }
        }
        if (objectCandidates.Count == 0)
        {
            issue = MetadataCoreRoleSelectionIssue.CoreModuleAbsent;
            relatedModuleSha256 = null;
            return null;
        }
        if (objectCandidates.Count > 1)
        {
            issue = MetadataCoreRoleSelectionIssue.CoreModuleAmbiguous;
            relatedModuleSha256 = objectCandidates[0].Module.Sha256;
            return null;
        }

        var (coreModule, systemObject) = objectCandidates[0];
        var coreEntry = resolutionEntries.Single(entry => entry.SourceModule.Equals(coreModule));
        var coreTypes = coreEntry.ChainEntry.ChainCatalog.DefinitionAuthority.TypeDefinitions;
        var coreEdges = edgesByModule[coreModule];

        MetadataTypeDefinitionAuthorityIdentity? SelectRole(
            string typeName,
            MetadataTypeDefinitionAuthorityIdentity requiredBase,
            ref MetadataCoreRoleSelectionIssue selectionIssue)
        {
            var matches = coreTypes
                .Where(candidate =>
                    candidate.TypeDefinitionToken != ModulePseudoTypeToken &&
                    candidate.EnclosingTypeDefinitionToken is null &&
                    candidate.NamespaceName == "System" &&
                    candidate.TypeName == typeName)
                .ToArray();
            if (matches.Length == 0)
            {
                selectionIssue = MetadataCoreRoleSelectionIssue.RoleDefinitionAbsent;
                return null;
            }
            if (matches.Length > 1)
            {
                selectionIssue = MetadataCoreRoleSelectionIssue.RoleDefinitionAmbiguous;
                return null;
            }
            var edge = coreEdges[CanonicalReplayEncoding.MetadataTokenRowId(matches[0].TypeDefinitionToken) - 1];
            if (edge.TargetTypeDefinition is null ||
                !edge.TargetModule!.Equals(coreModule) ||
                !edge.TargetTypeDefinition.Equals(requiredBase))
            {
                selectionIssue = MetadataCoreRoleSelectionIssue.RoleBaseEdgeMismatch;
                return null;
            }
            return matches[0];
        }

        var roleIssue = MetadataCoreRoleSelectionIssue.None;
        var systemValueType = SelectRole("ValueType", systemObject, ref roleIssue);
        var systemEnum = systemValueType is null ? null : SelectRole("Enum", systemValueType, ref roleIssue);
        var systemDelegate = systemEnum is null ? null : SelectRole("Delegate", systemObject, ref roleIssue);
        var systemMulticastDelegate = systemDelegate is null
            ? null
            : SelectRole("MulticastDelegate", systemDelegate, ref roleIssue);
        if (systemMulticastDelegate is null)
        {
            issue = roleIssue;
            relatedModuleSha256 = coreModule.Sha256;
            return null;
        }

        issue = MetadataCoreRoleSelectionIssue.None;
        relatedModuleSha256 = null;
        return MetadataCoreRoleSelectionIdentity.Create(
            RowMintCapability,
            coreModule,
            systemObject,
            systemValueType!,
            systemEnum!,
            systemDelegate!,
            systemMulticastDelegate);
    }

    private static MetadataTypeDefinitionSemanticClassificationIdentity ClassifyRow(
        MetadataImmediateBaseEdgeAuthorityIdentity edge,
        MetadataCoreRoleSelectionIdentity coreRoles)
    {
        var observation = edge.Owner.TableRow.Observation;
        var isInterface = (observation.TypeAttributes & InterfaceFlag) != 0;
        if (edge.Owner.TypeDefinitionToken == ModulePseudoTypeToken)
        {
            return Row(edge, MetadataTypeDefinitionSemanticRole.ModulePseudoType);
        }
        if (isInterface)
        {
            return edge.Kind == MetadataImmediateBaseEdgeKind.None
                ? Row(edge, MetadataTypeDefinitionSemanticRole.Interface)
                : Row(edge, null, MetadataSemanticClassificationIssue.InterfaceExtendsInvalid);
        }
        if (edge.Kind == MetadataImmediateBaseEdgeKind.None)
        {
            return coreRoles.IsRoleDefinition(edge.SourceModule, edge.Owner)
                ? Row(edge, MetadataTypeDefinitionSemanticRole.Class)
                : Row(edge, null, MetadataSemanticClassificationIssue.BaseEdgeMissing);
        }
        if (edge.Kind == MetadataImmediateBaseEdgeKind.TypeReferenceUnresolved)
        {
            return Row(edge, null, MetadataSemanticClassificationIssue.BaseReferenceUnresolved);
        }
        if (edge.Kind == MetadataImmediateBaseEdgeKind.TypeSpecification)
        {
            return Row(edge, MetadataTypeDefinitionSemanticRole.Class);
        }
        if (coreRoles.IsRoleDefinition(edge.SourceModule, edge.Owner))
        {
            return Row(edge, MetadataTypeDefinitionSemanticRole.Class);
        }
        var target = edge.TargetTypeDefinition!;
        if (edge.TargetModule!.Equals(coreRoles.CoreModule))
        {
            if (target.Equals(coreRoles.SystemValueType))
            {
                return Row(edge, MetadataTypeDefinitionSemanticRole.ValueType);
            }
            if (target.Equals(coreRoles.SystemEnum))
            {
                return Row(edge, MetadataTypeDefinitionSemanticRole.Enum);
            }
            if (target.Equals(coreRoles.SystemMulticastDelegate))
            {
                return Row(edge, MetadataTypeDefinitionSemanticRole.Delegate);
            }
        }
        return Row(edge, MetadataTypeDefinitionSemanticRole.Class);
    }

    private static MetadataTypeDefinitionAncestryChainIdentity WalkAncestry(
        MetadataImmediateBaseEdgeAuthorityIdentity subjectEdge,
        Dictionary<StaticFieldMetadataModuleIdentity, ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity>>
            edgesByModule,
        MetadataCoreRoleSelectionIdentity coreRoles)
    {
        var traversed = ImmutableArray.CreateBuilder<MetadataImmediateBaseEdgeAuthorityIdentity>();
        var visited = new HashSet<(string, int)>
        {
            (subjectEdge.SourceModule.Sha256, subjectEdge.Owner.TypeDefinitionToken),
        };
        var current = subjectEdge;
        while (true)
        {
            switch (current.Kind)
            {
                case MetadataImmediateBaseEdgeKind.None:
                    var observation = current.Owner.TableRow.Observation;
                    var terminal = current.Owner.TypeDefinitionToken == ModulePseudoTypeToken
                        ? MetadataAncestryChainTerminalKind.ModulePseudoTypeRoot
                        : (observation.TypeAttributes & InterfaceFlag) != 0
                            ? MetadataAncestryChainTerminalKind.InterfaceRoot
                            : current.SourceModule.Equals(coreRoles.CoreModule) &&
                              current.Owner.Equals(coreRoles.SystemObject)
                                ? MetadataAncestryChainTerminalKind.SystemObjectReached
                                : MetadataAncestryChainTerminalKind.MissingBaseInvalid;
                    return Chain(subjectEdge, traversed, terminal, null, null);
                case MetadataImmediateBaseEdgeKind.TypeSpecification:
                    return Chain(
                        subjectEdge,
                        traversed,
                        MetadataAncestryChainTerminalKind.GenericBaseReached,
                        null,
                        current.ExtendsMetadataToken);
                case MetadataImmediateBaseEdgeKind.TypeReferenceUnresolved:
                    return Chain(
                        subjectEdge,
                        traversed,
                        MetadataAncestryChainTerminalKind.UnresolvedReferenceReached,
                        null,
                        current.ExtendsMetadataToken);
                default:
                    var targetModule = current.TargetModule!;
                    var target = current.TargetTypeDefinition!;
                    if (!visited.Add((targetModule.Sha256, target.TypeDefinitionToken)))
                    {
                        return Chain(
                            subjectEdge,
                            traversed,
                            MetadataAncestryChainTerminalKind.CycleDetected,
                            null,
                            target.TypeDefinitionToken);
                    }
                    if (traversed.Count + 1 > MaximumAncestryDepth)
                    {
                        return Chain(
                            subjectEdge,
                            traversed,
                            MetadataAncestryChainTerminalKind.DepthBoundReached,
                            new EvaluationDeterministicBound(
                                ExpressionV2ContractLimits.ConstructedAncestryDepthBoundName,
                                MaximumAncestryDepth),
                            target.TypeDefinitionToken);
                    }
                    var nextEdges = edgesByModule[targetModule];
                    current = nextEdges[CanonicalReplayEncoding.MetadataTokenRowId(target.TypeDefinitionToken) - 1];
                    traversed.Add(current);
                    break;
            }
        }
    }

    private static MetadataTypeDefinitionSemanticClassificationIdentity Row(
        MetadataImmediateBaseEdgeAuthorityIdentity edge,
        MetadataTypeDefinitionSemanticRole? role,
        MetadataSemanticClassificationIssue issue = MetadataSemanticClassificationIssue.None) =>
        MetadataTypeDefinitionSemanticClassificationIdentity.Create(RowMintCapability, edge, role, issue);

    private static MetadataTypeDefinitionAncestryChainIdentity Chain(
        MetadataImmediateBaseEdgeAuthorityIdentity subjectEdge,
        ImmutableArray<MetadataImmediateBaseEdgeAuthorityIdentity>.Builder traversed,
        MetadataAncestryChainTerminalKind terminalKind,
        EvaluationDeterministicBound? reachedBound,
        int? relatedMetadataToken) =>
        MetadataTypeDefinitionAncestryChainIdentity.Create(
            RowMintCapability,
            subjectEdge,
            traversed.ToImmutable(),
            terminalKind,
            reachedBound,
            relatedMetadataToken);

    private static MetadataAncestryAuthorityPortfolioIdentity Stopped(
        MetadataAncestryAuthorityPortfolioResultKind resultKind,
        MetadataAncestryAuthorityPortfolioIssue issue,
        MetadataTypeReferenceResolutionPortfolioIdentity resolutionPortfolio,
        int observedCount,
        string? relatedModuleSha256) =>
        new(
            resultKind,
            issue,
            resolutionPortfolio,
            null,
            [],
            observedCount,
            relatedModuleSha256);
}
