using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Carries counted dump-memory evidence for one supported <see cref="Nullable{T}"/> field specialized with
/// <see cref="int"/>.
/// </summary>
/// <remarks>
/// Exact null is represented by an exact <c>hasValue == false</c> read and no value-payload read. A missing or short
/// payload never becomes a fabricated integer, while a missing flag never becomes null.
/// </remarks>
public sealed class ClrmdNullableInt32FieldObservation
{
    internal ClrmdNullableInt32FieldObservation(
        ClrmdInstanceFieldInfo field,
        MemoryReadResult hasValueMemory,
        MemoryReadResult? valueMemory,
        bool? hasValue,
        int? value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(hasValueMemory);
        if (hasValueMemory.RequestedLength != sizeof(byte))
        {
            throw new ArgumentException(
                "A nullable Int32 flag observation must request exactly one byte.",
                nameof(hasValueMemory));
        }

        if (hasValue is not null && hasValueMemory.Status != MemoryReadStatus.Exact)
        {
            throw new ArgumentException(
                "A nullable Int32 flag can be decoded only from an exact byte.",
                nameof(hasValue));
        }

        if (valueMemory is not null && valueMemory.RequestedLength != sizeof(int))
        {
            throw new ArgumentException(
                "A nullable Int32 payload observation must request exactly four bytes.",
                nameof(valueMemory));
        }

        if (hasValue == false && (valueMemory is not null || value is not null))
        {
            throw new ArgumentException(
                "An exact null observation cannot contain a value-payload read or decoded value.",
                nameof(valueMemory));
        }

        if (value is not null &&
            (hasValue != true || valueMemory?.Status != MemoryReadStatus.Exact))
        {
            throw new ArgumentException(
                "A nullable Int32 value requires an exact true flag and exact payload read.",
                nameof(value));
        }

        Field = field;
        HasValueMemory = hasValueMemory;
        ValueMemory = valueMemory;
        HasValue = hasValue;
        Value = value;
    }

    /// <summary>
    /// Gets the immutable outer-field descriptor selected for this observation.
    /// </summary>
    public ClrmdInstanceFieldInfo Field { get; }

    /// <summary>
    /// Gets the counted one-byte read of the nested <c>hasValue</c> field.
    /// </summary>
    public MemoryReadResult HasValueMemory { get; }

    /// <summary>
    /// Gets the counted four-byte read of the nested <c>value</c> field when an exact true flag required that read.
    /// </summary>
    /// <remarks>
    /// This property is <see langword="null"/> for exact null and whenever decoding stopped before the payload read.
    /// </remarks>
    public MemoryReadResult? ValueMemory { get; }

    /// <summary>
    /// Gets the decoded nullable presence flag when its byte was exact and canonical; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public bool? HasValue { get; }

    /// <summary>
    /// Gets whether exact flag evidence proves that the nullable field is null.
    /// </summary>
    public bool IsNull => HasValue == false;

    /// <summary>
    /// Gets the little-endian signed value only when both the true flag and all four payload bytes were exact.
    /// </summary>
    public int? Value { get; }
}
