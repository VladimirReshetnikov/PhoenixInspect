using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the type-composition and type-relationship surface of <c>System.Type</c>: the assignability relation
/// (<c>IsAssignableFrom</c>/<c>To</c>, <c>IsSubclassOf</c>, <c>IsInstanceOfType</c>, <c>BaseType</c>) and generic
/// construction (<c>typeof(List&lt;int&gt;)</c>, <c>typeof(List&lt;&gt;)</c>, <c>MakeGenericType</c>, the
/// <c>IsGenericType</c> family). Every expected value below was cross-checked against the live CoreCLR answer, so
/// these rows pin the evaluator to the runtime's exact semantics — including variance, array covariance, and the
/// nullable lifting rule.
/// </summary>
public sealed class ExpressionEvaluatorTypeCompositionTests
{
    /// <summary>Proves the assignability relation reproduces the runtime's exact answers.</summary>
    /// <param name="expression">The relation query.</param>
    /// <param name="expected">The runtime's answer.</param>
    [Theory]
    [InlineData("typeof(object).IsAssignableFrom(typeof(int))", true)]
    [InlineData("typeof(System.ValueType).IsAssignableFrom(typeof(System.Enum))", true)]
    [InlineData("typeof(System.ValueType).IsAssignableFrom(typeof(int?))", true)]
    [InlineData("typeof(int?).IsAssignableFrom(typeof(int))", true)]
    [InlineData("typeof(int?).IsAssignableFrom(typeof(long))", false)]
    [InlineData("typeof(IEnumerable<object>).IsAssignableFrom(typeof(string[]))", true)]
    [InlineData("typeof(IEnumerable<long>).IsAssignableFrom(typeof(int[]))", false)]
    [InlineData("typeof(IEnumerable<int>).IsAssignableFrom(typeof(int[]))", true)]
    [InlineData("typeof(IEnumerable<char>).IsAssignableFrom(typeof(string))", true)]
    [InlineData("typeof(IComparable).IsAssignableFrom(typeof(DayOfWeek))", true)]
    [InlineData("typeof(IComparable<string>).IsAssignableFrom(typeof(IComparable<object>))", true)]
    [InlineData("typeof(IComparable<object>).IsAssignableFrom(typeof(IComparable<string>))", false)]
    [InlineData("typeof(ICollection<int>).IsAssignableFrom(typeof(IList<int>))", true)]
    [InlineData("typeof(IReadOnlyList<object>).IsAssignableFrom(typeof(List<string>))", true)]
    [InlineData("typeof(IDictionary<string, int>).IsAssignableFrom(typeof(Dictionary<string, int>))", true)]
    [InlineData(
        "typeof(IEnumerable<KeyValuePair<string, int>>).IsAssignableFrom(typeof(Dictionary<string, int>))", true)]
    [InlineData("typeof(object[]).IsAssignableFrom(typeof(string[]))", true)]
    [InlineData("typeof(string[]).IsAssignableFrom(typeof(object[]))", false)]
    [InlineData("typeof(IEquatable<int>).IsAssignableFrom(typeof(int?))", false)]
    [InlineData("typeof(IEquatable<int>).IsAssignableFrom(typeof(int))", true)]
    [InlineData("typeof(object).IsAssignableFrom(typeof(List<>))", true)]
    [InlineData("typeof(IList<>).IsAssignableFrom(typeof(List<>))", false)]
    [InlineData("typeof(System.Array).IsAssignableFrom(typeof(int[]))", true)]
    [InlineData("typeof(System.Enum).IsAssignableFrom(typeof(DayOfWeek))", true)]
    [InlineData("typeof(int).IsAssignableTo(typeof(IComparable<int>))", true)]
    [InlineData("typeof(int).IsAssignableTo(typeof(string))", false)]
    [InlineData("typeof(int[]).IsSubclassOf(typeof(System.Array))", true)]
    [InlineData("typeof(string[]).IsSubclassOf(typeof(object[]))", false)]
    [InlineData("typeof(DayOfWeek).IsSubclassOf(typeof(System.Enum))", true)]
    [InlineData("typeof(int).IsSubclassOf(typeof(int))", false)]
    [InlineData("typeof(IComparable).IsInstanceOfType(5)", true)]
    [InlineData("typeof(IEnumerable<int>).IsInstanceOfType(new[] { 1 })", true)]
    [InlineData("typeof(string).IsInstanceOfType(3.5)", false)]
    [InlineData("typeof(object).IsInstanceOfType(\"text\")", true)]
    public void Assignability_matches_the_runtime(string expression, bool expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.BooleanValue);
    }

    /// <summary>Proves generic construction and introspection reproduce the runtime's answers.</summary>
    /// <param name="expression">The generic-surface query.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected display.</param>
    [Theory]
    [InlineData("typeof(List<>).MakeGenericType(typeof(int)) == typeof(List<int>)", ExpressionValueKind.Boolean,
        "True")]
    [InlineData("typeof(List<>).MakeGenericType(typeof(int)).ToString()", ExpressionValueKind.String,
        "System.Collections.Generic.List`1[System.Int32]")]
    [InlineData("typeof(List<int>).GetGenericTypeDefinition().FullName", ExpressionValueKind.String,
        "System.Collections.Generic.List`1")]
    [InlineData("typeof(int?).Name", ExpressionValueKind.String, "Nullable`1")]
    [InlineData("typeof(Nullable<int>) == typeof(int?)", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(List<>).FullName", ExpressionValueKind.String, "System.Collections.Generic.List`1")]
    [InlineData("typeof(List<int>).Name", ExpressionValueKind.String, "List`1")]
    [InlineData("typeof(List<int>).Namespace", ExpressionValueKind.String, "System.Collections.Generic")]
    [InlineData("typeof(List<int>[]).ToString()", ExpressionValueKind.String,
        "System.Collections.Generic.List`1[System.Int32][]")]
    [InlineData("typeof(List<int>).IsGenericType", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(List<int>).IsGenericTypeDefinition", ExpressionValueKind.Boolean, "False")]
    [InlineData("typeof(List<>).IsGenericTypeDefinition", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(List<>).ContainsGenericParameters", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(List<int>).IsConstructedGenericType", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(List<int>[]).IsGenericType", ExpressionValueKind.Boolean, "False")]
    [InlineData("typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(int)).GenericTypeArguments.Length",
        ExpressionValueKind.Int32, "2")]
    [InlineData("typeof(List<int>).GenericTypeArguments[0] == typeof(int)", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(List<int>).GetGenericArguments().Length", ExpressionValueKind.Int32, "1")]
    [InlineData("typeof(int).GetGenericArguments().Length", ExpressionValueKind.Int32, "0")]
    [InlineData("typeof(int).BaseType.FullName", ExpressionValueKind.String, "System.ValueType")]
    [InlineData("typeof(System.ValueType).BaseType.Name", ExpressionValueKind.String, "Object")]
    [InlineData("typeof(DayOfWeek).BaseType == typeof(System.Enum)", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(IEnumerable<int>).IsInterface", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(IEnumerable<int>).IsClass", ExpressionValueKind.Boolean, "False")]
    [InlineData("typeof(List<int>).IsClass", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(KeyValuePair<string, int>).IsValueType", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(int?).IsValueType", ExpressionValueKind.Boolean, "True")]
    [InlineData("typeof(int?).GetGenericTypeDefinition() == typeof(Nullable<>)", ExpressionValueKind.Boolean,
        "True")]
    public void Generic_surface_matches_the_runtime(
        string expression,
        ExpressionValueKind expectedKind,
        string expected)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
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
    }

    /// <summary>Proves the runtime's failure semantics and the evaluator's own typed stops.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("typeof(List<int>).MakeGenericType(typeof(int))", "System.InvalidOperationException")]
    [InlineData("typeof(List<>).MakeGenericType(typeof(int), typeof(int))", "System.ArgumentException")]
    [InlineData("typeof(Nullable<>).MakeGenericType(typeof(string))", "System.ArgumentException")]
    [InlineData("typeof(int).GetGenericTypeDefinition()", "System.InvalidOperationException")]
    [InlineData("typeof(int).IsSubclassOf(null)", "System.ArgumentNullException")]
    [InlineData("typeof(List<int>).FullName", "EVAL_MEMBER_UNSUPPORTED")]
    [InlineData("typeof(int).AssemblyQualifiedName", "EVAL_MEMBER_UNSUPPORTED")]
    [InlineData("typeof(List<>).GetGenericArguments()", "EVAL_MEMBER_UNSUPPORTED")]
    public void Composition_stops_are_typed(string expression, string expectedCode)
    {
        var result = ExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>Proves a typeof over a constructed generic renders in C# spelling and replays canonically.</summary>
    [Fact]
    public void Constructed_generic_typeof_renders_and_replays()
    {
        var result = ExpressionEvaluator.Evaluate(session: null, "typeof(Dictionary<string, List<int>>)");
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(ExpressionValueKind.Type, result.Kind);
        Assert.Equal("typeof(Dictionary<string, List<int>>)", result.ValueText);
        Assert.Equal(
            result.Sha256,
            ExpressionEvaluator.Evaluate(session: null, "typeof(Dictionary<string, List<int>>)").Sha256);

        // The nested construction spelled through MakeGenericType is the same reference.
        var composed = ExpressionEvaluator.Evaluate(
            session: null,
            "typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(List<int>))"
            + " == typeof(Dictionary<string, List<int>>)");
        Assert.Equal(true, composed.BooleanValue);
    }
}
