using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises normalized multi-module definition-compatibility draft portfolios.</summary>
public sealed class W8MetadataDefinitionCompatibilityPortfolioContractTests
{
    private const int OuterTypeDefinitionRid = 2;
    private const string PortfolioSnapshotDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string OtherSnapshotDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";

    /// <summary>
    /// Proves caller order is discarded, exact-module lookup works, one snapshot is retained, and compatible, absent,
    /// and mismatched row outcomes remain owned by their original draft catalogs.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_multi_module_portfolio_normalizes_order_without_reinterpreting_rows()
    {
        var supplied = BuildSmallCatalogSet();
        var replaySupplied = BuildSmallCatalogSet();
        var first = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [supplied.Mismatch, supplied.Compatible, supplied.Absent]);
        var replay = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [replaySupplied.Absent, replaySupplied.Mismatch, replaySupplied.Compatible]);

        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.Exact, first.ResultKind);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioIssue.None, first.Issue);
        Assert.Equal(supplied.Compatible.DefinitionAuthority.SourceEnds.SourceModule.Module.SnapshotSha256,
            first.SnapshotSha256);
        Assert.Equal(3, first.Entries.Length);
        Assert.Equal(first, replay);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));

        for (var index = 1; index < first.Entries.Length; index++)
        {
            Assert.True(first.Entries[index - 1].SourceModule.CanonicalBytes.AsSpan().SequenceCompareTo(
                first.Entries[index].SourceModule.CanonicalBytes.AsSpan()) < 0);
        }
        Assert.All(first.Entries, entry => Assert.Equal(first.SnapshotSha256, entry.SnapshotSha256));

        var compatibleEntry = Assert.IsType<MetadataDefinitionCompatibilityPortfolioEntryIdentity>(
            first.ExactEntryOrDefault(supplied.Compatible.DefinitionAuthority.SourceEnds.SourceModule));
        var absentEntry = Assert.IsType<MetadataDefinitionCompatibilityPortfolioEntryIdentity>(
            first.ExactEntryOrDefault(supplied.Absent.DefinitionAuthority.SourceEnds));
        var mismatchEntry = Assert.IsType<MetadataDefinitionCompatibilityPortfolioEntryIdentity>(
            first.ExactEntryOrDefault(supplied.Mismatch.DefinitionAuthority.SourceEnds.SourceModule));
        Assert.Same(supplied.Compatible, compatibleEntry.CompatibilityCatalog);
        Assert.Same(supplied.Absent, absentEntry.CompatibilityCatalog);
        Assert.Same(supplied.Mismatch, mismatchEntry.CompatibilityCatalog);
        Assert.Equal(
            MetadataW7TypeDefinitionCompatibilityResultKind.Compatible,
            Certificate(compatibleEntry.CompatibilityCatalog, OuterTypeDefinitionRid).ResultKind);
        Assert.Equal(
            MetadataW7TypeDefinitionCompatibilityResultKind.CandidateAbsent,
            Certificate(absentEntry.CompatibilityCatalog, OuterTypeDefinitionRid).ResultKind);
        Assert.Equal(
            MetadataW7TypeDefinitionCompatibilityResultKind.Mismatch,
            Certificate(mismatchEntry.CompatibilityCatalog, OuterTypeDefinitionRid).ResultKind);
        Assert.Equal(
            MetadataW7TypeDefinitionCompatibilityIssue.TypeNameMismatch,
            Certificate(mismatchEntry.CompatibilityCatalog, OuterTypeDefinitionRid).Issue);

        var replayCompatibleEntry = Assert.IsType<MetadataDefinitionCompatibilityPortfolioEntryIdentity>(
            first.ExactEntryOrDefault(replaySupplied.Compatible.DefinitionAuthority.SourceEnds));
        Assert.Equal(compatibleEntry, replayCompatibleEntry);
        Assert.Null(first.ExactEntryOrDefault(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0xD000, 'd')));
    }

    /// <summary>
    /// Proves default and explicit-empty vectors remain distinct, exact-at-cap input stays complete, and cap-plus-one
    /// stops before normalization while retaining only the shared draft bound.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Initialization_and_module_count_bound_have_exact_boundary_outcomes()
    {
        var missing = MetadataDefinitionCompatibilityPortfolioIdentity.Create(default);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.NonExact, missing.ResultKind);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioIssue.CatalogVectorUninitialized, missing.Issue);
        Assert.Empty(missing.Entries);
        Assert.Null(missing.SnapshotSha256);
        Assert.Null(missing.ReachedBound);
        Assert.Equal(0, missing.ObservedCount);

        var empty = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.Exact, empty.ResultKind);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioIssue.None, empty.Issue);
        Assert.Empty(empty.Entries);
        Assert.Null(empty.SnapshotSha256);
        Assert.NotEqual(missing.Sha256, empty.Sha256);

        var catalogs = ImmutableArray.CreateBuilder<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>(
            MetadataDefinitionCompatibilityPortfolioIdentity.MaximumModuleCount);
        for (var index = 0;
             index < MetadataDefinitionCompatibilityPortfolioIdentity.MaximumModuleCount;
             index++)
        {
            catalogs.Add(BuildMinimalCompatibilityCatalog(index, PortfolioSnapshotDigest));
        }
        var exactInput = catalogs.MoveToImmutable();
        var exact = MetadataDefinitionCompatibilityPortfolioIdentity.Create(exactInput);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.Exact, exact.ResultKind);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioIdentity.MaximumModuleCount, exact.Entries.Length);
        Assert.Equal(PortfolioSnapshotDigest, exact.SnapshotSha256);
        Assert.Null(exact.ReachedBound);
        Assert.True(exact.CanonicalBytes.Length <
                    exact.Entries.Length * 40 + 512);
        Assert.Single(exact.Entries.Select(entry => entry.CanonicalBytes.Length).Distinct());

        var overInput = exactInput.Add(exactInput[0]);
        var over = MetadataDefinitionCompatibilityPortfolioIdentity.Create(overInput);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.NonExact, over.ResultKind);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioIssue.ModuleCountBoundReached, over.Issue);
        Assert.Empty(over.Entries);
        Assert.Null(over.SnapshotSha256);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioIdentity.MaximumModuleCount + 1,
            over.ObservedCount);
        var bound = Assert.IsType<Interpreter.Core.Abstractions.EvaluationDeterministicBound>(over.ReachedBound);
        Assert.Equal("expression-v2.metadata.modules", bound.Name);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioIdentity.MaximumModuleCount, bound.Value);
        Assert.Null(over.RelatedModuleSha256);
        Assert.Null(over.RelatedCatalogSha256);
    }

    /// <summary>
    /// Proves missing, non-exact, invalid, mixed-snapshot, and duplicate-module inputs produce deterministic typed
    /// draft stops without an entry prefix, including prerequisite-bound propagation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_and_cross_module_stops_are_typed_deterministic_and_prefix_free()
    {
        var nullSlot = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!));
        AssertStop(
            nullSlot,
            MetadataDefinitionCompatibilityPortfolioResultKind.NonExact,
            MetadataDefinitionCompatibilityPortfolioIssue.CompatibilityCatalogMissing,
            1);

        var nonExactAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            omitGenericParameterRows: true).DefinitionAuthority;
        var nonExactCatalog = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            nonExactAuthority,
            default);
        var invalidAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: true,
            invalidModuleName: true).DefinitionAuthority;
        var invalidCatalog = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(invalidAuthority, default);
        var mixedPrerequisites = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [invalidCatalog, nonExactCatalog]);
        var replayMixedPrerequisites = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [nonExactCatalog, invalidCatalog]);
        AssertStop(
            mixedPrerequisites,
            MetadataDefinitionCompatibilityPortfolioResultKind.NonExact,
            MetadataDefinitionCompatibilityPortfolioIssue.CompatibilityCatalogNonExact,
            nonExactCatalog.ObservedCount);
        Assert.Equal(nonExactCatalog.ReachedBound, mixedPrerequisites.ReachedBound);
        Assert.Equal(nonExactCatalog.RelatedMetadataToken, mixedPrerequisites.RelatedMetadataToken);
        Assert.Equal(nonExactCatalog.Sha256, mixedPrerequisites.RelatedCatalogSha256);
        Assert.Equal(mixedPrerequisites, replayMixedPrerequisites);

        var invalidOnly = MetadataDefinitionCompatibilityPortfolioIdentity.Create([invalidCatalog]);
        AssertStop(
            invalidOnly,
            MetadataDefinitionCompatibilityPortfolioResultKind.Invalid,
            MetadataDefinitionCompatibilityPortfolioIssue.CompatibilityCatalogInvalid,
            invalidCatalog.ObservedCount);
        Assert.Equal(invalidCatalog.RelatedMetadataToken, invalidOnly.RelatedMetadataToken);
        Assert.Equal(invalidCatalog.Sha256, invalidOnly.RelatedCatalogSha256);

        var boundCatalog = BuildBoundBearingNonExactCompatibilityCatalog();
        var boundStop = MetadataDefinitionCompatibilityPortfolioIdentity.Create([boundCatalog]);
        AssertStop(
            boundStop,
            MetadataDefinitionCompatibilityPortfolioResultKind.NonExact,
            MetadataDefinitionCompatibilityPortfolioIssue.CompatibilityCatalogNonExact,
            boundCatalog.ObservedCount);
        Assert.NotNull(boundCatalog.ReachedBound);
        Assert.Equal(boundCatalog.ReachedBound, boundStop.ReachedBound);
        Assert.Equal(boundCatalog.ObservedCount, boundStop.ObservedCount);
        Assert.Equal(boundCatalog.RelatedMetadataToken, boundStop.RelatedMetadataToken);

        var firstSnapshot = BuildMinimalCompatibilityCatalog(5_000, PortfolioSnapshotDigest);
        var secondSnapshot = BuildMinimalCompatibilityCatalog(5_001, OtherSnapshotDigest);
        var mixedSnapshots = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [secondSnapshot, firstSnapshot]);
        AssertStop(
            mixedSnapshots,
            MetadataDefinitionCompatibilityPortfolioResultKind.Invalid,
            MetadataDefinitionCompatibilityPortfolioIssue.SnapshotMismatch,
            2);
        Assert.Null(mixedSnapshots.RelatedMetadataToken);

        var duplicate = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [firstSnapshot, firstSnapshot]);
        AssertStop(
            duplicate,
            MetadataDefinitionCompatibilityPortfolioResultKind.Invalid,
            MetadataDefinitionCompatibilityPortfolioIssue.DuplicateSourceModule,
            2);
        Assert.Equal(firstSnapshot.Sha256, duplicate.RelatedCatalogSha256);
    }

    /// <summary>
    /// Proves immutable replay, fixed-size digest references, the draft golden digest, private entry issuance,
    /// reflection shape, and emitted XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Portfolio_is_immutable_fixed_reference_guarded_and_documented()
    {
        var catalogs = BuildSmallCatalogSet();
        var portfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [catalogs.Mismatch, catalogs.Compatible, catalogs.Absent]);
        var replayCatalogs = BuildSmallCatalogSet();
        var replay = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [replayCatalogs.Compatible, replayCatalogs.Absent, replayCatalogs.Mismatch]);
        var originalEntries = portfolio.Entries;
        var originalBytes = portfolio.CanonicalBytes;
        var originalSha256 = portfolio.Sha256;
        var firstEntryBytes = originalEntries[0].CanonicalBytes;

        var returnedEntries = portfolio.Entries;
        ImmutableCollectionsMarshal.AsArray(returnedEntries)![0] = returnedEntries[^1];
        var returnedPortfolioBytes = portfolio.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedPortfolioBytes)![0] ^= 0x5A;
        var returnedEntryBytes = originalEntries[0].CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedEntryBytes)![0] ^= 0x3C;

        Assert.Equal(portfolio, replay);
        Assert.Equal(portfolio.GetHashCode(), replay.GetHashCode());
        Assert.Equal(portfolio.Sha256, replay.Sha256);
        Assert.True(portfolio.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        Assert.True(originalBytes.AsSpan().SequenceEqual(portfolio.CanonicalBytes.AsSpan()));
        Assert.True(firstEntryBytes.AsSpan().SequenceEqual(originalEntries[0].CanonicalBytes.AsSpan()));
        Assert.Equal(originalEntries[0].Sha256, portfolio.Entries[0].Sha256);
        Assert.Equal(originalSha256, portfolio.Sha256);
        Assert.Equal("1dbba26e7b0a005c791c72993a8472c461e1bd4436d6e7ba58d27a476e5696c6", originalSha256);
        Assert.Equal("2bbc13b637416a4f6135b12487a97228d4bb0d4e23dd3523ca6871123889e41b",
            originalEntries[0].Sha256);

        Assert.Single(portfolio.Entries.Select(entry => entry.CanonicalBytes.Length).Distinct());
        var empty = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty);
        Assert.Equal((portfolio.Entries.Length + 1) * 32,
            portfolio.CanonicalBytes.Length - empty.CanonicalBytes.Length);
        foreach (var entry in portfolio.Entries)
        {
            Assert.Equal(-1, entry.CanonicalBytes.AsSpan().IndexOf(entry.SourceModule.CanonicalBytes.AsSpan()));
            Assert.Equal(-1, entry.CanonicalBytes.AsSpan().IndexOf(entry.DefinitionAuthority.CanonicalBytes.AsSpan()));
            Assert.Equal(-1,
                entry.CanonicalBytes.AsSpan().IndexOf(entry.CompatibilityCatalog.CanonicalBytes.AsSpan()));
        }

        Assert.Throws<ArgumentException>(() =>
            MetadataDefinitionCompatibilityPortfolioEntryIdentity.Create(new object(), catalogs.Compatible));
        Assert.False(MetadataDefinitionCompatibilityPortfolioIdentity.OwnsEntryMintCapability(new object()));
        Assert.Empty(typeof(MetadataDefinitionCompatibilityPortfolioEntryIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataDefinitionCompatibilityPortfolioIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataDefinitionCompatibilityPortfolioEntryIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(
            ["Create"],
            typeof(MetadataDefinitionCompatibilityPortfolioIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToArray());

        AssertPublicDraftXml(
            typeof(MetadataDefinitionCompatibilityPortfolioResultKind),
            typeof(MetadataDefinitionCompatibilityPortfolioIssue),
            typeof(MetadataDefinitionCompatibilityPortfolioEntryIdentity),
            typeof(MetadataDefinitionCompatibilityPortfolioIdentity));
    }

    private static SmallCatalogSet BuildSmallCatalogSet()
    {
        var compatibleAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule(0xA000, 'a')).DefinitionAuthority;
        var absentAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule(0xB000, 'b')).DefinitionAuthority;
        var mismatchAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule(0xC000, 'c')).DefinitionAuthority;
        var compatibleCandidate = Candidate(compatibleAuthority.TypeDefinitions[OuterTypeDefinitionRid - 1]);
        var mismatchedCandidate = Candidate(
            mismatchAuthority.TypeDefinitions[OuterTypeDefinitionRid - 1],
            typeName: "ChangedOuter`2");
        return new SmallCatalogSet(
            MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
                compatibleAuthority,
                CandidateSlots(compatibleAuthority, (OuterTypeDefinitionRid, compatibleCandidate))),
            MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
                absentAuthority,
                CandidateSlots(absentAuthority)),
            MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
                mismatchAuthority,
                CandidateSlots(mismatchAuthority, (OuterTypeDefinitionRid, mismatchedCandidate))));
    }

    private static MetadataW7TypeDefinitionCompatibilityCatalogIdentity BuildMinimalCompatibilityCatalog(
        int index,
        string snapshotSha256)
    {
        var module = CreateMinimalMetadataModule(index, snapshotSha256);
        var sourceEnds = CreateSourceEnds(module, typeDefinitionRowCount: 1);
        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                TypeToken(1),
                fieldListRowId: 1,
                methodListRowId: 1,
                namespaceName: string.Empty,
                typeName: "<Module>",
                typeAttributes: (int)(TypeAttributes.NotPublic | TypeAttributes.Class),
                extendsMetadataToken: null)],
            pointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            ImmutableArray<StaticFieldTypeDefinitionIdentity?>.Empty.Add(null));
        Assert.True(
            compatibility.ResultKind == MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact,
            $"Compatibility={compatibility.ResultKind}/{compatibility.Issue}; " +
            $"Authority={authority.ResultKind}/{authority.Issue}; " +
            $"TypeDefs={typeDefinitions.ResultKind}/{typeDefinitions.Issue}; " +
            $"Pointers={pointers.ResultKind}/{pointers.Issue}; " +
            $"Nested={nestedClasses.ResultKind}/{nestedClasses.Issue}; " +
            $"Generics={genericParameters.ResultKind}/{genericParameters.Issue}; " +
            $"Methods={methods.ResultKind}/{methods.Issue}.");
        return compatibility;
    }

    private static MetadataW7TypeDefinitionCompatibilityCatalogIdentity
        BuildBoundBearingNonExactCompatibilityCatalog()
    {
        var module = CreateMinimalMetadataModule(6_000, PortfolioSnapshotDigest);
        var sourceEnds = CreateSourceEnds(
            module,
            StaticFieldV2Limits.MaximumTypeDefinitionRowCount + 1);
        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(sourceEnds, default, pointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            default);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(authority, default);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact, compatibility.ResultKind);
        Assert.NotNull(compatibility.ReachedBound);
        return compatibility;
    }

    private static StaticFieldMetadataModuleIdentity CreateMinimalMetadataModule(
        int index,
        string snapshotSha256)
    {
        var moduleAddress = checked(0x10_0000UL + (ulong)index * 0x100UL);
        var module = StaticFieldModuleInstanceIdentity.Create(
            snapshotSha256,
            sizeof(ulong),
            applicationDomainAddress: 0x1000,
            moduleAddress: moduleAddress,
            imageBase: checked(0x4000_0000UL + (ulong)index * 0x2_0000UL),
            imageSize: 0x1_0000);
        var content = ModuleContentIdentity.FromDigest(
            mvid: new Guid(index + 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            metadataLength: 4_096,
            metadataSha256: (index + 1).ToString("x64", CultureInfo.InvariantCulture));
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            generation: 0,
            name: $"portfolio-{index:x}.dll",
            mvid: content.Mvid,
            encId: Guid.Empty,
            encBaseId: Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            name: "Synthetic.Portfolio",
            majorVersion: 1,
            minorVersion: 0,
            buildNumber: 0,
            revisionNumber: 0,
            culture: string.Empty,
            flags: 0,
            hashAlgorithm: 0x8004,
            publicKey: ImmutableArray<byte>.Empty);
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

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRowCount) =>
        MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: typeDefinitionRowCount,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: typeDefinitionRowCount,
                fieldDefinitionRowCount: 0,
                typeReferenceRowCount: 0,
                typeSpecificationRowCount: 0,
                assemblyReferenceRowCount: 0,
                methodDefinitionRowCount: 0,
                parameterDefinitionRowCount: 0,
                propertyDefinitionRowCount: 0,
                eventDefinitionRowCount: 0,
                moduleDefinitionRowCount: 1,
                assemblyDefinitionRowCount: 1,
                interfaceImplementationRowCount: 0,
                memberReferenceRowCount: 0,
                customAttributeRowCount: 0,
                moduleReferenceRowCount: 0,
                fileRowCount: 0,
                exportedTypeRowCount: 0,
                nestedClassRowCount: 0,
                genericParameterRowCount: 0,
                genericParameterConstraintRowCount: 0,
                fieldPointerRowCount: 0,
                methodPointerRowCount: 0,
                parameterPointerRowCount: 0));

    private static StaticFieldTypeDefinitionIdentity Candidate(
        MetadataTypeDefinitionAuthorityIdentity authority,
        string? typeName = null)
    {
        var row = authority.TableRow;
        return StaticFieldTypeDefinitionIdentity.Create(
            authority.SourceEnds.SourceModule,
            authority.TypeDefinitionToken,
            row.Observation.FieldListRowId,
            row.FieldListEndExclusiveRowId,
            row.Observation.MethodListRowId,
            row.MethodListEndExclusiveRowId,
            authority.NamespaceName,
            typeName ?? authority.TypeName,
            row.Observation.TypeAttributes,
            authority.TotalGenericArity,
            authority.TotalGenericArity,
            row.Observation.ExtendsMetadataToken,
            enclosingType: null);
    }

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity?> CandidateSlots(
        MetadataDefinitionAuthorityCatalogIdentity authority,
        params (int TypeDefinitionRid, StaticFieldTypeDefinitionIdentity Candidate)[] supplied)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity?>(
            authority.TypeDefinitions.Length);
        for (var index = 0; index < authority.TypeDefinitions.Length; index++)
        {
            builder.Add(null);
        }
        foreach (var (typeDefinitionRid, candidate) in supplied)
        {
            builder[typeDefinitionRid - 1] = candidate;
        }
        return builder.MoveToImmutable();
    }

    private static MetadataW7TypeDefinitionCompatibilityCertificateIdentity Certificate(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity catalog,
        int typeDefinitionRid) =>
        Assert.IsType<MetadataW7TypeDefinitionCompatibilityCertificateIdentity>(
            catalog.CompleteCertificateOrDefault(TypeToken(typeDefinitionRid)));

    private static void AssertStop(
        MetadataDefinitionCompatibilityPortfolioIdentity result,
        MetadataDefinitionCompatibilityPortfolioResultKind resultKind,
        MetadataDefinitionCompatibilityPortfolioIssue issue,
        int observedCount)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Entries);
        Assert.Null(result.SnapshotSha256);
        Assert.Equal(observedCount, result.ObservedCount);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataDefinitionCompatibilityPortfolioIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.Contains("draft", typeDocumentation.Value, StringComparison.OrdinalIgnoreCase);
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(method => !method.IsSpecialName))
            {
                var prefix = $"M:{type.FullName}.{method.Name}";
                var methodDocumentation = members.Where(member =>
                    ((string?)member.Attribute("name")) is { } name &&
                    (string.Equals(name, prefix, StringComparison.Ordinal) ||
                     name.StartsWith($"{prefix}(", StringComparison.Ordinal))).ToArray();
                Assert.NotEmpty(methodDocumentation);
                Assert.All(methodDocumentation, member =>
                    Assert.Contains("draft", member.Value, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static int TypeToken(int rowId) => 0x0200_0000 | rowId;

    private sealed record SmallCatalogSet(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatible,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Absent,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Mismatch);
}
