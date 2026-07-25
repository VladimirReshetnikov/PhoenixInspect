using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PhoenixInspect.Explorer.Wpf.Models;
using PhoenixInspect.Explorer.Wpf.ViewModels;

namespace PhoenixInspect.Explorer.Wpf;

/// <summary>The shell window hosting every demonstration panel.</summary>
/// <remarks>
/// Code-behind is limited to gestures that WPF cannot express as a plain binding: tree selection and expansion,
/// keyboard submission, and applying a template chip. All state and all adapter access stay in the view models.
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainViewModel model = new();

    /// <summary>Creates the shell window and installs its view model.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        Closed += (_, _) => model.Dispose();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (Application.Current is App { StartupDumpPath: { } path })
        {
            _ = model.OpenDumpAsync(path);
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedFile(e) is { } path)
        {
            e.Handled = true;
            _ = model.OpenDumpAsync(path);
        }
    }

    private static string? TryGetDroppedFile(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files
            ? files[0]
            : null;

    private void OnDismissErrorClick(object sender, RoutedEventArgs e) => model.DismissError();

    private void OnStackSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        model.CallStacks.SelectedNode = e.NewValue;

    private void OnThreadExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: CallStackThreadNode node })
        {
            _ = model.CallStacks.EnsureFramesLoadedAsync(node);
        }
    }

    private void OnExpressionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        var command = model.Evaluate.EvaluateCommand;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnObjectSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        var command = model.HeapObjects.SearchCommand;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnSampleClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpressionSample sample })
        {
            model.Evaluate.ApplySample(sample);
            ExpressionBox.Focus();
            ExpressionBox.CaretIndex = ExpressionBox.Text.Length;
        }
    }
}
