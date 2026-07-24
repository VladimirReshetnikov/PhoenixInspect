using PhoenixInspect.Core.Abstractions;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the bounded field and TypeSpec byte readers against the runtime's augmented named-type token rules.
/// </summary>
public sealed class BoundedEcmaSignatureProjectionTests
{
    private const int TypeDefinitionRowOne = 0x02000001;
    private const int TypeReferenceRowOne = 0x01000001;
    private const int TypeSpecificationRowOne = 0x1B000001;
    private const int TypeSpecificationRowTwo = 0x1B000002;

    /// <summary>
    /// Proves direct and generic field type heads accept TypeDef/TypeRef tags while rejecting the TypeSpec tag.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_named_type_heads_distinguish_typedef_typeref_and_typespec_tags()
    {
        var typeDefinitionClass = DecodeField(0x06, 0x12, 0x04);
        Assert.Equal(BoundedEcmaFieldSignatureKind.ClassType, typeDefinitionClass.Kind);
        Assert.Equal(TypeDefinitionRowOne, typeDefinitionClass.NamedTypeMetadataToken);

        var typeReferenceClass = DecodeField(0x06, 0x12, 0x05);
        Assert.Equal(BoundedEcmaFieldSignatureKind.ClassType, typeReferenceClass.Kind);
        Assert.Equal(TypeReferenceRowOne, typeReferenceClass.NamedTypeMetadataToken);

        var typeDefinitionGeneric = DecodeField(0x06, 0x15, 0x11, 0x04, 0x01, 0x08);
        Assert.Equal(BoundedEcmaFieldSignatureKind.GenericInstanceValueTypeInt32, typeDefinitionGeneric.Kind);
        Assert.Equal(TypeDefinitionRowOne, typeDefinitionGeneric.NamedTypeMetadataToken);

        var typeReferenceGeneric = DecodeField(0x06, 0x15, 0x11, 0x05, 0x01, 0x08);
        Assert.Equal(BoundedEcmaFieldSignatureKind.GenericInstanceValueTypeInt32, typeReferenceGeneric.Kind);
        Assert.Equal(TypeReferenceRowOne, typeReferenceGeneric.NamedTypeMetadataToken);

        AssertFieldRejected(0x06, 0x12, 0x06);
        AssertFieldRejected(0x06, 0x15, 0x11, 0x06, 0x01, 0x08);
    }

    /// <summary>
    /// Proves required and optional modifier sequences admit TypeSpec tags and retain their encoded order.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_modifier_sequences_admit_typespec_tags_and_retain_order()
    {
        var projection = DecodeField(
            0x06,
            0x1F, 0x06,
            0x20, 0x05,
            0x12, 0x04);

        Assert.Equal(BoundedEcmaFieldSignatureKind.ClassType, projection.Kind);
        Assert.Equal(TypeDefinitionRowOne, projection.NamedTypeMetadataToken);
        Assert.Equal(
            [TypeSpecificationRowOne, TypeReferenceRowOne],
            projection.CustomModifierTypeMetadataTokens.ToArray());

        var genericProjection = DecodeField(
            0x06,
            0x20, 0x04,
            0x1F, 0x06,
            0x15, 0x11, 0x05, 0x01, 0x08);
        Assert.Equal(TypeReferenceRowOne, genericProjection.NamedTypeMetadataToken);
        Assert.Equal(
            [TypeDefinitionRowOne, TypeSpecificationRowOne],
            genericProjection.CustomModifierTypeMetadataTokens.ToArray());
    }

    /// <summary>
    /// Proves malformed, non-canonical, trailing, and over-bound field encodings remain rejected.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Field_reader_preserves_canonical_byte_and_modifier_count_rules()
    {
        AssertFieldRejected(0x06, 0x1F);
        AssertFieldRejected(0x06, 0x1F, 0x07, 0x08);
        AssertFieldRejected(0x06, 0x1F, 0x80, 0x06, 0x08);
        AssertFieldRejected(0x06, 0x08, 0x00);
        AssertFieldRejected(0x06, 0x12, 0x04, 0x00);
        AssertFieldRejected(0x06, 0x15, 0x11, 0x04, 0x01);

        var atLimit = FieldWithRequiredModifiers(BoundedEcmaFieldSignatureProjection.MaximumCustomModifierCount);
        Assert.True(BoundedEcmaFieldSignatureProjection.TryDecode(atLimit, out _));

        var overLimit = FieldWithRequiredModifiers(BoundedEcmaFieldSignatureProjection.MaximumCustomModifierCount + 1);
        Assert.False(BoundedEcmaFieldSignatureProjection.TryDecode(overLimit, out _));
    }

    /// <summary>
    /// Proves recursive CLASS/VALUETYPE nodes accept TypeDef/TypeRef tags while rejecting TypeSpec tags.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Type_specification_recursive_named_types_distinguish_token_tags()
    {
        var projection = DecodeTypeSpecification(
            0x15, 0x12, 0x04, 0x02,
            0x12, 0x05,
            0x11, 0x04);

        Assert.Equal(TypeDefinitionRowOne, projection.GenericHeadMetadataToken);
        Assert.Equal(2, projection.GenericArgumentCount);
        Assert.Equal(2, projection.AggregateGenericArgumentCount);
        Assert.Equal(1, projection.MaximumObservedDepth);
        Assert.Equal(
            [TypeDefinitionRowOne, TypeReferenceRowOne, TypeDefinitionRowOne],
            projection.ReferencedTypeMetadataTokens.ToArray());

        AssertTypeSpecificationRejected(0x15, 0x12, 0x04, 0x01, 0x12, 0x06);
        AssertTypeSpecificationRejected(0x15, 0x12, 0x04, 0x01, 0x11, 0x06);
    }

    /// <summary>
    /// Proves root and nested generic heads accept TypeDef/TypeRef tags while rejecting TypeSpec tags.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Type_specification_generic_heads_distinguish_token_tags()
    {
        var typeReferenceRoot = DecodeTypeSpecification(0x15, 0x12, 0x05, 0x01, 0x08);
        Assert.Equal(TypeReferenceRowOne, typeReferenceRoot.GenericHeadMetadataToken);

        var nestedClass = DecodeTypeSpecification(
            0x15, 0x12, 0x04, 0x01,
            0x15, 0x12, 0x05, 0x01, 0x08);
        Assert.Equal(2, nestedClass.AggregateGenericArgumentCount);
        Assert.Equal(2, nestedClass.MaximumObservedDepth);
        Assert.Equal(
            [TypeDefinitionRowOne, TypeReferenceRowOne],
            nestedClass.ReferencedTypeMetadataTokens.ToArray());

        var nestedValueType = DecodeTypeSpecification(
            0x15, 0x12, 0x05, 0x01,
            0x15, 0x11, 0x04, 0x01, 0x08);
        Assert.Equal(
            [TypeReferenceRowOne, TypeDefinitionRowOne],
            nestedValueType.ReferencedTypeMetadataTokens.ToArray());

        AssertTypeSpecificationRejected(0x15, 0x12, 0x06, 0x01, 0x08);
        AssertTypeSpecificationRejected(
            0x15, 0x12, 0x04, 0x01,
            0x15, 0x12, 0x06, 0x01, 0x08);
        AssertTypeSpecificationRejected(
            0x15, 0x12, 0x04, 0x01,
            0x15, 0x11, 0x06, 0x01, 0x08);
    }

    /// <summary>
    /// Proves TypeSpec-tagged modifiers remain valid across consecutive outer and nested array modifier sequences.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Type_specification_nested_modifier_sequences_admit_typespec_tags()
    {
        var projection = DecodeTypeSpecification(
            0x15, 0x12, 0x04, 0x01,
            0x1F, 0x06,
            0x20, 0x05,
            0x1D,
            0x1F, 0x0A,
            0x20, 0x04,
            0x12, 0x05);

        Assert.Equal(1, projection.GenericArgumentCount);
        Assert.Equal(1, projection.AggregateGenericArgumentCount);
        Assert.Equal(2, projection.MaximumObservedDepth);
        Assert.Equal(
            [
                TypeDefinitionRowOne,
                TypeSpecificationRowOne,
                TypeReferenceRowOne,
                TypeSpecificationRowTwo,
                TypeDefinitionRowOne,
                TypeReferenceRowOne,
            ],
            projection.ReferencedTypeMetadataTokens.ToArray());
    }

    /// <summary>
    /// Proves canonical-byte, complete-consumption, signature-length, depth, and aggregate-count limits are unchanged.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Type_specification_reader_preserves_existing_bounds_and_malformed_input_rules()
    {
        AssertTypeSpecificationRejected(0x15, 0x12, 0x04, 0x01, 0x1F);
        AssertTypeSpecificationRejected(0x15, 0x12, 0x04, 0x01, 0x1F, 0x07, 0x08);
        AssertTypeSpecificationRejected(0x15, 0x12, 0x04, 0x01, 0x1F, 0x80, 0x06, 0x08);
        AssertTypeSpecificationRejected(0x15, 0x12, 0x80, 0x04, 0x01, 0x08);
        AssertTypeSpecificationRejected(0x15, 0x12, 0x04, 0x01, 0x08, 0x00);
        AssertTypeSpecificationRejected(0x15, 0x12, 0x04, 0x00);

        byte[] nested =
        [
            0x15, 0x12, 0x04, 0x01,
            0x15, 0x12, 0x05, 0x01, 0x08,
        ];
        Assert.True(BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
            nested,
            maximumSignatureLength: nested.Length,
            maximumDepth: 2,
            maximumAggregateGenericArgumentCount: 2,
            out _));
        Assert.False(BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
            nested,
            maximumSignatureLength: nested.Length - 1,
            maximumDepth: 2,
            maximumAggregateGenericArgumentCount: 2,
            out _));
        Assert.False(BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
            nested,
            maximumSignatureLength: nested.Length,
            maximumDepth: 1,
            maximumAggregateGenericArgumentCount: 2,
            out _));
        Assert.False(BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
            nested,
            maximumSignatureLength: nested.Length,
            maximumDepth: 2,
            maximumAggregateGenericArgumentCount: 1,
            out _));
    }

    private static BoundedEcmaFieldSignature DecodeField(params byte[] signature)
    {
        Assert.True(
            BoundedEcmaFieldSignatureProjection.TryDecode(signature, out var projection),
            $"Expected field signature {Convert.ToHexString(signature)} to decode.");
        return projection;
    }

    private static void AssertFieldRejected(params byte[] signature) =>
        Assert.False(
            BoundedEcmaFieldSignatureProjection.TryDecode(signature, out _),
            $"Expected field signature {Convert.ToHexString(signature)} to be rejected.");

    private static BoundedEcmaTypeSpecification DecodeTypeSpecification(params byte[] signature)
    {
        Assert.True(
            BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                signature,
                maximumSignatureLength: 256,
                maximumDepth: 16,
                maximumAggregateGenericArgumentCount: 32,
                out var projection),
            $"Expected TypeSpec signature {Convert.ToHexString(signature)} to decode.");
        return projection;
    }

    private static void AssertTypeSpecificationRejected(params byte[] signature) =>
        Assert.False(
            BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                signature,
                maximumSignatureLength: 256,
                maximumDepth: 16,
                maximumAggregateGenericArgumentCount: 32,
                out _),
            $"Expected TypeSpec signature {Convert.ToHexString(signature)} to be rejected.");

    private static byte[] FieldWithRequiredModifiers(int modifierCount)
    {
        var signature = new List<byte>(2 + (modifierCount * 2)) { 0x06 };
        for (var index = 0; index < modifierCount; index++)
        {
            signature.Add(0x1F);
            signature.Add(0x04);
        }
        signature.Add(0x08);
        return signature.ToArray();
    }
}
