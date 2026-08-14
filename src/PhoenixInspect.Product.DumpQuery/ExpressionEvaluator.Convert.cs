using System.Collections.Immutable;
using System.Globalization;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The <see cref="Convert"/> surface and <see cref="DBNull"/>. Every conversion calls the BCL's own method over
/// the operand's exact boxed value, so the semantics are Convert's — banker's rounding from floating to
/// integral, null converting to zero, overflow and invalid-cast refusals — not a re-implementation. String
/// parsing and rendering run under the invariant culture, matching the evaluator's numeric-text convention;
/// the base-N spellings, Base64, and hexadecimal codecs are culture-free by definition.
/// </content>
public static partial class ExpressionEvaluator
{
    private const string TypeCodeFullName = "System.TypeCode";

    private static FoldOutcome TypeCodeOperand(TypeCode code) => FoldOutcome.Folded(Operand.FromEnum(
        (int)code,
        TypeCodeFullName,
        code.ToString()));

    /// <summary>The <see cref="TypeCode"/> of one folded operand, exactly as <see cref="Convert.GetTypeCode"/>.</summary>
    private static TypeCode TypeCodeOf(Operand value) => value.Kind switch
    {
        OperandKind.Null => TypeCode.Empty,
        OperandKind.Boolean => TypeCode.Boolean,
        OperandKind.Char => TypeCode.Char,
        OperandKind.String => TypeCode.String,
        OperandKind.Int32 => TypeCode.Int32,
        OperandKind.Enum => NumericTypeCodeOf(value.NumericKind),
        OperandKind.Numeric => NumericTypeCodeOf(value.NumericKind),
        OperandKind.Temporal when value.TemporalKind == TemporalKind.DateTime => TypeCode.DateTime,
        OperandKind.BclValue when value.BclValueKind == BclValueKind.DBNull => TypeCode.DBNull,
        _ => TypeCode.Object,
    };

    private static TypeCode NumericTypeCodeOf(NumericKind kind) => kind switch
    {
        NumericKind.SByte => TypeCode.SByte,
        NumericKind.Byte => TypeCode.Byte,
        NumericKind.Int16 => TypeCode.Int16,
        NumericKind.UInt16 => TypeCode.UInt16,
        NumericKind.Int32 => TypeCode.Int32,
        NumericKind.UInt32 => TypeCode.UInt32,
        NumericKind.Int64 => TypeCode.Int64,
        NumericKind.UInt64 => TypeCode.UInt64,
        NumericKind.Single => TypeCode.Single,
        NumericKind.Double => TypeCode.Double,
        NumericKind.Decimal => TypeCode.Decimal,
        _ => TypeCode.Object,
    };

    // ---- The IConvertible bridge ------------------------------------------------------------------------------------

    /// <summary>
    /// Boxes one operand as the exact value <see cref="Convert"/> understands. The wide numerics have no
    /// <see cref="IConvertible"/> implementation in the BCL either, so they stop with that fact named.
    /// </summary>
    private static bool TryBoxConvertible(Operand value, out object? box, out FoldOutcome stop)
    {
        stop = default;
        switch (value.Kind)
        {
            case OperandKind.Null:
                box = null;
                return true;
            case OperandKind.Int32:
                box = value.Int32;
                return true;
            case OperandKind.Boolean:
                box = value.Boolean;
                return true;
            case OperandKind.Char:
                box = value.Char;
                return true;
            case OperandKind.String:
                box = value.String;
                return true;
            case OperandKind.Enum:
                box = BoxEnumBits(value.NumericKind, value.EnumBits);
                return true;
            case OperandKind.Numeric when value.NumericKind is not
                (NumericKind.BigInteger or NumericKind.Int128 or NumericKind.UInt128 or
                    NumericKind.IntPtr or NumericKind.UIntPtr):
                box = value.Box;
                return true;
            case OperandKind.Temporal when value.TemporalKind == TemporalKind.DateTime:
                box = value.Box;
                return true;
            case OperandKind.BclValue when value.BclValueKind == BclValueKind.DBNull:
                box = DBNull.Value;
                return true;
            default:
                box = null;
                stop = FoldOutcome.Error(
                    OperandTypeCode,
                    "Convert admits the IConvertible domains: the fixed-size numerics, decimal, bool, char, "
                    + "string, enum values, DateTime, DBNull, and null.");
                return false;
        }
    }

    /// <summary>One <c>Convert.ToX(value)</c> call over the boxed operand, under the invariant culture.</summary>
    private static FoldOutcome ConvertViaBcl(string name, Operand value)
    {
        if (!TryBoxConvertible(value, out var box, out var stop))
        {
            return stop;
        }

        try
        {
            var invariant = CultureInfo.InvariantCulture;
            return name switch
            {
                "ToBoolean" => FoldOutcome.Folded(Operand.FromBoolean(Convert.ToBoolean(box, invariant))),
                "ToChar" => FoldOutcome.Folded(Operand.FromChar(Convert.ToChar(box, invariant))),
                "ToSByte" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.SByte, Convert.ToSByte(box, invariant))),
                "ToByte" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Byte, Convert.ToByte(box, invariant))),
                "ToInt16" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Int16, Convert.ToInt16(box, invariant))),
                "ToUInt16" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.UInt16, Convert.ToUInt16(box, invariant))),
                "ToInt32" => FoldOutcome.Folded(Operand.FromInt32(Convert.ToInt32(box, invariant))),
                "ToUInt32" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.UInt32, Convert.ToUInt32(box, invariant))),
                "ToInt64" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Int64, Convert.ToInt64(box, invariant))),
                "ToUInt64" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.UInt64, Convert.ToUInt64(box, invariant))),
                "ToSingle" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Single, Convert.ToSingle(box, invariant))),
                "ToDouble" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Double, Convert.ToDouble(box, invariant))),
                "ToDecimal" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Decimal, Convert.ToDecimal(box, invariant))),
                "ToString" => FoldOutcome.Folded(Operand.FromString(Convert.ToString(box, invariant)
                    ?? string.Empty)),
                "ToDateTime" when value is
                    { Kind: OperandKind.Temporal, TemporalKind: TemporalKind.DateTime } =>
                    FoldOutcome.Folded(value),
                "ToDateTime" => CultureSensitive(
                    "Convert.ToDateTime over this value",
                    "the DateTime constructors or DateTime.ParseExact with an invariant format"),
                _ => MemberUnsupported($"Convert.{name}"),
            };
        }
        catch (OverflowException exception)
        {
            return FoldOutcome.Error("System.OverflowException", exception.Message);
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error("System.FormatException", exception.Message);
        }
        catch (InvalidCastException exception)
        {
            return FoldOutcome.Error("System.InvalidCastException", exception.Message);
        }
    }

    /// <summary>The base-2/8/10/16 string-to-integral spellings, culture-free by definition.</summary>
    private static FoldOutcome ConvertFromBase(string name, string? text, int fromBase)
    {
        try
        {
            return name switch
            {
                "ToSByte" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.SByte, Convert.ToSByte(text, fromBase))),
                "ToByte" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Byte, Convert.ToByte(text, fromBase))),
                "ToInt16" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Int16, Convert.ToInt16(text, fromBase))),
                "ToUInt16" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.UInt16, Convert.ToUInt16(text, fromBase))),
                "ToInt32" => FoldOutcome.Folded(Operand.FromInt32(Convert.ToInt32(text, fromBase))),
                "ToUInt32" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.UInt32, Convert.ToUInt32(text, fromBase))),
                "ToInt64" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Int64, Convert.ToInt64(text, fromBase))),
                "ToUInt64" => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.UInt64, Convert.ToUInt64(text, fromBase))),
                _ => MemberUnsupported($"Convert.{name} with a base argument"),
            };
        }
        catch (Exception exception) when (exception is
            FormatException or OverflowException or ArgumentException)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }

    // ---- The Convert static surface ---------------------------------------------------------------------------------

    /// <summary>The <c>Convert.DBNull</c> field and the class's absence of other static state.</summary>
    private static FoldOutcome DispatchConvertStaticProperty(string member) => member == "DBNull"
        ? FoldOutcome.Folded(Operand.FromBclValue(BclValueKind.DBNull, DBNull.Value))
        : MemberUnsupported($"Convert.{member}");

    private static FoldOutcome DispatchConvert(string name, List<Operand> arguments)
    {
        switch (name, arguments)
        {
            case ("IsDBNull", [{ } probed]):
                return FoldOutcome.Folded(Operand.FromBoolean(
                    probed is { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.DBNull }));
            case ("GetTypeCode", [{ } coded]):
                return TypeCodeOperand(TypeCodeOf(coded));
            case ("ToBase64String", [{ } bytes]):
                return TryReadByteSequence(bytes, out var encoded)
                    ? FoldOutcome.Folded(Operand.FromString(Convert.ToBase64String(encoded)))
                    : ByteArrayRequired("Convert.ToBase64String");
            case ("ToHexString", [{ } bytes]):
                return TryReadByteSequence(bytes, out var hexed)
                    ? FoldOutcome.Folded(Operand.FromString(Convert.ToHexString(hexed)))
                    : ByteArrayRequired("Convert.ToHexString");
            case ("ToHexStringLower", [{ } bytes]):
                return TryReadByteSequence(bytes, out var lowered)
                    ? FoldOutcome.Folded(Operand.FromString(Convert.ToHexStringLower(lowered)))
                    : ByteArrayRequired("Convert.ToHexStringLower");
            case ("FromBase64String", [{ Kind: OperandKind.String } base64]):
                try
                {
                    return CreateByteSequence(Convert.FromBase64String(base64.String!));
                }
                catch (FormatException exception)
                {
                    return FoldOutcome.Error("System.FormatException", exception.Message);
                }

            case ("FromHexString", [{ Kind: OperandKind.String } hex]):
                try
                {
                    return CreateByteSequence(Convert.FromHexString(hex.String!));
                }
                catch (FormatException exception)
                {
                    return FoldOutcome.Error("System.FormatException", exception.Message);
                }

            case ("ToString", [{ } value, { } toBase]) when value.IsNumeric && TryImplicitInt32(toBase, out var target):
                try
                {
                    return NumericKindOf(value) == NumericKind.Int64
                        ? FoldOutcome.Folded(Operand.FromString(Convert.ToString((long)BoxOf(value), target)))
                        : FoldOutcome.Folded(Operand.FromString(Convert.ToString(value.AsInt32, target)));
                }
                catch (ArgumentException exception)
                {
                    return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
                }

            case ("ToSByte" or "ToByte" or "ToInt16" or "ToUInt16" or "ToInt32" or "ToUInt32"
                or "ToInt64" or "ToUInt64",
                [{ Kind: OperandKind.String or OperandKind.Null } text, { } fromBase])
                when TryImplicitInt32(fromBase, out var numberBase):
                return ConvertFromBase(name, text.String, numberBase);
            case ("ToBoolean" or "ToChar" or "ToSByte" or "ToByte" or "ToInt16" or "ToUInt16" or "ToInt32"
                or "ToUInt32" or "ToInt64" or "ToUInt64" or "ToSingle" or "ToDouble" or "ToDecimal"
                or "ToString" or "ToDateTime", [{ } value]):
                return ConvertViaBcl(name, value);
            case ("ChangeType", [{ } value, { Kind: OperandKind.Type } target]):
                return ChangeTypeViaBcl(value, (TypeRef)target.Box!);
            case ("ChangeType", [_, { Kind: OperandKind.Null }]):
                return FoldOutcome.Error("System.ArgumentNullException", "ChangeType requires a conversion type.");
            default:
                return MemberUnsupported($"Convert.{name}");
        }
    }

    /// <summary><see cref="Convert.ChangeType(object, Type)"/> over the modeled target types.</summary>
    private static FoldOutcome ChangeTypeViaBcl(Operand value, TypeRef target)
    {
        var conversion = target.FullName switch
        {
            "System.Boolean" => "ToBoolean",
            "System.Char" => "ToChar",
            "System.SByte" => "ToSByte",
            "System.Byte" => "ToByte",
            "System.Int16" => "ToInt16",
            "System.UInt16" => "ToUInt16",
            "System.Int32" => "ToInt32",
            "System.UInt32" => "ToUInt32",
            "System.Int64" => "ToInt64",
            "System.UInt64" => "ToUInt64",
            "System.Single" => "ToSingle",
            "System.Double" => "ToDouble",
            "System.Decimal" => "ToDecimal",
            "System.String" => "ToString",
            "System.DateTime" => "ToDateTime",
            _ => null,
        };
        return conversion is null
            ? FoldOutcome.Error(
                OperandTypeCode,
                $"Convert.ChangeType converts to the IConvertible primitives; '{target.CSharpName}' is not one.")
            : ConvertViaBcl(conversion, value);
    }

    // ---- System.DBNull ----------------------------------------------------------------------------------------------

    /// <summary>The <see cref="DBNull"/> instance surface: its empty text and its own type code.</summary>
    private static FoldOutcome DispatchDBNullMethod(string name, List<Operand> arguments) =>
        (name, arguments) switch
        {
            ("ToString", []) => FoldOutcome.Folded(Operand.FromString(string.Empty)),
            ("GetTypeCode", []) => TypeCodeOperand(TypeCode.DBNull),
            ("Equals", [{ } other]) => FoldOutcome.Folded(Operand.FromBoolean(
                other is { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.DBNull })),
            _ => MemberUnsupported($"DBNull.{name}"),
        };
}
