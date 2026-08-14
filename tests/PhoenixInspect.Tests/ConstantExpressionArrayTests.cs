using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes array creation in every supported spelling — sized with C#'s zero-fill semantics, initialized, typed
/// empty, collection expressions with spreads, and <c>Array.Empty&lt;T&gt;()</c> — plus the pure functional
/// <c>System.Array</c> surface, the widened <c>System.Type</c> members, and the typed stops for mutation.
/// </summary>
public sealed class ConstantExpressionArrayTests
{
    /// <summary>Proves creation expressions produce exactly the sequences C# defines.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact element domain.</param>
    /// <param name="expectedText">The rendered sequence.</param>
    [Theory]
    [InlineData("new int[3]", "Int32[]", "{ 0, 0, 0 }")]
    [InlineData("new bool[2]", "Boolean[]", "{ false, false }")]
    [InlineData("new double[2]", "Double[]", "{ 0, 0 }")]
    [InlineData("new string[2]", "String[]", "{ null, null }")]
    [InlineData("new DayOfWeek[2]", "DayOfWeek[]", "{ Sunday, Sunday }")]
    [InlineData("new TimeSpan[1]", "TimeSpan[]", "{ 00:00:00 }")]
    [InlineData("new Guid[1]", "Guid[]", "{ 00000000-0000-0000-0000-000000000000 }")]
    [InlineData("new int[3] { 1, 2, 3 }", "Int32[]", "{ 1, 2, 3 }")]
    [InlineData("new int[0]", "Int32[]", "{ }")]
    [InlineData("new int[] { }", "Int32[]", "{ }")]
    [InlineData("new long[] { 1, 2 }", "Int64[]", "{ 1, 2 }")]
    [InlineData("Array.Empty<int>()", "Int32[]", "{ }")]
    [InlineData("Array.Empty<string>()", "String[]", "{ }")]
    [InlineData("[1, 2, 3]", "Int32[]", "{ 1, 2, 3 }")]
    [InlineData("[1, .. new[] { 2, 3 }, 4]", "Int32[]", "{ 1, 2, 3, 4 }")]
    [InlineData("[\"a\", \"b\"]", "String[]", "{ \"a\", \"b\" }")]
    [InlineData("new int[2 + 1]", "Int32[]", "{ 0, 0, 0 }")]
    public void Array_creation_folds_exactly(string expression, string expectedTypeName, string expectedText)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Sequence, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, ConstantExpressionEvaluator.Evaluate(session: null, expression).Sha256);
    }

    /// <summary>Proves the Array and Type surfaces produce exact scalar results.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected display.</param>
    [Theory]
    [InlineData("Array.IndexOf(new[] { 5, 6, 7 }, 6)", ConstantValueKind.Int32, "1")]
    [InlineData("Array.IndexOf(new[] { 5, 6, 7 }, 9)", ConstantValueKind.Int32, "-1")]
    [InlineData("Array.LastIndexOf(new[] { 1, 2, 1 }, 1)", ConstantValueKind.Int32, "2")]
    [InlineData("Array.BinarySearch(new[] { 1, 3, 5, 7 }, 5)", ConstantValueKind.Int32, "2")]
    [InlineData("Array.BinarySearch(new[] { 1, 3, 5, 7 }, 4)", ConstantValueKind.Int32, "-3")]
    [InlineData("Array.Exists(new[] { 1, 2, 3 }, x => x > 2)", ConstantValueKind.Boolean, "True")]
    [InlineData("Array.TrueForAll(new[] { 1, 2, 3 }, x => x > 0)", ConstantValueKind.Boolean, "True")]
    [InlineData("Array.Find(new[] { 1, 2, 3 }, x => x > 1)", ConstantValueKind.Int32, "2")]
    [InlineData("Array.FindLast(new[] { 1, 2, 3 }, x => x < 3)", ConstantValueKind.Int32, "2")]
    [InlineData("Array.FindIndex(new[] { 5, 6, 7 }, x => x > 5)", ConstantValueKind.Int32, "1")]
    [InlineData("Array.FindLastIndex(new[] { 5, 6, 7 }, x => x > 5)", ConstantValueKind.Int32, "2")]
    [InlineData("new[] { 1, 2, 3 }.Rank", ConstantValueKind.Int32, "1")]
    [InlineData("new[] { 1, 2, 3 }.GetValue(1)", ConstantValueKind.Int32, "2")]
    [InlineData("new[] { 1, 2, 3 }.GetLength(0)", ConstantValueKind.Int32, "3")]
    [InlineData("new[] { 1, 2, 3 }.GetLowerBound(0)", ConstantValueKind.Int32, "0")]
    [InlineData("new[] { 1, 2, 3 }.GetUpperBound(0)", ConstantValueKind.Int32, "2")]
    [InlineData("Array.MaxLength", ConstantValueKind.Int32, "2147483591")]
    [InlineData("typeof(int[]).Name", ConstantValueKind.String, "Int32[]")]
    [InlineData("typeof(int[]).GetElementType().Name", ConstantValueKind.String, "Int32")]
    [InlineData("typeof(int).MakeArrayType().FullName", ConstantValueKind.String, "System.Int32[]")]
    [InlineData("typeof(int[]).IsArray", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(int).IsValueType", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(string).IsValueType", ConstantValueKind.Boolean, "False")]
    [InlineData("typeof(string).IsClass", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(int).IsPrimitive", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(decimal).IsPrimitive", ConstantValueKind.Boolean, "False")]
    [InlineData("typeof(DayOfWeek).GetEnumNames().Length", ConstantValueKind.Int32, "7")]
    [InlineData("typeof(DayOfWeek).GetEnumName(5)", ConstantValueKind.String, "Friday")]
    [InlineData("typeof(DayOfWeek).IsEnumDefined(9)", ConstantValueKind.Boolean, "False")]
    [InlineData("typeof(DayOfWeek).GetEnumUnderlyingType().Name", ConstantValueKind.String, "Int32")]
    [InlineData("typeof(int).Equals(typeof(int))", ConstantValueKind.Boolean, "True")]
    [InlineData("new[] { 1, 2 }.LongLength.ToString()", ConstantValueKind.String, "2")]
    public void Array_and_type_surfaces_fold_exactly(
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

    /// <summary>Proves the lambda-mapped Array operators produce sequences.</summary>
    [Fact]
    public void Array_lambda_operators_map_to_sequence_semantics()
    {
        var filtered = ConstantExpressionEvaluator.Evaluate(
            session: null, "Array.FindAll(new[] { 1, 2, 3, 4 }, x => x % 2 == 0)");
        Assert.Equal(ConstantExpressionStatus.Exact, filtered.Status);
        Assert.Equal("{ 2, 4 }", filtered.ValueText);

        var converted = ConstantExpressionEvaluator.Evaluate(
            session: null, "Array.ConvertAll(new[] { 1, 2 }, x => x * 10)");
        Assert.Equal("{ 10, 20 }", converted.ValueText);

        var zeroFilledPipeline = ConstantExpressionEvaluator.Evaluate(
            session: null, "new int[4].Select((x, i) => x + i).Sum()");
        Assert.Equal(6, zeroFilledPipeline.Int32Value);
    }

    /// <summary>Proves the typed stops: mutation, shape violations, and bounds.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("Array.Sort(new[] { 2, 1 })", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("Array.Reverse(new[] { 2, 1 })", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("Array.Fill(new[] { 2, 1 }, 0)", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData("new int[2] { 1, 2, 3 }", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new int[-1]", "System.OverflowException")]
    [InlineData("new int[5000]", "CONSTANT_SEQUENCE_BOUND_EXCEEDED")]
    [InlineData("new int[2][]", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new int[,]{{1,2},{3}}", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new int[65, 65]", "CONSTANT_SEQUENCE_BOUND_EXCEEDED")]
    [InlineData("[]", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new[] { 1, 2 }.GetValue(9)", "System.IndexOutOfRangeException")]
    [InlineData("new[] { 1, 2 }.GetLength(1)", "System.IndexOutOfRangeException")]
    public void Array_stops_are_typed(string expression, string expectedCode)
    {
        var result = ConstantExpressionEvaluator.Evaluate(session: null, expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }
}
