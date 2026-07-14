using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

/// <summary>
/// Implements a lifted-flat concrete value lattice for validating domain-parametric IL semantics.
/// </summary>
/// <remarks>
/// Each static type has <c>bottom &lt; known constant &lt; unknown</c>. Distinct known constants join to unknown and
/// meet to bottom. This is intentionally a validation domain rather than a complete CLR runtime: the current slice
/// provides exact primitive integer behavior and deterministic draft representations for object/array references.
/// </remarks>
public sealed class ConcreteDomain : IValueDomain<ConcreteValue>
{
    /// <summary>Gets the canonical draft signature for <see cref="int"/>.</summary>
    public static TypeSig Int32Type { get; } = new("System.Int32");

    /// <summary>Gets the canonical draft signature for <see cref="long"/>.</summary>
    public static TypeSig Int64Type { get; } = new("System.Int64");

    /// <summary>Gets the canonical draft signature for <see cref="bool"/>.</summary>
    public static TypeSig BooleanType { get; } = new("System.Boolean");

    /// <summary>Gets the canonical draft signature for <see cref="string"/>.</summary>
    public static TypeSig StringType { get; } = new("System.String");

    /// <inheritdoc />
    public ConcreteValue Bottom(TypeSig type) => new(ConcreteValueKind.Bottom, RequireType(type));

    /// <inheritdoc />
    public bool IsBottom(ConcreteValue value) => RequireValue(value).Kind == ConcreteValueKind.Bottom;

    /// <inheritdoc />
    public ConcreteValue Top(TypeSig type) => new(ConcreteValueKind.Unknown, RequireType(type));

    internal ConcreteValue ConstNull(TypeSig refType) => new(ConcreteValueKind.Null, RequireType(refType));

    /// <inheritdoc />
    public ConcreteValue ConstInt32(int value) => new(ConcreteValueKind.Int32, Int32Type, value);

    /// <summary>Creates a known 64-bit integer value for memory and profile-boundary validation.</summary>
    /// <param name="value">The exact signed 64-bit payload.</param>
    /// <returns>A concrete I8 value.</returns>
    public ConcreteValue ConstInt64(long value) => new(ConcreteValueKind.Int64, Int64Type, value);

    private ConcreteValue ConstBool(bool value) => new(ConcreteValueKind.Boolean, BooleanType, value);

    /// <summary>Creates a known immutable string value for memory and profile-boundary validation.</summary>
    /// <param name="value">The exact string payload, which diagnostic formatting must redact.</param>
    /// <returns>A concrete non-null string value.</returns>
    public ConcreteValue ConstString(string value) =>
        new(ConcreteValueKind.String, StringType, value ?? throw new ArgumentNullException(nameof(value)));

    /// <inheritdoc />
    public ConcreteValue Join(ConcreteValue a, ConcreteValue b)
    {
        EnsureSameType(a, b);
        if (a.Kind == ConcreteValueKind.Bottom)
        {
            return b;
        }

        if (b.Kind == ConcreteValueKind.Bottom || a == b)
        {
            return a;
        }

        if (a.Kind == ConcreteValueKind.Unknown)
        {
            return a;
        }

        if (b.Kind == ConcreteValueKind.Unknown)
        {
            return b;
        }

        return Top(a.StaticType);
    }

    /// <inheritdoc />
    public bool IsLessThanOrEqual(ConcreteValue a, ConcreteValue b)
    {
        EnsureSameType(a, b);
        return a.Kind == ConcreteValueKind.Bottom ||
            b.Kind == ConcreteValueKind.Unknown ||
            a == b;
    }

    /// <inheritdoc />
    public ConcreteValue Meet(ConcreteValue a, ConcreteValue b)
    {
        EnsureSameType(a, b);
        if (a.Kind == ConcreteValueKind.Bottom || b.Kind == ConcreteValueKind.Bottom)
        {
            return Bottom(a.StaticType);
        }

        if (a.Kind == ConcreteValueKind.Unknown)
        {
            return b;
        }

        if (b.Kind == ConcreteValueKind.Unknown || a == b)
        {
            return a;
        }

        return Bottom(a.StaticType);
    }

    /// <inheritdoc />
    public ConcreteValue Widen(ConcreteValue prev, ConcreteValue next) => Join(prev, next);

    /// <inheritdoc />
    public TypeSig GetStaticType(ConcreteValue value) => RequireValue(value).StaticType;

    /// <inheritdoc />
    public StackKind GetStackKind(ConcreteValue value)
    {
        value = RequireValue(value);
        return value.Kind switch
        {
            ConcreteValueKind.Int32 or ConcreteValueKind.Boolean => StackKind.I4,
            ConcreteValueKind.Int64 => StackKind.I8,
            ConcreteValueKind.Null or ConcreteValueKind.String or ConcreteValueKind.ObjectReference or
                ConcreteValueKind.ArrayReference => StackKind.Ref,
            ConcreteValueKind.Bottom or ConcreteValueKind.Unknown => InferStackKind(value.StaticType),
            _ => StackKind.ValueType,
        };
    }

    /// <inheritdoc />
    public bool TryGetConstInt32(ConcreteValue value, out int c)
    {
        value = RequireValue(value);
        if (value.Kind == ConcreteValueKind.Int32 && value.Payload is int integer)
        {
            c = integer;
            return true;
        }

        if (value.Kind == ConcreteValueKind.Boolean && value.Payload is bool boolean)
        {
            c = boolean ? 1 : 0;
            return true;
        }

        c = default;
        return false;
    }

    /// <inheritdoc />
    public ConcreteValue ApplyBinary(BinaryOp op, ConcreteValue a, ConcreteValue b)
    {
        EnsureSameType(a, b);
        if (a.Kind == ConcreteValueKind.Bottom || b.Kind == ConcreteValueKind.Bottom)
        {
            return Bottom(a.StaticType);
        }

        if (a.Kind == ConcreteValueKind.Unknown || b.Kind == ConcreteValueKind.Unknown)
        {
            return Top(a.StaticType);
        }

        if (TryGetConstInt32(a, out var left32) && TryGetConstInt32(b, out var right32))
        {
            return ApplyInt32(op, left32, right32);
        }

        if (a.Kind == ConcreteValueKind.Int64 && b.Kind == ConcreteValueKind.Int64 &&
            a.Payload is long left64 && b.Payload is long right64)
        {
            return ApplyInt64(op, left64, right64);
        }

        throw new NotSupportedException($"Binary operation {op} is not defined for {a.Kind} and {b.Kind}.");
    }

    internal ConcreteValue ObjectReference(long id, TypeSig type) =>
        new(ConcreteValueKind.ObjectReference, RequireType(type), id);

    internal ConcreteValue ArrayReference(long id, TypeSig elementType) =>
        new(ConcreteValueKind.ArrayReference, new TypeSig($"{RequireType(elementType).DisplayName}[]"), id);

    internal ConcreteValue DefaultValue(TypeSig type) => type.DisplayName switch
    {
        "System.Int32" => ConstInt32(0),
        "System.Int64" => ConstInt64(0),
        "System.Boolean" => ConstBool(false),
        "System.String" or "System.Object" => ConstNull(type),
        _ when type.DisplayName.EndsWith("[]", StringComparison.Ordinal) => ConstNull(type),
        _ => Top(type),
    };

    private ConcreteValue ApplyInt32(BinaryOp op, int left, int right) => op switch
    {
        BinaryOp.Add => ConstInt32(unchecked(left + right)),
        BinaryOp.Sub => ConstInt32(unchecked(left - right)),
        BinaryOp.Mul => ConstInt32(unchecked(left * right)),
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private ConcreteValue ApplyInt64(BinaryOp op, long left, long right) => op switch
    {
        BinaryOp.Add => ConstInt64(unchecked(left + right)),
        BinaryOp.Sub => ConstInt64(unchecked(left - right)),
        BinaryOp.Mul => ConstInt64(unchecked(left * right)),
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private static StackKind InferStackKind(TypeSig type) => type.DisplayName switch
    {
        "System.Int32" or "System.Boolean" or "System.Byte" or "System.SByte" or
            "System.Int16" or "System.UInt16" or "System.UInt32" => StackKind.I4,
        "System.Int64" or "System.UInt64" => StackKind.I8,
        "System.Single" => StackKind.R4,
        "System.Double" => StackKind.R8,
        "System.IntPtr" or "System.UIntPtr" => StackKind.NativeInt,
        "System.String" or "System.Object" => StackKind.Ref,
        _ when type.DisplayName.EndsWith("[]", StringComparison.Ordinal) => StackKind.Ref,
        _ => StackKind.ValueType,
    };

    private static TypeSig RequireType(TypeSig type) => type ?? throw new ArgumentNullException(nameof(type));

    private static ConcreteValue RequireValue(ConcreteValue value) =>
        value ?? throw new ArgumentNullException(nameof(value));

    private static void EnsureSameType(ConcreteValue a, ConcreteValue b)
    {
        a = RequireValue(a);
        b = RequireValue(b);
        if (a.StaticType != b.StaticType)
        {
            throw new ArgumentException($"Domain operation requires equal static types, got {a.StaticType.DisplayName} and {b.StaticType.DisplayName}.");
        }
    }
}
