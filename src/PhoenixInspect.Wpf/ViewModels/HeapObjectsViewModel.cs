using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Wpf.ViewModels;

/// <summary>Runs bounded strong-handle object searches and lets one match become the expression root.</summary>
public sealed class HeapObjectsViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly RelayCommand searchCommand;
    private readonly RelayCommand useRootCommand;
    private string typeName = string.Empty;
    private int maximumMatches = 64;
    private int maximumHandlesScanned = 100_000;
    private string summary = "No dump is open.";
    private bool isExhaustive;
    private HeapObjectRow? selected;

    /// <summary>Creates the heap-object panel.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public HeapObjectsViewModel(IShellServices shell)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        searchCommand = new RelayCommand(
            () => _ = SearchAsync(),
            () => this.shell.IsDumpOpen && !string.IsNullOrWhiteSpace(typeName));
        useRootCommand = new RelayCommand(
            () =>
            {
                if (selected is { } row)
                {
                    this.shell.UseAsEvaluationRoot(row);
                }
            },
            () => selected is not null);
        GroupedSearchFacts = PropertyGrouping.Create(SearchFacts);
    }

    /// <summary>Gets the last search's traversal facts grouped by section heading.</summary>
    public System.ComponentModel.ICollectionView GroupedSearchFacts { get; }

    /// <summary>Gets the retained matches of the last search.</summary>
    public ObservableCollection<HeapObjectRow> Matches { get; } = [];

    /// <summary>Gets the traversal counters and caps of the last search.</summary>
    public ObservableCollection<PropertyRow> SearchFacts { get; } = [];

    /// <summary>Gets the command that runs a bounded strong-handle search.</summary>
    public RelayCommand SearchCommand => searchCommand;

    /// <summary>Gets the command that adopts the selected match as the expression root.</summary>
    public RelayCommand UseAsRootCommand => useRootCommand;

    /// <summary>Gets or sets the exact ordinal runtime type-name predicate; it is not case-folded by the adapter.</summary>
    public string TypeName
    {
        get => typeName;
        set
        {
            if (Set(ref typeName, value))
            {
                searchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets or sets the retained-match cap.</summary>
    public int MaximumMatches
    {
        get => maximumMatches;
        set => Set(ref maximumMatches, Math.Clamp(value, 1, 4096));
    }

    /// <summary>Gets or sets the handle-inspection cap.</summary>
    public int MaximumHandlesScanned
    {
        get => maximumHandlesScanned;
        set => Set(ref maximumHandlesScanned, Math.Clamp(value, 1, 100_000));
    }

    /// <summary>Gets a one-line description of the last search outcome.</summary>
    public string Summary
    {
        get => summary;
        private set => Set(ref summary, value);
    }

    /// <summary>Gets whether the last traversal completed without reaching a cap or a partial read.</summary>
    public bool IsExhaustive
    {
        get => isExhaustive;
        private set => Set(ref isExhaustive, value);
    }

    /// <summary>Gets or sets the selected match.</summary>
    public HeapObjectRow? Selected
    {
        get => selected;
        set
        {
            if (Set(ref selected, value))
            {
                useRootCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Clears every previous match so the panel matches a newly opened or closed dump.</summary>
    public void Reset()
    {
        Matches.Clear();
        SearchFacts.Clear();
        Selected = null;
        IsExhaustive = false;
        Summary = shell.IsDumpOpen
            ? "Enter an exact runtime type name to search the strong-handle catalog."
            : "No dump is open.";
        searchCommand.RaiseCanExecuteChanged();
    }

    private async Task SearchAsync()
    {
        var predicate = typeName.Trim();
        var matches = MaximumMatches;
        var handles = MaximumHandlesScanned;
        var projection = await shell.RunAsync(
            $"Scanning up to {DisplayFormatting.Count(handles)} handles for {predicate}…",
            session => DumpInspectionService.SearchObjects(session, predicate, matches, handles)).ConfigureAwait(true);
        if (projection is null)
        {
            return;
        }

        Matches.Clear();
        SearchFacts.Clear();
        Selected = null;
        foreach (var row in projection.Rows)
        {
            Matches.Add(row);
        }

        foreach (var fact in projection.Facts)
        {
            SearchFacts.Add(fact);
        }

        Summary = projection.Summary;
        IsExhaustive = projection.IsExhaustive;
        shell.SetStatus(projection.Summary);
    }
}
