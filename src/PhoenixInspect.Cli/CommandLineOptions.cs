using System.Collections.Immutable;

namespace PhoenixInspect.Cli;

/// <summary>Carries one validated console invocation.</summary>
/// <param name="DumpPath">The dump file to open, or null when attaching to a process.</param>
/// <param name="AttachProcessId">The process id to attach to, or null when opening a dump.</param>
/// <param name="Commands">
/// The session commands to run in order. An empty array means the session should prompt interactively.
/// </param>
/// <param name="Verbose">Whether every answer should also print its complete evidence.</param>
/// <param name="Styled">Whether ANSI styling may be emitted.</param>
public sealed record CommandLineOptions(
    string? DumpPath,
    int? AttachProcessId,
    ImmutableArray<string> Commands,
    bool Verbose,
    bool Styled)
{
    /// <summary>Parses raw arguments into a validated invocation.</summary>
    /// <param name="args">The raw arguments.</param>
    /// <param name="options">The parsed invocation when parsing succeeded.</param>
    /// <param name="error">
    /// The usage error when parsing failed, or <see langword="null"/> when the caller merely asked for help.
    /// </param>
    /// <returns><see langword="true"/> when a session should run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    public static bool TryParse(
        string[] args,
        out CommandLineOptions options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        options = null!;
        error = null;

        string? dumpPath = null;
        int? attachProcessId = null;
        var commands = ImmutableArray.CreateBuilder<string>();
        var verbose = false;
        var styled = ShouldStyleByDefault();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--help" or "-h" or "-?" or "/?":
                    return false;

                case "--verbose" or "-v":
                    verbose = true;
                    break;

                case "--no-color" or "--no-colour":
                    styled = false;
                    break;

                case "--eval" or "-e":
                    if (!TryTakeValue(args, ref index, out var expression))
                    {
                        error = "--eval requires an expression.";
                        return false;
                    }

                    commands.Add("eval " + expression);
                    break;

                case "--attach":
                    if (!TryTakeValue(args, ref index, out var pidText) ||
                        !int.TryParse(pidText, System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out var pid) ||
                        pid <= 0)
                    {
                        error = "--attach requires a positive process id.";
                        return false;
                    }

                    attachProcessId = pid;
                    break;

                case "--command" or "-c":
                    if (!TryTakeValue(args, ref index, out var command))
                    {
                        error = "--command requires command text.";
                        return false;
                    }

                    commands.Add(command);
                    break;

                case "--script" or "-s":
                    if (!TryTakeValue(args, ref index, out var scriptPath))
                    {
                        error = "--script requires a file path.";
                        return false;
                    }

                    if (!File.Exists(scriptPath))
                    {
                        error = $"the script file '{scriptPath}' does not exist.";
                        return false;
                    }

                    commands.AddRange(File.ReadAllLines(scriptPath));
                    break;

                default:
                    if (argument.StartsWith('-'))
                    {
                        error = $"unrecognized option '{argument}'.";
                        return false;
                    }

                    if (dumpPath is not null)
                    {
                        error = "only one dump file may be opened per session.";
                        return false;
                    }

                    dumpPath = argument;
                    break;
            }
        }

        if (dumpPath is null && attachProcessId is null)
        {
            error = "a dump file path or --attach <pid> is required.";
            return false;
        }

        if (dumpPath is not null && attachProcessId is not null)
        {
            error = "open a dump file or attach to a process, not both.";
            return false;
        }

        if (dumpPath is not null && !File.Exists(dumpPath))
        {
            error = $"the dump file '{dumpPath}' does not exist.";
            return false;
        }

        options = new CommandLineOptions(
            dumpPath is null ? null : Path.GetFullPath(dumpPath),
            attachProcessId,
            commands.ToImmutable(),
            verbose,
            styled);
        return true;
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool ShouldStyleByDefault() =>
        !Console.IsOutputRedirected &&
        Environment.GetEnvironmentVariable("NO_COLOR") is null;
}
