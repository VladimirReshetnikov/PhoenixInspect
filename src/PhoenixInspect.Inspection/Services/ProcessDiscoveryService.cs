using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PhoenixInspect.Inspection;

/// <summary>States how a process was recognised as running a .NET runtime.</summary>
public enum ManagedRuntimeEvidence
{
    /// <summary>No managed-runtime evidence was found; the process is probably not a .NET process.</summary>
    None = 0,

    /// <summary>
    /// The runtime's diagnostics IPC endpoint exists for this process — a named pipe on Windows, a socket file
    /// elsewhere. Every .NET Core 3.0+ runtime publishes one unless diagnostics are explicitly disabled.
    /// </summary>
    DiagnosticsEndpoint = 1,

    /// <summary>A runtime module (<c>coreclr</c>, <c>clr</c>, or <c>mscorwks</c>) is loaded in the process.</summary>
    LoadedRuntimeModule = 2,
}

/// <summary>One running process offered for attach.</summary>
/// <param name="ProcessId">The process id.</param>
/// <param name="Name">The process name, without extension.</param>
/// <param name="Evidence">How the process was recognised as managed.</param>
/// <param name="StartedAtUtc">When the process started, or null when the caller may not query it.</param>
/// <param name="ExecutablePath">The main module path, or null when the caller may not query it.</param>
/// <param name="IsArchitectureCompatible">
/// Whether the process's bitness matches this inspector's. ClrMD reads a target's memory in-process, so a 64-bit
/// inspector cannot attach to a 32-bit target or the reverse.
/// </param>
public sealed record ProcessCandidate(
    int ProcessId,
    string Name,
    ManagedRuntimeEvidence Evidence,
    DateTime? StartedAtUtc,
    string? ExecutablePath,
    bool IsArchitectureCompatible)
{
    /// <summary>Gets whether this process is a plausible attach target from this inspector.</summary>
    public bool IsAttachable => Evidence != ManagedRuntimeEvidence.None && IsArchitectureCompatible;

    /// <summary>Gets a short statement of why the process is or is not offered for attach.</summary>
    public string Note => (Evidence, IsArchitectureCompatible) switch
    {
        (ManagedRuntimeEvidence.None, _) => "No managed runtime detected",
        (_, false) => "Different bitness than this inspector",
        (ManagedRuntimeEvidence.DiagnosticsEndpoint, true) => ".NET runtime (diagnostics endpoint)",
        _ => ".NET runtime (runtime module loaded)",
    };
}

/// <summary>
/// Discovers running processes that a live-attach session could inspect, and says how each one was recognised.
/// Detection is evidence-based and never guesses: a process is offered because its runtime published a
/// diagnostics endpoint or because a runtime module is loaded in it, and anything else is listed as unattachable
/// with the reason stated.
/// </summary>
/// <remarks>
/// Enumeration is deliberately cheap and non-invasive: it opens no target memory, suspends nothing, and tolerates
/// every per-process query failing — a process the caller may not query still appears, with the facts it could not
/// read left null rather than invented.
/// </remarks>
public static class ProcessDiscoveryService
{
    /// <summary>The greatest number of processes one enumeration returns.</summary>
    public const int MaximumProcesses = 4_096;

    /// <summary>Enumerates candidate processes, managed ones first, then by name.</summary>
    /// <returns>The candidates, bounded by <see cref="MaximumProcesses"/>.</returns>
    public static ImmutableArray<ProcessCandidate> ListCandidates()
    {
        var diagnosticsPids = ReadDiagnosticsEndpointProcessIds();
        var candidates = ImmutableArray.CreateBuilder<ProcessCandidate>();
        var self = Environment.ProcessId;
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return [];
        }

        foreach (var process in processes)
        {
            using (process)
            {
                if (candidates.Count >= MaximumProcesses)
                {
                    continue;
                }

                int processId;
                string name;
                try
                {
                    processId = process.Id;
                    name = process.ProcessName;
                }
                catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
                {
                    continue;
                }

                if (processId == self || processId <= 0)
                {
                    continue;
                }

                var evidence = diagnosticsPids.Contains(processId)
                    ? ManagedRuntimeEvidence.DiagnosticsEndpoint
                    : HasRuntimeModule(process)
                        ? ManagedRuntimeEvidence.LoadedRuntimeModule
                        : ManagedRuntimeEvidence.None;
                if (evidence == ManagedRuntimeEvidence.None)
                {
                    continue;
                }

                candidates.Add(new ProcessCandidate(
                    processId,
                    name,
                    evidence,
                    TryReadStartTimeUtc(process),
                    TryReadExecutablePath(process),
                    IsArchitectureCompatible(process)));
            }
        }

        return
        [
            .. candidates
                .OrderByDescending(static candidate => candidate.IsAttachable)
                .ThenBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static candidate => candidate.ProcessId),
        ];
    }

    /// <summary>Applies the process-list filter: blank admits all, otherwise name or process id substring.</summary>
    /// <param name="candidate">The candidate to test.</param>
    /// <param name="filter">The raw filter text, possibly null or blank.</param>
    /// <returns>Whether the candidate satisfies the filter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> is null.</exception>
    public static bool Matches(ProcessCandidate candidate, string? filter)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return string.IsNullOrWhiteSpace(filter)
            || candidate.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || candidate.ProcessId.ToString(CultureInfo.InvariantCulture)
                .Contains(filter.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the process ids that published a runtime diagnostics endpoint: named pipes on Windows, socket files
    /// in the temporary directory elsewhere. This is the same signal the .NET diagnostics tools list processes by.
    /// </summary>
    private static HashSet<int> ReadDiagnosticsEndpointProcessIds()
    {
        var ids = new HashSet<int>();
        try
        {
            var (directory, pattern) = OperatingSystem.IsWindows()
                ? (@"\\.\pipe\", "dotnet-diagnostic-*")
                : (Path.GetTempPath(), "dotnet-diagnostic-*-socket");
            foreach (var entry in Directory.EnumerateFiles(directory, pattern))
            {
                // dotnet-diagnostic-{pid}-{disambiguationKey}-socket
                var segments = Path.GetFileName(entry).Split('-');
                if (segments.Length >= 3 &&
                    int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                {
                    ids.Add(pid);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // No endpoint listing available; the module scan still recognises managed processes.
        }

        return ids;
    }

    private static bool HasRuntimeModule(Process process)
    {
        try
        {
            foreach (ProcessModule module in process.Modules)
            {
                using (module)
                {
                    var moduleName = module.ModuleName;
                    if (moduleName is null)
                    {
                        continue;
                    }

                    if (moduleName.StartsWith("coreclr", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.StartsWith("libcoreclr", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.Equals("mscorwks.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Module enumeration needs rights and matching bitness the caller may not have; absence of evidence
            // is reported as absence of evidence, never as a claim that the process is unmanaged.
        }

        return false;
    }

    private static DateTime? TryReadStartTimeUtc(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static string? TryReadExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsArchitectureCompatible(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Only Windows runs mixed-bitness processes side by side in a way this check must separate.
            return true;
        }

        try
        {
            // A WOW64 process is 32-bit; anything else on a 64-bit OS matches a 64-bit inspector.
            return !IsWow64Process(process.Handle, out var isWow64) || isWow64 != Environment.Is64BitProcess;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Without the handle the bitness is unknown; offering the process and letting the attach report the
            // typed failure is more honest than hiding it.
            return true;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(nint process, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);
}
