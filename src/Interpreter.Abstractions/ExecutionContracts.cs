namespace Interpreter.Abstractions;

/// <summary>
/// Represents the lifecycle state of a prototype interpreter session.
/// </summary>
/// <remarks>
/// This enumeration is part of an early draft contract and is expected to evolve as design decisions around
/// suspension, rewind semantics, and host-controlled recovery become more concrete.
/// </remarks>
public enum ExecutionLifecycleState
{
    /// <summary>
    /// Indicates that the session has been created but has not started any IL execution.
    /// </summary>
    Created,

    /// <summary>
    /// Indicates that the session is currently executing instructions and may emit diagnostics.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates that execution has completed due to budget limits, stop conditions, or natural termination.
    /// </summary>
    Completed,
}

/// <summary>
/// Carries immutable budget constraints for one interpreter run.
/// </summary>
/// <param name="InstructionBudget">Gets the maximum number of instructions the engine is allowed to evaluate before it must stop.</param>
/// <param name="BranchBudget">Gets the maximum number of branch forks the engine can materialize in a single session.</param>
/// <param name="WallClockBudget">Gets an optional wall-clock budget used by hosts that enforce elapsed-time limits.</param>
/// <remarks>
/// This record is intentionally minimal in the prototype stage and should be treated as a placeholder for richer policy
/// objects that can include widening cadence, memory quotas, and per-domain safeguards.
/// </remarks>
public sealed record ExecutionBudget(
    int InstructionBudget,
    int BranchBudget,
    TimeSpan? WallClockBudget);

/// <summary>
/// Describes the immutable request payload used to start one prototype interpretation session.
/// </summary>
/// <remarks>
/// The shape of this contract is intentionally conservative so we can validate solution boundaries before committing
/// to concrete metadata and memory-model payloads.
/// </remarks>
public interface IExecutionRequest
{
    /// <summary>
    /// Gets a stable identifier that correlates execution artifacts across logs, traces, and host diagnostics.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Gets the fully qualified method identity for the entry point to interpret.
    /// </summary>
    string EntryMethodIdentity { get; }

    /// <summary>
    /// Gets the budget policy that bounds deterministic execution behavior for this request.
    /// </summary>
    ExecutionBudget Budget { get; }
}


/// <summary>
/// Represents the high-level category describing why execution stopped or yielded a snapshot.
/// </summary>
/// <remarks>
/// Categories are intentionally broad during the prototype phase so hosts can start wiring UX flows without
/// prematurely depending on fine-grained engine internals.
/// </remarks>
public enum ExecutionStopCategory
{
    /// <summary>
    /// Indicates that execution reached the natural end of the target method body.
    /// </summary>
    Completed,

    /// <summary>
    /// Indicates that execution halted because one or more configured budgets were exhausted.
    /// </summary>
    BudgetExceeded,

    /// <summary>
    /// Indicates that execution yielded due to an explicit host- or debugger-driven stop request.
    /// </summary>
    HostStop,

    /// <summary>
    /// Indicates that execution terminated because unsupported behavior forced conservative bailout.
    /// </summary>
    Unsupported,
}

/// <summary>
/// Describes the stop condition attached to one execution result snapshot.
/// </summary>
/// <param name="Category">Gets the high-level stop category used by hosts to map UX behavior.</param>
/// <param name="Code">Gets a stable machine-readable code value such as <c>budget:instruction</c> or <c>unsupported:tailcall</c>.</param>
/// <param name="Message">Gets a human-readable summary suitable for logs and explainability panes.</param>
/// <remarks>
/// This record complements <see cref="IExecutionResult.StopReason"/> rather than replacing it immediately so prototype consumers
/// can migrate incrementally as the contract surface evolves.
/// </remarks>
public sealed record ExecutionStopDescriptor(
    ExecutionStopCategory Category,
    string Code,
    string Message);

/// <summary>
/// Represents an immutable snapshot describing the current outcome of a prototype execution session.
/// </summary>
/// <remarks>
/// In this draft phase, the result surface emphasizes explainability-first fields rather than concrete value modeling.
/// </remarks>
public interface IExecutionResult
{
    /// <summary>
    /// Gets the session identifier copied from the originating request for correlation purposes.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Gets the lifecycle state reached when the engine produced this result snapshot.
    /// </summary>
    ExecutionLifecycleState State { get; }

    /// <summary>
    /// Gets a host-readable reason describing why execution stopped or yielded.
    /// </summary>
    string StopReason { get; }

    /// <summary>
    /// Gets structured stop details that classify the stop outcome for host policy and UX routing.
    /// </summary>
    ExecutionStopDescriptor StopDescriptor { get; }

    /// <summary>
    /// Gets a collection of explainability notes generated during execution, including unknown propagation rationale.
    /// </summary>
    IReadOnlyList<string> ExplainabilityNotes { get; }
}
