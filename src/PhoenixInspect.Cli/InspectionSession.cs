using System.Collections.Immutable;
using System.Globalization;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpDebugging;

namespace PhoenixInspect.Cli;

/// <summary>Classifies how one submitted command line ended.</summary>
public enum CommandOutcome
{
    /// <summary>The command ran and the session should continue.</summary>
    Continue,

    /// <summary>The command was rejected or could not run; a scripted run treats this as a failure.</summary>
    Failed,

    /// <summary>The caller asked to end the session.</summary>
    Exit,
}

/// <summary>
/// Holds the state of one console inspection session and executes the commands that drive it.
/// </summary>
/// <remarks>
/// <para>
/// The session deliberately mirrors a debugger's shape — modules, threads and frames, rooted objects, and an
/// expression evaluator that answers against them — because that is the shape a post-mortem user already thinks in.
/// The resemblance stops at the evidence boundary: nothing here resumes, mutates, or infers past execution. Every
/// value is read from the snapshot, and an answer that the snapshot cannot support is reported as the typed
/// non-exact outcome the product produced rather than being filled in.
/// </para>
/// <para>
/// State that a later command depends on — the adopted expression root, the frame supplying name context, the
/// Portable-PDB candidates — is explicit and inspectable through <c>status</c>, so a transcript can always be read
/// back to see which evidence an answer was allowed to use.
/// </para>
/// </remarks>
public sealed class InspectionSession
{
    private const int DefaultThreadProbe = 24;
    private const int DefaultFrameDepth = 32;
    private const int DefaultMatchCap = 32;
    private const int HandleScanCap = ClrmdDumpSession.MaximumHandleScanCount;

    private readonly DumpSessionHost host;
    private readonly ConsoleRenderer renderer;

    private ImmutableArray<HeapObjectRow> lastSearch = [];
    private ImmutableArray<string> portablePdbCandidates = [];
    private CallStackFrameNode? contextFrame;
    private RootSelection? root;
    private string rootIdentifier = "root";
    private ImmutableArray<ModuleRow> modules = [];

    /// <summary>Creates a session over an already-open dump host.</summary>
    /// <param name="host">The host owning the open snapshot and its dedicated adapter thread.</param>
    /// <param name="renderer">The renderer that writes every line of session output.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public InspectionSession(DumpSessionHost host, ConsoleRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(renderer);
        this.host = host;
        this.renderer = renderer;
    }

    /// <summary>Gets or sets whether every evaluation also prints its complete evidence.</summary>
    public bool Verbose { get; set; }

    /// <summary>Gets the number of evaluations this session has produced.</summary>
    public int EvaluationCount { get; private set; }

    /// <summary>Gets the number of evaluations that did not reach an exact or exhaustively absent answer.</summary>
    public int NonExactEvaluationCount { get; private set; }

    /// <summary>Gets a short description of the current context for the interactive prompt.</summary>
    public string PromptContext =>
        root is null ? "phoenix" : $"phoenix {rootIdentifier}";

    /// <summary>Writes the snapshot overview.</summary>
    /// <returns>A task that completes when the overview has been written.</returns>
    public async Task ShowOverviewAsync()
    {
        var path = host.DumpPath!;
        var length = host.DumpLength ?? 0;
        var snapshot = await host.QueryAsync(session =>
            DumpInspectionService.LoadSnapshot(session, path, length)).ConfigureAwait(false);
        modules = snapshot.Modules;
        renderer.Heading("Session");
        renderer.Properties(snapshot.Properties, includeDetail: true);
    }

    /// <summary>Executes one command line.</summary>
    /// <param name="line">The raw command line, which may be blank or a <c>#</c> comment.</param>
    /// <returns>How the session should proceed.</returns>
    public async Task<CommandOutcome> ExecuteAsync(string line)
    {
        var text = (line ?? string.Empty).Trim();
        if (text.Length == 0 || text.StartsWith('#'))
        {
            return CommandOutcome.Continue;
        }

        var (verb, rest) = Split(text);
        try
        {
            return verb switch
            {
                "help" or "?" when rest.Length == 0 => ShowHelp(),
                "exit" or "quit" or "q" => CommandOutcome.Exit,
                "info" or "overview" => await RunOverviewAsync().ConfigureAwait(false),
                "status" => ShowStatus(),
                "modules" => await ShowModulesAsync(rest).ConfigureAwait(false),
                "module" => await ShowModuleAsync(rest).ConfigureAwait(false),
                "threads" => await ShowThreadsAsync(rest).ConfigureAwait(false),
                "frames" => await ShowFramesAsync(rest).ConfigureAwait(false),
                "context" => await SetContextAsync(rest).ConfigureAwait(false),
                "pdb" => await SetPortablePdbCandidatesAsync(rest).ConfigureAwait(false),
                "objects" => await SearchObjectsAsync(rest).ConfigureAwait(false),
                "root" => await SetRootAsync(rest).ConfigureAwait(false),
                "as" => SetRootIdentifier(rest),
                "verbose" => SetVerbose(rest),
                "echo" => Echo(rest),
                "eval" or "?" => await EvaluateAsync(rest).ConfigureAwait(false),
                _ => await EvaluateAsync(text).ConfigureAwait(false),
            };
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            // A command that cannot run must end as a reported failure, never as a lost session. The dump stays open
            // and the next command still runs against the same snapshot.
            renderer.Error($"{exception.GetType().Name}: {exception.Message}");
            return CommandOutcome.Failed;
        }
    }

    private static (string Verb, string Argument) Split(string text)
    {
        if (text.StartsWith('?'))
        {
            return ("?", text[1..].Trim());
        }

        var index = text.IndexOf(' ', StringComparison.Ordinal);
        return index < 0
            ? (text.ToLowerInvariant(), string.Empty)
            : (text[..index].ToLowerInvariant(), text[(index + 1)..].Trim());
    }

    private CommandOutcome ShowHelp()
    {
        renderer.Heading("Commands");
        renderer.Pair("info", "Snapshot identity, target facts, declared bounds, and the expression front end.");
        renderer.Pair("status", "The evidence this session is currently allowed to use.");
        renderer.Pair("modules [substring]", "Managed module instances, optionally filtered by name.");
        renderer.Pair("module <index>", "Counted metadata content identity of one module, read from dump memory.");
        renderer.Pair("threads [count] [depth]", "Probe thread ordinals and show up to depth managed frames of each.");
        renderer.Pair("frames <thread> [count]", "Managed frames of one thread, in stack order.");
        renderer.Pair("context <thread> <frame>", "Adopt a frame's namespace, import, and alias facts for name binding.");
        renderer.Pair("context <method>", "Adopt the first probed frame whose method name contains that text.");
        renderer.Pair("context none", "Require context-independent fully qualified names again.");
        renderer.Pair("pdb <path>", "Offer a Portable-PDB candidate; 'pdb clear' withdraws all candidates.");
        renderer.Pair("pdb auto", "Probe paths derived from target-side module hints on this machine.");
        renderer.Pair("objects <TypeName> [max]", "Bounded strong-handle search over an exact ordinal type name.");
        renderer.Pair("root <index>", "Adopt a search match as the expression root.");
        renderer.Pair("root <expression>", "Adopt the object value of a static-field expression as the root.");
        renderer.Pair("root none", "Drop the adopted root.");
        renderer.Pair("as <identifier>", "Rename the root identifier used in root-relative expressions.");
        renderer.Pair("verbose on|off", "Print the complete evidence behind every answer.");
        renderer.Pair("eval <expression>", "Evaluate a C# expression; a bare expression is also accepted.");
        renderer.Pair("exit", "End the session.");

        renderer.Heading("Expression routing");
        renderer.Note(
            "  An expression whose first identifier is the current root identifier is evaluated against that object.");
        renderer.Note(
            "  Every other expression is treated as a static-field expression and bound from module metadata.");
        return CommandOutcome.Continue;
    }

    private CommandOutcome ShowStatus()
    {
        renderer.Heading("Session state");
        renderer.Pair("Dump", host.DumpPath ?? "<none>");
        renderer.Pair("Root", root?.Description ?? "<none adopted>");
        renderer.Pair("Root identifier", rootIdentifier);
        renderer.Pair(
            "Name context",
            contextFrame is null
                ? "<none: fully qualified names only>"
                : $"thread #{contextFrame.Selector.ThreadOrdinal}, frame #{contextFrame.Selector.FrameOrdinal}");
        renderer.Pair(
            "Portable-PDB candidates",
            portablePdbCandidates.IsDefaultOrEmpty ? "<none offered>" : string.Join(", ", portablePdbCandidates));
        renderer.Pair("Verbose evidence", Verbose ? "on" : "off");
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> RunOverviewAsync()
    {
        await ShowOverviewAsync().ConfigureAwait(false);
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> ShowModulesAsync(string filter)
    {
        if (modules.IsDefaultOrEmpty)
        {
            modules = await host.QueryAsync(DumpInspectionService.DescribeModules).ConfigureAwait(false);
        }

        var selected = filter.Length == 0
            ? modules
            : [.. modules.Where(row => row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))];

        renderer.Heading($"Managed module instances ({selected.Length} of {modules.Length})");
        if (selected.Length == 0)
        {
            renderer.Note("  No module instance name contains that text.");
            return CommandOutcome.Continue;
        }

        renderer.Table(
            ["#", "name", "domain", "metadata root", "metadata length", "layout"],
            [.. selected.Select(row => new[]
            {
                modules.IndexOf(row).ToString(CultureInfo.InvariantCulture),
                row.Name,
                row.AppDomainId.ToString(CultureInfo.InvariantCulture),
                row.MetadataAddress,
                row.MetadataLength,
                row.Layout,
            })]);
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> ShowModuleAsync(string argument)
    {
        if (modules.IsDefaultOrEmpty)
        {
            modules = await host.QueryAsync(DumpInspectionService.DescribeModules).ConfigureAwait(false);
        }

        if (!TryParseIndex(argument, modules.Length, out var index))
        {
            renderer.Error($"expected a module index between 0 and {modules.Length - 1}.");
            return CommandOutcome.Failed;
        }

        var module = modules[index].Module;
        var rows = await host.QueryAsync(session =>
            DumpInspectionService.DescribeModuleContent(session, module)).ConfigureAwait(false);
        renderer.Heading($"Module #{index}: {modules[index].Name}");
        renderer.Properties(rows, includeDetail: true);
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> ShowThreadsAsync(string argument)
    {
        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var probe = parts.Length > 0 ? ParseCount(parts[0], DefaultThreadProbe) : DefaultThreadProbe;
        var depth = parts.Length > 1 ? ParseCount(parts[1], 1) : 1;
        var projection = await host.QueryAsync(session =>
        {
            var probed = DumpInspectionService.ProbeCallStacks(session, probe);
            if (depth > 1)
            {
                foreach (var thread in probed.Threads)
                {
                    foreach (var frame in DumpInspectionService.LoadFrames(session, thread, depth))
                    {
                        thread.Frames.Add(frame);
                    }
                }
            }

            return probed;
        }).ConfigureAwait(false);

        renderer.Heading("Managed threads");
        renderer.Note("  " + projection.Summary);
        if (projection.Threads.Length != 0)
        {
            renderer.Line();
            foreach (var thread in projection.Threads)
            {
                renderer.Line($"  {thread.Header}");
                foreach (var frame in thread.Frames)
                {
                    renderer.Line($"      {frame.Header}");
                    renderer.Note($"        {frame.Detail}");
                }
            }
        }

        renderer.Line();
        renderer.Note("  " + projection.Note);
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> ShowFramesAsync(string argument)
    {
        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var threadOrdinal))
        {
            renderer.Error("usage: frames <threadOrdinal> [count]");
            return CommandOutcome.Failed;
        }

        var depth = parts.Length > 1 ? ParseCount(parts[1], DefaultFrameDepth) : DefaultFrameDepth;
        var frames = await host.QueryAsync(session =>
        {
            var top = DumpSelectedFrameSelector.Create(session.Snapshot, threadOrdinal, 0);
            var observation = session.SelectExpressionFrame(top);
            var collected = ImmutableArray.CreateBuilder<CallStackFrameNode>();
            if (observation.Frame is not null)
            {
                var node = new CallStackThreadNode(threadOrdinal, $"Thread #{threadOrdinal}", string.Empty);
                collected.Add(DumpInspectionService.CreateFrameNode(session, top, observation));
                collected.AddRange(DumpInspectionService.LoadFrames(session, node, depth));
            }

            return collected.ToImmutable();
        }).ConfigureAwait(false);

        renderer.Heading($"Thread #{threadOrdinal} managed frames");
        if (frames.Length == 0)
        {
            renderer.Note(
                "  No managed frame 0 was observed. The adapter returns the same typed unavailable observation for a "
                + "past-the-end ordinal and for a live thread with no managed frames.");
            return CommandOutcome.Continue;
        }

        foreach (var frame in frames)
        {
            renderer.Line($"  {frame.Header}");
            renderer.Note($"    {frame.Detail}");
        }

        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> SetContextAsync(string argument)
    {
        if (argument is "none" or "off" or "clear")
        {
            contextFrame = null;
            renderer.Note("  Name context withdrawn; only context-independent fully qualified names bind.");
            return CommandOutcome.Continue;
        }

        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var threadOrdinal = 0;
        var frameOrdinal = 0;
        var byOrdinal =
            parts.Length == 2 &&
            int.TryParse(parts[0], CultureInfo.InvariantCulture, out threadOrdinal) &&
            int.TryParse(parts[1], CultureInfo.InvariantCulture, out frameOrdinal);
        if (!byOrdinal && argument.Length == 0)
        {
            renderer.Error("usage: context <threadOrdinal> <frameOrdinal>  |  context <method>  |  context none");
            return CommandOutcome.Failed;
        }

        // Thread ordinals are not stable between runs of the same program, so a frame can also be named. Naming is
        // the useful form in a script: "the frame that was running Program.Main" survives a re-run, "thread 2 frame
        // 4" does not.
        var node = byOrdinal
            ? await SelectFrameByOrdinalAsync(threadOrdinal, frameOrdinal).ConfigureAwait(false)
            : await SelectFrameByMethodNameAsync(argument).ConfigureAwait(false);

        if (node is null)
        {
            renderer.Error(byOrdinal
                ? $"thread #{threadOrdinal} frame #{frameOrdinal} produced no exact managed frame."
                : $"no probed managed frame's method name contains '{argument}'.");
            return CommandOutcome.Failed;
        }

        contextFrame = node;
        renderer.Note($"  Name context is now {node.Header}.");
        return CommandOutcome.Continue;
    }

    private Task<CallStackFrameNode?> SelectFrameByOrdinalAsync(int threadOrdinal, int frameOrdinal) =>
        host.QueryAsync(session =>
        {
            var selector = DumpSelectedFrameSelector.Create(session.Snapshot, threadOrdinal, frameOrdinal);
            var observation = session.SelectExpressionFrame(selector);
            return observation.Frame is null
                ? null
                : DumpInspectionService.CreateFrameNode(session, selector, observation);
        });

    private Task<CallStackFrameNode?> SelectFrameByMethodNameAsync(string methodName) =>
        host.QueryAsync(CallStackFrameNode? (session) =>
        {
            var probed = DumpInspectionService.ProbeCallStacks(session, DefaultThreadProbe);
            foreach (var thread in probed.Threads)
            {
                foreach (var frame in DumpInspectionService.LoadFrames(session, thread, DefaultFrameDepth))
                {
                    thread.Frames.Add(frame);
                }

                foreach (var frame in thread.Frames)
                {
                    if (frame.Frame is { } identity &&
                        session.DescribeFrameMethod(identity).Value is { } method &&
                        method.DisplayName.Contains(methodName, StringComparison.Ordinal))
                    {
                        return frame;
                    }
                }
            }

            return null;
        });

    private async Task<CommandOutcome> SetPortablePdbCandidatesAsync(string argument)
    {
        if (argument is "clear" or "none")
        {
            portablePdbCandidates = [];
            renderer.Note("  Portable-PDB candidates withdrawn.");
            return CommandOutcome.Continue;
        }

        if (argument is "auto")
        {
            return await OfferDiscoveredPortablePdbsAsync().ConfigureAwait(false);
        }

        if (argument.Length == 0)
        {
            renderer.Error("usage: pdb <path>  |  pdb auto  |  pdb clear");
            return CommandOutcome.Failed;
        }

        portablePdbCandidates = portablePdbCandidates.Add(argument);
        renderer.Note($"  Offered {argument} as a Portable-PDB candidate; identity is still validated before use.");
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> OfferDiscoveredPortablePdbsAsync()
    {
        // Target path hints are target-side strings, not identity. This command says out loud that it is probing
        // paths derived from them on the analysis machine, and offering a file changes nothing on its own: the
        // product still validates a candidate's identity against the module before any name binds through it.
        var hints = await host.QueryAsync(static session => session.Modules
            .Select(static module => module.TargetPathHint)
            .Where(static hint => !string.IsNullOrEmpty(hint))
            .Select(static hint => Path.ChangeExtension(hint!, ".pdb"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray()).ConfigureAwait(false);

        var discovered = hints.Where(File.Exists).ToImmutableArray();
        renderer.Note(
            $"  Probed {hints.Length} path(s) derived from target-side module hints on this machine; "
            + $"{discovered.Length} exist.");
        if (discovered.Length == 0)
        {
            renderer.Note("  No candidate was offered. Supply one explicitly with 'pdb <path>'.");
            return CommandOutcome.Continue;
        }

        foreach (var candidate in discovered)
        {
            renderer.Note($"    offered  {candidate}");
        }

        portablePdbCandidates = portablePdbCandidates.AddRange(discovered);
        renderer.Note("  A target path hint is not identity; each candidate is still validated before use.");
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> SearchObjectsAsync(string argument)
    {
        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            renderer.Error("usage: objects <ExactTypeName> [maxMatches]");
            return CommandOutcome.Failed;
        }

        var typeName = parts[0];
        var requested = parts.Length > 1 ? ParseCount(parts[1], DefaultMatchCap) : DefaultMatchCap;
        var cap = Math.Min(requested, ClrmdDumpSession.MaximumHandleMatches);
        if (cap != requested)
        {
            renderer.Note(
                $"  The adapter retains at most {ClrmdDumpSession.MaximumHandleMatches:N0} matches; using that cap.");
        }

        var projection = await host.QueryAsync(session =>
            DumpInspectionService.SearchObjects(session, typeName, cap, HandleScanCap)).ConfigureAwait(false);
        lastSearch = projection.Rows;

        renderer.Heading($"Strong-handle search: {typeName}");
        renderer.Note("  " + projection.Summary);
        if (projection.Rows.Length != 0)
        {
            renderer.Line();
            renderer.Table(
                ["#", "type", "address", "method table", "rooted by", "module"],
                [.. projection.Rows.Select((row, index) => new[]
                {
                    index.ToString(CultureInfo.InvariantCulture),
                    row.TypeName,
                    row.Address,
                    row.MethodTable,
                    row.RootKind,
                    row.ModuleName,
                })]);
            renderer.Line();
            renderer.Note("  Adopt one as the expression root with 'root <#>'.");
        }

        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> SetRootAsync(string argument)
    {
        if (argument is "none" or "off" or "clear")
        {
            root = null;
            renderer.Note("  Expression root dropped.");
            return CommandOutcome.Continue;
        }

        if (argument.Length == 0)
        {
            renderer.Error("usage: root <searchIndex>  |  root <staticFieldExpression>  |  root none");
            return CommandOutcome.Failed;
        }

        if (TryParseIndex(argument, lastSearch.Length, out var index))
        {
            root = RootSelection.FromHandleObject(lastSearch[index]);
            renderer.Note($"  Root is {root.Description}; refer to it as '{rootIdentifier}'.");
            return CommandOutcome.Continue;
        }

        var report = await EvaluateStaticFieldAsync(argument).ConfigureAwait(false);
        RecordEvaluation(report);
        renderer.ResultHeadline(report);
        if (Verbose)
        {
            renderer.ResultEvidence(report);
        }

        if (report.PromotableRoot is not { } promoted)
        {
            renderer.Error(
                "that expression did not produce a validated object reference, so it cannot become an expression root.");
            return CommandOutcome.Failed;
        }

        root = promoted;
        renderer.Line();
        renderer.Note($"  Root is {root.Description}; refer to it as '{rootIdentifier}'.");
        return CommandOutcome.Continue;
    }

    private CommandOutcome SetRootIdentifier(string argument)
    {
        var identifier = argument.Trim();
        if (identifier.Length == 0 || identifier.Contains(' ', StringComparison.Ordinal))
        {
            renderer.Error("usage: as <identifier>");
            return CommandOutcome.Failed;
        }

        rootIdentifier = identifier;
        renderer.Note($"  Root-relative expressions now spell the root '{rootIdentifier}'.");
        return CommandOutcome.Continue;
    }

    private CommandOutcome SetVerbose(string argument)
    {
        switch (argument)
        {
            case "on":
                Verbose = true;
                break;
            case "off":
                Verbose = false;
                break;
            default:
                renderer.Error("usage: verbose on|off");
                return CommandOutcome.Failed;
        }

        renderer.Note($"  Complete evidence is {(Verbose ? "shown" : "hidden")} for each answer.");
        return CommandOutcome.Continue;
    }

    private CommandOutcome Echo(string argument)
    {
        renderer.Line();
        renderer.Line(argument);
        return CommandOutcome.Continue;
    }

    private async Task<CommandOutcome> EvaluateAsync(string expression)
    {
        if (expression.Length == 0)
        {
            renderer.Error("usage: eval <expression>");
            return CommandOutcome.Failed;
        }

        var report = UsesRoot(expression)
            ? await EvaluateRootRelativeAsync(expression).ConfigureAwait(false)
            : await EvaluateStaticFieldAsync(expression).ConfigureAwait(false);

        RecordEvaluation(report);
        renderer.ResultHeadline(report);
        if (Verbose)
        {
            renderer.ResultEvidence(report);
        }

        return CommandOutcome.Continue;
    }

    private Task<EvaluationReport> EvaluateStaticFieldAsync(string expression)
    {
        var selector = contextFrame?.Selector;
        var candidates = portablePdbCandidates;
        return host.QueryAsync(session =>
            ExpressionEvaluationService.EvaluateStaticField(session, expression, selector, candidates));
    }

    private Task<EvaluationReport> EvaluateRootRelativeAsync(string expression)
    {
        var selected = root!;
        var identifier = rootIdentifier;
        var policy = DumpExpressionPolicy.Create(
            DumpMethodEvaluationMode.Interpreted,
            instructionLimit: 100_000,
            logicalDepthLimit: 8,
            traversalLimit: CounterfactualMethodRequest.MaximumTraversalUnits);
        return host.QueryAsync(session => ExpressionEvaluationService.EvaluateRootRelative(
            session,
            expression,
            selected,
            identifier,
            policy,
            DumpExpressionLanguageProfile.MemberChainV2));
    }

    private void RecordEvaluation(EvaluationReport report)
    {
        EvaluationCount++;
        if (report.Severity is not (EvaluationSeverity.Exact or EvaluationSeverity.Absent))
        {
            NonExactEvaluationCount++;
        }
    }

    private bool UsesRoot(string expression)
    {
        if (root is null)
        {
            return false;
        }

        var length = 0;
        while (length < expression.Length &&
               (char.IsLetterOrDigit(expression[length]) || expression[length] == '_'))
        {
            length++;
        }

        return length == rootIdentifier.Length &&
            string.CompareOrdinal(expression, 0, rootIdentifier, 0, length) == 0;
    }

    private static string Describe(string declaringNamespace) =>
        string.IsNullOrEmpty(declaringNamespace) ? "<global namespace>" : declaringNamespace;

    private static bool TryParseIndex(string argument, int exclusiveUpperBound, out int index) =>
        int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
        index >= 0 &&
        index < exclusiveUpperBound;

    private static int ParseCount(string argument, int fallback) =>
        int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
}
