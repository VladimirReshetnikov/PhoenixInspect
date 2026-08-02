using Avalonia.Controls;
using Avalonia.Input;
using PhoenixInspect.Desktop.Docking;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The Call Stack tool pane.</summary>
public partial class CallStackPaneView : UserControl
{
    /// <summary>Creates the view.</summary>
    public CallStackPaneView() => InitializeComponent();

    private void OnDoubleTapped(object? sender, TappedEventArgs e) =>
        (DataContext as CallStackTool)?.Panel.ActivateSelectedFrame();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter activates the selected frame, matching the double-click and Visual Studio's Call Stack window.
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            (DataContext as CallStackTool)?.Panel.ActivateSelectedFrame();
        }
    }
}
