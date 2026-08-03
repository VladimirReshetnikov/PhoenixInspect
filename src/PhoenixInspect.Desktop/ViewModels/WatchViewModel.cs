using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// One row of the Watch pane: an editable expression, and the complete report of its most recent evaluation.
/// </summary>
/// <remarks>
/// The last row of the pane is always a placeholder — committing text into it turns it into a real watch and
/// appends a fresh placeholder, mirroring Visual Studio's "Add item to watch window" row.
/// </remarks>
public sealed class WatchEntry : ObservableObject
{
    private string expression = string.Empty;
    private string committedExpression = string.Empty;
    private EvaluationReport? report;
    private string hint = string.Empty;
    private bool isPlaceholder;

    /// <summary>Creates one row.</summary>
    /// <param name="isPlaceholder">Whether this row is the trailing add-a-watch placeholder.</param>
    public WatchEntry(bool isPlaceholder) => this.isPlaceholder = isPlaceholder;

    /// <summary>Gets or sets the editable expression text; committing it re-evaluates the row.</summary>
    public string Expression
    {
        get => expression;
        set => Set(ref expression, value ?? string.Empty);
    }

    /// <summary>Gets the expression text of the last commit, used to suppress redundant re-evaluation.</summary>
    public string CommittedExpression
    {
        get => committedExpression;
        internal set => committedExpression = value;
    }

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

    /// <summary>Gets whether the row shows its delete affordance.</summary>
    public bool ShowsDelete => !isPlaceholder;

    /// <summary>Gets or sets the complete report of the last evaluation, or null before one ran.</summary>
    public EvaluationReport? Report
    {
        get => report;
        internal set
        {
            if (Set(ref report, value))
            {
                Raise(nameof(Value));
                Raise(nameof(ValueKind));
                Raise(nameof(Status));
                Raise(nameof(Severity));
                Raise(nameof(HasReport));
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

    /// <summary>Gets whether an evaluation report is displayed.</summary>
    public bool HasReport => report is not null;

    /// <summary>Gets the rendered value, or the hint while no report exists.</summary>
    public string Value => report?.Value ?? hint;

    /// <summary>Gets the value kind spelling, or an empty string.</summary>
    public string ValueKind => report?.ValueKind ?? string.Empty;

    /// <summary>Gets the product's terminal status spelling, or an empty string.</summary>
    public string Status => report?.Status ?? string.Empty;

    /// <summary>Gets the severity grouping of the last report, for the row's colour dot.</summary>
    public EvaluationSeverity Severity => report?.Severity ?? EvaluationSeverity.Stopped;
}

/// <summary>
/// Presents editable watch expressions the way Visual Studio's Watch window does: every row's expression can be
/// edited in place, the trailing row adds a new watch, and all rows re-evaluate when the evaluation context — the
/// adopted frame, the adopted root, or the root identifier — changes.
/// </summary>
/// <remarks>
/// Each expression routes through <see cref="ExpressionEvaluationService.EvaluateWatch"/>, so which entry point
/// answers is the API's single deterministic rule, not a pane decision. Expressions survive dump close and reopen;
/// their values are re-read from the new session's evidence.
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
        Entries.Add(new WatchEntry(isPlaceholder: true));
        evaluate.PropertyChanged += OnEvaluateContextChanged;
    }

    /// <summary>Gets the watch rows; the last row is always the add-a-watch placeholder.</summary>
    public ObservableCollection<WatchEntry> Entries { get; } = [];

    /// <summary>Gets the command that re-evaluates every committed watch.</summary>
    public RelayCommand RefreshCommand => refreshCommand;

    /// <summary>Gets the pane caption.</summary>
    public static string Caption => "Type an expression and press Enter. Rows re-evaluate when the context changes.";

    /// <summary>Gets a statement of the context watches currently evaluate under.</summary>
    public string Summary
    {
        get
        {
            if (!shell.IsDumpOpen)
            {
                return "No dump is open. Watch expressions are kept and re-evaluate when a dump opens.";
            }

            var identifier = evaluate.RootIdentifier.Trim();
            var rootPart = evaluate.RootSelection is { } root
                ? $"Expressions mentioning '{identifier}' evaluate against the adopted root: {root.Description}"
                : "No root is adopted, so every expression binds through the static-field path.";
            var contextPart = evaluate.ContextFrame is null
                ? " Static names must be fully qualified until a frame is adopted as name context."
                : " Contextual static names may bind through the adopted frame.";
            return rootPart + contextPart;
        }
    }

    private bool HasCommittedEntries => Entries.Any(static entry => !entry.IsPlaceholder);

    /// <summary>Resets values (never expressions) to match a newly opened or closed dump, then re-evaluates.</summary>
    public void Reset()
    {
        foreach (var entry in Entries)
        {
            entry.Report = null;
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

    /// <summary>Removes one committed watch row.</summary>
    /// <param name="entry">The row to remove; the placeholder row is never removed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public void Remove(WatchEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsPlaceholder)
        {
            Entries.Remove(entry);
            refreshCommand.RaiseCanExecuteChanged();
        }
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
            Entries.Add(new WatchEntry(isPlaceholder: true));
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
                entry.Report = null;
                entry.Hint = "No dump is open.";
            }

            return;
        }

        var expressions = targets.Select(static entry => entry.Expression).ToImmutableArray();
        var contextSelector = evaluate.ContextFrame?.Selector;
        var root = evaluate.RootSelection;
        var rootIdentifier = evaluate.RootIdentifier;
        var options = new RootRelativeEvaluationOptions
        {
            UseModeledMethods = evaluate.UseModeledMethods,
            AdmitMemberChain = evaluate.AdmitMemberChain,
            InstructionLimit = evaluate.InstructionLimit,
            LogicalDepthLimit = evaluate.LogicalDepthLimit,
            TraversalLimit = evaluate.TraversalLimit,
        };
        var explicitCandidates = shell.ExplicitPortablePdbCandidates;
        var reports = await shell.RunAsync(
            targets.Count == 1
                ? "Evaluating watch expression…"
                : $"Refreshing {targets.Count} watch expressions…",
            session =>
            {
                var context = new WatchEvaluationContext
                {
                    ContextSelector = contextSelector,
                    PortablePdbCandidates = SourceNavigationService.AssemblePortablePdbCandidates(
                        session, explicitCandidates),
                    Root = root,
                    RootIdentifier = rootIdentifier,
                    Options = options,
                };
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
            targets[index].Report = reports[index];
        }

        shell.SetStatus(targets.Count == 1
            ? $"Watch: {targets[0].Report!.Path} — {targets[0].Report!.Status}"
            : $"Watch: {targets.Count} expressions re-evaluated.");
    }
}
