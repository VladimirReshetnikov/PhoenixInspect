using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the multi-module authority-derived TypeRef resolution portfolio.</summary>
public sealed class W8MetadataTypeReferenceResolutionContractTests
{
    private const int OuterTypeRid = 2;
    private const int EqualTypeRid = 3;
    private const int KeywordTypeRid = 10;
    private const int PlainGenericTypeRid = 12;

    /// <summary>
    /// Proves same-module, cross-assembly, nested-through-reference, self-referencing, and forwarder-hopping TypeRef
    /// rows resolve to exact authority-issued TypeDefs while netmodule scopes, absent modules, absent targets,
    /// unresolved parents, and File-implemented forwarders retain complete typed row dispositions.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_portfolio_resolves_typerefs_across_modules_and_forwarders()
    {
        var world = BuildPrimaryWorld();
        var replayWorld = BuildPrimaryWorld();
        var portfolio = world.Resolution;

        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, portfolio.ResultKind);
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioIssue.None, portfolio.Issue);
        Assert.Equal(3, portfolio.Entries.Length);
        Assert.Equal(world.ChainPortfolio.SnapshotSha256, portfolio.SnapshotSha256);
        Assert.Equal(
            world.ChainPortfolio.Entries.Select(static entry => entry.SourceModule),
            portfolio.Entries.Select(static entry => entry.SourceModule));

        var app = world.App.Module;
        var lib = world.Lib.Module;
        var libAuthority = world.ChainPortfolio.Entries
            .Single(entry => entry.SourceModule.Equals(lib))
            .ChainCatalog.DefinitionAuthority;
        var appAuthority = world.ChainPortfolio.Entries
            .Single(entry => entry.SourceModule.Equals(app))
            .ChainCatalog.DefinitionAuthority;

        var sameModule = Resolution(portfolio, app, 1);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.Resolved, sameModule.Disposition);
        Assert.Equal(app, sameModule.TargetModule);
        Assert.Same(appAuthority.TypeDefinitions[OuterTypeRid - 1], sameModule.TargetTypeDefinition);
        Assert.Equal(0, sameModule.TraversalStepCount);
        Assert.Empty(sameModule.TraversedForwarders);

        var crossAssembly = Resolution(portfolio, app, 2);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.Resolved, crossAssembly.Disposition);
        Assert.Equal(lib, crossAssembly.TargetModule);
        Assert.Same(libAuthority.TypeDefinitions[OuterTypeRid - 1], crossAssembly.TargetTypeDefinition);

        var nestedThroughReference = Resolution(portfolio, app, 3);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.Resolved, nestedThroughReference.Disposition);
        Assert.Equal(lib, nestedThroughReference.TargetModule);
        Assert.Same(
            libAuthority.TypeDefinitions[EqualTypeRid - 1],
            nestedThroughReference.TargetTypeDefinition);
        Assert.Equal(1, nestedThroughReference.TraversalStepCount);

        var forwarded = Resolution(portfolio, app, 4);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.Resolved, forwarded.Disposition);
        Assert.Equal(lib, forwarded.TargetModule);
        Assert.Same(
            libAuthority.TypeDefinitions[PlainGenericTypeRid - 1],
            forwarded.TargetTypeDefinition);
        Assert.Equal(1, forwarded.TraversalStepCount);
        var traversedForwarder = Assert.Single(forwarded.TraversedForwarders);
        Assert.Equal("PlainGeneric", traversedForwarder.Observation.TypeName);
        Assert.Equal(world.Facade.Module, traversedForwarder.SourceEnds.SourceModule);

        var netmoduleScope = Resolution(portfolio, app, 5);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.ModuleReferenceScopeUnsupported,
            netmoduleScope.Disposition);
        Assert.Null(netmoduleScope.TargetModule);
        Assert.Null(netmoduleScope.TargetTypeDefinition);
        Assert.Equal(0x1A00_0001, netmoduleScope.RelatedMetadataToken);

        var absentTarget = Resolution(portfolio, app, 6);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.TargetTypeAbsent, absentTarget.Disposition);
        var absentModule = Resolution(portfolio, app, 7);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.TargetModuleAbsent, absentModule.Disposition);
        Assert.Equal(0x2300_0003, absentModule.RelatedMetadataToken);

        var unresolvedParent = Resolution(portfolio, app, 8);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.ParentReferenceUnresolved,
            unresolvedParent.Disposition);
        Assert.Equal(0x0100_0006, unresolvedParent.RelatedMetadataToken);

        var fileForwarded = Resolution(portfolio, app, 9);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.ForwarderImplementationUnsupported,
            fileForwarded.Disposition);
        Assert.Equal(0x2700_0002, fileForwarded.RelatedMetadataToken);

        var nestedSameModule = Resolution(portfolio, app, 10);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.Resolved, nestedSameModule.Disposition);
        Assert.Equal(app, nestedSameModule.TargetModule);
        Assert.Same(
            appAuthority.TypeDefinitions[EqualTypeRid - 1],
            nestedSameModule.TargetTypeDefinition);

        var selfAssembly = Resolution(portfolio, app, 11);
        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.Resolved, selfAssembly.Disposition);
        Assert.Equal(app, selfAssembly.TargetModule);
        Assert.Same(
            appAuthority.TypeDefinitions[KeywordTypeRid - 1],
            selfAssembly.TargetTypeDefinition);

        Assert.Null(portfolio.ExactResolutionOrDefault(app, 0x0100_000C));
        Assert.Null(portfolio.ExactResolutionOrDefault(app, 0x0200_0001));
        Assert.Null(portfolio.ExactResolutionOrDefault(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0xF100, 'f', "Synthetic.Alien"),
            0x0100_0001));

        Assert.Equal(portfolio, replayWorld.Resolution);
        Assert.Equal(portfolio.GetHashCode(), replayWorld.Resolution.GetHashCode());
        Assert.Equal(portfolio.Sha256, replayWorld.Resolution.Sha256);
        Assert.True(portfolio.CanonicalBytes.AsSpan().SequenceEqual(replayWorld.Resolution.CanonicalBytes.AsSpan()));
        Assert.Equal(
            "1c5a81290b885363e228533804639f9547d5625eeedc0f0d9e80dfd3507b0f54",
            portfolio.Sha256);
    }

    /// <summary>
    /// Proves duplicate assembly identities, duplicate forwarder names, reference cycles, and forwarder loops retain
    /// deterministic typed row dispositions with exact bound accounting instead of selecting a candidate.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ambiguity_cycle_and_depth_rows_never_select_a_candidate()
    {
        var consumer = BuildModule("Synthetic.App2", 0xA200, '5', assemblyReferences: module =>
            [
                AssemblyReferenceRow(module, 1, "Synthetic.Dup"),
                AssemblyReferenceRow(module, 2, "Synthetic.Fan"),
                AssemblyReferenceRow(module, 3, "Synthetic.Loop1"),
            ],
            typeReferences: module =>
            [
                TypeReferenceRow(module, 1, "Synthetic.Mapping", "Outer`2", 0x2300_0001),
                TypeReferenceRow(module, 2, "Synthetic.Mapping", "Doubled", 0x2300_0002),
                TypeReferenceRow(module, 3, "Forwarding", "Ping", 0x2300_0003),
            ]);
        var duplicateOne = BuildModule("Synthetic.Dup", 0xD100, '6');
        var duplicateTwo = BuildModule("Synthetic.Dup", 0xD200, '7');
        var fan = BuildModule("Synthetic.Fan", 0xF200, '8', assemblyReferences: module =>
            [AssemblyReferenceRow(module, 1, "Synthetic.Dup")],
            exportedTypes: module =>
            [
                ExportedTypeRow(module, 1, "Synthetic.Mapping", "Doubled", 0x2300_0001),
                ExportedTypeRow(module, 2, "Synthetic.Mapping", "Doubled", 0x2300_0001),
            ]);
        var gyre = BuildModule("Synthetic.Gyre", 0xF300, '9', typeReferences: module =>
            [
                TypeReferenceRow(module, 1, "Synthetic.Mapping", "First", 0x0100_0002),
                TypeReferenceRow(module, 2, "Synthetic.Mapping", "Second", 0x0100_0001),
            ]);
        var loopOne = BuildModule("Synthetic.Loop1", 0xF400, '0', assemblyReferences: module =>
            [AssemblyReferenceRow(module, 1, "Synthetic.Loop2")],
            exportedTypes: module =>
            [ExportedTypeRow(module, 1, "Forwarding", "Ping", 0x2300_0001)]);
        var loopTwo = BuildModule("Synthetic.Loop2", 0xF500, 'd', assemblyReferences: module =>
            [AssemblyReferenceRow(module, 1, "Synthetic.Loop1")],
            exportedTypes: module =>
            [ExportedTypeRow(module, 1, "Forwarding", "Ping", 0x2300_0001)]);

        var world = BuildWorld(consumer, duplicateOne, duplicateTwo, fan, gyre, loopOne, loopTwo);
        var portfolio = world.Resolution;
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, portfolio.ResultKind);

        var ambiguousModule = Resolution(portfolio, consumer.Module, 1);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.TargetModuleAmbiguous,
            ambiguousModule.Disposition);
        Assert.Equal(0x2300_0001, ambiguousModule.RelatedMetadataToken);

        var ambiguousForwarder = Resolution(portfolio, consumer.Module, 2);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.TargetTypeAmbiguous,
            ambiguousForwarder.Disposition);
        Assert.Equal(0x2700_0001, ambiguousForwarder.RelatedMetadataToken);

        var firstCycle = Resolution(portfolio, gyre.Module, 1);
        var secondCycle = Resolution(portfolio, gyre.Module, 2);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.ResolutionCycleDetected,
            firstCycle.Disposition);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.ResolutionCycleDetected,
            secondCycle.Disposition);

        var depthBound = Resolution(portfolio, consumer.Module, 3);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.ResolutionDepthBoundReached,
            depthBound.Disposition);
        var bound = Assert.IsType<EvaluationDeterministicBound>(depthBound.ReachedBound);
        Assert.Equal("expression-v2.metadata.typeref-depth", bound.Name);
        Assert.Equal(
            MetadataTypeReferenceResolutionPortfolioIdentity.MaximumResolutionDepth,
            bound.Value);
        Assert.Equal(
            MetadataTypeReferenceResolutionPortfolioIdentity.MaximumResolutionDepth + 1,
            depthBound.TraversalStepCount);
    }

    /// <summary>
    /// Proves prerequisite, vector-shape, member-table, module-correlation, and source-lineage contradictions produce
    /// deterministic prefix-free typed stops while default and explicit-empty inputs remain distinct.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_vector_and_lineage_stops_are_typed_and_prefix_free()
    {
        var world = BuildPrimaryWorld();
        var chainPortfolio = world.ChainPortfolio;
        var sets = new[] { world.App.Tables, world.Lib.Tables, world.Facade.Tables };

        var stoppedChain = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
            [world.App.ChainCatalog]);
        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(stoppedChain, [sets[0]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
            MetadataTypeReferenceResolutionPortfolioIssue.ChainPortfolioNonExact);

        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(chainPortfolio, default),
            MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
            MetadataTypeReferenceResolutionPortfolioIssue.TableSetVectorUninitialized);

        var overCap = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            chainPortfolio,
            [.. Enumerable.Repeat(
                sets[0],
                MetadataTypeReferenceResolutionPortfolioIdentity.MaximumModuleCount + 1)]);
        AssertStop(
            overCap,
            MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
            MetadataTypeReferenceResolutionPortfolioIssue.ModuleCountBoundReached);
        Assert.Equal("expression-v2.metadata.modules", overCap.ReachedBound!.Name);

        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(chainPortfolio, [sets[0], sets[1]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
            MetadataTypeReferenceResolutionPortfolioIssue.TableSetSlotsIncomplete);
        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                chainPortfolio,
                [sets[0], sets[1], sets[2], sets[2]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
            MetadataTypeReferenceResolutionPortfolioIssue.TableSetSlotCountConflict);
        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(chainPortfolio, [sets[0], null!, sets[2]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
            MetadataTypeReferenceResolutionPortfolioIssue.TableSetMissing);
        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                chainPortfolio,
                [sets[0], sets[0], sets[2]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
            MetadataTypeReferenceResolutionPortfolioIssue.DuplicateSourceModule);

        var incompleteTypeReferences = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            world.App.ReferenceEnds,
            default);
        var nonExactSet = MetadataModuleReferenceTableSetIdentity.Create(
            world.App.ReferenceEnds,
            incompleteTypeReferences,
            world.App.Tables.ModuleReferences,
            world.App.Tables.TypeSpecifications,
            world.App.Tables.AssemblyReferences,
            world.App.Tables.Files,
            world.App.Tables.ExportedTypes);
        var nonExactStop = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            chainPortfolio,
            [nonExactSet, sets[1], sets[2]]);
        AssertStop(
            nonExactStop,
            MetadataTypeReferenceResolutionPortfolioResultKind.NonExact,
            MetadataTypeReferenceResolutionPortfolioIssue.MemberTableNonExact);
        Assert.Equal(world.App.Module.Sha256, nonExactStop.RelatedModuleSha256);

        var appModule = world.App.Module;
        var invalidTypeReferences = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            world.App.ReferenceEnds,
            [.. Enumerable.Range(1, 11).Select(rowId => TypeReferenceRow(
                appModule,
                rowId == 1 ? 2 : rowId == 2 ? 1 : rowId,
                "Synthetic.Mapping",
                "Misordered",
                0x0000_0001))]);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Invalid, invalidTypeReferences.ResultKind);
        var invalidSet = MetadataModuleReferenceTableSetIdentity.Create(
            world.App.ReferenceEnds,
            invalidTypeReferences,
            world.App.Tables.ModuleReferences,
            world.App.Tables.TypeSpecifications,
            world.App.Tables.AssemblyReferences,
            world.App.Tables.Files,
            world.App.Tables.ExportedTypes);
        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                chainPortfolio,
                [invalidSet, sets[1], sets[2]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
            MetadataTypeReferenceResolutionPortfolioIssue.MemberTableInvalid);

        var stranger = BuildModule("Synthetic.Stranger", 0xF600, '4');
        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                chainPortfolio,
                [stranger.Tables, sets[1], sets[2]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
            MetadataTypeReferenceResolutionPortfolioIssue.SourceModuleNotInPortfolio);

        var relineagedScenario = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: world.App.Module,
            moduleReferenceRowCount: 1);
        var relineagedEnds = MetadataReferenceSourceEndIdentity.Create(relineagedScenario.SourceEnds);
        var relineagedSet = MetadataModuleReferenceTableSetIdentity.Create(
            relineagedEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
                relineagedEnds,
                ImmutableArray<MetadataTypeReferenceRowObservationIdentity>.Empty),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                relineagedEnds,
                [ModuleReferenceRow(world.App.Module, 1)]),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                relineagedEnds,
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(
                relineagedEnds,
                ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>.Empty),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                relineagedEnds,
                ImmutableArray<MetadataAssemblyFileRowObservationIdentity>.Empty),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                relineagedEnds,
                ImmutableArray<MetadataExportedTypeRowObservationIdentity>.Empty));
        AssertStop(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                chainPortfolio,
                [relineagedSet, sets[1], sets[2]]),
            MetadataTypeReferenceResolutionPortfolioResultKind.Invalid,
            MetadataTypeReferenceResolutionPortfolioIssue.ReferenceSourceEndsMismatch);

        var emptyChainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty),
            ImmutableArray<MetadataNamedTypeDefinitionChainCatalogIdentity>.Empty);
        var emptyPortfolio = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            emptyChainPortfolio,
            ImmutableArray<MetadataModuleReferenceTableSetIdentity>.Empty);
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, emptyPortfolio.ResultKind);
        Assert.Empty(emptyPortfolio.Entries);
        Assert.Null(emptyPortfolio.SnapshotSha256);
    }

    /// <summary>
    /// Proves defensive copies, private guarded row and entry issuance, the closed public issuer surface, and
    /// emitted XML documentation for the resolution contract family.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Resolution_contracts_are_immutable_guarded_and_documented()
    {
        var world = BuildPrimaryWorld();
        var portfolio = world.Resolution;
        var originalBytes = portfolio.CanonicalBytes;
        var originalSha = portfolio.Sha256;
        var forwarded = Resolution(portfolio, world.App.Module, 4);
        var originalForwarders = forwarded.TraversedForwarders;
        var originalFirstEntry = portfolio.Entries[0];

        var returnedEntries = portfolio.Entries;
        ImmutableCollectionsMarshal.AsArray(returnedEntries)![0] = returnedEntries[^1];
        var returnedBytes = portfolio.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        var returnedForwarders = forwarded.TraversedForwarders;
        ImmutableCollectionsMarshal.AsArray(returnedForwarders)![0] = null!;

        Assert.True(originalBytes.AsSpan().SequenceEqual(portfolio.CanonicalBytes.AsSpan()));
        Assert.Equal(originalSha, portfolio.Sha256);
        Assert.Equal(originalFirstEntry, portfolio.Entries[0]);
        Assert.Equal(originalForwarders[0], forwarded.TraversedForwarders[0]);

        var appEntry = portfolio.Entries.Single(entry => entry.SourceModule.Equals(world.App.Module));
        Assert.Throws<ArgumentException>(() => MetadataTypeReferenceResolutionIdentity.Create(
            new object(),
            world.App.Tables.TypeReferences.Rows[0],
            MetadataTypeReferenceResolutionDispositionKind.TargetTypeAbsent,
            null,
            null,
            [],
            0,
            null,
            null));
        Assert.Throws<ArgumentException>(() => MetadataTypeReferenceResolutionModuleIdentity.Create(
            new object(),
            appEntry.ChainEntry,
            world.App.Tables,
            appEntry.Resolutions));
        Assert.False(MetadataTypeReferenceResolutionPortfolioIdentity.OwnsRowMintCapability(new object()));

        var publicTypes = new[]
        {
            typeof(MetadataModuleReferenceTableSetIdentity),
            typeof(MetadataTypeReferenceResolutionDispositionKind),
            typeof(MetadataTypeReferenceResolutionIdentity),
            typeof(MetadataTypeReferenceResolutionModuleIdentity),
            typeof(MetadataTypeReferenceResolutionPortfolioResultKind),
            typeof(MetadataTypeReferenceResolutionPortfolioIssue),
            typeof(MetadataTypeReferenceResolutionPortfolioIdentity),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .ToArray();
            if (type == typeof(MetadataModuleReferenceTableSetIdentity) ||
                type == typeof(MetadataTypeReferenceResolutionPortfolioIdentity))
            {
                Assert.Equal(["Create"], publicStatics);
            }
            else if (!type.IsEnum)
            {
                Assert.Empty(publicStatics);
            }
        }
        AssertPublicDraftXml(publicTypes);
    }

    private static PrimaryWorld BuildPrimaryWorld()
    {
        var app = BuildModule("Synthetic.App", 0xA100, '1',
            typeReferences: module =>
            [
                TypeReferenceRow(module, 1, "Synthetic.Mapping", "Outer`2", 0x0000_0001),
                TypeReferenceRow(module, 2, "Synthetic.Mapping", "Outer`2", 0x2300_0001),
                TypeReferenceRow(module, 3, string.Empty, "Equal", 0x0100_0002),
                TypeReferenceRow(module, 4, "Synthetic.Mapping", "PlainGeneric", 0x2300_0002),
                TypeReferenceRow(module, 5, "Interop", "Native", 0x1A00_0001),
                TypeReferenceRow(module, 6, "Synthetic.Mapping", "Missing", 0x2300_0001),
                TypeReferenceRow(module, 7, "Synthetic.Mapping", "Anywhere", 0x2300_0003),
                TypeReferenceRow(module, 8, string.Empty, "Child", 0x0100_0006),
                TypeReferenceRow(module, 9, "Synthetic.Mapping", "FileForwarded", 0x2300_0002),
                TypeReferenceRow(module, 10, string.Empty, "Equal", 0x0100_0001),
                TypeReferenceRow(module, 11, "Synthetic.Mapping", "class", 0x2300_0004),
            ],
            moduleReferences: module => [ModuleReferenceRow(module, 1)],
            assemblyReferences: module =>
            [
                AssemblyReferenceRow(module, 1, "Synthetic.Lib"),
                AssemblyReferenceRow(module, 2, "Synthetic.Facade"),
                AssemblyReferenceRow(module, 3, "Synthetic.Absent"),
                AssemblyReferenceRow(module, 4, "Synthetic.App"),
            ]);
        var lib = BuildModule("Synthetic.Lib", 0xB100, '2');
        var facade = BuildModule("Synthetic.Facade", 0xC100, '3',
            assemblyReferences: module => [AssemblyReferenceRow(module, 1, "Synthetic.Lib")],
            files: module => [FileRow(module, 1)],
            exportedTypes: module =>
            [
                ExportedTypeRow(module, 1, "Synthetic.Mapping", "PlainGeneric", 0x2300_0001),
                ExportedTypeRow(module, 2, "Synthetic.Mapping", "FileForwarded", 0x2600_0001),
            ],
            includeScenarioTypes: false);
        var world = BuildWorld(app, lib, facade);
        return new PrimaryWorld(app, lib, facade, world.ChainPortfolio, world.Resolution);
    }

    private static BuiltWorld BuildWorld(params ResolutionModule[] modules)
    {
        var compatibilityPortfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [.. modules.Select(static module => module.Compatibility)]);
        Assert.Equal(
            MetadataDefinitionCompatibilityPortfolioResultKind.Exact,
            compatibilityPortfolio.ResultKind);
        var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [.. modules.Select(static module => module.ChainCatalog)]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, chainPortfolio.ResultKind);
        var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            chainPortfolio,
            [.. modules.Select(static module => module.Tables)]);
        return new BuiltWorld(chainPortfolio, resolution);
    }

    private static ResolutionModule BuildModule(
        string assemblyName,
        ulong moduleAddress,
        char digestCharacter,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeReferenceRowObservationIdentity>>? typeReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataModuleReferenceRowObservationIdentity>>? moduleReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>>? assemblyReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataAssemblyFileRowObservationIdentity>>? files = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataExportedTypeRowObservationIdentity>>? exportedTypes = null,
        bool includeScenarioTypes = true)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var typeReferenceRows = typeReferences?.Invoke(module) ?? [];
        var moduleReferenceRows = moduleReferences?.Invoke(module) ?? [];
        var assemblyReferenceRows = assemblyReferences?.Invoke(module) ?? [];
        var fileRows = files?.Invoke(module) ?? [];
        var exportedTypeRows = exportedTypes?.Invoke(module) ?? [];
        MetadataSourceEndIdentity definitionSourceEnds;
        MetadataDefinitionAuthorityCatalogIdentity authority;
        if (includeScenarioTypes)
        {
            var scenario = W8CompilerNameMappingContractTests.BuildScenario(
                usePointers: false,
                module: module,
                typeReferenceRowCount: typeReferenceRows.Length,
                assemblyReferenceRowCount: assemblyReferenceRows.Length,
                moduleReferenceRowCount: moduleReferenceRows.Length,
                fileRowCount: fileRows.Length,
                exportedTypeRowCount: exportedTypeRows.Length);
            definitionSourceEnds = scenario.SourceEnds;
            authority = scenario.DefinitionAuthority;
        }
        else
        {
            (definitionSourceEnds, authority) = BuildMinimalAuthority(
                module,
                typeReferenceRows.Length,
                assemblyReferenceRows.Length,
                moduleReferenceRows.Length,
                fileRows.Length,
                exportedTypeRows.Length);
        }
        var referenceEnds = MetadataReferenceSourceEndIdentity.Create(definitionSourceEnds);
        var tables = MetadataModuleReferenceTableSetIdentity.Create(
            referenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(referenceEnds, typeReferenceRows),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(referenceEnds, moduleReferenceRows),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(referenceEnds, assemblyReferenceRows),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(referenceEnds, fileRows),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(referenceEnds, exportedTypeRows));
        Assert.True(
            tables.AllTablesExact,
            $"TypeRefs={tables.TypeReferences.ResultKind}/{tables.TypeReferences.Issue}; " +
            $"AssemblyRefs={tables.AssemblyReferences.ResultKind}/{tables.AssemblyReferences.Issue}; " +
            $"Exported={tables.ExportedTypes.ResultKind}/{tables.ExportedTypes.Issue}.");
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var mapping = MetadataCompilerNameMappingCatalogIdentity.Create(authority);
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(compatibility, mapping);
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);
        return new ResolutionModule(module, compatibility, chainCatalog, referenceEnds, tables);
    }

    private static (MetadataSourceEndIdentity SourceEnds, MetadataDefinitionAuthorityCatalogIdentity Authority)
        BuildMinimalAuthority(
            StaticFieldMetadataModuleIdentity module,
            int typeReferenceRowCount,
            int assemblyReferenceRowCount,
            int moduleReferenceRowCount,
            int fileRowCount,
            int exportedTypeRowCount)
    {
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: 1,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: 1,
                fieldDefinitionRowCount: 0,
                typeReferenceRowCount: typeReferenceRowCount,
                assemblyReferenceRowCount: assemblyReferenceRowCount,
                moduleReferenceRowCount: moduleReferenceRowCount,
                fileRowCount: fileRowCount,
                exportedTypeRowCount: exportedTypeRowCount));
        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            [MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                0x0200_0001,
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
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);
        return (sourceEnds, authority);
    }

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity?> NullCandidateSlots(
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity?>(
            authority.TypeDefinitions.Length);
        for (var index = 0; index < authority.TypeDefinitions.Length; index++)
        {
            builder.Add(null);
        }
        return builder.MoveToImmutable();
    }

    private static MetadataTypeReferenceRowObservationIdentity TypeReferenceRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        string namespaceName,
        string typeName,
        int resolutionScope) =>
        MetadataTypeReferenceRowObservationIdentity.Create(
            module,
            0x0100_0000 | rowId,
            namespaceName,
            typeName,
            resolutionScope);

    private static MetadataModuleReferenceRowObservationIdentity ModuleReferenceRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataModuleReferenceRowObservationIdentity.Create(module, 0x1A00_0000 | rowId, "native-companion.dll");

    private static MetadataAssemblyReferenceRowObservationIdentity AssemblyReferenceRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        string assemblyName) =>
        MetadataAssemblyReferenceRowObservationIdentity.Create(
            module,
            0x2300_0000 | rowId,
            assemblyName,
            majorVersion: 1,
            minorVersion: 0,
            buildNumber: 0,
            revisionNumber: 0,
            culture: string.Empty,
            flags: 0,
            publicKeyOrToken: ImmutableArray<byte>.Empty,
            hashValue: ImmutableArray<byte>.Empty);

    private static MetadataAssemblyFileRowObservationIdentity FileRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataAssemblyFileRowObservationIdentity.Create(
            module,
            0x2600_0000 | rowId,
            flags: 0,
            name: "companion.netmodule",
            hashValue: [0x11, 0x22]);

    private static MetadataExportedTypeRowObservationIdentity ExportedTypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        string namespaceName,
        string typeName,
        int implementation) =>
        MetadataExportedTypeRowObservationIdentity.Create(
            module,
            0x2700_0000 | rowId,
            typeAttributes: 0x0020_0000,
            typeDefinitionId: 0,
            namespaceName: namespaceName,
            typeName: typeName,
            implementationMetadataToken: implementation);

    private static MetadataTypeReferenceResolutionIdentity Resolution(
        MetadataTypeReferenceResolutionPortfolioIdentity portfolio,
        StaticFieldMetadataModuleIdentity module,
        int typeReferenceRowId) =>
        Assert.IsType<MetadataTypeReferenceResolutionIdentity>(
            portfolio.ExactResolutionOrDefault(module, 0x0100_0000 | typeReferenceRowId));

    private static void AssertStop(
        MetadataTypeReferenceResolutionPortfolioIdentity result,
        MetadataTypeReferenceResolutionPortfolioResultKind resultKind,
        MetadataTypeReferenceResolutionPortfolioIssue issue)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Entries);
        Assert.Null(result.SnapshotSha256);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataTypeReferenceResolutionPortfolioIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
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
    }

    private sealed record ResolutionModule(
        StaticFieldMetadataModuleIdentity Module,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog,
        MetadataReferenceSourceEndIdentity ReferenceEnds,
        MetadataModuleReferenceTableSetIdentity Tables);

    private sealed record BuiltWorld(
        MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution);

    private sealed record PrimaryWorld(
        ResolutionModule App,
        ResolutionModule Lib,
        ResolutionModule Facade,
        MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution);
}
