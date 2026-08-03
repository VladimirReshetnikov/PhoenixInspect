using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the date and time surface of the constant-expression domain: construction, deterministic factories,
/// calendar and clock members, the operator algebra, invariant formatting, sequence integration, and the typed
/// stops for everything that would read the analysis machine's clock, time zone, or culture.
/// </summary>
public sealed class ConstantExpressionTemporalTests
{
    /// <summary>Proves temporal constructions and operators produce exact round-trip values.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact temporal kind.</param>
    /// <param name="expectedText">The invariant round-trip text.</param>
    [Theory]
    [InlineData("new DateTime(2026, 7, 30)", "DateTime", "2026-07-30T00:00:00.0000000")]
    [InlineData("new DateTime(2026, 7, 30, 13, 45, 30)", "DateTime", "2026-07-30T13:45:30.0000000")]
    [InlineData("new System.DateTime(638000000000000000L)", "DateTime", "2022-09-28T22:13:20.0000000")]
    [InlineData("new TimeSpan(1, 2, 3)", "TimeSpan", "01:02:03")]
    [InlineData("new TimeSpan(2, 3, 4, 5)", "TimeSpan", "2.03:04:05")]
    [InlineData("TimeSpan.FromSeconds(90)", "TimeSpan", "00:01:30")]
    [InlineData("TimeSpan.Zero", "TimeSpan", "00:00:00")]
    [InlineData("new DateTime(2026, 7, 30) + TimeSpan.FromHours(6)", "DateTime", "2026-07-30T06:00:00.0000000")]
    [InlineData("new DateTime(2026, 8, 1) - new DateTime(2026, 7, 30)", "TimeSpan", "2.00:00:00")]
    [InlineData("TimeSpan.FromMinutes(90) * 2", "TimeSpan", "03:00:00")]
    [InlineData("2 * TimeSpan.FromMinutes(90)", "TimeSpan", "03:00:00")]
    [InlineData("TimeSpan.FromHours(1) / 4", "TimeSpan", "00:15:00")]
    [InlineData("TimeSpan.FromMinutes(90) - TimeSpan.FromMinutes(30)", "TimeSpan", "01:00:00")]
    [InlineData("new DateOnly(2026, 7, 30)", "DateOnly", "2026-07-30")]
    [InlineData("new DateOnly(2026, 7, 30).AddDays(2)", "DateOnly", "2026-08-01")]
    [InlineData("new TimeOnly(13, 45)", "TimeOnly", "13:45:00.0000000")]
    [InlineData("new TimeOnly(13, 45) - new TimeOnly(12, 15)", "TimeSpan", "01:30:00")]
    [InlineData("DateTimeOffset.FromUnixTimeSeconds(1753833600)", "DateTimeOffset", "2025-07-30T00:00:00.0000000+00:00")]
    [InlineData("DateTimeOffset.UnixEpoch", "DateTimeOffset", "1970-01-01T00:00:00.0000000+00:00")]
    [InlineData("new DateTime(2026, 7, 30).AddMonths(1).AddDays(-1)", "DateTime", "2026-08-29T00:00:00.0000000")]
    [InlineData("new DateOnly(2026, 7, 30).ToDateTime(new TimeOnly(6, 30))", "DateTime", "2026-07-30T06:30:00.0000000")]
    [InlineData("TimeSpan.FromMilliseconds(30045).Duration()", "TimeSpan", "00:00:30.0450000")]
    public void Temporal_values_fold_exactly(string expression, string expectedTypeName, string expectedText)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Temporal, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves calendar and clock components produce exact Int32 values.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The exact component value.</param>
    [Theory]
    [InlineData("new DateTime(2026, 7, 30).Year", 2026)]
    [InlineData("new DateTime(2026, 7, 30).DayOfYear", 211)]
    [InlineData("new DateTime(2026, 7, 30, 13, 45, 30).Hour", 13)]
    [InlineData("new TimeSpan(1, 2, 3).Minutes", 2)]
    [InlineData("(new DateTime(2026, 8, 1) - new DateTime(2026, 7, 30)).Days", 2)]
    [InlineData("new DateOnly(2026, 7, 30).Day", 30)]
    [InlineData("new TimeOnly(13, 45).Hour", 13)]
    public void Temporal_components_fold_to_exact_int32(string expression, int expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Int32, result.Kind);
        Assert.Equal(expected, result.Int32Value);
    }

    /// <summary>Proves the wider-domain members keep their exact numeric kinds and values.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="typeName">The exact numeric kind name.</param>
    /// <param name="text">The invariant value text.</param>
    [Theory]
    [InlineData("TimeSpan.FromSeconds(90).TotalMinutes", "Double", "1.5")]
    [InlineData("TimeSpan.FromHours(2) / TimeSpan.FromMinutes(30)", "Double", "4")]
    [InlineData("DateTimeOffset.FromUnixTimeSeconds(1753833600).ToUnixTimeSeconds()", "Int64", "1753833600")]
    [InlineData("TimeSpan.TicksPerSecond", "Int64", "10000000")]
    [InlineData("TimeSpan.FromTicks(TimeSpan.TicksPerDay).TotalDays", "Double", "1")]
    public void Temporal_numeric_members_keep_their_kind(string expression, string typeName, string text)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Numeric, result.Kind);
        Assert.Equal(typeName, result.ValueTypeName);
        Assert.Equal(text, result.ValueText);
    }

    /// <summary>Proves temporal comparisons and pattern-style logic fold to exact Booleans.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The exact Boolean.</param>
    [Theory]
    [InlineData("new DateTime(2026, 7, 30) < new DateTime(2026, 8, 1)", true)]
    [InlineData("new TimeSpan(1, 0, 0) == TimeSpan.FromMinutes(60)", true)]
    [InlineData("new DateOnly(2026, 7, 30) >= DateOnly.MinValue", true)]
    [InlineData("new DateTime(2026, 7, 30).DayOfWeek == DayOfWeek.Thursday", true)]
    [InlineData("TimeSpan.FromSeconds(30) > TimeSpan.FromSeconds(29)", true)]
    [InlineData("new TimeOnly(13, 45) != new TimeOnly(13, 46)", true)]
    public void Temporal_comparisons_fold_to_exact_booleans(string expression, bool expected)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.BooleanValue);
    }

    /// <summary>Proves invariant formatting, interpolation, and enum-valued members.</summary>
    [Fact]
    public void Temporal_formatting_is_invariant()
    {
        var formatted = ConstantExpressionEvaluator.Evaluate(
            session: null, "new DateTime(2026, 7, 30).ToString(\"yyyy-MM-dd\")");
        Assert.Equal(ConstantExpressionStatus.Exact, formatted.Status);
        Assert.Equal("2026-07-30", formatted.StringValue);

        var interpolated = ConstantExpressionEvaluator.Evaluate(
            session: null, "$\"stalled for {TimeSpan.FromMilliseconds(30045):c}\"");
        Assert.Equal(ConstantExpressionStatus.Exact, interpolated.Status);
        Assert.Equal("stalled for 00:00:30.0450000", interpolated.StringValue);

        var dayOfWeek = ConstantExpressionEvaluator.Evaluate(
            session: null, "new DateTime(2026, 7, 30).AddDays(2).DayOfWeek");
        Assert.Equal(ConstantExpressionStatus.Exact, dayOfWeek.Status);
        Assert.Equal(ConstantValueKind.EnumMember, dayOfWeek.Kind);
        Assert.Equal("Saturday", dayOfWeek.EnumMemberName);
    }

    /// <summary>Proves temporal values compose with sequences and lambdas.</summary>
    [Fact]
    public void Temporal_values_compose_with_sequences_and_lambdas()
    {
        var projected = ConstantExpressionEvaluator.Evaluate(
            session: null, "new[] { 1200, 30045 }.Select(ms => TimeSpan.FromMilliseconds(ms))");
        Assert.Equal(ConstantExpressionStatus.Exact, projected.Status);
        Assert.Equal("TimeSpan[]", projected.ValueTypeName);
        Assert.Equal("{ 00:00:01.2000000, 00:00:30.0450000 }", projected.ValueText);

        var maximum = ConstantExpressionEvaluator.Evaluate(
            session: null, "new[] { 3, 1, 2 }.Select(d => new DateTime(2026, 7, d)).Max()");
        Assert.Equal(ConstantExpressionStatus.Exact, maximum.Status);
        Assert.Equal(ConstantValueKind.Temporal, maximum.Kind);
        Assert.Equal("2026-07-03T00:00:00.0000000", maximum.ValueText);

        var ordered = ConstantExpressionEvaluator.Evaluate(
            session: null, "new[] { 2, 1 }.OrderBy(d => new DateTime(2026, 7, d))");
        Assert.Equal(ConstantExpressionStatus.Exact, ordered.Status);
        Assert.Equal("{ 1, 2 }", ordered.ValueText);

        var thresholds = ConstantExpressionEvaluator.Evaluate(
            session: null,
            "new[] { 1200, 30045 }.Count(ms => TimeSpan.FromMilliseconds(ms) > TimeSpan.FromSeconds(2))");
        Assert.Equal(ConstantExpressionStatus.Exact, thresholds.Status);
        Assert.Equal(1, thresholds.Int32Value);
    }

    /// <summary>Proves the typed stops for clocks, time zones, culture, and invalid arguments.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("DateTime.Now", "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData("DateTime.UtcNow", "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData("DateTime.Today", "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData("DateTimeOffset.Now", "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData("new DateTime(2026, 7, 30).ToLocalTime()", "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData("new DateTime(2026, 7, 30).ToUniversalTime()", "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData("DateTime.Parse(\"2026-07-30\")", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("new DateTime(2026, 13, 1)", "System.ArgumentOutOfRangeException")]
    [InlineData("DateTime.MaxValue + TimeSpan.FromDays(1)", "System.OverflowException")]
    [InlineData("new TimeSpan(1, 0, 0) + 5", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new DateTime(2026, 7, 30) + new DateTime(2026, 7, 30)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new DateTime(2026, 7, 30) == new DateOnly(2026, 7, 30)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    public void Temporal_stops_are_typed(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>Proves object creations outside the temporal set stay on their existing not-constant path.</summary>
    [Theory]
    [InlineData("new SomeType(1)")]
    [InlineData("new System.Text.StringBuilder()")]
    public void Other_object_creations_stay_not_constant(string expression)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.NotConstant, result.Status);
    }
}
