using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Provides minimal dump-loading and module-discovery operations for draft integration tests.
/// </summary>
public sealed class ClrmdDumpSession : IDisposable
{
    private readonly DataTarget _dataTarget;

    private ClrmdDumpSession(DataTarget dataTarget, ClrRuntime runtime, IReadOnlyList<ClrmdModuleInfo> modules)
    {
        _dataTarget = dataTarget;
        Runtime = runtime;
        Modules = modules;
    }

    /// <summary>
    /// Gets the active CLR runtime opened from the dump.
    /// </summary>
    public ClrRuntime Runtime { get; }

    /// <summary>
    /// Gets discovered managed modules from the loaded dump runtime.
    /// </summary>
    public IReadOnlyList<ClrmdModuleInfo> Modules { get; }

    /// <summary>
    /// Loads a dump file and creates a CLR runtime plus module catalog.
    /// </summary>
    /// <param name="dumpPath">Path to the dump file that should be opened.</param>
    /// <returns>A new <see cref="ClrmdDumpSession"/> for the specified dump.</returns>
    public static ClrmdDumpSession Load(string dumpPath)
    {
        if (string.IsNullOrWhiteSpace(dumpPath))
        {
            throw new ArgumentException("Dump path is required.", nameof(dumpPath));
        }

        var dataTarget = DataTarget.LoadDump(dumpPath);
        var clrInfo = dataTarget.ClrVersions.First();
        var runtime = clrInfo.CreateRuntime();

        var modules = runtime.Modules
            .Select(module => new ClrmdModuleInfo(
                Name: Path.GetFileName(module.Name),
                FilePath: string.IsNullOrWhiteSpace(module.Name) ? null : Path.GetFullPath(module.Name)))
            .ToArray();

        return new ClrmdDumpSession(dataTarget, runtime, modules);
    }

    /// <summary>
    /// Attempts to find a module by case-insensitive file name.
    /// </summary>
    /// <param name="fileName">Simple file name (for example, <c>Interpreter.TestTarget.dll</c>).</param>
    /// <param name="module">Resolved module metadata when found.</param>
    /// <returns><see langword="true"/> when a module matches; otherwise <see langword="false"/>.</returns>
    public bool TryFindModuleByFileName(string fileName, out ClrmdModuleInfo module)
    {
        module = default!;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var match = Modules.FirstOrDefault(
            candidate => string.Equals(candidate.Name, fileName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        module = match;
        return true;
    }

    /// <summary>
    /// Releases underlying ClrMD resources associated with the dump session.
    /// </summary>
    public void Dispose() => _dataTarget.Dispose();
}
