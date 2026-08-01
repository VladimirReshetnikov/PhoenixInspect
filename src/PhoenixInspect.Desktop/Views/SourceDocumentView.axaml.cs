using System.ComponentModel;
using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.Views;

/// <summary>
/// One resolved source document: a read-only, syntax-colored editor with the frame's mapped span highlighted, or
/// the plain-language explanation when content must not be shown.
/// </summary>
public partial class SourceDocumentView : UserControl
{
    private readonly LineSpanBackgroundRenderer highlighter = new();
    private SourceDocument? document;
    private int pendingScrollLine;

    /// <summary>Creates the view and configures the read-only editor.</summary>
    public SourceDocumentView()
    {
        InitializeComponent();
        Editor.SyntaxHighlighting = PhoenixCSharpHighlighting.Definition;
        Editor.TextArea.TextView.BackgroundRenderers.Add(highlighter);
        DataContextChanged += (_, _) => Attach(DataContext as SourceDocument);

        // Content is applied while the tab is still unrealized, when ScrollToLine is a no-op. The pending line is
        // therefore replayed once the editor has an actual layout.
        Editor.LayoutUpdated += (_, _) => FlushPendingScroll();
    }

    private void Attach(SourceDocument? next)
    {
        if (ReferenceEquals(document, next))
        {
            return;
        }

        if (document is not null)
        {
            document.PropertyChanged -= OnDocumentChanged;
        }

        document = next;
        if (document is not null)
        {
            document.PropertyChanged += OnDocumentChanged;
        }

        Apply();
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SourceDocument.Revision), StringComparison.Ordinal))
        {
            Apply();
        }
    }

    private void Apply()
    {
        var result = document?.Result;
        var verified = result is { Verification: SourceContentVerification.VerifiedExact };
        var decompiled = result is { Verification: SourceContentVerification.DecompiledFromValidatedAssembly };
        Editor.IsVisible = verified || decompiled;
        VerificationBadge.Text = verified
            ? "✔ checksum-verified"
            : decompiled
                ? "⚠ decompiled from IL"
                : string.Empty;
        if (result is null || !(verified || decompiled))
        {
            Editor.Text = string.Empty;
            highlighter.Clear();
            return;
        }

        var text = new StringBuilder();
        foreach (var line in result.Lines)
        {
            text.AppendLine(line.Text);
        }

        Editor.Text = text.ToString();
        highlighter.SetSpan(result.StartLine, result.EndLine);
        Editor.TextArea.TextView.Redraw();
        pendingScrollLine = Math.Max(1, result.StartLine);
        FlushPendingScroll();
    }

    private void FlushPendingScroll()
    {
        if (pendingScrollLine <= 0 || Editor.Bounds.Height <= 0 ||
            Editor.Document is not { } textDocument || textDocument.LineCount < pendingScrollLine)
        {
            return;
        }

        var line = pendingScrollLine;
        pendingScrollLine = 0;
        Editor.ScrollToLine(line);
    }
}

/// <summary>Paints the khaki highlight behind the frame's mapped line span.</summary>
public sealed class LineSpanBackgroundRenderer : IBackgroundRenderer
{
    private static readonly IBrush Highlight = new SolidColorBrush(Color.Parse("#66F1EBBB"));
    private int startLine;
    private int endLine;

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>Sets the one-based inclusive line span to highlight.</summary>
    /// <param name="start">The first highlighted line.</param>
    /// <param name="end">The last highlighted line.</param>
    public void SetSpan(int start, int end)
    {
        startLine = start;
        endLine = end;
    }

    /// <summary>Clears the highlight.</summary>
    public void Clear()
    {
        startLine = 0;
        endLine = 0;
    }

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (startLine <= 0 || textView.Document is null)
        {
            return;
        }

        foreach (var visualLine in textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (lineNumber < startLine || lineNumber > endLine)
            {
                continue;
            }

            var top = visualLine.VisualTop - textView.VerticalOffset;
            drawingContext.FillRectangle(
                Highlight,
                new Avalonia.Rect(0, top, textView.Bounds.Width, visualLine.Height));
        }
    }
}
