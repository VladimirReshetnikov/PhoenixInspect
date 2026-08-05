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

/// <summary>Exercises the composed no-fallback static-field V2 pipeline and its strict outcome axes.</summary>
public sealed class W8V2ExpressionPipelineTests
{
    private const int PublicClass = 0x0000_0001;

    private const int FieldPublic = (int)FieldAttributes.Public;
    private const int FieldStatic = (int)FieldAttributes.Static;
    private const int FieldLiteral = (int)(FieldAttributes.Literal | FieldAttributes.HasDefault);
    private const int FieldRva = (int)FieldAttributes.HasFieldRVA;
    private const int MethodPublicStatic =
        (int)(MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig);

    private const int ReferenceTypeConstraintFlag = 0x0004;
    private const int ConstantInt32 = 0x08;

    private const string CoreAssembly = "Synthetic.PipeCore";
    private const string LibAssembly = "Synthetic.PipeLib";
    private const string AppAssembly = "Synthetic.PipeApp";

    private const int LibHolderToken = 0x0200_0002;
    private const int AppGenericSlotToken = 0x0200_0002;
    private const int AppConstrainedToken = 0x0200_0003;
    private const int AppHolderToken = 0x0200_0004;
    private const int AppImportedToken = 0x0200_0005;
    private const int AppHostToken = 0x0200_0006;

    private const int LibAssemblyReferenceToken = 0x2300_0002;

    private const int AppAliasSlotToken = 0x0200_0002;
    private const int AppAliasMarkerToken = 0x0200_0003;
    private const int GroundAliasTypeSpecToken = 0x1B00_0001;
    private const int ArityConflictTypeSpecToken = 0x1B00_0002;

    private const string ConstructedSlotGoldenSha256 =
        "1b9ddc6550ee983432f50ca7862993e7c5d291a1a3492e840b36f7e8686e78dd";

    /// <summary>
    /// Proves a fully qualified constructed-generic static field reaches an exact value while calling no context
    /// capability at all, with the absence proven by zero counters and by a poisoned scoped-context seam.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Fully_qualified_constructed_generic_reaches_a_value_with_zero_context_calls()
    {
        var world = BuildWorld();
        var probes = ExpressionV2CapabilityProbeSet.Create();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Current",
            scopedContext: PoisonedContext(),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A),
            capabilityProbes: probes));

        Assert.Equal(StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal, result.Route);
        AssertExactAxes(result);
        Assert.Equal(42, result.SignedValue);
        Assert.Equal(DumpExpressionContextOutcome.NotRequired, result.Axes.Context);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, result.Axes.TypeConstruction);
        Assert.Equal(DumpExpressionRuntimeConstructionOutcome.Exact, result.Axes.RuntimeConstruction);
        Assert.Equal(DumpExpressionStorageOutcome.Exact, result.Axes.Storage);

        // The fully qualified route proves the absence of frame and PDB calls with zero counters, not by omission.
        var ledger = result.Provenance.EvidenceLedger;
        Assert.Equal(0, ledger.ScopedContextProjectionCallCount);
        Assert.Equal(0, ledger.LexicalEnvelopeCallCount);
        Assert.Equal(0, ledger.ContextCallCount);
        Assert.Equal(1, ledger.RuntimeConstructionCandidatesCallCount);
        Assert.Equal(1, ledger.RuntimeSlotFactsCallCount);
        Assert.Equal(1, ledger.RawMemoryReadCallCount);
        Assert.Null(result.Provenance.ScopedContext);
        Assert.Null(result.Provenance.ContextualBinding);
        Assert.Null(result.Provenance.LexicalCertificate);
        Assert.Null(result.Provenance.BareRoot);

        Assert.Equal(
            StaticFieldV2StorageStrategy.ConstructedSlot,
            result.Provenance.StorageStrategy!.Strategy);
        Assert.Equal(
            AppGenericSlotToken,
            result.Provenance.RuntimeConstruction!.SelectedCandidate!.TypeDefinitionToken);
        Assert.Equal(0x5000_0040UL, result.Provenance.StaticSlot!.Slot!.SlotAddress);
        Assert.Equal([0x2A, 0x00, 0x00, 0x00], result.Provenance.RawValueBytes.ToArray());
        Assert.True(probes.RuntimeConstructionCallCount >= 1);
        Assert.True(probes.MemoryReadCallCount >= 1);
    }

    /// <summary>
    /// Proves a contextual spelling that itself carries a type argument closes its owner construction through the
    /// same scope that bound the owner, converging on the fully qualified spelling's construction identity, and that
    /// an argument the scope cannot reach stops typed instead of falling back to a metadata-global lookup.
    /// </summary>
    /// <remarks>
    /// The three spellings below share one namespace import. The first names its argument through that import, the
    /// second names the same argument with an explicit <c>global::</c> qualifier the argument itself carries, and
    /// the third names an argument the import does not reach at all. The first two must produce byte-identical owner
    /// constructions; the third must stop, and the fully qualified control proves the stop is a scope fact rather
    /// than the argument being unbindable anywhere.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Contextual_owner_arguments_bind_through_the_owner_scope_and_never_fall_back()
    {
        var world = BuildWorld();
        var qualified = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<global::Pipe.App.Holder>.Current",
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        var contextual = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "GenericSlot<Holder>.Current",
            scopedContext: NamespaceImportContext(world, "Pipe.App"),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        var mixed = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "GenericSlot<global::Pipe.App.Holder>.Current",
            scopedContext: NamespaceImportContext(world, "Pipe.App"),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));

        foreach (var result in new[] { qualified, contextual, mixed })
        {
            AssertExactAxes(result);
            Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, result.Axes.TypeConstruction);
            Assert.Equal(42, result.SignedValue);
        }

        Assert.Equal(StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal, qualified.Route);
        Assert.Equal(StaticFieldV2ExpressionRoute.Contextual, contextual.Route);
        Assert.Equal(StaticFieldV2ExpressionRoute.Contextual, mixed.Route);

        // One construction identity, three spellings; the retained binding evidence still distinguishes the routes.
        var qualifiedOwner = qualified.Provenance.OwnerConstruction!;
        var contextualOwner = contextual.Provenance.OwnerConstruction!;
        Assert.Equal(qualifiedOwner.OwnerConstruction!.Sha256, contextualOwner.OwnerConstruction!.Sha256);
        Assert.Equal(
            qualifiedOwner.OwnerConstruction.Sha256,
            mixed.Provenance.OwnerConstruction!.OwnerConstruction!.Sha256);
        Assert.Single(contextualOwner.FlattenedArguments);
        Assert.NotNull(qualifiedOwner.NameBinding);
        Assert.Null(qualifiedOwner.ContextualBinding);
        Assert.Null(contextualOwner.NameBinding);
        Assert.NotNull(contextualOwner.ContextualBinding);
        Assert.NotEqual(qualified.Sha256, contextual.Sha256);
        Assert.DoesNotContain(
            StaticFieldV2PipelineCoverageBoundary.ContextualRouteClosedConstructionDeferred,
            contextual.Provenance.DeclaredCoverageBoundaries);

        // An argument outside the imported namespace is a typed stop, never a metadata-global fallback.
        var unreachable = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "GenericSlot<LibHolder>.Current",
            scopedContext: NamespaceImportContext(world, "Pipe.App"),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, unreachable.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Partial, unreachable.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.TypeArgumentAbsent,
            unreachable.Provenance.OwnerConstruction!.Issue);
        Assert.Null(unreachable.SignedValue);

        var reachableControl = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<global::Pipe.Lib.LibHolder>.Current",
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, reachableControl.Axes.TypeConstruction);
    }

    /// <summary>
    /// Proves an extern-alias contextual route reaches the identical owner and field identity as the fully qualified
    /// spelling of the same declaration while retaining materially different provenance.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Contextual_alias_reaches_the_same_field_identity_with_different_provenance()
    {
        var world = BuildWorld();
        var qualified = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.Lib.LibHolder.LibAnswer",
            constantCatalogs: LiteralCatalogs(world, 7)));
        var aliased = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "lib::Pipe.Lib.LibHolder.LibAnswer",
            scopedContext: ExternAliasContext(world),
            constantCatalogs: LiteralCatalogs(world, 7)));

        AssertExactAxes(qualified);
        AssertExactAxes(aliased);
        Assert.Equal(StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal, qualified.Route);
        Assert.Equal(StaticFieldV2ExpressionRoute.Contextual, aliased.Route);

        // The same physical declaration, the same owner construction identity, and the same exact value.
        Assert.Equal(
            qualified.Provenance.MemberLookup!.SelectedCandidate!.FieldDefinitionToken,
            aliased.Provenance.MemberLookup!.SelectedCandidate!.FieldDefinitionToken);
        Assert.Equal(
            qualified.Provenance.MemberLookup.SelectedCandidate.DeclaringTypeDefinition,
            aliased.Provenance.MemberLookup.SelectedCandidate.DeclaringTypeDefinition);
        Assert.Equal(LibHolderToken, aliased.Provenance.MemberLookup.Request.OwnerTypeDefinitionToken);
        Assert.Equal(7, qualified.SignedValue);
        Assert.Equal(7, aliased.SignedValue);

        // The provenance differs: only the contextual route consulted context, and the digests never converge.
        Assert.Equal(DumpExpressionContextOutcome.NotRequired, qualified.Axes.Context);
        Assert.Equal(DumpExpressionContextOutcome.Exact, aliased.Axes.Context);
        Assert.Equal(0, qualified.Provenance.EvidenceLedger.ContextCallCount);
        Assert.Equal(1, aliased.Provenance.EvidenceLedger.ContextCallCount);
        Assert.NotNull(qualified.Provenance.ExplicitNameBinding);
        Assert.Null(qualified.Provenance.ContextualBinding);
        Assert.Null(aliased.Provenance.ExplicitNameBinding);
        Assert.NotNull(aliased.Provenance.ContextualBinding);
        Assert.NotEqual(qualified.Provenance.Sha256, aliased.Provenance.Sha256);
        Assert.NotEqual(qualified.Sha256, aliased.Sha256);

        // Both routes now freeze an exact owner construction, tagged with the binding evidence that produced it:
        // the explicit construction retains the name binding and the contextual construction retains the
        // contextual binding, so the constructed identities converge while their binding provenance never does.
        var qualifiedConstruction = qualified.Provenance.OwnerConstruction!;
        var aliasedConstruction = aliased.Provenance.OwnerConstruction!;
        Assert.NotNull(aliasedConstruction);
        Assert.Equal(
            qualifiedConstruction.OwnerConstruction!.Sha256,
            aliasedConstruction.OwnerConstruction!.Sha256);
        Assert.NotNull(qualifiedConstruction.NameBinding);
        Assert.Null(qualifiedConstruction.ContextualBinding);
        Assert.Null(aliasedConstruction.NameBinding);
        Assert.NotNull(aliasedConstruction.ContextualBinding);
        Assert.NotEqual(qualifiedConstruction.Sha256, aliasedConstruction.Sha256);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, aliased.Axes.TypeConstruction);
        Assert.DoesNotContain(
            StaticFieldV2PipelineCoverageBoundary.ContextualRouteClosedConstructionDeferred,
            aliased.Provenance.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves an extern-alias-qualified type argument resolves through the same scope that bound its owner, and that
    /// the identical spelling without a scoped context stays the declared unsupported stop rather than silently
    /// resolving as though the alias qualifier were not written.
    /// </summary>
    /// <remarks>
    /// The two halves matter together. Dropping an alias qualifier the metadata-global route cannot interpret would
    /// answer with whatever assembly the plain name reaches, which is exactly the confusion an extern alias exists to
    /// prevent; the fixture makes that concrete by giving two composed assemblies the same spelling and reading the
    /// value that only the aliased one holds.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Extern_alias_qualified_type_argument_binds_through_the_scope_that_bound_its_owner()
    {
        var world = BuildWorld();
        var aliased = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "GenericSlot<lib::Pipe.Dup.Twin>.Current",
            scopedContext: ImportAndExternAliasContext(world, "Pipe.App"),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));

        AssertExactAxes(aliased);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, aliased.Axes.TypeConstruction);
        Assert.Equal(42, aliased.SignedValue);

        // The argument the alias names is the library's Twin, not the application's identically spelled one.
        var argument = Assert.Single(aliased.Provenance.OwnerConstruction!.FlattenedArguments);
        Assert.Equal(
            LibAssembly,
            argument.FinalClassification!.SourceModule.ContainingAssembly.AssemblyDefinition.Name);

        // The same argument without the alias qualifier is ambiguous over the two composed modules that spell it,
        // which is what the alias exists to settle: the alias answers a name the plain spelling cannot.
        var unaliased = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "GenericSlot<Pipe.Dup.Twin>.Current",
            scopedContext: ImportAndExternAliasContext(world, "Pipe.App"),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        Assert.NotEqual(DumpExpressionTypeConstructionOutcome.Exact, unaliased.Axes.TypeConstruction);
        Assert.Null(unaliased.SignedValue);

        // Without a scoped context the same spelling has no alias to interpret and stops typed rather than dropping
        // the qualifier: an argument named through an alias the route cannot read is never resolved as a plain name.
        var unscoped = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<lib::Pipe.Dup.Twin>.Current",
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Unsupported, unscoped.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.TypeArgumentAliasUnsupported,
            unscoped.Provenance.OwnerConstruction!.Issue);
        Assert.Null(unscoped.SignedValue);
    }

    /// <summary>
    /// Proves a whole-owner alias whose Portable-PDB target is a physical TypeSpec row reaches the same construction,
    /// field, and value as the fully qualified control, and that the decode rests on evidence rather than on the
    /// spelling.
    /// </summary>
    /// <remarks>
    /// The alias spelling carries no type arguments at all — <c>S.Current</c> names a construction only the TypeSpec
    /// blob records — so this is the one contextual shape whose arguments cannot come from the expression. Three
    /// facts make the decode evidence rather than inference: withholding the token-resolution catalogs alone returns
    /// the same request to its previous typed stop, an alias whose target blob instantiates an arity-zero definition
    /// is refused instead of guessed, and the control reaches the same owner-construction digest through the
    /// explicit route while the two outcomes keep distinct binding provenance.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Whole_owner_typespec_alias_reaches_the_control_construction_from_the_decoded_target()
    {
        var aliasWorld = BuildTypeSpecAliasWorld();
        var world = aliasWorld.World;
        var aliased = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "S.Current",
            scopedContext: TypeSpecAliasContext(aliasWorld, "S", GroundAliasTypeSpecToken),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        var control = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Slot<global::Pipe.App.Marker>.Current",
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));

        AssertExactAxes(aliased);
        AssertExactAxes(control);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, aliased.Axes.TypeConstruction);
        Assert.Equal(42, aliased.SignedValue);
        Assert.Equal(42, control.SignedValue);
        Assert.Equal(StaticFieldV2ExpressionRoute.Contextual, aliased.Route);
        Assert.Equal(StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal, control.Route);

        // One construction identity and one physical declaration, reached by two materially different routes.
        var aliasedConstruction = aliased.Provenance.OwnerConstruction!;
        var controlConstruction = control.Provenance.OwnerConstruction!;
        Assert.Equal(
            controlConstruction.OwnerConstruction!.Sha256,
            aliasedConstruction.OwnerConstruction!.Sha256);
        Assert.Single(aliasedConstruction.FlattenedArguments);
        Assert.Equal(
            AppAliasMarkerToken,
            aliasedConstruction.FlattenedArguments[0].FinalClassification!.TypeDefinition.TypeDefinitionToken);
        Assert.Equal(
            control.Provenance.MemberLookup!.SelectedCandidate!.FieldDefinitionToken,
            aliased.Provenance.MemberLookup!.SelectedCandidate!.FieldDefinitionToken);
        Assert.Null(aliasedConstruction.NameBinding);
        Assert.NotNull(aliasedConstruction.ContextualBinding);
        Assert.NotNull(controlConstruction.NameBinding);
        Assert.Null(controlConstruction.ContextualBinding);
        Assert.NotEqual(control.Sha256, aliased.Sha256);

        // The alias target itself is the retained evidence: a decoded physical row, not a resolved definition token.
        var import = Assert.Single(aliased.Provenance.ScopedContext!.ScopeLevels[0].Imports);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeSpecificationResolved,
            import.TargetDisposition);
        Assert.Equal(GroundAliasTypeSpecToken, import.TargetTypeSpecificationRow!.TypeSpecificationToken);
        Assert.Equal(
            aliasedConstruction.OwnerConstruction.Sha256,
            import.TargetClosedConstruction!.Sha256);
        Assert.Equal(AppAliasSlotToken, import.TargetTypeDefinition!.TypeDefinitionToken);
        Assert.True(import.IsTargetExact);
        Assert.DoesNotContain(
            StaticFieldV2ScopedContextCoverageBoundary.AliasTypeSpecTargetNotDecoded,
            aliased.Provenance.ScopedContext.DeclaredCoverageBoundaries);

        // Counterfactual: the identical request without the token-resolution catalogs decodes nothing and returns to
        // the previously declared boundary, so the catalogs are what changed the answer.
        var withheld = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "S.Current",
            scopedContext: TypeSpecAliasContext(
                aliasWorld,
                "S",
                GroundAliasTypeSpecToken,
                supplyTokenCatalogs: false),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        Assert.Equal(DumpExpressionContextOutcome.Exact, withheld.Axes.Context);
        Assert.Equal(DumpExpressionTypeBindingOutcome.Partial, withheld.Axes.TypeBinding);
        Assert.Null(withheld.SignedValue);
        Assert.Contains(
            StaticFieldV2ScopedContextCoverageBoundary.AliasTypeSpecTargetNotDecoded,
            withheld.Provenance.ScopedContext!.DeclaredCoverageBoundaries);

        // An alias whose target blob instantiates an arity-zero definition is refused with the catalogs present.
        var conflicting = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "S.Current",
            scopedContext: TypeSpecAliasContext(aliasWorld, "S", ArityConflictTypeSpecToken),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Partial, conflicting.Axes.TypeBinding);
        Assert.Null(conflicting.SignedValue);
        var refused = Assert.Single(conflicting.Provenance.ScopedContext!.ScopeLevels[0].Imports);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeSpecificationNotDecoded,
            refused.TargetDisposition);
        Assert.Null(refused.TargetClosedConstruction);
        Assert.Contains(
            StaticFieldV2ScopedContextCoverageBoundary.AliasTypeSpecTargetNotDecoded,
            conflicting.Provenance.ScopedContext.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves a bare single-identifier root reaches an imported <c>using static</c> declaration through the lexical
    /// completeness certificate, and that the certificate is the axis that decides it.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Bare_using_static_root_binds_through_lexical_completeness()
    {
        var world = BuildWorld();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "Bare",
            scopedContext: UsingStaticContext(world),
            constantCatalogs: LiteralCatalogs(world, 11)));

        Assert.Equal(StaticFieldV2ExpressionRoute.BareStaticMember, result.Route);
        AssertExactAxes(result);
        Assert.Equal(DumpExpressionContextOutcome.Exact, result.Axes.Context);
        Assert.Equal(DumpExpressionLexicalCompletenessOutcome.Complete, result.Axes.LexicalCompleteness);
        Assert.Equal(DumpExpressionTypeBindingOutcome.NotRequired, result.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.NotRequired, result.Axes.TypeConstruction);
        Assert.Equal(11, result.SignedValue);
        Assert.Equal(
            StaticFieldV2BareRootSource.UsingStaticImport,
            result.Provenance.BareRoot!.Source);
        Assert.Equal(AppImportedToken, result.Provenance.BareRoot.SelectedOwnerTypeDefinition!.TypeDefinitionToken);
        Assert.Equal(
            StaticFieldV2LexicalCertificateResultKind.Complete,
            result.Provenance.LexicalCertificate!.ResultKind);
        Assert.Equal(1, result.Provenance.EvidenceLedger.ScopedContextProjectionCallCount);
        Assert.Equal(1, result.Provenance.EvidenceLedger.LexicalEnvelopeCallCount);
        Assert.Null(result.Provenance.MemberLookup);
    }

    /// <summary>
    /// Proves a bare root imported through a <em>constructed</em> <c>using static</c> closes the owner construction
    /// its import decoded, so the value comes from that construction's own slot rather than from a definition.
    /// </summary>
    /// <remarks>
    /// A bare spelling names no owner, which is why the bare route declared a construction boundary: nothing in the
    /// expression says what the arguments would be. A constructed import does say — in its physical TypeSpec target —
    /// so the construction is evidence rather than inference, and the boundary is retired exactly for imports that
    /// carry one. The runtime candidate the seam returns must therefore be keyed by the import's own arguments.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Bare_root_through_a_constructed_using_static_closes_the_imported_construction()
    {
        var aliasWorld = BuildTypeSpecAliasWorld();
        var world = aliasWorld.World;
        var result = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "Current",
            scopedContext: ConstructedUsingStaticContext(aliasWorld),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));

        Assert.Equal(StaticFieldV2ExpressionRoute.BareStaticMember, result.Route);
        AssertExactAxes(result);
        Assert.Equal(DumpExpressionTypeBindingOutcome.NotRequired, result.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, result.Axes.TypeConstruction);
        Assert.Equal(42, result.SignedValue);
        Assert.Equal(
            StaticFieldV2BareRootSource.UsingStaticImport,
            result.Provenance.BareRoot!.Source);

        // The construction is the import's own decoded target, and its retained evidence names that import rather
        // than a name binding or a contextual binding, so replay can tell which route closed it.
        var construction = result.Provenance.OwnerConstruction!;
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, construction.ResultKind);
        Assert.Null(construction.NameBinding);
        Assert.Null(construction.ContextualBinding);
        var importedOwner = Assert.IsType<StaticFieldV2ScopedImportProjection>(construction.ImportedOwnerBinding);
        Assert.Equal(StaticFieldV2ScopedImportKind.UsingStatic, importedOwner.Kind);
        Assert.Equal(
            StaticFieldV2ScopedImportTargetDisposition.TypeSpecificationResolved,
            importedOwner.TargetDisposition);
        Assert.Equal(importedOwner.TargetClosedConstruction!.Sha256, construction.OwnerConstruction!.Sha256);
        Assert.Equal(
            AppAliasMarkerToken,
            Assert.Single(construction.FlattenedArguments)
                .FinalClassification!.TypeDefinition.TypeDefinitionToken);
        Assert.DoesNotContain(
            StaticFieldV2PipelineCoverageBoundary.ContextualRouteClosedConstructionDeferred,
            result.Provenance.DeclaredCoverageBoundaries);

        // Withholding the catalogs leaves the same import undecoded, and the bare route then keeps its declared
        // boundary and stops before a construction, which is what makes the decoded target the decisive evidence.
        var withheld = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "Current",
            scopedContext: StaticFieldV2ScopedContextSource.Create(
                () => StaticFieldV2ScopedContextRequest.Create(
                    world.App,
                    Scopes([DumpPortablePdbImportFact.UsingStatic(
                        ScopeToken(1),
                        0,
                        3,
                        "Pipe.App.Slot`1",
                        GroundAliasTypeSpecToken,
                        [0xAB, 0x01])]),
                    world.HostType,
                    world.Ancestry,
                    world.Resolution),
                () => LexicalEnvelope(
                    Scopes([DumpPortablePdbImportFact.UsingStatic(
                        ScopeToken(1),
                        0,
                        3,
                        "Pipe.App.Slot`1",
                        GroundAliasTypeSpecToken,
                        [0xAB, 0x01])]),
                    AppAliasMarkerToken)),
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        Assert.NotEqual(DumpExpressionValueOutcome.ExactValue, withheld.Axes.Value);
        Assert.Null(withheld.SignedValue);
        Assert.Null(withheld.Provenance.OwnerConstruction);
    }

    /// <summary>
    /// Proves a metadata literal reaches an exact value end to end while every runtime, thread, module, slot, and
    /// memory capability probe is poisoned and the runtime seam is absent entirely.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Literal_reaches_a_value_with_zero_runtime_thread_slot_and_memory_calls()
    {
        var world = BuildWorld();
        var poisoned = ExpressionV2CapabilityProbeSet.Create(
            static () => throw new InvalidOperationException("runtime construction"),
            static () => throw new InvalidOperationException("thread identity"),
            static () => throw new InvalidOperationException("module content"),
            static () => throw new InvalidOperationException("static slot"),
            static () => throw new InvalidOperationException("memory read"));

        var result = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Holder.Answer",
            runtimeEvidence: PoisonedRuntimeEvidence(),
            constantCatalogs: LiteralCatalogs(world, 99),
            capabilityProbes: poisoned));

        AssertExactAxes(result);
        Assert.Equal(99, result.SignedValue);
        Assert.Equal(StaticFieldV2StorageStrategy.MetadataLiteral, result.Provenance.StorageStrategy!.Strategy);
        Assert.Equal(DumpExpressionRuntimeConstructionOutcome.NotRequired, result.Axes.RuntimeConstruction);
        Assert.Equal(DumpExpressionStorageOutcome.NotRequired, result.Axes.Storage);

        Assert.Equal(0, poisoned.TotalCallCount);
        foreach (var capability in Enum.GetValues<StaticFieldV2StorageCapability>())
        {
            Assert.Equal(0, poisoned.CallCount(capability));
        }
        var ledger = result.Provenance.EvidenceLedger;
        Assert.Equal(0, ledger.RuntimeCallCount);
        Assert.Equal(0, ledger.ContextCallCount);

        // The literal answer now calls no seam at all: its Constant row comes from a landed catalog carried by the
        // request, so the whole ledger is zero rather than merely zero on the runtime and context axes.
        Assert.True(ledger.IsZero);
        Assert.True(result.Provenance.CapabilityCallLedger!.IsZero);
        Assert.Null(result.Provenance.RuntimeConstruction);
        Assert.Null(result.Provenance.StaticSlot);
        Assert.True(result.Provenance.RawValueBytes.IsDefault);
        Assert.NotNull(result.Provenance.LiteralConstantRow);
        Assert.Equal(
            MetadataConstantParentKind.FieldDefinition,
            result.Provenance.LiteralConstantRow!.ParentKind);
    }

    /// <summary>
    /// Proves every typed stage stop forces every later axis to <c>NotReached</c> and overall completeness to
    /// <c>NoAnswer</c>, across syntax, context, name, construction, member, runtime, storage, and value.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Every_axis_stop_forces_later_axes_not_reached_and_no_answer()
    {
        var world = BuildWorld();

        var invalidSyntax = StaticFieldV2ExpressionPipeline.Evaluate(Request(world, "global::"));
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, invalidSyntax.Axes.Syntax);
        Assert.Equal(StaticFieldV2ExpressionRoute.NotSelected, invalidSyntax.Route);
        AssertNotReachedFrom(invalidSyntax, 0);

        var contextUnavailable = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "lib::Pipe.Lib.LibHolder.LibAnswer"));
        Assert.Equal(DumpExpressionContextOutcome.Unavailable, contextUnavailable.Axes.Context);
        AssertNotReachedFrom(contextUnavailable, 1);

        var nameAbsent = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Nowhere.Current"));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Absent, nameAbsent.Axes.TypeBinding);
        AssertNotReachedFrom(nameAbsent, 4);

        var nameAmbiguous = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.Dup.Twin.Current"));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Ambiguous, nameAmbiguous.Axes.TypeBinding);
        AssertNotReachedFrom(nameAmbiguous, 4);

        var constraintViolated = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Constrained<int>.Current"));
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Invalid, constraintViolated.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.ConstraintViolated,
            constraintViolated.Provenance.OwnerConstruction!.Issue);
        AssertNotReachedFrom(constraintViolated, 5);

        var memberAbsent = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Holder.Nowhere"));
        Assert.Equal(DumpExpressionMemberLookupOutcome.Absent, memberAbsent.Axes.MemberLookup);
        AssertNotReachedFrom(memberAbsent, 6);

        var memberHidden = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Holder.Twin"));
        Assert.Equal(
            DumpExpressionMemberLookupOutcome.HiddenByUnsupportedMember,
            memberHidden.Axes.MemberLookup);
        AssertNotReachedFrom(memberHidden, 6);

        var memberAmbiguous = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Holder.Duo"));
        Assert.Equal(DumpExpressionMemberLookupOutcome.Ambiguous, memberAmbiguous.Axes.MemberLookup);
        AssertNotReachedFrom(memberAmbiguous, 6);

        var runtimeAbsent = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Current",
            runtimeEvidence: StaticFieldV2RuntimeEvidenceSource.Create(
                static (_, _) => [],
                static (_, _) => null,
                static (_, _) => default)));
        Assert.Equal(DumpExpressionRuntimeConstructionOutcome.Absent, runtimeAbsent.Axes.RuntimeConstruction);
        AssertNotReachedFrom(runtimeAbsent, 7);

        var storageUnavailable = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Current",
            runtimeEvidence: StaticFieldV2RuntimeEvidenceSource.Create(
                (construction, strategy) => Candidates(world, construction, strategy),
                static (_, _) => null,
                static (_, _) => default)));
        Assert.Equal(DumpExpressionStorageOutcome.Unavailable, storageUnavailable.Axes.Storage);
        AssertNotReachedFrom(storageUnavailable, 8);

        var valueInvalid = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Current",
            runtimeEvidence: StaticFieldV2RuntimeEvidenceSource.Create(
                (construction, strategy) => Candidates(world, construction, strategy),
                static (_, _) => StaticFieldV2RuntimeSlotFacts.Create(4, 0x5000_0040UL),
                static (_, _) => [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08])));
        Assert.Equal(DumpExpressionValueOutcome.Invalid, valueInvalid.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Invalid, valueInvalid.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, valueInvalid.Axes.Completeness);
        Assert.Equal(
            StaticFieldV2RuntimeValueIssue.RawByteCountDisagreesWithDeclaredWidth,
            valueInvalid.Provenance.RuntimeValue!.Issue);

        var lexicalUnavailable = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "Bare",
            scopedContext: StaticFieldV2ScopedContextSource.Create(() => ContextRequest(world, []))));
        Assert.Equal(DumpExpressionLexicalCompletenessOutcome.Partial, lexicalUnavailable.Axes.LexicalCompleteness);
        Assert.Equal(DumpExpressionCompletenessOutcome.Partial, lexicalUnavailable.Axes.Completeness);
        AssertNotReachedFrom(lexicalUnavailable, 3, expectNoAnswer: false);
    }

    /// <summary>
    /// Proves a non-exact host context acquisition is its own typed context-axis stop: every non-exact disposition
    /// maps onto the independent context axis, the projection binder never runs over evidence the host could not
    /// establish, the metered seam count still proves the acquisition was consulted exactly once, and an exact
    /// envelope behaves identically to the always-exact request seam.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Context_acquisition_dispositions_map_onto_the_independent_context_axis()
    {
        var world = BuildWorld();
        var expectations = new[]
        {
            (StaticFieldV2ScopedContextAcquisitionDisposition.Partial, DumpExpressionContextOutcome.Partial),
            (StaticFieldV2ScopedContextAcquisitionDisposition.Unavailable, DumpExpressionContextOutcome.Unavailable),
            (StaticFieldV2ScopedContextAcquisitionDisposition.Ambiguous, DumpExpressionContextOutcome.Ambiguous),
            (StaticFieldV2ScopedContextAcquisitionDisposition.Conflict, DumpExpressionContextOutcome.Conflict),
            (StaticFieldV2ScopedContextAcquisitionDisposition.Invalid, DumpExpressionContextOutcome.Invalid),
        };
        foreach (var (disposition, outcome) in expectations)
        {
            var stopped = StaticFieldV2ExpressionPipeline.Evaluate(Request(
                world,
                "lib::Pipe.Lib.LibHolder.LibAnswer",
                scopedContext: StaticFieldV2ScopedContextSource.CreateFromAcquisition(
                    () => StaticFieldV2ScopedContextAcquisition.Stopped(disposition))));
            Assert.Equal(outcome, stopped.Axes.Context);
            Assert.Equal(1, stopped.Provenance.EvidenceLedger.ContextCallCount);
            Assert.Null(stopped.Provenance.ScopedContext);

            // A partial acquisition retains partial completeness; every other stop is a complete non-answer.
            var expectNoAnswer = disposition != StaticFieldV2ScopedContextAcquisitionDisposition.Partial;
            AssertNotReachedFrom(stopped, 1, expectNoAnswer);
            if (!expectNoAnswer)
            {
                Assert.Equal(DumpExpressionCompletenessOutcome.Partial, stopped.Axes.Completeness);
            }
        }

        // An exact acquisition envelope reaches the same terminal as the always-exact request seam.
        var exact = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "lib::Pipe.Lib.LibHolder.LibAnswer",
            scopedContext: StaticFieldV2ScopedContextSource.CreateFromAcquisition(
                () => StaticFieldV2ScopedContextAcquisition.Exact(ExternAliasContextRequest(world))),
            constantCatalogs: LiteralCatalogs(world, 7)));
        Assert.Equal(DumpExpressionContextOutcome.Exact, exact.Axes.Context);
        Assert.Equal(7, exact.SignedValue);

        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2ScopedContextSource.CreateFromAcquisition(null!));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2ScopedContextAcquisition.Exact(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StaticFieldV2ScopedContextAcquisition.Stopped(
                StaticFieldV2ScopedContextAcquisitionDisposition.Exact));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StaticFieldV2ScopedContextAcquisition.Stopped(
                (StaticFieldV2ScopedContextAcquisitionDisposition)9));
    }

    /// <summary>
    /// Proves the two profiles are isolated entry points: a static request can never produce a frame answer and a
    /// frame request can never reach the static binders, and neither rejection performs any parse.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Profiles_never_fall_through_to_one_another()
    {
        var world = BuildWorld();

        var frameThroughStatic = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Holder.Answer",
            profile: DumpExpressionProfileKind.FrameValueExpressionV1));
        var staticThroughFrame = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(Request(
            world,
            "global::Pipe.App.Holder.Answer"));
        var frameThroughFrame = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(Request(
            world,
            "this",
            profile: DumpExpressionProfileKind.FrameValueExpressionV1));

        foreach (var rejected in new[] { frameThroughStatic, staticThroughFrame, frameThroughFrame })
        {
            Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, rejected.Axes.Syntax);
            Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, rejected.Axes.Completeness);
            Assert.Equal(StaticFieldV2ExpressionRoute.NotSelected, rejected.Route);
            AssertNotReachedFrom(rejected, 0);

            // No parse, no binder, no seam call: the rejection retains nothing at all.
            Assert.Null(rejected.Provenance.Syntax);
            Assert.Null(rejected.Provenance.ExplicitNameBinding);
            Assert.Null(rejected.Provenance.MemberLookup);
            Assert.Null(rejected.Provenance.StorageStrategy);
            Assert.Null(rejected.Provenance.LiteralValue);
            Assert.True(rejected.Provenance.EvidenceLedger.IsZero);
            Assert.Contains(
                StaticFieldV2PipelineCoverageBoundary.FrameValueProfileOwnedBySeparateEntryPoint,
                rejected.Provenance.DeclaredCoverageBoundaries);
        }

        // The same static spelling still succeeds through its own entry point, so the rejection is profile-driven.
        var accepted = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Holder.Answer",
            constantCatalogs: LiteralCatalogs(world, 99)));
        AssertExactAxes(accepted);
        Assert.NotEqual(accepted.Sha256, frameThroughStatic.Sha256);
    }

    /// <summary>
    /// Proves the frozen suffix boundary: an absent suffix records <c>NotRequested</c> and the retained descriptor is
    /// the parser's own frozen suffix descriptor rather than a re-derived one.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Absent_suffix_is_not_requested_and_the_frozen_descriptor_is_retained()
    {
        var world = BuildWorld();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.Holder.Answer",
            constantCatalogs: LiteralCatalogs(world, 99)));

        Assert.Equal(DumpExpressionSuffixOutcome.NotRequested, result.Axes.Suffix);
        var suffix = Assert.IsType<DumpExpressionSuffixDescriptor>(result.Provenance.Suffix);
        Assert.Equal(DumpExpressionSuffixKind.NotRequested, suffix.Kind);
        Assert.Empty(suffix.Segments);

        var parsed = StaticFieldV2ExpressionParser.Parse("global::Pipe.App.Holder.Answer");
        Assert.Contains(
            parsed.Descriptor!.Partitions,
            partition => partition.Suffix.Equals(suffix));
        Assert.Contains(
            StaticFieldV2PipelineCoverageBoundary.SuffixEvaluationSuppliedByCallerSeam,
            result.Provenance.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves the reference-target validation stage runs only for a non-null reference with a caller-supplied target,
    /// and that a non-assignable target blocks the suffix instead of forging a complete answer.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reference_target_validation_runs_only_when_it_is_required()
    {
        var world = BuildWorld();
        var evidence = StaticFieldV2RuntimeEvidenceSource.Create(
            (construction, strategy) => Candidates(world, construction, strategy),
            static (_, _) => StaticFieldV2RuntimeSlotFacts.Create(8, 0x5000_0040UL),
            static (_, _) => [0x10, 0x20, 0x30, 0x40, 0x00, 0x00, 0x00, 0x00]);

        var withoutTarget = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text",
            runtimeEvidence: evidence));
        AssertExactAxes(withoutTarget);
        Assert.Null(withoutTarget.Provenance.ReferenceTargetValidation);
        Assert.Equal(0x4030_2010UL, withoutTarget.ReferenceAddress);

        var assignable = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text",
            runtimeEvidence: evidence,
            referenceTargetType: MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Object)));
        AssertExactAxes(assignable);
        Assert.Equal(
            StaticFieldV2AssignabilityResultKind.Assignable,
            assignable.Provenance.ReferenceTargetValidation!.ResultKind);

        var blocked = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text",
            runtimeEvidence: evidence,
            referenceTargetType: MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int32)));
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, blocked.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Blocked, blocked.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, blocked.Axes.Completeness);
        Assert.NotEqual(
            StaticFieldV2AssignabilityResultKind.Assignable,
            blocked.Provenance.ReferenceTargetValidation!.ResultKind);
    }

    /// <summary>
    /// Proves an uninitialized Property catalog vector and an empty one are different requests with different
    /// canonical bytes, because they reach member lookup as different typed stops.
    /// </summary>
    /// <remarks>
    /// The two are not interchangeable: a caller that supplied no vector at all is told so, and a caller that
    /// supplied one covering no module is told that instead. Encoding both as length zero would give two requests
    /// with materially different answers a single replay digest, which is the one thing a canonical encoding exists
    /// to prevent.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Uninitialized_and_empty_property_vectors_are_distinct_requests()
    {
        var world = BuildWorld();
        const string expression = "global::Pipe.App.Holder.Answer";

        var uninitialized = StaticFieldV2ExpressionRequest.Create(
            expression,
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            world.Ancestry,
            world.Constraints,
            world.FieldCatalogs,
            null,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass);
        var empty = StaticFieldV2ExpressionRequest.Create(
            expression,
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            world.Ancestry,
            world.Constraints,
            world.FieldCatalogs,
            null,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            propertyCatalogs: ImmutableArray<MetadataPropertyTableCatalogIdentity>.Empty);

        Assert.False(uninitialized.IsPropertyCatalogVectorInitialized);
        Assert.True(empty.IsPropertyCatalogVectorInitialized);
        Assert.NotEqual(uninitialized.Sha256, empty.Sha256);
        Assert.NotEqual(uninitialized, empty);

        // The distinction is not decorative: the two reach member lookup as different stops.
        Assert.Equal(
            StaticFieldV2MemberLookupIssue.PropertyCatalogVectorUninitialized,
            StaticFieldV2ExpressionPipeline.Evaluate(uninitialized)
                .Provenance.MemberLookup!.Issue);
        Assert.Equal(
            StaticFieldV2MemberLookupIssue.PropertyCatalogSlotsIncomplete,
            StaticFieldV2ExpressionPipeline.Evaluate(empty)
                .Provenance.MemberLookup!.Issue);
    }

    /// <summary>
    /// Proves canonical replay across two independently built worlds, defensive copies, the closed public
    /// surface, the frozen result digest, and emitted XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Pipeline_contracts_replay_are_immutable_guarded_and_documented()
    {
        var world = BuildWorld();
        var replayWorld = BuildWorld();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Current",
            runtimeEvidence: ConstructedSlotEvidence(world, 0x2A)));
        var replay = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            replayWorld,
            "global::Pipe.App.GenericSlot<int>.Current",
            runtimeEvidence: ConstructedSlotEvidence(replayWorld, 0x2A)));

        Assert.Equal(result, replay);
        Assert.Equal(result.GetHashCode(), replay.GetHashCode());
        Assert.Equal(result.Sha256, replay.Sha256);
        Assert.Equal(result.Axes, replay.Axes);
        Assert.Equal(result.Provenance, replay.Provenance);
        Assert.Equal(result.Request, replay.Request);
        Assert.True(result.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        Assert.NotEqual(
            result,
            StaticFieldV2ExpressionPipeline.Evaluate(Request(
                world,
                "global::Pipe.App.GenericSlot<int>.Current",
                runtimeEvidence: ConstructedSlotEvidence(world, 0x2B))));
        Assert.Equal(ConstructedSlotGoldenSha256, result.Sha256);

        var originalBytes = result.CanonicalBytes;
        var originalRaw = result.Provenance.RawValueBytes;
        var originalBoundaries = result.Provenance.DeclaredCoverageBoundaries;
        ImmutableCollectionsMarshal.AsArray(result.CanonicalBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(result.Provenance.CanonicalBytes)![0] ^= 0x33;
        ImmutableCollectionsMarshal.AsArray(result.Provenance.RawValueBytes)![0] ^= 0xFF;
        ImmutableCollectionsMarshal.AsArray(result.Provenance.DeclaredCoverageBoundaries)![0] =
            StaticFieldV2PipelineCoverageBoundary.FrameValueProfileOwnedBySeparateEntryPoint;
        ImmutableCollectionsMarshal.AsArray(result.Provenance.EvidenceLedger.CanonicalBytes)![0] ^= 0x11;
        ImmutableCollectionsMarshal.AsArray(result.Request.FieldCatalogs)![0] = null!;

        Assert.True(originalBytes.AsSpan().SequenceEqual(result.CanonicalBytes.AsSpan()));
        Assert.Equal(originalRaw.ToArray(), result.Provenance.RawValueBytes.ToArray());
        Assert.Equal(originalBoundaries.ToArray(), result.Provenance.DeclaredCoverageBoundaries.ToArray());
        Assert.Equal(ConstructedSlotGoldenSha256, result.Sha256);
        Assert.NotNull(result.Request.FieldCatalogs[0]);

        Assert.Throws<ArgumentNullException>(() => StaticFieldV2ExpressionPipeline.Evaluate(null!));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2ExpressionPipeline.EvaluateFrameValue(null!));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2ScopedContextSource.Create(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldV2RuntimeSlotFacts.Create(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            result.Provenance.EvidenceLedger.CallCount((StaticFieldV2PipelineEvidenceKind)9));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ExpressionRequest.Create(
            "global::A.B",
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            world.Ancestry,
            world.Constraints,
            world.FieldCatalogs,
            accessibilityMode: StaticFieldV2AccessibilityMode.UseSiteCertificate));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldV2ExpressionRequest.Create(
            "global::A.B",
            (DumpExpressionProfileKind)7,
            world.Ancestry,
            world.Constraints,
            world.FieldCatalogs));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2ExpressionRoute),
            typeof(StaticFieldV2PipelineEvidenceKind),
            typeof(StaticFieldV2PipelineCoverageBoundary),
            typeof(StaticFieldV2PipelineEvidenceLedger),
            typeof(StaticFieldV2ScopedContextAcquisitionDisposition),
            typeof(StaticFieldV2ScopedContextAcquisition),
            typeof(StaticFieldV2ScopedContextSource),
            typeof(StaticFieldV2RuntimeSlotFacts),
            typeof(StaticFieldV2RuntimeEvidenceSource),
            typeof(StaticFieldV2SuffixEvaluationRequest),
            typeof(StaticFieldV2SuffixEvaluationSource),
            typeof(StaticFieldV2ExpressionRequest),
            typeof(StaticFieldV2ExpressionProvenance),
            typeof(StaticFieldV2ExpressionResult),
            typeof(StaticFieldV2ExpressionPipeline),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (type == typeof(StaticFieldV2ExpressionPipeline))
            {
                Assert.Equal(["Evaluate", "EvaluateFrameValue"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2ScopedContextSource))
            {
                Assert.Equal(["Create", "CreateFromAcquisition"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2ScopedContextAcquisition))
            {
                Assert.Equal(["Exact", "Stopped"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2RuntimeSlotFacts) ||
                     type == typeof(StaticFieldV2RuntimeEvidenceSource) ||
                     type == typeof(StaticFieldV2SuffixEvaluationSource) ||
                     type == typeof(StaticFieldV2ExpressionRequest))
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

    private static void AssertExactAxes(StaticFieldV2ExpressionResult result)
    {
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, result.Axes.Syntax);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.NotRequested, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.Equal(DumpExpressionRootAttributionOutcome.NotRequired, result.Axes.RootAttribution);
        Assert.True(result.IsComplete);
    }

    private static void AssertNotReachedFrom(
        StaticFieldV2ExpressionResult result,
        int firstNotReachedIndex,
        bool expectNoAnswer = true)
    {
        var reached = new[]
        {
            result.Axes.Context == DumpExpressionContextOutcome.NotReached,
            result.Axes.RootAttribution == DumpExpressionRootAttributionOutcome.NotReached,
            result.Axes.LexicalCompleteness == DumpExpressionLexicalCompletenessOutcome.NotReached,
            result.Axes.TypeBinding == DumpExpressionTypeBindingOutcome.NotReached,
            result.Axes.TypeConstruction == DumpExpressionTypeConstructionOutcome.NotReached,
            result.Axes.MemberLookup == DumpExpressionMemberLookupOutcome.NotReached,
            result.Axes.RuntimeConstruction == DumpExpressionRuntimeConstructionOutcome.NotReached,
            result.Axes.Storage == DumpExpressionStorageOutcome.NotReached,
        };
        for (var index = firstNotReachedIndex; index < reached.Length; index++)
        {
            Assert.True(reached[index], $"Axis {index} must be NotReached after the typed stop.");
        }
        Assert.Equal(DumpExpressionValueOutcome.NotReached, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.NotReached, result.Axes.Suffix);
        Assert.Equal(
            expectNoAnswer
                ? DumpExpressionCompletenessOutcome.NoAnswer
                : DumpExpressionCompletenessOutcome.Partial,
            result.Axes.Completeness);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2ExpressionPipeline).Assembly;
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

    internal static StaticFieldV2ExpressionRequest Request(
        PipelineWorld world,
        string expressionText,
        DumpExpressionProfileKind profile = DumpExpressionProfileKind.StaticFieldExpressionV2,
        StaticFieldV2ScopedContextSource? scopedContext = null,
        StaticFieldV2RuntimeEvidenceSource? runtimeEvidence = null,
        StaticFieldV2SuffixEvaluationSource? suffixEvaluation = null,
        ImmutableArray<MetadataConstantTableCatalogIdentity> constantCatalogs = default,
        MetadataClosedTypeIdentity? referenceTargetType = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null,
        ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs = default) =>
        StaticFieldV2ExpressionRequest.Create(
            expressionText,
            profile,
            world.Ancestry,
            world.Constraints,
            world.FieldCatalogs,
            null,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            null,
            default,
            scopedContext,
            runtimeEvidence,
            suffixEvaluation,
            constantCatalogs,
            propertyCatalogs.IsDefault ? world.PropertyCatalogs : propertyCatalogs,
            referenceTargetType,
            capabilityProbes);

    /// <summary>
    /// Builds one exact Constant catalog per composed module, giving every literal field the requested value.
    /// </summary>
    /// <remarks>
    /// A complete Constant table proves its pairing in both directions, so a catalog cannot cover only the field a
    /// test happens to read: every field declaring a default value needs a row. Giving them one shared value keeps
    /// the fixture honest without making each test carry a table.
    /// </remarks>
    private static ImmutableArray<MetadataConstantTableCatalogIdentity> LiteralCatalogs(
        PipelineWorld world,
        int value)
    {
        var catalogs = ImmutableArray.CreateBuilder<MetadataConstantTableCatalogIdentity>(
            world.FieldCatalogs.Length);
        foreach (var fieldCatalog in world.FieldCatalogs)
        {
            var defaults = fieldCatalog.Rows.Where(static row => row.HasDefault).ToArray();
            var observations = ImmutableArray.CreateBuilder<MetadataConstantRowObservationIdentity>(defaults.Length);
            for (var index = 0; index < defaults.Length; index++)
            {
                observations.Add(MetadataConstantRowObservationIdentity.Create(
                    metadataModule: fieldCatalog.SourceEnds.SourceModule,
                    constantToken: 0x0B00_0000 | (index + 1),
                    constantTypeCode: ConstantInt32,
                    parentMetadataToken: defaults[index].Observation.FieldDefinitionToken,
                    constantValueBlob:
                    [
                        (byte)(value & 0xFF),
                        (byte)((value >> 8) & 0xFF),
                        (byte)((value >> 16) & 0xFF),
                        (byte)((value >> 24) & 0xFF),
                    ]));
            }

            var catalog = MetadataConstantTableCatalogIdentity.Create(
                MetadataDeclaredMemberSourceEndIdentity.Create(fieldCatalog.SourceEnds),
                fieldCatalog,
                observations.MoveToImmutable());
            Assert.Equal(MetadataConstantTableResultKind.Exact, catalog.ResultKind);
            catalogs.Add(catalog);
        }

        return catalogs.MoveToImmutable();
    }

    private static StaticFieldV2ScopedContextSource PoisonedContext() =>
        StaticFieldV2ScopedContextSource.Create(
            static () => throw new InvalidOperationException("scoped context"),
            static () => throw new InvalidOperationException("lexical envelope"));

    private static StaticFieldV2RuntimeEvidenceSource PoisonedRuntimeEvidence() =>
        StaticFieldV2RuntimeEvidenceSource.Create(
            static (_, _) => throw new InvalidOperationException("runtime construction"),
            static (_, _) => throw new InvalidOperationException("slot facts"),
            static (_, _) => throw new InvalidOperationException("memory read"));

    internal static StaticFieldV2RuntimeEvidenceSource ConstructedSlotEvidence(PipelineWorld world, byte payload) =>
        StaticFieldV2RuntimeEvidenceSource.Create(
            (construction, strategy) => Candidates(world, construction, strategy),
            static (_, _) => StaticFieldV2RuntimeSlotFacts.Create(4, 0x5000_0040UL),
            (_, _) => [payload, 0x00, 0x00, 0x00]);

    internal static ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> Candidates(
        PipelineWorld world,
        StaticFieldV2ClosedConstructionOutcome construction,
        StaticFieldV2StorageStrategyOutcome strategy)
    {
        Assert.Equal(StaticFieldV2StorageStrategy.ConstructedSlot, strategy.Strategy);
        return
        [
            Candidate(world, construction.FlattenedArguments, 0x200),
            Candidate(world, [MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int64)], 0x300),
        ];
    }

    private static StaticFieldV2RuntimeConstructionCandidate Candidate(
        PipelineWorld world,
        ImmutableArray<MetadataClosedTypeIdentity> arguments,
        ulong seed) =>
        StaticFieldV2RuntimeConstructionCandidate.Create(
            0x1_0000UL + seed,
            0x2_0000UL + seed,
            world.App,
            AppGenericSlotToken,
            world.App,
            world.App.ContainingAssembly,
            0x7000,
            0x8000,
            arguments);

    private static StaticFieldV2ScopedContextSource NamespaceImportContext(PipelineWorld world, string imported) =>
        StaticFieldV2ScopedContextSource.Create(() => ContextRequest(
            world,
            Scopes([DumpPortablePdbImportFact.NamespaceImport(
                ScopeToken(1),
                0,
                1,
                imported,
                [0xAB, 0x02])])));

    /// <summary>
    /// Supplies one scope declaring both a namespace import, which reaches the owner, and the extern alias its type
    /// argument names, so the owner and the argument are answered by the same level walk.
    /// </summary>
    /// <param name="world">The composed pipeline world.</param>
    /// <param name="imported">The imported namespace the owner spelling reaches through.</param>
    /// <returns>The caller-owned scoped-context seam.</returns>
    private static StaticFieldV2ScopedContextSource ImportAndExternAliasContext(
        PipelineWorld world,
        string imported) =>
        StaticFieldV2ScopedContextSource.Create(() => ContextRequest(
            world,
            Scopes(
            [
                DumpPortablePdbImportFact.NamespaceImport(
                    ScopeToken(1),
                    0,
                    1,
                    imported,
                    [0xAB, 0x02]),
                DumpPortablePdbImportFact.ExternAlias(
                    ScopeToken(1),
                    1,
                    6,
                    "lib",
                    [0xAB, 0x00],
                    LibAssemblyReferenceToken),
            ])));

    private static StaticFieldV2ScopedContextSource ExternAliasContext(PipelineWorld world) =>
        StaticFieldV2ScopedContextSource.Create(() => ExternAliasContextRequest(world));

    private static StaticFieldV2ScopedContextRequest ExternAliasContextRequest(PipelineWorld world) =>
        ContextRequest(
            world,
            Scopes([DumpPortablePdbImportFact.ExternAlias(
                ScopeToken(1),
                0,
                6,
                "lib",
                [0xAB, 0x00],
                LibAssemblyReferenceToken)]));

    private static StaticFieldV2ScopedContextSource UsingStaticContext(PipelineWorld world)
    {
        var scopes = Scopes([DumpPortablePdbImportFact.UsingStatic(
            ScopeToken(1),
            0,
            3,
            "Pipe.App.Imported",
            AppImportedToken,
            [0xAB, 0x01])]);
        return StaticFieldV2ScopedContextSource.Create(
            () => ContextRequest(world, scopes),
            () => LexicalEnvelope(scopes));
    }

    /// <summary>
    /// Supplies a <c>using static</c> import whose target is the physical TypeSpec row naming a closed construction,
    /// together with the token-resolution catalogs that let the projection decode it.
    /// </summary>
    /// <param name="world">The alias world holding the TypeSpec rows and their catalogs.</param>
    /// <returns>The caller-owned scoped-context seam with a lexical envelope.</returns>
    private static StaticFieldV2ScopedContextSource ConstructedUsingStaticContext(TypeSpecAliasWorld world)
    {
        var scopes = Scopes([DumpPortablePdbImportFact.UsingStatic(
            ScopeToken(1),
            0,
            3,
            "Pipe.App.Slot`1",
            GroundAliasTypeSpecToken,
            [0xAB, 0x01])]);
        return StaticFieldV2ScopedContextSource.Create(
            () => StaticFieldV2ScopedContextRequest.Create(
                world.World.App,
                scopes,
                world.World.HostType,
                world.World.Ancestry,
                world.World.Resolution,
                world.TokenCatalogs),
            () => LexicalEnvelope(scopes, AppAliasMarkerToken));
    }

    private static StaticFieldV2ScopedContextRequest ContextRequest(
        PipelineWorld world,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes) =>
        StaticFieldV2ScopedContextRequest.Create(
            world.App,
            scopes,
            world.HostType,
            world.Ancestry,
            world.Resolution);

    private static ImmutableArray<DumpPortablePdbImportScopeIdentity> Scopes(
        ImmutableArray<DumpPortablePdbImportFact> facts) =>
        [DumpPortablePdbImportScopeIdentity.Create(ScopeToken(1), null, 0, facts)];

    private static int ScopeToken(int rowId) => 0x3500_0000 | rowId;

    private static DumpSelectedMethodLexicalObservation LexicalEnvelope(
        ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes,
        int declaringTypeToken = AppHostToken)
    {
        const int selectedMethodToken = 0x0600_0001;
        const int localScopeToken = 0x3200_0001;
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
            declaringNamespace: "Pipe.App",
            DumpInstructionLocation.Create(0x0040_1234, 10));
        var debugIdentity = DumpPortablePdbDebugIdentity.Create(
            Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
            stamp: 0x5A17_C0DE);
        var moduleDebug = DumpModulePortablePdbDebugIdentity.Create(runtimeModule, moduleContent, debugIdentity);
        var artifact = DumpPortablePdbArtifactIdentity.Create(
            DumpPortablePdbContentIdentity.Create(31_744, new string('c', 64)),
            debugIdentity);
        var pdbScopes = ImmutableArray.Create(DumpPortablePdbLocalScopeIdentity.Create(
            localScopeToken,
            selectedMethodToken,
            importScopes.IsDefaultOrEmpty ? null : importScopes[^1].ImportScopeToken,
            0,
            100,
            nestingDepth: 0));
        var pdbFacts = DumpPortablePdbContextFacts.Acquire(
            frame,
            moduleDebug,
            artifact,
            methodDebugInformationToken: 0x3100_0001,
            document: null,
            pdbScopes,
            importScopes.IsDefault ? [] : importScopes);

        var methodRows = ImmutableArray.Create(DumpLexicalMethodDefinitionRowFact.Create(
            selectedMethodToken,
            declaringTypeToken,
            relativeVirtualAddress: 0x20,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            "Run",
            [0x00, 0x00, 0x01],
            0,
            parameterListStartRowId: 1,
            []));
        var scopeRows = ImmutableArray.Create(DumpLexicalLocalScopeRowFact.Create(
            localScopeToken,
            selectedMethodToken,
            importScopes.IsDefaultOrEmpty ? null : importScopes[^1].ImportScopeToken,
            0,
            100,
            variableListStartRowId: 1,
            constantListStartRowId: 1));
        var sourceEnds = DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: methodRows.Length,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: scopeRows.Length,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: methodRows[0].Signature.Length,
            aggregateMethodSignatureByteCount: methodRows[0].Signature.Length,
            maximumSignatureTypeDepth: methodRows[0].SignatureTypeDepth,
            maximumSignatureTypeNodeCount: methodRows[0].SignatureTypeNodeCount,
            standaloneSignatureByteCount: 0,
            localSlotCount: 0,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0);

        var observation = DumpSelectedMethodLexicalObservation.Acquire(
            pdbFacts,
            sourceEnds,
            methodRows,
            [],
            [],
            null,
            scopeRows,
            [],
            []);
        Assert.Equal(DumpContextEvidenceStatus.Exact, observation.Status);
        return observation;
    }

    internal static PipelineWorld BuildWorld()
    {
        var core = BuildModule(
            CoreAssembly,
            0xE100,
            '1',
            [
                new PipeTypeRow("System", "Object", PublicClass, null),
                new PipeTypeRow("System", "ValueType", PublicClass, 0x0200_0002),
                new PipeTypeRow("System", "Enum", PublicClass, 0x0200_0003),
                new PipeTypeRow("System", "Delegate", PublicClass, 0x0200_0002),
                new PipeTypeRow("System", "MulticastDelegate", PublicClass, 0x0200_0005),
            ],
            []);
        var lib = BuildModule(
            LibAssembly,
            0xE200,
            '2',
            [
                new PipeTypeRow("Pipe.Lib", "LibHolder", PublicClass, 0x0100_0001),
                new PipeTypeRow("Pipe.Dup", "Twin", PublicClass, 0x0100_0001),
            ],
            [
                new PipeFieldRow("LibHolder", "LibAnswer", FieldPublic | FieldStatic | FieldLiteral, Int32Signature),
                new PipeFieldRow("Twin", "Current", FieldPublic | FieldStatic, Int32Signature),
            ],
            [("System", "Object")],
            [CoreAssembly]);
        var app = BuildModule(
            AppAssembly,
            0xE300,
            '3',
            [
                new PipeTypeRow("Pipe.App", "GenericSlot`1", PublicClass, 0x0100_0001, GenericArity: 1),
                new PipeTypeRow(
                    "Pipe.App",
                    "Constrained`1",
                    PublicClass,
                    0x0100_0001,
                    GenericArity: 1,
                    GenericParameterFlags: ReferenceTypeConstraintFlag),
                new PipeTypeRow("Pipe.App", "Holder", PublicClass, 0x0100_0001),
                new PipeTypeRow("Pipe.App", "Imported", PublicClass, 0x0100_0001),
                new PipeTypeRow("Pipe.App", "Host", PublicClass, 0x0100_0001),
                new PipeTypeRow("Pipe.Dup", "Twin", PublicClass, 0x0100_0001),
            ],
            [
                new PipeFieldRow("GenericSlot`1", "Current", FieldPublic | FieldStatic, Int32Signature),
                new PipeFieldRow("GenericSlot`1", "Text", FieldPublic | FieldStatic, StringSignature),
                new PipeFieldRow("Constrained`1", "Current", FieldPublic | FieldStatic, Int32Signature),
                new PipeFieldRow("Holder", "Answer", FieldPublic | FieldStatic | FieldLiteral, Int32Signature),
                new PipeFieldRow("Holder", "Blob", FieldPublic | FieldStatic | FieldRva, Int32Signature),
                new PipeFieldRow("Holder", "Twin", FieldPublic | FieldStatic, Int32Signature),
                new PipeFieldRow("Holder", "Duo", FieldPublic | FieldStatic, Int32Signature),
                new PipeFieldRow("Holder", "Duo", FieldPublic | FieldStatic, Int32Signature),
                new PipeFieldRow("Imported", "Bare", FieldPublic | FieldStatic | FieldLiteral, Int32Signature),
                new PipeFieldRow("Twin", "Current", FieldPublic | FieldStatic, Int32Signature),
            ],
            [("System", "Object")],
            [CoreAssembly, LibAssembly],
            [new PipeMethodRow("Holder", "Twin", MethodPublicStatic)]);

        var ancestryWorld = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            core.Ancestry,
            lib.Ancestry,
            app.Ancestry);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestryWorld.Ancestry.ResultKind);

        var byModule = new[] { core, lib, app }.ToDictionary(static built => built.Ancestry.Module);
        var order = ancestryWorld.Ancestry.Entries.Select(static entry => entry.SourceModule).ToArray();
        var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            ancestryWorld.Resolution,
            [.. order.Select(module => byModule[module].Constraints)]);
        Assert.Equal(MetadataConstraintTargetResolutionPortfolioResultKind.Exact, constraints.ResultKind);
        var fieldCatalogs = ImmutableArray.CreateRange(order.Select(module => byModule[module].FieldCatalog));
        var propertyCatalogs = ImmutableArray.CreateRange(
            order.Select(module => byModule[module].PropertyCatalog));

        return new PipelineWorld(
            app.Ancestry.Module,
            ancestryWorld.Resolution,
            ancestryWorld.Ancestry,
            constraints,
            fieldCatalogs,
            propertyCatalogs,
            app.Ancestry.ChainCatalog.ExactChainOrDefault(AppHostToken)!.FinalTypeDefinition);
    }

    /// <summary>
    /// Builds an independent world whose sole application type is a closed-constructible generic <c>Slot`1</c> owning
    /// three static fields: an owner <c>VAR 0</c> field, an out-of-arity owner <c>VAR 1</c> field, and a ground
    /// <c>Int32</c> field. It shares none of <see cref="BuildWorld"/>'s frozen-golden modules, so the owner-VAR
    /// substitution decoder can be exercised end to end without touching any frozen static-path digest.
    /// </summary>
    internal static PipelineWorld BuildGenericVarWorld()
    {
        var core = BuildModule(
            CoreAssembly,
            0xE100,
            '1',
            [
                new PipeTypeRow("System", "Object", PublicClass, null),
                new PipeTypeRow("System", "ValueType", PublicClass, 0x0200_0002),
                new PipeTypeRow("System", "Enum", PublicClass, 0x0200_0003),
                new PipeTypeRow("System", "Delegate", PublicClass, 0x0200_0002),
                new PipeTypeRow("System", "MulticastDelegate", PublicClass, 0x0200_0005),
            ],
            []);
        var app = BuildModule(
            AppAssembly,
            0xE300,
            '3',
            [
                new PipeTypeRow("Pipe.App", "Slot`1", PublicClass, 0x0100_0001, GenericArity: 1),
            ],
            [
                new PipeFieldRow("Slot`1", "Current", FieldPublic | FieldStatic, OwnerVar0Signature),
                new PipeFieldRow("Slot`1", "OutOfArity", FieldPublic | FieldStatic, OwnerVar1Signature),
                new PipeFieldRow("Slot`1", "IntCurrent", FieldPublic | FieldStatic, Int32Signature),
            ],
            [("System", "Object")],
            [CoreAssembly]);

        var ancestryWorld = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(core.Ancestry, app.Ancestry);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestryWorld.Ancestry.ResultKind);

        var byModule = new[] { core, app }.ToDictionary(static built => built.Ancestry.Module);
        var order = ancestryWorld.Ancestry.Entries.Select(static entry => entry.SourceModule).ToArray();
        var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            ancestryWorld.Resolution,
            [.. order.Select(module => byModule[module].Constraints)]);
        Assert.Equal(MetadataConstraintTargetResolutionPortfolioResultKind.Exact, constraints.ResultKind);
        var fieldCatalogs = ImmutableArray.CreateRange(order.Select(module => byModule[module].FieldCatalog));
        var propertyCatalogs = ImmutableArray.CreateRange(
            order.Select(module => byModule[module].PropertyCatalog));

        return new PipelineWorld(
            app.Ancestry.Module,
            ancestryWorld.Resolution,
            ancestryWorld.Ancestry,
            constraints,
            fieldCatalogs,
            propertyCatalogs,
            app.Ancestry.ChainCatalog.ExactChainOrDefault(AppGenericSlotToken)!.FinalTypeDefinition);
    }

    /// <summary>
    /// Builds an independent world whose application module declares a generic <c>Slot`1</c>, a non-generic
    /// <c>Marker</c>, and two physical TypeSpec rows: the closed construction <c>Slot&lt;Marker&gt;</c> a whole-owner
    /// alias can name, and an arity-disagreeing <c>Marker&lt;Marker&gt;</c> that no decode can admit.
    /// </summary>
    /// <remarks>
    /// It shares none of <see cref="BuildWorld"/>'s frozen-golden modules, because a TypeSpec row changes the source
    /// ends of the module that declares it and would move every digest frozen over that world.
    /// </remarks>
    private static TypeSpecAliasWorld BuildTypeSpecAliasWorld()
    {
        var core = BuildModule(
            CoreAssembly,
            0xE100,
            '1',
            [
                new PipeTypeRow("System", "Object", PublicClass, null),
                new PipeTypeRow("System", "ValueType", PublicClass, 0x0200_0002),
                new PipeTypeRow("System", "Enum", PublicClass, 0x0200_0003),
                new PipeTypeRow("System", "Delegate", PublicClass, 0x0200_0002),
                new PipeTypeRow("System", "MulticastDelegate", PublicClass, 0x0200_0005),
            ],
            []);
        var app = BuildModule(
            AppAssembly,
            0xE300,
            '3',
            [
                new PipeTypeRow("Pipe.App", "Slot`1", PublicClass, 0x0100_0001, GenericArity: 1),
                new PipeTypeRow("Pipe.App", "Marker", PublicClass, 0x0100_0001),
            ],
            [
                new PipeFieldRow("Slot`1", "Current", FieldPublic | FieldStatic, Int32Signature),
            ],
            [("System", "Object")],
            [CoreAssembly],
            default,
            [
                // GENERICINST CLASS Slot`1 <1> CLASS Marker — the construction a whole-owner alias names.
                new PipeTypeSpecRow(GroundAliasTypeSpecToken, [0x15, 0x12, 0x08, 0x01, 0x12, 0x0C]),

                // GENERICINST CLASS Marker <1> CLASS Marker — arity 0 instantiated with one argument.
                new PipeTypeSpecRow(ArityConflictTypeSpecToken, [0x15, 0x12, 0x0C, 0x01, 0x12, 0x0C]),
            ]);

        var ancestryWorld = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(core.Ancestry, app.Ancestry);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestryWorld.Ancestry.ResultKind);

        var byModule = new[] { core, app }.ToDictionary(static built => built.Ancestry.Module);
        var order = ancestryWorld.Ancestry.Entries.Select(static entry => entry.SourceModule).ToArray();
        var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            ancestryWorld.Resolution,
            [.. order.Select(module => byModule[module].Constraints)]);
        Assert.Equal(MetadataConstraintTargetResolutionPortfolioResultKind.Exact, constraints.ResultKind);

        var world = new PipelineWorld(
            app.Ancestry.Module,
            ancestryWorld.Resolution,
            ancestryWorld.Ancestry,
            constraints,
            [.. order.Select(module => byModule[module].FieldCatalog)],
            [.. order.Select(module => byModule[module].PropertyCatalog)],
            app.Ancestry.ChainCatalog.ExactChainOrDefault(AppAliasMarkerToken)!.FinalTypeDefinition);

        // The host-side acquisition role: one catalog for the module whose TypeSpec blobs name these tokens, with
        // exactly the classified targets those blobs reference. Nothing here interprets a name.
        var appModule = app.Ancestry.Module;
        var tokenCatalog = MetadataSignatureTokenResolutionCatalog.Create(
            app.FieldCatalog.SourceEnds,
            [
                NamedTokenEntry(ancestryWorld.Ancestry, appModule, AppAliasSlotToken),
                NamedTokenEntry(ancestryWorld.Ancestry, appModule, AppAliasMarkerToken),
            ]);
        Assert.Equal(MetadataSignatureTokenResolutionCatalogResultKind.Exact, tokenCatalog.ResultKind);
        return new TypeSpecAliasWorld(world, [tokenCatalog]);
    }

    private static MetadataSignatureTokenResolutionEntry NamedTokenEntry(
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionToken)
    {
        var classification = ancestry.ExactClassificationOrDefault(module, typeDefinitionToken);
        Assert.NotNull(classification);
        return MetadataSignatureTokenResolutionEntry.Named(
            MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(classification),
            [classification]);
    }

    private static StaticFieldV2ScopedContextSource TypeSpecAliasContext(
        TypeSpecAliasWorld world,
        string alias,
        int typeSpecificationToken,
        bool supplyTokenCatalogs = true) =>
        StaticFieldV2ScopedContextSource.Create(() => StaticFieldV2ScopedContextRequest.Create(
            world.World.App,
            Scopes([DumpPortablePdbImportFact.TypeAlias(
                ScopeToken(1),
                0,
                9,
                alias,
                "Pipe.App.Slot`1",
                typeSpecificationToken,
                [0xAB, 0x03])]),
            world.World.HostType,
            world.World.Ancestry,
            world.World.Resolution,
            supplyTokenCatalogs ? world.TokenCatalogs : default));

    private static ImmutableArray<byte> Int32Signature => [0x06, 0x08];

    private static ImmutableArray<byte> StringSignature => [0x06, 0x0E];

    private static ImmutableArray<byte> OwnerVar0Signature => [0x06, 0x13, 0x00];

    private static ImmutableArray<byte> OwnerVar1Signature => [0x06, 0x13, 0x01];

    private static PipeModule BuildModule(
        string assemblyName,
        ulong moduleAddress,
        char digestCharacter,
        ImmutableArray<PipeTypeRow> namedTypes,
        ImmutableArray<PipeFieldRow> fields,
        ImmutableArray<(string NamespaceName, string TypeName)> typeReferences = default,
        ImmutableArray<string> assemblyReferences = default,
        ImmutableArray<PipeMethodRow> methods = default,
        ImmutableArray<PipeTypeSpecRow> typeSpecifications = default)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var referenceSpecs = typeReferences.IsDefault ? [] : typeReferences;
        var assemblySpecs = assemblyReferences.IsDefault ? [] : assemblyReferences;
        var methodSpecs = methods.IsDefault ? [] : methods;
        var typeSpecificationSpecs = typeSpecifications.IsDefault ? [] : typeSpecifications;

        var assemblyReferenceRows =
            ImmutableArray.CreateBuilder<MetadataAssemblyReferenceRowObservationIdentity>(assemblySpecs.Length);
        for (var index = 0; index < assemblySpecs.Length; index++)
        {
            assemblyReferenceRows.Add(W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(
                module,
                index + 1,
                assemblySpecs[index]));
        }
        var typeReferenceRows =
            ImmutableArray.CreateBuilder<MetadataTypeReferenceRowObservationIdentity>(referenceSpecs.Length);
        for (var index = 0; index < referenceSpecs.Length; index++)
        {
            typeReferenceRows.Add(W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                module,
                index + 1,
                referenceSpecs[index].NamespaceName,
                referenceSpecs[index].TypeName,
                0x2300_0001));
        }

        var totalTypeCount = namedTypes.Length + 1;
        var fieldObservations = ImmutableArray.CreateBuilder<MetadataFieldDefinitionRowObservationIdentity>();
        var methodObservations = ImmutableArray.CreateBuilder<MetadataMethodDefinitionRowObservationIdentity>();
        var genericParameterRows = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        var fieldStarts = new int[totalTypeCount];
        var methodStarts = new int[totalTypeCount];
        fieldStarts[0] = 1;
        methodStarts[0] = 1;
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            var ownerToken = 0x0200_0002 + index;
            fieldStarts[index + 1] = fieldObservations.Count + 1;
            methodStarts[index + 1] = methodObservations.Count + 1;
            foreach (var field in fields.Where(candidate =>
                         string.Equals(candidate.TypeName, row.TypeName, StringComparison.Ordinal)))
            {
                fieldObservations.Add(MetadataFieldDefinitionRowObservationIdentity.Create(
                    module,
                    0x0400_0000 | checked(fieldObservations.Count + 1),
                    field.Attributes,
                    field.Name,
                    field.Signature));
            }
            foreach (var method in methodSpecs.Where(candidate =>
                         string.Equals(candidate.TypeName, row.TypeName, StringComparison.Ordinal)))
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
            for (var number = 0; number < row.GenericArity; number++)
            {
                genericParameterRows.Add(MetadataGenericParameterRowObservationIdentity.Create(
                    module,
                    0x2A00_0000 | checked(genericParameterRows.Count + 1),
                    number,
                    flags: row.GenericParameterFlags,
                    ownerMetadataToken: ownerToken,
                    name: $"T{ownerToken & 0x00FF_FFFF}_{number}"));
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
                typeReferenceRowCount: typeReferenceRows.Count,
                typeSpecificationRowCount: typeSpecificationSpecs.Length,
                assemblyReferenceRowCount: assemblyReferenceRows.Count,
                methodDefinitionRowCount: methodObservations.Count,
                genericParameterRowCount: genericParameterRows.Count,

                // One Constant row per field declaring a default value: the end a complete Constant table must meet.
                declaredMemberRowCounts: StaticFieldModuleDeclaredMemberRowCounts.Create(
                    constantRowCount: fieldObservations.Count(
                        static field => ((FieldAttributes)field.Attributes & FieldAttributes.HasDefault) != 0),
                    propertyMapRowCount: 0,
                    propertyPointerRowCount: 0)));

        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(totalTypeCount);
        typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x0200_0001,
            fieldListRowId: fieldStarts[0],
            methodListRowId: methodStarts[0],
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
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, typeDefinitions, []);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            genericParameterRows.Count == 0 ? default : genericParameterRows.ToImmutable());
        var methodCatalog = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            methodObservations.Count == 0 ? default : methodObservations.ToImmutable());
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodCatalog);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var referenceEnds = MetadataReferenceSourceEndIdentity.Create(sourceEnds);
        var tables = MetadataModuleReferenceTableSetIdentity.Create(
            referenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(referenceEnds, typeReferenceRows.ToImmutable()),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                referenceEnds,
                [.. typeSpecificationSpecs.Select(row => MetadataTypeSpecificationRowObservationIdentity.Create(
                    module,
                    row.TypeSpecificationToken,
                    row.SignatureBytes))]),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                assemblyReferenceRows.ToImmutable()),
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
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            compatibility,
            MetadataCompilerNameMappingCatalogIdentity.Create(authority));
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);

        var constraintCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(authority),
            []);
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            constraintCatalog.ResultKind);

        var fieldCatalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            fieldObservations.Count == 0 ? default : fieldObservations.ToImmutable());
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, fieldCatalog.ResultKind);

        var propertyCatalog = MetadataPropertyTableCatalogIdentity.Create(
            MetadataDeclaredMemberSourceEndIdentity.Create(sourceEnds),
            authority,
            ImmutableArray<MetadataPropertyRowObservationIdentity>.Empty);
        Assert.Equal(MetadataPropertyTableResultKind.Exact, propertyCatalog.ResultKind);

        return new PipeModule(
            new W8MetadataAncestryAuthorityContractTests.AncestryModule(
                module,
                compatibility,
                chainCatalog,
                tables),
            constraintCatalog,
            fieldCatalog,
            propertyCatalog);
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

    private sealed record PipeTypeRow(
        string NamespaceName,
        string TypeName,
        int TypeAttributes,
        int? ExtendsMetadataToken,
        int GenericArity = 0,
        int GenericParameterFlags = 0);

    private sealed record PipeFieldRow(
        string TypeName,
        string Name,
        int Attributes,
        ImmutableArray<byte> Signature);

    private sealed record PipeMethodRow(string TypeName, string Name, int Attributes);

    private sealed record PipeTypeSpecRow(int TypeSpecificationToken, ImmutableArray<byte> SignatureBytes);

    private sealed record TypeSpecAliasWorld(
        PipelineWorld World,
        ImmutableArray<MetadataSignatureTokenResolutionCatalog> TokenCatalogs);

    private sealed record PipeModule(
        W8MetadataAncestryAuthorityContractTests.AncestryModule Ancestry,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Constraints,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog,
        MetadataPropertyTableCatalogIdentity PropertyCatalog);

    internal sealed record PipelineWorld(
        StaticFieldMetadataModuleIdentity App,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        MetadataConstraintTargetResolutionPortfolioIdentity Constraints,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs,
        ImmutableArray<MetadataPropertyTableCatalogIdentity> PropertyCatalogs,
        MetadataTypeDefinitionAuthorityIdentity HostType);
}
