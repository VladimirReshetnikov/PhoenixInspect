using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines the minimum memory model contract required by the interpreter core.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
public interface IMemoryModel<TValue, TMem>
{
    /// <summary>Gets a value indicating whether the model supports object/array allocation.</summary>
    bool CanAllocate { get; }

    /// <summary>Allocates a new object instance.</summary>
    (TValue objRef, TMem mem) NewObject(TMem mem, TypeSig type);

    /// <summary>Allocates a new array instance.</summary>
    (TValue arrRef, TMem mem) NewArray(TMem mem, TypeSig elemType, TValue length);

    /// <summary>Loads a field value.</summary>
    TValue LoadField(TMem mem, TValue objRef, FieldHandle field);

    /// <summary>Stores a field value.</summary>
    TMem StoreField(TMem mem, TValue objRef, FieldHandle field, TValue value);

    /// <summary>Loads an array element value.</summary>
    TValue LoadElement(TMem mem, TValue arrRef, TValue index);

    /// <summary>Stores an array element value.</summary>
    TMem StoreElement(TMem mem, TValue arrRef, TValue index, TValue value);

    /// <summary>Creates a by-reference value targeting an object field.</summary>
    TValue MakeByRefToField(TMem mem, TValue objRef, FieldHandle field);

    /// <summary>Creates a by-reference value targeting a local slot.</summary>
    TValue MakeByRefToLocal(int localIndex);

    /// <summary>Loads through a by-reference value.</summary>
    TValue LoadIndirect(TMem mem, TValue byRef);

    /// <summary>Stores through a by-reference value.</summary>
    TMem StoreIndirect(TMem mem, TValue byRef, TValue value);

    /// <summary>Conservatively invalidates a memory region.</summary>
    TMem Havoc(TMem mem, HavocRegion region, UnknownOrigin origin);
}
