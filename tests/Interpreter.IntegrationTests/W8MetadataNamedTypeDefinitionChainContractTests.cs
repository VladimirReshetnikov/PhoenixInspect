using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises authority-derived named-TypeDef draft chains and their normalized multi-module portfolio.</summary>
public sealed class W8MetadataNamedTypeDefinitionChainContractTests
{
    private const int ModuleTypeRid = 1;
    private const int OuterTypeRid = 2;
    private const int EqualTypeRid = 3;
    private const int DeltaTypeRid = 4;
    private const int UnderflowTypeRid = 5;
    private const int KeywordTypeRid = 10;
    private const int EarlierBacktickTypeRid = 11;

    /// <summary>
    /// Proves one exact direct or pointer authority yields RID-ordered outer-to-inner draft chains whose segments,
    /// parents, depths, arities, and C# spellability are all authority-derived, including non-exact mapping rows.
    /// </summary>
    /// <param name="usePointers">Whether TypeDef ownership uses complete reordered FieldPtr and MethodPtr rows.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Fast")]
    public void Exact_catalog_derives_outer_to_inner_chains_from_definition_authority(bool usePointers)
    {
        var catalog = BuildExactChainCatalog(usePointers);
        var replay = BuildExactChainCatalog(usePointers);

        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogIssue.None, catalog.Issue);
        Assert.Equal(catalog.DefinitionAuthority.TypeDefinitions.Length, catalog.Chains.Length);
        Assert.Equal(
            catalog.DefinitionAuthority.TypeDefinitions.Select(static typeDefinition =>
                typeDefinition.TypeDefinitionToken),
            catalog.Chains.Select(static chain => chain.FinalTypeDefinitionToken));
        Assert.Null(catalog.ReachedBound);
        Assert.Null(catalog.RelatedMetadataToken);

        var moduleChain = Assert.IsType<MetadataNamedTypeDefinitionChainIdentity>(catalog.ModulePseudoTypeChain);
        Assert.Same(catalog.Chains[0], catalog.ExactChainOrDefault(TypeToken(ModuleTypeRid)));
        Assert.True(moduleChain.IsModulePseudoType);
        Assert.False(moduleChain.CanAppearInCSharpNamedType);
        var moduleSegment = Assert.Single(moduleChain.Segments);
        Assert.True(moduleSegment.IsModulePseudoType);
        Assert.Equal(0, moduleSegment.NestingDepth);
        Assert.Equal("<Module>", moduleSegment.RawMetadataName);

        var outerChain = Chain(catalog, OuterTypeRid);
        var outerSegment = Assert.Single(outerChain.Segments);
        Assert.True(outerChain.CanAppearInCSharpNamedType);
        Assert.Equal(2, outerSegment.TotalGenericArity);
        Assert.Equal(2, outerSegment.IntroducedGenericArity);
        Assert.Equal("Outer`2", outerSegment.RawMetadataName);
        Assert.Equal("Outer", outerSegment.RoslynProjection!.ProjectedSimpleName);
        Assert.Null(outerSegment.EnclosingTypeDefinitionToken);

        var equalChain = Chain(catalog, EqualTypeRid);
        Assert.Equal(2, equalChain.Segments.Length);
        Assert.Equal(outerSegment.TypeDefinition, equalChain.Segments[0].TypeDefinition);
        Assert.Equal(TypeToken(OuterTypeRid), equalChain.Segments[1].EnclosingTypeDefinitionToken);
        Assert.Equal(1, equalChain.Segments[1].NestingDepth);
        Assert.Equal(0, equalChain.Segments[1].IntroducedGenericArity);
        Assert.Equal(equalChain.Segments[1], equalChain.FinalSegment);
        Assert.Equal(equalChain.FinalSegment.TypeDefinition, equalChain.FinalTypeDefinition);
        Assert.True(equalChain.CanAppearInCSharpNamedType);

        var deltaChain = Chain(catalog, DeltaTypeRid);
        Assert.Equal(2, deltaChain.Segments.Length);
        Assert.Equal(1, deltaChain.FinalSegment.IntroducedGenericArity);
        Assert.Equal(3, deltaChain.FinalSegment.TotalGenericArity);

        var underflowChain = Chain(catalog, UnderflowTypeRid);
        Assert.Equal(2, underflowChain.Segments.Length);
        Assert.Equal(
            MetadataCompilerNameMappingResultKind.NonExact,
            underflowChain.FinalSegment.CompilerNameMapping.ResultKind);
        Assert.Null(underflowChain.FinalSegment.IntroducedGenericArity);
        Assert.Null(underflowChain.FinalSegment.RoslynProjection);
        Assert.Null(underflowChain.FinalSegment.CSharpAddressability);
        Assert.False(underflowChain.FinalSegment.CanAppearInCSharpNamedType);
        Assert.False(underflowChain.CanAppearInCSharpNamedType);

        Assert.True(Chain(catalog, KeywordTypeRid).CanAppearInCSharpNamedType);
        Assert.False(Chain(catalog, EarlierBacktickTypeRid).CanAppearInCSharpNamedType);

        Assert.Null(catalog.ExactChainOrDefault(TypeToken(catalog.Chains.Length + 1)));
        Assert.Null(catalog.ExactChainOrDefault(0x0200_0000));
        Assert.Null(catalog.ExactChainOrDefault(0x0600_0001));

        Assert.Equal(catalog, replay);
        Assert.Equal(catalog.GetHashCode(), replay.GetHashCode());
        Assert.Equal(catalog.Sha256, replay.Sha256);
        Assert.True(catalog.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        if (!usePointers)
        {
            Assert.Equal(
                "4342fe8c069568fa230b253372243006361d18ad6a3ab5ec05976a05fb21e8f4",
                catalog.Sha256);
        }
    }

    /// <summary>
    /// Proves non-exact and invalid compatibility or mapping prerequisites and cross-catalog authority disagreement
    /// stop the draft chain catalog deterministically without a chain prefix.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_and_authority_stops_are_typed_and_prefix_free()
    {
        var exactAuthority = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var exactCompatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            exactAuthority,
            NullCandidateSlots(exactAuthority));
        var exactMapping = MetadataCompilerNameMappingCatalogIdentity.Create(exactAuthority);

        var nonExactCompatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            exactAuthority,
            default);
        Assert.Equal(
            MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact,
            nonExactCompatibility.ResultKind);
        AssertStop(
            MetadataNamedTypeDefinitionChainCatalogIdentity.Create(nonExactCompatibility, exactMapping),
            MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
            MetadataNamedTypeDefinitionChainCatalogIssue.CompatibilityCatalogNonExact);

        var invalidAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            invalidModuleName: true).DefinitionAuthority;
        var invalidCompatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            invalidAuthority,
            default);
        var invalidMapping = MetadataCompilerNameMappingCatalogIdentity.Create(invalidAuthority);
        AssertStop(
            MetadataNamedTypeDefinitionChainCatalogIdentity.Create(invalidCompatibility, exactMapping),
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.CompatibilityCatalogInvalid);

        var nonExactMappingAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            omitGenericParameterRows: true).DefinitionAuthority;
        var nonExactMapping = MetadataCompilerNameMappingCatalogIdentity.Create(nonExactMappingAuthority);
        AssertStop(
            MetadataNamedTypeDefinitionChainCatalogIdentity.Create(exactCompatibility, nonExactMapping),
            MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
            MetadataNamedTypeDefinitionChainCatalogIssue.CompilerMappingCatalogNonExact);
        AssertStop(
            MetadataNamedTypeDefinitionChainCatalogIdentity.Create(exactCompatibility, invalidMapping),
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.CompilerMappingCatalogInvalid);

        var otherAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule(0xB000, 'b')).DefinitionAuthority;
        var otherMapping = MetadataCompilerNameMappingCatalogIdentity.Create(otherAuthority);
        AssertStop(
            MetadataNamedTypeDefinitionChainCatalogIdentity.Create(exactCompatibility, otherMapping),
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.DefinitionAuthorityMismatch);

        Assert.Throws<ArgumentNullException>(() =>
            MetadataNamedTypeDefinitionChainCatalogIdentity.Create(null!, exactMapping));
        Assert.Throws<ArgumentNullException>(() =>
            MetadataNamedTypeDefinitionChainCatalogIdentity.Create(exactCompatibility, null!));
    }

    /// <summary>
    /// Proves a fabricated authority view cannot smuggle shape, parent, cycle, depth, or relation claims past the
    /// catalog: each contradiction has one deterministic prefix-free typed draft stop, and the authority-consistent
    /// view reproduces the public result exactly.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Fabricated_authority_views_expose_shape_missing_cycle_depth_and_relation_stops()
    {
        var authority = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var mapping = MetadataCompilerNameMappingCatalogIdentity.Create(authority);
        var publicResult = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(compatibility, mapping);
        var authorityNodes = authority.TypeDefinitions.Select(static typeDefinition =>
            new MetadataNamedTypeDefinitionChainAuthorityNode(
                typeDefinition,
                typeDefinition.EnclosingTypeDefinitionToken,
                typeDefinition.NestingDepth)).ToImmutableArray();

        var consistent = MetadataNamedTypeDefinitionChainCatalogIdentity.CreateWithAuthorityNodes(
            compatibility,
            mapping,
            authorityNodes);
        Assert.Equal(publicResult, consistent);
        Assert.Equal(publicResult.Sha256, consistent.Sha256);

        var truncated = MetadataNamedTypeDefinitionChainCatalogIdentity.CreateWithAuthorityNodes(
            compatibility,
            mapping,
            authorityNodes.RemoveAt(authorityNodes.Length - 1));
        AssertStop(
            truncated,
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.AuthorityViewShapeMismatch);

        var reordered = MetadataNamedTypeDefinitionChainCatalogIdentity.CreateWithAuthorityNodes(
            compatibility,
            mapping,
            [.. authorityNodes.SetItem(0, authorityNodes[1]).SetItem(1, authorityNodes[0])]);
        AssertStop(
            reordered,
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.AuthorityViewShapeMismatch);

        var missingParent = MetadataNamedTypeDefinitionChainCatalogIdentity.CreateWithAuthorityNodes(
            compatibility,
            mapping,
            authorityNodes.SetItem(
                OuterTypeRid - 1,
                authorityNodes[OuterTypeRid - 1] with { EnclosingTypeDefinitionToken = TypeToken(4_999) }));
        AssertStop(
            missingParent,
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionMissing);
        Assert.Equal(TypeToken(4_999), missingParent.RelatedMetadataToken);

        var cyclic = MetadataNamedTypeDefinitionChainCatalogIdentity.CreateWithAuthorityNodes(
            compatibility,
            mapping,
            authorityNodes
                .SetItem(
                    OuterTypeRid - 1,
                    authorityNodes[OuterTypeRid - 1] with { EnclosingTypeDefinitionToken = TypeToken(EqualTypeRid) })
                .SetItem(
                    EqualTypeRid - 1,
                    authorityNodes[EqualTypeRid - 1] with { EnclosingTypeDefinitionToken = TypeToken(OuterTypeRid) }));
        AssertStop(
            cyclic,
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionCycle);
        Assert.Equal(TypeToken(OuterTypeRid), cyclic.RelatedMetadataToken);
        Assert.Equal(2, cyclic.ObservedCount);

        var mismatchedDepth = MetadataNamedTypeDefinitionChainCatalogIdentity.CreateWithAuthorityNodes(
            compatibility,
            mapping,
            authorityNodes.SetItem(
                OuterTypeRid - 1,
                authorityNodes[OuterTypeRid - 1] with { NestingDepth = 5 }));
        AssertStop(
            mismatchedDepth,
            MetadataNamedTypeDefinitionChainCatalogResultKind.Invalid,
            MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionRelationMismatch);
        Assert.Equal(TypeToken(OuterTypeRid), mismatchedDepth.RelatedMetadataToken);

        var deep = BuildFlatScenario(namedTypeCount: StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 2);
        var deepNodes = deep.ChainCatalogInputNodes;
        for (var rid = 2; rid < deepNodes.Length; rid++)
        {
            deepNodes = deepNodes.SetItem(
                rid - 1,
                deepNodes[rid - 1] with { EnclosingTypeDefinitionToken = TypeToken(rid + 1) });
        }
        var depthStopped = MetadataNamedTypeDefinitionChainCatalogIdentity.CreateWithAuthorityNodes(
            deep.Compatibility,
            deep.Mapping,
            deepNodes);
        AssertStop(
            depthStopped,
            MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
            MetadataNamedTypeDefinitionChainCatalogIssue.EnclosingTypeDefinitionDepthBoundReached);
        var bound = Assert.IsType<EvaluationDeterministicBound>(depthStopped.ReachedBound);
        Assert.Equal("expression-v2.metadata.typedef-depth", bound.Name);
        Assert.Equal(StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth, bound.Value);
        Assert.Equal(StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth + 1, depthStopped.ObservedCount);
        Assert.Equal(TypeToken(2), depthStopped.RelatedMetadataToken);
    }

    /// <summary>
    /// Proves the normalized multi-module draft portfolio discards caller order, keys exact lookup by module and
    /// token, keeps default and explicit-empty vectors distinct, and stops deterministically for prerequisite,
    /// vector-shape, module-mismatch, and lineage contradictions.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Portfolio_normalizes_modules_and_stops_deterministically()
    {
        var moduleSets = BuildThreeModuleChainSet();
        var compatibilityPortfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [moduleSets[0].Compatibility, moduleSets[1].Compatibility, moduleSets[2].Compatibility]);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.Exact, compatibilityPortfolio.ResultKind);

        var portfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [moduleSets[2].ChainCatalog, moduleSets[0].ChainCatalog, moduleSets[1].ChainCatalog]);
        var replay = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [moduleSets[1].ChainCatalog, moduleSets[2].ChainCatalog, moduleSets[0].ChainCatalog]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, portfolio.ResultKind);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioIssue.None, portfolio.Issue);
        Assert.Equal(3, portfolio.Entries.Length);
        Assert.Equal(compatibilityPortfolio.SnapshotSha256, portfolio.SnapshotSha256);
        Assert.Equal(
            compatibilityPortfolio.Entries.Select(static entry => entry.SourceModule),
            portfolio.Entries.Select(static entry => entry.SourceModule));
        Assert.Equal(portfolio, replay);
        Assert.Equal(portfolio.Sha256, replay.Sha256);

        foreach (var moduleSet in moduleSets)
        {
            var sourceModule = moduleSet.ChainCatalog.SourceEnds.SourceModule;
            var chain = Assert.IsType<MetadataNamedTypeDefinitionChainIdentity>(
                portfolio.ExactChainOrDefault(sourceModule, TypeToken(EqualTypeRid)));
            Assert.Equal(2, chain.Segments.Length);
            Assert.Equal(sourceModule, chain.SourceEnds.SourceModule);
        }
        Assert.Null(portfolio.ExactChainOrDefault(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0xD000, 'd'),
            TypeToken(EqualTypeRid)));

        var uninitialized = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            default);
        AssertPortfolioStop(
            uninitialized,
            MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
            MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogVectorUninitialized);

        var emptyCompatibilityPortfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty);
        var emptyPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            emptyCompatibilityPortfolio,
            ImmutableArray<MetadataNamedTypeDefinitionChainCatalogIdentity>.Empty);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, emptyPortfolio.ResultKind);
        Assert.Empty(emptyPortfolio.Entries);
        Assert.Null(emptyPortfolio.SnapshotSha256);
        Assert.NotEqual(uninitialized.Sha256, emptyPortfolio.Sha256);

        var overCap = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [.. Enumerable.Repeat(
                moduleSets[0].ChainCatalog,
                MetadataNamedTypeDefinitionChainPortfolioIdentity.MaximumModuleCount + 1)]);
        AssertPortfolioStop(
            overCap,
            MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
            MetadataNamedTypeDefinitionChainPortfolioIssue.ModuleCountBoundReached);
        var moduleBound = Assert.IsType<EvaluationDeterministicBound>(overCap.ReachedBound);
        Assert.Equal("expression-v2.metadata.modules", moduleBound.Name);
        Assert.Equal(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.MaximumModuleCount + 1,
            overCap.ObservedCount);

        AssertPortfolioStop(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                compatibilityPortfolio,
                [moduleSets[0].ChainCatalog, moduleSets[1].ChainCatalog]),
            MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
            MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogSlotsIncomplete);
        AssertPortfolioStop(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                compatibilityPortfolio,
                [moduleSets[0].ChainCatalog, moduleSets[1].ChainCatalog, moduleSets[2].ChainCatalog,
                 moduleSets[2].ChainCatalog]),
            MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
            MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogSlotCountConflict);
        AssertPortfolioStop(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                compatibilityPortfolio,
                [moduleSets[0].ChainCatalog, null!, moduleSets[2].ChainCatalog]),
            MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
            MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogMissing);

        var nonExactChainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
                moduleSets[0].ChainCatalog.DefinitionAuthority,
                default),
            moduleSets[0].Mapping);
        Assert.Equal(
            MetadataNamedTypeDefinitionChainCatalogResultKind.NonExact,
            nonExactChainCatalog.ResultKind);
        var nonExactStop = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [nonExactChainCatalog, moduleSets[1].ChainCatalog, moduleSets[2].ChainCatalog]);
        AssertPortfolioStop(
            nonExactStop,
            MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
            MetadataNamedTypeDefinitionChainPortfolioIssue.CatalogNonExact);
        Assert.Equal(
            nonExactChainCatalog.SourceEnds.SourceModule.Sha256,
            nonExactStop.RelatedModuleSha256);

        var duplicate = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [moduleSets[0].ChainCatalog, moduleSets[0].ChainCatalog, moduleSets[2].ChainCatalog]);
        AssertPortfolioStop(
            duplicate,
            MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
            MetadataNamedTypeDefinitionChainPortfolioIssue.DuplicateSourceModule);
        Assert.Equal(
            moduleSets[0].ChainCatalog.SourceEnds.SourceModule.Sha256,
            duplicate.RelatedModuleSha256);

        var strangerSet = BuildModuleChainSet(0xE000, 'e');
        AssertPortfolioStop(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                compatibilityPortfolio,
                [moduleSets[0].ChainCatalog, moduleSets[1].ChainCatalog, strangerSet.ChainCatalog]),
            MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
            MetadataNamedTypeDefinitionChainPortfolioIssue.SourceModuleNotInPortfolio);

        var relineagedCompatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            moduleSets[0].ChainCatalog.DefinitionAuthority,
            NullCandidateSlots(moduleSets[0].ChainCatalog.DefinitionAuthority)
                .SetItem(OuterTypeRid - 1, Candidate(
                    moduleSets[0].ChainCatalog.DefinitionAuthority.TypeDefinitions[OuterTypeRid - 1])));
        var relineagedChainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            relineagedCompatibility,
            moduleSets[0].Mapping);
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, relineagedChainCatalog.ResultKind);
        AssertPortfolioStop(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                compatibilityPortfolio,
                [relineagedChainCatalog, moduleSets[1].ChainCatalog, moduleSets[2].ChainCatalog]),
            MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid,
            MetadataNamedTypeDefinitionChainPortfolioIssue.CompatibilityCatalogMismatch);

        var stoppedCompatibilityPortfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!));
        AssertPortfolioStop(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                stoppedCompatibilityPortfolio,
                [moduleSets[0].ChainCatalog]),
            MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact,
            MetadataNamedTypeDefinitionChainPortfolioIssue.CompatibilityPortfolioNonExact);
        Assert.Equal(
            "8515e2c4f27495bd871af3562979fe767abb1a39a7248c4f7ed31913835ed13e",
            portfolio.Sha256);
    }

    /// <summary>
    /// Proves defensive copies, private segment, chain, and entry issuance, the closed public issuer surface, and
    /// emitted draft XML documentation for the named-TypeDef chain contract family.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Chain_contracts_are_immutable_guarded_and_documented()
    {
        var catalog = BuildExactChainCatalog(usePointers: false);
        var originalBytes = catalog.CanonicalBytes;
        var originalSha = catalog.Sha256;
        var equalChain = Chain(catalog, EqualTypeRid);
        var originalSegments = equalChain.Segments;

        var returnedChains = catalog.Chains;
        ImmutableCollectionsMarshal.AsArray(returnedChains)![0] = returnedChains[^1];
        var returnedBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        var returnedSegments = equalChain.Segments;
        ImmutableCollectionsMarshal.AsArray(returnedSegments)![0] = returnedSegments[^1];

        Assert.True(originalBytes.AsSpan().SequenceEqual(catalog.CanonicalBytes.AsSpan()));
        Assert.Equal(originalSha, catalog.Sha256);
        Assert.Equal(TypeToken(ModuleTypeRid), catalog.Chains[0].FinalTypeDefinitionToken);
        Assert.Equal(originalSegments[0], equalChain.Segments[0]);
        Assert.Equal(TypeToken(OuterTypeRid), equalChain.Segments[0].TypeDefinitionToken);

        var outerSegment = Chain(catalog, OuterTypeRid).FinalSegment;
        Assert.Throws<ArgumentException>(() => MetadataNamedTypeDefinitionChainSegmentIdentity.Create(
            new object(),
            outerSegment.TypeDefinition,
            outerSegment.CompilerNameMapping));
        Assert.Throws<ArgumentException>(() => MetadataNamedTypeDefinitionChainIdentity.Create(
            new object(),
            equalChain.Segments));
        Assert.Throws<ArgumentException>(() => MetadataNamedTypeDefinitionChainPortfolioEntryIdentity.Create(
            new object(),
            BuildPortfolioEntryInputs().CompatibilityEntry,
            BuildPortfolioEntryInputs().ChainCatalog));
        Assert.False(MetadataNamedTypeDefinitionChainCatalogIdentity.OwnsSegmentMintCapability(new object()));
        Assert.False(MetadataNamedTypeDefinitionChainCatalogIdentity.OwnsChainMintCapability(new object()));
        Assert.False(MetadataNamedTypeDefinitionChainPortfolioIdentity.OwnsEntryMintCapability(new object()));

        var publicTypes = new[]
        {
            typeof(MetadataNamedTypeDefinitionChainCatalogResultKind),
            typeof(MetadataNamedTypeDefinitionChainCatalogIssue),
            typeof(MetadataNamedTypeDefinitionChainSegmentIdentity),
            typeof(MetadataNamedTypeDefinitionChainIdentity),
            typeof(MetadataNamedTypeDefinitionChainCatalogIdentity),
            typeof(MetadataNamedTypeDefinitionChainPortfolioResultKind),
            typeof(MetadataNamedTypeDefinitionChainPortfolioIssue),
            typeof(MetadataNamedTypeDefinitionChainPortfolioEntryIdentity),
            typeof(MetadataNamedTypeDefinitionChainPortfolioIdentity),
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
            if (type == typeof(MetadataNamedTypeDefinitionChainCatalogIdentity) ||
                type == typeof(MetadataNamedTypeDefinitionChainPortfolioIdentity))
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

    private static MetadataNamedTypeDefinitionChainCatalogIdentity BuildExactChainCatalog(bool usePointers)
    {
        var authority = W8CompilerNameMappingContractTests.BuildScenario(usePointers).DefinitionAuthority;
        return MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(authority, NullCandidateSlots(authority)),
            MetadataCompilerNameMappingCatalogIdentity.Create(authority));
    }

    private static ModuleChainSet[] BuildThreeModuleChainSet() =>
    [
        BuildModuleChainSet(0xA000, 'a'),
        BuildModuleChainSet(0xB000, 'b'),
        BuildModuleChainSet(0xC000, 'c'),
    ];

    private static ModuleChainSet BuildModuleChainSet(ulong moduleAddress, char digestCharacter)
    {
        var authority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule(moduleAddress, digestCharacter))
            .DefinitionAuthority;
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var mapping = MetadataCompilerNameMappingCatalogIdentity.Create(authority);
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(compatibility, mapping);
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);
        return new ModuleChainSet(compatibility, mapping, chainCatalog);
    }

    private static FlatScenario BuildFlatScenario(int namedTypeCount)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(0x9000, '9');
        var totalTypeCount = namedTypeCount + 1;
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: totalTypeCount,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: totalTypeCount,
                fieldDefinitionRowCount: 0));
        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(totalTypeCount);
        for (var rowId = 1; rowId <= totalTypeCount; rowId++)
        {
            typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                metadataModule: module,
                typeDefinitionToken: TypeToken(rowId),
                fieldListRowId: 1,
                methodListRowId: 1,
                namespaceName: rowId == ModuleTypeRid ? string.Empty : "Synthetic.Flat",
                typeName: rowId == ModuleTypeRid ? "<Module>" : $"Flat{rowId}",
                typeAttributes: (int)(rowId == ModuleTypeRid ? TypeAttributes.NotPublic : TypeAttributes.Public),
                extendsMetadataToken: null));
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
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            ImmutableArray<MetadataMethodDefinitionRowObservationIdentity>.Empty);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var mapping = MetadataCompilerNameMappingCatalogIdentity.Create(authority);
        var nodes = authority.TypeDefinitions.Select(static typeDefinition =>
            new MetadataNamedTypeDefinitionChainAuthorityNode(
                typeDefinition,
                typeDefinition.EnclosingTypeDefinitionToken,
                typeDefinition.NestingDepth)).ToImmutableArray();
        return new FlatScenario(compatibility, mapping, nodes);
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

    private static StaticFieldTypeDefinitionIdentity Candidate(
        MetadataTypeDefinitionAuthorityIdentity authority)
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
            authority.TypeName,
            row.Observation.TypeAttributes,
            authority.TotalGenericArity,
            authority.TotalGenericArity,
            row.Observation.ExtendsMetadataToken,
            enclosingType: null);
    }

    private static PortfolioEntryInputs BuildPortfolioEntryInputs()
    {
        var moduleSet = BuildModuleChainSet(0xA000, 'a');
        var compatibilityPortfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [moduleSet.Compatibility]);
        return new PortfolioEntryInputs(compatibilityPortfolio.Entries[0], moduleSet.ChainCatalog);
    }

    private static MetadataNamedTypeDefinitionChainIdentity Chain(
        MetadataNamedTypeDefinitionChainCatalogIdentity catalog,
        int typeDefinitionRowId) =>
        Assert.IsType<MetadataNamedTypeDefinitionChainIdentity>(
            catalog.ExactChainOrDefault(TypeToken(typeDefinitionRowId)));

    private static void AssertStop(
        MetadataNamedTypeDefinitionChainCatalogIdentity result,
        MetadataNamedTypeDefinitionChainCatalogResultKind resultKind,
        MetadataNamedTypeDefinitionChainCatalogIssue issue)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Chains);
        Assert.Null(result.ModulePseudoTypeChain);
        Assert.Null(result.ExactChainOrDefault(TypeToken(ModuleTypeRid)));
    }

    private static void AssertPortfolioStop(
        MetadataNamedTypeDefinitionChainPortfolioIdentity result,
        MetadataNamedTypeDefinitionChainPortfolioResultKind resultKind,
        MetadataNamedTypeDefinitionChainPortfolioIssue issue)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Entries);
        Assert.Null(result.SnapshotSha256);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataNamedTypeDefinitionChainCatalogIdentity).Assembly;
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

    private static int TypeToken(int rowId) => 0x0200_0000 | rowId;

    private sealed record ModuleChainSet(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataCompilerNameMappingCatalogIdentity Mapping,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog);

    private sealed record FlatScenario(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataCompilerNameMappingCatalogIdentity Mapping,
        ImmutableArray<MetadataNamedTypeDefinitionChainAuthorityNode> ChainCatalogInputNodes);

    private sealed record PortfolioEntryInputs(
        MetadataDefinitionCompatibilityPortfolioEntryIdentity CompatibilityEntry,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog);
}
