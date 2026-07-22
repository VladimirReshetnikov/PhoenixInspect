using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises authority-classified TypeDef consumption and metadata-preserving Nullable construction.</summary>
/// <remarks>
/// These headless tests consume semantic-classification rows issued only by the ancestry authority portfolio and do
/// not present the draft contracts as a final compatibility surface.
/// </remarks>
public sealed class W8MetadataClassificationAndNullableContractTests
{
    /// <summary>Proves each exact semantic role has the required physical base-edge evidence before consumption.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_classification_requires_role_specific_physical_proof()
    {
        var fixture = ClassificationFixture.Create();

        Assert.Equal(MetadataTypeDefinitionSemanticRole.Interface, fixture.InterfaceDefinition.Role);
        Assert.Equal(MetadataTypeDefinitionSemanticRole.Class, fixture.ReferenceClassDefinition.Role);
        Assert.Equal(MetadataTypeDefinitionSemanticRole.ValueType, fixture.Int32Definition.Role);
        Assert.Equal(MetadataTypeDefinitionSemanticRole.Enum, fixture.EnumDefinition.Role);
        Assert.Equal(MetadataTypeDefinitionSemanticRole.Delegate, fixture.DelegateDefinition.Role);
        Assert.Equal(
            MetadataImmediateBaseEdgeKind.TypeReference,
            fixture.DelegateDefinition.BaseEdge.Kind);
        Assert.Equal(
            fixture.CoreRoles.SystemMulticastDelegate,
            fixture.DelegateDefinition.BaseEdge.TargetTypeDefinition);
        Assert.Equal(fixture.CoreModule, fixture.DelegateDefinition.BaseEdge.TargetModule);

        var exactDelegateNode = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.DelegateDefinition),
            [fixture.DelegateDefinition]);
        Assert.Equal(
            MetadataTypeConstructionResultKind.Exact,
            MetadataTypeConstructionResult.Classify(exactDelegateNode).Kind);

        Assert.Null(fixture.UnresolvedOuterDefinition.Role);
        Assert.Equal(
            MetadataSemanticClassificationIssue.BaseReferenceUnresolved,
            fixture.UnresolvedOuterDefinition.Issue);
        var unresolvedNode = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.UnresolvedOuterDefinition),
            [fixture.UnresolvedOuterDefinition]);
        Assert.Equal(
            MetadataTypeConstructionResultKind.NonExact,
            MetadataTypeConstructionResult.Classify(unresolvedNode).Kind);

        Assert.Null(fixture.BrokenInterfaceDefinition.Role);
        Assert.Equal(
            MetadataSemanticClassificationIssue.InterfaceExtendsInvalid,
            fixture.BrokenInterfaceDefinition.Issue);

        Assert.Throws<ArgumentException>(() => MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.ValueType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.ReferenceClassDefinition),
            [fixture.ReferenceClassDefinition]));
        Assert.Throws<ArgumentException>(() => MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Int32Definition),
            [fixture.Int32Definition]));

        Assert.Equal(MetadataTypeDefinitionSemanticRole.ModulePseudoType, fixture.ModulePseudoDefinition.Role);
        Assert.Throws<ArgumentException>(() => MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.ModulePseudoDefinition),
            [fixture.ModulePseudoDefinition]));

        var originalDigest = fixture.DelegateDefinition.Sha256;
        var exposedBytes = fixture.DelegateDefinition.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(exposedBytes)![0] ^= 0x7F;
        Assert.Equal(originalDigest, fixture.DelegateDefinition.Sha256);
        Assert.NotEqual(exposedBytes, fixture.DelegateDefinition.CanonicalBytes);
    }

    /// <summary>Proves an unclassified row anywhere in a ground named topology yields typed NonExact evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Unclassified_nested_and_generic_heads_never_yield_exact_construction()
    {
        var fixture = ClassificationFixture.Create();
        var nested = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InnerDefinition),
            [fixture.UnresolvedOuterDefinition, fixture.InnerDefinition]);
        Assert.True(nested.ContainsUnclassifiedTypeDefinition);

        var nestedResult = MetadataTypeConstructionResult.Classify(nested);
        Assert.Equal(MetadataTypeConstructionResultKind.NonExact, nestedResult.Kind);
        Assert.Null(nestedResult.ClosedType);
        Assert.Null(nestedResult.ReachedBound);
        var unclassifiedSegment = MetadataTypeConstructionSegment.Create(
            fixture.UnresolvedOuterDefinition,
            flattenedArgumentOffset: 0,
            ImmutableArray<MetadataClosedTypeIdentity>.Empty);
        Assert.Throws<ArgumentException>(() => MetadataClosedTypeIdentity.Named([unclassifiedSegment]));

        var genericHead = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.GenericDefinition),
            [fixture.GenericDefinition]);
        var generic = MetadataTypeSignatureNode.GenericInstantiation(
            genericHead,
            [MetadataTypeSignatureNode.Primitive(MetadataPrimitiveTypeKind.Int32)]);
        var array = MetadataTypeSignatureNode.SzArray(generic);
        var genericResult = MetadataTypeConstructionResult.Classify(array);
        Assert.Equal(MetadataTypeConstructionResultKind.NonExact, genericResult.Kind);
        Assert.Null(genericResult.ClosedType);
        Assert.Null(genericResult.ReachedBound);

        var exactNamedHead = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.ReferenceClassDefinition),
            [fixture.ReferenceClassDefinition]);
        Assert.Equal(
            MetadataTypeConstructionResultKind.Exact,
            MetadataTypeConstructionResult.Classify(exactNamedHead).Kind);
    }

    /// <summary>Proves Nullable retains its exact metadata head while counting its semantic element topology once.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nullable_preserves_metadata_construction_and_single_element_topology()
    {
        var fixture = ClassificationFixture.Create();
        var nullableHead = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.ValueType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.NullableDefinition),
            [fixture.NullableDefinition]);
        var signature = MetadataTypeSignatureNode.GenericInstantiation(
            nullableHead,
            [MetadataTypeSignatureNode.Primitive(MetadataPrimitiveTypeKind.Int32)]);

        var result = MetadataTypeConstructionResult.Classify(signature);
        var closed = Assert.IsType<MetadataClosedTypeIdentity>(result.ClosedType);
        Assert.Equal(MetadataTypeConstructionResultKind.Exact, result.Kind);
        Assert.Equal(MetadataClosedTypeKind.Nullable, closed.Kind);
        Assert.Equal(fixture.NullableDefinition, closed.FinalClassification);
        Assert.Equal(MetadataTypeDefinitionSemanticRole.ValueType, closed.FinalClassification!.Role);
        Assert.Equal(fixture.CoreModule, closed.FinalClassification.SourceModule);

        var segment = Assert.Single(closed.ConstructionSegments);
        Assert.Equal(fixture.NullableDefinition, segment.Classification);
        Assert.Equal(0, segment.FlattenedArgumentOffset);
        var flattenedArgument = Assert.Single(closed.FlattenedArguments);
        Assert.Equal(flattenedArgument, Assert.Single(segment.LocalArguments));
        Assert.Equal(flattenedArgument, closed.ElementType);
        Assert.Equal(flattenedArgument.TopologyDepth + 1, closed.TopologyDepth);
        Assert.Equal(flattenedArgument.TopologyNodeCount + 1, closed.TopologyNodeCount);
        Assert.Equal(
            MetadataClosedTypeKind.Nullable,
            MetadataClosedTypeIdentity.Named(closed.ConstructionSegments).Kind);

        var exposedArguments = closed.FlattenedArguments;
        ImmutableCollectionsMarshal.AsArray(exposedArguments)![0] =
            MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Boolean);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, Assert.Single(closed.FlattenedArguments).PrimitiveKind);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, closed.ElementType!.PrimitiveKind);
    }

    private sealed class ClassificationFixture
    {
        private ClassificationFixture()
        {
            var core = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
                "Synthetic.Core",
                0x9000,
                'e',
                [
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "System", "Object", (int)(TypeAttributes.Public | TypeAttributes.Class), null),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "System",
                        "ValueType",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
                        0x02000002),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "System",
                        "Enum",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
                        0x02000003),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "System",
                        "Delegate",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
                        0x02000002),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "System",
                        "MulticastDelegate",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
                        0x02000005),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "System",
                        "Int32",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed),
                        0x02000003),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "System",
                        "Nullable`1",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed),
                        0x02000003,
                        GenericArity: 1),
                ]);
            var app = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
                "Synthetic.ClassificationApp",
                0x9100,
                'f',
                [
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "Synthetic",
                        "Calculation",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed),
                        0x01000003),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "Synthetic",
                        "ReferenceNode",
                        (int)(TypeAttributes.Public | TypeAttributes.Class),
                        0x01000001),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "Synthetic",
                        "Mode",
                        (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed),
                        0x01000002),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "Synthetic",
                        "IProject",
                        (int)(TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract),
                        null),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "Synthetic",
                        "IBroken",
                        (int)(TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract),
                        0x01000001),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "Synthetic",
                        "UnresolvedOuter",
                        (int)(TypeAttributes.Public | TypeAttributes.Class),
                        0x01000004),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        string.Empty,
                        "Inner",
                        (int)(TypeAttributes.NestedPublic | TypeAttributes.Class),
                        0x01000001,
                        EnclosingTypeRowId: 7),
                    new W8MetadataAncestryAuthorityContractTests.TypeRow(
                        "Synthetic",
                        "Envelope`1",
                        (int)(TypeAttributes.Public | TypeAttributes.Class),
                        0x01000004,
                        GenericArity: 1),
                ],
                typeReferences: static module =>
                [
                    W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                        module, 1, "System", "Object", 0x23000001),
                    W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                        module, 2, "System", "Enum", 0x23000001),
                    W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                        module, 3, "System", "MulticastDelegate", 0x23000001),
                    W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                        module, 4, "System", "Missing", 0x23000001),
                ],
                assemblyReferences: static module =>
                    [W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Synthetic.Core")]);
            var world = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(core, app);
            Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, world.Ancestry.ResultKind);
            CoreModule = core.Module;
            AppModule = app.Module;
            CoreRoles = Assert.IsType<MetadataCoreRoleSelectionIdentity>(world.Ancestry.CoreRoles);
            Int32Definition = Classification(world, CoreModule, 0x02000007);
            NullableDefinition = Classification(world, CoreModule, 0x02000008);
            ModulePseudoDefinition = Classification(world, AppModule, 0x02000001);
            DelegateDefinition = Classification(world, AppModule, 0x02000002);
            ReferenceClassDefinition = Classification(world, AppModule, 0x02000003);
            EnumDefinition = Classification(world, AppModule, 0x02000004);
            InterfaceDefinition = Classification(world, AppModule, 0x02000005);
            BrokenInterfaceDefinition = Classification(world, AppModule, 0x02000006);
            UnresolvedOuterDefinition = Classification(world, AppModule, 0x02000007);
            InnerDefinition = Classification(world, AppModule, 0x02000008);
            GenericDefinition = Classification(world, AppModule, 0x02000009);
        }

        internal StaticFieldMetadataModuleIdentity CoreModule { get; }
        internal StaticFieldMetadataModuleIdentity AppModule { get; }
        internal MetadataCoreRoleSelectionIdentity CoreRoles { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity Int32Definition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity NullableDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity ModulePseudoDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity DelegateDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity ReferenceClassDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity EnumDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity InterfaceDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity BrokenInterfaceDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity UnresolvedOuterDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity InnerDefinition { get; }
        internal MetadataTypeDefinitionSemanticClassificationIdentity GenericDefinition { get; }

        internal static ClassificationFixture Create() => new();

        private static MetadataTypeDefinitionSemanticClassificationIdentity Classification(
            W8MetadataAncestryAuthorityContractTests.AncestryWorld world,
            StaticFieldMetadataModuleIdentity module,
            int typeDefinitionToken) =>
            Assert.IsType<MetadataTypeDefinitionSemanticClassificationIdentity>(
                world.Ancestry.ExactClassificationOrDefault(module, typeDefinitionToken));
    }
}
