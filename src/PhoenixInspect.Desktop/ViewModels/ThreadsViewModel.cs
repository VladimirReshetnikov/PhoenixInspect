using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// Presents the probed managed threads the way Visual Studio's Threads window does: one row per thread, with the
/// selected thread's call stack shown by the separate Call Stack pane.
/// </summary>
/// <remarks>
/// The adapter selects one frame at a time by snapshot-scoped ordinal, so this pane probes ordinals rather than
/// enumerating a thread list. It states what the probe cannot distinguish instead of implying an exhaustive view.
/// </remarks>
public sealed class ThreadsViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly RelayCommand probeCommand;
    private int threadOrdinalsToProbe = 64;
    private string summary = "No dump is open.";
    private string note = string.Empty;
    private CallStackThreadNode? selectedThread;

    /// <summary>Creates the threads pane.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public ThreadsViewModel(IShellServices shell)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        probeCommand = new RelayCommand(() => _ = ProbeAsync(), () => this.shell.IsDumpOpen);
    }

    /// <summary>Gets the probed threads that carry at least one exact managed frame.</summary>
    public ObservableCollection<CallStackThreadNode> Threads { get; } = [];

    /// <summary>Gets the command that probes thread ordinals and reloads the retained threads.</summary>
    public RelayCommand ProbeCommand => probeCommand;

    /// <summary>Gets or sets how many zero-based thread ordinals the probe should request.</summary>
    public int ThreadOrdinalsToProbe
    {
        get => threadOrdinalsToProbe;
        set => Set(ref threadOrdinalsToProbe, Math.Clamp(value, 1, 1024));
    }

    /// <summary>Gets a one-line description of the last probe.</summary>
    public string Summary
    {
        get => summary;
        private set => Set(ref summary, value);
    }

    /// <summary>Gets an explanation of what the ordinal probe cannot distinguish.</summary>
    public string Note
    {
        get => note;
        private set => Set(ref note, value);
    }

    /// <summary>
    /// Gets or sets the selected thread. Selecting a thread loads its call stack into the Call Stack pane without
    /// stealing that pane's tab, so the thread list can be browsed frame-by-frame.
    /// </summary>
    public CallStackThreadNode? SelectedThread
    {
        get => selectedThread;
        set
        {
            if (Set(ref selectedThread, value) && value is not null)
            {
                _ = shell.ShowThreadCallStackAsync(value, activatePane: false);
            }
        }
    }

    /// <summary>
    /// Activates the selected thread the way Visual Studio's "Switch to Thread" does: its stack loads and the Call
    /// Stack pane comes to the front of its tab group.
    /// </summary>
    public void ActivateSelectedThread()
    {
        if (selectedThread is { } node)
        {
            _ = shell.ShowThreadCallStackAsync(node, activatePane: true);
        }
    }

    /// <summary>Clears every probed thread so the pane matches a newly opened or closed dump.</summary>
    public void Reset()
    {
        Threads.Clear();
        SelectedThread = null;
        Summary = shell.IsDumpOpen
            ? "Probe thread ordinals to list managed threads."
            : "No dump is open.";
        Note = string.Empty;
        probeCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Probes the configured thread ordinals, reloads the retained threads, and selects the first.</summary>
    /// <returns>A task that completes once the probe outcome is displayed.</returns>
    /// <remarks>
    /// The shell also invokes this once per opened dump so the stopped threads are visible without a manual probe,
    /// matching what a debugger user expects on attach. Re-running it from the pane stays supported.
    /// </remarks>
    public async Task ProbeAsync()
    {
        if (!shell.IsDumpOpen)
        {
            return;
        }

        var ordinals = ThreadOrdinalsToProbe;
        var projection = await shell.RunAsync(
            $"Probing {DisplayFormatting.Count(ordinals)} thread ordinals…",
            session => DumpInspectionService.ProbeCallStacks(session, ordinals)).ConfigureAwait(true);
        if (projection is null)
        {
            return;
        }

        Threads.Clear();
        selectedThread = null;
        Raise(nameof(SelectedThread));
        foreach (var thread in projection.Threads)
        {
            Threads.Add(thread);
        }

        Summary = projection.Summary;
        Note = projection.Note;
        shell.SetStatus(projection.Summary);

        // A debugger lands on a stopped thread rather than an empty stack; selecting the first probed thread loads
        // its call stack through the same path a user click would.
        SelectedThread = Threads.FirstOrDefault();
    }
}
