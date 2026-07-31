using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the session-free half of the constant-expression domain: checked Int32 folding, string and char
/// literals with the deterministic culture-independent BCL member surface, Boolean logic, the typed error
/// dispositions, and the not-constant dispositions that keep every other expression on its existing evaluation
/// path. Literal-field resolution needs a dump session and is proven by the integration lane.
/// </summary>
public sealed class ConstantExpressionEvaluatorTests
{
    /// <summary>Proves exact checked Int32 folding across the admitted operator set.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The exact Int32 value C# constant semantics produce.</param>
    [Theory]
    [InlineData("42", 42)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("10 / 3", 3)]
    [InlineData("10 % 3", 1)]
    [InlineData("-5", -5)]
    [InlineData("+5", 5)]
    [InlineData("~0", -1)]
    [InlineData("-(2 + 3)", -5)]
    [InlineData("-2147483648", int.MinValue)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("int.MaxValue", int.MaxValue)]
    [InlineData("int.MinValue", int.MinValue)]
    [InlineData("1 << 5", 32)]
    [InlineData("-32 >> 2", -8)]
    [InlineData("-32 >>> 28", 15)]
    [InlineData("1 << 33", 2)]
    [InlineData("6 & 3", 2)]
    [InlineData("6 | 3", 7)]
    [InlineData("6 ^ 3", 5)]
    [InlineData("(86400 / 24) / 60", 60)]
    [InlineData("'a' + 'b'", 195)]
    [InlineData("'b' - 'a'", 1)]
    [InlineData("\"abc\".Length", 3)]
    [InlineData("\"abc\".IndexOf('b')", 1)]
    [InlineData("\"abcabc\".IndexOf('b', 2)", 4)]
    [InlineData("\"abcabc\".LastIndexOf('b')", 4)]
    [InlineData("string.CompareOrdinal(\"a\", \"b\")", -1)]
    [InlineData("(\"ab\" + \"cd\").Length * 2", 8)]
    [InlineData("true ? 1 : 2", 1)]
    [InlineData("false ? 1 : 2", 2)]
    public void Integer_results_fold_exactly(string expression, int expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Int32, result.Kind);
        Assert.Equal(expected, result.Int32Value);
        Assert.Null(result.DiagnosticCode);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves the deterministic string surface evaluates exactly.</summary>
    /// <param name="expression">The constant expression producing a string.</param>
    /// <param name="expected">The exact string value.</param>
    [Theory]
    [InlineData("\"abc\"", "abc")]
    [InlineData("\"abc\" + \"def\"", "abcdef")]
    [InlineData("\"ab\" + 'c'", "abc")]
    [InlineData("'a' + \"bc\"", "abc")]
    [InlineData("\"a\" + \"b\" + \"c\" + \"d\"", "abcd")]
    [InlineData("string.Empty", "")]
    [InlineData("string.Empty + \"x\"", "x")]
    [InlineData("\"HELLO\".ToLowerInvariant()", "hello")]
    [InlineData("\"hello\".ToUpperInvariant()", "HELLO")]
    [InlineData("\"hello world\".Substring(6)", "world")]
    [InlineData("\"batch-2026-07-30-0042\".Substring(6, 4)", "2026")]
    [InlineData("\"a-b-c\".Replace('-', '+')", "a+b+c")]
    [InlineData("\"a-b-c\".Replace(\"-\", \" / \")", "a / b / c")]
    [InlineData("\"  padded  \".Trim()", "padded")]
    [InlineData("\"  padded  \".TrimStart()", "padded  ")]
    [InlineData("\"  padded  \".TrimEnd()", "  padded")]
    [InlineData("\"xxvaluexx\".Trim('x')", "value")]
    [InlineData("\"7\".PadLeft(3, '0')", "007")]
    [InlineData("\"7\".PadRight(3, '.')", "7..")]
    [InlineData("\"ac\".Insert(1, \"b\")", "abc")]
    [InlineData("\"abcd\".Remove(2)", "ab")]
    [InlineData("\"abcd\".Remove(1, 2)", "ad")]
    [InlineData("string.Concat(\"a\", \"b\")", "ab")]
    [InlineData("string.Concat(\"a\", 'b', \"c\", 'd', \"e\")", "abcde")]
    [InlineData("string.Join(\"-\", \"a\", \"b\", \"c\")", "a-b-c")]
    [InlineData("'x'.ToString()", "x")]
    [InlineData("\"same\".ToString()", "same")]
    [InlineData("char.ToUpperInvariant('a').ToString()", "A")]
    [InlineData("true ? \"yes\" : \"no\"", "yes")]
    public void String_results_evaluate_exactly(string expression, string expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.String, result.Kind);
        Assert.Equal(expected, result.StringValue);
    }

    /// <summary>Proves ordinal membership, comparison, and char predicates evaluate exactly.</summary>
    /// <param name="expression">The constant expression producing a Boolean.</param>
    /// <param name="expected">The exact Boolean value.</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("!false", true)]
    [InlineData("true && false", false)]
    [InlineData("true || false", true)]
    [InlineData("1 == 1", true)]
    [InlineData("1 != 1", false)]
    [InlineData("1 < 2", true)]
    [InlineData("2 <= 1", false)]
    [InlineData("'b' > 'a'", true)]
    [InlineData("\"abc\" == \"abc\"", true)]
    [InlineData("\"abc\" == \"ABC\"", false)]
    [InlineData("\"abc\" != \"def\"", true)]
    [InlineData("'a' == 'a'", true)]
    [InlineData("\"hello\".Contains(\"ell\")", true)]
    [InlineData("\"hello\".Contains('h')", true)]
    [InlineData("\"hello\".Contains('z')", false)]
    [InlineData("\"hello\".StartsWith('h')", true)]
    [InlineData("\"hello\".EndsWith('o')", true)]
    [InlineData("\"hello\".Equals(\"hello\")", true)]
    [InlineData("string.IsNullOrEmpty(\"\")", true)]
    [InlineData("string.IsNullOrEmpty(\"x\")", false)]
    [InlineData("string.IsNullOrWhiteSpace(\"  \")", true)]
    [InlineData("string.Equals(\"a\", \"a\")", true)]
    [InlineData("char.IsDigit('5')", true)]
    [InlineData("char.IsDigit('x')", false)]
    [InlineData("char.IsLetter('x')", true)]
    [InlineData("char.IsLetterOrDigit('_')", false)]
    [InlineData("char.IsWhiteSpace(' ')", true)]
    [InlineData("char.IsUpper('A')", true)]
    [InlineData("char.IsLower('A')", false)]
    [InlineData("char.IsPunctuation('!')", true)]
    [InlineData("char.IsControl('\\n')", true)]
    [InlineData("char.IsAscii('a')", true)]
    [InlineData("char.IsAsciiHexDigit('F')", true)]
    [InlineData("\"abc\".Length > 2", true)]
    [InlineData("\"abc\"[1] == 'b'", true)]
    public void Boolean_results_evaluate_exactly(string expression, bool expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.BooleanValue);
    }

    /// <summary>Proves char-valued results evaluate exactly.</summary>
    /// <param name="expression">The constant expression producing a char.</param>
    /// <param name="expected">The exact char value.</param>
    [Theory]
    [InlineData("'a'", 'a')]
    [InlineData("\"abc\"[1]", 'b')]
    [InlineData("char.ToUpperInvariant('a')", 'A')]
    [InlineData("char.ToLowerInvariant('A')", 'a')]
    [InlineData("char.MaxValue", char.MaxValue)]
    [InlineData("char.MinValue", char.MinValue)]
    public void Char_results_evaluate_exactly(string expression, char expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Char, result.Kind);
        Assert.Equal(expected, result.CharValue);
    }

    /// <summary>
    /// Proves constant-domain errors are typed stops with familiar exception names for runtime-error analogues
    /// and descriptive codes for admission limits — never fabricated values.
    /// </summary>
    /// <param name="expression">The erroneous constant expression.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("2147483647 + 1", "System.OverflowException")]
    [InlineData("-2147483648 - 1", "System.OverflowException")]
    [InlineData("2147483647 * 2", "System.OverflowException")]
    [InlineData("-(-2147483648)", "System.OverflowException")]
    [InlineData("-2147483648 / -1", "System.OverflowException")]
    [InlineData("1 / 0", "System.DivideByZeroException")]
    [InlineData("1 % 0", "System.DivideByZeroException")]
    [InlineData("1 / (2 - 2)", "System.DivideByZeroException")]
    [InlineData("\"abc\".Substring(5)", "System.ArgumentOutOfRangeException")]
    [InlineData("\"abc\".Substring(1, 5)", "System.ArgumentOutOfRangeException")]
    [InlineData("\"abc\"[3]", "System.ArgumentOutOfRangeException")]
    [InlineData("\"abc\"[-1]", "System.ArgumentOutOfRangeException")]
    [InlineData("\"7\".PadLeft(-1)", "System.ArgumentOutOfRangeException")]
    [InlineData("2147483648", "CONSTANT_LITERAL_TYPE_UNSUPPORTED")]
    [InlineData("1.5 + 2", "CONSTANT_LITERAL_TYPE_UNSUPPORTED")]
    [InlineData("1L + 2", "CONSTANT_LITERAL_TYPE_UNSUPPORTED")]
    [InlineData("\"abc\".ToLower()", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".ToUpper()", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".IndexOf(\"b\")", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".StartsWith(\"a\")", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("char.ToUpper('a')", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".CompareTo(\"abd\")", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".GetHashCode()", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("\"abc\".Split('b')", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("\"abc\".NoSuchMethod()", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("string.Format(\"{0}\", \"x\")", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("\"a\" + 1", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("true + 1", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("\"a\" < \"b\"", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("!1", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("1 && true", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("true ? 1 : \"x\"", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("1 ? 2 : 3", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    public void Errors_are_typed_stops(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(ConstantValueKind.None, result.Kind);
        Assert.Null(result.Int32Value);
        Assert.Equal(expectedCode, result.DiagnosticCode);
    }

    /// <summary>
    /// Proves everything outside the constant domain stays on its existing path: names, roots, method calls on
    /// non-constant receivers, malformed input, and — without a session — every qualified name.
    /// </summary>
    /// <param name="expression">The expression that must fall through.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("root.Field")]
    [InlineData("Some.Namespace.Type.Field")]
    [InlineData("System.DayOfWeek.Monday")]
    [InlineData("1 + x")]
    [InlineData("x + 1")]
    [InlineData("1 +")]
    [InlineData("root.GetMarkerSummary()")]
    [InlineData("root.Name.ToUpperInvariant()")]
    [InlineData("x.Length")]
    [InlineData("1 ?? 2")]
    [InlineData("checked(1 + 2)")]
    public void Everything_else_is_not_constant(string? expression)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.NotConstant, result.Status);
        Assert.Null(result.Int32Value);
        Assert.Null(result.DiagnosticCode);
    }
}
