namespace Interpreter.W8RequestShapeTarget;

/// <summary>Names the request-shape reference argument of the single-parameter slot family.</summary>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public sealed class RequestContext
{
    /// <summary>Initializes a request context with a stable fixture label.</summary>
    /// <param name="label">The explicitly retained fixture label.</param>
    public RequestContext(string label) => Label = label;

    /// <summary>Gets the fixture label.</summary>
    public string Label { get; }
}

/// <summary>Names a second reference argument that must select a different construction and slot.</summary>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public sealed class BatchContext
{
    /// <summary>Initializes a batch context with a stable fixture label.</summary>
    /// <param name="label">The explicitly retained fixture label.</param>
    public BatchContext(string label) => Label = label;

    /// <summary>Gets the fixture label.</summary>
    public string Label { get; }
}

/// <summary>Names an argument the target deliberately never constructs.</summary>
/// <remarks>The absent construction is the point of this draft fixture type.</remarks>
public sealed class NeverConstructedContext
{
    /// <summary>Initializes the never-constructed argument witness.</summary>
    public NeverConstructedContext()
    {
    }
}

/// <summary>Owns the request-shape per-construction static storage the corpus reads.</summary>
/// <typeparam name="TContext">The closed reference argument that selects one construction.</typeparam>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public static class RequestSlot<TContext>
    where TContext : class
{
    /// <summary>Stores the per-construction request sentinel read by the qualified route.</summary>
    public static int Sentinel;

    /// <summary>Stores the per-construction, per-thread request sentinel.</summary>
    [ThreadStatic]
    public static int ThreadSentinel;

    /// <summary>Stores the per-construction current reference whose declared type is <c>VAR 0</c>.</summary>
    public static TContext? Current;
}

/// <summary>Supplies the constructed <c>using static</c> import surface of the request shape.</summary>
/// <typeparam name="TContext">The closed argument of the imported constructed head.</typeparam>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public static class RequestImports<TContext>
    where TContext : class
{
    /// <summary>Stores the bare-spelled imported sentinel.</summary>
    public static int ImportedSentinel;

    /// <summary>Stores the imported sentinel an active local of the same spelling shadows.</summary>
    public static int ShadowedSentinel;
}

/// <summary>Owns the outer-scope declaration of the shadowed alias pair.</summary>
/// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
public static class OuterScopedSlot
{
    /// <summary>Stores the outer-scope sentinel that the inner alias must not reach.</summary>
    public static int Sentinel;
}
