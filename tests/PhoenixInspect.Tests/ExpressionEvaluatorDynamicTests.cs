using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Proves the <c>dynamic</c> surface: a cast to dynamic folds to its untouched operand and every later member
/// dispatch stays late-bound over the runtime kind — the evaluator's native mode — while <c>dynamic[]</c>
/// erases to the per-element-domain object shape and <c>default(dynamic)</c> is null.
/// </summary>
public sealed class ExpressionEvaluatorDynamicTests
{
    /// <summary>A dynamic cast is the identity, and composition continues over the runtime kind.</summary>
    [Theory]
    [InlineData("(dynamic)5 + 2", "7")]
    [InlineData("((dynamic)\"abc\").Length", "3")]
    [InlineData("((dynamic)\"a,b\").Split(',').Length", "2")]
    [InlineData("((dynamic)new[] { 3, 1 }).Max()", "3")]
    [InlineData("(dynamic)(1 + 1) * (dynamic)3", "6")]
    public void Dynamic_casts_fold_to_their_operands(string expression, string expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expected, result.Int32Value?.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Null flows through a dynamic view, and default(dynamic) is the null reference.</summary>
    [Fact]
    public void Dynamic_null_and_default_are_null()
    {
        Assert.Equal(
            ExpressionValueKind.Null, ExpressionEvaluator.Evaluate(session: null, "(dynamic)null").Kind);
        Assert.Equal(
            ExpressionValueKind.Null, ExpressionEvaluator.Evaluate(session: null, "default(dynamic)").Kind);
        Assert.Equal(
            "fallback",
            ExpressionEvaluator.Evaluate(session: null, "(dynamic)null ?? \"fallback\"").StringValue);
    }

    /// <summary>'dynamic[]' erases to the object shape: each element keeps its own folded domain.</summary>
    [Fact]
    public void Dynamic_arrays_keep_per_element_domains()
    {
        var mixed = ExpressionEvaluator.Evaluate(session: null, "new dynamic[] { 1, \"a\", true }.Length");
        Assert.Equal(ExpressionEvaluationStatus.Exact, mixed.Status);
        Assert.Equal(3, mixed.Int32Value);

        Assert.Equal(
            "a", ExpressionEvaluator.Evaluate(session: null, "(string)new dynamic[] { 1, \"a\" }[1]").StringValue);
    }
}
