using System.Collections.ObjectModel;
using System.ComponentModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Wpf.ViewModels;

/// <summary>Presents the immutable snapshot identity, target facts, and the profiles this build actually enables.</summary>
public sealed class OverviewViewModel : ObservableObject
{
    /// <summary>Creates the overview panel and its grouped fact view.</summary>
    public OverviewViewModel() => GroupedProperties = PropertyGrouping.Create(Properties);

    /// <summary>Gets the snapshot facts, or an empty list when no dump is open.</summary>
    public ObservableCollection<PropertyRow> Properties { get; } = [];

    /// <summary>Gets the same facts grouped by their section heading.</summary>
    public ICollectionView GroupedProperties { get; }

    /// <summary>Gets the fixed capability notes shown beside the snapshot facts.</summary>
    /// <remarks>
    /// These notes describe the deliberately narrow expression surface that is implemented today. They exist so a
    /// viewer of this demo does not mistake a bounded capability for a general-purpose debugger feature.
    /// </remarks>
    public ObservableCollection<string> CapabilityNotes { get; } =
    [
        "Expression evaluation is read-only and deterministic. Nothing in the target is executed for real; the "
        + "interpreter runs against captured bytes under explicit instruction, depth, and traversal budgets.",
        "The static-field path binds context-independent fully qualified static fields from counted module metadata. "
        + "Contextual names additionally need selected-frame and Portable-PDB import, alias, and namespace facts.",
        "The root-relative path evaluates a member expression against one exact heap object: a direct field, an "
        + "opt-in two-hop member chain, a null-coalescing literal fallback, or one admitted parameterless method.",
        "Observed values are limited to null, Int32, Nullable<Int32>, bounded strings, and validated object "
        + "references. Anything else is reported as an explicit typed stop rather than guessed.",
        "Every answer carries its own status, completeness, evidence quality, applied bounds, raw reads, and a "
        + "canonical replay digest. A partial answer is never presented as an exact one.",
    ];

    /// <summary>Replaces the displayed snapshot facts.</summary>
    /// <param name="rows">The grouped facts to display; an empty span clears the panel.</param>
    public void Load(IEnumerable<PropertyRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Properties.Clear();
        foreach (var row in rows)
        {
            Properties.Add(row);
        }

        Raise(nameof(HasProperties));
    }

    /// <summary>Gets whether any snapshot fact is currently displayed.</summary>
    public bool HasProperties => Properties.Count > 0;
}
