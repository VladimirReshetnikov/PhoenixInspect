using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the deterministic BCL value surface: <c>System.Guid</c> and <c>System.Version</c> construction,
/// fixed-grammar parsing, members, comparisons, invariant formatting, sequence integration, and the typed stop
/// for <c>Guid.NewGuid</c>, whose result is not evidence.
/// </summary>
public sealed class ConstantExpressionBclValueTests
{
    /// <summary>Proves Guid and Version values fold to their exact invariant forms.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact value kind.</param>
    /// <param name="expectedText">The invariant text form.</param>
    [Theory]
    [InlineData(
        "new Guid(\"8AFF0911-AC7F-439B-A7C1-1F038CAF3BA1\")",
        "Guid",
        "8aff0911-ac7f-439b-a7c1-1f038caf3ba1")]
    [InlineData(
        "Guid.Parse(\"{8aff0911-ac7f-439b-a7c1-1f038caf3ba1}\")",
        "Guid",
        "8aff0911-ac7f-439b-a7c1-1f038caf3ba1")]
    [InlineData(
        "Guid.ParseExact(\"8aff0911ac7f439ba7c11f038caf3ba1\", \"N\")",
        "Guid",
        "8aff0911-ac7f-439b-a7c1-1f038caf3ba1")]
    [InlineData("Guid.Empty", "Guid", "00000000-0000-0000-0000-000000000000")]
    [InlineData("new Version(10, 0)", "Version", "10.0")]
    [InlineData("new Version(10, 0, 26300)", "Version", "10.0.26300")]
    [InlineData("new Version(\"5.3.0.1\")", "Version", "5.3.0.1")]
    [InlineData("Version.Parse(\"1.2.3\")", "Version", "1.2.3")]
    [InlineData("new[] { \"2.0\", \"1.5\" }.Select(v => Version.Parse(v)).Min()", "Version", "1.5")]
    public void Bcl_values_fold_exactly(string expression, string expectedTypeName, string expectedText)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.BclValue, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves comparisons, members, formatting, and sequence composition.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData(
        "new Guid(\"8AFF0911-AC7F-439B-A7C1-1F038CAF3BA1\") == "
        + "Guid.Parse(\"8aff0911-ac7f-439b-a7c1-1f038caf3ba1\")",
        ConstantValueKind.Boolean,
        "True")]
    [InlineData("Guid.Empty == Guid.Empty", ConstantValueKind.Boolean, "True")]
    [InlineData("new Version(1, 5) < new Version(1, 10)", ConstantValueKind.Boolean, "True")]
    [InlineData("Version.Parse(\"5.3.0\") >= new Version(5, 3)", ConstantValueKind.Boolean, "True")]
    [InlineData("new Version(10, 0, 26300).Build", ConstantValueKind.Int32, "26300")]
    [InlineData("new Version(10, 0).Revision", ConstantValueKind.Int32, "-1")]
    [InlineData("Guid.Empty.Version", ConstantValueKind.Int32, "0")]
    [InlineData(
        "new Guid(\"8aff0911-ac7f-439b-a7c1-1f038caf3ba1\").ToString(\"N\")",
        ConstantValueKind.String,
        "8aff0911ac7f439ba7c11f038caf3ba1")]
    [InlineData("new Version(5, 3, 0, 1).ToString(2)", ConstantValueKind.String, "5.3")]
    [InlineData(
        "$\"module {Guid.Parse(\"8aff0911-ac7f-439b-a7c1-1f038caf3ba1\"):B}\"",
        ConstantValueKind.String,
        "module {8aff0911-ac7f-439b-a7c1-1f038caf3ba1}")]
    [InlineData(
        "new[] { \"1.0\", \"2.0\", \"1.0\" }.Select(v => Version.Parse(v)).Distinct().Count()",
        ConstantValueKind.Int32,
        "2")]
    [InlineData(
        "new[] { \"2.0\", \"1.5\", \"1.10\" }.Select(v => Version.Parse(v)).OrderBy(v => v).First().ToString()",
        ConstantValueKind.String,
        "1.5")]
    public void Bcl_value_operations_fold_exactly(
        string expression,
        ConstantValueKind expectedKind,
        string expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(expectedKind, result.Kind);
        var actual = expectedKind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>Proves the typed stops: non-determinism, malformed input, and undefined operators.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("Guid.NewGuid()", "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData("new Guid(\"not-a-guid\")", "System.FormatException")]
    [InlineData("Version.Parse(\"one.two\")", "System.FormatException")]
    [InlineData("new Version(1, -2)", "System.ArgumentOutOfRangeException")]
    [InlineData("Guid.Empty + Guid.Empty", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("Guid.Empty == new Version(1, 0)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("Guid.Empty == 5", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    public void Bcl_value_stops_are_typed(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }
}
