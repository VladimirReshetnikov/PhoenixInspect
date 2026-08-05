using System.Collections.Immutable;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the composed static-field V2 pipeline's owner <c>VAR</c> field-signature substitution: an
/// <c>ELEMENT_TYPE_VAR</c> field signature is decoded to the bound closed owner construction's ordered closed argument,
/// which then flows into the unchanged storage and value decoding. Every world here is independent of the frozen
/// <see cref="W8V2ExpressionPipelineTests"/> goldens, so no frozen static-path result digest is touched.
/// </summary>
public sealed class W8V2GenericVarFieldTests
{
    private const int SlotTypeDefinitionToken = 0x0200_0002;

    // Captured from a real run of Owner_var_substituted_result_replays_to_a_stable_digest below.
    private const string OwnerVarInt32GoldenSha256 =
        "85f750b5716b4d6025cd4bcdfef0fa9f0c19a9531db4c0db935d2ca26ee47a5f";

    /// <summary>
    /// Proves an owner <c>VAR 0</c> field signature over a closed <c>Slot&lt;int&gt;</c> construction is substituted to
    /// the <c>Int32</c> argument and reaches an exact value through the existing value decoder, and that the broadened
    /// grammar drops the ground-primitive declared-type boundary for the substituted field.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Owner_var_field_substitutes_the_closed_argument_and_reaches_a_value()
    {
        var world = W8V2ExpressionPipelineTests.BuildGenericVarWorld();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.Slot<int>.Current",
            runtimeEvidence: ConstructedEvidence(world, [0x2A, 0x00, 0x00, 0x00])));

        Assert.Equal(StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal, result.Route);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, result.Axes.TypeConstruction);
        Assert.Equal(DumpExpressionMemberLookupOutcome.Exact, result.Axes.MemberLookup);
        Assert.Equal(DumpExpressionStorageOutcome.Exact, result.Axes.Storage);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);
        Assert.Equal(42, result.SignedValue);

        // The owner VAR was substituted, so the ground-primitive declared-type boundary no longer applies to it.
        Assert.DoesNotContain(
            StaticFieldV2PipelineCoverageBoundary.DeclaredFieldTypeLimitedToGroundPrimitiveSignature,
            result.Provenance.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves the SAME owner <c>VAR 0</c> field signature substitutes to a different closed argument per construction:
    /// over <c>Slot&lt;long&gt;</c> the declared type becomes <c>Int64</c>, decoded from eight bytes, while the same
    /// four-byte read that succeeds for <c>Slot&lt;int&gt;</c> is a typed width disagreement for the wider argument.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Owner_var_substitution_tracks_the_construction_argument_width()
    {
        var world = W8V2ExpressionPipelineTests.BuildGenericVarWorld();

        var wideValue = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.Slot<long>.Current",
            runtimeEvidence: ConstructedEvidence(world, [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08])));
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, wideValue.Axes.Value);
        Assert.Equal(0x0807_0605_0403_0201L, wideValue.SignedValue);
        Assert.DoesNotContain(
            StaticFieldV2PipelineCoverageBoundary.DeclaredFieldTypeLimitedToGroundPrimitiveSignature,
            wideValue.Provenance.DeclaredCoverageBoundaries);

        // The exact same VAR field over Slot<int> declares a four-byte Int32, so an eight-byte read is a typed width
        // disagreement rather than a value: the substituted declared width tracks the construction argument.
        var narrowMismatch = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.Slot<int>.Current",
            runtimeEvidence: ConstructedEvidence(world, [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08])));
        Assert.Equal(DumpExpressionValueOutcome.Invalid, narrowMismatch.Axes.Value);
    }

    /// <summary>
    /// Proves an owner <c>VAR 1</c> field signature over an arity-one <c>Slot&lt;int&gt;</c> construction is an
    /// incomplete substitution: the out-of-arity index is a typed non-answer (unsupported value), never an absence and
    /// never a fault, and the field still declares the ground-primitive declared-type boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Out_of_arity_owner_var_is_a_typed_non_answer()
    {
        var world = W8V2ExpressionPipelineTests.BuildGenericVarWorld();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.Slot<int>.OutOfArity",
            runtimeEvidence: ConstructedEvidence(world, [0x2A, 0x00, 0x00, 0x00])));

        Assert.Equal(DumpExpressionMemberLookupOutcome.Exact, result.Axes.MemberLookup);
        Assert.Equal(DumpExpressionStorageOutcome.Exact, result.Axes.Storage);
        Assert.Equal(DumpExpressionValueOutcome.Unsupported, result.Axes.Value);
        Assert.NotEqual(DumpExpressionCompletenessOutcome.Complete, result.Axes.Completeness);

        // The substitution never completed, so the field is still limited to the ground-primitive grammar.
        Assert.Contains(
            StaticFieldV2PipelineCoverageBoundary.DeclaredFieldTypeLimitedToGroundPrimitiveSignature,
            result.Provenance.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Regression guard: a non-VAR ground <c>Int32</c> field over the same generic owner still decodes exactly as
    /// before through the unchanged two-byte fast path and still declares the ground-primitive boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ground_primitive_field_over_a_generic_owner_is_unchanged()
    {
        var world = W8V2ExpressionPipelineTests.BuildGenericVarWorld();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.Slot<int>.IntCurrent",
            runtimeEvidence: ConstructedEvidence(world, [0x2A, 0x00, 0x00, 0x00])));

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, result.Axes.Value);
        Assert.Equal(42, result.SignedValue);
        Assert.Contains(
            StaticFieldV2PipelineCoverageBoundary.DeclaredFieldTypeLimitedToGroundPrimitiveSignature,
            result.Provenance.DeclaredCoverageBoundaries);
    }

    /// <summary>Freezes the owner-VAR substituted result digest captured from a real run and proves canonical replay.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Owner_var_substituted_result_replays_to_a_stable_digest()
    {
        var world = W8V2ExpressionPipelineTests.BuildGenericVarWorld();
        var replayWorld = W8V2ExpressionPipelineTests.BuildGenericVarWorld();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "global::Pipe.App.Slot<int>.Current",
            runtimeEvidence: ConstructedEvidence(world, [0x2A, 0x00, 0x00, 0x00])));
        var replay = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            replayWorld,
            "global::Pipe.App.Slot<int>.Current",
            runtimeEvidence: ConstructedEvidence(replayWorld, [0x2A, 0x00, 0x00, 0x00])));

        Assert.Equal(result.Sha256, replay.Sha256);
        Assert.Equal(result, replay);
        Assert.Equal(OwnerVarInt32GoldenSha256, result.Sha256);
    }

    private static StaticFieldV2RuntimeEvidenceSource ConstructedEvidence(
        W8V2ExpressionPipelineTests.PipelineWorld world,
        byte[] payload) =>
        StaticFieldV2RuntimeEvidenceSource.Create(
            (construction, strategy) =>
            {
                Assert.Equal(StaticFieldV2StorageStrategy.ConstructedSlot, strategy.Strategy);
                return
                [
                    StaticFieldV2RuntimeConstructionCandidate.Create(
                        0x0001_0200UL,
                        0x0002_0200UL,
                        world.App,
                        SlotTypeDefinitionToken,
                        world.App,
                        world.App.ContainingAssembly,
                        0x7000,
                        0x8000,
                        construction.FlattenedArguments),
                ];
            },
            (_, _) => StaticFieldV2RuntimeSlotFacts.Create(payload.Length, 0x5000_0040UL),
            (_, _) => [.. payload]);
}
