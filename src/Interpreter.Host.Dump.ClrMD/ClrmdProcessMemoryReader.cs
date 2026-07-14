using System.Buffers;
using Interpreter.Host.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Host.Dump.ClrMD;

internal sealed class ClrmdProcessMemoryReader : IProcessMemoryReader, IDisposable
{
    private const int ReadLimit = 16 * 1024 * 1024;
    private readonly IDataReader _reader;
    private bool _disposed;

    public ClrmdProcessMemoryReader(IDataReader reader, string sourceId)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        SourceId = string.IsNullOrWhiteSpace(sourceId)
            ? throw new ArgumentException("A memory source identifier is required.", nameof(sourceId))
            : sourceId;
    }

    public int PointerSize => _reader.PointerSize;

    public int MaximumReadLength => ReadLimit;

    public string SourceId { get; }

    public MemoryReadResult Read(ulong address, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (length > MaximumReadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"One memory read is limited to {MaximumReadLength} bytes.");
        }

        if (length > 0 && address > ulong.MaxValue - (ulong)(length - 1))
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The requested address range overflows the target address space.");
        }

        if (length == 0)
        {
            return MemoryReadResult.Create(SourceId, address, 0, ReadOnlySpan<byte>.Empty);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var bytesRead = _reader.Read(address, buffer.AsSpan(0, length));
            if ((uint)bytesRead > (uint)length)
            {
                throw new InvalidDataException($"ClrMD returned an invalid byte count {bytesRead} for a {length}-byte read.");
            }

            return MemoryReadResult.Create(SourceId, address, length, buffer.AsSpan(0, bytesRead));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
