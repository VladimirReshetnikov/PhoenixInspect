using System.Collections.Immutable;

namespace PhoenixInspect.Host.Abstractions;

/// <summary>
/// Carries immutable byte evidence and completeness for one process-memory read.
/// </summary>
/// <remarks>
/// The result retains only bytes actually returned by the backing reader. In particular, a partial or unavailable
/// read never exposes an allocated, zero-filled suffix that could be mistaken for observed target state. This is a
/// draft dump-evidence contract and may gain more specific backend miss diagnostics as the supported dump corpus grows.
/// </remarks>
public sealed class MemoryReadResult
{
    private MemoryReadResult(
        string sourceId,
        ulong address,
        int requestedLength,
        ImmutableArray<byte> bytes,
        MemoryReadStatus status)
    {
        SourceId = sourceId;
        Address = address;
        RequestedLength = requestedLength;
        Bytes = bytes;
        Status = status;
    }

    /// <summary>
    /// Gets the stable identifier of the immutable source that supplied this evidence.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Gets the first target virtual address requested by the read.
    /// </summary>
    public ulong Address { get; }

    /// <summary>
    /// Gets the number of bytes requested from the target address space.
    /// </summary>
    public int RequestedLength { get; }

    /// <summary>
    /// Gets the immutable prefix of bytes actually read from the target snapshot.
    /// </summary>
    public ImmutableArray<byte> Bytes { get; }

    /// <summary>
    /// Gets whether the requested range was read exactly, partially, or not at all.
    /// </summary>
    public MemoryReadStatus Status { get; }

    /// <summary>
    /// Gets the number of bytes actually supplied by the snapshot.
    /// </summary>
    public int BytesRead => Bytes.Length;

    /// <summary>
    /// Gets the number of requested suffix bytes that were not supplied by the snapshot.
    /// </summary>
    public int MissingByteCount => RequestedLength - BytesRead;

    /// <summary>
    /// Creates a validated result from the prefix returned by a process-memory backend.
    /// </summary>
    /// <param name="sourceId">Stable identifier of the immutable source that supplied the bytes.</param>
    /// <param name="address">The first target virtual address requested.</param>
    /// <param name="requestedLength">The number of bytes requested.</param>
    /// <param name="bytes">The prefix actually returned by the backend.</param>
    /// <returns>A result whose status is derived solely from requested and returned byte counts.</returns>
    /// <exception cref="ArgumentException"><paramref name="sourceId"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requestedLength"/> is negative, the address range overflows, or more bytes were supplied than
    /// requested.
    /// </exception>
    public static MemoryReadResult Create(
        string sourceId,
        ulong address,
        int requestedLength,
        ReadOnlySpan<byte> bytes)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("A memory source identifier is required.", nameof(sourceId));
        }

        if (requestedLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedLength));
        }

        if (requestedLength > 0 && address > ulong.MaxValue - (ulong)(requestedLength - 1))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedLength), "The requested address range overflows the target address space.");
        }

        if (bytes.Length > requestedLength)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), "A backend cannot return more bytes than were requested.");
        }

        var status = bytes.Length == requestedLength
            ? MemoryReadStatus.Exact
            : bytes.IsEmpty
                ? MemoryReadStatus.Unavailable
                : MemoryReadStatus.Partial;

        return new MemoryReadResult(
            sourceId,
            address,
            requestedLength,
            ImmutableArray.Create(bytes.ToArray()),
            status);
    }
}
