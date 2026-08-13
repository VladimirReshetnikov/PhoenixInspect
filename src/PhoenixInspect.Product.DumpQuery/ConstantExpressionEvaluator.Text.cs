using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The deterministic text-processing surface: <see cref="Encoding"/> and the <see cref="Regex"/> family —
/// <see cref="Regex"/>, <see cref="Match"/>, <see cref="Group"/>, <see cref="Capture"/>, and their collections.
/// Byte and character transcoding under a named encoding is a pure function of its input, and a regular
/// expression is a pure function of pattern, options, and input, so both fold with the BCL's own semantics.
/// The two ways non-determinism could leak in are closed with typed stops: case-insensitive matching without
/// <see cref="RegexOptions.CultureInvariant"/> depends on the analysis machine's culture and is refused, and a
/// pattern whose backtracking exceeds a fixed one-second budget stops with the timeout named rather than
/// hanging the evaluator.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    /// <summary>The full name RegexOptions members carry, matching the known-enum dispatch table.</summary>
    private const string RegexOptionsFullName = "System.Text.RegularExpressions.RegexOptions";

    /// <summary>
    /// The fixed budget one pattern may spend matching. A pattern that exceeds it stops with a typed timeout
    /// instead of hanging the prompt; every non-pathological pattern folds far inside the budget.
    /// </summary>
    private static readonly TimeSpan RegexFoldTimeout = TimeSpan.FromSeconds(1);

    // ---- Rendering --------------------------------------------------------------------------------------------------

    /// <summary>Renders one text-family value in its invariant form.</summary>
    /// <remarks>
    /// An <see cref="Encoding"/> renders as its IANA web name, a <see cref="Regex"/> as its pattern, and a
    /// match, group, or capture as its matched text, exactly as the BCL's own <c>ToString</c> defines.
    /// </remarks>
    private static string RenderTextValue(Operand operand) => operand.BclValueKind switch
    {
        BclValueKind.Encoding => ((Encoding)operand.Box!).WebName,
        BclValueKind.Regex => ((Regex)operand.Box!).ToString(),
        BclValueKind.Match => ((Match)operand.Box!).Value,
        BclValueKind.Group => ((Group)operand.Box!).Value,
        BclValueKind.Capture => ((Capture)operand.Box!).Value,
        _ => RenderSequence(MaterializeRegexCollection(operand)),
    };

    // ---- Sequence integration ---------------------------------------------------------------------------------------

    private static bool IsRegexCollection(BclValueKind kind) =>
        kind is BclValueKind.MatchCollection or BclValueKind.GroupCollection or BclValueKind.CaptureCollection;

    /// <summary>
    /// Realizes one regex collection as a sequence of its members, so the LINQ surface — lambdas, quantifiers,
    /// slicing — composes over matches, groups, and captures exactly as over arrays.
    /// </summary>
    private static SequencePayload MaterializeRegexCollection(Operand operand) => operand.BclValueKind switch
    {
        BclValueKind.MatchCollection => new SequencePayload(
            [.. ((MatchCollection)operand.Box!).Select(static match => Operand.FromBclValue(BclValueKind.Match, match))],
            OperandKind.BclValue,
            default,
            "Match"),
        // GroupCollection enumerates both as groups and as name-group pairs; the typed parameter picks groups.
        BclValueKind.GroupCollection => new SequencePayload(
            [
                .. ((GroupCollection)operand.Box!)
                    .Select(static (Group group) => Operand.FromBclValue(BclValueKind.Group, group)),
            ],
            OperandKind.BclValue,
            default,
            "Group"),
        _ => new SequencePayload(
            [
                .. ((CaptureCollection)operand.Box!)
                    .Select(static capture => Operand.FromBclValue(BclValueKind.Capture, capture)),
            ],
            OperandKind.BclValue,
            default,
            "Capture"),
    };

    /// <summary>Builds a byte-array sequence, the operand form of every encoder's output.</summary>
    private static FoldOutcome CreateByteSequence(byte[] bytes) => CreateSequence(new SequencePayload(
        [.. bytes.Select(static value => Operand.FromNumeric(NumericKind.Byte, value))],
        OperandKind.Numeric,
        NumericKind.Byte,
        "Byte"));

    /// <summary>Reads a folded sequence as an exact <c>byte[]</c>; only a Byte element domain qualifies.</summary>
    private static bool TryReadByteSequence(Operand operand, out byte[] bytes)
    {
        bytes = [];
        if (operand.Kind != OperandKind.Sequence)
        {
            return false;
        }

        var payload = PayloadOf(operand);
        if (payload.ElementNumeric != NumericKind.Byte)
        {
            return false;
        }

        bytes = new byte[payload.Items.Length];
        for (var index = 0; index < payload.Items.Length; index++)
        {
            bytes[index] = (byte)BoxOf(payload.Items[index]);
        }

        return true;
    }

    /// <summary>Reads a folded char sequence as an exact <c>char[]</c>.</summary>
    private static bool TryReadCharSequence(Operand operand, out char[] characters)
    {
        characters = [];
        if (operand.Kind != OperandKind.Sequence)
        {
            return false;
        }

        var payload = PayloadOf(operand);
        if (payload.ElementKind != OperandKind.Char)
        {
            return false;
        }

        characters = new char[payload.Items.Length];
        for (var index = 0; index < payload.Items.Length; index++)
        {
            characters[index] = payload.Items[index].Char;
        }

        return true;
    }

    private static FoldOutcome ByteArrayRequired(string member) => FoldOutcome.Error(
        OperandTypeCode,
        $"'{member}' requires a byte[] constant argument; write 'new byte[] {{ … }}' or compose a value whose "
        + "element type is Byte, such as GetBytes' result.");

    // ---- Encoding: type statics -------------------------------------------------------------------------------------

    /// <summary>
    /// The named encoding singletons. Each is a fixed transcoding table of the runtime, so the values are
    /// deterministic; UTF-7 is refused by the BCL itself as insecure and stays a typed stop.
    /// </summary>
    private static FoldOutcome DispatchEncodingStaticProperty(string member) => member switch
    {
        "UTF8" => BclValue(BclValueKind.Encoding, Encoding.UTF8),
        "ASCII" => BclValue(BclValueKind.Encoding, Encoding.ASCII),
        "Unicode" => BclValue(BclValueKind.Encoding, Encoding.Unicode),
        "BigEndianUnicode" => BclValue(BclValueKind.Encoding, Encoding.BigEndianUnicode),
        "UTF32" => BclValue(BclValueKind.Encoding, Encoding.UTF32),
        "Latin1" => BclValue(BclValueKind.Encoding, Encoding.Latin1),
        // Encoding.Default is UTF-8 on every .NET runtime the evaluator runs on, not a machine code page.
        "Default" => BclValue(BclValueKind.Encoding, Encoding.Default),
        "UTF7" => FoldOutcome.Error(
            MemberUnsupportedCode,
            "Encoding.UTF7 is obsolete and insecure, and the runtime refuses it; use Encoding.UTF8."),
        _ => MemberUnsupported($"Encoding.{member}"),
    };

    private static FoldOutcome DispatchEncodingStaticMethod(string name, List<Operand> arguments)
    {
        try
        {
            switch (name, arguments)
            {
                // The reachable set is the runtime's fixed built-in registry: the evaluator never registers a
                // code-page provider, so the same name answers the same encoding on every machine.
                case ("GetEncoding", [{ Kind: OperandKind.String } encodingName]):
                    return BclValue(BclValueKind.Encoding, Encoding.GetEncoding(encodingName.String!));
                case ("GetEncoding", [{ } codePage]) when TryImplicitInt32(codePage, out var page):
                    return BclValue(BclValueKind.Encoding, Encoding.GetEncoding(page));
                case ("Convert", [
                    { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Encoding } source,
                    { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Encoding } destination,
                    { } bytesOperand]):
                    return TryReadByteSequence(bytesOperand, out var bytes)
                        ? CreateByteSequence(Encoding.Convert(
                            (Encoding)source.Box!,
                            (Encoding)destination.Box!,
                            bytes))
                        : ByteArrayRequired("Encoding.Convert");
                default:
                    return MemberUnsupported($"Encoding.{name}");
            }
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return FoldOutcome.Error("System.NotSupportedException", exception.Message);
        }
    }

    // ---- Encoding: instance members ---------------------------------------------------------------------------------

    private static FoldOutcome DispatchEncodingProperty(Encoding encoding, string member) => member switch
    {
        "CodePage" => FoldOutcome.Folded(Operand.FromInt32(encoding.CodePage)),
        "WebName" => FoldOutcome.Folded(Operand.FromString(encoding.WebName)),
        "BodyName" => FoldOutcome.Folded(Operand.FromString(encoding.BodyName)),
        "HeaderName" => FoldOutcome.Folded(Operand.FromString(encoding.HeaderName)),
        "EncodingName" => FoldOutcome.Folded(Operand.FromString(encoding.EncodingName)),
        "IsSingleByte" => FoldOutcome.Folded(Operand.FromBoolean(encoding.IsSingleByte)),
        "Preamble" => CreateByteSequence(encoding.GetPreamble()),
        _ => MemberUnsupported($"Encoding.{member}"),
    };

    private static FoldOutcome DispatchEncodingMethod(Encoding encoding, string name, List<Operand> arguments)
    {
        try
        {
            switch (name, arguments)
            {
                case ("GetBytes", [{ Kind: OperandKind.String } text]):
                    return CreateByteSequence(encoding.GetBytes(text.String!));
                case ("GetBytes", [{ } charsOperand]) when TryReadCharSequence(charsOperand, out var encodedChars):
                    return CreateByteSequence(encoding.GetBytes(encodedChars));
                case ("GetByteCount", [{ Kind: OperandKind.String } text]):
                    return FoldOutcome.Folded(Operand.FromInt32(encoding.GetByteCount(text.String!)));
                case ("GetByteCount", [{ } charsOperand]) when TryReadCharSequence(charsOperand, out var counted):
                    return FoldOutcome.Folded(Operand.FromInt32(encoding.GetByteCount(counted)));
                case ("GetString", [{ } bytesOperand]):
                    return TryReadByteSequence(bytesOperand, out var decoded)
                        ? FoldOutcome.Folded(Operand.FromString(encoding.GetString(decoded)))
                        : ByteArrayRequired($"Encoding.{name}");
                case ("GetString", [{ } bytesOperand, { } indexOperand, { } countOperand])
                    when TryImplicitInt32(indexOperand, out var index) &&
                        TryImplicitInt32(countOperand, out var count):
                    return TryReadByteSequence(bytesOperand, out var window)
                        ? FoldOutcome.Folded(Operand.FromString(encoding.GetString(window, index, count)))
                        : ByteArrayRequired($"Encoding.{name}");
                case ("GetCharCount", [{ } bytesOperand]):
                    return TryReadByteSequence(bytesOperand, out var charCounted)
                        ? FoldOutcome.Folded(Operand.FromInt32(encoding.GetCharCount(charCounted)))
                        : ByteArrayRequired($"Encoding.{name}");
                case ("GetChars", [{ } bytesOperand]):
                    return TryReadByteSequence(bytesOperand, out var charSource)
                        ? CreateCharSequence(encoding.GetChars(charSource))
                        : ByteArrayRequired($"Encoding.{name}");
                case ("GetPreamble", []):
                    return CreateByteSequence(encoding.GetPreamble());
                case ("GetMaxByteCount", [{ } charCount]) when TryImplicitInt32(charCount, out var maxChars):
                    return FoldOutcome.Folded(Operand.FromInt32(encoding.GetMaxByteCount(maxChars)));
                case ("GetMaxCharCount", [{ } byteCount]) when TryImplicitInt32(byteCount, out var maxBytes):
                    return FoldOutcome.Folded(Operand.FromInt32(encoding.GetMaxCharCount(maxBytes)));
                case ("Equals", [{ Kind: OperandKind.BclValue, BclValueKind: BclValueKind.Encoding } other]):
                    return FoldOutcome.Folded(Operand.FromBoolean(encoding.Equals(other.Box)));
                case ("Equals", [{ Kind: OperandKind.Null }]):
                    return FoldOutcome.Folded(Operand.FromBoolean(false));
                default:
                    return MemberUnsupported($"Encoding.{name}");
            }
        }
        catch (ArgumentException exception)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
        catch (Exception exception) when (exception is DecoderFallbackException or EncoderFallbackException)
        {
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }

    // ---- Regex: construction and guards -----------------------------------------------------------------------------

    /// <summary>
    /// Runs one regular-expression computation with the family's exception vocabulary mapped to typed stops:
    /// a malformed pattern, an unsupported option combination, and the fixed matching budget.
    /// </summary>
    private static FoldOutcome GuardRegex(Func<FoldOutcome> evaluate)
    {
        try
        {
            return evaluate();
        }
        catch (RegexMatchTimeoutException)
        {
            return FoldOutcome.Error(
                "System.Text.RegularExpressions.RegexMatchTimeoutException",
                "The pattern exceeded the evaluator's one-second matching budget; rewrite it to avoid "
                + "catastrophic backtracking, or add RegexOptions.NonBacktracking.");
        }
        catch (NotSupportedException exception)
        {
            return FoldOutcome.Error("System.NotSupportedException", exception.Message);
        }
        catch (ArgumentException exception)
        {
            // RegexParseException derives from ArgumentException; the exact runtime type names the stop.
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }

    /// <summary>Reads one options argument as an exact <see cref="RegexOptions"/> value.</summary>
    private static FoldOutcome WithRegexOptions(Operand operand, Func<RegexOptions, FoldOutcome> evaluate)
    {
        if (operand is not { Kind: OperandKind.Enum, EnumTypeFullName: RegexOptionsFullName })
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "The options argument must be a System.Text.RegularExpressions.RegexOptions enum value.");
        }

        return evaluate((RegexOptions)unchecked((int)operand.EnumBits));
    }

    /// <summary>
    /// Builds the <see cref="Regex"/> one fold computes with, under the deterministic-options contract: case
    /// folding depends on the analysis machine's culture unless <see cref="RegexOptions.CultureInvariant"/>
    /// pins it, so IgnoreCase without CultureInvariant is a typed culture stop, never a machine-dependent answer.
    /// </summary>
    private static FoldOutcome FoldWithRegex(string pattern, RegexOptions options, Func<Regex, FoldOutcome> run)
    {
        if (options.HasFlag(RegexOptions.IgnoreCase) && !options.HasFlag(RegexOptions.CultureInvariant))
        {
            return CultureSensitive(
                "RegexOptions.IgnoreCase without RegexOptions.CultureInvariant",
                "RegexOptions.IgnoreCase | RegexOptions.CultureInvariant");
        }

        return GuardRegex(() => run(new Regex(pattern, options, RegexFoldTimeout)));
    }

    /// <summary>Folds <c>new Regex(pattern)</c> and <c>new Regex(pattern, options)</c>.</summary>
    private static FoldOutcome FoldRegexCreation(List<Operand> arguments) => arguments switch
    {
        [{ Kind: OperandKind.String } pattern] =>
            FoldWithRegex(pattern.String!, RegexOptions.None, static regex =>
                BclValue(BclValueKind.Regex, regex)),
        [{ Kind: OperandKind.String } pattern, { } options] =>
            WithRegexOptions(options, resolved => FoldWithRegex(pattern.String!, resolved, static regex =>
                BclValue(BclValueKind.Regex, regex))),
        _ => FoldOutcome.Error(
            MemberUnsupportedCode,
            "This Regex constructor shape is not admitted; use new Regex(pattern) or new Regex(pattern, options)."),
    };

    /// <summary>
    /// Boxes one fully evaluated match collection. Counting forces the lazy matcher to run to completion under
    /// the budget, so every later member read is a pure read of finished evidence.
    /// </summary>
    private static FoldOutcome CreateMatchCollectionValue(MatchCollection matches)
    {
        var count = matches.Count;
        if (count > MaximumSequenceLength)
        {
            return FoldOutcome.Error(
                SequenceBoundCode,
                $"The pattern produced {count.ToString(CultureInfo.InvariantCulture)} matches, beyond the "
                + $"deterministic bound of {MaximumSequenceLength.ToString(CultureInfo.InvariantCulture)}.");
        }

        return BclValue(BclValueKind.MatchCollection, matches);
    }

    // ---- Regex: type statics ----------------------------------------------------------------------------------------

    /// <summary>
    /// The static <see cref="Regex"/> surface. Every method is the corresponding instance operation over a
    /// regex constructed from the pattern and options arguments, exactly as the BCL defines the statics.
    /// </summary>
    private static FoldOutcome DispatchRegexStaticMethod(string name, List<Operand> arguments)
    {
        switch (name, arguments)
        {
            case ("Escape", [{ Kind: OperandKind.String } text]):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromString(Regex.Escape(text.String!))));
            case ("Unescape", [{ Kind: OperandKind.String } text]):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromString(Regex.Unescape(text.String!))));
            case ("IsMatch", [{ Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern]):
                return FoldWithRegex(pattern.String!, RegexOptions.None, regex =>
                    FoldOutcome.Folded(Operand.FromBoolean(regex.IsMatch(input.String!))));
            case ("IsMatch", [
                { Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern, { } options]):
                return WithRegexOptions(options, resolved => FoldWithRegex(pattern.String!, resolved, regex =>
                    FoldOutcome.Folded(Operand.FromBoolean(regex.IsMatch(input.String!)))));
            case ("Match", [{ Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern]):
                return FoldWithRegex(pattern.String!, RegexOptions.None, regex =>
                    BclValue(BclValueKind.Match, regex.Match(input.String!)));
            case ("Match", [
                { Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern, { } options]):
                return WithRegexOptions(options, resolved => FoldWithRegex(pattern.String!, resolved, regex =>
                    BclValue(BclValueKind.Match, regex.Match(input.String!))));
            case ("Matches", [{ Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern]):
                return FoldWithRegex(pattern.String!, RegexOptions.None, regex =>
                    CreateMatchCollectionValue(regex.Matches(input.String!)));
            case ("Matches", [
                { Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern, { } options]):
                return WithRegexOptions(options, resolved => FoldWithRegex(pattern.String!, resolved, regex =>
                    CreateMatchCollectionValue(regex.Matches(input.String!))));
            case ("Replace", [
                { Kind: OperandKind.String } input,
                { Kind: OperandKind.String } pattern,
                { Kind: OperandKind.String } replacement]):
                return FoldWithRegex(pattern.String!, RegexOptions.None, regex =>
                    FoldOutcome.Folded(Operand.FromString(regex.Replace(input.String!, replacement.String!))));
            case ("Replace", [
                { Kind: OperandKind.String } input,
                { Kind: OperandKind.String } pattern,
                { Kind: OperandKind.String } replacement,
                { } options]):
                return WithRegexOptions(options, resolved => FoldWithRegex(pattern.String!, resolved, regex =>
                    FoldOutcome.Folded(Operand.FromString(regex.Replace(input.String!, replacement.String!)))));
            case ("Split", [{ Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern]):
                return FoldWithRegex(pattern.String!, RegexOptions.None, regex =>
                    CreateStringSequence(regex.Split(input.String!)));
            case ("Split", [
                { Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern, { } options]):
                return WithRegexOptions(options, resolved => FoldWithRegex(pattern.String!, resolved, regex =>
                    CreateStringSequence(regex.Split(input.String!))));
            case ("Count", [{ Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern]):
                return FoldWithRegex(pattern.String!, RegexOptions.None, regex =>
                    FoldOutcome.Folded(Operand.FromInt32(regex.Count(input.String!))));
            case ("Count", [
                { Kind: OperandKind.String } input, { Kind: OperandKind.String } pattern, { } options]):
                return WithRegexOptions(options, resolved => FoldWithRegex(pattern.String!, resolved, regex =>
                    FoldOutcome.Folded(Operand.FromInt32(regex.Count(input.String!)))));
            default:
                return MemberUnsupported($"Regex.{name}");
        }
    }

    // ---- Regex family: instance members -----------------------------------------------------------------------------

    /// <summary>Routes one instance property over the text-family kinds.</summary>
    private static FoldOutcome DispatchTextValueProperty(Operand receiver, string member)
    {
        switch (receiver.BclValueKind)
        {
            case BclValueKind.Encoding:
                return DispatchEncodingProperty((Encoding)receiver.Box!, member);
            case BclValueKind.Regex:
                var regex = (Regex)receiver.Box!;
                return member switch
                {
                    "Options" => FoldOutcome.Folded(Operand.FromEnum(
                        (int)regex.Options,
                        RegexOptionsFullName,
                        regex.Options.ToString())),
                    "RightToLeft" => FoldOutcome.Folded(Operand.FromBoolean(regex.RightToLeft)),
                    _ => MemberUnsupported($"Regex.{member}"),
                };
            case BclValueKind.Match:
                var match = (Match)receiver.Box!;
                return member switch
                {
                    "Groups" => BclValue(BclValueKind.GroupCollection, match.Groups),
                    _ => DispatchGroupProperty(match, "Match", member),
                };
            case BclValueKind.Group:
                var group = (Group)receiver.Box!;
                return member switch
                {
                    "Captures" => BclValue(BclValueKind.CaptureCollection, group.Captures),
                    _ => DispatchGroupProperty(group, "Group", member),
                };
            case BclValueKind.Capture:
                var capture = (Capture)receiver.Box!;
                return member switch
                {
                    "Value" => FoldOutcome.Folded(Operand.FromString(capture.Value)),
                    "Index" => FoldOutcome.Folded(Operand.FromInt32(capture.Index)),
                    "Length" => FoldOutcome.Folded(Operand.FromInt32(capture.Length)),
                    _ => MemberUnsupported($"Capture.{member}"),
                };
            default:
                return member == "Count"
                    ? FoldOutcome.Folded(Operand.FromInt32(receiver.BclValueKind switch
                    {
                        BclValueKind.MatchCollection => ((MatchCollection)receiver.Box!).Count,
                        BclValueKind.GroupCollection => ((GroupCollection)receiver.Box!).Count,
                        _ => ((CaptureCollection)receiver.Box!).Count,
                    }))
                    : MemberUnsupported($"{receiver.BclValueKind}.{member}");
        }
    }

    /// <summary>The shared property surface of <see cref="Group"/>, which <see cref="Match"/> extends.</summary>
    private static FoldOutcome DispatchGroupProperty(Group group, string kindName, string member) => member switch
    {
        "Success" => FoldOutcome.Folded(Operand.FromBoolean(group.Success)),
        "Value" => FoldOutcome.Folded(Operand.FromString(group.Value)),
        "Index" => FoldOutcome.Folded(Operand.FromInt32(group.Index)),
        "Length" => FoldOutcome.Folded(Operand.FromInt32(group.Length)),
        "Name" => FoldOutcome.Folded(Operand.FromString(group.Name)),
        _ => MemberUnsupported($"{kindName}.{member}"),
    };

    /// <summary>Routes one instance method over the text-family kinds.</summary>
    private static FoldOutcome DispatchTextValueMethod(Operand receiver, string name, List<Operand> arguments)
    {
        switch (receiver.BclValueKind)
        {
            case BclValueKind.Encoding:
                return DispatchEncodingMethod((Encoding)receiver.Box!, name, arguments);
            case BclValueKind.Regex:
                return DispatchRegexMethod((Regex)receiver.Box!, name, arguments);
            case BclValueKind.Match:
                var match = (Match)receiver.Box!;
                return (name, arguments) switch
                {
                    ("NextMatch", []) => GuardRegex(() => BclValue(BclValueKind.Match, match.NextMatch())),
                    ("Result", [{ Kind: OperandKind.String } replacement]) =>
                        GuardRegex(() => FoldOutcome.Folded(Operand.FromString(
                            match.Result(replacement.String!)))),
                    ("ToString", []) => FoldOutcome.Folded(Operand.FromString(match.Value)),
                    _ => MemberUnsupported($"Match.{name}"),
                };
            case BclValueKind.Group:
                return name == "ToString" && arguments.Count == 0
                    ? FoldOutcome.Folded(Operand.FromString(((Group)receiver.Box!).Value))
                    : MemberUnsupported($"Group.{name}");
            case BclValueKind.Capture:
                return name == "ToString" && arguments.Count == 0
                    ? FoldOutcome.Folded(Operand.FromString(((Capture)receiver.Box!).Value))
                    : MemberUnsupported($"Capture.{name}");
            default:
                // The collections answer everything else through the sequence surface, so quantifiers and
                // element operators work over matches exactly as over arrays.
                return DispatchSequence(
                    Operand.FromSequence(MaterializeRegexCollection(receiver)),
                    name,
                    arguments);
        }
    }

    private static FoldOutcome DispatchRegexMethod(Regex regex, string name, List<Operand> arguments)
    {
        switch (name, arguments)
        {
            case ("IsMatch", [{ Kind: OperandKind.String } input]):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromBoolean(regex.IsMatch(input.String!))));
            case ("IsMatch", [{ Kind: OperandKind.String } input, { } start])
                when TryImplicitInt32(start, out var isMatchStart):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromBoolean(
                    regex.IsMatch(input.String!, isMatchStart))));
            case ("Match", [{ Kind: OperandKind.String } input]):
                return GuardRegex(() => BclValue(BclValueKind.Match, regex.Match(input.String!)));
            case ("Match", [{ Kind: OperandKind.String } input, { } start])
                when TryImplicitInt32(start, out var matchStart):
                return GuardRegex(() => BclValue(BclValueKind.Match, regex.Match(input.String!, matchStart)));
            case ("Match", [{ Kind: OperandKind.String } input, { } start, { } length])
                when TryImplicitInt32(start, out var windowStart) && TryImplicitInt32(length, out var windowLength):
                return GuardRegex(() => BclValue(
                    BclValueKind.Match,
                    regex.Match(input.String!, windowStart, windowLength)));
            case ("Matches", [{ Kind: OperandKind.String } input]):
                return GuardRegex(() => CreateMatchCollectionValue(regex.Matches(input.String!)));
            case ("Matches", [{ Kind: OperandKind.String } input, { } start])
                when TryImplicitInt32(start, out var matchesStart):
                return GuardRegex(() => CreateMatchCollectionValue(regex.Matches(input.String!, matchesStart)));
            case ("Replace", [{ Kind: OperandKind.String } input, { Kind: OperandKind.String } replacement]):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromString(
                    regex.Replace(input.String!, replacement.String!))));
            case ("Replace", [
                { Kind: OperandKind.String } input, { Kind: OperandKind.String } replacement, { } count])
                when TryImplicitInt32(count, out var replaceCount):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromString(
                    regex.Replace(input.String!, replacement.String!, replaceCount))));
            case ("Split", [{ Kind: OperandKind.String } input]):
                return GuardRegex(() => CreateStringSequence(regex.Split(input.String!)));
            case ("Split", [{ Kind: OperandKind.String } input, { } count])
                when TryImplicitInt32(count, out var splitCount):
                return GuardRegex(() => CreateStringSequence(regex.Split(input.String!, splitCount)));
            case ("Count", [{ Kind: OperandKind.String } input]):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromInt32(regex.Count(input.String!))));
            case ("GetGroupNames", []):
                return CreateStringSequence(regex.GetGroupNames());
            case ("GetGroupNumbers", []):
                return CreateSequence(new SequencePayload(
                    [.. regex.GetGroupNumbers().Select(Operand.FromInt32)],
                    OperandKind.Int32,
                    NumericKind.Int32,
                    "Int32"));
            case ("GroupNameFromNumber", [{ } number]) when TryImplicitInt32(number, out var groupNumber):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromString(
                    regex.GroupNameFromNumber(groupNumber))));
            case ("GroupNumberFromName", [{ Kind: OperandKind.String } groupName]):
                return GuardRegex(() => FoldOutcome.Folded(Operand.FromInt32(
                    regex.GroupNumberFromName(groupName.String!))));
            case ("ToString", []):
                return FoldOutcome.Folded(Operand.FromString(regex.ToString()));
            default:
                return MemberUnsupported($"Regex.{name}");
        }
    }

    // ---- Regex collections: element access --------------------------------------------------------------------------

    /// <summary>
    /// Indexes one regex collection with BCL semantics: a group collection answers unknown names and numbers
    /// with the empty non-success group, exactly as <see cref="GroupCollection"/> defines, while match and
    /// capture positions are bounds-checked.
    /// </summary>
    private static FoldOutcome DispatchBclValueElementAccess(
        Operand receiver,
        ElementAccessExpressionSyntax elementAccess,
        FoldContext context)
    {
        if (!IsRegexCollection(receiver.BclValueKind))
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Constant element access requires one string, array, or regex-collection receiver.");
        }

        if (elementAccess.ArgumentList.Arguments.Count != 1)
        {
            return FoldOutcome.Error(OperandTypeCode, "Regex collection element access takes one index.");
        }

        var argument = Fold(elementAccess.ArgumentList.Arguments[0].Expression, context);
        if (argument.Disposition != FoldDisposition.Folded)
        {
            return argument;
        }

        if (receiver.BclValueKind == BclValueKind.GroupCollection &&
            argument.Operand.Kind == OperandKind.String)
        {
            return BclValue(BclValueKind.Group, ((GroupCollection)receiver.Box!)[argument.Operand.String!]);
        }

        if (!TryImplicitInt32(argument.Operand, out var index))
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                receiver.BclValueKind == BclValueKind.GroupCollection
                    ? "A group index must be an Int32 constant or a group-name string."
                    : "A regex collection index must be an Int32 constant.");
        }

        if (receiver.BclValueKind == BclValueKind.GroupCollection)
        {
            return BclValue(BclValueKind.Group, ((GroupCollection)receiver.Box!)[index]);
        }

        var count = receiver.BclValueKind == BclValueKind.MatchCollection
            ? ((MatchCollection)receiver.Box!).Count
            : ((CaptureCollection)receiver.Box!).Count;
        if (index < 0 || index >= count)
        {
            return FoldOutcome.Error(
                ArgumentOutOfRangeCode,
                $"Index {index.ToString(CultureInfo.InvariantCulture)} is outside the collection of count "
                + $"{count.ToString(CultureInfo.InvariantCulture)}.");
        }

        return receiver.BclValueKind == BclValueKind.MatchCollection
            ? BclValue(BclValueKind.Match, ((MatchCollection)receiver.Box!)[index])
            : BclValue(BclValueKind.Capture, ((CaptureCollection)receiver.Box!)[index]);
    }
}
