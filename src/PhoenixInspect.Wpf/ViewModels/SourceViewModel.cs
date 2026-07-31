using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Wpf.ViewModels;

/// <summary>
/// Presents the verified source view of one selected frame: the mapped file with its span highlighted when the
/// on-disk bytes reproduce the PDB checksum, and an explained typed limit otherwise.
/// </summary>
public sealed class SourceViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private SourceViewResult? result;

    /// <summary>Creates the source panel.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public SourceViewModel(IShellServices shell) =>
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

    /// <summary>Gets the rendered source lines; empty unless the content was checksum-verified.</summary>
    public ObservableCollection<SourceLineRow> Lines { get; } = [];

    /// <summary>Gets the resolution and verification facts of the displayed result.</summary>
    public ObservableCollection<PropertyRow> Facts { get; } = [];

    /// <summary>Gets the last resolution result, or null before the first attempt.</summary>
    public SourceViewResult? Result
    {
        get => result;
        private set
        {
            if (!Set(ref result, value))
            {
                return;
            }

            Raise(nameof(HasResult));
            Raise(nameof(HasContent));
            Raise(nameof(Title));
            Raise(nameof(Summary));
            Raise(nameof(DocumentPath));
            Raise(nameof(IsVerified));
        }
    }

    /// <summary>Gets whether any resolution has been attempted since the panel was last reset.</summary>
    public bool HasResult => result is not null;

    /// <summary>Gets whether checksum-verified content is currently displayed.</summary>
    public bool HasContent => result is { Verification: SourceContentVerification.VerifiedExact };

    /// <summary>Gets the tool-window header text.</summary>
    public string Title => result?.Title ?? "Source";

    /// <summary>Gets the plain-language statement of the current outcome.</summary>
    public string Summary => result?.Summary
        ?? "Double-click an exact frame in the call stack to resolve and verify its source.";

    /// <summary>Gets the build-recorded document path of the displayed result, or an empty string.</summary>
    public string DocumentPath => result?.DocumentPath ?? string.Empty;

    /// <summary>Gets whether the displayed content is checksum-verified, for the verification badge.</summary>
    public bool IsVerified => HasContent;

    /// <summary>Gets the first highlighted line row, used to scroll the mapped span into view.</summary>
    public SourceLineRow? HighlightedLine => Lines.FirstOrDefault(static line => line.IsHighlighted);

    /// <summary>Clears the panel so it matches a newly opened or closed dump.</summary>
    public void Reset()
    {
        Result = null;
        Lines.Clear();
        Facts.Clear();
    }

    /// <summary>Resolves and displays the source of one frame.</summary>
    /// <param name="frame">The frame to resolve.</param>
    /// <param name="explicitCandidates">Caller-supplied Portable-PDB candidate paths, possibly empty.</param>
    /// <returns>The first highlighted line for the view to scroll to, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
    public async Task<SourceLineRow?> ShowFrameAsync(
        CallStackFrameNode frame,
        System.Collections.Immutable.ImmutableArray<string> explicitCandidates)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var produced = await shell.RunAsync(
            "Resolving and verifying the frame's source…",
            session => SourceNavigationService.ResolveFrameSource(session, frame, explicitCandidates))
            .ConfigureAwait(true);
        if (produced is null)
        {
            return null;
        }

        Lines.Clear();
        Facts.Clear();
        foreach (var line in produced.Lines)
        {
            Lines.Add(line);
        }

        foreach (var fact in produced.Facts)
        {
            Facts.Add(fact);
        }

        Result = produced;
        shell.SetStatus(produced.Summary);
        return HighlightedLine;
    }
}
