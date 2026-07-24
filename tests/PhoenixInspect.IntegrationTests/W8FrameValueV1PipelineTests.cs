using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the composed pipeline's <c>FrameValueExpressionV1</c> entry point end to end through the caller-owned
/// frame-root evidence seam: an exact memory-homed <see langword="this"/>, local, and parameter root reaching an exact
/// value; a conditional suffix over an exact-null root short-circuiting with zero suffix reads; the two frozen W8.1
/// non-admissions surfacing their codes; a context stop that is a typed answer rather than a crash; the frame-root
/// evidence-ledger axis; and the two-way profile isolation between the frame and static entry points.
/// </summary>
public sealed class W8FrameValueV1PipelineTests
{
    // Captured from a real run (never hand-computed): the canonical result digest of the memory-homed local answer.
    private const string MemoryHomedLocalGoldenSha256 =
        "fd50f4ded62b10c8531275f8bd040656a324f964777ba473aabb818b14babe90";

    private const int MemoryHomedLocalValue = 1_090_520_099;

    private const ulong ExactHomeAddress = 0x7FF6_0040_1180UL;

    private const ulong ExactReferenceAddress = 0x0000_01F2_3400_5000UL;

    /// <summary>
    /// Proves an exact memory-homed local root reaches an exact value: the frame context, memory-home attribution, and
    /// storage are exact, the local's lexical catalog is complete, the twelve axes compose to Complete, the memory home
    /// and value are retained in provenance, the frame-root seam is metered on its own ledger axis, and the canonical
    /// result digest is frozen from a real run.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_memory_homed_local_reaches_a_value()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var probes = ExpressionV2CapabilityProbeSet.Create();
        var seam = new RecordingFrameSeam(_ => ExactLocal(MemoryHomedLocalValue));

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "stageLocal",
            seam.Source,
            capabilityProbes: probes));

        // The twelve axes compose exactly to the memory-homed frame-value answer.
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, result.Axes.Syntax);
        Assert.Equal(DumpExpressionContextOutcome.Exact, result.Axes.Context);
        Assert.Equal(DumpExpressionRootAttributionOutcome.Exact, result.Axes.RootAttribution);
        Assert.Equal(DumpExpressionLexicalCompletenessOutcome.Complete, result.Axes.LexicalCompleteness);
        Assert.Equal(DumpExpressionTypeBindingOutcome.NotRequired, result.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.NotRequired, result.Axes.TypeConstruction);
        Assert.Equal(DumpExpressionMemberLookupOutcome.NotRequired, result.Axes.MemberLookup);
        Assert.Equal(DumpExpressionRuntimeConstructionOutcome.NotRequired, result.Axes.RuntimeConstruction);
        Assert.Equal(DumpExpressionStorageOutcome.Exact, result.Axes.Storage);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.NotRequested, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.True(result.IsComplete);

        // The decoded value and the retained memory home are surfaced from provenance.
        Assert.Equal(MemoryHomedLocalValue, result.SignedValue);
        Assert.Equal(DumpQueryValueKind.Int32, result.FrameValue!.Kind);
        Assert.Equal(StaticFieldV2FrameRootDisposition.Exact, result.Provenance.FrameRoot!.Disposition);
        Assert.Equal(StaticFieldV2FrameValueRootKind.Local, result.Provenance.FrameRoot.RootKind);
        Assert.Equal(ExactHomeAddress, result.Provenance.FrameRoot.MemoryHomeAddress);
        Assert.Equal(4, result.Provenance.FrameRoot.ReadWidth);

        // The frame-root evidence ledger meters exactly one seam call on its own independent axis and nothing else.
        var ledger = result.Provenance.EvidenceLedger;
        Assert.Equal(1, seam.Calls);
        Assert.Equal(1, ledger.FrameRootEvaluationCallCount);
        Assert.Equal(1, ledger.CallCount(StaticFieldV2PipelineEvidenceKind.FrameRootEvaluation));
        Assert.Equal(0, ledger.SuffixChainEvaluationCallCount);
        Assert.Equal(0, ledger.RawMemoryReadCallCount);
        Assert.Equal(0, ledger.ContextCallCount);

        // The projected root the seam received is the descriptor's identifier root.
        Assert.Equal(FrameValueV1RootKind.Identifier, seam.LastRequest!.RootKind);
        Assert.Equal("stageLocal", seam.LastRequest.Identifier!.DecodedText);

        // The frame answer declares the frame-root evidence boundary.
        Assert.Contains(
            StaticFieldV2PipelineCoverageBoundary.FrameRootEvidenceSuppliedByCallerSeam,
            result.Provenance.DeclaredCoverageBoundaries);

        Assert.Equal(MemoryHomedLocalGoldenSha256, result.Sha256);
    }

    /// <summary>
    /// Proves an exact memory-homed <see langword="this"/> receiver root reaches a value with the lexical axis
    /// NotRequired: a receiver has no bare-root lexical catalog to complete.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_memory_homed_this_reaches_a_value()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingFrameSeam(_ => ExactPrimitive(StaticFieldV2FrameValueRootKind.This, 55));

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(world, "this", seam.Source));

        Assert.Equal(DumpExpressionContextOutcome.Exact, result.Axes.Context);
        Assert.Equal(DumpExpressionRootAttributionOutcome.Exact, result.Axes.RootAttribution);
        Assert.Equal(DumpExpressionLexicalCompletenessOutcome.NotRequired, result.Axes.LexicalCompleteness);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.Equal(55, result.SignedValue);
        Assert.Equal(FrameValueV1RootKind.This, seam.LastRequest!.RootKind);
        Assert.Null(seam.LastRequest.Identifier);
    }

    /// <summary>
    /// Proves an exact memory-homed declared-parameter root reaches a value with the lexical axis NotRequired.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_memory_homed_parameter_reaches_a_value()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingFrameSeam(_ => ExactPrimitive(StaticFieldV2FrameValueRootKind.Parameter, 7));

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(world, "stage", seam.Source));

        Assert.Equal(DumpExpressionRootAttributionOutcome.Exact, result.Axes.RootAttribution);
        Assert.Equal(DumpExpressionLexicalCompletenessOutcome.NotRequired, result.Axes.LexicalCompleteness);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.Equal(7, result.SignedValue);
    }

    /// <summary>
    /// Proves a conditional <c>?.</c> suffix over an exact-null frame root reaches the coalesce fallback WITHOUT any
    /// suffix read: the exact-null root short-circuits to the frozen coalesce literal, and a poisoned suffix seam proves
    /// zero suffix-evidence calls.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Conditional_suffix_over_exact_null_root_reaches_fallback_without_a_read()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingFrameSeam(_ => ExactNull(StaticFieldV2FrameValueRootKind.Local));

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "stageLocal?.inner ?? 9",
            seam.Source,
            suffixEvaluation: PoisonedSuffixSeam()));

        Assert.Equal(DumpExpressionValueOutcome.ExactNull, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Completed, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.True(result.IsComplete);

        // The poisoned suffix seam was never consulted: the exact-null root short-circuited to the coalesce literal.
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        var fallback = Assert.IsType<DumpQueryValue>(result.SuffixValue);
        Assert.Equal(DumpQueryValueKind.Int32, fallback.Kind);
        Assert.Equal(9, fallback.Int32Value);
    }

    /// <summary>
    /// Proves a direct <c>.</c> suffix over an exact-null frame root is the unchanged W2/W6 null-target block and still
    /// never consults the suffix seam.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Direct_suffix_over_exact_null_root_blocks_without_a_read()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingFrameSeam(_ => ExactNull(StaticFieldV2FrameValueRootKind.Local));

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "stageLocal.inner",
            seam.Source,
            suffixEvaluation: PoisonedSuffixSeam()));

        Assert.Equal(DumpExpressionValueOutcome.ExactNull, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Blocked, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        Assert.Null(result.SuffixValue);
    }

    /// <summary>
    /// Proves a direct-member suffix over an exact non-null reference root roots the unchanged W2/W6 evaluator at the
    /// reference and completes with the seam's value, metering exactly one suffix-evidence call.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reference_root_suffix_completes_through_the_shared_suffix_seam()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var frame = new RecordingFrameSeam(_ => ExactReference(StaticFieldV2FrameValueRootKind.This));
        StaticFieldV2SuffixEvaluationRequest? suffixRequest = null;
        var suffix = StaticFieldV2SuffixEvaluationSource.Create(request =>
        {
            suffixRequest = request;
            return Completed(DumpQueryValue.FromInt32(321));
        });

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "this.Length",
            frame.Source,
            suffixEvaluation: suffix));

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Completed, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.Equal(1, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        Assert.Equal(ExactReferenceAddress, suffixRequest!.ReferenceAddress);
        Assert.Equal(DumpExpressionSuffixKind.DirectMember, suffixRequest.Suffix.Kind);
        var value = Assert.IsType<DumpQueryValue>(result.SuffixValue);
        Assert.Equal(321, value.Int32Value);
    }

    /// <summary>
    /// Proves a primitive frame root with a requested suffix stays Unsupported without any object navigation, exactly
    /// as a metadata literal's suffix is unsupported: the poisoned suffix seam is never consulted.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Primitive_root_with_requested_suffix_is_unsupported()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingFrameSeam(_ => ExactPrimitive(StaticFieldV2FrameValueRootKind.Local, 42));

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "stageLocal.inner",
            seam.Source,
            suffixEvaluation: PoisonedSuffixSeam()));

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(42, result.SignedValue);
        Assert.Equal(DumpExpressionSuffixOutcome.Unsupported, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
    }

    /// <summary>
    /// Proves the two frozen W8.1 non-admissions, a register home and a selected frame's own generic arguments, map to
    /// a typed executable root-attribution non-admission that surfaces the frozen diagnostic code, never an absent gap
    /// and never a crash.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Frozen_non_admissions_surface_their_codes_as_typed_root_attribution_stops()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var cases = new[]
        {
            (StaticFieldV2FrameRootDisposition.RegisterHomeNotAdmitted,
                StaticFieldV2FrameValueRootOutcome.FrameRegisterHomeNotAdmittedCode),
            (StaticFieldV2FrameRootDisposition.GenericArgumentNotAdmitted,
                StaticFieldV2FrameValueRootOutcome.FrameGenericArgumentNotAdmittedCode),
        };

        foreach (var (disposition, code) in cases)
        {
            var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
                world,
                "stageLocal",
                StaticFieldV2FrameRootEvaluationSource.Create(
                    _ => StaticFieldV2FrameRootEvaluationResult.Stop(disposition, code))));

            Assert.Equal(DumpExpressionSyntaxStatus.Admitted, result.Axes.Syntax);
            Assert.Equal(DumpExpressionContextOutcome.Exact, result.Axes.Context);
            Assert.Equal(DumpExpressionRootAttributionOutcome.Unsupported, result.Axes.RootAttribution);
            Assert.Equal(DumpExpressionValueOutcome.NotReached, result.Axes.Value);
            Assert.Equal(DumpExpressionSuffixOutcome.NotReached, result.Axes.Suffix);
            Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);

            // The frozen code is retained on the frame-root result, never mapped to Absent and never thrown.
            Assert.Equal(code, result.Provenance.FrameRoot!.DiagnosticCode);
            Assert.Equal(disposition, result.Provenance.FrameRoot.Disposition);
            Assert.Null(result.SignedValue);
        }
    }

    /// <summary>
    /// Proves an unavailable selected-thread or frame context is a typed context stop rather than a crash: the frame
    /// root is NotReached and the answer is NoAnswer over the incomplete evidence chain.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Context_unavailable_disposition_is_a_typed_stop_not_a_crash()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "stageLocal",
            StaticFieldV2FrameRootEvaluationSource.Create(
                _ => StaticFieldV2FrameRootEvaluationResult.Stop(
                    StaticFieldV2FrameRootDisposition.ContextUnavailable))));

        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, result.Axes.Syntax);
        Assert.Equal(DumpExpressionContextOutcome.Unavailable, result.Axes.Context);
        Assert.Equal(DumpExpressionRootAttributionOutcome.NotReached, result.Axes.RootAttribution);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Null(result.SignedValue);
        Assert.Null(result.Provenance.FrameRoot!.MemoryHomeAddress);
    }

    /// <summary>
    /// Proves a frame-profiled request that supplies no frame-root evidence seam is declined as an unsupported profile
    /// exactly as before this checkpoint: no parse, no seam call, no answer. This preserves the frozen separate-entry
    /// isolation, so the frame binder never runs without its caller-owned dependency.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Frame_request_without_a_seam_is_declined_without_a_parse()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(StaticFieldV2ExpressionRequest.Create(
            "stageLocal",
            DumpExpressionProfileKind.FrameValueExpressionV1,
            world.Ancestry,
            world.Constraints,
            world.FieldCatalogs));

        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, result.Axes.Syntax);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Equal(StaticFieldV2ExpressionRoute.NotSelected, result.Route);
        Assert.Null(result.Provenance.Syntax);
        Assert.Null(result.Provenance.FrameRoot);
        Assert.True(result.Provenance.EvidenceLedger.IsZero);
    }

    /// <summary>
    /// Proves an unsupported frame grammar is a typed syntax stop with no seam call: a two-hop member root over
    /// <see langword="this"/> is outside the frozen one-or-two trailing-member frame grammar's root shape.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Unsupported_frame_grammar_is_a_typed_syntax_stop()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingFrameSeam(_ => ExactPrimitive(StaticFieldV2FrameValueRootKind.This, 1));

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "1 + 2",
            seam.Source));

        Assert.NotEqual(DumpExpressionSyntaxStatus.Admitted, result.Axes.Syntax);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Equal(0, seam.Calls);
        Assert.Equal(0, result.Provenance.EvidenceLedger.FrameRootEvaluationCallCount);
    }

    /// <summary>
    /// Proves the two profiles are isolated entry points in both directions: a static request through the frame entry
    /// point and a frame request through the static entry point are both declined without a parse or a seam call.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void The_two_entry_points_reject_the_other_profile()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var frameSeam = new RecordingFrameSeam(_ => ExactPrimitive(StaticFieldV2FrameValueRootKind.This, 1));

        // A static-profiled request through the frame entry point is rejected.
        var staticThroughFrame = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "this",
            frameSeam.Source,
            profile: DumpExpressionProfileKind.StaticFieldExpressionV2));

        // A frame-profiled request through the static entry point is rejected even with a frame-root seam present.
        var frameThroughStatic = StaticFieldV2ExpressionPipeline.Evaluate(FrameRequest(
            world,
            "this",
            frameSeam.Source));

        foreach (var rejected in new[] { staticThroughFrame, frameThroughStatic })
        {
            Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, rejected.Axes.Syntax);
            Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, rejected.Axes.Completeness);
            Assert.Equal(StaticFieldV2ExpressionRoute.NotSelected, rejected.Route);
            Assert.Null(rejected.Provenance.Syntax);
            Assert.Null(rejected.Provenance.FrameRoot);
            Assert.True(rejected.Provenance.EvidenceLedger.IsZero);
        }

        Assert.Equal(0, frameSeam.Calls);
    }

    /// <summary>Proves replaying the same frame request reproduces a byte-identical canonical result.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Replaying_the_same_frame_request_reproduces_the_canonical_result()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var replayWorld = W8V2ExpressionPipelineTests.BuildWorld();

        var first = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            world,
            "stageLocal",
            new RecordingFrameSeam(_ => ExactLocal(MemoryHomedLocalValue)).Source));
        var replay = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(FrameRequest(
            replayWorld,
            "stageLocal",
            new RecordingFrameSeam(_ => ExactLocal(MemoryHomedLocalValue)).Source));

        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.True(first.Equals(replay));
    }

    private static StaticFieldV2ExpressionRequest FrameRequest(
        W8V2ExpressionPipelineTests.PipelineWorld world,
        string expression,
        StaticFieldV2FrameRootEvaluationSource frameRootEvaluation,
        StaticFieldV2SuffixEvaluationSource? suffixEvaluation = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null,
        DumpExpressionProfileKind profile = DumpExpressionProfileKind.FrameValueExpressionV1) =>
        StaticFieldV2ExpressionRequest.Create(
            expression,
            profile,
            world.Ancestry,
            world.Constraints,
            world.FieldCatalogs,
            suffixEvaluation: suffixEvaluation,
            capabilityProbes: capabilityProbes,
            frameRootEvaluation: frameRootEvaluation);

    private static StaticFieldV2FrameRootEvaluationResult ExactLocal(int value) =>
        StaticFieldV2FrameRootEvaluationResult.Exact(
            StaticFieldV2FrameValueRootKind.Local,
            ExactHomeAddress,
            4,
            LittleEndian(value),
            DumpQueryValue.FromInt32(value));

    private static StaticFieldV2FrameRootEvaluationResult ExactPrimitive(
        StaticFieldV2FrameValueRootKind rootKind,
        int value) =>
        StaticFieldV2FrameRootEvaluationResult.Exact(
            rootKind,
            ExactHomeAddress,
            4,
            LittleEndian(value),
            DumpQueryValue.FromInt32(value));

    private static StaticFieldV2FrameRootEvaluationResult ExactNull(StaticFieldV2FrameValueRootKind rootKind) =>
        StaticFieldV2FrameRootEvaluationResult.Exact(
            rootKind,
            ExactHomeAddress,
            8,
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            DumpQueryValue.FromNull());

    private static StaticFieldV2FrameRootEvaluationResult ExactReference(StaticFieldV2FrameValueRootKind rootKind) =>
        StaticFieldV2FrameRootEvaluationResult.Exact(
            rootKind,
            ExactHomeAddress,
            8,
            [0x00, 0x50, 0x00, 0x34, 0xF2, 0x01, 0x00, 0x00],
            DumpQueryValue.FromString("frame"),
            ExactReferenceAddress);

    private static ImmutableArray<byte> LittleEndian(int value) =>
    [
        (byte)(value & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 24) & 0xFF),
    ];

    private static StaticFieldV2SuffixEvaluationSource PoisonedSuffixSeam() =>
        StaticFieldV2SuffixEvaluationSource.Create(
            static _ => throw new InvalidOperationException("suffix evaluation"));

    private static EvaluationResult<DumpQueryValue> Completed(DumpQueryValue value) =>
        EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            value);

    private sealed class RecordingFrameSeam
    {
        public RecordingFrameSeam(
            Func<StaticFieldV2FrameRootEvaluationRequest, StaticFieldV2FrameRootEvaluationResult> responder)
        {
            Source = StaticFieldV2FrameRootEvaluationSource.Create(request =>
            {
                Calls++;
                LastRequest = request;
                return responder(request);
            });
        }

        public StaticFieldV2FrameRootEvaluationSource Source { get; }

        public int Calls { get; private set; }

        public StaticFieldV2FrameRootEvaluationRequest? LastRequest { get; private set; }
    }
}
