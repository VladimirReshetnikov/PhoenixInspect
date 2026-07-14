using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Carries an Int32 field location, its counted raw dump-memory read, and a value only when all four bytes were exact.
/// </summary>
public sealed class ClrmdInt32FieldObservation
{
    internal ClrmdInt32FieldObservation(
        ClrmdInstanceFieldInfo field,
        MemoryReadResult memory,
        int? value)
    {
        Field = field;
        Memory = memory;
        Value = value;
    }

    /// <summary>
    /// Gets the runtime-selected field identity and target storage address.
    /// </summary>
    public ClrmdInstanceFieldInfo Field { get; }

    /// <summary>
    /// Gets the raw four-byte read used as the sole source of the decoded value.
    /// </summary>
    public MemoryReadResult Memory { get; }

    /// <summary>
    /// Gets the little-endian target value when <see cref="Memory"/> is exact; otherwise <see langword="null"/>.
    /// </summary>
    public int? Value { get; }
}
