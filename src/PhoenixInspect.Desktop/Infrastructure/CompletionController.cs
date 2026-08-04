using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop;

/// <summary>
/// The one piece of completion the presentation layer owns: wiring a drop-down popup and list to a
/// <see cref="CompletionSessionState"/>. What completes, how items filter, and how acceptance rewrites the text
/// all come from the Inspection layer; this class only shows, navigates, and closes the widget.
/// </summary>
public sealed class CompletionController
{
    private readonly Popup popup;
    private readonly ListBox list;
    private readonly CompletionSessionState completion;
    private TextBox? owner;
    private CompletionResult result = CompletionResult.Empty;

    /// <summary>Creates the controller over one popup and list pair.</summary>
    /// <param name="popup">The drop-down popup, placed under the active editor.</param>
    /// <param name="list">The item list inside the popup.</param>
    /// <param name="completion">The shared completion state that computes items.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CompletionController(Popup popup, ListBox list, CompletionSessionState completion)
    {
        this.popup = popup ?? throw new ArgumentNullException(nameof(popup));
        this.list = list ?? throw new ArgumentNullException(nameof(list));
        this.completion = completion ?? throw new ArgumentNullException(nameof(completion));
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

    /// <summary>Recomputes and shows (or hides) the drop-down for one editor's current text and caret.</summary>
    /// <param name="box">The editor being typed into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is null.</exception>
    public void Update(TextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        var text = box.Text ?? string.Empty;
        var caret = box.CaretIndex;
        if (caret <= 0 && text.Length > 0)
        {
            // A programmatic SetValue (UI automation) leaves the caret at zero; complete at the end instead.
            caret = text.Length;
        }

        result = completion.Complete(text, caret);
        owner = box;
        if (result.Items.IsDefaultOrEmpty)
        {
            Close();
            return;
        }

        list.ItemsSource = result.Items;
        list.SelectedIndex = 0;
        popup.PlacementTarget = box;
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
            case Key.Enter:
            case Key.Tab:
                Accept();
                return true;
            case Key.Escape:
                Close();
                return true;
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
