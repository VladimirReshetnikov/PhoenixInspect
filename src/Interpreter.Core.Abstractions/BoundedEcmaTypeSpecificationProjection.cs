using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

internal readonly record struct BoundedEcmaTypeSpecification(
    int GenericHeadMetadataToken,
    int GenericArgumentCount,
    int AggregateGenericArgumentCount,
    int MaximumObservedDepth,
    ImmutableArray<int> ReferencedTypeMetadataTokens);

/// <summary>
/// Performs bounded structural validation of one canonical ECMA-335 TypeSpec GENERICINST CLASS signature.
/// </summary>
/// <remarks>
/// The caller owns the public operation caps and checks the byte length before any copy. This shared reader covers the
/// complete encoded type grammar needed inside generic arguments, including arrays, custom modifiers, pointers,
/// function pointers, byrefs in method-signature positions, nested generic instances, VAR/MVAR, and primitive types.
/// It retains the bounded ordered TypeDef/TypeRef/TypeSpec token stream for later owner-table correlation, but
/// deliberately performs no metadata resolution.
/// </remarks>
internal static class BoundedEcmaTypeSpecificationProjection
{
    private const byte ElementTypeVoid = 0x01;
    private const byte ElementTypeBoolean = 0x02;
    private const byte ElementTypeChar = 0x03;
    private const byte ElementTypeI1 = 0x04;
    private const byte ElementTypeU1 = 0x05;
    private const byte ElementTypeI2 = 0x06;
    private const byte ElementTypeU2 = 0x07;
    private const byte ElementTypeI4 = 0x08;
    private const byte ElementTypeU4 = 0x09;
    private const byte ElementTypeI8 = 0x0A;
    private const byte ElementTypeU8 = 0x0B;
    private const byte ElementTypeR4 = 0x0C;
    private const byte ElementTypeR8 = 0x0D;
    private const byte ElementTypeString = 0x0E;
    private const byte ElementTypePointer = 0x0F;
    private const byte ElementTypeByReference = 0x10;
    private const byte ElementTypeValueType = 0x11;
    private const byte ElementTypeClass = 0x12;
    private const byte ElementTypeVar = 0x13;
    private const byte ElementTypeArray = 0x14;
    private const byte ElementTypeGenericInstance = 0x15;
    private const byte ElementTypeTypedByReference = 0x16;
    private const byte ElementTypeI = 0x18;
    private const byte ElementTypeU = 0x19;
    private const byte ElementTypeFunctionPointer = 0x1B;
    private const byte ElementTypeObject = 0x1C;
    private const byte ElementTypeSzArray = 0x1D;
    private const byte ElementTypeMVar = 0x1E;
    private const byte ElementTypeRequiredModifier = 0x1F;
    private const byte ElementTypeOptionalModifier = 0x20;
    private const byte ElementTypeSentinel = 0x41;
    private const byte ElementTypePinned = 0x45;

    internal static bool TryDecodeGenericClass(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateGenericArgumentCount,
        out BoundedEcmaTypeSpecification projection)
    {
        projection = default;
        if (signature.IsEmpty || signature.Length > maximumSignatureLength ||
            maximumSignatureLength <= 0 || maximumDepth <= 0 || maximumAggregateGenericArgumentCount <= 0)
        {
            return false;
        }

        var reader = new Reader(signature, maximumDepth, maximumAggregateGenericArgumentCount);
        if (!reader.TryReadByte(out var genericInstance) || genericInstance != ElementTypeGenericInstance ||
            !reader.TryReadByte(out var classKind) || classKind != ElementTypeClass ||
            !reader.TryReadTypeDefOrRefToken(out var genericHeadToken) ||
            !reader.TryReadCompressedUInt32(out var argumentCount) || argumentCount == 0 ||
            argumentCount > (uint)maximumAggregateGenericArgumentCount)
        {
            return false;
        }

        reader.AggregateGenericArgumentCount = checked((int)argumentCount);
        for (var index = 0; index < argumentCount; index++)
        {
            if (!reader.TryReadType(depth: 1, allowVoid: false, allowByReference: false, allowPinned: false))
            {
                return false;
            }
        }
        if (!reader.AtEnd)
        {
            return false;
        }

        projection = new BoundedEcmaTypeSpecification(
            genericHeadToken,
            checked((int)argumentCount),
            reader.AggregateGenericArgumentCount,
            reader.MaximumObservedDepth,
            reader.ReferencedTypeMetadataTokens.ToImmutable());
        return true;
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> bytes;
        private readonly int maximumDepth;
        private readonly int maximumAggregateGenericArgumentCount;
        private readonly ImmutableArray<int>.Builder referencedTypeMetadataTokens;
        private int offset;

        internal Reader(
            ReadOnlySpan<byte> bytes,
            int maximumDepth,
            int maximumAggregateGenericArgumentCount)
        {
            this.bytes = bytes;
            this.maximumDepth = maximumDepth;
            this.maximumAggregateGenericArgumentCount = maximumAggregateGenericArgumentCount;
            referencedTypeMetadataTokens = ImmutableArray.CreateBuilder<int>();
            offset = 0;
            AggregateGenericArgumentCount = 0;
            MaximumObservedDepth = 0;
        }

        internal bool AtEnd => offset == bytes.Length;

        internal int AggregateGenericArgumentCount { get; set; }

        internal int MaximumObservedDepth { get; private set; }

        internal ImmutableArray<int>.Builder ReferencedTypeMetadataTokens => referencedTypeMetadataTokens;

        internal bool TryReadType(int depth, bool allowVoid, bool allowByReference, bool allowPinned)
        {
            if (depth > maximumDepth)
            {
                return false;
            }
            MaximumObservedDepth = Math.Max(MaximumObservedDepth, depth);

            if (!TryReadCustomModifiers() || !TryReadByte(out var elementType))
            {
                return false;
            }
            if (elementType == ElementTypePinned)
            {
                if (!allowPinned || !TryReadCustomModifiers() || !TryReadByte(out elementType))
                {
                    return false;
                }
            }
            if (elementType == ElementTypeByReference)
            {
                return allowByReference &&
                    TryReadType(depth + 1, allowVoid: false, allowByReference: false, allowPinned: false);
            }

            switch (elementType)
            {
                case ElementTypeVoid:
                    return allowVoid;
                case ElementTypeBoolean:
                case ElementTypeChar:
                case ElementTypeI1:
                case ElementTypeU1:
                case ElementTypeI2:
                case ElementTypeU2:
                case ElementTypeI4:
                case ElementTypeU4:
                case ElementTypeI8:
                case ElementTypeU8:
                case ElementTypeR4:
                case ElementTypeR8:
                case ElementTypeString:
                case ElementTypeI:
                case ElementTypeU:
                case ElementTypeObject:
                    return true;
                case ElementTypeTypedByReference:
                    return allowByReference;
                case ElementTypeClass:
                case ElementTypeValueType:
                    return TryReadTypeDefOrRefToken(out _);
                case ElementTypeVar:
                case ElementTypeMVar:
                    return TryReadCompressedUInt32(out _);
                case ElementTypePointer:
                    return TryReadPointerTarget(depth + 1);
                case ElementTypeSzArray:
                    return TryReadCustomModifiers() &&
                        TryReadType(depth + 1, allowVoid: false, allowByReference: false, allowPinned: false);
                case ElementTypeArray:
                    return TryReadArray(depth + 1);
                case ElementTypeGenericInstance:
                    return TryReadNestedGenericInstance(depth + 1);
                case ElementTypeFunctionPointer:
                    return TryReadMethodSignature(depth + 1);
                default:
                    return false;
            }
        }

        private bool TryReadPointerTarget(int depth)
        {
            if (depth > maximumDepth || !TryReadCustomModifiers())
            {
                return false;
            }
            var savedOffset = offset;
            if (TryReadByte(out var elementType) && elementType == ElementTypeVoid)
            {
                MaximumObservedDepth = Math.Max(MaximumObservedDepth, depth);
                return true;
            }
            offset = savedOffset;
            return TryReadType(depth, allowVoid: false, allowByReference: false, allowPinned: false);
        }

        private bool TryReadArray(int depth)
        {
            if (!TryReadType(depth, allowVoid: false, allowByReference: false, allowPinned: false) ||
                !TryReadCompressedUInt32(out var rank) || rank == 0 ||
                !TryReadCompressedUInt32(out var sizeCount) || sizeCount > rank)
            {
                return false;
            }
            for (var index = 0; index < sizeCount; index++)
            {
                if (!TryReadCompressedUInt32(out _))
                {
                    return false;
                }
            }
            if (!TryReadCompressedUInt32(out var lowerBoundCount) || lowerBoundCount > rank)
            {
                return false;
            }
            for (var index = 0; index < lowerBoundCount; index++)
            {
                if (!TryReadCompressedSignedInteger(out _))
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryReadNestedGenericInstance(int depth)
        {
            if (depth > maximumDepth ||
                !TryReadByte(out var classKind) ||
                classKind is not (ElementTypeClass or ElementTypeValueType) ||
                !TryReadTypeDefOrRefToken(out _) ||
                !TryReadCompressedUInt32(out var argumentCount) || argumentCount == 0 ||
                argumentCount > (uint)(maximumAggregateGenericArgumentCount - AggregateGenericArgumentCount))
            {
                return false;
            }
            AggregateGenericArgumentCount += checked((int)argumentCount);
            for (var index = 0; index < argumentCount; index++)
            {
                if (!TryReadType(depth, allowVoid: false, allowByReference: false, allowPinned: false))
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryReadMethodSignature(int depth)
        {
            if (depth > maximumDepth || !TryReadByte(out var callingConvention) ||
                (callingConvention & 0x80) != 0)
            {
                return false;
            }
            var convention = callingConvention & 0x0F;
            if (convention is > 0x05 and not 0x09)
            {
                return false;
            }
            var isGeneric = (callingConvention & 0x10) != 0;
            var hasThis = (callingConvention & 0x20) != 0;
            var hasExplicitThis = (callingConvention & 0x40) != 0;
            var isManagedConvention = convention is 0x00 or 0x05;
            if (hasExplicitThis && !hasThis ||
                !isManagedConvention && (isGeneric || hasThis || hasExplicitThis) ||
                convention == 0x05 && isGeneric)
            {
                return false;
            }
            if (isGeneric &&
                (!TryReadCompressedUInt32(out var genericParameterCount) || genericParameterCount == 0))
            {
                return false;
            }
            if (!TryReadCompressedUInt32(out var parameterCount) ||
                !TryReadReturnOrParameterType(depth, allowVoid: true))
            {
                return false;
            }

            var sentinelSeen = false;
            for (var parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
            {
                if (TryPeekByte(out var marker) && marker == ElementTypeSentinel)
                {
                    if (sentinelSeen || convention != 0x05)
                    {
                        return false;
                    }
                    sentinelSeen = true;
                    offset++;
                }
                if (!TryReadReturnOrParameterType(depth, allowVoid: false))
                {
                    return false;
                }
            }
            return !TryPeekByte(out var trailing) || trailing != ElementTypeSentinel;
        }

        private bool TryReadReturnOrParameterType(int depth, bool allowVoid)
        {
            if (!TryReadCustomModifiers())
            {
                return false;
            }
            var savedOffset = offset;
            if (TryReadByte(out var special))
            {
                if (special == ElementTypeTypedByReference)
                {
                    return true;
                }
                if (allowVoid && special == ElementTypeVoid)
                {
                    return true;
                }
                if (special == ElementTypeByReference)
                {
                    return TryReadType(depth, allowVoid: false, allowByReference: false, allowPinned: false);
                }
            }
            offset = savedOffset;
            return TryReadType(depth, allowVoid: false, allowByReference: false, allowPinned: false);
        }

        private bool TryReadCustomModifiers()
        {
            while (TryPeekByte(out var elementType) &&
                   elementType is ElementTypeRequiredModifier or ElementTypeOptionalModifier)
            {
                offset++;
                if (!TryReadCustomModifierTypeToken())
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryReadCustomModifierTypeToken() =>
            TryReadTypeDefOrRefToken(out var token) &&
            (token >>> 24) is 0x01 or 0x02;

        internal bool TryReadTypeDefOrRefToken(out int token)
        {
            token = 0;
            if (!TryReadCompressedUInt32(out var codedIndex))
            {
                return false;
            }
            var rowId = codedIndex >> 2;
            if (rowId is 0 or > 0x00FF_FFFF)
            {
                return false;
            }
            var table = (codedIndex & 0x03) switch
            {
                0 => 0x02,
                1 => 0x01,
                2 => 0x1B,
                _ => -1,
            };
            if (table < 0)
            {
                return false;
            }
            token = (table << 24) | checked((int)rowId);
            referencedTypeMetadataTokens.Add(token);
            return true;
        }

        internal bool TryReadCompressedUInt32(out uint value)
        {
            value = 0;
            if (!TryReadByte(out var first))
            {
                return false;
            }
            if ((first & 0x80) == 0)
            {
                value = first;
                return true;
            }
            if ((first & 0xC0) == 0x80)
            {
                if (!TryReadByte(out var second))
                {
                    return false;
                }
                value = (uint)(((first & 0x3F) << 8) | second);
                return value >= 0x80;
            }
            if ((first & 0xE0) == 0xC0 && bytes.Length - offset >= 3)
            {
                value = (uint)(((first & 0x1F) << 24) |
                               (bytes[offset] << 16) |
                               (bytes[offset + 1] << 8) |
                               bytes[offset + 2]);
                offset += 3;
                return value is >= 0x4000 and <= 0x1FFF_FFFF;
            }
            return false;
        }

        private bool TryReadCompressedSignedInteger(out int value)
        {
            value = 0;
            if (!TryReadByte(out var first))
            {
                return false;
            }

            uint encoded;
            int encodedBitCount;
            int byteCount;
            if ((first & 0x80) == 0)
            {
                encoded = first;
                encodedBitCount = 7;
                byteCount = 1;
            }
            else if ((first & 0xC0) == 0x80)
            {
                if (!TryReadByte(out var second))
                {
                    return false;
                }
                encoded = (uint)(((first & 0x3F) << 8) | second);
                encodedBitCount = 14;
                byteCount = 2;
            }
            else if ((first & 0xE0) == 0xC0 && bytes.Length - offset >= 3)
            {
                encoded = (uint)(((first & 0x1F) << 24) |
                                 (bytes[offset] << 16) |
                                 (bytes[offset + 1] << 8) |
                                 bytes[offset + 2]);
                offset += 3;
                encodedBitCount = 29;
                byteCount = 4;
            }
            else
            {
                return false;
            }

            var decoded = checked((int)(encoded >> 1));
            if ((encoded & 1) != 0)
            {
                decoded |= -1 << (encodedBitCount - 1);
            }

            // ECMA-335 requires the shortest available signed encoding. Unsigned canonicality alone
            // is insufficient because an overlong negative value has high payload bits set.
            if (byteCount == 2 && decoded is >= -64 and <= 63 ||
                byteCount == 4 && decoded is >= -8192 and <= 8191)
            {
                return false;
            }

            value = decoded;
            return true;
        }

        internal bool TryReadByte(out byte value)
        {
            if ((uint)offset >= (uint)bytes.Length)
            {
                value = 0;
                return false;
            }
            value = bytes[offset++];
            return true;
        }

        private bool TryPeekByte(out byte value)
        {
            if ((uint)offset >= (uint)bytes.Length)
            {
                value = 0;
                return false;
            }
            value = bytes[offset];
            return true;
        }
    }
}
