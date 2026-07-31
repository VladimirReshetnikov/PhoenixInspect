using System.Collections.Immutable;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Cli;

/// <summary>
/// Writes session output to the console with optional ANSI styling.
/// </summary>
/// <remarks>
/// <para>
/// The renderer decides how a fact is displayed, never what the fact is. Statuses, issues, counters, bounds, and
/// diagnostic codes arrive already projected by <c>PhoenixInspect.Inspection</c> and are printed verbatim, so a
/// colour never stands in for a result the product did not produce.
/// </para>
/// <para>
/// Styling is suppressed when output is redirected, when <c>NO_COLOR</c> is set, or when the caller asks for plain
/// text, because the console transcript is also a demo artifact that has to remain readable in a file.
/// </para>
/// </remarks>
public sealed class ConsoleRenderer
{
    private const char Escape = (char)0x1b;
    private static readonly string Reset = $"{Escape}[0m";
    private static readonly string Bold = $"{Escape}[1m";
    private static readonly string Dim = $"{Escape}[2m";
    private static readonly string Red = $"{Escape}[31m";
    private static readonly string Green = $"{Escape}[32m";
    private static readonly string Yellow = $"{Escape}[33m";
    private static readonly string Blue = $"{Escape}[34m";
    private static readonly string Magenta = $"{Escape}[35m";
    private static readonly string Cyan = $"{Escape}[36m";

    private readonly TextWriter output;
    private readonly bool styled;

    /// <summary>Creates a renderer over the supplied writer.</summary>
    /// <param name="output">The writer that receives every rendered line.</param>
    /// <param name="styled">Whether ANSI styling may be emitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
    public ConsoleRenderer(TextWriter output, bool styled)
    {
        ArgumentNullException.ThrowIfNull(output);
        this.output = output;
        this.styled = styled;
    }

    /// <summary>Writes one unstyled line.</summary>
    /// <param name="text">The line to write; an empty string writes a blank line.</param>
    public void Line(string text = "") => output.WriteLine(text);

    /// <summary>Writes a section heading followed by a rule.</summary>
    /// <param name="text">The heading text.</param>
    public void Heading(string text)
    {
        output.WriteLine();
        output.WriteLine(Style(text, Bold));
        output.WriteLine(Style(new string('─', Math.Min(text.Length, 78)), Dim));
    }

    /// <summary>Writes an informational line that is commentary rather than product evidence.</summary>
    /// <param name="text">The commentary text.</param>
    public void Note(string text) => output.WriteLine(Style(text, Dim));

    /// <summary>Writes a line describing a condition that stopped the requested operation.</summary>
    /// <param name="text">The error text.</param>
    public void Error(string text) => output.WriteLine(Style($"error: {text}", Red));

    /// <summary>Writes an aligned name and value pair.</summary>
    /// <param name="name">The label.</param>
    /// <param name="value">The value.</param>
    /// <param name="width">The label column width.</param>
    public void Pair(string name, string value, int width = 26) =>
        output.WriteLine($"  {Style(Pad(name, width), Dim)}{value}");

    /// <summary>Writes grouped property rows, emitting each group name once.</summary>
    /// <param name="rows">The rows to write, in projection order.</param>
    /// <param name="includeDetail">Whether each row's explanatory detail should be written under it.</param>
    public void Properties(ImmutableArray<PropertyRow> rows, bool includeDetail = false)
    {
        if (rows.IsDefaultOrEmpty)
        {
            return;
        }

        string? currentGroup = null;
        foreach (var row in rows)
        {
            if (!string.Equals(currentGroup, row.Group, StringComparison.Ordinal))
            {
                currentGroup = row.Group;
                output.WriteLine($"  {Style(currentGroup, Cyan)}");
            }

            output.WriteLine($"    {Style(Pad(row.Name, 28), Dim)}{row.Value}");
            if (includeDetail && !string.IsNullOrEmpty(row.Detail))
            {
                output.WriteLine($"    {Style(Wrap(row.Detail, 30), Dim)}");
            }
        }
    }

    /// <summary>Writes a table with a header row and dimmed column rule.</summary>
    /// <param name="headers">The column headings.</param>
    /// <param name="rows">The cell values, one array per row.</param>
    public void Table(string[] headers, IReadOnlyList<string[]> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        var widths = new int[headers.Length];
        for (var column = 0; column < headers.Length; column++)
        {
            widths[column] = headers[column].Length;
            foreach (var row in rows)
            {
                if (column < row.Length)
                {
                    widths[column] = Math.Max(widths[column], row[column].Length);
                }
            }
        }

        output.WriteLine("  " + Style(Join(headers, widths), Bold));
        output.WriteLine("  " + Style(Join([.. widths.Select(width => new string('─', width))], widths), Dim));
        foreach (var row in rows)
        {
            output.WriteLine("  " + Join(row, widths));
        }
    }

    /// <summary>Writes the headline of one evaluation: its severity marker, status, and value.</summary>
    /// <param name="report">The complete evaluation report.</param>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
    public void ResultHeadline(EvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var colour = SeverityColour(report.Severity);
        output.WriteLine();
        output.WriteLine($"  {Style(SeverityMarker(report.Severity), colour)} {Style(report.Expression, Bold)}");
        output.WriteLine(
            $"    {Style(Pad("=", 4), Dim)}{Style(report.Value, colour)}"
            + (report.ValueKind is null ? string.Empty : Style($"   [{report.ValueKind}]", Dim)));
        output.WriteLine($"    {Style(Pad("status", 4), Dim)}{report.Status}  {Style("·", Dim)}  {report.Stage}");
        output.WriteLine(
            $"    {Style(Pad("via", 4), Dim)}{report.Path}"
            + Style($"  ·  {report.Duration.TotalMilliseconds:F1} ms", Dim)
            + (report.Sha256 is null
                ? string.Empty
                : Style($"  ·  replay {DisplayFormatting.ShortDigest(report.Sha256)}", Dim)));

        foreach (var diagnostic in report.Diagnostics)
        {
            output.WriteLine($"    {Style(diagnostic.Code, colour)}  {diagnostic.Message}");
        }
    }

    /// <summary>Writes the complete evidence behind one evaluation.</summary>
    /// <param name="report">The complete evaluation report.</param>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
    public void ResultEvidence(EvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!report.Facts.IsDefaultOrEmpty)
        {
            Heading("Binding facts");
            Properties(report.Facts, includeDetail: true);
        }

        if (!report.MemoryReads.IsDefaultOrEmpty)
        {
            Heading("Memory reads");
            Table(
                ["address", "requested", "observed", "kind", "bytes"],
                [.. report.MemoryReads.Select(read =>
                    new[] { read.Address, read.Requested, read.Observed, read.Status, read.Preview })]);
        }

        if (!report.Bounds.IsDefaultOrEmpty)
        {
            Heading("Bounds actually reached");
            foreach (var bound in report.Bounds)
            {
                Pair(bound.Name, bound.Value, 44);
            }
        }
    }

    private static string SeverityMarker(EvaluationSeverity severity) => severity switch
    {
        EvaluationSeverity.Exact => "[exact]   ",
        EvaluationSeverity.Absent => "[absent]  ",
        EvaluationSeverity.Partial => "[partial] ",
        EvaluationSeverity.Rejected => "[rejected]",
        _ => "[stopped] ",
    };

    private static string SeverityColour(EvaluationSeverity severity) => severity switch
    {
        EvaluationSeverity.Exact => Green,
        EvaluationSeverity.Absent => Cyan,
        EvaluationSeverity.Partial => Yellow,
        EvaluationSeverity.Rejected => Magenta,
        _ => Red,
    };

    private static string Pad(string text, int width) =>
        text.Length >= width ? text + "  " : text.PadRight(width);

    private static string Join(string[] cells, int[] widths)
    {
        var parts = new string[widths.Length];
        for (var column = 0; column < widths.Length; column++)
        {
            var cell = column < cells.Length ? cells[column] : string.Empty;
            parts[column] = column == widths.Length - 1 ? cell : cell.PadRight(widths[column]);
        }

        return string.Join("  ", parts);
    }

    private static string Wrap(string text, int indent)
    {
        var margin = new string(' ', indent);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > 92 - indent)
            {
                lines.Add(current);
                current = word;
                continue;
            }

            current = current.Length == 0 ? word : $"{current} {word}";
        }

        if (current.Length != 0)
        {
            lines.Add(current);
        }

        return string.Join(Environment.NewLine + "    " + margin, lines);
    }

    private string Style(string text, string code) => styled ? code + text + Reset : text;

    /// <summary>Gets a bracketed colour marker for the supplied severity, used by headline summaries.</summary>
    /// <param name="severity">The presentation severity.</param>
    /// <returns>The styled marker text.</returns>
    public string Marker(EvaluationSeverity severity) => Style(SeverityMarker(severity), SeverityColour(severity));

    /// <summary>Writes the product banner.</summary>
    /// <param name="version">The informational version to display.</param>
    public void Banner(string version)
    {
        output.WriteLine(Style($"PhoenixInspect {version}", Bold));
        output.WriteLine(Style("Post-mortem .NET inspection — a dump presented as an inspectable session.", Dim));
        output.WriteLine(Style(
            "Read-only. Every answer is evidence from the snapshot, never a reconstruction of what the process did.",
            Dim));
    }

    /// <summary>Writes the interactive prompt without a trailing newline.</summary>
    /// <param name="context">A short description of the current session context.</param>
    public void Prompt(string context)
    {
        output.Write(Style(context, Blue));
        output.Write(Style("> ", Blue));
        output.Flush();
    }
}
