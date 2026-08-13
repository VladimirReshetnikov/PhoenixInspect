using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes virtually created delegates: creation through <c>new</c>, delegate-type casts over expression
/// lambdas and method groups, and the <c>CreateDelegate</c> family; invocation through <c>…(args)</c>,
/// <c>.Invoke</c>, and <c>DynamicInvoke</c>; the multicast algebra (<c>+</c>, <c>-</c>, equality,
/// <c>Combine</c>/<c>Remove</c>/<c>RemoveAll</c>); and the <c>Delegate</c>/<c>MulticastDelegate</c> member
/// surface — <c>Method</c>, <c>Target</c>, <c>HasSingleTarget</c>, <c>GetInvocationList</c>.
/// </summary>
public sealed class ConstantExpressionDelegateTests
{
    private static ConstantExpressionEvaluation Evaluate(string expression) =>
        ConstantExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>Proves creation, conversion, and invocation fold exactly.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("new Func<int, int>(x => x + 1)(5)", "6")]
    [InlineData("((Func<int, int>)(x => x * 2)).Invoke(21)", "42")]
    [InlineData("((Func<int, int>)(x => x * 2)).DynamicInvoke(new object[] { 21 })", "42")]
    [InlineData("new Func<double, double>(Math.Sqrt)(81.0)", "9")]
    [InlineData("((Func<double, double>)Math.Sqrt)(4.0)", "2")]
    [InlineData("new Func<string>(\"hi\".ToUpperInvariant)()", "HI")]
    [InlineData("new Func<int, int, int>((a, b) => a * b)(6, 7)", "42")]
    [InlineData("((Comparison<int>)((a, b) => a - b))(5, 3)", "2")]
    [InlineData("((Predicate<string>)(s => s.Length > 2))(\"abcd\")", "True")]
    [InlineData("new Action(() => 0)()", "null")]
    [InlineData(
        "Delegate.CreateDelegate(typeof(Func<double, double>), typeof(Math).GetMethod(\"Cbrt\"))(8.0)",
        "2")]
    [InlineData("typeof(Math).GetMethod(\"Cbrt\").CreateDelegate(typeof(Func<double, double>))(27.0)", "3")]
    [InlineData(
        "typeof(string).GetMethod(\"ToUpperInvariant\")"
        + ".CreateDelegate(typeof(Func<string>), \"abc\")()",
        "ABC")]
    [InlineData("new Func<int, int>((Func<int, int>)(x => x + 3))(4)", "7")]
    [InlineData("((Func<string, bool>)\"hello\".Contains)(\"ell\")", "True")]
    [InlineData("((Func<char, bool>)\"hello\".Contains)('z')", "False")]
    [InlineData("((Func<int, Func<int, int>>)(x => y => x + y))(5)(6)", "11")]
    [InlineData("new Func<int, Func<int, Func<int, int>>>(a => b => c => a * b * c)(2)(3)(7)", "42")]
    [InlineData("((Func<Func<int, int>, int>)(g => g(10)))(x => x + 1)", "11")]
    [InlineData("((Func<int, Func<double, double>>)(x => Math.Sqrt))(1)(16.0)", "4")]
    [InlineData("((Func<int, Func<int, int>>)(x => null))(5) == null", "True")]
    [InlineData("(Action)null == null", "True")]
    [InlineData("((Func<int, int>)(x => x + 1) + (Func<int, int>)(x => x * 10))(3)", "30")]
    [InlineData(
        "new[] { 1, 2, 3 }.Select(x => new Func<int, int>(y => x + y)).Select(f => f(10)).Sum()",
        "36")]
    public void Delegates_create_and_invoke_exactly(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ConstantValueKind.Null => "null",
            ConstantValueKind.Numeric => result.ValueText!,
            _ => result.StringValue ?? result.ValueText!,
        };
        Assert.Equal(expected, actual);

        // The same expression reproduces the same canonical outcome identity.
        Assert.Equal(result.Sha256, Evaluate(expression).Sha256);
    }

    /// <summary>Proves the multicast algebra and the Delegate member surface.</summary>
    /// <param name="expression">The constant expression to fold.</param>
    /// <param name="expected">The expected invariant display.</param>
    [Theory]
    [InlineData("new Func<double, double>(Math.Sqrt) == new Func<double, double>(Math.Sqrt)", "True")]
    [InlineData("new Func<double, double>(Math.Sqrt) != new Func<double, double>(Math.Cbrt)", "True")]
    [InlineData("(Func<int, int>)(x => x + 1) == (Func<int, int>)(x => x + 1)", "False")]
    [InlineData("new Func<double, double>(Math.Sqrt).Method.Name", "Sqrt")]
    [InlineData("new Func<double, double>(Math.Sqrt).Method.IsStatic", "True")]
    [InlineData("new Func<double, double>(Math.Sqrt).Target == null", "True")]
    [InlineData("new Func<string>(\"hi\".ToUpperInvariant).Target", "hi")]
    [InlineData("new Func<double, double>(Math.Sqrt).HasSingleTarget", "True")]
    [InlineData("((Action)(() => 1) + (Action)(() => 2)).HasSingleTarget", "False")]
    [InlineData("((Action)(() => 1) + (Action)(() => 2)).GetInvocationList().Length", "2")]
    [InlineData("Delegate.Combine((Action)(() => 1), (Action)(() => 2)).GetInvocationList().Length", "2")]
    [InlineData("Delegate.Combine(null, (Action)(() => 1)).HasSingleTarget", "True")]
    [InlineData("(null + (Func<int, int>)(x => x))(5)", "5")]
    [InlineData("((Func<int, int>)(x => x) + null)(5)", "5")]
    [InlineData("Delegate.Remove(null, (Action)(() => 1)) == null", "True")]
    [InlineData(
        "new[] { new Func<double, double>(Math.Sqrt), new Func<double, double>(Math.Cbrt) }"
        + ".Select(f => f(64.0)).Sum()",
        "12")]
    public void Delegate_algebra_and_members_fold_exactly(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ConstantValueKind.Numeric => result.ValueText!,
            _ => result.StringValue ?? result.ValueText!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Proves the Remove family's exact semantics: the last occurrence of the value's whole list is removed,
    /// an unmatched value leaves the source unchanged, and an emptied list is exactly null.
    /// </summary>
    [Fact]
    public void Remove_matches_the_runtime_semantics()
    {
        // A method-group delegate equals another over the same method, so Remove finds and removes it.
        var removed = Evaluate(
            "(new Func<double, double>(Math.Sqrt) + new Func<double, double>(Math.Cbrt) "
            + "- new Func<double, double>(Math.Sqrt))(64.0)");
        Assert.Equal(ConstantExpressionStatus.Exact, removed.Status);
        Assert.Equal("4", removed.ValueText);

        // Distinct lambda creations are unequal, so removal leaves the source untouched.
        var untouched = Evaluate("(new Func<int, int>(x => x) - new Func<int, int>(x => x))(9)");
        Assert.Equal(ConstantExpressionStatus.Exact, untouched.Status);
        Assert.Equal(9, untouched.Int32Value);

        // Removing a delegate's only entry empties the list to exactly null.
        var emptied = Evaluate(
            "new Func<double, double>(Math.Sqrt) - new Func<double, double>(Math.Sqrt) == null");
        Assert.Equal(ConstantExpressionStatus.Exact, emptied.Status);
        Assert.True(emptied.BooleanValue);
    }

    /// <summary>Proves delegate values render with their type and entry texts.</summary>
    [Theory]
    [InlineData("(Func<int, int>)(x => x + 1)", "Func<int, int>", "x => x + 1")]
    [InlineData("new Func<double, double>(Math.Sqrt)", "Func<double, double>", "Math.Sqrt")]
    [InlineData("(Action)(() => 0) + (Action)(() => 1)", "Action", "() => 0 + () => 1")]
    public void Delegates_render_their_identities(string expression, string expectedTypeName, string expectedText)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        Assert.Equal(ConstantValueKind.BclValue, result.Kind);
        Assert.Equal(expectedTypeName, result.ValueTypeName);
        Assert.Equal(expectedText, result.ValueText);
    }

    /// <summary>Proves the typed stops: arity mismatches, type mismatches, and non-delegate operands.</summary>
    /// <param name="expression">The expression whose evaluation stops.</param>
    /// <param name="expectedCode">The stable diagnostic code.</param>
    [Theory]
    [InlineData("new Func<int, int>((a, b) => a + b)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new Func<int, int>(x => x)(1, 2)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new Func<int, int>(Math.Sqrt)", "System.ArgumentException")]
    [InlineData("(Action)(() => 1) + (Func<int>)(() => 1)", "System.ArgumentException")]
    [InlineData("(Action)(() => 1) + 5", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("(Action)(() => 1) * (Action)(() => 2)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new Func<int, int>(5)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("new Action(() => 1, () => 2)", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("(Func<int, int>)(x => x + 1) == \"text\"", "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    [InlineData("((Func<int, int>)(x => x)).Method", "CONSTANT_MEMBER_UNSUPPORTED")]
    [InlineData(
        "(Action<string>)\"hello\".Contains",
        "System.ArgumentException")]
    [InlineData(
        "new Func<double, double>(Math.Sqrt)(81.0, 1.0)",
        "CONSTANT_OPERAND_TYPE_UNSUPPORTED")]
    public void Delegate_stops_are_typed(string expression, string expectedCode)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Invalid, result.Status);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.NotNull(result.DiagnosticMessage);
    }

    /// <summary>
    /// Proves the safety property: a delegate invocation routes through the fold dispatch, so culture and
    /// non-determinism stops hold identically when reached through a delegate.
    /// </summary>
    [Fact]
    public void Delegate_invocation_keeps_the_determinism_stops()
    {
        var culture = Evaluate("new Func<string>(\"abc\".ToUpper)()");
        Assert.Equal(ConstantExpressionStatus.Invalid, culture.Status);
        Assert.Equal("CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED", culture.DiagnosticCode);

        var nondeterministic = Evaluate(
            "typeof(Guid).GetMethod(\"NewGuid\").CreateDelegate(typeof(Func<Guid>))()");
        Assert.Equal(ConstantExpressionStatus.Invalid, nondeterministic.Status);
        Assert.Equal("CONSTANT_NONDETERMINISTIC_UNSUPPORTED", nondeterministic.DiagnosticCode);
    }

    /// <summary>
    /// Proves creation is lazy about invocability: a delegate over a non-deterministic or culture-sensitive
    /// member creates exactly, its info surface reads, and the stop occurs only when it is actually invoked.
    /// </summary>
    [Theory]
    [InlineData("new Func<Guid>(Guid.NewGuid).Method.Name", "NewGuid")]
    [InlineData("((Func<Guid>)Guid.NewGuid).HasSingleTarget", "True")]
    [InlineData(
        "Delegate.CreateDelegate(typeof(Func<Guid>), typeof(Guid).GetMethod(\"NewGuid\")).Method.IsStatic",
        "True")]
    [InlineData("new Func<string>(\"abc\".ToUpper).Method.Name", "ToUpper")]
    [InlineData("((Func<DateTime>)(() => DateTime.Now)).HasSingleTarget", "True")]
    [InlineData("(new Func<Guid>(Guid.NewGuid) + new Func<Guid>(Guid.NewGuid)).GetInvocationList().Length", "2")]
    public void Creation_never_stops_for_invocable_shape(string expression, string expected)
    {
        var result = Evaluate(expression);
        Assert.Equal(ConstantExpressionStatus.Exact, result.Status);
        var actual = result.Kind switch
        {
            ConstantValueKind.Boolean => result.BooleanValue!.Value ? "True" : "False",
            ConstantValueKind.Int32 => result.Int32Value!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _ => result.StringValue!,
        };
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// A property accessor reached as a MethodInfo answers as its property: a bound getter delegate folds the
    /// read, and a non-deterministic getter stops with the property's own stop — at invocation, never before.
    /// </summary>
    [Fact]
    public void Property_getter_delegates_answer_as_their_properties()
    {
        var bound = Evaluate(
            "typeof(string).GetProperty(\"Length\").GetGetMethod()"
            + ".CreateDelegate(typeof(Func<int>), \"hello\")()");
        Assert.Equal(ConstantExpressionStatus.Exact, bound.Status);
        Assert.Equal(5, bound.Int32Value);

        var created = Evaluate(
            "typeof(DateTime).GetProperty(\"UtcNow\").GetGetMethod()"
            + ".CreateDelegate(typeof(Func<DateTime>)).HasSingleTarget");
        Assert.Equal(ConstantExpressionStatus.Exact, created.Status);
        Assert.True(created.BooleanValue);

        var invoked = Evaluate(
            "typeof(DateTime).GetProperty(\"UtcNow\").GetGetMethod()"
            + ".CreateDelegate(typeof(Func<DateTime>))()");
        Assert.Equal(ConstantExpressionStatus.Invalid, invoked.Status);
        Assert.Equal("CONSTANT_NONDETERMINISTIC_UNSUPPORTED", invoked.DiagnosticCode);
    }

    /// <summary>A curried result is a first-class delegate: its identity renders and its closure holds.</summary>
    [Fact]
    public void Curried_results_carry_their_closures()
    {
        var partial = Evaluate("((Func<int, Func<int, int>>)(x => y => x + y))(5)");
        Assert.Equal(ConstantExpressionStatus.Exact, partial.Status);
        Assert.Equal("Func<int, int>", partial.ValueTypeName);
        Assert.Equal("y => x + y", partial.ValueText);

        // Two partial applications close over different outer values.
        var separated = Evaluate(
            "((Func<int, Func<int, int>>)(x => y => x + y))(100)(1) + "
            + "((Func<int, Func<int, int>>)(x => y => x + y))(200)(2)");
        Assert.Equal(ConstantExpressionStatus.Exact, separated.Status);
        Assert.Equal(303, separated.Int32Value);
    }

    /// <summary>GetType and typeof agree on the delegate's exact constructed identity.</summary>
    [Fact]
    public void Delegate_type_identity_is_exact()
    {
        var identity = Evaluate("((Func<int, int>)(x => x)).GetType() == typeof(Func<int, int>)");
        Assert.Equal(ConstantExpressionStatus.Exact, identity.Status);
        Assert.True(identity.BooleanValue);

        var name = Evaluate("typeof(Action<int>).Name");
        Assert.Equal("Action`1", name.StringValue);
    }
}
