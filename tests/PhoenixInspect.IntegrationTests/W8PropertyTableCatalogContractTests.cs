using System.Collections.Immutable;
using System.Reflection;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the complete Property (0x17) declaration-table catalog and, more importantly, the exact edge of what it can
/// claim: ownership blocks it can derive, and the PropertyMap and MethodSemantics facts it deliberately cannot.
/// </summary>
/// <remarks>
/// PropertyMap (0x15) has no read-side projection, so ownership arrives per row rather than from a range column. The
/// two invariants asserted here are the only two that remain derivable, and each is deliberately weaker than the row
/// walk it replaces: contiguity without an ordering requirement, and a block-count inequality rather than an equality.
/// </remarks>
public sealed class W8PropertyTableCatalogContractTests
{
    private const int PropertySpecialName = 0x0200;
    private const int PropertyHasDefault = 0x1000;

    private static readonly ImmutableArray<byte> PropertySignature = [0x08, 0x00, 0x08];

    /// <summary>Proves an exact catalog, its per-owner projection, and its pure attribute decodings.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complete_property_table_projects_rows_by_authority_issued_owner()
    {
        var world = Build(
            propertyMapRowCount: 2,
            rows:
            [
                (TypeToken(1), "First", PropertySignature, 0),
                (TypeToken(1), "Second", PropertySignature, PropertySpecialName | PropertyHasDefault),
                (TypeToken(2), "Third", PropertySignature, 0),
            ]);

        Assert.Equal(MetadataPropertyTableResultKind.Exact, world.Catalog.ResultKind);
        Assert.Equal(MetadataPropertyTableIssue.None, world.Catalog.Issue);
        Assert.Equal(3, world.Catalog.Rows.Length);

        var first = world.Catalog.FindRow(PropertyToken(1))!;
        Assert.Equal("First", first.Name);
        Assert.False(first.IsSpecialName);
        Assert.False(first.HasDefault);

        // Real compiler output leaves every one of these bits clear, so a zero attribute set must be ordinary.
        var second = world.Catalog.FindRow(PropertyToken(2))!;
        Assert.True(second.IsSpecialName);
        Assert.True(second.HasDefault);
        Assert.False(second.IsRuntimeSpecialName);

        var moduleOwner = world.Authority.TypeDefinitions[0];
        var holderOwner = world.Authority.TypeDefinitions[1];
        Assert.Equal(2, world.Catalog.RowsForDeclaringTypeOrEmpty(moduleOwner).Length);
        Assert.Single(world.Catalog.RowsForDeclaringTypeOrEmpty(holderOwner));

        // A TypeDef this catalog's own authority did not issue projects nothing, whatever it looks like.
        var foreign = Build(propertyMapRowCount: 0, rows: [], digestCharacter: 'b', moduleAddress: 0xB000);
        Assert.Empty(world.Catalog.RowsForDeclaringTypeOrEmpty(foreign.Authority.TypeDefinitions[0]));
    }

    /// <summary>
    /// Proves the two ownership invariants that remain derivable without a PropertyMap row walk, and proves each is
    /// deliberately no stronger than the physical evidence supports.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ownership_is_proved_only_as_far_as_the_absent_property_map_allows()
    {
        // Contiguity holds: one owner may not own two separated runs, because no range column can express that.
        var split = Build(
            propertyMapRowCount: 2,
            rows:
            [
                (TypeToken(1), "First", PropertySignature, 0),
                (TypeToken(2), "Second", PropertySignature, 0),
                (TypeToken(1), "Third", PropertySignature, 0),
            ]);
        Assert.Equal(MetadataPropertyTableResultKind.Invalid, split.Catalog.ResultKind);
        Assert.Equal(MetadataPropertyTableIssue.OwnershipBlockNotContiguous, split.Catalog.Issue);
        Assert.Empty(split.Catalog.Rows);

        // Ordering does not: PropertyMap is not in the ECMA-335 II.24.2.6 sorted-table set, so a descending owner
        // sequence is a legal image and must be admitted.
        var descending = Build(
            propertyMapRowCount: 2,
            rows:
            [
                (TypeToken(2), "First", PropertySignature, 0),
                (TypeToken(1), "Second", PropertySignature, 0),
            ]);
        Assert.Equal(MetadataPropertyTableResultKind.Exact, descending.Catalog.ResultKind);

        // The block count is bounded by the PropertyMap end, as an inequality: fewer blocks than rows is legal,
        // because a PropertyMap row owning a zero-length run is legal and invisible from this side.
        var fewerBlocks = Build(
            propertyMapRowCount: 5,
            rows: [(TypeToken(1), "Only", PropertySignature, 0)]);
        Assert.Equal(MetadataPropertyTableResultKind.Exact, fewerBlocks.Catalog.ResultKind);

        // More blocks than the PropertyMap end can account for is the contradiction.
        var tooManyBlocks = Build(
            propertyMapRowCount: 1,
            rows:
            [
                (TypeToken(1), "First", PropertySignature, 0),
                (TypeToken(2), "Second", PropertySignature, 0),
            ]);
        Assert.Equal(MetadataPropertyTableResultKind.Invalid, tooManyBlocks.Catalog.ResultKind);
        Assert.Equal(MetadataPropertyTableIssue.OwnershipBlockCountConflict, tooManyBlocks.Catalog.Issue);

        // Properties with no PropertyMap at all cannot be owned by anything.
        var noMap = Build(propertyMapRowCount: 0, rows: [(TypeToken(1), "Orphan", PropertySignature, 0)]);
        Assert.Equal(MetadataPropertyTableIssue.OwnershipBlockCountConflict, noMap.Catalog.Issue);
    }

    /// <summary>
    /// Proves overloaded indexers are admitted and only a repeated signature is a contradiction, which is the
    /// difference between the ECMA key and the one a name-only check would invent.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Property_identity_is_keyed_on_owner_name_and_signature()
    {
        ImmutableArray<byte> intIndexer = [0x08, 0x01, 0x08, 0x08];
        ImmutableArray<byte> stringIndexer = [0x08, 0x01, 0x08, 0x0E];

        // Two legal overloaded indexers share the name Item and differ only in signature.
        var overloaded = Build(
            propertyMapRowCount: 1,
            rows:
            [
                (TypeToken(1), "Item", intIndexer, 0),
                (TypeToken(1), "Item", stringIndexer, 0),
            ]);
        Assert.Equal(MetadataPropertyTableResultKind.Exact, overloaded.Catalog.ResultKind);
        Assert.Equal(2, overloaded.Catalog.Rows.Length);

        // The same name and the same signature on one owner is the contradiction.
        var duplicated = Build(
            propertyMapRowCount: 1,
            rows:
            [
                (TypeToken(1), "Item", intIndexer, 0),
                (TypeToken(1), "Item", intIndexer, 0),
            ]);
        Assert.Equal(MetadataPropertyTableResultKind.Invalid, duplicated.Catalog.ResultKind);
        Assert.Equal(MetadataPropertyTableIssue.DuplicatePropertySignature, duplicated.Catalog.Issue);
    }

    /// <summary>Proves each source, row, and owner invariant carries its own typed stop.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Source_row_and_owner_invariants_each_carry_their_own_typed_stop()
    {
        var incomplete = Build(propertyMapRowCount: 1, rows: [], declaredPropertyRowCount: 2);
        Assert.Equal(MetadataPropertyTableResultKind.NonExact, incomplete.Catalog.ResultKind);
        Assert.Equal(MetadataPropertyTableIssue.TableIncomplete, incomplete.Catalog.Issue);
        Assert.Empty(incomplete.Catalog.Rows);

        var overrun = Build(
            propertyMapRowCount: 1,
            rows: [(TypeToken(1), "A", PropertySignature, 0), (TypeToken(1), "B", PropertySignature, 0)],
            declaredPropertyRowCount: 1);
        Assert.Equal(MetadataPropertyTableIssue.TableRowCountConflict, overrun.Catalog.Issue);

        var emptyName = Build(propertyMapRowCount: 1, rows: [(TypeToken(1), string.Empty, PropertySignature, 0)]);
        Assert.Equal(MetadataPropertyTableIssue.NameEmpty, emptyName.Catalog.Issue);

        var emptySignature = Build(propertyMapRowCount: 1, rows: [(TypeToken(1), "A", [], 0)]);
        Assert.Equal(MetadataPropertyTableIssue.SignatureUninitialized, emptySignature.Catalog.Issue);

        // ECMA-335 II.23.1.14 defines exactly three flags; anything else is a contradiction.
        var badAttributes = Build(propertyMapRowCount: 1, rows: [(TypeToken(1), "A", PropertySignature, 0x0001)]);
        Assert.Equal(MetadataPropertyTableIssue.PropertyAttributesNotAdmitted, badAttributes.Catalog.Issue);

        var unknownOwner = Build(propertyMapRowCount: 1, rows: [(TypeToken(9), "A", PropertySignature, 0)]);
        Assert.Equal(MetadataPropertyTableIssue.DeclaringTypeDefinitionOutOfRange, unknownOwner.Catalog.Issue);

        // An image redirecting ownership through PropertyPtr is unmodeled, so unavailable rather than contradictory.
        var indirected = Build(
            propertyMapRowCount: 1,
            rows: [(TypeToken(1), "A", PropertySignature, 0)],
            propertyPointerRowCount: 1);
        Assert.Equal(MetadataPropertyTableResultKind.NonExact, indirected.Catalog.ResultKind);
        Assert.Equal(MetadataPropertyTableIssue.PropertyPointerIndirectionNotModeled, indirected.Catalog.Issue);
        Assert.Empty(indirected.Catalog.Rows);
    }

    /// <summary>Proves derived rows are minted only by their own catalog and the public surface stays closed.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Property_rows_are_guarded_and_the_public_surface_is_closed()
    {
        var world = Build(propertyMapRowCount: 1, rows: [(TypeToken(1), "A", PropertySignature, 0)]);
        var row = Assert.Single(world.Catalog.Rows);

        var mint = typeof(MetadataPropertyTableRowIdentity).GetMethod(
            "Create",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var thrown = Assert.Throws<TargetInvocationException>(() => mint.Invoke(
            null,
            [new object(), row.Observation, row.DeclaringTypeDefinition]));
        Assert.IsType<ArgumentException>(thrown.InnerException);

        Assert.Empty(typeof(MetadataPropertyTableRowIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(
            ["Create"],
            typeof(MetadataPropertyTableCatalogIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct());
        Assert.Empty(typeof(MetadataPropertyTableCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
    }

    private static PropertyWorld Build(
        int propertyMapRowCount,
        (int Owner, string Name, ImmutableArray<byte> Signature, int Attributes)[] rows,
        int? declaredPropertyRowCount = null,
        int propertyPointerRowCount = 0,
        char digestCharacter = 'd',
        ulong moduleAddress = 0xD000)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            "Synthetic.PropertyCatalog");
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: 2,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: 2,
                fieldDefinitionRowCount: 0,
                propertyDefinitionRowCount: declaredPropertyRowCount ?? rows.Length,
                declaredMemberRowCounts: StaticFieldModuleDeclaredMemberRowCounts.Create(
                    constantRowCount: 0,
                    propertyMapRowCount: propertyMapRowCount,
                    propertyPointerRowCount: propertyPointerRowCount)));

        var typeRows = ImmutableArray.Create(
            TypeRow(module, 1, "<Module>", string.Empty, 0),
            TypeRow(module, 2, "PropertyHolder", "Synthetic.Properties", (int)TypeAttributes.Public));
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            typeRows,
            MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default));
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            MetadataNestedClassTableCatalogIdentity.Create(
                sourceEnds,
                typeDefinitions,
                ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty),
            MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
                sourceEnds,
                ImmutableArray<MetadataGenericParameterRowObservationIdentity>.Empty),
            MetadataMethodDefinitionTableCatalogIdentity.Create(
                typeDefinitions,
                ImmutableArray<MetadataMethodDefinitionRowObservationIdentity>.Empty));
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var observations = ImmutableArray.CreateBuilder<MetadataPropertyRowObservationIdentity>(rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            observations.Add(MetadataPropertyRowObservationIdentity.Create(
                metadataModule: module,
                propertyToken: PropertyToken(index + 1),
                attributes: rows[index].Attributes,
                name: rows[index].Name,
                signatureBytes: rows[index].Signature.IsDefault
                    ? ImmutableArray<byte>.Empty
                    : rows[index].Signature,
                declaringTypeDefinitionToken: rows[index].Owner));
        }

        return new PropertyWorld(
            authority,
            MetadataPropertyTableCatalogIdentity.Create(
                MetadataDeclaredMemberSourceEndIdentity.Create(sourceEnds),
                authority,
                observations.MoveToImmutable()));
    }

    private static MetadataTypeDefinitionRowObservationIdentity TypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        string typeName,
        string namespaceName,
        int attributes) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            typeDefinitionToken: TypeToken(rowId),
            fieldListRowId: 0,
            methodListRowId: 0,
            namespaceName: namespaceName,
            typeName: typeName,
            typeAttributes: attributes,
            extendsMetadataToken: null);

    private static int TypeToken(int rowId) => 0x0200_0000 | rowId;

    private static int PropertyToken(int rowId) => 0x1700_0000 | rowId;

    private sealed record PropertyWorld(
        MetadataDefinitionAuthorityCatalogIdentity Authority,
        MetadataPropertyTableCatalogIdentity Catalog);
}
