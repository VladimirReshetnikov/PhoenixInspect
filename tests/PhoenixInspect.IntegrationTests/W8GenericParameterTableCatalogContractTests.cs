using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the circularity-free physical GenericParam-table draft contract with synthetic owner groups.</summary>
public sealed class W8GenericParameterTableCatalogContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// Proves interleaved unsorted type and method owners, nested-style total arities, and 65 physical owner positions.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_unsorted_catalog_groups_complete_owner_positions_without_a_semantic_arity_cap()
    {
        var module = CreateMetadataModule();
        var observations = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        void Add(int ownerToken, int number, string? name = null, int flags = 0) =>
            observations.Add(Row(
                module,
                observations.Count + 1,
                number,
                flags,
                ownerToken,
                name ?? $"P{number}"));

        Add(TypeToken(3), 3);
        Add(MethodToken(2), 2);
        Add(TypeToken(2), 1);
        for (var number = 64; number >= 0; number--)
        {
            Add(TypeToken(4), number, flags: number == 64 ? 0x20 : 0);
        }
        Add(TypeToken(3), 0);
        Add(MethodToken(2), 0);
        Add(TypeToken(2), 0);
        Add(TypeToken(3), 2);
        Add(MethodToken(2), 1);
        Add(TypeToken(3), 1);

        var sourceEnds = CreateSourceEnds(
            module,
            genericParameterRows: observations.Count,
            typeDefinitionRows: 6,
            methodDefinitionRows: 5);
        var catalog = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            observations.ToImmutable());

        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataGenericParameterPhysicalTableIssue.None, catalog.Issue);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.Unsorted, catalog.OrderProfile);
        Assert.Equal(74, catalog.Rows.Length);
        Assert.Equal(
            [TypeToken(2), MethodToken(2), TypeToken(3), TypeToken(4)],
            catalog.Owners.Select(static owner => owner.OwnerMetadataToken).ToArray());

        Assert.Equal(
            [0, 1],
            catalog.RowsForOwnerOrEmpty(MetadataGenericParameterOwnerKind.TypeDefinition, TypeToken(2))
                .Select(static row => row.Number).ToArray());
        Assert.Equal(
            [0, 1, 2, 3],
            catalog.RowsForOwnerOrEmpty(MetadataGenericParameterOwnerKind.TypeDefinition, TypeToken(3))
                .Select(static row => row.Number).ToArray());
        var wideOwner = catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(4));
        Assert.Equal(65, wideOwner.Length);
        Assert.Equal(Enumerable.Range(0, 65), wideOwner.Select(static row => row.Number));
        Assert.Equal(0x20, wideOwner[^1].Flags);
        Assert.Equal(
            [0, 1, 2],
            catalog.RowsForOwnerOrEmpty(MetadataGenericParameterOwnerKind.MethodDefinition, MethodToken(2))
                .Select(static row => row.Number).ToArray());

        Assert.Empty(catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(5)));
        Assert.Empty(catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.MethodDefinition,
            MethodToken(5)));
        Assert.Equal(TypeToken(3), catalog.Rows[0].Owner.OwnerMetadataToken);
        Assert.Equal(3, catalog.Rows[0].Number);
    }

    /// <summary>Proves sorted order is recorded while empty decoded names and cross-owner duplicates remain physical.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Sorted_profile_is_recorded_without_cross_owner_name_collisions()
    {
        var module = CreateMetadataModule();
        var observations = ImmutableArray.Create(
            Row(module, 1, 0, 0, TypeToken(2), string.Empty),
            Row(module, 2, 1, 0, TypeToken(2), "TSecond"),
            Row(module, 3, 0, 0, MethodToken(2), string.Empty),
            Row(module, 4, 0, 0, TypeToken(3), "NestedOuter"),
            Row(module, 5, 1, 0, TypeToken(3), "NestedInnerA"),
            Row(module, 6, 2, 0, TypeToken(3), "NestedInnerB"));
        var sourceEnds = CreateSourceEnds(module, 6, 4, 3);

        var catalog = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, observations);

        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.EcmaOwnerThenNumber, catalog.OrderProfile);
        Assert.Equal(3, catalog.Owners.Length);
        Assert.Equal(observations.Select(static row => row.GenericParameterToken),
            catalog.Rows.Select(static row => row.GenericParameterToken));
    }

    /// <summary>Proves an exactly empty table and valid owners with no rows return initialized empty arrays.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Empty_table_and_absent_owner_lookups_are_exact()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, 0, 3, 3);

        var catalog = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);

        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.EcmaOwnerThenNumber, catalog.OrderProfile);
        Assert.Empty(catalog.Rows);
        Assert.Empty(catalog.Owners);
        Assert.Empty(catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(2)));
        Assert.Empty(catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.MethodDefinition,
            MethodToken(2)));

        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.RowsForOwnerOrEmpty(
            (MetadataGenericParameterOwnerKind)99,
            TypeToken(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            MethodToken(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(4)));
    }

    /// <summary>Proves incomplete and surplus complete-table claims are typed and retain no owner or row prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Coverage_outcomes_are_prefix_free()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, 3, 3, 3);
        var rows = ValidThreeRows(module);

        AssertNonExact(
            MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default),
            MetadataGenericParameterPhysicalTableIssue.TableIncomplete,
            0);
        AssertNonExact(
            MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, rows[..2]),
            MetadataGenericParameterPhysicalTableIssue.TableIncomplete,
            2);
        AssertInvalid(
            MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
                sourceEnds,
                rows.Add(Row(module, 4, 0, 0, TypeToken(3), "Surplus"))),
            MetadataGenericParameterPhysicalTableIssue.TableRowCountConflict,
            4);
    }

    /// <summary>Proves every physical token, source, name, uniqueness, and Number-coverage contradiction is typed.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Malformed_complete_rows_are_rejected_independently()
    {
        var module = CreateMetadataModule();
        var otherModule = CreateMetadataModule(moduleAddress: 0x9000, digestCharacter: 'b');
        var sourceEnds = CreateSourceEnds(module, 3, 3, 3);
        var rows = ValidThreeRows(module);

        AssertIssue(
            sourceEnds,
            [rows[1], rows[0], rows[2]],
            MetadataGenericParameterPhysicalTableIssue.PhysicalOrderInvalid);
        AssertIssue(
            sourceEnds,
            ImmutableArray.Create<MetadataGenericParameterRowObservationIdentity>(rows[0], null!, rows[2]),
            MetadataGenericParameterPhysicalTableIssue.PhysicalRowMissing);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(otherModule, 2, 1, 0, TypeToken(2), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.SourceModuleMismatch);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0, 0x04000001, "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.OwnerTokenKindInvalid);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0, TypeToken(0), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.OwnerTokenOutOfRange);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0, TypeToken(4), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.OwnerTokenOutOfRange);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0, MethodToken(0), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.OwnerTokenOutOfRange);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0, MethodToken(4), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.OwnerTokenOutOfRange);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0x40, TypeToken(2), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.FlagsInvalid);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0x03, TypeToken(2), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.FlagsInvalid);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 0, 0, TypeToken(2), "Different")),
            MetadataGenericParameterPhysicalTableIssue.DuplicateOwnerNumber);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 1, 0, TypeToken(2), "TFirst")),
            MetadataGenericParameterPhysicalTableIssue.DuplicateOwnerName);
        AssertIssue(
            sourceEnds,
            rows.SetItem(1, Row(module, 2, 2, 0, TypeToken(2), "TSecond")),
            MetadataGenericParameterPhysicalTableIssue.OwnerNumberCoverageInvalid);
    }

    /// <summary>Proves the global cap is independent of the unrestricted physical row count for any one owner.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Global_table_cap_plus_one_is_nonexact_and_prefix_free()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            StaticFieldV2Limits.MaximumGenericParameterRowCount + 1,
            typeDefinitionRows: 1,
            methodDefinitionRows: 0);

        var result = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);

        AssertNonExact(
            result,
            MetadataGenericParameterPhysicalTableIssue.TableRowBoundReached,
            StaticFieldV2Limits.MaximumGenericParameterRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterRowCountBoundName, result.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterRowCount, result.ReachedBound.Value);
    }

    /// <summary>Proves owner identity compares the exact source module, kind, and token rather than a digest shortcut.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Owner_identity_distinguishes_sources_and_owner_kinds_with_equal_row_ids()
    {
        var firstModule = CreateMetadataModule(moduleAddress: 0x7000, digestCharacter: 'a');
        var secondModule = CreateMetadataModule(moduleAddress: 0x8000, digestCharacter: 'a');
        var first = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            CreateSourceEnds(firstModule, 2, 3, 3),
            [
                Row(firstModule, 1, 0, 0, TypeToken(2), "T"),
                Row(firstModule, 2, 0, 0, MethodToken(2), "M"),
            ]);
        var second = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            CreateSourceEnds(secondModule, 1, 3, 3),
            [Row(secondModule, 1, 0, 0, TypeToken(2), "T")]);

        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.Exact, first.ResultKind);
        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.Exact, second.ResultKind);
        Assert.NotEqual(first.Owners[0], first.Owners[1]);
        Assert.NotEqual(first.Owners[0], second.Owners[0]);
        Assert.Equal(TypeToken(2), first.Owners[0].OwnerMetadataToken);
        Assert.Equal(MethodToken(2), first.Owners[1].OwnerMetadataToken);
        Assert.Equal(firstModule, first.Owners[0].SourceModule);
        Assert.Equal(secondModule, second.Owners[0].SourceModule);
    }

    /// <summary>Proves replay identity and every input or output collection remain immutable.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_replays_and_defensively_copies_all_arrays()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, 3, 3, 3);
        var backing = ValidThreeRows(module).ToArray();
        var observations = ImmutableCollectionsMarshal.AsImmutableArray(backing);
        var catalog = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, observations);
        var replay = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, observations);
        var originalSha = catalog.Sha256;

        backing[0] = Row(module, 1, 0, 0, MethodToken(3), "Changed");
        var returnedRows = catalog.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        var returnedOwners = catalog.Owners;
        ImmutableCollectionsMarshal.AsArray(returnedOwners)![0] = returnedOwners[^1];
        var returnedGroup = catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(2));
        ImmutableCollectionsMarshal.AsArray(returnedGroup)![0] = returnedGroup[^1];
        var returnedBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;

        Assert.Equal(replay, catalog);
        Assert.Equal(originalSha, catalog.Sha256);
        Assert.Equal("TFirst", catalog.Rows[0].Name);
        Assert.Equal([0, 1], catalog.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(2)).Select(static row => row.Number).ToArray());
    }

    /// <summary>Proves observations remain physical-only and exact owner and row issuance is guarded.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Public_and_internal_surfaces_prevent_caller_authored_exact_rows()
    {
        Assert.Null(typeof(MetadataGenericParameterRowObservationIdentity).GetProperty("Owner"));
        Assert.Null(typeof(MetadataGenericParameterRowObservationIdentity).GetProperty("TypeDefinition"));
        Assert.Null(typeof(MetadataGenericParameterRowObservationIdentity).GetProperty("MethodDefinition"));
        Assert.Empty(typeof(MetadataGenericParameterOwnerTokenIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataGenericParameterTableRowIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataGenericParameterPhysicalTableCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataGenericParameterOwnerTokenIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(MetadataGenericParameterTableRowIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));

        var ownerFactory = Assert.Single(typeof(MetadataGenericParameterOwnerTokenIdentity).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", ownerFactory.Name);
        Assert.Equal(
            [
                typeof(object),
                typeof(StaticFieldMetadataModuleIdentity),
                typeof(MetadataGenericParameterOwnerKind),
                typeof(int),
            ],
            ownerFactory.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        Assert.Throws<ArgumentException>(() => MetadataGenericParameterOwnerTokenIdentity.Create(
            new object(),
            null!,
            MetadataGenericParameterOwnerKind.TypeDefinition,
            TypeToken(1)));

        var rowFactory = Assert.Single(typeof(MetadataGenericParameterTableRowIdentity).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", rowFactory.Name);
        Assert.Throws<ArgumentException>(() => MetadataGenericParameterTableRowIdentity.Create(
            new object(),
            null!,
            null!,
            null!));
        Assert.False(MetadataGenericParameterPhysicalTableCatalogIdentity.OwnsOwnerMintCapability(new object()));
        Assert.False(MetadataGenericParameterPhysicalTableCatalogIdentity.OwnsRowMintCapability(new object()));

        var module = CreateMetadataModule();
        Assert.Throws<ArgumentOutOfRangeException>(() => Row(module, 1, -1, 0, TypeToken(1), "T"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Row(module, 1, 0, 0x1_0000, TypeToken(1), "T"));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataGenericParameterRowObservationIdentity.Create(
            module,
            0x29000001,
            0,
            0,
            TypeToken(1),
            "T"));
        Assert.ThrowsAny<ArgumentException>(() => MetadataGenericParameterRowObservationIdentity.Create(
            module,
            0x2A000001,
            0,
            0,
            TypeToken(1),
            null!));
    }

    /// <summary>Proves every new public physical-table draft type and method has emitted XML documentation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void GenericParameter_physical_table_public_surface_has_draft_XML()
    {
        var assembly = typeof(MetadataGenericParameterPhysicalTableCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataGenericParameterPhysicalTableResultKind),
            typeof(MetadataGenericParameterPhysicalTableIssue),
            typeof(MetadataGenericParameterPhysicalOrderProfile),
            typeof(MetadataGenericParameterOwnerTokenIdentity),
            typeof(MetadataGenericParameterRowObservationIdentity),
            typeof(MetadataGenericParameterTableRowIdentity),
            typeof(MetadataGenericParameterPhysicalTableCatalogIdentity),
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

    private static void AssertIssue(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataGenericParameterRowObservationIdentity> rows,
        MetadataGenericParameterPhysicalTableIssue issue) =>
        AssertInvalid(
            MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, rows),
            issue,
            rows.Length);

    private static void AssertNonExact(
        MetadataGenericParameterPhysicalTableCatalogIdentity result,
        MetadataGenericParameterPhysicalTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.NonExact, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.Unavailable, result.OrderProfile);
        Assert.Empty(result.Rows);
        Assert.Empty(result.Owners);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static void AssertInvalid(
        MetadataGenericParameterPhysicalTableCatalogIdentity result,
        MetadataGenericParameterPhysicalTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.Invalid, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.Unavailable, result.OrderProfile);
        Assert.Empty(result.Rows);
        Assert.Empty(result.Owners);
        Assert.Null(result.ReachedBound);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static ImmutableArray<MetadataGenericParameterRowObservationIdentity> ValidThreeRows(
        StaticFieldMetadataModuleIdentity module) =>
        [
            Row(module, 1, 0, 0, TypeToken(2), "TFirst"),
            Row(module, 2, 1, 0, TypeToken(2), "TSecond"),
            Row(module, 3, 0, 0, MethodToken(2), "MFirst"),
        ];

    private static MetadataGenericParameterRowObservationIdentity Row(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int number,
        int flags,
        int ownerToken,
        string name) =>
        MetadataGenericParameterRowObservationIdentity.Create(
            module,
            0x2A000000 | rowId,
            number,
            flags,
            ownerToken,
            name);

    private static int TypeToken(int rowId) => 0x02000000 | rowId;

    private static int MethodToken(int rowId) => 0x06000000 | rowId;

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int genericParameterRows,
        int typeDefinitionRows,
        int methodDefinitionRows) =>
        MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module.Module,
                module.ModuleContent,
                typeDefinitionsExamined: typeDefinitionRows,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: typeDefinitionRows,
                fieldDefinitionRowCount: 0,
                methodDefinitionRowCount: methodDefinitionRows,
                genericParameterRowCount: genericParameterRows));

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
            $"genericparam-physical-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.GenericParameterPhysicalTable",
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
