using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises exact W8 TypeDef classification and metadata-preserving Nullable construction.</summary>
/// <remarks>
/// These headless tests use one detached synthetic core-library graph and do not present the draft contracts as a
/// final compatibility surface.
/// </remarks>
public sealed class W8MetadataClassificationAndNullableContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves each exact semantic kind has the required physical flags and ancestry evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_classification_requires_role_specific_physical_proof()
    {
        var fixture = ClassificationFixture.Create();

        Assert.Equal(
            MetadataTypeDefinitionClassificationStatus.Exact,
            fixture.InterfaceDefinition.ClassificationStatus);
        Assert.Equal(
            MetadataTypeDefinitionClassificationStatus.Exact,
            fixture.ReferenceClassDefinition.ClassificationStatus);
        Assert.Equal(
            MetadataTypeDefinitionClassificationStatus.Exact,
            fixture.Int32Definition.ClassificationStatus);
        Assert.Equal(
            MetadataTypeDefinitionClassificationStatus.Exact,
            fixture.EnumDefinition.ClassificationStatus);
        Assert.Equal(
            MetadataTypeDefinitionClassificationStatus.Exact,
            fixture.DelegateDefinition.ClassificationStatus);
        Assert.Equal(fixture.DelegateProof, fixture.DelegateDefinition.DelegateAncestry);
        Assert.Equal(
            fixture.MulticastDelegateType,
            fixture.DelegateDefinition.DelegateAncestry!.SystemMulticastDelegateType);
        Assert.Equal(fixture.DelegateType, fixture.DelegateDefinition.DelegateAncestry.SystemDelegateType);

        var provisionalDelegate = MetadataTypeDefinitionIdentity.FromPinnedW7(
            fixture.DeclaredDelegateType,
            MetadataTypeDefinitionKind.Delegate,
            ImmutableArray<MetadataGenericParameterIdentity>.Empty,
            fixture.DelegateAncestry);
        Assert.Equal(
            MetadataTypeDefinitionClassificationStatus.Provisional,
            provisionalDelegate.ClassificationStatus);
        Assert.Null(provisionalDelegate.DelegateAncestry);
        var exactDelegateNode = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.DelegateDefinition),
            [fixture.DelegateDefinition]);
        Assert.Equal(
            MetadataTypeConstructionResultKind.Exact,
            MetadataTypeConstructionResult.Classify(exactDelegateNode).Kind);
        var provisionalDelegateNode = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(provisionalDelegate),
            [provisionalDelegate]);
        Assert.Equal(
            MetadataTypeConstructionResultKind.NonExact,
            MetadataTypeConstructionResult.Classify(provisionalDelegateNode).Kind);

        var invalidInterface = MetadataRawTypeDefinitionIdentity.Create(
            fixture.Module,
            0x02000020,
            fieldListRowId: 1,
            fieldListEndExclusiveRowId: 1,
            methodListRowId: 1,
            methodListEndExclusiveRowId: 1,
            "Synthetic",
            "IBroken",
            (int)(TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract),
            genericParameterCount: 0,
            extendsMetadataToken: fixture.ObjectType.TypeDefinitionToken);
        Assert.Throws<ArgumentException>(() => MetadataTypeDefinitionIdentity.Create(
            invalidInterface,
            MetadataTypeDefinitionKind.Interface,
            ImmutableArray<MetadataGenericParameterIdentity>.Empty));

        var wrongDelegateAncestry = StaticFieldTypeAncestryIdentity.Create(
            fixture.ReferenceClassType,
            [StaticFieldTypeAncestryEdge.Create(fixture.ReferenceClassType, fixture.ObjectType)],
            fixture.CoreLibrary);
        Assert.Throws<ArgumentException>(() => MetadataDelegateTypeAncestryIdentity.Create(
            wrongDelegateAncestry,
            fixture.MulticastDelegateType,
            fixture.DelegateType));

        var originalDigest = fixture.DelegateProof.Sha256;
        var exposedBytes = fixture.DelegateProof.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(exposedBytes)![0] ^= 0x7F;
        Assert.Equal(originalDigest, fixture.DelegateProof.Sha256);
        Assert.NotEqual(exposedBytes, fixture.DelegateProof.CanonicalBytes);
    }

    /// <summary>Proves a provisional definition anywhere in a ground named topology yields typed NonExact evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Provisional_nested_and_generic_heads_never_yield_exact_construction()
    {
        var fixture = ClassificationFixture.Create();
        var nested = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InnerDefinition),
            [fixture.OuterDefinition, fixture.InnerDefinition]);
        Assert.True(nested.ContainsProvisionalTypeDefinitionClassification);

        var nestedResult = MetadataTypeConstructionResult.Classify(nested);
        Assert.Equal(MetadataTypeConstructionResultKind.NonExact, nestedResult.Kind);
        Assert.Null(nestedResult.ClosedType);
        Assert.Null(nestedResult.ReachedBound);
        var provisionalSegment = MetadataTypeConstructionSegment.Create(
            fixture.OuterDefinition,
            flattenedArgumentOffset: 0,
            ImmutableArray<MetadataClosedTypeIdentity>.Empty);
        Assert.Throws<ArgumentException>(() => MetadataClosedTypeIdentity.Named([provisionalSegment]));

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
        Assert.Equal(fixture.NullableDefinition, closed.FinalDefinition);
        Assert.Equal(
            MetadataTypeDefinitionClassificationStatus.Exact,
            closed.FinalDefinition!.ClassificationStatus);
        Assert.Equal(fixture.Module, closed.FinalDefinition.RawDefinition.MetadataModule);

        var segment = Assert.Single(closed.ConstructionSegments);
        Assert.Equal(fixture.NullableDefinition, segment.Definition);
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
            Module = CreateMetadataModule();
            ObjectType = CreatePinnedType(
                0x02000001,
                "System",
                "Object",
                TypeAttributes.Public | TypeAttributes.Class,
                extendsMetadataToken: null);
            var valueType = CreatePinnedType(
                0x02000002,
                "System",
                "ValueType",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract,
                ObjectType.TypeDefinitionToken);
            var enumType = CreatePinnedType(
                0x02000003,
                "System",
                "Enum",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract,
                valueType.TypeDefinitionToken);
            var selection = StaticFieldCoreLibrarySelectionIdentity.Create(
                StaticFieldCoreLibrarySelectionProvenance.ClrMdRuntimeBaseClassLibrary,
                runtimeOrdinal: 0,
                Module);
            CoreLibrary = StaticFieldCoreLibraryIdentity.Create(
                selection,
                Module,
                ObjectType,
                valueType,
                enumType,
                StaticFieldTypeAncestryEdge.Create(valueType, ObjectType),
                StaticFieldTypeAncestryEdge.Create(enumType, valueType));

            DelegateType = CreatePinnedType(
                0x02000004,
                "System",
                "Delegate",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract,
                ObjectType.TypeDefinitionToken);
            MulticastDelegateType = CreatePinnedType(
                0x02000005,
                "System",
                "MulticastDelegate",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract,
                DelegateType.TypeDefinitionToken);
            DeclaredDelegateType = CreatePinnedType(
                0x02000006,
                "Synthetic",
                "Calculation",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                MulticastDelegateType.TypeDefinitionToken);
            DelegateAncestry = StaticFieldTypeAncestryIdentity.Create(
                DeclaredDelegateType,
                [
                    StaticFieldTypeAncestryEdge.Create(DeclaredDelegateType, MulticastDelegateType),
                    StaticFieldTypeAncestryEdge.Create(MulticastDelegateType, DelegateType),
                    StaticFieldTypeAncestryEdge.Create(DelegateType, ObjectType),
                ],
                CoreLibrary);
            DelegateProof = MetadataDelegateTypeAncestryIdentity.Create(
                DelegateAncestry,
                MulticastDelegateType,
                DelegateType);
            DelegateDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                DeclaredDelegateType,
                MetadataTypeDefinitionKind.Delegate,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                DelegateAncestry,
                DelegateProof);

            ReferenceClassType = CreatePinnedType(
                0x02000007,
                "Synthetic",
                "ReferenceNode",
                TypeAttributes.Public | TypeAttributes.Class,
                ObjectType.TypeDefinitionToken);
            var classAncestry = StaticFieldTypeAncestryIdentity.Create(
                ReferenceClassType,
                [StaticFieldTypeAncestryEdge.Create(ReferenceClassType, ObjectType)],
                CoreLibrary);
            ReferenceClassDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                ReferenceClassType,
                MetadataTypeDefinitionKind.Class,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                classAncestry);

            var int32Type = CreatePinnedType(
                0x02000008,
                "System",
                "Int32",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                valueType.TypeDefinitionToken);
            var int32Ancestry = StaticFieldTypeAncestryIdentity.Create(
                int32Type,
                [StaticFieldTypeAncestryEdge.Create(int32Type, valueType)],
                CoreLibrary);
            Int32Definition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                int32Type,
                MetadataTypeDefinitionKind.ValueType,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                int32Ancestry);

            var concreteEnumType = CreatePinnedType(
                0x02000009,
                "Synthetic",
                "Mode",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                enumType.TypeDefinitionToken);
            var concreteEnumAncestry = StaticFieldTypeAncestryIdentity.Create(
                concreteEnumType,
                [StaticFieldTypeAncestryEdge.Create(concreteEnumType, enumType)],
                CoreLibrary);
            EnumDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                concreteEnumType,
                MetadataTypeDefinitionKind.Enum,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                concreteEnumAncestry);

            var nullableType = CreatePinnedType(
                0x0200000A,
                "System",
                "Nullable`1",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                valueType.TypeDefinitionToken,
                genericParameterCount: 1,
                introducedGenericArity: 1);
            var nullableRaw = MetadataRawTypeDefinitionIdentity.FromPinnedW7(nullableType);
            var nullableAncestry = StaticFieldTypeAncestryIdentity.Create(
                nullableType,
                [StaticFieldTypeAncestryEdge.Create(nullableType, valueType)],
                CoreLibrary);
            NullableDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                nullableType,
                MetadataTypeDefinitionKind.ValueType,
                CreateTypeParameters(nullableRaw, rowStart: 1),
                nullableAncestry);

            var interfaceType = CreatePinnedType(
                0x0200000B,
                "Synthetic",
                "IProject",
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
                extendsMetadataToken: null);
            InterfaceDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                interfaceType,
                MetadataTypeDefinitionKind.Interface,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty);

            var outerType = CreatePinnedType(
                0x0200000C,
                "Synthetic",
                "Outer",
                TypeAttributes.Public | TypeAttributes.Class,
                ObjectType.TypeDefinitionToken);
            OuterDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                outerType,
                MetadataTypeDefinitionKind.Class,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty);
            var innerType = CreatePinnedType(
                0x0200000D,
                string.Empty,
                "Inner",
                TypeAttributes.NestedPublic | TypeAttributes.Class,
                ObjectType.TypeDefinitionToken,
                enclosingType: outerType);
            var innerAncestry = StaticFieldTypeAncestryIdentity.Create(
                innerType,
                [StaticFieldTypeAncestryEdge.Create(innerType, ObjectType)],
                CoreLibrary);
            InnerDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                innerType,
                MetadataTypeDefinitionKind.Class,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                innerAncestry);

            var genericType = CreatePinnedType(
                0x0200000E,
                "Synthetic",
                "Envelope`1",
                TypeAttributes.Public | TypeAttributes.Class,
                ObjectType.TypeDefinitionToken,
                genericParameterCount: 1,
                introducedGenericArity: 1);
            var genericRaw = MetadataRawTypeDefinitionIdentity.FromPinnedW7(genericType);
            GenericDefinition = MetadataTypeDefinitionIdentity.FromPinnedW7(
                genericType,
                MetadataTypeDefinitionKind.Class,
                CreateTypeParameters(genericRaw, rowStart: 2));
        }

        internal StaticFieldMetadataModuleIdentity Module { get; }
        internal StaticFieldCoreLibraryIdentity CoreLibrary { get; }
        internal StaticFieldTypeDefinitionIdentity ObjectType { get; }
        internal StaticFieldTypeDefinitionIdentity DelegateType { get; }
        internal StaticFieldTypeDefinitionIdentity MulticastDelegateType { get; }
        internal StaticFieldTypeDefinitionIdentity DeclaredDelegateType { get; }
        internal StaticFieldTypeAncestryIdentity DelegateAncestry { get; }
        internal MetadataDelegateTypeAncestryIdentity DelegateProof { get; }
        internal MetadataTypeDefinitionIdentity InterfaceDefinition { get; }
        internal StaticFieldTypeDefinitionIdentity ReferenceClassType { get; }
        internal MetadataTypeDefinitionIdentity ReferenceClassDefinition { get; }
        internal MetadataTypeDefinitionIdentity Int32Definition { get; }
        internal MetadataTypeDefinitionIdentity EnumDefinition { get; }
        internal MetadataTypeDefinitionIdentity DelegateDefinition { get; }
        internal MetadataTypeDefinitionIdentity NullableDefinition { get; }
        internal MetadataTypeDefinitionIdentity OuterDefinition { get; }
        internal MetadataTypeDefinitionIdentity InnerDefinition { get; }
        internal MetadataTypeDefinitionIdentity GenericDefinition { get; }

        internal static ClassificationFixture Create() => new();

        private StaticFieldTypeDefinitionIdentity CreatePinnedType(
            int token,
            string namespaceName,
            string name,
            TypeAttributes attributes,
            int? extendsMetadataToken,
            int genericParameterCount = 0,
            int introducedGenericArity = 0,
            StaticFieldTypeDefinitionIdentity? enclosingType = null) =>
            StaticFieldTypeDefinitionIdentity.Create(
                Module,
                token,
                fieldListRowId: 1,
                fieldListEndExclusiveRowId: 1,
                methodListRowId: 1,
                methodListEndExclusiveRowId: 1,
                namespaceName,
                name,
                (int)attributes,
                genericParameterCount,
                introducedGenericArity,
                extendsMetadataToken,
                enclosingType);

        private static ImmutableArray<MetadataGenericParameterIdentity> CreateTypeParameters(
            MetadataRawTypeDefinitionIdentity raw,
            int rowStart)
        {
            var owner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(raw);
            return Enumerable.Range(0, raw.GenericParameterCount)
                .Select(position => MetadataGenericParameterIdentity.Create(
                    0x2A000000 | checked(rowStart + position),
                    owner,
                    position,
                    $"T{position}",
                    attributes: 0))
                .ToImmutableArray();
        }

        private static StaticFieldMetadataModuleIdentity CreateMetadataModule()
        {
            var module = StaticFieldModuleInstanceIdentity.Create(
                SnapshotDigest,
                sizeof(ulong),
                applicationDomainAddress: 0x1000,
                moduleAddress: 0x2000,
                imageBase: 0x402000,
                imageSize: 0x18000);
            var content = ModuleContentIdentity.FromDigest(
                Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
                metadataLength: 24_576,
                new string('b', 64));
            var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
                generation: 0,
                "classification-fixture.dll",
                content.Mvid,
                Guid.Empty,
                Guid.Empty);
            var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
                "System.Private.CoreLib",
                8,
                0,
                0,
                0,
                string.Empty,
                flags: 0,
                hashAlgorithm: 0x8004,
                ImmutableArray<byte>.Empty);
            var assembly = StaticFieldContainingAssemblyIdentity.Create(
                module,
                content,
                moduleDefinition,
                assemblyDefinition);
            return StaticFieldMetadataModuleIdentity.ForManifestModule(
                module,
                content,
                moduleDefinition,
                assembly);
        }
    }
}
