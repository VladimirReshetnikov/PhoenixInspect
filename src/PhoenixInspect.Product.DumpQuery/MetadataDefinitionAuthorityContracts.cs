using System.Collections.Immutable;
using System.Reflection;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete W8 definition-authority join.</summary>
/// <remarks>
/// This draft discriminator separates a complete cross-catalog result from a prerequisite stop and contradictory
/// complete evidence. Every result other than <see cref="Exact"/> exposes no authority row prefix.
/// </remarks>
public enum MetadataDefinitionAuthorityResultKind
{
    /// <summary>Every prerequisite and every cross-catalog definition relation is exact.</summary>
    Exact = 1,

    /// <summary>At least one required complete prerequisite stopped before an exact result.</summary>
    NonExact = 2,

    /// <summary>The supplied catalogs or their complete cross-catalog relations are contradictory.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one W8 definition-authority join.</summary>
/// <remarks>
/// The draft issue remains explicit when no authority rows are exposed. The values describe physical definition
/// composition only; compiler-name projection and evaluator admission are separate later contracts.
/// </remarks>
public enum MetadataDefinitionAuthorityIssue
{
    /// <summary>No issue applies to an exact complete authority catalog.</summary>
    None = 0,

    /// <summary>The complete TypeDef prerequisite stopped without an exact row catalog.</summary>
    TypeDefinitionCatalogNonExact = 1,

    /// <summary>The complete TypeDef prerequisite retained contradictory evidence.</summary>
    TypeDefinitionCatalogInvalid = 2,

    /// <summary>The NestedClass prerequisite named different exact metadata source ends.</summary>
    NestedClassSourceMismatch = 3,

    /// <summary>The GenericParam prerequisite named different exact metadata source ends.</summary>
    GenericParameterSourceMismatch = 4,

    /// <summary>The MethodDef prerequisite named different exact metadata source ends.</summary>
    MethodDefinitionSourceMismatch = 5,

    /// <summary>The NestedClass prerequisite was derived from a different TypeDef catalog.</summary>
    NestedClassTypeDefinitionCatalogMismatch = 6,

    /// <summary>The MethodDef prerequisite was derived from a different TypeDef catalog.</summary>
    MethodDefinitionTypeDefinitionCatalogMismatch = 7,

    /// <summary>The complete NestedClass prerequisite stopped without an exact parent map.</summary>
    NestedClassCatalogNonExact = 8,

    /// <summary>The complete NestedClass prerequisite retained contradictory evidence.</summary>
    NestedClassCatalogInvalid = 9,

    /// <summary>The complete GenericParam prerequisite stopped without an exact owner catalog.</summary>
    GenericParameterCatalogNonExact = 10,

    /// <summary>The complete GenericParam prerequisite retained contradictory evidence.</summary>
    GenericParameterCatalogInvalid = 11,

    /// <summary>The complete MethodDef prerequisite stopped without an exact declaration catalog.</summary>
    MethodDefinitionCatalogNonExact = 12,

    /// <summary>The complete MethodDef prerequisite retained contradictory evidence.</summary>
    MethodDefinitionCatalogInvalid = 13,

    /// <summary>A MethodDef GenericParam group cardinality disagreed with its decoded signature arity.</summary>
    MethodDefinitionGenericParameterArityMismatch = 14,

    /// <summary>TypeDef RID one did not carry the exact module pseudo-type name.</summary>
    ModuleTypeNameMismatch = 15,

    /// <summary>TypeDef RID one did not carry the empty module pseudo-type namespace.</summary>
    ModuleTypeNamespaceMismatch = 16,

    /// <summary>TypeDef RID one carried a non-nil Extends token.</summary>
    ModuleTypeExtendsMismatch = 17,

    /// <summary>TypeDef RID one did not carry top-level NotPublic visibility.</summary>
    ModuleTypeVisibilityMismatch = 18,

    /// <summary>TypeDef RID one did not carry class semantics.</summary>
    ModuleTypeClassSemanticsMismatch = 19,

    /// <summary>TypeDef RID one appeared as a child in the exact NestedClass parent map.</summary>
    ModuleTypeNestingMismatch = 20,

    /// <summary>TypeDef RID one owned one or more physical GenericParam rows.</summary>
    ModuleTypeGenericParameterMismatch = 21,
}

/// <summary>Freezes one TypeDef after complete cross-catalog physical-authority composition.</summary>
/// <remarks>
/// This sealed draft identity can be minted only by an exact <see cref="MetadataDefinitionAuthorityCatalogIdentity"/>.
/// It retains physical ownership, parent, depth, and total GenericParam arity facts. It makes no compiler-name,
/// source-addressability, semantic-kind, or evaluator-admission assertion.
/// </remarks>
public sealed class MetadataTypeDefinitionAuthorityIdentity :
    IEquatable<MetadataTypeDefinitionAuthorityIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typedef-authority";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataGenericParameterTableRowIdentity> genericParameters;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefinitionAuthorityIdentity(
        MetadataTypeDefinitionTableRowIdentity tableRow,
        int? enclosingTypeDefinitionToken,
        int nestingDepth,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> genericParameters)
    {
        TableRow = tableRow;
        EnclosingTypeDefinitionToken = enclosingTypeDefinitionToken;
        NestingDepth = nestingDepth;
        this.genericParameters = ExpressionV2ContractEncoding.Copy(genericParameters);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(tableRow.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, enclosingTypeDefinitionToken);
        writer.WriteInt32(nestingDepth);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            genericParameters,
            static row => row.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact complete-table TypeDef row from which this authority identity was composed.</summary>
    public MetadataTypeDefinitionTableRowIdentity TableRow { get; }

    /// <summary>Gets the exact source ends shared by every prerequisite used to compose this draft identity.</summary>
    public MetadataSourceEndIdentity SourceEnds => TableRow.SourceEnds;

    /// <summary>Gets the exact non-nil TypeDef token.</summary>
    public int TypeDefinitionToken => TableRow.TypeDefinitionToken;

    /// <summary>Gets the decoded namespace retained by the physical TypeDef observation.</summary>
    public string NamespaceName => TableRow.Observation.NamespaceName;

    /// <summary>Gets the uninterpreted decoded metadata name retained by the physical TypeDef observation.</summary>
    public string TypeName => TableRow.Observation.TypeName;

    /// <summary>Gets the enclosing TypeDef token derived from the exact parent map, or null for a top-level type.</summary>
    public int? EnclosingTypeDefinitionToken { get; }

    /// <summary>Gets the exact parent-link count to the top-level TypeDef root.</summary>
    public int NestingDepth { get; }

    /// <summary>Gets whether this TypeDef has an exact enclosing TypeDef relation.</summary>
    public bool IsNested => EnclosingTypeDefinitionToken.HasValue;

    /// <summary>Gets a defensive Number-ordered copy of all TypeDef-owned GenericParam rows.</summary>
    public ImmutableArray<MetadataGenericParameterTableRowIdentity> GenericParameters =>
        ExpressionV2ContractEncoding.Copy(genericParameters);

    /// <summary>
    /// Gets the complete physical TypeDef arity, including enclosing positions redeclared by a nested TypeDef.
    /// </summary>
    public int TotalGenericArity => genericParameters.Length;

    /// <summary>Gets a defensive ownership-order copy of exact FieldDef tokens owned by this TypeDef.</summary>
    public ImmutableArray<int> FieldDefinitionTokens => TableRow.FieldDefinitionTokens;

    /// <summary>Gets a defensive ownership-order copy of exact MethodDef tokens owned by this TypeDef.</summary>
    public ImmutableArray<int> MethodDefinitionTokens => TableRow.MethodDefinitionTokens;

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two authority-issued TypeDef draft identities.</summary>
    /// <param name="other">The other authority-issued TypeDef draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeDefinitionAuthorityIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-issued TypeDef draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeDefinitionAuthorityIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft content.</summary>
    /// <returns>A hash code for this authority-issued TypeDef draft identity.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeDefinitionAuthorityIdentity Create(
        object mintCapability,
        MetadataTypeDefinitionTableRowIdentity tableRow,
        int? enclosingTypeDefinitionToken,
        int nestingDepth,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> genericParameters)
    {
        if (!MetadataDefinitionAuthorityCatalogIdentity.OwnsTypeRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A TypeDef authority row requires the exact definition catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(tableRow);
        if (enclosingTypeDefinitionToken.HasValue)
        {
            CanonicalReplayEncoding.ValidateMetadataToken(
                enclosingTypeDefinitionToken.Value,
                0x02,
                nameof(enclosingTypeDefinitionToken));
            if (nestingDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nestingDepth));
            }
        }
        else if (nestingDepth != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nestingDepth));
        }
        if (genericParameters.IsDefault)
        {
            throw new ArgumentException("GenericParam rows must be initialized.", nameof(genericParameters));
        }

        return new MetadataTypeDefinitionAuthorityIdentity(
            tableRow,
            enclosingTypeDefinitionToken,
            nestingDepth,
            genericParameters);
    }
}

/// <summary>Freezes one MethodDef after complete cross-catalog physical-authority composition.</summary>
/// <remarks>
/// This sealed draft identity can be minted only by an exact <see cref="MetadataDefinitionAuthorityCatalogIdentity"/>.
/// The declaring TypeDef comes from pointer-aware ownership, while GenericParam positions must agree exactly with the
/// shared-grammar signature arity. It makes no method-body or evaluator-admission assertion.
/// </remarks>
public sealed class MetadataMethodDefinitionAuthorityIdentity :
    IEquatable<MetadataMethodDefinitionAuthorityIdentity>
{
    private const string CanonicalDomain = "metadata-v2-methoddef-authority";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataGenericParameterTableRowIdentity> genericParameters;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataMethodDefinitionAuthorityIdentity(
        MetadataMethodDefinitionTableRowIdentity tableRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> genericParameters)
    {
        TableRow = tableRow;
        DeclaringTypeDefinition = declaringTypeDefinition;
        this.genericParameters = ExpressionV2ContractEncoding.Copy(genericParameters);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(tableRow.CanonicalBytes.AsSpan());
        writer.WriteString(declaringTypeDefinition.Sha256);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            genericParameters,
            static row => row.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact complete-table MethodDef row from which this authority identity was composed.</summary>
    public MetadataMethodDefinitionTableRowIdentity TableRow { get; }

    /// <summary>Gets the authority-issued TypeDef that owns this exact MethodDef.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the exact source ends shared by every prerequisite used to compose this draft identity.</summary>
    public MetadataSourceEndIdentity SourceEnds => TableRow.SourceEnds;

    /// <summary>Gets the exact non-nil MethodDef token.</summary>
    public int MethodDefinitionToken => TableRow.MethodDefinitionToken;

    /// <summary>Gets the exact pointer-aware declaring TypeDef token.</summary>
    public int DeclaringTypeDefinitionToken => DeclaringTypeDefinition.TypeDefinitionToken;

    /// <summary>Gets a defensive Number-ordered copy of all MethodDef-owned GenericParam rows.</summary>
    public ImmutableArray<MetadataGenericParameterTableRowIdentity> GenericParameters =>
        ExpressionV2ContractEncoding.Copy(genericParameters);

    /// <summary>Gets the exact generic arity jointly proven by signature and physical GenericParam rows.</summary>
    public int DeclaredGenericArity => TableRow.DeclaredGenericArity;

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two authority-issued MethodDef draft identities.</summary>
    /// <param name="other">The other authority-issued MethodDef draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataMethodDefinitionAuthorityIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests authority-issued MethodDef draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataMethodDefinitionAuthorityIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft content.</summary>
    /// <returns>A hash code for this authority-issued MethodDef draft identity.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataMethodDefinitionAuthorityIdentity Create(
        object mintCapability,
        MetadataMethodDefinitionTableRowIdentity tableRow,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> genericParameters)
    {
        if (!MetadataDefinitionAuthorityCatalogIdentity.OwnsMethodRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A MethodDef authority row requires the exact definition catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(tableRow);
        ArgumentNullException.ThrowIfNull(declaringTypeDefinition);
        if (tableRow.DeclaringTypeDefinitionToken != declaringTypeDefinition.TypeDefinitionToken)
        {
            throw new ArgumentException("The MethodDef and authority-issued declaring TypeDef must agree.");
        }
        if (genericParameters.IsDefault || genericParameters.Length != tableRow.DeclaredGenericArity)
        {
            throw new ArgumentException(
                "The initialized MethodDef GenericParam rows must agree with signature arity.",
                nameof(genericParameters));
        }

        return new MetadataMethodDefinitionAuthorityIdentity(
            tableRow,
            declaringTypeDefinition,
            genericParameters);
    }
}

/// <summary>Freezes the complete W8 cross-catalog physical definition authority for one metadata source.</summary>
/// <remarks>
/// This sealed draft catalog is the only issuer of TypeDef and MethodDef authority rows in this contract family. It
/// composes complete TypeDef ownership, NestedClass parents, physical GenericParam owners, and MethodDef signatures
/// from identical source ends. Every prerequisite and global invariant is checked before any issued row is retained.
/// Compiler-name mapping and evaluator admission remain separate later contracts.
/// </remarks>
public sealed class MetadataDefinitionAuthorityCatalogIdentity :
    IEquatable<MetadataDefinitionAuthorityCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-definition-authority-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object TypeRowMintCapability = new();
    private static readonly object MethodRowMintCapability = new();
    private readonly ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> typeDefinitions;
    private readonly ImmutableArray<MetadataMethodDefinitionAuthorityIdentity> methodDefinitions;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataDefinitionAuthorityCatalogIdentity(
        MetadataDefinitionAuthorityResultKind resultKind,
        MetadataDefinitionAuthorityIssue issue,
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataNestedClassTableCatalogIdentity nestedClassCatalog,
        MetadataGenericParameterPhysicalTableCatalogIdentity genericParameterCatalog,
        MetadataMethodDefinitionTableCatalogIdentity methodDefinitionCatalog,
        ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> typeDefinitions,
        ImmutableArray<MetadataMethodDefinitionAuthorityIdentity> methodDefinitions,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        TypeDefinitionCatalog = typeDefinitionCatalog;
        NestedClassCatalog = nestedClassCatalog;
        GenericParameterCatalog = genericParameterCatalog;
        MethodDefinitionCatalog = methodDefinitionCatalog;
        this.typeDefinitions = ExpressionV2ContractEncoding.Copy(typeDefinitions);
        this.methodDefinitions = ExpressionV2ContractEncoding.Copy(methodDefinitions);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(typeDefinitionCatalog.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(nestedClassCatalog.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(genericParameterCatalog.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(methodDefinitionCatalog.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            typeDefinitions,
            static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            methodDefinitions,
            static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft authority join is exact, non-exact, or invalid.</summary>
    public MetadataDefinitionAuthorityResultKind ResultKind { get; }

    /// <summary>Gets the typed draft authority issue, or none for an exact result.</summary>
    public MetadataDefinitionAuthorityIssue Issue { get; }

    /// <summary>Gets the complete TypeDef ownership prerequisite retained by this outcome.</summary>
    public MetadataTypeDefinitionTableCatalogIdentity TypeDefinitionCatalog { get; }

    /// <summary>Gets the complete NestedClass parent-map prerequisite retained by this outcome.</summary>
    public MetadataNestedClassTableCatalogIdentity NestedClassCatalog { get; }

    /// <summary>Gets the complete physical GenericParam prerequisite retained by this outcome.</summary>
    public MetadataGenericParameterPhysicalTableCatalogIdentity GenericParameterCatalog { get; }

    /// <summary>Gets the complete MethodDef declaration prerequisite retained by this outcome.</summary>
    public MetadataMethodDefinitionTableCatalogIdentity MethodDefinitionCatalog { get; }

    /// <summary>
    /// Gets the TypeDef prerequisite's exact source ends, which are shared by all prerequisites for same-source and
    /// exact outcomes.
    /// </summary>
    public MetadataSourceEndIdentity SourceEnds => TypeDefinitionCatalog.SourceEnds;

    /// <summary>Gets a defensive RID-order copy of exact TypeDef authority rows, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> TypeDefinitions =>
        ExpressionV2ContractEncoding.Copy(typeDefinitions);

    /// <summary>Gets a defensive RID-order copy of exact MethodDef authority rows, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataMethodDefinitionAuthorityIdentity> MethodDefinitions =>
        ExpressionV2ContractEncoding.Copy(methodDefinitions);

    /// <summary>Gets the exact essential module pseudo-type authority row, or null for every non-exact result.</summary>
    /// <remarks>
    /// Exactness proves the decoded empty namespace, but not that its original string-heap index was nil. This join
    /// does not consume InterfaceImpl rows and therefore does not prove their absence. Global fields and methods are
    /// permitted; whether each such declaration has static semantics requires its own complete projection.
    /// </remarks>
    public MetadataTypeDefinitionAuthorityIdentity? EssentialModuleTypeDefinition =>
        ResultKind == MetadataDefinitionAuthorityResultKind.Exact && !typeDefinitions.IsEmpty
            ? typeDefinitions[0]
            : null;

    /// <summary>Gets a prerequisite operation bound propagated by a non-exact result, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related prerequisite, owner-group, or arity count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the TypeDef, MethodDef, or GenericParam-owner token related to the issue, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete cross-catalog physical definition-authority draft result.</summary>
    /// <param name="typeDefinitionCatalog">The complete pointer-aware TypeDef ownership prerequisite.</param>
    /// <param name="nestedClassCatalog">The complete TypeDef-correlated NestedClass parent-map prerequisite.</param>
    /// <param name="genericParameterCatalog">The complete token-only physical GenericParam prerequisite.</param>
    /// <param name="methodDefinitionCatalog">The complete TypeDef-correlated MethodDef declaration prerequisite.</param>
    /// <returns>
    /// An exact catalog with guarded authority rows, a prefix-free prerequisite stop, or a factless invalid result.
    /// </returns>
    public static MetadataDefinitionAuthorityCatalogIdentity Create(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataNestedClassTableCatalogIdentity nestedClassCatalog,
        MetadataGenericParameterPhysicalTableCatalogIdentity genericParameterCatalog,
        MetadataMethodDefinitionTableCatalogIdentity methodDefinitionCatalog)
    {
        ArgumentNullException.ThrowIfNull(typeDefinitionCatalog);
        ArgumentNullException.ThrowIfNull(nestedClassCatalog);
        ArgumentNullException.ThrowIfNull(genericParameterCatalog);
        ArgumentNullException.ThrowIfNull(methodDefinitionCatalog);

        if (!typeDefinitionCatalog.SourceEnds.Equals(nestedClassCatalog.SourceEnds))
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.NestedClassSourceMismatch,
                nestedClassCatalog.ObservedCount,
                null);
        }
        if (!typeDefinitionCatalog.SourceEnds.Equals(genericParameterCatalog.SourceEnds))
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.GenericParameterSourceMismatch,
                genericParameterCatalog.ObservedCount,
                null);
        }
        if (!typeDefinitionCatalog.SourceEnds.Equals(methodDefinitionCatalog.SourceEnds))
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.MethodDefinitionSourceMismatch,
                methodDefinitionCatalog.ObservedCount,
                methodDefinitionCatalog.StoppedMethodDefinitionToken);
        }
        if (!nestedClassCatalog.TypeDefinitionCatalog.Equals(typeDefinitionCatalog))
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.NestedClassTypeDefinitionCatalogMismatch,
                nestedClassCatalog.ObservedCount,
                null);
        }
        if (!methodDefinitionCatalog.TypeDefinitionCatalog.Equals(typeDefinitionCatalog))
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.MethodDefinitionTypeDefinitionCatalogMismatch,
                methodDefinitionCatalog.ObservedCount,
                methodDefinitionCatalog.StoppedMethodDefinitionToken);
        }

        if (typeDefinitionCatalog.ResultKind == MetadataTypeDefinitionTableResultKind.NonExact)
        {
            return NonExact(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.TypeDefinitionCatalogNonExact,
                typeDefinitionCatalog.ReachedBound,
                typeDefinitionCatalog.ObservedCount,
                null);
        }
        if (typeDefinitionCatalog.ResultKind == MetadataTypeDefinitionTableResultKind.Invalid)
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.TypeDefinitionCatalogInvalid,
                typeDefinitionCatalog.ObservedCount,
                null);
        }
        if (nestedClassCatalog.ResultKind == MetadataNestedClassTableResultKind.NonExact)
        {
            return NonExact(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.NestedClassCatalogNonExact,
                nestedClassCatalog.ReachedBound,
                nestedClassCatalog.ObservedCount,
                null);
        }
        if (nestedClassCatalog.ResultKind == MetadataNestedClassTableResultKind.Invalid)
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.NestedClassCatalogInvalid,
                nestedClassCatalog.ObservedCount,
                null);
        }
        if (genericParameterCatalog.ResultKind == MetadataGenericParameterPhysicalTableResultKind.NonExact)
        {
            return NonExact(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.GenericParameterCatalogNonExact,
                genericParameterCatalog.ReachedBound,
                genericParameterCatalog.ObservedCount,
                null);
        }
        if (genericParameterCatalog.ResultKind == MetadataGenericParameterPhysicalTableResultKind.Invalid)
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.GenericParameterCatalogInvalid,
                genericParameterCatalog.ObservedCount,
                null);
        }
        if (methodDefinitionCatalog.ResultKind == MetadataMethodDefinitionTableResultKind.NonExact)
        {
            return NonExact(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.MethodDefinitionCatalogNonExact,
                methodDefinitionCatalog.ReachedBound,
                methodDefinitionCatalog.ObservedCount,
                methodDefinitionCatalog.StoppedMethodDefinitionToken);
        }
        if (methodDefinitionCatalog.ResultKind == MetadataMethodDefinitionTableResultKind.Invalid)
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                MetadataDefinitionAuthorityIssue.MethodDefinitionCatalogInvalid,
                methodDefinitionCatalog.ObservedCount,
                methodDefinitionCatalog.StoppedMethodDefinitionToken);
        }

        var typeRows = typeDefinitionCatalog.Rows;
        var methodRows = methodDefinitionCatalog.Rows;
        var relationByNestedToken = nestedClassCatalog.Relations.ToDictionary(
            static relation => relation.NestedTypeDefinition.TypeDefinitionToken,
            static relation => relation);
        var pendingTypes = new PendingType[typeRows.Length];
        for (var index = 0; index < typeRows.Length; index++)
        {
            var row = typeRows[index];
            var genericRows = genericParameterCatalog.RowsForOwnerOrEmpty(
                MetadataGenericParameterOwnerKind.TypeDefinition,
                row.TypeDefinitionToken);
            relationByNestedToken.TryGetValue(row.TypeDefinitionToken, out var relation);
            pendingTypes[index] = new PendingType(
                row,
                relation?.EnclosingTypeDefinition.TypeDefinitionToken,
                relation?.NestingDepth ?? 0,
                genericRows);
        }

        var moduleIssue = ValidateModuleType(pendingTypes[0]);
        if (moduleIssue != MetadataDefinitionAuthorityIssue.None)
        {
            return Invalid(
                typeDefinitionCatalog,
                nestedClassCatalog,
                genericParameterCatalog,
                methodDefinitionCatalog,
                moduleIssue,
                pendingTypes[0].GenericParameters.Length,
                pendingTypes[0].TableRow.TypeDefinitionToken);
        }

        var pendingMethods = new PendingMethod[methodRows.Length];
        for (var index = 0; index < methodRows.Length; index++)
        {
            var row = methodRows[index];
            var genericRows = genericParameterCatalog.RowsForOwnerOrEmpty(
                MetadataGenericParameterOwnerKind.MethodDefinition,
                row.MethodDefinitionToken);
            if (genericRows.Length != row.DeclaredGenericArity)
            {
                return Invalid(
                    typeDefinitionCatalog,
                    nestedClassCatalog,
                    genericParameterCatalog,
                    methodDefinitionCatalog,
                    MetadataDefinitionAuthorityIssue.MethodDefinitionGenericParameterArityMismatch,
                    genericRows.Length,
                    row.MethodDefinitionToken);
            }
            pendingMethods[index] = new PendingMethod(row, genericRows);
        }

        var typeBuilder = ImmutableArray.CreateBuilder<MetadataTypeDefinitionAuthorityIdentity>(pendingTypes.Length);
        var authorityTypesByToken = new Dictionary<int, MetadataTypeDefinitionAuthorityIdentity>(pendingTypes.Length);
        foreach (var pending in pendingTypes)
        {
            var authority = MetadataTypeDefinitionAuthorityIdentity.Create(
                TypeRowMintCapability,
                pending.TableRow,
                pending.EnclosingTypeDefinitionToken,
                pending.NestingDepth,
                pending.GenericParameters);
            typeBuilder.Add(authority);
            authorityTypesByToken.Add(authority.TypeDefinitionToken, authority);
        }

        var methodBuilder =
            ImmutableArray.CreateBuilder<MetadataMethodDefinitionAuthorityIdentity>(pendingMethods.Length);
        foreach (var pending in pendingMethods)
        {
            methodBuilder.Add(MetadataMethodDefinitionAuthorityIdentity.Create(
                MethodRowMintCapability,
                pending.TableRow,
                authorityTypesByToken[pending.TableRow.DeclaringTypeDefinitionToken],
                pending.GenericParameters));
        }

        return new MetadataDefinitionAuthorityCatalogIdentity(
            MetadataDefinitionAuthorityResultKind.Exact,
            MetadataDefinitionAuthorityIssue.None,
            typeDefinitionCatalog,
            nestedClassCatalog,
            genericParameterCatalog,
            methodDefinitionCatalog,
            typeBuilder.MoveToImmutable(),
            methodBuilder.MoveToImmutable(),
            null,
            0,
            null);
    }

    /// <summary>Tests canonical equality between two complete definition-authority draft results.</summary>
    /// <param name="other">The other complete draft result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataDefinitionAuthorityCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete definition-authority draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a result with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataDefinitionAuthorityCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical draft content.</summary>
    /// <returns>A hash code for this complete definition-authority draft result.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsTypeRowMintCapability(object? capability) =>
        ReferenceEquals(capability, TypeRowMintCapability);

    internal static bool OwnsMethodRowMintCapability(object? capability) =>
        ReferenceEquals(capability, MethodRowMintCapability);

    internal MetadataTypeDefinitionAuthorityIdentity? ExactTypeDefinitionOrDefault(int typeDefinitionToken)
    {
        if (ResultKind != MetadataDefinitionAuthorityResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(typeDefinitionToken, 0x02))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinitionToken);
        return rowId > 0 && rowId <= typeDefinitions.Length
            ? typeDefinitions[rowId - 1]
            : null;
    }

    internal MetadataMethodDefinitionAuthorityIdentity? ExactMethodDefinitionOrDefault(int methodDefinitionToken)
    {
        if (ResultKind != MetadataDefinitionAuthorityResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(methodDefinitionToken, 0x06))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(methodDefinitionToken);
        return rowId > 0 && rowId <= methodDefinitions.Length
            ? methodDefinitions[rowId - 1]
            : null;
    }

    private static MetadataDefinitionAuthorityIssue ValidateModuleType(PendingType module)
    {
        var observation = module.TableRow.Observation;
        if (!StringComparer.Ordinal.Equals(observation.TypeName, "<Module>"))
        {
            return MetadataDefinitionAuthorityIssue.ModuleTypeNameMismatch;
        }
        if (observation.NamespaceName.Length != 0)
        {
            return MetadataDefinitionAuthorityIssue.ModuleTypeNamespaceMismatch;
        }
        if (observation.ExtendsMetadataToken.HasValue)
        {
            return MetadataDefinitionAuthorityIssue.ModuleTypeExtendsMismatch;
        }
        if (module.EnclosingTypeDefinitionToken.HasValue || module.NestingDepth != 0)
        {
            return MetadataDefinitionAuthorityIssue.ModuleTypeNestingMismatch;
        }

        var attributes = (TypeAttributes)observation.TypeAttributes;
        if ((attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NotPublic)
        {
            return MetadataDefinitionAuthorityIssue.ModuleTypeVisibilityMismatch;
        }
        if ((attributes & TypeAttributes.ClassSemanticsMask) != TypeAttributes.Class)
        {
            return MetadataDefinitionAuthorityIssue.ModuleTypeClassSemanticsMismatch;
        }
        if (!module.GenericParameters.IsEmpty)
        {
            return MetadataDefinitionAuthorityIssue.ModuleTypeGenericParameterMismatch;
        }
        return MetadataDefinitionAuthorityIssue.None;
    }

    private static MetadataDefinitionAuthorityCatalogIdentity NonExact(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataNestedClassTableCatalogIdentity nestedClassCatalog,
        MetadataGenericParameterPhysicalTableCatalogIdentity genericParameterCatalog,
        MetadataMethodDefinitionTableCatalogIdentity methodDefinitionCatalog,
        MetadataDefinitionAuthorityIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            MetadataDefinitionAuthorityResultKind.NonExact,
            issue,
            typeDefinitionCatalog,
            nestedClassCatalog,
            genericParameterCatalog,
            methodDefinitionCatalog,
            ImmutableArray<MetadataTypeDefinitionAuthorityIdentity>.Empty,
            ImmutableArray<MetadataMethodDefinitionAuthorityIdentity>.Empty,
            reachedBound,
            observedCount,
            relatedMetadataToken);

    private static MetadataDefinitionAuthorityCatalogIdentity Invalid(
        MetadataTypeDefinitionTableCatalogIdentity typeDefinitionCatalog,
        MetadataNestedClassTableCatalogIdentity nestedClassCatalog,
        MetadataGenericParameterPhysicalTableCatalogIdentity genericParameterCatalog,
        MetadataMethodDefinitionTableCatalogIdentity methodDefinitionCatalog,
        MetadataDefinitionAuthorityIssue issue,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            MetadataDefinitionAuthorityResultKind.Invalid,
            issue,
            typeDefinitionCatalog,
            nestedClassCatalog,
            genericParameterCatalog,
            methodDefinitionCatalog,
            ImmutableArray<MetadataTypeDefinitionAuthorityIdentity>.Empty,
            ImmutableArray<MetadataMethodDefinitionAuthorityIdentity>.Empty,
            null,
            observedCount,
            relatedMetadataToken);

    private readonly record struct PendingType(
        MetadataTypeDefinitionTableRowIdentity TableRow,
        int? EnclosingTypeDefinitionToken,
        int NestingDepth,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> GenericParameters);

    private readonly record struct PendingMethod(
        MetadataMethodDefinitionTableRowIdentity TableRow,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> GenericParameters);
}
