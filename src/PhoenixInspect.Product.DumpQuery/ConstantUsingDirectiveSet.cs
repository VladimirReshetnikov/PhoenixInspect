using System.Collections.Immutable;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>
/// The active <c>using</c> directives of a constant-evaluation session: imported namespaces, statically imported
/// types, and name/type aliases. It expands a written short name into the ordered candidate fully qualified names
/// the metadata resolvers try, so a prefix-less name binds exactly as it would in source with the same imports.
/// </summary>
/// <remarks>
/// Expansion never resolves anything itself; it only produces candidates, and the caller resolves each through the
/// same session-module and referenced-assembly scan a fully qualified name uses. The written name is always the
/// first candidate, so a fully qualified name keeps winning and an import can only add reach, never redirect one.
/// An extern-alias-qualified name is not expanded at all, because its scope is already named explicitly.
/// </remarks>
public sealed class ConstantUsingDirectiveSet
{
    /// <summary>An empty directive set that expands every name to itself alone.</summary>
    public static readonly ConstantUsingDirectiveSet Empty = new([], [], ImmutableDictionary<string, string>.Empty);

    private readonly ImmutableArray<string> importedNamespaces;
    private readonly ImmutableArray<string> staticImportedTypes;
    private readonly ImmutableDictionary<string, string> aliases;

    private ConstantUsingDirectiveSet(
        ImmutableArray<string> importedNamespaces,
        ImmutableArray<string> staticImportedTypes,
        ImmutableDictionary<string, string> aliases)
    {
        this.importedNamespaces = importedNamespaces;
        this.staticImportedTypes = staticImportedTypes;
        this.aliases = aliases;
    }

    /// <summary>Gets whether this set carries no directives.</summary>
    public bool IsEmpty =>
        importedNamespaces.IsEmpty && staticImportedTypes.IsEmpty && aliases.IsEmpty;

    /// <summary>Gets the imported namespaces, in declaration order.</summary>
    public ImmutableArray<string> ImportedNamespaces => importedNamespaces;

    /// <summary>Gets the statically imported type full names, in declaration order.</summary>
    public ImmutableArray<string> StaticImportedTypes => staticImportedTypes;

    /// <summary>Gets the alias bindings, keyed by alias identifier.</summary>
    public IReadOnlyDictionary<string, string> Aliases => aliases;

    /// <summary>Adds one imported namespace: <c>using Namespace;</c>.</summary>
    /// <param name="namespaceName">The namespace whose members become prefix-less.</param>
    /// <returns>A new set with the namespace imported.</returns>
    public ConstantUsingDirectiveSet WithImportedNamespace(string namespaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        return importedNamespaces.Contains(namespaceName, StringComparer.Ordinal)
            ? this
            : new ConstantUsingDirectiveSet(
                importedNamespaces.Add(namespaceName),
                staticImportedTypes,
                aliases);
    }

    /// <summary>Adds one statically imported type: <c>using static Namespace.Type;</c>.</summary>
    /// <param name="typeFullName">The type whose static members become prefix-less.</param>
    /// <returns>A new set with the type statically imported.</returns>
    public ConstantUsingDirectiveSet WithStaticImport(string typeFullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeFullName);
        return staticImportedTypes.Contains(typeFullName, StringComparer.Ordinal)
            ? this
            : new ConstantUsingDirectiveSet(
                importedNamespaces,
                staticImportedTypes.Add(typeFullName),
                aliases);
    }

    /// <summary>Adds one alias: <c>using Alias = Namespace;</c> or <c>using Alias = Namespace.Type;</c>.</summary>
    /// <param name="alias">The alias identifier.</param>
    /// <param name="target">The namespace or type the alias stands for.</param>
    /// <returns>A new set with the alias bound; a repeated alias replaces its target.</returns>
    public ConstantUsingDirectiveSet WithAlias(string alias, string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        return new ConstantUsingDirectiveSet(
            importedNamespaces,
            staticImportedTypes,
            aliases.SetItem(alias, target));
    }

    /// <summary>Expands a type name into ordered candidate fully qualified type names.</summary>
    /// <param name="parts">The written type name split on dots.</param>
    /// <returns>The written name first, then alias-substituted and namespace-prefixed candidates.</returns>
    public ImmutableArray<string> ExpandTypeCandidates(ImmutableArray<string> parts)
    {
        var candidates = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string candidate)
        {
            if (seen.Add(candidate))
            {
                candidates.Add(candidate);
            }
        }

        Add(string.Join('.', parts));
        foreach (var substituted in AliasSubstitutions(parts))
        {
            Add(substituted);
        }

        foreach (var importedNamespace in importedNamespaces)
        {
            Add($"{importedNamespace}.{string.Join('.', parts)}");
        }

        return candidates.ToImmutable();
    }

    /// <summary>Expands a member name into ordered candidate fully qualified <c>Namespace.Type.Member</c> names.</summary>
    /// <param name="parts">The written member name split on dots.</param>
    /// <returns>
    /// The written name first, then alias-substituted, namespace-prefixed, and static-import-prefixed candidates.
    /// Static imports contribute only when the written name is a single member identifier, matching the language.
    /// </returns>
    public ImmutableArray<string> ExpandMemberCandidates(ImmutableArray<string> parts)
    {
        var candidates = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string candidate)
        {
            if (seen.Add(candidate))
            {
                candidates.Add(candidate);
            }
        }

        var written = string.Join('.', parts);
        Add(written);
        foreach (var substituted in AliasSubstitutions(parts))
        {
            Add(substituted);
        }

        foreach (var importedNamespace in importedNamespaces)
        {
            Add($"{importedNamespace}.{written}");
        }

        // 'using static T;' brings T's own static members into scope as bare names, so a single written identifier
        // may be one of them; a dotted member reference already names its declaring type.
        if (parts.Length == 1)
        {
            foreach (var staticType in staticImportedTypes)
            {
                Add($"{staticType}.{written}");
            }
        }

        return candidates.ToImmutable();
    }

    private IEnumerable<string> AliasSubstitutions(ImmutableArray<string> parts)
    {
        if (parts.Length >= 1 && aliases.TryGetValue(parts[0], out var target))
        {
            yield return parts.Length == 1 ? target : $"{target}.{string.Join('.', parts.Skip(1))}";
        }
    }
}
