using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Freezes rectangular (multi-dimensional) array evaluation: creation from nested initializers and explicit
/// sizes, Array.CreateInstance with lengths and lower bounds, per-dimension shape members, multi-index element
/// access, and the row-major flat enumeration the whole sequence surface rides on.
/// </summary>
public sealed class RectangularArrayTests
{
    private static ExpressionEvaluation Evaluate(string expression) =>
        ExpressionEvaluationService.EvaluateValue(expression);

    /// <summary>Proves nested initializers fold with inferred dimensions and C#'s uniformity rules.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nested_initializers_fold_rectangular_arrays()
    {
        var created = Evaluate("new int[,]{{1,2},{3,4}}");
        Assert.Equal(ExpressionEvaluationStatus.Exact, created.Status);
        Assert.Equal("{ { 1, 2 }, { 3, 4 } }", created.ValueText);
        Assert.Equal("Int32[,]", created.StoredValueTypeName);

        // Explicit sizes must agree with the initializer's shape; ragged rows are refused, as C# requires.
        Assert.Equal(ExpressionEvaluationStatus.Exact, Evaluate("new int[2,2]{{1,2},{3,4}}").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("new int[3,2]{{1,2},{3,4}}").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("new int[,]{{1,2},{3}}").Status);

        // 'new T[m,n]' zero-fills, and a three-dimensional shape works the same way.
        Assert.Equal("{ { 0, 0, 0 }, { 0, 0, 0 } }", Evaluate("new int[2,3]").ValueText);
        Assert.Equal(
            "{ { { 1, 2 }, { 3, 4 } }, { { 5, 6 }, { 7, 8 } } }",
            Evaluate("new int[,,]{{{1,2},{3,4}},{{5,6},{7,8}}}").ValueText);
    }

    /// <summary>Proves the shape members and multi-index element access follow .NET array semantics.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Shape_members_and_indexing_follow_array_semantics()
    {
        const string Grid = "new int[,]{{1,2},{3,4}}";
        Assert.Equal(4, Evaluate(Grid + ".Length").Int32Value);
        Assert.Equal(2, Evaluate(Grid + ".Rank").Int32Value);
        Assert.Equal(2, Evaluate(Grid + ".GetLength(1)").Int32Value);
        Assert.Equal(0, Evaluate(Grid + ".GetLowerBound(0)").Int32Value);
        Assert.Equal(1, Evaluate(Grid + ".GetUpperBound(0)").Int32Value);
        Assert.Equal(3, Evaluate(Grid + "[1,0]").Int32Value);
        Assert.Equal(3, Evaluate(Grid + ".GetValue(1, 0)").Int32Value);

        // The flat row-major enumeration carries the whole sequence surface, exactly like LINQ over a .NET
        // rectangular array.
        Assert.Equal(10, Evaluate(Grid + ".Sum()").Int32Value);
        Assert.Equal("{ 2, 4 }", Evaluate(Grid + ".Where(x => x % 2 == 0).ToArray()").ValueText);

        // Wrong index counts and out-of-range dimensions are typed stops.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate(Grid + "[1]").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate(Grid + ".GetLength(2)").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate(Grid + "[2,0]").Status);
    }

    /// <summary>Proves Array.CreateInstance in its modeled shapes, including non-zero lower bounds.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CreateInstance_builds_zero_filled_arrays_with_bounds()
    {
        var square = Evaluate("Array.CreateInstance(typeof(int), [2, 2], [0, 0])");
        Assert.Equal(ExpressionEvaluationStatus.Exact, square.Status);
        Assert.Equal("{ { 0, 0 }, { 0, 0 } }", square.ValueText);
        Assert.Equal("Int32[,]", square.StoredValueTypeName);

        // The plain-lengths shapes: direct Int32 arguments and a lengths sequence.
        Assert.Equal(3, Evaluate("Array.CreateInstance(typeof(string), 3).Length").Int32Value);
        Assert.Equal(6, Evaluate("Array.CreateInstance(typeof(double), [2, 3]).Length").Int32Value);

        // Non-zero lower bounds are honored by the bound members and element access.
        const string Offset = "Array.CreateInstance(typeof(int), [2], [5])";
        Assert.Equal(5, Evaluate(Offset + ".GetLowerBound(0)").Int32Value);
        Assert.Equal(6, Evaluate(Offset + ".GetUpperBound(0)").Int32Value);
        Assert.Equal(0, Evaluate(Offset + ".GetValue(6)").Int32Value);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate(Offset + ".GetValue(0)").Status);

        // Unmodeled element types, mismatched bounds ranks, and the deterministic caps are typed stops.
        Assert.Equal(
            ExpressionEvaluationStatus.Invalid, Evaluate("Array.CreateInstance(typeof(int[]), 2)").Status);
        Assert.Equal(
            ExpressionEvaluationStatus.Invalid,
            Evaluate("Array.CreateInstance(typeof(int), [2, 2], [0])").Status);
        Assert.Equal(
            ExpressionEvaluationStatus.Invalid, Evaluate("Array.CreateInstance(typeof(int), [65, 65])").Status);
    }
}
