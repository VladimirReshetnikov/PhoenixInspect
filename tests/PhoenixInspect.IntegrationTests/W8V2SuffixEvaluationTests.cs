using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the composed static-field V2 pipeline's wiring of the unchanged W2/W6 detached suffix through the
/// caller-supplied suffix-evaluation seam: real completion, exact-null short-circuit, value-type unsupported,
/// blocked-target blocking, absent-seam blocking, disposition mapping, and the twelve-axis composition.
/// </summary>
public sealed class W8V2SuffixEvaluationTests
{
    private const string DirectMemberSuffixGoldenSha256 =
        "37bec7e76782ef92e3843b0dd5c35d2c5f819e6257177a21c7a304bb15b097fa";

    private const ulong ExactReferenceAddress = 0x4030_2010UL;

    /// <summary>
    /// Proves a direct-member suffix over an exact non-null reference roots the unchanged W2/W6 evaluator at that
    /// reference, completes with the seam's value, meters exactly one suffix-evidence call, composes the twelve-axis
    /// result with Complete completeness, and freezes a canonical result digest.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Direct_member_suffix_over_exact_reference_completes_with_a_value()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingSeam(_ => Completed(DumpQueryValue.FromInt32(123)));

        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text.Length",
            runtimeEvidence: ReferenceEvidence(world),
            suffixEvaluation: seam.Source));

        // The suffix axis is genuinely composed into the full twelve-axis result with Complete completeness.
        Assert.Equal(StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal, result.Route);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, result.Axes.Syntax);
        Assert.Equal(DumpExpressionContextOutcome.NotRequired, result.Axes.Context);
        Assert.Equal(DumpExpressionRootAttributionOutcome.NotRequired, result.Axes.RootAttribution);
        Assert.Equal(DumpExpressionLexicalCompletenessOutcome.NotRequired, result.Axes.LexicalCompleteness);
        Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, result.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, result.Axes.TypeConstruction);
        Assert.Equal(DumpExpressionMemberLookupOutcome.Exact, result.Axes.MemberLookup);
        Assert.Equal(DumpExpressionRuntimeConstructionOutcome.Exact, result.Axes.RuntimeConstruction);
        Assert.Equal(DumpExpressionStorageOutcome.Exact, result.Axes.Storage);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Completed, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.True(result.IsComplete);

        // The chain was rooted at the resolved exact reference and the frozen suffix descriptor was handed over.
        Assert.Equal(ExactReferenceAddress, result.ReferenceAddress);
        Assert.Equal(1, seam.Calls);
        Assert.Equal(ExactReferenceAddress, seam.LastRequest!.ReferenceAddress);
        Assert.Equal(DumpExpressionSuffixKind.DirectMember, seam.LastRequest.Suffix.Kind);
        Assert.True(seam.LastRequest.Suffix.Equals(result.Provenance.Suffix));

        // The evidence ledger meters the one suffix-evidence call on its own axis.
        var ledger = result.Provenance.EvidenceLedger;
        Assert.Equal(1, ledger.SuffixChainEvaluationCallCount);
        Assert.Equal(1, ledger.CallCount(StaticFieldV2PipelineEvidenceKind.SuffixChainEvaluation));

        // The completed suffix value is retained in provenance and surfaced by the result.
        var suffixValue = Assert.IsType<DumpQueryValue>(result.SuffixValue);
        Assert.Equal(DumpQueryValueKind.Int32, suffixValue.Kind);
        Assert.Equal(123, suffixValue.Int32Value);
        Assert.Same(result.SuffixValue, result.Provenance.SuffixValue);
        Assert.Equal(DirectMemberSuffixGoldenSha256, result.Sha256);
    }

    /// <summary>
    /// Proves a two-hop conditional member chain (<c>a?.b?.c</c>) is projected as a fixed-depth conditional suffix,
    /// rooted at the exact reference, and completed with the seam's value.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Two_hop_conditional_chain_completes_with_a_value()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var seam = new RecordingSeam(_ => Completed(DumpQueryValue.FromString("deep")));

        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text?.inner?.leaf",
            runtimeEvidence: ReferenceEvidence(world),
            suffixEvaluation: seam.Source));

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Completed, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.True(result.IsComplete);

        Assert.Equal(1, seam.Calls);
        Assert.Equal(ExactReferenceAddress, seam.LastRequest!.ReferenceAddress);
        Assert.Equal(DumpExpressionSuffixKind.FixedDepthMemberChain, seam.LastRequest.Suffix.Kind);
        Assert.Equal(2, seam.LastRequest.Suffix.Segments.Length);
        Assert.All(
            seam.LastRequest.Suffix.Segments,
            segment => Assert.Equal(DumpExpressionSuffixAccessKind.Conditional, segment.AccessKind));

        var suffixValue = Assert.IsType<DumpQueryValue>(result.SuffixValue);
        Assert.Equal(DumpQueryValueKind.String, suffixValue.Kind);
        Assert.Equal("deep", suffixValue.StringValue);
        Assert.Equal(1, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
    }

    /// <summary>
    /// Proves a <c>?? fallback</c> coalesce over an exact-null reference reaches the fallback WITHOUT a suffix read:
    /// the conditional first edge short-circuits to the coalesce literal, and a poisoned seam proves zero
    /// suffix-evidence calls.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Coalesce_over_exact_null_reference_reaches_fallback_without_a_read()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text?.member ?? 7",
            runtimeEvidence: NullReferenceEvidence(world),
            suffixEvaluation: PoisonedSuffixSeam()));

        Assert.Equal(DumpExpressionValueOutcome.ExactNull, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Completed, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.True(result.IsComplete);
        Assert.Null(result.ReferenceAddress);

        // The suffix short-circuited over the exact-null root: the poisoned seam was never consulted.
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);

        // The coalesce fallback literal was retained without any suffix read.
        var fallback = Assert.IsType<DumpQueryValue>(result.SuffixValue);
        Assert.Equal(DumpQueryValueKind.Int32, fallback.Kind);
        Assert.Equal(7, fallback.Int32Value);

        // The one raw read that did happen was the root null-reference read, not a suffix read.
        Assert.Equal(1, result.Provenance.EvidenceLedger.RawMemoryReadCallCount);
    }

    /// <summary>
    /// Proves a direct <c>.</c> first edge over an exact-null reference is the unchanged W2/W6 null-target block and
    /// still never consults the suffix seam.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Direct_access_over_exact_null_reference_blocks_without_a_read()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text.member",
            runtimeEvidence: NullReferenceEvidence(world),
            suffixEvaluation: PoisonedSuffixSeam()));

        Assert.Equal(DumpExpressionValueOutcome.ExactNull, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Blocked, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        Assert.Null(result.SuffixValue);
    }

    /// <summary>
    /// Proves a value-type/primitive field value with a requested suffix stays Unsupported without any object
    /// navigation, exactly as a metadata literal's suffix is unsupported: the poisoned seam is never consulted.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Value_type_field_with_requested_suffix_is_unsupported()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Current.member",
            runtimeEvidence: W8V2ExpressionPipelineTests.ConstructedSlotEvidence(world, 0x2A),
            suffixEvaluation: PoisonedSuffixSeam()));

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(42, result.SignedValue);
        Assert.Null(result.ReferenceAddress);
        Assert.Equal(DumpExpressionSuffixOutcome.Unsupported, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        Assert.Null(result.SuffixValue);
    }

    /// <summary>
    /// Proves a blocked reference target blocks the suffix before any navigation and never consults the seam, even
    /// though the suffix is requested and a seam is supplied.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Blocked_reference_target_blocks_the_suffix_without_consulting_the_seam()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text.member",
            runtimeEvidence: ReferenceEvidence(world),
            suffixEvaluation: PoisonedSuffixSeam(),
            referenceTargetType: MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int32)));

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Blocked, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.NotEqual(
            StaticFieldV2AssignabilityResultKind.Assignable,
            result.Provenance.ReferenceTargetValidation!.ResultKind);
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        Assert.Null(result.SuffixValue);
    }

    /// <summary>
    /// Proves that when no suffix seam is supplied a requested reference suffix is a typed Blocked (evidence
    /// unavailable) rather than a crash.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Missing_suffix_seam_blocks_a_requested_reference_suffix()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.GenericSlot<int>.Text.member",
            runtimeEvidence: ReferenceEvidence(world)));

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Blocked, result.Axes.Suffix);
        Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
        Assert.Equal(0, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        Assert.Null(result.Provenance.SuffixValue);
    }

    /// <summary>
    /// Proves the seam's own <see cref="EvaluationResult{DumpQueryValue}"/> dispositions map onto the independent
    /// suffix axis: partial and unavailable become Blocked, conflict becomes Conflict, and invalid becomes Invalid,
    /// with each mapped stop forcing NoAnswer completeness and no retained suffix value.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Suffix_seam_dispositions_map_onto_the_independent_suffix_axis()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var cases = new (EvaluationResult<DumpQueryValue> SeamResult, DumpExpressionSuffixOutcome Expected)[]
        {
            (NonExact(EvaluationCompletionStatus.Blocked, EvaluationEvidenceStatus.Unavailable),
                DumpExpressionSuffixOutcome.Blocked),
            (PartialPrefix(), DumpExpressionSuffixOutcome.Blocked),
            (NonExact(EvaluationCompletionStatus.Blocked, EvaluationEvidenceStatus.Conflict),
                DumpExpressionSuffixOutcome.Conflict),
            (NonExact(EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Invalid),
                DumpExpressionSuffixOutcome.Invalid),
        };

        foreach (var (seamResult, expected) in cases)
        {
            var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
                world,
                "global::Pipe.App.GenericSlot<int>.Text.member",
                runtimeEvidence: ReferenceEvidence(world),
                suffixEvaluation: StaticFieldV2SuffixEvaluationSource.Create(_ => seamResult)));

            Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
            Assert.Equal(expected, result.Axes.Suffix);
            Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, result.Axes.Completeness);
            Assert.Null(result.SuffixValue);
            Assert.Equal(1, result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        }
    }

    private static StaticFieldV2RuntimeEvidenceSource ReferenceEvidence(W8V2ExpressionPipelineTests.PipelineWorld world) =>
        StaticFieldV2RuntimeEvidenceSource.Create(
            (construction, strategy) => W8V2ExpressionPipelineTests.Candidates(world, construction, strategy),
            static (_, _) => StaticFieldV2RuntimeSlotFacts.Create(8, 0x5000_0040UL),
            static (_, _) => [0x10, 0x20, 0x30, 0x40, 0x00, 0x00, 0x00, 0x00]);

    private static StaticFieldV2RuntimeEvidenceSource NullReferenceEvidence(
        W8V2ExpressionPipelineTests.PipelineWorld world) =>
        StaticFieldV2RuntimeEvidenceSource.Create(
            (construction, strategy) => W8V2ExpressionPipelineTests.Candidates(world, construction, strategy),
            static (_, _) => StaticFieldV2RuntimeSlotFacts.Create(8, 0x5000_0040UL),
            static (_, _) => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

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

    private static EvaluationResult<DumpQueryValue> NonExact(
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence) =>
        EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            completion,
            EvaluationCompleteness.None,
            evidence,
            EvaluationEffectStatus.None,
            value: null);

    private static EvaluationResult<DumpQueryValue> PartialPrefix() =>
        EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Partial,
            EvaluationEffectStatus.None,
            DumpQueryValue.FromString("pre"));

    private sealed class RecordingSeam
    {
        public RecordingSeam(Func<StaticFieldV2SuffixEvaluationRequest, EvaluationResult<DumpQueryValue>> responder)
        {
            Source = StaticFieldV2SuffixEvaluationSource.Create(request =>
            {
                Calls++;
                LastRequest = request;
                return responder(request);
            });
        }

        public StaticFieldV2SuffixEvaluationSource Source { get; }

        public int Calls { get; private set; }

        public StaticFieldV2SuffixEvaluationRequest? LastRequest { get; private set; }
    }
}
