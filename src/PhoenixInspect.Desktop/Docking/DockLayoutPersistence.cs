using System.Text.Json;
using System.Text.Json.Serialization;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace PhoenixInspect.Desktop.Docking;

/// <summary>One node of a captured panel layout: a split band, a tab group, a splitter, or a pane.</summary>
public sealed record DockLayoutNode
{
    /// <summary>Gets the node kind: <c>Proportional</c>, <c>Tools</c>, <c>Documents</c>, <c>Splitter</c>, or <c>Pane</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Gets the dock or pane id.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the proportional size within the parent band, or null when unset.</summary>
    public double? Proportion { get; init; }

    /// <summary>Gets the band orientation for a proportional node.</summary>
    public string? Orientation { get; init; }

    /// <summary>Gets the tool-dock alignment.</summary>
    public string? Alignment { get; init; }

    /// <summary>Gets the id of the tab active within this group.</summary>
    public string? ActiveId { get; init; }

    /// <summary>Gets the child nodes, in visual order.</summary>
    public IReadOnlyList<DockLayoutNode>? Children { get; init; }
}

/// <summary>A complete captured panel layout, versioned so an incompatible file falls back to the default.</summary>
public sealed record DockLayoutSnapshot
{
    /// <summary>The current snapshot schema version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets the schema version the snapshot was written with.</summary>
    public int Version { get; init; }

    /// <summary>Gets the root layout node.</summary>
    public DockLayoutNode? Root { get; init; }
}

/// <summary>
/// Captures and rebuilds the shell's panel layout as plain geometry facts — which pane lives in which group, in
/// what order, which tab is active, and how the bands are proportioned. Only those facts are persisted: the
/// panes themselves are non-closable singletons resolved by id at rebuild time, so no view-model state ever
/// reaches the layout file, and any file the rebuild cannot place exactly falls back to the default layout.
/// </summary>
public static class DockLayoutPersistence
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes one snapshot as indented JSON.</summary>
    /// <param name="snapshot">The snapshot to serialize.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(DockLayoutSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SerializerOptions);

    /// <summary>Deserializes one snapshot, or null when the text is not a snapshot.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The snapshot, or null.</returns>
    public static DockLayoutSnapshot? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DockLayoutSnapshot>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Captures the current layout, or null when the tree contains a shape this schema does not model — a
    /// floating pane's host, a nested document window — so an unknown arrangement is never half-persisted.
    /// </summary>
    /// <param name="root">The live root dock.</param>
    /// <returns>The snapshot, or null.</returns>
    public static DockLayoutSnapshot? Capture(IRootDock root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.VisibleDockables is not [{ } main] || CaptureNode(main) is not { } captured)
        {
            return null;
        }

        return new DockLayoutSnapshot { Version = DockLayoutSnapshot.CurrentVersion, Root = captured };
    }

    private static DockLayoutNode? CaptureNode(IDockable dockable)
    {
        switch (dockable)
        {
            case ProportionalDockSplitter splitter:
                return new DockLayoutNode { Kind = "Splitter", Id = splitter.Id };
            case ToolDock tools:
                return CaptureGroup(tools, "Tools", tools.Alignment.ToString());
            case DocumentDock documents:
                return CaptureGroup(documents, "Documents", alignment: null);
            case ProportionalDock band:
            {
                var children = new List<DockLayoutNode>();
                foreach (var child in band.VisibleDockables ?? [])
                {
                    if (CaptureNode(child) is not { } node)
                    {
                        return null;
                    }

                    children.Add(node);
                }

                return new DockLayoutNode
                {
                    Kind = "Proportional",
                    Id = band.Id,
                    Proportion = ProportionOf(band.Proportion),
                    Orientation = band.Orientation.ToString(),
                    Children = children,
                };
            }

            default:
                return null;
        }
    }

    private static DockLayoutNode? CaptureGroup(IDock group, string kind, string? alignment)
    {
        var panes = new List<DockLayoutNode>();
        foreach (var child in group.VisibleDockables ?? [])
        {
            switch (child)
            {
                // A transient document — a source tab — belongs to the session, not the layout.
                case Document { CanClose: true }:
                    continue;
                case Tool or Document:
                    panes.Add(new DockLayoutNode { Kind = "Pane", Id = child.Id });
                    continue;
                default:
                    return null;
            }
        }

        return new DockLayoutNode
        {
            Kind = kind,
            Id = group.Id,
            Proportion = ProportionOf(group.Proportion),
            Alignment = alignment,
            ActiveId = group.ActiveDockable is Tool or Document ? group.ActiveDockable.Id : null,
            Children = panes,
        };
    }

    private static double? ProportionOf(double proportion) =>
        double.IsNaN(proportion) || proportion <= 0 ? null : proportion;
}
