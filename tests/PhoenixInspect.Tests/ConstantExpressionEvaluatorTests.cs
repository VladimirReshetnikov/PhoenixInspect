using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the session-free half of the constant-expression domain: checked Int32 folding over integer literals,
/// its typed arithmetic errors, and the not-constant dispositions that keep every other expression on its
/// existing evaluation path. Literal-field resolution needs a dump session and is proven by the integration lane.
/// </summary>
public sealed class ConstantExpressionEvaluatorTests
{
    /// <summary>Proves exact checked folding across the admitted operator set.</summary>
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
    [InlineData("1 << 5", 32)]
    [InlineData("-32 >> 2", -8)]
    [InlineData("-32 >>> 28", 15)]
    [InlineData("1 << 33", 2)]
    [InlineData("6 & 3", 2)]
    [InlineData("6 | 3", 7)]
    [InlineData("6 ^ 3", 5)]
    [InlineData("(86400 / 24) / 60", 60)]
    [InlineData("2147483647 + -1", 2147483646)]
    public void Integer_arithmetic_folds_exactly(string expression, int expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Int32, result.Kind);
        Assert.Equal(expected, result.Int32Value);
        Assert.Null(result.DiagnosticCode);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves arithmetic errors are typed constant-domain stops, never fabricated values.</summary>
    /// <param name="expression">The erroneous constant expression.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("2147483647 + 1", "CONSTANT_OVERFLOW")]
    [InlineData("-2147483648 - 1", "CONSTANT_OVERFLOW")]
    [InlineData("2147483647 * 2", "CONSTANT_OVERFLOW")]
    [InlineData("-(-2147483648)", "CONSTANT_OVERFLOW")]
    [InlineData("-2147483648 / -1", "CONSTANT_OVERFLOW")]
    [InlineData("1 / 0", "CONSTANT_DIVISION_BY_ZERO")]
    [InlineData("1 % 0", "CONSTANT_DIVISION_BY_ZERO")]
    [InlineData("1 / (2 - 2)", "CONSTANT_DIVISION_BY_ZERO")]
    [InlineData("2147483648", "CONSTANT_LITERAL_TYPE_UNSUPPORTED")]
    [InlineData("1.5 + 2", "CONSTANT_LITERAL_TYPE_UNSUPPORTED")]
    [InlineData("1L + 2", "CONSTANT_LITERAL_TYPE_UNSUPPORTED")]
    public void Arithmetic_errors_are_typed_stops(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(ConstantValueKind.None, result.Kind);
        Assert.Null(result.Int32Value);
        Assert.Equal(expectedCode, result.DiagnosticCode);
    }

    /// <summary>
    /// Proves everything outside the constant domain stays on its existing path: names, roots, method calls,
    /// non-arithmetic operators, malformed input, and — without a session — every qualified name.
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
    [InlineData("true")]
    [InlineData("1 == 1")]
    [InlineData("1 < 2")]
    [InlineData("root.GetMarkerSummary()")]
    [InlineData("\"text\"")]
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
