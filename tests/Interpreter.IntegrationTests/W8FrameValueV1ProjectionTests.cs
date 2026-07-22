using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises the W8.3 production projection from the sole Roslyn parse into frame-value V1 contracts.</summary>
public sealed class W8FrameValueV1ProjectionTests
{
    private const string ConditionalChainExpression = "this?.Owner?.Name ?? \"none\"";

    // Golden digest captured from a real FrameValueV1ExpressionParser.Parse run; never hand-computed.
    private const string GoldenConditionalChainDescriptorSha256 =
        "772b1a97a99b9053ac4a883ce728538f03e18b94baa14bcaeaa606c93059b7c1";

    /// <summary>Proves the bare this and identifier roots admit with no suffix and exact counters.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void This_and_identifier_roots_are_admitted_without_suffix()
    {
        var thisOutcome = FrameValueV1ExpressionParser.Parse("this");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, thisOutcome.Status);
        Assert.Equal(FrameValueV1SyntaxIssue.None, thisOutcome.Issue);
        Assert.Empty(thisOutcome.Diagnostics);
        Assert.Empty(thisOutcome.ReachedBounds);
        var thisDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(thisOutcome.Descriptor);
        Assert.Equal(FrameValueV1RootKind.This, thisDescriptor.RootKind);
        Assert.Null(thisDescriptor.Identifier);
        Assert.Equal(DumpExpressionSuffixKind.NotRequested, thisDescriptor.Suffix.Kind);
        Assert.Equal(DumpExpressionFallbackKind.None, thisDescriptor.Suffix.FallbackKind);
        AssertCounts(thisOutcome.ParserCounts, 1, 1, 1, 0, 0);

        var localOutcome = FrameValueV1ExpressionParser.Parse("local");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, localOutcome.Status);
        var localDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(localOutcome.Descriptor);
        Assert.Equal(FrameValueV1RootKind.Identifier, localDescriptor.RootKind);
        Assert.Equal("local", localDescriptor.Identifier!.DecodedText);
        Assert.Equal(DumpExpressionSuffixKind.NotRequested, localDescriptor.Suffix.Kind);
        AssertCounts(localOutcome.ParserCounts, 1, 1, 1, 5, 0);
    }

    /// <summary>Proves direct and conditional member suffixes retain the unchanged W2/W6 shapes and fallback.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Member_suffixes_and_literal_fallbacks_are_admitted()
    {
        var directOutcome = FrameValueV1ExpressionParser.Parse("this.Owner");
        var directDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(directOutcome.Descriptor);
        Assert.Equal(FrameValueV1RootKind.This, directDescriptor.RootKind);
        Assert.Equal(DumpExpressionSuffixKind.DirectMember, directDescriptor.Suffix.Kind);
        var directSegment = Assert.Single(directDescriptor.Suffix.Segments);
        Assert.Equal("Owner", directSegment.Identifier.DecodedText);
        Assert.Equal(DumpExpressionSuffixAccessKind.Direct, directSegment.AccessKind);
        AssertCounts(directOutcome.ParserCounts, 3, 3, 2, 5, 0);

        var chainOutcome = FrameValueV1ExpressionParser.Parse(ConditionalChainExpression);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, chainOutcome.Status);
        var chainDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(chainOutcome.Descriptor);
        Assert.Equal(FrameValueV1RootKind.This, chainDescriptor.RootKind);
        Assert.Equal(DumpExpressionSuffixKind.FixedDepthMemberChain, chainDescriptor.Suffix.Kind);
        Assert.Equal(
            new[] { "Owner", "Name" },
            chainDescriptor.Suffix.Segments.Select(static segment => segment.Identifier.DecodedText));
        Assert.All(chainDescriptor.Suffix.Segments, static segment =>
            Assert.Equal(DumpExpressionSuffixAccessKind.Conditional, segment.AccessKind));
        Assert.Equal(DumpExpressionFallbackKind.String, chainDescriptor.Suffix.FallbackKind);
        Assert.Equal("none", chainDescriptor.Suffix.StringFallback);
        AssertCounts(chainOutcome.ParserCounts, 9, 9, 5, 5, 4);
        Assert.Equal(GoldenConditionalChainDescriptorSha256, chainDescriptor.Sha256);

        var identifierChain = FrameValueV1ExpressionParser.Parse("local.Field.Name");
        var identifierChainDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(identifierChain.Descriptor);
        Assert.Equal(FrameValueV1RootKind.Identifier, identifierChainDescriptor.RootKind);
        Assert.Equal(DumpExpressionSuffixKind.FixedDepthMemberChain, identifierChainDescriptor.Suffix.Kind);
        Assert.All(identifierChainDescriptor.Suffix.Segments, static segment =>
            Assert.Equal(DumpExpressionSuffixAccessKind.Direct, segment.AccessKind));
        AssertCounts(identifierChain.ParserCounts, 5, 5, 3, 5, 0);

        var escapedOutcome = FrameValueV1ExpressionParser.Parse("@class?.Payload ?? 17");
        var escapedDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(escapedOutcome.Descriptor);
        Assert.Equal("class", escapedDescriptor.Identifier!.DecodedText);
        Assert.Equal(DumpExpressionFallbackKind.Int32, escapedDescriptor.Suffix.FallbackKind);
        Assert.Equal(17, escapedDescriptor.Suffix.Int32Fallback);
        AssertCounts(escapedOutcome.ParserCounts, 6, 6, 4, 7, 0);
    }

    /// <summary>Proves valid trees outside the frame grammar stop as typed unsupported issues with full counters.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Valid_but_unsupported_frame_shapes_stop_with_typed_issues()
    {
        var rows = new (string Expression, FrameValueV1SyntaxIssue Issue, int[] Counts)[]
        {
            ("this.Owner<int>", FrameValueV1SyntaxIssue.SuffixShapeUnsupported, [5, 6, 4, 5, 0]),
            ("A.B.C.D", FrameValueV1SyntaxIssue.SuffixShapeUnsupported, [7, 7, 4, 1, 0]),
            ("base", FrameValueV1SyntaxIssue.RootShapeUnsupported, [1, 1, 1, 0, 0]),
            ("42", FrameValueV1SyntaxIssue.RootShapeUnsupported, [1, 1, 1, 0, 0]),
            ("this()", FrameValueV1SyntaxIssue.RootShapeUnsupported, [3, 3, 2, 0, 0]),
            ("a?.b[0]", FrameValueV1SyntaxIssue.TreeShapeUnsupported, [8, 7, 5, 1, 0]),
            ("x ?? y", FrameValueV1SyntaxIssue.SuffixShapeUnsupported, [3, 3, 2, 1, 0]),
            ("local ?? 5", FrameValueV1SyntaxIssue.SuffixShapeUnsupported, [3, 3, 2, 5, 0]),
        };
        foreach (var row in rows)
        {
            var outcome = FrameValueV1ExpressionParser.Parse(row.Expression);
            Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, outcome.Status);
            Assert.Equal(row.Issue, outcome.Issue);
            Assert.Equal(row.Expression, outcome.RawExpression);
            Assert.Null(outcome.Descriptor);
            var diagnostic = Assert.Single(outcome.Diagnostics);
            Assert.Equal(DumpExpressionDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(DumpExpressionDiagnosticStage.Projection, diagnostic.Stage);
            AssertCounts(outcome.ParserCounts, row.Counts[0], row.Counts[1], row.Counts[2], row.Counts[3], row.Counts[4]);
            Assert.Empty(outcome.ReachedBounds);
        }
    }

    /// <summary>Proves malformed inputs and pre-projection crossings stop invalid with parse-stage evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Invalid_frame_inputs_stop_with_parse_stage_issues()
    {
        var nullOutcome = FrameValueV1ExpressionParser.Parse(null);
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, nullOutcome.Status);
        Assert.Equal(FrameValueV1SyntaxIssue.ParseError, nullOutcome.Issue);
        Assert.Equal(string.Empty, nullOutcome.RawExpression);
        AssertCounts(nullOutcome.ParserCounts, 0, 0, 0, 0, 0);

        var emptyOutcome = FrameValueV1ExpressionParser.Parse("   ");
        Assert.Equal(FrameValueV1SyntaxIssue.ParseError, emptyOutcome.Issue);

        foreach (var expression in new[] { "(", "#if DEBUG\nx\n#endif" })
        {
            var outcome = FrameValueV1ExpressionParser.Parse(expression);
            Assert.Equal(DumpExpressionSyntaxStatus.Invalid, outcome.Status);
            Assert.Equal(FrameValueV1SyntaxIssue.ParserDiagnostic, outcome.Issue);
            Assert.Null(outcome.Descriptor);
            var diagnostic = Assert.Single(outcome.Diagnostics);
            Assert.Equal(DumpExpressionDiagnosticStage.Parse, diagnostic.Stage);
            Assert.Equal(DumpExpressionDiagnosticSeverity.Error, diagnostic.Severity);
            AssertCounts(outcome.ParserCounts, 0, 0, 0, 0, 0);
            Assert.Empty(outcome.ReachedBounds);
        }

        var overLong = FrameValueV1ExpressionParser.Parse(new string('x', 600));
        Assert.Equal(FrameValueV1SyntaxIssue.ExpressionBoundReached, overLong.Issue);
        Assert.Equal(new string('x', 513), overLong.RawExpression);
        AssertCounts(overLong.ParserCounts, 0, 0, 0, 0, 0);
        AssertReachedBounds(overLong.ReachedBounds, ExpressionV2ContractLimits.ExpressionCharacterCountBoundName);

        var nodeTokenOutcome = FrameValueV1ExpressionParser.Parse(string.Join("+", Enumerable.Repeat("a", 128)));
        Assert.Equal(FrameValueV1SyntaxIssue.NodeTokenBoundReached, nodeTokenOutcome.Issue);
        Assert.Equal(257, nodeTokenOutcome.ParserCounts.NodeCount + nodeTokenOutcome.ParserCounts.TokenCount);
        AssertCounts(nodeTokenOutcome.ParserCounts, 255, 2, 0, 0, 0);
        AssertReachedBounds(
            nodeTokenOutcome.ReachedBounds,
            ExpressionV2ContractLimits.SyntaxNodeTokenCountBoundName);

        var depthOutcome = FrameValueV1ExpressionParser.Parse(new string('(', 70) + "a" + new string(')', 70));
        Assert.Equal(FrameValueV1SyntaxIssue.SyntaxDepthBoundReached, depthOutcome.Issue);
        AssertCounts(depthOutcome.ParserCounts, 71, 141, 65, 0, 0);
        AssertReachedBounds(depthOutcome.ReachedBounds, ExpressionV2ContractLimits.SyntaxDepthBoundName);
    }

    /// <summary>Proves identifier and fallback caps admit exactly at cap and saturate at cap-plus-one.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Frame_identifier_and_fallback_caps_are_exact_at_cap_and_saturated()
    {
        var atCap = FrameValueV1ExpressionParser.Parse(new string('z', 64));
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, atCap.Status);
        var atCapDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(atCap.Descriptor);
        Assert.Equal(64, atCapDescriptor.Identifier!.DecodedText.Length);
        AssertCounts(atCap.ParserCounts, 1, 1, 1, 64, 0);
        AssertReachedBounds(atCap.ReachedBounds, ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);

        var overCap = FrameValueV1ExpressionParser.Parse(new string('z', 65));
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, overCap.Status);
        Assert.Equal(FrameValueV1SyntaxIssue.IdentifierBoundReached, overCap.Issue);
        Assert.Null(overCap.Descriptor);
        AssertCounts(overCap.ParserCounts, 1, 1, 1, 65, 0);
        AssertReachedBounds(overCap.ReachedBounds, ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);

        var fallbackAtCap = FrameValueV1ExpressionParser.Parse($"x.a ?? \"{new string('f', 256)}\"");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, fallbackAtCap.Status);
        var fallbackDescriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(fallbackAtCap.Descriptor);
        Assert.Equal(256, fallbackDescriptor.Suffix.StringFallback!.Length);
        AssertCounts(fallbackAtCap.ParserCounts, 5, 5, 3, 1, 256);
        AssertReachedBounds(
            fallbackAtCap.ReachedBounds,
            ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName);

        var fallbackOverCap = FrameValueV1ExpressionParser.Parse($"x.a ?? \"{new string('f', 257)}\"");
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, fallbackOverCap.Status);
        Assert.Equal(FrameValueV1SyntaxIssue.FallbackStringBoundReached, fallbackOverCap.Issue);
        AssertCounts(fallbackOverCap.ParserCounts, 5, 5, 3, 1, 257);
        AssertReachedBounds(
            fallbackOverCap.ReachedBounds,
            ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName);
    }

    /// <summary>Proves frame replay is deterministic and round-trips the frozen contract factory bit-for-bit.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Frame_projection_replays_with_frozen_canonical_digest()
    {
        var first = FrameValueV1ExpressionParser.Parse(ConditionalChainExpression);
        var replay = FrameValueV1ExpressionParser.Parse(ConditionalChainExpression);
        Assert.Equal(first, replay);
        Assert.Equal(first.Sha256, replay.Sha256);
        var descriptor = Assert.IsType<FrameValueV1ExpressionDescriptor>(first.Descriptor);
        Assert.Equal(GoldenConditionalChainDescriptorSha256, descriptor.Sha256);

        var roundTrip = FrameValueV1ExpressionDescriptor.Create(
            descriptor.RawExpression,
            descriptor.RootKind,
            descriptor.Identifier,
            descriptor.Suffix,
            descriptor.ParserCounts,
            descriptor.ReachedBounds);
        Assert.Equal(descriptor, roundTrip);
        Assert.Equal(GoldenConditionalChainDescriptorSha256, roundTrip.Sha256);
        Assert.Equal(descriptor.CanonicalBytes.ToArray(), roundTrip.CanonicalBytes.ToArray());
    }

    /// <summary>Proves each parser stays inside its own profile instead of falling through to the other.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Profiles_never_fall_through_to_each_other()
    {
        var frameOnStaticShape = FrameValueV1ExpressionParser.Parse("global::A.B");
        var frameOutcome = Assert.IsType<FrameValueV1SyntaxOutcome>(frameOnStaticShape);
        Assert.Equal(DumpExpressionProfileKind.FrameValueExpressionV1, frameOutcome.Profile);
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, frameOutcome.Status);
        Assert.Equal(FrameValueV1SyntaxIssue.RootShapeUnsupported, frameOutcome.Issue);

        var staticOnFrameShape = StaticFieldV2ExpressionParser.Parse("this");
        var staticOutcome = Assert.IsType<StaticFieldV2SyntaxOutcome>(staticOnFrameShape);
        Assert.Equal(DumpExpressionProfileKind.StaticFieldExpressionV2, staticOutcome.Profile);
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, staticOutcome.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.TreeShapeUnsupported, staticOutcome.Issue);
    }

    private static void AssertCounts(
        FrameValueV1ParserCounts counts,
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        int maximumDecodedIdentifierLength,
        int maximumDecodedFallbackStringLength)
    {
        Assert.Equal(nodeCount, counts.NodeCount);
        Assert.Equal(tokenCount, counts.TokenCount);
        Assert.Equal(maximumDepth, counts.MaximumDepth);
        Assert.Equal(maximumDecodedIdentifierLength, counts.MaximumDecodedIdentifierLength);
        Assert.Equal(maximumDecodedFallbackStringLength, counts.MaximumDecodedFallbackStringLength);
    }

    private static void AssertReachedBounds(
        ImmutableArray<EvaluationDeterministicBound> actual,
        params string[] expectedNames)
    {
        Assert.Equal(
            expectedNames.OrderBy(static name => name, StringComparer.Ordinal),
            actual.Select(static bound => bound.Name));
    }
}
