namespace Interpreter.Core.Abstractions;

/// <summary>
/// Marks a memory-state representation that can produce an independently evolvable snapshot.
/// </summary>
/// <typeparam name="TSelf">The concrete persistent-state type.</typeparam>
/// <remarks>
/// Interpreter states and branch forks rely on value semantics: changing a derived snapshot must never mutate an
/// earlier snapshot. Implementations may return <see langword="this"/> from <see cref="Fork"/> when deeply
/// immutable, or create a copy-on-write branch token when internal storage requires it.
/// </remarks>
public interface IPersistentMemoryState<TSelf>
    where TSelf : IPersistentMemoryState<TSelf>
{
    /// <summary>
    /// Produces a snapshot that may evolve independently from the current state.
    /// </summary>
    /// <returns>An immutable or copy-on-write branch preserving all current memory observations.</returns>
    TSelf Fork();
}
