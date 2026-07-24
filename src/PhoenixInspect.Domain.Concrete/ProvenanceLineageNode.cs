using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Domain.Concrete;

/// <summary>Represents one ordered exact or unknown operand embedded in a lineage transformation.</summary>
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

    internal bool HasValidShape => Kind switch
    {
        LineageOperandKind.ExactInt32 => ExactInt32.HasValue && Predecessor is null,
        LineageOperandKind.Unknown => ExactInt32 is null && Predecessor is { } predecessor && predecessor.IsValid,
        _ => false,
    };
}

/// <summary>Represents one immutable, content-addressed node in an explained-unknown lineage DAG.</summary>
public abstract class LineageNode
{
    private readonly ImmutableArray<LineageNodeId> dependencies;
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
        this.dependencies = Copy(dependencies);
        this.canonicalBytes = Copy(canonicalBytes);
    }

    /// <summary>Gets the SHA-256 identity of the canonical node bytes.</summary>
    public LineageNodeId Id { get; }

    /// <summary>Gets the closed node classification.</summary>
    public LineageNodeKind Kind { get; }

    /// <summary>Gets the exact structural type of the represented unknown.</summary>
    public TypeSig StaticType { get; }

    /// <summary>Gets a defensive copy of the ordered predecessor identities.</summary>
    public ImmutableArray<LineageNodeId> Dependencies => Copy(dependencies);

    /// <summary>Gets a defensive copy of the versioned canonical bytes whose SHA-256 is <see cref="Id"/>.</summary>
    public ImmutableArray<byte> CanonicalBytes => Copy(canonicalBytes);

    private static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty
            ? ImmutableArray<T>.Empty
            : ImmutableArray.CreateRange(values.AsSpan().ToArray());
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

/// <summary>
/// Represents one explained <c>Int32</c> crossing into a metadata-ordered parameter of an admitted direct call.
/// </summary>
/// <remarks>
/// The exact caller MethodDef, call IL offset, callee MethodDef, and zero-based parameter index are identity-bearing.
/// The node has exactly one predecessor: the explanation attached to the caller-stack value before the call boundary.
/// Exact arguments do not receive this node.
/// </remarks>
public sealed class CallArgumentTransformLineageNode : LineageNode
{
    internal CallArgumentTransformLineageNode(
        LineageNodeId id,
        DirectCallSiteIdentity callSite,
        int parameterIndex,
        LineageNodeId predecessor,
        ImmutableArray<byte> canonicalBytes)
        : base(
            id,
            LineageNodeKind.CallArgumentTransform,
            TypeSig.Int32,
            ImmutableArray.Create(predecessor),
            canonicalBytes)
    {
        ValidateCallSite(callSite);
        if (parameterIndex is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameterIndex),
                "The closed W4 call profile requires metadata parameter index zero or one.");
        }

        if (!predecessor.IsValid)
        {
            throw new ArgumentException(
                "A call-argument lineage transform requires a non-default predecessor.",
                nameof(predecessor));
        }

        CallSite = callSite;
        ParameterIndex = parameterIndex;
        Predecessor = predecessor;
    }

    /// <summary>Gets the exact caller, call IL offset, and admitted direct-call target identity.</summary>
    public DirectCallSiteIdentity CallSite { get; }

    /// <summary>Gets the zero-based metadata parameter index assigned to the transformed argument.</summary>
    public int ParameterIndex { get; }

    /// <summary>Gets the caller-side explanation that existed before the argument crossed the call boundary.</summary>
    public LineageNodeId Predecessor { get; }

    private static void ValidateCallSite(DirectCallSiteIdentity callSite)
    {
        if (callSite.Caller == default ||
            callSite.Callee == default ||
            callSite.CallIlOffset < 0 ||
            callSite.Caller.Module != callSite.Callee.Module)
        {
            throw new ArgumentException(
                "A call-argument lineage transform requires one valid same-module direct-call identity.",
                nameof(callSite));
        }
    }
}

/// <summary>
/// Represents one explained <c>Int32</c> returned from an interpreted callee to its frozen caller continuation.
/// </summary>
/// <remarks>
/// The node retains the complete direct-call identity, including the actual callee, and exactly one predecessor: the
/// explanation produced inside that callee. Exact returns cross the boundary without allocating a lineage node.
/// </remarks>
public sealed class InterpretedReturnTransformLineageNode : LineageNode
{
    internal InterpretedReturnTransformLineageNode(
        LineageNodeId id,
        DirectCallSiteIdentity callSite,
        LineageNodeId predecessor,
        ImmutableArray<byte> canonicalBytes)
        : base(
            id,
            LineageNodeKind.InterpretedReturnTransform,
            TypeSig.Int32,
            ImmutableArray.Create(predecessor),
            canonicalBytes)
    {
        if (callSite.Caller == default ||
            callSite.Callee == default ||
            callSite.CallIlOffset < 0 ||
            callSite.Caller.Module != callSite.Callee.Module)
        {
            throw new ArgumentException(
                "An interpreted-return lineage transform requires one valid same-module direct-call identity.",
                nameof(callSite));
        }

        if (!predecessor.IsValid)
        {
            throw new ArgumentException(
                "An interpreted-return lineage transform requires a non-default predecessor.",
                nameof(predecessor));
        }

        CallSite = callSite;
        Predecessor = predecessor;
    }

    /// <summary>Gets the exact caller, call IL offset, and interpreted callee identity.</summary>
    public DirectCallSiteIdentity CallSite { get; }

    /// <summary>Gets the exact MethodDef that produced the returned value.</summary>
    public MethodHandle Callee => CallSite.Callee;

    /// <summary>Gets the callee-side explanation that existed before the result crossed the return boundary.</summary>
    public LineageNodeId Predecessor { get; }
}

/// <summary>
/// Represents one explained <c>Int32</c> returned by a versioned pure model over an admitted direct call.
/// </summary>
/// <remarks>
/// The node retains the complete direct-call identity, stable model identity and semantic version, and exactly two
/// metadata-ordered argument operands. Exact arguments are embedded directly. Each unknown argument refers to its
/// parameter-indexed <see cref="CallArgumentTransformLineageNode"/> created at the same modeled boundary. At least
/// one argument must be unknown, so the modeled result remains grounded in admitted input lineage.
/// </remarks>
public sealed class ModeledReturnTransformLineageNode : LineageNode
{
    private readonly ImmutableArray<LineageOperand> arguments;

    internal ModeledReturnTransformLineageNode(
        LineageNodeId id,
        DirectCallSiteIdentity callSite,
        PureCallModelIdentity modelIdentity,
        ImmutableArray<LineageOperand> arguments,
        ImmutableArray<byte> canonicalBytes)
        : base(
            id,
            LineageNodeKind.ModeledReturnTransform,
            TypeSig.Int32,
            GetDependencies(arguments),
            canonicalBytes)
    {
        if (callSite.Caller == default ||
            callSite.Callee == default ||
            callSite.CallIlOffset < 0 ||
            callSite.Caller.Module != callSite.Callee.Module)
        {
            throw new ArgumentException(
                "A modeled-return lineage transform requires one valid same-module direct-call identity.",
                nameof(callSite));
        }

        if (modelIdentity.StableId is null)
        {
            throw new ArgumentException(
                "A modeled-return lineage transform requires a non-default model identity.",
                nameof(modelIdentity));
        }

        CallSite = callSite;
        ModelIdentity = modelIdentity;
        this.arguments = Copy(arguments);
    }

    /// <summary>Gets the exact caller, call IL offset, and body-free modeled target identity.</summary>
    public DirectCallSiteIdentity CallSite { get; }

    /// <summary>Gets the stable model identifier and exact semantic version selected during preparation.</summary>
    public PureCallModelIdentity ModelIdentity { get; }

    /// <summary>
    /// Gets a defensive copy of the complete metadata-ordered two-argument vector, embedding exact integers and
    /// identifying unknowns.
    /// </summary>
    public ImmutableArray<LineageOperand> Arguments => Copy(arguments);

    private static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty
            ? ImmutableArray<T>.Empty
            : ImmutableArray.CreateRange(values.AsSpan().ToArray());

    private static ImmutableArray<LineageNodeId> GetDependencies(ImmutableArray<LineageOperand> arguments)
    {
        if (arguments.IsDefault ||
            arguments.Length != 2 ||
            arguments.Any(static argument => argument is null || !argument.HasValidShape))
        {
            throw new ArgumentException(
                "A modeled-return lineage transform requires exactly two valid ordered Int32 operands.",
                nameof(arguments));
        }

        var dependencies = ImmutableArray.CreateBuilder<LineageNodeId>(2);
        foreach (var argument in arguments)
        {
            if (argument.Predecessor is { } predecessor)
            {
                dependencies.Add(predecessor);
            }
        }

        if (dependencies.Count == 0)
        {
            throw new ArgumentException(
                "A modeled unknown return must be grounded in at least one unknown argument.",
                nameof(arguments));
        }

        return dependencies.ToImmutable();
    }
}
