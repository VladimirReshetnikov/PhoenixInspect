using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The widened syntax surface: interpolated strings, <c>is</c> patterns, <c>switch</c> expressions,
/// <c>nameof</c>, <c>default(T)</c>, <c>sizeof(T)</c>, and <c>checked</c>/<c>unchecked</c> wrappers. Every form
/// folds over the same operand domains as the rest of the evaluator, stays deterministic and
/// culture-independent, and reports what it does not model as a typed stop instead of guessing.
/// </content>
public static partial class ExpressionEvaluator
{
    private const string PatternUnsupportedCode = "EVAL_PATTERN_UNSUPPORTED";
    private const string UncheckedUnsupportedCode = "EVAL_UNCHECKED_UNSUPPORTED";
    private const string SwitchUnmatchedCode = "System.Runtime.CompilerServices.SwitchExpressionException";

    /// <summary>
    /// Folds one interpolated string. Text segments contribute their unescaped content; each interpolation folds,
    /// renders under the invariant culture with its optional format string, and honors a constant alignment with
    /// <c>string.Format</c> padding semantics.
    /// </summary>
    private static FoldOutcome FoldInterpolatedString(
        InterpolatedStringExpressionSyntax interpolated,
        FoldContext context)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    builder.Append(text.TextToken.ValueText);
                    break;
                case InterpolationSyntax interpolation:
                    var folded = Fold(interpolation.Expression, context);
                    if (folded.Disposition != FoldDisposition.Folded)
                    {
                        return folded;
                    }

                    var format = interpolation.FormatClause?.FormatStringToken.ValueText;
                    var rendered = RenderInterpolatedValue(folded.Operand, format);
                    if (rendered.Disposition != FoldDisposition.Folded)
                    {
                        return rendered;
                    }

                    var segment = rendered.Operand.String!;
                    if (interpolation.AlignmentClause is { } alignmentClause)
                    {
                        var alignment = Fold(alignmentClause.Value, context);
                        if (alignment.Disposition != FoldDisposition.Folded)
                        {
                            return alignment;
                        }

                        if (!TryImplicitInt32(alignment.Operand, out var width))
                        {
                            return FoldOutcome.Error(
                                OperandTypeCode,
                                "An interpolation alignment must be an exact Int32 value.");
                        }

                        // string.Format semantics: positive right-aligns, negative left-aligns within |width|.
                        segment = width >= 0 ? segment.PadLeft(width) : segment.PadRight(-width);
                    }

                    builder.Append(segment);
                    break;
                default:
                    return FoldOutcome.NotArithmetic();
            }
        }

        return FoldOutcome.Folded(Operand.FromString(builder.ToString()));
    }

    /// <summary>Renders one folded value the way string interpolation would, under the invariant culture.</summary>
    /// <param name="operand">The folded interpolation value.</param>
    /// <param name="format">The format-clause text, or null when none was written.</param>
    private static FoldOutcome RenderInterpolatedValue(Operand operand, string? format)
    {
        switch (operand.Kind)
        {
            case OperandKind.Null:
                // A null interpolation value contributes an empty segment, exactly as string.Format defines.
                return FoldOutcome.Folded(Operand.FromString(string.Empty));
            case OperandKind.Tuple when format is null:
                // ValueTuple is not IFormattable in the shapes the evaluator folds; ToString renders.
                return FoldOutcome.Folded(Operand.FromString(TupleToStringInvariant(operand)));
            case OperandKind.Anonymous when format is null:
                return FoldOutcome.Folded(Operand.FromString(AnonymousToStringInvariant(operand)));
            case OperandKind.String:
                // string is not IFormattable, so a format clause is ignored, matching string.Format.
                return FoldOutcome.Folded(operand);
            case OperandKind.Char:
                return FoldOutcome.Folded(Operand.FromString(operand.Char.ToString()));
            case OperandKind.Boolean:
                return FoldOutcome.Folded(Operand.FromString(operand.Boolean ? "True" : "False"));
            case OperandKind.Int32:
            case OperandKind.Numeric:
                return NumericToString(operand, format);
            case OperandKind.Temporal:
                return TemporalToString(operand, format);
            case OperandKind.BclValue when operand.BclValueKind == BclValueKind.Guid:
                return FoldOutcome.Folded(Operand.FromString(((Guid)operand.Box!).ToString(
                    format ?? "D",
                    System.Globalization.CultureInfo.InvariantCulture)));
            case OperandKind.BclValue when format is null:
                return FoldOutcome.Folded(Operand.FromString(RenderBclValue(operand)));
            case OperandKind.Enum when format is null:
                return FoldOutcome.Folded(Operand.FromString(operand.EnumMemberName!));
            case OperandKind.Enum:
                return EnumToStringWithFormat(operand, format);
            default:
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "An interpolated value must be a scalar value: a string, char, Boolean, number, null, or "
                    + "an enum member without a format clause.");
        }
    }

    /// <summary>Folds an <c>is</c> pattern over the folded left value.</summary>
    private static FoldOutcome FoldIsPattern(IsPatternExpressionSyntax isPattern, FoldContext context)
    {
        var value = Fold(isPattern.Expression, context);
        if (value.Disposition != FoldDisposition.Folded)
        {
            return value;
        }

        return MatchPattern(value.Operand, isPattern.Pattern, context);
    }

    /// <summary>
    /// Folds a <c>switch</c> expression: the governing value folds once, arms are tested in source order, and the
    /// first arm whose pattern matches — and whose <c>when</c> clause, if any, folds to true — supplies the result.
    /// </summary>
    private static FoldOutcome FoldSwitchExpression(SwitchExpressionSyntax switchExpression, FoldContext context)
    {
        var governing = Fold(switchExpression.GoverningExpression, context);
        if (governing.Disposition != FoldDisposition.Folded)
        {
            return governing;
        }

        foreach (var arm in switchExpression.Arms)
        {
            var matched = MatchPattern(governing.Operand, arm.Pattern, context);
            if (matched.Disposition != FoldDisposition.Folded)
            {
                return matched;
            }

            if (!matched.Operand.Boolean)
            {
                continue;
            }

            if (arm.WhenClause is { } whenClause)
            {
                var condition = Fold(whenClause.Condition, context);
                if (condition.Disposition != FoldDisposition.Folded)
                {
                    return condition;
                }

                if (condition.Operand.Kind != OperandKind.Boolean)
                {
                    return FoldOutcome.Error(OperandTypeCode, "A when clause requires a Boolean condition.");
                }

                if (!condition.Operand.Boolean)
                {
                    continue;
                }
            }

            return Fold(arm.Expression, context);
        }

        return FoldOutcome.Error(
            SwitchUnmatchedCode,
            "No switch arm matched the value, which at run time throws SwitchExpressionException.");
    }

    /// <summary>
    /// Matches one folded value against a pattern, producing a Boolean. Supported patterns are the value-domain
    /// ones: constants, <c>null</c>, relational (<c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c>),
    /// <c>and</c>/<c>or</c>/<c>not</c> combinators, parentheses, and the discard. Type, declaration, property,
    /// and list patterns need runtime type identity this evaluator does not model, so they are typed stops.
    /// </summary>
    private static FoldOutcome MatchPattern(Operand value, PatternSyntax pattern, FoldContext context)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant:
                var expected = Fold(constant.Expression, context);
                if (expected.Disposition != FoldDisposition.Folded)
                {
                    return expected;
                }

                return MatchConstant(value, expected.Operand);
            case RelationalPatternSyntax relational:
                var bound = Fold(relational.Expression, context);
                if (bound.Disposition != FoldDisposition.Folded)
                {
                    return bound;
                }

                // A lifted relational pattern over null is false, matching C#'s lifted comparison semantics.
                if (value.Kind == OperandKind.Null)
                {
                    return FoldOutcome.Folded(Operand.FromBoolean(false));
                }

                if (!value.IsNumeric || !bound.Operand.IsNumeric)
                {
                    return FoldOutcome.Error(
                        OperandTypeCode,
                        "A relational pattern requires numeric operands.");
                }

                return ComputeNumericComparison(
                    relational.OperatorToken.Kind() switch
                    {
                        SyntaxKind.LessThanToken => SyntaxKind.LessThanExpression,
                        SyntaxKind.LessThanEqualsToken => SyntaxKind.LessThanOrEqualExpression,
                        SyntaxKind.GreaterThanToken => SyntaxKind.GreaterThanExpression,
                        _ => SyntaxKind.GreaterThanOrEqualExpression,
                    },
                    value,
                    bound.Operand);
            case UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } negation:
                var inner = MatchPattern(value, negation.Pattern, context);
                return inner.Disposition == FoldDisposition.Folded
                    ? FoldOutcome.Folded(Operand.FromBoolean(!inner.Operand.Boolean))
                    : inner;
            case BinaryPatternSyntax binary:
                var left = MatchPattern(value, binary.Left, context);
                if (left.Disposition != FoldDisposition.Folded)
                {
                    return left;
                }

                // Both sides fold so an unsupported right-hand pattern stays a typed stop even when the left
                // side already decides the outcome, matching how the conditional operator folds both branches.
                var right = MatchPattern(value, binary.Right, context);
                if (right.Disposition != FoldDisposition.Folded)
                {
                    return right;
                }

                return FoldOutcome.Folded(Operand.FromBoolean(binary.IsKind(SyntaxKind.AndPattern)
                    ? left.Operand.Boolean && right.Operand.Boolean
                    : left.Operand.Boolean || right.Operand.Boolean));
            case ParenthesizedPatternSyntax parenthesized:
                return MatchPattern(value, parenthesized.Pattern, context);
            case DiscardPatternSyntax:
                return FoldOutcome.Folded(Operand.FromBoolean(true));
            default:
                return FoldOutcome.Error(
                    PatternUnsupportedCode,
                    "Only constant, null, relational, and/or/not, parenthesized, and discard patterns are "
                    + "supported; type, declaration, property, and list patterns need runtime type identity "
                    + "this evaluator does not model.");
        }
    }

    /// <summary>Compares one folded value against a folded constant pattern.</summary>
    private static FoldOutcome MatchConstant(Operand value, Operand expected)
    {
        if (expected.Kind == OperandKind.Null || value.Kind == OperandKind.Null)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(
                expected.Kind == OperandKind.Null && value.Kind == OperandKind.Null));
        }

        if (value.Kind == OperandKind.String && expected.Kind == OperandKind.String)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(
                string.Equals(value.String, expected.String, StringComparison.Ordinal)));
        }

        if (value.Kind == OperandKind.Boolean && expected.Kind == OperandKind.Boolean)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(value.Boolean == expected.Boolean));
        }

        if (value.IsNumeric && expected.IsNumeric)
        {
            return ComputeNumericComparison(SyntaxKind.EqualsExpression, value, expected);
        }

        return FoldOutcome.Error(
            OperandTypeCode,
            "A constant pattern requires the value and the pattern constant to share a comparable value domain.");
    }

    /// <summary>Folds <c>default(T)</c> for the supported value domains.</summary>
    private static FoldOutcome FoldDefaultExpression(DefaultExpressionSyntax defaultExpression)
    {
        if (!TryResolveCastType(defaultExpression.Type, out var target, out var numericKind, out var nullable))
        {
            return FoldOutcome.NotArithmetic();
        }

        if (nullable)
        {
            return FoldOutcome.Folded(Operand.Null());
        }

        return target switch
        {
            CastTarget.Char => FoldOutcome.Folded(Operand.FromChar('\0')),
            CastTarget.Boolean => FoldOutcome.Folded(Operand.FromBoolean(false)),
            CastTarget.String => FoldOutcome.Folded(Operand.Null()),
            _ => FoldOutcome.Folded(Operand.FromNumeric(numericKind, NumericZero(numericKind))),
        };
    }

    /// <summary>Boxes the zero value of one numeric kind as its exact CLR type.</summary>
    private static object NumericZero(NumericKind kind) => kind switch
    {
        NumericKind.SByte => (sbyte)0,
        NumericKind.Byte => (byte)0,
        NumericKind.Int16 => (short)0,
        NumericKind.UInt16 => (ushort)0,
        NumericKind.Int32 => 0,
        NumericKind.UInt32 => 0u,
        NumericKind.Int64 or NumericKind.IntPtr => 0L,
        NumericKind.UInt64 or NumericKind.UIntPtr => 0ul,
        NumericKind.Int128 => (System.Int128)0,
        NumericKind.UInt128 => (System.UInt128)0,
        NumericKind.BigInteger => System.Numerics.BigInteger.Zero,
        NumericKind.Single => 0f,
        NumericKind.Double => 0d,
        _ => 0m,
    };

    /// <summary>
    /// Folds <c>sizeof(T)</c> for the fixed-size value domains. <c>nint</c>/<c>nuint</c> answer 8, consistent
    /// with the evaluator folding them at 64 bits for the x64 processes the preview targets.
    /// </summary>
    private static FoldOutcome FoldSizeOf(SizeOfExpressionSyntax sizeOf)
    {
        if (!TryResolveCastType(sizeOf.Type, out var target, out var numericKind, out var nullable) || nullable)
        {
            return FoldOutcome.NotArithmetic();
        }

        int? size = target switch
        {
            CastTarget.Char => 2,
            CastTarget.Boolean => 1,
            CastTarget.String => null,
            _ => numericKind switch
            {
                NumericKind.SByte or NumericKind.Byte => 1,
                NumericKind.Int16 or NumericKind.UInt16 => 2,
                NumericKind.Int32 or NumericKind.UInt32 or NumericKind.Single => 4,
                NumericKind.Int64 or NumericKind.UInt64 or NumericKind.Double or
                    NumericKind.IntPtr or NumericKind.UIntPtr => 8,
                NumericKind.Int128 or NumericKind.UInt128 or NumericKind.Decimal => 16,
                _ => null,
            },
        };
        return size is { } bytes
            ? FoldOutcome.Folded(Operand.FromInt32(bytes))
            : FoldOutcome.Error(OperandTypeCode, "sizeof is supported only for the fixed-size value types.");
    }

    /// <summary>
    /// Folds <c>checked(e)</c> and <c>unchecked(e)</c>. The evaluator computes with checked semantics
    /// everywhere, so <c>checked</c> is a pass-through. <c>unchecked</c> passes through when nothing overflows —
    /// where the two semantics agree — and reports a typed stop when the inner expression overflows, because
    /// wrap-around results are deliberately not modeled.
    /// </summary>
    private static FoldOutcome FoldCheckedExpression(CheckedExpressionSyntax checkedExpression, FoldContext context)
    {
        var inner = Fold(checkedExpression.Expression, context);
        if (checkedExpression.IsKind(SyntaxKind.UncheckedExpression) &&
            inner is { Disposition: FoldDisposition.Error, Code: OverflowCode })
        {
            return FoldOutcome.Error(
                UncheckedUnsupportedCode,
                "The inner expression overflows, and unchecked wrap-around is deliberately not modeled; the "
                + "checked evaluation of the same expression reports the overflow.");
        }

        return inner;
    }

    /// <summary>
    /// Folds <c>nameof(...)</c> lexically: the result is the final identifier of the argument, exactly as the
    /// compiler defines, and the argument is never evaluated or bound.
    /// </summary>
    private static FoldOutcome FoldNameOf(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments is not
            [{ NameColon: null, RefKindKeyword.RawKind: 0, Expression: { } argument }])
        {
            return FoldOutcome.Error(OperandTypeCode, "nameof requires exactly one unnamed argument.");
        }

        var name = FinalIdentifierOf(argument);
        return name is null
            ? FoldOutcome.Error(
                OperandTypeCode,
                "nameof requires a name: an identifier, a dotted name, or a member access.")
            : FoldOutcome.Folded(Operand.FromString(name));
    }

    /// <summary>Returns the final identifier of a name-shaped expression, or null when it has none.</summary>
    private static string? FinalIdentifierOf(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } member =>
            FinalIdentifierOf(member.Name),
        QualifiedNameSyntax qualified => FinalIdentifierOf(qualified.Right),
        AliasQualifiedNameSyntax alias => FinalIdentifierOf(alias.Name),
        _ => null,
    };
}
