using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop;

/// <summary>
/// Shows method signatures while an argument list is being typed: the popup renders the modeled overloads with
/// the active parameter emphasized, aligned under the call's opening parenthesis. What counts as a modeled call
/// and which overloads exist come from the Inspection layer; this class only renders and closes.
/// </summary>
public sealed class SignatureHelpController
{
    private readonly Popup popup;
    private readonly StackPanel content;
    private readonly Func<CompletionContext>? contextProvider;
    private TextBox? measuredBox;
    private double measuredCharWidth;

    /// <summary>Creates the controller over one popup and its content panel.</summary>
    /// <param name="popup">The signature popup, placed beside the active editor.</param>
    /// <param name="content">The panel the signature lines render into.</param>
    /// <param name="contextProvider">The editor-specific completion context, or null for plain expressions.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public SignatureHelpController(
        Popup popup,
        StackPanel content,
        Func<CompletionContext>? contextProvider = null)
    {
        this.popup = popup ?? throw new ArgumentNullException(nameof(popup));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.contextProvider = contextProvider;
    }

    /// <summary>Gets whether the signature popup is currently open.</summary>
    public bool IsOpen => popup.IsOpen;

    /// <summary>Recomputes and shows (or hides) the signatures for one editor's current text and caret.</summary>
    /// <param name="box">The editor being typed into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is null.</exception>
    public void Update(TextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        var text = box.Text ?? string.Empty;
        var caret = box.CaretIndex;
        if (caret <= 0 && text.Length > 0)
        {
            caret = text.Length;
        }

        var help = ExpressionCompletionService.GetSignatureHelp(contextProvider?.Invoke(), text, caret);
        if (help is null)
        {
            Close();
            return;
        }

        Render(help);
        popup.PlacementTarget = box;
        popup.HorizontalOffset = box.Padding.Left + (help.OpenParenOffset * MeasureCharWidth(box));
        popup.IsOpen = true;
    }

    /// <summary>Recomputes after the caret moves without a text change, once the move has been applied.</summary>
    /// <param name="box">The editor being typed into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is null.</exception>
    public void ScheduleUpdate(TextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        Dispatcher.UIThread.Post(() => Update(box));
    }

    /// <summary>Handles one key press while the popup may be open; only Escape is consumed.</summary>
    /// <param name="e">The key event.</param>
    /// <returns>Whether the key was consumed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is null.</exception>
    public bool HandleKey(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (popup.IsOpen && e.Key == Key.Escape)
        {
            Close();
            return true;
        }

        return false;
    }

    /// <summary>Closes the signature popup.</summary>
    public void Close()
    {
        popup.IsOpen = false;
        content.Children.Clear();
    }

    private void Render(SignatureHelp help)
    {
        content.Children.Clear();

        // Overloads that still have a slot for the active parameter sort first, so the emphasized line is the
        // one the typed arguments can still match.
        var ordered = help.Signatures
            .OrderBy(signature => signature.Parameters.Length <= help.ActiveParameter ? 1 : 0)
            .ThenBy(static signature => signature.Parameters.Length);
        foreach (var signature in ordered)
        {
            var line = new TextBlock
            {
                FontSize = 11,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
            };
            line.Inlines ??= [];
            line.Inlines.Add(new Run($"{signature.ReturnTypeText} ")
            {
                Foreground = new SolidColorBrush(Color.Parse("#8AA0B4")),
            });
            line.Inlines.Add(new Run($"{help.ReceiverDisplay}.{signature.MethodName}("));
            for (var index = 0; index < signature.Parameters.Length; index++)
            {
                if (index > 0)
                {
                    line.Inlines.Add(new Run(", "));
                }

                var parameter = signature.Parameters[index];
                var run = new Run(string.Create(
                    CultureInfo.InvariantCulture, $"{parameter.TypeText} {parameter.Name}"));
                if (index == help.ActiveParameter)
                {
                    run.FontWeight = FontWeight.Bold;
                }

                line.Inlines.Add(run);
            }

            line.Inlines.Add(new Run(")"));
            content.Children.Add(line);
        }
    }

    private double MeasureCharWidth(TextBox box)
    {
        if (!ReferenceEquals(measuredBox, box))
        {
            var formatted = new FormattedText(
                new string('M', 16),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(box.FontFamily, box.FontStyle, box.FontWeight),
                box.FontSize,
                Brushes.Black);
            measuredCharWidth = formatted.Width / 16;
            measuredBox = box;
        }

        return measuredCharWidth;
    }
}
