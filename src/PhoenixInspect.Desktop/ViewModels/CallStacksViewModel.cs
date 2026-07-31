using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>Presents bounded managed call stacks and lets one frame become the evaluation name context.</summary>
/// <remarks>
/// The adapter selects one frame at a time by snapshot-scoped ordinal, so this pane probes ordinals rather than
/// enumerating a thread list. It states what the probe cannot distinguish instead of implying an exhaustive view.
/// </remarks>
public sealed class CallStacksViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly HashSet<int> loadedThreads = [];
    private readonly RelayCommand probeCommand;
    private readonly RelayCommand useContextCommand;
    private int threadOrdinalsToProbe = 64;
    private int framesPerThread = 64;
    private string summary = "No dump is open.";
    private string note = string.Empty;
    private object? selectedNode;

    /// <summary>Creates the call-stack pane.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public CallStacksViewModel(IShellServices shell)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        probeCommand = new RelayCommand(() => _ = ProbeAsync(), () => this.shell.IsDumpOpen);
        useContextCommand = new RelayCommand(
            () =>
            {
                if (SelectedFrame is { IsExact: true } frame)
                {
                    this.shell.UseAsEvaluationContext(frame);
                }
            },
            () => SelectedFrame is { IsExact: true });
    }

    /// <summary>Gets the probed threads that carry at least one exact managed frame.</summary>
    public ObservableCollection<CallStackThreadNode> Threads { get; } = [];

    /// <summary>Gets the command that probes thread ordinals and loads each thread's top frame.</summary>
    public RelayCommand ProbeCommand => probeCommand;

    /// <summary>Gets the command that adopts the selected frame as the evaluation name context.</summary>
    public RelayCommand UseAsContextCommand => useContextCommand;

    /// <summary>Gets or sets how many zero-based thread ordinals the probe should request.</summary>
    public int ThreadOrdinalsToProbe
    {
        get => threadOrdinalsToProbe;
        set => Set(ref threadOrdinalsToProbe, Math.Clamp(value, 1, 1024));
    }

    /// <summary>Gets or sets the maximum number of managed frames retained per thread.</summary>
    public int FramesPerThread
    {
        get => framesPerThread;
        set => Set(ref framesPerThread, Math.Clamp(value, 1, 1024));
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

    /// <summary>Gets or sets the tree selection, which may be a thread node or a frame node.</summary>
    public object? SelectedNode
    {
        get => selectedNode;
        set
        {
            if (!Set(ref selectedNode, value))
            {
                return;
            }

            Raise(nameof(SelectedFrame));
            useContextCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Gets the selected frame node, or null when the selection is not a frame.</summary>
    public CallStackFrameNode? SelectedFrame => selectedNode as CallStackFrameNode;

    /// <summary>
    /// Activates the selected exact frame the way a debugger call-stack window does: it becomes the name-binding
    /// context and its verified source opens as a document.
    /// </summary>
    public void ActivateSelectedFrame()
    {
        if (SelectedFrame is { IsExact: true } frame)
        {
            shell.UseAsEvaluationContext(frame);
            _ = shell.ShowFrameSourceAsync(frame);
        }
    }

    /// <summary>Clears every probed thread so the pane matches a newly opened or closed dump.</summary>
    public void Reset()
    {
        Threads.Clear();
        loadedThreads.Clear();
        SelectedNode = null;
        Summary = shell.IsDumpOpen
            ? "Probe thread ordinals to load managed call stacks."
            : "No dump is open.";
        Note = string.Empty;
        probeCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Loads the remaining frames of one thread the first time it is expanded.</summary>
    /// <param name="node">The expanded thread node.</param>
    /// <returns>A task that completes once the thread's frames are present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    public async Task EnsureFramesLoadedAsync(CallStackThreadNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!loadedThreads.Add(node.ThreadOrdinal))
        {
            return;
        }

        var frames = await shell.RunAsync(
            $"Walking managed frames of thread #{node.ThreadOrdinal}…",
            session => DumpInspectionService.LoadFrames(session, node, FramesPerThread)).ConfigureAwait(true);
        if (frames.IsDefault)
        {
            loadedThreads.Remove(node.ThreadOrdinal);
            return;
        }

        foreach (var frame in frames)
        {
            node.Frames.Add(frame);
        }
    }

    /// <summary>Probes the configured thread ordinals and reloads the retained threads.</summary>
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
        loadedThreads.Clear();
        SelectedNode = null;
        foreach (var thread in projection.Threads)
        {
            Threads.Add(thread);
        }

        Summary = projection.Summary;
        Note = projection.Note;
        shell.SetStatus(projection.Summary);
    }
}
