using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises exact W8 metadata source ends and bounded signature token catalogs with synthetic modules.</summary>
public sealed class W8MetadataSourceCatalogContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves source ends derive only from the exact fact for the same physical module and content.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Source_ends_require_one_matching_exact_module_fact()
    {
        var module = CreateMetadataModule();
        var fact = CreateExactFact(
            module,
            typeDefinitionRows: 13,
            typeReferenceRows: 17,
            typeSpecificationRows: 19,
            fieldDefinitionRows: 23,
            methodDefinitionRows: 29,
            genericParameterRows: 31,
            interfaceImplementationRows: 37,
            genericParameterConstraintRows: 41,
            nestedClassRows: 43,
            fieldPointerRows: 47,
            methodPointerRows: 53);

        var sourceEnds = MetadataSourceEndIdentity.Create(module, fact);

        Assert.True(sourceEnds.SourceEndObserved);
        Assert.Equal(module, sourceEnds.SourceModule);
        Assert.Equal(fact, sourceEnds.SourceModuleFact);
        Assert.Equal(13, sourceEnds.TypeDefinitionRowCount);
        Assert.Equal(17, sourceEnds.TypeReferenceRowCount);
        Assert.Equal(19, sourceEnds.TypeSpecificationRowCount);
        Assert.Equal(23, sourceEnds.FieldDefinitionRowCount);
        Assert.Equal(47, sourceEnds.FieldPointerRowCount);
        Assert.Equal(29, sourceEnds.MethodDefinitionRowCount);
        Assert.Equal(53, sourceEnds.MethodPointerRowCount);
        Assert.Equal(43, sourceEnds.NestedClassRowCount);
        Assert.Equal(31, sourceEnds.GenericParameterRowCount);
        Assert.Equal(37, sourceEnds.InterfaceImplementationRowCount);
        Assert.Equal(41, sourceEnds.GenericParameterConstraintRowCount);

        var canonicalSha = sourceEnds.Sha256;
        var returnedBytes = sourceEnds.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        Assert.Equal(canonicalSha, sourceEnds.Sha256);
        Assert.NotEqual(returnedBytes[0], sourceEnds.CanonicalBytes[0]);

        var partial = StaticFieldModuleSearchFact.Partial(
            module.Module,
            StaticFieldModuleSearchIssue.MetadataPartial,
            typeDefinitionsExamined: 13,
            fieldDefinitionsExamined: 23);
        Assert.Throws<ArgumentException>(() => MetadataSourceEndIdentity.Create(module, partial));

        var otherModule = CreateMetadataModule(moduleAddress: 0x3000, digestCharacter: 'b');
        var otherFact = CreateExactFact(otherModule);
        Assert.Throws<ArgumentException>(() => MetadataSourceEndIdentity.Create(module, otherFact));
    }

    /// <summary>Proves exact token maps canonicalize permutations and reject duplicate, foreign, or out-of-range rows.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_is_a_physical_token_ordered_same_source_map()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeSpecificationRows: 4);
        var first = TypeSpecificationEntry(module, rowId: 1);
        var second = TypeSpecificationEntry(module, rowId: 2);
        var third = TypeSpecificationEntry(module, rowId: 3);

        var forward = MetadataSignatureTokenResolutionCatalog.Create(sourceEnds, [first, second, third]);
        var permuted = MetadataSignatureTokenResolutionCatalog.Create(sourceEnds, [third, first, second]);

        Assert.Equal(MetadataSignatureTokenResolutionCatalogResultKind.Exact, forward.ResultKind);
        Assert.Null(forward.ReachedBound);
        Assert.Equal(0, forward.ObservedCount);
        Assert.Equal(forward, permuted);
        Assert.Equal(forward.Sha256, permuted.Sha256);
        Assert.Equal(
            [0x1B000001, 0x1B000002, 0x1B000003],
            forward.Entries.Select(static entry => entry.SourceMetadataToken).ToArray());

        var returnedEntries = forward.Entries;
        ImmutableCollectionsMarshal.AsArray(returnedEntries)![0] = third;
        Assert.Equal(0x1B000001, forward.Entries[0].SourceMetadataToken);

        Assert.Throws<ArgumentException>(() =>
            MetadataSignatureTokenResolutionCatalog.Create(sourceEnds, [first, first]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataSignatureTokenResolutionCatalog.Create(
                sourceEnds,
                [TypeSpecificationEntry(module, rowId: 5)]));

        var otherModule = CreateMetadataModule(moduleAddress: 0x3000, digestCharacter: 'b');
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataSignatureTokenResolutionCatalog.Create(
                sourceEnds,
                [TypeSpecificationEntry(otherModule, rowId: 1)]));
    }

    /// <summary>Proves an unresolved in-range token is non-exact while a token beyond exact source end is invalid.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Decoder_distinguishes_incomplete_resolution_from_invalid_source_token()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 2,
            typeSpecificationRows: 2);
        var catalog = MetadataSignatureTokenResolutionCatalog.Create(
            sourceEnds,
            ImmutableArray<MetadataSignatureTokenResolutionEntry>.Empty);
        var reference = MetadataTypeSpecificationRowReferenceIdentity.Create(module, 0x1B000001);

        var incomplete = MetadataTypeSignatureDecoder.DecodeTypeSpecification(
            reference,
            [0x12, .. EncodeTypeDefOrRef(0x02000001)],
            catalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, incomplete.Kind);
        Assert.Equal("W8_SIGNATURE_TOKEN_RESOLUTION_INCOMPLETE", incomplete.NonExactCode);
        Assert.Null(incomplete.InvalidCode);
        Assert.NotNull(incomplete.Row);
        Assert.Null(incomplete.Root);
        Assert.Null(incomplete.Certificate);
        Assert.Null(incomplete.ReachedBound);

        var invalid = MetadataTypeSignatureDecoder.DecodeTypeSpecification(
            reference,
            [0x12, .. EncodeTypeDefOrRef(0x02000003)],
            catalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, invalid.Kind);
        Assert.Equal("W8_SIGNATURE_TOKEN_OUT_OF_RANGE", invalid.InvalidCode);
        Assert.Null(invalid.NonExactCode);
        Assert.NotNull(invalid.Row);
        Assert.Null(invalid.Root);
    }

    /// <summary>Proves the token-map cap has exact and cap-plus-one outcomes with no usable non-exact prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Token_catalog_cap_plus_one_retains_no_usable_map_prefix()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeSpecificationRows: StaticFieldV2Limits.MaximumSignatureTokenResolutionCount + 1);
        var entries = Enumerable.Range(
                1,
                StaticFieldV2Limits.MaximumSignatureTokenResolutionCount + 1)
            .Select(rowId => TypeSpecificationEntry(module, rowId))
            .ToImmutableArray();

        var exact = MetadataSignatureTokenResolutionCatalog.Create(
            sourceEnds,
            entries.Take(StaticFieldV2Limits.MaximumSignatureTokenResolutionCount).ToImmutableArray());
        Assert.Equal(MetadataSignatureTokenResolutionCatalogResultKind.Exact, exact.ResultKind);
        Assert.Equal(StaticFieldV2Limits.MaximumSignatureTokenResolutionCount, exact.Entries.Length);

        var over = MetadataSignatureTokenResolutionCatalog.Create(sourceEnds, entries);
        Assert.Equal(MetadataSignatureTokenResolutionCatalogResultKind.NonExact, over.ResultKind);
        Assert.Empty(over.Entries);
        Assert.Equal(
            ExpressionV2ContractLimits.SignatureTokenResolutionCountBoundName,
            over.ReachedBound!.Name);
        Assert.Equal(
            StaticFieldV2Limits.MaximumSignatureTokenResolutionCount + 1,
            over.ObservedCount);

        var reference = MetadataTypeSpecificationRowReferenceIdentity.Create(module, 0x1B000001);
        var decode = MetadataTypeSignatureDecoder.DecodeTypeSpecification(reference, [0x08], over);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, decode.Kind);
        Assert.Null(decode.Row);
        Assert.Null(decode.Root);
        Assert.Equal(over.ReachedBound, decode.ReachedBound);
        Assert.Equal(over.ObservedCount, decode.ObservedCount);

        Assert.Contains(
            StaticFieldV2Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.SignatureTokenResolutionCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.SignatureTokenResolutionCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.InterfaceImplementationRowCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.GenericParameterRowCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName);
        Assert.Contains(
            StaticFieldV2Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.NestedClassRowCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.NestedClassRowCountBoundName);
    }

    /// <summary>Proves new draft source-catalog types document public static and instance methods in emitted XML.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Source_catalog_public_method_surface_has_emitted_draft_documentation()
    {
        var assembly = typeof(MetadataSourceEndIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataSignatureTokenResolutionCatalogResultKind),
            typeof(MetadataSourceEndIdentity),
            typeof(MetadataSignatureTokenResolutionCatalog),
        };

        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.True(typeDocumentation.Value.Contains("draft", StringComparison.OrdinalIgnoreCase));

            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(static method => !method.IsSpecialName))
            {
                var prefix = $"M:{type.FullName}.{method.Name}";
                var methodDocumentation = members.Where(member =>
                    ((string?)member.Attribute("name")) is { } name &&
                    (string.Equals(name, prefix, StringComparison.Ordinal) ||
                     name.StartsWith($"{prefix}(", StringComparison.Ordinal))).ToArray();
                Assert.NotEmpty(methodDocumentation);
                Assert.All(methodDocumentation, static member =>
                    Assert.True(member.Value.Contains("draft", StringComparison.OrdinalIgnoreCase)));
            }
        }
    }

    private static MetadataSignatureTokenResolutionEntry TypeSpecificationEntry(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataSignatureTokenResolutionEntry.TypeSpecification(
            MetadataTypeSpecificationRowReferenceIdentity.Create(module, 0x1B000000 | rowId));

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRows = 16,
        int typeReferenceRows = 16,
        int typeSpecificationRows = 16,
        int fieldDefinitionRows = 16,
        int methodDefinitionRows = 16,
        int genericParameterRows = 16,
        int interfaceImplementationRows = 16,
        int genericParameterConstraintRows = 16,
        int nestedClassRows = 16,
        int fieldPointerRows = 0,
        int methodPointerRows = 0) =>
        MetadataSourceEndIdentity.Create(
            module,
            CreateExactFact(
                module,
                typeDefinitionRows,
                typeReferenceRows,
                typeSpecificationRows,
                fieldDefinitionRows,
                methodDefinitionRows,
                genericParameterRows,
                interfaceImplementationRows,
                genericParameterConstraintRows,
                nestedClassRows,
                fieldPointerRows,
                methodPointerRows));

    private static StaticFieldModuleSearchFact CreateExactFact(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRows = 16,
        int typeReferenceRows = 16,
        int typeSpecificationRows = 16,
        int fieldDefinitionRows = 16,
        int methodDefinitionRows = 16,
        int genericParameterRows = 16,
        int interfaceImplementationRows = 16,
        int genericParameterConstraintRows = 16,
        int nestedClassRows = 16,
        int fieldPointerRows = 0,
        int methodPointerRows = 0) =>
        StaticFieldModuleSearchFact.Exact(
            module.Module,
            module.ModuleContent,
            typeDefinitionsExamined: typeDefinitionRows,
            fieldDefinitionsExamined: fieldDefinitionRows,
            typeDefinitionRowCount: typeDefinitionRows,
            fieldDefinitionRowCount: fieldDefinitionRows,
            typeReferenceRowCount: typeReferenceRows,
            typeSpecificationRowCount: typeSpecificationRows,
            methodDefinitionRowCount: methodDefinitionRows,
            nestedClassRowCount: nestedClassRows,
            interfaceImplementationRowCount: interfaceImplementationRows,
            genericParameterRowCount: genericParameterRows,
            genericParameterConstraintRowCount: genericParameterConstraintRows,
            fieldPointerRowCount: fieldPointerRows,
            methodPointerRowCount: methodPointerRows);

    private static StaticFieldMetadataModuleIdentity CreateMetadataModule(
        ulong moduleAddress = 0x2000,
        char digestCharacter = 'a')
    {
        var module = StaticFieldModuleInstanceIdentity.Create(
            SnapshotDigest,
            sizeof(ulong),
            applicationDomainAddress: 0x1000,
            moduleAddress,
            imageBase: 0x400000 + moduleAddress,
            imageSize: 0x18000);
        var content = ModuleContentIdentity.FromDigest(
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            metadataLength: 24_576,
            new string(digestCharacter, 64));
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            generation: 0,
            $"source-catalog-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.SourceCatalog",
            1,
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

    private static ImmutableArray<byte> EncodeTypeDefOrRef(int metadataToken)
    {
        var rowId = metadataToken & 0x00FF_FFFF;
        var tag = (metadataToken & unchecked((int)0xFF00_0000)) switch
        {
            0x02000000 => 0,
            0x01000000 => 1,
            0x1B000000 => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(metadataToken)),
        };
        var coded = (rowId << 2) | tag;
        Assert.InRange(coded, 1, 0x7F);
        return [(byte)coded];
    }
}
