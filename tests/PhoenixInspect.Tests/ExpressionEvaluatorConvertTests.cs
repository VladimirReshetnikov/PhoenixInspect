using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the <c>System.Convert</c> surface and <c>DBNull</c>: every conversion calls the BCL's own method
/// over the exact boxed value — banker's rounding, null converting to zero, overflow and invalid-cast
/// refusals — with string parsing and rendering under the invariant culture, plus the base-N spellings, the
/// Base64 and hexadecimal codecs, <c>GetTypeCode</c> with the <c>TypeCode</c> enum, and <c>ChangeType</c>.
/// </summary>
public sealed class ExpressionEvaluatorConvertTests
{
    private static ExpressionEvaluation Evaluate(string expression) =>
        ExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>Proves conversions fold with Convert's exact semantics.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("Convert.ToInt32(3.5)", "4")]
    [InlineData("Convert.ToInt32(2.5)", "2")]
    [InlineData("Convert.ToInt32(\"123\")", "123")]
    [InlineData("Convert.ToInt32(null)", "0")]
    [InlineData("Convert.ToInt32(true)", "1")]
    [InlineData("Convert.ToInt32('A')", "65")]
    [InlineData("Convert.ToInt32(DayOfWeek.Friday)", "5")]
    [InlineData("Convert.ToBoolean(\"True\")", "True")]
    [InlineData("Convert.ToBoolean(0)", "False")]
    [InlineData("Convert.ToChar(65)", "A")]
    [InlineData("Convert.ToDouble(\"2.5\")", "2.5")]
    [InlineData("Convert.ToDecimal(1.5)", "1.5")]
    [InlineData("Convert.ToString(3.5)", "3.5")]
    [InlineData("Convert.ToString(true)", "True")]
    [InlineData("Convert.ToString(null)", "")]
    [InlineData("Convert.ToInt32(\"ff\", 16)", "255")]
    [InlineData("Convert.ToInt32(\"1010\", 2)", "10")]
    [InlineData("Convert.ToInt64(\"777\", 8)", "511")]
    [InlineData("Convert.ToString(255, 2)", "11111111")]
    [InlineData("Convert.ToString(255, 16)", "ff")]
    [InlineData("Convert.ToBase64String(Encoding.UTF8.GetBytes(\"Hi\"))", "SGk=")]
    [InlineData("Encoding.UTF8.GetString(Convert.FromBase64String(\"SGk=\"))", "Hi")]
    [InlineData("Convert.ToHexString(new byte[] { 255, 0, 171 })", "FF00AB")]
    [InlineData("Convert.ToHexStringLower(new byte[] { 255 })", "ff")]
    [InlineData("Convert.FromHexString(\"FF00AB\").Length", "3")]
    [InlineData("Convert.ChangeType(3.7, typeof(int))", "4")]
    [InlineData("Convert.ChangeType(\"42\", typeof(double))", "42")]
    [InlineData("Convert.ToDateTime(new DateTime(2024, 1, 2)).Year", "2024")]
    [InlineData("((Func<double, int>)Convert.ToInt32)(9.5)", "10")]
    public void Conversions_fold_with_convert_semantics(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ExpressionValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ExpressionValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ExpressionValueKind.Char => result.CharValue!.Value.ToString(),
            ExpressionValueKind.Numeric => result.ValueText!,
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, Evaluate(expression).Sha256);
    }

    /// <summary>Proves the type-code queries and the DBNull singleton semantics.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The expected Boolean or string display.</param>
    [Theory]
    [InlineData("Convert.GetTypeCode(5) == TypeCode.Int32", "True")]
    [InlineData("Convert.GetTypeCode(\"x\") == TypeCode.String", "True")]
    [InlineData("Convert.GetTypeCode(1.5) == TypeCode.Double", "True")]
    [InlineData("Convert.GetTypeCode(null) == TypeCode.Empty", "True")]
    [InlineData("Convert.GetTypeCode(new DateTime(2024, 1, 1)) == TypeCode.DateTime", "True")]
    [InlineData("Convert.GetTypeCode(DBNull.Value) == TypeCode.DBNull", "True")]
    [InlineData("Convert.IsDBNull(DBNull.Value)", "True")]
    [InlineData("Convert.IsDBNull(5)", "False")]
    [InlineData("Convert.IsDBNull(null)", "False")]
    [InlineData("Convert.DBNull == DBNull.Value", "True")]
    [InlineData("DBNull.Value.ToString()", "")]
    [InlineData("DBNull.Value.GetTypeCode() == TypeCode.DBNull", "True")]
    [InlineData("DBNull.Value.Equals(DBNull.Value)", "True")]
    [InlineData("DBNull.Value.Equals(5)", "False")]
    [InlineData("DBNull.Value.GetType() == typeof(DBNull)", "True")]
    public void Type_codes_and_dbnull_fold_exactly(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ExpressionValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>Proves the typed stops: overflow, malformed input, DBNull casts, and culture-sensitive parses.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("Convert.ToByte(300)", "System.OverflowException")]
    [InlineData("Convert.ToInt32(\"not-a-number\")", "System.FormatException")]
    [InlineData("Convert.ToInt32(DBNull.Value)", "System.InvalidCastException")]
    [InlineData("Convert.ToChar(\"ab\")", "System.FormatException")]
    [InlineData("Convert.ToInt32(\"12\", 3)", "System.ArgumentException")]
    [InlineData("Convert.FromBase64String(\"!!!\")", "System.FormatException")]
    [InlineData("Convert.FromHexString(\"XYZ\")", "System.FormatException")]
    [InlineData("Convert.ToDateTime(\"2024-01-01\")", "EVAL_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("Convert.ToInt32(Guid.Empty)", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("Convert.ChangeType(5, typeof(Guid))", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    public void Convert_stops_are_typed(string expression, string expectedCode)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }
}
