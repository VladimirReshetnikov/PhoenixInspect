using Avalonia.Controls;
using Avalonia.Input;
using PhoenixInspect.Desktop.Docking;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The Threads tool pane.</summary>
public partial class ThreadsPaneView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ThreadsPaneView() => InitializeComponent();

    private void OnDoubleTapped(object? sender, TappedEventArgs e) =>
        (DataContext as ThreadsTool)?.Panel.ActivateSelectedThread();
}
