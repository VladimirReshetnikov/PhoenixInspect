using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the complete authority-joined FieldDef declaration-table draft projection.</summary>
public sealed class W8FieldDefinitionTableCatalogContractTests
{
    private const int ModuleTypeRid = 1;
    private const int OuterTypeRid = 2;
    private const int EqualTypeRid = 3;

    /// <summary>Proves direct and FieldPtr ownership domains both derive authority-issued declaring TypeDefs.</summary>
    /// <param name="usePointers">Whether TypeDef ownership follows the reordered synthetic FieldPtr table.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Fast")]
    public void Exact_catalog_joins_every_FieldDef_to_its_authority_declaring_TypeDef(bool usePointers)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule();
        var scenario = W8CompilerNameMappingContractTests.BuildScenario(usePointers, module: module);
        var observations = ImmutableArray.Create(
            FieldRow(module, 1, (int)(FieldAttributes.Public | FieldAttributes.Static), [0x06, 0x08]),
            FieldRow(module, 2, (int)FieldAttributes.Private, [0x06, 0x0E]));

        var catalog = MetadataFieldDefinitionTableCatalogIdentity.Create(scenario.DefinitionAuthority, observations);

        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, scenario.DefinitionAuthority.ResultKind);
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataFieldDefinitionTableIssue.None, catalog.Issue);
        Assert.Same(scenario.DefinitionAuthority, catalog.DefinitionAuthority);
        Assert.Equal(scenario.SourceEnds, catalog.SourceEnds);
        Assert.Null(catalog.ReachedBound);
        Assert.Null(catalog.RelatedMetadataToken);
        Assert.Equal(0, catalog.ObservedCount);

        Assert.Equal(
            [FieldToken(1), FieldToken(2)],
            catalog.Rows.Select(static row => row.FieldDefinitionToken).ToArray());
        Assert.Equal(
            usePointers
                ? [TypeToken(OuterTypeRid), TypeToken(ModuleTypeRid)]
                : [TypeToken(ModuleTypeRid), TypeToken(OuterTypeRid)],
            catalog.Rows.Select(static row => row.DeclaringTypeDefinitionToken).ToArray());
        Assert.Equal(["SyntheticField1", "SyntheticField2"],
            catalog.Rows.Select(static row => row.Name).ToArray());
        Assert.Equal([true, false], catalog.Rows.Select(static row => row.IsStatic).ToArray());
        Assert.All(catalog.Rows, row => Assert.Equal(scenario.SourceEnds, row.SourceEnds));
        Assert.Equal(observations.Select(static row => row.SignatureBytes.ToArray()),
            catalog.Rows.Select(static row => row.SignatureBytes.ToArray()));

        var moduleType = scenario.DefinitionAuthority.TypeDefinitions[ModuleTypeRid - 1];
        var outerType = scenario.DefinitionAuthority.TypeDefinitions[OuterTypeRid - 1];
        var equalType = scenario.DefinitionAuthority.TypeDefinitions[EqualTypeRid - 1];
        var moduleRows = catalog.RowsForDeclaringTypeOrEmpty(moduleType);
        var outerRows = catalog.RowsForDeclaringTypeOrEmpty(outerType);

        Assert.Equal(moduleType.FieldDefinitionTokens, moduleRows.Select(static row => row.FieldDefinitionToken));
        Assert.Equal(outerType.FieldDefinitionTokens, outerRows.Select(static row => row.FieldDefinitionToken));
        Assert.Equal(usePointers ? FieldToken(2) : FieldToken(1), Assert.Single(moduleRows).FieldDefinitionToken);
        Assert.Equal(usePointers ? FieldToken(1) : FieldToken(2), Assert.Single(outerRows).FieldDefinitionToken);
        Assert.All(moduleRows, row => Assert.Same(moduleType, row.DeclaringTypeDefinition));
        Assert.All(outerRows, row => Assert.Same(outerType, row.DeclaringTypeDefinition));
        Assert.Empty(catalog.RowsForDeclaringTypeOrEmpty(equalType));

        Assert.Same(catalog.Rows[0], catalog.FindRow(FieldToken(1)));
        Assert.Same(catalog.Rows[1], catalog.FindRow(FieldToken(2)));
        Assert.Null(catalog.FindRow(FieldToken(3)));
        Assert.Null(catalog.FindRow(FieldToken(0)));
        Assert.Null(catalog.FindRow(0x0600_0001));

        var foreignAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule(0xC000, 'c')).DefinitionAuthority;
        Assert.Empty(catalog.RowsForDeclaringTypeOrEmpty(
            foreignAuthority.TypeDefinitions[ModuleTypeRid - 1]));
    }

    /// <summary>Proves every physical FieldAttributes flag and all seven accessibility encodings decode exactly.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Physical_FieldAttributes_flags_and_accessibility_decode_exactly()
    {
        var module = CreateFieldModule();
        var authority = BuildFieldAuthority(module, fieldDefinitionRowCount: 8);
        var attributes = new[]
        {
            (int)(FieldAttributes.PrivateScope | FieldAttributes.Static),
            (int)FieldAttributes.Private,
            (int)(FieldAttributes.FamANDAssem | FieldAttributes.Static | FieldAttributes.InitOnly),
            (int)(FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.Literal |
                  FieldAttributes.HasDefault),
            (int)(FieldAttributes.Family | FieldAttributes.Static | FieldAttributes.HasFieldRVA),
            (int)(FieldAttributes.FamORAssem | FieldAttributes.InitOnly),
            (int)(FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.HasDefault),
            (int)FieldAttributes.Public,
        };
        var observations = Enumerable.Range(1, attributes.Length)
            .Select(rowId => FieldRow(module, rowId, attributes[rowId - 1], [0x06, 0x08]))
            .ToImmutableArray();

        var catalog = MetadataFieldDefinitionTableCatalogIdentity.Create(authority, observations);

        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(attributes, catalog.Rows.Select(static row => row.Attributes).ToArray());
        Assert.Equal(
            [
                MetadataFieldAccessibility.CompilerControlled,
                MetadataFieldAccessibility.Private,
                MetadataFieldAccessibility.FamilyAndAssembly,
                MetadataFieldAccessibility.Assembly,
                MetadataFieldAccessibility.Family,
                MetadataFieldAccessibility.FamilyOrAssembly,
                MetadataFieldAccessibility.Public,
                MetadataFieldAccessibility.Public,
            ],
            catalog.Rows.Select(static row => row.DeclaredAccessibility).ToArray());
        Assert.Equal([true, false, true, true, true, false, true, false],
            catalog.Rows.Select(static row => row.IsStatic).ToArray());
        Assert.Equal([false, true, false, false, false, true, false, true],
            catalog.Rows.Select(static row => row.IsInstance).ToArray());
        Assert.Equal([false, false, true, false, false, true, false, false],
            catalog.Rows.Select(static row => row.IsInitOnly).ToArray());
        Assert.Equal([false, false, false, true, false, false, false, false],
            catalog.Rows.Select(static row => row.IsLiteral).ToArray());
        Assert.Equal([false, false, false, false, true, false, false, false],
            catalog.Rows.Select(static row => row.HasFieldRva).ToArray());
        Assert.Equal([false, false, false, true, false, false, true, false],
            catalog.Rows.Select(static row => row.HasDefault).ToArray());

        var moduleType = authority.TypeDefinitions[ModuleTypeRid - 1];
        var holderType = authority.TypeDefinitions[OuterTypeRid - 1];
        Assert.Equal([FieldToken(1)],
            catalog.RowsForDeclaringTypeOrEmpty(moduleType).Select(static row => row.FieldDefinitionToken).ToArray());
        Assert.Equal(
            Enumerable.Range(2, 7).Select(FieldToken),
            catalog.RowsForDeclaringTypeOrEmpty(holderType).Select(static row => row.FieldDefinitionToken));
    }

    /// <summary>Proves source count, RID order, and module identity remain prefix-free typed draft stops.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complete_source_physical_RID_order_and_module_identity_are_required()
    {
        var module = CreateFieldModule();
        var emptyAuthority = BuildFieldAuthority(module, fieldDefinitionRowCount: 0);
        var defaultEmpty = MetadataFieldDefinitionTableCatalogIdentity.Create(emptyAuthority, default);
        var explicitEmpty = MetadataFieldDefinitionTableCatalogIdentity.Create(
            emptyAuthority,
            ImmutableArray<MetadataFieldDefinitionRowObservationIdentity>.Empty);
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, defaultEmpty.ResultKind);
        Assert.Empty(defaultEmpty.Rows);
        Assert.Equal(defaultEmpty, explicitEmpty);
        Assert.Null(defaultEmpty.FindRow(FieldToken(1)));

        var authority = BuildFieldAuthority(module, fieldDefinitionRowCount: 2);
        var first = FieldRow(module, 1, (int)FieldAttributes.Public, [0x06, 0x08]);
        var second = FieldRow(module, 2, (int)FieldAttributes.Private, [0x06, 0x0E]);

        AssertNonExact(
            MetadataFieldDefinitionTableCatalogIdentity.Create(authority, default),
            MetadataFieldDefinitionTableIssue.TableIncomplete,
            observedCount: 0);
        AssertNonExact(
            MetadataFieldDefinitionTableCatalogIdentity.Create(authority, [first]),
            MetadataFieldDefinitionTableIssue.TableIncomplete,
            observedCount: 1);

        var surplus = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            [first, second, FieldRow(module, 3, (int)FieldAttributes.Public, [0x06, 0x08])]);
        AssertInvalid(surplus, MetadataFieldDefinitionTableIssue.TableRowCountConflict);
        Assert.Equal(3, surplus.ObservedCount);

        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(authority, [second, first]),
            MetadataFieldDefinitionTableIssue.PhysicalOrderInvalid,
            FieldToken(2));

        var otherModule = CreateFieldModule(moduleAddress: 0xD000, digestCharacter: 'd');
        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(
                authority,
                [first, FieldRow(otherModule, 2, (int)FieldAttributes.Private, [0x06, 0x0E])]),
            MetadataFieldDefinitionTableIssue.SourceMismatch,
            FieldToken(2));
    }

    /// <summary>Proves the ownership join is complete and that contradictory ownership evidence stops the draft.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ownership_join_requires_exactly_one_declaring_TypeDef_for_every_FieldDef()
    {
        var module = CreateFieldModule();
        var exactAuthority = BuildFieldAuthority(module, fieldDefinitionRowCount: 3);
        var observations = Enumerable.Range(1, 3)
            .Select(rowId => FieldRow(module, rowId, (int)FieldAttributes.Public, [0x06, 0x08]))
            .ToImmutableArray();
        var catalog = MetadataFieldDefinitionTableCatalogIdentity.Create(exactAuthority, observations);

        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(
            exactAuthority.TypeDefinitions.SelectMany(static type => type.FieldDefinitionTokens).OrderBy(static t => t),
            catalog.Rows.Select(static row => row.FieldDefinitionToken));
        Assert.Equal(3, catalog.Rows.Select(static row => row.FieldDefinitionToken).Distinct().Count());
        Assert.All(catalog.Rows, row => Assert.Contains(
            row.FieldDefinitionToken,
            row.DeclaringTypeDefinition.FieldDefinitionTokens));

        // A FieldDef row that no TypeDef field list can reach makes the authority itself contradictory, so the join
        // stops on the prerequisite before any FieldDef-level ownership fact is derived.
        var unownedAuthority = BuildFieldAuthority(
            module,
            fieldDefinitionRowCount: 3,
            moduleTypeFieldListRowId: 2);
        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(unownedAuthority, observations),
            MetadataFieldDefinitionTableIssue.DefinitionAuthorityInvalid,
            unownedAuthority.RelatedMetadataToken);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Invalid, unownedAuthority.ResultKind);
        Assert.Equal(
            MetadataTypeDefinitionTableIssue.FieldListCoverageInvalid,
            unownedAuthority.TypeDefinitionCatalog.Issue);

        // Duplicate FieldPtr ownership is likewise refused upstream and never reaches a derived FieldDef row.
        var duplicateAuthority = BuildFieldAuthority(
            module,
            fieldDefinitionRowCount: 2,
            fieldPointerTargets: [1, 1]);
        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(duplicateAuthority, [observations[0], observations[1]]),
            MetadataFieldDefinitionTableIssue.DefinitionAuthorityInvalid,
            duplicateAuthority.RelatedMetadataToken);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Invalid, duplicateAuthority.ResultKind);
        Assert.Equal(
            MetadataMemberPointerTableIssue.FieldPointerTargetDuplicate,
            duplicateAuthority.TypeDefinitionCatalog.MemberPointerCatalog.Issue);
    }

    /// <summary>Proves non-exact and invalid authority prerequisites propagate without any derived draft prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Definition_authority_prerequisite_stops_are_prefix_free()
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule();
        var observations = ImmutableArray.Create(
            FieldRow(module, 1, (int)FieldAttributes.Public, [0x06, 0x08]),
            FieldRow(module, 2, (int)FieldAttributes.Private, [0x06, 0x0E]));

        var nonExactAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            omitGenericParameterRows: true,
            module: module).DefinitionAuthority;
        var nonExact = MetadataFieldDefinitionTableCatalogIdentity.Create(nonExactAuthority, observations);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.NonExact, nonExactAuthority.ResultKind);
        AssertNonExact(
            nonExact,
            MetadataFieldDefinitionTableIssue.DefinitionAuthorityNonExact,
            nonExactAuthority.ObservedCount);
        Assert.Equal(nonExactAuthority.ReachedBound, nonExact.ReachedBound);
        Assert.Equal(nonExactAuthority.RelatedMetadataToken, nonExact.RelatedMetadataToken);
        Assert.Null(nonExact.FindRow(FieldToken(1)));

        var invalidAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: true,
            invalidModuleName: true,
            module: module).DefinitionAuthority;
        var invalid = MetadataFieldDefinitionTableCatalogIdentity.Create(invalidAuthority, observations);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Invalid, invalidAuthority.ResultKind);
        AssertInvalid(
            invalid,
            MetadataFieldDefinitionTableIssue.DefinitionAuthorityInvalid,
            invalidAuthority.RelatedMetadataToken);
        Assert.Equal(invalidAuthority.ObservedCount, invalid.ObservedCount);
        Assert.NotEqual(nonExact, invalid);

        var overCapAuthority = BuildFieldAuthority(
            CreateFieldModule(),
            fieldDefinitionRowCount: StaticFieldV2Limits.MaximumFieldDefinitionRowCount + 1);
        var overCap = MetadataFieldDefinitionTableCatalogIdentity.Create(overCapAuthority, default);
        AssertNonExact(
            overCap,
            MetadataFieldDefinitionTableIssue.DefinitionAuthorityNonExact,
            StaticFieldV2Limits.MaximumFieldDefinitionRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.FieldDefinitionRowCountBoundName, overCap.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumFieldDefinitionRowCount, overCap.ReachedBound.Value);
    }

    /// <summary>Proves malformed, truncated, trailing, and over-deep signatures expose no derived draft prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Shared_grammar_FieldSig_consumption_is_typed_and_prefix_free()
    {
        var module = CreateFieldModule();
        var authority = BuildFieldAuthority(module, fieldDefinitionRowCount: 2);
        var valid = FieldRow(module, 1, (int)FieldAttributes.Public, [0x06, 0x08]);

        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(
                authority,
                [valid, FieldRow(module, 2, (int)FieldAttributes.Public, [])]),
            MetadataFieldDefinitionTableIssue.SignatureInvalid,
            FieldToken(2));
        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(
                authority,
                [valid, FieldRow(module, 2, (int)FieldAttributes.Public, [0x06])]),
            MetadataFieldDefinitionTableIssue.SignatureInvalid,
            FieldToken(2));
        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(
                authority,
                [valid, FieldRow(module, 2, (int)FieldAttributes.Public, [0x00, 0x08])]),
            MetadataFieldDefinitionTableIssue.SignatureInvalid,
            FieldToken(2));
        AssertInvalid(
            MetadataFieldDefinitionTableCatalogIdentity.Create(
                authority,
                [valid, FieldRow(module, 2, (int)FieldAttributes.Public, [0x06, 0x08, 0x08])]),
            MetadataFieldDefinitionTableIssue.SignatureInvalid,
            FieldToken(2));

        var deep = ImmutableArray.CreateBuilder<byte>();
        deep.Add(0x06);
        deep.AddRange(Enumerable.Repeat((byte)0x1D, StaticFieldV2Limits.MaximumTypeSpecificationDepth));
        deep.Add(0x08);
        var overDeep = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            [valid, FieldRow(module, 2, (int)FieldAttributes.Public, deep.ToImmutable())]);
        AssertNonExact(
            overDeep,
            MetadataFieldDefinitionTableIssue.SignatureBoundReached,
            StaticFieldV2Limits.MaximumTypeSpecificationDepth + 1);
        Assert.Equal(ExpressionV2ContractLimits.TypeSpecificationDepthBoundName, overDeep.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumTypeSpecificationDepth, overDeep.ReachedBound.Value);
        Assert.Equal(FieldToken(2), overDeep.RelatedMetadataToken);

        var exact = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            [valid, FieldRow(module, 2, (int)FieldAttributes.Public, [0x06, 0x1D, 0x0E])]);
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, exact.ResultKind);
        Assert.Equal([0x06, 0x1D, 0x0E], exact.Rows[1].SignatureBytes.ToArray());
    }

    /// <summary>Proves exact catalog replay, canonical digests, and every defensive draft copy boundary.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_replays_canonically_and_copies_every_mutable_boundary()
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule();
        var authority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: true,
            module: module).DefinitionAuthority;
        var signatureBacking = new byte[] { 0x06, 0x08 };
        var first = FieldRow(
            module,
            1,
            (int)(FieldAttributes.Public | FieldAttributes.Static),
            ImmutableCollectionsMarshal.AsImmutableArray(signatureBacking));
        var second = FieldRow(module, 2, (int)FieldAttributes.Private, [0x06, 0x0E]);
        var observationBacking = new[] { first, second };

        var catalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            ImmutableCollectionsMarshal.AsImmutableArray(observationBacking));
        var replayAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: true,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule()).DefinitionAuthority;
        var replay = MetadataFieldDefinitionTableCatalogIdentity.Create(
            replayAuthority,
            [
                FieldRow(
                    W8CompilerNameMappingContractTests.CreateMetadataModule(),
                    1,
                    (int)(FieldAttributes.Public | FieldAttributes.Static),
                    [0x06, 0x08]),
                FieldRow(W8CompilerNameMappingContractTests.CreateMetadataModule(), 2, (int)FieldAttributes.Private, [0x06, 0x0E]),
            ]);
        var originalSha = catalog.Sha256;
        var originalCanonical = catalog.CanonicalBytes;

        signatureBacking[0] = 0x00;
        observationBacking[0] = second;
        var returnedRows = catalog.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[1];
        var returnedSignature = catalog.Rows[0].SignatureBytes;
        ImmutableCollectionsMarshal.AsArray(returnedSignature)![0] = 0x00;
        var returnedObservationSignature = first.SignatureBytes;
        ImmutableCollectionsMarshal.AsArray(returnedObservationSignature)![0] = 0x00;
        var returnedRowBytes = catalog.Rows[0].CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedRowBytes)![0] ^= 0x5A;
        var returnedCatalogBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedCatalogBytes)![0] ^= 0x5A;

        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(FieldToken(1), catalog.Rows[0].FieldDefinitionToken);
        Assert.Equal(0x06, catalog.Rows[0].SignatureBytes[0]);
        Assert.Equal(0x06, first.SignatureBytes[0]);
        Assert.Equal(originalSha, catalog.Sha256);
        Assert.True(originalCanonical.AsSpan().SequenceEqual(catalog.CanonicalBytes.AsSpan()));
        Assert.NotEqual(returnedRowBytes[0], catalog.Rows[0].CanonicalBytes[0]);
        Assert.NotEqual(returnedCatalogBytes[0], catalog.CanonicalBytes[0]);

        Assert.Equal(catalog, replay);
        Assert.Equal(catalog.GetHashCode(), replay.GetHashCode());
        Assert.Equal(catalog.Sha256, replay.Sha256);
        Assert.True(catalog.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        Assert.Equal("acbf1a2ed058309c1f8da68d8371ed52f085757994a0e182a7db05b51d5db78f", originalSha);
    }

    /// <summary>Proves the new public draft surface is closed, guarded, validated, and fully documented.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void FieldDefinition_table_surface_has_draft_XML_and_guarded_exact_issuance()
    {
        var observationType = typeof(MetadataFieldDefinitionRowObservationIdentity);
        var rowType = typeof(MetadataFieldDefinitionTableRowIdentity);
        var catalogType = typeof(MetadataFieldDefinitionTableCatalogIdentity);

        Assert.Null(observationType.GetProperty("DeclaringTypeDefinition"));
        Assert.Null(observationType.GetProperty("DeclaringTypeDefinitionToken"));
        Assert.Empty(rowType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(rowType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(catalogType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(observationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            ["Create"],
            observationType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name).ToArray());
        Assert.Equal(
            ["Create"],
            catalogType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name).ToArray());
        Assert.All(new[] { observationType, rowType, catalogType }, static type => Assert.True(type.IsSealed));
        Assert.Throws<ArgumentException>(() => MetadataFieldDefinitionTableRowIdentity.Create(
            new object(),
            null!,
            null!,
            null!,
            default));
        Assert.False(MetadataFieldDefinitionTableCatalogIdentity.OwnsRowMintCapability(new object()));

        var module = CreateFieldModule();
        Assert.Throws<ArgumentNullException>(() => MetadataFieldDefinitionRowObservationIdentity.Create(
            null!,
            FieldToken(1),
            (int)FieldAttributes.Public,
            "F",
            [0x06, 0x08]));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataFieldDefinitionRowObservationIdentity.Create(
            module,
            0x0600_0001,
            (int)FieldAttributes.Public,
            "F",
            [0x06, 0x08]));
        Assert.Throws<ArgumentException>(() => MetadataFieldDefinitionRowObservationIdentity.Create(
            module,
            FieldToken(1),
            (int)FieldAttributes.Public,
            string.Empty,
            [0x06, 0x08]));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataFieldDefinitionRowObservationIdentity.Create(
            module,
            FieldToken(1),
            ushort.MaxValue + 1,
            "F",
            [0x06, 0x08]));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataFieldDefinitionRowObservationIdentity.Create(
            module,
            FieldToken(1),
            0x0007,
            "F",
            [0x06, 0x08]));
        Assert.Throws<ArgumentException>(() => MetadataFieldDefinitionRowObservationIdentity.Create(
            module,
            FieldToken(1),
            (int)FieldAttributes.Public,
            "F",
            default));
        Assert.Throws<ArgumentException>(() => MetadataFieldDefinitionRowObservationIdentity.Create(
            module,
            FieldToken(1),
            (int)FieldAttributes.Public,
            "F",
            ImmutableArray.CreateRange(
                Enumerable.Repeat((byte)0x08, StaticFieldV2Limits.MaximumTypeSpecificationByteCount + 1))));

        AssertPublicDraftXml(
            typeof(MetadataFieldAccessibility),
            typeof(MetadataFieldDefinitionTableResultKind),
            typeof(MetadataFieldDefinitionTableIssue),
            observationType,
            rowType,
            catalogType);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataFieldDefinitionTableCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
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
        }
    }

    private static void AssertNonExact(
        MetadataFieldDefinitionTableCatalogIdentity result,
        MetadataFieldDefinitionTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataFieldDefinitionTableResultKind.NonExact, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Rows);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static void AssertInvalid(
        MetadataFieldDefinitionTableCatalogIdentity result,
        MetadataFieldDefinitionTableIssue issue,
        int? relatedMetadataToken = null)
    {
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Invalid, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Rows);
        Assert.Null(result.ReachedBound);
        Assert.Equal(relatedMetadataToken, result.RelatedMetadataToken);
    }

    private static MetadataDefinitionAuthorityCatalogIdentity BuildFieldAuthority(
        StaticFieldMetadataModuleIdentity module,
        int fieldDefinitionRowCount,
        int? moduleTypeFieldListRowId = null,
        ImmutableArray<int> fieldPointerTargets = default)
    {
        var pointerRowCount = fieldPointerTargets.IsDefault ? 0 : fieldPointerTargets.Length;
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: 2,
                fieldDefinitionsExamined: fieldDefinitionRowCount,
                typeDefinitionRowCount: 2,
                fieldDefinitionRowCount: fieldDefinitionRowCount,
                fieldPointerRowCount: pointerRowCount));
        var moduleStart = moduleTypeFieldListRowId ?? (fieldDefinitionRowCount == 0 ? 0 : 1);
        var holderStart = fieldDefinitionRowCount == 0 ? 0 : Math.Min(2, fieldDefinitionRowCount + 1);
        var typeRows = ImmutableArray.Create(
            TypeRow(module, ModuleTypeRid, "<Module>", string.Empty, TypeAttributes.NotPublic, moduleStart),
            TypeRow(
                module,
                OuterTypeRid,
                "FieldHolder",
                "Synthetic.FieldDefinitions",
                TypeAttributes.Public,
                holderStart));
        var memberPointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            fieldPointerTargets.IsDefault ? default : FieldPointerRows(module, fieldPointerTargets),
            default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            typeRows,
            memberPointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            ImmutableArray<MetadataGenericParameterRowObservationIdentity>.Empty);
        var methodDefinitions = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            ImmutableArray<MetadataMethodDefinitionRowObservationIdentity>.Empty);
        return MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions);
    }

    private static ImmutableArray<MetadataMemberPointerRowObservationIdentity> FieldPointerRows(
        StaticFieldMetadataModuleIdentity module,
        ImmutableArray<int> targetRowIds)
    {
        var rows = ImmutableArray.CreateBuilder<MetadataMemberPointerRowObservationIdentity>(targetRowIds.Length);
        for (var index = 0; index < targetRowIds.Length; index++)
        {
            rows.Add(MetadataMemberPointerRowObservationIdentity.Create(
                metadataModule: module,
                pointerMetadataToken: 0x0300_0000 | checked(index + 1),
                targetDefinitionMetadataToken: FieldToken(targetRowIds[index])));
        }
        return rows.MoveToImmutable();
    }

    private static MetadataTypeDefinitionRowObservationIdentity TypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        string typeName,
        string namespaceName,
        TypeAttributes attributes,
        int fieldListRowId) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            typeDefinitionToken: TypeToken(rowId),
            fieldListRowId: fieldListRowId,
            methodListRowId: 0,
            namespaceName: namespaceName,
            typeName: typeName,
            typeAttributes: (int)attributes,
            extendsMetadataToken: null);

    private static MetadataFieldDefinitionRowObservationIdentity FieldRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int attributes,
        ImmutableArray<byte> signatureBytes) =>
        MetadataFieldDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            fieldDefinitionToken: FieldToken(rowId),
            attributes: attributes,
            name: $"SyntheticField{rowId}",
            signatureBytes: signatureBytes);

    private static StaticFieldMetadataModuleIdentity CreateFieldModule(
        ulong moduleAddress = 0xE000,
        char digestCharacter = 'e') =>
        W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            "Synthetic.FieldDefinitionCatalog");

    private static int TypeToken(int rowId) => 0x0200_0000 | rowId;

    private static int FieldToken(int rowId) => 0x0400_0000 | rowId;
}
