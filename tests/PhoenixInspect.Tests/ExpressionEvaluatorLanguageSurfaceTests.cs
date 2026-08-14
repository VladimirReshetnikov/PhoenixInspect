using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the language-surface batch: unchecked wrap-around semantics, the generic-math type statics
/// (T.Max/Min/Clamp), the numeric instance surface (CompareTo, Equals, GetHashCode, BigInteger's classifiers,
/// decimal.Scale), the MathF family at float precision, the BigInteger static gaps, and 'is'/'as' type tests
/// answered from runtime identity.
/// </summary>
public sealed class ExpressionEvaluatorLanguageSurfaceTests
{
    private static ExpressionEvaluation Evaluate(string expression) =>
        ExpressionEvaluator.Evaluate(session: null, expression);

    private static string? Render(ExpressionEvaluation result) =>
        result.ValueText
        ?? result.Int32Value?.ToString(System.Globalization.CultureInfo.InvariantCulture)
        ?? result.BooleanValue?.ToString();

    /// <summary>'unchecked' switches its lexical region to wrap-around, exactly as C# defines it.</summary>
    [Theory]
    [InlineData("unchecked(int.MaxValue + 1)", "-2147483648", "Int32")]
    [InlineData("unchecked(int.MinValue - 1)", "2147483647", "Int32")]
    [InlineData("unchecked((int)4000000000u)", "-294967296", "Int32")]
    [InlineData("unchecked((byte)300)", "44", "Byte")]
    [InlineData("unchecked(2_000_000_000 + 2_000_000_000)", "-294967296", "Int32")]
    [InlineData("unchecked((short)(short.MaxValue + 1))", "-32768", "Int16")]
    [InlineData("unchecked(ulong.MaxValue * 2)", "18446744073709551614", "UInt64")]
    public void Unchecked_wraps_the_low_bits(string expression, string expectedValue, string expectedType)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedType, result.StoredValueTypeName);
        Assert.Equal(expectedValue, Render(result));
    }

    /// <summary>Checked stays the default, a nested 'checked' wins back, and division overflow still throws.</summary>
    [Fact]
    public void Checked_remains_the_default_and_division_overflow_still_throws()
    {
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("int.MaxValue + 1").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("unchecked(checked(int.MaxValue + 1))").Status);
        Assert.Equal(
            ExpressionEvaluationStatus.Exact, Evaluate("checked(unchecked(int.MaxValue + 1))").Status);

        // 'int.MinValue / -1' overflows at run time even in an unchecked context.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("unchecked(int.MinValue / -1)").Status);
    }

    /// <summary>The generic-math statics fold at each type's own kind, with implicit-argument admission.</summary>
    [Theory]
    [InlineData("int.Max(3, 5)", "5", "Int32")]
    [InlineData("int.Min(-3, 5)", "-3", "Int32")]
    [InlineData("int.Clamp(10, 0, 5)", "5", "Int32")]
    [InlineData("byte.Clamp(200, 10, 100)", "100", "Byte")]
    [InlineData("long.Min(2, 3)", "2", "Int64")]
    [InlineData("double.Clamp(2.5, 0.0, 1.0)", "1", "Double")]
    [InlineData("double.Max(1, 2.5)", "2.5", "Double")]
    [InlineData("float.Min(1.5f, 2f)", "1.5", "Single")]
    [InlineData("Half.Max((Half)1, (Half)2)", "2", "Half")]
    [InlineData("decimal.Max(1.5m, 2.5m)", "2.5", "Decimal")]
    [InlineData("decimal.Clamp(7, 1, 5)", "5", "Decimal")]
    [InlineData("nint.Max(3, 4)", "4", "IntPtr")]
    [InlineData("Int128.Max(3, 4)", "4", "Int128")]
    [InlineData("BigInteger.Clamp(10, 0, 5)", "5", "BigInteger")]
    public void Generic_math_statics_fold(string expression, string expectedValue, string expectedType)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedType, result.StoredValueTypeName);
        Assert.Equal(expectedValue, Render(result));
    }

    /// <summary>Clamp validates its bounds and the statics refuse arguments without an implicit conversion.</summary>
    [Fact]
    public void Generic_math_statics_validate_arguments()
    {
        var inverted = Evaluate("int.Clamp(1, 5, 0)");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, inverted.Status);
        Assert.Equal("System.ArgumentException", inverted.DiagnosticCode);

        // 3.5 does not convert implicitly to int, exactly as 'int.Max(1, 3.5)' does not compile.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("int.Max(1, 3.5)").Status);

        // Half admits only Half arguments: C# provides no implicit conversion into binary16.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("Half.Max(1, 2)").Status);
    }

    /// <summary>The numeric instance surface: CompareTo, Equals, and the value-based hash.</summary>
    [Fact]
    public void Numeric_instance_members_fold()
    {
        Assert.Equal(1, Evaluate("(5).CompareTo(3)").Int32Value);
        Assert.Equal(-1, Evaluate("(3).CompareTo(5)").Int32Value);
        Assert.Equal(0, Evaluate("2.5.CompareTo(2.5)").Int32Value);
        Assert.Equal(true, Evaluate("(3).Equals(3)").BooleanValue);
        Assert.Equal(false, Evaluate("(3).Equals(4)").BooleanValue);
        Assert.Equal(3, Evaluate("(3).GetHashCode()").Int32Value);
        Assert.Equal(0, Evaluate("3L.CompareTo(3)").Int32Value);

        // Equals is value equality, not the '==' operator: NaN equals NaN.
        Assert.Equal(true, Evaluate("double.NaN.Equals(0.0 / 0.0)").BooleanValue);
        Assert.Equal(false, Evaluate("(1.0m).Equals(2)").BooleanValue);
    }

    /// <summary>BigInteger's classifier properties and decimal.Scale read from the folded value.</summary>
    [Fact]
    public void Numeric_instance_properties_fold()
    {
        Assert.Equal(-1, Evaluate("BigInteger.Parse(\"-5\").Sign").Int32Value);
        Assert.Equal(true, Evaluate("BigInteger.Parse(\"12\").IsEven").BooleanValue);
        Assert.Equal(true, Evaluate("BigInteger.One.IsOne").BooleanValue);
        Assert.Equal(true, Evaluate("BigInteger.Zero.IsZero").BooleanValue);
        Assert.Equal(true, Evaluate("BigInteger.Pow(2, 10).IsPowerOfTwo").BooleanValue);

        var scale = Evaluate("1.500m.Scale");
        Assert.Equal(ExpressionEvaluationStatus.Exact, scale.Status);
        Assert.Equal("Byte", scale.StoredValueTypeName);
        Assert.Equal("3", scale.ValueText);
    }

    /// <summary>MathF computes at float precision and answers Single, with float-domain admission.</summary>
    [Fact]
    public void MathF_folds_at_float_precision()
    {
        var sqrt = Evaluate("MathF.Sqrt(4f)");
        Assert.Equal(ExpressionEvaluationStatus.Exact, sqrt.Status);
        Assert.Equal("Single", sqrt.StoredValueTypeName);
        Assert.Equal("2", sqrt.ValueText);

        Assert.Equal("3.1415927", Evaluate("MathF.PI").ValueText);
        Assert.Equal("2", Evaluate("MathF.Max(1f, 2f)").ValueText);
        Assert.Equal("3", Evaluate("MathF.Abs(-3)").ValueText);
        Assert.Equal("Single", Evaluate("MathF.Abs(-3)").StoredValueTypeName);
        Assert.Equal("8", Evaluate("MathF.Pow(2f, 3f)").ValueText);
        Assert.Equal(1, Evaluate("MathF.Sign(0.5f)").Int32Value);

        // A double argument does not convert implicitly to float, exactly as 'MathF.Sqrt(2.0)' does not compile.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("MathF.Sqrt(2.0)").Status);
    }

    /// <summary>The BigInteger static gaps: named arithmetic, Compare, and the logarithm family.</summary>
    [Fact]
    public void BigInteger_static_gaps_fold()
    {
        Assert.Equal("5", Evaluate("BigInteger.Add(2, 3)").ValueText);
        Assert.Equal("-1", Evaluate("BigInteger.Subtract(2, 3)").ValueText);
        Assert.Equal("6", Evaluate("BigInteger.Multiply(2, 3)").ValueText);
        Assert.Equal("3", Evaluate("BigInteger.Divide(7, 2)").ValueText);
        Assert.Equal("1", Evaluate("BigInteger.Remainder(7, 2)").ValueText);
        Assert.Equal(-1, Evaluate("BigInteger.Compare(2, 3)").Int32Value);
        Assert.Equal("5", Evaluate("BigInteger.Log10(BigInteger.Pow(10, 5))").ValueText);
        Assert.Equal("Double", Evaluate("BigInteger.Log10(BigInteger.Pow(10, 5))").StoredValueTypeName);
        Assert.Equal("10", Evaluate("BigInteger.Log2(1024)").ValueText);
        Assert.Equal("BigInteger", Evaluate("BigInteger.Log2(1024)").StoredValueTypeName);

        var divideByZero = Evaluate("BigInteger.Divide(1, 0)");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, divideByZero.Status);
        Assert.Equal("System.DivideByZeroException", divideByZero.DiagnosticCode);
    }

    /// <summary>'is T' answers from the folded value's exact runtime kind.</summary>
    [Theory]
    [InlineData("5 is int", true)]
    [InlineData("5 is long", false)]
    [InlineData("5L is long", true)]
    [InlineData("\"abc\" is string", true)]
    [InlineData("5 is string", false)]
    [InlineData("5 is object", true)]
    [InlineData("null is object", false)]
    [InlineData("null is string", false)]
    [InlineData("2.5 is double", true)]
    [InlineData("2.5f is float", true)]
    [InlineData("2.5m is decimal", true)]
    [InlineData("'a' is char", true)]
    [InlineData("true is bool", true)]
    [InlineData("5 is int?", true)]
    [InlineData("(nint)5 is nint", true)]
    [InlineData("(Half)1 is Half", true)]
    [InlineData("BigInteger.One is BigInteger", true)]
    [InlineData("DateTime.UnixEpoch is DateTime", true)]
    [InlineData("Guid.Empty is Guid", true)]
    [InlineData("5 is not int", false)]
    public void Is_type_test_answers_runtime_identity(string expression, bool expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expected, result.BooleanValue);
    }

    /// <summary>'as' yields the value or null for the reference and nullable targets C# admits.</summary>
    [Fact]
    public void As_yields_the_value_or_null()
    {
        var same = Evaluate("\"abc\" as string");
        Assert.Equal(ExpressionEvaluationStatus.Exact, same.Status);
        Assert.Equal("abc", same.StringValue);

        Assert.Equal(5, Evaluate("5 as int?").Int32Value);
        Assert.Equal(ExpressionValueKind.Null, Evaluate("2.5 as int?").Kind);
        Assert.Equal(ExpressionValueKind.Null, Evaluate("5 as long?").Kind);
        Assert.Equal(5, Evaluate("5 as object").Int32Value);

        // A bare value-type target is a compile error in C# and a typed stop here.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("5 as int").Status);
    }

    /// <summary>Type patterns participate in switch expressions and pattern combinators.</summary>
    [Fact]
    public void Type_patterns_fold_in_switches_and_combinators()
    {
        Assert.Equal("i", Evaluate("5 switch { int => \"i\", _ => \"o\" }").StringValue);
        Assert.Equal("s", Evaluate("\"x\" switch { int => \"i\", string => \"s\", _ => \"o\" }").StringValue);
        Assert.Equal(true, Evaluate("5 is int and > 3").BooleanValue);
        Assert.Equal(false, Evaluate("5 is double or string").BooleanValue);
    }
}
