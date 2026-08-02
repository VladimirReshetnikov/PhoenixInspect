using System.Collections.Immutable;
using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// Presents the selected frame's parameters and local variable slots the way Visual Studio's Locals window does:
/// one row per variable with its name, declared type, and slot, following the Call Stack selection automatically.
/// </summary>
/// <remarks>
/// The pane shows declarations, not values: the adapter publishes no register or stack-slot mapping for managed
/// frames, and the pane says so instead of fabricating values. Names come from the identity-validated Portable
/// PDB and slot types from the method body in dump memory.
/// </remarks>
public sealed class LocalsViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private CallStackFrameNode? frame;
    private string caption = "No dump is open.";
    private string summary = string.Empty;

    /// <summary>Creates the locals pane.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public LocalsViewModel(IShellServices shell) =>
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

    /// <summary>Gets the decoded variable rows: parameters first, then local slots.</summary>
    public ObservableCollection<FrameVariableRow> Rows { get; } = [];

    /// <summary>Gets a one-line description of the frame whose variables are displayed.</summary>
    public string Caption
    {
        get => caption;
        private set => Set(ref caption, value);
    }

    /// <summary>Gets the honest summary of what was decoded and why values are not shown.</summary>
    public string Summary
    {
        get => summary;
        private set => Set(ref summary, value);
    }

    /// <summary>Clears the pane so it matches a newly opened or closed dump.</summary>
    public void Reset()
    {
        frame = null;
        Rows.Clear();
        Caption = shell.IsDumpOpen
            ? "Select a frame in the Call Stack pane to decode its parameters and locals."
            : "No dump is open.";
        Summary = string.Empty;
    }

    /// <summary>Decodes and displays one selected frame's parameters and local slots.</summary>
    /// <param name="node">The selected frame.</param>
    /// <returns>A task that completes once the rows are displayed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    public async Task LoadFrameAsync(CallStackFrameNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        frame = node;
        Caption = node.MethodDisplay;

        var explicitCandidates = shell.ExplicitPortablePdbCandidates;
        var projection = await shell.RunAsync(
            $"Decoding variables of frame #{node.FrameOrdinal}…",
            session => DumpInspectionService.DescribeFrameVariables(
                session,
                node,
                explicitCandidates
                    .Concat(SourceNavigationService.DiscoverPortablePdbCandidates(session))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(64)
                    .ToImmutableArray())).ConfigureAwait(true);
        if (projection is null || !ReferenceEquals(frame, node))
        {
            return;
        }

        Rows.Clear();
        foreach (var row in projection.Rows)
        {
            Rows.Add(row);
        }

        Summary = projection.Summary;
    }
}
