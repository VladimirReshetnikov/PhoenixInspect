using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises complete pointer-aware MethodDef ownership and shared-grammar declaration draft projection.</summary>
public sealed class W8MethodDefinitionTableCatalogContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves direct and reordered MethodPtr ownership cover all four receiver and generic draft forms.</summary>
    /// <param name="useMethodPointers">Whether ownership follows the synthetic reordered MethodPtr table.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Fast")]
    public void Exact_catalog_derives_pointer_aware_owners_and_all_method_declaration_forms(
        bool useMethodPointers)
    {
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 3,
            methodDefinitionRows: 4,
            methodPointerRows: useMethodPointers ? 4 : 0);
        var typeRows = ImmutableArray.Create(
            TypeRow(module, rowId: 1, methodStart: 0),
            TypeRow(module, rowId: 2, methodStart: 1),
            TypeRow(module, rowId: 3, methodStart: 3));
        var types = CreateTypeCatalog(
            sourceEnds,
            typeRows,
            useMethodPointers ? [4, 1, 3, 2] : default);
        var observations = ImmutableArray.Create(
            MethodRow(module, 1, isStatic: false, [0x20, 0x01, 0x08, 0x0E]),
            MethodRow(module, 2, isStatic: true, [0x10, 0x02, 0x01, 0x13, 0x00, 0x1E, 0x01]),
            MethodRow(module, 3, isStatic: true, [0x00, 0x00, 0x01]),
            MethodRow(module, 4, isStatic: false, [0x30, 0x01, 0x02, 0x1D, 0x0E, 0x10, 0x08, 0x0F, 0x01]));

        var catalog = MetadataMethodDefinitionTableCatalogIdentity.Create(types, observations);

        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, types.ResultKind);
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataMethodDefinitionTableIssue.None, catalog.Issue);
        Assert.Equal(sourceEnds, catalog.SourceEnds);
        Assert.Equal(types, catalog.TypeDefinitionCatalog);
        Assert.Null(catalog.ReachedBound);
        Assert.Null(catalog.StoppedMethodDefinitionToken);
        Assert.Equal(0, catalog.ObservedCount);
        Assert.Equal([0x06000001, 0x06000002, 0x06000003, 0x06000004],
            catalog.Rows.Select(static row => row.MethodDefinitionToken).ToArray());
        Assert.Equal(
            useMethodPointers
                ? [0x02000002, 0x02000003, 0x02000003, 0x02000002]
                : [0x02000002, 0x02000002, 0x02000003, 0x02000003],
            catalog.Rows.Select(static row => row.DeclaringTypeDefinitionToken).ToArray());

        Assert.Equal([0x20, 0x10, 0x00, 0x30],
            catalog.Rows.Select(static row => row.SignatureHeader).ToArray());
        Assert.Equal([true, false, false, true],
            catalog.Rows.Select(static row => row.HasThis).ToArray());
        Assert.All(catalog.Rows, static row => Assert.False(row.HasExplicitThis));
        Assert.Equal([false, true, true, false],
            catalog.Rows.Select(static row => row.IsStatic).ToArray());
        Assert.Equal([true, false, false, true],
            catalog.Rows.Select(static row => row.IsInstance).ToArray());
        Assert.Equal([0, 2, 0, 1],
            catalog.Rows.Select(static row => row.DeclaredGenericArity).ToArray());
        Assert.Equal([false, true, false, true],
            catalog.Rows.Select(static row => row.IsGeneric).ToArray());
        Assert.Equal([1, 1, 0, 2],
            catalog.Rows.Select(static row => row.ParameterCount).ToArray());
        Assert.All(catalog.Rows, row =>
        {
            Assert.Equal(0, row.CallingConvention);
            Assert.True(row.AggregateTypeCount > 0);
            Assert.True(row.ProjectedSignatureNodeCount >= row.AggregateTypeCount);
            Assert.True(row.MaximumSignatureDepth > 0);
            Assert.True(row.SignatureSourceEndObserved);
            Assert.Equal(sourceEnds, row.SourceEnds);
        });
        Assert.Equal(2, catalog.Rows[1].MaximumDeclaredGenericParameterCount);
        Assert.Equal(1, catalog.Rows[3].MaximumDeclaredGenericParameterCount);
        Assert.Equal([0x1220, 0x1240, 0x1260, 0x1280],
            catalog.Rows.Select(static row => row.Observation.RelativeVirtualAddress).ToArray());
        Assert.All(catalog.Rows, static row => Assert.Equal(
            (int)(MethodImplAttributes.NoInlining | MethodImplAttributes.NoOptimization),
            row.Observation.ImplementationAttributes));
        Assert.Equal(["ProjectedMethod1", "ProjectedMethod2", "ProjectedMethod3", "ProjectedMethod4"],
            catalog.Rows.Select(static row => row.Observation.Name).ToArray());
        Assert.Equal([3, 6, 9, 12],
            catalog.Rows.Select(static row => row.Observation.ParameterListRowId).ToArray());
        Assert.All(catalog.Rows, static row => Assert.True(
            (row.Observation.Attributes & (int)MethodAttributes.HideBySig) != 0));
        Assert.Equal(observations.Select(static row => row.SignaturePrefixBytes.ToArray()),
            catalog.Rows.Select(static row => row.SignatureBytes.ToArray()));
    }

    /// <summary>Proves one owner may physically declare more than 64 methods and a larger generic arity exactly.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Physical_owner_cardinality_above_evaluator_admission_remains_exact()
    {
        const int methodCount = 65;
        var module = CreateMetadataModule();
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 2, methodDefinitionRows: methodCount);
        var types = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [TypeRow(module, 1, 0), TypeRow(module, 2, 1)]);
        var builder = ImmutableArray.CreateBuilder<MetadataMethodDefinitionRowObservationIdentity>(methodCount);
        builder.Add(MethodRow(module, 1, isStatic: true, [0x10, 0x41, 0x00, 0x01]));
        for (var rowId = 2; rowId <= methodCount; rowId++)
        {
            builder.Add(MethodRow(module, rowId, isStatic: true, [0x00, 0x00, 0x01]));
        }

        var catalog = MetadataMethodDefinitionTableCatalogIdentity.Create(types, builder.MoveToImmutable());

        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(methodCount, catalog.Rows.Length);
        Assert.All(catalog.Rows, static row => Assert.Equal(0x02000002, row.DeclaringTypeDefinitionToken));
        Assert.Equal(methodCount, catalog.Rows[0].DeclaredGenericArity);
        Assert.Null(catalog.ReachedBound);

        var componentLength = catalog.TypeDefinitionCatalog.CanonicalBytes.Length +
                              catalog.Rows.Sum(static row => row.CanonicalBytes.Length);
        Assert.InRange(catalog.CanonicalBytes.Length, componentLength, componentLength + 1024);
    }

    /// <summary>Proves missing and duplicate ownership dependencies become typed prefix-free invalid draft results.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ownership_requires_one_derived_TypeDef_for_every_MethodDef()
    {
        var module = CreateMetadataModule();
        var directEnds = CreateSourceEnds(module, typeDefinitionRows: 2, methodDefinitionRows: 2);
        var missingTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            directEnds,
            [TypeRow(module, 1, 0), TypeRow(module, 2, 2)]);
        var methods = ImmutableArray.Create(
            MethodRow(module, 1, isStatic: true, [0x00, 0x00, 0x01]),
            MethodRow(module, 2, isStatic: true, [0x00, 0x00, 0x01]));

        var missing = MetadataMethodDefinitionTableCatalogIdentity.Create(missingTypes, methods);

        Assert.Equal(MetadataTypeDefinitionTableResultKind.Invalid, missingTypes.ResultKind);
        AssertInvalid(missing, MetadataMethodDefinitionTableIssue.OwnershipMissing);

        var pointerEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 2,
            methodDefinitionRows: 2,
            methodPointerRows: 2);
        var duplicatePointers = MetadataMemberPointerTableCatalogIdentity.Create(
            pointerEnds,
            default,
            PointerRows(module, [1, 1]));
        var duplicateTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            pointerEnds,
            [TypeRow(module, 1, 0), TypeRow(module, 2, 1)],
            duplicatePointers);
        var duplicate = MetadataMethodDefinitionTableCatalogIdentity.Create(duplicateTypes, methods);

        Assert.Equal(MetadataMemberPointerTableResultKind.Invalid, duplicatePointers.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Invalid, duplicateTypes.ResultKind);
        AssertInvalid(duplicate, MetadataMethodDefinitionTableIssue.OwnershipDuplicate);

        var outOfRangePointers = MetadataMemberPointerTableCatalogIdentity.Create(
            pointerEnds,
            default,
            PointerRows(module, [3, 1]));
        var outOfRangeTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            pointerEnds,
            [TypeRow(module, 1, 0), TypeRow(module, 2, 1)],
            outOfRangePointers);
        var outOfRange = MetadataMethodDefinitionTableCatalogIdentity.Create(outOfRangeTypes, methods);

        Assert.Equal(MetadataMemberPointerTableResultKind.Invalid, outOfRangePointers.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Invalid, outOfRangeTypes.ResultKind);
        AssertInvalid(outOfRange, MetadataMethodDefinitionTableIssue.OwnershipTargetOutOfRange);

        var wrongTablePointers = MetadataMemberPointerTableCatalogIdentity.Create(
            pointerEnds,
            default,
            [
                MetadataMemberPointerRowObservationIdentity.Create(module, 0x05000001, 0x04000001),
                MetadataMemberPointerRowObservationIdentity.Create(module, 0x05000002, 0x06000002),
            ]);
        var wrongTableTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            pointerEnds,
            [TypeRow(module, 1, 0), TypeRow(module, 2, 1)],
            wrongTablePointers);
        var wrongTable = MetadataMethodDefinitionTableCatalogIdentity.Create(wrongTableTypes, methods);

        Assert.Equal(MetadataMemberPointerTableResultKind.Invalid, wrongTablePointers.ResultKind);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Invalid, wrongTableTypes.ResultKind);
        AssertInvalid(wrongTable, MetadataMethodDefinitionTableIssue.OwnershipTargetOutOfRange);
    }

    /// <summary>Proves malformed, trailing, and receiver-contradictory signatures expose no derived draft prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Signature_and_receiver_contradictions_are_typed_and_prefix_free()
    {
        var module = CreateMetadataModule();
        var types = ExactTypeCatalogForMethods(module, methodCount: 2);
        var valid = MethodRow(module, 1, isStatic: true, [0x00, 0x00, 0x01]);

        var malformed = MetadataMethodDefinitionTableCatalogIdentity.Create(
            types,
            [valid, MethodRow(module, 2, isStatic: true, [])]);
        AssertInvalid(malformed, MetadataMethodDefinitionTableIssue.SignatureInvalid, 0x06000002);

        var trailing = MetadataMethodDefinitionTableCatalogIdentity.Create(
            types,
            [valid, MethodRow(module, 2, isStatic: true, [0x00, 0x00, 0x01, 0x08])]);
        AssertInvalid(trailing, MetadataMethodDefinitionTableIssue.SignatureTrailingData, 0x06000002);

        var staticWithReceiver = MetadataMethodDefinitionTableCatalogIdentity.Create(
            types,
            [valid, MethodRow(module, 2, isStatic: true, [0x20, 0x00, 0x01])]);
        AssertInvalid(staticWithReceiver, MetadataMethodDefinitionTableIssue.ReceiverMismatch, 0x06000002);

        var instanceWithoutReceiver = MetadataMethodDefinitionTableCatalogIdentity.Create(
            types,
            [valid, MethodRow(module, 2, isStatic: false, [0x00, 0x00, 0x01])]);
        AssertInvalid(instanceWithoutReceiver, MetadataMethodDefinitionTableIssue.ReceiverMismatch, 0x06000002);

        var explicitReceiver = MetadataMethodDefinitionTableCatalogIdentity.Create(
            types,
            [valid, MethodRow(module, 2, isStatic: false, [0x60, 0x00, 0x01])]);
        AssertInvalid(explicitReceiver, MetadataMethodDefinitionTableIssue.SignatureInvalid, 0x06000002);
    }

    /// <summary>Proves source count, RID order, module identity, and dependency stops remain prefix-free draft facts.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complete_source_and_physical_RID_order_are_required()
    {
        var module = CreateMetadataModule();
        var emptyEnds = CreateSourceEnds(module, typeDefinitionRows: 1, methodDefinitionRows: 0);
        var emptyTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(
            emptyEnds,
            [TypeRow(module, 1, 0)]);
        var defaultEmpty = MetadataMethodDefinitionTableCatalogIdentity.Create(emptyTypes, default);
        var explicitEmpty = MetadataMethodDefinitionTableCatalogIdentity.Create(
            emptyTypes,
            ImmutableArray<MetadataMethodDefinitionRowObservationIdentity>.Empty);
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, defaultEmpty.ResultKind);
        Assert.Empty(defaultEmpty.Rows);
        Assert.Equal(defaultEmpty, explicitEmpty);

        var types = ExactTypeCatalogForMethods(module, methodCount: 2);
        var first = MethodRow(module, 1, isStatic: true, [0x00, 0x00, 0x01]);
        var second = MethodRow(module, 2, isStatic: false, [0x20, 0x01, 0x08, 0x0E]);

        var unavailable = MetadataMethodDefinitionTableCatalogIdentity.Create(types, default);
        AssertNonExact(unavailable, MetadataMethodDefinitionTableIssue.TableIncomplete, observedCount: 0);

        var incomplete = MetadataMethodDefinitionTableCatalogIdentity.Create(types, [first]);
        AssertNonExact(incomplete, MetadataMethodDefinitionTableIssue.TableIncomplete, observedCount: 1);

        var surplus = MetadataMethodDefinitionTableCatalogIdentity.Create(
            types,
            [first, second, MethodRow(module, 3, isStatic: true, [0x00, 0x00, 0x01])]);
        AssertInvalid(surplus, MetadataMethodDefinitionTableIssue.TableRowCountConflict);
        Assert.Equal(3, surplus.ObservedCount);

        var permuted = MetadataMethodDefinitionTableCatalogIdentity.Create(types, [second, first]);
        AssertInvalid(permuted, MetadataMethodDefinitionTableIssue.PhysicalOrderInvalid, 0x06000002);

        var otherModule = CreateMetadataModule(moduleAddress: 0xA000, digestCharacter: 'b');
        var foreign = MetadataMethodDefinitionTableCatalogIdentity.Create(
            types,
            [first, MethodRow(otherModule, 2, isStatic: false, [0x20, 0x01, 0x08, 0x0E])]);
        AssertInvalid(foreign, MetadataMethodDefinitionTableIssue.SourceModuleMismatch, 0x06000002);

        var overEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 1,
            methodDefinitionRows: StaticFieldV2Limits.MaximumMethodDefinitionRowCount + 1);
        var stoppedTypes = MetadataTypeDefinitionTableCatalogIdentity.Create(overEnds, default);
        var stopped = MetadataMethodDefinitionTableCatalogIdentity.Create(stoppedTypes, default);
        AssertNonExact(
            stopped,
            MetadataMethodDefinitionTableIssue.TypeDefinitionCatalogNonExact,
            StaticFieldV2Limits.MaximumMethodDefinitionRowCount + 1);
        Assert.Equal(ExpressionV2ContractLimits.MethodDefinitionRowCountBoundName, stopped.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumMethodDefinitionRowCount, stopped.ReachedBound.Value);
    }

    /// <summary>Proves byte, depth, generic-argument, parameter, and array-rank draft caps retain cap-plus-one facts.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Shared_signature_grammar_reports_each_reachable_MethodDef_bound()
    {
        var module = CreateMetadataModule();

        var longBytes = new byte[MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount + 1];
        longBytes[0] = 0x00;
        longBytes[1] = 0x00;
        longBytes[2] = 0x01;
        var exactCapPlusOne = MethodRow(module, 1, isStatic: true, longBytes.ToImmutableArray());
        var longerSource = MethodRow(
            module,
            1,
            isStatic: true,
            longBytes.ToImmutableArray(),
            signatureByteCount: 1024);
        AssertBound(
            MetadataMethodDefinitionTableCatalogIdentity.Create(
                ExactTypeCatalogForMethods(module, methodCount: 1),
                [exactCapPlusOne]),
            MetadataMethodDefinitionTableIssue.SignatureByteBoundReached,
            MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCountBoundName,
            MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount,
            MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount + 1);
        AssertBound(
            MetadataMethodDefinitionTableCatalogIdentity.Create(
                ExactTypeCatalogForMethods(module, methodCount: 1),
                [longerSource]),
            MetadataMethodDefinitionTableIssue.SignatureByteBoundReached,
            MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCountBoundName,
            MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount,
            MetadataMethodDefinitionRowObservationIdentity.MaximumSignatureByteCount + 1);
        Assert.Equal(257, exactCapPlusOne.SignatureByteCount);
        Assert.Equal(1024, longerSource.SignatureByteCount);
        Assert.True(exactCapPlusOne.SignaturePrefixBytes.AsSpan().SequenceEqual(
            longerSource.SignaturePrefixBytes.AsSpan()));
        Assert.NotEqual(exactCapPlusOne, longerSource);

        var incompleteSignature = MethodRow(
            module,
            1,
            isStatic: true,
            [0x00, 0x00],
            signatureSourceEndObserved: false);
        var incompleteResult = MetadataMethodDefinitionTableCatalogIdentity.Create(
            ExactTypeCatalogForMethods(module, methodCount: 1),
            [incompleteSignature]);
        Assert.False(incompleteSignature.SignatureSourceEndObserved);
        Assert.Null(incompleteSignature.SignatureByteCount);
        AssertNonExact(
            incompleteResult,
            MetadataMethodDefinitionTableIssue.SignatureIncomplete,
            observedCount: 2);
        Assert.Null(incompleteResult.ReachedBound);
        Assert.Equal(0x06000001, incompleteResult.StoppedMethodDefinitionToken);

        var depthSignature = ImmutableArray.CreateBuilder<byte>();
        depthSignature.AddRange(new byte[] { 0x00, 0x00 });
        depthSignature.AddRange(Enumerable.Repeat((byte)0x1D, StaticFieldV2Limits.MaximumTypeSpecificationDepth));
        depthSignature.Add(0x08);
        AssertBound(
            CatalogForOneMethod(module, depthSignature.ToImmutable()),
            MetadataMethodDefinitionTableIssue.SignatureDepthBoundReached,
            ExpressionV2ContractLimits.TypeSpecificationDepthBoundName,
            StaticFieldV2Limits.MaximumTypeSpecificationDepth,
            StaticFieldV2Limits.MaximumTypeSpecificationDepth + 1);

        var genericArguments = ImmutableArray.CreateBuilder<byte>();
        genericArguments.AddRange(new byte[] { 0x00, 0x00, 0x15, 0x12, 0x04, 0x41 });
        genericArguments.AddRange(Enumerable.Repeat(
            (byte)0x08,
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount + 1));
        AssertBound(
            CatalogForOneMethod(module, genericArguments.ToImmutable()),
            MetadataMethodDefinitionTableIssue.SignatureGenericArgumentBoundReached,
            ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount + 1);

        AssertBound(
            CatalogForOneMethod(module, [0x00, 0x81, 0x01, 0x01]),
            MetadataMethodDefinitionTableIssue.SignatureParameterBoundReached,
            ExpressionV2ContractLimits.ParameterCountBoundName,
            StaticFieldV2Limits.MaximumParameterCount,
            StaticFieldV2Limits.MaximumParameterCount + 1);

        AssertBound(
            CatalogForOneMethod(module, [0x00, 0x00, 0x14, 0x08, 0x21, 0x00, 0x00]),
            MetadataMethodDefinitionTableIssue.SignatureArrayRankBoundReached,
            ExpressionV2ContractLimits.ArrayRankBoundName,
            StaticFieldV2Limits.MaximumArrayRank,
            StaticFieldV2Limits.MaximumArrayRank + 1);
    }

    /// <summary>Proves input aliases, returned arrays, signatures, and canonical draft bytes cannot change replay.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_catalog_replays_canonically_and_copies_every_mutable_boundary()
    {
        var module = CreateMetadataModule();
        var types = ExactTypeCatalogForMethods(module, methodCount: 2);
        var signatureBacking = new byte[] { 0x00, 0x00, 0x01 };
        var first = MethodRow(
            module,
            1,
            isStatic: true,
            ImmutableCollectionsMarshal.AsImmutableArray(signatureBacking));
        var second = MethodRow(module, 2, isStatic: false, [0x20, 0x01, 0x08, 0x0E]);
        var observationBacking = new[] { first, second };
        var observations = ImmutableCollectionsMarshal.AsImmutableArray(observationBacking);

        var catalog = MetadataMethodDefinitionTableCatalogIdentity.Create(types, observations);
        var replay = MetadataMethodDefinitionTableCatalogIdentity.Create(types, [first, second]);
        var replayObservation = MethodRow(module, 1, isStatic: true, [0x00, 0x00, 0x01]);
        var differentRvaObservation = MethodRow(
            module,
            1,
            isStatic: true,
            [0x00, 0x00, 0x01],
            relativeVirtualAddress: first.RelativeVirtualAddress + 1);
        var originalSha = catalog.Sha256;
        var originalCanonical = catalog.CanonicalBytes;

        signatureBacking[0] = 0x20;
        observationBacking[0] = second;
        var returnedRows = catalog.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[1];
        var returnedSignature = catalog.Rows[0].SignatureBytes;
        ImmutableCollectionsMarshal.AsArray(returnedSignature)![0] = 0x20;
        var returnedObservationBytes = first.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedObservationBytes)![0] ^= 0x5A;
        var returnedRowBytes = catalog.Rows[0].CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedRowBytes)![0] ^= 0x5A;
        var returnedCatalogBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedCatalogBytes)![0] ^= 0x5A;

        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(first, replayObservation);
        Assert.Equal(first.Sha256, replayObservation.Sha256);
        Assert.NotEqual(first, differentRvaObservation);
        Assert.Equal(catalog, replay);
        Assert.Equal(catalog.GetHashCode(), replay.GetHashCode());
        Assert.Equal(originalSha, catalog.Sha256);
        Assert.True(originalCanonical.AsSpan().SequenceEqual(catalog.CanonicalBytes.AsSpan()));
        Assert.Equal(0x06000001, catalog.Rows[0].MethodDefinitionToken);
        Assert.Equal(0x00, catalog.Rows[0].SignatureBytes[0]);
        Assert.NotEqual(returnedObservationBytes[0], first.CanonicalBytes[0]);
        Assert.NotEqual(returnedRowBytes[0], catalog.Rows[0].CanonicalBytes[0]);
        Assert.NotEqual(returnedCatalogBytes[0], catalog.CanonicalBytes[0]);
    }

    /// <summary>Proves the new public draft surface exposes physical observations and catalog-only derived rows.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void MethodDefinition_table_surface_has_draft_XML_and_guarded_exact_issuance()
    {
        var observationType = typeof(MetadataMethodDefinitionRowObservationIdentity);
        var rowType = typeof(MetadataMethodDefinitionTableRowIdentity);
        var catalogType = typeof(MetadataMethodDefinitionTableCatalogIdentity);

        Assert.Null(observationType.GetProperty("DeclaringType"));
        Assert.Null(observationType.GetProperty("DeclaringTypeDefinitionToken"));
        Assert.NotNull(rowType.GetProperty(nameof(MetadataMethodDefinitionTableRowIdentity.HasExplicitThis)));
        Assert.Empty(rowType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(rowType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(catalogType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            ["Create"],
            observationType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name).ToArray());
        Assert.Equal(
            ["Create"],
            catalogType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name).ToArray());
        Assert.Throws<ArgumentException>(() => MetadataMethodDefinitionTableRowIdentity.Create(
            new object(),
            null!,
            null!,
            0x02000001,
            default));
        Assert.False(MetadataMethodDefinitionTableCatalogIdentity.OwnsRowMintCapability(new object()));
        Assert.All(new[] { observationType, rowType, catalogType }, static type => Assert.True(type.IsSealed));

        var module = CreateMetadataModule();
        var emptyName = MetadataMethodDefinitionRowObservationIdentity.Create(
            module,
            0x06000001,
            relativeVirtualAddress: 0,
            implementationAttributes: ushort.MaxValue,
            attributes: ushort.MaxValue,
            name: string.Empty,
            signaturePrefixBytes: [0x00, 0x00, 0x01],
            signatureByteCount: 3,
            parameterListRowId: 0);
        Assert.Equal(string.Empty, emptyName.Name);
        Assert.Equal(ushort.MaxValue, emptyName.ImplementationAttributes);
        Assert.Equal(ushort.MaxValue, emptyName.Attributes);
        var emptyNameCatalog = MetadataMethodDefinitionTableCatalogIdentity.Create(
            ExactTypeCatalogForMethods(module, methodCount: 1),
            [emptyName]);
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, emptyNameCatalog.ResultKind);
        Assert.Equal(string.Empty, emptyNameCatalog.Rows[0].Observation.Name);
        Assert.Equal(ushort.MaxValue, emptyNameCatalog.Rows[0].Observation.ImplementationAttributes);
        Assert.Equal(ushort.MaxValue, emptyNameCatalog.Rows[0].Observation.Attributes);
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataMethodDefinitionRowObservationIdentity.Create(
            module,
            0x06000001,
            relativeVirtualAddress: 0,
            implementationAttributes: -1,
            attributes: 0,
            name: "M",
            signaturePrefixBytes: [0x00, 0x00, 0x01],
            signatureByteCount: 3,
            parameterListRowId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataMethodDefinitionRowObservationIdentity.Create(
            module,
            0x06000001,
            relativeVirtualAddress: 0,
            implementationAttributes: 0,
            attributes: ushort.MaxValue + 1,
            name: "M",
            signaturePrefixBytes: [0x00, 0x00, 0x01],
            signatureByteCount: 3,
            parameterListRowId: 0));
        Assert.Throws<ArgumentException>(() => MetadataMethodDefinitionRowObservationIdentity.Create(
            module,
            0x06000001,
            relativeVirtualAddress: 0,
            implementationAttributes: 0,
            attributes: 0,
            name: "M",
            signaturePrefixBytes: [0x00, 0x00, 0x01],
            signatureByteCount: 4,
            parameterListRowId: 0));
        Assert.Equal(0, MethodRow(
            module,
            1,
            isStatic: true,
            [0x00, 0x00, 0x01],
            parameterListRowId: 0).ParameterListRowId);
        Assert.Equal(0x01000000, MethodRow(
            module,
            1,
            isStatic: true,
            [0x00, 0x00, 0x01],
            parameterListRowId: 0x01000000).ParameterListRowId);
        Assert.Throws<ArgumentOutOfRangeException>(() => MethodRow(
            module,
            1,
            isStatic: true,
            [0x00, 0x00, 0x01],
            parameterListRowId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MethodRow(
            module,
            1,
            isStatic: true,
            [0x00, 0x00, 0x01],
            parameterListRowId: 0x01000001));

        var assembly = catalogType.Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataMethodDefinitionTableResultKind),
            typeof(MetadataMethodDefinitionTableIssue),
            observationType,
            rowType,
            catalogType,
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

    private static void AssertBound(
        MetadataMethodDefinitionTableCatalogIdentity result,
        MetadataMethodDefinitionTableIssue issue,
        string boundName,
        int boundValue,
        int observedCount)
    {
        AssertNonExact(result, issue, observedCount);
        Assert.Equal(boundName, result.ReachedBound!.Name);
        Assert.Equal(boundValue, result.ReachedBound.Value);
        Assert.Equal(0x06000001, result.StoppedMethodDefinitionToken);
    }

    private static void AssertNonExact(
        MetadataMethodDefinitionTableCatalogIdentity result,
        MetadataMethodDefinitionTableIssue issue,
        int observedCount)
    {
        Assert.Equal(MetadataMethodDefinitionTableResultKind.NonExact, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Rows);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static void AssertInvalid(
        MetadataMethodDefinitionTableCatalogIdentity result,
        MetadataMethodDefinitionTableIssue issue,
        int? stoppedMethodDefinitionToken = null)
    {
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Invalid, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Rows);
        Assert.Null(result.ReachedBound);
        Assert.Equal(stoppedMethodDefinitionToken, result.StoppedMethodDefinitionToken);
    }

    private static MetadataMethodDefinitionTableCatalogIdentity CatalogForOneMethod(
        StaticFieldMetadataModuleIdentity module,
        ImmutableArray<byte> signature) =>
        MetadataMethodDefinitionTableCatalogIdentity.Create(
            ExactTypeCatalogForMethods(module, methodCount: 1),
            [MethodRow(module, 1, isStatic: true, signature)]);

    private static MetadataTypeDefinitionTableCatalogIdentity ExactTypeCatalogForMethods(
        StaticFieldMetadataModuleIdentity module,
        int methodCount)
    {
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRows: 2, methodDefinitionRows: methodCount);
        return MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [TypeRow(module, 1, 0), TypeRow(module, 2, 1)]);
    }

    private static MetadataTypeDefinitionTableCatalogIdentity CreateTypeCatalog(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> typeRows,
        ImmutableArray<int> methodPointerTargets)
    {
        if (methodPointerTargets.IsDefault)
        {
            return MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, typeRows);
        }
        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            default,
            PointerRows(sourceEnds.SourceModule, methodPointerTargets));
        return MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, typeRows, pointers);
    }

    private static ImmutableArray<MetadataMemberPointerRowObservationIdentity> PointerRows(
        StaticFieldMetadataModuleIdentity module,
        ImmutableArray<int> targetRowIds)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataMemberPointerRowObservationIdentity>(targetRowIds.Length);
        for (var index = 0; index < targetRowIds.Length; index++)
        {
            builder.Add(MetadataMemberPointerRowObservationIdentity.Create(
                module,
                0x05000000 | checked(index + 1),
                0x06000000 | targetRowIds[index]));
        }
        return builder.MoveToImmutable();
    }

    private static MetadataMethodDefinitionRowObservationIdentity MethodRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        bool isStatic,
        ImmutableArray<byte> signature,
        int? relativeVirtualAddress = null,
        int? parameterListRowId = null,
        int? signatureByteCount = null,
        bool signatureSourceEndObserved = true) =>
        MetadataMethodDefinitionRowObservationIdentity.Create(
            module,
            0x06000000 | rowId,
            relativeVirtualAddress ?? 0x1200 + rowId * 0x20,
            (int)(MethodImplAttributes.NoInlining | MethodImplAttributes.NoOptimization),
            (int)(MethodAttributes.Public | MethodAttributes.HideBySig |
                  (isStatic ? MethodAttributes.Static : default)),
            $"ProjectedMethod{rowId}",
            signature,
            signatureSourceEndObserved ? signatureByteCount ?? signature.Length : null,
            parameterListRowId ?? rowId * 3);

    private static MetadataTypeDefinitionRowObservationIdentity TypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int methodStart) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x02000000 | rowId,
            fieldListRowId: 0,
            methodStart,
            "Synthetic.MethodDefinitions",
            rowId == 1 ? "<Module>" : $"Container{rowId}",
            rowId == 1 ? 0 : (int)TypeAttributes.Public,
            extendsMetadataToken: null);

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRows,
        int methodDefinitionRows,
        int methodPointerRows = 0) =>
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
                methodPointerRowCount: methodPointerRows));

    private static StaticFieldMetadataModuleIdentity CreateMetadataModule(
        ulong moduleAddress = 0x9000,
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
            $"methoddef-catalog-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.MethodDefinitionCatalog",
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
