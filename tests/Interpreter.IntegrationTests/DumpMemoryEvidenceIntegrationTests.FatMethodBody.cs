using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Adds the real compiler-emitted fat-method-body proof to the generated full-dump scenario.
/// </summary>
public sealed partial class DumpMemoryEvidenceIntegrationTests
{
    private const string FatMethodName = "FatBodyWithLocalsAndExceptionRegions";

    private static void AssertFatMethodBodyEvidence(
        ClrmdDumpSession session,
        ClrmdModuleInfo module,
        string targetAssemblyPath)
    {
        var result = session.ReadMethodBody(module, "Program", FatMethodName);
        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        Assert.Equal(ClrmdValueIssue.None, result.Issue);
        var dumpMethod = result.Value ??
            throw new InvalidOperationException("Exact fat method-body result carried no value.");

        Assert.True(dumpMethod.RelativeVirtualAddress > 0);
        Assert.Equal(
            module.Identity.ImageBase + (ulong)dumpMethod.RelativeVirtualAddress,
            dumpMethod.HeaderAddress);
        Assert.Equal(ClrmdMethodHeaderKind.Fat, dumpMethod.HeaderKind);

        var metadataEvidence = Assert.Single(result.Evidence.Take(1));
        AssertExactRead(
            metadataEvidence,
            session.Memory.SourceId,
            module.MetadataAddress,
            checked((int)module.MetadataLength));
        using (var provider = MetadataReaderProvider.FromMetadataImage(metadataEvidence.Bytes))
        {
            var dumpMetadata = provider.GetMetadataReader();
            Assert.Equal(0x06000000, dumpMethod.MetadataToken & unchecked((int)0xFF000000));
            var methodRow = dumpMethod.MetadataToken & 0x00FFFFFF;
            Assert.NotEqual(0, methodRow);
            var definition = dumpMetadata.GetMethodDefinition(
                MetadataTokens.MethodDefinitionHandle(methodRow));
            Assert.Equal(FatMethodName, dumpMetadata.GetString(definition.Name));
            Assert.Equal(dumpMethod.RelativeVirtualAddress, definition.RelativeVirtualAddress);
        }

        Assert.Collection(
            dumpMethod.HeaderEvidence,
            first => AssertExactRead(first, session.Memory.SourceId, dumpMethod.HeaderAddress, 1),
            second => AssertExactRead(second, session.Memory.SourceId, dumpMethod.HeaderAddress + 1, 1),
            remainder => AssertExactRead(remainder, session.Memory.SourceId, dumpMethod.HeaderAddress + 2, 10));
        var headerBytes = dumpMethod.HeaderEvidence.SelectMany(static read => read.Bytes).ToArray();
        Assert.Equal(12, headerBytes.Length);
        var flagsAndSize = BinaryPrimitives.ReadUInt16LittleEndian(headerBytes.AsSpan(0, 2));
        Assert.Equal(3, flagsAndSize >> 12);
        Assert.Equal(0x0003, flagsAndSize & 0x0003);
        Assert.NotEqual(0, flagsAndSize & 0x0008);
        Assert.NotEqual(0, flagsAndSize & 0x0010);
        Assert.Equal(dumpMethod.MaxStack, BinaryPrimitives.ReadUInt16LittleEndian(headerBytes.AsSpan(2, 2)));
        Assert.Equal(
            dumpMethod.Code.Bytes.Length,
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(4, 4))));
        Assert.Equal(
            dumpMethod.LocalSignatureToken,
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(8, 4))));
        Assert.Equal(0x11000000, dumpMethod.LocalSignatureToken & unchecked((int)0xFF000000));
        Assert.NotEqual(0, dumpMethod.LocalSignatureToken & 0x00FFFFFF);
        Assert.True(dumpMethod.LocalVariablesInitialized);

        Assert.Equal(dumpMethod.HeaderAddress + 12, dumpMethod.CodeAddress);
        AssertExactRead(
            dumpMethod.Code,
            session.Memory.SourceId,
            dumpMethod.CodeAddress,
            dumpMethod.Code.Bytes.Length);
        Assert.NotEmpty(dumpMethod.Code.Bytes);

        var countedExceptionRegions = AssertExactExtraSections(dumpMethod, session.Memory.SourceId);
        Assert.Equal(2, countedExceptionRegions);
        Assert.Equal(countedExceptionRegions, dumpMethod.ExceptionRegionCount);

        var physicalReads = dumpMethod.HeaderEvidence
            .Append(dumpMethod.Code)
            .Concat(dumpMethod.ExtraSectionEvidence)
            .ToArray();
        Assert.Equal(physicalReads.Length + 1, result.Evidence.Length);
        for (var index = 0; index < physicalReads.Length; index++)
        {
            Assert.Same(physicalReads[index], result.Evidence[index + 1]);
        }

        // Open the complete PE only after the dump-backed body has been constructed and physically validated. The
        // resulting SRM body is an equality oracle; no disk byte or SRM body participates in the dump read above.
        var diskOracle = ReadWholeFileOracle(targetAssemblyPath);
        Assert.Equal(diskOracle.MetadataToken, dumpMethod.MetadataToken);
        Assert.Equal(diskOracle.RelativeVirtualAddress, dumpMethod.RelativeVirtualAddress);
        Assert.Equal(diskOracle.MaxStack, dumpMethod.MaxStack);
        Assert.Equal(diskOracle.CodeBytes, dumpMethod.Code.Bytes.ToArray());
        Assert.Equal(diskOracle.LocalVariablesInitialized, dumpMethod.LocalVariablesInitialized);
        Assert.Equal(diskOracle.LocalSignatureToken, dumpMethod.LocalSignatureToken);
        Assert.Equal(diskOracle.ExceptionRegionCount, dumpMethod.ExceptionRegionCount);
        Assert.True(diskOracle.LocalSignatureToken != 0);
        Assert.True(diskOracle.ExceptionRegionCount > 0);
    }

    private static int AssertExactExtraSections(ClrmdMethodBodyInfo method, string sourceId)
    {
        var codeEnd = method.CodeAddress + (ulong)method.Code.Bytes.Length;
        var sectionAddress = Align4(codeEnd);
        var initialPaddingLength = checked((int)(sectionAddress - codeEnd));
        Assert.InRange(initialPaddingLength, 1, 3);

        var evidenceIndex = 0;
        AssertExactRead(
            method.ExtraSectionEvidence[evidenceIndex++],
            sourceId,
            codeEnd,
            initialPaddingLength);

        var exceptionRegionCount = 0;
        while (true)
        {
            var sectionHeader = method.ExtraSectionEvidence[evidenceIndex++];
            AssertExactRead(sectionHeader, sourceId, sectionAddress, sizeof(uint));
            var kindAndFlags = sectionHeader.Bytes[0];
            Assert.Equal(0x01, kindAndFlags & 0x3F);
            var isFat = (kindAndFlags & 0x40) != 0;
            var hasMoreSections = (kindAndFlags & 0x80) != 0;
            var dataSize = isFat
                ? sectionHeader.Bytes[1] |
                    (sectionHeader.Bytes[2] << 8) |
                    (sectionHeader.Bytes[3] << 16)
                : sectionHeader.Bytes[1];
            var clauseSize = isFat ? 24 : 12;
            Assert.True(dataSize >= sizeof(uint));
            Assert.Equal(0, (dataSize - sizeof(uint)) % clauseSize);

            var payloadSize = dataSize - sizeof(uint);
            Assert.True(payloadSize > 0);
            AssertExactRead(
                method.ExtraSectionEvidence[evidenceIndex++],
                sourceId,
                sectionAddress + sizeof(uint),
                payloadSize);
            exceptionRegionCount += payloadSize / clauseSize;
            var sectionEnd = sectionAddress + (ulong)dataSize;
            if (!hasMoreSections)
            {
                Assert.Equal(evidenceIndex, method.ExtraSectionEvidence.Length);
                return exceptionRegionCount;
            }

            sectionAddress = Align4(sectionEnd);
            var paddingLength = checked((int)(sectionAddress - sectionEnd));
            if (paddingLength > 0)
            {
                AssertExactRead(
                    method.ExtraSectionEvidence[evidenceIndex++],
                    sourceId,
                    sectionEnd,
                    paddingLength);
            }
        }
    }

    private static void AssertExactRead(
        MemoryReadResult read,
        string sourceId,
        ulong address,
        int length)
    {
        Assert.Equal(MemoryReadStatus.Exact, read.Status);
        Assert.Equal(sourceId, read.SourceId);
        Assert.Equal(address, read.Address);
        Assert.Equal(length, read.RequestedLength);
        Assert.Equal(length, read.BytesRead);
        Assert.Equal(0, read.MissingByteCount);
    }

    private static DiskMethodBodyOracle ReadWholeFileOracle(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        MethodDefinitionHandle selectedMethod = default;
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeHandle);
            if (!string.Equals(metadata.GetString(type.Name), "Program", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(metadata.GetString(type.Namespace)))
            {
                continue;
            }

            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                if (string.Equals(metadata.GetString(method.Name), FatMethodName, StringComparison.Ordinal))
                {
                    Assert.True(selectedMethod.IsNil, $"Expected one MethodDef named '{FatMethodName}'.");
                    selectedMethod = methodHandle;
                }
            }
        }

        Assert.False(selectedMethod.IsNil, $"Expected a MethodDef named '{FatMethodName}'.");
        var definition = metadata.GetMethodDefinition(selectedMethod);
        var body = peReader.GetMethodBody(definition.RelativeVirtualAddress);
        var codeBytes = body.GetILBytes();
        Assert.NotNull(codeBytes);
        return new DiskMethodBodyOracle(
            MetadataTokens.GetToken(selectedMethod),
            definition.RelativeVirtualAddress,
            body.MaxStack,
            codeBytes,
            body.LocalVariablesInitialized,
            body.LocalSignature.IsNil ? 0 : MetadataTokens.GetToken(body.LocalSignature),
            body.ExceptionRegions.Length);
    }

    private static ulong Align4(ulong value) => (value + 3) & ~3UL;

    private sealed record DiskMethodBodyOracle(
        int MetadataToken,
        int RelativeVirtualAddress,
        int MaxStack,
        byte[] CodeBytes,
        bool LocalVariablesInitialized,
        int LocalSignatureToken,
        int ExceptionRegionCount);
}
