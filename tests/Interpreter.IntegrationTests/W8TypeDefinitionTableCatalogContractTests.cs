using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises the complete source-anchored TypeDef-table foundation and derived member intervals with synthetic tables.
/// This checkpoint does not claim that the legacy final TypeDef identities already require the new catalog.
/// </summary>
public sealed class W8TypeDefinitionTableCatalogContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves a complex exact table derives leading-null, repeated, non-null, and terminal intervals.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_derives_every_member_interval_from_complete_physical_order()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 5,
            typeReferenceRows: 2,
            typeSpecificationRows: 3,
            fieldDefinitionRows: 7,
            methodDefinitionRows: 5);
        var observations = ImmutableArray.Create(
            Row(module, 1, fieldStart: 0, methodStart: 0, "Synthetic", "ZeroPrefix", extendsToken: null),
            Row(module, 2, fieldStart: 1, methodStart: 1, "Synthetic", "", 0x02000001),
            Row(module, 3, fieldStart: 1, methodStart: 3, "Synthetic", "OwnsFirst", 0x01000002),
            Row(module, 4, fieldStart: 5, methodStart: 3, "Synthetic", "OwnsSecond", 0x1B000003),
            Row(module, 5, fieldStart: 8, methodStart: 6, "", "TerminalEmpty", extendsToken: null));

        var catalog = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, observations);
        var replay = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, observations);

        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableIssue.None, catalog.Issue);
        Assert.Equal(sourceEnds, catalog.SourceEnds);
        Assert.Null(catalog.ReachedBound);
        Assert.Equal(0, catalog.ObservedCount);
        Assert.Equal(catalog, replay);
        Assert.Equal(catalog.Sha256, replay.Sha256);
        Assert.Equal(
            [(1, 1), (1, 1), (1, 5), (5, 8), (8, 8)],
            catalog.Rows.Select(static row => (row.FieldListRowId, row.FieldListEndExclusiveRowId)).ToArray());
        Assert.Equal(
            [(1, 1), (1, 3), (3, 3), (3, 6), (6, 6)],
            catalog.Rows.Select(static row => (row.MethodListRowId, row.MethodListEndExclusiveRowId)).ToArray());
        Assert.Equal(observations.ToArray(), catalog.Rows.Select(static row => row.Observation).ToArray());
        Assert.All(catalog.Rows, row => Assert.Equal(sourceEnds, row.SourceEnds));

        var returnedRows = catalog.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        Assert.Equal(0x02000001, catalog.Rows[0].TypeDefinitionToken);

        var canonicalSha = catalog.Sha256;
        var returnedBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        Assert.Equal(canonicalSha, catalog.Sha256);
        Assert.NotEqual(returnedBytes[0], catalog.CanonicalBytes[0]);
    }

    /// <summary>Proves empty member tables admit arbitrary physical null and canonical-end mixtures as empty.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Empty_member_tables_normalize_null_prefixes_to_exact_empty_ranges()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 4,
            fieldDefinitionRows: 0,
            methodDefinitionRows: 0);
        var mixed = ImmutableArray.Create(
            Row(module, 1, 0, 0),
            Row(module, 2, 0, 0),
            Row(module, 3, 1, 1),
            Row(module, 4, 1, 1));
        var allNull = ImmutableArray.Create(
            Row(module, 1, 0, 0),
            Row(module, 2, 0, 0),
            Row(module, 3, 0, 0),
            Row(module, 4, 0, 0));
        var allCanonical = ImmutableArray.Create(
            Row(module, 1, 1, 1),
            Row(module, 2, 1, 1),
            Row(module, 3, 1, 1),
            Row(module, 4, 1, 1));
        var alternating = ImmutableArray.Create(
            Row(module, 1, 1, 0),
            Row(module, 2, 0, 1),
            Row(module, 3, 1, 0),
            Row(module, 4, 0, 1));

        foreach (var observations in new[] { mixed, allNull, allCanonical, alternating })
        {
            var catalog = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, observations);
            Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, catalog.ResultKind);
            Assert.All(catalog.Rows, static row =>
            {
                Assert.Equal((1, 1), (row.FieldListRowId, row.FieldListEndExclusiveRowId));
                Assert.Equal((1, 1), (row.MethodListRowId, row.MethodListEndExclusiveRowId));
            });
        }
    }

    /// <summary>Proves a null suffix ends the preceding run and represents empty ownership at the table end.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Null_suffixes_consume_the_member_table_tail_and_normalize_to_empty_end_intervals()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 4,
            fieldDefinitionRows: 4,
            methodDefinitionRows: 4);
        var observations = ImmutableArray.Create(
            Row(module, 1, fieldStart: 0, methodStart: 1),
            Row(module, 2, fieldStart: 1, methodStart: 2),
            Row(module, 3, fieldStart: 0, methodStart: 0),
            Row(module, 4, fieldStart: 5, methodStart: 5));

        var catalog = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, observations);

        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(
            [(1, 1), (1, 5), (5, 5), (5, 5)],
            catalog.Rows.Select(static row => (row.FieldListRowId, row.FieldListEndExclusiveRowId)).ToArray());
        Assert.Equal(
            [(1, 2), (2, 5), (5, 5), (5, 5)],
            catalog.Rows.Select(static row => (row.MethodListRowId, row.MethodListEndExclusiveRowId)).ToArray());
    }

    /// <summary>Proves a final null list entry represents the unencodable end sentinel at the 16-bit width boundary.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Null_final_entries_encode_exact_Field_and_Method_tail_intervals_at_0xFFFF_rows()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 2,
            fieldDefinitionRows: 0xFFFF,
            methodDefinitionRows: 0xFFFF);
        var observations = ImmutableArray.Create(
            Row(module, 1, fieldStart: 1, methodStart: 1),
            Row(module, 2, fieldStart: 0, methodStart: 0));

        var catalog = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, observations);

        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(
            [(1, 0x1_0000), (0x1_0000, 0x1_0000)],
            catalog.Rows.Select(static row => (row.FieldListRowId, row.FieldListEndExclusiveRowId)).ToArray());
        Assert.Equal(
            [(1, 0x1_0000), (0x1_0000, 0x1_0000)],
            catalog.Rows.Select(static row => (row.MethodListRowId, row.MethodListEndExclusiveRowId)).ToArray());
    }

    /// <summary>Proves incomplete, surplus, permuted, foreign, and over-bound table claims expose no rows.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Coverage_stops_and_table_contradictions_are_prefix_free()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 3,
            typeReferenceRows: 1,
            fieldDefinitionRows: 3,
            methodDefinitionRows: 3);
        var first = Row(module, 1, 1, 1);
        var second = Row(module, 2, 2, 2);
        var third = Row(module, 3, 4, 4);

        var unavailable = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            default);
        AssertNonExact(unavailable, MetadataTypeDefinitionTableIssue.TableIncomplete, observedCount: 0);

        var stopped = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, [first, second]);
        AssertNonExact(stopped, MetadataTypeDefinitionTableIssue.TableIncomplete, observedCount: 2);

        var surplus = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [first, second, third, Row(module, 4, 4, 4)]);
        AssertInvalid(surplus, MetadataTypeDefinitionTableIssue.TableRowCountConflict, observedCount: 4);

        var permuted = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, [second, first, third]);
        AssertInvalid(permuted, MetadataTypeDefinitionTableIssue.PhysicalOrderInvalid, observedCount: 3);

        var otherModule = CreateMetadataModule(moduleAddress: 0x3000, digestCharacter: 'b');
        var foreign = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [first, Row(otherModule, 2, 2, 2), third]);
        AssertInvalid(foreign, MetadataTypeDefinitionTableIssue.SourceModuleMismatch, observedCount: 3);

        var badBase = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [first, Row(module, 2, 2, 2, extendsToken: 0x01000002), third]);
        AssertInvalid(badBase, MetadataTypeDefinitionTableIssue.ExtendsTokenOutOfRange, observedCount: 3);

        var overEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: StaticFieldV2Limits.MaximumTypeDefinitionRowCount + 1,
            fieldDefinitionRows: 0,
            methodDefinitionRows: 0);
        var over = MetadataTypeDefinitionTableCatalogIdentity.Create(overEnds, default);
        AssertNonExact(
            over,
            MetadataTypeDefinitionTableIssue.TableRowBoundReached,
            StaticFieldV2Limits.MaximumTypeDefinitionRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.TypeDefinitionRowCountBoundName, over.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumTypeDefinitionRowCount, over.ReachedBound.Value);

        var overFields = MetadataTypeDefinitionTableCatalogIdentity.Create(
            CreateSourceEnds(
                module,
                typeDefinitionRows: 1,
                fieldDefinitionRows: StaticFieldV2Limits.MaximumFieldDefinitionRowCount + 1),
            default);
        AssertNonExact(
            overFields,
            MetadataTypeDefinitionTableIssue.FieldDefinitionRowBoundReached,
            StaticFieldV2Limits.MaximumFieldDefinitionRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.FieldDefinitionRowCountBoundName, overFields.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumFieldDefinitionRowCount, overFields.ReachedBound.Value);

        var overMethods = MetadataTypeDefinitionTableCatalogIdentity.Create(
            CreateSourceEnds(
                module,
                typeDefinitionRows: 1,
                methodDefinitionRows: StaticFieldV2Limits.MaximumMethodDefinitionRowCount + 1),
            default);
        AssertNonExact(
            overMethods,
            MetadataTypeDefinitionTableIssue.MethodDefinitionRowBoundReached,
            StaticFieldV2Limits.MaximumMethodDefinitionRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.MethodDefinitionRowCountBoundName, overMethods.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumMethodDefinitionRowCount, overMethods.ReachedBound.Value);
    }

    /// <summary>Proves the two-argument factory remains direct-only and never invents pointer identity mappings.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Two_argument_factory_is_exact_only_for_direct_member_list_domains()
    {
        var module = CreateMetadataModule();
        var observations = ImmutableArray.Create(
            Row(module, 1, 0, 0),
            Row(module, 2, 1, 1),
            Row(module, 3, 5, 5));
        var direct = MetadataTypeDefinitionTableCatalogIdentity.Create(
            CreateSourceEnds(
                module,
                typeDefinitionRows: 3,
                fieldDefinitionRows: 4,
                methodDefinitionRows: 4),
            observations);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, direct.ResultKind);

        foreach (var (fieldPointerCount, methodPointerCount) in new[] { (4, 0), (0, 4), (4, 4) })
        {
            var sourceEnds = CreateSourceEnds(
                module,
                typeDefinitionRows: 3,
                fieldDefinitionRows: 4,
                methodDefinitionRows: 4,
                fieldPointerRows: fieldPointerCount,
                methodPointerRows: methodPointerCount);
            var result = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, observations);

            Assert.Equal(fieldPointerCount, sourceEnds.FieldPointerRowCount);
            Assert.Equal(methodPointerCount, sourceEnds.MethodPointerRowCount);
            AssertNonExact(
                result,
                MetadataTypeDefinitionTableIssue.MemberPointerCatalogNonExact,
                observedCount: 0);
            Assert.Null(result.ReachedBound);
        }
    }

    /// <summary>Proves list derivation rejects out-of-range, decreasing, orphaned, and entirely null ownership.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Member_list_ownership_requires_complete_unambiguous_coverage()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 3,
            fieldDefinitionRows: 4,
            methodDefinitionRows: 4);

        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 1), Row(module, 2, 2, 2), Row(module, 3, 6, 5)],
            MetadataTypeDefinitionTableIssue.FieldListStartOutOfRange);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 1), Row(module, 2, 4, 2), Row(module, 3, 3, 5)],
            MetadataTypeDefinitionTableIssue.FieldListOrderInvalid);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 2, 1), Row(module, 2, 2, 2), Row(module, 3, 5, 5)],
            MetadataTypeDefinitionTableIssue.FieldListCoverageInvalid);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 0, 1), Row(module, 2, 0, 2), Row(module, 3, 0, 5)],
            MetadataTypeDefinitionTableIssue.FieldListCoverageInvalid);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 1), Row(module, 2, 0, 2), Row(module, 3, 3, 5)],
            MetadataTypeDefinitionTableIssue.FieldListCoverageInvalid);

        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 1), Row(module, 2, 2, 2), Row(module, 3, 5, 6)],
            MetadataTypeDefinitionTableIssue.MethodListStartOutOfRange);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 1), Row(module, 2, 2, 4), Row(module, 3, 5, 3)],
            MetadataTypeDefinitionTableIssue.MethodListOrderInvalid);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 2), Row(module, 2, 2, 2), Row(module, 3, 5, 5)],
            MetadataTypeDefinitionTableIssue.MethodListCoverageInvalid);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 0), Row(module, 2, 2, 0), Row(module, 3, 5, 0)],
            MetadataTypeDefinitionTableIssue.MethodListCoverageInvalid);
        AssertIssue(
            sourceEnds,
            [Row(module, 1, 1, 1), Row(module, 2, 2, 0), Row(module, 3, 5, 3)],
            MetadataTypeDefinitionTableIssue.MethodListCoverageInvalid);
    }

    /// <summary>Proves a missing required module TypeDef row is invalid before every member-table disposition.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Zero_TypeDef_source_end_is_always_invalid_even_when_every_other_table_is_empty()
    {
        var module = CreateMetadataModule();
        foreach (var (fieldCount, methodCount) in new[] { (0, 0), (1, 0), (0, 1) })
        {
            var result = MetadataTypeDefinitionTableCatalogIdentity.Create(
                CreateSourceEnds(
                    module,
                    typeDefinitionRows: 0,
                    fieldDefinitionRows: fieldCount,
                    methodDefinitionRows: methodCount),
                ImmutableArray<MetadataTypeDefinitionRowObservationIdentity>.Empty);
            AssertInvalid(
                result,
                MetadataTypeDefinitionTableIssue.RequiredModuleTypeDefinitionMissing,
                observedCount: 0);
        }

        var surplusClaim = MetadataTypeDefinitionTableCatalogIdentity.Create(
            CreateSourceEnds(module, typeDefinitionRows: 0),
            [Row(module, 1, 0, 0)]);
        AssertInvalid(
            surplusClaim,
            MetadataTypeDefinitionTableIssue.RequiredModuleTypeDefinitionMissing,
            observedCount: 1);
    }

    /// <summary>Proves observations retain physical columns while derived facts remain catalog-mint-only.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Physical_observation_and_derived_row_surfaces_prevent_caller_asserted_facts()
    {
        var module = CreateMetadataModule();
        var observation = Row(
            module,
            rowId: 1,
            fieldStart: 0,
            methodStart: 0,
            namespaceName: "Synthetic.Deep",
            typeName: "Container`2",
            extendsToken: 0x02000001);

        Assert.Equal(0, observation.FieldListRowId);
        Assert.Equal(0, observation.MethodListRowId);
        Assert.Equal("Synthetic.Deep", observation.NamespaceName);
        Assert.Equal("Container`2", observation.TypeName);
        Assert.Equal(0x02000001, observation.ExtendsMetadataToken);
        Assert.Null(typeof(MetadataTypeDefinitionRowObservationIdentity).GetProperty("FieldListEndExclusiveRowId"));
        Assert.Null(typeof(MetadataTypeDefinitionRowObservationIdentity).GetProperty("MethodListEndExclusiveRowId"));
        Assert.Null(typeof(MetadataTypeDefinitionRowObservationIdentity).GetProperty("GenericParameterCount"));
        Assert.Null(typeof(MetadataTypeDefinitionRowObservationIdentity).GetProperty("EnclosingType"));

        Assert.Empty(typeof(MetadataTypeDefinitionTableRowIdentity).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataTypeDefinitionTableRowIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(MetadataTypeDefinitionTableCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));

        var guardedFactory = Assert.Single(typeof(MetadataTypeDefinitionTableRowIdentity).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", guardedFactory.Name);
        Assert.Equal(
            [
                typeof(object),
                typeof(MetadataSourceEndIdentity),
                typeof(MetadataTypeDefinitionRowObservationIdentity),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(ImmutableArray<int>),
                typeof(ImmutableArray<int>),
            ],
            guardedFactory.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        Assert.Throws<ArgumentException>(() => MetadataTypeDefinitionTableRowIdentity.Create(
            new object(),
            null!,
            null!,
            fieldListRowId: 99,
            fieldListEndExclusiveRowId: 1,
            methodListRowId: 99,
            methodListEndExclusiveRowId: 1,
            fieldDefinitionTokens: ImmutableArray<int>.Empty,
            methodDefinitionTokens: ImmutableArray<int>.Empty));
        Assert.False(MetadataTypeDefinitionTableCatalogIdentity.OwnsRowMintCapability(new object()));

        Assert.Throws<ArgumentOutOfRangeException>(() => Row(module, 1, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                0x01000001,
                1,
                1,
                string.Empty,
                "WrongTokenTable",
                0,
                null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Row(module, 1, 1, 1, extendsToken: 0x04000001));
    }

    /// <summary>Proves the source-end schema and both V2 profiles expose the NestedClass table bound.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void NestedClass_source_end_and_bound_are_explicit_profile_inputs()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 1, nestedClassRows: 23);

        Assert.Equal(23, sourceEnds.NestedClassRowCount);
        Assert.Equal(65_536, ExpressionV2ContractLimits.MaximumNestedClassRowCount);
        Assert.Equal(65_536, ExpressionV2ContractLimits.MaximumTypeDefinitionRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumNestedClassRowCount);
        Assert.Equal(65_536, FrameValueV1Limits.MaximumTypeDefinitionRowCount);
        Assert.Equal(65_536, FrameValueV1Limits.MaximumNestedClassRowCount);
        Assert.Contains(
            ExpressionV2ContractLimits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.NestedClassRowCountBoundName &&
                            bound.Value == 65_536);
        Assert.Contains(
            StaticFieldV2Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.NestedClassRowCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.NestedClassRowCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.TypeDefinitionRowCountBoundName);

        Assert.False(sourceEnds.ContainsTypeDefinitionToken(0x02000000));
        Assert.False(sourceEnds.ContainsMethodDefinitionToken(0x06000000));
        Assert.False(sourceEnds.ContainsGenericParameterToken(0x2A000000));
        var catalog = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [Row(module, 1, 0, 0)]);
        Assert.Null(catalog.ExactRowOrDefault(0x02000000));
        Assert.NotNull(catalog.ExactRowOrDefault(0x02000001));
    }

    /// <summary>Records the public legacy TypeDef factories as the next migration boundary, not catalog evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Legacy_TypeDef_factories_remain_an_explicit_followup_boundary()
    {
        Assert.NotNull(typeof(MetadataRawTypeDefinitionIdentity).GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(MetadataTypeDefinitionIdentity).GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static));
    }

    /// <summary>Proves every new public draft type and method has emitted XML and an intentionally narrow issuer surface.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void TypeDefinition_table_public_surface_has_draft_XML_and_no_derived_row_factory()
    {
        var assembly = typeof(MetadataTypeDefinitionTableCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataTypeDefinitionTableResultKind),
            typeof(MetadataTypeDefinitionTableIssue),
            typeof(MetadataTypeDefinitionRowObservationIdentity),
            typeof(MetadataTypeDefinitionTableRowIdentity),
            typeof(MetadataTypeDefinitionTableCatalogIdentity),
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
        }

        Assert.Equal(
            ["Create"],
            typeof(MetadataTypeDefinitionRowObservationIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name)
                .ToArray());
        var catalogFactories = typeof(MetadataTypeDefinitionTableCatalogIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(2, catalogFactories.Length);
        Assert.All(catalogFactories, static method => Assert.Equal("Create", method.Name));
    }

    private static void AssertIssue(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> observations,
        MetadataTypeDefinitionTableIssue expectedIssue)
    {
        var result = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, observations);
        AssertInvalid(result, expectedIssue, observations.Length);
    }

    private static void AssertNonExact(
        MetadataTypeDefinitionTableCatalogIdentity result,
        MetadataTypeDefinitionTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataTypeDefinitionTableResultKind.NonExact, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Rows);
        Assert.Equal(observedCount, result.ObservedCount);
        if (issue is not MetadataTypeDefinitionTableIssue.TableRowBoundReached and
            not MetadataTypeDefinitionTableIssue.FieldDefinitionRowBoundReached and
            not MetadataTypeDefinitionTableIssue.MethodDefinitionRowBoundReached)
        {
            Assert.Null(result.ReachedBound);
        }
    }

    private static void AssertInvalid(
        MetadataTypeDefinitionTableCatalogIdentity result,
        MetadataTypeDefinitionTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Invalid, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Rows);
        Assert.Null(result.ReachedBound);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static MetadataTypeDefinitionRowObservationIdentity Row(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int fieldStart,
        int methodStart,
        string namespaceName = "Synthetic",
        string? typeName = null,
        int? extendsToken = null) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x02000000 | rowId,
            fieldStart,
            methodStart,
            namespaceName,
            typeName ?? $"Type{rowId}",
            (int)TypeAttributes.Public,
            extendsToken);

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRows,
        int typeReferenceRows = 0,
        int typeSpecificationRows = 0,
        int fieldDefinitionRows = 0,
        int methodDefinitionRows = 0,
        int nestedClassRows = 0,
        int fieldPointerRows = 0,
        int methodPointerRows = 0) =>
        MetadataSourceEndIdentity.Create(
            module,
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
                fieldPointerRowCount: fieldPointerRows,
                methodPointerRowCount: methodPointerRows));

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
            $"typedef-catalog-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.TypeDefinitionCatalog",
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
}
