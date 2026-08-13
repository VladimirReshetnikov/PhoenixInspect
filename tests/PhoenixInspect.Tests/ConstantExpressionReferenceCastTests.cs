using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes reference conversions: upcasts — including to <c>object</c>, base classes, and implemented
/// interfaces — fold to the same value, downcasts check the operand's exact runtime identity, and an
/// incompatible cast is the runtime's own <c>InvalidCastException</c>. Null flows through reference targets
/// and refuses to unbox into value targets.
/// </summary>
public sealed class ConstantExpressionReferenceCastTests
{
    private static ConstantExpressionEvaluation Evaluate(string expression) =>
        ConstantExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>Proves upcasts and identity downcasts fold to the unchanged value.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("(string) (object) \"abc\"", "abc")]
    [InlineData("(object) 5", "5")]
    [InlineData("(int) (object) 5", "5")]
    [InlineData("((object) \"xyz\").GetType().Name", "String")]
    [InlineData("(IComparable) (object) 5", "5")]
    [InlineData("((object) \"a\" == null)", "False")]
    [InlineData("((Version) (object) null) == null", "True")]
    [InlineData("(string) null == null", "True")]
    [InlineData("((Delegate) (Func<int, int>) (x => x + 1)).HasSingleTarget", "True")]
    [InlineData("((MulticastDelegate) (Action) (() => 0)) != null", "True")]
    [InlineData("((Capture) (object) Regex.Match(\"a1\", \"[0-9]\")).Value", "1")]
    [InlineData("((Group) (object) Regex.Match(\"a1\", \"[0-9]\")).Success", "True")]
    [InlineData("((int[]) (object) new[] { 1, 2 }).Length", "2")]
    [InlineData("((IEnumerable<int>) (object) new[] { 1, 2, 3 }).Length", "3")]
    public void Upcasts_and_matching_downcasts_fold_to_the_same_value(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _ => result.StringValue ?? result.ValueText!,
        };
        Assert.Equal(expected, actual);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, Evaluate(expression).Sha256);
    }

    /// <summary>The goal transcript: a known enum round-trips through object with its member identity.</summary>
    [Fact]
    public void Known_enums_round_trip_through_object()
    {
        var result = Evaluate("(DayOfWeek) (object) DayOfWeek.Friday");
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.EnumMember, result.Kind);
        Assert.Equal("Friday", result.EnumMemberName);
        Assert.Equal(5, result.Int32Value);

        var compared = Evaluate("(DayOfWeek) (object) DayOfWeek.Friday == DayOfWeek.Friday");
        Assert.Equal(ConstantExpressionStatus.Exact, compared.Status);
        Assert.True(compared.BooleanValue);
    }

    /// <summary>Proves BCL value domains round-trip through object unchanged.</summary>
    [Theory]
    [InlineData("(Guid) (object) Guid.Empty", "Guid", "00000000-0000-0000-0000-000000000000")]
    [InlineData("(DateTime) (object) new DateTime(2024, 1, 2)", "DateTime", "2024-01-02T00:00:00.0000000")]
    [InlineData("(Encoding) (object) Encoding.UTF8", "Encoding", "utf-8")]
    [InlineData("(Match) (object) Regex.Match(\"a1\", \"[0-9]\")", "Match", "1")]
    public void Value_domains_round_trip_through_object(
        string expression,
        string expectedTypeName,
        string expectedText)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);
    }

    /// <summary>
    /// Proves incompatible downcasts stop with the runtime's exception, and unboxing null with its own.
    /// </summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("(Guid) (object) \"not-a-guid\"", "System.InvalidCastException")]
    [InlineData("(Regex) (object) 42", "System.InvalidCastException")]
    [InlineData("(Match) (object) Regex.Match(\"a\", \"a\").Groups[0]", "System.InvalidCastException")]
    [InlineData("(int[]) (object) new[] { \"a\" }", "System.InvalidCastException")]
    [InlineData("(Delegate) (object) 5", "System.InvalidCastException")]
    [InlineData("(Guid) (object) null", "System.NullReferenceException")]
    [InlineData("(string) (object) 5", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("(DayOfWeek) (object) \"abc\"", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    public void Incompatible_downcasts_stop_with_the_runtime_exception(string expression, string expectedCode)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>A downcast that narrows within a modeled hierarchy keeps the exact runtime value.</summary>
    [Fact]
    public void Downcasts_within_a_hierarchy_keep_the_runtime_value()
    {
        // Match upcasts to Capture and downcasts back to Match with its groups intact.
        var roundTrip = Evaluate(
            "((Match) (object) Regex.Match(\"a1\", \"([a-z])([0-9])\")).Groups[2].Value");
        Assert.Equal(ConstantExpressionStatus.Exact, roundTrip.Status);
        Assert.Equal("1", roundTrip.StringValue);

        // A Group value is not a Match, so the downcast is the runtime's refusal.
        var refused = Evaluate("(Match) (object) Regex.Match(\"a1\", \"([a-z])\").Groups[1]");
        Assert.Equal(ConstantExpressionStatus.Invalid, refused.Status);
        Assert.Equal("System.InvalidCastException", refused.DiagnosticCode);
    }
}
