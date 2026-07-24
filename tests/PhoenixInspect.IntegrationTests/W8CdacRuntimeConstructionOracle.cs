using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
// Framework identifier required only to hash detached descriptor bytes; it does not define project vocabulary.
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Diagnostics.Runtime;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Reads .NET 10 runtime-construction evidence directly from the contract descriptor embedded in a full dump.
/// </summary>
/// <remarks>
/// This is an internal W8.1 experiment. It deliberately exposes no product contract and uses ClrMD only as the
/// bounded dump-memory transport and module-export reader. Runtime layout offsets always come from the descriptor.
/// </remarks>
internal sealed class W8CdacRuntimeConstructionOracle
{
    internal const string DescriptorExportName = "DotNetRuntimeContractDescriptor";
    internal const int MaximumDescriptorBytes = 4 * 1_024 * 1_024;
    internal const int MaximumPointerDataSlots = 65_536;
    internal const int MaximumRuntimeModules = 1_024;
    internal const int MaximumHashBuckets = 65_536;
    internal const int MaximumHashEntries = 65_536;
    internal const int MaximumDictionaryCount = 128;
    internal const int MaximumTypeArgumentCount = 128;

    private const ulong DescriptorMagic = 0x0043414443434E44UL;
    private const uint DescriptorPointerSizeFlag = 0x2;
    private const uint MethodTableGenericsMask = 0x30;
    private const uint MethodTableCategoryMask = 0x000F0000;
    private const uint MethodTableArrayCategory = 0x00080000;
    private const uint MethodTableSzArrayCategory = 0x000A0000;
    private const uint MethodTableArrayMask = 0x000C0000;
    private const int TypeDefRidShift = 8;
    private const int HashBucketPrefixSlots = 3;
    private const ulong HashEndSentinelMask = 0x1;
    private const ulong TypeHandleDescriptorMask = 0x2;
    private const byte ElementTypePointer = 0x0F;
    private const byte ElementTypeByReference = 0x10;
    private const byte ElementTypeValueType = 0x11;
    private const int MaximumFieldOffset = 1 * 1_024 * 1_024;

    private readonly W8CdacExactMemoryReader memory;
    private readonly W8CdacLayout layout;

    private W8CdacRuntimeConstructionOracle(
        W8CdacExactMemoryReader memory,
        W8CdacLayout layout,
        W8CdacDescriptorEvidence descriptor)
    {
        this.memory = memory;
        this.layout = layout;
        Descriptor = descriptor;
    }

    internal W8CdacDescriptorEvidence Descriptor { get; }

    internal W8CdacReadAccounting ReadAccounting => memory.Accounting;

    internal static W8CdacRuntimeConstructionOracle Open(DataTarget dataTarget, ClrInfo clrInfo)
    {
        ArgumentNullException.ThrowIfNull(dataTarget);
        ArgumentNullException.ThrowIfNull(clrInfo);

        var modules = dataTarget.DataReader
            .EnumerateModules()
            .Take(MaximumRuntimeModules + 1)
            .ToArray();
        if (modules.Length > MaximumRuntimeModules)
        {
            throw W8CdacObservationException.BoundExceeded(
                "W8_CDAC_RUNTIME_MODULE_CAP",
                MaximumRuntimeModules,
                modules.Length);
        }

        var runtimeModules = modules
            .Where(candidate => candidate.ImageBase == clrInfo.ModuleInfo.ImageBase)
            .ToArray();
        if (runtimeModules.Length != 1)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_RUNTIME_MODULE_CARDINALITY",
                $"Expected one runtime module at 0x{clrInfo.ModuleInfo.ImageBase:x}, observed {runtimeModules.Length}.");
        }

        var descriptorAddress = runtimeModules[0].GetExportSymbolAddress(DescriptorExportName);
        if (descriptorAddress == 0)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_DESCRIPTOR_EXPORT_ABSENT",
                $"The runtime module does not export {DescriptorExportName}.");
        }

        if (clrInfo.ContractDescriptorAddress != 0 && clrInfo.ContractDescriptorAddress != descriptorAddress)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_DESCRIPTOR_EXPORT_CONFLICT",
                "The module export and ClrInfo descriptor addresses disagree.");
        }

        var memory = new W8CdacExactMemoryReader(dataTarget.DataReader);
        var parsed = ReadDescriptor(memory, descriptorAddress, modules.Length);
        return new W8CdacRuntimeConstructionOracle(memory, parsed.Layout, parsed.Evidence);
    }

    internal W8CdacAvailableTypes ReadAvailableTypes(ulong moduleAddress)
    {
        if (moduleAddress == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(moduleAddress));
        }

        var availableTypeParams = memory.ReadPointer(Add(
            moduleAddress,
            layout.Field("Module", "AvailableTypeParams")));
        if (availableTypeParams == 0)
        {
            return new W8CdacAvailableTypes(
                moduleAddress,
                availableTypeParams,
                0,
                0,
                ImmutableArray<W8CdacAvailableTypeEntry>.Empty);
        }

        var buckets = memory.ReadPointer(Add(
            availableTypeParams,
            layout.Field("EETypeHashTable", "Buckets")));
        var declaredCount = memory.ReadUInt32(Add(
            availableTypeParams,
            layout.Field("EETypeHashTable", "Count")));
        if (buckets == 0)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_HASH_BUCKETS_NULL",
                "The non-null available-type table has a null bucket vector.");
        }

        var bucketCountValue = memory.ReadPointer(buckets);
        if (bucketCountValue > MaximumHashBuckets)
        {
            throw W8CdacObservationException.BoundExceeded(
                "W8_CDAC_HASH_BUCKET_CAP",
                MaximumHashBuckets,
                checked((long)Math.Min(bucketCountValue, int.MaxValue)));
        }

        var bucketCount = checked((int)bucketCountValue);
        var entries = ImmutableArray.CreateBuilder<W8CdacAvailableTypeEntry>();
        var visitedNodes = new HashSet<ulong>();
        for (var bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            var bucketSlotIndex = checked(bucketIndex + HashBucketPrefixSlots);
            var chain = memory.ReadPointer(Add(
                buckets,
                checked((ulong)bucketSlotIndex * (ulong)memory.PointerSize)));

            while (!IsHashEndSentinel(chain))
            {
                if (chain == 0)
                {
                    throw new W8CdacObservationException(
                        "W8_CDAC_HASH_CHAIN_NULL",
                        $"Bucket {bucketIndex} reached a null chain node instead of the end sentinel.");
                }

                if (!visitedNodes.Add(chain))
                {
                    throw new W8CdacObservationException(
                        "W8_CDAC_HASH_CHAIN_CYCLE",
                        $"The available-type table revisited chain node 0x{chain:x}.");
                }

                if (entries.Count == MaximumHashEntries)
                {
                    throw W8CdacObservationException.BoundExceeded(
                        "W8_CDAC_HASH_ENTRY_CAP",
                        MaximumHashEntries,
                        MaximumHashEntries + 1L);
                }

                var valueSlot = Add(
                    chain,
                    layout.Field("EETypeHashTable", "VolatileEntryValue"));
                var taggedTypeHandle = memory.ReadPointer(valueSlot);
                var flags = checked((uint)(taggedTypeHandle & HashEndSentinelMask));
                var typeHandle = taggedTypeHandle & ~HashEndSentinelMask;
                if (typeHandle == 0)
                {
                    throw new W8CdacObservationException(
                        "W8_CDAC_TYPE_HANDLE_NULL",
                        $"Chain node 0x{chain:x} contains a null type handle.");
                }

                entries.Add(new W8CdacAvailableTypeEntry(typeHandle, flags, chain, bucketIndex));
                chain = memory.ReadPointer(Add(
                    chain,
                    layout.Field("EETypeHashTable", "VolatileEntryNextEntry")));
            }
        }

        if (entries.Count != declaredCount)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_HASH_COUNT_CONFLICT",
                $"The available-type table declares {declaredCount} entries but traversal observed {entries.Count}.");
        }

        return new W8CdacAvailableTypes(
            moduleAddress,
            availableTypeParams,
            bucketCount,
            declaredCount,
            entries.ToImmutable());
    }

    internal bool TryReadMethodTableIdentity(
        ulong typeHandle,
        out W8CdacMethodTableIdentity? identity)
    {
        identity = null;
        if (typeHandle == 0 || (typeHandle & TypeHandleDescriptorMask) != 0)
        {
            return false;
        }

        var alignmentMask = checked((ulong)memory.PointerSize - 1UL);
        if ((typeHandle & alignmentMask) != 0)
        {
            return false;
        }

        var mtFlags = memory.ReadUInt32(Add(typeHandle, layout.Field("MethodTable", "MTFlags")));
        var mtFlags2 = memory.ReadUInt32(Add(typeHandle, layout.Field("MethodTable", "MTFlags2")));
        var module = memory.ReadPointer(Add(typeHandle, layout.Field("MethodTable", "Module")));
        var typeDefRid = mtFlags2 >> TypeDefRidShift;
        var typeDefToken = typeDefRid == 0 ? 0 : checked((int)(0x02000000U | typeDefRid));

        if ((mtFlags & MethodTableArrayMask) == MethodTableArrayCategory)
        {
            identity = new W8CdacMethodTableIdentity(
                typeHandle,
                module,
                typeDefToken,
                mtFlags,
                mtFlags2,
                ImmutableArray<ulong>.Empty,
                memory.ReadPointer(Add(typeHandle, layout.Field("MethodTable", "PerInstInfo"))),
                0,
                0);
            return true;
        }

        if ((mtFlags & MethodTableGenericsMask) == 0)
        {
            identity = new W8CdacMethodTableIdentity(
                typeHandle,
                module,
                typeDefToken,
                mtFlags,
                mtFlags2,
                ImmutableArray<ulong>.Empty,
                0,
                0,
                0);
            return true;
        }

        var perInstInfo = memory.ReadPointer(Add(
            typeHandle,
            layout.Field("MethodTable", "PerInstInfo")));
        if (perInstInfo < (ulong)memory.PointerSize)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_PER_INST_INFO_ABSENT",
                $"Generic method table 0x{typeHandle:x} has no readable PerInstInfo vector.");
        }

        var dictionaryInfoAddress = perInstInfo - (ulong)memory.PointerSize;
        var dictionaryCount = memory.ReadUInt16(Add(
            dictionaryInfoAddress,
            layout.Field("GenericsDictInfo", "NumDicts")));
        var typeArgumentCount = memory.ReadUInt16(Add(
            dictionaryInfoAddress,
            layout.Field("GenericsDictInfo", "NumTypeArgs")));
        if (dictionaryCount == 0 || dictionaryCount > MaximumDictionaryCount)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_DICTIONARY_COUNT",
                $"Method table 0x{typeHandle:x} reports dictionary count {dictionaryCount}.");
        }

        if (typeArgumentCount == 0 || typeArgumentCount > MaximumTypeArgumentCount)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_TYPE_ARGUMENT_COUNT",
                $"Method table 0x{typeHandle:x} reports type-argument count {typeArgumentCount}.");
        }

        var finalDictionarySlot = Add(
            perInstInfo,
            checked((ulong)(dictionaryCount - 1) * (ulong)memory.PointerSize));
        var finalDictionary = memory.ReadPointer(finalDictionarySlot);
        if (finalDictionary == 0)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_FINAL_DICTIONARY_NULL",
                $"Method table 0x{typeHandle:x} has a null final dictionary.");
        }

        var arguments = ImmutableArray.CreateBuilder<ulong>(typeArgumentCount);
        for (var index = 0; index < typeArgumentCount; index++)
        {
            var argument = memory.ReadPointer(Add(
                finalDictionary,
                checked((ulong)index * (ulong)memory.PointerSize)));
            if (argument == 0)
            {
                throw new W8CdacObservationException(
                    "W8_CDAC_TYPE_ARGUMENT_NULL",
                    $"Method table 0x{typeHandle:x} has a null argument at index {index}.");
            }

            arguments.Add(argument);
        }

        identity = new W8CdacMethodTableIdentity(
            typeHandle,
            module,
            typeDefToken,
            mtFlags,
            mtFlags2,
            arguments.ToImmutable(),
            perInstInfo,
            dictionaryCount,
            finalDictionary);
        return true;
    }

    internal W8CdacMethodTableIdentity ReadMethodTableIdentity(ulong typeHandle)
    {
        if (!TryReadMethodTableIdentity(typeHandle, out var identity))
        {
            throw new W8CdacObservationException(
                "W8_CDAC_NOT_METHOD_TABLE",
                $"Type handle 0x{typeHandle:x} is not a method-table handle.");
        }

        return identity!;
    }

    internal W8CdacTypeShape ReadTypeShape(ulong typeHandle)
    {
        if (typeHandle == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(typeHandle));
        }

        var tagBits = typeHandle & checked((ulong)memory.PointerSize - 1);
        if (tagBits is not 0 and not TypeHandleDescriptorMask)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_TYPE_HANDLE_TAG",
                $"Type handle 0x{typeHandle:x} has unsupported tag bits 0x{tagBits:x}.");
        }

        if ((typeHandle & TypeHandleDescriptorMask) != 0)
        {
            var descriptorAddress = typeHandle & ~TypeHandleDescriptorMask;
            var typeAndFlags = memory.ReadUInt32(Add(
                descriptorAddress,
                layout.Field("TypeDesc", "TypeAndFlags")));
            var elementType = checked((byte)(typeAndFlags & byte.MaxValue));
            var hasTypeArgument = elementType is
                ElementTypePointer or ElementTypeByReference or ElementTypeValueType;
            var typeArgument = hasTypeArgument
                ? memory.ReadPointer(Add(
                    descriptorAddress,
                    layout.Field("ParamTypeDesc", "TypeArg")))
                : 0;
            return new W8CdacTypeShape(
                typeHandle,
                W8CdacTypeShapeKind.TypeDescriptor,
                0,
                0,
                ImmutableArray<ulong>.Empty,
                false,
                false,
                0,
                typeArgument,
                elementType);
        }

        var identity = ReadMethodTableIdentity(typeHandle);
        var category = identity.MethodTableFlags & MethodTableCategoryMask;
        if ((identity.MethodTableFlags & MethodTableArrayMask) != MethodTableArrayCategory)
        {
            return new W8CdacTypeShape(
                typeHandle,
                W8CdacTypeShapeKind.MethodTable,
                identity.ModuleAddress,
                identity.TypeDefToken,
                identity.TypeArgumentHandles,
                false,
                false,
                0,
                0,
                0);
        }

        var isSzArray = category == MethodTableSzArrayCategory;
        var rank = isSzArray ? 1 : ReadMultiDimensionalArrayRank(typeHandle);
        if (identity.PerInstInfoAddress == 0)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_ARRAY_ELEMENT_NULL",
                $"Array method table 0x{typeHandle:x} has a null element handle.");
        }

        return new W8CdacTypeShape(
            typeHandle,
            W8CdacTypeShapeKind.MethodTable,
            identity.ModuleAddress,
            identity.TypeDefToken,
            ImmutableArray<ulong>.Empty,
            true,
            isSzArray,
            rank,
            identity.PerInstInfoAddress,
            0);
    }

    private int ReadMultiDimensionalArrayRank(ulong methodTable)
    {
        var classOrCanonical = memory.ReadPointer(Add(
            methodTable,
            layout.Field("MethodTable", "EEClassOrCanonMT")));
        if (classOrCanonical == 0)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_ARRAY_CLASS_NULL",
                $"Array method table 0x{methodTable:x} has no class evidence.");
        }

        ulong classAddress;
        if ((classOrCanonical & 0x1) == 0)
        {
            classAddress = classOrCanonical;
        }
        else
        {
            var canonicalMethodTable = classOrCanonical & ~0x1UL;
            classAddress = memory.ReadPointer(Add(
                canonicalMethodTable,
                layout.Field("MethodTable", "EEClassOrCanonMT")));
            if (classAddress == 0 || (classAddress & 0x1) != 0)
            {
                throw new W8CdacObservationException(
                    "W8_CDAC_ARRAY_CANONICAL_CLASS",
                    $"Canonical array method table 0x{canonicalMethodTable:x} has no direct class evidence.");
            }
        }

        var rank = memory.ReadByte(Add(classAddress, layout.Field("ArrayClass", "Rank")));
        if (rank == 0 || rank > 32)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_ARRAY_RANK",
                $"Array method table 0x{methodTable:x} reports rank {rank}.");
        }

        return rank;
    }

    private static W8CdacParsedDescriptor ReadDescriptor(
        W8CdacExactMemoryReader memory,
        ulong descriptorAddress,
        int enumeratedModuleCount)
    {
        var pointerDataOffset = checked(24 + memory.PointerSize);
        var headerSize = checked(pointerDataOffset + memory.PointerSize);
        var header = memory.ReadExact(descriptorAddress, headerSize);

        var littleEndianMagic = BinaryPrimitives.ReadUInt64LittleEndian(header);
        var bigEndianMagic = BinaryPrimitives.ReadUInt64BigEndian(header);
        var endianness = littleEndianMagic == DescriptorMagic
            ? W8CdacEndianness.Little
            : bigEndianMagic == DescriptorMagic
                ? W8CdacEndianness.Big
                : throw new W8CdacObservationException(
                    "W8_CDAC_DESCRIPTOR_MAGIC",
                    $"Descriptor address 0x{descriptorAddress:x} does not contain the expected magic.");

        var flags = ReadUInt32(header.AsSpan(8, sizeof(uint)), endianness);
        var descriptorPointerSize = (flags & DescriptorPointerSizeFlag) == 0
            ? sizeof(ulong)
            : sizeof(uint);
        if (descriptorPointerSize != memory.PointerSize)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_DESCRIPTOR_POINTER_SIZE",
                $"The descriptor reports {descriptorPointerSize}-byte pointers; the reader reports {memory.PointerSize}.");
        }

        memory.Endianness = endianness;

        var descriptorSize = ReadUInt32(header.AsSpan(12, sizeof(uint)), endianness);
        if (descriptorSize == 0 || descriptorSize > MaximumDescriptorBytes)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_DESCRIPTOR_SIZE",
                $"The descriptor byte count {descriptorSize} is outside the admitted range.");
        }

        var jsonAddress = ReadPointer(header.AsSpan(16, memory.PointerSize), memory.PointerSize, endianness);
        var pointerDataCountOffset = checked(16 + memory.PointerSize);
        var pointerDataCount = ReadUInt32(
            header.AsSpan(pointerDataCountOffset, sizeof(uint)),
            endianness);
        if (pointerDataCount > MaximumPointerDataSlots)
        {
            throw W8CdacObservationException.BoundExceeded(
                "W8_CDAC_POINTER_DATA_CAP",
                MaximumPointerDataSlots,
                pointerDataCount);
        }

        var pointerDataAddress = ReadPointer(
            header.AsSpan(pointerDataOffset, memory.PointerSize),
            memory.PointerSize,
            endianness);
        if (jsonAddress == 0 || (pointerDataCount != 0 && pointerDataAddress == 0))
        {
            throw new W8CdacObservationException(
                "W8_CDAC_DESCRIPTOR_POINTER",
                "The descriptor contains a null address for required data.");
        }

        var jsonBytes = memory.ReadExact(jsonAddress, checked((int)descriptorSize));
        var pointerDataBytes = pointerDataCount == 0
            ? Array.Empty<byte>()
            : memory.ReadExact(
                pointerDataAddress,
                checked((int)pointerDataCount * memory.PointerSize));

        W8CdacLayout layout;
        try
        {
            layout = W8CdacLayout.Parse(jsonBytes);
        }
        catch (JsonException exception)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_DESCRIPTOR_JSON",
                "The runtime descriptor JSON could not be parsed.",
                exception);
        }

        if (layout.DescriptorVersion != 0 || layout.LoaderContractVersion != 1 ||
            layout.RuntimeTypeSystemContractVersion != 1)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_CONTRACT_VERSION",
                $"Observed descriptor/Loader/RuntimeTypeSystem versions " +
                $"{layout.DescriptorVersion}/{layout.LoaderContractVersion}/{layout.RuntimeTypeSystemContractVersion}.");
        }

        var evidence = new W8CdacDescriptorEvidence(
            descriptorAddress,
            flags,
            memory.PointerSize,
            endianness,
            descriptorSize,
            jsonAddress,
            pointerDataCount,
            pointerDataAddress,
            Convert.ToHexString(SHA256.HashData(jsonBytes)).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(pointerDataBytes)).ToLowerInvariant(),
            layout.DescriptorVersion,
            layout.LoaderContractVersion,
            layout.RuntimeTypeSystemContractVersion,
            enumeratedModuleCount);
        return new W8CdacParsedDescriptor(layout, evidence);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, W8CdacEndianness endianness) =>
        endianness == W8CdacEndianness.Little
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);

    private static ulong ReadPointer(
        ReadOnlySpan<byte> bytes,
        int pointerSize,
        W8CdacEndianness endianness) => pointerSize switch
        {
            sizeof(uint) => ReadUInt32(bytes, endianness),
            sizeof(ulong) when endianness == W8CdacEndianness.Little =>
                BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            sizeof(ulong) => BinaryPrimitives.ReadUInt64BigEndian(bytes),
            _ => throw new W8CdacObservationException(
                "W8_CDAC_READER_POINTER_SIZE",
                $"Pointer size {pointerSize} is not admitted."),
        };

    private static bool IsHashEndSentinel(ulong value) =>
        (value & HashEndSentinelMask) == HashEndSentinelMask;

    private static ulong Add(ulong address, int offset) =>
        checked(address + checked((ulong)offset));

    private static ulong Add(ulong address, ulong offset) => checked(address + offset);

    private sealed record W8CdacParsedDescriptor(
        W8CdacLayout Layout,
        W8CdacDescriptorEvidence Evidence);

    private sealed class W8CdacLayout
    {
        private static readonly (string Type, string Field)[] RequiredFields =
        [
            ("Module", "AvailableTypeParams"),
            ("EETypeHashTable", "Buckets"),
            ("EETypeHashTable", "Count"),
            ("EETypeHashTable", "VolatileEntryValue"),
            ("EETypeHashTable", "VolatileEntryNextEntry"),
            ("MethodTable", "MTFlags"),
            ("MethodTable", "MTFlags2"),
            ("MethodTable", "Module"),
            ("MethodTable", "PerInstInfo"),
            ("MethodTable", "EEClassOrCanonMT"),
            ("GenericsDictInfo", "NumDicts"),
            ("GenericsDictInfo", "NumTypeArgs"),
            ("ArrayClass", "Rank"),
            ("TypeDesc", "TypeAndFlags"),
            ("ParamTypeDesc", "TypeArg"),
        ];

        private readonly ImmutableDictionary<string, ImmutableDictionary<string, int>> types;

        private W8CdacLayout(
            int descriptorVersion,
            int loaderContractVersion,
            int runtimeTypeSystemContractVersion,
            ImmutableDictionary<string, ImmutableDictionary<string, int>> types)
        {
            DescriptorVersion = descriptorVersion;
            LoaderContractVersion = loaderContractVersion;
            RuntimeTypeSystemContractVersion = runtimeTypeSystemContractVersion;
            this.types = types;
        }

        internal int DescriptorVersion { get; }

        internal int LoaderContractVersion { get; }

        internal int RuntimeTypeSystemContractVersion { get; }

        internal int Field(string typeName, string fieldName)
        {
            if (!types.TryGetValue(typeName, out var fields) ||
                !fields.TryGetValue(fieldName, out var offset))
            {
                throw new W8CdacObservationException(
                    "W8_CDAC_DESCRIPTOR_FIELD_ABSENT",
                    $"The descriptor does not define {typeName}.{fieldName}.");
            }

            return offset;
        }

        internal static W8CdacLayout Parse(byte[] jsonBytes)
        {
            using var document = JsonDocument.Parse(jsonBytes);
            var root = document.RootElement;
            var descriptorVersion = ReadVersion(root.GetProperty("version"), "version");
            var contracts = root.GetProperty("contracts");
            var loaderVersion = ReadVersion(contracts.GetProperty("Loader"), "contracts.Loader");
            var runtimeTypeSystemVersion = ReadVersion(
                contracts.GetProperty("RuntimeTypeSystem"),
                "contracts.RuntimeTypeSystem");
            var typeElements = root.GetProperty("types");

            var typeBuilder = ImmutableDictionary.CreateBuilder<
                string,
                ImmutableDictionary<string, int>>(StringComparer.Ordinal);
            foreach (var requiredType in RequiredFields.Select(static field => field.Type).Distinct())
            {
                if (!typeElements.TryGetProperty(requiredType, out var typeElement) ||
                    typeElement.ValueKind != JsonValueKind.Object)
                {
                    throw new W8CdacObservationException(
                        "W8_CDAC_DESCRIPTOR_TYPE_ABSENT",
                        $"The descriptor does not define type {requiredType}.");
                }

                var fields = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
                foreach (var requiredField in RequiredFields.Where(field => field.Type == requiredType))
                {
                    if (!typeElement.TryGetProperty(requiredField.Field, out var fieldElement))
                    {
                        throw new W8CdacObservationException(
                            "W8_CDAC_DESCRIPTOR_FIELD_ABSENT",
                            $"The descriptor does not define {requiredType}.{requiredField.Field}.");
                    }

                    fields.Add(requiredField.Field, ReadFieldOffset(fieldElement, requiredType, requiredField.Field));
                }

                typeBuilder.Add(requiredType, fields.ToImmutable());
            }

            return new W8CdacLayout(
                descriptorVersion,
                loaderVersion,
                runtimeTypeSystemVersion,
                typeBuilder.ToImmutable());
        }

        private static int ReadVersion(JsonElement element, string evidenceName)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (element.ValueKind == JsonValueKind.String &&
                int.TryParse(
                    element.GetString(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out numeric))
            {
                return numeric;
            }

            throw new W8CdacObservationException(
                "W8_CDAC_DESCRIPTOR_VERSION_VALUE",
                $"Descriptor value {evidenceName} is not an integer version.");
        }

        private static int ReadFieldOffset(
            JsonElement element,
            string typeName,
            string fieldName)
        {
            var offsetElement = element.ValueKind == JsonValueKind.Array
                ? ReadFirstArrayElement(element, typeName, fieldName)
                : element;
            if (offsetElement.ValueKind != JsonValueKind.Number ||
                !offsetElement.TryGetInt32(out var offset) ||
                offset < 0 || offset > MaximumFieldOffset)
            {
                throw new W8CdacObservationException(
                    "W8_CDAC_DESCRIPTOR_FIELD_OFFSET",
                    $"Descriptor field {typeName}.{fieldName} has an inadmissible offset.");
            }

            return offset;
        }

        private static JsonElement ReadFirstArrayElement(
            JsonElement element,
            string typeName,
            string fieldName)
        {
            var enumerator = element.EnumerateArray();
            if (!enumerator.MoveNext())
            {
                throw new W8CdacObservationException(
                    "W8_CDAC_DESCRIPTOR_FIELD_OFFSET",
                    $"Descriptor field {typeName}.{fieldName} has an empty offset tuple.");
            }

            return enumerator.Current;
        }
    }
}

internal sealed class W8CdacExactMemoryReader
{
    private readonly IMemoryReader reader;
    private long readCount;
    private long byteCount;

    internal W8CdacExactMemoryReader(IMemoryReader reader)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        if (reader.PointerSize is not (sizeof(uint) or sizeof(ulong)))
        {
            throw new W8CdacObservationException(
                "W8_CDAC_READER_POINTER_SIZE",
                $"Pointer size {reader.PointerSize} is not admitted.");
        }
    }

    internal int PointerSize => reader.PointerSize;

    internal W8CdacEndianness Endianness { get; set; } = W8CdacEndianness.Little;

    internal W8CdacReadAccounting Accounting => new(readCount, byteCount);

    internal byte[] ReadExact(ulong address, int length)
    {
        if (address == 0)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_READ_NULL",
                "A required dump-memory read has address zero.");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var bytes = new byte[length];
        int observed;
        try
        {
            observed = reader.Read(address, bytes);
        }
        catch (Exception exception) when (exception is not W8CdacObservationException)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_READ_FAILED",
                $"The dump-memory read at 0x{address:x} failed.",
                exception);
        }

        readCount = checked(readCount + 1);
        byteCount = checked(byteCount + Math.Max(observed, 0));
        if (observed != length)
        {
            throw new W8CdacObservationException(
                "W8_CDAC_SHORT_READ",
                $"The dump-memory read at 0x{address:x} requested {length} bytes and returned {observed}.");
        }

        return bytes;
    }

    internal byte ReadByte(ulong address) => ReadExact(address, sizeof(byte))[0];

    internal ushort ReadUInt16(ulong address)
    {
        var bytes = ReadExact(address, sizeof(ushort));
        return Endianness == W8CdacEndianness.Little
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    internal uint ReadUInt32(ulong address)
    {
        var bytes = ReadExact(address, sizeof(uint));
        return Endianness == W8CdacEndianness.Little
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    internal ulong ReadPointer(ulong address) => PointerSize switch
    {
        sizeof(uint) when Endianness == W8CdacEndianness.Little =>
            BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(address, sizeof(uint))),
        sizeof(uint) => BinaryPrimitives.ReadUInt32BigEndian(ReadExact(address, sizeof(uint))),
        sizeof(ulong) when Endianness == W8CdacEndianness.Little =>
            BinaryPrimitives.ReadUInt64LittleEndian(ReadExact(address, sizeof(ulong))),
        sizeof(ulong) => BinaryPrimitives.ReadUInt64BigEndian(ReadExact(address, sizeof(ulong))),
        _ => throw new W8CdacObservationException(
            "W8_CDAC_READER_POINTER_SIZE",
            $"Pointer size {PointerSize} is not admitted."),
    };
}

internal sealed class W8CdacObservationException : InvalidOperationException
{
    internal W8CdacObservationException(string code, string message)
        : base(message) => Code = code;

    internal W8CdacObservationException(string code, string message, Exception innerException)
        : base(message, innerException) => Code = code;

    internal string Code { get; }

    internal static W8CdacObservationException BoundExceeded(
        string code,
        long maximum,
        long observed) => new(
            code,
            $"The bounded traversal admits {maximum} items and observed at least {observed}.");
}

internal enum W8CdacEndianness
{
    Little,
    Big,
}

internal sealed record W8CdacDescriptorEvidence(
    ulong DescriptorAddress,
    uint Flags,
    int PointerSize,
    W8CdacEndianness Endianness,
    uint DescriptorSize,
    ulong JsonAddress,
    uint PointerDataCount,
    ulong PointerDataAddress,
    string JsonSha256,
    string PointerDataSha256,
    int DescriptorVersion,
    int LoaderContractVersion,
    int RuntimeTypeSystemContractVersion,
    int EnumeratedModuleCount);

internal sealed record W8CdacAvailableTypes(
    ulong ModuleAddress,
    ulong TableAddress,
    int BucketCount,
    uint DeclaredCount,
    ImmutableArray<W8CdacAvailableTypeEntry> Entries);

internal sealed record W8CdacAvailableTypeEntry(
    ulong TypeHandle,
    uint Flags,
    ulong ChainNodeAddress,
    int BucketIndex);

internal sealed record W8CdacConstructionCandidate(
    ulong LoaderModuleAddress,
    ulong TableAddress,
    int BucketCount,
    uint DeclaredEntryCount,
    W8CdacAvailableTypeEntry Entry,
    W8CdacMethodTableIdentity Identity);

internal sealed record W8CdacMethodTableIdentity(
    ulong TypeHandle,
    ulong ModuleAddress,
    int TypeDefToken,
    uint MethodTableFlags,
    uint MethodTableFlags2,
    ImmutableArray<ulong> TypeArgumentHandles,
    ulong PerInstInfoAddress,
    ushort DictionaryCount,
    ulong FinalDictionaryAddress);

internal enum W8CdacTypeShapeKind
{
    MethodTable,
    TypeDescriptor,
}

internal sealed record W8CdacTypeShape(
    ulong TypeHandle,
    W8CdacTypeShapeKind Kind,
    ulong ModuleAddress,
    int TypeDefToken,
    ImmutableArray<ulong> TypeArgumentHandles,
    bool IsArray,
    bool IsSzArray,
    int ArrayRank,
    ulong ElementOrParameterTypeHandle,
    byte ElementType);

internal readonly record struct W8CdacReadAccounting(long ReadCount, long ByteCount);
