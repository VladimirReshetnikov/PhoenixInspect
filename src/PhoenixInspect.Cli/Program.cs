using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Cli;

/// <summary>Entry point of the PhoenixInspect console host.</summary>
public static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 2;
    private const int ExitDumpUnavailable = 3;
    private const int ExitCommandFailed = 4;

    /// <summary>Runs one console session.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// Zero when the session completed; two for a usage error; three when the dump could not be opened exactly; and
    /// four when a scripted command was rejected or could not run.
    /// </returns>
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args is [CaptureCommand.Verb, ..])
        {
            return CaptureCommand.Run(args[1..]);
        }

        if (!CommandLineOptions.TryParse(args, out var options, out var usageError))
        {
            var plain = new ConsoleRenderer(Console.Error, styled: false);
            if (usageError is not null)
            {
                plain.Error(usageError);
            }

            WriteUsage(plain);
            return usageError is null ? ExitSuccess : ExitUsage;
        }

        var renderer = new ConsoleRenderer(Console.Out, options.Styled);
        renderer.Banner(Version);

        using var host = new DumpSessionHost();
        var opened = await host.OpenAsync(options.DumpPath).ConfigureAwait(false);
        renderer.Line();
        if (!opened.IsOpen)
        {
            renderer.Error(opened.Message);
            return ExitDumpUnavailable;
        }

        renderer.Note($"  {opened.Message}");
        var session = new InspectionSession(host, renderer) { Verbose = options.Verbose };
        await session.ShowOverviewAsync().ConfigureAwait(false);

        var exitCode = options.Commands.IsDefaultOrEmpty
            ? await RunInteractiveAsync(session, renderer).ConfigureAwait(false)
            : await RunScriptedAsync(session, renderer, options.Commands).ConfigureAwait(false);

        renderer.Heading("Session summary");
        renderer.Pair("Expressions evaluated", session.EvaluationCount.ToString());
        renderer.Pair("Answers that were not exact or exhaustively absent", session.NonExactEvaluationCount.ToString());
        renderer.Note(
            "  Every answer above is evidence read from the snapshot under the product's own result axes. A non-exact "
            + "answer is a reported limit of the evidence, not a failure to try harder.");
        return exitCode;
    }

    private static async Task<int> RunScriptedAsync(
        InspectionSession session,
        ConsoleRenderer renderer,
        ImmutableArray<string> commands)
    {
        foreach (var command in commands)
        {
            renderer.Line();
            renderer.Note($"phoenix> {command}");
            var outcome = await session.ExecuteAsync(command).ConfigureAwait(false);
            if (outcome == CommandOutcome.Exit)
            {
                break;
            }

            if (outcome == CommandOutcome.Failed)
            {
                return ExitCommandFailed;
            }
        }

        return ExitSuccess;
    }

    private static async Task<int> RunInteractiveAsync(InspectionSession session, ConsoleRenderer renderer)
    {
        renderer.Line();
        renderer.Note("  Type 'help' for commands, or an expression to evaluate. 'exit' ends the session.");
        while (true)
        {
            renderer.Line();
            renderer.Prompt(session.PromptContext);
            var line = Console.ReadLine();
            if (line is null)
            {
                return ExitSuccess;
            }

            var outcome = await session.ExecuteAsync(line).ConfigureAwait(false);
            if (outcome == CommandOutcome.Exit)
            {
                return ExitSuccess;
            }
        }
    }

    private static void WriteUsage(ConsoleRenderer renderer)
    {
        renderer.Line();
        renderer.Line("usage: phoenixinspect <dump-file> [options]");
        renderer.Line();
        renderer.Pair("--eval <expression>", "Evaluate one expression, then continue. May be repeated.", 30);
        renderer.Pair("--command <text>", "Run one session command. May be repeated; order is preserved.", 30);
        renderer.Pair("--script <path>", "Run session commands from a file, one per line; '#' starts a comment.", 30);
        renderer.Pair("--verbose", "Print the complete evidence behind every answer.", 30);
        renderer.Pair("--no-color", "Suppress ANSI styling.", 30);
        renderer.Pair("--help", "Show this help.", 30);
        renderer.Line();
        renderer.Line("With no --eval, --command, or --script the session starts an interactive prompt.");
        renderer.Line();
        renderer.Line("usage: phoenixinspect capture --pid <processId> --output <path>");
        renderer.Line();
        renderer.Line("Writes a full dump of a running process. Dumps from any other collector are equally valid input.");
    }

    private static string Version =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { } informational && informational.Length != 0
            ? informational.Split('+')[0]
            : "preview";
}
