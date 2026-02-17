namespace Interpreter.Host.Abstractions;

/// <summary>
/// Provides host-specific frame seeding for a chosen thread/frame pair.
/// </summary>
public interface IFrameSeeder
{
    /// <summary>
    /// Tries to materialize frame seed values for the requested frame.
    /// </summary>
    /// <param name="thread">Target thread identity.</param>
    /// <param name="frame">Target frame identity.</param>
    /// <param name="seed">Resolved seed values when available.</param>
    /// <returns><see langword="true"/> when seeding succeeds; otherwise <see langword="false"/>.</returns>
    bool TrySeedFrame(ExternalThreadId thread, ExternalFrameId frame, out FrameSeed seed);
}
