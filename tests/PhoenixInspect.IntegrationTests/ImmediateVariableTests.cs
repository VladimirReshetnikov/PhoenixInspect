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

    /// <summary>Byte arrays store as variables and compose with the Encoding surface.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Byte_arrays_store_and_round_trip_through_encoding()
    {
        var store = new ImmediateVariableStore();

        Assert.True(Apply(store, "var b = Encoding.UTF8.GetBytes(\"Hi\");", out var message));
        Assert.Contains("Byte[]", message, StringComparison.Ordinal);
        Assert.Contains("{ 72, 105 }", message, StringComparison.Ordinal);

        // The stored bytes decode back to the original text, and compose elementwise.
        var decoded = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "Encoding.UTF8.GetString(b)", localVariables: store.LocalNameResolver);
        Assert.Equal(EvaluationSeverity.Exact, decoded.Severity);
        Assert.Contains("Hi", decoded.Value, StringComparison.Ordinal);

        var first = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "b[0]", localVariables: store.LocalNameResolver);
        Assert.Equal("72", first.Value);

        var summed = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "b.Sum(x => x)", localVariables: store.LocalNameResolver);
        Assert.Equal("177", summed.Value);

        // A declared byte[] type accepts the value; a mismatched declared type is refused.
        Assert.True(Apply(store, "byte[] preamble = Encoding.Unicode.GetPreamble();", out _));
        Assert.False(Apply(store, "int[] wrong = Encoding.UTF8.GetBytes(\"x\");", out var mismatch));
        Assert.Contains("cannot hold", mismatch, StringComparison.Ordinal);
    }

    /// <summary>Temporal and BCL values — dates, Guids, Versions, Regexes — store and compose.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Temporal_and_bcl_values_store_and_compose()
    {
        var store = new ImmediateVariableStore();

        Assert.True(Apply(store, "var t = new DateTime(2024, 1, 2);", out var temporalMessage));
        Assert.Contains("DateTime", temporalMessage, StringComparison.Ordinal);
        var year = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "t.Year + t.Day", localVariables: store.LocalNameResolver);
        Assert.Equal("2026", year.Value);

        Assert.True(Apply(store, "var g = Guid.Parse(\"8aff0911-ac7f-439b-a7c1-1f038caf3ba1\");", out _));
        var guidVersion = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "g.Version", localVariables: store.LocalNameResolver);
        Assert.Equal("4", guidVersion.Value);
        var guidEquality = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "g == Guid.Parse(\"8AFF0911-AC7F-439B-A7C1-1F038CAF3BA1\")",
            localVariables: store.LocalNameResolver);
        Assert.Equal("true", guidEquality.Value);

        Assert.True(Apply(store, "Version v = Version.Parse(\"1.5\");", out _));
        var comparison = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "v < new Version(1, 10)", localVariables: store.LocalNameResolver);
        Assert.Equal("true", comparison.Value);

        Assert.True(Apply(store, "var re = new Regex(\"[0-9]+\");", out var regexMessage));
        Assert.Contains("Regex", regexMessage, StringComparison.Ordinal);
        var matched = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "re.Match(\"abc123\").Value", localVariables: store.LocalNameResolver);
        Assert.Contains("123", matched.Value, StringComparison.Ordinal);

        Assert.True(Apply(store, "var enc = Encoding.GetEncoding(\"us-ascii\");", out _));
        var codePage = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "enc.CodePage", localVariables: store.LocalNameResolver);
        Assert.Equal("20127", codePage.Value);

        // A stored match keeps its groups readable.
        Assert.True(Apply(store, "var m = new Regex(\"(?<word>[a-z]+)\").Match(\"abc123\");", out _));
        var group = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "m.Groups[\"word\"].Value", localVariables: store.LocalNameResolver);
        Assert.Contains("abc", group.Value, StringComparison.Ordinal);
    }

    /// <summary>Decimals, wide integers, and their sequences store exactly.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Decimals_and_wide_numerics_store_exactly()
    {
        var store = new ImmediateVariableStore();

        Assert.True(Apply(store, "decimal d = 1.10m;", out var decimalMessage));
        Assert.Contains("Decimal", decimalMessage, StringComparison.Ordinal);
        var scaled = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "d * 3", localVariables: store.LocalNameResolver);
        Assert.Equal("3.30", scaled.Value);

        Assert.True(Apply(store, "var wide = UInt128.MaxValue;", out _));
        var read = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "wide", localVariables: store.LocalNameResolver);
        Assert.Equal("340282366920938463463374607431768211455", read.Value);

        Assert.True(Apply(store, "var xs = new[] { 1.5m, 2.5m };", out _));
        var sum = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "xs.Sum()", localVariables: store.LocalNameResolver);
        Assert.Equal("4.0", sum.Value);

        // Values with no operand carrier keep their typed refusal.
        Assert.False(Apply(store, "var t = (1, 2);", out var tuple));
        Assert.Contains("cannot be stored", tuple, StringComparison.Ordinal);
    }

    /// <summary>Delegates store as variables: a typed lambda declaration converts, invokes, and combines.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Delegates_store_and_invoke_as_variables()
    {
        var store = new ImmediateVariableStore();

        // A typed declaration supplies the lambda's target type, exactly as C# conversion does.
        Assert.True(Apply(store, "Func<int, int> f = x => x + 1;", out var message));
        Assert.Contains("Func<int, int>", message, StringComparison.Ordinal);

        var invoked = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "f(41)", localVariables: store.LocalNameResolver);
        Assert.Equal(EvaluationSeverity.Exact, invoked.Severity);
        Assert.Equal("42", invoked.Value);

        var throughInvoke = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "f.Invoke(1) + f(2)", localVariables: store.LocalNameResolver);
        Assert.Equal("5", throughInvoke.Value);

        // A stored delegate equals itself, and combination through variables keeps entry identity.
        Assert.True(Apply(store, "var g = f + f;", out _));
        var combined = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "g.GetInvocationList().Length", localVariables: store.LocalNameResolver);
        Assert.Equal("2", combined.Value);
        var removed = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "(g - f).HasSingleTarget", localVariables: store.LocalNameResolver);
        Assert.Equal("true", removed.Value);
        var equal = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "f == f", localVariables: store.LocalNameResolver);
        Assert.Equal("true", equal.Value);

        // A method-group delegate keeps its runtime method identity through storage.
        Assert.True(Apply(store, "var sqrt = new Func<double, double>(Math.Sqrt);", out _));
        var methodName = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "sqrt.Method.Name", localVariables: store.LocalNameResolver);
        Assert.Contains("Sqrt", methodName.Value, StringComparison.Ordinal);

        // A curried delegate declares, partially applies through a variable, and keeps its closure.
        Assert.True(Apply(store, "Func<int, Func<int, int>> curried = x => y => x + y;", out var curriedMessage));
        Assert.Contains("Func<int, Func<int, int>>", curriedMessage, StringComparison.Ordinal);
        var curriedResult = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "curried(5)(6)", localVariables: store.LocalNameResolver);
        Assert.Equal(EvaluationSeverity.Exact, curriedResult.Severity);
        Assert.Equal("11", curriedResult.Value);

        Assert.True(Apply(store, "var addFive = curried(5);", out _));
        var partial = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "addFive(37)", localVariables: store.LocalNameResolver);
        Assert.Equal("42", partial.Value);

        // A self-referential declaration recurses through the store's late-bound name resolution.
        Assert.True(Apply(store, "Func<int, int> fact = n => n <= 1 ? 1 : n * fact(n - 1);", out _));
        var recursed = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "fact(10)", localVariables: store.LocalNameResolver);
        Assert.Equal(EvaluationSeverity.Exact, recursed.Severity);
        Assert.Equal("3628800", recursed.Value);
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

    /// <summary>A dynamic declaration admits any value, rebinding late over the stored runtime kind.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Dynamic_declarations_admit_any_value_and_bind_late()
    {
        var store = new ImmediateVariableStore();

        // 'dynamic' admits any storable value without a declared-type check.
        Assert.True(Apply(store, "dynamic d = 5;", out var declared));
        Assert.Contains("d = 5", declared, StringComparison.Ordinal);
        Assert.Equal(
            "7",
            ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "d + 2", localVariables: store.LocalNameResolver).Value);

        // Reassignment may change the runtime kind, and member dispatch follows the new value.
        Assert.True(Apply(store, "d = \"text\";", out _));
        Assert.Equal(
            "4",
            ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "d.Length", localVariables: store.LocalNameResolver).Value);

        // A lambda still has no value without a target type, exactly as C# refuses the same declaration.
        Assert.False(Apply(store, "dynamic f = x => x + 1;", out var lambdaMessage));
        Assert.Contains("not assigned", lambdaMessage, StringComparison.Ordinal);
    }

    private static bool Apply(ImmediateVariableStore store, string line, out string message) =>
        store.TryApply(
            line,
            initializer => ExpressionEvaluationService.EvaluateValue(
                initializer,
                usings: null,
                localVariables: store.LocalNameResolver),
            out message);
}
