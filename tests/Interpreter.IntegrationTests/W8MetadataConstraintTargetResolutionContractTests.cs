using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises the multi-module authority-derived constraint-target draft resolution portfolio.</summary>
public sealed class W8MetadataConstraintTargetResolutionContractTests
{
    private const int OuterTypeRid = 2;
    private const int EqualTypeRid = 3;

    /// <summary>
    /// Proves TypeDef, resolved TypeRef, unresolved TypeRef, and TypeSpec constraint targets join their exact
    /// authority rows through the resolution portfolio with complete typed row facts and canonical replay.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_portfolio_joins_constraint_targets_through_authority()
    {
        var world = BuildConstraintWorld();
        var replay = BuildConstraintWorld();
        var portfolio = world.Targets;

        Assert.Equal(MetadataConstraintTargetResolutionPortfolioResultKind.Exact, portfolio.ResultKind);
        Assert.Equal(MetadataConstraintTargetResolutionPortfolioIssue.None, portfolio.Issue);
        Assert.Equal(2, portfolio.Entries.Length);
        Assert.Equal(world.Resolution.SnapshotSha256, portfolio.SnapshotSha256);

        var libAuthority = world.Resolution.Entries
            .Single(entry => entry.SourceModule.Equals(world.Lib))
            .ChainEntry.ChainCatalog.DefinitionAuthority;
        var appAuthority = world.Resolution.Entries
            .Single(entry => entry.SourceModule.Equals(world.App))
            .ChainEntry.ChainCatalog.DefinitionAuthority;

        var definitionTarget = Target(portfolio, world.App, 1);
        Assert.Equal(MetadataConstraintTargetKind.TypeDefinition, definitionTarget.Kind);
        Assert.Equal(world.App, definitionTarget.TargetModule);
        Assert.Same(appAuthority.TypeDefinitions[EqualTypeRid - 1], definitionTarget.TargetTypeDefinition);
        Assert.Equal(0x0200_0003, definitionTarget.ConstraintMetadataToken);
        Assert.Null(definitionTarget.ReferenceResolution);
        Assert.Null(definitionTarget.TypeSpecificationRow);

        var referenceTarget = Target(portfolio, world.App, 2);
        Assert.Equal(MetadataConstraintTargetKind.TypeReference, referenceTarget.Kind);
        Assert.Equal(world.Lib, referenceTarget.TargetModule);
        Assert.Same(libAuthority.TypeDefinitions[OuterTypeRid - 1], referenceTarget.TargetTypeDefinition);
        Assert.NotNull(referenceTarget.ReferenceResolution);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.Resolved,
            referenceTarget.ReferenceResolution!.Disposition);

        var specificationTarget = Target(portfolio, world.App, 3);
        Assert.Equal(MetadataConstraintTargetKind.TypeSpecification, specificationTarget.Kind);
        Assert.Null(specificationTarget.TargetModule);
        Assert.Equal(0x1B00_0001, specificationTarget.TypeSpecificationRow!.TypeSpecificationToken);

        var unresolvedTarget = Target(portfolio, world.App, 4);
        Assert.Equal(MetadataConstraintTargetKind.TypeReferenceUnresolved, unresolvedTarget.Kind);
        Assert.Null(unresolvedTarget.TargetTypeDefinition);
        Assert.Equal(
            MetadataTypeReferenceResolutionDispositionKind.TargetTypeAbsent,
            unresolvedTarget.ReferenceResolution!.Disposition);

        var appEntry = portfolio.Entries.Single(entry => entry.SourceModule.Equals(world.App));
        Assert.Equal(4, appEntry.Targets.Length);
        Assert.Same(appEntry.FindTarget(0x2C00_0001), definitionTarget);
        Assert.Null(appEntry.FindTarget(0x2C00_0005));
        Assert.Null(appEntry.FindTarget(0x0200_0001));
        Assert.Empty(portfolio.Entries.Single(entry => entry.SourceModule.Equals(world.Lib)).Targets);
        Assert.Null(portfolio.ExactTargetOrDefault(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0xEF00, 'f', "Synthetic.Otherwhere"),
            0x2C00_0001));

        Assert.Equal(portfolio, replay.Targets);
        Assert.Equal(portfolio.GetHashCode(), replay.Targets.GetHashCode());
        Assert.Equal(portfolio.Sha256, replay.Targets.Sha256);
        Assert.Equal(
            "93c6bc070c84eb3d8306012b721fdf06fee38e64c9a4120bd0e2e80cfefdfa5e",
            portfolio.Sha256);
    }

    /// <summary>
    /// Proves prerequisite, vector-shape, catalog, module-correlation, and source-lineage contradictions produce
    /// deterministic prefix-free typed draft stops.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_vector_and_lineage_stops_are_typed_and_prefix_free()
    {
        var world = BuildConstraintWorld();
        var resolution = world.Resolution;
        var catalogs = new[] { world.AppConstraints, world.LibConstraints };

        var stoppedResolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                    ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
                ImmutableArray<MetadataNamedTypeDefinitionChainCatalogIdentity>.Empty),
            ImmutableArray<MetadataModuleReferenceTableSetIdentity>.Empty);
        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(stoppedResolution, [catalogs[0]]),
            MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
            MetadataConstraintTargetResolutionPortfolioIssue.ResolutionPortfolioNonExact);

        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(resolution, default),
            MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
            MetadataConstraintTargetResolutionPortfolioIssue.CatalogVectorUninitialized);
        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(resolution, [catalogs[0]]),
            MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
            MetadataConstraintTargetResolutionPortfolioIssue.CatalogSlotsIncomplete);
        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(
                resolution,
                [catalogs[0], catalogs[1], catalogs[1]]),
            MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
            MetadataConstraintTargetResolutionPortfolioIssue.CatalogSlotCountConflict);
        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(resolution, [catalogs[0], null!]),
            MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
            MetadataConstraintTargetResolutionPortfolioIssue.CatalogMissing);
        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(resolution, [catalogs[0], catalogs[0]]),
            MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
            MetadataConstraintTargetResolutionPortfolioIssue.DuplicateSourceModule);

        var overCap = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            resolution,
            [.. Enumerable.Repeat(
                catalogs[0],
                MetadataConstraintTargetResolutionPortfolioIdentity.MaximumModuleCount + 1)]);
        AssertStop(
            overCap,
            MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
            MetadataConstraintTargetResolutionPortfolioIssue.ModuleCountBoundReached);
        Assert.Equal("expression-v2.metadata.modules", overCap.ReachedBound!.Name);

        var appScenario = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: world.App,
            typeReferenceRowCount: 2,
            typeSpecificationRowCount: 1,
            assemblyReferenceRowCount: 1,
            genericParameterConstraintRowCount: 4);
        var nonExactCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            appScenario.SourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(appScenario.DefinitionAuthority),
            default);
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact,
            nonExactCatalog.ResultKind);
        var nonExactStop = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            resolution,
            [nonExactCatalog, catalogs[1]]);
        AssertStop(
            nonExactStop,
            MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
            MetadataConstraintTargetResolutionPortfolioIssue.ConstraintCatalogNonExact);
        Assert.Equal(world.App.Sha256, nonExactStop.RelatedModuleSha256);

        var relineagedScenario = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: world.App);
        var relineagedCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            relineagedScenario.SourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(relineagedScenario.DefinitionAuthority),
            ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity>.Empty);
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            relineagedCatalog.ResultKind);
        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(
                resolution,
                [relineagedCatalog, catalogs[1]]),
            MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
            MetadataConstraintTargetResolutionPortfolioIssue.ConstraintSourceEndsMismatch);

        var strangerScenario = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: W8CompilerNameMappingContractTests.CreateMetadataModule(0xEA00, 'c', "Synthetic.Stray"));
        var strangerCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            strangerScenario.SourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(strangerScenario.DefinitionAuthority),
            ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity>.Empty);
        AssertStop(
            MetadataConstraintTargetResolutionPortfolioIdentity.Create(
                resolution,
                [strangerCatalog, catalogs[1]]),
            MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
            MetadataConstraintTargetResolutionPortfolioIssue.SourceModuleNotInPortfolio);
    }

    /// <summary>
    /// Proves defensive copies, private guarded issuance, the closed public issuer surface, and emitted draft XML
    /// documentation for the constraint-target contract family.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Constraint_target_contracts_are_immutable_guarded_and_documented()
    {
        var world = BuildConstraintWorld();
        var portfolio = world.Targets;
        var originalBytes = portfolio.CanonicalBytes;
        var originalSha = portfolio.Sha256;

        var returnedEntries = portfolio.Entries;
        ImmutableCollectionsMarshal.AsArray(returnedEntries)![0] = returnedEntries[^1];
        var returnedBytes = portfolio.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        Assert.True(originalBytes.AsSpan().SequenceEqual(portfolio.CanonicalBytes.AsSpan()));
        Assert.Equal(originalSha, portfolio.Sha256);

        var appEntry = portfolio.Entries.Single(entry => entry.SourceModule.Equals(world.App));
        var definitionTarget = appEntry.FindTarget(0x2C00_0001)!;
        Assert.Throws<ArgumentException>(() => MetadataConstraintTargetResolutionIdentity.Create(
            new object(),
            definitionTarget.ConstraintRow,
            MetadataConstraintTargetKind.TypeSpecification,
            null,
            null,
            null,
            null));
        Assert.Throws<ArgumentException>(() => MetadataConstraintTargetResolutionModuleIdentity.Create(
            new object(),
            appEntry.ConstraintCatalog,
            appEntry.ResolutionEntry,
            appEntry.Targets));
        Assert.False(MetadataConstraintTargetResolutionPortfolioIdentity.OwnsRowMintCapability(new object()));

        var publicTypes = new[]
        {
            typeof(MetadataConstraintTargetKind),
            typeof(MetadataConstraintTargetResolutionIdentity),
            typeof(MetadataConstraintTargetResolutionModuleIdentity),
            typeof(MetadataConstraintTargetResolutionPortfolioResultKind),
            typeof(MetadataConstraintTargetResolutionPortfolioIssue),
            typeof(MetadataConstraintTargetResolutionPortfolioIdentity),
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
            if (type == typeof(MetadataConstraintTargetResolutionPortfolioIdentity))
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

    private static ConstraintWorld BuildConstraintWorld()
    {
        var appModule = W8CompilerNameMappingContractTests.CreateMetadataModule(0xA300, 'a', "Synthetic.CApp");
        var libModule = W8CompilerNameMappingContractTests.CreateMetadataModule(0xB300, 'b', "Synthetic.CLib");
        var appScenario = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: appModule,
            typeReferenceRowCount: 2,
            typeSpecificationRowCount: 1,
            assemblyReferenceRowCount: 1,
            genericParameterConstraintRowCount: 4);
        var libScenario = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            module: libModule);

        var appReferenceEnds = MetadataReferenceSourceEndIdentity.Create(appScenario.SourceEnds);
        var appTables = MetadataModuleReferenceTableSetIdentity.Create(
            appReferenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
                appReferenceEnds,
                [
                    MetadataTypeReferenceRowObservationIdentity.Create(
                        appModule, 0x0100_0001, "Synthetic.Mapping", "Outer`2", 0x2300_0001),
                    MetadataTypeReferenceRowObservationIdentity.Create(
                        appModule, 0x0100_0002, "Synthetic.Mapping", "Missing", 0x2300_0001),
                ]),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                appReferenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                appReferenceEnds,
                [MetadataTypeSpecificationRowObservationIdentity.Create(
                    appModule,
                    0x1B00_0001,
                    [0x15, 0x12, 0x08, 0x01, 0x0E])]),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(
                appReferenceEnds,
                [MetadataAssemblyReferenceRowObservationIdentity.Create(
                    appModule,
                    0x2300_0001,
                    "Synthetic.CLib",
                    majorVersion: 1,
                    minorVersion: 0,
                    buildNumber: 0,
                    revisionNumber: 0,
                    culture: string.Empty,
                    flags: 0,
                    publicKeyOrToken: ImmutableArray<byte>.Empty,
                    hashValue: ImmutableArray<byte>.Empty)]),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                appReferenceEnds,
                ImmutableArray<MetadataAssemblyFileRowObservationIdentity>.Empty),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                appReferenceEnds,
                ImmutableArray<MetadataExportedTypeRowObservationIdentity>.Empty));

        var libReferenceEnds = MetadataReferenceSourceEndIdentity.Create(libScenario.SourceEnds);
        var libTables = MetadataModuleReferenceTableSetIdentity.Create(
            libReferenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
                libReferenceEnds,
                ImmutableArray<MetadataTypeReferenceRowObservationIdentity>.Empty),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                libReferenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                libReferenceEnds,
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(
                libReferenceEnds,
                ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>.Empty),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                libReferenceEnds,
                ImmutableArray<MetadataAssemblyFileRowObservationIdentity>.Empty),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                libReferenceEnds,
                ImmutableArray<MetadataExportedTypeRowObservationIdentity>.Empty));

        var appCompatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            appScenario.DefinitionAuthority,
            NullCandidateSlots(appScenario.DefinitionAuthority));
        var libCompatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            libScenario.DefinitionAuthority,
            NullCandidateSlots(libScenario.DefinitionAuthority));
        var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create([appCompatibility, libCompatibility]),
            [
                MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
                    appCompatibility,
                    MetadataCompilerNameMappingCatalogIdentity.Create(appScenario.DefinitionAuthority)),
                MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
                    libCompatibility,
                    MetadataCompilerNameMappingCatalogIdentity.Create(libScenario.DefinitionAuthority)),
            ]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, chainPortfolio.ResultKind);
        var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            chainPortfolio,
            [appTables, libTables]);
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, resolution.ResultKind);

        var appConstraints = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            appScenario.SourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(appScenario.DefinitionAuthority),
            [
                ConstraintRow(appModule, 1, ownerGenericParameterRowId: 1, constraintToken: 0x0200_0003),
                ConstraintRow(appModule, 2, ownerGenericParameterRowId: 1, constraintToken: 0x0100_0001),
                ConstraintRow(appModule, 3, ownerGenericParameterRowId: 2, constraintToken: 0x1B00_0001),
                ConstraintRow(appModule, 4, ownerGenericParameterRowId: 2, constraintToken: 0x0100_0002),
            ]);
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            appConstraints.ResultKind);
        var libConstraints = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            libScenario.SourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(libScenario.DefinitionAuthority),
            ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity>.Empty);
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            libConstraints.ResultKind);

        var targets = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            resolution,
            [appConstraints, libConstraints]);
        return new ConstraintWorld(appModule, libModule, resolution, appConstraints, libConstraints, targets);
    }

    private static MetadataGenericParameterConstraintRowObservationIdentity ConstraintRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int ownerGenericParameterRowId,
        int constraintToken) =>
        MetadataGenericParameterConstraintRowObservationIdentity.Create(
            module,
            0x2C00_0000 | rowId,
            0x2A00_0000 | ownerGenericParameterRowId,
            constraintToken);

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

    private static MetadataConstraintTargetResolutionIdentity Target(
        MetadataConstraintTargetResolutionPortfolioIdentity portfolio,
        StaticFieldMetadataModuleIdentity module,
        int constraintRowId) =>
        Assert.IsType<MetadataConstraintTargetResolutionIdentity>(
            portfolio.ExactTargetOrDefault(module, 0x2C00_0000 | constraintRowId));

    private static void AssertStop(
        MetadataConstraintTargetResolutionPortfolioIdentity result,
        MetadataConstraintTargetResolutionPortfolioResultKind resultKind,
        MetadataConstraintTargetResolutionPortfolioIssue issue)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Entries);
        Assert.Null(result.SnapshotSha256);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataConstraintTargetResolutionPortfolioIdentity).Assembly;
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

    private sealed record ConstraintWorld(
        StaticFieldMetadataModuleIdentity App,
        StaticFieldMetadataModuleIdentity Lib,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity AppConstraints,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity LibConstraints,
        MetadataConstraintTargetResolutionPortfolioIdentity Targets);
}
