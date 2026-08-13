using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop;

/// <summary>
/// The one piece of completion the presentation layer owns: wiring a drop-down popup and list to a
/// <see cref="CompletionSessionState"/>. What completes, how items filter, and how acceptance rewrites the text
/// all come from the Inspection layer; this class only shows, navigates, and closes the widget.
/// </summary>
public sealed class CompletionController
{
    private const int PageSize = 8;

    private readonly Popup popup;
    private readonly ListBox list;
    private readonly CompletionSessionState completion;
    private readonly Func<CompletionContext>? contextProvider;
    private TextBox? owner;
    private TextBox? measuredBox;
    private double measuredCharWidth;
    private CompletionResult result = CompletionResult.Empty;

    /// <summary>Creates the controller over one popup and list pair.</summary>
    /// <param name="popup">The drop-down popup, placed under the active editor.</param>
    /// <param name="list">The item list inside the popup.</param>
    /// <param name="completion">The shared completion state that computes items.</param>
    /// <param name="contextProvider">The editor-specific completion context, or null for plain expressions.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public CompletionController(
        Popup popup,
        ListBox list,
        CompletionSessionState completion,
        Func<CompletionContext>? contextProvider = null)
    {
        this.popup = popup ?? throw new ArgumentNullException(nameof(popup));
        this.list = list ?? throw new ArgumentNullException(nameof(list));
        this.completion = completion ?? throw new ArgumentNullException(nameof(completion));
        this.contextProvider = contextProvider;
        completion.Changed += (_, _) => RefreshIfOpen();
        list.PointerReleased += (_, _) =>
        {
            if (list.SelectedItem is not null)
            {
                Accept();
            }
        };
    }

    /// <summary>Gets whether the drop-down is currently open.</summary>
    public bool IsOpen => popup.IsOpen;

    /// <summary>Opens the drop-down on the user's explicit ask (Ctrl+Space), completing even an empty token.</summary>
    /// <param name="box">The editor being typed into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is null.</exception>
    public void Invoke(TextBox box) => Update(box, explicitInvocation: true);

    /// <summary>Recomputes and shows (or hides) the drop-down for one editor's current text and caret.</summary>
    /// <param name="box">The editor being typed into.</param>
    /// <param name="explicitInvocation">Whether the user asked for completion, which completes an empty token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is null.</exception>
    public void Update(TextBox box, bool explicitInvocation = false)
    {
        ArgumentNullException.ThrowIfNull(box);
        var text = box.Text ?? string.Empty;
        var caret = box.CaretIndex;
        if (caret <= 0 && text.Length > 0)
        {
            // A programmatic SetValue (UI automation) leaves the caret at zero; complete at the end instead.
            caret = text.Length;
        }

        result = completion.Complete(text, caret, contextProvider?.Invoke(), explicitInvocation);
        owner = box;
        if (result.Items.IsDefaultOrEmpty)
        {
            Close();
            return;
        }

        list.ItemsSource = result.Items;
        list.SelectedIndex = 0;
        list.ScrollIntoView(result.Items[0]);
        popup.PlacementTarget = box;
        popup.HorizontalOffset = box.Padding.Left + (result.ReplaceStart * MeasureCharWidth(box));
        popup.IsOpen = true;
    }

    /// <summary>Handles one key press while the drop-down may be open.</summary>
    /// <param name="e">The key event.</param>
    /// <returns>Whether the key was consumed by the drop-down.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is null.</exception>
    public bool HandleKey(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!popup.IsOpen)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                return true;
            case Key.Up:
                MoveSelection(-1);
                return true;
            case Key.PageDown:
                MoveSelection(PageSize);
                return true;
            case Key.PageUp:
                MoveSelection(-PageSize);
                return true;
            case Key.Enter:
            case Key.Tab:
                if (SelectionIsExactTypedToken())
                {
                    // The selection adds nothing the user has not already typed; Enter falls through to the
                    // editor's own submit, so evaluating a fully typed name never costs an extra keystroke.
                    Close();
                    return e.Key == Key.Tab;
                }

                Accept();
                return true;
            case Key.Escape:
                Close();
                return true;
            case Key.OemPeriod or Key.Decimal when SelectionExtendsTypedToken():
                // Committing on '.' chains completions the IDE way: 'root.Cur' + '.' inserts 'CurrentBatch'
                // and the dot then opens its members. The key stays unconsumed so the dot itself still types.
                Accept();
                return false;
            case Key.Left or Key.Right or Key.Home or Key.End:
                // The caret is about to move without a text change; recompute for its new position afterwards,
                // so the list follows the token under the caret instead of going stale.
                Dispatcher.UIThread.Post(RefreshIfOpen);
                return false;
            default:
                return false;
        }
    }

    /// <summary>Closes the drop-down.</summary>
    public void Close()
    {
        popup.IsOpen = false;
        list.ItemsSource = null;
    }

    private void RefreshIfOpen()
    {
        if (popup.IsOpen && owner is { } box)
        {
            Update(box);
        }
    }

    private void MoveSelection(int delta)
    {
        var count = list.ItemCount;
        if (count == 0)
        {
            return;
        }

        list.SelectedIndex = Math.Clamp(list.SelectedIndex + delta, 0, count - 1);
        if (list.SelectedItem is { } item)
        {
            list.ScrollIntoView(item);
        }
    }

    private bool SelectionIsExactTypedToken() =>
        owner is { } box
        && list.SelectedItem is CompletionItem item
        && string.Equals(TypedToken(box), item.Text, StringComparison.Ordinal);

    private bool SelectionExtendsTypedToken()
    {
        // A dot only commits a selection that begins with the typed token, so a loose match — a substring or
        // camel-hump candidate — never hijacks a dot typed after a complete, deliberate name.
        if (owner is not { } box || list.SelectedItem is not CompletionItem item)
        {
            return false;
        }

        var token = TypedToken(box);
        return token.Length > 0 && item.Text.StartsWith(token, StringComparison.OrdinalIgnoreCase);
    }

    private string TypedToken(TextBox box)
    {
        var text = box.Text ?? string.Empty;
        var start = Math.Clamp(result.ReplaceStart, 0, text.Length);
        var length = Math.Clamp(result.ReplaceLength, 0, text.Length - start);
        return text.Substring(start, length);
    }

    private double MeasureCharWidth(TextBox box)
    {
        if (!ReferenceEquals(measuredBox, box))
        {
            // The expression editors use a monospace font, so one measured glyph places the drop-down under the
            // token being completed, the way the IDEs anchor their lists at the caret.
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

    private void Accept()
    {
        if (owner is not { } box || list.SelectedItem is not CompletionItem item)
        {
            Close();
            return;
        }

        var (newText, newCaret) = result.Apply(box.Text ?? string.Empty, item);
        box.Text = newText;
        box.CaretIndex = newCaret;
        Close();
        box.Focus();
    }
}
