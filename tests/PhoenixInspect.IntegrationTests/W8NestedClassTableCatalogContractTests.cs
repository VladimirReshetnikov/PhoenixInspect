using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the complete source-anchored NestedClass-table catalog with synthetic type forests.</summary>
public sealed class W8NestedClassTableCatalogContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves an exact multi-root forest derives every sibling and descendant relation from physical rows.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_derives_a_complete_multi_root_parent_map()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 8, nestedClassRows: 4);
        var typeDefinitions = CreateTypeDefinitionCatalog(
            sourceEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("RootA", TypeAttributes.Public),
            Type("RootB", TypeAttributes.NotPublic),
            Type("ChildA", TypeAttributes.NestedPublic),
            Type("ChildB", TypeAttributes.NestedPrivate),
            Type("GrandchildA", TypeAttributes.NestedFamily),
            Type("RootC", TypeAttributes.Public),
            Type("ChildC", TypeAttributes.NestedAssembly));
        var observations = ImmutableArray.Create(
            Relation(module, rowId: 1, nestedRowId: 4, enclosingRowId: 2),
            Relation(module, rowId: 2, nestedRowId: 5, enclosingRowId: 2),
            Relation(module, rowId: 3, nestedRowId: 6, enclosingRowId: 4),
            Relation(module, rowId: 4, nestedRowId: 8, enclosingRowId: 7));

        var catalog = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            observations);
        var replay = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            observations);

        Assert.Equal(MetadataNestedClassTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataNestedClassTableIssue.None, catalog.Issue);
        Assert.Equal(sourceEnds, catalog.SourceEnds);
        Assert.Equal(typeDefinitions, catalog.TypeDefinitionCatalog);
        Assert.Null(catalog.ReachedBound);
        Assert.Equal(0, catalog.ObservedCount);
        Assert.Equal(catalog, replay);
        Assert.Equal(catalog.Sha256, replay.Sha256);
        Assert.Equal(
            [(4, 2, 1), (5, 2, 1), (6, 4, 2), (8, 7, 1)],
            catalog.Relations.Select(static relation =>
                (
                    RowId(relation.NestedTypeDefinition.TypeDefinitionToken),
                    RowId(relation.EnclosingTypeDefinition.TypeDefinitionToken),
                    relation.NestingDepth)).ToArray());
        Assert.Equal(observations.ToArray(), catalog.Relations.Select(static row => row.Observation).ToArray());
        Assert.Equal(catalog.Relations[2], catalog.ExactRelationOrDefault(0x02000006));
        Assert.Null(catalog.ExactRelationOrDefault(0x02000002));
        Assert.Null(catalog.ExactRelationOrDefault(0x01000001));

        var returnedRelations = catalog.Relations;
        ImmutableCollectionsMarshal.AsArray(returnedRelations)![0] = returnedRelations[^1];
        Assert.Equal(0x02000004, catalog.Relations[0].NestedTypeDefinition.TypeDefinitionToken);

        var canonicalSha = catalog.Sha256;
        var returnedBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        Assert.Equal(canonicalSha, catalog.Sha256);
        Assert.NotEqual(returnedBytes[0], catalog.CanonicalBytes[0]);
    }

    /// <summary>Proves an initialized empty table exactly certifies an all-top-level TypeDef catalog.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Initialized_empty_table_is_an_exact_complete_parent_map()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 3, nestedClassRows: 0);
        var types = CreateTypeDefinitionCatalog(
            sourceEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("PublicRoot", TypeAttributes.Public),
            Type("InternalRoot", TypeAttributes.NotPublic));

        var exact = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            types,
            ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty);
        var unavailable = MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, types, default);

        Assert.Equal(MetadataNestedClassTableResultKind.Exact, exact.ResultKind);
        Assert.Empty(exact.Relations);
        Assert.Equal(0, exact.ObservedCount);
        AssertNonExact(unavailable, MetadataNestedClassTableIssue.TableIncomplete, observedCount: 0);
    }

    /// <summary>Proves missing, surplus, foreign, mismatched, and over-bound prerequisites expose no parent prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Coverage_and_prerequisite_stops_are_prefix_free()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 4, nestedClassRows: 2);
        var types = CreateTypeDefinitionCatalog(
            sourceEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("Root", TypeAttributes.Public),
            Type("Child", TypeAttributes.NestedPublic),
            Type("Grandchild", TypeAttributes.NestedPrivate));
        var first = Relation(module, rowId: 1, nestedRowId: 3, enclosingRowId: 2);
        var second = Relation(module, rowId: 2, nestedRowId: 4, enclosingRowId: 3);

        AssertNonExact(
            MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, types, default),
            MetadataNestedClassTableIssue.TableIncomplete,
            observedCount: 0);
        AssertNonExact(
            MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, types, [first]),
            MetadataNestedClassTableIssue.TableIncomplete,
            observedCount: 1);
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [first, second, Relation(module, 3, 4, 3)]),
            MetadataNestedClassTableIssue.TableRowCountConflict,
            observedCount: 3);

        var foreignModule = CreateMetadataModule(moduleAddress: 0x3000, digestCharacter: 'b');
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [first, Relation(foreignModule, 2, 4, 3)]),
            MetadataNestedClassTableIssue.SourceModuleMismatch,
            observedCount: 2);

        var otherSourceEnds = CreateSourceEnds(foreignModule, typeDefinitionRows: 1, nestedClassRows: 0);
        var otherTypes = CreateTypeDefinitionCatalog(
            otherSourceEnds,
            foreignModule,
            Type("<Module>", TypeAttributes.NotPublic));
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, otherTypes, default),
            MetadataNestedClassTableIssue.TypeDefinitionSourceMismatch,
            observedCount: 0);

        var incompleteTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, default);
        var propagated = MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, incompleteTypes, default);
        AssertNonExact(
            propagated,
            MetadataNestedClassTableIssue.TypeDefinitionTableNonExact,
            incompleteTypes.ObservedCount);
        Assert.Equal(incompleteTypes.ReachedBound, propagated.ReachedBound);

        var invalidTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [
                TypeDefinition(module, 2, Type("WrongRid", TypeAttributes.Public)),
                TypeDefinition(module, 2, Type("Root", TypeAttributes.Public)),
                TypeDefinition(module, 3, Type("Child", TypeAttributes.NestedPublic)),
                TypeDefinition(module, 4, Type("Grandchild", TypeAttributes.NestedPrivate)),
            ]);
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, invalidTypes, default),
            MetadataNestedClassTableIssue.TypeDefinitionTableInvalid,
            invalidTypes.ObservedCount);

        var overSourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            nestedClassRows: StaticFieldV2Limits.MaximumNestedClassRowCount + 1);
        var overTypes = CreateTypeDefinitionCatalog(
            overSourceEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic));
        var over = MetadataNestedClassTableCatalogIdentity.Create(overSourceEnds, overTypes, default);
        AssertNonExact(
            over,
            MetadataNestedClassTableIssue.TableRowBoundReached,
            StaticFieldV2Limits.MaximumNestedClassRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.NestedClassRowCountBoundName, over.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumNestedClassRowCount, over.ReachedBound.Value);
    }

    /// <summary>Proves physical RID and NestedClass-primary-key order are checked without caller-order normalization.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Physical_and_primary_key_order_are_independent_invariants()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 5, nestedClassRows: 2);
        var types = CreateTypeDefinitionCatalog(
            sourceEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("Root", TypeAttributes.Public),
            Type("First", TypeAttributes.NestedPublic),
            Type("Second", TypeAttributes.NestedPrivate),
            Type("OtherRoot", TypeAttributes.Public));

        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [Relation(module, 2, 3, 2), Relation(module, 1, 4, 2)]),
            MetadataNestedClassTableIssue.PhysicalOrderInvalid,
            observedCount: 2);
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [Relation(module, 1, 4, 2), Relation(module, 2, 3, 2)]),
            MetadataNestedClassTableIssue.PrimaryKeySortInvalid,
            observedCount: 2);

        var exact = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            types,
            [Relation(module, 1, 3, 2), Relation(module, 2, 4, 2)]);
        Assert.Equal(MetadataNestedClassTableResultKind.Exact, exact.ResultKind);
    }

    /// <summary>Proves target ranges, unique ownership, and irreflexive parent links are independently typed.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Target_domain_duplicate_owner_and_self_parent_contradictions_are_typed()
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 5, nestedClassRows: 2);
        var types = CreateTypeDefinitionCatalog(
            sourceEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("RootA", TypeAttributes.Public),
            Type("Child", TypeAttributes.NestedPublic),
            Type("RootB", TypeAttributes.Public),
            Type("OtherChild", TypeAttributes.NestedPrivate));

        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [Relation(module, 1, 3, 2), Relation(module, 2, 6, 4)]),
            MetadataNestedClassTableIssue.NestedTypeDefinitionOutOfRange,
            observedCount: 2);
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [Relation(module, 1, 3, 2), Relation(module, 2, 5, 6)]),
            MetadataNestedClassTableIssue.EnclosingTypeDefinitionOutOfRange,
            observedCount: 2);
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [Relation(module, 1, 3, 2), Relation(module, 2, 3, 2)]),
            MetadataNestedClassTableIssue.DuplicateNestedType,
            observedCount: 2);
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                types,
                [Relation(module, 1, 3, 3), Relation(module, 2, 5, 4)]),
            MetadataNestedClassTableIssue.SelfNesting,
            observedCount: 2);
    }

    /// <summary>Proves TypeAttributes and a complete parent map must agree in both directions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Type_visibility_and_complete_parent_coverage_must_agree()
    {
        var module = CreateMetadataModule();

        var topLevelChildEnds = CreateSourceEnds(module, typeDefinitionRows: 3, nestedClassRows: 1);
        var topLevelChildTypes = CreateTypeDefinitionCatalog(
            topLevelChildEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("Root", TypeAttributes.Public),
            Type("ClaimedChild", TypeAttributes.Public));
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                topLevelChildEnds,
                topLevelChildTypes,
                [Relation(module, 1, 3, 2)]),
            MetadataNestedClassTableIssue.NestedVisibilityMismatch,
            observedCount: 1);

        var missingEnds = CreateSourceEnds(module, typeDefinitionRows: 4, nestedClassRows: 1);
        var missingTypes = CreateTypeDefinitionCatalog(
            missingEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("Root", TypeAttributes.Public),
            Type("PresentChild", TypeAttributes.NestedPublic),
            Type("MissingChild", TypeAttributes.NestedFamily));
        AssertInvalid(
            MetadataNestedClassTableCatalogIdentity.Create(
                missingEnds,
                missingTypes,
                [Relation(module, 1, 3, 2)]),
            MetadataNestedClassTableIssue.NestedVisibilityRelationMissing,
            observedCount: 1);
    }

    /// <summary>Proves cycles are invalid while an acyclic depth of sixteen remains exact.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Cycles_are_rejected_before_the_depth_bound_and_depth_sixteen_is_exact()
    {
        var module = CreateMetadataModule();
        var cycleEnds = CreateSourceEnds(module, typeDefinitionRows: 3, nestedClassRows: 2);
        var cycleTypes = CreateTypeDefinitionCatalog(
            cycleEnds,
            module,
            Type("<Module>", TypeAttributes.NotPublic),
            Type("CycleA", TypeAttributes.NestedPublic),
            Type("CycleB", TypeAttributes.NestedPrivate));
        var cycle = MetadataNestedClassTableCatalogIdentity.Create(
            cycleEnds,
            cycleTypes,
            [Relation(module, 1, 2, 3), Relation(module, 2, 3, 2)]);
        AssertInvalid(cycle, MetadataNestedClassTableIssue.ParentCycleDetected, observedCount: 2);

        var exactDepth = StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth;
        var exactEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: exactDepth + 2,
            nestedClassRows: exactDepth);
        var exactTypes = CreateDepthTypeDefinitionCatalog(exactEnds, module, exactDepth);
        var exactRelations = CreateDepthRelations(module, exactDepth);
        var exact = MetadataNestedClassTableCatalogIdentity.Create(exactEnds, exactTypes, exactRelations);

        Assert.Equal(MetadataNestedClassTableResultKind.Exact, exact.ResultKind);
        Assert.Equal(exactDepth, exact.Relations.Length);
        Assert.Equal(exactDepth, exact.Relations[^1].NestingDepth);
        Assert.Null(exact.ReachedBound);
    }

    /// <summary>Proves acyclic depth seventeen produces a prefix-free named bound rather than a partial chain.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Depth_seventeen_reaches_the_named_bound_without_exposing_relations()
    {
        var module = CreateMetadataModule();
        var depth = StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1;
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: depth + 2,
            nestedClassRows: depth);
        var types = CreateDepthTypeDefinitionCatalog(sourceEnds, module, depth);
        var relations = CreateDepthRelations(module, depth);

        var result = MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, types, relations);

        AssertNonExact(
            result,
            MetadataNestedClassTableIssue.NestingDepthBoundReached,
            observedCount: depth);
        Assert.Equal(ExpressionV2ContractLimits.NestedTypeDefinitionDepthBoundName, result.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth, result.ReachedBound.Value);
        Assert.Null(result.ExactRelationOrDefault(0x02000003));
    }

    /// <summary>Proves physical observations cannot carry chains and catalog-derived relations have no caller mint route.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Observation_and_relation_surfaces_prevent_caller_asserted_parent_chains()
    {
        var module = CreateMetadataModule();
        var observation = Relation(module, rowId: 7, nestedRowId: 9, enclosingRowId: 4);

        Assert.Equal(module, observation.MetadataModule);
        Assert.Equal(0x29000007, observation.NestedClassRowToken);
        Assert.Equal(0x02000009, observation.NestedTypeDefinitionToken);
        Assert.Equal(0x02000004, observation.EnclosingTypeDefinitionToken);
        Assert.Null(typeof(MetadataNestedClassRowObservationIdentity).GetProperty("EnclosingChain"));
        Assert.Null(typeof(MetadataNestedClassRowObservationIdentity).GetProperty("NestingDepth"));
        Assert.Empty(typeof(MetadataNestedClassRelationIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataNestedClassRelationIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(MetadataNestedClassTableCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));

        var guardedFactory = Assert.Single(typeof(MetadataNestedClassRelationIdentity).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", guardedFactory.Name);
        Assert.Equal(
            [
                typeof(object),
                typeof(MetadataNestedClassRowObservationIdentity),
                typeof(MetadataTypeDefinitionTableRowIdentity),
                typeof(MetadataTypeDefinitionTableRowIdentity),
                typeof(int),
            ],
            guardedFactory.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        Assert.Throws<ArgumentException>(() => MetadataNestedClassRelationIdentity.Create(
            new object(),
            null!,
            null!,
            null!,
            nestingDepth: 999));
        Assert.False(MetadataNestedClassTableCatalogIdentity.OwnsRelationMintCapability(new object()));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataNestedClassRowObservationIdentity.Create(
                module,
                0x28000001,
                0x02000002,
                0x02000001));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataNestedClassRowObservationIdentity.Create(
                module,
                0x29000001,
                0x01000002,
                0x02000001));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataNestedClassRowObservationIdentity.Create(
                module,
                0x29000001,
                0x02000002,
                0x02000000));
    }

    /// <summary>Proves every new public type and method has emitted XML and a deliberately narrow factory surface.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void NestedClass_catalog_public_surface_has_draft_XML_and_no_relation_factory()
    {
        var assembly = typeof(MetadataNestedClassTableCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataNestedClassTableResultKind),
            typeof(MetadataNestedClassTableIssue),
            typeof(MetadataNestedClassRowObservationIdentity),
            typeof(MetadataNestedClassRelationIdentity),
            typeof(MetadataNestedClassTableCatalogIdentity),
        };

        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(typeDocumentation.Value));

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
                    Assert.False(string.IsNullOrWhiteSpace(member.Value)));
            }
        }

        Assert.Equal(
            ["Create"],
            typeof(MetadataNestedClassRowObservationIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name)
                .ToArray());
        Assert.Equal(
            ["Create"],
            typeof(MetadataNestedClassTableCatalogIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name)
                .ToArray());
    }

    private static MetadataTypeDefinitionTableCatalogIdentity CreateDepthTypeDefinitionCatalog(
        MetadataSourceEndIdentity sourceEnds,
        StaticFieldMetadataModuleIdentity module,
        int depth)
    {
        var rows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(depth + 2);
        rows.Add(TypeDefinition(module, 1, Type("<Module>", TypeAttributes.NotPublic)));
        rows.Add(TypeDefinition(module, 2, Type("Root", TypeAttributes.Public)));
        for (var rowId = 3; rowId <= depth + 2; rowId++)
        {
            rows.Add(TypeDefinition(module, rowId, Type($"Nested{rowId}", TypeAttributes.NestedPublic)));
        }

        var result = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, rows.MoveToImmutable());
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, result.ResultKind);
        return result;
    }

    private static ImmutableArray<MetadataNestedClassRowObservationIdentity> CreateDepthRelations(
        StaticFieldMetadataModuleIdentity module,
        int depth)
    {
        var rows = ImmutableArray.CreateBuilder<MetadataNestedClassRowObservationIdentity>(depth);
        for (var relationRowId = 1; relationRowId <= depth; relationRowId++)
        {
            rows.Add(Relation(
                module,
                relationRowId,
                nestedRowId: relationRowId + 2,
                enclosingRowId: relationRowId + 1));
        }
        return rows.MoveToImmutable();
    }

    private static MetadataTypeDefinitionTableCatalogIdentity CreateTypeDefinitionCatalog(
        MetadataSourceEndIdentity sourceEnds,
        StaticFieldMetadataModuleIdentity module,
        params TypeShape[] types)
    {
        var rows = types.Select((type, index) => TypeDefinition(module, index + 1, type)).ToImmutableArray();
        var result = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, rows);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, result.ResultKind);
        return result;
    }

    private static MetadataTypeDefinitionRowObservationIdentity TypeDefinition(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        TypeShape type) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x02000000 | rowId,
            fieldListRowId: 0,
            methodListRowId: 0,
            type.NamespaceName,
            type.Name,
            (int)(type.Attributes | TypeAttributes.Class),
            extendsMetadataToken: null);

    private static MetadataNestedClassRowObservationIdentity Relation(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int nestedRowId,
        int enclosingRowId) =>
        MetadataNestedClassRowObservationIdentity.Create(
            module,
            0x29000000 | rowId,
            0x02000000 | nestedRowId,
            0x02000000 | enclosingRowId);

    private static TypeShape Type(string name, TypeAttributes attributes) =>
        new(name == "<Module>" ? string.Empty : "Synthetic", name, attributes);

    private static int RowId(int metadataToken) => metadataToken & 0x00FF_FFFF;

    private static void AssertNonExact(
        MetadataNestedClassTableCatalogIdentity result,
        MetadataNestedClassTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataNestedClassTableResultKind.NonExact, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Relations);
        Assert.Equal(observedCount, result.ObservedCount);
        if (issue is not MetadataNestedClassTableIssue.TableRowBoundReached and
            not MetadataNestedClassTableIssue.NestingDepthBoundReached and
            not MetadataNestedClassTableIssue.TypeDefinitionTableNonExact)
        {
            Assert.Null(result.ReachedBound);
        }
    }

    private static void AssertInvalid(
        MetadataNestedClassTableCatalogIdentity result,
        MetadataNestedClassTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataNestedClassTableResultKind.Invalid, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Relations);
        Assert.Null(result.ReachedBound);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRows,
        int nestedClassRows) =>
        MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module.Module,
                module.ModuleContent,
                typeDefinitionsExamined: typeDefinitionRows,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: typeDefinitionRows,
                fieldDefinitionRowCount: 0,
                nestedClassRowCount: nestedClassRows));

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
            $"nestedclass-catalog-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.NestedClassCatalog",
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

    private sealed record TypeShape(string NamespaceName, string Name, TypeAttributes Attributes);
}
