using Avalonia.Controls;
using Avalonia.Input;
using PhoenixInspect.Desktop.Docking;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The Processes tool pane: attachable .NET processes, attached from selection.</summary>
public partial class ProcessesPaneView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ProcessesPaneView() => InitializeComponent();

    private void OnDoubleTapped(object? sender, TappedEventArgs e) =>
        (DataContext as ProcessesTool)?.Panel.AttachToSelected();
}
