using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes the System.Collections.Immutable surface: the factories (Create, CreateRange, Empty), the
/// ToImmutable* extensions, the persistent instance operations that always return a new collection, the per-kind
/// property fidelity (Length vs Count vs IsEmpty), set and queue and stack semantics, indexer fidelity, and the
/// type identities the collections report through GetType, typeof, and 'is'.
/// </summary>
public sealed class ExpressionEvaluatorImmutableCollectionTests
{
    private static ExpressionEvaluation Evaluate(string expression) =>
        ExpressionEvaluator.Evaluate(session: null, expression);

    /// <summary>The factories build tagged collections with exact type spellings and renderings.</summary>
    [Theory]
    [InlineData("ImmutableArray.Create(1, 2, 3)", "{ 1, 2, 3 }", "ImmutableArray<Int32>")]
    [InlineData("ImmutableArray.Create<double>(1, 2)", "{ 1, 2 }", "ImmutableArray<Double>")]
    [InlineData("ImmutableArray<int>.Empty", "{ }", "ImmutableArray<Int32>")]
    [InlineData("ImmutableList.Create(\"a\", \"b\")", "{ \"a\", \"b\" }", "ImmutableList<String>")]
    [InlineData("ImmutableList.CreateRange(new[] { 1, 2 })", "{ 1, 2 }", "ImmutableList<Int32>")]
    [InlineData("ImmutableHashSet.Create(1, 2, 2, 3)", "{ 1, 2, 3 }", "ImmutableHashSet<Int32>")]
    [InlineData("ImmutableSortedSet.Create(3, 1, 2, 3)", "{ 1, 2, 3 }", "ImmutableSortedSet<Int32>")]
    [InlineData("ImmutableQueue.Create(1, 2, 3)", "{ 1, 2, 3 }", "ImmutableQueue<Int32>")]
    [InlineData("ImmutableStack.Create(1, 2, 3)", "{ 3, 2, 1 }", "ImmutableStack<Int32>")]
    [InlineData("System.Collections.Immutable.ImmutableArray.Create(1)", "{ 1 }", "ImmutableArray<Int32>")]
    [InlineData("ImmutableList<string>.Empty", "{ }", "ImmutableList<String>")]
    public void Factories_build_tagged_collections(string expression, string expectedValue, string expectedType)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedType, result.StoredValueTypeName);
        Assert.Equal(expectedValue, result.ValueText);
    }

    /// <summary>The ToImmutable* extensions convert any sequence, applying each kind's invariant.</summary>
    [Fact]
    public void ToImmutable_extensions_convert_sequences()
    {
        var array = Evaluate("new[] { 3, 1, 2 }.ToImmutableArray()");
        Assert.Equal("ImmutableArray<Int32>", array.StoredValueTypeName);
        Assert.Equal("{ 3, 1, 2 }", array.ValueText);

        Assert.Equal("ImmutableList<Int32>", Evaluate("new[] { 1 }.ToImmutableList()").StoredValueTypeName);
        Assert.Equal(2, Evaluate("\"a,b,a\".Split(',').ToImmutableHashSet()").Int32Value);
        Assert.Equal("{ 1, 2, 3 }", Evaluate("new[] { 3, 1, 2, 1 }.ToImmutableSortedSet()").ValueText);

        // Chains keep working: an immutable collection converts onward, and ToArray sheds the identity.
        Assert.Equal(
            "ImmutableHashSet<Int32>",
            Evaluate("ImmutableList.Create(1, 1, 2).ToImmutableHashSet()").StoredValueTypeName);
        Assert.Equal("Int32[]", Evaluate("ImmutableList.Create(1, 2).ToArray()").StoredValueTypeName);
    }

    /// <summary>Every mutator returns a new collection of the same kind, as the BCL defines.</summary>
    [Theory]
    [InlineData("ImmutableList.Create(1, 2).Add(3)", "{ 1, 2, 3 }", "ImmutableList<Int32>")]
    [InlineData("ImmutableArray.Create(1, 2, 3).SetItem(1, 9)", "{ 1, 9, 3 }", "ImmutableArray<Int32>")]
    [InlineData("ImmutableArray.Create(1, 2, 3).RemoveAt(0)", "{ 2, 3 }", "ImmutableArray<Int32>")]
    [InlineData("ImmutableList.Create(1, 2, 3).Remove(2)", "{ 1, 3 }", "ImmutableList<Int32>")]
    [InlineData("ImmutableList.Create(1, 2).Remove(9)", "{ 1, 2 }", "ImmutableList<Int32>")]
    [InlineData("ImmutableList.Create(3, 1, 2).Sort()", "{ 1, 2, 3 }", "ImmutableList<Int32>")]
    [InlineData("ImmutableList.Create(1, 2, 3).Reverse()", "{ 3, 2, 1 }", "ImmutableList<Int32>")]
    [InlineData("ImmutableArray.Create(1).AddRange(new[] { 2, 3 })", "{ 1, 2, 3 }", "ImmutableArray<Int32>")]
    [InlineData("ImmutableList.Create(1, 3).Insert(1, 2)", "{ 1, 2, 3 }", "ImmutableList<Int32>")]
    [InlineData("ImmutableList.Create(1, 2).Clear()", "{ }", "ImmutableList<Int32>")]
    [InlineData("ImmutableHashSet.Create(1, 2).Add(2)", "{ 1, 2 }", "ImmutableHashSet<Int32>")]
    [InlineData("ImmutableSortedSet.Create(2, 4).Add(3)", "{ 2, 3, 4 }", "ImmutableSortedSet<Int32>")]
    [InlineData("ImmutableQueue.Create(1, 2).Enqueue(3)", "{ 1, 2, 3 }", "ImmutableQueue<Int32>")]
    [InlineData("ImmutableQueue.Create(1, 2).Dequeue()", "{ 2 }", "ImmutableQueue<Int32>")]
    [InlineData("ImmutableStack.Create(1, 2).Push(9)", "{ 9, 2, 1 }", "ImmutableStack<Int32>")]
    [InlineData("ImmutableStack.Create(1, 2).Pop()", "{ 1 }", "ImmutableStack<Int32>")]
    public void Mutators_return_new_collections(string expression, string expectedValue, string expectedType)
    {
        var result = Evaluate(expression);
        Assert.Equal(ExpressionEvaluationStatus.Exact, result.Status);
        Assert.Equal(expectedType, result.StoredValueTypeName);
        Assert.Equal(expectedValue, result.ValueText);
    }

    /// <summary>The set algebra keeps the set identity and answers the relation predicates.</summary>
    [Fact]
    public void Set_algebra_folds()
    {
        Assert.Equal(
            "{ 1, 2, 3 }", Evaluate("ImmutableHashSet.Create(1, 2).Union(new[] { 2, 3 })").ValueText);
        Assert.Equal(
            "{ 2 }", Evaluate("ImmutableHashSet.Create(1, 2).Intersect(new[] { 2, 3 })").ValueText);
        Assert.Equal(
            "{ 1 }", Evaluate("ImmutableHashSet.Create(1, 2).Except(new[] { 2, 3 })").ValueText);
        Assert.Equal(
            "{ 1, 3 }", Evaluate("ImmutableHashSet.Create(1, 2).SymmetricExcept(new[] { 2, 3 })").ValueText);
        Assert.Equal(
            "ImmutableSortedSet<Int32>",
            Evaluate("ImmutableSortedSet.Create(1).Union(new[] { 3, 2 })").StoredValueTypeName);

        Assert.Equal(true, Evaluate("ImmutableHashSet.Create(1, 2).SetEquals(new[] { 2, 1, 1 })").BooleanValue);
        Assert.Equal(true, Evaluate("ImmutableHashSet.Create(1).IsSubsetOf(new[] { 1, 2 })").BooleanValue);
        Assert.Equal(true, Evaluate("ImmutableHashSet.Create(1, 2).IsProperSupersetOf(new[] { 1 })").BooleanValue);
        Assert.Equal(false, Evaluate("ImmutableHashSet.Create(1).Overlaps(new[] { 2, 3 })").BooleanValue);
    }

    /// <summary>Queues answer FIFO, stacks answer LIFO, and an empty instance stops with the BCL's exception.</summary>
    [Fact]
    public void Queue_and_stack_semantics_hold()
    {
        Assert.Equal(1, Evaluate("ImmutableQueue.Create(1, 2, 3).Peek()").Int32Value);
        Assert.Equal(3, Evaluate("ImmutableStack.Create(1, 2, 3).Peek()").Int32Value);
        Assert.Equal(2, Evaluate("ImmutableQueue.Create(1, 2, 3).Dequeue().Peek()").Int32Value);
        Assert.Equal(2, Evaluate("ImmutableStack.Create(1, 2, 3).Pop().Peek()").Int32Value);

        var emptyDequeue = Evaluate("ImmutableQueue.Create(1).Dequeue().Dequeue()");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, emptyDequeue.Status);
        Assert.Equal("System.InvalidOperationException", emptyDequeue.DiagnosticCode);
    }

    /// <summary>Property fidelity: each kind answers the properties its BCL type declares, and no others.</summary>
    [Fact]
    public void Properties_follow_each_kind()
    {
        Assert.Equal(3, Evaluate("ImmutableArray.Create(1, 2, 3).Length").Int32Value);
        Assert.Equal(2, Evaluate("ImmutableList.Create(1, 2).Count").Int32Value);
        Assert.Equal(2, Evaluate("ImmutableHashSet.Create(1, 2, 2).Count").Int32Value);
        Assert.Equal(true, Evaluate("ImmutableArray<int>.Empty.IsEmpty").BooleanValue);
        Assert.Equal(false, Evaluate("ImmutableArray.Create(1).IsDefault").BooleanValue);
        Assert.Equal(true, Evaluate("ImmutableArray<int>.Empty.IsDefaultOrEmpty").BooleanValue);
        Assert.Equal(true, Evaluate("ImmutableStack.Create<int>().IsEmpty").BooleanValue);
        Assert.Equal(1, Evaluate("ImmutableSortedSet.Create(3, 1, 2).Min").Int32Value);
        Assert.Equal(3, Evaluate("ImmutableSortedSet.Create(3, 1, 2).Max").Int32Value);

        // 'Length' belongs to ImmutableArray alone; 'Count' is not an ImmutableArray property.
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableList.Create(1).Length").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableArray.Create(1).Count").Status);

        // The LINQ Count() method still answers for every kind.
        Assert.Equal(3, Evaluate("ImmutableQueue.Create(1, 2, 3).Count()").Int32Value);
    }

    /// <summary>Indexers exist exactly where the BCL declares one.</summary>
    [Fact]
    public void Indexers_follow_each_kind()
    {
        Assert.Equal(2, Evaluate("ImmutableList.Create(1, 2, 3)[1]").Int32Value);
        Assert.Equal(9, Evaluate("ImmutableArray.Create(9, 8)[0]").Int32Value);
        Assert.Equal(1, Evaluate("ImmutableSortedSet.Create(3, 1, 2)[0]").Int32Value);

        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableHashSet.Create(1)[0]").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableQueue.Create(1)[0]").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableStack.Create(1)[0]").Status);
    }

    /// <summary>The whole LINQ surface answers over immutable receivers, exactly as IEnumerable provides it.</summary>
    [Fact]
    public void Linq_composes_over_immutable_collections()
    {
        Assert.Equal(4, Evaluate("ImmutableList.Create(1, 2, 3).Where(x => x % 2 == 1).Sum()").Int32Value);
        Assert.Equal("{ 2, 4, 6 }", Evaluate("ImmutableArray.Create(1, 2, 3).Select(x => x * 2).ToArray()").ValueText);
        Assert.Equal(3, Evaluate("ImmutableSortedSet.Create(3, 1, 2).Last()").Int32Value);
        Assert.Equal(true, Evaluate("ImmutableHashSet.Create(1, 2).Contains(2)").BooleanValue);
        Assert.Equal(
            "{ 1, 2 }",
            Evaluate("(from x in ImmutableList.Create(2, 1) orderby x select x).ToImmutableArray()").ValueText);
    }

    /// <summary>GetType, typeof, and 'is' all report the collection's constructed generic identity.</summary>
    [Fact]
    public void Type_identity_is_the_constructed_generic()
    {
        Assert.Equal(true, Evaluate("ImmutableList.Create(1) is ImmutableList<int>").BooleanValue);
        Assert.Equal(false, Evaluate("ImmutableList.Create(1) is ImmutableList<long>").BooleanValue);
        Assert.Equal(true, Evaluate("ImmutableArray.Create(1) is ImmutableArray<int>").BooleanValue);
        Assert.Equal(true, Evaluate("ImmutableList.Create(1) is IImmutableList<int>").BooleanValue);
        Assert.Equal(true, Evaluate("ImmutableHashSet.Create(1) is IImmutableSet<int>").BooleanValue);
        Assert.Equal(false, Evaluate("new[] { 1 } is ImmutableArray<int>").BooleanValue);
        Assert.Equal(true, Evaluate("new[] { 1 } is int[]").BooleanValue);

        Assert.Equal(
            true,
            Evaluate("ImmutableList.Create(1).GetType() == typeof(ImmutableList<int>)").BooleanValue);
        Assert.Equal(
            true,
            Evaluate("typeof(ImmutableList<int>).IsAssignableTo(typeof(IImmutableList<int>))").BooleanValue);
        Assert.Equal(true, Evaluate("typeof(ImmutableArray<int>).IsValueType").BooleanValue);

        // 'as' yields the value for the class kinds and null on a mismatch.
        Assert.Equal(
            ExpressionValueKind.Sequence,
            Evaluate("ImmutableList.Create(1) as ImmutableList<int>").Kind);
        Assert.Equal(
            ExpressionValueKind.Null,
            Evaluate("ImmutableList.Create(1) as ImmutableList<string>").Kind);
    }

    /// <summary>The deliberate stops: uninferable Create, culture-sensitive string ordering, bad indexes.</summary>
    [Fact]
    public void Typed_stops_stay_typed()
    {
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableArray.Create()").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableSortedSet.Create(\"b\", \"a\")").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableList.Create(1).RemoveAt(5)").Status);
        Assert.Equal(ExpressionEvaluationStatus.Invalid, Evaluate("ImmutableList.Create(1).SetItem(1, 2)").Status);

        // An unmodeled member stays a typed stop naming the member.
        var builder = Evaluate("ImmutableList.Create(1).ToBuilder()");
        Assert.Equal(ExpressionEvaluationStatus.Invalid, builder.Status);
    }
}
