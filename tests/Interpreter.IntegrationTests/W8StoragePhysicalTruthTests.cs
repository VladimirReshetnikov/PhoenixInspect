using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Proves the W8.1 value, storage-family, and constructed-assignability branches from emitted artifacts and full dumps.
/// </summary>
/// <remarks>
/// This draft physical gate introduces no product binder. Runtime APIs locate exact slots only after metadata-token and
/// construction identity are fixed; copied raw bytes remain the value source, and high-level reads are late oracles.
/// </remarks>
public sealed class W8StoragePhysicalTruthTests
{
    private const string TargetNamespace = "Interpreter.W8TestTarget";
    private const string NamedRvaNamespace = "Interpreter.W8NamedRvaTarget";
    private const int MaximumRuntimeModules = 256;
    private const int MaximumRuntimeTypesWithStatics = 2_048;
    private const int MaximumRuntimeFields = 256;
    private const int MaximumRuntimeThreads = 256;
    private const int MaximumFramesPerThread = 256;
    private const int MaximumMetadataBytes = 16 * 1_024 * 1_024;
    private const int MaximumMetadataTypes = 4_096;
    private const int MaximumMetadataFieldsPerType = 1_024;
    private const int MaximumMetadataMethodsPerType = 1_024;
    private const int MaximumMetadataInterfacesPerType = 64;
    private const int MaximumMetadataGenericParameters = 64;
    private const int MaximumMetadataCustomAttributes = 128;
    private const int MaximumPeSections = 128;
    private const int MaximumAssemblyForwarderDepth = 16;
    private const int MaximumAssemblyArtifactBytes = 64 * 1_024 * 1_024;
    private const TypeAttributes ForwarderTypeAttribute = (TypeAttributes)0x00200000;
    private const int MaximumTypeTopologyDepth = 16;
    private const int MaximumTypeTopologyNodes = 128;
    private const int MaximumMetadataSignatureDepth = 32;
    private const int MaximumMetadataSignatureNodes = 128;
    private const int MaximumStringCharacters = 1_024;

    /// <summary>
    /// Proves every fixed-width primitive, target-width native integer, enum representation, nullable form, string,
    /// reference/null geometry, and the invariant/variant/base/interface/array graph, including close/reopen replay.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8StorageGeometryV1")]
    public void Full_dump_proves_value_geometry_and_constructed_assignability()
    {
        var catalog = W8StorageMetadataCatalog.Read(
            RequireArtifact(W8TestTargetPaths.ResolveAssembly()),
            RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly()));
        var dumpPath = TemporaryPath("w8-storage-geometry", ".dmp");
        var alteredDumpPath = TemporaryPath("w8-storage-geometry-altered", ".dmp");

        try
        {
            WriteDump("generic-frame", dumpPath);
            var first = ObserveValueGeometry(dumpPath, catalog);
            var reopened = ObserveValueGeometry(dumpPath, catalog);
            AssertCanonicalReplay(first.CanonicalLines, reopened.CanonicalLines);

            File.Copy(dumpPath, alteredDumpPath, overwrite: false);
            PatchDumpVirtualByte(alteredDumpPath, first.Int32Address, xorMask: 0x40);
            var originalPair = ObserveSelectedPrimitivePair(dumpPath, catalog);
            var alteredPair = ObserveSelectedPrimitivePair(alteredDumpPath, catalog);
            Assert.Equal(originalPair.ModuleVersionId, alteredPair.ModuleVersionId);
            Assert.Equal(originalPair.ModuleAddress, alteredPair.ModuleAddress);
            Assert.Equal(originalPair.Int32Address, alteredPair.Int32Address);
            Assert.Equal(originalPair.UInt32Address, alteredPair.UInt32Address);
            Assert.Equal(
                W8PhysicalEvidenceStatus.Conflict,
                CompareBytes(originalPair.Int32Bytes.AsSpan(), alteredPair.Int32Bytes.AsSpan()));
            Assert.Equal(
                W8PhysicalEvidenceStatus.Exact,
                CompareBytes(originalPair.UInt32Bytes.AsSpan(), alteredPair.UInt32Bytes.AsSpan()));
        }
        finally
        {
            DeleteIfPresent(alteredDumpPath);
            DeleteIfPresent(dumpPath);
        }
    }

    /// <summary>
    /// Proves two selected worker identities each own two distinct constructed thread-relative slots with the exact
    /// raw values assigned by that worker, and proves the complete observation survives close/reopen rebinding.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8ThreadRelativeStorageV1")]
    public void Full_dump_proves_two_workers_by_two_constructed_thread_relative_slots()
    {
        var catalog = W8StorageMetadataCatalog.Read(
            RequireArtifact(W8TestTargetPaths.ResolveAssembly()),
            RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly()));
        var dumpPath = TemporaryPath("w8-thread-relative", ".dmp");
        try
        {
            WriteDump("thread-relative", dumpPath);
            var first = ObserveThreadRelative(dumpPath, catalog);
            var reopened = ObserveThreadRelative(dumpPath, catalog);
            Assert.Equal(W8StorageBranchDisposition.Exact, first.Disposition);
            AssertCanonicalReplay(first.CanonicalLines, reopened.CanonicalLines);
        }
        finally
        {
            DeleteIfPresent(dumpPath);
        }
    }

    /// <summary>
    /// Proves the emitted context marker maps to one ordinary static slot but supplies no exact runtime-context
    /// identity, producing the reproducible typed non-admission required before W8 contracts freeze.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8ContextRelativeStorageV1")]
    public void Full_dump_records_context_relative_identity_non_admission()
    {
        var catalog = W8StorageMetadataCatalog.Read(
            RequireArtifact(W8TestTargetPaths.ResolveAssembly()),
            RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly()));
        var dumpPath = TemporaryPath("w8-context-relative", ".dmp");
        try
        {
            WriteDump("context-relative", dumpPath);
            var first = ObserveContextRelative(dumpPath, catalog);
            var reopened = ObserveContextRelative(dumpPath, catalog);
            Assert.Equal(W8StorageBranchDisposition.NonAdmitted, first.Disposition);
            Assert.Equal("W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE", first.ReasonCode);
            AssertCanonicalReplay(first.CanonicalLines, reopened.CanonicalLines);
        }
        finally
        {
            DeleteIfPresent(dumpPath);
        }
    }

    /// <summary>
    /// Proves named FieldRVA rows map exact module-relative geometry to raw dump bytes without construction or slot
    /// acquisition, and an independently altered PE preserves unrelated metadata while changing only one payload.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8NamedRvaStorageV1")]
    public void Full_dump_proves_named_rva_storage_and_independent_artifact_rejection()
    {
        var assemblyPath = RequireArtifact(W8TestTargetPaths.ResolveAssembly());
        var namedRvaPath = RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly());
        var catalog = W8StorageMetadataCatalog.Read(assemblyPath, namedRvaPath);
        var dumpPath = TemporaryPath("w8-named-rva", ".dmp");
        var alteredArtifactPath = TemporaryPath("w8-named-rva-altered", ".dll");
        try
        {
            WriteDump("rva-frame", dumpPath);
            var first = ObserveNamedRva(dumpPath, catalog);
            var reopened = ObserveNamedRva(dumpPath, catalog);
            Assert.Equal(W8StorageBranchDisposition.Exact, first.Disposition);
            Assert.Equal(0, first.RuntimeConstructionCalls);
            Assert.Equal(0, first.StorageAcquisitionCalls);
            Assert.Equal(2, first.ValueMemoryCalls);
            AssertCanonicalReplay(first.CanonicalLines, reopened.CanonicalLines);

            File.Copy(namedRvaPath, alteredArtifactPath, overwrite: false);
            PatchPeRvaByte(alteredArtifactPath, catalog.NamedSentinelRva, xorMask: 0x20);
            var altered = ReadNamedRvaArtifact(alteredArtifactPath, catalog);
            Assert.Equal(catalog.NamedRvaModuleVersionId, altered.ModuleVersionId);
            Assert.True(catalog.NamedRvaMetadataBytes.AsSpan().SequenceEqual(altered.MetadataBytes.AsSpan()));
            Assert.Equal(catalog.NamedSentinelToken, altered.NamedSentinelToken);
            Assert.Equal(catalog.NamedWideSentinelToken, altered.NamedWideSentinelToken);
            Assert.Equal(catalog.NamedSentinelRva, altered.NamedSentinelRva);
            Assert.Equal(catalog.NamedWideSentinelRva, altered.NamedWideSentinelRva);
            Assert.Equal(
                W8PhysicalEvidenceStatus.Conflict,
                CompareBytes(catalog.NamedSentinelBytes.AsSpan(), altered.NamedSentinelBytes));
            Assert.Equal(
                W8PhysicalEvidenceStatus.Exact,
                CompareBytes(catalog.NamedWideSentinelBytes.AsSpan(), altered.NamedWideSentinelBytes));
        }
        finally
        {
            DeleteIfPresent(alteredArtifactPath);
            DeleteIfPresent(dumpPath);
        }
    }

    /// <summary>
    /// Proves every admitted metadata literal, including exact floating bits, enum underlyings, strings, nulls, and
    /// the pinned decimal encoding, decodes twice while runtime construction, storage, and memory calls remain zero.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Metadata_literals_have_zero_runtime_storage_and_memory_calls()
    {
        var path = RequireArtifact(W8TestTargetPaths.ResolveAssembly());
        var firstLedger = new W8CountingLiteralCapabilities();
        var first = DecodeLiterals(path, firstLedger);
        var replayLedger = new W8CountingLiteralCapabilities();
        var replay = DecodeLiterals(path, replayLedger);

        Assert.Equal(1, firstLedger.MetadataArtifactCalls);
        Assert.Equal(0, firstLedger.RuntimeConstructionCalls);
        Assert.Equal(0, firstLedger.StorageAcquisitionCalls);
        Assert.Equal(0, firstLedger.MemoryCalls);
        Assert.Equal(1, replayLedger.MetadataArtifactCalls);
        Assert.Equal(0, replayLedger.RuntimeConstructionCalls);
        Assert.Equal(0, replayLedger.StorageAcquisitionCalls);
        Assert.Equal(0, replayLedger.MemoryCalls);
        AssertCanonicalReplay(first, replay);

        var interceptionProbe = new W8CountingLiteralCapabilities();
        Assert.Throws<Xunit.Sdk.XunitException>(interceptionProbe.RequestRuntimeConstruction);
        Assert.Throws<Xunit.Sdk.XunitException>(interceptionProbe.RequestStorageAcquisition);
        Assert.Throws<Xunit.Sdk.XunitException>(interceptionProbe.RequestValueMemory);
        Assert.Equal(0, interceptionProbe.MetadataArtifactCalls);
        Assert.Equal(1, interceptionProbe.RuntimeConstructionCalls);
        Assert.Equal(1, interceptionProbe.StorageAcquisitionCalls);
        Assert.Equal(1, interceptionProbe.MemoryCalls);
    }

    /// <summary>
    /// Proves raw type-signature and runtime-topology accounting accepts the exact configured depth and node caps,
    /// then rejects the first additional structural node before a recursive formatter can consume it.
    /// </summary>
    /// <remarks>
    /// This is draft design evidence for cumulative accounting. It intentionally tests both a deeply nested vector
    /// signature and a broad generic instantiation so independent depth and node limits cannot mask one another.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Structural_budgets_accept_exact_cap_and_reject_cap_plus_one()
    {
        var exactDepthBytes = Enumerable
            .Repeat((byte)0x1D, MaximumMetadataSignatureDepth - 1)
            .Append((byte)0x08)
            .ToArray();
        var exactDepth = CreateTestSignaturePredecoder().DecodeType(exactDepthBytes);
        Assert.Equal(MaximumMetadataSignatureDepth, exactDepth.MaximumDepth);
        Assert.Equal(MaximumMetadataSignatureDepth, exactDepth.NodeCount);
        var excessiveDepthBytes = exactDepthBytes.Prepend((byte)0x1D).ToArray();
        Assert.Throws<Xunit.Sdk.XunitException>(() =>
            CreateTestSignaturePredecoder().DecodeType(excessiveDepthBytes));

        var exactNodeBytes = RepeatedPrimitiveGenericSignature(MaximumMetadataSignatureNodes - 2);
        var exactNodes = CreateTestSignaturePredecoder().DecodeType(exactNodeBytes);
        Assert.Equal(2, exactNodes.MaximumDepth);
        Assert.Equal(MaximumMetadataSignatureNodes, exactNodes.NodeCount);
        var excessiveNodeBytes = RepeatedPrimitiveGenericSignature(MaximumMetadataSignatureNodes - 1);
        Assert.Throws<Xunit.Sdk.XunitException>(() =>
            CreateTestSignaturePredecoder().DecodeType(excessiveNodeBytes));

        var exactGraph = CreateSyntheticTypeSpecificationGraph(tailArgumentCount: 92);
        var exactDownstreamCalls = 0;
        var exactGraphFact = DecodeAfterBoundedPrewalk(
            CreateTestSignaturePredecoder(exactGraph.Signatures),
            exactGraph.Signatures[exactGraph.Root],
            exactGraph.Root,
            fact =>
            {
                exactDownstreamCalls++;
                return fact;
            });
        Assert.Equal(1, exactDownstreamCalls);
        Assert.Equal(MaximumMetadataSignatureDepth, exactGraphFact.MaximumDepth);
        Assert.Equal(MaximumMetadataSignatureNodes, exactGraphFact.NodeCount);

        var excessiveGraph = CreateSyntheticTypeSpecificationGraph(tailArgumentCount: 93);
        var rejectedDownstreamCalls = 0;
        Assert.Throws<Xunit.Sdk.XunitException>(() => DecodeAfterBoundedPrewalk(
            CreateTestSignaturePredecoder(excessiveGraph.Signatures),
            excessiveGraph.Signatures[excessiveGraph.Root],
            excessiveGraph.Root,
            fact =>
            {
                rejectedDownstreamCalls++;
                return fact;
            }));
        Assert.Equal(0, rejectedDownstreamCalls);

        var exactTopologyReader = CreateBroadSyntheticTypeShapeReader(
            MaximumTypeTopologyNodes - 1);
        var exactTopology = FormatTypeShape(
            exactTopologyReader,
            typeHandle: 1,
            MaximumTypeTopologyDepth,
            MaximumTypeTopologyNodes);
        Assert.Equal(MaximumTypeTopologyNodes, exactTopology.NodeCount);
        Assert.Equal(MaximumTypeTopologyNodes, exactTopologyReader.ReadCount);

        var excessiveTopologyReader = CreateBroadSyntheticTypeShapeReader(MaximumTypeTopologyNodes);
        Assert.Throws<Xunit.Sdk.XunitException>(() => FormatTypeShape(
            excessiveTopologyReader,
            typeHandle: 1,
            MaximumTypeTopologyDepth,
            MaximumTypeTopologyNodes));
        Assert.Equal(MaximumTypeTopologyNodes, excessiveTopologyReader.ReadCount);

        var exactDepthReader = CreateSyntheticTypeShapeChain(MaximumTypeTopologyDepth + 1);
        var exactTopologyDepth = FormatTypeShape(
            exactDepthReader,
            typeHandle: 1,
            MaximumTypeTopologyDepth,
            MaximumTypeTopologyNodes);
        Assert.Equal(MaximumTypeTopologyDepth + 1, exactTopologyDepth.NodeCount);
        var excessiveDepthReader = CreateSyntheticTypeShapeChain(MaximumTypeTopologyDepth + 2);
        Assert.Throws<Xunit.Sdk.XunitException>(() => FormatTypeShape(
            excessiveDepthReader,
            typeHandle: 1,
            MaximumTypeTopologyDepth,
            MaximumTypeTopologyNodes));
        Assert.Equal(MaximumTypeTopologyDepth + 1, excessiveDepthReader.ReadCount);

        var cyclicReader = CreateCyclicSyntheticTypeShapeReader();
        Assert.Throws<Xunit.Sdk.XunitException>(() => FormatTypeShape(
            cyclicReader,
            typeHandle: 1,
            MaximumTypeTopologyDepth,
            MaximumTypeTopologyNodes));
        Assert.Equal(2, cyclicReader.ReadCount);
    }

    private static W8ValueGeometryObservation ObserveValueGeometry(
        string dumpPath,
        W8StorageMetadataCatalog catalog)
    {
        using var view = W8DumpView.Open(dumpPath, catalog.TargetModuleVersionId);
        Assert.Equal(sizeof(ulong), view.DataTarget.DataReader.PointerSize);
        using var targetMetadata = ReadModuleMetadata(view.DataTarget.DataReader, view.TargetModule);
        Assert.Equal(catalog.TargetModuleVersionId, targetMetadata.ModuleVersionId);
        var primitiveType = ReadRuntimeTypeWithStaticFields(
            view.TargetModule,
            catalog.PrimitiveStorageToken);
        var primitiveFields = ReadRuntimeFields(primitiveType.StaticFields);

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add($"module|{view.TargetModule.Address:x16}|{targetMetadata.ModuleVersionId:D}");
        ulong int32Address = 0;
        foreach (var expected in PrimitiveExpectations(view.DataTarget.DataReader.PointerSize))
        {
            var token = catalog.PrimitiveFieldTokens[expected.Name];
            var field = Assert.Single(primitiveFields, candidate => candidate.Token == token);
            Assert.True(field.IsInitialized(view.TargetModule.AppDomain));
            Assert.Equal(expected.Bytes.Length, field.Size);
            var address = field.GetAddress(view.TargetModule.AppDomain);
            Assert.NotEqual(0UL, address);
            var bytes = ReadExact(view.DataTarget.DataReader, address, expected.Bytes.Length);
            Assert.Equal(expected.Bytes, bytes);
            if (string.Equals(expected.Name, "Int32", StringComparison.Ordinal))
            {
                int32Address = address;
            }

            lines.Add($"primitive|{expected.Name}|{token:x8}|{address:x16}|{Convert.ToHexString(bytes)}");
        }

        Assert.NotEqual(0UL, int32Address);
        ObserveNullable(
            view,
            primitiveFields,
            catalog.PrimitiveFieldTokens["Nullable"],
            expectedHasValue: true,
            expectedValue: 0x17283940,
            "Nullable",
            lines);
        ObserveNullable(
            view,
            primitiveFields,
            catalog.PrimitiveFieldTokens["NullableNull"],
            expectedHasValue: false,
            expectedValue: 0,
            "NullableNull",
            lines);
        ObserveString(
            view,
            primitiveFields,
            catalog.PrimitiveFieldTokens["Text"],
            "w8-primitive-storage",
            lines);
        ObserveNullReference(
            view,
            primitiveFields,
            catalog.PrimitiveFieldTokens["NullReference"],
            lines);
        ObserveArray(
            view,
            primitiveFields,
            catalog.PrimitiveFieldTokens["Vector"],
            expectedSzArray: true,
            expectedRank: 1,
            view.TargetModule.Address,
            catalog.RequestContextToken,
            "Vector",
            lines);
        var coreLibrary = Assert.Single(
            view.Modules,
            static module => string.Equals(
                Path.GetFileName(module.Name),
                "System.Private.CoreLib.dll",
                StringComparison.OrdinalIgnoreCase));
        using var coreMetadata = ReadModuleMetadata(view.DataTarget.DataReader, coreLibrary);
        var int32Token = FindTopLevelTypeToken(coreMetadata.Reader, "System", "Int32");
        ObserveArray(
            view,
            primitiveFields,
            catalog.PrimitiveFieldTokens["Matrix"],
            expectedSzArray: false,
            expectedRank: 2,
            coreLibrary.Address,
            int32Token,
            "Matrix",
            lines);

        ObserveConstructedAssignability(view, catalog, lines);
        lines.Add($"cdac-reads|{view.Oracle.ReadAccounting.ReadCount}|{view.Oracle.ReadAccounting.ByteCount}");
        return new W8ValueGeometryObservation(int32Address, lines.ToImmutable());
    }

    private static void ObserveConstructedAssignability(
        W8DumpView view,
        W8StorageMetadataCatalog catalog,
        ImmutableArray<string>.Builder lines)
    {
        var runtimeType = ReadRuntimeTypeWithStaticFields(
            view.TargetModule,
            catalog.AssignabilityStorageToken);
        var fields = ReadRuntimeFields(runtimeType.StaticFields);
        var values = new Dictionary<string, W8ReferenceSlotObservation>(StringComparer.Ordinal);
        foreach (var pair in catalog.AssignabilityFieldTokens.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var field = Assert.Single(fields, candidate => candidate.Token == pair.Value);
            Assert.True(field.IsObjectReference);
            Assert.True(field.IsInitialized(view.TargetModule.AppDomain));
            var slotAddress = field.GetAddress(view.TargetModule.AppDomain);
            var objectAddress = DecodePointer(
                ReadExact(view.DataTarget.DataReader, slotAddress, view.DataTarget.DataReader.PointerSize),
                view.DataTarget.DataReader.PointerSize);
            Assert.NotEqual(0UL, objectAddress);
            var methodTable = DecodePointer(
                ReadExact(view.DataTarget.DataReader, objectAddress, view.DataTarget.DataReader.PointerSize),
                view.DataTarget.DataReader.PointerSize);
            Assert.NotEqual(0UL, methodTable);
            Assert.NotNull(field.Type);
            Assert.NotEqual(0UL, field.Type!.MethodTable);
            var declaredShape = FormatTypeShape(view.Oracle, field.Type.MethodTable);
            var actualShape = FormatTypeShape(view.Oracle, methodTable);
            values.Add(pair.Key, new W8ReferenceSlotObservation(
                slotAddress,
                objectAddress,
                methodTable,
                field.Type.MethodTable,
                declaredShape,
                actualShape));
            lines.Add(
                $"assign|{pair.Key}|{pair.Value:x8}|{slotAddress:x16}|{objectAddress:x16}|" +
                $"{methodTable:x16}|{field.Type.MethodTable:x16}|{declaredShape}|{actualShape}");
        }

        AssertSameObject(values, "DirectDerived", "BaseView");
        AssertSameObject(values, "DirectRequestCarrier", "InvariantRequest");
        AssertSameObject(values, "DirectRequestCarrier", "CovariantRequest");
        AssertSameObject(values, "DirectRequestCarrier", "CovariantObject");
        AssertSameObject(values, "DirectObjectCarrier", "ContravariantObject");
        AssertSameObject(values, "DirectObjectCarrier", "ContravariantRequest");
        AssertSameObject(values, "RequestVector", "ObjectVector");
        AssertSameObject(values, "RequestMatrix", "ObjectMatrix");
        AssertSameObject(values, "ValueVector", "ValueArrayView");

        Assert.NotEqual(values["InvariantRequest"].ObjectAddress, values["InvariantBatch"].ObjectAddress);
        Assert.NotEqual(values["InvariantRequest"].ActualMethodTable, values["InvariantBatch"].ActualMethodTable);
        Assert.NotEqual(values["InvariantRequest"].DeclaredMethodTable, values["InvariantBatch"].DeclaredMethodTable);
        Assert.NotEqual(values["CovariantRequest"].DeclaredMethodTable, values["CovariantObject"].DeclaredMethodTable);
        Assert.NotEqual(
            values["ContravariantObject"].DeclaredMethodTable,
            values["ContravariantRequest"].DeclaredMethodTable);
        Assert.NotEqual(values["RequestVector"].DeclaredMethodTable, values["ObjectVector"].DeclaredMethodTable);
        Assert.NotEqual(values["RequestMatrix"].DeclaredMethodTable, values["ObjectMatrix"].DeclaredMethodTable);

        var coreLibrary = Assert.Single(view.Modules, static module => string.Equals(
            Path.GetFileName(module.Name),
            "System.Private.CoreLib.dll",
            StringComparison.OrdinalIgnoreCase));
        using var coreMetadata = ReadModuleMetadata(view.DataTarget.DataReader, coreLibrary);
        var targetModule = view.TargetModule.Address;
        var request = new W8RuntimeTypeIdentity(targetModule, catalog.RequestContextToken);
        var batch = new W8RuntimeTypeIdentity(targetModule, catalog.BatchContextToken);
        var value = new W8RuntimeTypeIdentity(targetModule, catalog.ValueContextToken);
        var objectType = new W8RuntimeTypeIdentity(
            coreLibrary.Address,
            FindTopLevelTypeToken(coreMetadata.Reader, "System", "Object"));
        var arrayType = new W8RuntimeTypeIdentity(
            coreLibrary.Address,
            FindTopLevelTypeToken(coreMetadata.Reader, "System", "Array"));
        var derived = new W8RuntimeTypeIdentity(targetModule, catalog.AssignabilityDerivedToken);
        var baseType = new W8RuntimeTypeIdentity(targetModule, catalog.AssignabilityBaseToken);
        var carrier = new W8RuntimeTypeIdentity(targetModule, catalog.AssignabilityCarrierToken);
        var invariant = new W8RuntimeTypeIdentity(targetModule, catalog.InvariantNodeToken);
        var covariant = new W8RuntimeTypeIdentity(targetModule, catalog.CovariantNodeToken);
        var contravariant = new W8RuntimeTypeIdentity(targetModule, catalog.ContravariantNodeToken);

        AssertNamedShape(view.Oracle, values["DirectDerived"].DeclaredMethodTable, derived);
        AssertNamedShape(view.Oracle, values["DirectDerived"].ActualMethodTable, derived);
        AssertNamedShape(view.Oracle, values["BaseView"].DeclaredMethodTable, baseType);
        AssertNamedShape(view.Oracle, values["BaseView"].ActualMethodTable, derived);
        AssertNamedShape(view.Oracle, values["DirectRequestCarrier"].DeclaredMethodTable, carrier, request);
        AssertNamedShape(view.Oracle, values["DirectRequestCarrier"].ActualMethodTable, carrier, request);
        AssertNamedShape(view.Oracle, values["InvariantRequest"].DeclaredMethodTable, invariant, request);
        AssertNamedShape(view.Oracle, values["InvariantRequest"].ActualMethodTable, carrier, request);
        AssertNamedShape(view.Oracle, values["InvariantBatch"].DeclaredMethodTable, invariant, batch);
        AssertNamedShape(view.Oracle, values["InvariantBatch"].ActualMethodTable, carrier, batch);
        AssertNamedShape(view.Oracle, values["CovariantRequest"].DeclaredMethodTable, covariant, request);
        AssertNamedShape(view.Oracle, values["CovariantRequest"].ActualMethodTable, carrier, request);
        AssertNamedShape(view.Oracle, values["CovariantObject"].DeclaredMethodTable, covariant, objectType);
        AssertNamedShape(view.Oracle, values["CovariantObject"].ActualMethodTable, carrier, request);
        AssertNamedShape(view.Oracle, values["DirectObjectCarrier"].DeclaredMethodTable, carrier, objectType);
        AssertNamedShape(view.Oracle, values["DirectObjectCarrier"].ActualMethodTable, carrier, objectType);
        AssertNamedShape(view.Oracle, values["ContravariantObject"].DeclaredMethodTable, contravariant, objectType);
        AssertNamedShape(view.Oracle, values["ContravariantObject"].ActualMethodTable, carrier, objectType);
        AssertNamedShape(view.Oracle, values["ContravariantRequest"].DeclaredMethodTable, contravariant, request);
        AssertNamedShape(view.Oracle, values["ContravariantRequest"].ActualMethodTable, carrier, objectType);
        AssertArrayShape(view.Oracle, values["RequestVector"].DeclaredMethodTable, true, 1, request);
        AssertArrayShape(view.Oracle, values["RequestVector"].ActualMethodTable, true, 1, request);
        AssertArrayShape(view.Oracle, values["ObjectVector"].DeclaredMethodTable, true, 1, objectType);
        AssertArrayShape(view.Oracle, values["ObjectVector"].ActualMethodTable, true, 1, request);
        AssertArrayShape(view.Oracle, values["RequestMatrix"].DeclaredMethodTable, false, 2, request);
        AssertArrayShape(view.Oracle, values["RequestMatrix"].ActualMethodTable, false, 2, request);
        AssertArrayShape(view.Oracle, values["ObjectMatrix"].DeclaredMethodTable, false, 2, objectType);
        AssertArrayShape(view.Oracle, values["ObjectMatrix"].ActualMethodTable, false, 2, request);
        AssertArrayShape(view.Oracle, values["ValueVector"].DeclaredMethodTable, true, 1, value);
        AssertArrayShape(view.Oracle, values["ValueVector"].ActualMethodTable, true, 1, value);
        AssertNamedShape(view.Oracle, values["ValueArrayView"].DeclaredMethodTable, arrayType);
        AssertArrayShape(view.Oracle, values["ValueArrayView"].ActualMethodTable, true, 1, value);

        Assert.Equal(GenericParameterAttributes.None, catalog.InvariantVariance);
        Assert.Equal(GenericParameterAttributes.Covariant, catalog.CovariantVariance);
        Assert.Equal(GenericParameterAttributes.Contravariant, catalog.ContravariantVariance);
        Assert.True(catalog.CarrierInterfaceSignatures.Length == 3);
        Assert.All(catalog.CarrierInterfaceSignatures, static signature => Assert.Contains("var:0", signature));
        Assert.Equal(catalog.AssignabilityBaseToken, catalog.AssignabilityDerivedBaseToken);
    }

    private static void AssertNamedShape(
        W8CdacRuntimeConstructionOracle oracle,
        ulong typeHandle,
        W8RuntimeTypeIdentity expected,
        params W8RuntimeTypeIdentity[] arguments)
    {
        var shape = oracle.ReadTypeShape(typeHandle);
        Assert.Equal(W8CdacTypeShapeKind.MethodTable, shape.Kind);
        Assert.False(shape.IsArray);
        Assert.Equal(expected.ModuleAddress, shape.ModuleAddress);
        Assert.Equal(expected.TypeToken, shape.TypeDefToken);
        Assert.Equal(arguments.Length, shape.TypeArgumentHandles.Length);
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = oracle.ReadTypeShape(shape.TypeArgumentHandles[index]);
            Assert.Equal(W8CdacTypeShapeKind.MethodTable, argument.Kind);
            Assert.False(argument.IsArray);
            Assert.Equal(arguments[index].ModuleAddress, argument.ModuleAddress);
            Assert.Equal(arguments[index].TypeToken, argument.TypeDefToken);
            Assert.Empty(argument.TypeArgumentHandles);
        }
    }

    private static void AssertArrayShape(
        W8CdacRuntimeConstructionOracle oracle,
        ulong typeHandle,
        bool expectedSzArray,
        int expectedRank,
        W8RuntimeTypeIdentity expectedElement)
    {
        var shape = oracle.ReadTypeShape(typeHandle);
        Assert.Equal(W8CdacTypeShapeKind.MethodTable, shape.Kind);
        Assert.True(shape.IsArray);
        Assert.Equal(expectedSzArray, shape.IsSzArray);
        Assert.Equal(expectedRank, shape.ArrayRank);
        var element = oracle.ReadTypeShape(shape.ElementOrParameterTypeHandle);
        Assert.Equal(W8CdacTypeShapeKind.MethodTable, element.Kind);
        Assert.False(element.IsArray);
        Assert.Equal(expectedElement.ModuleAddress, element.ModuleAddress);
        Assert.Equal(expectedElement.TypeToken, element.TypeDefToken);
        Assert.Empty(element.TypeArgumentHandles);
    }

    private static void AssertSameObject(
        IReadOnlyDictionary<string, W8ReferenceSlotObservation> values,
        string left,
        string right)
    {
        Assert.NotEqual(values[left].SlotAddress, values[right].SlotAddress);
        Assert.Equal(values[left].ObjectAddress, values[right].ObjectAddress);
        Assert.Equal(values[left].ActualMethodTable, values[right].ActualMethodTable);
    }

    private static void ObserveNullable(
        W8DumpView view,
        ImmutableArray<ClrStaticField> fields,
        int fieldToken,
        bool expectedHasValue,
        int expectedValue,
        string label,
        ImmutableArray<string>.Builder lines)
    {
        var field = Assert.Single(fields, candidate => candidate.Token == fieldToken);
        Assert.NotNull(field.Type);
        Assert.True(field.IsInitialized(view.TargetModule.AppDomain));
        var slotAddress = field.GetAddress(view.TargetModule.AppDomain);
        var slotBytes = ReadExact(
            view.DataTarget.DataReader,
            slotAddress,
            view.DataTarget.DataReader.PointerSize);
        var objectAddress = DecodePointer(slotBytes, view.DataTarget.DataReader.PointerSize);
        Assert.NotEqual(0UL, objectAddress);
        var headerBytes = ReadExact(
            view.DataTarget.DataReader,
            objectAddress,
            view.DataTarget.DataReader.PointerSize);
        var methodTable = DecodePointer(headerBytes, view.DataTarget.DataReader.PointerSize);
        Assert.Equal(field.Type!.MethodTable, methodTable);

        var childFields = field.Type.Fields;
        Assert.False(childFields.IsDefault);
        Assert.InRange(childFields.Length, 2, MaximumRuntimeFields);
        var hasValueField = Assert.Single(childFields, static candidate => string.Equals(
            candidate.Name,
            "hasValue",
            StringComparison.Ordinal));
        var valueField = Assert.Single(childFields, static candidate => string.Equals(
            candidate.Name,
            "value",
            StringComparison.Ordinal));
        Assert.Equal(sizeof(byte), hasValueField.Size);
        Assert.Equal(sizeof(int), valueField.Size);
        Assert.NotEqual(hasValueField.Offset, valueField.Offset);
        var payloadAddress = checked(objectAddress + (ulong)view.DataTarget.DataReader.PointerSize);
        var hasValueBytes = ReadExact(
            view.DataTarget.DataReader,
            checked(payloadAddress + (ulong)hasValueField.Offset),
            sizeof(byte));
        Assert.Equal(expectedHasValue ? (byte)1 : (byte)0, hasValueBytes[0]);
        var valueBytes = ReadExact(
            view.DataTarget.DataReader,
            checked(payloadAddress + (ulong)valueField.Offset),
            sizeof(int));
        var value = BinaryPrimitives.ReadInt32LittleEndian(valueBytes);
        if (expectedHasValue)
        {
            Assert.Equal(expectedValue, value);
        }

        var late = field.ReadStruct(view.TargetModule.AppDomain);
        Assert.True(late.IsValid);
        Assert.Equal(payloadAddress, late.Address);
        lines.Add(
            $"nullable|{label}|{fieldToken:x8}|{slotAddress:x16}|{objectAddress:x16}|{methodTable:x16}|" +
            $"{hasValueField.Token:x8}|{hasValueField.Offset}|{hasValueBytes[0]}|" +
            $"{valueField.Token:x8}|{valueField.Offset}|{Convert.ToHexString(valueBytes)}");
    }

    private static void ObserveString(
        W8DumpView view,
        ImmutableArray<ClrStaticField> fields,
        int fieldToken,
        string expected,
        ImmutableArray<string>.Builder lines)
    {
        var field = Assert.Single(fields, candidate => candidate.Token == fieldToken);
        var slotAddress = field.GetAddress(view.TargetModule.AppDomain);
        var objectAddress = DecodePointer(
            ReadExact(view.DataTarget.DataReader, slotAddress, view.DataTarget.DataReader.PointerSize),
            view.DataTarget.DataReader.PointerSize);
        Assert.NotEqual(0UL, objectAddress);
        var methodTable = DecodePointer(
            ReadExact(view.DataTarget.DataReader, objectAddress, view.DataTarget.DataReader.PointerSize),
            view.DataTarget.DataReader.PointerSize);
        Assert.Equal(field.Type?.MethodTable, methodTable);
        var lengthAddress = checked(objectAddress + (ulong)view.DataTarget.DataReader.PointerSize);
        var lengthBytes = ReadExact(view.DataTarget.DataReader, lengthAddress, sizeof(int));
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        Assert.InRange(length, 0, MaximumStringCharacters);
        Assert.Equal(expected.Length, length);
        var textBytes = ReadExact(
            view.DataTarget.DataReader,
            checked(lengthAddress + sizeof(int)),
            checked(length * sizeof(char)));
        Assert.Equal(expected, Encoding.Unicode.GetString(textBytes));
        Assert.Equal(expected, field.ReadString(view.TargetModule.AppDomain));
        lines.Add(
            $"string|{fieldToken:x8}|{slotAddress:x16}|{objectAddress:x16}|{methodTable:x16}|" +
            $"{length}|{Convert.ToHexString(textBytes)}");
    }

    private static void ObserveNullReference(
        W8DumpView view,
        ImmutableArray<ClrStaticField> fields,
        int fieldToken,
        ImmutableArray<string>.Builder lines)
    {
        var field = Assert.Single(fields, candidate => candidate.Token == fieldToken);
        var slotAddress = field.GetAddress(view.TargetModule.AppDomain);
        var bytes = ReadExact(
            view.DataTarget.DataReader,
            slotAddress,
            view.DataTarget.DataReader.PointerSize);
        Assert.Equal(0UL, DecodePointer(bytes, view.DataTarget.DataReader.PointerSize));
        Assert.Equal(0UL, field.ReadObject(view.TargetModule.AppDomain).Address);
        lines.Add($"null-reference|{fieldToken:x8}|{slotAddress:x16}|{Convert.ToHexString(bytes)}");
    }

    private static void ObserveArray(
        W8DumpView view,
        ImmutableArray<ClrStaticField> fields,
        int fieldToken,
        bool expectedSzArray,
        int expectedRank,
        ulong expectedElementModule,
        int expectedElementToken,
        string label,
        ImmutableArray<string>.Builder lines)
    {
        var field = Assert.Single(fields, candidate => candidate.Token == fieldToken);
        var slotAddress = field.GetAddress(view.TargetModule.AppDomain);
        var objectAddress = DecodePointer(
            ReadExact(view.DataTarget.DataReader, slotAddress, view.DataTarget.DataReader.PointerSize),
            view.DataTarget.DataReader.PointerSize);
        Assert.NotEqual(0UL, objectAddress);
        var methodTable = DecodePointer(
            ReadExact(view.DataTarget.DataReader, objectAddress, view.DataTarget.DataReader.PointerSize),
            view.DataTarget.DataReader.PointerSize);
        var shape = view.Oracle.ReadTypeShape(methodTable);
        Assert.True(shape.IsArray);
        Assert.Equal(expectedSzArray, shape.IsSzArray);
        Assert.Equal(expectedRank, shape.ArrayRank);
        var element = view.Oracle.ReadTypeShape(shape.ElementOrParameterTypeHandle);
        Assert.Equal(expectedElementModule, element.ModuleAddress);
        Assert.Equal(expectedElementToken, element.TypeDefToken);
        var late = field.ReadObject(view.TargetModule.AppDomain);
        Assert.Equal(objectAddress, late.Address);
        Assert.Equal(methodTable, late.Type?.MethodTable);
        lines.Add(
            $"array|{label}|{fieldToken:x8}|{slotAddress:x16}|{objectAddress:x16}|{methodTable:x16}|" +
            $"{(shape.IsSzArray ? "sz" : "md")}|{shape.ArrayRank}|{shape.ElementOrParameterTypeHandle:x16}|" +
            $"{element.ModuleAddress:x16}|{element.TypeDefToken:x8}");
    }

    private static W8ThreadRelativeObservation ObserveThreadRelative(
        string dumpPath,
        W8StorageMetadataCatalog catalog)
    {
        using var view = W8DumpView.Open(dumpPath, catalog.TargetModuleVersionId);
        var requestConstruction = FindExactGenericConstruction(
            view,
            catalog.GenericSlotToken,
            catalog.RequestContextToken);
        var batchConstruction = FindExactGenericConstruction(
            view,
            catalog.GenericSlotToken,
            catalog.BatchContextToken);
        Assert.NotEqual(requestConstruction.TypeHandle, batchConstruction.TypeHandle);

        var requestType = Assert.IsAssignableFrom<ClrType>(
            view.Runtime.GetTypeByMethodTable(requestConstruction.TypeHandle));
        var batchType = Assert.IsAssignableFrom<ClrType>(
            view.Runtime.GetTypeByMethodTable(batchConstruction.TypeHandle));
        Assert.Equal(requestConstruction.TypeHandle, requestType.MethodTable);
        Assert.Equal(batchConstruction.TypeHandle, batchType.MethodTable);
        var requestField = Assert.Single(
            ReadThreadStaticFields(requestType),
            candidate => candidate.Token == catalog.ThreadSentinelToken);
        var batchField = Assert.Single(
            ReadThreadStaticFields(batchType),
            candidate => candidate.Token == catalog.ThreadSentinelToken);

        var workerThreads = SelectWorkerThreads(view, catalog);
        Assert.Equal(2, workerThreads.Length);
        Assert.Equal(2, workerThreads.Select(static thread => thread.Address).Distinct().Count());
        Assert.Equal(2, workerThreads.Select(static thread => thread.ManagedThreadId).Distinct().Count());
        Assert.Equal(2, workerThreads.Select(static thread => thread.OSThreadId).Distinct().Count());

        var observedPairs = new HashSet<(int Request, int Batch)>();
        var addresses = new HashSet<ulong>();
        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add(
            $"construction|request|{requestConstruction.TypeHandle:x16}|" +
            $"{requestConstruction.ModuleAddress:x16}|{requestConstruction.TypeDefToken:x8}|" +
            $"{requestConstruction.TypeArgumentHandles[0]:x16}");
        lines.Add(
            $"construction|batch|{batchConstruction.TypeHandle:x16}|" +
            $"{batchConstruction.ModuleAddress:x16}|{batchConstruction.TypeDefToken:x8}|" +
            $"{batchConstruction.TypeArgumentHandles[0]:x16}");
        foreach (var thread in workerThreads.OrderBy(static thread => thread.Address))
        {
            Assert.True(requestField.IsInitialized(thread));
            Assert.True(batchField.IsInitialized(thread));
            var requestAddress = requestField.GetAddress(thread);
            var batchAddress = batchField.GetAddress(thread);
            Assert.NotEqual(0UL, requestAddress);
            Assert.NotEqual(0UL, batchAddress);
            Assert.True(addresses.Add(requestAddress));
            Assert.True(addresses.Add(batchAddress));
            var requestBytes = ReadExact(view.DataTarget.DataReader, requestAddress, sizeof(int));
            var batchBytes = ReadExact(view.DataTarget.DataReader, batchAddress, sizeof(int));
            var requestValue = BinaryPrimitives.ReadInt32LittleEndian(requestBytes);
            var batchValue = BinaryPrimitives.ReadInt32LittleEndian(batchBytes);
            Assert.Equal(requestValue, requestField.Read<int>(thread));
            Assert.Equal(batchValue, batchField.Read<int>(thread));
            Assert.True(observedPairs.Add((requestValue, batchValue)));
            lines.Add(
                $"thread|{thread.Address:x16}|{thread.ManagedThreadId}|{thread.OSThreadId}|" +
                $"request|{requestAddress:x16}|{Convert.ToHexString(requestBytes)}|" +
                $"batch|{batchAddress:x16}|{Convert.ToHexString(batchBytes)}");
        }

        Assert.Equal(4, addresses.Count);
        Assert.True(observedPairs.SetEquals(
        [
            (unchecked((int)0xE1017A01), unchecked((int)0xE1117A11)),
            (unchecked((int)0xE2027A02), unchecked((int)0xE2127A12)),
        ]));
        lines.Add("disposition|Exact|W8_THREAD_IDENTITY_AND_SLOTS_ATTRIBUTABLE");
        return new W8ThreadRelativeObservation(
            W8StorageBranchDisposition.Exact,
            lines.ToImmutable());
    }

    private static ImmutableArray<ClrThread> SelectWorkerThreads(
        W8DumpView view,
        W8StorageMetadataCatalog catalog)
    {
        var threads = view.Runtime.Threads
            .OrderBy(static thread => thread.Address)
            .ThenBy(static thread => thread.ManagedThreadId)
            .ThenBy(static thread => thread.OSThreadId)
            .Take(MaximumRuntimeThreads + 1)
            .ToArray();
        Assert.True(
            threads.Length <= MaximumRuntimeThreads,
            $"Runtime-thread traversal exceeded {MaximumRuntimeThreads}; cap-plus-one observed {threads.Length}.");
        var matches = ImmutableArray.CreateBuilder<ClrThread>();
        foreach (var thread in threads)
        {
            var frames = thread
                .EnumerateStackTrace(includeContext: false, maxFrames: MaximumFramesPerThread + 1)
                .Take(MaximumFramesPerThread + 1)
                .ToArray();
            Assert.True(
                frames.Length <= MaximumFramesPerThread,
                $"Frame traversal exceeded {MaximumFramesPerThread}; cap-plus-one observed {frames.Length}.");
            var exactFrames = frames.Where(frame =>
                    frame.Kind == ClrStackFrameKind.ManagedMethod &&
                    frame.Method is { } method &&
                    method.Type is { } type &&
                    method.MetadataToken == catalog.ThreadWorkerMethodToken &&
                    type.MetadataToken == catalog.ThreadProfileToken &&
                    type.Module.Address == view.TargetModule.Address)
                .ToArray();
            if (exactFrames.Length != 0)
            {
                Assert.Single(exactFrames);
                Assert.True(thread.ManagedThreadId > 0);
                Assert.NotEqual(0U, thread.OSThreadId);
                Assert.NotEqual(0UL, thread.Address);
                matches.Add(thread);
            }
        }

        return matches.ToImmutable();
    }

    private static W8ContextRelativeObservation ObserveContextRelative(
        string dumpPath,
        W8StorageMetadataCatalog catalog)
    {
        using var view = W8DumpView.OpenWithoutConstructionOracle(dumpPath, catalog.TargetModuleVersionId);
        var forwarding = ProveContextMarkerForwarding(view, catalog.ContextMarkerIdentity);
        var runtimeType = ReadRuntimeTypeWithStaticFields(view.TargetModule, catalog.ContextStorageToken);
        var ordinaryFields = ReadRuntimeFields(runtimeType.StaticFields);
        var contextField = Assert.Single(
            ordinaryFields,
            candidate => candidate.Token == catalog.ContextSentinelToken);
        Assert.DoesNotContain(
            ReadThreadStaticFields(runtimeType),
            candidate => candidate.Token == catalog.ContextSentinelToken);
        var domains = view.Runtime.AppDomains.Take(3).ToArray();
        var domain = Assert.Single(domains);
        Assert.Equal(view.TargetModule.AppDomain.Address, domain.Address);
        Assert.True(contextField.IsInitialized(domain));
        var slotAddress = contextField.GetAddress(domain);
        Assert.NotEqual(0UL, slotAddress);
        var bytes = ReadExact(view.DataTarget.DataReader, slotAddress, sizeof(int));
        Assert.Equal(unchecked((int)0xE3037A03), BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal(unchecked((int)0xE3037A03), contextField.Read<int>(domain));

        var reasonCode = "W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE";
        var marker = catalog.ContextMarkerIdentity;
        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add(
            $"metadata|{catalog.ContextStorageToken:x8}|{catalog.ContextSentinelToken:x8}|" +
            $"attribute={marker.AttributeToken:x8}|constructor={marker.ConstructorToken:x8}|" +
            $"constructor-signature={Convert.ToHexString(marker.ConstructorSignature.AsSpan())}|" +
            $"attribute-value={Convert.ToHexString(marker.AttributeValue.AsSpan())}|" +
            $"type-ref={marker.TypeReferenceToken:x8}|type={marker.TypeNamespace}.{marker.TypeName}|" +
            $"direct-scope={FormatAssemblyReference(marker.DirectAssemblyReference)}");
        lines.Add(
            $"facade|{FormatArtifact(forwarding.FacadeArtifact)}");
        foreach (var step in forwarding.Steps)
        {
            lines.Add(
                $"forwarder|{step.Ordinal}|source={FormatArtifact(step.SourceArtifact)}|" +
                $"exported={step.ExportedTypeToken:x8}|type={step.TypeNamespace}.{step.TypeName}|" +
                $"attributes={(int)step.Attributes:x8}|implementation={step.ImplementationToken:x8}|" +
                $"target-reference={FormatAssemblyReference(step.TargetReference)}|" +
                $"target={FormatArtifact(step.TargetArtifact)}");
        }

        lines.Add(
            $"terminal|type-def={forwarding.TerminalTypeDefinitionToken:x8}|" +
            $"base-class-library={FormatArtifact(forwarding.TerminalArtifact)}");
        lines.Add($"ordinary-slot|{domain.Address:x16}|{slotAddress:x16}|{Convert.ToHexString(bytes)}");
        lines.Add(
            $"runtime-shape|ordinary={ordinaryFields.Length}|thread-relative={runtimeType.ThreadStaticFields.Length}");
        lines.Add($"disposition|NonAdmitted|{reasonCode}");
        return new W8ContextRelativeObservation(
            W8StorageBranchDisposition.NonAdmitted,
            reasonCode,
            lines.ToImmutable());
    }

    private static W8ContextForwarderProof ProveContextMarkerForwarding(
        W8DumpView view,
        W8ContextMarkerIdentity marker)
    {
        Assert.NotEqual(0, marker.AttributeToken);
        Assert.NotEqual(0, marker.ConstructorToken);
        Assert.NotEqual(0, marker.TypeReferenceToken);
        Assert.NotEmpty(marker.ConstructorSignature);
        Assert.NotEmpty(marker.AttributeValue);
        Assert.Equal("System.Runtime", marker.DirectAssemblyReference.Name);
        var facade = LocateExactLoadedAssemblyArtifact(view, marker.DirectAssemblyReference);
        var current = facade;
        var steps = ImmutableArray.CreateBuilder<W8AssemblyForwarderStep>();
        var visited = new HashSet<(Guid ModuleVersionId, int ExportedTypeToken)>();

        for (var depth = 0; depth < MaximumAssemblyForwarderDepth; depth++)
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(current.MetadataBytes);
            var reader = provider.GetMetadataReader();
            var definitions = ReadBounded(
                    reader.TypeDefinitions,
                    MaximumMetadataTypes,
                    "forwarder TypeDefs")
                .Where(handle => IsNamedTypeDefinition(
                    reader,
                    handle,
                    marker.TypeNamespace,
                    marker.TypeName))
                .ToArray();
            Assert.True(
                definitions.Length <= 1,
                $"The forwarding artifact contained {definitions.Length} matching TypeDefs.");
            if (definitions.Length == 1)
            {
                var selectedBaseClassLibrary = view.Runtime.BaseClassLibrary ??
                    throw new Xunit.Sdk.XunitException(
                        "ClrMD did not expose the runtime-selected base class library.");
                var selectedArtifact = ReadLoadedAssemblyArtifact(view, selectedBaseClassLibrary);
                AssertSameArtifact(current, selectedArtifact);
                Assert.Equal("System.Private.CoreLib", selectedArtifact.Definition.Name);
                return new W8ContextForwarderProof(
                    facade,
                    steps.ToImmutable(),
                    current,
                    MetadataTokens.GetToken(definitions[0]));
            }

            var exportedHandle = Assert.Single(
                ReadBounded(
                    reader.ExportedTypes,
                    MaximumMetadataTypes,
                    "forwarder ExportedTypes"),
                handle => IsNamedExportedType(
                    reader,
                    handle,
                    marker.TypeNamespace,
                    marker.TypeName));
            var exportedToken = MetadataTokens.GetToken(exportedHandle);
            Assert.True(
                visited.Add((current.Content.ModuleVersionId, exportedToken)),
                $"The forwarding chain revisited ExportedType 0x{exportedToken:x8} in " +
                $"{current.Content.ModuleVersionId:D}.");
            var exported = reader.GetExportedType(exportedHandle);
            Assert.True((exported.Attributes & ForwarderTypeAttribute) != 0);
            Assert.Equal(HandleKind.AssemblyReference, exported.Implementation.Kind);
            var implementation = (AssemblyReferenceHandle)exported.Implementation;
            var targetReference = ReadAssemblyReferenceIdentity(reader, implementation);
            var target = LocateExactLoadedAssemblyArtifact(view, targetReference);
            steps.Add(new W8AssemblyForwarderStep(
                steps.Count,
                current,
                exportedToken,
                marker.TypeNamespace,
                marker.TypeName,
                exported.Attributes,
                MetadataTokens.GetToken(implementation),
                targetReference,
                target));
            current = target;
        }

        throw new Xunit.Sdk.XunitException(
            $"The assembly forwarding chain exceeded depth {MaximumAssemblyForwarderDepth}.");
    }

    private static bool IsNamedExportedType(
        MetadataReader reader,
        ExportedTypeHandle handle,
        string expectedNamespace,
        string expectedName)
    {
        var exported = reader.GetExportedType(handle);
        return string.Equals(reader.GetString(exported.Namespace), expectedNamespace, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(exported.Name), expectedName, StringComparison.Ordinal);
    }

    private static W8LoadedAssemblyArtifact LocateExactLoadedAssemblyArtifact(
        W8DumpView view,
        W8AssemblyReferenceIdentity reference)
    {
        Assert.NotEqual(0, reference.Token);
        Assert.NotEmpty(reference.Name);
        Assert.Empty(reference.HashValue);
        var matches = ImmutableArray.CreateBuilder<W8LoadedAssemblyArtifact>();
        foreach (var module in view.Modules)
        {
            if (module.MetadataAddress == 0 || module.MetadataLength == 0)
            {
                continue;
            }

            using var metadata = ReadModuleMetadata(view.DataTarget.DataReader, module);
            if (!metadata.Reader.IsAssembly ||
                !AssemblyDefinitionMayMatchReference(metadata.Reader, reference))
            {
                continue;
            }

            var artifact = ReadLoadedAssemblyArtifact(view, module);
            if (AssemblyReferenceTargetsDefinition(reference, artifact.Definition))
            {
                matches.Add(artifact);
            }
        }

        return Assert.Single(matches);
    }

    private static bool AssemblyDefinitionMayMatchReference(
        MetadataReader reader,
        W8AssemblyReferenceIdentity reference)
    {
        var definition = reader.GetAssemblyDefinition();
        return string.Equals(reader.GetString(definition.Name), reference.Name, StringComparison.Ordinal) &&
            definition.Version == reference.Version &&
            string.Equals(
                definition.Culture.IsNil ? string.Empty : reader.GetString(definition.Culture),
                reference.Culture,
                StringComparison.Ordinal) &&
            NormalizeAssemblyFlags(definition.Flags) == NormalizeAssemblyFlags(reference.Flags);
    }

    private static bool AssemblyReferenceTargetsDefinition(
        W8AssemblyReferenceIdentity reference,
        W8AssemblyDefinitionIdentity definition)
    {
        var expectedKey = (reference.Flags & AssemblyFlags.PublicKey) != 0
            ? definition.PublicKey
            : definition.PublicKeyToken;
        return string.Equals(reference.Name, definition.Name, StringComparison.Ordinal) &&
            reference.Version == definition.Version &&
            string.Equals(reference.Culture, definition.Culture, StringComparison.Ordinal) &&
            reference.PublicKeyOrToken.AsSpan().SequenceEqual(expectedKey.AsSpan()) &&
            NormalizeAssemblyFlags(reference.Flags) == NormalizeAssemblyFlags(definition.Flags);
    }

    private static AssemblyFlags NormalizeAssemblyFlags(AssemblyFlags flags) =>
        flags & ~AssemblyFlags.PublicKey;

    private static W8LoadedAssemblyArtifact ReadLoadedAssemblyArtifact(
        W8DumpView view,
        ClrModule module)
    {
        Assert.Contains(
            view.Modules,
            candidate => candidate.Address == module.Address &&
                candidate.AppDomain.Address == module.AppDomain.Address);
        using var dumpMetadata = ReadModuleMetadata(view.DataTarget.DataReader, module);
        Assert.True(dumpMetadata.Reader.IsAssembly);
        var artifactPath = module.Name ??
            throw new Xunit.Sdk.XunitException("A loaded assembly did not retain its artifact path.");
        var artifactLength = new FileInfo(artifactPath).Length;
        Assert.InRange(artifactLength, 1L, MaximumAssemblyArtifactBytes);
        var artifactBytes = File.ReadAllBytes(artifactPath);
        Assert.Equal(artifactLength, artifactBytes.Length);
        using var stream = new MemoryStream(artifactBytes, writable: false);
        using var peReader = new PEReader(stream);
        var artifactReader = peReader.GetMetadataReader();
        Assert.True(artifactReader.IsAssembly);
        var artifactMetadata = ImmutableArray.CreateRange(peReader.GetMetadata().GetContent());
        Assert.Equal(dumpMetadata.ModuleVersionId, artifactReader.GetGuid(artifactReader.GetModuleDefinition().Mvid));
        Assert.True(dumpMetadata.Bytes.AsSpan().SequenceEqual(artifactMetadata.AsSpan()));
        var assemblyName = AssemblyName.GetAssemblyName(artifactPath);
        var definition = ReadAssemblyDefinitionIdentity(dumpMetadata.Reader, assemblyName);
        return new W8LoadedAssemblyArtifact(
            module.Address,
            CreateArtifactContentIdentity(
                dumpMetadata.ModuleVersionId,
                artifactBytes,
                artifactMetadata.AsSpan()),
            definition,
            dumpMetadata.Bytes);
    }

    private static W8AssemblyDefinitionIdentity ReadAssemblyDefinitionIdentity(
        MetadataReader reader,
        AssemblyName assemblyName)
    {
        var definition = reader.GetAssemblyDefinition();
        var name = reader.GetString(definition.Name);
        var culture = definition.Culture.IsNil ? string.Empty : reader.GetString(definition.Culture);
        var publicKey = ImmutableArray.CreateRange(reader.GetBlobBytes(definition.PublicKey));
        var publicKeyToken = ImmutableArray.CreateRange(assemblyName.GetPublicKeyToken() ?? []);
        Assert.Equal(name, assemblyName.Name);
        Assert.Equal(definition.Version, assemblyName.Version);
        Assert.Equal(culture, assemblyName.CultureName ?? string.Empty);
        Assert.Equal((int)definition.Flags, (int)assemblyName.Flags);
        Assert.Equal(
            Convert.ToHexString(publicKey.AsSpan()),
            Convert.ToHexString(assemblyName.GetPublicKey() ?? []));
        return new W8AssemblyDefinitionIdentity(
            name,
            definition.Version,
            culture,
            publicKey,
            publicKeyToken,
            definition.Flags,
            definition.HashAlgorithm);
    }

    private static W8ArtifactContentIdentity CreateArtifactContentIdentity(
        Guid moduleVersionId,
        ReadOnlySpan<byte> artifactBytes,
        ReadOnlySpan<byte> metadataBytes) => new(
        moduleVersionId,
        new W8ByteContentIdentity(
            artifactBytes.Length,
            CanonicalReplayEncoding.ComputeSha256(artifactBytes)),
        new W8ByteContentIdentity(
            metadataBytes.Length,
            CanonicalReplayEncoding.ComputeSha256(metadataBytes)));

    private static void AssertSameArtifact(
        W8LoadedAssemblyArtifact expected,
        W8LoadedAssemblyArtifact actual)
    {
        Assert.Equal(expected.ModuleAddress, actual.ModuleAddress);
        Assert.Equal(expected.Content, actual.Content);
        Assert.Equal(expected.Definition.Name, actual.Definition.Name);
        Assert.Equal(expected.Definition.Version, actual.Definition.Version);
        Assert.Equal(expected.Definition.Culture, actual.Definition.Culture);
        Assert.Equal(
            Convert.ToHexString(expected.Definition.PublicKey.AsSpan()),
            Convert.ToHexString(actual.Definition.PublicKey.AsSpan()));
        Assert.Equal(
            Convert.ToHexString(expected.Definition.PublicKeyToken.AsSpan()),
            Convert.ToHexString(actual.Definition.PublicKeyToken.AsSpan()));
        Assert.Equal(expected.Definition.Flags, actual.Definition.Flags);
        Assert.Equal(expected.Definition.HashAlgorithm, actual.Definition.HashAlgorithm);
        Assert.True(expected.MetadataBytes.AsSpan().SequenceEqual(actual.MetadataBytes.AsSpan()));
    }

    private static string FormatArtifact(W8LoadedAssemblyArtifact artifact) =>
        $"module={artifact.ModuleAddress:x16},mvid={artifact.Content.ModuleVersionId:D}," +
        $"pe={FormatContent(artifact.Content.Artifact)},metadata={FormatContent(artifact.Content.Metadata)}," +
        $"definition={FormatAssemblyDefinition(artifact.Definition)}";

    private static string FormatContent(W8ByteContentIdentity content) =>
        $"{content.Length}:{content.Sha256}";

    private static string FormatAssemblyReference(W8AssemblyReferenceIdentity reference) =>
        $"{reference.Token:x8}:{reference.Name}:{reference.Version}:culture={reference.Culture}:" +
        $"key-or-token={Convert.ToHexString(reference.PublicKeyOrToken.AsSpan())}:" +
        $"flags={(int)reference.Flags:x8}:hash={Convert.ToHexString(reference.HashValue.AsSpan())}";

    private static string FormatAssemblyDefinition(W8AssemblyDefinitionIdentity definition) =>
        $"{definition.Name}:{definition.Version}:culture={definition.Culture}:" +
        $"public-key={Convert.ToHexString(definition.PublicKey.AsSpan())}:" +
        $"public-key-token={Convert.ToHexString(definition.PublicKeyToken.AsSpan())}:" +
        $"flags={(int)definition.Flags:x8}:algorithm={(int)definition.HashAlgorithm:x8}";

    private static W8NamedRvaObservation ObserveNamedRva(
        string dumpPath,
        W8StorageMetadataCatalog catalog)
    {
        using var view = W8DumpView.OpenWithoutConstructionOracle(dumpPath, catalog.TargetModuleVersionId);
        var modules = view.Runtime.EnumerateModules()
            .Take(MaximumRuntimeModules + 1)
            .ToArray();
        Assert.True(
            modules.Length <= MaximumRuntimeModules,
            $"Runtime-module traversal exceeded {MaximumRuntimeModules}; cap-plus-one observed {modules.Length}.");
        var module = Assert.Single(modules, candidate => string.Equals(
            Path.GetFileName(candidate.Name),
            W8TestTargetPaths.NamedRvaAssemblyFileName,
            StringComparison.OrdinalIgnoreCase));
        using var metadata = ReadModuleMetadata(view.DataTarget.DataReader, module);
        Assert.Equal(catalog.NamedRvaModuleVersionId, metadata.ModuleVersionId);
        Assert.True(metadata.Bytes.AsSpan().SequenceEqual(catalog.NamedRvaMetadataBytes.AsSpan()));
        Assert.Equal(catalog.NamedSentinelToken, FindFieldToken(
            metadata.Reader,
            NamedRvaNamespace,
            "NamedRvaStorage",
            "NamedSentinel"));
        Assert.Equal(catalog.NamedWideSentinelToken, FindFieldToken(
            metadata.Reader,
            NamedRvaNamespace,
            "NamedRvaStorage",
            "NamedWideSentinel"));

        IW8RvaCapabilities capabilities = new W8CountingRvaCapabilities(view.DataTarget.DataReader);
        Assert.InRange((ulong)catalog.NamedSentinelRva, 1UL, module.Size - (ulong)catalog.NamedSentinelBytes.Length);
        Assert.InRange((ulong)catalog.NamedWideSentinelRva, 1UL, module.Size - (ulong)catalog.NamedWideSentinelBytes.Length);
        var sentinelAddress = checked(module.ImageBase + (uint)catalog.NamedSentinelRva);
        var wideAddress = checked(module.ImageBase + (uint)catalog.NamedWideSentinelRva);
        var sentinel = capabilities.ReadValue(sentinelAddress, catalog.NamedSentinelBytes.Length);
        var wide = capabilities.ReadValue(wideAddress, catalog.NamedWideSentinelBytes.Length);
        Assert.Equal(catalog.NamedSentinelBytes, sentinel);
        Assert.Equal(catalog.NamedWideSentinelBytes, wide);
        Assert.Equal(0x21047A61, BinaryPrimitives.ReadInt32LittleEndian(sentinel));
        Assert.Equal(
            unchecked((long)0xD3E5A71942087A92UL),
            BinaryPrimitives.ReadInt64LittleEndian(wide));
        Assert.Equal(0, capabilities.RuntimeConstructionCalls);
        Assert.Equal(0, capabilities.StorageAcquisitionCalls);
        Assert.Equal(2, capabilities.MemoryCalls);
        var interceptionProbe = new W8CountingRvaCapabilities(view.DataTarget.DataReader);
        Assert.Throws<Xunit.Sdk.XunitException>(interceptionProbe.RequestRuntimeConstruction);
        Assert.Throws<Xunit.Sdk.XunitException>(interceptionProbe.RequestStorageAcquisition);
        Assert.Equal(1, interceptionProbe.RuntimeConstructionCalls);
        Assert.Equal(1, interceptionProbe.StorageAcquisitionCalls);
        Assert.Equal(0, interceptionProbe.MemoryCalls);
        var lines = ImmutableArray.Create(
            $"module|{module.Address:x16}|{module.ImageBase:x16}|{metadata.ModuleVersionId:D}",
            $"rva|{catalog.NamedSentinelToken:x8}|{catalog.NamedSentinelRva:x8}|{sentinelAddress:x16}|{Convert.ToHexString(sentinel)}",
            $"rva|{catalog.NamedWideSentinelToken:x8}|{catalog.NamedWideSentinelRva:x8}|{wideAddress:x16}|{Convert.ToHexString(wide)}",
            $"calls|runtime={capabilities.RuntimeConstructionCalls}|" +
            $"storage={capabilities.StorageAcquisitionCalls}|memory={capabilities.MemoryCalls}",
            "disposition|Exact|W8_NAMED_RVA_GEOMETRY_ATTRIBUTABLE");
        return new W8NamedRvaObservation(
            W8StorageBranchDisposition.Exact,
            capabilities.RuntimeConstructionCalls,
            capabilities.StorageAcquisitionCalls,
            capabilities.MemoryCalls,
            lines);
    }

    private static W8PrimitivePairObservation ObserveSelectedPrimitivePair(
        string dumpPath,
        W8StorageMetadataCatalog catalog)
    {
        using var view = W8DumpView.OpenWithoutConstructionOracle(dumpPath, catalog.TargetModuleVersionId);
        using var metadata = ReadModuleMetadata(view.DataTarget.DataReader, view.TargetModule);
        var type = ReadRuntimeTypeWithStaticFields(view.TargetModule, catalog.PrimitiveStorageToken);
        var fields = ReadRuntimeFields(type.StaticFields);
        var int32Field = Assert.Single(
            fields,
            candidate => candidate.Token == catalog.PrimitiveFieldTokens["Int32"]);
        var uint32Field = Assert.Single(
            fields,
            candidate => candidate.Token == catalog.PrimitiveFieldTokens["UInt32"]);
        var int32Address = int32Field.GetAddress(view.TargetModule.AppDomain);
        var uint32Address = uint32Field.GetAddress(view.TargetModule.AppDomain);
        return new W8PrimitivePairObservation(
            metadata.ModuleVersionId,
            view.TargetModule.Address,
            int32Address,
            uint32Address,
            ImmutableArray.Create(ReadExact(view.DataTarget.DataReader, int32Address, sizeof(int))),
            ImmutableArray.Create(ReadExact(view.DataTarget.DataReader, uint32Address, sizeof(uint))));
    }

    private static W8CdacMethodTableIdentity FindExactGenericConstruction(
        W8DumpView view,
        int genericTypeToken,
        int argumentTypeToken)
    {
        var candidates = ImmutableArray.CreateBuilder<W8CdacMethodTableIdentity>();
        foreach (var module in view.Modules)
        {
            var available = view.Oracle.ReadAvailableTypes(module.Address);
            foreach (var entry in available.Entries)
            {
                if (!view.Oracle.TryReadMethodTableIdentity(entry.TypeHandle, out var identity) ||
                    identity is null ||
                    identity.ModuleAddress != view.TargetModule.Address ||
                    identity.TypeDefToken != genericTypeToken ||
                    identity.TypeArgumentHandles.Length != 1)
                {
                    continue;
                }

                var argument = view.Oracle.ReadTypeShape(identity.TypeArgumentHandles[0]);
                if (argument.Kind == W8CdacTypeShapeKind.MethodTable &&
                    !argument.IsArray &&
                    argument.ModuleAddress == view.TargetModule.Address &&
                    argument.TypeDefToken == argumentTypeToken)
                {
                    candidates.Add(identity);
                }
            }
        }

        return Assert.Single(candidates);
    }

    private static ImmutableArray<ClrStaticField> ReadRuntimeFields(
        IEnumerable<ClrStaticField> source)
    {
        var fields = source.Take(MaximumRuntimeFields + 1).ToImmutableArray();
        Assert.True(
            fields.Length <= MaximumRuntimeFields,
            $"Runtime-field traversal exceeded {MaximumRuntimeFields}; cap-plus-one observed {fields.Length}.");
        Assert.NotEmpty(fields);
        Assert.Equal(fields.Length, fields.Select(static field => field.Token).Distinct().Count());
        return fields;
    }

    private static ImmutableArray<ClrThreadStaticField> ReadThreadStaticFields(ClrType type)
    {
        var fields = type.ThreadStaticFields.Take(MaximumRuntimeFields + 1).ToImmutableArray();
        Assert.True(
            fields.Length <= MaximumRuntimeFields,
            $"Thread-relative field traversal exceeded {MaximumRuntimeFields}; " +
            $"cap-plus-one observed {fields.Length}.");
        Assert.Equal(fields.Length, fields.Select(static field => field.Token).Distinct().Count());
        return fields;
    }

    private static ClrType ReadRuntimeTypeWithStaticFields(ClrModule module, int typeToken)
    {
        var types = module.EnumerateTypesWithStaticFields()
            .Take(MaximumRuntimeTypesWithStatics + 1)
            .ToArray();
        Assert.True(
            types.Length <= MaximumRuntimeTypesWithStatics,
            $"Runtime static-type traversal exceeded {MaximumRuntimeTypesWithStatics}; " +
            $"cap-plus-one observed {types.Length}.");
        return Assert.Single(types, candidate => candidate.MetadataToken == typeToken);
    }

    private static ImmutableArray<W8PrimitiveExpectation> PrimitiveExpectations(int pointerSize)
    {
        Assert.True(pointerSize is sizeof(uint) or sizeof(ulong));
        var nativeInt = pointerSize == sizeof(uint)
            ? LittleEndian(unchecked((int)-0x1234567))
            : LittleEndian(unchecked((long)-0x1234567));
        var nativeUInt = pointerSize == sizeof(uint)
            ? LittleEndian(0x01234567U)
            : LittleEndian(0x0000000001234567UL);
        return
        [
            new("Boolean", [1]),
            new("Int8", [unchecked((byte)-0x12)]),
            new("UInt8", [0xE1]),
            new("Int16", LittleEndian((short)-0x1234)),
            new("UInt16", LittleEndian((ushort)0xE123)),
            new("Int32", LittleEndian(unchecked((int)0x81A2B3C4))),
            new("UInt32", LittleEndian(0xE1A2B3C4U)),
            new("Int64", LittleEndian(unchecked((long)0x81A2B3C4D5E6F708UL))),
            new("UInt64", LittleEndian(0xE1A2B3C4D5E6F708UL)),
            new("NativeInt", nativeInt),
            new("NativeUInt", nativeUInt),
            new("Character", LittleEndian('\u4E2D')),
            new("Single", LittleEndian(17.25F)),
            new("Double", LittleEndian(-29.5D)),
            new("Enum", LittleEndian((short)0x0272)),
            new("SignedByteEnum", [unchecked((byte)-0x35)]),
            new("UnsignedByteEnum", [0xD3]),
            new("UnsignedInt16Enum", LittleEndian((ushort)0xD3E5)),
            new("SignedInt32Enum", LittleEndian(unchecked((int)0xD3E5A719))),
            new("UnsignedInt32Enum", LittleEndian(0xD3E5A719U)),
            new("SignedInt64Enum", LittleEndian(unchecked((long)0xD3E5A7192B4C6D8EUL))),
            new("UnsignedInt64Enum", LittleEndian(0xD3E5A7192B4C6D8EUL)),
        ];
    }

    private static byte[] LittleEndian(short value)
    {
        var bytes = new byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(ushort value)
    {
        var bytes = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(char value) => LittleEndian((ushort)value);

    private static byte[] LittleEndian(int value)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(uint value)
    {
        var bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(long value)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(ulong value)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(float value) => LittleEndian(BitConverter.SingleToInt32Bits(value));

    private static byte[] LittleEndian(double value) => LittleEndian(BitConverter.DoubleToInt64Bits(value));

    private static byte[] ReadExact(IMemoryReader memory, ulong address, int length)
    {
        Assert.NotEqual(0UL, address);
        Assert.InRange(length, 1, MaximumMetadataBytes);
        var bytes = new byte[length];
        var observed = memory.Read(address, bytes);
        Assert.Equal(length, observed);
        return bytes;
    }

    private static ulong DecodePointer(ReadOnlySpan<byte> bytes, int pointerSize) => pointerSize switch
    {
        sizeof(uint) when bytes.Length == sizeof(uint) => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        sizeof(ulong) when bytes.Length == sizeof(ulong) => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
        _ => throw new Xunit.Sdk.XunitException("The raw pointer has an invalid target width."),
    };

    private static W8SyntheticTypeShapeReader CreateBroadSyntheticTypeShapeReader(int argumentCount)
    {
        Assert.InRange(argumentCount, 1, MaximumTypeTopologyNodes);
        var shapes = new Dictionary<ulong, W8CdacTypeShape>();
        var arguments = Enumerable.Range(2, argumentCount)
            .Select(static value => checked((ulong)value))
            .ToImmutableArray();
        shapes.Add(1, CreateSyntheticMethodTableShape(1, arguments));
        foreach (var argument in arguments)
        {
            shapes.Add(argument, CreateSyntheticMethodTableShape(argument, []));
        }

        return new W8SyntheticTypeShapeReader(shapes.ToImmutableDictionary());
    }

    private static W8SyntheticTypeShapeReader CreateSyntheticTypeShapeChain(int nodeCount)
    {
        Assert.InRange(nodeCount, 1, MaximumTypeTopologyNodes);
        var shapes = new Dictionary<ulong, W8CdacTypeShape>();
        for (var index = 1; index <= nodeCount; index++)
        {
            var handle = checked((ulong)index);
            shapes.Add(handle, new W8CdacTypeShape(
                handle,
                W8CdacTypeShapeKind.TypeDescriptor,
                ModuleAddress: 0,
                TypeDefToken: 0,
                TypeArgumentHandles: [],
                IsArray: false,
                IsSzArray: false,
                ArrayRank: 0,
                ElementOrParameterTypeHandle: index == nodeCount ? 0 : checked((ulong)(index + 1)),
                ElementType: 0x1D));
        }

        return new W8SyntheticTypeShapeReader(shapes.ToImmutableDictionary());
    }

    private static W8SyntheticTypeShapeReader CreateCyclicSyntheticTypeShapeReader()
    {
        var shapes = new Dictionary<ulong, W8CdacTypeShape>
        {
            [1] = new W8CdacTypeShape(
                1,
                W8CdacTypeShapeKind.TypeDescriptor,
                ModuleAddress: 0,
                TypeDefToken: 0,
                TypeArgumentHandles: [],
                IsArray: false,
                IsSzArray: false,
                ArrayRank: 0,
                ElementOrParameterTypeHandle: 2,
                ElementType: 0x1D),
            [2] = new W8CdacTypeShape(
                2,
                W8CdacTypeShapeKind.TypeDescriptor,
                ModuleAddress: 0,
                TypeDefToken: 0,
                TypeArgumentHandles: [],
                IsArray: false,
                IsSzArray: false,
                ArrayRank: 0,
                ElementOrParameterTypeHandle: 1,
                ElementType: 0x1D),
        };
        return new W8SyntheticTypeShapeReader(shapes.ToImmutableDictionary());
    }

    private static W8CdacTypeShape CreateSyntheticMethodTableShape(
        ulong handle,
        ImmutableArray<ulong> arguments) => new(
        handle,
        W8CdacTypeShapeKind.MethodTable,
        ModuleAddress: 0x1000,
        TypeDefToken: checked(0x02000000 | (int)handle),
        TypeArgumentHandles: arguments,
        IsArray: false,
        IsSzArray: false,
        ArrayRank: 0,
        ElementOrParameterTypeHandle: 0,
        ElementType: 0);

    private static string FormatTypeShape(
        W8CdacRuntimeConstructionOracle oracle,
        ulong typeHandle) => FormatTypeShape(
            new W8CdacTypeShapeReader(oracle),
            typeHandle,
            MaximumTypeTopologyDepth,
            MaximumTypeTopologyNodes).CanonicalShape;

    private static W8FormattedTypeShape FormatTypeShape(
        IW8TypeShapeReader reader,
        ulong typeHandle,
        int maximumDepth,
        int maximumNodes)
    {
        var budget = new W8TypeTopologyBudget(maximumNodes);
        var canonicalShape = FormatTypeShape(
            reader,
            typeHandle,
            depth: 0,
            maximumDepth,
            new HashSet<ulong>(),
            budget);
        return new W8FormattedTypeShape(canonicalShape, budget.NodeCount);
    }

    private static string FormatTypeShape(
        IW8TypeShapeReader reader,
        ulong typeHandle,
        int depth,
        int maximumDepth,
        HashSet<ulong> path,
        W8TypeTopologyBudget budget)
    {
        if (depth > maximumDepth || !path.Add(typeHandle))
        {
            throw new Xunit.Sdk.XunitException("The runtime type topology exceeded its depth or cycle bound.");
        }

        try
        {
            budget.Enter();
            var shape = reader.ReadTypeShape(typeHandle);
            if (shape.Kind == W8CdacTypeShapeKind.TypeDescriptor)
            {
                var parameter = shape.ElementOrParameterTypeHandle == 0
                    ? string.Empty
                    : "[" + FormatTypeShape(
                        reader,
                        shape.ElementOrParameterTypeHandle,
                        checked(depth + 1),
                        maximumDepth,
                        path,
                        budget) + "]";
                return $"desc:{shape.TypeHandle:x16}:{shape.ElementType:x2}{parameter}";
            }

            if (shape.IsArray)
            {
                return $"{(shape.IsSzArray ? "sz" : $"md{shape.ArrayRank}")}:" +
                    $"{shape.TypeHandle:x16}[" +
                    FormatTypeShape(
                        reader,
                        shape.ElementOrParameterTypeHandle,
                        checked(depth + 1),
                        maximumDepth,
                        path,
                        budget) + "]";
            }

            var arguments = shape.TypeArgumentHandles.Length == 0
                ? string.Empty
                : "<" + string.Join(
                    ",",
                    shape.TypeArgumentHandles.Select(argument => FormatTypeShape(
                        reader,
                        argument,
                        checked(depth + 1),
                        maximumDepth,
                        path,
                        budget))) + ">";
            return $"mt:{shape.TypeHandle:x16}:{shape.ModuleAddress:x16}:{shape.TypeDefToken:x8}{arguments}";
        }
        finally
        {
            _ = path.Remove(typeHandle);
        }
    }

    private static W8ModuleMetadata ReadModuleMetadata(IMemoryReader memory, ClrModule module)
    {
        Assert.NotEqual(0UL, module.MetadataAddress);
        Assert.InRange(module.MetadataLength, 1UL, checked((ulong)MaximumMetadataBytes));
        var bytes = ReadExact(memory, module.MetadataAddress, checked((int)module.MetadataLength));
        return W8ModuleMetadata.Open(bytes);
    }

    private static int FindTopLevelTypeToken(
        MetadataReader reader,
        string expectedNamespace,
        string expectedName) => MetadataTokens.GetToken(
            FindTopLevelType(reader, expectedNamespace, expectedName));

    private static int FindFieldToken(
        MetadataReader reader,
        string expectedNamespace,
        string typeName,
        string fieldName)
    {
        var typeHandle = MetadataTokens.TypeDefinitionHandle(
            FindTopLevelTypeToken(reader, expectedNamespace, typeName) & 0x00FFFFFF);
        return MetadataTokens.GetToken(Assert.Single(
            ReadBounded(
                reader.GetTypeDefinition(typeHandle).GetFields(),
                MaximumMetadataFieldsPerType,
                "FieldDefs"),
            handle => string.Equals(
                reader.GetString(reader.GetFieldDefinition(handle).Name),
                fieldName,
                StringComparison.Ordinal)));
    }

    private static void WriteDump(string profile, string dumpPath)
    {
        using var target = TestTargetRunner.StartAndWaitReady(
            RequireArtifact(W8TestTargetPaths.ResolveExecutable()),
            ["--truth-gate", profile],
            isolatedDirectory: null);
        DumpWriter.WriteFullDump(target.Pid, dumpPath);
    }

    private static void PatchDumpVirtualByte(string dumpPath, ulong address, byte xorMask)
    {
        using var stream = new FileStream(dumpPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Assert.Equal(0x504D444DU, reader.ReadUInt32());
        _ = reader.ReadUInt32();
        var streamCount = reader.ReadUInt32();
        var directoryRva = reader.ReadUInt32();
        Assert.InRange(streamCount, 1U, 1_024U);
        ulong? memory64ListRva = null;
        for (var index = 0U; index < streamCount; index++)
        {
            stream.Position = checked(directoryRva + index * 12L);
            var streamType = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            var rva = reader.ReadUInt32();
            if (streamType == 9)
            {
                Assert.Null(memory64ListRva);
                memory64ListRva = rva;
            }
        }

        Assert.NotNull(memory64ListRva);
        stream.Position = checked((long)memory64ListRva!.Value);
        var rangeCount = reader.ReadUInt64();
        var dataRva = reader.ReadUInt64();
        Assert.InRange(rangeCount, 1UL, 1_000_000UL);
        var dataOffset = dataRva;
        ulong? patchOffset = null;
        for (var index = 0UL; index < rangeCount; index++)
        {
            stream.Position = checked((long)(memory64ListRva.Value + 16UL + index * 16UL));
            var start = reader.ReadUInt64();
            var size = reader.ReadUInt64();
            if (address >= start && address - start < size)
            {
                Assert.Null(patchOffset);
                patchOffset = checked(dataOffset + address - start);
            }

            dataOffset = checked(dataOffset + size);
        }

        Assert.NotNull(patchOffset);
        Assert.InRange(patchOffset!.Value, 0UL, checked((ulong)stream.Length - 1UL));
        stream.Position = checked((long)patchOffset.Value);
        var original = reader.ReadByte();
        stream.Position = checked((long)patchOffset.Value);
        writer.Write((byte)(original ^ xorMask));
        writer.Flush();
    }

    private static void PatchPeRvaByte(string path, int rva, byte xorMask)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var section = Assert.Single(
            ReadBounded(peReader.PEHeaders.SectionHeaders, MaximumPeSections, "PE sections"),
            candidate => rva >= candidate.VirtualAddress &&
                rva - candidate.VirtualAddress < Math.Max(candidate.VirtualSize, candidate.SizeOfRawData));
        var fileOffset = checked(section.PointerToRawData + rva - section.VirtualAddress);
        Assert.InRange(fileOffset, 0, checked((int)stream.Length - 1));
        stream.Position = fileOffset;
        var original = stream.ReadByte();
        Assert.InRange(original, byte.MinValue, byte.MaxValue);
        stream.Position = fileOffset;
        stream.WriteByte((byte)(original ^ xorMask));
        stream.Flush(flushToDisk: true);
    }

    private static string TemporaryPath(string prefix, string extension) =>
        Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{extension}");

    private static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"Required W8 artifact was not found: {path}");
        return path;
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void AssertCanonicalReplay(
        ImmutableArray<string> expected,
        ImmutableArray<string> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.True(
                string.Equals(expected[index], actual[index], StringComparison.Ordinal),
                $"Canonical replay differs at line {index}:{Environment.NewLine}" +
                $"expected: {expected[index]}{Environment.NewLine}" +
                $"actual:   {actual[index]}");
        }
    }

    private static W8PhysicalEvidenceStatus CompareBytes(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> observed) => expected.SequenceEqual(observed)
            ? W8PhysicalEvidenceStatus.Exact
            : W8PhysicalEvidenceStatus.Conflict;

    private static ImmutableArray<string> DecodeLiterals(
        string assemblyPath,
        IW8LiteralCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var bytes = capabilities.ReadMetadataArtifact(assemblyPath);
        using var stream = new MemoryStream(bytes, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var typeHandle = FindTopLevelType(reader, TargetNamespace, "PrimitiveStorage");
        var fields = ReadBounded(
                reader.GetTypeDefinition(typeHandle).GetFields(),
                MaximumMetadataFieldsPerType,
                "literal FieldDefs")
            .ToDictionary(
            handle => reader.GetString(reader.GetFieldDefinition(handle).Name),
            static handle => handle,
            StringComparer.Ordinal);
        var expected = LiteralExpectations();
        var actualLiteralNames = fields
            .Where(pair => (reader.GetFieldDefinition(pair.Value).Attributes & FieldAttributes.Literal) != 0)
            .Select(static pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actualLiteralNames);

        var lines = ImmutableArray.CreateBuilder<string>();
        foreach (var pair in expected.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var fieldHandle = fields[pair.Key];
            var field = reader.GetFieldDefinition(fieldHandle);
            Assert.True(
                (field.Attributes & (FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault)) ==
                (FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault));
            Assert.False((field.Attributes & (FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA)) != 0);
            var constant = reader.GetConstant(field.GetDefaultValue());
            var value = reader.GetBlobBytes(constant.Value);
            Assert.Equal(pair.Value.TypeCode, constant.TypeCode);
            Assert.Equal(pair.Value.Bytes, value);
            lines.Add(
                $"literal|{pair.Key}|{MetadataTokens.GetToken(fieldHandle):x8}|{constant.TypeCode}|" +
                $"{Convert.ToHexString(value)}");
        }

        var decimalHandle = fields["DecimalLiteral"];
        var decimalField = reader.GetFieldDefinition(decimalHandle);
        Assert.Equal(
            FieldAttributes.Static | FieldAttributes.InitOnly,
            decimalField.Attributes & (FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Literal));
        Assert.True(decimalField.GetDefaultValue().IsNil);
        var decimalAttributeHandle = Assert.Single(
            ReadBounded(
                decimalField.GetCustomAttributes(),
                MaximumMetadataCustomAttributes,
                "decimal CustomAttribute rows"),
            handle => IsNamedAttribute(
                reader,
                handle,
                "System.Runtime.CompilerServices",
                "DecimalConstantAttribute"));
        var blob = reader.GetBlobReader(reader.GetCustomAttribute(decimalAttributeHandle).Value);
        Assert.Equal(1, blob.ReadUInt16());
        var expectedDecimal = 9876.5432M;
        var bits = decimal.GetBits(expectedDecimal);
        var scale = blob.ReadByte();
        var sign = blob.ReadByte();
        var high = blob.ReadUInt32();
        var middle = blob.ReadUInt32();
        var low = blob.ReadUInt32();
        Assert.Equal((byte)((bits[3] >> 16) & 0x7F), scale);
        Assert.Equal((byte)((bits[3] >> 31) & 1), sign);
        Assert.Equal(unchecked((uint)bits[2]), high);
        Assert.Equal(unchecked((uint)bits[1]), middle);
        Assert.Equal(unchecked((uint)bits[0]), low);
        Assert.Equal(0, blob.ReadUInt16());
        Assert.Equal(0, blob.RemainingBytes);
        lines.Add(
            $"literal|DecimalLiteral|{MetadataTokens.GetToken(decimalHandle):x8}|decimal|" +
            $"{scale:x2}{sign:x2}{high:x8}{middle:x8}{low:x8}");
        lines.Add(
            $"calls|metadata={capabilities.MetadataArtifactCalls}|runtime={capabilities.RuntimeConstructionCalls}|" +
            $"storage={capabilities.StorageAcquisitionCalls}|memory={capabilities.MemoryCalls}");
        return lines.ToImmutable();
    }

    private static ImmutableDictionary<string, W8LiteralExpectation> LiteralExpectations() =>
        new Dictionary<string, W8LiteralExpectation>(StringComparer.Ordinal)
        {
            ["BooleanLiteral"] = new(ConstantTypeCode.Boolean, [1]),
            ["Int8Literal"] = new(ConstantTypeCode.SByte, [unchecked((byte)-0x35)]),
            ["UInt8Literal"] = new(ConstantTypeCode.Byte, [0xD3]),
            ["Int16Literal"] = new(ConstantTypeCode.Int16, LittleEndian((short)-0x3527)),
            ["UInt16Literal"] = new(ConstantTypeCode.UInt16, LittleEndian((ushort)0xD3E5)),
            ["Int32Literal"] = new(ConstantTypeCode.Int32, LittleEndian(unchecked((int)0x81234567))),
            ["UInt32Literal"] = new(ConstantTypeCode.UInt32, LittleEndian(0xD3E5A719U)),
            ["Int64Literal"] = new(
                ConstantTypeCode.Int64,
                LittleEndian(unchecked((long)0xD3E5A7192B4C6D8EUL))),
            ["UInt64Literal"] = new(ConstantTypeCode.UInt64, LittleEndian(0xE123456789ABCDEFUL)),
            ["CharacterLiteral"] = new(ConstantTypeCode.Char, LittleEndian('\u03A9')),
            ["SingleLiteral"] = new(ConstantTypeCode.Single, LittleEndian(-19.625F)),
            ["DoubleLiteral"] = new(ConstantTypeCode.Double, LittleEndian(-17.125D)),
            ["EnumLiteral"] = new(ConstantTypeCode.Int16, LittleEndian((short)0x0171)),
            ["SignedByteEnumLiteral"] = new(ConstantTypeCode.SByte, [unchecked((byte)-0x35)]),
            ["UnsignedByteEnumLiteral"] = new(ConstantTypeCode.Byte, [0xD3]),
            ["UnsignedInt16EnumLiteral"] = new(ConstantTypeCode.UInt16, LittleEndian((ushort)0xD3E5)),
            ["SignedInt32EnumLiteral"] = new(
                ConstantTypeCode.Int32,
                LittleEndian(unchecked((int)0xD3E5A719))),
            ["UnsignedInt32EnumLiteral"] = new(ConstantTypeCode.UInt32, LittleEndian(0xD3E5A719U)),
            ["SignedInt64EnumLiteral"] = new(
                ConstantTypeCode.Int64,
                LittleEndian(unchecked((long)0xD3E5A7192B4C6D8EUL))),
            ["UnsignedInt64EnumLiteral"] = new(
                ConstantTypeCode.UInt64,
                LittleEndian(0xD3E5A7192B4C6D8EUL)),
            ["StringLiteral"] = new(ConstantTypeCode.String, Encoding.Unicode.GetBytes("w8-literal")),
            ["NullLiteral"] = new(ConstantTypeCode.NullReference, new byte[sizeof(int)]),
            ["ObjectNullLiteral"] = new(ConstantTypeCode.NullReference, new byte[sizeof(int)]),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static W8NamedRvaArtifactObservation ReadNamedRvaArtifact(
        string path,
        W8StorageMetadataCatalog catalog)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
        var sentinelHandle = FindField(
            reader,
            NamedRvaNamespace,
            "NamedRvaStorage",
            "NamedSentinel");
        var wideHandle = FindField(
            reader,
            NamedRvaNamespace,
            "NamedRvaStorage",
            "NamedWideSentinel");
        var sentinel = reader.GetFieldDefinition(sentinelHandle);
        var wide = reader.GetFieldDefinition(wideHandle);
        return new W8NamedRvaArtifactObservation(
            mvid,
            ImmutableArray.CreateRange(peReader.GetMetadata().GetContent()),
            MetadataTokens.GetToken(sentinelHandle),
            MetadataTokens.GetToken(wideHandle),
            sentinel.GetRelativeVirtualAddress(),
            wide.GetRelativeVirtualAddress(),
            peReader.GetSectionData(sentinel.GetRelativeVirtualAddress())
                .GetContent(0, catalog.NamedSentinelBytes.Length)
                .ToArray(),
            peReader.GetSectionData(wide.GetRelativeVirtualAddress())
                .GetContent(0, catalog.NamedWideSentinelBytes.Length)
                .ToArray());
    }

    private static TypeDefinitionHandle FindTopLevelType(
        MetadataReader reader,
        string expectedNamespace,
        string expectedName) => Assert.Single(
            ReadBounded(reader.TypeDefinitions, MaximumMetadataTypes, "TypeDefs"),
            handle =>
            {
                var definition = reader.GetTypeDefinition(handle);
                return definition.GetDeclaringType().IsNil &&
                    string.Equals(reader.GetString(definition.Namespace), expectedNamespace, StringComparison.Ordinal) &&
                    string.Equals(reader.GetString(definition.Name), expectedName, StringComparison.Ordinal);
            });

    private static FieldDefinitionHandle FindField(
        MetadataReader reader,
        string expectedNamespace,
        string expectedTypeName,
        string expectedFieldName)
    {
        var type = reader.GetTypeDefinition(FindTopLevelType(reader, expectedNamespace, expectedTypeName));
        return Assert.Single(
            ReadBounded(type.GetFields(), MaximumMetadataFieldsPerType, "FieldDefs"),
            handle => string.Equals(
                reader.GetString(reader.GetFieldDefinition(handle).Name),
                expectedFieldName,
                StringComparison.Ordinal));
    }

    private static bool IsNamedAttribute(
        MetadataReader reader,
        CustomAttributeHandle handle,
        string expectedNamespace,
        string expectedName)
    {
        var attribute = reader.GetCustomAttribute(handle);
        EntityHandle typeHandle;
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                typeHandle = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
                break;
            case HandleKind.MethodDefinition:
                typeHandle = FindDeclaringType(reader, (MethodDefinitionHandle)attribute.Constructor);
                break;
            default:
                return false;
        }

        return typeHandle.Kind switch
        {
            HandleKind.TypeReference => IsNamedTypeReference(
                reader,
                (TypeReferenceHandle)typeHandle,
                expectedNamespace,
                expectedName),
            HandleKind.TypeDefinition => IsNamedTypeDefinition(
                reader,
                (TypeDefinitionHandle)typeHandle,
                expectedNamespace,
                expectedName),
            _ => false,
        };
    }

    private static W8ContextMarkerIdentity ReadExactContextMarkerIdentity(
        MetadataReader reader,
        FieldDefinitionHandle fieldHandle)
    {
        var attributeHandle = Assert.Single(
            ReadBounded(
                reader.GetFieldDefinition(fieldHandle).GetCustomAttributes(),
                MaximumMetadataCustomAttributes,
                "context-field CustomAttribute rows"),
            handle => IsNamedAttribute(reader, handle, "System", "ContextStaticAttribute"));
        var attribute = reader.GetCustomAttribute(attributeHandle);
        Assert.Equal(HandleKind.MemberReference, attribute.Constructor.Kind);
        var constructorHandle = (MemberReferenceHandle)attribute.Constructor;
        var constructor = reader.GetMemberReference(constructorHandle);
        Assert.Equal(".ctor", reader.GetString(constructor.Name));
        Assert.Equal(HandleKind.TypeReference, constructor.Parent.Kind);
        var typeHandle = (TypeReferenceHandle)constructor.Parent;
        var typeReference = reader.GetTypeReference(typeHandle);
        Assert.Equal("System", reader.GetString(typeReference.Namespace));
        Assert.Equal("ContextStaticAttribute", reader.GetString(typeReference.Name));
        Assert.Equal(HandleKind.AssemblyReference, typeReference.ResolutionScope.Kind);
        var assemblyHandle = (AssemblyReferenceHandle)typeReference.ResolutionScope;
        Assert.InRange(
            MetadataTokens.GetRowNumber(assemblyHandle),
            1,
            reader.GetTableRowCount(TableIndex.AssemblyRef));
        return new W8ContextMarkerIdentity(
            MetadataTokens.GetToken(attributeHandle),
            MetadataTokens.GetToken(constructorHandle),
            ImmutableArray.CreateRange(reader.GetBlobBytes(constructor.Signature)),
            ImmutableArray.CreateRange(reader.GetBlobBytes(attribute.Value)),
            MetadataTokens.GetToken(typeHandle),
            reader.GetString(typeReference.Namespace),
            reader.GetString(typeReference.Name),
            ReadAssemblyReferenceIdentity(reader, assemblyHandle));
    }

    private static W8AssemblyReferenceIdentity ReadAssemblyReferenceIdentity(
        MetadataReader reader,
        AssemblyReferenceHandle handle)
    {
        Assert.InRange(
            MetadataTokens.GetRowNumber(handle),
            1,
            reader.GetTableRowCount(TableIndex.AssemblyRef));
        var reference = reader.GetAssemblyReference(handle);
        return new W8AssemblyReferenceIdentity(
            MetadataTokens.GetToken(handle),
            reader.GetString(reference.Name),
            reference.Version,
            reference.Culture.IsNil ? string.Empty : reader.GetString(reference.Culture),
            ImmutableArray.CreateRange(reader.GetBlobBytes(reference.PublicKeyOrToken)),
            reference.Flags,
            ImmutableArray.CreateRange(reader.GetBlobBytes(reference.HashValue)));
    }

    private static TypeDefinitionHandle FindDeclaringType(
        MetadataReader reader,
        MethodDefinitionHandle methodHandle) => Assert.Single(
            ReadBounded(reader.TypeDefinitions, MaximumMetadataTypes, "attribute owner TypeDefs"),
            handle => ReadBounded(
                    reader.GetTypeDefinition(handle).GetMethods(),
                    MaximumMetadataMethodsPerType,
                    "attribute owner MethodDefs")
                .Contains(methodHandle));

    private static ImmutableArray<T> ReadBounded<T>(
        IEnumerable<T> source,
        int maximumCount,
        string label)
    {
        var values = source.Take(checked(maximumCount + 1)).ToImmutableArray();
        Assert.True(
            values.Length <= maximumCount,
            $"{label} traversal exceeded {maximumCount}; cap-plus-one observed {values.Length}.");
        return values;
    }

    private static bool IsNamedTypeReference(
        MetadataReader reader,
        TypeReferenceHandle handle,
        string expectedNamespace,
        string expectedName)
    {
        var reference = reader.GetTypeReference(handle);
        return string.Equals(reader.GetString(reference.Namespace), expectedNamespace, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(reference.Name), expectedName, StringComparison.Ordinal);
    }

    private static bool IsNamedTypeDefinition(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        string expectedNamespace,
        string expectedName)
    {
        var definition = reader.GetTypeDefinition(handle);
        return string.Equals(reader.GetString(definition.Namespace), expectedNamespace, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(definition.Name), expectedName, StringComparison.Ordinal);
    }

    private sealed record W8StorageMetadataCatalog(
        Guid TargetModuleVersionId,
        int PrimitiveStorageToken,
        ImmutableDictionary<string, int> PrimitiveFieldTokens,
        int GenericSlotToken,
        int ThreadSentinelToken,
        int RequestContextToken,
        int BatchContextToken,
        int ThreadProfileToken,
        int ThreadWorkerMethodToken,
        int ContextStorageToken,
        int ContextSentinelToken,
        W8ContextMarkerIdentity ContextMarkerIdentity,
        int AssignabilityStorageToken,
        ImmutableDictionary<string, int> AssignabilityFieldTokens,
        int AssignabilityBaseToken,
        int AssignabilityDerivedToken,
        int AssignabilityDerivedBaseToken,
        int AssignabilityCarrierToken,
        int InvariantNodeToken,
        int CovariantNodeToken,
        int ContravariantNodeToken,
        int ValueContextToken,
        GenericParameterAttributes InvariantVariance,
        GenericParameterAttributes CovariantVariance,
        GenericParameterAttributes ContravariantVariance,
        ImmutableArray<string> CarrierInterfaceSignatures,
        Guid NamedRvaModuleVersionId,
        ImmutableArray<byte> NamedRvaMetadataBytes,
        int NamedSentinelToken,
        int NamedWideSentinelToken,
        int NamedSentinelRva,
        int NamedWideSentinelRva,
        ImmutableArray<byte> NamedSentinelBytes,
        ImmutableArray<byte> NamedWideSentinelBytes)
    {
        internal static W8StorageMetadataCatalog Read(string targetPath, string namedRvaPath)
        {
            using var targetStream = File.OpenRead(targetPath);
            using var targetPe = new PEReader(targetStream);
            var reader = targetPe.GetMetadataReader();
            var primitiveHandle = FindTopLevelType(reader, TargetNamespace, "PrimitiveStorage");
            var primitiveFields = ReadFieldTokens(reader, primitiveHandle);
            var genericSlotHandle = FindTopLevelType(reader, TargetNamespace, "GenericSlot`1");
            var genericSlotFields = ReadFieldTokens(reader, genericSlotHandle);
            var requestHandle = FindTopLevelType(reader, TargetNamespace, "RequestContext");
            var batchHandle = FindTopLevelType(reader, TargetNamespace, "BatchContext");
            var threadProfileHandle = FindTopLevelType(reader, TargetNamespace, "ThreadRelativeProfile");
            var workerMethod = Assert.Single(
                ReadBounded(
                    reader.GetTypeDefinition(threadProfileHandle).GetMethods(),
                    MaximumMetadataMethodsPerType,
                    "thread-profile MethodDefs"),
                handle => string.Equals(
                    reader.GetString(reader.GetMethodDefinition(handle).Name),
                    "ThreadRelativeWorker",
                    StringComparison.Ordinal));
            var contextHandle = FindTopLevelType(reader, TargetNamespace, "ContextRelativeStorage");
            var contextFields = ReadFieldTokens(reader, contextHandle);
            var contextFieldHandle = MetadataTokens.FieldDefinitionHandle(
                contextFields["ContextSentinel"] & 0x00FFFFFF);
            var contextMarkerIdentity = ReadExactContextMarkerIdentity(reader, contextFieldHandle);

            var assignabilityStorageHandle = FindTopLevelType(
                reader,
                TargetNamespace,
                "ConstructedAssignabilityStorage");
            var assignabilityFields = ReadFieldTokens(reader, assignabilityStorageHandle);
            var baseHandle = FindTopLevelType(reader, TargetNamespace, "AssignabilityBaseNode");
            var derivedHandle = FindTopLevelType(reader, TargetNamespace, "AssignabilityDerivedNode");
            var derivedBase = reader.GetTypeDefinition(derivedHandle).BaseType;
            Assert.Equal(HandleKind.TypeDefinition, derivedBase.Kind);
            var invariantHandle = FindTopLevelType(reader, TargetNamespace, "IInvariantNode`1");
            var covariantHandle = FindTopLevelType(reader, TargetNamespace, "ICovariantNode`1");
            var contravariantHandle = FindTopLevelType(reader, TargetNamespace, "IContravariantNode`1");
            var carrierHandle = FindTopLevelType(reader, TargetNamespace, "AssignabilityCarrier`1");
            var valueContextHandle = FindTopLevelType(reader, TargetNamespace, "ValueContext");
            var formatter = new W8SignatureFormatter(reader);
            var carrierInterfaces = ReadBounded(
                    reader.GetTypeDefinition(carrierHandle).GetInterfaceImplementations(),
                    MaximumMetadataInterfacesPerType,
                    "carrier InterfaceImpl rows")
                .Select(handle => formatter.Format(reader.GetInterfaceImplementation(handle).Interface))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            Assert.Equal(3, carrierInterfaces.Length);
            Assert.Equal(
                new[]
                {
                    $"td:{MetadataTokens.GetToken(invariantHandle):x8}<var:0>",
                    $"td:{MetadataTokens.GetToken(covariantHandle):x8}<var:0>",
                    $"td:{MetadataTokens.GetToken(contravariantHandle):x8}<var:0>",
                }.Order(StringComparer.Ordinal),
                carrierInterfaces);

            using var namedStream = File.OpenRead(namedRvaPath);
            using var namedPe = new PEReader(namedStream);
            var namedReader = namedPe.GetMetadataReader();
            var namedSentinelHandle = FindField(
                namedReader,
                NamedRvaNamespace,
                "NamedRvaStorage",
                "NamedSentinel");
            var namedWideHandle = FindField(
                namedReader,
                NamedRvaNamespace,
                "NamedRvaStorage",
                "NamedWideSentinel");
            var namedSentinel = namedReader.GetFieldDefinition(namedSentinelHandle);
            var namedWide = namedReader.GetFieldDefinition(namedWideHandle);
            Assert.True((namedSentinel.Attributes & FieldAttributes.HasFieldRVA) != 0);
            Assert.True((namedWide.Attributes & FieldAttributes.HasFieldRVA) != 0);
            Assert.True(namedSentinel.GetRelativeVirtualAddress() > 0);
            Assert.True(namedWide.GetRelativeVirtualAddress() > 0);
            var namedSentinelBytes = namedPe.GetSectionData(namedSentinel.GetRelativeVirtualAddress())
                .GetContent(0, sizeof(int));
            var namedWideBytes = namedPe.GetSectionData(namedWide.GetRelativeVirtualAddress())
                .GetContent(0, sizeof(long));

            return new W8StorageMetadataCatalog(
                reader.GetGuid(reader.GetModuleDefinition().Mvid),
                MetadataTokens.GetToken(primitiveHandle),
                primitiveFields,
                MetadataTokens.GetToken(genericSlotHandle),
                genericSlotFields["ThreadSentinel"],
                MetadataTokens.GetToken(requestHandle),
                MetadataTokens.GetToken(batchHandle),
                MetadataTokens.GetToken(threadProfileHandle),
                MetadataTokens.GetToken(workerMethod),
                MetadataTokens.GetToken(contextHandle),
                contextFields["ContextSentinel"],
                contextMarkerIdentity,
                MetadataTokens.GetToken(assignabilityStorageHandle),
                assignabilityFields,
                MetadataTokens.GetToken(baseHandle),
                MetadataTokens.GetToken(derivedHandle),
                MetadataTokens.GetToken((TypeDefinitionHandle)derivedBase),
                MetadataTokens.GetToken(carrierHandle),
                MetadataTokens.GetToken(invariantHandle),
                MetadataTokens.GetToken(covariantHandle),
                MetadataTokens.GetToken(contravariantHandle),
                MetadataTokens.GetToken(valueContextHandle),
                ReadVariance(reader, invariantHandle),
                ReadVariance(reader, covariantHandle),
                ReadVariance(reader, contravariantHandle),
                carrierInterfaces,
                namedReader.GetGuid(namedReader.GetModuleDefinition().Mvid),
                ImmutableArray.CreateRange(namedPe.GetMetadata().GetContent()),
                MetadataTokens.GetToken(namedSentinelHandle),
                MetadataTokens.GetToken(namedWideHandle),
                namedSentinel.GetRelativeVirtualAddress(),
                namedWide.GetRelativeVirtualAddress(),
                ImmutableArray.CreateRange(namedSentinelBytes),
                ImmutableArray.CreateRange(namedWideBytes));
        }

        private static GenericParameterAttributes ReadVariance(
            MetadataReader reader,
            TypeDefinitionHandle handle)
        {
            var parameter = reader.GetGenericParameter(Assert.Single(ReadBounded(
                reader.GetTypeDefinition(handle).GetGenericParameters(),
                MaximumMetadataGenericParameters,
                "GenericParam rows")));
            return parameter.Attributes & GenericParameterAttributes.VarianceMask;
        }

        private static ImmutableDictionary<string, int> ReadFieldTokens(
            MetadataReader reader,
            TypeDefinitionHandle handle) => ReadBounded(
                    reader.GetTypeDefinition(handle).GetFields(),
                    MaximumMetadataFieldsPerType,
                    "FieldDefs")
                .ToImmutableDictionary(
                    field => reader.GetString(reader.GetFieldDefinition(field).Name),
                    static field => MetadataTokens.GetToken(field),
                    StringComparer.Ordinal);
    }

    private static W8BoundedSignaturePredecoder CreateTestSignaturePredecoder(
        IReadOnlyDictionary<TypeSpecificationHandle, ReadOnlyMemory<byte>>? signatures = null)
    {
        var typeSpecifications = signatures ??
            ImmutableDictionary<TypeSpecificationHandle, ReadOnlyMemory<byte>>.Empty;
        return new W8BoundedSignaturePredecoder(
            handle => typeSpecifications.TryGetValue(handle, out var signature)
                ? signature
                : throw new BadImageFormatException(
                    $"Synthetic TypeSpec 0x{MetadataTokens.GetToken(handle):x8} was not declared."),
            handle =>
            {
                var row = MetadataTokens.GetRowNumber(handle);
                if (row <= 0 ||
                    (handle.Kind == HandleKind.TypeSpecification &&
                    !typeSpecifications.ContainsKey((TypeSpecificationHandle)handle)))
                {
                    throw new BadImageFormatException(
                        $"Synthetic type handle 0x{MetadataTokens.GetToken(handle):x8} was not declared.");
                }
            },
            MaximumMetadataSignatureDepth,
            MaximumMetadataSignatureNodes);
    }

    private static W8SyntheticTypeSpecificationGraph CreateSyntheticTypeSpecificationGraph(
        int tailArgumentCount)
    {
        var root = MetadataTokens.TypeSpecificationHandle(1);
        var deepSibling = MetadataTokens.TypeSpecificationHandle(2);
        var broadSibling = MetadataTokens.TypeSpecificationHandle(3);
        var rootBytes = new List<byte>
        {
            0x15,
            0x12,
            0x05,
            0x02,
            0x12,
        };
        WriteCompressedUnsigned(rootBytes, EncodeTypeDefOrRef(deepSibling));
        rootBytes.Add(0x12);
        WriteCompressedUnsigned(rootBytes, EncodeTypeDefOrRef(broadSibling));
        var deepBytes = Enumerable
            .Repeat((byte)0x1D, MaximumMetadataSignatureDepth - 3)
            .Append((byte)0x08)
            .ToArray();
        return new W8SyntheticTypeSpecificationGraph(
            root,
            new Dictionary<TypeSpecificationHandle, ReadOnlyMemory<byte>>
            {
                [root] = rootBytes.ToArray(),
                [deepSibling] = deepBytes,
                [broadSibling] = RepeatedPrimitiveGenericSignature(tailArgumentCount),
            }.ToImmutableDictionary());
    }

    private static uint EncodeTypeDefOrRef(EntityHandle handle)
    {
        var tag = handle.Kind switch
        {
            HandleKind.TypeDefinition => 0U,
            HandleKind.TypeReference => 1U,
            HandleKind.TypeSpecification => 2U,
            _ => throw new ArgumentException("Expected a TypeDefOrRef handle.", nameof(handle)),
        };
        return checked(((uint)MetadataTokens.GetRowNumber(handle) << 2) | tag);
    }

    private static TResult DecodeAfterBoundedPrewalk<TResult>(
        W8BoundedSignaturePredecoder predecoder,
        ReadOnlyMemory<byte> signature,
        TypeSpecificationHandle rootHandle,
        Func<W8SignatureTraversalFact, TResult> downstream)
    {
        var traversal = predecoder.DecodeType(signature, rootHandle);
        return downstream(traversal);
    }

    private static W8BoundedSignaturePredecoder CreateMetadataSignaturePredecoder(
        MetadataReader reader) => new(
        handle => ReadTypeSpecificationBytes(reader, handle),
        handle => ValidateTypeDefOrRefHandle(reader, handle),
        MaximumMetadataSignatureDepth,
        MaximumMetadataSignatureNodes);

    private static ReadOnlyMemory<byte> ReadTypeSpecificationBytes(
        MetadataReader reader,
        TypeSpecificationHandle handle)
    {
        ValidateTypeDefOrRefHandle(reader, handle);
        return reader.GetBlobBytes(reader.GetTypeSpecification(handle).Signature);
    }

    private static void ValidateTypeDefOrRefHandle(MetadataReader reader, EntityHandle handle)
    {
        var row = MetadataTokens.GetRowNumber(handle);
        var maximumRow = handle.Kind switch
        {
            HandleKind.TypeDefinition => reader.GetTableRowCount(TableIndex.TypeDef),
            HandleKind.TypeReference => reader.GetTableRowCount(TableIndex.TypeRef),
            HandleKind.TypeSpecification => reader.GetTableRowCount(TableIndex.TypeSpec),
            _ => throw new BadImageFormatException($"A type signature used an invalid {handle.Kind} handle."),
        };
        if (row <= 0 || row > maximumRow)
        {
            throw new BadImageFormatException(
                $"A type signature used {handle.Kind} row {row} outside the table bound {maximumRow}.");
        }
    }

    private static byte[] RepeatedPrimitiveGenericSignature(int argumentCount)
    {
        Assert.InRange(argumentCount, 1, MaximumMetadataSignatureNodes);
        var bytes = new List<byte>
        {
            0x15,
            0x12,
            0x05,
        };
        WriteCompressedUnsigned(bytes, checked((uint)argumentCount));
        bytes.AddRange(Enumerable.Repeat((byte)0x08, argumentCount));
        return [.. bytes];
    }

    private static void WriteCompressedUnsigned(List<byte> destination, uint value)
    {
        if (value <= 0x7F)
        {
            destination.Add((byte)value);
            return;
        }

        if (value <= 0x3FFF)
        {
            destination.Add((byte)(0x80 | (value >> 8)));
            destination.Add((byte)value);
            return;
        }

        if (value <= 0x1FFFFFFF)
        {
            destination.Add((byte)(0xC0 | (value >> 24)));
            destination.Add((byte)(value >> 16));
            destination.Add((byte)(value >> 8));
            destination.Add((byte)value);
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    private sealed class W8BoundedSignaturePredecoder(
        Func<TypeSpecificationHandle, ReadOnlyMemory<byte>> resolveTypeSpecification,
        Action<EntityHandle> validateTypeDefOrRef,
        int maximumDepth,
        int maximumNodes)
    {
        private readonly HashSet<TypeSpecificationHandle> activeTypeSpecifications = [];
        private int observedMaximumDepth;
        private int nodeCount;

        internal W8SignatureTraversalFact DecodeType(
            ReadOnlyMemory<byte> signature,
            TypeSpecificationHandle rootHandle = default)
        {
            Assert.Equal(0, nodeCount);
            Assert.Equal(0, observedMaximumDepth);
            if (!rootHandle.IsNil && !activeTypeSpecifications.Add(rootHandle))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Raw signature traversal revisited TypeSpec token " +
                    $"0x{MetadataTokens.GetToken(rootHandle):x8}.");
            }

            try
            {
                var signatureReader = new W8RawSignatureReader(signature);
                ParseType(signatureReader, depth: 1, allowVoid: false);
                signatureReader.RequireEnd();
                return new W8SignatureTraversalFact(observedMaximumDepth, nodeCount);
            }
            finally
            {
                if (!rootHandle.IsNil)
                {
                    Assert.True(activeTypeSpecifications.Remove(rootHandle));
                }
            }
        }

        private void ParseType(W8RawSignatureReader signature, int depth, bool allowVoid)
        {
            var typeCode = signature.ReadByte();
            switch (typeCode)
            {
                case 0x01:
                    if (!allowVoid)
                    {
                        throw new BadImageFormatException("Void is not valid at this signature position.");
                    }

                    EnterNode(depth);
                    return;
                case >= 0x02 and <= 0x0E:
                case 0x16:
                case 0x18:
                case 0x19:
                case 0x1C:
                    EnterNode(depth);
                    return;
                case 0x0F:
                    EnterNode(depth);
                    ParseType(signature, checked(depth + 1), allowVoid: true);
                    return;
                case 0x10:
                case 0x1D:
                case 0x45:
                    EnterNode(depth);
                    ParseType(signature, checked(depth + 1), allowVoid: false);
                    return;
                case 0x11:
                case 0x12:
                    EnterNode(depth);
                    FollowTypeDefOrRef(signature.ReadTypeDefOrRef(), checked(depth + 1));
                    return;
                case 0x13:
                case 0x1E:
                    EnterNode(depth);
                    _ = signature.ReadCompressedUnsigned();
                    return;
                case 0x14:
                    EnterNode(depth);
                    ParseType(signature, checked(depth + 1), allowVoid: false);
                    ReadArrayShape(signature);
                    return;
                case 0x15:
                    ParseGenericInstantiation(signature, depth);
                    return;
                case 0x1B:
                    EnterNode(depth);
                    ParseMethodSignature(signature, checked(depth + 1));
                    return;
                case 0x1F:
                case 0x20:
                    ParseModifiedType(signature, depth);
                    return;
                default:
                    throw new BadImageFormatException($"Unsupported signature type code 0x{typeCode:X2}.");
            }
        }

        private void ParseGenericInstantiation(W8RawSignatureReader signature, int depth)
        {
            EnterNode(depth);
            var headKind = signature.ReadByte();
            if (headKind is not 0x11 and not 0x12)
            {
                throw new BadImageFormatException("A generic-instantiation head must be a class or value type.");
            }

            EnterNode(checked(depth + 1));
            FollowTypeDefOrRef(signature.ReadTypeDefOrRef(), checked(depth + 2));
            var argumentCount = ReadBoundedCount(signature, "generic argument");
            for (var index = 0; index < argumentCount; index++)
            {
                ParseType(signature, checked(depth + 1), allowVoid: false);
            }
        }

        private void ParseModifiedType(W8RawSignatureReader signature, int depth)
        {
            EnterNode(depth);
            EnterNode(checked(depth + 1));
            FollowTypeDefOrRef(signature.ReadTypeDefOrRef(), checked(depth + 2));
            ParseType(signature, checked(depth + 1), allowVoid: false);
        }

        private void ParseMethodSignature(W8RawSignatureReader signature, int childDepth)
        {
            var header = signature.ReadByte();
            if ((header & 0x10) != 0)
            {
                _ = signature.ReadCompressedUnsigned();
            }

            var parameterCount = ReadBoundedCount(signature, "method parameter");
            ParseType(signature, childDepth, allowVoid: true);
            var parsedParameters = 0;
            while (parsedParameters < parameterCount)
            {
                if (signature.TryReadByte(0x41))
                {
                    continue;
                }

                ParseType(signature, childDepth, allowVoid: false);
                parsedParameters++;
            }
        }

        private void ReadArrayShape(W8RawSignatureReader signature)
        {
            _ = signature.ReadCompressedUnsigned();
            var sizeCount = ReadBoundedCount(signature, "array size");
            for (var index = 0; index < sizeCount; index++)
            {
                _ = signature.ReadCompressedUnsigned();
            }

            var lowerBoundCount = ReadBoundedCount(signature, "array lower bound");
            for (var index = 0; index < lowerBoundCount; index++)
            {
                _ = signature.ReadCompressedUnsigned();
            }
        }

        private int ReadBoundedCount(W8RawSignatureReader signature, string role)
        {
            var count = signature.ReadCompressedUnsigned();
            if (count > maximumNodes)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Raw signature {role} count exceeded {maximumNodes}.");
            }

            return checked((int)count);
        }

        private void FollowTypeDefOrRef(EntityHandle handle, int depth)
        {
            validateTypeDefOrRef(handle);
            if (handle.Kind != HandleKind.TypeSpecification)
            {
                return;
            }

            var typeSpecification = (TypeSpecificationHandle)handle;
            if (!activeTypeSpecifications.Add(typeSpecification))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Raw signature traversal revisited TypeSpec token " +
                    $"0x{MetadataTokens.GetToken(typeSpecification):x8}.");
            }

            try
            {
                var nestedReader = new W8RawSignatureReader(resolveTypeSpecification(typeSpecification));
                ParseType(nestedReader, depth, allowVoid: false);
                nestedReader.RequireEnd();
            }
            finally
            {
                Assert.True(activeTypeSpecifications.Remove(typeSpecification));
            }
        }

        private void EnterNode(int depth)
        {
            if (depth > maximumDepth)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Raw signature traversal exceeded depth {maximumDepth}.");
            }

            if (nodeCount >= maximumNodes)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Raw signature traversal exceeded {maximumNodes} nodes.");
            }

            nodeCount++;
            observedMaximumDepth = Math.Max(observedMaximumDepth, depth);
        }
    }

    private sealed class W8RawSignatureReader(ReadOnlyMemory<byte> bytes)
    {
        private int offset;

        internal byte ReadByte()
        {
            if ((uint)offset >= (uint)bytes.Length)
            {
                throw new BadImageFormatException(
                    "A signature ended before its declared structure was complete.");
            }

            return bytes.Span[offset++];
        }

        internal bool TryReadByte(byte value)
        {
            if ((uint)offset >= (uint)bytes.Length || bytes.Span[offset] != value)
            {
                return false;
            }

            offset++;
            return true;
        }

        internal uint ReadCompressedUnsigned()
        {
            var first = ReadByte();
            if ((first & 0x80) == 0)
            {
                return first;
            }

            if ((first & 0xC0) == 0x80)
            {
                return checked((uint)(((first & 0x3F) << 8) | ReadByte()));
            }

            if ((first & 0xE0) == 0xC0)
            {
                return checked(
                    ((uint)(first & 0x1F) << 24) |
                    ((uint)ReadByte() << 16) |
                    ((uint)ReadByte() << 8) |
                    ReadByte());
            }

            throw new BadImageFormatException("A signature contains an invalid compressed integer.");
        }

        internal EntityHandle ReadTypeDefOrRef()
        {
            var encoded = ReadCompressedUnsigned();
            var row = checked((int)(encoded >> 2));
            if (row == 0)
            {
                throw new BadImageFormatException("A TypeDefOrRef signature token has row zero.");
            }

            return (encoded & 0x03) switch
            {
                0 => MetadataTokens.TypeDefinitionHandle(row),
                1 => MetadataTokens.TypeReferenceHandle(row),
                2 => MetadataTokens.TypeSpecificationHandle(row),
                _ => throw new BadImageFormatException(
                    "A TypeDefOrRef signature token has an invalid tag."),
            };
        }

        internal void RequireEnd()
        {
            if (offset != bytes.Length)
            {
                throw new BadImageFormatException(
                    $"A signature retained {bytes.Length - offset} trailing bytes.");
            }
        }
    }

    private sealed class W8SignatureFormatter(MetadataReader reader)
        : ISignatureTypeProvider<string, object?>
    {
        private const int MaximumSignatureDepth = 32;
        private readonly HashSet<TypeSpecificationHandle> active = [];
        private int depth;

        internal string Format(EntityHandle handle) => handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
            HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
            HandleKind.TypeSpecification => Decode((TypeSpecificationHandle)handle, genericContext: null),
            _ => throw new Xunit.Sdk.XunitException($"Expected a metadata type handle, observed {handle.Kind}."),
        };

        public string GetArrayType(string elementType, ArrayShape shape) =>
            $"{elementType}[rank={shape.Rank};sizes={string.Join(',', shape.Sizes)};" +
            $"lower={string.Join(',', shape.LowerBounds)}]";

        public string GetByReferenceType(string elementType) => $"{elementType}&";

        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            $"methodptr({string.Join(',', signature.ParameterTypes)})->{signature.ReturnType}";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(',', typeArguments)}>";

        public string GetGenericMethodParameter(object? genericContext, int index) => $"mvar:{index}";

        public string GetGenericTypeParameter(object? genericContext, int index) => $"var:{index}";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
            $"{(isRequired ? "modreq" : "modopt")}({modifier}) {unmodifiedType}";

        public string GetPinnedType(string elementType) => $"pinned {elementType}";

        public string GetPointerType(string elementType) => $"{elementType}*";

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        public string GetTypeFromDefinition(
            MetadataReader metadataReader,
            TypeDefinitionHandle handle,
            byte rawTypeKind) => $"td:{MetadataTokens.GetToken(handle):x8}";

        public string GetTypeFromReference(
            MetadataReader metadataReader,
            TypeReferenceHandle handle,
            byte rawTypeKind) => $"tr:{MetadataTokens.GetToken(handle):x8}";

        public string GetTypeFromSpecification(
            MetadataReader metadataReader,
            object? genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) => Decode(handle, genericContext);

        private string Decode(TypeSpecificationHandle handle, object? genericContext)
        {
            if (depth >= MaximumSignatureDepth || !active.Add(handle))
            {
                throw new Xunit.Sdk.XunitException(
                    $"TypeSpec traversal exceeded its depth or cycle bound at " +
                    $"0x{MetadataTokens.GetToken(handle):x8}.");
            }

            depth++;
            try
            {
                var rawSignature = reader.GetBlobBytes(reader.GetTypeSpecification(handle).Signature);
                return DecodeAfterBoundedPrewalk(
                    CreateMetadataSignaturePredecoder(reader),
                    rawSignature,
                    handle,
                    traversal =>
                    {
                        Assert.InRange(traversal.MaximumDepth, 1, MaximumMetadataSignatureDepth);
                        Assert.InRange(traversal.NodeCount, 1, MaximumMetadataSignatureNodes);
                        return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
                    });
            }
            finally
            {
                depth--;
                Assert.True(active.Remove(handle));
            }
        }
    }

    private sealed class W8DumpView : IDisposable
    {
        private W8DumpView(
            DataTarget dataTarget,
            ClrRuntime runtime,
            ImmutableArray<ClrModule> modules,
            ClrModule targetModule,
            W8CdacRuntimeConstructionOracle? oracle)
        {
            DataTarget = dataTarget;
            Runtime = runtime;
            Modules = modules;
            TargetModule = targetModule;
            this.oracle = oracle;
        }

        internal DataTarget DataTarget { get; }

        internal ClrRuntime Runtime { get; }

        internal ImmutableArray<ClrModule> Modules { get; }

        internal ClrModule TargetModule { get; }

        internal W8CdacRuntimeConstructionOracle Oracle => oracle ??
            throw new InvalidOperationException("This dump view did not request the construction oracle.");

        private readonly W8CdacRuntimeConstructionOracle? oracle;

        internal static W8DumpView Open(string dumpPath, Guid expectedTargetMvid)
            => OpenCore(dumpPath, expectedTargetMvid, includeConstructionOracle: true);

        internal static W8DumpView OpenWithoutConstructionOracle(
            string dumpPath,
            Guid expectedTargetMvid)
            => OpenCore(dumpPath, expectedTargetMvid, includeConstructionOracle: false);

        private static W8DumpView OpenCore(
            string dumpPath,
            Guid expectedTargetMvid,
            bool includeConstructionOracle)
        {
            var options = new DataTargetOptions
            {
                CacheOptions = new CacheOptions
                {
                    MaxDumpCacheSize = 256L * 1_024 * 1_024,
                    CacheStackRoots = false,
                    CacheStackTraces = false,
                },
                FileLocator = ClrmdOfflineFileLocator.Instance,
            };
            DataTarget? dataTarget = null;
            ClrRuntime? runtime = null;
            try
            {
                dataTarget = DataTarget.LoadDump(dumpPath, options);
                var clrInfo = Assert.Single(dataTarget.ClrVersions);
                Assert.Equal(10, clrInfo.Version.Major);
                runtime = clrInfo.CreateRuntime();
                var modules = runtime.EnumerateModules()
                    .Take(MaximumRuntimeModules + 1)
                    .ToImmutableArray();
                Assert.True(
                    modules.Length <= MaximumRuntimeModules,
                    $"Runtime-module traversal exceeded {MaximumRuntimeModules}; " +
                    $"cap-plus-one observed {modules.Length}.");
                var target = Assert.Single(modules, static candidate => string.Equals(
                    Path.GetFileName(candidate.Name),
                    W8TestTargetPaths.AssemblyFileName,
                    StringComparison.OrdinalIgnoreCase));
                using var metadata = ReadModuleMetadata(dataTarget.DataReader, target);
                Assert.Equal(expectedTargetMvid, metadata.ModuleVersionId);
                var oracle = includeConstructionOracle
                    ? W8CdacRuntimeConstructionOracle.Open(dataTarget, clrInfo)
                    : null;
                return new W8DumpView(dataTarget, runtime, modules, target, oracle);
            }
            catch
            {
                runtime?.Dispose();
                dataTarget?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Runtime.Dispose();
            DataTarget.Dispose();
        }
    }

    private sealed class W8ModuleMetadata : IDisposable
    {
        private readonly MetadataReaderProvider provider;

        private W8ModuleMetadata(MetadataReaderProvider provider, MetadataReader reader)
        {
            this.provider = provider;
            Reader = reader;
            ModuleVersionId = reader.GetGuid(reader.GetModuleDefinition().Mvid);
        }

        internal MetadataReader Reader { get; }

        internal Guid ModuleVersionId { get; }

        internal ImmutableArray<byte> Bytes { get; private init; }

        internal static W8ModuleMetadata Open(byte[] bytes)
        {
            var provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.CreateRange(bytes));
            try
            {
                var reader = provider.GetMetadataReader();
                return new W8ModuleMetadata(provider, reader)
                {
                    Bytes = ImmutableArray.CreateRange(bytes),
                };
            }
            catch
            {
                provider.Dispose();
                throw;
            }
        }

        public void Dispose() => provider.Dispose();
    }

    private interface IW8LiteralCapabilities
    {
        int MetadataArtifactCalls { get; }

        int RuntimeConstructionCalls { get; }

        int StorageAcquisitionCalls { get; }

        int MemoryCalls { get; }

        byte[] ReadMetadataArtifact(string path);

        void RequestRuntimeConstruction();

        void RequestStorageAcquisition();

        void RequestValueMemory();
    }

    private sealed class W8CountingLiteralCapabilities : IW8LiteralCapabilities
    {
        public int MetadataArtifactCalls { get; private set; }

        public int RuntimeConstructionCalls { get; private set; }

        public int StorageAcquisitionCalls { get; private set; }

        public int MemoryCalls { get; private set; }

        public byte[] ReadMetadataArtifact(string path)
        {
            var length = new FileInfo(path).Length;
            Assert.InRange(length, 1L, MaximumMetadataBytes);
            MetadataArtifactCalls++;
            return File.ReadAllBytes(path);
        }

        public void RequestRuntimeConstruction()
        {
            RuntimeConstructionCalls++;
            throw new Xunit.Sdk.XunitException(
                "The metadata-literal path requested runtime construction.");
        }

        public void RequestStorageAcquisition()
        {
            StorageAcquisitionCalls++;
            throw new Xunit.Sdk.XunitException(
                "The metadata-literal path requested storage acquisition.");
        }

        public void RequestValueMemory()
        {
            MemoryCalls++;
            throw new Xunit.Sdk.XunitException(
                "The metadata-literal path requested value memory.");
        }
    }

    private interface IW8RvaCapabilities
    {
        int RuntimeConstructionCalls { get; }

        int StorageAcquisitionCalls { get; }

        int MemoryCalls { get; }

        void RequestRuntimeConstruction();

        void RequestStorageAcquisition();

        byte[] ReadValue(ulong address, int length);
    }

    private sealed class W8CountingRvaCapabilities(IMemoryReader memory) : IW8RvaCapabilities
    {
        public int RuntimeConstructionCalls { get; private set; }

        public int StorageAcquisitionCalls { get; private set; }

        public int MemoryCalls { get; private set; }

        public void RequestRuntimeConstruction()
        {
            RuntimeConstructionCalls++;
            throw new Xunit.Sdk.XunitException(
                "The module-RVA path requested runtime construction.");
        }

        public void RequestStorageAcquisition()
        {
            StorageAcquisitionCalls++;
            throw new Xunit.Sdk.XunitException(
                "The module-RVA path requested slot acquisition.");
        }

        public byte[] ReadValue(ulong address, int length)
        {
            MemoryCalls++;
            return ReadExact(memory, address, length);
        }
    }

    private enum W8StorageBranchDisposition
    {
        Exact,
        NonAdmitted,
    }

    private enum W8PhysicalEvidenceStatus
    {
        Exact,
        Conflict,
    }

    private interface IW8TypeShapeReader
    {
        W8CdacTypeShape ReadTypeShape(ulong typeHandle);
    }

    private sealed class W8CdacTypeShapeReader(W8CdacRuntimeConstructionOracle oracle)
        : IW8TypeShapeReader
    {
        public W8CdacTypeShape ReadTypeShape(ulong typeHandle) => oracle.ReadTypeShape(typeHandle);
    }

    private sealed class W8SyntheticTypeShapeReader(
        ImmutableDictionary<ulong, W8CdacTypeShape> shapes) : IW8TypeShapeReader
    {
        internal int ReadCount { get; private set; }

        public W8CdacTypeShape ReadTypeShape(ulong typeHandle)
        {
            ReadCount++;
            return shapes.TryGetValue(typeHandle, out var shape)
                ? shape
                : throw new Xunit.Sdk.XunitException(
                    $"Synthetic type shape 0x{typeHandle:x16} was not declared.");
        }
    }

    private sealed class W8TypeTopologyBudget(int maximumNodes)
    {
        internal int NodeCount { get; private set; }

        internal void Enter()
        {
            if (NodeCount >= maximumNodes)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Runtime type topology traversal exceeded {maximumNodes} nodes.");
            }

            NodeCount++;
        }
    }

    private sealed record W8PrimitiveExpectation(string Name, byte[] Bytes);

    private sealed record W8LiteralExpectation(ConstantTypeCode TypeCode, byte[] Bytes);

    private readonly record struct W8SignatureTraversalFact(int MaximumDepth, int NodeCount);

    private readonly record struct W8FormattedTypeShape(string CanonicalShape, int NodeCount);

    private sealed record W8SyntheticTypeSpecificationGraph(
        TypeSpecificationHandle Root,
        ImmutableDictionary<TypeSpecificationHandle, ReadOnlyMemory<byte>> Signatures);

    private sealed record W8ContextMarkerIdentity(
        int AttributeToken,
        int ConstructorToken,
        ImmutableArray<byte> ConstructorSignature,
        ImmutableArray<byte> AttributeValue,
        int TypeReferenceToken,
        string TypeNamespace,
        string TypeName,
        W8AssemblyReferenceIdentity DirectAssemblyReference);

    private sealed record W8AssemblyReferenceIdentity(
        int Token,
        string Name,
        Version Version,
        string Culture,
        ImmutableArray<byte> PublicKeyOrToken,
        AssemblyFlags Flags,
        ImmutableArray<byte> HashValue);

    private sealed record W8AssemblyDefinitionIdentity(
        string Name,
        Version Version,
        string Culture,
        ImmutableArray<byte> PublicKey,
        ImmutableArray<byte> PublicKeyToken,
        AssemblyFlags Flags,
        AssemblyHashAlgorithm HashAlgorithm);

    private readonly record struct W8ByteContentIdentity(
        int Length,
        string Sha256);

    private readonly record struct W8ArtifactContentIdentity(
        Guid ModuleVersionId,
        W8ByteContentIdentity Artifact,
        W8ByteContentIdentity Metadata);

    private sealed record W8LoadedAssemblyArtifact(
        ulong ModuleAddress,
        W8ArtifactContentIdentity Content,
        W8AssemblyDefinitionIdentity Definition,
        ImmutableArray<byte> MetadataBytes);

    private sealed record W8AssemblyForwarderStep(
        int Ordinal,
        W8LoadedAssemblyArtifact SourceArtifact,
        int ExportedTypeToken,
        string TypeNamespace,
        string TypeName,
        TypeAttributes Attributes,
        int ImplementationToken,
        W8AssemblyReferenceIdentity TargetReference,
        W8LoadedAssemblyArtifact TargetArtifact);

    private sealed record W8ContextForwarderProof(
        W8LoadedAssemblyArtifact FacadeArtifact,
        ImmutableArray<W8AssemblyForwarderStep> Steps,
        W8LoadedAssemblyArtifact TerminalArtifact,
        int TerminalTypeDefinitionToken);

    private sealed record W8ValueGeometryObservation(
        ulong Int32Address,
        ImmutableArray<string> CanonicalLines);

    private sealed record W8PrimitivePairObservation(
        Guid ModuleVersionId,
        ulong ModuleAddress,
        ulong Int32Address,
        ulong UInt32Address,
        ImmutableArray<byte> Int32Bytes,
        ImmutableArray<byte> UInt32Bytes);

    private sealed record W8ReferenceSlotObservation(
        ulong SlotAddress,
        ulong ObjectAddress,
        ulong ActualMethodTable,
        ulong DeclaredMethodTable,
        string DeclaredShape,
        string ActualShape);

    private readonly record struct W8RuntimeTypeIdentity(ulong ModuleAddress, int TypeToken);

    private sealed record W8ThreadRelativeObservation(
        W8StorageBranchDisposition Disposition,
        ImmutableArray<string> CanonicalLines);

    private sealed record W8ContextRelativeObservation(
        W8StorageBranchDisposition Disposition,
        string ReasonCode,
        ImmutableArray<string> CanonicalLines);

    private sealed record W8NamedRvaObservation(
        W8StorageBranchDisposition Disposition,
        int RuntimeConstructionCalls,
        int StorageAcquisitionCalls,
        int ValueMemoryCalls,
        ImmutableArray<string> CanonicalLines);

    private sealed record W8NamedRvaArtifactObservation(
        Guid ModuleVersionId,
        ImmutableArray<byte> MetadataBytes,
        int NamedSentinelToken,
        int NamedWideSentinelToken,
        int NamedSentinelRva,
        int NamedWideSentinelRva,
        byte[] NamedSentinelBytes,
        byte[] NamedWideSentinelBytes);
}
