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
    public ConstantUsingDirectiveSet Usings { get; init; } = ConstantUsingDirectiveSet.Empty;

    /// <summary>Gets the '#r'-referenced assemblies' completion facts, or null with no references.</summary>
    public ReferenceCompletionIndex? References { get; init; }
}

/// <summary>
/// The completion facts of the immediate window's <c>#r</c> references: top-level type names scanned once at
/// construction, and static-member lists realized synchronously on first use — a reference's metadata is retained
/// local bytes, so no session round-trip is needed. An aliased reference is reachable only through its extern
/// alias, which the completion grammar never spells, so it contributes nothing here.
/// </summary>
/// <remarks>The realized-member cache is not thread-safe; one editor consults it from its UI thread.</remarks>
public sealed class ReferenceCompletionIndex
{
    private readonly ImmutableArray<ConstantReferenceAssembly> globalReferences;
    private readonly Dictionary<string, ImmutableArray<CompletionItem>> realizedMembers = new(StringComparer.Ordinal);

    /// <summary>Gets the empty index.</summary>
    public static ReferenceCompletionIndex Empty { get; } = new([]);

    /// <summary>Builds the index over the current references.</summary>
    /// <param name="references">The referenced assemblies; aliased ones are skipped.</param>
    /// <exception cref="ArgumentNullException"><paramref name="references"/> is default.</exception>
    public ReferenceCompletionIndex(ImmutableArray<ConstantReferenceAssembly> references)
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
        "and", "bool", "byte", "char", "checked", "decimal", "default", "double", "false", "float", "global",
        "int", "is", "long", "nameof", "new", "not", "null", "object", "or", "sbyte", "short", "sizeof", "string",
        "switch", "true", "typeof", "uint", "ulong", "unchecked", "ushort", "when",
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
        "Func", "Guid", "ICollection", "IComparable", "IDictionary", "IEnumerable", "IEquatable", "IList",
        "Int128", "IReadOnlyCollection", "IReadOnlyDictionary", "IReadOnlyList", "KeyValuePair", "List", "Math",
        "MemberTypes", "MulticastDelegate", "Nullable", "Object", "Predicate", "Regex", "RegexOptions", "Rune",
        "String", "StringComparison", "StringSplitOptions", "TimeOnly", "TimeSpan", "TypeCode", "UInt128",
        "UnicodeCategory", "ValueType", "Version",
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
            ["Enumerable"] = ["Empty", "Range", "Repeat"],
            ["Enum"] =
            [
                "GetName", "GetNames", "GetUnderlyingType", "GetValues", "IsDefined", "Parse", "ToObject",
                "TryParse",
            ],
            ["Array"] =
            [
                "BinarySearch", "Clear", "ConstrainedCopy", "ConvertAll", "Copy", "Empty", "Exists", "Fill",
                "Find", "FindAll", "FindIndex", "FindLast", "FindLastIndex", "IndexOf", "LastIndexOf", "MaxLength",
                "Resize", "Reverse", "SetValue", "Sort", "TrueForAll",
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
            ["int"] = ["MaxValue", "MinValue"],
            ["uint"] = ["MaxValue", "MinValue"],
            ["long"] = ["MaxValue", "MinValue"],
            ["ulong"] = ["MaxValue", "MinValue"],
            ["short"] = ["MaxValue", "MinValue"],
            ["ushort"] = ["MaxValue", "MinValue"],
            ["byte"] = ["MaxValue", "MinValue"],
            ["sbyte"] = ["MaxValue", "MinValue"],
            ["decimal"] = ["MaxValue", "MinValue", "One", "Zero", "MinusOne"],
            ["double"] =
            ["Epsilon", "MaxValue", "MinValue", "NaN", "NegativeInfinity", "Pi", "PositiveInfinity", "Tau"],
            ["float"] = ["Epsilon", "MaxValue", "MinValue", "NaN", "NegativeInfinity", "PositiveInfinity"],
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

    // ---- Instance members over stored immediate-variable values ------------------------------------------------
    // These tables mirror the constant evaluator's instance dispatch exactly, including members that produce a
    // deliberate explained stop (ToUpper redirects to the invariant form, ToLocalTime is nondeterministic): the
    // stop is part of the documented surface, so the name still completes.

    private static readonly ImmutableArray<CompletionItem> SequenceInstanceMembers = BuildInstanceItems(
        ["Length", "LongLength", "Rank"],
        [
            "All", "Any", "Append", "Average", "Concat", "Contains", "Count", "Distinct", "ElementAt",
            "ElementAtOrDefault", "Except", "FindIndex", "FindLastIndex", "First", "FirstOrDefault", "GetLength",
            "GetLowerBound", "GetType", "GetUpperBound", "GetValue", "GroupBy", "GroupJoin", "Intersect", "Join",
            "Last", "LastOrDefault", "LongCount", "Max", "Min", "Order", "OrderBy", "OrderByDescending",
            "OrderDescending", "Prepend", "Reverse", "Select", "SelectMany", "SequenceEqual", "Single",
            "SingleOrDefault", "Skip", "SkipLast", "SkipWhile", "Sum", "Take", "TakeLast", "TakeWhile", "ThenBy",
            "ThenByDescending", "ToArray", "ToList", "Union", "Where",
        ]);

    private static readonly ImmutableArray<CompletionItem> DelegateInstanceMembers = BuildInstanceItems(
        ["HasSingleTarget", "Method", "Target"],
        ["DynamicInvoke", "Equals", "GetInvocationList", "GetType", "Invoke", "ToString"]);

    // A plain scalar — a numeric, a Boolean, or a stored enum, which reads back as its underlying value — only
    // answers the universal members.
    private static readonly ImmutableArray<CompletionItem> ScalarInstanceMembers = BuildInstanceItems(
        [],
        ["GetType", "ToString"]);

    private static readonly ImmutableDictionary<string, ImmutableArray<CompletionItem>> InstanceReceiverMembers =
        BuildInstanceReceiverTables();

    private static ImmutableDictionary<string, ImmutableArray<CompletionItem>> BuildInstanceReceiverTables()
    {
        var tables = new Dictionary<string, ImmutableArray<CompletionItem>>(StringComparer.Ordinal)
        {
            ["String"] = BuildInstanceItems(
                ["Length"],
                [
                    "CompareTo", "Contains", "EndsWith", "EnumerateRunes", "Equals", "GetType", "IndexOf",
                    "Insert", "LastIndexOf", "PadLeft", "PadRight", "Remove", "Replace", "Split", "StartsWith",
                    "Substring", "ToCharArray", "ToLower", "ToLowerInvariant", "ToString", "ToUpper",
                    "ToUpperInvariant", "Trim", "TrimEnd", "TrimStart",
                ]),
            ["Char"] = BuildInstanceItems([], ["CompareTo", "Equals", "GetHashCode", "GetType", "ToString"]),
            ["DateTime"] = BuildInstanceItems(
                [
                    "Date", "Day", "DayOfWeek", "DayOfYear", "Hour", "Kind", "Millisecond", "Minute", "Month",
                    "Second", "Ticks", "TimeOfDay", "Year",
                ],
                [
                    "Add", "AddDays", "AddHours", "AddMilliseconds", "AddMinutes", "AddMonths", "AddSeconds",
                    "AddTicks", "AddYears", "GetType", "Subtract", "ToLocalTime", "ToString", "ToUniversalTime",
                ]),
            ["DateTimeOffset"] = BuildInstanceItems(
                [
                    "Date", "DateTime", "Day", "DayOfWeek", "DayOfYear", "Hour", "LocalDateTime", "Millisecond",
                    "Minute", "Month", "Offset", "Second", "Ticks", "TimeOfDay", "UtcDateTime", "UtcTicks",
                    "Year",
                ],
                [
                    "Add", "AddDays", "AddHours", "AddMilliseconds", "AddMinutes", "AddMonths", "AddSeconds",
                    "AddTicks", "AddYears", "GetType", "Subtract", "ToLocalTime", "ToOffset", "ToString",
                    "ToUniversalTime", "ToUnixTimeMilliseconds", "ToUnixTimeSeconds",
                ]),
            ["TimeSpan"] = BuildInstanceItems(
                [
                    "Days", "Hours", "Milliseconds", "Minutes", "Seconds", "Ticks", "TotalDays", "TotalHours",
                    "TotalMilliseconds", "TotalMinutes", "TotalSeconds",
                ],
                ["Add", "Divide", "Duration", "GetType", "Multiply", "Negate", "Subtract", "ToString"]),
            ["DateOnly"] = BuildInstanceItems(
                ["Day", "DayNumber", "DayOfWeek", "DayOfYear", "Month", "Year"],
                ["AddDays", "AddMonths", "AddYears", "GetType", "ToDateTime", "ToString"]),
            ["TimeOnly"] = BuildInstanceItems(
                ["Hour", "Millisecond", "Minute", "Second", "Ticks"],
                ["Add", "AddHours", "AddMinutes", "GetType", "ToString", "ToTimeSpan"]),
            ["Guid"] = BuildInstanceItems(["Variant", "Version"], ["CompareTo", "GetType", "ToString"]),
            ["Version"] = BuildInstanceItems(
                ["Build", "Major", "MajorRevision", "Minor", "MinorRevision", "Revision"],
                ["CompareTo", "GetType", "ToString"]),
            ["DBNull"] = BuildInstanceItems([], ["Equals", "GetType", "GetTypeCode", "ToString"]),
            ["Rune"] = BuildInstanceItems(
                ["IsAscii", "IsBmp", "Plane", "Utf16SequenceLength", "Utf8SequenceLength", "Value"],
                ["CompareTo", "Equals", "GetHashCode", "GetType", "ToString"]),
            ["Encoding"] = BuildInstanceItems(
                ["BodyName", "CodePage", "EncodingName", "HeaderName", "IsSingleByte", "Preamble", "WebName"],
                [
                    "Equals", "GetByteCount", "GetBytes", "GetCharCount", "GetChars", "GetMaxByteCount",
                    "GetMaxCharCount", "GetPreamble", "GetString", "GetType",
                ]),
            ["Regex"] = BuildInstanceItems(
                ["Options", "RightToLeft"],
                [
                    "Count", "GetGroupNames", "GetGroupNumbers", "GetType", "GroupNameFromNumber",
                    "GroupNumberFromName", "IsMatch", "Match", "Matches", "Replace", "Split", "ToString",
                ]),
            ["Match"] = BuildInstanceItems(
                ["Groups", "Index", "Length", "Name", "Success", "Value"],
                ["GetType", "NextMatch", "Result", "ToString"]),
            ["Group"] = BuildInstanceItems(
                ["Captures", "Index", "Length", "Name", "Success", "Value"],
                ["GetType", "ToString"]),
            ["Capture"] = BuildInstanceItems(["Index", "Length", "Value"], ["GetType", "ToString"]),
            ["MethodInfo"] = BuildInstanceItems(
                ["DeclaringType", "IsGenericMethod", "IsPublic", "IsStatic", "MemberType", "Name", "ReturnType"],
                ["CreateDelegate", "GetParameters", "GetType", "Invoke", "ToString"]),
            ["ConstructorInfo"] = BuildInstanceItems(
                ["DeclaringType", "IsPublic", "IsStatic", "MemberType", "Name"],
                ["GetParameters", "GetType", "Invoke", "ToString"]),
            ["PropertyInfo"] = BuildInstanceItems(
                ["CanRead", "CanWrite", "DeclaringType", "MemberType", "Name", "PropertyType"],
                ["GetGetMethod", "GetIndexParameters", "GetSetMethod", "GetType", "GetValue", "SetValue",
                    "ToString"]),
            ["FieldInfo"] = BuildInstanceItems(
                ["DeclaringType", "FieldType", "IsInitOnly", "IsLiteral", "IsPublic", "IsStatic", "MemberType",
                    "Name"],
                ["GetType", "GetValue", "SetValue", "ToString"]),
            ["ParameterInfo"] = BuildInstanceItems(
                ["HasDefaultValue", "IsOptional", "Name", "ParameterType", "Position"],
                ["GetType", "ToString"]),
        };

        // The materialized regex collections carry Count as a property and otherwise answer the whole sequence
        // surface.
        foreach (var collection in (string[])["MatchCollection", "GroupCollection", "CaptureCollection"])
        {
            tables[collection] =
            [
                .. SequenceInstanceMembers
                    .Concat([new CompletionItem("Count", CompletionItemKind.Member, "property")])
                    .GroupBy(static item => item.Text, StringComparer.Ordinal)
                    .Select(static group => group.First())
                    .OrderBy(static item => item.Text, StringComparer.OrdinalIgnoreCase),
            ];
        }

        // Booleans and every boxed numeric answer only the universal members.
        foreach (var scalar in (string[])
        [
            "Boolean", "SByte", "Byte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "IntPtr",
            "UIntPtr", "Int128", "UInt128", "BigInteger", "Single", "Double", "Decimal",
        ])
        {
            tables[scalar] = ScalarInstanceMembers;
        }

        return tables.ToImmutableDictionary(StringComparer.Ordinal);
    }

    private static ImmutableArray<CompletionItem> BuildInstanceItems(
        ReadOnlySpan<string> properties,
        ReadOnlySpan<string> methods)
    {
        var items = ImmutableArray.CreateBuilder<CompletionItem>(properties.Length + methods.Length);
        foreach (var property in properties)
        {
            items.Add(new CompletionItem(property, CompletionItemKind.Member, "property"));
        }

        foreach (var method in methods)
        {
            items.Add(new CompletionItem(method, CompletionItemKind.Member, "method"));
        }

        items.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Text, right.Text));
        return items.MoveToImmutable();
    }

    /// <summary>
    /// Maps a stored immediate-variable type name to the instance members the constant evaluator dispatches for
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

        if (typeName is "Action" or "Delegate"
            || typeName.StartsWith("Func<", StringComparison.Ordinal)
            || typeName.StartsWith("Action<", StringComparison.Ordinal)
            || typeName.StartsWith("Predicate<", StringComparison.Ordinal)
            || typeName.StartsWith("Comparison<", StringComparison.Ordinal))
        {
            return DelegateInstanceMembers;
        }

        if (InstanceReceiverMembers.TryGetValue(typeName, out var members))
        {
            return members;
        }

        // A dotted name is a stored enum's full type name; the value reads back as its underlying numeric.
        return typeName.Contains('.', StringComparison.Ordinal) ? ScalarInstanceMembers : [];
    }

    // An enum value dispatches exactly these instance members.
    private static readonly ImmutableArray<CompletionItem> EnumValueInstanceMembers = BuildInstanceItems(
        [],
        ["CompareTo", "GetType", "HasFlag", "ToString"]);

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
            ["int.MaxValue"] = "Int32",
            ["int.MinValue"] = "Int32",
            ["uint.MaxValue"] = "UInt32",
            ["uint.MinValue"] = "UInt32",
            ["long.MaxValue"] = "Int64",
            ["long.MinValue"] = "Int64",
            ["ulong.MaxValue"] = "UInt64",
            ["ulong.MinValue"] = "UInt64",
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
            ["double.Epsilon"] = "Double",
            ["double.MaxValue"] = "Double",
            ["double.MinValue"] = "Double",
            ["double.NaN"] = "Double",
            ["double.NegativeInfinity"] = "Double",
            ["double.Pi"] = "Double",
            ["double.PositiveInfinity"] = "Double",
            ["double.Tau"] = "Double",
            ["float.Epsilon"] = "Single",
            ["float.MaxValue"] = "Single",
            ["float.MinValue"] = "Single",
            ["float.NaN"] = "Single",
            ["float.NegativeInfinity"] = "Single",
            ["float.PositiveInfinity"] = "Single",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static ImmutableArray<CompletionItem> StaticMemberInstanceSurface(string receiver, string member)
    {
        if (EnumReceivers.Contains(receiver)
            && ReceiverMembers.TryGetValue(receiver, out var enumMembers)
            && enumMembers.Contains(member, StringComparer.Ordinal))
        {
            return EnumValueInstanceMembers;
        }

        return StaticMemberResultTypes.TryGetValue($"{receiver}.{member}", out var resultType)
            ? InstanceMembersForStoredType(resultType)
            : [];
    }

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
            var segments = ReadReceiverSegments(text, prefixStart - 1);
            return segments.Length == 0
                ? CompletionResult.Empty
                : CompleteMembers(catalog, context, segments, prefix, prefixStart, caret);
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
        ImmutableArray<string> segments,
        string prefix,
        int prefixStart,
        int caret)
    {
        var replaceLength = caret - prefixStart;

        // A member chain rooted at the adopted root walks declared field types: after 'root.A.' the candidates
        // are the instance fields of A's declared type, realized from the dump on demand — the same field set a
        // chain hop evaluates. An unknown hop offers nothing rather than guessing.
        if (catalog.HasRoot
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

        if (segments is [var single])
        {
            // A declared immediate variable completes the instance members the constant evaluator dispatches
            // for its stored value's type; a variable with no modeled instance surface offers nothing.
            if (context.Locals.FirstOrDefault(
                local => string.Equals(local.Text, single, StringComparison.Ordinal)) is { } localReceiver)
            {
                return Finish(
                    Score(InstanceMembersForStoredType(localReceiver.Detail), prefix),
                    prefixStart,
                    replaceLength);
            }

            if (ReceiverMembers.TryGetValue(single, out var members))
            {
                return Finish(
                    Score(members.Select(static member => new CompletionItem(member, CompletionItemKind.Member)),
                        prefix),
                    prefixStart,
                    replaceLength);
            }
        }

        // A static member with a modeled folded value completes that value's instance surface: 'Guid.Empty.',
        // 'Encoding.UTF8.', 'TimeSpan.Zero.', an enum member, or a numeric bound.
        if (segments is [var staticReceiver, var staticMember]
            && StaticMemberInstanceSurface(staticReceiver, staticMember) is { IsDefaultOrEmpty: false } surface)
        {
            return Finish(Score(surface, prefix), prefixStart, replaceLength);
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

    private static ImmutableArray<string> ReadReceiverSegments(string text, int dotOffset)
    {
        var segments = new List<string>();
        var end = dotOffset;
        while (end > 0)
        {
            var start = end;
            while (start > 0 && IsIdentifierChar(text[start - 1]))
            {
                start--;
            }

            if (start == end)
            {
                return [];
            }

            segments.Insert(0, text[start..end]);
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
