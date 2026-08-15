using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the immutable dictionaries and their KeyValuePair element domain: construction from pairs and
/// selectors, key uniqueness with the BCL's exceptions, lookup, the persistent Add/SetItem/Remove operations,
/// the Keys and Values projections, sorted-by-key ordering, and the pair value itself.
/// </summary>
public sealed class ExpressionEvaluatorImmutableDictionaryTests
{
    private static ExpressionEvaluation Evaluate(string expression) =>
        ExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>KeyValuePair is a first-class value with Key and Value and the pair rendering.</summary>
    [Fact]
    public void KeyValuePair_is_a_value()
    {
        var pair = Evaluate("KeyValuePair.Create(1, \"a\")");
        Assert.Equal(ExpressionEvaluationStatus.Exact, pair.Status);
        Assert.Equal("KeyValuePair<Int32, String>", pair.StoredValueTypeName);
        Assert.Equal("[1, \"a\"]", pair.ValueText);

        Assert.Equal(1, Evaluate("KeyValuePair.Create(1, \"a\").Key").Int32Value);
        Assert.Equal("a", Evaluate("KeyValuePair.Create(1, \"a\").Value").StringValue);
        Assert.Equal(2, Evaluate("new KeyValuePair<int, string>(2, \"b\").Key").Int32Value);
        Assert.Equal(
            true,
            Evaluate("KeyValuePair.Create(1, \"a\") is KeyValuePair<int, string>").BooleanValue);
    }

    /// <summary>The dictionary factories build from pairs, with unique keys and sorted-by-key ordering.</summary>
    [Fact]
    public void Dictionary_factories_build_from_pairs()
    {
        var range = Evaluate(
            "ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create(1, \"a\"), KeyValuePair.Create(2, \"b\") })");
        Assert.Equal(ExpressionEvaluationStatus.Exact, range.Status);
        Assert.Equal("ImmutableDictionary<Int32, String>", range.StoredValueTypeName);
        Assert.Equal("{ [1, \"a\"], [2, \"b\"] }", range.ValueText);

        var sorted = Evaluate(
            "ImmutableSortedDictionary.CreateRange(new[] { KeyValuePair.Create(2, \"b\"), KeyValuePair.Create(1, \"a\") })");
        Assert.Equal("ImmutableSortedDictionary<Int32, String>", sorted.StoredValueTypeName);
        Assert.Equal("{ [1, \"a\"], [2, \"b\"] }", sorted.ValueText);

        var empty = Evaluate("ImmutableDictionary.Create<int, string>()");
        Assert.Equal("ImmutableDictionary<Int32, String>", empty.StoredValueTypeName);
        Assert.Equal("{ }", empty.ValueText);
        Assert.Equal(
            "ImmutableSortedDictionary<String, Int32>",
            Evaluate("ImmutableSortedDictionary<string, int>.Empty").StoredValueTypeName);

        var duplicate = Evaluate(
            "ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create(1, \"a\"), KeyValuePair.Create(1, \"b\") })");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, duplicate.Status);
        Assert.Equal("System.ArgumentException", duplicate.DiagnosticCode);
    }

    /// <summary>The selector overloads of ToImmutableDictionary key and project each element.</summary>
    [Fact]
    public void ToImmutableDictionary_selectors_fold()
    {
        var keyed = Evaluate("new[] { \"a\", \"bb\" }.ToImmutableDictionary(s => s.Length)");
        Assert.Equal(ExpressionEvaluationStatus.Exact, keyed.Status);
        Assert.Equal("ImmutableDictionary<Int32, String>", keyed.StoredValueTypeName);
        Assert.Equal("{ [1, \"a\"], [2, \"bb\"] }", keyed.ValueText);

        var projected = Evaluate(
            "new[] { 1, 2 }.ToImmutableSortedDictionary(x => x, x => x * 10)");
        Assert.Equal("ImmutableSortedDictionary<Int32, Int32>", projected.StoredValueTypeName);
        Assert.Equal("{ [1, 10], [2, 20] }", projected.ValueText);

        // Duplicate keys throw exactly as running LINQ does.
        Assert.Equal(
            ExpressionEvaluationStatus.Invalid,
            Evaluate("new[] { 1, 1 }.ToImmutableDictionary(x => x)").Status);

        // The pair-sequence overload retags a KeyValuePair sequence.
        Assert.Equal(
            "ImmutableDictionary<Int32, Int32>",
            Evaluate("new[] { 5 }.ToImmutableDictionary(x => x, x => x).ToImmutableDictionary()")
                .StoredValueTypeName);
    }

    /// <summary>Lookup answers by key, with the BCL's missing-key exception.</summary>
    [Fact]
    public void Lookup_answers_by_key()
    {
        Assert.Equal(
            "a",
            Evaluate("new[] { \"a\", \"bb\" }.ToImmutableDictionary(s => s.Length)[1]").StringValue);
        Assert.Equal(
            10,
            Evaluate("new[] { 1 }.ToImmutableDictionary(x => x, x => x * 10)[1]").Int32Value);

        var missing = Evaluate("new[] { 1 }.ToImmutableDictionary(x => x)[9]");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, missing.Status);
        Assert.Equal("System.Collections.Generic.KeyNotFoundException", missing.DiagnosticCode);
    }

    /// <summary>Add, SetItem, and Remove are persistent, with Add's exact same-key semantics.</summary>
    [Fact]
    public void Mutators_are_persistent()
    {
        const string Dictionary = "new[] { 1, 2 }.ToImmutableDictionary(x => x, x => x * 10)";
        Assert.Equal("{ [1, 10], [2, 20], [3, 9] }", Evaluate($"{Dictionary}.Add(3, 9)").ValueText);
        Assert.Equal("{ [1, 99], [2, 20] }", Evaluate($"{Dictionary}.SetItem(1, 99)").ValueText);
        Assert.Equal("{ [2, 20] }", Evaluate($"{Dictionary}.Remove(1)").ValueText);
        Assert.Equal("{ [1, 10], [2, 20] }", Evaluate($"{Dictionary}.Remove(9)").ValueText);
        Assert.Equal("{ }", Evaluate($"{Dictionary}.Clear()").ValueText);
        Assert.Equal(
            "ImmutableDictionary<Int32, Int32>",
            Evaluate($"{Dictionary}.Add(3, 9)").StoredValueTypeName);

        // Adding an existing key with an equal value answers the same dictionary; a different value throws.
        Assert.Equal("{ [1, 10], [2, 20] }", Evaluate($"{Dictionary}.Add(1, 10)").ValueText);
        var conflict = Evaluate($"{Dictionary}.Add(1, 11)");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, conflict.Status);
        Assert.Equal("System.ArgumentException", conflict.DiagnosticCode);
    }

    /// <summary>The dictionary properties and predicates answer per the BCL surface.</summary>
    [Fact]
    public void Properties_and_predicates_answer()
    {
        const string Dictionary = "new[] { 1, 2 }.ToImmutableDictionary(x => x, x => x * 10)";
        Assert.Equal(2, Evaluate($"{Dictionary}.Count").Int32Value);
        Assert.Equal(false, Evaluate($"{Dictionary}.IsEmpty").BooleanValue);
        Assert.Equal(true, Evaluate($"{Dictionary}.ContainsKey(2)").BooleanValue);
        Assert.Equal(false, Evaluate($"{Dictionary}.ContainsKey(9)").BooleanValue);
        Assert.Equal(true, Evaluate($"{Dictionary}.ContainsValue(20)").BooleanValue);
        Assert.Equal("{ 1, 2 }", Evaluate($"{Dictionary}.Keys").ValueText);
        Assert.Equal("{ 10, 20 }", Evaluate($"{Dictionary}.Values").ValueText);
        Assert.Equal("Int32[]", Evaluate($"{Dictionary}.Keys").StoredValueTypeName);
    }

    /// <summary>LINQ composes over dictionaries as pair sequences, and identity flows through types.</summary>
    [Fact]
    public void Linq_and_type_identity_compose()
    {
        const string Dictionary = "new[] { 1, 2, 3 }.ToImmutableDictionary(x => x, x => x * 10)";
        Assert.Equal(60, Evaluate($"{Dictionary}.Sum(pair => pair.Value)").Int32Value);
        Assert.Equal(
            "{ 1, 2 }",
            Evaluate($"{Dictionary}.Where(pair => pair.Value < 30).Select(pair => pair.Key).ToArray()")
                .ValueText);

        Assert.Equal(true, Evaluate($"{Dictionary} is ImmutableDictionary<int, int>").BooleanValue);
        Assert.Equal(true, Evaluate($"{Dictionary} is IImmutableDictionary<int, int>").BooleanValue);
        Assert.Equal(false, Evaluate($"{Dictionary} is ImmutableDictionary<int, string>").BooleanValue);
        Assert.Equal(
            true,
            Evaluate($"{Dictionary}.GetType() == typeof(ImmutableDictionary<int, int>)").BooleanValue);
        Assert.Equal(
            true,
            Evaluate("typeof(ImmutableSortedDictionary<int, string>).IsGenericType").BooleanValue);
    }
}
