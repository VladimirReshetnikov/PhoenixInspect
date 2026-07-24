using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises authority-issued W8 GenericParam owner-group and binding draft proofs.</summary>
public sealed class W8MetadataGenericParameterProofContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

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
        Assert.Equal("f68f0be911f0c76286de45115d1ef7109174323585cae1e3f7189b619789af6b", ledger.Sha256);
        Assert.Equal("e07ac71ae7681dcc35c97a25a88a2a44fd7cb356e1ebe39b603de43062ca524f", boundLedger.Sha256);
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

        var assembly = typeof(MetadataGenericParameterAuthorityCatalogIdentity).Assembly;
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

    private sealed record AuthorityScenario(
        MetadataSourceEndIdentity SourceEnds,
        MetadataGenericParameterPhysicalTableCatalogIdentity GenericParameters,
        MetadataDefinitionAuthorityCatalogIdentity Authority);

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
