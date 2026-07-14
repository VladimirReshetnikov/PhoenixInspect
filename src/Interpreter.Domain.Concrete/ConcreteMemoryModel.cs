using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

/// <summary>
/// Implements deterministic, persistent object, field, and one-dimensional array operations for
/// <see cref="ConcreteValue"/>.
/// </summary>
/// <remarks>
/// This is a semantic-validation heap, not a CLR object-layout emulator. Field reads become exact after an explicit
/// store; an unstored field yields canonical top because the current <see cref="FieldHandle"/> does not carry its
/// declared type or default-layout evidence. Addressable locals and managed references are deliberately absent
/// until an admitted by-reference opcode forces one coherent frame/memory aliasing design.
/// </remarks>
public sealed class ConcreteMemoryModel : IMemoryModel<ConcreteValue, ConcreteMemory>
{
    /// <summary>
    /// Gets the largest array length materialized by the bounded validation model.
    /// </summary>
    /// <remarks>The cap prevents hostile or accidental fixture values from causing analyzer-scale allocations.</remarks>
    public const int MaxArrayLength = 4096;

    private static readonly TypeSig UnknownFieldType = new("<unknown-field-type>");
    private readonly ConcreteDomain domain;

    /// <summary>
    /// Creates a persistent concrete memory model over the supplied value domain.
    /// </summary>
    /// <param name="domain">The concrete value domain used for references, defaults, and unknown values.</param>
    public ConcreteMemoryModel(ConcreteDomain domain)
    {
        this.domain = domain ?? throw new ArgumentNullException(nameof(domain));
    }

    /// <inheritdoc />
    public bool CanAllocate => true;

    /// <inheritdoc />
    public (ConcreteValue objRef, ConcreteMemory mem) NewObject(ConcreteMemory mem, TypeSig type)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(type);
        var id = mem.NextReferenceId;
        var next = new ConcreteMemory(
            checked(id + 1),
            mem.Objects.Add(id, new ConcreteObjectState(type, ImmutableDictionary<FieldHandle, ConcreteValue>.Empty)),
            mem.Arrays);
        return (domain.ObjectReference(id, type), next);
    }

    /// <inheritdoc />
    public (ConcreteValue arrRef, ConcreteMemory mem) NewArray(
        ConcreteMemory mem,
        TypeSig elemType,
        ConcreteValue length)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(elemType);
        if (!domain.TryGetConstInt32(length, out var concreteLength) ||
            concreteLength < 0 ||
            concreteLength > MaxArrayLength)
        {
            throw new ArgumentException(
                $"Concrete array allocation requires a known Int32 length between zero and {MaxArrayLength}.",
                nameof(length));
        }

        var id = mem.NextReferenceId;
        var elements = ImmutableArray.CreateRange(
            Enumerable.Repeat(domain.DefaultValue(elemType), concreteLength));
        var next = new ConcreteMemory(
            checked(id + 1),
            mem.Objects,
            mem.Arrays.Add(id, new ConcreteArrayState(elemType, elements)));
        return (domain.ArrayReference(id, elemType), next);
    }

    /// <inheritdoc />
    public ConcreteValue LoadField(ConcreteMemory mem, ConcreteValue objRef, FieldHandle field)
    {
        var state = GetObject(RequireMemory(mem), objRef);
        return state.Fields.TryGetValue(field, out var value)
            ? value
            : domain.Top(UnknownFieldType);
    }

    /// <inheritdoc />
    public ConcreteMemory StoreField(
        ConcreteMemory mem,
        ConcreteValue objRef,
        FieldHandle field,
        ConcreteValue value)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(value);
        var id = GetReferenceId(objRef, ConcreteValueKind.ObjectReference);
        var state = GetObject(mem, objRef);
        return new ConcreteMemory(
            mem.NextReferenceId,
            mem.Objects.SetItem(id, state with { Fields = state.Fields.SetItem(field, value) }),
            mem.Arrays);
    }

    /// <inheritdoc />
    public ConcreteValue LoadElement(ConcreteMemory mem, ConcreteValue arrRef, ConcreteValue index)
    {
        var state = GetArray(RequireMemory(mem), arrRef);
        var concreteIndex = GetIndex(index, state.Elements.Length);
        return state.Elements[concreteIndex];
    }

    /// <inheritdoc />
    public ConcreteMemory StoreElement(
        ConcreteMemory mem,
        ConcreteValue arrRef,
        ConcreteValue index,
        ConcreteValue value)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(value);
        var id = GetReferenceId(arrRef, ConcreteValueKind.ArrayReference);
        var state = GetArray(mem, arrRef);
        if (value.StaticType != state.ElementType)
        {
            throw new ArgumentException(
                $"Cannot store {value.StaticType.DisplayName} in an array of {state.ElementType.DisplayName}.",
                nameof(value));
        }

        var concreteIndex = GetIndex(index, state.Elements.Length);
        return new ConcreteMemory(
            mem.NextReferenceId,
            mem.Objects,
            mem.Arrays.SetItem(id, state with { Elements = state.Elements.SetItem(concreteIndex, value) }));
    }

    private ConcreteObjectState GetObject(ConcreteMemory mem, ConcreteValue objRef)
    {
        if (objRef.Kind == ConcreteValueKind.Null)
        {
            throw new NullReferenceException("Cannot access a field through null.");
        }

        var id = GetReferenceId(objRef, ConcreteValueKind.ObjectReference);
        if (!mem.Objects.TryGetValue(id, out var state))
        {
            throw new InvalidOperationException($"Object reference {id} does not exist in this snapshot.");
        }

        if (objRef.StaticType != state.Type)
        {
            throw new InvalidOperationException(
                $"Object reference {id} claims type {objRef.StaticType.DisplayName}, but this snapshot records {state.Type.DisplayName}.");
        }

        return state;
    }

    private ConcreteArrayState GetArray(ConcreteMemory mem, ConcreteValue arrRef)
    {
        if (arrRef.Kind == ConcreteValueKind.Null)
        {
            throw new NullReferenceException("Cannot access an array through null.");
        }

        var id = GetReferenceId(arrRef, ConcreteValueKind.ArrayReference);
        if (!mem.Arrays.TryGetValue(id, out var state))
        {
            throw new InvalidOperationException($"Array reference {id} does not exist in this snapshot.");
        }

        var expectedType = $"{state.ElementType.DisplayName}[]";
        if (!string.Equals(arrRef.StaticType.DisplayName, expectedType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Array reference {id} claims type {arrRef.StaticType.DisplayName}, but this snapshot records {expectedType}.");
        }

        return state;
    }

    private int GetIndex(ConcreteValue index, int length)
    {
        if (!domain.TryGetConstInt32(index, out var concreteIndex))
        {
            throw new ArgumentException("Concrete array access requires a known Int32 index.", nameof(index));
        }

        if ((uint)concreteIndex >= (uint)length)
        {
            throw new IndexOutOfRangeException($"Array index {concreteIndex} is outside length {length}.");
        }

        return concreteIndex;
    }

    private static long GetReferenceId(ConcreteValue value, ConcreteValueKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind != expectedKind || !value.TryGetReferenceId(out var id))
        {
            throw new ArgumentException($"Expected {expectedKind}, got {value.Kind}.", nameof(value));
        }

        return id;
    }

    private static ConcreteMemory RequireMemory(ConcreteMemory mem) =>
        mem ?? throw new ArgumentNullException(nameof(mem));
}
