using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Interpreter.IntegrationTests;

internal enum HostileArtifactExpectedOutcome
{
    NonSuccess,
    DeterministicTypedOutcome,
    LimitExceeded,
}

internal sealed class HostileArtifactCorpus
{
    internal const string ManifestSchema = "interpreter-hostile-artifact-corpus/v1";
    internal const int HeaderSize = 32;
    internal const int MaximumCaseCount = 128;
    internal const long ExternalDumpLimit = 8L * 1024 * 1024 * 1024;

    private const uint MinidumpSignature = 0x504D444D;
    private const int DirectoryEntrySize = 12;
    private const uint MemoryListStream = 5;
    private const uint Memory64ListStream = 9;
    private const int MaximumDirectoryEntries = 4_096;
    private const int MaximumBitMutatedDirectoryEntries = 16;
    private const int MaximumDeclaredMemoryDescriptors = 1_000_000;
    private const int MaximumMemoryDescriptorsScanned = 4_096;
    private const int AppendedJunkLength = 32;

    private HostileArtifactCorpus(ImmutableArray<HostileArtifactCase> cases)
    {
        Cases = cases;
    }

    internal ImmutableArray<HostileArtifactCase> Cases { get; }

    internal static HostileArtifactCorpus Create(Stream seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.CanRead || !seed.CanSeek)
        {
            throw new ArgumentException("The minidump seed must be a readable, seekable stream.", nameof(seed));
        }

        if (seed.Length < HeaderSize)
        {
            throw new InvalidDataException("The minidump seed does not contain a complete header.");
        }

        if (seed.Length > ExternalDumpLimit)
        {
            throw new InvalidDataException("The minidump seed exceeds the external dump admission limit.");
        }

        var originalPosition = seed.Position;
        try
        {
            Span<byte> header = stackalloc byte[HeaderSize];
            ReadExactlyAt(seed, 0, header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != MinidumpSignature)
            {
                throw new InvalidDataException("The corpus seed is not a Windows minidump.");
            }

            var streamCount = BinaryPrimitives.ReadUInt32LittleEndian(header[8..12]);
            if (streamCount == 0 || streamCount > MaximumDirectoryEntries)
            {
                throw new InvalidDataException("The minidump stream-directory count is outside the corpus profile.");
            }

            var directoryRva = BinaryPrimitives.ReadUInt32LittleEndian(header[12..16]);
            var directoryLength = checked((long)streamCount * DirectoryEntrySize);
            var directoryEnd = checked((long)directoryRva + directoryLength);
            if (directoryRva < HeaderSize || directoryEnd > seed.Length)
            {
                throw new InvalidDataException("The minidump stream directory is outside the seed extent.");
            }

            var directoryBytes = new byte[checked((int)directoryLength)];
            ReadExactlyAt(seed, directoryRva, directoryBytes);
            var entries = ParseDirectory(directoryBytes, streamCount);
            var cases = ImmutableArray.CreateBuilder<HostileArtifactCase>();

            AddConstantCases(cases);
            AddHeaderPrefixCases(cases, seed.Length);
            AddHeaderCorruptionCases(cases, seed.Length);
            AddDirectoryCorruptionCases(cases, seed.Length, directoryRva, directoryEnd, entries);
            AddMemoryTruncationCases(cases, seed, seed.Length, entries);
            AddBitMutationCases(cases, seed, seed.Length, directoryRva, entries.Length);
            AddTrailingAndLimitCases(cases, seed.Length);

            var ordered = cases
                .OrderBy(static item => item.Id, StringComparer.Ordinal)
                .ToImmutableArray();
            if (ordered.Length > MaximumCaseCount)
            {
                throw new InvalidDataException("The generated hostile-artifact corpus exceeds its deterministic case bound.");
            }

            for (var index = 1; index < ordered.Length; index++)
            {
                if (string.Equals(ordered[index - 1].Id, ordered[index].Id, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The generated hostile-artifact corpus contains a duplicate case identifier.");
                }
            }

            return new HostileArtifactCorpus(ordered);
        }
        finally
        {
            seed.Position = originalPosition;
        }
    }

    internal byte[] SerializeCanonicalManifest()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", ManifestSchema);
            writer.WriteStartArray("cases");
            foreach (var item in Cases)
            {
                writer.WriteStartObject();
                writer.WriteString("id", item.Id);
                writer.WriteString("family", item.Family);
                writer.WriteString("mutation", item.Mutation);
                writer.WriteString("expectedOutcome", ToManifestValue(item.ExpectedOutcome));
                writer.WriteNumber("artifactLength", item.ArtifactLength);
                writer.WriteBoolean("sparseLengthOnly", item.IsSparseLengthOnly);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static ImmutableArray<MinidumpDirectoryEntry> ParseDirectory(byte[] bytes, uint streamCount)
    {
        var entries = ImmutableArray.CreateBuilder<MinidumpDirectoryEntry>(checked((int)streamCount));
        for (var index = 0; index < streamCount; index++)
        {
            var offset = checked((int)index * DirectoryEntrySize);
            var entry = bytes.AsSpan(offset, DirectoryEntrySize);
            entries.Add(new MinidumpDirectoryEntry(
                BinaryPrimitives.ReadUInt32LittleEndian(entry[..4]),
                BinaryPrimitives.ReadUInt32LittleEndian(entry[4..8]),
                BinaryPrimitives.ReadUInt32LittleEndian(entry[8..12])));
        }

        return entries.ToImmutable();
    }

    private static void AddConstantCases(ImmutableArray<HostileArtifactCase>.Builder cases)
    {
        cases.Add(HostileArtifactCase.FromConstant(
            "empty",
            "baseline-invalid",
            "Emit a zero-byte artifact.",
            HostileArtifactExpectedOutcome.NonSuccess,
            ImmutableArray<byte>.Empty));

        var garbage = Enumerable.Range(0, 64)
            .Select(static index => unchecked((byte)(0xA5 ^ (index * 37))))
            .ToImmutableArray();
        cases.Add(HostileArtifactCase.FromConstant(
            "garbage-64",
            "baseline-invalid",
            "Replace the seed with 64 deterministic non-minidump bytes.",
            HostileArtifactExpectedOutcome.NonSuccess,
            garbage));
    }

    private static void AddHeaderPrefixCases(
        ImmutableArray<HostileArtifactCase>.Builder cases,
        long seedLength)
    {
        for (var length = 1; length < HeaderSize; length++)
        {
            cases.Add(HostileArtifactCase.FromSeed(
                $"header-prefix-{length:D2}",
                "short-header-prefix",
                $"Retain exactly the first {length} bytes of the 32-byte MINIDUMP_HEADER.",
                HostileArtifactExpectedOutcome.NonSuccess,
                seedLength,
                length));
        }
    }

    private static void AddHeaderCorruptionCases(
        ImmutableArray<HostileArtifactCase>.Builder cases,
        long seedLength)
    {
        cases.Add(HostileArtifactCase.FromSeed(
            "header-bad-signature",
            "header-corruption",
            "Replace the MINIDUMP_HEADER signature with zero.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            seedLength,
            patches: UInt32Patches(0, 0)));
        cases.Add(HostileArtifactCase.FromSeed(
            "header-bad-version",
            "header-corruption",
            "Replace the MINIDUMP_HEADER version with zero.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            seedLength,
            patches: UInt32Patches(4, 0)));

    }

    private static void AddDirectoryCorruptionCases(
        ImmutableArray<HostileArtifactCase>.Builder cases,
        long seedLength,
        uint directoryRva,
        long directoryEnd,
        ImmutableArray<MinidumpDirectoryEntry> entries)
    {
        cases.Add(HostileArtifactCase.FromSeed(
            "directory-truncated-at-start",
            "stream-directory-truncation",
            "Truncate immediately before the first MINIDUMP_DIRECTORY entry.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            directoryRva));
        cases.Add(HostileArtifactCase.FromSeed(
            "directory-truncated-final-entry",
            "stream-directory-truncation",
            "Remove the last byte required by the declared MINIDUMP_DIRECTORY array.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            directoryEnd - 1));
        cases.Add(HostileArtifactCase.FromSeed(
            "directory-count-overflow",
            "directory-overflow",
            "Set NumberOfStreams to UInt32.MaxValue.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            seedLength,
            patches: UInt32Patches(8, uint.MaxValue)));
        cases.Add(HostileArtifactCase.FromSeed(
            "directory-rva-overflow",
            "directory-overflow",
            "Move the stream-directory RVA to the final eight bytes of the UInt32 address space.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            seedLength,
            patches: UInt32Patches(12, uint.MaxValue - 7)));
        cases.Add(HostileArtifactCase.FromSeed(
            "directory-rva-overlaps-header",
            "directory-overlap",
            "Move the stream directory into the MINIDUMP_HEADER.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            seedLength,
            patches: UInt32Patches(12, 16)));

        var firstEntryOffset = checked((long)directoryRva);
        cases.Add(HostileArtifactCase.FromSeed(
            "directory-descriptor-range-overflow",
            "directory-overflow",
            "Give the first stream descriptor a near-UInt32.MaxValue RVA and an overflowing size.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            seedLength,
            patches: UInt32Patches(firstEntryOffset + 4, 0x100)
                .AddRange(UInt32Patches(firstEntryOffset + 8, uint.MaxValue - 15))));
        cases.Add(HostileArtifactCase.FromSeed(
            "directory-stream-overlaps-table",
            "directory-overlap",
            "Move the first stream payload over the stream-directory table.",
            HostileArtifactExpectedOutcome.DeterministicTypedOutcome,
            seedLength,
            seedLength,
            patches: UInt32Patches(firstEntryOffset + 4, checked((uint)(entries.Length * DirectoryEntrySize)))
                .AddRange(UInt32Patches(firstEntryOffset + 8, directoryRva))));

        if (entries.Length >= 2)
        {
            var secondEntryOffset = firstEntryOffset + DirectoryEntrySize;
            cases.Add(HostileArtifactCase.FromSeed(
                "directory-stream-ranges-overlap",
                "directory-overlap",
                "Move the second stream payload over the first stream payload.",
                HostileArtifactExpectedOutcome.DeterministicTypedOutcome,
                seedLength,
                seedLength,
                patches: UInt32Patches(secondEntryOffset + 4, Math.Max(entries[0].DataSize, 1))
                    .AddRange(UInt32Patches(secondEntryOffset + 8, entries[0].Rva))));
        }
    }

    private static void AddMemoryTruncationCases(
        ImmutableArray<HostileArtifactCase>.Builder cases,
        Stream seed,
        long seedLength,
        ImmutableArray<MinidumpDirectoryEntry> entries)
    {
        var memory64 = entries.FirstOrDefault(static item => item.StreamType == Memory64ListStream);
        if (memory64 != default && TryReadMemory64Range(seed, seedLength, memory64, out var memory64Range))
        {
            AddMemoryCases(cases, seedLength, memory64Range, "MINIDUMP_MEMORY64_LIST");
            return;
        }

        var memory = entries.FirstOrDefault(static item => item.StreamType == MemoryListStream);
        if (memory != default && TryReadMemoryRange(seed, seedLength, memory, out var memoryRange))
        {
            AddMemoryCases(cases, seedLength, memoryRange, "MINIDUMP_MEMORY_LIST");
            return;
        }

        throw new InvalidDataException("The minidump seed has no bounded, non-empty memory range for corpus truncation.");
    }

    private static void AddMemoryCases(
        ImmutableArray<HostileArtifactCase>.Builder cases,
        long seedLength,
        MemoryMutationRange range,
        string structureName)
    {
        cases.Add(HostileArtifactCase.FromSeed(
            "memory-descriptor-truncated",
            "memory-range-truncation",
            $"Remove the last byte of the declared {structureName} descriptor array.",
            HostileArtifactExpectedOutcome.NonSuccess,
            seedLength,
            range.DescriptorArrayEnd - 1));
        cases.Add(HostileArtifactCase.FromSeed(
            "memory-payload-truncated",
            "memory-range-truncation",
            $"Remove the last byte of one non-empty {structureName} memory range.",
            HostileArtifactExpectedOutcome.DeterministicTypedOutcome,
            seedLength,
            range.PayloadEnd - 1));
    }

    private static bool TryReadMemoryRange(
        Stream seed,
        long seedLength,
        MinidumpDirectoryEntry entry,
        out MemoryMutationRange range)
    {
        range = default;
        if (entry.DataSize < 4 || !IsWithin(seedLength, entry.Rva, 4))
        {
            return false;
        }

        Span<byte> countBytes = stackalloc byte[4];
        ReadExactlyAt(seed, entry.Rva, countBytes);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(countBytes);
        if (count == 0 || count > MaximumDeclaredMemoryDescriptors)
        {
            return false;
        }

        var descriptorArrayEnd = checked((long)entry.Rva + 4 + ((long)count * 16));
        if (descriptorArrayEnd > seedLength || descriptorArrayEnd > (long)entry.Rva + entry.DataSize)
        {
            return false;
        }

        Span<byte> descriptor = stackalloc byte[16];
        var descriptorsToScan = Math.Min(count, MaximumMemoryDescriptorsScanned);
        for (var index = 0; index < descriptorsToScan; index++)
        {
            ReadExactlyAt(seed, checked((long)entry.Rva + 4 + (index * 16L)), descriptor);
            var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[8..12]);
            var rva = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[12..16]);
            if (dataSize == 0 || !IsWithin(seedLength, rva, dataSize))
            {
                continue;
            }

            range = new MemoryMutationRange(descriptorArrayEnd, checked((long)rva + dataSize));
            return true;
        }

        return false;
    }

    private static bool TryReadMemory64Range(
        Stream seed,
        long seedLength,
        MinidumpDirectoryEntry entry,
        out MemoryMutationRange range)
    {
        range = default;
        if (entry.DataSize < 16 || !IsWithin(seedLength, entry.Rva, 16))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[16];
        ReadExactlyAt(seed, entry.Rva, header);
        var count = BinaryPrimitives.ReadUInt64LittleEndian(header[..8]);
        var baseRva = BinaryPrimitives.ReadUInt64LittleEndian(header[8..16]);
        if (count == 0 || count > MaximumDeclaredMemoryDescriptors)
        {
            return false;
        }

        var descriptorArrayEnd = checked((long)entry.Rva + 16 + checked((long)count * 16));
        if (descriptorArrayEnd > seedLength || descriptorArrayEnd > (long)entry.Rva + entry.DataSize ||
            baseRva > (ulong)seedLength)
        {
            return false;
        }

        Span<byte> descriptor = stackalloc byte[16];
        var payloadOffset = baseRva;
        var descriptorsToScan = Math.Min(count, MaximumMemoryDescriptorsScanned);
        for (ulong index = 0; index < descriptorsToScan; index++)
        {
            ReadExactlyAt(seed, checked((long)entry.Rva + 16 + checked((long)index * 16)), descriptor);
            var dataSize = BinaryPrimitives.ReadUInt64LittleEndian(descriptor[8..16]);
            if (dataSize > 0 && payloadOffset <= long.MaxValue && dataSize <= long.MaxValue &&
                IsWithin(seedLength, checked((long)payloadOffset), checked((long)dataSize)))
            {
                range = new MemoryMutationRange(
                    descriptorArrayEnd,
                    checked((long)(payloadOffset + dataSize)));
                return true;
            }

            if (ulong.MaxValue - payloadOffset < dataSize)
            {
                return false;
            }

            payloadOffset += dataSize;
        }

        return false;
    }

    private static void AddBitMutationCases(
        ImmutableArray<HostileArtifactCase>.Builder cases,
        Stream seed,
        long seedLength,
        uint directoryRva,
        int directoryEntryCount)
    {
        (string Name, int Offset)[] headerFields =
        [
            ("signature", 0),
            ("version", 4),
            ("stream-count", 8),
            ("directory-rva", 12),
            ("checksum", 16),
            ("timestamp", 20),
            ("flags-low", 24),
            ("flags-high", 31),
        ];
        for (var index = 0; index < headerFields.Length; index++)
        {
            var field = headerFields[index];
            cases.Add(BitFlipCase(
                seed,
                seedLength,
                $"bitflip-header-{field.Name}",
                "deterministic-header-bit-mutation",
                $"Flip bit {index % 8} at the {field.Name} field boundary.",
                field.Offset,
                index % 8));
        }

        var mutatedEntryCount = Math.Min(directoryEntryCount, MaximumBitMutatedDirectoryEntries);
        (string Name, int RelativeOffset)[] directoryFields =
        [
            ("type", 0),
            ("size", 4),
            ("rva", 8),
        ];
        for (var entryIndex = 0; entryIndex < mutatedEntryCount; entryIndex++)
        {
            for (var fieldIndex = 0; fieldIndex < directoryFields.Length; fieldIndex++)
            {
                var field = directoryFields[fieldIndex];
                var bit = (entryIndex + fieldIndex) % 8;
                var offset = checked((long)directoryRva + (entryIndex * DirectoryEntrySize) + field.RelativeOffset);
                cases.Add(BitFlipCase(
                    seed,
                    seedLength,
                    $"bitflip-directory-{entryIndex:D3}-{field.Name}",
                    "deterministic-directory-bit-mutation",
                    $"Flip bit {bit} in directory entry {entryIndex}'s {field.Name} field.",
                    offset,
                    bit));
            }
        }
    }

    private static HostileArtifactCase BitFlipCase(
        Stream seed,
        long seedLength,
        string id,
        string family,
        string mutation,
        long offset,
        int bit)
    {
        var original = ReadByteAt(seed, offset);
        return HostileArtifactCase.FromSeed(
            id,
            family,
            mutation,
            HostileArtifactExpectedOutcome.DeterministicTypedOutcome,
            seedLength,
            seedLength,
            patches: ImmutableArray.Create(new HostileArtifactPatch(offset, (byte)(original ^ (1 << bit)))));
    }

    private static void AddTrailingAndLimitCases(
        ImmutableArray<HostileArtifactCase>.Builder cases,
        long seedLength)
    {
        var junk = Enumerable.Range(0, AppendedJunkLength)
            .Select(static index => unchecked((byte)(0x5A + (index * 13))))
            .ToImmutableArray();
        cases.Add(HostileArtifactCase.FromSeed(
            "appended-junk-32",
            "trailing-data",
            "Append 32 deterministic bytes after the complete seed.",
            seedLength <= ExternalDumpLimit - AppendedJunkLength
                ? HostileArtifactExpectedOutcome.DeterministicTypedOutcome
                : HostileArtifactExpectedOutcome.LimitExceeded,
            seedLength,
            seedLength,
            suffix: junk));
        cases.Add(HostileArtifactCase.SparseLength(
            "limit-8gib-plus-one",
            "resource-limit",
            "Create a sparse artifact whose logical length is one byte above the 8 GiB admission limit.",
            HostileArtifactExpectedOutcome.LimitExceeded,
            ExternalDumpLimit + 1));
    }

    private static ImmutableArray<HostileArtifactPatch> UInt32Patches(long offset, uint value)
    {
        var builder = ImmutableArray.CreateBuilder<HostileArtifactPatch>(sizeof(uint));
        for (var index = 0; index < sizeof(uint); index++)
        {
            builder.Add(new HostileArtifactPatch(offset + index, unchecked((byte)(value >> (index * 8)))));
        }

        return builder.MoveToImmutable();
    }

    private static byte ReadByteAt(Stream stream, long offset)
    {
        stream.Position = offset;
        var value = stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException("The minidump seed ended during structure-aware mutation planning.");
        }

        return (byte)value;
    }

    private static void ReadExactlyAt(Stream stream, long offset, Span<byte> destination)
    {
        stream.Position = offset;
        stream.ReadExactly(destination);
    }

    private static bool IsWithin(long extentLength, long offset, long length) =>
        offset >= 0 && length >= 0 && offset <= extentLength && length <= extentLength - offset;

    private static bool IsWithin(long extentLength, uint offset, uint length) =>
        IsWithin(extentLength, (long)offset, (long)length);

    private static string ToManifestValue(HostileArtifactExpectedOutcome outcome) => outcome switch
    {
        HostileArtifactExpectedOutcome.NonSuccess => "non-success",
        HostileArtifactExpectedOutcome.DeterministicTypedOutcome => "deterministic-typed-outcome",
        HostileArtifactExpectedOutcome.LimitExceeded => "limit-exceeded",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private readonly record struct MinidumpDirectoryEntry(uint StreamType, uint DataSize, uint Rva);

    private readonly record struct MemoryMutationRange(long DescriptorArrayEnd, long PayloadEnd);
}

internal sealed class HostileArtifactCase
{
    private readonly HostileArtifactMaterializationKind _kind;
    private readonly long _seedLength;
    private readonly long _seedPrefixLength;
    private readonly ImmutableArray<HostileArtifactPatch> _patches;
    private readonly ImmutableArray<byte> _constantBytes;
    private readonly ImmutableArray<byte> _suffix;

    private HostileArtifactCase(
        string id,
        string family,
        string mutation,
        HostileArtifactExpectedOutcome expectedOutcome,
        long artifactLength,
        HostileArtifactMaterializationKind kind,
        long seedLength,
        long seedPrefixLength,
        ImmutableArray<HostileArtifactPatch> patches,
        ImmutableArray<byte> constantBytes,
        ImmutableArray<byte> suffix)
    {
        Id = id;
        Family = family;
        Mutation = mutation;
        ExpectedOutcome = expectedOutcome;
        ArtifactLength = artifactLength;
        _kind = kind;
        _seedLength = seedLength;
        _seedPrefixLength = seedPrefixLength;
        _patches = patches;
        _constantBytes = constantBytes;
        _suffix = suffix;
    }

    internal string Id { get; }

    internal string Family { get; }

    internal string Mutation { get; }

    internal HostileArtifactExpectedOutcome ExpectedOutcome { get; }

    internal long ArtifactLength { get; }

    internal bool IsSparseLengthOnly => _kind == HostileArtifactMaterializationKind.SparseLength;

    internal static HostileArtifactCase FromConstant(
        string id,
        string family,
        string mutation,
        HostileArtifactExpectedOutcome expectedOutcome,
        ImmutableArray<byte> bytes) =>
        new(
            id,
            family,
            mutation,
            expectedOutcome,
            bytes.Length,
            HostileArtifactMaterializationKind.Constant,
            seedLength: 0,
            seedPrefixLength: 0,
            ImmutableArray<HostileArtifactPatch>.Empty,
            bytes,
            ImmutableArray<byte>.Empty);

    internal static HostileArtifactCase FromSeed(
        string id,
        string family,
        string mutation,
        HostileArtifactExpectedOutcome expectedOutcome,
        long seedLength,
        long seedPrefixLength,
        ImmutableArray<HostileArtifactPatch> patches = default,
        ImmutableArray<byte> suffix = default)
    {
        var normalizedPatches = patches.IsDefault
            ? ImmutableArray<HostileArtifactPatch>.Empty
            : patches.OrderBy(static patch => patch.Offset).ToImmutableArray();
        var normalizedSuffix = suffix.IsDefault ? ImmutableArray<byte>.Empty : suffix;
        if (seedLength < 0 || seedPrefixLength < 0 || seedPrefixLength > seedLength)
        {
            throw new ArgumentOutOfRangeException(nameof(seedPrefixLength));
        }

        if (normalizedPatches.Any(patch => patch.Offset < 0 || patch.Offset >= seedPrefixLength))
        {
            throw new ArgumentOutOfRangeException(nameof(patches));
        }

        for (var index = 1; index < normalizedPatches.Length; index++)
        {
            if (normalizedPatches[index - 1].Offset == normalizedPatches[index].Offset)
            {
                throw new ArgumentException("A mutation cannot patch one seed offset more than once.", nameof(patches));
            }
        }

        var artifactLength = checked(seedPrefixLength + normalizedSuffix.Length);
        return new HostileArtifactCase(
            id,
            family,
            mutation,
            expectedOutcome,
            artifactLength,
            HostileArtifactMaterializationKind.Seed,
            seedLength,
            seedPrefixLength,
            normalizedPatches,
            ImmutableArray<byte>.Empty,
            normalizedSuffix);
    }

    internal static HostileArtifactCase SparseLength(
        string id,
        string family,
        string mutation,
        HostileArtifactExpectedOutcome expectedOutcome,
        long artifactLength)
    {
        if (artifactLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(artifactLength));
        }

        return new HostileArtifactCase(
            id,
            family,
            mutation,
            expectedOutcome,
            artifactLength,
            HostileArtifactMaterializationKind.SparseLength,
            seedLength: 0,
            seedPrefixLength: 0,
            ImmutableArray<HostileArtifactPatch>.Empty,
            ImmutableArray<byte>.Empty,
            ImmutableArray<byte>.Empty);
    }

    internal void Materialize(Stream seed, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(seed, destination))
        {
            throw new ArgumentException("Seed and destination streams must be distinct.", nameof(destination));
        }

        if (!destination.CanWrite || !destination.CanSeek)
        {
            throw new ArgumentException("The destination must be a writable, seekable stream.", nameof(destination));
        }

        if (!seed.CanRead || !seed.CanSeek)
        {
            throw new ArgumentException("The seed must be a readable, seekable stream.", nameof(seed));
        }

        var seedPosition = seed.Position;
        try
        {
            destination.Position = 0;
            destination.SetLength(0);
            switch (_kind)
            {
                case HostileArtifactMaterializationKind.Constant:
                    destination.Write(_constantBytes.AsSpan());
                    break;

                case HostileArtifactMaterializationKind.Seed:
                    if (seed.Length != _seedLength)
                    {
                        throw new InvalidDataException("The materialization seed length differs from the planned corpus seed.");
                    }

                    seed.Position = 0;
                    CopyExactly(seed, destination, _seedPrefixLength);
                    destination.Write(_suffix.AsSpan());
                    foreach (var patch in _patches)
                    {
                        destination.Position = patch.Offset;
                        destination.WriteByte(patch.Value);
                    }

                    break;

                case HostileArtifactMaterializationKind.SparseLength:
                    destination.SetLength(ArtifactLength);
                    break;

                default:
                    throw new InvalidOperationException("The hostile-artifact materialization kind is invalid.");
            }

            destination.SetLength(ArtifactLength);
            destination.Position = 0;
        }
        finally
        {
            seed.Position = seedPosition;
        }
    }

    private static void CopyExactly(Stream source, Stream destination, long length)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var remaining = length;
            while (remaining > 0)
            {
                var requested = (int)Math.Min(buffer.Length, remaining);
                var count = source.Read(buffer, 0, requested);
                if (count == 0)
                {
                    throw new EndOfStreamException("The minidump seed ended during corpus materialization.");
                }

                destination.Write(buffer, 0, count);
                remaining -= count;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}

internal readonly record struct HostileArtifactPatch(long Offset, byte Value);

internal enum HostileArtifactMaterializationKind
{
    Seed,
    Constant,
    SparseLength,
}
