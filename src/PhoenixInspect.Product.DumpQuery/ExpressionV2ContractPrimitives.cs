using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Declares the operation-derived limits shared by the additive W8 expression contracts.</summary>
/// <remarks>
/// These values are limits, not compatibility promises. Each name identifies the physical operation
/// being counted so a later implementation can report cap-plus-one evidence without silently retaining a prefix.
/// </remarks>
public static class ExpressionV2ContractLimits
{
    /// <summary>Gets the maximum complete expression character count.</summary>
    public const int MaximumExpressionCharacterCount = CSharpExpressionFrontEnd.MaximumExpressionLength;

    /// <summary>Gets the maximum cumulative syntax node/token count.</summary>
    public const int MaximumSyntaxNodeTokenCount = CSharpExpressionFrontEnd.MaximumNodeTokenCount;

    /// <summary>Gets the maximum complete syntax-tree depth.</summary>
    public const int MaximumSyntaxDepth = CSharpExpressionFrontEnd.MaximumSyntaxDepth;

    /// <summary>Gets the maximum decoded identifier character count.</summary>
    public const int MaximumIdentifierCharacterCount = CSharpExpressionFrontEnd.MaximumIdentifierLength;

    /// <summary>Gets the maximum decoded suffix-fallback string character count.</summary>
    public const int MaximumFallbackStringCharacterCount = CSharpExpressionFrontEnd.MaximumStringLiteralLength;

    /// <summary>Gets the maximum number of qualified expression-name segments.</summary>
    public const int MaximumExpressionSegmentCount = 32;

    /// <summary>Gets the maximum enclosing TypeDef depth.</summary>
    public const int MaximumNestedTypeDefinitionDepth = 16;

    /// <summary>Gets the maximum TypeRef resolution-chain length.</summary>
    public const int MaximumTypeReferenceResolutionDepth = 16;

    /// <summary>Gets the maximum number of bytes in one retained TypeSpec or field-type signature.</summary>
    public const int MaximumTypeSpecificationByteCount = 256;

    /// <summary>Gets the maximum recursive signature depth.</summary>
    public const int MaximumTypeSpecificationDepth = 32;

    /// <summary>Gets the maximum cumulative generic-argument count in one projected construction.</summary>
    public const int MaximumTypeSpecificationArgumentCount = 64;

    /// <summary>Gets the maximum cumulative raw signature-node count before closed projection.</summary>
    public const int MaximumRawTypeSignatureNodeCount = 256;

    /// <summary>Gets the maximum cumulative TypeSpec-row count followed through one resolution graph.</summary>
    public const int MaximumTypeSpecificationRowTraversalCount = 64;

    /// <summary>Gets the maximum token-resolution fact count retained by one metadata signature catalog.</summary>
    public const int MaximumSignatureTokenResolutionCount = 256;

    /// <summary>Gets the maximum closed-type topology depth.</summary>
    public const int MaximumClosedTypeTopologyDepth = 16;

    /// <summary>Gets the maximum cumulative node count in one closed-type topology.</summary>
    public const int MaximumClosedTypeTopologyNodeCount = 128;

    /// <summary>Gets the maximum complete syntax-partition count.</summary>
    /// <remarks>
    /// The cap is 63 because the real three-field-position derivation reaches exactly 63 partitions at 23
    /// simple segments; a 24th segment truthfully exposes the first over-cap partition without retaining a prefix.
    /// </remarks>
    public const int MaximumSyntaxPartitionCount = 63;

    /// <summary>Gets the maximum namespace/type candidate-occurrence count.</summary>
    public const int MaximumNameCandidateOccurrenceCount = 4_096;

    /// <summary>Gets the maximum grouped physical-candidate count.</summary>
    public const int MaximumGroupedPhysicalCandidateCount = 4_096;

    /// <summary>Gets the maximum semantic namespace-declaration level count.</summary>
    public const int MaximumNamespaceLevelCount = 32;

    /// <summary>Gets the maximum constructed ancestry depth.</summary>
    public const int MaximumConstructedAncestryDepth = 64;

    /// <summary>Gets the maximum module count examined by one binding operation.</summary>
    public const int MaximumModuleCount = 4_096;

    /// <summary>Gets the maximum TypeDef-row count examined in one metadata table.</summary>
    public const int MaximumTypeDefinitionRowCount = 65_536;

    /// <summary>Gets the maximum NestedClass-row count examined in one complete metadata table.</summary>
    public const int MaximumNestedClassRowCount = 65_536;

    /// <summary>Gets the maximum Field-row count examined in one metadata table.</summary>
    public const int MaximumFieldDefinitionRowCount = 65_536;

    /// <summary>Gets the maximum MethodDef-row count examined in one complete metadata table.</summary>
    public const int MaximumMethodDefinitionRowCount = 65_536;

    /// <summary>Gets the maximum Constant-row count examined in one complete metadata table.</summary>
    public const int MaximumConstantRowCount = 65_536;

    /// <summary>Gets the maximum FieldPtr-row count examined in one complete metadata table.</summary>
    public const int MaximumFieldPointerRowCount = 65_536;

    /// <summary>Gets the maximum MethodPtr-row count examined in one complete metadata table.</summary>
    public const int MaximumMethodPointerRowCount = 65_536;

    /// <summary>Gets the maximum InterfaceImpl-row count examined in one metadata table.</summary>
    public const int MaximumInterfaceImplementationRowCount = 4_096;

    /// <summary>Gets the maximum GenericParam-row count examined in one complete metadata table.</summary>
    public const int MaximumGenericParameterRowCount = 65_536;

    /// <summary>Gets the maximum GenericParamConstraint-row count examined in one complete metadata table.</summary>
    public const int MaximumGenericParameterConstraintRowCount = 65_536;

    /// <summary>Gets the maximum runtime-construction count examined for one TypeDef.</summary>
    public const int MaximumRuntimeConstructionCount = 4_096;

    /// <summary>Gets the maximum directly declared field count examined on one construction.</summary>
    public const int MaximumFieldCountPerConstruction = 256;

    /// <summary>Gets the maximum active import-scope depth.</summary>
    public const int MaximumImportScopeDepth = 32;

    /// <summary>Gets the maximum cumulative import-fact count.</summary>
    public const int MaximumImportFactCount = 128;

    /// <summary>Gets the independent maximum parameter count.</summary>
    public const int MaximumParameterCount = 256;

    /// <summary>Gets the independent maximum local-variable count.</summary>
    public const int MaximumLocalCount = 256;

    /// <summary>Gets the independent maximum local-constant count.</summary>
    public const int MaximumLocalConstantCount = 256;

    /// <summary>Gets the independent maximum lexical-blocker count.</summary>
    public const int MaximumLexicalBlockerCount = 256;

    /// <summary>Gets the maximum generated local-function association count.</summary>
    public const int MaximumLocalFunctionAssociationCount = 256;

    /// <summary>Gets the maximum directly declared property count.</summary>
    public const int MaximumDeclaredPropertyCount = 256;

    /// <summary>Gets the maximum directly declared event count.</summary>
    public const int MaximumDeclaredEventCount = 256;

    /// <summary>Gets the maximum directly declared method count.</summary>
    public const int MaximumDeclaredMethodCount = 1_024;

    /// <summary>Gets the maximum directly declared nested-type count.</summary>
    public const int MaximumDeclaredNestedTypeCount = 256;

    /// <summary>Gets the maximum default-constructor search count.</summary>
    public const int MaximumDefaultConstructorSearchCount = 256;

    /// <summary>Gets the maximum friend-assembly declaration count.</summary>
    public const int MaximumFriendAssemblyDeclarationCount = 256;

    /// <summary>Gets the maximum accessibility-check count.</summary>
    public const int MaximumAccessibilityCheckCount = 256;

    /// <summary>Gets the maximum GenericParam-row count retained for one construction.</summary>
    public const int MaximumGenericParameterCount = 64;

    /// <summary>Gets the maximum cumulative generic-constraint count.</summary>
    public const int MaximumGenericConstraintCount = 256;

    /// <summary>Gets the maximum custom-attribute count examined for one constrained construction.</summary>
    public const int MaximumConstraintAttributeCount = 256;

    /// <summary>Gets the maximum multidimensional-array rank.</summary>
    public const int MaximumArrayRank = 32;

    /// <summary>Gets the maximum thread count examined during selected-thread attribution.</summary>
    public const int MaximumFrameThreadCount = 4_096;

    /// <summary>Gets the maximum frame count examined on one thread.</summary>
    public const int MaximumFrameCountPerThread = 256;

    /// <summary>Gets the maximum copied byte width of one frame value.</summary>
    public const int MaximumFrameValueByteCount = 64;

    /// <summary>Gets the maximum decoded character count of one frame string.</summary>
    public const int MaximumFrameStringCharacterCount = 256;

    /// <summary>Gets the maximum decoded character count of one static-field string.</summary>
    public const int MaximumStaticStringCharacterCount = 1_024;

    /// <summary>Gets the maximum PE-section count examined for one module-RVA mapping.</summary>
    public const int MaximumPortableExecutableSectionCount = 128;

    /// <summary>Gets all shared limits in canonical bound-name order.</summary>
    /// <remarks>The returned array is a defensive copy and remains a W8 contract artifact.</remarks>
    public static ImmutableArray<EvaluationDeterministicBound> AllDeclaredBounds =>
        ImmutableArray.CreateRange(DeclaredBounds.OrderBy(static bound => bound.Name, StringComparer.Ordinal));

    internal const string ExpressionSegmentCountBoundName = "expression-v2.syntax.segments";
    internal const string ExpressionCharacterCountBoundName = "expression-v2.syntax.characters";
    internal const string SyntaxNodeTokenCountBoundName = "expression-v2.syntax.nodes-and-tokens";
    internal const string SyntaxDepthBoundName = "expression-v2.syntax.depth";
    internal const string IdentifierCharacterCountBoundName = "expression-v2.syntax.identifier-characters";
    internal const string FallbackStringCharacterCountBoundName = "expression-v2.syntax.fallback-string-characters";
    internal const string NestedTypeDefinitionDepthBoundName = "expression-v2.metadata.typedef-depth";
    internal const string TypeReferenceResolutionDepthBoundName = "expression-v2.metadata.typeref-depth";
    internal const string TypeSpecificationByteCountBoundName = "expression-v2.signature.bytes";
    internal const string TypeSpecificationDepthBoundName = "expression-v2.signature.depth";
    internal const string TypeSpecificationArgumentCountBoundName = "expression-v2.signature.arguments";
    internal const string RawTypeSignatureNodeCountBoundName = "expression-v2.signature.raw-nodes";
    internal const string TypeSpecificationRowTraversalCountBoundName = "expression-v2.signature.typespec-rows";
    internal const string SignatureTokenResolutionCountBoundName = "expression-v2.signature.token-resolutions";
    internal const string ClosedTypeTopologyDepthBoundName = "expression-v2.closed-type.depth";
    internal const string ClosedTypeTopologyNodeCountBoundName = "expression-v2.closed-type.nodes";
    internal const string SyntaxPartitionCountBoundName = "expression-v2.syntax.partitions";
    internal const string NameCandidateOccurrenceCountBoundName = "expression-v2.binding.candidate-occurrences";
    internal const string GroupedPhysicalCandidateCountBoundName = "expression-v2.binding.candidate-groups";
    internal const string NamespaceLevelCountBoundName = "expression-v2.binding.namespace-levels";
    internal const string ConstructedAncestryDepthBoundName = "expression-v2.metadata.ancestry-depth";
    internal const string ModuleCountBoundName = "expression-v2.metadata.modules";
    internal const string TypeDefinitionRowCountBoundName = "expression-v2.metadata.typedef-rows";
    internal const string NestedClassRowCountBoundName = "expression-v2.metadata.nestedclass-rows";
    internal const string FieldDefinitionRowCountBoundName = "expression-v2.metadata.field-rows";
    internal const string MethodDefinitionRowCountBoundName = "expression-v2.metadata.methoddef-rows";
    internal const string ConstantRowCountBoundName = "expression-v2.metadata.constant-rows";
    internal const string FieldPointerRowCountBoundName = "expression-v2.metadata.fieldptr-rows";
    internal const string MethodPointerRowCountBoundName = "expression-v2.metadata.methodptr-rows";
    internal const string InterfaceImplementationRowCountBoundName = "expression-v2.metadata.interfaceimpl-rows";
    internal const string GenericParameterRowCountBoundName = "expression-v2.metadata.genericparam-rows";
    internal const string GenericParameterConstraintRowCountBoundName =
        "expression-v2.metadata.genericparamconstraint-rows";
    internal const string RuntimeConstructionCountBoundName = "expression-v2.runtime.constructions";
    internal const string FieldCountPerConstructionBoundName = "expression-v2.metadata.fields-per-construction";
    internal const string ImportScopeDepthBoundName = "expression-v2.context.import-depth";
    internal const string ImportFactCountBoundName = "expression-v2.context.import-facts";
    internal const string ParameterCountBoundName = "expression-v2.context.parameters";
    internal const string LocalCountBoundName = "expression-v2.context.locals";
    internal const string LocalConstantCountBoundName = "expression-v2.context.constants";
    internal const string LexicalBlockerCountBoundName = "expression-v2.context.blockers";
    internal const string LocalFunctionAssociationCountBoundName = "expression-v2.context.local-function-associations";
    internal const string DeclaredPropertyCountBoundName = "expression-v2.members.declared-properties";
    internal const string DeclaredEventCountBoundName = "expression-v2.members.declared-events";
    internal const string DeclaredMethodCountBoundName = "expression-v2.members.declared-methods";
    internal const string DeclaredNestedTypeCountBoundName = "expression-v2.members.declared-nested-types";
    internal const string DefaultConstructorSearchCountBoundName = "expression-v2.constraints.default-constructors";
    internal const string FriendAssemblyDeclarationCountBoundName = "expression-v2.access.friend-assemblies";
    internal const string AccessibilityCheckCountBoundName = "expression-v2.access.checks";
    internal const string GenericParameterCountBoundName = "expression-v2.constraints.parameters";
    internal const string GenericConstraintCountBoundName = "expression-v2.constraints.entries";
    internal const string ConstraintAttributeCountBoundName = "expression-v2.constraints.attributes";
    internal const string ArrayRankBoundName = "expression-v2.closed-type.array-rank";
    internal const string FrameThreadCountBoundName = "expression-v2.frame.threads";
    internal const string FrameCountPerThreadBoundName = "expression-v2.frame.frames-per-thread";
    internal const string FrameValueByteCountBoundName = "expression-v2.frame.value-bytes";
    internal const string FrameStringCharacterCountBoundName = "expression-v2.frame.string-characters";
    internal const string StaticStringCharacterCountBoundName = "expression-v2.static.string-characters";
    internal const string PortableExecutableSectionCountBoundName = "expression-v2.module-rva.pe-sections";

    private static readonly ImmutableArray<EvaluationDeterministicBound> DeclaredBounds =
        ImmutableArray.Create(
            new EvaluationDeterministicBound(ArrayRankBoundName, MaximumArrayRank),
            new EvaluationDeterministicBound(AccessibilityCheckCountBoundName, MaximumAccessibilityCheckCount),
            new EvaluationDeterministicBound(ClosedTypeTopologyDepthBoundName, MaximumClosedTypeTopologyDepth),
            new EvaluationDeterministicBound(ClosedTypeTopologyNodeCountBoundName, MaximumClosedTypeTopologyNodeCount),
            new EvaluationDeterministicBound(ConstructedAncestryDepthBoundName, MaximumConstructedAncestryDepth),
            new EvaluationDeterministicBound(ConstraintAttributeCountBoundName, MaximumConstraintAttributeCount),
            new EvaluationDeterministicBound(DeclaredEventCountBoundName, MaximumDeclaredEventCount),
            new EvaluationDeterministicBound(DeclaredMethodCountBoundName, MaximumDeclaredMethodCount),
            new EvaluationDeterministicBound(DeclaredNestedTypeCountBoundName, MaximumDeclaredNestedTypeCount),
            new EvaluationDeterministicBound(DeclaredPropertyCountBoundName, MaximumDeclaredPropertyCount),
            new EvaluationDeterministicBound(DefaultConstructorSearchCountBoundName, MaximumDefaultConstructorSearchCount),
            new EvaluationDeterministicBound(FriendAssemblyDeclarationCountBoundName, MaximumFriendAssemblyDeclarationCount),
            new EvaluationDeterministicBound(GroupedPhysicalCandidateCountBoundName, MaximumGroupedPhysicalCandidateCount),
            new EvaluationDeterministicBound(IdentifierCharacterCountBoundName, MaximumIdentifierCharacterCount),
            new EvaluationDeterministicBound(ImportFactCountBoundName, MaximumImportFactCount),
            new EvaluationDeterministicBound(ImportScopeDepthBoundName, MaximumImportScopeDepth),
            new EvaluationDeterministicBound(InterfaceImplementationRowCountBoundName, MaximumInterfaceImplementationRowCount),
            new EvaluationDeterministicBound(LexicalBlockerCountBoundName, MaximumLexicalBlockerCount),
            new EvaluationDeterministicBound(LocalFunctionAssociationCountBoundName, MaximumLocalFunctionAssociationCount),
            new EvaluationDeterministicBound(LocalConstantCountBoundName, MaximumLocalConstantCount),
            new EvaluationDeterministicBound(LocalCountBoundName, MaximumLocalCount),
            new EvaluationDeterministicBound(ConstantRowCountBoundName, MaximumConstantRowCount),
            new EvaluationDeterministicBound(FieldDefinitionRowCountBoundName, MaximumFieldDefinitionRowCount),
            new EvaluationDeterministicBound(FieldPointerRowCountBoundName, MaximumFieldPointerRowCount),
            new EvaluationDeterministicBound(FieldCountPerConstructionBoundName, MaximumFieldCountPerConstruction),
            new EvaluationDeterministicBound(FrameCountPerThreadBoundName, MaximumFrameCountPerThread),
            new EvaluationDeterministicBound(FrameStringCharacterCountBoundName, MaximumFrameStringCharacterCount),
            new EvaluationDeterministicBound(FrameThreadCountBoundName, MaximumFrameThreadCount),
            new EvaluationDeterministicBound(FrameValueByteCountBoundName, MaximumFrameValueByteCount),
            new EvaluationDeterministicBound(FallbackStringCharacterCountBoundName, MaximumFallbackStringCharacterCount),
            new EvaluationDeterministicBound(GenericConstraintCountBoundName, MaximumGenericConstraintCount),
            new EvaluationDeterministicBound(GenericParameterCountBoundName, MaximumGenericParameterCount),
            new EvaluationDeterministicBound(
                GenericParameterConstraintRowCountBoundName,
                MaximumGenericParameterConstraintRowCount),
            new EvaluationDeterministicBound(GenericParameterRowCountBoundName, MaximumGenericParameterRowCount),
            new EvaluationDeterministicBound(ModuleCountBoundName, MaximumModuleCount),
            new EvaluationDeterministicBound(MethodDefinitionRowCountBoundName, MaximumMethodDefinitionRowCount),
            new EvaluationDeterministicBound(MethodPointerRowCountBoundName, MaximumMethodPointerRowCount),
            new EvaluationDeterministicBound(NameCandidateOccurrenceCountBoundName, MaximumNameCandidateOccurrenceCount),
            new EvaluationDeterministicBound(NamespaceLevelCountBoundName, MaximumNamespaceLevelCount),
            new EvaluationDeterministicBound(NestedTypeDefinitionDepthBoundName, MaximumNestedTypeDefinitionDepth),
            new EvaluationDeterministicBound(NestedClassRowCountBoundName, MaximumNestedClassRowCount),
            new EvaluationDeterministicBound(ParameterCountBoundName, MaximumParameterCount),
            new EvaluationDeterministicBound(PortableExecutableSectionCountBoundName, MaximumPortableExecutableSectionCount),
            new EvaluationDeterministicBound(RuntimeConstructionCountBoundName, MaximumRuntimeConstructionCount),
            new EvaluationDeterministicBound(StaticStringCharacterCountBoundName, MaximumStaticStringCharacterCount),
            new EvaluationDeterministicBound(ExpressionCharacterCountBoundName, MaximumExpressionCharacterCount),
            new EvaluationDeterministicBound(ExpressionSegmentCountBoundName, MaximumExpressionSegmentCount),
            new EvaluationDeterministicBound(SyntaxDepthBoundName, MaximumSyntaxDepth),
            new EvaluationDeterministicBound(SyntaxNodeTokenCountBoundName, MaximumSyntaxNodeTokenCount),
            new EvaluationDeterministicBound(SyntaxPartitionCountBoundName, MaximumSyntaxPartitionCount),
            new EvaluationDeterministicBound(TypeDefinitionRowCountBoundName, MaximumTypeDefinitionRowCount),
            new EvaluationDeterministicBound(TypeReferenceResolutionDepthBoundName, MaximumTypeReferenceResolutionDepth),
            new EvaluationDeterministicBound(TypeSpecificationArgumentCountBoundName, MaximumTypeSpecificationArgumentCount),
            new EvaluationDeterministicBound(TypeSpecificationByteCountBoundName, MaximumTypeSpecificationByteCount),
            new EvaluationDeterministicBound(TypeSpecificationDepthBoundName, MaximumTypeSpecificationDepth),
            new EvaluationDeterministicBound(RawTypeSignatureNodeCountBoundName, MaximumRawTypeSignatureNodeCount),
            new EvaluationDeterministicBound(SignatureTokenResolutionCountBoundName, MaximumSignatureTokenResolutionCount),
            new EvaluationDeterministicBound(TypeSpecificationRowTraversalCountBoundName, MaximumTypeSpecificationRowTraversalCount));
}

/// <summary>Exposes the limits that can constrain the additive <c>StaticFieldExpressionV2</c> profile.</summary>
/// <remarks>The aliases keep profile ownership explicit while sharing the single numeric declaration above.</remarks>
public static class StaticFieldV2Limits
{
    /// <summary>Gets the maximum complete expression character count.</summary>
    public const int MaximumExpressionCharacterCount = ExpressionV2ContractLimits.MaximumExpressionCharacterCount;

    /// <summary>Gets the maximum cumulative syntax node/token count.</summary>
    public const int MaximumSyntaxNodeTokenCount = ExpressionV2ContractLimits.MaximumSyntaxNodeTokenCount;

    /// <summary>Gets the maximum complete syntax-tree depth.</summary>
    public const int MaximumSyntaxDepth = ExpressionV2ContractLimits.MaximumSyntaxDepth;

    /// <summary>Gets the maximum decoded identifier character count.</summary>
    public const int MaximumIdentifierCharacterCount = ExpressionV2ContractLimits.MaximumIdentifierCharacterCount;

    /// <summary>Gets the maximum decoded suffix-fallback string character count.</summary>
    public const int MaximumFallbackStringCharacterCount = ExpressionV2ContractLimits.MaximumFallbackStringCharacterCount;

    /// <summary>Gets the maximum qualified expression-name segment count.</summary>
    public const int MaximumExpressionSegmentCount = ExpressionV2ContractLimits.MaximumExpressionSegmentCount;

    /// <summary>Gets the maximum complete syntax-partition count.</summary>
    public const int MaximumSyntaxPartitionCount = ExpressionV2ContractLimits.MaximumSyntaxPartitionCount;

    /// <summary>Gets the maximum namespace/type candidate-occurrence count.</summary>
    public const int MaximumNameCandidateOccurrenceCount = ExpressionV2ContractLimits.MaximumNameCandidateOccurrenceCount;

    /// <summary>Gets the maximum grouped physical-candidate count.</summary>
    public const int MaximumGroupedPhysicalCandidateCount = ExpressionV2ContractLimits.MaximumGroupedPhysicalCandidateCount;

    /// <summary>Gets the maximum semantic namespace-declaration level count.</summary>
    public const int MaximumNamespaceLevelCount = ExpressionV2ContractLimits.MaximumNamespaceLevelCount;

    /// <summary>Gets the maximum nested TypeDef depth.</summary>
    public const int MaximumNestedTypeDefinitionDepth = ExpressionV2ContractLimits.MaximumNestedTypeDefinitionDepth;

    /// <summary>Gets the maximum TypeRef resolution depth.</summary>
    public const int MaximumTypeReferenceResolutionDepth = ExpressionV2ContractLimits.MaximumTypeReferenceResolutionDepth;

    /// <summary>Gets the maximum retained TypeSpec byte count.</summary>
    public const int MaximumTypeSpecificationByteCount = ExpressionV2ContractLimits.MaximumTypeSpecificationByteCount;

    /// <summary>Gets the maximum signature-tree depth.</summary>
    public const int MaximumTypeSpecificationDepth = ExpressionV2ContractLimits.MaximumTypeSpecificationDepth;

    /// <summary>Gets the maximum cumulative generic-argument count.</summary>
    public const int MaximumTypeSpecificationArgumentCount = ExpressionV2ContractLimits.MaximumTypeSpecificationArgumentCount;

    /// <summary>Gets the maximum cumulative raw signature-node count.</summary>
    public const int MaximumRawTypeSignatureNodeCount = ExpressionV2ContractLimits.MaximumRawTypeSignatureNodeCount;

    /// <summary>Gets the maximum cumulative TypeSpec-row traversal count.</summary>
    public const int MaximumTypeSpecificationRowTraversalCount =
        ExpressionV2ContractLimits.MaximumTypeSpecificationRowTraversalCount;

    /// <summary>Gets the maximum metadata signature token-resolution fact count.</summary>
    public const int MaximumSignatureTokenResolutionCount =
        ExpressionV2ContractLimits.MaximumSignatureTokenResolutionCount;

    /// <summary>Gets the maximum closed-type topology depth.</summary>
    public const int MaximumClosedTypeTopologyDepth = ExpressionV2ContractLimits.MaximumClosedTypeTopologyDepth;

    /// <summary>Gets the maximum cumulative closed-type topology node count.</summary>
    public const int MaximumClosedTypeTopologyNodeCount = ExpressionV2ContractLimits.MaximumClosedTypeTopologyNodeCount;

    /// <summary>Gets the maximum multidimensional-array rank.</summary>
    public const int MaximumArrayRank = ExpressionV2ContractLimits.MaximumArrayRank;

    /// <summary>Gets the maximum constructed ancestry depth.</summary>
    public const int MaximumConstructedAncestryDepth = ExpressionV2ContractLimits.MaximumConstructedAncestryDepth;

    /// <summary>Gets the maximum module count examined by one binding operation.</summary>
    public const int MaximumModuleCount = ExpressionV2ContractLimits.MaximumModuleCount;

    /// <summary>Gets the maximum TypeDef-row count examined in one metadata table.</summary>
    public const int MaximumTypeDefinitionRowCount = ExpressionV2ContractLimits.MaximumTypeDefinitionRowCount;

    /// <summary>Gets the maximum NestedClass-row count examined in one complete metadata table.</summary>
    public const int MaximumNestedClassRowCount = ExpressionV2ContractLimits.MaximumNestedClassRowCount;

    /// <summary>Gets the maximum Field-row count examined in one metadata table.</summary>
    public const int MaximumFieldDefinitionRowCount = ExpressionV2ContractLimits.MaximumFieldDefinitionRowCount;

    /// <summary>Gets the maximum MethodDef-row count examined in one complete metadata table.</summary>
    public const int MaximumMethodDefinitionRowCount = ExpressionV2ContractLimits.MaximumMethodDefinitionRowCount;

    /// <summary>Gets the maximum Constant-row count examined in one complete metadata table.</summary>
    public const int MaximumConstantRowCount = ExpressionV2ContractLimits.MaximumConstantRowCount;

    /// <summary>Gets the maximum FieldPtr-row count examined in one complete metadata table.</summary>
    public const int MaximumFieldPointerRowCount = ExpressionV2ContractLimits.MaximumFieldPointerRowCount;

    /// <summary>Gets the maximum MethodPtr-row count examined in one complete metadata table.</summary>
    public const int MaximumMethodPointerRowCount = ExpressionV2ContractLimits.MaximumMethodPointerRowCount;

    /// <summary>Gets the maximum InterfaceImpl-row count examined in one metadata table.</summary>
    public const int MaximumInterfaceImplementationRowCount =
        ExpressionV2ContractLimits.MaximumInterfaceImplementationRowCount;

    /// <summary>Gets the maximum GenericParam-row count examined in one complete metadata table.</summary>
    public const int MaximumGenericParameterRowCount =
        ExpressionV2ContractLimits.MaximumGenericParameterRowCount;

    /// <summary>Gets the maximum GenericParamConstraint-row count examined in one complete metadata table.</summary>
    public const int MaximumGenericParameterConstraintRowCount =
        ExpressionV2ContractLimits.MaximumGenericParameterConstraintRowCount;

    /// <summary>Gets the maximum runtime-construction count.</summary>
    public const int MaximumRuntimeConstructionCount = ExpressionV2ContractLimits.MaximumRuntimeConstructionCount;

    /// <summary>Gets the maximum directly declared field count examined on one construction.</summary>
    public const int MaximumFieldCountPerConstruction = ExpressionV2ContractLimits.MaximumFieldCountPerConstruction;

    /// <summary>Gets the maximum active import-scope depth.</summary>
    public const int MaximumImportScopeDepth = ExpressionV2ContractLimits.MaximumImportScopeDepth;

    /// <summary>Gets the maximum cumulative import-fact count.</summary>
    public const int MaximumImportFactCount = ExpressionV2ContractLimits.MaximumImportFactCount;

    /// <summary>Gets the independently bounded parameter count.</summary>
    public const int MaximumParameterCount = ExpressionV2ContractLimits.MaximumParameterCount;

    /// <summary>Gets the independently bounded local-variable count.</summary>
    public const int MaximumLocalCount = ExpressionV2ContractLimits.MaximumLocalCount;

    /// <summary>Gets the independently bounded local-constant count.</summary>
    public const int MaximumLocalConstantCount = ExpressionV2ContractLimits.MaximumLocalConstantCount;

    /// <summary>Gets the independently bounded lexical-blocker count.</summary>
    public const int MaximumLexicalBlockerCount = ExpressionV2ContractLimits.MaximumLexicalBlockerCount;

    /// <summary>Gets the maximum generated local-function association count.</summary>
    public const int MaximumLocalFunctionAssociationCount =
        ExpressionV2ContractLimits.MaximumLocalFunctionAssociationCount;

    /// <summary>Gets the maximum directly declared property count.</summary>
    public const int MaximumDeclaredPropertyCount = ExpressionV2ContractLimits.MaximumDeclaredPropertyCount;

    /// <summary>Gets the maximum directly declared event count.</summary>
    public const int MaximumDeclaredEventCount = ExpressionV2ContractLimits.MaximumDeclaredEventCount;

    /// <summary>Gets the maximum directly declared method count.</summary>
    public const int MaximumDeclaredMethodCount = ExpressionV2ContractLimits.MaximumDeclaredMethodCount;

    /// <summary>Gets the maximum directly declared nested-type count.</summary>
    public const int MaximumDeclaredNestedTypeCount = ExpressionV2ContractLimits.MaximumDeclaredNestedTypeCount;

    /// <summary>Gets the maximum GenericParam-row count for one construction.</summary>
    public const int MaximumGenericParameterCount = ExpressionV2ContractLimits.MaximumGenericParameterCount;

    /// <summary>Gets the maximum cumulative generic-constraint count.</summary>
    public const int MaximumGenericConstraintCount = ExpressionV2ContractLimits.MaximumGenericConstraintCount;

    /// <summary>Gets the maximum constraint custom-attribute count.</summary>
    public const int MaximumConstraintAttributeCount = ExpressionV2ContractLimits.MaximumConstraintAttributeCount;

    /// <summary>Gets the maximum default-constructor search count.</summary>
    public const int MaximumDefaultConstructorSearchCount =
        ExpressionV2ContractLimits.MaximumDefaultConstructorSearchCount;

    /// <summary>Gets the maximum friend-assembly declaration count.</summary>
    public const int MaximumFriendAssemblyDeclarationCount =
        ExpressionV2ContractLimits.MaximumFriendAssemblyDeclarationCount;

    /// <summary>Gets the maximum accessibility-check count.</summary>
    public const int MaximumAccessibilityCheckCount = ExpressionV2ContractLimits.MaximumAccessibilityCheckCount;

    /// <summary>Gets the maximum PE-section count used for module-RVA mapping.</summary>
    public const int MaximumPortableExecutableSectionCount =
        ExpressionV2ContractLimits.MaximumPortableExecutableSectionCount;

    /// <summary>Gets the maximum selected-thread candidate count for thread-relative storage.</summary>
    public const int MaximumSelectedThreadCount = ExpressionV2ContractLimits.MaximumFrameThreadCount;

    /// <summary>Gets the maximum decoded static string character count.</summary>
    public const int MaximumStringCharacterCount = ExpressionV2ContractLimits.MaximumStaticStringCharacterCount;

    /// <summary>Gets every operation-derived limit available to this profile in canonical name order.</summary>
    /// <remarks>The defensive catalog is part of the profile and may change as its bounded operations evolve.</remarks>
    public static ImmutableArray<EvaluationDeterministicBound> AllDeclaredBounds =>
        ExpressionV2ContractLimits.AllDeclaredBounds
            .Where(static bound =>
                !bound.Name.StartsWith("expression-v2.frame.", StringComparison.Ordinal) ||
                bound.Name == ExpressionV2ContractLimits.FrameThreadCountBoundName)
            .ToImmutableArray();
}

/// <summary>Exposes the limits that can constrain the additive <c>FrameValueExpressionV1</c> profile.</summary>
/// <remarks>The frame profile is a distinct route and does not inherit static-field storage assumptions.</remarks>
public static class FrameValueV1Limits
{
    /// <summary>Gets the maximum complete expression character count.</summary>
    public const int MaximumExpressionCharacterCount = ExpressionV2ContractLimits.MaximumExpressionCharacterCount;

    /// <summary>Gets the maximum cumulative syntax node/token count.</summary>
    public const int MaximumSyntaxNodeTokenCount = ExpressionV2ContractLimits.MaximumSyntaxNodeTokenCount;

    /// <summary>Gets the maximum complete syntax-tree depth.</summary>
    public const int MaximumSyntaxDepth = ExpressionV2ContractLimits.MaximumSyntaxDepth;

    /// <summary>Gets the maximum decoded identifier character count.</summary>
    public const int MaximumIdentifierCharacterCount = ExpressionV2ContractLimits.MaximumIdentifierCharacterCount;

    /// <summary>Gets the maximum decoded suffix-fallback string character count.</summary>
    public const int MaximumFallbackStringCharacterCount = ExpressionV2ContractLimits.MaximumFallbackStringCharacterCount;

    /// <summary>Gets the maximum projected root-and-suffix expression segment count.</summary>
    public const int MaximumExpressionSegmentCount = ExpressionV2ContractLimits.MaximumExpressionSegmentCount;

    /// <summary>Gets the maximum retained metadata type-signature byte count.</summary>
    public const int MaximumTypeSignatureByteCount = ExpressionV2ContractLimits.MaximumTypeSpecificationByteCount;

    /// <summary>Gets the maximum metadata type-signature depth.</summary>
    public const int MaximumTypeSignatureDepth = ExpressionV2ContractLimits.MaximumTypeSpecificationDepth;

    /// <summary>Gets the maximum cumulative metadata type-signature argument count.</summary>
    public const int MaximumTypeSignatureArgumentCount = ExpressionV2ContractLimits.MaximumTypeSpecificationArgumentCount;

    /// <summary>Gets the maximum cumulative raw metadata signature-node count.</summary>
    public const int MaximumRawTypeSignatureNodeCount = ExpressionV2ContractLimits.MaximumRawTypeSignatureNodeCount;

    /// <summary>Gets the maximum cumulative TypeSpec-row traversal count.</summary>
    public const int MaximumTypeSpecificationRowTraversalCount =
        ExpressionV2ContractLimits.MaximumTypeSpecificationRowTraversalCount;

    /// <summary>Gets the maximum metadata signature token-resolution fact count.</summary>
    public const int MaximumSignatureTokenResolutionCount =
        ExpressionV2ContractLimits.MaximumSignatureTokenResolutionCount;

    /// <summary>Gets the maximum TypeDef-row count examined for metadata type context.</summary>
    public const int MaximumTypeDefinitionRowCount = ExpressionV2ContractLimits.MaximumTypeDefinitionRowCount;

    /// <summary>Gets the maximum FieldDef-row count examined for metadata type context.</summary>
    public const int MaximumFieldDefinitionRowCount = ExpressionV2ContractLimits.MaximumFieldDefinitionRowCount;

    /// <summary>Gets the maximum MethodDef-row count examined for metadata type context.</summary>
    public const int MaximumMethodDefinitionRowCount = ExpressionV2ContractLimits.MaximumMethodDefinitionRowCount;

    /// <summary>Gets the maximum FieldPtr-row count examined for metadata type context.</summary>
    public const int MaximumFieldPointerRowCount = ExpressionV2ContractLimits.MaximumFieldPointerRowCount;

    /// <summary>Gets the maximum MethodPtr-row count examined for metadata type context.</summary>
    public const int MaximumMethodPointerRowCount = ExpressionV2ContractLimits.MaximumMethodPointerRowCount;

    /// <summary>Gets the maximum InterfaceImpl-row count examined for metadata type relations.</summary>
    public const int MaximumInterfaceImplementationRowCount =
        ExpressionV2ContractLimits.MaximumInterfaceImplementationRowCount;

    /// <summary>Gets the maximum GenericParam-row count examined for metadata type context.</summary>
    public const int MaximumGenericParameterRowCount =
        ExpressionV2ContractLimits.MaximumGenericParameterRowCount;

    /// <summary>Gets the maximum NestedClass-row count examined for metadata type context.</summary>
    public const int MaximumNestedClassRowCount = ExpressionV2ContractLimits.MaximumNestedClassRowCount;

    /// <summary>Gets the maximum GenericParamConstraint-row count examined for metadata type context.</summary>
    public const int MaximumGenericParameterConstraintRowCount =
        ExpressionV2ContractLimits.MaximumGenericParameterConstraintRowCount;

    /// <summary>Gets the maximum closed-type topology depth.</summary>
    public const int MaximumClosedTypeTopologyDepth = ExpressionV2ContractLimits.MaximumClosedTypeTopologyDepth;

    /// <summary>Gets the maximum cumulative closed-type topology node count.</summary>
    public const int MaximumClosedTypeTopologyNodeCount = ExpressionV2ContractLimits.MaximumClosedTypeTopologyNodeCount;

    /// <summary>Gets the maximum multidimensional-array rank.</summary>
    public const int MaximumArrayRank = ExpressionV2ContractLimits.MaximumArrayRank;

    /// <summary>Gets the independently bounded parameter count.</summary>
    public const int MaximumParameterCount = ExpressionV2ContractLimits.MaximumParameterCount;

    /// <summary>Gets the independently bounded local-variable count.</summary>
    public const int MaximumLocalCount = ExpressionV2ContractLimits.MaximumLocalCount;

    /// <summary>Gets the independently bounded local-constant count.</summary>
    public const int MaximumLocalConstantCount = ExpressionV2ContractLimits.MaximumLocalConstantCount;

    /// <summary>Gets the independently bounded lexical-blocker count.</summary>
    public const int MaximumLexicalBlockerCount = ExpressionV2ContractLimits.MaximumLexicalBlockerCount;

    /// <summary>Gets the maximum generated local-function association count.</summary>
    public const int MaximumLocalFunctionAssociationCount =
        ExpressionV2ContractLimits.MaximumLocalFunctionAssociationCount;

    /// <summary>Gets the maximum directly declared property count used by selected-type lexical attribution.</summary>
    public const int MaximumDeclaredPropertyCount = ExpressionV2ContractLimits.MaximumDeclaredPropertyCount;

    /// <summary>Gets the maximum directly declared event count used by selected-type lexical attribution.</summary>
    public const int MaximumDeclaredEventCount = ExpressionV2ContractLimits.MaximumDeclaredEventCount;

    /// <summary>Gets the maximum directly declared method count used by selected-type lexical attribution.</summary>
    public const int MaximumDeclaredMethodCount = ExpressionV2ContractLimits.MaximumDeclaredMethodCount;

    /// <summary>Gets the maximum directly declared nested-type count used by selected-type lexical attribution.</summary>
    public const int MaximumDeclaredNestedTypeCount = ExpressionV2ContractLimits.MaximumDeclaredNestedTypeCount;

    /// <summary>Gets the maximum thread count examined during selected-thread attribution.</summary>
    public const int MaximumThreadCount = ExpressionV2ContractLimits.MaximumFrameThreadCount;

    /// <summary>Gets the maximum frame count examined on one selected thread.</summary>
    public const int MaximumFrameCountPerThread = ExpressionV2ContractLimits.MaximumFrameCountPerThread;

    /// <summary>Gets the maximum copied byte width of one frame value.</summary>
    public const int MaximumValueByteCount = ExpressionV2ContractLimits.MaximumFrameValueByteCount;

    /// <summary>Gets the maximum decoded frame string character count.</summary>
    public const int MaximumStringCharacterCount = ExpressionV2ContractLimits.MaximumFrameStringCharacterCount;

    /// <summary>Gets every operation-derived limit available to this profile in canonical name order.</summary>
    /// <remarks>The defensive catalog is part of the profile and may change as its bounded operations evolve.</remarks>
    public static ImmutableArray<EvaluationDeterministicBound> AllDeclaredBounds =>
        ExpressionV2ContractLimits.AllDeclaredBounds
            .Where(static bound =>
                bound.Name.StartsWith("expression-v2.frame.", StringComparison.Ordinal) ||
                bound.Name is ExpressionV2ContractLimits.ExpressionCharacterCountBoundName or
                    ExpressionV2ContractLimits.SyntaxNodeTokenCountBoundName or
                    ExpressionV2ContractLimits.SyntaxDepthBoundName or
                    ExpressionV2ContractLimits.IdentifierCharacterCountBoundName or
                    ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName or
                    ExpressionV2ContractLimits.ExpressionSegmentCountBoundName or
                    ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName or
                    ExpressionV2ContractLimits.ClosedTypeTopologyNodeCountBoundName or
                    ExpressionV2ContractLimits.TypeSpecificationByteCountBoundName or
                    ExpressionV2ContractLimits.TypeSpecificationDepthBoundName or
                    ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName or
                    ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName or
                    ExpressionV2ContractLimits.SignatureTokenResolutionCountBoundName or
                    ExpressionV2ContractLimits.TypeSpecificationRowTraversalCountBoundName or
                    ExpressionV2ContractLimits.TypeDefinitionRowCountBoundName or
                    ExpressionV2ContractLimits.FieldDefinitionRowCountBoundName or
                    ExpressionV2ContractLimits.FieldPointerRowCountBoundName or
                    ExpressionV2ContractLimits.MethodDefinitionRowCountBoundName or
                    ExpressionV2ContractLimits.MethodPointerRowCountBoundName or
                    ExpressionV2ContractLimits.InterfaceImplementationRowCountBoundName or
                    ExpressionV2ContractLimits.NestedClassRowCountBoundName or
                    ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName or
                    ExpressionV2ContractLimits.GenericParameterRowCountBoundName or
                    ExpressionV2ContractLimits.ArrayRankBoundName or
                    ExpressionV2ContractLimits.ParameterCountBoundName or
                    ExpressionV2ContractLimits.LocalCountBoundName or
                    ExpressionV2ContractLimits.LocalConstantCountBoundName or
                    ExpressionV2ContractLimits.LexicalBlockerCountBoundName or
                    ExpressionV2ContractLimits.LocalFunctionAssociationCountBoundName or
                    ExpressionV2ContractLimits.DeclaredPropertyCountBoundName or
                    ExpressionV2ContractLimits.DeclaredEventCountBoundName or
                    ExpressionV2ContractLimits.DeclaredMethodCountBoundName or
                    ExpressionV2ContractLimits.DeclaredNestedTypeCountBoundName)
            .ToImmutableArray();
}

/// <summary>Classifies a detached V2 syntax outcome.</summary>
/// <remarks>This discriminator is not yet a compatibility commitment.</remarks>
public enum DumpExpressionSyntaxStatus
{
    /// <summary>The complete parse belongs to the selected V2 profile.</summary>
    Admitted = 1,

    /// <summary>The input or complete parse violates an integrity rule.</summary>
    Invalid = 2,

    /// <summary>The complete parse is valid C# but outside the selected V2 profile.</summary>
    Unsupported = 3,
}

/// <summary>Classifies the reusable suffix topology detached from a V2 root.</summary>
/// <remarks>This discriminator is not yet a compatibility commitment.</remarks>
public enum DumpExpressionSuffixKind
{
    /// <summary>No instance-member suffix was requested.</summary>
    NotRequested = 0,

    /// <summary>One direct or conditional member follows the root.</summary>
    DirectMember = 1,

    /// <summary>Exactly two direct or conditional members follow the root.</summary>
    FixedDepthMemberChain = 2,
}

/// <summary>Classifies the edge leading to one detached suffix member.</summary>
/// <remarks>This discriminator is not yet a compatibility commitment.</remarks>
public enum DumpExpressionSuffixAccessKind
{
    /// <summary>The member is reached through ordinary dot access.</summary>
    Direct = 1,

    /// <summary>The member is reached through conditional access.</summary>
    Conditional = 2,
}

/// <summary>Classifies the optional literal fallback retained by a suffix.</summary>
/// <remarks>This discriminator is not yet a compatibility commitment.</remarks>
public enum DumpExpressionFallbackKind
{
    /// <summary>No fallback is present.</summary>
    None = 0,

    /// <summary>The exact null literal is present.</summary>
    Null = 1,

    /// <summary>An exact <see cref="int"/> literal is present.</summary>
    Int32 = 2,

    /// <summary>An exact decoded string literal is present.</summary>
    String = 3,
}

/// <summary>Classifies one stable structured diagnostic argument.</summary>
/// <remarks>This discriminator is not yet a compatibility commitment.</remarks>
public enum DumpExpressionDiagnosticArgumentKind
{
    /// <summary>The argument is bounded ordinal text.</summary>
    Text = 1,

    /// <summary>The argument is a signed 64-bit integer.</summary>
    Int64 = 2,

    /// <summary>The argument is an unsigned 64-bit integer.</summary>
    UInt64 = 3,

    /// <summary>The argument is a non-nil metadata token.</summary>
    MetadataToken = 4,

    /// <summary>The argument is a lowercase SHA-256 content digest.</summary>
    Sha256 = 5,
}

/// <summary>Classifies the stable importance of one structured diagnostic.</summary>
/// <remarks>This discriminator is not yet a compatibility commitment.</remarks>
public enum DumpExpressionDiagnosticSeverity
{
    /// <summary>The diagnostic records supplementary exact evidence.</summary>
    Information = 1,

    /// <summary>The diagnostic records a non-fatal qualification.</summary>
    Warning = 2,

    /// <summary>The diagnostic identifies the typed terminal disposition.</summary>
    Error = 3,
}

/// <summary>Identifies the fixed W8 pipeline stage that emitted one structured diagnostic.</summary>
/// <remarks>This stage catalog is not yet a compatibility commitment.</remarks>
public enum DumpExpressionDiagnosticStage
{
    /// <summary>The selected profile and complete parse.</summary>
    Parse = 1,

    /// <summary>Detached syntax or root projection.</summary>
    Projection = 2,

    /// <summary>Route-specific context acquisition.</summary>
    ContextAcquisition = 3,

    /// <summary>Frame-root or bare-root attribution.</summary>
    RootAttribution = 4,

    /// <summary>Bare-root lexical completeness.</summary>
    LexicalCompleteness = 5,

    /// <summary>Namespace, type, or alias binding.</summary>
    TypeBinding = 6,

    /// <summary>Closed metadata construction and generic constraints.</summary>
    TypeConstruction = 7,

    /// <summary>Definition-kind-specific member lookup.</summary>
    MemberLookup = 8,

    /// <summary>Exact closed runtime-construction mapping.</summary>
    RuntimeConstruction = 9,

    /// <summary>Strategy selection and storage acquisition.</summary>
    Storage = 10,

    /// <summary>Raw-byte or metadata-literal value projection.</summary>
    Value = 11,

    /// <summary>Constructed reference-target validation.</summary>
    Assignability = 12,

    /// <summary>Evaluation of the unchanged detached suffix.</summary>
    Suffix = 13,

    /// <summary>Final completeness and provenance projection.</summary>
    Completion = 14,
}

/// <summary>Identifies a capability whose use or deliberate omission belongs to V2 provenance.</summary>
/// <remarks>This capability catalog is not yet a compatibility commitment.</remarks>
public enum DumpExpressionCapabilityKind
{
    /// <summary>The sole complete expression parse.</summary>
    Parse = 1,

    /// <summary>Selected-frame acquisition.</summary>
    SelectedFrame = 2,

    /// <summary>Portable-PDB context acquisition.</summary>
    PortablePdb = 3,

    /// <summary>Selected-method parameters, locals, scopes, and lexical catalogs.</summary>
    SelectedMethodLexical = 4,

    /// <summary>Metadata acquisition and resolution.</summary>
    Metadata = 5,

    /// <summary>Closed runtime-construction enumeration.</summary>
    RuntimeConstruction = 6,

    /// <summary>Selected-thread attribution.</summary>
    ThreadSelection = 7,

    /// <summary>Strategy-specific storage acquisition.</summary>
    StorageAcquisition = 8,

    /// <summary>Project-owned raw-byte acquisition.</summary>
    RawMemory = 9,

    /// <summary>Constructed reference-assignability validation.</summary>
    ConstructedAssignability = 10,

    /// <summary>Evaluation of the unchanged detached suffix.</summary>
    Suffix = 11,
}

/// <summary>Classifies how one V2 capability participated in an outcome.</summary>
/// <remarks>This discriminator is not yet a compatibility commitment.</remarks>
public enum DumpExpressionCapabilityDisposition
{
    /// <summary>The fixed route proved that the capability was unnecessary.</summary>
    NotRequired = 1,

    /// <summary>The capability returned complete exact evidence.</summary>
    Exact = 2,

    /// <summary>The capability was called but did not return complete exact evidence.</summary>
    NonExact = 3,

    /// <summary>An earlier typed stop prevented the capability from being reached.</summary>
    NotReached = 4,
}

/// <summary>Identifies the route selected before V2 binding begins.</summary>
/// <remarks>This route catalog is not yet a compatibility commitment.</remarks>
public enum DumpExpressionRouteKind
{
    /// <summary>Syntax stopped before any binding route could be selected.</summary>
    NotReached = 0,

    /// <summary>An explicit <c>global::</c> static-owner route.</summary>
    FullyQualified = 1,

    /// <summary>An ordinary dot-qualified route whose binder runs metadata-global then conditional context lookup.</summary>
    OrdinaryQualified = 2,

    /// <summary>An explicit named-alias-qualified route.</summary>
    AliasQualified = 3,

    /// <summary>Bare static-member lookup with fixed current-member-then-using-static precedence.</summary>
    BareStaticMember = 4,

    /// <summary>A separately selected frame-value route.</summary>
    FrameValue = 5,
}

/// <summary>Identifies the exact additive expression profile represented by shared W8 provenance.</summary>
/// <remarks>This profile catalog is not yet a compatibility commitment.</remarks>
public enum DumpExpressionProfileKind
{
    /// <summary>The additive bounded static-field expression profile.</summary>
    StaticFieldExpressionV2 = 1,

    /// <summary>The separately selected exact frame-value expression profile.</summary>
    FrameValueExpressionV1 = 2,
}

/// <summary>Freezes one identifier value decoded by the sole complete Roslyn parse.</summary>
/// <remarks>
/// This sealed value retains no per-token source spelling, parser node, or semantic symbol. The parent syntax
/// descriptor retains the exact complete expression, preventing a second token parse while preserving global replay.
/// </remarks>
public sealed class DumpExpressionIdentifier : IEquatable<DumpExpressionIdentifier>
{
    private const string CanonicalDomain = "expression-v2-identifier";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionIdentifier(string decodedText)
    {
        DecodedText = decodedText;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(decodedText);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact identifier value decoded by the complete parser.</summary>
    public string DecodedText { get; }

    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated identifier value supplied by the sole complete parser.</summary>
    /// <param name="decodedText">The exact non-empty decoded identifier value.</param>
    /// <remarks>
    /// This factory validates a projection value only; its accepted identifier policy may change before the
    /// schema is declared compatible.
    /// </remarks>
    /// <returns>A sealed immutable identifier for the current early-development phase.</returns>
    /// <exception cref="ArgumentException">The value is empty, too long, NUL-containing, or not a C# identifier/keyword value.</exception>
    public static DumpExpressionIdentifier Create(string decodedText)
    {
        ExpressionV2ContractEncoding.RequireText(
            decodedText,
            nameof(decodedText),
            CSharpExpressionFrontEnd.MaximumIdentifierLength);
        if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(decodedText) &&
            Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(decodedText) ==
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.None &&
            Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetContextualKeywordKind(decodedText) ==
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)
        {
            throw new ArgumentException(
                "A complete decoded C# identifier, reserved-keyword, or contextual-keyword value is required.",
                nameof(decodedText));
        }
        return new DumpExpressionIdentifier(decodedText);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other identifier.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionIdentifier? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionIdentifier);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one member and the exact access edge leading to it.</summary>
/// <remarks>This is an additive suffix primitive shared by both W8 expression profiles.</remarks>
public sealed class DumpExpressionSuffixSegment : IEquatable<DumpExpressionSuffixSegment>
{
    private const string CanonicalDomain = "expression-v2-suffix-segment";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionSuffixSegment(DumpExpressionIdentifier identifier, DumpExpressionSuffixAccessKind accessKind)
    {
        Identifier = identifier;
        AccessKind = accessKind;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(identifier.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)accessKind);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the detached member identifier.</summary>
    public DumpExpressionIdentifier Identifier { get; }

    /// <summary>Gets the exact direct or conditional access edge.</summary>
    public DumpExpressionSuffixAccessKind AccessKind { get; }

    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated detached suffix segment.</summary>
    /// <param name="identifier">The exact member identifier.</param>
    /// <param name="accessKind">The exact edge leading to the member.</param>
    /// <remarks>
    /// This factory freezes projection identity only and does not commit later binding or evaluation behavior.
    /// </remarks>
    /// <returns>A sealed immutable segment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identifier"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="accessKind"/> is undefined.</exception>
    public static DumpExpressionSuffixSegment Create(
        DumpExpressionIdentifier identifier,
        DumpExpressionSuffixAccessKind accessKind)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ExpressionV2ContractEncoding.RequireDefined(accessKind, nameof(accessKind));
        return new DumpExpressionSuffixSegment(identifier, accessKind);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other segment.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionSuffixSegment? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionSuffixSegment);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the unchanged zero-, one-, or two-member suffix and optional literal fallback.</summary>
/// <remarks>The contract does not execute the suffix.</remarks>
public sealed class DumpExpressionSuffixDescriptor : IEquatable<DumpExpressionSuffixDescriptor>
{
    private const string CanonicalDomain = "expression-v2-suffix-descriptor";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<DumpExpressionSuffixSegment> segments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionSuffixDescriptor(
        DumpExpressionSuffixKind kind,
        ImmutableArray<DumpExpressionSuffixSegment> segments,
        DumpExpressionFallbackKind fallbackKind,
        int? int32Fallback,
        string? stringFallback)
    {
        Kind = kind;
        this.segments = segments;
        FallbackKind = fallbackKind;
        Int32Fallback = int32Fallback;
        StringFallback = stringFallback;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            segments,
            static segment => segment.CanonicalBytes);
        writer.WriteInt32((int)fallbackKind);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, int32Fallback);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, stringFallback);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact suffix topology.</summary>
    public DumpExpressionSuffixKind Kind { get; }

    /// <summary>Gets a defensive copy of the ordered suffix members.</summary>
    public ImmutableArray<DumpExpressionSuffixSegment> Segments => ExpressionV2ContractEncoding.Copy(segments);

    /// <summary>Gets the optional fallback kind.</summary>
    public DumpExpressionFallbackKind FallbackKind { get; }

    /// <summary>Gets the integer fallback payload when present.</summary>
    public int? Int32Fallback { get; }

    /// <summary>Gets the decoded string fallback payload when present.</summary>
    public string? StringFallback { get; }

    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated detached suffix descriptor.</summary>
    /// <param name="kind">The requested suffix topology.</param>
    /// <param name="segments">The exact ordered member segments.</param>
    /// <param name="fallbackKind">The optional literal fallback kind.</param>
    /// <param name="int32Fallback">The payload used only by an Int32 fallback.</param>
    /// <param name="stringFallback">The payload used only by a string fallback.</param>
    /// <remarks>
    /// This factory validates detached suffix identity only; admitted suffix behavior remains independently
    /// versioned.
    /// </remarks>
    /// <returns>A sealed immutable descriptor.</returns>
    /// <exception cref="ArgumentException">The topology, array, or fallback payloads disagree.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is undefined.</exception>
    public static DumpExpressionSuffixDescriptor Create(
        DumpExpressionSuffixKind kind,
        ImmutableArray<DumpExpressionSuffixSegment> segments,
        DumpExpressionFallbackKind fallbackKind = DumpExpressionFallbackKind.None,
        int? int32Fallback = null,
        string? stringFallback = null)
    {
        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        ExpressionV2ContractEncoding.RequireDefined(fallbackKind, nameof(fallbackKind));
        var copied = ExpressionV2ContractEncoding.CopyRequired(segments, nameof(segments), maximumCount: 2);
        var expectedCount = kind switch
        {
            DumpExpressionSuffixKind.NotRequested => 0,
            DumpExpressionSuffixKind.DirectMember => 1,
            DumpExpressionSuffixKind.FixedDepthMemberChain => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        if (copied.Length != expectedCount)
        {
            throw new ArgumentException("The suffix kind and ordered segment count disagree.", nameof(segments));
        }

        var payloadMatches = fallbackKind switch
        {
            DumpExpressionFallbackKind.None or DumpExpressionFallbackKind.Null =>
                int32Fallback is null && stringFallback is null,
            DumpExpressionFallbackKind.Int32 => int32Fallback.HasValue && stringFallback is null,
            DumpExpressionFallbackKind.String => int32Fallback is null && stringFallback is not null,
            _ => false,
        };
        if (!payloadMatches || fallbackKind != DumpExpressionFallbackKind.None && kind == DumpExpressionSuffixKind.NotRequested)
        {
            throw new ArgumentException("The suffix and fallback payloads disagree.", nameof(fallbackKind));
        }
        if (stringFallback is not null)
        {
            ExpressionV2ContractEncoding.RequireText(
                stringFallback,
                nameof(stringFallback),
                CSharpExpressionFrontEnd.MaximumStringLiteralLength,
                allowEmpty: true);
        }

        return new DumpExpressionSuffixDescriptor(kind, copied, fallbackKind, int32Fallback, stringFallback);
    }

    /// <summary>Creates the canonical no-suffix descriptor.</summary>
    /// <remarks>This convenience factory belongs to the suffix schema and is not a compatibility commitment.</remarks>
    /// <returns>A new content-equal immutable descriptor.</returns>
    public static DumpExpressionSuffixDescriptor NotRequested() =>
        Create(DumpExpressionSuffixKind.NotRequested, ImmutableArray<DumpExpressionSuffixSegment>.Empty);

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other descriptor.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionSuffixDescriptor? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionSuffixDescriptor);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one typed argument without retaining exception or compiler wording.</summary>
/// <remarks>The stable argument kind and payload are canonical identity.</remarks>
public sealed class DumpExpressionDiagnosticArgument : IEquatable<DumpExpressionDiagnosticArgument>
{
    private const string CanonicalDomain = "expression-v2-diagnostic-argument";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionDiagnosticArgument(
        string name,
        DumpExpressionDiagnosticArgumentKind kind,
        string? textValue,
        long? int64Value,
        ulong? uint64Value)
    {
        Name = name;
        Kind = kind;
        TextValue = textValue;
        Int64Value = int64Value;
        UInt64Value = uint64Value;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(name);
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, textValue);
        ExpressionV2ContractEncoding.WriteOptionalInt64(writer, int64Value);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, uint64Value);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the stable lowercase semantic argument name.</summary>
    public string Name { get; }

    /// <summary>Gets the payload discriminator.</summary>
    public DumpExpressionDiagnosticArgumentKind Kind { get; }

    /// <summary>Gets the text, metadata-token hexadecimal text, or digest payload when present.</summary>
    public string? TextValue { get; }

    /// <summary>Gets the signed integer payload when present.</summary>
    public long? Int64Value { get; }

    /// <summary>Gets the unsigned integer payload when present.</summary>
    public ulong? UInt64Value { get; }

    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a bounded ordinal-text argument.</summary>
    /// <param name="name">The stable lowercase semantic argument name.</param>
    /// <param name="value">The non-empty stable text.</param>
    /// <remarks>This factory emits the diagnostic-argument schema, whose canonical form may still evolve.</remarks>
    /// <returns>A sealed immutable argument.</returns>
    public static DumpExpressionDiagnosticArgument Text(string name, string value)
    {
        ExpressionV2ContractEncoding.RequireStableName(name, nameof(name));
        ExpressionV2ContractEncoding.RequireText(value, nameof(value), 2_048);
        return new DumpExpressionDiagnosticArgument(name, DumpExpressionDiagnosticArgumentKind.Text, value, null, null);
    }

    /// <summary>Creates a signed integer argument.</summary>
    /// <param name="name">The stable lowercase semantic argument name.</param>
    /// <param name="value">The exact signed value.</param>
    /// <remarks>This factory emits the diagnostic-argument schema, whose canonical form may still evolve.</remarks>
    /// <returns>A sealed immutable argument.</returns>
    public static DumpExpressionDiagnosticArgument Signed(string name, long value)
    {
        ExpressionV2ContractEncoding.RequireStableName(name, nameof(name));
        return new DumpExpressionDiagnosticArgument(name, DumpExpressionDiagnosticArgumentKind.Int64, null, value, null);
    }

    /// <summary>Creates an unsigned integer argument.</summary>
    /// <param name="name">The stable lowercase semantic argument name.</param>
    /// <param name="value">The exact unsigned value.</param>
    /// <remarks>This factory emits the diagnostic-argument schema, whose canonical form may still evolve.</remarks>
    /// <returns>A sealed immutable argument.</returns>
    public static DumpExpressionDiagnosticArgument Unsigned(string name, ulong value)
    {
        ExpressionV2ContractEncoding.RequireStableName(name, nameof(name));
        return new DumpExpressionDiagnosticArgument(name, DumpExpressionDiagnosticArgumentKind.UInt64, null, null, value);
    }

    /// <summary>Creates a non-nil metadata-token argument.</summary>
    /// <param name="name">The stable lowercase semantic argument name.</param>
    /// <param name="metadataToken">The exact non-nil token.</param>
    /// <remarks>This factory emits the diagnostic-argument schema, whose canonical form may still evolve.</remarks>
    /// <returns>A sealed immutable argument.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="metadataToken"/> is nil.</exception>
    public static DumpExpressionDiagnosticArgument MetadataToken(string name, int metadataToken)
    {
        ExpressionV2ContractEncoding.RequireStableName(name, nameof(name));
        if ((metadataToken & 0x00FF_FFFF) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadataToken), "A non-nil metadata token is required.");
        }
        return new DumpExpressionDiagnosticArgument(
            name,
            DumpExpressionDiagnosticArgumentKind.MetadataToken,
            metadataToken.ToString("x8", System.Globalization.CultureInfo.InvariantCulture),
            null,
            null);
    }

    /// <summary>Creates a normalized SHA-256 argument.</summary>
    /// <param name="name">The stable lowercase semantic argument name.</param>
    /// <param name="sha256">A complete hexadecimal digest.</param>
    /// <remarks>This factory emits the diagnostic-argument schema, whose canonical form may still evolve.</remarks>
    /// <returns>A sealed immutable argument.</returns>
    public static DumpExpressionDiagnosticArgument Digest(string name, string sha256)
    {
        ExpressionV2ContractEncoding.RequireStableName(name, nameof(name));
        return new DumpExpressionDiagnosticArgument(
            name,
            DumpExpressionDiagnosticArgumentKind.Sha256,
            CanonicalReplayEncoding.NormalizeSha256(sha256, nameof(sha256)),
            null,
            null);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other argument.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionDiagnosticArgument? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionDiagnosticArgument);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one stable diagnostic code, stage, severity, and normalized named typed-argument set.</summary>
/// <remarks>Human-readable messages are derived by consumers and deliberately do not enter this identity.</remarks>
public sealed class DumpExpressionDiagnostic : IEquatable<DumpExpressionDiagnostic>
{
    /// <summary>Gets the maximum named argument count retained by one diagnostic.</summary>
    public const int MaximumArgumentCount = 16;

    private const string CanonicalDomain = "expression-v2-diagnostic";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<DumpExpressionDiagnosticArgument> arguments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionDiagnostic(
        string code,
        DumpExpressionDiagnosticStage stage,
        DumpExpressionDiagnosticSeverity severity,
        ImmutableArray<DumpExpressionDiagnosticArgument> arguments)
    {
        Code = code;
        Stage = stage;
        Severity = severity;
        this.arguments = arguments;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(code);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)severity);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            arguments,
            static argument => argument.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the stable uppercase ASCII diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the fixed pipeline stage that emitted this diagnostic.</summary>
    public DumpExpressionDiagnosticStage Stage { get; }

    /// <summary>Gets the stable diagnostic severity.</summary>
    public DumpExpressionDiagnosticSeverity Severity { get; }

    /// <summary>Gets a defensive copy of typed arguments in ordinal name order.</summary>
    public ImmutableArray<DumpExpressionDiagnosticArgument> Arguments => ExpressionV2ContractEncoding.Copy(arguments);

    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated structured diagnostic.</summary>
    /// <param name="code">A stable uppercase ASCII code beginning with <c>W8_</c>.</param>
    /// <param name="stage">The fixed pipeline stage that emitted the diagnostic.</param>
    /// <param name="severity">The stable severity.</param>
    /// <param name="arguments">Zero through sixteen uniquely named typed arguments; caller order is normalized.</param>
    /// <remarks>
    /// This factory validates the structured-diagnostic schema; its canonical contract may change before a
    /// compatibility declaration.
    /// </remarks>
    /// <returns>A sealed immutable diagnostic.</returns>
    /// <exception cref="ArgumentException">The code or argument array is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="severity"/> is undefined.</exception>
    public static DumpExpressionDiagnostic Create(
        string code,
        DumpExpressionDiagnosticStage stage,
        DumpExpressionDiagnosticSeverity severity,
        ImmutableArray<DumpExpressionDiagnosticArgument> arguments)
    {
        ExpressionV2ContractEncoding.RequireDiagnosticCode(code, nameof(code));
        ExpressionV2ContractEncoding.RequireDefined(stage, nameof(stage));
        ExpressionV2ContractEncoding.RequireDefined(severity, nameof(severity));
        var copied = ExpressionV2ContractEncoding.CopyRequired(arguments, nameof(arguments), MaximumArgumentCount);
        copied = ImmutableArray.CreateRange(copied.OrderBy(static argument => argument.Name, StringComparer.Ordinal));
        for (var index = 1; index < copied.Length; index++)
        {
            if (string.Equals(copied[index - 1].Name, copied[index].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException("Diagnostic argument names cannot repeat.", nameof(arguments));
            }
        }
        return new DumpExpressionDiagnostic(code, stage, severity, copied);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other diagnostic.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionDiagnostic? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionDiagnostic);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Records whether and how one named capability participated in a V2 route.</summary>
/// <remarks>The optional evidence digest addresses a detached canonical fact; no live service enters this value.</remarks>
public sealed class DumpExpressionCapabilityUse : IEquatable<DumpExpressionCapabilityUse>
{
    private const string CanonicalDomain = "expression-v2-capability-use";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<DumpExpressionDiagnostic> diagnostics;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionCapabilityUse(
        DumpExpressionCapabilityKind capability,
        DumpExpressionCapabilityDisposition disposition,
        int callCount,
        string? consultedSourceSha256,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics)
    {
        Capability = capability;
        Disposition = disposition;
        CallCount = callCount;
        ConsultedSourceSha256 = consultedSourceSha256;
        this.reachedBounds = reachedBounds;
        this.diagnostics = diagnostics;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)capability);
        writer.WriteInt32((int)disposition);
        writer.WriteInt32(callCount);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, consultedSourceSha256);
        ExpressionV2ContractEncoding.WriteBounds(writer, reachedBounds);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            diagnostics,
            static diagnostic => diagnostic.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the named capability.</summary>
    public DumpExpressionCapabilityKind Capability { get; }

    /// <summary>Gets its exact route disposition.</summary>
    public DumpExpressionCapabilityDisposition Disposition { get; }

    /// <summary>Gets the exact number of calls made to the capability.</summary>
    public int CallCount { get; }

    /// <summary>Gets the detached consulted-source digest for Exact or NonExact use, otherwise null.</summary>
    public string? ConsultedSourceSha256 { get; }

    /// <summary>Gets a defensive copy of normalized operation bounds reached by this called capability.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds =>
        ExpressionV2ContractEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of stable capability-local diagnostics.</summary>
    public ImmutableArray<DumpExpressionDiagnostic> Diagnostics => ExpressionV2ContractEncoding.Copy(diagnostics);

    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated capability-use record.</summary>
    /// <param name="capability">The named capability.</param>
    /// <param name="disposition">Whether it was omitted, exact, non-exact, or not reached.</param>
    /// <param name="callCount">The exact positive call count for a called capability, otherwise zero.</param>
    /// <param name="consultedSourceSha256">The detached consulted-source digest required only for a called capability.</param>
    /// <param name="reachedBounds">The normalized operation bounds reached by a called capability.</param>
    /// <param name="diagnostics">Stable capability-local diagnostics.</param>
    /// <remarks>
    /// This factory enforces ledger invariants; later checkpoints may refine capability accounting before the
    /// schema is declared compatible.
    /// </remarks>
    /// <returns>A sealed immutable capability-use record.</returns>
    /// <exception cref="ArgumentException">The digest or diagnostic payload contradicts the disposition.</exception>
    public static DumpExpressionCapabilityUse Create(
        DumpExpressionCapabilityKind capability,
        DumpExpressionCapabilityDisposition disposition,
        int callCount,
        string? consultedSourceSha256,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics)
    {
        ExpressionV2ContractEncoding.RequireDefined(capability, nameof(capability));
        ExpressionV2ContractEncoding.RequireDefined(disposition, nameof(disposition));
        ArgumentOutOfRangeException.ThrowIfNegative(callCount);
        var bounds = ExpressionV2ContractEncoding.NormalizeReachedBounds(reachedBounds, nameof(reachedBounds));
        var copied = ExpressionV2ContractEncoding.CopyRequired(diagnostics, nameof(diagnostics), maximumCount: 64);
        var called = disposition is DumpExpressionCapabilityDisposition.Exact or
            DumpExpressionCapabilityDisposition.NonExact;
        if (called != (callCount > 0) || called != (consultedSourceSha256 is not null))
        {
            throw new ArgumentException(
                "A called capability requires a positive count and consulted-source digest; an omitted capability requires neither.",
                nameof(callCount));
        }
        var normalized = consultedSourceSha256 is null
            ? null
            : CanonicalReplayEncoding.NormalizeSha256(consultedSourceSha256, nameof(consultedSourceSha256));
        if (!called && (bounds.Length != 0 || copied.Length != 0))
        {
            throw new ArgumentException("An omitted capability cannot carry reached bounds or local diagnostics.", nameof(diagnostics));
        }
        if (capability == DumpExpressionCapabilityKind.Parse && called && callCount != 1)
        {
            throw new ArgumentException("A reached Parse capability must record exactly one complete parse call.", nameof(callCount));
        }
        if (capability == DumpExpressionCapabilityKind.Parse &&
            disposition == DumpExpressionCapabilityDisposition.NotRequired)
        {
            throw new ArgumentException("Parse is always required and cannot be marked NotRequired.", nameof(disposition));
        }
        if (disposition == DumpExpressionCapabilityDisposition.Exact &&
            copied.Any(static diagnostic => diagnostic.Severity == DumpExpressionDiagnosticSeverity.Error))
        {
            throw new ArgumentException("An exact capability use cannot carry an Error diagnostic.", nameof(diagnostics));
        }
        if (disposition == DumpExpressionCapabilityDisposition.NonExact &&
            !copied.Any(static diagnostic => diagnostic.Severity == DumpExpressionDiagnosticSeverity.Error))
        {
            throw new ArgumentException("A non-exact capability use requires at least one stable Error diagnostic.", nameof(diagnostics));
        }
        return new DumpExpressionCapabilityUse(capability, disposition, callCount, normalized, bounds, copied);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other record.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionCapabilityUse? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionCapabilityUse);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete route, syntax digest, capability ledger, and stable diagnostics for one V2 outcome.</summary>
/// <remarks>
/// This identity requires exactly one record for every capability kind. It therefore proves both calls and
/// route-derived omissions rather than relying on absent log entries.
/// </remarks>
public sealed class DumpExpressionV2ProvenanceIdentity : IEquatable<DumpExpressionV2ProvenanceIdentity>
{
    private const string CanonicalDomain = "expression-v2-provenance-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<DumpExpressionCapabilityUse> capabilityUses;
    private readonly ImmutableArray<DumpExpressionDiagnostic> diagnostics;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionV2ProvenanceIdentity(
        DumpExpressionProfileKind profile,
        string syntaxSha256,
        DumpExpressionRouteKind route,
        ImmutableArray<DumpExpressionCapabilityUse> capabilityUses,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics)
    {
        Profile = profile;
        SyntaxSha256 = syntaxSha256;
        Route = route;
        this.capabilityUses = capabilityUses;
        this.diagnostics = diagnostics;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)profile);
        writer.WriteSha256(syntaxSha256, nameof(syntaxSha256));
        writer.WriteInt32((int)route);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            capabilityUses,
            static use => use.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            diagnostics,
            static diagnostic => diagnostic.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact selected expression profile.</summary>
    public DumpExpressionProfileKind Profile { get; }

    /// <summary>Gets the canonical detached syntax digest.</summary>
    public string SyntaxSha256 { get; }

    /// <summary>Gets the route selected before binding.</summary>
    public DumpExpressionRouteKind Route { get; }

    /// <summary>Gets a defensive copy of all capability records in enum order.</summary>
    public ImmutableArray<DumpExpressionCapabilityUse> CapabilityUses =>
        ExpressionV2ContractEncoding.Copy(capabilityUses);

    /// <summary>Gets a defensive copy of outcome-level stable diagnostics.</summary>
    public ImmutableArray<DumpExpressionDiagnostic> Diagnostics => ExpressionV2ContractEncoding.Copy(diagnostics);

    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete validated provenance identity.</summary>
    /// <param name="profile">The exact selected expression profile.</param>
    /// <param name="syntaxSha256">The detached syntax canonical digest.</param>
    /// <param name="route">The route selected before binding.</param>
    /// <param name="capabilityUses">Exactly one record for every capability kind.</param>
    /// <param name="diagnostics">Stable outcome-level diagnostics.</param>
    /// <remarks>
    /// This factory enforces the route ledger; route and capability invariants may evolve before the provenance
    /// schema is declared compatible.
    /// </remarks>
    /// <returns>A sealed immutable provenance identity.</returns>
    /// <exception cref="ArgumentException">A payload is missing, duplicated, malformed, or route-inconsistent.</exception>
    public static DumpExpressionV2ProvenanceIdentity Create(
        DumpExpressionProfileKind profile,
        string syntaxSha256,
        DumpExpressionRouteKind route,
        ImmutableArray<DumpExpressionCapabilityUse> capabilityUses,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics)
    {
        ExpressionV2ContractEncoding.RequireDefined(profile, nameof(profile));
        var normalizedDigest = CanonicalReplayEncoding.NormalizeSha256(syntaxSha256, nameof(syntaxSha256));
        ExpressionV2ContractEncoding.RequireDefined(route, nameof(route));
        var uses = ExpressionV2ContractEncoding.CopyRequired(
            capabilityUses,
            nameof(capabilityUses),
            Enum.GetValues<DumpExpressionCapabilityKind>().Length,
            requireExactCount: true);
        uses = ImmutableArray.CreateRange(uses.OrderBy(static use => use.Capability));
        var expected = Enum.GetValues<DumpExpressionCapabilityKind>();
        for (var index = 0; index < expected.Length; index++)
        {
            if (uses[index].Capability != expected[index])
            {
                throw new ArgumentException("Exactly one record for every capability kind is required.", nameof(capabilityUses));
            }
        }
        if (uses.Select(static use => use.Capability).Distinct().Count() != expected.Length)
        {
            throw new ArgumentException("Capability records cannot repeat a capability kind.", nameof(capabilityUses));
        }

        var globalDiagnostics = ExpressionV2ContractEncoding.CopyRequired(diagnostics, nameof(diagnostics), maximumCount: 256);
        var parse = uses.Single(static use => use.Capability == DumpExpressionCapabilityKind.Parse);
        var frame = uses.Single(static use => use.Capability == DumpExpressionCapabilityKind.SelectedFrame);
        var pdb = uses.Single(static use => use.Capability == DumpExpressionCapabilityKind.PortablePdb);
        var lexical = uses.Single(static use => use.Capability == DumpExpressionCapabilityKind.SelectedMethodLexical);
        if (route == DumpExpressionRouteKind.NotReached)
        {
            if (parse.Disposition == DumpExpressionCapabilityDisposition.NotRequired ||
                uses.Any(static use => use.Capability != DumpExpressionCapabilityKind.Parse &&
                    use.Disposition != DumpExpressionCapabilityDisposition.NotReached))
            {
                throw new ArgumentException(
                    "An unreached route requires a reached/failed/unreached Parse record and every later capability NotReached.",
                    nameof(capabilityUses));
            }
        }
        else if (parse.Disposition != DumpExpressionCapabilityDisposition.Exact)
        {
            throw new ArgumentException("Every selected route requires one exact complete parse.", nameof(capabilityUses));
        }
        if (route == DumpExpressionRouteKind.FullyQualified &&
            (frame.Disposition != DumpExpressionCapabilityDisposition.NotRequired ||
             pdb.Disposition != DumpExpressionCapabilityDisposition.NotRequired ||
             lexical.Disposition != DumpExpressionCapabilityDisposition.NotRequired))
        {
            throw new ArgumentException(
                "A context-independent route must explicitly mark frame, PDB, and selected-method lexical capabilities NotRequired.",
                nameof(capabilityUses));
        }

        var frameProfile = profile == DumpExpressionProfileKind.FrameValueExpressionV1;
        if (route != DumpExpressionRouteKind.NotReached && frameProfile != (route == DumpExpressionRouteKind.FrameValue))
        {
            throw new ArgumentException("Only the frame-value profile can use the frame-value route, and it must use that route.", nameof(route));
        }
        if (route == DumpExpressionRouteKind.OrdinaryQualified)
        {
            var contextFree = frame.Disposition == DumpExpressionCapabilityDisposition.NotRequired &&
                lexical.Disposition == DumpExpressionCapabilityDisposition.NotRequired &&
                pdb.Disposition == DumpExpressionCapabilityDisposition.NotRequired;
            if (!contextFree)
            {
                ValidateCapabilitySequence(
                    nameof(capabilityUses),
                    (frame, false),
                    (pdb, true),
                    (lexical, true));
            }
        }
        if (route == DumpExpressionRouteKind.AliasQualified)
        {
            ValidateCapabilitySequence(nameof(capabilityUses), (frame, false), (pdb, false));
            if (lexical.Disposition != DumpExpressionCapabilityDisposition.NotRequired)
            {
                throw new ArgumentException(
                    "An alias-qualified route must mark selected-method lexical evidence NotRequired.",
                    nameof(capabilityUses));
            }
        }
        if (route == DumpExpressionRouteKind.BareStaticMember)
        {
            ValidateCapabilitySequence(
                nameof(capabilityUses),
                (frame, false),
                (pdb, false),
                (lexical, false));
        }
        if (route == DumpExpressionRouteKind.FrameValue)
        {
            ValidateCapabilitySequence(
                nameof(capabilityUses),
                (frame, false),
                (pdb, false),
                (lexical, false));
        }

        return new DumpExpressionV2ProvenanceIdentity(profile, normalizedDigest, route, uses, globalDiagnostics);
    }

    private static void ValidateCapabilitySequence(
        string parameterName,
        params (DumpExpressionCapabilityUse Use, bool Optional)[] sequence)
    {
        var stopped = false;
        foreach (var (use, optional) in sequence)
        {
            if (stopped)
            {
                if (use.Disposition != DumpExpressionCapabilityDisposition.NotReached)
                {
                    throw new ArgumentException(
                        "A capability after an earlier non-exact stop must be marked NotReached.",
                        parameterName);
                }
                continue;
            }

            switch (use.Disposition)
            {
                case DumpExpressionCapabilityDisposition.Exact:
                    break;
                case DumpExpressionCapabilityDisposition.NonExact:
                    stopped = true;
                    break;
                case DumpExpressionCapabilityDisposition.NotRequired when optional:
                    break;
                case DumpExpressionCapabilityDisposition.NotRequired:
                    throw new ArgumentException(
                        "A route-required capability cannot be marked NotRequired.",
                        parameterName);
                case DumpExpressionCapabilityDisposition.NotReached:
                    throw new ArgumentException(
                        "A capability can be NotReached only after an earlier required or reached capability stopped non-exactly.",
                        parameterName);
                default:
                    throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionV2ProvenanceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionV2ProvenanceIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

internal static class ExpressionV2ContractEncoding
{
    internal static ImmutableArray<T> Copy<T>(ImmutableArray<T> value) => CanonicalReplayEncoding.Copy(value);

    internal static void WriteOptionalBound(
        CanonicalReplayEncoding.Writer writer,
        EvaluationDeterministicBound? bound)
    {
        writer.WriteBoolean(bound is not null);
        if (bound is not null)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }

    internal static ImmutableArray<T> CopyRequired<T>(
        ImmutableArray<T> value,
        string parameterName,
        int maximumCount,
        bool requireExactCount = false)
        where T : class
    {
        if (value.IsDefault || value.Length > maximumCount || requireExactCount && value.Length != maximumCount)
        {
            throw new ArgumentException(
                requireExactCount
                    ? $"An initialized array of exactly {maximumCount} entries is required."
                    : $"An initialized array of at most {maximumCount} entries is required.",
                parameterName);
        }
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] is null)
            {
                throw new ArgumentException("The array cannot contain null entries.", parameterName);
            }
        }
        return Copy(value);
    }

    internal static ImmutableArray<T> CopyRequiredValues<T>(
        ImmutableArray<T> value,
        string parameterName,
        int maximumCount)
        where T : struct
    {
        if (value.IsDefault || value.Length > maximumCount)
        {
            throw new ArgumentException($"An initialized array of at most {maximumCount} entries is required.", parameterName);
        }
        return Copy(value);
    }

    internal static void RequireDefined<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "A defined contract value is required.");
        }
    }

    internal static void RequireText(
        string value,
        string parameterName,
        int maximumLength,
        bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if ((!allowEmpty && value.Length == 0) || value.Length > maximumLength || value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException(
                $"Text must be initialized, NUL-free, and contain {(allowEmpty ? "zero through" : "one through")} {maximumLength} characters.",
                parameterName);
        }
    }

    internal static void RequireIdentifier(string value, string parameterName)
    {
        RequireText(value, parameterName, CSharpExpressionFrontEnd.MaximumIdentifierLength);
        if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(value))
        {
            throw new ArgumentException("A complete decoded C# identifier value is required.", parameterName);
        }
    }

    internal static void RequireDiagnosticCode(string value, string parameterName)
    {
        RequireText(value, parameterName, 96);
        if (!value.StartsWith("W8_", StringComparison.Ordinal) ||
            value.Any(static character => character is not (>= 'A' and <= 'Z') and
                not (>= '0' and <= '9') and not '_'))
        {
            throw new ArgumentException("A stable uppercase ASCII W8 diagnostic code is required.", parameterName);
        }
    }

    internal static void RequireStableName(string value, string parameterName)
    {
        RequireText(value, parameterName, 96);
        if (value[0] is not (>= 'a' and <= 'z') ||
            value.Any(static character => character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and not '.' and not '-'))
        {
            throw new ArgumentException(
                "A lowercase semantic name containing ASCII letters, digits, periods, or hyphens is required.",
                parameterName);
        }
    }

    internal static void WriteCanonicalArray<T>(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<T> values,
        Func<T, ImmutableArray<byte>> getCanonicalBytes)
    {
        ArgumentNullException.ThrowIfNull(getCanonicalBytes);
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            var bytes = getCanonicalBytes(value);
            writer.WriteLengthPrefixedBytes(bytes.AsSpan());
        }
    }

    internal static void WriteOptionalInt32(CanonicalReplayEncoding.Writer writer, int? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteInt32(value.Value);
        }
    }

    internal static void WriteOptionalEnum<T>(CanonicalReplayEncoding.Writer writer, T? value)
        where T : struct, Enum
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteInt32(Convert.ToInt32(value.Value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    internal static void WriteOptionalInt64(CanonicalReplayEncoding.Writer writer, long? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteInt64(value.Value);
        }
    }

    internal static void WriteOptionalUInt64(CanonicalReplayEncoding.Writer writer, ulong? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteUInt64(value.Value);
        }
    }

    internal static void WriteOptionalString(CanonicalReplayEncoding.Writer writer, string? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteString(value);
        }
    }

    internal static void WriteOptionalDigest(CanonicalReplayEncoding.Writer writer, string? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteSha256(value, nameof(value));
        }
    }

    internal static ImmutableArray<EvaluationDeterministicBound> NormalizeReachedBounds(
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        string parameterName)
    {
        var normalized = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, parameterName);
        var declared = ExpressionV2ContractLimits.AllDeclaredBounds.ToDictionary(
            static bound => bound.Name,
            static bound => bound.Value,
            StringComparer.Ordinal);
        foreach (var bound in normalized)
        {
            if (!declared.TryGetValue(bound.Name, out var value) || value != bound.Value)
            {
                throw new ArgumentException("Each reached bound must be one exact shared W8 declaration.", parameterName);
            }
        }
        return normalized;
    }

    internal static void WriteBounds(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        writer.WriteInt32(bounds.Length);
        foreach (var bound in bounds)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }
}
