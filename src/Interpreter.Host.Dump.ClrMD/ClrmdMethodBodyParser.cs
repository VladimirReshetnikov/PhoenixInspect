using System.Buffers.Binary;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Parses the bounded ECMA-335 physical method-body subset directly from counted target-memory reads.
/// </summary>
internal static class ClrmdMethodBodyParser
{
    private const int MaximumCodeBytes = 4_096;
    private const int MaximumExtraSectionCount = 16;
    private const int MaximumExtraSectionBytes = 64 * 1_024;
    private const int MaximumExceptionRegionCount = 1_024;
    private const ushort FatFormat = 0x0003;
    private const ushort MoreSections = 0x0008;
    private const ushort InitLocals = 0x0010;
    private const ushort KnownFatFlags = FatFormat | MoreSections | InitLocals;
    private const byte ExceptionHandlingTable = 0x01;
    private const byte SectionKindMask = 0x3F;
    private const byte SectionFatFormat = 0x40;
    private const byte SectionMoreSections = 0x80;

    internal static ClrmdEvidenceResult<ClrmdMethodBodyInfo> Read(
        IProcessMemoryReader memory,
        int metadataToken,
        int relativeVirtualAddress,
        ulong imageBase,
        ulong imageSize,
        int standaloneSignatureRowCount,
        ImmutableArray<MemoryReadResult> initialEvidence = default)
    {
        ArgumentNullException.ThrowIfNull(memory);

        var evidence = ImmutableArray.CreateBuilder<MemoryReadResult>();
        if (!initialEvidence.IsDefaultOrEmpty)
        {
            evidence.AddRange(initialEvidence);
        }

        if (relativeVirtualAddress <= 0 || imageBase == 0 || imageSize == 0 || standaloneSignatureRowCount < 0 ||
            !TryAdd(imageBase, (uint)relativeVirtualAddress, out var headerAddress))
        {
            return Invalid(evidence);
        }

        var headerEvidence = ImmutableArray.CreateBuilder<MemoryReadResult>();
        var extraSectionEvidence = ImmutableArray.CreateBuilder<MemoryReadResult>();
        var readFailure = ReadRequired(
            memory,
            imageBase,
            imageSize,
            headerAddress,
            1,
            evidence,
            headerEvidence,
            out var firstHeaderByte);
        if (readFailure is not null)
        {
            return readFailure;
        }

        ClrmdMethodHeaderKind headerKind;
        int headerSize;
        int codeSize;
        int maxStack;
        bool localVariablesInitialized;
        int localSignatureToken;
        bool hasMoreSections;

        var firstByte = firstHeaderByte.Bytes[0];
        switch (firstByte & 0x03)
        {
            case 0x02:
                headerKind = ClrmdMethodHeaderKind.Tiny;
                headerSize = 1;
                codeSize = firstByte >> 2;
                maxStack = 8;
                localVariablesInitialized = false;
                localSignatureToken = 0;
                hasMoreSections = false;
                break;

            case 0x03:
            {
                headerKind = ClrmdMethodHeaderKind.Fat;
                if ((headerAddress & 0x03) != 0)
                {
                    return Invalid(evidence);
                }

                if (!TryAdd(headerAddress, 1, out var secondByteAddress))
                {
                    return Invalid(evidence);
                }

                readFailure = ReadRequired(
                    memory,
                    imageBase,
                    imageSize,
                    secondByteAddress,
                    1,
                    evidence,
                    headerEvidence,
                    out var secondHeaderByte);
                if (readFailure is not null)
                {
                    return readFailure;
                }

                var flagsAndSize = (ushort)(firstByte | (secondHeaderByte.Bytes[0] << 8));
                var headerDwordCount = flagsAndSize >> 12;
                if (headerDwordCount < 3)
                {
                    return Invalid(evidence);
                }

                headerSize = headerDwordCount * sizeof(uint);
                if (!TryAdd(headerAddress, 2, out var remainingHeaderAddress))
                {
                    return Invalid(evidence);
                }

                readFailure = ReadRequired(
                    memory,
                    imageBase,
                    imageSize,
                    remainingHeaderAddress,
                    headerSize - 2,
                    evidence,
                    headerEvidence,
                    out var remainingHeader);
                if (readFailure is not null)
                {
                    return readFailure;
                }

                if (headerDwordCount != 3)
                {
                    return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.MethodHeaderUnsupported,
                        evidence: evidence.ToImmutable());
                }

                var flags = (ushort)(flagsAndSize & 0x0FFF);
                if ((flags & 0x0003) != FatFormat || (flags & ~KnownFatFlags) != 0)
                {
                    return Invalid(evidence);
                }

                Span<byte> header = stackalloc byte[12];
                header[0] = firstByte;
                header[1] = secondHeaderByte.Bytes[0];
                remainingHeader.Bytes.AsSpan().CopyTo(header[2..]);
                maxStack = BinaryPrimitives.ReadUInt16LittleEndian(header[2..4]);
                var declaredCodeSize = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
                if (declaredCodeSize > MaximumCodeBytes)
                {
                    return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.LimitExceeded,
                        evidence: evidence.ToImmutable());
                }

                codeSize = checked((int)declaredCodeSize);
                var declaredLocalSignature = BinaryPrimitives.ReadUInt32LittleEndian(header[8..12]);
                if (!TryNormalizeLocalSignature(
                        declaredLocalSignature,
                        standaloneSignatureRowCount,
                        out localSignatureToken))
                {
                    return Invalid(evidence);
                }

                localVariablesInitialized = (flags & InitLocals) != 0;
                hasMoreSections = (flags & MoreSections) != 0;
                break;
            }

            default:
                return Invalid(evidence);
        }

        if (!TryAdd(headerAddress, (ulong)headerSize, out var codeAddress))
        {
            return Invalid(evidence);
        }

        readFailure = ReadRequired(
            memory,
            imageBase,
            imageSize,
            codeAddress,
            codeSize,
            evidence,
            categoryEvidence: null,
            out var code);
        if (readFailure is not null)
        {
            return readFailure;
        }

        var exceptionRegionCount = 0;
        if (hasMoreSections)
        {
            if (!TryAdd(codeAddress, (ulong)codeSize, out var codeEnd) ||
                !TryAlign4(codeEnd, out var sectionAddress))
            {
                return Invalid(evidence);
            }

            var totalExtraSectionBytes = 0;
            readFailure = ReadPadding(
                memory,
                imageBase,
                imageSize,
                codeEnd,
                sectionAddress,
                evidence,
                extraSectionEvidence,
                ref totalExtraSectionBytes);
            if (readFailure is not null)
            {
                return readFailure;
            }

            for (var sectionIndex = 0; ; sectionIndex++)
            {
                if (sectionIndex == MaximumExtraSectionCount)
                {
                    return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.LimitExceeded,
                        evidence: evidence.ToImmutable());
                }

                if (!TryAddExtraBytes(sizeof(uint), ref totalExtraSectionBytes))
                {
                    return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.LimitExceeded,
                        evidence: evidence.ToImmutable());
                }

                readFailure = ReadRequired(
                    memory,
                    imageBase,
                    imageSize,
                    sectionAddress,
                    sizeof(uint),
                    evidence,
                    extraSectionEvidence,
                    out var sectionHeader);
                if (readFailure is not null)
                {
                    return readFailure;
                }

                var sectionKindAndFlags = sectionHeader.Bytes[0];
                var sectionKind = sectionKindAndFlags & SectionKindMask;
                if (sectionKind != ExceptionHandlingTable)
                {
                    return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.MethodSectionUnsupported,
                        evidence: evidence.ToImmutable());
                }

                var isFatSection = (sectionKindAndFlags & SectionFatFormat) != 0;
                var hasAnotherSection = (sectionKindAndFlags & SectionMoreSections) != 0;
                int dataSize;
                int clauseSize;
                if (isFatSection)
                {
                    dataSize = sectionHeader.Bytes[1] |
                        (sectionHeader.Bytes[2] << 8) |
                        (sectionHeader.Bytes[3] << 16);
                    clauseSize = 24;
                }
                else
                {
                    if (sectionHeader.Bytes[2] != 0 || sectionHeader.Bytes[3] != 0)
                    {
                        return Invalid(evidence);
                    }

                    dataSize = sectionHeader.Bytes[1];
                    clauseSize = 12;
                }

                if (dataSize < sizeof(uint) || (dataSize - sizeof(uint)) % clauseSize != 0)
                {
                    return Invalid(evidence);
                }

                var payloadSize = dataSize - sizeof(uint);
                var clauseCount = payloadSize / clauseSize;
                if (clauseCount > MaximumExceptionRegionCount - exceptionRegionCount ||
                    !TryAddExtraBytes(payloadSize, ref totalExtraSectionBytes))
                {
                    return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.LimitExceeded,
                        evidence: evidence.ToImmutable());
                }

                exceptionRegionCount += clauseCount;
                if (!TryAdd(sectionAddress, sizeof(uint), out var sectionPayloadAddress))
                {
                    return Invalid(evidence);
                }

                if (payloadSize > 0)
                {
                    readFailure = ReadRequired(
                        memory,
                        imageBase,
                        imageSize,
                        sectionPayloadAddress,
                        payloadSize,
                        evidence,
                        extraSectionEvidence,
                        out _);
                    if (readFailure is not null)
                    {
                        return readFailure;
                    }
                }

                if (!TryAdd(sectionAddress, (ulong)dataSize, out var sectionEnd))
                {
                    return Invalid(evidence);
                }

                if (!hasAnotherSection)
                {
                    break;
                }

                if (!TryAlign4(sectionEnd, out var nextSectionAddress))
                {
                    return Invalid(evidence);
                }

                readFailure = ReadPadding(
                    memory,
                    imageBase,
                    imageSize,
                    sectionEnd,
                    nextSectionAddress,
                    evidence,
                    extraSectionEvidence,
                    ref totalExtraSectionBytes);
                if (readFailure is not null)
                {
                    return readFailure;
                }

                sectionAddress = nextSectionAddress;
            }
        }

        var body = MethodBody.Create(
            maxStack,
            code.Bytes.AsSpan(),
            localVariablesInitialized,
            localSignatureToken,
            exceptionRegionCount);
        var value = new ClrmdMethodBodyInfo(
            metadataToken,
            relativeVirtualAddress,
            headerAddress,
            headerKind,
            headerEvidence.ToImmutable(),
            codeAddress,
            code,
            extraSectionEvidence.ToImmutable(),
            body);
        return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            value,
            evidence.ToImmutable());
    }

    private static ClrmdEvidenceResult<ClrmdMethodBodyInfo>? ReadPadding(
        IProcessMemoryReader memory,
        ulong imageBase,
        ulong imageSize,
        ulong unalignedAddress,
        ulong alignedAddress,
        ImmutableArray<MemoryReadResult>.Builder evidence,
        ImmutableArray<MemoryReadResult>.Builder extraSectionEvidence,
        ref int totalExtraSectionBytes)
    {
        var paddingSize = checked((int)(alignedAddress - unalignedAddress));
        if (paddingSize == 0)
        {
            return null;
        }

        if (!TryAddExtraBytes(paddingSize, ref totalExtraSectionBytes))
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.LimitExceeded,
                evidence: evidence.ToImmutable());
        }

        return ReadRequired(
            memory,
            imageBase,
            imageSize,
            unalignedAddress,
            paddingSize,
            evidence,
            extraSectionEvidence,
            out _);
    }

    private static ClrmdEvidenceResult<ClrmdMethodBodyInfo>? ReadRequired(
        IProcessMemoryReader memory,
        ulong imageBase,
        ulong imageSize,
        ulong address,
        int length,
        ImmutableArray<MemoryReadResult>.Builder evidence,
        ImmutableArray<MemoryReadResult>.Builder? categoryEvidence,
        out MemoryReadResult read)
    {
        read = null!;
        if (length < 0 || !IsRangeWithinExtent(imageBase, imageSize, address, (ulong)length))
        {
            return Invalid(evidence);
        }

        if (length > memory.MaximumReadLength)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.LimitExceeded,
                evidence: evidence.ToImmutable());
        }

        read = memory.Read(address, length);
        evidence.Add(read);
        categoryEvidence?.Add(read);
        if (read.Status == MemoryReadStatus.Exact)
        {
            return null;
        }

        return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
            read.Status == MemoryReadStatus.Partial
                ? ClrmdEvidenceStatus.Partial
                : ClrmdEvidenceStatus.Unavailable,
            ClrmdValueIssue.MemoryUnavailable,
            evidence: evidence.ToImmutable());
    }

    private static bool TryNormalizeLocalSignature(
        uint token,
        int standaloneSignatureRowCount,
        out int normalizedToken)
    {
        if (token == 0)
        {
            normalizedToken = 0;
            return true;
        }

        var rid = token & 0x00FFFFFF;
        if ((token & 0xFF000000) != 0x11000000 || rid == 0 || rid > (uint)standaloneSignatureRowCount)
        {
            normalizedToken = 0;
            return false;
        }

        normalizedToken = checked((int)token);
        return true;
    }

    private static bool TryAddExtraBytes(int byteCount, ref int total)
    {
        if (byteCount < 0 || byteCount > MaximumExtraSectionBytes - total)
        {
            return false;
        }

        total += byteCount;
        return true;
    }

    private static bool TryAdd(ulong address, ulong offset, out ulong result)
    {
        if (address > ulong.MaxValue - offset)
        {
            result = 0;
            return false;
        }

        result = address + offset;
        return true;
    }

    private static bool TryAlign4(ulong address, out ulong alignedAddress)
    {
        var padding = (4UL - (address & 0x03)) & 0x03;
        return TryAdd(address, padding, out alignedAddress);
    }

    private static bool IsRangeWithinExtent(
        ulong extentAddress,
        ulong extentSize,
        ulong rangeAddress,
        ulong rangeLength)
    {
        if (extentSize > ulong.MaxValue - extentAddress || rangeAddress < extentAddress)
        {
            return false;
        }

        var offset = rangeAddress - extentAddress;
        return offset <= extentSize && rangeLength <= extentSize - offset;
    }

    private static ClrmdEvidenceResult<ClrmdMethodBodyInfo> Invalid(
        ImmutableArray<MemoryReadResult>.Builder evidence) =>
        ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
            ClrmdEvidenceStatus.Invalid,
            ClrmdValueIssue.InvalidData,
            evidence: evidence.ToImmutable());
}
