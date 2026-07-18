namespace Interpreter.W8AliasTarget;

/// <summary>
/// Supplies a distinct assembly-owned type argument for W8 extern-alias and AssemblyRef evidence.
/// </summary>
/// <remarks>
/// This emitted fixture type is a draft physical oracle. It is not a product contract.
/// </remarks>
public sealed class ExternalRequestContext
{
    /// <summary>Initializes a context with a stable fixture label.</summary>
    /// <param name="label">The explicitly initialized label retained by the target.</param>
    public ExternalRequestContext(string label)
    {
        Label = label;
    }

    /// <summary>Gets the fixture label.</summary>
    public string Label { get; }
}

/// <summary>
/// Supplies a generic static owner that is reachable only through the W8 target's explicit extern alias.
/// </summary>
/// <typeparam name="T">The exact closed argument distinguishing one runtime construction.</typeparam>
/// <remarks>
/// The fields exist only to establish emitted metadata, runtime construction, slot, and value facts.
/// </remarks>
public static class ExternalSlot<T>
{
    /// <summary>Stores the construction-specific integer sentinel.</summary>
    public static int Sentinel;

    /// <summary>Stores the construction-specific reference or value.</summary>
    public static T? Current;

    /// <summary>Provides a runtime-free metadata literal on an assembly-qualified generic owner.</summary>
    public const int Literal = 0x18A17A5;
}

/// <summary>
/// Supplies an external generic interface definition with a construction-specific stored static field.
/// </summary>
/// <typeparam name="T">The exact closed argument distinguishing the interface construction.</typeparam>
/// <remarks>This is an emitted W8 truth-gate fixture, not a reusable interface.</remarks>
public interface IExternalInterfaceSlot<T>
{
    /// <summary>Stores the construction-specific interface sentinel.</summary>
    public static int Sentinel = 0x18A17A6;
}
