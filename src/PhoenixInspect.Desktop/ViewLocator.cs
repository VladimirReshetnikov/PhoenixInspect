using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Desktop.Views;

namespace PhoenixInspect.Desktop;

/// <summary>Maps dockable view models to their views for the dock chrome.</summary>
/// <remarks>
/// The mapping is explicit rather than convention-based so a missing view is a compile-visible gap here instead of
/// a blank pane at runtime. Views are cached per dockable identity because the dock chrome re-realizes content on
/// tab switches, and pane state (scroll position, editor caret) should survive that.
/// </remarks>
public sealed class ViewLocator : IDataTemplate
{
    private readonly Dictionary<object, Control> cache = [];

    /// <inheritdoc />
    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        if (cache.TryGetValue(data, out var cached))
        {
            return cached;
        }

        Control? view = data switch
        {
            CallStackTool => new CallStackPaneView(),
            ModulesTool => new ModulesPaneView(),
            HeapSearchTool => new HeapSearchPaneView(),
            EvaluateTool => new EvaluatePaneView(),
            ResultTool => new ResultPaneView(),
            WelcomeDocument => new WelcomeDocumentView(),
            SourceDocument => new SourceDocumentView(),
            _ => null,
        };
        if (view is null)
        {
            return null;
        }

        view.DataContext = data;
        cache[data] = view;
        return view;
    }

    /// <inheritdoc />
    public bool Match(object? data) => data is
        CallStackTool or ModulesTool or HeapSearchTool or EvaluateTool or ResultTool or
        WelcomeDocument or SourceDocument;
}
