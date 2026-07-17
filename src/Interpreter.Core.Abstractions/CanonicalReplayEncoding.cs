using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Interpreter.Core.Abstractions;

internal static class CanonicalReplayEncoding
{
    private const int MetadataTokenTypeMask = unchecked((int)0xFF000000);
    private const int MetadataTokenRowIdMask = 0x00FFFFFF;

    internal static string ComputeSha256(ReadOnlySpan<byte> canonicalBytes) =>
        Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant();

    internal static string NormalizeSha256(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != SHA256.HashSizeInBytes * 2)
        {
            throw new ArgumentException("A complete 64-character SHA-256 digest is required.", parameterName);
        }

        try
        {
            _ = Convert.FromHexString(value);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "A SHA-256 digest must contain only hexadecimal characters.",
                parameterName,
                exception);
        }

        return value.ToLowerInvariant();
    }

    internal static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty
            ? ImmutableArray<T>.Empty
            : ImmutableArray.CreateRange(values.AsSpan().ToArray());

    internal static ImmutableArray<T> Copy<T>(ReadOnlySpan<T> values) =>
        values.IsEmpty
            ? ImmutableArray<T>.Empty
            : ImmutableArray.CreateRange(values.ToArray());

    internal static bool CanonicalEquals(ImmutableArray<byte> left, ImmutableArray<byte> right) =>
        left.AsSpan().SequenceEqual(right.AsSpan());

    internal static int CanonicalHashCode(ImmutableArray<byte> canonicalBytes) =>
        CanonicalHashCode(canonicalBytes.AsSpan());

    internal static int CanonicalHashCode(ReadOnlySpan<byte> canonicalBytes)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.TryHashData(canonicalBytes, digest, out _);
        return BinaryPrimitives.ReadInt32BigEndian(digest);
    }

    internal static bool IsMetadataTokenForTable(int metadataToken, byte tableIndex) =>
        (metadataToken & MetadataTokenTypeMask) == tableIndex << 24 &&
        (metadataToken & MetadataTokenRowIdMask) != 0;

    internal static int ValidateMetadataToken(
        int metadataToken,
        byte expectedTableIndex,
        string parameterName)
    {
        if (!IsMetadataTokenForTable(metadataToken, expectedTableIndex))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"A non-nil metadata token for table 0x{expectedTableIndex:X2} is required.");
        }

        return metadataToken;
    }

    internal static int MetadataTokenRowId(int metadataToken) => metadataToken & MetadataTokenRowIdMask;

    internal static void ValidatePointerWidth(int pointerWidth)
    {
        if (pointerWidth is not (sizeof(uint) or sizeof(ulong)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pointerWidth),
                "A target pointer width must be exactly four or eight bytes.");
        }
    }

    internal static ulong ValidatePointerValue(
        ulong value,
        int pointerWidth,
        bool allowZero,
        string parameterName)
    {
        ValidatePointerWidth(pointerWidth);
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((!allowZero && value == 0) || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                allowZero
                    ? "The pointer value does not fit the target pointer width."
                    : "A nonzero pointer value fitting the target pointer width is required.");
        }

        return value;
    }

    internal static void ValidateAddressRange(
        ulong address,
        int byteLength,
        int pointerWidth,
        string parameterName)
    {
        ValidatePointerWidth(pointerWidth);
        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength),
                "A target-memory range must contain at least one byte.");
        }

        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        var inclusiveOffset = (ulong)byteLength - 1;
        if (address == 0 || address > maximum || inclusiveOffset > maximum - address)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A nonzero, nonoverflowing range fitting the target pointer width is required.");
        }
    }

    /// <summary>
    /// Copies and orders an initialized deterministic-bound array while rejecting null entries and duplicate names.
    /// </summary>
    /// <param name="bounds">
    /// An explicitly initialized array. Empty is valid; the default array is rejected so a missing contract payload
    /// cannot silently acquire the meaning of an intentionally empty reached-bound set.
    /// </param>
    /// <param name="parameterName">The public caller's parameter name to retain in validation failures.</param>
    /// <returns>A defensive copy in ordinal bound-name order.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="bounds"/> is default, contains a null entry, or contains the same bound name more than once.
    /// </exception>
    internal static ImmutableArray<EvaluationDeterministicBound> NormalizeBounds(
        ImmutableArray<EvaluationDeterministicBound> bounds,
        string parameterName)
    {
        if (bounds.IsDefault)
        {
            throw new ArgumentException(
                "An explicitly initialized deterministic-bound array is required.",
                parameterName);
        }

        if (bounds.IsEmpty)
        {
            return ImmutableArray<EvaluationDeterministicBound>.Empty;
        }

        var normalized = new EvaluationDeterministicBound[bounds.Length];
        for (var index = 0; index < bounds.Length; index++)
        {
            normalized[index] = bounds[index] ?? throw new ArgumentException(
                "Deterministic bounds cannot contain null entries.",
                parameterName);
        }

        Array.Sort(
            normalized,
            static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        for (var index = 1; index < normalized.Length; index++)
        {
            if (string.Equals(normalized[index - 1].Name, normalized[index].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"The deterministic bound name '{normalized[index].Name}' occurs more than once.",
                    parameterName);
            }
        }

        return ImmutableArray.CreateRange(normalized);
    }

    internal sealed class Writer
    {
        private readonly ArrayBufferWriter<byte> buffer = new();

        internal Writer(string domain, int schemaVersion)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("A non-empty canonical domain is required.", nameof(domain));
            }

            if (schemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "A positive canonical schema version is required.");
            }

            WriteString(domain);
            WriteInt32(schemaVersion);
        }

        internal void WriteBoolean(bool value)
        {
            var span = buffer.GetSpan(1);
            span[0] = value ? (byte)1 : (byte)0;
            buffer.Advance(1);
        }

        internal void WriteInt32(int value)
        {
            var span = buffer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32BigEndian(span, value);
            buffer.Advance(sizeof(int));
        }

        internal void WriteUInt32(uint value)
        {
            var span = buffer.GetSpan(sizeof(uint));
            BinaryPrimitives.WriteUInt32BigEndian(span, value);
            buffer.Advance(sizeof(uint));
        }

        internal void WriteInt64(long value)
        {
            var span = buffer.GetSpan(sizeof(long));
            BinaryPrimitives.WriteInt64BigEndian(span, value);
            buffer.Advance(sizeof(long));
        }

        internal void WriteUInt64(ulong value)
        {
            var span = buffer.GetSpan(sizeof(ulong));
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
            buffer.Advance(sizeof(ulong));
        }

        internal void WriteRawBytes(ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty)
            {
                return;
            }

            value.CopyTo(buffer.GetSpan(value.Length));
            buffer.Advance(value.Length);
        }

        internal void WriteLengthPrefixedBytes(ReadOnlySpan<byte> value)
        {
            WriteUInt32((uint)value.Length);
            WriteRawBytes(value);
        }

        internal void WriteString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            WriteUInt32((uint)value.Length);
            foreach (var codeUnit in value)
            {
                var span = buffer.GetSpan(sizeof(char));
                BinaryPrimitives.WriteUInt16BigEndian(span, codeUnit);
                buffer.Advance(sizeof(char));
            }
        }

        internal void WriteSha256(string value, string parameterName)
        {
            var normalized = NormalizeSha256(value, parameterName);
            WriteRawBytes(Convert.FromHexString(normalized));
        }

        internal ImmutableArray<byte> ToImmutableArray() =>
            ImmutableArray.CreateRange(buffer.WrittenSpan.ToArray());
    }
}
