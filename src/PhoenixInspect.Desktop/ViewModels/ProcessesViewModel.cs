using System.Collections.Immutable;
using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// Presents the running .NET processes a live-attach session could inspect, the way Visual Studio's Attach to
/// Process window does — but as a dockable pane that stays open, so the list can be filtered, refreshed, and
/// attached from repeatedly without a modal interruption.
/// </summary>
/// <remarks>
/// Which processes are managed, and why, is decided by <see cref="ProcessDiscoveryService"/> in the shared API;
/// this pane only renders the candidates and asks the shell to attach to the selected one.
/// </remarks>
public sealed class ProcessesViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand attachCommand;
    private ImmutableArray<ProcessCandidate> all = [];
    private string filter = string.Empty;
    private string processIdText = string.Empty;
    private ProcessCandidate? selected;
    private string summary = "Refresh to list running .NET processes.";

    /// <summary>Creates the processes pane.</summary>
    /// <param name="shell">The shell services used to perform the attach.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public ProcessesViewModel(IShellServices shell)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        refreshCommand = new RelayCommand(Refresh);
        attachCommand = new RelayCommand(AttachToSelected, () => TargetProcessId is not null);
    }

    /// <summary>
    /// Gets the process id the Attach action would use: the typed id when one is entered — a PID from a log or a
    /// script is often what a user has — otherwise the selected row's, when it is attachable.
    /// </summary>
    public int? TargetProcessId
    {
        get
        {
            if (int.TryParse(
                    processIdText.Trim(),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var typed) &&
                typed > 0)
            {
                return typed;
            }

            return selected is { IsAttachable: true } candidate ? candidate.ProcessId : null;
        }
    }

    /// <summary>Gets or sets an explicitly typed process id, which takes precedence over the selection.</summary>
    public string ProcessIdText
    {
        get => processIdText;
        set
        {
            if (Set(ref processIdText, value ?? string.Empty))
            {
                Raise(nameof(TargetProcessId));
                attachCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets the filtered candidate rows bound by the grid.</summary>
    public ObservableCollection<ProcessCandidate> View { get; } = [];

    /// <summary>Gets the command that re-enumerates running processes.</summary>
    public RelayCommand RefreshCommand => refreshCommand;

    /// <summary>Gets the command that attaches to the selected process.</summary>
    public RelayCommand AttachCommand => attachCommand;

    /// <summary>Gets a one-line description of the last enumeration.</summary>
    public string Summary
    {
        get => summary;
        private set => Set(ref summary, value);
    }

    /// <summary>Gets or sets a case-insensitive filter applied to the process name and id.</summary>
    public string Filter
    {
        get => filter;
        set
        {
            if (Set(ref filter, value))
            {
                RefreshView();
            }
        }
    }

    /// <summary>Gets or sets the selected process.</summary>
    public ProcessCandidate? Selected
    {
        get => selected;
        set
        {
            if (Set(ref selected, value))
            {
                Raise(nameof(TargetProcessId));
                attachCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Attaches to the typed process id, or to the selected process when none is typed.</summary>
    public void AttachToSelected()
    {
        if (TargetProcessId is { } processId)
        {
            _ = shell.AttachToProcessAsync(processId);
        }
    }

    /// <summary>Re-enumerates the running .NET processes.</summary>
    public void Refresh()
    {
        all = ProcessDiscoveryService.ListCandidates();
        var attachable = all.Count(static candidate => candidate.IsAttachable);
        Summary = all.Length == 0
            ? "No running .NET processes were detected."
            : $"{DisplayFormatting.Count(all.Length)} .NET processes detected, "
              + $"{DisplayFormatting.Count(attachable)} attachable from this inspector. "
              + "Attaching suspends the target for the session.";
        RefreshView();
    }

    private void RefreshView()
    {
        var previous = selected?.ProcessId;
        View.Clear();
        foreach (var candidate in all)
        {
            if (ProcessDiscoveryService.Matches(candidate, filter))
            {
                View.Add(candidate);
            }
        }

        Selected = previous is { } pid
            ? View.FirstOrDefault(candidate => candidate.ProcessId == pid)
            : null;
    }
}
