using System.Collections.Concurrent;
using System.IO;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Inspection;

/// <summary>Describes the outcome of one interactive dump-open attempt.</summary>
/// <param name="IsOpen">Whether a session is now available for inspection.</param>
/// <param name="Status">The adapter status returned by <see cref="ClrmdDumpSession.Open"/>.</param>
/// <param name="Issue">The adapter issue explaining a non-exact open attempt.</param>
/// <param name="Message">A display message describing the outcome, including a caught exception.</param>
public sealed record DumpOpenOutcome(
    bool IsOpen,
    ClrmdEvidenceStatus? Status,
    ClrmdValueIssue? Issue,
    string Message);

/// <summary>
/// Owns the single open dump session and guarantees that every adapter call runs on one dedicated worker thread.
/// </summary>
/// <remarks>
/// <para>
/// The ClrMD adapter exposes an immutable snapshot but its underlying data reader is not designed for concurrent use.
/// The demo host therefore pins all session work to one long-lived thread and marshals results back through
/// <see cref="Task{TResult}"/>. This keeps the UI responsive without introducing an access pattern that the product
/// libraries do not promise to support.
/// </para>
/// <para>
/// Only one session is open at a time. Opening another dump disposes the previous session first, so no result rendered
/// by the UI can mix evidence from two snapshots.
/// </para>
/// </remarks>
public sealed class DumpSessionHost : IDisposable
{
    private readonly BlockingCollection<Action> queue = new(new ConcurrentQueue<Action>());
    private readonly Thread worker;
    private ClrmdDumpSession? session;
    private bool disposed;

    /// <summary>Creates the host and starts its dedicated adapter thread.</summary>
    public DumpSessionHost()
    {
        worker = new Thread(Pump)
        {
            IsBackground = true,
            Name = "PhoenixInspect dump session",
        };
        worker.Start();
    }

    /// <summary>Gets the path of the currently open dump, or <see langword="null"/> when none is open.</summary>
    public string? DumpPath { get; private set; }

    /// <summary>Gets the byte length of the currently open dump file, or <see langword="null"/> when none is open.</summary>
    public long? DumpLength { get; private set; }

    /// <summary>Gets whether a dump session is currently open.</summary>
    public bool IsOpen => session is not null;

    /// <summary>Closes any current session and opens the requested dump on the adapter thread.</summary>
    /// <param name="dumpPath">The full path of the dump file to open.</param>
    /// <returns>A display-ready outcome; a non-exact open leaves the host closed.</returns>
    public Task<DumpOpenOutcome> OpenAsync(string dumpPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpPath);
        return InvokeAsync(() =>
        {
            CloseCore();
            long length;
            try
            {
                length = new FileInfo(dumpPath).Length;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new DumpOpenOutcome(false, null, null, $"The dump file could not be read: {exception.Message}");
            }

            ClrmdEvidenceResult<ClrmdDumpSession> opened;
            try
            {
                opened = ClrmdDumpSession.Open(dumpPath);
            }
            catch (Exception exception)
            {
                return new DumpOpenOutcome(
                    false,
                    null,
                    null,
                    $"The dump adapter threw while opening the snapshot: {exception.Message}");
            }

            if (opened.Status != ClrmdEvidenceStatus.Exact || opened.Value is null)
            {
                return new DumpOpenOutcome(
                    false,
                    opened.Status,
                    opened.Issue,
                    $"The dump could not be opened exactly ({opened.Status}, {opened.Issue}).");
            }

            session = opened.Value;
            DumpPath = dumpPath;
            DumpLength = length;
            return new DumpOpenOutcome(
                true,
                opened.Status,
                opened.Issue,
                $"Opened {Path.GetFileName(dumpPath)} with {session.Modules.Length} managed module instances.");
        });
    }

    /// <summary>
    /// Closes any current session and attaches to a running .NET process on the adapter thread, suspending the
    /// target for the lifetime of the session. Closing the session detaches and resumes it.
    /// </summary>
    /// <param name="processId">The id of the process to attach to.</param>
    /// <returns>A display-ready outcome; a non-exact attach leaves the host closed.</returns>
    public Task<DumpOpenOutcome> AttachAsync(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        return InvokeAsync(() =>
        {
            CloseCore();
            ClrmdEvidenceResult<ClrmdDumpSession> attached;
            try
            {
                attached = ClrmdDumpSession.AttachToProcess(processId);
            }
            catch (Exception exception)
            {
                return new DumpOpenOutcome(
                    false,
                    null,
                    null,
                    $"The adapter threw while attaching to process {processId}: {exception.Message}");
            }

            if (attached.Status != ClrmdEvidenceStatus.Exact || attached.Value is null)
            {
                return new DumpOpenOutcome(
                    false,
                    attached.Status,
                    attached.Issue,
                    $"Process {processId} could not be attached exactly ({attached.Status}, {attached.Issue}). "
                    + "Attaching needs a running .NET process of the same architecture and sufficient privileges.");
            }

            session = attached.Value;
            DumpPath = null;
            DumpLength = null;
            return new DumpOpenOutcome(
                true,
                attached.Status,
                attached.Issue,
                $"Attached to {session.TargetProcessName} (PID {processId}), suspended, with "
                + $"{session.Modules.Length} managed module instances. Closing the session resumes it.");
        });
    }

    /// <summary>Closes the current session, if any, on the adapter thread.</summary>
    /// <returns>A task that completes once the session has been disposed.</returns>
    public Task CloseAsync() => InvokeAsync(() =>
    {
        CloseCore();
        return true;
    });

    /// <summary>Runs a read-only projection against the open session on the adapter thread.</summary>
    /// <typeparam name="TResult">The immutable display projection produced by <paramref name="work"/>.</typeparam>
    /// <param name="work">The projection to execute; it must not retain the session beyond the call.</param>
    /// <returns>The projection result.</returns>
    /// <exception cref="InvalidOperationException">No dump session is open.</exception>
    public Task<TResult> QueryAsync<TResult>(Func<ClrmdDumpSession, TResult> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InvokeAsync(() => work(
            session ?? throw new InvalidOperationException("No dump session is open.")));
    }

    /// <summary>
    /// Runs one read-only projection on the adapter thread with a cooperative cancellation token. The token never
    /// aborts the worker; the projection observes it at its own safe points and returns (or throws
    /// <see cref="OperationCanceledException"/>), after which the worker simply pumps the next request.
    /// </summary>
    /// <typeparam name="TResult">The immutable projection produced by <paramref name="work"/>.</typeparam>
    /// <param name="work">The projection to run; it receives the token to observe.</param>
    /// <param name="cancellationToken">The token the projection cooperates with.</param>
    /// <returns>A task producing the projection result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is null.</exception>
    public Task<TResult> QueryAsync<TResult>(
        Func<ClrmdDumpSession, CancellationToken, TResult> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InvokeAsync(() =>
        {
            // A request cancelled while still queued never starts; one cancelled mid-run returns through the
            // projection's own cooperative checks.
            cancellationToken.ThrowIfCancellationRequested();
            return work(
                session ?? throw new InvalidOperationException("No dump session is open."),
                cancellationToken);
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            InvokeAsync(() =>
            {
                CloseCore();
                return true;
            }).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            // The worker already stopped; the session is unreachable and the process is shutting down.
        }

        queue.CompleteAdding();
        worker.Join(TimeSpan.FromSeconds(5));
        queue.Dispose();
    }

    private void CloseCore()
    {
        session?.Dispose();
        session = null;
        DumpPath = null;
        DumpLength = null;
    }

    private Task<TResult> InvokeAsync<TResult>(Func<TResult> work)
    {
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            queue.Add(() =>
            {
                try
                {
                    completion.SetResult(work());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            completion.SetException(new InvalidOperationException("The dump session host is shutting down.", exception));
        }

        return completion.Task;
    }

    private void Pump()
    {
        foreach (var work in queue.GetConsumingEnumerable())
        {
            work();
        }
    }
}
