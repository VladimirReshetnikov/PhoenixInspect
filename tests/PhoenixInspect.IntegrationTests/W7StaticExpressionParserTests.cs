using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the real W7 Roslyn projection with qualified names, all bounded split shapes, suffix/fallback neighbors,
/// escaped spellings, malformed input, valid-but-unadmitted trees, and independent deterministic limit stops.
/// </summary>
[Trait("Category", "Fast")]
[Trait("Corpus", "StaticFieldExpressionV1")]
public sealed class W7StaticExpressionParserTests
{
    /// <summary>Proves one qualified access retains every possible static-field split without longest-prefix selection.</summary>
    [Fact]
    public void Qualified_access_retains_all_splits_and_exact_identifier_spellings()
    {
        const string expression =
            "global::@PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root.Marker";

        var first = StaticFieldExpressionParser.Parse(expression);
        var replay = StaticFieldExpressionParser.Parse(expression);

        Assert.Equal(StaticFieldSyntaxStatus.Accepted, first.Status);
        Assert.Equal(first, replay);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        var descriptor = Assert.IsType<StaticFieldExpressionDescriptor>(first.Descriptor);
        Assert.True(descriptor.HasGlobalQualifier);
        Assert.Equal(expression, descriptor.RawExpression);
        Assert.Equal(
            new[]
            {
                "PhoenixInspect",
                "OptimizedContextTestTarget",
                "StaticContextProbe",
                "Root",
                "Marker",
            },
            descriptor.Segments.Select(static segment => segment.DecodedIdentifier));
        Assert.Equal("@PhoenixInspect", descriptor.Segments[0].RawText);
        Assert.Equal(StaticFieldSegmentSeparatorKind.GlobalAliasQualifier, descriptor.Segments[0].SeparatorKind);

        var shapes = descriptor.CandidateShapes.OrderBy(static shape => shape.StaticFieldSegmentIndex).ToArray();
        Assert.Collection(
            shapes,
            shape =>
            {
                Assert.Equal(2, shape.StaticFieldSegmentIndex);
                Assert.Equal(StaticFieldSuffixShape.FixedDepthMemberChain, shape.SuffixShape);
            },
            shape =>
            {
                Assert.Equal(3, shape.StaticFieldSegmentIndex);
                Assert.Equal(StaticFieldSuffixShape.DirectMember, shape.SuffixShape);
            },
            shape =>
            {
                Assert.Equal(4, shape.StaticFieldSegmentIndex);
                Assert.Equal(StaticFieldSuffixShape.None, shape.SuffixShape);
            });
        Assert.All(shapes, static shape => Assert.Equal(StaticFieldFallbackKind.None, shape.FallbackKind));
        Assert.Equal(5, descriptor.ParserCounts.ProjectedSegmentCount);
        Assert.Equal(3, descriptor.ParserCounts.ProjectedCandidateShapeCount);
        Assert.Contains(StaticFieldExpressionParser.DeclaredIdentifierCharacterBound, descriptor.ReachedBounds);
        Assert.Contains(StaticFieldExpressionParser.DeclaredSegmentCountBound, descriptor.ReachedBounds);
        Assert.Contains(StaticFieldExpressionParser.DeclaredCandidateShapeCountBound, descriptor.ReachedBounds);
        Assert.DoesNotContain(
            StaticFieldExpressionParser.DeclaredStringLiteralCharacterBound,
            descriptor.ReachedBounds);
    }

    /// <summary>Proves the W6 two-hop conditional suffix and decoded string fallback survive Roslyn detachment.</summary>
    [Fact]
    public void Conditional_chain_and_string_fallback_project_one_compatible_shape()
    {
        const string expression = "CoordinatorValues.Root.Owner?.Name ?? \"\\u0041\"";

        var outcome = StaticFieldExpressionParser.Parse(expression);

        var descriptor = Assert.IsType<StaticFieldExpressionDescriptor>(outcome.Descriptor);
        Assert.False(descriptor.HasGlobalQualifier);
        Assert.Equal(
            new[] { "CoordinatorValues", "Root", "Owner", "Name" },
            descriptor.Segments.Select(static segment => segment.DecodedIdentifier));
        Assert.Equal(StaticFieldSegmentAccessKind.ConditionalMember, descriptor.Segments[^1].AccessKind);
        var shape = Assert.Single(descriptor.CandidateShapes);
        Assert.Equal(1, shape.StaticFieldSegmentIndex);
        Assert.Equal(StaticFieldSuffixShape.FixedDepthMemberChain, shape.SuffixShape);
        Assert.Equal(StaticFieldFallbackKind.String, shape.FallbackKind);
        Assert.Equal("A", shape.StringFallback);
        Assert.Contains(
            StaticFieldExpressionParser.DeclaredStringLiteralCharacterBound,
            descriptor.ReachedBounds);
    }

    /// <summary>Proves direct and two-hop alternatives share one exact Int32 fallback, including Int32.MinValue.</summary>
    [Fact]
    public void Coalescing_retains_every_suffix_split_with_one_shared_int32_fallback()
    {
        var outcome = StaticFieldExpressionParser.Parse(
            "Namespace.Type.Root.Marker ?? -2147483648");

        var descriptor = Assert.IsType<StaticFieldExpressionDescriptor>(outcome.Descriptor);
        var shapes = descriptor.CandidateShapes.OrderBy(static shape => shape.StaticFieldSegmentIndex).ToArray();
        Assert.Equal(2, shapes.Length);
        Assert.Equal(
            new[] { StaticFieldSuffixShape.FixedDepthMemberChain, StaticFieldSuffixShape.DirectMember },
            shapes.Select(static shape => shape.SuffixShape));
        Assert.All(shapes, static shape =>
        {
            Assert.Equal(StaticFieldFallbackKind.Int32, shape.FallbackKind);
            Assert.Equal(int.MinValue, shape.Int32Fallback);
        });
    }

    /// <summary>Proves Roslyn-decoded Unicode/verbatim identifiers retain their exact source spellings separately.</summary>
    [Fact]
    public void Escaped_identifiers_keep_raw_and_decoded_values_separate()
    {
        const string expression = "global::@namespace.\\u0054ype.@field";

        var descriptor = Assert.IsType<StaticFieldExpressionDescriptor>(
            StaticFieldExpressionParser.Parse(expression).Descriptor);

        Assert.Equal(
            new[] { "@namespace", "\\u0054ype", "@field" },
            descriptor.Segments.Select(static segment => segment.RawText));
        Assert.Equal(
            new[] { "namespace", "Type", "field" },
            descriptor.Segments.Select(static segment => segment.DecodedIdentifier));
    }

    /// <summary>Proves malformed C# is invalid while valid calls, indexing, generics, and fallback trees are unsupported.</summary>
    [Fact]
    public void Invalid_and_valid_but_unadmitted_neighbors_have_distinct_stable_dispositions()
    {
        var malformed = StaticFieldExpressionParser.Parse("global::Namespace.Type.");
        Assert.Equal(StaticFieldSyntaxStatus.Invalid, malformed.Status);
        Assert.Equal(StaticFieldSyntaxIssue.ParserDiagnostic, malformed.Issue);
        Assert.Null(malformed.Descriptor);

        var unsupported = new[]
        {
            "global::Namespace.Type.Field()",
            "global::Namespace.Type.Field[0]",
            "global::Namespace.Generic<int>.Field",
            "Namespace.Type.Field ?? true",
            "Namespace",
        };
        foreach (var expression in unsupported)
        {
            var outcome = StaticFieldExpressionParser.Parse(expression);
            Assert.Equal(StaticFieldSyntaxStatus.Unsupported, outcome.Status);
            Assert.NotEqual(StaticFieldSyntaxIssue.None, outcome.Issue);
            Assert.Null(outcome.Descriptor);
            Assert.NotNull(outcome.DiagnosticCode);
        }
    }

    /// <summary>Proves independent expression, identifier, string, segment, node/token, and depth limits stop cleanly.</summary>
    [Fact]
    public void Every_projection_limit_stops_without_a_partial_descriptor()
    {
        var overExpression = StaticFieldExpressionParser.Parse(new string('x', 513));
        AssertBoundStop(overExpression, "query.expression.characters");
        Assert.Equal(string.Empty, overExpression.RawExpression);

        var overIdentifier = StaticFieldExpressionParser.Parse($"{new string('i', 65)}.Field");
        AssertBoundStop(overIdentifier, StaticFieldExpressionParser.IdentifierCharacterBoundName);

        var overString = StaticFieldExpressionParser.Parse(
            $"N.T.Root.Marker ?? \"{new string('s', 257)}\"");
        AssertBoundStop(overString, StaticFieldExpressionParser.StringLiteralCharacterBoundName);

        var overSegments = StaticFieldExpressionParser.Parse(
            string.Join('.', Enumerable.Repeat("S", StaticFieldExpressionParser.MaximumSegmentCount + 1)));
        AssertBoundStop(overSegments, StaticFieldExpressionParser.SegmentCountBoundName);

        var overNodes = StaticFieldExpressionParser.Parse(
            string.Join('.', Enumerable.Repeat("Node", 90)));
        AssertBoundStop(overNodes, "query.syntax.nodes-and-tokens");

        var overDepth = StaticFieldExpressionParser.Parse(
            $"{new string('(', 65)}N.T{new string(')', 65)}");
        AssertBoundStop(overDepth, "query.syntax.depth");
    }

    /// <summary>Proves null/empty early rejection and defensive descriptor arrays do not alter canonical replay.</summary>
    [Fact]
    public void Early_rejections_and_returned_arrays_are_strict_and_defensive()
    {
        Assert.Equal(StaticFieldSyntaxIssue.ParseError, StaticFieldExpressionParser.Parse(null).Issue);
        Assert.Equal(StaticFieldSyntaxIssue.ParseError, StaticFieldExpressionParser.Parse("   ").Issue);

        var descriptor = Assert.IsType<StaticFieldExpressionDescriptor>(
            StaticFieldExpressionParser.Parse("N.T.Field").Descriptor);
        var digest = descriptor.Sha256;
        var segments = descriptor.Segments;
        var shapes = descriptor.CandidateShapes;
        segments = segments.SetItem(0, segments[^1]);
        shapes = shapes.SetItem(0, shapes[^1]);

        Assert.Equal(digest, descriptor.Sha256);
        Assert.Equal("N", descriptor.Segments[0].DecodedIdentifier);
        Assert.Equal(2, descriptor.CandidateShapes.Length);
    }

    private static void AssertBoundStop(StaticFieldSyntaxOutcome outcome, string boundName)
    {
        Assert.Equal(StaticFieldSyntaxStatus.Invalid, outcome.Status);
        Assert.Equal(StaticFieldSyntaxIssue.SyntaxBoundReached, outcome.Issue);
        Assert.Null(outcome.Descriptor);
        Assert.Contains(outcome.ReachedBounds, bound => string.Equals(bound.Name, boundName, StringComparison.Ordinal));
    }
}
