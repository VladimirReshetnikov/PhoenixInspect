using System.Collections.Immutable;
using System.Reflection;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises the W8.3 production projection from the sole Roslyn parse into the V2 syntax contracts.</summary>
public sealed class W8V2SyntaxProjectionTests
{
    private const string GenericSlotExpression =
        "global::Interpreter.W8TestTarget.GenericSlot<global::Interpreter.W8TestTarget.RequestContext>.Current";
    private const string RequestlibExpression =
        "requestlib::A.B<int?>.C<string[]>.D.Field?.Name ?? \"none\"";

    // Golden digests captured from a real StaticFieldV2ExpressionParser.Parse run; never hand-computed.
    private const string GoldenGenericSlotDescriptorSha256 =
        "8688cc24c8ba6c0e8fbc7f99f67d18a034537982fe8d7e32b8f90aff204e0224";
    private const string GoldenRequestlibDescriptorSha256 =
        "6907e432b93499189ffe19ae3b599709f36eaef862ce8b04755c597eb80e7cb7";

    /// <summary>Proves a global-alias generic owner projects segments, one closed type tree, and all partitions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Global_alias_generic_owner_is_admitted_with_complete_partition_universe()
    {
        var outcome = StaticFieldV2ExpressionParser.Parse(GenericSlotExpression);

        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, outcome.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.None, outcome.Issue);
        Assert.Empty(outcome.Diagnostics);
        Assert.Empty(outcome.ReachedBounds);
        var descriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(outcome.Descriptor);
        Assert.Equal(GenericSlotExpression, descriptor.RawExpression);
        Assert.Equal(StaticFieldV2AliasKind.Global, descriptor.AliasQualifier.Kind);
        Assert.Null(descriptor.AliasQualifier.Alias);
        Assert.Equal(
            new[] { "Interpreter", "W8TestTarget", "GenericSlot", "Current" },
            descriptor.Segments.Select(static segment => segment.Identifier.DecodedText));
        Assert.Equal(
            new[]
            {
                StaticFieldV2ExpressionSeparatorKind.None,
                StaticFieldV2ExpressionSeparatorKind.Dot,
                StaticFieldV2ExpressionSeparatorKind.Dot,
                StaticFieldV2ExpressionSeparatorKind.Dot,
            },
            descriptor.Segments.Select(static segment => segment.Separator));

        var argument = Assert.Single(descriptor.Segments[2].TypeArguments);
        Assert.Equal(StaticFieldV2TypeSyntaxKind.Named, argument.Kind);
        Assert.Equal(StaticFieldV2AliasKind.Global, argument.AliasQualifier!.Kind);
        Assert.Equal(
            new[] { "Interpreter", "W8TestTarget", "RequestContext" },
            argument.NameSegments.Select(static segment => segment.Identifier.DecodedText));
        Assert.All(argument.NameSegments, static segment => Assert.Equal(0, segment.Arity));
        Assert.Equal(1, argument.TopologyDepth);
        Assert.Equal(1, argument.TopologyNodeCount);
        Assert.Equal(0, argument.CumulativeArgumentCount);

        Assert.Equal(3, descriptor.Partitions.Length);
        Assert.All(descriptor.Partitions, static partition =>
        {
            Assert.Equal(StaticFieldV2CandidateKind.QualifiedOwner, partition.CandidateKind);
            Assert.Equal(3, partition.FieldSegmentIndex);
            Assert.Equal(DumpExpressionSuffixKind.NotRequested, partition.Suffix.Kind);
            Assert.Equal(DumpExpressionFallbackKind.None, partition.Suffix.FallbackKind);
        });
        Assert.Equal(
            new[] { 0, 1, 2 },
            descriptor.Partitions.Select(static partition => partition.PossibleTopLevelTypeSegmentIndex!.Value).Order());
        AssertCounts(outcome.ParserCounts, 17, 18, 8, 4, 1, 1, 1, 0, 14, 0, 3);
        Assert.Equal(GoldenGenericSlotDescriptorSha256, descriptor.Sha256);
    }

    /// <summary>Proves a named alias, nullable/array arguments, conditional suffix, and string fallback are retained.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Named_alias_conditional_generic_chain_with_string_fallback_is_admitted()
    {
        var outcome = StaticFieldV2ExpressionParser.Parse(RequestlibExpression);

        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, outcome.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.None, outcome.Issue);
        Assert.Empty(outcome.ReachedBounds);
        var descriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(outcome.Descriptor);
        Assert.Equal(StaticFieldV2AliasKind.Named, descriptor.AliasQualifier.Kind);
        Assert.Equal("requestlib", descriptor.AliasQualifier.Alias!.DecodedText);
        Assert.Equal(
            new[] { "A", "B", "C", "D", "Field", "Name" },
            descriptor.Segments.Select(static segment => segment.Identifier.DecodedText));
        Assert.Equal(
            StaticFieldV2ExpressionSeparatorKind.ConditionalDot,
            descriptor.Segments[5].Separator);

        var nullableArgument = Assert.Single(descriptor.Segments[1].TypeArguments);
        Assert.Equal(StaticFieldV2TypeSyntaxKind.Nullable, nullableArgument.Kind);
        Assert.Equal(StaticFieldV2PredefinedTypeKind.Int32, nullableArgument.ElementType!.PredefinedKind);
        var arrayArgument = Assert.Single(descriptor.Segments[2].TypeArguments);
        Assert.Equal(StaticFieldV2TypeSyntaxKind.SzArray, arrayArgument.Kind);
        Assert.Equal(1, arrayArgument.ArrayRank);
        Assert.Equal(StaticFieldV2PredefinedTypeKind.String, arrayArgument.ElementType!.PredefinedKind);

        Assert.Equal(4, descriptor.Partitions.Length);
        Assert.Equal(
            new[] { 3, 3, 4, 4 },
            descriptor.Partitions.Select(static partition => partition.FieldSegmentIndex).Order());
        Assert.Equal(
            new[] { 0, 1 },
            descriptor.Partitions.Select(static partition => partition.PossibleTopLevelTypeSegmentIndex!.Value)
                .Distinct()
                .Order());
        Assert.All(descriptor.Partitions, static partition =>
        {
            Assert.Equal(DumpExpressionFallbackKind.String, partition.Suffix.FallbackKind);
            Assert.Equal("none", partition.Suffix.StringFallback);
        });
        var fieldFourPartition = descriptor.Partitions.First(static partition => partition.FieldSegmentIndex == 4);
        var suffixSegment = Assert.Single(fieldFourPartition.Suffix.Segments);
        Assert.Equal("Name", suffixSegment.Identifier.DecodedText);
        Assert.Equal(DumpExpressionSuffixAccessKind.Conditional, suffixSegment.AccessKind);
        var fieldThreePartition = descriptor.Partitions.First(static partition => partition.FieldSegmentIndex == 3);
        Assert.Equal(DumpExpressionSuffixKind.FixedDepthMemberChain, fieldThreePartition.Suffix.Kind);
        Assert.Equal(
            new[] { DumpExpressionSuffixAccessKind.Direct, DumpExpressionSuffixAccessKind.Conditional },
            fieldThreePartition.Suffix.Segments.Select(static segment => segment.AccessKind));
        AssertCounts(outcome.ParserCounts, 24, 26, 10, 6, 2, 2, 4, 1, 10, 4, 4);
        Assert.Equal(GoldenRequestlibDescriptorSha256, descriptor.Sha256);
    }

    /// <summary>Proves nested generic arguments project the recursive closed topology with exact tree counters.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nested_generic_arguments_project_recursive_closed_topology()
    {
        var outcome = StaticFieldV2ExpressionParser.Parse("Outer<Outer<int,int[,]>,string>.Inner<byte>.Count");

        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, outcome.Status);
        var descriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(outcome.Descriptor);
        Assert.Equal(StaticFieldV2AliasKind.None, descriptor.AliasQualifier.Kind);
        Assert.Equal(2, descriptor.Segments[0].Arity);
        Assert.Equal(1, descriptor.Segments[1].Arity);
        Assert.Equal(0, descriptor.Segments[2].Arity);

        var outerArgument = descriptor.Segments[0].TypeArguments[0];
        Assert.Equal(StaticFieldV2TypeSyntaxKind.Named, outerArgument.Kind);
        var outerSegment = Assert.Single(outerArgument.NameSegments);
        Assert.Equal("Outer", outerSegment.Identifier.DecodedText);
        Assert.Equal(StaticFieldV2PredefinedTypeKind.Int32, outerSegment.TypeArguments[0].PredefinedKind);
        var matrix = outerSegment.TypeArguments[1];
        Assert.Equal(StaticFieldV2TypeSyntaxKind.MultidimensionalArray, matrix.Kind);
        Assert.Equal(2, matrix.ArrayRank);
        Assert.Equal(StaticFieldV2PredefinedTypeKind.Int32, matrix.ElementType!.PredefinedKind);
        Assert.Equal(3, outerArgument.TopologyDepth);
        Assert.Equal(4, outerArgument.TopologyNodeCount);
        Assert.Equal(2, outerArgument.CumulativeArgumentCount);
        Assert.Equal(
            StaticFieldV2PredefinedTypeKind.String,
            descriptor.Segments[0].TypeArguments[1].PredefinedKind);
        Assert.Equal(
            StaticFieldV2PredefinedTypeKind.Byte,
            descriptor.Segments[1].TypeArguments[0].PredefinedKind);

        var partition = Assert.Single(descriptor.Partitions);
        Assert.Equal(2, partition.FieldSegmentIndex);
        Assert.Equal(0, partition.PossibleTopLevelTypeSegmentIndex);
        AssertCounts(outcome.ParserCounts, 17, 23, 9, 3, 5, 3, 6, 2, 5, 0, 1);
        Assert.Empty(outcome.ReachedBounds);
    }

    /// <summary>Proves bare, two-segment, and conditional-suffix expressions admit their exact partition sets.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Bare_and_short_chains_are_admitted_with_exact_partitions()
    {
        var bare = StaticFieldV2ExpressionParser.Parse("Current");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, bare.Status);
        var bareDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(bare.Descriptor);
        var barePartition = Assert.Single(bareDescriptor.Partitions);
        Assert.Equal(StaticFieldV2CandidateKind.BareMember, barePartition.CandidateKind);
        Assert.Equal(0, barePartition.FieldSegmentIndex);
        Assert.Null(barePartition.PossibleTopLevelTypeSegmentIndex);
        Assert.Equal(DumpExpressionSuffixKind.NotRequested, barePartition.Suffix.Kind);
        AssertCounts(bare.ParserCounts, 1, 1, 1, 1, 0, 0, 0, 0, 7, 0, 1);

        var twoSegments = StaticFieldV2ExpressionParser.Parse("A.B");
        var twoDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(twoSegments.Descriptor);
        Assert.Equal(2, twoDescriptor.Partitions.Length);
        var qualified = Assert.Single(
            twoDescriptor.Partitions,
            static partition => partition.CandidateKind == StaticFieldV2CandidateKind.QualifiedOwner);
        Assert.Equal(1, qualified.FieldSegmentIndex);
        Assert.Equal(0, qualified.PossibleTopLevelTypeSegmentIndex);
        var bareSplit = Assert.Single(
            twoDescriptor.Partitions,
            static partition => partition.CandidateKind == StaticFieldV2CandidateKind.BareMember);
        Assert.Equal("B", Assert.Single(bareSplit.Suffix.Segments).Identifier.DecodedText);
        AssertCounts(twoSegments.ParserCounts, 3, 3, 2, 2, 0, 0, 0, 0, 1, 0, 2);

        var conditional = StaticFieldV2ExpressionParser.Parse("A?.B");
        var conditionalDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(conditional.Descriptor);
        var conditionalPartition = Assert.Single(conditionalDescriptor.Partitions);
        Assert.Equal(StaticFieldV2CandidateKind.BareMember, conditionalPartition.CandidateKind);
        Assert.Equal(
            DumpExpressionSuffixAccessKind.Conditional,
            Assert.Single(conditionalPartition.Suffix.Segments).AccessKind);
        AssertCounts(conditional.ParserCounts, 4, 4, 3, 2, 0, 0, 0, 0, 1, 0, 1);
    }

    /// <summary>Proves the predefined sweep covers every source-constructible kind and the enum's exact catalog.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Predefined_keyword_sweep_covers_every_source_constructible_kind()
    {
        var outcome = StaticFieldV2ExpressionParser.Parse(
            "Slot<bool,char,sbyte,byte,short,ushort,int,uint,long,ulong,float,double,decimal,string,object,nint,nuint>.F");

        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, outcome.Status);
        var descriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(outcome.Descriptor);
        var arguments = descriptor.Segments[0].TypeArguments;
        Assert.Equal(17, arguments.Length);
        Assert.Equal(
            new StaticFieldV2PredefinedTypeKind[]
            {
                StaticFieldV2PredefinedTypeKind.Boolean,
                StaticFieldV2PredefinedTypeKind.Char,
                StaticFieldV2PredefinedTypeKind.SByte,
                StaticFieldV2PredefinedTypeKind.Byte,
                StaticFieldV2PredefinedTypeKind.Int16,
                StaticFieldV2PredefinedTypeKind.UInt16,
                StaticFieldV2PredefinedTypeKind.Int32,
                StaticFieldV2PredefinedTypeKind.UInt32,
                StaticFieldV2PredefinedTypeKind.Int64,
                StaticFieldV2PredefinedTypeKind.UInt64,
                StaticFieldV2PredefinedTypeKind.Single,
                StaticFieldV2PredefinedTypeKind.Double,
                StaticFieldV2PredefinedTypeKind.Decimal,
                StaticFieldV2PredefinedTypeKind.String,
                StaticFieldV2PredefinedTypeKind.Object,
            },
            arguments.Take(15).Select(static argument =>
            {
                Assert.Equal(StaticFieldV2TypeSyntaxKind.Predefined, argument.Kind);
                return argument.PredefinedKind!.Value;
            }));

        // Roslyn parses nint/nuint as ordinary identifiers, so NativeInt/NativeUInt stay contract-only kinds here.
        Assert.Equal(
            new[] { "nint", "nuint" },
            arguments.Skip(15).Select(static argument =>
            {
                Assert.Equal(StaticFieldV2TypeSyntaxKind.Named, argument.Kind);
                return Assert.Single(argument.NameSegments).Identifier.DecodedText;
            }));
        Assert.Equal(17, Enum.GetValues<StaticFieldV2PredefinedTypeKind>().Length);
        Assert.Contains(StaticFieldV2PredefinedTypeKind.NativeInt, Enum.GetValues<StaticFieldV2PredefinedTypeKind>());
        Assert.Contains(StaticFieldV2PredefinedTypeKind.NativeUInt, Enum.GetValues<StaticFieldV2PredefinedTypeKind>());
        AssertCounts(outcome.ParserCounts, 21, 38, 4, 2, 17, 1, 17, 0, 5, 0, 1);

        // The dynamic keyword is an ordinary identifier name in the detached V2 grammar.
        var dynamicOutcome = StaticFieldV2ExpressionParser.Parse("Slot<dynamic>.F");
        var dynamicDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(dynamicOutcome.Descriptor);
        var dynamicArgument = Assert.Single(dynamicDescriptor.Segments[0].TypeArguments);
        Assert.Equal(StaticFieldV2TypeSyntaxKind.Named, dynamicArgument.Kind);
        Assert.Equal("dynamic", Assert.Single(dynamicArgument.NameSegments).Identifier.DecodedText);

        // The void keyword cannot form a complete expression type argument and stops as a parser diagnostic.
        var voidOutcome = StaticFieldV2ExpressionParser.Parse("Slot<void>.F");
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, voidOutcome.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.ParserDiagnostic, voidOutcome.Issue);
        AssertCounts(voidOutcome.ParserCounts, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>Proves valid trees outside the closed V2 grammar stop as typed unsupported issues, never invalid.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Valid_but_unsupported_trees_stop_with_typed_projection_issues()
    {
        var rows = new (string Expression, StaticFieldV2SyntaxIssue Issue, int[] Counts)[]
        {
            ("A.B()", StaticFieldV2SyntaxIssue.TreeShapeUnsupported, [5, 5, 3, 0, 0, 0, 0, 0, 1, 0, 0]),
            ("A.B[0]", StaticFieldV2SyntaxIssue.TreeShapeUnsupported, [7, 6, 4, 0, 0, 0, 0, 0, 1, 0, 0]),
            ("x => x", StaticFieldV2SyntaxIssue.TreeShapeUnsupported, [3, 3, 2, 0, 0, 0, 0, 0, 1, 0, 0]),
            ("A + B", StaticFieldV2SyntaxIssue.TreeShapeUnsupported, [3, 3, 2, 0, 0, 0, 0, 0, 1, 0, 0]),
            ("Slot<int*>.F", StaticFieldV2SyntaxIssue.TypeArgumentShapeUnsupported, [6, 7, 5, 2, 0, 0, 0, 0, 4, 0, 0]),
            ("List<>.Current", StaticFieldV2SyntaxIssue.TypeArgumentShapeUnsupported, [5, 6, 4, 2, 0, 0, 0, 0, 7, 0, 0]),
            ("Slot<int[][]>.F", StaticFieldV2SyntaxIssue.TypeArgumentShapeUnsupported, [10, 12, 6, 2, 0, 0, 0, 0, 4, 0, 0]),
            ("Slot<(int,int)>.F", StaticFieldV2SyntaxIssue.TypeArgumentShapeUnsupported, [9, 10, 6, 2, 0, 0, 0, 0, 4, 0, 0]),
            ("A.B<int>", StaticFieldV2SyntaxIssue.SuffixShapeUnsupported, [5, 6, 4, 2, 1, 1, 1, 0, 1, 0, 0]),
            ("global::X", StaticFieldV2SyntaxIssue.SuffixShapeUnsupported, [3, 3, 2, 1, 0, 0, 0, 0, 1, 0, 0]),
            ("A?.B.C.D.E", StaticFieldV2SyntaxIssue.SeparatorTopologyUnsupported, [10, 10, 6, 5, 0, 0, 0, 0, 1, 0, 0]),
        };
        foreach (var row in rows)
        {
            var outcome = StaticFieldV2ExpressionParser.Parse(row.Expression);
            Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, outcome.Status);
            Assert.Equal(row.Issue, outcome.Issue);
            Assert.Equal(row.Expression, outcome.RawExpression);
            Assert.Null(outcome.Descriptor);
            var diagnostic = Assert.Single(outcome.Diagnostics);
            Assert.Equal(DumpExpressionDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(DumpExpressionDiagnosticStage.Projection, diagnostic.Stage);
            AssertCounts(
                outcome.ParserCounts,
                row.Counts[0], row.Counts[1], row.Counts[2], row.Counts[3], row.Counts[4], row.Counts[5],
                row.Counts[6], row.Counts[7], row.Counts[8], row.Counts[9], row.Counts[10]);
            Assert.Empty(outcome.ReachedBounds);
        }
    }

    /// <summary>Proves malformed inputs and pre-projection integrity stops are invalid with parse-stage evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Invalid_inputs_stop_with_parse_stage_issues_and_no_post_stop_counters()
    {
        var nullOutcome = StaticFieldV2ExpressionParser.Parse(null);
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, nullOutcome.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.ParseError, nullOutcome.Issue);
        Assert.Equal(string.Empty, nullOutcome.RawExpression);
        AssertCounts(nullOutcome.ParserCounts, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        foreach (var expression in new[] { string.Empty, "   " })
        {
            var outcome = StaticFieldV2ExpressionParser.Parse(expression);
            Assert.Equal(DumpExpressionSyntaxStatus.Invalid, outcome.Status);
            Assert.Equal(StaticFieldV2SyntaxIssue.ParseError, outcome.Issue);
            Assert.Equal(expression, outcome.RawExpression);
        }

        foreach (var expression in new[] { "A.<", "#if DEBUG\nA.B\n#endif", "A.b::C", "Slot<int[3]>.F" })
        {
            var outcome = StaticFieldV2ExpressionParser.Parse(expression);
            Assert.Equal(DumpExpressionSyntaxStatus.Invalid, outcome.Status);
            Assert.Equal(StaticFieldV2SyntaxIssue.ParserDiagnostic, outcome.Issue);
            Assert.Null(outcome.Descriptor);
            var diagnostic = Assert.Single(outcome.Diagnostics);
            Assert.Equal(DumpExpressionDiagnosticStage.Parse, diagnostic.Stage);
            Assert.Equal(DumpExpressionDiagnosticSeverity.Error, diagnostic.Severity);
            AssertCounts(outcome.ParserCounts, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.Empty(outcome.ReachedBounds);
        }

        var overLong = StaticFieldV2ExpressionParser.Parse(new string('x', 600));
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, overLong.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.ExpressionBoundReached, overLong.Issue);
        Assert.Equal(new string('x', 513), overLong.RawExpression);
        AssertCounts(overLong.ParserCounts, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        AssertReachedBounds(overLong.ReachedBounds, ExpressionV2ContractLimits.ExpressionCharacterCountBoundName);
    }

    /// <summary>Proves identifier and fallback-string caps admit at-cap and saturate exactly at cap-plus-one.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Identifier_and_fallback_string_caps_are_exact_at_cap_and_saturated_over_cap()
    {
        var atCap = StaticFieldV2ExpressionParser.Parse(new string('y', 64));
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, atCap.Status);
        AssertCounts(atCap.ParserCounts, 1, 1, 1, 1, 0, 0, 0, 0, 64, 0, 1);
        AssertReachedBounds(atCap.ReachedBounds, ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);

        var overCap = StaticFieldV2ExpressionParser.Parse(new string('y', 65));
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, overCap.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.IdentifierBoundReached, overCap.Issue);
        Assert.Null(overCap.Descriptor);
        AssertCounts(overCap.ParserCounts, 1, 1, 1, 0, 0, 0, 0, 0, 65, 0, 0);
        AssertReachedBounds(overCap.ReachedBounds, ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);

        var fallbackAtCap = StaticFieldV2ExpressionParser.Parse($"A.B ?? \"{new string('f', 256)}\"");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, fallbackAtCap.Status);
        var fallbackDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(fallbackAtCap.Descriptor);
        var fallbackPartition = Assert.Single(fallbackDescriptor.Partitions);
        Assert.Equal(StaticFieldV2CandidateKind.BareMember, fallbackPartition.CandidateKind);
        Assert.Equal(256, fallbackPartition.Suffix.StringFallback!.Length);
        AssertCounts(fallbackAtCap.ParserCounts, 5, 5, 3, 2, 0, 0, 0, 0, 1, 256, 1);
        AssertReachedBounds(
            fallbackAtCap.ReachedBounds,
            ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName);

        var fallbackOverCap = StaticFieldV2ExpressionParser.Parse($"A.B ?? \"{new string('f', 257)}\"");
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, fallbackOverCap.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.FallbackStringBoundReached, fallbackOverCap.Issue);
        AssertCounts(fallbackOverCap.ParserCounts, 5, 5, 3, 0, 0, 0, 0, 0, 1, 257, 0);
        AssertReachedBounds(
            fallbackOverCap.ReachedBounds,
            ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName);
    }

    /// <summary>Proves node/token and depth crossings saturate their own counter and zero every later counter.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Node_token_and_syntax_depth_crossings_saturate_at_cap_plus_one()
    {
        var nodeTokenOutcome = StaticFieldV2ExpressionParser.Parse(
            string.Join("+", Enumerable.Repeat("a", 128)));
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, nodeTokenOutcome.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.NodeTokenBoundReached, nodeTokenOutcome.Issue);
        Assert.Equal(
            257,
            nodeTokenOutcome.ParserCounts.NodeCount + nodeTokenOutcome.ParserCounts.TokenCount);
        AssertCounts(nodeTokenOutcome.ParserCounts, 255, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        AssertReachedBounds(
            nodeTokenOutcome.ReachedBounds,
            ExpressionV2ContractLimits.SyntaxNodeTokenCountBoundName);

        var depthOutcome = StaticFieldV2ExpressionParser.Parse(
            new string('(', 70) + "a" + new string(')', 70));
        Assert.Equal(DumpExpressionSyntaxStatus.Invalid, depthOutcome.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.SyntaxDepthBoundReached, depthOutcome.Issue);
        AssertCounts(depthOutcome.ParserCounts, 71, 141, 65, 0, 0, 0, 0, 0, 0, 0, 0);
        AssertReachedBounds(depthOutcome.ReachedBounds, ExpressionV2ContractLimits.SyntaxDepthBoundName);
    }

    /// <summary>Proves segment and partition caps come from the real projection and derivation counts.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Segment_and_partition_caps_follow_the_production_derivation()
    {
        var segmentsOverCap = StaticFieldV2ExpressionParser.Parse(SimpleChain(33));
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, segmentsOverCap.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.SegmentBoundReached, segmentsOverCap.Issue);
        AssertCounts(segmentsOverCap.ParserCounts, 65, 65, 33, 33, 0, 0, 0, 0, 3, 0, 0);
        AssertReachedBounds(
            segmentsOverCap.ReachedBounds,
            ExpressionV2ContractLimits.ExpressionSegmentCountBoundName);

        // A generic head keeps the 32-segment chain under the partition cap, so the segment cap admits exactly.
        var segmentsAtCap = StaticFieldV2ExpressionParser.Parse(
            "A<int>." + string.Join(".", Enumerable.Range(1, 31).Select(static index => $"N{index}")));
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, segmentsAtCap.Status);
        var segmentsAtCapDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(segmentsAtCap.Descriptor);
        Assert.Equal(32, segmentsAtCapDescriptor.Segments.Length);
        Assert.Equal(3, segmentsAtCapDescriptor.Partitions.Length);
        Assert.All(
            segmentsAtCapDescriptor.Partitions,
            static partition => Assert.Equal(0, partition.PossibleTopLevelTypeSegmentIndex));
        Assert.Equal(
            new[] { 29, 30, 31 },
            segmentsAtCapDescriptor.Partitions.Select(static partition => partition.FieldSegmentIndex).Order());
        AssertCounts(segmentsAtCap.ParserCounts, 65, 66, 34, 32, 1, 1, 1, 0, 3, 0, 3);
        AssertReachedBounds(
            segmentsAtCap.ReachedBounds,
            ExpressionV2ContractLimits.ExpressionSegmentCountBoundName);

        var partitionsAtCap = StaticFieldV2ExpressionParser.Parse(SimpleChain(23));
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, partitionsAtCap.Status);
        var partitionsAtCapDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(partitionsAtCap.Descriptor);
        Assert.Equal(StaticFieldV2Limits.MaximumSyntaxPartitionCount, partitionsAtCapDescriptor.Partitions.Length);
        AssertCounts(partitionsAtCap.ParserCounts, 45, 45, 23, 23, 0, 0, 0, 0, 3, 0, 63);
        AssertReachedBounds(
            partitionsAtCap.ReachedBounds,
            ExpressionV2ContractLimits.SyntaxPartitionCountBoundName);

        var partitionsOverCap = StaticFieldV2ExpressionParser.Parse(SimpleChain(24));
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, partitionsOverCap.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.PartitionBoundReached, partitionsOverCap.Issue);
        Assert.Null(partitionsOverCap.Descriptor);
        AssertCounts(partitionsOverCap.ParserCounts, 47, 47, 24, 24, 0, 0, 0, 0, 3, 0, 64);
        AssertReachedBounds(
            partitionsOverCap.ReachedBounds,
            ExpressionV2ContractLimits.SyntaxPartitionCountBoundName);
    }

    /// <summary>Proves topology depth, cumulative argument, and array-rank caps admit at-cap and stop over cap.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Type_topology_argument_and_array_rank_caps_are_exact_at_cap_and_saturated()
    {
        var depthAtCap = StaticFieldV2ExpressionParser.Parse($"X<{NestedGeneric(15)}>.F");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, depthAtCap.Status);
        var depthDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(depthAtCap.Descriptor);
        Assert.Equal(16, Assert.Single(depthDescriptor.Segments[0].TypeArguments).TopologyDepth);
        AssertCounts(depthAtCap.ParserCounts, 35, 51, 34, 2, 16, 16, 16, 0, 1, 0, 1);
        AssertReachedBounds(
            depthAtCap.ReachedBounds,
            ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName);

        var depthOverCap = StaticFieldV2ExpressionParser.Parse($"X<{NestedGeneric(16)}>.F");
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, depthOverCap.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.TypeTopologyDepthBoundReached, depthOverCap.Issue);
        AssertCounts(depthOverCap.ParserCounts, 37, 54, 36, 2, 0, 17, 0, 0, 1, 0, 0);
        AssertReachedBounds(
            depthOverCap.ReachedBounds,
            ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName);

        var argumentsAtCap = StaticFieldV2ExpressionParser.Parse(ArgumentSweep(64));
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, argumentsAtCap.Status);
        AssertCounts(argumentsAtCap.ParserCounts, 68, 132, 4, 2, 64, 1, 64, 0, 3, 0, 1);
        AssertReachedBounds(
            argumentsAtCap.ReachedBounds,
            ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName);

        var argumentsOverCap = StaticFieldV2ExpressionParser.Parse(ArgumentSweep(65));
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, argumentsOverCap.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.TypeArgumentBoundReached, argumentsOverCap.Issue);
        AssertCounts(argumentsOverCap.ParserCounts, 69, 134, 4, 2, 65, 1, 65, 0, 3, 0, 0);
        AssertReachedBounds(
            argumentsOverCap.ReachedBounds,
            ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName);

        var rankAtCap = StaticFieldV2ExpressionParser.Parse($"S<a[{new string(',', 31)}]>.F");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, rankAtCap.Status);
        var rankDescriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(rankAtCap.Descriptor);
        Assert.Equal(32, Assert.Single(rankDescriptor.Segments[0].TypeArguments).ArrayRank);
        AssertCounts(rankAtCap.ParserCounts, 39, 71, 6, 2, 1, 2, 2, 32, 1, 0, 1);
        AssertReachedBounds(rankAtCap.ReachedBounds, ExpressionV2ContractLimits.ArrayRankBoundName);

        var rankOverCap = StaticFieldV2ExpressionParser.Parse($"S<a[{new string(',', 32)}]>.F");
        Assert.Equal(DumpExpressionSyntaxStatus.Unsupported, rankOverCap.Status);
        Assert.Equal(StaticFieldV2SyntaxIssue.ArrayRankBoundReached, rankOverCap.Issue);
        AssertCounts(rankOverCap.ParserCounts, 40, 73, 6, 2, 1, 2, 2, 33, 1, 0, 0);
        AssertReachedBounds(rankOverCap.ReachedBounds, ExpressionV2ContractLimits.ArrayRankBoundName);
    }

    /// <summary>Proves an expression at the exact character cap admits with its reached bound recorded.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Expression_at_exact_character_cap_is_admitted_with_reached_bound()
    {
        var expression = string.Join(".", Enumerable.Repeat(new string('x', 64), 7)) + "." + new string('z', 57);
        Assert.Equal(512, expression.Length);

        var outcome = StaticFieldV2ExpressionParser.Parse(expression);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, outcome.Status);
        var descriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(outcome.Descriptor);
        Assert.Equal(8, descriptor.Segments.Length);
        Assert.Equal(18, descriptor.Partitions.Length);
        AssertCounts(outcome.ParserCounts, 15, 15, 8, 8, 0, 0, 0, 0, 64, 0, 18);
        AssertReachedBounds(
            outcome.ReachedBounds,
            ExpressionV2ContractLimits.ExpressionCharacterCountBoundName,
            ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);
    }

    /// <summary>Proves replayed parses and descriptor round-trips reproduce the frozen canonical digests.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Admitted_projection_replays_with_frozen_canonical_digests()
    {
        foreach (var (expression, golden) in new[]
                 {
                     (GenericSlotExpression, GoldenGenericSlotDescriptorSha256),
                     (RequestlibExpression, GoldenRequestlibDescriptorSha256),
                 })
        {
            var first = StaticFieldV2ExpressionParser.Parse(expression);
            var replay = StaticFieldV2ExpressionParser.Parse(expression);
            Assert.Equal(first, replay);
            Assert.Equal(first.Sha256, replay.Sha256);
            var descriptor = Assert.IsType<StaticFieldV2ExpressionDescriptor>(first.Descriptor);
            Assert.Equal(golden, descriptor.Sha256);
            Assert.Equal(golden, replay.Descriptor!.Sha256);

            // The parser output must round-trip through the frozen contract factory with identical canonical bytes.
            var roundTrip = StaticFieldV2ExpressionDescriptor.Create(
                descriptor.RawExpression,
                descriptor.AliasQualifier,
                descriptor.Segments,
                descriptor.Partitions,
                descriptor.ParserCounts,
                descriptor.ReachedBounds);
            Assert.Equal(descriptor, roundTrip);
            Assert.Equal(golden, roundTrip.Sha256);
            Assert.Equal(descriptor.CanonicalBytes.ToArray(), roundTrip.CanonicalBytes.ToArray());
        }
    }

    /// <summary>Proves both projector types are pure static surfaces whose only public operation is Parse.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Projectors_are_stateless_static_surfaces_with_only_a_parse_method()
    {
        foreach (var type in new[] { typeof(StaticFieldV2ExpressionParser), typeof(FrameValueV1ExpressionParser) })
        {
            Assert.True(type.IsAbstract && type.IsSealed);
            Assert.Empty(type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            Assert.Empty(type.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var parse = Assert.Single(
                type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                static method => !method.IsSpecialName);
            Assert.Equal("Parse", parse.Name);
            var parameter = Assert.Single(parse.GetParameters());
            Assert.Equal(typeof(string), parameter.ParameterType);
        }

        // Malformed input replays identically, which is the observable no-side-effect evidence for a pure parse.
        Assert.Equal(
            StaticFieldV2ExpressionParser.Parse("A.<").Sha256,
            StaticFieldV2ExpressionParser.Parse("A.<").Sha256);
        Assert.Equal(
            FrameValueV1ExpressionParser.Parse("(").Sha256,
            FrameValueV1ExpressionParser.Parse("(").Sha256);
    }

    /// <summary>Proves both public projector types and their Parse members carry draft XML documentation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Projector_types_carry_draft_xml_documentation()
    {
        var assembly = typeof(StaticFieldV2ExpressionParser).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        foreach (var type in new[] { typeof(StaticFieldV2ExpressionParser), typeof(FrameValueV1ExpressionParser) })
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

    private static string SimpleChain(int count) =>
        string.Join(".", Enumerable.Range(0, count).Select(static index => $"N{index}"));

    private static string NestedGeneric(int levels) =>
        string.Concat(Enumerable.Repeat("G<", levels)) + "int" + new string('>', levels);

    private static string ArgumentSweep(int count) =>
        "S<" + string.Join(",", Enumerable.Range(1, count).Select(static index => $"a{index}")) + ">.F";

    private static void AssertCounts(
        StaticFieldV2ParserCounts counts,
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        int projectedSegmentCount,
        int projectedTypeArgumentCount,
        int maximumTypeTopologyDepth,
        int cumulativeTypeTopologyNodeCount,
        int maximumArrayRank,
        int maximumDecodedIdentifierLength,
        int maximumDecodedFallbackStringLength,
        int completePartitionCount)
    {
        Assert.Equal(nodeCount, counts.NodeCount);
        Assert.Equal(tokenCount, counts.TokenCount);
        Assert.Equal(maximumDepth, counts.MaximumDepth);
        Assert.Equal(projectedSegmentCount, counts.ProjectedSegmentCount);
        Assert.Equal(projectedTypeArgumentCount, counts.ProjectedTypeArgumentCount);
        Assert.Equal(maximumTypeTopologyDepth, counts.MaximumTypeTopologyDepth);
        Assert.Equal(cumulativeTypeTopologyNodeCount, counts.CumulativeTypeTopologyNodeCount);
        Assert.Equal(maximumArrayRank, counts.MaximumArrayRank);
        Assert.Equal(maximumDecodedIdentifierLength, counts.MaximumDecodedIdentifierLength);
        Assert.Equal(maximumDecodedFallbackStringLength, counts.MaximumDecodedFallbackStringLength);
        Assert.Equal(completePartitionCount, counts.CompletePartitionCount);
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
