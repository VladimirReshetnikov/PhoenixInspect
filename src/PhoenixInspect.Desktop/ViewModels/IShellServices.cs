using PhoenixInspect.Inspection;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Desktop.ViewModels;

/// <summary>
/// The shell capabilities a pane needs: serialized session access, status reporting, and cross-pane actions.
/// </summary>
/// <remarks>
/// Panes never touch the dump host directly. Routing every adapter call through the shell keeps busy state, error
/// reporting, and single-threaded session access in one place.
/// </remarks>
public interface IShellServices
{
    /// <summary>Gets whether a dump session is currently open.</summary>
    bool IsDumpOpen { get; }

    /// <summary>Runs a read-only projection on the session thread while the shell shows a busy indicator.</summary>
    /// <typeparam name="TResult">The immutable display projection produced by <paramref name="work"/>.</typeparam>
    /// <param name="busyMessage">The message shown while the projection runs.</param>
    /// <param name="work">The projection to run on the session thread.</param>
    /// <returns>
    /// The projection result, or <see langword="default"/> when no dump is open or the adapter threw. A thrown
    /// exception is reported through the shell's error banner rather than propagated to the caller.
    /// </returns>
    Task<TResult?> RunAsync<TResult>(string busyMessage, Func<ClrmdDumpSession, TResult> work);

    /// <summary>Replaces the status-bar message.</summary>
    /// <param name="message">The new status text.</param>
    void SetStatus(string message);

    /// <summary>Adopts one selected frame as the name-binding context of the evaluation pane.</summary>
    /// <param name="frame">The exact frame to adopt.</param>
    void UseAsEvaluationContext(CallStackFrameNode frame);

    /// <summary>Adopts one heap object as the root of the evaluation pane's root-relative path.</summary>
    /// <param name="row">The exact object to adopt.</param>
    void UseAsEvaluationRoot(HeapObjectRow row);

    /// <summary>Resolves one frame's verified source and shows it as a document tab.</summary>
    /// <param name="frame">The frame to resolve.</param>
    /// <returns>A task that completes once the document is shown.</returns>
    Task ShowFrameSourceAsync(CallStackFrameNode frame);
}
