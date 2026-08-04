using System.Collections.Immutable;
using System.Reflection;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the complete Constant (0x0B) declaration-table catalog: its completeness without a table walk, its parent
/// and value invariants, and the bidirectional FieldDef pairing that turns an absent row into a proven negative.
/// </summary>
/// <remarks>
/// The shared metadata reader projects no Constant rows, so a complete table can only be assembled from the parent
/// side. These assertions are about what makes that assembly a proof rather than a collection: agreement with an end
/// the collection did not produce, a contiguous physical RID sequence, and no two rows claiming one parent.
/// </remarks>
public sealed class W8ConstantTableCatalogContractTests
{
    private const int ModuleTypeRid = 1;
    private const int HolderTypeRid = 2;
    private const int FieldAccessPublic = 0x0006;
    private const int FieldStatic = 0x0010;
    private const int FieldLiteral = 0x0040;
    private const int FieldHasDefault = 0x8000;
    private const int ElementTypeInt32 = 0x08;
    private const int ElementTypeString = 0x0E;
    private const int ElementTypeClass = 0x12;

    private static readonly ImmutableArray<byte> Int32Signature = [0x06, 0x08];

    /// <summary>
    /// Proves an exact catalog over a complete parent-side collection, and that a field declaring no default value
    /// is answered as a proven absence rather than a failed lookup.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complete_parent_side_collection_proves_presence_and_absence()
    {
        var world = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldLiteral | FieldHasDefault, FieldAccessPublic | FieldStatic],
            constantRowCount: 1,
            observations: module =>
            [
                ConstantRow(module, rowId: 1, ElementTypeInt32, FieldToken(1), [0x2A, 0x00, 0x00, 0x00]),
            ]);

        Assert.Equal(MetadataConstantTableResultKind.Exact, world.Catalog.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.None, world.Catalog.Issue);
        Assert.Equal(MetadataConstantParentOrderProfile.EcmaParentSorted, world.Catalog.ParentOrderProfile);
        var row = Assert.Single(world.Catalog.Rows);
        Assert.Equal(MetadataConstantParentKind.FieldDefinition, row.ParentKind);
        Assert.Equal(ElementTypeInt32, row.ConstantTypeCode);
        Assert.Equal(4, row.ConstantValueByteCount);
        Assert.NotNull(row.DeclaringFieldRow);

        // The literal field's value is present and joined to the exact field row that owns it.
        var literalField = world.FieldDefinitions.FindRow(FieldToken(1))!;
        Assert.Equal(
            MetadataConstantDisposition.Present,
            world.Catalog.DispositionForField(literalField, out var found));
        Assert.Same(row, found);

        // The plain field's absence is proven over the complete pairing, not merely unfound.
        var plainField = world.FieldDefinitions.FindRow(FieldToken(2))!;
        Assert.Equal(
            MetadataConstantDisposition.AbsentByDeclaredAttributes,
            world.Catalog.DispositionForField(plainField, out var absent));
        Assert.Null(absent);

        // A row from another catalog's FieldDef table can never be answered, whatever it looks like.
        var foreign = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic],
            constantRowCount: 0,
            observations: _ => [],
            digestCharacter: 'f',
            moduleAddress: 0xF000);
        Assert.Equal(
            MetadataConstantDisposition.OwnerNotIssuedByThisCatalog,
            world.Catalog.DispositionForField(foreign.FieldDefinitions.FindRow(FieldToken(1))!, out _));
    }

    /// <summary>
    /// Proves the three independent completeness facts: agreement with an end the collection did not produce, a
    /// contiguous physical RID sequence, and parent uniqueness.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Completeness_rests_on_count_agreement_contiguity_and_parent_uniqueness()
    {
        // Short of the declared end: incomplete, and no row prefix is exposed.
        var incomplete = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic],
            constantRowCount: 2,
            observations: module => [ConstantRow(module, 1, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0])]);
        Assert.Equal(MetadataConstantTableResultKind.NonExact, incomplete.Catalog.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.TableIncomplete, incomplete.Catalog.Issue);
        Assert.Empty(incomplete.Catalog.Rows);
        Assert.Equal(1, incomplete.Catalog.ObservedCount);

        // Beyond the declared end: a contradiction, not an incompleteness.
        var overrun = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldHasDefault],
            constantRowCount: 1,
            observations: module =>
            [
                ConstantRow(module, 1, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0]),
                ConstantRow(module, 2, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0]),
            ]);
        Assert.Equal(MetadataConstantTableResultKind.Invalid, overrun.Catalog.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.TableRowCountConflict, overrun.Catalog.Issue);
        Assert.Empty(overrun.Catalog.Rows);

        // A gap in the physical RID sequence is a contradiction even at the right count.
        var gapped = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldHasDefault],
            constantRowCount: 1,
            observations: module => [ConstantRow(module, 2, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0])]);
        Assert.Equal(MetadataConstantTableResultKind.Invalid, gapped.Catalog.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.PhysicalOrderInvalid, gapped.Catalog.Issue);

        // Two rows claiming one parent cannot both be the parent's default value.
        var duplicated = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldHasDefault, FieldAccessPublic | FieldStatic | FieldHasDefault],
            constantRowCount: 2,
            observations: module =>
            [
                ConstantRow(module, 1, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0]),
                ConstantRow(module, 2, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0]),
            ]);
        Assert.Equal(MetadataConstantTableResultKind.Invalid, duplicated.Catalog.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.DuplicateParentConstant, duplicated.Catalog.Issue);
    }

    /// <summary>Proves every parent and value invariant is its own typed stop, never a shared or inferred one.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Parent_and_value_invariants_each_carry_their_own_typed_stop()
    {
        // A parent outside the HasConstant coded index.
        AssertIssue(
            MetadataConstantTableIssue.ParentTokenKindInvalid,
            [FieldAccessPublic | FieldStatic],
            module => [ConstantRow(module, 1, ElementTypeInt32, 0x0600_0001, [0, 0, 0, 0])]);

        // A parent inside the coded index but past its own table's exact end.
        AssertIssue(
            MetadataConstantTableIssue.ParentTokenOutOfRange,
            [FieldAccessPublic | FieldStatic],
            module => [ConstantRow(module, 1, ElementTypeInt32, FieldToken(9), [0, 0, 0, 0])]);

        // A type code outside the admitted ECMA encoding set.
        AssertIssue(
            MetadataConstantTableIssue.ConstantTypeCodeNotAdmitted,
            [FieldAccessPublic | FieldStatic | FieldHasDefault],
            module => [ConstantRow(module, 1, 0x18, FieldToken(1), [0, 0, 0, 0])]);

        // A width the type code does not fix.
        AssertIssue(
            MetadataConstantTableIssue.ConstantValueWidthInvalid,
            [FieldAccessPublic | FieldStatic | FieldHasDefault],
            module => [ConstantRow(module, 1, ElementTypeInt32, FieldToken(1), [0, 0])]);

        // A null reference encoding whose four bytes are not all zero is not a reference value.
        AssertIssue(
            MetadataConstantTableIssue.NullReferenceValueNonZero,
            [FieldAccessPublic | FieldStatic | FieldHasDefault],
            module => [ConstantRow(module, 1, ElementTypeClass, FieldToken(1), [0, 0, 1, 0])]);

        // A string constant is UTF-16, so only evenness is fixed - an odd blob contradicts the encoding.
        AssertIssue(
            MetadataConstantTableIssue.ConstantValueWidthInvalid,
            [FieldAccessPublic | FieldStatic | FieldHasDefault],
            module => [ConstantRow(module, 1, ElementTypeString, FieldToken(1), [0x41])]);

        // An even-length string constant of any width is admitted.
        var stringWorld = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldHasDefault],
            constantRowCount: 1,
            observations: module =>
                [ConstantRow(module, 1, ElementTypeString, FieldToken(1), [0x41, 0x00, 0x42, 0x00])]);
        Assert.Equal(MetadataConstantTableResultKind.Exact, stringWorld.Catalog.ResultKind);
        Assert.Equal(4, Assert.Single(stringWorld.Catalog.Rows).ConstantValueByteCount);
    }

    /// <summary>
    /// Proves the bidirectional FieldDef pairing in both directions, which is what makes an absence claimable.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Bidirectional_field_pairing_rejects_both_directions_of_disagreement()
    {
        // A Constant row whose field declares no default value.
        AssertIssue(
            MetadataConstantTableIssue.FieldParentWithoutDefaultFlag,
            [FieldAccessPublic | FieldStatic],
            module => [ConstantRow(module, 1, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0])]);

        // A field declaring a default value with no Constant row at all - the direction that makes absence provable.
        var missing = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldHasDefault],
            constantRowCount: 0,
            observations: _ => []);
        Assert.Equal(MetadataConstantTableResultKind.Invalid, missing.Catalog.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.FieldDefaultFlagWithoutConstantRow, missing.Catalog.Issue);

        // ECMA-335 II.22.15: a literal field must declare a default value.
        var literalWithout = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldLiteral],
            constantRowCount: 0,
            observations: _ => []);
        Assert.Equal(MetadataConstantTableResultKind.Invalid, literalWithout.Catalog.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.FieldLiteralWithoutDefaultFlag, literalWithout.Catalog.Issue);
    }

    /// <summary>Proves derived rows are minted only by their own catalog and the public surface stays closed.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constant_rows_are_guarded_and_the_public_surface_is_closed()
    {
        var world = Build(
            fieldAttributes: [FieldAccessPublic | FieldStatic | FieldHasDefault],
            constantRowCount: 1,
            observations: module => [ConstantRow(module, 1, ElementTypeInt32, FieldToken(1), [0, 0, 0, 0])]);
        var observation = Assert.Single(world.Catalog.Rows).Observation;

        var mint = typeof(MetadataConstantTableRowIdentity).GetMethod(
            "Create",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var thrown = Assert.Throws<TargetInvocationException>(() => mint.Invoke(
            null,
            [new object(), observation, MetadataConstantParentKind.FieldDefinition, null]));
        Assert.IsType<ArgumentException>(thrown.InnerException);

        // The catalog is the only public issuer; the derived row exposes none.
        Assert.Empty(typeof(MetadataConstantTableRowIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(
            ["Create"],
            typeof(MetadataConstantTableCatalogIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct());
        Assert.Empty(typeof(MetadataConstantTableCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
    }

    private static void AssertIssue(
        MetadataConstantTableIssue expected,
        int[] fieldAttributes,
        Func<StaticFieldMetadataModuleIdentity, ImmutableArray<MetadataConstantRowObservationIdentity>> observations)
    {
        var world = Build(fieldAttributes, constantRowCount: 1, observations);
        Assert.Equal(expected, world.Catalog.Issue);
        Assert.Empty(world.Catalog.Rows);
        Assert.Equal(MetadataConstantParentOrderProfile.Unavailable, world.Catalog.ParentOrderProfile);
    }

    private static ConstantWorld Build(
        int[] fieldAttributes,
        int constantRowCount,
        Func<StaticFieldMetadataModuleIdentity, ImmutableArray<MetadataConstantRowObservationIdentity>> observations,
        char digestCharacter = 'c',
        ulong moduleAddress = 0xC000)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            "Synthetic.ConstantCatalog");
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: 2,
                fieldDefinitionsExamined: fieldAttributes.Length,
                typeDefinitionRowCount: 2,
                fieldDefinitionRowCount: fieldAttributes.Length,
                parameterDefinitionRowCount: 0,
                propertyDefinitionRowCount: 0,
                declaredMemberRowCounts: StaticFieldModuleDeclaredMemberRowCounts.Create(
                    constantRowCount,
                    propertyMapRowCount: 0,
                    propertyPointerRowCount: 0)));

        var typeRows = ImmutableArray.Create(
            TypeRow(module, ModuleTypeRid, "<Module>", string.Empty, 0, fieldAttributes.Length == 0 ? 0 : 1),
            TypeRow(
                module,
                HolderTypeRid,
                "ConstantHolder",
                "Synthetic.Constants",
                (int)TypeAttributes.Public,
                fieldAttributes.Length == 0 ? 0 : fieldAttributes.Length + 1));
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

        var fieldRows = ImmutableArray.CreateBuilder<MetadataFieldDefinitionRowObservationIdentity>(
            fieldAttributes.Length);
        for (var index = 0; index < fieldAttributes.Length; index++)
        {
            fieldRows.Add(MetadataFieldDefinitionRowObservationIdentity.Create(
                metadataModule: module,
                fieldDefinitionToken: FieldToken(index + 1),
                attributes: fieldAttributes[index],
                name: $"ConstantField{index + 1}",
                signatureBytes: Int32Signature));
        }

        var fieldDefinitions = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            fieldRows.MoveToImmutable());
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, fieldDefinitions.ResultKind);

        var declaredEnds = MetadataDeclaredMemberSourceEndIdentity.Create(sourceEnds);
        return new ConstantWorld(
            fieldDefinitions,
            MetadataConstantTableCatalogIdentity.Create(
                declaredEnds,
                fieldDefinitions,
                observations(module)));
    }

    private static MetadataConstantRowObservationIdentity ConstantRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int typeCode,
        int parentToken,
        byte[] value) =>
        MetadataConstantRowObservationIdentity.Create(
            metadataModule: module,
            constantToken: 0x0B00_0000 | rowId,
            constantTypeCode: typeCode,
            parentMetadataToken: parentToken,
            constantValueBlob: [.. value]);

    private static MetadataTypeDefinitionRowObservationIdentity TypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        string typeName,
        string namespaceName,
        int attributes,
        int fieldListRowId) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            typeDefinitionToken: 0x0200_0000 | rowId,
            fieldListRowId: fieldListRowId,
            methodListRowId: 0,
            namespaceName: namespaceName,
            typeName: typeName,
            typeAttributes: attributes,
            extendsMetadataToken: null);

    private static int FieldToken(int rowId) => 0x0400_0000 | rowId;

    private sealed record ConstantWorld(
        MetadataFieldDefinitionTableCatalogIdentity FieldDefinitions,
        MetadataConstantTableCatalogIdentity Catalog);
}
