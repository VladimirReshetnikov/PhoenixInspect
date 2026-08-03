using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace PhoenixInspect.Desktop.Docking;

/// <summary>
/// Builds the shell's fixed docking layout, arranged the way Visual Studio arranges a debugging session: documents
/// in the center, Modules and Heap Search on the left, the evidence pane on the right, and a full-width bottom band
/// holding the evaluation console beside the Call Stack and Threads tab group.
/// </summary>
/// <remarks>
/// Layout persistence is deliberately out of scope for the preview — every launch starts from this one layout, and
/// the panes themselves are non-closable singletons, so there is no hidden state to restore.
/// </remarks>
public sealed class InspectionDockFactory : Factory
{
    private readonly CallStackTool callStack;
    private readonly ThreadsTool threads;
    private readonly LocalsTool locals;
    private readonly WatchTool watch;
    private readonly ModulesTool modules;
    private readonly HeapSearchTool heapSearch;
    private readonly EvaluateTool evaluate;
    private readonly ResultTool resultPane;
    private readonly WelcomeDocument welcome;

    /// <summary>Creates the factory over the singleton panes.</summary>
    /// <param name="callStack">The call-stack tool pane.</param>
    /// <param name="threads">The threads tool pane.</param>
    /// <param name="locals">The locals tool pane.</param>
    /// <param name="watch">The watch tool pane.</param>
    /// <param name="modules">The modules tool pane.</param>
    /// <param name="heapSearch">The heap-search tool pane.</param>
    /// <param name="evaluate">The evaluation-console tool pane.</param>
    /// <param name="resultPane">The evidence tool pane.</param>
    /// <param name="welcome">The non-closable start document.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public InspectionDockFactory(
        CallStackTool callStack,
        ThreadsTool threads,
        LocalsTool locals,
        WatchTool watch,
        ModulesTool modules,
        HeapSearchTool heapSearch,
        EvaluateTool evaluate,
        ResultTool resultPane,
        WelcomeDocument welcome)
    {
        this.callStack = callStack ?? throw new ArgumentNullException(nameof(callStack));
        this.threads = threads ?? throw new ArgumentNullException(nameof(threads));
        this.locals = locals ?? throw new ArgumentNullException(nameof(locals));
        this.watch = watch ?? throw new ArgumentNullException(nameof(watch));
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
        this.heapSearch = heapSearch ?? throw new ArgumentNullException(nameof(heapSearch));
        this.evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        this.resultPane = resultPane ?? throw new ArgumentNullException(nameof(resultPane));
        this.welcome = welcome ?? throw new ArgumentNullException(nameof(welcome));
    }

    /// <summary>Gets the document dock that hosts the welcome page and resolved source tabs.</summary>
    public IDocumentDock? Documents { get; private set; }

    /// <inheritdoc />
    public override IRootDock CreateLayout()
    {
        var documents = new DocumentDock
        {
            Id = "Documents",
            Title = "Documents",
            IsCollapsable = false,
            Proportion = 0.56,
            DockCapabilityPolicy = new DockCapabilityPolicy(),
        };
        Documents = documents;
        documents.VisibleDockables = CreateList<IDockable>(welcome);
        documents.ActiveDockable = welcome;

        var leftTools = new ToolDock
        {
            Id = "LeftTools",
            Proportion = 0.22,
            Alignment = Alignment.Left,
            VisibleDockables = CreateList<IDockable>(modules, heapSearch),
            ActiveDockable = modules,
            DockCapabilityPolicy = new DockCapabilityPolicy(),
        };

        var rightTools = new ToolDock
        {
            Id = "RightTools",
            Proportion = 0.22,
            Alignment = Alignment.Right,
            VisibleDockables = CreateList<IDockable>(resultPane),
            ActiveDockable = resultPane,
            DockCapabilityPolicy = new DockCapabilityPolicy(),
        };

        var upperBand = new ProportionalDock
        {
            Id = "UpperBand",
            Orientation = Orientation.Horizontal,
            Proportion = 0.63,
            VisibleDockables = CreateList<IDockable>(
                leftTools,
                new ProportionalDockSplitter { Id = "LeftSplitter" },
                documents,
                new ProportionalDockSplitter { Id = "RightSplitter" },
                rightTools),
            DockCapabilityPolicy = new DockCapabilityPolicy(),
        };

        // The bottom band mirrors Visual Studio's debugger tool-window band: the watch-style console on the left,
        // and the Call Stack / Threads tab group on the right, both spanning the full window width.
        var bottomBand = new ProportionalDock
        {
            Id = "BottomBand",
            Orientation = Orientation.Horizontal,
            Proportion = 0.37,
            VisibleDockables = CreateList<IDockable>(
                new ToolDock
                {
                    Id = "BottomLeftTools",
                    Proportion = 0.55,
                    Alignment = Alignment.Bottom,
                    // Watch and Locals tab with the console, mirroring Visual Studio's bottom-left group.
                    VisibleDockables = CreateList<IDockable>(evaluate, watch, locals),
                    ActiveDockable = evaluate,
                    DockCapabilityPolicy = new DockCapabilityPolicy(),
                },
                new ProportionalDockSplitter { Id = "BottomSplitter" },
                new ToolDock
                {
                    Id = "BottomRightTools",
                    Proportion = 0.45,
                    Alignment = Alignment.Bottom,
                    VisibleDockables = CreateList<IDockable>(callStack, threads),
                    ActiveDockable = callStack,
                    DockCapabilityPolicy = new DockCapabilityPolicy(),
                }),
            DockCapabilityPolicy = new DockCapabilityPolicy(),
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                upperBand,
                new ProportionalDockSplitter { Id = "BandSplitter" },
                bottomBand),
            DockCapabilityPolicy = new DockCapabilityPolicy(),
        };

        var root = CreateRootDock();
        root.Id = "Root";
        root.IsCollapsable = false;
        root.VisibleDockables = CreateList<IDockable>(mainLayout);
        root.DefaultDockable = mainLayout;
        root.ActiveDockable = mainLayout;
        root.DockCapabilityPolicy = new DockCapabilityPolicy();
        return root;
    }

    /// <inheritdoc />
    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>();
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            [callStack.Id] = () => callStack,
            [threads.Id] = () => threads,
            [locals.Id] = () => locals,
            [watch.Id] = () => watch,
            [modules.Id] = () => modules,
            [heapSearch.Id] = () => heapSearch,
            [evaluate.Id] = () => evaluate,
            [resultPane.Id] = () => resultPane,
            [welcome.Id] = () => welcome,
        };
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow(),
        };
        base.InitLayout(layout);
    }

    /// <summary>Brings the Call Stack pane to the front of its tab group.</summary>
    public void ActivateCallStackPane() => SetActiveDockable(callStack);

    /// <summary>Adds a source document to the document dock and activates it.</summary>
    /// <param name="document">The document to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public void ShowDocument(SourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Documents is not { } dock)
        {
            return;
        }

        if (dock.VisibleDockables?.Contains(document) != true)
        {
            AddDockable(dock, document);
        }

        SetActiveDockable(document);
        SetFocusedDockable(dock, document);
    }

    /// <summary>Finds an open source document by dock id.</summary>
    /// <param name="id">The dock id derived from the document path.</param>
    /// <returns>The open document, or null.</returns>
    public SourceDocument? FindDocument(string id) =>
        Documents?.VisibleDockables?.OfType<SourceDocument>()
            .FirstOrDefault(document => string.Equals(document.Id, id, StringComparison.Ordinal));

    /// <summary>Closes every open source document, keeping the welcome page.</summary>
    public void CloseSourceDocuments()
    {
        if (Documents?.VisibleDockables is not { } dockables)
        {
            return;
        }

        foreach (var document in dockables.OfType<SourceDocument>().ToArray())
        {
            RemoveDockable(document, collapse: false);
        }

        if (Documents is { } dock)
        {
            SetActiveDockable(welcome);
            SetFocusedDockable(dock, welcome);
        }
    }
}
