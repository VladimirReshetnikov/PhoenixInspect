using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace PhoenixInspect.Explorer.Wpf.Infrastructure;

/// <summary>Culture-independent display helpers shared by every demonstration panel.</summary>
/// <remarks>
/// Target-derived data is rendered verbatim only where the panel explicitly exists to show a value. Everywhere else
/// the helpers keep shape information (lengths, counts, addresses) so the UI does not accidentally imply that a
/// bounded prefix was a complete observation.
/// </remarks>
public static class DisplayFormatting
{
    /// <summary>The placeholder rendered wherever an optional fact is absent.</summary>
    public const string Absent = "—";

    /// <summary>Formats a target virtual address as a fixed-width hexadecimal literal.</summary>
    /// <param name="address">The address to format; zero renders as the absence placeholder.</param>
    /// <returns>A <c>0x</c>-prefixed uppercase address, or the absence placeholder for zero.</returns>
    public static string Address(ulong address) =>
        address == 0 ? Absent : "0x" + address.ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>Formats a metadata token as its canonical eight-digit hexadecimal spelling.</summary>
    /// <param name="token">The token value; zero renders as the absence placeholder.</param>
    /// <returns>A <c>0x</c>-prefixed uppercase token, or the absence placeholder for a nil token.</returns>
    public static string Token(int token) =>
        token == 0 ? Absent : "0x" + token.ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>Formats a byte count with a binary-prefix suffix for readability.</summary>
    /// <param name="bytes">The nonnegative byte count.</param>
    /// <returns>A compact size such as <c>512 B</c> or <c>3.4 GiB</c>.</returns>
    public static string ByteSize(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString("N0", CultureInfo.InvariantCulture) + " B";
        }

        string[] units = ["KiB", "MiB", "GiB", "TiB"];
        var scaled = (double)bytes;
        var unit = -1;
        while (scaled >= 1024 && unit < units.Length - 1)
        {
            scaled /= 1024;
            unit++;
        }

        return scaled.ToString(scaled >= 100 ? "N0" : "N1", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    /// <summary>Formats an integer with group separators.</summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The grouped invariant spelling.</returns>
    public static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Shortens a digest for chrome that cannot show all 64 characters.</summary>
    /// <param name="sha256">The complete lowercase digest, or null.</param>
    /// <returns>A leading and trailing fragment joined by an ellipsis, or the absence placeholder.</returns>
    public static string ShortDigest(string? sha256) =>
        string.IsNullOrEmpty(sha256) || sha256.Length <= 16
            ? sha256 ?? Absent
            : sha256[..8] + "…" + sha256[^8..];

    /// <summary>Renders a bounded hexadecimal preview of raw evidence bytes.</summary>
    /// <param name="bytes">The retained bytes, which may be empty for a failed read.</param>
    /// <param name="maximumBytes">The maximum number of bytes to spell out.</param>
    /// <returns>Space-separated byte pairs with an explicit truncation marker, or the absence placeholder.</returns>
    public static string HexPreview(ImmutableArray<byte> bytes, int maximumBytes = 16)
    {
        if (bytes.IsDefaultOrEmpty)
        {
            return Absent;
        }

        var shown = Math.Min(bytes.Length, maximumBytes);
        var builder = new StringBuilder((shown * 3) + 8);
        for (var index = 0; index < shown; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (bytes.Length > shown)
        {
            builder.Append(" … (+").Append(bytes.Length - shown).Append(')');
        }

        return builder.ToString();
    }

    /// <summary>Renders a string value with escaped control characters so the UI stays single-line and unambiguous.</summary>
    /// <param name="value">The exact or bounded-prefix string observed from the target.</param>
    /// <returns>A quoted, escaped spelling of the value.</returns>
    public static string QuotedString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character) || char.IsSurrogate(character))
                    {
                        builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>Inserts spaces before interior capitals so enum names read as prose in the UI.</summary>
    /// <param name="name">The identifier-style name to humanize.</param>
    /// <returns>A spaced spelling, or the original text when nothing needed splitting.</returns>
    public static string Humanize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (index > 0 &&
                char.IsUpper(character) &&
                (!char.IsUpper(name[index - 1]) || (index + 1 < name.Length && char.IsLower(name[index + 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
