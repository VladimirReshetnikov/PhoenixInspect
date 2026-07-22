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
