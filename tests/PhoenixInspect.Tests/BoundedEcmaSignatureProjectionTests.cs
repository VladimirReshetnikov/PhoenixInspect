using PhoenixInspect.Core.Abstractions;
using Xunit;

namespace PhoenixInspect.Tests;

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

    /// <summary>
    /// Proves TypeSpec and FieldSig use the same recursive grammar while the optional node stream retains enough
    /// parent, token, header, arity, and shape data to reconstruct a detached tree.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Typed_decoder_projects_complex_type_specification_and_field_trees()
    {
        byte[] typeSpecification =
        [
            0x15, 0x12, 0x04, 0x02,
            0x1D, 0x08,
            0x1B, 0x09, 0x02,
            0x0F, 0x01,
            0x10, 0x12, 0x05,
            0x16,
        ];
        var sink = new RecordingNodeSink();

        var typeOutcome = DecodeTyped(
            typeSpecification,
            BoundedEcmaSignatureForm.TypeSpecification,
            sink);

        Assert.Equal(BoundedEcmaSignatureDecodeKind.Exact, typeOutcome.Kind);
        Assert.Equal(BoundedEcmaSignatureFailureKind.None, typeOutcome.Failure);
        Assert.Equal(BoundedEcmaSignatureBoundKind.None, typeOutcome.ReachedBound);
        Assert.Equal(typeSpecification.Length, typeOutcome.Counters.ConsumedByteCount);
        Assert.Equal(8, typeOutcome.Counters.AggregateTypeCount);
        Assert.Equal(2, typeOutcome.Counters.AggregateGenericArgumentCount);
        Assert.Equal(4, typeOutcome.Counters.MaximumObservedDepth);
        Assert.Equal(sink.Nodes.Count, typeOutcome.Counters.ProjectedNodeCount);
        Assert.Equal(BoundedEcmaSignatureForm.TypeSpecification, typeOutcome.Certificate!.Value.Form);
        Assert.Equal(-1, typeOutcome.Certificate.Value.Header);

        var root = Assert.Single(sink.Nodes, node => node.ParentNodeOrdinal == -1);
        Assert.Equal(BoundedEcmaSignatureNodeKind.GenericInstantiation, root.Kind);
        var head = Assert.Single(sink.Nodes, node =>
            node.ParentNodeOrdinal == root.NodeOrdinal &&
            node.Kind == BoundedEcmaSignatureNodeKind.Class);
        Assert.Equal(0x02000001, head.MetadataToken);
        Assert.Equal(2, head.Count);
        var functionPointer = Assert.Single(sink.Nodes, node =>
            node.ParentNodeOrdinal == root.NodeOrdinal &&
            node.Kind == BoundedEcmaSignatureNodeKind.FunctionPointer);
        Assert.Equal(0x09, functionPointer.Header);
        Assert.Equal(0, functionPointer.Index);
        Assert.Equal(2, functionPointer.Count);
        Assert.Contains(sink.Nodes, node =>
            node.ParentNodeOrdinal == functionPointer.NodeOrdinal &&
            node.Kind == BoundedEcmaSignatureNodeKind.ByReference);

        byte[] fieldSignature =
        [
            0x06,
            0x20, 0x06,
            0x15, 0x11, 0x04, 0x01,
            0x14, 0x13, 0x00, 0x02, 0x01, 0x03, 0x01, 0x7F,
        ];
        sink = new RecordingNodeSink();

        var fieldOutcome = DecodeTyped(fieldSignature, BoundedEcmaSignatureForm.Field, sink);

        Assert.Equal(BoundedEcmaSignatureDecodeKind.Exact, fieldOutcome.Kind);
        Assert.Equal(0x06, fieldOutcome.Certificate!.Value.Header);
        Assert.Equal(1, fieldOutcome.Counters.AggregateGenericArgumentCount);
        var modifier = Assert.Single(sink.Nodes, node =>
            node.Kind == BoundedEcmaSignatureNodeKind.OptionalModifier);
        Assert.Equal(0x1B000001, modifier.MetadataToken);
        var arrayShape = Assert.Single(sink.Nodes, node =>
            node.Kind == BoundedEcmaSignatureNodeKind.ArrayShape);
        Assert.Equal(2, arrayShape.Count);
        Assert.Contains(sink.Nodes, node =>
            node.ParentNodeOrdinal == arrayShape.NodeOrdinal &&
            node.Kind == BoundedEcmaSignatureNodeKind.ArraySize &&
            node.Index == 0 &&
            node.Value == 3);
        Assert.Contains(sink.Nodes, node =>
            node.ParentNodeOrdinal == arrayShape.NodeOrdinal &&
            node.Kind == BoundedEcmaSignatureNodeKind.ArrayLowerBound &&
            node.Index == 0 &&
            node.Value == -1);
    }

    /// <summary>
    /// Proves plain Type positions reject return/parameter-only markers, while return, parameter, pointer-target, and
    /// local positions admit only their explicitly assigned alternatives.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Context_specific_type_grammar_rejects_nested_special_markers()
    {
        foreach (var signature in new[]
                 {
                     new byte[] { 0x01 },
                     new byte[] { 0x10, 0x08 },
                     new byte[] { 0x16 },
                     new byte[] { 0x1D, 0x01 },
                     new byte[] { 0x1D, 0x10, 0x08 },
                     new byte[] { 0x15, 0x12, 0x04, 0x01, 0x16 },
                     new byte[] { 0x0F, 0x10, 0x08 },
                     new byte[] { 0x0F, 0x16 },
                 })
        {
            AssertTypedInvalid(
                signature,
                BoundedEcmaSignatureForm.TypeSpecification,
                BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
        }

        Assert.Equal(
            BoundedEcmaSignatureDecodeKind.Exact,
            DecodeTyped([0x0F, 0x01], BoundedEcmaSignatureForm.TypeSpecification).Kind);
        Assert.Equal(
            BoundedEcmaSignatureDecodeKind.Exact,
            DecodeTyped([0x00, 0x02, 0x01, 0x16, 0x10, 0x08], BoundedEcmaSignatureForm.MethodDefinition).Kind);
        Assert.Equal(
            BoundedEcmaSignatureDecodeKind.Exact,
            DecodeTyped([0x07, 0x02, 0x16, 0x10, 0x08], BoundedEcmaSignatureForm.LocalVariables).Kind);

        foreach (var signature in new[]
                 {
                     new byte[] { 0x06, 0x01 },
                     new byte[] { 0x06, 0x10, 0x08 },
                     new byte[] { 0x06, 0x16 },
                     new byte[] { 0x06, 0x1D, 0x16 },
                 })
        {
            AssertTypedInvalid(
                signature,
                BoundedEcmaSignatureForm.Field,
                BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
        }

        AssertTypedInvalid(
            [0x00, 0x01, 0x01, 0x01],
            BoundedEcmaSignatureForm.MethodDefinition,
            BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
        AssertTypedInvalid(
            [0x00, 0x01, 0x01, 0x1D, 0x16],
            BoundedEcmaSignatureForm.MethodDefinition,
            BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
        AssertTypedInvalid(
            [0x07, 0x01, 0x1D, 0x10, 0x08],
            BoundedEcmaSignatureForm.LocalVariables,
            BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
    }

    /// <summary>
    /// Proves nested function pointers admit every supported convention and reject generic non-default headers,
    /// receiver-bit contradictions, reserved bits, and sentinels with a stable typed reason.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Typed_function_pointer_grammar_covers_conventions_and_header_failures()
    {
        foreach (var convention in new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x09 })
        {
            var outcome = DecodeTyped(
                [0x1B, convention, 0x01, 0x01, 0x08],
                BoundedEcmaSignatureForm.TypeSpecification);
            Assert.Equal(BoundedEcmaSignatureDecodeKind.Exact, outcome.Kind);
        }

        var genericManaged = DecodeTyped(
            [0x1B, 0x10, 0x02, 0x01, 0x1E, 0x00, 0x13, 0x01],
            BoundedEcmaSignatureForm.TypeSpecification);
        Assert.Equal(BoundedEcmaSignatureDecodeKind.Exact, genericManaged.Kind);

        foreach (var signature in new[]
                 {
                     new byte[] { 0x1B, 0x11, 0x01, 0x00, 0x01 },
                     new byte[] { 0x1B, 0x15, 0x01, 0x00, 0x01 },
                     new byte[] { 0x1B, 0x19, 0x01, 0x00, 0x01 },
                 })
        {
            AssertTypedInvalid(
                signature,
                BoundedEcmaSignatureForm.TypeSpecification,
                BoundedEcmaSignatureFailureKind.InvalidGenericHeader);
        }

        AssertTypedInvalid(
            [0x1B, 0x40, 0x00, 0x01],
            BoundedEcmaSignatureForm.TypeSpecification,
            BoundedEcmaSignatureFailureKind.InvalidReceiverFlags);
        AssertTypedInvalid(
            [0x1B, 0x80, 0x00, 0x01],
            BoundedEcmaSignatureForm.TypeSpecification,
            BoundedEcmaSignatureFailureKind.InvalidHeader);
        AssertTypedInvalid(
            [0x1B, 0x05, 0x02, 0x01, 0x08, 0x41, 0x09],
            BoundedEcmaSignatureForm.TypeSpecification,
            BoundedEcmaSignatureFailureKind.SentinelNotPermitted);
        AssertTypedInvalid(
            [0x1B, 0x00, 0x01, 0x41, 0x08],
            BoundedEcmaSignatureForm.TypeSpecification,
            BoundedEcmaSignatureFailureKind.SentinelNotPermitted);
        AssertTypedInvalid(
            [0x1B, 0x00, 0x00, 0x01, 0x41],
            BoundedEcmaSignatureForm.TypeSpecification,
            BoundedEcmaSignatureFailureKind.SentinelNotPermitted);
    }

    /// <summary>
    /// Proves typed outcomes distinguish grammar failures from every parser-owned cap and retain cap-plus-one
    /// counters without exposing an exact certificate.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Typed_outcomes_distinguish_invalid_input_from_parser_bounds()
    {
        var invalid = BoundedEcmaSignatureProjection.Decode(
            [0x06, 0x08, 0x00],
            BoundedEcmaSignatureForm.Field,
            BoundedEcmaSignatureLimits.Create(3, 1, 1));
        Assert.Equal(BoundedEcmaSignatureDecodeKind.Invalid, invalid.Kind);
        Assert.Equal(BoundedEcmaSignatureFailureKind.TrailingData, invalid.Failure);
        Assert.Null(invalid.Certificate);

        var byteBound = BoundedEcmaSignatureProjection.Decode(
            [0x06, 0x08],
            BoundedEcmaSignatureForm.Field,
            BoundedEcmaSignatureLimits.Create(1, 1, 1));
        AssertBound(byteBound, BoundedEcmaSignatureBoundKind.ByteCount);
        Assert.Equal(2, byteBound.Counters.InputByteCount);
        Assert.Equal(0, byteBound.Counters.ConsumedByteCount);

        var depthBound = BoundedEcmaSignatureProjection.Decode(
            [0x0F, 0x0F, 0x08],
            BoundedEcmaSignatureForm.TypeSpecification,
            BoundedEcmaSignatureLimits.Create(3, 2, 3));
        AssertBound(depthBound, BoundedEcmaSignatureBoundKind.RecursiveDepth);
        Assert.Equal(3, depthBound.Counters.MaximumObservedDepth);

        var typeBound = BoundedEcmaSignatureProjection.Decode(
            [0x0F, 0x0F, 0x08],
            BoundedEcmaSignatureForm.TypeSpecification,
            BoundedEcmaSignatureLimits.Create(3, 3, 2));
        AssertBound(typeBound, BoundedEcmaSignatureBoundKind.AggregateTypeCount);
        Assert.Equal(3, typeBound.Counters.AggregateTypeCount);

        var genericBound = BoundedEcmaSignatureProjection.Decode(
            [0x15, 0x12, 0x04, 0x02, 0x08, 0x09],
            BoundedEcmaSignatureForm.TypeSpecification,
            new BoundedEcmaSignatureLimits(6, 2, 3, 1));
        AssertBound(genericBound, BoundedEcmaSignatureBoundKind.AggregateGenericArgumentCount);
        Assert.Equal(2, genericBound.Counters.AggregateGenericArgumentCount);

        var methodArityBound = BoundedEcmaSignatureProjection.Decode(
            [0x10, 0x02, 0x00, 0x01],
            BoundedEcmaSignatureForm.MethodDefinition,
            new BoundedEcmaSignatureLimits(4, 1, 4, 4, 1, 4, 4));
        AssertBound(methodArityBound, BoundedEcmaSignatureBoundKind.GenericParameterCount);
        Assert.Equal(2, methodArityBound.Counters.MaximumDeclaredGenericParameterCount);

        var parameterBound = BoundedEcmaSignatureProjection.Decode(
            [0x00, 0x02, 0x01, 0x08, 0x09],
            BoundedEcmaSignatureForm.MethodDefinition,
            new BoundedEcmaSignatureLimits(5, 1, 4, 4, 4, 1, 4));
        AssertBound(parameterBound, BoundedEcmaSignatureBoundKind.ParameterCount);
        Assert.Equal(2, parameterBound.Counters.MaximumDeclaredParameterCount);

        var localBound = BoundedEcmaSignatureProjection.Decode(
            [0x07, 0x02, 0x08, 0x09],
            BoundedEcmaSignatureForm.LocalVariables,
            new BoundedEcmaSignatureLimits(4, 1, 4, 4, 4, 4, 1));
        AssertBound(localBound, BoundedEcmaSignatureBoundKind.LocalSlotCount);
        Assert.Equal(2, localBound.Counters.MaximumDeclaredLocalSlotCount);

        var arrayRankBound = BoundedEcmaSignatureProjection.Decode(
            [0x14, 0x08, 0x02, 0x00, 0x00],
            BoundedEcmaSignatureForm.TypeSpecification,
            new BoundedEcmaSignatureLimits(5, 2, 2, 2, 2, 2, 2, 1));
        AssertBound(arrayRankBound, BoundedEcmaSignatureBoundKind.ArrayRank);
        Assert.Equal(2, arrayRankBound.Counters.MaximumDeclaredArrayRank);

        var exactArrayRankEdge = BoundedEcmaSignatureProjection.Decode(
            [0x14, 0x08, 0x01, 0x00, 0x00],
            BoundedEcmaSignatureForm.TypeSpecification,
            new BoundedEcmaSignatureLimits(5, 2, 2, 2, 2, 2, 2, 1));
        Assert.Equal(BoundedEcmaSignatureDecodeKind.Exact, exactArrayRankEdge.Kind);
        Assert.Equal(1, exactArrayRankEdge.Counters.MaximumDeclaredArrayRank);
    }

    /// <summary>
    /// Proves the sink-free path retains the established allocation-free behavior on the exact 65,536-local edge,
    /// so the host can validate a large signature without materializing a tree.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Sink_free_decoder_allocates_no_managed_objects_at_large_local_edge()
    {
        var signature = new byte[65_541];
        signature[0] = 0x07;
        signature[1] = 0xC0;
        signature[2] = 0x01;
        signature[3] = 0x00;
        signature[4] = 0x00;
        signature.AsSpan(5).Fill(0x08);
        var limits = new BoundedEcmaSignatureLimits(signature.Length, 1, 65_536, 65_536);

        Assert.True(BoundedEcmaSignatureProjection.Decode(
            signature,
            BoundedEcmaSignatureForm.LocalVariables,
            limits).IsExact);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var outcome = BoundedEcmaSignatureProjection.Decode(
            signature,
            BoundedEcmaSignatureForm.LocalVariables,
            limits);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(BoundedEcmaSignatureDecodeKind.Exact, outcome.Kind);
        Assert.Equal(65_536, outcome.Certificate!.Value.LocalSlotCount);
        Assert.Equal(65_536, outcome.Counters.AggregateTypeCount);
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

    private static BoundedEcmaSignatureDecodeOutcome DecodeTyped(
        byte[] signature,
        BoundedEcmaSignatureForm form,
        IBoundedEcmaSignatureNodeSink? nodeSink = null) =>
        BoundedEcmaSignatureProjection.Decode(
            signature,
            form,
            new BoundedEcmaSignatureLimits(
                DefaultMaximumSignatureLength,
                DefaultMaximumDepth,
                DefaultMaximumAggregateTypeCount,
                DefaultMaximumAggregateTypeCount),
            nodeSink);

    private static void AssertTypedInvalid(
        byte[] signature,
        BoundedEcmaSignatureForm form,
        BoundedEcmaSignatureFailureKind expectedFailure)
    {
        var outcome = DecodeTyped(signature, form);
        Assert.Equal(BoundedEcmaSignatureDecodeKind.Invalid, outcome.Kind);
        Assert.Equal(expectedFailure, outcome.Failure);
        Assert.Equal(BoundedEcmaSignatureBoundKind.None, outcome.ReachedBound);
        Assert.Null(outcome.Certificate);
    }

    private static void AssertBound(
        BoundedEcmaSignatureDecodeOutcome outcome,
        BoundedEcmaSignatureBoundKind expectedBound)
    {
        Assert.Equal(BoundedEcmaSignatureDecodeKind.BoundReached, outcome.Kind);
        Assert.Equal(BoundedEcmaSignatureFailureKind.None, outcome.Failure);
        Assert.Equal(expectedBound, outcome.ReachedBound);
        Assert.Null(outcome.Certificate);
    }

    private sealed class RecordingNodeSink : IBoundedEcmaSignatureNodeSink
    {
        internal List<BoundedEcmaSignatureNodeEvent> Nodes { get; } = [];

        public void Add(in BoundedEcmaSignatureNodeEvent node) => Nodes.Add(node);
    }
}
