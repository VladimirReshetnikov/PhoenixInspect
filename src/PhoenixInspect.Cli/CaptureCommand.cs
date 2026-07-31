using System.Globalization;
using Microsoft.Diagnostics.NETCore.Client;

namespace PhoenixInspect.Cli;

/// <summary>
/// Writes a full process dump so an inspection session has something to open.
/// </summary>
/// <remarks>
/// <para>
/// Collection is separate from inspection and stays that way. This command only asks the target's own diagnostics
/// server for a full dump; it never attaches a debugger, never suspends the process on PhoenixInspect's behalf
/// beyond what the runtime does to write the dump, and never modifies the target.
/// </para>
/// <para>
/// A dump written by any other collector — Windows Error Reporting, Task Manager, <c>dotnet-dump collect</c>,
/// <c>procdump -ma</c> — is equally valid input. This exists so a first session does not require installing a second
/// tool, not because PhoenixInspect needs to have produced the file.
/// </para>
/// </remarks>
public static class CaptureCommand
{
    /// <summary>The verb that selects this command.</summary>
    public const string Verb = "capture";

    /// <summary>Runs one capture invocation.</summary>
    /// <param name="args">The arguments following the <c>capture</c> verb.</param>
    /// <returns>Zero on success, two for a usage error, and five when the dump could not be written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var renderer = new ConsoleRenderer(Console.Out, styled: false);

        int? pid = null;
        string? output = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--pid" or "-p" when index + 1 < args.Length:
                    if (!int.TryParse(args[++index], CultureInfo.InvariantCulture, out var parsed))
                    {
                        renderer.Error("--pid requires a process id.");
                        return 2;
                    }

                    pid = parsed;
                    break;

                case "--output" or "-o" when index + 1 < args.Length:
                    output = args[++index];
                    break;

                default:
                    renderer.Error($"unrecognized capture argument '{args[index]}'.");
                    WriteUsage(renderer);
                    return 2;
            }
        }

        if (pid is null || output is null)
        {
            renderer.Error("capture requires both --pid and --output.");
            WriteUsage(renderer);
            return 2;
        }

        var fullPath = Path.GetFullPath(output);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            new DiagnosticsClient(pid.Value).WriteDump(DumpType.Full, fullPath, logDumpGeneration: false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            renderer.Error($"the runtime could not write a full dump of process {pid.Value}: {exception.Message}");
            return 5;
        }

        var length = new FileInfo(fullPath).Length;
        renderer.Line($"Wrote a full dump of process {pid.Value} to {fullPath} ({length:N0} bytes).");
        return 0;
    }

    private static void WriteUsage(ConsoleRenderer renderer)
    {
        renderer.Line();
        renderer.Line("usage: phoenixinspect capture --pid <processId> --output <path>");
    }
}
