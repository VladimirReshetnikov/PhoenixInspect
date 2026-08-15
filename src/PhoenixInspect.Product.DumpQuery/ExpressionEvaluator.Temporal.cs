using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Date and time computations: <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>,
/// <see cref="DateOnly"/>, and <see cref="TimeOnly"/> with the exact BCL semantics under the invariant culture.
/// Everything here is deterministic content of the expression itself. Members that read the analysis machine —
/// <c>Now</c>, <c>Today</c>, <c>UtcNow</c>, and every local-time-zone conversion — are typed stops, because a
/// post-mortem answer must never depend on when or where the dump is inspected.
/// </content>
public static partial class ExpressionEvaluator
{
    private const string NondeterministicCode = "EVAL_NONDETERMINISTIC_UNSUPPORTED";

    /// <summary>The five supported date and time value domains.</summary>
    internal enum TemporalKind
    {
        /// <summary>A <see cref="System.DateTime"/> value.</summary>
        DateTime,

        /// <summary>A <see cref="System.DateTimeOffset"/> value.</summary>
        DateTimeOffset,

        /// <summary>A <see cref="System.TimeSpan"/> value.</summary>
        TimeSpan,

        /// <summary>A <see cref="System.DateOnly"/> value.</summary>
        DateOnly,

        /// <summary>A <see cref="System.TimeOnly"/> value.</summary>
        TimeOnly,
    }

    private static FoldOutcome Nondeterministic(string member) => FoldOutcome.Error(
        NondeterministicCode,
        $"'{member}' reads the analysis machine's clock or time zone, which is not evidence from the snapshot; "
        + "construct the value explicitly or compute it from dump values instead.");

    /// <summary>Renders one temporal value with its round-trip pattern under the invariant culture.</summary>
    /// <remarks><see cref="TimeSpan"/> has no <c>O</c> pattern; <c>c</c> is its culture-insensitive constant form.</remarks>
    private static string RenderTemporal(Operand operand) =>
        ((IFormattable)operand.Box!).ToString(
            operand.TemporalKind == TemporalKind.TimeSpan ? "c" : "O",
            CultureInfo.InvariantCulture);

    /// <summary>Formats one temporal value under the invariant culture, with an optional format string.</summary>
    private static FoldOutcome TemporalToString(Operand operand, string? format)
    {
        try
        {
            return FoldOutcome.Folded(Operand.FromString(
                ((IFormattable)operand.Box!).ToString(format, CultureInfo.InvariantCulture)));
        }
        catch (FormatException exception)
        {
            return FoldOutcome.Error("System.FormatException", exception.Message);
        }
    }

    // ---- Construction -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Folds <c>new DateTime(...)</c>, <c>new DateTimeOffset(...)</c>, <c>new TimeSpan(...)</c>,
    /// <c>new DateOnly(...)</c>, and <c>new TimeOnly(...)</c> over folded arguments, with the exact
    /// BCL constructor semantics; every other object creation stays not-folded.
    /// </summary>
    private static FoldOutcome FoldObjectCreation(ObjectCreationExpressionSyntax creation, FoldContext context)
    {
        if (creation.Initializer is not null)
        {
            return FoldOutcome.NotArithmetic();
        }

        // 'new Action(…)' and 'new Func<int, int>(…)' convert their single argument — a lambda, a method
        // group, or another delegate — before ordinary argument folding, which cannot fold a bare lambda.
        if (TryResolveDelegateTypeRef(creation.Type, context, out var delegateType))
        {
            if (creation.ArgumentList?.Arguments is not [{ NameColon: null } single] ||
                single.RefKindKeyword != default)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    $"'new {delegateType!.CSharpName}(…)' takes exactly one argument: a lambda, a method "
                    + "group, or another delegate.");
            }

            return ConvertToDelegate(delegateType!, single.Expression, context);
        }

        // 'new KeyValuePair<K, V>(key, value)' constructs the pair value the immutable dictionaries carry.
        if (creation.Type is GenericNameSyntax
            {
                Identifier.ValueText: "KeyValuePair",
                TypeArgumentList.Arguments.Count: 2,
            })
        {
            if (creation.ArgumentList?.Arguments is not
                [{ NameColon: null } keyArgument, { NameColon: null } valueArgument] ||
                keyArgument.RefKindKeyword != default || valueArgument.RefKindKeyword != default)
            {
                return FoldOutcome.Error(
                    OperandTypeCode, "'new KeyValuePair<K, V>(…)' takes exactly a key and a value.");
            }

            var pairKey = Fold(keyArgument.Expression, context);
            if (pairKey.Disposition != FoldDisposition.Folded)
            {
                return pairKey;
            }

            var pairValue = Fold(valueArgument.Expression, context);
            if (pairValue.Disposition != FoldDisposition.Folded)
            {
                return pairValue;
            }

            return FoldOutcome.Folded(Operand.FromKeyValuePair(
                new KeyValuePairPayload(pairKey.Operand, pairValue.Operand)));
        }

        if (!TryReadTemporalTypeName(creation.Type, out var kind))
        {
            return FoldBclValueCreation(creation, context);
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

        try
        {
            return ConstructTemporal(kind, arguments);
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    private static bool TryReadTemporalTypeName(TypeSyntax type, out TemporalKind kind)
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
            _ => null,
        };
        switch (name)
        {
            case "DateTime":
                kind = TemporalKind.DateTime;
                return true;
            case "DateTimeOffset":
                kind = TemporalKind.DateTimeOffset;
                return true;
            case "TimeSpan":
                kind = TemporalKind.TimeSpan;
                return true;
            case "DateOnly":
                kind = TemporalKind.DateOnly;
                return true;
            case "TimeOnly":
                kind = TemporalKind.TimeOnly;
                return true;
            default:
                return false;
        }
    }

    private static FoldOutcome ConstructTemporal(TemporalKind kind, List<Operand> arguments)
    {
        switch (kind)
        {
            case TemporalKind.DateTime:
                if (TryInt64Argument(arguments, out var dateTimeTicks))
                {
                    return Temporal(TemporalKind.DateTime, new DateTime(dateTimeTicks));
                }

                if (arguments is [{ } tickOperand, { EnumTypeFullName: "System.DateTimeKind" } tickKind] &&
                    TryImplicitInt64(tickOperand, out var kindedTicks))
                {
                    return Temporal(
                        TemporalKind.DateTime,
                        new DateTime(kindedTicks, (DateTimeKind)tickKind.Int32));
                }

                if (TryInt32Arguments(arguments, out var dateParts))
                {
                    return dateParts.Length switch
                    {
                        3 => Temporal(TemporalKind.DateTime, new DateTime(
                            dateParts[0], dateParts[1], dateParts[2])),
                        6 => Temporal(TemporalKind.DateTime, new DateTime(
                            dateParts[0], dateParts[1], dateParts[2], dateParts[3], dateParts[4], dateParts[5])),
                        7 => Temporal(TemporalKind.DateTime, new DateTime(
                            dateParts[0], dateParts[1], dateParts[2], dateParts[3], dateParts[4], dateParts[5],
                            dateParts[6])),
                        _ => TemporalConstructorUnsupported(kind),
                    };
                }

                return TemporalConstructorUnsupported(kind);
            case TemporalKind.DateTimeOffset:
                if (arguments is [{ TemporalKind: TemporalKind.DateTime, Kind: OperandKind.Temporal } dateOperand,
                        { TemporalKind: TemporalKind.TimeSpan, Kind: OperandKind.Temporal } offsetOperand])
                {
                    return Temporal(TemporalKind.DateTimeOffset, new DateTimeOffset(
                        (DateTime)dateOperand.Box!,
                        (TimeSpan)offsetOperand.Box!));
                }

                if (arguments is [{ } ticksOperand, { TemporalKind: TemporalKind.TimeSpan, Kind: OperandKind.Temporal } tickOffset] &&
                    TryImplicitInt64(ticksOperand, out var offsetTicks))
                {
                    return Temporal(TemporalKind.DateTimeOffset, new DateTimeOffset(
                        offsetTicks,
                        (TimeSpan)tickOffset.Box!));
                }

                if (arguments is [{ TemporalKind: TemporalKind.DateTime, Kind: OperandKind.Temporal } bare] &&
                    ((DateTime)bare.Box!).Kind != DateTimeKind.Utc)
                {
                    return Nondeterministic("new DateTimeOffset(DateTime)");
                }

                if (arguments is [{ TemporalKind: TemporalKind.DateTime, Kind: OperandKind.Temporal } utc])
                {
                    return Temporal(TemporalKind.DateTimeOffset, new DateTimeOffset((DateTime)utc.Box!));
                }

                return TemporalConstructorUnsupported(kind);
            case TemporalKind.TimeSpan:
                if (TryInt64Argument(arguments, out var spanTicks))
                {
                    return Temporal(TemporalKind.TimeSpan, new TimeSpan(spanTicks));
                }

                if (TryInt32Arguments(arguments, out var spanParts))
                {
                    return spanParts.Length switch
                    {
                        3 => Temporal(TemporalKind.TimeSpan, new TimeSpan(
                            spanParts[0], spanParts[1], spanParts[2])),
                        4 => Temporal(TemporalKind.TimeSpan, new TimeSpan(
                            spanParts[0], spanParts[1], spanParts[2], spanParts[3])),
                        5 => Temporal(TemporalKind.TimeSpan, new TimeSpan(
                            spanParts[0], spanParts[1], spanParts[2], spanParts[3], spanParts[4])),
                        _ => TemporalConstructorUnsupported(kind),
                    };
                }

                return TemporalConstructorUnsupported(kind);
            case TemporalKind.DateOnly:
                return TryInt32Arguments(arguments, out var dayParts) && dayParts.Length == 3
                    ? Temporal(TemporalKind.DateOnly, new DateOnly(dayParts[0], dayParts[1], dayParts[2]))
                    : TemporalConstructorUnsupported(kind);
            default:
                if (TryInt64Argument(arguments, out var timeTicks))
                {
                    return Temporal(TemporalKind.TimeOnly, new TimeOnly(timeTicks));
                }

                if (TryInt32Arguments(arguments, out var timeParts))
                {
                    return timeParts.Length switch
                    {
                        2 => Temporal(TemporalKind.TimeOnly, new TimeOnly(timeParts[0], timeParts[1])),
                        3 => Temporal(TemporalKind.TimeOnly, new TimeOnly(
                            timeParts[0], timeParts[1], timeParts[2])),
                        4 => Temporal(TemporalKind.TimeOnly, new TimeOnly(
                            timeParts[0], timeParts[1], timeParts[2], timeParts[3])),
                        _ => TemporalConstructorUnsupported(kind),
                    };
                }

                return TemporalConstructorUnsupported(kind);
        }
    }

    private static FoldOutcome Temporal(TemporalKind kind, object value) =>
        FoldOutcome.Folded(Operand.FromTemporal(kind, value));

    private static FoldOutcome TemporalConstructorUnsupported(TemporalKind kind) => FoldOutcome.Error(
        MemberUnsupportedCode,
        $"This {kind} constructor shape is not admitted; use ticks, the calendar/clock component overloads, or "
        + "the deterministic factory methods.");

    /// <summary>Reads a single integral argument widened to Int64, for tick-based constructors.</summary>
    private static bool TryInt64Argument(List<Operand> arguments, out long value)
    {
        value = 0;
        return arguments is [{ } single] && TryImplicitInt64(single, out value);
    }

    private static bool TryImplicitInt64(Operand operand, out long value)
    {
        value = 0;
        if (operand.Kind == OperandKind.Int32)
        {
            value = operand.Int32;
            return true;
        }

        if (operand.Kind == OperandKind.Numeric && IsIntegral(operand.NumericKind))
        {
            var wide = ToBigInteger(operand.NumericKind, operand.Box!);
            if (wide >= long.MinValue && wide <= long.MaxValue)
            {
                value = (long)wide;
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads every argument as an Int32, for calendar and clock component constructors.</summary>
    private static bool TryInt32Arguments(List<Operand> arguments, out int[] values)
    {
        values = new int[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!TryImplicitInt32(arguments[index], out values[index]))
            {
                return false;
            }
        }

        return arguments.Count > 1;
    }

    // ---- Type statics -----------------------------------------------------------------------------------------------

    private static FoldOutcome DispatchTemporalStaticProperty(TemporalKind kind, string member)
    {
        switch (member)
        {
            case "Now" or "UtcNow" or "Today":
                return Nondeterministic($"{kind}.{member}");
        }

        object? value = (kind, member) switch
        {
            (TemporalKind.DateTime, "MinValue") => DateTime.MinValue,
            (TemporalKind.DateTime, "MaxValue") => DateTime.MaxValue,
            (TemporalKind.DateTime, "UnixEpoch") => DateTime.UnixEpoch,
            (TemporalKind.DateTimeOffset, "MinValue") => DateTimeOffset.MinValue,
            (TemporalKind.DateTimeOffset, "MaxValue") => DateTimeOffset.MaxValue,
            (TemporalKind.DateTimeOffset, "UnixEpoch") => DateTimeOffset.UnixEpoch,
            (TemporalKind.TimeSpan, "Zero") => TimeSpan.Zero,
            (TemporalKind.TimeSpan, "MinValue") => TimeSpan.MinValue,
            (TemporalKind.TimeSpan, "MaxValue") => TimeSpan.MaxValue,
            (TemporalKind.DateOnly, "MinValue") => DateOnly.MinValue,
            (TemporalKind.DateOnly, "MaxValue") => DateOnly.MaxValue,
            (TemporalKind.TimeOnly, "MinValue") => TimeOnly.MinValue,
            (TemporalKind.TimeOnly, "MaxValue") => TimeOnly.MaxValue,
            _ => null,
        };
        if (value is not null)
        {
            return Temporal(kind, value);
        }

        long? tickConstant = (kind, member) switch
        {
            (TemporalKind.TimeSpan, "TicksPerDay") => TimeSpan.TicksPerDay,
            (TemporalKind.TimeSpan, "TicksPerHour") => TimeSpan.TicksPerHour,
            (TemporalKind.TimeSpan, "TicksPerMinute") => TimeSpan.TicksPerMinute,
            (TemporalKind.TimeSpan, "TicksPerSecond") => TimeSpan.TicksPerSecond,
            (TemporalKind.TimeSpan, "TicksPerMillisecond") => TimeSpan.TicksPerMillisecond,
            (TemporalKind.TimeSpan, "TicksPerMicrosecond") => TimeSpan.TicksPerMicrosecond,
            _ => null,
        };
        return tickConstant is { } ticks
            ? FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, ticks))
            : MemberUnsupported($"{kind}.{member}");
    }

    private static FoldOutcome DispatchTemporalStaticMethod(TemporalKind kind, string name, List<Operand> arguments)
    {
        try
        {
            switch (kind, name)
            {
                case (TemporalKind.DateTime or TemporalKind.DateTimeOffset or
                        TemporalKind.DateOnly or TemporalKind.TimeOnly, "Parse" or "ParseExact" or "TryParse"):
                    return CultureSensitive(
                        $"{kind}.{name}",
                        "an explicit constructor or the deterministic factory methods; parsing follows "
                        + "culture-dependent rules");
                case (TemporalKind.TimeSpan, "FromDays" or "FromHours" or "FromMinutes" or "FromSeconds" or
                        "FromMilliseconds") when arguments is [{ } scale] && TryTemporalDouble(scale, out var units):
                    return Temporal(TemporalKind.TimeSpan, name switch
                    {
                        "FromDays" => TimeSpan.FromDays(units),
                        "FromHours" => TimeSpan.FromHours(units),
                        "FromMinutes" => TimeSpan.FromMinutes(units),
                        "FromSeconds" => TimeSpan.FromSeconds(units),
                        _ => TimeSpan.FromMilliseconds(units),
                    });
                case (TemporalKind.TimeSpan, "FromTicks") when TryInt64Argument(arguments, out var spanTicks):
                    return Temporal(TemporalKind.TimeSpan, TimeSpan.FromTicks(spanTicks));
                case (TemporalKind.DateTimeOffset, "FromUnixTimeSeconds")
                    when TryInt64Argument(arguments, out var unixSeconds):
                    return Temporal(TemporalKind.DateTimeOffset, DateTimeOffset.FromUnixTimeSeconds(unixSeconds));
                case (TemporalKind.DateTimeOffset, "FromUnixTimeMilliseconds")
                    when TryInt64Argument(arguments, out var unixMilliseconds):
                    return Temporal(
                        TemporalKind.DateTimeOffset,
                        DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds));
                case (TemporalKind.DateOnly, "FromDayNumber")
                    when arguments is [{ } day] && TryImplicitInt32(day, out var dayNumber):
                    return Temporal(TemporalKind.DateOnly, DateOnly.FromDayNumber(dayNumber));
                case (TemporalKind.DateOnly, "FromDateTime")
                    when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.DateTime } source]:
                    return Temporal(TemporalKind.DateOnly, DateOnly.FromDateTime((DateTime)source.Box!));
                case (TemporalKind.TimeOnly, "FromDateTime")
                    when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.DateTime } source]:
                    return Temporal(TemporalKind.TimeOnly, TimeOnly.FromDateTime((DateTime)source.Box!));
                case (TemporalKind.TimeOnly, "FromTimeSpan")
                    when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } span]:
                    return Temporal(TemporalKind.TimeOnly, TimeOnly.FromTimeSpan((TimeSpan)span.Box!));
                default:
                    return MemberUnsupported($"{kind}.{name}");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    private static bool TryTemporalDouble(Operand operand, out double value)
    {
        value = 0;
        if (!operand.IsNumeric || operand.Kind == OperandKind.Enum)
        {
            return false;
        }

        value = ToDoubleValue(NumericKindOf(operand), BoxOf(operand));
        return true;
    }

    // ---- Instance properties ----------------------------------------------------------------------------------------

    private static FoldOutcome DispatchTemporalProperty(Operand receiver, string member)
    {
        switch (receiver.TemporalKind, receiver.Box, member)
        {
            case (_, _, "Kind") when receiver.TemporalKind == TemporalKind.DateTime:
                var dateTimeKind = ((DateTime)receiver.Box!).Kind;
                return FoldOutcome.Folded(Operand.FromEnum(
                    (int)dateTimeKind, "System.DateTimeKind", dateTimeKind.ToString()));
            case (TemporalKind.DateTime, DateTime dateTime, _):
                return DispatchDateLikeProperty(
                    member,
                    dateTime.Year, dateTime.Month, dateTime.Day, dateTime.DayOfYear, dateTime.DayOfWeek,
                    dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, dateTime.Ticks)
                    ?? member switch
                    {
                        "Date" => Temporal(TemporalKind.DateTime, dateTime.Date),
                        "TimeOfDay" => Temporal(TemporalKind.TimeSpan, dateTime.TimeOfDay),
                        _ => MemberUnsupported($"DateTime.{member}"),
                    };
            case (TemporalKind.DateTimeOffset, DateTimeOffset offset, _):
                return DispatchDateLikeProperty(
                    member,
                    offset.Year, offset.Month, offset.Day, offset.DayOfYear, offset.DayOfWeek,
                    offset.Hour, offset.Minute, offset.Second, offset.Millisecond, offset.Ticks)
                    ?? member switch
                    {
                        "Date" => Temporal(TemporalKind.DateTime, offset.Date),
                        "TimeOfDay" => Temporal(TemporalKind.TimeSpan, offset.TimeOfDay),
                        "Offset" => Temporal(TemporalKind.TimeSpan, offset.Offset),
                        "DateTime" => Temporal(TemporalKind.DateTime, offset.DateTime),
                        "UtcDateTime" => Temporal(TemporalKind.DateTime, offset.UtcDateTime),
                        "UtcTicks" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, offset.UtcTicks)),
                        "LocalDateTime" => Nondeterministic("DateTimeOffset.LocalDateTime"),
                        _ => MemberUnsupported($"DateTimeOffset.{member}"),
                    };
            case (TemporalKind.TimeSpan, TimeSpan span, _):
                return member switch
                {
                    "Days" => FoldOutcome.Folded(Operand.FromInt32(span.Days)),
                    "Hours" => FoldOutcome.Folded(Operand.FromInt32(span.Hours)),
                    "Minutes" => FoldOutcome.Folded(Operand.FromInt32(span.Minutes)),
                    "Seconds" => FoldOutcome.Folded(Operand.FromInt32(span.Seconds)),
                    "Milliseconds" => FoldOutcome.Folded(Operand.FromInt32(span.Milliseconds)),
                    "Ticks" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, span.Ticks)),
                    "TotalDays" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, span.TotalDays)),
                    "TotalHours" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, span.TotalHours)),
                    "TotalMinutes" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, span.TotalMinutes)),
                    "TotalSeconds" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, span.TotalSeconds)),
                    "TotalMilliseconds" => FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.Double, span.TotalMilliseconds)),
                    _ => MemberUnsupported($"TimeSpan.{member}"),
                };
            case (TemporalKind.DateOnly, DateOnly date, _):
                return member switch
                {
                    "Year" => FoldOutcome.Folded(Operand.FromInt32(date.Year)),
                    "Month" => FoldOutcome.Folded(Operand.FromInt32(date.Month)),
                    "Day" => FoldOutcome.Folded(Operand.FromInt32(date.Day)),
                    "DayOfYear" => FoldOutcome.Folded(Operand.FromInt32(date.DayOfYear)),
                    "DayNumber" => FoldOutcome.Folded(Operand.FromInt32(date.DayNumber)),
                    "DayOfWeek" => FoldOutcome.Folded(Operand.FromEnum(
                        (int)date.DayOfWeek, "System.DayOfWeek", date.DayOfWeek.ToString())),
                    _ => MemberUnsupported($"DateOnly.{member}"),
                };
            default:
                var time = (TimeOnly)receiver.Box!;
                return member switch
                {
                    "Hour" => FoldOutcome.Folded(Operand.FromInt32(time.Hour)),
                    "Minute" => FoldOutcome.Folded(Operand.FromInt32(time.Minute)),
                    "Second" => FoldOutcome.Folded(Operand.FromInt32(time.Second)),
                    "Millisecond" => FoldOutcome.Folded(Operand.FromInt32(time.Millisecond)),
                    "Ticks" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, time.Ticks)),
                    _ => MemberUnsupported($"TimeOnly.{member}"),
                };
        }
    }

    /// <summary>The calendar and clock components DateTime and DateTimeOffset share, or null when unmatched.</summary>
    private static FoldOutcome? DispatchDateLikeProperty(
        string member,
        int year,
        int month,
        int day,
        int dayOfYear,
        DayOfWeek dayOfWeek,
        int hour,
        int minute,
        int second,
        int millisecond,
        long ticks) => member switch
    {
        "Year" => FoldOutcome.Folded(Operand.FromInt32(year)),
        "Month" => FoldOutcome.Folded(Operand.FromInt32(month)),
        "Day" => FoldOutcome.Folded(Operand.FromInt32(day)),
        "DayOfYear" => FoldOutcome.Folded(Operand.FromInt32(dayOfYear)),
        "DayOfWeek" => FoldOutcome.Folded(Operand.FromEnum(
            (int)dayOfWeek, "System.DayOfWeek", dayOfWeek.ToString())),
        "Hour" => FoldOutcome.Folded(Operand.FromInt32(hour)),
        "Minute" => FoldOutcome.Folded(Operand.FromInt32(minute)),
        "Second" => FoldOutcome.Folded(Operand.FromInt32(second)),
        "Millisecond" => FoldOutcome.Folded(Operand.FromInt32(millisecond)),
        "Ticks" => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, ticks)),
        _ => null,
    };

    // ---- Instance methods -------------------------------------------------------------------------------------------

    private static FoldOutcome DispatchTemporalMethod(Operand receiver, string name, List<Operand> arguments)
    {
        if (name == "ToString")
        {
            return arguments switch
            {
                [] => TemporalToString(receiver, format: null),
                [{ Kind: OperandKind.String } format] => TemporalToString(receiver, format.String),
                _ => MemberUnsupported($"{receiver.TemporalKind}.ToString"),
            };
        }

        try
        {
            return receiver.TemporalKind switch
            {
                TemporalKind.DateTime => DispatchDateTimeMethod((DateTime)receiver.Box!, name, arguments),
                TemporalKind.DateTimeOffset => DispatchDateTimeOffsetMethod(
                    (DateTimeOffset)receiver.Box!, name, arguments),
                TemporalKind.TimeSpan => DispatchTimeSpanMethod((TimeSpan)receiver.Box!, name, arguments),
                TemporalKind.DateOnly => DispatchDateOnlyMethod((DateOnly)receiver.Box!, name, arguments),
                _ => DispatchTimeOnlyMethod((TimeOnly)receiver.Box!, name, arguments),
            };
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
        }
    }

    private static FoldOutcome DispatchDateTimeMethod(DateTime value, string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "ToUniversalTime" or "ToLocalTime":
                return Nondeterministic($"DateTime.{name}");
            case "AddYears" or "AddMonths" when arguments is [{ } wholeUnits] &&
                TryImplicitInt32(wholeUnits, out var whole):
                return Temporal(TemporalKind.DateTime, name == "AddYears"
                    ? value.AddYears(whole)
                    : value.AddMonths(whole));
            case "AddDays" or "AddHours" or "AddMinutes" or "AddSeconds" or "AddMilliseconds"
                when arguments is [{ } scaledUnits] && TryTemporalDouble(scaledUnits, out var scaled):
                return Temporal(TemporalKind.DateTime, name switch
                {
                    "AddDays" => value.AddDays(scaled),
                    "AddHours" => value.AddHours(scaled),
                    "AddMinutes" => value.AddMinutes(scaled),
                    "AddSeconds" => value.AddSeconds(scaled),
                    _ => value.AddMilliseconds(scaled),
                });
            case "AddTicks" when arguments is [{ } tickOperand] && TryImplicitInt64(tickOperand, out var ticks):
                return Temporal(TemporalKind.DateTime, value.AddTicks(ticks));
            case "Add" when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } span]:
                return Temporal(TemporalKind.DateTime, value.Add((TimeSpan)span.Box!));
            case "Subtract" when arguments is [{ Kind: OperandKind.Temporal } other]:
                return other.TemporalKind switch
                {
                    TemporalKind.TimeSpan => Temporal(TemporalKind.DateTime, value.Subtract((TimeSpan)other.Box!)),
                    TemporalKind.DateTime => Temporal(TemporalKind.TimeSpan, value.Subtract((DateTime)other.Box!)),
                    _ => MemberUnsupported("DateTime.Subtract"),
                };
            default:
                return MemberUnsupported($"DateTime.{name}");
        }
    }

    private static FoldOutcome DispatchDateTimeOffsetMethod(
        DateTimeOffset value,
        string name,
        List<Operand> arguments)
    {
        switch (name)
        {
            case "ToLocalTime":
                return Nondeterministic("DateTimeOffset.ToLocalTime");
            case "ToUniversalTime" when arguments.Count == 0:
                // Deterministic for DateTimeOffset: the value carries its own offset.
                return Temporal(TemporalKind.DateTimeOffset, value.ToUniversalTime());
            case "ToUnixTimeSeconds" when arguments.Count == 0:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, value.ToUnixTimeSeconds()));
            case "ToUnixTimeMilliseconds" when arguments.Count == 0:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, value.ToUnixTimeMilliseconds()));
            case "ToOffset" when arguments is
                [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } offset]:
                return Temporal(TemporalKind.DateTimeOffset, value.ToOffset((TimeSpan)offset.Box!));
            case "AddYears" or "AddMonths" when arguments is [{ } wholeUnits] &&
                TryImplicitInt32(wholeUnits, out var whole):
                return Temporal(TemporalKind.DateTimeOffset, name == "AddYears"
                    ? value.AddYears(whole)
                    : value.AddMonths(whole));
            case "AddDays" or "AddHours" or "AddMinutes" or "AddSeconds" or "AddMilliseconds"
                when arguments is [{ } scaledUnits] && TryTemporalDouble(scaledUnits, out var scaled):
                return Temporal(TemporalKind.DateTimeOffset, name switch
                {
                    "AddDays" => value.AddDays(scaled),
                    "AddHours" => value.AddHours(scaled),
                    "AddMinutes" => value.AddMinutes(scaled),
                    "AddSeconds" => value.AddSeconds(scaled),
                    _ => value.AddMilliseconds(scaled),
                });
            case "AddTicks" when arguments is [{ } tickOperand] && TryImplicitInt64(tickOperand, out var ticks):
                return Temporal(TemporalKind.DateTimeOffset, value.AddTicks(ticks));
            case "Add" when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } span]:
                return Temporal(TemporalKind.DateTimeOffset, value.Add((TimeSpan)span.Box!));
            case "Subtract" when arguments is [{ Kind: OperandKind.Temporal } other]:
                return other.TemporalKind switch
                {
                    TemporalKind.TimeSpan => Temporal(
                        TemporalKind.DateTimeOffset,
                        value.Subtract((TimeSpan)other.Box!)),
                    TemporalKind.DateTimeOffset => Temporal(
                        TemporalKind.TimeSpan,
                        value.Subtract((DateTimeOffset)other.Box!)),
                    _ => MemberUnsupported("DateTimeOffset.Subtract"),
                };
            default:
                return MemberUnsupported($"DateTimeOffset.{name}");
        }
    }

    private static FoldOutcome DispatchTimeSpanMethod(TimeSpan value, string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "Negate" when arguments.Count == 0:
                return Temporal(TemporalKind.TimeSpan, value.Negate());
            case "Duration" when arguments.Count == 0:
                return Temporal(TemporalKind.TimeSpan, value.Duration());
            case "Add" when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } span]:
                return Temporal(TemporalKind.TimeSpan, value.Add((TimeSpan)span.Box!));
            case "Subtract" when arguments is
                [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } span]:
                return Temporal(TemporalKind.TimeSpan, value.Subtract((TimeSpan)span.Box!));
            case "Multiply" when arguments is [{ } factor] && TryTemporalDouble(factor, out var by):
                return Temporal(TemporalKind.TimeSpan, value.Multiply(by));
            case "Divide" when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } span]:
                return FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Double,
                    value.Divide((TimeSpan)span.Box!)));
            case "Divide" when arguments is [{ } divisor] && TryTemporalDouble(divisor, out var by):
                return Temporal(TemporalKind.TimeSpan, value.Divide(by));
            default:
                return MemberUnsupported($"TimeSpan.{name}");
        }
    }

    private static FoldOutcome DispatchDateOnlyMethod(DateOnly value, string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "AddDays" or "AddMonths" or "AddYears" when arguments is [{ } wholeUnits] &&
                TryImplicitInt32(wholeUnits, out var whole):
                return Temporal(TemporalKind.DateOnly, name switch
                {
                    "AddDays" => value.AddDays(whole),
                    "AddMonths" => value.AddMonths(whole),
                    _ => value.AddYears(whole),
                });
            case "ToDateTime" when arguments is
                [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeOnly } time]:
                return Temporal(TemporalKind.DateTime, value.ToDateTime((TimeOnly)time.Box!));
            default:
                return MemberUnsupported($"DateOnly.{name}");
        }
    }

    private static FoldOutcome DispatchTimeOnlyMethod(TimeOnly value, string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "AddHours" or "AddMinutes" when arguments is [{ } scaledUnits] &&
                TryTemporalDouble(scaledUnits, out var scaled):
                return Temporal(TemporalKind.TimeOnly, name == "AddHours"
                    ? value.AddHours(scaled)
                    : value.AddMinutes(scaled));
            case "Add" when arguments is [{ Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan } span]:
                return Temporal(TemporalKind.TimeOnly, value.Add((TimeSpan)span.Box!));
            case "ToTimeSpan" when arguments.Count == 0:
                return Temporal(TemporalKind.TimeSpan, value.ToTimeSpan());
            default:
                return MemberUnsupported($"TimeOnly.{name}");
        }
    }

    // ---- Operators --------------------------------------------------------------------------------------------------

    /// <summary>The C# operator algebra over the five temporal domains, with checked BCL semantics.</summary>
    private static FoldOutcome ComputeTemporalBinary(SyntaxKind kind, Operand left, Operand right)
    {
        try
        {
            if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or
                SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or
                SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression)
            {
                if (!TryCompareTemporal(left, right, out var comparison))
                {
                    return FoldOutcome.Error(
                        OperandTypeCode,
                        "Date and time comparisons require two values of the same temporal kind.");
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

            switch (kind, left.TemporalKind, right.TemporalKind)
            {
                case (SyntaxKind.AddExpression, TemporalKind.DateTime, TemporalKind.TimeSpan)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.DateTime, (DateTime)left.Box! + (TimeSpan)right.Box!);
                case (SyntaxKind.AddExpression, TemporalKind.DateTimeOffset, TemporalKind.TimeSpan)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.DateTimeOffset, (DateTimeOffset)left.Box! + (TimeSpan)right.Box!);
                case (SyntaxKind.AddExpression, TemporalKind.TimeSpan, TemporalKind.TimeSpan)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.TimeSpan, (TimeSpan)left.Box! + (TimeSpan)right.Box!);
                case (SyntaxKind.SubtractExpression, TemporalKind.DateTime, TemporalKind.DateTime)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.TimeSpan, (DateTime)left.Box! - (DateTime)right.Box!);
                case (SyntaxKind.SubtractExpression, TemporalKind.DateTime, TemporalKind.TimeSpan)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.DateTime, (DateTime)left.Box! - (TimeSpan)right.Box!);
                case (SyntaxKind.SubtractExpression, TemporalKind.DateTimeOffset, TemporalKind.DateTimeOffset)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.TimeSpan, (DateTimeOffset)left.Box! - (DateTimeOffset)right.Box!);
                case (SyntaxKind.SubtractExpression, TemporalKind.DateTimeOffset, TemporalKind.TimeSpan)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.DateTimeOffset, (DateTimeOffset)left.Box! - (TimeSpan)right.Box!);
                case (SyntaxKind.SubtractExpression, TemporalKind.TimeSpan, TemporalKind.TimeSpan)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.TimeSpan, (TimeSpan)left.Box! - (TimeSpan)right.Box!);
                case (SyntaxKind.SubtractExpression, TemporalKind.TimeOnly, TemporalKind.TimeOnly)
                    when IsTemporal(left) && IsTemporal(right):
                    return Temporal(TemporalKind.TimeSpan, (TimeOnly)left.Box! - (TimeOnly)right.Box!);
            }

            // TimeSpan scales by a number on either side, and divides by a number or another TimeSpan.
            if (kind == SyntaxKind.MultiplyExpression)
            {
                if (IsTimeSpan(left) && TryTemporalDouble(right, out var rightFactor))
                {
                    return Temporal(TemporalKind.TimeSpan, (TimeSpan)left.Box! * rightFactor);
                }

                if (IsTimeSpan(right) && TryTemporalDouble(left, out var leftFactor))
                {
                    return Temporal(TemporalKind.TimeSpan, leftFactor * (TimeSpan)right.Box!);
                }
            }

            if (kind == SyntaxKind.DivideExpression && IsTimeSpan(left))
            {
                if (IsTimeSpan(right))
                {
                    return FoldOutcome.Folded(Operand.FromNumeric(
                        NumericKind.Double,
                        (TimeSpan)left.Box! / (TimeSpan)right.Box!));
                }

                if (TryTemporalDouble(right, out var divisor))
                {
                    return Temporal(TemporalKind.TimeSpan, (TimeSpan)left.Box! / divisor);
                }
            }

            return FoldOutcome.Error(
                OperandTypeCode,
                "This operator is not defined over these date and time operand kinds.");
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return FoldOutcome.Error(OverflowCode, exception.Message);
        }
    }

    private static bool IsTemporal(Operand operand) => operand.Kind == OperandKind.Temporal;

    private static bool IsTimeSpan(Operand operand) =>
        operand is { Kind: OperandKind.Temporal, TemporalKind: TemporalKind.TimeSpan };

    /// <summary>Compares two temporal values of the same kind; false when the kinds differ.</summary>
    private static bool TryCompareTemporal(Operand left, Operand right, out int comparison)
    {
        comparison = 0;
        if (!IsTemporal(left) || !IsTemporal(right) || left.TemporalKind != right.TemporalKind)
        {
            return false;
        }

        comparison = left.TemporalKind switch
        {
            TemporalKind.DateTime => ((DateTime)left.Box!).CompareTo((DateTime)right.Box!),
            TemporalKind.DateTimeOffset => ((DateTimeOffset)left.Box!).CompareTo((DateTimeOffset)right.Box!),
            TemporalKind.TimeSpan => ((TimeSpan)left.Box!).CompareTo((TimeSpan)right.Box!),
            TemporalKind.DateOnly => ((DateOnly)left.Box!).CompareTo((DateOnly)right.Box!),
            _ => ((TimeOnly)left.Box!).CompareTo((TimeOnly)right.Box!),
        };
        return true;
    }
}
