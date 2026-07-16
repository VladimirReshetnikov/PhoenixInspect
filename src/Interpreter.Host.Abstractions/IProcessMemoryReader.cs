namespace Interpreter.Host.Abstractions;

/// <summary>
/// Reads immutable byte evidence from a captured process address space.
/// </summary>
/// <remarks>
/// This draft contract deliberately reports exact byte counts instead of a Boolean success flag. Dumps are sparse,
/// and a short read is materially different from both a complete read and a wholly unavailable range. Callers must
/// not decode a scalar value unless the corresponding <see cref="MemoryReadResult.Status"/> is
/// <see cref="MemoryReadStatus.Exact"/>.
/// </remarks>
public interface IProcessMemoryReader
{
    /// <summary>
    /// Gets the target process pointer size in bytes.
    /// </summary>
    /// <remarks>
    /// This is the pointer size of the captured target, not necessarily the pointer size of the analysis process.
    /// </remarks>
    int PointerSize { get; }

    /// <summary>
    /// Gets the largest byte count accepted by one read operation.
    /// </summary>
    /// <remarks>
    /// The cap is deterministic and rejects unexpectedly large allocation requests. Caveat: callers must chunk larger
    /// logical reads and preserve each chunk's completeness separately.
    /// </remarks>
    int MaximumReadLength { get; }

    /// <summary>
    /// Gets the stable identifier of the immutable memory source used for read provenance.
    /// </summary>
    /// <remarks>
    /// A dump-backed implementation should derive this value from dump content rather than from a local path. The
    /// identifier is evidence provenance; it is not a runtime object or module identity by itself.
    /// </remarks>
    string SourceId { get; }

    /// <summary>
    /// Reads a bounded prefix of a process-memory range and reports whether the requested bytes were fully available.
    /// </summary>
    /// <param name="address">The first target virtual address to read.</param>
    /// <param name="length">The requested number of bytes. Zero is allowed and produces an exact empty read.</param>
    /// <returns>
    /// An immutable result containing only bytes actually supplied by the backing snapshot. Missing suffix bytes are
    /// never zero-filled into the returned evidence.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, exceeds <see cref="MaximumReadLength"/>, or the requested address range
    /// would overflow the target address space.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner of this reader has ended the source lifetime.</exception>
    MemoryReadResult Read(ulong address, int length);
}
