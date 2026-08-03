using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Desktop.ViewModels;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.Views;

/// <summary>
/// The Watch tool pane: editable expressions with in-place commit, and a completion drop-down that offers
/// keywords, modeled identifiers, and evidence-derived member names as you type, like Visual Studio.
/// </summary>
public partial class WatchPaneView : UserControl
{
    private TextBox? completionOwner;
    private CompletionResult completionResult = CompletionResult.Empty;
    private WatchViewModel? subscribedPanel;

    /// <summary>Creates the view.</summary>
    public WatchPaneView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribePanel();
    }

    private WatchTool? Tool => DataContext as WatchTool;

    private void SubscribePanel()
    {
        if (subscribedPanel is not null)
        {
            subscribedPanel.CompletionCatalogChanged -= OnCompletionCatalogChanged;
        }

        subscribedPanel = Tool?.Panel;
        if (subscribedPanel is not null)
        {
            subscribedPanel.CompletionCatalogChanged += OnCompletionCatalogChanged;
        }
    }

    private void OnCompletionCatalogChanged(object? sender, EventArgs e)
    {
        // Fresh catalog facts (root fields, realized type members) refine an already-open drop-down.
        if (CompletionPopup.IsOpen && completionOwner is { } owner)
        {
            UpdateCompletions(owner);
        }
    }

    private void OnExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: WatchEntry entry } || Tool is not { } tool)
        {
            return;
        }

        if (CompletionPopup.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    MoveCompletionSelection(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    MoveCompletionSelection(-1);
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Tab:
                    AcceptCompletion();
                    e.Handled = true;
                    return;
                case Key.Escape:
                    CloseCompletion();
                    e.Handled = true;
                    return;
                default:
                    break;
            }
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = tool.Panel.CommitAsync(entry);
        }
    }

    private void OnExpressionTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box)
        {
            UpdateCompletions(box);
        }
    }

    private void OnExpressionLostFocus(object? sender, RoutedEventArgs e)
    {
        CloseCompletion();
        if (sender is Control { DataContext: WatchEntry entry } && Tool is { } tool)
        {
            tool.Panel.CommitOnFocusLoss(entry);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: WatchEntry entry } && Tool is { } tool)
        {
            tool.Panel.Remove(entry);
        }
    }

    private void OnToggleExpand(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: WatchRow row } && Tool is { } tool)
        {
            tool.Panel.ToggleExpand(row);
        }
    }

    private void OnCompletionPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (CompletionList.SelectedItem is not null)
        {
            AcceptCompletion();
        }
    }

    private void UpdateCompletions(TextBox box)
    {
        if (Tool is not { } tool)
        {
            return;
        }

        var text = box.Text ?? string.Empty;
        var caret = box.CaretIndex;
        if (caret <= 0 && text.Length > 0)
        {
            // A programmatic SetValue (UI automation) leaves the caret at zero; complete at the end instead.
            caret = text.Length;
        }

        var result = tool.Panel.GetCompletions(text, caret);
        if (result.Items.IsDefaultOrEmpty)
        {
            CloseCompletion();
            completionResult = result;
            completionOwner = box;
            return;
        }

        completionResult = result;
        completionOwner = box;
        CompletionList.ItemsSource = result.Items;
        CompletionList.SelectedIndex = 0;
        CompletionPopup.PlacementTarget = box;
        CompletionPopup.IsOpen = true;
    }

    private void MoveCompletionSelection(int delta)
    {
        var count = CompletionList.ItemCount;
        if (count == 0)
        {
            return;
        }

        var next = Math.Clamp(CompletionList.SelectedIndex + delta, 0, count - 1);
        CompletionList.SelectedIndex = next;
        if (CompletionList.SelectedItem is { } item)
        {
            CompletionList.ScrollIntoView(item);
        }
    }

    private void AcceptCompletion()
    {
        if (completionOwner is not { } box || CompletionList.SelectedItem is not CompletionItem item)
        {
            CloseCompletion();
            return;
        }

        var text = box.Text ?? string.Empty;
        var start = Math.Clamp(completionResult.ReplaceStart, 0, text.Length);
        var end = Math.Clamp(start + completionResult.ReplaceLength, start, text.Length);
        box.Text = text[..start] + item.Text + text[end..];
        box.CaretIndex = start + item.Text.Length;
        CloseCompletion();
        box.Focus();
    }

    private void CloseCompletion()
    {
        CompletionPopup.IsOpen = false;
        CompletionList.ItemsSource = null;
    }
}
