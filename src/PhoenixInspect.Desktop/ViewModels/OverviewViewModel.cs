using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>Presents the immutable snapshot identity, target facts, and the profiles this build actually enables.</summary>
public sealed class OverviewViewModel : ObservableObject
{
    /// <summary>Creates the overview pane and its grouped fact view.</summary>
    public OverviewViewModel() => GroupedProperties = new PropertyGroupList(Properties);

    /// <summary>Gets the snapshot facts, or an empty list when no dump is open.</summary>
    public ObservableCollection<PropertyRow> Properties { get; } = [];

    /// <summary>Gets the same facts grouped by their section heading.</summary>
    public PropertyGroupList GroupedProperties { get; }

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
        "Constant expressions evaluate without a runtime value read: integer arithmetic folds with checked C# "
        + "semantics, deterministic culture-independent string and char operations (ordinal search, substrings, "
        + "invariant case mapping, char classification) evaluate over constant operands, and fully qualified enum "
        + "members and const fields come from the module's metadata Constant table.",
        "The root-relative path evaluates a member expression against one exact heap object: a direct field, an "
        + "opt-in member chain of any depth with per-hop conditional access, a null-coalescing literal fallback, "
        + "or one admitted parameterless method.",
        "Observed values are limited to null, Int32, Nullable<Int32>, bounded strings, and validated object "
        + "references. Anything else is reported as an explicit typed stop rather than guessed.",
        "Source files are shown only when the on-disk bytes reproduce the Portable PDB's document checksum. A "
        + "missing or drifted file is a reported limit, never silently rendered as the captured code.",
        "Every answer carries its own status, completeness, evidence quality, applied bounds, raw reads, and a "
        + "canonical replay digest. A partial answer is never presented as an exact one.",
    ];

    /// <summary>Replaces the displayed snapshot facts.</summary>
    /// <param name="rows">The grouped facts to display; an empty span clears the pane.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
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
