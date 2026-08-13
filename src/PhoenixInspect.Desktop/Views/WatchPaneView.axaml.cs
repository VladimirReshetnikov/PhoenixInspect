using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Desktop.ViewModels;

namespace PhoenixInspect.Desktop.Views;

/// <summary>
/// The Watch tool pane: editable expressions with in-place commit, and the shared completion drop-down.
/// </summary>
public partial class WatchPaneView : UserControl
{
    private CompletionController? controller;

    /// <summary>Creates the view.</summary>
    public WatchPaneView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (Tool is { } tool)
            {
                controller = new CompletionController(CompletionPopup, CompletionList, tool.Panel.Completion);
            }
        };
    }

    private WatchTool? Tool => DataContext as WatchTool;

    private void OnExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: WatchEntry entry } || Tool is not { } tool)
        {
            return;
        }

        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control) && sender is TextBox box)
        {
            // Ctrl+Space asks for completion explicitly, IDE-style: even an empty entry offers the universe.
            controller?.Invoke(box);
            e.Handled = true;
            return;
        }

        if (controller?.HandleKey(e) == true)
        {
            e.Handled = true;
            return;
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
            controller?.Update(box);
        }
    }

    private void OnExpressionLostFocus(object? sender, RoutedEventArgs e)
    {
        controller?.Close();
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
}
