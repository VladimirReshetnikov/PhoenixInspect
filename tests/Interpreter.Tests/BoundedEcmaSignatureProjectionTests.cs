using Interpreter.Core.Abstractions;
using Xunit;

namespace Interpreter.Tests;

/// <summary>
/// Exercises complete bounded MethodDef and local-variable signature decoding with complex synthetic ECMA-335 blobs.
/// </summary>
/// <remarks>
/// These headless tests freeze the draft byte-only structural contract. Metadata-row existence and generic-context
/// cardinality remain separate resolver responsibilities.
/// </remarks>
public sealed class BoundedEcmaSignatureProjectionTests
{
    private const int DefaultMaximumSignatureLength = 4_096;
    private const int DefaultMaximumDepth = 64;
    private const int DefaultMaximumAggregateTypeCount = 256;

    /// <summary>
    /// Proves MethodDef decoding exposes header counts while fully consuming every recursively legal type shape.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Method_definition_decodes_counts_and_complete_recursive_type_shapes()
    {
        var signature = new byte[]
        {
            0x10, 0x02, 0x08,
            0x20, 0x06, 0x10, 0x1F, 0x04,
            0x15, 0x12, 0x05, 0x02,
            0x13, 0x00,
            0x1D, 0x20, 0x06, 0x1E, 0x01,
            0x0F, 0x1F, 0x06, 0x01,
            0x10, 0x14, 0x08, 0x02, 0x02, 0x03, 0x04, 0x02, 0x00, 0x7F,
            0x15, 0x11, 0x04, 0x01, 0x08,
            0x1B, 0x01, 0x02, 0x01, 0x08, 0x0F, 0x05,
            0x12, 0x05,
            0x11, 0x04,
            0x16,
            0x1C,
        };

        var projection = DecodeMethod(signature);

        Assert.Equal(0, projection.CallingConvention);
        Assert.False(projection.HasThis);
        Assert.False(projection.HasExplicitThis);
        Assert.Equal(2, projection.GenericParameterCount);
        Assert.Equal(8, projection.ParameterCount);
        Assert.Equal(19, projection.AggregateTypeCount);
        Assert.Equal(3, projection.MaximumObservedDepth);

        var genericInstance = DecodeMethod(0x30, 0x01, 0x00, 0x01);
        Assert.True(genericInstance.HasThis);
        Assert.False(genericInstance.HasExplicitThis);
        Assert.Equal(1, genericInstance.GenericParameterCount);
        Assert.Equal(0, genericInstance.ParameterCount);

        var variableArguments = DecodeMethod(0x05, 0x01, 0x01, 0x08);
        Assert.Equal(5, variableArguments.CallingConvention);
        Assert.Equal(1, variableArguments.ParameterCount);
    }

    /// <summary>
    /// Proves MethodDef headers, compressed counts, fixed parameter lists, and complete consumption are mandatory.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Method_definition_rejects_invalid_headers_counts_prefixes_and_suffixes()
    {
        foreach (var signature in new[]
                 {
                     Array.Empty<byte>(),
                     new byte[] { 0x01, 0x00, 0x01 },
                     new byte[] { 0x02, 0x00, 0x01 },
                     new byte[] { 0x09, 0x00, 0x01 },
                     new byte[] { 0x40, 0x00, 0x01 },
                     new byte[] { 0x60, 0x00, 0x01 },
                     new byte[] { 0x70, 0x01, 0x00, 0x01 },
                     new byte[] { 0x80, 0x00, 0x01 },
                     new byte[] { 0x15, 0x01, 0x00, 0x01 },
                     new byte[] { 0x10, 0x00, 0x00, 0x01 },
                     new byte[] { 0x10, 0x80, 0x01, 0x00, 0x01 },
                     new byte[] { 0x00, 0x80, 0x00, 0x01 },
                     new byte[] { 0x00, 0x01, 0x01 },
                     new byte[] { 0x00, 0x00 },
                     new byte[] { 0x00, 0x00, 0x01, 0x00 },
                     new byte[] { 0x05, 0x01, 0x01, 0x41, 0x08 },
                 })
        {
            AssertMethodRejected(signature);
        }
    }

    /// <summary>
    /// Proves local decoding admits modifiers, managed pointers, pinned value/reference/pointer types, arrays, and
    /// nested function pointers while returning the exact slot count.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Local_signature_decodes_complete_recursive_type_shapes_and_pinned_constraints()
    {
        var signature = new byte[]
        {
            0x07, 0x0A,
            0x1F, 0x06, 0x45, 0x20, 0x05, 0x10, 0x1F, 0x04, 0x08,
            0x45, 0x1C,
            0x45, 0x12, 0x05,
            0x45, 0x1D, 0x20, 0x06, 0x0E,
            0x10, 0x1E, 0x00,
            0x0F, 0x20, 0x06, 0x01,
            0x16,
            0x14, 0x08, 0x02, 0x01, 0x03, 0x01, 0x7F,
            0x15, 0x11, 0x04, 0x02, 0x13, 0x00, 0x1E, 0x01,
            0x1B, 0x09, 0x01, 0x20, 0x04, 0x01, 0x0F, 0x08,
        };

        var projection = DecodeLocal(signature);

        Assert.Equal(10, projection.LocalSlotCount);
        Assert.Equal(18, projection.AggregateTypeCount);
        Assert.Equal(3, projection.MaximumObservedDepth);

        var empty = DecodeLocal(0x07, 0x00);
        Assert.Equal(0, empty.LocalSlotCount);
        Assert.Equal(0, empty.AggregateTypeCount);
        Assert.Equal(0, empty.MaximumObservedDepth);

        var pinnedGenericParameter = DecodeLocal(0x07, 0x01, 0x45, 0x13, 0x00);
        Assert.Equal(1, pinnedGenericParameter.LocalSlotCount);

        var pinnedValueAndPointerTypes = DecodeLocal(
            0x07, 0x03,
            0x45, 0x08,
            0x45, 0x11, 0x04,
            0x45, 0x0F, 0x08);
        Assert.Equal(3, pinnedValueAndPointerTypes.LocalSlotCount);
    }

    /// <summary>
    /// Proves local headers, slot cardinality, context-sensitive modifiers, and recursive encodings reject every tested
    /// truncation, noncanonical integer, forbidden type placement, and trailing byte.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Local_signature_rejects_malformed_recursive_shapes_and_noncanonical_encodings()
    {
        foreach (var signature in new[]
                 {
                     Array.Empty<byte>(),
                     new byte[] { 0x47, 0x01, 0x08 },
                     new byte[] { 0x07, 0x80, 0x01, 0x08 },
                     new byte[] { 0x07, 0xC0, 0x00, 0xFF, 0xFF, 0x08 },
                     new byte[] { 0x07, 0x01 },
                     new byte[] { 0x07, 0x01, 0x01 },
                     new byte[] { 0x07, 0x01, 0x08, 0x08 },
                     new byte[] { 0x07, 0x02, 0x08 },
                     new byte[] { 0x07, 0x01, 0x10, 0x10, 0x08 },
                     new byte[] { 0x07, 0x01, 0x10, 0x01 },
                     new byte[] { 0x07, 0x01, 0x45, 0x16 },
                     new byte[] { 0x07, 0x01, 0x45, 0x45, 0x1C },
                     new byte[] { 0x07, 0x01, 0x41 },
                     new byte[] { 0x07, 0x01, 0x1F },
                     new byte[] { 0x07, 0x01, 0x1F, 0x00, 0x08 },
                     new byte[] { 0x07, 0x01, 0x1F, 0x07, 0x08 },
                     new byte[] { 0x07, 0x01, 0x1F, 0x80, 0x06, 0x08 },
                     new byte[] { 0x07, 0x01, 0x12, 0x06 },
                     new byte[] { 0x07, 0x01, 0x0F },
                     new byte[] { 0x07, 0x01, 0x0F, 0x10, 0x08 },
                     new byte[] { 0x07, 0x01, 0x1D, 0x01 },
                     new byte[] { 0x07, 0x01, 0x13, 0x80, 0x00 },
                     new byte[] { 0x07, 0x01, 0x15, 0x12, 0x04, 0x00 },
                     new byte[] { 0x07, 0x01, 0x15, 0x12, 0x06, 0x01, 0x08 },
                     new byte[] { 0x07, 0x01, 0x15, 0x12, 0x04, 0x01, 0x10, 0x08 },
                     new byte[] { 0x07, 0x01, 0x14, 0x08, 0x00, 0x00, 0x00 },
                     new byte[] { 0x07, 0x01, 0x14, 0x08, 0x01, 0x02, 0x01, 0x02, 0x00 },
                     new byte[] { 0x07, 0x01, 0x14, 0x08, 0x01, 0x00, 0x02, 0x00, 0x00 },
                     new byte[] { 0x07, 0x01, 0x14, 0x08, 0x01, 0x00, 0x01, 0x80, 0x00 },
                 })
        {
            AssertLocalRejected(signature);
        }
    }

    /// <summary>
    /// Proves nested function pointers accept complete managed, legacy unmanaged, and modern unmanaged MethodDefSig
    /// forms while rejecting MethodRefSig sentinels and enforcing generic, receiver, parameter-count, and
    /// full-consumption rules.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Function_pointer_signatures_enforce_nested_calling_convention_rules()
    {
        DecodeLocal(0x07, 0x01, 0x1B, 0x60, 0x01, 0x01, 0x12, 0x04);
        DecodeLocal(0x07, 0x01, 0x1B, 0x01, 0x02, 0x01, 0x08, 0x0F, 0x20, 0x06, 0x05);
        DecodeLocal(0x07, 0x01, 0x1B, 0x05, 0x02, 0x01, 0x08, 0x1D, 0x12, 0x04);
        DecodeLocal(0x07, 0x01, 0x1B, 0x09, 0x00, 0x20, 0x04, 0x01);
        DecodeLocal(
            0x07, 0x01,
            0x1B, 0x05, 0x02, 0x01,
            0x1B, 0x00, 0x00, 0x01,
            0x08);

        foreach (var signature in new[]
                 {
                     new byte[] { 0x07, 0x01, 0x1B, 0x06, 0x00, 0x01 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x80, 0x00, 0x01 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x40, 0x00, 0x01 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x15, 0x01, 0x00, 0x01 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x19, 0x01, 0x00, 0x01 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x10, 0x00, 0x00, 0x01 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x00, 0x01, 0x01, 0x41, 0x08 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x01, 0x02, 0x01, 0x08, 0x41, 0x09 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x02, 0x01, 0x01, 0x41, 0x08 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x05, 0x02, 0x01, 0x41, 0x08, 0x41, 0x09 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x05, 0x01, 0x01, 0x08, 0x41 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x05, 0x01, 0x01, 0x41 },
                     new byte[] { 0x07, 0x01, 0x1B, 0x00, 0x02, 0x01, 0x08 },
                 })
        {
            AssertLocalRejected(signature);
        }
    }

    /// <summary>
    /// Proves byte-length, recursive-depth, and aggregate-type limits admit their exact edge and reject edge minus one.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Projection_bounds_apply_at_exact_byte_depth_and_type_edges()
    {
        byte[] local = [0x07, 0x01, 0x0F, 0x0F, 0x08];
        Assert.True(BoundedEcmaSignatureProjection.TryDecodeLocal(
            local, local.Length, maximumDepth: 3, maximumAggregateTypeCount: 3, out var projection));
        Assert.Equal(3, projection.AggregateTypeCount);
        Assert.Equal(3, projection.MaximumObservedDepth);

        Assert.False(BoundedEcmaSignatureProjection.TryDecodeLocal(
            local, local.Length - 1, maximumDepth: 3, maximumAggregateTypeCount: 3, out _));
        Assert.False(BoundedEcmaSignatureProjection.TryDecodeLocal(
            local, local.Length, maximumDepth: 2, maximumAggregateTypeCount: 3, out _));
        Assert.False(BoundedEcmaSignatureProjection.TryDecodeLocal(
            local, local.Length, maximumDepth: 3, maximumAggregateTypeCount: 2, out _));
        Assert.False(BoundedEcmaSignatureProjection.TryDecodeLocal(
            local, maximumSignatureLength: 0, maximumDepth: 3, maximumAggregateTypeCount: 3, out _));
        Assert.False(BoundedEcmaSignatureProjection.TryDecodeLocal(
            local, local.Length, maximumDepth: 0, maximumAggregateTypeCount: 3, out _));
        Assert.False(BoundedEcmaSignatureProjection.TryDecodeLocal(
            local, local.Length, maximumDepth: 3, maximumAggregateTypeCount: 0, out _));

        byte[] method = [0x00, 0x00, 0x01];
        Assert.True(BoundedEcmaSignatureProjection.TryDecodeMethodDefinition(
            method, method.Length, maximumDepth: 1, maximumAggregateTypeCount: 1, out var methodProjection));
        Assert.Equal(1, methodProjection.AggregateTypeCount);
        Assert.False(BoundedEcmaSignatureProjection.TryDecodeMethodDefinition(
            method, method.Length, maximumDepth: 1, maximumAggregateTypeCount: 0, out _));

        byte[] excessiveGenericCount = [0x10, 0x03, 0x00, 0x01];
        Assert.False(BoundedEcmaSignatureProjection.TryDecodeMethodDefinition(
            excessiveGenericCount,
            excessiveGenericCount.Length,
            maximumDepth: 1,
            maximumAggregateTypeCount: 2,
            out _));
    }

    /// <summary>
    /// Proves the existing generic-class TypeSpec facade retains token order, nested depth, and complete-consumption
    /// behavior after adopting the shared recursive reader.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Generic_class_type_specification_facade_reuses_the_complete_reader()
    {
        byte[] signature =
        [
            0x15, 0x12, 0x04, 0x01,
            0x1F, 0x06,
            0x1D,
            0x15, 0x11, 0x05, 0x01, 0x08,
        ];

        Assert.True(BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
            signature,
            maximumSignatureLength: signature.Length,
            maximumDepth: 3,
            maximumAggregateGenericArgumentCount: 2,
            out var projection));
        Assert.Equal(0x02000001, projection.GenericHeadMetadataToken);
        Assert.Equal(1, projection.GenericArgumentCount);
        Assert.Equal(2, projection.AggregateGenericArgumentCount);
        Assert.Equal(3, projection.MaximumObservedDepth);
        Assert.Equal(
            [0x02000001, 0x1B000001, 0x01000001],
            projection.ReferencedTypeMetadataTokens.ToArray());

        Assert.False(BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
            [.. signature, 0x00],
            maximumSignatureLength: signature.Length + 1,
            maximumDepth: 3,
            maximumAggregateGenericArgumentCount: 2,
            out _));
    }

    private static BoundedEcmaMethodSignature DecodeMethod(params byte[] signature)
    {
        Assert.True(
            BoundedEcmaSignatureProjection.TryDecodeMethodDefinition(
                signature,
                DefaultMaximumSignatureLength,
                DefaultMaximumDepth,
                DefaultMaximumAggregateTypeCount,
                out var projection),
            $"Expected MethodDef signature {Convert.ToHexString(signature)} to decode.");
        return projection;
    }

    private static BoundedEcmaLocalSignature DecodeLocal(params byte[] signature)
    {
        Assert.True(
            BoundedEcmaSignatureProjection.TryDecodeLocal(
                signature,
                DefaultMaximumSignatureLength,
                DefaultMaximumDepth,
                DefaultMaximumAggregateTypeCount,
                out var projection),
            $"Expected local signature {Convert.ToHexString(signature)} to decode.");
        return projection;
    }

    private static void AssertMethodRejected(params byte[] signature) =>
        Assert.False(
            BoundedEcmaSignatureProjection.TryDecodeMethodDefinition(
                signature,
                DefaultMaximumSignatureLength,
                DefaultMaximumDepth,
                DefaultMaximumAggregateTypeCount,
                out _),
            $"Expected MethodDef signature {Convert.ToHexString(signature)} to be rejected.");

    private static void AssertLocalRejected(params byte[] signature) =>
        Assert.False(
            BoundedEcmaSignatureProjection.TryDecodeLocal(
                signature,
                DefaultMaximumSignatureLength,
                DefaultMaximumDepth,
                DefaultMaximumAggregateTypeCount,
                out _),
            $"Expected local signature {Convert.ToHexString(signature)} to be rejected.");
}
