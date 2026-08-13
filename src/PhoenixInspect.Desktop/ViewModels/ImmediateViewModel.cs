using System.Collections.Immutable;
using System.Text;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// Presents an Immediate window, like Visual Studio's: type an expression at the prompt, press Enter, and the
/// expression and its answer append to a C#-colored transcript. Expressions route through the same watch entry
/// point the Watch pane uses, completion comes from the same shared state, and the transcript renders values with
/// evidence-status annotations as comments, so a stop reads as an explanation rather than a bare error.
/// </summary>
public sealed class ImmediateViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly EvaluateViewModel evaluate;
    private readonly RelayCommand clearCommand;
    private readonly StringBuilder transcript = new();
    private readonly List<string> history = [];
    private int historyIndex = -1;
    private string inputText = string.Empty;
    private ImmutableArray<ConstantReferenceAssembly> references = [];
    private ConstantUsingDirectiveSet usings = ConstantUsingDirectiveSet.Empty;
    private readonly ImmediateVariableStore variables = new();

    /// <summary>Creates the immediate pane.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <param name="evaluate">The evaluation pane whose adopted context, root, and options this pane shares.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ImmediateViewModel(IShellServices shell, EvaluateViewModel evaluate)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        this.evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        clearCommand = new RelayCommand(Clear, () => transcript.Length > 0);
    }

    /// <summary>Gets the pane caption.</summary>
    public static string Caption =>
        "Type an expression and press Enter. ↑/↓ recall history; 'cls' clears the transcript.";

    /// <summary>Gets the complete transcript text rendered by the C#-colored editor.</summary>
    public string TranscriptText => transcript.ToString();

    /// <summary>Gets or sets the prompt input text.</summary>
    public string InputText
    {
        get => inputText;
        set => Set(ref inputText, value ?? string.Empty);
    }

    /// <summary>Gets the command that clears the transcript.</summary>
    public RelayCommand ClearCommand => clearCommand;

    /// <summary>Gets the shell's shared completion state; the drop-down renders exactly what it returns.</summary>
    public CompletionSessionState Completion => shell.Completion;

    /// <summary>
    /// Gets the immediate window's completion context: the declared variables complete as identifiers, and the
    /// statement keywords offer at the start of a line, because the prompt accepts declarations and directives.
    /// </summary>
    public CompletionContext CompletionContext => new()
    {
        Locals = variables.ListCompletions(),
        AllowsStatements = true,
        Usings = usings,
    };

    /// <summary>Gets a statement of the context immediate expressions currently evaluate under.</summary>
    public string Summary => shell.IsDumpOpen
        ? evaluate.WatchContextSummary
        : "No dump is open. Literals and deterministic expressions evaluate now; dump values need an open snapshot.";

    /// <summary>Updates the context statement to match a newly opened or closed dump.</summary>
    public void Reset() => Raise(nameof(Summary));

    /// <summary>Submits the current input: evaluates it, or runs the <c>cls</c> transcript command.</summary>
    /// <returns>A task that completes once the transcript reflects the submission.</returns>
    public async Task SubmitAsync()
    {
        var text = InputText.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (history.Count == 0 || !string.Equals(history[^1], text, StringComparison.Ordinal))
        {
            history.Add(text);
        }

        historyIndex = -1;
        InputText = string.Empty;

        if (text is "cls" or "clear")
        {
            Clear();
            return;
        }

        AppendLine("> " + text);

        // A '#r' directive references an assembly for subsequent expressions rather than evaluating a value; its
        // types, constants, and enums then resolve alongside the dump's own modules, or on their own with no dump.
        if (ReferenceDirective.IsDirective(text))
        {
            var applied = ReferenceDirective.TryApply(text, references, out var updated, out var directiveMessage);
            if (applied)
            {
                references = updated;
            }

            AppendLine($"// {directiveMessage}");
            AppendLine(string.Empty);
            shell.SetStatus($"Immediate: #r — {(applied ? "referenced" : "not referenced")}");
            return;
        }

        // A 'using' directive imports a namespace, statically imports a type, or binds an alias for subsequent
        // expressions, so they may omit the imported prefixes; like '#r' it names scope rather than a value.
        if (UsingDirective.IsDirective(text))
        {
            var applied = UsingDirective.TryApply(text, usings, out var updatedUsings, out var usingMessage);
            if (applied)
            {
                usings = updatedUsings;
            }

            AppendLine($"// {usingMessage}");
            AppendLine(string.Empty);
            shell.SetStatus($"Immediate: using — {(applied ? "imported" : "not imported")}");
            return;
        }

        // A declaration or assignment stores a variable for later expressions rather than producing a value; the
        // initializer is evaluated with the variables already in scope, so 'var y = x * 2;' composes with 'x'.
        if (ImmediateVariableStore.IsStatement(text))
        {
            var pinnedReferencesForStatement = references;
            var pinnedUsingsForStatement = usings;
            var applied = variables.TryApply(
                text,
                initializer => EvaluateForValue(initializer, pinnedReferencesForStatement, pinnedUsingsForStatement),
                out var statementMessage);
            AppendLine($"// {statementMessage}");
            AppendLine(string.Empty);
            shell.SetStatus($"Immediate: variable — {(applied ? "assigned" : "not assigned")}");
            return;
        }

        if (!shell.IsDumpOpen)
        {
            // The sessionless entry folds the evidence-free constant subset and refuses everything beyond it
            // with the typed no-snapshot stop, so the prompt is useful before any dump opens. References,
            // using directives, and declared variables contribute their values even with no snapshot.
            RenderReport(ExpressionEvaluationService.EvaluateWithoutSnapshot(
                text, references, usings, variables.LocalNameResolver));
            return;
        }

        var contextFactory = evaluate.CreateWatchContextFactory();
        var pinnedReferences = references;
        var pinnedUsings = usings;
        var pinnedVariables = variables.LocalNameResolver;
        var report = await shell.RunAsync(
            "Evaluating immediate expression…",
            session => ExpressionEvaluationService.EvaluateWatch(
                session,
                text,
                contextFactory(session),
                pinnedReferences,
                pinnedUsings,
                pinnedVariables))
            .ConfigureAwait(true);
        if (report is null)
        {
            AppendLine("// The evaluation failed; the shell reported the error.");
            AppendLine(string.Empty);
            return;
        }

        RenderReport(report);
    }

    private ConstantExpressionEvaluation EvaluateForValue(
        string initializer,
        ImmutableArray<ConstantReferenceAssembly> pinnedReferences,
        ConstantUsingDirectiveSet pinnedUsings) =>
        ExpressionEvaluationService.EvaluateConstantValue(
            initializer,
            pinnedReferences,
            pinnedUsings,
            variables.LocalNameResolver);

    private void RenderReport(EvaluationReport report)
    {
        if (report.Severity is EvaluationSeverity.Exact or EvaluationSeverity.Absent)
        {
            AppendLine(report.ValueKind is { } kind ? $"{report.Value}  // {kind}" : report.Value);
        }
        else
        {
            // A non-exact answer is an explanation, not a value; comments keep it visually distinct.
            AppendLine($"// {report.Status} — {report.Stage}");
            foreach (var diagnostic in report.Diagnostics)
            {
                AppendLine($"// {diagnostic.Code}: {diagnostic.Message}");
            }
        }

        AppendLine(string.Empty);
        shell.SetStatus($"Immediate: {report.Path} — {report.Status}");
    }

    /// <summary>Recalls the previous history entry, or null at the oldest.</summary>
    /// <returns>The recalled expression, or null.</returns>
    public string? HistoryPrevious()
    {
        if (history.Count == 0)
        {
            return null;
        }

        historyIndex = historyIndex < 0 ? history.Count - 1 : Math.Max(0, historyIndex - 1);
        return history[historyIndex];
    }

    /// <summary>Recalls the next history entry, or an empty string past the newest.</summary>
    /// <returns>The recalled expression, or null when history is not being navigated.</returns>
    public string? HistoryNext()
    {
        if (history.Count == 0 || historyIndex < 0)
        {
            return null;
        }

        historyIndex++;
        if (historyIndex >= history.Count)
        {
            historyIndex = -1;
            return string.Empty;
        }

        return history[historyIndex];
    }

    private void Clear()
    {
        transcript.Clear();
        Raise(nameof(TranscriptText));
        clearCommand.RaiseCanExecuteChanged();
    }

    private void AppendLine(string line)
    {
        transcript.AppendLine(line);
        Raise(nameof(TranscriptText));
        clearCommand.RaiseCanExecuteChanged();
    }
}
