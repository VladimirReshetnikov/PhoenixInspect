using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>
/// Projects the sole complete Roslyn parse into the detached draft <c>StaticFieldExpressionV2</c> syntax contracts.
/// </summary>
/// <remarks>
/// This draft projector is a pure function from expression text to one typed syntax outcome. It obtains its tree only
/// through the shared <see cref="CSharpExpressionFrontEnd.ParseCompleteExpression"/> front end, performs no metadata,
/// context, runtime, or memory call, and stops at the first crossed boundary in the declared deterministic order:
/// expression characters, parse, parser diagnostics, parser integrity, node/token count, syntax depth, decoded
/// identifier length, decoded fallback-string length, and then the projection-stage shapes and topology bounds.
/// A crossed boundary saturates its own counter at cap-plus-one and leaves every later counter at zero.
/// </remarks>
public static class StaticFieldV2ExpressionParser
{
    /// <summary>Parses and classifies one complete V2 static-field expression exactly once.</summary>
    /// <param name="expressionText">The complete expression text, including source trivia and escaped spellings.</param>
    /// <remarks>
    /// This draft operation is deterministic and bounded. Valid C# outside the closed V2 grammar becomes a typed
    /// unsupported stop, integrity and bound crossings become typed invalid stops, and no stop retains a prefix
    /// descriptor. Replaying the same text produces a content-equal outcome.
    /// </remarks>
    /// <returns>An admitted detached descriptor outcome or one typed invalid/unsupported syntax stop.</returns>
    public static StaticFieldV2SyntaxOutcome Parse(string? expressionText)
    {
        if (expressionText is null || expressionText.IndexOf('\0') >= 0)
        {
            return Stopped(
                string.Empty,
                StaticFieldV2SyntaxIssue.ParseError,
                "W8_V2_EXPRESSION_REQUIRED",
                ZeroCounts());
        }

        if (expressionText.Length > StaticFieldV2Limits.MaximumExpressionCharacterCount)
        {
            return Stopped(
                expressionText[..(StaticFieldV2Limits.MaximumExpressionCharacterCount + 1)],
                StaticFieldV2SyntaxIssue.ExpressionBoundReached,
                "W8_V2_EXPRESSION_BOUND",
                ZeroCounts());
        }

        if (string.IsNullOrWhiteSpace(expressionText))
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.ParseError,
                "W8_V2_EXPRESSION_REQUIRED",
                ZeroCounts());
        }

        // The shared front end performs the sole complete parse. Every disposition below consumes this one tree.
        var syntax = CSharpExpressionFrontEnd.ParseCompleteExpression(expressionText);
        var tokens = syntax.DescendantTokens(descendIntoTrivia: true).ToImmutableArray();

        if (syntax.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            tokens.Any(static token => token.IsMissing) ||
            syntax.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
                trivia.IsKind(SyntaxKind.SkippedTokensTrivia)))
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.ParserDiagnostic,
                "W8_V2_CSHARP_SYNTAX_INVALID",
                ZeroCounts());
        }

        if (syntax.FullSpan.Start != 0 ||
            syntax.FullSpan.Length != expressionText.Length ||
            !string.Equals(syntax.ToFullString(), expressionText, StringComparison.Ordinal))
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.ParserIntegrityFailure,
                "W8_V2_PARSER_INTEGRITY",
                ZeroCounts());
        }

        var nodeCount = syntax.DescendantNodesAndSelf(descendIntoTrivia: false).Count();
        var tokenCount = tokens.Length;
        if ((long)nodeCount + tokenCount > StaticFieldV2Limits.MaximumSyntaxNodeTokenCount)
        {
            var saturatedNodes = Math.Min(nodeCount, StaticFieldV2Limits.MaximumSyntaxNodeTokenCount + 1);
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.NodeTokenBoundReached,
                "W8_V2_NODE_TOKEN_BOUND",
                Counts(saturatedNodes, StaticFieldV2Limits.MaximumSyntaxNodeTokenCount + 1 - saturatedNodes, 0));
        }

        var maximumDepth = syntax.DescendantNodesAndSelf(descendIntoTrivia: false)
            .Max(static node => node.Ancestors().Count() + 1);
        if (maximumDepth > StaticFieldV2Limits.MaximumSyntaxDepth)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.SyntaxDepthBoundReached,
                "W8_V2_SYNTAX_DEPTH_BOUND",
                Counts(nodeCount, tokenCount, StaticFieldV2Limits.MaximumSyntaxDepth + 1));
        }

        var maximumIdentifierLength = tokens
            .Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(static token => token.ValueText.Length)
            .DefaultIfEmpty(0)
            .Max();
        if (maximumIdentifierLength > StaticFieldV2Limits.MaximumIdentifierCharacterCount)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.IdentifierBoundReached,
                "W8_V2_IDENTIFIER_BOUND",
                Counts(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    identifierLength: StaticFieldV2Limits.MaximumIdentifierCharacterCount + 1));
        }

        var maximumFallbackStringLength = tokens
            .Where(static token => token.Kind() is SyntaxKind.StringLiteralToken or
                SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken)
            .Select(static token => token.Value is string value ? value.Length : 0)
            .DefaultIfEmpty(0)
            .Max();
        if (maximumFallbackStringLength > StaticFieldV2Limits.MaximumFallbackStringCharacterCount)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.FallbackStringBoundReached,
                "W8_V2_FALLBACK_STRING_BOUND",
                Counts(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    identifierLength: maximumIdentifierLength,
                    fallbackStringLength: StaticFieldV2Limits.MaximumFallbackStringCharacterCount + 1));
        }

        var sweepCounts = Counts(
            nodeCount,
            tokenCount,
            maximumDepth,
            identifierLength: maximumIdentifierLength,
            fallbackStringLength: maximumFallbackStringLength);
        if (syntax.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
                trivia.GetStructure() is DirectiveTriviaSyntax ||
                trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.TreeShapeUnsupported,
                "W8_V2_DIRECTIVE_UNSUPPORTED",
                sweepCounts);
        }

        var access = syntax;
        ExpressionSyntax? fallbackSyntax = null;
        if (syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce)
        {
            access = coalesce.Left;
            fallbackSyntax = coalesce.Right;
        }

        var rawSegments = new List<RawExpressionSegment>();
        if (!TryFlattenAccess(access, rawSegments, out var aliasQualifier))
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.TreeShapeUnsupported,
                "W8_V2_TREE_UNSUPPORTED",
                sweepCounts);
        }

        if (rawSegments.Count > StaticFieldV2Limits.MaximumExpressionSegmentCount)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.SegmentBoundReached,
                "W8_V2_SEGMENT_BOUND",
                Counts(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    segmentCount: StaticFieldV2Limits.MaximumExpressionSegmentCount + 1,
                    identifierLength: maximumIdentifierLength,
                    fallbackStringLength: maximumFallbackStringLength));
        }

        var maximumTypeTopologyDepth = 0;
        var cumulativeTypeTopologyNodes = 0;
        var totalTypeArguments = 0;
        var maximumArrayRank = 0;
        foreach (var rawSegment in rawSegments)
        {
            if (rawSegment.TypeArguments is null)
            {
                continue;
            }
            foreach (var argument in rawSegment.TypeArguments.Arguments)
            {
                if (!TryMeasureClosedType(argument, out var metrics))
                {
                    return Stopped(
                        expressionText,
                        StaticFieldV2SyntaxIssue.TypeArgumentShapeUnsupported,
                        "W8_V2_TYPE_ARGUMENT_UNSUPPORTED",
                        Counts(
                            nodeCount,
                            tokenCount,
                            maximumDepth,
                            segmentCount: rawSegments.Count,
                            identifierLength: maximumIdentifierLength,
                            fallbackStringLength: maximumFallbackStringLength));
                }
                maximumTypeTopologyDepth = Math.Max(maximumTypeTopologyDepth, metrics.Depth);
                cumulativeTypeTopologyNodes += metrics.Nodes;
                totalTypeArguments += 1 + metrics.Arguments;
                maximumArrayRank = Math.Max(maximumArrayRank, metrics.MaximumRank);
            }
        }

        if (maximumTypeTopologyDepth > StaticFieldV2Limits.MaximumClosedTypeTopologyDepth)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.TypeTopologyDepthBoundReached,
                "W8_V2_TYPE_TOPOLOGY_DEPTH_BOUND",
                Counts(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    segmentCount: rawSegments.Count,
                    typeTopologyDepth: StaticFieldV2Limits.MaximumClosedTypeTopologyDepth + 1,
                    identifierLength: maximumIdentifierLength,
                    fallbackStringLength: maximumFallbackStringLength));
        }

        if (cumulativeTypeTopologyNodes > StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.TypeTopologyNodeBoundReached,
                "W8_V2_TYPE_TOPOLOGY_NODE_BOUND",
                Counts(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    segmentCount: rawSegments.Count,
                    typeTopologyDepth: maximumTypeTopologyDepth,
                    typeTopologyNodes: StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount + 1,
                    identifierLength: maximumIdentifierLength,
                    fallbackStringLength: maximumFallbackStringLength));
        }

        if (totalTypeArguments > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.TypeArgumentBoundReached,
                "W8_V2_TYPE_ARGUMENT_BOUND",
                Counts(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    segmentCount: rawSegments.Count,
                    typeArgumentCount: StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount + 1,
                    typeTopologyDepth: maximumTypeTopologyDepth,
                    typeTopologyNodes: cumulativeTypeTopologyNodes,
                    identifierLength: maximumIdentifierLength,
                    fallbackStringLength: maximumFallbackStringLength));
        }

        if (maximumArrayRank > StaticFieldV2Limits.MaximumArrayRank)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.ArrayRankBoundReached,
                "W8_V2_ARRAY_RANK_BOUND",
                Counts(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    segmentCount: rawSegments.Count,
                    typeArgumentCount: totalTypeArguments,
                    typeTopologyDepth: maximumTypeTopologyDepth,
                    typeTopologyNodes: cumulativeTypeTopologyNodes,
                    arrayRank: StaticFieldV2Limits.MaximumArrayRank + 1,
                    identifierLength: maximumIdentifierLength,
                    fallbackStringLength: maximumFallbackStringLength));
        }

        var segments = rawSegments
            .Select(static raw => StaticFieldV2ExpressionNameSegment.Create(
                DumpExpressionIdentifier.Create(raw.Identifier.ValueText),
                raw.Separator,
                raw.TypeArguments is null
                    ? ImmutableArray<StaticFieldV2TypeSyntax>.Empty
                    : raw.TypeArguments.Arguments.Select(ProjectClosedType).ToImmutableArray()))
            .ToImmutableArray();
        var projectionCounts = Counts(
            nodeCount,
            tokenCount,
            maximumDepth,
            segmentCount: segments.Length,
            typeArgumentCount: totalTypeArguments,
            typeTopologyDepth: maximumTypeTopologyDepth,
            typeTopologyNodes: cumulativeTypeTopologyNodes,
            arrayRank: maximumArrayRank,
            identifierLength: maximumIdentifierLength,
            fallbackStringLength: maximumFallbackStringLength);

        var fallbackKind = DumpExpressionFallbackKind.None;
        int? int32Fallback = null;
        string? stringFallback = null;
        if (fallbackSyntax is not null &&
            !TryProjectFallback(fallbackSyntax, out fallbackKind, out int32Fallback, out stringFallback))
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.SuffixShapeUnsupported,
                "W8_V2_FALLBACK_UNSUPPORTED",
                projectionCounts);
        }

        var anyPartition = false;
        var anyPartitionIgnoringConditionalPrefix = false;
        for (var fieldIndex = Math.Max(0, segments.Length - 3); fieldIndex < segments.Length; fieldIndex++)
        {
            if (segments[fieldIndex].Arity != 0 ||
                segments.Skip(fieldIndex + 1).Any(static segment => segment.Arity != 0) ||
                (segments.Length - fieldIndex - 1 == 0 && fallbackKind != DumpExpressionFallbackKind.None) ||
                (fieldIndex == 0 && aliasQualifier.Kind != StaticFieldV2AliasKind.None))
            {
                continue;
            }
            anyPartitionIgnoringConditionalPrefix = true;
            if (!segments.Take(fieldIndex + 1)
                    .Any(static segment => segment.Separator == StaticFieldV2ExpressionSeparatorKind.ConditionalDot))
            {
                anyPartition = true;
                break;
            }
        }

        if (!anyPartition)
        {
            return Stopped(
                expressionText,
                anyPartitionIgnoringConditionalPrefix
                    ? StaticFieldV2SyntaxIssue.SeparatorTopologyUnsupported
                    : StaticFieldV2SyntaxIssue.SuffixShapeUnsupported,
                anyPartitionIgnoringConditionalPrefix
                    ? "W8_V2_SEPARATOR_UNSUPPORTED"
                    : "W8_V2_SUFFIX_UNSUPPORTED",
                projectionCounts);
        }

        var partitions = StaticFieldV2ExpressionDescriptor.DerivePartitionsThroughFirstBound(
            aliasQualifier,
            segments,
            fallbackKind,
            int32Fallback,
            stringFallback);
        var completeCounts = Counts(
            nodeCount,
            tokenCount,
            maximumDepth,
            segmentCount: segments.Length,
            typeArgumentCount: totalTypeArguments,
            typeTopologyDepth: maximumTypeTopologyDepth,
            typeTopologyNodes: cumulativeTypeTopologyNodes,
            arrayRank: maximumArrayRank,
            identifierLength: maximumIdentifierLength,
            fallbackStringLength: stringFallback?.Length ?? 0,
            partitionCount: partitions.Length);
        if (partitions.Length > StaticFieldV2Limits.MaximumSyntaxPartitionCount)
        {
            return Stopped(
                expressionText,
                StaticFieldV2SyntaxIssue.PartitionBoundReached,
                "W8_V2_PARTITION_BOUND",
                completeCounts);
        }

        var descriptor = StaticFieldV2ExpressionDescriptor.Create(
            expressionText,
            aliasQualifier,
            segments,
            partitions,
            completeCounts,
            StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(expressionText, completeCounts));
        return StaticFieldV2SyntaxOutcome.Admitted(descriptor, ImmutableArray<DumpExpressionDiagnostic>.Empty);
    }

    private static StaticFieldV2SyntaxOutcome Stopped(
        string rawExpression,
        StaticFieldV2SyntaxIssue issue,
        string code,
        StaticFieldV2ParserCounts counts)
    {
        var invalid = (int)issue < 100;
        var diagnostics = ImmutableArray.Create(DumpExpressionDiagnostic.Create(
            code,
            invalid ? DumpExpressionDiagnosticStage.Parse : DumpExpressionDiagnosticStage.Projection,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty));
        var bounds = StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(rawExpression, counts);
        return invalid
            ? StaticFieldV2SyntaxOutcome.Invalid(rawExpression, issue, counts, diagnostics, bounds)
            : StaticFieldV2SyntaxOutcome.Unsupported(rawExpression, issue, counts, diagnostics, bounds);
    }

    private static StaticFieldV2ParserCounts ZeroCounts() => Counts(0, 0, 0);

    private static StaticFieldV2ParserCounts Counts(
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        int segmentCount = 0,
        int typeArgumentCount = 0,
        int typeTopologyDepth = 0,
        int typeTopologyNodes = 0,
        int arrayRank = 0,
        int identifierLength = 0,
        int fallbackStringLength = 0,
        int partitionCount = 0) =>
        StaticFieldV2ParserCounts.Create(
            nodeCount,
            tokenCount,
            maximumDepth,
            segmentCount,
            typeArgumentCount,
            typeTopologyDepth,
            typeTopologyNodes,
            arrayRank,
            identifierLength,
            fallbackStringLength,
            partitionCount);

    private static bool TryFlattenAccess(
        ExpressionSyntax syntax,
        List<RawExpressionSegment> segments,
        out StaticFieldV2AliasQualifier aliasQualifier)
    {
        switch (syntax)
        {
            case IdentifierNameSyntax or GenericNameSyntax:
                aliasQualifier = StaticFieldV2AliasQualifier.None();
                segments.Add(CreateSegment((SimpleNameSyntax)syntax, StaticFieldV2ExpressionSeparatorKind.None));
                return true;

            case AliasQualifiedNameSyntax alias:
                aliasQualifier = alias.Alias.Identifier.RawKind == (int)SyntaxKind.GlobalKeyword
                    ? StaticFieldV2AliasQualifier.Global()
                    : StaticFieldV2AliasQualifier.Named(
                        DumpExpressionIdentifier.Create(alias.Alias.Identifier.ValueText));
                segments.Add(CreateSegment(alias.Name, StaticFieldV2ExpressionSeparatorKind.None));
                return true;

            case QualifiedNameSyntax qualified:
                if (!TryFlattenAccess(qualified.Left, segments, out aliasQualifier))
                {
                    return false;
                }
                segments.Add(CreateSegment(qualified.Right, StaticFieldV2ExpressionSeparatorKind.Dot));
                return true;

            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: ExpressionSyntax receiver,
                Name: SimpleNameSyntax member,
            }:
                if (!TryFlattenAccess(receiver, segments, out aliasQualifier))
                {
                    return false;
                }
                segments.Add(CreateSegment(member, StaticFieldV2ExpressionSeparatorKind.Dot));
                return true;

            case ConditionalAccessExpressionSyntax conditional:
                if (!TryFlattenAccess(conditional.Expression, segments, out aliasQualifier))
                {
                    return false;
                }
                return TryFlattenConditionalTail(conditional.WhenNotNull, segments);

            default:
                aliasQualifier = StaticFieldV2AliasQualifier.None();
                return false;
        }
    }

    private static bool TryFlattenConditionalTail(ExpressionSyntax syntax, List<RawExpressionSegment> segments)
    {
        switch (syntax)
        {
            case MemberBindingExpressionSyntax { Name: SimpleNameSyntax bound }:
                segments.Add(CreateSegment(bound, StaticFieldV2ExpressionSeparatorKind.ConditionalDot));
                return true;

            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: ExpressionSyntax receiver,
                Name: SimpleNameSyntax member,
            }:
                if (!TryFlattenConditionalTail(receiver, segments))
                {
                    return false;
                }
                segments.Add(CreateSegment(member, StaticFieldV2ExpressionSeparatorKind.Dot));
                return true;

            case ConditionalAccessExpressionSyntax conditional:
                return TryFlattenConditionalTail(conditional.Expression, segments) &&
                    TryFlattenConditionalTail(conditional.WhenNotNull, segments);

            default:
                return false;
        }
    }

    private static RawExpressionSegment CreateSegment(
        SimpleNameSyntax name,
        StaticFieldV2ExpressionSeparatorKind separator) =>
        new(name.Identifier, separator, (name as GenericNameSyntax)?.TypeArgumentList);

    private static bool TryMeasureClosedType(TypeSyntax syntax, out ClosedTypeMetrics metrics)
    {
        metrics = default;
        switch (syntax)
        {
            case PredefinedTypeSyntax predefined:
                if (!TryMapPredefined(predefined.Keyword.Kind(), out _))
                {
                    return false;
                }
                metrics = new ClosedTypeMetrics(1, 1, 0, 0);
                return true;

            case NameSyntax name:
                {
                    var nameSegments = new List<SimpleNameSyntax>();
                    if (!TryCollectTypeNameSegments(name, nameSegments, out _) ||
                        nameSegments.Count > StaticFieldV2Limits.MaximumExpressionSegmentCount)
                    {
                        return false;
                    }
                    var depth = 1;
                    var nodes = 1;
                    var arguments = 0;
                    var maximumRank = 0;
                    foreach (var segment in nameSegments)
                    {
                        if (segment is not GenericNameSyntax generic)
                        {
                            continue;
                        }
                        foreach (var argument in generic.TypeArgumentList.Arguments)
                        {
                            if (!TryMeasureClosedType(argument, out var argumentMetrics))
                            {
                                return false;
                            }
                            depth = Math.Max(depth, argumentMetrics.Depth + 1);
                            nodes += argumentMetrics.Nodes;
                            arguments += 1 + argumentMetrics.Arguments;
                            maximumRank = Math.Max(maximumRank, argumentMetrics.MaximumRank);
                        }
                    }
                    metrics = new ClosedTypeMetrics(depth, nodes, arguments, maximumRank);
                    return true;
                }

            case NullableTypeSyntax nullable:
                if (!TryMeasureClosedType(nullable.ElementType, out var nullableElement))
                {
                    return false;
                }
                metrics = new ClosedTypeMetrics(
                    nullableElement.Depth + 1,
                    nullableElement.Nodes + 1,
                    nullableElement.Arguments,
                    nullableElement.MaximumRank);
                return true;

            case ArrayTypeSyntax array:
                {
                    if (array.RankSpecifiers.Count != 1 ||
                        array.RankSpecifiers[0].Sizes.Any(static size => size is not OmittedArraySizeExpressionSyntax) ||
                        !TryMeasureClosedType(array.ElementType, out var arrayElement))
                    {
                        return false;
                    }
                    var saturatedRank = Math.Min(array.RankSpecifiers[0].Rank, StaticFieldV2Limits.MaximumArrayRank + 1);
                    metrics = new ClosedTypeMetrics(
                        arrayElement.Depth + 1,
                        arrayElement.Nodes + 1,
                        arrayElement.Arguments,
                        Math.Max(saturatedRank, arrayElement.MaximumRank));
                    return true;
                }

            default:
                return false;
        }
    }

    private static bool TryCollectTypeNameSegments(
        NameSyntax syntax,
        List<SimpleNameSyntax> segments,
        out StaticFieldV2AliasQualifier aliasQualifier)
    {
        switch (syntax)
        {
            case IdentifierNameSyntax or GenericNameSyntax:
                aliasQualifier = StaticFieldV2AliasQualifier.None();
                segments.Add((SimpleNameSyntax)syntax);
                return true;

            case AliasQualifiedNameSyntax alias:
                aliasQualifier = alias.Alias.Identifier.RawKind == (int)SyntaxKind.GlobalKeyword
                    ? StaticFieldV2AliasQualifier.Global()
                    : StaticFieldV2AliasQualifier.Named(
                        DumpExpressionIdentifier.Create(alias.Alias.Identifier.ValueText));
                segments.Add(alias.Name);
                return true;

            case QualifiedNameSyntax qualified:
                if (!TryCollectTypeNameSegments(qualified.Left, segments, out aliasQualifier))
                {
                    return false;
                }
                segments.Add(qualified.Right);
                return true;

            default:
                aliasQualifier = StaticFieldV2AliasQualifier.None();
                return false;
        }
    }

    private static StaticFieldV2TypeSyntax ProjectClosedType(TypeSyntax syntax)
    {
        switch (syntax)
        {
            case PredefinedTypeSyntax predefined:
                if (!TryMapPredefined(predefined.Keyword.Kind(), out var predefinedKind))
                {
                    throw new InvalidOperationException("An unmeasured predefined keyword reached projection.");
                }
                return StaticFieldV2TypeSyntax.Predefined(predefinedKind);

            case NameSyntax name:
                {
                    var nameSegments = new List<SimpleNameSyntax>();
                    if (!TryCollectTypeNameSegments(name, nameSegments, out var aliasQualifier))
                    {
                        throw new InvalidOperationException("An unmeasured named type reached projection.");
                    }
                    return StaticFieldV2TypeSyntax.Named(
                        aliasQualifier,
                        nameSegments
                            .Select(static segment => StaticFieldV2TypeNameSegment.Create(
                                DumpExpressionIdentifier.Create(segment.Identifier.ValueText),
                                segment is GenericNameSyntax generic
                                    ? generic.TypeArgumentList.Arguments.Select(ProjectClosedType).ToImmutableArray()
                                    : ImmutableArray<StaticFieldV2TypeSyntax>.Empty))
                            .ToImmutableArray());
                }

            case NullableTypeSyntax nullable:
                return StaticFieldV2TypeSyntax.Nullable(ProjectClosedType(nullable.ElementType));

            case ArrayTypeSyntax array:
                var element = ProjectClosedType(array.ElementType);
                return array.RankSpecifiers[0].Rank == 1
                    ? StaticFieldV2TypeSyntax.SzArray(element)
                    : StaticFieldV2TypeSyntax.MultidimensionalArray(element, array.RankSpecifiers[0].Rank);

            default:
                throw new InvalidOperationException("An unmeasured type tree reached projection.");
        }
    }

    private static bool TryMapPredefined(SyntaxKind keyword, out StaticFieldV2PredefinedTypeKind kind)
    {
        kind = keyword switch
        {
            SyntaxKind.BoolKeyword => StaticFieldV2PredefinedTypeKind.Boolean,
            SyntaxKind.ByteKeyword => StaticFieldV2PredefinedTypeKind.Byte,
            SyntaxKind.SByteKeyword => StaticFieldV2PredefinedTypeKind.SByte,
            SyntaxKind.ShortKeyword => StaticFieldV2PredefinedTypeKind.Int16,
            SyntaxKind.UShortKeyword => StaticFieldV2PredefinedTypeKind.UInt16,
            SyntaxKind.IntKeyword => StaticFieldV2PredefinedTypeKind.Int32,
            SyntaxKind.UIntKeyword => StaticFieldV2PredefinedTypeKind.UInt32,
            SyntaxKind.LongKeyword => StaticFieldV2PredefinedTypeKind.Int64,
            SyntaxKind.ULongKeyword => StaticFieldV2PredefinedTypeKind.UInt64,
            SyntaxKind.CharKeyword => StaticFieldV2PredefinedTypeKind.Char,
            SyntaxKind.FloatKeyword => StaticFieldV2PredefinedTypeKind.Single,
            SyntaxKind.DoubleKeyword => StaticFieldV2PredefinedTypeKind.Double,
            SyntaxKind.DecimalKeyword => StaticFieldV2PredefinedTypeKind.Decimal,
            SyntaxKind.StringKeyword => StaticFieldV2PredefinedTypeKind.String,
            SyntaxKind.ObjectKeyword => StaticFieldV2PredefinedTypeKind.Object,
            _ => default,
        };
        return kind != default;
    }

    private static bool TryProjectFallback(
        ExpressionSyntax syntax,
        out DumpExpressionFallbackKind kind,
        out int? int32Value,
        out string? stringValue)
    {
        kind = DumpExpressionFallbackKind.None;
        int32Value = null;
        stringValue = null;
        if (syntax.IsKind(SyntaxKind.NullLiteralExpression))
        {
            kind = DumpExpressionFallbackKind.Null;
            return true;
        }

        if (syntax is LiteralExpressionSyntax stringLiteral &&
            stringLiteral.IsKind(SyntaxKind.StringLiteralExpression) &&
            stringLiteral.Token.Value is string decodedString)
        {
            kind = DumpExpressionFallbackKind.String;
            stringValue = decodedString;
            return true;
        }

        if (TryProjectInt32(syntax, out var value))
        {
            kind = DumpExpressionFallbackKind.Int32;
            int32Value = value;
            return true;
        }
        return false;
    }

    private static bool TryProjectInt32(ExpressionSyntax syntax, out int value)
    {
        value = 0;
        if (syntax is LiteralExpressionSyntax numeric &&
            numeric.IsKind(SyntaxKind.NumericLiteralExpression) &&
            numeric.Token.Value is int direct)
        {
            value = direct;
            return true;
        }

        if (syntax is not PrefixUnaryExpressionSyntax unary ||
            unary.Operand is not LiteralExpressionSyntax operand ||
            !operand.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            return false;
        }

        if (unary.IsKind(SyntaxKind.UnaryPlusExpression) && operand.Token.Value is int positive)
        {
            value = positive;
            return true;
        }
        if (unary.IsKind(SyntaxKind.UnaryMinusExpression))
        {
            if (operand.Token.Value is int magnitude)
            {
                value = -magnitude;
                return true;
            }
            if (operand.Token.Value is uint unsignedMagnitude && unsignedMagnitude == 2_147_483_648U)
            {
                value = int.MinValue;
                return true;
            }
        }
        return false;
    }

    private sealed record RawExpressionSegment(
        SyntaxToken Identifier,
        StaticFieldV2ExpressionSeparatorKind Separator,
        TypeArgumentListSyntax? TypeArguments);

    private readonly record struct ClosedTypeMetrics(int Depth, int Nodes, int Arguments, int MaximumRank);
}
