using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

internal readonly record struct BoundedEcmaMethodSignature(
    byte CallingConvention,
    bool HasThis,
    bool HasExplicitThis,
    int GenericParameterCount,
    int ParameterCount,
    int AggregateTypeCount,
    int MaximumObservedDepth);

internal readonly record struct BoundedEcmaLocalSignature(
    int LocalSlotCount,
    int AggregateTypeCount,
    int MaximumObservedDepth);

/// <summary>
/// Performs bounded, byte-only structural validation of ECMA-335 MethodDef and local-variable signatures.
/// </summary>
/// <remarks>
/// This draft reader deliberately validates signature structure without resolving referenced metadata rows. It accepts
/// the runtime's documented signature augmentations: TypeSpec tokens in custom modifiers, custom modifiers at recursive
/// type positions, and the modern unmanaged function-pointer calling convention. Named CLASS/VALUETYPE nodes still
/// require TypeDef or TypeRef coded indices. Every entry point requires complete input consumption and applies explicit
/// byte-length, recursive-depth, and aggregate-type-node limits before returning a projection.
/// The local reader admits the runtime-compatible empty <c>07 00</c> signature even though the ECMA-335 sixth-edition
/// prose describes a positive local count; current System.Reflection.Metadata encoders accept zero.
/// The MethodDefSig reader has no MethodAttributes input; its caller must additionally require <c>HasThis</c> exactly
/// when the owning MethodDef is non-static.
/// </remarks>
internal static class BoundedEcmaSignatureProjection
{
    private const byte CallingConventionMask = 0x0F;
    private const byte CallingConventionDefault = 0x00;
    private const byte CallingConventionCDecl = 0x01;
    private const byte CallingConventionStandardCall = 0x02;
    private const byte CallingConventionThisCall = 0x03;
    private const byte CallingConventionFastCall = 0x04;
    private const byte CallingConventionVariableArguments = 0x05;
    private const byte CallingConventionUnmanaged = 0x09;
    private const byte CallingConventionGeneric = 0x10;
    private const byte CallingConventionHasThis = 0x20;
    private const byte CallingConventionExplicitThis = 0x40;
    private const byte CallingConventionReserved = 0x80;

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
    private const byte ElementTypePinned = 0x45;

    private const byte LocalSignatureHeader = 0x07;
    internal static bool TryDecodeMethodDefinition(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount,
        out BoundedEcmaMethodSignature projection)
    {
        projection = default;
        if (!HasValidBounds(signature, maximumSignatureLength, maximumDepth, maximumAggregateTypeCount))
        {
            return false;
        }

        var reader = new Reader(
            signature,
            maximumDepth,
            maximumAggregateTypeCount,
            maximumAggregateGenericArgumentCount: maximumAggregateTypeCount,
            retainReferencedTypeTokens: false);
        if (!reader.TryReadMethodDefinitionSignature(
                out var callingConvention,
                out var hasThis,
                out var hasExplicitThis,
                out var genericParameterCount,
                out var parameterCount) ||
            !reader.AtEnd)
        {
            return false;
        }

        projection = new BoundedEcmaMethodSignature(
            callingConvention,
            hasThis,
            hasExplicitThis,
            genericParameterCount,
            parameterCount,
            reader.AggregateTypeCount,
            reader.MaximumObservedDepth);
        return true;
    }

    internal static bool TryDecodeLocal(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount,
        out BoundedEcmaLocalSignature projection)
    {
        projection = default;
        if (!HasValidBounds(signature, maximumSignatureLength, maximumDepth, maximumAggregateTypeCount))
        {
            return false;
        }

        var reader = new Reader(
            signature,
            maximumDepth,
            maximumAggregateTypeCount,
            maximumAggregateGenericArgumentCount: maximumAggregateTypeCount,
            retainReferencedTypeTokens: false);
        if (!reader.TryReadByte(out var header) ||
            header != LocalSignatureHeader ||
            !reader.TryReadCompressedUInt32(out var slotCount) ||
            slotCount > (uint)maximumAggregateTypeCount)
        {
            return false;
        }

        for (uint slot = 0; slot < slotCount; slot++)
        {
            if (!reader.TryReadLocalVariableType(depth: 1))
            {
                return false;
            }
        }

        if (!reader.AtEnd)
        {
            return false;
        }

        projection = new BoundedEcmaLocalSignature(
            checked((int)slotCount),
            reader.AggregateTypeCount,
            reader.MaximumObservedDepth);
        return true;
    }

    internal static bool TryDecodeGenericClassTypeSpecification(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateGenericArgumentCount,
        out BoundedEcmaTypeSpecification projection)
    {
        projection = default;
        if (signature.IsEmpty ||
            signature.Length > maximumSignatureLength ||
            maximumSignatureLength <= 0 ||
            maximumDepth <= 0 ||
            maximumAggregateGenericArgumentCount <= 0)
        {
            return false;
        }

        var reader = new Reader(
            signature,
            maximumDepth,
            maximumAggregateTypeCount: signature.Length,
            maximumAggregateGenericArgumentCount,
            retainReferencedTypeTokens: true);
        if (!reader.TryReadGenericClassTypeSpecification(out var genericHeadToken, out var genericArgumentCount) ||
            !reader.AtEnd)
        {
            return false;
        }

        projection = new BoundedEcmaTypeSpecification(
            genericHeadToken,
            genericArgumentCount,
            reader.AggregateGenericArgumentCount,
            reader.MaximumObservedDepth,
            reader.ReferencedTypeMetadataTokens);
        return true;
    }

    private static bool HasValidBounds(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount) =>
        !signature.IsEmpty &&
        maximumSignatureLength > 0 &&
        signature.Length <= maximumSignatureLength &&
        maximumDepth > 0 &&
        maximumAggregateTypeCount > 0;

    private enum EncodedTypeCategory
    {
        DefinitelyNonReference,
        DefinitelyReference,
        GenericParameter,
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> bytes;
        private readonly int maximumDepth;
        private readonly int maximumAggregateTypeCount;
        private readonly int maximumAggregateGenericArgumentCount;
        private readonly ImmutableArray<int>.Builder? referencedTypeMetadataTokens;
        private int offset;

        internal Reader(
            ReadOnlySpan<byte> bytes,
            int maximumDepth,
            int maximumAggregateTypeCount,
            int maximumAggregateGenericArgumentCount,
            bool retainReferencedTypeTokens)
        {
            this.bytes = bytes;
            this.maximumDepth = maximumDepth;
            this.maximumAggregateTypeCount = maximumAggregateTypeCount;
            this.maximumAggregateGenericArgumentCount = maximumAggregateGenericArgumentCount;
            referencedTypeMetadataTokens = retainReferencedTypeTokens
                ? ImmutableArray.CreateBuilder<int>()
                : null;
            offset = 0;
            AggregateTypeCount = 0;
            AggregateGenericArgumentCount = 0;
            MaximumObservedDepth = 0;
        }

        internal bool AtEnd => offset == bytes.Length;

        internal int AggregateTypeCount { get; private set; }

        internal int AggregateGenericArgumentCount { get; private set; }

        internal int MaximumObservedDepth { get; private set; }

        internal ImmutableArray<int> ReferencedTypeMetadataTokens =>
            referencedTypeMetadataTokens?.ToImmutable() ?? ImmutableArray<int>.Empty;

        internal bool TryReadMethodDefinitionSignature(
            out byte callingConvention,
            out bool hasThis,
            out bool hasExplicitThis,
            out int genericParameterCount,
            out int parameterCount)
        {
            callingConvention = 0;
            hasThis = false;
            hasExplicitThis = false;
            genericParameterCount = 0;
            parameterCount = 0;
            if (!TryReadByte(out var header) || (header & CallingConventionReserved) != 0)
            {
                return false;
            }

            callingConvention = (byte)(header & CallingConventionMask);
            if (callingConvention is not (CallingConventionDefault or CallingConventionVariableArguments))
            {
                return false;
            }

            hasThis = (header & CallingConventionHasThis) != 0;
            hasExplicitThis = (header & CallingConventionExplicitThis) != 0;
            if (hasExplicitThis ||
                callingConvention == CallingConventionVariableArguments &&
                    (header & CallingConventionGeneric) != 0 ||
                !TryReadGenericParameterCount(header, callingConvention, out genericParameterCount) ||
                !TryReadCompressedUInt32(out var encodedParameterCount) ||
                encodedParameterCount > int.MaxValue ||
                encodedParameterCount >= (uint)maximumAggregateTypeCount)
            {
                return false;
            }

            parameterCount = checked((int)encodedParameterCount);
            if (!TryReadReturnType(depth: 1))
            {
                return false;
            }

            for (var parameter = 0; parameter < parameterCount; parameter++)
            {
                if (!TryReadParameterType(depth: 1))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryReadLocalVariableType(int depth)
        {
            if (!TryReadCustomModifiers())
            {
                return false;
            }

            var isPinned = TryPeekByte(out var marker) && marker == ElementTypePinned;
            if (isPinned)
            {
                offset++;
                if (!TryReadCustomModifiers())
                {
                    return false;
                }
            }

            if (TryPeekByte(out marker) && marker == ElementTypeTypedByReference)
            {
                if (isPinned)
                {
                    return false;
                }

                offset++;
                return TryConsumeTypeNode(depth);
            }

            var isByReference = TryPeekByte(out marker) && marker == ElementTypeByReference;
            if (isByReference)
            {
                offset++;
                if (!TryReadCustomModifiers())
                {
                    return false;
                }
            }

            return TryReadTypeCore(depth, out _);
        }

        internal bool TryReadGenericClassTypeSpecification(
            out int genericHeadToken,
            out int genericArgumentCount)
        {
            genericHeadToken = 0;
            genericArgumentCount = 0;
            if (!TryReadByte(out var genericInstance) || genericInstance != ElementTypeGenericInstance ||
                !TryReadByte(out var classKind) || classKind != ElementTypeClass ||
                !TryReadTypeDefOrRefToken(allowTypeSpecification: false, out genericHeadToken) ||
                !TryReadCompressedUInt32(out var argumentCount) ||
                argumentCount == 0 ||
                !TryAddGenericArguments(argumentCount))
            {
                return false;
            }

            genericArgumentCount = checked((int)argumentCount);
            for (uint argument = 0; argument < argumentCount; argument++)
            {
                if (!TryReadType(depth: 1, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryReadType(int depth, out EncodedTypeCategory category)
        {
            category = default;
            return TryReadCustomModifiers() && TryReadTypeCore(depth, out category);
        }

        private bool TryReadTypeCore(int depth, out EncodedTypeCategory category)
        {
            category = EncodedTypeCategory.DefinitelyNonReference;
            if (!TryConsumeTypeNode(depth) || !TryReadByte(out var elementType))
            {
                return false;
            }

            switch (elementType)
            {
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
                case ElementTypeI:
                case ElementTypeU:
                    return true;
                case ElementTypeString:
                case ElementTypeObject:
                    category = EncodedTypeCategory.DefinitelyReference;
                    return true;
                case ElementTypeClass:
                    category = EncodedTypeCategory.DefinitelyReference;
                    return TryReadTypeDefOrRefToken(allowTypeSpecification: false, out _);
                case ElementTypeValueType:
                    return TryReadTypeDefOrRefToken(allowTypeSpecification: false, out _);
                case ElementTypeVar:
                case ElementTypeMVar:
                    category = EncodedTypeCategory.GenericParameter;
                    return TryReadCompressedUInt32(out _);
                case ElementTypePointer:
                    return TryReadPointerTarget(depth + 1);
                case ElementTypeSzArray:
                    category = EncodedTypeCategory.DefinitelyReference;
                    return TryReadType(depth + 1, out _);
                case ElementTypeArray:
                    category = EncodedTypeCategory.DefinitelyReference;
                    return TryReadArray(depth + 1);
                case ElementTypeGenericInstance:
                    return TryReadGenericInstance(depth + 1, out category);
                case ElementTypeFunctionPointer:
                    return TryReadFunctionPointerSignature(depth + 1);
                default:
                    return false;
            }
        }

        private bool TryReadReturnType(int depth)
        {
            if (!TryReadCustomModifiers())
            {
                return false;
            }

            if (TryPeekByte(out var special))
            {
                if (special is ElementTypeVoid or ElementTypeTypedByReference)
                {
                    offset++;
                    return TryConsumeTypeNode(depth);
                }

                if (special == ElementTypeByReference)
                {
                    offset++;
                    return TryReadType(depth, out _);
                }
            }

            return TryReadTypeCore(depth, out _);
        }

        private bool TryReadParameterType(int depth)
        {
            if (!TryReadCustomModifiers())
            {
                return false;
            }

            if (TryPeekByte(out var special))
            {
                if (special == ElementTypeTypedByReference)
                {
                    offset++;
                    return TryConsumeTypeNode(depth);
                }

                if (special == ElementTypeByReference)
                {
                    offset++;
                    return TryReadType(depth, out _);
                }
            }

            return TryReadTypeCore(depth, out _);
        }

        private bool TryReadPointerTarget(int depth)
        {
            if (!TryReadCustomModifiers())
            {
                return false;
            }

            if (TryPeekByte(out var elementType) && elementType == ElementTypeVoid)
            {
                offset++;
                return TryConsumeTypeNode(depth);
            }

            return TryReadTypeCore(depth, out _);
        }

        private bool TryReadArray(int depth)
        {
            if (!TryReadType(depth, out _) ||
                !TryReadCompressedUInt32(out var rank) ||
                rank == 0 ||
                !TryReadCompressedUInt32(out var sizeCount) ||
                sizeCount > rank)
            {
                return false;
            }

            for (uint size = 0; size < sizeCount; size++)
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

            for (uint lowerBound = 0; lowerBound < lowerBoundCount; lowerBound++)
            {
                if (!TryReadCompressedSignedInteger(out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryReadGenericInstance(int depth, out EncodedTypeCategory category)
        {
            category = default;
            if (!TryReadByte(out var classKind) ||
                classKind is not (ElementTypeClass or ElementTypeValueType) ||
                !TryReadTypeDefOrRefToken(allowTypeSpecification: false, out _) ||
                !TryReadCompressedUInt32(out var argumentCount) ||
                argumentCount == 0 ||
                !TryAddGenericArguments(argumentCount))
            {
                return false;
            }

            category = classKind == ElementTypeClass
                ? EncodedTypeCategory.DefinitelyReference
                : EncodedTypeCategory.DefinitelyNonReference;
            for (uint argument = 0; argument < argumentCount; argument++)
            {
                if (!TryReadType(depth, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryReadFunctionPointerSignature(int depth)
        {
            if (depth > maximumDepth ||
                !TryReadByte(out var header) ||
                (header & CallingConventionReserved) != 0)
            {
                return false;
            }

            var callingConvention = (byte)(header & CallingConventionMask);
            if (callingConvention is not (
                    CallingConventionDefault or
                    CallingConventionCDecl or
                    CallingConventionStandardCall or
                    CallingConventionThisCall or
                    CallingConventionFastCall or
                    CallingConventionVariableArguments or
                    CallingConventionUnmanaged))
            {
                return false;
            }

            var hasThis = (header & CallingConventionHasThis) != 0;
            var hasExplicitThis = (header & CallingConventionExplicitThis) != 0;
            if (hasExplicitThis && !hasThis ||
                callingConvention == CallingConventionVariableArguments &&
                    (header & CallingConventionGeneric) != 0 ||
                !TryReadGenericParameterCount(header, callingConvention, out _) ||
                !TryReadCompressedUInt32(out var parameterCount) ||
                parameterCount > int.MaxValue ||
                parameterCount >= (uint)(maximumAggregateTypeCount - AggregateTypeCount) ||
                !TryReadReturnType(depth))
            {
                return false;
            }

            for (uint parameter = 0; parameter < parameterCount; parameter++)
            {
                if (!TryReadParameterType(depth))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryReadGenericParameterCount(
            byte header,
            byte callingConvention,
            out int genericParameterCount)
        {
            genericParameterCount = 0;
            if ((header & CallingConventionGeneric) == 0)
            {
                return true;
            }

            if (callingConvention is not (CallingConventionDefault or CallingConventionVariableArguments) ||
                !TryReadCompressedUInt32(out var encodedCount) ||
                encodedCount is 0 or > int.MaxValue ||
                encodedCount > (uint)maximumAggregateTypeCount)
            {
                return false;
            }

            genericParameterCount = checked((int)encodedCount);
            return true;
        }

        private bool TryReadCustomModifiers()
        {
            while (TryPeekByte(out var elementType) &&
                   elementType is ElementTypeRequiredModifier or ElementTypeOptionalModifier)
            {
                offset++;
                if (!TryReadTypeDefOrRefToken(allowTypeSpecification: true, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryAddGenericArguments(uint count)
        {
            if (count > int.MaxValue ||
                count > (uint)(maximumAggregateGenericArgumentCount - AggregateGenericArgumentCount) ||
                count > (uint)(maximumAggregateTypeCount - AggregateTypeCount))
            {
                return false;
            }

            AggregateGenericArgumentCount += checked((int)count);
            return true;
        }

        private bool TryConsumeTypeNode(int depth)
        {
            if (depth <= 0 || depth > maximumDepth || AggregateTypeCount == maximumAggregateTypeCount)
            {
                return false;
            }

            AggregateTypeCount++;
            MaximumObservedDepth = Math.Max(MaximumObservedDepth, depth);
            return true;
        }

        private bool TryReadTypeDefOrRefToken(bool allowTypeSpecification, out int token)
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
                2 when allowTypeSpecification => 0x1B,
                _ => -1,
            };
            if (table < 0)
            {
                return false;
            }

            token = (table << 24) | checked((int)rowId);
            referencedTypeMetadataTokens?.Add(token);
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
