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
    [InlineData("(int)3.99", 3)]
    [InlineData("(int)(-3.99)", -3)]
    [InlineData("(int)2.5m", 2)]
    [InlineData("(int)'a'", 97)]
    [InlineData("(int)(long)5", 5)]
    [InlineData("(byte)200 + (byte)100", 300)]
    [InlineData("Math.Abs(-7)", 7)]
    [InlineData("Math.Max(2, 3)", 3)]
    [InlineData("Math.Clamp(10, 1, 5)", 5)]
    [InlineData("Math.Sign(-9)", -1)]
    [InlineData("Math.ILogB(1024.0)", 10)]
    [InlineData("null ?? 2", 2)]
    [InlineData("1 ?? 2", 1)]
    [InlineData("(int?)5 + 3", 8)]
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
    [InlineData("\"hello\"[1..3]", "el")]
    [InlineData("\"hello\"[..2]", "he")]
    [InlineData("\"hello\"[2..]", "llo")]
    [InlineData("\"hello\"[..]", "hello")]
    [InlineData("\"hello\"[^3..^1]", "ll")]
    [InlineData("\"hello\"[1..^1]", "ell")]
    [InlineData("\"hello\"[^0..^0]", "")]
    [InlineData("\"abcdef\"[(1 + 1)..(2 * 2)]", "cd")]
    [InlineData("(\"prefix-\" + \"payload\")[7..]", "payload")]
    [InlineData("\"batch-2026-07-30-0042\"[6..^5].ToUpperInvariant()", "2026-07-30")]
    [InlineData("\"a\" + 1", "a1")]
    [InlineData("\"n=\" + 2.5", "n=2.5")]
    [InlineData("\"\" + true", "True")]
    [InlineData("null + \"x\"", "x")]
    [InlineData("true.ToString()", "True")]
    [InlineData("(1.0 / 0.0).ToString()", "Infinity")]
    [InlineData("(-1.0 / 0.0).ToString()", "-Infinity")]
    [InlineData("(0.0 / 0.0).ToString()", "NaN")]
    [InlineData("(-0.0).ToString()", "-0")]
    [InlineData("(0.1 + 0.2).ToString()", "0.30000000000000004")]
    [InlineData("(1e21).ToString()", "1E+21")]
    [InlineData("(255).ToString(\"X4\")", "00FF")]
    [InlineData("(3.14159).ToString(\"F2\")", "3.14")]
    [InlineData("(1234567.891).ToString(\"N2\")", "1,234,567.89")]
    [InlineData("12.5m.ToString()", "12.5")]
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
    [InlineData("0.1 + 0.2 == 0.3", false)]
    [InlineData("0.1 + 0.2 > 0.3", true)]
    [InlineData("double.NaN == double.NaN", false)]
    [InlineData("double.NaN != double.NaN", true)]
    [InlineData("double.IsNaN(0.0 / 0.0)", true)]
    [InlineData("double.IsPositiveInfinity(1.0 / 0.0)", true)]
    [InlineData("double.IsNegative(-0.0)", true)]
    [InlineData("double.IsFinite(1.0 / 0.0)", false)]
    [InlineData("float.IsNaN(0f / 0f)", true)]
    [InlineData("1 == 1.0", true)]
    [InlineData("1.0f == 1.0", true)]
    [InlineData("2m == 2.0m", true)]
    [InlineData("(nint)4 == 4", true)]
    [InlineData("1UL < 2", true)]
    [InlineData("BigInteger.Pow(2, 64) > ulong.MaxValue", true)]
    [InlineData("null == null", true)]
    [InlineData("\"a\" == null", false)]
    [InlineData("(int?)null == null", true)]
    [InlineData("5 > null", false)]
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
    [InlineData("\"hello\"[^1]", 'o')]
    [InlineData("\"hello\"[^5]", 'h')]
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
    [InlineData("\"abc\"[2..1]", "System.ArgumentOutOfRangeException")]
    [InlineData("\"abc\"[..4]", "System.ArgumentOutOfRangeException")]
    [InlineData("\"abc\"[^4..]", "System.ArgumentOutOfRangeException")]
    [InlineData("\"abc\"[^0]", "System.ArgumentOutOfRangeException")]
    [InlineData("\"abc\"[^(-1)..]", "System.ArgumentOutOfRangeException")]
    [InlineData("1[0..1]", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("\"abc\"[\"x\"..]", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("(byte)300", "System.OverflowException")]
    [InlineData("(int)1e300", "System.OverflowException")]
    [InlineData("(int)double.NaN", "System.OverflowException")]
    [InlineData("(long)ulong.MaxValue", "System.OverflowException")]
    [InlineData("(char)(-1)", "System.OverflowException")]
    [InlineData("decimal.MaxValue + 1m", "System.OverflowException")]
    [InlineData("(decimal)double.NaN", "System.OverflowException")]
    [InlineData("long.MaxValue + 1", "System.OverflowException")]
    [InlineData("Int128.MaxValue + 1", "System.OverflowException")]
    [InlineData("10m / 0m", "System.DivideByZeroException")]
    [InlineData("1L / 0", "System.DivideByZeroException")]
    [InlineData("Math.Sign(0.0 / 0.0)", "System.ArithmeticException")]
    [InlineData("BigInteger.Parse(\"abc\")", "System.FormatException")]
    [InlineData("(1.5).ToString(\"Q9\")", "System.FormatException")]
    [InlineData("Math.ReciprocalEstimate(2.0)", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("Math.Sqrt(2m)", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("1m + 0.5", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("1.5 & 2", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("-1 + 1UL", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("-(1UL)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("(bool)1", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("(int)\"5\"", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("(int)null", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("\"abc\".ToLower()", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".ToUpper()", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".IndexOf(\"b\")", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".StartsWith(\"a\")", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("char.ToUpper('a')", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".CompareTo(\"abd\")", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("\"abc\".GetHashCode()", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("\"abc\".NoSuchMethod()", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("string.Format(\"{0}\", \"x\")", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("true + 1", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("\"a\" < \"b\"", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("!1", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("1 && true", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("1 ? 1 : 2", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
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
    [InlineData("System.ConsoleColor.Red")]
    [InlineData("1 + x")]
    [InlineData("x + 1")]
    [InlineData("1 +")]
    [InlineData("root.GetMarkerSummary()")]
    [InlineData("root.Name.ToUpperInvariant()")]
    [InlineData("x.Length")]
    [InlineData("(SomeUnknownType)1")]
    [InlineData("default(SomeUnknownType)")]
    [InlineData("sizeof(SomeUnknownType)")]
    public void Everything_else_is_not_constant(string? expression)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.NotConstant, result.Status);
        Assert.Null(result.Int32Value);
        Assert.Null(result.DiagnosticCode);
    }

    /// <summary>
    /// Proves the wider numeric tower folds with C# promotion, checked conversion, and IEEE-754 semantics: every
    /// fixed-size type, <c>nint</c>/<c>nuint</c> at 64 bits, <c>Int128</c>/<c>UInt128</c>, <c>BigInteger</c>,
    /// <c>float</c>/<c>double</c> with signed zero, infinities, and NaN, and <c>decimal</c> with exact scale.
    /// </summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact numeric kind the C# rules assign to the result.</param>
    /// <param name="expectedText">The invariant-culture text of the exact value.</param>
    [Theory]
    [InlineData("1.5 + 2", "Double", "3.5")]
    [InlineData("1L + 2", "Int64", "3")]
    [InlineData("2147483648", "UInt32", "2147483648")]
    [InlineData("10 / 4.0", "Double", "2.5")]
    [InlineData("(double)1 / 3", "Double", "0.3333333333333333")]
    [InlineData("0.1 + 0.2", "Double", "0.30000000000000004")]
    [InlineData("1.0 / 0.0", "Double", "Infinity")]
    [InlineData("-1.0 / 0.0", "Double", "-Infinity")]
    [InlineData("0.0 / 0.0", "Double", "NaN")]
    [InlineData("-0.0", "Double", "-0")]
    [InlineData("1e300 * 1e300", "Double", "Infinity")]
    [InlineData("7 % 2.5", "Double", "2")]
    [InlineData("1f / 4", "Single", "0.25")]
    [InlineData("(float)0.1", "Single", "0.1")]
    [InlineData("float.MaxValue * 2f", "Single", "Infinity")]
    [InlineData("10m / 4", "Decimal", "2.5")]
    [InlineData("0.1m + 0.2m", "Decimal", "0.3")]
    [InlineData("1m + 2", "Decimal", "3")]
    [InlineData("(decimal)0.1", "Decimal", "0.1")]
    [InlineData("10.5m % 3", "Decimal", "1.5")]
    [InlineData("Math.Abs(-1.5m)", "Decimal", "1.5")]
    [InlineData("Math.Floor(-2.5m)", "Decimal", "-3")]
    [InlineData("3 * 1000000000L", "Int64", "3000000000")]
    [InlineData("(long)int.MaxValue + 1", "Int64", "2147483648")]
    [InlineData("-9223372036854775808", "Int64", "-9223372036854775808")]
    [InlineData("long.MaxValue", "Int64", "9223372036854775807")]
    [InlineData("uint.MaxValue + 1", "Int64", "4294967296")]
    [InlineData("ulong.MaxValue", "UInt64", "18446744073709551615")]
    [InlineData("5UL + 5", "UInt64", "10")]
    [InlineData("(uint)15", "UInt32", "15")]
    [InlineData("(ushort)65535", "UInt16", "65535")]
    [InlineData("(sbyte)-5", "SByte", "-5")]
    [InlineData("(short)1000", "Int16", "1000")]
    [InlineData("(nint)5 + 3", "IntPtr", "8")]
    [InlineData("(nuint)5 * 2", "UIntPtr", "10")]
    [InlineData("nint.MaxValue", "IntPtr", "9223372036854775807")]
    [InlineData("(Int128)long.MaxValue + 1", "Int128", "9223372036854775808")]
    [InlineData("Int128.MaxValue", "Int128", "170141183460469231731687303715884105727")]
    [InlineData("UInt128.MaxValue", "UInt128", "340282366920938463463374607431768211455")]
    [InlineData(
        "(System.Numerics.BigInteger)1 << 200",
        "BigInteger",
        "1606938044258990275541962092341162602522202993782792835301376")]
    [InlineData("BigInteger.Pow(2, 100)", "BigInteger", "1267650600228229401496703205376")]
    [InlineData(
        "BigInteger.Parse(\"123456789012345678901234567890\") + 1",
        "BigInteger",
        "123456789012345678901234567891")]
    [InlineData("Math.Sqrt(2.0)", "Double", "1.4142135623730951")]
    [InlineData("Math.Pow(2, 10)", "Double", "1024")]
    [InlineData("Math.Floor(2.9)", "Double", "2")]
    [InlineData("Math.Round(2.5)", "Double", "2")]
    [InlineData("Math.Round(2.567, 2)", "Double", "2.57")]
    [InlineData("Math.Abs(-2.5)", "Double", "2.5")]
    [InlineData("Math.Max(1.5, 2)", "Double", "2")]
    [InlineData("Math.Min(3L, 2)", "Int64", "2")]
    [InlineData("Math.PI", "Double", "3.141592653589793")]
    [InlineData("double.MaxValue", "Double", "1.7976931348623157E+308")]
    [InlineData("double.Epsilon", "Double", "5E-324")]
    [InlineData("1 + 2L + 0.5", "Double", "3.5")]
    [InlineData("(double?)2.5", "Double", "2.5")]
    public void Numeric_results_fold_exactly(string expression, string expectedTypeName, string expectedText)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Numeric, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>
    /// Proves virtual array sequences: creation from initializers and array-producing BCL members, indexing and
    /// slicing, and the deterministic lambda-free <c>System.Linq.Enumerable</c> surface. A sequence result renders
    /// its exact element type and elements.
    /// </summary>
    /// <param name="expression">The constant expression producing a sequence.</param>
    /// <param name="expectedTypeName">The element type name with array suffix.</param>
    /// <param name="expectedText">The rendered elements.</param>
    [Theory]
    [InlineData("new[] { 1, 2, 3 }", "Int32[]", "{ 1, 2, 3 }")]
    [InlineData("new int[] { 4, 5 }", "Int32[]", "{ 4, 5 }")]
    [InlineData("new[] { 1, 2.5 }", "Double[]", "{ 1, 2.5 }")]
    [InlineData("new[] { \"a\", null, \"c\" }", "String[]", "{ \"a\", null, \"c\" }")]
    [InlineData("new[] { 'x', 'y' }", "Char[]", "{ 'x', 'y' }")]
    [InlineData("\"abc\".ToCharArray()", "Char[]", "{ 'a', 'b', 'c' }")]
    [InlineData("\"a,b,c\".Split(',')", "String[]", "{ \"a\", \"b\", \"c\" }")]
    [InlineData("\"a,b;c\".Split(',', ';')", "String[]", "{ \"a\", \"b\", \"c\" }")]
    [InlineData("\"a, ,b\".Split(',', StringSplitOptions.RemoveEmptyEntries)", "String[]", "{ \"a\", \" \", \"b\" }")]
    [InlineData(
        "\"a, ,b\".Split(',', StringSplitOptions.TrimEntries)",
        "String[]",
        "{ \"a\", \"\", \"b\" }")]
    [InlineData("\"x--y\".Split(\"--\")", "String[]", "{ \"x\", \"y\" }")]
    [InlineData("Enumerable.Range(3, 4)", "Int32[]", "{ 3, 4, 5, 6 }")]
    [InlineData("Enumerable.Repeat(\"ok\", 3)", "String[]", "{ \"ok\", \"ok\", \"ok\" }")]
    [InlineData("new[] { 5, 1, 4 }.Order()", "Int32[]", "{ 1, 4, 5 }")]
    [InlineData("new[] { 5, 1, 4 }.OrderDescending()", "Int32[]", "{ 5, 4, 1 }")]
    [InlineData("new[] { 1, 2, 2, 3, 1 }.Distinct()", "Int32[]", "{ 1, 2, 3 }")]
    [InlineData("new[] { 1, 2, 3 }.Reverse()", "Int32[]", "{ 3, 2, 1 }")]
    [InlineData("new[] { 1, 2, 3, 4 }.Skip(1).Take(2)", "Int32[]", "{ 2, 3 }")]
    [InlineData("new[] { 1, 2, 3, 4 }.SkipLast(1).TakeLast(2)", "Int32[]", "{ 2, 3 }")]
    [InlineData("new[] { 1, 2 }.Concat(new[] { 3 })", "Int32[]", "{ 1, 2, 3 }")]
    [InlineData("new[] { 1, 2 }.Append(3).Prepend(0)", "Int32[]", "{ 0, 1, 2, 3 }")]
    [InlineData("new[] { 1, 2, 2, 3 }.Union(new[] { 3, 4 })", "Int32[]", "{ 1, 2, 3, 4 }")]
    [InlineData("new[] { 1, 2, 3 }.Except(new[] { 2 })", "Int32[]", "{ 1, 3 }")]
    [InlineData("new[] { 1, 2, 3 }.Intersect(new[] { 2, 3, 4 })", "Int32[]", "{ 2, 3 }")]
    [InlineData("new[] { 1, 2, 3, 4 }[1..3]", "Int32[]", "{ 2, 3 }")]
    [InlineData("\"batch-2026-07-30-0042\".Split('-')[1..^1]", "String[]", "{ \"2026\", \"07\", \"30\" }")]
    [InlineData("new[] { \"b\", \"a\", \"b\" }.Distinct()", "String[]", "{ \"b\", \"a\" }")]
    [InlineData("\"aab\".ToCharArray().Distinct()", "Char[]", "{ 'a', 'b' }")]
    public void Sequence_results_evaluate_exactly(string expression, string expectedTypeName, string expectedText)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Sequence, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves sequences collapse to scalar answers through the lambda-free Enumerable surface.</summary>
    /// <param name="expression">The constant expression consuming a sequence.</param>
    /// <param name="expected">The exact rendered scalar.</param>
    [Theory]
    [InlineData("new[] { 1, 2, 3 }.Length", "3")]
    [InlineData("\"a,b,c\".Split(',').Length", "3")]
    [InlineData("new[] { 1, 2, 3 }.Count()", "3")]
    [InlineData("new[] { 1, 2, 3 }.Sum()", "6")]
    [InlineData("new[] { 1210, 980, 30045, 1105 }.Max()", "30045")]
    [InlineData("new[] { 1210, 980, 30045, 1105 }.Min()", "980")]
    [InlineData("new[] { 1, 2, 3, 4 }.Average()", "2.5")]
    [InlineData("new[] { 1.5, 2.5 }.Sum()", "4")]
    [InlineData("new[] { 10m, 0.1m }.Sum()", "10.1")]
    [InlineData("new[] { 1L, 2L }.Sum()", "3")]
    [InlineData("new[] { 1, 2, 3 }.First()", "1")]
    [InlineData("new[] { 1, 2, 3 }.Last()", "3")]
    [InlineData("new[] { 7 }.Single()", "7")]
    [InlineData("new[] { 1, 2, 3 }.ElementAt(1)", "2")]
    [InlineData("new[] { 1, 2, 3 }.ElementAtOrDefault(9)", "0")]
    [InlineData("\"abc\".ToCharArray()[^1].ToString()", "\"c\"")]
    [InlineData("\"a-b-c\".Split('-')[1]", "\"b\"")]
    [InlineData("string.Join(\"/\", new[] { \"x\", \"y\" })", "\"x/y\"")]
    [InlineData("string.Join(\", \", new[] { 1, 2, 3 })", "\"1, 2, 3\"")]
    [InlineData("string.Concat(\"abc\".ToCharArray().Reverse())", "\"cba\"")]
    [InlineData("\"batch-2026-07-30-0042\".Split('-').First()", "\"batch\"")]
    public void Sequence_scalars_evaluate_exactly(string expression, string expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var rendered = result.Kind switch
        {
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ConstantValueKind.Numeric => result.ValueText,
            ConstantValueKind.String => "\"" + result.StringValue + "\"",
            _ => result.ValueText,
        };
        Assert.Equal(expected, rendered);
    }

    /// <summary>Proves sequence predicates answer exactly.</summary>
    /// <param name="expression">The constant expression producing a Boolean from a sequence.</param>
    /// <param name="expected">The exact Boolean value.</param>
    [Theory]
    [InlineData("new[] { \"priority\", \"cross-border\" }.Contains(\"priority\")", true)]
    [InlineData("new[] { \"priority\", \"cross-border\" }.Contains(\"bulk\")", false)]
    [InlineData("new[] { 1, 2, 3 }.Contains(2)", true)]
    [InlineData("new[] { 1, 2, 3 }.Contains(2L)", true)]
    [InlineData("\"abc\".ToCharArray().Contains('b')", true)]
    [InlineData("new[] { 1, 2 }.Any()", true)]
    [InlineData("new[] { 1, 2 }.Skip(5).Any()", false)]
    [InlineData("new[] { 1, 2 }.SequenceEqual(new[] { 1, 2 })", true)]
    [InlineData("new[] { 1, 2 }.SequenceEqual(new[] { 2, 1 })", false)]
    [InlineData("\"a,b\".Split(',').SequenceEqual(new[] { \"a\", \"b\" })", true)]
    public void Sequence_predicates_evaluate_exactly(string expression, bool expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.BooleanValue);
    }

    /// <summary>Proves sequence errors are typed stops with exact semantics, never fabricated values.</summary>
    /// <param name="expression">The erroneous sequence expression.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("new int[0].First()", "System.InvalidOperationException")]
    [InlineData("new[] { 1, 2 }.Skip(5).First()", "System.InvalidOperationException")]
    [InlineData("new[] { 1, 2 }.Single()", "System.InvalidOperationException")]
    [InlineData("new[] { 1, 2 }.Skip(9).Max()", "System.InvalidOperationException")]
    [InlineData("new[] { 1, 2 }[5]", "System.IndexOutOfRangeException")]
    [InlineData("new[] { 1, 2 }.ElementAt(5)", "System.ArgumentOutOfRangeException")]
    [InlineData("new[] { int.MaxValue, 1 }.Sum()", "System.OverflowException")]
    [InlineData("Enumerable.Range(0, 5000)", "CONSTANT_SEQUENCE_BOUND_EXCEEDED")]
    [InlineData("Enumerable.Repeat(1, -1)", "System.ArgumentOutOfRangeException")]
    [InlineData("new[] { \"b\", \"a\" }.Order()", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("new[] { \"b\", \"a\" }.Max()", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("new[] { \"a\", 'b' }", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new[] { \"a\" }.Sum()", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    public void Sequence_errors_are_typed_stops(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
    }

    /// <summary>Proves lifted nullable semantics: a null operand yields exactly null, never a fabricated value.</summary>
    /// <param name="expression">The constant expression producing exactly null.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("(int?)null")]
    [InlineData("(int?)null + 5")]
    [InlineData("-((int?)null)")]
    [InlineData("(long?)null * 10")]
    [InlineData("(double?)null ?? null")]
    public void Null_results_are_exact(string expression)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Null, result.Kind);
        Assert.Equal("null", result.ValueText);
        Assert.Null(result.DiagnosticCode);
    }
}
