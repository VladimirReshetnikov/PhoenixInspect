using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>
/// Parses the immediate window's <c>using</c> directives — <c>using Namespace;</c>,
/// <c>using static Namespace.Type;</c>, and <c>using Alias = Namespace(.Type);</c> — and folds each into the
/// running <see cref="UsingDirectiveSet"/> so subsequent expressions may omit the imported prefixes.
/// </summary>
/// <remarks>
/// The directive is recognized before evaluation, exactly as <c>#r</c> is, and never evaluates a value. A trailing
/// semicolon is accepted but optional, matching how these read at a prompt. A malformed directive is a typed
/// message the caller renders, never an exception.
/// </remarks>
public static class UsingDirective
{
    /// <summary>Gets whether one submitted line is a using directive.</summary>
    /// <param name="line">The submitted line, already trimmed.</param>
    /// <returns><see langword="true"/> when the line begins the directive.</returns>
    public static bool IsDirective(string line) =>
        line is not null && (line.StartsWith("using ", StringComparison.Ordinal) || line == "using");

    /// <summary>Applies one using directive to the current directive set.</summary>
    /// <param name="line">The complete directive line.</param>
    /// <param name="current">The directives already in effect.</param>
    /// <param name="updated">The directives after the line, unchanged on failure.</param>
    /// <param name="message">A human-readable confirmation or the typed failure reason.</param>
    /// <returns><see langword="true"/> when a directive was applied.</returns>
    public static bool TryApply(
        string line,
        UsingDirectiveSet current,
        out UsingDirectiveSet updated,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(current);
        updated = current;

        var body = line.Length >= 5 ? line[5..].Trim() : string.Empty;
        body = body.TrimEnd(';').Trim();
        if (body.Length == 0)
        {
            message = "Usage: using Namespace; | using static Namespace.Type; | using Alias = Namespace.Type;";
            return false;
        }

        if (body.StartsWith("static ", StringComparison.Ordinal))
        {
            var typeName = body["static ".Length..].Trim();
            if (!IsDottedName(typeName))
            {
                message = $"'{typeName}' is not a valid type name for a static import.";
                return false;
            }

            updated = current.WithStaticImport(typeName);
            message = $"using static {typeName}";
            return true;
        }

        var equals = body.IndexOf('=');
        if (equals >= 0)
        {
            var alias = body[..equals].Trim();
            var target = body[(equals + 1)..].Trim();
            if (!IsIdentifier(alias))
            {
                message = $"'{alias}' is not a valid alias identifier.";
                return false;
            }

            if (!IsDottedName(target))
            {
                message = $"'{target}' is not a valid namespace or type for an alias.";
                return false;
            }

            updated = current.WithAlias(alias, target);
            message = $"using {alias} = {target}";
            return true;
        }

        if (!IsDottedName(body))
        {
            message = $"'{body}' is not a valid namespace name.";
            return false;
        }

        updated = current.WithImportedNamespace(body);
        message = $"using {body}";
        return true;
    }

    private static bool IsDottedName(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var part in text.Split('.'))
        {
            if (!IsIdentifier(part))
            {
                return false;
            }
        }

        return true;
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
