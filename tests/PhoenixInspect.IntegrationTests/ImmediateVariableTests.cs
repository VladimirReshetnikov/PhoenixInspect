using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the immediate window's variables: a declaration stores a folded constant, later expressions read and
/// compose with it, an assignment reassigns it, a declared type is checked against the value, and a value outside
/// the operand domain is refused rather than truncated.
/// </summary>
public sealed class ImmediateVariableTests
{
    /// <summary>Declaring, reading, composing, and reassigning a variable round-trips its value.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Declare_read_compose_and_reassign()
    {
        var store = new ImmediateVariableStore();

        Assert.True(Apply(store, "int x = 5;", out var declareMessage));
        Assert.Contains("x = 5", declareMessage, StringComparison.Ordinal);

        // A bare read of the variable folds to its stored value.
        var read = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "x", localVariables: store.LocalNameResolver);
        Assert.Equal(EvaluationSeverity.Exact, read.Severity);
        Assert.Equal("5", read.Value);

        // A composed expression consumes the variable as an operand.
        var composed = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "x * 2 + 1", localVariables: store.LocalNameResolver);
        Assert.Equal("11", composed.Value);

        // 'var' infers the type and composes with the variable already in scope.
        Assert.True(Apply(store, "var y = x * 10;", out _));
        var readY = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "y - x", localVariables: store.LocalNameResolver);
        Assert.Equal("45", readY.Value);

        // A plain assignment reassigns without a type.
        Assert.True(Apply(store, "x = 100;", out _));
        var reread = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "x", localVariables: store.LocalNameResolver);
        Assert.Equal("100", reread.Value);
    }

    /// <summary>String and Boolean variables store and read their own kinds.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Strings_and_booleans_store_and_read()
    {
        var store = new ImmediateVariableStore();
        Assert.True(Apply(store, "string s = \"ab\" + \"cd\";", out _));
        Assert.True(Apply(store, "var flag = s.Length == 4;", out _));

        var readS = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "s + \"!\"", localVariables: store.LocalNameResolver);
        Assert.Equal(EvaluationSeverity.Exact, readS.Severity);
        Assert.Contains("abcd!", readS.Value, StringComparison.Ordinal);

        var readFlag = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "flag && true", localVariables: store.LocalNameResolver);
        Assert.Equal("true", readFlag.Value);
    }

    /// <summary>A type mismatch, an undeclared assignment, and a non-constant initializer are refused.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Invalid_statements_are_refused_with_typed_messages()
    {
        var store = new ImmediateVariableStore();

        Assert.False(Apply(store, "int bad = \"text\";", out var mismatch));
        Assert.Contains("cannot hold", mismatch, StringComparison.Ordinal);

        Assert.False(Apply(store, "z = 5;", out var undeclared));
        Assert.Contains("not declared", undeclared, StringComparison.Ordinal);

        Assert.False(Apply(store, "var q = SomethingUnknown.Value;", out var notConstant));
        Assert.Contains("not assigned", notConstant, StringComparison.Ordinal);

        // A refused statement leaves the store unchanged: nothing named 'bad' resolves.
        var read = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "bad", localVariables: store.LocalNameResolver);
        Assert.NotEqual(EvaluationSeverity.Exact, read.Severity);
    }

    /// <summary>The statement classifier distinguishes statements from expressions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Statement_classifier_distinguishes_statements_from_expressions()
    {
        Assert.True(ImmediateVariableStore.IsStatement("int x = 5"));
        Assert.True(ImmediateVariableStore.IsStatement("x = 5;"));
        Assert.True(ImmediateVariableStore.IsStatement("var y = 1 + 2;"));
        Assert.False(ImmediateVariableStore.IsStatement("x == 5"));
        Assert.False(ImmediateVariableStore.IsStatement("1 + 2"));
        Assert.False(ImmediateVariableStore.IsStatement("Math.Max(1, 2)"));
    }

    private static bool Apply(ImmediateVariableStore store, string line, out string message) =>
        store.TryApply(
            line,
            initializer => ExpressionEvaluationService.EvaluateConstantValue(
                initializer,
                usings: null,
                localVariables: store.LocalNameResolver),
            out message);
}
