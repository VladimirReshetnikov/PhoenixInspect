using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

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

    private const string ConstructedSlotGoldenSha256 =
        "c7872a08d2ea8039e1f886b54c699ce58875c86ebd14b473ac7eefa58172bc60";

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
    /// Proves an extern-alias contextual route reaches the identical owner and field identity as the fully qualified
    /// spelling of the same declaration while retaining materially different draft provenance.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Contextual_alias_reaches_the_same_field_identity_with_different_provenance()
    {
        var world = BuildWorld();
        var qualified = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "global::Pipe.Lib.LibHolder.LibAnswer",
            literalConstantSource: LiteralSource(7)));
        var aliased = StaticFieldV2ExpressionPipeline.Evaluate(Request(
            world,
            "lib::Pipe.Lib.LibHolder.LibAnswer",
            scopedContext: ExternAliasContext(world),
            literalConstantSource: LiteralSource(7)));

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
        Assert.NotNull(qualified.Provenance.OwnerConstruction);
        Assert.Null(aliased.Provenance.OwnerConstruction);
        Assert.NotEqual(qualified.Provenance.Sha256, aliased.Provenance.Sha256);
        Assert.NotEqual(qualified.Sha256, aliased.Sha256);
        Assert.Contains(
            StaticFieldV2PipelineCoverageBoundary.ContextualRouteClosedConstructionDeferred,
            aliased.Provenance.DeclaredCoverageBoundaries);
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
            literalConstantSource: LiteralSource(11)));

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
            literalConstantSource: LiteralSource(99),
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
        Assert.Equal(1, ledger.MetadataConstantRowCallCount);
        Assert.True(result.Provenance.CapabilityCallLedger!.IsZero);
        Assert.Null(result.Provenance.RuntimeConstruction);
        Assert.Null(result.Provenance.StaticSlot);
        Assert.True(result.Provenance.RawValueBytes.IsDefault);
        Assert.NotNull(result.Provenance.LiteralConstant);
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
            literalConstantSource: LiteralSource(99)));
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
            literalConstantSource: LiteralSource(99)));

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
    /// Proves canonical replay across two independently built worlds, defensive draft copies, the closed public
    /// surface, the frozen result digest, and emitted draft XML documentation.
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
        Assert.Throws<ArgumentException>(() => StaticFieldV2LiteralConstantFact.Create(ConstantInt32, default));
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
            typeof(StaticFieldV2ScopedContextSource),
            typeof(StaticFieldV2RuntimeSlotFacts),
            typeof(StaticFieldV2LiteralConstantFact),
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
            else if (type == typeof(StaticFieldV2ScopedContextSource) ||
                     type == typeof(StaticFieldV2RuntimeSlotFacts) ||
                     type == typeof(StaticFieldV2LiteralConstantFact) ||
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

    internal static StaticFieldV2ExpressionRequest Request(
        PipelineWorld world,
        string expressionText,
        DumpExpressionProfileKind profile = DumpExpressionProfileKind.StaticFieldExpressionV2,
        StaticFieldV2ScopedContextSource? scopedContext = null,
        StaticFieldV2RuntimeEvidenceSource? runtimeEvidence = null,
        StaticFieldV2SuffixEvaluationSource? suffixEvaluation = null,
        Func<MetadataFieldDefinitionTableRowIdentity, StaticFieldV2LiteralConstantFact?>? literalConstantSource = null,
        MetadataClosedTypeIdentity? referenceTargetType = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null) =>
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
            literalConstantSource,
            referenceTargetType,
            capabilityProbes);

    private static Func<MetadataFieldDefinitionTableRowIdentity, StaticFieldV2LiteralConstantFact?> LiteralSource(
        int value) =>
        _ => StaticFieldV2LiteralConstantFact.Create(
            ConstantInt32,
            [
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF),
            ]);

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

    private static StaticFieldV2ScopedContextSource ExternAliasContext(PipelineWorld world) =>
        StaticFieldV2ScopedContextSource.Create(() => ContextRequest(
            world,
            Scopes([DumpPortablePdbImportFact.ExternAlias(
                ScopeToken(1),
                0,
                6,
                "lib",
                [0xAB, 0x00],
                LibAssemblyReferenceToken)])));

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
        ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes)
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
            AppHostToken,
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
            AppHostToken,
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

        return new PipelineWorld(
            app.Ancestry.Module,
            ancestryWorld.Resolution,
            ancestryWorld.Ancestry,
            constraints,
            fieldCatalogs,
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

        return new PipelineWorld(
            app.Ancestry.Module,
            ancestryWorld.Resolution,
            ancestryWorld.Ancestry,
            constraints,
            fieldCatalogs,
            app.Ancestry.ChainCatalog.ExactChainOrDefault(AppGenericSlotToken)!.FinalTypeDefinition);
    }

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
        ImmutableArray<PipeMethodRow> methods = default)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var referenceSpecs = typeReferences.IsDefault ? [] : typeReferences;
        var assemblySpecs = assemblyReferences.IsDefault ? [] : assemblyReferences;
        var methodSpecs = methods.IsDefault ? [] : methods;

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
                assemblyReferenceRowCount: assemblyReferenceRows.Count,
                methodDefinitionRowCount: methodObservations.Count,
                genericParameterRowCount: genericParameterRows.Count));

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
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
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

        return new PipeModule(
            new W8MetadataAncestryAuthorityContractTests.AncestryModule(
                module,
                compatibility,
                chainCatalog,
                tables),
            constraintCatalog,
            fieldCatalog);
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

    private sealed record PipeModule(
        W8MetadataAncestryAuthorityContractTests.AncestryModule Ancestry,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Constraints,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog);

    internal sealed record PipelineWorld(
        StaticFieldMetadataModuleIdentity App,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        MetadataConstraintTargetResolutionPortfolioIdentity Constraints,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs,
        MetadataTypeDefinitionAuthorityIdentity HostType);
}
