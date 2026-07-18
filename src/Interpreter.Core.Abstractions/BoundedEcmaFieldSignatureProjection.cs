using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

internal enum BoundedEcmaFieldSignatureKind
{
    Int32 = 1,
    PrimitiveString = 2,
    ClassType = 3,
    GenericInstanceValueTypeInt32 = 4,
    PrimitiveObject = 5,
}

internal readonly record struct BoundedEcmaFieldSignature(
    BoundedEcmaFieldSignatureKind Kind,
    int? NamedTypeMetadataToken,
    ImmutableArray<int> CustomModifierTypeMetadataTokens);

internal static class BoundedEcmaFieldSignatureProjection
{
    internal const int MaximumCustomModifierCount = 16;

    internal static bool TryDecode(
        ReadOnlySpan<byte> signature,
        out BoundedEcmaFieldSignature projection)
    {
        projection = default;
        if (signature.IsEmpty || signature[0] != 0x06)
        {
            return false;
        }

        var offset = 1;
        var customModifierTokens = ImmutableArray.CreateBuilder<int>();
        while (offset < signature.Length && signature[offset] is 0x1F or 0x20)
        {
            if (customModifierTokens.Count == MaximumCustomModifierCount)
            {
                return false;
            }
            offset++;
            if (!TryReadTypeDefOrRefToken(signature, ref offset, allowTypeSpecification: true, out var modifierToken))
            {
                return false;
            }
            customModifierTokens.Add(modifierToken);
        }
        var modifiers = customModifierTokens.ToImmutable();
        var typeOffset = offset;
        if (offset < signature.Length && signature[offset] == 0x08 && offset + 1 == signature.Length)
        {
            projection = new BoundedEcmaFieldSignature(BoundedEcmaFieldSignatureKind.Int32, null, modifiers);
            return true;
        }

        if (offset < signature.Length && signature[offset] == 0x0E && offset + 1 == signature.Length)
        {
            projection = new BoundedEcmaFieldSignature(BoundedEcmaFieldSignatureKind.PrimitiveString, null, modifiers);
            return true;
        }

        if (offset < signature.Length && signature[offset] == 0x1C && offset + 1 == signature.Length)
        {
            projection = new BoundedEcmaFieldSignature(BoundedEcmaFieldSignatureKind.PrimitiveObject, null, modifiers);
            return true;
        }

        if (offset < signature.Length && signature[offset++] == 0x12 &&
            TryReadTypeDefOrRefToken(signature, ref offset, allowTypeSpecification: false, out var classToken) &&
            offset == signature.Length)
        {
            projection = new BoundedEcmaFieldSignature(BoundedEcmaFieldSignatureKind.ClassType, classToken, modifiers);
            return true;
        }

        offset = typeOffset;
        if (offset + 2 <= signature.Length &&
            signature[offset++] == 0x15 &&
            signature[offset++] == 0x11 &&
            TryReadTypeDefOrRefToken(signature, ref offset, allowTypeSpecification: false, out var genericDefinitionToken) &&
            offset + 2 == signature.Length &&
            signature[offset++] == 0x01 &&
            signature[offset++] == 0x08)
        {
            projection = new BoundedEcmaFieldSignature(
                BoundedEcmaFieldSignatureKind.GenericInstanceValueTypeInt32,
                genericDefinitionToken,
                modifiers);
            return true;
        }

        return false;
    }

    private static bool TryReadTypeDefOrRefToken(
        ReadOnlySpan<byte> signature,
        ref int offset,
        bool allowTypeSpecification,
        out int metadataToken)
    {
        metadataToken = 0;
        if (!TryReadCompressedUInt32(signature, ref offset, out var coded) || coded == 0)
        {
            return false;
        }

        var tag = coded & 0x03;
        var rowId = coded >> 2;
        if (rowId == 0 || rowId > 0x00FF_FFFF)
        {
            return false;
        }

        var table = tag switch
        {
            0 => 0x02,
            1 => 0x01,
            2 when allowTypeSpecification => 0x1B,
            _ => 0,
        };
        if (table == 0)
        {
            return false;
        }

        metadataToken = (table << 24) | (int)rowId;
        return true;
    }

    private static bool TryReadCompressedUInt32(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        out uint value)
    {
        value = 0;
        if ((uint)offset >= (uint)bytes.Length)
        {
            return false;
        }

        var first = bytes[offset++];
        if ((first & 0x80) == 0)
        {
            value = first;
            return true;
        }

        if ((first & 0xC0) == 0x80)
        {
            if ((uint)offset >= (uint)bytes.Length)
            {
                return false;
            }

            value = (uint)(((first & 0x3F) << 8) | bytes[offset++]);
            return value >= 0x80;
        }

        if ((first & 0xE0) != 0xC0 || offset > bytes.Length - 3)
        {
            return false;
        }

        value = ((uint)(first & 0x1F) << 24) |
                ((uint)bytes[offset++] << 16) |
                ((uint)bytes[offset++] << 8) |
                bytes[offset++];
        return value is >= 0x4000 and <= 0x1FFF_FFFF;
    }
}
