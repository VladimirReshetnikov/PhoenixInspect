using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the session-free half of the full enum surface: conversions between enums and their underlying types,
/// the flags algebra, enum formatting, <c>typeof</c> references, and the deterministic <c>System.Enum</c> API in
/// its generic and <c>typeof</c> spellings over the closed BCL enum table. Dump-declared enums are proven by the
/// integration lane.
/// </summary>
public sealed class ConstantExpressionEnumTests
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
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.EnumMember, result.Kind);
        Assert.Equal(expectedMember, result.EnumMemberName);
        Assert.Equal(expectedValue, result.Int32Value);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves enum-consuming operations produce exact scalar results.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected display.</param>
    [Theory]
    [InlineData("(int)DayOfWeek.Friday", ConstantValueKind.Int32, "5")]
    [InlineData("DayOfWeek.Friday - DayOfWeek.Monday", ConstantValueKind.Int32, "4")]
    [InlineData("DayOfWeek.Friday.CompareTo(DayOfWeek.Monday)", ConstantValueKind.Int32, "1")]
    [InlineData("Enum.GetNames<DayOfWeek>().Length", ConstantValueKind.Int32, "7")]
    [InlineData("Enum.GetNames(typeof(DayOfWeek)).Length", ConstantValueKind.Int32, "7")]
    [InlineData(
        "Enum.GetValues<DayOfWeek>().Count(d => d > DayOfWeek.Wednesday)",
        ConstantValueKind.Int32,
        "3")]
    [InlineData("DayOfWeek.Friday.ToString()", ConstantValueKind.String, "Friday")]
    [InlineData("DayOfWeek.Friday.ToString(\"D\")", ConstantValueKind.String, "5")]
    [InlineData("DayOfWeek.Friday.ToString(\"X\")", ConstantValueKind.String, "00000005")]
    [InlineData("$\"day {DayOfWeek.Friday:D}\"", ConstantValueKind.String, "day 5")]
    [InlineData("Enum.GetName<DayOfWeek>(5)", ConstantValueKind.String, "Friday")]
    [InlineData("typeof(DayOfWeek).Name", ConstantValueKind.String, "DayOfWeek")]
    [InlineData("typeof(DayOfWeek).FullName", ConstantValueKind.String, "System.DayOfWeek")]
    [InlineData("typeof(string).Namespace", ConstantValueKind.String, "System")]
    [InlineData(
        "(StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)"
        + ".HasFlag(StringSplitOptions.TrimEntries)",
        ConstantValueKind.Boolean,
        "True")]
    [InlineData("DayOfWeek.Monday.HasFlag(DayOfWeek.Sunday)", ConstantValueKind.Boolean, "True")]
    [InlineData("Enum.IsDefined<DayOfWeek>(9)", ConstantValueKind.Boolean, "False")]
    [InlineData("Enum.IsDefined<DayOfWeek>(DayOfWeek.Friday)", ConstantValueKind.Boolean, "True")]
    [InlineData("Enum.IsDefined<DayOfWeek>(\"Friday\")", ConstantValueKind.Boolean, "True")]
    [InlineData("Enum.IsDefined(typeof(DayOfWeek), 5)", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(DayOfWeek).IsEnum", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(int).IsEnum", ConstantValueKind.Boolean, "False")]
    [InlineData("typeof(int) == typeof(int)", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(int) == typeof(long)", ConstantValueKind.Boolean, "False")]
    [InlineData("typeof(DateTime) != typeof(TimeSpan)", ConstantValueKind.Boolean, "True")]
    public void Enum_operations_fold_exactly(string expression, ConstantValueKind expectedKind, string expected)
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

    /// <summary>Proves typeof references, GetValues sequences, and GetName misses render honestly.</summary>
    [Fact]
    public void Typeof_and_enum_api_shapes_render_exactly()
    {
        var typeRef = ConstantExpressionEvaluator.Evaluate(session: null, "typeof(DayOfWeek)");
        Assert.Equal(ConstantExpressionStatus.Exact, typeRef.Status);
        Assert.Equal(ConstantValueKind.Type, typeRef.Kind);
        Assert.Equal("typeof(DayOfWeek)", typeRef.ValueText);

        var primitive = ConstantExpressionEvaluator.Evaluate(session: null, "typeof(int)");
        Assert.Equal("typeof(int)", primitive.ValueText);

        var underlying = ConstantExpressionEvaluator.Evaluate(
            session: null, "Enum.GetUnderlyingType(typeof(DayOfWeek))");
        Assert.Equal(ConstantValueKind.Type, underlying.Kind);
        Assert.Equal("typeof(int)", underlying.ValueText);

        var values = ConstantExpressionEvaluator.Evaluate(session: null, "Enum.GetValues<StringSplitOptions>()");
        Assert.Equal(ConstantValueKind.Sequence, values.Kind);
        Assert.Equal("StringSplitOptions[]", values.ValueTypeName);
        Assert.Equal("{ None, RemoveEmptyEntries, TrimEntries }", values.ValueText);

        var names = ConstantExpressionEvaluator.Evaluate(session: null, "Enum.GetNames<StringSplitOptions>()");
        Assert.Equal("{ \"None\", \"RemoveEmptyEntries\", \"TrimEntries\" }", names.ValueText);

        var missing = ConstantExpressionEvaluator.Evaluate(session: null, "Enum.GetName<DayOfWeek>(99)");
        Assert.Equal(ConstantValueKind.Null, missing.Kind);

        var complement = ConstantExpressionEvaluator.Evaluate(session: null, "~StringSplitOptions.None");
        Assert.Equal(ConstantValueKind.EnumMember, complement.Kind);
        Assert.Equal("-1", complement.EnumMemberName);
    }

    /// <summary>Proves the typed stops: wrong types, unknown members, and the out-parameter boundary.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("DayOfWeek.Friday.HasFlag(StringSplitOptions.TrimEntries)", "System.ArgumentException")]
    [InlineData("DayOfWeek.Friday.CompareTo(StringSplitOptions.TrimEntries)", "System.ArgumentException")]
    [InlineData("Enum.Parse<DayOfWeek>(\"Nope\")", "System.ArgumentException")]
    [InlineData("Enum.TryParse<DayOfWeek>(\"Friday\")", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("Enum.GetNames<int>()", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("DayOfWeek.Friday.ToString(\"Q\")", "System.FormatException")]
    [InlineData("5 - DayOfWeek.Monday", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("typeof(int) + typeof(int)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("typeof(System.Collections.ArrayList)", "CONSTANT_MEMBER_UNSUPPORTED")]
    public void Enum_stops_are_typed(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }
}
