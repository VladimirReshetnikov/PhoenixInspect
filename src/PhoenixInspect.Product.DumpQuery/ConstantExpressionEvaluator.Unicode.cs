using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The Unicode surface: the complete deterministic <see cref="char"/> static and instance API,
/// <see cref="CharUnicodeInfo"/>, and <see cref="Rune"/> values. Every answer reads the pinned analysis
/// runtime's Unicode tables — fixed data, not culture state — so classifications, categories, numeric values,
/// and case mappings fold exactly; the culture-sensitive case mappings remain typed stops naming their
/// invariant counterparts.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    private const string UnicodeCategoryFullName = "System.Globalization.UnicodeCategory";

    private static FoldOutcome UnicodeCategoryOperand(UnicodeCategory category) => FoldOutcome.Folded(
        Operand.FromEnum((int)category, UnicodeCategoryFullName, category.ToString()));

    // ---- The char static surface ------------------------------------------------------------------------------------

    /// <summary>
    /// The complete deterministic <see cref="char"/> static surface: every classification in both the
    /// <c>(char)</c> and <c>(string, index)</c> spellings, the ASCII family, categories and numeric values,
    /// UTF-32 conversions, surrogate pairing, parsing, and invariant case mapping.
    /// </summary>
    private static FoldOutcome DispatchStaticChar(string name, List<Operand> arguments)
    {
        try
        {
            switch (name, arguments)
            {
                case ("ToUpperInvariant", [{ Kind: OperandKind.Char } upper]):
                    return FoldOutcome.Folded(Operand.FromChar(char.ToUpperInvariant(upper.Char)));
                case ("ToLowerInvariant", [{ Kind: OperandKind.Char } lower]):
                    return FoldOutcome.Folded(Operand.FromChar(char.ToLowerInvariant(lower.Char)));
                case ("ToUpper" or "ToLower", _):
                    return CultureSensitive(name, $"char.{name}Invariant");
                case ("ToString", [{ Kind: OperandKind.Char } text]):
                    return FoldOutcome.Folded(Operand.FromString(text.Char.ToString()));
                case ("Parse", [{ Kind: OperandKind.String } parsed]):
                    return FoldOutcome.Folded(Operand.FromChar(char.Parse(parsed.String!)));
                case ("TryParse", _):
                    return FoldOutcome.Error(
                        MemberUnsupportedCode,
                        "char.TryParse writes through an out parameter, which expressions cannot observe; "
                        + "use char.Parse, whose failure is a typed stop.");
                case ("GetNumericValue", [{ Kind: OperandKind.Char } numeric]):
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.Double,
                        char.GetNumericValue(numeric.Char)));
                case ("GetNumericValue", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var numericIndex):
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.Double,
                        char.GetNumericValue(text.String!, numericIndex)));
                case ("GetUnicodeCategory", [{ Kind: OperandKind.Char } categorized]):
                    return UnicodeCategoryOperand(char.GetUnicodeCategory(categorized.Char));
                case ("GetUnicodeCategory", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var categoryIndex):
                    return UnicodeCategoryOperand(char.GetUnicodeCategory(text.String!, categoryIndex));
                case ("ConvertFromUtf32", [{ } scalar]) when TryImplicitInt32(scalar, out var utf32):
                    return FoldOutcome.Folded(Operand.FromString(char.ConvertFromUtf32(utf32)));
                case ("ConvertToUtf32", [{ Kind: OperandKind.Char } high, { Kind: OperandKind.Char } low]):
                    return FoldOutcome.Folded(Operand.FromInt32(char.ConvertToUtf32(high.Char, low.Char)));
                case ("ConvertToUtf32", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var utf32Index):
                    return FoldOutcome.Folded(Operand.FromInt32(char.ConvertToUtf32(text.String!, utf32Index)));
                case ("IsSurrogatePair", [{ Kind: OperandKind.Char } high, { Kind: OperandKind.Char } low]):
                    return FoldOutcome.Folded(Operand.FromBoolean(char.IsSurrogatePair(high.Char, low.Char)));
                case ("IsSurrogatePair", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var pairIndex):
                    return FoldOutcome.Folded(Operand.FromBoolean(char.IsSurrogatePair(text.String!, pairIndex)));
                case ("IsBetween", [
                    { Kind: OperandKind.Char } probe,
                    { Kind: OperandKind.Char } minimum,
                    { Kind: OperandKind.Char } maximum]):
                    return FoldOutcome.Folded(Operand.FromBoolean(
                        char.IsBetween(probe.Char, minimum.Char, maximum.Char)));
            }

            if (CharClassifierOf(name) is not { } classifier)
            {
                return MemberUnsupported($"char.{name}");
            }

            return arguments switch
            {
                [{ Kind: OperandKind.Char } single] =>
                    FoldOutcome.Folded(Operand.FromBoolean(classifier(single.Char))),
                [{ Kind: OperandKind.String } text, { } index] when TryImplicitInt32(index, out var at) =>
                    at >= 0 && at < text.String!.Length
                        ? FoldOutcome.Folded(Operand.FromBoolean(classifier(text.String![at])))
                        : FoldOutcome.Error(
                            ArgumentOutOfRangeCode,
                            $"Index {at.ToString(CultureInfo.InvariantCulture)} is outside the string of length "
                            + $"{text.String!.Length.ToString(CultureInfo.InvariantCulture)}."),
                _ => MemberUnsupported($"char.{name}"),
            };
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error("System.FormatException", exception.Message);
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    /// <summary>The classification predicates, shared by the <c>(char)</c> and <c>(string, index)</c> spellings.</summary>
    private static Func<char, bool>? CharClassifierOf(string name) => name switch
    {
        "IsDigit" => char.IsDigit,
        "IsLetter" => char.IsLetter,
        "IsLetterOrDigit" => char.IsLetterOrDigit,
        "IsWhiteSpace" => char.IsWhiteSpace,
        "IsUpper" => char.IsUpper,
        "IsLower" => char.IsLower,
        "IsPunctuation" => char.IsPunctuation,
        "IsSymbol" => char.IsSymbol,
        "IsSeparator" => char.IsSeparator,
        "IsControl" => char.IsControl,
        "IsNumber" => char.IsNumber,
        "IsSurrogate" => char.IsSurrogate,
        "IsHighSurrogate" => char.IsHighSurrogate,
        "IsLowSurrogate" => char.IsLowSurrogate,
        "IsAscii" => char.IsAscii,
        "IsAsciiDigit" => char.IsAsciiDigit,
        "IsAsciiLetter" => char.IsAsciiLetter,
        "IsAsciiLetterOrDigit" => char.IsAsciiLetterOrDigit,
        "IsAsciiLetterLower" => char.IsAsciiLetterLower,
        "IsAsciiLetterUpper" => char.IsAsciiLetterUpper,
        "IsAsciiHexDigit" => char.IsAsciiHexDigit,
        "IsAsciiHexDigitLower" => char.IsAsciiHexDigitLower,
        "IsAsciiHexDigitUpper" => char.IsAsciiHexDigitUpper,
        _ => null,
    };

    /// <summary>The char instance surface: invariant rendering, ordinal comparison, and exact equality.</summary>
    private static FoldOutcome DispatchCharInstanceMethod(char receiver, string name, List<Operand> arguments) =>
        (name, arguments) switch
        {
            ("ToString", []) => FoldOutcome.Folded(Operand.FromString(receiver.ToString())),
            ("CompareTo", [{ Kind: OperandKind.Char } other]) =>
                FoldOutcome.Folded(Operand.FromInt32(receiver.CompareTo(other.Char))),
            ("Equals", [{ Kind: OperandKind.Char } other]) =>
                FoldOutcome.Folded(Operand.FromBoolean(receiver.Equals(other.Char))),
            ("Equals", [{ Kind: OperandKind.Null }]) => FoldOutcome.Folded(Operand.FromBoolean(false)),
            ("GetHashCode", []) => FoldOutcome.Folded(Operand.FromInt32(receiver.GetHashCode())),
            _ => MemberUnsupported($"char.{name}"),
        };

    // ---- System.Globalization.CharUnicodeInfo -----------------------------------------------------------------------

    /// <summary>
    /// The <see cref="CharUnicodeInfo"/> statics: categories, numeric values, and digit values, each in its
    /// <c>(char)</c> and <c>(string, index)</c> spellings — pure reads of the runtime's Unicode tables.
    /// </summary>
    private static FoldOutcome DispatchCharUnicodeInfo(string name, List<Operand> arguments)
    {
        try
        {
            switch (name, arguments)
            {
                case ("GetUnicodeCategory", [{ Kind: OperandKind.Char } value]):
                    return UnicodeCategoryOperand(CharUnicodeInfo.GetUnicodeCategory(value.Char));
                case ("GetUnicodeCategory", [{ Kind: OperandKind.Int32 } scalar]):
                    return UnicodeCategoryOperand(CharUnicodeInfo.GetUnicodeCategory(scalar.Int32));
                case ("GetUnicodeCategory", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var categoryIndex):
                    return UnicodeCategoryOperand(
                        CharUnicodeInfo.GetUnicodeCategory(text.String!, categoryIndex));
                case ("GetNumericValue", [{ Kind: OperandKind.Char } value]):
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.Double,
                        CharUnicodeInfo.GetNumericValue(value.Char)));
                case ("GetNumericValue", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var numericIndex):
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.Double,
                        CharUnicodeInfo.GetNumericValue(text.String!, numericIndex)));
                case ("GetDecimalDigitValue", [{ Kind: OperandKind.Char } value]):
                    return FoldOutcome.Folded(Operand.FromInt32(
                        CharUnicodeInfo.GetDecimalDigitValue(value.Char)));
                case ("GetDecimalDigitValue", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var decimalIndex):
                    return FoldOutcome.Folded(Operand.FromInt32(
                        CharUnicodeInfo.GetDecimalDigitValue(text.String!, decimalIndex)));
                case ("GetDigitValue", [{ Kind: OperandKind.Char } value]):
                    return FoldOutcome.Folded(Operand.FromInt32(CharUnicodeInfo.GetDigitValue(value.Char)));
                case ("GetDigitValue", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var digitIndex):
                    return FoldOutcome.Folded(Operand.FromInt32(
                        CharUnicodeInfo.GetDigitValue(text.String!, digitIndex)));
                default:
                    return MemberUnsupported($"CharUnicodeInfo.{name}");
            }
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    // ---- System.Text.Rune -------------------------------------------------------------------------------------------

    private static FoldOutcome RuneOperand(Rune value) =>
        FoldOutcome.Folded(Operand.FromBclValue(BclValueKind.Rune, value));

    /// <summary>Constructs a <see cref="Rune"/> from a char, a scalar value, or a surrogate pair.</summary>
    private static FoldOutcome ConstructRune(List<Operand> arguments)
    {
        try
        {
            switch (arguments)
            {
                case []:
                    return RuneOperand(default);
                case [{ Kind: OperandKind.Char } single]:
                    return RuneOperand(new Rune(single.Char));
                case [{ Kind: OperandKind.Char } high, { Kind: OperandKind.Char } low]:
                    return RuneOperand(new Rune(high.Char, low.Char));
                case [{ } scalar] when TryImplicitInt32(scalar, out var value):
                    return RuneOperand(new Rune(value));
                default:
                    return FoldOutcome.Error(
                        MemberUnsupportedCode,
                        "This Rune constructor shape is not admitted; use a char, a scalar value, or a "
                        + "surrogate pair.");
            }
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    /// <summary>The <see cref="Rune"/> statics: scalar factories, classifications, and invariant case mapping.</summary>
    private static FoldOutcome DispatchRuneStaticMethod(string name, List<Operand> arguments)
    {
        try
        {
            switch (name, arguments)
            {
                case ("GetRuneAt", [{ Kind: OperandKind.String } text, { } index])
                    when TryImplicitInt32(index, out var runeIndex):
                    return RuneOperand(Rune.GetRuneAt(text.String!, runeIndex));
                case ("IsValid", [{ } scalar]) when TryImplicitInt32(scalar, out var probed):
                    return FoldOutcome.Folded(Operand.FromBoolean(Rune.IsValid(probed)));
                case ("GetNumericValue", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } rune]):
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.Double,
                        Rune.GetNumericValue((Rune)rune.Box!)));
                case ("GetUnicodeCategory", [
                    { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } rune]):
                    return UnicodeCategoryOperand(Rune.GetUnicodeCategory((Rune)rune.Box!));
                case ("ToUpperInvariant", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } rune]):
                    return RuneOperand(Rune.ToUpperInvariant((Rune)rune.Box!));
                case ("ToLowerInvariant", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } rune]):
                    return RuneOperand(Rune.ToLowerInvariant((Rune)rune.Box!));
                case ("ToUpper" or "ToLower", _):
                    return CultureSensitive($"Rune.{name}", $"Rune.{name}Invariant");
                case ("TryCreate" or "TryGetRuneAt" or "DecodeFromUtf16" or "DecodeFromUtf8"
                    or "DecodeLastFromUtf16" or "DecodeLastFromUtf8", _):
                    return FoldOutcome.Error(
                        MemberUnsupportedCode,
                        $"Rune.{name} writes through an out parameter, which expressions cannot observe; use "
                        + "the constructor or GetRuneAt, whose failures are typed stops.");
                default:
                    if (RuneClassifierOf(name) is { } classifier && arguments is
                        [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } classified])
                    {
                        return FoldOutcome.Folded(Operand.FromBoolean(classifier((Rune)classified.Box!)));
                    }

                    return MemberUnsupported($"Rune.{name}");
            }
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    private static Func<Rune, bool>? RuneClassifierOf(string name) => name switch
    {
        "IsDigit" => Rune.IsDigit,
        "IsLetter" => Rune.IsLetter,
        "IsLetterOrDigit" => Rune.IsLetterOrDigit,
        "IsWhiteSpace" => Rune.IsWhiteSpace,
        "IsUpper" => Rune.IsUpper,
        "IsLower" => Rune.IsLower,
        "IsPunctuation" => Rune.IsPunctuation,
        "IsSymbol" => Rune.IsSymbol,
        "IsSeparator" => Rune.IsSeparator,
        "IsControl" => Rune.IsControl,
        "IsNumber" => Rune.IsNumber,
        _ => null,
    };

    /// <summary>The read-only properties of one <see cref="Rune"/> value.</summary>
    private static FoldOutcome DispatchRuneProperty(Rune rune, string member) => member switch
    {
        "Value" => FoldOutcome.Folded(Operand.FromInt32(rune.Value)),
        "IsAscii" => FoldOutcome.Folded(Operand.FromBoolean(rune.IsAscii)),
        "IsBmp" => FoldOutcome.Folded(Operand.FromBoolean(rune.IsBmp)),
        "Plane" => FoldOutcome.Folded(Operand.FromInt32(rune.Plane)),
        "Utf16SequenceLength" => FoldOutcome.Folded(Operand.FromInt32(rune.Utf16SequenceLength)),
        "Utf8SequenceLength" => FoldOutcome.Folded(Operand.FromInt32(rune.Utf8SequenceLength)),
        _ => MemberUnsupported($"Rune.{member}"),
    };

    /// <summary>The instance methods of one <see cref="Rune"/> value.</summary>
    private static FoldOutcome DispatchRuneMethod(Rune rune, string name, List<Operand> arguments) =>
        (name, arguments) switch
        {
            ("ToString", []) => FoldOutcome.Folded(Operand.FromString(rune.ToString())),
            ("CompareTo", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } other]) =>
                FoldOutcome.Folded(Operand.FromInt32(rune.CompareTo((Rune)other.Box!))),
            ("Equals", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } other]) =>
                FoldOutcome.Folded(Operand.FromBoolean(rune.Equals((Rune)other.Box!))),
            ("Equals", [{ Kind: OperandKind.Null }]) => FoldOutcome.Folded(Operand.FromBoolean(false)),
            ("GetHashCode", []) => FoldOutcome.Folded(Operand.FromInt32(rune.GetHashCode())),
            _ => MemberUnsupported($"Rune.{name}"),
        };

    /// <summary>Folds <c>text.EnumerateRunes()</c> into the exact sequence of scalar values.</summary>
    private static FoldOutcome CreateRuneSequence(string text) => CreateSequence(new SequencePayload(
        [.. text.EnumerateRunes().Select(static rune => Operand.FromBclValue(BclValueKind.Rune, rune))],
        OperandKind.BclValue,
        default,
        "Rune"));

    /// <summary>
    /// The explicit <see cref="Rune"/> conversions: <c>(Rune) 'a'</c> and <c>(Rune) 0x1F600</c> construct with
    /// the constructor's own validation, and a Rune value round-trips through <c>object</c> unchanged.
    /// </summary>
    private static FoldOutcome FoldRuneCast(Operand value) => value switch
    {
        { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Rune } => FoldOutcome.Folded(value),
        { Kind: OperandKind.Char } => ConstructRune([value]),
        { IsNumeric: true } when value.Kind != OperandKind.Enum => ConstructRune([value]),
        { Kind: OperandKind.Null } => FoldOutcome.Error(
            "System.NullReferenceException",
            "Object reference not set to an instance of an object."),
        _ => FoldOutcome.Error(OperandTypeCode, "Only a char or a scalar value converts to Rune."),
    };
}
