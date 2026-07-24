using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Core.Execution;

/// <summary>
/// Carries deterministic execution bookkeeping that must not participate in semantic-state equality or joins.
/// </summary>
/// <param name="Budget">The remaining instruction allowance enforced by the current slice.</param>
/// <remarks>
/// Future analysis fixpoints must compare and join <see cref="MachineState{TValue,TMemory}"/> through an explicit
/// semantic comparer, while excluding this envelope. Keeping decreasing budgets separate prevents traversal
/// history from blocking convergence. The depth high-water values begin at the activated root depth of one and are
/// monotonic execution observations rather than semantic-state facts. Step transcripts should still record this
/// operational state explicitly.
/// </remarks>
public sealed record MachineOperationalState(BudgetState Budget)
{
    /// <summary>
    /// Gets the ordered immutable audit of frozen pure-model capabilities actually entered by this machine session.
    /// </summary>
    /// <remarks>
    /// The vector is initialized empty for legacy and fresh prepared sessions. A modeled invocation appends exactly
    /// one item even when its typed outcome cannot complete a semantic transfer. Budget/preflight failures append
    /// nothing. This operational evidence is deliberately excluded from semantic-state equality and joins.
    /// </remarks>
    public ImmutableArray<PureModelAttempt> ModelAttempts { get; init; } =
        ImmutableArray<PureModelAttempt>.Empty;

    /// <summary>Gets the number of frozen pure-model capabilities entered during this session.</summary>
    /// <remarks>
    /// The monotonic counter must equal <see cref="ModelAttempts"/> length. It is retained explicitly so hosts
    /// can account for model capability use without inferring it from semantic debug events.
    /// </remarks>
    public int ModelInvocationCount { get; init; }

    /// <summary>Gets the number of modeled calls whose caller transfer completed atomically.</summary>
    /// <remarks>
    /// The monotonic counter equals the number of attempts whose
    /// <see cref="PureModelAttempt.TransferCompleted"/> fact is true. It never exceeds
    /// <see cref="ModelInvocationCount"/>.
    /// </remarks>
    public int CompletedModeledCallCount { get; init; }

    /// <summary>
    /// Gets the configured maximum logical call depth for prepared-graph execution, or <see langword="null"/> for a
    /// legacy call-free session.
    /// </summary>
    /// <remarks>
    /// The value is a nonconsumable replay fact. <see cref="IlMachine{TValue,TMemory}.CreatePreparedOperationalState"/>
    /// initializes it from the graph session so callers do not have to duplicate activation policy.
    /// </remarks>
    public int? ConfiguredMaximumLogicalCallDepth { get; init; }

    /// <summary>
    /// Gets the frozen graph's required logical depth, or <see langword="null"/> for a legacy call-free session.
    /// </summary>
    /// <remarks>
    /// Prepared steps revalidate this retained fact against the exact bound graph. It is distinct from observed high
    /// water: required depth is known before execution, while observed depth advances across completed interpreted
    /// calls and every pure-model capability entry, including an invocation whose outcome does not transfer.
    /// </remarks>
    public int? RequiredLogicalCallDepth { get; init; }

    /// <summary>
    /// Gets the greatest logical call depth observed so far, counting the root boundary as depth one.
    /// </summary>
    /// <remarks>
    /// W4.5 interpreted calls increase logical and active-frame depth together. A model-covered call enters its
    /// logical boundary without pushing a frame and advances this observation as soon as the frozen capability is
    /// invoked, even if its outcome later blocks or fails validation without a semantic transfer. Preflight and
    /// pre-instruction budget failures do not advance it. Consumers therefore must not infer this value from call-
    /// stack length or completed-call count. This init-only counter defaults to the root depth.
    /// </remarks>
    public int ObservedLogicalDepthHighWater { get; init; } = 1;

    /// <summary>
    /// Gets the greatest number of simultaneously active interpreted frames observed so far.
    /// </summary>
    /// <remarks>
    /// The counter defaults to the single activated root frame. It is operational evidence, excluded from
    /// <see cref="MachineStateSemanticComparer{TValue,TMemory}"/>, and advances only when a real frame push completes.
    /// </remarks>
    public int ActiveFrameDepthHighWater { get; init; } = 1;
}
