using System.Collections.Immutable;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Desktop.ViewModels;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>
/// Freezes panel-layout persistence: the docking arrangement captures as plain geometry facts, round-trips
/// through JSON, and rebuilds over the singleton panes — while any snapshot the rebuild cannot honor exactly
/// falls back to null so the shell starts from its default layout instead of a broken one.
/// </summary>
public sealed class DockLayoutPersistenceTests
{
    /// <summary>A view-model host with no session, so panes construct without a live shell.</summary>
    private sealed class ShellStub : ObservableObject, IShellServices, ICompletionQueryExecutor
    {
        public ShellStub() => Completion = new CompletionSessionState(this);

        public bool IsDumpOpen => false;

        public bool IsSessionOpen => false;

        public CompletionSessionState Completion { get; }

        public ImmutableArray<string> ExplicitPortablePdbCandidates => [];

        public Task<TResult?> RunAsync<TResult>(string busyMessage, Func<ClrmdDumpSession, TResult> work) =>
            Task.FromResult<TResult?>(default);

        public Task<TResult?> RunQuietAsync<TResult>(Func<ClrmdDumpSession, TResult> work) =>
            Task.FromResult<TResult?>(default);

        public void SetStatus(string message)
        {
        }

        public void UseAsEvaluationContext(CallStackFrameNode frame)
        {
        }

        public void UseAsEvaluationRoot(HeapObjectRow row)
        {
        }

        public Task ShowFrameSourceAsync(CallStackFrameNode frame) => Task.CompletedTask;

        public Task ShowThreadCallStackAsync(CallStackThreadNode thread, bool activatePane) =>
            Task.CompletedTask;

        public Task ShowFrameVariablesAsync(CallStackFrameNode frame) => Task.CompletedTask;

        public Task AttachToProcessAsync(int processId) => Task.CompletedTask;
    }

    private static InspectionDockFactory CreateFactory()
    {
        var shell = new ShellStub();
        var evaluate = new EvaluateViewModel(shell);
        return new InspectionDockFactory(
            new CallStackTool(new CallStacksViewModel(shell)),
            new ThreadsTool(new ThreadsViewModel(shell)),
            new LocalsTool(new LocalsViewModel(shell)),
            new WatchTool(new WatchViewModel(shell, evaluate)),
            new ImmediateTool(new ImmediateViewModel(shell, evaluate)),
            new ModulesTool(new ModulesViewModel(shell)),
            new ProcessesTool(new ProcessesViewModel(shell)),
            new HeapSearchTool(new HeapObjectsViewModel(shell)),
            new EvaluateTool(evaluate),
            new ResultTool(evaluate),
            new WelcomeDocument(new OverviewViewModel()));
    }

    /// <summary>The default layout captures, survives JSON, rebuilds, and re-captures identically.</summary>
    [Fact]
    public void Default_layout_round_trips_through_json()
    {
        var factory = CreateFactory();
        var layout = factory.CreateLayout();

        var captured = DockLayoutPersistence.Capture(layout);
        Assert.NotNull(captured);

        var json = DockLayoutPersistence.Serialize(captured!);
        var reloaded = DockLayoutPersistence.Deserialize(json);
        Assert.NotNull(reloaded);

        var rebuilt = factory.TryCreateLayout(reloaded);
        Assert.NotNull(rebuilt);
        Assert.NotNull(factory.Documents);

        var recaptured = DockLayoutPersistence.Capture(rebuilt!);
        Assert.NotNull(recaptured);
        Assert.Equal(json, DockLayoutPersistence.Serialize(recaptured!));
    }

    /// <summary>A rearranged snapshot — moved pane, different active tabs — rebuilds to the same geometry.</summary>
    [Fact]
    public void Rearranged_layouts_rebuild_exactly()
    {
        var factory = CreateFactory();
        var layout = factory.CreateLayout();
        var captured = DockLayoutPersistence.Capture(layout)!;

        // Simulate a user session: Locals moved to the bottom-right group.
        var moved = captured with
        {
            Root = MovePane(captured.Root!, "Locals", from: "BottomLeftTools", to: "BottomRightTools"),
        };
        Assert.NotEqual(DockLayoutPersistence.Serialize(captured), DockLayoutPersistence.Serialize(moved));

        var rebuilt = factory.TryCreateLayout(moved);
        Assert.NotNull(rebuilt);
        var recaptured = DockLayoutPersistence.Capture(rebuilt!);
        Assert.Equal(DockLayoutPersistence.Serialize(moved), DockLayoutPersistence.Serialize(recaptured!));
    }

    private static DockLayoutNode MovePane(DockLayoutNode node, string paneId, string from, string to)
    {
        if (node.Children is not { } children)
        {
            return node;
        }

        var rewritten = children.Select(child => MovePane(child, paneId, from, to)).ToList();
        if (string.Equals(node.Id, from, StringComparison.Ordinal))
        {
            rewritten.RemoveAll(child =>
                child.Kind == "Pane" && string.Equals(child.Id, paneId, StringComparison.Ordinal));
        }

        if (string.Equals(node.Id, to, StringComparison.Ordinal))
        {
            rewritten.Add(new DockLayoutNode { Kind = "Pane", Id = paneId });
        }

        return node with { Children = rewritten };
    }

    /// <summary>Snapshots the rebuild cannot honor exactly fall back to null, never to a broken layout.</summary>
    [Fact]
    public void Unhonorable_snapshots_fall_back_to_null()
    {
        var factory = CreateFactory();
        var captured = DockLayoutPersistence.Capture(factory.CreateLayout())!;

        // A version from a different schema.
        Assert.Null(factory.TryCreateLayout(captured with { Version = DockLayoutSnapshot.CurrentVersion + 1 }));

        // A pane id the shell does not declare.
        var unknown = captured with
        {
            Root = MovePane(captured.Root!, "NoSuchPane", from: "-", to: "BottomRightTools"),
        };
        Assert.Null(factory.TryCreateLayout(unknown));

        // The same pane placed twice.
        var duplicated = captured with
        {
            Root = MovePane(captured.Root!, "Locals", from: "-", to: "BottomRightTools"),
        };
        Assert.Null(factory.TryCreateLayout(duplicated));

        // No snapshot at all, and text that is not a snapshot.
        Assert.Null(factory.TryCreateLayout(null));
        Assert.Null(DockLayoutPersistence.Deserialize("not json"));
    }

    /// <summary>A pane the snapshot omits joins the first tool group instead of disappearing.</summary>
    [Fact]
    public void Omitted_panes_rejoin_the_layout()
    {
        var factory = CreateFactory();
        var captured = DockLayoutPersistence.Capture(factory.CreateLayout())!;
        var missingLocals = captured with
        {
            Root = MovePane(captured.Root!, "Locals", from: "BottomLeftTools", to: "-"),
        };

        var rebuilt = factory.TryCreateLayout(missingLocals);
        Assert.NotNull(rebuilt);
        var recaptured = DockLayoutPersistence.Capture(rebuilt!)!;
        var allPaneIds = CollectPaneIds(recaptured.Root!).ToList();
        Assert.Contains("Locals", allPaneIds);
        Assert.Equal(allPaneIds.Count, allPaneIds.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The shell's Restore-default-layout command — the one the View menu binds — swaps the layout and raises
    /// the change notification the DockControl needs to rebind, leaving the shell on the default geometry.
    /// </summary>
    [Fact]
    public void Restore_command_swaps_the_shell_layout_and_notifies()
    {
        using var model = new MainWindowViewModel();
        var before = model.Layout;
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        Assert.True(model.RestoreDefaultLayoutCommand.CanExecute(null));
        model.RestoreDefaultLayoutCommand.Execute(null);

        Assert.NotSame(before, model.Layout);
        Assert.Contains(nameof(MainWindowViewModel.Layout), changed);

        // The shell now holds exactly the default geometry.
        var restored = DockLayoutPersistence.Capture(model.Layout);
        Assert.NotNull(restored);
        var paneIds = CollectPaneIds(restored!.Root!).ToList();
        Assert.Equal(11, paneIds.Count);
        Assert.Equal(paneIds.Count, paneIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("UpperBand", CollectDockIds(restored.Root!));
        Assert.Contains("BottomBand", CollectDockIds(restored.Root!));
    }

    private static IEnumerable<string> CollectDockIds(DockLayoutNode node)
    {
        if (node.Kind != "Pane" && node.Id is { } id)
        {
            yield return id;
        }

        foreach (var child in node.Children ?? [])
        {
            foreach (var dockId in CollectDockIds(child))
            {
                yield return dockId;
            }
        }
    }

    private static IEnumerable<string> CollectPaneIds(DockLayoutNode node)
    {
        if (node.Kind == "Pane" && node.Id is { } id)
        {
            yield return id;
        }

        foreach (var child in node.Children ?? [])
        {
            foreach (var paneId in CollectPaneIds(child))
            {
                yield return paneId;
            }
        }
    }

    /// <summary>
    /// Restoring the default layout discards a rearranged one and returns the exact default geometry, while
    /// keeping the panes placed and the document dock live.
    /// </summary>
    [Fact]
    public void Restoring_the_default_layout_returns_the_default_geometry()
    {
        var factory = CreateFactory();
        var defaultLayout = factory.CreateLayout();
        var defaultJson = DockLayoutPersistence.Serialize(DockLayoutPersistence.Capture(defaultLayout)!);

        // A rearranged layout captures differently...
        var rearranged = factory.TryCreateLayout(
            DockLayoutPersistence.Capture(defaultLayout)! with
            {
                Root = MovePane(
                    DockLayoutPersistence.Capture(defaultLayout)!.Root!,
                    "Locals",
                    from: "BottomLeftTools",
                    to: "BottomRightTools"),
            });
        Assert.NotNull(rearranged);
        Assert.NotEqual(defaultJson, DockLayoutPersistence.Serialize(DockLayoutPersistence.Capture(rearranged!)!));

        // ...and rebuilding the default returns exactly the default geometry again.
        var restored = factory.CreateLayout();
        factory.InitLayout(restored);
        Assert.Equal(defaultJson, DockLayoutPersistence.Serialize(DockLayoutPersistence.Capture(restored)!));
        Assert.NotNull(factory.Documents);
    }

    /// <summary>The rebuilt document dock keeps hosting source documents through the factory.</summary>
    [Fact]
    public void Rebuilt_layouts_still_host_documents()
    {
        var factory = CreateFactory();
        var captured = DockLayoutPersistence.Capture(factory.CreateLayout())!;
        var rebuilt = factory.TryCreateLayout(captured);
        Assert.NotNull(rebuilt);
        factory.InitLayout(rebuilt!);

        var document = new SourceDocument("doc:test", new SourceViewResult
        {
            IsResolved = false,
            Title = "Test",
            Summary = "No content in this test.",
        });
        factory.ShowDocument(document);
        Assert.Same(document, factory.FindDocument("doc:test"));

        // Transient source documents never persist: the capture keeps only the welcome page.
        var recaptured = DockLayoutPersistence.Capture(rebuilt!)!;
        Assert.DoesNotContain("doc:test", CollectPaneIds(recaptured.Root!));
        Assert.Contains("Welcome", CollectPaneIds(recaptured.Root!));
    }
}
