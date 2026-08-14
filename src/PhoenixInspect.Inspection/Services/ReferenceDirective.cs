using System.Collections.Immutable;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>
/// Parses and applies the immediate window's <c>#r</c> directive: reference an assembly by path or from the local
/// NuGet cache, optionally binding an extern alias with <c>as</c>, so its literal fields, enums, and type names
/// participate in expression evaluation.
/// </summary>
/// <remarks>
/// The NuGet form resolves only from the machine's already-populated package cache; no network fetch or restore is
/// performed, because the immediate window is an offline evaluator. A resolution failure is a typed message the
/// caller renders, never an exception.
/// </remarks>
public static class ReferenceDirective
{
    /// <summary>Gets whether one submitted line is a reference directive.</summary>
    /// <param name="line">The submitted line, already trimmed.</param>
    /// <returns><see langword="true"/> when the line begins the directive.</returns>
    public static bool IsDirective(string line) =>
        line is not null &&
        (line.StartsWith("#r ", StringComparison.Ordinal) || line == "#r");

    /// <summary>Applies one reference directive to the current reference set.</summary>
    /// <param name="line">The complete directive line.</param>
    /// <param name="current">The references already in effect.</param>
    /// <param name="updated">The references after the directive, unchanged on failure.</param>
    /// <param name="message">A human-readable confirmation or the typed failure reason.</param>
    /// <returns><see langword="true"/> when a reference was added.</returns>
    public static bool TryApply(
        string line,
        ImmutableArray<ReferenceAssembly> current,
        out ImmutableArray<ReferenceAssembly> updated,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(line);
        updated = current.IsDefault ? [] : current;

        if (!TryParse(line, out var target, out var isNuGet, out var alias, out message))
        {
            return false;
        }

        var path = isNuGet ? ResolveFromNuGetCache(target, out message) : target;
        if (path is null)
        {
            return false;
        }

        var reference = ReferenceAssembly.TryLoad(path, alias, out var loadError);
        if (reference is null)
        {
            message = loadError!;
            return false;
        }

        updated = updated.Add(reference);
        message = alias is null
            ? $"Referenced {reference.DisplayName} ({reference.MetadataSha256[..12]}…)."
            : $"Referenced {reference.DisplayName} as {alias} ({reference.MetadataSha256[..12]}…).";
        return true;
    }

    private static bool TryParse(
        string line,
        out string target,
        out bool isNuGet,
        out string? alias,
        out string message)
    {
        target = string.Empty;
        isNuGet = false;
        alias = null;
        message = string.Empty;

        var body = line[2..].Trim();
        if (body.Length == 0 || body[0] != '"')
        {
            message = "Usage: #r \"path-or-nuget-spec\" [as alias]. The reference target must be a quoted string.";
            return false;
        }

        var closingQuote = body.IndexOf('"', 1);
        if (closingQuote < 0)
        {
            message = "The reference target is missing its closing quote.";
            return false;
        }

        var quoted = body[1..closingQuote].Trim();
        var remainder = body[(closingQuote + 1)..].Trim();
        if (remainder.Length > 0)
        {
            var tokens = remainder.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 2 || !string.Equals(tokens[0], "as", StringComparison.Ordinal))
            {
                message = "An alias is written as: #r \"target\" as alias.";
                return false;
            }

            if (!IsIdentifier(tokens[1]))
            {
                message = $"'{tokens[1]}' is not a valid alias identifier.";
                return false;
            }

            alias = tokens[1];
        }

        const string nuGetPrefix = "nuget:";
        if (quoted.StartsWith(nuGetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            isNuGet = true;
            target = quoted[nuGetPrefix.Length..].Trim();
            if (target.Length == 0)
            {
                message = "A NuGet reference needs a package id, optionally followed by ', version'.";
                return false;
            }

            return true;
        }

        target = quoted;
        return true;
    }

    /// <summary>Resolves one 'Id' or 'Id, Version' spec against the machine's local NuGet package cache only.</summary>
    private static string? ResolveFromNuGetCache(string spec, out string message)
    {
        message = string.Empty;
        var comma = spec.IndexOf(',');
        var id = (comma < 0 ? spec : spec[..comma]).Trim();
        var requestedVersion = comma < 0 ? null : spec[(comma + 1)..].Trim();
        if (id.Length == 0)
        {
            message = "A NuGet reference needs a package id.";
            return null;
        }

        var packageRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages",
            id.ToLowerInvariant());
        if (!Directory.Exists(packageRoot))
        {
            message = $"Package '{id}' is not in the local NuGet cache. This offline evaluator performs no restore.";
            return null;
        }

        var versionDirectory = SelectVersionDirectory(packageRoot, requestedVersion);
        if (versionDirectory is null)
        {
            message = requestedVersion is null
                ? $"Package '{id}' has no cached version."
                : $"Package '{id}' version '{requestedVersion}' is not in the local NuGet cache.";
            return null;
        }

        var libRoot = Path.Combine(versionDirectory, "lib");
        if (!Directory.Exists(libRoot))
        {
            message = $"Package '{id}' carries no lib assemblies in the cache.";
            return null;
        }

        // The offline evaluator reads metadata only, so any target framework's assembly serves; the newest by
        // directory name is chosen deterministically, and the id-named assembly within it is preferred.
        foreach (var frameworkDirectory in Directory
                     .EnumerateDirectories(libRoot)
                     .OrderByDescending(static directory => Path.GetFileName(directory), StringComparer.Ordinal))
        {
            var assemblies = Directory.GetFiles(frameworkDirectory, "*.dll");
            var preferred = assemblies.FirstOrDefault(candidate =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(candidate),
                    id,
                    StringComparison.OrdinalIgnoreCase))
                ?? assemblies.FirstOrDefault();
            if (preferred is not null)
            {
                return preferred;
            }
        }

        message = $"Package '{id}' carries no readable lib assembly in the cache.";
        return null;
    }

    private static string? SelectVersionDirectory(string packageRoot, string? requestedVersion)
    {
        if (requestedVersion is not null)
        {
            var exact = Path.Combine(packageRoot, requestedVersion);
            return Directory.Exists(exact) ? exact : null;
        }

        return Directory
            .EnumerateDirectories(packageRoot)
            .OrderByDescending(TryParseVersion)
            .ThenByDescending(static directory => Path.GetFileName(directory), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static Version TryParseVersion(string directory)
    {
        var name = Path.GetFileName(directory);
        var dash = name.IndexOf('-');
        var core = dash < 0 ? name : name[..dash];
        return Version.TryParse(core, out var version) ? version : new Version(0, 0);
    }

    private static bool IsIdentifier(string text)
    {
        if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_'))
        {
            return false;
        }

        foreach (var character in text)
        {
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
