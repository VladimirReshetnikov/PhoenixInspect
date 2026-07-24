using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the physical InterfaceImpl catalog and the bounded transitive interface authority portfolio.</summary>
public sealed class W8InterfaceImplementationAuthorityTests
{
    private const int InterfaceAttributes = 0x0000_00A1;
    private const int PublicClassAttributes = 0x0000_0001;
    private const int ModuleTypeToken = 0x0200_0001;
    private const int AlphaToken = 0x0200_0002;
    private const int BetaToken = 0x0200_0003;
    private const int WidgetToken = 0x0200_0004;
    private const int DerivedToken = 0x0200_0005;
    private const int CrossUserToken = 0x0200_0006;
    private const int PhantomToken = 0x0200_0007;
    private const int GenericUserToken = 0x0200_0008;
    private const int CycleAToken = 0x0200_0009;
    private const int CycleBToken = 0x0200_000A;
    private const int PlainToken = 0x0200_000B;
    private const int OrphanToken = 0x0200_000C;
    private const int LibSharedToken = 0x0200_0002;

    /// <summary>
    /// Proves the exact catalog joins every physical InterfaceImpl row to its authority owner, that the portfolio
    /// resolves same-module, cross-module, unresolved, and generic interface edges, and that bounded transitive
    /// closures union direct edges, interface extension, and base-chain contribution with typed terminals.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_portfolio_resolves_edges_and_bounded_transitive_closures()
    {
        var world = BuildInterfaceWorld();
        var replay = BuildInterfaceWorld();
        var portfolio = world.Portfolio;
        var catalog = world.App.Catalog;

        Assert.Equal(MetadataInterfaceImplementationTableResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataInterfaceImplementationTableIssue.None, catalog.Issue);
        Assert.Null(catalog.ReachedBound);
        Assert.Equal(0, catalog.ObservedCount);
        Assert.Null(catalog.RelatedMetadataToken);
        Assert.Equal(7, catalog.Rows.Length);
        Assert.Equal(
            Enumerable.Range(1, 7).Select(static rowId => 0x0900_0000 | rowId),
            catalog.Rows.Select(static row => row.InterfaceImplementationToken));
        Assert.Equal(world.App.SourceEnds, catalog.SourceEnds);

        var betaRow = Assert.IsType<MetadataInterfaceImplementationTableRowIdentity>(catalog.FindRow(0x0900_0001));
        Assert.Equal(BetaToken, betaRow.ImplementingTypeDefinitionToken);
        Assert.Equal(AlphaToken, betaRow.InterfaceMetadataToken);
        Assert.Equal("IBeta", betaRow.ImplementingTypeDefinition.TypeName);
        Assert.Null(catalog.FindRow(0x0900_0008));
        Assert.Null(catalog.FindRow(0x0200_0001));

        var appAuthority = world.App.Authority;
        var widgetRows = catalog.RowsForImplementingTypeOrEmpty(appAuthority.TypeDefinitions[3]);
        Assert.Equal(BetaToken, Assert.Single(widgetRows).InterfaceMetadataToken);
        Assert.Empty(catalog.RowsForImplementingTypeOrEmpty(appAuthority.TypeDefinitions[4]));
        Assert.Empty(catalog.RowsForImplementingTypeOrEmpty(world.Lib.Authority.TypeDefinitions[1]));

        Assert.Equal(MetadataInterfaceImplementationPortfolioResultKind.Exact, portfolio.ResultKind);
        Assert.Equal(MetadataInterfaceImplementationPortfolioIssue.None, portfolio.Issue);
        Assert.Equal(3, portfolio.Entries.Length);
        Assert.Equal(world.Ancestry.SnapshotSha256, portfolio.SnapshotSha256);

        var appEntry = portfolio.Entries.Single(entry => entry.SourceModule.Equals(world.App.Module));
        Assert.Equal(7, appEntry.Edges.Length);
        Assert.Equal(12, appEntry.Closures.Length);
        Assert.Null(appEntry.FindEdge(0x0900_0008));
        Assert.Null(appEntry.FindEdge(0x0200_0002));
        Assert.Null(appEntry.FindClosure(0x0200_00FF));
        Assert.Null(appEntry.FindClosure(0x0900_0001));

        // A same-module TypeDef interface selects its authority row directly.
        var betaEdge = Assert.IsType<MetadataInterfaceImplementationEdgeAuthorityIdentity>(
            appEntry.FindEdge(0x0900_0001));
        Assert.Equal(MetadataInterfaceImplementationEdgeKind.TypeDefinition, betaEdge.Kind);
        Assert.Equal(world.App.Module, betaEdge.TargetModule);
        Assert.Equal(appAuthority.TypeDefinitions[1], betaEdge.TargetTypeDefinition);
        Assert.Null(betaEdge.ReferenceResolution);
        Assert.Null(betaEdge.GenericInterfaceRow);

        // A cross-module interface resolves only through the retained TypeRef resolution portfolio.
        var crossEdge = appEntry.FindEdge(0x0900_0003)!;
        Assert.Equal(MetadataInterfaceImplementationEdgeKind.TypeReference, crossEdge.Kind);
        Assert.Equal(world.Lib.Module, crossEdge.TargetModule);
        Assert.Equal("ILibShared", crossEdge.TargetTypeDefinition!.TypeName);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.Resolved,
            crossEdge.ReferenceResolution!.Disposition);

        var phantomEdge = appEntry.FindEdge(0x0900_0004)!;
        Assert.Equal(MetadataInterfaceImplementationEdgeKind.TypeReferenceUnresolved, phantomEdge.Kind);
        Assert.Null(phantomEdge.TargetModule);
        Assert.Null(phantomEdge.TargetTypeDefinition);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.TargetTypeAbsent,
            phantomEdge.ReferenceResolution!.Disposition);

        // A generic interface instantiation is retained undecoded for the later construction slice.
        var genericEdge = appEntry.FindEdge(0x0900_0005)!;
        Assert.Equal(MetadataInterfaceImplementationEdgeKind.TypeSpecification, genericEdge.Kind);
        Assert.Null(genericEdge.TargetTypeDefinition);
        Assert.Equal(0x1B00_0001, genericEdge.GenericInterfaceRow!.TypeSpecificationToken);

        // A class implementing an interface directly, plus that interface's own extension edge.
        var widget = Closure(portfolio, world.App.Module, WidgetToken);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.Complete, widget.TerminalKind);
        Assert.Null(widget.ReachedBound);
        Assert.Null(widget.RelatedMetadataToken);
        Assert.Equal(
            [BetaToken, AlphaToken],
            widget.InterfaceEdges.Select(static edge => edge.TargetTypeDefinition!.TypeDefinitionToken));
        Assert.True(widget.ContainsInterface(world.App.Module, AlphaToken));
        Assert.False(widget.ContainsInterface(world.Lib.Module, AlphaToken));

        // An interface extending another interface is transitive on its own.
        var beta = Closure(portfolio, world.App.Module, BetaToken);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.Complete, beta.TerminalKind);
        Assert.Equal(AlphaToken, Assert.Single(beta.InterfaceEdges).TargetTypeDefinition!.TypeDefinitionToken);
        Assert.Empty(Closure(portfolio, world.App.Module, AlphaToken).InterfaceEdges);

        // An interface inherited only from a base class arrives through the bounded ancestry chain.
        var derived = Closure(portfolio, world.App.Module, DerivedToken);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.Complete, derived.TerminalKind);
        Assert.Equal(
            [BetaToken, AlphaToken],
            derived.InterfaceEdges.Select(static edge => edge.TargetTypeDefinition!.TypeDefinitionToken));
        Assert.Empty(catalog.RowsForImplementingTypeOrEmpty(appAuthority.TypeDefinitions[4]));

        var cross = Closure(portfolio, world.App.Module, CrossUserToken);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.Complete, cross.TerminalKind);
        Assert.True(cross.ContainsInterface(world.Lib.Module, LibSharedToken));

        var phantom = Closure(portfolio, world.App.Module, PhantomToken);
        Assert.Equal(
            MetadataInterfaceImplementationClosureTerminalKind.UnresolvedReferenceReached,
            phantom.TerminalKind);
        Assert.Empty(phantom.InterfaceEdges);
        Assert.Equal(0x0100_0003, phantom.RelatedMetadataToken);
        Assert.Null(phantom.ReachedBound);

        var generic = Closure(portfolio, world.App.Module, GenericUserToken);
        Assert.Equal(
            MetadataInterfaceImplementationClosureTerminalKind.GenericInterfaceReached,
            generic.TerminalKind);
        Assert.Empty(generic.InterfaceEdges);
        Assert.Equal(0x1B00_0001, generic.RelatedMetadataToken);

        var cycle = Closure(portfolio, world.App.Module, CycleAToken);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.CycleDetected, cycle.TerminalKind);
        Assert.Equal(CycleBToken, Assert.Single(cycle.InterfaceEdges).TargetTypeDefinition!.TypeDefinitionToken);
        Assert.Equal(CycleAToken, cycle.RelatedMetadataToken);

        // An unresolved base edge makes the base contribution incomplete even with no interface row of its own.
        var orphan = Closure(portfolio, world.App.Module, OrphanToken);
        Assert.Equal(
            MetadataInterfaceImplementationClosureTerminalKind.UnresolvedReferenceReached,
            orphan.TerminalKind);
        Assert.Empty(orphan.InterfaceEdges);

        var plain = Closure(portfolio, world.App.Module, PlainToken);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.Complete, plain.TerminalKind);
        Assert.Empty(plain.InterfaceEdges);
        Assert.Equal(
            MetadataInterfaceImplementationClosureTerminalKind.Complete,
            Closure(portfolio, world.App.Module, ModuleTypeToken).TerminalKind);

        // The three-valued answer proves a negative only over a complete closure.
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Yes,
            portfolio.Implements(world.App.Module, WidgetToken, world.App.Module, AlphaToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Yes,
            portfolio.Implements(world.App.Module, DerivedToken, world.App.Module, AlphaToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Yes,
            portfolio.Implements(world.App.Module, CrossUserToken, world.Lib.Module, LibSharedToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.No,
            portfolio.Implements(world.App.Module, PlainToken, world.App.Module, AlphaToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.No,
            portfolio.Implements(world.App.Module, WidgetToken, world.Lib.Module, LibSharedToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Unprovable,
            portfolio.Implements(world.App.Module, PhantomToken, world.App.Module, AlphaToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Unprovable,
            portfolio.Implements(world.App.Module, GenericUserToken, world.App.Module, AlphaToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Unprovable,
            portfolio.Implements(world.App.Module, CycleAToken, world.App.Module, AlphaToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Unprovable,
            portfolio.Implements(world.App.Module, OrphanToken, world.App.Module, AlphaToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Yes,
            portfolio.Implements(world.App.Module, CycleAToken, world.App.Module, CycleBToken));
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Unprovable,
            portfolio.Implements(world.App.Module, 0x0200_00FF, world.App.Module, AlphaToken));
        Assert.Null(portfolio.ExactImplementedInterfacesOrDefault(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0xEC00, 'd', "Synthetic.Elsewhere"),
            WidgetToken));

        Assert.Equal(portfolio, replay.Portfolio);
        Assert.Equal(portfolio.GetHashCode(), replay.Portfolio.GetHashCode());
        Assert.Equal(portfolio.Sha256, replay.Portfolio.Sha256);
        Assert.Equal(
            "37d20157d6cf15555ad2374807f44d2e29d47c3c9924535315aaf5116f4127f1",
            portfolio.Sha256);
    }

    /// <summary>
    /// Proves every physical InterfaceImpl catalog contradiction and acquisition stop is typed and prefix-free.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Catalog_stops_are_typed_and_prefix_free()
    {
        var world = BuildInterfaceWorld();
        var authority = world.App.Authority;
        var module = world.App.Module;
        var rows = world.App.InterfaceRows;

        var nonExactAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            omitGenericParameterRows: true).DefinitionAuthority;
        Assert.Equal(MetadataDefinitionAuthorityResultKind.NonExact, nonExactAuthority.ResultKind);
        var authorityNonExact = MetadataInterfaceImplementationTableCatalogIdentity.Create(
            nonExactAuthority,
            ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>.Empty);
        AssertStop(
            authorityNonExact,
            MetadataInterfaceImplementationTableResultKind.NonExact,
            MetadataInterfaceImplementationTableIssue.DefinitionAuthorityNonExact);
        Assert.Equal(nonExactAuthority.ObservedCount, authorityNonExact.ObservedCount);
        Assert.Equal(nonExactAuthority.ReachedBound, authorityNonExact.ReachedBound);

        var invalidAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            invalidModuleName: true).DefinitionAuthority;
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Invalid, invalidAuthority.ResultKind);
        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(
                invalidAuthority,
                ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>.Empty),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.DefinitionAuthorityInvalid);

        var overCapModule = BuildModule(
            "Synthetic.IfaceCap",
            0xC900,
            'c',
            [new W8MetadataAncestryAuthorityContractTests.TypeRow(
                "Synthetic.IfaceCap",
                "Lonely",
                PublicClassAttributes,
                null)],
            interfaceImplementationRowCountOverride:
                StaticFieldV2Limits.MaximumInterfaceImplementationRowCount + 1);
        var overCap = MetadataInterfaceImplementationTableCatalogIdentity.Create(
            overCapModule.Authority,
            default);
        AssertStop(
            overCap,
            MetadataInterfaceImplementationTableResultKind.NonExact,
            MetadataInterfaceImplementationTableIssue.TableRowBoundReached);
        Assert.Equal(
            "expression-v2.metadata.interfaceimpl-rows",
            Assert.IsType<EvaluationDeterministicBound>(overCap.ReachedBound).Name);
        Assert.Equal(
            StaticFieldV2Limits.MaximumInterfaceImplementationRowCount,
            overCap.ReachedBound!.Value);
        Assert.Equal(StaticFieldV2Limits.MaximumInterfaceImplementationRowCount + 1, overCap.ObservedCount);

        var incomplete = MetadataInterfaceImplementationTableCatalogIdentity.Create(authority, default);
        AssertStop(
            incomplete,
            MetadataInterfaceImplementationTableResultKind.NonExact,
            MetadataInterfaceImplementationTableIssue.TableIncomplete);
        Assert.Equal(0, incomplete.ObservedCount);
        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(authority, [.. rows.Take(6)]),
            MetadataInterfaceImplementationTableResultKind.NonExact,
            MetadataInterfaceImplementationTableIssue.TableIncomplete);

        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(authority, [.. rows, rows[0]]),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.TableRowCountConflict);

        var misordered = rows.SetItem(0, rows[1]).SetItem(1, rows[0]);
        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(authority, misordered),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.PhysicalOrderInvalid);
        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(authority, rows.SetItem(0, null!)),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.PhysicalOrderInvalid);

        var strangerModule = W8CompilerNameMappingContractTests.CreateMetadataModule(0xEB00, 'e', "Synthetic.Stray");
        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(
                authority,
                rows.SetItem(0, InterfaceRow(strangerModule, 1, BetaToken, AlphaToken))),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.SourceMismatch);

        var absentOwner = MetadataInterfaceImplementationTableCatalogIdentity.Create(
            authority,
            rows.SetItem(0, InterfaceRow(module, 1, 0x0200_00FF, AlphaToken)));
        AssertStop(
            absentOwner,
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.ImplementingTypeDefinitionMissing);
        Assert.Equal(0x0200_00FF, absentOwner.RelatedMetadataToken);

        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(
                authority,
                rows.SetItem(0, InterfaceRow(module, 1, BetaToken, 0x0200_00FF))),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.InterfaceTokenOutOfRange);
        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(
                authority,
                rows.SetItem(0, InterfaceRow(module, 1, BetaToken, 0x0400_0001))),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.InterfaceTokenOutOfRange);

        AssertStop(
            MetadataInterfaceImplementationTableCatalogIdentity.Create(
                authority,
                rows.SetItem(1, InterfaceRow(module, 2, BetaToken, AlphaToken))),
            MetadataInterfaceImplementationTableResultKind.Invalid,
            MetadataInterfaceImplementationTableIssue.DuplicateSemanticEdge);

        Assert.Throws<ArgumentNullException>(() =>
            MetadataInterfaceImplementationTableCatalogIdentity.Create(null!, rows));
        Assert.Throws<ArgumentNullException>(() =>
            MetadataInterfaceImplementationRowObservationIdentity.Create(null!, 0x0900_0001, BetaToken, AlphaToken));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataInterfaceImplementationRowObservationIdentity.Create(module, 0x0900_0000, BetaToken, AlphaToken));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataInterfaceImplementationRowObservationIdentity.Create(module, 0x0200_0001, BetaToken, AlphaToken));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetadataInterfaceImplementationRowObservationIdentity.Create(module, 0x0900_0001, 0x0100_0001, AlphaToken));
    }

    /// <summary>
    /// Proves the shared sixty-four-hop bound is exact at the cap and typed at cap plus one over one generated deep
    /// interface-extension chain.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Closure_depth_bound_is_exact_at_cap_and_typed_at_cap_plus_one()
    {
        const int depthCap = MetadataInterfaceImplementationPortfolioIdentity.MaximumClosureDepth;
        var core = W8MetadataAncestryAuthorityContractTests.BuildCoreModule();
        var deepTypes = ImmutableArray.CreateBuilder<W8MetadataAncestryAuthorityContractTests.TypeRow>(depthCap + 2);
        for (var index = 1; index <= depthCap + 2; index++)
        {
            deepTypes.Add(new W8MetadataAncestryAuthorityContractTests.TypeRow(
                "Synthetic.DeepIface",
                $"IDeep{index}",
                InterfaceAttributes,
                null));
        }
        var deep = BuildModule(
            "Synthetic.DeepIface",
            0xD900,
            '2',
            deepTypes.ToImmutable(),
            interfaceImplementations: metadataModule =>
            [
                .. Enumerable.Range(1, depthCap + 1).Select(rowId => InterfaceRow(
                    metadataModule,
                    rowId,
                    0x0200_0001 + rowId,
                    0x0200_0002 + rowId)),
            ]);
        var world = BuildWorld(core, deep);
        Assert.Equal(MetadataInterfaceImplementationPortfolioResultKind.Exact, world.Portfolio.ResultKind);

        var atCap = Closure(world.Portfolio, deep.Module, 0x0200_0003);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.Complete, atCap.TerminalKind);
        Assert.Equal(depthCap, atCap.InterfaceEdges.Length);
        Assert.Null(atCap.ReachedBound);

        var overCap = Closure(world.Portfolio, deep.Module, 0x0200_0002);
        Assert.Equal(MetadataInterfaceImplementationClosureTerminalKind.DepthBoundReached, overCap.TerminalKind);
        Assert.Equal(depthCap, overCap.InterfaceEdges.Length);
        var bound = Assert.IsType<EvaluationDeterministicBound>(overCap.ReachedBound);
        Assert.Equal("expression-v2.metadata.ancestry-depth", bound.Name);
        Assert.Equal(depthCap, bound.Value);
        Assert.Equal(0x0200_0002 + depthCap + 1, overCap.RelatedMetadataToken);
        Assert.Equal(
            MetadataInterfaceImplementationAnswer.Unprovable,
            world.Portfolio.Implements(deep.Module, 0x0200_0002, deep.Module, 0x0200_0002 + depthCap + 1));
    }

    /// <summary>
    /// Proves prerequisite, vector-shape, catalog, module-correlation, and source-lineage contradictions produce
    /// deterministic prefix-free typed portfolio stops.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Portfolio_prerequisite_vector_and_lineage_stops_are_typed_and_prefix_free()
    {
        var world = BuildInterfaceWorld();
        var ancestry = world.Ancestry;
        var catalogs = new[] { world.Core.Catalog, world.Lib.Catalog, world.App.Catalog };

        var stoppedAncestry = MetadataAncestryAuthorityPortfolioIdentity.Create(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                    MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                        ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
                    ImmutableArray<MetadataNamedTypeDefinitionChainCatalogIdentity>.Empty),
                ImmutableArray<MetadataModuleReferenceTableSetIdentity>.Empty));
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(stoppedAncestry, [catalogs[0]]),
            MetadataInterfaceImplementationPortfolioResultKind.NonExact,
            MetadataInterfaceImplementationPortfolioIssue.AncestryPortfolioNonExact);

        var ambiguousAncestry = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule(),
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule("Synthetic.Core2", 0xDB00, '9')).Ancestry;
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Invalid, ambiguousAncestry.ResultKind);
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(ambiguousAncestry, [catalogs[0]]),
            MetadataInterfaceImplementationPortfolioResultKind.Invalid,
            MetadataInterfaceImplementationPortfolioIssue.AncestryPortfolioInvalid);

        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(ancestry, default),
            MetadataInterfaceImplementationPortfolioResultKind.NonExact,
            MetadataInterfaceImplementationPortfolioIssue.CatalogVectorUninitialized);
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(ancestry, [catalogs[0], catalogs[1]]),
            MetadataInterfaceImplementationPortfolioResultKind.NonExact,
            MetadataInterfaceImplementationPortfolioIssue.CatalogSlotsIncomplete);
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(
                ancestry,
                [catalogs[0], catalogs[1], catalogs[2], catalogs[2]]),
            MetadataInterfaceImplementationPortfolioResultKind.Invalid,
            MetadataInterfaceImplementationPortfolioIssue.CatalogSlotCountConflict);
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(
                ancestry,
                [catalogs[0], catalogs[1], null!]),
            MetadataInterfaceImplementationPortfolioResultKind.NonExact,
            MetadataInterfaceImplementationPortfolioIssue.CatalogMissing);
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(
                ancestry,
                [catalogs[0], catalogs[1], catalogs[1]]),
            MetadataInterfaceImplementationPortfolioResultKind.Invalid,
            MetadataInterfaceImplementationPortfolioIssue.DuplicateSourceModule);

        var overCap = MetadataInterfaceImplementationPortfolioIdentity.Create(
            ancestry,
            [.. Enumerable.Repeat(
                catalogs[0],
                MetadataInterfaceImplementationPortfolioIdentity.MaximumModuleCount + 1)]);
        AssertPortfolioStop(
            overCap,
            MetadataInterfaceImplementationPortfolioResultKind.NonExact,
            MetadataInterfaceImplementationPortfolioIssue.ModuleCountBoundReached);
        Assert.Equal("expression-v2.metadata.modules", overCap.ReachedBound!.Name);

        var nonExactCatalog = MetadataInterfaceImplementationTableCatalogIdentity.Create(
            world.App.Authority,
            default);
        Assert.Equal(MetadataInterfaceImplementationTableResultKind.NonExact, nonExactCatalog.ResultKind);
        var nonExactStop = MetadataInterfaceImplementationPortfolioIdentity.Create(
            ancestry,
            [catalogs[0], catalogs[1], nonExactCatalog]);
        AssertPortfolioStop(
            nonExactStop,
            MetadataInterfaceImplementationPortfolioResultKind.NonExact,
            MetadataInterfaceImplementationPortfolioIssue.ImplementationCatalogNonExact);
        Assert.Equal(world.App.Module.Sha256, nonExactStop.RelatedModuleSha256);

        var invalidCatalog = MetadataInterfaceImplementationTableCatalogIdentity.Create(
            world.App.Authority,
            [.. world.App.InterfaceRows, world.App.InterfaceRows[0]]);
        Assert.Equal(MetadataInterfaceImplementationTableResultKind.Invalid, invalidCatalog.ResultKind);
        var invalidStop = MetadataInterfaceImplementationPortfolioIdentity.Create(
            ancestry,
            [catalogs[0], catalogs[1], invalidCatalog]);
        AssertPortfolioStop(
            invalidStop,
            MetadataInterfaceImplementationPortfolioResultKind.Invalid,
            MetadataInterfaceImplementationPortfolioIssue.ImplementationCatalogInvalid);
        Assert.Equal(world.App.Module.Sha256, invalidStop.RelatedModuleSha256);

        // The same module re-observed with a different InterfaceImpl source end is a lineage contradiction.
        var relineaged = BuildModule(
            "Synthetic.IfaceApp",
            0xA900,
            'a',
            AppTypeRows(),
            typeReferences: AppTypeReferences,
            assemblyReferences: AppAssemblyReferences,
            typeSpecifications: AppTypeSpecifications,
            interfaceImplementationRowCountOverride: 0);
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(
                ancestry,
                [catalogs[0], catalogs[1], relineaged.Catalog]),
            MetadataInterfaceImplementationPortfolioResultKind.Invalid,
            MetadataInterfaceImplementationPortfolioIssue.ImplementationSourceEndsMismatch);

        var stranger = BuildModule(
            "Synthetic.IfaceStray",
            0xE900,
            '1',
            [new W8MetadataAncestryAuthorityContractTests.TypeRow(
                "Synthetic.IfaceStray",
                "Solo",
                PublicClassAttributes,
                null)]);
        AssertPortfolioStop(
            MetadataInterfaceImplementationPortfolioIdentity.Create(
                ancestry,
                [catalogs[0], catalogs[1], stranger.Catalog]),
            MetadataInterfaceImplementationPortfolioResultKind.Invalid,
            MetadataInterfaceImplementationPortfolioIssue.SourceModuleNotInPortfolio);

        Assert.Throws<ArgumentNullException>(() =>
            MetadataInterfaceImplementationPortfolioIdentity.Create(null!, catalogs.ToImmutableArray()));
        Assert.Throws<ArgumentNullException>(() =>
            world.Portfolio.ExactImplementedInterfacesOrDefault(null!, WidgetToken));
        Assert.Throws<ArgumentNullException>(() =>
            world.Portfolio.Implements(world.App.Module, WidgetToken, null!, AlphaToken));
    }

    /// <summary>
    /// Proves defensive copies, private guarded issuance for every interface-implementation row family, the closed
    /// public issuer surface, and emitted XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Interface_implementation_contracts_are_immutable_guarded_and_documented()
    {
        var world = BuildInterfaceWorld();
        var portfolio = world.Portfolio;
        var catalog = world.App.Catalog;
        var originalBytes = portfolio.CanonicalBytes;
        var originalSha = portfolio.Sha256;
        var widget = Closure(portfolio, world.App.Module, WidgetToken);
        var originalClosureEdges = widget.InterfaceEdges;
        var originalRows = catalog.Rows;

        var returnedEntries = portfolio.Entries;
        ImmutableCollectionsMarshal.AsArray(returnedEntries)![0] = returnedEntries[^1];
        var returnedBytes = portfolio.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        var returnedClosureEdges = widget.InterfaceEdges;
        ImmutableCollectionsMarshal.AsArray(returnedClosureEdges)![0] = returnedClosureEdges[^1];
        var returnedRows = catalog.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        var returnedOwned = catalog.RowsForImplementingTypeOrEmpty(world.App.Authority.TypeDefinitions[3]);
        ImmutableCollectionsMarshal.AsArray(returnedOwned)![0] = null!;

        Assert.True(originalBytes.AsSpan().SequenceEqual(portfolio.CanonicalBytes.AsSpan()));
        Assert.Equal(originalSha, portfolio.Sha256);
        Assert.Equal(originalClosureEdges[0], widget.InterfaceEdges[0]);
        Assert.Equal(originalRows[0], catalog.Rows[0]);
        Assert.NotNull(catalog.RowsForImplementingTypeOrEmpty(world.App.Authority.TypeDefinitions[3])[0]);

        var appEntry = portfolio.Entries.Single(entry => entry.SourceModule.Equals(world.App.Module));
        var betaEdge = appEntry.FindEdge(0x0900_0001)!;
        Assert.Throws<ArgumentException>(() => MetadataInterfaceImplementationTableRowIdentity.Create(
            new object(),
            catalog.SourceEnds,
            world.App.InterfaceRows[0],
            world.App.Authority.TypeDefinitions[2]));
        Assert.Throws<ArgumentException>(() => MetadataInterfaceImplementationEdgeAuthorityIdentity.Create(
            new object(),
            betaEdge.ImplementationRow,
            MetadataInterfaceImplementationEdgeKind.TypeSpecification,
            null,
            null,
            null,
            null));
        Assert.Throws<ArgumentException>(() => MetadataInterfaceImplementationClosureIdentity.Create(
            new object(),
            world.App.Module,
            world.App.Authority.TypeDefinitions[3],
            widget.InterfaceEdges,
            MetadataInterfaceImplementationClosureTerminalKind.Complete,
            null,
            null));
        Assert.Throws<ArgumentException>(() => MetadataInterfaceImplementationPortfolioModuleIdentity.Create(
            new object(),
            appEntry.ImplementationCatalog,
            appEntry.AncestryEntry,
            appEntry.Edges,
            appEntry.Closures));
        Assert.False(MetadataInterfaceImplementationTableCatalogIdentity.OwnsRowMintCapability(new object()));
        Assert.False(MetadataInterfaceImplementationPortfolioIdentity.OwnsRowMintCapability(new object()));

        var publicTypes = new[]
        {
            typeof(MetadataInterfaceImplementationTableResultKind),
            typeof(MetadataInterfaceImplementationTableIssue),
            typeof(MetadataInterfaceImplementationRowObservationIdentity),
            typeof(MetadataInterfaceImplementationTableRowIdentity),
            typeof(MetadataInterfaceImplementationTableCatalogIdentity),
            typeof(MetadataInterfaceImplementationEdgeKind),
            typeof(MetadataInterfaceImplementationEdgeAuthorityIdentity),
            typeof(MetadataInterfaceImplementationClosureTerminalKind),
            typeof(MetadataInterfaceImplementationClosureIdentity),
            typeof(MetadataInterfaceImplementationAnswer),
            typeof(MetadataInterfaceImplementationPortfolioModuleIdentity),
            typeof(MetadataInterfaceImplementationPortfolioResultKind),
            typeof(MetadataInterfaceImplementationPortfolioIssue),
            typeof(MetadataInterfaceImplementationPortfolioIdentity),
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
            if (type == typeof(MetadataInterfaceImplementationRowObservationIdentity) ||
                type == typeof(MetadataInterfaceImplementationTableCatalogIdentity) ||
                type == typeof(MetadataInterfaceImplementationPortfolioIdentity))
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

    private static InterfaceWorld BuildInterfaceWorld()
    {
        var core = W8MetadataAncestryAuthorityContractTests.BuildCoreModule();
        var lib = BuildModule(
            "Synthetic.IfaceLib",
            0xB900,
            'b',
            [new W8MetadataAncestryAuthorityContractTests.TypeRow(
                "Synthetic.IfaceLib",
                "ILibShared",
                InterfaceAttributes,
                null)]);
        var app = BuildModule(
            "Synthetic.IfaceApp",
            0xA900,
            'a',
            AppTypeRows(),
            typeReferences: AppTypeReferences,
            assemblyReferences: AppAssemblyReferences,
            typeSpecifications: AppTypeSpecifications,
            interfaceImplementations: AppInterfaceImplementations);
        return BuildWorld(core, lib, app);
    }

    private static ImmutableArray<W8MetadataAncestryAuthorityContractTests.TypeRow> AppTypeRows() =>
    [
        new("Synthetic.IfaceApp", "IAlpha", InterfaceAttributes, null),
        new("Synthetic.IfaceApp", "IBeta", InterfaceAttributes, null),
        new("Synthetic.IfaceApp", "Widget", PublicClassAttributes, 0x0100_0001),
        new("Synthetic.IfaceApp", "Derived", PublicClassAttributes, WidgetToken),
        new("Synthetic.IfaceApp", "CrossUser", PublicClassAttributes, 0x0100_0001),
        new("Synthetic.IfaceApp", "Phantom", PublicClassAttributes, 0x0100_0001),
        new("Synthetic.IfaceApp", "GenericUser", PublicClassAttributes, 0x0100_0001),
        new("Synthetic.IfaceApp", "ICycleA", InterfaceAttributes, null),
        new("Synthetic.IfaceApp", "ICycleB", InterfaceAttributes, null),
        new("Synthetic.IfaceApp", "Plain", PublicClassAttributes, 0x0100_0001),
        new("Synthetic.IfaceApp", "Orphan", PublicClassAttributes, 0x0100_0003),
    ];

    private static ImmutableArray<MetadataTypeReferenceRowObservationIdentity> AppTypeReferences(
        StaticFieldMetadataModuleIdentity module) =>
    [
        W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(module, 1, "System", "Object", 0x2300_0001),
        W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
            module,
            2,
            "Synthetic.IfaceLib",
            "ILibShared",
            0x2300_0002),
        W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(module, 3, "System", "Missing", 0x2300_0001),
    ];

    private static ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity> AppAssemblyReferences(
        StaticFieldMetadataModuleIdentity module) =>
    [
        W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Synthetic.Core"),
        W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 2, "Synthetic.IfaceLib"),
    ];

    private static ImmutableArray<MetadataTypeSpecificationRowObservationIdentity> AppTypeSpecifications(
        StaticFieldMetadataModuleIdentity module) =>
    [
        MetadataTypeSpecificationRowObservationIdentity.Create(
            module,
            0x1B00_0001,
            [0x15, 0x12, 0x08, 0x01, 0x0E]),
    ];

    private static ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>
        AppInterfaceImplementations(StaticFieldMetadataModuleIdentity module) =>
    [
        InterfaceRow(module, 1, BetaToken, AlphaToken),
        InterfaceRow(module, 2, WidgetToken, BetaToken),
        InterfaceRow(module, 3, CrossUserToken, 0x0100_0002),
        InterfaceRow(module, 4, PhantomToken, 0x0100_0003),
        InterfaceRow(module, 5, GenericUserToken, 0x1B00_0001),
        InterfaceRow(module, 6, CycleAToken, CycleBToken),
        InterfaceRow(module, 7, CycleBToken, CycleAToken),
    ];

    private static MetadataInterfaceImplementationRowObservationIdentity InterfaceRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int implementingTypeDefinitionToken,
        int interfaceMetadataToken) =>
        MetadataInterfaceImplementationRowObservationIdentity.Create(
            module,
            0x0900_0000 | rowId,
            implementingTypeDefinitionToken,
            interfaceMetadataToken);

    private static InterfaceWorld BuildWorld(
        W8MetadataAncestryAuthorityContractTests.AncestryModule core,
        params InterfaceModule[] modules)
    {
        var coreModule = new InterfaceModule(
            core,
            core.ChainCatalog.DefinitionAuthority,
            ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>.Empty,
            MetadataInterfaceImplementationTableCatalogIdentity.Create(
                core.ChainCatalog.DefinitionAuthority,
                ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>.Empty));
        Assert.Equal(
            MetadataInterfaceImplementationTableResultKind.Exact,
            coreModule.Catalog.ResultKind);
        var ancestry = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            [core, .. modules.Select(static entry => entry.Ancestry)]).Ancestry;
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestry.ResultKind);
        var portfolio = MetadataInterfaceImplementationPortfolioIdentity.Create(
            ancestry,
            [coreModule.Catalog, .. modules.Select(static entry => entry.Catalog)]);
        return new InterfaceWorld(
            ancestry,
            portfolio,
            coreModule,
            modules.Length > 0 ? modules[0] : coreModule,
            modules.Length > 1 ? modules[1] : coreModule);
    }

    private static InterfaceModule BuildModule(
        string assemblyName,
        ulong moduleAddress,
        char digestCharacter,
        ImmutableArray<W8MetadataAncestryAuthorityContractTests.TypeRow> namedTypes,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeReferenceRowObservationIdentity>>? typeReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>>? assemblyReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>>? typeSpecifications = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>>? interfaceImplementations = null,
        int? interfaceImplementationRowCountOverride = null)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var typeReferenceRows = typeReferences?.Invoke(module) ?? [];
        var assemblyReferenceRows = assemblyReferences?.Invoke(module) ?? [];
        var typeSpecificationRows = typeSpecifications?.Invoke(module) ?? [];
        var interfaceRows = interfaceImplementations?.Invoke(module) ?? [];
        var totalTypeCount = namedTypes.Length + 1;
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: totalTypeCount,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: totalTypeCount,
                fieldDefinitionRowCount: 0,
                typeReferenceRowCount: typeReferenceRows.Length,
                typeSpecificationRowCount: typeSpecificationRows.Length,
                assemblyReferenceRowCount: assemblyReferenceRows.Length,
                interfaceImplementationRowCount:
                    interfaceImplementationRowCountOverride ?? interfaceRows.Length));

        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(totalTypeCount);
        typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            ModuleTypeToken,
            fieldListRowId: 1,
            methodListRowId: 1,
            namespaceName: string.Empty,
            typeName: "<Module>",
            typeAttributes: 0,
            extendsMetadataToken: null));
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                0x0200_0002 + index,
                fieldListRowId: 1,
                methodListRowId: 1,
                namespaceName: row.NamespaceName,
                typeName: row.TypeName,
                typeAttributes: row.TypeAttributes,
                extendsMetadataToken: row.ExtendsMetadataToken));
        }

        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            typeRows.MoveToImmutable(),
            pointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            ImmutableArray<MetadataGenericParameterRowObservationIdentity>.Empty);
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var referenceEnds = MetadataReferenceSourceEndIdentity.Create(sourceEnds);
        var tables = MetadataModuleReferenceTableSetIdentity.Create(
            referenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(referenceEnds, typeReferenceRows),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(referenceEnds, typeSpecificationRows),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(referenceEnds, assemblyReferenceRows),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataAssemblyFileRowObservationIdentity>.Empty),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataExportedTypeRowObservationIdentity>.Empty));
        Assert.True(tables.AllTablesExact);

        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var mapping = MetadataCompilerNameMappingCatalogIdentity.Create(authority);
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(compatibility, mapping);
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);

        return new InterfaceModule(
            new W8MetadataAncestryAuthorityContractTests.AncestryModule(
                module,
                compatibility,
                chainCatalog,
                tables),
            authority,
            interfaceRows,
            MetadataInterfaceImplementationTableCatalogIdentity.Create(
                authority,
                interfaceImplementationRowCountOverride is null ? interfaceRows : default));
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

    private static MetadataInterfaceImplementationClosureIdentity Closure(
        MetadataInterfaceImplementationPortfolioIdentity portfolio,
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionToken) =>
        Assert.IsType<MetadataInterfaceImplementationClosureIdentity>(
            portfolio.ExactImplementedInterfacesOrDefault(module, typeDefinitionToken));

    private static void AssertStop(
        MetadataInterfaceImplementationTableCatalogIdentity result,
        MetadataInterfaceImplementationTableResultKind resultKind,
        MetadataInterfaceImplementationTableIssue issue)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Rows);
        Assert.Null(result.FindRow(0x0900_0001));
    }

    private static void AssertPortfolioStop(
        MetadataInterfaceImplementationPortfolioIdentity result,
        MetadataInterfaceImplementationPortfolioResultKind resultKind,
        MetadataInterfaceImplementationPortfolioIssue issue)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Entries);
        Assert.Null(result.SnapshotSha256);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataInterfaceImplementationPortfolioIdentity).Assembly;
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

    private sealed record InterfaceModule(
        W8MetadataAncestryAuthorityContractTests.AncestryModule Ancestry,
        MetadataDefinitionAuthorityCatalogIdentity Authority,
        ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity> InterfaceRows,
        MetadataInterfaceImplementationTableCatalogIdentity Catalog)
    {
        internal StaticFieldMetadataModuleIdentity Module => Ancestry.Module;

        internal MetadataSourceEndIdentity SourceEnds => Authority.SourceEnds;
    }

    private sealed record InterfaceWorld(
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        MetadataInterfaceImplementationPortfolioIdentity Portfolio,
        InterfaceModule Core,
        InterfaceModule Lib,
        InterfaceModule App);
}
