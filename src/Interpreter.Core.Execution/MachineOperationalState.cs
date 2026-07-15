using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

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
    /// water: required depth is known before execution, while observed depth advances only across completed calls.
    /// </remarks>
    public int? RequiredLogicalCallDepth { get; init; }

    /// <summary>
    /// Gets the greatest logical call depth observed so far, counting the root boundary as depth one.
    /// </summary>
    /// <remarks>
    /// W4.5 interpreted calls increase logical and active-frame depth together. A later model-covered call may enter
    /// a logical boundary without pushing a frame, so consumers must not infer this value from call-stack length.
    /// This draft init-only counter defaults to the root depth and is updated only after completed call transfers.
    /// </remarks>
    public int ObservedLogicalDepthHighWater { get; init; } = 1;

    /// <summary>
    /// Gets the greatest number of simultaneously active interpreted frames observed so far.
    /// </summary>
    /// <remarks>
    /// The draft counter defaults to the single activated root frame. It is operational evidence, excluded from
    /// <see cref="MachineStateSemanticComparer{TValue,TMemory}"/>, and advances only when a real frame push completes.
    /// </remarks>
    public int ActiveFrameDepthHighWater { get; init; } = 1;
}
