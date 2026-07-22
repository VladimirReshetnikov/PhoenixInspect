using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises core-role selection, immediate-base edges, semantic classification, and bounded ancestry.</summary>
public sealed class W8MetadataAncestryAuthorityContractTests
{
    private const int InterfaceAttributes = 0x0000_00A1;
    private const int PublicClassAttributes = 0x0000_0001;

    /// <summary>
    /// Proves the exact portfolio selects the five core roles physically, derives every immediate-base edge form,
    /// classifies value-type, enum, delegate, interface, class, and module rows only from exact immediate base-role
    /// edges, rejects indirect role derivation, and walks bounded cross-module ancestry chains.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_portfolio_selects_roles_and_classifies_only_from_immediate_edges()
    {
        var world = BuildRoleWorld();
        var replay = BuildRoleWorld();
        var portfolio = world.Ancestry;

        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, portfolio.ResultKind);
        Assert.Equal(MetadataAncestryAuthorityPortfolioIssue.None, portfolio.Issue);
        Assert.Equal(2, portfolio.Entries.Length);
        Assert.Equal(world.Resolution.SnapshotSha256, portfolio.SnapshotSha256);

        var coreRoles = Assert.IsType<MetadataCoreRoleSelectionIdentity>(portfolio.CoreRoles);
        Assert.Equal(world.Core, coreRoles.CoreModule);
        Assert.Equal("Object", coreRoles.SystemObject.TypeName);
        Assert.Equal("ValueType", coreRoles.SystemValueType.TypeName);
        Assert.Equal("Enum", coreRoles.SystemEnum.TypeName);
        Assert.Equal("Delegate", coreRoles.SystemDelegate.TypeName);
        Assert.Equal("MulticastDelegate", coreRoles.SystemMulticastDelegate.TypeName);
        Assert.True(coreRoles.IsRoleDefinition(world.Core, coreRoles.SystemEnum));
        Assert.False(coreRoles.IsRoleDefinition(world.App, coreRoles.SystemEnum));

        AssertRole(portfolio, world.Core, 1, MetadataTypeDefinitionSemanticRole.ModulePseudoType);
        AssertRole(portfolio, world.Core, 2, MetadataTypeDefinitionSemanticRole.Class);
        AssertRole(portfolio, world.Core, 3, MetadataTypeDefinitionSemanticRole.Class);
        AssertRole(portfolio, world.Core, 4, MetadataTypeDefinitionSemanticRole.Class);
        AssertRole(portfolio, world.Core, 5, MetadataTypeDefinitionSemanticRole.Class);
        AssertRole(portfolio, world.Core, 6, MetadataTypeDefinitionSemanticRole.Class);
        AssertRole(portfolio, world.Core, 7, MetadataTypeDefinitionSemanticRole.Class);

        var widget = AssertRole(portfolio, world.App, 2, MetadataTypeDefinitionSemanticRole.Class);
        Assert.Equal(MetadataImmediateBaseEdgeKind.TypeReference, widget.BaseEdge.Kind);
        Assert.Equal(world.Core, widget.BaseEdge.TargetModule);
        Assert.Equal(coreRoles.SystemObject, widget.BaseEdge.TargetTypeDefinition);
        Assert.NotNull(widget.BaseEdge.ReferenceResolution);

        AssertRole(portfolio, world.App, 3, MetadataTypeDefinitionSemanticRole.ValueType);
        AssertRole(portfolio, world.App, 4, MetadataTypeDefinitionSemanticRole.Enum);
        AssertRole(portfolio, world.App, 5, MetadataTypeDefinitionSemanticRole.Delegate);
        AssertRole(portfolio, world.App, 6, MetadataTypeDefinitionSemanticRole.Interface);

        var broken = Classification(portfolio, world.App, 7);
        Assert.Null(broken.Role);
        Assert.Equal(MetadataSemanticClassificationIssue.BaseEdgeMissing, broken.Issue);
        var phantom = Classification(portfolio, world.App, 8);
        Assert.Null(phantom.Role);
        Assert.Equal(MetadataSemanticClassificationIssue.BaseReferenceUnresolved, phantom.Issue);
        Assert.Equal(
            MetadataImmediateBaseEdgeKind.TypeReferenceUnresolved,
            phantom.BaseEdge.Kind);

        var genericDerived = AssertRole(portfolio, world.App, 9, MetadataTypeDefinitionSemanticRole.Class);
        Assert.Equal(MetadataImmediateBaseEdgeKind.TypeSpecification, genericDerived.BaseEdge.Kind);
        Assert.NotNull(genericDerived.BaseEdge.GenericBaseRow);

        var badInterface = Classification(portfolio, world.App, 10);
        Assert.Null(badInterface.Role);
        Assert.Equal(MetadataSemanticClassificationIssue.InterfaceExtendsInvalid, badInterface.Issue);

        var indirect = AssertRole(portfolio, world.App, 11, MetadataTypeDefinitionSemanticRole.Class);
        Assert.Equal(MetadataImmediateBaseEdgeKind.TypeDefinition, indirect.BaseEdge.Kind);
        Assert.Equal(0x0200_0003, indirect.BaseEdge.TargetTypeDefinition!.TypeDefinitionToken);

        var widgetChain = Assert.IsType<MetadataTypeDefinitionAncestryChainIdentity>(
            portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0002));
        Assert.Equal(MetadataAncestryChainTerminalKind.SystemObjectReached, widgetChain.TerminalKind);
        var widgetHop = Assert.Single(widgetChain.Edges);
        Assert.Equal(coreRoles.SystemObject, widgetHop.Owner);

        var enumChain = Assert.IsType<MetadataTypeDefinitionAncestryChainIdentity>(
            portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0004));
        Assert.Equal(MetadataAncestryChainTerminalKind.SystemObjectReached, enumChain.TerminalKind);
        Assert.Equal(3, enumChain.Edges.Length);
        Assert.Equal(coreRoles.SystemEnum, enumChain.Edges[0].Owner);
        Assert.Equal(coreRoles.SystemValueType, enumChain.Edges[1].Owner);
        Assert.Equal(coreRoles.SystemObject, enumChain.Edges[2].Owner);

        var interfaceChain = Assert.IsType<MetadataTypeDefinitionAncestryChainIdentity>(
            portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0006));
        Assert.Equal(MetadataAncestryChainTerminalKind.InterfaceRoot, interfaceChain.TerminalKind);
        Assert.Empty(interfaceChain.Edges);
        Assert.Equal(
            MetadataAncestryChainTerminalKind.ModulePseudoTypeRoot,
            portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0001)!.TerminalKind);
        Assert.Equal(
            MetadataAncestryChainTerminalKind.MissingBaseInvalid,
            portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0007)!.TerminalKind);
        Assert.Equal(
            MetadataAncestryChainTerminalKind.UnresolvedReferenceReached,
            portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0008)!.TerminalKind);
        Assert.Equal(
            MetadataAncestryChainTerminalKind.GenericBaseReached,
            portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0009)!.TerminalKind);

        var selfLoop = portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_000C)!;
        Assert.Equal(MetadataAncestryChainTerminalKind.CycleDetected, selfLoop.TerminalKind);
        Assert.Empty(selfLoop.Edges);
        var cycleA = portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_000D)!;
        Assert.Equal(MetadataAncestryChainTerminalKind.CycleDetected, cycleA.TerminalKind);
        Assert.Single(cycleA.Edges);

        Assert.Null(portfolio.ExactClassificationOrDefault(world.App, 0x0200_00FF));
        Assert.Null(portfolio.ExactBaseEdgeOrDefault(world.App, 0x0600_0001));
        Assert.Null(portfolio.ExactAncestryChainOrDefault(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0xEE00, 'e', "Synthetic.Elsewhere"),
            0x0200_0002));

        Assert.Equal(portfolio, replay.Ancestry);
        Assert.Equal(portfolio.GetHashCode(), replay.Ancestry.GetHashCode());
        Assert.Equal(portfolio.Sha256, replay.Ancestry.Sha256);
        Assert.Equal(
            "df3e9517bac4095c2c91063adfe70b0143843fea31ff6c8ca27e00dedade4a08",
            portfolio.Sha256);
    }

    /// <summary>
    /// Proves the shared sixty-four-hop ancestry bound is exact at the cap and typed at cap plus one across one
    /// generated deep single-module chain ending in the cross-module core System.Object.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ancestry_depth_bound_is_exact_at_cap_and_typed_at_cap_plus_one()
    {
        const int depthCap = MetadataAncestryAuthorityPortfolioIdentity.MaximumAncestryDepth;
        var core = BuildCoreModule();
        var deepTypes = ImmutableArray.CreateBuilder<TypeRow>(depthCap + 1);
        for (var index = 1; index <= depthCap + 1; index++)
        {
            deepTypes.Add(new TypeRow(
                "Synthetic.Deep",
                $"Deep{index}",
                PublicClassAttributes,
                index == depthCap + 1 ? 0x0100_0001 : 0x0200_0000 | index + 2));
        }
        var deep = BuildCustomModule(
            "Synthetic.Deep",
            0xDD00,
            '7',
            deepTypes.ToImmutable(),
            typeReferences: module => [TypeReferenceRow(module, 1, "System", "Object", 0x2300_0001)],
            assemblyReferences: module => [AssemblyReferenceRow(module, 1, "Synthetic.Core")]);
        var world = BuildAncestryWorld(core, deep);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, world.Ancestry.ResultKind);

        var atCap = world.Ancestry.ExactAncestryChainOrDefault(deep.Module, 0x0200_0003)!;
        Assert.Equal(MetadataAncestryChainTerminalKind.SystemObjectReached, atCap.TerminalKind);
        Assert.Equal(depthCap, atCap.Edges.Length);
        Assert.Null(atCap.ReachedBound);

        var overCap = world.Ancestry.ExactAncestryChainOrDefault(deep.Module, 0x0200_0002)!;
        Assert.Equal(MetadataAncestryChainTerminalKind.DepthBoundReached, overCap.TerminalKind);
        Assert.Equal(depthCap, overCap.Edges.Length);
        var bound = Assert.IsType<EvaluationDeterministicBound>(overCap.ReachedBound);
        Assert.Equal("expression-v2.metadata.ancestry-depth", bound.Name);
        Assert.Equal(depthCap, bound.Value);
    }

    /// <summary>
    /// Proves prerequisite and core-role selection contradictions produce deterministic prefix-free typed stops:
    /// non-exact resolution input, absent core module, ambiguous core module, absent role definition, and a role
    /// definition whose immediate base resolves to the wrong parent.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_and_core_role_stops_are_typed_and_prefix_free()
    {
        var core = BuildCoreModule();
        var emptyChainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
            [core.ChainCatalog]);
        var nonExactResolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            emptyChainPortfolio,
            [core.Tables]);
        AssertStop(
            MetadataAncestryAuthorityPortfolioIdentity.Create(nonExactResolution),
            MetadataAncestryAuthorityPortfolioResultKind.NonExact,
            MetadataAncestryAuthorityPortfolioIssue.ResolutionPortfolioNonExact);

        var plain = BuildCustomModule(
            "Synthetic.NoCore",
            0xDA00,
            '8',
            [new TypeRow("Synthetic.NoCore", "Loner", PublicClassAttributes, null)]);
        var noCore = BuildAncestryWorld(plain);
        AssertStop(
            noCore.Ancestry,
            MetadataAncestryAuthorityPortfolioResultKind.NonExact,
            MetadataAncestryAuthorityPortfolioIssue.CoreModuleAbsent);

        var otherCore = BuildCoreModule(assemblyName: "Synthetic.Core2", moduleAddress: 0xDB00, digest: '9');
        var twoCores = BuildAncestryWorld(BuildCoreModule(), otherCore);
        AssertStop(
            twoCores.Ancestry,
            MetadataAncestryAuthorityPortfolioResultKind.Invalid,
            MetadataAncestryAuthorityPortfolioIssue.CoreModuleAmbiguous);

        var withoutEnum = BuildCustomModule(
            "Synthetic.Core",
            0xDC00,
            '6',
            [
                new TypeRow("System", "Object", PublicClassAttributes, null),
                new TypeRow("System", "ValueType", PublicClassAttributes, 0x0200_0002),
                new TypeRow("System", "Delegate", PublicClassAttributes, 0x0200_0002),
                new TypeRow("System", "MulticastDelegate", PublicClassAttributes, 0x0200_0004),
            ]);
        var missingRole = BuildAncestryWorld(withoutEnum);
        AssertStop(
            missingRole.Ancestry,
            MetadataAncestryAuthorityPortfolioResultKind.NonExact,
            MetadataAncestryAuthorityPortfolioIssue.RoleDefinitionAbsent);
        Assert.Equal(withoutEnum.Module.Sha256, missingRole.Ancestry.RelatedModuleSha256);

        var misparented = BuildCustomModule(
            "Synthetic.Core",
            0xDE00,
            '5',
            [
                new TypeRow("System", "Object", PublicClassAttributes, null),
                new TypeRow("System", "Anchor", PublicClassAttributes, 0x0200_0002),
                new TypeRow("System", "ValueType", PublicClassAttributes, 0x0200_0003),
                new TypeRow("System", "Enum", PublicClassAttributes, 0x0200_0004),
                new TypeRow("System", "Delegate", PublicClassAttributes, 0x0200_0002),
                new TypeRow("System", "MulticastDelegate", PublicClassAttributes, 0x0200_0006),
            ]);
        var wrongParent = BuildAncestryWorld(misparented);
        AssertStop(
            wrongParent.Ancestry,
            MetadataAncestryAuthorityPortfolioResultKind.Invalid,
            MetadataAncestryAuthorityPortfolioIssue.RoleBaseEdgeMismatch);
    }

    /// <summary>
    /// Proves defensive copies, private guarded issuance for every ancestry row family, the closed public issuer
    /// surface, and emitted draft XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ancestry_contracts_are_immutable_guarded_and_documented()
    {
        var world = BuildRoleWorld();
        var portfolio = world.Ancestry;
        var originalBytes = portfolio.CanonicalBytes;
        var originalSha = portfolio.Sha256;
        var enumChain = portfolio.ExactAncestryChainOrDefault(world.App, 0x0200_0004)!;
        var originalEdges = enumChain.Edges;

        var returnedEntries = portfolio.Entries;
        ImmutableCollectionsMarshal.AsArray(returnedEntries)![0] = returnedEntries[^1];
        var returnedBytes = portfolio.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        var returnedEdges = enumChain.Edges;
        ImmutableCollectionsMarshal.AsArray(returnedEdges)![0] = returnedEdges[^1];

        Assert.True(originalBytes.AsSpan().SequenceEqual(portfolio.CanonicalBytes.AsSpan()));
        Assert.Equal(originalSha, portfolio.Sha256);
        Assert.Equal(originalEdges[0], enumChain.Edges[0]);

        var entry = portfolio.Entries.Single(candidate => candidate.SourceModule.Equals(world.App));
        var widgetEdge = entry.FindBaseEdge(0x0200_0002)!;
        var widgetClassification = entry.FindClassification(0x0200_0002)!;
        Assert.Throws<ArgumentException>(() => MetadataImmediateBaseEdgeAuthorityIdentity.Create(
            new object(),
            world.App,
            widgetEdge.Owner,
            MetadataImmediateBaseEdgeKind.None,
            null,
            null,
            null,
            null));
        Assert.Throws<ArgumentException>(() => MetadataCoreRoleSelectionIdentity.Create(
            new object(),
            world.Core,
            portfolio.CoreRoles!.SystemObject,
            portfolio.CoreRoles.SystemValueType,
            portfolio.CoreRoles.SystemEnum,
            portfolio.CoreRoles.SystemDelegate,
            portfolio.CoreRoles.SystemMulticastDelegate));
        Assert.Throws<ArgumentException>(() => MetadataTypeDefinitionSemanticClassificationIdentity.Create(
            new object(),
            widgetEdge,
            MetadataTypeDefinitionSemanticRole.Class,
            MetadataSemanticClassificationIssue.None));
        Assert.Throws<ArgumentException>(() => MetadataTypeDefinitionAncestryChainIdentity.Create(
            new object(),
            widgetEdge,
            [],
            MetadataAncestryChainTerminalKind.SystemObjectReached,
            null,
            null));
        Assert.Throws<ArgumentException>(() => MetadataAncestryAuthorityModuleIdentity.Create(
            new object(),
            entry.ResolutionEntry,
            entry.BaseEdges,
            entry.Classifications,
            entry.AncestryChains));
        Assert.False(MetadataAncestryAuthorityPortfolioIdentity.OwnsRowMintCapability(new object()));
        Assert.NotNull(widgetClassification);

        var publicTypes = new[]
        {
            typeof(MetadataImmediateBaseEdgeKind),
            typeof(MetadataImmediateBaseEdgeAuthorityIdentity),
            typeof(MetadataCoreRoleSelectionIssue),
            typeof(MetadataCoreRoleSelectionIdentity),
            typeof(MetadataTypeDefinitionSemanticRole),
            typeof(MetadataSemanticClassificationIssue),
            typeof(MetadataTypeDefinitionSemanticClassificationIdentity),
            typeof(MetadataAncestryChainTerminalKind),
            typeof(MetadataTypeDefinitionAncestryChainIdentity),
            typeof(MetadataAncestryAuthorityModuleIdentity),
            typeof(MetadataAncestryAuthorityPortfolioResultKind),
            typeof(MetadataAncestryAuthorityPortfolioIssue),
            typeof(MetadataAncestryAuthorityPortfolioIdentity),
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
            if (type == typeof(MetadataAncestryAuthorityPortfolioIdentity))
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

    private static RoleWorld BuildRoleWorld()
    {
        var core = BuildCoreModule();
        var app = BuildCustomModule(
            "Synthetic.RoleApp",
            0xAB00,
            '4',
            [
                new TypeRow("Synthetic.RoleApp", "Widget", PublicClassAttributes, 0x0100_0001),
                new TypeRow("Synthetic.RoleApp", "Point", PublicClassAttributes, 0x0100_0002),
                new TypeRow("Synthetic.RoleApp", "Color", PublicClassAttributes, 0x0100_0003),
                new TypeRow("Synthetic.RoleApp", "Handler", PublicClassAttributes, 0x0100_0004),
                new TypeRow("Synthetic.RoleApp", "IShape", InterfaceAttributes, null),
                new TypeRow("Synthetic.RoleApp", "Broken", PublicClassAttributes, null),
                new TypeRow("Synthetic.RoleApp", "Phantom", PublicClassAttributes, 0x0100_0005),
                new TypeRow("Synthetic.RoleApp", "GenericDerived", PublicClassAttributes, 0x1B00_0001),
                new TypeRow("Synthetic.RoleApp", "IBad", InterfaceAttributes, 0x0200_0002),
                new TypeRow("Synthetic.RoleApp", "DoubleStruct", PublicClassAttributes, 0x0200_0003),
                new TypeRow("Synthetic.RoleApp", "SelfLoop", PublicClassAttributes, 0x0200_000C),
                new TypeRow("Synthetic.RoleApp", "CycleA", PublicClassAttributes, 0x0200_000E),
                new TypeRow("Synthetic.RoleApp", "CycleB", PublicClassAttributes, 0x0200_000D),
            ],
            typeReferences: module =>
            [
                TypeReferenceRow(module, 1, "System", "Object", 0x2300_0001),
                TypeReferenceRow(module, 2, "System", "ValueType", 0x2300_0001),
                TypeReferenceRow(module, 3, "System", "Enum", 0x2300_0001),
                TypeReferenceRow(module, 4, "System", "MulticastDelegate", 0x2300_0001),
                TypeReferenceRow(module, 5, "System", "Missing", 0x2300_0001),
            ],
            assemblyReferences: module => [AssemblyReferenceRow(module, 1, "Synthetic.Core")],
            typeSpecifications: module =>
            [
                MetadataTypeSpecificationRowObservationIdentity.Create(
                    module,
                    0x1B00_0001,
                    [0x15, 0x12, 0x08, 0x01, 0x0E]),
            ]);
        var world = BuildAncestryWorld(core, app);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, world.Ancestry.ResultKind);
        return new RoleWorld(core.Module, app.Module, world.Resolution, world.Ancestry);
    }

    private static AncestryModule BuildCoreModule(
        string assemblyName = "Synthetic.Core",
        ulong moduleAddress = 0xCC00,
        char digest = '3') =>
        BuildCustomModule(
            assemblyName,
            moduleAddress,
            digest,
            [
                new TypeRow("System", "Object", PublicClassAttributes, null),
                new TypeRow("System", "ValueType", PublicClassAttributes, 0x0200_0002),
                new TypeRow("System", "Enum", PublicClassAttributes, 0x0200_0003),
                new TypeRow("System", "Delegate", PublicClassAttributes, 0x0200_0002),
                new TypeRow("System", "MulticastDelegate", PublicClassAttributes, 0x0200_0005),
                new TypeRow("Synthetic.Core", "Anchor", PublicClassAttributes, 0x0200_0002),
            ]);

    private static AncestryWorld BuildAncestryWorld(params AncestryModule[] modules)
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
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, resolution.ResultKind);
        return new AncestryWorld(resolution, MetadataAncestryAuthorityPortfolioIdentity.Create(resolution));
    }

    private static AncestryModule BuildCustomModule(
        string assemblyName,
        ulong moduleAddress,
        char digestCharacter,
        ImmutableArray<TypeRow> namedTypes,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeReferenceRowObservationIdentity>>? typeReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>>? assemblyReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>>? typeSpecifications = null)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var typeReferenceRows = typeReferences?.Invoke(module) ?? [];
        var assemblyReferenceRows = assemblyReferences?.Invoke(module) ?? [];
        var typeSpecificationRows = typeSpecifications?.Invoke(module) ?? [];
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
                assemblyReferenceRowCount: assemblyReferenceRows.Length));
        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(totalTypeCount);
        typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x0200_0001,
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
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.True(
            authority.ResultKind == MetadataDefinitionAuthorityResultKind.Exact,
            $"Authority={authority.ResultKind}/{authority.Issue}; " +
            $"TypeDefs={typeDefinitions.ResultKind}/{typeDefinitions.Issue}.");
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
        Assert.True(
            tables.AllTablesExact,
            $"TypeRefs={tables.TypeReferences.ResultKind}/{tables.TypeReferences.Issue}; " +
            $"TypeSpecs={tables.TypeSpecifications.ResultKind}/{tables.TypeSpecifications.Issue}.");
        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var mapping = MetadataCompilerNameMappingCatalogIdentity.Create(authority);
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(compatibility, mapping);
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);
        return new AncestryModule(module, compatibility, chainCatalog, tables);
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

    private static MetadataTypeDefinitionSemanticClassificationIdentity Classification(
        MetadataAncestryAuthorityPortfolioIdentity portfolio,
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRowId) =>
        Assert.IsType<MetadataTypeDefinitionSemanticClassificationIdentity>(
            portfolio.ExactClassificationOrDefault(module, 0x0200_0000 | typeDefinitionRowId));

    private static MetadataTypeDefinitionSemanticClassificationIdentity AssertRole(
        MetadataAncestryAuthorityPortfolioIdentity portfolio,
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRowId,
        MetadataTypeDefinitionSemanticRole role)
    {
        var classification = Classification(portfolio, module, typeDefinitionRowId);
        Assert.Equal(MetadataSemanticClassificationIssue.None, classification.Issue);
        Assert.Equal(role, classification.Role);
        return classification;
    }

    private static void AssertStop(
        MetadataAncestryAuthorityPortfolioIdentity result,
        MetadataAncestryAuthorityPortfolioResultKind resultKind,
        MetadataAncestryAuthorityPortfolioIssue issue)
    {
        Assert.Equal(resultKind, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.Entries);
        Assert.Null(result.CoreRoles);
        Assert.Null(result.SnapshotSha256);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataAncestryAuthorityPortfolioIdentity).Assembly;
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

    private sealed record TypeRow(
        string NamespaceName,
        string TypeName,
        int TypeAttributes,
        int? ExtendsMetadataToken);

    private sealed record AncestryModule(
        StaticFieldMetadataModuleIdentity Module,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog,
        MetadataModuleReferenceTableSetIdentity Tables);

    private sealed record AncestryWorld(
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry);

    private sealed record RoleWorld(
        StaticFieldMetadataModuleIdentity Core,
        StaticFieldMetadataModuleIdentity App,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry);
}
