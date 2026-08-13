using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.Converters;

/// <summary>
/// Maps a <see cref="CompletionItemKind"/> to a one-letter badge, the way the IDE completion lists carry an icon
/// per item kind: K keyword, T type, N namespace, M member, F field, R root, L local.
/// </summary>
public sealed class CompletionKindToGlyphConverter : IValueConverter
{
    /// <summary>Gets the shared converter instance.</summary>
    public static CompletionKindToGlyphConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CompletionItemKind kind
            ? kind switch
            {
                CompletionItemKind.Keyword => "K",
                CompletionItemKind.Type => "T",
                CompletionItemKind.Namespace => "N",
                CompletionItemKind.Member => "M",
                CompletionItemKind.Field => "F",
                CompletionItemKind.Root => "R",
                CompletionItemKind.Local => "L",
                _ => "?",
            }
            : "?";

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the mapping is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Completion glyphs are display-only.");
}

/// <summary>Maps a <see cref="CompletionItemKind"/> to its badge's background brush, in the muted pane palette.</summary>
public sealed class CompletionKindToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Keyword = new(Color.Parse("#7A8BC4"));
    private static readonly SolidColorBrush Type = new(Color.Parse("#4E9C97"));
    private static readonly SolidColorBrush Namespace = new(Color.Parse("#8AA0B4"));
    private static readonly SolidColorBrush Member = new(Color.Parse("#A796AE"));
    private static readonly SolidColorBrush Field = new(Color.Parse("#6E87C8"));
    private static readonly SolidColorBrush Root = new(Color.Parse("#84A05E"));
    private static readonly SolidColorBrush Local = new(Color.Parse("#CBA95B"));

    /// <summary>Gets the shared converter instance.</summary>
    public static CompletionKindToBrushConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CompletionItemKind kind
            ? kind switch
            {
                CompletionItemKind.Keyword => Keyword,
                CompletionItemKind.Type => Type,
                CompletionItemKind.Namespace => Namespace,
                CompletionItemKind.Member => Member,
                CompletionItemKind.Field => Field,
                CompletionItemKind.Root => Root,
                CompletionItemKind.Local => Local,
                _ => Namespace,
            }
            : Namespace;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the mapping is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Completion brushes are display-only.");
}
