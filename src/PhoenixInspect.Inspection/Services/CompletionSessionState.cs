using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Inspection;

/// <summary>
/// Executes ambient read-only session projections for completion: quiet, serialized, and never surfaced as busy
/// state or errors. A host implements this over its own session-access discipline.
/// </summary>
public interface ICompletionQueryExecutor
{
    /// <summary>Gets whether a dump session is currently open.</summary>
    bool IsSessionOpen { get; }

    /// <summary>Runs one read-only projection on the session thread without user-visible side effects.</summary>
    /// <typeparam name="TResult">The immutable projection produced by <paramref name="work"/>.</typeparam>
    /// <param name="work">The projection to run.</param>
    /// <returns>The projection result, or <see langword="default"/> when no session is open or the adapter threw.</returns>
    Task<TResult?> RunQuietAsync<TResult>(Func<ClrmdDumpSession, TResult> work);
}

/// <summary>
/// The single, host-independent completion state every expression editor shares: the cached session catalog, the
/// on-demand realization of metadata type members, and the pure completion query over both. Hosts render the
/// items; nothing about what completes lives in a presentation layer.
/// </summary>
/// <remarks>
/// One instance is owned per shell and handed to every pane, so a type's members realized while typing in one
/// editor are immediately available to all of them. <see cref="Changed"/> is raised on whatever context the
/// executor completes on — hosts that marshal session work back to their UI thread hear it there.
/// </remarks>
public sealed class CompletionSessionState
{
    private readonly ICompletionQueryExecutor executor;
    private readonly HashSet<string> pendingTypeMemberFetches = new(StringComparer.Ordinal);

    /// <summary>Creates the state over one host executor.</summary>
    /// <param name="executor">The host's quiet session-query discipline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="executor"/> is null.</exception>
    public CompletionSessionState(ICompletionQueryExecutor executor) =>
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <summary>Raised when the catalog gained or lost facts, so an open drop-down can recompute.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the current catalog; <see cref="CompletionCatalog.Empty"/> before a session contributes.</summary>
    public CompletionCatalog Catalog { get; private set; } = CompletionCatalog.Empty;

    /// <summary>Drops every session-derived fact; keyword and receiver completion keeps working.</summary>
    public void Clear()
    {
        Catalog = CompletionCatalog.Empty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Rebuilds the catalog for the current session, root, and root identifier.</summary>
    /// <param name="root">The adopted root, or null.</param>
    /// <param name="rootIdentifier">The identifier root-relative expressions use.</param>
    /// <returns>A task that completes once the catalog reflects the arguments.</returns>
    public async Task RebuildAsync(RootSelection? root, string rootIdentifier)
    {
        if (!executor.IsSessionOpen)
        {
            Clear();
            return;
        }

        var built = await executor.RunQuietAsync(
            session => ExpressionCompletionService.BuildCatalog(session, root, rootIdentifier))
            .ConfigureAwait(true);
        if (built is not null)
        {
            Catalog = built;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Computes the completions for one caret position over the cached catalog. A receiver naming a metadata type
    /// whose members are not realized yet triggers their quiet realization; <see cref="Changed"/> announces them.
    /// </summary>
    /// <param name="text">The expression text being edited.</param>
    /// <param name="caretOffset">The caret offset within <paramref name="text"/>.</param>
    /// <param name="context">The editor-specific context, or null for a plain expression editor.</param>
    /// <param name="explicitInvocation">Whether the user asked for completion, which completes an empty token.</param>
    /// <returns>The completion result, possibly empty.</returns>
    public CompletionResult Complete(
        string text,
        int caretOffset,
        CompletionContext? context = null,
        bool explicitInvocation = false)
    {
        var result = ExpressionCompletionService.Complete(
            Catalog, text ?? string.Empty, caretOffset, context, explicitInvocation);
        if (result.PendingTypeMembers is { } typeFullName)
        {
            _ = RealizeTypeMembersAsync(typeFullName);
        }

        return result;
    }

    private async Task RealizeTypeMembersAsync(string typeFullName)
    {
        if (!executor.IsSessionOpen || !pendingTypeMemberFetches.Add(typeFullName))
        {
            return;
        }

        var members = await executor.RunQuietAsync(
            session => ExpressionCompletionService.ListStaticMemberCompletions(session, typeFullName))
            .ConfigureAwait(true);
        pendingTypeMemberFetches.Remove(typeFullName);
        if (!members.IsDefault)
        {
            Catalog = Catalog with { TypeMembers = Catalog.TypeMembers.SetItem(typeFullName, members) };
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
