using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Diagnostics.Runtime;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Names one field whose offset may be projected from a CoreCLR runtime contract descriptor.</summary>
internal readonly record struct ClrmdRuntimeContractField(string TypeName, string FieldName);

/// <summary>
/// Freezes the bounded physical evidence and selected field offsets read from one CoreCLR runtime contract
/// descriptor.
/// </summary>
internal sealed class ClrmdRuntimeContractDescriptor
{
    internal ClrmdRuntimeContractDescriptor(
        ulong address,
        uint flags,
        uint size,
        string jsonSha256,
        string pointerDataSha256,
        int loaderContractVersion,
        int runtimeTypeSystemContractVersion,
        int pointerSize,
        ImmutableDictionary<string, ImmutableDictionary<string, int>> layout)
    {
        Address = address;
        Flags = flags;
        Size = size;
        JsonSha256 = jsonSha256;
        PointerDataSha256 = pointerDataSha256;
        LoaderContractVersion = loaderContractVersion;
        RuntimeTypeSystemContractVersion = runtimeTypeSystemContractVersion;
        PointerSize = pointerSize;
        Layout = layout;
    }

    internal ulong Address { get; }

    internal uint Flags { get; }

    internal uint Size { get; }

    internal string JsonSha256 { get; }

    internal string PointerDataSha256 { get; }

    internal int LoaderContractVersion { get; }

    internal int RuntimeTypeSystemContractVersion { get; }

    internal int PointerSize { get; }

    internal ImmutableDictionary<string, ImmutableDictionary<string, int>> Layout { get; }

    internal bool TryGetFieldOffset(string typeName, string fieldName, out int offset)
    {
        if (Layout.TryGetValue(typeName, out var fields) && fields.TryGetValue(fieldName, out offset))
        {
            return true;
        }

        offset = 0;
        return false;
    }
}

/// <summary>Carries one exact descriptor or a typed, diagnostic non-exact disposition.</summary>
internal sealed class ClrmdRuntimeContractDescriptorReadResult
{
    private ClrmdRuntimeContractDescriptorReadResult(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ClrmdRuntimeContractDescriptor? descriptor,
        string diagnostic)
    {
        Status = status;
        Issue = issue;
        Descriptor = descriptor;
        Diagnostic = diagnostic;
    }

    internal ClrmdEvidenceStatus Status { get; }

    internal ClrmdValueIssue Issue { get; }

    internal ClrmdRuntimeContractDescriptor? Descriptor { get; }

    internal string Diagnostic { get; }

    internal static ClrmdRuntimeContractDescriptorReadResult Exact(ClrmdRuntimeContractDescriptor descriptor) =>
        new(ClrmdEvidenceStatus.Exact, ClrmdValueIssue.None, descriptor, string.Empty);

    internal static ClrmdRuntimeContractDescriptorReadResult Stop(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        string diagnostic) =>
        new(status, issue, null, diagnostic);
}

/// <summary>
/// Reads the versioned CoreCLR contract descriptor once through a single bounded, fail-closed parser shared by host
/// and product acquisition paths.
/// </summary>
internal static class ClrmdRuntimeContractDescriptorReader
{
    internal const int MaximumDescriptorByteCount = 4 * 1_024 * 1_024;
    internal const int MaximumPointerDataSlotCount = 65_536;
    internal const int MaximumDescriptorFieldOffset = 1 * 1_024 * 1_024;

    internal static readonly ImmutableArray<ClrmdRuntimeContractField> ModuleEditStateFields =
    [
        new("Module", "Flags"),
        new("Module", "DynamicMetadata"),
    ];

    private const string DescriptorExportName = "DotNetRuntimeContractDescriptor";
    private const ulong DescriptorMagic = 0x0043414443434E44UL;
    private const uint DescriptorPointerSizeFlag = 0x2;

    /// <summary>
    /// Reads a descriptor and returns a typed stop for every expected absence or structural contradiction. Required
    /// fields make their absence non-exact; optional fields are retained when present without changing openability.
    /// </summary>
    internal static ClrmdRuntimeContractDescriptorReadResult Read(
        DataTarget dataTarget,
        ClrInfo clrInfo,
        IReadOnlyCollection<ClrmdRuntimeContractField> requiredFields,
        IReadOnlyCollection<ClrmdRuntimeContractField> optionalFields)
    {
        ArgumentNullException.ThrowIfNull(dataTarget);
        ArgumentNullException.ThrowIfNull(clrInfo);
        ArgumentNullException.ThrowIfNull(requiredFields);
        ArgumentNullException.ThrowIfNull(optionalFields);

        try
        {
            return ClrmdRuntimeContractDescriptorReadResult.Exact(
                ReadCore(dataTarget, clrInfo, requiredFields, optionalFields));
        }
        catch (DescriptorReadException exception)
        {
            return ClrmdRuntimeContractDescriptorReadResult.Stop(
                exception.Status,
                exception.Issue,
                exception.Message);
        }
        catch (Exception exception) when (IsExpectedBackendFailure(exception))
        {
            return ClrmdRuntimeContractDescriptorReadResult.Stop(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.RuntimeContractUnavailable,
                $"The runtime contract descriptor could not be read: {exception.Message}");
        }
    }

    private static ClrmdRuntimeContractDescriptor ReadCore(
        DataTarget dataTarget,
        ClrInfo clrInfo,
        IReadOnlyCollection<ClrmdRuntimeContractField> requiredFields,
        IReadOnlyCollection<ClrmdRuntimeContractField> optionalFields)
    {
        var reader = dataTarget.DataReader;
        if (reader.PointerSize is not (sizeof(uint) or sizeof(ulong)))
        {
            throw Invalid($"A pointer size of {reader.PointerSize} bytes is not admitted.");
        }

        var runtimeModules = reader
            .EnumerateModules()
            .Where(candidate => candidate.ImageBase == clrInfo.ModuleInfo.ImageBase)
            .Take(2)
            .ToArray();
        if (runtimeModules.Length != 1)
        {
            throw Unavailable(
                $"The target exposes {runtimeModules.Length} runtime modules at the reported image base.");
        }

        var descriptorAddress = runtimeModules[0].GetExportSymbolAddress(DescriptorExportName);
        if (descriptorAddress == 0)
        {
            throw Unavailable($"The runtime module does not export {DescriptorExportName}.");
        }

        if (clrInfo.ContractDescriptorAddress != 0 && clrInfo.ContractDescriptorAddress != descriptorAddress)
        {
            throw Invalid("The runtime module export and the reported descriptor addresses disagree.");
        }

        var pointerDataOffset = checked(24 + reader.PointerSize);
        var header = ReadExact(reader, descriptorAddress, checked(pointerDataOffset + reader.PointerSize));
        if (BinaryPrimitives.ReadUInt64LittleEndian(header) != DescriptorMagic)
        {
            throw Invalid(
                $"Descriptor address 0x{descriptorAddress:x} does not carry the little-endian descriptor magic.");
        }

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, sizeof(uint)));
        var descriptorPointerSize = (flags & DescriptorPointerSizeFlag) == 0 ? sizeof(ulong) : sizeof(uint);
        if (descriptorPointerSize != reader.PointerSize)
        {
            throw Invalid("The descriptor pointer size and the target pointer size disagree.");
        }

        var descriptorSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12, sizeof(uint)));
        if (descriptorSize == 0 || descriptorSize > MaximumDescriptorByteCount)
        {
            throw Invalid($"The descriptor byte count {descriptorSize} is outside the admitted range.");
        }

        var jsonAddress = ReadPointerValue(header.AsSpan(16, reader.PointerSize), reader.PointerSize);
        var pointerDataCount = BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan(checked(16 + reader.PointerSize), sizeof(uint)));
        if (pointerDataCount > MaximumPointerDataSlotCount)
        {
            throw Invalid(
                $"The descriptor pointer-data slot count {pointerDataCount} is outside the admitted range.");
        }

        var pointerDataAddress = ReadPointerValue(header.AsSpan(pointerDataOffset, reader.PointerSize), reader.PointerSize);
        if (jsonAddress == 0 || (pointerDataCount != 0 && pointerDataAddress == 0))
        {
            throw Invalid("The descriptor contains a null address for required data.");
        }

        var jsonBytes = ReadExact(reader, jsonAddress, checked((int)descriptorSize));
        var pointerDataBytes = pointerDataCount == 0
            ? []
            : ReadExact(reader, pointerDataAddress, checked((int)pointerDataCount * reader.PointerSize));
        var parsed = ParseDescriptorJson(jsonBytes, requiredFields, optionalFields);
        return new ClrmdRuntimeContractDescriptor(
            descriptorAddress,
            flags,
            descriptorSize,
            Convert.ToHexString(SHA256.HashData(jsonBytes)).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(pointerDataBytes)).ToLowerInvariant(),
            parsed.LoaderContractVersion,
            parsed.RuntimeTypeSystemContractVersion,
            reader.PointerSize,
            parsed.Layout);
    }

    private static ParsedDescriptorJson ParseDescriptorJson(
        byte[] jsonBytes,
        IReadOnlyCollection<ClrmdRuntimeContractField> requiredFields,
        IReadOnlyCollection<ClrmdRuntimeContractField> optionalFields)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonBytes);
        }
        catch (JsonException exception)
        {
            throw Invalid("The runtime contract descriptor JSON could not be parsed.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("The runtime contract descriptor JSON root is not an object.");
            }

            var descriptorVersion = ReadDescriptorVersion(root, "version");
            if (!root.TryGetProperty("contracts", out var contracts) || contracts.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("The runtime contract descriptor does not define the contracts object.");
            }

            var loaderVersion = ReadDescriptorVersion(contracts, "Loader");
            var runtimeTypeSystemVersion = ReadDescriptorVersion(contracts, "RuntimeTypeSystem");
            if (descriptorVersion != 0 || loaderVersion != 1 || runtimeTypeSystemVersion != 1)
            {
                throw Invalid(
                    $"Observed descriptor/Loader/RuntimeTypeSystem versions " +
                    $"{descriptorVersion}/{loaderVersion}/{runtimeTypeSystemVersion}.");
            }

            if (!root.TryGetProperty("types", out var typeElements) || typeElements.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("The runtime contract descriptor does not define the types object.");
            }

            var required = requiredFields.ToHashSet();
            var optional = optionalFields.Where(field => !required.Contains(field)).ToHashSet();
            var typeNames = required
                .Concat(optional)
                .Select(static field => field.TypeName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var typeBuilder = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, int>>(
                StringComparer.Ordinal);
            foreach (var typeName in typeNames)
            {
                var requiredForType = required
                    .Where(field => string.Equals(field.TypeName, typeName, StringComparison.Ordinal))
                    .ToArray();
                var optionalForType = optional
                    .Where(field => string.Equals(field.TypeName, typeName, StringComparison.Ordinal))
                    .ToArray();
                if (!typeElements.TryGetProperty(typeName, out var typeElement))
                {
                    if (requiredForType.Length != 0)
                    {
                        throw Unavailable($"The runtime contract descriptor does not define type {typeName}.");
                    }

                    continue;
                }

                if (typeElement.ValueKind != JsonValueKind.Object)
                {
                    throw Invalid($"The runtime contract descriptor type {typeName} is not an object.");
                }

                var fieldBuilder = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
                foreach (var field in requiredForType)
                {
                    if (!typeElement.TryGetProperty(field.FieldName, out var fieldElement))
                    {
                        throw Unavailable(
                            $"The runtime contract descriptor does not define {typeName}.{field.FieldName}.");
                    }

                    fieldBuilder.Add(
                        field.FieldName,
                        ReadDescriptorFieldOffset(fieldElement, typeName, field.FieldName));
                }

                foreach (var field in optionalForType)
                {
                    if (typeElement.TryGetProperty(field.FieldName, out var fieldElement))
                    {
                        fieldBuilder.Add(
                            field.FieldName,
                            ReadDescriptorFieldOffset(fieldElement, typeName, field.FieldName));
                    }
                }

                typeBuilder.Add(typeName, fieldBuilder.ToImmutable());
            }

            return new ParsedDescriptorJson(loaderVersion, runtimeTypeSystemVersion, typeBuilder.ToImmutable());
        }
    }

    private static int ReadDescriptorVersion(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
        {
            throw Invalid($"The runtime contract descriptor does not declare {propertyName}.");
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
        {
            return numeric;
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out numeric))
        {
            return numeric;
        }

        throw Invalid($"The runtime contract descriptor value {propertyName} is not an integer version.");
    }

    private static int ReadDescriptorFieldOffset(JsonElement element, string typeName, string fieldName)
    {
        var offsetElement = element;
        if (element.ValueKind == JsonValueKind.Array)
        {
            var enumerator = element.EnumerateArray();
            if (!enumerator.MoveNext())
            {
                throw Invalid(
                    $"The runtime contract descriptor field {typeName}.{fieldName} has an empty offset tuple.");
            }

            offsetElement = enumerator.Current;
        }

        if (offsetElement.ValueKind != JsonValueKind.Number ||
            !offsetElement.TryGetInt32(out var offset) ||
            offset < 0 ||
            offset > MaximumDescriptorFieldOffset)
        {
            throw Invalid(
                $"The runtime contract descriptor field {typeName}.{fieldName} has an inadmissible offset.");
        }

        return offset;
    }

    private static byte[] ReadExact(IDataReader reader, ulong address, int length)
    {
        if (address == 0 || length <= 0)
        {
            throw Unavailable("A required descriptor read names a null address or an empty range.");
        }

        if (address > ulong.MaxValue - checked((ulong)(length - 1)))
        {
            throw Invalid("A required descriptor read overflows the target address space.");
        }

        var bytes = new byte[length];
        int observed;
        try
        {
            observed = reader.Read(address, bytes);
        }
        catch (Exception exception) when (IsExpectedBackendFailure(exception))
        {
            throw Unavailable($"The descriptor read at 0x{address:x} failed.", exception);
        }

        if (observed != length)
        {
            throw Unavailable(
                $"The descriptor read at 0x{address:x} requested {length} bytes and returned {observed}.");
        }

        return bytes;
    }

    private static ulong ReadPointerValue(ReadOnlySpan<byte> bytes, int pointerSize) =>
        pointerSize == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt64LittleEndian(bytes);

    private static DescriptorReadException Unavailable(string message, Exception? innerException = null) =>
        new(
            ClrmdEvidenceStatus.Unavailable,
            ClrmdValueIssue.RuntimeContractUnavailable,
            message,
            innerException);

    private static DescriptorReadException Invalid(string message, Exception? innerException = null) =>
        new(ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, message, innerException);

    private static bool IsExpectedBackendFailure(Exception exception) =>
        exception is ClrDiagnosticsException or
            BadImageFormatException or
            InvalidDataException or
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidOperationException or
            OverflowException;

    private sealed record ParsedDescriptorJson(
        int LoaderContractVersion,
        int RuntimeTypeSystemContractVersion,
        ImmutableDictionary<string, ImmutableDictionary<string, int>> Layout);

    private sealed class DescriptorReadException : Exception
    {
        internal DescriptorReadException(
            ClrmdEvidenceStatus status,
            ClrmdValueIssue issue,
            string message,
            Exception? innerException)
            : base(message, innerException)
        {
            Status = status;
            Issue = issue;
        }

        internal ClrmdEvidenceStatus Status { get; }

        internal ClrmdValueIssue Issue { get; }
    }
}
