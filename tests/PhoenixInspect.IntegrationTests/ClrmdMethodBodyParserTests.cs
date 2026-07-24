using System.Buffers.Binary;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the bounded dump-memory method-body parser without requiring a process dump.</summary>
public sealed class ClrmdMethodBodyParserTests
{
    private const ulong ImageBase = 0x1000;
    private const int MethodRva = 0x20;
    private const int MethodToken = 0x06000001;

    /// <summary>Verifies the ECMA tiny-header defaults and exact-only normalized body projection.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Tiny_body_is_derived_from_counted_header_and_code_reads()
    {
        var image = new byte[0x40];
        image[MethodRva] = 0x06;
        image[MethodRva + 1] = 0x2A;

        var result = Parse(image);

        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        Assert.Equal(ClrmdValueIssue.None, result.Issue);
        var value = Assert.IsType<ClrmdMethodBodyInfo>(result.Value);
        Assert.Equal(ClrmdMethodHeaderKind.Tiny, value.HeaderKind);
        Assert.Equal(ImageBase + MethodRva, value.HeaderAddress);
        Assert.Equal(value.HeaderAddress + 1, value.CodeAddress);
        Assert.Equal(8, value.MaxStack);
        Assert.False(value.LocalVariablesInitialized);
        Assert.Equal(0, value.LocalSignatureToken);
        Assert.Equal(0, value.ExceptionRegionCount);
        Assert.Equal(new byte[] { 0x2A }, value.Body.CodeBytes.ToArray());
        Assert.Single(value.HeaderEvidence);
        Assert.Empty(value.ExtraSectionEvidence);
        Assert.Equal(2, result.Evidence.Length);
    }

    /// <summary>Verifies current fat-header stack, initialization, and StandAloneSig facts.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Fat_body_derives_admission_facts_without_runtime_decoded_header_fields()
    {
        var image = new byte[0x50];
        WriteFatHeader(
            image,
            flags: 0x0010,
            maxStack: 9,
            codeSize: 1,
            localSignatureToken: 0x11000001);
        image[MethodRva + 12] = 0x2A;

        var result = Parse(image, standaloneSignatureRowCount: 1);

        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        var value = Assert.IsType<ClrmdMethodBodyInfo>(result.Value);
        Assert.Equal(ClrmdMethodHeaderKind.Fat, value.HeaderKind);
        Assert.Equal(9, value.MaxStack);
        Assert.True(value.LocalVariablesInitialized);
        Assert.Equal(0x11000001, value.LocalSignatureToken);
        Assert.Equal(0, value.ExceptionRegionCount);
        Assert.Equal(new[] { 1, 1, 10 }, value.HeaderEvidence.Select(read => read.RequestedLength).ToArray());
        Assert.Equal(new byte[] { 0x2A }, value.Code.Bytes.ToArray());
    }

    /// <summary>Verifies small, fat, and chained exception tables are counted from retained raw sections.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Chained_exception_sections_are_bounded_counted_and_retained()
    {
        var image = new byte[0x90];
        WriteFatHeader(image, flags: 0x0008, maxStack: 8, codeSize: 1, localSignatureToken: 0);
        image[MethodRva + 12] = 0x2A;
        image[0x2D] = 0xAA;
        image[0x2E] = 0xBB;
        image[0x2F] = 0xCC;

        image[0x30] = 0x81;
        image[0x31] = 16;

        image[0x40] = 0x41;
        image[0x41] = 52;

        var result = Parse(image);

        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        var value = Assert.IsType<ClrmdMethodBodyInfo>(result.Value);
        Assert.Equal(3, value.ExceptionRegionCount);
        Assert.Equal(5, value.ExtraSectionEvidence.Length);
        Assert.Equal(new[] { 3, 4, 12, 4, 48 },
            value.ExtraSectionEvidence.Select(read => read.RequestedLength).ToArray());
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, value.ExtraSectionEvidence[0].Bytes.ToArray());
        Assert.All(value.ExtraSectionEvidence, read => Assert.Equal(MemoryReadStatus.Exact, read.Status));
    }

    /// <summary>Verifies a short required read retains evidence but cannot expose an executable body.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Partial_code_read_never_exposes_method_body()
    {
        var capturedPrefix = new byte[MethodRva + 13];
        WriteFatHeader(capturedPrefix, flags: 0, maxStack: 8, codeSize: 2, localSignatureToken: 0);
        capturedPrefix[MethodRva + 12] = 0x2A;

        var result = Parse(capturedPrefix, reportedImageSize: 0x100);

        Assert.Equal(ClrmdEvidenceStatus.Partial, result.Status);
        Assert.Equal(ClrmdValueIssue.MemoryUnavailable, result.Issue);
        Assert.Null(result.Value);
        var codeRead = result.Evidence[^1];
        Assert.Equal(MemoryReadStatus.Partial, codeRead.Status);
        Assert.Equal(2, codeRead.RequestedLength);
        Assert.Equal(new byte[] { 0x2A }, codeRead.Bytes.ToArray());
    }

    /// <summary>Verifies one-byte and eleven-byte fat-header prefixes retain only observed bytes.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Truncated_fat_headers_are_unavailable_or_partial_without_a_body()
    {
        var oneByteHeader = new byte[MethodRva + 1];
        oneByteHeader[MethodRva] = 0x03;
        var unavailable = Parse(oneByteHeader, reportedImageSize: 0x100);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, unavailable.Status);
        Assert.Equal(ClrmdValueIssue.MemoryUnavailable, unavailable.Issue);
        Assert.Null(unavailable.Value);
        Assert.Equal(MemoryReadStatus.Exact, unavailable.Evidence[0].Status);
        Assert.Equal(MemoryReadStatus.Unavailable, unavailable.Evidence[1].Status);

        var completeImage = new byte[0x50];
        WriteFatHeader(completeImage, flags: 0, maxStack: 8, codeSize: 1, localSignatureToken: 0);
        var elevenByteHeader = completeImage[..(MethodRva + 11)];
        var partial = Parse(elevenByteHeader, reportedImageSize: 0x100);
        Assert.Equal(ClrmdEvidenceStatus.Partial, partial.Status);
        Assert.Equal(ClrmdValueIssue.MemoryUnavailable, partial.Issue);
        Assert.Null(partial.Value);
        Assert.Equal(MemoryReadStatus.Partial, partial.Evidence[^1].Status);
        Assert.Equal(10, partial.Evidence[^1].RequestedLength);
        Assert.Equal(9, partial.Evidence[^1].BytesRead);
    }

    /// <summary>Verifies malformed formats, local tokens, and section sizes fail as invalid evidence.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Malformed_physical_shapes_fail_closed()
    {
        var invalidFormat = new byte[0x40];
        AssertInvalid(Parse(invalidFormat));

        var invalidLocalToken = new byte[0x50];
        WriteFatHeader(invalidLocalToken, flags: 0, maxStack: 8, codeSize: 1, localSignatureToken: 0x12000001);
        invalidLocalToken[MethodRva + 12] = 0x2A;
        AssertInvalid(Parse(invalidLocalToken, standaloneSignatureRowCount: 1));

        var invalidLocalRid = new byte[0x50];
        WriteFatHeader(invalidLocalRid, flags: 0, maxStack: 8, codeSize: 1, localSignatureToken: 0x11000002);
        invalidLocalRid[MethodRva + 12] = 0x2A;
        AssertInvalid(Parse(invalidLocalRid, standaloneSignatureRowCount: 1));

        var invalidSection = new byte[0x50];
        WriteFatHeader(invalidSection, flags: 0x0008, maxStack: 8, codeSize: 1, localSignatureToken: 0);
        invalidSection[MethodRva + 12] = 0x2A;
        invalidSection[0x30] = 0x01;
        invalidSection[0x31] = 5;
        AssertInvalid(Parse(invalidSection));

        var invalidReservedBytes = new byte[0x50];
        WriteFatHeader(invalidReservedBytes, flags: 0x0008, maxStack: 8, codeSize: 1, localSignatureToken: 0);
        invalidReservedBytes[MethodRva + 12] = 0x2A;
        invalidReservedBytes[0x30] = 0x01;
        invalidReservedBytes[0x31] = 4;
        invalidReservedBytes[0x32] = 1;
        AssertInvalid(Parse(invalidReservedBytes));
    }

    /// <summary>Verifies address overflow and declared ranges outside the module image fail before a read.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Out_of_image_and_overflowed_method_ranges_are_invalid()
    {
        var image = new byte[0x50];
        WriteFatHeader(image, flags: 0, maxStack: 8, codeSize: 1, localSignatureToken: 0);
        image[MethodRva + 12] = 0x2A;
        AssertInvalid(Parse(image, reportedImageSize: MethodRva + 12));

        var overflow = ClrmdMethodBodyParser.Read(
            new BufferMemoryReader(ulong.MaxValue - 1, Array.Empty<byte>()),
            MethodToken,
            MethodRva,
            ulong.MaxValue - 1,
            1,
            standaloneSignatureRowCount: 0);
        AssertInvalid(overflow);
    }

    /// <summary>Verifies resource limits and semantically unknown extensibility shapes are typed, valueless misses.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Unsupported_and_oversized_shapes_do_not_trigger_unbounded_reads()
    {
        var oversizedCode = new byte[0x50];
        WriteFatHeader(oversizedCode, flags: 0, maxStack: 8, codeSize: 4097, localSignatureToken: 0);
        var oversizedResult = Parse(oversizedCode, reportedImageSize: 0x4000);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, oversizedResult.Status);
        Assert.Equal(ClrmdValueIssue.LimitExceeded, oversizedResult.Issue);
        Assert.Null(oversizedResult.Value);

        var extendedHeader = new byte[0x60];
        BinaryPrimitives.WriteUInt16LittleEndian(
            extendedHeader.AsSpan(MethodRva, sizeof(ushort)),
            0x4003);
        var extendedResult = Parse(extendedHeader);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, extendedResult.Status);
        Assert.Equal(ClrmdValueIssue.MethodHeaderUnsupported, extendedResult.Issue);
        Assert.Null(extendedResult.Value);
        Assert.Equal(new[] { 1, 1, 14 }, extendedResult.Evidence.Select(read => read.RequestedLength).ToArray());

        var unknownSection = new byte[0x50];
        WriteFatHeader(unknownSection, flags: 0x0008, maxStack: 8, codeSize: 1, localSignatureToken: 0);
        unknownSection[MethodRva + 12] = 0x2A;
        unknownSection[0x30] = 0x02;
        unknownSection[0x31] = 4;
        var unknownSectionResult = Parse(unknownSection);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, unknownSectionResult.Status);
        Assert.Equal(ClrmdValueIssue.MethodSectionUnsupported, unknownSectionResult.Issue);
        Assert.Null(unknownSectionResult.Value);

        var excessiveClauses = new byte[0x50];
        WriteFatHeader(excessiveClauses, flags: 0x0008, maxStack: 8, codeSize: 1, localSignatureToken: 0);
        excessiveClauses[MethodRva + 12] = 0x2A;
        const int excessiveDataSize = 4 + (24 * 1_025);
        excessiveClauses[0x30] = 0x41;
        excessiveClauses[0x31] = unchecked((byte)excessiveDataSize);
        excessiveClauses[0x32] = unchecked((byte)(excessiveDataSize >> 8));
        excessiveClauses[0x33] = unchecked((byte)(excessiveDataSize >> 16));
        var excessiveClausesResult = Parse(excessiveClauses, reportedImageSize: 0x10000);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, excessiveClausesResult.Status);
        Assert.Equal(ClrmdValueIssue.LimitExceeded, excessiveClausesResult.Issue);
        Assert.Null(excessiveClausesResult.Value);
    }

    private static ClrmdEvidenceResult<ClrmdMethodBodyInfo> Parse(
        byte[] capturedImage,
        int standaloneSignatureRowCount = 0,
        ulong? reportedImageSize = null) =>
        ClrmdMethodBodyParser.Read(
            new BufferMemoryReader(ImageBase, capturedImage),
            MethodToken,
            MethodRva,
            ImageBase,
            reportedImageSize ?? (ulong)capturedImage.Length,
            standaloneSignatureRowCount);

    private static void WriteFatHeader(
        byte[] image,
        ushort flags,
        ushort maxStack,
        uint codeSize,
        uint localSignatureToken)
    {
        var header = image.AsSpan(MethodRva, 12);
        BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)(0x3003 | flags));
        BinaryPrimitives.WriteUInt16LittleEndian(header[2..], maxStack);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], codeSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], localSignatureToken);
    }

    private static void AssertInvalid(ClrmdEvidenceResult<ClrmdMethodBodyInfo> result)
    {
        Assert.Equal(ClrmdEvidenceStatus.Invalid, result.Status);
        Assert.Equal(ClrmdValueIssue.InvalidData, result.Issue);
        Assert.Null(result.Value);
    }

    private sealed class BufferMemoryReader : IProcessMemoryReader
    {
        private readonly ulong _baseAddress;
        private readonly byte[] _bytes;

        internal BufferMemoryReader(ulong baseAddress, byte[] bytes)
        {
            _baseAddress = baseAddress;
            _bytes = bytes;
        }

        public int PointerSize => sizeof(ulong);

        public int MaximumReadLength => 1 << 20;

        public string SourceId => "method-body-fixture";

        public MemoryReadResult Read(ulong address, int length)
        {
            if (length < 0 || length > MaximumReadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (length > 0 && address > ulong.MaxValue - (ulong)(length - 1))
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (length == 0)
            {
                return MemoryReadResult.Create(SourceId, address, length, ReadOnlySpan<byte>.Empty);
            }

            if (address < _baseAddress || address - _baseAddress >= (ulong)_bytes.Length)
            {
                return MemoryReadResult.Create(SourceId, address, length, ReadOnlySpan<byte>.Empty);
            }

            var offset = checked((int)(address - _baseAddress));
            var bytesRead = Math.Min(length, _bytes.Length - offset);
            return MemoryReadResult.Create(SourceId, address, length, _bytes.AsSpan(offset, bytesRead));
        }
    }
}
