using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PhoenixInspect.Desktop.ViewModels;

namespace PhoenixInspect.Desktop.Views;

/// <summary>The shell window hosting the docked inspection workspace.</summary>
/// <remarks>
/// Code-behind is limited to gestures that need the platform window: the file picker, drag-drop, and process exit.
/// All state and all adapter access stay in the view models.
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel model = new();

    /// <summary>Creates the shell window with no startup dump, for the runtime XAML loader.</summary>
    public MainWindow()
        : this(null)
    {
    }

    /// <summary>Creates the shell window and installs its view model.</summary>
    /// <param name="startupDumpPath">An optional dump path from the command line, opened once loaded.</param>
    public MainWindow(string? startupDumpPath)
    {
        InitializeComponent();
        DataContext = model;
        model.OpenDumpRequested += OnOpenDumpRequested;
        Closed += (_, _) => model.Dispose();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        if (startupDumpPath is { Length: > 0 })
        {
            Loaded += (_, _) => _ = model.OpenDumpAsync(startupDumpPath);
        }
    }

    private async void OnOpenDumpRequested(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a .NET process dump",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Dump files") { Patterns = ["*.dmp"] },
                FilePickerFileTypes.All,
            ],
        });
        if (files is [{ } file, ..] && file.TryGetLocalPath() is { Length: > 0 } path)
        {
            await model.OpenDumpAsync(path);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDroppedFile(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (TryGetDroppedFile(e) is { } path)
        {
            e.Handled = true;
            _ = model.OpenDumpAsync(path);
        }
    }

    private static string? TryGetDroppedFile(DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            return null;
        }

        var files = e.DataTransfer.TryGetFiles()?.ToArray();
        return files is [{ } single] ? single.TryGetLocalPath() : null;
    }

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e) => model.DismissError();

    private void OnRecentDumpClick(object? sender, RoutedEventArgs e)
    {
        // The click bubbles from a generated child item whose data context is the stored path; the parent
        // "Open recent" item itself has the shell as its context and must not trigger an open.
        if (e.Source is MenuItem { DataContext: string path })
        {
            e.Handled = true;
            _ = model.OpenDumpAsync(path);
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
