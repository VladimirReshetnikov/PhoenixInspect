using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises complete physical GenericParamConstraint draft authority without target-semantic resolution.</summary>
public sealed class W8MetadataGenericParameterConstraintAuthorityContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves exact owner joins across an unsorted multi-owner GenericParam authority and unresolved targets.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complete_constraint_table_resolves_only_authority_owners_and_retains_physical_targets()
    {
        var scenario = BuildScenario();
        var observations = ConstraintRows(scenario.Module);
        var catalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            observations);
        var replayScenario = BuildScenario();
        var replay = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            replayScenario.SourceEnds,
            replayScenario.GenericParameterAuthority,
            ConstraintRows(replayScenario.Module));

        Assert.Equal(MetadataGenericParameterConstraintPhysicalTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataGenericParameterConstraintPhysicalTableIssue.None, catalog.Issue);
        Assert.Equal(scenario.SourceEnds, catalog.SourceEnds);
        Assert.Equal(scenario.GenericParameterAuthority, catalog.GenericParameterAuthority);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.Unsorted, scenario.GenericParameters.OrderProfile);
        Assert.Equal(7, catalog.Rows.Length);
        Assert.Null(catalog.ReachedBound);
        Assert.Equal(0, catalog.ObservedCount);
        Assert.Equal(catalog, replay);
        Assert.Equal(catalog.GetHashCode(), replay.GetHashCode());
        Assert.Equal(scenario.GenericParameters.Rows.Length, scenario.SourceEnds.GenericParameterRowCount);
        Assert.All(scenario.GenericParameters.Rows, row =>
            Assert.Equal(row, scenario.GenericParameterAuthority.FindGenericParameter(row.GenericParameterToken)));

        var rows = catalog.Rows;
        Assert.Equal(GenericParameterToken(1), rows[0].OwnerGenericParameterToken);
        Assert.Equal(GenericParameterToken(66), rows[2].OwnerGenericParameterToken);
        Assert.Equal(GenericParameterToken(68), rows[3].OwnerGenericParameterToken);
        Assert.Equal(GenericParameterToken(70), rows[5].OwnerGenericParameterToken);
        Assert.Equal(GenericParameterToken(134), rows[6].OwnerGenericParameterToken);
        Assert.Equal(MetadataGenericParameterOwnerKind.TypeDefinition, rows[0].OwnerGroup.OwnerKind);
        Assert.Equal(TypeToken(4), rows[0].OwnerGroup.OwnerMetadataToken);
        Assert.Equal(MetadataGenericParameterOwnerKind.MethodDefinition, rows[2].OwnerGroup.OwnerKind);
        Assert.Equal(MethodToken(2), rows[2].OwnerGroup.OwnerMetadataToken);
        Assert.Equal(MetadataGenericParameterOwnerKind.TypeDefinition, rows[3].OwnerGroup.OwnerKind);
        Assert.Equal(TypeToken(2), rows[3].OwnerGroup.OwnerMetadataToken);
        Assert.Equal(MetadataGenericParameterOwnerKind.MethodDefinition, rows[5].OwnerGroup.OwnerKind);
        Assert.Equal(MethodToken(1), rows[5].OwnerGroup.OwnerMetadataToken);
        Assert.Equal(TypeSpecificationToken(2), rows[0].ConstraintMetadataToken);
        Assert.Equal(TypeToken(2), rows[1].ConstraintMetadataToken);
        Assert.Equal(TypeReferenceToken(4), rows[2].ConstraintMetadataToken);
        Assert.Equal(observations[0], rows[0].Observation);
        Assert.Equal(scenario.SourceEnds, rows[0].SourceEnds);

        foreach (var row in rows)
        {
            Assert.Equal(
                scenario.GenericParameterAuthority.FindGenericParameter(row.OwnerGenericParameterToken),
                row.OwnerParameter);
            Assert.Contains(row.OwnerParameter, row.OwnerGroup.Parameters);
        }

        var firstOwner = scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(1))!;
        var pairLeft = scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(68))!;
        var unconstrainedPairRight = scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(67))!;
        Assert.Equal(2, catalog.RowsForOwnerOrEmpty(firstOwner).Length);
        Assert.Equal(2, catalog.RowsForOwnerOrEmpty(pairLeft).Length);
        Assert.Empty(catalog.RowsForOwnerOrEmpty(unconstrainedPairRight));

        var foreignScenario = BuildScenario(CreateMetadataModule(0x3000, 'b'));
        var foreignParameter = foreignScenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(1))!;
        Assert.Empty(catalog.RowsForOwnerOrEmpty(foreignParameter));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scenario.GenericParameterAuthority.FindGenericParameter(TypeToken(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(135)));

        var rowCanonicalSizes = rows.Select(static row => row.CanonicalBytes.Length).Distinct().ToArray();
        Assert.Single(rowCanonicalSizes);
        Assert.True(catalog.CanonicalBytes.Length < scenario.GenericParameterAuthority.CanonicalBytes.Length);
        var longNameScenario = BuildScenario(pairLeftName: new string('P', 1024));
        var longNameCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            longNameScenario.SourceEnds,
            longNameScenario.GenericParameterAuthority,
            ConstraintRows(longNameScenario.Module));
        Assert.Equal(catalog.CanonicalBytes.Length, longNameCatalog.CanonicalBytes.Length);

        var originalBytes = catalog.CanonicalBytes;
        var returnedRows = catalog.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        var returnedCatalogBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedCatalogBytes)![0] ^= 0x5A;
        var returnedRowBytes = rows[0].CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedRowBytes)![0] ^= 0x5A;
        Assert.Equal(ConstraintToken(1), catalog.Rows[0].GenericParameterConstraintToken);
        Assert.True(originalBytes.AsSpan().SequenceEqual(catalog.CanonicalBytes.AsSpan()));

        Assert.Equal("fd46a01818837acc0d28efe16ae2cb6859a146e61adec49abb5ab478e32498b3", catalog.Sha256);
        Assert.Equal("94b83067ad34ec0180339c6454a4efea468c97565d9061a8f21196bb269ad03a", rows[0].Sha256);
    }

    /// <summary>Proves constraint edges and sets derive all owner facts and raw targets from physical authority.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constraint_consumers_derive_owner_declaration_and_unresolved_target_from_physical_authority()
    {
        var scenario = BuildScenario(useReorderedMethodPointers: true);
        var catalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            ConstraintRows(scenario.Module));
        var methodOwner = scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(66))!;
        var typeOwner = scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(68))!;
        var unconstrainedOwner = scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(67))!;

        var methodConstraints = MetadataGenericParameterConstraintSetIdentity.Create(catalog, methodOwner);
        var typeConstraints = MetadataGenericParameterConstraintSetIdentity.Create(catalog, typeOwner);
        var absentConstraints = MetadataGenericParameterConstraintSetIdentity.Create(catalog, unconstrainedOwner);

        Assert.Equal(MetadataEdgeAggregateResultKind.Exact, methodConstraints.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.None, methodConstraints.Issue);
        Assert.Equal(catalog, methodConstraints.ConstraintCatalog);
        Assert.Equal(scenario.GenericParameterAuthority, methodConstraints.GenericParameterAuthority);
        Assert.Equal(scenario.SourceEnds, methodConstraints.SourceEnds);
        Assert.Equal(methodOwner, methodConstraints.OwnerParameter);
        Assert.Equal(MetadataGenericParameterOwnerKind.MethodDefinition, methodConstraints.OwnerGroup.OwnerKind);
        Assert.Equal(MethodToken(2), methodConstraints.DeclaringMethodDefinition!.MethodDefinitionToken);
        Assert.Equal(TypeToken(2), methodConstraints.DeclaringTypeDefinition.TypeDefinitionToken);
        Assert.Equal(7, methodConstraints.CompleteTableRows.Length);

        var methodEdge = Assert.Single(methodConstraints.Rows);
        Assert.Equal(catalog.Rows[2], methodEdge.ConstraintRow);
        Assert.Equal(methodOwner, methodEdge.OwnerParameter);
        Assert.Equal(methodConstraints.OwnerGroup, methodEdge.OwnerGroup);
        Assert.Equal(methodConstraints.DeclaringMethodDefinition, methodEdge.DeclaringMethodDefinition);
        Assert.Equal(methodConstraints.DeclaringTypeDefinition, methodEdge.DeclaringTypeDefinition);
        Assert.Equal(TypeReferenceToken(4), methodEdge.ConstraintMetadataToken);
        Assert.Equal(ConstraintToken(3), methodEdge.GenericParameterConstraintToken);
        Assert.Equal(scenario.SourceEnds, methodEdge.SourceEnds);

        Assert.Equal(MetadataGenericParameterOwnerKind.TypeDefinition, typeConstraints.OwnerGroup.OwnerKind);
        Assert.Null(typeConstraints.DeclaringMethodDefinition);
        Assert.Equal(TypeToken(2), typeConstraints.DeclaringTypeDefinition.TypeDefinitionToken);
        Assert.Equal([ConstraintToken(4), ConstraintToken(5)],
            typeConstraints.Rows.Select(static edge => edge.GenericParameterConstraintToken).ToArray());
        Assert.All(typeConstraints.Rows, edge => Assert.Equal(typeOwner, edge.OwnerParameter));

        Assert.Equal(MetadataEdgeAggregateResultKind.Exact, absentConstraints.ResultKind);
        Assert.Empty(absentConstraints.Rows);
        Assert.Equal(7, absentConstraints.CompleteTableRows.Length);

        var replayScenario = BuildScenario(useReorderedMethodPointers: true);
        var replayCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            replayScenario.SourceEnds,
            replayScenario.GenericParameterAuthority,
            ConstraintRows(replayScenario.Module));
        var replayOwner = replayScenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(66))!;
        var replay = MetadataGenericParameterConstraintSetIdentity.Create(replayCatalog, replayOwner);
        Assert.Equal(methodConstraints, replay);
        Assert.Equal(methodConstraints.GetHashCode(), replay.GetHashCode());

        var returnedRows = methodConstraints.CompleteTableRows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        Assert.Equal(ConstraintToken(1), methodConstraints.CompleteTableRows[0].GenericParameterConstraintToken);
        Assert.Equal("a50fe88c4e4dbc79daa9cbbc7200ef04e3656c2682ed5b477142046926a3657e", methodEdge.Sha256);
        Assert.Equal("35dff48a5b922ab9af06c1ae54cd1c37d1a0625c91da9174930d721ac2da6761",
            methodConstraints.Sha256);
    }

    /// <summary>Proves TypeSpec constraint use joins the exact physical row before applying owner-specific grammar.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constraint_type_use_requires_exact_same_source_row_and_uses_authority_owner_kind()
    {
        var scenario = BuildScenario(useReorderedMethodPointers: true);
        var catalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            ConstraintRowsForTypeUse(scenario.Module));
        var typeVariable = SpecificationAndGraph(scenario, rowId: 1, [0x1E, 0x00]);
        var methodVariable = SpecificationAndGraph(scenario, rowId: 3, [0x1E, 0x00]);

        var typeUse = MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.GenericParameterConstraint,
            typeVariable.Specification,
            typeVariable.Graph,
            catalog.Rows[3]);
        var methodUse = MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.GenericParameterConstraint,
            methodVariable.Specification,
            methodVariable.Graph,
            catalog.Rows[5]);

        Assert.Equal(MetadataTypeUseResultKind.Invalid, typeUse.Kind);
        Assert.Equal(MetadataTypeUseResultKind.Open, methodUse.Kind);
        Assert.Equal(catalog.Rows[5], methodUse.ConstraintRow);
        Assert.Equal(catalog.Rows[5].OwnerParameter, methodUse.ConstraintOwnerParameter);
        Assert.Equal(catalog.Rows[5].OwnerGroup, methodUse.ConstraintOwnerGroup);
        Assert.Equal(MethodToken(1), methodUse.ConstraintDeclaringMethodDefinition!.MethodDefinitionToken);
        Assert.Equal(TypeToken(3), methodUse.ConstraintDeclaringTypeDefinition!.TypeDefinitionToken);
        Assert.Equal(scenario.SourceEnds, methodUse.ConstraintSourceEnds);
        Assert.Equal(TypeSpecificationToken(3), methodUse.ConstraintMetadataToken);

        Assert.Throws<ArgumentException>(() => MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.GenericParameterConstraint,
            typeVariable.Specification,
            typeVariable.Graph,
            catalog.Rows[5]));
        Assert.Throws<ArgumentException>(() => MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.GenericParameterConstraint,
            typeVariable.Specification,
            typeVariable.Graph));
        Assert.Throws<ArgumentException>(() => MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.TypeSpecification,
            typeVariable.Specification,
            typeVariable.Graph,
            catalog.Rows[3]));

        var foreignScenario = BuildScenario(CreateMetadataModule(0x3000, 'b'));
        var foreignCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            foreignScenario.SourceEnds,
            foreignScenario.GenericParameterAuthority,
            ConstraintRowsForTypeUse(foreignScenario.Module));
        Assert.Throws<ArgumentException>(() => MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.GenericParameterConstraint,
            methodVariable.Specification,
            methodVariable.Graph,
            foreignCatalog.Rows[5]));
        Assert.Equal("ac044646b4895bdd3d1e2b86efecbdb9056ac27e6200ff31154ee87b8332acd8", methodUse.Sha256);
    }

    /// <summary>Proves consumer projections propagate physical stops and reject foreign authority selections.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constraint_consumer_stops_are_prefix_free_and_owner_selection_is_authority_exact()
    {
        var scenario = BuildScenario();
        var observations = ConstraintRows(scenario.Module);
        var owner = scenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(68))!;
        var incompleteCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            observations.RemoveAt(observations.Length - 1));
        var incomplete = MetadataGenericParameterConstraintSetIdentity.Create(incompleteCatalog, owner);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, incomplete.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.SourceIncomplete, incomplete.Issue);
        Assert.Equal(6, incomplete.ObservedCount);
        Assert.Empty(incomplete.CompleteTableRows);
        Assert.Empty(incomplete.Rows);

        var otherModule = CreateMetadataModule(0x3000, 'b');
        var invalidCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            Replace(observations, 0, ConstraintRow(
                otherModule,
                rowId: 1,
                ownerToken: GenericParameterToken(1),
                constraintToken: TypeSpecificationToken(2))));
        var invalid = MetadataGenericParameterConstraintSetIdentity.Create(invalidCatalog, owner);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, invalid.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.ModuleMismatch, invalid.Issue);
        Assert.Empty(invalid.CompleteTableRows);
        Assert.Empty(invalid.Rows);

        var exactCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            observations);
        var foreignScenario = BuildScenario(otherModule);
        var foreignOwner = foreignScenario.GenericParameterAuthority.FindGenericParameter(GenericParameterToken(68))!;
        Assert.Throws<ArgumentException>(() =>
            MetadataGenericParameterConstraintSetIdentity.Create(exactCatalog, foreignOwner));
        var sameSourceDifferentAuthority = BuildScenario(scenario.Module, pairLeftName: "OtherPairLeft");
        var conflictingOwner = sameSourceDifferentAuthority.GenericParameterAuthority.FindGenericParameter(
            GenericParameterToken(68))!;
        Assert.Throws<ArgumentException>(() =>
            MetadataGenericParameterConstraintSetIdentity.Create(exactCatalog, conflictingOwner));

        var tableBoundScenario = BuildScenario(
            genericParameterConstraintRowCount:
                StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount + 1);
        var tableBoundOwner = tableBoundScenario.GenericParameterAuthority.FindGenericParameter(
            GenericParameterToken(68))!;
        var tableBoundCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            tableBoundScenario.SourceEnds,
            tableBoundScenario.GenericParameterAuthority,
            default);
        var tableBound = MetadataGenericParameterConstraintSetIdentity.Create(tableBoundCatalog, tableBoundOwner);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, tableBound.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.BoundReached, tableBound.Issue);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName,
            tableBound.ReachedBound!.Name);
        Assert.Empty(tableBound.CompleteTableRows);

        var ownerConstraintCount = StaticFieldV2Limits.MaximumGenericConstraintCount + 1;
        var ownerBoundScenario = BuildScenario(
            genericParameterConstraintRowCount: ownerConstraintCount,
            typeSpecificationRowCount: ownerConstraintCount);
        var ownerBoundObservations = Enumerable.Range(1, ownerConstraintCount)
            .Select(rowId => ConstraintRow(
                ownerBoundScenario.Module,
                rowId,
                GenericParameterToken(68),
                TypeSpecificationToken(rowId)))
            .ToImmutableArray();
        var ownerBoundCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            ownerBoundScenario.SourceEnds,
            ownerBoundScenario.GenericParameterAuthority,
            ownerBoundObservations);
        var ownerBoundOwner = ownerBoundScenario.GenericParameterAuthority.FindGenericParameter(
            GenericParameterToken(68))!;
        var ownerBound = MetadataGenericParameterConstraintSetIdentity.Create(ownerBoundCatalog, ownerBoundOwner);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, ownerBound.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.BoundReached, ownerBound.Issue);
        Assert.Equal(ExpressionV2ContractLimits.GenericConstraintCountBoundName, ownerBound.ReachedBound!.Name);
        Assert.Equal(ownerConstraintCount, ownerBound.ObservedCount);
        Assert.Empty(ownerBound.CompleteTableRows);
        Assert.Empty(ownerBound.Rows);
    }

    /// <summary>Proves deterministic rejection for physical ordering, source, token, and duplicate contradictions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constraint_table_rejects_order_source_range_and_duplicate_contradictions_prefix_free()
    {
        var scenario = BuildScenario();
        var rows = ConstraintRows(scenario.Module);
        var otherModule = CreateMetadataModule(0x3000, 'b');

        AssertInvalid(
            scenario,
            Replace(rows, 3, null!),
            MetadataGenericParameterConstraintPhysicalTableIssue.PhysicalRowMissing);
        AssertInvalid(
            scenario,
            Replace(rows, 0, ConstraintRow(
                scenario.Module,
                rowId: 2,
                ownerToken: GenericParameterToken(1),
                constraintToken: TypeSpecificationToken(2))),
            MetadataGenericParameterConstraintPhysicalTableIssue.PhysicalOrderInvalid);
        AssertInvalid(
            scenario,
            Replace(rows, 0, ConstraintRow(
                otherModule,
                rowId: 1,
                ownerToken: GenericParameterToken(1),
                constraintToken: TypeSpecificationToken(2))),
            MetadataGenericParameterConstraintPhysicalTableIssue.SourceModuleMismatch);
        AssertInvalid(
            scenario,
            Replace(rows, 0, ConstraintRow(
                scenario.Module,
                rowId: 1,
                ownerToken: TypeToken(1),
                constraintToken: TypeSpecificationToken(2))),
            MetadataGenericParameterConstraintPhysicalTableIssue.OwnerTokenKindInvalid);
        AssertInvalid(
            scenario,
            Replace(rows, 0, ConstraintRow(
                scenario.Module,
                rowId: 1,
                ownerToken: GenericParameterToken(135),
                constraintToken: TypeSpecificationToken(2))),
            MetadataGenericParameterConstraintPhysicalTableIssue.OwnerTokenOutOfRange);
        AssertInvalid(
            scenario,
            Replace(rows, 3, ConstraintRow(
                scenario.Module,
                rowId: 4,
                ownerToken: GenericParameterToken(65),
                constraintToken: TypeSpecificationToken(1))),
            MetadataGenericParameterConstraintPhysicalTableIssue.OwnerOrderInvalid);
        AssertInvalid(
            scenario,
            Replace(rows, 0, ConstraintRow(
                scenario.Module,
                rowId: 1,
                ownerToken: GenericParameterToken(1),
                constraintToken: MethodToken(1))),
            MetadataGenericParameterConstraintPhysicalTableIssue.ConstraintTokenKindInvalid);
        AssertInvalid(
            scenario,
            Replace(rows, 0, ConstraintRow(
                scenario.Module,
                rowId: 1,
                ownerToken: GenericParameterToken(1),
                constraintToken: TypeReferenceToken(5))),
            MetadataGenericParameterConstraintPhysicalTableIssue.ConstraintTokenOutOfRange);
        AssertInvalid(
            scenario,
            Replace(rows, 1, ConstraintRow(
                scenario.Module,
                rowId: 2,
                ownerToken: GenericParameterToken(1),
                constraintToken: TypeSpecificationToken(2))),
            MetadataGenericParameterConstraintPhysicalTableIssue.DuplicateOwnerConstraint);

        var extra = rows.Add(ConstraintRow(
            scenario.Module,
            rowId: 8,
            ownerToken: GenericParameterToken(134),
            constraintToken: TypeSpecificationToken(3)));
        AssertInvalid(
            scenario,
            extra,
            MetadataGenericParameterConstraintPhysicalTableIssue.TableRowCountConflict);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataGenericParameterConstraintRowObservationIdentity.Create(
                scenario.Module,
                TypeToken(1),
                GenericParameterToken(1),
                TypeToken(2)));
    }

    /// <summary>Proves default, short, bounded, non-exact, invalid, empty, and foreign prerequisites are prefix-free.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constraint_table_completion_and_prerequisite_stops_are_prefix_free()
    {
        var scenario = BuildScenario();
        var rows = ConstraintRows(scenario.Module);
        var absent = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            default);
        var shortRows = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            rows.RemoveAt(rows.Length - 1));

        AssertStop(
            absent,
            MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact,
            MetadataGenericParameterConstraintPhysicalTableIssue.TableIncomplete,
            observedCount: 0);
        AssertStop(
            shortRows,
            MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact,
            MetadataGenericParameterConstraintPhysicalTableIssue.TableIncomplete,
            observedCount: 6);

        var emptyScenario = BuildZeroGenericScenario();
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, emptyScenario.GenericParameterAuthority.ResultKind);
        Assert.Empty(emptyScenario.GenericParameters.Rows);
        Assert.All(emptyScenario.GenericParameterAuthority.Groups, static group => Assert.Empty(group.Parameters));
        var absentEmpty = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            emptyScenario.SourceEnds,
            emptyScenario.GenericParameterAuthority,
            default);
        AssertStop(
            absentEmpty,
            MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact,
            MetadataGenericParameterConstraintPhysicalTableIssue.TableIncomplete,
            observedCount: 0);
        var empty = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            emptyScenario.SourceEnds,
            emptyScenario.GenericParameterAuthority,
            ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity>.Empty);
        Assert.Equal(MetadataGenericParameterConstraintPhysicalTableResultKind.Exact, empty.ResultKind);
        Assert.Equal(MetadataGenericParameterConstraintPhysicalTableIssue.None, empty.Issue);
        Assert.Empty(empty.Rows);
        Assert.Equal(0, empty.ObservedCount);

        var boundedScenario = BuildScenario(
            genericParameterConstraintRowCount:
                StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount + 1);
        var bounded = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            boundedScenario.SourceEnds,
            boundedScenario.GenericParameterAuthority,
            default);
        AssertStop(
            bounded,
            MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact,
            MetadataGenericParameterConstraintPhysicalTableIssue.TableRowBoundReached,
            StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount + 1);
        Assert.Equal(
            ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName,
            bounded.ReachedBound!.Name);
        Assert.Equal(
            StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount,
            bounded.ReachedBound.Value);

        var incompleteScenario = BuildScenario(omitGenericParameterObservations: true);
        var incompleteAuthority = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            incompleteScenario.SourceEnds,
            incompleteScenario.GenericParameterAuthority,
            ConstraintRows(incompleteScenario.Module));
        AssertStop(
            incompleteAuthority,
            MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact,
            MetadataGenericParameterConstraintPhysicalTableIssue.GenericParameterAuthorityNonExact,
            incompleteScenario.GenericParameterAuthority.ObservedCount);

        var invalidScenario = BuildScenario(moduleTypeName: "WrongModuleType");
        var invalidAuthority = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            invalidScenario.SourceEnds,
            invalidScenario.GenericParameterAuthority,
            ConstraintRows(invalidScenario.Module));
        AssertStop(
            invalidAuthority,
            MetadataGenericParameterConstraintPhysicalTableResultKind.Invalid,
            MetadataGenericParameterConstraintPhysicalTableIssue.GenericParameterAuthorityInvalid,
            invalidScenario.GenericParameterAuthority.ObservedCount);

        var foreignScenario = BuildScenario(CreateMetadataModule(0x3000, 'b'));
        var sourceMismatch = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            foreignScenario.GenericParameterAuthority,
            rows);
        AssertStop(
            sourceMismatch,
            MetadataGenericParameterConstraintPhysicalTableResultKind.Invalid,
            MetadataGenericParameterConstraintPhysicalTableIssue.GenericParameterAuthoritySourceMismatch,
            rows.Length);

        Assert.Equal("bf7ac78eb1cca94030f70cfc12f61c81700031dd85d6a09f43e1cf451c72c76f", bounded.Sha256);
        Assert.Equal(
            "35cc86596d8fc774cfb9d2e085c40ce47fe97880298d60acb2797b3290fa77da",
            incompleteAuthority.Sha256);
        Assert.Equal(
            "e51153a538c5312e7e37c7926afc40c4babdd87a784609f9d477334ea6d308b2",
            invalidAuthority.Sha256);
    }

    /// <summary>Proves exact row issuance is private and every additive public draft API is documented.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constraint_authority_issuers_are_guarded_and_public_surface_is_draft_documented()
    {
        var scenario = BuildScenario();
        var observation = ConstraintRows(scenario.Module)[0];
        var owner = scenario.GenericParameterAuthority.FindGenericParameter(observation.OwnerGenericParameterToken)!;
        var ownerGroup = scenario.GenericParameterAuthority.ExactOwnerGroupOrDefault(
            observation.OwnerGenericParameterToken)!;

        Assert.Throws<ArgumentException>(() =>
            MetadataGenericParameterConstraintTableRowIdentity.Create(
                new object(),
                scenario.SourceEnds,
                observation,
                owner,
                ownerGroup));
        Assert.False(MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.OwnsRowMintCapability(new object()));
        Assert.Empty(typeof(MetadataGenericParameterConstraintTableRowIdentity)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataGenericParameterConstraintTableRowIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Single(typeof(MetadataGenericParameterConstraintRowObservationIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Single(typeof(MetadataGenericParameterConstraintPhysicalTableCatalogIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(MetadataGenericParameterConstraintEdgeIdentity)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataGenericParameterConstraintEdgeIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(MetadataGenericParameterConstraintEdgeIdentity).GetProperty("Target"));
        Assert.Null(typeof(MetadataGenericParameterConstraintEdgeIdentity).GetProperty("DeclaringType"));
        Assert.Equal(
            typeof(MetadataGenericParameterTableRowIdentity),
            typeof(MetadataGenericParameterConstraintEdgeIdentity)
                .GetProperty(nameof(MetadataGenericParameterConstraintEdgeIdentity.OwnerParameter))!.PropertyType);

        var setFactory = Assert.Single(typeof(MetadataGenericParameterConstraintSetIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(nameof(MetadataGenericParameterConstraintSetIdentity.Create), setFactory.Name);
        Assert.Equal(
            [
                typeof(MetadataGenericParameterConstraintPhysicalTableCatalogIdentity),
                typeof(MetadataGenericParameterTableRowIdentity),
            ],
            setFactory.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        Assert.Null(typeof(MetadataGenericParameterConstraintSetIdentity).GetProperty("GenericParameterCatalog"));
        Assert.Null(typeof(MetadataGenericParameterConstraintSetIdentity).GetProperty("DuplicateRowTokens"));
        Assert.Null(typeof(MetadataGenericParameterConstraintSetIdentity).GetProperty("DuplicateSemanticRowTokens"));

        var typeUseFactory = Assert.Single(typeof(MetadataTypeUseResult)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(
            typeof(MetadataGenericParameterConstraintTableRowIdentity),
            typeUseFactory.GetParameters()[3].ParameterType);
        Assert.Null(typeof(MetadataTypeUseResult).GetProperty("ConstraintOwner"));

        var publicTypes = new[]
        {
            typeof(MetadataGenericParameterConstraintPhysicalTableResultKind),
            typeof(MetadataGenericParameterConstraintPhysicalTableIssue),
            typeof(MetadataGenericParameterConstraintRowObservationIdentity),
            typeof(MetadataGenericParameterConstraintTableRowIdentity),
            typeof(MetadataGenericParameterConstraintPhysicalTableCatalogIdentity),
            typeof(MetadataGenericParameterConstraintEdgeIdentity),
            typeof(MetadataGenericParameterConstraintSetIdentity),
        };
        Assert.All(publicTypes.Where(static type => type.IsClass), static type => Assert.True(type.IsSealed));

        var assembly = typeof(MetadataGenericParameterConstraintPhysicalTableCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
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

    private static void AssertInvalid(
        AuthorityScenario scenario,
        ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity> observations,
        MetadataGenericParameterConstraintPhysicalTableIssue issue)
    {
        var result = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            scenario.SourceEnds,
            scenario.GenericParameterAuthority,
            observations);
        AssertStop(
            result,
            MetadataGenericParameterConstraintPhysicalTableResultKind.Invalid,
            issue,
            observations.Length);
    }

    private static void AssertStop(
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity catalog,
        MetadataGenericParameterConstraintPhysicalTableResultKind resultKind,
        MetadataGenericParameterConstraintPhysicalTableIssue issue,
        int observedCount)
    {
        Assert.Equal(resultKind, catalog.ResultKind);
        Assert.Equal(issue, catalog.Issue);
        Assert.Empty(catalog.Rows);
        Assert.Equal(observedCount, catalog.ObservedCount);
    }

    private static ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity> Replace(
        ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity> rows,
        int index,
        MetadataGenericParameterConstraintRowObservationIdentity replacement)
    {
        var builder = rows.ToBuilder();
        builder[index] = replacement;
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity> ConstraintRows(
        StaticFieldMetadataModuleIdentity module) =>
        [
            ConstraintRow(module, 1, GenericParameterToken(1), TypeSpecificationToken(2)),
            ConstraintRow(module, 2, GenericParameterToken(1), TypeToken(2)),
            ConstraintRow(module, 3, GenericParameterToken(66), TypeReferenceToken(4)),
            ConstraintRow(module, 4, GenericParameterToken(68), TypeSpecificationToken(1)),
            ConstraintRow(module, 5, GenericParameterToken(68), TypeReferenceToken(2)),
            ConstraintRow(module, 6, GenericParameterToken(70), TypeToken(4)),
            ConstraintRow(module, 7, GenericParameterToken(134), TypeReferenceToken(1)),
        ];

    private static ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity> ConstraintRowsForTypeUse(
        StaticFieldMetadataModuleIdentity module) =>
        ConstraintRows(module).SetItem(
            5,
            ConstraintRow(module, 6, GenericParameterToken(70), TypeSpecificationToken(3)));

    private static (MetadataTypeSpecificationIdentity Specification, MetadataTypeSpecificationGraphIdentity Graph)
        SpecificationAndGraph(AuthorityScenario scenario, int rowId, ImmutableArray<byte> signatureBytes)
    {
        var reference = MetadataTypeSpecificationRowReferenceIdentity.Create(
            scenario.Module,
            TypeSpecificationToken(rowId));
        var tokenCatalog = MetadataSignatureTokenResolutionCatalog.Create(
            scenario.SourceEnds,
            ImmutableArray<MetadataSignatureTokenResolutionEntry>.Empty);
        var outcome = MetadataTypeSignatureDecoder.DecodeTypeSpecification(reference, signatureBytes, tokenCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, outcome.Kind);
        var specification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(outcome);
        var graph = MetadataTypeSpecificationGraphIdentity.Create(reference, [outcome]);
        return (specification, graph);
    }

    private static MetadataGenericParameterConstraintRowObservationIdentity ConstraintRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int ownerToken,
        int constraintToken) =>
        MetadataGenericParameterConstraintRowObservationIdentity.Create(
            module,
            ConstraintToken(rowId),
            ownerToken,
            constraintToken);

    private static AuthorityScenario BuildScenario(
        StaticFieldMetadataModuleIdentity? module = null,
        int genericParameterConstraintRowCount = 7,
        bool omitGenericParameterObservations = false,
        string moduleTypeName = "<Module>",
        string pairLeftName = "PairLeft",
        bool useReorderedMethodPointers = false,
        int typeSpecificationRowCount = 3)
    {
        module ??= CreateMetadataModule();
        var genericObservations = AuthorityGenericParameterRows(module, pairLeftName);
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module.Module,
                module.ModuleContent,
                typeDefinitionsExamined: 4,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: 4,
                fieldDefinitionRowCount: 0,
                typeReferenceRowCount: 4,
                typeSpecificationRowCount: typeSpecificationRowCount,
                methodDefinitionRowCount: 2,
                nestedClassRowCount: 0,
                genericParameterRowCount: genericObservations.Length,
                genericParameterConstraintRowCount: genericParameterConstraintRowCount,
                methodPointerRowCount: useReorderedMethodPointers ? 2 : 0));
        var typeRows = ImmutableArray.Create(
            AuthorityTypeRow(module, 1, methodListRowId: 0, string.Empty, moduleTypeName,
                TypeAttributes.NotPublic),
            AuthorityTypeRow(module, 2, methodListRowId: 1, "Synthetic.ConstraintAuthority", "Pair`2",
                TypeAttributes.Public),
            AuthorityTypeRow(module, 3, methodListRowId: 2, "Synthetic.ConstraintAuthority", "AtLimit`64",
                TypeAttributes.Public),
            AuthorityTypeRow(module, 4, methodListRowId: 3, "Synthetic.ConstraintAuthority", "BeyondLimit`65",
                TypeAttributes.Public));
        var methodPointers = useReorderedMethodPointers
            ? MetadataMemberPointerTableCatalogIdentity.Create(
                sourceEnds,
                default,
                [
                    MetadataMemberPointerRowObservationIdentity.Create(module, 0x05000001, MethodToken(2)),
                    MetadataMemberPointerRowObservationIdentity.Create(module, 0x05000002, MethodToken(1)),
                ])
            : null;
        var typeDefinitions = methodPointers is null
            ? MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, typeRows)
            : MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, typeRows, methodPointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            omitGenericParameterObservations ? default : genericObservations);
        var methodDefinitions = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            [
                AuthorityMethodRow(module, 1, isStatic: true, [0x10, 0x02, 0x00, 0x01]),
                AuthorityMethodRow(module, 2, isStatic: false, [0x30, 0x01, 0x00, 0x01]),
            ]);
        var definitionAuthority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions);
        var genericParameterAuthority = MetadataGenericParameterAuthorityCatalogIdentity.Create(definitionAuthority);
        return new AuthorityScenario(
            module,
            sourceEnds,
            genericParameters,
            definitionAuthority,
            genericParameterAuthority);
    }

    private static AuthorityScenario BuildZeroGenericScenario()
    {
        var module = CreateMetadataModule();
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module.Module,
                module.ModuleContent,
                typeDefinitionsExamined: 1,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: 1,
                fieldDefinitionRowCount: 0,
                methodDefinitionRowCount: 0,
                nestedClassRowCount: 0,
                genericParameterRowCount: 0,
                genericParameterConstraintRowCount: 0));
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [AuthorityTypeRow(module, 1, 0, string.Empty, "<Module>", TypeAttributes.NotPublic)]);
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
        var definitionAuthority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions);
        var genericParameterAuthority = MetadataGenericParameterAuthorityCatalogIdentity.Create(definitionAuthority);
        return new AuthorityScenario(
            module,
            sourceEnds,
            genericParameters,
            definitionAuthority,
            genericParameterAuthority);
    }

    private static MetadataTypeDefinitionRowObservationIdentity AuthorityTypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int methodListRowId,
        string namespaceName,
        string typeName,
        TypeAttributes attributes) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            typeDefinitionToken: TypeToken(rowId),
            fieldListRowId: 0,
            methodListRowId: methodListRowId,
            namespaceName: namespaceName,
            typeName: typeName,
            typeAttributes: (int)attributes,
            extendsMetadataToken: null);

    private static MetadataMethodDefinitionRowObservationIdentity AuthorityMethodRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        bool isStatic,
        ImmutableArray<byte> signature) =>
        MetadataMethodDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            methodDefinitionToken: MethodToken(rowId),
            relativeVirtualAddress: 0x2400 + rowId * 0x20,
            implementationAttributes: (int)(MethodImplAttributes.NoInlining | MethodImplAttributes.NoOptimization),
            attributes: (int)(MethodAttributes.Public | MethodAttributes.HideBySig |
                              (isStatic ? MethodAttributes.Static : default)),
            name: $"ConstraintAuthorityMethod{rowId}",
            signaturePrefixBytes: signature,
            signatureByteCount: signature.Length,
            parameterListRowId: 0);

    private static ImmutableArray<MetadataGenericParameterRowObservationIdentity> AuthorityGenericParameterRows(
        StaticFieldMetadataModuleIdentity module,
        string pairLeftName)
    {
        var rows = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        void Add(int ownerToken, int number, string name) =>
            rows.Add(MetadataGenericParameterRowObservationIdentity.Create(
                metadataModule: module,
                genericParameterToken: GenericParameterToken(rows.Count + 1),
                number: number,
                flags: number % 2 == 0 ? 0 : 0x10,
                ownerMetadataToken: ownerToken,
                name: name));

        for (var number = 64; number >= 0; number--)
        {
            Add(TypeToken(4), number, $"Beyond{number}");
        }
        Add(MethodToken(2), 0, "MethodTwoOnly");
        Add(TypeToken(2), 1, "PairRight");
        Add(TypeToken(2), 0, pairLeftName);
        Add(MethodToken(1), 1, "MethodOneRight");
        Add(MethodToken(1), 0, "MethodOneLeft");
        for (var number = 63; number >= 0; number--)
        {
            Add(TypeToken(3), number, $"At{number}");
        }
        return rows.ToImmutable();
    }

    private static int TypeReferenceToken(int rowId) => 0x0100_0000 | rowId;

    private static int TypeToken(int rowId) => 0x0200_0000 | rowId;

    private static int MethodToken(int rowId) => 0x0600_0000 | rowId;

    private static int TypeSpecificationToken(int rowId) => 0x1B00_0000 | rowId;

    private static int GenericParameterToken(int rowId) => 0x2A00_0000 | rowId;

    private static int ConstraintToken(int rowId) => 0x2C00_0000 | rowId;

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
            $"generic-constraint-authority-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.GenericConstraintAuthority",
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

    private sealed record AuthorityScenario(
        StaticFieldMetadataModuleIdentity Module,
        MetadataSourceEndIdentity SourceEnds,
        MetadataGenericParameterPhysicalTableCatalogIdentity GenericParameters,
        MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority,
        MetadataGenericParameterAuthorityCatalogIdentity GenericParameterAuthority);
}
