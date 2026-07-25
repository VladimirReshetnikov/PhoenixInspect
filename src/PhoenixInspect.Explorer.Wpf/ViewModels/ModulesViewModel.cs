using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using PhoenixInspect.Explorer.Wpf.Infrastructure;
using PhoenixInspect.Explorer.Wpf.Models;
using PhoenixInspect.Explorer.Wpf.Services;

namespace PhoenixInspect.Explorer.Wpf.ViewModels;

/// <summary>Presents every managed module instance and reads one module's content identity on demand.</summary>
public sealed class ModulesViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly ICollectionView view;
    private string filter = string.Empty;
    private ModuleRow? selected;

    /// <summary>Creates the modules panel.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public ModulesViewModel(IShellServices shell)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        view = new CollectionViewSource { Source = Modules }.View;
        view.Filter = OnFilter;
        GroupedDetails = PropertyGrouping.Create(Details);
    }

    /// <summary>Gets the selected module's content-identity facts grouped by section heading.</summary>
    public ICollectionView GroupedDetails { get; }

    /// <summary>Gets every projected module instance.</summary>
    public ObservableCollection<ModuleRow> Modules { get; } = [];

    /// <summary>Gets the content-identity facts for <see cref="Selected"/>.</summary>
    public ObservableCollection<PropertyRow> Details { get; } = [];

    /// <summary>Gets the filtered view bound by the grid.</summary>
    public ICollectionView View => view;

    /// <summary>Gets or sets a case-insensitive substring filter applied to the module name and target path hint.</summary>
    public string Filter
    {
        get => filter;
        set
        {
            if (Set(ref filter, value))
            {
                view.Refresh();
            }
        }
    }

    /// <summary>Gets or sets the selected module; assigning a module reads its metadata content identity.</summary>
    public ModuleRow? Selected
    {
        get => selected;
        set
        {
            if (!Set(ref selected, value))
            {
                return;
            }

            Raise(nameof(HasSelection));
            _ = LoadDetailsAsync(value);
        }
    }

    /// <summary>Gets whether a module is currently selected.</summary>
    public bool HasSelection => selected is not null;

    /// <summary>Replaces the module list and clears any previous selection.</summary>
    /// <param name="rows">The projected modules to display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public void Load(IEnumerable<ModuleRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Selected = null;
        Details.Clear();
        Modules.Clear();
        foreach (var row in rows)
        {
            Modules.Add(row);
        }

        view.Refresh();
        Raise(nameof(Summary));
    }

    /// <summary>Gets a one-line description of the loaded module catalog.</summary>
    public string Summary => Modules.Count == 0
        ? "No dump is open."
        : $"{DisplayFormatting.Count(Modules.Count)} managed module instances reported by the runtime. "
          + "Select one to read its counted metadata content identity from dump memory.";

    private bool OnFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        if (item is not ModuleRow row)
        {
            return false;
        }

        return row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.TargetPathHint.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadDetailsAsync(ModuleRow? row)
    {
        Details.Clear();
        if (row is null || !shell.IsDumpOpen)
        {
            return;
        }

        var rows = await shell.RunAsync(
            $"Reading metadata for {row.Name}…",
            session => DumpInspectionService.DescribeModuleContent(session, row.Module)).ConfigureAwait(true);
        if (rows.IsDefault || !ReferenceEquals(row, selected))
        {
            return;
        }

        foreach (var detail in rows)
        {
            Details.Add(detail);
        }
    }
}
