using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises W8 physical metadata type construction with complex detached synthetic examples.</summary>
public sealed class W8MetadataConstructionContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves additive raw rows remain distinct from the pinned nested-argument mapping.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Raw_rows_and_pinned_nested_mapping_remain_separate()
    {
        var fixture = new SyntheticMetadataFixture();
        var raw = CreateRawType(
            fixture.Module,
            0x02000020,
            "Synthetic",
            "ReadableWithoutBacktick",
            genericParameterCount: 17);
        var parameters = CreateTypeParameters(raw, 17, genericParameterRowStart: 40);
        var readable = MetadataTypeDefinitionIdentity.Create(
            raw,
            MetadataTypeDefinitionKind.Class,
            parameters);

        Assert.Equal(17, raw.GenericParameterCount);
        Assert.Equal(17, readable.FlattenedArity);
        Assert.Equal(MetadataNestedGenericMappingStatus.NonExact, readable.GenericMappingStatus);
        Assert.Null(readable.IntroducedArity);
        Assert.Empty(readable.GenericParameterSegmentIndices);
        Assert.Equal(16, parameters[^1].Position);

        var nestedHead = MetadataTypeSignatureNode.Named(
            MetadataNamedSignatureHeadKind.Class,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Inner),
            [fixture.Outer, fixture.Inner]);
        var open = MetadataTypeConstructionResult.Classify(nestedHead);
        var ground = MetadataTypeSignatureNode.GenericInstantiation(
            nestedHead,
            [
                MetadataTypeSignatureNode.Primitive(MetadataPrimitiveTypeKind.Int32),
                MetadataTypeSignatureNode.Primitive(MetadataPrimitiveTypeKind.String),
            ]);
        var construction = MetadataTypeConstructionResult.Classify(ground);

        Assert.Equal(MetadataTypeConstructionResultKind.Open, open.Kind);
        Assert.Equal(MetadataTypeConstructionResultKind.Exact, construction.Kind);
        Assert.Equal(2, construction.ClosedType!.ConstructionSegments.Length);
        Assert.Equal(1, construction.ClosedType.ConstructionSegments[0].LocalArgumentCount);
        Assert.Equal(1, construction.ClosedType.ConstructionSegments[1].LocalArgumentCount);
        Assert.Equal(0, construction.ClosedType.ConstructionSegments[0].FlattenedArgumentOffset);
        Assert.Equal(1, construction.ClosedType.ConstructionSegments[1].FlattenedArgumentOffset);

    }

    /// <summary>Proves physical TypeDefOrRef edges retain their exact table-specific owners and targets.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Physical_targets_and_edges_retain_exact_table_rows()
    {
        var fixture = new SyntheticMetadataFixture();
        var baseTarget = MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.BaseType);
        var baseEdge = MetadataBaseTypeEdgeIdentity.Create(fixture.DerivedType, baseTarget);

        Assert.Equal(fixture.DerivedType, baseEdge.OwnerType);
        Assert.Equal(fixture.BaseType.TypeDefinitionToken, baseTarget.SourceMetadataToken);
        Assert.Null(baseTarget.TypeSpecification);
        Assert.Null(baseTarget.TypeReferenceResolution);
        Assert.Throws<ArgumentException>(() => MetadataBaseTypeEdgeIdentity.Create(
            fixture.DerivedType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha)));
        var otherModule = SyntheticMetadataFixture.CreateMetadataModule(
            moduleAddress: 0x3000,
            digestCharacter: 'b');
        var crossModuleRaw = CreateRawType(
            otherModule,
            fixture.BaseType.TypeDefinitionToken,
            "Synthetic",
            "Base",
            genericParameterCount: 0);
        var crossModuleTarget = MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(
            MetadataTypeDefinitionIdentity.Create(
                crossModuleRaw,
                MetadataTypeDefinitionKind.Class,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty));
        Assert.Throws<ArgumentException>(() => MetadataBaseTypeEdgeIdentity.Create(
            fixture.DerivedType,
            crossModuleTarget));
        Assert.Throws<ArgumentException>(() => MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000004,
            fixture.DerivedType,
            crossModuleTarget));

        var typeReferenceRow = StaticFieldTypeReferenceRowIdentity.Create(
            0x01000001,
            "Synthetic",
            "Pair`2",
            0x00000001);
        var resolution = StaticFieldTypeReferenceResolutionIdentity.ForModule(
            fixture.Module,
            [typeReferenceRow],
            fixture.PairPinned);
        var referenceTarget = MetadataTypeDefOrRefTargetIdentity.FromTypeReference(
            resolution,
            fixture.Pair);
        Assert.Same(resolution, referenceTarget.TypeReferenceResolution);
        Assert.Equal(typeReferenceRow.TypeReferenceToken, referenceTarget.SourceMetadataToken);
        Assert.Null(referenceTarget.TypeSpecification);
        var referenceDecode = Decode(
            fixture,
            3,
            [
                0x15,
                0x12,
                .. EncodeTypeDefOrRef(typeReferenceRow.TypeReferenceToken),
                0x02,
                0x08,
                0x0E,
            ],
            MetadataSignatureTokenResolutionEntry.Named(referenceTarget, [fixture.Pair]));
        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, referenceDecode.Kind);
        Assert.Equal(MetadataTypeDefOrRefTargetKind.TypeReference, referenceDecode.Root!.NamedTarget!.Kind);
        Assert.Same(resolution, referenceDecode.Root.NamedTarget.TypeReferenceResolution);

        var rowOne = MetadataTypeSpecificationRowIdentity.Create(
            MetadataTypeSpecificationRowReferenceIdentity.Create(fixture.Module, 0x1B000001),
            [0x08]);
        var rowTwo = MetadataTypeSpecificationRowIdentity.Create(
            MetadataTypeSpecificationRowReferenceIdentity.Create(fixture.Module, 0x1B000002),
            [0x08]);
        var specificationTarget = MetadataTypeDefOrRefTargetIdentity.FromTypeSpecification(rowOne);
        Assert.NotEqual(
            specificationTarget,
            MetadataTypeDefOrRefTargetIdentity.FromTypeSpecification(rowTwo));
        Assert.Equal(rowOne.SignatureBytes.ToArray(), specificationTarget.TypeSpecification!.SignatureBytes.ToArray());

        var implementation = MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000001,
            fixture.DerivedType,
            specificationTarget);
        var constraint = MetadataGenericParameterConstraintEdgeIdentity.Create(
            0x2C000001,
            fixture.MethodParameter,
            fixture.MethodContainer,
            MetadataTypeDefOrRefTargetIdentity.FromTypeSpecification(rowTwo));
        Assert.Equal(0x09000001, implementation.InterfaceImplementationToken);
        Assert.Equal(MetadataGenericParameterOwnerKind.MethodDefinition, constraint.OwnerParameter.Owner.Kind);
        Assert.Equal(rowTwo.Reference, constraint.Target.TypeSpecification!.Reference);
    }

    /// <summary>Proves ordered physical edge aggregates reject duplicates without normalizing row order.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ordered_edge_aggregates_reject_duplicates_and_preserve_order()
    {
        var fixture = new SyntheticMetadataFixture();
        var alpha = MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000001,
            fixture.DerivedType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha));
        var beta = MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000002,
            fixture.DerivedType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceBeta));
        var repeatedAlpha = MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000003,
            fixture.DerivedType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha));

        var twoInterfaceRows = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 2,
            genericParameterConstraintRowCount: 0);
        var threeInterfaceRows = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 3,
            genericParameterConstraintRowCount: 0);
        var ordered = MetadataInterfaceImplementationSetIdentity.Create(
            twoInterfaceRows,
            fixture.DerivedType,
            [alpha, beta]);
        var reversed = MetadataInterfaceImplementationSetIdentity.Create(
            twoInterfaceRows,
            fixture.DerivedType,
            [beta, alpha]);
        var duplicate = MetadataInterfaceImplementationSetIdentity.Create(
            threeInterfaceRows,
            fixture.DerivedType,
            [alpha, beta, repeatedAlpha]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Exact, ordered.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.None, ordered.Issue);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, reversed.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.PhysicalOrderInvalid, reversed.Issue);
        Assert.NotEqual(ordered, reversed);
        Assert.Equal(new[] { alpha.InterfaceImplementationToken, beta.InterfaceImplementationToken },
            ordered.Rows.Select(static row => row.InterfaceImplementationToken));
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, duplicate.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.DuplicateSemanticEdge, duplicate.Issue);
        Assert.Empty(duplicate.Rows);
        Assert.Equal(3, duplicate.CompleteTableRows.Length);
        Assert.Equal([repeatedAlpha.InterfaceImplementationToken], duplicate.DuplicateSemanticRowTokens.ToArray());

        var target = MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha);
        var firstConstraint = MetadataGenericParameterConstraintEdgeIdentity.Create(
            0x2C000001,
            fixture.MethodParameter,
            fixture.MethodContainer,
            target);
        var repeatedConstraint = MetadataGenericParameterConstraintEdgeIdentity.Create(
            0x2C000002,
            fixture.MethodParameter,
            fixture.MethodContainer,
            target);
        var constraintSourceEnds = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 0,
            genericParameterConstraintRowCount: 2);
        var constraints = MetadataGenericParameterConstraintSetIdentity.Create(
            CreateGenericParameterCatalog(fixture, constraintSourceEnds),
            fixture.MethodParameter,
            [firstConstraint, repeatedConstraint]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, constraints.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.DuplicateSemanticEdge, constraints.Issue);
        Assert.Equal([repeatedConstraint.GenericParameterConstraintToken], constraints.DuplicateSemanticRowTokens.ToArray());
        Assert.Empty(constraints.Rows);
        Assert.Equal(2, constraints.CompleteTableRows.Length);
    }

    /// <summary>Proves complete edge tables can contain other owners while source stops remain entirely factless.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Edge_aggregates_project_one_owner_only_after_complete_source_proof()
    {
        var fixture = new SyntheticMetadataFixture();
        var sourceEnds = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 2,
            genericParameterConstraintRowCount: 2);
        var selectedInterface = MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000002,
            fixture.DerivedType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha));
        var otherInterface = MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000001,
            fixture.BaseType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceBeta));

        var exactInterfaces = MetadataInterfaceImplementationSetIdentity.Create(
            sourceEnds,
            fixture.DerivedType,
            [otherInterface, selectedInterface]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Exact, exactInterfaces.ResultKind);
        Assert.Equal(2, exactInterfaces.CompleteTableRows.Length);
        Assert.Equal([selectedInterface.InterfaceImplementationToken],
            exactInterfaces.Rows.Select(static row => row.InterfaceImplementationToken));

        var crossedOwnerOrder = MetadataInterfaceImplementationSetIdentity.Create(
            sourceEnds,
            fixture.DerivedType,
            [
                MetadataInterfaceImplementationEdgeIdentity.Create(
                    0x09000001,
                    fixture.DerivedType,
                    MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha)),
                MetadataInterfaceImplementationEdgeIdentity.Create(
                    0x09000002,
                    fixture.BaseType,
                    MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceBeta)),
            ]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, crossedOwnerOrder.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.PhysicalOrderInvalid, crossedOwnerOrder.Issue);

        var incompleteInterfaces = MetadataInterfaceImplementationSetIdentity.Create(
            sourceEnds,
            fixture.DerivedType,
            [selectedInterface]);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, incompleteInterfaces.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.SourceIncomplete, incompleteInterfaces.Issue);
        Assert.Empty(incompleteInterfaces.CompleteTableRows);
        Assert.Empty(incompleteInterfaces.Rows);

        var surplusInterface = MetadataInterfaceImplementationEdgeIdentity.Create(
            0x09000003,
            fixture.DerivedType,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceBeta));
        var surplusInterfaces = MetadataInterfaceImplementationSetIdentity.Create(
            sourceEnds,
            fixture.DerivedType,
            [otherInterface, selectedInterface, surplusInterface]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, surplusInterfaces.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.SourceRowCountConflict, surplusInterfaces.Issue);
        Assert.Empty(surplusInterfaces.CompleteTableRows);
        Assert.Empty(surplusInterfaces.Rows);

        var oneInterfaceRow = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 1,
            genericParameterConstraintRowCount: 0);
        var outOfRangeTarget = MetadataInterfaceImplementationSetIdentity.Create(
            oneInterfaceRow,
            fixture.DerivedType,
            [
                MetadataInterfaceImplementationEdgeIdentity.Create(
                    0x09000001,
                    fixture.DerivedType,
                    MetadataTypeDefOrRefTargetIdentity.FromTypeSpecification(
                        MetadataTypeSpecificationRowIdentity.Create(
                            MetadataTypeSpecificationRowReferenceIdentity.Create(
                                fixture.Module,
                                0x1B000201),
                            [0x08]))),
            ]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, outOfRangeTarget.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.SourceTokenOutOfRange, outOfRangeTarget.Issue);

        var interfaceCapPlusOne = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: StaticFieldV2Limits.MaximumInterfaceImplementationRowCount + 1,
            genericParameterConstraintRowCount: 0);
        var boundedInterfaces = MetadataInterfaceImplementationSetIdentity.Create(
            interfaceCapPlusOne,
            fixture.DerivedType,
            ImmutableArray<MetadataInterfaceImplementationEdgeIdentity>.Empty);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, boundedInterfaces.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.BoundReached, boundedInterfaces.Issue);
        Assert.Equal(StaticFieldV2Limits.MaximumInterfaceImplementationRowCount + 1,
            boundedInterfaces.ObservedCount);
        Assert.Empty(boundedInterfaces.CompleteTableRows);

        var typeConstraint = MetadataGenericParameterConstraintEdgeIdentity.Create(
            0x2C000002,
            fixture.Pair.GenericParameters[0],
            fixture.Pair,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha));
        var methodConstraint = MetadataGenericParameterConstraintEdgeIdentity.Create(
            0x2C000001,
            fixture.MethodParameter,
            fixture.MethodContainer,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceBeta));
        var genericParameterCatalog = CreateGenericParameterCatalog(fixture, sourceEnds);
        var exactConstraints = MetadataGenericParameterConstraintSetIdentity.Create(
            genericParameterCatalog,
            fixture.MethodParameter,
            [methodConstraint, typeConstraint]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Exact, exactConstraints.ResultKind);
        Assert.Equal(genericParameterCatalog, exactConstraints.GenericParameterCatalog);
        Assert.Equal(2, exactConstraints.CompleteTableRows.Length);
        Assert.Equal([methodConstraint.GenericParameterConstraintToken],
            exactConstraints.Rows.Select(static row => row.GenericParameterConstraintToken));

        var incompleteConstraints = MetadataGenericParameterConstraintSetIdentity.Create(
            genericParameterCatalog,
            fixture.MethodParameter,
            [methodConstraint]);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, incompleteConstraints.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.SourceIncomplete, incompleteConstraints.Issue);
        Assert.Empty(incompleteConstraints.CompleteTableRows);
        Assert.Empty(incompleteConstraints.Rows);

        var absentConstraints = MetadataGenericParameterConstraintSetIdentity.Create(
            genericParameterCatalog,
            fixture.MethodParameter,
            default);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, absentConstraints.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.SourceIncomplete, absentConstraints.Issue);
        Assert.Empty(absentConstraints.CompleteTableRows);

        var surplusConstraint = MetadataGenericParameterConstraintEdgeIdentity.Create(
            0x2C000003,
            fixture.MethodParameter,
            fixture.MethodContainer,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Int32));
        var surplusConstraints = MetadataGenericParameterConstraintSetIdentity.Create(
            genericParameterCatalog,
            fixture.MethodParameter,
            [methodConstraint, typeConstraint, surplusConstraint]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, surplusConstraints.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.SourceRowCountConflict, surplusConstraints.Issue);
        Assert.Empty(surplusConstraints.CompleteTableRows);
        Assert.Empty(surplusConstraints.Rows);

        var forgedParameter = MetadataGenericParameterIdentity.Create(
            fixture.MethodParameter.GenericParameterToken,
            fixture.Pair.GenericParameters[0].Owner,
            position: 1,
            "TForgedOwnerAndPosition",
            attributes: 0);
        var forgedSelection = MetadataGenericParameterConstraintSetIdentity.Create(
            genericParameterCatalog,
            forgedParameter,
            [methodConstraint, typeConstraint]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, forgedSelection.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.GenericParameterCatalogMismatch, forgedSelection.Issue);
        Assert.Empty(forgedSelection.CompleteTableRows);
        Assert.Empty(forgedSelection.Rows);

        var oneConstraintSourceEnds = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 0,
            genericParameterConstraintRowCount: 1);
        var forgedConstraint = MetadataGenericParameterConstraintEdgeIdentity.Create(
            0x2C000001,
            forgedParameter,
            fixture.Pair,
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.InterfaceAlpha));
        var forgedEdgeOwner = MetadataGenericParameterConstraintSetIdentity.Create(
            CreateGenericParameterCatalog(fixture, oneConstraintSourceEnds),
            fixture.MethodParameter,
            [forgedConstraint]);
        Assert.Equal(MetadataEdgeAggregateResultKind.Invalid, forgedEdgeOwner.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.GenericParameterCatalogMismatch, forgedEdgeOwner.Issue);
        Assert.Empty(forgedEdgeOwner.CompleteTableRows);
        Assert.Empty(forgedEdgeOwner.Rows);

        var incompleteCatalog = MetadataGenericParameterTableCatalogIdentity.Create(sourceEnds, default);
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, incompleteCatalog.ResultKind);
        Assert.Throws<ArgumentException>(() => MetadataGenericParameterConstraintSetIdentity.Create(
            incompleteCatalog,
            fixture.MethodParameter,
            [methodConstraint, typeConstraint]));

        var constraintCapPlusOne = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 0,
            genericParameterConstraintRowCount:
                StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount + 1);
        var boundedConstraints = MetadataGenericParameterConstraintSetIdentity.Create(
            CreateGenericParameterCatalog(fixture, constraintCapPlusOne),
            fixture.MethodParameter,
            ImmutableArray<MetadataGenericParameterConstraintEdgeIdentity>.Empty);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, boundedConstraints.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.BoundReached, boundedConstraints.Issue);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName,
            boundedConstraints.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount + 1,
            boundedConstraints.ObservedCount);
        Assert.Empty(boundedConstraints.CompleteTableRows);

        var selectedConstraintCount = StaticFieldV2Limits.MaximumGenericConstraintCount + 1;
        var selectedConstraintSourceEnds = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 0,
            genericParameterConstraintRowCount: selectedConstraintCount);
        var selectedConstraintRows = Enumerable.Range(1, selectedConstraintCount)
            .Select(rowId => MetadataGenericParameterConstraintEdgeIdentity.Create(
                0x2C000000 | rowId,
                fixture.MethodParameter,
                fixture.MethodContainer,
                MetadataTypeDefOrRefTargetIdentity.FromTypeSpecification(
                    MetadataTypeSpecificationRowIdentity.Create(
                        MetadataTypeSpecificationRowReferenceIdentity.Create(
                            fixture.Module,
                            0x1B000000 | rowId),
                        [0x08]))))
            .ToImmutableArray();
        var selectedConstraintBound = MetadataGenericParameterConstraintSetIdentity.Create(
            CreateGenericParameterCatalog(fixture, selectedConstraintSourceEnds),
            fixture.MethodParameter,
            selectedConstraintRows);
        Assert.Equal(MetadataEdgeAggregateResultKind.NonExact, selectedConstraintBound.ResultKind);
        Assert.Equal(MetadataEdgeAggregateIssue.BoundReached, selectedConstraintBound.Issue);
        Assert.Equal(ExpressionV2ContractLimits.GenericConstraintCountBoundName,
            selectedConstraintBound.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericConstraintCount + 1,
            selectedConstraintBound.ObservedCount);
        Assert.Empty(selectedConstraintBound.CompleteTableRows);
        Assert.Empty(selectedConstraintBound.Rows);
    }

    /// <summary>Proves the real decoder preserves generic heads, exact ARRAY vectors, and complete source bytes.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Decoder_projects_complex_generic_array_signature_from_complete_bytes()
    {
        var fixture = new SyntheticMetadataFixture();
        var signature = ImmutableArray.CreateBuilder<byte>();
        signature.Add(0x15);
        signature.Add(0x12);
        signature.AddRange(EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken));
        signature.Add(0x02);
        signature.Add(0x14);
        signature.Add(0x08);
        signature.Add(0x02);
        signature.Add(0x02);
        signature.AddRange(EncodeCompressedUnsigned(0));
        signature.AddRange(EncodeCompressedUnsigned(0x1FFF_FFFF));
        signature.Add(0x02);
        signature.AddRange(EncodeCompressedSigned(-0x1000_0000));
        signature.AddRange(EncodeCompressedSigned(0x0FFF_FFFF));
        signature.Add(0x1D);
        signature.Add(0x0E);

        var outcome = Decode(
            fixture,
            rowId: 1,
            signature.ToImmutable(),
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Pair),
                [fixture.Pair]));
        var specification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(outcome);

        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, outcome.Kind);
        Assert.NotNull(outcome.Certificate);
        Assert.Equal(signature.Count, outcome.Certificate!.ConsumedByteCount);
        Assert.Equal(MetadataTypeSignatureNodeKind.GenericInstantiation, specification.Root.Kind);
        Assert.Equal(MetadataNamedSignatureHeadKind.Class, specification.Root.NamedHeadKind);
        Assert.Equal(2, specification.Root.Children.Length);
        var array = specification.Root.Children[0];
        Assert.Equal(MetadataTypeSignatureNodeKind.MultidimensionalArray, array.Kind);
        Assert.Equal(2, array.ArrayRank);
        Assert.Equal([0, 0x1FFF_FFFF], array.ArraySizes.ToArray());
        Assert.Equal([-0x1000_0000, 0x0FFF_FFFF], array.ArrayLowerBounds.ToArray());
        Assert.Equal(MetadataTypeSignatureNodeKind.SzArray, specification.Root.Children[1].Kind);
        Assert.Equal(MetadataTypeConstructionResultKind.Exact, specification.Construction.Kind);
        Assert.Equal(signature.ToArray(), specification.OriginalBytes.ToArray());

        var rankOneArray = MetadataTypeSpecificationIdentity.FromDecodeOutcome(
            Decode(fixture, 2, [0x14, 0x08, 0x01, 0x00, 0x00]));
        var vector = MetadataTypeSpecificationIdentity.FromDecodeOutcome(
            Decode(fixture, 3, [0x1D, 0x08]));
        Assert.Equal(MetadataTypeSignatureNodeKind.MultidimensionalArray, rankOneArray.Root.Kind);
        Assert.Equal(1, rankOneArray.Root.ArrayRank);
        Assert.Equal(MetadataTypeSignatureNodeKind.SzArray, vector.Root.Kind);
        Assert.NotEqual(rankOneArray.Construction.ClosedType, vector.Construction.ClosedType);
    }

    /// <summary>Proves direct non-generic CLASS and VALUETYPE TypeSpec roots enter normal construction classification.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Direct_named_typespec_roots_are_admitted_by_normal_construction_classification()
    {
        var fixture = new SyntheticMetadataFixture();
        var classOutcome = Decode(
            fixture,
            20,
            [0x12, .. EncodeTypeDefOrRef(fixture.BaseType.TypeDefinitionToken)],
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.BaseType),
                [fixture.BaseType]));
        var valueOutcome = Decode(
            fixture,
            21,
            [0x11, .. EncodeTypeDefOrRef(fixture.Int32.TypeDefinitionToken)],
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Int32),
                [fixture.Int32]));

        var classSpecification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(classOutcome);
        var valueSpecification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(valueOutcome);
        Assert.Equal(MetadataTypeConstructionResultKind.Exact, classSpecification.Construction.Kind);
        Assert.Equal(MetadataClosedTypeKind.Named, classSpecification.Construction.ClosedType!.Kind);
        Assert.Equal(MetadataTypeConstructionResultKind.Exact, valueSpecification.Construction.Kind);
        Assert.Equal(MetadataClosedTypeKind.Named, valueSpecification.Construction.ClosedType!.Kind);

        var openGeneric = MetadataTypeSpecificationIdentity.FromDecodeOutcome(Decode(
            fixture,
            22,
            [0x12, .. EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken)],
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Pair),
                [fixture.Pair])));
        Assert.Equal(MetadataTypeConstructionResultKind.Open, openGeneric.Construction.Kind);
        Assert.Null(openGeneric.Construction.ClosedType);
    }

    /// <summary>Proves decoder outcomes distinguish exact source end, malformed source, and independent bounds.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Decoder_outcomes_cover_source_end_malformed_and_cap_plus_one()
    {
        var fixture = new SyntheticMetadataFixture();
        var exactAtByteCap = ImmutableArray.CreateBuilder<byte>(
            StaticFieldV2Limits.MaximumTypeSpecificationByteCount);
        exactAtByteCap.Add(0x1B);
        exactAtByteCap.Add(0x00);
        exactAtByteCap.AddRange(EncodeCompressedUnsigned(251));
        exactAtByteCap.Add(0x08);
        for (var index = 0; index < 251; index++)
        {
            exactAtByteCap.Add(0x08);
        }
        Assert.Equal(StaticFieldV2Limits.MaximumTypeSpecificationByteCount, exactAtByteCap.Count);
        var exact = Decode(fixture, 1, exactAtByteCap.ToImmutable());
        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, exact.Kind);
        Assert.Equal(exactAtByteCap.Count, exact.CumulativeByteCount);

        var byteBound = Decode(fixture, 2, exactAtByteCap.Append((byte)0x08).ToImmutableArray());
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, byteBound.Kind);
        Assert.Null(byteBound.Row);
        Assert.Null(byteBound.Root);
        Assert.Equal(ExpressionV2ContractLimits.TypeSpecificationByteCountBoundName, byteBound.ReachedBound!.Name);

        var nodeBound = Decode(fixture, 3, [0x1B, 0x00, 0x81, 0x00]);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, nodeBound.Kind);
        Assert.Equal(ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName, nodeBound.ReachedBound!.Name);
        Assert.Null(nodeBound.Root);

        var genericArgumentBound = Decode(
            fixture,
            4,
            [0x15, 0x12, .. EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken), 0x41],
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Pair),
                [fixture.Pair]));
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, genericArgumentBound.Kind);
        Assert.Equal(ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
            genericArgumentBound.ReachedBound!.Name);

        var arrayRankBound = Decode(fixture, 8, [0x14, 0x08, 0x21]);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, arrayRankBound.Kind);
        Assert.Equal(ExpressionV2ContractLimits.ArrayRankBoundName, arrayRankBound.ReachedBound!.Name);
        var functionGenericBound = Decode(fixture, 9, [0x1B, 0x10, 0x41]);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, functionGenericBound.Kind);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterCountBoundName,
            functionGenericBound.ReachedBound!.Name);

        var malformed = Decode(fixture, 5, [0x08, 0x08]);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, malformed.Kind);
        Assert.Equal([0x08, 0x08], malformed.Row!.SignatureBytes.ToArray());
        Assert.Null(malformed.Root);
        Assert.Null(malformed.Certificate);
        Assert.Equal("W8_SIGNATURE_TRAILING_BYTES", malformed.InvalidCode);

        var typeSpecificationReference = MetadataTypeSpecificationRowReferenceIdentity.Create(
            fixture.Module,
            0x1B000006);
        var invalidHead = Decode(
            fixture,
            6,
            [0x12, .. EncodeTypeDefOrRef(typeSpecificationReference.TypeSpecificationToken)],
            MetadataSignatureTokenResolutionEntry.TypeSpecification(typeSpecificationReference));
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, invalidHead.Kind);
        Assert.Equal("W8_SIGNATURE_TYPESPEC_HEAD_INVALID", invalidHead.InvalidCode);

        var deep = ImmutableArray.CreateRange(
            Enumerable.Repeat((byte)0x1D, StaticFieldV2Limits.MaximumClosedTypeTopologyDepth)
                .Append((byte)0x08));
        var deepSpecification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(Decode(fixture, 7, deep));
        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, deepSpecification.Decode.Kind);
        Assert.Equal(MetadataTypeConstructionResultKind.NonExact, deepSpecification.Construction.Kind);
        Assert.Equal(ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName,
            deepSpecification.Construction.ReachedBound!.Name);
    }

    /// <summary>Proves admitted FNPTR conventions stay structured while generic-vararg and sentinel forms are invalid.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Function_pointer_conventions_are_structured_and_nested_variables_are_never_hidden()
    {
        var fixture = new SyntheticMetadataFixture();
        var bytes = ImmutableArray.Create<byte>(
            0x1B, 0x10, 0x01, 0x03, 0x08, 0x13, 0x00, 0x1E, 0x00, 0x0F, 0x01);
        var functionPointer = Decode(fixture, 1, bytes).Root!;

        Assert.Equal(MetadataTypeSignatureNodeKind.FunctionPointer, functionPointer.Kind);
        Assert.Equal(0x10, functionPointer.FunctionPointerHeader);
        Assert.Equal(1, functionPointer.FunctionPointerGenericParameterCount);
        Assert.Equal(3, functionPointer.FunctionPointerParameterCount);
        Assert.Equal(3, functionPointer.FunctionPointerRequiredParameterCount);
        Assert.Equal(bytes.ToArray(), functionPointer.OpaqueSignatureBytes.ToArray());
        Assert.Equal(MetadataTypeSignatureNodeKind.OwnerTypeParameter, functionPointer.Children[1].Kind);
        Assert.Equal(MetadataTypeSignatureNodeKind.MethodTypeParameter, functionPointer.Children[2].Kind);
        Assert.Equal(MetadataTypeSignatureNodeKind.Void, functionPointer.Children[3].Children[0].Kind);

        ImmutableArray<byte> unmanagedBytes = [0x1B, 0x09, 0x01, 0x08, 0x1D, 0x0E];
        var unmanaged = Decode(fixture, 2, unmanagedBytes).Root!;
        Assert.Equal(MetadataTypeSignatureNodeKind.FunctionPointer, unmanaged.Kind);
        Assert.Equal(0x09, unmanaged.FunctionPointerHeader);
        Assert.Equal(1, unmanaged.FunctionPointerRequiredParameterCount);
        Assert.Equal(MetadataTypeSignatureNodeKind.SzArray, unmanaged.Children[1].Kind);
        Assert.Equal(unmanagedBytes.ToArray(), unmanaged.OpaqueSignatureBytes.ToArray());

        var genericVararg = Decode(fixture, 3, [0x1B, 0x15, 0x01, 0x00, 0x08]);
        var sentinel = Decode(fixture, 4, [0x1B, 0x05, 0x02, 0x08, 0x13, 0x00, 0x41, 0x1E, 0x00]);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, genericVararg.Kind);
        Assert.Equal("W8_SIGNATURE_FNPTR_HEADER_INVALID", genericVararg.InvalidCode);
        Assert.Null(genericVararg.Root);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, sentinel.Kind);
        Assert.Equal("W8_SIGNATURE_FNPTR_SENTINEL_INVALID", sentinel.InvalidCode);
        Assert.Null(sentinel.Root);

    }

    /// <summary>Proves modifier edges reject cycles while equal blobs at distinct physical TypeSpec rows remain valid.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Modifier_typespec_graph_represents_cycles_missing_rows_and_duplicate_blobs()
    {
        var fixture = new SyntheticMetadataFixture();
        var first = MetadataTypeSpecificationRowReferenceIdentity.Create(fixture.Module, 0x1B000001);
        var second = MetadataTypeSpecificationRowReferenceIdentity.Create(fixture.Module, 0x1B000002);
        var catalog = MetadataSignatureTokenResolutionCatalog.Create(
            fixture.SourceEnds,
            [
                MetadataSignatureTokenResolutionEntry.TypeSpecification(first),
                MetadataSignatureTokenResolutionEntry.TypeSpecification(second),
            ]);
        ImmutableArray<byte> firstBytes =
            [0x1F, .. EncodeTypeDefOrRef(second.TypeSpecificationToken), 0x08];
        ImmutableArray<byte> secondBytes =
            [0x20, .. EncodeTypeDefOrRef(first.TypeSpecificationToken), 0x0E];
        var firstOutcome = MetadataTypeSignatureDecoder.DecodeTypeSpecification(first, firstBytes, catalog);
        var secondOutcome = MetadataTypeSignatureDecoder.DecodeTypeSpecification(second, secondBytes, catalog);
        Assert.Equal(second, firstOutcome.Root!.ModifierTypeSpecificationReference);

        var cycle = MetadataTypeSpecificationGraphIdentity.Create(first, [firstOutcome, secondOutcome]);
        var missing = MetadataTypeSpecificationGraphIdentity.Create(first, [firstOutcome]);
        Assert.Equal(MetadataTypeSpecificationGraphResultKind.Invalid, cycle.ResultKind);
        Assert.Equal(MetadataTypeSpecificationGraphResultKind.NonExact, missing.ResultKind);

        var duplicateCatalog = MetadataSignatureTokenResolutionCatalog.Create(
            fixture.SourceEnds,
            ImmutableArray<MetadataSignatureTokenResolutionEntry>.Empty);
        var duplicateOne = MetadataTypeSignatureDecoder.DecodeTypeSpecification(first, [0x08], duplicateCatalog);
        var duplicateTwo = MetadataTypeSignatureDecoder.DecodeTypeSpecification(second, [0x08], duplicateCatalog);
        var equalBlobRows = MetadataTypeSpecificationGraphIdentity.Create(first, [duplicateOne, duplicateTwo]);
        Assert.Equal(MetadataTypeSpecificationGraphResultKind.Exact, equalBlobRows.ResultKind);
        Assert.Equal(2, equalBlobRows.InputCatalog.Outcomes.Length);
        Assert.Single(equalBlobRows.DecodeOutcomes);
        Assert.Equal(first, equalBlobRows.DecodeOutcomes[0].Reference);

        var use = MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.TypeSpecification,
            MetadataTypeSpecificationIdentity.FromDecodeOutcome(firstOutcome),
            cycle);
        Assert.Equal(MetadataTypeUseResultKind.Invalid, use.Kind);
    }

    /// <summary>Proves graph traversal reaches the exact row cap and reports cap-plus-one independently.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Typespec_graph_row_bound_has_exact_at_cap_and_nonexact_at_cap_plus_one()
    {
        var fixture = new SyntheticMetadataFixture();
        var exact = CreateModifierChainGraph(
            fixture,
            StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount);
        var over = CreateModifierChainGraph(
            fixture,
            StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount + 1);

        Assert.Equal(MetadataTypeSpecificationGraphResultKind.Exact, exact.ResultKind);
        Assert.Equal(StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount, exact.VisitedRowCount);
        Assert.Null(exact.ReachedBound);
        Assert.Equal(MetadataTypeSpecificationGraphResultKind.NonExact, over.ResultKind);
        Assert.Equal(0, over.VisitedRowCount);
        Assert.Empty(over.DecodeOutcomes);
        Assert.Equal(
            StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount + 1,
            over.ReachedBoundObservedCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount + 1,
            over.InputCatalog.Outcomes.Length);
        Assert.Equal(ExpressionV2ContractLimits.TypeSpecificationRowTraversalCountBoundName,
            over.ReachedBound!.Name);
    }

    /// <summary>Proves TypeSpec positional grammar precedes role classification while method-owner context stays separate.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Typespec_position_rejects_byref_void_typedbyref_before_role_classification()
    {
        var fixture = new SyntheticMetadataFixture();
        var methodVariable = SpecificationAndGraph(fixture, 1, [0x1E, 0x00]);
        Assert.Equal(MetadataTypeUseResultKind.Invalid, MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.FieldDefinition,
            methodVariable.Specification,
            methodVariable.Graph).Kind);
        Assert.Equal(MetadataTypeUseResultKind.Invalid, MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.BaseType,
            methodVariable.Specification,
            methodVariable.Graph).Kind);
        Assert.Equal(MetadataTypeUseResultKind.Invalid, MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.GenericParameterConstraint,
            methodVariable.Specification,
            methodVariable.Graph,
            fixture.Pair.GenericParameters[0]).Kind);
        Assert.Equal(MetadataTypeUseResultKind.Open, MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.GenericParameterConstraint,
            methodVariable.Specification,
            methodVariable.Graph,
            fixture.MethodParameter).Kind);
        Assert.Equal(MetadataTypeUseResultKind.Open, MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.TypeSpecification,
            methodVariable.Specification,
            methodVariable.Graph).Kind);

        var byReference = Decode(fixture, 2, [0x10, 0x08]);
        var directVoid = Decode(fixture, 3, [0x01]);
        var voidPointer = SpecificationAndGraph(fixture, 4, [0x0F, 0x01]);
        var typedReference = Decode(fixture, 5, [0x16]);
        Assert.All([byReference, directVoid, typedReference], static outcome =>
        {
            Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, outcome.Kind);
            Assert.Equal("W8_SIGNATURE_TYPE_PLACEMENT_INVALID", outcome.InvalidCode);
            Assert.Null(outcome.Root);
            Assert.Null(outcome.Certificate);
        });
        Assert.Equal(MetadataTypeUseResultKind.Unsupported, MetadataTypeUseResult.Classify(
            MetadataTypeUseRole.FieldDefinition,
            voidPointer.Specification,
            voidPointer.Graph).Kind);
    }

    /// <summary>Proves a complex FieldSig anchors generic VAR topology, Core counters, and immutable exact evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_signature_identity_anchors_complex_generic_var_projection_and_is_immutable()
    {
        var fixture = new SyntheticMetadataFixture();
        ImmutableArray<byte> signature =
        [
            0x06,
            0x15,
            0x12,
            .. EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken),
            0x02,
            0x13,
            0x00,
            0x1D,
            0x08,
        ];
        var field = CreateFieldDefinition(fixture, rowId: 1, signature);
        var catalog = MetadataSignatureTokenResolutionCatalog.Create(
            fixture.SourceEnds,
            [MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Pair),
                [fixture.Pair])]);

        var outcome = MetadataFieldSignatureDecodeOutcome.Decode(field, fixture.SourceEnds, catalog);
        var identity = Assert.IsType<MetadataFieldSignatureIdentity>(outcome.Identity);
        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, outcome.Kind);
        Assert.Same(field, outcome.FieldDefinition);
        Assert.Equal(fixture.SourceEnds, identity.SourceEnds);
        Assert.Equal(signature.ToArray(), identity.SignatureBytes.ToArray());
        Assert.Equal(MetadataTypeSignatureNodeKind.GenericInstantiation, identity.Root.Kind);
        Assert.Equal(MetadataTypeSignatureNodeKind.OwnerTypeParameter, identity.Root.Children[0].Kind);
        Assert.Equal(0, identity.Root.Children[0].VariableIndex);
        Assert.Equal(MetadataTypeSignatureNodeKind.SzArray, identity.Root.Children[1].Kind);
        Assert.Equal(0x06, identity.Certificate.Header);
        Assert.Equal(signature.Length, identity.Certificate.SignatureByteCount);
        Assert.Equal(4, identity.Certificate.AggregateTypeCount);
        Assert.Equal(2, identity.Certificate.AggregateGenericArgumentCount);
        Assert.Equal(3, identity.Certificate.MaximumObservedDepth);
        Assert.Equal(5, identity.Certificate.ProjectedNodeCount);
        Assert.True(identity.Certificate.SourceEndObserved);

        var replay = MetadataFieldSignatureDecodeOutcome.Decode(field, fixture.SourceEnds, catalog);
        Assert.Equal(outcome, replay);
        Assert.Equal(outcome.Sha256, replay.Sha256);

        var signatureCopy = identity.SignatureBytes;
        var mutableSignatureCopy = ImmutableCollectionsMarshal.AsArray(signatureCopy)!;
        mutableSignatureCopy[0] = 0x07;
        Assert.Equal(0x06, identity.SignatureBytes[0]);
        var canonicalCopy = outcome.CanonicalBytes;
        var mutableCanonicalCopy = ImmutableCollectionsMarshal.AsArray(canonicalCopy)!;
        mutableCanonicalCopy[0] ^= 0x7F;
        Assert.Equal(replay.CanonicalBytes.ToArray(), outcome.CanonicalBytes.ToArray());
    }

    /// <summary>Proves every FieldSig stop is typed, source-anchored, and exposes no draft projection prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_signature_stops_are_prefix_free_for_incomplete_invalid_and_bounded_inputs()
    {
        var fixture = new SyntheticMetadataFixture();
        var emptyCatalog = MetadataSignatureTokenResolutionCatalog.Create(
            fixture.SourceEnds,
            ImmutableArray<MetadataSignatureTokenResolutionEntry>.Empty);

        var incomplete = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(
                fixture,
                2,
                [0x06, 0x12, .. EncodeTypeDefOrRef(fixture.BaseType.TypeDefinitionToken)]),
            fixture.SourceEnds,
            emptyCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, incomplete.Kind);
        Assert.Equal("W8_SIGNATURE_TOKEN_RESOLUTION_INCOMPLETE", incomplete.NonExactCode);
        AssertFieldStopHasNoProjection(incomplete);

        var outOfRangeToken = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(
                fixture,
                3,
                [0x06, 0x12, .. EncodeTypeDefOrRef(0x02000101)]),
            fixture.SourceEnds,
            emptyCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, outOfRangeToken.Kind);
        Assert.Equal("W8_SIGNATURE_TOKEN_OUT_OF_RANGE", outOfRangeToken.InvalidCode);
        AssertFieldStopHasNoProjection(outOfRangeToken);

        var forbiddenTypeSpecificationHead = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(
                fixture,
                4,
                [0x06, 0x12, .. EncodeTypeDefOrRef(0x1B000001)]),
            fixture.SourceEnds,
            emptyCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, forbiddenTypeSpecificationHead.Kind);
        Assert.Equal("W8_SIGNATURE_TYPESPEC_HEAD_INVALID", forbiddenTypeSpecificationHead.InvalidCode);
        AssertFieldStopHasNoProjection(forbiddenTypeSpecificationHead);

        foreach (var (rowId, signature) in new (int RowId, ImmutableArray<byte> Signature)[]
                 {
                     (5, [0x06, 0x01]),
                     (6, [0x06, 0x10, 0x08]),
                     (7, [0x06, 0x16]),
                     (8, [0x07, 0x08]),
                 })
        {
            var invalid = MetadataFieldSignatureDecodeOutcome.Decode(
                CreateFieldDefinition(fixture, rowId, signature),
                fixture.SourceEnds,
                emptyCatalog);
            Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, invalid.Kind);
            AssertFieldStopHasNoProjection(invalid);
        }

        var genericCountBound = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(fixture, 9, [0x06, 0x1B, 0x10, 0x41]),
            fixture.SourceEnds,
            emptyCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, genericCountBound.Kind);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterCountBoundName,
            genericCountBound.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount + 1, genericCountBound.ObservedCount);
        AssertFieldStopHasNoProjection(genericCountBound);

        var outOfRangeField = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(fixture, rowId: 257, [0x06, 0x08]),
            fixture.SourceEnds,
            emptyCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, outOfRangeField.Kind);
        Assert.Equal("W8_FIELD_SIGNATURE_SOURCE_TOKEN_OUT_OF_RANGE", outOfRangeField.InvalidCode);
        AssertFieldStopHasNoProjection(outOfRangeField);

        var outOfRangeOwner = StaticFieldTypeDefinitionIdentity.Create(
            fixture.Module,
            0x02000101,
            fieldListRowId: 1,
            fieldListEndExclusiveRowId: 2,
            methodListRowId: 1,
            methodListEndExclusiveRowId: 1,
            "Synthetic",
            "OutOfRangeOwner",
            (int)(TypeAttributes.Public | TypeAttributes.Class),
            genericParameterCount: 0,
            introducedGenericArity: 0,
            extendsMetadataToken: 0x02000001,
            enclosingType: null);
        var ownerStop = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(fixture, rowId: 1, [0x06, 0x08], outOfRangeOwner),
            fixture.SourceEnds,
            emptyCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, ownerStop.Kind);
        Assert.Equal("W8_FIELD_SIGNATURE_SOURCE_OWNER_OUT_OF_RANGE", ownerStop.InvalidCode);
        AssertFieldStopHasNoProjection(ownerStop);
    }

    /// <summary>Proves wrapper-inclusive Product depth is exact at its draft cap and prefix-free at cap-plus-one.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_signature_modifier_chain_observes_product_depth_cap_inclusively()
    {
        var fixture = new SyntheticMetadataFixture();
        var catalog = MetadataSignatureTokenResolutionCatalog.Create(
            fixture.SourceEnds,
            [MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.BaseType),
                [fixture.BaseType])]);
        var exactBytes = CreateFieldModifierChain(
            fixture.BaseType.TypeDefinitionToken,
            StaticFieldV2Limits.MaximumTypeSpecificationDepth - 1);
        var overBytes = CreateFieldModifierChain(
            fixture.BaseType.TypeDefinitionToken,
            StaticFieldV2Limits.MaximumTypeSpecificationDepth);

        var exact = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(fixture, 10, exactBytes),
            fixture.SourceEnds,
            catalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, exact.Kind);
        Assert.Equal(StaticFieldV2Limits.MaximumTypeSpecificationDepth, exact.Root!.TopologyDepth);

        var over = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(fixture, 11, overBytes),
            fixture.SourceEnds,
            catalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.NonExact, over.Kind);
        Assert.Equal(ExpressionV2ContractLimits.TypeSpecificationDepthBoundName, over.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumTypeSpecificationDepth + 1, over.ObservedCount);
        AssertFieldStopHasNoProjection(over);
    }

    /// <summary>Proves a complex decoded FieldSig substitutes only through its exact declaring-TypeDef proof.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_substitution_is_source_anchored_recursive_canonical_and_immutable()
    {
        var fixture = new SyntheticMetadataFixture();
        var sourceEnds = CreateAggregateSourceEnds(
            fixture.Module,
            interfaceImplementationRowCount: 0,
            genericParameterConstraintRowCount: 0);
        ImmutableArray<byte> signature =
        [
            0x06,
            0x15,
            0x12,
            .. EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken),
            0x02,
            0x13,
            0x00,
            0x14,
            0x15,
            0x12,
            .. EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken),
            0x02,
            0x1D,
            0x13,
            0x01,
            0x0E,
            0x02,
            0x00,
            0x00,
        ];
        var fieldSignature = DecodeFieldSignature(
            fixture,
            sourceEnds,
            CreateFieldDefinition(fixture, rowId: 20, signature),
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Pair),
                [fixture.Pair]));
        var ownerSet = CreateOwnerSet(fixture, sourceEnds, fixture.FieldOwnerPinned);
        var int32 = SourceDerivedPrimitive(fixture, rowId: 500, elementType: 0x08);
        var text = SourceDerivedPrimitive(fixture, rowId: 499, elementType: 0x0E);
        var first = MetadataTypeArgumentBindingIdentity.Exact(ownerSet.Parameters[0], int32);
        var second = MetadataTypeArgumentBindingIdentity.Exact(ownerSet.Parameters[1], text);
        var forwardLedger = MetadataGenericParameterBindingLedgerIdentity.Create(ownerSet, [first, second]);
        var reversedLedger = MetadataGenericParameterBindingLedgerIdentity.Create(ownerSet, [second, first]);
        Assert.Equal(forwardLedger, reversedLedger);

        var request = MetadataTypeSubstitutionRequest.ForFieldDefinition(fieldSignature, reversedLedger);
        var equivalentRequest = MetadataTypeSubstitutionRequest.ForFieldDefinition(fieldSignature, forwardLedger);
        var exact = MetadataTypeSubstitutionResult.Substitute(request);

        Assert.Equal(equivalentRequest, request);
        Assert.Equal(request.Sha256, equivalentRequest.Sha256);
        Assert.Equal(MetadataTypeSubstitutionContextKind.FieldDefinition, request.ContextKind);
        Assert.Equal(fieldSignature, request.FieldSignature);
        Assert.Equal(fieldSignature.Root, request.Root);
        Assert.Equal(reversedLedger, request.DeclaringTypeBindings);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, request.BindingProofResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.None, request.BindingProofIssue);
        Assert.Null(request.ReachedBound);
        Assert.Equal(MetadataTypeSubstitutionResultKind.Exact, exact.Kind);
        Assert.Equal(request.Root, exact.Root);
        Assert.Equal(MetadataClosedTypeKind.Named, exact.ClosedType!.Kind);
        Assert.Equal(2, exact.ClosedType.FlattenedArguments.Length);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, exact.ClosedType.FlattenedArguments[0].PrimitiveKind);
        Assert.Equal(MetadataClosedTypeKind.MultidimensionalArray, exact.ClosedType.FlattenedArguments[1].Kind);
        Assert.Equal(2, exact.ClosedType.FlattenedArguments[1].ArrayRank);
        Assert.Equal(2, exact.SubstitutedTree.Children.Length);
        Assert.Equal(first, exact.SubstitutedTree.Children[0].AppliedBinding);
        var nestedVariable = exact.SubstitutedTree.Children[1].Children[0].Children[0].Children[0];
        Assert.Equal(second, nestedVariable.AppliedBinding);
        Assert.Equal(text, nestedVariable.Replacement);

        var unavailableLedger = MetadataGenericParameterBindingLedgerIdentity.Create(
            ownerSet,
            [first, MetadataTypeArgumentBindingIdentity.Unavailable(ownerSet.Parameters[1])]);
        var open = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(fieldSignature, unavailableLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.Open, open.Kind);
        Assert.Equal([1], open.UnresolvedOwnerVariableIndices.ToArray());
        Assert.Null(open.ClosedType);

        var returnedCanonical = request.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedCanonical)![0] ^= 0x7F;
        Assert.Equal(equivalentRequest.CanonicalBytes.ToArray(), request.CanonicalBytes.ToArray());
        var returnedBindings = request.DeclaringTypeBindings.Bindings;
        ImmutableCollectionsMarshal.AsArray(returnedBindings)![0] = second;
        Assert.Equal(first, request.DeclaringTypeBindings.Bindings[0]);

        var secondFieldSignature = DecodeFieldSignature(
            fixture,
            sourceEnds,
            CreateFieldDefinition(fixture, rowId: 21, signature),
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Pair),
                [fixture.Pair]));
        var secondRequest = MetadataTypeSubstitutionRequest.ForFieldDefinition(secondFieldSignature, forwardLedger);
        Assert.NotEqual(request, secondRequest);
        Assert.NotEqual(request.Sha256, secondRequest.Sha256);
    }

    /// <summary>Proves FieldSig requests reject differing source ends and a different declaring TypeDef proof.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_substitution_requires_exact_source_ends_and_declaring_owner()
    {
        var fixture = new SyntheticMetadataFixture();
        var sourceEnds = CreateAggregateSourceEnds(fixture.Module, 0, 0);
        var fieldSignature = DecodeFieldSignature(
            fixture,
            sourceEnds,
            CreateFieldDefinition(fixture, rowId: 22, [0x06, 0x13, 0x00]));
        var ownerSet = CreateOwnerSet(fixture, sourceEnds, fixture.FieldOwnerPinned);
        var int32 = SourceDerivedPrimitive(fixture, rowId: 498, elementType: 0x08);
        var text = SourceDerivedPrimitive(fixture, rowId: 497, elementType: 0x0E);
        var matchingLedger = MetadataGenericParameterBindingLedgerIdentity.Create(
            ownerSet,
            [
                MetadataTypeArgumentBindingIdentity.Exact(ownerSet.Parameters[0], int32),
                MetadataTypeArgumentBindingIdentity.Exact(ownerSet.Parameters[1], text),
            ]);
        Assert.NotNull(MetadataTypeSubstitutionRequest.ForFieldDefinition(fieldSignature, matchingLedger));

        var differingSourceEnds = CreateAggregateSourceEnds(fixture.Module, 1, 0);
        var differingSet = CreateOwnerSet(fixture, differingSourceEnds, fixture.FieldOwnerPinned);
        var differingLedger = MetadataGenericParameterBindingLedgerIdentity.Create(
            differingSet,
            [
                MetadataTypeArgumentBindingIdentity.Exact(differingSet.Parameters[0], int32),
                MetadataTypeArgumentBindingIdentity.Exact(differingSet.Parameters[1], text),
            ]);
        Assert.Throws<ArgumentException>(() =>
            MetadataTypeSubstitutionRequest.ForFieldDefinition(fieldSignature, differingLedger));

        var otherOwnerSet = CreateOwnerSet(fixture, sourceEnds, fixture.PairPinned);
        var otherOwnerLedger = MetadataGenericParameterBindingLedgerIdentity.Create(
            otherOwnerSet,
            [
                MetadataTypeArgumentBindingIdentity.Exact(otherOwnerSet.Parameters[0], int32),
                MetadataTypeArgumentBindingIdentity.Exact(otherOwnerSet.Parameters[1], text),
            ]);
        Assert.Throws<ArgumentException>(() =>
            MetadataTypeSubstitutionRequest.ForFieldDefinition(fieldSignature, otherOwnerLedger));
    }

    /// <summary>Proves exact-empty owner proof is distinct from missing, incomplete, invalid, and bounded proof.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_substitution_exact_zero_and_prefix_free_proof_stops_are_distinct()
    {
        var fixture = new SyntheticMetadataFixture();
        var sourceEnds = CreateAggregateSourceEnds(fixture.Module, 0, 0);
        var zeroField = DecodeFieldSignature(
            fixture,
            sourceEnds,
            CreateFieldDefinition(
                fixture,
                rowId: 301,
                [0x06, 0x08],
                fixture.ZeroFieldOwnerPinned));
        var zeroSet = CreateOwnerSet(fixture, sourceEnds, fixture.ZeroFieldOwnerPinned);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, zeroSet.ResultKind);
        Assert.Empty(zeroSet.Parameters);
        var exactEmpty = MetadataGenericParameterBindingLedgerIdentity.Create(
            zeroSet,
            ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
        var zeroResult = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(zeroField, exactEmpty));
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, exactEmpty.ResultKind);
        Assert.Equal(MetadataTypeSubstitutionResultKind.Exact, zeroResult.Kind);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, zeroResult.ClosedType!.PrimitiveKind);

        var missingZero = MetadataGenericParameterBindingLedgerIdentity.Create(
            zeroSet,
            default);
        var missingZeroResult = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(zeroField, missingZero));
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, missingZero.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.BindingIncomplete, missingZeroResult.BindingProofIssue);
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, missingZeroResult.Kind);
        Assert.Null(missingZeroResult.ClosedType);
        AssertNoAppliedBindings(missingZeroResult.SubstitutedTree);

        var genericField = DecodeFieldSignature(
            fixture,
            sourceEnds,
            CreateFieldDefinition(fixture, rowId: 23, [0x06, 0x13, 0x00]));
        var ownerSet = CreateOwnerSet(fixture, sourceEnds, fixture.FieldOwnerPinned);
        var int32 = SourceDerivedPrimitive(fixture, rowId: 496, elementType: 0x08);
        var first = MetadataTypeArgumentBindingIdentity.Exact(ownerSet.Parameters[0], int32);
        var incomplete = MetadataGenericParameterBindingLedgerIdentity.Create(ownerSet, [first]);
        var incompleteResult = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(genericField, incomplete));
        Assert.Equal(MetadataGenericParameterProofIssue.BindingIncomplete, incompleteResult.BindingProofIssue);
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, incompleteResult.Kind);
        Assert.Null(incompleteResult.ClosedType);
        AssertNoAppliedBindings(incompleteResult.SubstitutedTree);

        var duplicate = MetadataGenericParameterBindingLedgerIdentity.Create(ownerSet, [first, first]);
        var invalidResult = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(genericField, duplicate));
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, invalidResult.BindingProofResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.DuplicateBinding, invalidResult.BindingProofIssue);
        Assert.Equal(MetadataTypeSubstitutionResultKind.Invalid, invalidResult.Kind);
        Assert.Null(invalidResult.ClosedType);
        AssertNoAppliedBindings(invalidResult.SubstitutedTree);

        var largeField = DecodeFieldSignature(
            fixture,
            sourceEnds,
            CreateFieldDefinition(
                fixture,
                rowId: 321,
                [0x06, 0x08],
                fixture.LargeFieldOwnerPinned));
        var largeSet = CreateOwnerSet(fixture, sourceEnds, fixture.LargeFieldOwnerPinned);
        var largeLedger = MetadataGenericParameterBindingLedgerIdentity.Create(
            largeSet,
            ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
        var boundedResult = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(largeField, largeLedger));
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerArityBoundReached, boundedResult.BindingProofIssue);
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, boundedResult.Kind);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterCountBoundName, boundedResult.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount, boundedResult.ReachedBound.Value);
        Assert.Null(boundedResult.ClosedType);
        AssertNoAppliedBindings(boundedResult.SubstitutedTree);
    }

    /// <summary>
    /// Proves FieldSig-invalid nodes outrank binding-proof stops while TypeSpec custom modifiers keep their normal
    /// proof-dependent disposition.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_context_invalidity_precedes_binding_proof_stops_and_retains_all_indices()
    {
        var fixture = new SyntheticMetadataFixture();
        var sourceEnds = CreateAggregateSourceEnds(fixture.Module, 0, 0);
        var ownerSet = CreateOwnerSet(fixture, sourceEnds, fixture.FieldOwnerPinned);
        var int32 = SourceDerivedPrimitive(fixture, rowId: 495, elementType: 0x08);
        var text = SourceDerivedPrimitive(fixture, rowId: 494, elementType: 0x0E);
        var first = MetadataTypeArgumentBindingIdentity.Exact(ownerSet.Parameters[0], int32);
        var second = MetadataTypeArgumentBindingIdentity.Exact(ownerSet.Parameters[1], text);
        var exactLedger = MetadataGenericParameterBindingLedgerIdentity.Create(ownerSet, [first, second]);
        var incompleteLedger = MetadataGenericParameterBindingLedgerIdentity.Create(ownerSet, [first]);
        var invalidLedger = MetadataGenericParameterBindingLedgerIdentity.Create(ownerSet, [first, first]);
        Assert.Equal(MetadataGenericParameterProofIssue.None, exactLedger.Issue);
        Assert.Equal(MetadataGenericParameterProofIssue.BindingIncomplete, incompleteLedger.Issue);
        Assert.Equal(MetadataGenericParameterProofIssue.DuplicateBinding, invalidLedger.Issue);

        var inRange = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(fixture, rowId: 24, [0x06, 0x13, 0x00])),
                incompleteLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, inRange.Kind);
        Assert.Empty(inRange.UndeclaredOwnerVariableIndices);
        Assert.Empty(inRange.UndeclaredMethodVariableIndices);
        AssertNoAppliedBindings(inRange.SubstitutedTree);

        var outOfRange = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(fixture, rowId: 25, [0x06, 0x13, 0x02])),
                incompleteLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.Invalid, outOfRange.Kind);
        Assert.Equal(MetadataGenericParameterProofIssue.BindingIncomplete, outOfRange.BindingProofIssue);
        Assert.Equal([2], outOfRange.UndeclaredOwnerVariableIndices.ToArray());
        Assert.Empty(outOfRange.UndeclaredMethodVariableIndices);
        AssertNoAppliedBindings(outOfRange.SubstitutedTree);

        var methodVariable = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(fixture, rowId: 26, [0x06, 0x1E, 0x00])),
                incompleteLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.Invalid, methodVariable.Kind);
        Assert.Equal([0], methodVariable.UndeclaredMethodVariableIndices.ToArray());
        AssertNoAppliedBindings(methodVariable.SubstitutedTree);

        var typeSpecificationReference = MetadataTypeSpecificationRowReferenceIdentity.Create(
            fixture.Module,
            0x1B000001);
        var typeSpecificationEntry = MetadataSignatureTokenResolutionEntry.TypeSpecification(
            typeSpecificationReference);
        var modifierField = DecodeFieldSignature(
            fixture,
            sourceEnds,
            CreateFieldDefinition(
                fixture,
                rowId: 27,
                [
                    0x06,
                    0x1F,
                    .. EncodeTypeDefOrRef(typeSpecificationReference.TypeSpecificationToken),
                    0x08,
                ]),
            typeSpecificationEntry);
        var incompleteModifier = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                modifierField,
                incompleteLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, incompleteModifier.Kind);
        Assert.Equal(MetadataGenericParameterProofIssue.BindingIncomplete, incompleteModifier.BindingProofIssue);
        Assert.Null(incompleteModifier.ReachedBound);
        AssertNoAppliedBindings(incompleteModifier.SubstitutedTree);

        var exactModifier = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(modifierField, exactLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.Unsupported, exactModifier.Kind);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, exactModifier.BindingProofResultKind);
        Assert.Null(exactModifier.ClosedType);
        Assert.Null(exactModifier.ReachedBound);
        AssertNoAppliedBindings(exactModifier.SubstitutedTree);

        var directTypeSpecificationHead = MetadataFieldSignatureDecodeOutcome.Decode(
            CreateFieldDefinition(
                fixture,
                rowId: 29,
                [
                    0x06,
                    0x12,
                    .. EncodeTypeDefOrRef(typeSpecificationReference.TypeSpecificationToken),
                ]),
            sourceEnds,
            MetadataSignatureTokenResolutionCatalog.Create(sourceEnds, [typeSpecificationEntry]));
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, directTypeSpecificationHead.Kind);
        Assert.Equal("W8_SIGNATURE_TYPESPEC_HEAD_INVALID", directTypeSpecificationHead.InvalidCode);
        Assert.Null(directTypeSpecificationHead.Identity);
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, incompleteLedger.ResultKind);

        var mixed = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(
                        fixture,
                        rowId: 28,
                        [
                            0x06,
                            0x20,
                            .. EncodeTypeDefOrRef(typeSpecificationReference.TypeSpecificationToken),
                            0x15,
                            0x12,
                            .. EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken),
                            0x02,
                            0x1E,
                            0x00,
                            0x15,
                            0x12,
                            .. EncodeTypeDefOrRef(fixture.Pair.TypeDefinitionToken),
                            0x02,
                            0x13,
                            0x02,
                            0x1E,
                            0x03,
                        ]),
                    typeSpecificationEntry,
                    MetadataSignatureTokenResolutionEntry.Named(
                        MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Pair),
                        [fixture.Pair])),
                invalidLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.Invalid, mixed.Kind);
        Assert.Equal(MetadataGenericParameterProofIssue.DuplicateBinding, mixed.BindingProofIssue);
        Assert.Equal([2], mixed.UndeclaredOwnerVariableIndices.ToArray());
        Assert.Equal([0, 3], mixed.UndeclaredMethodVariableIndices.ToArray());
        AssertNoAppliedBindings(mixed.SubstitutedTree);

        var largeSet = CreateOwnerSet(fixture, sourceEnds, fixture.LargeFieldOwnerPinned);
        var arityBoundLedger = MetadataGenericParameterBindingLedgerIdentity.Create(
            largeSet,
            ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerArityBoundReached, arityBoundLedger.Issue);

        var boundedInRange = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(
                        fixture,
                        rowId: 322,
                        [0x06, 0x13, 0x00],
                        fixture.LargeFieldOwnerPinned)),
                arityBoundLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, boundedInRange.Kind);
        Assert.Empty(boundedInRange.UndeclaredOwnerVariableIndices);
        AssertNoAppliedBindings(boundedInRange.SubstitutedTree);

        var boundedUnknownRange = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(
                        fixture,
                        rowId: 323,
                        [0x06, 0x13, .. EncodeCompressedUnsigned(65)],
                        fixture.LargeFieldOwnerPinned)),
                arityBoundLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, boundedUnknownRange.Kind);
        Assert.Empty(boundedUnknownRange.UndeclaredOwnerVariableIndices);
        AssertNoAppliedBindings(boundedUnknownRange.SubstitutedTree);

        var boundedMethodVariable = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(
                        fixture,
                        rowId: 324,
                        [0x06, 0x1E, 0x00],
                        fixture.LargeFieldOwnerPinned)),
                arityBoundLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.Invalid, boundedMethodVariable.Kind);
        Assert.Equal([0], boundedMethodVariable.UndeclaredMethodVariableIndices.ToArray());
        Assert.Equal(
            ExpressionV2ContractLimits.GenericParameterCountBoundName,
            boundedMethodVariable.BindingProofReachedBound!.Name);
        Assert.Equal(
            StaticFieldV2Limits.MaximumGenericParameterCount + 1,
            boundedMethodVariable.BindingProofObservedCount);
        Assert.Null(boundedMethodVariable.ReachedBound);
        AssertNoAppliedBindings(boundedMethodVariable.SubstitutedTree);

        var boundedModifier = MetadataTypeSubstitutionResult.Substitute(
            MetadataTypeSubstitutionRequest.ForFieldDefinition(
                DecodeFieldSignature(
                    fixture,
                    sourceEnds,
                    CreateFieldDefinition(
                        fixture,
                        rowId: 325,
                        [
                            0x06,
                            0x20,
                            .. EncodeTypeDefOrRef(typeSpecificationReference.TypeSpecificationToken),
                            0x08,
                        ],
                        fixture.LargeFieldOwnerPinned),
                    typeSpecificationEntry),
                arityBoundLedger));
        Assert.Equal(MetadataTypeSubstitutionResultKind.NonExact, boundedModifier.Kind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerArityBoundReached, boundedModifier.BindingProofIssue);
        Assert.Equal(boundedModifier.BindingProofReachedBound, boundedModifier.ReachedBound);
        Assert.NotNull(boundedModifier.ReachedBound);
        AssertNoAppliedBindings(boundedModifier.SubstitutedTree);
    }

    /// <summary>Proves Nullable is derived only from a decoded generic value-type head with exact core-library proof.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nullable_is_derived_after_exact_head_and_core_library_proof()
    {
        var fixture = new SyntheticMetadataFixture();
        ImmutableArray<byte> signature =
        [
            0x15,
            0x11,
            .. EncodeTypeDefOrRef(fixture.Nullable.TypeDefinitionToken),
            0x01,
            0x08,
        ];
        var outcome = Decode(
            fixture,
            1,
            signature,
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Nullable),
                [fixture.Nullable]));
        var specification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(outcome);

        Assert.Equal(MetadataTypeSignatureNodeKind.GenericInstantiation, specification.Root.Kind);
        Assert.Equal(MetadataNamedSignatureHeadKind.ValueType, specification.Root.NamedHeadKind);
        Assert.Equal(MetadataClosedTypeKind.Nullable, specification.Construction.ClosedType!.Kind);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32,
            specification.Construction.ClosedType.ElementType!.PrimitiveKind);

        ImmutableArray<byte> invalidArgumentSignature =
        [
            0x15,
            0x11,
            .. EncodeTypeDefOrRef(fixture.Nullable.TypeDefinitionToken),
            0x01,
            0x12,
            .. EncodeTypeDefOrRef(fixture.BaseType.TypeDefinitionToken),
        ];
        var invalidArgument = MetadataTypeSpecificationIdentity.FromDecodeOutcome(Decode(
            fixture,
            3,
            invalidArgumentSignature,
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Nullable),
                [fixture.Nullable]),
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.BaseType),
                [fixture.BaseType])));
        Assert.Equal(MetadataTypeConstructionResultKind.Invalid, invalidArgument.Construction.Kind);

        var classHead = Decode(
            fixture,
            2,
            [0x12, .. EncodeTypeDefOrRef(fixture.Int32.TypeDefinitionToken)],
            MetadataSignatureTokenResolutionEntry.Named(
                MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(fixture.Int32),
                [fixture.Int32]));
        Assert.Equal(MetadataSignatureDecodeResultKind.Invalid, classHead.Kind);
    }

    /// <summary>Proves canonical artifacts are immutable and only the real decoder can issue certificates.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Canonical_artifacts_are_defensive_and_certificate_issuance_is_decoder_only()
    {
        var fixture = new SyntheticMetadataFixture();
        var assembly = typeof(MetadataTypeSignatureNode).Assembly;
        var outcome = Decode(fixture, 1, [0x08]);
        var before = outcome.CanonicalBytes.ToArray();
        var exposed = outcome.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(exposed)![0] ^= 0x7F;
        Assert.Equal(before, outcome.CanonicalBytes.ToArray());
        Assert.Equal(outcome, Decode(fixture, 1, [0x08]));
        Assert.Equal(outcome.Sha256, Decode(fixture, 1, [0x08]).Sha256);

        var certificateType = typeof(MetadataSignatureDecodeCertificate);
        Assert.Empty(certificateType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain(
            certificateType.GetMethods(BindingFlags.Public | BindingFlags.Static),
            static method => method.ReturnType == typeof(MetadataSignatureDecodeCertificate));
        Assert.DoesNotContain(
            typeof(MetadataSignatureDecodeOutcome).GetMethods(BindingFlags.Public | BindingFlags.Static),
            static method => method.ReturnType == typeof(MetadataSignatureDecodeOutcome));

        Assert.True(typeof(MetadataTypeSpecificationIdentity).IsSealed);
        Assert.True(typeof(MetadataTypeSpecificationGraphIdentity).IsSealed);
        Assert.True(typeof(MetadataTypeSubstitutionResult).IsSealed);
        Assert.True(typeof(MetadataSubstitutedTypeNodeIdentity).IsSealed);

        Assert.Equal(
            [MetadataTypeSubstitutionContextKind.FieldDefinition],
            Enum.GetValues<MetadataTypeSubstitutionContextKind>());
        var requestFactory = Assert.Single(typeof(MetadataTypeSubstitutionRequest).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(nameof(MetadataTypeSubstitutionRequest.ForFieldDefinition), requestFactory.Name);
        Assert.Equal(
            [typeof(MetadataFieldSignatureIdentity), typeof(MetadataGenericParameterBindingLedgerIdentity)],
            requestFactory.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        var executor = Assert.Single(typeof(MetadataTypeSubstitutionResult).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(nameof(MetadataTypeSubstitutionResult.Substitute), executor.Name);
        Assert.Equal(
            [typeof(MetadataTypeSubstitutionRequest)],
            executor.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        Assert.Empty(typeof(MetadataTypeSignatureNode).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(MetadataTypeConstructionResult).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(MetadataClosedTypeIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));

        var guardedTypes = new HashSet<Type>
        {
            typeof(MetadataTypeSignatureNode),
            typeof(MetadataTypeConstructionResult),
            typeof(MetadataClosedTypeIdentity),
        };
        Assert.All(
            guardedTypes,
            static type => Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));
        var assemblyPublicIssuers = assembly.GetExportedTypes()
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => guardedTypes.Contains(method.ReturnType))
            .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(assemblyPublicIssuers);

        var guardedProperties = assembly.GetExportedTypes()
            .SelectMany(static type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => ContainsGuardedType(property.PropertyType, guardedTypes))
            .OrderBy(static property => property.DeclaringType!.Name, StringComparer.Ordinal)
            .ThenBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.All(guardedProperties, static property => Assert.Null(property.SetMethod));
        Assert.Equal(
            new[]
            {
                "MetadataClosedTypeIdentity.ElementType",
                "MetadataClosedTypeIdentity.FlattenedArguments",
                "MetadataFieldSignatureDecodeOutcome.Root",
                "MetadataFieldSignatureIdentity.Root",
                "MetadataGenericParameterAuthorityBindingIdentity.Argument",
                "MetadataSignatureDecodeOutcome.Root",
                "MetadataSubstitutedTypeNodeIdentity.Replacement",
                "MetadataSubstitutedTypeNodeIdentity.SourceNode",
                "MetadataTypeArgumentBindingIdentity.Argument",
                "MetadataTypeConstructionResult.ClosedType",
                "MetadataTypeConstructionResult.Root",
                "MetadataTypeConstructionSegment.LocalArguments",
                "MetadataTypeSignatureNode.Children",
                "MetadataTypeSpecificationIdentity.Construction",
                "MetadataTypeSpecificationIdentity.Root",
                "MetadataTypeSubstitutionRequest.Root",
                "MetadataTypeSubstitutionResult.ClosedType",
                "MetadataTypeSubstitutionResult.Root",
            },
            guardedProperties.Select(static property =>
                $"{property.DeclaringType!.Name}.{property.Name}").ToArray());

        Assert.Null(typeof(MetadataTypeArgumentBindingIdentity).GetMethod(
            nameof(MetadataTypeArgumentBindingIdentity.Exact),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(MetadataTypeArgumentBindingIdentity).GetMethod(
            nameof(MetadataTypeArgumentBindingIdentity.Exact),
            BindingFlags.NonPublic | BindingFlags.Static));
        var publicBindingFactory = Assert.Single(typeof(MetadataTypeArgumentBindingIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(nameof(MetadataTypeArgumentBindingIdentity.Unavailable), publicBindingFactory.Name);

        static bool ContainsGuardedType(Type candidate, HashSet<Type> guarded) =>
            guarded.Contains(candidate) ||
            candidate.IsGenericType && candidate.GetGenericArguments().Any(argument =>
                ContainsGuardedType(argument, guarded));
    }

    /// <summary>Proves every public metadata-construction checkpoint type is sealed where applicable and draft-documented.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Public_metadata_construction_surface_is_sealed_and_draft_documented()
    {
        var assembly = typeof(MetadataTypeSignatureNode).Assembly;
        var checkpointTypes = assembly.GetExportedTypes()
            .Where(static type =>
                type.Namespace == "Interpreter.Product.DumpQuery" &&
                type.Name.StartsWith("Metadata", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(checkpointTypes);
        Assert.All(
            checkpointTypes.Where(static type => type.IsClass && !type.IsAbstract),
            static type => Assert.True(type.IsSealed));

        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        foreach (var type in checkpointTypes)
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

    private static MetadataSignatureDecodeOutcome Decode(
        SyntheticMetadataFixture fixture,
        int rowId,
        ImmutableArray<byte> bytes,
        params MetadataSignatureTokenResolutionEntry[] entries)
    {
        var reference = MetadataTypeSpecificationRowReferenceIdentity.Create(
            fixture.Module,
            0x1B000000 | rowId);
        var catalog = MetadataSignatureTokenResolutionCatalog.Create(
            fixture.SourceEnds,
            entries.ToImmutableArray());
        return MetadataTypeSignatureDecoder.DecodeTypeSpecification(reference, bytes, catalog);
    }

    private static StaticFieldDefinitionIdentity CreateFieldDefinition(
        SyntheticMetadataFixture fixture,
        int rowId,
        ImmutableArray<byte> signature,
        StaticFieldTypeDefinitionIdentity? declaringType = null)
    {
        var token = 0x04000000 | rowId;
        return StaticFieldDefinitionIdentity.Create(
            declaringType ?? fixture.FieldOwnerPinned,
            token,
            $"Field{rowId}",
            (int)(FieldAttributes.Public | FieldAttributes.Static),
            signature,
            StaticFieldFieldCustomAttributeProjection.Create(
                token,
                ownedRowsExamined: 0,
                ownedRowCount: 0,
                ImmutableArray<StaticFieldCustomAttributeRowIdentity>.Empty));
    }

    private static MetadataFieldSignatureIdentity DecodeFieldSignature(
        SyntheticMetadataFixture fixture,
        MetadataSourceEndIdentity sourceEnds,
        StaticFieldDefinitionIdentity fieldDefinition,
        params MetadataSignatureTokenResolutionEntry[] entries)
    {
        var tokenCatalog = MetadataSignatureTokenResolutionCatalog.Create(
            sourceEnds,
            entries.ToImmutableArray());
        var outcome = MetadataFieldSignatureDecodeOutcome.Decode(
            fieldDefinition,
            sourceEnds,
            tokenCatalog);
        Assert.Equal(MetadataSignatureDecodeResultKind.Exact, outcome.Kind);
        return Assert.IsType<MetadataFieldSignatureIdentity>(outcome.Identity);
    }

    private static MetadataGenericParameterOwnerSetIdentity CreateOwnerSet(
        SyntheticMetadataFixture fixture,
        MetadataSourceEndIdentity sourceEnds,
        StaticFieldTypeDefinitionIdentity owner)
    {
        var declaration = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            sourceEnds,
            MetadataRawTypeDefinitionIdentity.FromPinnedW7(owner));
        return MetadataGenericParameterOwnerSetIdentity.Create(
            declaration,
            CreateGenericParameterCatalog(fixture, sourceEnds));
    }

    private static MetadataClosedTypeIdentity SourceDerivedPrimitive(
        SyntheticMetadataFixture fixture,
        int rowId,
        byte elementType)
    {
        var specification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(
            Decode(fixture, rowId, [elementType]));
        Assert.Equal(MetadataTypeConstructionResultKind.Exact, specification.Construction.Kind);
        return Assert.IsType<MetadataClosedTypeIdentity>(specification.Construction.ClosedType);
    }

    private static void AssertNoAppliedBindings(MetadataSubstitutedTypeNodeIdentity node)
    {
        Assert.Null(node.AppliedBinding);
        Assert.Null(node.Replacement);
        foreach (var child in node.Children)
        {
            AssertNoAppliedBindings(child);
        }
    }

    private static ImmutableArray<byte> CreateFieldModifierChain(int modifierToken, int modifierCount)
    {
        var encodedToken = EncodeTypeDefOrRef(modifierToken);
        var bytes = ImmutableArray.CreateBuilder<byte>();
        bytes.Add(0x06);
        for (var index = 0; index < modifierCount; index++)
        {
            bytes.Add(0x1F);
            bytes.AddRange(encodedToken);
        }
        bytes.Add(0x08);
        return bytes.ToImmutable();
    }

    private static void AssertFieldStopHasNoProjection(MetadataFieldSignatureDecodeOutcome outcome)
    {
        Assert.NotEqual(MetadataSignatureDecodeResultKind.Exact, outcome.Kind);
        Assert.Null(outcome.Identity);
        Assert.Null(outcome.Root);
        Assert.Null(outcome.Certificate);
    }

    private static (MetadataTypeSpecificationIdentity Specification, MetadataTypeSpecificationGraphIdentity Graph)
        SpecificationAndGraph(SyntheticMetadataFixture fixture, int rowId, ImmutableArray<byte> bytes)
    {
        var outcome = Decode(fixture, rowId, bytes);
        var specification = MetadataTypeSpecificationIdentity.FromDecodeOutcome(outcome);
        var graph = MetadataTypeSpecificationGraphIdentity.Create(outcome.Reference, [outcome]);
        return (specification, graph);
    }

    private static MetadataTypeSpecificationGraphIdentity CreateModifierChainGraph(
        SyntheticMetadataFixture fixture,
        int rowCount)
    {
        var references = Enumerable.Range(1, rowCount)
            .Select(row => MetadataTypeSpecificationRowReferenceIdentity.Create(
                fixture.Module,
                0x1B000000 | row))
            .ToImmutableArray();
        var catalog = MetadataSignatureTokenResolutionCatalog.Create(
            fixture.SourceEnds,
            references.Select(MetadataSignatureTokenResolutionEntry.TypeSpecification).ToImmutableArray());
        var outcomes = ImmutableArray.CreateBuilder<MetadataSignatureDecodeOutcome>(rowCount);
        for (var index = 0; index < references.Length; index++)
        {
            ImmutableArray<byte> bytes;
            if (index == references.Length - 1)
            {
                bytes = [0x08];
            }
            else
            {
                bytes = [0x20, .. EncodeTypeDefOrRef(references[index + 1].TypeSpecificationToken), 0x08];
            }
            outcomes.Add(MetadataTypeSignatureDecoder.DecodeTypeSpecification(references[index], bytes, catalog));
        }
        Assert.All(outcomes, static outcome => Assert.Equal(MetadataSignatureDecodeResultKind.Exact, outcome.Kind));
        return MetadataTypeSpecificationGraphIdentity.Create(references[0], outcomes.ToImmutable());
    }

    private static MetadataSourceEndIdentity CreateAggregateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int interfaceImplementationRowCount,
        int genericParameterConstraintRowCount)
    {
        const int supportingTableRowCount = 512;
        const int genericParameterRowCount = 9;
        var fact = StaticFieldModuleSearchFact.Exact(
            module.Module,
            module.ModuleContent,
            typeDefinitionsExamined: supportingTableRowCount,
            fieldDefinitionsExamined: supportingTableRowCount,
            typeDefinitionRowCount: supportingTableRowCount,
            fieldDefinitionRowCount: supportingTableRowCount,
            typeReferenceRowCount: supportingTableRowCount,
            typeSpecificationRowCount: supportingTableRowCount,
            methodDefinitionRowCount: supportingTableRowCount,
            interfaceImplementationRowCount: interfaceImplementationRowCount,
            genericParameterRowCount: genericParameterRowCount,
            genericParameterConstraintRowCount: genericParameterConstraintRowCount);
        return MetadataSourceEndIdentity.Create(module, fact);
    }

    private static MetadataGenericParameterTableCatalogIdentity CreateGenericParameterCatalog(
        SyntheticMetadataFixture fixture,
        MetadataSourceEndIdentity sourceEnds)
    {
        var catalog = MetadataGenericParameterTableCatalogIdentity.Create(
            sourceEnds,
            fixture.GenericParameterRows);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, catalog.ResultKind);
        return catalog;
    }

    private static ImmutableArray<byte> EncodeTypeDefOrRef(int metadataToken)
    {
        var table = (metadataToken >> 24) & 0xFF;
        var row = metadataToken & 0x00FF_FFFF;
        var tag = table switch
        {
            0x02 => 0,
            0x01 => 1,
            0x1B => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(metadataToken)),
        };
        return EncodeCompressedUnsigned(checked((row << 2) | tag));
    }

    private static ImmutableArray<byte> EncodeCompressedUnsigned(int value)
    {
        if (value is < 0 or > 0x1FFF_FFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        if (value <= 0x7F)
        {
            return [(byte)value];
        }
        if (value <= 0x3FFF)
        {
            return [(byte)(0x80 | value >> 8), (byte)value];
        }
        return [
            (byte)(0xC0 | value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value,
        ];
    }

    private static ImmutableArray<byte> EncodeCompressedSigned(int value)
    {
        if (value is >= -64 and <= 63)
        {
            return [(byte)(((value & 0x3F) << 1) | (value < 0 ? 1 : 0))];
        }
        if (value is >= -8192 and <= 8191)
        {
            var raw = ((value & 0x1FFF) << 1) | (value < 0 ? 1 : 0);
            return [(byte)(0x80 | raw >> 8), (byte)raw];
        }
        if (value is < -0x1000_0000 or > 0x0FFF_FFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        var wide = ((value & 0x0FFF_FFFF) << 1) | (value < 0 ? 1 : 0);
        return [
            (byte)(0xC0 | wide >> 24),
            (byte)(wide >> 16),
            (byte)(wide >> 8),
            (byte)wide,
        ];
    }

    private static MetadataRawTypeDefinitionIdentity CreateRawType(
        StaticFieldMetadataModuleIdentity module,
        int token,
        string namespaceName,
        string name,
        int genericParameterCount) =>
        MetadataRawTypeDefinitionIdentity.Create(
            module,
            token,
            fieldListRowId: 1,
            fieldListEndExclusiveRowId: 1,
            methodListRowId: 1,
            methodListEndExclusiveRowId: 1,
            namespaceName,
            name,
            (int)(TypeAttributes.Public | TypeAttributes.Class),
            genericParameterCount,
            extendsMetadataToken: 0x02000001);

    private static ImmutableArray<MetadataGenericParameterIdentity> CreateTypeParameters(
        MetadataRawTypeDefinitionIdentity raw,
        int count,
        int genericParameterRowStart)
    {
        var owner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(raw);
        return Enumerable.Range(0, count)
            .Select(index => MetadataGenericParameterIdentity.Create(
                0x2A000000 | checked(genericParameterRowStart + index),
                owner,
                index,
                $"T{index}",
                attributes: 0))
            .ToImmutableArray();
    }

    private sealed class SyntheticMetadataFixture
    {
        internal SyntheticMetadataFixture()
        {
            Module = CreateMetadataModule();
            SourceEnds = CreateSourceEnds(Module);
            var objectType = CreatePinnedType(
                Module,
                0x02000001,
                "System",
                "Object",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                genericParameterCount: 0,
                introducedGenericArity: 0,
                extendsMetadataToken: null);
            var valueType = CreatePinnedType(
                Module,
                0x02000002,
                "System",
                "ValueType",
                (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
                0,
                0,
                objectType.TypeDefinitionToken);
            var enumType = CreatePinnedType(
                Module,
                0x02000003,
                "System",
                "Enum",
                (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
                0,
                0,
                valueType.TypeDefinitionToken);
            var selection = StaticFieldCoreLibrarySelectionIdentity.Create(
                StaticFieldCoreLibrarySelectionProvenance.ClrMdRuntimeBaseClassLibrary,
                runtimeOrdinal: 0,
                Module);
            var coreLibrary = StaticFieldCoreLibraryIdentity.Create(
                selection,
                Module,
                objectType,
                valueType,
                enumType,
                StaticFieldTypeAncestryEdge.Create(valueType, objectType),
                StaticFieldTypeAncestryEdge.Create(enumType, valueType));

            var int32Pinned = CreatePinnedType(
                Module,
                0x02000004,
                "System",
                "Int32",
                (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed),
                0,
                0,
                valueType.TypeDefinitionToken);
            var int32Ancestry = StaticFieldTypeAncestryIdentity.Create(
                int32Pinned,
                [StaticFieldTypeAncestryEdge.Create(int32Pinned, valueType)],
                coreLibrary);
            Int32 = MetadataTypeDefinitionIdentity.FromPinnedW7(
                int32Pinned,
                MetadataTypeDefinitionKind.ValueType,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                int32Ancestry);

            var nullablePinned = CreatePinnedType(
                Module,
                0x02000005,
                "System",
                "Nullable`1",
                (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed),
                1,
                1,
                valueType.TypeDefinitionToken);
            var nullableRaw = MetadataRawTypeDefinitionIdentity.FromPinnedW7(nullablePinned);
            var nullableParameters = CreateTypeParameters(nullableRaw, 1, genericParameterRowStart: 2);
            var nullableAncestry = StaticFieldTypeAncestryIdentity.Create(
                nullablePinned,
                [StaticFieldTypeAncestryEdge.Create(nullablePinned, valueType)],
                coreLibrary);
            Nullable = MetadataTypeDefinitionIdentity.FromPinnedW7(
                nullablePinned,
                MetadataTypeDefinitionKind.ValueType,
                nullableParameters,
                nullableAncestry);

            PairPinned = CreatePinnedType(
                Module,
                0x02000006,
                "Synthetic",
                "Pair`2",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                2,
                2,
                objectType.TypeDefinitionToken);
            var pairRaw = MetadataRawTypeDefinitionIdentity.FromPinnedW7(PairPinned);
            var pairAncestry = StaticFieldTypeAncestryIdentity.Create(
                PairPinned,
                [StaticFieldTypeAncestryEdge.Create(PairPinned, objectType)],
                coreLibrary);
            Pair = MetadataTypeDefinitionIdentity.FromPinnedW7(
                PairPinned,
                MetadataTypeDefinitionKind.Class,
                CreateTypeParameters(pairRaw, 2, genericParameterRowStart: 3),
                pairAncestry);

            var outerPinned = CreatePinnedType(
                Module,
                0x02000007,
                "Synthetic",
                "Outer`1",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                1,
                1,
                objectType.TypeDefinitionToken);
            var outerRaw = MetadataRawTypeDefinitionIdentity.FromPinnedW7(outerPinned);
            var outerAncestry = StaticFieldTypeAncestryIdentity.Create(
                outerPinned,
                [StaticFieldTypeAncestryEdge.Create(outerPinned, objectType)],
                coreLibrary);
            Outer = MetadataTypeDefinitionIdentity.FromPinnedW7(
                outerPinned,
                MetadataTypeDefinitionKind.Class,
                CreateTypeParameters(outerRaw, 1, genericParameterRowStart: 5),
                outerAncestry);
            var innerPinned = CreatePinnedType(
                Module,
                0x02000008,
                string.Empty,
                "Inner`1",
                (int)(TypeAttributes.NestedPublic | TypeAttributes.Class),
                2,
                1,
                objectType.TypeDefinitionToken,
                outerPinned);
            var innerRaw = MetadataRawTypeDefinitionIdentity.FromPinnedW7(innerPinned);
            var innerAncestry = StaticFieldTypeAncestryIdentity.Create(
                innerPinned,
                [StaticFieldTypeAncestryEdge.Create(innerPinned, objectType)],
                coreLibrary);
            Inner = MetadataTypeDefinitionIdentity.FromPinnedW7(
                innerPinned,
                MetadataTypeDefinitionKind.Class,
                CreateTypeParameters(innerRaw, 2, genericParameterRowStart: 6),
                innerAncestry);

            InterfaceAlpha = CreateInterface(0x02000009, "IAlpha");
            InterfaceBeta = CreateInterface(0x0200000A, "IBeta");
            var basePinned = CreatePinnedType(
                Module,
                0x0200000B,
                "Synthetic",
                "Base",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                0,
                0,
                objectType.TypeDefinitionToken);
            var baseAncestry = StaticFieldTypeAncestryIdentity.Create(
                basePinned,
                [StaticFieldTypeAncestryEdge.Create(basePinned, objectType)],
                coreLibrary);
            BaseType = MetadataTypeDefinitionIdentity.FromPinnedW7(
                basePinned,
                MetadataTypeDefinitionKind.Class,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                baseAncestry);
            var derivedPinned = CreatePinnedType(
                Module,
                0x0200000C,
                "Synthetic",
                "Derived",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                0,
                0,
                basePinned.TypeDefinitionToken);
            var derivedAncestry = StaticFieldTypeAncestryIdentity.Create(
                derivedPinned,
                [
                    StaticFieldTypeAncestryEdge.Create(derivedPinned, basePinned),
                    StaticFieldTypeAncestryEdge.Create(basePinned, objectType),
                ],
                coreLibrary);
            DerivedType = MetadataTypeDefinitionIdentity.FromPinnedW7(
                derivedPinned,
                MetadataTypeDefinitionKind.Class,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                derivedAncestry);

            var methodContainerPinned = CreatePinnedType(
                Module,
                0x0200000D,
                "Synthetic",
                "MethodContainer",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                0,
                0,
                objectType.TypeDefinitionToken,
                methodListRowId: 1,
                methodListEndExclusiveRowId: 2);
            MethodContainer = MetadataTypeDefinitionIdentity.FromPinnedW7(
                methodContainerPinned,
                MetadataTypeDefinitionKind.Class,
                ImmutableArray<MetadataGenericParameterIdentity>.Empty,
                StaticFieldTypeAncestryIdentity.Create(
                    methodContainerPinned,
                    [StaticFieldTypeAncestryEdge.Create(methodContainerPinned, objectType)],
                    coreLibrary));
            var method = StaticFieldMethodDefinitionIdentity.Create(
                methodContainerPinned,
                0x06000001,
                relativeVirtualAddress: 0,
                implementationAttributes: 0,
                attributes: 0,
                "Transform",
                [0x10, 0x01, 0x00, 0x01],
                parameterListRowId: 1,
                parameterListEndExclusiveRowId: 1,
                parameterDefinitionRowCount: 0);
            MethodParameter = MetadataGenericParameterIdentity.Create(
                0x2A000001,
                MetadataGenericParameterOwnerIdentity.ForMethodDefinition(method),
                position: 0,
                "TMethod",
                attributes: 0);

            FieldOwnerPinned = CreatePinnedType(
                Module,
                0x0200000E,
                "Synthetic",
                "FieldOwner`2",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                genericParameterCount: 2,
                introducedGenericArity: 2,
                extendsMetadataToken: objectType.TypeDefinitionToken,
                fieldListRowId: 1,
                fieldListEndExclusiveRowId: 301);
            var fieldOwner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(
                MetadataRawTypeDefinitionIdentity.FromPinnedW7(FieldOwnerPinned));
            var fieldOwnerFirstParameter = MetadataGenericParameterIdentity.Create(
                0x2A000008,
                fieldOwner,
                position: 0,
                "TKey",
                attributes: 0);
            var fieldOwnerSecondParameter = MetadataGenericParameterIdentity.Create(
                0x2A000009,
                fieldOwner,
                position: 1,
                "TValue",
                attributes: 0);
            ZeroFieldOwnerPinned = CreatePinnedType(
                Module,
                0x0200000F,
                "Synthetic",
                "ZeroFieldOwner",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                genericParameterCount: 0,
                introducedGenericArity: 0,
                extendsMetadataToken: objectType.TypeDefinitionToken,
                fieldListRowId: 301,
                fieldListEndExclusiveRowId: 321);
            LargeFieldOwnerPinned = CreatePinnedType(
                Module,
                0x02000010,
                "Synthetic",
                "LargeFieldOwner`65",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                genericParameterCount: StaticFieldV2Limits.MaximumGenericParameterCount + 1,
                introducedGenericArity: StaticFieldV2Limits.MaximumGenericParameterCount + 1,
                extendsMetadataToken: objectType.TypeDefinitionToken,
                fieldListRowId: 321,
                fieldListEndExclusiveRowId: 341);
            GenericParameterRows =
            [
                MethodParameter,
                Nullable.GenericParameters[0],
                Pair.GenericParameters[0],
                Pair.GenericParameters[1],
                Outer.GenericParameters[0],
                Inner.GenericParameters[0],
                Inner.GenericParameters[1],
                fieldOwnerFirstParameter,
                fieldOwnerSecondParameter,
            ];

            MetadataTypeDefinitionIdentity CreateInterface(int token, string name)
            {
                var pinned = CreatePinnedType(
                    Module,
                    token,
                    "Synthetic",
                    name,
                    (int)(TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract),
                    0,
                    0,
                    extendsMetadataToken: null);
                return MetadataTypeDefinitionIdentity.FromPinnedW7(
                    pinned,
                    MetadataTypeDefinitionKind.Interface,
                    ImmutableArray<MetadataGenericParameterIdentity>.Empty);
            }

        }

        internal StaticFieldMetadataModuleIdentity Module { get; }
        internal MetadataSourceEndIdentity SourceEnds { get; }
        internal MetadataTypeDefinitionIdentity Int32 { get; }
        internal MetadataTypeDefinitionIdentity Nullable { get; }
        internal StaticFieldTypeDefinitionIdentity PairPinned { get; }
        internal MetadataTypeDefinitionIdentity Pair { get; }
        internal MetadataTypeDefinitionIdentity Outer { get; }
        internal MetadataTypeDefinitionIdentity Inner { get; }
        internal MetadataTypeDefinitionIdentity InterfaceAlpha { get; }
        internal MetadataTypeDefinitionIdentity InterfaceBeta { get; }
        internal MetadataTypeDefinitionIdentity BaseType { get; }
        internal MetadataTypeDefinitionIdentity DerivedType { get; }
        internal MetadataTypeDefinitionIdentity MethodContainer { get; }
        internal MetadataGenericParameterIdentity MethodParameter { get; }
        internal ImmutableArray<MetadataGenericParameterIdentity> GenericParameterRows { get; }
        internal StaticFieldTypeDefinitionIdentity FieldOwnerPinned { get; }
        internal StaticFieldTypeDefinitionIdentity ZeroFieldOwnerPinned { get; }
        internal StaticFieldTypeDefinitionIdentity LargeFieldOwnerPinned { get; }

        internal static StaticFieldMetadataModuleIdentity CreateMetadataModule(
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
                $"synthetic-metadata-{moduleAddress:x}.dll",
                content.Mvid,
                Guid.Empty,
                Guid.Empty);
            var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
                "System.Private.CoreLib",
                8,
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

        private static MetadataSourceEndIdentity CreateSourceEnds(StaticFieldMetadataModuleIdentity module)
        {
            const int typeDefinitionRowEnd = 256;
            const int fieldDefinitionRowEnd = 256;
            var fact = StaticFieldModuleSearchFact.Exact(
                module.Module,
                module.ModuleContent,
                typeDefinitionsExamined: typeDefinitionRowEnd,
                fieldDefinitionsExamined: fieldDefinitionRowEnd,
                typeDefinitionRowCount: typeDefinitionRowEnd,
                fieldDefinitionRowCount: fieldDefinitionRowEnd,
                typeReferenceRowCount: 256,
                typeSpecificationRowCount: 512,
                methodDefinitionRowCount: 256,
                interfaceImplementationRowCount: 256,
                genericParameterRowCount: 256,
                genericParameterConstraintRowCount: 256);
            return MetadataSourceEndIdentity.Create(module, fact);
        }

        private static StaticFieldTypeDefinitionIdentity CreatePinnedType(
            StaticFieldMetadataModuleIdentity module,
            int token,
            string namespaceName,
            string name,
            int attributes,
            int genericParameterCount,
            int introducedGenericArity,
            int? extendsMetadataToken,
            StaticFieldTypeDefinitionIdentity? enclosingType = null,
            int methodListRowId = 1,
            int methodListEndExclusiveRowId = 1,
            int fieldListRowId = 1,
            int fieldListEndExclusiveRowId = 1) =>
            StaticFieldTypeDefinitionIdentity.Create(
                module,
                token,
                fieldListRowId,
                fieldListEndExclusiveRowId,
                methodListRowId,
                methodListEndExclusiveRowId,
                namespaceName,
                name,
                attributes,
                genericParameterCount,
                introducedGenericArity,
                extendsMetadataToken,
                enclosingType);
    }
}
