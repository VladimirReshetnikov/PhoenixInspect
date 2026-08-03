using Dock.Model.Mvvm.Controls;
using PhoenixInspect.Desktop.ViewModels;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.Docking;

/// <summary>The dockable Call Stack tool pane.</summary>
public sealed class CallStackTool : Tool
{
    /// <summary>Creates the pane over its view model.</summary>
    /// <param name="panel">The call-stack pane view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public CallStackTool(CallStacksViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "CallStack";
        Title = "Call Stack";
        CanClose = false;
    }

    /// <summary>Gets the pane view model the view binds to.</summary>
    public CallStacksViewModel Panel { get; }
}

/// <summary>The dockable Threads tool pane.</summary>
public sealed class ThreadsTool : Tool
{
    /// <summary>Creates the pane over its view model.</summary>
    /// <param name="panel">The threads pane view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public ThreadsTool(ThreadsViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "Threads";
        Title = "Threads";
        CanClose = false;
    }

    /// <summary>Gets the pane view model the view binds to.</summary>
    public ThreadsViewModel Panel { get; }
}

/// <summary>The dockable Watch tool pane.</summary>
public sealed class WatchTool : Tool
{
    /// <summary>Creates the pane over its view model.</summary>
    /// <param name="panel">The watch pane view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public WatchTool(WatchViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "Watch";
        Title = "Watch 1";
        CanClose = false;
    }

    /// <summary>Gets the pane view model the view binds to.</summary>
    public WatchViewModel Panel { get; }
}

/// <summary>The dockable Locals tool pane.</summary>
public sealed class LocalsTool : Tool
{
    /// <summary>Creates the pane over its view model.</summary>
    /// <param name="panel">The locals pane view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public LocalsTool(LocalsViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "Locals";
        Title = "Locals";
        CanClose = false;
    }

    /// <summary>Gets the pane view model the view binds to.</summary>
    public LocalsViewModel Panel { get; }
}

/// <summary>The dockable Modules tool pane.</summary>
public sealed class ModulesTool : Tool
{
    /// <summary>Creates the pane over its view model.</summary>
    /// <param name="panel">The modules pane view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public ModulesTool(ModulesViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "Modules";
        Title = "Modules";
        CanClose = false;
    }

    /// <summary>Gets the pane view model the view binds to.</summary>
    public ModulesViewModel Panel { get; }
}

/// <summary>The dockable Heap Search tool pane.</summary>
public sealed class HeapSearchTool : Tool
{
    /// <summary>Creates the pane over its view model.</summary>
    /// <param name="panel">The heap-object pane view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public HeapSearchTool(HeapObjectsViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "HeapSearch";
        Title = "Heap Search";
        CanClose = false;
    }

    /// <summary>Gets the pane view model the view binds to.</summary>
    public HeapObjectsViewModel Panel { get; }
}

/// <summary>The dockable evaluation-console tool pane.</summary>
public sealed class EvaluateTool : Tool
{
    /// <summary>Creates the pane over its view model.</summary>
    /// <param name="panel">The shared evaluation view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public EvaluateTool(EvaluateViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "Evaluate";
        Title = "Evaluate";
        CanClose = false;
    }

    /// <summary>Gets the pane view model the view binds to.</summary>
    public EvaluateViewModel Panel { get; }
}

/// <summary>The dockable evidence pane showing the selected answer's complete provenance.</summary>
public sealed class ResultTool : Tool
{
    /// <summary>Creates the pane over the shared evaluation view model.</summary>
    /// <param name="panel">The shared evaluation view model whose selected report is displayed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    public ResultTool(EvaluateViewModel panel)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Id = "Result";
        Title = "Result";
        CanClose = false;
    }

    /// <summary>Gets the shared evaluation view model the view binds to.</summary>
    public EvaluateViewModel Panel { get; }
}

/// <summary>The non-closable start document: snapshot facts and the honest statement of the supported surface.</summary>
public sealed class WelcomeDocument : Document
{
    /// <summary>Creates the document over the overview view model.</summary>
    /// <param name="overview">The snapshot overview view model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="overview"/> is null.</exception>
    public WelcomeDocument(OverviewViewModel overview)
    {
        Overview = overview ?? throw new ArgumentNullException(nameof(overview));
        Id = "Welcome";
        Title = "Session";
        CanClose = false;
    }

    /// <summary>Gets the overview view model the view binds to.</summary>
    public OverviewViewModel Overview { get; }
}

/// <summary>One resolved source view presented as a document tab.</summary>
/// <remarks>
/// The document carries a complete <see cref="SourceViewResult"/>: verified content renders in the editor with the
/// mapped span highlighted, while every non-verified outcome renders its plain-language explanation instead of
/// content. Re-resolving a frame into the same file updates this document in place rather than opening a twin.
/// </remarks>
public sealed class SourceDocument : Document
{
    private SourceViewResult result;
    private int revision;

    /// <summary>Creates the document from one resolution result.</summary>
    /// <param name="id">The stable dock id, derived from the document path or failure identity.</param>
    /// <param name="result">The resolution result to display.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SourceDocument(string id, SourceViewResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        this.result = result ?? throw new ArgumentNullException(nameof(result));
        Id = id;
        Title = ShortTitle(result);
        CanClose = true;
    }

    /// <summary>Gets the displayed resolution result.</summary>
    public SourceViewResult Result
    {
        get => result;
        private set => SetProperty(ref result, value);
    }

    /// <summary>Gets a counter the view observes to re-apply text, highlight, and scroll state.</summary>
    public int Revision
    {
        get => revision;
        private set => SetProperty(ref revision, value);
    }

    /// <summary>Replaces the displayed result, for a re-resolution into the same document.</summary>
    /// <param name="updated">The new resolution result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="updated"/> is null.</exception>
    public void Update(SourceViewResult updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        Result = updated;
        Title = ShortTitle(updated);
        Revision++;
    }

    private static string ShortTitle(SourceViewResult result)
    {
        if (result.DocumentPath is { Length: > 0 } path)
        {
            try
            {
                var name = System.IO.Path.GetFileName(path);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch (ArgumentException)
            {
                // Fall through to the result title.
            }
        }

        return result.Title;
    }
}
