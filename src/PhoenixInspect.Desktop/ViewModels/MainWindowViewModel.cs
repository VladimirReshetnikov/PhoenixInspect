using System.ComponentModel;
using System.IO;
using Dock.Model.Controls;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// The shell view model: it owns the dump session, the docking layout, and the shared busy and status surfaces.
/// </summary>
/// <remarks>
/// The shell is the only component that talks to <see cref="DumpSessionHost"/>. Panes receive it as
/// <see cref="IShellServices"/> so every adapter call is serialized, timed, and reported the same way.
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject, IShellServices, IDisposable
{
    private readonly DumpSessionHost host = new();
    private readonly InspectionDockFactory factory;
    private readonly RelayCommand openCommand;
    private readonly RelayCommand closeCommand;
    private int busyDepth;
    private string busyMessage = string.Empty;
    private string statusMessage = "Ready. Open a dump to begin.";
    private string? errorMessage;
    private string? dumpFileName;
    private string? dumpDirectory;
    private string? snapshotDigest;
    private string targetSummary = string.Empty;

    /// <summary>Creates the shell, its panes, and the docking layout.</summary>
    public MainWindowViewModel()
    {
        Overview = new OverviewViewModel();
        Modules = new ModulesViewModel(this);
        CallStacks = new CallStacksViewModel(this);
        HeapObjects = new HeapObjectsViewModel(this);
        Evaluate = new EvaluateViewModel(this);

        factory = new InspectionDockFactory(
            new CallStackTool(CallStacks),
            new ModulesTool(Modules),
            new HeapSearchTool(HeapObjects),
            new EvaluateTool(Evaluate),
            new ResultTool(Evaluate),
            new WelcomeDocument(Overview));
        Layout = factory.CreateLayout();
        factory.InitLayout(Layout);

        openCommand = new RelayCommand(() => OpenDumpRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy);
        closeCommand = new RelayCommand(() => _ = CloseDumpAsync(), () => IsDumpOpen && !IsBusy);
        Evaluate.Reset();
        CallStacks.Reset();
        HeapObjects.Reset();
    }

    /// <summary>Raised when the user asks to open a dump; the view shows the platform file picker.</summary>
    /// <remarks>File dialogs need a top-level window, so the view owns the picker and calls back with a path.</remarks>
    public event EventHandler? OpenDumpRequested;

    /// <summary>Gets the docking layout rendered by the window's DockControl.</summary>
    public IRootDock Layout { get; }

    /// <summary>Gets the overview pane backing the welcome document.</summary>
    public OverviewViewModel Overview { get; }

    /// <summary>Gets the modules pane.</summary>
    public ModulesViewModel Modules { get; }

    /// <summary>Gets the call-stack pane.</summary>
    public CallStacksViewModel CallStacks { get; }

    /// <summary>Gets the heap-object pane.</summary>
    public HeapObjectsViewModel HeapObjects { get; }

    /// <summary>Gets the evaluation pane shared by the console and the evidence view.</summary>
    public EvaluateViewModel Evaluate { get; }

    /// <summary>Gets the command that prompts for and opens a dump file.</summary>
    public RelayCommand OpenDumpCommand => openCommand;

    /// <summary>Gets the command that closes the current dump session.</summary>
    public RelayCommand CloseDumpCommand => closeCommand;

    /// <inheritdoc />
    public bool IsDumpOpen => host.IsOpen;

    /// <summary>Gets whether at least one adapter operation is currently running.</summary>
    public bool IsBusy => busyDepth > 0;

    /// <summary>Gets the message shown by the busy overlay.</summary>
    public string BusyMessage
    {
        get => busyMessage;
        private set => Set(ref busyMessage, value);
    }

    /// <summary>Gets the status-bar message.</summary>
    public string StatusMessage
    {
        get => statusMessage;
        private set => Set(ref statusMessage, value);
    }

    /// <summary>Gets the current error banner text, or null when no error is displayed.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (Set(ref errorMessage, value))
            {
                Raise(nameof(HasError));
            }
        }
    }

    /// <summary>Gets whether an error banner is displayed.</summary>
    public bool HasError => errorMessage is not null;

    /// <summary>Gets the open dump's file name, or null when no dump is open.</summary>
    public string? DumpFileName
    {
        get => dumpFileName;
        private set => Set(ref dumpFileName, value);
    }

    /// <summary>Gets the open dump's containing directory, or null when no dump is open.</summary>
    public string? DumpDirectory
    {
        get => dumpDirectory;
        private set => Set(ref dumpDirectory, value);
    }

    /// <summary>Gets the shortened snapshot digest shown in the title chrome, or null when no dump is open.</summary>
    public string? SnapshotDigest
    {
        get => snapshotDigest;
        private set => Set(ref snapshotDigest, value);
    }

    /// <summary>Gets a short platform and architecture summary, or an empty string when no dump is open.</summary>
    public string TargetSummary
    {
        get => targetSummary;
        private set => Set(ref targetSummary, value);
    }

    /// <summary>Dismisses the error banner.</summary>
    public void DismissError() => ErrorMessage = null;

    /// <inheritdoc />
    public void SetStatus(string message) => StatusMessage = message ?? string.Empty;

    /// <inheritdoc />
    public void UseAsEvaluationContext(CallStackFrameNode frame)
    {
        Evaluate.AdoptContext(frame);
        SetStatus("Adopted a selected frame as the static-field name context.");
    }

    /// <inheritdoc />
    public void UseAsEvaluationRoot(HeapObjectRow row)
    {
        Evaluate.AdoptRoot(RootSelection.FromHandleObject(row));
        SetStatus("Adopted an exact heap object as the root-relative expression root.");
    }

    /// <summary>Resolves one frame's verified source and shows it as a document tab.</summary>
    /// <param name="frame">The frame to resolve.</param>
    /// <returns>A task that completes once the document is shown.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
    public async Task ShowFrameSourceAsync(CallStackFrameNode frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var explicitCandidates = EvaluateViewModel.ParseCandidates(Evaluate.PortablePdbCandidates);
        var produced = await RunAsync(
            "Resolving and verifying the frame's source…",
            session => SourceNavigationService.ResolveFrameSource(session, frame, explicitCandidates))
            .ConfigureAwait(true);
        if (produced is null)
        {
            return;
        }

        // One document per recorded file; unresolved outcomes share one reusable explanation tab so a run of
        // failed lookups does not open a tab per attempt.
        var id = produced.DocumentPath is { Length: > 0 } path
            ? "src:" + path.ToUpperInvariant()
            : "src:unresolved";
        if (factory.FindDocument(id) is { } existing)
        {
            existing.Update(produced);
            factory.ShowDocument(existing);
        }
        else
        {
            factory.ShowDocument(new SourceDocument(id, produced));
        }

        SetStatus(produced.Summary);
    }

    /// <inheritdoc />
    public async Task<TResult?> RunAsync<TResult>(string busyMessage, Func<ClrmdDumpSession, TResult> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!host.IsOpen)
        {
            return default;
        }

        EnterBusy(busyMessage);
        try
        {
            return await host.QueryAsync(work).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
            return default;
        }
        finally
        {
            ExitBusy();
        }
    }

    /// <summary>Opens a specific dump file and reloads every pane.</summary>
    /// <param name="dumpPath">The full path of the dump file to open.</param>
    /// <returns>A task that completes once the panes reflect the new session.</returns>
    /// <exception cref="ArgumentException"><paramref name="dumpPath"/> is null or blank.</exception>
    public async Task OpenDumpAsync(string dumpPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpPath);
        DismissError();
        EnterBusy($"Opening {Path.GetFileName(dumpPath)}…");
        DumpOpenOutcome outcome;
        try
        {
            outcome = await host.OpenAsync(dumpPath).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            outcome = new DumpOpenOutcome(false, null, null, exception.Message);
        }
        finally
        {
            ExitBusy();
        }

        if (!outcome.IsOpen)
        {
            ClearSessionState();
            ReportError(outcome.Message);
            return;
        }

        DumpFileName = Path.GetFileName(dumpPath);
        DumpDirectory = Path.GetDirectoryName(dumpPath);
        Raise(nameof(IsDumpOpen));
        closeCommand.RaiseCanExecuteChanged();

        CallStacks.Reset();
        HeapObjects.Reset();
        Evaluate.Reset();
        factory.CloseSourceDocuments();

        var length = host.DumpLength ?? 0;
        var snapshot = await RunAsync(
            "Reading snapshot facts…",
            session => DumpInspectionService.LoadSnapshot(session, dumpPath, length)).ConfigureAwait(true);
        if (snapshot is not null)
        {
            Overview.Load(snapshot.Properties);
            Modules.Load(snapshot.Modules);
            SnapshotDigest = DisplayFormatting.ShortDigest(snapshot.SnapshotSha256);
            TargetSummary = $"{snapshot.TargetPlatform} · {snapshot.TargetArchitecture}";
        }

        // A debugger surfaces the stopped threads without being asked; the probe stays re-runnable from the pane.
        await CallStacks.ProbeAsync().ConfigureAwait(true);
        SubscribeThreadExpansion();
        SetStatus(outcome.Message);
    }

    /// <summary>Closes the current session and clears every pane.</summary>
    /// <returns>A task that completes once the session is closed.</returns>
    public async Task CloseDumpAsync()
    {
        EnterBusy("Closing the dump session…");
        try
        {
            await host.CloseAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
        finally
        {
            ExitBusy();
        }

        ClearSessionState();
        SetStatus("Dump session closed.");
    }

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    private void SubscribeThreadExpansion()
    {
        // Expanding a thread node lazily walks its remaining frames. The node publishes expansion through
        // INotifyPropertyChanged, so no view code-behind is needed for the load trigger. A thread the projection
        // pre-expanded never raises that change, so its frames are walked eagerly here.
        foreach (var thread in CallStacks.Threads)
        {
            thread.PropertyChanged += OnThreadNodeChanged;
            if (thread.IsExpanded)
            {
                _ = CallStacks.EnsureFramesLoadedAsync(thread);
            }
        }
    }

    private void OnThreadNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is CallStackThreadNode { IsExpanded: true } node &&
            string.Equals(e.PropertyName, nameof(CallStackThreadNode.IsExpanded), StringComparison.Ordinal))
        {
            _ = CallStacks.EnsureFramesLoadedAsync(node);
        }
    }

    private void ClearSessionState()
    {
        DumpFileName = null;
        DumpDirectory = null;
        SnapshotDigest = null;
        TargetSummary = string.Empty;
        Overview.Load([]);
        Modules.Load([]);
        CallStacks.Reset();
        HeapObjects.Reset();
        Evaluate.Reset();
        factory.CloseSourceDocuments();
        Raise(nameof(IsDumpOpen));
        closeCommand.RaiseCanExecuteChanged();
    }

    private void ReportError(string message) =>
        ErrorMessage = string.IsNullOrWhiteSpace(message) ? "An unexpected adapter error occurred." : message;

    private void EnterBusy(string message)
    {
        BusyMessage = message;
        busyDepth++;
        if (busyDepth == 1)
        {
            Raise(nameof(IsBusy));
            openCommand.RaiseCanExecuteChanged();
            closeCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExitBusy()
    {
        busyDepth = Math.Max(0, busyDepth - 1);
        if (busyDepth == 0)
        {
            BusyMessage = string.Empty;
            Raise(nameof(IsBusy));
            openCommand.RaiseCanExecuteChanged();
            closeCommand.RaiseCanExecuteChanged();
        }
    }
}
