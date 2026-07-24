using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises lexical-blocker completeness certificates and bare <c>using static</c> root binding.</summary>
public sealed class W8V2LexicalCompletenessTests
{
    private const int NotPublicClass = 0x0000_0000;
    private const int PublicClass = 0x0000_0001;
    private const int NestedPrivateClass = 0x0000_0003;

    private const int FieldPublic = (int)FieldAttributes.Public;
    private const int FieldStatic = (int)FieldAttributes.Static;
    private const int MethodPublicStatic =
        (int)(MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig);

    private const int ModulePseudoRid = 1;
    private const int HostRid = 2;
    private const int MemberHostRid = 3;
    private const int NestHostRid = 4;
    private const int NestedCurrentRid = 5;
    private const int InheritBaseRid = 6;
    private const int InheritHostRid = 7;
    private const int InstanceBaseRid = 8;
    private const int InstanceHostRid = 9;
    private const int MethodHostRid = 10;
    private const int PartialHostRid = 11;
    private const int OuterRid = 12;
    private const int InnerRid = 13;
    private const int FloodHostRid = 14;
    private const int ImportOwnerARid = 15;
    private const int ImportOwnerBRid = 16;
    private const int ImportedBaseRid = 17;
    private const int ImportedDerivedRid = 18;
    private const int AmbiguousOwnerRid = 19;
    private const int HiddenOwnerRid = 20;

    private const int LibOwnerRid = 2;

    private const int ObjectTypeReferenceToken = 0x0100_0001;
    private const int LibOwnerTypeReferenceToken = 0x0100_0002;
    private const int MissingTypeReferenceToken = 0x0100_0003;

    private const int FloodFieldCount = 260;

    /// <summary>
    /// Proves a certificate whose every blocker kind is complete and absent lets one active <c>using static</c>
    /// import contribute its directly declared static field, and retains one disposition row per kind.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complete_certificate_admits_one_imported_static_field()
    {
        var world = World();
        var certificate = Certify(world, HostRid, "Current", Lexical(world, HostRid, UsingStaticChain(world)));

        Assert.Equal(StaticFieldV2LexicalCertificateResultKind.Complete, certificate.ResultKind);
        Assert.Equal(StaticFieldV2LexicalCertificateIssue.None, certificate.Issue);
        Assert.Null(certificate.OwningKind);
        Assert.Null(certificate.FirstIncompleteKind);
        Assert.Null(certificate.ReachedBound);
        Assert.Equal(7, certificate.Blockers.Length);
        Assert.All(certificate.Blockers, static blocker => Assert.Equal(
            StaticFieldV2LexicalBlockerDisposition.CompleteAndAbsent,
            blocker.Disposition));
        Assert.Equal(
            certificate.Blockers.Select(static blocker => blocker.Kind).Order().ToArray(),
            certificate.Blockers.Select(static blocker => blocker.Kind).ToArray());
        Assert.Equal(
            StaticFieldV2LexicalBlockerDisposition.CompleteAndAbsent,
            certificate.BlockerFor(StaticFieldV2LexicalBlockerKind.NestedTypeName)!.Disposition);

        var outcome = Bind(world, HostRid, "Current", UsingStaticChain(world));

        Assert.Equal(StaticFieldV2BareRootResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.None, outcome.Issue);
        Assert.Equal(StaticFieldV2BareRootSource.UsingStaticImport, outcome.Source);
        Assert.Equal(world.App.Module, outcome.SelectedOwnerModule);
        Assert.Equal(TypeToken(ImportOwnerARid), outcome.SelectedOwnerTypeDefinition!.TypeDefinitionToken);
        Assert.Equal(0, outcome.SelectedField!.LevelIndex);
        Assert.True(outcome.SelectedField.IsStatic);
        Assert.Equal(StaticFieldV2FieldStorageShape.StoredSlot, outcome.SelectedField.StorageShape);
        Assert.Equal(outcome.SelectedField.FieldDefinitionToken, outcome.RelatedMetadataToken);
        var candidate = Assert.Single(outcome.ImportCandidates);
        Assert.True(candidate.IsAccepted);
        Assert.False(candidate.IsInheritedAndRefused);
        Assert.Equal(StaticFieldV2ScopedImportKind.UsingStatic, candidate.ContributingImport.Kind);
        Assert.Single(outcome.DeclaringChainLookups);
        Assert.Contains(
            StaticFieldV2LexicalCoverageBoundary.UsingStaticInheritedMembersNotImported,
            outcome.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves each complete blocker kind that declares the spelling shadows the bare root, names its own kind, and
    /// refuses the import even though the import would otherwise contribute a directly declared static field.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Each_complete_blocker_kind_shadows_the_bare_root()
    {
        var world = World();
        var scopes = UsingStaticChain(world);

        AssertShadowed(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, locals: ["Current"]),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalVariable);
        AssertShadowed(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, constants: ["Current"]),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalConstant);
        AssertShadowed(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, parameters: ["Current"]),
            scopes,
            StaticFieldV2LexicalBlockerKind.Parameter);
        AssertShadowed(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, typeParameters: ["Current"]),
            scopes,
            StaticFieldV2LexicalBlockerKind.TypeParameter);
        AssertShadowed(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, localFunctions: ["Current"]),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalFunction);
        AssertShadowed(
            world,
            MemberHostRid,
            Lexical(world, MemberHostRid, scopes),
            scopes,
            StaticFieldV2LexicalBlockerKind.TypeMemberName);
        AssertShadowed(
            world,
            MethodHostRid,
            Lexical(world, MethodHostRid, scopes),
            scopes,
            StaticFieldV2LexicalBlockerKind.TypeMemberName);
        AssertShadowed(
            world,
            InnerRid,
            Lexical(world, InnerRid, scopes),
            scopes,
            StaticFieldV2LexicalBlockerKind.TypeMemberName);
        AssertShadowed(
            world,
            NestHostRid,
            Lexical(world, NestHostRid, scopes),
            scopes,
            StaticFieldV2LexicalBlockerKind.NestedTypeName);
    }

    /// <summary>
    /// Proves every physical incompleteness source makes the whole certificate partial rather than absent, names the
    /// exact evidence gap, and refuses to bind any bare root.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Each_incompleteness_source_yields_partial_and_no_binding()
    {
        var world = World();
        var scopes = UsingStaticChain(world);

        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, locals: [string.Empty]),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalVariable,
            StaticFieldV2LexicalIncompletenessSource.UnnamedLocalSlot);
        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, constants: [string.Empty]),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalConstant,
            StaticFieldV2LexicalIncompletenessSource.UnnamedLocalSlot);
        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, locals: ["kept"], extraLocalSlots: 1),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalVariable,
            StaticFieldV2LexicalIncompletenessSource.UnaccountedLocalSlot);
        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, activeScope: false),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalVariable,
            StaticFieldV2LexicalIncompletenessSource.ActiveScopeUnavailable);
        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, parameters: [string.Empty]),
            scopes,
            StaticFieldV2LexicalBlockerKind.Parameter,
            StaticFieldV2LexicalIncompletenessSource.StrippedParameterName);
        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, typeParameters: [string.Empty]),
            scopes,
            StaticFieldV2LexicalBlockerKind.TypeParameter,
            StaticFieldV2LexicalIncompletenessSource.UnnamedTypeParameter);
        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes, localFunctions: ["Helper"], unattributedLocalFunction: true),
            scopes,
            StaticFieldV2LexicalBlockerKind.LocalFunction,
            StaticFieldV2LexicalIncompletenessSource.UnattributedCompilerGeneratedName);
        AssertPartial(
            world,
            HostRid,
            Lexical(world, HostRid, scopes),
            scopes,
            StaticFieldV2LexicalBlockerKind.TypeMemberName,
            StaticFieldV2LexicalIncompletenessSource.MemberCatalogMissing,
            fieldCatalogs: default(ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity>));
    }

    /// <summary>
    /// Proves a directly declared member of the selected or an enclosing type refuses the import through the
    /// certificate, while an inherited static field of the selected type is selected ahead of the same import.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Declaring_chain_members_outrank_an_active_using_static_import()
    {
        var world = World();
        var scopes = UsingStaticChain(world);

        var directOutcome = Bind(world, MemberHostRid, "Current", scopes);
        Assert.Equal(StaticFieldV2BareRootResultKind.Shadowed, directOutcome.ResultKind);
        Assert.Empty(directOutcome.ImportCandidates);
        Assert.Empty(directOutcome.DeclaringChainLookups);

        var inheritedOutcome = Bind(world, InheritHostRid, "Current", scopes);
        Assert.Equal(StaticFieldV2BareRootResultKind.Exact, inheritedOutcome.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.None, inheritedOutcome.Issue);
        Assert.Equal(StaticFieldV2BareRootSource.DeclaringTypeChain, inheritedOutcome.Source);
        Assert.Equal(1, inheritedOutcome.SelectedField!.LevelIndex);
        Assert.Equal(
            TypeToken(InheritBaseRid),
            inheritedOutcome.SelectedField.DeclaringTypeDefinition.TypeDefinitionToken);
        Assert.Equal(
            TypeToken(InheritHostRid),
            inheritedOutcome.SelectedOwnerTypeDefinition!.TypeDefinitionToken);
        Assert.Empty(inheritedOutcome.ImportCandidates);
        Assert.Equal(
            StaticFieldV2LexicalCertificateResultKind.Complete,
            inheritedOutcome.Certificate.ResultKind);
    }

    /// <summary>
    /// Proves an imported owner contributes only its directly declared accessible static fields; an inherited
    /// same-name declaration is retained as a refused candidate and never becomes an imported answer.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Using_static_imports_only_directly_declared_members()
    {
        var world = World();
        var scopes = Chain([UsingStaticImport("Lexical.App.ImportedDerived", TypeToken(ImportedDerivedRid))]);

        var direct = Bind(world, HostRid, "Direct", scopes);
        Assert.Equal(StaticFieldV2BareRootResultKind.Exact, direct.ResultKind);
        Assert.Equal(0, direct.SelectedField!.LevelIndex);
        Assert.Equal(
            TypeToken(ImportedDerivedRid),
            direct.SelectedField.DeclaringTypeDefinition.TypeDefinitionToken);

        var inherited = Bind(world, HostRid, "Inherited", scopes);
        Assert.Equal(StaticFieldV2BareRootResultKind.Absent, inherited.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.DeclarationAbsent, inherited.Issue);
        Assert.Null(inherited.SelectedField);
        var refused = Assert.Single(inherited.ImportCandidates);
        Assert.True(refused.IsInheritedAndRefused);
        Assert.False(refused.IsAccepted);
        Assert.Equal(StaticFieldV2MemberLookupResultKind.Exact, refused.Lookup.ResultKind);
        Assert.Equal(1, refused.Lookup.SelectedCandidate!.LevelIndex);
        Assert.Equal(
            TypeToken(ImportedBaseRid),
            refused.Lookup.SelectedCandidate.DeclaringTypeDefinition.TypeDefinitionToken);
    }

    /// <summary>
    /// Proves two imports naming the same physical field converge on one exact answer while two imports naming
    /// different physical fields are ambiguous rather than ordered by import position.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Converging_imports_are_exact_and_diverging_imports_are_ambiguous()
    {
        var world = World();
        var converging = Bind(
            world,
            HostRid,
            "Current",
            Chain(
                [UsingStaticImport("Lexical.App.ImportOwnerA", TypeToken(ImportOwnerARid))],
                [UsingStaticImport("Lexical.App.ImportOwnerA", TypeToken(ImportOwnerARid))]));

        Assert.Equal(StaticFieldV2BareRootResultKind.Exact, converging.ResultKind);
        Assert.Equal(2, converging.ImportCandidates.Length);
        Assert.All(converging.ImportCandidates, static entry => Assert.True(entry.IsAccepted));
        Assert.Equal(
            converging.ImportCandidates[0].Lookup.SelectedCandidate!.FieldRow,
            converging.ImportCandidates[1].Lookup.SelectedCandidate!.FieldRow);

        var diverging = Bind(
            world,
            HostRid,
            "Current",
            Chain(
            [
                UsingStaticImport("Lexical.App.ImportOwnerA", TypeToken(ImportOwnerARid)),
                UsingStaticImport("Lexical.App.ImportOwnerB", TypeToken(ImportOwnerBRid)),
            ]));

        Assert.Equal(StaticFieldV2BareRootResultKind.Ambiguous, diverging.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.AmbiguousImportedDeclarations, diverging.Issue);
        Assert.Null(diverging.SelectedField);
        Assert.Equal(2, diverging.ImportCandidates.Length);
    }

    /// <summary>
    /// Proves a same-name instance member truthfully blocks both the declaring chain and an imported owner, and an
    /// imported owner declaring the name twice is ambiguous rather than silently skipped.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Same_name_unsupported_members_block_truthfully()
    {
        var world = World();
        var scopes = UsingStaticChain(world);

        var declaringChain = Bind(world, InstanceHostRid, "Current", scopes);
        Assert.Equal(StaticFieldV2BareRootResultKind.HiddenByUnsupportedMember, declaringChain.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.HiddenByUnsupportedMember, declaringChain.Issue);
        Assert.Equal(StaticFieldV2BareRootSource.DeclaringTypeChain, declaringChain.Source);
        Assert.Null(declaringChain.SelectedField);
        Assert.Empty(declaringChain.ImportCandidates);

        var importedBlock = Bind(
            world,
            HostRid,
            "Blocked",
            Chain([UsingStaticImport("Lexical.App.HiddenOwner", TypeToken(HiddenOwnerRid))]));
        Assert.Equal(StaticFieldV2BareRootResultKind.HiddenByUnsupportedMember, importedBlock.ResultKind);
        Assert.Equal(StaticFieldV2BareRootSource.UsingStaticImport, importedBlock.Source);

        var importedAmbiguity = Bind(
            world,
            HostRid,
            "Twin",
            Chain([UsingStaticImport("Lexical.App.AmbiguousOwner", TypeToken(AmbiguousOwnerRid))]));
        Assert.Equal(StaticFieldV2BareRootResultKind.Ambiguous, importedAmbiguity.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.AmbiguousDeclarations, importedAmbiguity.Issue);
        Assert.Equal(StaticFieldV2BareRootSource.UsingStaticImport, importedAmbiguity.Source);
    }

    /// <summary>
    /// Proves absence is claimable only over an exhaustive scoped context whose every active <c>using static</c>
    /// target resolved exactly; otherwise the answer is a prefix-free non-exact stop, never an absence.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Absence_is_claimable_only_over_exhaustive_scoped_evidence()
    {
        var world = World();

        var absent = Bind(world, HostRid, "Missing", UsingStaticChain(world));
        Assert.Equal(StaticFieldV2BareRootResultKind.Absent, absent.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.DeclarationAbsent, absent.Issue);
        Assert.Null(absent.Source);
        Assert.Single(absent.ImportCandidates);

        var unresolved = Bind(
            world,
            HostRid,
            "Missing",
            Chain([UsingStaticImport("System.Missing", MissingTypeReferenceToken)]));
        Assert.Equal(StaticFieldV2BareRootResultKind.NonExact, unresolved.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.UnresolvedUsingStaticTargetRetained, unresolved.Issue);
        Assert.Empty(unresolved.ImportCandidates);
        Assert.Empty(unresolved.DeclaringChainLookups);

        var nonExhaustive = Bind(
            world,
            HostRid,
            "Missing",
            Chain([UnsupportedImport(0x7B)]));
        Assert.Equal(StaticFieldV2BareRootResultKind.NonExact, nonExhaustive.ResultKind);
        Assert.Equal(
            StaticFieldV2BareRootIssue.AbsenceNotClaimableOverRetainedEvidence,
            nonExhaustive.Issue);

        var crossModule = Bind(
            world,
            HostRid,
            "LibCurrent",
            Chain([UsingStaticImport("Lexical.Lib.LibOwner", LibOwnerTypeReferenceToken)]));
        Assert.Equal(StaticFieldV2BareRootResultKind.Exact, crossModule.ResultKind);
        Assert.Equal(world.Lib.Module, crossModule.SelectedOwnerModule);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeReferenceResolved,
            crossModule.ImportCandidates[0].ContributingImport.TargetDisposition);
    }

    /// <summary>Proves every certificate prerequisite stop is typed, prefix-free, and retains no blocker row.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Certificate_prerequisite_stops_are_typed_and_prefix_free()
    {
        var world = World();
        var scopes = UsingStaticChain(world);
        var evidence = Lexical(world, HostRid, scopes);

        AssertCertificateStop(
            CertifyWith(world, HostRid, "Current", NonExactEvidence(world, scopes), world.Ancestry, world.FieldCatalogs),
            StaticFieldV2LexicalCertificateResultKind.NonExact,
            StaticFieldV2LexicalCertificateIssue.LexicalEvidenceNonExact);
        AssertCertificateStop(
            CertifyWith(world, HostRid, "Current", evidence, NonExactAncestry(world), world.FieldCatalogs),
            StaticFieldV2LexicalCertificateResultKind.NonExact,
            StaticFieldV2LexicalCertificateIssue.AncestryPortfolioNonExact);
        AssertCertificateStop(
            CertifyWith(world, HostRid, "Current", evidence, InvalidAncestry(), world.FieldCatalogs),
            StaticFieldV2LexicalCertificateResultKind.Invalid,
            StaticFieldV2LexicalCertificateIssue.AncestryPortfolioInvalid);

        var foreignModule = W8CompilerNameMappingContractTests.CreateMetadataModule(
            0xD900,
            'd',
            "Lexical.Foreign");
        AssertCertificateStop(
            StaticFieldV2LexicalCompleteness.CertifyBlockers(StaticFieldV2LexicalCertificateRequest.Create(
                evidence,
                Identifier("Current"),
                foreignModule,
                AppType(world, HostRid),
                world.Ancestry,
                world.FieldCatalogs)),
            StaticFieldV2LexicalCertificateResultKind.Invalid,
            StaticFieldV2LexicalCertificateIssue.SelectedModuleNotInPortfolio);
        AssertCertificateStop(
            StaticFieldV2LexicalCompleteness.CertifyBlockers(StaticFieldV2LexicalCertificateRequest.Create(
                evidence,
                Identifier("Current"),
                world.App.Module,
                LibType(world, LibOwnerRid),
                world.Ancestry,
                world.FieldCatalogs)),
            StaticFieldV2LexicalCertificateResultKind.Invalid,
            StaticFieldV2LexicalCertificateIssue.SelectedTypeDefinitionNotIssued);
        AssertCertificateStop(
            Certify(world, MemberHostRid, "Current", evidence),
            StaticFieldV2LexicalCertificateResultKind.Invalid,
            StaticFieldV2LexicalCertificateIssue.SelectedMethodDeclaringTypeMismatch);
        AssertCertificateStop(
            Certify(world, ModulePseudoRid, "Current", Lexical(world, ModulePseudoRid, scopes)),
            StaticFieldV2LexicalCertificateResultKind.Unsupported,
            StaticFieldV2LexicalCertificateIssue.ModulePseudoTypeOwner);

        var flooded = Certify(world, FloodHostRid, "Current", Lexical(world, FloodHostRid, scopes));
        AssertCertificateStop(
            flooded,
            StaticFieldV2LexicalCertificateResultKind.NonExact,
            StaticFieldV2LexicalCertificateIssue.LexicalBlockerCountBoundReached);
        Assert.Equal(
            StaticFieldV2LexicalCertificateOutcome.MaximumLexicalBlockerCount + 1,
            flooded.ObservedCount);
        Assert.Equal(
            "expression-v2.context.blockers",
            Assert.IsType<EvaluationDeterministicBound>(flooded.ReachedBound).Name);
    }

    /// <summary>Proves every bare-root prerequisite stop is typed, prefix-free, and names its deciding step.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Bare_root_prerequisite_stops_are_typed_and_prefix_free()
    {
        var world = World();
        var scopes = UsingStaticChain(world);
        var evidence = Lexical(world, HostRid, scopes);
        var context = Context(world, HostRid, scopes);

        AssertBareRootStop(
            BindWith(
                CertifyRequest(world, HostRid, "Current", evidence, NonExactAncestry(world), world.FieldCatalogs),
                context),
            StaticFieldV2BareRootResultKind.NonExact,
            StaticFieldV2BareRootIssue.CertificateNonExact);
        AssertBareRootStop(
            BindWith(
                CertifyRequest(world, HostRid, "Current", evidence, InvalidAncestry(), world.FieldCatalogs),
                context),
            StaticFieldV2BareRootResultKind.Invalid,
            StaticFieldV2BareRootIssue.CertificateInvalid);
        AssertBareRootStop(
            BindWith(
                CertifyRequest(
                    world,
                    ModulePseudoRid,
                    "Current",
                    Lexical(world, ModulePseudoRid, scopes),
                    world.Ancestry,
                    world.FieldCatalogs),
                context),
            StaticFieldV2BareRootResultKind.Unsupported,
            StaticFieldV2BareRootIssue.CertificateUnsupported);

        var nonExactContext = StaticFieldV2ScopedContextBinder.ProjectContext(
            StaticFieldV2ScopedContextRequest.Create(
                world.App.Module,
                scopes,
                AppType(world, HostRid),
                world.Ancestry,
                NonExactResolution(world)));
        Assert.Equal(StaticFieldV2ScopedContextResultKind.NonExact, nonExactContext.ResultKind);
        AssertBareRootStop(
            BindWith(
                CertifyRequest(world, HostRid, "Current", evidence, world.Ancestry, world.FieldCatalogs),
                nonExactContext),
            StaticFieldV2BareRootResultKind.NonExact,
            StaticFieldV2BareRootIssue.ScopedContextNonExact);

        var invalidContext = StaticFieldV2ScopedContextBinder.ProjectContext(
            StaticFieldV2ScopedContextRequest.Create(
                world.App.Module,
                default,
                AppType(world, HostRid),
                world.Ancestry,
                world.Resolution));
        Assert.Equal(StaticFieldV2ScopedContextResultKind.Invalid, invalidContext.ResultKind);
        AssertBareRootStop(
            BindWith(
                CertifyRequest(world, HostRid, "Current", evidence, world.Ancestry, world.FieldCatalogs),
                invalidContext),
            StaticFieldV2BareRootResultKind.Invalid,
            StaticFieldV2BareRootIssue.ScopedContextInvalid);

        AssertBareRootStop(
            BindWith(
                CertifyRequest(world, HostRid, "Current", evidence, world.Ancestry, world.FieldCatalogs),
                Context(world, MethodHostRid, scopes)),
            StaticFieldV2BareRootResultKind.Invalid,
            StaticFieldV2BareRootIssue.ScopedContextSubjectMismatch);

        AssertBareRootStop(
            BindWith(
                CertifyRequest(
                    world,
                    HostRid,
                    "Current",
                    evidence,
                    world.Ancestry,
                    [world.AppFieldCatalog]),
                context),
            StaticFieldV2BareRootResultKind.NonExact,
            StaticFieldV2BareRootIssue.MemberLookupNonExact);
        AssertBareRootStop(
            BindWith(
                CertifyRequest(
                    world,
                    HostRid,
                    "Current",
                    evidence,
                    world.Ancestry,
                    DuplicateAppCatalogs(world)),
                context),
            StaticFieldV2BareRootResultKind.Invalid,
            StaticFieldV2BareRootIssue.MemberLookupInvalid);

        var pseudoImport = Chain([UsingStaticImport("Lexical.App.Module", TypeToken(ModulePseudoRid))]);
        var pseudoContext = Context(world, HostRid, pseudoImport);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeDefinitionNotIssued,
            pseudoContext.ScopeLevels[0].Imports[0].TargetDisposition);
        AssertBareRootStop(
            BindWith(
                CertifyRequest(
                    world,
                    HostRid,
                    "Current",
                    Lexical(world, HostRid, pseudoImport),
                    world.Ancestry,
                    world.FieldCatalogs),
                pseudoContext),
            StaticFieldV2BareRootResultKind.NonExact,
            StaticFieldV2BareRootIssue.UnresolvedUsingStaticTargetRetained);

        var partial = Bind(world, PartialHostRid, "Current", scopes);
        Assert.Equal(StaticFieldV2BareRootResultKind.Partial, partial.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.MemberLookupIncomplete, partial.Issue);
        Assert.Equal(StaticFieldV2BareRootSource.DeclaringTypeChain, partial.Source);
        Assert.Single(partial.DeclaringChainLookups);
        Assert.Empty(partial.ImportCandidates);
    }

    /// <summary>
    /// Proves canonical replay, defensive draft copies, guarded private issuance, the closed public surface, and
    /// emitted draft XML documentation for the whole lexical-completeness surface.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Lexical_completeness_contracts_replay_are_immutable_guarded_and_documented()
    {
        var world = World();
        var replayWorld = World();
        var outcome = Bind(world, HostRid, "Current", UsingStaticChain(world));
        var replay = Bind(replayWorld, HostRid, "Current", UsingStaticChain(replayWorld));

        Assert.Equal(outcome, replay);
        Assert.Equal(outcome.GetHashCode(), replay.GetHashCode());
        Assert.Equal(outcome.Certificate, replay.Certificate);
        Assert.Equal(outcome.Request, replay.Request);
        Assert.Equal(
            "8006dd4dcb035c0585ba9da0d70ea37937c4071152a1b57e253ef33519b1a104",
            outcome.Sha256);

        var certificate = outcome.Certificate;
        var blocker = certificate.Blockers[0];
        var candidate = outcome.ImportCandidates[0];
        var originalBytes = outcome.CanonicalBytes;
        var originalBlockers = certificate.Blockers;
        var originalCandidates = outcome.ImportCandidates;

        ImmutableCollectionsMarshal.AsArray(outcome.CanonicalBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(outcome.ImportCandidates)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(outcome.DeclaringChainLookups)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(outcome.DeclaredCoverageBoundaries)![0] =
            StaticFieldV2LexicalCoverageBoundary.UsingStaticInheritedMembersNotImported;
        ImmutableCollectionsMarshal.AsArray(certificate.Blockers)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(certificate.DeclaredCoverageBoundaries)![0] =
            StaticFieldV2LexicalCoverageBoundary.ImportedMemberGroupBlockingNotModeled;
        ImmutableCollectionsMarshal.AsArray(blocker.CanonicalBytes)![0] ^= 0x33;
        ImmutableCollectionsMarshal.AsArray(candidate.CanonicalBytes)![0] ^= 0x11;
        ImmutableCollectionsMarshal.AsArray(certificate.Request.FieldCatalogs)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(outcome.Request.FriendAssemblyGrants);

        Assert.True(originalBytes.AsSpan().SequenceEqual(outcome.CanonicalBytes.AsSpan()));
        Assert.Equal(originalBlockers[0], certificate.Blockers[0]);
        Assert.Equal(originalCandidates[0], outcome.ImportCandidates[0]);
        Assert.Equal(
            StaticFieldV2LexicalCoverageBoundary.PropertyAndEventTablesNotModeled,
            outcome.DeclaredCoverageBoundaries[0]);
        Assert.Equal(
            StaticFieldV2LexicalCoverageBoundary.PropertyAndEventTablesNotModeled,
            certificate.DeclaredCoverageBoundaries[0]);
        Assert.NotNull(certificate.Request.FieldCatalogs[0]);
        Assert.Equal(outcome.Sha256, replay.Sha256);

        Assert.False(StaticFieldV2LexicalCertificateOutcome.OwnsRowMintCapability(new object()));
        Assert.False(StaticFieldV2BareRootOutcome.OwnsRowMintCapability(new object()));
        Assert.Throws<ArgumentException>(() => StaticFieldV2LexicalBlockerRow.Create(
            new object(),
            blocker.Kind,
            blocker.Disposition,
            blocker.IncompletenessSource,
            blocker.ExaminedNameCount,
            blocker.OwningSymbolName,
            blocker.OwningSymbolMetadataToken));
        Assert.Throws<ArgumentException>(() => StaticFieldV2BareRootImportCandidate.Create(
            new object(),
            candidate.ContributingImport,
            candidate.OwnerModule,
            candidate.OwnerTypeDefinition,
            candidate.Lookup,
            candidate.IsInheritedAndRefused,
            candidate.IsAccepted));

        Assert.Throws<ArgumentException>(() => StaticFieldV2BareRootRequest.Create(
            certificate.Request,
            outcome.Request.ScopedContext,
            StaticFieldV2AccessibilityMode.UseSiteCertificate));
        Assert.Throws<ArgumentException>(() => StaticFieldV2BareRootRequest.Create(
            certificate.Request,
            outcome.Request.ScopedContext,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            null,
            [StaticFieldV2FriendAssemblyGrantIdentity.Create(world.App.Module.ContainingAssembly, [])]));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2LexicalCompleteness.CertifyBlockers(null!));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2LexicalCompleteness.BindBareStaticRoot(null!));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2LexicalBlockerKind),
            typeof(StaticFieldV2LexicalBlockerDisposition),
            typeof(StaticFieldV2LexicalIncompletenessSource),
            typeof(StaticFieldV2LexicalCertificateResultKind),
            typeof(StaticFieldV2LexicalCertificateIssue),
            typeof(StaticFieldV2LexicalCoverageBoundary),
            typeof(StaticFieldV2BareRootSource),
            typeof(StaticFieldV2BareRootResultKind),
            typeof(StaticFieldV2BareRootIssue),
            typeof(StaticFieldV2LexicalBlockerRow),
            typeof(StaticFieldV2LexicalCertificateRequest),
            typeof(StaticFieldV2LexicalCertificateOutcome),
            typeof(StaticFieldV2BareRootImportCandidate),
            typeof(StaticFieldV2BareRootRequest),
            typeof(StaticFieldV2BareRootOutcome),
            typeof(StaticFieldV2LexicalCompleteness),
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
            if (type == typeof(StaticFieldV2LexicalCompleteness))
            {
                Assert.Equal(["BindBareStaticRoot", "CertifyBlockers"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2LexicalCertificateRequest) ||
                     type == typeof(StaticFieldV2BareRootRequest))
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

    private static void AssertShadowed(
        LexicalWorld world,
        int hostRid,
        DumpSelectedMethodLexicalObservation evidence,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes,
        StaticFieldV2LexicalBlockerKind expectedKind)
    {
        var certificate = Certify(world, hostRid, "Current", evidence);
        Assert.Equal(StaticFieldV2LexicalCertificateResultKind.Shadowed, certificate.ResultKind);
        Assert.Equal(StaticFieldV2LexicalCertificateIssue.BlockerOwnsSpelling, certificate.Issue);
        Assert.Equal(expectedKind, certificate.OwningKind);
        var owning = certificate.BlockerFor(expectedKind)!;
        Assert.Equal(StaticFieldV2LexicalBlockerDisposition.CompleteAndOwned, owning.Disposition);
        Assert.Equal("Current", owning.OwningSymbolName);
        Assert.Equal(owning.OwningSymbolMetadataToken, certificate.RelatedMetadataToken);
        Assert.Equal(7, certificate.Blockers.Length);

        var outcome = BindWith(
            CertifyRequest(world, hostRid, "Current", evidence, world.Ancestry, world.FieldCatalogs),
            Context(world, hostRid, scopes));
        Assert.Equal(StaticFieldV2BareRootResultKind.Shadowed, outcome.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.CertificateShadowed, outcome.Issue);
        Assert.Null(outcome.SelectedField);
        Assert.Null(outcome.Source);
        Assert.Empty(outcome.ImportCandidates);
        Assert.Empty(outcome.DeclaringChainLookups);
    }

    private static void AssertPartial(
        LexicalWorld world,
        int hostRid,
        DumpSelectedMethodLexicalObservation evidence,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes,
        StaticFieldV2LexicalBlockerKind expectedKind,
        StaticFieldV2LexicalIncompletenessSource expectedSource,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity>? fieldCatalogs = null)
    {
        var catalogs = fieldCatalogs ?? world.FieldCatalogs;
        var request = CertifyRequest(world, hostRid, "Current", evidence, world.Ancestry, catalogs);
        var certificate = StaticFieldV2LexicalCompleteness.CertifyBlockers(request);

        Assert.Equal(StaticFieldV2LexicalCertificateResultKind.Partial, certificate.ResultKind);
        Assert.Equal(StaticFieldV2LexicalCertificateIssue.BlockerEvidenceIncomplete, certificate.Issue);
        Assert.Equal(expectedKind, certificate.FirstIncompleteKind);
        Assert.Null(certificate.OwningKind);
        var incomplete = certificate.BlockerFor(expectedKind)!;
        Assert.Equal(StaticFieldV2LexicalBlockerDisposition.Incomplete, incomplete.Disposition);
        Assert.Equal(expectedSource, incomplete.IncompletenessSource);
        Assert.Null(incomplete.OwningSymbolName);
        Assert.Equal(7, certificate.Blockers.Length);

        var outcome = BindWith(request, Context(world, hostRid, scopes));
        Assert.Equal(StaticFieldV2BareRootResultKind.Partial, outcome.ResultKind);
        Assert.Equal(StaticFieldV2BareRootIssue.CertificateIncomplete, outcome.Issue);
        Assert.Null(outcome.SelectedField);
        Assert.Empty(outcome.ImportCandidates);
    }

    private static void AssertCertificateStop(
        StaticFieldV2LexicalCertificateOutcome outcome,
        StaticFieldV2LexicalCertificateResultKind resultKind,
        StaticFieldV2LexicalCertificateIssue issue)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(issue, outcome.Issue);
        Assert.Empty(outcome.Blockers);
        Assert.Null(outcome.OwningKind);
        Assert.Null(outcome.FirstIncompleteKind);
        Assert.NotEmpty(outcome.DeclaredCoverageBoundaries);
    }

    private static void AssertBareRootStop(
        StaticFieldV2BareRootOutcome outcome,
        StaticFieldV2BareRootResultKind resultKind,
        StaticFieldV2BareRootIssue issue)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(issue, outcome.Issue);
        Assert.Null(outcome.SelectedField);
        Assert.Null(outcome.SelectedOwnerModule);
        Assert.Null(outcome.SelectedOwnerTypeDefinition);
        Assert.Empty(outcome.DeclaringChainLookups);
        Assert.Empty(outcome.ImportCandidates);
        Assert.NotEmpty(outcome.DeclaredCoverageBoundaries);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2LexicalCompleteness).Assembly;
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

    private static DumpExpressionIdentifier Identifier(string spelling)
    {
        var parsed = StaticFieldV2ExpressionParser.Parse(spelling);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, parsed.Status);
        var descriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(parsed.Descriptor);
        var partition = Assert.Single(descriptor.Partitions);
        Assert.Equal(StaticFieldV2CandidateKind.BareMember, partition.CandidateKind);
        return descriptor.Segments[partition.FieldSegmentIndex].Identifier;
    }

    private static StaticFieldV2LexicalCertificateRequest CertifyRequest(
        LexicalWorld world,
        int hostRid,
        string spelling,
        DumpSelectedMethodLexicalObservation evidence,
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs) =>
        StaticFieldV2LexicalCertificateRequest.Create(
            evidence,
            Identifier(spelling),
            world.App.Module,
            AppType(world, hostRid),
            ancestry,
            fieldCatalogs);

    private static StaticFieldV2LexicalCertificateOutcome CertifyWith(
        LexicalWorld world,
        int hostRid,
        string spelling,
        DumpSelectedMethodLexicalObservation evidence,
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs) =>
        StaticFieldV2LexicalCompleteness.CertifyBlockers(
            CertifyRequest(world, hostRid, spelling, evidence, ancestry, fieldCatalogs));

    private static StaticFieldV2LexicalCertificateOutcome Certify(
        LexicalWorld world,
        int hostRid,
        string spelling,
        DumpSelectedMethodLexicalObservation evidence) =>
        CertifyWith(world, hostRid, spelling, evidence, world.Ancestry, world.FieldCatalogs);

    private static StaticFieldV2BareRootOutcome BindWith(
        StaticFieldV2LexicalCertificateRequest certificateRequest,
        StaticFieldV2ScopedContextOutcome context) =>
        StaticFieldV2LexicalCompleteness.BindBareStaticRoot(StaticFieldV2BareRootRequest.Create(
            certificateRequest,
            context,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass));

    private static StaticFieldV2BareRootOutcome Bind(
        LexicalWorld world,
        int hostRid,
        string spelling,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes) =>
        BindWith(
            CertifyRequest(
                world,
                hostRid,
                spelling,
                Lexical(world, hostRid, scopes),
                world.Ancestry,
                world.FieldCatalogs),
            Context(world, hostRid, scopes));

    private static StaticFieldV2ScopedContextOutcome Context(
        LexicalWorld world,
        int hostRid,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes) =>
        StaticFieldV2ScopedContextBinder.ProjectContext(StaticFieldV2ScopedContextRequest.Create(
            world.App.Module,
            scopes,
            AppType(world, hostRid),
            world.Ancestry,
            world.Resolution));

    private static MetadataTypeDefinitionAuthorityIdentity AppType(LexicalWorld world, int rid) =>
        world.App.Authority.TypeDefinitions.Single(candidate =>
            candidate.TypeDefinitionToken == TypeToken(rid));

    private static MetadataTypeDefinitionAuthorityIdentity LibType(LexicalWorld world, int rid) =>
        world.Lib.Authority.TypeDefinitions.Single(candidate =>
            candidate.TypeDefinitionToken == TypeToken(rid));

    private static ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> DuplicateAppCatalogs(
        LexicalWorld world)
    {
        for (var index = 0; index < world.FieldCatalogs.Length; index++)
        {
            if (!world.FieldCatalogs[index].Equals(world.AppFieldCatalog))
            {
                return world.FieldCatalogs.SetItem(index, world.AppFieldCatalog);
            }
        }
        return world.FieldCatalogs;
    }

    private static MetadataAncestryAuthorityPortfolioIdentity NonExactAncestry(LexicalWorld world)
    {
        var portfolio = MetadataAncestryAuthorityPortfolioIdentity.Create(NonExactResolution(world));
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.NonExact, portfolio.ResultKind);
        return portfolio;
    }

    private static MetadataTypeReferenceResolutionPortfolioIdentity NonExactResolution(LexicalWorld world)
    {
        var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                    ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
                [world.Core.ChainCatalog]),
            [world.Core.Tables]);
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.NonExact, resolution.ResultKind);
        return resolution;
    }

    private static MetadataAncestryAuthorityPortfolioIdentity InvalidAncestry()
    {
        var first = W8MetadataAncestryAuthorityContractTests.BuildCoreModule();
        var second = W8MetadataAncestryAuthorityContractTests.BuildCoreModule(
            assemblyName: "Synthetic.Core2",
            moduleAddress: 0xDB00,
            digest: '9');
        var world = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(first, second);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Invalid, world.Ancestry.ResultKind);
        return world.Ancestry;
    }

    private static ImmutableArray<DumpPortablePdbImportScopeIdentity> UsingStaticChain(LexicalWorld world)
    {
        Assert.NotNull(world);
        return Chain([UsingStaticImport("Lexical.App.ImportOwnerA", TypeToken(ImportOwnerARid))]);
    }

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

    private static ImportSpec UsingStaticImport(string target, int targetTypeToken) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.UsingStatic(
            scopeToken,
            ordinal,
            3,
            target,
            targetTypeToken,
            [0xAB, (byte)ordinal]);

    private static ImportSpec UnsupportedImport(byte rawKind) =>
        (scopeToken, ordinal) => DumpPortablePdbImportFact.UnsupportedRaw(
            scopeToken,
            ordinal,
            rawKind,
            [0xAB, (byte)ordinal]);

    private static int ScopeToken(int rowId) => 0x3500_0000 | rowId;

    private static int TypeToken(int rowId) => 0x0200_0000 | rowId;

    private static DumpSelectedMethodLexicalObservation NonExactEvidence(
        LexicalWorld world,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes) =>
        DumpSelectedMethodLexicalObservation.Partial(
            Lexical(world, HostRid, scopes).Facts!.PortablePdbContext.SelectedFrame,
            DumpContextEvidenceIssue.SourceIncomplete,
            []);

    private static DumpSelectedMethodLexicalObservation Lexical(
        LexicalWorld world,
        int declaringTypeRid,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes,
        string[]? parameters = null,
        string[]? locals = null,
        string[]? constants = null,
        string[]? typeParameters = null,
        string[]? localFunctions = null,
        bool unattributedLocalFunction = false,
        bool activeScope = true,
        int extraLocalSlots = 0)
    {
        Assert.NotNull(world);
        parameters ??= [];
        locals ??= [];
        constants ??= [];
        typeParameters ??= [];
        localFunctions ??= [];

        const int selectedMethodToken = 0x0600_0001;
        const int localScopeToken = 0x3200_0001;
        const int ilOffset = 10;
        var declaringTypeToken = TypeToken(declaringTypeRid);
        var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
        var runtimeModule = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: 0x1000,
            ModuleAddress: 0x2000,
            ImageBase: 0x0040_0000,
            ImageSize: 0x0001_8000);
        var moduleContent = ModuleContentIdentity.FromDigest(
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            metadataLength: 24_576,
            new string('b', 64));
        var frame = DumpSelectedFrameIdentity.Create(
            DumpSelectedFrameSelector.Create(snapshot, threadOrdinal: 1, frameOrdinal: 2),
            managedThreadId: 37,
            runtimeThreadAddress: 0x7000,
            stackPointer: 0x7FFF_0000,
            runtimeModule,
            moduleContent,
            selectedMethodToken,
            declaringTypeToken,
            declaringNamespace: "Lexical.App",
            DumpInstructionLocation.Create(0x0040_1234, ilOffset));
        var debugIdentity = DumpPortablePdbDebugIdentity.Create(
            Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
            stamp: 0x5A17_C0DE);
        var moduleDebug = DumpModulePortablePdbDebugIdentity.Create(runtimeModule, moduleContent, debugIdentity);
        var artifact = DumpPortablePdbArtifactIdentity.Create(
            DumpPortablePdbContentIdentity.Create(31_744, new string('c', 64)),
            debugIdentity);
        int? scopeImportToken = importScopes.IsDefaultOrEmpty
            ? null
            : importScopes[^1].ImportScopeToken;
        var scopeStart = activeScope ? 0 : 50;
        var scopeLength = activeScope ? 100 : 10;
        var pdbScopes = activeScope
            ? ImmutableArray.Create(DumpPortablePdbLocalScopeIdentity.Create(
                localScopeToken,
                selectedMethodToken,
                scopeImportToken,
                scopeStart,
                scopeLength,
                nestingDepth: 0))
            : ImmutableArray<DumpPortablePdbLocalScopeIdentity>.Empty;
        var pdbFacts = DumpPortablePdbContextFacts.Acquire(
            frame,
            moduleDebug,
            artifact,
            methodDebugInformationToken: 0x3100_0001,
            document: null,
            pdbScopes,
            importScopes.IsDefault ? [] : importScopes);

        var parameterRows = ImmutableArray.CreateBuilder<DumpLexicalParameterRowFact>(parameters.Length);
        for (var index = 0; index < parameters.Length; index++)
        {
            parameterRows.Add(DumpLexicalParameterRowFact.Create(
                0x0800_0000 | (index + 1),
                index + 1,
                ParameterAttributes.None,
                parameters[index]));
        }

        var methods = ImmutableArray.CreateBuilder<DumpLexicalMethodDefinitionRowFact>(1 + localFunctions.Length);
        methods.Add(DumpLexicalMethodDefinitionRowFact.Create(
            selectedMethodToken,
            declaringTypeToken,
            relativeVirtualAddress: 0x20,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            "Run",
            MethodSignature(typeParameters.Length, parameters.Length),
            parameters.Length,
            parameterListStartRowId: 1,
            parameterRows.MoveToImmutable()));
        for (var index = 0; index < localFunctions.Length; index++)
        {
            var suffix = unattributedLocalFunction ? $"|{index}" : $"|0_{index}";
            methods.Add(DumpLexicalMethodDefinitionRowFact.Create(
                0x0600_0000 | (index + 2),
                declaringTypeToken,
                relativeVirtualAddress: 0x40 + index * 0x10,
                MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                $"<Run>g__{localFunctions[index]}{suffix}",
                MethodSignature(0, 0),
                0,
                parameterListStartRowId: parameters.Length + 1,
                []));
        }

        var methodGenericParameters = ImmutableArray.CreateBuilder<DumpLexicalGenericParameterRowFact>(
            typeParameters.Length);
        for (var index = 0; index < typeParameters.Length; index++)
        {
            methodGenericParameters.Add(DumpLexicalGenericParameterRowFact.Create(
                0x2A00_0000 | (index + 1),
                selectedMethodToken,
                index,
                GenericParameterAttributes.None,
                typeParameters[index]));
        }

        var slotCount = locals.Length + extraLocalSlots;
        var localSignature = slotCount == 0 ? null : LocalSignature(slotCount);
        var variables = ImmutableArray.CreateBuilder<DumpLexicalLocalVariableRowFact>(locals.Length);
        for (var index = 0; index < locals.Length; index++)
        {
            variables.Add(DumpLexicalLocalVariableRowFact.Create(
                0x3300_0000 | (index + 1),
                localScopeToken,
                index,
                LocalVariableAttributes.None,
                locals[index]));
        }
        var constantRows = ImmutableArray.CreateBuilder<DumpLexicalLocalConstantRowFact>(constants.Length);
        for (var index = 0; index < constants.Length; index++)
        {
            constantRows.Add(DumpLexicalLocalConstantRowFact.Create(
                0x3400_0000 | (index + 1),
                localScopeToken,
                constants[index],
                [0x08, (byte)(index + 1)]));
        }

        var scopeRows = ImmutableArray.Create(DumpLexicalLocalScopeRowFact.Create(
            localScopeToken,
            selectedMethodToken,
            scopeImportToken,
            scopeStart,
            scopeLength,
            variableListStartRowId: 1,
            constantListStartRowId: 1));
        var methodRows = methods.MoveToImmutable();
        var constantSignatures = constantRows.ToImmutable();
        var sourceEnds = DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: methodRows.Length,
            parameterRowCount: parameters.Length,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: typeParameters.Length,
            localScopeRowCount: scopeRows.Length,
            localVariableRowCount: locals.Length,
            localConstantRowCount: constants.Length,
            maximumMethodSignatureByteCount: methodRows.Max(static method => method.Signature.Length),
            aggregateMethodSignatureByteCount: methodRows.Sum(static method => (long)method.Signature.Length),
            maximumSignatureTypeDepth: Math.Max(
                methodRows.Max(static method => method.SignatureTypeDepth),
                localSignature?.SignatureTypeDepth ?? 0),
            maximumSignatureTypeNodeCount: Math.Max(
                methodRows.Max(static method => method.SignatureTypeNodeCount),
                localSignature?.SignatureTypeNodeCount ?? 0),
            standaloneSignatureByteCount: localSignature?.Signature.Length ?? 0,
            localSlotCount: localSignature?.LocalSlotCount ?? 0,
            maximumLocalConstantSignatureByteCount: constantSignatures.IsEmpty
                ? 0
                : constantSignatures.Max(static constant => constant.Signature.Length),
            aggregateLocalConstantSignatureByteCount: constantSignatures.Sum(
                static constant => (long)constant.Signature.Length),
            generatedLocalFunctionRowCount: localFunctions.Length);

        var observation = DumpSelectedMethodLexicalObservation.Acquire(
            pdbFacts,
            sourceEnds,
            methodRows,
            [],
            methodGenericParameters.MoveToImmutable(),
            localSignature,
            scopeRows,
            variables.MoveToImmutable(),
            constantSignatures);
        Assert.Equal(DumpContextEvidenceStatus.Exact, observation.Status);
        return observation;
    }

    private static ImmutableArray<byte> MethodSignature(int genericParameterCount, int parameterCount)
    {
        var builder = ImmutableArray.CreateBuilder<byte>();
        if (genericParameterCount > 0)
        {
            builder.Add(0x10);
            builder.Add(checked((byte)genericParameterCount));
        }
        else
        {
            builder.Add(0x00);
        }
        builder.Add(checked((byte)parameterCount));
        builder.Add(0x01);
        for (var index = 0; index < parameterCount; index++)
        {
            builder.Add(0x08);
        }
        return builder.ToImmutable();
    }

    private static DumpLexicalStandaloneSignatureFact LocalSignature(int slotCount)
    {
        var builder = ImmutableArray.CreateBuilder<byte>(2 + slotCount);
        builder.Add(0x07);
        builder.Add(checked((byte)slotCount));
        for (var index = 0; index < slotCount; index++)
        {
            builder.Add(0x08);
        }
        return DumpLexicalStandaloneSignatureFact.Create(0x1100_0001, slotCount, builder.MoveToImmutable());
    }

    private static LexicalWorld World()
    {
        var core = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0x9000, '1', "Lexical.Core"),
            [
                Type("System", "Object", PublicClass, null),
                Type("System", "ValueType", PublicClass, TypeToken(2)),
                Type("System", "Enum", PublicClass, TypeToken(3)),
                Type("System", "Delegate", PublicClass, TypeToken(2)),
                Type("System", "MulticastDelegate", PublicClass, TypeToken(5)),
            ]);
        var lib = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0x9100, '2', "Lexical.Lib"),
            [
                Type(
                    "Lexical.Lib",
                    "LibOwner",
                    PublicClass,
                    ObjectTypeReferenceToken,
                    fields: [Field("LibCurrent", FieldPublic | FieldStatic)]),
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
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Lexical.Core"),
            ]);

        var appTypes = ImmutableArray.CreateBuilder<LexicalTypeRow>();
        appTypes.Add(Type("Lexical.App", "Host", PublicClass, ObjectTypeReferenceToken));
        appTypes.Add(Type(
            "Lexical.App",
            "MemberHost",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Current", FieldPublic | FieldStatic)]));
        appTypes.Add(Type("Lexical.App", "NestHost", PublicClass, ObjectTypeReferenceToken));
        appTypes.Add(Type(
            string.Empty,
            "Current",
            NestedPrivateClass,
            ObjectTypeReferenceToken,
            enclosingTypeRowId: NestHostRid));
        appTypes.Add(Type(
            "Lexical.App",
            "InheritBase",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Current", FieldPublic | FieldStatic)]));
        appTypes.Add(Type("Lexical.App", "InheritHost", PublicClass, TypeToken(InheritBaseRid)));
        appTypes.Add(Type(
            "Lexical.App",
            "InstanceBase",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Current", FieldPublic)]));
        appTypes.Add(Type("Lexical.App", "InstanceHost", PublicClass, TypeToken(InstanceBaseRid)));
        appTypes.Add(Type(
            "Lexical.App",
            "MethodHost",
            PublicClass,
            ObjectTypeReferenceToken,
            methods: [Method("Current", MethodPublicStatic)]));
        appTypes.Add(Type("Lexical.App", "PartialHost", PublicClass, MissingTypeReferenceToken));
        appTypes.Add(Type(
            "Lexical.App",
            "Outer",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Current", FieldPublic | FieldStatic)]));
        appTypes.Add(Type(
            string.Empty,
            "Inner",
            NestedPrivateClass,
            ObjectTypeReferenceToken,
            enclosingTypeRowId: OuterRid));
        var floodFields = ImmutableArray.CreateBuilder<LexicalFieldSpec>(FloodFieldCount);
        for (var index = 0; index < FloodFieldCount; index++)
        {
            floodFields.Add(Field($"Flood{index}", FieldPublic | FieldStatic));
        }
        appTypes.Add(Type(
            "Lexical.App",
            "FloodHost",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: floodFields.MoveToImmutable()));
        appTypes.Add(Type(
            "Lexical.App",
            "ImportOwnerA",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Current", FieldPublic | FieldStatic)]));
        appTypes.Add(Type(
            "Lexical.App",
            "ImportOwnerB",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Current", FieldPublic | FieldStatic)]));
        appTypes.Add(Type(
            "Lexical.App",
            "ImportedBase",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Inherited", FieldPublic | FieldStatic)]));
        appTypes.Add(Type(
            "Lexical.App",
            "ImportedDerived",
            PublicClass,
            TypeToken(ImportedBaseRid),
            fields: [Field("Direct", FieldPublic | FieldStatic)]));
        appTypes.Add(Type(
            "Lexical.App",
            "AmbiguousOwner",
            PublicClass,
            ObjectTypeReferenceToken,
            fields:
            [
                Field("Twin", FieldPublic | FieldStatic),
                Field("Twin", FieldPublic | FieldStatic),
            ]));
        appTypes.Add(Type(
            "Lexical.App",
            "HiddenOwner",
            PublicClass,
            ObjectTypeReferenceToken,
            fields: [Field("Blocked", FieldPublic)]));

        var app = BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(0x9200, '3', "Lexical.App"),
            appTypes.ToImmutable(),
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
                    "Lexical.Lib",
                    "LibOwner",
                    0x2300_0002),
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module,
                    3,
                    "System",
                    "Missing",
                    0x2300_0001),
            ],
            assemblyReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, "Lexical.Core"),
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 2, "Lexical.Lib"),
            ]);

        var compatibility = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [core.Compatibility, lib.Compatibility, app.Compatibility]);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.Exact, compatibility.ResultKind);
        var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibility,
            [core.ChainCatalog, lib.ChainCatalog, app.ChainCatalog]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, chainPortfolio.ResultKind);
        var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
            chainPortfolio,
            [core.Tables, lib.Tables, app.Tables]);
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, resolution.ResultKind);
        var ancestry = MetadataAncestryAuthorityPortfolioIdentity.Create(resolution);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestry.ResultKind);

        var byModule = new[] { core, lib, app }.ToDictionary(static built => built.Module);
        var catalogs = ImmutableArray.CreateRange(
            ancestry.Entries.Select(entry => byModule[entry.SourceModule].FieldCatalog));
        return new LexicalWorld(core, lib, app, resolution, ancestry, catalogs, app.FieldCatalog);
    }

    private static LexicalModule BuildModule(
        StaticFieldMetadataModuleIdentity module,
        ImmutableArray<LexicalTypeRow> namedTypes,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataTypeReferenceRowObservationIdentity>>? typeReferences = null,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>>? assemblyReferences = null)
    {
        var typeReferenceRows = typeReferences?.Invoke(module) ?? [];
        var assemblyReferenceRows = assemblyReferences?.Invoke(module) ?? [];
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
                typeSpecificationRowCount: 0,
                assemblyReferenceRowCount: assemblyReferenceRows.Length,
                methodDefinitionRowCount: methodObservations.Count,
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
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
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
        return new LexicalModule(module, authority, fieldCatalog, compatibility, chainCatalog, tables);
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

    private static LexicalTypeRow Type(
        string namespaceName,
        string typeName,
        int typeAttributes,
        int? extendsMetadataToken,
        ImmutableArray<LexicalFieldSpec> fields = default,
        ImmutableArray<LexicalMethodSpec> methods = default,
        int? enclosingTypeRowId = null) =>
        new(
            namespaceName,
            typeName,
            typeAttributes,
            extendsMetadataToken,
            fields.IsDefault ? [] : fields,
            methods.IsDefault ? [] : methods,
            enclosingTypeRowId);

    private static LexicalFieldSpec Field(string name, int attributes) => new(name, attributes);

    private static LexicalMethodSpec Method(string name, int attributes) => new(name, attributes);

    private delegate DumpPortablePdbImportFact ImportSpec(int scopeToken, int ordinal);

    private sealed record LexicalFieldSpec(string Name, int Attributes);

    private sealed record LexicalMethodSpec(string Name, int Attributes);

    private sealed record LexicalTypeRow(
        string NamespaceName,
        string TypeName,
        int TypeAttributes,
        int? ExtendsMetadataToken,
        ImmutableArray<LexicalFieldSpec> Fields,
        ImmutableArray<LexicalMethodSpec> Methods,
        int? EnclosingTypeRowId);

    private sealed record LexicalModule(
        StaticFieldMetadataModuleIdentity Module,
        MetadataDefinitionAuthorityCatalogIdentity Authority,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog,
        MetadataModuleReferenceTableSetIdentity Tables);

    private sealed record LexicalWorld(
        LexicalModule Core,
        LexicalModule Lib,
        LexicalModule App,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs,
        MetadataFieldDefinitionTableCatalogIdentity AppFieldCatalog);
}
