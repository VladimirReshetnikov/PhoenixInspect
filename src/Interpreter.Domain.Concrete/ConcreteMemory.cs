using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

/// <summary>
/// Represents a deeply immutable concrete object-and-array heap snapshot.
/// </summary>
/// <remarks>
/// Allocation identifiers are monotonically assigned from snapshot state, making equal operation sequences
/// produce equal identities. Frame locals deliberately remain in <c>FrameState</c>; addressable-local semantics
/// will be added only with the first admitted by-reference opcode so the VM never has two competing local stores.
/// </remarks>
public sealed class ConcreteMemory : IPersistentMemoryState<ConcreteMemory>, IEquatable<ConcreteMemory>
{
    internal ConcreteMemory(
        long nextReferenceId,
        ImmutableDictionary<long, ConcreteObjectState> objects,
        ImmutableDictionary<long, ConcreteArrayState> arrays)
    {
        NextReferenceId = nextReferenceId;
        Objects = objects;
        Arrays = arrays;
    }

    /// <summary>Gets an empty concrete memory snapshot whose first allocation receives identity one.</summary>
    public static ConcreteMemory Empty { get; } = new(
        1,
        ImmutableDictionary<long, ConcreteObjectState>.Empty,
        ImmutableDictionary<long, ConcreteArrayState>.Empty);

    /// <summary>Gets the total number of allocated and imported object instances.</summary>
    public int ObjectCount => Objects.Count;

    /// <summary>Gets the number of objects imported from exact external evidence.</summary>
    public int ImportedObjectCount =>
        Objects.Count(static item => item.Value.Origin == ConcreteObjectOrigin.Imported);

    /// <summary>Gets the number of allocated array instances.</summary>
    public int ArrayCount => Arrays.Count;

    internal long NextReferenceId { get; }

    internal ImmutableDictionary<long, ConcreteObjectState> Objects { get; }

    internal ImmutableDictionary<long, ConcreteArrayState> Arrays { get; }

    /// <inheritdoc />
    public ConcreteMemory Fork() => this;

    /// <inheritdoc />
    public bool Equals(ConcreteMemory? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || NextReferenceId != other.NextReferenceId)
        {
            return false;
        }

        return DictionaryEquals(Objects, other.Objects, ObjectStateEquals) &&
            DictionaryEquals(Arrays, other.Arrays, ArrayStateEquals);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ConcreteMemory);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = StableContentHash.Create();
        hash.AddInt64(NextReferenceId);
        hash.AddInt32(Objects.Count);
        foreach (var item in Objects.OrderBy(static item => item.Key))
        {
            hash.AddInt64(item.Key);
            hash.AddType(item.Value.Type);
            hash.AddInt32((int)item.Value.Origin);
            hash.AddString(item.Value.ImportedEvidenceIdentity?.Value);
            hash.AddInt32(item.Value.Fields.Count);
            foreach (var field in item.Value.Fields
                .OrderBy(static field => field.Key.Module.High)
                .ThenBy(static field => field.Key.Module.Low)
                .ThenBy(static field => field.Key.MetadataToken))
            {
                hash.AddUInt64(field.Key.Module.High);
                hash.AddUInt64(field.Key.Module.Low);
                hash.AddInt32(field.Key.MetadataToken);
                hash.AddValue(field.Value);
            }
        }

        hash.AddInt32(Arrays.Count);
        foreach (var item in Arrays.OrderBy(static item => item.Key))
        {
            hash.AddInt64(item.Key);
            hash.AddType(item.Value.ElementType);
            hash.AddInt32(item.Value.Elements.Length);
            foreach (var element in item.Value.Elements)
            {
                hash.AddValue(element);
            }
        }

        return hash.ToInt32();
    }

    private static bool ObjectStateEquals(ConcreteObjectState left, ConcreteObjectState right) =>
        left.Type == right.Type &&
        left.Origin == right.Origin &&
        left.ImportedEvidenceIdentity == right.ImportedEvidenceIdentity &&
        DictionaryEquals(left.Fields, right.Fields, static (a, b) => a == b);

    private static bool ArrayStateEquals(ConcreteArrayState left, ConcreteArrayState right) =>
        left.ElementType == right.ElementType && left.Elements.SequenceEqual(right.Elements);

    private static bool DictionaryEquals<TKey, TValue>(
        ImmutableDictionary<TKey, TValue> left,
        ImmutableDictionary<TKey, TValue> right,
        Func<TValue, TValue, bool> valueEquals)
        where TKey : notnull
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var item in left)
        {
            if (!right.TryGetValue(item.Key, out var otherValue) || !valueEquals(item.Value, otherValue))
            {
                return false;
            }
        }

        return true;
    }

    private struct StableContentHash
    {
        private const uint OffsetBasis = 2166136261;
        private const uint Prime = 16777619;
        private uint value;

        public static StableContentHash Create() => new() { value = OffsetBasis };

        public readonly int ToInt32() => unchecked((int)value);

        public void AddInt32(int item) => AddUInt32(unchecked((uint)item));

        public void AddInt64(long item) => AddUInt64(unchecked((ulong)item));

        public void AddUInt64(ulong item)
        {
            AddUInt32(unchecked((uint)item));
            AddUInt32(unchecked((uint)(item >> 32)));
        }

        public void AddString(string? item)
        {
            if (item is null)
            {
                AddInt32(-1);
                return;
            }

            AddInt32(item.Length);
            foreach (var character in item)
            {
                AddUInt32(character);
            }
        }

        public void AddValue(ConcreteValue item)
        {
            AddInt32((int)item.Kind);
            AddType(item.StaticType);
            switch (item.Payload)
            {
                case null:
                    AddInt32(0);
                    break;
                case int int32:
                    AddInt32(int32);
                    break;
                case long int64:
                    AddInt64(int64);
                    break;
                case bool boolean:
                    AddInt32(boolean ? 1 : 0);
                    break;
                case string text:
                    AddString(text);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected concrete payload type {item.Payload.GetType().FullName}.");
            }
        }

        public void AddType(TypeSig type)
        {
            AddInt32((int)type.Kind);
            switch (type.Kind)
            {
                case TypeSigKind.Void:
                    break;
                case TypeSigKind.Intrinsic:
                    AddInt32((int)type.IntrinsicKind!);
                    break;
                case TypeSigKind.TypeDefinition:
                    AddUInt64(type.Module!.Value.High);
                    AddUInt64(type.Module.Value.Low);
                    AddInt32(type.MetadataToken);
                    break;
                case TypeSigKind.Synthetic:
                    AddString(type.DisplayName);
                    break;
                case TypeSigKind.SzArray:
                    AddType(type.ElementType!);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected structural type kind {type.Kind}.");
            }
        }

        private void AddUInt32(uint item)
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                value = unchecked((value ^ ((item >> shift) & 0xFF)) * Prime);
            }
        }
    }
}

internal sealed record ConcreteObjectState(
    TypeSig Type,
    ConcreteObjectOrigin Origin,
    ImportedObjectEvidenceIdentity? ImportedEvidenceIdentity,
    ImmutableDictionary<FieldHandle, ConcreteValue> Fields);

internal enum ConcreteObjectOrigin
{
    Allocated,
    Imported,
}

internal sealed record ConcreteArrayState(
    TypeSig ElementType,
    ImmutableArray<ConcreteValue> Elements);
