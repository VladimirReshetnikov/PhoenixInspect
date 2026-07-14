namespace Interpreter.Host.Abstractions;

/// <summary>
/// Describes how much of a requested process-memory range was present in an immutable snapshot.
/// </summary>
public enum MemoryReadStatus
{
    /// <summary>
    /// Every requested byte was read from the snapshot.
    /// </summary>
    Exact,

    /// <summary>
    /// A non-empty prefix was read, but at least one requested suffix byte was unavailable.
    /// </summary>
    Partial,

    /// <summary>
    /// No requested byte was available from the snapshot.
    /// </summary>
    Unavailable,
}
