using Avalonia.Controls;
using Avalonia.Input;
using PhoenixInspect.Desktop.Docking;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The Call Stack tool pane.</summary>
public partial class CallStackPaneView : UserControl
{
    /// <summary>Creates the view.</summary>
    public CallStackPaneView() => InitializeComponent();

    private CallStackTool? Tool => DataContext as CallStackTool;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Tool is { } tool)
        {
            tool.Panel.SelectedNode = StacksTree.SelectedItem;
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e) => Tool?.Panel.ActivateSelectedFrame();
}
