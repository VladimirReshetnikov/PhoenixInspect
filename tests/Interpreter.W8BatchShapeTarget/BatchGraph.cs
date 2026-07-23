namespace Interpreter.W8BatchShapeTarget;

/// <summary>Names the outer key argument of the batch-shape nested ledger.</summary>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public sealed class BatchKey
{
    /// <summary>Initializes a batch key with a stable fixture label.</summary>
    /// <param name="label">The explicitly retained fixture label.</param>
    public BatchKey(string label) => Label = label;

    /// <summary>Gets the fixture label.</summary>
    public string Label { get; }
}

/// <summary>Names the inner value argument of the batch-shape nested ledger.</summary>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public sealed class BatchValue
{
    /// <summary>Initializes a batch value with a stable fixture label.</summary>
    /// <param name="label">The explicitly retained fixture label.</param>
    public BatchValue(string label) => Label = label;

    /// <summary>Gets the fixture label.</summary>
    public string Label { get; }
}

/// <summary>Supplies one recursive wrapper used to exceed the closed-construction depth cap.</summary>
/// <typeparam name="TInner">The wrapped argument at the next construction level.</typeparam>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public sealed class Wrap<TInner>
    where TInner : class
{
    /// <summary>Initializes the wrapper witness.</summary>
    public Wrap()
    {
    }
}

/// <summary>Owns the single-parameter batch ledger whose closed constructions the corpus counts.</summary>
/// <typeparam name="TKey">The closed key argument that selects one construction.</typeparam>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public static class Ledger<TKey>
    where TKey : class
{
    /// <summary>Stores the per-construction ledger entry count.</summary>
    public static int Count;
}

/// <summary>Owns the two-level nested batch ledger whose segments carry separate arity.</summary>
/// <typeparam name="TKey">The outer segment argument.</typeparam>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public static class Outer<TKey>
    where TKey : class
{
    /// <summary>Owns the inner segment of the nested batch ledger.</summary>
    /// <typeparam name="TValue">The inner segment argument.</typeparam>
    /// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
    public static class Inner<TValue>
        where TValue : class
    {
        /// <summary>Stores the per-construction nested entry count.</summary>
        public static int Count;

        /// <summary>Stores the inner segment's own retained value.</summary>
        public static TValue? Current;
    }
}

/// <summary>Names the nested reference head a <c>using static</c> import exposes to a suffix pipeline.</summary>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public sealed class ImportedNested
{
    /// <summary>Initializes the nested reference head.</summary>
    /// <param name="label">The explicitly retained fixture label.</param>
    public ImportedNested(string label) => Label = label;

    /// <summary>Stores the fixture label read by the direct-member suffix.</summary>
    public readonly string Label;
}

/// <summary>Supplies the batch-shape <c>using static</c> import surface.</summary>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public static class BatchImports
{
    /// <summary>Stores the bare-spelled nested reference head.</summary>
    public static ImportedNested? ImportedNestedCurrent;

    /// <summary>Stores the bare-spelled pending count whose blocker catalog stays incomplete.</summary>
    public static int PendingCount;
}
