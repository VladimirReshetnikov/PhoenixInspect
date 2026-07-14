namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines the minimum memory model contract required by the interpreter core.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
public interface IMemoryModel<TValue, TMem>
    where TMem : IPersistentMemoryState<TMem>
{
    /// <summary>Gets a value indicating whether this model admits deterministic object and array allocation.</summary>
    bool CanAllocate { get; }

    /// <summary>Allocates an object in a derived persistent snapshot.</summary>
    /// <param name="mem">The immutable ancestor snapshot.</param>
    /// <param name="type">The exact draft type of the new object.</param>
    /// <returns>The new reference and a descendant snapshot; <paramref name="mem"/> remains unchanged.</returns>
    (TValue objRef, TMem mem) NewObject(TMem mem, TypeSig type);

    /// <summary>Allocates a zero/default-initialized one-dimensional array in a derived snapshot.</summary>
    /// <param name="mem">The immutable ancestor snapshot.</param>
    /// <param name="elemType">The array element type.</param>
    /// <param name="length">A domain value that the model must prove is a valid non-negative length.</param>
    /// <returns>The new array reference and descendant snapshot.</returns>
    /// <exception cref="ArgumentException">The length is unknown, negative, or exceeds the model's deterministic cap.</exception>
    (TValue arrRef, TMem mem) NewArray(TMem mem, TypeSig elemType, TValue length);

    /// <summary>Loads a field observation without changing memory.</summary>
    /// <param name="mem">The snapshot to observe.</param>
    /// <param name="objRef">A non-null object reference valid in <paramref name="mem"/>.</param>
    /// <param name="field">The frozen resolved field descriptor to observe.</param>
    /// <returns>
    /// An exact typed value, a typed non-exact evidence outcome, or structured target-exception information.
    /// Implementations must never convert absent imported evidence into an allocated-object default.
    /// </returns>
    MemoryLoadResult<TValue> LoadField(TMem mem, TValue objRef, ResolvedField field);

    /// <summary>Stores a field value in a derived persistent snapshot.</summary>
    /// <param name="mem">The immutable ancestor snapshot.</param>
    /// <param name="objRef">A non-null object reference valid in <paramref name="mem"/>.</param>
    /// <param name="field">The frozen resolved field descriptor to update.</param>
    /// <param name="value">The domain value to store.</param>
    /// <returns>A descendant snapshot in which the field contains <paramref name="value"/>.</returns>
    TMem StoreField(TMem mem, TValue objRef, ResolvedField field, TValue value);

    /// <summary>Loads an in-range array element without changing memory.</summary>
    /// <param name="mem">The snapshot to observe.</param>
    /// <param name="arrRef">A non-null array reference valid in <paramref name="mem"/>.</param>
    /// <param name="index">A domain value the model must prove is an in-range integer index.</param>
    /// <returns>The element value at the requested index.</returns>
    TValue LoadElement(TMem mem, TValue arrRef, TValue index);

    /// <summary>Stores an array element in a derived persistent snapshot.</summary>
    /// <param name="mem">The immutable ancestor snapshot.</param>
    /// <param name="arrRef">A non-null array reference valid in <paramref name="mem"/>.</param>
    /// <param name="index">A provably in-range integer index.</param>
    /// <param name="value">The domain value to store.</param>
    /// <returns>A descendant snapshot containing the updated element.</returns>
    TMem StoreElement(TMem mem, TValue arrRef, TValue index, TValue value);
}
