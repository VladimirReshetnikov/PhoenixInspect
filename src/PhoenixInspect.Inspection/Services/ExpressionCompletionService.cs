using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

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
public sealed record CompletionResult(
    ImmutableArray<CompletionItem> Items,
    int ReplaceStart,
    int ReplaceLength,
    string? PendingTypeMembers = null)
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
                var reader = provider.GetMetadataReader();
                foreach (var handle in reader.TypeDefinitions)
                {
                    if (typeNames.Count >= MaximumCatalogTypes)
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
                var reader = provider.GetMetadataReader();
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
            catch (BadImageFormatException)
            {
                // A malformed image contributes no members.
            }
        }

        return
        [
            .. members
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
                : CompleteMembers(catalog, segments, prefix, prefixStart, caret);
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
        return Finish(items, prefixStart, caret - prefixStart);
    }

    private static CompletionResult CompleteMembers(
        CompletionCatalog catalog,
        ImmutableArray<string> segments,
        string prefix,
        int prefixStart,
        int caret)
    {
        var replaceLength = caret - prefixStart;
        if (segments is [var single])
        {
            if (catalog.HasRoot && string.Equals(single, catalog.RootIdentifier, StringComparison.Ordinal))
            {
                return Finish(
                    Score(catalog.RootMembers, prefix),
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

        var dotted = string.Join('.', segments);
        if (catalog.TypeMembers.TryGetValue(dotted, out var realized))
        {
            return Finish(Score(realized, prefix), prefixStart, replaceLength);
        }

        var isKnownType = catalog.TypeFullNames.Contains(dotted, StringComparer.Ordinal);
        var namespacePrefix = dotted + ".";
        var items = new List<ScoredItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fullName in catalog.TypeFullNames)
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

        if (items.Count == 0 && isKnownType)
        {
            // The receiver is a realizable metadata type; the host fetches its members and re-queries.
            return new CompletionResult([], prefixStart, replaceLength, PendingTypeMembers: dotted);
        }

        return Finish(items, prefixStart, replaceLength);
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
    /// Ranks how well a candidate matches the typed token, IDE-style: a case-sensitive prefix beats a
    /// case-insensitive one, which beats a camel-hump match, which beats a plain substring. A candidate the user
    /// already typed in full is not offered, and the looser modes need two characters, so a single keystroke never
    /// floods the list.
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
            return null;
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
