using System.Text;
using PhoenixInspect.Inspection;

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

    /// <summary>Gets a statement of the context immediate expressions currently evaluate under.</summary>
    public string Summary => shell.IsDumpOpen
        ? evaluate.WatchContextSummary
        : "No dump is open. Open one to evaluate expressions here.";

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
        if (!shell.IsDumpOpen)
        {
            AppendLine("// No dump is open.");
            AppendLine(string.Empty);
            return;
        }

        var contextFactory = evaluate.CreateWatchContextFactory();
        var report = await shell.RunAsync(
            "Evaluating immediate expression…",
            session => ExpressionEvaluationService.EvaluateWatch(session, text, contextFactory(session)))
            .ConfigureAwait(true);
        if (report is null)
        {
            AppendLine("// The evaluation failed; the shell reported the error.");
            AppendLine(string.Empty);
            return;
        }

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
