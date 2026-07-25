using PhoenixInspect.Explorer.Wpf.Infrastructure;

namespace PhoenixInspect.Explorer.Wpf.ViewModels;

/// <summary>One entry in the shell's left navigation rail.</summary>
public sealed class NavigationSection : ObservableObject
{
    private string badge = string.Empty;

    /// <summary>Creates a navigation entry.</summary>
    /// <param name="title">The section title shown in the rail.</param>
    /// <param name="glyph">A short pictographic glyph rendered beside the title.</param>
    /// <param name="caption">A one-line description of what the section shows.</param>
    public NavigationSection(string title, string glyph, string caption)
    {
        Title = title;
        Glyph = glyph;
        Caption = caption;
    }

    /// <summary>Gets the section title.</summary>
    public string Title { get; }

    /// <summary>Gets the pictographic glyph rendered beside the title.</summary>
    public string Glyph { get; }

    /// <summary>Gets the one-line description of the section.</summary>
    public string Caption { get; }

    /// <summary>Gets or sets a short count or state badge shown at the trailing edge of the entry.</summary>
    public string Badge
    {
        get => badge;
        set => Set(ref badge, value);
    }
}
