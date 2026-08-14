using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the expression-lambda sequence surface: <c>Select</c> and <c>Where</c> (with and without the index
/// parameter), predicated element operators, selector aggregates, key-ordered sorts, the materializing identities,
/// and the typed stops for lambda shapes the evaluator deliberately does not model.
/// </summary>
public sealed class ExpressionEvaluatorLambdaTests
{
    /// <summary>Proves lambda operators that produce sequences render the exact expected elements.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact element domain of the produced sequence.</param>
    /// <param name="expectedText">The rendered sequence.</param>
    [Theory]
    [InlineData("new[] { 1, 2, 3 }.Select(x => x * 2)", "Int32[]", "{ 2, 4, 6 }")]
    [InlineData("new[] { 1, 2, 3 }.Select(x => x * 2).ToArray()", "Int32[]", "{ 2, 4, 6 }")]
    [InlineData("new[] { 1, 2, 3 }.Select(x => x * 2).ToList()", "Int32[]", "{ 2, 4, 6 }")]
    [InlineData("new[] { 1, 2, 3 }.Where(x => x > 1)", "Int32[]", "{ 2, 3 }")]
    [InlineData("new[] { 1, 2, 3 }.Select((x, i) => x + i)", "Int32[]", "{ 1, 3, 5 }")]
    [InlineData("new[] { 5, 6, 7 }.Where((x, i) => i != 1)", "Int32[]", "{ 5, 7 }")]
    [InlineData("\"a,bb,ccc\".Split(',').Select(s => s.Length)", "Int32[]", "{ 1, 2, 3 }")]
    [InlineData("new[] { 1, 2 }.Select(x => $\"n{x}\")", "String[]", "{ \"n1\", \"n2\" }")]
    [InlineData("new[] { 3, 1, 2 }.OrderBy(x => x)", "Int32[]", "{ 1, 2, 3 }")]
    [InlineData("new[] { 3, 1, 2 }.OrderBy(x => -x)", "Int32[]", "{ 3, 2, 1 }")]
    [InlineData("new[] { 3, 1, 2 }.OrderByDescending(x => x)", "Int32[]", "{ 3, 2, 1 }")]
    [InlineData("new[] { \"bb\", \"a\" }.OrderBy(s => s.Length)", "String[]", "{ \"a\", \"bb\" }")]
    [InlineData("new[] { 1, 2, 3, 1 }.TakeWhile(x => x < 3)", "Int32[]", "{ 1, 2 }")]
    [InlineData("new[] { 1, 2, 3, 1 }.SkipWhile(x => x < 3)", "Int32[]", "{ 3, 1 }")]
    [InlineData("new[] { 1, 2, 3 }.Select(x => x * 1.5)", "Double[]", "{ 1.5, 3, 4.5 }")]
    [InlineData("new[] { 1, 2, 3, 4 }.Where(x => x % 2 == 0).Select(x => x * x)", "Int32[]", "{ 4, 16 }")]
    [InlineData("System.Linq.Enumerable.Select(new[] { 1, 2 }, x => x + 1)", "Int32[]", "{ 2, 3 }")]
    [InlineData("Enumerable.Range(1, 5).Where(x => x % 2 == 1)", "Int32[]", "{ 1, 3, 5 }")]
    [InlineData("new[] { 1, 2 }.Select(x => x switch { 1 => \"one\", _ => \"more\" })", "String[]", "{ \"one\", \"more\" }")]
    [InlineData("new[] { 1, -2, 3 }.Select(x => Math.Abs(x)).Where(x => x is > 1)", "Int32[]", "{ 2, 3 }")]
    public void Lambda_operators_produce_exact_sequences(
        string expression,
        string expectedTypeName,
        string expectedText)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Sequence, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves lambda operators that produce scalars fold to the exact Int32 values.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The exact Int32 value real Enumerable semantics produce.</param>
    [Theory]
    [InlineData("new[] { 1, 2, 3 }.Count(x => x % 2 == 1)", 2)]
    [InlineData("new[] { 1, 2, 3 }.First(x => x > 1)", 2)]
    [InlineData("new[] { 1, 2, 3 }.FirstOrDefault(x => x > 9)", 0)]
    [InlineData("new[] { 1, 2, 3 }.Last(x => x < 3)", 2)]
    [InlineData("new[] { 1, 2, 3 }.LastOrDefault(x => x > 9)", 0)]
    [InlineData("new[] { 1, 2, 3 }.Single(x => x == 2)", 2)]
    [InlineData("new[] { 1, 2, 3 }.Sum(x => x * x)", 14)]
    [InlineData("new[] { 1, 2, 3 }.Max(x => -x)", -1)]
    [InlineData("new[] { 1, 2, 3 }.Min(x => 10 - x)", 7)]
    [InlineData("Enumerable.Range(1, 5).Where(x => x % 2 == 1).Select(x => x * x).Sum()", 35)]
    [InlineData("\"batch-2026-07-30\".Split('-').Count(part => part.Length == 2)", 2)]
    public void Lambda_operators_produce_exact_scalars(string expression, int expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Int32, result.Kind);
        Assert.Equal(expected, result.Int32Value);
    }

    /// <summary>Proves lambda predicates fold to exact Booleans.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The exact Boolean.</param>
    [Theory]
    [InlineData("new[] { 1, 2, 3 }.Any(x => x > 2)", true)]
    [InlineData("new[] { 1, 2, 3 }.Any(x => x > 9)", false)]
    [InlineData("new[] { 1, 2, 3 }.All(x => x > 0)", true)]
    [InlineData("new[] { 1, 2, 3 }.All(x => x > 1)", false)]
    [InlineData("new[] { \"a\", \"ab\" }.All(s => s.StartsWith('a'))", true)]
    public void Lambda_predicates_fold_to_exact_booleans(string expression, bool expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.BooleanValue);
    }

    /// <summary>Proves the typed stops for lambda shapes and uses the evaluator does not model.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("new[] { 1 }.Select(x => { return x; })", "EVAL_LAMBDA_UNSUPPORTED")]
    [InlineData("new[] { 1 }.Select(static x => x)", "EVAL_LAMBDA_UNSUPPORTED")]
    [InlineData("new[] { 1 }.Sum((x, i) => x + i)", "EVAL_LAMBDA_UNSUPPORTED")]
    [InlineData("new[] { 1 }.Aggregate((a, b) => a + b)", "EVAL_LAMBDA_UNSUPPORTED")]
    [InlineData("\"abc\".Select(c => c)", "EVAL_LAMBDA_UNSUPPORTED")]
    [InlineData("new[] { 1, 2 }.Where(x => x + 1)", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new[] { 1, 2 }.First(x => x > 5)", "System.InvalidOperationException")]
    [InlineData("new[] { 1, 1 }.Single(x => x == 1)", "System.InvalidOperationException")]
    [InlineData("new[] { \"b\", \"a\" }.OrderBy(s => s)", "EVAL_CULTURE_SENSITIVE_UNSUPPORTED")]
    public void Lambda_stops_are_typed(string expression, string expectedCode)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>
    /// Proves scoping is lexical: parameters shadow outer names inside the body and vanish outside it, and an
    /// unbound name inside a lambda keeps the whole expression on its existing not-constant path.
    /// </summary>
    [Fact]
    public void Lambda_parameters_scope_lexically()
    {
        var nested = ExpressionEvaluator.Evaluate(
            session: null,
            "new[] { 1, 2 }.Select(x => new[] { 10, 20 }.Select(y => x * y).Sum()).ToArray()");
        Assert.Equal(ExpressionEvaluationStatus.Exact, nested.Status);
        Assert.Equal("{ 30, 60 }", nested.ValueText);

        var shadowed = ExpressionEvaluator.Evaluate(
            session: null,
            "new[] { 1, 2 }.Select(x => new[] { 5 }.Select(x => x).Sum() + x).ToArray()");
        Assert.Equal(ExpressionEvaluationStatus.Exact, shadowed.Status);
        Assert.Equal("{ 6, 7 }", shadowed.ValueText);

        var unbound = ExpressionEvaluator.Evaluate(session: null, "new[] { 1 }.Select(x => y)");
        Assert.Equal(ExpressionEvaluationStatus.NotFolded, unbound.Status);

        var outsideScope = ExpressionEvaluator.Evaluate(session: null, "new[] { 1 }.Select(x => x).Sum() + x");
        Assert.Equal(ExpressionEvaluationStatus.NotFolded, outsideScope.Status);
    }
}
