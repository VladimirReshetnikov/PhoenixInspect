using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Further deterministic BCL value types: <see cref="Guid"/> and <see cref="Version"/>. Their parsing grammars are
/// fixed and culture-independent, their comparisons total and deterministic, and their text forms invariant, so
/// they fold with exact BCL semantics. The parts that are not evidence — <see cref="Guid.NewGuid"/> — are typed
/// stops, never evaluated. The text-family kinds — Encoding and the Regex family — route to their own partial.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    /// <summary>The supported deterministic BCL value domains outside the other operand kinds.</summary>
    internal enum BclValueKind
    {
        /// <summary>A <see cref="System.Guid"/> value.</summary>
        Guid,

        /// <summary>A <see cref="System.Version"/> value.</summary>
        Version,

        /// <summary>A <see cref="System.Text.Encoding"/> singleton.</summary>
        Encoding,

        /// <summary>A <see cref="System.Text.RegularExpressions.Regex"/> value.</summary>
        Regex,

        /// <summary>A <see cref="System.Text.RegularExpressions.Match"/> value.</summary>
        Match,

        /// <summary>A <see cref="System.Text.RegularExpressions.Group"/> value.</summary>
        Group,

        /// <summary>A <see cref="System.Text.RegularExpressions.Capture"/> value.</summary>
        Capture,

        /// <summary>A fully evaluated <see cref="System.Text.RegularExpressions.MatchCollection"/>.</summary>
        MatchCollection,

        /// <summary>A <see cref="System.Text.RegularExpressions.GroupCollection"/> value.</summary>
        GroupCollection,

        /// <summary>A <see cref="System.Text.RegularExpressions.CaptureCollection"/> value.</summary>
        CaptureCollection,

        /// <summary>A <see cref="System.Reflection.MethodInfo"/> of the modeled reflection universe.</summary>
        MethodInfo,

        /// <summary>A <see cref="System.Reflection.ConstructorInfo"/> of the modeled reflection universe.</summary>
        ConstructorInfo,

        /// <summary>A <see cref="System.Reflection.PropertyInfo"/> of the modeled reflection universe.</summary>
        PropertyInfo,

        /// <summary>A <see cref="System.Reflection.FieldInfo"/> of the modeled reflection universe.</summary>
        FieldInfo,

        /// <summary>A <see cref="System.Reflection.ParameterInfo"/> of the modeled reflection universe.</summary>
        ParameterInfo,
    }

    /// <summary>The namespace each BCL value kind's runtime type lives in.</summary>
    private static string BclValueNamespaceOf(BclValueKind kind) => kind switch
    {
        BclValueKind.Guid or BclValueKind.Version => "System",
        BclValueKind.Encoding => "System.Text",
        BclValueKind.MethodInfo or BclValueKind.ConstructorInfo or BclValueKind.PropertyInfo or
            BclValueKind.FieldInfo or BclValueKind.ParameterInfo => "System.Reflection",
        _ => "System.Text.RegularExpressions",
    };

    /// <summary>Renders one BCL value in its invariant text form.</summary>
    /// <remarks><see cref="Guid"/> uses its <c>D</c> form and <see cref="Version"/> its full component form.</remarks>
    private static string RenderBclValue(Operand operand) => operand.BclValueKind switch
    {
        BclValueKind.Guid => ((Guid)operand.Box!).ToString("D", CultureInfo.InvariantCulture),
        BclValueKind.Version => ((Version)operand.Box!).ToString(),
        _ when IsReflectionKind(operand.BclValueKind) => RenderReflectionValue(operand),
        _ => RenderTextValue(operand),
    };

    private static FoldOutcome BclValue(BclValueKind kind, object value) =>
        FoldOutcome.Folded(Operand.FromBclValue(kind, value));

    // ---- Construction -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Folds <c>new Guid(...)</c> and <c>new Version(...)</c>: a Guid from its fixed-grammar string form, and a
    /// Version from two to four components or its dotted string form. Every other creation stays not-constant.
    /// </summary>
    private static FoldOutcome FoldBclValueCreation(ObjectCreationExpressionSyntax creation, FoldContext context)
    {
        if (!TryReadBclValueTypeName(creation.Type, out var kind))
        {
            return FoldOutcome.NotArithmetic();
        }

        var argumentExpressions = creation.ArgumentList?.Arguments ?? default;
        var arguments = new List<Operand>(argumentExpressions.Count);
        foreach (var argument in argumentExpressions)
        {
            if (argument.NameColon is not null || argument.RefKindKeyword != default)
            {
                return FoldOutcome.NotArithmetic();
            }

            var folded = Fold(argument.Expression, context);
            if (folded.Disposition != FoldDisposition.Folded)
            {
                return folded;
            }

            arguments.Add(folded.Operand);
        }

        return DispatchBclValueConstruction(kind, arguments);
    }

    /// <summary>Constructs one BCL value from folded constant arguments, with exact constructor semantics.</summary>
    private static FoldOutcome DispatchBclValueConstruction(BclValueKind kind, List<Operand> arguments)
    {
        if (kind == BclValueKind.Regex)
        {
            return FoldRegexCreation(arguments);
        }

        try
        {
            switch (kind)
            {
                case BclValueKind.Guid when arguments.Count == 0:
                    return BclValue(BclValueKind.Guid, Guid.Empty);
                case BclValueKind.Guid when arguments is [{ Kind: OperandKind.String } text]:
                    return BclValue(BclValueKind.Guid, new Guid(text.String!));
                case BclValueKind.Version when arguments.Count == 0:
                    return BclValue(BclValueKind.Version, new Version());
                case BclValueKind.Version when arguments is [{ Kind: OperandKind.String } text]:
                    return BclValue(BclValueKind.Version, new Version(text.String!));
                case BclValueKind.Version when TryInt32Arguments(arguments, out var parts):
                    return parts.Length switch
                    {
                        2 => BclValue(BclValueKind.Version, new Version(parts[0], parts[1])),
                        3 => BclValue(BclValueKind.Version, new Version(parts[0], parts[1], parts[2])),
                        4 => BclValue(BclValueKind.Version, new Version(parts[0], parts[1], parts[2], parts[3])),
                        _ => BclValueConstructorUnsupported(kind),
                    };
                default:
                    return BclValueConstructorUnsupported(kind);
            }
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error("System.FormatException", exception.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    private static FoldOutcome BclValueConstructorUnsupported(BclValueKind kind) => FoldOutcome.Error(
        MemberUnsupportedCode,
        kind == BclValueKind.Guid
            ? "This Guid constructor shape is not admitted; use the string form or Guid.Parse."
            : "This Version constructor shape is not admitted; use two to four components or the dotted string form.");

    private static bool TryReadBclValueTypeName(TypeSyntax type, out BclValueKind kind)
    {
        kind = default;
        var name = type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax
            {
                Left: IdentifierNameSyntax { Identifier.ValueText: "System" },
                Right: IdentifierNameSyntax nested,
            } => nested.Identifier.ValueText,
            QualifiedNameSyntax
            {
                Left: QualifiedNameSyntax
                {
                    Left: QualifiedNameSyntax
                    {
                        Left: IdentifierNameSyntax { Identifier.ValueText: "System" },
                        Right.Identifier.ValueText: "Text",
                    },
                    Right.Identifier.ValueText: "RegularExpressions",
                },
                Right: IdentifierNameSyntax { Identifier.ValueText: "Regex" },
            } => "Regex",
            _ => null,
        };
        switch (name)
        {
            case "Guid":
                kind = BclValueKind.Guid;
                return true;
            case "Version":
                kind = BclValueKind.Version;
                return true;
            case "Regex":
                kind = BclValueKind.Regex;
                return true;
            default:
                return false;
        }
    }

    // ---- Type statics -----------------------------------------------------------------------------------------------

    private static FoldOutcome DispatchBclValueStaticProperty(BclValueKind kind, string member) =>
        (kind, member) switch
        {
            (BclValueKind.Guid, "Empty") => BclValue(BclValueKind.Guid, Guid.Empty),
            (BclValueKind.Guid, "NewGuid") => Nondeterministic("Guid.NewGuid"),
            (BclValueKind.Encoding, _) => DispatchEncodingStaticProperty(member),
            _ => MemberUnsupported($"{kind}.{member}"),
        };

    private static FoldOutcome DispatchBclValueStaticMethod(BclValueKind kind, string name, List<Operand> arguments)
    {
        if (kind == BclValueKind.Encoding)
        {
            return DispatchEncodingStaticMethod(name, arguments);
        }

        if (kind == BclValueKind.Regex)
        {
            return DispatchRegexStaticMethod(name, arguments);
        }

        try
        {
            switch (kind, name)
            {
                case (BclValueKind.Guid, "NewGuid"):
                    return Nondeterministic("Guid.NewGuid()");
                case (BclValueKind.Guid, "Parse") when arguments is [{ Kind: OperandKind.String } text]:
                    // Guid's grammar is fixed hexadecimal with punctuation; parsing is culture-independent.
                    return BclValue(BclValueKind.Guid, Guid.Parse(text.String!));
                case (BclValueKind.Guid, "ParseExact") when arguments is
                    [{ Kind: OperandKind.String } text, { Kind: OperandKind.String } format]:
                    return BclValue(BclValueKind.Guid, Guid.ParseExact(text.String!, format.String!));
                case (BclValueKind.Version, "Parse") when arguments is [{ Kind: OperandKind.String } text]:
                    return BclValue(BclValueKind.Version, Version.Parse(text.String!));
                default:
                    return MemberUnsupported($"{kind}.{name}");
            }
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error("System.FormatException", exception.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    // ---- Instance members -------------------------------------------------------------------------------------------

    private static FoldOutcome DispatchBclValueProperty(Operand receiver, string member)
    {
        if (receiver.BclValueKind == BclValueKind.Guid)
        {
            var guid = (Guid)receiver.Box!;
            return member switch
            {
                // Variant and Version are structural facts of the value itself, fixed by RFC 4122.
                "Variant" => FoldOutcome.Folded(Operand.FromInt32(guid.Variant)),
                "Version" => FoldOutcome.Folded(Operand.FromInt32(guid.Version)),
                _ => MemberUnsupported($"Guid.{member}"),
            };
        }

        if (IsReflectionKind(receiver.BclValueKind))
        {
            return DispatchReflectionProperty(receiver, member);
        }

        if (receiver.BclValueKind != BclValueKind.Version)
        {
            return DispatchTextValueProperty(receiver, member);
        }

        var version = (Version)receiver.Box!;
        return member switch
        {
            "Major" => FoldOutcome.Folded(Operand.FromInt32(version.Major)),
            "Minor" => FoldOutcome.Folded(Operand.FromInt32(version.Minor)),
            "Build" => FoldOutcome.Folded(Operand.FromInt32(version.Build)),
            "Revision" => FoldOutcome.Folded(Operand.FromInt32(version.Revision)),
            "MajorRevision" => FoldOutcome.Folded(Operand.FromInt32(version.MajorRevision)),
            "MinorRevision" => FoldOutcome.Folded(Operand.FromInt32(version.MinorRevision)),
            _ => MemberUnsupported($"Version.{member}"),
        };
    }

    private static FoldOutcome DispatchBclValueMethod(Operand receiver, string name, List<Operand> arguments)
    {
        // Invocation reaches reflection values through the fold-context-aware path; this arm only backstops
        // callers without a context, where the Enum and Array static surfaces cannot resolve shapes.
        if (IsReflectionKind(receiver.BclValueKind))
        {
            return DispatchReflectionMethod(receiver, name, arguments, context: null);
        }

        if (receiver.BclValueKind is not (BclValueKind.Guid or BclValueKind.Version))
        {
            return DispatchTextValueMethod(receiver, name, arguments);
        }

        try
        {
            if (receiver.BclValueKind == BclValueKind.Guid)
            {
                var guid = (Guid)receiver.Box!;
                return (name, arguments) switch
                {
                    ("ToString", []) => FoldOutcome.Folded(Operand.FromString(
                        guid.ToString("D", CultureInfo.InvariantCulture))),
                    ("ToString", [{ Kind: OperandKind.String } format]) => FoldOutcome.Folded(Operand.FromString(
                        guid.ToString(format.String!, CultureInfo.InvariantCulture))),
                    ("CompareTo", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Guid } other]) =>
                        FoldOutcome.Folded(Operand.FromInt32(guid.CompareTo((Guid)other.Box!))),
                    _ => MemberUnsupported($"Guid.{name}"),
                };
            }

            var version = (Version)receiver.Box!;
            return (name, arguments) switch
            {
                ("ToString", []) => FoldOutcome.Folded(Operand.FromString(version.ToString())),
                ("ToString", [{ } fieldCount]) when TryImplicitInt32(fieldCount, out var fields) =>
                    FoldOutcome.Folded(Operand.FromString(version.ToString(fields))),
                ("CompareTo", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Version } other]) =>
                    FoldOutcome.Folded(Operand.FromInt32(version.CompareTo((Version)other.Box!))),
                _ => MemberUnsupported($"Version.{name}"),
            };
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error("System.FormatException", exception.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    // ---- Operators --------------------------------------------------------------------------------------------------

    /// <summary>Comparisons and equality over one BCL value kind; no arithmetic operator is defined for them.</summary>
    private static FoldOutcome ComputeBclValueBinary(SyntaxKind kind, Operand left, Operand right)
    {
        // Every same-code-page encoding the evaluator can produce is the runtime's cached singleton, so value
        // equality and the language's reference equality agree; the regex family defines no constant operator.
        if (left is { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Encoding } &&
            right is { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Encoding } &&
            kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            var sameEncoding = left.Box!.Equals(right.Box);
            return FoldOutcome.Folded(Operand.FromBoolean(
                kind == SyntaxKind.EqualsExpression ? sameEncoding : !sameEncoding));
        }

        if (kind is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or
            SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or
            SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression))
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Guid and Version values define comparisons and equality, not arithmetic.");
        }

        if (!TryCompareBclValue(left, right, out var comparison))
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Comparison requires two Guid or two Version values; encodings define equality only, and the "
                + "Regex family defines no constant operators.");
        }

        return FoldOutcome.Folded(Operand.FromBoolean(kind switch
        {
            SyntaxKind.EqualsExpression => comparison == 0,
            SyntaxKind.NotEqualsExpression => comparison != 0,
            SyntaxKind.LessThanExpression => comparison < 0,
            SyntaxKind.LessThanOrEqualExpression => comparison <= 0,
            SyntaxKind.GreaterThanExpression => comparison > 0,
            _ => comparison >= 0,
        }));
    }

    /// <summary>Compares two BCL values of the same kind; false when the kinds differ.</summary>
    private static bool TryCompareBclValue(Operand left, Operand right, out int comparison)
    {
        comparison = 0;
        if (left.Kind != OperandKind.BclValue || right.Kind != OperandKind.BclValue ||
            left.BclValueKind != right.BclValueKind ||
            left.BclValueKind is not (BclValueKind.Guid or BclValueKind.Version))
        {
            return false;
        }

        comparison = left.BclValueKind switch
        {
            BclValueKind.Guid => ((Guid)left.Box!).CompareTo((Guid)right.Box!),
            _ => ((Version)left.Box!).CompareTo((Version)right.Box!),
        };
        return true;
    }
}
