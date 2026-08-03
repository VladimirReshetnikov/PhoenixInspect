using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the tuple value domain: literal folding with optional element names, positional and named element
/// access, C#'s element-wise equality, invariant rendering, and the structured children that make a tuple
/// expandable in a watch grid.
/// </summary>
public sealed class ConstantExpressionTupleTests
{
    /// <summary>Proves tuple literals fold with exact shapes and display forms.</summary>
    /// <param name="expression">The tuple expression.</param>
    /// <param name="expectedTypeName">The expected shape name.</param>
    /// <param name="expectedText">The expected display rendering.</param>
    [Theory]
    [InlineData("(1, 2)", "(Int32, Int32)", "(1, 2)")]
    [InlineData("(1, \"a\")", "(Int32, String)", "(1, \"a\")")]
    [InlineData("(count: 1, name: \"x\")", "(Int32, String)", "(1, \"x\")")]
    [InlineData("((1, 2), 3)", "((Int32, Int32), Int32)", "((1, 2), 3)")]
    [InlineData("(new[] { 1, 2 }, true)", "(Int32[], Boolean)", "({ 1, 2 }, true)")]
    [InlineData("(1.5, 'c')", "(Double, Char)", "(1.5, 'c')")]
    public void Tuple_literals_fold_exactly(string expression, string expectedTypeName, string expectedText)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Tuple, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves element access, equality, ToString, and interpolation follow C# semantics.</summary>
    /// <param name="expression">The expression over tuples.</param>
    /// <param name="expectedKind">The expected result domain.</param>
    /// <param name="expected">The expected display.</param>
    [Theory]
    [InlineData("(1, \"a\").Item1", ConstantValueKind.Int32, "1")]
    [InlineData("(1, \"a\").Item2", ConstantValueKind.String, "a")]
    [InlineData("(count: 5, name: \"x\").count", ConstantValueKind.Int32, "5")]
    [InlineData("(count: 5, name: \"x\").Item2", ConstantValueKind.String, "x")]
    [InlineData("((1, 2), 3).Item1.Item2", ConstantValueKind.Int32, "2")]
    [InlineData("(1, 2) == (1, 2)", ConstantValueKind.Boolean, "True")]
    [InlineData("(1, 2) == (1, 3)", ConstantValueKind.Boolean, "False")]
    [InlineData("(1, 2) != (1, 3)", ConstantValueKind.Boolean, "True")]
    [InlineData("(1, 2L) == (1, 2)", ConstantValueKind.Boolean, "True")]
    [InlineData("((1, 2), \"a\") == ((1, 2), \"a\")", ConstantValueKind.Boolean, "True")]
    [InlineData("(1, \"a\").Equals((1, \"a\"))", ConstantValueKind.Boolean, "True")]
    [InlineData("(1, \"a\").ToString()", ConstantValueKind.String, "(1, a)")]
    [InlineData("((1, 2), 3).ToString()", ConstantValueKind.String, "((1, 2), 3)")]
    [InlineData("$\"pair {(1, 2)}\"", ConstantValueKind.String, "pair (1, 2)")]
    [InlineData("(1, 2).Item1 + (3, 4).Item2", ConstantValueKind.Int32, "5")]
    public void Tuple_operations_follow_csharp_semantics(
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

    /// <summary>Proves the typed stops: missing elements, arity mismatch, and non-equality operators.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    [Theory]
    [InlineData("(1, 2).Item3")]
    [InlineData("(1, 2) == (1, 2, 3)")]
    [InlineData("(1, 2) + (3, 4)")]
    [InlineData("(1, 2) < (3, 4)")]
    public void Tuple_stops_are_typed(string expression)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal("CONSTANT_OPERAND_TYPE_UNSUPPORTED", result.DiagnosticCode);
    }

    /// <summary>Proves compound values expose structured children for expansion, recursively.</summary>
    [Fact]
    public void Compound_values_expose_structured_children()
    {
        var tuple = ConstantExpressionEvaluator.Evaluate(session: null, "(1, \"a\", (2, 3))");
        Assert.Equal(3, tuple.Children.Length);
        Assert.Equal(("Item1", "1", "Int32"), (tuple.Children[0].Name, tuple.Children[0].ValueText, tuple.Children[0].ValueTypeName));
        Assert.Equal(("Item2", "\"a\"", "String"), (tuple.Children[1].Name, tuple.Children[1].ValueText, tuple.Children[1].ValueTypeName));
        Assert.Equal(("Item3", "(2, 3)", "(Int32, Int32)"), (tuple.Children[2].Name, tuple.Children[2].ValueText, tuple.Children[2].ValueTypeName));
        Assert.Equal(2, tuple.Children[2].Children.Length);
        Assert.Equal("Item2", tuple.Children[2].Children[1].Name);
        Assert.Equal("3", tuple.Children[2].Children[1].ValueText);

        var named = ConstantExpressionEvaluator.Evaluate(session: null, "(count: 7, name: \"x\")");
        Assert.Equal(["count", "name"], named.Children.Select(static child => child.Name).ToArray());

        var sequence = ConstantExpressionEvaluator.Evaluate(session: null, "new[] { 10, 20, 30 }");
        Assert.Equal(3, sequence.Children.Length);
        Assert.Equal(("[0]", "10", "Int32"), (sequence.Children[0].Name, sequence.Children[0].ValueText, sequence.Children[0].ValueTypeName));
        Assert.Equal("[2]", sequence.Children[2].Name);
        Assert.Empty(sequence.Children[0].Children);

        var scalar = ConstantExpressionEvaluator.Evaluate(session: null, "1 + 2");
        Assert.Empty(scalar.Children);

        // A sequence beyond the realization bound gains one honest tail row instead of silently truncating.
        var bounded = ConstantExpressionEvaluator.Evaluate(session: null, "Enumerable.Range(0, 600)");
        Assert.Equal(513, bounded.Children.Length);
        Assert.Equal("…", bounded.Children[^1].Name);
        Assert.Contains("88 more", bounded.Children[^1].ValueText, StringComparison.Ordinal);
    }
}
