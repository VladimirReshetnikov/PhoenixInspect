using System.Runtime.InteropServices;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Verifies the raw ClrMD memory boundary independently of a process dump so corrupt-backend failures cannot escape
/// through public evidence reads or expose backend payloads.
/// </summary>
public sealed class ClrmdProcessMemoryReaderTests
{
    private const string SourceId = "dump-sha256:fixture";
    private const string ArtifactPayloadCanary = "artifact-backend-payload-canary";

    /// <summary>
    /// Checks that expected parser, dump-reader, and operating-system read failures become valueless typed evidence
    /// without copying exception-controlled text into the result.
    /// </summary>
    /// <param name="failureKind">Stable test selector for the backend exception family.</param>
    [Theory]
    [Trait("Category", "Fast")]
    [InlineData("clr-diagnostics")]
    [InlineData("bad-image")]
    [InlineData("invalid-data")]
    [InlineData("io")]
    [InlineData("unauthorized")]
    [InlineData("overflow")]
    public void Expected_backend_failures_become_text_free_unavailable_evidence(string failureKind)
    {
        var backend = new StubDataReader(CreateExpectedFailure(failureKind));
        using var reader = new ClrmdProcessMemoryReader(backend, SourceId);

        var result = reader.Read(0x1234, 8);

        Assert.Equal(MemoryReadStatus.Unavailable, result.Status);
        Assert.Equal(SourceId, result.SourceId);
        Assert.Equal(0x1234UL, result.Address);
        Assert.Equal(8, result.RequestedLength);
        Assert.Equal(8, result.MissingByteCount);
        Assert.Empty(result.Bytes);
        Assert.DoesNotContain(ArtifactPayloadCanary, result.SourceId, StringComparison.Ordinal);
        Assert.Equal(1, backend.ReadCallCount);
    }

    /// <summary>
    /// Checks that impossible negative or oversized byte counts returned by a corrupt backend fail closed without
    /// exposing an incidental exception or fabricated byte prefix.
    /// </summary>
    /// <param name="returnedByteCount">The invalid count supplied by the fake ClrMD backend.</param>
    [Theory]
    [Trait("Category", "Fast")]
    [InlineData(-1)]
    [InlineData(9)]
    public void Invalid_backend_byte_counts_become_unavailable_evidence(int returnedByteCount)
    {
        var backend = new StubDataReader(returnedByteCount);
        using var reader = new ClrmdProcessMemoryReader(backend, SourceId);

        var result = reader.Read(0x2000, 8);

        Assert.Equal(MemoryReadStatus.Unavailable, result.Status);
        Assert.Empty(result.Bytes);
        Assert.Equal(8, result.MissingByteCount);
        Assert.Equal(1, backend.ReadCallCount);
    }

    /// <summary>
    /// Checks that ordinary exact, partial, and unavailable byte counts retain their established evidence semantics.
    /// </summary>
    /// <param name="returnedByteCount">The valid prefix length returned by the fake backend.</param>
    /// <param name="expectedStatus">The status derived from the requested and returned byte counts.</param>
    [Theory]
    [Trait("Category", "Fast")]
    [InlineData(0, MemoryReadStatus.Unavailable)]
    [InlineData(2, MemoryReadStatus.Partial)]
    [InlineData(4, MemoryReadStatus.Exact)]
    public void Valid_backend_byte_counts_preserve_evidence_classification(
        int returnedByteCount,
        MemoryReadStatus expectedStatus)
    {
        var backend = new StubDataReader(returnedByteCount);
        using var reader = new ClrmdProcessMemoryReader(backend, SourceId);

        var result = reader.Read(0x3000, 4);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(returnedByteCount, result.BytesRead);
        Assert.Equal(Enumerable.Range(1, returnedByteCount).Select(static value => (byte)value), result.Bytes);
    }

    /// <summary>
    /// Checks that caller contract violations and unexpected backend programming failures remain exceptions rather
    /// than being mislabeled as missing dump evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Programmer_errors_remain_visible_and_do_not_invoke_the_backend_for_invalid_requests()
    {
        var backend = new StubDataReader(returnedByteCount: 0);
        using var reader = new ClrmdProcessMemoryReader(backend, SourceId);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(0x1000, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(0x1000, reader.MaximumReadLength + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(ulong.MaxValue, 2));
        Assert.Equal(0, backend.ReadCallCount);

        var unexpected = new InvalidOperationException(ArtifactPayloadCanary);
        var failingBackend = new StubDataReader(unexpected);
        using var failingReader = new ClrmdProcessMemoryReader(failingBackend, SourceId);
        Assert.Same(unexpected, Assert.Throws<InvalidOperationException>(() => failingReader.Read(0x1000, 1)));

        reader.Dispose();
        Assert.Throws<ObjectDisposedException>(() => reader.Read(0x1000, 1));
        Assert.Equal(0, backend.ReadCallCount);
    }

    private static Exception CreateExpectedFailure(string failureKind) => failureKind switch
    {
        "clr-diagnostics" => new ClrDiagnosticsException(ArtifactPayloadCanary),
        "bad-image" => new BadImageFormatException(ArtifactPayloadCanary),
        "invalid-data" => new InvalidDataException(ArtifactPayloadCanary),
        "io" => new IOException(ArtifactPayloadCanary),
        "unauthorized" => new UnauthorizedAccessException(ArtifactPayloadCanary),
        "overflow" => new OverflowException(ArtifactPayloadCanary),
        _ => throw new ArgumentOutOfRangeException(nameof(failureKind)),
    };

    private sealed class StubDataReader : IDataReader
    {
        private readonly Exception? _readFailure;
        private readonly int _returnedByteCount;

        internal StubDataReader(Exception readFailure)
        {
            _readFailure = readFailure;
        }

        internal StubDataReader(int returnedByteCount)
        {
            _returnedByteCount = returnedByteCount;
        }

        internal int ReadCallCount { get; private set; }

        public string DisplayName => "payload-omitting-fixture";

        public bool IsThreadSafe => true;

        public OSPlatform TargetPlatform => OSPlatform.Windows;

        public Architecture Architecture => Architecture.X64;

        public int ProcessId => 0;

        public int PointerSize => sizeof(ulong);

        public IEnumerable<ModuleInfo> EnumerateModules() => Array.Empty<ModuleInfo>();

        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context) => false;

        public void FlushCachedData()
        {
        }

        public int Read(ulong address, Span<byte> buffer)
        {
            ReadCallCount++;
            if (_readFailure is not null)
            {
                throw _readFailure;
            }

            var bytesToWrite = Math.Clamp(_returnedByteCount, 0, buffer.Length);
            for (var index = 0; index < bytesToWrite; index++)
            {
                buffer[index] = checked((byte)(index + 1));
            }

            return _returnedByteCount;
        }

        public bool Read<T>(ulong address, out T value)
            where T : unmanaged
        {
            value = default;
            return false;
        }

        public T Read<T>(ulong address)
            where T : unmanaged => default;

        public bool ReadPointer(ulong address, out ulong value)
        {
            value = 0;
            return false;
        }

        public ulong ReadPointer(ulong address) => 0;
    }
}
