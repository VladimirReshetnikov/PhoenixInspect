using System.Collections.Immutable;
using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// Presents the bounded managed call stack of one selected thread, the way Visual Studio's Call Stack window
/// presents the active thread: one row per frame with its signature, source location, module, and offsets.
/// </summary>
/// <remarks>
/// The thread whose stack is shown comes from the Threads pane; this pane never probes ordinals itself. Each frame
/// row carries the typed evidence behind its cells, so an unnamed method or an unresolved source stays an explained
/// limit rather than a blank.
/// </remarks>
public sealed class CallStacksViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand useContextCommand;
    private int framesPerThread = 64;
    private CallStackThreadNode? thread;
    private CallStackFrameNode? selectedFrame;
    private string caption = "No dump is open.";

    /// <summary>Creates the call-stack pane.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public CallStacksViewModel(IShellServices shell)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        refreshCommand = new RelayCommand(
            () =>
            {
                if (thread is { } current)
                {
                    _ = LoadThreadAsync(current);
                }
            },
            () => thread is not null && this.shell.IsDumpOpen);
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

    /// <summary>Gets the displayed thread's frames, from the top of the stack downward.</summary>
    public ObservableCollection<CallStackFrameNode> Frames { get; } = [];

    /// <summary>Gets the command that re-walks the displayed thread's stack with the current frame cap.</summary>
    public RelayCommand RefreshCommand => refreshCommand;

    /// <summary>Gets the command that adopts the selected frame as the evaluation name context.</summary>
    public RelayCommand UseAsContextCommand => useContextCommand;

    /// <summary>Gets or sets the maximum number of managed frames retained for the displayed thread.</summary>
    public int FramesPerThread
    {
        get => framesPerThread;
        set => Set(ref framesPerThread, Math.Clamp(value, 1, 1024));
    }

    /// <summary>Gets a one-line description of the thread whose stack is displayed.</summary>
    public string Caption
    {
        get => caption;
        private set => Set(ref caption, value);
    }

    /// <summary>
    /// Gets or sets the selected frame row. Selecting a frame decodes its parameters and locals into the Locals
    /// pane, matching how Visual Studio's Locals window follows the Call Stack selection.
    /// </summary>
    public CallStackFrameNode? SelectedFrame
    {
        get => selectedFrame;
        set
        {
            if (Set(ref selectedFrame, value))
            {
                useContextCommand.RaiseCanExecuteChanged();
                if (value is not null)
                {
                    _ = shell.ShowFrameVariablesAsync(value);
                }
            }
        }
    }

    /// <summary>Gets the thread whose stack is currently displayed, or null.</summary>
    public CallStackThreadNode? Thread => thread;

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

    /// <summary>Clears the pane so it matches a newly opened or closed dump.</summary>
    public void Reset()
    {
        thread = null;
        Frames.Clear();
        SelectedFrame = null;
        Caption = shell.IsDumpOpen
            ? "Select a thread in the Threads pane to walk its managed call stack."
            : "No dump is open.";
        refreshCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Walks one thread's bounded stack, resolving each frame's source location, and displays it.</summary>
    /// <param name="node">The probed thread whose stack should be shown.</param>
    /// <returns>A task that completes once the stack is displayed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    public async Task LoadThreadAsync(CallStackThreadNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        thread = node;
        Caption = node.Header;
        refreshCommand.RaiseCanExecuteChanged();

        var frameCap = FramesPerThread;
        var explicitCandidates = shell.ExplicitPortablePdbCandidates;
        var frames = await shell.RunAsync(
            $"Walking managed frames of thread #{node.ThreadOrdinal}…",
            session => DumpInspectionService.LoadFrames(
                session,
                node,
                frameCap,
                SourceNavigationService.AssemblePortablePdbCandidates(
                    session, explicitCandidates))).ConfigureAwait(true);
        if (frames.IsDefault || !ReferenceEquals(thread, node))
        {
            return;
        }

        Frames.Clear();
        SelectedFrame = null;
        foreach (var frame in frames)
        {
            Frames.Add(frame);
        }

        // A debugger lands on the top frame; selecting it here also populates the Locals pane through the same
        // path a user click would take.
        SelectedFrame = Frames.FirstOrDefault(static frame => frame.IsExact) ?? Frames.FirstOrDefault();
    }
}
