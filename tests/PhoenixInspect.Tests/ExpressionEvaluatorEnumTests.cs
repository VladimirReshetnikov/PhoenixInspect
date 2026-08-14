using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the session-free half of the full enum surface: conversions between enums and their underlying types,
/// the flags algebra, enum formatting, <c>typeof</c> references, and the deterministic <c>System.Enum</c> API in
/// its generic and <c>typeof</c> spellings over the closed BCL enum table. Dump-declared enums are proven by the
/// integration lane.
/// </summary>
public sealed class ExpressionEvaluatorEnumTests
{
    /// <summary>Proves conversions and operators that produce enum values name them exactly.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedMember">The exact member display name.</param>
    /// <param name="expectedValue">The exact underlying value.</param>
    [Theory]
    [InlineData("(DayOfWeek)3", "Wednesday", 3)]
    [InlineData("(DayOfWeek)99", "99", 99)]
    [InlineData("(System.DayOfWeek)5", "Friday", 5)]
    [InlineData("(StringSplitOptions)3", "RemoveEmptyEntries, TrimEntries", 3)]
    [InlineData("StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries", "RemoveEmptyEntries, TrimEntries", 3)]
    [InlineData("StringSplitOptions.RemoveEmptyEntries & StringSplitOptions.TrimEntries", "None", 0)]
    [InlineData("StringSplitOptions.RemoveEmptyEntries ^ StringSplitOptions.RemoveEmptyEntries", "None", 0)]
    [InlineData("DayOfWeek.Monday + 2", "Wednesday", 3)]
    [InlineData("2 + DayOfWeek.Monday", "Wednesday", 3)]
    [InlineData("DayOfWeek.Friday - 1", "Thursday", 4)]
    [InlineData("Enum.Parse<DayOfWeek>(\"Friday\")", "Friday", 5)]
    [InlineData("Enum.Parse<DayOfWeek>(\"friday\", true)", "Friday", 5)]
    [InlineData("Enum.Parse<StringSplitOptions>(\"RemoveEmptyEntries, TrimEntries\")", "RemoveEmptyEntries, TrimEntries", 3)]
    [InlineData("Enum.Parse<DayOfWeek>(\"5\")", "Friday", 5)]
    [InlineData("Enum.ToObject(typeof(DayOfWeek), 5)", "Friday", 5)]
    public void Enum_conversions_and_operators_name_values_exactly(
        string expression,
        string expectedMember,
        int expectedValue)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.EnumMember, result.Kind);
        Assert.Equal(expectedMember, result.EnumMemberName);
        Assert.Equal(expectedValue, result.Int32Value);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves enum-consuming operations produce exact scalar results.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected display.</param>
    [Theory]
    [InlineData("(int)DayOfWeek.Friday", ExpressionValueKind.Int32, "5")]
    [InlineData("DayOfWeek.Friday - DayOfWeek.Monday", ExpressionValueKind.Int32, "4")]
    [InlineData("DayOfWeek.Friday.CompareTo(DayOfWeek.Monday)", ExpressionValueKind.Int32, "1")]
    [InlineData("Enum.GetNames<DayOfWeek>().Length", ExpressionValueKind.Int32, "7")]
    [InlineData("Enum.GetNames(typeof(DayOfWeek)).Length", ExpressionValueKind.Int32, "7")]
    [InlineData(
        "Enum.GetValues<DayOfWeek>().Count(d => d > DayOfWeek.Wednesday)",
        ExpressionValueKind.Int32,
        "3")]
    [InlineData("DayOfWeek.Friday.ToString()", ExpressionValueKind.String, "Friday")]
    [InlineData("DayOfWeek.Friday.ToString(\"D\")", ExpressionValueKind.String, "5")]
    [InlineData("DayOfWeek.Friday.ToString(\"X\")", ExpressionValueKind.String, "00000005")]
    [InlineData("$\"day {DayOfWeek.Friday:D}\"", ExpressionValueKind.String, "day 5")]
    [InlineData("Enum.GetName<DayOfWeek>(5)", ExpressionValueKind.String, "Friday")]
    [InlineData("typeof(DayOfWeek).Name", ExpressionValueKind.String, "DayOfWeek")]
    [InlineData("typeof(DayOfWeek).FullName", ExpressionValueKind.String, "System.DayOfWeek")]
    [InlineData("typeof(string).Namespace", ExpressionValueKind.String, "System")]
    [InlineData(
        "(StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)"
        + ".HasFlag(StringSplitOptions.TrimEntries)",
        ExpressionValueKind.Boolean,
        "True")]
    [InlineData("DayOfWeek.Monday.HasFlag(DayOfWeek.Sunday)", ExpressionValueKind.Boolean, "True")]
    [InlineData("Enum.IsDefined<DayOfWeek>(9)", ExpressionValueKind.Boolean, "False")]
    [InlineData("Enum.IsDefined<DayOfWeek>(DayOfWeek.Friday)", ExpressionValueKind.Boolean, "True")]
    [InlineData("Enum.IsDefined<DayOfWeek>(\"Friday\")", ExpressionValueKind.Boolean, "True")]
    [InlineData("Enum.IsDefined(typeof(DayOfWeek), 5)", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(DayOfWeek).IsEnum", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(int).IsEnum", ExpressionValueKind.Boolean, "False")]
    [InlineData("typeof(int) == typeof(int)", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(int) == typeof(long)", ExpressionValueKind.Boolean, "False")]
    [InlineData("typeof(DateTime) != typeof(TimeSpan)", ExpressionValueKind.Boolean, "True")]
    public void Enum_operations_fold_exactly(string expression, ExpressionValueKind expectedKind, string expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedKind, result.Kind);
        var actual = expectedKind switch
        {
            ExpressionValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ExpressionValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>Proves typeof references, GetValues sequences, and GetName misses render honestly.</summary>
    [Fact]
    public void Typeof_and_enum_api_shapes_render_exactly()
    {
        var typeRef = ExpressionEvaluator.Evaluate(session: null, "typeof(DayOfWeek)");
        Assert.Equal(ExpressionEvaluationStatus.Exact, typeRef.Status);
        Assert.Equal(ExpressionValueKind.Type, typeRef.Kind);
        Assert.Equal("typeof(DayOfWeek)", typeRef.ValueText);

        var primitive = ExpressionEvaluator.Evaluate(session: null, "typeof(int)");
        Assert.Equal("typeof(int)", primitive.ValueText);

        var underlying = ExpressionEvaluator.Evaluate(
            session: null, "Enum.GetUnderlyingType(typeof(DayOfWeek))");
        Assert.Equal(ExpressionValueKind.Type, underlying.Kind);
        Assert.Equal("typeof(int)", underlying.ValueText);

        var values = ExpressionEvaluator.Evaluate(session: null, "Enum.GetValues<StringSplitOptions>()");
        Assert.Equal(ExpressionValueKind.Sequence, values.Kind);
        Assert.Equal("StringSplitOptions[]", values.ValueTypeName);
        Assert.Equal("{ None, RemoveEmptyEntries, TrimEntries }", values.ValueText);

        var names = ExpressionEvaluator.Evaluate(session: null, "Enum.GetNames<StringSplitOptions>()");
        Assert.Equal("{ \"None\", \"RemoveEmptyEntries\", \"TrimEntries\" }", names.ValueText);

        var missing = ExpressionEvaluator.Evaluate(session: null, "Enum.GetName<DayOfWeek>(99)");
        Assert.Equal(ExpressionValueKind.Null, missing.Kind);

        var complement = ExpressionEvaluator.Evaluate(session: null, "~StringSplitOptions.None");
        Assert.Equal(ExpressionValueKind.EnumMember, complement.Kind);
        Assert.Equal("-1", complement.EnumMemberName);
    }

    /// <summary>Proves the typed stops: wrong types, unknown members, and the out-parameter boundary.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("DayOfWeek.Friday.HasFlag(StringSplitOptions.TrimEntries)", "System.ArgumentException")]
    [InlineData("DayOfWeek.Friday.CompareTo(StringSplitOptions.TrimEntries)", "System.ArgumentException")]
    [InlineData("Enum.Parse<DayOfWeek>(\"Nope\")", "System.ArgumentException")]
    [InlineData("Enum.TryParse<DayOfWeek>(\"Friday\")", "EVAL_MEMBER_UNSUPPORTED")]
    [InlineData("Enum.GetNames<int>()", "EVAL_MEMBER_UNSUPPORTED")]
    [InlineData("DayOfWeek.Friday.ToString(\"Q\")", "System.FormatException")]
    [InlineData("5 - DayOfWeek.Monday", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("typeof(int) + typeof(int)", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("typeof(System.ConsoleColor).IsEnum", "EVAL_MEMBER_UNSUPPORTED")]
    [InlineData("typeof(System.Collections.ArrayList)", "EVAL_MEMBER_UNSUPPORTED")]
    public void Enum_stops_are_typed(string expression, string expectedCode)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }
}
