using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// One row of the Watch grid: an editable watch entry, or a structured child row realized by expanding a compound
/// value. The shared shape lets one grid template render both without conditional bindings.
/// </summary>
public abstract class WatchRow : ObservableObject
{
    private bool isExpanded;

    /// <summary>Gets or sets the editable expression text; non-editable rows ignore writes.</summary>
    public virtual string Expression
    {
        get => string.Empty;
        set
        {
        }
    }

    /// <summary>Gets the display name of a non-editable row.</summary>
    public virtual string NameText => string.Empty;

    /// <summary>Gets the rendered value.</summary>
    public abstract string Value { get; }

    /// <summary>Gets the value kind spelling, or an empty string.</summary>
    public abstract string ValueKind { get; }

    /// <summary>Gets the status spelling, or an empty string.</summary>
    public virtual string Status => string.Empty;

    /// <summary>Gets the severity grouping for the row's colour dot.</summary>
    public virtual EvaluationSeverity Severity => EvaluationSeverity.Exact;

    /// <summary>Gets whether the row shows its severity dot.</summary>
    public virtual bool HasReport => false;

    /// <summary>Gets whether the row shows its delete affordance.</summary>
    public virtual bool ShowsDelete => false;

    /// <summary>Gets whether the row's name cell is a live expression editor.</summary>
    public virtual bool IsEditable => false;

    /// <summary>Gets whether the row carries structured children that can expand.</summary>
    public abstract bool HasChildren { get; }

    /// <summary>Gets the zero-based tree depth: entries at zero, children below their parent.</summary>
    public virtual int Depth => 0;

    /// <summary>Gets the left indent of the name cell, following the tree depth.</summary>
    public Thickness NameIndent => new(Depth * 16, 0, 0, 0);

    /// <summary>Gets or sets whether the row's children are currently realized in the grid.</summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (Set(ref isExpanded, value))
            {
                Raise(nameof(ExpanderGlyph));
            }
        }
    }

    /// <summary>Gets the expander glyph: a chevron when children exist, otherwise empty.</summary>
    public string ExpanderGlyph => HasChildren ? (IsExpanded ? "▾" : "▸") : string.Empty;

    /// <summary>Raises the change notifications that follow a value change.</summary>
    protected void RaiseValueChanged()
    {
        Raise(nameof(Value));
        Raise(nameof(ValueKind));
        Raise(nameof(Status));
        Raise(nameof(Severity));
        Raise(nameof(HasReport));
        Raise(nameof(HasChildren));
        Raise(nameof(ExpanderGlyph));
    }
}

/// <summary>
/// One editable watch entry: an expression and the complete report of its most recent evaluation. The last entry
/// of the pane is always a placeholder — committing text into it turns it into a real watch.
/// </summary>
public sealed class WatchEntry : WatchRow
{
    private string expression = string.Empty;
    private EvaluationReport? report;
    private string hint = string.Empty;
    private bool isPlaceholder;

    /// <summary>Creates one entry.</summary>
    /// <param name="isPlaceholder">Whether this row is the trailing add-a-watch placeholder.</param>
    public WatchEntry(bool isPlaceholder) => this.isPlaceholder = isPlaceholder;

    /// <inheritdoc />
    public override string Expression
    {
        get => expression;
        set => Set(ref expression, value ?? string.Empty);
    }

    /// <summary>Gets the expression text of the last commit, used to suppress redundant re-evaluation.</summary>
    public string CommittedExpression { get; internal set; } = string.Empty;

    /// <summary>Gets whether this row is the trailing add-a-watch placeholder.</summary>
    public bool IsPlaceholder
    {
        get => isPlaceholder;
        internal set
        {
            if (Set(ref isPlaceholder, value))
            {
                Raise(nameof(ShowsDelete));
            }
        }
    }

    /// <summary>Gets or sets the complete report of the last evaluation, or null before one ran.</summary>
    public EvaluationReport? Report
    {
        get => report;
        internal set
        {
            if (Set(ref report, value))
            {
                RaiseValueChanged();
            }
        }
    }

    /// <summary>Gets or sets the text shown in the value cell while no report exists.</summary>
    public string Hint
    {
        get => hint;
        internal set
        {
            if (Set(ref hint, value))
            {
                Raise(nameof(Value));
            }
        }
    }

    /// <inheritdoc />
    public override string Value => report?.Value ?? hint;

    /// <inheritdoc />
    public override string ValueKind => report?.ValueKind ?? string.Empty;

    /// <inheritdoc />
    public override string Status => report?.Status ?? string.Empty;

    /// <inheritdoc />
    public override EvaluationSeverity Severity => report?.Severity ?? EvaluationSeverity.Stopped;

    /// <inheritdoc />
    public override bool HasReport => report is not null;

    /// <inheritdoc />
    public override bool ShowsDelete => !isPlaceholder;

    /// <inheritdoc />
    public override bool IsEditable => true;

    /// <inheritdoc />
    public override bool HasChildren => report is { Children.Length: > 0 };

    /// <summary>Gets the structured children of the current report; empty before one ran.</summary>
    public ImmutableArray<ValueChildRow> Children => report?.Children ?? [];
}

/// <summary>One realized child row of an expanded compound value.</summary>
public sealed class WatchChildRow : WatchRow
{
    private readonly ValueChildRow source;

    /// <summary>Creates one child row.</summary>
    /// <param name="source">The structured child this row displays.</param>
    /// <param name="depth">The row's tree depth; direct children of an entry sit at one.</param>
    public WatchChildRow(ValueChildRow source, int depth)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        Depth = depth;
    }

    /// <inheritdoc />
    public override string NameText => source.Name;

    /// <inheritdoc />
    public override string Value => source.Value;

    /// <inheritdoc />
    public override string ValueKind => source.ValueKind ?? string.Empty;

    /// <inheritdoc />
    public override bool HasChildren => source.Children.Length > 0;

    /// <inheritdoc />
    public override int Depth { get; }

    /// <summary>Gets the structured children this row expands into.</summary>
    public ImmutableArray<ValueChildRow> Children => source.Children;
}

/// <summary>
/// Presents editable watch expressions the way Visual Studio's Watch window does: every row's expression can be
/// edited in place, the trailing row adds a new watch, compound values — arrays and tuples — expand into child
/// rows, and all rows re-evaluate when the evaluation context changes.
/// </summary>
/// <remarks>
/// Each expression routes through <see cref="ExpressionEvaluationService"/>.EvaluateWatch, and the children a row
/// expands into come from the report itself, so the pane decides nothing about values. The flattened-tree shape —
/// child rows realized beneath their parent on expansion, released on collapse — adapts the watch-expression tree
/// of the Helix IDE (used with permission) to this pane's immutable evidence reports.
/// </remarks>
public sealed class WatchViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly EvaluateViewModel evaluate;
    private readonly RelayCommand refreshCommand;

    /// <summary>Creates the watch pane.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <param name="evaluate">The evaluation pane whose adopted context, root, and options watches share.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public WatchViewModel(IShellServices shell, EvaluateViewModel evaluate)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        this.evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        refreshCommand = new RelayCommand(() => _ = RefreshAllAsync(), () => this.shell.IsDumpOpen);
        Rows.Add(new WatchEntry(isPlaceholder: true));
        evaluate.PropertyChanged += OnEvaluateContextChanged;
    }

    /// <summary>Gets the flattened rows: entries in order, expanded children beneath their parents.</summary>
    public ObservableCollection<WatchRow> Rows { get; } = [];

    /// <summary>Gets the command that commits pending edits and re-evaluates every watch.</summary>
    public RelayCommand RefreshCommand => refreshCommand;

    /// <summary>Gets the shell's shared completion state; the drop-down renders exactly what it returns.</summary>
    public CompletionSessionState Completion => shell.Completion;

    /// <summary>Gets the pane caption.</summary>
    public static string Caption =>
        "Type an expression and press Enter. Compound values expand; rows re-evaluate when the context changes.";

    /// <summary>Gets a statement of the context watches currently evaluate under.</summary>
    public string Summary => shell.IsDumpOpen
        ? evaluate.WatchContextSummary
        : "No dump is open. Watch expressions are kept and re-evaluate when a dump opens.";

    private IEnumerable<WatchEntry> Entries => Rows.OfType<WatchEntry>();

    private bool HasCommittedEntries => Entries.Any(static entry => !entry.IsPlaceholder);

    /// <summary>Resets values (never expressions) to match a newly opened or closed dump, then re-evaluates.</summary>
    public void Reset()
    {
        foreach (var entry in Entries.ToArray())
        {
            SetReport(entry, null);
            entry.Hint = shell.IsDumpOpen ? string.Empty : "No dump is open.";
        }

        Raise(nameof(Summary));
        refreshCommand.RaiseCanExecuteChanged();
        if (shell.IsDumpOpen && HasCommittedEntries)
        {
            _ = RefreshAllAsync();
        }
    }

    /// <summary>Commits one row's edited expression: adds, re-evaluates, or removes it.</summary>
    /// <param name="entry">The row being committed.</param>
    /// <returns>A task that completes once the row reflects the commit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public async Task CommitAsync(WatchEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (CommitText(entry))
        {
            await EvaluateEntriesAsync([entry]).ConfigureAwait(true);
        }
    }

    /// <summary>Handles focus leaving a row's editor: commits a change, or restores an emptied expression.</summary>
    /// <param name="entry">The row whose editor lost focus.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public void CommitOnFocusLoss(WatchEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var text = entry.Expression.Trim();
        if (entry.IsPlaceholder)
        {
            if (text.Length > 0)
            {
                _ = CommitAsync(entry);
            }

            return;
        }

        if (text.Length == 0)
        {
            // Deleting on mere focus loss would be surprising; emptied text restores. Enter deletes explicitly.
            entry.Expression = entry.CommittedExpression;
            return;
        }

        _ = CommitAsync(entry);
    }

    /// <summary>Removes one committed watch row and its realized children.</summary>
    /// <param name="entry">The row to remove; the placeholder row is never removed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public void Remove(WatchEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.IsPlaceholder)
        {
            return;
        }

        RemoveDescendants(entry);
        Rows.Remove(entry);
        refreshCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Expands or collapses one row's structured children.</summary>
    /// <param name="row">The row whose expander was toggled.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public void ToggleExpand(WatchRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.HasChildren)
        {
            return;
        }

        if (row.IsExpanded)
        {
            row.IsExpanded = false;
            RemoveDescendants(row);
            return;
        }

        row.IsExpanded = true;
        InsertChildren(row);
    }

    /// <summary>
    /// Commits any pending edits — including text typed into the placeholder but not yet entered — and then
    /// re-evaluates every committed watch against the current context.
    /// </summary>
    /// <returns>A task that completes once every row reflects the refreshed evaluation.</returns>
    public Task RefreshAllAsync()
    {
        foreach (var entry in Entries.ToArray())
        {
            CommitText(entry);
        }

        return EvaluateEntriesAsync([.. Entries.Where(static entry => !entry.IsPlaceholder)]);
    }

    /// <summary>Normalizes and commits one row's text without evaluating it.</summary>
    /// <param name="entry">The row to commit.</param>
    /// <returns>Whether the row now carries a committed expression that needs (re-)evaluation.</returns>
    private bool CommitText(WatchEntry entry)
    {
        var text = entry.Expression.Trim();
        if (entry.IsPlaceholder)
        {
            if (text.Length == 0)
            {
                return false;
            }

            // The placeholder becomes a real watch and a fresh placeholder keeps the add row available.
            entry.IsPlaceholder = false;
            Rows.Add(new WatchEntry(isPlaceholder: true));
        }
        else if (text.Length == 0)
        {
            // Committing an emptied expression deletes the watch, exactly as Visual Studio does.
            Remove(entry);
            return false;
        }
        else if (string.Equals(text, entry.CommittedExpression, StringComparison.Ordinal) && entry.HasReport)
        {
            return false;
        }

        entry.Expression = text;
        entry.CommittedExpression = text;
        refreshCommand.RaiseCanExecuteChanged();
        return true;
    }

    private void OnEvaluateContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(EvaluateViewModel.ContextFrame) or nameof(EvaluateViewModel.RootSelection)
            or nameof(EvaluateViewModel.RootIdentifier)))
        {
            return;
        }

        Raise(nameof(Summary));
        if (shell.IsDumpOpen && HasCommittedEntries)
        {
            _ = RefreshAllAsync();
        }
    }

    private async Task EvaluateEntriesAsync(IReadOnlyList<WatchEntry> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        if (!shell.IsDumpOpen)
        {
            foreach (var entry in targets)
            {
                SetReport(entry, null);
                entry.Hint = "No dump is open.";
            }

            return;
        }

        var expressions = targets.Select(static entry => entry.Expression).ToImmutableArray();
        var contextFactory = evaluate.CreateWatchContextFactory();
        var reports = await shell.RunAsync(
            targets.Count == 1
                ? "Evaluating watch expression…"
                : $"Refreshing {targets.Count} watch expressions…",
            session =>
            {
                var context = contextFactory(session);
                return expressions
                    .Select(expression => ExpressionEvaluationService.EvaluateWatch(session, expression, context))
                    .ToImmutableArray();
            }).ConfigureAwait(true);
        if (reports.IsDefault || reports.Length != targets.Count)
        {
            return;
        }

        for (var index = 0; index < targets.Count; index++)
        {
            SetReport(targets[index], reports[index]);
        }

        shell.SetStatus(targets.Count == 1
            ? $"Watch: {targets[0].Report!.Path} — {targets[0].Report!.Status}"
            : $"Watch: {targets.Count} expressions re-evaluated.");
    }

    /// <summary>
    /// Replaces one entry's report and re-realizes its child rows: an expanded entry keeps its expansion and shows
    /// the fresh children; children of children re-collapse, because the new values are new evidence.
    /// </summary>
    private void SetReport(WatchEntry entry, EvaluationReport? report)
    {
        RemoveDescendants(entry);
        entry.Report = report;
        if (!entry.HasChildren)
        {
            entry.IsExpanded = false;
            return;
        }

        if (entry.IsExpanded)
        {
            InsertChildren(entry);
        }
    }

    private void InsertChildren(WatchRow row)
    {
        var children = row switch
        {
            WatchEntry entry => entry.Children,
            WatchChildRow child => child.Children,
            _ => [],
        };
        var insertAt = Rows.IndexOf(row) + 1;
        foreach (var child in children)
        {
            Rows.Insert(insertAt++, new WatchChildRow(child, row.Depth + 1));
        }
    }

    private void RemoveDescendants(WatchRow row)
    {
        var index = Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        while (index + 1 < Rows.Count && Rows[index + 1].Depth > row.Depth && Rows[index + 1] is WatchChildRow)
        {
            Rows.RemoveAt(index + 1);
        }
    }
}
