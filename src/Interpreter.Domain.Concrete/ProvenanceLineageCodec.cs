using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

internal static class ProvenanceLineageCodec
{
    private const int NodeSchemaVersion = 1;
    private const int GraphSchemaVersion = 1;
    private const int MaximumTypeDepth = 1024;
    private static ReadOnlySpan<byte> NodeDomain => "Interpreter.ProvenanceLineage.Node"u8;
    private static ReadOnlySpan<byte> GraphDomain => "Interpreter.ProvenanceLineage.Graph"u8;

    internal static InputOriginLineageNode CreateInputOrigin(ProvenanceInputOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var bytes = EncodeInputOrigin(origin);
        return new InputOriginLineageNode(LineageNodeId.Hash(bytes.AsSpan()), origin, bytes);
    }

    internal static BinaryTransformLineageNode CreateBinaryTransform(
        BinaryOp operation,
        TypeSig staticType,
        LineageOperand left,
        LineageOperand right)
    {
        ArgumentNullException.ThrowIfNull(staticType);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (staticType != TypeSig.Int32)
        {
            throw new ArgumentException("W4.2 binary lineage requires the structural Int32 type.", nameof(staticType));
        }

        if (left.Kind != LineageOperandKind.Unknown && right.Kind != LineageOperandKind.Unknown)
        {
            throw new ArgumentException("A binary lineage transform requires at least one unknown operand.");
        }

        var bytes = EncodeBinary(operation, staticType, left, right);
        return new BinaryTransformLineageNode(
            LineageNodeId.Hash(bytes.AsSpan()),
            operation,
            staticType,
            left,
            right,
            bytes);
    }

    internal static bool IsCanonical(LineageNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var encoded = node switch
        {
            InputOriginLineageNode input => EncodeInputOrigin(input.Origin),
            BinaryTransformLineageNode binary => EncodeBinary(
                binary.Operation,
                binary.StaticType,
                binary.Left,
                binary.Right),
            _ => ImmutableArray<byte>.Empty,
        };
        return !encoded.IsDefaultOrEmpty &&
            encoded.AsSpan().SequenceEqual(node.CanonicalBytes.AsSpan()) &&
            LineageNodeId.Hash(encoded.AsSpan()) == node.Id;
    }

    internal static ImmutableArray<byte> EncodeGraph(
        LineageNodeId root,
        ImmutableArray<LineageNode> nodes)
    {
        if (!root.IsValid)
        {
            throw new ArgumentException("A non-default graph root is required.", nameof(root));
        }

        if (nodes.IsDefault || nodes.Any(static node => node is null))
        {
            throw new ArgumentException("Graph nodes must be initialized and non-null.", nameof(nodes));
        }

        var writer = new CanonicalWriter();
        writer.WriteBytes(GraphDomain);
        writer.WriteInt32(GraphSchemaVersion);
        writer.WriteDigest(root.Sha256);
        writer.WriteInt32(nodes.Length);
        foreach (var node in nodes)
        {
            writer.WriteDigest(node.Id.Sha256);
            writer.WriteBytes(node.CanonicalBytes.AsSpan());
        }

        return writer.ToImmutableArray();
    }

    private static ImmutableArray<byte> EncodeInputOrigin(ProvenanceInputOrigin origin)
    {
        var writer = StartNode(LineageNodeKind.InputOrigin);
        WriteType(writer, origin.StaticType, 0);
        writer.WriteInt32(EncodeInputKind(origin.Kind));
        writer.WriteInt32(origin.OriginIndex);
        writer.WriteInt32(EncodeEvidence(origin.Evidence));
        writer.WriteDigest(origin.SourceKey.Sha256);
        writer.WriteString(origin.ReasonCode);
        return writer.ToImmutableArray();
    }

    private static ImmutableArray<byte> EncodeBinary(
        BinaryOp operation,
        TypeSig staticType,
        LineageOperand left,
        LineageOperand right)
    {
        var writer = StartNode(LineageNodeKind.BinaryTransform);
        WriteType(writer, staticType, 0);
        writer.WriteInt32(EncodeBinaryOperation(operation));
        WriteOperand(writer, left);
        WriteOperand(writer, right);
        return writer.ToImmutableArray();
    }

    private static CanonicalWriter StartNode(LineageNodeKind kind)
    {
        var writer = new CanonicalWriter();
        writer.WriteBytes(NodeDomain);
        writer.WriteInt32(NodeSchemaVersion);
        writer.WriteInt32(EncodeNodeKind(kind));
        return writer;
    }

    private static void WriteOperand(CanonicalWriter writer, LineageOperand operand)
    {
        writer.WriteInt32(EncodeOperandKind(operand.Kind));
        switch (operand.Kind)
        {
            case LineageOperandKind.ExactInt32 when operand.ExactInt32 is { } exact && operand.Predecessor is null:
                writer.WriteInt32(exact);
                return;
            case LineageOperandKind.Unknown when operand.Predecessor is { } predecessor &&
                                                     operand.ExactInt32 is null &&
                                                     predecessor.IsValid:
                writer.WriteDigest(predecessor.Sha256);
                return;
            default:
                throw new ArgumentException("A lineage operand violates its discriminated-union shape.", nameof(operand));
        }
    }

    private static void WriteType(CanonicalWriter writer, TypeSig type, int depth)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (depth > MaximumTypeDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(type), "A structural lineage type is nested too deeply.");
        }

        writer.WriteInt32(EncodeTypeKind(type.Kind));
        switch (type.Kind)
        {
            case TypeSigKind.Void:
                return;
            case TypeSigKind.Intrinsic when type.IntrinsicKind is { } intrinsic:
                writer.WriteInt32(EncodeIntrinsic(intrinsic));
                return;
            case TypeSigKind.TypeDefinition when type.Module is { } module:
                writer.WriteUInt64(module.High);
                writer.WriteUInt64(module.Low);
                writer.WriteInt32(type.MetadataToken);
                return;
            case TypeSigKind.Synthetic:
                throw new ArgumentException(
                    "Synthetic diagnostic type names cannot participate in lineage identity.",
                    nameof(type));
            case TypeSigKind.SzArray when type.ElementType is { } element:
                WriteType(writer, element, depth + 1);
                return;
            default:
                throw new ArgumentException("A structural lineage type violates its discriminated-union shape.", nameof(type));
        }
    }

    private static int EncodeNodeKind(LineageNodeKind kind) => kind switch
    {
        LineageNodeKind.InputOrigin => 1,
        LineageNodeKind.BinaryTransform => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static int EncodeInputKind(ProvenanceInputKind kind) => kind switch
    {
        ProvenanceInputKind.RequestArgument => 1,
        ProvenanceInputKind.Receiver => 2,
        ProvenanceInputKind.ImportedField => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static int EncodeEvidence(EvaluationEvidenceStatus evidence) => evidence switch
    {
        EvaluationEvidenceStatus.Partial => 1,
        EvaluationEvidenceStatus.Unavailable => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(evidence)),
    };

    private static int EncodeOperandKind(LineageOperandKind kind) => kind switch
    {
        LineageOperandKind.ExactInt32 => 1,
        LineageOperandKind.Unknown => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static int EncodeBinaryOperation(BinaryOp operation) => operation switch
    {
        BinaryOp.Add => 1,
        BinaryOp.Sub => 2,
        BinaryOp.Mul => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static int EncodeTypeKind(TypeSigKind kind) => kind switch
    {
        TypeSigKind.Void => 1,
        TypeSigKind.Intrinsic => 2,
        TypeSigKind.TypeDefinition => 3,
        TypeSigKind.Synthetic => 4,
        TypeSigKind.SzArray => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static int EncodeIntrinsic(IntrinsicTypeKind kind) => kind switch
    {
        IntrinsicTypeKind.Boolean => 1,
        IntrinsicTypeKind.Int32 => 2,
        IntrinsicTypeKind.Int64 => 3,
        IntrinsicTypeKind.String => 4,
        IntrinsicTypeKind.Object => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private sealed class CanonicalWriter
    {
        private readonly ArrayBufferWriter<byte> buffer = new();

        internal void WriteInt32(int value)
        {
            var span = buffer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32BigEndian(span, value);
            buffer.Advance(sizeof(int));
        }

        internal void WriteUInt64(ulong value)
        {
            var span = buffer.GetSpan(sizeof(ulong));
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
            buffer.Advance(sizeof(ulong));
        }

        internal void WriteString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            WriteBytes(Encoding.UTF8.GetBytes(value));
        }

        internal void WriteDigest(string sha256)
        {
            var bytes = Convert.FromHexString(CanonicalSha256.Require(sha256, nameof(sha256)));
            if (bytes.Length != 32)
            {
                throw new ArgumentException("A canonical digest must contain 32 bytes.", nameof(sha256));
            }

            WriteRaw(bytes);
        }

        internal void WriteBytes(ReadOnlySpan<byte> value)
        {
            WriteInt32(value.Length);
            WriteRaw(value);
        }

        internal ImmutableArray<byte> ToImmutableArray() =>
            ImmutableArray.CreateRange(buffer.WrittenSpan.ToArray());

        private void WriteRaw(ReadOnlySpan<byte> value)
        {
            value.CopyTo(buffer.GetSpan(value.Length));
            buffer.Advance(value.Length);
        }
    }
}
