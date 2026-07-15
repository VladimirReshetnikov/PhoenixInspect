using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

/// <summary>Represents one ordered operand of an unknown arithmetic transformation.</summary>
public sealed record LineageOperand
{
    private LineageOperand(
        LineageOperandKind kind,
        int? exactInt32,
        LineageNodeId? predecessor)
    {
        Kind = kind;
        ExactInt32 = exactInt32;
        Predecessor = predecessor;
    }

    /// <summary>Gets whether this operand embeds an exact integer or refers to an unknown predecessor.</summary>
    public LineageOperandKind Kind { get; }

    /// <summary>Gets the embedded exact integer, or <see langword="null"/> for an unknown predecessor.</summary>
    public int? ExactInt32 { get; }

    /// <summary>Gets the unknown predecessor identity, or <see langword="null"/> for an exact operand.</summary>
    public LineageNodeId? Predecessor { get; }

    /// <summary>Embeds an exact signed 32-bit operand without creating an unnecessary lineage node.</summary>
    /// <param name="value">The exact operand value.</param>
    /// <returns>An exact operand atom.</returns>
    public static LineageOperand FromExactInt32(int value) =>
        new(LineageOperandKind.ExactInt32, value, null);

    /// <summary>Refers to one validated predecessor unknown.</summary>
    /// <param name="predecessor">The content-addressed predecessor node.</param>
    /// <returns>An unknown operand atom.</returns>
    /// <exception cref="ArgumentException"><paramref name="predecessor"/> is default.</exception>
    public static LineageOperand FromUnknown(LineageNodeId predecessor)
    {
        if (!predecessor.IsValid)
        {
            throw new ArgumentException("A non-default predecessor identity is required.", nameof(predecessor));
        }

        return new LineageOperand(LineageOperandKind.Unknown, null, predecessor);
    }
}

/// <summary>Represents one immutable, content-addressed node in an explained-unknown lineage DAG.</summary>
public abstract class LineageNode
{
    private readonly ImmutableArray<byte> canonicalBytes;

    internal LineageNode(
        LineageNodeId id,
        LineageNodeKind kind,
        TypeSig staticType,
        ImmutableArray<LineageNodeId> dependencies,
        ImmutableArray<byte> canonicalBytes)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("A non-default lineage identity is required.", nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentNullException.ThrowIfNull(staticType);
        if (staticType.Kind == TypeSigKind.Void)
        {
            throw new ArgumentException("A lineage node cannot have the void type.", nameof(staticType));
        }

        if (dependencies.IsDefault || dependencies.Any(static dependency => !dependency.IsValid))
        {
            throw new ArgumentException("Lineage dependencies must be initialized identities.", nameof(dependencies));
        }

        if (canonicalBytes.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Canonical node bytes cannot be empty.", nameof(canonicalBytes));
        }

        Id = id;
        Kind = kind;
        StaticType = staticType;
        Dependencies = dependencies;
        this.canonicalBytes = canonicalBytes;
    }

    /// <summary>Gets the SHA-256 identity of the canonical node bytes.</summary>
    public LineageNodeId Id { get; }

    /// <summary>Gets the closed node classification.</summary>
    public LineageNodeKind Kind { get; }

    /// <summary>Gets the exact structural type of the represented unknown.</summary>
    public TypeSig StaticType { get; }

    /// <summary>Gets the ordered predecessor identities.</summary>
    public ImmutableArray<LineageNodeId> Dependencies { get; }

    /// <summary>Gets the versioned canonical bytes whose SHA-256 is <see cref="Id"/>.</summary>
    public ImmutableArray<byte> CanonicalBytes => canonicalBytes;
}

/// <summary>Represents a partial or unavailable external input that introduced one unknown.</summary>
public sealed class InputOriginLineageNode : LineageNode
{
    internal InputOriginLineageNode(
        LineageNodeId id,
        ProvenanceInputOrigin origin,
        ImmutableArray<byte> canonicalBytes)
        : base(
            id,
            LineageNodeKind.InputOrigin,
            origin?.StaticType ?? throw new ArgumentNullException(nameof(origin)),
            ImmutableArray<LineageNodeId>.Empty,
            canonicalBytes)
    {
        Origin = origin;
    }

    /// <summary>Gets the structured source, status, reason, ordinal, and type facts.</summary>
    public ProvenanceInputOrigin Origin { get; }
}

/// <summary>Represents one ordered add, subtract, or multiply over at least one unknown operand.</summary>
public sealed class BinaryTransformLineageNode : LineageNode
{
    internal BinaryTransformLineageNode(
        LineageNodeId id,
        BinaryOp operation,
        TypeSig staticType,
        LineageOperand left,
        LineageOperand right,
        ImmutableArray<byte> canonicalBytes)
        : base(
            id,
            LineageNodeKind.BinaryTransform,
            staticType,
            GetDependencies(left, right),
            canonicalBytes)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        Operation = operation;
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    /// <summary>Gets the exact admitted arithmetic operation.</summary>
    public BinaryOp Operation { get; }

    /// <summary>Gets the left IL stack operand without commutative reordering.</summary>
    public LineageOperand Left { get; }

    /// <summary>Gets the right IL stack operand without commutative reordering.</summary>
    public LineageOperand Right { get; }

    private static ImmutableArray<LineageNodeId> GetDependencies(
        LineageOperand left,
        LineageOperand right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var builder = ImmutableArray.CreateBuilder<LineageNodeId>(2);
        if (left.Predecessor is { } leftPredecessor)
        {
            builder.Add(leftPredecessor);
        }

        if (right.Predecessor is { } rightPredecessor && rightPredecessor != left.Predecessor)
        {
            builder.Add(rightPredecessor);
        }

        return builder.ToImmutable();
    }
}

/// <summary>
/// Represents one approximate imported-field read through an exact imported receiver and frozen instance field.
/// </summary>
/// <remarks>
/// The receiver identity is the prepared imported-object digest, never the concrete domain's local numeric reference
/// payload. The sole predecessor is the imported-field <see cref="InputOriginLineageNode"/> that retains the partial
/// or unavailable observation.
/// </remarks>
public sealed class FieldLoadTransformLineageNode : LineageNode
{
    internal FieldLoadTransformLineageNode(
        LineageNodeId id,
        ImportedReceiverKey receiver,
        ResolvedField field,
        LineageNodeId inputOrigin,
        ImmutableArray<byte> canonicalBytes)
        : base(
            id,
            LineageNodeKind.FieldLoadTransform,
            TypeSig.Int32,
            ImmutableArray.Create(inputOrigin),
            canonicalBytes)
    {
        if (!receiver.IsValid)
        {
            throw new ArgumentException("A field-load transform requires a non-default imported receiver key.", nameof(receiver));
        }

        ArgumentNullException.ThrowIfNull(field);
        if (field.FieldType != TypeSig.Int32)
        {
            throw new ArgumentException("W4.3 field-load lineage requires the structural Int32 field type.", nameof(field));
        }

        if (field.IsStatic || field.IsLiteral || field.HasRva)
        {
            throw new ArgumentException("W4.3 field-load lineage requires an ordinary instance field.", nameof(field));
        }

        Receiver = receiver;
        Field = field;
        InputOrigin = inputOrigin;
    }

    /// <summary>Gets the complete imported-object evidence digest for the exact receiver.</summary>
    public ImportedReceiverKey Receiver { get; }

    /// <summary>Gets the complete frozen ordinary-instance field descriptor.</summary>
    public ResolvedField Field { get; }

    /// <summary>Gets the sole imported-field origin predecessor.</summary>
    public LineageNodeId InputOrigin { get; }
}
