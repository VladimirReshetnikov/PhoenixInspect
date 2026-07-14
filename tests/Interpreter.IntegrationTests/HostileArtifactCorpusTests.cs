using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Verifies the deterministic, payload-free hostile minidump corpus planning and materialization helpers.</summary>
public sealed class HostileArtifactCorpusTests
{
    private const int DirectoryRva = 32;
    private const int MemoryListRva = 56;
    private const int MemoryPayloadRva = 96;

    /// <summary>Checks manifest stability, schema versioning, case-count/size bounds, and secret exclusion.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Scope", "Cybersecurity")]
    public void Canonical_manifest_is_versioned_deterministic_bounded_and_payload_free()
    {
        var seed = CreateStructuredSeed();
        using var firstSeed = new MemoryStream(seed, writable: false);
        using var secondSeed = new MemoryStream(seed.ToArray(), writable: false);
        var first = HostileArtifactCorpus.Create(firstSeed);
        var second = HostileArtifactCorpus.Create(secondSeed);

        var firstManifest = first.SerializeCanonicalManifest();
        var secondManifest = second.SerializeCanonicalManifest();
        Assert.Equal(firstManifest, secondManifest);
        Assert.InRange(first.Cases.Length, 1, HostileArtifactCorpus.MaximumCaseCount);
        Assert.Equal(
            first.Cases.Select(static item => item.Id).OrderBy(static id => id, StringComparer.Ordinal),
            first.Cases.Select(static item => item.Id));
        Assert.Equal(first.Cases.Length, first.Cases.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count());

        var shortLengths = first.Cases
            .Where(static item => item.Family == "short-header-prefix")
            .Select(static item => item.ArtifactLength)
            .Prepend(AssertCase(first, "empty").ArtifactLength)
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(0, HostileArtifactCorpus.HeaderSize).Select(static value => (long)value), shortLengths);

        var limit = AssertCase(first, "limit-8gib-plus-one");
        Assert.True(limit.IsSparseLengthOnly);
        Assert.Equal(HostileArtifactCorpus.ExternalDumpLimit + 1, limit.ArtifactLength);
        Assert.Equal(HostileArtifactExpectedOutcome.LimitExceeded, limit.ExpectedOutcome);
        Assert.All(
            first.Cases.Where(static item => !item.IsSparseLengthOnly),
            item => Assert.InRange(item.ArtifactLength, 0, seed.LongLength + 32));

        using var document = JsonDocument.Parse(firstManifest);
        Assert.Equal(HostileArtifactCorpus.ManifestSchema, document.RootElement.GetProperty("schema").GetString());
        Assert.Equal(first.Cases.Length, document.RootElement.GetProperty("cases").GetArrayLength());
        var manifestText = Encoding.UTF8.GetString(firstManifest);
        Assert.DoesNotContain("C:\\secret\\incident.dmp", manifestText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-payload-canary", manifestText, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(seed), manifestText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks that structure-aware truncations and patches affect only their declared byte ranges.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Scope", "Cybersecurity")]
    public void Structure_aware_mutations_change_only_the_declared_regions()
    {
        var seed = CreateStructuredSeed();
        using var planningSeed = new MemoryStream(seed, writable: false);
        var corpus = HostileArtifactCorpus.Create(planningSeed);

        Assert.Empty(Materialize(AssertCase(corpus, "empty"), seed));
        Assert.Equal(64, Materialize(AssertCase(corpus, "garbage-64"), seed).Length);
        for (var length = 1; length < HostileArtifactCorpus.HeaderSize; length++)
        {
            Assert.Equal(seed[..length], Materialize(AssertCase(corpus, $"header-prefix-{length:D2}"), seed));
        }

        var badSignature = Materialize(AssertCase(corpus, "header-bad-signature"), seed);
        Assert.Equal(new byte[4], badSignature[..4]);
        Assert.Equal(seed[4..], badSignature[4..]);

        var badVersion = Materialize(AssertCase(corpus, "header-bad-version"), seed);
        Assert.Equal(new byte[4], badVersion[4..8]);
        Assert.Equal(seed[..4], badVersion[..4]);
        Assert.Equal(seed[8..], badVersion[8..]);

        var countOverflow = Materialize(AssertCase(corpus, "directory-count-overflow"), seed);
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(countOverflow.AsSpan(8, 4)));
        AssertOnlyRangeChanged(seed, countOverflow, 8, 4);

        var descriptorOverflow = Materialize(AssertCase(corpus, "directory-descriptor-range-overflow"), seed);
        Assert.Equal(0x100U, BinaryPrimitives.ReadUInt32LittleEndian(descriptorOverflow.AsSpan(DirectoryRva + 4, 4)));
        Assert.Equal(uint.MaxValue - 15, BinaryPrimitives.ReadUInt32LittleEndian(descriptorOverflow.AsSpan(DirectoryRva + 8, 4)));
        AssertOnlyRangesChanged(seed, descriptorOverflow, (DirectoryRva + 4, 4), (DirectoryRva + 8, 4));

        var overlap = Materialize(AssertCase(corpus, "directory-stream-ranges-overlap"), seed);
        Assert.Equal(
            BinaryPrimitives.ReadUInt32LittleEndian(overlap.AsSpan(DirectoryRva + 8, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(overlap.AsSpan(DirectoryRva + 12 + 8, 4)));

        var descriptorTruncated = Materialize(AssertCase(corpus, "memory-descriptor-truncated"), seed);
        Assert.Equal(MemoryListRva + 4 + 16 - 1, descriptorTruncated.Length);
        Assert.Equal(seed[..descriptorTruncated.Length], descriptorTruncated);

        var payloadTruncated = Materialize(AssertCase(corpus, "memory-payload-truncated"), seed);
        Assert.Equal(MemoryPayloadRva + 4 - 1, payloadTruncated.Length);
        Assert.Equal(seed[..payloadTruncated.Length], payloadTruncated);

        AssertSingleBitDifference(seed, Materialize(AssertCase(corpus, "bitflip-header-version"), seed));
        AssertSingleBitDifference(seed, Materialize(AssertCase(corpus, "bitflip-directory-000-rva"), seed));

        var appended = Materialize(AssertCase(corpus, "appended-junk-32"), seed);
        Assert.Equal(seed.Length + 32, appended.Length);
        Assert.Equal(seed, appended[..seed.Length]);
        Assert.NotEqual(new byte[32], appended[seed.Length..]);
    }

    /// <summary>Checks that the over-limit artifact uses logical sparse length rather than an allocated payload.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Scope", "Cybersecurity")]
    public void Sparse_limit_case_materializes_by_length_without_allocating_payload()
    {
        var seed = CreateStructuredSeed();
        using var planningSeed = new MemoryStream(seed, writable: false);
        var corpus = HostileArtifactCorpus.Create(planningSeed);
        var limit = AssertCase(corpus, "limit-8gib-plus-one");
        using var materializationSeed = new MemoryStream(seed, writable: false);
        using var destination = new LengthRecordingStream();

        limit.Materialize(materializationSeed, destination);

        Assert.Equal(HostileArtifactCorpus.ExternalDumpLimit + 1, destination.Length);
        Assert.Equal(0, destination.BytesWritten);
        Assert.Equal(0, destination.Position);
    }

    /// <summary>Checks that corpus planning fails closed for invalid seeds and seeds without memory evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Scope", "Cybersecurity")]
    public void Generator_rejects_non_minidumps_and_seeds_without_memory_ranges()
    {
        using var shortSeed = new MemoryStream(new byte[HostileArtifactCorpus.HeaderSize - 1], writable: false);
        Assert.Throws<InvalidDataException>(() => HostileArtifactCorpus.Create(shortSeed));

        var garbage = new byte[HostileArtifactCorpus.HeaderSize];
        using var garbageSeed = new MemoryStream(garbage, writable: false);
        Assert.Throws<InvalidDataException>(() => HostileArtifactCorpus.Create(garbageSeed));

        var noMemory = CreateStructuredSeed();
        BinaryPrimitives.WriteUInt32LittleEndian(noMemory.AsSpan(DirectoryRva, 4), 7);
        using var noMemorySeed = new MemoryStream(noMemory, writable: false);
        Assert.Throws<InvalidDataException>(() => HostileArtifactCorpus.Create(noMemorySeed));
    }

    /// <summary>Checks that the alternate MINIDUMP_MEMORY64_LIST layout produces equivalent bounded truncations.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Scope", "Cybersecurity")]
    public void Generator_understands_memory64_descriptor_and_contiguous_payload_layout()
    {
        var seed = CreateMemory64Seed();
        using var planningSeed = new MemoryStream(seed, writable: false);
        var corpus = HostileArtifactCorpus.Create(planningSeed);

        var descriptorTruncated = Materialize(AssertCase(corpus, "memory-descriptor-truncated"), seed);
        Assert.Equal(MemoryListRva + 16 + 16 - 1, descriptorTruncated.Length);
        Assert.Equal(seed[..descriptorTruncated.Length], descriptorTruncated);

        var payloadTruncated = Materialize(AssertCase(corpus, "memory-payload-truncated"), seed);
        Assert.Equal(112 + 4 - 1, payloadTruncated.Length);
        Assert.Equal(seed[..payloadTruncated.Length], payloadTruncated);
        Assert.Contains(
            "MINIDUMP_MEMORY64_LIST",
            AssertCase(corpus, "memory-payload-truncated").Mutation,
            StringComparison.Ordinal);
    }

    private static HostileArtifactCase AssertCase(HostileArtifactCorpus corpus, string id) =>
        Assert.Single(corpus.Cases, item => string.Equals(item.Id, id, StringComparison.Ordinal));

    private static byte[] Materialize(HostileArtifactCase item, byte[] seed)
    {
        using var seedStream = new MemoryStream(seed, writable: false);
        using var destination = new MemoryStream();
        item.Materialize(seedStream, destination);
        return destination.ToArray();
    }

    private static byte[] CreateStructuredSeed()
    {
        var seed = new byte[256];
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(0, 4), 0x504D444D);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(4, 4), 0x0000A793);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(8, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(12, 4), DirectoryRva);

        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva, 4), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 4, 4), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 8, 4), MemoryListRva);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 12, 4), 7);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 16, 4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 20, 4), 80);

        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(MemoryListRva, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(seed.AsSpan(MemoryListRva + 4, 8), 0x0000_7FFF_1234_0000);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(MemoryListRva + 12, 4), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(MemoryListRva + 16, 4), MemoryPayloadRva);
        new byte[] { 0x10, 0x20, 0x30, 0x40 }.CopyTo(seed, MemoryPayloadRva);

        Encoding.UTF8.GetBytes("C:\\secret\\incident.dmp secret-payload-canary")
            .CopyTo(seed, 128);
        return seed;
    }

    private static byte[] CreateMemory64Seed()
    {
        var seed = new byte[256];
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(0, 4), 0x504D444D);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(4, 4), 0x0000A793);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(8, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(12, 4), DirectoryRva);

        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva, 4), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 4, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 8, 4), MemoryListRva);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 12, 4), 7);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 16, 4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(seed.AsSpan(DirectoryRva + 20, 4), 96);

        BinaryPrimitives.WriteUInt64LittleEndian(seed.AsSpan(MemoryListRva, 8), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(seed.AsSpan(MemoryListRva + 8, 8), 112);
        BinaryPrimitives.WriteUInt64LittleEndian(seed.AsSpan(MemoryListRva + 16, 8), 0x0000_7FFF_1234_0000);
        BinaryPrimitives.WriteUInt64LittleEndian(seed.AsSpan(MemoryListRva + 24, 8), 4);
        new byte[] { 0x50, 0x60, 0x70, 0x80 }.CopyTo(seed, 112);
        return seed;
    }

    private static void AssertOnlyRangeChanged(byte[] expected, byte[] actual, int start, int length) =>
        AssertOnlyRangesChanged(expected, actual, (start, length));

    private static void AssertOnlyRangesChanged(
        byte[] expected,
        byte[] actual,
        params (int Start, int Length)[] changedRanges)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            if (changedRanges.Any(range => index >= range.Start && index < range.Start + range.Length))
            {
                continue;
            }

            Assert.Equal(expected[index], actual[index]);
        }
    }

    private static void AssertSingleBitDifference(byte[] expected, byte[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        var differences = expected
            .Zip(actual, static (left, right) => (byte)(left ^ right))
            .Where(static difference => difference != 0)
            .ToArray();
        var difference = Assert.Single(differences);
        Assert.Equal(0, difference & (difference - 1));
    }

    private sealed class LengthRecordingStream : Stream
    {
        private long _length;
        private long _position;

        internal long BytesWritten { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(_length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void SetLength(long value)
        {
            _length = value;
            if (_position > value)
            {
                _position = value;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BytesWritten += count;
            _position = checked(_position + count);
            _length = Math.Max(_length, _position);
        }
    }
}
