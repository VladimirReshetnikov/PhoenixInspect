using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.Converters;

/// <summary>Maps an <see cref="EvaluationSeverity"/> to the light-palette accent brush of the result chrome.</summary>
/// <remarks>
/// The palette deliberately distinguishes an exhaustively proven absence from a bounded partial answer: both are
/// legitimate outcomes, and conflating them would misrepresent the product's result axes. Error-shaped outcomes use
/// the peach family; exact evidence uses olive.
/// </remarks>
public sealed class SeverityToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Exact = new(Color.Parse("#84A05E"));
    private static readonly SolidColorBrush Absent = new(Color.Parse("#8AA0B4"));
    private static readonly SolidColorBrush Partial = new(Color.Parse("#CBA95B"));
    private static readonly SolidColorBrush Stopped = new(Color.Parse("#E3986F"));
    private static readonly SolidColorBrush Rejected = new(Color.Parse("#A796AE"));

    /// <summary>Gets the shared converter instance.</summary>
    public static SeverityToBrushConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EvaluationSeverity severity
            ? severity switch
            {
                EvaluationSeverity.Exact => Exact,
                EvaluationSeverity.Absent => Absent,
                EvaluationSeverity.Partial => Partial,
                EvaluationSeverity.Rejected => Rejected,
                _ => Stopped,
            }
            : Stopped;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the mapping is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Severity brushes are display-only.");
}

/// <summary>Maps an <see cref="EvaluationSeverity"/> to a short glyph shown beside the status text.</summary>
public sealed class SeverityToGlyphConverter : IValueConverter
{
    /// <summary>Gets the shared converter instance.</summary>
    public static SeverityToGlyphConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EvaluationSeverity severity
            ? severity switch
            {
                EvaluationSeverity.Exact => "✔",
                EvaluationSeverity.Absent => "∅",
                EvaluationSeverity.Partial => "◐",
                EvaluationSeverity.Rejected => "✖",
                _ => "！",
            }
            : "?";

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the mapping is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Severity glyphs are display-only.");
}
