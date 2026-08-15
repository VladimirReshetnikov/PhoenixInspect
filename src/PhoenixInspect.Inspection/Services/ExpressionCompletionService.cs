using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>Classifies one completion item for iconography and ordering.</summary>
public enum CompletionItemKind
{
    /// <summary>A C# keyword.</summary>
    Keyword = 0,

    /// <summary>A type or type-receiver name the evaluator models.</summary>
    Type = 1,

    /// <summary>A namespace segment read from dump-module metadata.</summary>
    Namespace = 2,

    /// <summary>A member of a modeled receiver.</summary>
    Member = 3,

    /// <summary>A field name read from dump evidence: the root object's runtime type or module metadata.</summary>
    Field = 4,

    /// <summary>The adopted root identifier.</summary>
    Root = 5,

    /// <summary>A variable declared in the immediate window.</summary>
    Local = 6,
}

/// <summary>
/// The editor-specific completion facts one query runs under: the declared immediate variables, and whether the
/// editor admits statements, which offers the statement keywords at the start of a line.
/// </summary>
public sealed record CompletionContext
{
    /// <summary>Gets the plain expression context: no locals, no statements. Watch entries use this.</summary>
    public static CompletionContext Expression { get; } = new();

    /// <summary>Gets the editor's declared variables, completed as identifiers.</summary>
    public ImmutableArray<CompletionItem> Locals { get; init; } = [];

    /// <summary>Gets whether the editor admits statements, such as the immediate window's declarations.</summary>
    public bool AllowsStatements { get; init; }

    /// <summary>
    /// Gets the active using directives: imported namespaces and aliases let dump-metadata names complete and
    /// resolve without their prefixes, and static imports offer their members as bare identifiers.
    /// </summary>
    public UsingDirectiveSet Usings { get; init; } = UsingDirectiveSet.Empty;

    /// <summary>Gets the '#r'-referenced assemblies' completion facts, or null with no references.</summary>
    public ReferenceCompletionIndex? References { get; init; }
}

/// <summary>One parameter of a shown method signature.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="TypeText">The parameter type, spelled the C# way.</param>
public sealed record SignatureParameter(string Name, string TypeText);

/// <summary>One method overload shown by signature help.</summary>
/// <param name="MethodName">The method name.</param>
/// <param name="Parameters">The parameters, in order.</param>
/// <param name="ReturnTypeText">The return type, spelled the C# way.</param>
public sealed record MethodSignature(
    string MethodName,
    ImmutableArray<SignatureParameter> Parameters,
    string ReturnTypeText);

/// <summary>The signature help for one call in progress.</summary>
/// <param name="ReceiverDisplay">The receiver's display name, such as <c>Math</c> or <c>String</c>.</param>
/// <param name="Signatures">The modeled method's overloads.</param>
/// <param name="ActiveParameter">The zero-based parameter the caret sits in, by comma count.</param>
/// <param name="OpenParenOffset">The offset of the call's opening parenthesis, for popup alignment.</param>
public sealed record SignatureHelp(
    string ReceiverDisplay,
    ImmutableArray<MethodSignature> Signatures,
    int ActiveParameter,
    int OpenParenOffset);

/// <summary>
/// The completion facts of the immediate window's <c>#r</c> references: top-level type names scanned once at
/// construction, and static-member lists realized synchronously on first use — a reference's metadata is retained
/// local bytes, so no session round-trip is needed. An aliased reference is reachable only through its extern
/// alias, which the completion grammar never spells, so it contributes nothing here.
/// </summary>
/// <remarks>The realized-member cache is not thread-safe; one editor consults it from its UI thread.</remarks>
public sealed class ReferenceCompletionIndex
{
    private readonly ImmutableArray<ReferenceAssembly> globalReferences;
    private readonly Dictionary<string, ImmutableArray<CompletionItem>> realizedMembers = new(StringComparer.Ordinal);

    /// <summary>Gets the empty index.</summary>
    public static ReferenceCompletionIndex Empty { get; } = new([]);

    /// <summary>Builds the index over the current references.</summary>
    /// <param name="references">The referenced assemblies; aliased ones are skipped.</param>
    /// <exception cref="ArgumentNullException"><paramref name="references"/> is default.</exception>
    public ReferenceCompletionIndex(ImmutableArray<ReferenceAssembly> references)
    {
        if (references.IsDefault)
        {
            throw new ArgumentNullException(nameof(references));
        }

        globalReferences = [.. references.Where(static reference => reference.Alias is null)];
        var typeNames = ImmutableArray.CreateBuilder<string>();
        foreach (var reference in globalReferences)
        {
            if (typeNames.Count >= ExpressionCompletionService.MaximumCatalogTypes)
            {
                break;
            }

            try
            {
                using var provider = MetadataReaderProvider.FromMetadataImage(reference.MetadataBytes);
                ExpressionCompletionService.CollectTopLevelTypeNames(
                    provider.GetMetadataReader(), typeNames, ExpressionCompletionService.MaximumCatalogTypes);
            }
            catch (BadImageFormatException)
            {
                // A malformed image contributes no names.
            }
        }

        TypeFullNames = [.. typeNames.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Gets whether the index carries no names.</summary>
    public bool IsEmpty => TypeFullNames.IsEmpty;

    /// <summary>Gets the top-level type full names of the global-scope references, bounded.</summary>
    public ImmutableArray<string> TypeFullNames { get; }

    /// <summary>Reads one referenced type's static-field completions, cached after the first scan.</summary>
    /// <param name="typeFullName">The dotted full name of a top-level type.</param>
    /// <returns>The static-field completions, possibly empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeFullName"/> is null.</exception>
    public ImmutableArray<CompletionItem> GetStaticMembers(string typeFullName)
    {
        ArgumentNullException.ThrowIfNull(typeFullName);
        if (realizedMembers.TryGetValue(typeFullName, out var cached))
        {
            return cached;
        }

        var separator = typeFullName.LastIndexOf('.');
        var typeNamespace = separator < 0 ? string.Empty : typeFullName[..separator];
        var typeName = separator < 0 ? typeFullName : typeFullName[(separator + 1)..];
        var members = ImmutableArray.CreateBuilder<CompletionItem>();
        foreach (var reference in globalReferences)
        {
            try
            {
                using var provider = MetadataReaderProvider.FromMetadataImage(reference.MetadataBytes);
                ExpressionCompletionService.CollectStaticMemberCompletions(
                    provider.GetMetadataReader(), typeNamespace, typeName, members);
            }
            catch (BadImageFormatException)
            {
                // A malformed image contributes no members.
            }
        }

        var realized = ExpressionCompletionService.FinishMemberList(members);
        realizedMembers[typeFullName] = realized;
        return realized;
    }
}

/// <summary>One completion candidate.</summary>
/// <param name="Text">The exact text inserted on acceptance.</param>
/// <param name="Kind">The item's classification.</param>
/// <param name="Detail">An optional short annotation, such as the field's type name.</param>
public sealed record CompletionItem(string Text, CompletionItemKind Kind, string? Detail = null);

/// <summary>The outcome of one completion query.</summary>
/// <param name="Items">The candidates, filtered by the partial token and ordered for display.</param>
/// <param name="ReplaceStart">The offset of the partial token the accepted item replaces.</param>
/// <param name="ReplaceLength">The length of the partial token.</param>
/// <param name="PendingTypeMembers">
/// The full name of a metadata type whose static members the catalog has not realized yet, or null. A host fetches
/// them with <see cref="ExpressionCompletionService.ListStaticMemberCompletions"/> and re-queries.
/// </param>
/// <param name="PendingInstanceMembers">
/// The full name of a runtime type whose instance fields the catalog has not realized yet, or null. A host fetches
/// them with <see cref="ExpressionCompletionService.ListInstanceMemberCompletions"/> and re-queries.
/// </param>
public sealed record CompletionResult(
    ImmutableArray<CompletionItem> Items,
    int ReplaceStart,
    int ReplaceLength,
    string? PendingTypeMembers = null,
    string? PendingInstanceMembers = null)
{
    /// <summary>Gets the empty result.</summary>
    public static CompletionResult Empty { get; } = new([], 0, 0);

    /// <summary>Applies one accepted item to the text this result was computed over.</summary>
    /// <param name="text">The expression text the completion query ran against.</param>
    /// <param name="item">The accepted item.</param>
    /// <returns>The new text with the partial token replaced, and the caret offset after the insertion.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public (string NewText, int NewCaretOffset) Apply(string text, CompletionItem item)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(item);
        var start = Math.Clamp(ReplaceStart, 0, text.Length);
        var end = Math.Clamp(start + ReplaceLength, start, text.Length);
        return (text[..start] + item.Text + text[end..], start + item.Text.Length);
    }
}

/// <summary>
/// The session-derived completion facts a host caches and hands back to every completion query: the adopted root's
/// declared field names, and the namespaces and top-level type names read from dump-module metadata.
/// </summary>
public sealed record CompletionCatalog
{
    /// <summary>Gets the catalog with no session-derived facts; keyword and receiver completion still works.</summary>
    public static CompletionCatalog Empty { get; } = new();

    /// <summary>Gets the case-sensitive identifier root-relative expressions use for the root.</summary>
    public string RootIdentifier { get; init; } = "root";

    /// <summary>Gets whether a root is adopted, which admits the root identifier and its members.</summary>
    public bool HasRoot { get; init; }

    /// <summary>Gets the adopted root object's declared field names, from its validated runtime type.</summary>
    public ImmutableArray<CompletionItem> RootMembers { get; init; } = [];

    /// <summary>Gets the top-level type full names read from dump-module metadata, bounded.</summary>
    public ImmutableArray<string> TypeFullNames { get; init; } = [];

    /// <summary>Gets the realized static-member completions, keyed by type full name.</summary>
    public ImmutableDictionary<string, ImmutableArray<CompletionItem>> TypeMembers { get; init; } =
        ImmutableDictionary<string, ImmutableArray<CompletionItem>>.Empty;

    /// <summary>
    /// Gets the realized instance-field completions along root member chains, keyed by runtime type full name.
    /// An empty array is a realized answer too — the type contributed no spellable fields.
    /// </summary>
    public ImmutableDictionary<string, ImmutableArray<CompletionItem>> TypeInstanceMembers { get; init; } =
        ImmutableDictionary<string, ImmutableArray<CompletionItem>>.Empty;
}

/// <summary>
/// Computes keyword, identifier, and member completions for watch expressions, the way Visual Studio's Watch
/// window completes as you type. The candidate universe is the evaluator's own: C# keywords the expression grammar
/// admits, the modeled type receivers and their dispatched members, the adopted root's declared fields, the
/// immediate window's declared variables, and the namespaces, types, and static fields read from dump-module
/// metadata. Completion never invents a name — every candidate either evaluates or produces its own explained stop.
/// Matching follows the IDE conventions: a typed token matches by prefix first, then by camel humps
/// (<c>DTO</c> finds <c>DateTimeOffset</c>), then by substring, and items order by that match quality.
/// </summary>
public static class ExpressionCompletionService
{
    /// <summary>The greatest number of items one completion query returns.</summary>
    public const int MaximumItems = 256;

    /// <summary>The greatest number of type names the catalog realizes from module metadata.</summary>
    public const int MaximumCatalogTypes = 8_192;

    private static readonly ImmutableArray<string> Keywords =
    [
        "and", "bool", "byte", "char", "checked", "decimal", "default", "double", "dynamic", "false", "float",
        "global", "int", "is", "long", "nameof", "new", "nint", "not", "nuint", "null", "object", "or", "sbyte",
        "short", "sizeof", "string", "switch", "true", "typeof", "uint", "ulong", "unchecked", "ushort", "when",
    ];

    // Statement keywords complete only at the start of a line, and only in editors that admit statements: the
    // immediate window's declarations ('var x = …') and scope directives ('using System;').
    private static readonly ImmutableArray<CompletionItem> StatementKeywords =
    [
        new("using", CompletionItemKind.Keyword, "import a namespace"),
        new("var", CompletionItemKind.Keyword, "declare a variable"),
    ];

    private static readonly ImmutableArray<string> ModeledTypeNames =
    [
        "Action", "Activator", "Array", "BigInteger", "CharUnicodeInfo", "Comparison", "Convert", "DateOnly",
        "DateTime", "DateTimeKind", "DateTimeOffset", "DayOfWeek", "DBNull", "Delegate", "Dictionary", "Encoding",
        "Enum", "Enumerable",
        "Func", "Guid", "Half", "ICollection", "IComparable", "IDictionary", "IEnumerable", "IEquatable",
        "IList", "ImmutableArray", "ImmutableDictionary", "ImmutableHashSet", "ImmutableList", "ImmutableQueue",
        "ImmutableSortedDictionary", "ImmutableSortedSet",
        "ImmutableStack", "Int128", "IntPtr", "IReadOnlyCollection", "IReadOnlyDictionary", "IReadOnlyList",
        "KeyValuePair", "List", "Math", "MathF", "NFloat",
        "MemberTypes", "MulticastDelegate", "Nullable", "Object", "Predicate", "Regex", "RegexOptions", "Rune",
        "String", "StringComparison", "StringSplitOptions", "TimeOnly", "TimeSpan", "TypeCode", "UInt128",
        "UIntPtr", "UnicodeCategory", "ValueType", "Version",
    ];

    // Members mirror the evaluator's dispatch tables; a member that is a deliberate typed stop (Now, NewGuid,
    // TryParse, the Array mutators) still completes, because the stop itself is part of the documented surface.
    private static readonly ImmutableDictionary<string, ImmutableArray<string>> ReceiverMembers =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["Math"] =
            [
                "Abs", "Acos", "Acosh", "Asin", "Asinh", "Atan", "Atan2", "Atanh", "BitDecrement", "BitIncrement",
                "Cbrt", "Ceiling", "Clamp", "CopySign", "Cos", "Cosh", "E", "Exp", "Floor", "FusedMultiplyAdd",
                "IEEERemainder", "ILogB", "Log", "Log10", "Log2", "Max", "Min", "PI", "Pow", "ReciprocalEstimate",
                "ReciprocalSqrtEstimate", "Round", "ScaleB", "Sign", "Sin", "Sinh", "Sqrt", "Tan", "Tanh", "Tau",
                "Truncate",
            ],
            ["MathF"] =
            [
                "Abs", "Acos", "Acosh", "Asin", "Asinh", "Atan", "Atan2", "Atanh", "BitDecrement", "BitIncrement",
                "Cbrt", "Ceiling", "CopySign", "Cos", "Cosh", "E", "Exp", "Floor", "FusedMultiplyAdd",
                "IEEERemainder", "ILogB", "Log", "Log10", "Log2", "Max", "Min", "PI", "Pow", "ReciprocalEstimate",
                "ReciprocalSqrtEstimate", "Round", "ScaleB", "Sign", "Sin", "Sinh", "Sqrt", "Tan", "Tanh", "Tau",
                "Truncate",
            ],
            ["Enumerable"] = ["Empty", "Range", "Repeat"],
            ["ImmutableArray"] = ["Create", "CreateRange", "Empty"],
            ["ImmutableList"] = ["Create", "CreateRange", "Empty"],
            ["ImmutableHashSet"] = ["Create", "CreateRange", "Empty"],
            ["ImmutableSortedSet"] = ["Create", "CreateRange", "Empty"],
            ["ImmutableQueue"] = ["Create", "CreateRange", "Empty"],
            ["ImmutableStack"] = ["Create", "CreateRange", "Empty"],
            ["ImmutableDictionary"] = ["Create", "CreateRange", "Empty"],
            ["ImmutableSortedDictionary"] = ["Create", "CreateRange", "Empty"],
            ["KeyValuePair"] = ["Create"],
            ["Enum"] =
            [
                "GetName", "GetNames", "GetUnderlyingType", "GetValues", "IsDefined", "Parse", "ToObject",
                "TryParse",
            ],
            ["Array"] =
            [
                "BinarySearch", "Clear", "ConstrainedCopy", "ConvertAll", "Copy", "CreateInstance", "Empty",
                "Exists", "Fill", "Find", "FindAll", "FindIndex", "FindLast", "FindLastIndex", "IndexOf",
                "LastIndexOf", "MaxLength", "Resize", "Reverse", "SetValue", "Sort", "TrueForAll",
            ],
            ["DateTime"] =
            ["MaxValue", "MinValue", "Now", "Parse", "ParseExact", "Today", "TryParse", "UnixEpoch", "UtcNow"],
            ["DateTimeOffset"] =
            [
                "FromUnixTimeMilliseconds", "FromUnixTimeSeconds", "MaxValue", "MinValue", "Now", "Parse",
                "UnixEpoch", "UtcNow",
            ],
            ["TimeSpan"] =
            [
                "FromDays", "FromHours", "FromMilliseconds", "FromMinutes", "FromSeconds", "FromTicks", "MaxValue",
                "MinValue", "Parse", "TicksPerDay", "TicksPerHour", "TicksPerMicrosecond", "TicksPerMillisecond",
                "TicksPerMinute", "TicksPerSecond", "Zero",
            ],
            ["DateOnly"] = ["FromDateTime", "FromDayNumber", "MaxValue", "MinValue", "Parse"],
            ["TimeOnly"] = ["FromDateTime", "FromTimeSpan", "MaxValue", "MinValue", "Parse"],
            ["Guid"] = ["Empty", "NewGuid", "Parse", "ParseExact"],
            ["Version"] = ["Parse"],
            ["Encoding"] =
            [
                "ASCII", "BigEndianUnicode", "Convert", "Default", "GetEncoding", "Latin1", "Unicode", "UTF32",
                "UTF8",
            ],
            ["Regex"] = ["Count", "Escape", "IsMatch", "Match", "Matches", "Replace", "Split", "Unescape"],
            ["Activator"] = ["CreateInstance"],
            ["Delegate"] = ["Combine", "CreateDelegate", "Remove", "RemoveAll"],
            ["MulticastDelegate"] = ["Combine", "CreateDelegate", "Remove", "RemoveAll"],
            ["Rune"] =
            [
                "GetNumericValue", "GetRuneAt", "GetUnicodeCategory", "IsControl", "IsDigit", "IsLetter",
                "IsLetterOrDigit", "IsLower", "IsNumber", "IsPunctuation", "IsSeparator", "IsSymbol", "IsUpper",
                "IsValid", "IsWhiteSpace", "ReplacementChar", "ToLowerInvariant", "ToUpperInvariant",
            ],
            ["CharUnicodeInfo"] =
            ["GetDecimalDigitValue", "GetDigitValue", "GetNumericValue", "GetUnicodeCategory"],
            ["Convert"] =
            [
                "ChangeType", "DBNull", "FromBase64String", "FromHexString", "GetTypeCode", "IsDBNull",
                "ToBase64String", "ToBoolean", "ToByte", "ToChar", "ToDateTime", "ToDecimal", "ToDouble",
                "ToHexString", "ToHexStringLower", "ToInt16", "ToInt32", "ToInt64", "ToSByte", "ToSingle",
                "ToString", "ToUInt16", "ToUInt32", "ToUInt64",
            ],
            ["DBNull"] = ["Value"],
            ["TypeCode"] =
            [
                "Boolean", "Byte", "Char", "DateTime", "DBNull", "Decimal", "Double", "Empty", "Int16", "Int32",
                "Int64", "Object", "SByte", "Single", "String", "UInt16", "UInt32", "UInt64",
            ],
            ["MemberTypes"] =
            ["All", "Constructor", "Custom", "Event", "Field", "Method", "NestedType", "Property", "TypeInfo"],
            ["RegexOptions"] =
            [
                "Compiled", "CultureInvariant", "ECMAScript", "ExplicitCapture", "IgnoreCase",
                "IgnorePatternWhitespace", "Multiline", "NonBacktracking", "None", "RightToLeft", "Singleline",
            ],
            ["int"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["uint"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["long"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["ulong"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["nint"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse", "Size", "Zero"],
            ["nuint"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse", "Size", "Zero"],
            ["IntPtr"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse", "Size", "Zero"],
            ["UIntPtr"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse", "Size", "Zero"],
            ["short"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["ushort"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["byte"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["sbyte"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "Parse"],
            ["Int128"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "NegativeOne", "One", "Parse", "Zero"],
            ["UInt128"] = ["Clamp", "Max", "MaxValue", "Min", "MinValue", "One", "Parse", "Zero"],
            ["BigInteger"] =
            [
                "Abs", "Add", "Clamp", "Compare", "Divide", "GreatestCommonDivisor", "Log", "Log10", "Log2",
                "Max", "Min", "MinusOne", "ModPow", "Multiply", "Negate", "One", "Parse", "Pow", "Remainder",
                "Subtract", "Zero",
            ],
            ["decimal"] =
            [
                "Add", "Ceiling", "Clamp", "Compare", "Divide", "Floor", "Max", "MaxValue", "Min", "MinusOne",
                "MinValue", "Multiply", "Negate", "One", "Parse", "Remainder", "Round", "Subtract", "Truncate",
                "Zero",
            ],
            ["double"] =
            [
                "Clamp", "E", "Epsilon", "IsEvenInteger", "IsFinite", "IsInfinity", "IsInteger", "IsNaN",
                "IsNegative", "IsNegativeInfinity", "IsNormal", "IsOddInteger", "IsPositive",
                "IsPositiveInfinity", "IsRealNumber", "IsSubnormal", "Max", "MaxValue", "Min", "MinValue",
                "NaN", "NegativeInfinity", "NegativeZero", "Parse", "Pi", "PositiveInfinity", "Tau",
            ],
            ["float"] =
            [
                "Clamp", "E", "Epsilon", "IsEvenInteger", "IsFinite", "IsInfinity", "IsInteger", "IsNaN",
                "IsNegative", "IsNegativeInfinity", "IsNormal", "IsOddInteger", "IsPositive",
                "IsPositiveInfinity", "IsRealNumber", "IsSubnormal", "Max", "MaxValue", "Min", "MinValue",
                "NaN", "NegativeInfinity", "NegativeZero", "Parse", "Pi", "PositiveInfinity", "Tau",
            ],
            ["Half"] =
            [
                "Clamp", "E", "Epsilon", "IsEvenInteger", "IsFinite", "IsInfinity", "IsInteger", "IsNaN",
                "IsNegative", "IsNegativeInfinity", "IsNormal", "IsOddInteger", "IsPositive",
                "IsPositiveInfinity", "IsRealNumber", "IsSubnormal", "Max", "MaxValue", "Min", "MinValue",
                "NaN", "NegativeInfinity", "NegativeOne", "NegativeZero", "One", "Parse", "Pi",
                "PositiveInfinity", "Tau", "Zero",
            ],
            ["NFloat"] =
            [
                "Clamp", "E", "Epsilon", "IsEvenInteger", "IsFinite", "IsInfinity", "IsInteger", "IsNaN",
                "IsNegative", "IsNegativeInfinity", "IsNormal", "IsOddInteger", "IsPositive",
                "IsPositiveInfinity", "IsRealNumber", "IsSubnormal", "Max", "MaxValue", "Min", "MinValue",
                "NaN", "NegativeInfinity", "NegativeZero", "Parse", "Pi", "PositiveInfinity", "Size", "Tau",
            ],
            ["string"] = ["Empty"],
            ["DayOfWeek"] =
            ["Friday", "Monday", "Saturday", "Sunday", "Thursday", "Tuesday", "Wednesday"],
            ["DateTimeKind"] = ["Local", "Unspecified", "Utc"],
            ["StringComparison"] =
            [
                "CurrentCulture", "CurrentCultureIgnoreCase", "InvariantCulture", "InvariantCultureIgnoreCase",
                "Ordinal", "OrdinalIgnoreCase",
            ],
            ["StringSplitOptions"] = ["None", "RemoveEmptyEntries", "TrimEntries"],
        }.ToImmutableDictionary(StringComparer.Ordinal);

    // ---- Instance members over modeled values ------------------------------------------------------------------
    // These tables mirror the expression evaluator's instance dispatch exactly, including members that produce a
    // deliberate explained stop (ToUpper redirects to the invariant form, ToLocalTime is nondeterministic): the
    // stop is part of the documented surface, so the name still completes. Each member also records the type of
    // the value it folds to, when the evaluator models one, which is what lets chains keep completing:
    // 's.Trim().Length' knows Trim() folds a String and Length an Int32. A null result means the chain has no
    // modeled continuation there.

    private readonly record struct InstanceMemberInfo(bool IsMethod, string? ResultType);

    private sealed class InstanceSurface
    {
        public InstanceSurface(
            (string Name, string? Result)[] properties,
            (string Name, string? Result)[] methods)
        {
            var items = ImmutableArray.CreateBuilder<CompletionItem>(properties.Length + methods.Length);
            var members = ImmutableDictionary.CreateBuilder<string, InstanceMemberInfo>(StringComparer.Ordinal);
            foreach (var (name, result) in properties)
            {
                items.Add(new CompletionItem(name, CompletionItemKind.Member, "property"));
                members[name] = new InstanceMemberInfo(IsMethod: false, result);
            }

            foreach (var (name, result) in methods)
            {
                items.Add(new CompletionItem(name, CompletionItemKind.Member, "method"));
                members[name] = new InstanceMemberInfo(IsMethod: true, result);
            }

            items.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Text, right.Text));
            Items = items.MoveToImmutable();
            Members = members.ToImmutable();
        }

        public ImmutableArray<CompletionItem> Items { get; }

        public ImmutableDictionary<string, InstanceMemberInfo> Members { get; }
    }

    // Sequence member results are parametric in the element type; these tokens stand for it in the template.
    private const string ElementResult = "@element";
    private const string SequenceResult = "@sequence";
    private const string SelfResult = "@self";
    private const string ImmutableResultPrefix = "@immutable:";
    private const string KeysResult = "@keys";
    private const string ValuesResult = "@values";

    private static readonly InstanceSurface SequenceSurfaceTemplate = new(
        [("Length", "Int32"), ("LongLength", "Int64"), ("Rank", "Int32")],
        [
            ("All", "Boolean"), ("Any", "Boolean"), ("Append", SequenceResult), ("Average", "Double"),
            ("Concat", SequenceResult), ("Contains", "Boolean"), ("Count", "Int32"),
            ("Distinct", SequenceResult), ("ElementAt", ElementResult), ("ElementAtOrDefault", ElementResult),
            ("Except", SequenceResult), ("FindIndex", "Int32"), ("FindLastIndex", "Int32"),
            ("First", ElementResult), ("FirstOrDefault", ElementResult), ("GetLength", "Int32"),
            ("GetLowerBound", "Int32"), ("GetType", "Type"), ("GetUpperBound", "Int32"),
            ("GetValue", ElementResult), ("GroupBy", null), ("GroupJoin", null), ("Intersect", SequenceResult),
            ("Join", null), ("Last", ElementResult), ("LastOrDefault", ElementResult), ("LongCount", "Int64"),
            ("Max", ElementResult), ("Min", ElementResult), ("Order", SequenceResult),
            ("OrderBy", SequenceResult), ("OrderByDescending", SequenceResult),
            ("OrderDescending", SequenceResult), ("Prepend", SequenceResult), ("Reverse", SequenceResult),
            ("Select", null), ("SelectMany", null), ("SequenceEqual", "Boolean"), ("Single", ElementResult),
            ("SingleOrDefault", ElementResult), ("Skip", SequenceResult), ("SkipLast", SequenceResult),
            ("SkipWhile", SequenceResult), ("Sum", "Double"), ("Take", SequenceResult),
            ("TakeLast", SequenceResult), ("TakeWhile", SequenceResult), ("ThenBy", SequenceResult),
            ("ThenByDescending", SequenceResult), ("ToArray", SequenceResult),
            ("ToImmutableArray", ImmutableResultPrefix + "ImmutableArray"),
            ("ToImmutableDictionary", null), ("ToImmutableHashSet", ImmutableResultPrefix + "ImmutableHashSet"),
            ("ToImmutableList", ImmutableResultPrefix + "ImmutableList"),
            ("ToImmutableSortedDictionary", null),
            ("ToImmutableSortedSet", ImmutableResultPrefix + "ImmutableSortedSet"),
            ("ToList", SequenceResult),
            ("Union", SequenceResult), ("Where", SequenceResult),
        ]);

    private static readonly ImmutableArray<CompletionItem> SequenceInstanceMembers = SequenceSurfaceTemplate.Items;

    // The per-kind immutable-collection surfaces: the persistent operations each BCL type declares, with
    // '@self' standing for the receiver's own collection type. The shared sequence surface answers the rest.
    private static readonly ImmutableDictionary<string, InstanceSurface> ImmutableCollectionSurfaces =
        new Dictionary<string, InstanceSurface>(StringComparer.Ordinal)
        {
            ["ImmutableArray"] = new(
                [
                    ("IsDefault", "Boolean"), ("IsDefaultOrEmpty", "Boolean"), ("IsEmpty", "Boolean"),
                    ("Length", "Int32"),
                ],
                [
                    ("Add", SelfResult), ("AddRange", SelfResult), ("Clear", SelfResult), ("IndexOf", "Int32"),
                    ("Insert", SelfResult), ("InsertRange", SelfResult), ("LastIndexOf", "Int32"),
                    ("Remove", SelfResult), ("RemoveAt", SelfResult), ("RemoveRange", SelfResult),
                    ("SetItem", SelfResult),
                ]),
            ["ImmutableList"] = new(
                [("Count", "Int32"), ("IsEmpty", "Boolean")],
                [
                    ("Add", SelfResult), ("AddRange", SelfResult), ("Clear", SelfResult), ("IndexOf", "Int32"),
                    ("Insert", SelfResult), ("InsertRange", SelfResult), ("LastIndexOf", "Int32"),
                    ("Remove", SelfResult), ("RemoveAt", SelfResult), ("RemoveRange", SelfResult),
                    ("Reverse", SelfResult), ("SetItem", SelfResult), ("Sort", SelfResult),
                ]),
            ["ImmutableHashSet"] = new(
                [("Count", "Int32"), ("IsEmpty", "Boolean")],
                [
                    ("Add", SelfResult), ("Clear", SelfResult), ("Except", SelfResult),
                    ("Intersect", SelfResult), ("IsProperSubsetOf", "Boolean"),
                    ("IsProperSupersetOf", "Boolean"), ("IsSubsetOf", "Boolean"), ("IsSupersetOf", "Boolean"),
                    ("Overlaps", "Boolean"), ("Remove", SelfResult), ("SetEquals", "Boolean"),
                    ("SymmetricExcept", SelfResult), ("Union", SelfResult),
                ]),
            ["ImmutableSortedSet"] = new(
                [("Count", "Int32"), ("IsEmpty", "Boolean"), ("Max", ElementResult), ("Min", ElementResult)],
                [
                    ("Add", SelfResult), ("Clear", SelfResult), ("Except", SelfResult), ("IndexOf", "Int32"),
                    ("Intersect", SelfResult), ("IsProperSubsetOf", "Boolean"),
                    ("IsProperSupersetOf", "Boolean"), ("IsSubsetOf", "Boolean"), ("IsSupersetOf", "Boolean"),
                    ("Overlaps", "Boolean"), ("Remove", SelfResult), ("SetEquals", "Boolean"),
                    ("SymmetricExcept", SelfResult), ("Union", SelfResult),
                ]),
            ["ImmutableQueue"] = new(
                [("IsEmpty", "Boolean")],
                [
                    ("Clear", SelfResult), ("Dequeue", SelfResult), ("Enqueue", SelfResult),
                    ("Peek", ElementResult),
                ]),
            ["ImmutableStack"] = new(
                [("IsEmpty", "Boolean")],
                [("Clear", SelfResult), ("Peek", ElementResult), ("Pop", SelfResult), ("Push", SelfResult)]),
            ["ImmutableDictionary"] = new(
                [("Count", "Int32"), ("IsEmpty", "Boolean"), (
                    "Keys", KeysResult), ("Values", ValuesResult)],
                [
                    ("Add", SelfResult), ("Clear", SelfResult), ("ContainsKey", "Boolean"),
                    ("ContainsValue", "Boolean"), ("Remove", SelfResult), ("SetItem", SelfResult),
                ]),
            ["ImmutableSortedDictionary"] = new(
                [("Count", "Int32"), ("IsEmpty", "Boolean"), (
                    "Keys", KeysResult), ("Values", ValuesResult)],
                [
                    ("Add", SelfResult), ("Clear", SelfResult), ("ContainsKey", "Boolean"),
                    ("ContainsValue", "Boolean"), ("Remove", SelfResult), ("SetItem", SelfResult),
                ]),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    // The sequence members only a real array answers; an immutable collection does not complete them.
    private static readonly ImmutableHashSet<string> ArrayOnlySequenceMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Length", "LongLength", "Rank", "GetLength", "GetLowerBound", "GetUpperBound", "GetValue");

    /// <summary>Splits an immutable-collection spelling — 'ImmutableList&lt;Int32&gt;' — into kind and element.</summary>
    private static bool TrySplitImmutableTypeName(string typeName, out string kindName, out string elementType)
    {
        kindName = string.Empty;
        elementType = string.Empty;
        var open = typeName.IndexOf('<', StringComparison.Ordinal);
        if (open <= 0 || !typeName.EndsWith('>'))
        {
            return false;
        }

        var name = typeName[..open];
        if (!ImmutableCollectionSurfaces.ContainsKey(name))
        {
            return false;
        }

        kindName = name;
        elementType = typeName[(open + 1)..^1];
        return true;
    }

    /// <summary>Splits a <c>KeyValuePair&lt;K, V&gt;</c> spelling into its argument text; false otherwise.</summary>
    private static bool TrySplitKeyValuePairTypeName(string typeName, out string pairArguments)
    {
        pairArguments = string.Empty;
        if (!typeName.StartsWith("KeyValuePair<", StringComparison.Ordinal) || !typeName.EndsWith('>'))
        {
            return false;
        }

        pairArguments = typeName["KeyValuePair<".Length..^1];
        return true;
    }

    /// <summary>Splits a pair's argument text — <c>Int32, String</c> — at its top-level comma.</summary>
    private static (string Key, string Value) SplitPairArguments(string pairArguments)
    {
        var depth = 0;
        for (var position = 0; position < pairArguments.Length; position++)
        {
            switch (pairArguments[position])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return (pairArguments[..position].Trim(), pairArguments[(position + 1)..].Trim());
            }
        }

        return (pairArguments.Trim(), pairArguments.Trim());
    }

    /// <summary>The completion items of one immutable collection: its own surface plus the shared sequence one.</summary>
    private static ImmutableArray<CompletionItem> ImmutableInstanceItems(string kindName)
    {
        var surface = ImmutableCollectionSurfaces[kindName];
        return
        [
            .. surface.Items,
            .. SequenceSurfaceTemplate.Items.Where(item =>
                !surface.Members.ContainsKey(item.Text) && !ArrayOnlySequenceMembers.Contains(item.Text)),
        ];
    }

    private static readonly InstanceSurface DelegateSurface = new(
        [("HasSingleTarget", "Boolean"), ("Method", "MethodInfo"), ("Target", null)],
        [
            ("DynamicInvoke", null), ("Equals", "Boolean"), ("GetInvocationList", "Delegate[]"),
            ("GetType", "Type"), ("Invoke", null), ("ToString", "String"),
        ]);

    private static readonly ImmutableArray<CompletionItem> DelegateInstanceMembers = DelegateSurface.Items;

    // A numeric scalar answers the universal members plus the instance trio every numeric kind shares:
    // CompareTo and Equals with the kind's own parameter, and the value-based hash.
    private static readonly InstanceSurface ScalarSurface = new(
        [],
        [
            ("CompareTo", "Int32"), ("Equals", "Boolean"), ("GetHashCode", "Int32"), ("GetType", "Type"),
            ("ToString", "String"),
        ]);

    // A Boolean or a stored enum — which reads back as its underlying value — only answers the universal members.
    private static readonly InstanceSurface BooleanSurface = new(
        [],
        [("GetType", "Type"), ("ToString", "String")]);

    private static readonly ImmutableArray<CompletionItem> ScalarInstanceMembers = ScalarSurface.Items;

    // A live enum value — an enum-typed property or a spelled enum member, unlike a stored variable — keeps its
    // identity and dispatches the enum instance surface.
    private static readonly InstanceSurface EnumValueSurface = new(
        [],
        [("CompareTo", "Int32"), ("GetType", "Type"), ("HasFlag", "Boolean"), ("ToString", "String")]);

    private static readonly ImmutableDictionary<string, InstanceSurface> InstanceReceiverSurfaces =
        BuildInstanceReceiverSurfaces();

    private static ImmutableDictionary<string, InstanceSurface> BuildInstanceReceiverSurfaces()
    {
        var tables = new Dictionary<string, InstanceSurface>(StringComparer.Ordinal)
        {
            ["String"] = new(
                [("Length", "Int32")],
                [
                    ("CompareTo", null), ("Contains", "Boolean"), ("EndsWith", "Boolean"),
                    ("EnumerateRunes", "Rune[]"), ("Equals", "Boolean"), ("GetType", "Type"),
                    ("IndexOf", "Int32"), ("Insert", "String"), ("LastIndexOf", "Int32"),
                    ("PadLeft", "String"), ("PadRight", "String"), ("Remove", "String"), ("Replace", "String"),
                    ("Split", "String[]"), ("StartsWith", "Boolean"), ("Substring", "String"),
                    ("ToCharArray", "Char[]"), ("ToLower", null), ("ToLowerInvariant", "String"),
                    ("ToString", "String"), ("ToUpper", null), ("ToUpperInvariant", "String"),
                    ("Trim", "String"), ("TrimEnd", "String"), ("TrimStart", "String"),
                ]),
            ["Char"] = new(
                [],
                [
                    ("CompareTo", "Int32"), ("Equals", "Boolean"), ("GetHashCode", "Int32"),
                    ("GetType", "Type"), ("ToString", "String"),
                ]),
            ["DateTime"] = new(
                [
                    ("Date", "DateTime"), ("Day", "Int32"), ("DayOfWeek", "Enum"), ("DayOfYear", "Int32"),
                    ("Hour", "Int32"), ("Kind", "Enum"), ("Millisecond", "Int32"), ("Minute", "Int32"),
                    ("Month", "Int32"), ("Second", "Int32"), ("Ticks", "Int64"), ("TimeOfDay", "TimeSpan"),
                    ("Year", "Int32"),
                ],
                [
                    ("Add", "DateTime"), ("AddDays", "DateTime"), ("AddHours", "DateTime"),
                    ("AddMilliseconds", "DateTime"), ("AddMinutes", "DateTime"), ("AddMonths", "DateTime"),
                    ("AddSeconds", "DateTime"), ("AddTicks", "DateTime"), ("AddYears", "DateTime"),
                    ("GetType", "Type"), ("Subtract", null), ("ToLocalTime", null), ("ToString", "String"),
                    ("ToUniversalTime", null),
                ]),
            ["DateTimeOffset"] = new(
                [
                    ("Date", "DateTime"), ("DateTime", "DateTime"), ("Day", "Int32"), ("DayOfWeek", "Enum"),
                    ("DayOfYear", "Int32"), ("Hour", "Int32"), ("LocalDateTime", null),
                    ("Millisecond", "Int32"), ("Minute", "Int32"), ("Month", "Int32"), ("Offset", "TimeSpan"),
                    ("Second", "Int32"), ("Ticks", "Int64"), ("TimeOfDay", "TimeSpan"),
                    ("UtcDateTime", "DateTime"), ("UtcTicks", "Int64"), ("Year", "Int32"),
                ],
                [
                    ("Add", "DateTimeOffset"), ("AddDays", "DateTimeOffset"), ("AddHours", "DateTimeOffset"),
                    ("AddMilliseconds", "DateTimeOffset"), ("AddMinutes", "DateTimeOffset"),
                    ("AddMonths", "DateTimeOffset"), ("AddSeconds", "DateTimeOffset"),
                    ("AddTicks", "DateTimeOffset"), ("AddYears", "DateTimeOffset"), ("GetType", "Type"),
                    ("Subtract", null), ("ToLocalTime", null), ("ToOffset", "DateTimeOffset"),
                    ("ToString", "String"), ("ToUniversalTime", "DateTimeOffset"),
                    ("ToUnixTimeMilliseconds", "Int64"), ("ToUnixTimeSeconds", "Int64"),
                ]),
            ["TimeSpan"] = new(
                [
                    ("Days", "Int32"), ("Hours", "Int32"), ("Milliseconds", "Int32"), ("Minutes", "Int32"),
                    ("Seconds", "Int32"), ("Ticks", "Int64"), ("TotalDays", "Double"),
                    ("TotalHours", "Double"), ("TotalMilliseconds", "Double"), ("TotalMinutes", "Double"),
                    ("TotalSeconds", "Double"),
                ],
                [
                    ("Add", "TimeSpan"), ("Divide", null), ("Duration", "TimeSpan"), ("GetType", "Type"),
                    ("Multiply", "TimeSpan"), ("Negate", "TimeSpan"), ("Subtract", "TimeSpan"),
                    ("ToString", "String"),
                ]),
            ["DateOnly"] = new(
                [
                    ("Day", "Int32"), ("DayNumber", "Int32"), ("DayOfWeek", "Enum"), ("DayOfYear", "Int32"),
                    ("Month", "Int32"), ("Year", "Int32"),
                ],
                [
                    ("AddDays", "DateOnly"), ("AddMonths", "DateOnly"), ("AddYears", "DateOnly"),
                    ("GetType", "Type"), ("ToDateTime", "DateTime"), ("ToString", "String"),
                ]),
            ["TimeOnly"] = new(
                [
                    ("Hour", "Int32"), ("Millisecond", "Int32"), ("Minute", "Int32"), ("Second", "Int32"),
                    ("Ticks", "Int64"),
                ],
                [
                    ("Add", "TimeOnly"), ("AddHours", "TimeOnly"), ("AddMinutes", "TimeOnly"),
                    ("GetType", "Type"), ("ToString", "String"), ("ToTimeSpan", "TimeSpan"),
                ]),
            ["Guid"] = new(
                [("Variant", "Int32"), ("Version", "Int32")],
                [("CompareTo", "Int32"), ("GetType", "Type"), ("ToString", "String")]),
            ["Version"] = new(
                [
                    ("Build", "Int32"), ("Major", "Int32"), ("MajorRevision", "Int16"), ("Minor", "Int32"),
                    ("MinorRevision", "Int16"), ("Revision", "Int32"),
                ],
                [("CompareTo", "Int32"), ("GetType", "Type"), ("ToString", "String")]),
            ["DBNull"] = new(
                [],
                [
                    ("Equals", "Boolean"), ("GetType", "Type"), ("GetTypeCode", "Enum"),
                    ("ToString", "String"),
                ]),
            ["Rune"] = new(
                [
                    ("IsAscii", "Boolean"), ("IsBmp", "Boolean"), ("Plane", "Int32"),
                    ("Utf16SequenceLength", "Int32"), ("Utf8SequenceLength", "Int32"), ("Value", "Int32"),
                ],
                [
                    ("CompareTo", "Int32"), ("Equals", "Boolean"), ("GetHashCode", "Int32"),
                    ("GetType", "Type"), ("ToString", "String"),
                ]),
            ["Encoding"] = new(
                [
                    ("BodyName", "String"), ("CodePage", "Int32"), ("EncodingName", "String"),
                    ("HeaderName", "String"), ("IsSingleByte", "Boolean"), ("Preamble", "Byte[]"),
                    ("WebName", "String"),
                ],
                [
                    ("Equals", "Boolean"), ("GetByteCount", "Int32"), ("GetBytes", "Byte[]"),
                    ("GetCharCount", "Int32"), ("GetChars", "Char[]"), ("GetMaxByteCount", "Int32"),
                    ("GetMaxCharCount", "Int32"), ("GetPreamble", "Byte[]"), ("GetString", "String"),
                    ("GetType", "Type"),
                ]),
            ["Regex"] = new(
                [("Options", "Enum"), ("RightToLeft", "Boolean")],
                [
                    ("Count", "Int32"), ("GetGroupNames", "String[]"), ("GetGroupNumbers", "Int32[]"),
                    ("GetType", "Type"), ("GroupNameFromNumber", "String"), ("GroupNumberFromName", "Int32"),
                    ("IsMatch", "Boolean"), ("Match", "Match"), ("Matches", "MatchCollection"),
                    ("Replace", "String"), ("Split", "String[]"), ("ToString", "String"),
                ]),
            ["Match"] = new(
                [
                    ("Groups", "GroupCollection"), ("Index", "Int32"), ("Length", "Int32"),
                    ("Name", "String"), ("Success", "Boolean"), ("Value", "String"),
                ],
                [
                    ("GetType", "Type"), ("NextMatch", "Match"), ("Result", "String"),
                    ("ToString", "String"),
                ]),
            ["Group"] = new(
                [
                    ("Captures", "CaptureCollection"), ("Index", "Int32"), ("Length", "Int32"),
                    ("Name", "String"), ("Success", "Boolean"), ("Value", "String"),
                ],
                [("GetType", "Type"), ("ToString", "String")]),
            ["Capture"] = new(
                [("Index", "Int32"), ("Length", "Int32"), ("Value", "String")],
                [("GetType", "Type"), ("ToString", "String")]),
            ["MethodInfo"] = new(
                [
                    ("DeclaringType", "Type"), ("IsGenericMethod", "Boolean"), ("IsPublic", "Boolean"),
                    ("IsStatic", "Boolean"), ("MemberType", "Enum"), ("Name", "String"),
                    ("ReturnType", "Type"),
                ],
                [
                    ("CreateDelegate", "Delegate"), ("GetParameters", "ParameterInfo[]"), ("GetType", "Type"),
                    ("Invoke", null), ("ToString", "String"),
                ]),
            ["ConstructorInfo"] = new(
                [
                    ("DeclaringType", "Type"), ("IsPublic", "Boolean"), ("IsStatic", "Boolean"),
                    ("MemberType", "Enum"), ("Name", "String"),
                ],
                [
                    ("GetParameters", "ParameterInfo[]"), ("GetType", "Type"), ("Invoke", null),
                    ("ToString", "String"),
                ]),
            ["PropertyInfo"] = new(
                [
                    ("CanRead", "Boolean"), ("CanWrite", "Boolean"), ("DeclaringType", "Type"),
                    ("MemberType", "Enum"), ("Name", "String"), ("PropertyType", "Type"),
                ],
                [
                    ("GetGetMethod", "MethodInfo"), ("GetIndexParameters", "ParameterInfo[]"),
                    ("GetSetMethod", "MethodInfo"), ("GetType", "Type"), ("GetValue", null),
                    ("SetValue", null), ("ToString", "String"),
                ]),
            ["FieldInfo"] = new(
                [
                    ("DeclaringType", "Type"), ("FieldType", "Type"), ("IsInitOnly", "Boolean"),
                    ("IsLiteral", "Boolean"), ("IsPublic", "Boolean"), ("IsStatic", "Boolean"),
                    ("MemberType", "Enum"), ("Name", "String"),
                ],
                [("GetType", "Type"), ("GetValue", null), ("SetValue", null), ("ToString", "String")]),
            ["ParameterInfo"] = new(
                [
                    ("HasDefaultValue", "Boolean"), ("IsOptional", "Boolean"), ("Name", "String"),
                    ("ParameterType", "Type"), ("Position", "Int32"),
                ],
                [("GetType", "Type"), ("ToString", "String")]),
            ["Type"] = new(
                [
                    ("AssemblyQualifiedName", null), ("BaseType", "Type"),
                    ("ContainsGenericParameters", "Boolean"), ("FullName", "String"),
                    ("GenericTypeArguments", "Type[]"), ("HasElementType", "Boolean"), ("IsArray", "Boolean"),
                    ("IsClass", "Boolean"), ("IsConstructedGenericType", "Boolean"), ("IsEnum", "Boolean"),
                    ("IsGenericType", "Boolean"), ("IsGenericTypeDefinition", "Boolean"),
                    ("IsInterface", "Boolean"), ("IsPrimitive", "Boolean"), ("IsValueType", "Boolean"),
                    ("Name", "String"), ("Namespace", "String"), ("UnderlyingSystemType", "Type"),
                ],
                [
                    ("Equals", "Boolean"), ("GetConstructors", "ConstructorInfo[]"),
                    ("GetElementType", "Type"), ("GetEnumName", "String"), ("GetEnumNames", "String[]"),
                    ("GetEnumUnderlyingType", "Type"), ("GetEnumValues", null), ("GetField", "FieldInfo"),
                    ("GetFields", "FieldInfo[]"), ("GetGenericArguments", "Type[]"),
                    ("GetGenericTypeDefinition", "Type"), ("GetMember", null), ("GetMembers", null),
                    ("GetMethod", "MethodInfo"), ("GetMethods", "MethodInfo[]"),
                    ("GetProperties", "PropertyInfo[]"), ("GetProperty", "PropertyInfo"), ("GetType", "Type"),
                    ("IsAssignableFrom", "Boolean"), ("IsAssignableTo", "Boolean"),
                    ("IsEnumDefined", "Boolean"), ("IsInstanceOfType", "Boolean"),
                    ("IsSubclassOf", "Boolean"), ("MakeArrayType", "Type"), ("MakeGenericType", "Type"),
                    ("ToString", "String"),
                ]),
            ["Delegate"] = DelegateSurface,
            ["Enum"] = EnumValueSurface,
        };

        // The materialized regex collections carry Count as a property and otherwise answer the whole sequence
        // surface, with their element types fixed.
        tables["MatchCollection"] = CollectionSurface("Match");
        tables["GroupCollection"] = CollectionSurface("Group");
        tables["CaptureCollection"] = CollectionSurface("Capture");

        // Every boxed numeric answers the shared scalar surface; Booleans keep the universal pair only.
        foreach (var scalar in (string[])
        [
            "SByte", "Byte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "IntPtr",
            "UIntPtr", "Int128", "UInt128", "Half", "NFloat", "Single", "Double",
        ])
        {
            tables[scalar] = ScalarSurface;
        }

        tables["Boolean"] = BooleanSurface;

        // BigInteger and decimal add the few instance properties the evaluator models beyond the shared trio.
        tables["BigInteger"] = new InstanceSurface(
            [
                ("IsEven", "Boolean"), ("IsOne", "Boolean"), ("IsPowerOfTwo", "Boolean"), ("IsZero", "Boolean"),
                ("Sign", "Int32"),
            ],
            [
                ("CompareTo", "Int32"), ("Equals", "Boolean"), ("GetHashCode", "Int32"), ("GetType", "Type"),
                ("ToString", "String"),
            ]);
        tables["Decimal"] = new InstanceSurface(
            [("Scale", "Byte")],
            [
                ("CompareTo", "Int32"), ("Equals", "Boolean"), ("GetHashCode", "Int32"), ("GetType", "Type"),
                ("ToString", "String"),
            ]);

        return tables.ToImmutableDictionary(StringComparer.Ordinal);
    }

    private static InstanceSurface CollectionSurface(string elementType) => new(
        [("Count", "Int32")],
        [
            .. SequenceSurfaceTemplate.Members
                .Where(static pair => pair.Value.IsMethod)
                .Select(pair => (pair.Key, ResolveSequenceToken(pair.Value.ResultType, elementType))),
        ]);

    private static string? ResolveSequenceToken(string? resultType, string elementType) => resultType switch
    {
        ElementResult => elementType,
        SequenceResult => elementType + "[]",
        { } immutable when immutable.StartsWith(ImmutableResultPrefix, StringComparison.Ordinal) =>
            $"{immutable[ImmutableResultPrefix.Length..]}<{elementType}>",
        _ => resultType,
    };

    /// <summary>
    /// Maps a stored immediate-variable type name to the instance members the expression evaluator dispatches for
    /// that value: the string surface for 'String', the sequence surface for any array spelling, the delegate
    /// surface for a delegate's C# spelling, and the universal members for scalars — including stored enums,
    /// which read back as their underlying numeric value. An unmodeled name completes nothing.
    /// </summary>
    /// <param name="typeName">The stored value's type name, as the variable store spells it.</param>
    /// <returns>The instance members, possibly empty.</returns>
    public static ImmutableArray<CompletionItem> InstanceMembersForStoredType(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName) || typeName == "null")
        {
            return [];
        }

        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            return SequenceInstanceMembers;
        }

        if (TrySplitImmutableTypeName(typeName, out var immutableKind, out _))
        {
            return ImmutableInstanceItems(immutableKind);
        }

        if (TrySplitKeyValuePairTypeName(typeName, out _))
        {
            return
            [
                new CompletionItem("Key", CompletionItemKind.Member, "property"),
                new CompletionItem("Value", CompletionItemKind.Member, "property"),
                new CompletionItem("GetType", CompletionItemKind.Member, "method"),
            ];
        }

        if (IsDelegateTypeName(typeName))
        {
            return DelegateInstanceMembers;
        }

        if (InstanceReceiverSurfaces.TryGetValue(typeName, out var surface))
        {
            return surface.Items;
        }

        // A dotted name is a stored enum's full type name; the value reads back as its underlying numeric.
        return typeName.Contains('.', StringComparison.Ordinal) ? ScalarInstanceMembers : [];
    }

    private static bool IsDelegateTypeName(string typeName) =>
        typeName is "Action" or "Delegate"
        || typeName.StartsWith("Func<", StringComparison.Ordinal)
        || typeName.StartsWith("Action<", StringComparison.Ordinal)
        || typeName.StartsWith("Predicate<", StringComparison.Ordinal)
        || typeName.StartsWith("Comparison<", StringComparison.Ordinal);

    // The modeled receivers whose members are enum values, so 'DayOfWeek.Monday.' completes the enum surface.
    private static readonly ImmutableHashSet<string> EnumReceivers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "DayOfWeek", "DateTimeKind", "StringComparison", "StringSplitOptions", "TypeCode", "MemberTypes",
        "RegexOptions");

    // Static members whose folded value's type the evaluator models, so 'Guid.Empty.' or 'Encoding.UTF8.'
    // completes that value's instance surface. Members that produce explained stops (Now, NewGuid) fold no value,
    // so they map nothing here.
    private static readonly ImmutableDictionary<string, string> StaticMemberResultTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Guid.Empty"] = "Guid",
            ["string.Empty"] = "String",
            ["DBNull.Value"] = "DBNull",
            ["Convert.DBNull"] = "DBNull",
            ["Rune.ReplacementChar"] = "Rune",
            ["DateTime.MaxValue"] = "DateTime",
            ["DateTime.MinValue"] = "DateTime",
            ["DateTime.UnixEpoch"] = "DateTime",
            ["DateTimeOffset.MaxValue"] = "DateTimeOffset",
            ["DateTimeOffset.MinValue"] = "DateTimeOffset",
            ["DateTimeOffset.UnixEpoch"] = "DateTimeOffset",
            ["TimeSpan.MaxValue"] = "TimeSpan",
            ["TimeSpan.MinValue"] = "TimeSpan",
            ["TimeSpan.Zero"] = "TimeSpan",
            ["TimeSpan.TicksPerDay"] = "Int64",
            ["TimeSpan.TicksPerHour"] = "Int64",
            ["TimeSpan.TicksPerMicrosecond"] = "Int64",
            ["TimeSpan.TicksPerMillisecond"] = "Int64",
            ["TimeSpan.TicksPerMinute"] = "Int64",
            ["TimeSpan.TicksPerSecond"] = "Int64",
            ["DateOnly.MaxValue"] = "DateOnly",
            ["DateOnly.MinValue"] = "DateOnly",
            ["TimeOnly.MaxValue"] = "TimeOnly",
            ["TimeOnly.MinValue"] = "TimeOnly",
            ["Encoding.ASCII"] = "Encoding",
            ["Encoding.BigEndianUnicode"] = "Encoding",
            ["Encoding.Default"] = "Encoding",
            ["Encoding.Latin1"] = "Encoding",
            ["Encoding.Unicode"] = "Encoding",
            ["Encoding.UTF32"] = "Encoding",
            ["Encoding.UTF8"] = "Encoding",
            ["Math.E"] = "Double",
            ["Math.PI"] = "Double",
            ["Math.Tau"] = "Double",
            ["MathF.E"] = "Single",
            ["MathF.PI"] = "Single",
            ["MathF.Tau"] = "Single",
            ["int.MaxValue"] = "Int32",
            ["int.MinValue"] = "Int32",
            ["uint.MaxValue"] = "UInt32",
            ["uint.MinValue"] = "UInt32",
            ["long.MaxValue"] = "Int64",
            ["long.MinValue"] = "Int64",
            ["ulong.MaxValue"] = "UInt64",
            ["ulong.MinValue"] = "UInt64",
            ["nint.MaxValue"] = "IntPtr",
            ["nint.MinValue"] = "IntPtr",
            ["nint.Size"] = "Int32",
            ["nint.Zero"] = "IntPtr",
            ["nuint.MaxValue"] = "UIntPtr",
            ["nuint.MinValue"] = "UIntPtr",
            ["nuint.Size"] = "Int32",
            ["nuint.Zero"] = "UIntPtr",
            ["IntPtr.MaxValue"] = "IntPtr",
            ["IntPtr.MinValue"] = "IntPtr",
            ["IntPtr.Size"] = "Int32",
            ["IntPtr.Zero"] = "IntPtr",
            ["UIntPtr.MaxValue"] = "UIntPtr",
            ["UIntPtr.MinValue"] = "UIntPtr",
            ["UIntPtr.Size"] = "Int32",
            ["UIntPtr.Zero"] = "UIntPtr",
            ["short.MaxValue"] = "Int16",
            ["short.MinValue"] = "Int16",
            ["ushort.MaxValue"] = "UInt16",
            ["ushort.MinValue"] = "UInt16",
            ["byte.MaxValue"] = "Byte",
            ["byte.MinValue"] = "Byte",
            ["sbyte.MaxValue"] = "SByte",
            ["sbyte.MinValue"] = "SByte",
            ["decimal.MaxValue"] = "Decimal",
            ["decimal.MinValue"] = "Decimal",
            ["decimal.MinusOne"] = "Decimal",
            ["decimal.One"] = "Decimal",
            ["decimal.Zero"] = "Decimal",
            ["double.E"] = "Double",
            ["double.Epsilon"] = "Double",
            ["double.MaxValue"] = "Double",
            ["double.MinValue"] = "Double",
            ["double.NaN"] = "Double",
            ["double.NegativeInfinity"] = "Double",
            ["double.NegativeZero"] = "Double",
            ["double.Pi"] = "Double",
            ["double.PositiveInfinity"] = "Double",
            ["double.Tau"] = "Double",
            ["float.E"] = "Single",
            ["float.Epsilon"] = "Single",
            ["float.MaxValue"] = "Single",
            ["float.MinValue"] = "Single",
            ["float.NaN"] = "Single",
            ["float.NegativeInfinity"] = "Single",
            ["float.NegativeZero"] = "Single",
            ["float.Pi"] = "Single",
            ["float.PositiveInfinity"] = "Single",
            ["float.Tau"] = "Single",
            ["Half.E"] = "Half",
            ["Half.Epsilon"] = "Half",
            ["Half.MaxValue"] = "Half",
            ["Half.MinValue"] = "Half",
            ["Half.NaN"] = "Half",
            ["Half.NegativeInfinity"] = "Half",
            ["Half.NegativeOne"] = "Half",
            ["Half.NegativeZero"] = "Half",
            ["Half.One"] = "Half",
            ["Half.Pi"] = "Half",
            ["Half.PositiveInfinity"] = "Half",
            ["Half.Tau"] = "Half",
            ["Half.Zero"] = "Half",
            ["NFloat.E"] = "NFloat",
            ["NFloat.Epsilon"] = "NFloat",
            ["NFloat.MaxValue"] = "NFloat",
            ["NFloat.MinValue"] = "NFloat",
            ["NFloat.NaN"] = "NFloat",
            ["NFloat.NegativeInfinity"] = "NFloat",
            ["NFloat.NegativeZero"] = "NFloat",
            ["NFloat.Pi"] = "NFloat",
            ["NFloat.PositiveInfinity"] = "NFloat",
            ["NFloat.Size"] = "Int32",
            ["NFloat.Tau"] = "NFloat",
            ["Int128.MaxValue"] = "Int128",
            ["Int128.MinValue"] = "Int128",
            ["Int128.NegativeOne"] = "Int128",
            ["Int128.One"] = "Int128",
            ["Int128.Zero"] = "Int128",
            ["UInt128.MaxValue"] = "UInt128",
            ["UInt128.MinValue"] = "UInt128",
            ["UInt128.One"] = "UInt128",
            ["UInt128.Zero"] = "UInt128",
            ["BigInteger.MinusOne"] = "BigInteger",
            ["BigInteger.One"] = "BigInteger",
            ["BigInteger.Zero"] = "BigInteger",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static string? StaticMemberResultType(string receiver, string member)
    {
        if (EnumReceivers.Contains(receiver)
            && ReceiverMembers.TryGetValue(receiver, out var enumMembers)
            && enumMembers.Contains(member, StringComparer.Ordinal))
        {
            return "Enum";
        }

        return StaticMemberResultTypes.TryGetValue($"{receiver}.{member}", out var resultType)
            ? resultType
            : null;
    }

    /// <summary>Folds one member access over a modeled value type, honoring property-versus-method spelling.</summary>
    private static string? ApplyMemberResult(string receiverType, string member, bool invoked)
    {
        if (receiverType.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = receiverType[..^2];
            return SequenceSurfaceTemplate.Members.TryGetValue(member, out var sequenceMember)
                && sequenceMember.IsMethod == invoked
                ? ResolveSequenceToken(sequenceMember.ResultType, elementType)
                : null;
        }

        if (TrySplitImmutableTypeName(receiverType, out var immutableKind, out var immutableElement))
        {
            // A dictionary's shared-surface element is its pair; its own surface adds the key/value splits.
            var isDictionary = immutableKind is "ImmutableDictionary" or "ImmutableSortedDictionary";
            var elementToken = isDictionary ? $"KeyValuePair<{immutableElement}>" : immutableElement;
            if (ImmutableCollectionSurfaces[immutableKind].Members.TryGetValue(member, out var kindMember)
                && kindMember.IsMethod == invoked)
            {
                return kindMember.ResultType switch
                {
                    SelfResult => receiverType,
                    KeysResult => SplitPairArguments(immutableElement).Key + "[]",
                    ValuesResult => SplitPairArguments(immutableElement).Value + "[]",
                    _ => ResolveSequenceToken(kindMember.ResultType, elementToken),
                };
            }

            return SequenceSurfaceTemplate.Members.TryGetValue(member, out var shared)
                && shared.IsMethod == invoked
                && !ArrayOnlySequenceMembers.Contains(member)
                ? ResolveSequenceToken(shared.ResultType, elementToken)
                : null;
        }

        if (TrySplitKeyValuePairTypeName(receiverType, out var pairArguments))
        {
            return (member, invoked) switch
            {
                ("Key", false) => SplitPairArguments(pairArguments).Key,
                ("Value", false) => SplitPairArguments(pairArguments).Value,
                ("GetType", true) => "Type",
                _ => null,
            };
        }

        var key = IsDelegateTypeName(receiverType) ? "Delegate"
            : receiverType.Contains('.', StringComparison.Ordinal) ? "Int32"
            : receiverType;
        return InstanceReceiverSurfaces.TryGetValue(key, out var surface)
            && surface.Members.TryGetValue(member, out var info)
            && info.IsMethod == invoked
            ? info.ResultType
            : null;
    }

    /// <summary>Folds one index access: an array yields its element, a string a char, a collection its item.</summary>
    private static string? ApplyIndexResult(string receiverType) => receiverType switch
    {
        _ when receiverType.EndsWith("[]", StringComparison.Ordinal) => receiverType[..^2],
        _ when TrySplitImmutableTypeName(receiverType, out var immutableKind, out var immutableElement) =>
            immutableKind switch
            {
                "ImmutableArray" or "ImmutableList" or "ImmutableSortedSet" => immutableElement,
                "ImmutableDictionary" or "ImmutableSortedDictionary" =>
                    SplitPairArguments(immutableElement).Value,
                _ => null,
            },
        "String" => "Char",
        "MatchCollection" => "Match",
        "GroupCollection" => "Group",
        "CaptureCollection" => "Capture",
        _ => null,
    };

    /// <summary>
    /// Resolves the modeled value type a receiver chain folds to: a string or char literal, a declared variable,
    /// or a modeled static value at its head, then one member, call, or index application per hop. Null means
    /// some hop has no modeled value — a method group without parentheses, an unmodeled member, or an unknown
    /// head — so completion offers nothing rather than guessing.
    /// </summary>
    private static string? ResolveChainValueType(
        CompletionContext context,
        ImmutableArray<ReceiverSegment> chain)
    {
        string? type;
        int consumed;
        var head = chain[0];
        if (head.LiteralType is { } literalType)
        {
            type = head.IsInvocation ? null : ApplyIndexes(literalType, head.IndexCount);
            consumed = 1;
        }
        else if (context.Locals.FirstOrDefault(local =>
            string.Equals(local.Text, head.Name, StringComparison.Ordinal)) is { } local)
        {
            type = head.IsInvocation ? null : ApplyIndexes(local.Detail, head.IndexCount);
            consumed = 1;
        }
        else if (chain.Length >= 2
            && head is { IsInvocation: false, IndexCount: 0 }
            && chain[1] is { IsInvocation: false } staticMember)
        {
            type = ApplyIndexes(StaticMemberResultType(head.Name, staticMember.Name), staticMember.IndexCount);
            consumed = 2;
        }
        else
        {
            return null;
        }

        for (var hop = consumed; hop < chain.Length && type is not null; hop++)
        {
            var segment = chain[hop];
            type = ApplyIndexes(
                ApplyMemberResult(type, segment.Name, segment.IsInvocation),
                segment.IndexCount);
        }

        return type is null or "" or "null" ? null : type;

        static string? ApplyIndexes(string? type, int count)
        {
            for (var index = 0; index < count && type is not null; index++)
            {
                type = ApplyIndexResult(type);
            }

            return type;
        }
    }

    // ---- Signature help ----------------------------------------------------------------------------------------

    private const int MaximumSignatureOverloads = 12;

    private static readonly ConcurrentDictionary<(string TypeName, string Method, bool IsStatic),
        ImmutableArray<MethodSignature>> SignatureCache = new();

    /// <summary>
    /// Computes the signature help for one caret position: when the caret sits inside the argument list of a
    /// modeled method — a static receiver's, or an instance method on a resolved value chain — the method's
    /// overloads are read from the live BCL surface the evaluator mirrors, with the active parameter counted
    /// from commas. Anywhere else, including grouping parentheses and unmodeled calls, yields null.
    /// </summary>
    /// <param name="context">The editor-specific context, or null for a plain expression editor.</param>
    /// <param name="text">The expression text being edited.</param>
    /// <param name="caretOffset">The caret offset within <paramref name="text"/>.</param>
    /// <returns>The signature help, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static SignatureHelp? GetSignatureHelp(CompletionContext? context, string text, int caretOffset)
    {
        ArgumentNullException.ThrowIfNull(text);
        context ??= CompletionContext.Expression;
        var caret = Math.Clamp(caretOffset, 0, text.Length);

        // The innermost unclosed '(' owns the caret; commas at its own depth count the active parameter.
        var parenDepth = 0;
        var bracketDepth = 0;
        var commas = 0;
        var open = -1;
        for (var scan = caret - 1; scan >= 0; scan--)
        {
            var current = text[scan];
            if (current == ')')
            {
                parenDepth++;
            }
            else if (current == ']')
            {
                bracketDepth++;
            }
            else if (current == '[')
            {
                if (bracketDepth == 0)
                {
                    return null;
                }

                bracketDepth--;
            }
            else if (current == '(')
            {
                if (parenDepth == 0 && bracketDepth == 0)
                {
                    open = scan;
                    break;
                }

                if (parenDepth > 0)
                {
                    parenDepth--;
                }
            }
            else if (current == ',' && parenDepth == 0 && bracketDepth == 0)
            {
                commas++;
            }
        }

        if (open < 0)
        {
            return null;
        }

        var nameStart = open;
        while (nameStart > 0 && IsIdentifierChar(text[nameStart - 1]))
        {
            nameStart--;
        }

        if (nameStart == open || char.IsAsciiDigit(text[nameStart]))
        {
            return null;
        }

        var methodName = text[nameStart..open];
        if (nameStart == 0 || text[nameStart - 1] != '.')
        {
            return null;
        }

        var chain = ReadReceiverChain(text, nameStart - 1);
        if (chain.Length == 0)
        {
            return null;
        }

        // A single plain receiver that is not a local names a modeled static surface: 'Math.Sqrt(…'.
        if (chain is [{ IsInvocation: false, IndexCount: 0, LiteralType: null } receiver]
            && !context.Locals.Any(local => string.Equals(local.Text, receiver.Name, StringComparison.Ordinal))
            && ReceiverMembers.TryGetValue(receiver.Name, out var staticMembers)
            && staticMembers.Contains(methodName, StringComparer.Ordinal))
        {
            var signatures = LookupSignatures(receiver.Name, methodName, isStatic: true);
            return signatures.IsEmpty ? null : new SignatureHelp(receiver.Name, signatures, commas, open);
        }

        // Otherwise the receiver must fold to a modeled value whose surface dispatches the method.
        if (ResolveChainValueType(context, chain) is { } receiverType
            && InstanceMethodExists(receiverType, methodName))
        {
            var signatures = LookupSignatures(receiverType, methodName, isStatic: false);
            return signatures.IsEmpty ? null : new SignatureHelp(receiverType, signatures, commas, open);
        }

        return null;
    }

    private static bool InstanceMethodExists(string receiverType, string member)
    {
        if (receiverType.EndsWith("[]", StringComparison.Ordinal))
        {
            return SequenceSurfaceTemplate.Members.TryGetValue(member, out var sequenceMember)
                && sequenceMember.IsMethod;
        }

        if (TrySplitImmutableTypeName(receiverType, out var immutableKind, out _))
        {
            return (ImmutableCollectionSurfaces[immutableKind].Members.TryGetValue(member, out var kindMember)
                    && kindMember.IsMethod)
                || (SequenceSurfaceTemplate.Members.TryGetValue(member, out var shared)
                    && shared.IsMethod
                    && !ArrayOnlySequenceMembers.Contains(member));
        }

        var key = IsDelegateTypeName(receiverType) ? "Delegate"
            : receiverType.Contains('.', StringComparison.Ordinal) ? "Int32"
            : receiverType;
        return InstanceReceiverSurfaces.TryGetValue(key, out var surface)
            && surface.Members.TryGetValue(member, out var info)
            && info.IsMethod;
    }

    private static ImmutableArray<MethodSignature> LookupSignatures(
        string typeName,
        string methodName,
        bool isStatic) =>
        SignatureCache.GetOrAdd((typeName, methodName, isStatic), static key => ComputeSignatures(key));

    private static ImmutableArray<MethodSignature> ComputeSignatures(
        (string TypeName, string Method, bool IsStatic) key)
    {
        var (typeName, methodName, isStatic) = key;
        var candidates = new List<MethodSignature>();
        if (!isStatic && typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            // A sequence dispatches array instance members, the LINQ operators, and the Array helpers the
            // evaluator renames onto sequences; the extension-style source parameter is not shown.
            AddSignatures(candidates, typeof(Array), methodName, BindingFlags.Public | BindingFlags.Instance, 0);
            AddSignatures(candidates, typeof(Enumerable), methodName, BindingFlags.Public | BindingFlags.Static, 1);
            AddSignatures(candidates, typeof(Array), methodName, BindingFlags.Public | BindingFlags.Static, 1);
        }
        else if (!isStatic && TrySplitImmutableTypeName(typeName, out var immutableKind, out _)
            && ImmutableOpenTypeFor(immutableKind) is { } immutableOpenType)
        {
            AddSignatures(
                candidates, immutableOpenType, methodName, BindingFlags.Public | BindingFlags.Instance, 0);
            AddSignatures(candidates, typeof(Enumerable), methodName, BindingFlags.Public | BindingFlags.Static, 1);
        }
        else
        {
            var normalized = !isStatic && IsDelegateTypeName(typeName) ? "Delegate"
                : !isStatic && typeName.Contains('.', StringComparison.Ordinal) ? "Int32"
                : typeName;
            if (RuntimeTypeFor(normalized) is not { } runtimeType)
            {
                return [];
            }

            var flags = BindingFlags.Public
                | (isStatic ? BindingFlags.Static | BindingFlags.FlattenHierarchy : BindingFlags.Instance);
            AddSignatures(candidates, runtimeType, methodName, flags, 0);
        }

        return
        [
            .. candidates
                .GroupBy(static signature => string.Join(
                    "|", signature.Parameters.Select(static parameter => parameter.TypeText)))
                .Select(static group => group.First())
                .OrderBy(static signature => signature.Parameters.Length)
                .ThenBy(
                    static signature => string.Join(
                        "|", signature.Parameters.Select(static parameter => parameter.TypeText)),
                    StringComparer.Ordinal)
                .Take(MaximumSignatureOverloads),
        ];
    }

    private static void AddSignatures(
        List<MethodSignature> candidates,
        Type runtimeType,
        string methodName,
        BindingFlags flags,
        int skipParameters)
    {
        foreach (var method in runtimeType.GetMethods(flags))
        {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)
                || method.IsSpecialName
                || method.GetParameters().Length < skipParameters)
            {
                continue;
            }

            candidates.Add(new MethodSignature(
                method.Name,
                [
                    .. method.GetParameters()
                        .Skip(skipParameters)
                        .Select(static parameter => new SignatureParameter(
                            parameter.Name ?? "value", FormatTypeText(parameter.ParameterType))),
                ],
                FormatTypeText(method.ReturnType)));
        }
    }

    private static string FormatTypeText(Type type)
    {
        if (type.IsByRef)
        {
            return "ref " + FormatTypeText(type.GetElementType()!);
        }

        if (type.IsArray)
        {
            return FormatTypeText(type.GetElementType()!) + "[]";
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return FormatTypeText(underlying) + "?";
        }

        if (type.IsGenericType)
        {
            var name = type.Name;
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick >= 0)
            {
                name = name[..tick];
            }

            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FormatTypeText))}>";
        }

        return type switch
        {
            _ when type == typeof(void) => "void",
            _ when type == typeof(object) => "object",
            _ when type == typeof(string) => "string",
            _ when type == typeof(char) => "char",
            _ when type == typeof(bool) => "bool",
            _ when type == typeof(int) => "int",
            _ when type == typeof(uint) => "uint",
            _ when type == typeof(long) => "long",
            _ when type == typeof(ulong) => "ulong",
            _ when type == typeof(short) => "short",
            _ when type == typeof(ushort) => "ushort",
            _ when type == typeof(byte) => "byte",
            _ when type == typeof(sbyte) => "sbyte",
            _ when type == typeof(double) => "double",
            _ when type == typeof(float) => "float",
            _ when type == typeof(decimal) => "decimal",
            _ => type.Name,
        };
    }

    /// <summary>The open generic runtime type of one immutable collection kind, for signature reflection.</summary>
    private static Type? ImmutableOpenTypeFor(string kindName) => kindName switch
    {
        "ImmutableArray" => typeof(System.Collections.Immutable.ImmutableArray<>),
        "ImmutableList" => typeof(System.Collections.Immutable.ImmutableList<>),
        "ImmutableHashSet" => typeof(System.Collections.Immutable.ImmutableHashSet<>),
        "ImmutableSortedSet" => typeof(System.Collections.Immutable.ImmutableSortedSet<>),
        "ImmutableQueue" => typeof(System.Collections.Immutable.ImmutableQueue<>),
        "ImmutableStack" => typeof(System.Collections.Immutable.ImmutableStack<>),
        "ImmutableDictionary" => typeof(System.Collections.Immutable.ImmutableDictionary<,>),
        "ImmutableSortedDictionary" => typeof(System.Collections.Immutable.ImmutableSortedDictionary<,>),
        _ => null,
    };

    private static Type? RuntimeTypeFor(string name) => name switch
    {
        "String" or "string" => typeof(string),
        "Char" or "char" => typeof(char),
        "Boolean" or "bool" => typeof(bool),
        "Int32" or "int" => typeof(int),
        "UInt32" or "uint" => typeof(uint),
        "Int64" or "long" => typeof(long),
        "UInt64" or "ulong" => typeof(ulong),
        "Int16" or "short" => typeof(short),
        "UInt16" or "ushort" => typeof(ushort),
        "Byte" or "byte" => typeof(byte),
        "SByte" or "sbyte" => typeof(sbyte),
        "Double" or "double" => typeof(double),
        "Single" or "float" => typeof(float),
        "Decimal" or "decimal" => typeof(decimal),
        "IntPtr" => typeof(nint),
        "UIntPtr" => typeof(nuint),
        "Int128" => typeof(Int128),
        "UInt128" => typeof(UInt128),
        "BigInteger" => typeof(System.Numerics.BigInteger),
        "Half" => typeof(Half),
        "NFloat" => typeof(System.Runtime.InteropServices.NFloat),
        "DateTime" => typeof(DateTime),
        "DateTimeOffset" => typeof(DateTimeOffset),
        "TimeSpan" => typeof(TimeSpan),
        "DateOnly" => typeof(DateOnly),
        "TimeOnly" => typeof(TimeOnly),
        "Guid" => typeof(Guid),
        "Version" => typeof(Version),
        "Rune" => typeof(System.Text.Rune),
        "Encoding" => typeof(System.Text.Encoding),
        "Regex" => typeof(System.Text.RegularExpressions.Regex),
        "Match" => typeof(System.Text.RegularExpressions.Match),
        "Group" => typeof(System.Text.RegularExpressions.Group),
        "Capture" => typeof(System.Text.RegularExpressions.Capture),
        "MatchCollection" => typeof(System.Text.RegularExpressions.MatchCollection),
        "GroupCollection" => typeof(System.Text.RegularExpressions.GroupCollection),
        "CaptureCollection" => typeof(System.Text.RegularExpressions.CaptureCollection),
        "MethodInfo" => typeof(MethodInfo),
        "ConstructorInfo" => typeof(ConstructorInfo),
        "PropertyInfo" => typeof(PropertyInfo),
        "FieldInfo" => typeof(FieldInfo),
        "ParameterInfo" => typeof(ParameterInfo),
        "Type" => typeof(Type),
        "Enum" => typeof(Enum),
        "Delegate" => typeof(Delegate),
        "MulticastDelegate" => typeof(MulticastDelegate),
        "DBNull" => typeof(DBNull),
        "Math" => typeof(Math),
        "MathF" => typeof(MathF),
        "ImmutableArray" => typeof(System.Collections.Immutable.ImmutableArray),
        "ImmutableList" => typeof(System.Collections.Immutable.ImmutableList),
        "ImmutableHashSet" => typeof(System.Collections.Immutable.ImmutableHashSet),
        "ImmutableSortedSet" => typeof(System.Collections.Immutable.ImmutableSortedSet),
        "ImmutableQueue" => typeof(System.Collections.Immutable.ImmutableQueue),
        "ImmutableStack" => typeof(System.Collections.Immutable.ImmutableStack),
        "ImmutableDictionary" => typeof(System.Collections.Immutable.ImmutableDictionary),
        "ImmutableSortedDictionary" => typeof(System.Collections.Immutable.ImmutableSortedDictionary),
        "KeyValuePair" => typeof(System.Collections.Generic.KeyValuePair),
        "Array" => typeof(Array),
        "Convert" => typeof(Convert),
        "Activator" => typeof(Activator),
        "Enumerable" => typeof(Enumerable),
        "CharUnicodeInfo" => typeof(System.Globalization.CharUnicodeInfo),
        _ => null,
    };

    /// <summary>
    /// Builds the session-derived completion catalog: the adopted root's declared fields and the top-level type
    /// names of dump modules. Modules contribute in ascending metadata size, so when the type bound bites, the
    /// application's own modules — the small ones — are the names that survive.
    /// </summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="root">The adopted root, or null.</param>
    /// <param name="rootIdentifier">The identifier root-relative expressions use.</param>
    /// <returns>The catalog; building never throws on malformed metadata, it simply contributes fewer names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static CompletionCatalog BuildCatalog(
        ClrmdDumpSession session,
        RootSelection? root,
        string rootIdentifier)
    {
        ArgumentNullException.ThrowIfNull(session);
        var rootMembers = ImmutableArray<CompletionItem>.Empty;
        if (root?.TryResolveHeapObject(session) is { } heapObject)
        {
            var fields = session.ListInstanceFieldNames(heapObject);
            if (fields.Status == ClrmdEvidenceStatus.Exact && fields.Value is { } fieldList)
            {
                rootMembers =
                [
                    .. fieldList.Fields
                        .Select(static field => new CompletionItem(
                            field.Name, CompletionItemKind.Field, field.TypeName))
                        .OrderBy(static item => item.Text, StringComparer.OrdinalIgnoreCase),
                ];
            }
        }

        var typeNames = ImmutableArray.CreateBuilder<string>();
        var identities = session.Modules
            .Select(module => (Module: module, Content: session.ReadModuleContentIdentity(module)))
            .Where(static pair => pair.Content.Status == ClrmdEvidenceStatus.Exact
                && pair.Content.Value is not null
                && pair.Content.Evidence.Length == 1
                && pair.Content.Evidence[0].Status == MemoryReadStatus.Exact)
            .OrderBy(static pair => pair.Content.Evidence[0].Bytes.Length);
        foreach (var (_, content) in identities)
        {
            if (typeNames.Count >= MaximumCatalogTypes)
            {
                break;
            }

            try
            {
                using var provider = MetadataReaderProvider.FromMetadataImage(content.Evidence[0].Bytes);
                CollectTopLevelTypeNames(provider.GetMetadataReader(), typeNames, MaximumCatalogTypes);
            }
            catch (BadImageFormatException)
            {
                // A malformed image contributes no names; completion stays honest with fewer candidates.
            }
        }

        return new CompletionCatalog
        {
            RootIdentifier = rootIdentifier.Trim(),
            HasRoot = root is not null,
            RootMembers = rootMembers,
            TypeFullNames = [.. typeNames.Distinct(StringComparer.Ordinal)],
        };
    }

    /// <summary>Reads one metadata type's static-field completions: literals first-class, storage fields too.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="typeFullName">The dotted full name of a top-level type.</param>
    /// <returns>The static-field completions, possibly empty.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ImmutableArray<CompletionItem> ListStaticMemberCompletions(
        ClrmdDumpSession session,
        string typeFullName)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(typeFullName);
        var separator = typeFullName.LastIndexOf('.');
        var typeNamespace = separator < 0 ? string.Empty : typeFullName[..separator];
        var typeName = separator < 0 ? typeFullName : typeFullName[(separator + 1)..];
        var members = ImmutableArray.CreateBuilder<CompletionItem>();
        foreach (var module in session.Modules)
        {
            var content = session.ReadModuleContentIdentity(module);
            if (content.Status != ClrmdEvidenceStatus.Exact ||
                content.Value is null ||
                content.Evidence.Length != 1 ||
                content.Evidence[0].Status != MemoryReadStatus.Exact)
            {
                continue;
            }

            try
            {
                using var provider = MetadataReaderProvider.FromMetadataImage(content.Evidence[0].Bytes);
                CollectStaticMemberCompletions(provider.GetMetadataReader(), typeNamespace, typeName, members);
            }
            catch (BadImageFormatException)
            {
                // A malformed image contributes no members.
            }
        }

        return FinishMemberList(members);
    }

    /// <summary>Collects the spellable top-level type full names of one metadata image, bounded.</summary>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="typeNames">The builder the names accumulate into, across images.</param>
    /// <param name="maximum">The greatest total number of names to collect.</param>
    internal static void CollectTopLevelTypeNames(
        MetadataReader reader,
        ImmutableArray<string>.Builder typeNames,
        int maximum)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            if (typeNames.Count >= maximum)
            {
                break;
            }

            var typeDefinition = reader.GetTypeDefinition(handle);
            if (!typeDefinition.GetDeclaringType().IsNil)
            {
                continue;
            }

            var name = reader.GetString(typeDefinition.Name);
            if (name.Length == 0 || name.Contains('<', StringComparison.Ordinal) || name == "<Module>")
            {
                continue;
            }

            var typeNamespace = reader.GetString(typeDefinition.Namespace);
            typeNames.Add(typeNamespace.Length == 0 ? name : $"{typeNamespace}.{name}");
        }
    }

    /// <summary>Collects one named type's static-field completions from one metadata image.</summary>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="typeNamespace">The type's namespace, possibly empty.</param>
    /// <param name="typeName">The type's simple name.</param>
    /// <param name="members">The builder the members accumulate into, across images.</param>
    internal static void CollectStaticMemberCompletions(
        MetadataReader reader,
        string typeNamespace,
        string typeName,
        ImmutableArray<CompletionItem>.Builder members)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDefinition = reader.GetTypeDefinition(handle);
            if (!typeDefinition.GetDeclaringType().IsNil ||
                !reader.StringComparer.Equals(typeDefinition.Name, typeName) ||
                !reader.StringComparer.Equals(typeDefinition.Namespace, typeNamespace))
            {
                continue;
            }

            foreach (var fieldHandle in typeDefinition.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                if ((field.Attributes & FieldAttributes.Static) == 0)
                {
                    continue;
                }

                var fieldName = reader.GetString(field.Name);
                if (fieldName.Length == 0 || fieldName.Contains('<', StringComparison.Ordinal))
                {
                    continue;
                }

                var isLiteral = (field.Attributes & FieldAttributes.Literal) != 0;
                members.Add(new CompletionItem(
                    fieldName,
                    CompletionItemKind.Field,
                    isLiteral ? "const" : "static field"));
            }
        }
    }

    /// <summary>Deduplicates and orders one collected member list for display.</summary>
    /// <param name="members">The collected members.</param>
    /// <returns>The display list.</returns>
    internal static ImmutableArray<CompletionItem> FinishMemberList(
        ImmutableArray<CompletionItem>.Builder members) =>
    [
        .. members
            .GroupBy(static item => item.Text, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static item => item.Text, StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>
    /// Reads one runtime type's instance-field completions, for member-chain hops: each item's detail carries the
    /// field's declared type name, so the next hop can realize its fields in turn.
    /// </summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="typeFullName">The runtime full name of the type, as the previous hop's detail spells it.</param>
    /// <returns>The instance-field completions; empty when the type is unavailable, which is a realized answer.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ImmutableArray<CompletionItem> ListInstanceMemberCompletions(
        ClrmdDumpSession session,
        string typeFullName)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(typeFullName);
        var fields = session.ListInstanceFieldNamesByTypeName(typeFullName);
        if (fields.Status != ClrmdEvidenceStatus.Exact || fields.Value is not { } list)
        {
            return [];
        }

        return
        [
            .. list.Fields
                .Select(static field => new CompletionItem(field.Name, CompletionItemKind.Field, field.TypeName))
                .GroupBy(static item => item.Text, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static item => item.Text, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Computes the completions for one caret position: member completion after a dot, and keyword-plus-identifier
    /// completion inside a partial token. Purely lexical, so it never fails; an unknown receiver yields no items.
    /// </summary>
    /// <param name="catalog">The session-derived catalog; <see cref="CompletionCatalog.Empty"/> works context-free.</param>
    /// <param name="text">The expression text being edited.</param>
    /// <param name="caretOffset">The caret offset within <paramref name="text"/>.</param>
    /// <param name="context">The editor-specific context, or null for <see cref="CompletionContext.Expression"/>.</param>
    /// <param name="explicitInvocation">
    /// Whether the user asked for completion (Ctrl+Space); an explicit ask completes an empty token, offering the
    /// whole applicable universe, where as-you-type completion waits for a first character.
    /// </param>
    /// <returns>The completion result, possibly empty.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static CompletionResult Complete(
        CompletionCatalog catalog,
        string text,
        int caretOffset,
        CompletionContext? context = null,
        bool explicitInvocation = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(text);
        context ??= CompletionContext.Expression;
        var caret = Math.Clamp(caretOffset, 0, text.Length);
        var prefixStart = caret;
        while (prefixStart > 0 && IsIdentifierChar(text[prefixStart - 1]))
        {
            prefixStart--;
        }

        var prefix = text[prefixStart..caret];
        if (prefix.Length > 0 && char.IsAsciiDigit(prefix[0]))
        {
            // The token is a numeric literal, not an identifier; '32' must not surface 'Int32'.
            return CompletionResult.Empty;
        }

        if (prefixStart > 0 && text[prefixStart - 1] == '.')
        {
            var chain = ReadReceiverChain(text, prefixStart - 1);
            return chain.Length == 0
                ? CompletionResult.Empty
                : CompleteMembers(catalog, context, chain, prefix, prefixStart, caret);
        }

        if (prefix.Length == 0 && !explicitInvocation)
        {
            return CompletionResult.Empty;
        }

        var items = new List<ScoredItem>();
        items.AddRange(Score(
            Keywords.Select(static keyword => new CompletionItem(keyword, CompletionItemKind.Keyword)), prefix));
        if (context.AllowsStatements && text.AsSpan(0, prefixStart).IsWhiteSpace())
        {
            items.AddRange(Score(StatementKeywords, prefix));
        }

        items.AddRange(Score(context.Locals, prefix));
        items.AddRange(Score(
            ModeledTypeNames.Select(static name => new CompletionItem(name, CompletionItemKind.Type)), prefix));
        if (catalog.HasRoot && catalog.RootIdentifier.Length > 0)
        {
            items.AddRange(Score(
                [new CompletionItem(catalog.RootIdentifier, CompletionItemKind.Root, "adopted root")], prefix));
        }

        items.AddRange(Score(
            catalog.TypeFullNames
                .Select(static fullName => FirstSegment(fullName))
                .Distinct(StringComparer.Ordinal)
                .Select(static segment => new CompletionItem(
                    segment, CompletionItemKind.Namespace, "from dump modules")),
            prefix));
        var referenceNames = context.References?.TypeFullNames ?? [];
        items.AddRange(Score(
            referenceNames
                .Select(static fullName => FirstSegment(fullName))
                .Distinct(StringComparer.Ordinal)
                .Select(static segment => new CompletionItem(
                    segment, CompletionItemKind.Namespace, "from references")),
            prefix));

        var pendingStaticImport = (string?)null;
        if (!context.Usings.IsEmpty)
        {
            // Imported namespaces make their contents spellable bare: their types complete as types, and deeper
            // sub-namespace segments as namespaces, each annotated with the namespace that admits it.
            var seenImported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var importedNamespace in context.Usings.ImportedNamespaces)
            {
                var namespacePrefix = importedNamespace + ".";
                foreach (var fullName in catalog.TypeFullNames.Concat(referenceNames))
                {
                    if (!fullName.StartsWith(namespacePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var rest = fullName[namespacePrefix.Length..];
                    var next = FirstSegment(rest);
                    var isLeaf = next.Length == rest.Length;
                    if (Rank(next, prefix) is { } rank && seenImported.Add(next))
                    {
                        items.Add(new ScoredItem(
                            new CompletionItem(
                                next,
                                isLeaf ? CompletionItemKind.Type : CompletionItemKind.Namespace,
                                importedNamespace),
                            rank));
                    }
                }
            }

            // Alias names complete as identifiers, annotated with their targets.
            items.AddRange(Score(
                context.Usings.Aliases
                    .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new CompletionItem(
                        pair.Key,
                        catalog.TypeFullNames.Contains(pair.Value, StringComparer.Ordinal)
                            || referenceNames.Contains(pair.Value, StringComparer.Ordinal)
                            ? CompletionItemKind.Type
                            : CompletionItemKind.Namespace,
                        pair.Value)),
                prefix));

            // Statically imported members complete bare: a referenced type's members realize synchronously, a
            // dump type's members once realized — the first unrealized import asks the host to fetch, and the
            // refreshed catalog answers the next query.
            foreach (var staticType in context.Usings.StaticImportedTypes)
            {
                if (catalog.TypeMembers.TryGetValue(staticType, out var importedMembers))
                {
                    items.AddRange(Score(importedMembers, prefix));
                }
                else if (context.References?.GetStaticMembers(staticType) is { IsEmpty: false } referenceMembers)
                {
                    items.AddRange(Score(referenceMembers, prefix));
                }
                else if (pendingStaticImport is null
                    && catalog.TypeFullNames.Contains(staticType, StringComparer.Ordinal))
                {
                    pendingStaticImport = staticType;
                }
            }
        }

        return Finish(items, prefixStart, caret - prefixStart) with { PendingTypeMembers = pendingStaticImport };
    }

    private static CompletionResult CompleteMembers(
        CompletionCatalog catalog,
        CompletionContext context,
        ImmutableArray<ReceiverSegment> chain,
        string prefix,
        int prefixStart,
        int caret)
    {
        var replaceLength = caret - prefixStart;

        // Qualified names, root chains, and receiver lookups speak in plain dotted identifiers; a chain with
        // calls, indexes, or a literal head resolves only as a typed value chain.
        var isPlain = chain.All(static segment =>
            segment is { IsInvocation: false, IndexCount: 0, LiteralType: null });
        var segments = isPlain ? [.. chain.Select(static segment => segment.Name)] : ImmutableArray<string>.Empty;

        // A member chain rooted at the adopted root walks declared field types: after 'root.A.' the candidates
        // are the instance fields of A's declared type, realized from the dump on demand — the same field set a
        // chain hop evaluates. An unknown hop offers nothing rather than guessing.
        if (isPlain
            && catalog.HasRoot
            && segments.Length >= 1
            && string.Equals(segments[0], catalog.RootIdentifier, StringComparison.Ordinal))
        {
            var chainMembers = catalog.RootMembers;
            foreach (var segment in segments.Skip(1))
            {
                var hop = chainMembers.FirstOrDefault(
                    member => string.Equals(member.Text, segment, StringComparison.Ordinal));
                if (hop?.Detail is not { Length: > 0 } declaredType)
                {
                    return Finish([], prefixStart, replaceLength);
                }

                if (!catalog.TypeInstanceMembers.TryGetValue(declaredType, out chainMembers))
                {
                    return new CompletionResult(
                        [], prefixStart, replaceLength, PendingInstanceMembers: declaredType);
                }
            }

            return Finish(Score(chainMembers, prefix), prefixStart, replaceLength);
        }

        // A typed value chain — a literal, a declared variable, or a modeled static value at the head, folded
        // through members, calls, and index accesses — completes the resulting value's instance surface:
        // 's.Trim().', 'xs[0].', '"text".Split(',')[0].', 'Guid.Empty.Version.', 'x.GetType().Name.'.
        if (ResolveChainValueType(context, chain) is { } chainType)
        {
            return Finish(Score(InstanceMembersForStoredType(chainType), prefix), prefixStart, replaceLength);
        }

        if (isPlain && segments is [var single] && ReceiverMembers.TryGetValue(single, out var members))
        {
            return Finish(
                Score(members.Select(static member => new CompletionItem(member, CompletionItemKind.Member)),
                    prefix),
                prefixStart,
                replaceLength);
        }

        if (!isPlain)
        {
            return Finish([], prefixStart, replaceLength);
        }

        // A qualified receiver resolves as written first; with using directives active, the alias-substituted
        // and namespace-prefixed spellings then get their chance, in evaluation's own candidate order.
        var referenceNames = context.References?.TypeFullNames ?? [];
        var candidates = context.Usings.IsEmpty
            ? ImmutableArray.Create(string.Join('.', segments))
            : context.Usings.ExpandTypeCandidates(segments);
        foreach (var dotted in candidates)
        {
            if (catalog.TypeMembers.TryGetValue(dotted, out var realized))
            {
                return Finish(Score(realized, prefix), prefixStart, replaceLength);
            }

            // A referenced type's static members realize synchronously from the retained metadata bytes.
            if (referenceNames.Contains(dotted, StringComparer.Ordinal)
                && context.References!.GetStaticMembers(dotted) is { IsEmpty: false } referenceMembers)
            {
                return Finish(Score(referenceMembers, prefix), prefixStart, replaceLength);
            }

            var namespacePrefix = dotted + ".";
            var items = new List<ScoredItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fullName in catalog.TypeFullNames.Concat(referenceNames))
            {
                if (!fullName.StartsWith(namespacePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var rest = fullName[namespacePrefix.Length..];
                var next = FirstSegment(rest);
                var isLeaf = next.Length == rest.Length;
                if (Rank(next, prefix) is { } rank && seen.Add(next))
                {
                    items.Add(new ScoredItem(
                        new CompletionItem(next, isLeaf ? CompletionItemKind.Type : CompletionItemKind.Namespace),
                        rank));
                }
            }

            if (items.Count > 0)
            {
                return Finish(items, prefixStart, replaceLength);
            }

            if (catalog.TypeFullNames.Contains(dotted, StringComparer.Ordinal))
            {
                // The receiver is a realizable metadata type; the host fetches its members and re-queries.
                return new CompletionResult([], prefixStart, replaceLength, PendingTypeMembers: dotted);
            }
        }

        return Finish([], prefixStart, replaceLength);
    }

    /// <summary>One parsed hop of a receiver chain, read right-to-left before the completion dot.</summary>
    /// <param name="Name">The member or identifier name; empty for a literal head.</param>
    /// <param name="IsInvocation">Whether the hop is spelled as a call, with balanced parentheses.</param>
    /// <param name="IndexCount">How many index applications follow the hop.</param>
    /// <param name="LiteralType">The literal head's value type — 'String' or 'Char' — or null.</param>
    private readonly record struct ReceiverSegment(
        string Name,
        bool IsInvocation,
        int IndexCount,
        string? LiteralType = null);

    private static ImmutableArray<ReceiverSegment> ReadReceiverChain(string text, int dotOffset)
    {
        var segments = new List<ReceiverSegment>();
        var end = dotOffset;
        while (end > 0)
        {
            var indexCount = 0;
            var invoked = false;

            // Trailing suffix groups read backwards: index groups first, then at most one call group adjacent
            // to the name — "Split(',')[0]" is a call followed by one index. The balance scan is lexical, like
            // the rest of completion; a bracket inside a string argument defeats it and simply completes
            // nothing.
            while (end > 0 && text[end - 1] is ')' or ']')
            {
                var close = text[end - 1];
                var open = close == ')' ? '(' : '[';
                var depth = 0;
                var scan = end - 1;
                while (scan >= 0)
                {
                    if (text[scan] == close)
                    {
                        depth++;
                    }
                    else if (text[scan] == open)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            break;
                        }
                    }

                    scan--;
                }

                if (scan < 0)
                {
                    return [];
                }

                end = scan;
                if (close == ')')
                {
                    invoked = true;
                    break;
                }

                indexCount++;
            }

            // A string or char literal ends the backward walk as the chain's head value.
            if (end > 0 && text[end - 1] is '"' or '\'')
            {
                if (invoked)
                {
                    return [];
                }

                var quote = text[end - 1];
                var scan = end - 2;
                while (scan >= 0 && (text[scan] != quote || (scan > 0 && text[scan - 1] == '\\')))
                {
                    scan--;
                }

                if (scan < 0)
                {
                    return [];
                }

                segments.Insert(0, new ReceiverSegment(
                    string.Empty,
                    IsInvocation: false,
                    indexCount,
                    quote == '"' ? "String" : "Char"));
                return [.. segments];
            }

            var start = end;
            while (start > 0 && IsIdentifierChar(text[start - 1]))
            {
                start--;
            }

            if (start == end)
            {
                return [];
            }

            segments.Insert(0, new ReceiverSegment(text[start..end], invoked, indexCount));
            if (start > 0 && text[start - 1] == '.')
            {
                end = start - 1;
                continue;
            }

            break;
        }

        return [.. segments];
    }

    private readonly record struct ScoredItem(CompletionItem Item, int Rank);

    private static IEnumerable<ScoredItem> Score(IEnumerable<CompletionItem> items, string prefix)
    {
        foreach (var item in items)
        {
            if (Rank(item.Text, prefix) is { } rank)
            {
                yield return new ScoredItem(item, rank);
            }
        }
    }

    private static CompletionResult Finish(
        IEnumerable<ScoredItem> items,
        int replaceStart,
        int replaceLength)
    {
        var ordered = items
            .GroupBy(static scored => scored.Item.Text, StringComparer.Ordinal)
            .Select(static group => group.OrderBy(static scored => scored.Rank).First())
            .OrderBy(static scored => scored.Rank)
            .ThenBy(static scored => scored.Item.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static scored => scored.Item.Kind)
            .Select(static scored => scored.Item)
            .Take(MaximumItems)
            .ToImmutableArray();
        return new CompletionResult(ordered, replaceStart, replaceLength);
    }

    /// <summary>
    /// Ranks how well a candidate matches the typed token, IDE-style: an exact match ranks first — so the
    /// selection never falls to a longer neighbor of a fully typed name — then a case-sensitive prefix, a
    /// case-insensitive one, a camel-hump match, and a plain substring. The looser modes need two characters, so
    /// a single keystroke never floods the list.
    /// </summary>
    /// <returns>The rank, lower matching better, or null when the candidate does not match.</returns>
    private static int? Rank(string candidate, string prefix)
    {
        if (prefix.Length == 0)
        {
            return 0;
        }

        if (string.Equals(candidate, prefix, StringComparison.Ordinal))
        {
            return -1;
        }

        if (candidate.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (prefix.Length >= 2)
        {
            if (MatchesCamelHumps(candidate, prefix))
            {
                return 2;
            }

            if (candidate.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }
        }

        return null;
    }

    /// <summary>
    /// Matches a pattern against a candidate's camel humps, the way ReSharper and Rider do: each pattern character
    /// either starts a hump — the candidate's start, an upper-case letter, or a character after '_' — or continues
    /// the run begun by the previous one, so <c>DTO</c> and <c>dto</c> both find <c>DateTimeOffset</c>, and
    /// <c>SqEs</c> finds <c>ReciprocalSqrtEstimate</c> from its middle humps.
    /// </summary>
    private static bool MatchesCamelHumps(string candidate, string pattern)
    {
        return MatchFrom(0, 0, inRun: false);

        bool MatchFrom(int candidateIndex, int patternIndex, bool inRun)
        {
            if (patternIndex == pattern.Length)
            {
                return true;
            }

            if (inRun
                && candidateIndex < candidate.Length
                && CharsEqualIgnoreCase(candidate[candidateIndex], pattern[patternIndex])
                && MatchFrom(candidateIndex + 1, patternIndex + 1, inRun: true))
            {
                return true;
            }

            for (var index = candidateIndex; index < candidate.Length; index++)
            {
                if (IsHumpStart(candidate, index)
                    && CharsEqualIgnoreCase(candidate[index], pattern[patternIndex])
                    && MatchFrom(index + 1, patternIndex + 1, inRun: true))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool IsHumpStart(string candidate, int index) =>
        index == 0
        || char.IsUpper(candidate[index])
        || candidate[index - 1] is '_' or '@'
        || (char.IsAsciiDigit(candidate[index]) && !char.IsAsciiDigit(candidate[index - 1]));

    private static bool CharsEqualIgnoreCase(char left, char right) =>
        char.ToUpperInvariant(left) == char.ToUpperInvariant(right);

    private static bool IsIdentifierChar(char value) => char.IsLetterOrDigit(value) || value is '_' or '@';

    private static string FirstSegment(string dotted)
    {
        var separator = dotted.IndexOf('.', StringComparison.Ordinal);
        return separator < 0 ? dotted : dotted[..separator];
    }
}
