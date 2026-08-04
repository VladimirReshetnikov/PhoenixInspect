using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes C# query expressions — translated by the specification's own rules onto the folded operator surface —
/// and the anonymous-type value domain that serves as their transparent identifiers: every clause of the query
/// grammar, the multi-lambda operators the translation needs, and anonymous creation, member access, equality,
/// and rendering.
/// </summary>
public sealed class ConstantExpressionQueryTests
{
    /// <summary>Proves each query clause produces exactly what its method-syntax translation produces.</summary>
    /// <param name="expression">The query expression.</param>
    /// <param name="expectedText">The rendered result.</param>
    [Theory]
    [InlineData("from x in new[] { 1, 2, 3 } select x", "{ 1, 2, 3 }")]
    [InlineData("from x in new[] { 1, 2, 3 } where x > 1 select x * 10", "{ 20, 30 }")]
    [InlineData("from x in new[] { 1, 2, 3 } let y = x * x where y > 4 select y + x", "{ 12 }")]
    [InlineData("from x in new[] { 1, 2 } from y in new[] { 10, 20 } select x * y", "{ 10, 20, 20, 40 }")]
    [InlineData(
        "from x in new[] { 1, 2, 3 } from y in Enumerable.Range(0, x) select x * 10 + y",
        "{ 10, 20, 21, 30, 31, 32 }")]
    [InlineData("from x in new[] { 13, 1, 22, 11, 2 } orderby x % 10, x descending select x", "{ 11, 1, 22, 2, 13 }")]
    [InlineData("from x in new[] { 3, 1, 2 } orderby x descending select x * 10", "{ 30, 20, 10 }")]
    [InlineData(
        "from x in new[] { 1, 2, 3, 4 } group x by x % 2 into g select g.Key * 100 + g.Count()",
        "{ 102, 2 }")]
    [InlineData(
        "from x in new[] { 1, 2, 3, 4, 5 } group x * 10 by x % 2 into g orderby g.Key select g.Sum()",
        "{ 60, 90 }")]
    [InlineData(
        "from x in new[] { 1, 2, 3 } join y in new[] { 10, 20, 21, 30 } on x equals y / 10 select x * 100 + y",
        "{ 110, 220, 221, 330 }")]
    [InlineData(
        "from x in new[] { 1, 2, 3 } join y in new[] { 10, 20, 21 } on x equals y / 10 into g "
        + "select x * 10 + g.Count()",
        "{ 11, 22, 30 }")]
    [InlineData("from x in new[] { 1, 2, 3 } select x * 2 into y where y > 2 select y + 1", "{ 5, 7 }")]
    [InlineData("from double d in new[] { 1, 2 } select d / 2", "{ 0.5, 1 }")]
    [InlineData(
        "from x in new[] { 1, 2 } let s = new[] { x, x * 10 } select s.Sum() + new[] { x }.Select(x => x).First()",
        "{ 12, 24 }")]
    [InlineData("(from x in new[] { 5, 6, 7 } where x != 6 select x).Sum()", "12")]
    [InlineData(
        "from w in \"the quick brown fox\".Split(' ') orderby w.Length select w.Length",
        "{ 3, 3, 5, 5 }")]
    public void Query_expressions_translate_and_fold_exactly(string expression, string expectedText)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Sequence => result.ValueText!,
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _ => result.ValueText ?? result.StringValue!,
        };
        Assert.Equal(expectedText, actual);
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves anonymous types fold with C#'s member, equality, and rendering semantics.</summary>
    /// <param name="expression">The expression over anonymous objects.</param>
    /// <param name="expectedKind">The expected result domain.</param>
    /// <param name="expected">The expected display.</param>
    [Theory]
    [InlineData("new { a = 1, b = \"x\" }.a", ConstantValueKind.Int32, "1")]
    [InlineData("new { a = 1, b = \"x\" }.b", ConstantValueKind.String, "x")]
    [InlineData("new { DayOfWeek.Monday }.Monday", ConstantValueKind.EnumMember, "Monday")]
    [InlineData("new { a = 1 }.Equals(new { a = 1 })", ConstantValueKind.Boolean, "True")]
    [InlineData("new { a = 1 }.Equals(new { a = 2 })", ConstantValueKind.Boolean, "False")]
    [InlineData("new { a = 1 }.Equals(new { b = 1 })", ConstantValueKind.Boolean, "False")]
    [InlineData("new { a = 1 }.Equals(new { a = 1, b = 2 })", ConstantValueKind.Boolean, "False")]
    [InlineData("new { a = 1, b = \"x\" }.ToString()", ConstantValueKind.String, "{ a = 1, b = x }")]
    [InlineData("$\"{new { a = 1 }}\"", ConstantValueKind.String, "{ a = 1 }")]
    [InlineData("new { Total = new[] { 1, 2 }.Sum(), Pair = (1, 2) }.Pair.Item2", ConstantValueKind.Int32, "2")]
    public void Anonymous_types_follow_csharp_semantics(
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
            ConstantValueKind.EnumMember => result.EnumMemberName!,
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>Proves anonymous values render with shape names and expose structured children.</summary>
    [Fact]
    public void Anonymous_values_render_and_expand()
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, "new { Name = \"a\", Total = 3 }");
        Assert.Equal(ConstantValueKind.Anonymous, result.Kind);
        Assert.Equal("new { Name, Total }", result.ValueTypeName);
        Assert.Equal("{ Name = \"a\", Total = 3 }", result.ValueText);
        Assert.Equal(
            [("Name", "\"a\""), ("Total", "3")],
            result.Children.Select(static child => (child.Name, child.ValueText)).ToArray());

        // Queries selecting anonymous shapes produce sequences of them, expandable per element.
        var sequence = ConstantExpressionEvaluator.Evaluate(
            session: null,
            "from x in new[] { 1, 2 } select new { x, Square = x * x }");
        Assert.Equal(ConstantValueKind.Sequence, sequence.Kind);
        Assert.Equal("new { x, Square }[]", sequence.ValueTypeName);
        Assert.Equal("{ { x = 1, Square = 1 }, { x = 2, Square = 4 } }", sequence.ValueText);
        Assert.Equal("Square", sequence.Children[1].Children[1].Name);
        Assert.Equal("4", sequence.Children[1].Children[1].ValueText);
    }

    /// <summary>Proves groupings carry keys and members, and behave as sequences.</summary>
    [Fact]
    public void Groupings_expose_keys_and_act_as_sequences()
    {
        var groups = ConstantExpressionEvaluator.Evaluate(
            session: null,
            "new[] { 1, 2, 3, 4, 5 }.GroupBy(x => x % 2)");
        Assert.Equal(ConstantValueKind.Sequence, groups.Kind);
        Assert.Equal("IGrouping[]", groups.ValueTypeName);
        Assert.Equal("{ [1] { 1, 3, 5 }, [0] { 2, 4 } }", groups.ValueText);
        Assert.Equal("Key", groups.Children[0].Children[0].Name);

        var first = ConstantExpressionEvaluator.Evaluate(
            session: null,
            "new[] { 1, 2, 3 }.GroupBy(x => x % 2).First().Sum()");
        Assert.Equal(4, first.Int32Value);

        var nested = ConstantExpressionEvaluator.Evaluate(
            session: null,
            "from g in new[] { 1, 2, 3, 4 }.GroupBy(x => x % 2) from x in g select g.Key * 10 + x");
        Assert.Equal("{ 11, 13, 2, 4 }", nested.ValueText);
    }

    /// <summary>Proves the typed stops: reference equality, bad member names, and unordered ThenBy.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("new { a = 1 } == new { a = 1 }", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new { 1 * 2 }", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new { a = 1, a = 2 }", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new[] { 1, 2 }.ThenBy(x => x)", "CONSTANT_LAMBDA_UNSUPPORTED")]
    public void Query_and_anonymous_stops_are_typed(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
    }
}
