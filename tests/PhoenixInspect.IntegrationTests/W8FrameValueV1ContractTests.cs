using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the detached W8 frame-value syntax contract with complex synthetic examples.</summary>
public sealed class W8FrameValueV1ContractTests
{
    /// <summary>Proves <see langword="this"/> and identifier roots retain one unchanged bounded suffix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Admitted_roots_are_profile_sealed_and_content_equal()
    {
        var state = Segment("State", DumpExpressionSuffixAccessKind.Direct);
        var payload = Segment("Payload", DumpExpressionSuffixAccessKind.Conditional);
        var thisSuffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.FixedDepthMemberChain,
            ImmutableArray.Create(state, payload),
            DumpExpressionFallbackKind.String,
            stringFallback: "missing");
        var thisCounts = Counts(
            nodeCount: 14,
            tokenCount: 10,
            maximumDepth: 7,
            rootIdentifier: null,
            thisSuffix);
        var thisDescriptor = FrameValueV1ExpressionDescriptor.Create(
            "this.State?.Payload ?? \"missing\"",
            FrameValueV1RootKind.This,
            identifier: null,
            thisSuffix,
            thisCounts,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var replay = FrameValueV1ExpressionDescriptor.Create(
            "this.State?.Payload ?? \"missing\"",
            FrameValueV1RootKind.This,
            identifier: null,
            DumpExpressionSuffixDescriptor.Create(
                DumpExpressionSuffixKind.FixedDepthMemberChain,
                ImmutableArray.Create(Segment("State"), Segment("Payload", DumpExpressionSuffixAccessKind.Conditional)),
                DumpExpressionFallbackKind.String,
                stringFallback: "missing"),
            Counts(14, 10, 7, null, thisSuffix),
            ImmutableArray<EvaluationDeterministicBound>.Empty);

        Assert.Equal(DumpExpressionProfileKind.FrameValueExpressionV1, thisDescriptor.Profile);
        Assert.Equal(FrameValueV1RootKind.This, thisDescriptor.RootKind);
        Assert.Null(thisDescriptor.Identifier);
        Assert.Equal(thisDescriptor, replay);
        Assert.Equal(thisDescriptor.Sha256, replay.Sha256);
        Assert.Equal(thisDescriptor.GetHashCode(), replay.GetHashCode());

        var escapedKeyword = DumpExpressionIdentifier.Create("class");
        var identifierSuffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.DirectMember,
            ImmutableArray.Create(Segment("Payload", DumpExpressionSuffixAccessKind.Conditional)),
            DumpExpressionFallbackKind.Int32,
            int32Fallback: 17);
        var identifierDescriptor = FrameValueV1ExpressionDescriptor.Create(
            "@class?.Payload ?? 17",
            FrameValueV1RootKind.Identifier,
            escapedKeyword,
            identifierSuffix,
            Counts(11, 8, 6, escapedKeyword, identifierSuffix),
            ImmutableArray<EvaluationDeterministicBound>.Empty);

        Assert.Equal("class", identifierDescriptor.Identifier!.DecodedText);
        Assert.NotEqual(thisDescriptor, identifierDescriptor);
        Assert.Equal(DumpExpressionRouteKind.FrameValue, FrameRoute());
        Assert.Equal(
            new[] { FrameValueV1RootKind.This, FrameValueV1RootKind.Identifier },
            Enum.GetValues<FrameValueV1RootKind>());
    }

    /// <summary>Proves exact-at-cap descriptors remain complete while cap-plus-one stops retain no prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Identifier_boundary_is_exact_at_cap_and_factless_at_cap_plus_one()
    {
        var identifier = DumpExpressionIdentifier.Create(
            new string('x', ExpressionV2ContractLimits.MaximumIdentifierCharacterCount));
        var suffix = DumpExpressionSuffixDescriptor.NotRequested();
        var atCapCounts = Counts(2, 1, 2, identifier, suffix);
        var identifierBound = Bound(ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);
        var admitted = FrameValueV1ExpressionDescriptor.Create(
            identifier.DecodedText,
            FrameValueV1RootKind.Identifier,
            identifier,
            suffix,
            atCapCounts,
            ImmutableArray.Create(identifierBound));

        Assert.Equal(identifierBound, Assert.Single(admitted.ReachedBounds));

        var capPlusOneText = new string('x', ExpressionV2ContractLimits.MaximumIdentifierCharacterCount + 1);
        var capPlusOneCounts = FrameValueV1ParserCounts.Create(
            nodeCount: 2,
            tokenCount: 1,
            maximumDepth: 2,
            maximumDecodedIdentifierLength: ExpressionV2ContractLimits.MaximumIdentifierCharacterCount + 1,
            maximumDecodedFallbackStringLength: 0);
        var stopped = FrameValueV1SyntaxOutcome.Invalid(
            capPlusOneText,
            FrameValueV1SyntaxIssue.IdentifierBoundReached,
            capPlusOneCounts,
            ImmutableArray.Create(Error("W8_FRAME_IDENTIFIER_BOUND", DumpExpressionDiagnosticStage.Parse)),
            ImmutableArray.Create(identifierBound));

        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, stopped.Status);
        Assert.Null(stopped.Descriptor);
        Assert.Equal(capPlusOneText, stopped.RawExpression);
        Assert.Equal(ExpressionV2ContractLimits.MaximumIdentifierCharacterCount + 1,
            stopped.ParserCounts.MaximumDecodedIdentifierLength);
        Assert.Equal(identifierBound, Assert.Single(stopped.ReachedBounds));

        Assert.Throws<ArgumentException>(() => FrameValueV1SyntaxOutcome.Unsupported(
            capPlusOneText,
            FrameValueV1SyntaxIssue.TreeShapeUnsupported,
            capPlusOneCounts,
            ImmutableArray.Create(Error("W8_FRAME_SHAPE", DumpExpressionDiagnosticStage.Projection)),
            ImmutableArray.Create(identifierBound)));
    }

    /// <summary>Proves stopped outcomes preserve syntax-stage typing and cannot carry an admitted descriptor.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Invalid_and_unsupported_outcomes_are_stage_typed()
    {
        var emptyCounts = FrameValueV1ParserCounts.Create(0, 0, 0, 0, 0);
        var invalid = FrameValueV1SyntaxOutcome.Invalid(
            "(",
            FrameValueV1SyntaxIssue.ParseError,
            emptyCounts,
            ImmutableArray.Create(Error("W8_FRAME_PARSE", DumpExpressionDiagnosticStage.Parse)),
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, invalid.Status);
        Assert.Equal(DumpExpressionProfileKind.FrameValueExpressionV1, invalid.Profile);
        Assert.Null(invalid.Descriptor);

        var completeCounts = FrameValueV1ParserCounts.Create(5, 4, 4, 5, 0);
        var unsupported = FrameValueV1SyntaxOutcome.Unsupported(
            "value + 1",
            FrameValueV1SyntaxIssue.RootShapeUnsupported,
            completeCounts,
            ImmutableArray.Create(Error("W8_FRAME_ROOT", DumpExpressionDiagnosticStage.Projection)),
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, unsupported.Status);
        Assert.Null(unsupported.Descriptor);

        Assert.Throws<ArgumentException>(() => FrameValueV1SyntaxOutcome.Invalid(
            "(",
            FrameValueV1SyntaxIssue.RootShapeUnsupported,
            emptyCounts,
            ImmutableArray.Create(Error("W8_FRAME_PARSE", DumpExpressionDiagnosticStage.Parse)),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => FrameValueV1SyntaxOutcome.Unsupported(
            "value + 1",
            FrameValueV1SyntaxIssue.RootShapeUnsupported,
            completeCounts,
            ImmutableArray.Create(Error("W8_FRAME_WRONG_STAGE", DumpExpressionDiagnosticStage.Parse)),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => FrameValueV1SyntaxOutcome.Unsupported(
            "value + 1",
            FrameValueV1SyntaxIssue.RootShapeUnsupported,
            completeCounts,
            ImmutableArray.Create(Error("W8_FRAME_STORAGE", DumpExpressionDiagnosticStage.Storage)),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
    }

    /// <summary>Proves descriptor factories reject every contradictory root, counter, suffix, and bound payload.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Descriptor_invalid_input_matrix_rejects_contradictions()
    {
        var identifier = DumpExpressionIdentifier.Create("current");
        var suffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.DirectMember,
            ImmutableArray.Create(Segment("Value")));
        var counts = Counts(6, 4, 4, identifier, suffix);

        Assert.Throws<ArgumentException>(() => FrameValueV1ExpressionDescriptor.Create(
            "current.Value",
            FrameValueV1RootKind.This,
            identifier,
            suffix,
            counts,
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => FrameValueV1ExpressionDescriptor.Create(
            "current.Value",
            FrameValueV1RootKind.Identifier,
            identifier: null,
            suffix,
            counts,
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameValueV1ExpressionDescriptor.Create(
            "current.Value",
            (FrameValueV1RootKind)999,
            identifier,
            suffix,
            counts,
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => FrameValueV1ExpressionDescriptor.Create(
            "current.Value",
            FrameValueV1RootKind.Identifier,
            identifier,
            suffix,
            FrameValueV1ParserCounts.Create(6, 4, 4, 6, 0),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => FrameValueV1ExpressionDescriptor.Create(
            "current.Value",
            FrameValueV1RootKind.Identifier,
            identifier,
            suffix,
            counts,
            ImmutableArray.Create(Bound(ExpressionV2ContractLimits.SyntaxDepthBoundName))));
        Assert.Throws<ArgumentException>(() => FrameValueV1ExpressionDescriptor.Create(
            "current.Value",
            FrameValueV1RootKind.Identifier,
            identifier,
            suffix,
            FrameValueV1ParserCounts.Create(0, 0, 0, 7, 0),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
    }

    /// <summary>Proves canonical identity changes independently with root, suffix, counters, bounds, and raw spelling.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Canonical_identity_tracks_each_detached_syntax_dimension()
    {
        var identifier = DumpExpressionIdentifier.Create("item");
        var noSuffix = DumpExpressionSuffixDescriptor.NotRequested();
        var baseline = FrameValueV1ExpressionDescriptor.Create(
            "item",
            FrameValueV1RootKind.Identifier,
            identifier,
            noSuffix,
            Counts(2, 1, 2, identifier, noSuffix),
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var rawSpelling = FrameValueV1ExpressionDescriptor.Create(
            "@item",
            FrameValueV1RootKind.Identifier,
            identifier,
            noSuffix,
            Counts(2, 1, 2, identifier, noSuffix),
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var thisRoot = FrameValueV1ExpressionDescriptor.Create(
            "this",
            FrameValueV1RootKind.This,
            null,
            noSuffix,
            Counts(2, 1, 2, null, noSuffix),
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var memberSuffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.DirectMember,
            ImmutableArray.Create(Segment("Value")));
        var withSuffix = FrameValueV1ExpressionDescriptor.Create(
            "item.Value",
            FrameValueV1RootKind.Identifier,
            identifier,
            memberSuffix,
            Counts(6, 3, 4, identifier, memberSuffix),
            ImmutableArray<EvaluationDeterministicBound>.Empty);

        Assert.NotEqual(baseline.Sha256, rawSpelling.Sha256);
        Assert.NotEqual(baseline.Sha256, thisRoot.Sha256);
        Assert.NotEqual(baseline.Sha256, withSuffix.Sha256);
        Assert.NotEqual(baseline.CanonicalBytes.ToArray(), withSuffix.CanonicalBytes.ToArray());

        var copy = baseline.CanonicalBytes.ToArray();
        copy[0] ^= 0x7f;
        Assert.NotEqual(copy, baseline.CanonicalBytes.ToArray());
    }

    private static DumpExpressionRouteKind FrameRoute() => DumpExpressionRouteKind.FrameValue;

    private static FrameValueV1ParserCounts Counts(
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        DumpExpressionIdentifier? rootIdentifier,
        DumpExpressionSuffixDescriptor suffix)
    {
        var maximumIdentifierLength = suffix.Segments
            .Select(static segment => segment.Identifier.DecodedText.Length)
            .Append(rootIdentifier?.DecodedText.Length ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return FrameValueV1ParserCounts.Create(
            nodeCount,
            tokenCount,
            maximumDepth,
            maximumIdentifierLength,
            suffix.StringFallback?.Length ?? 0);
    }

    private static DumpExpressionSuffixSegment Segment(
        string name,
        DumpExpressionSuffixAccessKind accessKind = DumpExpressionSuffixAccessKind.Direct) =>
        DumpExpressionSuffixSegment.Create(DumpExpressionIdentifier.Create(name), accessKind);

    private static DumpExpressionDiagnostic Error(string code, DumpExpressionDiagnosticStage stage) =>
        DumpExpressionDiagnostic.Create(
            code,
            stage,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty);

    private static EvaluationDeterministicBound Bound(string name) =>
        ExpressionV2ContractLimits.AllDeclaredBounds.Single(bound =>
            string.Equals(bound.Name, name, StringComparison.Ordinal));
}
