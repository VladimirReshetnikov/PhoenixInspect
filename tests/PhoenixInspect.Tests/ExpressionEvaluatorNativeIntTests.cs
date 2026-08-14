using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Proves the native integer surface: <c>nint</c>/<c>nuint</c> and their BCL spellings fold at 64 bits —
/// matching the x64 processes the preview targets, with the visible kind stating the assumption — across
/// statics, casts, checked arithmetic, and <c>default</c>/<c>sizeof</c>.
/// </summary>
public sealed class ExpressionEvaluatorNativeIntTests
{
    /// <summary>The static surface answers under both keyword and BCL spellings.</summary>
    [Theory]
    [InlineData("nint.MaxValue", "9223372036854775807", "IntPtr")]
    [InlineData("nint.MinValue", "-9223372036854775808", "IntPtr")]
    [InlineData("IntPtr.Zero", "0", "IntPtr")]
    [InlineData("System.IntPtr.MaxValue", "9223372036854775807", "IntPtr")]
    [InlineData("nuint.MaxValue", "18446744073709551615", "UIntPtr")]
    [InlineData("UIntPtr.Zero", "0", "UIntPtr")]
    [InlineData("default(nint)", "0", "IntPtr")]
    [InlineData("default(nuint)", "0", "UIntPtr")]
    public void Native_int_statics_fold(string expression, string expectedValue, string expectedType)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedValue, result.ValueText);
        Assert.Equal(expectedType, result.StoredValueTypeName);
    }

    /// <summary>Size answers 8 as an Int32, the evaluator's stated 64-bit assumption.</summary>
    [Theory]
    [InlineData("nint.Size")]
    [InlineData("nuint.Size")]
    [InlineData("IntPtr.Size")]
    [InlineData("UIntPtr.Size")]
    [InlineData("sizeof(nint)")]
    [InlineData("sizeof(nuint)")]
    public void Native_int_size_answers_eight(string expression)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(8, result.Int32Value);
    }

    /// <summary>Casts and arithmetic fold with C#'s promotion and checked semantics at 64 bits.</summary>
    [Theory]
    [InlineData("(nint)5 + 3", "8", "IntPtr")]
    [InlineData("(nint)(-70000) / 7", "-10000", "IntPtr")]
    [InlineData("(nuint)10 % (nuint)3", "1", "UIntPtr")]
    [InlineData("(int)(nint)70000", "70000", "Int32")]
    [InlineData("(nint)1 << 40", "1099511627776", "IntPtr")]
    public void Native_int_casts_and_arithmetic_fold(string expression, string expectedValue, string expectedType)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedValue, result.ValueText ?? result.Int32Value?.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(expectedType, result.StoredValueTypeName);
    }

    /// <summary>Checked overflow and narrowing keep their typed stops over the native kinds.</summary>
    [Theory]
    [InlineData("nint.MaxValue + (nint)1")]
    [InlineData("checked((ushort)(nint)70000)")]
    [InlineData("(nuint)0 - (nuint)1")]
    public void Native_int_overflow_stays_a_typed_stop(string expression)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.NotNull(result.DiagnosticCode);
    }
}
