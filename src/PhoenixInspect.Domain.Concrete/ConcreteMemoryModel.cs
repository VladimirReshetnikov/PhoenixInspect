using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Domain.Concrete;

/// <summary>
/// Implements deterministic persistent objects, imported field evidence, and one-dimensional arrays for
/// <see cref="ConcreteValue"/>.
/// </summary>
/// <remarks>
/// Freshly allocated objects and externally imported objects have deliberately different missing-field semantics.
/// An allocated field receives its CLI default from <see cref="IValueDomain{TValue}.DefaultValue"/>, while an absent
/// imported field is unavailable because no target observation established its bytes. The model is a semantic
/// validation heap, not a CLR object-layout emulator.
/// </remarks>
public sealed class ConcreteMemoryModel : IMemoryModel<ConcreteValue, ConcreteMemory>
{
    /// <summary>Gets the largest array length materialized by the bounded validation model.</summary>
    /// <remarks>The cap prevents accidental fixture values from causing analyzer-scale allocations.</remarks>
    public const int MaxArrayLength = 4096;

    private readonly ConcreteDomain domain;

    /// <summary>Creates a persistent concrete memory model over the supplied value domain.</summary>
    /// <param name="domain">The concrete value domain used for references and CLI defaults.</param>
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
        if (type.Kind is TypeSigKind.Void or TypeSigKind.SzArray ||
            type.Kind == TypeSigKind.Intrinsic && type.IntrinsicKind != IntrinsicTypeKind.Object)
        {
            throw new ArgumentException("Object allocation requires an object-reference type.", nameof(type));
        }

        var id = mem.NextReferenceId;
        var next = new ConcreteMemory(
            checked(id + 1),
            mem.Objects.Add(
                id,
                new ConcreteObjectState(
                    type,
                    ConcreteObjectOrigin.Allocated,
                    ImportedEvidenceIdentity: null,
                    ImmutableDictionary<FieldHandle, ConcreteValue>.Empty)),
            mem.Arrays);
        return (domain.ObjectReference(id, type), next);
    }

    /// <summary>
    /// Imports one exact metadata-typed external object while retaining its canonical evidence identity.
    /// </summary>
    /// <param name="mem">The immutable ancestor snapshot.</param>
    /// <param name="type">The exact metadata TypeDef observed for the external object.</param>
    /// <param name="evidenceIdentity">
    /// The bounded snapshot/object evidence identity selected by the dump-preparation layer.
    /// </param>
    /// <returns>
    /// The deterministic object reference and descendant snapshot. Reimporting equal evidence with the same type
    /// is idempotent and returns the existing reference and snapshot.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mem"/>, <paramref name="type"/>, or <paramref name="evidenceIdentity"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not an exact metadata TypeDef.</exception>
    /// <exception cref="InvalidOperationException">
    /// The same evidence identity is already bound to an incompatible object type.
    /// </exception>
    public (ConcreteValue objRef, ConcreteMemory mem) ImportObject(
        ConcreteMemory mem,
        TypeSig type,
        ImportedObjectEvidenceIdentity evidenceIdentity)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(evidenceIdentity);
        if (!type.IsMetadataTypeDefinition)
        {
            throw new ArgumentException(
                "External object import requires an exact metadata TypeDef identity.",
                nameof(type));
        }

        foreach (var item in mem.Objects)
        {
            if (item.Value.ImportedEvidenceIdentity != evidenceIdentity)
            {
                continue;
            }

            if (item.Value.Type != type)
            {
                throw new InvalidOperationException(
                    "An external-object evidence identity is already bound to a different structural type.");
            }

            return (domain.ObjectReference(item.Key, type), mem);
        }

        var id = mem.NextReferenceId;
        var next = new ConcreteMemory(
            checked(id + 1),
            mem.Objects.Add(
                id,
                new ConcreteObjectState(
                    type,
                    ConcreteObjectOrigin.Imported,
                    evidenceIdentity,
                    ImmutableDictionary<FieldHandle, ConcreteValue>.Empty)),
            mem.Arrays);
        return (domain.ObjectReference(id, type), next);
    }

    /// <summary>
    /// Populates one imported field from an exact, owner-validated external observation.
    /// </summary>
    /// <param name="mem">The immutable ancestor snapshot containing the imported object.</param>
    /// <param name="objRef">The exact imported-object reference.</param>
    /// <param name="field">The frozen instance-field descriptor correlated by the preparation layer.</param>
    /// <param name="value">The exact concrete value observed for the field.</param>
    /// <returns>
    /// A descendant snapshot carrying the exact field cell. Reimporting the same cell is idempotent.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The reference is not an imported object, owner/type/storage facts disagree, or <paramref name="value"/> is
    /// bottom or unknown rather than exact evidence.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The field already carries a different exact value.
    /// </exception>
    public ConcreteMemory ImportField(
        ConcreteMemory mem,
        ConcreteValue objRef,
        ResolvedField field,
        ConcreteValue value)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);
        var id = GetReferenceId(objRef, ConcreteValueKind.ObjectReference);
        var state = GetObjectForMutation(mem, objRef);
        if (state.Origin != ConcreteObjectOrigin.Imported)
        {
            throw new ArgumentException("Exact external field evidence requires an imported object.", nameof(objRef));
        }

        ValidateWritableField(state, field, value);
        if (value.Kind is ConcreteValueKind.Bottom or ConcreteValueKind.Unknown)
        {
            throw new ArgumentException(
                "Imported field evidence must be one exact concrete value.",
                nameof(value));
        }

        if (state.Fields.TryGetValue(field.Handle, out var existing))
        {
            if (existing != value)
            {
                throw new InvalidOperationException(
                    "Conflicting exact values cannot occupy one imported field evidence cell.");
            }

            return mem;
        }

        return new ConcreteMemory(
            mem.NextReferenceId,
            mem.Objects.SetItem(id, state with { Fields = state.Fields.Add(field.Handle, value) }),
            mem.Arrays);
    }

    /// <summary>Tries to recover the retained evidence identity of an imported object reference.</summary>
    /// <param name="mem">The persistent snapshot containing the reference.</param>
    /// <param name="objRef">An object reference issued by this model.</param>
    /// <param name="evidenceIdentity">
    /// Receives the retained identity for an imported object, or <see langword="null"/> for an allocated object.
    /// </param>
    /// <returns><see langword="true"/> exactly when <paramref name="objRef"/> identifies an imported object.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="objRef"/> is not an object reference or is absent/inconsistent in <paramref name="mem"/>.
    /// </exception>
    public bool TryGetImportedObjectEvidenceIdentity(
        ConcreteMemory mem,
        ConcreteValue objRef,
        out ImportedObjectEvidenceIdentity? evidenceIdentity)
    {
        var state = GetObjectForMutation(RequireMemory(mem), objRef);
        evidenceIdentity = state.ImportedEvidenceIdentity;
        return state.Origin == ConcreteObjectOrigin.Imported;
    }

    /// <inheritdoc />
    public (ConcreteValue arrRef, ConcreteMemory mem) NewArray(
        ConcreteMemory mem,
        TypeSig elemType,
        ConcreteValue length)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(elemType);
        if (elemType.Kind == TypeSigKind.Void)
        {
            throw new ArgumentException("An array element type cannot be void.", nameof(elemType));
        }

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
    public MemoryLoadResult<ConcreteValue> LoadField(
        ConcreteMemory mem,
        ConcreteValue objRef,
        ResolvedField field)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(objRef);
        ArgumentNullException.ThrowIfNull(field);

        if (field.IsStatic || field.IsLiteral || field.HasRva)
        {
            return MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.Invalid,
                "MEMORY_FIELD_STORAGE_UNSUPPORTED");
        }

        if (objRef.StaticType != field.DeclaringType)
        {
            return MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.Invalid,
                "MEMORY_FIELD_OWNER_MISMATCH");
        }

        if (objRef.Kind == ConcreteValueKind.Null)
        {
            return MemoryLoadResult<ConcreteValue>.ForTargetException(
                new TargetExceptionInfo(TargetExceptionKind.NullReference, "TARGET_NULL_REFERENCE"));
        }

        if (objRef.Kind != ConcreteValueKind.ObjectReference || !objRef.TryGetReferenceId(out var id))
        {
            return MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.Invalid,
                "MEMORY_RECEIVER_NOT_OBJECT");
        }

        if (!mem.Objects.TryGetValue(id, out var state))
        {
            return MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.Invalid,
                "MEMORY_RECEIVER_NOT_IN_SNAPSHOT");
        }

        if (state.Type != objRef.StaticType || state.Type != field.DeclaringType)
        {
            return MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.Invalid,
                "MEMORY_FIELD_OWNER_MISMATCH");
        }

        if (state.Fields.TryGetValue(field.Handle, out var value))
        {
            return value.StaticType == field.FieldType
                ? MemoryLoadResult<ConcreteValue>.Exact(value)
                : MemoryLoadResult<ConcreteValue>.NonExact(
                    MemoryLoadKind.Invalid,
                    "MEMORY_FIELD_VALUE_TYPE_MISMATCH");
        }

        if (state.Origin == ConcreteObjectOrigin.Imported)
        {
            return MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.Unavailable,
                "MEMORY_IMPORTED_FIELD_UNAVAILABLE");
        }

        try
        {
            return MemoryLoadResult<ConcreteValue>.Exact(domain.DefaultValue(field.FieldType));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.Invalid,
                "MEMORY_FIELD_DEFAULT_UNSUPPORTED");
        }
    }

    /// <inheritdoc />
    public ConcreteMemory StoreField(
        ConcreteMemory mem,
        ConcreteValue objRef,
        ResolvedField field,
        ConcreteValue value)
    {
        mem = RequireMemory(mem);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);
        var id = GetReferenceId(objRef, ConcreteValueKind.ObjectReference);
        var state = GetObjectForMutation(mem, objRef);
        ValidateWritableField(state, field, value);
        return new ConcreteMemory(
            mem.NextReferenceId,
            mem.Objects.SetItem(id, state with { Fields = state.Fields.SetItem(field.Handle, value) }),
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

    private static void ValidateWritableField(
        ConcreteObjectState state,
        ResolvedField field,
        ConcreteValue value)
    {
        if (field.IsStatic || field.IsLiteral || field.HasRva)
        {
            throw new ArgumentException(
                "The concrete object heap accepts only ordinary instance fields.",
                nameof(field));
        }

        if (state.Type != field.DeclaringType)
        {
            throw new ArgumentException(
                "The field declaring type does not exactly match the receiver object type.",
                nameof(field));
        }

        if (value.StaticType != field.FieldType)
        {
            throw new ArgumentException(
                $"Cannot store {value.StaticType.DisplayName} in a field of {field.FieldType.DisplayName}.",
                nameof(value));
        }
    }

    private static ConcreteObjectState GetObjectForMutation(ConcreteMemory mem, ConcreteValue objRef)
    {
        var id = GetReferenceId(objRef, ConcreteValueKind.ObjectReference);
        if (!mem.Objects.TryGetValue(id, out var state))
        {
            throw new ArgumentException("The object reference does not exist in this snapshot.", nameof(objRef));
        }

        if (objRef.StaticType != state.Type)
        {
            throw new ArgumentException(
                "The object reference structural type differs from the snapshot object type.",
                nameof(objRef));
        }

        return state;
    }

    private static ConcreteArrayState GetArray(ConcreteMemory mem, ConcreteValue arrRef)
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

        var expectedType = TypeSig.CreateSzArray(state.ElementType);
        if (arrRef.StaticType != expectedType)
        {
            throw new InvalidOperationException(
                $"Array reference {id} claims type {arrRef.StaticType.DisplayName}, but this snapshot records {expectedType.DisplayName}.");
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
