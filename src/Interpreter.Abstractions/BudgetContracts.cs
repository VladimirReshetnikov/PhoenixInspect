namespace Interpreter.Abstractions;

/// <summary>
/// Identifies a coarse budget meter that can be charged while interpreting one execution session.
/// </summary>
/// <remarks>
/// Meter names are intentionally broad in the prototype phase so budget-accounting behavior can be validated across
/// execution, stepping, metadata, and async orchestration before we commit to fine-grained production counters.
/// </remarks>
public enum BudgetMetricKind
{
    /// <summary>
    /// Charges one or more IL instruction evaluation units.
    /// </summary>
    Instruction,

    /// <summary>
    /// Charges branch-fork materialization units for path-splitting behavior.
    /// </summary>
    BranchFork,

    /// <summary>
    /// Charges abstract memory-read operations initiated by instruction semantics.
    /// </summary>
    MemoryRead,

    /// <summary>
    /// Charges abstract memory-write operations applied to the prototype state store.
    /// </summary>
    MemoryWrite,

    /// <summary>
    /// Charges call-site classification work delegated to the call-model subsystem.
    /// </summary>
    CallClassification,

    /// <summary>
    /// Charges debug-map lookup or synthesis work used by stepping and source mapping.
    /// </summary>
    DebugMapLookup,

    /// <summary>
    /// Charges async state-machine transitions handled by the virtual task runtime.
    /// </summary>
    AsyncTransition,
}

/// <summary>
/// Captures one immutable request to charge a budget meter during prototype execution.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier used to correlate budget accounting with diagnostics.</param>
/// <param name="Metric">Gets the meter category that should be charged.</param>
/// <param name="Amount">Gets the non-negative amount to charge against the selected meter.</param>
/// <param name="ReasonCode">Gets a machine-readable reason code such as <c>step:instruction</c> or <c>call:classify</c>.</param>
/// <param name="InstructionOffset">Gets the optional IL offset associated with this budget charge.</param>
/// <remarks>
/// This request remains intentionally string-friendly in early design so hosts can quickly inspect and filter budget events
/// without depending on finalized semantic model types.
/// </remarks>
public sealed record BudgetChargeRequest(
    string SessionId,
    BudgetMetricKind Metric,
    int Amount,
    string ReasonCode,
    int? InstructionOffset);

/// <summary>
/// Represents the result of attempting to apply one budget charge.
/// </summary>
/// <param name="Metric">Gets the budget meter that was charged.</param>
/// <param name="AmountApplied">Gets the amount that was actually applied to the meter.</param>
/// <param name="Remaining">Gets the remaining budget for the meter after the charge was processed.</param>
/// <param name="IsLimitExceeded">Gets a value indicating whether this charge caused the meter to cross a configured limit.</param>
/// <param name="StopDescriptor">Gets an optional stop descriptor that should be surfaced when the charge exceeded budget limits.</param>
/// <param name="Message">Gets a host-readable summary of the budget decision and resulting meter state.</param>
/// <remarks>
/// The stop descriptor field allows callers to bridge budget accounting and execution stop reporting without introducing
/// hard coupling between control-plane components during the conceptual design phase.
/// </remarks>
public sealed record BudgetChargeResult(
    BudgetMetricKind Metric,
    int AmountApplied,
    int Remaining,
    bool IsLimitExceeded,
    ExecutionStopDescriptor? StopDescriptor,
    string Message);

/// <summary>
/// Provides a snapshot of aggregate budget usage for one execution session.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier associated with this budget snapshot.</param>
/// <param name="RemainingByMetric">Gets remaining budget values keyed by meter kind.</param>
/// <param name="ConsumedByMetric">Gets consumed budget values keyed by meter kind.</param>
/// <param name="LastUpdatedUtc">Gets the UTC timestamp when the budget tracker last updated this snapshot.</param>
/// <remarks>
/// Dictionary-based payloads are used deliberately in the prototype to keep metric evolution low-friction while budget
/// governance and observability requirements are still being discovered.
/// </remarks>
public sealed record BudgetSnapshot(
    string SessionId,
    IReadOnlyDictionary<BudgetMetricKind, int> RemainingByMetric,
    IReadOnlyDictionary<BudgetMetricKind, int> ConsumedByMetric,
    DateTimeOffset LastUpdatedUtc);

/// <summary>
/// Defines the prototype contract responsible for charging and inspecting execution budgets.
/// </summary>
/// <remarks>
/// This service boundary is intentionally explicit so the project can validate deterministic accounting and replay semantics
/// independently from instruction semantics, stepping policy, and host-level persistence concerns.
/// </remarks>
public interface IExecutionBudgetTracker
{
    /// <summary>
    /// Applies one budget charge request and returns the resulting meter state.
    /// </summary>
    /// <param name="request">The budget charge request containing meter, amount, and explainability metadata.</param>
    /// <param name="executionRequest">The parent execution request that defines baseline configured limits.</param>
    /// <param name="cancellationToken">A token used to abort accounting work when host cancellation is requested.</param>
    /// <returns>
    /// A value task that resolves to a budget charge result describing remaining limits and any stop condition metadata.
    /// </returns>
    ValueTask<BudgetChargeResult> ChargeAsync(
        BudgetChargeRequest request,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the current budget snapshot for the specified execution session.
    /// </summary>
    /// <param name="sessionId">The execution session identifier whose budget state should be returned.</param>
    /// <param name="cancellationToken">A token used to abort snapshot retrieval when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the current budget snapshot for the requested session.</returns>
    ValueTask<BudgetSnapshot> GetSnapshotAsync(string sessionId, CancellationToken cancellationToken);
}
