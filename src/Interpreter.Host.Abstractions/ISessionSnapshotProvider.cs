namespace Interpreter.Host.Abstractions;

/// <summary>
/// Provides session-snapshot metadata for host-integrated call models and diagnostics.
/// </summary>
public interface ISessionSnapshotProvider
{
    /// <summary>
    /// Gets the current session snapshot.
    /// </summary>
    /// <returns>Session snapshot data captured by the active host implementation.</returns>
    SessionSnapshot GetSnapshot();
}
