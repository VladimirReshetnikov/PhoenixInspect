using Avalonia.Media;
using AvaloniaEdit.Highlighting;

namespace PhoenixInspect.Desktop.Views;

/// <summary>
/// The shell's own C# syntax palette: the stock highlighting grammar restyled to harmonize with the paper-white
/// olive theme. Every keyword group shares one muted olive, literals sit in warm ochre and gray-teal, comments in
/// gray-sage italics — and nothing is saturated blue or red, and nothing is bold.
/// </summary>
internal static class PhoenixCSharpHighlighting
{
    private static readonly Lazy<IHighlightingDefinition> Cached = new(Create);

    /// <summary>All keyword groups share this one muted olive, so keywords read as one category.</summary>
    private const string KeywordOlive = "#7A8A4E";

    /// <summary>String and char literals: warm ochre, kin to the khaki chips of the shell.</summary>
    private const string LiteralOchre = "#A3814F";

    /// <summary>Numeric literals: desaturated teal-gray, quiet next to the olive keywords.</summary>
    private const string NumberTeal = "#6D8A83";

    /// <summary>Comments, documentation, and preprocessor lines: gray-sage that recedes behind the code.</summary>
    private const string CommentSage = "#97A08A";

    /// <summary>Punctuation: near-ink warm gray.</summary>
    private const string PunctuationGray = "#6F7568";

    /// <summary>Member invocations: a darker olive-gray, distinct from keywords without shouting.</summary>
    private const string MethodOliveGray = "#5E684D";

    /// <summary>Gets the restyled C# highlighting definition.</summary>
    internal static IHighlightingDefinition Definition => Cached.Value;

    private static IHighlightingDefinition Create()
    {
        var definition = HighlightingManager.Instance.GetDefinition("C#");
        foreach (var color in definition.NamedHighlightingColors)
        {
            Restyle(color);
        }

        // Documentation comments delegate their tag colors to the shared XmlDoc grammar, which must recede the
        // same way the comment body does.
        if (HighlightingManager.Instance.GetDefinition("XmlDoc") is { } xmlDoc)
        {
            foreach (var color in xmlDoc.NamedHighlightingColors)
            {
                Restyle(color);
            }
        }

        return definition;
    }

    private static void Restyle(HighlightingColor color)
    {
        var (foreground, italic) = color.Name switch
        {
            "Comment" or "Documentation" or "DocComment" => (CommentSage, true),
            not null when color.Name.StartsWith("Xml", StringComparison.Ordinal) => (CommentSage, true),
            "KnownDocTags" => (CommentSage, true),
            "Preprocessor" => (CommentSage, false),
            "String" or "Char" or "StringInterpolation" => (LiteralOchre, false),
            "NumberLiteral" => (NumberTeal, false),
            "Punctuation" => (PunctuationGray, false),
            "MethodCall" => (MethodOliveGray, false),
            _ => (KeywordOlive, false),
        };
        color.Foreground = new SimpleHighlightingBrush(Color.Parse(foreground));
        color.FontWeight = FontWeight.Normal;
        color.FontStyle = italic ? FontStyle.Italic : FontStyle.Normal;
    }
}
