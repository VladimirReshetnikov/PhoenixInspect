using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises definition-kind-specific static field lookup with hiding and accessibility.</summary>
public sealed class W8V2MemberLookupTests
{
    private const int NotPublicClass = 0x0000_0000;
    private const int PublicClass = 0x0000_0001;
    private const int NestedPrivateClass = 0x0000_0003;
    private const int InterfaceAttributes = 0x0000_00A1;

    private const int FieldPublic = (int)FieldAttributes.Public;
    private const int FieldPrivate = (int)FieldAttributes.Private;
    private const int FieldFamily = (int)FieldAttributes.Family;
    private const int FieldAssembly = (int)FieldAttributes.Assembly;
    private const int FieldFamOrAssem = (int)FieldAttributes.FamORAssem;
    private const int FieldFamAndAssem = (int)FieldAttributes.FamANDAssem;
    private const int FieldPrivateScope = (int)FieldAttributes.PrivateScope;
    private const int FieldStatic = (int)FieldAttributes.Static;
    private const int FieldLiteral = (int)(FieldAttributes.Literal | FieldAttributes.HasDefault);
    private const int FieldRva = (int)FieldAttributes.HasFieldRVA;

    private const int MethodPublicStatic =
        (int)(MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig);

    private const int BaseRid = 2;
    private const int DerivedRid = 3;
    private const int DeeperRid = 4;
    private const int MethodClashRid = 5;
    private const int LiteralHolderRid = 6;
    private const int RvaHolderRid = 7;
    private const int ShapeInterfaceRid = 8;
    private const int PointishRid = 9;
    private const int PrivateDerivedRid = 10;
    private const int UnresolvedBasedRid = 11;
    private const int GenericBasedRid = 12;
    private const int DuplicateHolderRid = 13;
    private const int InstanceShadowRid = 14;
    private const int AccessZooRid = 15;
    private const int PrivateShellRid = 16;
    private const int InnerRid = 17;
    private const int LibDerivedRid = 18;
    private const int MethodOnlyClashRid = 19;
    private const int ShapeBaseRid = 20;
    private const int ShapeUnresolvedRid = 21;

    private const int LibBaseRid = 2;

    /// <summary>
    /// Proves direct selection, two-level inherited selection with declaring-type retention, derived redeclaration
    /// hiding, per-level provenance, and every physical storage shape of one selected static declaration.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_selection_retains_its_declaring_level_and_storage_shape()
    {
        var world = BuildWorld();

        var direct = Lookup(world, world.App, DerivedRid, "Shared");
        AssertExact(direct, world.App, DerivedRid, levelIndex: 0, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Single(direct.Levels);
        Assert.Equal(0, direct.Levels[0].LevelIndex);
        Assert.Equal(1, direct.Levels[0].ExaminedFieldCount);
        Assert.Equal(0, direct.Levels[0].ExaminedMethodCount);
        Assert.Equal(1, direct.Levels[0].AccessibleCandidateCount);
        Assert.Equal(MetadataAncestryChainTerminalKind.SystemObjectReached, direct.AncestryTerminal);
        Assert.Empty(direct.RejectedCandidates);
        Assert.Contains(
            StaticFieldV2MemberLookupCoverageBoundary.PropertyAndEventTablesNotModeled,
            direct.DeclaredCoverageBoundaries);

        var inherited = Lookup(world, world.App, DeeperRid, "RootOnly");
        AssertExact(inherited, world.App, BaseRid, levelIndex: 2, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Equal(
            [TypeToken(DeeperRid), TypeToken(DerivedRid), TypeToken(BaseRid)],
            inherited.Levels.Select(static level => level.DeclaringTypeDefinitionToken).ToArray());
        Assert.Equal([0, 1, 2], inherited.Levels.Select(static level => level.LevelIndex).ToArray());
        Assert.Equal([0, 1, 3], inherited.Levels.Select(static level => level.ExaminedFieldCount).ToArray());
        Assert.Equal("RootOnly", inherited.SelectedCandidate!.FieldRow.Name);
        Assert.Equal(TypeToken(BaseRid), inherited.SelectedCandidate.DeclaringTypeDefinition.TypeDefinitionToken);

        var hidden = Lookup(world, world.App, DeeperRid, "Shared");
        AssertExact(hidden, world.App, DerivedRid, levelIndex: 1, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Equal(2, hidden.Levels.Length);
        Assert.NotEqual(
            Lookup(world, world.App, BaseRid, "Shared").SelectedCandidate!.FieldDefinitionToken,
            hidden.SelectedCandidate!.FieldDefinitionToken);

        AssertExact(
            Lookup(world, world.App, LiteralHolderRid, "Answer"),
            world.App,
            LiteralHolderRid,
            levelIndex: 0,
            StaticFieldV2FieldStorageShape.MetadataLiteral);
        Assert.True(Lookup(world, world.App, LiteralHolderRid, "Answer").SelectedCandidate!.FieldRow.HasDefault);
        AssertExact(
            Lookup(world, world.App, RvaHolderRid, "Blob"),
            world.App,
            RvaHolderRid,
            levelIndex: 0,
            StaticFieldV2FieldStorageShape.ModuleRvaCandidate);
        Assert.True(Lookup(world, world.App, RvaHolderRid, "Blob").SelectedCandidate!.FieldRow.HasFieldRva);
    }

    /// <summary>
    /// Proves an inaccessible derived redeclaration hides nothing under the use-site certificate yet does hide under
    /// qualified inspection, and that every refused declaration is retained rather than silently skipped.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Inaccessible_derived_declaration_hides_only_under_qualified_inspection()
    {
        var world = BuildWorld();

        var bypass = Lookup(world, world.App, PrivateDerivedRid, "Shared");
        AssertExact(bypass, world.App, PrivateDerivedRid, levelIndex: 0, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Equal(MetadataFieldAccessibility.Private, bypass.SelectedCandidate!.EffectiveAccessibility);
        Assert.True(bypass.SelectedCandidate.IsAccessible);
        Assert.Contains(
            StaticFieldV2MemberLookupCoverageBoundary.AccessibilityBypassApplied,
            bypass.DeclaredCoverageBoundaries);

        var useSite = Lookup(
            world,
            world.App,
            PrivateDerivedRid,
            "Shared",
            StaticFieldV2AccessibilityMode.UseSiteCertificate,
            world.AppAssembly);
        AssertExact(useSite, world.App, BaseRid, levelIndex: 1, StaticFieldV2FieldStorageShape.StoredSlot);
        var refused = Assert.Single(useSite.RejectedCandidates);
        Assert.False(refused.IsAccessible);
        Assert.Equal(MetadataFieldAccessibility.Private, refused.EffectiveAccessibility);
        Assert.Equal(0, refused.LevelIndex);
        Assert.Equal(0, useSite.Levels[0].AccessibleCandidateCount);
        Assert.Contains(
            StaticFieldV2MemberLookupCoverageBoundary.FriendAssemblyAttributesNotModeled,
            useSite.DeclaredCoverageBoundaries);
        Assert.DoesNotContain(
            StaticFieldV2MemberLookupCoverageBoundary.AccessibilityBypassApplied,
            useSite.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves the field-only draft profile stops truthfully on a same-name method or instance field instead of
    /// skipping past it to a farther static declaration.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Same_name_method_or_instance_field_stops_the_field_only_profile()
    {
        var world = BuildWorld();

        var sameLevelMethod = Lookup(world, world.App, MethodClashRid, "Twin");
        Assert.Equal(
            StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember,
            sameLevelMethod.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.HiddenByDeclaredMethod, sameLevelMethod.Issue);
        Assert.Null(sameLevelMethod.SelectedCandidate);
        Assert.Equal(
            world.AppMethodClashMethodToken,
            sameLevelMethod.RelatedMetadataToken);
        Assert.Single(sameLevelMethod.Levels);
        Assert.Equal(2, sameLevelMethod.Levels[0].AccessibleCandidateCount);
        Assert.Equal(2, sameLevelMethod.ObservedCount);

        var derivedMethod = Lookup(world, world.App, MethodOnlyClashRid, "Shared");
        Assert.Equal(
            StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember,
            derivedMethod.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.HiddenByDeclaredMethod, derivedMethod.Issue);
        Assert.Single(derivedMethod.Levels);
        Assert.Empty(derivedMethod.RejectedCandidates);

        var instanceShadow = Lookup(world, world.App, InstanceShadowRid, "Shared");
        Assert.Equal(
            StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember,
            instanceShadow.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.HiddenByInstanceField, instanceShadow.Issue);
        Assert.Null(instanceShadow.SelectedCandidate);
        var shadowed = Assert.Single(instanceShadow.RejectedCandidates);
        Assert.False(shadowed.IsStatic);
        Assert.Equal(shadowed.FieldDefinitionToken, instanceShadow.RelatedMetadataToken);
    }

    /// <summary>
    /// Proves the interface, value-type, and module pseudo-type definition kinds each get their own draft treatment
    /// and that the interface owner records its declared ancestry boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Definition_kinds_select_their_own_bounded_search()
    {
        var world = BuildWorld();

        var interfaceOwner = Lookup(world, world.App, ShapeInterfaceRid, "Shared");
        AssertExact(interfaceOwner, world.App, ShapeInterfaceRid, levelIndex: 0, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Single(interfaceOwner.Levels);
        Assert.Equal(MetadataAncestryChainTerminalKind.InterfaceRoot, interfaceOwner.AncestryTerminal);
        Assert.Contains(
            StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled,
            interfaceOwner.DeclaredCoverageBoundaries);

        var interfaceAbsent = Lookup(world, world.App, ShapeInterfaceRid, "RootOnly");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, interfaceAbsent.ResultKind);
        Assert.Equal(MetadataAncestryChainTerminalKind.InterfaceRoot, interfaceAbsent.AncestryTerminal);
        Assert.Single(interfaceAbsent.Levels);

        var valueType = Lookup(world, world.App, PointishRid, "Origin");
        AssertExact(valueType, world.App, PointishRid, levelIndex: 0, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Equal(MetadataAncestryChainTerminalKind.SystemObjectReached, valueType.AncestryTerminal);
        Assert.Single(valueType.Levels);
        Assert.DoesNotContain(
            StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled,
            valueType.DeclaredCoverageBoundaries);

        var valueTypeAbsent = Lookup(world, world.App, PointishRid, "RootOnly");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, valueTypeAbsent.ResultKind);
        Assert.Equal(3, valueTypeAbsent.Levels.Length);

        var modulePseudoType = Lookup(world, world.App, 1, "Shared");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Unsupported, modulePseudoType.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.ModulePseudoTypeOwner, modulePseudoType.Issue);
        Assert.Empty(modulePseudoType.Levels);
        Assert.Empty(modulePseudoType.RejectedCandidates);
        Assert.Null(modulePseudoType.AncestryTerminal);
        Assert.Null(modulePseudoType.SelectedCandidate);
    }

    /// <summary>
    /// Proves an interface owner walks its bounded transitive interface closure once the request supplies an
    /// interface-implementation portfolio: level zero remains the interface itself, each closure interface follows in
    /// the closure's deterministic order, the unchanged hiding rule still lets the nearest level win, a complete
    /// closure proves absence, an incomplete closure yields a partial answer rather than absence, and the declared
    /// interface-ancestry boundary is no longer declared because the evidence was actually consulted.
    /// </summary>
    /// <remarks>
    /// C# permits static fields on an interface, and the fixture declares them on both interface levels. No instance
    /// member rule is invented here: the same accessibility and hiding rules that govern a class chain apply.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Interface_owner_walks_its_closure_when_the_portfolio_is_supplied()
    {
        var world = BuildWorld();

        var inherited = LookupWithInterfaces(world, ShapeInterfaceRid, "BaseOnly");
        AssertExact(inherited, world.App, ShapeBaseRid, levelIndex: 1, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Equal(
            [TypeToken(ShapeInterfaceRid), TypeToken(ShapeBaseRid)],
            inherited.Levels.Select(static level => level.DeclaringTypeDefinitionToken).ToArray());
        Assert.Equal([0, 1], inherited.Levels.Select(static level => level.LevelIndex).ToArray());
        Assert.Equal(MetadataAncestryChainTerminalKind.InterfaceRoot, inherited.AncestryTerminal);
        Assert.DoesNotContain(
            StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled,
            inherited.DeclaredCoverageBoundaries);

        // Hiding is unchanged: the nearest level that declares an accessible same-name static field still wins.
        var hidden = LookupWithInterfaces(world, ShapeInterfaceRid, "Shared");
        AssertExact(hidden, world.App, ShapeInterfaceRid, levelIndex: 0, StaticFieldV2FieldStorageShape.StoredSlot);
        Assert.Single(hidden.Levels);

        // A complete interface closure proves absence exactly as a complete class chain does.
        var absent = LookupWithInterfaces(world, ShapeInterfaceRid, "RootOnly");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, absent.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.DeclarationAbsent, absent.Issue);
        Assert.Equal(2, absent.Levels.Length);

        // An incomplete interface closure can never produce an absent answer.
        var partial = LookupWithInterfaces(world, ShapeUnresolvedRid, "RootOnly");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Partial, partial.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.AncestryIncomplete, partial.Issue);
        Assert.Single(partial.Levels);
        Assert.Equal(MetadataAncestryChainTerminalKind.InterfaceRoot, partial.AncestryTerminal);
        Assert.Null(partial.SelectedCandidate);

        // Omitting the portfolio keeps the pre-migration behaviour: the interface owner examines only itself.
        var withoutPortfolio = Lookup(world, world.App, ShapeInterfaceRid, "BaseOnly");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, withoutPortfolio.ResultKind);
        Assert.Single(withoutPortfolio.Levels);
        Assert.Contains(
            StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled,
            withoutPortfolio.DeclaredCoverageBoundaries);

        // A class owner is unaffected by the portfolio: its levels remain the bounded class base chain.
        Assert.Equal(
            Lookup(world, world.App, DeeperRid, "RootOnly").Levels.Length,
            LookupWithInterfaces(world, DeeperRid, "RootOnly").Levels.Length);
    }

    /// <summary>
    /// Proves a supplied interface-implementation portfolio that itself stopped propagates as a typed prefix-free
    /// draft stop rather than silently degrading to a self-only interface search.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Interface_portfolio_prerequisite_stops_are_typed_and_prefix_free()
    {
        var world = BuildWorld();

        var nonExact = MetadataInterfaceImplementationPortfolioIdentity.Create(world.Ancestry, default);
        Assert.Equal(MetadataInterfaceImplementationPortfolioResultKind.NonExact, nonExact.ResultKind);
        AssertStop(
            LookupWithInterfaces(world, ShapeInterfaceRid, "Shared", nonExact),
            StaticFieldV2MemberLookupResultKind.NonExact,
            StaticFieldV2MemberLookupIssue.InterfaceImplementationPortfolioNonExact);

        var twoCores = BuildAncestryStop();
        var invalid = MetadataInterfaceImplementationPortfolioIdentity.Create(twoCores, []);
        Assert.Equal(MetadataInterfaceImplementationPortfolioResultKind.Invalid, invalid.ResultKind);
        AssertStop(
            LookupWithInterfaces(world, ShapeInterfaceRid, "Shared", invalid),
            StaticFieldV2MemberLookupResultKind.Invalid,
            StaticFieldV2MemberLookupIssue.InterfaceImplementationPortfolioInvalid);
    }

    /// <summary>
    /// Proves absence is claimed only over a complete bounded chain and that every incomplete terminal produces a
    /// partial draft answer instead.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Incomplete_ancestry_never_claims_absence()
    {
        var world = BuildWorld();

        var absent = Lookup(world, world.App, DeeperRid, "NeverDeclared");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, absent.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.DeclarationAbsent, absent.Issue);
        Assert.Equal(MetadataAncestryChainTerminalKind.SystemObjectReached, absent.AncestryTerminal);
        Assert.Equal(4, absent.Levels.Length);
        Assert.Equal(0, absent.ObservedCount);

        var unresolved = Lookup(world, world.App, UnresolvedBasedRid, "Shared");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Partial, unresolved.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.AncestryIncomplete, unresolved.Issue);
        Assert.Equal(MetadataAncestryChainTerminalKind.UnresolvedReferenceReached, unresolved.AncestryTerminal);
        Assert.Single(unresolved.Levels);
        Assert.Null(unresolved.SelectedCandidate);

        var generic = Lookup(world, world.App, GenericBasedRid, "Shared");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Partial, generic.ResultKind);
        Assert.Equal(MetadataAncestryChainTerminalKind.GenericBaseReached, generic.AncestryTerminal);

        var deep = BuildDepthBoundWorld();
        var depthBound = Lookup(deep, deep.App, BaseRid, "Shared");
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Partial, depthBound.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.AncestryIncomplete, depthBound.Issue);
        Assert.Equal(MetadataAncestryChainTerminalKind.DepthBoundReached, depthBound.AncestryTerminal);
        Assert.Equal(
            MetadataAncestryAuthorityPortfolioIdentity.MaximumAncestryDepth + 1,
            depthBound.Levels.Length);
    }

    /// <summary>Proves two accessible same-name static declarations at one level remain ambiguous.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Duplicate_static_declarations_at_one_level_are_ambiguous()
    {
        var world = BuildWorld();

        var ambiguous = Lookup(world, world.App, DuplicateHolderRid, "Twin2");

        Assert.Equal(StaticFieldV2MemberLookupResultKind.Ambiguous, ambiguous.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.AmbiguousStaticDeclarations, ambiguous.Issue);
        Assert.Null(ambiguous.SelectedCandidate);
        Assert.Single(ambiguous.Levels);
        Assert.Equal(2, ambiguous.Levels[0].AccessibleCandidateCount);
        Assert.Equal(2, ambiguous.RejectedCandidates.Length);
        Assert.All(ambiguous.RejectedCandidates, static candidate => Assert.True(candidate.IsAccessible));
        Assert.Equal(MetadataAncestryChainTerminalKind.SystemObjectReached, ambiguous.AncestryTerminal);
    }

    /// <summary>
    /// Proves every physical accessibility encoding decides identically under qualified inspection and decides
    /// exactly the definition-only subset under the use-site certificate, including nested declaring chains and
    /// cross-assembly friend and non-friend requesting assemblies.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Every_accessibility_encoding_decides_in_both_modes()
    {
        var world = BuildWorld();
        var names = new[] { "PubF", "PrivF", "FamF", "AsmF", "FamOrAsmF", "FamAndAsmF", "CompF" };
        var declared = new[]
        {
            MetadataFieldAccessibility.Public,
            MetadataFieldAccessibility.Private,
            MetadataFieldAccessibility.Family,
            MetadataFieldAccessibility.Assembly,
            MetadataFieldAccessibility.FamilyOrAssembly,
            MetadataFieldAccessibility.FamilyAndAssembly,
            MetadataFieldAccessibility.CompilerControlled,
        };

        for (var index = 0; index < names.Length; index++)
        {
            var bypass = Lookup(world, world.App, AccessZooRid, names[index]);
            AssertExact(bypass, world.App, AccessZooRid, levelIndex: 0, StaticFieldV2FieldStorageShape.StoredSlot);
            Assert.Equal(declared[index], bypass.SelectedCandidate!.EffectiveAccessibility);

            var sameAssembly = Lookup(
                world,
                world.App,
                AccessZooRid,
                names[index],
                StaticFieldV2AccessibilityMode.UseSiteCertificate,
                world.AppAssembly);
            var expectedSameAssembly = declared[index] is MetadataFieldAccessibility.Public
                or MetadataFieldAccessibility.Assembly
                or MetadataFieldAccessibility.FamilyOrAssembly;
            Assert.Equal(
                expectedSameAssembly
                    ? StaticFieldV2MemberLookupResultKind.Exact
                    : StaticFieldV2MemberLookupResultKind.Absent,
                sameAssembly.ResultKind);

            var stranger = Lookup(
                world,
                world.App,
                AccessZooRid,
                names[index],
                StaticFieldV2AccessibilityMode.UseSiteCertificate,
                world.StrangerAssembly);
            Assert.Equal(
                declared[index] == MetadataFieldAccessibility.Public
                    ? StaticFieldV2MemberLookupResultKind.Exact
                    : StaticFieldV2MemberLookupResultKind.Absent,
                stranger.ResultKind);
            Assert.Equal(
                declared[index],
                (stranger.SelectedCandidate ?? stranger.RejectedCandidates[0]).EffectiveAccessibility);

            var friend = Lookup(
                world,
                world.App,
                AccessZooRid,
                names[index],
                StaticFieldV2AccessibilityMode.UseSiteCertificate,
                world.FriendAssembly,
                [AppGrant(world)]);
            Assert.Equal(
                expectedSameAssembly
                    ? StaticFieldV2MemberLookupResultKind.Exact
                    : StaticFieldV2MemberLookupResultKind.Absent,
                friend.ResultKind);
        }

        var nestedBypass = Lookup(world, world.App, InnerRid, "Tucked");
        AssertExact(nestedBypass, world.App, InnerRid, levelIndex: 0, StaticFieldV2FieldStorageShape.StoredSlot);
        var nestedUseSite = Lookup(
            world,
            world.App,
            InnerRid,
            "Tucked",
            StaticFieldV2AccessibilityMode.UseSiteCertificate,
            world.AppAssembly);
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, nestedUseSite.ResultKind);
        Assert.Equal(
            MetadataFieldAccessibility.Private,
            Assert.Single(nestedUseSite.RejectedCandidates).EffectiveAccessibility);

        var crossOpen = Lookup(
            world,
            world.App,
            LibDerivedRid,
            "LibOpen",
            StaticFieldV2AccessibilityMode.UseSiteCertificate,
            world.AppAssembly);
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Exact, crossOpen.ResultKind);
        Assert.Equal(TypeToken(LibBaseRid), crossOpen.SelectedCandidate!.DeclaringTypeDefinition.TypeDefinitionToken);

        var crossInternalWithoutGrant = Lookup(
            world,
            world.App,
            LibDerivedRid,
            "LibShared",
            StaticFieldV2AccessibilityMode.UseSiteCertificate,
            world.AppAssembly);
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, crossInternalWithoutGrant.ResultKind);

        var crossInternalWithGrant = Lookup(
            world,
            world.App,
            LibDerivedRid,
            "LibShared",
            StaticFieldV2AccessibilityMode.UseSiteCertificate,
            world.AppAssembly,
            [LibGrant(world)]);
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Exact, crossInternalWithGrant.ResultKind);
        Assert.Equal(1, crossInternalWithGrant.SelectedCandidate!.LevelIndex);

        var wrongGrantOwner = Lookup(
            world,
            world.App,
            LibDerivedRid,
            "LibShared",
            StaticFieldV2AccessibilityMode.UseSiteCertificate,
            world.AppAssembly,
            [AppGrant(world)]);
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Absent, wrongGrantOwner.ResultKind);
    }

    /// <summary>Proves every prerequisite, catalog-vector, and owner draft stop is typed and prefix-free.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_and_catalog_vector_stops_are_typed_and_prefix_free()
    {
        var world = BuildWorld();
        var name = DumpExpressionIdentifier.Create("Shared");
        var stoppedPortfolio = MetadataAncestryAuthorityPortfolioIdentity.Create(
            MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                    MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                        ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
                    [world.CoreCatalogs.ChainCatalog]),
                [world.CoreCatalogs.Tables]));
        Assert.Equal(
            MetadataAncestryAuthorityPortfolioResultKind.NonExact,
            stoppedPortfolio.ResultKind);
        AssertStop(
            StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
                world.App,
                TypeToken(BaseRid),
                name,
                stoppedPortfolio,
                world.FieldCatalogs,
                StaticFieldV2AccessibilityMode.QualifiedInspectionBypass)),
            StaticFieldV2MemberLookupResultKind.NonExact,
            StaticFieldV2MemberLookupIssue.AncestryPortfolioNonExact);

        AssertStop(
            Select(world, world.App, BaseRid, name, default),
            StaticFieldV2MemberLookupResultKind.NonExact,
            StaticFieldV2MemberLookupIssue.FieldCatalogVectorUninitialized);
        AssertStop(
            Select(world, world.App, BaseRid, name, [world.FieldCatalogs[0]]),
            StaticFieldV2MemberLookupResultKind.NonExact,
            StaticFieldV2MemberLookupIssue.FieldCatalogSlotsIncomplete);
        AssertStop(
            Select(world, world.App, BaseRid, name, world.FieldCatalogs.Add(world.FieldCatalogs[0])),
            StaticFieldV2MemberLookupResultKind.Invalid,
            StaticFieldV2MemberLookupIssue.FieldCatalogSlotCountConflict);
        AssertStop(
            Select(
                world,
                world.App,
                BaseRid,
                name,
                world.FieldCatalogs.SetItem(0, null!)),
            StaticFieldV2MemberLookupResultKind.NonExact,
            StaticFieldV2MemberLookupIssue.FieldCatalogMissing);
        AssertStop(
            Select(
                world,
                world.App,
                BaseRid,
                name,
                world.FieldCatalogs.SetItem(0, world.FieldCatalogs[1])),
            StaticFieldV2MemberLookupResultKind.Invalid,
            StaticFieldV2MemberLookupIssue.DuplicateFieldCatalogModule);

        var nonExactCatalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            world.CoreCatalogs.Authority,
            ImmutableArray<MetadataFieldDefinitionRowObservationIdentity>.Empty.Add(null!));
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Invalid, nonExactCatalog.ResultKind);
        AssertStop(
            Select(world, world.App, BaseRid, name, world.FieldCatalogs.SetItem(0, nonExactCatalog)),
            StaticFieldV2MemberLookupResultKind.Invalid,
            StaticFieldV2MemberLookupIssue.FieldCatalogInvalid);

        var strangerCatalogs = world.FieldCatalogs.SetItem(0, BuildWorld(0x7000).FieldCatalogs[0]);
        var strangerStop = Select(world, world.App, BaseRid, name, strangerCatalogs);
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Invalid, strangerStop.ResultKind);
        Assert.Equal(
            StaticFieldV2MemberLookupIssue.FieldCatalogModuleNotInPortfolio,
            strangerStop.Issue);

        var foreignModule = W8CompilerNameMappingContractTests.CreateMetadataModule(
            0x7700,
            'f',
            "Synthetic.LookupForeign");
        AssertStop(
            StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
                foreignModule,
                TypeToken(BaseRid),
                name,
                world.Ancestry,
                world.FieldCatalogs,
                StaticFieldV2AccessibilityMode.QualifiedInspectionBypass)),
            StaticFieldV2MemberLookupResultKind.Invalid,
            StaticFieldV2MemberLookupIssue.OwnerModuleNotInPortfolio);
        AssertStop(
            Select(world, world.App, 0x00FF, name, world.FieldCatalogs),
            StaticFieldV2MemberLookupResultKind.Invalid,
            StaticFieldV2MemberLookupIssue.OwnerTypeDefinitionNotIssued);
    }

    /// <summary>
    /// Proves canonical replay, defensive draft copies, guarded private issuance, the closed public surface, and
    /// emitted draft XML documentation for the whole member-lookup surface.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Member_lookup_contracts_replay_are_immutable_guarded_and_documented()
    {
        var world = BuildWorld();
        var replayWorld = BuildWorld();
        var outcome = Lookup(world, world.App, DeeperRid, "RootOnly");
        var replay = Lookup(replayWorld, replayWorld.App, DeeperRid, "RootOnly");

        Assert.Equal(outcome, replay);
        Assert.Equal(outcome.GetHashCode(), replay.GetHashCode());
        Assert.Equal(outcome.Sha256, replay.Sha256);
        Assert.Equal(outcome.Request, replay.Request);
        Assert.True(outcome.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        Assert.NotEqual(outcome, Lookup(world, world.App, DerivedRid, "Shared"));
        Assert.Equal(
            "c8cc916736efd2d3ba4cb324b336ee3d7cf706f5a5b0cf9776068dd46f59d020",
            outcome.Sha256);

        var originalBytes = outcome.CanonicalBytes;
        var originalSha = outcome.Sha256;
        var originalLevels = outcome.Levels;
        var originalFirstCatalog = outcome.Request.FieldCatalogs[0];
        var returnedLevels = outcome.Levels;
        ImmutableCollectionsMarshal.AsArray(returnedLevels)![0] = returnedLevels[^1];
        var returnedRejected = outcome.RejectedCandidates;
        if (!returnedRejected.IsEmpty)
        {
            ImmutableCollectionsMarshal.AsArray(returnedRejected)![0] = returnedRejected[^1];
        }
        var returnedBoundaries = outcome.DeclaredCoverageBoundaries;
        ImmutableCollectionsMarshal.AsArray(returnedBoundaries)![0] =
            StaticFieldV2MemberLookupCoverageBoundary.InterfaceAncestryNotModeled;
        var returnedOutcomeBytes = outcome.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedOutcomeBytes)![0] ^= 0x5A;
        var returnedCatalogs = outcome.Request.FieldCatalogs;
        ImmutableCollectionsMarshal.AsArray(returnedCatalogs)![0] = returnedCatalogs[^1];
        var returnedGrants = LibGrant(world).DeclaredFriendAssemblies;
        ImmutableCollectionsMarshal.AsArray(returnedGrants)![0] = world.StrangerAssembly.AssemblyDefinition;

        Assert.Equal(originalLevels[0], outcome.Levels[0]);
        Assert.Equal(originalSha, outcome.Sha256);
        Assert.True(originalBytes.AsSpan().SequenceEqual(outcome.CanonicalBytes.AsSpan()));
        Assert.Equal(
            StaticFieldV2MemberLookupCoverageBoundary.PropertyAndEventTablesNotModeled,
            outcome.DeclaredCoverageBoundaries[0]);
        Assert.Equal(originalFirstCatalog, outcome.Request.FieldCatalogs[0]);
        Assert.Equal(3, outcome.Request.FieldCatalogs.Length);
        Assert.True(outcome.Request.IsFieldCatalogVectorInitialized);
        Assert.True(LibGrant(world).Declares(world.AppAssembly.AssemblyDefinition));

        Assert.Throws<ArgumentException>(() => StaticFieldV2MemberCandidateIdentity.Create(
            new object(),
            outcome.SelectedCandidate!.FieldRow,
            outcome.SelectedCandidate.DeclaringTypeDefinition,
            0,
            true,
            MetadataFieldAccessibility.Public,
            StaticFieldV2FieldStorageShape.StoredSlot));
        Assert.Throws<ArgumentException>(() => StaticFieldV2MemberLookupLevelIdentity.Create(
            new object(),
            0,
            outcome.SelectedCandidate!.DeclaringTypeDefinition,
            0,
            0,
            0));
        Assert.False(StaticFieldV2MemberLookupOutcome.OwnsRowMintCapability(new object()));

        var name = DumpExpressionIdentifier.Create("Shared");
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2MemberLookup.SelectStaticField(null!));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2MemberLookupRequest.Create(
            null!,
            TypeToken(BaseRid),
            name,
            world.Ancestry,
            world.FieldCatalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldV2MemberLookupRequest.Create(
            world.App,
            0x0400_0001,
            name,
            world.Ancestry,
            world.FieldCatalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldV2MemberLookupRequest.Create(
            world.App,
            TypeToken(BaseRid),
            name,
            world.Ancestry,
            world.FieldCatalogs,
            (StaticFieldV2AccessibilityMode)7));
        Assert.Throws<ArgumentException>(() => StaticFieldV2MemberLookupRequest.Create(
            world.App,
            TypeToken(BaseRid),
            name,
            world.Ancestry,
            world.FieldCatalogs,
            StaticFieldV2AccessibilityMode.UseSiteCertificate));
        Assert.Throws<ArgumentException>(() => StaticFieldV2MemberLookupRequest.Create(
            world.App,
            TypeToken(BaseRid),
            name,
            world.Ancestry,
            world.FieldCatalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            world.AppAssembly));
        Assert.Throws<ArgumentException>(() => StaticFieldV2MemberLookupRequest.Create(
            world.App,
            TypeToken(BaseRid),
            name,
            world.Ancestry,
            world.FieldCatalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            null,
            [LibGrant(world)]));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2FriendAssemblyGrantIdentity.Create(
            null!,
            [world.AppAssembly.AssemblyDefinition]));
        Assert.Throws<ArgumentException>(() => StaticFieldV2FriendAssemblyGrantIdentity.Create(
            world.LibAssembly,
            default));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2AccessibilityMode),
            typeof(StaticFieldV2MemberLookupResultKind),
            typeof(StaticFieldV2MemberLookupIssue),
            typeof(StaticFieldV2FieldStorageShape),
            typeof(StaticFieldV2MemberLookupCoverageBoundary),
            typeof(StaticFieldV2FriendAssemblyGrantIdentity),
            typeof(StaticFieldV2MemberCandidateIdentity),
            typeof(StaticFieldV2MemberLookupLevelIdentity),
            typeof(StaticFieldV2MemberLookupRequest),
            typeof(StaticFieldV2MemberLookupOutcome),
            typeof(StaticFieldV2MemberLookup),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .ToArray();
            if (type == typeof(StaticFieldV2MemberLookup))
            {
                Assert.Equal(["SelectStaticField"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2MemberLookupRequest) ||
                     type == typeof(StaticFieldV2FriendAssemblyGrantIdentity))
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

    private static void AssertExact(
        StaticFieldV2MemberLookupOutcome outcome,
        StaticFieldMetadataModuleIdentity module,
        int declaringTypeRowId,
        int levelIndex,
        StaticFieldV2FieldStorageShape storageShape)
    {
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2MemberLookupIssue.None, outcome.Issue);
        var candidate = Assert.IsType<StaticFieldV2MemberCandidateIdentity>(outcome.SelectedCandidate);
        Assert.Equal(TypeToken(declaringTypeRowId), candidate.DeclaringTypeDefinition.TypeDefinitionToken);
        Assert.Equal(levelIndex, candidate.LevelIndex);
        Assert.Equal(storageShape, candidate.StorageShape);
        Assert.True(candidate.IsStatic);
        Assert.True(candidate.IsAccessible);
        Assert.Equal(candidate.FieldDefinitionToken, outcome.RelatedMetadataToken);
        Assert.Null(outcome.ReachedBound);
        Assert.Equal(module, outcome.Request.OwnerModule);
    }

    private static void AssertStop(
        StaticFieldV2MemberLookupOutcome outcome,
        StaticFieldV2MemberLookupResultKind resultKind,
        StaticFieldV2MemberLookupIssue issue)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(issue, outcome.Issue);
        Assert.Empty(outcome.Levels);
        Assert.Empty(outcome.RejectedCandidates);
        Assert.Null(outcome.SelectedCandidate);
        Assert.Null(outcome.AncestryTerminal);
        Assert.NotEmpty(outcome.DeclaredCoverageBoundaries);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2MemberLookupOutcome).Assembly;
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

    private static StaticFieldV2FriendAssemblyGrantIdentity AppGrant(LookupWorld world) =>
        StaticFieldV2FriendAssemblyGrantIdentity.Create(
            world.AppAssembly,
            [world.FriendAssembly.AssemblyDefinition]);

    private static StaticFieldV2FriendAssemblyGrantIdentity LibGrant(LookupWorld world) =>
        StaticFieldV2FriendAssemblyGrantIdentity.Create(
            world.LibAssembly,
            [world.AppAssembly.AssemblyDefinition]);

    private static StaticFieldV2MemberLookupOutcome Lookup(
        LookupWorld world,
        StaticFieldMetadataModuleIdentity module,
        int typeRowId,
        string fieldName,
        StaticFieldV2AccessibilityMode mode = StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
        StaticFieldContainingAssemblyIdentity? requestingAssembly = null,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> grants = default) =>
        StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
            module,
            TypeToken(typeRowId),
            DumpExpressionIdentifier.Create(fieldName),
            world.Ancestry,
            world.FieldCatalogs,
            mode,
            requestingAssembly,
            grants));

    private static StaticFieldV2MemberLookupOutcome LookupWithInterfaces(
        LookupWorld world,
        int typeRowId,
        string fieldName,
        MetadataInterfaceImplementationPortfolioIdentity? portfolio = null) =>
        StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
            world.App,
            TypeToken(typeRowId),
            DumpExpressionIdentifier.Create(fieldName),
            world.Ancestry,
            world.FieldCatalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            null,
            default,
            portfolio ?? world.InterfaceImplementations));

    private static StaticFieldV2MemberLookupOutcome Select(
        LookupWorld world,
        StaticFieldMetadataModuleIdentity module,
        int typeRowId,
        DumpExpressionIdentifier fieldName,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> catalogs) =>
        StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
            module,
            TypeToken(typeRowId),
            fieldName,
            world.Ancestry,
            catalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass));

    private static LookupWorld BuildWorld(ulong addressBase = 0x5000)
    {
        var core = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(
                addressBase,
                '1',
                "Synthetic.LookupCore"),
            [
                Type("System", "Object", PublicClass, null),
                Type("System", "ValueType", PublicClass, TypeToken(2)),
                Type("System", "Enum", PublicClass, TypeToken(3)),
                Type("System", "Delegate", PublicClass, TypeToken(2)),
                Type("System", "MulticastDelegate", PublicClass, TypeToken(5)),
            ]);
        var lib = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(
                addressBase + 0x0100,
                '3',
                "Synthetic.LookupLib"),
            [
                Type(
                    "Synthetic.LookupLib",
                    "LibBase",
                    PublicClass,
                    0x0100_0001,
                    fields:
                    [
                        Field("LibShared", FieldAssembly | FieldStatic),
                        Field("LibOpen", FieldPublic | FieldStatic),
                    ]),
            ],
            typeReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module,
                    1,
                    "System",
                    "Object",
                    0x2300_0001),
            ],
            assemblyReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Synthetic.LookupCore"),
            ]);
        var app = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(
                addressBase + 0x0200,
                '2',
                "Synthetic.LookupApp"),
            [
                Type(
                    "Synthetic.Lookup",
                    "Base",
                    PublicClass,
                    0x0100_0001,
                    fields:
                    [
                        Field("Shared", FieldPublic | FieldStatic),
                        Field("Instance", FieldPublic),
                        Field("RootOnly", FieldPublic | FieldStatic),
                    ]),
                Type(
                    "Synthetic.Lookup",
                    "Derived",
                    PublicClass,
                    TypeToken(BaseRid),
                    fields: [Field("Shared", FieldPublic | FieldStatic)]),
                Type("Synthetic.Lookup", "Deeper", PublicClass, TypeToken(DerivedRid)),
                Type(
                    "Synthetic.Lookup",
                    "MethodClash",
                    PublicClass,
                    0x0100_0001,
                    fields: [Field("Twin", FieldPublic | FieldStatic)],
                    methods: [Method("Twin", MethodPublicStatic)]),
                Type(
                    "Synthetic.Lookup",
                    "LiteralHolder",
                    PublicClass,
                    0x0100_0001,
                    fields: [Field("Answer", FieldPublic | FieldStatic | FieldLiteral)]),
                Type(
                    "Synthetic.Lookup",
                    "RvaHolder",
                    PublicClass,
                    0x0100_0001,
                    fields: [Field("Blob", FieldPublic | FieldStatic | FieldRva)]),
                Type(
                    "Synthetic.Lookup",
                    "IShape",
                    InterfaceAttributes,
                    null,
                    fields: [Field("Shared", FieldPublic | FieldStatic)]),
                Type(
                    "Synthetic.Lookup",
                    "Pointish",
                    PublicClass,
                    0x0100_0002,
                    fields: [Field("Origin", FieldPublic | FieldStatic)]),
                Type(
                    "Synthetic.Lookup",
                    "PrivateDerived",
                    PublicClass,
                    TypeToken(BaseRid),
                    fields: [Field("Shared", FieldPrivate | FieldStatic)]),
                Type("Synthetic.Lookup", "UnresolvedBased", PublicClass, 0x0100_0004),
                Type("Synthetic.Lookup", "GenericBased", PublicClass, 0x1B00_0001),
                Type(
                    "Synthetic.Lookup",
                    "DuplicateHolder",
                    PublicClass,
                    0x0100_0001,
                    fields:
                    [
                        Field("Twin2", FieldPublic | FieldStatic),
                        Field("Twin2", FieldPublic | FieldStatic),
                    ]),
                Type(
                    "Synthetic.Lookup",
                    "InstanceShadow",
                    PublicClass,
                    TypeToken(BaseRid),
                    fields: [Field("Shared", FieldPublic)]),
                Type(
                    "Synthetic.Lookup",
                    "AccessZoo",
                    PublicClass,
                    0x0100_0001,
                    fields:
                    [
                        Field("PubF", FieldPublic | FieldStatic),
                        Field("PrivF", FieldPrivate | FieldStatic),
                        Field("FamF", FieldFamily | FieldStatic),
                        Field("AsmF", FieldAssembly | FieldStatic),
                        Field("FamOrAsmF", FieldFamOrAssem | FieldStatic),
                        Field("FamAndAsmF", FieldFamAndAssem | FieldStatic),
                        Field("CompF", FieldPrivateScope | FieldStatic),
                    ]),
                Type("Synthetic.Lookup", "PrivateShell", PublicClass, 0x0100_0001),
                Type(
                    string.Empty,
                    "Inner",
                    NestedPrivateClass,
                    0x0100_0001,
                    fields: [Field("Tucked", FieldPublic | FieldStatic)],
                    enclosingTypeRowId: PrivateShellRid),
                Type("Synthetic.Lookup", "LibDerived", PublicClass, 0x0100_0003),
                Type(
                    "Synthetic.Lookup",
                    "MethodOnlyClash",
                    PublicClass,
                    TypeToken(BaseRid),
                    methods: [Method("Shared", MethodPublicStatic)]),
                Type(
                    "Synthetic.Lookup",
                    "IShapeBase",
                    InterfaceAttributes,
                    null,
                    fields:
                    [
                        Field("Shared", FieldPublic | FieldStatic),
                        Field("BaseOnly", FieldPublic | FieldStatic),
                    ]),
                Type("Synthetic.Lookup", "IShapeUnresolved", InterfaceAttributes, null),
            ],
            typeReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module,
                    1,
                    "System",
                    "Object",
                    0x2300_0001),
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module,
                    2,
                    "System",
                    "ValueType",
                    0x2300_0001),
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module,
                    3,
                    "Synthetic.LookupLib",
                    "LibBase",
                    0x2300_0002),
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module,
                    4,
                    "System",
                    "Missing",
                    0x2300_0001),
            ],
            assemblyReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Synthetic.LookupCore"),
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 2, "Synthetic.LookupLib"),
            ],
            typeSpecifications: module =>
            [
                MetadataTypeSpecificationRowObservationIdentity.Create(
                    module,
                    0x1B00_0001,
                    [0x15, 0x12, 0x08, 0x01, 0x0E]),
            ],
            interfaceImplementations: module =>
            [
                MetadataInterfaceImplementationRowObservationIdentity.Create(
                    module,
                    0x0900_0001,
                    TypeToken(ShapeInterfaceRid),
                    TypeToken(ShapeBaseRid)),
                MetadataInterfaceImplementationRowObservationIdentity.Create(
                    module,
                    0x0900_0002,
                    TypeToken(ShapeUnresolvedRid),
                    0x0100_0004),
            ]);

        var ancestry = BuildAncestry(core, lib, app);
        var order = ancestry.Entries.Select(static entry => entry.SourceModule).ToArray();
        var byModule = new[] { core, lib, app }.ToDictionary(static built => built.Module);
        var catalogs = ImmutableArray.CreateRange(order.Select(module => byModule[module].FieldCatalog));
        var interfacePortfolio = MetadataInterfaceImplementationPortfolioIdentity.Create(
            ancestry,
            [.. order.Select(module => byModule[module].InterfaceCatalog)]);
        Assert.Equal(
            MetadataInterfaceImplementationPortfolioResultKind.Exact,
            interfacePortfolio.ResultKind);
        return new LookupWorld(
            core.Module,
            lib.Module,
            app.Module,
            core,
            ancestry,
            catalogs,
            interfacePortfolio,
            app.Module.ContainingAssembly,
            lib.Module.ContainingAssembly,
            W8CompilerNameMappingContractTests.CreateMetadataModule(
                addressBase + 0x0300,
                '6',
                "Synthetic.LookupFriend").ContainingAssembly,
            W8CompilerNameMappingContractTests.CreateMetadataModule(
                addressBase + 0x0400,
                '5',
                "Synthetic.LookupStranger").ContainingAssembly);
    }

    private static LookupWorld BuildDepthBoundWorld()
    {
        const int depthCap = MetadataAncestryAuthorityPortfolioIdentity.MaximumAncestryDepth;
        var core = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0x6000, '1', "Synthetic.LookupCore"),
            [
                Type("System", "Object", PublicClass, null),
                Type("System", "ValueType", PublicClass, TypeToken(2)),
                Type("System", "Enum", PublicClass, TypeToken(3)),
                Type("System", "Delegate", PublicClass, TypeToken(2)),
                Type("System", "MulticastDelegate", PublicClass, TypeToken(5)),
            ]);
        var deepTypes = ImmutableArray.CreateBuilder<LookupTypeRow>(depthCap + 1);
        for (var index = 1; index <= depthCap + 1; index++)
        {
            deepTypes.Add(Type(
                "Synthetic.LookupDeep",
                $"Deep{index}",
                PublicClass,
                index == depthCap + 1 ? 0x0100_0001 : TypeToken(index + 2)));
        }
        var deep = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0x6100, '4', "Synthetic.LookupDeep"),
            deepTypes.ToImmutable(),
            typeReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module,
                    1,
                    "System",
                    "Object",
                    0x2300_0001),
            ],
            assemblyReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Synthetic.LookupCore"),
            ]);

        var ancestry = BuildAncestry(core, deep);
        var byModule = new[] { core, deep }.ToDictionary(static built => built.Module);
        var catalogs = ImmutableArray.CreateRange(
            ancestry.Entries.Select(entry => byModule[entry.SourceModule].FieldCatalog));
        var interfacePortfolio = MetadataInterfaceImplementationPortfolioIdentity.Create(
            ancestry,
            [.. ancestry.Entries.Select(entry => byModule[entry.SourceModule].InterfaceCatalog)]);
        return new LookupWorld(
            core.Module,
            deep.Module,
            deep.Module,
            core,
            ancestry,
            catalogs,
            interfacePortfolio,
            deep.Module.ContainingAssembly,
            core.Module.ContainingAssembly,
            deep.Module.ContainingAssembly,
            core.Module.ContainingAssembly);
    }

    private static MetadataAncestryAuthorityPortfolioIdentity BuildAncestryStop()
    {
        var ancestry = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule(),
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule("Synthetic.Core2", 0x7100, '8')).Ancestry;
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Invalid, ancestry.ResultKind);
        return ancestry;
    }

    private static MetadataAncestryAuthorityPortfolioIdentity BuildAncestry(params BuiltModule[] modules)
    {
        var compatibility = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [.. modules.Select(static module => module.Compatibility)]);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.Exact, compatibility.ResultKind);
        var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibility,
            [.. modules.Select(static module => module.ChainCatalog)]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, chainPortfolio.ResultKind);
        var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            chainPortfolio,
            [.. modules.Select(static module => module.Tables)]);
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, resolution.ResultKind);
        var ancestry = MetadataAncestryAuthorityPortfolioIdentity.Create(resolution);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestry.ResultKind);
        return ancestry;
    }

    private static BuiltModule BuildModule(
        StaticFieldMetadataModuleIdentity module,
        ImmutableArray<LookupTypeRow> namedTypes,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeReferenceRowObservationIdentity>>? typeReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>>? assemblyReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>>? typeSpecifications = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>>? interfaceImplementations = null)
    {
        var typeReferenceRows = typeReferences?.Invoke(module) ?? [];
        var assemblyReferenceRows = assemblyReferences?.Invoke(module) ?? [];
        var typeSpecificationRows = typeSpecifications?.Invoke(module) ?? [];
        var interfaceRows = interfaceImplementations?.Invoke(module) ?? [];
        var totalTypeCount = namedTypes.Length + 1;

        var fieldObservations = ImmutableArray.CreateBuilder<MetadataFieldDefinitionRowObservationIdentity>();
        var methodObservations = ImmutableArray.CreateBuilder<MetadataMethodDefinitionRowObservationIdentity>();
        var nestedClassRows = ImmutableArray.CreateBuilder<MetadataNestedClassRowObservationIdentity>();
        var fieldStarts = new int[totalTypeCount];
        var methodStarts = new int[totalTypeCount];
        fieldStarts[0] = 1;
        methodStarts[0] = 1;
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            fieldStarts[index + 1] = fieldObservations.Count + 1;
            methodStarts[index + 1] = methodObservations.Count + 1;
            foreach (var field in row.Fields)
            {
                fieldObservations.Add(MetadataFieldDefinitionRowObservationIdentity.Create(
                    module,
                    0x0400_0000 | checked(fieldObservations.Count + 1),
                    field.Attributes,
                    field.Name,
                    [0x06, 0x08]));
            }
            foreach (var method in row.Methods)
            {
                methodObservations.Add(MetadataMethodDefinitionRowObservationIdentity.Create(
                    module,
                    0x0600_0000 | checked(methodObservations.Count + 1),
                    relativeVirtualAddress: 0x2000 + methodObservations.Count * 0x20,
                    implementationAttributes: 0,
                    attributes: method.Attributes,
                    name: method.Name,
                    signaturePrefixBytes: [0x00, 0x00, 0x01],
                    signatureByteCount: 3,
                    parameterListRowId: 0));
            }
            if (row.EnclosingTypeRowId is { } enclosingRowId)
            {
                nestedClassRows.Add(MetadataNestedClassRowObservationIdentity.Create(
                    module,
                    0x2900_0000 | checked(nestedClassRows.Count + 1),
                    nestedTypeDefinitionToken: TypeToken(index + 2),
                    enclosingTypeDefinitionToken: TypeToken(enclosingRowId)));
            }
        }

        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: totalTypeCount,
                fieldDefinitionsExamined: fieldObservations.Count,
                typeDefinitionRowCount: totalTypeCount,
                fieldDefinitionRowCount: fieldObservations.Count,
                typeReferenceRowCount: typeReferenceRows.Length,
                typeSpecificationRowCount: typeSpecificationRows.Length,
                assemblyReferenceRowCount: assemblyReferenceRows.Length,
                methodDefinitionRowCount: methodObservations.Count,
                interfaceImplementationRowCount: interfaceRows.Length,
                nestedClassRowCount: nestedClassRows.Count));

        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(totalTypeCount);
        typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            TypeToken(1),
            fieldListRowId: fieldStarts[0],
            methodListRowId: methodStarts[0],
            namespaceName: string.Empty,
            typeName: "<Module>",
            typeAttributes: NotPublicClass,
            extendsMetadataToken: null));
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                TypeToken(index + 2),
                fieldListRowId: fieldStarts[index + 1],
                methodListRowId: methodStarts[index + 1],
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
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, typeDefinitions.ResultKind);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            nestedClassRows.ToImmutable());
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            methodObservations.Count == 0 ? default : methodObservations.ToImmutable());
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, methods.ResultKind);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var fieldCatalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            fieldObservations.Count == 0 ? default : fieldObservations.ToImmutable());
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, fieldCatalog.ResultKind);

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

        var interfaceCatalog = MetadataInterfaceImplementationTableCatalogIdentity.Create(authority, interfaceRows);
        Assert.Equal(MetadataInterfaceImplementationTableResultKind.Exact, interfaceCatalog.ResultKind);
        return new BuiltModule(
            module,
            authority,
            fieldCatalog,
            compatibility,
            chainCatalog,
            tables,
            interfaceCatalog);
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

    private static LookupTypeRow Type(
        string namespaceName,
        string typeName,
        int typeAttributes,
        int? extendsMetadataToken,
        ImmutableArray<FieldSpec> fields = default,
        ImmutableArray<MethodSpec> methods = default,
        int? enclosingTypeRowId = null) =>
        new(
            namespaceName,
            typeName,
            typeAttributes,
            extendsMetadataToken,
            fields.IsDefault ? [] : fields,
            methods.IsDefault ? [] : methods,
            enclosingTypeRowId);

    private static FieldSpec Field(string name, int attributes) => new(name, attributes);

    private static MethodSpec Method(string name, int attributes) => new(name, attributes);

    private static int TypeToken(int rowId) => 0x0200_0000 | rowId;

    private sealed record FieldSpec(string Name, int Attributes);

    private sealed record MethodSpec(string Name, int Attributes);

    private sealed record LookupTypeRow(
        string NamespaceName,
        string TypeName,
        int TypeAttributes,
        int? ExtendsMetadataToken,
        ImmutableArray<FieldSpec> Fields,
        ImmutableArray<MethodSpec> Methods,
        int? EnclosingTypeRowId);

    private sealed record BuiltModule(
        StaticFieldMetadataModuleIdentity Module,
        MetadataDefinitionAuthorityCatalogIdentity Authority,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog,
        MetadataModuleReferenceTableSetIdentity Tables,
        MetadataInterfaceImplementationTableCatalogIdentity InterfaceCatalog);

    private sealed record LookupWorld(
        StaticFieldMetadataModuleIdentity Core,
        StaticFieldMetadataModuleIdentity Lib,
        StaticFieldMetadataModuleIdentity App,
        BuiltModule CoreCatalogs,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs,
        MetadataInterfaceImplementationPortfolioIdentity InterfaceImplementations,
        StaticFieldContainingAssemblyIdentity AppAssembly,
        StaticFieldContainingAssemblyIdentity LibAssembly,
        StaticFieldContainingAssemblyIdentity FriendAssembly,
        StaticFieldContainingAssemblyIdentity StrangerAssembly)
    {
        public int AppMethodClashMethodToken => 0x0600_0001;
    }
}
