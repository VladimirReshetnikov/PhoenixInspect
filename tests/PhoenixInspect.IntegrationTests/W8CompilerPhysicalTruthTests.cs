using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Establishes an independent compiler, metadata, and Portable-PDB oracle for the optimized W8 fixture.
/// </summary>
/// <remarks>
/// The tests decode PE and Portable-PDB records through SRM primitives and deliberately do not call a product
/// projection. The asserted shapes are physical evidence, not commitments for the eventual public API.
/// </remarks>
public sealed class W8CompilerPhysicalTruthTests
{
    private const string TargetNamespace = "PhoenixInspect.W8TestTarget";
    private const string AliasNamespace = "PhoenixInspect.W8AliasTarget";
    private const string AliasAssemblyName = "PhoenixInspect.W8AliasTarget";
    private const int MaximumImportScopeDepth = 32;
    private const int MaximumImportCount = 128;
    private const int MaximumMetadataTraversalDepth = 32;
    private const int MaximumMetadataTraversalNodes = 128;
    private const int MaximumMutationArtifactBytes = 64 * 1024 * 1024;
    private static readonly ImmutableDictionary<ushort, OpCode> IlOpCodes = CreateIlOpCodes();

    /// <summary>
    /// Proves every accepted argument spelling can observe the target's readiness marker through the hidden-window
    /// process harness. This smoke test does not identify the paused method or associate a runtime frame with the
    /// argument; the parallel W8 frame-physical suite owns that evidence.
    /// </summary>
    /// <param name="profile">The exact W8 frame profile passed to the optimized target.</param>
    [Theory]
    [InlineData("generic-frame")]
    [InlineData("method-generic-frame")]
    [InlineData("shadow-frame")]
    [InlineData("lexical-frame")]
    [InlineData("optimized-frame")]
    [InlineData("thread-relative")]
    [InlineData("context-relative")]
    [InlineData("query-frame")]
    [InlineData("ambiguity-frame")]
    [InlineData("rva-frame")]
    [InlineData("slot-reuse-frame")]
    [Trait("Category", "Fast")]
    public void Each_argument_spelling_can_reach_readiness_without_proving_frame_association(string profile)
    {
        var executable = RequireArtifact(W8TestTargetPaths.ResolveExecutable());
        using var target = TestTargetRunner.StartAndWaitReady(
            executable,
            ["--truth-gate", profile],
            isolatedDirectory: null);

        Assert.True(target.Pid > 0);
    }

    /// <summary>
    /// Proves the PE CodeView record and Portable-PDB debug header carry the same complete content identity, while
    /// retaining the exact deterministic path, age, checksum record, and target-module MVID as independent facts.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CodeView_and_portable_pdb_headers_have_one_exact_physical_identity()
    {
        using var target = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        var pdbPath = RequireArtifact(W8TestTargetPaths.ResolvePortablePdb());
        using var pdb = PdbArtifact.Open(pdbPath);
        var entries = target.PeReader.ReadDebugDirectory();
        var codeViewEntry = Assert.Single(entries, static entry => entry.Type == DebugDirectoryEntryType.CodeView);
        var codeView = target.PeReader.ReadCodeViewDebugDirectoryData(codeViewEntry);
        var header = pdb.Reader.DebugMetadataHeader;
        Assert.NotNull(header);
        var pdbId = header.Id.ToArray();

        Assert.Equal(20, pdbId.Length);
        Assert.Equal(codeView.Guid, new Guid(pdbId.AsSpan(0, 16)));
        Assert.Equal(codeViewEntry.Stamp, BinaryPrimitives.ReadUInt32LittleEndian(pdbId.AsSpan(16, 4)));
        Assert.Equal(1, codeView.Age);
        Assert.Equal(W8TestTargetPaths.PortablePdbFileName, Path.GetFileName(codeView.Path));
        Assert.Equal(
            "/_/tests/PhoenixInspect.W8TestTarget/obj/Release/net10.0/PhoenixInspect.W8TestTarget.pdb",
            codeView.Path.Replace('\\', '/'));

        var checksumEntry = Assert.Single(entries, static entry => entry.Type == DebugDirectoryEntryType.PdbChecksum);
        var checksum = target.PeReader.ReadPdbChecksumDebugDirectoryData(checksumEntry);
        Assert.Equal("SHA256", checksum.AlgorithmName);
        Assert.Equal(32, checksum.Checksum.Length);
        var finalPdbBytes = File.ReadAllBytes(pdbPath);
        var contentIdOffset = Assert.Single(FindSequenceOffsets(finalPdbBytes, pdbId));
        Assert.Equal(124, contentIdOffset);
        var preContentIdBytes = (byte[])finalPdbBytes.Clone();
        preContentIdBytes.AsSpan(contentIdOffset, pdbId.Length).Clear();
        Assert.Equal(checksum.Checksum.AsSpan(), ComputeSha256(preContentIdBytes));
        Assert.False(checksum.Checksum.AsSpan().SequenceEqual(ComputeSha256(finalPdbBytes)));
        Assert.Contains(entries, static entry => entry.Type == DebugDirectoryEntryType.Reproducible);

        var module = target.Reader.GetModuleDefinition();
        var mvid = target.Reader.GetGuid(module.Mvid);
        Assert.NotEqual(Guid.Empty, mvid);
        Assert.True(PdbIdentityMatches(codeViewEntry, codeView, pdbId));

        var unrelatedId = (byte[])pdbId.Clone();
        unrelatedId[7] ^= 0x40;
        Assert.False(PdbIdentityMatches(codeViewEntry, codeView, unrelatedId));
        Assert.Equal(mvid, target.Reader.GetGuid(target.Reader.GetModuleDefinition().Mvid));
        Assert.Equal(EvidenceDigest(pdbId), EvidenceDigest(header.Id.AsSpan()));
    }

    /// <summary>
    /// Proves every recursive metadata route accepts its exact configured depth, rejects cap-plus-one input, and
    /// rejects revisiting a previously observed node. Raw inline array/generic signatures are predecoded with
    /// cumulative depth and node accounting before SRM can recursively materialize provider results.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Metadata_recursion_guards_reject_cap_plus_one_depth_and_revisited_nodes()
    {
        foreach (var kind in new[]
        {
            MetadataTraversalKind.DeclaringTypeDefinition,
            MetadataTraversalKind.TypeReferenceResolutionScope,
            MetadataTraversalKind.DisplayFormatter,
        })
        {
            var exact = ReadBoundedChain(
                0,
                value => value + 1 < MaximumMetadataTraversalDepth
                    ? (true, value + 1)
                    : (false, default),
                kind);
            Assert.Equal(MaximumMetadataTraversalDepth, exact.Length);
            Assert.Throws<Xunit.Sdk.XunitException>(() => ReadBoundedChain(
                0,
                value => value < MaximumMetadataTraversalDepth
                    ? (true, value + 1)
                    : (false, default),
                kind));
            Assert.Throws<Xunit.Sdk.XunitException>(() => ReadBoundedChain(
                0,
                static _ => (true, 0),
                kind));
        }

        var exactDepthBytes = Enumerable
            .Repeat((byte)SignatureTypeCode.SZArray, MaximumMetadataTraversalDepth - 1)
            .Append((byte)PrimitiveTypeCode.Int32)
            .ToArray();
        var exactDepth = new BoundedSignaturePredecoder(static _ => throw new BadImageFormatException())
            .DecodeType(exactDepthBytes);
        Assert.Equal(MaximumMetadataTraversalDepth, exactDepth.MaximumDepth);
        Assert.Equal(MaximumMetadataTraversalDepth, exactDepth.NodeCount);
        var excessiveDepthBytes = exactDepthBytes.Prepend((byte)SignatureTypeCode.SZArray).ToArray();
        Assert.Throws<Xunit.Sdk.XunitException>(() =>
            new BoundedSignaturePredecoder(static _ => throw new BadImageFormatException())
                .DecodeType(excessiveDepthBytes));

        var exactNodeBytes = RepeatedPrimitiveGenericSignature(MaximumMetadataTraversalNodes - 2);
        var exactNodes = new BoundedSignaturePredecoder(static _ => throw new BadImageFormatException())
            .DecodeType(exactNodeBytes);
        Assert.Equal(MaximumMetadataTraversalNodes, exactNodes.NodeCount);
        Assert.Equal(2, exactNodes.MaximumDepth);
        var excessiveNodeBytes = RepeatedPrimitiveGenericSignature(MaximumMetadataTraversalNodes - 1);
        Assert.Throws<Xunit.Sdk.XunitException>(() =>
            new BoundedSignaturePredecoder(static _ => throw new BadImageFormatException())
                .DecodeType(excessiveNodeBytes));

        var selfHandle = MetadataTokens.TypeSpecificationHandle(1);
        var selfReferenceBytes = new List<byte> { (byte)SignatureTypeKind.Class };
        WriteCompressedUnsigned(selfReferenceBytes, EncodeTypeDefOrRef(selfHandle));
        Assert.Throws<Xunit.Sdk.XunitException>(() =>
            new BoundedSignaturePredecoder(_ => selfReferenceBytes.ToArray())
                .DecodeType(selfReferenceBytes.ToArray(), selfHandle));
    }

    /// <summary>
    /// Proves the generic class, value type, interface, and nested owner definitions retain their exact emitted
    /// arities, generic-parameter rows, fields, signatures, literal, and thread-relative marker.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Generic_definitions_fields_and_nested_arities_are_physically_distinct()
    {
        using var artifact = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        var reader = artifact.Reader;
        var signatures = new MetadataSignatureFormatter(reader);

        var genericClassHandle = FindTopLevelType(reader, TargetNamespace, "GenericSlot`1");
        var genericClass = reader.GetTypeDefinition(genericClassHandle);
        AssertGenericParameters(reader, genericClass, (0, "T"));
        Assert.True((genericClass.Attributes & (TypeAttributes.Abstract | TypeAttributes.Sealed)) ==
            (TypeAttributes.Abstract | TypeAttributes.Sealed));
        Assert.False((genericClass.Attributes & TypeAttributes.Interface) != 0);
        Assert.Equal("System.Object", signatures.Format(genericClass.BaseType));

        var classFields = ReadFields(reader, genericClass);
        Assert.Equal(new[] { "Current", "Literal", "Sentinel", "ThreadSentinel" }, classFields.Keys.Order());
        AssertFieldSignature(reader, signatures, classFields["Sentinel"], "int32");
        AssertFieldSignature(reader, signatures, classFields["Current"], "!0");
        AssertFieldSignature(reader, signatures, classFields["ThreadSentinel"], "int32");
        AssertFieldSignature(reader, signatures, classFields["Literal"], "int32");
        AssertStoredStatic(reader.GetFieldDefinition(classFields["Sentinel"]));
        AssertStoredStatic(reader.GetFieldDefinition(classFields["Current"]));
        AssertStoredStatic(reader.GetFieldDefinition(classFields["ThreadSentinel"]));
        Assert.True(HasNamedAttribute(
            reader,
            reader.GetFieldDefinition(classFields["ThreadSentinel"]).GetCustomAttributes(),
            "System",
            "ThreadStaticAttribute"));
        Assert.DoesNotContain(
            reader.GetFieldDefinition(classFields["Sentinel"]).GetCustomAttributes(),
            handle => IsNamedAttribute(reader, handle, "System", "ThreadStaticAttribute"));
        AssertMetadataLiteral(
            reader,
            classFields["Literal"],
            ConstantTypeCode.Int32,
            LittleEndian(unchecked((int)0x018B17A5)));

        var valueTypeHandle = FindTopLevelType(reader, TargetNamespace, "GenericStructSlot`1");
        var valueType = reader.GetTypeDefinition(valueTypeHandle);
        AssertGenericParameters(reader, valueType, (0, "T"));
        Assert.True((valueType.Attributes & TypeAttributes.Sealed) != 0);
        Assert.False((valueType.Attributes & TypeAttributes.Interface) != 0);
        Assert.Equal("System.ValueType", signatures.Format(valueType.BaseType));
        var valueFields = ReadFields(reader, valueType);
        Assert.Equal(new[] { "Current", "Sentinel" }, valueFields.Keys.Order());
        AssertFieldSignature(reader, signatures, valueFields["Sentinel"], "int32");
        AssertFieldSignature(reader, signatures, valueFields["Current"], "!0");

        var interfaceHandle = FindTopLevelType(reader, TargetNamespace, "IGenericInterfaceSlot`1");
        var interfaceType = reader.GetTypeDefinition(interfaceHandle);
        AssertGenericParameters(reader, interfaceType, (0, "T"));
        Assert.True((interfaceType.Attributes & TypeAttributes.Interface) != 0);
        Assert.True((interfaceType.Attributes & TypeAttributes.Abstract) != 0);
        Assert.True(interfaceType.BaseType.IsNil);
        var interfaceFields = ReadFields(reader, interfaceType);
        Assert.Equal(new[] { "Current", "Literal", "Sentinel" }, interfaceFields.Keys.Order());
        AssertFieldSignature(reader, signatures, interfaceFields["Sentinel"], "int32");
        AssertFieldSignature(reader, signatures, interfaceFields["Current"], "!0");
        AssertFieldSignature(reader, signatures, interfaceFields["Literal"], "int32");
        AssertStoredStatic(reader.GetFieldDefinition(interfaceFields["Sentinel"]));
        AssertStoredStatic(reader.GetFieldDefinition(interfaceFields["Current"]));
        AssertMetadataLiteral(
            reader,
            interfaceFields["Literal"],
            ConstantTypeCode.Int32,
            LittleEndian(unchecked((int)0x018B17A7)));

        var outerHandle = FindTopLevelType(reader, TargetNamespace, "Outer`1");
        var outer = reader.GetTypeDefinition(outerHandle);
        AssertGenericParameters(reader, outer, (0, "TOuter"));
        var middleHandle = FindNestedType(reader, outerHandle, "Middle");
        var middle = reader.GetTypeDefinition(middleHandle);
        Assert.Equal(outerHandle, middle.GetDeclaringType());
        AssertGenericParameters(reader, middle, (0, "TOuter"));
        var innerHandle = FindNestedType(reader, middleHandle, "Inner`1");
        var inner = reader.GetTypeDefinition(innerHandle);
        Assert.Equal(middleHandle, inner.GetDeclaringType());
        AssertGenericParameters(reader, inner, (0, "TOuter"), (1, "TInner"));
        var innerFields = ReadFields(reader, inner);
        Assert.Equal(new[] { "Count", "InnerValue", "OuterValue" }, innerFields.Keys.Order());
        AssertFieldSignature(reader, signatures, innerFields["OuterValue"], "!0");
        AssertFieldSignature(reader, signatures, innerFields["InnerValue"], "!1");
        AssertFieldSignature(reader, signatures, innerFields["Count"], "int32");
    }

    /// <summary>
    /// Proves both recursive constructed-base TypeSpecs and every selected compiler constraint representation,
    /// including the dependent generic-parameter reference and unmanaged marker.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Recursive_base_specs_and_constraint_rows_have_exact_emitted_shapes()
    {
        using var artifact = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        var reader = artifact.Reader;
        var signatures = new MetadataSignatureFormatter(reader);

        var mid = reader.GetTypeDefinition(FindTopLevelType(reader, TargetNamespace, "MidSlot`1"));
        var derived = reader.GetTypeDefinition(FindTopLevelType(reader, TargetNamespace, "DerivedSlot`1"));
        Assert.Equal(HandleKind.TypeSpecification, mid.BaseType.Kind);
        Assert.Equal(HandleKind.TypeSpecification, derived.BaseType.Kind);
        Assert.Equal(
            $"{TargetNamespace}.BaseSlot`1<!0[]>",
            signatures.Format(mid.BaseType));
        Assert.Equal(
            $"{TargetNamespace}.MidSlot`1<System.Collections.Generic.List`1<!0>>",
            signatures.Format(derived.BaseType));
        AssertTypeSpecStartsWithGenericInstantiation(reader, (TypeSpecificationHandle)mid.BaseType);
        AssertTypeSpecStartsWithGenericInstantiation(reader, (TypeSpecificationHandle)derived.BaseType);

        var expectations = new[]
        {
            new ConstraintExpectation(
                "ConstructibleSlot`1", 0, "T",
                GenericParameterAttributes.ReferenceTypeConstraint |
                    GenericParameterAttributes.DefaultConstructorConstraint,
                [],
                ["System.Runtime.CompilerServices.NullableAttribute:01-00-01-00-00"]),
            new ConstraintExpectation(
                "ReferenceConstructorConstraintSlot`1", 0, "T",
                GenericParameterAttributes.ReferenceTypeConstraint |
                    GenericParameterAttributes.DefaultConstructorConstraint,
                [],
                ["System.Runtime.CompilerServices.NullableAttribute:01-00-01-00-00"]),
            new ConstraintExpectation(
                "NullableReferenceConstraintSlot`1", 0, "T",
                GenericParameterAttributes.ReferenceTypeConstraint,
                [],
                ["System.Runtime.CompilerServices.NullableAttribute:01-00-02-00-00"]),
            new ConstraintExpectation(
                "NonNullConstraintSlot`1", 0, "T",
                GenericParameterAttributes.None,
                [],
                ["System.Runtime.CompilerServices.NullableAttribute:01-00-01-00-00"]),
            new ConstraintExpectation(
                "ValueConstraintSlot`1", 0, "T",
                GenericParameterAttributes.NotNullableValueTypeConstraint |
                    GenericParameterAttributes.DefaultConstructorConstraint,
                ["System.ValueType"],
                []),
            new ConstraintExpectation(
                "EnumConstraintSlot`1", 0, "T",
                GenericParameterAttributes.NotNullableValueTypeConstraint |
                    GenericParameterAttributes.DefaultConstructorConstraint,
                ["System.Enum", "System.ValueType"],
                []),
            new ConstraintExpectation(
                "DelegateConstraintSlot`1", 0, "T",
                GenericParameterAttributes.None,
                ["System.Delegate"],
                []),
            new ConstraintExpectation(
                "CompositeConstraintSlot`1", 0, "T",
                GenericParameterAttributes.DefaultConstructorConstraint,
                ["System.IO.Stream", "System.IDisposable"],
                []),
            new ConstraintExpectation(
                "RefLikeConstraintSlot`1", 0, "T",
                (GenericParameterAttributes)0x20,
                [],
                ["System.Runtime.CompilerServices.NullableAttribute:01-00-02-00-00"]),
            new ConstraintExpectation(
                "UnmanagedSlot`1", 0, "T",
                GenericParameterAttributes.NotNullableValueTypeConstraint |
                    GenericParameterAttributes.DefaultConstructorConstraint,
                ["modreq(System.Runtime.InteropServices.UnmanagedType) System.ValueType"],
                ["System.Runtime.CompilerServices.IsUnmanagedAttribute:01-00-00-00"]),
            new ConstraintExpectation(
                "DependentSlot`2", 0, "TBase",
                GenericParameterAttributes.None,
                [],
                ["System.Runtime.CompilerServices.NullableAttribute:01-00-02-00-00"]),
            new ConstraintExpectation(
                "DependentSlot`2", 1, "TDerived",
                GenericParameterAttributes.None,
                ["!0"],
                []),
        };

        foreach (var ownerGroup in expectations.GroupBy(static expectation => expectation.Owner, StringComparer.Ordinal))
        {
            var owner = reader.GetTypeDefinition(FindTopLevelType(reader, TargetNamespace, ownerGroup.Key));
            var parameters = owner.GetGenericParameters().Select(reader.GetGenericParameter).ToArray();
            Assert.Equal(ownerGroup.Count(), parameters.Length);
            foreach (var expectation in ownerGroup)
            {
                var parameter = Assert.Single(parameters, row => row.Index == expectation.Index);
                Assert.Equal(expectation.Name, reader.GetString(parameter.Name));
                Assert.Equal(expectation.Attributes, parameter.Attributes);
                Assert.Equal(
                    expectation.ConstraintTypes,
                    parameter.GetConstraints()
                        .Select(reader.GetGenericParameterConstraint)
                        .Select(constraint => signatures.Format(constraint.Type))
                        .ToArray());
                Assert.Equal(
                    expectation.CustomAttributes,
                    ReadNamedAttributeFacts(reader, parameter.GetCustomAttributes()));
            }
        }
    }

    /// <summary>
    /// Proves the selected primitive, enum, nullable, array, reference, metadata-literal, decimal-attribute, and
    /// compiler-generated RVA encodings without loading the target assembly into the test process.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Primitive_literals_and_rva_payload_have_exact_metadata_encodings()
    {
        using var artifact = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        var reader = artifact.Reader;
        var signatures = new MetadataSignatureFormatter(reader);
        var storage = reader.GetTypeDefinition(FindTopLevelType(reader, TargetNamespace, "PrimitiveStorage"));
        var fields = ReadFields(reader, storage);
        var expectedSignatures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Boolean"] = "bool",
            ["Int8"] = "int8",
            ["UInt8"] = "uint8",
            ["Int16"] = "int16",
            ["UInt16"] = "uint16",
            ["Int32"] = "int32",
            ["UInt32"] = "uint32",
            ["Int64"] = "int64",
            ["UInt64"] = "uint64",
            ["NativeInt"] = "native int",
            ["NativeUInt"] = "native uint",
            ["Character"] = "char",
            ["Single"] = "float32",
            ["Double"] = "float64",
            ["Decimal"] = "System.Decimal",
            ["Enum"] = $"{TargetNamespace}.MarkerKind",
            ["SignedByteEnum"] = $"{TargetNamespace}.SignedByteMarker",
            ["UnsignedByteEnum"] = $"{TargetNamespace}.UnsignedByteMarker",
            ["UnsignedInt16Enum"] = $"{TargetNamespace}.UnsignedInt16Marker",
            ["SignedInt32Enum"] = $"{TargetNamespace}.SignedInt32Marker",
            ["UnsignedInt32Enum"] = $"{TargetNamespace}.UnsignedInt32Marker",
            ["SignedInt64Enum"] = $"{TargetNamespace}.SignedInt64Marker",
            ["UnsignedInt64Enum"] = $"{TargetNamespace}.UnsignedInt64Marker",
            ["Nullable"] = "System.Nullable`1<int32>",
            ["NullableNull"] = "System.Nullable`1<int32>",
            ["Text"] = "string",
            ["NullReference"] = $"{TargetNamespace}.RequestContext",
            ["Vector"] = $"{TargetNamespace}.RequestContext[]",
            ["Matrix"] = "int32[rank=2;sizes=;lower=0,0]",
        };

        foreach (var expected in expectedSignatures)
        {
            Assert.True(fields.TryGetValue(expected.Key, out var field), $"Missing FieldDef '{expected.Key}'.");
            AssertFieldSignature(reader, signatures, field, expected.Value);
            AssertStoredStatic(reader.GetFieldDefinition(field));
        }

        var literalExpectations = new[]
        {
            Literal("GenericSlot`1", "Literal", ConstantTypeCode.Int32, LittleEndian(0x018B17A5)),
            Literal("IGenericInterfaceSlot`1", "Literal", ConstantTypeCode.Int32, LittleEndian(0x018B17A7)),
            Literal("StaticImports`1", "ImportedLiteral", ConstantTypeCode.Int32, LittleEndian(0x018C17A5)),
            Literal("NonGenericImports", "NonGenericImportedLiteral", ConstantTypeCode.Int32, LittleEndian(0x1B017A01)),
            Literal("MarkerKind", "Request", ConstantTypeCode.Int16, LittleEndian((short)0x0171)),
            Literal("MarkerKind", "Workflow", ConstantTypeCode.Int16, LittleEndian((short)0x0272)),
            Literal("SignedByteMarker", "Retained", ConstantTypeCode.SByte, [unchecked((byte)-0x35)]),
            Literal("UnsignedByteMarker", "Retained", ConstantTypeCode.Byte, [0xD3]),
            Literal("UnsignedInt16Marker", "Retained", ConstantTypeCode.UInt16, LittleEndian((ushort)0xD3E5)),
            Literal("SignedInt32Marker", "Retained", ConstantTypeCode.Int32, LittleEndian(unchecked((int)0xD3E5A719))),
            Literal("UnsignedInt32Marker", "Retained", ConstantTypeCode.UInt32, LittleEndian(0xD3E5A719U)),
            Literal("SignedInt64Marker", "Retained", ConstantTypeCode.Int64, LittleEndian(unchecked((long)0xD3E5A7192B4C6D8EUL))),
            Literal("UnsignedInt64Marker", "Retained", ConstantTypeCode.UInt64, LittleEndian(0xD3E5A7192B4C6D8EUL)),
            Literal("PrimitiveStorage", "BooleanLiteral", ConstantTypeCode.Boolean, [1]),
            Literal("PrimitiveStorage", "Int8Literal", ConstantTypeCode.SByte, [unchecked((byte)-0x35)]),
            Literal("PrimitiveStorage", "UInt8Literal", ConstantTypeCode.Byte, [0xD3]),
            Literal("PrimitiveStorage", "Int16Literal", ConstantTypeCode.Int16, LittleEndian((short)-0x3527)),
            Literal("PrimitiveStorage", "UInt16Literal", ConstantTypeCode.UInt16, LittleEndian((ushort)0xD3E5)),
            Literal("PrimitiveStorage", "Int32Literal", ConstantTypeCode.Int32, LittleEndian(unchecked((int)0x81234567))),
            Literal("PrimitiveStorage", "UInt32Literal", ConstantTypeCode.UInt32, LittleEndian(0xD3E5A719U)),
            Literal("PrimitiveStorage", "Int64Literal", ConstantTypeCode.Int64, LittleEndian(unchecked((long)0xD3E5A7192B4C6D8EUL))),
            Literal("PrimitiveStorage", "UInt64Literal", ConstantTypeCode.UInt64, LittleEndian(0xE123456789ABCDEFUL)),
            Literal("PrimitiveStorage", "CharacterLiteral", ConstantTypeCode.Char, LittleEndian('\u03A9')),
            Literal("PrimitiveStorage", "SingleLiteral", ConstantTypeCode.Single, LittleEndian(-19.625F)),
            Literal("PrimitiveStorage", "DoubleLiteral", ConstantTypeCode.Double, LittleEndian(-17.125D)),
            Literal("PrimitiveStorage", "EnumLiteral", ConstantTypeCode.Int16, LittleEndian((short)0x0171)),
            Literal("PrimitiveStorage", "SignedByteEnumLiteral", ConstantTypeCode.SByte, [unchecked((byte)-0x35)]),
            Literal("PrimitiveStorage", "UnsignedByteEnumLiteral", ConstantTypeCode.Byte, [0xD3]),
            Literal("PrimitiveStorage", "UnsignedInt16EnumLiteral", ConstantTypeCode.UInt16, LittleEndian((ushort)0xD3E5)),
            Literal("PrimitiveStorage", "SignedInt32EnumLiteral", ConstantTypeCode.Int32, LittleEndian(unchecked((int)0xD3E5A719))),
            Literal("PrimitiveStorage", "UnsignedInt32EnumLiteral", ConstantTypeCode.UInt32, LittleEndian(0xD3E5A719U)),
            Literal("PrimitiveStorage", "SignedInt64EnumLiteral", ConstantTypeCode.Int64, LittleEndian(unchecked((long)0xD3E5A7192B4C6D8EUL))),
            Literal("PrimitiveStorage", "UnsignedInt64EnumLiteral", ConstantTypeCode.UInt64, LittleEndian(0xD3E5A7192B4C6D8EUL)),
            Literal("PrimitiveStorage", "StringLiteral", ConstantTypeCode.String, Encoding.Unicode.GetBytes("w8-literal")),
            Literal("PrimitiveStorage", "NullLiteral", ConstantTypeCode.NullReference, new byte[4]),
            Literal("PrimitiveStorage", "ObjectNullLiteral", ConstantTypeCode.NullReference, new byte[4]),
            new LiteralExpectation(
                "PhoenixInspect.Tests.Infrastructure.HeadlessTestProcess::SuppressedErrorModeMask",
                ConstantTypeCode.UInt32,
                LittleEndian(0x00008003U)),
            new LiteralExpectation(
                "PhoenixInspect.Tests.Infrastructure.HeadlessTestProcess::WerAlwaysShowUi",
                ConstantTypeCode.UInt32,
                LittleEndian(0x00000010U)),
            new LiteralExpectation(
                "PhoenixInspect.Tests.Infrastructure.HeadlessTestProcess::WerNoUi",
                ConstantTypeCode.UInt32,
                LittleEndian(0x00000020U)),
        };
        var literalFields = reader.FieldDefinitions
            .Where(handle => (reader.GetFieldDefinition(handle).Attributes & FieldAttributes.Literal) != 0)
            .ToDictionary(
                handle => LiteralKey(reader, handle),
                static handle => handle,
                StringComparer.Ordinal);
        Assert.Equal(
            literalExpectations.Select(static expectation => expectation.Key).Order(StringComparer.Ordinal),
            literalFields.Keys.Order(StringComparer.Ordinal));
        foreach (var expectation in literalExpectations)
        {
            AssertMetadataLiteral(reader, literalFields[expectation.Key], expectation.TypeCode, expectation.Value);
        }

        var decimalLiteral = reader.GetFieldDefinition(fields["DecimalLiteral"]);
        Assert.Equal(FieldAttributes.Static | FieldAttributes.InitOnly,
            decimalLiteral.Attributes & (FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Literal));
        Assert.True(decimalLiteral.GetDefaultValue().IsNil);
        AssertDecimalConstantAttribute(reader, decimalLiteral, 9876.5432M);

        using var alias = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly()));
        var aliasLiteral = Assert.Single(
            alias.Reader.FieldDefinitions,
            handle => (alias.Reader.GetFieldDefinition(handle).Attributes & FieldAttributes.Literal) != 0);
        Assert.Equal($"{AliasNamespace}.ExternalSlot`1::Literal", LiteralKey(alias.Reader, aliasLiteral));
        AssertMetadataLiteral(alias.Reader, aliasLiteral, ConstantTypeCode.Int32, LittleEndian(0x018A17A5));

        var expectedRvaPayload = Encoding.UTF8.GetBytes("w8-rva-materialization\0");
        var matchingRvaFields = reader.FieldDefinitions
            .Where(handle => (reader.GetFieldDefinition(handle).Attributes & FieldAttributes.HasFieldRVA) != 0)
            .Where(handle => ReadRvaPrefix(artifact.PeReader, reader.GetFieldDefinition(handle), expectedRvaPayload.Length)
                .SequenceEqual(expectedRvaPayload))
            .ToArray();
        var rvaFieldHandle = Assert.Single(matchingRvaFields);
        var rvaField = reader.GetFieldDefinition(rvaFieldHandle);
        Assert.True((rvaField.Attributes & (FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA)) ==
            (FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA));
        var rvaOwner = reader.GetTypeDefinition(FindDeclaringType(reader, rvaFieldHandle));
        Assert.Equal("<PrivateImplementationDetails>", reader.GetString(rvaOwner.Name));
        Assert.StartsWith(
            "<PrivateImplementationDetails>+__StaticArrayInitTypeSize=",
            signatures.FormatField(rvaFieldHandle),
            StringComparison.Ordinal);
        Assert.Contains($"__StaticArrayInitTypeSize={expectedRvaPayload.Length}", signatures.FormatField(rvaFieldHandle));
        var generatedGeometry = ReadRvaGeometry(artifact, rvaFieldHandle, expectedRvaPayload.Length);
        Assert.Equal(".text", generatedGeometry.SectionName);
        Assert.Equal(expectedRvaPayload, generatedGeometry.Bytes);
        Assert.Equal(expectedRvaPayload, generatedGeometry.FileBytes);
    }

    /// <summary>
    /// Proves the named IL fixture retains two non-overlapping FieldRVA rows, exact four/eight-byte signatures and
    /// payloads, section-relative geometry, and agreement between mapped RVA bytes and raw file bytes.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Named_rva_fixture_has_exact_rows_payloads_and_pe_geometry()
    {
        using var artifact = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly()));
        var reader = artifact.Reader;
        var signatures = new MetadataSignatureFormatter(reader);
        var owner = reader.GetTypeDefinition(
            FindTopLevelType(reader, "PhoenixInspect.W8NamedRvaTarget", "NamedRvaStorage"));
        var fields = ReadFields(reader, owner);
        Assert.Equal(new[] { "NamedSentinel", "NamedWideSentinel" }, fields.Keys.Order(StringComparer.Ordinal));

        var expectations = new[]
        {
            new NamedRvaExpectation("NamedSentinel", "int32", 4, LittleEndian(0x21047A61)),
            new NamedRvaExpectation(
                "NamedWideSentinel",
                "int64",
                8,
                LittleEndian(unchecked((long)0xD3E5A71942087A92UL))),
        };
        var geometries = new List<RvaGeometry>();
        foreach (var expectation in expectations)
        {
            var handle = fields[expectation.Name];
            var field = reader.GetFieldDefinition(handle);
            Assert.Equal(
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA,
                field.Attributes & (FieldAttributes.FieldAccessMask | FieldAttributes.Static |
                    FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA));
            AssertFieldSignature(reader, signatures, handle, expectation.Signature);
            var geometry = ReadRvaGeometry(artifact, handle, expectation.Width);
            Assert.Equal(".sdata", geometry.SectionName);
            Assert.Equal(expectation.Bytes, geometry.Bytes);
            Assert.Equal(expectation.Bytes, geometry.FileBytes);
            geometries.Add(geometry);
        }

        Assert.Equal(0x4000, geometries[0].Rva);
        Assert.Equal(0x4004, geometries[1].Rva);
        Assert.Equal(4, geometries[1].Rva - geometries[0].Rva);
        Assert.True(geometries[0].Rva + geometries[0].Width <= geometries[1].Rva);
        Assert.Equal(geometries[0].SectionRawPointer, geometries[0].FileOffset);
        Assert.Equal(geometries[0].FileOffset + 4, geometries[1].FileOffset);
    }

    /// <summary>
    /// Proves alias, forwarding, and destination assemblies retain their complete identities; the forwarding row
    /// names the exact destination AssemblyRef; and consumer TypeRefs converge on the physically defining assembly.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Alias_and_forwarder_rows_converge_on_complete_assembly_identities()
    {
        using var target = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        using var alias = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly()));
        using var forwarder = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveForwarderAssembly()));
        var reader = target.Reader;
        var aliasReferenceHandle = FindAssemblyReference(reader, AliasAssemblyName);
        var forwarderReferenceHandle = FindAssemblyReference(reader, "PhoenixInspect.W8ForwarderTarget");
        var aliasDefinitionIdentity = ReadAssemblyDefinitionIdentity(alias.Reader);
        var forwarderDefinitionIdentity = ReadAssemblyDefinitionIdentity(forwarder.Reader);
        Assert.Equal(aliasDefinitionIdentity, ReadAssemblyReferenceIdentity(reader, aliasReferenceHandle));
        Assert.Equal(forwarderDefinitionIdentity, ReadAssemblyReferenceIdentity(reader, forwarderReferenceHandle));
        Assert.Equal(new Version(1, 0, 0, 0), aliasDefinitionIdentity.Version);
        Assert.Equal(string.Empty, aliasDefinitionIdentity.Culture);
        Assert.Empty(aliasDefinitionIdentity.KeyOrToken);

        var externalTypeReferences = reader.TypeReferences
            .Where(handle => reader.GetTypeReference(handle).ResolutionScope == aliasReferenceHandle)
            .Select(handle =>
            {
                var type = reader.GetTypeReference(handle);
                return (Namespace: reader.GetString(type.Namespace), Name: reader.GetString(type.Name));
            })
            .ToArray();
        Assert.Contains((AliasNamespace, "ExternalRequestContext"), externalTypeReferences);
        Assert.Contains((AliasNamespace, "ExternalSlot`1"), externalTypeReferences);
        Assert.Contains((AliasNamespace, "IExternalInterfaceSlot`1"), externalTypeReferences);
        Assert.Contains((AliasNamespace, "ForwardedRequestContext"), externalTypeReferences);

        Assert.False(FindTopLevelType(alias.Reader, AliasNamespace, "ExternalRequestContext").IsNil);
        Assert.False(FindTopLevelType(alias.Reader, AliasNamespace, "ExternalSlot`1").IsNil);
        Assert.False(FindTopLevelType(alias.Reader, AliasNamespace, "ForwardedRequestContext").IsNil);

        var destinationReference = FindAssemblyReference(forwarder.Reader, AliasAssemblyName);
        Assert.Equal(aliasDefinitionIdentity, ReadAssemblyReferenceIdentity(forwarder.Reader, destinationReference));
        var exportedHandle = Assert.Single(forwarder.Reader.ExportedTypes);
        var exported = forwarder.Reader.GetExportedType(exportedHandle);
        Assert.Equal(AliasNamespace, forwarder.Reader.GetString(exported.Namespace));
        Assert.Equal("ForwardedRequestContext", forwarder.Reader.GetString(exported.Name));
        Assert.Equal(destinationReference, exported.Implementation);
        Assert.True((exported.Attributes & (TypeAttributes)0x00200000) != 0);
        Assert.DoesNotContain(
            forwarder.Reader.TypeDefinitions,
            handle => IsNamedTypeDefinition(
                forwarder.Reader,
                handle,
                AliasNamespace,
                "ForwardedRequestContext"));

        var forwardedConsumerReference = Assert.Single(
            reader.TypeReferences,
            handle => IsNamedTypeReference(reader, handle, AliasNamespace, "ForwardedRequestContext"));
        Assert.Equal(aliasReferenceHandle, reader.GetTypeReference(forwardedConsumerReference).ResolutionScope);
        var nonFriendConsumerReference = Assert.Single(
            reader.TypeReferences,
            handle => IsNamedTypeReference(
                reader,
                handle,
                "PhoenixInspect.W8ForwarderTarget",
                "NonFriendAccessibilityOwner"));
        Assert.Equal(forwarderReferenceHandle, reader.GetTypeReference(nonFriendConsumerReference).ResolutionScope);

        Assert.NotEqual(
            alias.Reader.GetGuid(alias.Reader.GetModuleDefinition().Mvid),
            forwarder.Reader.GetGuid(forwarder.Reader.GetModuleDefinition().Mvid));
        Assert.NotEqual(aliasDefinitionIdentity, forwarderDefinitionIdentity);
    }

    /// <summary>
    /// Compiles independent synthetic source with Roslyn and proves one same-level namespace import converges through
    /// direct and forwarded metadata to the destination symbol, while two distinct same-level imports produce a
    /// genuine ambiguity. This compiler-semantic oracle is evidence independent of the SRM row assertions.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Roslyn_semantics_distinguish_forwarder_convergence_from_same_level_ambiguity()
    {
        var targetReference = MetadataReference.CreateFromFile(
            RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        var aliasReference = MetadataReference.CreateFromFile(
            RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly()));
        var forwarderReference = MetadataReference.CreateFromFile(
            RequireArtifact(W8TestTargetPaths.ResolveForwarderAssembly()));
        var runtimeDirectory = Assert.IsType<string>(Path.GetDirectoryName(typeof(object).Assembly.Location));
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(RequireArtifact(Path.Combine(runtimeDirectory, "System.Runtime.dll"))),
            targetReference,
            aliasReference,
            forwarderReference,
            MetadataReference.CreateFromFile(RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly())),
        };
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp14, DocumentationMode.None, SourceCodeKind.Regular);
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            nullableContextOptions: NullableContextOptions.Enable,
            deterministic: true);

        const string convergenceSource = """
            using PhoenixInspect.W8AliasTarget;

            namespace Synthetic.Convergence;

            internal sealed class Probe
            {
                internal ForwardedRequestContext Value = null!;
            }
            """;
        var convergenceTree = CSharpSyntaxTree.ParseText(
            convergenceSource,
            parseOptions,
            path: "/synthetic/ForwarderConvergence.cs",
            encoding: Encoding.UTF8);
        var convergenceCompilation = CSharpCompilation.Create(
            "Synthetic.ForwarderConvergence",
            [convergenceTree],
            references,
            compilationOptions);
        Assert.Empty(convergenceCompilation.GetDiagnostics().Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error));
        var convergenceType = Assert.Single(
            convergenceTree.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>()).Type;
        var convergenceInfo = convergenceCompilation.GetSemanticModel(convergenceTree).GetSymbolInfo(convergenceType);
        var convergenceSymbol = Assert.IsAssignableFrom<INamedTypeSymbol>(convergenceInfo.Symbol);
        Assert.Equal("ForwardedRequestContext", convergenceSymbol.Name);
        Assert.Equal(AliasNamespace, convergenceSymbol.ContainingNamespace.ToDisplayString());
        Assert.Equal(AliasAssemblyName, convergenceSymbol.ContainingAssembly.Identity.Name);
        Assert.Empty(convergenceInfo.CandidateSymbols);
        Assert.Equal(CandidateReason.None, convergenceInfo.CandidateReason);

        var aliasAssembly = Assert.IsAssignableFrom<IAssemblySymbol>(
            convergenceCompilation.GetAssemblyOrModuleSymbol(aliasReference));
        var forwarderAssembly = Assert.IsAssignableFrom<IAssemblySymbol>(
            convergenceCompilation.GetAssemblyOrModuleSymbol(forwarderReference));
        var directSymbol = aliasAssembly.GetTypeByMetadataName($"{AliasNamespace}.ForwardedRequestContext");
        var forwardedSymbol = Assert.Single(
            forwarderAssembly.GetForwardedTypes(),
            type => string.Equals(
                type.ToDisplayString(),
                $"{AliasNamespace}.ForwardedRequestContext",
                StringComparison.Ordinal));
        Assert.True(SymbolEqualityComparer.Default.Equals(convergenceSymbol, directSymbol));
        Assert.True(SymbolEqualityComparer.Default.Equals(convergenceSymbol, forwardedSymbol));

        const string ambiguitySource = """
            using PhoenixInspect.W8AliasTarget;
            using PhoenixInspect.W8TestTarget;

            namespace Synthetic.Ambiguity;

            internal sealed class Probe
            {
                internal SharedSpelling Value = null!;
            }
            """;
        var ambiguityTree = CSharpSyntaxTree.ParseText(
            ambiguitySource,
            parseOptions,
            path: "/synthetic/SameLevelAmbiguity.cs",
            encoding: Encoding.UTF8);
        var ambiguityCompilation = CSharpCompilation.Create(
            "Synthetic.SameLevelAmbiguity",
            [ambiguityTree],
            references,
            compilationOptions);
        var ambiguityType = Assert.Single(
            ambiguityTree.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>()).Type;
        var ambiguityInfo = ambiguityCompilation.GetSemanticModel(ambiguityTree).GetSymbolInfo(ambiguityType);
        Assert.Null(ambiguityInfo.Symbol);
        Assert.Equal(CandidateReason.Ambiguous, ambiguityInfo.CandidateReason);
        Assert.Equal(
            new[] { AliasAssemblyName, "PhoenixInspect.W8TestTarget" },
            ambiguityInfo.CandidateSymbols
                .OfType<INamedTypeSymbol>()
                .Select(static symbol => symbol.ContainingAssembly.Identity.Name)
                .Order(StringComparer.Ordinal));
        var ambiguityDiagnostic = Assert.Single(
            ambiguityCompilation.GetDiagnostics(),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Equal("CS0104", ambiguityDiagnostic.Id);
        Assert.Equal(ambiguityType.Span, ambiguityDiagnostic.Location.SourceSpan);
    }

    /// <summary>
    /// Proves every CLI field-access flag is retained in local, friend-visible external, and ordinary external
    /// owners, while top-level and nested owner visibility remains a separate effective-access fact.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Accessibility_flags_and_owner_visibility_form_complete_physical_matrices()
    {
        using var target = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        using var alias = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly()));
        using var forwarder = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveForwarderAssembly()));

        AssertAccessibilityMatrix(target.Reader, TargetNamespace, "AccessibilityMatrix");
        AssertAccessibilityMatrix(alias.Reader, AliasNamespace, "ExternalAccessibilityBase");
        AssertAccessibilityMatrix(
            forwarder.Reader,
            "PhoenixInspect.W8ForwarderTarget",
            "NonFriendAccessibilityOwner");

        var aliasAssembly = alias.Reader.GetAssemblyDefinition();
        var friendValue = Assert.Single(
            aliasAssembly.GetCustomAttributes()
                .Where(handle => IsNamedAttribute(
                    alias.Reader,
                    handle,
                    "System.Runtime.CompilerServices",
                    "InternalsVisibleToAttribute"))
                .Select(handle => ReadSingleStringAttributeArgument(alias.Reader, handle)));
        Assert.Equal("PhoenixInspect.W8TestTarget", friendValue);
        Assert.DoesNotContain(
            forwarder.Reader.GetAssemblyDefinition().GetCustomAttributes(),
            handle => IsNamedAttribute(
                forwarder.Reader,
                handle,
                "System.Runtime.CompilerServices",
                "InternalsVisibleToAttribute"));

        var friendOwner = alias.Reader.GetTypeDefinition(
            FindTopLevelType(alias.Reader, AliasNamespace, "FriendVisibleOwner"));
        var assemblyOwner = alias.Reader.GetTypeDefinition(
            FindTopLevelType(alias.Reader, AliasNamespace, "AssemblyOnlyOwner"));
        Assert.Equal(TypeAttributes.NotPublic, friendOwner.Attributes & TypeAttributes.VisibilityMask);
        Assert.Equal(TypeAttributes.NotPublic, assemblyOwner.Attributes & TypeAttributes.VisibilityMask);
        Assert.Equal(
            FieldAttributes.Assembly,
            alias.Reader.GetFieldDefinition(Assert.Single(friendOwner.GetFields())).Attributes &
                FieldAttributes.FieldAccessMask);
        Assert.Equal(
            FieldAttributes.Assembly,
            alias.Reader.GetFieldDefinition(Assert.Single(assemblyOwner.GetFields())).Attributes &
                FieldAttributes.FieldAccessMask);

        var nestedOwnerHandle = FindTopLevelType(target.Reader, TargetNamespace, "NestedAccessibilityOwner");
        var privateNestedHandle = FindNestedType(target.Reader, nestedOwnerHandle, "PrivateNested");
        var privateNested = target.Reader.GetTypeDefinition(privateNestedHandle);
        Assert.Equal(TypeAttributes.NestedPrivate, privateNested.Attributes & TypeAttributes.VisibilityMask);
        var nestedField = target.Reader.GetFieldDefinition(Assert.Single(privateNested.GetFields()));
        Assert.Equal(FieldAttributes.Public, nestedField.Attributes & FieldAttributes.FieldAccessMask);
        Assert.Equal("Sentinel", target.Reader.GetString(nestedField.Name));
    }

    /// <summary>
    /// Independently decodes each declared pause method, proves the one exact <c>Thread.Sleep(int)</c> call and its IL
    /// offset, then accounts for active and inactive Portable-PDB rows, parameters, generic rows, constants, unnamed
    /// slots, and disjoint-scope slot relationships, including the current compiler's typed non-admission of named
    /// local-slot reuse. Mapping an argument spelling to a runtime frame belongs to the parallel W8 frame-physical
    /// suite and is not inferred here from a readiness line.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Each_declared_pause_method_has_exact_active_and_inactive_lexical_evidence()
    {
        using var target = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        using var pdb = PdbArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolvePortablePdb()));
        var request = $"{TargetNamespace}.RequestContext";
        var batch = $"{TargetNamespace}.BatchContext";
        var value = $"{TargetNamespace}.ValueContext";
        var external = $"{AliasNamespace}.ExternalRequestContext";
        var importedRequest = $"{TargetNamespace}.StaticImports`1+ImportedNested<{request}>";
        var importedBatch = $"{TargetNamespace}.StaticImports`1+ImportedNested<{batch}>";
        var frames = new[]
        {
            Frame(
                "generic-frame", TargetNamespace, "GenericFrameOwner`1", "Run", 74,
                ["TType"], ["TMethod"],
                [("profile", "string"), ("methodValue", "!!0"), ("request", request),
                    ("value", value), ("number", "int32")],
                [("localThis", $"{TargetNamespace}.GenericFrameOwner`1<!0>"), ("localOwnerValue", "!0"),
                    ("localMethodValue", "!!0"), ("localRequest", request), ("localValue", value),
                    ("localNumber", "int32"), ("declaringTypeWitness", "System.Type"),
                    ("methodTypeWitness", "System.Type")]),
            Frame(
                "method-generic-frame", TargetNamespace, "W8FrameProbe", "Run", 74,
                [], ["TType", "TMethod"],
                [("profile", "string"), ("request", request), ("number", "int32"),
                    ("importedNested", importedRequest), ("rvaLength", "int32")],
                [("localRequest", request), ("localNumber", "int32"), ("localVector", $"{request}[]"),
                    ("localExternal", external), ("localImported", "int32"), ("typeWitness", "System.Type"),
                    ("methodWitness", "System.Type")]),
            Frame(
                "shadow-frame", $"{TargetNamespace}.ScopeEvidence.InnerScope", "InnerScopeProbe", "Run", 37,
                [], [],
                [("profile", "string"), ("request", request), ("batch", batch),
                    ("importedNested", importedRequest), ("rvaLength", "int32")],
                [("shadowedAliasValue", batch), ("localRequest", request), ("localBatch", batch),
                    ("localNumber", "int32")]),
            Frame(
                "lexical-frame", $"{TargetNamespace}.LexicalEvidence", "LexicalCatalogProbe", "Run", 332,
                [], [],
                [("profile", "string"), ("request", request), ("number", "int32")],
                [("ordinaryLocal", request), ("typeReferenceLocal", external),
                    ("externalInterfaceAliasWitness", "System.Type"), ("vectorLocal", $"{request}[]"),
                    ("matrixLocal", $"{request}[rank=2;sizes=;lower=0,0]"),
                    ("nestedOwnerAliasWitness", "System.Type"),
                    ("usingLocal", "System.IO.MemoryStream"), ("deconstructionNumber", "int32"),
                    ("deconstructionText", "string"), ("byteVector", "uint8[]"),
                    ("catchLocal", "System.InvalidOperationException"), ("foreachLocal", request),
                    ("patternLocal", "string"), ("rangeLocal", "int32"), ("fixedLocal", "uint8*"),
                    ("localFunctionResult", "int32"), ("genericImportedLocal", "int32"),
                    ("nonGenericImportedLocal", "int32"), ("importedNestedLocal", importedBatch),
                    ("nonGenericNestedLocal", $"{TargetNamespace}.NonGenericImports+NonGenericNested")],
                unnamedSlots: [11, 12, 17, 23],
                constants: [("localConstant", "08-01-7A-02-1C")]),
            Frame(
                "optimized-frame", TargetNamespace, "OptimizedFrameProfile", "Run", 74,
                [], [],
                [("profile", "string"), ("request", request), ("number", "int32")],
                [("foldedNumber", "int32"), ("selectedReference", request), ("projectedNumber", "int32")]),
            Frame(
                "slot-reuse-frame", TargetNamespace, "SlotReuseProfile", "Run", 44,
                [], [], [("profile", "string"), ("number", "int32")],
                [("activeSlot", "int32")],
                inactiveLocals: [("inactiveSlot", "int32")],
                namedSlotNonReuse: new("inactiveSlot", 0, "activeSlot", 1)),
            Frame(
                "thread-relative", TargetNamespace, "ThreadRelativeProfile", "Run", 186,
                [], [], [("profile", "string")],
                [("ready", "System.Threading.CountdownEvent"),
                    ("firstState", $"{TargetNamespace}.ThreadRelativeProfile+ThreadRelativeState"),
                    ("secondState", $"{TargetNamespace}.ThreadRelativeProfile+ThreadRelativeState"),
                    ("first", "System.Threading.Thread"), ("second", "System.Threading.Thread")],
                unnamedSlots: [5]),
            Frame(
                "context-relative", TargetNamespace, "ContextRelativeProfile", "Run", 37,
                [], [], [("profile", "string")], [("retainedSentinel", "int32")]),
            Frame(
                "query-frame", TargetNamespace, "QueryRangeProfile", "PauseFromRange", 28,
                [], [], [("profile", "string"), ("queryRangeVariable", request)],
                [("retainedName", "string")]),
            Frame(
                "ambiguity-frame", "PhoenixInspect.W8AmbiguityEvidence", "CrossAssemblyAmbiguityProfile", "Run", 61,
                [], [], [("profile", "string")],
                [("localCandidate", $"{TargetNamespace}.SharedSpelling"),
                    ("externalCandidate", $"{AliasNamespace}.SharedSpelling"),
                    ("distinctCandidates", "bool")]),
            Frame(
                "rva-frame", TargetNamespace, "NamedRvaProfile", "Run", 69,
                [], [], [("profile", "string"), ("sentinel", "int32"), ("wideSentinel", "int64")],
                [("combined", "uint64")]),
        };

        Assert.Equal(11, frames.Length);
        foreach (var frame in frames)
        {
            AssertFrameEvidence(target, pdb.Reader, frame);
        }
    }

    /// <summary>
    /// Proves raw Portable-PDB import blobs retain constructed type aliases, constructed <c>using static</c>, the
    /// extern alias, nested import-scope ancestry, same-name alias shadowing, and named optimized locals.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Portable_pdb_imports_shadowing_and_locals_are_exact()
    {
        using var target = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        using var pdb = PdbArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolvePortablePdb()));
        var reader = target.Reader;
        var signatures = new MetadataSignatureFormatter(reader);

        var genericRun = FindMethod(reader, TargetNamespace, "W8FrameProbe", "Run");
        var genericScopes = ReadMethodScopes(pdb.Reader, genericRun);
        var genericImports = ReadImportChain(pdb.Reader, reader, SelectLeafImportScope(genericScopes));
        Assert.InRange(genericImports.Length, 5, MaximumImportCount);
        Assert.Contains(genericImports, fact =>
            fact.RawKind == 5 && string.Equals(fact.Alias, "requestlib", StringComparison.Ordinal));
        AssertImportType(
            genericImports,
            rawKind: 9,
            alias: "ExternalRequestSlot",
            $"{AliasNamespace}.ExternalSlot`1<{AliasNamespace}.ExternalRequestContext>",
            signatures);
        AssertImportType(
            genericImports,
            rawKind: 9,
            alias: "RequestSlot",
            $"{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext>",
            signatures);
        AssertImportType(
            genericImports,
            rawKind: 9,
            alias: "RequestVectorSlot",
            $"{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext[]>",
            signatures);
        AssertImportType(
            genericImports,
            rawKind: 3,
            alias: null,
            $"{TargetNamespace}.StaticImports`1<{TargetNamespace}.RequestContext>",
            signatures);

        AssertNamedLocals(
            target,
            pdb.Reader,
            genericRun,
            ("localRequest", $"{TargetNamespace}.RequestContext"),
            ("localNumber", "int32"),
            ("localVector", $"{TargetNamespace}.RequestContext[]"),
            ("localExternal", $"{AliasNamespace}.ExternalRequestContext"),
            ("localImported", "int32"),
            ("typeWitness", "System.Type"),
            ("methodWitness", "System.Type"));

        var outerRun = FindMethod(reader, $"{TargetNamespace}.ScopeEvidence", "OuterScopeProbe", "Run");
        var innerRun = FindMethod(
            reader,
            $"{TargetNamespace}.ScopeEvidence.InnerScope",
            "InnerScopeProbe",
            "Run");
        var outerChain = ReadImportChain(
            pdb.Reader,
            reader,
            SelectLeafImportScope(ReadMethodScopes(pdb.Reader, outerRun)));
        var innerChain = ReadImportChain(
            pdb.Reader,
            reader,
            SelectLeafImportScope(ReadMethodScopes(pdb.Reader, innerRun)));
        var outerAlias = Assert.Single(
            outerChain,
            fact => fact.RawKind == 9 && string.Equals(fact.Alias, "RequestSlot", StringComparison.Ordinal));
        Assert.Equal(
            $"{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext>",
            signatures.Format(outerAlias.TargetType));

        var shadowAliases = innerChain
            .Where(fact => fact.RawKind == 9 && string.Equals(fact.Alias, "RequestSlot", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, shadowAliases.Length);
        Assert.Equal(
            $"{TargetNamespace}.GenericSlot`1<{TargetNamespace}.BatchContext>",
            signatures.Format(shadowAliases[0].TargetType));
        Assert.Equal(
            $"{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext>",
            signatures.Format(shadowAliases[1].TargetType));
        Assert.True(shadowAliases[0].ScopeDepth < shadowAliases[1].ScopeDepth);
        Assert.Contains(outerAlias.ScopeHandle, innerChain.Select(static fact => fact.ScopeHandle));

        AssertNamedLocals(
            target,
            pdb.Reader,
            innerRun,
            ("shadowedAliasValue", $"{TargetNamespace}.BatchContext"),
            ("localRequest", $"{TargetNamespace}.RequestContext"),
            ("localBatch", $"{TargetNamespace}.BatchContext"),
            ("localNumber", "int32"));

        var genericScopeChain = ReadImportScopeChain(
            pdb.Reader,
            reader,
            SelectLeafImportScope(genericScopes));
        Assert.Equal(new[] { 0, 0, 23, 1 }, genericScopeChain.Select(static scope => scope.Imports.Length));
        Assert.Equal(
            new[]
            {
                "5|requestlib",
                "1|System", "1|System.Collections.Generic", "1|System.IO", "1|System.Linq",
                "1|System.Net.Http", "1|System.Threading", "1|System.Threading.Tasks",
                "1|System.Runtime.CompilerServices",
                "1|System.Runtime.Loader",
                $"3|{TargetNamespace}.NonGenericImports",
                $"3|{TargetNamespace}.StaticImports`1<{TargetNamespace}.RequestContext>",
                $"9|ExternalRequestSlot|{AliasNamespace}.ExternalSlot`1<{AliasNamespace}.ExternalRequestContext>",
                $"9|ExternalTypeAlias|{AliasNamespace}.ExternalRequestContext",
                $"9|ForwardedTypeAlias|{AliasNamespace}.ForwardedRequestContext",
                $"9|IntMatrixSlot|{TargetNamespace}.GenericSlot`1<int32[rank=2;sizes=;lower=0,0]>",
                $"9|LocalTypeAlias|{TargetNamespace}.RequestContext",
                "9|NamedRvaStorage|PhoenixInspect.W8NamedRvaTarget.NamedRvaStorage",
                $"9|RequestMatrixAlias|{TargetNamespace}.RequestContext[rank=2;sizes=;lower=0,0]",
                $"9|RequestMatrixSlot|{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext[rank=2;sizes=;lower=0,0]>",
                $"9|RequestSlot|{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext>",
                $"9|RequestVectorAlias|{TargetNamespace}.RequestContext[]",
                $"9|RequestVectorSlot|{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext[]>",
            },
            genericScopeChain[2].Imports.Select(import => DescribeImport(reader, import, signatures)));
        Assert.Equal(new[] { "6|requestlib|PhoenixInspect.W8AliasTarget" },
            genericScopeChain[3].Imports.Select(import => DescribeImport(reader, import, signatures)));

        var shadowScopeChain = ReadImportScopeChain(
            pdb.Reader,
            reader,
            SelectLeafImportScope(ReadMethodScopes(pdb.Reader, innerRun)));
        Assert.Equal(new[] { 1, 0, 0, 0, 9, 1 }, shadowScopeChain.Select(static scope => scope.Imports.Length));
        Assert.Equal(
            new[] { $"9|RequestSlot|{TargetNamespace}.GenericSlot`1<{TargetNamespace}.BatchContext>" },
            shadowScopeChain[0].Imports.Select(import => DescribeImport(reader, import, signatures)));
        Assert.Equal(
            new[]
            {
                "1|System", "1|System.Collections.Generic", "1|System.IO", "1|System.Linq",
                "1|System.Net.Http", "1|System.Threading", "1|System.Threading.Tasks",
                "1|System.Runtime.CompilerServices",
                $"9|RequestSlot|{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext>",
            },
            shadowScopeChain[4].Imports.Select(import => DescribeImport(reader, import, signatures)));

        var lexicalRun = FindMethod(
            reader,
            $"{TargetNamespace}.LexicalEvidence",
            "LexicalCatalogProbe",
            "Run");
        var lexicalScopeChain = ReadImportScopeChain(
            pdb.Reader,
            reader,
            SelectLeafImportScope(ReadMethodScopes(pdb.Reader, lexicalRun)));
        Assert.Equal(new[] { 0, 0, 0, 18, 1 }, lexicalScopeChain.Select(static scope => scope.Imports.Length));
        Assert.Equal(
            new[]
            {
                "5|requestlib",
                "1|System", "1|System.Collections.Generic", "1|System.IO", "1|System.Linq",
                "1|System.Net.Http", "1|System.Threading", "1|System.Threading.Tasks",
                "1|System.Runtime.CompilerServices",
                $"3|{TargetNamespace}.NonGenericImports",
                $"3|{TargetNamespace}.StaticImports`1<{TargetNamespace}.BatchContext>",
                $"9|ConstructedAlias|{TargetNamespace}.GenericSlot`1<{TargetNamespace}.RequestContext>",
                $"9|ExternalInterfaceAlias|{AliasNamespace}.IExternalInterfaceSlot`1<{AliasNamespace}.ExternalRequestContext>",
                $"9|MatrixAlias|{TargetNamespace}.RequestContext[rank=2;sizes=;lower=0,0]",
                $"9|NestedOwnerAlias|{TargetNamespace}.Outer`1+Middle+Inner`1<{TargetNamespace}.RequestContext,{TargetNamespace}.BatchContext>",
                $"9|TypeDefinitionAlias|{TargetNamespace}.RequestContext",
                $"9|TypeReferenceAlias|{AliasNamespace}.ExternalRequestContext",
                $"9|VectorAlias|{TargetNamespace}.RequestContext[]",
            },
            lexicalScopeChain[3].Imports.Select(import => DescribeImport(reader, import, signatures)));

        var ambiguityRun = FindMethod(
            reader,
            "PhoenixInspect.W8AmbiguityEvidence",
            "CrossAssemblyAmbiguityProfile",
            "Run");
        var ambiguityChain = ReadImportScopeChain(
            pdb.Reader,
            reader,
            SelectLeafImportScope(ReadMethodScopes(pdb.Reader, ambiguityRun)));
        Assert.Equal(new[] { 2, 0, 8, 1 }, ambiguityChain.Select(static scope => scope.Imports.Length));
        Assert.Equal(
            new[] { "1|PhoenixInspect.W8AliasTarget", "1|PhoenixInspect.W8TestTarget" },
            ambiguityChain[0].Imports.Select(import => DescribeImport(reader, import, signatures)));
    }

    /// <summary>
    /// Decodes general TypeSpec signatures into structured trees that preserve raw bytes, class/value kind,
    /// definition or reference tokens, resolution scopes, ordered generic arguments, and exact array topology.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void TypeSpec_aliases_retain_structured_tokens_scopes_arguments_and_arrays()
    {
        using var target = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        using var pdb = PdbArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolvePortablePdb()));
        var reader = target.Reader;
        var genericRun = FindMethod(reader, TargetNamespace, "W8FrameProbe", "Run");
        var lexicalRun = FindMethod(
            reader,
            $"{TargetNamespace}.LexicalEvidence",
            "LexicalCatalogProbe",
            "Run");
        var imports = ReadImportChain(
            pdb.Reader,
            reader,
            SelectLeafImportScope(ReadMethodScopes(pdb.Reader, genericRun)))
            .Concat(ReadImportChain(
                pdb.Reader,
                reader,
                SelectLeafImportScope(ReadMethodScopes(pdb.Reader, lexicalRun))))
            .ToArray();
        var aliases = imports
            .Where(static import => import.RawKind == 9 && import.Alias is not null)
            .GroupBy(static import => import.Alias!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        var requestDefinition = FindTopLevelType(reader, TargetNamespace, "RequestContext");
        var batchDefinition = FindTopLevelType(reader, TargetNamespace, "BatchContext");
        var genericSlotDefinition = FindTopLevelType(reader, TargetNamespace, "GenericSlot`1");
        var outerDefinition = FindTopLevelType(reader, TargetNamespace, "Outer`1");
        var middleDefinition = FindNestedType(reader, outerDefinition, "Middle");
        var innerDefinition = FindNestedType(reader, middleDefinition, "Inner`1");
        var externalContextReference = FindTypeReference(
            reader,
            AliasNamespace,
            "ExternalRequestContext",
            AliasAssemblyName);
        var externalSlotReference = FindTypeReference(reader, AliasNamespace, "ExternalSlot`1", AliasAssemblyName);
        var externalInterfaceReference = FindTypeReference(
            reader,
            AliasNamespace,
            "IExternalInterfaceSlot`1",
            AliasAssemblyName);
        var request = ExpectedNamed(reader, requestDefinition, (byte)SignatureTypeKind.Class);
        var batch = ExpectedNamed(reader, batchDefinition, (byte)SignatureTypeKind.Class);
        var external = ExpectedNamed(reader, externalContextReference, (byte)SignatureTypeKind.Class);
        var requestSlot = ExpectedGeneric(
            reader,
            genericSlotDefinition,
            (byte)SignatureTypeKind.Class,
            request);
        var requestMatrix = ExpectedMatrix(request);
        var requestVector = ExpectedVector(request);
        var expectations = new[]
        {
            new AliasSignatureExpectation("RequestSlot", requestSlot),
            new AliasSignatureExpectation("ConstructedAlias", requestSlot),
            new AliasSignatureExpectation(
                "ExternalRequestSlot",
                ExpectedGeneric(
                    reader,
                    externalSlotReference,
                    (byte)SignatureTypeKind.Class,
                    external)),
            new AliasSignatureExpectation(
                "ExternalInterfaceAlias",
                ExpectedGeneric(
                    reader,
                    externalInterfaceReference,
                    (byte)SignatureTypeKind.Class,
                    external)),
            new AliasSignatureExpectation(
                "IntMatrixSlot",
                ExpectedGeneric(
                    reader,
                    genericSlotDefinition,
                    (byte)SignatureTypeKind.Class,
                    ExpectedMatrix(ExpectedPrimitive(PrimitiveTypeCode.Int32)))),
            new AliasSignatureExpectation("MatrixAlias", requestMatrix),
            new AliasSignatureExpectation("RequestMatrixAlias", requestMatrix),
            new AliasSignatureExpectation(
                "RequestMatrixSlot",
                ExpectedGeneric(
                    reader,
                    genericSlotDefinition,
                    (byte)SignatureTypeKind.Class,
                    requestMatrix)),
            new AliasSignatureExpectation(
                "NestedOwnerAlias",
                ExpectedGeneric(
                    reader,
                    innerDefinition,
                    (byte)SignatureTypeKind.Class,
                    request,
                    batch)),
            new AliasSignatureExpectation("RequestVectorAlias", requestVector),
            new AliasSignatureExpectation("VectorAlias", requestVector),
            new AliasSignatureExpectation(
                "RequestVectorSlot",
                ExpectedGeneric(
                    reader,
                    genericSlotDefinition,
                    (byte)SignatureTypeKind.Class,
                    requestVector)),
        };
        foreach (var expectation in expectations)
        {
            var import = aliases[expectation.Alias];
            Assert.Equal(HandleKind.TypeSpecification, import.TargetType.Kind);
            AssertStructuredSignature(reader, ReadStructuredSignature(reader, import.TargetType), expectation.Signature);
        }

        var directExpectations = new[]
        {
            (Alias: "LocalTypeAlias", Expected: ExpectedNamed(reader, requestDefinition, rawTypeKind: 0)),
            (Alias: "TypeDefinitionAlias", Expected: ExpectedNamed(reader, requestDefinition, rawTypeKind: 0)),
            (Alias: "ExternalTypeAlias", Expected: ExpectedNamed(reader, externalContextReference, rawTypeKind: 0)),
            (Alias: "TypeReferenceAlias", Expected: ExpectedNamed(reader, externalContextReference, rawTypeKind: 0)),
            (Alias: "ForwardedTypeAlias", Expected: ExpectedNamed(
                reader,
                FindTypeReference(reader, AliasNamespace, "ForwardedRequestContext", AliasAssemblyName),
                rawTypeKind: 0)),
            (Alias: "NamedRvaStorage", Expected: ExpectedNamed(
                reader,
                FindTypeReference(
                    reader,
                    "PhoenixInspect.W8NamedRvaTarget",
                    "NamedRvaStorage",
                    "PhoenixInspect.W8NamedRvaTarget"),
                rawTypeKind: 0)),
        };
        foreach (var expectation in directExpectations)
        {
            var actual = ReadStructuredSignature(reader, aliases[expectation.Alias].TargetType);
            Assert.Empty(actual.RawBytes);
            AssertStructuredNode(actual.Root, expectation.Expected);
        }

        var nullableField = ReadFields(
            reader,
            reader.GetTypeDefinition(FindTopLevelType(reader, TargetNamespace, "PrimitiveStorage")))["Nullable"];
        var nullableReference = FindTypeReference(reader, "System", "Nullable`1", assemblyName: null);
        var expectedNullable = ExpectedGeneric(
            reader,
            nullableReference,
            (byte)SignatureTypeKind.ValueType,
            ExpectedPrimitive(PrimitiveTypeCode.Int32));
        var nullable = ReadStructuredFieldSignature(reader, nullableField);
        Assert.Equal(
            new[] { (byte)SignatureKind.Field }.Concat(EncodeExpectedSignature(expectedNullable)),
            nullable.RawBytes);
        AssertStructuredNode(nullable.Root, expectedNullable);
    }

    /// <summary>
    /// Proves source lexical spellings have exact emitted dispositions: active locals, a Portable-PDB constant,
    /// parameters, import aliases, generated local-function/query methods, or deliberately no field/local row.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Lexical_spellings_have_complete_emitted_and_non_emitted_dispositions()
    {
        using var target = PeArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
        using var pdb = PdbArtifact.Open(RequireArtifact(W8TestTargetPaths.ResolvePortablePdb()));
        var reader = target.Reader;
        var lexicalRun = FindMethod(
            reader,
            $"{TargetNamespace}.LexicalEvidence",
            "LexicalCatalogProbe",
            "Run");
        var pause = AssertSinglePauseInstruction(target, lexicalRun);
        var activeScopes = ReadActiveMethodScopes(pdb.Reader, lexicalRun, pause.Offset);
        var activeLocals = activeScopes
            .SelectMany(static scope => scope.GetLocalVariables())
            .Select(pdb.Reader.GetLocalVariable)
            .Select(local => pdb.Reader.GetString(local.Name))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new[]
            {
                "byteVector", "catchLocal", "deconstructionNumber", "deconstructionText",
                "externalInterfaceAliasWitness", "fixedLocal",
                "foreachLocal", "genericImportedLocal", "importedNestedLocal", "localFunctionResult",
                "matrixLocal", "nestedOwnerAliasWitness", "nonGenericImportedLocal", "nonGenericNestedLocal", "ordinaryLocal",
                "patternLocal", "rangeLocal", "typeReferenceLocal", "usingLocal", "vectorLocal",
            },
            activeLocals.Order(StringComparer.Ordinal));
        var constant = Assert.Single(
            activeScopes.SelectMany(static scope => scope.GetLocalConstants()).Select(pdb.Reader.GetLocalConstant));
        Assert.Equal("localConstant", pdb.Reader.GetString(constant.Name));
        Assert.Equal("08-01-7A-02-1C", BitConverter.ToString(pdb.Reader.GetBlobBytes(constant.Signature)));
        Assert.DoesNotContain("localConstant", activeLocals);

        var importAliases = ReadImportChain(
                pdb.Reader,
                reader,
                SelectLeafImportScope(activeScopes))
            .Where(static import => import.RawKind == 9 && import.Alias is not null)
            .Select(static import => import.Alias!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new[]
            {
                "ConstructedAlias", "ExternalInterfaceAlias", "MatrixAlias", "NestedOwnerAlias",
                "TypeDefinitionAlias", "TypeReferenceAlias", "VectorAlias",
            },
            importAliases.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(importAliases, activeLocals.Contains);

        var lexicalOwner = reader.GetTypeDefinition(
            FindTopLevelType(reader, $"{TargetNamespace}.LexicalEvidence", "LexicalCatalogProbe"));
        var localFunction = Assert.Single(
            lexicalOwner.GetMethods(),
            handle => reader.GetString(reader.GetMethodDefinition(handle).Name).Contains(
                "g__LocalFunction",
                StringComparison.Ordinal));
        Assert.Equal(
            new[] { "left", "right", "element" },
            ReadParameterNames(reader, localFunction));
        Assert.DoesNotContain(
            reader.GetString(reader.GetMethodDefinition(localFunction).Name),
            activeLocals);

        var queryOwnerHandle = FindTopLevelType(reader, TargetNamespace, "QueryRangeProfile");
        var queryPause = FindMethod(reader, TargetNamespace, "QueryRangeProfile", "PauseFromRange");
        Assert.Equal(new[] { "profile", "queryRangeVariable" }, ReadParameterNames(reader, queryPause));
        var displayClass = Assert.Single(
            reader.TypeDefinitions,
            handle => reader.GetTypeDefinition(handle).GetDeclaringType() == queryOwnerHandle &&
                reader.GetString(reader.GetTypeDefinition(handle).Name).StartsWith("<>c__DisplayClass", StringComparison.Ordinal));
        var generatedQueryParameters = reader.GetTypeDefinition(displayClass).GetMethods()
            .Where(handle => reader.GetString(reader.GetMethodDefinition(handle).Name).StartsWith("<Run>b__", StringComparison.Ordinal))
            .Select(handle => Assert.Single(ReadParameterNames(reader, handle)))
            .ToArray();
        Assert.Equal(new[] { "queryRangeVariable", "queryRangeVariable" }, generatedQueryParameters);

        var parameterBlocker = FindMethod(
            reader,
            $"{TargetNamespace}.LexicalEvidence",
            "ParameterBlockerProbe",
            "ReadParameter");
        Assert.Equal(new[] { "ImportedSentinel" }, ReadParameterNames(reader, parameterBlocker));
        Assert.Empty(ReadMethodScopesOrEmpty(pdb.Reader, parameterBlocker)
            .SelectMany(static scope => scope.GetLocalVariables()));

        var blockerOwner = reader.GetTypeDefinition(
            FindTopLevelType(reader, $"{TargetNamespace}.LexicalEvidence", "CurrentMemberBlockerProbe"));
        Assert.DoesNotContain(
            blockerOwner.GetFields(),
            handle => string.Equals(
                reader.GetString(reader.GetFieldDefinition(handle).Name),
                "ImportedSentinel",
                StringComparison.Ordinal));
        Assert.Single(
            blockerOwner.GetProperties(),
            handle => string.Equals(reader.GetString(reader.GetPropertyDefinition(handle).Name), "ImportedSentinel", StringComparison.Ordinal));
        Assert.Single(
            blockerOwner.GetMethods(),
            handle => string.Equals(reader.GetString(reader.GetMethodDefinition(handle).Name), "NonGenericImportedSentinel", StringComparison.Ordinal));

        var bareRead = FindMethod(
            reader,
            $"{TargetNamespace}.LexicalEvidence",
            "BlockerFreeBareNameProbe",
            "ReadImportedFields");
        Assert.Equal(
            new[]
            {
                $"{TargetNamespace}.NonGenericImports::NonGenericImportedSentinel",
                $"{TargetNamespace}.StaticImports`1<{TargetNamespace}.BatchContext>::ImportedSentinel",
            },
            ReadReferencedFields(target, bareRead).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Applies independent one-fact perturbations only to copied PDB identities, a physically truncated PDB,
    /// TypeSpec/import bytes, an assembly identity, and an RVA payload; each altered copy is rejected or distinguished
    /// while unrelated artifact digests, identities, and table facts remain byte-for-byte unchanged.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Independent_copied_fact_perturbations_do_not_change_unrelated_evidence()
    {
        var targetPath = RequireArtifact(W8TestTargetPaths.ResolveAssembly());
        var pdbPath = RequireArtifact(W8TestTargetPaths.ResolvePortablePdb());
        var aliasPath = RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly());
        var namedPath = RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly());
        using var target = PeArtifact.Open(targetPath);
        using var pdb = PdbArtifact.Open(pdbPath);
        using var alias = PeArtifact.Open(aliasPath);
        using var named = PeArtifact.Open(namedPath);
        using var temporary = TemporaryArtifactDirectory.Create();
        var targetDigest = EvidenceDigest(File.ReadAllBytes(targetPath));
        var pdbDigest = EvidenceDigest(File.ReadAllBytes(pdbPath));
        var aliasDigest = EvidenceDigest(File.ReadAllBytes(aliasPath));
        var namedDigest = EvidenceDigest(File.ReadAllBytes(namedPath));
        var targetMvid = target.Reader.GetGuid(target.Reader.GetModuleDefinition().Mvid);
        var aliasMvid = alias.Reader.GetGuid(alias.Reader.GetModuleDefinition().Mvid);
        var namedMvid = named.Reader.GetGuid(named.Reader.GetModuleDefinition().Mvid);
        var aliasIdentity = ReadAssemblyDefinitionIdentity(alias.Reader);
        var targetAliasIdentity = ReadAssemblyReferenceIdentity(
            target.Reader,
            FindAssemblyReference(target.Reader, AliasAssemblyName));
        var originalLocalScopeCount = pdb.Reader.GetTableRowCount(TableIndex.LocalScope);
        Assert.Equal(targetAliasIdentity, aliasIdentity);

        var codeViewEntry = Assert.Single(
            target.PeReader.ReadDebugDirectory(),
            static entry => entry.Type == DebugDirectoryEntryType.CodeView);
        var codeView = target.PeReader.ReadCodeViewDebugDirectoryData(codeViewEntry);
        var pdbId = pdb.Reader.DebugMetadataHeader!.Id.ToArray();
        var changedPdbId = (byte[])pdbId.Clone();
        changedPdbId[0] ^= 0x01;
        var identityCopyPath = CreateUniqueMutationCopy(
            pdbPath,
            temporary,
            "identity.pdb",
            pdbId,
            relativeOffset: 0,
            changedPdbId[0]);
        using (var identityCopy = PdbArtifact.Open(identityCopyPath))
        {
            Assert.Equal(changedPdbId, identityCopy.Reader.DebugMetadataHeader!.Id);
            Assert.False(PdbIdentityMatches(codeViewEntry, codeView, identityCopy.Reader.DebugMetadataHeader.Id.AsSpan()));
            Assert.Equal(
                pdb.Reader.GetTableRowCount(TableIndex.LocalScope),
                identityCopy.Reader.GetTableRowCount(TableIndex.LocalScope));
            Assert.Equal(
                pdb.Reader.GetTableRowCount(TableIndex.ImportScope),
                identityCopy.Reader.GetTableRowCount(TableIndex.ImportScope));
        }

        Assert.True(PdbIdentityMatches(codeViewEntry, codeView, pdbId));

        var partialPdbPath = CreatePartialCopy(
            pdbPath,
            temporary,
            "partial.pdb",
            retainedLength: 128);
        Assert.Throws<BadImageFormatException>(() =>
        {
            using var partialPdb = PdbArtifact.Open(partialPdbPath);
            _ = partialPdb.Reader.DebugMetadataHeader;
        });
        Assert.Equal(pdbDigest, EvidenceDigest(File.ReadAllBytes(pdbPath)));
        Assert.Equal(originalLocalScopeCount, pdb.Reader.GetTableRowCount(TableIndex.LocalScope));

        var run = FindMethod(target.Reader, TargetNamespace, "W8FrameProbe", "Run");
        var leafImportScope = SelectLeafImportScope(ReadMethodScopes(pdb.Reader, run));
        var importScopes = ReadImportScopeChain(pdb.Reader, target.Reader, leafImportScope);
        var populatedImportScope = Assert.Single(importScopes, static scope => scope.Imports.Length > 20);
        var changedImport = (byte[])populatedImportScope.RawPayload.Clone();
        changedImport[0] = 0;
        var importCopyPath = CreateUniqueMutationCopy(
            pdbPath,
            temporary,
            "import.pdb",
            populatedImportScope.RawPayload,
            relativeOffset: 0,
            changedImport[0]);
        using (var importCopy = PdbArtifact.Open(importCopyPath))
        {
            Assert.Equal(pdbId, importCopy.Reader.DebugMetadataHeader!.Id);
            Assert.Throws<Xunit.Sdk.XunitException>(() => ReadImportScopeChain(
                importCopy.Reader,
                target.Reader,
                leafImportScope));
            Assert.Equal(
                pdb.Reader.GetTableRowCount(TableIndex.LocalVariable),
                importCopy.Reader.GetTableRowCount(TableIndex.LocalVariable));
        }

        var lexicalRun = FindMethod(
            target.Reader,
            $"{TargetNamespace}.LexicalEvidence",
            "LexicalCatalogProbe",
            "Run");
        var nestedAlias = Assert.Single(
            ReadImportChain(
                pdb.Reader,
                target.Reader,
                SelectLeafImportScope(ReadMethodScopes(pdb.Reader, lexicalRun))),
            import => import.RawKind == 9 && string.Equals(import.Alias, "NestedOwnerAlias", StringComparison.Ordinal));
        var typeSpec = ReadStructuredSignature(target.Reader, nestedAlias.TargetType);
        var changedTypeSpec = (byte[])typeSpec.RawBytes.Clone();
        changedTypeSpec[0] = 0;
        var typeSpecCopyPath = CreateUniqueMutationCopy(
            targetPath,
            temporary,
            "typespec.dll",
            typeSpec.RawBytes,
            relativeOffset: 0,
            changedTypeSpec[0]);
        using (var typeSpecCopy = PeArtifact.Open(typeSpecCopyPath))
        {
            Assert.Equal(targetMvid, typeSpecCopy.Reader.GetGuid(typeSpecCopy.Reader.GetModuleDefinition().Mvid));
            Assert.Throws<BadImageFormatException>(() => ReadStructuredSignature(
                typeSpecCopy.Reader,
                nestedAlias.TargetType));
            Assert.Equal(target.Reader.TypeDefinitions.Count, typeSpecCopy.Reader.TypeDefinitions.Count);
            Assert.Equal(target.Reader.AssemblyReferences.Count, typeSpecCopy.Reader.AssemblyReferences.Count);
        }

        var aliasIdentityBytes = Encoding.UTF8.GetBytes($"{aliasIdentity.Name}\0");
        var changedAliasNameByte = (byte)(aliasIdentityBytes[^2] ^ 0x01);
        var aliasNameFileOffset = GetMetadataStringFileOffset(
            alias,
            alias.Reader.GetAssemblyDefinition().Name,
            aliasIdentityBytes);
        var aliasIdentityCopyPath = CreateMappedMutationCopy(
            aliasPath,
            temporary,
            "identity-alias.dll",
            aliasIdentityBytes,
            aliasNameFileOffset,
            relativeOffset: aliasIdentityBytes.Length - 2,
            changedAliasNameByte);
        using (var aliasIdentityCopy = PeArtifact.Open(aliasIdentityCopyPath))
        {
            var changedIdentity = ReadAssemblyDefinitionIdentity(aliasIdentityCopy.Reader);
            Assert.NotEqual(aliasIdentity, changedIdentity);
            Assert.NotEqual(targetAliasIdentity, changedIdentity);
            Assert.Equal(aliasIdentity.Name.Length, changedIdentity.Name.Length);
            Assert.Equal(aliasIdentity.Version, changedIdentity.Version);
            Assert.Equal(aliasIdentity.Culture, changedIdentity.Culture);
            Assert.Equal(aliasIdentity.KeyOrToken, changedIdentity.KeyOrToken);
            Assert.Equal(aliasIdentity.Flags, changedIdentity.Flags);
            Assert.Equal(
                aliasMvid,
                aliasIdentityCopy.Reader.GetGuid(aliasIdentityCopy.Reader.GetModuleDefinition().Mvid));
            Assert.Equal(alias.Reader.TypeDefinitions.Count, aliasIdentityCopy.Reader.TypeDefinitions.Count);
            Assert.Equal(alias.Reader.AssemblyReferences.Count, aliasIdentityCopy.Reader.AssemblyReferences.Count);
        }

        var namedOwner = named.Reader.GetTypeDefinition(
            FindTopLevelType(named.Reader, "PhoenixInspect.W8NamedRvaTarget", "NamedRvaStorage"));
        var namedFields = ReadFields(named.Reader, namedOwner);
        var namedField = namedFields["NamedSentinel"];
        var wideField = namedFields["NamedWideSentinel"];
        var geometry = ReadRvaGeometry(named, namedField, sizeof(int));
        var changedRva = (byte[])geometry.Bytes.Clone();
        changedRva[^1] ^= 0x80;
        var wideGeometry = ReadRvaGeometry(named, wideField, sizeof(long));
        var rvaCopyPath = CreateUniqueMutationCopy(
            namedPath,
            temporary,
            "rva.dll",
            geometry.Bytes,
            relativeOffset: geometry.Bytes.Length - 1,
            changedRva[^1]);
        using (var rvaCopy = PeArtifact.Open(rvaCopyPath))
        {
            Assert.Equal(namedMvid, rvaCopy.Reader.GetGuid(rvaCopy.Reader.GetModuleDefinition().Mvid));
            Assert.Equal(changedRva, ReadRvaGeometry(rvaCopy, namedField, sizeof(int)).Bytes);
            Assert.Equal(wideGeometry.Bytes, ReadRvaGeometry(rvaCopy, wideField, sizeof(long)).Bytes);
            Assert.Equal(
                named.Reader.GetFieldDefinition(namedField).GetRelativeVirtualAddress(),
                rvaCopy.Reader.GetFieldDefinition(namedField).GetRelativeVirtualAddress());
        }

        Assert.Equal(targetDigest, EvidenceDigest(File.ReadAllBytes(targetPath)));
        Assert.Equal(pdbDigest, EvidenceDigest(File.ReadAllBytes(pdbPath)));
        Assert.Equal(aliasDigest, EvidenceDigest(File.ReadAllBytes(aliasPath)));
        Assert.Equal(namedDigest, EvidenceDigest(File.ReadAllBytes(namedPath)));
        Assert.Equal(targetMvid, target.Reader.GetGuid(target.Reader.GetModuleDefinition().Mvid));
        Assert.Equal(aliasMvid, alias.Reader.GetGuid(alias.Reader.GetModuleDefinition().Mvid));
        Assert.Equal(namedMvid, named.Reader.GetGuid(named.Reader.GetModuleDefinition().Mvid));
        Assert.Equal(aliasIdentity, ReadAssemblyDefinitionIdentity(alias.Reader));
        Assert.Equal(typeSpec.RawBytes, target.Reader.GetBlobBytes(
            target.Reader.GetTypeSpecification((TypeSpecificationHandle)typeSpec.Token).Signature));
        Assert.Equal(geometry.Rva, named.Reader.GetFieldDefinition(namedField).GetRelativeVirtualAddress());
        Assert.Equal(geometry.Bytes, ReadRvaGeometry(named, namedField, sizeof(int)).Bytes);
    }

    private static bool PdbIdentityMatches(
        DebugDirectoryEntry codeViewEntry,
        CodeViewDebugDirectoryData codeView,
        ReadOnlySpan<byte> pdbId) =>
        pdbId.Length == 20 &&
        codeView.Guid == new Guid(pdbId[..16]) &&
        codeViewEntry.Stamp == BinaryPrimitives.ReadUInt32LittleEndian(pdbId[16..]);

    private static ulong EvidenceDigest(ReadOnlySpan<byte> bytes)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var digest = offset;
        foreach (var value in bytes)
        {
            digest ^= value;
            digest *= prime;
        }

        return digest;
    }

    private static string CreateUniqueMutationCopy(
        string sourcePath,
        TemporaryArtifactDirectory temporary,
        string copyName,
        ReadOnlySpan<byte> exactSequence,
        int relativeOffset,
        byte replacement)
    {
        var source = File.ReadAllBytes(sourcePath);
        Assert.InRange(source.Length, 1, MaximumMutationArtifactBytes);
        Assert.InRange(exactSequence.Length, 1, source.Length);
        Assert.InRange(relativeOffset, 0, exactSequence.Length - 1);
        Assert.NotEqual(exactSequence[relativeOffset], replacement);

        var occurrences = FindSequenceOffsets(source, exactSequence);
        var occurrence = Assert.Single(occurrences);
        return CreateMappedMutationCopy(
            sourcePath,
            temporary,
            copyName,
            exactSequence,
            occurrence,
            relativeOffset,
            replacement);
    }

    private static string CreateMappedMutationCopy(
        string sourcePath,
        TemporaryArtifactDirectory temporary,
        string copyName,
        ReadOnlySpan<byte> exactSequence,
        int sequenceFileOffset,
        int relativeOffset,
        byte replacement)
    {
        var source = File.ReadAllBytes(sourcePath);
        Assert.InRange(source.Length, 1, MaximumMutationArtifactBytes);
        Assert.InRange(exactSequence.Length, 1, source.Length);
        Assert.InRange(sequenceFileOffset, 0, source.Length - exactSequence.Length);
        Assert.InRange(relativeOffset, 0, exactSequence.Length - 1);
        Assert.True(source.AsSpan(sequenceFileOffset, exactSequence.Length).SequenceEqual(exactSequence));
        Assert.NotEqual(exactSequence[relativeOffset], replacement);
        var absoluteOffset = checked(sequenceFileOffset + relativeOffset);
        var changed = (byte[])source.Clone();
        changed[absoluteOffset] = replacement;

        var changedOffsets = Enumerable.Range(0, source.Length)
            .Where(index => source[index] != changed[index])
            .ToArray();
        Assert.Equal(new[] { absoluteOffset }, changedOffsets);

        var copyPath = temporary.Resolve(copyName);
        File.WriteAllBytes(copyPath, changed);
        Assert.Equal(source.Length, new FileInfo(copyPath).Length);
        Assert.Equal(changed, File.ReadAllBytes(copyPath));
        return copyPath;
    }

    private static int GetMetadataStringFileOffset(
        PeArtifact artifact,
        StringHandle handle,
        ReadOnlySpan<byte> expectedBytes)
    {
        var image = File.ReadAllBytes(artifact.Path);
        var metadataStart = artifact.PeReader.PEHeaders.MetadataStartOffset;
        Assert.InRange(metadataStart, 0, image.Length - 20);
        var metadata = image.AsSpan(metadataStart);
        Assert.Equal(0x424A5342U, BinaryPrimitives.ReadUInt32LittleEndian(metadata));
        var versionLength = BinaryPrimitives.ReadInt32LittleEndian(metadata[12..]);
        Assert.InRange(versionLength, 1, metadata.Length - 20);
        var cursor = AlignToFour(checked(16 + versionLength));
        Assert.InRange(cursor, 0, metadata.Length - 4);
        var streamCount = BinaryPrimitives.ReadUInt16LittleEndian(metadata[(cursor + 2)..]);
        Assert.InRange(streamCount, (ushort)1, (ushort)16);
        cursor += 4;

        int? stringsStreamOffset = null;
        for (var index = 0; index < streamCount; index++)
        {
            Assert.InRange(cursor, 0, metadata.Length - 9);
            var streamOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(metadata[cursor..]));
            var streamSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(metadata[(cursor + 4)..]));
            var nameStart = cursor + 8;
            var nameLength = 0;
            while (nameStart + nameLength < metadata.Length &&
                metadata[nameStart + nameLength] != 0 &&
                nameLength < 32)
            {
                nameLength++;
            }

            Assert.InRange(nameStart + nameLength, nameStart, metadata.Length - 1);
            Assert.Equal((byte)0, metadata[nameStart + nameLength]);
            var streamName = Encoding.ASCII.GetString(metadata.Slice(nameStart, nameLength));
            Assert.InRange(streamOffset, 0, metadata.Length);
            Assert.InRange(streamSize, 0, metadata.Length - streamOffset);
            if (string.Equals(streamName, "#Strings", StringComparison.Ordinal))
            {
                Assert.Null(stringsStreamOffset);
                stringsStreamOffset = streamOffset;
            }

            cursor = AlignToFour(checked(nameStart + nameLength + 1));
        }

        var heapOffset = MetadataTokens.GetHeapOffset(handle);
        var fileOffset = checked(metadataStart + Assert.IsType<int>(stringsStreamOffset) + heapOffset);
        Assert.InRange(fileOffset, 0, image.Length - expectedBytes.Length);
        Assert.True(image.AsSpan(fileOffset, expectedBytes.Length).SequenceEqual(expectedBytes));
        return fileOffset;
    }

    private static int AlignToFour(int value) => checked((value + 3) & ~3);

    private static string CreatePartialCopy(
        string sourcePath,
        TemporaryArtifactDirectory temporary,
        string copyName,
        int retainedLength)
    {
        var source = File.ReadAllBytes(sourcePath);
        Assert.InRange(source.Length, 2, MaximumMutationArtifactBytes);
        Assert.InRange(retainedLength, 1, source.Length - 1);
        var partial = source.AsSpan(0, retainedLength).ToArray();
        var copyPath = temporary.Resolve(copyName);
        File.WriteAllBytes(copyPath, partial);
        Assert.Equal(retainedLength, new FileInfo(copyPath).Length);
        Assert.Equal(partial, File.ReadAllBytes(copyPath));
        Assert.True(source.AsSpan(0, retainedLength).SequenceEqual(partial));
        return copyPath;
    }

    private static ImmutableArray<int> FindSequenceOffsets(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> sequence)
    {
        var offsets = ImmutableArray.CreateBuilder<int>();
        var lastStart = source.Length - sequence.Length;
        for (var offset = 0; offset <= lastStart; offset++)
        {
            if (!source.Slice(offset, sequence.Length).SequenceEqual(sequence))
            {
                continue;
            }

            offsets.Add(offset);
            if (offsets.Count > 1)
            {
                break;
            }
        }

        return offsets.ToImmutable();
    }

    private static byte[] ComputeSha256(ReadOnlySpan<byte> source)
    {
        ReadOnlySpan<uint> roundConstants =
        [
            0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5, 0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
            0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3, 0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
            0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC, 0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
            0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7, 0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
            0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13, 0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
            0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3, 0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
            0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5, 0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
            0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208, 0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2,
        ];
        var paddedLength = checked(((source.Length + 9 + 63) / 64) * 64);
        var padded = new byte[paddedLength];
        source.CopyTo(padded);
        padded[source.Length] = 0x80;
        BinaryPrimitives.WriteUInt64BigEndian(
            padded.AsSpan(paddedLength - sizeof(ulong)),
            checked((ulong)source.Length * 8UL));

        var state = new uint[]
        {
            0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
            0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19,
        };
        Span<uint> schedule = stackalloc uint[64];
        unchecked
        {
            for (var blockOffset = 0; blockOffset < padded.Length; blockOffset += 64)
            {
                for (var index = 0; index < 16; index++)
                {
                    schedule[index] = BinaryPrimitives.ReadUInt32BigEndian(
                        padded.AsSpan(blockOffset + (index * sizeof(uint)), sizeof(uint)));
                }

                for (var index = 16; index < schedule.Length; index++)
                {
                    var lower = BitOperations.RotateRight(schedule[index - 15], 7) ^
                        BitOperations.RotateRight(schedule[index - 15], 18) ^
                        (schedule[index - 15] >> 3);
                    var upper = BitOperations.RotateRight(schedule[index - 2], 17) ^
                        BitOperations.RotateRight(schedule[index - 2], 19) ^
                        (schedule[index - 2] >> 10);
                    schedule[index] = schedule[index - 16] + lower + schedule[index - 7] + upper;
                }

                var a = state[0];
                var b = state[1];
                var c = state[2];
                var d = state[3];
                var e = state[4];
                var f = state[5];
                var g = state[6];
                var h = state[7];
                for (var index = 0; index < schedule.Length; index++)
                {
                    var upperRotate = BitOperations.RotateRight(e, 6) ^
                        BitOperations.RotateRight(e, 11) ^
                        BitOperations.RotateRight(e, 25);
                    var choice = (e & f) ^ (~e & g);
                    var first = h + upperRotate + choice + roundConstants[index] + schedule[index];
                    var lowerRotate = BitOperations.RotateRight(a, 2) ^
                        BitOperations.RotateRight(a, 13) ^
                        BitOperations.RotateRight(a, 22);
                    var majority = (a & b) ^ (a & c) ^ (b & c);
                    var second = lowerRotate + majority;
                    h = g;
                    g = f;
                    f = e;
                    e = d + first;
                    d = c;
                    c = b;
                    b = a;
                    a = first + second;
                }

                state[0] += a;
                state[1] += b;
                state[2] += c;
                state[3] += d;
                state[4] += e;
                state[5] += f;
                state[6] += g;
                state[7] += h;
            }
        }

        var result = new byte[32];
        for (var index = 0; index < state.Length; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(index * sizeof(uint)), state[index]);
        }

        return result;
    }

    private static ImmutableArray<T> ReadBoundedChain<T>(
        T start,
        Func<T, (bool HasNext, T Next)> readNext,
        MetadataTraversalKind kind)
        where T : notnull
    {
        var values = ImmutableArray.CreateBuilder<T>();
        var visited = new HashSet<T>();
        var current = start;
        while (true)
        {
            AssertTraversalCanEnter(kind, values.Count, visited.Count);
            if (!visited.Add(current))
            {
                throw new Xunit.Sdk.XunitException($"{kind} traversal revisited a metadata node.");
            }

            values.Add(current);
            var next = readNext(current);
            if (!next.HasNext)
            {
                return values.ToImmutable();
            }

            current = next.Next;
        }
    }

    private static void AssertTraversalCanEnter(
        MetadataTraversalKind kind,
        int depth,
        int visitedCount)
    {
        if (depth >= MaximumMetadataTraversalDepth)
        {
            throw new Xunit.Sdk.XunitException(
                $"{kind} traversal exceeded depth {MaximumMetadataTraversalDepth}.");
        }

        if (visitedCount >= MaximumMetadataTraversalNodes)
        {
            throw new Xunit.Sdk.XunitException(
                $"{kind} traversal exceeded {MaximumMetadataTraversalNodes} visited nodes.");
        }
    }

    private static LiteralExpectation Literal(
        string owner,
        string field,
        ConstantTypeCode typeCode,
        byte[] value) =>
        new($"{TargetNamespace}.{owner}::{field}", typeCode, value);

    private static string LiteralKey(MetadataReader reader, FieldDefinitionHandle handle)
    {
        var field = reader.GetFieldDefinition(handle);
        return $"{FormatTypeDefinitionName(reader, FindDeclaringType(reader, handle))}::{reader.GetString(field.Name)}";
    }

    private static string FormatTypeDefinitionName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var chain = ReadBoundedChain(
            handle,
            current =>
            {
                var parent = reader.GetTypeDefinition(current).GetDeclaringType();
                return parent.IsNil ? (false, default) : (true, parent);
            },
            MetadataTraversalKind.DeclaringTypeDefinition);
        var outer = reader.GetTypeDefinition(chain[^1]);
        var namespaceName = reader.GetString(outer.Namespace);
        var names = chain.Reverse().Select(current => reader.GetString(reader.GetTypeDefinition(current).Name));
        var nestedName = string.Join('+', names);
        return namespaceName.Length == 0 ? nestedName : $"{namespaceName}.{nestedName}";
    }

    private static AssemblyReferenceHandle FindAssemblyReference(MetadataReader reader, string name) =>
        Assert.Single(
            reader.AssemblyReferences,
            handle => string.Equals(
                reader.GetString(reader.GetAssemblyReference(handle).Name),
                name,
                StringComparison.Ordinal));

    private static AssemblyIdentityFact ReadAssemblyDefinitionIdentity(MetadataReader reader)
    {
        var definition = reader.GetAssemblyDefinition();
        Assert.Empty(reader.GetBlobBytes(definition.PublicKey));
        Assert.Equal(AssemblyHashAlgorithm.Sha1, definition.HashAlgorithm);
        return new AssemblyIdentityFact(
            reader.GetString(definition.Name),
            definition.Version,
            definition.Culture.IsNil ? string.Empty : reader.GetString(definition.Culture),
            Convert.ToHexString(reader.GetBlobBytes(definition.PublicKey)),
            definition.Flags & ~AssemblyFlags.PublicKey);
    }

    private static AssemblyIdentityFact ReadAssemblyReferenceIdentity(
        MetadataReader reader,
        AssemblyReferenceHandle handle)
    {
        var reference = reader.GetAssemblyReference(handle);
        Assert.Empty(reader.GetBlobBytes(reference.HashValue));
        return new AssemblyIdentityFact(
            reader.GetString(reference.Name),
            reference.Version,
            reference.Culture.IsNil ? string.Empty : reader.GetString(reference.Culture),
            Convert.ToHexString(reader.GetBlobBytes(reference.PublicKeyOrToken)),
            reference.Flags & ~AssemblyFlags.PublicKey);
    }

    private static bool IsNamedTypeDefinition(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        string namespaceName,
        string typeName)
    {
        var definition = reader.GetTypeDefinition(handle);
        return definition.GetDeclaringType().IsNil &&
            string.Equals(reader.GetString(definition.Namespace), namespaceName, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(definition.Name), typeName, StringComparison.Ordinal);
    }

    private static bool IsNamedTypeReference(
        MetadataReader reader,
        TypeReferenceHandle handle,
        string namespaceName,
        string typeName)
    {
        var reference = reader.GetTypeReference(handle);
        return string.Equals(reader.GetString(reference.Namespace), namespaceName, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(reference.Name), typeName, StringComparison.Ordinal);
    }

    private static TypeReferenceHandle FindTypeReference(
        MetadataReader reader,
        string namespaceName,
        string typeName,
        string? assemblyName) =>
        Assert.Single(
            reader.TypeReferences,
            handle => IsNamedTypeReference(reader, handle, namespaceName, typeName) &&
                (assemblyName is null || string.Equals(
                    ReadTypeReferenceAssemblyName(reader, handle),
                    assemblyName,
                    StringComparison.Ordinal)));

    private static string? ReadTypeReferenceAssemblyName(
        MetadataReader reader,
        TypeReferenceHandle handle)
    {
        var chain = ReadBoundedChain(
            reader.GetTypeReference(handle).ResolutionScope,
            current => current.Kind == HandleKind.TypeReference
                ? (true, reader.GetTypeReference((TypeReferenceHandle)current).ResolutionScope)
                : (false, default),
            MetadataTraversalKind.TypeReferenceResolutionScope);
        var terminal = chain[^1];
        return terminal.Kind == HandleKind.AssemblyReference
            ? reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)terminal).Name)
            : null;
    }

    private static void AssertAccessibilityMatrix(
        MetadataReader reader,
        string namespaceName,
        string typeName)
    {
        var owner = reader.GetTypeDefinition(FindTopLevelType(reader, namespaceName, typeName));
        Assert.Equal(TypeAttributes.Public, owner.Attributes & TypeAttributes.VisibilityMask);
        var expected = new Dictionary<string, FieldAttributes>(StringComparer.Ordinal)
        {
            ["PublicSentinel"] = FieldAttributes.Public,
            ["FamilySentinel"] = FieldAttributes.Family,
            ["FamilyOrAssemblySentinel"] = FieldAttributes.FamORAssem,
            ["FamilyAndAssemblySentinel"] = FieldAttributes.FamANDAssem,
            ["AssemblySentinel"] = FieldAttributes.Assembly,
            ["PrivateSentinel"] = FieldAttributes.Private,
        };
        var fields = ReadFields(reader, owner);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), fields.Keys.Order(StringComparer.Ordinal));
        foreach (var item in expected)
        {
            var field = reader.GetFieldDefinition(fields[item.Key]);
            Assert.Equal(item.Value, field.Attributes & FieldAttributes.FieldAccessMask);
            Assert.True((field.Attributes & FieldAttributes.Static) != 0);
        }
    }

    private static string ReadSingleStringAttributeArgument(
        MetadataReader reader,
        CustomAttributeHandle handle)
    {
        var attribute = reader.GetCustomAttribute(handle);
        var blob = reader.GetBlobReader(attribute.Value);
        Assert.Equal(1, blob.ReadUInt16());
        var value = blob.ReadSerializedString();
        Assert.NotNull(value);
        Assert.Equal(0, blob.ReadUInt16());
        Assert.Equal(0, blob.RemainingBytes);
        return value;
    }

    private static string[] ReadNamedAttributeFacts(
        MetadataReader reader,
        CustomAttributeHandleCollection attributes) =>
        attributes
            .Select(handle =>
            {
                var attribute = reader.GetCustomAttribute(handle);
                return $"{FormatAttributeType(reader, attribute.Constructor)}:{BitConverter.ToString(reader.GetBlobBytes(attribute.Value))}";
            })
            .ToArray();

    private static string FormatAttributeType(MetadataReader reader, EntityHandle constructor)
    {
        var owner = constructor.Kind switch
        {
            HandleKind.MethodDefinition =>
                (EntityHandle)reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected attribute constructor kind {constructor.Kind}."),
        };
        var formatter = new MetadataSignatureFormatter(reader);
        return formatter.Format(owner);
    }

    private static FrameExpectation Frame(
        string profile,
        string namespaceName,
        string typeName,
        string methodName,
        int pauseOffset,
        string[] typeGenericParameters,
        string[] methodGenericParameters,
        (string Name, string Type)[] parameters,
        (string Name, string Type)[] locals,
        (string Name, string Type)[]? inactiveLocals = null,
        NamedSlotNonReuseExpectation? namedSlotNonReuse = null,
        int[]? reusedSlots = null,
        int[]? unnamedSlots = null,
        (string Name, string HexSignature)[]? constants = null,
        (string Name, string HexSignature)[]? inactiveConstants = null) =>
        new(
            profile,
            namespaceName,
            typeName,
            methodName,
            pauseOffset,
            typeGenericParameters,
            methodGenericParameters,
            parameters,
            locals,
            inactiveLocals ?? [],
            namedSlotNonReuse,
            reusedSlots ?? [],
            unnamedSlots ?? [],
            constants ?? [],
            inactiveConstants ?? []);

    private static void AssertFrameEvidence(
        PeArtifact target,
        MetadataReader pdbReader,
        FrameExpectation expectation)
    {
        var reader = target.Reader;
        var methodHandle = FindMethod(
            reader,
            expectation.Namespace,
            expectation.Type,
            expectation.Method);
        var method = reader.GetMethodDefinition(methodHandle);
        var pause = AssertSinglePauseInstruction(target, methodHandle);
        Assert.Equal(expectation.PauseOffset, pause.Offset);
        Assert.Equal(OpCodes.Call, pause.OpCode);

        var formatter = new MetadataSignatureFormatter(reader);
        var signature = method.DecodeSignature(formatter, genericContext: null);
        Assert.Equal(expectation.Parameters.Select(static item => item.Type), signature.ParameterTypes);
        Assert.Equal("int32", signature.ReturnType);
        Assert.Equal(expectation.Profile == "generic-frame", signature.Header.IsInstance);
        Assert.Equal(expectation.Profile != "generic-frame", (method.Attributes & MethodAttributes.Static) != 0);
        Assert.Equal(expectation.Parameters.Select(static item => item.Name), ReadParameterNames(reader, methodHandle));

        var owner = reader.GetTypeDefinition(method.GetDeclaringType());
        Assert.Equal(
            expectation.TypeGenericParameters,
            owner.GetGenericParameters()
                .Select(reader.GetGenericParameter)
                .OrderBy(static parameter => parameter.Index)
                .Select(parameter => reader.GetString(parameter.Name)));
        Assert.Equal(
            expectation.MethodGenericParameters,
            method.GetGenericParameters()
                .Select(reader.GetGenericParameter)
                .OrderBy(static parameter => parameter.Index)
                .Select(parameter => reader.GetString(parameter.Name)));

        var allScopes = ReadMethodScopes(pdbReader, methodHandle);
        var activeScopes = ReadActiveMethodScopes(pdbReader, methodHandle, pause.Offset);
        Assert.NotEmpty(activeScopes);
        var allRows = allScopes
            .SelectMany(scope => scope.GetLocalVariables().Select(handle =>
                new ScopedLocal(scope, pdbReader.GetLocalVariable(handle))))
            .ToArray();
        Assert.All(allRows, static row => Assert.Equal(LocalVariableAttributes.None, row.Local.Attributes));
        var repeatedSlotGroups = allRows
            .GroupBy(static row => row.Local.Index)
            .Where(static group => group.Count() > 1)
            .OrderBy(static group => group.Key)
            .ToArray();
        foreach (var group in repeatedSlotGroups)
        {
            var rows = group.ToArray();
            for (var left = 0; left < rows.Length; left++)
            {
                for (var right = left + 1; right < rows.Length; right++)
                {
                    Assert.True(ScopesAreDisjoint(rows[left].Scope, rows[right].Scope));
                }
            }
        }

        Assert.Equal(expectation.ReusedSlots, repeatedSlotGroups.Select(static group => group.Key));

        var body = target.PeReader.GetMethodBody(method.RelativeVirtualAddress);
        var localTypes = body.LocalSignature.IsNil
            ? []
            : reader.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(formatter, genericContext: null);
        Assert.All(allRows, row => Assert.InRange(row.Local.Index, 0, localTypes.Length - 1));
        var activeRows = allRows.Where(row => ScopeContains(row.Scope, pause.Offset)).ToArray();
        var inactiveRows = allRows.Where(row => !ScopeContains(row.Scope, pause.Offset)).ToArray();
        var activeLocals = activeRows
            .ToDictionary(
                row => pdbReader.GetString(row.Local.Name),
                row => (Index: row.Local.Index, Type: localTypes[row.Local.Index]),
                StringComparer.Ordinal);
        var inactiveLocals = inactiveRows
            .ToDictionary(
                row => pdbReader.GetString(row.Local.Name),
                row => (Index: row.Local.Index, Type: localTypes[row.Local.Index]),
                StringComparer.Ordinal);
        Assert.Equal(
            expectation.Locals.Select(static item => item.Name).Order(StringComparer.Ordinal),
            activeLocals.Keys.Order(StringComparer.Ordinal));
        foreach (var expected in expectation.Locals)
        {
            Assert.Equal(expected.Type, activeLocals[expected.Name].Type);
        }

        Assert.Equal(
            expectation.InactiveLocals.Select(static item => item.Name).Order(StringComparer.Ordinal),
            inactiveLocals.Keys.Order(StringComparer.Ordinal));
        foreach (var expected in expectation.InactiveLocals)
        {
            Assert.Equal(expected.Type, inactiveLocals[expected.Name].Type);
        }

        if (expectation.NamedSlotNonReuse is { } nonReuse)
        {
            Assert.Equal(nonReuse.InactiveIndex, inactiveLocals[nonReuse.InactiveName].Index);
            Assert.Equal(nonReuse.ActiveIndex, activeLocals[nonReuse.ActiveName].Index);
            Assert.NotEqual(nonReuse.InactiveIndex, nonReuse.ActiveIndex);
            var inactiveRow = Assert.Single(
                inactiveRows,
                row => string.Equals(
                    pdbReader.GetString(row.Local.Name),
                    nonReuse.InactiveName,
                    StringComparison.Ordinal));
            var activeRow = Assert.Single(
                activeRows,
                row => string.Equals(
                    pdbReader.GetString(row.Local.Name),
                    nonReuse.ActiveName,
                    StringComparison.Ordinal));
            Assert.True(ScopesAreDisjoint(inactiveRow.Scope, activeRow.Scope));
        }

        var namedSlots = allRows.Select(static row => row.Local.Index).ToHashSet();
        var unnamedSlots = Enumerable.Range(0, localTypes.Length)
            .Where(index => !namedSlots.Contains(index))
            .ToArray();
        Assert.Equal(expectation.UnnamedSlots, unnamedSlots);

        var constants = activeScopes
            .SelectMany(static scope => scope.GetLocalConstants())
            .Select(pdbReader.GetLocalConstant)
            .Select(constant => (
                Name: pdbReader.GetString(constant.Name),
                HexSignature: BitConverter.ToString(pdbReader.GetBlobBytes(constant.Signature))))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectation.Constants.OrderBy(static item => item.Name, StringComparer.Ordinal), constants);
        var inactiveConstants = allScopes
            .Where(scope => !ScopeContains(scope, pause.Offset))
            .SelectMany(static scope => scope.GetLocalConstants())
            .Select(pdbReader.GetLocalConstant)
            .Select(constant => (
                Name: pdbReader.GetString(constant.Name),
                HexSignature: BitConverter.ToString(pdbReader.GetBlobBytes(constant.Signature))))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectation.InactiveConstants.OrderBy(static item => item.Name, StringComparer.Ordinal),
            inactiveConstants);
    }

    private static bool ScopeContains(LocalScope scope, int ilOffset) =>
        ilOffset >= scope.StartOffset && ilOffset < scope.StartOffset + scope.Length;

    private static bool ScopesAreDisjoint(LocalScope left, LocalScope right) =>
        left.StartOffset + left.Length <= right.StartOffset ||
        right.StartOffset + right.Length <= left.StartOffset;

    private static IlInstruction AssertSinglePauseInstruction(
        PeArtifact target,
        MethodDefinitionHandle methodHandle)
    {
        var reader = target.Reader;
        var method = reader.GetMethodDefinition(methodHandle);
        var body = target.PeReader.GetMethodBody(method.RelativeVirtualAddress);
        var formatter = new MetadataSignatureFormatter(reader);
        var il = body.GetILBytes();
        Assert.NotNull(il);
        var matches = ReadInstructions(il)
            .Where(static instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            .Where(instruction => instruction.MetadataToken is int token &&
                IsExactThreadSleep(reader, formatter, MetadataTokens.EntityHandle(token)))
            .ToArray();
        return Assert.Single(matches);
    }

    private static bool IsExactThreadSleep(
        MetadataReader reader,
        MetadataSignatureFormatter formatter,
        EntityHandle handle)
    {
        if (handle.Kind == HandleKind.MethodSpecification)
        {
            handle = reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
        }

        if (handle.Kind != HandleKind.MemberReference)
        {
            return false;
        }

        var member = reader.GetMemberReference((MemberReferenceHandle)handle);
        if (!string.Equals(reader.GetString(member.Name), "Sleep", StringComparison.Ordinal) ||
            !IsNamedType(reader, member.Parent, "System.Threading", "Thread"))
        {
            return false;
        }

        var signature = member.DecodeMethodSignature(formatter, genericContext: null);
        return !signature.Header.IsInstance &&
            signature.GenericParameterCount == 0 &&
            signature.RequiredParameterCount == 1 &&
            signature.ReturnType == "void" &&
            signature.ParameterTypes.SequenceEqual(["int32"]);
    }

    private static ImmutableArray<IlInstruction> ReadInstructions(byte[] bytes)
    {
        var instructions = ImmutableArray.CreateBuilder<IlInstruction>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            var instructionOffset = offset;
            var first = bytes[offset++];
            ushort value = first;
            if (first == 0xFE)
            {
                Assert.True(offset < bytes.Length);
                value = (ushort)(0xFE00 | bytes[offset++]);
            }

            Assert.True(IlOpCodes.TryGetValue(value, out var opCode), $"Unknown IL opcode 0x{value:X4}.");
            var operandStart = offset;
            var operandSize = ReadOperandSize(opCode.OperandType, bytes.AsSpan(), operandStart);
            Assert.InRange(operandSize, 0, bytes.Length - operandStart);
            int? metadataToken = opCode.OperandType is
                OperandType.InlineField or
                OperandType.InlineMethod or
                OperandType.InlineSig or
                OperandType.InlineString or
                OperandType.InlineTok or
                OperandType.InlineType
                ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(operandStart, 4))
                : null;
            instructions.Add(new IlInstruction(instructionOffset, opCode, metadataToken));
            offset += operandSize;
        }

        Assert.Equal(bytes.Length, offset);
        return instructions.ToImmutable();
    }

    private static ImmutableDictionary<ushort, OpCode> CreateIlOpCodes() =>
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(OpCode))
            .Select(static field => (OpCode)field.GetValue(null)!)
            .GroupBy(static opCode => unchecked((ushort)opCode.Value))
            .ToImmutableDictionary(static group => group.Key, static group => group.First());

    private static int ReadOperandSize(OperandType type, ReadOnlySpan<byte> bytes, int offset) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod or
            OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or
            OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => ReadSwitchOperandSize(bytes, offset),
        _ => throw new Xunit.Sdk.XunitException($"Unexpected IL operand type {type}."),
    };

    private static int ReadSwitchOperandSize(ReadOnlySpan<byte> bytes, int offset)
    {
        Assert.True(offset + sizeof(int) <= bytes.Length);
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
        Assert.InRange(count, 0, (bytes.Length - offset - sizeof(int)) / sizeof(int));
        return sizeof(int) + (count * sizeof(int));
    }

    private static ImmutableArray<LocalScope> ReadActiveMethodScopes(
        MetadataReader pdbReader,
        MethodDefinitionHandle methodHandle,
        int ilOffset) =>
        ReadMethodScopes(pdbReader, methodHandle)
            .Where(scope => ilOffset >= scope.StartOffset && ilOffset < scope.StartOffset + scope.Length)
            .ToImmutableArray();

    private static ImmutableArray<LocalScope> ReadMethodScopesOrEmpty(
        MetadataReader pdbReader,
        MethodDefinitionHandle methodHandle) =>
        pdbReader.LocalScopes
            .Select(pdbReader.GetLocalScope)
            .Where(scope => scope.Method == methodHandle)
            .OrderBy(static scope => scope.StartOffset)
            .ThenByDescending(static scope => scope.Length)
            .ToImmutableArray();

    private static string[] ReadParameterNames(
        MetadataReader reader,
        MethodDefinitionHandle methodHandle) =>
        reader.GetMethodDefinition(methodHandle).GetParameters()
            .Select(reader.GetParameter)
            .Where(static parameter => parameter.SequenceNumber > 0)
            .OrderBy(static parameter => parameter.SequenceNumber)
            .Select(parameter => reader.GetString(parameter.Name))
            .ToArray();

    private static string[] ReadReferencedFields(
        PeArtifact target,
        MethodDefinitionHandle methodHandle)
    {
        var reader = target.Reader;
        var method = reader.GetMethodDefinition(methodHandle);
        var body = target.PeReader.GetMethodBody(method.RelativeVirtualAddress);
        var formatter = new MetadataSignatureFormatter(reader);
        var il = body.GetILBytes();
        Assert.NotNull(il);
        return ReadInstructions(il)
            .Where(static instruction => instruction.OpCode.OperandType == OperandType.InlineField)
            .Select(instruction => MetadataTokens.EntityHandle(instruction.MetadataToken!.Value))
            .Select(handle => handle.Kind switch
            {
                HandleKind.FieldDefinition =>
                    $"{FormatTypeDefinitionName(reader, FindDeclaringType(reader, (FieldDefinitionHandle)handle))}::" +
                    reader.GetString(reader.GetFieldDefinition((FieldDefinitionHandle)handle).Name),
                HandleKind.MemberReference =>
                    $"{formatter.Format(reader.GetMemberReference((MemberReferenceHandle)handle).Parent)}::" +
                    reader.GetString(reader.GetMemberReference((MemberReferenceHandle)handle).Name),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected field token kind {handle.Kind}."),
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"Expected the W8 physical fixture at '{path}'.");
        return path;
    }

    private static TypeDefinitionHandle FindTopLevelType(
        MetadataReader reader,
        string namespaceName,
        string typeName) =>
        Assert.Single(reader.TypeDefinitions, handle =>
        {
            var definition = reader.GetTypeDefinition(handle);
            return definition.GetDeclaringType().IsNil &&
                string.Equals(reader.GetString(definition.Namespace), namespaceName, StringComparison.Ordinal) &&
                string.Equals(reader.GetString(definition.Name), typeName, StringComparison.Ordinal);
        });

    private static TypeDefinitionHandle FindNestedType(
        MetadataReader reader,
        TypeDefinitionHandle declaringType,
        string typeName) =>
        Assert.Single(reader.TypeDefinitions, handle =>
        {
            var definition = reader.GetTypeDefinition(handle);
            return definition.GetDeclaringType() == declaringType &&
                string.Equals(reader.GetString(definition.Name), typeName, StringComparison.Ordinal);
        });

    private static MethodDefinitionHandle FindMethod(
        MetadataReader reader,
        string namespaceName,
        string typeName,
        string methodName)
    {
        var type = reader.GetTypeDefinition(FindTopLevelType(reader, namespaceName, typeName));
        return Assert.Single(
            type.GetMethods(),
            handle => string.Equals(
                reader.GetString(reader.GetMethodDefinition(handle).Name),
                methodName,
                StringComparison.Ordinal));
    }

    private static Dictionary<string, FieldDefinitionHandle> ReadFields(
        MetadataReader reader,
        TypeDefinition definition) =>
        definition.GetFields().ToDictionary(
            handle => reader.GetString(reader.GetFieldDefinition(handle).Name),
            StringComparer.Ordinal);

    private static void AssertGenericParameters(
        MetadataReader reader,
        TypeDefinition definition,
        params (int Index, string Name)[] expected)
    {
        var actual = definition.GetGenericParameters()
            .Select(handle => reader.GetGenericParameter(handle))
            .Select(parameter => (parameter.Index, reader.GetString(parameter.Name)))
            .ToArray();
        Assert.Equal(expected, actual);
    }

    private static void AssertFieldSignature(
        MetadataReader reader,
        MetadataSignatureFormatter signatures,
        FieldDefinitionHandle fieldHandle,
        string expected)
    {
        var field = reader.GetFieldDefinition(fieldHandle);
        var raw = reader.GetBlobBytes(field.Signature);
        Assert.NotEmpty(raw);
        Assert.Equal((byte)SignatureKind.Field, (byte)(raw[0] & 0x0F));
        Assert.Equal(expected, signatures.FormatField(fieldHandle));
    }

    private static void AssertStoredStatic(FieldDefinition field)
    {
        Assert.True((field.Attributes & FieldAttributes.Static) != 0);
        Assert.True((field.Attributes & (FieldAttributes.Literal | FieldAttributes.HasFieldRVA)) == 0);
        Assert.True(field.GetDefaultValue().IsNil);
    }

    private static void AssertMetadataLiteral(
        MetadataReader reader,
        FieldDefinitionHandle fieldHandle,
        ConstantTypeCode typeCode,
        byte[] expectedValue)
    {
        var field = reader.GetFieldDefinition(fieldHandle);
        Assert.True((field.Attributes & (FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault)) ==
            (FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault));
        Assert.False((field.Attributes & (FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA)) != 0);
        var constant = reader.GetConstant(field.GetDefaultValue());
        Assert.Equal(typeCode, constant.TypeCode);
        Assert.Equal(expectedValue, reader.GetBlobBytes(constant.Value));
    }

    private static void AssertTypeSpecStartsWithGenericInstantiation(
        MetadataReader reader,
        TypeSpecificationHandle handle)
    {
        var signature = reader.GetBlobBytes(reader.GetTypeSpecification(handle).Signature);
        Assert.NotEmpty(signature);
        Assert.Equal((byte)SignatureTypeCode.GenericTypeInstance, signature[0]);
    }

    private static void AssertDecimalConstantAttribute(
        MetadataReader reader,
        FieldDefinition field,
        decimal expected)
    {
        var attributeHandle = Assert.Single(
            field.GetCustomAttributes(),
            handle => IsNamedAttribute(
                reader,
                handle,
                "System.Runtime.CompilerServices",
                "DecimalConstantAttribute"));
        var attribute = reader.GetCustomAttribute(attributeHandle);
        var blob = reader.GetBlobReader(attribute.Value);
        Assert.Equal(1, blob.ReadUInt16());
        var bits = decimal.GetBits(expected);
        var scale = (byte)((bits[3] >> 16) & 0x7F);
        var sign = (byte)((bits[3] >> 31) & 1);
        Assert.Equal(scale, blob.ReadByte());
        Assert.Equal(sign, blob.ReadByte());
        Assert.Equal(unchecked((uint)bits[2]), blob.ReadUInt32());
        Assert.Equal(unchecked((uint)bits[1]), blob.ReadUInt32());
        Assert.Equal(unchecked((uint)bits[0]), blob.ReadUInt32());
        Assert.Equal(0, blob.ReadUInt16());
        Assert.Equal(0, blob.RemainingBytes);
    }

    private static byte[] ReadRvaPrefix(PEReader peReader, FieldDefinition field, int length)
    {
        var rva = field.GetRelativeVirtualAddress();
        Assert.True(rva > 0);
        var block = peReader.GetSectionData(rva);
        return block.Length < length ? [] : block.GetContent(0, length).ToArray();
    }

    private static RvaGeometry ReadRvaGeometry(
        PeArtifact artifact,
        FieldDefinitionHandle fieldHandle,
        int width)
    {
        var field = artifact.Reader.GetFieldDefinition(fieldHandle);
        var rva = field.GetRelativeVirtualAddress();
        Assert.True(rva > 0);
        var section = Assert.Single(
            artifact.PeReader.PEHeaders.SectionHeaders,
            candidate => rva >= candidate.VirtualAddress &&
                rva + width <= candidate.VirtualAddress + Math.Max(candidate.VirtualSize, candidate.SizeOfRawData));
        var sectionOffset = rva - section.VirtualAddress;
        Assert.InRange(sectionOffset, 0, section.SizeOfRawData - width);
        var fileOffset = section.PointerToRawData + sectionOffset;
        var mappedBytes = artifact.PeReader.GetSectionData(rva).GetContent(0, width).ToArray();
        var completeFile = File.ReadAllBytes(artifact.Path);
        Assert.InRange(fileOffset, 0, completeFile.Length - width);
        var fileBytes = completeFile.AsSpan(fileOffset, width).ToArray();
        return new RvaGeometry(
            rva,
            width,
            section.Name,
            section.PointerToRawData,
            fileOffset,
            mappedBytes,
            fileBytes);
    }

    private static TypeDefinitionHandle FindDeclaringType(
        MetadataReader reader,
        FieldDefinitionHandle fieldHandle) =>
        Assert.Single(
            reader.TypeDefinitions,
            handle => reader.GetTypeDefinition(handle).GetFields().Contains(fieldHandle));

    private static bool HasNamedAttribute(
        MetadataReader reader,
        CustomAttributeHandleCollection attributes,
        string namespaceName,
        string typeName) =>
        attributes.Any(handle => IsNamedAttribute(reader, handle, namespaceName, typeName));

    private static bool IsNamedAttribute(
        MetadataReader reader,
        CustomAttributeHandle handle,
        string namespaceName,
        string typeName)
    {
        var attribute = reader.GetCustomAttribute(handle);
        EntityHandle constructorOwner = attribute.Constructor.Kind switch
        {
            HandleKind.MethodDefinition =>
                reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            _ => default,
        };
        return IsNamedType(reader, constructorOwner, namespaceName, typeName);
    }

    private static bool IsNamedType(
        MetadataReader reader,
        EntityHandle handle,
        string namespaceName,
        string typeName) =>
        handle.Kind switch
        {
            HandleKind.TypeDefinition => IsNamedType(
                reader,
                reader.GetTypeDefinition((TypeDefinitionHandle)handle).Namespace,
                reader.GetTypeDefinition((TypeDefinitionHandle)handle).Name,
                namespaceName,
                typeName),
            HandleKind.TypeReference => IsNamedType(
                reader,
                reader.GetTypeReference((TypeReferenceHandle)handle).Namespace,
                reader.GetTypeReference((TypeReferenceHandle)handle).Name,
                namespaceName,
                typeName),
            _ => false,
        };

    private static bool IsNamedType(
        MetadataReader reader,
        StringHandle namespaceHandle,
        StringHandle nameHandle,
        string namespaceName,
        string typeName) =>
        string.Equals(reader.GetString(namespaceHandle), namespaceName, StringComparison.Ordinal) &&
        string.Equals(reader.GetString(nameHandle), typeName, StringComparison.Ordinal);

    private static ImmutableArray<LocalScope> ReadMethodScopes(
        MetadataReader pdbReader,
        MethodDefinitionHandle methodHandle)
    {
        var scopes = pdbReader.LocalScopes
            .Select(pdbReader.GetLocalScope)
            .Where(scope => scope.Method == methodHandle)
            .OrderBy(static scope => scope.StartOffset)
            .ThenByDescending(static scope => scope.Length)
            .ToImmutableArray();
        Assert.NotEmpty(scopes);
        return scopes;
    }

    private static ImportScopeHandle SelectLeafImportScope(ImmutableArray<LocalScope> scopes) =>
        scopes
            .Where(static scope => !scope.ImportScope.IsNil)
            .OrderBy(static scope => scope.Length)
            .ThenByDescending(static scope => scope.StartOffset)
            .Select(static scope => scope.ImportScope)
            .First();

    private static ImmutableArray<RawImportFact> ReadImportChain(
        MetadataReader pdbReader,
        MetadataReader moduleReader,
        ImportScopeHandle leaf)
    {
        var facts = ImmutableArray.CreateBuilder<RawImportFact>();
        var seen = new HashSet<ImportScopeHandle>();
        var current = leaf;
        var depth = 0;
        while (!current.IsNil)
        {
            Assert.True(depth < MaximumImportScopeDepth);
            Assert.True(seen.Add(current));
            var scope = pdbReader.GetImportScope(current);
            if (!scope.ImportsBlob.IsNil)
            {
                DecodeRawImports(pdbReader, moduleReader, current, depth, scope.ImportsBlob, facts);
            }

            current = scope.Parent;
            depth++;
        }

        Assert.InRange(facts.Count, 1, MaximumImportCount);
        return facts.ToImmutable();
    }

    private static void DecodeRawImports(
        MetadataReader pdbReader,
        MetadataReader moduleReader,
        ImportScopeHandle scopeHandle,
        int depth,
        BlobHandle importsBlob,
        ImmutableArray<RawImportFact>.Builder facts)
    {
        var completePayload = pdbReader.GetBlobBytes(importsBlob);
        var blob = pdbReader.GetBlobReader(importsBlob);
        while (blob.RemainingBytes > 0)
        {
            Assert.True(facts.Count < MaximumImportCount);
            var start = blob.Offset;
            var rawKind = blob.ReadCompressedInteger();
            string? alias = null;
            string? targetNamespace = null;
            AssemblyReferenceHandle targetAssembly = default;
            EntityHandle targetType = default;
            switch (rawKind)
            {
                case 1:
                    targetNamespace = ReadImportString(pdbReader, ref blob);
                    break;
                case 2:
                    targetAssembly = ReadAssemblyReferenceHandle(ref blob);
                    targetNamespace = ReadImportString(pdbReader, ref blob);
                    break;
                case 3:
                    targetType = ReadTypeHandle(ref blob);
                    break;
                case 4:
                    alias = ReadImportString(pdbReader, ref blob);
                    targetNamespace = ReadImportString(pdbReader, ref blob);
                    break;
                case 5:
                    alias = ReadImportString(pdbReader, ref blob);
                    break;
                case 6:
                    alias = ReadImportString(pdbReader, ref blob);
                    targetAssembly = ReadAssemblyReferenceHandle(ref blob);
                    break;
                case 7:
                    alias = ReadImportString(pdbReader, ref blob);
                    targetNamespace = ReadImportString(pdbReader, ref blob);
                    break;
                case 8:
                    alias = ReadImportString(pdbReader, ref blob);
                    targetAssembly = ReadAssemblyReferenceHandle(ref blob);
                    targetNamespace = ReadImportString(pdbReader, ref blob);
                    break;
                case 9:
                    alias = ReadImportString(pdbReader, ref blob);
                    targetType = ReadTypeHandle(ref blob);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"Unexpected Portable-PDB import kind {rawKind}.");
            }

            Assert.True(targetAssembly.IsNil || MetadataTokens.GetRowNumber(targetAssembly) <= moduleReader.AssemblyReferences.Count);
            Assert.True(targetType.IsNil || IsInTypeTableBounds(moduleReader, targetType));
            var payload = completePayload.AsSpan(start, blob.Offset - start).ToArray();
            Assert.NotEmpty(payload);
            Assert.Equal((byte)rawKind, payload[0]);
            facts.Add(new RawImportFact(
                scopeHandle,
                depth,
                rawKind,
                alias,
                targetNamespace,
                targetAssembly,
                targetType,
                payload));
        }

        Assert.Equal(completePayload.Length, blob.Offset);
    }

    private static ImmutableArray<ImportScopeFact> ReadImportScopeChain(
        MetadataReader pdbReader,
        MetadataReader moduleReader,
        ImportScopeHandle leaf)
    {
        var scopes = ImmutableArray.CreateBuilder<ImportScopeFact>();
        var seen = new HashSet<ImportScopeHandle>();
        var current = leaf;
        var depth = 0;
        while (!current.IsNil)
        {
            Assert.True(depth < MaximumImportScopeDepth);
            Assert.True(seen.Add(current));
            var scope = pdbReader.GetImportScope(current);
            var imports = ImmutableArray.CreateBuilder<RawImportFact>();
            var payload = scope.ImportsBlob.IsNil ? [] : pdbReader.GetBlobBytes(scope.ImportsBlob);
            if (!scope.ImportsBlob.IsNil)
            {
                DecodeRawImports(
                    pdbReader,
                    moduleReader,
                    current,
                    depth,
                    scope.ImportsBlob,
                    imports);
            }

            Assert.Equal(payload, imports.SelectMany(static import => import.RawPayload).ToArray());
            scopes.Add(new ImportScopeFact(current, scope.Parent, depth, payload, imports.ToImmutable()));
            current = scope.Parent;
            depth++;
        }

        return scopes.ToImmutable();
    }

    private static string DescribeImport(
        MetadataReader reader,
        RawImportFact import,
        MetadataSignatureFormatter formatter) => import.RawKind switch
        {
            1 => $"1|{import.TargetNamespace}",
            2 => $"2|{ReadAssemblyReferenceName(reader, import.TargetAssembly)}|{import.TargetNamespace}",
            3 => $"3|{formatter.Format(import.TargetType)}",
            4 => $"4|{import.Alias}|{import.TargetNamespace}",
            5 => $"5|{import.Alias}",
            6 => $"6|{import.Alias}|{ReadAssemblyReferenceName(reader, import.TargetAssembly)}",
            7 => $"7|{import.Alias}|{import.TargetNamespace}",
            8 => $"8|{import.Alias}|{ReadAssemblyReferenceName(reader, import.TargetAssembly)}|{import.TargetNamespace}",
            9 => $"9|{import.Alias}|{formatter.Format(import.TargetType)}",
            _ => throw new Xunit.Sdk.XunitException($"Unexpected import kind {import.RawKind}."),
        };

    private static string ReadAssemblyReferenceName(
        MetadataReader reader,
        AssemblyReferenceHandle handle) =>
        reader.GetString(reader.GetAssemblyReference(handle).Name);

    private static string ReadImportString(MetadataReader reader, ref BlobReader blob)
    {
        var offset = blob.ReadCompressedInteger();
        Assert.True(offset >= 0);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(reader.GetBlobBytes(MetadataTokens.BlobHandle(offset)));
    }

    private static AssemblyReferenceHandle ReadAssemblyReferenceHandle(ref BlobReader blob)
    {
        var row = blob.ReadCompressedInteger();
        Assert.True(row > 0);
        return MetadataTokens.AssemblyReferenceHandle(row);
    }

    private static EntityHandle ReadTypeHandle(ref BlobReader blob)
    {
        var coded = blob.ReadCompressedInteger();
        Assert.True(coded > 0);
        var row = coded >>> 2;
        Assert.True(row > 0);
        return (coded & 3) switch
        {
            0 => MetadataTokens.TypeDefinitionHandle(row),
            1 => MetadataTokens.TypeReferenceHandle(row),
            2 => MetadataTokens.TypeSpecificationHandle(row),
            _ => throw new Xunit.Sdk.XunitException("The Portable-PDB type coded index has an unknown tag."),
        };
    }

    private static bool IsInTypeTableBounds(MetadataReader reader, EntityHandle handle) =>
        handle.Kind switch
        {
            HandleKind.TypeDefinition => MetadataTokens.GetRowNumber((TypeDefinitionHandle)handle) <= reader.TypeDefinitions.Count,
            HandleKind.TypeReference => MetadataTokens.GetRowNumber((TypeReferenceHandle)handle) <= reader.TypeReferences.Count,
            HandleKind.TypeSpecification => MetadataTokens.GetRowNumber((TypeSpecificationHandle)handle) <=
                reader.GetTableRowCount(TableIndex.TypeSpec),
            _ => false,
        };

    private static void AssertImportType(
        ImmutableArray<RawImportFact> imports,
        int rawKind,
        string? alias,
        string expectedType,
        MetadataSignatureFormatter signatures)
    {
        var fact = Assert.Single(
            imports,
            candidate => candidate.RawKind == rawKind &&
                string.Equals(candidate.Alias, alias, StringComparison.Ordinal) &&
                !candidate.TargetType.IsNil &&
                string.Equals(signatures.Format(candidate.TargetType), expectedType, StringComparison.Ordinal));
        Assert.False(fact.TargetType.IsNil);
        Assert.Equal(HandleKind.TypeSpecification, fact.TargetType.Kind);
        Assert.Equal(expectedType, signatures.Format(fact.TargetType));
        Assert.NotEmpty(fact.RawPayload);
    }

    private static void AssertNamedLocals(
        PeArtifact target,
        MetadataReader pdbReader,
        MethodDefinitionHandle methodHandle,
        params (string Name, string Type)[] expected)
    {
        var method = target.Reader.GetMethodDefinition(methodHandle);
        var body = target.PeReader.GetMethodBody(method.RelativeVirtualAddress);
        Assert.False(body.LocalSignature.IsNil);
        var formatter = new MetadataSignatureFormatter(target.Reader);
        var localTypes = target.Reader.GetStandaloneSignature(body.LocalSignature)
            .DecodeLocalSignature(formatter, genericContext: null);
        var namedLocals = ReadMethodScopes(pdbReader, methodHandle)
            .SelectMany(static scope => scope.GetLocalVariables())
            .Select(pdbReader.GetLocalVariable)
            .Select(local => (Name: pdbReader.GetString(local.Name), local.Index, local.Attributes))
            .GroupBy(static local => local.Index)
            .Select(static group => group.Single())
            .OrderBy(static local => local.Index)
            .ToArray();
        Assert.All(namedLocals, static local => Assert.Equal(LocalVariableAttributes.None, local.Attributes));
        Assert.All(namedLocals, local => Assert.InRange(local.Index, 0, localTypes.Length - 1));
        var observed = namedLocals.ToDictionary(
            static local => local.Name,
            local => localTypes[local.Index],
            StringComparer.Ordinal);
        foreach (var item in expected)
        {
            Assert.True(observed.TryGetValue(item.Name, out var observedType), $"Missing local '{item.Name}'.");
            Assert.Equal(item.Type, observedType);
        }
    }

    private static byte[] LittleEndian(int value)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
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

    private static byte[] LittleEndian(char value) => LittleEndian(unchecked((short)value));

    private static byte[] LittleEndian(ulong value)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(double value) => LittleEndian(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

    private static byte[] LittleEndian(float value) => LittleEndian(unchecked((uint)BitConverter.SingleToInt32Bits(value)));

    private static StructuredSignatureFact ReadStructuredSignature(
        MetadataReader reader,
        EntityHandle handle)
    {
        var provider = new StructuredSignatureProvider(reader);
        if (handle.Kind != HandleKind.TypeSpecification)
        {
            return handle.Kind switch
            {
                HandleKind.TypeDefinition => new StructuredSignatureFact(
                handle,
                [],
                provider.GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, rawTypeKind: 0)),
                HandleKind.TypeReference => new StructuredSignatureFact(
                handle,
                [],
                provider.GetTypeFromReference(reader, (TypeReferenceHandle)handle, rawTypeKind: 0)),
                _ => throw new Xunit.Sdk.XunitException($"Expected a type token, observed {handle.Kind}."),
            };
        }

        var typeSpecificationHandle = (TypeSpecificationHandle)handle;
        var rawBytes = reader.GetBlobBytes(reader.GetTypeSpecification(typeSpecificationHandle).Signature);
        ValidateBoundedTypeSignature(reader, typeSpecificationHandle, rawBytes);
        return new StructuredSignatureFact(
            handle,
            rawBytes,
            provider.DecodeTypeSpecification(typeSpecificationHandle, genericContext: null));
    }

    private static StructuredSignatureFact ReadStructuredFieldSignature(
        MetadataReader reader,
        FieldDefinitionHandle handle)
    {
        var field = reader.GetFieldDefinition(handle);
        var provider = new StructuredSignatureProvider(reader);
        var rawBytes = reader.GetBlobBytes(field.Signature);
        ValidateBoundedFieldSignature(reader, rawBytes);
        return new StructuredSignatureFact(
            handle,
            rawBytes,
            field.DecodeSignature(provider, genericContext: null));
    }

    private static ExpectedSignatureNode ExpectedNamed(
        MetadataReader reader,
        EntityHandle handle,
        byte rawTypeKind) =>
        new(
            SignatureNodeKind.Named,
            rawTypeKind,
            handle,
            ExpectedScope(reader, handle),
            [],
            null,
            PrimitiveCode: null);

    private static ExpectedSignatureNode ExpectedPrimitive(PrimitiveTypeCode code) =>
        new(
            SignatureNodeKind.Primitive,
            0,
            default,
            string.Empty,
            [],
            null,
            (byte)code);

    private static ExpectedSignatureNode ExpectedGeneric(
        MetadataReader reader,
        EntityHandle head,
        byte rawTypeKind,
        params ExpectedSignatureNode[] arguments) =>
        new(
            SignatureNodeKind.GenericInstantiation,
            rawTypeKind,
            head,
            ExpectedScope(reader, head),
            [.. arguments],
            null,
            PrimitiveCode: null);

    private static ExpectedSignatureNode ExpectedVector(ExpectedSignatureNode element) =>
        new(SignatureNodeKind.Vector, 0, default, string.Empty, [element], new ArrayTopology(1, [], []), null);

    private static ExpectedSignatureNode ExpectedMatrix(ExpectedSignatureNode element) =>
        new(
            SignatureNodeKind.MultiDimensionalArray,
            0,
            default,
            string.Empty,
            [element],
            new ArrayTopology(2, [], [0, 0]),
            null);

    private static string ExpectedScope(MetadataReader reader, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => $"module:{reader.GetGuid(reader.GetModuleDefinition().Mvid):D}",
        HandleKind.TypeReference => ExpectedTypeReferenceScope(reader, (TypeReferenceHandle)handle),
        _ => string.Empty,
    };

    private static string ExpectedTypeReferenceScope(MetadataReader reader, TypeReferenceHandle handle)
    {
        var scopes = ReadBoundedChain(
            reader.GetTypeReference(handle).ResolutionScope,
            current => current.Kind == HandleKind.TypeReference
                ? (true, reader.GetTypeReference((TypeReferenceHandle)current).ResolutionScope)
                : (false, default),
            MetadataTraversalKind.TypeReferenceResolutionScope);
        var terminal = scopes[^1];
        return terminal.Kind switch
        {
            HandleKind.AssemblyReference => FormatExpectedAssemblyScope(
                ReadAssemblyReferenceIdentity(reader, (AssemblyReferenceHandle)terminal)),
            HandleKind.ModuleDefinition => $"module:{reader.GetGuid(reader.GetModuleDefinition().Mvid):D}",
            HandleKind.ModuleReference =>
                $"module-ref:{reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)terminal).Name)}",
            _ => $"{terminal.Kind}:0x{MetadataTokens.GetToken(terminal):X8}",
        };
    }

    private static string FormatExpectedAssemblyScope(AssemblyIdentityFact identity) =>
        $"assembly:{identity.Name}|{identity.Version}|{identity.Culture}|{identity.KeyOrToken}|{identity.Flags}";

    private static void AssertStructuredSignature(
        MetadataReader reader,
        StructuredSignatureFact actual,
        ExpectedSignatureNode expected)
    {
        Assert.Equal(EncodeExpectedSignature(expected), actual.RawBytes);
        AssertStructuredNode(actual.Root, expected);
        Assert.Equal(ExpectedDisplay(reader, expected), actual.Root.Display);
    }

    private static void AssertStructuredNode(SignatureNode actual, ExpectedSignatureNode expected)
    {
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.RawTypeKind, actual.RawTypeKind);
        Assert.Equal(expected.Handle, actual.Handle);
        Assert.Equal(expected.Scope, actual.Scope);
        Assert.Equal(expected.Arguments.Length, actual.TypeArguments.Length);
        if (expected.Array is null)
        {
            Assert.Null(actual.Array);
        }
        else
        {
            Assert.NotNull(actual.Array);
            Assert.Equal(expected.Array.Rank, actual.Array.Rank);
            Assert.Equal(expected.Array.Sizes.ToArray(), actual.Array.Sizes.ToArray());
            Assert.Equal(expected.Array.LowerBounds.ToArray(), actual.Array.LowerBounds.ToArray());
        }

        for (var index = 0; index < expected.Arguments.Length; index++)
        {
            AssertStructuredNode(actual.TypeArguments[index], expected.Arguments[index]);
        }
    }

    private static byte[] EncodeExpectedSignature(ExpectedSignatureNode node)
    {
        var bytes = new List<byte>();
        EncodeExpectedSignature(node, bytes);
        return [.. bytes];
    }

    private static void EncodeExpectedSignature(ExpectedSignatureNode node, List<byte> bytes)
    {
        switch (node.Kind)
        {
            case SignatureNodeKind.Named:
                bytes.Add(node.RawTypeKind);
                WriteCompressedUnsigned(bytes, EncodeTypeDefOrRef(node.Handle));
                return;
            case SignatureNodeKind.Primitive:
                bytes.Add(Assert.IsType<byte>(node.PrimitiveCode));
                return;
            case SignatureNodeKind.GenericInstantiation:
                bytes.Add((byte)SignatureTypeCode.GenericTypeInstance);
                bytes.Add(node.RawTypeKind);
                WriteCompressedUnsigned(bytes, EncodeTypeDefOrRef(node.Handle));
                WriteCompressedUnsigned(bytes, checked((uint)node.Arguments.Length));
                foreach (var argument in node.Arguments)
                {
                    EncodeExpectedSignature(argument, bytes);
                }

                return;
            case SignatureNodeKind.Vector:
                bytes.Add((byte)SignatureTypeCode.SZArray);
                EncodeExpectedSignature(Assert.Single(node.Arguments), bytes);
                return;
            case SignatureNodeKind.MultiDimensionalArray:
                bytes.Add((byte)SignatureTypeCode.Array);
                EncodeExpectedSignature(Assert.Single(node.Arguments), bytes);
                Assert.NotNull(node.Array);
                WriteCompressedUnsigned(bytes, checked((uint)node.Array.Rank));
                WriteCompressedUnsigned(bytes, checked((uint)node.Array.Sizes.Length));
                foreach (var size in node.Array.Sizes)
                {
                    WriteCompressedUnsigned(bytes, checked((uint)size));
                }

                WriteCompressedUnsigned(bytes, checked((uint)node.Array.LowerBounds.Length));
                foreach (var lowerBound in node.Array.LowerBounds)
                {
                    Assert.Equal(0, lowerBound);
                    WriteCompressedUnsigned(bytes, 0);
                }

                return;
            default:
                throw new Xunit.Sdk.XunitException($"No expected-signature encoder exists for {node.Kind}.");
        }
    }

    private static uint EncodeTypeDefOrRef(EntityHandle handle)
    {
        var tag = handle.Kind switch
        {
            HandleKind.TypeDefinition => 0U,
            HandleKind.TypeReference => 1U,
            HandleKind.TypeSpecification => 2U,
            _ => throw new Xunit.Sdk.XunitException($"Expected TypeDefOrRef token, observed {handle.Kind}."),
        };
        return checked(((uint)MetadataTokens.GetRowNumber(handle) << 2) | tag);
    }

    private static void WriteCompressedUnsigned(List<byte> bytes, uint value)
    {
        if (value <= 0x7F)
        {
            bytes.Add((byte)value);
            return;
        }

        if (value <= 0x3FFF)
        {
            bytes.Add((byte)((value >> 8) | 0x80));
            bytes.Add((byte)value);
            return;
        }

        Assert.InRange(value, 0U, 0x1FFFFFFFU);
        bytes.Add((byte)((value >> 24) | 0xC0));
        bytes.Add((byte)(value >> 16));
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    private static byte[] RepeatedPrimitiveGenericSignature(int argumentCount)
    {
        Assert.InRange(argumentCount, 1, MaximumMetadataTraversalNodes);
        var bytes = new List<byte>
        {
            (byte)SignatureTypeCode.GenericTypeInstance,
            (byte)SignatureTypeKind.Class,
        };
        WriteCompressedUnsigned(bytes, EncodeTypeDefOrRef(MetadataTokens.TypeReferenceHandle(1)));
        WriteCompressedUnsigned(bytes, checked((uint)argumentCount));
        bytes.AddRange(Enumerable.Repeat((byte)PrimitiveTypeCode.Int32, argumentCount));
        return [.. bytes];
    }

    private static string ExpectedDisplay(MetadataReader reader, ExpectedSignatureNode node) => node.Kind switch
    {
        SignatureNodeKind.Named => new MetadataSignatureFormatter(reader).Format(node.Handle),
        SignatureNodeKind.Primitive => new MetadataSignatureFormatter(reader).GetPrimitiveType(
            (PrimitiveTypeCode)Assert.IsType<byte>(node.PrimitiveCode)),
        SignatureNodeKind.GenericInstantiation =>
            $"{new MetadataSignatureFormatter(reader).Format(node.Handle)}<" +
            $"{string.Join(',', node.Arguments.Select(argument => ExpectedDisplay(reader, argument)))}>",
        SignatureNodeKind.Vector => $"{ExpectedDisplay(reader, Assert.Single(node.Arguments))}[]",
        SignatureNodeKind.MultiDimensionalArray =>
            $"{ExpectedDisplay(reader, Assert.Single(node.Arguments))}[rank={node.Array!.Rank};" +
            $"sizes={string.Join(',', node.Array.Sizes)};lower={string.Join(',', node.Array.LowerBounds)}]",
        _ => throw new Xunit.Sdk.XunitException($"No expected display formatter exists for {node.Kind}."),
    };

    private static SignatureTraversalFact ValidateBoundedTypeSignature(
        MetadataReader reader,
        TypeSpecificationHandle rootHandle,
        ReadOnlyMemory<byte> signature) =>
        new BoundedSignaturePredecoder(handle => ReadTypeSpecificationBytes(reader, handle))
            .DecodeType(signature, rootHandle);

    private static SignatureTraversalFact ValidateBoundedFieldSignature(
        MetadataReader reader,
        ReadOnlyMemory<byte> signature) =>
        new BoundedSignaturePredecoder(handle => ReadTypeSpecificationBytes(reader, handle))
            .DecodeField(signature);

    private static ReadOnlyMemory<byte> ReadTypeSpecificationBytes(
        MetadataReader reader,
        TypeSpecificationHandle handle)
    {
        var row = MetadataTokens.GetRowNumber(handle);
        if (row <= 0 || row > reader.GetTableRowCount(TableIndex.TypeSpec))
        {
            throw new BadImageFormatException($"TypeSpec row {row} lies outside the table.");
        }

        return reader.GetBlobBytes(reader.GetTypeSpecification(handle).Signature);
    }

    private sealed class BoundedSignaturePredecoder(
        Func<TypeSpecificationHandle, ReadOnlyMemory<byte>> resolveTypeSpecification)
    {
        private readonly HashSet<TypeSpecificationHandle> activeTypeSpecifications = [];
        private int maximumDepth;
        private int nodeCount;

        internal SignatureTraversalFact DecodeType(
            ReadOnlyMemory<byte> signature,
            TypeSpecificationHandle rootHandle = default)
        {
            Assert.Equal(0, nodeCount);
            Assert.Equal(0, maximumDepth);
            if (!rootHandle.IsNil && !activeTypeSpecifications.Add(rootHandle))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Signature traversal revisited TypeSpec token 0x{MetadataTokens.GetToken(rootHandle):X8}.");
            }

            try
            {
                var signatureReader = new RawSignatureReader(signature);
                ParseType(signatureReader, depth: 1, allowVoid: false);
                signatureReader.RequireEnd();
                return new SignatureTraversalFact(maximumDepth, nodeCount);
            }
            finally
            {
                if (!rootHandle.IsNil)
                {
                    Assert.True(activeTypeSpecifications.Remove(rootHandle));
                }
            }
        }

        internal SignatureTraversalFact DecodeField(ReadOnlyMemory<byte> signature)
        {
            Assert.Equal(0, nodeCount);
            Assert.Equal(0, maximumDepth);
            var signatureReader = new RawSignatureReader(signature);
            if (signatureReader.ReadByte() != 0x06)
            {
                throw new BadImageFormatException("Expected a field-signature header.");
            }

            ParseType(signatureReader, depth: 1, allowVoid: false);
            signatureReader.RequireEnd();
            return new SignatureTraversalFact(maximumDepth, nodeCount);
        }

        private void ParseType(RawSignatureReader signature, int depth, bool allowVoid)
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
                    EnterNode(depth);
                    ParseType(signature, checked(depth + 1), allowVoid: false);
                    return;
                case 0x11:
                case 0x12:
                    EnterNode(depth);
                    FollowTypeSpecification(signature.ReadTypeDefOrRef(), checked(depth + 1));
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
                case 0x1D:
                    EnterNode(depth);
                    ParseType(signature, checked(depth + 1), allowVoid: false);
                    return;
                case 0x1F:
                case 0x20:
                    ParseModifiedType(signature, depth);
                    return;
                case 0x45:
                    EnterNode(depth);
                    ParseType(signature, checked(depth + 1), allowVoid: false);
                    return;
                default:
                    throw new BadImageFormatException($"Unsupported signature type code 0x{typeCode:X2}.");
            }
        }

        private void ParseGenericInstantiation(RawSignatureReader signature, int depth)
        {
            EnterNode(depth);
            var headKind = signature.ReadByte();
            if (headKind is not 0x11 and not 0x12)
            {
                throw new BadImageFormatException("A generic-instantiation head must be a class or value type.");
            }

            EnterNode(checked(depth + 1));
            FollowTypeSpecification(signature.ReadTypeDefOrRef(), checked(depth + 2));
            var argumentCount = ReadBoundedCount(signature, "generic argument");
            for (var index = 0; index < argumentCount; index++)
            {
                ParseType(signature, checked(depth + 1), allowVoid: false);
            }
        }

        private void ParseModifiedType(RawSignatureReader signature, int depth)
        {
            EnterNode(depth);
            EnterNode(checked(depth + 1));
            FollowTypeSpecification(signature.ReadTypeDefOrRef(), checked(depth + 2));
            ParseType(signature, checked(depth + 1), allowVoid: false);
        }

        private void ParseMethodSignature(RawSignatureReader signature, int childDepth)
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

        private static void ReadArrayShape(RawSignatureReader signature)
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

        private static int ReadBoundedCount(RawSignatureReader signature, string role)
        {
            var count = signature.ReadCompressedUnsigned();
            if (count > MaximumMetadataTraversalNodes)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Signature {role} count exceeded {MaximumMetadataTraversalNodes}.");
            }

            return checked((int)count);
        }

        private void FollowTypeSpecification(EntityHandle handle, int depth)
        {
            if (handle.Kind != HandleKind.TypeSpecification)
            {
                return;
            }

            var typeSpecification = (TypeSpecificationHandle)handle;
            if (!activeTypeSpecifications.Add(typeSpecification))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Signature traversal revisited TypeSpec token 0x{MetadataTokens.GetToken(typeSpecification):X8}.");
            }

            try
            {
                var nestedReader = new RawSignatureReader(resolveTypeSpecification(typeSpecification));
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
            if (depth > MaximumMetadataTraversalDepth)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Structured signature traversal exceeded depth {MaximumMetadataTraversalDepth}.");
            }

            if (nodeCount >= MaximumMetadataTraversalNodes)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Structured signature traversal exceeded {MaximumMetadataTraversalNodes} nodes.");
            }

            nodeCount++;
            maximumDepth = Math.Max(maximumDepth, depth);
        }
    }

    private sealed class RawSignatureReader(ReadOnlyMemory<byte> bytes)
    {
        private int offset;

        internal byte ReadByte()
        {
            if ((uint)offset >= (uint)bytes.Length)
            {
                throw new BadImageFormatException("A signature ended before its declared structure was complete.");
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
                _ => throw new BadImageFormatException("A TypeDefOrRef signature token has an invalid tag."),
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

    private sealed class StructuredSignatureProvider(MetadataReader reader)
        : ISignatureTypeProvider<SignatureNode, object?>
    {
        private readonly HashSet<TypeSpecificationHandle> activeTypeSpecifications = [];
        private readonly MetadataSignatureFormatter display = new(reader);
        private int typeSpecificationDepth;

        internal SignatureNode DecodeTypeSpecification(
            TypeSpecificationHandle handle,
            object? genericContext)
        {
            var rawBytes = reader.GetBlobBytes(reader.GetTypeSpecification(handle).Signature);
            ValidateBoundedTypeSignature(reader, handle, rawBytes);
            AssertTraversalCanEnter(
                MetadataTraversalKind.StructuredTypeSpecification,
                typeSpecificationDepth,
                activeTypeSpecifications.Count);
            if (!activeTypeSpecifications.Add(handle))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Structured TypeSpec traversal revisited token 0x{MetadataTokens.GetToken(handle):X8}.");
            }

            typeSpecificationDepth++;
            try
            {
                return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
            }
            finally
            {
                typeSpecificationDepth--;
                Assert.True(activeTypeSpecifications.Remove(handle));
            }
        }

        public SignatureNode GetArrayType(SignatureNode elementType, ArrayShape shape) =>
            Create(
                SignatureNodeKind.MultiDimensionalArray,
                $"{elementType.Display}[rank={shape.Rank};sizes={string.Join(',', shape.Sizes)};" +
                    $"lower={string.Join(',', shape.LowerBounds)}]",
                0,
                default,
                string.Empty,
                [elementType],
                new ArrayTopology(shape.Rank, shape.Sizes, shape.LowerBounds));

        public SignatureNode GetByReferenceType(SignatureNode elementType) =>
            Unary(SignatureNodeKind.ByReference, $"{elementType.Display}&", elementType);

        public SignatureNode GetFunctionPointerType(MethodSignature<SignatureNode> signature) =>
            Create(
                SignatureNodeKind.FunctionPointer,
                $"methodptr({string.Join(',', signature.ParameterTypes.Select(static type => type.Display))})->" +
                    signature.ReturnType.Display,
                0,
                default,
                string.Empty,
                signature.ParameterTypes.Insert(0, signature.ReturnType),
                null);

        public SignatureNode GetGenericInstantiation(
            SignatureNode genericType,
            ImmutableArray<SignatureNode> typeArguments) =>
            Create(
                SignatureNodeKind.GenericInstantiation,
                $"{genericType.Display}<{string.Join(',', typeArguments.Select(static type => type.Display))}>",
                genericType.RawTypeKind,
                genericType.Handle,
                genericType.Scope,
                typeArguments,
                null);

        public SignatureNode GetGenericMethodParameter(object? genericContext, int index) =>
            Create(SignatureNodeKind.MethodTypeParameter, $"!!{index}", 0, default, string.Empty, [], null);

        public SignatureNode GetGenericTypeParameter(object? genericContext, int index) =>
            Create(SignatureNodeKind.TypeParameter, $"!{index}", 0, default, string.Empty, [], null);

        public SignatureNode GetModifiedType(
            SignatureNode modifier,
            SignatureNode unmodifiedType,
            bool isRequired) =>
            Create(
                SignatureNodeKind.Modified,
                $"{(isRequired ? "modreq" : "modopt")}({modifier.Display}) {unmodifiedType.Display}",
                unmodifiedType.RawTypeKind,
                unmodifiedType.Handle,
                unmodifiedType.Scope,
                [modifier, unmodifiedType],
                null);

        public SignatureNode GetPinnedType(SignatureNode elementType) =>
            Unary(SignatureNodeKind.Pinned, $"pinned {elementType.Display}", elementType);

        public SignatureNode GetPointerType(SignatureNode elementType) =>
            Unary(SignatureNodeKind.Pointer, $"{elementType.Display}*", elementType);

        public SignatureNode GetPrimitiveType(PrimitiveTypeCode typeCode) =>
            Create(SignatureNodeKind.Primitive, display.GetPrimitiveType(typeCode), 0, default, string.Empty, [], null);

        public SignatureNode GetSZArrayType(SignatureNode elementType) =>
            Create(
                SignatureNodeKind.Vector,
                $"{elementType.Display}[]",
                0,
                default,
                string.Empty,
                [elementType],
                new ArrayTopology(1, [], []));

        public SignatureNode GetTypeFromDefinition(
            MetadataReader metadataReader,
            TypeDefinitionHandle handle,
            byte rawTypeKind) =>
            Create(
                SignatureNodeKind.Named,
                FormatTypeDefinitionName(metadataReader, handle),
                rawTypeKind,
                handle,
                $"module:{metadataReader.GetGuid(metadataReader.GetModuleDefinition().Mvid):D}",
                [],
                null);

        public SignatureNode GetTypeFromReference(
            MetadataReader metadataReader,
            TypeReferenceHandle handle,
            byte rawTypeKind)
        {
            var chain = ReadTypeReferenceChain(metadataReader, handle);
            var outer = metadataReader.GetTypeReference(chain[^1]);
            var namespaceName = metadataReader.GetString(outer.Namespace);
            var nestedName = string.Join(
                '+',
                chain.Reverse().Select(current => metadataReader.GetString(metadataReader.GetTypeReference(current).Name)));
            var typeName = namespaceName.Length == 0 ? nestedName : $"{namespaceName}.{nestedName}";
            return Create(
                SignatureNodeKind.Named,
                typeName,
                rawTypeKind,
                handle,
                DescribeResolutionScope(metadataReader, metadataReader.GetTypeReference(handle).ResolutionScope),
                [],
                null);
        }

        public SignatureNode GetTypeFromSpecification(
            MetadataReader metadataReader,
            object? genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) =>
            DecodeTypeSpecification(handle, genericContext);

        private SignatureNode Unary(
            SignatureNodeKind kind,
            string text,
            SignatureNode element) =>
            Create(kind, text, element.RawTypeKind, element.Handle, element.Scope, [element], null);

        private static string DescribeResolutionScope(MetadataReader metadataReader, EntityHandle scope)
        {
            var chain = ReadBoundedChain(
                scope,
                current => current.Kind == HandleKind.TypeReference
                    ? (true, metadataReader.GetTypeReference((TypeReferenceHandle)current).ResolutionScope)
                    : (false, default),
                MetadataTraversalKind.TypeReferenceResolutionScope);
            var terminal = chain[^1];
            return terminal.Kind switch
            {
                HandleKind.AssemblyReference => DescribeAssemblyReference(
                    metadataReader,
                    (AssemblyReferenceHandle)terminal),
                HandleKind.ModuleDefinition =>
                    $"module:{metadataReader.GetGuid(metadataReader.GetModuleDefinition().Mvid):D}",
                HandleKind.ModuleReference =>
                    $"module-ref:{metadataReader.GetString(metadataReader.GetModuleReference((ModuleReferenceHandle)terminal).Name)}",
                _ => $"{terminal.Kind}:0x{MetadataTokens.GetToken(terminal):X8}",
            };
        }

        private static ImmutableArray<TypeReferenceHandle> ReadTypeReferenceChain(
            MetadataReader metadataReader,
            TypeReferenceHandle handle) =>
            ReadBoundedChain(
                handle,
                current => metadataReader.GetTypeReference(current).ResolutionScope.Kind == HandleKind.TypeReference
                    ? (true, (TypeReferenceHandle)metadataReader.GetTypeReference(current).ResolutionScope)
                    : (false, default),
                MetadataTraversalKind.TypeReferenceResolutionScope);

        private static SignatureNode Create(
            SignatureNodeKind kind,
            string display,
            byte rawTypeKind,
            EntityHandle handle,
            string scope,
            ImmutableArray<SignatureNode> typeArguments,
            ArrayTopology? array)
            => new(
                kind,
                display,
                rawTypeKind,
                handle,
                scope,
                typeArguments,
                array);

        private static string DescribeAssemblyReference(
            MetadataReader metadataReader,
            AssemblyReferenceHandle handle)
        {
            var identity = ReadAssemblyReferenceIdentity(metadataReader, handle);
            return $"assembly:{identity.Name}|{identity.Version}|{identity.Culture}|{identity.KeyOrToken}|{identity.Flags}";
        }
    }

    private sealed class TemporaryArtifactDirectory : IDisposable
    {
        private TemporaryArtifactDirectory(string path)
        {
            Path = path;
        }

        private string Path { get; }

        internal static TemporaryArtifactDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"interpreter-w8-compiler-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryArtifactDirectory(path);
        }

        internal string Resolve(string fileName)
        {
            Assert.Equal(fileName, System.IO.Path.GetFileName(fileName));
            return System.IO.Path.Combine(Path, fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class PeArtifact : IDisposable
    {
        private readonly FileStream stream;

        private PeArtifact(string path, FileStream stream, PEReader peReader)
        {
            Path = path;
            this.stream = stream;
            PeReader = peReader;
            Reader = peReader.GetMetadataReader();
        }

        internal PEReader PeReader { get; }

        internal string Path { get; }

        internal MetadataReader Reader { get; }

        internal static PeArtifact Open(string path)
        {
            var stream = File.OpenRead(path);
            return new PeArtifact(path, stream, new PEReader(stream));
        }

        public void Dispose()
        {
            PeReader.Dispose();
            stream.Dispose();
        }
    }

    private sealed class PdbArtifact : IDisposable
    {
        private readonly FileStream stream;
        private readonly MetadataReaderProvider provider;

        private PdbArtifact(FileStream stream, MetadataReaderProvider provider)
        {
            this.stream = stream;
            this.provider = provider;
            Reader = provider.GetMetadataReader();
        }

        internal MetadataReader Reader { get; }

        internal static PdbArtifact Open(string path)
        {
            var stream = File.OpenRead(path);
            try
            {
                return new PdbArtifact(stream, MetadataReaderProvider.FromPortablePdbStream(stream));
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            provider.Dispose();
            stream.Dispose();
        }
    }

    private sealed class MetadataSignatureFormatter(MetadataReader reader)
        : ISignatureTypeProvider<string, object?>
    {
        private readonly HashSet<TypeSpecificationHandle> activeTypeSpecifications = [];
        private int typeSpecificationDepth;

        internal string Format(FieldDefinitionHandle handle) => FormatField(handle);

        internal string FormatField(FieldDefinitionHandle handle)
        {
            _ = ReadStructuredFieldSignature(reader, handle);
            return reader.GetFieldDefinition(handle).DecodeSignature(this, genericContext: null);
        }

        internal string Format(EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeSpecification)
            {
                _ = ReadStructuredSignature(reader, handle);
            }

            return handle.Kind switch
            {
                HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, rawTypeKind: 0),
                HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, rawTypeKind: 0),
                HandleKind.TypeSpecification => DecodeTypeSpecification(
                    (TypeSpecificationHandle)handle,
                    genericContext: null),
                _ => throw new Xunit.Sdk.XunitException($"Expected a type handle, observed {handle.Kind}."),
            };
        }

        public string GetArrayType(string elementType, ArrayShape shape) =>
            $"{elementType}[rank={shape.Rank};sizes={string.Join(',', shape.Sizes)};" +
            $"lower={string.Join(',', shape.LowerBounds)}]";

        public string GetByReferenceType(string elementType) => $"{elementType}&";

        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            $"methodptr({string.Join(',', signature.ParameterTypes)})->{signature.ReturnType}";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(',', typeArguments)}>";

        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
            $"{(isRequired ? "modreq" : "modopt")}({modifier}) {unmodifiedType}";

        public string GetPinnedType(string elementType) => $"pinned {elementType}";

        public string GetPointerType(string elementType) => $"{elementType}*";

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Byte => "uint8",
            PrimitiveTypeCode.SByte => "int8",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.Int16 => "int16",
            PrimitiveTypeCode.UInt16 => "uint16",
            PrimitiveTypeCode.Int32 => "int32",
            PrimitiveTypeCode.UInt32 => "uint32",
            PrimitiveTypeCode.Int64 => "int64",
            PrimitiveTypeCode.UInt64 => "uint64",
            PrimitiveTypeCode.IntPtr => "native int",
            PrimitiveTypeCode.UIntPtr => "native uint",
            PrimitiveTypeCode.Single => "float32",
            PrimitiveTypeCode.Double => "float64",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.Void => "void",
            PrimitiveTypeCode.TypedReference => "typedref",
            _ => throw new Xunit.Sdk.XunitException($"Unexpected primitive signature code {typeCode}."),
        };

        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        public string GetTypeFromDefinition(
            MetadataReader metadataReader,
            TypeDefinitionHandle handle,
            byte rawTypeKind) =>
            FormatTypeDefinitionName(metadataReader, handle);

        public string GetTypeFromReference(
            MetadataReader metadataReader,
            TypeReferenceHandle handle,
            byte rawTypeKind)
        {
            var chain = ReadBoundedChain(
                handle,
                current => metadataReader.GetTypeReference(current).ResolutionScope.Kind == HandleKind.TypeReference
                    ? (true, (TypeReferenceHandle)metadataReader.GetTypeReference(current).ResolutionScope)
                    : (false, default),
                MetadataTraversalKind.DisplayFormatter);
            var outer = metadataReader.GetTypeReference(chain[^1]);
            var namespaceName = metadataReader.GetString(outer.Namespace);
            var nestedName = string.Join(
                '+',
                chain.Reverse().Select(current => metadataReader.GetString(metadataReader.GetTypeReference(current).Name)));
            return namespaceName.Length == 0 ? nestedName : $"{namespaceName}.{nestedName}";
        }

        public string GetTypeFromSpecification(
            MetadataReader metadataReader,
            object? genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) =>
            DecodeTypeSpecification(handle, genericContext);

        private string DecodeTypeSpecification(TypeSpecificationHandle handle, object? genericContext)
        {
            var rawBytes = reader.GetBlobBytes(reader.GetTypeSpecification(handle).Signature);
            ValidateBoundedTypeSignature(reader, handle, rawBytes);
            AssertTraversalCanEnter(
                MetadataTraversalKind.DisplayFormatter,
                typeSpecificationDepth,
                activeTypeSpecifications.Count);
            if (!activeTypeSpecifications.Add(handle))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Display formatter revisited TypeSpec token 0x{MetadataTokens.GetToken(handle):X8}.");
            }

            typeSpecificationDepth++;
            try
            {
                return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
            }
            finally
            {
                typeSpecificationDepth--;
                Assert.True(activeTypeSpecifications.Remove(handle));
            }
        }
    }

    private sealed record ConstraintExpectation(
        string Owner,
        int Index,
        string Name,
        GenericParameterAttributes Attributes,
        string[] ConstraintTypes,
        string[] CustomAttributes);

    private sealed record LiteralExpectation(
        string Key,
        ConstantTypeCode TypeCode,
        byte[] Value);

    private sealed record NamedRvaExpectation(
        string Name,
        string Signature,
        int Width,
        byte[] Bytes);

    private sealed record RvaGeometry(
        int Rva,
        int Width,
        string SectionName,
        int SectionRawPointer,
        int FileOffset,
        byte[] Bytes,
        byte[] FileBytes);

    private sealed record AssemblyIdentityFact(
        string Name,
        Version Version,
        string Culture,
        string KeyOrToken,
        AssemblyFlags Flags);

    private sealed record FrameExpectation(
        string Profile,
        string Namespace,
        string Type,
        string Method,
        int PauseOffset,
        string[] TypeGenericParameters,
        string[] MethodGenericParameters,
        (string Name, string Type)[] Parameters,
        (string Name, string Type)[] Locals,
        (string Name, string Type)[] InactiveLocals,
        NamedSlotNonReuseExpectation? NamedSlotNonReuse,
        int[] ReusedSlots,
        int[] UnnamedSlots,
        (string Name, string HexSignature)[] Constants,
        (string Name, string HexSignature)[] InactiveConstants);

    private sealed record NamedSlotNonReuseExpectation(
        string InactiveName,
        int InactiveIndex,
        string ActiveName,
        int ActiveIndex);

    private sealed record ScopedLocal(LocalScope Scope, LocalVariable Local);

    private sealed record IlInstruction(int Offset, OpCode OpCode, int? MetadataToken);

    private sealed record ImportScopeFact(
        ImportScopeHandle Handle,
        ImportScopeHandle Parent,
        int Depth,
        byte[] RawPayload,
        ImmutableArray<RawImportFact> Imports);

    private enum MetadataTraversalKind
    {
        DeclaringTypeDefinition,
        TypeReferenceResolutionScope,
        StructuredTypeSpecification,
        DisplayFormatter,
    }

    private enum SignatureNodeKind
    {
        Named,
        Primitive,
        GenericInstantiation,
        TypeParameter,
        MethodTypeParameter,
        Vector,
        MultiDimensionalArray,
        ByReference,
        Pointer,
        Modified,
        Pinned,
        FunctionPointer,
    }

    private sealed record ArrayTopology(
        int Rank,
        ImmutableArray<int> Sizes,
        ImmutableArray<int> LowerBounds);

    private sealed record SignatureNode(
        SignatureNodeKind Kind,
        string Display,
        byte RawTypeKind,
        EntityHandle Handle,
        string Scope,
        ImmutableArray<SignatureNode> TypeArguments,
        ArrayTopology? Array);

    private sealed record StructuredSignatureFact(
        EntityHandle Token,
        byte[] RawBytes,
        SignatureNode Root);

    private sealed record SignatureTraversalFact(
        int MaximumDepth,
        int NodeCount);

    private sealed record ExpectedSignatureNode(
        SignatureNodeKind Kind,
        byte RawTypeKind,
        EntityHandle Handle,
        string Scope,
        ImmutableArray<ExpectedSignatureNode> Arguments,
        ArrayTopology? Array,
        byte? PrimitiveCode);

    private sealed record AliasSignatureExpectation(
        string Alias,
        ExpectedSignatureNode Signature);

    private sealed record RawImportFact(
        ImportScopeHandle ScopeHandle,
        int ScopeDepth,
        int RawKind,
        string? Alias,
        string? TargetNamespace,
        AssemblyReferenceHandle TargetAssembly,
        EntityHandle TargetType,
        byte[] RawPayload);
}
