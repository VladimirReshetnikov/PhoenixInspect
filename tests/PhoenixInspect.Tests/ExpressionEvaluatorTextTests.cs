using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the deterministic text-processing surface: <c>System.Text.Encoding</c> transcoding and the
/// <c>System.Text.RegularExpressions</c> family — construction, matching, groups, captures, collections, their
/// sequence integration, and the typed stops for culture-sensitive case folding, malformed patterns, and the
/// matching budget.
/// </summary>
public sealed class ExpressionEvaluatorTextTests
{
    private static ExpressionEvaluation Evaluate(string expression) =>
        ExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>Proves scalar Encoding and Regex results fold to their exact values.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("Encoding.UTF8.WebName", ExpressionValueKind.String, "utf-8")]
    [InlineData("Encoding.UTF8.CodePage", ExpressionValueKind.Int32, "65001")]
    [InlineData("System.Text.Encoding.Unicode.CodePage", ExpressionValueKind.Int32, "1200")]
    [InlineData("Encoding.GetEncoding(\"utf-8\").CodePage", ExpressionValueKind.Int32, "65001")]
    [InlineData("Encoding.GetEncoding(20127).WebName", ExpressionValueKind.String, "us-ascii")]
    [InlineData("Encoding.Latin1.IsSingleByte", ExpressionValueKind.Boolean, "True")]
    [InlineData("Encoding.UTF8.GetByteCount(\"héllo\")", ExpressionValueKind.Int32, "6")]
    [InlineData("Encoding.Unicode.GetByteCount(\"hi\")", ExpressionValueKind.Int32, "4")]
    [InlineData("Encoding.UTF8.GetString(new byte[] { 72, 105 })", ExpressionValueKind.String, "Hi")]
    [InlineData(
        "Encoding.Unicode.GetString(Encoding.Convert(Encoding.UTF8, Encoding.Unicode, "
        + "Encoding.UTF8.GetBytes(\"réponse\")))",
        ExpressionValueKind.String,
        "réponse")]
    [InlineData("Encoding.UTF8.GetBytes(\"é\").Length", ExpressionValueKind.Int32, "2")]
    [InlineData("Encoding.Unicode.GetPreamble().Length", ExpressionValueKind.Int32, "2")]
    [InlineData("Encoding.UTF8.GetCharCount(new byte[] { 195, 169 })", ExpressionValueKind.Int32, "1")]
    [InlineData("Encoding.ASCII.GetChars(new byte[] { 72 })[0] == 'H'", ExpressionValueKind.Boolean, "True")]
    [InlineData("Encoding.UTF8.GetMaxByteCount(10)", ExpressionValueKind.Int32, "33")]
    [InlineData("Encoding.UTF8 == Encoding.GetEncoding(65001)", ExpressionValueKind.Boolean, "True")]
    [InlineData("Encoding.UTF8 != Encoding.ASCII", ExpressionValueKind.Boolean, "True")]
    [InlineData("Encoding.UTF8.Equals(Encoding.GetEncoding(\"utf-8\"))", ExpressionValueKind.Boolean, "True")]
    [InlineData("Regex.IsMatch(\"abc123\", \"\\\\d+\")", ExpressionValueKind.Boolean, "True")]
    [InlineData("Regex.IsMatch(\"abc\", \"^[0-9]+$\")", ExpressionValueKind.Boolean, "False")]
    [InlineData(
        "System.Text.RegularExpressions.Regex.IsMatch(\"x7\", \"[0-9]\")",
        ExpressionValueKind.Boolean,
        "True")]
    [InlineData("Regex.Match(\"abc123\", \"[0-9]+\").Value", ExpressionValueKind.String, "123")]
    [InlineData("Regex.Match(\"abc123\", \"[0-9]+\").Index", ExpressionValueKind.Int32, "3")]
    [InlineData("Regex.Match(\"abc123\", \"[0-9]+\").Length", ExpressionValueKind.Int32, "3")]
    [InlineData("Regex.Match(\"abc\", \"z\").Success", ExpressionValueKind.Boolean, "False")]
    [InlineData("Regex.Match(\"a1b2\", \"[0-9]\").NextMatch().Value", ExpressionValueKind.String, "2")]
    [InlineData(
        "Regex.Match(\"john@example.com\", \"^(\\\\w+)@\").Result(\"$1\")",
        ExpressionValueKind.String,
        "john")]
    [InlineData("Regex.Matches(\"a1b22c333\", \"[0-9]+\").Count", ExpressionValueKind.Int32, "3")]
    [InlineData("Regex.Matches(\"a1b22c333\", \"[0-9]+\")[2].Value", ExpressionValueKind.String, "333")]
    [InlineData(
        "Regex.Matches(\"a1b22c333\", \"[0-9]+\").Select(m => m.Value).Last()",
        ExpressionValueKind.String,
        "333")]
    [InlineData(
        "Regex.Matches(\"a1b22c333\", \"[0-9]+\").Sum(m => m.Length)",
        ExpressionValueKind.Int32,
        "6")]
    [InlineData("Regex.Replace(\"a1b2\", \"[0-9]\", \"#\")", ExpressionValueKind.String, "a#b#")]
    [InlineData("Regex.Count(\"a1b22c\", \"[0-9]\")", ExpressionValueKind.Int32, "3")]
    [InlineData("Regex.Escape(\"a.b\")", ExpressionValueKind.String, "a\\.b")]
    [InlineData("Regex.Unescape(\"a\\\\.b\")", ExpressionValueKind.String, "a.b")]
    [InlineData("new Regex(\"[a-z]+\").IsMatch(\"abc\")", ExpressionValueKind.Boolean, "True")]
    [InlineData("new Regex(\"[a-z]+\").ToString()", ExpressionValueKind.String, "[a-z]+")]
    [InlineData("new Regex(\"a\").Count(\"banana\")", ExpressionValueKind.Int32, "3")]
    [InlineData(
        "new Regex(\"a\", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).IsMatch(\"A\")",
        ExpressionValueKind.Boolean,
        "True")]
    [InlineData(
        "Regex.IsMatch(\"HELLO\", \"hello\", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)",
        ExpressionValueKind.Boolean,
        "True")]
    [InlineData(
        "new Regex(\"(?<word>[a-z]+)\").Match(\"abc123\").Groups[\"word\"].Value",
        ExpressionValueKind.String,
        "abc")]
    [InlineData(
        "new Regex(\"([a-z])([0-9])\").Match(\"x7\").Groups[2].Value",
        ExpressionValueKind.String,
        "7")]
    [InlineData(
        "new Regex(\"([a-z])\").Match(\"abc\").Groups.Count",
        ExpressionValueKind.Int32,
        "2")]
    [InlineData(
        "new Regex(\"(x)?\").Match(\"y\").Groups[1].Success",
        ExpressionValueKind.Boolean,
        "False")]
    [InlineData(
        "new Regex(\"(a)+\").Match(\"aaa\").Groups[1].Captures.Count",
        ExpressionValueKind.Int32,
        "3")]
    [InlineData(
        "new Regex(\"(a)+\").Match(\"aaa\").Groups[1].Captures[1].Index",
        ExpressionValueKind.Int32,
        "1")]
    [InlineData("new Regex(\"(?<x>a)(b)\").GroupNumberFromName(\"x\")", ExpressionValueKind.Int32, "2")]
    [InlineData("new Regex(\"(?<x>a)(b)\").GetGroupNames().Length", ExpressionValueKind.Int32, "3")]
    [InlineData(
        "new Regex(\"a\", RegexOptions.Multiline).Options == RegexOptions.Multiline",
        ExpressionValueKind.Boolean,
        "True")]
    public void Text_values_fold_exactly(string expression, ExpressionValueKind expectedKind, string expected)
    {
        var result = Evaluate(expression);
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

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, Evaluate(expression).Sha256);
    }

    /// <summary>Proves compound results — byte arrays, splits, encodings, matches — render invariantly.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact value type display.</param>
    /// <param name="expectedText">The invariant rendering.</param>
    [Theory]
    [InlineData("Encoding.UTF8.GetBytes(\"Hi\")", "Byte[]", "{ 72, 105 }")]
    [InlineData("Encoding.UTF8.GetBytes(\"é\")", "Byte[]", "{ 195, 169 }")]
    [InlineData("Encoding.Unicode.GetPreamble()", "Byte[]", "{ 255, 254 }")]
    [InlineData("Regex.Split(\"a1b2c\", \"[0-9]\")", "String[]", "{ \"a\", \"b\", \"c\" }")]
    [InlineData("new Regex(\"(?<x>a)(b)\").GetGroupNames()", "String[]", "{ \"0\", \"1\", \"x\" }")]
    public void Text_sequences_fold_exactly(string expression, string expectedTypeName, string expectedText)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Sequence, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);
    }

    /// <summary>Proves Encoding and Regex values themselves render as their invariant identities.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact value kind.</param>
    /// <param name="expectedText">The invariant text form.</param>
    [Theory]
    [InlineData("Encoding.UTF8", "Encoding", "utf-8")]
    [InlineData("Encoding.GetEncoding(\"latin1\")", "Encoding", "iso-8859-1")]
    [InlineData("new Regex(\"[0-9]+\")", "Regex", "[0-9]+")]
    [InlineData("Regex.Match(\"abc123\", \"[0-9]+\")", "Match", "123")]
    [InlineData("new Regex(\"(?<word>[a-z]+)\").Match(\"abc\").Groups[\"word\"]", "Group", "abc")]
    public void Text_values_render_their_identities(
        string expression,
        string expectedTypeName,
        string expectedText)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.BclValue, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);
    }

    /// <summary>Proves the typed stops: culture-sensitive folding, malformed input, and undefined operators.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData(
        "Regex.IsMatch(\"a\", \"a\", RegexOptions.IgnoreCase)",
        "EVAL_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData(
        "new Regex(\"a\", RegexOptions.IgnoreCase).IsMatch(\"A\")",
        "EVAL_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData("new Regex(\"(\")", "System.Text.RegularExpressions.RegexParseException")]
    [InlineData("Regex.IsMatch(\"a\", \"[\")", "System.Text.RegularExpressions.RegexParseException")]
    [InlineData("Regex.Unescape(\"\\\\x\")", "System.Text.RegularExpressions.RegexParseException")]
    [InlineData("Encoding.GetEncoding(\"no-such-encoding\")", "System.ArgumentException")]
    [InlineData("Encoding.UTF7", "EVAL_MEMBER_UNSUPPORTED")]
    [InlineData("Encoding.UTF8.GetString(new[] { 72, 105 })", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("Encoding.UTF8 + Encoding.ASCII", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("Encoding.UTF8 < Encoding.ASCII", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new Regex(\"a\") == new Regex(\"a\")", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("Regex.Matches(\"aaa\", \"a\")[5].Value", "System.ArgumentOutOfRangeException")]
    [InlineData("Regex.Match(\"a\", \"b\").Result(\"$1\")", "System.NotSupportedException")]
    public void Text_stops_are_typed(string expression, string expectedCode)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>
    /// Proves the matching budget stops a catastrophically backtracking pattern with the timeout named, and that
    /// the same pattern folds exactly under <c>RegexOptions.NonBacktracking</c>.
    /// </summary>
    [Fact]
    public void Catastrophic_backtracking_stops_with_the_budget_named()
    {
        // The trailing 'b' forces the backtracker to try every partition of the a-run before failing.
        var input = new string('a', 40) + "b";
        var result = Evaluate($"Regex.IsMatch(\"{input}\", \"(a+)+$\")");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.Equal("System.Text.RegularExpressions.RegexMatchTimeoutException", result.DiagnosticCode);

        var linear = Evaluate($"Regex.IsMatch(\"{input}\", \"(a+)+$\", RegexOptions.NonBacktracking)");
        Assert.Equal(ExpressionEvaluationStatus.Exact, linear.Status);
        Assert.False(linear.BooleanValue);
    }
}
