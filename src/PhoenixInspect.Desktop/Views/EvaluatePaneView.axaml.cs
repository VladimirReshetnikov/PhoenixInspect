using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Desktop.ViewModels;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The evaluation console pane.</summary>
public partial class EvaluatePaneView : UserControl
{
    /// <summary>Creates the view.</summary>
    public EvaluatePaneView() => InitializeComponent();

    private EvaluateTool? Tool => DataContext as EvaluateTool;

    private void OnExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Tool is { } tool)
        {
            e.Handled = true;
            tool.Panel.EvaluateCommand.Execute(null);
        }
    }

    private void OnSampleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ExpressionSample sample } && Tool is { } tool)
        {
            tool.Panel.ApplySample(sample);
            ExpressionBox.Focus();
            ExpressionBox.CaretIndex = ExpressionBox.Text?.Length ?? 0;
        }
    }
}
