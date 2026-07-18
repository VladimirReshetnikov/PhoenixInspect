using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Interpreter.Product.DumpQuery;

/// <summary>
/// Performs the sole complete Roslyn parse for the draft W7 static-field expression profile and detaches the admitted
/// tree into project-owned immutable syntax contracts.
/// </summary>
/// <remarks>
/// This parser creates no compilation or semantic model. It admits only identifier access trees, an optional literal
/// <c>global::</c> qualifier, the already frozen W2/W6 suffix topologies, and compatible literal coalescing. Every
/// possible static-field split is retained; no textual-prefix or longest-name heuristic selects one during parsing.
/// </remarks>
public static class StaticFieldExpressionParser
{
    /// <summary>Gets the maximum decoded identifier segments retained from one access tree.</summary>
    public const int MaximumSegmentCount = 32;

    /// <summary>Gets the maximum candidate type/field/suffix splits retained from one admitted tree.</summary>
    public const int MaximumCandidateShapeCount = 3;

    /// <summary>Gets the canonical bound name for decoded identifier length.</summary>
    public const string IdentifierCharacterBoundName = "query.identifier.characters";

    /// <summary>Gets the canonical bound name for decoded string-literal length.</summary>
    public const string StringLiteralCharacterBoundName = "query.string-literal.characters";

    /// <summary>Gets the canonical bound name for projected access-tree segments.</summary>
    public const string SegmentCountBoundName = "static-field.syntax.segments";

    /// <summary>Gets the canonical bound name for possible static-field/suffix splits.</summary>
    public const string CandidateShapeCountBoundName = "static-field.syntax.candidate-shapes";

    private const string ExpressionCharacterBoundName = "query.expression.characters";
    private const string SyntaxNodeTokenBoundName = "query.syntax.nodes-and-tokens";
    private const string SyntaxDepthBoundName = "query.syntax.depth";
    /// <summary>Gets the fixed decoded-identifier character bound applied to every parsed identifier token.</summary>
    public static EvaluationDeterministicBound DeclaredIdentifierCharacterBound =>
        new(IdentifierCharacterBoundName, CSharpExpressionFrontEnd.MaximumIdentifierLength);

    /// <summary>Gets the fixed decoded-string character bound applied when the parse contains a string literal.</summary>
    public static EvaluationDeterministicBound DeclaredStringLiteralCharacterBound =>
        new(StringLiteralCharacterBoundName, CSharpExpressionFrontEnd.MaximumStringLiteralLength);

    /// <summary>Gets the fixed projected-segment bound applied after an identifier access tree is recognized.</summary>
    public static EvaluationDeterministicBound DeclaredSegmentCountBound =>
        new(SegmentCountBoundName, MaximumSegmentCount);

    /// <summary>Gets the fixed candidate-split bound applied before constructing an admitted descriptor.</summary>
    public static EvaluationDeterministicBound DeclaredCandidateShapeCountBound =>
        new(CandidateShapeCountBoundName, MaximumCandidateShapeCount);

    /// <summary>Parses and classifies one complete static-field expression exactly once.</summary>
    /// <param name="expression">The complete expression text, including source trivia and escaped spellings.</param>
    /// <returns>
    /// An accepted compiler-detached descriptor, an invalid syntax/integrity outcome, or a valid-but-unsupported tree
    /// outcome. Rejections never expose a partial descriptor.
    /// </returns>
    /// <remarks>
    /// The draft operation is deterministic and bounded. It performs no frame/PDB lookup, metadata traversal, module
    /// lookup, static storage operation, memory read, heap scan, or target-code execution.
    /// </remarks>
    public static StaticFieldSyntaxOutcome Parse(string? expression)
    {
        if (expression is null)
        {
            return InvalidEarly(
                rawExpression: string.Empty,
                StaticFieldSyntaxIssue.ParseError,
                "STATIC_FIELD_EXPRESSION_REQUIRED",
                "A static-field expression is required.",
                ImmutableArray<EvaluationDeterministicBound>.Empty);
        }

        var expressionBound = new EvaluationDeterministicBound(
            ExpressionCharacterBoundName,
            CSharpExpressionFrontEnd.MaximumExpressionLength);
        var earlyBounds = ImmutableArray.Create(expressionBound);
        if (expression.Length > CSharpExpressionFrontEnd.MaximumExpressionLength)
        {
            return InvalidEarly(
                rawExpression: string.Empty,
                StaticFieldSyntaxIssue.SyntaxBoundReached,
                "STATIC_FIELD_EXPRESSION_LIMIT",
                "The static-field expression exceeds the deterministic character limit.",
                earlyBounds);
        }

        if (string.IsNullOrWhiteSpace(expression))
        {
            return InvalidEarly(
                expression,
                StaticFieldSyntaxIssue.ParseError,
                "STATIC_FIELD_EXPRESSION_REQUIRED",
                "A static-field expression is required.",
                earlyBounds);
        }

        // The shared front end performs the sole complete parse. Every disposition below consumes this one tree.
        var syntax = CSharpExpressionFrontEnd.ParseCompleteExpression(expression);
        var nodeCount = syntax.DescendantNodesAndSelf(descendIntoTrivia: false).Count();
        var tokens = syntax.DescendantTokens(descendIntoTrivia: true).ToImmutableArray();
        var tokenCount = tokens.Length;
        var maximumDepth = syntax.DescendantNodesAndSelf(descendIntoTrivia: false)
            .Max(static node => node.Ancestors().Count() + 1);
        var bounds = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>();
        bounds.Add(expressionBound);
        bounds.Add(new EvaluationDeterministicBound(
            SyntaxNodeTokenBoundName,
            CSharpExpressionFrontEnd.MaximumNodeTokenCount));
        bounds.Add(new EvaluationDeterministicBound(
            SyntaxDepthBoundName,
            CSharpExpressionFrontEnd.MaximumSyntaxDepth));

        var emptyProjectionCounts = StaticFieldParserCounts.Create(
            nodeCount,
            tokenCount,
            maximumDepth,
            projectedSegmentCount: 0,
            projectedCandidateShapeCount: 0);
        if ((long)nodeCount + tokenCount > CSharpExpressionFrontEnd.MaximumNodeTokenCount)
        {
            return StaticFieldSyntaxOutcome.Invalid(
                expression,
                StaticFieldSyntaxIssue.SyntaxBoundReached,
                "STATIC_FIELD_SYNTAX_NODE_LIMIT",
                "The complete Roslyn tree exceeds the deterministic node-and-token limit.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        if (maximumDepth > CSharpExpressionFrontEnd.MaximumSyntaxDepth)
        {
            return StaticFieldSyntaxOutcome.Invalid(
                expression,
                StaticFieldSyntaxIssue.SyntaxBoundReached,
                "STATIC_FIELD_SYNTAX_DEPTH_LIMIT",
                "The complete Roslyn tree exceeds the deterministic depth limit.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        if (syntax.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
                trivia.GetStructure() is DirectiveTriviaSyntax ||
                trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            return StaticFieldSyntaxOutcome.Unsupported(
                expression,
                StaticFieldSyntaxIssue.AccessTopologyUnsupported,
                "STATIC_FIELD_DIRECTIVE_UNSUPPORTED",
                "Directives and disabled text are outside the static-field expression profile.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        if (syntax.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            tokens.Any(static token => token.IsMissing) ||
            syntax.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
                trivia.IsKind(SyntaxKind.SkippedTokensTrivia)))
        {
            return StaticFieldSyntaxOutcome.Invalid(
                expression,
                StaticFieldSyntaxIssue.ParserDiagnostic,
                "STATIC_FIELD_CSHARP_SYNTAX_INVALID",
                "The input is not one complete well-formed C# expression.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        if (syntax.FullSpan.Start != 0 ||
            syntax.FullSpan.Length != expression.Length ||
            !string.Equals(syntax.ToFullString(), expression, StringComparison.Ordinal))
        {
            return StaticFieldSyntaxOutcome.Invalid(
                expression,
                StaticFieldSyntaxIssue.ParserIntegrityFailure,
                "STATIC_FIELD_PARSER_INTEGRITY",
                "The complete Roslyn tree does not reproduce the supplied expression text exactly.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        var identifiers = tokens.Where(static token => token.IsKind(SyntaxKind.IdentifierToken)).ToImmutableArray();
        if (!identifiers.IsEmpty)
        {
            bounds.Add(DeclaredIdentifierCharacterBound);
        }
        if (identifiers.Any(static token =>
                token.ValueText.Length > CSharpExpressionFrontEnd.MaximumIdentifierLength))
        {
            return StaticFieldSyntaxOutcome.Invalid(
                expression,
                StaticFieldSyntaxIssue.SyntaxBoundReached,
                "STATIC_FIELD_IDENTIFIER_LIMIT",
                "A decoded identifier exceeds the deterministic character limit.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        var strings = tokens.Where(static token => token.IsKind(SyntaxKind.StringLiteralToken)).ToImmutableArray();
        if (!strings.IsEmpty)
        {
            bounds.Add(DeclaredStringLiteralCharacterBound);
        }
        if (strings.Any(static token => token.Value is string value &&
                value.Length > CSharpExpressionFrontEnd.MaximumStringLiteralLength))
        {
            return StaticFieldSyntaxOutcome.Invalid(
                expression,
                StaticFieldSyntaxIssue.SyntaxBoundReached,
                "STATIC_FIELD_STRING_LITERAL_LIMIT",
                "A decoded string literal exceeds the deterministic character limit.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        var access = syntax;
        ExpressionSyntax? fallback = null;
        if (syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce)
        {
            access = coalesce.Left;
            fallback = coalesce.Right;
        }

        var projectedSegments = ImmutableArray.CreateBuilder<StaticFieldAccessSegment>();
        if (!TryFlattenAccess(access, projectedSegments, out var hasGlobalQualifier))
        {
            return StaticFieldSyntaxOutcome.Unsupported(
                expression,
                StaticFieldSyntaxIssue.TreeShapeUnsupported,
                "STATIC_FIELD_TREE_UNSUPPORTED",
                "The valid C# tree is not an identifier-only static-field access topology.",
                emptyProjectionCounts,
                bounds.ToImmutable());
        }

        bounds.Add(DeclaredSegmentCountBound);
        if (projectedSegments.Count > MaximumSegmentCount)
        {
            var counts = StaticFieldParserCounts.Create(
                nodeCount,
                tokenCount,
                maximumDepth,
                projectedSegments.Count,
                projectedCandidateShapeCount: 0);
            return StaticFieldSyntaxOutcome.Invalid(
                expression,
                StaticFieldSyntaxIssue.SyntaxBoundReached,
                "STATIC_FIELD_SEGMENT_LIMIT",
                "The access tree exceeds the deterministic projected-segment limit.",
                counts,
                bounds.ToImmutable());
        }

        var fallbackKind = StaticFieldFallbackKind.None;
        int? int32Fallback = null;
        string? stringFallback = null;
        if (fallback is not null &&
            !TryProjectFallback(fallback, out fallbackKind, out int32Fallback, out stringFallback))
        {
            var counts = StaticFieldParserCounts.Create(
                nodeCount,
                tokenCount,
                maximumDepth,
                projectedSegments.Count,
                projectedCandidateShapeCount: 0);
            return StaticFieldSyntaxOutcome.Unsupported(
                expression,
                StaticFieldSyntaxIssue.FallbackShapeUnsupported,
                "STATIC_FIELD_FALLBACK_UNSUPPORTED",
                "The coalescing fallback is not an admitted null, Int32, or string literal.",
                counts,
                bounds.ToImmutable());
        }

        bounds.Add(DeclaredCandidateShapeCountBound);
        var shapes = ProjectCandidateShapes(
            projectedSegments,
            fallbackKind,
            int32Fallback,
            stringFallback);
        var parserCounts = StaticFieldParserCounts.Create(
            nodeCount,
            tokenCount,
            maximumDepth,
            projectedSegments.Count,
            shapes.Length);
        if (shapes.IsEmpty)
        {
            return StaticFieldSyntaxOutcome.Unsupported(
                expression,
                projectedSegments.Count < 2
                    ? StaticFieldSyntaxIssue.AccessTopologyUnsupported
                    : StaticFieldSyntaxIssue.SuffixShapeUnsupported,
                "STATIC_FIELD_SUFFIX_UNSUPPORTED",
                "The access tree has no complete admitted type, static-field, and instance-suffix split.",
                parserCounts,
                bounds.ToImmutable());
        }

        var descriptor = StaticFieldExpressionDescriptor.Create(
            expression,
            hasGlobalQualifier,
            projectedSegments.ToImmutable(),
            shapes,
            parserCounts,
            bounds.ToImmutable());
        return StaticFieldSyntaxOutcome.Accepted(descriptor);
    }

    private static StaticFieldSyntaxOutcome InvalidEarly(
        string rawExpression,
        StaticFieldSyntaxIssue issue,
        string code,
        string message,
        ImmutableArray<EvaluationDeterministicBound> bounds) =>
        StaticFieldSyntaxOutcome.Invalid(
            rawExpression,
            issue,
            code,
            message,
            StaticFieldParserCounts.Create(0, 0, 0, 0, 0),
            bounds);

    private static bool TryFlattenAccess(
        ExpressionSyntax syntax,
        ImmutableArray<StaticFieldAccessSegment>.Builder segments,
        out bool hasGlobalQualifier)
    {
        switch (syntax)
        {
            case IdentifierNameSyntax identifier:
                segments.Add(CreateSegment(
                    identifier.Identifier,
                    StaticFieldSegmentSeparatorKind.None,
                    StaticFieldSegmentAccessKind.Root));
                hasGlobalQualifier = false;
                return true;

            case AliasQualifiedNameSyntax
            {
                Alias.Identifier.RawKind: (int)SyntaxKind.GlobalKeyword,
                Name: IdentifierNameSyntax globalName,
            }:
                segments.Add(CreateSegment(
                    globalName.Identifier,
                    StaticFieldSegmentSeparatorKind.GlobalAliasQualifier,
                    StaticFieldSegmentAccessKind.Root));
                hasGlobalQualifier = true;
                return true;

            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: ExpressionSyntax receiver,
                Name: IdentifierNameSyntax member,
            }:
                if (!TryFlattenAccess(receiver, segments, out hasGlobalQualifier))
                {
                    return false;
                }
                segments.Add(CreateSegment(
                    member.Identifier,
                    StaticFieldSegmentSeparatorKind.Dot,
                    StaticFieldSegmentAccessKind.DirectMember));
                return true;

            case ConditionalAccessExpressionSyntax
            {
                Expression: ExpressionSyntax receiver,
                WhenNotNull: MemberBindingExpressionSyntax { Name: IdentifierNameSyntax member },
            }:
                if (!TryFlattenAccess(receiver, segments, out hasGlobalQualifier))
                {
                    return false;
                }
                segments.Add(CreateSegment(
                    member.Identifier,
                    StaticFieldSegmentSeparatorKind.ConditionalDot,
                    StaticFieldSegmentAccessKind.ConditionalMember));
                return true;

            default:
                hasGlobalQualifier = false;
                return false;
        }
    }

    private static StaticFieldAccessSegment CreateSegment(
        SyntaxToken token,
        StaticFieldSegmentSeparatorKind separator,
        StaticFieldSegmentAccessKind access) =>
        StaticFieldAccessSegment.Create(token.Text, token.ValueText, separator, access);

    private static ImmutableArray<StaticFieldCandidateShape> ProjectCandidateShapes(
        ImmutableArray<StaticFieldAccessSegment>.Builder segments,
        StaticFieldFallbackKind fallbackKind,
        int? int32Fallback,
        string? stringFallback)
    {
        var shapes = ImmutableArray.CreateBuilder<StaticFieldCandidateShape>(MaximumCandidateShapeCount);
        var lastIndex = segments.Count - 1;
        if (fallbackKind == StaticFieldFallbackKind.None &&
            lastIndex >= 1 &&
            segments[lastIndex].AccessKind == StaticFieldSegmentAccessKind.DirectMember)
        {
            shapes.Add(StaticFieldCandidateShape.Create(lastIndex, StaticFieldSuffixShape.None));
        }

        if (lastIndex >= 2 &&
            segments[lastIndex - 1].AccessKind == StaticFieldSegmentAccessKind.DirectMember &&
            segments[lastIndex].AccessKind == StaticFieldSegmentAccessKind.DirectMember)
        {
            shapes.Add(StaticFieldCandidateShape.Create(
                lastIndex - 1,
                StaticFieldSuffixShape.DirectMember,
                fallbackKind,
                int32Fallback,
                stringFallback));
        }

        if (lastIndex >= 3 &&
            segments[lastIndex - 2].AccessKind == StaticFieldSegmentAccessKind.DirectMember &&
            segments[lastIndex - 1].AccessKind == StaticFieldSegmentAccessKind.DirectMember &&
            segments[lastIndex].AccessKind is
                StaticFieldSegmentAccessKind.DirectMember or StaticFieldSegmentAccessKind.ConditionalMember)
        {
            shapes.Add(StaticFieldCandidateShape.Create(
                lastIndex - 2,
                StaticFieldSuffixShape.FixedDepthMemberChain,
                fallbackKind,
                int32Fallback,
                stringFallback));
        }

        if (shapes.Count > MaximumCandidateShapeCount)
        {
            throw new InvalidOperationException("The closed candidate-shape projector exceeded its declared bound.");
        }
        return shapes.ToImmutable();
    }

    private static bool TryProjectFallback(
        ExpressionSyntax syntax,
        out StaticFieldFallbackKind kind,
        out int? int32Value,
        out string? stringValue)
    {
        kind = StaticFieldFallbackKind.None;
        int32Value = null;
        stringValue = null;
        if (syntax.IsKind(SyntaxKind.NullLiteralExpression))
        {
            kind = StaticFieldFallbackKind.Null;
            return true;
        }

        if (syntax is LiteralExpressionSyntax stringLiteral &&
            stringLiteral.IsKind(SyntaxKind.StringLiteralExpression) &&
            stringLiteral.Token.Value is string decodedString)
        {
            kind = StaticFieldFallbackKind.String;
            stringValue = decodedString;
            return true;
        }

        if (TryProjectInt32(syntax, out var value))
        {
            kind = StaticFieldFallbackKind.Int32;
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
}
