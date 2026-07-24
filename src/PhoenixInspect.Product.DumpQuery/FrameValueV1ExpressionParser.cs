using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>
/// Projects the sole complete Roslyn parse into the detached draft <c>FrameValueExpressionV1</c> syntax contracts.
/// </summary>
/// <remarks>
/// This draft projector is a pure function from expression text to one typed frame-value syntax outcome. It obtains
/// its tree only through the shared <see cref="CSharpExpressionFrontEnd.ParseCompleteExpression"/> front end and
/// performs no frame, PDB, metadata, runtime, or memory call. The admitted grammar is exactly a <see langword="this"/>
/// or identifier root, zero through two trailing identifier members over direct or conditional access, and one
/// optional literal coalescing fallback. A crossed boundary saturates its own counter at cap-plus-one, leaves later
/// counters at zero, and never redirects the stopped frame request to the static-field profile.
/// </remarks>
public static class FrameValueV1ExpressionParser
{
    /// <summary>Parses and classifies one complete frame-value expression exactly once.</summary>
    /// <param name="expressionText">The complete expression text, including source trivia and escaped spellings.</param>
    /// <remarks>
    /// This draft operation is deterministic and bounded. Valid C# outside the closed frame-value grammar becomes a
    /// typed unsupported stop, integrity and bound crossings become typed invalid stops, and no stop retains a prefix
    /// descriptor. Replaying the same text produces a content-equal outcome.
    /// </remarks>
    /// <returns>An admitted detached frame descriptor outcome or one typed invalid/unsupported syntax stop.</returns>
    public static FrameValueV1SyntaxOutcome Parse(string? expressionText)
    {
        if (expressionText is null || expressionText.IndexOf('\0') >= 0)
        {
            return Stopped(
                string.Empty,
                FrameValueV1SyntaxIssue.ParseError,
                "W8_FRAME_EXPRESSION_REQUIRED",
                ZeroCounts());
        }

        if (expressionText.Length > FrameValueV1Limits.MaximumExpressionCharacterCount)
        {
            return Stopped(
                expressionText[..(FrameValueV1Limits.MaximumExpressionCharacterCount + 1)],
                FrameValueV1SyntaxIssue.ExpressionBoundReached,
                "W8_FRAME_EXPRESSION_BOUND",
                ZeroCounts());
        }

        if (string.IsNullOrWhiteSpace(expressionText))
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.ParseError,
                "W8_FRAME_EXPRESSION_REQUIRED",
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
                FrameValueV1SyntaxIssue.ParserDiagnostic,
                "W8_FRAME_CSHARP_SYNTAX_INVALID",
                ZeroCounts());
        }

        if (syntax.FullSpan.Start != 0 ||
            syntax.FullSpan.Length != expressionText.Length ||
            !string.Equals(syntax.ToFullString(), expressionText, StringComparison.Ordinal))
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.ParserIntegrityFailure,
                "W8_FRAME_PARSER_INTEGRITY",
                ZeroCounts());
        }

        var nodeCount = syntax.DescendantNodesAndSelf(descendIntoTrivia: false).Count();
        var tokenCount = tokens.Length;
        if ((long)nodeCount + tokenCount > FrameValueV1Limits.MaximumSyntaxNodeTokenCount)
        {
            var saturatedNodes = Math.Min(nodeCount, FrameValueV1Limits.MaximumSyntaxNodeTokenCount + 1);
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.NodeTokenBoundReached,
                "W8_FRAME_NODE_TOKEN_BOUND",
                FrameValueV1ParserCounts.Create(
                    saturatedNodes,
                    FrameValueV1Limits.MaximumSyntaxNodeTokenCount + 1 - saturatedNodes,
                    0,
                    0,
                    0));
        }

        var maximumDepth = syntax.DescendantNodesAndSelf(descendIntoTrivia: false)
            .Max(static node => node.Ancestors().Count() + 1);
        if (maximumDepth > FrameValueV1Limits.MaximumSyntaxDepth)
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.SyntaxDepthBoundReached,
                "W8_FRAME_SYNTAX_DEPTH_BOUND",
                FrameValueV1ParserCounts.Create(
                    nodeCount,
                    tokenCount,
                    FrameValueV1Limits.MaximumSyntaxDepth + 1,
                    0,
                    0));
        }

        var maximumIdentifierLength = tokens
            .Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(static token => token.ValueText.Length)
            .DefaultIfEmpty(0)
            .Max();
        if (maximumIdentifierLength > FrameValueV1Limits.MaximumIdentifierCharacterCount)
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.IdentifierBoundReached,
                "W8_FRAME_IDENTIFIER_BOUND",
                FrameValueV1ParserCounts.Create(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    FrameValueV1Limits.MaximumIdentifierCharacterCount + 1,
                    0));
        }

        var maximumFallbackStringLength = tokens
            .Where(static token => token.Kind() is SyntaxKind.StringLiteralToken or
                SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken)
            .Select(static token => token.Value is string value ? value.Length : 0)
            .DefaultIfEmpty(0)
            .Max();
        if (maximumFallbackStringLength > FrameValueV1Limits.MaximumFallbackStringCharacterCount)
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.FallbackStringBoundReached,
                "W8_FRAME_FALLBACK_STRING_BOUND",
                FrameValueV1ParserCounts.Create(
                    nodeCount,
                    tokenCount,
                    maximumDepth,
                    maximumIdentifierLength,
                    FrameValueV1Limits.MaximumFallbackStringCharacterCount + 1));
        }

        var sweepCounts = FrameValueV1ParserCounts.Create(
            nodeCount,
            tokenCount,
            maximumDepth,
            maximumIdentifierLength,
            maximumFallbackStringLength);
        if (syntax.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
                trivia.GetStructure() is DirectiveTriviaSyntax ||
                trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.TreeShapeUnsupported,
                "W8_FRAME_DIRECTIVE_UNSUPPORTED",
                sweepCounts);
        }

        var access = syntax;
        ExpressionSyntax? fallbackSyntax = null;
        if (syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce)
        {
            access = coalesce.Left;
            fallbackSyntax = coalesce.Right;
        }

        var members = new List<RawFrameMember>();
        if (!TryFlattenFrameAccess(access, members, out var root))
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.TreeShapeUnsupported,
                "W8_FRAME_TREE_UNSUPPORTED",
                sweepCounts);
        }

        FrameValueV1RootKind rootKind;
        DumpExpressionIdentifier? rootIdentifier;
        switch (root)
        {
            case ThisExpressionSyntax:
                rootKind = FrameValueV1RootKind.This;
                rootIdentifier = null;
                break;
            case IdentifierNameSyntax rootName:
                rootKind = FrameValueV1RootKind.Identifier;
                rootIdentifier = DumpExpressionIdentifier.Create(rootName.Identifier.ValueText);
                break;
            default:
                return Stopped(
                    expressionText,
                    FrameValueV1SyntaxIssue.RootShapeUnsupported,
                    "W8_FRAME_ROOT_UNSUPPORTED",
                    sweepCounts);
        }

        if (members.Count > 2 || members.Any(static member => member.Name is not IdentifierNameSyntax))
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.SuffixShapeUnsupported,
                "W8_FRAME_SUFFIX_UNSUPPORTED",
                sweepCounts);
        }

        var fallbackKind = DumpExpressionFallbackKind.None;
        int? int32Fallback = null;
        string? stringFallback = null;
        if (fallbackSyntax is not null &&
            !TryProjectFallback(fallbackSyntax, out fallbackKind, out int32Fallback, out stringFallback))
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.SuffixShapeUnsupported,
                "W8_FRAME_FALLBACK_UNSUPPORTED",
                sweepCounts);
        }

        if (members.Count == 0 && fallbackKind != DumpExpressionFallbackKind.None)
        {
            return Stopped(
                expressionText,
                FrameValueV1SyntaxIssue.SuffixShapeUnsupported,
                "W8_FRAME_SUFFIX_UNSUPPORTED",
                sweepCounts);
        }

        var suffix = DumpExpressionSuffixDescriptor.Create(
            members.Count switch
            {
                0 => DumpExpressionSuffixKind.NotRequested,
                1 => DumpExpressionSuffixKind.DirectMember,
                _ => DumpExpressionSuffixKind.FixedDepthMemberChain,
            },
            members
                .Select(static member => DumpExpressionSuffixSegment.Create(
                    DumpExpressionIdentifier.Create(member.Name.Identifier.ValueText),
                    member.Conditional
                        ? DumpExpressionSuffixAccessKind.Conditional
                        : DumpExpressionSuffixAccessKind.Direct))
                .ToImmutableArray(),
            fallbackKind,
            int32Fallback,
            stringFallback);
        var completeCounts = FrameValueV1ParserCounts.Create(
            nodeCount,
            tokenCount,
            maximumDepth,
            maximumIdentifierLength,
            stringFallback?.Length ?? 0);
        var descriptor = FrameValueV1ExpressionDescriptor.Create(
            expressionText,
            rootKind,
            rootIdentifier,
            suffix,
            completeCounts,
            FrameValueV1ExpressionDescriptor.ExpectedSyntaxReachedBounds(expressionText, completeCounts));
        return FrameValueV1SyntaxOutcome.Admitted(descriptor, ImmutableArray<DumpExpressionDiagnostic>.Empty);
    }

    private static FrameValueV1SyntaxOutcome Stopped(
        string rawExpression,
        FrameValueV1SyntaxIssue issue,
        string code,
        FrameValueV1ParserCounts counts)
    {
        var invalid = (int)issue < 100;
        var diagnostics = ImmutableArray.Create(DumpExpressionDiagnostic.Create(
            code,
            invalid ? DumpExpressionDiagnosticStage.Parse : DumpExpressionDiagnosticStage.Projection,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty));
        var bounds = FrameValueV1ExpressionDescriptor.ExpectedSyntaxReachedBounds(rawExpression, counts);
        return invalid
            ? FrameValueV1SyntaxOutcome.Invalid(rawExpression, issue, counts, diagnostics, bounds)
            : FrameValueV1SyntaxOutcome.Unsupported(rawExpression, issue, counts, diagnostics, bounds);
    }

    private static FrameValueV1ParserCounts ZeroCounts() => FrameValueV1ParserCounts.Create(0, 0, 0, 0, 0);

    private static bool TryFlattenFrameAccess(
        ExpressionSyntax syntax,
        List<RawFrameMember> members,
        out ExpressionSyntax root)
    {
        switch (syntax)
        {
            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: ExpressionSyntax receiver,
                Name: SimpleNameSyntax member,
            }:
                if (!TryFlattenFrameAccess(receiver, members, out root))
                {
                    return false;
                }
                members.Add(new RawFrameMember(member, Conditional: false));
                return true;

            case ConditionalAccessExpressionSyntax conditional:
                if (!TryFlattenFrameAccess(conditional.Expression, members, out root))
                {
                    return false;
                }
                return TryFlattenFrameConditionalTail(conditional.WhenNotNull, members);

            default:
                root = syntax;
                return true;
        }
    }

    private static bool TryFlattenFrameConditionalTail(ExpressionSyntax syntax, List<RawFrameMember> members)
    {
        switch (syntax)
        {
            case MemberBindingExpressionSyntax { Name: SimpleNameSyntax bound }:
                members.Add(new RawFrameMember(bound, Conditional: true));
                return true;

            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: ExpressionSyntax receiver,
                Name: SimpleNameSyntax member,
            }:
                if (!TryFlattenFrameConditionalTail(receiver, members))
                {
                    return false;
                }
                members.Add(new RawFrameMember(member, Conditional: false));
                return true;

            case ConditionalAccessExpressionSyntax conditional:
                return TryFlattenFrameConditionalTail(conditional.Expression, members) &&
                    TryFlattenFrameConditionalTail(conditional.WhenNotNull, members);

            default:
                return false;
        }
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

    private sealed record RawFrameMember(SimpleNameSyntax Name, bool Conditional);
}
