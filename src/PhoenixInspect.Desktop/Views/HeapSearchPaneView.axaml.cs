using Avalonia.Controls;
using Avalonia.Input;
using PhoenixInspect.Desktop.Docking;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The Heap Search tool pane.</summary>
public partial class HeapSearchPaneView : UserControl
{
    /// <summary>Creates the view.</summary>
    public HeapSearchPaneView() => InitializeComponent();

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is HeapSearchTool tool)
        {
            e.Handled = true;
            tool.Panel.SearchCommand.Execute(null);
        }
    }
}
