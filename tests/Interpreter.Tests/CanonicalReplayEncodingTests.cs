using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Interpreter.Core.Abstractions;
using Xunit;

namespace Interpreter.Tests;

/// <summary>
/// Freezes the shared W7 canonical replay primitives without changing any legacy request, plan, or result encoding.
/// </summary>
public sealed class CanonicalReplayEncodingTests
{
    /// <summary>
    /// Verifies the domain/version envelope, every fixed-width integer encoding, raw and counted bytes, and exact
    /// UTF-16 code-unit preservation, including unpaired surrogates.
    /// </summary>
    [Fact]
    public void WriterUsesOneDomainSeparatedBigEndianEncoding()
    {
        var writer = new CanonicalReplayEncoding.Writer("D", schemaVersion: 3);
        writer.WriteBoolean(false);
        writer.WriteBoolean(true);
        writer.WriteInt32(0x01020304);
        writer.WriteUInt32(0x89ABCDEF);
        writer.WriteInt64(0x0102030405060708);
        writer.WriteUInt64(0xFFEEDDCCBBAA9988);
        writer.WriteRawBytes([0xAA, 0xBB]);
        writer.WriteLengthPrefixedBytes([0x10, 0x20, 0x30]);
        writer.WriteString(new string(['A', '\uD800', 'B', '\uDC01']));

        Assert.Equal(
            new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x00, 0x44,
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x01,
                0x01, 0x02, 0x03, 0x04,
                0x89, 0xAB, 0xCD, 0xEF,
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88,
                0xAA, 0xBB,
                0x00, 0x00, 0x00, 0x03, 0x10, 0x20, 0x30,
                0x00, 0x00, 0x00, 0x04, 0x00, 0x41, 0xD8, 0x00, 0x00, 0x42, 0xDC, 0x01,
            },
            writer.ToImmutableArray().ToArray());

        Assert.False(CanonicalReplayEncoding.CanonicalEquals(
            new CanonicalReplayEncoding.Writer("D", schemaVersion: 3).ToImmutableArray(),
            new CanonicalReplayEncoding.Writer("other", schemaVersion: 3).ToImmutableArray()));
        Assert.False(CanonicalReplayEncoding.CanonicalEquals(
            new CanonicalReplayEncoding.Writer("D", schemaVersion: 3).ToImmutableArray(),
            new CanonicalReplayEncoding.Writer("D", schemaVersion: 4).ToImmutableArray()));
        Assert.Throws<ArgumentException>(() => new CanonicalReplayEncoding.Writer(" ", schemaVersion: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CanonicalReplayEncoding.Writer("D", schemaVersion: 0));
    }

    /// <summary>Verifies complete SHA-256 normalization, validation, hashing, and fixed-width digest emission.</summary>
    [Fact]
    public void Sha256HelpersRequireCompleteHexAndEmitFixedBytes()
    {
        const string uppercase = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        const string lowercase = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        Assert.Equal(lowercase, CanonicalReplayEncoding.NormalizeSha256(uppercase, "digest"));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData([0x01, 0x02, 0x03])).ToLowerInvariant(),
            CanonicalReplayEncoding.ComputeSha256([0x01, 0x02, 0x03]));
        Assert.Throws<ArgumentException>(() => CanonicalReplayEncoding.NormalizeSha256("abc", "digest"));
        Assert.Throws<ArgumentException>(() => CanonicalReplayEncoding.NormalizeSha256(
            new string('g', SHA256.HashSizeInBytes * 2),
            "digest"));

        var writer = new CanonicalReplayEncoding.Writer("digest", schemaVersion: 1);
        var envelopeLength = writer.ToImmutableArray().Length;
        writer.WriteSha256(uppercase, "digest");
        Assert.Equal(
            Convert.FromHexString(lowercase),
            writer.ToImmutableArray().AsSpan()[envelopeLength..].ToArray());
    }

    /// <summary>Verifies defensive copying plus content-only equality and deterministic canonical hashing.</summary>
    [Fact]
    public void CopyEqualityAndHashHelpersIgnoreMutableBackingStorage()
    {
        var backing = new byte[] { 0x10, 0x20, 0x30 };
        var exposed = ImmutableCollectionsMarshal.AsImmutableArray(backing);
        var copied = CanonicalReplayEncoding.Copy(exposed);
        backing[0] = 0xFF;

        Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, copied.ToArray());
        Assert.True(CanonicalReplayEncoding.CanonicalEquals(copied, ImmutableArray.Create<byte>(0x10, 0x20, 0x30)));
        Assert.False(CanonicalReplayEncoding.CanonicalEquals(copied, exposed));
        Assert.Equal(
            BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(copied.AsSpan())),
            CanonicalReplayEncoding.CanonicalHashCode(copied));
        Assert.Equal(
            CanonicalReplayEncoding.CanonicalHashCode(ImmutableArray<byte>.Empty),
            CanonicalReplayEncoding.CanonicalHashCode(default(ImmutableArray<byte>)));

        Span<int> values = stackalloc[] { 1, 2, 3 };
        var spanCopy = CanonicalReplayEncoding.Copy<int>(values);
        values[0] = 99;
        Assert.Equal(new[] { 1, 2, 3 }, spanCopy.ToArray());
    }

    /// <summary>
    /// Verifies table/RID checks and target-width-aware pointer and memory-range validation at every edge.
    /// </summary>
    [Fact]
    public void MetadataAndAddressValidatorsRejectNilWrongTableAndOverflow()
    {
        Assert.True(CanonicalReplayEncoding.IsMetadataTokenForTable(0x02000001, tableIndex: 0x02));
        Assert.True(CanonicalReplayEncoding.IsMetadataTokenForTable(0x02FFFFFF, tableIndex: 0x02));
        Assert.False(CanonicalReplayEncoding.IsMetadataTokenForTable(0x02000000, tableIndex: 0x02));
        Assert.False(CanonicalReplayEncoding.IsMetadataTokenForTable(0x04000001, tableIndex: 0x02));
        Assert.Equal(
            0x0200002A,
            CanonicalReplayEncoding.ValidateMetadataToken(0x0200002A, 0x02, "metadataToken"));
        Assert.Equal(42, CanonicalReplayEncoding.MetadataTokenRowId(0x0200002A));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidateMetadataToken(0x02000000, 0x02, "metadataToken"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidateMetadataToken(0x04000001, 0x02, "metadataToken"));

        CanonicalReplayEncoding.ValidatePointerWidth(sizeof(uint));
        CanonicalReplayEncoding.ValidatePointerWidth(sizeof(ulong));
        Assert.Equal(
            uint.MaxValue,
            CanonicalReplayEncoding.ValidatePointerValue(uint.MaxValue, sizeof(uint), false, "pointer"));
        Assert.Equal(0UL, CanonicalReplayEncoding.ValidatePointerValue(0, sizeof(ulong), true, "pointer"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidatePointerWidth(16));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidatePointerValue((ulong)uint.MaxValue + 1, sizeof(uint), true, "pointer"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidatePointerValue(0, sizeof(ulong), false, "pointer"));

        CanonicalReplayEncoding.ValidateAddressRange(uint.MaxValue - 3UL, 4, sizeof(uint), "address");
        CanonicalReplayEncoding.ValidateAddressRange(ulong.MaxValue, 1, sizeof(ulong), "address");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidateAddressRange(0, 1, sizeof(ulong), "address"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidateAddressRange(1, 0, sizeof(ulong), "address"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidateAddressRange(uint.MaxValue - 2UL, 4, sizeof(uint), "address"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalReplayEncoding.ValidateAddressRange(ulong.MaxValue, 2, sizeof(ulong), "address"));
    }

    /// <summary>
    /// Verifies that deterministic bounds reject a missing default payload, accept explicit empty input, copy and
    /// sort values, and reject null or duplicate-name entries independently of caller order and value.
    /// </summary>
    [Fact]
    public void BoundsAreDefensivelyCopiedSortedAndDuplicateFree()
    {
        Assert.Throws<ArgumentException>(() => CanonicalReplayEncoding.NormalizeBounds(default, "bounds"));
        Assert.Empty(CanonicalReplayEncoding.NormalizeBounds(
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            "bounds"));

        var mutable = new[]
        {
            new EvaluationDeterministicBound("z.bound", 9),
            new EvaluationDeterministicBound("a.bound", 1),
        };
        var normalized = CanonicalReplayEncoding.NormalizeBounds(
            ImmutableCollectionsMarshal.AsImmutableArray(mutable),
            "bounds");
        mutable[0] = new EvaluationDeterministicBound("mutated.bound", 99);

        Assert.Collection(
            normalized,
            bound => Assert.Equal(new EvaluationDeterministicBound("a.bound", 1), bound),
            bound => Assert.Equal(new EvaluationDeterministicBound("z.bound", 9), bound));
        Assert.Throws<ArgumentException>(() => CanonicalReplayEncoding.NormalizeBounds(
            ImmutableArray.CreateRange(new EvaluationDeterministicBound[] { null! }),
            "bounds"));
        Assert.Throws<ArgumentException>(() => CanonicalReplayEncoding.NormalizeBounds(
            ImmutableArray.Create(
                new EvaluationDeterministicBound("same.bound", 1),
                new EvaluationDeterministicBound("same.bound", 1)),
            "bounds"));
        Assert.Throws<ArgumentException>(() => CanonicalReplayEncoding.NormalizeBounds(
            ImmutableArray.Create(
                new EvaluationDeterministicBound("same.bound", 1),
                new EvaluationDeterministicBound("same.bound", 2)),
            "bounds"));
    }
}
