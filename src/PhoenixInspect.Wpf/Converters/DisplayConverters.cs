using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Wpf.Converters;

/// <summary>Maps an <see cref="EvaluationSeverity"/> to the accent brush used by the result banner.</summary>
/// <remarks>
/// The palette deliberately distinguishes an exhaustively proven absence from a bounded partial answer: both are
/// legitimate outcomes, and conflating them would misrepresent the product's result axes.
/// </remarks>
public sealed class SeverityToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is EvaluationSeverity severity ? severity : EvaluationSeverity.Stopped;
        var resourceKey = key switch
        {
            EvaluationSeverity.Exact => "Accent.Exact",
            EvaluationSeverity.Absent => "Accent.Absent",
            EvaluationSeverity.Partial => "Accent.Partial",
            EvaluationSeverity.Rejected => "Accent.Rejected",
            _ => "Accent.Stopped",
        };

        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the mapping is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Severity brushes are display-only.");
}

/// <summary>Maps an <see cref="EvaluationSeverity"/> to a short glyph shown beside the status text.</summary>
public sealed class SeverityToGlyphConverter : IValueConverter
{
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

/// <summary>Collapses an element when the bound Boolean is <see langword="true"/>.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the mapping is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Visibility inversion is display-only.");
}

/// <summary>Collapses an element when the bound collection, count, or string is empty.</summary>
/// <remarks>
/// Pass <c>invert</c> as the converter parameter to show the element only while the source is empty, which is how
/// empty-state hints are rendered behind a grid or list. Collection-backed visibility should bind the collection's
/// <c>Count</c> property rather than the collection itself: a binding to the instance is never re-evaluated when
/// items are added or removed, while <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/> raises a
/// property change for <c>Count</c>.
/// </remarks>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            int count => count == 0,
            System.Collections.ICollection collection => collection.Count == 0,
            _ => false,
        };

        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
        {
            isEmpty = !isEmpty;
        }

        return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the mapping is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Emptiness mapping is display-only.");
}
