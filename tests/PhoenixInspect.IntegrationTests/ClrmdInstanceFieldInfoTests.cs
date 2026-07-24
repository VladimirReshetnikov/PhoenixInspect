using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Verifies canonical identity and structural admission for frozen ClrMD instance-field descriptors.</summary>
public sealed class ClrmdInstanceFieldInfoTests
{
    /// <summary>
    /// Proves every nullable child-layout ingredient remains identity-significant when all outer descriptor facts are
    /// otherwise identical.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Canonical_projection_distinguishes_every_nullable_child_layout_ingredient()
    {
        var baselineLayout = new ClrmdNullableInt32FieldLayout(
            HasValueMetadataToken: 0x04000001,
            HasValueAddress: 0x1008,
            HasValueSize: sizeof(byte),
            ValueMetadataToken: 0x04000002,
            ValueAddress: 0x100c,
            ValueSize: sizeof(int));
        var baselineProjection = CreateNullableField(baselineLayout).ToCanonicalReplayProjection();
        var variants = new[]
        {
            baselineLayout with { HasValueMetadataToken = 0x04000003 },
            baselineLayout with { HasValueAddress = 0x1009 },
            baselineLayout with { HasValueSize = 2 },
            baselineLayout with { ValueMetadataToken = 0x04000004 },
            baselineLayout with { ValueAddress = 0x1010 },
            baselineLayout with { ValueSize = 8 },
        };

        Assert.All(
            variants,
            layout => Assert.NotEqual(
                baselineProjection,
                CreateNullableField(layout).ToCanonicalReplayProjection()));
        Assert.NotEqual(
            baselineProjection,
            CreateNullableField(nullableLayout: null).ToCanonicalReplayProjection());
    }

    /// <summary>
    /// Proves structurally valid non-overlapping child fields are admitted while identical, partially overlapping,
    /// duplicate, out-of-extent, and overflowing descriptors are rejected before decoding.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nullable_layout_requires_distinct_non_overlapping_in_extent_storage()
    {
        Assert.True(IsValidLayout(0x1000, sizeof(byte), 0x1004, sizeof(int)));
        Assert.False(IsValidLayout(0x1000, sizeof(byte), 0x1000, sizeof(int)));
        Assert.False(IsValidLayout(0x1004, sizeof(byte), 0x1001, sizeof(int)));
        Assert.False(IsValidLayout(
            0x1000,
            sizeof(byte),
            0x1004,
            sizeof(int),
            hasValueMetadataToken: 0x04000001,
            valueMetadataToken: 0x04000001));
        Assert.False(IsValidLayout(0x1000, sizeof(byte), 0x1008, sizeof(int)));
        Assert.False(ClrmdNullableInt32FieldLayout.HasValidDistinctStorage(
            outerAddress: ulong.MaxValue - 3,
            outerSize: 8,
            hasValueMetadataToken: 0x04000001,
            hasValueAddress: ulong.MaxValue - 3,
            hasValueSize: sizeof(byte),
            valueMetadataToken: 0x04000002,
            valueAddress: ulong.MaxValue - 1,
            valueSize: sizeof(int)));
    }

    private static ClrmdInstanceFieldInfo CreateNullableField(ClrmdNullableInt32FieldLayout? nullableLayout) => new(
        new ClrmdSnapshotIdentity(new string('a', 64)),
        ownerAddress: 0x1000,
        ownerMethodTable: 0x2000,
        ownerTypeName: "Fixture",
        name: "OptionalCount",
        metadataToken: 0x04000005,
        address: 0x1008,
        size: 8,
        isObjectReference: false,
        elementType: "Struct",
        fieldTypeName: "System.Nullable<System.Int32>",
        nullableLayout);

    private static bool IsValidLayout(
        ulong hasValueAddress,
        int hasValueSize,
        ulong valueAddress,
        int valueSize,
        int hasValueMetadataToken = 0x04000001,
        int valueMetadataToken = 0x04000002) =>
        ClrmdNullableInt32FieldLayout.HasValidDistinctStorage(
            outerAddress: 0x1000,
            outerSize: 8,
            hasValueMetadataToken,
            hasValueAddress,
            hasValueSize,
            valueMetadataToken,
            valueAddress,
            valueSize);
}
