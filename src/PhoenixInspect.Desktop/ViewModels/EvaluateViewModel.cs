using System.Collections.Immutable;
using System.Collections.ObjectModel;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>One ready-made expression shape offered by the evaluation pane.</summary>
/// <param name="Label">The short description shown on the chip.</param>
/// <param name="Text">The template text inserted into the editor.</param>
/// <param name="Path">The entry point the template belongs to.</param>
public sealed record ExpressionSample(string Label, string Text, ExpressionPath Path);

/// <summary>
/// Drives the two implemented expression entry points and presents the complete evidence behind each answer.
/// </summary>
public sealed class EvaluateViewModel : ObservableObject
{
    private readonly IShellServices shell;
    private readonly RelayCommand evaluateCommand;
    private readonly RelayCommand clearContextCommand;
    private readonly RelayCommand clearRootCommand;
    private readonly RelayCommand clearHistoryCommand;
    private readonly RelayCommand promoteRootCommand;

    private ExpressionPath path = ExpressionPath.StaticField;
    private string expression = string.Empty;
    private string portablePdbCandidates = string.Empty;
    private string rootIdentifier = "root";
    private bool admitMemberChain = true;
    private bool useModeledMethods;
    private long instructionLimit = RootRelativeEvaluationOptions.DefaultInstructionLimit;
    private int logicalDepthLimit = RootRelativeEvaluationOptions.DefaultLogicalDepthLimit;
    private int traversalLimit = ExpressionEvaluationService.MaximumTraversalUnits;
    private CallStackFrameNode? contextFrame;
    private RootSelection? rootSelection;
    private EvaluationReport? report;

    /// <summary>Creates the evaluation pane.</summary>
    /// <param name="shell">The shell services used for serialized session access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public EvaluateViewModel(IShellServices shell)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        evaluateCommand = new RelayCommand(() => _ = EvaluateAsync(), CanEvaluate);
        clearContextCommand = new RelayCommand(() => ContextFrame = null, () => contextFrame is not null);
        clearRootCommand = new RelayCommand(() => RootSelection = null, () => rootSelection is not null);
        promoteRootCommand = new RelayCommand(
            () =>
            {
                if (report?.PromotableRoot is { } promoted)
                {
                    AdoptRoot(promoted);
                }
            },
            () => report?.PromotableRoot is not null);
        clearHistoryCommand = new RelayCommand(History.Clear, () => History.Count > 0);
        GroupedReportFacts = new PropertyGroupList(ReportFacts);
        RefreshSamples();
    }

    /// <summary>Gets the displayed report's binding and evidence facts.</summary>
    public ObservableCollection<PropertyRow> ReportFacts { get; } = [];

    /// <summary>Gets the displayed report's facts grouped by pipeline stage.</summary>
    public PropertyGroupList GroupedReportFacts { get; }

    /// <summary>Gets the raw memory reads retained by the displayed report, in execution order.</summary>
    public ObservableCollection<MemoryReadRow> ReportMemoryReads { get; } = [];

    /// <summary>Gets the deterministic bounds the displayed report actually reached.</summary>
    public ObservableCollection<BoundRow> ReportBounds { get; } = [];

    /// <summary>Gets the stable diagnostics explaining the displayed report's outcome.</summary>
    public ObservableCollection<DiagnosticRow> ReportDiagnostics { get; } = [];

    /// <summary>Gets the reports produced during this session, most recent first.</summary>
    public ObservableCollection<EvaluationReport> History { get; } = [];

    /// <summary>Gets the ready-made expression shapes offered for the currently selected entry point.</summary>
    public ObservableCollection<ExpressionSample> Samples { get; } = [];

    /// <summary>Gets the command that evaluates <see cref="Expression"/> through the selected entry point.</summary>
    public RelayCommand EvaluateCommand => evaluateCommand;

    /// <summary>Gets the command that drops the adopted selected-frame name context.</summary>
    public RelayCommand ClearContextCommand => clearContextCommand;

    /// <summary>Gets the command that drops the adopted expression root.</summary>
    public RelayCommand ClearRootCommand => clearRootCommand;

    /// <summary>Gets the command that clears the evaluation history.</summary>
    public RelayCommand ClearHistoryCommand => clearHistoryCommand;

    /// <summary>Gets the command that adopts the last static-field object value as the expression root.</summary>
    public RelayCommand PromoteResultToRootCommand => promoteRootCommand;

    /// <summary>Gets whether the displayed static-field result carries an object that can become a root.</summary>
    public bool CanPromoteResultToRoot => report?.PromotableRoot is not null;

    /// <summary>Gets or sets which implemented entry point to invoke.</summary>
    public ExpressionPath Path
    {
        get => path;
        set
        {
            if (!Set(ref path, value))
            {
                return;
            }

            Raise(nameof(IsStaticFieldPath));
            Raise(nameof(IsRootRelativePath));
            Raise(nameof(ReadinessMessage));
            RefreshSamples();
            evaluateCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Gets or sets whether the static-field entry point is selected.</summary>
    public bool IsStaticFieldPath
    {
        get => path == ExpressionPath.StaticField;
        set
        {
            if (value)
            {
                Path = ExpressionPath.StaticField;
            }
        }
    }

    /// <summary>Gets or sets whether the root-relative entry point is selected.</summary>
    public bool IsRootRelativePath
    {
        get => path == ExpressionPath.RootRelative;
        set
        {
            if (value)
            {
                Path = ExpressionPath.RootRelative;
            }
        }
    }

    /// <summary>Gets or sets the raw expression text, which is submitted without normalization.</summary>
    public string Expression
    {
        get => expression;
        set
        {
            if (Set(ref expression, value))
            {
                evaluateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets or sets newline-separated Portable-PDB candidate paths offered to contextual name binding.</summary>
    public string PortablePdbCandidates
    {
        get => portablePdbCandidates;
        set => Set(ref portablePdbCandidates, value);
    }

    /// <summary>Gets or sets the case-sensitive identifier the root-relative expression uses for its root.</summary>
    public string RootIdentifier
    {
        get => rootIdentifier;
        set
        {
            if (Set(ref rootIdentifier, value))
            {
                Raise(nameof(ReadinessMessage));
                evaluateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets or sets whether the opt-in member-chain grammar of unlimited depth is admitted.</summary>
    public bool AdmitMemberChain
    {
        get => admitMemberChain;
        set => Set(ref admitMemberChain, value);
    }

    /// <summary>Gets or sets whether an admitted method call replaces its helper with an exact versioned pure model.</summary>
    public bool UseModeledMethods
    {
        get => useModeledMethods;
        set => Set(ref useModeledMethods, value);
    }

    /// <summary>Gets or sets the interpreter instruction-unit budget.</summary>
    public long InstructionLimit
    {
        get => instructionLimit;
        set => Set(ref instructionLimit, Math.Max(0, value));
    }

    /// <summary>Gets or sets the logical call-depth budget.</summary>
    public int LogicalDepthLimit
    {
        get => logicalDepthLimit;
        set => Set(ref logicalDepthLimit, Math.Clamp(value, 0, ExpressionEvaluationService.MaximumLogicalCallDepth));
    }

    /// <summary>Gets a caption stating the hard caps the product enforces on the configurable budgets.</summary>
    public string LimitsCaption =>
        $"Call depth is capped at {ExpressionEvaluationService.MaximumLogicalCallDepth} and traversal at "
        + $"{ExpressionEvaluationService.MaximumTraversalUnits} units by the product, independently of this pane.";

    /// <summary>Gets or sets the graph-preparation traversal budget.</summary>
    public int TraversalLimit
    {
        get => traversalLimit;
        set => Set(ref traversalLimit, Math.Clamp(value, 0, ExpressionEvaluationService.MaximumTraversalUnits));
    }

    /// <summary>Gets the frame adopted as name context, or null when only fully qualified names may bind.</summary>
    public CallStackFrameNode? ContextFrame
    {
        get => contextFrame;
        private set
        {
            if (!Set(ref contextFrame, value))
            {
                return;
            }

            Raise(nameof(ContextDescription));
            Raise(nameof(HasContextFrame));
            Raise(nameof(ReadinessMessage));
            clearContextCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Gets whether a selected frame is currently adopted as name context.</summary>
    public bool HasContextFrame => contextFrame is not null;

    /// <summary>Gets a description of the adopted name context.</summary>
    public string ContextDescription => contextFrame is { Frame: { } frame }
        ? $"Thread #{contextFrame.Selector.ThreadOrdinal}, frame #{contextFrame.Selector.FrameOrdinal} — "
          + $"{(string.IsNullOrEmpty(frame.DeclaringNamespace) ? "<global namespace>" : frame.DeclaringNamespace)}, "
          + $"MethodDef {DisplayFormatting.Token(frame.MethodDefinitionToken)}"
        : "No frame adopted. Only context-independent fully qualified names can bind.";

    /// <summary>Gets the root adopted for the root-relative path, or null when none is adopted.</summary>
    public RootSelection? RootSelection
    {
        get => rootSelection;
        private set
        {
            if (!Set(ref rootSelection, value))
            {
                return;
            }

            Raise(nameof(RootDescription));
            Raise(nameof(HasRootObject));
            Raise(nameof(ReadinessMessage));
            clearRootCommand.RaiseCanExecuteChanged();
            evaluateCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Gets whether an object is currently adopted as the expression root.</summary>
    public bool HasRootObject => rootSelection is not null;

    /// <summary>Gets a description of the adopted expression root.</summary>
    public string RootDescription => rootSelection?.Description
        ?? "No root adopted. Select a heap object, or evaluate a static field whose value is an object reference.";

    /// <summary>Gets the report currently displayed, or null before the first evaluation.</summary>
    public EvaluationReport? Report
    {
        get => report;
        set
        {
            if (!Set(ref report, value))
            {
                return;
            }

            Raise(nameof(HasReport));
            Raise(nameof(CanPromoteResultToRoot));
            promoteRootCommand.RaiseCanExecuteChanged();
            ApplyReportDetails(value);
        }
    }

    /// <summary>Gets whether a report is currently displayed.</summary>
    public bool HasReport => report is not null;

    /// <summary>Gets a statement of what, if anything, still blocks evaluation.</summary>
    public string ReadinessMessage
    {
        get
        {
            if (!shell.IsDumpOpen)
            {
                return "Open a dump to evaluate expressions.";
            }

            if (path == ExpressionPath.RootRelative)
            {
                if (rootSelection is null)
                {
                    return "The root-relative path needs one exact heap object as its root.";
                }

                if (string.IsNullOrWhiteSpace(rootIdentifier))
                {
                    return "Supply the identifier that the expression uses for the root.";
                }

                return $"Ready. Write the expression using '{rootIdentifier.Trim()}' as the root identifier.";
            }

            return contextFrame is null
                ? "Ready for context-independent fully qualified static-field names."
                : "Ready. Contextual names may bind through the adopted frame's namespace, import, and alias facts.";
        }
    }

    /// <summary>Resets adopted context and history to match a newly opened or closed dump.</summary>
    public void Reset()
    {
        ContextFrame = null;
        RootSelection = null;
        Report = null;
        History.Clear();
        RefreshSamples();
        Raise(nameof(ReadinessMessage));
        evaluateCommand.RaiseCanExecuteChanged();
        clearHistoryCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Adopts one exact frame as the name-binding context of the static-field path.</summary>
    /// <param name="frame">The frame to adopt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
    public void AdoptContext(CallStackFrameNode frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ContextFrame = frame;
        Path = ExpressionPath.StaticField;
        Raise(nameof(ReadinessMessage));
    }

    /// <summary>Adopts one exact object as the root of the root-relative path.</summary>
    /// <param name="root">The selected root to adopt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    public void AdoptRoot(RootSelection root)
    {
        ArgumentNullException.ThrowIfNull(root);
        RootSelection = root;
        Path = ExpressionPath.RootRelative;
    }

    /// <summary>Replaces the editor content with a ready-made template.</summary>
    /// <param name="sample">The template to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sample"/> is null.</exception>
    public void ApplySample(ExpressionSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        Expression = sample.Text;
    }

    private void ApplyReportDetails(EvaluationReport? value)
    {
        ReportFacts.Clear();
        ReportMemoryReads.Clear();
        ReportBounds.Clear();
        ReportDiagnostics.Clear();
        if (value is null)
        {
            return;
        }

        foreach (var fact in value.Facts)
        {
            ReportFacts.Add(fact);
        }

        foreach (var read in value.MemoryReads)
        {
            ReportMemoryReads.Add(read);
        }

        foreach (var bound in value.Bounds)
        {
            ReportBounds.Add(bound);
        }

        foreach (var diagnostic in value.Diagnostics)
        {
            ReportDiagnostics.Add(diagnostic);
        }
    }

    private void RefreshSamples()
    {
        Samples.Clear();
        if (path == ExpressionPath.StaticField)
        {
            Samples.Add(new ExpressionSample(
                "Fully qualified static field",
                "MyCompany.MyApp.Diagnostics.RequestCounters.Total",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Explicit global qualifier",
                "global::MyCompany.MyApp.Diagnostics.RequestCounters.LastError",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Static field plus one member",
                "MyCompany.MyApp.AppState.Current.Name",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Contextual name (needs a frame)",
                "RequestCounters.Total",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Enum literal",
                "System.DayOfWeek.Monday",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Constant arithmetic",
                "(2 + 3) * 4",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "String API",
                "(\"post\" + \"-\" + \"mortem\").ToUpperInvariant()",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Interpolated string",
                "$\"total {MyCompany.MyApp.Diagnostics.RequestCounters.Total:N0}\"",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Switch expression",
                "MyCompany.MyApp.Diagnostics.RequestCounters.Total switch { > 1000 => \"hot\", > 0 => \"warm\", _ => \"idle\" }",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Pattern test",
                "MyCompany.MyApp.Diagnostics.RequestCounters.Total is > 0 and < 100000",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Lambda pipeline",
                "Enumerable.Range(1, 10).Where(x => x % 2 == 1).Select(x => x * x).Sum()",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Date arithmetic",
                "new DateTime(2026, 7, 30) + TimeSpan.FromHours(6)",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Guid and Version",
                "new Version(10, 0, 26300) > Version.Parse(\"10.0\")",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Enum API",
                "Enum.GetNames(typeof(System.ConsoleColor)).Count(name => name.StartsWith(\"Dark\", StringComparison.Ordinal))",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Array API",
                "Array.BinarySearch([1, .. new[] { 3, 5 }, 7], 5)",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Type API",
                "typeof(System.DayOfWeek).GetEnumUnderlyingType().MakeArrayType().FullName",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Type relationships",
                "typeof(IEnumerable<object>).IsAssignableFrom(typeof(string[]))",
                ExpressionPath.StaticField));
            Samples.Add(new ExpressionSample(
                "Generic construction",
                "typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(List<int>)) == typeof(Dictionary<string, List<int>>)",
                ExpressionPath.StaticField));
            return;
        }

        var identifier = string.IsNullOrWhiteSpace(rootIdentifier) ? "root" : rootIdentifier.Trim();
        Samples.Add(new ExpressionSample("Direct field", $"{identifier}.Name", ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "Null-coalescing literal",
            $"{identifier}.Name ?? \"unset\"",
            ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "Member chain (any depth)",
            $"{identifier}.Child.Route.Corridor.Name",
            ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "Conditional member chain",
            $"{identifier}.Child?.Owner?.Name",
            ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "Admitted parameterless method",
            $"{identifier}.GetMarkerSummary()",
            ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "Interpolated summary",
            $"$\"name {{{identifier}.Name}} ({{{identifier}.Name.Length}} chars)\"",
            ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "Lambda over a dump array",
            $"{identifier}.RecentDispatchDurationsMs.Where(ms => ms > 1000).Select(ms => ms / 1000.0).ToArray()",
            ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "TimeSpan from a dump value",
            $"TimeSpan.FromMilliseconds({identifier}.RecentDispatchDurationsMs.Max())",
            ExpressionPath.RootRelative));
        Samples.Add(new ExpressionSample(
            "Pattern over a dump value",
            $"{identifier}.Child?.Name is not null",
            ExpressionPath.RootRelative));
    }

    private bool CanEvaluate()
    {
        if (!shell.IsDumpOpen || string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        return path != ExpressionPath.RootRelative
            || (rootSelection is not null && !string.IsNullOrWhiteSpace(rootIdentifier));
    }

    private async Task EvaluateAsync()
    {
        var submitted = expression;
        EvaluationReport? produced;
        if (path == ExpressionPath.StaticField)
        {
            var selector = contextFrame?.Selector;
            // The shell probes hint-derived Portable-PDB candidates automatically, exactly as the source view does;
            // only the console host makes that probe an explicit command ('pdb auto'). Identity is still validated
            // per candidate before any name binds through it.
            var explicitCandidates = SourceNavigationService.ParseCandidateList(portablePdbCandidates);
            produced = await shell.RunAsync(
                "Evaluating static-field expression…",
                session => ExpressionEvaluationService.EvaluateStaticField(
                    session,
                    submitted,
                    selector,
                    SourceNavigationService.AssemblePortablePdbCandidates(
                        session, explicitCandidates))).ConfigureAwait(true);
        }
        else
        {
            var root = rootSelection!;
            var identifier = rootIdentifier.Trim();
            var options = new RootRelativeEvaluationOptions
            {
                UseModeledMethods = useModeledMethods,
                AdmitMemberChain = admitMemberChain,
                InstructionLimit = instructionLimit,
                LogicalDepthLimit = logicalDepthLimit,
                TraversalLimit = traversalLimit,
            };
            produced = await shell.RunAsync(
                "Evaluating root-relative expression…",
                session => ExpressionEvaluationService.EvaluateRootRelative(
                    session,
                    submitted,
                    root,
                    identifier,
                    options)).ConfigureAwait(true);
        }

        if (produced is null)
        {
            return;
        }

        Report = produced;
        History.Insert(0, produced);
        while (History.Count > 50)
        {
            History.RemoveAt(History.Count - 1);
        }

        clearHistoryCommand.RaiseCanExecuteChanged();
        shell.SetStatus($"{produced.Path}: {produced.Status} — {produced.Stage}");
    }
}
