using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises W8 GenericParam declaration, table, owner-selection, and binding draft proofs.</summary>
public sealed class W8MetadataGenericParameterProofContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves TypeDef and shared-grammar MethodDef arity declarations, receiver consistency, and owner caps.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Owner_declarations_require_exact_source_ends_and_shared_method_grammar()
    {
        var fixture = new SyntheticFixture();
        var typeDeclaration = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            fixture.SourceEnds(5),
            fixture.GenericHostRaw);
        var method = fixture.CreateMethod(
            rowId: 1,
            attributes: (int)MethodAttributes.Public,
            signature: GenericInstanceMethodSignature());
        var methodDeclaration = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            fixture.SourceEnds(5),
            method);

        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, typeDeclaration.ResultKind);
        Assert.Equal(2, typeDeclaration.DeclaredArity);
        Assert.Equal(fixture.SourceEnds(5), typeDeclaration.SourceEnds);
        Assert.Null(typeDeclaration.MethodSignatureCertificate);
        Assert.Equal(fixture.GenericHostRaw, typeDeclaration.Owner.TypeDefinition);

        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, methodDeclaration.ResultKind);
        Assert.Equal(3, methodDeclaration.DeclaredArity);
        var certificate = Assert.IsType<MetadataGenericMethodDeclarationCertificateIdentity>(
            methodDeclaration.MethodSignatureCertificate);
        Assert.Equal(method, certificate.MethodDefinition);
        Assert.True(certificate.HasThis);
        Assert.False(certificate.HasExplicitThis);
        Assert.Equal(3, certificate.GenericParameterCount);
        Assert.Equal(2, certificate.ParameterCount);
        Assert.Equal(method.Signature.Length, certificate.SignatureByteCount);

        var staticMethod = fixture.CreateMethod(
            rowId: 2,
            attributes: (int)(MethodAttributes.Public | MethodAttributes.Static),
            signature: GenericStaticMethodSignature(3));
        var staticDeclaration = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            fixture.SourceEnds(5),
            staticMethod);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, staticDeclaration.ResultKind);
        Assert.False(staticDeclaration.MethodSignatureCertificate!.HasThis);

        var receiverMismatch = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            fixture.SourceEnds(5),
            fixture.CreateMethod(
                rowId: 3,
                attributes: (int)(MethodAttributes.Public | MethodAttributes.Static),
                signature: GenericInstanceMethodSignature()));
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, receiverMismatch.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.MethodReceiverMismatch, receiverMismatch.Issue);
        Assert.Null(receiverMismatch.DeclaredArity);

        var malformed = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            fixture.SourceEnds(5),
            fixture.CreateMethod(
                rowId: 4,
                attributes: (int)(MethodAttributes.Public | MethodAttributes.Static),
                signature: [0x10, 0x03]));
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, malformed.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.MethodSignatureInvalid, malformed.Issue);

        var typeAtCap = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            fixture.SourceEnds(5),
            fixture.CreateRawType(rowId: 3, arity: StaticFieldV2Limits.MaximumGenericParameterCount));
        var methodAtCap = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            fixture.SourceEnds(5),
            fixture.CreateMethod(
                rowId: 5,
                attributes: (int)(MethodAttributes.Public | MethodAttributes.Static),
                signature: GenericStaticMethodSignature(StaticFieldV2Limits.MaximumGenericParameterCount)));
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, typeAtCap.ResultKind);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount, typeAtCap.DeclaredArity);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, methodAtCap.ResultKind);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount, methodAtCap.DeclaredArity);

        var typeOverCap = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            fixture.SourceEnds(5),
            fixture.CreateRawType(rowId: 4, arity: StaticFieldV2Limits.MaximumGenericParameterCount + 1));
        var methodOverCap = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            fixture.SourceEnds(5),
            fixture.CreateMethod(
                rowId: 6,
                attributes: (int)(MethodAttributes.Public | MethodAttributes.Static),
                signature: GenericStaticMethodSignature(StaticFieldV2Limits.MaximumGenericParameterCount + 1)));
        AssertPrefixFreeOwnerCap(typeOverCap);
        AssertPrefixFreeOwnerCap(methodOverCap);

        var wrongSource = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            fixture.OtherSourceEnds(),
            fixture.GenericHostRaw);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, wrongSource.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.SourceModuleMismatch, wrongSource.Issue);
        Assert.Null(wrongSource.DeclaredArity);

        var narrowSource = fixture.SourceEnds(genericParameterRows: 0, typeDefinitionRows: 0);
        var outOfRange = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            narrowSource,
            fixture.GenericHostRaw);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, outOfRange.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerTokenOutOfRange, outOfRange.Issue);

        var methodOutOfRange = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            fixture.SourceEnds(genericParameterRows: 0, methodDefinitionRows: 0),
            method);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, methodOutOfRange.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerTokenOutOfRange, methodOutOfRange.Issue);
    }

    /// <summary>Proves complete physical RID and coded-owner ordering while allowing Number normalization by owner.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Module_table_proves_physical_order_owner_sort_and_unique_positions()
    {
        var fixture = new SyntheticFixture();
        var sourceEnds = fixture.SourceEnds(5);
        var rows = fixture.CreateMeaningfulRows();
        var exact = MetadataGenericParameterTableCatalogIdentity.Create(sourceEnds, rows);

        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, exact.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.None, exact.Issue);
        Assert.Equal(Enumerable.Range(1, 5).Select(static rowId => 0x2A000000 | rowId),
            exact.Rows.Select(static row => row.GenericParameterToken));
        Assert.Equal([1, 0, 2, 0, 1], exact.Rows.Select(static row => row.Position).ToArray());

        var canonicalSha = exact.Sha256;
        var returnedRows = exact.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[1];
        Assert.Equal(canonicalSha, exact.Sha256);
        Assert.Equal(0x2A000001, exact.Rows[0].GenericParameterToken);

        var permuted = MetadataGenericParameterTableCatalogIdentity.Create(
            sourceEnds,
            [rows[1], rows[0], rows[2], rows[3], rows[4]]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, permuted.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.PhysicalOrderInvalid, permuted.Issue);
        Assert.Empty(permuted.Rows);

        var typeOwner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(fixture.GenericHostRaw);
        var methodOwner = MetadataGenericParameterOwnerIdentity.ForMethodDefinition(fixture.GenericMethod);
        var crossedOwners = MetadataGenericParameterTableCatalogIdentity.Create(
            fixture.SourceEnds(2),
            [
                Parameter(1, methodOwner, 0, "TMethod"),
                Parameter(2, typeOwner, 0, "TType"),
            ]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, crossedOwners.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerSortInvalid, crossedOwners.Issue);
        Assert.Empty(crossedOwners.Rows);

        var duplicatePosition = MetadataGenericParameterTableCatalogIdentity.Create(
            fixture.SourceEnds(2),
            [
                Parameter(1, typeOwner, 0, "TFirst"),
                Parameter(2, typeOwner, 0, "TRepeated"),
            ]);
        Assert.Equal(MetadataGenericParameterProofIssue.DuplicateOwnerPosition, duplicatePosition.Issue);
        Assert.Empty(duplicatePosition.Rows);
    }

    /// <summary>Proves incomplete and globally over-cap table acquisition retains exact source facts but no row prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Module_table_incomplete_invalid_and_cap_plus_one_outcomes_are_prefix_free()
    {
        var fixture = new SyntheticFixture();
        var rows = fixture.CreateMeaningfulRows();
        var sourceEnds = fixture.SourceEnds(5);

        var incomplete = MetadataGenericParameterTableCatalogIdentity.Create(
            sourceEnds,
            rows.Take(4).ToImmutableArray());
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, incomplete.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.TableIncomplete, incomplete.Issue);
        Assert.Equal(4, incomplete.ObservedCount);
        Assert.Empty(incomplete.Rows);

        var absent = MetadataGenericParameterTableCatalogIdentity.Create(sourceEnds, default);
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, absent.ResultKind);
        Assert.Equal(0, absent.ObservedCount);
        Assert.Empty(absent.Rows);

        var extra = MetadataGenericParameterTableCatalogIdentity.Create(
            sourceEnds,
            [.. rows, Parameter(6, rows[0].Owner, 2, "TExtra")]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, extra.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.TableRowCountConflict, extra.Issue);
        Assert.Empty(extra.Rows);

        var overSourceEnds = fixture.SourceEnds(
            StaticFieldV2Limits.MaximumGenericParameterRowCount + 1);
        var over = MetadataGenericParameterTableCatalogIdentity.Create(
            overSourceEnds,
            ImmutableArray<MetadataGenericParameterIdentity>.Empty);
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, over.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.TableRowBoundReached, over.Issue);
        Assert.Empty(over.Rows);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterRowCountBoundName, over.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterRowCount + 1, over.ObservedCount);

        var foreignRow = Parameter(
            1,
            MetadataGenericParameterOwnerIdentity.ForTypeDefinition(fixture.OtherRawType()),
            0,
            "TForeign");
        var foreign = MetadataGenericParameterTableCatalogIdentity.Create(
            fixture.SourceEnds(1),
            [foreignRow]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, foreign.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.SourceModuleMismatch, foreign.Issue);
        Assert.Empty(foreign.Rows);

        var outOfRangeOwner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(
            fixture.CreateRawType(rowId: 3, arity: 1));
        var outOfRange = MetadataGenericParameterTableCatalogIdentity.Create(
            fixture.SourceEnds(genericParameterRows: 1, typeDefinitionRows: 2),
            [Parameter(1, outOfRangeOwner, 0, "TBeyondSourceEnd")]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, outOfRange.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerTokenOutOfRange, outOfRange.Issue);
        Assert.Empty(outOfRange.Rows);
    }

    /// <summary>Proves selected owners cover exact declared positions, normalize Number order, and admit zero arity.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Owner_sets_require_complete_position_coverage_including_exact_zero()
    {
        var fixture = new SyntheticFixture();
        var sourceEnds = fixture.SourceEnds(5);
        var catalog = MetadataGenericParameterTableCatalogIdentity.Create(
            sourceEnds,
            fixture.CreateMeaningfulRows());
        var typeDeclaration = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            sourceEnds,
            fixture.GenericHostRaw);
        var typeSet = MetadataGenericParameterOwnerSetIdentity.Create(typeDeclaration, catalog);

        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, typeSet.ResultKind);
        Assert.Equal([0, 1], typeSet.Parameters.Select(static row => row.Position).ToArray());
        Assert.Equal([0x2A000002, 0x2A000001],
            typeSet.Parameters.Select(static row => row.GenericParameterToken).ToArray());

        var methodDeclaration = MetadataGenericParameterOwnerDeclarationIdentity.FromMethodDefinition(
            sourceEnds,
            fixture.GenericMethod);
        var methodSet = MetadataGenericParameterOwnerSetIdentity.Create(methodDeclaration, catalog);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, methodSet.ResultKind);
        Assert.Equal([0, 1, 2], methodSet.Parameters.Select(static row => row.Position).ToArray());

        var zeroDeclaration = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            sourceEnds,
            fixture.ZeroArityRaw);
        var zeroSet = MetadataGenericParameterOwnerSetIdentity.Create(zeroDeclaration, catalog);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, zeroSet.ResultKind);
        Assert.Empty(zeroSet.Parameters);

        var typeOwner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(fixture.GenericHostRaw);
        var methodOwner = MetadataGenericParameterOwnerIdentity.ForMethodDefinition(fixture.GenericMethod);
        var gapCatalog = MetadataGenericParameterTableCatalogIdentity.Create(
            fixture.SourceEnds(5),
            [
                Parameter(1, typeOwner, 0, "TLeft"),
                Parameter(2, typeOwner, 2, "TRightWrong"),
                Parameter(3, methodOwner, 0, "TInput"),
                Parameter(4, methodOwner, 1, "TState"),
                Parameter(5, methodOwner, 2, "TResult"),
            ]);
        var gapSet = MetadataGenericParameterOwnerSetIdentity.Create(typeDeclaration, gapCatalog);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, gapSet.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerPositionCoverageInvalid, gapSet.Issue);
        Assert.Empty(gapSet.Parameters);

        var overDeclaration = MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
            sourceEnds,
            fixture.CreateRawType(3, StaticFieldV2Limits.MaximumGenericParameterCount + 1));
        var overSet = MetadataGenericParameterOwnerSetIdentity.Create(overDeclaration, catalog);
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, overSet.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerArityBoundReached, overSet.Issue);
        Assert.Empty(overSet.Parameters);

        var differingEnds = fixture.SourceEnds(6);
        var differentCatalog = MetadataGenericParameterTableCatalogIdentity.Create(
            differingEnds,
            [.. fixture.CreateMeaningfulRows(), Parameter(6, methodOwner, 3, "TExtra")]);
        var sourceMismatch = MetadataGenericParameterOwnerSetIdentity.Create(typeDeclaration, differentCatalog);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, sourceMismatch.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.SourceModuleMismatch, sourceMismatch.Issue);
    }

    /// <summary>Proves binding ledgers match physical rows, canonicalize caller order, and never infer positions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Binding_ledgers_cover_each_selected_row_once_without_positional_guesses()
    {
        var fixture = new SyntheticFixture();
        var sourceEnds = fixture.SourceEnds(5);
        var catalog = MetadataGenericParameterTableCatalogIdentity.Create(
            sourceEnds,
            fixture.CreateMeaningfulRows());
        var set = MetadataGenericParameterOwnerSetIdentity.Create(
            MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
                sourceEnds,
                fixture.GenericHostRaw),
            catalog);
        var first = MetadataTypeArgumentBindingIdentity.Unavailable(set.Parameters[0]);
        var second = MetadataTypeArgumentBindingIdentity.Exact(
            set.Parameters[1],
            MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.String));

        var forward = MetadataGenericParameterBindingLedgerIdentity.Create(set, [first, second]);
        var reversed = MetadataGenericParameterBindingLedgerIdentity.Create(set, [second, first]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, forward.ResultKind);
        Assert.Equal(forward, reversed);
        Assert.Equal([0, 1], forward.Bindings.Select(static binding => binding.Parameter.Position).ToArray());
        Assert.Equal(MetadataTypeArgumentBindingKind.Unavailable, forward.Bindings[0].Kind);
        Assert.Equal(MetadataTypeArgumentBindingKind.Exact, forward.Bindings[1].Kind);

        var returnedBindings = forward.Bindings;
        ImmutableCollectionsMarshal.AsArray(returnedBindings)![0] = second;
        Assert.Equal(MetadataTypeArgumentBindingKind.Unavailable, forward.Bindings[0].Kind);

        var incomplete = MetadataGenericParameterBindingLedgerIdentity.Create(set, [first]);
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, incomplete.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.BindingIncomplete, incomplete.Issue);
        Assert.Empty(incomplete.Bindings);

        var duplicate = MetadataGenericParameterBindingLedgerIdentity.Create(set, [first, first]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, duplicate.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.DuplicateBinding, duplicate.Issue);
        Assert.Empty(duplicate.Bindings);

        var guessedRow = MetadataGenericParameterIdentity.Create(
            0x2A000020,
            set.Parameters[0].Owner,
            position: 0,
            "TGuessed",
            attributes: 0);
        var guessed = MetadataGenericParameterBindingLedgerIdentity.Create(
            set,
            [MetadataTypeArgumentBindingIdentity.Unavailable(guessedRow), second]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, guessed.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.BindingRowMismatch, guessed.Issue);

        var zeroSet = MetadataGenericParameterOwnerSetIdentity.Create(
            MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition(
                sourceEnds,
                fixture.ZeroArityRaw),
            catalog);
        var zeroLedger = MetadataGenericParameterBindingLedgerIdentity.Create(
            zeroSet,
            ImmutableArray<MetadataTypeArgumentBindingIdentity>.Empty);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, zeroLedger.ResultKind);
        Assert.Empty(zeroLedger.Bindings);
    }

    /// <summary>
    /// Proves definition authority alone issues complete mixed TypeDef and MethodDef owner groups, including exact
    /// physical groups on both sides of the evaluator arity boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Authority_catalog_issues_mixed_complete_owner_groups_and_separate_arity_admission()
    {
        var scenario = BuildAuthorityScenario();
        var catalog = MetadataGenericParameterAuthorityCatalogIdentity.Create(scenario.Authority);

        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, scenario.Authority.ResultKind);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.Unsorted,
            scenario.GenericParameters.OrderProfile);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.None, catalog.Issue);
        Assert.Equal(scenario.Authority, catalog.DefinitionAuthority);
        Assert.Equal(
            [TypeToken(1), MethodToken(1), TypeToken(2), MethodToken(2), TypeToken(3), TypeToken(4)],
            catalog.Groups.Select(static group => group.OwnerMetadataToken).ToArray());
        Assert.Equal(
            [
                MetadataGenericParameterOwnerKind.TypeDefinition,
                MetadataGenericParameterOwnerKind.MethodDefinition,
                MetadataGenericParameterOwnerKind.TypeDefinition,
                MetadataGenericParameterOwnerKind.MethodDefinition,
                MetadataGenericParameterOwnerKind.TypeDefinition,
                MetadataGenericParameterOwnerKind.TypeDefinition,
            ],
            catalog.Groups.Select(static group => group.OwnerKind).ToArray());
        Assert.Equal([0, 2, 2, 1, 64, 65],
            catalog.Groups.Select(static group => group.DeclaredArity).ToArray());

        var pairGroup = Assert.IsType<MetadataGenericParameterAuthorityOwnerGroupIdentity>(
            catalog.FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[1]));
        var methodGroup = Assert.IsType<MetadataGenericParameterAuthorityOwnerGroupIdentity>(
            catalog.FindMethodDefinitionOwner(scenario.Authority.MethodDefinitions[0]));
        Assert.Same(scenario.Authority.TypeDefinitions[1], pairGroup.TypeDefinition);
        Assert.Null(pairGroup.MethodDefinition);
        Assert.Same(scenario.Authority.MethodDefinitions[0], methodGroup.MethodDefinition);
        Assert.Null(methodGroup.TypeDefinition);
        Assert.Equal(methodGroup.MethodDefinition!.DeclaredGenericArity, methodGroup.DeclaredArity);
        Assert.Equal([0, 1], pairGroup.Parameters.Select(static row => row.Number).ToArray());
        Assert.Equal(["PairLeft", "PairRight"], pairGroup.Parameters.Select(static row => row.Name).ToArray());

        var atLimit = catalog.FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[2])!;
        var beyondLimit = catalog.FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[3])!;
        Assert.Equal(64, atLimit.Parameters.Length);
        Assert.Equal(MetadataGenericParameterOwnerAdmissionKind.Admitted, atLimit.AdmissionKind);
        Assert.True(atLimit.IsAdmitted);
        Assert.Null(atLimit.ReachedBound);
        Assert.Equal(0, atLimit.ObservedCount);
        Assert.Equal(65, beyondLimit.Parameters.Length);
        Assert.Equal(MetadataGenericParameterOwnerAdmissionKind.ArityBoundReached, beyondLimit.AdmissionKind);
        Assert.False(beyondLimit.IsAdmitted);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterCountBoundName, beyondLimit.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount, beyondLimit.ReachedBound.Value);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount + 1, beyondLimit.ObservedCount);

        var foreign = BuildAuthorityScenario(CreateMetadataModule(0x7000, 'c'));
        Assert.Null(catalog.FindTypeDefinitionOwner(foreign.Authority.TypeDefinitions[1]));
        Assert.Null(catalog.FindMethodDefinitionOwner(foreign.Authority.MethodDefinitions[0]));
        Assert.Null(MetadataGenericParameterAuthorityCatalogIdentity.Create(
            BuildAuthorityScenario(omitGenericParameterObservations: true).Authority)
            .FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[1]));
    }

    /// <summary>
    /// Proves authority-row binding ledgers normalize complex observations and keep incomplete, duplicate, wrong-owner,
    /// different-source, and arity-stop outcomes prefix-free.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Authority_binding_ledgers_normalize_rows_and_keep_every_stop_prefix_free()
    {
        var scenario = BuildAuthorityScenario();
        var catalog = MetadataGenericParameterAuthorityCatalogIdentity.Create(scenario.Authority);
        var pairGroup = catalog.FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[1])!;
        var first = MetadataGenericParameterAuthorityBindingIdentity.Unavailable(pairGroup.Parameters[0]);
        var second = MetadataGenericParameterAuthorityBindingIdentity.Exact(
            pairGroup.Parameters[1],
            MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.String));

        var forward = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(pairGroup, [first, second]);
        var reversed = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(pairGroup, [second, first]);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, forward.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.None, forward.Issue);
        Assert.Equal(scenario.Authority, forward.DefinitionAuthority);
        Assert.Equal(forward, reversed);
        Assert.Equal([0, 1], forward.Bindings.Select(static binding => binding.Parameter.Number).ToArray());
        Assert.Equal(MetadataGenericParameterAuthorityBindingKind.Unavailable, forward.Bindings[0].Kind);
        Assert.Equal(MetadataGenericParameterAuthorityBindingKind.Exact, forward.Bindings[1].Kind);
        Assert.Same(forward.Bindings[0], forward.FindBinding(0));
        Assert.Null(forward.FindBinding(2));

        var incomplete = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(pairGroup, [first]);
        AssertAuthorityLedgerStop(
            incomplete,
            MetadataGenericParameterProofResultKind.NonExact,
            MetadataGenericParameterProofIssue.BindingIncomplete);
        var absent = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(pairGroup, default);
        AssertAuthorityLedgerStop(
            absent,
            MetadataGenericParameterProofResultKind.NonExact,
            MetadataGenericParameterProofIssue.BindingIncomplete);
        var duplicate = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(pairGroup, [first, first]);
        AssertAuthorityLedgerStop(
            duplicate,
            MetadataGenericParameterProofResultKind.Invalid,
            MetadataGenericParameterProofIssue.DuplicateBinding);

        var methodGroup = catalog.FindMethodDefinitionOwner(scenario.Authority.MethodDefinitions[0])!;
        var wrongOwner = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(
            pairGroup,
            [MetadataGenericParameterAuthorityBindingIdentity.Unavailable(methodGroup.Parameters[0]), second]);
        AssertAuthorityLedgerStop(
            wrongOwner,
            MetadataGenericParameterProofResultKind.Invalid,
            MetadataGenericParameterProofIssue.BindingRowMismatch);

        var differingScenario = BuildAuthorityScenario(
            scenario.SourceEnds.SourceModule,
            pairLeftName: "ChangedPairLeft");
        var differingCatalog = MetadataGenericParameterAuthorityCatalogIdentity.Create(differingScenario.Authority);
        var differingGroup = differingCatalog.FindTypeDefinitionOwner(differingScenario.Authority.TypeDefinitions[1])!;
        Assert.Equal(pairGroup.Parameters[0].GenericParameterToken,
            differingGroup.Parameters[0].GenericParameterToken);
        Assert.Equal(pairGroup.Parameters[0].SourceEnds, differingGroup.Parameters[0].SourceEnds);
        Assert.NotEqual(pairGroup.Parameters[0], differingGroup.Parameters[0]);
        var sameSourceForgedRow = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(
            pairGroup,
            [MetadataGenericParameterAuthorityBindingIdentity.Unavailable(differingGroup.Parameters[0]), second]);
        AssertAuthorityLedgerStop(
            sameSourceForgedRow,
            MetadataGenericParameterProofResultKind.Invalid,
            MetadataGenericParameterProofIssue.BindingRowMismatch);

        var foreignScenario = BuildAuthorityScenario(CreateMetadataModule(0x7000, 'c'));
        var foreignCatalog = MetadataGenericParameterAuthorityCatalogIdentity.Create(foreignScenario.Authority);
        var foreignGroup = foreignCatalog.FindTypeDefinitionOwner(foreignScenario.Authority.TypeDefinitions[1])!;
        var differentSource = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(
            pairGroup,
            [MetadataGenericParameterAuthorityBindingIdentity.Unavailable(foreignGroup.Parameters[0]), second]);
        AssertAuthorityLedgerStop(
            differentSource,
            MetadataGenericParameterProofResultKind.Invalid,
            MetadataGenericParameterProofIssue.SourceModuleMismatch);

        var moduleGroup = catalog.FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[0])!;
        var zero = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(
            moduleGroup,
            ImmutableArray<MetadataGenericParameterAuthorityBindingIdentity>.Empty);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, zero.ResultKind);
        Assert.Empty(zero.Bindings);
        var zeroAbsent = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(moduleGroup, default);
        AssertAuthorityLedgerStop(
            zeroAbsent,
            MetadataGenericParameterProofResultKind.NonExact,
            MetadataGenericParameterProofIssue.BindingIncomplete);

        var atLimit = catalog.FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[2])!;
        var atLimitBindings = atLimit.Parameters
            .Select(MetadataGenericParameterAuthorityBindingIdentity.Unavailable)
            .Reverse()
            .ToImmutableArray();
        var exactAtLimit = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(atLimit, atLimitBindings);
        Assert.Equal(MetadataGenericParameterProofResultKind.Exact, exactAtLimit.ResultKind);
        Assert.Equal(Enumerable.Range(0, 64),
            exactAtLimit.Bindings.Select(static binding => binding.Parameter.Number));

        var beyondLimit = catalog.FindTypeDefinitionOwner(scenario.Authority.TypeDefinitions[3])!;
        var boundStop = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(
            beyondLimit,
            beyondLimit.Parameters.Select(MetadataGenericParameterAuthorityBindingIdentity.Unavailable)
                .ToImmutableArray());
        AssertAuthorityLedgerStop(
            boundStop,
            MetadataGenericParameterProofResultKind.NonExact,
            MetadataGenericParameterProofIssue.OwnerArityBoundReached);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterCountBoundName, boundStop.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount + 1, boundStop.ObservedCount);
    }

    /// <summary>
    /// Proves authority catalog prerequisite stops, guarded issuance, defensive copies, replay, and canonical draft
    /// digests for exact, non-exact, invalid, and binding outcomes.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Authority_catalog_and_ledgers_are_guarded_immutable_and_canonically_replayable()
    {
        var exactScenario = BuildAuthorityScenario();
        var exact = MetadataGenericParameterAuthorityCatalogIdentity.Create(exactScenario.Authority);
        var replay = MetadataGenericParameterAuthorityCatalogIdentity.Create(BuildAuthorityScenario().Authority);
        var incomplete = MetadataGenericParameterAuthorityCatalogIdentity.Create(
            BuildAuthorityScenario(omitGenericParameterObservations: true).Authority);
        var invalid = MetadataGenericParameterAuthorityCatalogIdentity.Create(
            BuildAuthorityScenario(moduleTypeName: "WrongModuleType").Authority);

        Assert.Equal(exact, replay);
        Assert.Equal(exact.GetHashCode(), replay.GetHashCode());
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, incomplete.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.DefinitionAuthorityNonExact, incomplete.Issue);
        Assert.Empty(incomplete.Groups);
        Assert.Equal(MetadataGenericParameterProofResultKind.Invalid, invalid.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.DefinitionAuthorityInvalid, invalid.Issue);
        Assert.Empty(invalid.Groups);

        var originalSha = exact.Sha256;
        var originalBytes = exact.CanonicalBytes;
        var returnedGroups = exact.Groups;
        ImmutableCollectionsMarshal.AsArray(returnedGroups)![0] = returnedGroups[^1];
        var pairGroup = exact.FindTypeDefinitionOwner(exactScenario.Authority.TypeDefinitions[1])!;
        var returnedRows = pairGroup.Parameters;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        var returnedGroupBytes = pairGroup.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedGroupBytes)![0] ^= 0x5A;
        var returnedCatalogBytes = exact.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedCatalogBytes)![0] ^= 0x5A;
        Assert.Equal(originalSha, exact.Sha256);
        Assert.True(originalBytes.AsSpan().SequenceEqual(exact.CanonicalBytes.AsSpan()));
        Assert.Equal(TypeToken(1), exact.Groups[0].OwnerMetadataToken);
        Assert.Equal("PairLeft", pairGroup.Parameters[0].Name);

        Assert.Throws<ArgumentException>(() =>
            MetadataGenericParameterAuthorityOwnerGroupIdentity.CreateForTypeDefinition(
                new object(),
                exactScenario.Authority,
                exactScenario.Authority.TypeDefinitions[1]));
        Assert.Throws<ArgumentException>(() =>
            MetadataGenericParameterAuthorityOwnerGroupIdentity.CreateForMethodDefinition(
                new object(),
                exactScenario.Authority,
                exactScenario.Authority.MethodDefinitions[0]));
        Assert.False(MetadataGenericParameterAuthorityCatalogIdentity.OwnsOwnerGroupMintCapability(new object()));
        Assert.Empty(typeof(MetadataGenericParameterAuthorityOwnerGroupIdentity)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataGenericParameterAuthorityOwnerGroupIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(MetadataGenericParameterAuthorityBindingIdentity).GetMethod(
            nameof(MetadataGenericParameterAuthorityBindingIdentity.Exact),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(MetadataGenericParameterAuthorityBindingIdentity).GetMethod(
            nameof(MetadataGenericParameterAuthorityBindingIdentity.Exact),
            BindingFlags.NonPublic | BindingFlags.Static));
        var publicBindingFactory = Assert.Single(typeof(MetadataGenericParameterAuthorityBindingIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(nameof(MetadataGenericParameterAuthorityBindingIdentity.Unavailable), publicBindingFactory.Name);

        var first = MetadataGenericParameterAuthorityBindingIdentity.Unavailable(pairGroup.Parameters[0]);
        var second = MetadataGenericParameterAuthorityBindingIdentity.Exact(
            pairGroup.Parameters[1],
            MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.String));
        var ledger = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(pairGroup, [second, first]);
        var ledgerReplay = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(
            replay.FindTypeDefinitionOwner(replay.DefinitionAuthority.TypeDefinitions[1])!,
            [
                MetadataGenericParameterAuthorityBindingIdentity.Exact(
                    replay.FindTypeDefinitionOwner(replay.DefinitionAuthority.TypeDefinitions[1])!.Parameters[1],
                    MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.String)),
                MetadataGenericParameterAuthorityBindingIdentity.Unavailable(
                    replay.FindTypeDefinitionOwner(replay.DefinitionAuthority.TypeDefinitions[1])!.Parameters[0]),
            ]);
        Assert.Equal(ledger, ledgerReplay);

        var boundGroup = exact.FindTypeDefinitionOwner(exactScenario.Authority.TypeDefinitions[3])!;
        var boundLedger = MetadataGenericParameterAuthorityBindingLedgerIdentity.Create(
            boundGroup,
            boundGroup.Parameters.Select(MetadataGenericParameterAuthorityBindingIdentity.Unavailable)
                .ToImmutableArray());

        Assert.Equal("7095529d9205c93c52d3f9b2762c9d042382bb1f7ac9ea432989816f4d1bf393", exact.Sha256);
        Assert.Equal("0053928cde770c3317075336d58fe2f1883c89304276f5e7a10c9762d72107a9", incomplete.Sha256);
        Assert.Equal("ea883e415f6536ac340e4984d8288ffa16dc21097c95b7f7d3d40e1b317b721d", invalid.Sha256);
        Assert.Equal("b39f1bfa13afa1a12fedb2f160bb2ef641f42f8180c5d6439b500040f5a96a5e", ledger.Sha256);
        Assert.Equal("e07ac71ae7681dcc35c97a25a88a2a44fd7cb356e1ebe39b603de43062ca524f", boundLedger.Sha256);
    }

    /// <summary>
    /// Records the remaining public construction consumer that requires the legacy ledger until its parameter and
    /// implementation can move together to the authority-owned draft family.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_substitution_request_exposes_the_remaining_legacy_ledger_migration_boundary()
    {
        var factory = Assert.Single(typeof(MetadataTypeSubstitutionRequest)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
            static method => method.Name == nameof(MetadataTypeSubstitutionRequest.ForFieldDefinition));
        Assert.Equal(typeof(MetadataGenericParameterBindingLedgerIdentity), factory.GetParameters()[1].ParameterType);
        Assert.Equal(
            typeof(MetadataGenericParameterBindingLedgerIdentity),
            typeof(MetadataTypeSubstitutionRequest)
                .GetProperty(nameof(MetadataTypeSubstitutionRequest.DeclaringTypeBindings))!.PropertyType);
        Assert.NotEqual(
            typeof(MetadataGenericParameterAuthorityBindingLedgerIdentity),
            factory.GetParameters()[1].ParameterType);
        Assert.Contains(
            typeof(MetadataGenericParameterOwnerDeclarationIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
            static method => method.Name == nameof(MetadataGenericParameterOwnerDeclarationIdentity.FromTypeDefinition));
    }

    /// <summary>Proves the new sealed draft surface documents public static and instance methods and has no source-less factories.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Generic_parameter_proof_public_surface_is_sealed_and_draft_documented()
    {
        var publicTypes = new[]
        {
            typeof(MetadataGenericParameterProofResultKind),
            typeof(MetadataGenericParameterProofIssue),
            typeof(MetadataGenericMethodDeclarationCertificateIdentity),
            typeof(MetadataGenericParameterOwnerDeclarationIdentity),
            typeof(MetadataGenericParameterTableCatalogIdentity),
            typeof(MetadataGenericParameterOwnerSetIdentity),
            typeof(MetadataGenericParameterBindingLedgerIdentity),
            typeof(MetadataGenericParameterOwnerAdmissionKind),
            typeof(MetadataGenericParameterAuthorityOwnerGroupIdentity),
            typeof(MetadataGenericParameterAuthorityCatalogIdentity),
            typeof(MetadataGenericParameterAuthorityBindingKind),
            typeof(MetadataGenericParameterAuthorityBindingIdentity),
            typeof(MetadataGenericParameterAuthorityBindingLedgerIdentity),
        };
        Assert.All(
            publicTypes.Where(static type => type.IsClass),
            static type => Assert.True(type.IsSealed));

        var declarationFactories = typeof(MetadataGenericParameterOwnerDeclarationIdentity)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(2, declarationFactories.Length);
        Assert.All(declarationFactories, static method =>
            Assert.Equal(typeof(MetadataSourceEndIdentity), method.GetParameters()[0].ParameterType));

        var assembly = typeof(MetadataGenericParameterOwnerDeclarationIdentity).Assembly;
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

    private static void AssertAuthorityLedgerStop(
        MetadataGenericParameterAuthorityBindingLedgerIdentity ledger,
        MetadataGenericParameterProofResultKind resultKind,
        MetadataGenericParameterProofIssue issue)
    {
        Assert.Equal(resultKind, ledger.ResultKind);
        Assert.Equal(issue, ledger.Issue);
        Assert.Empty(ledger.Bindings);
        Assert.Null(ledger.FindBinding(0));
    }

    private static AuthorityScenario BuildAuthorityScenario(
        StaticFieldMetadataModuleIdentity? module = null,
        bool omitGenericParameterObservations = false,
        string moduleTypeName = "<Module>",
        string pairLeftName = "PairLeft")
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
                methodDefinitionRowCount: 2,
                nestedClassRowCount: 0,
                genericParameterRowCount: genericObservations.Length));
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [
                AuthorityTypeRow(module, 1, methodListRowId: 0, string.Empty, moduleTypeName,
                    TypeAttributes.NotPublic),
                AuthorityTypeRow(module, 2, methodListRowId: 1, "Synthetic.Authority", "Pair`2",
                    TypeAttributes.Public),
                AuthorityTypeRow(module, 3, methodListRowId: 2, "Synthetic.Authority", "AtLimit`64",
                    TypeAttributes.Public),
                AuthorityTypeRow(module, 4, methodListRowId: 3, "Synthetic.Authority", "BeyondLimit`65",
                    TypeAttributes.Public),
            ]);
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
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions);
        return new AuthorityScenario(sourceEnds, genericParameters, authority);
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
            name: $"AuthorityGenericMethod{rowId}",
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
                genericParameterToken: 0x2A000000 | checked(rows.Count + 1),
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

    private static int TypeToken(int rowId) => 0x02000000 | rowId;

    private static int MethodToken(int rowId) => 0x06000000 | rowId;

    private static void AssertPrefixFreeOwnerCap(MetadataGenericParameterOwnerDeclarationIdentity declaration)
    {
        Assert.Equal(MetadataGenericParameterProofResultKind.NonExact, declaration.ResultKind);
        Assert.Equal(MetadataGenericParameterProofIssue.OwnerArityBoundReached, declaration.Issue);
        Assert.Null(declaration.DeclaredArity);
        Assert.Null(declaration.MethodSignatureCertificate);
        Assert.Equal(ExpressionV2ContractLimits.GenericParameterCountBoundName, declaration.ReachedBound!.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumGenericParameterCount + 1, declaration.ObservedCount);
    }

    private static MetadataGenericParameterIdentity Parameter(
        int rowId,
        MetadataGenericParameterOwnerIdentity owner,
        int position,
        string name) =>
        MetadataGenericParameterIdentity.Create(
            0x2A000000 | rowId,
            owner,
            position,
            name,
            attributes: 0);

    private static ImmutableArray<byte> GenericInstanceMethodSignature() =>
        [0x30, 0x03, 0x02, 0x1E, 0x02, 0x1E, 0x00, 0x1D, 0x1E, 0x01];

    private static ImmutableArray<byte> GenericStaticMethodSignature(int arity)
    {
        Assert.InRange(arity, 1, 0x7F);
        return [0x10, (byte)arity, 0x00, 0x01];
    }

    private sealed class SyntheticFixture
    {
        internal SyntheticFixture()
        {
            Module = CreateMetadataModule();
            GenericHost = StaticFieldTypeDefinitionIdentity.Create(
                Module,
                0x02000001,
                fieldListRowId: 1,
                fieldListEndExclusiveRowId: 1,
                methodListRowId: 1,
                methodListEndExclusiveRowId: 7,
                "Synthetic",
                "GenericHost`2",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                genericParameterCount: 2,
                introducedGenericArity: 2,
                extendsMetadataToken: null);
            GenericHostRaw = MetadataRawTypeDefinitionIdentity.FromPinnedW7(GenericHost);
            ZeroArityRaw = CreateRawType(rowId: 2, arity: 0);
            GenericMethod = CreateMethod(
                rowId: 1,
                attributes: (int)MethodAttributes.Public,
                signature: GenericInstanceMethodSignature());
        }

        internal StaticFieldMetadataModuleIdentity Module { get; }
        internal StaticFieldTypeDefinitionIdentity GenericHost { get; }
        internal MetadataRawTypeDefinitionIdentity GenericHostRaw { get; }
        internal MetadataRawTypeDefinitionIdentity ZeroArityRaw { get; }
        internal StaticFieldMethodDefinitionIdentity GenericMethod { get; }

        internal StaticFieldMethodDefinitionIdentity CreateMethod(
            int rowId,
            int attributes,
            ImmutableArray<byte> signature) =>
            StaticFieldMethodDefinitionIdentity.Create(
                GenericHost,
                0x06000000 | rowId,
                relativeVirtualAddress: 0,
                implementationAttributes: 0,
                attributes,
                $"Transform{rowId}",
                signature,
                parameterListRowId: 1,
                parameterListEndExclusiveRowId: 1,
                parameterDefinitionRowCount: 0);

        internal MetadataRawTypeDefinitionIdentity CreateRawType(int rowId, int arity) =>
            MetadataRawTypeDefinitionIdentity.Create(
                Module,
                0x02000000 | rowId,
                fieldListRowId: 1,
                fieldListEndExclusiveRowId: 1,
                methodListRowId: 1,
                methodListEndExclusiveRowId: 1,
                "Synthetic",
                $"RawOwner{rowId}",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                arity,
                extendsMetadataToken: null);

        internal ImmutableArray<MetadataGenericParameterIdentity> CreateMeaningfulRows()
        {
            var typeOwner = MetadataGenericParameterOwnerIdentity.ForTypeDefinition(GenericHostRaw);
            var methodOwner = MetadataGenericParameterOwnerIdentity.ForMethodDefinition(GenericMethod);
            return
            [
                Parameter(1, typeOwner, 1, "TRight"),
                Parameter(2, typeOwner, 0, "TLeft"),
                Parameter(3, methodOwner, 2, "TResult"),
                Parameter(4, methodOwner, 0, "TInput"),
                Parameter(5, methodOwner, 1, "TState"),
            ];
        }

        internal MetadataSourceEndIdentity SourceEnds(
            int genericParameterRows,
            int typeDefinitionRows = 16,
            int methodDefinitionRows = 16) =>
            CreateSourceEnds(Module, genericParameterRows, typeDefinitionRows, methodDefinitionRows);

        internal MetadataSourceEndIdentity OtherSourceEnds() =>
            CreateSourceEnds(CreateMetadataModule(0x3000, 'b'), genericParameterRows: 5, 16, 16);

        internal MetadataRawTypeDefinitionIdentity OtherRawType()
        {
            var module = CreateMetadataModule(0x3000, 'b');
            return MetadataRawTypeDefinitionIdentity.Create(
                module,
                0x02000001,
                1,
                1,
                1,
                1,
                "Foreign",
                "Owner",
                (int)(TypeAttributes.Public | TypeAttributes.Class),
                genericParameterCount: 1,
                extendsMetadataToken: null);
        }
    }

    private sealed record AuthorityScenario(
        MetadataSourceEndIdentity SourceEnds,
        MetadataGenericParameterPhysicalTableCatalogIdentity GenericParameters,
        MetadataDefinitionAuthorityCatalogIdentity Authority);

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int genericParameterRows,
        int typeDefinitionRows,
        int methodDefinitionRows)
    {
        var fact = StaticFieldModuleSearchFact.Exact(
            module.Module,
            module.ModuleContent,
            typeDefinitionsExamined: typeDefinitionRows,
            fieldDefinitionsExamined: 0,
            typeDefinitionRowCount: typeDefinitionRows,
            fieldDefinitionRowCount: 0,
            methodDefinitionRowCount: methodDefinitionRows,
            genericParameterRowCount: genericParameterRows);
        return MetadataSourceEndIdentity.Create(module, fact);
    }

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
            $"generic-proof-{moduleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            "Synthetic.GenericProof",
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
