using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines the minimum value-domain contract required by the interpreter core.
/// </summary>
/// <typeparam name="TValue">Concrete value representation used by a domain implementation.</typeparam>
public interface IValueDomain<TValue>
{
    /// <summary>Creates a top/unknown value for a specific static type and provenance.</summary>
    TValue Top(TypeSig type, UnknownOrigin origin);

    /// <summary>Creates a null reference value for a specific reference type.</summary>
    TValue ConstNull(TypeSig refType);

    /// <summary>Creates an <see cref="int"/> constant value.</summary>
    TValue ConstInt32(int value);

    /// <summary>Creates a <see cref="long"/> constant value.</summary>
    TValue ConstInt64(long value);

    /// <summary>Creates a <see cref="bool"/> constant value.</summary>
    TValue ConstBool(bool value);

    /// <summary>Creates a string constant value.</summary>
    TValue ConstString(string value);

    /// <summary>Creates a fresh unknown value with explicit provenance.</summary>
    TValue FreshUnknown(TypeSig type, UnknownOrigin origin);

    /// <summary>Computes the least upper bound of two values.</summary>
    TValue Join(TValue a, TValue b);

    /// <summary>Computes a widening between two values for fixpoint convergence.</summary>
    TValue Widen(TValue prev, TValue next);

    /// <summary>Gets the static type associated with a value.</summary>
    TypeSig GetStaticType(TValue value);

    /// <summary>Gets the evaluation-stack category of a value.</summary>
    StackKind GetStackKind(TValue value);

    /// <summary>Gets the null-state classification for a value.</summary>
    Nullness GetNullness(TValue value);

    /// <summary>Refines a value toward a non-null assumption.</summary>
    TValue RefineNonNull(TValue value);

    /// <summary>Refines a value toward a null assumption.</summary>
    TValue RefineNull(TValue value);

    /// <summary>Tries to extract an <see cref="int"/> constant from a value.</summary>
    bool TryGetConstInt32(TValue value, out int c);

    /// <summary>Tries to extract a <see cref="bool"/> constant from a value.</summary>
    bool TryGetConstBool(TValue value, out bool b);

    /// <summary>Applies a unary operation.</summary>
    TValue ApplyUnary(UnaryOp op, TValue v);

    /// <summary>Applies a binary operation.</summary>
    TValue ApplyBinary(BinaryOp op, TValue a, TValue b);

    /// <summary>Converts a value to a target primitive representation.</summary>
    TValue Convert(ConvOp op, TValue v, bool checkedOverflow);

    /// <summary>Boxes a value to an object representation.</summary>
    TValue Box(TValue v, TypeSig boxedType);

    /// <summary>Unboxes a value to a target value type representation.</summary>
    TValue UnboxAny(TValue boxed, TypeSig targetType);
}
