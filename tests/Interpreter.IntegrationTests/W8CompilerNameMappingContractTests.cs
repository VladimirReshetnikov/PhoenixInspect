using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises definition-authority-bound draft compiler-name mapping with synthetic metadata modules.</summary>
public sealed class W8CompilerNameMappingContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const int ModuleTypeRid = 1;
    private const int OuterTypeRid = 2;
    private const int EqualTypeRid = 3;
    private const int DeltaTypeRid = 4;
    private const int UnderflowTypeRid = 5;
    private const int WideTypeRid = 6;
    private const int WiderTypeRid = 7;
    private const int DroppedNonJoinerTypeRid = 8;
    private const int DroppedJoinerTypeRid = 9;
    private const int KeywordTypeRid = 10;
    private const int EarlierBacktickTypeRid = 11;
    private const int PlainGenericTypeRid = 12;
    private const int MismatchedSuffixTypeRid = 13;

    private static readonly ImmutableArray<TypeShape> TypeShapes =
    [
        new(ModuleTypeRid, string.Empty, "<Module>", TypeAttributes.NotPublic, 0),
        new(OuterTypeRid, "Synthetic.Mapping", "Outer`2", TypeAttributes.Public, 2),
        new(EqualTypeRid, string.Empty, "Equal", TypeAttributes.NestedPublic, 2),
        new(DeltaTypeRid, string.Empty, "Delta`1", TypeAttributes.NestedPublic, 3),
        new(UnderflowTypeRid, string.Empty, "Underflow`1", TypeAttributes.NestedPublic, 1),
        new(WideTypeRid, "Synthetic.Mapping", "WideA`64", TypeAttributes.Public, 64),
        new(WiderTypeRid, "Synthetic.Mapping", "WideB`65", TypeAttributes.Public, 65),
        new(DroppedNonJoinerTypeRid, "Synthetic.Mapping", "A\u200C", TypeAttributes.Public, 0),
        new(DroppedJoinerTypeRid, "Synthetic.Mapping", "A\u200D", TypeAttributes.Public, 0),
        new(KeywordTypeRid, "Synthetic.Mapping", "class", TypeAttributes.Public, 0),
        new(EarlierBacktickTypeRid, "Synthetic.Mapping", "G`1`2", TypeAttributes.Public, 2),
        new(PlainGenericTypeRid, "Synthetic.Mapping", "PlainGeneric", TypeAttributes.Public, 1),
        new(MismatchedSuffixTypeRid, "Synthetic.Mapping", "Mismatch`2", TypeAttributes.Public, 1),
    ];

    /// <summary>
    /// Proves one exact direct or pointer authority produces RID-ordered mappings tied to the exact subject and parent
    /// rows while keeping compiler spelling, source addressability, and evaluator admission independent.
    /// </summary>
    /// <param name="usePointers">Whether TypeDef ownership uses complete reordered FieldPtr and MethodPtr rows.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Fast")]
    public void Exact_catalog_binds_every_mapping_to_its_physical_definition_authority(bool usePointers)
    {
        var scenario = BuildScenario(usePointers);
        var catalog = MetadataCompilerNameMappingCatalogIdentity.Create(scenario.DefinitionAuthority);

        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, scenario.DefinitionAuthority.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingCatalogResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingCatalogIssue.None, catalog.Issue);
        Assert.Same(scenario.DefinitionAuthority, catalog.DefinitionAuthority);
        Assert.Equal(TypeShapes.Length, catalog.Mappings.Length);
        Assert.Equal(
            Enumerable.Range(1, TypeShapes.Length).Select(TypeToken),
            catalog.Mappings.Select(static mapping => mapping.TypeDefinition.TypeDefinitionToken));

        for (var index = 0; index < catalog.Mappings.Length; index++)
        {
            var mapping = catalog.Mappings[index];
            Assert.Same(scenario.DefinitionAuthority.TypeDefinitions[index], mapping.TypeDefinition);
            Assert.Same(mapping.TypeDefinition, mapping.Input.TypeDefinition);
            Assert.Same(mapping.EnclosingTypeDefinition, mapping.Input.EnclosingTypeDefinition);
            Assert.Same(mapping, catalog.CompleteMappingOrDefault(TypeToken(index + 1)));
        }
        Assert.Null(catalog.CompleteMappingOrDefault(MethodToken(1)));
        Assert.Null(catalog.CompleteMappingOrDefault(TypeToken(TypeShapes.Length + 1)));

        var module = Mapping(catalog, ModuleTypeRid);
        Assert.Null(module.EnclosingTypeDefinition);
        Assert.Null(module.Input.EnclosingTotalGenericArity);
        Assert.Equal(0, module.IntroducedGenericArity);
        Assert.False(module.CSharpAddressability!.IsAddressable);

        var outer = Mapping(catalog, OuterTypeRid);
        Assert.Equal(2, outer.IntroducedGenericArity);
        Assert.Equal("Outer", outer.RoslynProjection!.ProjectedSimpleName);
        Assert.True(outer.ClsAritySpelling!.IsCanonical);
        Assert.Null(outer.EnclosingTypeDefinition);

        var equal = Mapping(catalog, EqualTypeRid);
        Assert.Same(scenario.DefinitionAuthority.TypeDefinitions[OuterTypeRid - 1], equal.EnclosingTypeDefinition);
        Assert.Equal(2, equal.Input.TotalGenericArity);
        Assert.Equal(2, equal.Input.EnclosingTotalGenericArity);
        Assert.Equal(0, equal.IntroducedGenericArity);
        Assert.Equal("Equal", equal.RoslynProjection!.ProjectedSimpleName);
        Assert.True(equal.ClsAritySpelling!.IsCanonical);

        var delta = Mapping(catalog, DeltaTypeRid);
        Assert.Same(equal.EnclosingTypeDefinition, delta.EnclosingTypeDefinition);
        Assert.Equal(3, delta.Input.TotalGenericArity);
        Assert.Equal(2, delta.Input.EnclosingTotalGenericArity);
        Assert.Equal(1, delta.IntroducedGenericArity);
        Assert.Equal("Delta", delta.RoslynProjection!.ProjectedSimpleName);
        Assert.True(delta.RoslynProjection.WasTerminalAritySuffixRemoved);

        var underflow = Mapping(catalog, UnderflowTypeRid);
        Assert.Equal(MetadataCompilerNameMappingResultKind.NonExact, underflow.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingIssue.NestedTotalArityUnderflow, underflow.Issue);
        Assert.Same(delta.EnclosingTypeDefinition, underflow.EnclosingTypeDefinition);
        Assert.Equal(1, underflow.Input.TotalGenericArity);
        Assert.Equal(2, underflow.Input.EnclosingTotalGenericArity);
        Assert.Null(underflow.IntroducedGenericArity);
        Assert.Null(underflow.ClsAritySpelling);
        Assert.Null(underflow.RoslynProjection);
        Assert.Null(underflow.CSharpAddressability);
        Assert.Null(underflow.EvaluatorAdmission);

        var wide = Mapping(catalog, WideTypeRid);
        Assert.Equal(MetadataCompilerNameMappingResultKind.Exact, wide.ResultKind);
        Assert.Equal(64, wide.Input.TypeDefinition.GenericParameters.Length);
        Assert.True(wide.EvaluatorAdmission!.IsAdmitted);
        Assert.Equal("WideA", wide.RoslynProjection!.ProjectedSimpleName);

        var wider = Mapping(catalog, WiderTypeRid);
        Assert.Equal(MetadataCompilerNameMappingResultKind.Exact, wider.ResultKind);
        Assert.Equal(65, wider.Input.TypeDefinition.GenericParameters.Length);
        Assert.Equal(65, wider.IntroducedGenericArity);
        Assert.Equal(MetadataEvaluatorGenericArityAdmissionStatus.TotalArityLimitExceeded,
            wider.EvaluatorAdmission!.Status);
        Assert.False(wider.EvaluatorAdmission.IsAdmitted);
        Assert.Equal(64, wider.EvaluatorAdmission.MaximumAdmittedTotalGenericArity);
        Assert.Equal("WideB", wider.RoslynProjection!.ProjectedSimpleName);

        Assert.Equal(wide.Input.CanonicalBytes.Length, wider.Input.CanonicalBytes.Length);
        Assert.Equal(wide.CanonicalBytes.Length, wider.CanonicalBytes.Length);
        Assert.True(wider.TypeDefinition.CanonicalBytes.Length > wide.TypeDefinition.CanonicalBytes.Length);
        Assert.True(wide.Input.CanonicalBytes.Length < wide.TypeDefinition.CanonicalBytes.Length);
        Assert.True(wider.CanonicalBytes.Length < wider.TypeDefinition.CanonicalBytes.Length);
        Assert.Equal(-1, wide.Input.CanonicalBytes.AsSpan().IndexOf(wide.TypeDefinition.CanonicalBytes.AsSpan()));
        Assert.Equal(-1, wider.CanonicalBytes.AsSpan().IndexOf(wider.TypeDefinition.CanonicalBytes.AsSpan()));

        Assert.False(Mapping(catalog, DroppedNonJoinerTypeRid).CSharpAddressability!.IsAddressable);
        Assert.False(Mapping(catalog, DroppedJoinerTypeRid).CSharpAddressability!.IsAddressable);
        Assert.True(SyntaxFacts.IsValidIdentifier("A\u200C"));
        Assert.True(SyntaxFacts.IsValidIdentifier("A\u200D"));

        var keyword = Mapping(catalog, KeywordTypeRid).CSharpAddressability!;
        Assert.True(keyword.IsAddressable);
        Assert.True(keyword.RequiresVerbatimEscape);
        Assert.Equal("@class", keyword.SourceSpelling);

        var earlierBacktick = Mapping(catalog, EarlierBacktickTypeRid);
        Assert.Equal("G`1", earlierBacktick.RoslynProjection!.ProjectedSimpleName);
        Assert.True(earlierBacktick.RoslynProjection.WasTerminalAritySuffixRemoved);
        Assert.False(earlierBacktick.ClsAritySpelling!.IsCanonical);
        Assert.False(earlierBacktick.CSharpAddressability!.IsAddressable);

        var plainGeneric = Mapping(catalog, PlainGenericTypeRid);
        Assert.Equal(1, plainGeneric.IntroducedGenericArity);
        Assert.False(plainGeneric.ClsAritySpelling!.IsCanonical);
        Assert.False(plainGeneric.RoslynProjection!.WasTerminalAritySuffixRemoved);
        Assert.True(plainGeneric.CSharpAddressability!.IsAddressable);

        var mismatched = Mapping(catalog, MismatchedSuffixTypeRid);
        Assert.Equal(1, mismatched.IntroducedGenericArity);
        Assert.False(mismatched.ClsAritySpelling!.IsCanonical);
        Assert.Equal(2, mismatched.ClsAritySpelling.ParsedTerminalArity);
        Assert.Equal("Mismatch`2", mismatched.RoslynProjection!.ProjectedSimpleName);
        Assert.False(mismatched.CSharpAddressability!.IsAddressable);

        Assert.Equal(
            usePointers ? FieldToken(2) : FieldToken(1),
            scenario.DefinitionAuthority.TypeDefinitions[ModuleTypeRid - 1].FieldDefinitionTokens.Single());
        Assert.Equal(
            usePointers ? MethodToken(2) : MethodToken(1),
            scenario.DefinitionAuthority.TypeDefinitions[ModuleTypeRid - 1].MethodDefinitionTokens.Single());
    }

    /// <summary>
    /// Proves non-exact and invalid definition authority propagate without a mapping prefix and replay canonically.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_stops_are_prefix_free_and_replay_for_both_result_families()
    {
        var nonExactAuthority = BuildScenario(usePointers: false, omitGenericParameterRows: true).DefinitionAuthority;
        var nonExactReplayAuthority =
            BuildScenario(usePointers: false, omitGenericParameterRows: true).DefinitionAuthority;
        var nonExact = MetadataCompilerNameMappingCatalogIdentity.Create(nonExactAuthority);
        var nonExactReplay = MetadataCompilerNameMappingCatalogIdentity.Create(nonExactReplayAuthority);

        Assert.Equal(MetadataDefinitionAuthorityResultKind.NonExact, nonExactAuthority.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingCatalogResultKind.NonExact, nonExact.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingCatalogIssue.DefinitionAuthorityNonExact, nonExact.Issue);
        Assert.Empty(nonExact.Mappings);
        Assert.Null(nonExact.CompleteMappingOrDefault(TypeToken(OuterTypeRid)));
        AssertCanonicalReplay(nonExact, nonExactReplay);

        var invalidAuthority = BuildScenario(usePointers: true, invalidModuleName: true).DefinitionAuthority;
        var invalidReplayAuthority = BuildScenario(usePointers: true, invalidModuleName: true).DefinitionAuthority;
        var invalid = MetadataCompilerNameMappingCatalogIdentity.Create(invalidAuthority);
        var invalidReplay = MetadataCompilerNameMappingCatalogIdentity.Create(invalidReplayAuthority);

        Assert.Equal(MetadataDefinitionAuthorityResultKind.Invalid, invalidAuthority.ResultKind);
        Assert.Equal(MetadataDefinitionAuthorityIssue.ModuleTypeNameMismatch, invalidAuthority.Issue);
        Assert.Equal(MetadataCompilerNameMappingCatalogResultKind.Invalid, invalid.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingCatalogIssue.DefinitionAuthorityInvalid, invalid.Issue);
        Assert.Empty(invalid.Mappings);
        Assert.Null(invalid.CompleteMappingOrDefault(TypeToken(OuterTypeRid)));
        AssertCanonicalReplay(invalid, invalidReplay);
        Assert.NotEqual(nonExact, invalid);
        Assert.NotEqual(nonExact.Sha256, invalid.Sha256);
        Assert.Equal("ce8ace4b80b7242db50ae7a3b0b5f0b30d6d56d9c85a2228a8583d264462026b", nonExact.Sha256);
        Assert.Equal("2740c9935109730809ec75ea8e16d390a7c973162725a7b0d5336ae1b286a3b3", invalid.Sha256);
    }

    /// <summary>Proves equal name facts from different physical metadata sources remain distinct authority mappings.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Equal_name_and_arity_facts_do_not_erase_source_lineage()
    {
        var first = MetadataCompilerNameMappingCatalogIdentity.Create(
            BuildScenario(
                usePointers: false,
                module: CreateMetadataModule(moduleAddress: 0xA000, digestCharacter: 'a')).DefinitionAuthority);
        var second = MetadataCompilerNameMappingCatalogIdentity.Create(
            BuildScenario(
                usePointers: false,
                module: CreateMetadataModule(moduleAddress: 0xB000, digestCharacter: 'b')).DefinitionAuthority);
        var firstOuter = Mapping(first, OuterTypeRid);
        var secondOuter = Mapping(second, OuterTypeRid);

        Assert.Equal(firstOuter.Input.RawMetadataName, secondOuter.Input.RawMetadataName);
        Assert.Equal(firstOuter.Input.TotalGenericArity, secondOuter.Input.TotalGenericArity);
        Assert.Equal(
            firstOuter.RoslynProjection!.ProjectedSimpleName,
            secondOuter.RoslynProjection!.ProjectedSimpleName);
        Assert.NotEqual(firstOuter.TypeDefinition.SourceEnds, secondOuter.TypeDefinition.SourceEnds);
        Assert.NotEqual(firstOuter.Input, secondOuter.Input);
        Assert.NotEqual(firstOuter, secondOuter);
        Assert.NotEqual(firstOuter.Sha256, secondOuter.Sha256);
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Proves exact catalog replay, defensive copies, guarded mapping issuance, and removal of scalar identity issuers.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_is_immutable_replayable_and_the_only_mapping_issuer()
    {
        var first = MetadataCompilerNameMappingCatalogIdentity.Create(
            BuildScenario(usePointers: true).DefinitionAuthority);
        var replay = MetadataCompilerNameMappingCatalogIdentity.Create(
            BuildScenario(usePointers: true).DefinitionAuthority);
        var direct = MetadataCompilerNameMappingCatalogIdentity.Create(
            BuildScenario(usePointers: false).DefinitionAuthority);
        var originalBytes = first.CanonicalBytes;
        var originalSha = first.Sha256;

        var returnedMappings = first.Mappings;
        ImmutableCollectionsMarshal.AsArray(returnedMappings)![0] = returnedMappings[^1];
        var returnedBytes = first.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;

        AssertCanonicalReplay(first, replay);
        Assert.Equal(originalSha, first.Sha256);
        Assert.Equal("294dd7041bcdf16a691909e10f932394ab330bf31de479f26d756173cb0243e4", originalSha);
        Assert.True(originalBytes.AsSpan().SequenceEqual(first.CanonicalBytes.AsSpan()));
        Assert.Equal(TypeToken(ModuleTypeRid), first.Mappings[0].TypeDefinition.TypeDefinitionToken);
        Assert.NotEqual(first, direct);

        var delta = Mapping(first, DeltaTypeRid);
        Assert.Throws<ArgumentException>(() => MetadataCompilerNameMappingIdentity.Create(
            new object(),
            delta.TypeDefinition,
            delta.EnclosingTypeDefinition));
        Assert.Throws<ArgumentException>(() => MetadataCompilerNameMappingInputIdentity.Create(
            new object(),
            delta.TypeDefinition,
            delta.EnclosingTypeDefinition));
        Assert.Throws<ArgumentException>(() => MetadataClsAritySpellingIdentity.Create(
            new object(),
            delta.Input,
            1));
        Assert.Throws<ArgumentException>(() => MetadataRoslynNameProjectionIdentity.Create(
            new object(),
            delta.Input,
            1));
        Assert.Throws<ArgumentException>(() => MetadataCSharpSimpleNameAddressabilityIdentity.Create(
            new object(),
            delta.RoslynProjection!));
        Assert.Throws<ArgumentException>(() => MetadataEvaluatorGenericArityAdmissionIdentity.Create(
            new object(),
            delta.Input));
        Assert.False(MetadataCompilerNameMappingCatalogIdentity.OwnsMappingMintCapability(new object()));

        var mappingIssuers = typeof(MetadataCompilerNameMappingIdentity).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .ToArray();
        Assert.DoesNotContain(mappingIssuers, static method => method.Name == "Derive");
        Assert.DoesNotContain(mappingIssuers, static method => method.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(string) ||
            parameter.ParameterType == typeof(int) ||
            parameter.ParameterType == typeof(int?)));
        Assert.Empty(typeof(MetadataCompilerNameMappingIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataCompilerNameMappingCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            ["Create"],
            typeof(MetadataCompilerNameMappingCatalogIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name)
                .ToArray());
    }

    /// <summary>Proves the retained pure Roslyn terminal-arity rule at malformed and numeric boundary spellings.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Pure_terminal_arity_rule_matches_the_pinned_Roslyn_boundaries()
    {
        AssertTerminalArity("G`1", expectedPrefix: "G", expectedArity: 1);
        AssertTerminalArity("G`1`2", expectedPrefix: "G`1", expectedArity: 2);
        AssertTerminalArity("G`32767", expectedPrefix: "G", expectedArity: 32_767);

        AssertNoTerminalArity("G");
        AssertNoTerminalArity("`1");
        AssertNoTerminalArity("G`");
        AssertNoTerminalArity("G`0");
        AssertNoTerminalArity("G`01");
        AssertNoTerminalArity("G`+1");
        AssertNoTerminalArity("G`-1");
        AssertNoTerminalArity("G`١");
        AssertNoTerminalArity("G`32768");
        AssertNoTerminalArity("G`999999");
    }

    /// <summary>Proves every public mapping-catalog draft type and method has emitted XML documentation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Compiler_name_mapping_public_surface_has_draft_XML()
    {
        var assembly = typeof(MetadataCompilerNameMappingCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataCompilerNameMappingResultKind),
            typeof(MetadataCompilerNameMappingIssue),
            typeof(MetadataClsAritySpellingStatus),
            typeof(MetadataRoslynNameProjectionStatus),
            typeof(MetadataCSharpSimpleNameAddressabilityStatus),
            typeof(MetadataEvaluatorGenericArityAdmissionStatus),
            typeof(MetadataCompilerNameMappingCatalogResultKind),
            typeof(MetadataCompilerNameMappingCatalogIssue),
            typeof(MetadataCompilerNameMappingInputIdentity),
            typeof(MetadataClsAritySpellingIdentity),
            typeof(MetadataRoslynNameProjectionIdentity),
            typeof(MetadataCSharpSimpleNameAddressabilityIdentity),
            typeof(MetadataEvaluatorGenericArityAdmissionIdentity),
            typeof(MetadataCompilerNameMappingIdentity),
            typeof(MetadataCompilerNameMappingCatalogIdentity),
        };

        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.Contains("draft", typeDocumentation.Value, StringComparison.OrdinalIgnoreCase);

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
                    Assert.Contains("draft", member.Value, StringComparison.OrdinalIgnoreCase));
            }

            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }
    }

    private static Scenario BuildScenario(
        bool usePointers,
        bool omitGenericParameterRows = false,
        bool invalidModuleName = false,
        StaticFieldMetadataModuleIdentity? module = null)
    {
        module ??= CreateMetadataModule();
        var genericParameterRows = GenericParameterRows(module);
        var sourceEnds = CreateSourceEnds(module, usePointers, genericParameterRows.Length);
        var memberPointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            usePointers ? PointerRows(module, MetadataMemberPointerTableKind.Field, [2, 1]) : default,
            usePointers ? PointerRows(module, MetadataMemberPointerTableKind.Method, [2, 1]) : default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            TypeDefinitionRows(module, invalidModuleName),
            memberPointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            NestedRows(module));
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            omitGenericParameterRows ? default : genericParameterRows);
        var methodDefinitions = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            MethodDefinitionRows(module));
        var definitionAuthority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions);
        return new Scenario(sourceEnds, definitionAuthority);
    }

    private static ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> TypeDefinitionRows(
        StaticFieldMetadataModuleIdentity module,
        bool invalidModuleName)
    {
        var rows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(TypeShapes.Length);
        foreach (var shape in TypeShapes)
        {
            var listStart = shape.RowId switch
            {
                ModuleTypeRid => 1,
                OuterTypeRid => 2,
                _ => 3,
            };
            rows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                metadataModule: module,
                typeDefinitionToken: TypeToken(shape.RowId),
                fieldListRowId: listStart,
                methodListRowId: listStart,
                namespaceName: shape.NamespaceName,
                typeName: invalidModuleName && shape.RowId == ModuleTypeRid ? "NotModule" : shape.TypeName,
                typeAttributes: (int)shape.Attributes,
                extendsMetadataToken: null));
        }
        return rows.ToImmutable();
    }

    private static ImmutableArray<MetadataNestedClassRowObservationIdentity> NestedRows(
        StaticFieldMetadataModuleIdentity module) =>
    [
        NestedRow(module, physicalRowId: 1, EqualTypeRid, OuterTypeRid),
        NestedRow(module, physicalRowId: 2, DeltaTypeRid, OuterTypeRid),
        NestedRow(module, physicalRowId: 3, UnderflowTypeRid, OuterTypeRid),
    ];

    private static MetadataNestedClassRowObservationIdentity NestedRow(
        StaticFieldMetadataModuleIdentity module,
        int physicalRowId,
        int nestedTypeRowId,
        int enclosingTypeRowId) =>
        MetadataNestedClassRowObservationIdentity.Create(
            metadataModule: module,
            nestedClassRowToken: 0x29000000 | physicalRowId,
            nestedTypeDefinitionToken: TypeToken(nestedTypeRowId),
            enclosingTypeDefinitionToken: TypeToken(enclosingTypeRowId));

    private static ImmutableArray<MetadataGenericParameterRowObservationIdentity> GenericParameterRows(
        StaticFieldMetadataModuleIdentity module)
    {
        var rows = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        foreach (var shape in TypeShapes)
        {
            for (var number = 0; number < shape.TotalGenericArity; number++)
            {
                rows.Add(MetadataGenericParameterRowObservationIdentity.Create(
                    metadataModule: module,
                    genericParameterToken: 0x2A000000 | checked(rows.Count + 1),
                    number: number,
                    flags: 0,
                    ownerMetadataToken: TypeToken(shape.RowId),
                    name: $"T{shape.RowId}_{number}"));
            }
        }
        return rows.ToImmutable();
    }

    private static ImmutableArray<MetadataMethodDefinitionRowObservationIdentity> MethodDefinitionRows(
        StaticFieldMetadataModuleIdentity module) =>
    [
        MethodRow(module, 1),
        MethodRow(module, 2),
    ];

    private static MetadataMethodDefinitionRowObservationIdentity MethodRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataMethodDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            methodDefinitionToken: MethodToken(rowId),
            relativeVirtualAddress: 0x2000 + rowId * 0x20,
            implementationAttributes: (int)MethodImplAttributes.NoInlining,
            attributes: (int)(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static),
            name: $"MappingMethod{rowId}",
            signaturePrefixBytes: [0x00, 0x00, 0x01],
            signatureByteCount: 3,
            parameterListRowId: 1);

    private static ImmutableArray<MetadataMemberPointerRowObservationIdentity> PointerRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataMemberPointerTableKind tableKind,
        ImmutableArray<int> targetRowIds)
    {
        var pointerTable = tableKind == MetadataMemberPointerTableKind.Field ? 0x03 : 0x05;
        var definitionTable = tableKind == MetadataMemberPointerTableKind.Field ? 0x04 : 0x06;
        var rows = ImmutableArray.CreateBuilder<MetadataMemberPointerRowObservationIdentity>(targetRowIds.Length);
        for (var index = 0; index < targetRowIds.Length; index++)
        {
            rows.Add(MetadataMemberPointerRowObservationIdentity.Create(
                metadataModule: module,
                pointerMetadataToken: pointerTable << 24 | checked(index + 1),
                targetDefinitionMetadataToken: definitionTable << 24 | targetRowIds[index]));
        }
        return rows.MoveToImmutable();
    }

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        bool usePointers,
        int genericParameterRowCount) =>
        MetadataSourceEndIdentity.Create(
            sourceModule: module,
            sourceModuleFact: StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: TypeShapes.Length,
                fieldDefinitionsExamined: 2,
                typeDefinitionRowCount: TypeShapes.Length,
                fieldDefinitionRowCount: 2,
                typeReferenceRowCount: 0,
                typeSpecificationRowCount: 0,
                assemblyReferenceRowCount: 0,
                methodDefinitionRowCount: 2,
                parameterDefinitionRowCount: 0,
                propertyDefinitionRowCount: 0,
                eventDefinitionRowCount: 0,
                moduleDefinitionRowCount: 1,
                assemblyDefinitionRowCount: 1,
                interfaceImplementationRowCount: 0,
                memberReferenceRowCount: 0,
                customAttributeRowCount: 0,
                moduleReferenceRowCount: 0,
                fileRowCount: 0,
                exportedTypeRowCount: 0,
                nestedClassRowCount: 3,
                genericParameterRowCount: genericParameterRowCount,
                genericParameterConstraintRowCount: 0,
                fieldPointerRowCount: usePointers ? 2 : 0,
                methodPointerRowCount: usePointers ? 2 : 0,
                parameterPointerRowCount: 0));

    private static StaticFieldMetadataModuleIdentity CreateMetadataModule(
        ulong moduleAddress = 0xA000,
        char digestCharacter = 'a')
    {
        var module = StaticFieldModuleInstanceIdentity.Create(
            SnapshotDigest,
            sizeof(ulong),
            applicationDomainAddress: 0x1000,
            moduleAddress: moduleAddress,
            imageBase: 0x400000 + moduleAddress,
            imageSize: 0x20000);
        var content = ModuleContentIdentity.FromDigest(
            mvid: Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"),
            metadataLength: 32_768,
            metadataSha256: new string(digestCharacter, 64));
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            generation: 0,
            name: $"compiler-name-mapping-{moduleAddress:x}.dll",
            mvid: content.Mvid,
            encId: Guid.Empty,
            encBaseId: Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            name: "Synthetic.CompilerNameMapping",
            majorVersion: 1,
            minorVersion: 0,
            buildNumber: 0,
            revisionNumber: 0,
            culture: string.Empty,
            flags: 0,
            hashAlgorithm: 0x8004,
            publicKey: ImmutableArray<byte>.Empty);
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

    private static MetadataCompilerNameMappingIdentity Mapping(
        MetadataCompilerNameMappingCatalogIdentity catalog,
        int typeDefinitionRowId) =>
        Assert.IsType<MetadataCompilerNameMappingIdentity>(
            catalog.CompleteMappingOrDefault(TypeToken(typeDefinitionRowId)));

    private static void AssertCanonicalReplay(
        MetadataCompilerNameMappingCatalogIdentity first,
        MetadataCompilerNameMappingCatalogIdentity replay)
    {
        Assert.Equal(first, replay);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
    }

    private static void AssertTerminalArity(
        string rawMetadataName,
        string expectedPrefix,
        int expectedArity)
    {
        Assert.True(MetadataCompilerNameMappingRules.TryParseTerminalArity(
            rawMetadataName,
            out var prefix,
            out var arity));
        Assert.Equal(expectedPrefix, prefix);
        Assert.Equal(expectedArity, arity);
    }

    private static void AssertNoTerminalArity(string rawMetadataName)
    {
        Assert.False(MetadataCompilerNameMappingRules.TryParseTerminalArity(
            rawMetadataName,
            out var prefix,
            out var arity));
        Assert.Equal(string.Empty, prefix);
        Assert.Equal(0, arity);
    }

    private static int TypeToken(int rowId) => 0x02000000 | rowId;

    private static int FieldToken(int rowId) => 0x04000000 | rowId;

    private static int MethodToken(int rowId) => 0x06000000 | rowId;

    private readonly record struct TypeShape(
        int RowId,
        string NamespaceName,
        string TypeName,
        TypeAttributes Attributes,
        int TotalGenericArity);

    private sealed record Scenario(
        MetadataSourceEndIdentity SourceEnds,
        MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority);
}
