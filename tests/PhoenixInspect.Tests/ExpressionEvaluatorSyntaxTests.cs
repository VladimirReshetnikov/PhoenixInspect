using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the widened syntax surface of the constant-expression domain: interpolated strings, <c>is</c> patterns,
/// <c>switch</c> expressions, <c>nameof</c>, <c>default(T)</c>, <c>sizeof(T)</c>, <c>checked</c>/<c>unchecked</c>
/// wrappers, and the null-forgiving operator, together with their typed stops and the not-constant dispositions
/// that keep unresolved names on their existing evaluation paths.
/// </summary>
public sealed class ExpressionEvaluatorSyntaxTests
{
    /// <summary>Proves the new syntax forms that produce exact Int32 values.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The exact Int32 value C# semantics produce.</param>
    [Theory]
    [InlineData("sizeof(byte)", 1)]
    [InlineData("sizeof(bool)", 1)]
    [InlineData("sizeof(char)", 2)]
    [InlineData("sizeof(int)", 4)]
    [InlineData("sizeof(float)", 4)]
    [InlineData("sizeof(long)", 8)]
    [InlineData("sizeof(double)", 8)]
    [InlineData("sizeof(nint)", 8)]
    [InlineData("sizeof(decimal)", 16)]
    [InlineData("default(int)", 0)]
    [InlineData("default(int) + 5", 5)]
    [InlineData("checked(2 + 3)", 5)]
    [InlineData("unchecked(2 + 3)", 5)]
    [InlineData("((int?)5)! + 1", 6)]
    [InlineData("5 switch { > 3 => 1, _ => 0 }", 1)]
    [InlineData("2 switch { 1 or 2 => 10, _ => 0 }", 10)]
    [InlineData("5 switch { > 3 and < 10 => 1, _ => 0 }", 1)]
    [InlineData("5 switch { > 3 when false => 1, _ => 9 }", 9)]
    [InlineData("\"b\" switch { \"a\" => 1, \"b\" => 2, _ => 0 }", 2)]
    [InlineData("((int?)null) switch { null => 7, _ => 0 }", 7)]
    [InlineData("'z' switch { >= 'a' and <= 'z' => 1, _ => 0 }", 1)]
    [InlineData("$\"{7:D3}\".Length", 3)]
    public void Widened_syntax_folds_to_exact_int32(string expression, int expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Int32, result.Kind);
        Assert.Equal(expected, result.Int32Value);
        Assert.Null(result.DiagnosticCode);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves interpolated strings and nameof render exactly under the invariant culture.</summary>
    /// <param name="expression">The constant expression producing a string.</param>
    /// <param name="expected">The exact string value.</param>
    [Theory]
    [InlineData("$\"a{1 + 2}b\"", "a3b")]
    [InlineData("$\"{'x'}{true}{null}\"", "xTrue")]
    [InlineData("$\"{\"s\"}!\"", "s!")]
    [InlineData("$\"{3.14159:F2}\"", "3.14")]
    [InlineData("$\"{42,5}\"", "   42")]
    [InlineData("$\"{42,-5}!\"", "42   !")]
    [InlineData("$\"{255:X4}\"", "00FF")]
    [InlineData("$\"queue depth {17 * 2}\"", "queue depth 34")]
    [InlineData("$\"{1 switch { 1 => \"one\", _ => \"other\" }}\"", "one")]
    [InlineData("$\"{System.StringComparison.Ordinal}\"", "Ordinal")]
    [InlineData("nameof(x)", "x")]
    [InlineData("nameof(System.String)", "String")]
    [InlineData("nameof(root.CurrentBatch.BatchId)", "BatchId")]
    [InlineData("nameof(int.MaxValue)", "MaxValue")]
    [InlineData("5 switch { > 3 => \"high\", _ => \"low\" }", "high")]
    public void Widened_syntax_folds_to_exact_strings(string expression, string expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.String, result.Kind);
        Assert.Equal(expected, result.StringValue);
    }

    /// <summary>Proves is-pattern expressions fold to exact Booleans.</summary>
    /// <param name="expression">The pattern expression to fold.</param>
    /// <param name="expected">The exact Boolean C# pattern semantics produce.</param>
    [Theory]
    [InlineData("5 is > 3", true)]
    [InlineData("5 is < 3", false)]
    [InlineData("5 is >= 5 and <= 5", true)]
    [InlineData("5 is 4 or 5", true)]
    [InlineData("5 is not 4", true)]
    [InlineData("5 is not (4 or 5)", false)]
    [InlineData("\"abc\" is \"abc\"", true)]
    [InlineData("\"abc\" is not \"abd\"", true)]
    [InlineData("((int?)null) is null", true)]
    [InlineData("((int?)null) is > 5", false)]
    [InlineData("default(string) is null", true)]
    [InlineData("((int?)5) is not null", true)]
    [InlineData("'c' is >= 'a' and <= 'z'", true)]
    [InlineData("2.5 is > 2 and < 3", true)]
    [InlineData("true is true", true)]
    public void Is_patterns_fold_to_exact_booleans(string expression, bool expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.BooleanValue);
    }

    /// <summary>Proves default expressions of nullable and reference targets produce exact null.</summary>
    /// <param name="expression">The constant expression producing null.</param>
    [Theory]
    [InlineData("default(string)")]
    [InlineData("default(int?)")]
    [InlineData("default(double?)")]
    public void Default_of_nullable_and_reference_targets_is_exact_null(string expression)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Null, result.Kind);
    }

    /// <summary>Proves defaults of non-Int32 numeric targets keep their exact numeric kind.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="typeName">The exact numeric kind name.</param>
    /// <param name="text">The invariant value text.</param>
    [Theory]
    [InlineData("default(double)", "Double", "0")]
    [InlineData("default(decimal)", "Decimal", "0")]
    [InlineData("default(long) + 1L", "Int64", "1")]
    public void Default_of_numeric_targets_keeps_the_exact_kind(string expression, string typeName, string text)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Numeric, result.Kind);
        Assert.Equal(typeName, result.ValueTypeName);
        Assert.Equal(text, result.ValueText);
    }

    /// <summary>Proves the typed stops the widened forms report instead of guessing.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("5 switch { > 10 => 1 }", "System.Runtime.CompilerServices.SwitchExpressionException")]
    [InlineData("unchecked(int.MaxValue + 1)", "EVAL_UNCHECKED_UNSUPPORTED")]
    [InlineData("checked(int.MaxValue + 1)", "System.OverflowException")]
    [InlineData("1 switch { var x => 2 }", "EVAL_PATTERN_UNSUPPORTED")]
    [InlineData("\"a\" switch { string s => 1, _ => 0 }", "EVAL_PATTERN_UNSUPPORTED")]
    [InlineData("\"a\" is > 3", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("5 is \"a\"", "EVAL_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("$\"{3:Z}\"", "System.FormatException")]
    public void Widened_syntax_reports_typed_stops(string expression, string expectedCode)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>
    /// Proves an interpolation whose value is an unresolved name stays not-constant, so the frozen pipelines keep
    /// answering — and rejecting — those names with their own complete evidence.
    /// </summary>
    [Theory]
    [InlineData("$\"{Some.Unknown}\"")]
    [InlineData("$\"{unknownName}\"")]
    [InlineData("unknownName switch { 1 => 2, _ => 0 }")]
    [InlineData("5 is UnknownConst")]
    public void Unresolved_names_inside_new_forms_stay_not_constant(string expression)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.NotFolded, result.Status);
    }
}
