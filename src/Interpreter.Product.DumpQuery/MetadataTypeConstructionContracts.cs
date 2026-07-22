using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies the metadata semantics of one exact TypeDef.</summary>
/// <remarks>This draft classification is additive evidence and is not a compatibility commitment.</remarks>
public enum MetadataTypeDefinitionKind
{
    /// <summary>A reference class other than a delegate.</summary>
    Class = 1,
    /// <summary>A non-enum value type.</summary>
    ValueType = 2,
    /// <summary>An interface.</summary>
    Interface = 3,
    /// <summary>An enum definition.</summary>
    Enum = 4,
    /// <summary>A delegate definition.</summary>
    Delegate = 5,
}

/// <summary>Classifies whether one draft TypeDef semantic kind has complete physical proof.</summary>
/// <remarks>Provisional rows remain readable without upgrading caller assertions into exact classification.</remarks>
public enum MetadataTypeDefinitionClassificationStatus
{
    /// <summary>The kind is derived from exact flags or an exact ancestry proof.</summary>
    Exact = 1,
    /// <summary>The non-interface kind is a provisional prototype attribution.</summary>
    Provisional = 2,
}

/// <summary>Classifies the pinned compiler mapping from flattened GenericParam positions to nested source segments.</summary>
/// <remarks>A non-exact draft mapping preserves readable metadata but cannot drive closed argument slicing.</remarks>
public enum MetadataNestedGenericMappingStatus
{
    /// <summary>Every flattened position belongs to its unique cumulative introduced-arity range.</summary>
    ExactPinnedCompiler = 1,
    /// <summary>At least one retained source-segment attribution differs from the pinned compiler pattern.</summary>
    NonExact = 2,
}

/// <summary>Identifies each primitive type represented directly by one CLI signature element.</summary>
/// <remarks>The draft catalog intentionally excludes topology represented by another contract node.</remarks>
public enum MetadataPrimitiveTypeKind
{
    /// <summary>Boolean.</summary>
    Boolean = 1,
    /// <summary>Unsigned eight-bit integer.</summary>
    UInt8 = 2,
    /// <summary>Signed eight-bit integer.</summary>
    Int8 = 3,
    /// <summary>Signed sixteen-bit integer.</summary>
    Int16 = 4,
    /// <summary>Unsigned sixteen-bit integer.</summary>
    UInt16 = 5,
    /// <summary>Signed thirty-two-bit integer.</summary>
    Int32 = 6,
    /// <summary>Unsigned thirty-two-bit integer.</summary>
    UInt32 = 7,
    /// <summary>Signed sixty-four-bit integer.</summary>
    Int64 = 8,
    /// <summary>Unsigned sixty-four-bit integer.</summary>
    UInt64 = 9,
    /// <summary>Native signed integer.</summary>
    NativeInt = 10,
    /// <summary>Native unsigned integer.</summary>
    NativeUInt = 11,
    /// <summary>UTF-16 code unit.</summary>
    Char = 12,
    /// <summary>Single-precision floating point.</summary>
    Single = 13,
    /// <summary>Double-precision floating point.</summary>
    Double = 14,
    /// <summary>Managed string reference.</summary>
    String = 15,
    /// <summary>Managed object reference.</summary>
    Object = 16,
}

/// <summary>Classifies one complete pre-substitution metadata signature node.</summary>
/// <remarks>This draft syntax tree retains admitted and typed non-admitted physical forms without flattening them.</remarks>
public enum MetadataTypeSignatureNodeKind
{
    /// <summary>A primitive signature element.</summary>
    Primitive = 1,
    /// <summary>A direct TypeDef-or-TypeRef named leaf.</summary>
    Named = 2,
    /// <summary>An owner <c>VAR</c> index.</summary>
    OwnerTypeParameter = 3,
    /// <summary>A method <c>MVAR</c> index.</summary>
    MethodTypeParameter = 4,
    /// <summary>A class or value-type generic instantiation.</summary>
    GenericInstantiation = 5,
    /// <summary>A single-dimensional zero-based array.</summary>
    SzArray = 6,
    /// <summary>A multidimensional array, including metadata-only rank one.</summary>
    MultidimensionalArray = 7,
    /// <summary>A required custom modifier retained as typed non-admitted evidence.</summary>
    RequiredModifier = 8,
    /// <summary>An optional custom modifier retained as typed non-admitted evidence.</summary>
    OptionalModifier = 9,
    /// <summary>A pointer retained as typed non-admitted evidence.</summary>
    Pointer = 10,
    /// <summary>A managed by-reference form retained as typed non-admitted evidence.</summary>
    ByReference = 11,
    /// <summary>A function pointer retained as typed non-admitted evidence.</summary>
    FunctionPointer = 12,
    /// <summary>A non-recursive TypeSpec-token indirection retained as typed non-admitted evidence.</summary>
    TypeSpecificationIndirection = 13,
    /// <summary>A physical VOID element retained for role-aware classification and <c>void*</c>.</summary>
    Void = 14,
    /// <summary>A physical TYPEDBYREF element retained for role-aware classification.</summary>
    TypedByReference = 15,
}

/// <summary>Retains the raw CLASS or VALUETYPE code preceding one named signature head.</summary>
/// <remarks>The draft fact remains separate from resolved TypeDef classification and is checked only against exact proof.</remarks>
public enum MetadataNamedSignatureHeadKind
{
    /// <summary>The physical signature encoded CLASS.</summary>
    Class = 1,
    /// <summary>The physical signature encoded VALUETYPE.</summary>
    ValueType = 2,
}

/// <summary>Classifies the exact owner-table alternative of one GenericParam row.</summary>
/// <remarks>The draft union admits both TypeDef-owned VAR and MethodDef-owned MVAR rows.</remarks>
public enum MetadataGenericParameterOwnerKind
{
    /// <summary>The GenericParam Owner coded index names a TypeDef.</summary>
    TypeDefinition = 1,
    /// <summary>The GenericParam Owner coded index names a MethodDef.</summary>
    MethodDefinition = 2,
}

/// <summary>Classifies one bounded signature decode operation.</summary>
/// <remarks>A non-exact draft outcome retains the physical owner and reached bound but never a prefix tree.</remarks>
public enum MetadataSignatureDecodeResultKind
{
    /// <summary>The decoder consumed the complete source and produced one complete tree.</summary>
    Exact = 1,
    /// <summary>The decoder observed cap-plus-one and produced no prefix tree.</summary>
    NonExact = 2,
    /// <summary>The complete retained source is malformed or the decoder did not consume it exactly.</summary>
    Invalid = 3,
}

/// <summary>Classifies acquisition of one bounded metadata signature token-resolution catalog.</summary>
/// <remarks>The draft non-exact form retains source ends and cap-plus-one evidence but no usable map prefix.</remarks>
public enum MetadataSignatureTokenResolutionCatalogResultKind
{
    /// <summary>Every retained map entry is within the exact source table ends and below the operation cap.</summary>
    Exact = 1,
    /// <summary>The acquisition observed cap-plus-one and retained no usable map prefix.</summary>
    NonExact = 2,
}

/// <summary>Classifies complete traversal of a detached TypeSpec row-reference graph.</summary>
/// <remarks>The draft graph distinguishes exact source-end closure, bounded non-exact traversal, and invalid cycles/rows.</remarks>
public enum MetadataTypeSpecificationGraphResultKind
{
    /// <summary>Every reachable row decoded exactly and traversal reached all source ends within bounds.</summary>
    Exact = 1,
    /// <summary>A child decode, missing row, or cumulative counter prevented complete traversal.</summary>
    NonExact = 2,
    /// <summary>The graph contains a cycle, duplicate physical row key, mixed module, or malformed child.</summary>
    Invalid = 3,
}

/// <summary>Identifies one metadata role applying a decoded type signature.</summary>
/// <remarks>The draft use role supplies contextual validity without changing physical row or decode identity.</remarks>
public enum MetadataTypeUseRole
{
    /// <summary>A role-neutral TypeSpec row.</summary>
    TypeSpecification = 1,
    /// <summary>A FieldDef field type.</summary>
    FieldDefinition = 2,
    /// <summary>A TypeDef Extends use.</summary>
    BaseType = 3,
    /// <summary>An InterfaceImpl Interface use.</summary>
    InterfaceImplementation = 4,
    /// <summary>A GenericParamConstraint Constraint use.</summary>
    GenericParameterConstraint = 5,
    /// <summary>A method-owned signature type use.</summary>
    MethodSignature = 6,
}

/// <summary>Classifies role-specific application of one complete decoded type.</summary>
/// <remarks>The draft outcome remains distinct from role-neutral decode and closed semantic construction.</remarks>
public enum MetadataTypeUseResultKind
{
    /// <summary>The role admits one exact closed semantic type.</summary>
    Exact = 1,
    /// <summary>The role admits the physical form but explicit generic bindings remain open.</summary>
    Open = 2,
    /// <summary>The role is valid metadata but outside the current evaluator grammar.</summary>
    Unsupported = 3,
    /// <summary>The physical form is invalid in this metadata role.</summary>
    Invalid = 4,
    /// <summary>Required decode or graph evidence is incomplete.</summary>
    NonExact = 5,
}

/// <summary>Identifies the physical TypeDefOrRef target form stored in a metadata edge.</summary>
/// <remarks>The draft alternatives preserve row and module identity instead of equating equal decoded type names.</remarks>
public enum MetadataTypeDefOrRefTargetKind
{
    /// <summary>A direct TypeDef in the source module.</summary>
    TypeDefinition = 1,
    /// <summary>A complete TypeRef resolution rooted in the source module.</summary>
    TypeReference = 2,
    /// <summary>An exact TypeSpec row and its complete signature bytes.</summary>
    TypeSpecification = 3,
}

/// <summary>Classifies validation of one ordered physical metadata-edge row aggregate.</summary>
/// <remarks>The draft aggregate preserves every row in source order even when duplicate physical facts make it invalid.</remarks>
public enum MetadataEdgeAggregateResultKind
{
    /// <summary>The complete physical source table was observed and the selected owner projection is exact.</summary>
    Exact = 1,
    /// <summary>The source exceeded a declared cap or a complete table was unavailable; no usable prefix is retained.</summary>
    NonExact = 2,
    /// <summary>The complete table contradicts physical token order, module identity, or semantic uniqueness.</summary>
    Invalid = 3,
}

/// <summary>Classifies the first typed disposition of one physical metadata-edge table projection.</summary>
/// <remarks>The draft issue remains independent from exact row payloads and deterministic reached-bound identity.</remarks>
public enum MetadataEdgeAggregateIssue
{
    /// <summary>The complete table and selected-owner projection are exact.</summary>
    None = 0,
    /// <summary>The exact source end exceeded the operation cap; observed count is saturated at cap plus one.</summary>
    BoundReached = 1,
    /// <summary>The supplied rows did not exhaust the exact within-cap source end.</summary>
    SourceIncomplete = 2,
    /// <summary>At least one complete-table row belongs to another metadata module.</summary>
    ModuleMismatch = 3,
    /// <summary>Physical row tokens do not exhaust monotonically increasing RIDs from one through source end.</summary>
    PhysicalOrderInvalid = 4,
    /// <summary>One owner-target semantic pair occurs in more than one physical row.</summary>
    DuplicateSemanticEdge = 5,
    /// <summary>An owner, declared row, or target token lies beyond an exact metadata table source end.</summary>
    SourceTokenOutOfRange = 6,
    /// <summary>The supplied row array exceeds the exact physical table source end.</summary>
    SourceRowCountConflict = 7,
    /// <summary>A selected or constraint-owning GenericParam identity differs from its exact catalog row.</summary>
    GenericParameterCatalogMismatch = 8,
}

/// <summary>Classifies one completely closed metadata type topology.</summary>
/// <remarks>This draft topology preserves nested construction groups and the SZ-array distinction.</remarks>
public enum MetadataClosedTypeKind
{
    /// <summary>A primitive identity.</summary>
    Primitive = 1,
    /// <summary>A top-level or nested named construction.</summary>
    Named = 2,
    /// <summary>An admitted nullable construction.</summary>
    Nullable = 3,
    /// <summary>A single-dimensional zero-based array.</summary>
    SzArray = 4,
    /// <summary>A multidimensional array, including metadata-only rank one.</summary>
    MultidimensionalArray = 5,
}

/// <summary>Classifies projection of one signature into the closed-type model.</summary>
/// <remarks>The draft result distinguishes open and non-admitted input from an exact construction.</remarks>
public enum MetadataTypeConstructionResultKind
{
    /// <summary>The complete signature projects to one exact closed type.</summary>
    Exact = 1,
    /// <summary>The signature contains variables or an uninstantiated generic definition.</summary>
    Open = 2,
    /// <summary>The signature contains a retained topology outside the current closed grammar.</summary>
    Unsupported = 3,
    /// <summary>The signature topology contradicts the metadata role in which it was observed.</summary>
    Invalid = 4,
    /// <summary>The valid ground signature lacks exact classification or exceeded a construction bound.</summary>
    NonExact = 5,
}

/// <summary>Identifies the exact metadata source supplying an admitted generic substitution.</summary>
/// <remarks>
/// The current draft admits only a source-anchored FieldDef signature. Other metadata uses require their own exact
/// physical-row and complete-catalog identities before they can extend this closed set.
/// </remarks>
public enum MetadataTypeSubstitutionContextKind
{
    /// <summary>An exact FieldDef signature with one proven declaring-TypeDef binding ledger.</summary>
    FieldDefinition = 1,
}

/// <summary>Classifies the complete result of an explicit recursive substitution.</summary>
/// <remarks>This draft outcome never supplies missing generic arguments from another observation source.</remarks>
public enum MetadataTypeSubstitutionResultKind
{
    /// <summary>Every variable was replaced and the result is one admitted closed type.</summary>
    Exact = 1,
    /// <summary>At least one variable or generic definition remains open.</summary>
    Open = 2,
    /// <summary>The signature contains a retained topology outside the current closed grammar.</summary>
    Unsupported = 3,
    /// <summary>The signature contradicts the selected substitution context or closed topology.</summary>
    Invalid = 4,
    /// <summary>The valid substituted topology lacks exact classification or exceeded a construction bound.</summary>
    NonExact = 5,
}

/// <summary>Classifies one exact GenericParam-row binding observation.</summary>
/// <remarks>The draft ledger distinguishes a proven closed argument from a declared row whose binding is unavailable.</remarks>
public enum MetadataTypeArgumentBindingKind
{
    /// <summary>The exact GenericParam row maps to one exact closed argument.</summary>
    Exact = 1,
    /// <summary>The exact declared GenericParam row has no observed closed binding.</summary>
    Unavailable = 2,
}

/// <summary>Freezes one additive raw W8 TypeDef row and its exact raw enclosing-TypeDef chain.</summary>
/// <remarks>
/// This sealed draft identity deliberately does not impose the pinned compiler's backtick/arity relation. Readable
/// rows outside that pattern remain exact physical metadata and receive a separate mapping disposition later.
/// </remarks>
public sealed class MetadataRawTypeDefinitionIdentity : IEquatable<MetadataRawTypeDefinitionIdentity>
{
    private const string CanonicalDomain = "metadata-v2-raw-type-definition-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataRawTypeDefinitionIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeDefinitionToken,
        int fieldListRowId,
        int fieldListEndExclusiveRowId,
        int methodListRowId,
        int methodListEndExclusiveRowId,
        string namespaceName,
        string typeName,
        int typeAttributes,
        int genericParameterCount,
        int? extendsMetadataToken,
        MetadataRawTypeDefinitionIdentity? enclosingType)
    {
        MetadataModule = metadataModule;
        TypeDefinitionToken = typeDefinitionToken;
        FieldListRowId = fieldListRowId;
        FieldListEndExclusiveRowId = fieldListEndExclusiveRowId;
        MethodListRowId = methodListRowId;
        MethodListEndExclusiveRowId = methodListEndExclusiveRowId;
        NamespaceName = namespaceName;
        TypeName = typeName;
        TypeAttributes = typeAttributes;
        GenericParameterCount = genericParameterCount;
        ExtendsMetadataToken = extendsMetadataToken;
        EnclosingType = enclosingType;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(typeDefinitionToken);
        writer.WriteInt32(fieldListRowId);
        writer.WriteInt32(fieldListEndExclusiveRowId);
        writer.WriteInt32(methodListRowId);
        writer.WriteInt32(methodListEndExclusiveRowId);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteInt32(typeAttributes);
        writer.WriteInt32(genericParameterCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, extendsMetadataToken);
        writer.WriteBoolean(enclosingType is not null);
        if (enclosingType is not null)
        {
            writer.WriteLengthPrefixedBytes(enclosingType.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the raw TypeDef row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }
    /// <summary>Gets the exact non-nil TypeDef token.</summary>
    public int TypeDefinitionToken { get; }
    /// <summary>Gets the raw TypeDef.FieldList starting row ID.</summary>
    public int FieldListRowId { get; }
    /// <summary>Gets the next TypeDef.FieldList or table-end row ID, exclusive.</summary>
    public int FieldListEndExclusiveRowId { get; }
    /// <summary>Gets the raw TypeDef.MethodList starting row ID.</summary>
    public int MethodListRowId { get; }
    /// <summary>Gets the next TypeDef.MethodList or table-end row ID, exclusive.</summary>
    public int MethodListEndExclusiveRowId { get; }
    /// <summary>Gets the exact decoded namespace, including empty.</summary>
    public string NamespaceName { get; }
    /// <summary>Gets the exact decoded metadata name without imposing compiler arity spelling.</summary>
    public string TypeName { get; }
    /// <summary>Gets the exact raw TypeAttributes bits.</summary>
    public int TypeAttributes { get; }
    /// <summary>Gets the exact GenericParam-row count owned by this raw TypeDef.</summary>
    public int GenericParameterCount { get; }
    /// <summary>Gets the decoded TypeDef, TypeRef, or TypeSpec Extends token, or null for nil.</summary>
    public int? ExtendsMetadataToken { get; }
    /// <summary>Gets the exact raw enclosing TypeDef chain, or null for a top-level row.</summary>
    public MetadataRawTypeDefinitionIdentity? EnclosingType { get; }
    /// <summary>Gets the exact count of retained enclosing raw TypeDefs.</summary>
    public int EnclosingTypeDepth => EnclosingType is null ? 0 : checked(EnclosingType.EnclosingTypeDepth + 1);
    /// <summary>Gets whether raw class-semantics bits encode interface.</summary>
    public bool IsInterface =>
        ((System.Reflection.TypeAttributes)TypeAttributes & System.Reflection.TypeAttributes.ClassSemanticsMask) ==
        System.Reflection.TypeAttributes.Interface;
    /// <summary>Gets the ordinal metadata full name.</summary>
    public string FullName => NamespaceName.Length == 0 ? TypeName : $"{NamespaceName}.{TypeName}";
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact additive raw-TypeDef draft identity.</summary>
    /// <param name="metadataModule">The exact source metadata module.</param>
    /// <param name="typeDefinitionToken">The exact non-nil TypeDef token.</param>
    /// <param name="fieldListRowId">The raw FieldList starting row ID.</param>
    /// <param name="fieldListEndExclusiveRowId">The next FieldList or table-end row ID.</param>
    /// <param name="methodListRowId">The raw MethodList starting row ID.</param>
    /// <param name="methodListEndExclusiveRowId">The next MethodList or table-end row ID.</param>
    /// <param name="namespaceName">The exact decoded namespace, including empty.</param>
    /// <param name="typeName">The exact decoded metadata name.</param>
    /// <param name="typeAttributes">The exact raw TypeAttributes bits.</param>
    /// <param name="genericParameterCount">The exact owned GenericParam-row count.</param>
    /// <param name="extendsMetadataToken">The decoded Extends token, or null for nil.</param>
    /// <param name="enclosingType">The exact raw enclosing TypeDef identity, or null.</param>
    /// <returns>A sealed immutable raw draft identity without compiler-arity assumptions.</returns>
    public static MetadataRawTypeDefinitionIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeDefinitionToken,
        int fieldListRowId,
        int fieldListEndExclusiveRowId,
        int methodListRowId,
        int methodListEndExclusiveRowId,
        string namespaceName,
        string typeName,
        int typeAttributes,
        int genericParameterCount,
        int? extendsMetadataToken,
        MetadataRawTypeDefinitionIdentity? enclosingType = null)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(typeDefinitionToken, 0x02, nameof(typeDefinitionToken));
        ValidateOwnerRange(fieldListRowId, fieldListEndExclusiveRowId, nameof(fieldListRowId));
        ValidateOwnerRange(methodListRowId, methodListEndExclusiveRowId, nameof(methodListRowId));
        ExpressionV2ContractEncoding.RequireText(
            namespaceName,
            nameof(namespaceName),
            StaticFieldMetadataTextLimits.MaximumTextLength,
            allowEmpty: true);
        ExpressionV2ContractEncoding.RequireText(
            typeName,
            nameof(typeName),
            StaticFieldMetadataTextLimits.MaximumTextLength);
        if (genericParameterCount < 0 || genericParameterCount > 0x00FF_FFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(genericParameterCount));
        }
        if (extendsMetadataToken.HasValue &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x01) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x02) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x1B))
        {
            throw new ArgumentOutOfRangeException(nameof(extendsMetadataToken));
        }
        if (enclosingType is not null)
        {
            if (!enclosingType.MetadataModule.Equals(metadataModule) ||
                enclosingType.EnclosingTypeDepth >= StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth)
            {
                throw new ArgumentException("The raw enclosing chain must be distinct, same-module, and within its depth bound.", nameof(enclosingType));
            }
            for (var current = enclosingType; current is not null; current = current.EnclosingType)
            {
                if (current.TypeDefinitionToken == typeDefinitionToken)
                {
                    throw new ArgumentException("The raw enclosing chain cannot repeat the subject TypeDef row.", nameof(enclosingType));
                }
            }
        }
        return new MetadataRawTypeDefinitionIdentity(
            metadataModule,
            typeDefinitionToken,
            fieldListRowId,
            fieldListEndExclusiveRowId,
            methodListRowId,
            methodListEndExclusiveRowId,
            namespaceName,
            typeName,
            typeAttributes,
            genericParameterCount,
            extendsMetadataToken,
            enclosingType);
    }

    /// <summary>Projects one narrowed W7 TypeDef fact into the additive raw W8 draft identity.</summary>
    /// <param name="definition">The exact W7 TypeDef and enclosing chain.</param>
    /// <returns>A sealed immutable raw draft identity with identical physical row fields.</returns>
    public static MetadataRawTypeDefinitionIdentity FromPinnedW7(StaticFieldTypeDefinitionIdentity definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Create(
            definition.MetadataModule,
            definition.TypeDefinitionToken,
            definition.FieldListRowId,
            definition.FieldListEndExclusiveRowId,
            definition.MethodListRowId,
            definition.MethodListEndExclusiveRowId,
            definition.NamespaceName,
            definition.TypeName,
            definition.TypeAttributes,
            definition.GenericParameterCount,
            definition.ExtendsMetadataToken,
            definition.EnclosingType is null ? null : FromPinnedW7(definition.EnclosingType));
    }

    /// <summary>Tests whether this raw draft identity has every physical field of one W7 TypeDef fact.</summary>
    /// <param name="definition">The narrowed W7 fact to compare.</param>
    /// <returns><see langword="true"/> only when the complete raw row and enclosing chain match.</returns>
    public bool MatchesPinnedW7(StaticFieldTypeDefinitionIdentity definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Equals(FromPinnedW7(definition));
    }

    /// <summary>Tests canonical equality between two raw draft TypeDef identities.</summary>
    /// <param name="other">The other raw draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataRawTypeDefinitionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataRawTypeDefinitionIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateOwnerRange(int start, int endExclusive, string parameterName)
    {
        if (start <= 0 || endExclusive < start || endExclusive > 0x0100_0000)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>Freezes the exact TypeDef-or-MethodDef owner alternative of one GenericParam row.</summary>
/// <remarks>The sealed draft union retains the complete physical owner row and source metadata module.</remarks>
public sealed class MetadataGenericParameterOwnerIdentity : IEquatable<MetadataGenericParameterOwnerIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-owner-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterOwnerIdentity(
        MetadataGenericParameterOwnerKind kind,
        MetadataRawTypeDefinitionIdentity? typeDefinition,
        StaticFieldMethodDefinitionIdentity? methodDefinition)
    {
        Kind = kind;
        TypeDefinition = typeDefinition;
        MethodDefinition = methodDefinition;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        WriteOptional(writer, typeDefinition?.CanonicalBytes);
        WriteOptional(writer, methodDefinition?.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact closed owner alternative.</summary>
    public MetadataGenericParameterOwnerKind Kind { get; }
    /// <summary>Gets the exact TypeDef owner only for a TypeDef-owned row.</summary>
    public MetadataRawTypeDefinitionIdentity? TypeDefinition { get; }
    /// <summary>Gets the exact MethodDef owner only for a MethodDef-owned row.</summary>
    public StaticFieldMethodDefinitionIdentity? MethodDefinition { get; }
    /// <summary>Gets the exact source metadata module containing the owner and GenericParam rows.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule => Kind switch
    {
        MetadataGenericParameterOwnerKind.TypeDefinition => TypeDefinition!.MetadataModule,
        MetadataGenericParameterOwnerKind.MethodDefinition => MethodDefinition!.DeclaringType.MetadataModule,
        _ => throw new InvalidOperationException("The owner kind is outside the closed draft union."),
    };
    /// <summary>Gets the exact TypeDef or MethodDef owner metadata token.</summary>
    public int OwnerMetadataToken => Kind switch
    {
        MetadataGenericParameterOwnerKind.TypeDefinition => TypeDefinition!.TypeDefinitionToken,
        MetadataGenericParameterOwnerKind.MethodDefinition => MethodDefinition!.MethodDefinitionToken,
        _ => throw new InvalidOperationException("The owner kind is outside the closed draft union."),
    };
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact TypeDef-owner draft identity.</summary>
    /// <param name="typeDefinition">The complete physical TypeDef owner row.</param>
    /// <returns>A sealed immutable draft owner identity.</returns>
    public static MetadataGenericParameterOwnerIdentity ForTypeDefinition(
        MetadataRawTypeDefinitionIdentity typeDefinition)
    {
        ArgumentNullException.ThrowIfNull(typeDefinition);
        return new MetadataGenericParameterOwnerIdentity(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            typeDefinition,
            null);
    }

    /// <summary>Creates one exact MethodDef-owner draft identity.</summary>
    /// <param name="methodDefinition">The complete physical MethodDef row and declaring TypeDef relation.</param>
    /// <returns>A sealed immutable draft owner identity.</returns>
    public static MetadataGenericParameterOwnerIdentity ForMethodDefinition(
        StaticFieldMethodDefinitionIdentity methodDefinition)
    {
        ArgumentNullException.ThrowIfNull(methodDefinition);
        return new MetadataGenericParameterOwnerIdentity(
            MetadataGenericParameterOwnerKind.MethodDefinition,
            null,
            methodDefinition);
    }

    /// <summary>Tests canonical equality between two draft GenericParam owner identities.</summary>
    /// <param name="other">The other draft owner.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataGenericParameterOwnerIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterOwnerIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void WriteOptional(CanonicalReplayEncoding.Writer writer, ImmutableArray<byte>? bytes)
    {
        writer.WriteBoolean(bytes.HasValue);
        if (bytes.HasValue)
        {
            writer.WriteLengthPrefixedBytes(bytes.Value.AsSpan());
        }
    }
}

/// <summary>Freezes one exact GenericParam row without binding an argument.</summary>
/// <remarks>This sealed draft identity preserves row token, exact owner alternative, raw number, name, and flags.</remarks>
public sealed class MetadataGenericParameterIdentity : IEquatable<MetadataGenericParameterIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterIdentity(
        int genericParameterToken,
        MetadataGenericParameterOwnerIdentity owner,
        int position,
        string name,
        int attributes)
    {
        GenericParameterToken = genericParameterToken;
        Owner = owner;
        Position = position;
        Name = name;
        Attributes = attributes;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(genericParameterToken);
        writer.WriteLengthPrefixedBytes(owner.CanonicalBytes.AsSpan());
        writer.WriteInt32(position);
        writer.WriteString(name);
        writer.WriteInt32(attributes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact non-nil GenericParam token.</summary>
    public int GenericParameterToken { get; }
    /// <summary>Gets the exact TypeDef-or-MethodDef owner row identity.</summary>
    public MetadataGenericParameterOwnerIdentity Owner { get; }
    /// <summary>Gets the exact decoded TypeDef or MethodDef owner token.</summary>
    public int OwnerMetadataToken => Owner.OwnerMetadataToken;
    /// <summary>Gets the zero-based flattened GenericParam number.</summary>
    public int Position { get; }
    /// <summary>Gets the exact decoded metadata name.</summary>
    public string Name { get; }
    /// <summary>Gets the exact GenericParameterAttributes bits.</summary>
    public int Attributes { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated GenericParam-row draft identity.</summary>
    /// <param name="genericParameterToken">The exact non-nil GenericParam token.</param>
    /// <param name="owner">The exact TypeDef-or-MethodDef owner row identity.</param>
    /// <param name="position">The zero-based flattened position.</param>
    /// <param name="name">The exact decoded non-empty metadata name.</param>
    /// <param name="attributes">The exact admitted GenericParameterAttributes bits.</param>
    /// <returns>A sealed immutable draft row identity.</returns>
    public static MetadataGenericParameterIdentity Create(
        int genericParameterToken,
        MetadataGenericParameterOwnerIdentity owner,
        int position,
        string name,
        int attributes)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(genericParameterToken, 0x2A, nameof(genericParameterToken));
        ArgumentNullException.ThrowIfNull(owner);
        if (position < 0 || position > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
        ExpressionV2ContractEncoding.RequireText(name, nameof(name), StaticFieldMetadataTextLimits.MaximumTextLength);
        if (attributes < 0 || (attributes & ~0x3F) != 0)
        {
            throw new ArgumentException("The GenericParam attributes contain bits outside the admitted CLI set.", nameof(attributes));
        }
        return new MetadataGenericParameterIdentity(
            genericParameterToken,
            owner,
            position,
            name,
            attributes);
    }

    /// <summary>Tests canonical equality between two draft GenericParam identities.</summary>
    /// <param name="other">The other draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataGenericParameterIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Proves that one exact reference-class ancestry has the canonical delegate base chain.</summary>
/// <remarks>
/// This sealed draft proof supplements the broader W7 reference-class ancestry without changing that established
/// contract. It retains the exact runtime-selected core-library <c>System.MulticastDelegate</c> and
/// <c>System.Delegate</c> TypeDefs and proves the subject derives directly through both roles to System.Object.
/// </remarks>
public sealed class MetadataDelegateTypeAncestryIdentity : IEquatable<MetadataDelegateTypeAncestryIdentity>
{
    private const string CanonicalDomain = "metadata-v2-delegate-type-ancestry-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataDelegateTypeAncestryIdentity(
        StaticFieldTypeAncestryIdentity ancestry,
        StaticFieldTypeDefinitionIdentity systemMulticastDelegateType,
        StaticFieldTypeDefinitionIdentity systemDelegateType)
    {
        Ancestry = ancestry;
        SystemMulticastDelegateType = systemMulticastDelegateType;
        SystemDelegateType = systemDelegateType;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(ancestry.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(systemMulticastDelegateType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(systemDelegateType.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete exact subject-to-System.Object reference-class ancestry.</summary>
    public StaticFieldTypeAncestryIdentity Ancestry { get; }

    /// <summary>Gets the exact runtime-selected core-library System.MulticastDelegate TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity SystemMulticastDelegateType { get; }

    /// <summary>Gets the exact runtime-selected core-library System.Delegate TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity SystemDelegateType { get; }

    /// <summary>Gets the exact delegate TypeDef classified by this proof.</summary>
    public StaticFieldTypeDefinitionIdentity SubjectType => Ancestry.SubjectType;

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact immutable delegate-ancestry proof.</summary>
    /// <param name="ancestry">The complete exact subject-to-System.Object reference-class ancestry.</param>
    /// <param name="systemMulticastDelegateType">
    /// The exact runtime-selected core-library System.MulticastDelegate TypeDef.
    /// </param>
    /// <param name="systemDelegateType">The exact runtime-selected core-library System.Delegate TypeDef.</param>
    /// <returns>A sealed draft proof of the canonical delegate base chain.</returns>
    public static MetadataDelegateTypeAncestryIdentity Create(
        StaticFieldTypeAncestryIdentity ancestry,
        StaticFieldTypeDefinitionIdentity systemMulticastDelegateType,
        StaticFieldTypeDefinitionIdentity systemDelegateType)
    {
        ArgumentNullException.ThrowIfNull(ancestry);
        ArgumentNullException.ThrowIfNull(systemMulticastDelegateType);
        ArgumentNullException.ThrowIfNull(systemDelegateType);
        if (ancestry.Classification != StaticFieldTypeClassification.ReferenceClass ||
            ancestry.CoreLibrary is not { } coreLibrary)
        {
            throw new ArgumentException(
                "A delegate proof requires one exact reference-class ancestry with a core-library terminal.",
                nameof(ancestry));
        }
        ValidateCoreLibraryRole(systemMulticastDelegateType, "MulticastDelegate", coreLibrary);
        ValidateCoreLibraryRole(systemDelegateType, "Delegate", coreLibrary);
        var edges = ancestry.Edges;
        if (edges.Length != 3 ||
            !edges[0].SourceType.Equals(ancestry.SubjectType) ||
            !edges[0].ResolvedBaseType.Equals(systemMulticastDelegateType) ||
            !edges[1].SourceType.Equals(systemMulticastDelegateType) ||
            !edges[1].ResolvedBaseType.Equals(systemDelegateType) ||
            !edges[2].SourceType.Equals(systemDelegateType) ||
            !edges[2].ResolvedBaseType.Equals(coreLibrary.SystemObjectType))
        {
            throw new ArgumentException(
                "A delegate proof must retain the exact subject, MulticastDelegate, Delegate, and Object chain.",
                nameof(ancestry));
        }
        return new MetadataDelegateTypeAncestryIdentity(
            ancestry,
            systemMulticastDelegateType,
            systemDelegateType);
    }

    /// <summary>Tests canonical equality between two immutable delegate-ancestry proofs.</summary>
    /// <param name="other">The other draft proof.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataDelegateTypeAncestryIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataDelegateTypeAncestryIdentity);

    /// <summary>Computes a hash code from immutable canonical draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateCoreLibraryRole(
        StaticFieldTypeDefinitionIdentity type,
        string expectedName,
        StaticFieldCoreLibraryIdentity coreLibrary)
    {
        var attributes = (System.Reflection.TypeAttributes)type.TypeAttributes;
        if (!type.MetadataModule.Equals(coreLibrary.MetadataModule) ||
            !type.IsTopLevel ||
            type.GenericArity != 0 ||
            type.IsInterface ||
            !string.Equals(type.NamespaceName, "System", StringComparison.Ordinal) ||
            !string.Equals(type.TypeName, expectedName, StringComparison.Ordinal) ||
            (attributes & System.Reflection.TypeAttributes.VisibilityMask) !=
                System.Reflection.TypeAttributes.Public ||
            (attributes & System.Reflection.TypeAttributes.Abstract) == 0 ||
            (attributes & System.Reflection.TypeAttributes.Sealed) != 0)
        {
            throw new ArgumentException(
                $"The {expectedName} role must be its exact public abstract core-library TypeDef.",
                nameof(type));
        }
    }
}

/// <summary>Augments one exact W7 TypeDef identity with W8 definition kind and flattened GenericParam rows.</summary>
/// <remarks>The sealed draft wrapper validates every parameter against the complete physical enclosing-type chain.</remarks>
public sealed class MetadataTypeDefinitionIdentity : IEquatable<MetadataTypeDefinitionIdentity>
{
    private const string CanonicalDomain = "metadata-v2-type-definition-identity";
    private const int CanonicalSchemaVersion = 3;
    private readonly ImmutableArray<MetadataGenericParameterIdentity> genericParameters;
    private readonly ImmutableArray<int> genericParameterSegmentIndices;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefinitionIdentity(
        MetadataRawTypeDefinitionIdentity rawDefinition,
        StaticFieldTypeDefinitionIdentity? pinnedDefinition,
        MetadataTypeDefinitionKind kind,
        MetadataTypeDefinitionClassificationStatus classificationStatus,
        StaticFieldTypeAncestryIdentity? ancestry,
        MetadataDelegateTypeAncestryIdentity? delegateAncestry,
        MetadataNestedGenericMappingStatus genericMappingStatus,
        ImmutableArray<MetadataGenericParameterIdentity> genericParameters,
        ImmutableArray<int> genericParameterSegmentIndices)
    {
        RawDefinition = rawDefinition;
        PinnedDefinition = pinnedDefinition;
        Kind = kind;
        ClassificationStatus = classificationStatus;
        Ancestry = ancestry;
        DelegateAncestry = delegateAncestry;
        GenericMappingStatus = genericMappingStatus;
        this.genericParameters = genericParameters;
        this.genericParameterSegmentIndices = genericParameterSegmentIndices;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(rawDefinition.CanonicalBytes.AsSpan());
        writer.WriteBoolean(pinnedDefinition is not null);
        if (pinnedDefinition is not null)
        {
            writer.WriteLengthPrefixedBytes(pinnedDefinition.CanonicalBytes.AsSpan());
        }
        writer.WriteInt32((int)kind);
        writer.WriteInt32((int)classificationStatus);
        writer.WriteBoolean(ancestry is not null);
        if (ancestry is not null)
        {
            writer.WriteLengthPrefixedBytes(ancestry.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(delegateAncestry is not null);
        if (delegateAncestry is not null)
        {
            writer.WriteLengthPrefixedBytes(delegateAncestry.CanonicalBytes.AsSpan());
        }
        writer.WriteInt32((int)genericMappingStatus);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, genericParameters, static value => value.CanonicalBytes);
        WriteInt32Array(writer, genericParameterSegmentIndices);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact additive raw W8 TypeDef row and enclosing chain.</summary>
    public MetadataRawTypeDefinitionIdentity RawDefinition { get; }
    /// <summary>Gets the exact additive physical row; retained as a draft naming bridge for W8 callers.</summary>
    public MetadataRawTypeDefinitionIdentity PhysicalDefinition => RawDefinition;
    /// <summary>Gets the narrowed W7 mapping fact only when the pinned compiler relation was proven.</summary>
    public StaticFieldTypeDefinitionIdentity? PinnedDefinition { get; }
    /// <summary>Gets the attributed semantic kind together with <see cref="ClassificationStatus"/>.</summary>
    public MetadataTypeDefinitionKind Kind { get; }
    /// <summary>Gets whether the semantic kind has exact physical proof.</summary>
    public MetadataTypeDefinitionClassificationStatus ClassificationStatus { get; }
    /// <summary>Gets the exact W7 ancestry proof when one was supplied for semantic classification.</summary>
    public StaticFieldTypeAncestryIdentity? Ancestry { get; }
    /// <summary>Gets the exact canonical delegate-base proof only for an exactly classified delegate.</summary>
    public MetadataDelegateTypeAncestryIdentity? DelegateAncestry { get; }
    /// <summary>Gets whether a pinned compiler mapping can partition flattened arguments.</summary>
    public MetadataNestedGenericMappingStatus GenericMappingStatus { get; }
    /// <summary>Gets every exact raw GenericParam row in flattened-number order.</summary>
    public ImmutableArray<MetadataGenericParameterIdentity> GenericParameters =>
        ExpressionV2ContractEncoding.Copy(genericParameters);
    /// <summary>Gets derived per-position segment indices only for an exact pinned compiler mapping.</summary>
    public ImmutableArray<int> GenericParameterSegmentIndices =>
        ExpressionV2ContractEncoding.Copy(genericParameterSegmentIndices);
    /// <summary>Gets the exact TypeDef metadata token.</summary>
    public int TypeDefinitionToken => RawDefinition.TypeDefinitionToken;
    /// <summary>Gets the exact total owned GenericParam-row count.</summary>
    public int FlattenedArity => genericParameters.Length;
    /// <summary>Gets the final segment's introduced arity only for an exact pinned compiler mapping.</summary>
    public int? IntroducedArity => PinnedDefinition?.IntroducedGenericArity;
    /// <summary>Gets the zero-based final raw named-segment index.</summary>
    public int SegmentIndex => RawDefinition.EnclosingTypeDepth;
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a readable raw-TypeDef draft identity with a typed non-exact compiler mapping.</summary>
    /// <param name="rawDefinition">The exact additive raw TypeDef row and enclosing chain.</param>
    /// <param name="kind">The attributed kind checked against exact flags and any supplied ancestry.</param>
    /// <param name="genericParameters">Every exact raw GenericParam row in flattened-number order.</param>
    /// <param name="ancestry">An optional exact W7 ancestry proof matching the raw row.</param>
    /// <param name="delegateAncestry">An optional canonical delegate-base proof matching <paramref name="ancestry"/>.</param>
    /// <returns>A sealed immutable draft definition whose argument slicing remains non-admitted.</returns>
    public static MetadataTypeDefinitionIdentity Create(
        MetadataRawTypeDefinitionIdentity rawDefinition,
        MetadataTypeDefinitionKind kind,
        ImmutableArray<MetadataGenericParameterIdentity> genericParameters,
        StaticFieldTypeAncestryIdentity? ancestry = null,
        MetadataDelegateTypeAncestryIdentity? delegateAncestry = null) =>
        CreateCore(rawDefinition, pinnedDefinition: null, kind, genericParameters, ancestry, delegateAncestry);

    /// <summary>Creates a draft TypeDef identity with an exact pinned compiler arity mapping.</summary>
    /// <param name="pinnedDefinition">The narrowed exact W7 TypeDef fact.</param>
    /// <param name="kind">The interface kind or a non-interface attribution checked against optional ancestry.</param>
    /// <param name="genericParameters">Every exact GenericParam row owned by the projected raw TypeDef.</param>
    /// <param name="ancestry">An optional exact W7 ancestry proof for semantic classification.</param>
    /// <param name="delegateAncestry">An optional canonical delegate-base proof matching <paramref name="ancestry"/>.</param>
    /// <returns>A sealed immutable draft identity able to partition nested flattened arguments.</returns>
    public static MetadataTypeDefinitionIdentity FromPinnedW7(
        StaticFieldTypeDefinitionIdentity pinnedDefinition,
        MetadataTypeDefinitionKind kind,
        ImmutableArray<MetadataGenericParameterIdentity> genericParameters,
        StaticFieldTypeAncestryIdentity? ancestry = null,
        MetadataDelegateTypeAncestryIdentity? delegateAncestry = null)
    {
        ArgumentNullException.ThrowIfNull(pinnedDefinition);
        return CreateCore(
            MetadataRawTypeDefinitionIdentity.FromPinnedW7(pinnedDefinition),
            pinnedDefinition,
            kind,
            genericParameters,
            ancestry,
            delegateAncestry);
    }

    /// <summary>Tests canonical equality between two draft TypeDef identities.</summary>
    /// <param name="other">The other draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeDefinitionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeDefinitionIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static MetadataTypeDefinitionIdentity CreateCore(
        MetadataRawTypeDefinitionIdentity rawDefinition,
        StaticFieldTypeDefinitionIdentity? pinnedDefinition,
        MetadataTypeDefinitionKind kind,
        ImmutableArray<MetadataGenericParameterIdentity> genericParameters,
        StaticFieldTypeAncestryIdentity? ancestry,
        MetadataDelegateTypeAncestryIdentity? delegateAncestry)
    {
        ArgumentNullException.ThrowIfNull(rawDefinition);
        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            genericParameters,
            nameof(genericParameters),
            StaticFieldV2Limits.MaximumGenericParameterCount);
        if (copied.Length != rawDefinition.GenericParameterCount)
        {
            throw new ArgumentException("GenericParam rows must exhaust the exact raw TypeDef-owned count.", nameof(genericParameters));
        }
        for (var position = 0; position < copied.Length; position++)
        {
            if (copied[position].Position != position ||
                copied[position].Owner.Kind != MetadataGenericParameterOwnerKind.TypeDefinition ||
                !copied[position].Owner.TypeDefinition!.Equals(rawDefinition))
            {
                throw new ArgumentException("GenericParam owner and number must match the exact raw TypeDef row.", nameof(genericParameters));
            }
        }
        if (pinnedDefinition is not null && !rawDefinition.MatchesPinnedW7(pinnedDefinition))
        {
            throw new ArgumentException("The pinned mapping fact must match every raw TypeDef field.", nameof(pinnedDefinition));
        }
        var classificationStatus = ValidateClassification(rawDefinition, kind, ancestry, delegateAncestry);
        var segmentIndices = pinnedDefinition is null
            ? ImmutableArray<int>.Empty
            : DerivePinnedSegmentIndices(pinnedDefinition, copied.Length);
        return new MetadataTypeDefinitionIdentity(
            rawDefinition,
            pinnedDefinition,
            kind,
            classificationStatus,
            ancestry,
            delegateAncestry,
            pinnedDefinition is null
                ? MetadataNestedGenericMappingStatus.NonExact
                : MetadataNestedGenericMappingStatus.ExactPinnedCompiler,
            copied,
            segmentIndices);
    }

    private static MetadataTypeDefinitionClassificationStatus ValidateClassification(
        MetadataRawTypeDefinitionIdentity rawDefinition,
        MetadataTypeDefinitionKind kind,
        StaticFieldTypeAncestryIdentity? ancestry,
        MetadataDelegateTypeAncestryIdentity? delegateAncestry)
    {
        if (rawDefinition.IsInterface)
        {
            if (rawDefinition.ExtendsMetadataToken is not null ||
                kind != MetadataTypeDefinitionKind.Interface ||
                delegateAncestry is not null ||
                ancestry is not null &&
                (!rawDefinition.MatchesPinnedW7(ancestry.SubjectType) ||
                 ancestry.Classification != StaticFieldTypeClassification.Interface))
            {
                throw new ArgumentException(
                    "Interface flags require a nil Extends column and any ancestry must prove that exact interface.",
                    nameof(kind));
            }
            return MetadataTypeDefinitionClassificationStatus.Exact;
        }
        if (kind == MetadataTypeDefinitionKind.Interface)
        {
            throw new ArgumentException("Non-interface flags cannot carry the interface kind.", nameof(kind));
        }
        if (ancestry is null)
        {
            if (delegateAncestry is not null)
            {
                throw new ArgumentException(
                    "A delegate-base proof requires the same complete reference-class ancestry.",
                    nameof(delegateAncestry));
            }
            return MetadataTypeDefinitionClassificationStatus.Provisional;
        }
        if (!rawDefinition.MatchesPinnedW7(ancestry.SubjectType))
        {
            throw new ArgumentException("The ancestry proof must classify the exact raw TypeDef row.", nameof(ancestry));
        }
        if (kind == MetadataTypeDefinitionKind.Delegate)
        {
            if (ancestry.Classification != StaticFieldTypeClassification.ReferenceClass)
            {
                throw new ArgumentException(
                    "A delegate attribution contradicts non-reference ancestry.",
                    nameof(kind));
            }
            if (delegateAncestry is null)
            {
                return MetadataTypeDefinitionClassificationStatus.Provisional;
            }
            if (!delegateAncestry.Ancestry.Equals(ancestry) ||
                !rawDefinition.MatchesPinnedW7(delegateAncestry.SubjectType))
            {
                throw new ArgumentException(
                    "The delegate-base proof must classify the same exact raw TypeDef and ancestry.",
                    nameof(delegateAncestry));
            }
            return MetadataTypeDefinitionClassificationStatus.Exact;
        }
        if (delegateAncestry is not null)
        {
            throw new ArgumentException(
                "A delegate-base proof is valid only with the delegate kind.",
                nameof(delegateAncestry));
        }
        var expectedKind = ancestry.Classification switch
        {
            StaticFieldTypeClassification.ReferenceClass => MetadataTypeDefinitionKind.Class,
            StaticFieldTypeClassification.ValueType => MetadataTypeDefinitionKind.ValueType,
            StaticFieldTypeClassification.Enum => MetadataTypeDefinitionKind.Enum,
            _ => throw new ArgumentException("The ancestry classification contradicts non-interface flags.", nameof(ancestry)),
        };
        if (kind != expectedKind)
        {
            throw new ArgumentException("The attributed kind must equal exact ancestry classification.", nameof(kind));
        }
        return MetadataTypeDefinitionClassificationStatus.Exact;
    }

    private static ImmutableArray<int> DerivePinnedSegmentIndices(
        StaticFieldTypeDefinitionIdentity finalDefinition,
        int parameterCount)
    {
        var stack = new Stack<StaticFieldTypeDefinitionIdentity>();
        for (var current = finalDefinition; current is not null; current = current.EnclosingType)
        {
            stack.Push(current);
        }
        var chain = stack.ToArray();
        var builder = ImmutableArray.CreateBuilder<int>(parameterCount);
        for (var position = 0; position < parameterCount; position++)
        {
            var segment = 0;
            while (position >= chain[segment].GenericParameterCount)
            {
                segment++;
            }
            builder.Add(segment);
        }
        return builder.ToImmutable();
    }

    private static void WriteInt32Array(CanonicalReplayEncoding.Writer writer, ImmutableArray<int> values)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteInt32(value);
        }
    }
}

/// <summary>Freezes a lightweight source-module/TypeSpec-token row reference.</summary>
/// <remarks>The sealed draft reference can participate in cyclic graphs without recursively embedding decoded rows.</remarks>
public sealed class MetadataTypeSpecificationRowReferenceIdentity : IEquatable<MetadataTypeSpecificationRowReferenceIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typespec-row-reference-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationRowReferenceIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeSpecificationToken)
    {
        MetadataModule = metadataModule;
        TypeSpecificationToken = typeSpecificationToken;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(typeSpecificationToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source metadata module containing the TypeSpec row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }
    /// <summary>Gets the exact non-nil TypeSpec token.</summary>
    public int TypeSpecificationToken { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one lightweight TypeSpec-row draft reference.</summary>
    /// <param name="metadataModule">The exact source metadata module.</param>
    /// <param name="typeSpecificationToken">The exact non-nil TypeSpec token.</param>
    /// <returns>A sealed immutable draft row reference suitable for graph edges.</returns>
    public static MetadataTypeSpecificationRowReferenceIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeSpecificationToken)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(typeSpecificationToken, 0x1B, nameof(typeSpecificationToken));
        return new MetadataTypeSpecificationRowReferenceIdentity(metadataModule, typeSpecificationToken);
    }

    /// <summary>Tests canonical equality between two lightweight draft TypeSpec references.</summary>
    /// <param name="other">The other draft reference.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSpecificationRowReferenceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationRowReferenceIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact physical TypeSpec row and its complete bounded blob.</summary>
/// <remarks>This sealed draft layer contains no decoded tree, construction, or use-site classification.</remarks>
public sealed class MetadataTypeSpecificationRowIdentity : IEquatable<MetadataTypeSpecificationRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typespec-row-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signatureBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationRowIdentity(
        MetadataTypeSpecificationRowReferenceIdentity reference,
        ImmutableArray<byte> signatureBytes)
    {
        Reference = reference;
        this.signatureBytes = signatureBytes;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(reference.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(signatureBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact lightweight source row reference.</summary>
    public MetadataTypeSpecificationRowReferenceIdentity Reference { get; }
    /// <summary>Gets a defensive copy of the complete original TypeSpec blob.</summary>
    public ImmutableArray<byte> SignatureBytes => ExpressionV2ContractEncoding.Copy(signatureBytes);
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the physical row's canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact physical bounded TypeSpec-row draft identity.</summary>
    /// <param name="reference">The exact source module and TypeSpec token.</param>
    /// <param name="signatureBytes">The complete original blob within the exact-operation byte cap.</param>
    /// <returns>A sealed immutable physical draft row with no derived analysis.</returns>
    public static MetadataTypeSpecificationRowIdentity Create(
        MetadataTypeSpecificationRowReferenceIdentity reference,
        ImmutableArray<byte> signatureBytes)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (signatureBytes.IsDefault ||
            signatureBytes.Length > StaticFieldV2Limits.MaximumTypeSpecificationByteCount)
        {
            throw new ArgumentException("A complete initialized within-cap TypeSpec blob is required.", nameof(signatureBytes));
        }
        return new MetadataTypeSpecificationRowIdentity(reference, ExpressionV2ContractEncoding.Copy(signatureBytes));
    }

    /// <summary>Tests canonical equality between two physical draft TypeSpec rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSpecificationRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationRowIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes full-consumption provenance tying one complete blob to one complete decoded tree.</summary>
/// <remarks>The sealed draft certificate is decoder evidence; it is not itself a physical metadata row identity.</remarks>
public sealed class MetadataSignatureDecodeCertificate : IEquatable<MetadataSignatureDecodeCertificate>
{
    private const string CanonicalDomain = "metadata-v2-signature-decode-certificate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataSignatureDecodeCertificate(
        string decoderIdentitySha256,
        string sourceBytesSha256,
        string rootSha256,
        int consumedByteCount,
        int nodeCount,
        int maximumDepth,
        int cumulativeGenericArgumentCount)
    {
        DecoderIdentitySha256 = decoderIdentitySha256;
        SourceBytesSha256 = sourceBytesSha256;
        RootSha256 = rootSha256;
        ConsumedByteCount = consumedByteCount;
        NodeCount = nodeCount;
        MaximumDepth = maximumDepth;
        CumulativeGenericArgumentCount = cumulativeGenericArgumentCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(decoderIdentitySha256, nameof(decoderIdentitySha256));
        writer.WriteSha256(sourceBytesSha256, nameof(sourceBytesSha256));
        writer.WriteSha256(rootSha256, nameof(rootSha256));
        writer.WriteInt32(consumedByteCount);
        writer.WriteInt32(nodeCount);
        writer.WriteInt32(maximumDepth);
        writer.WriteInt32(cumulativeGenericArgumentCount);
        writer.WriteBoolean(true);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact decoder implementation identity digest.</summary>
    public string DecoderIdentitySha256 { get; }
    /// <summary>Gets the SHA-256 digest of complete source bytes.</summary>
    public string SourceBytesSha256 { get; }
    /// <summary>Gets the canonical digest of the complete projected root.</summary>
    public string RootSha256 { get; }
    /// <summary>Gets the exact consumed byte count, equal to complete source length.</summary>
    public int ConsumedByteCount { get; }
    /// <summary>Gets the exact complete raw signature-node count.</summary>
    public int NodeCount { get; }
    /// <summary>Gets the exact maximum raw signature depth.</summary>
    public int MaximumDepth { get; }
    /// <summary>Gets the exact cumulative generic-argument count.</summary>
    public int CumulativeGenericArgumentCount { get; }
    /// <summary>Gets the explicit source-end observation retained by every exact certificate.</summary>
    public bool SourceEndObserved => true;
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    internal static MetadataSignatureDecodeCertificate CreateFromDecoder(
        ImmutableArray<byte> sourceBytes,
        MetadataTypeSignatureNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (sourceBytes.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Complete initialized source bytes are required.", nameof(sourceBytes));
        }
        const string decoderIdentitySha256 =
            "d96cc51d50d7cb4f515fddf8e53d6fb02871ca30fa56f908c920b98a3f104045";
        return new MetadataSignatureDecodeCertificate(
            decoderIdentitySha256,
            CanonicalReplayEncoding.ComputeSha256(sourceBytes.AsSpan()),
            root.Sha256,
            sourceBytes.Length,
            root.TopologyNodeCount,
            root.TopologyDepth,
            root.CumulativeGenericArgumentCount);
    }

    /// <summary>Tests canonical equality between two full-consumption draft certificates.</summary>
    /// <param name="other">The other draft certificate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataSignatureDecodeCertificate? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataSignatureDecodeCertificate);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact, incomplete, bound-stopped, or malformed metadata-signature decode operation.</summary>
/// <remarks>
/// The sealed draft outcome retains physical row evidence for exact, incomplete-resolution, and invalid results. A
/// cap-plus-one result keeps the row reference and reached bound but has no source prefix, certificate, or tree.
/// </remarks>
public sealed class MetadataSignatureDecodeOutcome : IEquatable<MetadataSignatureDecodeOutcome>
{
    private const string CanonicalDomain = "metadata-v2-signature-decode-outcome";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataSignatureDecodeOutcome(
        MetadataSignatureDecodeResultKind kind,
        MetadataTypeSpecificationRowReferenceIdentity reference,
        MetadataTypeSpecificationRowIdentity? row,
        MetadataTypeSignatureNode? root,
        MetadataSignatureDecodeCertificate? certificate,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int cumulativeTypeSpecificationRowCount,
        int cumulativeByteCount,
        int cumulativeRawNodeCount,
        int cumulativeGenericArgumentCount,
        string? nonExactCode,
        string? invalidCode)
    {
        Kind = kind;
        Reference = reference;
        Row = row;
        Root = root;
        Certificate = certificate;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        CumulativeTypeSpecificationRowCount = cumulativeTypeSpecificationRowCount;
        CumulativeByteCount = cumulativeByteCount;
        CumulativeRawNodeCount = cumulativeRawNodeCount;
        CumulativeGenericArgumentCount = cumulativeGenericArgumentCount;
        NonExactCode = nonExactCode;
        InvalidCode = invalidCode;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteLengthPrefixedBytes(reference.CanonicalBytes.AsSpan());
        WriteOptional(writer, row?.CanonicalBytes);
        WriteOptional(writer, root?.CanonicalBytes);
        WriteOptional(writer, certificate?.CanonicalBytes);
        writer.WriteBoolean(reachedBound is not null);
        if (reachedBound is not null)
        {
            writer.WriteString(reachedBound.Name);
            writer.WriteInt64(reachedBound.Value);
        }
        writer.WriteInt32(observedCount);
        writer.WriteInt32(cumulativeTypeSpecificationRowCount);
        writer.WriteInt32(cumulativeByteCount);
        writer.WriteInt32(cumulativeRawNodeCount);
        writer.WriteInt32(cumulativeGenericArgumentCount);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, nonExactCode);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, invalidCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact closed decode outcome kind.</summary>
    public MetadataSignatureDecodeResultKind Kind { get; }
    /// <summary>Gets the exact lightweight physical row reference.</summary>
    public MetadataTypeSpecificationRowReferenceIdentity Reference { get; }
    /// <summary>Gets the complete physical row for exact, incomplete-resolution, or malformed complete-source outcomes.</summary>
    public MetadataTypeSpecificationRowIdentity? Row { get; }
    /// <summary>Gets the complete projected tree only for an exact outcome.</summary>
    public MetadataTypeSignatureNode? Root { get; }
    /// <summary>Gets full-consumption evidence only for an exact outcome.</summary>
    public MetadataSignatureDecodeCertificate? Certificate { get; }
    /// <summary>Gets the exact reached operation bound only for a cap-plus-one outcome.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets the exact cap-plus-one counter value, otherwise zero.</summary>
    public int ObservedCount { get; }
    /// <summary>Gets the exact cumulative TypeSpec-row count for an exact graph prefix ending at source end.</summary>
    public int CumulativeTypeSpecificationRowCount { get; }
    /// <summary>Gets the exact cumulative complete-source byte count for an exact graph.</summary>
    public int CumulativeByteCount { get; }
    /// <summary>Gets the exact cumulative raw signature-node count for an exact graph.</summary>
    public int CumulativeRawNodeCount { get; }
    /// <summary>Gets the exact cumulative generic-argument count for an exact graph.</summary>
    public int CumulativeGenericArgumentCount { get; }
    /// <summary>Gets the stable incomplete-evidence code only for a non-bound, non-exact outcome.</summary>
    public string? NonExactCode { get; }
    /// <summary>Gets the stable invalid-decode code only for a malformed outcome.</summary>
    public string? InvalidCode { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one full-consumption exact draft decode outcome.</summary>
    /// <param name="row">The exact physical TypeSpec row and complete blob.</param>
    /// <param name="root">The complete projected raw signature tree.</param>
    /// <param name="certificate">The full-consumption decoder certificate tied to row bytes and root.</param>
    /// <param name="cumulativeTypeSpecificationRowCount">The exact cumulative graph row count.</param>
    /// <param name="cumulativeByteCount">The exact cumulative complete-source byte count.</param>
    /// <param name="cumulativeRawNodeCount">The exact cumulative raw signature-node count.</param>
    /// <param name="cumulativeGenericArgumentCount">The exact cumulative generic-argument count.</param>
    /// <returns>A sealed immutable exact draft decode outcome.</returns>
    internal static MetadataSignatureDecodeOutcome Exact(
        MetadataTypeSpecificationRowIdentity row,
        MetadataTypeSignatureNode root,
        MetadataSignatureDecodeCertificate certificate,
        int cumulativeTypeSpecificationRowCount,
        int cumulativeByteCount,
        int cumulativeRawNodeCount,
        int cumulativeGenericArgumentCount)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(certificate);
        if (!string.Equals(
                certificate.SourceBytesSha256,
                CanonicalReplayEncoding.ComputeSha256(row.SignatureBytes.AsSpan()),
                StringComparison.Ordinal) ||
            !string.Equals(certificate.RootSha256, root.Sha256, StringComparison.Ordinal) ||
            certificate.ConsumedByteCount != row.SignatureBytes.Length ||
            certificate.NodeCount != root.TopologyNodeCount ||
            certificate.MaximumDepth != root.TopologyDepth ||
            certificate.CumulativeGenericArgumentCount != root.CumulativeGenericArgumentCount)
        {
            throw new ArgumentException("The full-consumption certificate must exactly bind row bytes and root.", nameof(certificate));
        }
        if (cumulativeTypeSpecificationRowCount is < 1 or > StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount ||
            cumulativeByteCount < row.SignatureBytes.Length ||
            cumulativeByteCount > StaticFieldV2Limits.MaximumTypeSpecificationByteCount ||
            cumulativeRawNodeCount < root.TopologyNodeCount ||
            cumulativeRawNodeCount > StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount ||
            cumulativeGenericArgumentCount < root.CumulativeGenericArgumentCount ||
            cumulativeGenericArgumentCount > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount)
        {
            throw new ArgumentException("Exact cumulative counters must include the row and remain within every operation bound.");
        }
        return new MetadataSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.Exact,
            row.Reference,
            row,
            root,
            certificate,
            null,
            0,
            cumulativeTypeSpecificationRowCount,
            cumulativeByteCount,
            cumulativeRawNodeCount,
            cumulativeGenericArgumentCount,
            null,
            null);
    }

    /// <summary>Creates one cap-plus-one draft decode outcome with no prefix tree or bytes.</summary>
    /// <param name="reference">The exact physical source row reference.</param>
    /// <param name="reachedBound">The exact signature or TypeSpec-graph operation bound reached.</param>
    /// <param name="observedCount">Exactly the bound limit plus one.</param>
    /// <returns>A sealed immutable non-exact draft outcome retaining no prefix candidate.</returns>
    internal static MetadataSignatureDecodeOutcome BoundReached(
        MetadataTypeSpecificationRowReferenceIdentity reference,
        EvaluationDeterministicBound reachedBound,
        int observedCount)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(reachedBound);
        var admitted = ExpressionV2ContractLimits.AllDeclaredBounds.Any(bound =>
            string.Equals(bound.Name, reachedBound.Name, StringComparison.Ordinal) && bound.Value == reachedBound.Value) &&
            reachedBound.Name is
                ExpressionV2ContractLimits.TypeSpecificationByteCountBoundName or
                ExpressionV2ContractLimits.TypeSpecificationDepthBoundName or
                ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName or
                ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName or
                ExpressionV2ContractLimits.TypeSpecificationRowTraversalCountBoundName or
                ExpressionV2ContractLimits.SignatureTokenResolutionCountBoundName or
                ExpressionV2ContractLimits.ArrayRankBoundName or
                ExpressionV2ContractLimits.GenericParameterCountBoundName or
                ExpressionV2ContractLimits.ParameterCountBoundName;
        if (!admitted || reachedBound.Value >= int.MaxValue || observedCount != reachedBound.Value + 1)
        {
            throw new ArgumentException("A declared signature bound and its exact cap-plus-one count are required.");
        }
        return new MetadataSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.NonExact,
            reference,
            null,
            null,
            null,
            reachedBound,
            observedCount,
            0,
            0,
            0,
            0,
            null,
            null);
    }

    /// <summary>Creates one incomplete-resolution draft outcome with no projected tree.</summary>
    /// <param name="row">The complete physical TypeSpec row whose required in-range token was unresolved.</param>
    /// <param name="nonExactCode">A stable uppercase W8 incomplete-evidence code.</param>
    /// <returns>A sealed immutable non-exact draft outcome retaining no projected prefix tree.</returns>
    internal static MetadataSignatureDecodeOutcome Incomplete(
        MetadataTypeSpecificationRowIdentity row,
        string nonExactCode)
    {
        ArgumentNullException.ThrowIfNull(row);
        ExpressionV2ContractEncoding.RequireDiagnosticCode(nonExactCode, nameof(nonExactCode));
        return new MetadataSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.NonExact,
            row.Reference,
            row,
            null,
            null,
            null,
            0,
            0,
            row.SignatureBytes.Length,
            0,
            0,
            nonExactCode,
            null);
    }

    /// <summary>Creates one malformed complete-source draft decode outcome with no projected tree.</summary>
    /// <param name="row">The exact physical TypeSpec row and retained complete bytes.</param>
    /// <param name="invalidCode">A stable uppercase W8 invalid-decode code.</param>
    /// <returns>A sealed immutable invalid draft outcome retaining row bytes but no prefix tree.</returns>
    internal static MetadataSignatureDecodeOutcome Invalid(
        MetadataTypeSpecificationRowIdentity row,
        string invalidCode)
    {
        ArgumentNullException.ThrowIfNull(row);
        ExpressionV2ContractEncoding.RequireDiagnosticCode(invalidCode, nameof(invalidCode));
        return new MetadataSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.Invalid,
            row.Reference,
            row,
            null,
            null,
            null,
            0,
            0,
            row.SignatureBytes.Length,
            0,
            0,
            null,
            invalidCode);
    }

    /// <summary>Tests canonical equality between two typed draft decode outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataSignatureDecodeOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataSignatureDecodeOutcome);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void WriteOptional(CanonicalReplayEncoding.Writer writer, ImmutableArray<byte>? bytes)
    {
        writer.WriteBoolean(bytes.HasValue);
        if (bytes.HasValue)
        {
            writer.WriteLengthPrefixedBytes(bytes.Value.AsSpan());
        }
    }
}

/// <summary>Freezes one exact physical TypeDefOrRef target without erasing its source row form.</summary>
/// <remarks>
/// The sealed draft union reuses the complete W7 TypeRef resolution. Only its TypeSpec alternative carries a signature
/// blob, so a direct TypeDef or TypeRef can never acquire invented TypeSpec bytes.
/// </remarks>
public sealed class MetadataTypeDefOrRefTargetIdentity : IEquatable<MetadataTypeDefOrRefTargetIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typedef-or-ref-target-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefOrRefTargetIdentity(
        MetadataTypeDefOrRefTargetKind kind,
        MetadataTypeDefinitionIdentity? directDefinition,
        StaticFieldTypeReferenceResolutionIdentity? typeReferenceResolution,
        MetadataTypeDefinitionIdentity? resolvedReferenceDefinition,
        MetadataTypeSpecificationRowIdentity? typeSpecification)
    {
        Kind = kind;
        DirectDefinition = directDefinition;
        TypeReferenceResolution = typeReferenceResolution;
        ResolvedReferenceDefinition = resolvedReferenceDefinition;
        TypeSpecification = typeSpecification;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        WriteOptional(writer, directDefinition?.CanonicalBytes);
        WriteOptional(writer, typeReferenceResolution?.CanonicalBytes);
        WriteOptional(writer, resolvedReferenceDefinition?.CanonicalBytes);
        WriteOptional(writer, typeSpecification?.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact closed target alternative.</summary>
    public MetadataTypeDefOrRefTargetKind Kind { get; }
    /// <summary>Gets the exact direct TypeDef only for a TypeDef target.</summary>
    public MetadataTypeDefinitionIdentity? DirectDefinition { get; }
    /// <summary>Gets the complete exact W7 TypeRef resolution only for a TypeRef target.</summary>
    public StaticFieldTypeReferenceResolutionIdentity? TypeReferenceResolution { get; }
    /// <summary>Gets the W8-classified target TypeDef only for a TypeRef target.</summary>
    public MetadataTypeDefinitionIdentity? ResolvedReferenceDefinition { get; }
    /// <summary>Gets the exact TypeSpec row and bytes only for a TypeSpec target.</summary>
    public MetadataTypeSpecificationRowIdentity? TypeSpecification { get; }
    /// <summary>Gets the source metadata module containing the decoded TypeDefOrRef token.</summary>
    public StaticFieldMetadataModuleIdentity SourceMetadataModule => Kind switch
    {
        MetadataTypeDefOrRefTargetKind.TypeDefinition => DirectDefinition!.PhysicalDefinition.MetadataModule,
        MetadataTypeDefOrRefTargetKind.TypeReference => TypeReferenceResolution!.SourceModule,
        MetadataTypeDefOrRefTargetKind.TypeSpecification => TypeSpecification!.Reference.MetadataModule,
        _ => throw new InvalidOperationException("The target kind is outside the closed draft union."),
    };
    /// <summary>Gets the exact decoded TypeDef, TypeRef, or TypeSpec source token.</summary>
    public int SourceMetadataToken => Kind switch
    {
        MetadataTypeDefOrRefTargetKind.TypeDefinition => DirectDefinition!.TypeDefinitionToken,
        MetadataTypeDefOrRefTargetKind.TypeReference => TypeReferenceResolution!.ReferencedType.TypeReferenceToken,
        MetadataTypeDefOrRefTargetKind.TypeSpecification => TypeSpecification!.Reference.TypeSpecificationToken,
        _ => throw new InvalidOperationException("The target kind is outside the closed draft union."),
    };
    /// <summary>Gets the exact resolved named TypeDef when the target has one, otherwise null.</summary>
    public MetadataTypeDefinitionIdentity? ResolvedDefinition => Kind switch
    {
        MetadataTypeDefOrRefTargetKind.TypeDefinition => DirectDefinition,
        MetadataTypeDefOrRefTargetKind.TypeReference => ResolvedReferenceDefinition,
        MetadataTypeDefOrRefTargetKind.TypeSpecification => null,
        _ => null,
    };
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a direct-TypeDef draft target in the definition's exact source module.</summary>
    /// <param name="definition">The exact W8-classified TypeDef.</param>
    /// <returns>A sealed immutable draft target carrying no signature blob.</returns>
    public static MetadataTypeDefOrRefTargetIdentity FromTypeDefinition(MetadataTypeDefinitionIdentity definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new MetadataTypeDefOrRefTargetIdentity(
            MetadataTypeDefOrRefTargetKind.TypeDefinition,
            definition,
            null,
            null,
            null);
    }

    /// <summary>Creates a TypeRef draft target from one complete detached resolution.</summary>
    /// <param name="resolution">The exact W7 TypeRef row, source module, scope route, and target TypeDef.</param>
    /// <param name="resolvedDefinition">The W8 classification of the same exact resolved TypeDef.</param>
    /// <returns>A sealed immutable draft target carrying no signature blob.</returns>
    public static MetadataTypeDefOrRefTargetIdentity FromTypeReference(
        StaticFieldTypeReferenceResolutionIdentity resolution,
        MetadataTypeDefinitionIdentity resolvedDefinition)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(resolvedDefinition);
        if (!resolvedDefinition.RawDefinition.MatchesPinnedW7(resolution.ResolvedTargetType))
        {
            throw new ArgumentException("The W8 target definition must classify the exact TypeRef resolution target.", nameof(resolvedDefinition));
        }
        return new MetadataTypeDefOrRefTargetIdentity(
            MetadataTypeDefOrRefTargetKind.TypeReference,
            null,
            resolution,
            resolvedDefinition,
            null);
    }

    /// <summary>Creates a TypeSpec draft target retaining its exact source module, row token, and bytes.</summary>
    /// <param name="typeSpecification">The exact non-recursive TypeSpec identity.</param>
    /// <returns>A sealed immutable draft target distinct from equal-byte rows at other tokens.</returns>
    public static MetadataTypeDefOrRefTargetIdentity FromTypeSpecification(
        MetadataTypeSpecificationRowIdentity typeSpecification)
    {
        ArgumentNullException.ThrowIfNull(typeSpecification);
        return new MetadataTypeDefOrRefTargetIdentity(
            MetadataTypeDefOrRefTargetKind.TypeSpecification,
            null,
            null,
            null,
            typeSpecification);
    }

    /// <summary>Tests canonical equality between two draft TypeDefOrRef targets.</summary>
    /// <param name="other">The other draft target.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeDefOrRefTargetIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeDefOrRefTargetIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void WriteOptional(CanonicalReplayEncoding.Writer writer, ImmutableArray<byte>? bytes)
    {
        writer.WriteBoolean(bytes.HasValue);
        if (bytes.HasValue)
        {
            writer.WriteLengthPrefixedBytes(bytes.Value.AsSpan());
        }
    }
}

/// <summary>Freezes exact physical row ends for every metadata table consumed by the W8 type contracts.</summary>
/// <remarks>
/// This sealed draft identity is derived only from one exact module-search fact for the same module and content. Row
/// end zero denotes an empty table; a positive end is the greatest physical row identifier in that table.
/// </remarks>
public sealed class MetadataSourceEndIdentity : IEquatable<MetadataSourceEndIdentity>
{
    private const string CanonicalDomain = "metadata-v2-source-end-identity";
    private const int CanonicalSchemaVersion = 4;
    private const int MaximumMetadataRowId = 0x00FF_FFFF;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataSourceEndIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        StaticFieldModuleSearchFact sourceModuleFact,
        int typeDefinitionRowEnd,
        int typeReferenceRowEnd,
        int typeSpecificationRowEnd,
        int fieldDefinitionRowEnd,
        int fieldPointerRowEnd,
        int methodDefinitionRowEnd,
        int methodPointerRowEnd,
        int parameterDefinitionRowEnd,
        int parameterPointerRowEnd,
        int nestedClassRowEnd,
        int genericParameterRowEnd,
        int interfaceImplementationRowEnd,
        int genericParameterConstraintRowEnd)
    {
        SourceModule = sourceModule;
        SourceModuleFact = sourceModuleFact;
        TypeDefinitionRowCount = typeDefinitionRowEnd;
        TypeReferenceRowCount = typeReferenceRowEnd;
        TypeSpecificationRowCount = typeSpecificationRowEnd;
        FieldDefinitionRowCount = fieldDefinitionRowEnd;
        FieldPointerRowCount = fieldPointerRowEnd;
        MethodDefinitionRowCount = methodDefinitionRowEnd;
        MethodPointerRowCount = methodPointerRowEnd;
        ParameterDefinitionRowCount = parameterDefinitionRowEnd;
        ParameterPointerRowCount = parameterPointerRowEnd;
        NestedClassRowCount = nestedClassRowEnd;
        GenericParameterRowCount = genericParameterRowEnd;
        InterfaceImplementationRowCount = interfaceImplementationRowEnd;
        GenericParameterConstraintRowCount = genericParameterConstraintRowEnd;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(sourceModuleFact.CanonicalBytes.AsSpan());
        writer.WriteInt32(typeDefinitionRowEnd);
        writer.WriteInt32(typeReferenceRowEnd);
        writer.WriteInt32(typeSpecificationRowEnd);
        writer.WriteInt32(fieldDefinitionRowEnd);
        writer.WriteInt32(fieldPointerRowEnd);
        writer.WriteInt32(methodDefinitionRowEnd);
        writer.WriteInt32(methodPointerRowEnd);
        writer.WriteInt32(parameterDefinitionRowEnd);
        writer.WriteInt32(parameterPointerRowEnd);
        writer.WriteInt32(nestedClassRowEnd);
        writer.WriteInt32(genericParameterRowEnd);
        writer.WriteInt32(interfaceImplementationRowEnd);
        writer.WriteInt32(genericParameterConstraintRowEnd);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module whose physical table ends were observed.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }
    /// <summary>Gets the exact exhaustive module fact from which every row end was derived.</summary>
    public StaticFieldModuleSearchFact SourceModuleFact { get; }
    /// <summary>Gets the exact TypeDef table row end, or zero for an empty table.</summary>
    public int TypeDefinitionRowCount { get; }
    /// <summary>Gets the exact TypeRef table row end, or zero for an empty table.</summary>
    public int TypeReferenceRowCount { get; }
    /// <summary>Gets the exact TypeSpec table row end, or zero for an empty table.</summary>
    public int TypeSpecificationRowCount { get; }
    /// <summary>Gets the exact FieldDef table row end, or zero for an empty table.</summary>
    public int FieldDefinitionRowCount { get; }
    /// <summary>Gets the exact FieldPtr table row end, or zero when direct FieldDef list pointers are used.</summary>
    public int FieldPointerRowCount { get; }
    /// <summary>Gets the exact MethodDef table row end, or zero for an empty table.</summary>
    public int MethodDefinitionRowCount { get; }
    /// <summary>Gets the exact MethodPtr table row end, or zero when direct MethodDef list pointers are used.</summary>
    public int MethodPointerRowCount { get; }
    /// <summary>Gets the exact Param table row end, or zero for an empty table.</summary>
    public int ParameterDefinitionRowCount { get; }
    /// <summary>Gets the exact ParamPtr table row end, or zero when direct Param list pointers are used.</summary>
    public int ParameterPointerRowCount { get; }
    /// <summary>Gets the exact NestedClass table row end, or zero for an empty table.</summary>
    public int NestedClassRowCount { get; }
    /// <summary>Gets the exact GenericParam table row end, or zero for an empty table.</summary>
    public int GenericParameterRowCount { get; }
    /// <summary>Gets the exact InterfaceImpl table row end, or zero for an empty table.</summary>
    public int InterfaceImplementationRowCount { get; }
    /// <summary>Gets the exact GenericParamConstraint table row end, or zero for an empty table.</summary>
    public int GenericParameterConstraintRowCount { get; }
    /// <summary>Gets the explicit source-end observation retained by every exact draft identity.</summary>
    public bool SourceEndObserved => true;
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Derives exact physical table ends from one exact module-search fact.</summary>
    /// <param name="sourceModule">The exact metadata module containing every covered table.</param>
    /// <param name="sourceModuleFact">The exact exhaustive fact for the same module and metadata content.</param>
    /// <returns>A sealed immutable draft identity covering every required metadata source end.</returns>
    public static MetadataSourceEndIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        StaticFieldModuleSearchFact sourceModuleFact)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(sourceModuleFact);
        if (sourceModuleFact.Status != StaticFieldModuleSearchStatus.Exact ||
            sourceModuleFact.ModuleContent is not { } moduleContent ||
            !sourceModuleFact.Module.Equals(sourceModule.Module) ||
            !moduleContent.Equals(sourceModule.ModuleContent) ||
            sourceModuleFact.TypeDefinitionRowCount is not { } typeDefinitionRowEnd ||
            sourceModuleFact.TypeReferenceRowCount is not { } typeReferenceRowEnd ||
            sourceModuleFact.TypeSpecificationRowCount is not { } typeSpecificationRowEnd ||
            sourceModuleFact.FieldDefinitionRowCount is not { } fieldDefinitionRowEnd ||
            sourceModuleFact.FieldPointerRowCount is not { } fieldPointerRowEnd ||
            sourceModuleFact.MethodDefinitionRowCount is not { } methodDefinitionRowEnd ||
            sourceModuleFact.MethodPointerRowCount is not { } methodPointerRowEnd ||
            sourceModuleFact.ParameterDefinitionRowCount is not { } parameterDefinitionRowEnd ||
            sourceModuleFact.ParameterPointerRowCount is not { } parameterPointerRowEnd ||
            sourceModuleFact.NestedClassRowCount is not { } nestedClassRowEnd ||
            sourceModuleFact.GenericParameterRowCount is not { } genericParameterRowEnd ||
            sourceModuleFact.InterfaceImplementationRowCount is not { } interfaceImplementationRowEnd ||
            sourceModuleFact.GenericParameterConstraintRowCount is not { } genericParameterConstraintRowEnd)
        {
            throw new ArgumentException(
                "Metadata source ends require an exact exhaustive fact for the same module and content.",
                nameof(sourceModuleFact));
        }

        var rowEnds = new[]
        {
            typeDefinitionRowEnd,
            typeReferenceRowEnd,
            typeSpecificationRowEnd,
            fieldDefinitionRowEnd,
            fieldPointerRowEnd,
            methodDefinitionRowEnd,
            methodPointerRowEnd,
            parameterDefinitionRowEnd,
            parameterPointerRowEnd,
            nestedClassRowEnd,
            genericParameterRowEnd,
            interfaceImplementationRowEnd,
            genericParameterConstraintRowEnd,
        };
        if (rowEnds.Any(static rowEnd => rowEnd > MaximumMetadataRowId))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceModuleFact),
                "Every covered metadata table row end must fit a physical metadata token row identifier.");
        }

        return new MetadataSourceEndIdentity(
            sourceModule,
            sourceModuleFact,
            typeDefinitionRowEnd,
            typeReferenceRowEnd,
            typeSpecificationRowEnd,
            fieldDefinitionRowEnd,
            fieldPointerRowEnd,
            methodDefinitionRowEnd,
            methodPointerRowEnd,
            parameterDefinitionRowEnd,
            parameterPointerRowEnd,
            nestedClassRowEnd,
            genericParameterRowEnd,
            interfaceImplementationRowEnd,
            genericParameterConstraintRowEnd);
    }

    /// <summary>Tests canonical equality between two exact metadata source-end identities.</summary>
    /// <param name="other">The other draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataSourceEndIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataSourceEndIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal bool ContainsTypeDefOrRefToken(int token)
    {
        var rowId = token & MaximumMetadataRowId;
        return rowId > 0 && (token & unchecked((int)0xFF00_0000)) switch
        {
            0x0200_0000 => rowId <= TypeDefinitionRowCount,
            0x0100_0000 => rowId <= TypeReferenceRowCount,
            0x1B00_0000 => rowId <= TypeSpecificationRowCount,
            _ => false,
        };
    }

    internal bool ContainsTypeDefinitionToken(int token)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(token);
        return CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x02) &&
               rowId > 0 &&
               rowId <= TypeDefinitionRowCount;
    }

    internal bool ContainsMethodDefinitionToken(int token)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(token);
        return CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x06) &&
               rowId > 0 &&
               rowId <= MethodDefinitionRowCount;
    }

    internal bool ContainsGenericParameterToken(int token)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(token);
        return CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x2A) &&
               rowId > 0 &&
               rowId <= GenericParameterRowCount;
    }
}

/// <summary>Resolves one source-module TypeDefOrRef token occurrence for deterministic signature decoding.</summary>
/// <remarks>The sealed draft entry carries either a complete named resolution or a lightweight TypeSpec graph edge.</remarks>
public sealed class MetadataSignatureTokenResolutionEntry : IEquatable<MetadataSignatureTokenResolutionEntry>
{
    private const string CanonicalDomain = "metadata-v2-signature-token-resolution-entry";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataSignatureTokenResolutionEntry(
        int sourceMetadataToken,
        MetadataTypeDefOrRefTargetIdentity? namedTarget,
        ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain,
        MetadataTypeSpecificationRowReferenceIdentity? typeSpecificationReference)
    {
        SourceMetadataToken = sourceMetadataToken;
        NamedTarget = namedTarget;
        this.definitionChain = definitionChain;
        TypeSpecificationReference = typeSpecificationReference;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(sourceMetadataToken);
        writer.WriteBoolean(namedTarget is not null);
        if (namedTarget is not null)
        {
            writer.WriteLengthPrefixedBytes(namedTarget.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, definitionChain, static value => value.CanonicalBytes);
        writer.WriteBoolean(typeSpecificationReference is not null);
        if (typeSpecificationReference is not null)
        {
            writer.WriteLengthPrefixedBytes(typeSpecificationReference.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source TypeDef, TypeRef, or TypeSpec token.</summary>
    public int SourceMetadataToken { get; }
    /// <summary>Gets a complete direct TypeDef-or-TypeRef target only for a named occurrence.</summary>
    public MetadataTypeDefOrRefTargetIdentity? NamedTarget { get; }
    /// <summary>Gets the exact outer-to-inner W8 definition chain only for a named occurrence.</summary>
    public ImmutableArray<MetadataTypeDefinitionIdentity> DefinitionChain =>
        ExpressionV2ContractEncoding.Copy(definitionChain);
    /// <summary>Gets a lightweight TypeSpec row reference only for a TypeSpec occurrence.</summary>
    public MetadataTypeSpecificationRowReferenceIdentity? TypeSpecificationReference { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete named TypeDef-or-TypeRef token-resolution draft entry.</summary>
    /// <param name="target">The exact direct target in the decoder's source module.</param>
    /// <param name="definitionChain">The complete resolved outer-to-inner definition chain.</param>
    /// <returns>A sealed immutable draft token-resolution entry.</returns>
    public static MetadataSignatureTokenResolutionEntry Named(
        MetadataTypeDefOrRefTargetIdentity target,
        ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind == MetadataTypeDefOrRefTargetKind.TypeSpecification)
        {
            throw new ArgumentException("A named token resolution must be direct TypeDef or TypeRef.", nameof(target));
        }
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            definitionChain,
            nameof(definitionChain),
            StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1);
        if (copied.IsEmpty || target.ResolvedDefinition is null || !target.ResolvedDefinition.Equals(copied[^1]))
        {
            throw new ArgumentException("The named definition chain must end at the exact resolved target.", nameof(definitionChain));
        }
        return new MetadataSignatureTokenResolutionEntry(
            target.SourceMetadataToken,
            target,
            copied,
            null);
    }

    /// <summary>Creates one lightweight TypeSpec-token graph-edge draft entry.</summary>
    /// <param name="reference">The exact source module and TypeSpec token.</param>
    /// <returns>A sealed immutable draft entry containing no recursively decoded row.</returns>
    public static MetadataSignatureTokenResolutionEntry TypeSpecification(
        MetadataTypeSpecificationRowReferenceIdentity reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new MetadataSignatureTokenResolutionEntry(
            reference.TypeSpecificationToken,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            reference);
    }

    /// <summary>Tests canonical equality between two draft token-resolution entries.</summary>
    /// <param name="other">The other draft entry.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataSignatureTokenResolutionEntry? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataSignatureTokenResolutionEntry);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes a detached unique token-resolution catalog for one source metadata module.</summary>
/// <remarks>
/// The sealed draft catalog is immutable decoder input. Exact entries are canonicalized by physical token. A bounded
/// non-exact catalog retains exact source ends and cap-plus-one evidence but deliberately exposes no usable prefix.
/// </remarks>
public sealed class MetadataSignatureTokenResolutionCatalog : IEquatable<MetadataSignatureTokenResolutionCatalog>
{
    private const string CanonicalDomain = "metadata-v2-signature-token-resolution-catalog";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<MetadataSignatureTokenResolutionEntry> entries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataSignatureTokenResolutionCatalog(
        MetadataSourceEndIdentity sourceEnds,
        MetadataSignatureTokenResolutionCatalogResultKind resultKind,
        ImmutableArray<MetadataSignatureTokenResolutionEntry> entries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        SourceEnds = sourceEnds;
        ResultKind = resultKind;
        this.entries = entries;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)resultKind);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, entries, static value => value.CanonicalBytes);
        writer.WriteBoolean(reachedBound is not null);
        if (reachedBound is not null)
        {
            writer.WriteString(reachedBound.Name);
            writer.WriteInt64(reachedBound.Value);
        }
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source-end identity governing every token in this draft catalog.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }
    /// <summary>Gets the exact source metadata module whose token domain is resolved.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => SourceEnds.SourceModule;
    /// <summary>Gets whether the draft catalog is exact or stopped at cap-plus-one.</summary>
    public MetadataSignatureTokenResolutionCatalogResultKind ResultKind { get; }
    /// <summary>Gets a defensive copy of exact unique token-resolution entries in physical token order.</summary>
    public ImmutableArray<MetadataSignatureTokenResolutionEntry> Entries =>
        ExpressionV2ContractEncoding.Copy(entries);
    /// <summary>Gets the declared token-resolution bound only for a cap-plus-one catalog.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets the exact cap-plus-one observation count, otherwise zero.</summary>
    public int ObservedCount { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one detached source-module token-resolution draft catalog.</summary>
    /// <param name="sourceEnds">The exact table ends for the module containing every decoded token occurrence.</param>
    /// <param name="entries">The initialized resolution map entries acquired before source end or cap-plus-one.</param>
    /// <returns>An exact canonical map, or a typed non-exact draft catalog with no usable prefix.</returns>
    public static MetadataSignatureTokenResolutionCatalog Create(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataSignatureTokenResolutionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        if (entries.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized token-resolution map is required.", nameof(entries));
        }
        if (entries.Length > StaticFieldV2Limits.MaximumSignatureTokenResolutionCount)
        {
            return new MetadataSignatureTokenResolutionCatalog(
                sourceEnds,
                MetadataSignatureTokenResolutionCatalogResultKind.NonExact,
                ImmutableArray<MetadataSignatureTokenResolutionEntry>.Empty,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.SignatureTokenResolutionCountBoundName,
                    StaticFieldV2Limits.MaximumSignatureTokenResolutionCount),
                StaticFieldV2Limits.MaximumSignatureTokenResolutionCount + 1);
        }

        var copied = entries.ToImmutableArray();
        var seen = new HashSet<int>();
        foreach (var entry in copied)
        {
            if (entry is null)
            {
                throw new ArgumentException("Token-resolution entries cannot contain null.", nameof(entries));
            }
            var module = entry.NamedTarget?.SourceMetadataModule ?? entry.TypeSpecificationReference!.MetadataModule;
            if (!module.Equals(sourceEnds.SourceModule) ||
                !sourceEnds.ContainsTypeDefOrRefToken(entry.SourceMetadataToken))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries),
                    "Every resolution entry must name a physical token within the exact source module table ends.");
            }
            if (!seen.Add(entry.SourceMetadataToken))
            {
                throw new ArgumentException("Resolution entries must contain unique physical tokens.", nameof(entries));
            }
        }

        var canonicalEntries = copied
            .OrderBy(static entry => entry.SourceMetadataToken)
            .ToImmutableArray();
        return new MetadataSignatureTokenResolutionCatalog(
            sourceEnds,
            MetadataSignatureTokenResolutionCatalogResultKind.Exact,
            canonicalEntries,
            null,
            0);
    }

    /// <summary>Tests canonical equality between two detached draft token catalogs.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataSignatureTokenResolutionCatalog? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataSignatureTokenResolutionCatalog);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataSignatureTokenResolutionEntry? Resolve(int token) =>
        ResultKind == MetadataSignatureTokenResolutionCatalogResultKind.Exact
            ? entries.FirstOrDefault(entry => entry.SourceMetadataToken == token)
            : null;

    internal bool ContainsSourceToken(int token) => SourceEnds.ContainsTypeDefOrRefToken(token);
}

/// <summary>Freezes one bounded, complete pre-substitution metadata signature node.</summary>
/// <remarks>
/// This sealed draft tree retains VAR/MVAR, ordered custom modifiers, exact ARRAY shape vectors, and lossless bytes for
/// function pointers. Only the explicitly admitted projection can become a <see cref="MetadataClosedTypeIdentity"/>.
/// </remarks>
public sealed class MetadataTypeSignatureNode : IEquatable<MetadataTypeSignatureNode>
{
    private const string CanonicalDomain = "metadata-v2-type-signature-node";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataTypeDefinitionIdentity> namedDefinitionChain;
    private readonly ImmutableArray<int> arraySizes;
    private readonly ImmutableArray<int> arrayLowerBounds;
    private readonly ImmutableArray<byte> opaqueSignatureBytes;
    private readonly ImmutableArray<MetadataTypeSignatureNode> children;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSignatureNode(
        MetadataTypeSignatureNodeKind kind,
        MetadataPrimitiveTypeKind? primitiveKind,
        MetadataNamedSignatureHeadKind? namedHeadKind,
        MetadataTypeDefOrRefTargetIdentity? namedTarget,
        MetadataTypeSpecificationRowReferenceIdentity? modifierTypeSpecificationReference,
        ImmutableArray<MetadataTypeDefinitionIdentity> namedDefinitionChain,
        int? variableIndex,
        int? indirectTypeSpecificationToken,
        int? arrayRank,
        ImmutableArray<int> arraySizes,
        ImmutableArray<int> arrayLowerBounds,
        ImmutableArray<byte> opaqueSignatureBytes,
        ImmutableArray<MetadataTypeSignatureNode> children,
        int topologyDepth,
        int topologyNodeCount,
        int cumulativeGenericArgumentCount,
        int? functionPointerHeader,
        int? functionPointerGenericParameterCount,
        int? functionPointerRequiredParameterCount)
    {
        Kind = kind;
        PrimitiveKind = primitiveKind;
        NamedHeadKind = namedHeadKind;
        NamedTarget = namedTarget;
        ModifierTypeSpecificationReference = modifierTypeSpecificationReference;
        this.namedDefinitionChain = namedDefinitionChain;
        VariableIndex = variableIndex;
        IndirectTypeSpecificationToken = indirectTypeSpecificationToken;
        ArrayRank = arrayRank;
        this.arraySizes = arraySizes;
        this.arrayLowerBounds = arrayLowerBounds;
        this.opaqueSignatureBytes = opaqueSignatureBytes;
        this.children = children;
        TopologyDepth = topologyDepth;
        TopologyNodeCount = topologyNodeCount;
        CumulativeGenericArgumentCount = cumulativeGenericArgumentCount;
        FunctionPointerHeader = functionPointerHeader;
        FunctionPointerGenericParameterCount = functionPointerGenericParameterCount;
        FunctionPointerRequiredParameterCount = functionPointerRequiredParameterCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, primitiveKind);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, namedHeadKind);
        writer.WriteBoolean(namedTarget is not null);
        if (namedTarget is not null)
        {
            writer.WriteLengthPrefixedBytes(namedTarget.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(modifierTypeSpecificationReference is not null);
        if (modifierTypeSpecificationReference is not null)
        {
            writer.WriteLengthPrefixedBytes(modifierTypeSpecificationReference.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            namedDefinitionChain,
            static definition => definition.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, variableIndex);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, indirectTypeSpecificationToken);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, arrayRank);
        WriteInt32Array(writer, arraySizes);
        WriteInt32Array(writer, arrayLowerBounds);
        writer.WriteLengthPrefixedBytes(opaqueSignatureBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, children, static child => child.CanonicalBytes);
        writer.WriteInt32(topologyDepth);
        writer.WriteInt32(topologyNodeCount);
        writer.WriteInt32(cumulativeGenericArgumentCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, functionPointerHeader);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, functionPointerGenericParameterCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, functionPointerRequiredParameterCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact signature-node kind.</summary>
    public MetadataTypeSignatureNodeKind Kind { get; }
    /// <summary>Gets the primitive kind only for a primitive node.</summary>
    public MetadataPrimitiveTypeKind? PrimitiveKind { get; }
    /// <summary>Gets the exact raw CLASS or VALUETYPE code only for a named node or generic head.</summary>
    public MetadataNamedSignatureHeadKind? NamedHeadKind { get; }
    /// <summary>Gets the exact direct TypeDef-or-TypeRef target for a named node or generic head.</summary>
    public MetadataTypeDefOrRefTargetIdentity? NamedTarget { get; }
    /// <summary>Gets a lightweight TypeSpec row reference only for a custom-modifier occurrence.</summary>
    public MetadataTypeSpecificationRowReferenceIdentity? ModifierTypeSpecificationReference { get; }
    /// <summary>Gets a defensive copy of the exact outer-to-inner resolved TypeDef chain for a named form.</summary>
    public ImmutableArray<MetadataTypeDefinitionIdentity> NamedDefinitionChain =>
        ExpressionV2ContractEncoding.Copy(namedDefinitionChain);
    /// <summary>Gets the final resolved TypeDef for a named form, otherwise null.</summary>
    public MetadataTypeDefinitionIdentity? NamedDefinition =>
        namedDefinitionChain.IsEmpty ? null : namedDefinitionChain[^1];
    /// <summary>Gets the zero-based VAR or MVAR index only for a variable node.</summary>
    public int? VariableIndex { get; }
    /// <summary>Gets the referenced TypeSpec token only for a typed non-admitted indirection.</summary>
    public int? IndirectTypeSpecificationToken { get; }
    /// <summary>Gets the exact array rank only for an array node.</summary>
    public int? ArrayRank { get; }
    /// <summary>Gets a defensive copy of ordered ARRAY sizes; omitted trailing dimensions remain omitted.</summary>
    public ImmutableArray<int> ArraySizes => ExpressionV2ContractEncoding.Copy(arraySizes);
    /// <summary>Gets a defensive copy of ordered signed ARRAY lower bounds; omitted trailing dimensions remain omitted.</summary>
    public ImmutableArray<int> ArrayLowerBounds => ExpressionV2ContractEncoding.Copy(arrayLowerBounds);
    /// <summary>Gets a defensive copy of exact bytes for one typed non-admitted opaque signature fragment.</summary>
    public ImmutableArray<byte> OpaqueSignatureBytes => ExpressionV2ContractEncoding.Copy(opaqueSignatureBytes);
    /// <summary>Gets a defensive copy of ordered child nodes.</summary>
    public ImmutableArray<MetadataTypeSignatureNode> Children => ExpressionV2ContractEncoding.Copy(children);
    /// <summary>Gets the exact complete-tree depth.</summary>
    public int TopologyDepth { get; }
    /// <summary>Gets the exact cumulative complete-tree node count.</summary>
    public int TopologyNodeCount { get; }
    /// <summary>Gets the exact cumulative generic-argument count.</summary>
    public int CumulativeGenericArgumentCount { get; }
    /// <summary>Gets the exact function-pointer method-signature header only for FNPTR.</summary>
    public int? FunctionPointerHeader { get; }
    /// <summary>Gets the exact declared function-pointer generic-parameter count only for FNPTR.</summary>
    public int? FunctionPointerGenericParameterCount { get; }
    /// <summary>Gets the required-parameter count before the optional sentinel only for FNPTR.</summary>
    public int? FunctionPointerRequiredParameterCount { get; }
    /// <summary>Gets the total function-pointer parameter count, excluding the return node.</summary>
    public int? FunctionPointerParameterCount => Kind == MetadataTypeSignatureNodeKind.FunctionPointer
        ? children.Length - 1
        : null;
    /// <summary>Gets whether this subtree contains at least one owner VAR.</summary>
    public bool ContainsOwnerTypeParameter => ContainsKind(MetadataTypeSignatureNodeKind.OwnerTypeParameter);
    /// <summary>Gets whether this subtree contains at least one method MVAR.</summary>
    public bool ContainsMethodTypeParameter => ContainsKind(MetadataTypeSignatureNodeKind.MethodTypeParameter);
    /// <summary>Gets whether this subtree contains a managed by-reference form.</summary>
    public bool ContainsByReference => ContainsKind(MetadataTypeSignatureNodeKind.ByReference);
    /// <summary>Gets whether this subtree contains a physical VOID element.</summary>
    public bool ContainsVoid => ContainsKind(MetadataTypeSignatureNodeKind.Void);
    /// <summary>Gets whether this subtree contains a physical TYPEDBYREF element.</summary>
    public bool ContainsTypedByReference => ContainsKind(MetadataTypeSignatureNodeKind.TypedByReference);
    /// <summary>Gets whether this subtree contains a TypeSpec token in a forbidden named-head position.</summary>
    public bool ContainsInvalidTypeSpecificationHead =>
        ContainsKind(MetadataTypeSignatureNodeKind.TypeSpecificationIndirection);
    /// <summary>Gets whether this subtree contains an uninstantiated generic named definition.</summary>
    public bool ContainsOpenDefinition =>
        Kind == MetadataTypeSignatureNodeKind.Named && NamedDefinition!.FlattenedArity > 0 ||
        children.Any(static child => child.ContainsOpenDefinition);
    /// <summary>Gets whether a named subtree lacks the pinned compiler argument-slicing relation.</summary>
    public bool ContainsNonExactGenericMapping =>
        NamedDefinition is not null &&
            NamedDefinition.GenericMappingStatus == MetadataNestedGenericMappingStatus.NonExact ||
        children.Any(static child => child.ContainsNonExactGenericMapping);
    /// <summary>Gets whether any retained named-definition head has only provisional semantic classification.</summary>
    public bool ContainsProvisionalTypeDefinitionClassification =>
        namedDefinitionChain.Any(static definition =>
            definition.ClassificationStatus == MetadataTypeDefinitionClassificationStatus.Provisional) ||
        children.Any(static child => child.ContainsProvisionalTypeDefinitionClassification);
    /// <summary>Gets whether this subtree contains topology outside the admitted closed draft grammar.</summary>
    public bool ContainsUnsupportedClosedShape =>
        Kind is MetadataTypeSignatureNodeKind.RequiredModifier or
            MetadataTypeSignatureNodeKind.OptionalModifier or
            MetadataTypeSignatureNodeKind.Pointer or
            MetadataTypeSignatureNodeKind.ByReference or
            MetadataTypeSignatureNodeKind.FunctionPointer or
            MetadataTypeSignatureNodeKind.TypeSpecificationIndirection or
            MetadataTypeSignatureNodeKind.Void or
            MetadataTypeSignatureNodeKind.TypedByReference ||
        children.Any(static child => child.ContainsUnsupportedClosedShape);
    /// <summary>Gets whether the complete draft signature is variable-free and contains no open generic definition.</summary>
    public bool IsFullyGround =>
        !ContainsOwnerTypeParameter && !ContainsMethodTypeParameter && !ContainsOpenDefinition;
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one primitive draft signature node.</summary>
    /// <param name="kind">The exact primitive kind.</param>
    /// <returns>A sealed immutable draft node.</returns>
    internal static MetadataTypeSignatureNode Primitive(MetadataPrimitiveTypeKind kind)
    {
        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        return CreateCore(
            MetadataTypeSignatureNodeKind.Primitive,
            kind,
            null,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            null,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray<MetadataTypeSignatureNode>.Empty,
            0);
    }

    /// <summary>Creates one resolved direct-TypeDef-or-TypeRef named draft node.</summary>
    /// <param name="headKind">The exact raw CLASS or VALUETYPE element code.</param>
    /// <param name="target">The exact direct target; TypeSpec is rejected to avoid recursive self-containment.</param>
    /// <param name="definitionChain">Every exact resolved TypeDef from outermost through the target definition.</param>
    /// <returns>A sealed immutable draft node that remains open when the final definition has nonzero arity.</returns>
    internal static MetadataTypeSignatureNode Named(
        MetadataNamedSignatureHeadKind headKind,
        MetadataTypeDefOrRefTargetIdentity target,
        ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain)
    {
        ExpressionV2ContractEncoding.RequireDefined(headKind, nameof(headKind));
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind == MetadataTypeDefOrRefTargetKind.TypeSpecification)
        {
            throw new ArgumentException("A named signature leaf cannot recursively embed a TypeSpec identity.", nameof(target));
        }
        var copiedChain = ValidateDefinitionChain(target, definitionChain);
        var finalDefinition = copiedChain[^1];
        if (finalDefinition.ClassificationStatus == MetadataTypeDefinitionClassificationStatus.Exact)
        {
            var expectedHead = finalDefinition.Kind is MetadataTypeDefinitionKind.ValueType or MetadataTypeDefinitionKind.Enum
                ? MetadataNamedSignatureHeadKind.ValueType
                : MetadataNamedSignatureHeadKind.Class;
            if (headKind != expectedHead)
            {
                throw new ArgumentException("The raw CLASS/VALUETYPE head contradicts exact TypeDef classification.", nameof(headKind));
            }
        }
        return CreateCore(
            MetadataTypeSignatureNodeKind.Named,
            null,
            headKind,
            target,
            copiedChain,
            null,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray<MetadataTypeSignatureNode>.Empty,
            0);
    }

    /// <summary>Creates one owner-VAR draft signature node.</summary>
    /// <param name="index">The zero-based owner generic-parameter index.</param>
    /// <returns>A sealed immutable draft node.</returns>
    internal static MetadataTypeSignatureNode OwnerTypeParameter(int index) =>
        Variable(MetadataTypeSignatureNodeKind.OwnerTypeParameter, index);

    /// <summary>Creates one method-MVAR draft signature node.</summary>
    /// <param name="index">The zero-based method generic-parameter index.</param>
    /// <returns>A sealed immutable draft node whose later use remains context-checked.</returns>
    internal static MetadataTypeSignatureNode MethodTypeParameter(int index) =>
        Variable(MetadataTypeSignatureNodeKind.MethodTypeParameter, index);

    /// <summary>Creates one physical VOID draft signature node for role-aware use and <c>void*</c>.</summary>
    /// <returns>A sealed immutable draft node retained outside closed evaluation.</returns>
    internal static MetadataTypeSignatureNode Void() =>
        CreateLeaf(MetadataTypeSignatureNodeKind.Void);

    /// <summary>Creates one physical TYPEDBYREF draft signature node for role-aware use.</summary>
    /// <returns>A sealed immutable draft node retained outside closed evaluation.</returns>
    internal static MetadataTypeSignatureNode TypedByReference() =>
        CreateLeaf(MetadataTypeSignatureNodeKind.TypedByReference);

    /// <summary>Creates one generic-instantiation draft node with exact flattened arguments.</summary>
    /// <param name="namedHead">A resolved named leaf whose final TypeDef owns the flattened arity.</param>
    /// <param name="arguments">The exact ordered pre-substitution arguments.</param>
    /// <returns>A sealed immutable draft node retaining the complete nested definition chain.</returns>
    internal static MetadataTypeSignatureNode GenericInstantiation(
        MetadataTypeSignatureNode namedHead,
        ImmutableArray<MetadataTypeSignatureNode> arguments)
    {
        ArgumentNullException.ThrowIfNull(namedHead);
        if (namedHead.Kind != MetadataTypeSignatureNodeKind.Named || namedHead.NamedDefinition is null)
        {
            throw new ArgumentException("A generic-instantiation head must be one resolved named leaf.", nameof(namedHead));
        }
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            arguments,
            nameof(arguments),
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount);
        if (copied.IsEmpty || copied.Length != namedHead.NamedDefinition.FlattenedArity)
        {
            throw new ArgumentException(
                "The generic-instantiation argument count must equal exact flattened TypeDef arity.",
                nameof(arguments));
        }
        return CreateCore(
            MetadataTypeSignatureNodeKind.GenericInstantiation,
            null,
            namedHead.NamedHeadKind,
            namedHead.NamedTarget,
            namedHead.namedDefinitionChain,
            null,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            copied,
            copied.Length);
    }

    /// <summary>Creates one SZARRAY draft signature node.</summary>
    /// <param name="element">The complete pre-substitution element node.</param>
    /// <returns>A sealed immutable draft node distinct from every ARRAY node.</returns>
    internal static MetadataTypeSignatureNode SzArray(MetadataTypeSignatureNode element) =>
        Unary(MetadataTypeSignatureNodeKind.SzArray, element, 1,
            ImmutableArray<int>.Empty, ImmutableArray<int>.Empty);

    /// <summary>Creates one exact ARRAY draft signature node, including metadata-only rank one.</summary>
    /// <param name="element">The complete pre-substitution element node.</param>
    /// <param name="rank">The exact rank from one through thirty-two.</param>
    /// <param name="sizes">The ordered non-negative sizes for the encoded leading dimensions.</param>
    /// <param name="lowerBounds">The ordered signed lower bounds for the encoded leading dimensions.</param>
    /// <returns>A sealed immutable draft ARRAY node preserving rank, sizes, and lower bounds.</returns>
    internal static MetadataTypeSignatureNode MultidimensionalArray(
        MetadataTypeSignatureNode element,
        int rank,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds)
    {
        ValidateArrayShape(rank, sizes, lowerBounds, out var copiedSizes, out var copiedLowerBounds);
        return Unary(
            MetadataTypeSignatureNodeKind.MultidimensionalArray,
            element,
            rank,
            copiedSizes,
            copiedLowerBounds);
    }

    /// <summary>Creates one ordered required-modifier draft node.</summary>
    /// <param name="modifierType">The exact direct TypeDef-or-TypeRef modifier target.</param>
    /// <param name="unmodifiedType">The complete retained underlying type.</param>
    /// <returns>A sealed immutable draft node retained as typed non-admitted evidence.</returns>
    internal static MetadataTypeSignatureNode RequiredModifier(
        MetadataTypeDefOrRefTargetIdentity modifierType,
        MetadataTypeSignatureNode unmodifiedType) =>
        Modifier(MetadataTypeSignatureNodeKind.RequiredModifier, modifierType, unmodifiedType);

    /// <summary>Creates one ordered required-modifier draft node with a lightweight TypeSpec token edge.</summary>
    /// <param name="modifierType">The source-module/TypeSpec-token graph edge.</param>
    /// <param name="unmodifiedType">The complete retained underlying type.</param>
    /// <returns>A sealed immutable draft node whose TypeSpec decode belongs to a separate graph ledger.</returns>
    internal static MetadataTypeSignatureNode RequiredModifier(
        MetadataTypeSpecificationRowReferenceIdentity modifierType,
        MetadataTypeSignatureNode unmodifiedType) =>
        Modifier(MetadataTypeSignatureNodeKind.RequiredModifier, modifierType, unmodifiedType);

    /// <summary>Creates one ordered optional-modifier draft node.</summary>
    /// <param name="modifierType">The exact direct TypeDef-or-TypeRef modifier target.</param>
    /// <param name="unmodifiedType">The complete retained underlying type.</param>
    /// <returns>A sealed immutable draft node retained as typed non-admitted evidence.</returns>
    internal static MetadataTypeSignatureNode OptionalModifier(
        MetadataTypeDefOrRefTargetIdentity modifierType,
        MetadataTypeSignatureNode unmodifiedType) =>
        Modifier(MetadataTypeSignatureNodeKind.OptionalModifier, modifierType, unmodifiedType);

    /// <summary>Creates one ordered optional-modifier draft node with a lightweight TypeSpec token edge.</summary>
    /// <param name="modifierType">The source-module/TypeSpec-token graph edge.</param>
    /// <param name="unmodifiedType">The complete retained underlying type.</param>
    /// <returns>A sealed immutable draft node whose TypeSpec decode belongs to a separate graph ledger.</returns>
    internal static MetadataTypeSignatureNode OptionalModifier(
        MetadataTypeSpecificationRowReferenceIdentity modifierType,
        MetadataTypeSignatureNode unmodifiedType) =>
        Modifier(MetadataTypeSignatureNodeKind.OptionalModifier, modifierType, unmodifiedType);

    /// <summary>Creates one pointer draft node retained outside closed construction.</summary>
    /// <param name="element">The complete pointed-to type.</param>
    /// <returns>A sealed immutable draft typed non-admission.</returns>
    internal static MetadataTypeSignatureNode Pointer(MetadataTypeSignatureNode element) =>
        Unary(MetadataTypeSignatureNodeKind.Pointer, element, null,
            ImmutableArray<int>.Empty, ImmutableArray<int>.Empty);

    /// <summary>Creates one managed-by-reference draft node retained outside closed construction.</summary>
    /// <param name="element">The complete referenced type.</param>
    /// <returns>A sealed immutable draft typed non-admission.</returns>
    internal static MetadataTypeSignatureNode ByReference(MetadataTypeSignatureNode element) =>
        Unary(MetadataTypeSignatureNodeKind.ByReference, element, null,
            ImmutableArray<int>.Empty, ImmutableArray<int>.Empty);

    internal static MetadataTypeSignatureNode FunctionPointerDecoded(
        ImmutableArray<byte> exactEncodedSignature,
        int header,
        int genericParameterCount,
        int requiredParameterCount,
        ImmutableArray<MetadataTypeSignatureNode> returnAndParameterTypes)
    {
        if (exactEncodedSignature.IsDefaultOrEmpty || returnAndParameterTypes.IsDefaultOrEmpty ||
            requiredParameterCount < 0 || requiredParameterCount > returnAndParameterTypes.Length - 1)
        {
            throw new ArgumentException("A complete decoder-produced function-pointer shape is required.");
        }
        return CreateCore(
            MetadataTypeSignatureNodeKind.FunctionPointer,
            null,
            null,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            null,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ExpressionV2ContractEncoding.Copy(exactEncodedSignature),
            returnAndParameterTypes,
            0,
            null,
            header,
            genericParameterCount,
            requiredParameterCount);
    }

    /// <summary>Creates one non-recursive TypeSpec indirection draft node retained outside closed construction.</summary>
    /// <param name="typeSpecificationToken">The exact non-nil referenced TypeSpec token.</param>
    /// <returns>A sealed immutable draft typed non-admission; the containing TypeSpec rejects a direct self-cycle.</returns>
    internal static MetadataTypeSignatureNode TypeSpecificationIndirection(int typeSpecificationToken)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(
            typeSpecificationToken,
            0x1B,
            nameof(typeSpecificationToken));
        return CreateCore(
            MetadataTypeSignatureNodeKind.TypeSpecificationIndirection,
            null,
            null,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            null,
            typeSpecificationToken,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray<MetadataTypeSignatureNode>.Empty,
            0);
    }

    /// <summary>Tests canonical equality between two draft signature nodes.</summary>
    /// <param name="other">The other draft node.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSignatureNode? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSignatureNode);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal IEnumerable<MetadataTypeDefOrRefTargetIdentity> EnumerateNamedTargets()
    {
        if (NamedTarget is not null)
        {
            yield return NamedTarget;
        }
        foreach (var child in children)
        {
            foreach (var target in child.EnumerateNamedTargets())
            {
                yield return target;
            }
        }
    }

    internal IEnumerable<MetadataTypeSpecificationRowReferenceIdentity> EnumerateTypeSpecificationReferences()
    {
        if (ModifierTypeSpecificationReference is not null)
        {
            yield return ModifierTypeSpecificationReference;
        }
        foreach (var child in children)
        {
            foreach (var reference in child.EnumerateTypeSpecificationReferences())
            {
                yield return reference;
            }
        }
    }

    internal bool ContainsTypeSpecificationIndirection(int token) =>
        Kind == MetadataTypeSignatureNodeKind.TypeSpecificationIndirection &&
            IndirectTypeSpecificationToken == token ||
        children.Any(child => child.ContainsTypeSpecificationIndirection(token));

    private bool ContainsKind(MetadataTypeSignatureNodeKind kind) =>
        Kind == kind || children.Any(child => child.ContainsKind(kind));

    private static MetadataTypeSignatureNode CreateLeaf(MetadataTypeSignatureNodeKind kind) =>
        CreateCore(
            kind,
            null,
            null,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            null,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray<MetadataTypeSignatureNode>.Empty,
            0);

    private static MetadataTypeSignatureNode Variable(MetadataTypeSignatureNodeKind kind, int index)
    {
        if (index < 0 || index > 0x1FFF_FFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return CreateCore(
            kind,
            null,
            null,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            index,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray<MetadataTypeSignatureNode>.Empty,
            0);
    }

    private static MetadataTypeSignatureNode Unary(
        MetadataTypeSignatureNodeKind kind,
        MetadataTypeSignatureNode element,
        int? arrayRank,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds)
    {
        ArgumentNullException.ThrowIfNull(element);
        return CreateCore(
            kind,
            null,
            null,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            null,
            null,
            arrayRank,
            sizes,
            lowerBounds,
            ImmutableArray<byte>.Empty,
            ImmutableArray.Create(element),
            0);
    }

    private static MetadataTypeSignatureNode Modifier(
        MetadataTypeSignatureNodeKind kind,
        MetadataTypeDefOrRefTargetIdentity modifierType,
        MetadataTypeSignatureNode unmodifiedType)
    {
        ArgumentNullException.ThrowIfNull(modifierType);
        ArgumentNullException.ThrowIfNull(unmodifiedType);
        if (modifierType.Kind == MetadataTypeDefOrRefTargetKind.TypeSpecification)
        {
            throw new ArgumentException("A modifier TypeSpec must be retained as a lightweight graph reference.", nameof(modifierType));
        }
        return CreateCore(
            kind,
            null,
            null,
            modifierType,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            null,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray.Create(unmodifiedType),
            0);
    }

    private static MetadataTypeSignatureNode Modifier(
        MetadataTypeSignatureNodeKind kind,
        MetadataTypeSpecificationRowReferenceIdentity modifierType,
        MetadataTypeSignatureNode unmodifiedType)
    {
        ArgumentNullException.ThrowIfNull(modifierType);
        ArgumentNullException.ThrowIfNull(unmodifiedType);
        return CreateCore(
            kind,
            null,
            null,
            null,
            ImmutableArray<MetadataTypeDefinitionIdentity>.Empty,
            null,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray.Create(unmodifiedType),
            0,
            modifierType);
    }

    private static MetadataTypeSignatureNode CreateCore(
        MetadataTypeSignatureNodeKind kind,
        MetadataPrimitiveTypeKind? primitiveKind,
        MetadataNamedSignatureHeadKind? namedHeadKind,
        MetadataTypeDefOrRefTargetIdentity? namedTarget,
        ImmutableArray<MetadataTypeDefinitionIdentity> namedDefinitionChain,
        int? variableIndex,
        int? indirectTypeSpecificationToken,
        int? arrayRank,
        ImmutableArray<int> arraySizes,
        ImmutableArray<int> arrayLowerBounds,
        ImmutableArray<byte> opaqueSignatureBytes,
        ImmutableArray<MetadataTypeSignatureNode> children,
        int genericArgumentContribution,
        MetadataTypeSpecificationRowReferenceIdentity? modifierTypeSpecificationReference = null,
        int? functionPointerHeader = null,
        int? functionPointerGenericParameterCount = null,
        int? functionPointerRequiredParameterCount = null)
    {
        var depth = 1;
        var nodes = 1;
        var arguments = genericArgumentContribution;
        foreach (var child in children)
        {
            depth = Math.Max(depth, checked(child.TopologyDepth + 1));
            nodes = checked(nodes + child.TopologyNodeCount);
            arguments = checked(arguments + child.CumulativeGenericArgumentCount);
        }
        if (depth > StaticFieldV2Limits.MaximumTypeSpecificationDepth ||
            nodes > StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount ||
            arguments > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount)
        {
            throw new ArgumentException("The signature tree exceeds a declared W8 operation bound.");
        }
        return new MetadataTypeSignatureNode(
            kind,
            primitiveKind,
            namedHeadKind,
            namedTarget,
            modifierTypeSpecificationReference,
            namedDefinitionChain,
            variableIndex,
            indirectTypeSpecificationToken,
            arrayRank,
            arraySizes,
            arrayLowerBounds,
            opaqueSignatureBytes,
            children,
            depth,
            nodes,
            arguments,
            functionPointerHeader,
            functionPointerGenericParameterCount,
            functionPointerRequiredParameterCount);
    }

    private static ImmutableArray<MetadataTypeDefinitionIdentity> ValidateDefinitionChain(
        MetadataTypeDefOrRefTargetIdentity target,
        ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain)
    {
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            definitionChain,
            nameof(definitionChain),
            StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1);
        if (copied.IsEmpty || target.ResolvedDefinition is null ||
            !target.ResolvedDefinition.Equals(copied[^1]))
        {
            throw new ArgumentException("The definition chain must end at the exact named target.", nameof(definitionChain));
        }
        var cumulativeArity = 0;
        for (var index = 0; index < copied.Length; index++)
        {
            var definition = copied[index];
            if (definition.SegmentIndex != index)
            {
                throw new ArgumentException("Named definition segments must be complete and outer-to-inner.", nameof(definitionChain));
            }
            if (index == 0)
            {
                if (definition.PhysicalDefinition.EnclosingType is not null)
                {
                    throw new ArgumentException("The first named definition segment must be top-level.", nameof(definitionChain));
                }
            }
            else if (definition.PhysicalDefinition.EnclosingType is null ||
                     !definition.PhysicalDefinition.EnclosingType.Equals(copied[index - 1].PhysicalDefinition))
            {
                throw new ArgumentException("Each nested definition must name the preceding exact enclosing TypeDef.", nameof(definitionChain));
            }
            if (definition.GenericMappingStatus == MetadataNestedGenericMappingStatus.ExactPinnedCompiler)
            {
                cumulativeArity = checked(cumulativeArity + definition.IntroducedArity!.Value);
                if (definition.FlattenedArity != cumulativeArity)
                {
                    throw new ArgumentException("Pinned named segments must have cumulative flattened arity.", nameof(definitionChain));
                }
            }
        }
        return copied;
    }

    private static void ValidateArrayShape(
        int rank,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds,
        out ImmutableArray<int> copiedSizes,
        out ImmutableArray<int> copiedLowerBounds)
    {
        if (rank < 1 || rank > StaticFieldV2Limits.MaximumArrayRank)
        {
            throw new ArgumentOutOfRangeException(nameof(rank));
        }
        copiedSizes = ExpressionV2ContractEncoding.CopyRequiredValues(sizes, nameof(sizes), rank);
        copiedLowerBounds = ExpressionV2ContractEncoding.CopyRequiredValues(lowerBounds, nameof(lowerBounds), rank);
        if (copiedSizes.Any(static size => size < 0 || size > 0x1FFF_FFFF))
        {
            throw new ArgumentException("Every ARRAY size must fit the compressed unsigned encoding.", nameof(sizes));
        }
        if (copiedLowerBounds.Any(static value => value < -0x1000_0000 || value > 0x0FFF_FFFF))
        {
            throw new ArgumentException("Every ARRAY lower bound must fit the compressed signed encoding.", nameof(lowerBounds));
        }
    }

    private static void WriteInt32Array(CanonicalReplayEncoding.Writer writer, ImmutableArray<int> values)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteInt32(value);
        }
    }
}

/// <summary>Deterministically decodes complete ECMA metadata type signatures from detached bytes and token facts.</summary>
/// <remarks>
/// This draft decoder is the only certificate issuer. It consumes the complete source, resolves every token through an
/// immutable catalog, retains structured FNPTR content, and returns typed invalid or cap-plus-one outcomes without a
/// prefix tree.
/// </remarks>
public static class MetadataTypeSignatureDecoder
{
    /// <summary>Decodes one complete physical TypeSpec row under the bounded draft grammar.</summary>
    /// <param name="reference">The exact source module and TypeSpec token.</param>
    /// <param name="signatureBytes">The complete raw TypeSpec blob bytes.</param>
    /// <param name="tokenCatalog">The detached bounded token-resolution catalog for the same exact source module.</param>
    /// <returns>An exact, incomplete-resolution, malformed, or cap-plus-one draft outcome with no partial tree.</returns>
    public static MetadataSignatureDecodeOutcome DecodeTypeSpecification(
        MetadataTypeSpecificationRowReferenceIdentity reference,
        ImmutableArray<byte> signatureBytes,
        MetadataSignatureTokenResolutionCatalog tokenCatalog)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(tokenCatalog);
        if (!tokenCatalog.SourceModule.Equals(reference.MetadataModule))
        {
            throw new ArgumentException("The token catalog must belong to the exact TypeSpec source module.", nameof(tokenCatalog));
        }
        if (signatureBytes.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized complete blob is required.", nameof(signatureBytes));
        }
        var projection = MetadataSignatureProjectionAdapter.Decode(
            signatureBytes,
            BoundedEcmaSignatureForm.TypeSpecification,
            tokenCatalog);
        if (projection.ReachedBound is not null)
        {
            return MetadataSignatureDecodeOutcome.BoundReached(
                reference,
                projection.ReachedBound,
                projection.ObservedCount);
        }

        // Bound stops intentionally retain no source prefix. Every other Core result consumed an admitted complete
        // blob, so its physical row remains available even when grammar or token evidence is not exact.
        var row = MetadataTypeSpecificationRowIdentity.Create(reference, signatureBytes);
        if (!tokenCatalog.ContainsSourceToken(reference.TypeSpecificationToken))
        {
            return MetadataSignatureDecodeOutcome.Invalid(row, "W8_SIGNATURE_SOURCE_TOKEN_OUT_OF_RANGE");
        }
        if (projection.Kind == MetadataSignatureDecodeResultKind.NonExact)
        {
            return MetadataSignatureDecodeOutcome.Incomplete(row, projection.Code!);
        }
        if (projection.Kind == MetadataSignatureDecodeResultKind.Invalid)
        {
            return MetadataSignatureDecodeOutcome.Invalid(row, projection.Code!);
        }

        var root = projection.Root!;
        var certificate = MetadataSignatureDecodeCertificate.CreateFromDecoder(signatureBytes, root);
        return MetadataSignatureDecodeOutcome.Exact(
            row,
            root,
            certificate,
            cumulativeTypeSpecificationRowCount: 1,
            cumulativeByteCount: signatureBytes.Length,
            cumulativeRawNodeCount: root.TopologyNodeCount,
            cumulativeGenericArgumentCount: root.CumulativeGenericArgumentCount);
    }

}

/// <summary>Composes one exact physical TypeSpec row, real decode outcome, and role-neutral construction classification.</summary>
/// <remarks>The sealed draft composite keeps the physical row's canonical identity separate from derived analysis layers.</remarks>
public sealed class MetadataTypeSpecificationIdentity : IEquatable<MetadataTypeSpecificationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-type-specification-identity";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationIdentity(
        MetadataSignatureDecodeOutcome decode,
        MetadataTypeConstructionResult construction)
    {
        Decode = decode;
        Construction = construction;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(decode.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(construction.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact real full-consumption decode outcome.</summary>
    public MetadataSignatureDecodeOutcome Decode { get; }
    /// <summary>Gets the separately canonical physical TypeSpec row.</summary>
    public MetadataTypeSpecificationRowIdentity Row => Decode.Row!;
    /// <summary>Gets the exact source metadata module containing the TypeSpec row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule => Decode.Reference.MetadataModule;
    /// <summary>Gets the exact non-nil TypeSpec token.</summary>
    public int TypeSpecificationToken => Decode.Reference.TypeSpecificationToken;
    /// <summary>Gets a defensive copy of the complete original TypeSpec blob.</summary>
    public ImmutableArray<byte> OriginalBytes => Row.SignatureBytes;
    /// <summary>Gets the complete bounded projected signature tree.</summary>
    public MetadataTypeSignatureNode Root => Decode.Root!;
    /// <summary>Gets the typed draft construction result for the complete original signature.</summary>
    public MetadataTypeConstructionResult Construction { get; }
    /// <summary>Gets whether this TypeSpec projects to one exact closed construction.</summary>
    public bool IsFullyGround => Construction.Kind == MetadataTypeConstructionResultKind.Exact;
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one TypeSpec draft composite only from a real full-consumption exact decode.</summary>
    /// <param name="decode">The exact physical-row-addressed decoder outcome.</param>
    /// <returns>A sealed immutable draft composite with separate row, decode, and construction layers.</returns>
    public static MetadataTypeSpecificationIdentity FromDecodeOutcome(MetadataSignatureDecodeOutcome decode)
    {
        ArgumentNullException.ThrowIfNull(decode);
        if (decode.Kind != MetadataSignatureDecodeResultKind.Exact ||
            decode.Row is null || decode.Root is null || decode.Certificate is null)
        {
            throw new ArgumentException("A TypeSpec composite requires one complete exact decoder outcome.", nameof(decode));
        }
        var construction = MetadataTypeConstructionResult.Classify(decode.Root);
        return new MetadataTypeSpecificationIdentity(decode, construction);
    }

    /// <summary>Tests canonical equality between two row-addressed draft TypeSpec identities.</summary>
    /// <param name="other">The other draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSpecificationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

}

/// <summary>Freezes the detached input catalog supplied to one or more TypeSpec graph traversals.</summary>
/// <remarks>
/// This sealed draft identity normalizes caller order by physical TypeSpec token, retains duplicate row keys for typed
/// invalid classification, and deliberately permits equal signature blobs at distinct physical rows.
/// </remarks>
public sealed class MetadataTypeSpecificationGraphCatalogIdentity :
    IEquatable<MetadataTypeSpecificationGraphCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typespec-graph-catalog-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataSignatureDecodeOutcome> outcomes;
    private readonly ImmutableArray<int> duplicateTypeSpecificationTokens;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationGraphCatalogIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<MetadataSignatureDecodeOutcome> outcomes,
        ImmutableArray<int> duplicateTypeSpecificationTokens)
    {
        SourceModule = sourceModule;
        this.outcomes = outcomes;
        this.duplicateTypeSpecificationTokens = duplicateTypeSpecificationTokens;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, outcomes, static value => value.CanonicalBytes);
        writer.WriteInt32(duplicateTypeSpecificationTokens.Length);
        foreach (var token in duplicateTypeSpecificationTokens)
        {
            writer.WriteInt32(token);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source module whose TypeSpec table owns every catalog row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }
    /// <summary>Gets a defensive copy of all supplied outcomes in normalized physical-token order.</summary>
    public ImmutableArray<MetadataSignatureDecodeOutcome> Outcomes =>
        ExpressionV2ContractEncoding.Copy(outcomes);
    /// <summary>Gets a sorted defensive copy of physical TypeSpec tokens supplied more than once.</summary>
    public ImmutableArray<int> DuplicateTypeSpecificationTokens =>
        ExpressionV2ContractEncoding.Copy(duplicateTypeSpecificationTokens);
    /// <summary>Gets a defensive copy of the complete versioned draft canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one normalized detached TypeSpec graph input catalog.</summary>
    /// <param name="sourceModule">The exact module containing every referenced TypeSpec row.</param>
    /// <param name="outcomes">Initialized decoder outcomes, capped at one observable row beyond traversal limit.</param>
    /// <returns>An immutable draft input identity that retains duplicate row-key evidence.</returns>
    public static MetadataTypeSpecificationGraphCatalogIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<MetadataSignatureDecodeOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            outcomes,
            nameof(outcomes),
            StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount + 1);
        if (copied.Any(outcome => outcome is null ||
            !outcome.Reference.MetadataModule.Equals(sourceModule)))
        {
            throw new ArgumentException(
                "Every TypeSpec graph input must be a non-null row from the exact source module.",
                nameof(outcomes));
        }

        var normalized = copied
            .OrderBy(static outcome => outcome.Reference.TypeSpecificationToken)
            .ThenBy(static outcome => outcome.Sha256, StringComparer.Ordinal)
            .ToImmutableArray();
        var duplicates = normalized
            .GroupBy(static outcome => outcome.Reference.TypeSpecificationToken)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToImmutableArray();
        return new MetadataTypeSpecificationGraphCatalogIdentity(sourceModule, normalized, duplicates);
    }

    /// <summary>Tests canonical equality between two normalized draft input catalogs.</summary>
    /// <param name="other">The other input catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSpecificationGraphCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationGraphCatalogIdentity);
    /// <summary>Computes a hash code from immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes complete traversal of a row-keyed TypeSpec reference/decode graph.</summary>
/// <remarks>
/// The sealed draft graph keeps normalized input evidence separate from usable exact outcomes. A missing row,
/// decoder stop, or cumulative cap produces no usable prefix and zero committed counters; the first observed cap is
/// saturated at exactly limit plus one. Equal blobs at distinct physical TypeSpec rows remain independent valid facts.
/// </remarks>
public sealed class MetadataTypeSpecificationGraphIdentity : IEquatable<MetadataTypeSpecificationGraphIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typespec-graph-identity";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<MetadataSignatureDecodeOutcome> decodeOutcomes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSpecificationGraphIdentity(
        MetadataTypeSpecificationRowReferenceIdentity rootReference,
        MetadataTypeSpecificationGraphCatalogIdentity inputCatalog,
        ImmutableArray<MetadataSignatureDecodeOutcome> decodeOutcomes,
        MetadataTypeSpecificationGraphResultKind resultKind,
        EvaluationDeterministicBound? reachedBound,
        int reachedBoundObservedCount,
        int visitedRowCount,
        int cumulativeByteCount,
        int cumulativeRawNodeCount,
        int cumulativeGenericArgumentCount)
    {
        RootReference = rootReference;
        InputCatalog = inputCatalog;
        this.decodeOutcomes = decodeOutcomes;
        ResultKind = resultKind;
        ReachedBound = reachedBound;
        ReachedBoundObservedCount = reachedBoundObservedCount;
        VisitedRowCount = visitedRowCount;
        CumulativeByteCount = cumulativeByteCount;
        CumulativeRawNodeCount = cumulativeRawNodeCount;
        CumulativeGenericArgumentCount = cumulativeGenericArgumentCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(rootReference.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(inputCatalog.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, decodeOutcomes, static value => value.CanonicalBytes);
        writer.WriteInt32((int)resultKind);
        writer.WriteBoolean(reachedBound is not null);
        if (reachedBound is not null)
        {
            writer.WriteString(reachedBound.Name);
            writer.WriteInt64(reachedBound.Value);
        }
        writer.WriteInt32(reachedBoundObservedCount);
        writer.WriteInt32(visitedRowCount);
        writer.WriteInt32(cumulativeByteCount);
        writer.WriteInt32(cumulativeRawNodeCount);
        writer.WriteInt32(cumulativeGenericArgumentCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact root row reference.</summary>
    public MetadataTypeSpecificationRowReferenceIdentity RootReference { get; }
    /// <summary>Gets the complete normalized input catalog, including evidence unusable by a non-exact result.</summary>
    public MetadataTypeSpecificationGraphCatalogIdentity InputCatalog { get; }
    /// <summary>Gets reachable decoder outcomes only when the complete traversal is exact; otherwise an empty copy.</summary>
    public ImmutableArray<MetadataSignatureDecodeOutcome> DecodeOutcomes =>
        ExpressionV2ContractEncoding.Copy(decodeOutcomes);
    /// <summary>Gets the complete graph result kind.</summary>
    public MetadataTypeSpecificationGraphResultKind ResultKind { get; }
    /// <summary>Gets the first deterministic cumulative bound reached, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets exactly reached-bound limit plus one, or zero when no cumulative cap was reached.</summary>
    public int ReachedBoundObservedCount { get; }
    /// <summary>Gets the unique reachable row count only for an exact result; non-exact and invalid results expose zero.</summary>
    public int VisitedRowCount { get; }
    /// <summary>Gets cumulative complete-source bytes only for an exact result; otherwise zero.</summary>
    public int CumulativeByteCount { get; }
    /// <summary>Gets cumulative raw signature nodes only for an exact result; otherwise zero.</summary>
    public int CumulativeRawNodeCount { get; }
    /// <summary>Gets cumulative generic arguments only for an exact result; otherwise zero.</summary>
    public int CumulativeGenericArgumentCount { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates and completely traverses one detached row-keyed TypeSpec draft graph.</summary>
    /// <param name="rootReference">The exact root TypeSpec row reference.</param>
    /// <param name="decodeOutcomes">Every available decoder outcome in arbitrary caller order.</param>
    /// <returns>A sealed immutable graph with exact, prefix-free non-exact, or invalid status.</returns>
    public static MetadataTypeSpecificationGraphIdentity Create(
        MetadataTypeSpecificationRowReferenceIdentity rootReference,
        ImmutableArray<MetadataSignatureDecodeOutcome> decodeOutcomes)
    {
        ArgumentNullException.ThrowIfNull(rootReference);
        return Create(
            rootReference,
            MetadataTypeSpecificationGraphCatalogIdentity.Create(rootReference.MetadataModule, decodeOutcomes));
    }

    /// <summary>Traverses one exact root through a pre-normalized detached TypeSpec input catalog.</summary>
    /// <param name="rootReference">The exact root TypeSpec row reference.</param>
    /// <param name="inputCatalog">The immutable complete input catalog for the root module.</param>
    /// <returns>A sealed immutable draft graph that never exposes a usable prefix after a non-exact observation.</returns>
    public static MetadataTypeSpecificationGraphIdentity Create(
        MetadataTypeSpecificationRowReferenceIdentity rootReference,
        MetadataTypeSpecificationGraphCatalogIdentity inputCatalog)
    {
        ArgumentNullException.ThrowIfNull(rootReference);
        ArgumentNullException.ThrowIfNull(inputCatalog);
        if (!rootReference.MetadataModule.Equals(inputCatalog.SourceModule))
        {
            throw new ArgumentException("The graph root and input catalog must identify the same source module.", nameof(inputCatalog));
        }

        var byToken = new Dictionary<int, MetadataSignatureDecodeOutcome>();
        var invalid = !inputCatalog.DuplicateTypeSpecificationTokens.IsEmpty;
        foreach (var outcome in inputCatalog.Outcomes)
        {
            byToken.TryAdd(outcome.Reference.TypeSpecificationToken, outcome);
        }

        var state = new Dictionary<int, int>();
        var retained = ImmutableArray.CreateBuilder<MetadataSignatureDecodeOutcome>();
        var visitedRows = 0;
        var bytes = 0;
        var nodes = 0;
        var arguments = 0;
        var nonExact = false;
        var stopped = invalid;
        EvaluationDeterministicBound? reachedBound = null;
        var reachedBoundObservedCount = 0;

        Visit(rootReference);
        var resultKind = invalid
            ? MetadataTypeSpecificationGraphResultKind.Invalid
            : nonExact
                ? MetadataTypeSpecificationGraphResultKind.NonExact
                : MetadataTypeSpecificationGraphResultKind.Exact;
        var exact = resultKind == MetadataTypeSpecificationGraphResultKind.Exact;
        return new MetadataTypeSpecificationGraphIdentity(
            rootReference,
            inputCatalog,
            exact ? retained.ToImmutable() : ImmutableArray<MetadataSignatureDecodeOutcome>.Empty,
            resultKind,
            reachedBound,
            reachedBoundObservedCount,
            exact ? visitedRows : 0,
            exact ? bytes : 0,
            exact ? nodes : 0,
            exact ? arguments : 0);

        void Visit(MetadataTypeSpecificationRowReferenceIdentity reference)
        {
            if (stopped)
            {
                return;
            }
            if (!reference.MetadataModule.Equals(inputCatalog.SourceModule))
            {
                invalid = true;
                stopped = true;
                return;
            }

            var token = reference.TypeSpecificationToken;
            if (state.TryGetValue(token, out var currentState))
            {
                if (currentState == 1)
                {
                    invalid = true;
                    stopped = true;
                }
                return;
            }
            if (!byToken.TryGetValue(token, out var outcome))
            {
                nonExact = true;
                stopped = true;
                return;
            }

            state[token] = 1;
            if (outcome.Kind == MetadataSignatureDecodeResultKind.Invalid)
            {
                invalid = true;
                stopped = true;
                return;
            }
            if (outcome.Kind == MetadataSignatureDecodeResultKind.NonExact)
            {
                nonExact = true;
                stopped = true;
                reachedBound = outcome.ReachedBound;
                reachedBoundObservedCount = outcome.ObservedCount;
                return;
            }

            var candidateRows = checked(visitedRows + 1);
            var candidateBytes = checked(bytes + outcome.Row!.SignatureBytes.Length);
            var candidateNodes = checked(nodes + outcome.Root!.TopologyNodeCount);
            var candidateArguments = checked(arguments + outcome.Root.CumulativeGenericArgumentCount);
            if (TryStopAtBound(
                    candidateRows,
                    StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount,
                    ExpressionV2ContractLimits.TypeSpecificationRowTraversalCountBoundName) ||
                TryStopAtBound(
                    candidateBytes,
                    StaticFieldV2Limits.MaximumTypeSpecificationByteCount,
                    ExpressionV2ContractLimits.TypeSpecificationByteCountBoundName) ||
                TryStopAtBound(
                    candidateNodes,
                    StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
                    ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName) ||
                TryStopAtBound(
                    candidateArguments,
                    StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
                    ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName))
            {
                return;
            }

            visitedRows = candidateRows;
            bytes = candidateBytes;
            nodes = candidateNodes;
            arguments = candidateArguments;
            retained.Add(outcome);
            foreach (var child in outcome.Root.EnumerateTypeSpecificationReferences())
            {
                Visit(child);
                if (stopped)
                {
                    return;
                }
            }
            state[token] = 2;
        }

        bool TryStopAtBound(int observed, int limit, string name)
        {
            if (observed <= limit)
            {
                return false;
            }
            nonExact = true;
            stopped = true;
            reachedBound = new EvaluationDeterministicBound(name, limit);
            reachedBoundObservedCount = checked(limit + 1);
            return true;
        }
    }

    /// <summary>Tests canonical equality between two detached draft TypeSpec graphs.</summary>
    /// <param name="other">The other draft graph.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSpecificationGraphIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSpecificationGraphIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one role-specific use classification over separate TypeSpec decode and graph evidence.</summary>
/// <remarks>The sealed draft result never changes physical row or decoder identity when use-site rules evolve.</remarks>
public sealed class MetadataTypeUseResult : IEquatable<MetadataTypeUseResult>
{
    private const string CanonicalDomain = "metadata-v2-type-use-result";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeUseResult(
        MetadataTypeUseRole role,
        MetadataTypeUseResultKind kind,
        MetadataTypeSpecificationIdentity typeSpecification,
        MetadataTypeSpecificationGraphIdentity graph,
        MetadataGenericParameterIdentity? constraintOwner)
    {
        Role = role;
        Kind = kind;
        TypeSpecification = typeSpecification;
        Graph = graph;
        ConstraintOwner = constraintOwner;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)role);
        writer.WriteInt32((int)kind);
        writer.WriteLengthPrefixedBytes(typeSpecification.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(graph.CanonicalBytes.AsSpan());
        writer.WriteBoolean(constraintOwner is not null);
        if (constraintOwner is not null)
        {
            writer.WriteLengthPrefixedBytes(constraintOwner.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata use role.</summary>
    public MetadataTypeUseRole Role { get; }
    /// <summary>Gets the role-specific typed result kind.</summary>
    public MetadataTypeUseResultKind Kind { get; }
    /// <summary>Gets the separate exact TypeSpec decode/construction composite.</summary>
    public MetadataTypeSpecificationIdentity TypeSpecification { get; }
    /// <summary>Gets the separate complete TypeSpec graph traversal.</summary>
    public MetadataTypeSpecificationGraphIdentity Graph { get; }
    /// <summary>Gets the exact GenericParam owner only for a constraint use.</summary>
    public MetadataGenericParameterIdentity? ConstraintOwner { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Classifies one decoded TypeSpec under a specific metadata role.</summary>
    /// <param name="role">The exact use-site metadata role.</param>
    /// <param name="typeSpecification">The exact decoder-produced TypeSpec composite.</param>
    /// <param name="graph">The complete row-keyed modifier TypeSpec graph.</param>
    /// <param name="constraintOwner">The exact GenericParam owner only for a constraint role.</param>
    /// <returns>A sealed immutable role-specific draft result.</returns>
    public static MetadataTypeUseResult Classify(
        MetadataTypeUseRole role,
        MetadataTypeSpecificationIdentity typeSpecification,
        MetadataTypeSpecificationGraphIdentity graph,
        MetadataGenericParameterIdentity? constraintOwner = null)
    {
        ExpressionV2ContractEncoding.RequireDefined(role, nameof(role));
        ArgumentNullException.ThrowIfNull(typeSpecification);
        ArgumentNullException.ThrowIfNull(graph);
        if (!graph.RootReference.Equals(typeSpecification.Row.Reference))
        {
            throw new ArgumentException("The use graph root must equal the exact TypeSpec row.", nameof(graph));
        }
        var retainedRoot = graph.DecodeOutcomes.FirstOrDefault(outcome =>
            outcome.Reference.Equals(typeSpecification.Row.Reference));
        if (retainedRoot is not null && !retainedRoot.Equals(typeSpecification.Decode))
        {
            throw new ArgumentException("A retained graph-root decode must equal the exact TypeSpec composite decode.", nameof(graph));
        }
        if ((role == MetadataTypeUseRole.GenericParameterConstraint) != (constraintOwner is not null))
        {
            throw new ArgumentException("Only a generic-constraint role requires one exact GenericParam owner.", nameof(constraintOwner));
        }
        var root = typeSpecification.Root;
        MetadataTypeUseResultKind kind;
        if (graph.ResultKind == MetadataTypeSpecificationGraphResultKind.Invalid)
        {
            kind = MetadataTypeUseResultKind.Invalid;
        }
        else if (graph.ResultKind == MetadataTypeSpecificationGraphResultKind.NonExact)
        {
            kind = MetadataTypeUseResultKind.NonExact;
        }
        else if (((role is MetadataTypeUseRole.FieldDefinition or MetadataTypeUseRole.BaseType or
                    MetadataTypeUseRole.InterfaceImplementation) && root.ContainsMethodTypeParameter) ||
                 (role == MetadataTypeUseRole.GenericParameterConstraint &&
                    constraintOwner!.Owner.Kind == MetadataGenericParameterOwnerKind.TypeDefinition &&
                    root.ContainsMethodTypeParameter) ||
                 (role is MetadataTypeUseRole.FieldDefinition or MetadataTypeUseRole.BaseType or
                     MetadataTypeUseRole.InterfaceImplementation or MetadataTypeUseRole.GenericParameterConstraint &&
                    (root.ContainsByReference || root.ContainsTypedByReference || ContainsVoidOutsidePointer(root, false))))
        {
            kind = MetadataTypeUseResultKind.Invalid;
        }
        else
        {
            kind = typeSpecification.Construction.Kind switch
            {
                MetadataTypeConstructionResultKind.Exact => MetadataTypeUseResultKind.Exact,
                MetadataTypeConstructionResultKind.Open => MetadataTypeUseResultKind.Open,
                MetadataTypeConstructionResultKind.Unsupported => MetadataTypeUseResultKind.Unsupported,
                MetadataTypeConstructionResultKind.Invalid => MetadataTypeUseResultKind.Invalid,
                MetadataTypeConstructionResultKind.NonExact => MetadataTypeUseResultKind.NonExact,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
        return new MetadataTypeUseResult(role, kind, typeSpecification, graph, constraintOwner);
    }

    /// <summary>Tests canonical equality between two role-specific draft type-use results.</summary>
    /// <param name="other">The other draft use result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeUseResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeUseResult);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static bool ContainsVoidOutsidePointer(MetadataTypeSignatureNode node, bool pointerTarget)
    {
        if (node.Kind == MetadataTypeSignatureNodeKind.Void)
        {
            return !pointerTarget;
        }
        var childIsPointerTarget = node.Kind == MetadataTypeSignatureNodeKind.Pointer ||
            (pointerTarget && node.Kind is MetadataTypeSignatureNodeKind.RequiredModifier or
                MetadataTypeSignatureNodeKind.OptionalModifier);
        return node.Children.Any(child => ContainsVoidOutsidePointer(child, childIsPointerTarget));
    }
}

/// <summary>Freezes one exact TypeDef Extends column and its complete TypeDefOrRef target.</summary>
/// <remarks>The sealed draft edge retains the owner TypeDef row independently from any target TypeSpec row.</remarks>
public sealed class MetadataBaseTypeEdgeIdentity : IEquatable<MetadataBaseTypeEdgeIdentity>
{
    private const string CanonicalDomain = "metadata-v2-base-type-edge-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataBaseTypeEdgeIdentity(
        MetadataTypeDefinitionIdentity ownerType,
        MetadataTypeDefOrRefTargetIdentity target)
    {
        OwnerType = ownerType;
        Target = target;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(ownerType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(target.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact owner TypeDef whose Extends column stores the target token.</summary>
    public MetadataTypeDefinitionIdentity OwnerType { get; }
    /// <summary>Gets the exact decoded TypeDefOrRef target.</summary>
    public MetadataTypeDefOrRefTargetIdentity Target { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact TypeDef-Extends draft edge.</summary>
    /// <param name="ownerType">The exact owner TypeDef row and W8 classification.</param>
    /// <param name="target">The exact TypeDef, TypeRef resolution, or TypeSpec target.</param>
    /// <returns>A sealed immutable draft edge preserving owner and target rows.</returns>
    public static MetadataBaseTypeEdgeIdentity Create(
        MetadataTypeDefinitionIdentity ownerType,
        MetadataTypeDefOrRefTargetIdentity target)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(target);
        if (!ownerType.PhysicalDefinition.MetadataModule.Equals(target.SourceMetadataModule) ||
            ownerType.PhysicalDefinition.ExtendsMetadataToken != target.SourceMetadataToken)
        {
            throw new ArgumentException("The Extends owner module/token must exactly match the decoded target.", nameof(target));
        }
        return new MetadataBaseTypeEdgeIdentity(ownerType, target);
    }

    /// <summary>Tests canonical equality between two draft base edges.</summary>
    /// <param name="other">The other draft edge.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataBaseTypeEdgeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataBaseTypeEdgeIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact InterfaceImpl row and its complete TypeDefOrRef interface target.</summary>
/// <remarks>The sealed draft edge retains its InterfaceImpl row token instead of substituting a target TypeSpec token.</remarks>
public sealed class MetadataInterfaceImplementationEdgeIdentity : IEquatable<MetadataInterfaceImplementationEdgeIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interface-implementation-edge-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationEdgeIdentity(
        int interfaceImplementationToken,
        MetadataTypeDefinitionIdentity ownerType,
        MetadataTypeDefOrRefTargetIdentity target)
    {
        InterfaceImplementationToken = interfaceImplementationToken;
        OwnerType = ownerType;
        Target = target;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(interfaceImplementationToken);
        writer.WriteLengthPrefixedBytes(ownerType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(target.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact non-nil InterfaceImpl row token.</summary>
    public int InterfaceImplementationToken { get; }
    /// <summary>Gets the exact Class-column owner TypeDef.</summary>
    public MetadataTypeDefinitionIdentity OwnerType { get; }
    /// <summary>Gets the exact Interface-column TypeDefOrRef target.</summary>
    public MetadataTypeDefOrRefTargetIdentity Target { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact InterfaceImpl-row draft edge.</summary>
    /// <param name="interfaceImplementationToken">The exact non-nil InterfaceImpl token.</param>
    /// <param name="ownerType">The exact Class-column owner TypeDef.</param>
    /// <param name="target">The exact Interface-column TypeDefOrRef target.</param>
    /// <returns>A sealed immutable draft edge preserving owner and target row identities.</returns>
    public static MetadataInterfaceImplementationEdgeIdentity Create(
        int interfaceImplementationToken,
        MetadataTypeDefinitionIdentity ownerType,
        MetadataTypeDefOrRefTargetIdentity target)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(
            interfaceImplementationToken,
            0x09,
            nameof(interfaceImplementationToken));
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(target);
        if (!ownerType.PhysicalDefinition.MetadataModule.Equals(target.SourceMetadataModule))
        {
            throw new ArgumentException("The InterfaceImpl target token must originate in the owner row's module.", nameof(target));
        }
        return new MetadataInterfaceImplementationEdgeIdentity(interfaceImplementationToken, ownerType, target);
    }

    /// <summary>Tests canonical equality between two draft InterfaceImpl edges.</summary>
    /// <param name="other">The other draft edge.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationEdgeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataInterfaceImplementationEdgeIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact GenericParamConstraint row and its complete TypeDefOrRef constraint target.</summary>
/// <remarks>The sealed draft edge retains both the owner GenericParam row and the independent target row identity.</remarks>
public sealed class MetadataGenericParameterConstraintEdgeIdentity : IEquatable<MetadataGenericParameterConstraintEdgeIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-constraint-edge-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterConstraintEdgeIdentity(
        int genericParameterConstraintToken,
        MetadataGenericParameterIdentity ownerParameter,
        MetadataTypeDefinitionIdentity declaringType,
        MetadataTypeDefOrRefTargetIdentity target)
    {
        GenericParameterConstraintToken = genericParameterConstraintToken;
        OwnerParameter = ownerParameter;
        DeclaringType = declaringType;
        Target = target;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(genericParameterConstraintToken);
        writer.WriteLengthPrefixedBytes(ownerParameter.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(declaringType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(target.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact non-nil GenericParamConstraint row token.</summary>
    public int GenericParameterConstraintToken { get; }
    /// <summary>Gets the exact Owner-column GenericParam row.</summary>
    public MetadataGenericParameterIdentity OwnerParameter { get; }
    /// <summary>Gets the exact W8 declaring TypeDef for either a TypeDef- or MethodDef-owned parameter.</summary>
    public MetadataTypeDefinitionIdentity DeclaringType { get; }
    /// <summary>Gets the exact Constraint-column TypeDefOrRef target.</summary>
    public MetadataTypeDefOrRefTargetIdentity Target { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact GenericParamConstraint-row draft edge.</summary>
    /// <param name="genericParameterConstraintToken">The exact non-nil GenericParamConstraint token.</param>
    /// <param name="ownerParameter">The exact Owner-column GenericParam row.</param>
    /// <param name="declaringType">The W8 TypeDef owner or exact MethodDef declaring type.</param>
    /// <param name="target">The exact Constraint-column TypeDefOrRef target.</param>
    /// <returns>A sealed immutable draft edge preserving both owner rows and the target row.</returns>
    public static MetadataGenericParameterConstraintEdgeIdentity Create(
        int genericParameterConstraintToken,
        MetadataGenericParameterIdentity ownerParameter,
        MetadataTypeDefinitionIdentity declaringType,
        MetadataTypeDefOrRefTargetIdentity target)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(
            genericParameterConstraintToken,
            0x2C,
            nameof(genericParameterConstraintToken));
        ArgumentNullException.ThrowIfNull(ownerParameter);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(target);
        var ownerMatches = ownerParameter.Owner.Kind switch
        {
            MetadataGenericParameterOwnerKind.TypeDefinition =>
                declaringType.RawDefinition.Equals(ownerParameter.Owner.TypeDefinition),
            MetadataGenericParameterOwnerKind.MethodDefinition =>
                declaringType.RawDefinition.MatchesPinnedW7(ownerParameter.Owner.MethodDefinition!.DeclaringType),
            _ => false,
        };
        if (!ownerMatches || !ownerParameter.Owner.MetadataModule.Equals(target.SourceMetadataModule))
        {
            throw new ArgumentException(
                "The constraint owner must resolve to the exact declaring TypeDef and its target token must share that module.");
        }
        return new MetadataGenericParameterConstraintEdgeIdentity(
            genericParameterConstraintToken,
            ownerParameter,
            declaringType,
            target);
    }

    /// <summary>Tests canonical equality between two draft GenericParamConstraint edges.</summary>
    /// <param name="other">The other draft edge.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataGenericParameterConstraintEdgeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterConstraintEdgeIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes a complete InterfaceImpl table and one exact TypeDef-owner projection.</summary>
/// <remarks>
/// This sealed draft aggregate validates the complete physical table before selecting rows for <see cref="OwnerType"/>.
/// A bounded or incomplete source retains no table prefix or selected row. Other owners may occur only in the explicit
/// complete-table evidence and can never leak into <see cref="Rows"/>.
/// </remarks>
public sealed class MetadataInterfaceImplementationSetIdentity : IEquatable<MetadataInterfaceImplementationSetIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interface-implementation-set-identity";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> completeTableRows;
    private readonly ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> rows;
    private readonly ImmutableArray<int> duplicateRowTokens;
    private readonly ImmutableArray<int> duplicateSemanticRowTokens;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationSetIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionIdentity ownerType,
        ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> completeTableRows,
        ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> rows,
        MetadataEdgeAggregateResultKind resultKind,
        MetadataEdgeAggregateIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        ImmutableArray<int> duplicateRowTokens,
        ImmutableArray<int> duplicateSemanticRowTokens)
    {
        SourceEnds = sourceEnds;
        OwnerType = ownerType;
        this.completeTableRows = completeTableRows;
        this.rows = rows;
        ResultKind = resultKind;
        Issue = issue;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        this.duplicateRowTokens = duplicateRowTokens;
        this.duplicateSemanticRowTokens = duplicateSemanticRowTokens;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(ownerType.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, completeTableRows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        WriteInt32Array(writer, duplicateRowTokens);
        WriteInt32Array(writer, duplicateSemanticRowTokens);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata-table source ends consulted before any row array.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }
    /// <summary>Gets the exact selected TypeDef owner whose InterfaceImpl rows are projected.</summary>
    public MetadataTypeDefinitionIdentity OwnerType { get; }
    /// <summary>Gets a defensive copy of the complete physical InterfaceImpl table only for exact or invalid input.</summary>
    public ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> CompleteTableRows =>
        ExpressionV2ContractEncoding.Copy(completeTableRows);
    /// <summary>Gets a defensive copy of selected-owner rows only for an exact result.</summary>
    public ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);
    /// <summary>Gets the typed aggregate validation result.</summary>
    public MetadataEdgeAggregateResultKind ResultKind { get; }
    /// <summary>Gets the first deterministic aggregate disposition.</summary>
    public MetadataEdgeAggregateIssue Issue { get; }
    /// <summary>Gets the declared InterfaceImpl source bound only for a cap-plus-one result.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets exactly cap plus one only for a reached-bound result; otherwise zero.</summary>
    public int ObservedCount { get; }
    /// <summary>Gets sorted physical row tokens that occurred more than once.</summary>
    public ImmutableArray<int> DuplicateRowTokens => ExpressionV2ContractEncoding.Copy(duplicateRowTokens);
    /// <summary>Gets sorted later-row tokens whose Class-plus-Interface pair duplicates an earlier row.</summary>
    public ImmutableArray<int> DuplicateSemanticRowTokens =>
        ExpressionV2ContractEncoding.Copy(duplicateSemanticRowTokens);
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Validates a complete InterfaceImpl table and projects exactly one owner.</summary>
    /// <param name="sourceEnds">Exact unsaturated metadata table source ends for the source module.</param>
    /// <param name="ownerType">The exact TypeDef owner to project after complete-table validation.</param>
    /// <param name="allRows">Every physical InterfaceImpl row in RID order, or no prefix after a source stop.</param>
    /// <returns>An immutable exact, factless non-exact, or typed invalid draft aggregate.</returns>
    public static MetadataInterfaceImplementationSetIdentity Create(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionIdentity ownerType,
        ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> allRows)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(ownerType);
        if (!ownerType.PhysicalDefinition.MetadataModule.Equals(sourceEnds.SourceModule))
        {
            throw new ArgumentException("The selected InterfaceImpl owner must belong to the exact source module.", nameof(ownerType));
        }
        if (!sourceEnds.ContainsTypeDefinitionToken(ownerType.TypeDefinitionToken))
        {
            throw new ArgumentException("The selected InterfaceImpl owner must lie within the exact TypeDef source end.", nameof(ownerType));
        }

        var sourceCount = sourceEnds.InterfaceImplementationRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumInterfaceImplementationRowCount)
        {
            var bound = new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.InterfaceImplementationRowCountBoundName,
                StaticFieldV2Limits.MaximumInterfaceImplementationRowCount);
            return new MetadataInterfaceImplementationSetIdentity(
                sourceEnds,
                ownerType,
                ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty,
                ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty,
                MetadataEdgeAggregateResultKind.NonExact,
                MetadataEdgeAggregateIssue.BoundReached,
                bound,
                checked(StaticFieldV2Limits.MaximumInterfaceImplementationRowCount + 1),
                ImmutableArray<int>.Empty,
                ImmutableArray<int>.Empty);
        }
        if (allRows.IsDefault || allRows.Length < sourceCount)
        {
            return new MetadataInterfaceImplementationSetIdentity(
                sourceEnds,
                ownerType,
                ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty,
                ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty,
                MetadataEdgeAggregateResultKind.NonExact,
                MetadataEdgeAggregateIssue.SourceIncomplete,
                null,
                0,
                ImmutableArray<int>.Empty,
                ImmutableArray<int>.Empty);
        }
        if (allRows.Length > sourceCount)
        {
            return new MetadataInterfaceImplementationSetIdentity(
                sourceEnds,
                ownerType,
                ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty,
                ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty,
                MetadataEdgeAggregateResultKind.Invalid,
                MetadataEdgeAggregateIssue.SourceRowCountConflict,
                null,
                0,
                ImmutableArray<int>.Empty,
                ImmutableArray<int>.Empty);
        }

        var copied = ExpressionV2ContractEncoding.CopyRequired(
            allRows,
            nameof(allRows),
            StaticFieldV2Limits.MaximumInterfaceImplementationRowCount);
        var duplicateTokens = copied
            .GroupBy(static row => row.InterfaceImplementationToken)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static token => token)
            .ToImmutableArray();
        var physicalOrderInvalid = copied
            .Select((row, index) => CanonicalReplayEncoding.MetadataTokenRowId(row.InterfaceImplementationToken) != index + 1)
            .Any(static invalid => invalid);
        var ownerOrderInvalid = copied
            .Zip(copied.Skip(1), static (left, right) =>
                CanonicalReplayEncoding.MetadataTokenRowId(left.OwnerType.TypeDefinitionToken) >
                CanonicalReplayEncoding.MetadataTokenRowId(right.OwnerType.TypeDefinitionToken))
            .Any(static invalid => invalid);
        var moduleMismatch = copied.Any(row =>
            !row.OwnerType.PhysicalDefinition.MetadataModule.Equals(sourceEnds.SourceModule) ||
            !row.Target.SourceMetadataModule.Equals(sourceEnds.SourceModule));
        var sourceTokenOutOfRange = copied.Any(row =>
            !sourceEnds.ContainsTypeDefinitionToken(row.OwnerType.TypeDefinitionToken) ||
            !sourceEnds.ContainsTypeDefOrRefToken(row.Target.SourceMetadataToken));
        var duplicateSemanticTokens = FindDuplicateInterfaceSemanticRows(copied);
        var issue = !duplicateTokens.IsEmpty || physicalOrderInvalid || ownerOrderInvalid
            ? MetadataEdgeAggregateIssue.PhysicalOrderInvalid
            : moduleMismatch
                ? MetadataEdgeAggregateIssue.ModuleMismatch
                : sourceTokenOutOfRange
                    ? MetadataEdgeAggregateIssue.SourceTokenOutOfRange
                    : !duplicateSemanticTokens.IsEmpty
                        ? MetadataEdgeAggregateIssue.DuplicateSemanticEdge
                        : MetadataEdgeAggregateIssue.None;
        var exact = issue == MetadataEdgeAggregateIssue.None;
        var selected = exact
            ? copied.Where(row => row.OwnerType.Equals(ownerType)).ToImmutableArray()
            : ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty;
        return new MetadataInterfaceImplementationSetIdentity(
            sourceEnds,
            ownerType,
            copied,
            selected,
            exact ? MetadataEdgeAggregateResultKind.Exact : MetadataEdgeAggregateResultKind.Invalid,
            issue,
            null,
            0,
            duplicateTokens,
            duplicateSemanticTokens);
    }

    /// <summary>Tests canonical equality between two InterfaceImpl draft aggregates.</summary>
    /// <param name="other">The other draft aggregate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationSetIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataInterfaceImplementationSetIdentity);
    /// <summary>Computes a hash code from immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ImmutableArray<int> FindDuplicateInterfaceSemanticRows(
        ImmutableArray<MetadataInterfaceImplementationEdgeIdentity> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new SortedSet<int>();
        foreach (var row in values)
        {
            var key = string.Concat(row.OwnerType.Sha256, ":", row.Target.Sha256);
            if (!seen.Add(key))
            {
                duplicates.Add(row.InterfaceImplementationToken);
            }
        }
        return duplicates.ToImmutableArray();
    }

    private static void WriteInt32Array(CanonicalReplayEncoding.Writer writer, ImmutableArray<int> values)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteInt32(value);
        }
    }
}

/// <summary>Freezes a complete GenericParamConstraint table and one exact GenericParam-owner projection.</summary>
/// <remarks>
/// The sealed draft aggregate keeps complete-table evidence separate from selected-parameter rows. Bound or source
/// incompleteness retains neither prefix. Physical rows for other parameters are accepted only in the explicit complete
/// table needed to prove the selected parameter's exact absence or complete constraint set.
/// </remarks>
public sealed class MetadataGenericParameterConstraintSetIdentity :
    IEquatable<MetadataGenericParameterConstraintSetIdentity>
{
    private const string CanonicalDomain = "metadata-v2-generic-parameter-constraint-set-identity";
    private const int CanonicalSchemaVersion = 3;
    private readonly ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> completeTableRows;
    private readonly ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> rows;
    private readonly ImmutableArray<int> duplicateRowTokens;
    private readonly ImmutableArray<int> duplicateSemanticRowTokens;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterConstraintSetIdentity(
        MetadataGenericParameterTableCatalogIdentity genericParameterCatalog,
        MetadataGenericParameterIdentity ownerParameter,
        ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> completeTableRows,
        ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> rows,
        MetadataEdgeAggregateResultKind resultKind,
        MetadataEdgeAggregateIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        ImmutableArray<int> duplicateRowTokens,
        ImmutableArray<int> duplicateSemanticRowTokens)
    {
        GenericParameterCatalog = genericParameterCatalog;
        SourceEnds = genericParameterCatalog.SourceEnds;
        OwnerParameter = ownerParameter;
        this.completeTableRows = completeTableRows;
        this.rows = rows;
        ResultKind = resultKind;
        Issue = issue;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        this.duplicateRowTokens = duplicateRowTokens;
        this.duplicateSemanticRowTokens = duplicateSemanticRowTokens;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(genericParameterCatalog.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(ownerParameter.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, completeTableRows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        WriteInt32Array(writer, duplicateRowTokens);
        WriteInt32Array(writer, duplicateSemanticRowTokens);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets exact metadata-table source ends consulted before any constraint row array.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }
    /// <summary>Gets the exact complete GenericParam catalog proving every constraint-owner identity.</summary>
    public MetadataGenericParameterTableCatalogIdentity GenericParameterCatalog { get; }
    /// <summary>Gets the exact selected GenericParam row whose constraints are projected.</summary>
    public MetadataGenericParameterIdentity OwnerParameter { get; }
    /// <summary>Gets a defensive copy of the complete physical constraint table only for exact or invalid input.</summary>
    public ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> CompleteTableRows =>
        ExpressionV2ContractEncoding.Copy(completeTableRows);
    /// <summary>Gets selected-parameter constraint rows only for an exact result.</summary>
    public ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);
    /// <summary>Gets the typed aggregate validation result.</summary>
    public MetadataEdgeAggregateResultKind ResultKind { get; }
    /// <summary>Gets the first deterministic aggregate disposition.</summary>
    public MetadataEdgeAggregateIssue Issue { get; }
    /// <summary>Gets the module-table or selected-owner constraint bound only for cap plus one.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets exactly cap plus one only for a reached-bound result; otherwise zero.</summary>
    public int ObservedCount { get; }
    /// <summary>Gets sorted physical row tokens that occurred more than once.</summary>
    public ImmutableArray<int> DuplicateRowTokens => ExpressionV2ContractEncoding.Copy(duplicateRowTokens);
    /// <summary>Gets sorted later-row tokens whose Owner-plus-Constraint pair duplicates an earlier row.</summary>
    public ImmutableArray<int> DuplicateSemanticRowTokens =>
        ExpressionV2ContractEncoding.Copy(duplicateSemanticRowTokens);
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Validates a complete GenericParamConstraint table and projects exactly one parameter owner.</summary>
    /// <param name="genericParameterCatalog">The exact complete GenericParam catalog anchoring selected and edge owners.</param>
    /// <param name="ownerParameter">The exact catalog GenericParam row to project after complete-table validation.</param>
    /// <param name="allRows">Every physical GenericParamConstraint row in RID order, or no prefix after a stop.</param>
    /// <returns>An immutable exact, factless non-exact, or typed invalid draft aggregate.</returns>
    public static MetadataGenericParameterConstraintSetIdentity Create(
        MetadataGenericParameterTableCatalogIdentity genericParameterCatalog,
        MetadataGenericParameterIdentity ownerParameter,
        ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> allRows)
    {
        ArgumentNullException.ThrowIfNull(genericParameterCatalog);
        ArgumentNullException.ThrowIfNull(ownerParameter);
        if (genericParameterCatalog.ResultKind != MetadataGenericParameterProofResultKind.Exact)
        {
            throw new ArgumentException(
                "The constraint aggregate requires an exact complete GenericParam catalog.",
                nameof(genericParameterCatalog));
        }
        var sourceEnds = genericParameterCatalog.SourceEnds;
        if (!CatalogContainsExactRow(genericParameterCatalog, ownerParameter))
        {
            return InvalidCatalogBinding(genericParameterCatalog, ownerParameter);
        }

        var sourceCount = sourceEnds.GenericParameterConstraintRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount)
        {
            var bound = new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName,
                StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount);
            return new MetadataGenericParameterConstraintSetIdentity(
                genericParameterCatalog,
                ownerParameter,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                MetadataEdgeAggregateResultKind.NonExact,
                MetadataEdgeAggregateIssue.BoundReached,
                bound,
                checked(StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount + 1),
                ImmutableArray<int>.Empty,
                ImmutableArray<int>.Empty);
        }
        if (allRows.IsDefault || allRows.Length < sourceCount)
        {
            return new MetadataGenericParameterConstraintSetIdentity(
                genericParameterCatalog,
                ownerParameter,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                MetadataEdgeAggregateResultKind.NonExact,
                MetadataEdgeAggregateIssue.SourceIncomplete,
                null,
                0,
                ImmutableArray<int>.Empty,
                ImmutableArray<int>.Empty);
        }
        if (allRows.Length > sourceCount)
        {
            return new MetadataGenericParameterConstraintSetIdentity(
                genericParameterCatalog,
                ownerParameter,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                MetadataEdgeAggregateResultKind.Invalid,
                MetadataEdgeAggregateIssue.SourceRowCountConflict,
                null,
                0,
                ImmutableArray<int>.Empty,
                ImmutableArray<int>.Empty);
        }

        var copied = ExpressionV2ContractEncoding.CopyRequired(
            allRows,
            nameof(allRows),
            StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount);
        if (copied.Any(row => !CatalogContainsExactRow(genericParameterCatalog, row.OwnerParameter)))
        {
            return InvalidCatalogBinding(genericParameterCatalog, ownerParameter);
        }
        var duplicateTokens = copied
            .GroupBy(static row => row.GenericParameterConstraintToken)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static token => token)
            .ToImmutableArray();
        var physicalOrderInvalid = copied
            .Select((row, index) => CanonicalReplayEncoding.MetadataTokenRowId(row.GenericParameterConstraintToken) != index + 1)
            .Any(static invalid => invalid);
        var ownerOrderInvalid = copied
            .Zip(copied.Skip(1), static (left, right) =>
                CanonicalReplayEncoding.MetadataTokenRowId(left.OwnerParameter.GenericParameterToken) >
                CanonicalReplayEncoding.MetadataTokenRowId(right.OwnerParameter.GenericParameterToken))
            .Any(static invalid => invalid);
        var moduleMismatch = copied.Any(row =>
            !row.OwnerParameter.Owner.MetadataModule.Equals(sourceEnds.SourceModule) ||
            !row.Target.SourceMetadataModule.Equals(sourceEnds.SourceModule));
        var sourceTokenOutOfRange = copied.Any(row =>
            !sourceEnds.ContainsGenericParameterToken(row.OwnerParameter.GenericParameterToken) ||
            !OwnerTokenIsWithinSource(sourceEnds, row.OwnerParameter.Owner) ||
            !sourceEnds.ContainsTypeDefinitionToken(row.DeclaringType.TypeDefinitionToken) ||
            !sourceEnds.ContainsTypeDefOrRefToken(row.Target.SourceMetadataToken));
        var duplicateSemanticTokens = FindDuplicateConstraintSemanticRows(copied);
        var issue = !duplicateTokens.IsEmpty || physicalOrderInvalid || ownerOrderInvalid
            ? MetadataEdgeAggregateIssue.PhysicalOrderInvalid
            : moduleMismatch
                ? MetadataEdgeAggregateIssue.ModuleMismatch
                : sourceTokenOutOfRange
                    ? MetadataEdgeAggregateIssue.SourceTokenOutOfRange
                    : !duplicateSemanticTokens.IsEmpty
                        ? MetadataEdgeAggregateIssue.DuplicateSemanticEdge
                        : MetadataEdgeAggregateIssue.None;
        var exact = issue == MetadataEdgeAggregateIssue.None;
        var selected = exact
            ? copied.Where(row => row.OwnerParameter.Equals(ownerParameter)).ToImmutableArray()
            : ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty;
        if (exact && selected.Length > StaticFieldV2Limits.MaximumGenericConstraintCount)
        {
            var bound = new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.GenericConstraintCountBoundName,
                StaticFieldV2Limits.MaximumGenericConstraintCount);
            return new MetadataGenericParameterConstraintSetIdentity(
                genericParameterCatalog,
                ownerParameter,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
                MetadataEdgeAggregateResultKind.NonExact,
                MetadataEdgeAggregateIssue.BoundReached,
                bound,
                checked(StaticFieldV2Limits.MaximumGenericConstraintCount + 1),
                ImmutableArray<int>.Empty,
                ImmutableArray<int>.Empty);
        }
        return new MetadataGenericParameterConstraintSetIdentity(
            genericParameterCatalog,
            ownerParameter,
            copied,
            selected,
            exact ? MetadataEdgeAggregateResultKind.Exact : MetadataEdgeAggregateResultKind.Invalid,
            issue,
            null,
            0,
            duplicateTokens,
            duplicateSemanticTokens);
    }

    /// <summary>Tests canonical equality between two GenericParamConstraint draft aggregates.</summary>
    /// <param name="other">The other draft aggregate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataGenericParameterConstraintSetIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterConstraintSetIdentity);
    /// <summary>Computes a hash code from immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static bool CatalogContainsExactRow(
        MetadataGenericParameterTableCatalogIdentity genericParameterCatalog,
        MetadataGenericParameterIdentity candidate)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(candidate.GenericParameterToken);
        var catalogRows = genericParameterCatalog.ExactRows;
        return rowId > 0 && rowId <= catalogRows.Length && catalogRows[rowId - 1].Equals(candidate);
    }

    private static MetadataGenericParameterConstraintSetIdentity InvalidCatalogBinding(
        MetadataGenericParameterTableCatalogIdentity genericParameterCatalog,
        MetadataGenericParameterIdentity ownerParameter) =>
        new(
            genericParameterCatalog,
            ownerParameter,
            ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
            ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty,
            MetadataEdgeAggregateResultKind.Invalid,
            MetadataEdgeAggregateIssue.GenericParameterCatalogMismatch,
            null,
            0,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty);

    private static ImmutableArray<int> FindDuplicateConstraintSemanticRows(
        ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new SortedSet<int>();
        foreach (var row in values)
        {
            var key = string.Concat(row.OwnerParameter.Sha256, ":", row.Target.Sha256);
            if (!seen.Add(key))
            {
                duplicates.Add(row.GenericParameterConstraintToken);
            }
        }
        return duplicates.ToImmutableArray();
    }

    private static void WriteInt32Array(CanonicalReplayEncoding.Writer writer, ImmutableArray<int> values)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteInt32(value);
        }
    }

    private static bool OwnerTokenIsWithinSource(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterOwnerIdentity owner) =>
        owner.Kind switch
        {
            MetadataGenericParameterOwnerKind.TypeDefinition =>
                sourceEnds.ContainsTypeDefinitionToken(owner.OwnerMetadataToken),
            MetadataGenericParameterOwnerKind.MethodDefinition =>
                sourceEnds.ContainsMethodDefinitionToken(owner.OwnerMetadataToken),
            _ => false,
        };
}

/// <summary>Freezes one nested named-construction segment and its segment-local closed arguments.</summary>
/// <remarks>The sealed draft segment proves how one flattened vector is partitioned across metadata names.</remarks>
public sealed class MetadataTypeConstructionSegment : IEquatable<MetadataTypeConstructionSegment>
{
    private const string CanonicalDomain = "metadata-v2-type-construction-segment";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataClosedTypeIdentity> localArguments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeConstructionSegment(
        MetadataTypeDefinitionIdentity definition,
        int flattenedArgumentOffset,
        ImmutableArray<MetadataClosedTypeIdentity> localArguments)
    {
        Definition = definition;
        FlattenedArgumentOffset = flattenedArgumentOffset;
        this.localArguments = localArguments;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(definition.CanonicalBytes.AsSpan());
        writer.WriteInt32(flattenedArgumentOffset);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            localArguments,
            static argument => argument.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact TypeDef represented by this named segment.</summary>
    public MetadataTypeDefinitionIdentity Definition { get; }
    /// <summary>Gets the exact offset of this segment's arguments in the flattened vector.</summary>
    public int FlattenedArgumentOffset { get; }
    /// <summary>Gets a defensive copy of exact segment-local closed arguments.</summary>
    public ImmutableArray<MetadataClosedTypeIdentity> LocalArguments =>
        ExpressionV2ContractEncoding.Copy(localArguments);
    /// <summary>Gets the segment-local argument count.</summary>
    public int LocalArgumentCount => localArguments.Length;
    /// <summary>Gets the exclusive flattened argument end after overflow-checked validation.</summary>
    public int FlattenedArgumentEndExclusive => FlattenedArgumentOffset + localArguments.Length;
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one overflow-checked nested construction draft segment.</summary>
    /// <param name="definition">The exact W8 TypeDef for this source segment.</param>
    /// <param name="flattenedArgumentOffset">The non-negative start in the complete flattened vector.</param>
    /// <param name="localArguments">Exactly the arguments introduced by this metadata name.</param>
    /// <returns>A sealed immutable draft segment.</returns>
    public static MetadataTypeConstructionSegment Create(
        MetadataTypeDefinitionIdentity definition,
        int flattenedArgumentOffset,
        ImmutableArray<MetadataClosedTypeIdentity> localArguments)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            localArguments,
            nameof(localArguments),
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount);
        if (flattenedArgumentOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flattenedArgumentOffset));
        }
        if (copied.Length > int.MaxValue - flattenedArgumentOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flattenedArgumentOffset),
                "The flattened argument interval exceeds Int32 range.");
        }
        var endExclusive = flattenedArgumentOffset + copied.Length;
        if (definition.GenericMappingStatus != MetadataNestedGenericMappingStatus.ExactPinnedCompiler ||
            copied.Length != definition.IntroducedArity || endExclusive != definition.FlattenedArity)
        {
            throw new ArgumentException(
                "The segment-local argument interval must equal the definition's exact introduced/cumulative arity.",
                nameof(localArguments));
        }
        return new MetadataTypeConstructionSegment(definition, flattenedArgumentOffset, copied);
    }

    /// <summary>Tests canonical equality between two draft construction segments.</summary>
    /// <param name="other">The other draft segment.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeConstructionSegment? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeConstructionSegment);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact fully closed metadata type topology.</summary>
/// <remarks>
/// The sealed draft identity preserves nested local-argument groups, the exact Nullable metadata head, ARRAY
/// sizes/lower bounds, and rank-one ARRAY as distinct from SZARRAY. It never contains VAR, MVAR, or a bare generic
/// definition.
/// </remarks>
public sealed class MetadataClosedTypeIdentity : IEquatable<MetadataClosedTypeIdentity>
{
    private const string CanonicalDomain = "metadata-v2-closed-type-identity";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<MetadataTypeConstructionSegment> constructionSegments;
    private readonly ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments;
    private readonly ImmutableArray<int> arraySizes;
    private readonly ImmutableArray<int> arrayLowerBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataClosedTypeIdentity(
        MetadataClosedTypeKind kind,
        MetadataPrimitiveTypeKind? primitiveKind,
        ImmutableArray<MetadataTypeConstructionSegment> constructionSegments,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments,
        MetadataClosedTypeIdentity? elementType,
        int? arrayRank,
        ImmutableArray<int> arraySizes,
        ImmutableArray<int> arrayLowerBounds,
        int topologyDepth,
        int topologyNodeCount)
    {
        Kind = kind;
        PrimitiveKind = primitiveKind;
        this.constructionSegments = constructionSegments;
        this.flattenedArguments = flattenedArguments;
        ElementType = elementType;
        ArrayRank = arrayRank;
        this.arraySizes = arraySizes;
        this.arrayLowerBounds = arrayLowerBounds;
        TopologyDepth = topologyDepth;
        TopologyNodeCount = topologyNodeCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, primitiveKind);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            constructionSegments,
            static segment => segment.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            flattenedArguments,
            static argument => argument.CanonicalBytes);
        writer.WriteBoolean(elementType is not null);
        if (elementType is not null)
        {
            writer.WriteLengthPrefixedBytes(elementType.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, arrayRank);
        WriteInt32Array(writer, arraySizes);
        WriteInt32Array(writer, arrayLowerBounds);
        writer.WriteInt32(topologyDepth);
        writer.WriteInt32(topologyNodeCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact closed topology kind.</summary>
    public MetadataClosedTypeKind Kind { get; }
    /// <summary>Gets the primitive kind only for a primitive topology.</summary>
    public MetadataPrimitiveTypeKind? PrimitiveKind { get; }
    /// <summary>Gets a defensive copy of outer-to-inner named construction segments.</summary>
    public ImmutableArray<MetadataTypeConstructionSegment> ConstructionSegments =>
        ExpressionV2ContractEncoding.Copy(constructionSegments);
    /// <summary>Gets a defensive copy of the canonical flattened named-argument vector.</summary>
    public ImmutableArray<MetadataClosedTypeIdentity> FlattenedArguments =>
        ExpressionV2ContractEncoding.Copy(flattenedArguments);
    /// <summary>Gets the exact nullable or array element type, otherwise null.</summary>
    public MetadataClosedTypeIdentity? ElementType { get; }
    /// <summary>Gets the exact array rank only for an array topology.</summary>
    public int? ArrayRank { get; }
    /// <summary>Gets a defensive copy of exact ordered ARRAY sizes.</summary>
    public ImmutableArray<int> ArraySizes => ExpressionV2ContractEncoding.Copy(arraySizes);
    /// <summary>Gets a defensive copy of exact ordered signed ARRAY lower bounds.</summary>
    public ImmutableArray<int> ArrayLowerBounds => ExpressionV2ContractEncoding.Copy(arrayLowerBounds);
    /// <summary>Gets the exact complete closed-topology depth.</summary>
    public int TopologyDepth { get; }
    /// <summary>Gets the exact cumulative closed-topology node count.</summary>
    public int TopologyNodeCount { get; }
    /// <summary>Gets the final named TypeDef, or null for non-named topology.</summary>
    public MetadataTypeDefinitionIdentity? FinalDefinition =>
        constructionSegments.IsEmpty ? null : constructionSegments[^1].Definition;
    /// <summary>Gets whether this closed topology has reference-type semantics.</summary>
    public bool IsReferenceType => Kind switch
    {
        MetadataClosedTypeKind.SzArray or MetadataClosedTypeKind.MultidimensionalArray => true,
        MetadataClosedTypeKind.Primitive => PrimitiveKind is MetadataPrimitiveTypeKind.String or MetadataPrimitiveTypeKind.Object,
        MetadataClosedTypeKind.Named => FinalDefinition!.Kind is MetadataTypeDefinitionKind.Class or
            MetadataTypeDefinitionKind.Interface or MetadataTypeDefinitionKind.Delegate,
        _ => false,
    };
    /// <summary>Gets whether this closed topology has non-nullable value-type semantics.</summary>
    public bool IsNonNullableValueType => Kind switch
    {
        MetadataClosedTypeKind.Primitive => PrimitiveKind is not (MetadataPrimitiveTypeKind.String or MetadataPrimitiveTypeKind.Object),
        MetadataClosedTypeKind.Named => FinalDefinition!.Kind is MetadataTypeDefinitionKind.ValueType or MetadataTypeDefinitionKind.Enum,
        _ => false,
    };
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one primitive closed draft identity.</summary>
    /// <param name="kind">The exact primitive kind.</param>
    /// <returns>A sealed immutable draft closed identity.</returns>
    internal static MetadataClosedTypeIdentity Primitive(MetadataPrimitiveTypeKind kind)
    {
        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        return CreateCore(
            MetadataClosedTypeKind.Primitive,
            kind,
            ImmutableArray<MetadataTypeConstructionSegment>.Empty,
            ImmutableArray<MetadataClosedTypeIdentity>.Empty,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty);
    }

    /// <summary>Creates one top-level or nested named closed draft construction.</summary>
    /// <param name="segments">Every outer-to-inner named segment with exact local argument slices.</param>
    /// <returns>A sealed immutable draft closed identity.</returns>
    internal static MetadataClosedTypeIdentity Named(ImmutableArray<MetadataTypeConstructionSegment> segments)
    {
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            segments,
            nameof(segments),
            StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1);
        if (copied.IsEmpty)
        {
            throw new ArgumentException("A named construction requires at least one segment.", nameof(segments));
        }
        var flattened = ImmutableArray.CreateBuilder<MetadataClosedTypeIdentity>();
        for (var index = 0; index < copied.Length; index++)
        {
            var segment = copied[index];
            if (segment.Definition.ClassificationStatus != MetadataTypeDefinitionClassificationStatus.Exact)
            {
                throw new ArgumentException(
                    "Every named construction segment requires exact TypeDef classification.",
                    nameof(segments));
            }
            if (segment.Definition.SegmentIndex != index ||
                segment.FlattenedArgumentOffset != flattened.Count)
            {
                throw new ArgumentException("Named segments and flattened offsets must be complete and contiguous.", nameof(segments));
            }
            if (index == 0)
            {
                if (segment.Definition.PhysicalDefinition.EnclosingType is not null)
                {
                    throw new ArgumentException("The first named construction segment must be top-level.", nameof(segments));
                }
            }
            else if (segment.Definition.PhysicalDefinition.EnclosingType is null ||
                     !segment.Definition.PhysicalDefinition.EnclosingType.Equals(copied[index - 1].Definition.PhysicalDefinition))
            {
                throw new ArgumentException("Each nested segment must name the preceding exact enclosing TypeDef.", nameof(segments));
            }
            flattened.AddRange(segment.LocalArguments);
        }
        if (flattened.Count != copied[^1].Definition.FlattenedArity)
        {
            throw new ArgumentException("Segment-local groups must exhaust the final flattened arity.", nameof(segments));
        }
        var flattenedArguments = flattened.ToImmutable();
        if (IsExactNullableDefinition(
                copied.Select(static segment => segment.Definition).ToImmutableArray(),
                flattenedArguments))
        {
            return Nullable(copied, flattenedArguments[0]);
        }
        return CreateCore(
            MetadataClosedTypeKind.Named,
            null,
            copied,
            flattenedArguments,
            null,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty);
    }

    /// <summary>Creates one nullable closed draft identity over its exact metadata head and value-type argument.</summary>
    /// <param name="segments">The exact System.Nullable`1 construction segment.</param>
    /// <param name="elementType">The exact non-nullable value-type element.</param>
    /// <returns>A sealed immutable draft closed identity.</returns>
    internal static MetadataClosedTypeIdentity Nullable(
        ImmutableArray<MetadataTypeConstructionSegment> segments,
        MetadataClosedTypeIdentity elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        var copiedSegments = ExpressionV2ContractEncoding.CopyRequired(
            segments,
            nameof(segments),
            maximumCount: 1);
        if (copiedSegments.Length != 1 ||
            copiedSegments[0].Definition.ClassificationStatus != MetadataTypeDefinitionClassificationStatus.Exact ||
            copiedSegments[0].LocalArguments.Length != 1 ||
            !copiedSegments[0].LocalArguments[0].Equals(elementType))
        {
            throw new ArgumentException(
                "Nullable requires one exact metadata construction segment containing its element argument.",
                nameof(segments));
        }
        if (!elementType.IsNonNullableValueType ||
            elementType.Kind == MetadataClosedTypeKind.Named &&
            elementType.FinalDefinition!.ClassificationStatus != MetadataTypeDefinitionClassificationStatus.Exact)
        {
            throw new ArgumentException("Nullable requires one exact non-nullable value-type element.", nameof(elementType));
        }
        return CreateCore(
            MetadataClosedTypeKind.Nullable,
            null,
            copiedSegments,
            [elementType],
            elementType,
            null,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty);
    }

    /// <summary>Creates one SZARRAY closed draft identity.</summary>
    /// <param name="elementType">The exact closed element type.</param>
    /// <returns>A sealed immutable draft identity distinct from rank-one ARRAY.</returns>
    internal static MetadataClosedTypeIdentity SzArray(MetadataClosedTypeIdentity elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return CreateCore(
            MetadataClosedTypeKind.SzArray,
            null,
            ImmutableArray<MetadataTypeConstructionSegment>.Empty,
            ImmutableArray<MetadataClosedTypeIdentity>.Empty,
            elementType,
            1,
            ImmutableArray<int>.Empty,
            ImmutableArray<int>.Empty);
    }

    /// <summary>Creates one exact ARRAY closed draft identity, including metadata-only rank one.</summary>
    /// <param name="elementType">The exact closed element type.</param>
    /// <param name="rank">The exact rank from one through thirty-two.</param>
    /// <param name="sizes">The ordered non-negative sizes for encoded leading dimensions.</param>
    /// <param name="lowerBounds">The ordered signed lower bounds for encoded leading dimensions.</param>
    /// <returns>A sealed immutable draft identity preserving rank and both ordered shape vectors.</returns>
    internal static MetadataClosedTypeIdentity MultidimensionalArray(
        MetadataClosedTypeIdentity elementType,
        int rank,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        ValidateArrayShape(rank, sizes, lowerBounds, out var copiedSizes, out var copiedLowerBounds);
        return CreateCore(
            MetadataClosedTypeKind.MultidimensionalArray,
            null,
            ImmutableArray<MetadataTypeConstructionSegment>.Empty,
            ImmutableArray<MetadataClosedTypeIdentity>.Empty,
            elementType,
            rank,
            copiedSizes,
            copiedLowerBounds);
    }

    /// <summary>Tests canonical equality between two closed draft type identities.</summary>
    /// <param name="other">The other draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataClosedTypeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataClosedTypeIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataClosedTypeIdentity FromGroundSignature(MetadataTypeSignatureNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsFullyGround || node.ContainsUnsupportedClosedShape)
        {
            throw new ArgumentException("A closed projection requires one fully ground admitted signature.", nameof(node));
        }
        return node.Kind switch
        {
            MetadataTypeSignatureNodeKind.Primitive => Primitive(node.PrimitiveKind!.Value),
            MetadataTypeSignatureNodeKind.Named => Named(CreateSegments(
                node.NamedDefinitionChain,
                ImmutableArray<MetadataClosedTypeIdentity>.Empty)),
            MetadataTypeSignatureNodeKind.GenericInstantiation => ConstructNamed(
                node.NamedDefinitionChain,
                node.Children.Select(FromGroundSignature).ToImmutableArray()),
            MetadataTypeSignatureNodeKind.SzArray => SzArray(FromGroundSignature(node.Children[0])),
            MetadataTypeSignatureNodeKind.MultidimensionalArray => MultidimensionalArray(
                FromGroundSignature(node.Children[0]),
                node.ArrayRank!.Value,
                node.ArraySizes,
                node.ArrayLowerBounds),
            _ => throw new ArgumentException("The signature does not have an admitted closed topology.", nameof(node)),
        };
    }

    internal static MetadataClosedTypeIdentity ConstructNamed(
        ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments)
    {
        var segments = CreateSegments(definitionChain, flattenedArguments);
        if (IsExactNullableDefinition(definitionChain, flattenedArguments))
        {
            return Nullable(segments, flattenedArguments[0]);
        }
        return Named(segments);
    }

    private static MetadataClosedTypeIdentity CreateCore(
        MetadataClosedTypeKind kind,
        MetadataPrimitiveTypeKind? primitiveKind,
        ImmutableArray<MetadataTypeConstructionSegment> constructionSegments,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments,
        MetadataClosedTypeIdentity? elementType,
        int? arrayRank,
        ImmutableArray<int> arraySizes,
        ImmutableArray<int> arrayLowerBounds)
    {
        var depth = 1;
        var nodes = 1;
        if (elementType is not null)
        {
            depth = checked(elementType.TopologyDepth + 1);
            nodes = checked(elementType.TopologyNodeCount + 1);
        }
        else
        {
            foreach (var child in flattenedArguments)
            {
                depth = Math.Max(depth, checked(child.TopologyDepth + 1));
                nodes = checked(nodes + child.TopologyNodeCount);
            }
        }
        if (depth > StaticFieldV2Limits.MaximumClosedTypeTopologyDepth)
        {
            throw new MetadataClosedTypeBoundException(new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName,
                StaticFieldV2Limits.MaximumClosedTypeTopologyDepth));
        }
        if (nodes > StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount)
        {
            throw new MetadataClosedTypeBoundException(new EvaluationDeterministicBound(
                ExpressionV2ContractLimits.ClosedTypeTopologyNodeCountBoundName,
                StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount));
        }
        return new MetadataClosedTypeIdentity(
            kind,
            primitiveKind,
            constructionSegments,
            flattenedArguments,
            elementType,
            arrayRank,
            arraySizes,
            arrayLowerBounds,
            depth,
            nodes);
    }

    private static ImmutableArray<MetadataTypeConstructionSegment> CreateSegments(
        ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments)
    {
        if (definitionChain.IsDefaultOrEmpty ||
            flattenedArguments.IsDefault ||
            flattenedArguments.Length != definitionChain[^1].FlattenedArity)
        {
            throw new ArgumentException("A complete definition chain and exact flattened argument vector are required.");
        }
        var builder = ImmutableArray.CreateBuilder<MetadataTypeConstructionSegment>(definitionChain.Length);
        var offset = 0;
        foreach (var definition in definitionChain)
        {
            if (definition.IntroducedArity is not { } localCount)
            {
                throw new ArgumentException("A non-exact nested mapping cannot partition flattened arguments.", nameof(definitionChain));
            }
            var local = flattenedArguments.Slice(offset, localCount);
            builder.Add(MetadataTypeConstructionSegment.Create(definition, offset, local));
            offset = checked(offset + localCount);
        }
        return builder.ToImmutable();
    }

    private static bool IsExactNullableDefinition(
        ImmutableArray<MetadataTypeDefinitionIdentity> definitionChain,
        ImmutableArray<MetadataClosedTypeIdentity> flattenedArguments)
    {
        if (definitionChain.Length != 1 || flattenedArguments.Length != 1)
        {
            return false;
        }
        var definition = definitionChain[0];
        return definition.ClassificationStatus == MetadataTypeDefinitionClassificationStatus.Exact &&
               definition.Kind == MetadataTypeDefinitionKind.ValueType &&
               definition.GenericMappingStatus == MetadataNestedGenericMappingStatus.ExactPinnedCompiler &&
               definition.IntroducedArity == 1 &&
               string.Equals(definition.RawDefinition.NamespaceName, "System", StringComparison.Ordinal) &&
               string.Equals(definition.RawDefinition.TypeName, "Nullable`1", StringComparison.Ordinal) &&
               definition.Ancestry?.CoreLibrary is { } coreLibrary &&
               definition.RawDefinition.MetadataModule.Equals(coreLibrary.MetadataModule);
    }

    private static void ValidateArrayShape(
        int rank,
        ImmutableArray<int> sizes,
        ImmutableArray<int> lowerBounds,
        out ImmutableArray<int> copiedSizes,
        out ImmutableArray<int> copiedLowerBounds)
    {
        if (rank < 1 || rank > StaticFieldV2Limits.MaximumArrayRank)
        {
            throw new ArgumentOutOfRangeException(nameof(rank));
        }
        copiedSizes = ExpressionV2ContractEncoding.CopyRequiredValues(sizes, nameof(sizes), rank);
        copiedLowerBounds = ExpressionV2ContractEncoding.CopyRequiredValues(lowerBounds, nameof(lowerBounds), rank);
        if (copiedSizes.Any(static size => size < 0 || size > 0x1FFF_FFFF))
        {
            throw new ArgumentException("Every ARRAY size must fit the compressed unsigned encoding.", nameof(sizes));
        }
        if (copiedLowerBounds.Any(static value => value < -0x1000_0000 || value > 0x0FFF_FFFF))
        {
            throw new ArgumentException("Every ARRAY lower bound must fit the compressed signed encoding.", nameof(lowerBounds));
        }
    }

    private static void WriteInt32Array(CanonicalReplayEncoding.Writer writer, ImmutableArray<int> values)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteInt32(value);
        }
    }
}

internal sealed class MetadataClosedTypeBoundException : Exception
{
    internal MetadataClosedTypeBoundException(EvaluationDeterministicBound bound) => Bound = bound;

    internal EvaluationDeterministicBound Bound { get; }
}

/// <summary>Freezes the typed draft projection of one complete signature into the closed-type model.</summary>
/// <remarks>An exact result alone carries a closed identity; open, non-admitted, and invalid results retain the source tree.</remarks>
public sealed class MetadataTypeConstructionResult : IEquatable<MetadataTypeConstructionResult>
{
    private const string CanonicalDomain = "metadata-v2-type-construction-result";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeConstructionResult(
        MetadataTypeConstructionResultKind kind,
        MetadataTypeSignatureNode root,
        MetadataClosedTypeIdentity? closedType,
        EvaluationDeterministicBound? reachedBound)
    {
        Kind = kind;
        Root = root;
        ClosedType = closedType;
        ReachedBound = reachedBound;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteLengthPrefixedBytes(root.CanonicalBytes.AsSpan());
        writer.WriteBoolean(closedType is not null);
        if (closedType is not null)
        {
            writer.WriteLengthPrefixedBytes(closedType.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(reachedBound is not null);
        if (reachedBound is not null)
        {
            writer.WriteString(reachedBound.Name);
            writer.WriteInt64(reachedBound.Value);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact typed construction result kind.</summary>
    public MetadataTypeConstructionResultKind Kind { get; }
    /// <summary>Gets the complete source signature tree retained by the draft result.</summary>
    public MetadataTypeSignatureNode Root { get; }
    /// <summary>Gets the exact closed type only for <see cref="MetadataTypeConstructionResultKind.Exact"/>.</summary>
    public MetadataClosedTypeIdentity? ClosedType { get; }
    /// <summary>Gets the reached construction bound when that, rather than provisional classification, caused NonExact.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Classifies one complete signature under the draft closed-construction grammar.</summary>
    /// <param name="root">The complete bounded pre-substitution signature tree.</param>
    /// <returns>A sealed immutable typed draft result with an exact closed identity only when derivable.</returns>
    internal static MetadataTypeConstructionResult Classify(MetadataTypeSignatureNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.ContainsInvalidTypeSpecificationHead)
        {
            return Invalid(root);
        }
        if (root.ContainsUnsupportedClosedShape || root.ContainsNonExactGenericMapping)
        {
            return new MetadataTypeConstructionResult(MetadataTypeConstructionResultKind.Unsupported, root, null, null);
        }
        if (!root.IsFullyGround)
        {
            return new MetadataTypeConstructionResult(MetadataTypeConstructionResultKind.Open, root, null, null);
        }
        if (root.ContainsProvisionalTypeDefinitionClassification)
        {
            return new MetadataTypeConstructionResult(MetadataTypeConstructionResultKind.NonExact, root, null, null);
        }
        try
        {
            return new MetadataTypeConstructionResult(
                MetadataTypeConstructionResultKind.Exact,
                root,
                MetadataClosedTypeIdentity.FromGroundSignature(root),
                null);
        }
        catch (MetadataClosedTypeBoundException exception)
        {
            return new MetadataTypeConstructionResult(
                MetadataTypeConstructionResultKind.NonExact,
                root,
                null,
                exception.Bound);
        }
        catch (ArgumentException)
        {
            return Invalid(root);
        }
    }

    /// <summary>Tests canonical equality between two typed draft construction results.</summary>
    /// <param name="other">The other draft result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeConstructionResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeConstructionResult);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeConstructionResult Invalid(MetadataTypeSignatureNode root) =>
        new(MetadataTypeConstructionResultKind.Invalid, root, null, null);
}

/// <summary>Freezes one exact GenericParam-row binding or explicit unavailable observation.</summary>
/// <remarks>
/// The sealed draft binding keeps row owner/module/number evidence inseparable from any closed argument. Only an
/// internal source-derived producer can assert an exact argument mapping; public callers can retain unavailability.
/// </remarks>
public sealed class MetadataTypeArgumentBindingIdentity : IEquatable<MetadataTypeArgumentBindingIdentity>
{
    private const string CanonicalDomain = "metadata-v2-type-argument-binding-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeArgumentBindingIdentity(
        MetadataTypeArgumentBindingKind kind,
        MetadataGenericParameterIdentity parameter,
        MetadataClosedTypeIdentity? argument)
    {
        Kind = kind;
        Parameter = parameter;
        Argument = argument;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteLengthPrefixedBytes(parameter.CanonicalBytes.AsSpan());
        writer.WriteBoolean(argument is not null);
        if (argument is not null)
        {
            writer.WriteLengthPrefixedBytes(argument.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact binding observation kind.</summary>
    public MetadataTypeArgumentBindingKind Kind { get; }
    /// <summary>Gets the exact declared GenericParam row and owner identity.</summary>
    public MetadataGenericParameterIdentity Parameter { get; }
    /// <summary>Gets the exact closed argument only for an exact binding.</summary>
    public MetadataClosedTypeIdentity? Argument { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact GenericParam-row-to-source-derived-closed-argument draft binding.</summary>
    /// <param name="parameter">The exact declared GenericParam row.</param>
    /// <param name="argument">
    /// The exact closed argument obtained from a source-derived metadata construction result. This draft factory does
    /// not establish an acquisition source and callers must not synthesize the argument identity.
    /// </param>
    /// <returns>A sealed immutable exact draft binding retaining the supplied source-derived argument.</returns>
    internal static MetadataTypeArgumentBindingIdentity Exact(
        MetadataGenericParameterIdentity parameter,
        MetadataClosedTypeIdentity argument)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(argument);
        return new MetadataTypeArgumentBindingIdentity(MetadataTypeArgumentBindingKind.Exact, parameter, argument);
    }

    /// <summary>Creates one declared GenericParam row whose closed binding is unavailable.</summary>
    /// <param name="parameter">The exact declared GenericParam row.</param>
    /// <returns>A sealed immutable open draft binding observation.</returns>
    public static MetadataTypeArgumentBindingIdentity Unavailable(MetadataGenericParameterIdentity parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return new MetadataTypeArgumentBindingIdentity(MetadataTypeArgumentBindingKind.Unavailable, parameter, null);
    }

    /// <summary>Tests canonical equality between two row-addressed draft bindings.</summary>
    /// <param name="other">The other draft binding.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeArgumentBindingIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeArgumentBindingIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact FieldSig source and its declaring-TypeDef binding proof for recursive substitution.</summary>
/// <remarks>
/// The sealed draft request derives its root only from <see cref="MetadataFieldSignatureIdentity"/> and retains the
/// complete <see cref="MetadataGenericParameterAuthorityBindingLedgerIdentity"/> plus one compatible guarded W7
/// bridge certificate. It has no raw-tree, raw-array, method-ledger, or frame-discovery input.
/// </remarks>
public sealed class MetadataTypeSubstitutionRequest : IEquatable<MetadataTypeSubstitutionRequest>
{
    private const string CanonicalDomain = "metadata-v2-type-substitution-request";
    private const int CanonicalSchemaVersion = 4;
    private readonly ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity> exactOwnerBindings;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSubstitutionRequest(
        MetadataFieldSignatureIdentity fieldSignature,
        MetadataW7TypeDefinitionCompatibilityCertificateIdentity declaringTypeCompatibility,
        MetadataGenericParameterAuthorityBindingLedgerIdentity declaringTypeBindings)
    {
        FieldSignature = fieldSignature;
        DeclaringTypeCompatibility = declaringTypeCompatibility;
        DeclaringTypeBindings = declaringTypeBindings;
        exactOwnerBindings = declaringTypeBindings.ResultKind == MetadataGenericParameterProofResultKind.Exact
            ? declaringTypeBindings.Bindings
            : ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity>.Empty;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)MetadataTypeSubstitutionContextKind.FieldDefinition);
        writer.WriteLengthPrefixedBytes(fieldSignature.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(declaringTypeCompatibility.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(declaringTypeBindings.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the only currently admitted source-anchored substitution context.</summary>
    public MetadataTypeSubstitutionContextKind ContextKind => MetadataTypeSubstitutionContextKind.FieldDefinition;
    /// <summary>Gets the exact FieldDef, source ends, FieldSig bytes, certificate, and projected root.</summary>
    public MetadataFieldSignatureIdentity FieldSignature { get; }
    /// <summary>Gets the compatible guarded W7 candidate-to-authority bridge for the declaring TypeDef.</summary>
    public MetadataW7TypeDefinitionCompatibilityCertificateIdentity DeclaringTypeCompatibility { get; }
    /// <summary>Gets the complete declaring-TypeDef GenericParam binding proof, including exact zero arity.</summary>
    public MetadataGenericParameterAuthorityBindingLedgerIdentity DeclaringTypeBindings { get; }
    /// <summary>Gets the source-derived complete FieldSig root.</summary>
    public MetadataTypeSignatureNode Root => FieldSignature.Root;
    /// <summary>Gets whether the complete declaring-TypeDef binding proof is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind BindingProofResultKind => DeclaringTypeBindings.ResultKind;
    /// <summary>Gets the typed declaring-TypeDef binding-proof issue, or none.</summary>
    public MetadataGenericParameterProofIssue BindingProofIssue => DeclaringTypeBindings.Issue;
    /// <summary>Gets the evaluator arity bound when the authority binding proof stopped at cap plus one.</summary>
    public EvaluationDeterministicBound? ReachedBound => DeclaringTypeBindings.ReachedBound;
    /// <summary>Gets the binding proof's cap-plus-one or incomplete observation count, otherwise zero.</summary>
    public int BindingProofObservedCount => DeclaringTypeBindings.ObservedCount;
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one source-anchored FieldDef recursive-substitution draft request.</summary>
    /// <param name="fieldSignature">The exact FieldDef-owned, fully consumed FieldSig identity supplying the root.</param>
    /// <param name="declaringTypeCompatibility">
    /// One compatible guarded bridge from the FieldSig's W7 declaring TypeDef to the ledger's authority TypeDef.
    /// </param>
    /// <param name="declaringTypeBindings">
    /// The authority-owned GenericParam binding proof for that exact declaring TypeDef. An initialized exact empty
    /// ledger is required for a non-generic declaring type; absence never denotes zero arity.
    /// </param>
    /// <returns>
    /// A sealed immutable draft request. A matching non-exact or invalid proof is retained so execution can stop
    /// before applying any binding; mismatched source ends or owner rows cannot form a request.
    /// </returns>
    public static MetadataTypeSubstitutionRequest ForFieldDefinition(
        MetadataFieldSignatureIdentity fieldSignature,
        MetadataW7TypeDefinitionCompatibilityCertificateIdentity declaringTypeCompatibility,
        MetadataGenericParameterAuthorityBindingLedgerIdentity declaringTypeBindings)
    {
        ArgumentNullException.ThrowIfNull(fieldSignature);
        ArgumentNullException.ThrowIfNull(declaringTypeCompatibility);
        ArgumentNullException.ThrowIfNull(declaringTypeBindings);
        if (!declaringTypeCompatibility.IsCompatible ||
            declaringTypeCompatibility.Candidate is not { } candidate ||
            !candidate.Equals(fieldSignature.FieldDefinition.DeclaringType))
        {
            throw new ArgumentException(
                "FieldSig substitution requires a compatible certificate for its exact W7 declaring TypeDef.",
                nameof(declaringTypeCompatibility));
        }

        var ownerGroup = declaringTypeBindings.OwnerGroup;
        if (ownerGroup.OwnerKind != MetadataGenericParameterOwnerKind.TypeDefinition ||
            ownerGroup.TypeDefinition is not { } ownerTypeDefinition)
        {
            throw new ArgumentException(
                "The authority binding ledger must name one TypeDef owner group.",
                nameof(declaringTypeBindings));
        }
        var certificateAuthority = declaringTypeCompatibility.AuthorityTypeDefinition;
        var catalogAuthority = declaringTypeBindings.DefinitionAuthority.ExactTypeDefinitionOrDefault(
            certificateAuthority.TypeDefinitionToken);
        if (catalogAuthority is null ||
            !catalogAuthority.Equals(certificateAuthority) ||
            !ownerTypeDefinition.Equals(certificateAuthority))
        {
            throw new ArgumentException(
                "The compatibility certificate and binding ledger must name the same authority-issued TypeDef.",
                nameof(declaringTypeBindings));
        }
        if (!fieldSignature.SourceEnds.Equals(declaringTypeBindings.DefinitionAuthority.SourceEnds) ||
            !fieldSignature.SourceEnds.Equals(ownerGroup.SourceEnds) ||
            !fieldSignature.SourceEnds.Equals(certificateAuthority.SourceEnds))
        {
            throw new ArgumentException(
                "The declaring-TypeDef binding proof must use the FieldSig's exact metadata source ends.",
                nameof(declaringTypeBindings));
        }
        if (!declaringTypeCompatibility.FieldDefinitionTokens.Contains(
                fieldSignature.FieldDefinition.FieldDefinitionToken))
        {
            throw new ArgumentException(
                "The FieldDef token must belong to the certificate's authority-issued declaring TypeDef.",
                nameof(fieldSignature));
        }
        return new MetadataTypeSubstitutionRequest(
            fieldSignature,
            declaringTypeCompatibility,
            declaringTypeBindings);
    }

    /// <summary>Tests canonical equality between two row-addressed draft substitution requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSubstitutionRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSubstitutionRequest);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataGenericParameterAuthorityBindingIdentity? FindOwner(int index) =>
        BindingProofResultKind == MetadataGenericParameterProofResultKind.Exact &&
        index >= 0 && index < exactOwnerBindings.Length && exactOwnerBindings[index].Parameter.Number == index
            ? exactOwnerBindings[index]
            : null;
}

/// <summary>Freezes one node in the complete recursively substituted signature tree.</summary>
/// <remarks>
/// This sealed draft identity retains the original syntax node, every recursively processed child, and the exact
/// GenericParam-row binding applied at a VAR or MVAR leaf. A closed replacement is present only for an exact binding.
/// </remarks>
public sealed class MetadataSubstitutedTypeNodeIdentity : IEquatable<MetadataSubstitutedTypeNodeIdentity>
{
    private const string CanonicalDomain = "metadata-v2-substituted-type-node-identity";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<MetadataSubstitutedTypeNodeIdentity> children;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataSubstitutedTypeNodeIdentity(
        MetadataTypeSignatureNode sourceNode,
        MetadataGenericParameterAuthorityBindingIdentity? appliedBinding,
        MetadataClosedTypeIdentity? replacement,
        ImmutableArray<MetadataSubstitutedTypeNodeIdentity> children)
    {
        SourceNode = sourceNode;
        AppliedBinding = appliedBinding;
        Replacement = replacement;
        this.children = children;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceNode.CanonicalBytes.AsSpan());
        writer.WriteBoolean(appliedBinding is not null);
        if (appliedBinding is not null)
        {
            writer.WriteLengthPrefixedBytes(appliedBinding.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(replacement is not null);
        if (replacement is not null)
        {
            writer.WriteLengthPrefixedBytes(replacement.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, children, static child => child.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact original pre-substitution syntax node at this position.</summary>
    public MetadataTypeSignatureNode SourceNode { get; }
    /// <summary>Gets the exact row-addressed binding applied at a VAR or MVAR leaf, otherwise null.</summary>
    public MetadataGenericParameterAuthorityBindingIdentity? AppliedBinding { get; }
    /// <summary>Gets the exact closed replacement only when <see cref="AppliedBinding"/> is exact.</summary>
    public MetadataClosedTypeIdentity? Replacement { get; }
    /// <summary>Gets a defensive copy of all recursively substituted children in source order.</summary>
    public ImmutableArray<MetadataSubstitutedTypeNodeIdentity> Children =>
        ExpressionV2ContractEncoding.Copy(children);
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two recursively substituted draft nodes.</summary>
    /// <param name="other">The other draft node.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataSubstitutedTypeNodeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataSubstitutedTypeNodeIdentity);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataSubstitutedTypeNodeIdentity Create(
        MetadataTypeSignatureNode sourceNode,
        MetadataGenericParameterAuthorityBindingIdentity? appliedBinding,
        MetadataClosedTypeIdentity? replacement,
        ImmutableArray<MetadataSubstitutedTypeNodeIdentity> children)
    {
        ArgumentNullException.ThrowIfNull(sourceNode);
        var copiedChildren = ExpressionV2ContractEncoding.CopyRequired(
            children,
            nameof(children),
            StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount);
        var sourceChildren = sourceNode.Children;
        if (copiedChildren.Length != sourceChildren.Length ||
            copiedChildren.Where((child, index) => !child.SourceNode.Equals(sourceChildren[index])).Any())
        {
            throw new ArgumentException("Substituted children must exactly cover the source children in source order.", nameof(children));
        }
        var isVariable = sourceNode.Kind is MetadataTypeSignatureNodeKind.OwnerTypeParameter or
            MetadataTypeSignatureNodeKind.MethodTypeParameter;
        if (!isVariable && (appliedBinding is not null || replacement is not null))
        {
            throw new ArgumentException("Only a VAR or MVAR leaf can carry an applied binding.", nameof(appliedBinding));
        }
        if (isVariable && appliedBinding is not null)
        {
            var expectedOwnerKind = sourceNode.Kind == MetadataTypeSignatureNodeKind.OwnerTypeParameter
                ? MetadataGenericParameterOwnerKind.TypeDefinition
                : MetadataGenericParameterOwnerKind.MethodDefinition;
            if (appliedBinding.Parameter.Owner.Kind != expectedOwnerKind ||
                appliedBinding.Parameter.Number != sourceNode.VariableIndex)
            {
                throw new ArgumentException("The applied GenericParam row must exactly address the source variable.", nameof(appliedBinding));
            }
        }
        if ((replacement is not null) !=
            (appliedBinding?.Kind == MetadataGenericParameterAuthorityBindingKind.Exact) ||
            replacement is not null && !replacement.Equals(appliedBinding!.Argument))
        {
            throw new ArgumentException("A replacement must exactly equal the argument in an exact applied binding.", nameof(replacement));
        }
        return new MetadataSubstitutedTypeNodeIdentity(sourceNode, appliedBinding, replacement, copiedChildren);
    }
}

/// <summary>Freezes one complete typed result of source-anchored FieldSig owner-VAR substitution.</summary>
/// <remarks>
/// The sealed draft result retains the exact FieldSig request, recursively substituted tree, distinct unavailable and
/// undeclared indices, and an exact closed identity only when the complete declaring-TypeDef proof and every recursive
/// node are admitted. A non-exact or invalid proof applies no binding prefix.
/// </remarks>
public sealed class MetadataTypeSubstitutionResult : IEquatable<MetadataTypeSubstitutionResult>
{
    private const string CanonicalDomain = "metadata-v2-type-substitution-result";
    private const int CanonicalSchemaVersion = 4;
    private readonly ImmutableArray<int> unresolvedOwnerVariableIndices;
    private readonly ImmutableArray<int> unresolvedMethodVariableIndices;
    private readonly ImmutableArray<int> undeclaredOwnerVariableIndices;
    private readonly ImmutableArray<int> undeclaredMethodVariableIndices;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeSubstitutionResult(
        MetadataTypeSubstitutionResultKind kind,
        MetadataTypeSubstitutionRequest request,
        MetadataSubstitutedTypeNodeIdentity substitutedTree,
        ImmutableArray<int> unresolvedOwnerVariableIndices,
        ImmutableArray<int> unresolvedMethodVariableIndices,
        ImmutableArray<int> undeclaredOwnerVariableIndices,
        ImmutableArray<int> undeclaredMethodVariableIndices,
        MetadataClosedTypeIdentity? closedType,
        EvaluationDeterministicBound? reachedBound)
    {
        Kind = kind;
        Root = request.Root;
        Request = request;
        SubstitutedTree = substitutedTree;
        this.unresolvedOwnerVariableIndices = unresolvedOwnerVariableIndices;
        this.unresolvedMethodVariableIndices = unresolvedMethodVariableIndices;
        this.undeclaredOwnerVariableIndices = undeclaredOwnerVariableIndices;
        this.undeclaredMethodVariableIndices = undeclaredMethodVariableIndices;
        ClosedType = closedType;
        ReachedBound = reachedBound;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteLengthPrefixedBytes(Root.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(request.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(substitutedTree.CanonicalBytes.AsSpan());
        WriteInt32Array(writer, unresolvedOwnerVariableIndices);
        WriteInt32Array(writer, unresolvedMethodVariableIndices);
        WriteInt32Array(writer, undeclaredOwnerVariableIndices);
        WriteInt32Array(writer, undeclaredMethodVariableIndices);
        writer.WriteBoolean(closedType is not null);
        if (closedType is not null)
        {
            writer.WriteLengthPrefixedBytes(closedType.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(reachedBound is not null);
        if (reachedBound is not null)
        {
            writer.WriteString(reachedBound.Name);
            writer.WriteInt64(reachedBound.Value);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact typed recursive-substitution result kind.</summary>
    public MetadataTypeSubstitutionResultKind Kind { get; }
    /// <summary>Gets the complete original pre-substitution signature tree.</summary>
    public MetadataTypeSignatureNode Root { get; }
    /// <summary>Gets the explicit immutable draft row-addressed substitution request.</summary>
    public MetadataTypeSubstitutionRequest Request { get; }
    /// <summary>Gets whether the complete declaring-TypeDef binding proof is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterProofResultKind BindingProofResultKind => Request.BindingProofResultKind;
    /// <summary>Gets the typed declaring-TypeDef binding-proof issue, or none.</summary>
    public MetadataGenericParameterProofIssue BindingProofIssue => Request.BindingProofIssue;
    /// <summary>Gets the binding proof's reached bound even when FieldSig invalidity determines the result kind.</summary>
    public EvaluationDeterministicBound? BindingProofReachedBound => Request.ReachedBound;
    /// <summary>Gets the binding proof's cap-plus-one or incomplete observation count, otherwise zero.</summary>
    public int BindingProofObservedCount => Request.BindingProofObservedCount;
    /// <summary>Gets the complete recursively substituted tree, including every applied row binding.</summary>
    public MetadataSubstitutedTypeNodeIdentity SubstitutedTree { get; }
    /// <summary>Gets a defensive sorted set of owner-VAR indices whose declared bindings were unavailable.</summary>
    public ImmutableArray<int> UnresolvedOwnerVariableIndices =>
        ExpressionV2ContractEncoding.Copy(unresolvedOwnerVariableIndices);
    /// <summary>Gets a defensive sorted set of method-MVAR indices whose declared bindings were unavailable.</summary>
    public ImmutableArray<int> UnresolvedMethodVariableIndices =>
        ExpressionV2ContractEncoding.Copy(unresolvedMethodVariableIndices);
    /// <summary>Gets a defensive sorted set of owner-VAR indices that name no declared row in the ledger.</summary>
    public ImmutableArray<int> UndeclaredOwnerVariableIndices =>
        ExpressionV2ContractEncoding.Copy(undeclaredOwnerVariableIndices);
    /// <summary>Gets a defensive sorted set of method-MVAR indices that name no declared row or are forbidden by context.</summary>
    public ImmutableArray<int> UndeclaredMethodVariableIndices =>
        ExpressionV2ContractEncoding.Copy(undeclaredMethodVariableIndices);
    /// <summary>Gets the exact recursively constructed closed type only for an exact result.</summary>
    public MetadataClosedTypeIdentity? ClosedType { get; }
    /// <summary>Gets the reached bound when that, rather than provisional classification, caused NonExact.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }
    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Recursively substitutes the exact row-addressed owner VAR bindings in one exact FieldSig request.</summary>
    /// <param name="request">
    /// The source-anchored FieldSig and complete declaring-TypeDef binding proof, including an explicit exact empty
    /// ledger for a non-generic owner.
    /// </param>
    /// <returns>
    /// A sealed immutable typed draft result. Non-exact or invalid binding proofs stop before any binding is applied
    /// and cannot produce a closed result.
    /// </returns>
    public static MetadataTypeSubstitutionResult Substitute(MetadataTypeSubstitutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var root = request.Root;
        var unresolvedOwner = new SortedSet<int>();
        var unresolvedMethod = new SortedSet<int>();
        var undeclaredOwner = new SortedSet<int>();
        var undeclaredMethod = new SortedSet<int>();

        SubstitutionEvaluation evaluation;
        var exactDeclaredArity = request.DeclaringTypeBindings.OwnerGroup.DeclaredArity;
        var fieldContextInvalid = CollectFieldContextInvalidity(
            root,
            exactDeclaredArity,
            undeclaredOwner,
            undeclaredMethod);
        if (fieldContextInvalid)
        {
            evaluation = SubstitutionEvaluation.Invalid(CloneWithoutBindings(root));
        }
        else if (request.BindingProofResultKind == MetadataGenericParameterProofResultKind.NonExact)
        {
            evaluation = SubstitutionEvaluation.NonExact(
                CloneWithoutBindings(root),
                request.ReachedBound);
        }
        else if (request.BindingProofResultKind == MetadataGenericParameterProofResultKind.Invalid)
        {
            evaluation = SubstitutionEvaluation.Invalid(CloneWithoutBindings(root));
        }
        else
        {
            evaluation = Evaluate(
                root,
                request,
                unresolvedOwner,
                unresolvedMethod,
                undeclaredOwner,
                undeclaredMethod);
        }

        var unresolvedOwnerIndices = unresolvedOwner.ToImmutableArray();
        var unresolvedMethodIndices = unresolvedMethod.ToImmutableArray();
        var undeclaredOwnerIndices = undeclaredOwner.ToImmutableArray();
        var undeclaredMethodIndices = undeclaredMethod.ToImmutableArray();
        if (evaluation.Kind == MetadataTypeSubstitutionResultKind.Exact &&
            (!unresolvedOwnerIndices.IsEmpty || !unresolvedMethodIndices.IsEmpty ||
             !undeclaredOwnerIndices.IsEmpty || !undeclaredMethodIndices.IsEmpty ||
             evaluation.ClosedType is null || evaluation.ReachedBound is not null))
        {
            throw new InvalidOperationException("An exact substitution must have one closed result and no unresolved evidence.");
        }
        if (evaluation.Kind != MetadataTypeSubstitutionResultKind.Exact && evaluation.ClosedType is not null)
        {
            throw new InvalidOperationException("A non-exact substitution cannot carry a closed result.");
        }
        if (evaluation.ReachedBound is not null &&
            evaluation.Kind != MetadataTypeSubstitutionResultKind.NonExact)
        {
            throw new InvalidOperationException("Only a non-exact substitution carries one reached operation bound.");
        }
        return new MetadataTypeSubstitutionResult(
            evaluation.Kind,
            request,
            evaluation.Tree,
            unresolvedOwnerIndices,
            unresolvedMethodIndices,
            undeclaredOwnerIndices,
            undeclaredMethodIndices,
            evaluation.ClosedType,
            evaluation.ReachedBound);
    }

    /// <summary>Tests canonical equality between two recursive draft substitution results.</summary>
    /// <param name="other">The other draft result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataTypeSubstitutionResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only when the object has identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeSubstitutionResult);
    /// <summary>Computes a hash code from the immutable draft canonical bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static bool CollectFieldContextInvalidity(
        MetadataTypeSignatureNode node,
        int? exactDeclaredArity,
        SortedSet<int> undeclaredOwner,
        SortedSet<int> undeclaredMethod)
    {
        var invalid = node.Kind == MetadataTypeSignatureNodeKind.TypeSpecificationIndirection;
        if (node.Kind == MetadataTypeSignatureNodeKind.MethodTypeParameter &&
            node.VariableIndex is { } methodIndex)
        {
            undeclaredMethod.Add(methodIndex);
            invalid = true;
        }
        else if (node.Kind == MetadataTypeSignatureNodeKind.OwnerTypeParameter &&
                 exactDeclaredArity is { } declaredArity &&
                 node.VariableIndex is { } ownerIndex &&
                 ownerIndex >= declaredArity)
        {
            undeclaredOwner.Add(ownerIndex);
            invalid = true;
        }
        foreach (var child in node.Children)
        {
            if (CollectFieldContextInvalidity(
                    child,
                    exactDeclaredArity,
                    undeclaredOwner,
                    undeclaredMethod))
            {
                invalid = true;
            }
        }
        return invalid;
    }

    private static SubstitutionEvaluation Evaluate(
        MetadataTypeSignatureNode node,
        MetadataTypeSubstitutionRequest request,
        SortedSet<int> unresolvedOwner,
        SortedSet<int> unresolvedMethod,
        SortedSet<int> undeclaredOwner,
        SortedSet<int> undeclaredMethod)
    {
        var childEvaluations = node.Children
            .Select(child => Evaluate(
                child,
                request,
                unresolvedOwner,
                unresolvedMethod,
                undeclaredOwner,
                undeclaredMethod))
            .ToImmutableArray();
        var childTrees = childEvaluations.Select(static child => child.Tree).ToImmutableArray();
        var tree = MetadataSubstitutedTypeNodeIdentity.Create(node, null, null, childTrees);
        var childDisposition = Merge(childEvaluations.Select(static child => child.Kind));
        var childBound = childEvaluations
            .Where(static child => child.ReachedBound is not null)
            .Select(static child => child.ReachedBound)
            .FirstOrDefault();

        switch (node.Kind)
        {
            case MetadataTypeSignatureNodeKind.OwnerTypeParameter:
                return EvaluateVariable(
                    node,
                    request.FindOwner(node.VariableIndex!.Value),
                    unresolvedOwner,
                    undeclaredOwner);
            case MetadataTypeSignatureNodeKind.MethodTypeParameter:
                undeclaredMethod.Add(node.VariableIndex!.Value);
                return SubstitutionEvaluation.Invalid(tree);
            case MetadataTypeSignatureNodeKind.TypeSpecificationIndirection:
                return SubstitutionEvaluation.Invalid(tree);
            case MetadataTypeSignatureNodeKind.RequiredModifier:
            case MetadataTypeSignatureNodeKind.OptionalModifier:
            case MetadataTypeSignatureNodeKind.Pointer:
            case MetadataTypeSignatureNodeKind.ByReference:
            case MetadataTypeSignatureNodeKind.FunctionPointer:
            case MetadataTypeSignatureNodeKind.Void:
            case MetadataTypeSignatureNodeKind.TypedByReference:
                return WithOuterDisposition(
                    MetadataTypeSubstitutionResultKind.Unsupported,
                    tree,
                    childDisposition,
                    childBound);
            case MetadataTypeSignatureNodeKind.Primitive:
                return SubstitutionEvaluation.Exact(
                    tree,
                    MetadataClosedTypeIdentity.Primitive(node.PrimitiveKind!.Value));
            case MetadataTypeSignatureNodeKind.Named:
                if (node.ContainsNonExactGenericMapping)
                {
                    return SubstitutionEvaluation.Unsupported(tree);
                }
                if (node.NamedDefinition!.FlattenedArity != 0)
                {
                    return SubstitutionEvaluation.Open(tree);
                }
                if (node.ContainsProvisionalTypeDefinitionClassification)
                {
                    return SubstitutionEvaluation.NonExact(tree, bound: null);
                }
                return ProjectClosed(tree, () => MetadataClosedTypeIdentity.FromGroundSignature(node));
            case MetadataTypeSignatureNodeKind.GenericInstantiation:
                if (node.ContainsNonExactGenericMapping)
                {
                    return WithOuterDisposition(
                        MetadataTypeSubstitutionResultKind.Unsupported,
                        tree,
                        childDisposition,
                        childBound);
                }
                if (node.NamedDefinitionChain.Any(static definition =>
                        definition.ClassificationStatus == MetadataTypeDefinitionClassificationStatus.Provisional))
                {
                    return WithOuterDisposition(
                        MetadataTypeSubstitutionResultKind.NonExact,
                        tree,
                        childDisposition,
                        childBound);
                }
                if (childDisposition != MetadataTypeSubstitutionResultKind.Exact)
                {
                    return FromDisposition(childDisposition, tree, childBound);
                }
                return ProjectClosed(
                    tree,
                    () => MetadataClosedTypeIdentity.ConstructNamed(
                        node.NamedDefinitionChain,
                        childEvaluations.Select(static child => child.ClosedType!).ToImmutableArray()));
            case MetadataTypeSignatureNodeKind.SzArray:
            case MetadataTypeSignatureNodeKind.MultidimensionalArray:
                if (childDisposition != MetadataTypeSubstitutionResultKind.Exact)
                {
                    return FromDisposition(childDisposition, tree, childBound);
                }
                return ProjectClosed(
                    tree,
                    () => node.Kind == MetadataTypeSignatureNodeKind.SzArray
                        ? MetadataClosedTypeIdentity.SzArray(childEvaluations[0].ClosedType!)
                        : MetadataClosedTypeIdentity.MultidimensionalArray(
                            childEvaluations[0].ClosedType!,
                            node.ArrayRank!.Value,
                            node.ArraySizes,
                            node.ArrayLowerBounds));
            default:
                throw new ArgumentOutOfRangeException(nameof(node));
        }
    }

    private static SubstitutionEvaluation EvaluateVariable(
        MetadataTypeSignatureNode node,
        MetadataGenericParameterAuthorityBindingIdentity? binding,
        SortedSet<int> unresolved,
        SortedSet<int> undeclared)
    {
        var index = node.VariableIndex!.Value;
        if (binding is null)
        {
            undeclared.Add(index);
            return SubstitutionEvaluation.Invalid(
                MetadataSubstitutedTypeNodeIdentity.Create(
                    node,
                    null,
                    null,
                    ImmutableArray<MetadataSubstitutedTypeNodeIdentity>.Empty));
        }
        if (binding.Kind == MetadataGenericParameterAuthorityBindingKind.Unavailable)
        {
            unresolved.Add(index);
            return SubstitutionEvaluation.Open(
                MetadataSubstitutedTypeNodeIdentity.Create(
                    node,
                    binding,
                    null,
                    ImmutableArray<MetadataSubstitutedTypeNodeIdentity>.Empty));
        }
        return SubstitutionEvaluation.Exact(
            MetadataSubstitutedTypeNodeIdentity.Create(
                node,
                binding,
                binding.Argument,
                ImmutableArray<MetadataSubstitutedTypeNodeIdentity>.Empty),
            binding.Argument!);
    }

    private static SubstitutionEvaluation ProjectClosed(
        MetadataSubstitutedTypeNodeIdentity tree,
        Func<MetadataClosedTypeIdentity> factory)
    {
        try
        {
            return SubstitutionEvaluation.Exact(tree, factory());
        }
        catch (MetadataClosedTypeBoundException exception)
        {
            return SubstitutionEvaluation.NonExact(tree, exception.Bound);
        }
        catch (ArgumentException)
        {
            return SubstitutionEvaluation.Invalid(tree);
        }
        catch (OverflowException)
        {
            return SubstitutionEvaluation.Invalid(tree);
        }
    }

    private static SubstitutionEvaluation WithOuterDisposition(
        MetadataTypeSubstitutionResultKind outer,
        MetadataSubstitutedTypeNodeIdentity tree,
        MetadataTypeSubstitutionResultKind children,
        EvaluationDeterministicBound? childBound)
    {
        var merged = Rank(outer) >= Rank(children) ? outer : children;
        return FromDisposition(merged, tree, childBound);
    }

    private static SubstitutionEvaluation FromDisposition(
        MetadataTypeSubstitutionResultKind kind,
        MetadataSubstitutedTypeNodeIdentity tree,
        EvaluationDeterministicBound? bound) => kind switch
        {
            MetadataTypeSubstitutionResultKind.Exact =>
                throw new InvalidOperationException("Exact projection requires one closed type."),
            MetadataTypeSubstitutionResultKind.Open => SubstitutionEvaluation.Open(tree),
            MetadataTypeSubstitutionResultKind.Unsupported => SubstitutionEvaluation.Unsupported(tree),
            MetadataTypeSubstitutionResultKind.Invalid => SubstitutionEvaluation.Invalid(tree),
            MetadataTypeSubstitutionResultKind.NonExact => SubstitutionEvaluation.NonExact(tree, bound),
            _ => throw new InvalidOperationException("The child disposition is outside the closed draft set."),
        };

    private static MetadataTypeSubstitutionResultKind Merge(
        IEnumerable<MetadataTypeSubstitutionResultKind> kinds)
    {
        var result = MetadataTypeSubstitutionResultKind.Exact;
        foreach (var kind in kinds)
        {
            if (Rank(kind) > Rank(result))
            {
                result = kind;
            }
        }
        return result;
    }

    private static int Rank(MetadataTypeSubstitutionResultKind kind) => kind switch
    {
        MetadataTypeSubstitutionResultKind.Exact => 0,
        MetadataTypeSubstitutionResultKind.Open => 1,
        MetadataTypeSubstitutionResultKind.Unsupported => 2,
        MetadataTypeSubstitutionResultKind.NonExact => 3,
        MetadataTypeSubstitutionResultKind.Invalid => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static MetadataSubstitutedTypeNodeIdentity CloneWithoutBindings(MetadataTypeSignatureNode node) =>
        MetadataSubstitutedTypeNodeIdentity.Create(
            node,
            null,
            null,
            node.Children.Select(CloneWithoutBindings).ToImmutableArray());

    private static void WriteInt32Array(CanonicalReplayEncoding.Writer writer, ImmutableArray<int> values)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteInt32(value);
        }
    }

    private sealed class SubstitutionEvaluation
    {
        private SubstitutionEvaluation(
            MetadataTypeSubstitutionResultKind kind,
            MetadataSubstitutedTypeNodeIdentity tree,
            MetadataClosedTypeIdentity? closedType,
            EvaluationDeterministicBound? reachedBound)
        {
            Kind = kind;
            Tree = tree;
            ClosedType = closedType;
            ReachedBound = reachedBound;
        }

        internal MetadataTypeSubstitutionResultKind Kind { get; }
        internal MetadataSubstitutedTypeNodeIdentity Tree { get; }
        internal MetadataClosedTypeIdentity? ClosedType { get; }
        internal EvaluationDeterministicBound? ReachedBound { get; }

        internal static SubstitutionEvaluation Exact(
            MetadataSubstitutedTypeNodeIdentity tree,
            MetadataClosedTypeIdentity value) =>
            new(MetadataTypeSubstitutionResultKind.Exact, tree, value, null);
        internal static SubstitutionEvaluation Open(MetadataSubstitutedTypeNodeIdentity tree) =>
            new(MetadataTypeSubstitutionResultKind.Open, tree, null, null);
        internal static SubstitutionEvaluation Unsupported(MetadataSubstitutedTypeNodeIdentity tree) =>
            new(MetadataTypeSubstitutionResultKind.Unsupported, tree, null, null);
        internal static SubstitutionEvaluation Invalid(MetadataSubstitutedTypeNodeIdentity tree) =>
            new(MetadataTypeSubstitutionResultKind.Invalid, tree, null, null);
        internal static SubstitutionEvaluation NonExact(
            MetadataSubstitutedTypeNodeIdentity tree,
            EvaluationDeterministicBound? bound) =>
            new(MetadataTypeSubstitutionResultKind.NonExact, tree, null, bound);
    }
}
