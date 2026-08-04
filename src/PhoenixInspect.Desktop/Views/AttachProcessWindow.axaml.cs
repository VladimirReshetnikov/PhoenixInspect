using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PhoenixInspect.Desktop.Views;

/// <summary>One row of the attach picker.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="Name">The process name.</param>
public sealed record ProcessRow(int Pid, string Name);

/// <summary>
/// The Attach to Process dialog: pick a running process from the list, or type a PID directly. The dialog only
/// selects; the shell view model performs the attach and reports its outcome.
/// </summary>
public partial class AttachProcessWindow : Window
{
    private ProcessRow[] allProcesses = [];

    /// <summary>Creates the dialog and loads the process list.</summary>
    public AttachProcessWindow()
    {
        InitializeComponent();
        LoadProcesses();
    }

    private void LoadProcesses()
    {
        try
        {
            allProcesses =
            [
                .. Process.GetProcesses()
                    .Select(static process =>
                    {
                        using (process)
                        {
                            return new ProcessRow(process.Id, process.ProcessName);
                        }
                    })
                    .Where(row => row.Pid != Environment.ProcessId)
                    .OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static row => row.Pid),
            ];
        }
        catch (Exception)
        {
            // Enumeration is a convenience; typing a PID still works when the listing is unavailable.
            allProcesses = [];
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = FilterBox.Text ?? string.Empty;
        ProcessList.ItemsSource = string.IsNullOrWhiteSpace(filter)
            ? allProcesses
            : allProcesses.Where(row => row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => LoadProcesses();

    private void OnFilterChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProcessList.SelectedItem is ProcessRow row)
        {
            PidBox.Text = row.Pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void OnPidChanged(object? sender, TextChangedEventArgs e) =>
        AttachButton.IsEnabled = TryReadPid() is not null;

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TryReadPid() is { } pid)
        {
            Close(pid);
        }
    }

    private void OnAttachClick(object? sender, RoutedEventArgs e)
    {
        if (TryReadPid() is { } pid)
        {
            Close(pid);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private int? TryReadPid() =>
        int.TryParse(PidBox.Text, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var pid) && pid > 0
            ? pid
            : null;
}
