using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the complete .NET numeric surface: Half computes at true IEEE binary16 precision, NFloat folds at 64
/// bits under the stated x64 assumption, the wide integers carry their full constant sets, every fixed-size kind
/// parses under the invariant culture, the IEEE predicate families answer at each kind's own precision, and the
/// decimal type statics fold.
/// </summary>
public sealed class ExpressionEvaluatorNumericCoverageTests
{
    private static ExpressionEvaluation Evaluate(string expression) =>
        ExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>Half folds at binary16 precision: constants, casts, arithmetic, and its rounding.</summary>
    [Theory]
    [InlineData("(Half)1.5 + (Half)0.25", "1.75", "Half")]
    [InlineData("(Half)0.1 + (Half)0.2", "0.2998", "Half")]
    [InlineData("Half.MaxValue", "65500", "Half")]
    [InlineData("Half.Epsilon", "6E-08", "Half")]
    [InlineData("-(Half)2", "-2", "Half")]
    [InlineData("(Half)65520", "Infinity", "Half")]
    [InlineData("(double)(Half)0.1", "0.0999755859375", "Double")]
    [InlineData("(int)(Half)3.9", "3", "Int32")]
    [InlineData("Half.Pi", "3.14", "Half")]
    [InlineData("default(Half)", "0", "Half")]
    [InlineData("Half.Parse(\"1.5\")", "1.5", "Half")]
    public void Half_folds_at_binary16_precision(string expression, string expectedValue, string expectedType)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedType, result.StoredValueTypeName);
        Assert.Equal(expectedValue, result.ValueText ?? result.Int32Value?.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>NFloat folds at 64 bits, pairing with the numeric tower like the float it is.</summary>
    [Theory]
    [InlineData("(NFloat)2.5 + 1", "3.5", "NFloat")]
    [InlineData("(NFloat)1 / 4", "0.25", "NFloat")]
    [InlineData("NFloat.Size", "8", "Int32")]
    [InlineData("(double)(NFloat)0.5 * 2", "1", "Double")]
    [InlineData("(System.Runtime.InteropServices.NFloat)3 * 2", "6", "NFloat")]
    [InlineData("NFloat.Parse(\"2.5\")", "2.5", "NFloat")]
    public void NFloat_folds_at_64_bits(string expression, string expectedValue, string expectedType)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedType, result.StoredValueTypeName);
        Assert.Equal(expectedValue, result.ValueText ?? result.Int32Value?.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Half mixes with no other kind, exactly as C# defines no such operators.</summary>
    [Fact]
    public void Half_refuses_mixed_operands()
    {
        var mixed = Evaluate("(Half)1 + 2");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, mixed.Status);
        Assert.Contains("Half", mixed.DiagnosticMessage!, StringComparison.Ordinal);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("Math.Abs((Half)1)").Status);
    }

    /// <summary>The wide integer constants and invariant Parse fold for every fixed-size kind.</summary>
    [Theory]
    [InlineData("Int128.NegativeOne", "-1", "Int128")]
    [InlineData("UInt128.One + UInt128.Zero", "1", "UInt128")]
    [InlineData("int.Parse(\"-42\")", "-42", "Int32")]
    [InlineData("ulong.Parse(\"18446744073709551615\")", "18446744073709551615", "UInt64")]
    [InlineData("Int128.Parse(\"170141183460469231731687303715884105727\")",
        "170141183460469231731687303715884105727", "Int128")]
    [InlineData("nint.Parse(\"-5\")", "-5", "IntPtr")]
    [InlineData("double.Parse(\"1.5e300\")", "1.5E+300", "Double")]
    [InlineData("decimal.Parse(\"79228162514264337593543950335\")",
        "79228162514264337593543950335", "Decimal")]
    public void Wide_constants_and_invariant_parse_fold(
        string expression, string expectedValue, string expectedType)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedType, result.StoredValueTypeName);
        Assert.Equal(expectedValue, result.ValueText ?? result.Int32Value?.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>The IEEE predicates answer at each kind's own precision, where the widths disagree.</summary>
    [Fact]
    public void Ieee_predicates_answer_per_precision()
    {
        // 1e-40 is subnormal for float but normal for double — the predicate must compute at its own width.
        Assert.Equal(true, Evaluate("float.IsSubnormal(1e-40f)").BooleanValue);
        Assert.Equal(false, Evaluate("double.IsSubnormal(1e-40)").BooleanValue);
        Assert.Equal(true, Evaluate("double.IsNaN(0.0 / 0.0)").BooleanValue);
        Assert.Equal(true, Evaluate("Half.IsPositiveInfinity((Half)65520)").BooleanValue);
        Assert.Equal(true, Evaluate("double.IsInteger(4.0)").BooleanValue);
        Assert.Equal(true, Evaluate("float.IsOddInteger(3f)").BooleanValue);
        Assert.Equal(true, Evaluate("NFloat.IsFinite((NFloat)1)").BooleanValue);
        Assert.Equal(true, Evaluate("double.IsNegative(double.NegativeZero)").BooleanValue);

        // Half admits only Half arguments, exactly as C# provides no implicit conversion into binary16.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("Half.IsNaN(1.0)").Status);
    }

    /// <summary>The decimal type statics fold with the BCL's own semantics.</summary>
    [Fact]
    public void Decimal_type_statics_fold()
    {
        Assert.Equal("1.23", Evaluate("decimal.Round(1.2345m, 2)").ValueText);
        Assert.Equal("2", Evaluate("decimal.Ceiling(1.2m)").ValueText);
        Assert.Equal("1", Evaluate("decimal.Floor(1.8m)").ValueText);
        Assert.Equal("-1.5", Evaluate("decimal.Negate(1.5m)").ValueText);
        Assert.Equal("0.75", Evaluate("decimal.Divide(3, 4)").ValueText);
        Assert.Equal("1", Evaluate("decimal.Compare(2m, 1m)").Int32Value?.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("decimal.Divide(1, 0)").Status);
    }
}
