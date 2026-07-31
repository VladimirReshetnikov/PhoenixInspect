using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

internal enum DumpQueryLiteralKind
{
    Null,
    Int32,
    String,
}

internal sealed record DumpQueryLiteral(DumpQueryLiteralKind Kind, int Int32Value, string? StringValue);

internal sealed record ParsedDumpQuery(
    string RootName,
    string FieldName,
    DumpQueryLiteral? CoalesceLiteral);

internal enum ParsedExpressionOperationKind
{
    DirectMember,
    EmptyInstanceInvocation,
    DirectMemberChain,
    ConditionalMemberChain,
    MemberChainPath,
}

/// <summary>One admitted member hop of an arbitrary-depth chain.</summary>
/// <param name="Name">The decoded member identifier.</param>
/// <param name="IsConditionalAccess">Whether the separator before this member is <c>?.</c> rather than <c>.</c>.</param>
internal sealed record ParsedChainHop(string Name, bool IsConditionalAccess);

internal sealed record ParsedExpressionDescriptor(
    ParsedExpressionOperationKind Operation,
    string RootName,
    string FirstMemberName,
    string? SecondMemberName,
    DumpQueryLiteral? CoalesceLiteral)
{
    /// <summary>Gets every admitted hop in chain order; empty for legacy single- and two-member operations.</summary>
    internal System.Collections.Immutable.ImmutableArray<ParsedChainHop> Hops { get; init; } =
        System.Collections.Immutable.ImmutableArray<ParsedChainHop>.Empty;

    /// <summary>Gets the exact source spelling of the coalescing literal, when one is present on a chain.</summary>
    internal string? CoalesceLiteralText { get; init; }

    internal ParsedDumpQuery ToDumpQuery()
    {
        if (Operation != ParsedExpressionOperationKind.DirectMember)
        {
            throw new InvalidOperationException("Only an admitted direct member can become a W2 query.");
        }

        return new ParsedDumpQuery(RootName, FirstMemberName, CoalesceLiteral);
    }
}

/// <summary>Identifies the deterministic parser bounds reached while parsing one expression.</summary>
[Flags]
internal enum DumpQueryParserBounds
{
    None = 0,
    ExpressionLength = 1 << 0,
    RootNameLength = 1 << 1,
    FieldNameLength = 1 << 2,
    StringLiteralLength = 1 << 3,
    SyntaxNodeTokenCount = 1 << 4,
    SyntaxDepth = 1 << 5,
}

internal sealed record DumpQueryParseResult(
    ParsedDumpQuery? Query,
    string? DiagnosticCode,
    string? DiagnosticMessage,
    DumpQueryParserBounds AppliedBounds)
{
    internal bool IsSuccess => Query is not null;
}

internal enum CSharpExpressionAdmissionProfile
{
    FrozenW2,
    FrozenW5,
    FixedDepthMemberChainV1,
    MemberChainV2,
}

internal enum CSharpExpressionAdmissionStatus
{
    Accepted,
    Invalid,
    Unsupported,
}

internal sealed record CSharpExpressionAdmissionResult(
    CSharpExpressionAdmissionStatus Status,
    ParsedExpressionDescriptor? Expression,
    string? DiagnosticCode,
    string? DiagnosticMessage,
    DumpQueryParserBounds AppliedBounds)
{
    internal bool IsAccepted => Status == CSharpExpressionAdmissionStatus.Accepted;
}

internal static class DumpQueryParser
{
    internal const int MaximumExpressionLength = CSharpExpressionFrontEnd.MaximumExpressionLength;
    internal const int MaximumIdentifierLength = CSharpExpressionFrontEnd.MaximumIdentifierLength;
    internal const int MaximumStringLiteralLength = CSharpExpressionFrontEnd.MaximumStringLiteralLength;

    internal static DumpQueryParseResult Parse(string? expression, string? expectedRootName)
    {
        var admission = CSharpExpressionFrontEnd.Classify(
            expression,
            expectedRootName,
            CSharpExpressionAdmissionProfile.FrozenW2);
        return admission is { IsAccepted: true, Expression: { } descriptor }
            ? new DumpQueryParseResult(
                descriptor.ToDumpQuery(),
                DiagnosticCode: null,
                DiagnosticMessage: null,
                admission.AppliedBounds)
            : new DumpQueryParseResult(
                Query: null,
                admission.DiagnosticCode,
                admission.DiagnosticMessage,
                admission.AppliedBounds);
    }
}

internal static class CSharpExpressionFrontEnd
{
    internal const string ProfileId = "RoslynCSharpExpressionV1";
    internal const string PackageId = "Microsoft.CodeAnalysis.CSharp";
    internal const string PackageVersion = "5.3.0";
    internal const string LanguageVersionName = "CSharp14";
    internal const int MaximumExpressionLength = 512;
    internal const int MaximumNodeTokenCount = 256;
    internal const int MaximumSyntaxDepth = 64;
    internal const int MaximumIdentifierLength = 64;
    internal const int MaximumStringLiteralLength = 256;

    private const string UnsupportedCode = "QUERY_SYNTAX_UNSUPPORTED";
    private const string UnsupportedMessage =
        "The expression contains syntax outside the supported dump-query grammar.";
    private const string InvalidSyntaxCode = "QUERY_CSHARP_SYNTAX_INVALID";
    private const string InvalidSyntaxMessage =
        "The expression is not one complete well-formed C# expression.";

    private static readonly CSharpParseOptions ParseOptions = new(
        languageVersion: LanguageVersion.CSharp14,
        documentationMode: DocumentationMode.None,
        kind: SourceCodeKind.Regular,
        preprocessorSymbols: Array.Empty<string>());

    internal static ExpressionSyntax ParseCompleteExpression(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return SyntaxFactory.ParseExpression(
            text,
            offset: 0,
            options: ParseOptions,
            consumeFullText: true);
    }

    internal static CSharpExpressionAdmissionResult Classify(
        string? text,
        string? expectedRootName,
        CSharpExpressionAdmissionProfile profile)
    {
        if (text is null)
        {
            return Invalid(
                "QUERY_EXPRESSION_REQUIRED",
                "A dump-query expression is required.",
                DumpQueryParserBounds.None);
        }

        var bounds = DumpQueryParserBounds.ExpressionLength;
        if (text.Length > MaximumExpressionLength)
        {
            return Invalid(
                "QUERY_EXPRESSION_TOO_LONG",
                "The dump-query expression exceeds the deterministic length limit.",
                bounds);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid(
                "QUERY_EXPRESSION_REQUIRED",
                "A dump-query expression is required.",
                bounds);
        }

        bounds |= DumpQueryParserBounds.RootNameLength;
        var legacyProfile = profile is not (
            CSharpExpressionAdmissionProfile.FixedDepthMemberChainV1 or
            CSharpExpressionAdmissionProfile.MemberChainV2);
        if (!IsValidRootName(expectedRootName, legacyProfile, out var rootTooLong))
        {
            return Invalid(
                rootTooLong ? "QUERY_ROOT_NAME_TOO_LONG" : "QUERY_ROOT_NAME_INVALID",
                rootTooLong
                    ? "The configured root name exceeds the deterministic identifier limit."
                    : "The configured root name is not a supported identifier.",
                bounds);
        }

        var syntax = ParseCompleteExpression(text);
        AddLegacyShapeBounds(syntax, text, expectedRootName!, ref bounds);

        if (syntax.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
                trivia.GetStructure() is DirectiveTriviaSyntax || trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            return Unsupported(
                "QUERY_CSHARP_DIRECTIVE_UNSUPPORTED",
                "C# directives and disabled text are outside the expression profile.",
                bounds);
        }

        if (syntax.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            syntax.DescendantTokens(descendIntoTrivia: true).Any(static token => token.IsMissing) ||
            syntax.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
                trivia.IsKind(SyntaxKind.SkippedTokensTrivia)))
        {
            return NormalizeInvalidSyntax(text, bounds, legacyProfile);
        }

        if (syntax.FullSpan.Start != 0 || syntax.FullSpan.Length != text.Length ||
            !string.Equals(syntax.ToFullString(), text, StringComparison.Ordinal))
        {
            return Invalid(InvalidSyntaxCode, InvalidSyntaxMessage, bounds);
        }

        var nodesAndTokens = syntax.DescendantNodesAndTokensAndSelf(descendIntoTrivia: false).Count();
        if (nodesAndTokens > MaximumNodeTokenCount)
        {
            return Invalid(
                "QUERY_SYNTAX_NODE_LIMIT_EXCEEDED",
                "The parsed C# expression exceeds the deterministic node-and-token limit.",
                bounds | DumpQueryParserBounds.SyntaxNodeTokenCount);
        }

        var depth = GetMaximumDepth(syntax);
        if (depth > MaximumSyntaxDepth)
        {
            return Invalid(
                "QUERY_SYNTAX_DEPTH_LIMIT_EXCEEDED",
                "The parsed C# expression exceeds the deterministic syntax-depth limit.",
                bounds | DumpQueryParserBounds.SyntaxNodeTokenCount | DumpQueryParserBounds.SyntaxDepth);
        }

        if (syntax.DescendantTokens(descendIntoTrivia: false)
            .Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
            .Any(static token => token.ValueText.Length > MaximumIdentifierLength))
        {
            return Invalid(
                "QUERY_IDENTIFIER_TOO_LONG",
                "An expression identifier exceeds the deterministic identifier limit.",
                bounds);
        }

        if (syntax.DescendantTokens(descendIntoTrivia: false)
            .Where(static token => token.IsKind(SyntaxKind.StringLiteralToken))
            .Any(static token => token.Value is string value && value.Length > MaximumStringLiteralLength))
        {
            return Invalid(
                "QUERY_STRING_LITERAL_TOO_LONG",
                "The string literal exceeds the deterministic decoded-length limit.",
                bounds | DumpQueryParserBounds.StringLiteralLength);
        }

        if (profile is CSharpExpressionAdmissionProfile.FixedDepthMemberChainV1 or
            CSharpExpressionAdmissionProfile.MemberChainV2)
        {
            bounds |= DumpQueryParserBounds.SyntaxNodeTokenCount | DumpQueryParserBounds.SyntaxDepth;
        }

        if (TryGetLeadingRoot(syntax, out var observedRoot) &&
            !string.Equals(observedRoot.Identifier.ValueText, expectedRootName, StringComparison.Ordinal))
        {
            return Invalid(
                "QUERY_ROOT_MISMATCH",
                "The expression does not reference the configured root name exactly.",
                bounds);
        }

        var w2 = TryRecognizeDirectMember(syntax, expectedRootName!, legacyProfile, bounds);
        if (w2 is not null)
        {
            return w2;
        }

        if (profile != CSharpExpressionAdmissionProfile.FrozenW2)
        {
            var w5 = TryRecognizeEmptyInvocation(syntax, text, expectedRootName!, bounds);
            if (w5 is not null)
            {
                return w5;
            }
        }

        if (profile == CSharpExpressionAdmissionProfile.FixedDepthMemberChainV1)
        {
            var w6 = TryRecognizeMemberChain(syntax, expectedRootName!, bounds);
            if (w6 is not null)
            {
                return w6;
            }
        }

        if (profile == CSharpExpressionAdmissionProfile.MemberChainV2)
        {
            var chainPath = TryRecognizeMemberChainPath(syntax, expectedRootName!, bounds);
            if (chainPath is not null)
            {
                return chainPath;
            }
        }

        if (syntax is IdentifierNameSyntax identifier &&
            string.Equals(identifier.Identifier.ValueText, expectedRootName, StringComparison.Ordinal))
        {
            return Unsupported(
                "QUERY_MEMBER_ACCESS_REQUIRED",
                "The supported grammar requires one instance-field access.",
                bounds);
        }

        if (legacyProfile && syntax.DescendantNodesAndSelf().OfType<ElementAccessExpressionSyntax>().Any(element =>
                element.Expression is IdentifierNameSyntax elementRoot &&
                string.Equals(elementRoot.Identifier.ValueText, expectedRootName, StringComparison.Ordinal)))
        {
            return Unsupported(
                "QUERY_MEMBER_ACCESS_REQUIRED",
                "The supported grammar requires one instance-field access.",
                bounds);
        }

        return Unsupported(UnsupportedCode, UnsupportedMessage, bounds);
    }

    private static CSharpExpressionAdmissionResult? TryRecognizeDirectMember(
        ExpressionSyntax syntax,
        string expectedRootName,
        bool legacyProfile,
        DumpQueryParserBounds bounds)
    {
        var (left, right) = SplitCoalesce(syntax);
        if (left is not MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: IdentifierNameSyntax root,
                Name: IdentifierNameSyntax member,
            })
        {
            return null;
        }

        if (!string.Equals(root.Identifier.ValueText, expectedRootName, StringComparison.Ordinal))
        {
            return Invalid(
                "QUERY_ROOT_MISMATCH",
                "The expression does not reference the configured root name exactly.",
                bounds);
        }

        if (legacyProfile &&
            (!IsLegacyIdentifier(root.Identifier) ||
             !IsLegacyIdentifier(member.Identifier) ||
             !HasLegacyTrivia(syntax)))
        {
            return Unsupported(UnsupportedCode, UnsupportedMessage, bounds);
        }

        DumpQueryLiteral? literal = null;
        if (right is not null &&
            !TryProjectLiteral(right, legacyProfile, out literal, out var code, out var message))
        {
            return IsInvalidLiteralCode(code)
                ? Invalid(code!, message!, bounds)
                : Unsupported(code!, message!, bounds);
        }

        return Accepted(
            new ParsedExpressionDescriptor(
                ParsedExpressionOperationKind.DirectMember,
                root.Identifier.ValueText,
                member.Identifier.ValueText,
                SecondMemberName: null,
                literal),
            bounds);
    }

    private static CSharpExpressionAdmissionResult? TryRecognizeEmptyInvocation(
        ExpressionSyntax syntax,
        string rawText,
        string expectedRootName,
        DumpQueryParserBounds bounds)
    {
        if (syntax is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Expression: IdentifierNameSyntax root,
                    Name: IdentifierNameSyntax method,
                },
                ArgumentList.Arguments.Count: 0,
            } ||
            !string.Equals(root.Identifier.ValueText, expectedRootName, StringComparison.Ordinal) ||
            !string.Equals(method.Identifier.ValueText, "GetMarkerSummary", StringComparison.Ordinal) ||
            !string.Equals(rawText, $"{expectedRootName}.GetMarkerSummary()", StringComparison.Ordinal))
        {
            return null;
        }

        return Accepted(
            new ParsedExpressionDescriptor(
                ParsedExpressionOperationKind.EmptyInstanceInvocation,
                root.Identifier.ValueText,
                method.Identifier.ValueText,
                SecondMemberName: null,
                CoalesceLiteral: null),
            bounds);
    }

    private static CSharpExpressionAdmissionResult? TryRecognizeMemberChain(
        ExpressionSyntax syntax,
        string expectedRootName,
        DumpQueryParserBounds bounds)
    {
        var (left, right) = SplitCoalesce(syntax);
        ParsedExpressionOperationKind operation;
        IdentifierNameSyntax root;
        IdentifierNameSyntax referenceMember;
        IdentifierNameSyntax terminalMember;

        if (left is MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: MemberAccessExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Expression: IdentifierNameSyntax directRoot,
                    Name: IdentifierNameSyntax directReference,
                },
                Name: IdentifierNameSyntax directTerminal,
            })
        {
            operation = ParsedExpressionOperationKind.DirectMemberChain;
            root = directRoot;
            referenceMember = directReference;
            terminalMember = directTerminal;
        }
        else if (left is ConditionalAccessExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: IdentifierNameSyntax conditionalRoot,
                Name: IdentifierNameSyntax conditionalReference,
            },
            WhenNotNull: MemberBindingExpressionSyntax
            {
                Name: IdentifierNameSyntax conditionalTerminal,
            },
        })
        {
            operation = ParsedExpressionOperationKind.ConditionalMemberChain;
            root = conditionalRoot;
            referenceMember = conditionalReference;
            terminalMember = conditionalTerminal;
        }
        else
        {
            return null;
        }

        if (!string.Equals(root.Identifier.ValueText, expectedRootName, StringComparison.Ordinal))
        {
            return Invalid(
                "QUERY_ROOT_MISMATCH",
                "The expression does not reference the configured root name exactly.",
                bounds);
        }

        DumpQueryLiteral? literal = null;
        if (right is not null &&
            !TryProjectLiteral(right, legacyProfile: false, out literal, out var code, out var message))
        {
            return IsInvalidLiteralCode(code)
                ? Invalid(code!, message!, bounds)
                : Unsupported(code!, message!, bounds);
        }

        return Accepted(
            new ParsedExpressionDescriptor(
                operation,
                root.Identifier.ValueText,
                referenceMember.Identifier.ValueText,
                terminalMember.Identifier.ValueText,
                literal),
            bounds);
    }

    private static CSharpExpressionAdmissionResult? TryRecognizeMemberChainPath(
        ExpressionSyntax syntax,
        string expectedRootName,
        DumpQueryParserBounds bounds)
    {
        var (left, right) = SplitCoalesce(syntax);
        var hops = new List<ParsedChainHop>();
        if (!TryFlattenChain(left, hops, out var root) || root is null || hops.Count < 2)
        {
            return null;
        }

        // The first separator must be ordinary member access: the exact root object is host-selected and never
        // null, so a conditional first hop would claim a guard the evaluation cannot honestly exercise.
        if (hops[0].IsConditionalAccess)
        {
            return Unsupported(
                "QUERY_CHAIN_ROOT_CONDITIONAL_UNSUPPORTED",
                "Conditional access on the host-selected root is outside the member-chain grammar.",
                bounds);
        }

        if (!string.Equals(root.Identifier.ValueText, expectedRootName, StringComparison.Ordinal))
        {
            return Invalid(
                "QUERY_ROOT_MISMATCH",
                "The expression does not reference the configured root name exactly.",
                bounds);
        }

        DumpQueryLiteral? literal = null;
        if (right is not null &&
            !TryProjectLiteral(right, legacyProfile: false, out literal, out var code, out var message))
        {
            return IsInvalidLiteralCode(code)
                ? Invalid(code!, message!, bounds)
                : Unsupported(code!, message!, bounds);
        }

        var hopArray = hops.ToImmutableArray();
        if (hops.Count == 2)
        {
            // A two-member chain keeps its frozen V1 operation kind so it routes through the unchanged
            // fixed-depth pipeline and reproduces its exact behavior and identities.
            return Accepted(
                new ParsedExpressionDescriptor(
                    hops[1].IsConditionalAccess
                        ? ParsedExpressionOperationKind.ConditionalMemberChain
                        : ParsedExpressionOperationKind.DirectMemberChain,
                    root.Identifier.ValueText,
                    hops[0].Name,
                    hops[1].Name,
                    literal)
                {
                    Hops = hopArray,
                    CoalesceLiteralText = right?.ToString(),
                },
                bounds);
        }

        return Accepted(
            new ParsedExpressionDescriptor(
                ParsedExpressionOperationKind.MemberChainPath,
                root.Identifier.ValueText,
                hops[0].Name,
                hops[^1].Name,
                literal)
            {
                Hops = hopArray,
                CoalesceLiteralText = right?.ToString(),
            },
            bounds);
    }

    private static bool TryFlattenChain(
        ExpressionSyntax node,
        List<ParsedChainHop> hops,
        out IdentifierNameSyntax? root)
    {
        switch (node)
        {
            case IdentifierNameSyntax identifier:
                root = identifier;
                return true;
            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: { } inner,
                Name: IdentifierNameSyntax member,
            }:
                if (!TryFlattenChain(inner, hops, out root))
                {
                    return false;
                }

                hops.Add(new ParsedChainHop(member.Identifier.ValueText, IsConditionalAccess: false));
                return true;
            case ConditionalAccessExpressionSyntax conditional:
                if (!TryFlattenChain(conditional.Expression, hops, out root))
                {
                    return false;
                }

                return TryFlattenConditionalTail(conditional.WhenNotNull, hops);
            default:
                root = null;
                return false;
        }
    }

    private static bool TryFlattenConditionalTail(ExpressionSyntax node, List<ParsedChainHop> hops)
    {
        switch (node)
        {
            case MemberBindingExpressionSyntax { Name: IdentifierNameSyntax bound }:
                hops.Add(new ParsedChainHop(bound.Identifier.ValueText, IsConditionalAccess: true));
                return true;
            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: { } inner,
                Name: IdentifierNameSyntax member,
            }:
                if (!TryFlattenConditionalTail(inner, hops))
                {
                    return false;
                }

                hops.Add(new ParsedChainHop(member.Identifier.ValueText, IsConditionalAccess: false));
                return true;
            case ConditionalAccessExpressionSyntax conditional:
                return TryFlattenConditionalTail(conditional.Expression, hops) &&
                    TryFlattenConditionalTail(conditional.WhenNotNull, hops);
            default:
                return false;
        }
    }

    private static (ExpressionSyntax Left, ExpressionSyntax? Right) SplitCoalesce(ExpressionSyntax syntax) =>
        syntax is BinaryExpressionSyntax
        {
            RawKind: (int)SyntaxKind.CoalesceExpression,
        } coalesce
            ? (coalesce.Left, coalesce.Right)
            : (syntax, null);

    private static bool TryProjectLiteral(
        ExpressionSyntax syntax,
        bool legacyProfile,
        out DumpQueryLiteral? literal,
        out string? code,
        out string? message)
    {
        literal = null;
        code = null;
        message = null;
        if (syntax.IsKind(SyntaxKind.NullLiteralExpression))
        {
            literal = new DumpQueryLiteral(DumpQueryLiteralKind.Null, 0, null);
            return true;
        }

        if (syntax is LiteralExpressionSyntax stringSyntax &&
            stringSyntax.IsKind(SyntaxKind.StringLiteralExpression) &&
            stringSyntax.Token.Value is string value)
        {
            if (legacyProfile && !IsLegacyStringSpelling(stringSyntax.Token.Text))
            {
                code = "QUERY_STRING_ESCAPE_UNSUPPORTED";
                message = "The string literal contains an unsupported escape sequence.";
                return false;
            }

            literal = new DumpQueryLiteral(DumpQueryLiteralKind.String, 0, value);
            return true;
        }

        if (TryProjectInt32(syntax, legacyProfile, out var int32, out var numericOverflow))
        {
            literal = new DumpQueryLiteral(DumpQueryLiteralKind.Int32, int32, null);
            return true;
        }

        if (numericOverflow)
        {
            code = "QUERY_INT32_LITERAL_INVALID";
            message = "The integer literal is outside the supported Int32 range.";
            return false;
        }

        code = "QUERY_LITERAL_UNSUPPORTED";
        message = "The expression uses a literal outside the supported null, Int32, and string set.";
        return false;
    }

    private static bool TryProjectInt32(
        ExpressionSyntax syntax,
        bool legacyProfile,
        out int value,
        out bool numericOverflow)
    {
        value = 0;
        numericOverflow = false;
        var raw = syntax.ToString();
        if (legacyProfile)
        {
            var unsigned = raw.AsSpan();
            if (!unsigned.IsEmpty && unsigned[0] is '+' or '-')
            {
                unsigned = unsigned[1..];
            }

            if (unsigned.IsEmpty || !ContainsOnlyDecimalDigits(unsigned))
            {
                return false;
            }

            if (int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            numericOverflow = true;
            return false;
        }

        if (syntax is LiteralExpressionSyntax numeric &&
            numeric.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            if (numeric.Token.Value is int direct)
            {
                value = direct;
                return true;
            }

            numericOverflow = numeric.Token.Value is uint or long or ulong;
            return false;
        }

        if (syntax is PrefixUnaryExpressionSyntax unary &&
            unary.Operand is LiteralExpressionSyntax operand &&
            operand.IsKind(SyntaxKind.NumericLiteralExpression))
        {
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

                if (operand.Token.Value is uint unsignedMagnitude && unsignedMagnitude == 2147483648U)
                {
                    value = int.MinValue;
                    return true;
                }
            }

            numericOverflow = operand.Token.Value is uint or long or ulong;
        }

        return false;
    }

    private static CSharpExpressionAdmissionResult NormalizeInvalidSyntax(
        string text,
        DumpQueryParserBounds bounds,
        bool legacyProfile)
    {
        if (legacyProfile && text.Contains("??", StringComparison.Ordinal))
        {
            var tail = text[(text.IndexOf("??", StringComparison.Ordinal) + 2)..].TrimStart();
            if (tail.Length == 0)
            {
                return Invalid(
                    "QUERY_LITERAL_REQUIRED",
                    "Null coalescing requires one supported literal.",
                    bounds);
            }

            if (tail.StartsWith('"'))
            {
                bounds |= DumpQueryParserBounds.StringLiteralLength;
                if (ContainsUnsupportedLegacyEscape(tail))
                {
                    return Invalid(
                        "QUERY_STRING_ESCAPE_UNSUPPORTED",
                        "The string literal contains an unsupported escape sequence.",
                        bounds);
                }

                return Invalid(
                    "QUERY_STRING_LITERAL_INVALID",
                    "The string literal is not a valid terminated ordinary string literal.",
                    bounds);
            }
        }

        return Invalid(InvalidSyntaxCode, InvalidSyntaxMessage, bounds);
    }

    private static bool IsValidRootName(string? value, bool legacyProfile, out bool tooLong)
    {
        tooLong = value?.Length > MaximumIdentifierLength;
        if (string.IsNullOrEmpty(value) || tooLong)
        {
            return false;
        }

        if (legacyProfile)
        {
            return IsAsciiIdentifier(value);
        }

        if (!SyntaxFacts.IsIdentifierStartCharacter(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!SyntaxFacts.IsIdentifierPartCharacter(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLegacyIdentifier(SyntaxToken token) =>
        string.Equals(token.Text, token.ValueText, StringComparison.Ordinal) && IsAsciiIdentifier(token.Text);

    private static bool IsAsciiIdentifier(string value)
    {
        if (value.Length == 0 || !IsAsciiIdentifierStart(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsAsciiIdentifierStart(value[index]) && value[index] is not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiIdentifierStart(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';

    private static bool HasLegacyTrivia(SyntaxNode syntax) =>
        syntax.DescendantTrivia(descendIntoTrivia: false).All(static trivia =>
            trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia));

    private static bool IsLegacyStringSpelling(string text)
    {
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return false;
        }

        return !ContainsUnsupportedLegacyEscape(text);
    }

    private static bool ContainsUnsupportedLegacyEscape(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\')
            {
                continue;
            }

            index++;
            if (index >= text.Length || text[index] is not ('"' or '\\' or 'n' or 'r' or 't' or '0'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsOnlyDecimalDigits(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            if (character is not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetLeadingRoot(ExpressionSyntax syntax, out IdentifierNameSyntax root)
    {
        switch (syntax)
        {
            case IdentifierNameSyntax identifier:
                root = identifier;
                return true;
            case MemberAccessExpressionSyntax member:
                return TryGetLeadingRoot(member.Expression, out root);
            case ConditionalAccessExpressionSyntax conditional:
                return TryGetLeadingRoot(conditional.Expression, out root);
            case InvocationExpressionSyntax invocation:
                return TryGetLeadingRoot(invocation.Expression, out root);
            case ElementAccessExpressionSyntax element:
                return TryGetLeadingRoot(element.Expression, out root);
            case BinaryExpressionSyntax binary:
                return TryGetLeadingRoot(binary.Left, out root);
            case PrefixUnaryExpressionSyntax unary:
                return TryGetLeadingRoot(unary.Operand, out root);
            case PostfixUnaryExpressionSyntax postfix:
                return TryGetLeadingRoot(postfix.Operand, out root);
            case ParenthesizedExpressionSyntax parenthesized:
                return TryGetLeadingRoot(parenthesized.Expression, out root);
            default:
                root = null!;
                return false;
        }
    }

    private static void AddLegacyShapeBounds(
        SyntaxNode syntax,
        string text,
        string expectedRootName,
        ref DumpQueryParserBounds bounds)
    {
        if (TryGetLeadingMember(syntax, out var member) &&
            TryGetLeadingRoot(member, out var root) &&
            string.Equals(root.Identifier.ValueText, expectedRootName, StringComparison.Ordinal))
        {
            bounds |= DumpQueryParserBounds.FieldNameLength;
        }

        var coalesceIndex = text.IndexOf("??", StringComparison.Ordinal);
        if (coalesceIndex >= 0 && text[(coalesceIndex + 2)..].TrimStart().StartsWith('"'))
        {
            bounds |= DumpQueryParserBounds.StringLiteralLength;
        }
    }

    private static bool TryGetLeadingMember(SyntaxNode syntax, out MemberAccessExpressionSyntax member)
    {
        switch (syntax)
        {
            case MemberAccessExpressionSyntax candidate:
                if (candidate.Expression is IdentifierNameSyntax)
                {
                    member = candidate;
                    return true;
                }

                return TryGetLeadingMember(candidate.Expression, out member);
            case InvocationExpressionSyntax invocation:
                return TryGetLeadingMember(invocation.Expression, out member);
            case ConditionalAccessExpressionSyntax conditional:
                return TryGetLeadingMember(conditional.Expression, out member);
            case BinaryExpressionSyntax binary:
                return TryGetLeadingMember(binary.Left, out member);
            default:
                member = null!;
                return false;
        }
    }

    private static int GetMaximumDepth(SyntaxNode root)
    {
        var maximum = 1;
        var pending = new Stack<(SyntaxNode Node, int Depth)>();
        pending.Push((root, 1));
        while (pending.TryPop(out var item))
        {
            maximum = Math.Max(maximum, item.Depth);
            foreach (var child in item.Node.ChildNodes())
            {
                pending.Push((child, item.Depth + 1));
            }
        }

        return maximum;
    }

    private static bool IsInvalidLiteralCode(string? code) => code is
        "QUERY_INT32_LITERAL_INVALID" or
        "QUERY_STRING_LITERAL_TOO_LONG";

    private static CSharpExpressionAdmissionResult Accepted(
        ParsedExpressionDescriptor expression,
        DumpQueryParserBounds bounds) => new(
        CSharpExpressionAdmissionStatus.Accepted,
        expression,
        DiagnosticCode: null,
        DiagnosticMessage: null,
        bounds);

    private static CSharpExpressionAdmissionResult Invalid(
        string code,
        string message,
        DumpQueryParserBounds bounds) => new(
        CSharpExpressionAdmissionStatus.Invalid,
        Expression: null,
        code,
        message,
        bounds);

    private static CSharpExpressionAdmissionResult Unsupported(
        string code,
        string message,
        DumpQueryParserBounds bounds) => new(
        CSharpExpressionAdmissionStatus.Unsupported,
        Expression: null,
        code,
        message,
        bounds);
}
