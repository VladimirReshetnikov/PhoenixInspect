using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Desktop.ViewModels;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The Watch tool pane: editable expressions with in-place commit.</summary>
public partial class WatchPaneView : UserControl
{
    /// <summary>Creates the view.</summary>
    public WatchPaneView() => InitializeComponent();

    private WatchTool? Tool => DataContext as WatchTool;

    private void OnExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not Control { DataContext: WatchEntry entry } || Tool is not { } tool)
        {
            return;
        }

        e.Handled = true;
        _ = tool.Panel.CommitAsync(entry);
    }

    private void OnExpressionLostFocus(object? sender, RoutedEventArgs e)
    {
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
