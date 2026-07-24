using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises complete source-anchored FieldPtr and MethodPtr draft catalogs with synthetic table layouts.</summary>
public sealed class W8MemberPointerTableCatalogContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves independently present and combined pointer tables resolve active list rows to definition tokens.</summary>
    /// <param name="useFieldPointers">Whether the synthetic FieldPtr table is present.</param>
    /// <param name="useMethodPointers">Whether the synthetic MethodPtr table is present.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("Category", "Fast")]
    public void Exact_pointer_domains_resolve_reordered_FieldDef_and_MethodDef_ownership(
        bool useFieldPointers,
        bool useMethodPointers)
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 4,
            fieldDefinitionRows: 5,
            methodDefinitionRows: 4,
            fieldPointerRows: useFieldPointers ? 5 : 0,
            methodPointerRows: useMethodPointers ? 4 : 0);
        var fieldObservations = useFieldPointers
            ? PointerRows(module, MetadataMemberPointerTableKind.Field, [5, 2, 4, 1, 3])
            : default;
        var methodObservations = useMethodPointers
            ? PointerRows(module, MetadataMemberPointerTableKind.Method, [2, 4, 1, 3])
            : default;

        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            fieldObservations,
            methodObservations);
        var types = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [
                TypeRow(module, 1, fieldStart: 0, methodStart: 0),
                TypeRow(module, 2, fieldStart: 1, methodStart: 1),
                TypeRow(module, 3, fieldStart: 4, methodStart: 3),
                TypeRow(module, 4, fieldStart: 6, methodStart: 5),
            ],
            pointers);

        Assert.Equal(MetadataMemberPointerTableResultKind.Exact, pointers.ResultKind);
        Assert.Equal(useFieldPointers ? 5 : 0, pointers.FieldRows.Length);
        Assert.Equal(useMethodPointers ? 4 : 0, pointers.MethodRows.Length);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, types.ResultKind);
        Assert.Equal(
            useFieldPointers ? [0x04000005, 0x04000002, 0x04000004] : [0x04000001, 0x04000002, 0x04000003],
            types.Rows[1].FieldDefinitionTokens.ToArray());
        Assert.Equal(
            useFieldPointers ? [0x04000001, 0x04000003] : [0x04000004, 0x04000005],
            types.Rows[2].FieldDefinitionTokens.ToArray());
        Assert.Equal(
            useMethodPointers ? [0x06000002, 0x06000004] : [0x06000001, 0x06000002],
            types.Rows[1].MethodDefinitionTokens.ToArray());
        Assert.Equal(
            useMethodPointers ? [0x06000001, 0x06000003] : [0x06000003, 0x06000004],
            types.Rows[2].MethodDefinitionTokens.ToArray());
        Assert.Empty(types.Rows[0].FieldDefinitionTokens);
        Assert.Empty(types.Rows[3].MethodDefinitionTokens);
    }

    /// <summary>Proves absent pointer tables are exact direct domains, including wholly empty definition tables.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Direct_and_empty_domains_are_exact_without_pointer_observations()
    {
        var module = CreateMetadataModule();
        var directEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 4,
            fieldDefinitionRows: 3,
            methodDefinitionRows: 2);
        var directPointers = MetadataMemberPointerTableCatalogIdentity.Create(directEnds, default, default);
        var directTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            directEnds,
            [
                TypeRow(module, 1, 0, 0),
                TypeRow(module, 2, 1, 1),
                TypeRow(module, 3, 3, 3),
                TypeRow(module, 4, 3, 3),
            ]);

        Assert.Equal(MetadataMemberPointerTableResultKind.Exact, directPointers.ResultKind);
        Assert.Empty(directPointers.FieldRows);
        Assert.Empty(directPointers.MethodRows);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, directTypes.ResultKind);
        Assert.Equal([0x04000001, 0x04000002], directTypes.Rows[1].FieldDefinitionTokens.ToArray());
        Assert.Equal([0x06000001, 0x06000002], directTypes.Rows[1].MethodDefinitionTokens.ToArray());
        Assert.Empty(directTypes.Rows[2].FieldDefinitionTokens);
        Assert.Equal([0x04000003], directTypes.Rows[3].FieldDefinitionTokens.ToArray());

        var emptyEnds = CreateSourceEnds(module, typeDefinitionRows: 1);
        var emptyPointers = MetadataMemberPointerTableCatalogIdentity.Create(emptyEnds, default, default);
        var emptyTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            emptyEnds,
            [TypeRow(module, 1, 0, 0)],
            emptyPointers);
        Assert.Equal(MetadataMemberPointerTableResultKind.Exact, emptyPointers.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, emptyTypes.ResultKind);
        Assert.Empty(emptyTypes.Rows[0].FieldDefinitionTokens);
        Assert.Empty(emptyTypes.Rows[0].MethodDefinitionTokens);
    }

    /// <summary>Proves acquisition stops, surplus rows, and source-end count contradictions retain no pointer facts.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Coverage_outcomes_are_typed_and_prefix_free()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            fieldDefinitionRows: 3,
            methodDefinitionRows: 3,
            fieldPointerRows: 3,
            methodPointerRows: 3);
        var fields = PointerRows(module, MetadataMemberPointerTableKind.Field, [3, 1, 2]);
        var methods = PointerRows(module, MetadataMemberPointerTableKind.Method, [2, 3, 1]);

        AssertNonExact(
            MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, fields[..2], methods),
            MetadataMemberPointerTableIssue.FieldPointerTableIncomplete,
            2);
        AssertNonExact(
            MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, fields, methods[..1]),
            MetadataMemberPointerTableIssue.MethodPointerTableIncomplete,
            1);
        AssertInvalid(
            MetadataMemberPointerTableCatalogIdentity.Create(
                sourceEnds,
                fields.Add(Pointer(module, MetadataMemberPointerTableKind.Field, 4, 1)),
                methods),
            MetadataMemberPointerTableIssue.FieldPointerRowCountConflict,
            4);
        AssertInvalid(
            MetadataMemberPointerTableCatalogIdentity.Create(
                sourceEnds,
                fields,
                methods.Add(Pointer(module, MetadataMemberPointerTableKind.Method, 4, 1))),
            MetadataMemberPointerTableIssue.MethodPointerRowCountConflict,
            4);

        var shortFields = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            fieldDefinitionRows: 3,
            methodDefinitionRows: 3,
            fieldPointerRows: 2,
            methodPointerRows: 3);
        AssertInvalid(
            MetadataMemberPointerTableCatalogIdentity.Create(shortFields, default, methods),
            MetadataMemberPointerTableIssue.FieldPointerDefinitionCoverageConflict,
            2);

        var shortMethods = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            fieldDefinitionRows: 3,
            methodDefinitionRows: 3,
            fieldPointerRows: 3,
            methodPointerRows: 2);
        AssertInvalid(
            MetadataMemberPointerTableCatalogIdentity.Create(shortMethods, fields, default),
            MetadataMemberPointerTableIssue.MethodPointerDefinitionCoverageConflict,
            2);
    }

    /// <summary>Proves RID order, source identity, target table, target range, and permutation uniqueness independently.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Physical_pointer_invariants_reject_every_contradictory_complete_claim()
    {
        var module = CreateMetadataModule();
        var otherModule = CreateMetadataModule(moduleAddress: 0x9000, digestCharacter: 'b');
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            fieldDefinitionRows: 3,
            methodDefinitionRows: 3,
            fieldPointerRows: 3,
            methodPointerRows: 3);
        var fields = PointerRows(module, MetadataMemberPointerTableKind.Field, [3, 1, 2]);
        var methods = PointerRows(module, MetadataMemberPointerTableKind.Method, [2, 3, 1]);

        AssertFieldIssue(
            sourceEnds,
            [fields[1], fields[0], fields[2]],
            methods,
            MetadataMemberPointerTableIssue.FieldPointerPhysicalOrderInvalid);
        AssertFieldIssue(
            sourceEnds,
            fields.SetItem(1, Pointer(otherModule, MetadataMemberPointerTableKind.Field, 2, 1)),
            methods,
            MetadataMemberPointerTableIssue.FieldPointerSourceModuleMismatch);
        AssertFieldIssue(
            sourceEnds,
            fields.SetItem(1, Pointer(module, MetadataMemberPointerTableKind.Field, 2, 1, targetTable: 0x06)),
            methods,
            MetadataMemberPointerTableIssue.FieldPointerTargetTableInvalid);
        AssertFieldIssue(
            sourceEnds,
            fields.SetItem(1, Pointer(module, MetadataMemberPointerTableKind.Field, 2, 4)),
            methods,
            MetadataMemberPointerTableIssue.FieldPointerTargetOutOfRange);
        AssertFieldIssue(
            sourceEnds,
            fields.SetItem(1, Pointer(module, MetadataMemberPointerTableKind.Field, 2, 3)),
            methods,
            MetadataMemberPointerTableIssue.FieldPointerTargetDuplicate);

        AssertMethodIssue(
            sourceEnds,
            fields,
            [methods[1], methods[0], methods[2]],
            MetadataMemberPointerTableIssue.MethodPointerPhysicalOrderInvalid);
        AssertMethodIssue(
            sourceEnds,
            fields,
            methods.SetItem(1, Pointer(otherModule, MetadataMemberPointerTableKind.Method, 2, 3)),
            MetadataMemberPointerTableIssue.MethodPointerSourceModuleMismatch);
        AssertMethodIssue(
            sourceEnds,
            fields,
            methods.SetItem(1, Pointer(module, MetadataMemberPointerTableKind.Method, 2, 3, targetTable: 0x04)),
            MetadataMemberPointerTableIssue.MethodPointerTargetTableInvalid);
        AssertMethodIssue(
            sourceEnds,
            fields,
            methods.SetItem(1, Pointer(module, MetadataMemberPointerTableKind.Method, 2, 4)),
            MetadataMemberPointerTableIssue.MethodPointerTargetOutOfRange);
        AssertMethodIssue(
            sourceEnds,
            fields,
            methods.SetItem(1, Pointer(module, MetadataMemberPointerTableKind.Method, 2, 2)),
            MetadataMemberPointerTableIssue.MethodPointerTargetDuplicate);
    }

    /// <summary>Proves the two pointer scans have independent cap-plus-one outcomes and profile aliases.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Pointer_row_bounds_are_independent_deterministic_profile_inputs()
    {
        var module = CreateMetadataModule();
        var overField = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            fieldDefinitionRows: StaticFieldV2Limits.MaximumFieldPointerRowCount + 1,
            fieldPointerRows: StaticFieldV2Limits.MaximumFieldPointerRowCount + 1);
        var fieldResult = MetadataMemberPointerTableCatalogIdentity.Create(overField, default, default);
        AssertNonExact(
            fieldResult,
            MetadataMemberPointerTableIssue.FieldPointerRowBoundReached,
            StaticFieldV2Limits.MaximumFieldPointerRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.FieldPointerRowCountBoundName, fieldResult.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumFieldPointerRowCount, fieldResult.ReachedBound.Value);

        var overMethod = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            methodDefinitionRows: StaticFieldV2Limits.MaximumMethodPointerRowCount + 1,
            methodPointerRows: StaticFieldV2Limits.MaximumMethodPointerRowCount + 1);
        var methodResult = MetadataMemberPointerTableCatalogIdentity.Create(overMethod, default, default);
        AssertNonExact(
            methodResult,
            MetadataMemberPointerTableIssue.MethodPointerRowBoundReached,
            StaticFieldV2Limits.MaximumMethodPointerRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.MethodPointerRowCountBoundName, methodResult.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumMethodPointerRowCount, methodResult.ReachedBound.Value);

        Assert.Equal(65_536, ExpressionV2ContractLimits.MaximumFieldPointerRowCount);
        Assert.Equal(65_536, ExpressionV2ContractLimits.MaximumMethodPointerRowCount);
        Assert.Equal(65_536, ExpressionV2ContractLimits.MaximumMethodDefinitionRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumFieldPointerRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumMethodPointerRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumMethodDefinitionRowCount);
        Assert.Equal(65_536, FrameValueV1Limits.MaximumFieldPointerRowCount);
        Assert.Equal(65_536, FrameValueV1Limits.MaximumMethodPointerRowCount);
        Assert.Equal(65_536, FrameValueV1Limits.MaximumMethodDefinitionRowCount);
        Assert.Contains(
            ExpressionV2ContractLimits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.FieldPointerRowCountBoundName);
        Assert.Contains(
            ExpressionV2ContractLimits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.MethodPointerRowCountBoundName);
        Assert.Contains(
            ExpressionV2ContractLimits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.MethodDefinitionRowCountBoundName);
        Assert.Contains(
            StaticFieldV2Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.FieldPointerRowCountBoundName);
        Assert.Contains(
            FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.MethodPointerRowCountBoundName);
    }

    /// <summary>Proves the pointer domain retains the 0xFFFF list suffix and unencodable end sentinel exactly.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Pointer_domain_derives_the_0xFFFF_suffix_and_0x10000_end_sentinel()
    {
        const int rowCount = 0xFFFF;
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 3,
            fieldDefinitionRows: rowCount,
            fieldPointerRows: rowCount);
        var fieldBuilder = ImmutableArray.CreateBuilder<MetadataMemberPointerRowObservationIdentity>(rowCount);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            fieldBuilder.Add(Pointer(
                module,
                MetadataMemberPointerTableKind.Field,
                rowId,
                targetRowId: rowCount - rowId + 1));
        }
        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            fieldBuilder.MoveToImmutable(),
            default);
        var types = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [
                TypeRow(module, 1, fieldStart: 1, methodStart: 0),
                TypeRow(module, 2, fieldStart: 0xFFFF, methodStart: 0),
                TypeRow(module, 3, fieldStart: 0, methodStart: 0),
            ],
            pointers);

        Assert.Equal(MetadataMemberPointerTableResultKind.Exact, pointers.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, types.ResultKind);
        Assert.Equal((0xFFFF, 0x10000), (types.Rows[1].FieldListRowId, types.Rows[1].FieldListEndExclusiveRowId));
        Assert.Equal([0x04000001], types.Rows[1].FieldDefinitionTokens.ToArray());
        Assert.Equal((0x10000, 0x10000), (types.Rows[2].FieldListRowId, types.Rows[2].FieldListEndExclusiveRowId));
        Assert.Empty(types.Rows[2].FieldDefinitionTokens);
    }

    /// <summary>Proves pointer-catalog disposition is checked before TypeDef rows and exact foreign catalogs are rejected.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void TypeDefinition_integration_requires_an_exact_catalog_for_identical_source_ends()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 2,
            fieldDefinitionRows: 3,
            fieldPointerRows: 3);
        var fields = PointerRows(module, MetadataMemberPointerTableKind.Field, [2, 3, 1]);

        var incompletePointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            fields[..1],
            default);
        var stoppedTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            default,
            incompletePointers);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.NonExact, stoppedTypes.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableIssue.MemberPointerCatalogNonExact, stoppedTypes.Issue);
        Assert.Empty(stoppedTypes.Rows);

        var duplicatePointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            fields.SetItem(1, Pointer(module, MetadataMemberPointerTableKind.Field, 2, 2)),
            default);
        var invalidTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            default,
            duplicatePointers);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Invalid, invalidTypes.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableIssue.MemberPointerCatalogInvalid, invalidTypes.Issue);
        Assert.Empty(invalidTypes.Rows);

        var foreignEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 2,
            typeReferenceRows: 1,
            fieldDefinitionRows: 3,
            fieldPointerRows: 3);
        var foreignPointers = MetadataMemberPointerTableCatalogIdentity.Create(foreignEnds, fields, default);
        var foreignTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            default,
            foreignPointers);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Invalid, foreignTypes.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableIssue.MemberPointerCatalogSourceMismatch, foreignTypes.Issue);
        Assert.Empty(foreignTypes.Rows);
    }

    /// <summary>Proves input, output, canonical replay, and resolved ownership arrays are immutable by construction.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_replays_canonically_and_defensively_copies_every_array()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 2,
            fieldDefinitionRows: 3,
            fieldPointerRows: 3);
        var backing = new[]
        {
            Pointer(module, MetadataMemberPointerTableKind.Field, 1, 3),
            Pointer(module, MetadataMemberPointerTableKind.Field, 2, 1),
            Pointer(module, MetadataMemberPointerTableKind.Field, 3, 2),
        };
        var observations = ImmutableCollectionsMarshal.AsImmutableArray(backing);
        var catalog = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, observations, default);
        var replay = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, observations, default);
        var originalSha = catalog.Sha256;

        backing[0] = Pointer(module, MetadataMemberPointerTableKind.Field, 1, 1);
        var returnedRows = catalog.FieldRows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        var returnedBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;

        Assert.Equal(replay, catalog);
        Assert.Equal(originalSha, catalog.Sha256);
        Assert.Equal(0x04000003, catalog.FieldRows[0].TargetDefinitionMetadataToken);

        var types = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [TypeRow(module, 1, 0, 0), TypeRow(module, 2, 1, 0)],
            catalog);
        var returnedTokens = types.Rows[1].FieldDefinitionTokens;
        ImmutableCollectionsMarshal.AsArray(returnedTokens)![0] = 0x04000001;
        Assert.Equal([0x04000003, 0x04000001, 0x04000002], types.Rows[1].FieldDefinitionTokens.ToArray());
    }

    /// <summary>Proves exact pointer rows and resolved TypeDef token arrays have guarded draft issuer surfaces.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Derived_pointer_and_TypeDef_ownership_facts_cannot_be_caller_authored()
    {
        Assert.Empty(typeof(MetadataMemberPointerTableRowIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataMemberPointerTableRowIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(MetadataMemberPointerTableCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));

        var guardedFactory = Assert.Single(typeof(MetadataMemberPointerTableRowIdentity).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", guardedFactory.Name);
        Assert.Equal(
            [typeof(object), typeof(MetadataSourceEndIdentity), typeof(MetadataMemberPointerRowObservationIdentity)],
            guardedFactory.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        Assert.Throws<ArgumentException>(() => MetadataMemberPointerTableRowIdentity.Create(
            new object(),
            null!,
            null!));
        Assert.False(MetadataMemberPointerTableCatalogIdentity.OwnsRowMintCapability(new object()));

        var typeFactory = Assert.Single(typeof(MetadataTypeDefinitionTableRowIdentity).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Contains(
            typeFactory.GetParameters(),
            static parameter => parameter.ParameterType == typeof(ImmutableArray<int>));
        Assert.Throws<ArgumentException>(() => MetadataTypeDefinitionTableRowIdentity.Create(
            new object(),
            null!,
            null!,
            1,
            1,
            1,
            1,
            [0x04000001],
            [0x06000001]));

        var module = CreateMetadataModule();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataMemberPointerRowObservationIdentity.Create(module, 0x04000001, 0x04000001));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataMemberPointerRowObservationIdentity.Create(module, 0x03000000, 0x04000001));

        Assert.Equal(1, CanonicalSchemaVersion(typeof(MetadataTypeDefinitionRowObservationIdentity)));
        Assert.Equal(2, CanonicalSchemaVersion(typeof(MetadataTypeDefinitionTableRowIdentity)));
        Assert.Equal(2, CanonicalSchemaVersion(typeof(MetadataTypeDefinitionTableCatalogIdentity)));
    }

    /// <summary>Proves every new public pointer draft type and method has emitted XML documentation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Member_pointer_public_surface_has_draft_XML_documentation()
    {
        var assembly = typeof(MetadataMemberPointerTableCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataMemberPointerTableKind),
            typeof(MetadataMemberPointerTableResultKind),
            typeof(MetadataMemberPointerTableIssue),
            typeof(MetadataMemberPointerRowObservationIdentity),
            typeof(MetadataMemberPointerTableRowIdentity),
            typeof(MetadataMemberPointerTableCatalogIdentity),
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
    }

    private static void AssertFieldIssue(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> fields,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> methods,
        MetadataMemberPointerTableIssue issue) =>
        AssertInvalid(
            MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, fields, methods),
            issue,
            fields.Length);

    private static int CanonicalSchemaVersion(Type type) =>
        (int)type.GetField("CanonicalSchemaVersion", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;

    private static void AssertMethodIssue(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> fields,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> methods,
        MetadataMemberPointerTableIssue issue) =>
        AssertInvalid(
            MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, fields, methods),
            issue,
            methods.Length);

    private static void AssertNonExact(
        MetadataMemberPointerTableCatalogIdentity result,
        MetadataMemberPointerTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataMemberPointerTableResultKind.NonExact, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.FieldRows);
        Assert.Empty(result.MethodRows);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static void AssertInvalid(
        MetadataMemberPointerTableCatalogIdentity result,
        MetadataMemberPointerTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataMemberPointerTableResultKind.Invalid, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.FieldRows);
        Assert.Empty(result.MethodRows);
        Assert.Null(result.ReachedBound);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static ImmutableArray<MetadataMemberPointerRowObservationIdentity> PointerRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataMemberPointerTableKind tableKind,
        ImmutableArray<int> targetRowIds)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataMemberPointerRowObservationIdentity>(targetRowIds.Length);
        for (var index = 0; index < targetRowIds.Length; index++)
        {
            builder.Add(Pointer(module, tableKind, index + 1, targetRowIds[index]));
        }
        return builder.MoveToImmutable();
    }

    private static MetadataMemberPointerRowObservationIdentity Pointer(
        StaticFieldMetadataModuleIdentity module,
        MetadataMemberPointerTableKind tableKind,
        int rowId,
        int targetRowId,
        int? targetTable = null)
    {
        var pointerTable = tableKind == MetadataMemberPointerTableKind.Field ? 0x03 : 0x05;
        var definitionTable = targetTable ?? (tableKind == MetadataMemberPointerTableKind.Field ? 0x04 : 0x06);
        return MetadataMemberPointerRowObservationIdentity.Create(
            module,
            pointerTable << 24 | rowId,
            definitionTable << 24 | targetRowId);
    }

    private static MetadataTypeDefinitionRowObservationIdentity TypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int fieldStart,
        int methodStart) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x02000000 | rowId,
            fieldStart,
            methodStart,
            "Synthetic.PointerTables",
            rowId == 1 ? "<Module>" : $"Container{rowId}",
            rowId == 1 ? 0 : (int)TypeAttributes.Public,
            null);

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRows,
        int typeReferenceRows = 0,
        int fieldDefinitionRows = 0,
        int methodDefinitionRows = 0,
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
                methodDefinitionRowCount: methodDefinitionRows,
                fieldPointerRowCount: fieldPointerRows,
                methodPointerRowCount: methodPointerRows));

    private static StaticFieldMetadataModuleIdentity CreateMetadataModule(
        ulong moduleAddress = 0x7000,
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
            $"member-pointer-catalog-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.MemberPointerCatalog",
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
