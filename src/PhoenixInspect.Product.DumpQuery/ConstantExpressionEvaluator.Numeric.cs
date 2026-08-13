using System.Globalization;
using System.Numerics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The numeric tower: every C# numeric type with C# promotion, checked conversion, and IEEE-754 semantics,
/// plus the deterministic <c>System.Math</c> and <c>System.Numerics.BigInteger</c> surfaces.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    /// <summary>
    /// The numeric value domains the fold engine computes over. <c>nint</c>/<c>nuint</c> fold at 64 bits, matching
    /// the x64 processes the preview targets; the result states its kind so the assumption is visible.
    /// </summary>
    private enum NumericKind
    {
        SByte,
        Byte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        IntPtr,
        UIntPtr,
        Int128,
        UInt128,
        BigInteger,
        Single,
        Double,
        Decimal,
    }

    private static bool IsIntegral(NumericKind kind) => kind <= NumericKind.BigInteger;

    private static bool IsSigned(NumericKind kind) => kind is
        NumericKind.SByte or NumericKind.Int16 or NumericKind.Int32 or NumericKind.Int64 or
        NumericKind.IntPtr or NumericKind.Int128 or NumericKind.BigInteger;

    private static NumericKind NumericKindOf(Operand operand) => operand.Kind switch
    {
        OperandKind.Numeric => operand.NumericKind,
        // An enum participates in arithmetic as its exact underlying type, matching the C# conversions.
        OperandKind.Enum => operand.NumericKind,
        _ => NumericKind.Int32,
    };

    private static object BoxOf(Operand operand) => operand.Kind switch
    {
        OperandKind.Numeric => operand.Box!,
        OperandKind.Char => (int)operand.Char,
        OperandKind.Enum => BoxEnumBits(operand.NumericKind, operand.EnumBits),
        _ => operand.Int32,
    };

    /// <summary>Boxes an enum's raw bits as its exact underlying CLR type.</summary>
    private static object BoxEnumBits(NumericKind underlying, long bits) => underlying switch
    {
        NumericKind.SByte => (sbyte)bits,
        NumericKind.Byte => (byte)bits,
        NumericKind.Int16 => (short)bits,
        NumericKind.UInt16 => (ushort)bits,
        NumericKind.UInt32 => (uint)bits,
        NumericKind.Int64 => bits,
        NumericKind.UInt64 => unchecked((ulong)bits),
        _ => (int)bits,
    };

    private static BigInteger ToBigInteger(NumericKind kind, object box) => kind switch
    {
        NumericKind.SByte => (sbyte)box,
        NumericKind.Byte => (byte)box,
        NumericKind.Int16 => (short)box,
        NumericKind.UInt16 => (ushort)box,
        NumericKind.Int32 => (int)box,
        NumericKind.UInt32 => (uint)box,
        NumericKind.Int64 or NumericKind.IntPtr => (long)box,
        NumericKind.UInt64 or NumericKind.UIntPtr => (ulong)box,
        NumericKind.Int128 => (BigInteger)(Int128)box,
        NumericKind.UInt128 => (BigInteger)(UInt128)box,
        _ => (BigInteger)box,
    };

    private static double ToDoubleValue(NumericKind kind, object box) => kind switch
    {
        NumericKind.SByte => (sbyte)box,
        NumericKind.Byte => (byte)box,
        NumericKind.Int16 => (short)box,
        NumericKind.UInt16 => (ushort)box,
        NumericKind.Int32 => (int)box,
        NumericKind.UInt32 => (uint)box,
        NumericKind.Int64 or NumericKind.IntPtr => (long)box,
        NumericKind.UInt64 or NumericKind.UIntPtr => (ulong)box,
        NumericKind.Int128 => (double)(Int128)box,
        NumericKind.UInt128 => (double)(UInt128)box,
        NumericKind.BigInteger => (double)(BigInteger)box,
        NumericKind.Single => (float)box,
        NumericKind.Double => (double)box,
        _ => (double)(decimal)box,
    };

    private static float ToSingleValue(NumericKind kind, object box) => kind switch
    {
        NumericKind.SByte => (sbyte)box,
        NumericKind.Byte => (byte)box,
        NumericKind.Int16 => (short)box,
        NumericKind.UInt16 => (ushort)box,
        NumericKind.Int32 => (int)box,
        NumericKind.UInt32 => (uint)box,
        NumericKind.Int64 or NumericKind.IntPtr => (long)box,
        NumericKind.UInt64 or NumericKind.UIntPtr => (ulong)box,
        NumericKind.Int128 => (float)(Int128)box,
        NumericKind.UInt128 => (float)(UInt128)box,
        NumericKind.BigInteger => (float)(BigInteger)box,
        NumericKind.Single => (float)box,
        NumericKind.Double => (float)(double)box,
        _ => (float)(decimal)box,
    };

    private static (BigInteger Min, BigInteger Max) IntegralBounds(NumericKind kind) => kind switch
    {
        NumericKind.SByte => (sbyte.MinValue, sbyte.MaxValue),
        NumericKind.Byte => (byte.MinValue, byte.MaxValue),
        NumericKind.Int16 => (short.MinValue, short.MaxValue),
        NumericKind.UInt16 => (ushort.MinValue, ushort.MaxValue),
        NumericKind.Int32 => (int.MinValue, int.MaxValue),
        NumericKind.UInt32 => (uint.MinValue, uint.MaxValue),
        NumericKind.Int64 or NumericKind.IntPtr => (long.MinValue, long.MaxValue),
        NumericKind.UInt64 or NumericKind.UIntPtr => (ulong.MinValue, ulong.MaxValue),
        NumericKind.Int128 => (-(BigInteger.One << 127), (BigInteger.One << 127) - 1),
        _ => (BigInteger.Zero, (BigInteger.One << 128) - 1),
    };

    private static int IntegralBitWidth(NumericKind kind) => kind switch
    {
        NumericKind.SByte or NumericKind.Byte => 8,
        NumericKind.Int16 or NumericKind.UInt16 => 16,
        NumericKind.Int32 or NumericKind.UInt32 => 32,
        NumericKind.Int128 or NumericKind.UInt128 => 128,
        _ => 64,
    };

    private static object BoxFromBigInteger(NumericKind kind, BigInteger value) => kind switch
    {
        // Each arm boxes its exact CLR type; without the object casts the switch would unify every arm to
        // BigInteger through the implicit integral conversions.
        NumericKind.SByte => (object)(sbyte)value,
        NumericKind.Byte => (object)(byte)value,
        NumericKind.Int16 => (object)(short)value,
        NumericKind.UInt16 => (object)(ushort)value,
        NumericKind.Int32 => (object)(int)value,
        NumericKind.UInt32 => (object)(uint)value,
        NumericKind.Int64 or NumericKind.IntPtr => (object)(long)value,
        NumericKind.UInt64 or NumericKind.UIntPtr => (object)(ulong)value,
        NumericKind.Int128 => (object)(Int128)value,
        NumericKind.UInt128 => (object)(UInt128)value,
        NumericKind.Single => (object)(float)value,
        NumericKind.Double => (object)(double)value,
        NumericKind.Decimal => (object)(decimal)value,
        _ => value,
    };

    private static FoldOutcome FoldedInteger(NumericKind kind, BigInteger value) =>
        FoldOutcome.Folded(Operand.FromNumeric(kind, BoxFromBigInteger(kind, value)));

    /// <summary>Wraps a raw bit-operation result into the target kind with two's-complement semantics.</summary>
    private static BigInteger WrapBits(NumericKind kind, BigInteger value)
    {
        if (kind == NumericKind.BigInteger)
        {
            return value;
        }

        var width = IntegralBitWidth(kind);
        var mask = (BigInteger.One << width) - 1;
        var wrapped = value & mask;
        if (IsSigned(kind) && (wrapped >> (width - 1)) != 0)
        {
            wrapped -= BigInteger.One << width;
        }

        return wrapped;
    }

    /// <summary>Applies C# binary numeric promotion, extended to the wide and native-sized kinds.</summary>
    private static bool TryPromote(Operand left, Operand right, out NumericKind target, out FoldOutcome error)
    {
        var a = NumericKindOf(left);
        var b = NumericKindOf(right);
        error = default;
        target = NumericKind.Int32;

        if (a == NumericKind.Decimal || b == NumericKind.Decimal)
        {
            var other = a == NumericKind.Decimal ? b : a;
            if (other is NumericKind.Single or NumericKind.Double or NumericKind.BigInteger)
            {
                error = FoldOutcome.Error(
                    OperandTypeCode,
                    $"No operator combines decimal with {other} operands; convert one operand explicitly.");
                return false;
            }

            target = NumericKind.Decimal;
            return true;
        }

        if (a == NumericKind.Double || b == NumericKind.Double ||
            a == NumericKind.Single || b == NumericKind.Single)
        {
            var floating = a == NumericKind.Double || b == NumericKind.Double
                ? NumericKind.Double
                : NumericKind.Single;
            var other = a == floating ? b : a;
            if (other is NumericKind.BigInteger or NumericKind.Int128 or NumericKind.UInt128)
            {
                error = FoldOutcome.Error(
                    OperandTypeCode,
                    $"No operator combines {floating} with {other} operands; convert one operand explicitly.");
                return false;
            }

            target = floating;
            return true;
        }

        if (a == NumericKind.BigInteger || b == NumericKind.BigInteger)
        {
            target = NumericKind.BigInteger;
            return true;
        }

        if (a == NumericKind.UInt128 || b == NumericKind.UInt128)
        {
            return PromoteToUnsigned(left, right, NumericKind.UInt128, out target, out error);
        }

        if (a == NumericKind.Int128 || b == NumericKind.Int128)
        {
            target = NumericKind.Int128;
            return true;
        }

        if (a is NumericKind.UInt64 or NumericKind.UIntPtr || b is NumericKind.UInt64 or NumericKind.UIntPtr)
        {
            var unsignedTarget = a == NumericKind.UInt64 || b == NumericKind.UInt64
                ? NumericKind.UInt64
                : NumericKind.UIntPtr;
            return PromoteToUnsigned(left, right, unsignedTarget, out target, out error);
        }

        if (a == NumericKind.IntPtr || b == NumericKind.IntPtr)
        {
            var other = a == NumericKind.IntPtr ? b : a;
            target = other is NumericKind.Int64 or NumericKind.UInt32 ? NumericKind.Int64 : NumericKind.IntPtr;
            return true;
        }

        if (a == NumericKind.Int64 || b == NumericKind.Int64)
        {
            target = NumericKind.Int64;
            return true;
        }

        if (a == NumericKind.UInt32 || b == NumericKind.UInt32)
        {
            var other = a == NumericKind.UInt32 ? b : a;
            target = IsSigned(other) ? NumericKind.Int64 : NumericKind.UInt32;
            return true;
        }

        return true;
    }

    private static bool PromoteToUnsigned(
        Operand left,
        Operand right,
        NumericKind unsignedTarget,
        out NumericKind target,
        out FoldOutcome error)
    {
        // Mirrors the implicit constant expression conversion: a signed operand whose exact value is non-negative
        // converts to the unsigned kind; a negative value has no representation there.
        target = unsignedTarget;
        error = default;
        foreach (var operand in new[] { left, right })
        {
            var kind = NumericKindOf(operand);
            if (IsSigned(kind) && ToBigInteger(kind, BoxOf(operand)).Sign < 0)
            {
                error = FoldOutcome.Error(
                    OperandTypeCode,
                    $"A negative {kind} operand cannot combine with {unsignedTarget}.");
                return false;
            }
        }

        return true;
    }

    private static FoldOutcome ComputeBinaryNumeric(SyntaxKind kind, Operand left, Operand right)
    {
        if (kind is SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression or
            SyntaxKind.UnsignedRightShiftExpression)
        {
            return ComputeShift(kind, left, right);
        }

        if (!TryPromote(left, right, out var target, out var promoteError))
        {
            return promoteError;
        }

        return target switch
        {
            NumericKind.Single => ComputeSingle(kind, ToSingleValue(NumericKindOf(left), BoxOf(left)),
                ToSingleValue(NumericKindOf(right), BoxOf(right))),
            NumericKind.Double => ComputeDouble(kind, ToDoubleValue(NumericKindOf(left), BoxOf(left)),
                ToDoubleValue(NumericKindOf(right), BoxOf(right))),
            NumericKind.Decimal => ComputeDecimal(kind, left, right),
            _ => ComputeIntegral(kind, target,
                ToBigInteger(NumericKindOf(left), BoxOf(left)),
                ToBigInteger(NumericKindOf(right), BoxOf(right))),
        };
    }

    private static FoldOutcome ComputeIntegral(SyntaxKind kind, NumericKind target, BigInteger l, BigInteger r)
    {
        if (kind is SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression && r.IsZero)
        {
            return FoldOutcome.Error(DivisionByZeroCode, "The expression divides by zero.");
        }

        BigInteger result;
        var bitwise = false;
        switch (kind)
        {
            case SyntaxKind.AddExpression:
                result = l + r;
                break;
            case SyntaxKind.SubtractExpression:
                result = l - r;
                break;
            case SyntaxKind.MultiplyExpression:
                result = l * r;
                break;
            case SyntaxKind.DivideExpression:
                result = l / r;
                break;
            case SyntaxKind.ModuloExpression:
                result = l % r;
                break;
            case SyntaxKind.BitwiseAndExpression:
                result = l & r;
                bitwise = true;
                break;
            case SyntaxKind.BitwiseOrExpression:
                result = l | r;
                bitwise = true;
                break;
            default:
                result = l ^ r;
                bitwise = true;
                break;
        }

        if (bitwise || target == NumericKind.BigInteger)
        {
            return FoldedInteger(target, WrapBits(target, result));
        }

        var (min, max) = IntegralBounds(target);
        if (result < min || result > max)
        {
            return FoldOutcome.Error(
                OverflowCode,
                $"The constant expression overflows {target} under checked evaluation.");
        }

        return FoldedInteger(target, result);
    }

    private static FoldOutcome ComputeShift(SyntaxKind kind, Operand left, Operand right)
    {
        // C# shifts do not promote the operands jointly: the left operand is promoted alone and the count is Int32,
        // masked to the operand width.
        var target = NumericKindOf(left);
        if (target is NumericKind.SByte or NumericKind.Byte or NumericKind.Int16 or NumericKind.UInt16)
        {
            target = NumericKind.Int32;
        }

        if (!TryImplicitInt32(right, out var count))
        {
            return FoldOutcome.Error(OperandTypeCode, "A shift count must be an Int32 constant.");
        }

        if (target == NumericKind.BigInteger)
        {
            if (kind == SyntaxKind.UnsignedRightShiftExpression)
            {
                return FoldOutcome.Error(OperandTypeCode, "BigInteger does not define the unsigned right shift.");
            }

            var big = ToBigInteger(target, BoxOf(left));
            return FoldedInteger(target, kind == SyntaxKind.LeftShiftExpression ? big << count : big >> count);
        }

        var width = IntegralBitWidth(target);
        count &= width - 1;
        var value = ToBigInteger(NumericKindOf(left), BoxOf(left));
        BigInteger result;
        switch (kind)
        {
            case SyntaxKind.LeftShiftExpression:
                result = value << count;
                break;
            case SyntaxKind.RightShiftExpression:
                result = value >> count;
                break;
            default:
                // Logical shift: reinterpret as the unsigned bit pattern, shift, and decode.
                var mask = (BigInteger.One << width) - 1;
                result = (value & mask) >> count;
                break;
        }

        return FoldedInteger(target, WrapBits(target, result));
    }

    private static FoldOutcome ComputeSingle(SyntaxKind kind, float l, float r) => kind switch
    {
        SyntaxKind.AddExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, l + r)),
        SyntaxKind.SubtractExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, l - r)),
        SyntaxKind.MultiplyExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, l * r)),
        SyntaxKind.DivideExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, l / r)),
        SyntaxKind.ModuloExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, l % r)),
        _ => FoldOutcome.Error(OperandTypeCode, "Bitwise operators do not apply to floating-point operands."),
    };

    private static FoldOutcome ComputeDouble(SyntaxKind kind, double l, double r) => kind switch
    {
        SyntaxKind.AddExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, l + r)),
        SyntaxKind.SubtractExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, l - r)),
        SyntaxKind.MultiplyExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, l * r)),
        SyntaxKind.DivideExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, l / r)),
        SyntaxKind.ModuloExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, l % r)),
        _ => FoldOutcome.Error(OperandTypeCode, "Bitwise operators do not apply to floating-point operands."),
    };

    private static FoldOutcome ComputeDecimal(SyntaxKind kind, Operand left, Operand right)
    {
        try
        {
            var l = ToDecimalValue(left);
            var r = ToDecimalValue(right);
            return kind switch
            {
                SyntaxKind.AddExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, l + r)),
                SyntaxKind.SubtractExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, l - r)),
                SyntaxKind.MultiplyExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, l * r)),
                SyntaxKind.DivideExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, l / r)),
                SyntaxKind.ModuloExpression => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, l % r)),
                _ => FoldOutcome.Error(OperandTypeCode, "Bitwise operators do not apply to decimal operands."),
            };
        }
        catch (ArithmeticException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }

    private static decimal ToDecimalValue(Operand operand)
    {
        var kind = NumericKindOf(operand);
        var box = BoxOf(operand);
        return kind switch
        {
            NumericKind.Decimal => (decimal)box,
            NumericKind.Single => (decimal)(float)box,
            NumericKind.Double => (decimal)(double)box,
            _ => (decimal)ToBigInteger(kind, box),
        };
    }

    private static FoldOutcome ComputeNumericComparison(SyntaxKind kind, Operand left, Operand right)
    {
        if (!TryPromote(left, right, out var target, out var promoteError))
        {
            return promoteError;
        }

        bool result;
        switch (target)
        {
            case NumericKind.Single:
            case NumericKind.Double:
                // Native IEEE comparisons: every comparison with NaN is false except '!='.
                var dl = ToDoubleValue(NumericKindOf(left), BoxOf(left));
                var dr = ToDoubleValue(NumericKindOf(right), BoxOf(right));
                result = kind switch
                {
                    SyntaxKind.EqualsExpression => dl == dr,
                    SyntaxKind.NotEqualsExpression => dl != dr,
                    SyntaxKind.LessThanExpression => dl < dr,
                    SyntaxKind.LessThanOrEqualExpression => dl <= dr,
                    SyntaxKind.GreaterThanExpression => dl > dr,
                    _ => dl >= dr,
                };
                break;
            case NumericKind.Decimal:
                try
                {
                    result = CompareToOutcome(kind, ToDecimalValue(left).CompareTo(ToDecimalValue(right)));
                }
                catch (ArithmeticException exception)
                {
                    return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
                }

                break;
            default:
                result = CompareToOutcome(kind, ToBigInteger(NumericKindOf(left), BoxOf(left))
                    .CompareTo(ToBigInteger(NumericKindOf(right), BoxOf(right))));
                break;
        }

        return FoldOutcome.Folded(Operand.FromBoolean(result));
    }

    private static bool CompareToOutcome(SyntaxKind kind, int comparison) => kind switch
    {
        SyntaxKind.EqualsExpression => comparison == 0,
        SyntaxKind.NotEqualsExpression => comparison != 0,
        SyntaxKind.LessThanExpression => comparison < 0,
        SyntaxKind.LessThanOrEqualExpression => comparison <= 0,
        SyntaxKind.GreaterThanExpression => comparison > 0,
        _ => comparison >= 0,
    };

    private static FoldOutcome ComputeNumericUnary(SyntaxKind kind, Operand operand)
    {
        var numericKind = NumericKindOf(operand);
        var box = BoxOf(operand);
        switch (kind)
        {
            case SyntaxKind.UnaryPlusExpression:
                return numericKind is NumericKind.SByte or NumericKind.Byte or NumericKind.Int16 or NumericKind.UInt16
                    ? FoldedInteger(NumericKind.Int32, ToBigInteger(numericKind, box))
                    : FoldOutcome.Folded(Operand.FromNumeric(numericKind, box));
            case SyntaxKind.UnaryMinusExpression:
                switch (numericKind)
                {
                    case NumericKind.Single:
                        return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, -(float)box));
                    case NumericKind.Double:
                        return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, -(double)box));
                    case NumericKind.Decimal:
                        return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, -(decimal)box));
                    case NumericKind.UInt64 or NumericKind.UIntPtr or NumericKind.UInt128:
                        return FoldOutcome.Error(
                            OperandTypeCode,
                            $"Unary minus does not apply to {numericKind}.");
                    default:
                        // -uint promotes to long, exactly as C# defines; small kinds promote to int first.
                        var negated = -ToBigInteger(numericKind, box);
                        var target = numericKind switch
                        {
                            NumericKind.UInt32 => NumericKind.Int64,
                            NumericKind.SByte or NumericKind.Byte or NumericKind.Int16 or NumericKind.UInt16 =>
                                NumericKind.Int32,
                            _ => numericKind,
                        };
                        if (target != NumericKind.BigInteger)
                        {
                            var (min, max) = IntegralBounds(target);
                            if (negated < min || negated > max)
                            {
                                return FoldOutcome.Error(
                                    OverflowCode,
                                    $"The constant expression overflows {target} under checked evaluation.");
                            }
                        }

                        return FoldedInteger(target, negated);
                }

            case SyntaxKind.BitwiseNotExpression:
                if (numericKind is NumericKind.Single or NumericKind.Double or NumericKind.Decimal)
                {
                    return FoldOutcome.Error(OperandTypeCode, "Bitwise complement requires an integral operand.");
                }

                var promoted = numericKind is NumericKind.SByte or NumericKind.Byte or NumericKind.Int16
                    or NumericKind.UInt16
                    ? NumericKind.Int32
                    : numericKind;
                return FoldedInteger(promoted, WrapBits(promoted, ~ToBigInteger(numericKind, box)));
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    /// <summary>Accepts an operand where C# accepts an implicit conversion to <see cref="int"/>.</summary>
    private static bool TryImplicitInt32(Operand operand, out int value)
    {
        value = 0;
        if (operand.Kind == OperandKind.Int32)
        {
            value = operand.Int32;
            return true;
        }

        if (operand.Kind == OperandKind.Char)
        {
            value = operand.Char;
            return true;
        }

        if (operand.Kind == OperandKind.Numeric &&
            operand.NumericKind is NumericKind.SByte or NumericKind.Byte or NumericKind.Int16 or NumericKind.UInt16
                or NumericKind.Int32)
        {
            value = (int)ToBigInteger(operand.NumericKind, operand.Box!);
            return true;
        }

        return false;
    }

    private static string FormatNumeric(NumericKind kind, object box) =>
        ((IFormattable)box).ToString(format: null, CultureInfo.InvariantCulture);

    // ---- Conversions ----------------------------------------------------------------------------------------------

    private static FoldOutcome ConvertNumericOperand(Operand source, NumericKind target)
    {
        var sourceKind = NumericKindOf(source);
        var box = BoxOf(source);
        try
        {
            switch (target)
            {
                case NumericKind.Single:
                    return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, ToSingleValue(sourceKind, box)));
                case NumericKind.Double:
                    return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, ToDoubleValue(sourceKind, box)));
                case NumericKind.Decimal:
                    // The decimal conversions throw OverflowException for NaN, infinity, and out-of-range values.
                    return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, ToDecimalValue(source)));
                case NumericKind.BigInteger:
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        ToTruncatedBigInteger(sourceKind, box)));
                default:
                    var truncated = ToTruncatedBigInteger(sourceKind, box);
                    var (min, max) = IntegralBounds(target);
                    if (truncated < min || truncated > max)
                    {
                        return FoldOutcome.Error(
                            OverflowCode,
                            $"The value {FormatNumeric(sourceKind, box)} does not fit {target} under checked conversion.");
                    }

                    return FoldedInteger(target, truncated);
            }
        }
        catch (OverflowException exception)
        {
            return FoldOutcome.Error(OverflowCode, exception.Message);
        }
    }

    /// <summary>Truncates a numeric value toward zero into an integer, as C# explicit conversions do.</summary>
    private static BigInteger ToTruncatedBigInteger(NumericKind kind, object box)
    {
        switch (kind)
        {
            case NumericKind.Single:
            case NumericKind.Double:
                var value = kind == NumericKind.Single ? (float)box : (double)box;
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw new OverflowException(
                        $"The value {value.ToString(CultureInfo.InvariantCulture)} has no integral representation.");
                }

                return (BigInteger)value;
            case NumericKind.Decimal:
                return (BigInteger)decimal.Truncate((decimal)box);
            default:
                return ToBigInteger(kind, box);
        }
    }

    // ---- Cast target resolution -----------------------------------------------------------------------------------

    private enum CastTarget
    {
        Numeric,
        Char,
        Boolean,
        String,
    }

    private static bool TryResolveCastType(
        TypeSyntax type,
        out CastTarget target,
        out NumericKind numericKind,
        out bool nullable)
    {
        nullable = false;
        if (type is NullableTypeSyntax nullableType)
        {
            nullable = true;
            type = nullableType.ElementType;
        }

        target = CastTarget.Numeric;
        numericKind = NumericKind.Int32;
        switch (type)
        {
            case PredefinedTypeSyntax predefined:
                switch (predefined.Keyword.Kind())
                {
                    case SyntaxKind.SByteKeyword:
                        numericKind = NumericKind.SByte;
                        return true;
                    case SyntaxKind.ByteKeyword:
                        numericKind = NumericKind.Byte;
                        return true;
                    case SyntaxKind.ShortKeyword:
                        numericKind = NumericKind.Int16;
                        return true;
                    case SyntaxKind.UShortKeyword:
                        numericKind = NumericKind.UInt16;
                        return true;
                    case SyntaxKind.IntKeyword:
                        numericKind = NumericKind.Int32;
                        return true;
                    case SyntaxKind.UIntKeyword:
                        numericKind = NumericKind.UInt32;
                        return true;
                    case SyntaxKind.LongKeyword:
                        numericKind = NumericKind.Int64;
                        return true;
                    case SyntaxKind.ULongKeyword:
                        numericKind = NumericKind.UInt64;
                        return true;
                    case SyntaxKind.FloatKeyword:
                        numericKind = NumericKind.Single;
                        return true;
                    case SyntaxKind.DoubleKeyword:
                        numericKind = NumericKind.Double;
                        return true;
                    case SyntaxKind.DecimalKeyword:
                        numericKind = NumericKind.Decimal;
                        return true;
                    case SyntaxKind.CharKeyword:
                        target = CastTarget.Char;
                        return true;
                    case SyntaxKind.BoolKeyword:
                        target = CastTarget.Boolean;
                        return true;
                    case SyntaxKind.StringKeyword:
                        target = CastTarget.String;
                        return true;
                    default:
                        return false;
                }

            case IdentifierNameSyntax identifier:
                return TryMapCastTypeName(identifier.Identifier.ValueText, ref target, ref numericKind);
            case QualifiedNameSyntax qualified:
                var text = qualified.ToString();
                if (text is "System.Numerics.BigInteger" or "Numerics.BigInteger")
                {
                    numericKind = NumericKind.BigInteger;
                    return true;
                }

                return text.StartsWith("System.", StringComparison.Ordinal) &&
                    TryMapCastTypeName(text["System.".Length..], ref target, ref numericKind);
            default:
                return false;
        }
    }

    private static bool TryMapCastTypeName(string name, ref CastTarget target, ref NumericKind numericKind)
    {
        switch (name)
        {
            case "nint" or "IntPtr":
                numericKind = NumericKind.IntPtr;
                return true;
            case "nuint" or "UIntPtr":
                numericKind = NumericKind.UIntPtr;
                return true;
            case "Int128":
                numericKind = NumericKind.Int128;
                return true;
            case "UInt128":
                numericKind = NumericKind.UInt128;
                return true;
            case "BigInteger":
                numericKind = NumericKind.BigInteger;
                return true;
            case "SByte":
                numericKind = NumericKind.SByte;
                return true;
            case "Byte":
                numericKind = NumericKind.Byte;
                return true;
            case "Int16":
                numericKind = NumericKind.Int16;
                return true;
            case "UInt16":
                numericKind = NumericKind.UInt16;
                return true;
            case "Int32":
                numericKind = NumericKind.Int32;
                return true;
            case "UInt32":
                numericKind = NumericKind.UInt32;
                return true;
            case "Int64":
                numericKind = NumericKind.Int64;
                return true;
            case "UInt64":
                numericKind = NumericKind.UInt64;
                return true;
            case "Single":
                numericKind = NumericKind.Single;
                return true;
            case "Double":
                numericKind = NumericKind.Double;
                return true;
            case "Decimal":
                numericKind = NumericKind.Decimal;
                return true;
            case "Char":
                target = CastTarget.Char;
                return true;
            case "Boolean":
                target = CastTarget.Boolean;
                return true;
            case "String":
                target = CastTarget.String;
                return true;
            default:
                return false;
        }
    }

    private static FoldOutcome FoldCast(CastExpressionSyntax cast, FoldContext context)
    {
        if (!TryResolveCastType(cast.Type, out var target, out var numericKind, out var nullable))
        {
            // '(Action)(…)' and '(Func<int, int>)(x => x + 1)' are delegate conversions, not value casts.
            if (TryResolveDelegateTypeRef(cast.Type, context, out var delegateType))
            {
                return ConvertToDelegate(delegateType!, cast.Expression, context);
            }

            // '(Rune) 'a'' and '(Rune) 0x1F600' are the struct's explicit conversions, not reference casts.
            var castTypeName = cast.Type switch
            {
                IdentifierNameSyntax castIdentifier => castIdentifier.Identifier.ValueText,
                QualifiedNameSyntax castQualified when TryReadDottedName(castQualified, out var dotted) => dotted,
                _ => null,
            };
            if (castTypeName is "Rune" or "System.Text.Rune")
            {
                var runeSource = Fold(cast.Expression, context);
                return runeSource.Disposition == FoldDisposition.Folded
                    ? FoldRuneCast(runeSource.Operand)
                    : runeSource;
            }

            // A non-enum modeled type is a reference conversion: '(object) x' upcasts, '(string)(object) x'
            // downcasts against the runtime identity. Enum targets keep their numeric-conversion path below.
            if (TryResolveTypeRef(cast.Type, context, out var referenceTarget, out _) &&
                referenceTarget is { IsEnum: false })
            {
                return FoldReferenceCast(referenceTarget, cast.Expression, context);
            }

            return FoldEnumCast(cast, context);
        }

        var operand = Fold(cast.Expression, context);
        if (operand.Disposition != FoldDisposition.Folded)
        {
            return operand;
        }

        var value = operand.Operand;
        if (value.Kind == OperandKind.Null)
        {
            return nullable || target == CastTarget.String
                ? FoldOutcome.Folded(Operand.Null())
                : FoldOutcome.Error(
                    OperandTypeCode,
                    "A null value converts only to a nullable or reference target type.");
        }

        // A Rune converts outward through its scalar value: '(int) rune' and '(char) rune' are the struct's
        // explicit conversions, computed by the ordinary numeric conversion below.
        if (value is { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune })
        {
            value = Operand.FromInt32(((System.Text.Rune)value.Box!).Value);
        }

        switch (target)
        {
            case CastTarget.Char:
                if (value.Kind == OperandKind.Char)
                {
                    return FoldOutcome.Folded(value);
                }

                if (!value.IsNumeric)
                {
                    return FoldOutcome.Error(OperandTypeCode, "Only a numeric value converts to char.");
                }

                var toChar = ConvertNumericOperand(value, NumericKind.UInt16);
                return toChar.Disposition == FoldDisposition.Folded
                    ? FoldOutcome.Folded(Operand.FromChar((char)(ushort)toChar.Operand.Box!))
                    : toChar;
            case CastTarget.Boolean:
                return value.Kind == OperandKind.Boolean
                    ? FoldOutcome.Folded(value)
                    : FoldOutcome.Error(OperandTypeCode, "No conversion exists to bool.");
            case CastTarget.String:
                return value.Kind == OperandKind.String
                    ? FoldOutcome.Folded(value)
                    : FoldOutcome.Error(OperandTypeCode, "No conversion exists from this value to string.");
            default:
                if (!value.IsNumeric)
                {
                    return FoldOutcome.Error(
                        OperandTypeCode,
                        "Only a numeric, char, or enum value converts to a numeric type.");
                }

                return ConvertNumericOperand(value, numericKind);
        }
    }

    // ---- Type receivers and their static surfaces -----------------------------------------------------------------

    private enum TypeReceiverCategory
    {
        String,
        Char,
        Numeric,
        Math,
        Enumerable,
        KnownEnum,
        Temporal,
        BclValue,
        SystemEnum,
        SystemArray,
        Activator,
        SystemDelegate,
        CharUnicodeInfo,
    }

    private readonly record struct TypeReceiver(
        TypeReceiverCategory Category,
        NumericKind Numeric,
        string? EnumTypeFullName = null,
        TemporalKind Temporal = default,
        BclValueKind Value = default);

    /// <summary>
    /// The small closed set of BCL argument enums recognized without dump metadata, so comparison and split
    /// options work even before any module is read. The member values are fixed by the BCL contract.
    /// </summary>
    private static FoldOutcome DispatchKnownEnum(string enumTypeFullName, string member)
    {
        int? value = (enumTypeFullName, member) switch
        {
            ("System.StringComparison", "CurrentCulture") => 0,
            ("System.StringComparison", "CurrentCultureIgnoreCase") => 1,
            ("System.StringComparison", "InvariantCulture") => 2,
            ("System.StringComparison", "InvariantCultureIgnoreCase") => 3,
            ("System.StringComparison", "Ordinal") => 4,
            ("System.StringComparison", "OrdinalIgnoreCase") => 5,
            ("System.StringSplitOptions", "None") => 0,
            ("System.StringSplitOptions", "RemoveEmptyEntries") => 1,
            ("System.StringSplitOptions", "TrimEntries") => 2,
            ("System.DateTimeKind", "Unspecified") => 0,
            ("System.DateTimeKind", "Utc") => 1,
            ("System.DateTimeKind", "Local") => 2,
            ("System.DayOfWeek", "Sunday") => 0,
            ("System.DayOfWeek", "Monday") => 1,
            ("System.DayOfWeek", "Tuesday") => 2,
            ("System.DayOfWeek", "Wednesday") => 3,
            ("System.DayOfWeek", "Thursday") => 4,
            ("System.DayOfWeek", "Friday") => 5,
            ("System.DayOfWeek", "Saturday") => 6,
            (RegexOptionsFullName, "None") => 0,
            (RegexOptionsFullName, "IgnoreCase") => 1,
            (RegexOptionsFullName, "Multiline") => 2,
            (RegexOptionsFullName, "ExplicitCapture") => 4,
            (RegexOptionsFullName, "Compiled") => 8,
            (RegexOptionsFullName, "Singleline") => 16,
            (RegexOptionsFullName, "IgnorePatternWhitespace") => 32,
            (RegexOptionsFullName, "RightToLeft") => 64,
            (RegexOptionsFullName, "ECMAScript") => 256,
            (RegexOptionsFullName, "CultureInvariant") => 512,
            (RegexOptionsFullName, "NonBacktracking") => 1024,
            (MemberTypesFullName, "Constructor") => 1,
            (MemberTypesFullName, "Event") => 2,
            (MemberTypesFullName, "Field") => 4,
            (MemberTypesFullName, "Method") => 8,
            (MemberTypesFullName, "Property") => 16,
            (MemberTypesFullName, "TypeInfo") => 32,
            (MemberTypesFullName, "Custom") => 64,
            (MemberTypesFullName, "NestedType") => 128,
            (MemberTypesFullName, "All") => 191,
            (UnicodeCategoryFullName, "UppercaseLetter") => 0,
            (UnicodeCategoryFullName, "LowercaseLetter") => 1,
            (UnicodeCategoryFullName, "TitlecaseLetter") => 2,
            (UnicodeCategoryFullName, "ModifierLetter") => 3,
            (UnicodeCategoryFullName, "OtherLetter") => 4,
            (UnicodeCategoryFullName, "NonSpacingMark") => 5,
            (UnicodeCategoryFullName, "SpacingCombiningMark") => 6,
            (UnicodeCategoryFullName, "EnclosingMark") => 7,
            (UnicodeCategoryFullName, "DecimalDigitNumber") => 8,
            (UnicodeCategoryFullName, "LetterNumber") => 9,
            (UnicodeCategoryFullName, "OtherNumber") => 10,
            (UnicodeCategoryFullName, "SpaceSeparator") => 11,
            (UnicodeCategoryFullName, "LineSeparator") => 12,
            (UnicodeCategoryFullName, "ParagraphSeparator") => 13,
            (UnicodeCategoryFullName, "Control") => 14,
            (UnicodeCategoryFullName, "Format") => 15,
            (UnicodeCategoryFullName, "Surrogate") => 16,
            (UnicodeCategoryFullName, "PrivateUse") => 17,
            (UnicodeCategoryFullName, "ConnectorPunctuation") => 18,
            (UnicodeCategoryFullName, "DashPunctuation") => 19,
            (UnicodeCategoryFullName, "OpenPunctuation") => 20,
            (UnicodeCategoryFullName, "ClosePunctuation") => 21,
            (UnicodeCategoryFullName, "InitialQuotePunctuation") => 22,
            (UnicodeCategoryFullName, "FinalQuotePunctuation") => 23,
            (UnicodeCategoryFullName, "OtherPunctuation") => 24,
            (UnicodeCategoryFullName, "MathSymbol") => 25,
            (UnicodeCategoryFullName, "CurrencySymbol") => 26,
            (UnicodeCategoryFullName, "ModifierSymbol") => 27,
            (UnicodeCategoryFullName, "OtherSymbol") => 28,
            (UnicodeCategoryFullName, "OtherNotAssigned") => 29,
            _ => null,
        };
        return value is { } resolved
            ? FoldOutcome.Folded(Operand.FromEnum(resolved, enumTypeFullName, member))
            : MemberUnsupported(member);
    }

    private static bool TryReadTypeReceiver(ExpressionSyntax expression, out TypeReceiver receiver)
    {
        receiver = default;
        switch (expression)
        {
            case PredefinedTypeSyntax predefined:
                switch (predefined.Keyword.Kind())
                {
                    case SyntaxKind.StringKeyword:
                        receiver = new TypeReceiver(TypeReceiverCategory.String, default);
                        return true;
                    case SyntaxKind.CharKeyword:
                        receiver = new TypeReceiver(TypeReceiverCategory.Char, default);
                        return true;
                    default:
                        if (TryResolveCastType(predefined, out var castTarget, out var kind, out _) &&
                            castTarget == CastTarget.Numeric)
                        {
                            receiver = new TypeReceiver(TypeReceiverCategory.Numeric, kind);
                            return true;
                        }

                        return false;
                }

            case IdentifierNameSyntax identifier:
                return TryMapReceiverName(identifier.Identifier.ValueText, out receiver);
            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: IdentifierNameSyntax { Identifier.ValueText: "System" },
                Name: IdentifierNameSyntax nested,
            }:
                return TryMapReceiverName(nested.Identifier.ValueText, out receiver);
            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: MemberAccessExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "System" },
                    Name.Identifier.ValueText: "Numerics",
                },
                Name.Identifier.ValueText: "BigInteger",
            }:
                receiver = new TypeReceiver(TypeReceiverCategory.Numeric, NumericKind.BigInteger);
                return true;
            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: MemberAccessExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "System" },
                    Name.Identifier.ValueText: "Linq",
                },
                Name.Identifier.ValueText: "Enumerable",
            }:
                receiver = new TypeReceiver(TypeReceiverCategory.Enumerable, default);
                return true;
            case MemberAccessExpressionSyntax dotted
                when TryReadQualifiedName(dotted, out var dottedParts, out var dottedAlias) && dottedAlias is null:
                // The System.Text types resolve by their full dotted spelling, mirroring the shortcuts above.
                switch (string.Join('.', dottedParts))
                {
                    case "System.Text.Encoding":
                        receiver = new TypeReceiver(
                            TypeReceiverCategory.BclValue, default, Value: BclValueKind.Encoding);
                        return true;
                    case "System.Text.RegularExpressions.Regex":
                        receiver = new TypeReceiver(
                            TypeReceiverCategory.BclValue, default, Value: BclValueKind.Regex);
                        return true;
                    case "System.Text.RegularExpressions.RegexOptions":
                        receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, RegexOptionsFullName);
                        return true;
                    default:
                        return false;
                }

            default:
                return false;
        }
    }

    private static bool TryMapReceiverName(string name, out TypeReceiver receiver)
    {
        receiver = default;
        switch (name)
        {
            case "String":
                receiver = new TypeReceiver(TypeReceiverCategory.String, default);
                return true;
            case "Char":
                receiver = new TypeReceiver(TypeReceiverCategory.Char, default);
                return true;
            case "Math":
                receiver = new TypeReceiver(TypeReceiverCategory.Math, default);
                return true;
            case "Enumerable":
                receiver = new TypeReceiver(TypeReceiverCategory.Enumerable, default);
                return true;
            case "StringComparison":
                receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, "System.StringComparison");
                return true;
            case "StringSplitOptions":
                receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, "System.StringSplitOptions");
                return true;
            case "DateTimeKind":
                receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, "System.DateTimeKind");
                return true;
            case "DayOfWeek":
                receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, "System.DayOfWeek");
                return true;
            case "DateTime":
                receiver = new TypeReceiver(TypeReceiverCategory.Temporal, default, Temporal: TemporalKind.DateTime);
                return true;
            case "DateTimeOffset":
                receiver = new TypeReceiver(
                    TypeReceiverCategory.Temporal, default, Temporal: TemporalKind.DateTimeOffset);
                return true;
            case "TimeSpan":
                receiver = new TypeReceiver(TypeReceiverCategory.Temporal, default, Temporal: TemporalKind.TimeSpan);
                return true;
            case "DateOnly":
                receiver = new TypeReceiver(TypeReceiverCategory.Temporal, default, Temporal: TemporalKind.DateOnly);
                return true;
            case "TimeOnly":
                receiver = new TypeReceiver(TypeReceiverCategory.Temporal, default, Temporal: TemporalKind.TimeOnly);
                return true;
            case "Guid":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Guid);
                return true;
            case "Version":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Version);
                return true;
            case "Encoding":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Encoding);
                return true;
            case "Regex":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Regex);
                return true;
            case "RegexOptions":
                receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, RegexOptionsFullName);
                return true;
            case "Enum":
                receiver = new TypeReceiver(TypeReceiverCategory.SystemEnum, default);
                return true;
            case "Array":
                receiver = new TypeReceiver(TypeReceiverCategory.SystemArray, default);
                return true;
            case "Activator":
                receiver = new TypeReceiver(TypeReceiverCategory.Activator, default);
                return true;
            case "Delegate" or "MulticastDelegate":
                receiver = new TypeReceiver(TypeReceiverCategory.SystemDelegate, default);
                return true;
            case "Rune":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Rune);
                return true;
            case "CharUnicodeInfo":
                receiver = new TypeReceiver(TypeReceiverCategory.CharUnicodeInfo, default);
                return true;
            case "UnicodeCategory":
                receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, UnicodeCategoryFullName);
                return true;
            case "MemberTypes":
                receiver = new TypeReceiver(TypeReceiverCategory.KnownEnum, default, MemberTypesFullName);
                return true;
            default:
                var target = CastTarget.Numeric;
                var kind = NumericKind.Int32;
                if (TryMapCastTypeName(name, ref target, ref kind) && target == CastTarget.Numeric)
                {
                    receiver = new TypeReceiver(TypeReceiverCategory.Numeric, kind);
                    return true;
                }

                return false;
        }
    }

    private static FoldOutcome DispatchTypeStatic(TypeReceiver receiver, string member)
    {
        switch (receiver.Category)
        {
            case TypeReceiverCategory.String:
                return member == "Empty"
                    ? FoldOutcome.Folded(Operand.FromString(string.Empty))
                    : MemberUnsupported(member);
            case TypeReceiverCategory.Char:
                return member switch
                {
                    "MaxValue" => FoldOutcome.Folded(Operand.FromChar(char.MaxValue)),
                    "MinValue" => FoldOutcome.Folded(Operand.FromChar(char.MinValue)),
                    _ => MemberUnsupported(member),
                };
            case TypeReceiverCategory.Math:
                return member switch
                {
                    "PI" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.PI)),
                    "E" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.E)),
                    "Tau" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.Tau)),
                    _ => MemberUnsupported(member),
                };
            case TypeReceiverCategory.KnownEnum:
                return DispatchKnownEnum(receiver.EnumTypeFullName!, member);
            case TypeReceiverCategory.Enumerable:
                return MemberUnsupported($"Enumerable.{member}");
            case TypeReceiverCategory.Temporal:
                return DispatchTemporalStaticProperty(receiver.Temporal, member);
            case TypeReceiverCategory.BclValue:
                return DispatchBclValueStaticProperty(receiver.Value, member);
            case TypeReceiverCategory.SystemEnum:
                return MemberUnsupported($"Enum.{member}");
            case TypeReceiverCategory.SystemArray:
                return member == "MaxLength"
                    ? FoldOutcome.Folded(Operand.FromInt32(ArrayMaxLength))
                    : MemberUnsupported($"Array.{member}");
            case TypeReceiverCategory.Activator:
                return MemberUnsupported($"Activator.{member}");
            case TypeReceiverCategory.SystemDelegate:
                return MemberUnsupported($"Delegate.{member}");
            case TypeReceiverCategory.CharUnicodeInfo:
                return MemberUnsupported($"CharUnicodeInfo.{member}");
            default:
                return DispatchNumericTypeStatic(receiver.Numeric, member);
        }
    }

    private static FoldOutcome DispatchNumericTypeStatic(NumericKind kind, string member)
    {
        object? value = (kind, member) switch
        {
            (NumericKind.SByte, "MaxValue") => sbyte.MaxValue,
            (NumericKind.SByte, "MinValue") => sbyte.MinValue,
            (NumericKind.Byte, "MaxValue") => byte.MaxValue,
            (NumericKind.Byte, "MinValue") => byte.MinValue,
            (NumericKind.Int16, "MaxValue") => short.MaxValue,
            (NumericKind.Int16, "MinValue") => short.MinValue,
            (NumericKind.UInt16, "MaxValue") => ushort.MaxValue,
            (NumericKind.UInt16, "MinValue") => ushort.MinValue,
            (NumericKind.Int32, "MaxValue") => int.MaxValue,
            (NumericKind.Int32, "MinValue") => int.MinValue,
            (NumericKind.UInt32, "MaxValue") => uint.MaxValue,
            (NumericKind.UInt32, "MinValue") => uint.MinValue,
            (NumericKind.Int64, "MaxValue") => long.MaxValue,
            (NumericKind.Int64, "MinValue") => long.MinValue,
            (NumericKind.UInt64, "MaxValue") => ulong.MaxValue,
            (NumericKind.UInt64, "MinValue") => ulong.MinValue,
            (NumericKind.IntPtr, "MaxValue") => (long)nint.MaxValue,
            (NumericKind.IntPtr, "MinValue") => (long)nint.MinValue,
            (NumericKind.UIntPtr, "MaxValue") => (ulong)nuint.MaxValue,
            (NumericKind.UIntPtr, "MinValue") => (ulong)nuint.MinValue,
            (NumericKind.Int128, "MaxValue") => Int128.MaxValue,
            (NumericKind.Int128, "MinValue") => Int128.MinValue,
            (NumericKind.UInt128, "MaxValue") => UInt128.MaxValue,
            (NumericKind.UInt128, "MinValue") => UInt128.MinValue,
            (NumericKind.Single, "MaxValue") => float.MaxValue,
            (NumericKind.Single, "MinValue") => float.MinValue,
            (NumericKind.Single, "Epsilon") => float.Epsilon,
            (NumericKind.Single, "NaN") => float.NaN,
            (NumericKind.Single, "PositiveInfinity") => float.PositiveInfinity,
            (NumericKind.Single, "NegativeInfinity") => float.NegativeInfinity,
            (NumericKind.Single, "Pi") => float.Pi,
            (NumericKind.Single, "E") => float.E,
            (NumericKind.Single, "Tau") => float.Tau,
            (NumericKind.Double, "MaxValue") => double.MaxValue,
            (NumericKind.Double, "MinValue") => double.MinValue,
            (NumericKind.Double, "Epsilon") => double.Epsilon,
            (NumericKind.Double, "NaN") => double.NaN,
            (NumericKind.Double, "PositiveInfinity") => double.PositiveInfinity,
            (NumericKind.Double, "NegativeInfinity") => double.NegativeInfinity,
            (NumericKind.Double, "Pi") => double.Pi,
            (NumericKind.Double, "E") => double.E,
            (NumericKind.Double, "Tau") => double.Tau,
            (NumericKind.Decimal, "MaxValue") => decimal.MaxValue,
            (NumericKind.Decimal, "MinValue") => decimal.MinValue,
            (NumericKind.Decimal, "Zero") => decimal.Zero,
            (NumericKind.Decimal, "One") => decimal.One,
            (NumericKind.Decimal, "MinusOne") => decimal.MinusOne,
            (NumericKind.BigInteger, "Zero") => BigInteger.Zero,
            (NumericKind.BigInteger, "One") => BigInteger.One,
            (NumericKind.BigInteger, "MinusOne") => BigInteger.MinusOne,
            _ => null,
        };
        return value is null
            ? MemberUnsupported(member)
            : FoldOutcome.Folded(Operand.FromNumeric(kind, value));
    }

    // ---- System.Math ----------------------------------------------------------------------------------------------

    private static bool TryArgDouble(Operand operand, out double value)
    {
        // Mirrors the implicit conversions to double: every numeric kind except decimal and the wide integrals.
        value = 0;
        if (!operand.IsNumeric)
        {
            return false;
        }

        var kind = NumericKindOf(operand);
        if (kind is NumericKind.Decimal or NumericKind.BigInteger or NumericKind.Int128 or NumericKind.UInt128)
        {
            return false;
        }

        value = ToDoubleValue(kind, BoxOf(operand));
        return true;
    }

    private static FoldOutcome DispatchMath(string name, List<Operand> arguments)
    {
        try
        {
            return DispatchMathCore(name, arguments);
        }
        catch (ArithmeticException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }

    private static FoldOutcome DispatchMathCore(string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "Abs" when arguments is [{ IsNumeric: true } single]:
                return MathAbs(single);
            case "Sign" when arguments is [{ IsNumeric: true } single]:
                return MathSign(single);
            case "Min" or "Max" when arguments is [{ IsNumeric: true } left, { IsNumeric: true } right]:
                return MathMinMax(name == "Min", left, right);
            case "Clamp" when arguments is
                [{ IsNumeric: true } value, { IsNumeric: true } low, { IsNumeric: true } high]:
                var lowFirst = MathMinMax(wantMin: false, value, low);
                if (lowFirst.Disposition != FoldDisposition.Folded)
                {
                    return lowFirst;
                }

                var boundsOrder = ComputeNumericComparison(SyntaxKind.GreaterThanExpression, low, high);
                if (boundsOrder.Disposition == FoldDisposition.Folded && boundsOrder.Operand.Boolean)
                {
                    return FoldOutcome.Error(
                        "System.ArgumentException",
                        "Math.Clamp requires the minimum to be less than or equal to the maximum.");
                }

                return MathMinMax(wantMin: true, lowFirst.Operand, high);
            case "Floor" or "Ceiling" or "Truncate" or "Round" when arguments.Count >= 1 &&
                NumericKindOf(arguments[0]) == NumericKind.Decimal:
                return MathDecimalRounding(name, arguments);
            case "Floor" when arguments is [{ } x] && TryArgDouble(x, out var floor):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.Floor(floor)));
            case "Ceiling" when arguments is [{ } x] && TryArgDouble(x, out var ceiling):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.Ceiling(ceiling)));
            case "Truncate" when arguments is [{ } x] && TryArgDouble(x, out var truncate):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.Truncate(truncate)));
            case "Round" when arguments is [{ } x] && TryArgDouble(x, out var round):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.Round(round)));
            case "Round" when arguments is [{ } x, { } d] &&
                TryArgDouble(x, out var roundValue) && TryImplicitInt32(d, out var digits):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.Round(roundValue, digits)));
            case "ILogB" when arguments is [{ } x] && TryArgDouble(x, out var ilogb):
                return FoldOutcome.Folded(Operand.FromInt32(Math.ILogB(ilogb)));
            case "ScaleB" when arguments is [{ } x, { } n] &&
                TryArgDouble(x, out var scale) && TryImplicitInt32(n, out var power):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.ScaleB(scale, power)));
            case "ReciprocalEstimate" or "ReciprocalSqrtEstimate":
                return FoldOutcome.Error(
                    MemberUnsupportedCode,
                    $"'Math.{name}' is a hardware-dependent estimate and is not deterministic across machines.");
            case "Sqrt" or "Cbrt" or "Exp" or "Log" or "Log2" or "Log10" or "Sin" or "Cos" or "Tan" or "Asin"
                or "Acos" or "Atan" or "Sinh" or "Cosh" or "Tanh" or "Asinh" or "Acosh" or "Atanh"
                or "BitIncrement" or "BitDecrement" when arguments is [{ } x] && TryArgDouble(x, out var unary):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, name switch
                {
                    "Sqrt" => Math.Sqrt(unary),
                    "Cbrt" => Math.Cbrt(unary),
                    "Exp" => Math.Exp(unary),
                    "Log" => Math.Log(unary),
                    "Log2" => Math.Log2(unary),
                    "Log10" => Math.Log10(unary),
                    "Sin" => Math.Sin(unary),
                    "Cos" => Math.Cos(unary),
                    "Tan" => Math.Tan(unary),
                    "Asin" => Math.Asin(unary),
                    "Acos" => Math.Acos(unary),
                    "Atan" => Math.Atan(unary),
                    "Sinh" => Math.Sinh(unary),
                    "Cosh" => Math.Cosh(unary),
                    "Tanh" => Math.Tanh(unary),
                    "Asinh" => Math.Asinh(unary),
                    "Acosh" => Math.Acosh(unary),
                    "Atanh" => Math.Atanh(unary),
                    "BitIncrement" => Math.BitIncrement(unary),
                    _ => Math.BitDecrement(unary),
                }));
            case "Pow" or "Atan2" or "Log" or "IEEERemainder" or "CopySign" when arguments is [{ } a, { } b] &&
                TryArgDouble(a, out var first) && TryArgDouble(b, out var second):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, name switch
                {
                    "Pow" => Math.Pow(first, second),
                    "Atan2" => Math.Atan2(first, second),
                    "Log" => Math.Log(first, second),
                    "IEEERemainder" => Math.IEEERemainder(first, second),
                    _ => Math.CopySign(first, second),
                }));
            case "FusedMultiplyAdd" when arguments is [{ } a, { } b, { } c] &&
                TryArgDouble(a, out var x1) && TryArgDouble(b, out var x2) && TryArgDouble(c, out var x3):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.FusedMultiplyAdd(x1, x2, x3)));
            default:
                return MemberUnsupported($"Math.{name}");
        }
    }

    private static FoldOutcome MathDecimalRounding(string name, List<Operand> arguments)
    {
        var value = ToDecimalValue(arguments[0]);
        switch (name)
        {
            case "Floor" when arguments.Count == 1:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, decimal.Floor(value)));
            case "Ceiling" when arguments.Count == 1:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, decimal.Ceiling(value)));
            case "Truncate" when arguments.Count == 1:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, decimal.Truncate(value)));
            case "Round" when arguments.Count == 1:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, Math.Round(value)));
            case "Round" when arguments.Count == 2 && TryImplicitInt32(arguments[1], out var digits):
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, Math.Round(value, digits)));
            default:
                return MemberUnsupported($"Math.{name}");
        }
    }

    private static FoldOutcome MathAbs(Operand operand)
    {
        var kind = NumericKindOf(operand);
        var box = BoxOf(operand);
        switch (kind)
        {
            case NumericKind.Single:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, Math.Abs((float)box)));
            case NumericKind.Double:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, Math.Abs((double)box)));
            case NumericKind.Decimal:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, Math.Abs((decimal)box)));
            case NumericKind.BigInteger:
                return FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.BigInteger,
                    BigInteger.Abs((BigInteger)box)));
            default:
                if (!IsSigned(kind))
                {
                    return FoldOutcome.Folded(Operand.FromNumeric(kind, box));
                }

                var promoted = kind is NumericKind.SByte or NumericKind.Int16 ? NumericKind.Int32 : kind;
                var magnitude = BigInteger.Abs(ToBigInteger(kind, box));
                var (_, max) = IntegralBounds(promoted);
                return magnitude > max
                    ? FoldOutcome.Error(
                        OverflowCode,
                        $"Math.Abs of {promoted}.MinValue overflows under checked evaluation.")
                    : FoldedInteger(promoted, magnitude);
        }
    }

    private static FoldOutcome MathSign(Operand operand)
    {
        var kind = NumericKindOf(operand);
        var box = BoxOf(operand);
        return kind switch
        {
            // Math.Sign(NaN) throws ArithmeticException in running code; the caller maps it to a typed stop.
            NumericKind.Single => FoldOutcome.Folded(Operand.FromInt32(Math.Sign((float)box))),
            NumericKind.Double => FoldOutcome.Folded(Operand.FromInt32(Math.Sign((double)box))),
            NumericKind.Decimal => FoldOutcome.Folded(Operand.FromInt32(Math.Sign((decimal)box))),
            _ => FoldOutcome.Folded(Operand.FromInt32(ToBigInteger(kind, box).Sign)),
        };
    }

    private static FoldOutcome MathMinMax(bool wantMin, Operand left, Operand right)
    {
        if (!TryPromote(left, right, out var target, out var error))
        {
            return error;
        }

        switch (target)
        {
            case NumericKind.Single:
                var fl = ToSingleValue(NumericKindOf(left), BoxOf(left));
                var fr = ToSingleValue(NumericKindOf(right), BoxOf(right));
                return FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Single,
                    wantMin ? Math.Min(fl, fr) : Math.Max(fl, fr)));
            case NumericKind.Double:
                var dl = ToDoubleValue(NumericKindOf(left), BoxOf(left));
                var dr = ToDoubleValue(NumericKindOf(right), BoxOf(right));
                return FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Double,
                    wantMin ? Math.Min(dl, dr) : Math.Max(dl, dr)));
            case NumericKind.Decimal:
                var ml = ToDecimalValue(left);
                var mr = ToDecimalValue(right);
                return FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Decimal,
                    wantMin ? Math.Min(ml, mr) : Math.Max(ml, mr)));
            default:
                var bl = ToBigInteger(NumericKindOf(left), BoxOf(left));
                var br = ToBigInteger(NumericKindOf(right), BoxOf(right));
                return FoldedInteger(target, wantMin ? BigInteger.Min(bl, br) : BigInteger.Max(bl, br));
        }
    }

    // ---- Numeric-type static methods (double/float classification, BigInteger factories) ---------------------------

    private static FoldOutcome DispatchNumericTypeMethod(NumericKind kind, string name, List<Operand> arguments)
    {
        if (kind == NumericKind.BigInteger)
        {
            return DispatchBigInteger(name, arguments);
        }

        if (kind is NumericKind.Single or NumericKind.Double &&
            arguments is [{ } single] && TryArgDouble(single, out var value))
        {
            var probe = kind == NumericKind.Single ? ToSingleValue(NumericKindOf(single), BoxOf(single)) : 0f;
            bool? result = (kind, name) switch
            {
                (NumericKind.Double, "IsNaN") => double.IsNaN(value),
                (NumericKind.Double, "IsInfinity") => double.IsInfinity(value),
                (NumericKind.Double, "IsPositiveInfinity") => double.IsPositiveInfinity(value),
                (NumericKind.Double, "IsNegativeInfinity") => double.IsNegativeInfinity(value),
                (NumericKind.Double, "IsFinite") => double.IsFinite(value),
                (NumericKind.Double, "IsNegative") => double.IsNegative(value),
                (NumericKind.Single, "IsNaN") => float.IsNaN(probe),
                (NumericKind.Single, "IsInfinity") => float.IsInfinity(probe),
                (NumericKind.Single, "IsPositiveInfinity") => float.IsPositiveInfinity(probe),
                (NumericKind.Single, "IsNegativeInfinity") => float.IsNegativeInfinity(probe),
                (NumericKind.Single, "IsFinite") => float.IsFinite(probe),
                (NumericKind.Single, "IsNegative") => float.IsNegative(probe),
                _ => null,
            };
            if (result is { } predicate)
            {
                return FoldOutcome.Folded(Operand.FromBoolean(predicate));
            }
        }

        return MemberUnsupported(name);
    }

    private static FoldOutcome DispatchBigInteger(string name, List<Operand> arguments)
    {
        try
        {
            switch (name)
            {
                case "Parse" when arguments is [{ Kind: OperandKind.String } text]:
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        BigInteger.Parse(text.String!, CultureInfo.InvariantCulture)));
                case "Pow" when arguments is [{ IsNumeric: true } value, { } exponent] &&
                    TryImplicitInt32(exponent, out var power):
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        BigInteger.Pow(ToTruncatedBigInteger(NumericKindOf(value), BoxOf(value)), power)));
                case "Abs" when arguments is [{ IsNumeric: true } value]:
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        BigInteger.Abs(ToTruncatedBigInteger(NumericKindOf(value), BoxOf(value)))));
                case "Negate" when arguments is [{ IsNumeric: true } value]:
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        -ToTruncatedBigInteger(NumericKindOf(value), BoxOf(value))));
                case "Min" or "Max" when arguments is [{ IsNumeric: true } left, { IsNumeric: true } right]:
                    var l = ToTruncatedBigInteger(NumericKindOf(left), BoxOf(left));
                    var r = ToTruncatedBigInteger(NumericKindOf(right), BoxOf(right));
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        name == "Min" ? BigInteger.Min(l, r) : BigInteger.Max(l, r)));
                case "GreatestCommonDivisor" when arguments is
                    [{ IsNumeric: true } left, { IsNumeric: true } right]:
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        BigInteger.GreatestCommonDivisor(
                            ToTruncatedBigInteger(NumericKindOf(left), BoxOf(left)),
                            ToTruncatedBigInteger(NumericKindOf(right), BoxOf(right)))));
                case "ModPow" when arguments is
                    [{ IsNumeric: true } value, { IsNumeric: true } exponent, { IsNumeric: true } modulus]:
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.BigInteger,
                        BigInteger.ModPow(
                            ToTruncatedBigInteger(NumericKindOf(value), BoxOf(value)),
                            ToTruncatedBigInteger(NumericKindOf(exponent), BoxOf(exponent)),
                            ToTruncatedBigInteger(NumericKindOf(modulus), BoxOf(modulus)))));
                default:
                    return MemberUnsupported($"BigInteger.{name}");
            }
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
        catch (ArithmeticException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }

    /// <summary>Formats a numeric operand with an explicit format string, always under the invariant culture.</summary>
    private static FoldOutcome NumericToString(Operand operand, string? format)
    {
        try
        {
            return FoldOutcome.Folded(Operand.FromString(
                ((IFormattable)BoxOf(operand)).ToString(format, CultureInfo.InvariantCulture)));
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }
}
