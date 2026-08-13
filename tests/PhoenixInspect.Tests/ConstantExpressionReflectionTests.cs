using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the deterministic reflection surface: public member info queries over <c>typeof</c> references, the
/// read-only <c>MemberInfo</c> family, invocation of public members routed through the evaluator's own fold
/// dispatch (so every culture and non-determinism stop holds under reflection), <c>GetType()</c> over folded
/// values, and <c>System.Activator</c> construction.
/// </summary>
public sealed class ConstantExpressionReflectionTests
{
    private static ConstantExpressionEvaluation Evaluate(string expression) =>
        ConstantExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>Proves the info queries and read-only member facts fold exactly.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("typeof(Math).GetMethod(\"Sqrt\").Name", ConstantValueKind.String, "Sqrt")]
    [InlineData("typeof(Math).GetMethod(\"Sqrt\").IsStatic", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(Math).GetMethod(\"Sqrt\").IsPublic", ConstantValueKind.Boolean, "True")]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").GetParameters().Length",
        ConstantValueKind.Int32,
        "1")]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").GetParameters()[0].Name",
        ConstantValueKind.String,
        "d")]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").GetParameters()[0].Position",
        ConstantValueKind.Int32,
        "0")]
    [InlineData("typeof(string).GetProperty(\"Length\").Name", ConstantValueKind.String, "Length")]
    [InlineData("typeof(string).GetProperty(\"Length\").CanRead", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(string).GetProperty(\"Length\").CanWrite", ConstantValueKind.Boolean, "False")]
    [InlineData("typeof(int).GetField(\"MaxValue\").IsLiteral", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(int).GetField(\"MaxValue\").IsStatic", ConstantValueKind.Boolean, "True")]
    [InlineData(
        "typeof(string).GetMethods().Any(m => m.Name == \"ToUpperInvariant\")",
        ConstantValueKind.Boolean,
        "True")]
    [InlineData(
        "typeof(Guid).GetMethods().Count(m => m.Name == \"NewGuid\")",
        ConstantValueKind.Int32,
        "1")]
    [InlineData(
        "typeof(Math).GetMethods().Where(m => m.Name == \"Cbrt\").Count()",
        ConstantValueKind.Int32,
        "1")]
    [InlineData("typeof(string).GetMethod(\"NoSuchMethod\") == null", ConstantValueKind.Boolean, "True")]
    [InlineData(
        "typeof(DateTime).GetProperties().Select(p => p.Name).Contains(\"Year\")",
        ConstantValueKind.Boolean,
        "True")]
    [InlineData("typeof(Version).GetConstructors().Length > 1", ConstantValueKind.Boolean, "True")]
    [InlineData("typeof(TimeSpan).GetFields().Any(f => f.Name == \"Zero\")", ConstantValueKind.Boolean, "True")]
    [InlineData(
        "typeof(Math).GetMember(\"PI\").Length",
        ConstantValueKind.Int32,
        "1")]
    public void Reflection_info_folds_exactly(string expression, ConstantValueKind expectedKind, string expected)
    {
        var result = Evaluate(expression);
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

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, Evaluate(expression).Sha256);
    }

    /// <summary>Proves member types, declaring types, and return types resolve as modeled references.</summary>
    [Theory]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").ReturnType == typeof(double)",
        "True")]
    [InlineData(
        "typeof(string).GetProperty(\"Length\").PropertyType == typeof(int)",
        "True")]
    [InlineData(
        "typeof(int).GetField(\"MaxValue\").FieldType == typeof(int)",
        "True")]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").GetParameters()[0].ParameterType == typeof(double)",
        "True")]
    [InlineData(
        "typeof(string).GetProperty(\"Length\").MemberType == MemberTypes.Property",
        "True")]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").MemberType == MemberTypes.Method",
        "True")]
    [InlineData(
        "typeof(string).GetProperty(\"Length\").GetGetMethod().ReturnType == typeof(int)",
        "True")]
    public void Reflection_types_resolve_as_modeled_references(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.BooleanValue!.Value ? "True" : "False");
    }

    /// <summary>Proves invocation routes through the fold dispatch with exact results.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedKind">The expected value domain.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData(
        "typeof(Math).GetMethod(\"Cbrt\").Invoke(null, new object[] { 27.0 })",
        ConstantValueKind.Numeric,
        "3")]
    [InlineData(
        "typeof(string).GetMethod(\"ToUpperInvariant\").Invoke(\"abc\", null)",
        ConstantValueKind.String,
        "ABC")]
    [InlineData(
        "typeof(string).GetProperty(\"Length\").GetValue(\"hello\")",
        ConstantValueKind.Int32,
        "5")]
    [InlineData(
        "typeof(int).GetField(\"MaxValue\").GetValue(null)",
        ConstantValueKind.Int32,
        "2147483647")]
    [InlineData(
        "typeof(string).GetField(\"Empty\").GetValue(null).Length",
        ConstantValueKind.Int32,
        "0")]
    [InlineData(
        "typeof(DateTime).GetProperty(\"Year\").GetValue(new DateTime(2024, 3, 5))",
        ConstantValueKind.Int32,
        "2024")]
    [InlineData(
        "typeof(Guid).GetMethods().First(m => m.Name == \"Parse\" && "
        + "m.GetParameters()[0].ParameterType == typeof(string)).Invoke(null, "
        + "new object[] { \"8aff0911-ac7f-439b-a7c1-1f038caf3ba1\" }).Version",
        ConstantValueKind.Int32,
        "4")]
    public void Reflection_invocation_folds_exactly(
        string expression,
        ConstantValueKind expectedKind,
        string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(expectedKind, result.Kind);
        var actual = expectedKind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ConstantValueKind.Numeric => result.ValueText!,
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>Proves GetType() answers the folded value's exact modeled identity.</summary>
    [Theory]
    [InlineData("\"abc\".GetType() == typeof(string)", "True")]
    [InlineData("(5).GetType().Name", "Int32")]
    [InlineData("Guid.Empty.GetType() == typeof(Guid)", "True")]
    [InlineData("new DateTime(2024, 1, 1).GetType().FullName", "System.DateTime")]
    [InlineData("Encoding.UTF8.GetBytes(\"a\").GetType().IsArray", "True")]
    public void GetType_answers_the_exact_identity(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>Proves Activator construction routes through the modeled constructors.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expectedTypeName">The exact value type display.</param>
    /// <param name="expectedText">The invariant rendering.</param>
    [Theory]
    [InlineData("Activator.CreateInstance(typeof(int))", "Int32", "0")]
    [InlineData("Activator.CreateInstance<int>()", "Int32", "0")]
    [InlineData("Activator.CreateInstance(typeof(decimal))", "Decimal", "0")]
    [InlineData("Activator.CreateInstance(typeof(Guid))", "Guid", "00000000-0000-0000-0000-000000000000")]
    [InlineData("Activator.CreateInstance<Guid>()", "Guid", "00000000-0000-0000-0000-000000000000")]
    [InlineData("Activator.CreateInstance(typeof(TimeSpan))", "TimeSpan", "00:00:00")]
    [InlineData(
        "Activator.CreateInstance(typeof(Version), new object[] { 5, 3 })",
        "Version",
        "5.3")]
    [InlineData("Activator.CreateInstance(typeof(Version), 1, 2, 3)", "Version", "1.2.3")]
    [InlineData(
        "Activator.CreateInstance(typeof(DateTime), new object[] { 2024, 3, 5 })",
        "DateTime",
        "2024-03-05T00:00:00.0000000")]
    [InlineData("Activator.CreateInstance(typeof(Regex), \"[0-9]+\")", "Regex", "[0-9]+")]
    public void Activator_constructs_modeled_values(
        string expression,
        string expectedTypeName,
        string expectedText)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        if (expectedTypeName == "Int32")
        {
            Assert.Equal(ConstantValueKind.Int32, result.Kind);
            Assert.Equal(expectedText, result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);
    }

    /// <summary>
    /// Proves the safety property: invocation routes through the fold dispatch, so the culture and
    /// non-determinism stops hold identically under reflection, and mutation stays refused.
    /// </summary>
    [Theory]
    [InlineData(
        "typeof(string).GetMethods().First(m => m.Name == \"ToUpper\" && "
        + "m.GetParameters().Length == 0).Invoke(\"abc\", null)",
        "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED")]
    [InlineData(
        "typeof(Guid).GetMethod(\"NewGuid\").Invoke(null, null)",
        "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData(
        "typeof(DateTime).GetProperty(\"Now\").GetValue(null)",
        "CONSTANT_NONDETERMINISTIC_UNSUPPORTED")]
    [InlineData(
        "typeof(string).GetProperty(\"Length\").SetValue(\"abc\", 5)",
        "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").Invoke(null, new object[] { 1.0, 2.0 })",
        "System.Reflection.TargetParameterCountException")]
    [InlineData(
        "typeof(Math).GetMethod(\"Sqrt\").Invoke(null, \"not-an-array\")",
        "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData(
        "typeof(string).GetMethod(\"ToUpperInvariant\").Invoke(null, null)",
        "System.Reflection.TargetException")]
    [InlineData(
        "typeof(Math).GetMethod(\"Max\")",
        "System.Reflection.AmbiguousMatchException")]
    [InlineData("Activator.CreateInstance(typeof(string))", "System.MissingMethodException")]
    [InlineData("Activator.CreateInstance(typeof(Encoding))", "System.MissingMethodException")]
    [InlineData("Activator.CreateInstance(typeof(int), 5)", "System.MissingMethodException")]
    public void Reflection_stops_are_typed(string expression, string expectedCode)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>Member lists are canonically ordered, so the same query folds to the same identity.</summary>
    [Fact]
    public void Member_lists_are_canonically_ordered()
    {
        const string expression = "typeof(Math).GetMethods().Select(m => m.Name).Distinct().Take(3)";
        var first = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, first.Status);
        Assert.Equal(first.ValueText, Evaluate(expression).ValueText);
        Assert.Equal(first.Sha256, Evaluate(expression).Sha256);
    }

    /// <summary>A mixed object[] folds without a common element domain, carrying each element's own kind.</summary>
    [Fact]
    public void Object_arrays_carry_mixed_elements()
    {
        var result = Evaluate("new object[] { 1, \"a\", true }.Length");
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(3, result.Int32Value);
    }
}
