using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the Unicode surface: the complete <c>char</c> static API in both the <c>(char)</c> and
/// <c>(string, index)</c> spellings, the char instance members, <c>System.Globalization.CharUnicodeInfo</c>,
/// and <c>System.Text.Rune</c> values with construction, conversions, classification, and ordering — all
/// exact reads of the pinned runtime's Unicode tables.
/// </summary>
public sealed class ConstantExpressionUnicodeTests
{
    private static ConstantExpressionEvaluation Evaluate(string expression) =>
        ConstantExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>Proves the char and CharUnicodeInfo surfaces fold exactly.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("char.GetUnicodeCategory('A') == UnicodeCategory.UppercaseLetter", "True")]
    [InlineData("char.GetUnicodeCategory(\"a5\", 1) == UnicodeCategory.DecimalDigitNumber", "True")]
    [InlineData("char.GetNumericValue('7')", "7")]
    [InlineData("char.GetNumericValue(\"a5b\", 1)", "5")]
    [InlineData("char.IsDigit(\"a5b\", 1)", "True")]
    [InlineData("char.IsLetter(\"a5b\", 0)", "True")]
    [InlineData("char.IsWhiteSpace(\"a b\", 1)", "True")]
    [InlineData("char.ConvertFromUtf32(0x1F600).Length", "2")]
    [InlineData("char.ConvertToUtf32(\"\\uD83D\\uDE00\", 0)", "128512")]
    [InlineData("char.ConvertToUtf32('\\uD83D', '\\uDE00')", "128512")]
    [InlineData("char.IsSurrogatePair('\\uD83D', '\\uDE00')", "True")]
    [InlineData("char.IsSurrogatePair(\"a\\uD83D\\uDE00\", 1)", "True")]
    [InlineData("char.IsBetween('m', 'a', 'z')", "True")]
    [InlineData("char.IsAsciiHexDigitLower('f')", "True")]
    [InlineData("char.IsAsciiHexDigitUpper('F')", "True")]
    [InlineData("char.Parse(\"x\")", "x")]
    [InlineData("char.ToString('y')", "y")]
    [InlineData("'a'.CompareTo('b')", "-1")]
    [InlineData("'a'.Equals('a')", "True")]
    [InlineData("CharUnicodeInfo.GetUnicodeCategory('5') == UnicodeCategory.DecimalDigitNumber", "True")]
    [InlineData("CharUnicodeInfo.GetUnicodeCategory(0x1F600) == UnicodeCategory.OtherSymbol", "True")]
    [InlineData("CharUnicodeInfo.GetDecimalDigitValue('7')", "7")]
    [InlineData("CharUnicodeInfo.GetDecimalDigitValue('a')", "-1")]
    [InlineData("CharUnicodeInfo.GetDigitValue('7')", "7")]
    [InlineData("CharUnicodeInfo.GetNumericValue('\\u00BD')", "0.5")]
    [InlineData("CharUnicodeInfo.GetUnicodeCategory(\"a5\", 1) == UnicodeCategory.DecimalDigitNumber", "True")]
    public void Char_and_unicode_info_fold_exactly(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ConstantValueKind.Char => result.CharValue!.Value.ToString(),
            ConstantValueKind.Numeric => result.ValueText!,
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, Evaluate(expression).Sha256);
    }

    /// <summary>Proves Rune construction, properties, statics, conversions, and ordering fold exactly.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("new Rune('a').Value", "97")]
    [InlineData("new Rune(0x1F600).ToString().Length", "2")]
    [InlineData("new Rune(0x1F600).Utf8SequenceLength", "4")]
    [InlineData("new Rune(0x1F600).Utf16SequenceLength", "2")]
    [InlineData("new Rune(0x1F600).IsBmp", "False")]
    [InlineData("new Rune(0x1F600).Plane", "1")]
    [InlineData("new Rune('a').IsAscii", "True")]
    [InlineData("new Rune('\\uD83D', '\\uDE00').Value", "128512")]
    [InlineData("Rune.GetRuneAt(\"a\\uD83D\\uDE00\", 1).Value == 0x1F600", "True")]
    [InlineData("Rune.IsValid(0x10FFFF)", "True")]
    [InlineData("Rune.IsValid(0x110000)", "False")]
    [InlineData("Rune.IsLetter(new Rune('x'))", "True")]
    [InlineData("Rune.IsDigit(new Rune('5'))", "True")]
    [InlineData("Rune.GetNumericValue(new Rune('7'))", "7")]
    [InlineData("Rune.ToUpperInvariant(new Rune('a')).ToString()", "A")]
    [InlineData("Rune.GetUnicodeCategory(new Rune('!')) == UnicodeCategory.OtherPunctuation", "True")]
    [InlineData("Rune.ReplacementChar.Value", "65533")]
    [InlineData("(Rune)'a' == new Rune(97)", "True")]
    [InlineData("(Rune)0x1F600 != new Rune('a')", "True")]
    [InlineData("(int)new Rune('z')", "122")]
    [InlineData("(char)new Rune(65)", "A")]
    [InlineData("new Rune('a') < new Rune('b')", "True")]
    [InlineData("new Rune('a').CompareTo(new Rune('b'))", "-1")]
    [InlineData("new Rune('a').Equals(new Rune('a'))", "True")]
    [InlineData("\"héllo\".EnumerateRunes().Count()", "5")]
    [InlineData("\"a\\uD83D\\uDE00b\".EnumerateRunes().Count()", "3")]
    [InlineData("\"a\\uD83D\\uDE00b\".EnumerateRunes().Select(r => r.Value).Max()", "128512")]
    [InlineData("\"abc\".EnumerateRunes().All(r => r.IsAscii)", "True")]
    public void Runes_fold_exactly(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ConstantValueKind.Char => result.CharValue!.Value.ToString(),
            ConstantValueKind.Numeric => result.ValueText!,
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>A Rune renders as its character and carries its exact value identity.</summary>
    [Fact]
    public void Runes_render_their_characters()
    {
        var result = Evaluate("new Rune(0x1F600)");
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.BclValue, result.Kind);
        Assert.Equal("Rune", result.ValueTypeName);
        Assert.Equal("\U0001F600", result.ValueText);

        var identity = Evaluate("new Rune('a').GetType() == typeof(Rune)");
        Assert.Equal(ConstantExpressionStatus.Exact, identity.Status);
        Assert.True(identity.BooleanValue);
    }

    /// <summary>Proves the typed stops: invalid scalars, culture-sensitive mappings, and out parameters.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("new Rune(0x110000)", "System.ArgumentOutOfRangeException")]
    [InlineData("new Rune('\\uD83D')", "System.ArgumentOutOfRangeException")]
    [InlineData("(Rune)0xD800", "System.ArgumentOutOfRangeException")]
    [InlineData("Rune.GetRuneAt(\"a\\uD83D\\uDE00\", 2)", "System.ArgumentOutOfRangeException")]
    [InlineData("Rune.ToUpper(new Rune('a'), null)", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("char.ToUpper('a')", "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("char.TryParse(\"x\", null)", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("char.Parse(\"xy\")", "System.FormatException")]
    [InlineData("char.IsDigit(\"abc\", 9)", "System.ArgumentOutOfRangeException")]
    [InlineData("char.GetNumericValue(\"abc\", 9)", "System.ArgumentOutOfRangeException")]
    public void Unicode_stops_are_typed(string expression, string expectedCode)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }
}
