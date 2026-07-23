using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises scope-precise import, alias, and extern-alias binding over an exact ImportScope chain.</summary>
public sealed class W8V2ScopedContextBindingTests
{
    private const int PublicClass = 0x0000_0001;

    private const int HostToken = 0x0200_0002;
    private const int AliasTargetToken = 0x0200_0003;
    private const int OuterAliasToken = 0x0200_0004;
    private const int InnerThingToken = 0x0200_0005;
    private const int ImportedThing2Token = 0x0200_0006;
    private const int ImportedOneWidgetToken = 0x0200_0007;
    private const int ImportedTwoWidgetToken = 0x0200_0008;
    private const int DeepToken = 0x0200_0009;
    private const int AppSharedWidgetToken = 0x0200_000A;

    private const int LibSharedWidgetToken = 0x0200_0002;
    private const int LibConvergedToken = 0x0200_0003;

    private const int LibAssemblyReferenceToken = 0x2300_0001;
    private const int TypeSpecificationToken = 0x1B00_0001;

    /// <summary>
    /// Proves one exact type alias supplies the bound head, one exact namespace alias supplies the namespace
    /// prefix, and both are consulted before any namespace declaration or import at the same level.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Type_and_namespace_aliases_bind_exactly_at_their_declaring_level()
    {
        var typeAliasOutcome = Bind(
            "T.Field",
            Chain([], [TypeAliasImport("T", "Aliased.Space.AliasTarget", AliasTargetToken)]));

        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, typeAliasOutcome.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.None, typeAliasOutcome.Issue);
        Assert.Null(typeAliasOutcome.ReachedBound);
        var typeAliasGroup = Assert.IsType<StaticFieldV2ContextualCandidateGroup>(
            typeAliasOutcome.SelectedCandidate);
        Assert.Equal(AliasTargetToken, typeAliasGroup.FinalTypeDefinitionToken);
        var typeAliasPartition = Assert.Single(typeAliasOutcome.PartitionResults);
        Assert.Equal(0, typeAliasPartition.SelectedLevelIndex);
        Assert.Equal(1, typeAliasPartition.OwnerSegmentCount);
        var typeAliasLevel = Assert.Single(typeAliasPartition.Levels);
        Assert.Equal(StaticFieldV2ContextualLevelDisposition.Selected, typeAliasLevel.Disposition);
        Assert.Equal("App.Inner", typeAliasLevel.NamespaceName);
        Assert.Equal(1, typeAliasLevel.ScopeLevelIndex);
        var typeAliasCandidate = Assert.Single(typeAliasGroup.Candidates);
        Assert.Equal(StaticFieldV2ContextualCandidateSource.TypeAlias, typeAliasCandidate.Source);
        Assert.Equal(
            StaticFieldV2ScopedImportKind.TypeAlias,
            typeAliasCandidate.ContributingImport!.Kind);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeDefinitionResolved,
            typeAliasCandidate.ContributingImport.TargetDisposition);

        var namespaceAliasOutcome = Bind(
            "N.Deep.Field",
            Chain([], [NamespaceAliasImport("N", "Ns.Alias.Target")]));

        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, namespaceAliasOutcome.ResultKind);
        Assert.Equal(DeepToken, namespaceAliasOutcome.SelectedCandidate!.FinalTypeDefinitionToken);
        Assert.Equal(3, namespaceAliasOutcome.PartitionResults.Length);
        Assert.Equal(
            2,
            namespaceAliasOutcome.PartitionResults
                .Count(static result => result.ResultKind == StaticFieldV2ContextualBindingResultKind.Exact));
        Assert.Equal(
            StaticFieldV2ContextualCandidateSource.NamespaceAlias,
            namespaceAliasOutcome.SelectedCandidate.Candidates[0].Source);
    }

    /// <summary>
    /// Proves an inner same-name alias hides an outer one, that both declarations stay retained, and that the
    /// hidden outer declaration is marked shadowed rather than dropped from the projection.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Inner_alias_hides_outer_alias_and_both_declarations_stay_retained()
    {
        var scopes = Chain(
            [TypeAliasImport("T", "Aliased.Space.OuterAlias", OuterAliasToken)],
            [TypeAliasImport("T", "Aliased.Space.AliasTarget", AliasTargetToken)]);
        var context = Context(scopes);
        var outcome = StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse("T.Field"), context);

        Assert.Equal(StaticFieldV2ScopedContextResultKind.Exact, context.ResultKind);
        var outerImport = Assert.Single(context.ScopeLevels[0].Imports);
        var innerImport = Assert.Single(context.ScopeLevels[1].Imports);
        Assert.True(outerImport.IsShadowed);
        Assert.False(innerImport.IsShadowed);
        Assert.Equal(OuterAliasToken, outerImport.TargetTypeDefinition!.TypeDefinitionToken);

        var shadowing = Assert.Single(context.AliasShadowings);
        Assert.Equal("T", shadowing.AliasName);
        Assert.Equal(1, shadowing.HidingScopeLevelIndex);
        Assert.Equal(0, shadowing.ShadowedScopeLevelIndex);
        Assert.Equal(innerImport, shadowing.HidingImport);
        Assert.Equal(outerImport, shadowing.ShadowedImport);
        Assert.Equal([innerImport], context.ActiveAliasProjections("T").ToArray());

        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, outcome.ResultKind);
        Assert.Equal(AliasTargetToken, outcome.SelectedCandidate!.FinalTypeDefinitionToken);
    }

    /// <summary>
    /// Proves a namespace import contributes candidates only at its own declaring level, that an inner level with a
    /// viable candidate stops the outward search before an otherwise-matching outer import is consulted, and that an
    /// empty inner level correctly proceeds outward to that same import.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void First_viable_level_stops_the_outward_search_over_an_otherwise_matching_outer_import()
    {
        var ownLevel = Bind("Thing2.Field", Chain([], [UsingNamespace("Imported.One")]));
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, ownLevel.ResultKind);
        Assert.Equal(ImportedThing2Token, ownLevel.SelectedCandidate!.FinalTypeDefinitionToken);
        Assert.Equal(
            StaticFieldV2ContextualCandidateSource.NamespaceImport,
            ownLevel.SelectedCandidate.Candidates[0].Source);
        Assert.Equal(1, ownLevel.SelectedCandidate.Candidates[0].ContributingImport!.ScopeLevelIndex);

        var outerImportScopes = Chain([UsingNamespace("Imported.One")], []);
        var suppressed = Bind("Thing.Field", outerImportScopes);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, suppressed.ResultKind);
        Assert.Equal(InnerThingToken, suppressed.SelectedCandidate!.FinalTypeDefinitionToken);
        var suppressedPartition = Assert.Single(suppressed.PartitionResults);
        Assert.Equal(0, suppressedPartition.SelectedLevelIndex);
        var suppressedLevel = Assert.Single(suppressedPartition.Levels);
        Assert.Equal("App.Inner", suppressedLevel.NamespaceName);
        Assert.Equal(
            StaticFieldV2ContextualCandidateSource.NamespaceLevelDeclaration,
            Assert.Single(suppressed.SelectedCandidate.Candidates).Source);

        var reachedOutward = Bind("Thing2.Field", outerImportScopes);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, reachedOutward.ResultKind);
        Assert.Equal(ImportedThing2Token, reachedOutward.SelectedCandidate!.FinalTypeDefinitionToken);
        var outwardPartition = Assert.Single(reachedOutward.PartitionResults);
        Assert.Equal(1, outwardPartition.SelectedLevelIndex);
        Assert.Equal(2, outwardPartition.Levels.Length);
        Assert.Equal(
            StaticFieldV2ContextualLevelDisposition.NoCandidateAtLevel,
            outwardPartition.Levels[0].Disposition);
        Assert.Equal("App", outwardPartition.Levels[1].NamespaceName);
        Assert.Equal(0, outwardPartition.Levels[1].ScopeLevelIndex);
    }

    /// <summary>
    /// Proves two same-level imports that name one identical physical type converge into exactly one draft group
    /// while retaining both contributing imports, and that two same-level imports naming distinct physical types are
    /// ambiguous no matter which physical ordinal comes first.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Same_level_imports_converge_physically_and_import_order_never_chooses()
    {
        var converged = Bind(
            "Converged.Field",
            Chain([], [UsingNamespace("Shared.Space"), UsingNamespace("Shared.Space", LibAssemblyReferenceToken)]));

        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, converged.ResultKind);
        var group = Assert.IsType<StaticFieldV2ContextualCandidateGroup>(converged.SelectedCandidate);
        Assert.Equal(LibConvergedToken, group.FinalTypeDefinitionToken);
        Assert.Equal("Synthetic.Lib", group.SourceModule.ContainingAssembly.AssemblyDefinition.Name);
        Assert.Equal(2, group.Candidates.Length);
        Assert.Equal(
            [0, 1],
            group.Candidates.Select(static candidate => candidate.ContributingImport!.Fact.Ordinal).ToArray());
        Assert.Equal(
            [1, 2],
            group.Candidates.Select(static candidate => (int)candidate.ContributingImport!.Fact.RawKind).ToArray());

        var forward = Bind(
            "Widget.Field",
            Chain([], [UsingNamespace("Imported.One"), UsingNamespace("Imported.Two")]));
        var reversed = Bind(
            "Widget.Field",
            Chain([], [UsingNamespace("Imported.Two"), UsingNamespace("Imported.One")]));

        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Ambiguous, forward.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.AmbiguousWithinLevel, forward.Issue);
        Assert.Null(forward.SelectedCandidate);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Ambiguous, reversed.ResultKind);
        Assert.Null(reversed.SelectedCandidate);
        Assert.Equal(
            [ImportedOneWidgetToken, ImportedTwoWidgetToken],
            forward.PartitionResults[0].Groups
                .Select(static candidate => candidate.FinalTypeDefinitionToken)
                .Order()
                .ToArray());
        Assert.Equal(
            forward.PartitionResults[0].Groups
                .Select(static candidate => candidate.FinalTypeDefinitionToken).Order().ToArray(),
            reversed.PartitionResults[0].Groups
                .Select(static candidate => candidate.FinalTypeDefinitionToken).Order().ToArray());
    }

    /// <summary>
    /// Proves an extern-alias-qualified name is restricted to the AssemblyRef's identity-bound target module even
    /// when another portfolio module spells the identical name, and that an unknown alias is a typed absent answer.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Extern_alias_restricts_the_following_name_and_an_unknown_alias_is_absent()
    {
        var scopes = Chain([ExternAliasImport("lib", LibAssemblyReferenceToken)], []);
        var context = Context(scopes);
        var externImport = Assert.Single(context.ScopeLevels[0].Imports);
        Assert.Equal(StaticFieldV2ScopedImportKind.ExternAlias, externImport.Kind);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.AssemblyReferenceResolved,
            externImport.TargetDisposition);
        Assert.Equal(
            LibAssemblyReferenceToken,
            externImport.AssemblyReferenceRow!.AssemblyReferenceToken);
        Assert.Equal("Synthetic.Lib", externImport.AssemblyReferenceRow.Observation.Name);

        var outcome = StaticFieldV2ScopedContextBinder.BindContextualRoute(
            Parse("lib::Shared.Widget.Field"),
            context);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Exact, outcome.ResultKind);
        var selected = Assert.IsType<StaticFieldV2ContextualCandidateGroup>(outcome.SelectedCandidate);
        Assert.Equal(LibSharedWidgetToken, selected.FinalTypeDefinitionToken);
        Assert.Equal("Synthetic.Lib", selected.SourceModule.ContainingAssembly.AssemblyDefinition.Name);
        Assert.NotEqual(AppSharedWidgetToken, selected.FinalTypeDefinitionToken);
        Assert.Equal(
            StaticFieldV2ContextualCandidateSource.ExternAlias,
            selected.Candidates[0].Source);
        Assert.Equal(1, selected.Candidates[0].NamespaceSegmentCount);

        var unknown = StaticFieldV2ScopedContextBinder.BindContextualRoute(
            Parse("nope::Shared.Widget.Field"),
            context);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Absent, unknown.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.NamedAliasAbsent, unknown.Issue);
        Assert.Null(unknown.SelectedCandidate);
        Assert.Equal(3, unknown.PartitionResults.Length);
        Assert.All(
            unknown.PartitionResults,
            static result =>
            {
                Assert.Empty(result.Groups);
                Assert.Null(result.SelectedLevelIndex);
                Assert.Equal(
                    StaticFieldV2ContextualLevelDisposition.NoCandidateAtLevel,
                    Assert.Single(result.Levels).Disposition);
            });
    }

    /// <summary>
    /// Proves a TypeSpec alias target is retained as an undecoded physical row that never becomes a bound head, and
    /// that an unsupported raw import is retained and prevents an otherwise-exhaustive absent answer from being
    /// claimed while an identical context without it answers absent.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Retained_non_exact_imports_are_never_bound_and_prevent_a_claimed_absent_answer()
    {
        var typeSpecContext = Context(
            Chain([], [TypeAliasImport("TS", "Some.Generic", TypeSpecificationToken)]));
        var typeSpecImport = Assert.Single(typeSpecContext.ScopeLevels[1].Imports);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeSpecificationNotDecoded,
            typeSpecImport.TargetDisposition);
        Assert.Null(typeSpecImport.TargetTypeDefinition);
        Assert.Equal(
            TypeSpecificationToken,
            typeSpecImport.TargetTypeSpecificationRow!.TypeSpecificationToken);
        Assert.False(typeSpecImport.IsTargetExact);
        Assert.False(typeSpecContext.IsExhaustive);
        Assert.Contains(
            StaticFieldV2ScopedContextCoverageBoundary.AliasTypeSpecTargetNotDecoded,
            typeSpecContext.DeclaredCoverageBoundaries);

        var typeSpecOutcome = StaticFieldV2ScopedContextBinder.BindContextualRoute(
            Parse("TS.Field"),
            typeSpecContext);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.NonExact, typeSpecOutcome.ResultKind);
        Assert.Equal(
            StaticFieldV2ContextualBindingIssue.AbsenceNotClaimableOverRetainedEvidence,
            typeSpecOutcome.Issue);
        Assert.Null(typeSpecOutcome.SelectedCandidate);
        Assert.Empty(typeSpecOutcome.PartitionResults);

        var unsupportedContext = Context(Chain([], [UnsupportedImport(200)]));
        var unsupportedImport = Assert.Single(unsupportedContext.ScopeLevels[1].Imports);
        Assert.Equal(StaticFieldV2ScopedImportKind.UnsupportedRaw, unsupportedImport.Kind);
        Assert.Equal(200, unsupportedImport.Fact.RawKind);
        Assert.False(unsupportedContext.IsExhaustive);
        Assert.Contains(
            StaticFieldV2ScopedContextCoverageBoundary.RetainedUnsupportedImport,
            unsupportedContext.DeclaredCoverageBoundaries);

        var blocked = StaticFieldV2ScopedContextBinder.BindContextualRoute(
            Parse("Missing.Field"),
            unsupportedContext);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.NonExact, blocked.ResultKind);
        Assert.Equal(
            StaticFieldV2ContextualBindingIssue.AbsenceNotClaimableOverRetainedEvidence,
            blocked.Issue);
        Assert.Equal(1, blocked.ObservedCount);

        var claimed = Bind("Missing.Field", Chain([], []));
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Absent, claimed.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.CandidateAbsent, claimed.Issue);
        Assert.Equal(3, Assert.Single(claimed.PartitionResults).Levels.Length);
    }

    /// <summary>
    /// Proves a retained <c>using static</c> import projects its exact owner without contributing any contextual
    /// candidate, and that every declared draft coverage boundary of this slice is emitted in ascending order.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Using_static_is_retained_exactly_and_declared_coverage_boundaries_are_emitted()
    {
        var context = Context(
            Chain([], [UsingStaticImport("Aliased.Space.AliasTarget", AliasTargetToken)]));
        var usingStatic = Assert.Single(context.ScopeLevels[1].Imports);

        Assert.Equal(StaticFieldV2ScopedImportKind.UsingStatic, usingStatic.Kind);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeDefinitionResolved,
            usingStatic.TargetDisposition);
        Assert.Equal(AliasTargetToken, usingStatic.TargetTypeDefinition!.TypeDefinitionToken);
        Assert.True(context.IsExhaustive);
        Assert.Equal(
            [
                StaticFieldV2ScopedContextCoverageBoundary.UsingStaticConsumedByLaterSlice,
                StaticFieldV2ScopedContextCoverageBoundary.MetadataGlobalRouteSelectionDeferred,
                StaticFieldV2ScopedContextCoverageBoundary.ImportScopeToNamespaceLevelPairingDeclared,
            ],
            context.DeclaredCoverageBoundaries.ToArray());
        Assert.Equal(3, context.NamespaceLevels.Length);
        Assert.Equal(
            ["App.Inner", "App", string.Empty],
            context.NamespaceLevels.Select(static level => level.NamespaceName).ToArray());
        Assert.True(context.NamespaceLevels[2].IsGlobalNamespace);
        Assert.Equal(3, context.DeclarationLevelCount);

        var outcome = StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse("AliasTarget.Field"), context);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Absent, outcome.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.CandidateAbsent, outcome.Issue);
    }

    /// <summary>
    /// Proves the declared import-scope depth and import-fact count draft bounds stop projection at cap plus one
    /// without retaining any partial scope, namespace, or shadowing evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Import_scope_depth_and_fact_count_bounds_stop_at_cap_plus_one_without_a_prefix()
    {
        const int depthCap = StaticFieldV2ScopedContextOutcome.MaximumImportScopeDepth;
        const int factCap = StaticFieldV2ScopedContextOutcome.MaximumImportFactCount;
        Assert.Equal(32, depthCap);
        Assert.Equal(128, factCap);

        var deepLevels = new ImportSpec[depthCap + 1][];
        for (var index = 0; index < deepLevels.Length; index++)
        {
            deepLevels[index] = [];
        }
        var deep = Context(Chain(deepLevels));

        Assert.Equal(StaticFieldV2ScopedContextResultKind.NonExact, deep.ResultKind);
        Assert.Equal(StaticFieldV2ScopedContextIssue.ImportScopeDepthBoundReached, deep.Issue);
        Assert.Empty(deep.ScopeLevels);
        Assert.Empty(deep.NamespaceLevels);
        Assert.Empty(deep.AliasShadowings);
        Assert.False(deep.IsExhaustive);
        Assert.Equal(depthCap + 1, deep.ObservedCount);
        var depthBound = Assert.IsType<EvaluationDeterministicBound>(deep.ReachedBound);
        Assert.Equal("expression-v2.context.import-depth", depthBound.Name);
        Assert.Equal(depthCap, depthBound.Value);

        var half = factCap / 2 + 1;
        var crowded = new ImportSpec[2][];
        for (var level = 0; level < crowded.Length; level++)
        {
            var facts = new ImportSpec[half];
            for (var index = 0; index < half; index++)
            {
                facts[index] = UsingNamespace($"Bulk{level}.N{index}");
            }
            crowded[level] = facts;
        }
        var crowdedContext = Context(Chain(crowded));

        Assert.Equal(StaticFieldV2ScopedContextResultKind.NonExact, crowdedContext.ResultKind);
        Assert.Equal(StaticFieldV2ScopedContextIssue.ImportFactCountBoundReached, crowdedContext.Issue);
        Assert.Empty(crowdedContext.ScopeLevels);
        Assert.Equal(factCap + 1, crowdedContext.ObservedCount);
        var factBound = Assert.IsType<EvaluationDeterministicBound>(crowdedContext.ReachedBound);
        Assert.Equal("expression-v2.context.import-facts", factBound.Name);
        Assert.Equal(factCap, factBound.Value);

        var propagated = StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse("T.Field"), deep);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.NonExact, propagated.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.ContextNonExact, propagated.Issue);
        Assert.Empty(propagated.PartitionResults);
        Assert.Equal(depthBound, propagated.ReachedBound);
    }

    /// <summary>
    /// Proves the explicit <c>global::</c> route is rejected from the contextual draft entry point, that a
    /// bare-member-only descriptor stops, and that every scoped-context prerequisite contradiction is typed and
    /// prefix-free.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Route_and_prerequisite_stops_are_typed_and_prefix_free()
    {
        var world = World();
        var scopes = Chain([], [TypeAliasImport("T", "Aliased.Space.AliasTarget", AliasTargetToken)]);
        var context = Context(scopes);

        var explicitRoute = StaticFieldV2ScopedContextBinder.BindContextualRoute(
            Parse("global::App.Inner.Thing.Field"),
            context);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Unsupported, explicitRoute.ResultKind);
        Assert.Equal(
            StaticFieldV2ContextualBindingIssue.ExplicitRouteNotSupported,
            explicitRoute.Issue);
        Assert.Empty(explicitRoute.PartitionResults);
        Assert.Null(explicitRoute.SelectedCandidate);

        var deepLevels = new ImportSpec[StaticFieldV2ScopedContextOutcome.MaximumImportScopeDepth + 1][];
        for (var index = 0; index < deepLevels.Length; index++)
        {
            deepLevels[index] = [];
        }
        var lazyExplicitRoute = StaticFieldV2ScopedContextBinder.BindContextualRoute(
            Parse("global::App.Inner.Thing.Field"),
            Context(Chain(deepLevels)));
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Unsupported, lazyExplicitRoute.ResultKind);
        Assert.Equal(
            StaticFieldV2ContextualBindingIssue.ExplicitRouteNotSupported,
            lazyExplicitRoute.Issue);
        Assert.Null(lazyExplicitRoute.ReachedBound);
        Assert.Equal(0, lazyExplicitRoute.ObservedCount);

        var bareOnly = StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse("Field"), context);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Unsupported, bareOnly.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.NoQualifiedOwnerPartition, bareOnly.Issue);
        Assert.Empty(bareOnly.PartitionResults);

        AssertContextStop(
            StaticFieldV2ScopedContextBinder.ProjectContext(StaticFieldV2ScopedContextRequest.Create(
                world.App.Module,
                default,
                HostType(world),
                world.Ancestry,
                world.Resolution)),
            StaticFieldV2ScopedContextResultKind.Invalid,
            StaticFieldV2ScopedContextIssue.ImportScopeChainUninitialized);

        AssertContextStop(
            StaticFieldV2ScopedContextBinder.ProjectContext(StaticFieldV2ScopedContextRequest.Create(
                W8CompilerNameMappingContractTests.CreateMetadataModule(0xEE00, 'e', "Synthetic.Elsewhere"),
                scopes,
                HostType(world),
                world.Ancestry,
                world.Resolution)),
            StaticFieldV2ScopedContextResultKind.Invalid,
            StaticFieldV2ScopedContextIssue.SelectedModuleNotInPortfolio);

        AssertContextStop(
            StaticFieldV2ScopedContextBinder.ProjectContext(StaticFieldV2ScopedContextRequest.Create(
                world.App.Module,
                scopes,
                world.Lib.ChainCatalog.ExactChainOrDefault(LibSharedWidgetToken)!.FinalTypeDefinition,
                world.Ancestry,
                world.Resolution)),
            StaticFieldV2ScopedContextResultKind.Invalid,
            StaticFieldV2ScopedContextIssue.SelectedTypeDefinitionNotIssued);

        var disconnected = ImmutableArray.Create(
            DumpPortablePdbImportScopeIdentity.Create(ScopeToken(1), null, 1, []));
        AssertContextStop(
            StaticFieldV2ScopedContextBinder.ProjectContext(StaticFieldV2ScopedContextRequest.Create(
                world.App.Module,
                disconnected,
                HostType(world),
                world.Ancestry,
                world.Resolution)),
            StaticFieldV2ScopedContextResultKind.Invalid,
            StaticFieldV2ScopedContextIssue.ImportScopeChainNotOuterToInner);

        var coreOnly = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule());
        AssertContextStop(
            StaticFieldV2ScopedContextBinder.ProjectContext(StaticFieldV2ScopedContextRequest.Create(
                world.App.Module,
                scopes,
                HostType(world),
                world.Ancestry,
                coreOnly.Resolution)),
            StaticFieldV2ScopedContextResultKind.Invalid,
            StaticFieldV2ScopedContextIssue.ResolutionPortfolioMismatch);

        var invalidAncestry = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule(),
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule(
                assemblyName: "Synthetic.Core2",
                moduleAddress: 0xDB00,
                digest: '9'));
        Assert.Equal(
            MetadataAncestryAuthorityPortfolioResultKind.Invalid,
            invalidAncestry.Ancestry.ResultKind);
        var invalidContext = StaticFieldV2ScopedContextBinder.ProjectContext(
            StaticFieldV2ScopedContextRequest.Create(
                world.App.Module,
                scopes,
                HostType(world),
                invalidAncestry.Ancestry,
                world.Resolution));
        AssertContextStop(
            invalidContext,
            StaticFieldV2ScopedContextResultKind.Invalid,
            StaticFieldV2ScopedContextIssue.AncestryPortfolioInvalid);

        var propagated = StaticFieldV2ScopedContextBinder.BindContextualRoute(
            Parse("T.Field"),
            invalidContext);
        Assert.Equal(StaticFieldV2ContextualBindingResultKind.Invalid, propagated.ResultKind);
        Assert.Equal(StaticFieldV2ContextualBindingIssue.ContextInvalid, propagated.Issue);
        Assert.Empty(propagated.PartitionResults);

        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2ScopedContextBinder.ProjectContext(null!));
        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2ScopedContextBinder.BindContextualRoute(null!, context));
        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse("T.Field"), null!));
    }

    /// <summary>
    /// Proves canonical replay equality, defensive copies, guarded private issuance for every retained draft row,
    /// the closed public issuer surface, and emitted draft XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Scoped_context_contracts_are_replayable_immutable_guarded_and_documented()
    {
        var scopes = Chain(
            [TypeAliasImport("T", "Aliased.Space.OuterAlias", OuterAliasToken)],
            [TypeAliasImport("T", "Aliased.Space.AliasTarget", AliasTargetToken)]);
        var context = Context(scopes);
        var outcome = StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse("T.Field"), context);
        var replayContext = Context(scopes);
        var replay = StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse("T.Field"), replayContext);

        Assert.Equal(context, replayContext);
        Assert.Equal(context.GetHashCode(), replayContext.GetHashCode());
        Assert.Equal(outcome, replay);
        Assert.Equal(outcome.GetHashCode(), replay.GetHashCode());
        Assert.Equal(
            "55ab9535838c4eda3c43c89bae22ba4df7d81d2d645033795f2dce7ade79b7b0",
            outcome.Sha256);

        var originalBytes = outcome.CanonicalBytes;
        var partition = outcome.PartitionResults[0];
        var level = partition.Levels[0];
        var group = partition.Groups[0];
        var candidate = group.Candidates[0];
        var scopeLevel = context.ScopeLevels[0];
        var shadowing = context.AliasShadowings[0];
        var originalGroups = partition.Groups;
        var originalCandidates = group.Candidates;
        var originalImports = scopeLevel.Imports;

        ImmutableCollectionsMarshal.AsArray(outcome.PartitionResults)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(outcome.CanonicalBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(partition.Groups)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(partition.Levels)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(group.Candidates)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(candidate.CanonicalBytes)![0] ^= 0x33;
        ImmutableCollectionsMarshal.AsArray(scopeLevel.Imports)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(context.ScopeLevels)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(context.NamespaceLevels)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(context.AliasShadowings)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(context.DeclaredCoverageBoundaries)![0] =
            StaticFieldV2ScopedContextCoverageBoundary.RetainedUnsupportedImport;

        Assert.True(originalBytes.AsSpan().SequenceEqual(outcome.CanonicalBytes.AsSpan()));
        Assert.Equal(outcome.Sha256, replay.Sha256);
        Assert.Equal(originalGroups[0], partition.Groups[0]);
        Assert.Equal(originalCandidates[0], group.Candidates[0]);
        Assert.Equal(originalImports[0], scopeLevel.Imports[0]);
        Assert.Equal(partition, outcome.PartitionResults[0]);
        Assert.Equal(scopeLevel, context.ScopeLevels[0]);
        Assert.Equal(shadowing, context.AliasShadowings[0]);
        Assert.Equal(
            StaticFieldV2ScopedContextCoverageBoundary.UsingStaticConsumedByLaterSlice,
            context.DeclaredCoverageBoundaries[0]);

        Assert.False(StaticFieldV2ScopedContextOutcome.OwnsRowMintCapability(new object()));
        Assert.False(StaticFieldV2ContextualBindingOutcome.OwnsRowMintCapability(new object()));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ScopedImportProjection.Create(
            new object(),
            originalImports[0].Fact,
            0,
            StaticFieldV2ScopedImportKind.TypeAlias,
            StaticFieldV2ScopedImportTargetDisposition.NotApplicable,
            null,
            null,
            null,
            null,
            null,
            false));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ScopedAliasShadowingIdentity.Create(
            new object(),
            shadowing.AliasName,
            shadowing.HidingImport,
            shadowing.ShadowedImport));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ScopedScopeLevelIdentity.Create(
            new object(),
            scopeLevel.LevelIndex,
            scopeLevel.Scope,
            scopeLevel.Imports));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ScopedNamespaceLevelIdentity.Create(
            new object(),
            0,
            "App.Inner"));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ContextualCandidateIdentity.Create(
            new object(),
            candidate.SourceModule,
            candidate.Chain,
            candidate.LevelIndex,
            candidate.Source,
            candidate.ContributingImport,
            candidate.NamespaceSegmentCount,
            candidate.PartitionIndex));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ContextualCandidateGroup.Create(
            new object(),
            group.SourceModule,
            group.FinalTypeDefinition,
            group.Candidates));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ContextualLevelResult.Create(
            new object(),
            level.LevelIndex,
            level.NamespaceName,
            level.HasNamespaceLevel,
            level.ScopeLevelIndex,
            level.Disposition,
            level.Candidates,
            level.ExaminedOccurrenceCount,
            level.FirstRejectionReason));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ContextualPartitionResult.Create(
            new object(),
            partition.PartitionIndex,
            partition.CandidateKind,
            partition.OwnerSegmentCount,
            partition.ResultKind,
            partition.Levels,
            partition.SelectedLevelIndex,
            partition.Groups));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2ScopedImportKind),
            typeof(StaticFieldV2ScopedImportTargetDisposition),
            typeof(StaticFieldV2ScopedContextResultKind),
            typeof(StaticFieldV2ScopedContextIssue),
            typeof(StaticFieldV2ScopedContextCoverageBoundary),
            typeof(StaticFieldV2ContextualCandidateSource),
            typeof(StaticFieldV2ContextualRejectionReason),
            typeof(StaticFieldV2ContextualLevelDisposition),
            typeof(StaticFieldV2ContextualBindingResultKind),
            typeof(StaticFieldV2ContextualBindingIssue),
            typeof(StaticFieldV2ScopedImportProjection),
            typeof(StaticFieldV2ScopedAliasShadowingIdentity),
            typeof(StaticFieldV2ScopedScopeLevelIdentity),
            typeof(StaticFieldV2ScopedNamespaceLevelIdentity),
            typeof(StaticFieldV2ScopedContextRequest),
            typeof(StaticFieldV2ScopedContextOutcome),
            typeof(StaticFieldV2ContextualCandidateIdentity),
            typeof(StaticFieldV2ContextualCandidateGroup),
            typeof(StaticFieldV2ContextualLevelResult),
            typeof(StaticFieldV2ContextualPartitionResult),
            typeof(StaticFieldV2ContextualBindingOutcome),
            typeof(StaticFieldV2ScopedContextBinder),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (type == typeof(StaticFieldV2ScopedContextBinder))
            {
                Assert.Equal(["BindContextualRoute", "ProjectContext"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2ScopedContextRequest))
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

    private static StaticFieldV2ContextualBindingOutcome Bind(
        string expressionText,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes) =>
        StaticFieldV2ScopedContextBinder.BindContextualRoute(Parse(expressionText), Context(scopes));

    private static StaticFieldV2ExpressionDescriptor Parse(string expressionText)
    {
        var parsed = StaticFieldV2ExpressionParser.Parse(expressionText);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, parsed.Status);
        return parsed.Descriptor!;
    }

    private static StaticFieldV2ScopedContextOutcome Context(
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes)
    {
        var world = World();
        return StaticFieldV2ScopedContextBinder.ProjectContext(StaticFieldV2ScopedContextRequest.Create(
            world.App.Module,
            scopes,
            HostType(world),
            world.Ancestry,
            world.Resolution));
    }

    private static MetadataTypeDefinitionAuthorityIdentity HostType(ScopedWorld world) =>
        world.App.ChainCatalog.ExactChainOrDefault(HostToken)!.FinalTypeDefinition;

    private static ScopedWorld World()
    {
        var core = W8MetadataAncestryAuthorityContractTests.BuildCoreModule();
        var lib = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.Lib",
            0xB800,
            '8',
            [
                Row("Shared", "Widget"),
                Row("Shared.Space", "Converged"),
            ]);
        var app = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.App",
            0xA700,
            '7',
            [
                Row("App.Inner", "Host"),
                Row("Aliased.Space", "AliasTarget"),
                Row("Aliased.Space", "OuterAlias"),
                Row("App.Inner", "Thing"),
                Row("Imported.One", "Thing2"),
                Row("Imported.One", "Widget"),
                Row("Imported.Two", "Widget"),
                Row("Ns.Alias.Target", "Deep"),
                Row("Shared", "Widget"),
                Row("Imported.One", "Thing"),
            ],
            assemblyReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Synthetic.Lib"),
            ],
            typeSpecifications: module =>
            [
                MetadataTypeSpecificationRowObservationIdentity.Create(
                    module,
                    TypeSpecificationToken,
                    [0x15, 0x12, 0x08, 0x01, 0x0E]),
            ]);
        var world = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(core, lib, app);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, world.Ancestry.ResultKind);
        return new ScopedWorld(core, lib, app, world.Resolution, world.Ancestry);
    }

    private static W8MetadataAncestryAuthorityContractTests.TypeRow Row(string namespaceName, string typeName) =>
        new(namespaceName, typeName, PublicClass, null);

    private static ImmutableArray<DumpPortablePdbImportScopeIdentity> Chain(params ImportSpec[][] levels)
    {
        var builder = ImmutableArray.CreateBuilder<DumpPortablePdbImportScopeIdentity>(levels.Length);
        for (var index = 0; index < levels.Length; index++)
        {
            var scopeToken = ScopeToken(index + 1);
            var facts = ImmutableArray.CreateBuilder<DumpPortablePdbImportFact>(levels[index].Length);
            for (var ordinal = 0; ordinal < levels[index].Length; ordinal++)
            {
                facts.Add(levels[index][ordinal](scopeToken, ordinal));
            }
            builder.Add(DumpPortablePdbImportScopeIdentity.Create(
                scopeToken,
                index == 0 ? null : ScopeToken(index),
                index,
                facts.MoveToImmutable()));
        }
        return builder.MoveToImmutable();
    }

    private static int ScopeToken(int rowId) => 0x3500_0000 | rowId;

    private static ImmutableArray<byte> Payload(int ordinal) => [0xAB, (byte)ordinal];

    private static ImportSpec UsingNamespace(string namespaceName, int? assemblyReferenceToken = null) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.NamespaceImport(
            scopeToken,
            ordinal,
            assemblyReferenceToken is null ? (byte)1 : (byte)2,
            namespaceName,
            Payload(ordinal),
            assemblyReferenceToken);

    private static ImportSpec TypeAliasImport(string alias, string target, int targetTypeToken) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.TypeAlias(
            scopeToken,
            ordinal,
            9,
            alias,
            target,
            targetTypeToken,
            Payload(ordinal));

    private static ImportSpec NamespaceAliasImport(string alias, string namespaceName) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.NamespaceAlias(
            scopeToken,
            ordinal,
            7,
            alias,
            namespaceName,
            Payload(ordinal));

    private static ImportSpec ExternAliasImport(string alias, int assemblyReferenceToken) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.ExternAlias(
            scopeToken,
            ordinal,
            6,
            alias,
            Payload(ordinal),
            assemblyReferenceToken);

    private static ImportSpec UsingStaticImport(string target, int targetTypeToken) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.UsingStatic(
            scopeToken,
            ordinal,
            3,
            target,
            targetTypeToken,
            Payload(ordinal));

    private static ImportSpec UnsupportedImport(byte rawKind) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.UnsupportedRaw(
            scopeToken,
            ordinal,
            rawKind,
            Payload(ordinal));

    private static void AssertContextStop(
        StaticFieldV2ScopedContextOutcome outcome,
        StaticFieldV2ScopedContextResultKind resultKind,
        StaticFieldV2ScopedContextIssue issue)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(issue, outcome.Issue);
        Assert.Empty(outcome.ScopeLevels);
        Assert.Empty(outcome.NamespaceLevels);
        Assert.Empty(outcome.AliasShadowings);
        Assert.False(outcome.IsExhaustive);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2ScopedContextBinder).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
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

    private delegate DumpPortablePdbImportFact ImportSpec(int scopeToken, int ordinal);

    private sealed record ScopedWorld(
        W8MetadataAncestryAuthorityContractTests.AncestryModule Core,
        W8MetadataAncestryAuthorityContractTests.AncestryModule Lib,
        W8MetadataAncestryAuthorityContractTests.AncestryModule App,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry);
}
