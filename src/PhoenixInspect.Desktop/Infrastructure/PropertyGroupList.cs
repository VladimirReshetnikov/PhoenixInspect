using System.Collections.ObjectModel;
using System.Collections.Specialized;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop;

/// <summary>One rendered group of labelled facts.</summary>
/// <param name="Name">The section heading.</param>
/// <param name="Rows">The rows of the group, in source order.</param>
public sealed record PropertyGroup(string Name, IReadOnlyList<PropertyRow> Rows);

/// <summary>
/// Projects a flat <see cref="PropertyRow"/> collection into ordered groups keyed by <see cref="PropertyRow.Group"/>.
/// </summary>
/// <remarks>
/// WPF supplied this through <c>ICollectionView</c> grouping; Avalonia has no equivalent, so the projection is a
/// plain observable rebuild. Sources here are always replaced wholesale (clear + add), so rebuilding on every
/// change notification stays cheap and keeps the display order identical to the source order.
/// </remarks>
public sealed class PropertyGroupList
{
    private readonly ObservableCollection<PropertyRow> source;

    /// <summary>Creates the grouped projection and subscribes to the source.</summary>
    /// <param name="source">The flat fact collection to project.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public PropertyGroupList(ObservableCollection<PropertyRow> source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        source.CollectionChanged += OnSourceChanged;
        Rebuild();
    }

    /// <summary>Gets the ordered groups; first appearance of a group name fixes its position.</summary>
    public ObservableCollection<PropertyGroup> Groups { get; } = [];

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        Groups.Clear();
        var order = new List<string>();
        var rows = new Dictionary<string, List<PropertyRow>>(StringComparer.Ordinal);
        foreach (var row in source)
        {
            if (!rows.TryGetValue(row.Group, out var list))
            {
                list = [];
                rows.Add(row.Group, list);
                order.Add(row.Group);
            }

            list.Add(row);
        }

        foreach (var name in order)
        {
            Groups.Add(new PropertyGroup(name, rows[name]));
        }
    }
}
