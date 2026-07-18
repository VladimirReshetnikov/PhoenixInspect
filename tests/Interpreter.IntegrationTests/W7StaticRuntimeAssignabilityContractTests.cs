using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises Product-owned W7 runtime assignability over exact synthetic metadata, raw object headers, boxed values,
/// constructed generic interface closure, and TypeDef-less arrays without invoking a live runtime or metadata reader.
/// </summary>
[Trait("Category", "Fast")]
public sealed class W7StaticRuntimeAssignabilityContractTests
{
    private const int PointerWidth = sizeof(ulong);
    private const ulong ApplicationDomainAddress = 0x1000;
    private const ulong ObjectAddress = 0x9000;
    private static readonly ClrmdSnapshotIdentity Snapshot = new(new string('a', 64));
    private static readonly ImmutableArray<EvaluationDeterministicBound> SyntaxBounds =
    [
        new("query.expression.characters", 512),
        new("query.syntax.nodes-and-tokens", 256),
        new("query.syntax.depth", 64),
    ];
    private static readonly ImmutableArray<EvaluationDeterministicBound> BindingBounds =
    [
        new("binding.modules.count", 64),
        new("binding.typedef-rows.count", 4096),
        new("binding.fielddef-rows.count", 16384),
        StaticFieldTypeAncestryIdentity.DeclaredEdgeCountBound,
    ];

    /// <summary>Proves exact, base, Object, boxed ValueType, and strict String routes from raw header identities.</summary>
    [Fact]
    public void Type_definition_routes_cover_exact_base_object_boxing_and_string()
    {
        var fixture = CreateFixture(interfaceImplementationRowCount: 0, typeSpecificationRowCount: 0);
        var emptyCatalogs = ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty;
        var facts = ImmutableArray.Create(fixture.ModuleFact);

        var derivedObject = ExactObject(fixture.RuntimeType(fixture.DerivedAncestry, 0x7100));
        var exactDeclaration = fixture.ReferenceDeclaration(
            fixture.DerivedAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 1);
        var exact = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            exactDeclaration,
            derivedObject,
            fixture.DerivedAncestry,
            emptyCatalogs,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.ExactRuntimeType, exact.Kind);

        var baseDeclaration = fixture.ReferenceDeclaration(
            fixture.BaseAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 2);
        var viaBase = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            baseDeclaration,
            derivedObject,
            fixture.DerivedAncestry,
            emptyCatalogs,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.BaseType, viaBase.Kind);

        var objectDeclaration = fixture.ObjectDeclaration(fieldRowId: 3);
        var viaObject = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            objectDeclaration,
            derivedObject,
            fixture.DerivedAncestry,
            emptyCatalogs,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.SystemObject, viaObject.Kind);

        var boxedInt32 = ExactObject(fixture.RuntimeType(fixture.Int32Ancestry, 0x7200, isPrimitive: true));
        var boxedObject = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            objectDeclaration,
            boxedInt32,
            fixture.Int32Ancestry,
            emptyCatalogs,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.SystemObject, boxedObject.Kind);
        var valueTypeDeclaration = fixture.ReferenceDeclaration(
            fixture.ValueTypeAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 4);
        var boxedValueType = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            valueTypeDeclaration,
            boxedInt32,
            fixture.Int32Ancestry,
            emptyCatalogs,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.BaseType, boxedValueType.Kind);

        var stringDeclaration = fixture.StringDeclaration(fieldRowId: 5);
        var stringObject = ExactObject(fixture.RuntimeType(fixture.StringAncestry, 0x7300));
        var exactString = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            stringDeclaration,
            stringObject,
            fixture.StringAncestry,
            emptyCatalogs,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.ExactRuntimeType, exactString.Kind);
        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            stringDeclaration,
            derivedObject,
            fixture.DerivedAncestry,
            emptyCatalogs,
            facts));

        var foreignCoreFixture = CreateFixture(
            interfaceImplementationRowCount: 0,
            typeSpecificationRowCount: 0,
            moduleAddress: 0x6000,
            contentDigestCharacter: 'c');
        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            objectDeclaration,
            ExactObject(foreignCoreFixture.RuntimeType(foreignCoreFixture.DerivedAncestry, 0x7400)),
            foreignCoreFixture.DerivedAncestry,
            emptyCatalogs,
            [fixture.ModuleFact, foreignCoreFixture.ModuleFact]));

        Assert.Empty(exact.InterfacePath);
        Assert.Empty(exact.InterfaceCatalogs);
        Assert.Equal(fixture.DerivedType, exact.ActualTypeAncestry!.SubjectType);
        Assert.True(exact.CanonicalBytes.AsSpan().SequenceEqual(
            StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
                exactDeclaration,
                derivedObject,
                fixture.DerivedAncestry,
                emptyCatalogs,
                facts).CanonicalBytes.AsSpan()));
    }

    /// <summary>
    /// Proves a complete class/base/interface closure traverses a constructed generic TypeSpec intermediary,
    /// canonicalizes catalog order, rejects missing owner scans, and detects a reachable interface cycle.
    /// </summary>
    [Fact]
    public void Interface_closure_is_complete_generic_order_independent_and_cycle_rejecting()
    {
        var fixture = CreateFixture(interfaceImplementationRowCount: 2, typeSpecificationRowCount: 1);
        var envelopeToLeaf = fixture.EnvelopeToConstructedLeaf(0x09000001);
        var leafToMarker = fixture.LeafToMarker(0x09000002);
        var catalogs = fixture.InterfaceCatalogs(envelopeToLeaf, leafToMarker);
        var declaration = fixture.ReferenceDeclaration(
            fixture.MarkerAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 6);
        var objectReference = ExactObject(fixture.RuntimeType(fixture.EnvelopeAncestry, 0x7500));
        var facts = ImmutableArray.Create(fixture.ModuleFact);

        var proof = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            declaration,
            objectReference,
            fixture.EnvelopeAncestry,
            catalogs,
            facts);
        var replay = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            declaration,
            objectReference,
            fixture.EnvelopeAncestry,
            catalogs.Reverse().ToImmutableArray(),
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.InterfaceClosure, proof.Kind);
        Assert.Equal(
            new[] { fixture.EnvelopeType, fixture.LeafInterface, fixture.MarkerInterface },
            proof.InterfacePath);
        Assert.Equal(proof, replay);
        Assert.True(proof.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));

        var returnedCatalogs = proof.InterfaceCatalogs;
        ImmutableCollectionsMarshal.AsArray(returnedCatalogs)![0] = catalogs[0];
        Assert.Equal(5, proof.InterfaceCatalogs.Length);
        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            declaration,
            objectReference,
            fixture.EnvelopeAncestry,
            catalogs.Where(catalog => !catalog.ImplementingType.Equals(fixture.ObjectType)).ToImmutableArray(),
            facts));
        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            fixture.ReferenceDeclaration(
                fixture.LeafAncestry,
                StaticFieldDeclaredValueKind.ManagedReference,
                fieldRowId: 7),
            objectReference,
            fixture.EnvelopeAncestry,
            catalogs,
            facts));

        var cyclicFixture = CreateFixture(interfaceImplementationRowCount: 3, typeSpecificationRowCount: 2);
        var cyclicEnvelopeToLeaf = cyclicFixture.EnvelopeToConstructedLeaf(0x09000001);
        var cyclicLeafToMarker = cyclicFixture.LeafToMarker(0x09000002);
        var markerToLeaf = StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            cyclicFixture.MetadataModule,
            0x09000003,
            cyclicFixture.MarkerInterface,
            0x1B000002,
            [0x15, 0x12, 0x28, 0x01, 0x08],
            genericHeadTypeReferenceResolution: null,
            cyclicFixture.LeafAncestry);
        var cyclicCatalogs = cyclicFixture.InterfaceCatalogs(
            cyclicEnvelopeToLeaf,
            cyclicLeafToMarker,
            markerToLeaf);
        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            cyclicFixture.ReferenceDeclaration(
                cyclicFixture.MarkerAncestry,
                StaticFieldDeclaredValueKind.ManagedReference,
                fieldRowId: 6),
            ExactObject(cyclicFixture.RuntimeType(cyclicFixture.EnvelopeAncestry, 0x7600)),
            cyclicFixture.EnvelopeAncestry,
            cyclicCatalogs,
            [cyclicFixture.ModuleFact]));
    }

    /// <summary>
    /// Proves TypeDef-less arrays flow to exact Object, System.Array, and a transitive non-generic interface while
    /// structural core-library lookalikes and incomplete runtime-interface anchors remain non-exact.
    /// </summary>
    [Fact]
    public void Array_routes_correlate_core_library_and_transitive_interfaces()
    {
        var fixture = CreateFixture(interfaceImplementationRowCount: 1, typeSpecificationRowCount: 0);
        var leafToMarker = fixture.LeafToMarker(0x09000001);
        var leafCatalog = StaticFieldInterfaceImplementationCatalogIdentity.Create(
            fixture.LeafInterface,
            fixture.ModuleFact,
            [leafToMarker]);
        var markerCatalog = StaticFieldInterfaceImplementationCatalogIdentity.Create(
            fixture.MarkerInterface,
            fixture.ModuleFact,
            ImmutableArray<StaticFieldInterfaceImplementationRowIdentity>.Empty);
        var runtimeInt32 = fixture.RuntimeType(fixture.Int32Ancestry, 0x7700, isPrimitive: true);
        var runtimeArrayBase = fixture.RuntimeType(fixture.ArrayAncestry, 0x7710);
        var runtimeLeaf = fixture.RuntimeType(
            fixture.LeafAncestry,
            0x7720,
            genericArguments: [runtimeInt32]);
        var runtimeArray = ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            PointerWidth,
            "System.Int32[]",
            0x7800,
            rank: 1,
            isSzArray: true,
            runtimeInt32,
            runtimeArrayBase,
            [runtimeLeaf]);
        var arrayObject = ExactObject(runtimeArray);
        var facts = ImmutableArray.Create(fixture.ModuleFact);

        var objectProof = StaticFieldRuntimeAssignabilityProof.ForArray(
            fixture.ObjectDeclaration(fieldRowId: 3),
            arrayObject,
            fixture.ArrayAncestry,
            ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty,
            ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.SystemObject, objectProof.Kind);
        Assert.Equal(DumpObjectRuntimeTypeKind.Array, DumpObjectIdentity.FromExactObject(arrayObject).RuntimeTypeKind);

        var arrayDeclaration = fixture.ReferenceDeclaration(
            fixture.ArrayAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 8);
        var arrayProof = StaticFieldRuntimeAssignabilityProof.ForArray(
            arrayDeclaration,
            arrayObject,
            fixture.ArrayAncestry,
            ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty,
            ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty,
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.SystemArray, arrayProof.Kind);

        var markerDeclaration = fixture.ReferenceDeclaration(
            fixture.MarkerAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 6);
        var interfaceProof = StaticFieldRuntimeAssignabilityProof.ForArray(
            markerDeclaration,
            arrayObject,
            fixture.ArrayAncestry,
            [fixture.LeafAncestry],
            [leafCatalog, markerCatalog],
            facts);
        Assert.Equal(StaticFieldRuntimeAssignabilityKind.InterfaceClosure, interfaceProof.Kind);
        Assert.Equal(
            new[] { fixture.LeafInterface, fixture.MarkerInterface },
            interfaceProof.InterfacePath);

        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForArray(
            fixture.StringDeclaration(fieldRowId: 5),
            arrayObject,
            fixture.ArrayAncestry,
            ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty,
            ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty,
            facts));
        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForArray(
            markerDeclaration,
            arrayObject,
            fixture.ArrayAncestry,
            ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty,
            [leafCatalog, markerCatalog],
            facts));

        var lookalikeArrayBase = ClrmdStaticRuntimeTypeIdentity.Create(
            Snapshot,
            PointerWidth,
            fixture.RuntimeModule,
            fixture.Content,
            fixture.BaseType.TypeDefinitionToken,
            "System.Array",
            0x7790,
            isValueType: false,
            isPrimitive: false,
            isArray: false,
            isInterface: false,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty);
        var lookalikeArray = ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            PointerWidth,
            "System.Int32[]",
            0x7810,
            1,
            true,
            runtimeInt32,
            lookalikeArrayBase,
            [runtimeLeaf]);
        Assert.Throws<ArgumentException>(() => StaticFieldRuntimeAssignabilityProof.ForArray(
            fixture.ObjectDeclaration(fieldRowId: 3),
            ExactObject(lookalikeArray, ObjectAddress + 0x100),
            fixture.ArrayAncestry,
            ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty,
            ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty,
            facts));
    }

    /// <summary>
    /// Proves a composed exact static object requires the matching Product proof, a null reference requires none,
    /// and successful composition flows into ordinary source-agnostic object identity and typed provenance.
    /// </summary>
    [Fact]
    public void Static_observation_requires_assignability_only_after_a_matched_non_null_target()
    {
        const ulong slotAddress = 0x8000;
        var fixture = CreateFixture(interfaceImplementationRowCount: 2, typeSpecificationRowCount: 1);
        var declaration = fixture.ReferenceDeclaration(
            fixture.MarkerAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 6);
        var binding = fixture.ExactBinding(declaration);
        var runtimeOwner = fixture.RuntimeType(fixture.HolderAncestry, 0x7900);
        var runtimeDeclaredInterface = fixture.RuntimeType(fixture.MarkerAncestry, 0x7910);
        var request = StaticFieldObservation.CreatePhysicalRequest(
            binding,
            runtimeOwner,
            declaration.FieldDefinitionToken,
            declaration.FieldName,
            (FieldAttributes)declaration.FieldAttributes,
            runtimeReportsThreadStatic: false,
            runtimeReportsContextStatic: false,
            runtimeDeclaredInterface,
            ClrmdStaticRuntimeDeclarationMappingCounters.Create(12, 8, 1, 1, true, true));
        Assert.Contains(
            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphDepthBound,
            request.CanonicalBounds);
        Assert.Contains(
            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphNodeCountBound,
            request.CanonicalBounds);
        Assert.DoesNotContain(
            ClrmdStaticRuntimeTypeIdentity.DeclaredArrayRankBound,
            request.CanonicalBounds);
        var acquisition = ClrmdStaticStorageAcquisitionEvidence.Acquired(
            PointerWidth,
            ApplicationDomainAddress,
            applicationDomainCatalogCardinality: 1,
            matchingApplicationDomainOrdinal: 0,
            slotAddress,
            request.StorageSize);
        var actualObject = ExactObject(fixture.RuntimeType(fixture.EnvelopeAncestry, 0x7920));
        var slotRead = PointerRead(slotAddress, actualObject.Address);
        var hostObservation = ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            [slotRead, actualObject.HeaderEvidence],
            ClrmdStaticFieldValue.ExactObjectReference(actualObject),
            PhysicalReadBounds);
        var catalogs = fixture.InterfaceCatalogs(
            fixture.EnvelopeToConstructedLeaf(0x09000001),
            fixture.LeafToMarker(0x09000002));
        var proof = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            declaration,
            actualObject,
            fixture.EnvelopeAncestry,
            catalogs,
            [fixture.ModuleFact]);

        Assert.Throws<ArgumentException>(() => StaticFieldObservation.FromExactSymbol(binding, hostObservation));
        var composition = StaticFieldObservation.FromExactSymbol(
            binding,
            hostObservation,
            nullableInt32RuntimeLayout: null,
            proof);
        Assert.Equal(proof, composition.RuntimeAssignabilityProof);
        Assert.Null(composition.HostObservation!.TargetEvidence);
        Assert.Equal(actualObject, composition.HostObservation.Value!.ObjectReference);

        var source = DumpStaticFieldExpressionSourceIdentity.Create(composition);
        var identity = DumpObjectIdentity.FromExactObject(actualObject);
        var provenance = DumpObjectProvenance.FromStaticFieldExpression(source);
        var objectBinding = DumpObjectBinding.Create(identity, provenance);
        Assert.Equal(identity, objectBinding.Identity);
        Assert.Equal(DumpObjectProvenanceKind.StaticFieldExpression, objectBinding.Provenance.Kind);

        var nullSlotRead = PointerRead(slotAddress, 0);
        var nullHostObservation = ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            [nullSlotRead],
            ClrmdStaticFieldValue.NullReference(),
            PhysicalReadBounds);
        var nullComposition = StaticFieldObservation.FromExactSymbol(binding, nullHostObservation);
        Assert.Null(nullComposition.RuntimeAssignabilityProof);
        Assert.Null(nullComposition.HostObservation!.TargetEvidence);

        var wrongDeclaration = fixture.ReferenceDeclaration(
            fixture.BaseAncestry,
            StaticFieldDeclaredValueKind.ManagedReference,
            fieldRowId: 2);
        var wrongProof = StaticFieldRuntimeAssignabilityProof.ForTypeDefinition(
            wrongDeclaration,
            actualObject,
            fixture.EnvelopeAncestry,
            ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty,
            [fixture.ModuleFact]);
        Assert.Throws<ArgumentException>(() => StaticFieldObservation.FromExactSymbol(
            binding,
            hostObservation,
            nullableInt32RuntimeLayout: null,
            wrongProof));
    }

    /// <summary>
    /// Proves intrinsic object identity is independent of selection source while each strong-handle or explicit host
    /// boundary remains typed, physically correlated, and distinct in binding identity.
    /// </summary>
    [Fact]
    public void Object_provenance_is_source_agnostic_typed_and_physically_correlated()
    {
        const ulong handleSlotAddress = 0xA000;
        var fixture = CreateFixture(interfaceImplementationRowCount: 0, typeSpecificationRowCount: 0);
        var exactObject = ExactObject(fixture.RuntimeType(fixture.DerivedAncestry, 0x7A00));
        var identity = DumpObjectIdentity.FromExactObject(exactObject);
        var slotEvidence = PointerRead(handleSlotAddress, identity.Address);
        var strongSource = DumpStrongHandleSourceIdentity.Create(
            identity,
            exactObject,
            PointerWidth,
            handleSlotAddress,
            DumpStrongHandleKind.Strong,
            referenceCount: 0,
            slotEvidence);
        var strongBinding = DumpObjectBinding.Create(
            identity,
            DumpObjectProvenance.FromStrongHandle(strongSource));

        var hostSource = DumpHostSuppliedObjectSourceIdentity.Create(identity, "synthetic.exact-object-boundary");
        var hostBinding = DumpObjectBinding.Create(
            identity,
            DumpObjectProvenance.FromHostSuppliedExactObject(hostSource));

        Assert.Equal(identity, strongBinding.Identity);
        Assert.Equal(identity, hostBinding.Identity);
        Assert.NotEqual(strongBinding, hostBinding);
        Assert.Equal(DumpObjectProvenanceKind.StrongHandle, strongBinding.Provenance.Kind);
        Assert.Equal(DumpObjectProvenanceKind.HostSuppliedExactObject, hostBinding.Provenance.Kind);
        Assert.Null(strongBinding.Provenance.HostSupplied);
        Assert.Null(hostBinding.Provenance.StrongHandle);

        Assert.Throws<ArgumentException>(() => DumpStrongHandleSourceIdentity.Create(
            identity,
            exactObject,
            PointerWidth,
            handleSlotAddress,
            DumpStrongHandleKind.Strong,
            referenceCount: 0,
            PointerRead(handleSlotAddress, identity.Address + 1)));
        Assert.Throws<ArgumentException>(() => DumpStrongHandleSourceIdentity.Create(
            identity,
            exactObject,
            PointerWidth,
            handleSlotAddress,
            DumpStrongHandleKind.RefCounted,
            referenceCount: 0,
            slotEvidence));

        var otherObject = ExactObject(
            fixture.RuntimeType(fixture.DerivedAncestry, 0x7A00),
            ObjectAddress + 0x200);
        Assert.Throws<ArgumentException>(() => DumpObjectBinding.Create(
            DumpObjectIdentity.FromExactObject(otherObject),
            strongBinding.Provenance));
    }

    /// <summary>Proves complete table, fact, catalog, and retained-row caps cannot be relabeled as exact evidence.</summary>
    [Fact]
    public void Assignability_catalogs_enforce_exact_bounds_and_physical_owners()
    {
        var overBound = CreateFixture(
            interfaceImplementationRowCount:
                StaticFieldInterfaceImplementationCatalogIdentity.MaximumInterfaceImplementationRowsExamined + 1,
            typeSpecificationRowCount: 0);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StaticFieldInterfaceImplementationCatalogIdentity.Create(
                overBound.ObjectType,
                overBound.ModuleFact,
                ImmutableArray<StaticFieldInterfaceImplementationRowIdentity>.Empty));

        var fixture = CreateFixture(interfaceImplementationRowCount: 1, typeSpecificationRowCount: 0);
        var leafToMarker = fixture.LeafToMarker(0x09000001);
        Assert.Throws<ArgumentException>(() => StaticFieldInterfaceImplementationCatalogIdentity.Create(
            fixture.MarkerInterface,
            fixture.ModuleFact,
            [leafToMarker]));
        Assert.Throws<ArgumentException>(() => StaticFieldInterfaceImplementationCatalogIdentity.Create(
            fixture.LeafInterface,
            fixture.ModuleFact,
            [leafToMarker, leafToMarker]));

        Assert.Equal(
            new EvaluationDeterministicBound(
                StaticFieldRuntimeAssignabilityProof.MaximumInterfaceClosureNodeCountBoundName,
                256),
            StaticFieldRuntimeAssignabilityProof.DeclaredInterfaceClosureNodeCountBound);
        Assert.Equal(
            new EvaluationDeterministicBound(
                StaticFieldInterfaceImplementationCatalogIdentity.MaximumInterfaceImplementationRowsExaminedBoundName,
                4096),
            StaticFieldInterfaceImplementationCatalogIdentity.DeclaredRowsExaminedBound);
    }

    private static ClrmdExactObjectReference ExactObject(
        ClrmdStaticRuntimeTypeIdentity runtimeType,
        ulong address = ObjectAddress)
    {
        var methodTable = runtimeType.MethodTable!.Value;
        var bytes = new byte[PointerWidth];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, methodTable);
        var header = ClrmdRawMemoryEvidence.Exact(Snapshot, address, ImmutableArray.Create(bytes));
        return ClrmdExactObjectReference.Create(ClrmdStaticTargetEvidence.Matched(
            Snapshot,
            PointerWidth,
            address,
            header,
            runtimeType));
    }

    private static ClrmdRawMemoryEvidence PointerRead(ulong address, ulong value)
    {
        var bytes = new byte[PointerWidth];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return ClrmdRawMemoryEvidence.Exact(Snapshot, address, ImmutableArray.Create(bytes));
    }

    private static ImmutableArray<EvaluationDeterministicBound> PhysicalReadBounds =>
    [
        ClrmdStaticStorageAcquisitionEvidence.DeclaredApplicationDomainCountBound,
        ClrmdStaticFieldValueObservation.DeclaredRawReadCountBound,
    ];

    private static Fixture CreateFixture(
        int interfaceImplementationRowCount,
        int typeSpecificationRowCount,
        ulong moduleAddress = 0x4000,
        char contentDigestCharacter = 'b') =>
        new(interfaceImplementationRowCount, typeSpecificationRowCount, moduleAddress, contentDigestCharacter);

    private sealed class Fixture
    {
        internal Fixture(
            int interfaceImplementationRowCount,
            int typeSpecificationRowCount,
            ulong moduleAddress,
            char contentDigestCharacter)
        {
            Content = ModuleContentIdentity.FromDigest(
                Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
                32_768,
                new string(contentDigestCharacter, 64));
            Module = StaticFieldModuleInstanceIdentity.Create(
                Snapshot.Sha256,
                PointerWidth,
                ApplicationDomainAddress,
                moduleAddress,
                imageBase: 0x0040_0000 + moduleAddress,
                imageSize: 0x0004_0000);
            RuntimeModule = new ClrmdRuntimeModuleIdentity(
                Snapshot,
                ApplicationDomainAddress,
                moduleAddress,
                Module.ImageBase,
                Module.ImageSize);
            var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
                generation: 0,
                $"synthetic-{moduleAddress:x}.dll",
                Content.Mvid,
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
            var containingAssembly = StaticFieldContainingAssemblyIdentity.Create(
                Module,
                Content,
                moduleDefinition,
                assemblyDefinition);
            MetadataModule = StaticFieldMetadataModuleIdentity.ForManifestModule(
                Module,
                Content,
                moduleDefinition,
                containingAssembly);

            ObjectType = Type(1, "System", "Object", TypeAttributes.Public | TypeAttributes.Class, 0, null);
            ValueType = Type(2, "System", "ValueType", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract, 0, 0x02000001);
            EnumType = Type(3, "System", "Enum", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract, 0, 0x02000002);
            ArrayType = Type(4, "System", "Array", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract, 0, 0x02000001);
            StringType = Type(5, "System", "String", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, 0, 0x02000001);
            Int32Type = Type(6, "System", "Int32", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, 0, 0x02000002);
            BaseType = Type(7, "Synthetic", "Base", TypeAttributes.Public | TypeAttributes.Class, 0, 0x02000001);
            DerivedType = Type(8, "Synthetic", "Derived", TypeAttributes.Public | TypeAttributes.Class, 0, 0x02000007);
            MarkerInterface = Type(9, "Synthetic", "IMarker", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, 0, null);
            LeafInterface = Type(10, "Synthetic", "ILeaf`1", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, 1, null);
            EnvelopeType = Type(11, "Synthetic", "Envelope", TypeAttributes.Public | TypeAttributes.Class, 0, 0x02000007);
            HolderType = Type(
                12,
                "Synthetic",
                "Holder",
                TypeAttributes.Public | TypeAttributes.Class,
                0,
                0x02000001,
                fieldListRowId: 1,
                fieldListEndExclusiveRowId: 9);

            var objectEdge = StaticFieldTypeAncestryEdge.Create(ValueType, ObjectType);
            var enumEdge = StaticFieldTypeAncestryEdge.Create(EnumType, ValueType);
            var selection = StaticFieldCoreLibrarySelectionIdentity.Create(
                StaticFieldCoreLibrarySelectionProvenance.ClrMdRuntimeBaseClassLibrary,
                runtimeOrdinal: 0,
                MetadataModule);
            CoreLibrary = StaticFieldCoreLibraryIdentity.Create(
                selection,
                MetadataModule,
                ObjectType,
                ValueType,
                EnumType,
                objectEdge,
                enumEdge);
            ObjectAncestry = StaticFieldTypeAncestryIdentity.Create(
                ObjectType,
                ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
                CoreLibrary);
            ValueTypeAncestry = StaticFieldTypeAncestryIdentity.Create(
                ValueType,
                ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
                CoreLibrary);
            ArrayAncestry = StaticFieldTypeAncestryIdentity.Create(
                ArrayType,
                [StaticFieldTypeAncestryEdge.Create(ArrayType, ObjectType)],
                CoreLibrary);
            StringAncestry = StaticFieldTypeAncestryIdentity.Create(
                StringType,
                [StaticFieldTypeAncestryEdge.Create(StringType, ObjectType)],
                CoreLibrary);
            Int32Ancestry = StaticFieldTypeAncestryIdentity.Create(
                Int32Type,
                [StaticFieldTypeAncestryEdge.Create(Int32Type, ValueType)],
                CoreLibrary);
            BaseAncestry = StaticFieldTypeAncestryIdentity.Create(
                BaseType,
                [StaticFieldTypeAncestryEdge.Create(BaseType, ObjectType)],
                CoreLibrary);
            DerivedAncestry = StaticFieldTypeAncestryIdentity.Create(
                DerivedType,
                [
                    StaticFieldTypeAncestryEdge.Create(DerivedType, BaseType),
                    StaticFieldTypeAncestryEdge.Create(BaseType, ObjectType),
                ],
                CoreLibrary);
            MarkerAncestry = StaticFieldTypeAncestryIdentity.Create(
                MarkerInterface,
                ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
                coreLibrary: null);
            LeafAncestry = StaticFieldTypeAncestryIdentity.Create(
                LeafInterface,
                ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
                coreLibrary: null);
            EnvelopeAncestry = StaticFieldTypeAncestryIdentity.Create(
                EnvelopeType,
                [
                    StaticFieldTypeAncestryEdge.Create(EnvelopeType, BaseType),
                    StaticFieldTypeAncestryEdge.Create(BaseType, ObjectType),
                ],
                CoreLibrary);
            HolderAncestry = StaticFieldTypeAncestryIdentity.Create(
                HolderType,
                [StaticFieldTypeAncestryEdge.Create(HolderType, ObjectType)],
                CoreLibrary);
            ModuleFact = StaticFieldModuleSearchFact.Exact(
                Module,
                Content,
                typeDefinitionsExamined: 12,
                fieldDefinitionsExamined: 8,
                typeSpecificationRowCount: typeSpecificationRowCount,
                interfaceImplementationRowCount: interfaceImplementationRowCount,
                genericParameterRowCount: 1);
        }

        internal ModuleContentIdentity Content { get; }
        internal StaticFieldModuleInstanceIdentity Module { get; }
        internal ClrmdRuntimeModuleIdentity RuntimeModule { get; }
        internal StaticFieldMetadataModuleIdentity MetadataModule { get; }
        internal StaticFieldCoreLibraryIdentity CoreLibrary { get; }
        internal StaticFieldModuleSearchFact ModuleFact { get; }
        internal StaticFieldTypeDefinitionIdentity ObjectType { get; }
        internal StaticFieldTypeDefinitionIdentity ValueType { get; }
        internal StaticFieldTypeDefinitionIdentity EnumType { get; }
        internal StaticFieldTypeDefinitionIdentity ArrayType { get; }
        internal StaticFieldTypeDefinitionIdentity StringType { get; }
        internal StaticFieldTypeDefinitionIdentity Int32Type { get; }
        internal StaticFieldTypeDefinitionIdentity BaseType { get; }
        internal StaticFieldTypeDefinitionIdentity DerivedType { get; }
        internal StaticFieldTypeDefinitionIdentity MarkerInterface { get; }
        internal StaticFieldTypeDefinitionIdentity LeafInterface { get; }
        internal StaticFieldTypeDefinitionIdentity EnvelopeType { get; }
        internal StaticFieldTypeDefinitionIdentity HolderType { get; }
        internal StaticFieldTypeAncestryIdentity ObjectAncestry { get; }
        internal StaticFieldTypeAncestryIdentity ValueTypeAncestry { get; }
        internal StaticFieldTypeAncestryIdentity ArrayAncestry { get; }
        internal StaticFieldTypeAncestryIdentity StringAncestry { get; }
        internal StaticFieldTypeAncestryIdentity Int32Ancestry { get; }
        internal StaticFieldTypeAncestryIdentity BaseAncestry { get; }
        internal StaticFieldTypeAncestryIdentity DerivedAncestry { get; }
        internal StaticFieldTypeAncestryIdentity MarkerAncestry { get; }
        internal StaticFieldTypeAncestryIdentity LeafAncestry { get; }
        internal StaticFieldTypeAncestryIdentity EnvelopeAncestry { get; }
        internal StaticFieldTypeAncestryIdentity HolderAncestry { get; }

        internal StaticFieldSymbolDeclarationIdentity ObjectDeclaration(int fieldRowId)
        {
            var target = StaticFieldDeclaredReferenceIdentity.PrimitiveSystemObject(MetadataModule, ObjectAncestry);
            return Declaration(target, StaticFieldDeclaredValueKind.Object, fieldRowId, [0x06, 0x1C]);
        }

        internal StaticFieldSymbolDeclarationIdentity StringDeclaration(int fieldRowId)
        {
            var target = StaticFieldDeclaredReferenceIdentity.PrimitiveSystemString(MetadataModule, StringAncestry);
            return Declaration(target, StaticFieldDeclaredValueKind.String, fieldRowId, [0x06, 0x0E]);
        }

        internal StaticFieldSymbolDeclarationIdentity ReferenceDeclaration(
            StaticFieldTypeAncestryIdentity targetAncestry,
            StaticFieldDeclaredValueKind valueKind,
            int fieldRowId)
        {
            var target = StaticFieldDeclaredReferenceIdentity.ManagedReferenceTypeDefinition(
                MetadataModule,
                targetAncestry);
            var codedIndex = checked((byte)((targetAncestry.SubjectType.TypeDefinitionToken & 0x00FF_FFFF) << 2));
            return Declaration(target, valueKind, fieldRowId, [0x06, 0x12, codedIndex]);
        }

        internal StaticFieldSymbolBindingOutcome ExactBinding(
            StaticFieldSymbolDeclarationIdentity declaration)
        {
            var segments = ImmutableArray.Create(
                StaticFieldAccessSegment.Create(
                    "Synthetic",
                    "Synthetic",
                    StaticFieldSegmentSeparatorKind.GlobalAliasQualifier,
                    StaticFieldSegmentAccessKind.Root),
                StaticFieldAccessSegment.Create(
                    "Holder",
                    "Holder",
                    StaticFieldSegmentSeparatorKind.Dot,
                    StaticFieldSegmentAccessKind.DirectMember),
                StaticFieldAccessSegment.Create(
                    declaration.FieldName,
                    declaration.FieldName,
                    StaticFieldSegmentSeparatorKind.Dot,
                    StaticFieldSegmentAccessKind.DirectMember));
            var shape = StaticFieldCandidateShape.Create(2, StaticFieldSuffixShape.None);
            var descriptor = StaticFieldExpressionDescriptor.Create(
                $"global::Synthetic.Holder.{declaration.FieldName}",
                hasGlobalQualifier: true,
                segments,
                [shape],
                StaticFieldParserCounts.Create(7, 6, 4, 3, 1),
                SyntaxBounds);
            var expansion = StaticFieldNameExpansion.Create(
                shape,
                StaticFieldNameExpansionKind.GlobalQualified,
                "Synthetic",
                "Holder",
                declaration.FieldName);
            return StaticFieldSymbolBindingOutcome.Exact(
                descriptor,
                Snapshot.Sha256,
                DumpConsultedBindingContextIdentity.ForFullyQualified(Snapshot),
                [expansion],
                [ModuleFact],
                [StaticFieldSymbolCandidate.Create(declaration, shape, [expansion])],
                BindingBounds);
        }

        internal ClrmdStaticRuntimeTypeIdentity RuntimeType(
            StaticFieldTypeAncestryIdentity ancestry,
            ulong methodTable,
            bool isPrimitive = false,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity> genericArguments = default)
        {
            var arguments = genericArguments.IsDefault
                ? ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty
                : genericArguments;
            var name = StaticFieldRuntimeComposition.RuntimeFullName(ancestry.SubjectType);
            if (!arguments.IsEmpty)
            {
                var delimiter = name.LastIndexOf('`');
                name = $"{name[..delimiter]}<{string.Join(",", arguments.Select(static argument => argument.FullName))}>";
            }
            return ClrmdStaticRuntimeTypeIdentity.Create(
                Snapshot,
                PointerWidth,
                RuntimeModule,
                Content,
                ancestry.SubjectType.TypeDefinitionToken,
                name,
                methodTable,
                isValueType: ancestry.Classification is StaticFieldTypeClassification.ValueType or StaticFieldTypeClassification.Enum,
                isPrimitive,
                isArray: false,
                isInterface: ancestry.Classification == StaticFieldTypeClassification.Interface,
                arguments);
        }

        internal StaticFieldInterfaceImplementationRowIdentity EnvelopeToConstructedLeaf(int rowToken) =>
            StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
                MetadataModule,
                rowToken,
                EnvelopeType,
                0x1B000001,
                [0x15, 0x12, 0x28, 0x01, 0x08],
                genericHeadTypeReferenceResolution: null,
                LeafAncestry);

        internal StaticFieldInterfaceImplementationRowIdentity LeafToMarker(int rowToken) =>
            StaticFieldInterfaceImplementationRowIdentity.Create(
                MetadataModule,
                rowToken,
                LeafInterface,
                MarkerInterface.TypeDefinitionToken,
                interfaceTypeReferenceResolution: null,
                MarkerAncestry);

        internal ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> InterfaceCatalogs(
            StaticFieldInterfaceImplementationRowIdentity envelopeToLeaf,
            StaticFieldInterfaceImplementationRowIdentity leafToMarker,
            StaticFieldInterfaceImplementationRowIdentity? markerToLeaf = null)
        {
            var markerRows = markerToLeaf is null
                ? ImmutableArray<StaticFieldInterfaceImplementationRowIdentity>.Empty
                : ImmutableArray.Create(markerToLeaf);
            return
            [
                StaticFieldInterfaceImplementationCatalogIdentity.Create(
                    EnvelopeType,
                    ModuleFact,
                    [envelopeToLeaf]),
                StaticFieldInterfaceImplementationCatalogIdentity.Create(
                    BaseType,
                    ModuleFact,
                    ImmutableArray<StaticFieldInterfaceImplementationRowIdentity>.Empty),
                StaticFieldInterfaceImplementationCatalogIdentity.Create(
                    ObjectType,
                    ModuleFact,
                    ImmutableArray<StaticFieldInterfaceImplementationRowIdentity>.Empty),
                StaticFieldInterfaceImplementationCatalogIdentity.Create(
                    LeafInterface,
                    ModuleFact,
                    [leafToMarker]),
                StaticFieldInterfaceImplementationCatalogIdentity.Create(
                    MarkerInterface,
                    ModuleFact,
                    markerRows),
            ];
        }

        private StaticFieldTypeDefinitionIdentity Type(
            int rowId,
            string namespaceName,
            string typeName,
            TypeAttributes attributes,
            int genericArity,
            int? extendsToken,
            int fieldListRowId = 1,
            int fieldListEndExclusiveRowId = 1) =>
            StaticFieldTypeDefinitionIdentity.Create(
                MetadataModule,
                0x02000000 | rowId,
                fieldListRowId,
                fieldListEndExclusiveRowId,
                methodListRowId: 1,
                methodListEndExclusiveRowId: 1,
                namespaceName,
                typeName,
                (int)attributes,
                genericParameterCount: genericArity,
                introducedGenericArity: genericArity,
                extendsToken,
                enclosingType: null);

        private StaticFieldSymbolDeclarationIdentity Declaration(
            StaticFieldDeclaredReferenceIdentity target,
            StaticFieldDeclaredValueKind valueKind,
            int fieldRowId,
            ImmutableArray<byte> signature)
        {
            var fieldToken = 0x04000000 | fieldRowId;
            var customAttributes = StaticFieldFieldCustomAttributeProjection.Create(
                fieldToken,
                0,
                0,
                ImmutableArray<StaticFieldCustomAttributeRowIdentity>.Empty);
            var field = StaticFieldDefinitionIdentity.Create(
                HolderType,
                fieldToken,
                $"Value{fieldRowId}",
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                signature,
                customAttributes);
            return StaticFieldSymbolDeclarationIdentity.Create(
                HolderAncestry,
                field,
                valueKind,
                target);
        }
    }
}
