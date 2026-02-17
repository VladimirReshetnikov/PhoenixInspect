namespace Interpreter.Abstractions;

/// <summary>
/// Represents the lifecycle status of a virtual task tracked by the prototype async runtime.
/// </summary>
/// <remarks>
/// Status values are intentionally coarse during conceptual design so we can validate stepping and explainability flows
/// before committing to a production-ready async object model.
/// </remarks>
public enum VirtualTaskStatus
{
    /// <summary>
    /// Indicates that the virtual task has been allocated but has not yet executed user code.
    /// </summary>
    Created,

    /// <summary>
    /// Indicates that the associated async activation is currently running and may still suspend.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates that execution is paused while waiting for one or more awaited dependencies.
    /// </summary>
    Suspended,

    /// <summary>
    /// Indicates that execution completed successfully and a result (or void completion) is available.
    /// </summary>
    Completed,

    /// <summary>
    /// Indicates that execution completed due to an exception propagated through async builder semantics.
    /// </summary>
    Faulted,

    /// <summary>
    /// Indicates that completion was canceled by host policy or cancellation propagation rules.
    /// </summary>
    Canceled,
}

/// <summary>
/// Describes a virtual task snapshot returned by prototype async-runtime contracts.
/// </summary>
/// <param name="TaskId">Gets the stable virtual task identifier used for diagnostics and stepping correlation.</param>
/// <param name="ProducerMethodIdentity">Gets the method identity responsible for creating this task.</param>
/// <param name="Status">Gets the current lifecycle status of the virtual task.</param>
/// <param name="ResultTypeDisplayName">Gets the display name for the task result type, such as <c>System.Int32</c> or <c>void</c>.</param>
/// <param name="CompletionSummary">Gets a concise human-readable summary of completion or suspension state.</param>
/// <remarks>
/// The snapshot is intentionally string-oriented so host tooling can experiment with UX and explainability rendering
/// without taking dependencies on finalized value-domain contracts.
/// </remarks>
public sealed record VirtualTaskSnapshot(
    string TaskId,
    string ProducerMethodIdentity,
    VirtualTaskStatus Status,
    string ResultTypeDisplayName,
    string CompletionSummary);

/// <summary>
/// Defines the outcome category for registering an await operation in the draft async runtime.
/// </summary>
/// <remarks>
/// This contract captures the minimum distinctions needed for stepping control flow while awaiter support is still expanding.
/// </remarks>
public enum AwaitRegistrationOutcome
{
    /// <summary>
    /// Indicates that await registration succeeded and the current activation is now suspended.
    /// </summary>
    Registered,

    /// <summary>
    /// Indicates that the awaitable was already complete and execution should continue synchronously.
    /// </summary>
    CompletedSynchronously,

    /// <summary>
    /// Indicates that the awaiter pattern could not be interpreted in the current prototype capabilities.
    /// </summary>
    UnsupportedAwaiter,
}

/// <summary>
/// Captures an immutable request to register one await point against the virtual async runtime.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier used to correlate await behavior with diagnostics.</param>
/// <param name="ActivationId">Gets the async activation identifier that is about to suspend.</param>
/// <param name="AwaiterTypeDisplayName">Gets the awaiter type display name used for capability checks and diagnostics.</param>
/// <param name="AwaitedTaskId">Gets the optional virtual task identifier being awaited, when one is known.</param>
/// <remarks>
/// The request intentionally avoids exposing concrete runtime objects so hosts can test async orchestration semantics in
/// dump-only scenarios where object reconstruction may be partial.
/// </remarks>
public sealed record AwaitRegistrationRequest(
    string SessionId,
    string ActivationId,
    string AwaiterTypeDisplayName,
    string? AwaitedTaskId);

/// <summary>
/// Represents the result of one await-registration attempt in the prototype async runtime.
/// </summary>
/// <param name="Outcome">Gets the high-level await registration outcome category.</param>
/// <param name="ContinuationActivationId">Gets the activation identifier that should resume when the await completes.</param>
/// <param name="Explanation">Gets a host-readable explanation describing why this outcome was produced.</param>
/// <remarks>
/// This result surface is explainability-first and should be treated as draft-only while async policy boundaries are refined.
/// </remarks>
public sealed record AwaitRegistrationResult(
    AwaitRegistrationOutcome Outcome,
    string ContinuationActivationId,
    string Explanation);

/// <summary>
/// Defines a prototype contract for managing virtual task and await orchestration without executing real runtime schedulers.
/// </summary>
/// <remarks>
/// This interface is intentionally scoped to conceptual design and may be split into narrower services once activation,
/// builder, and scheduler responsibilities are validated through prototype iterations.
/// </remarks>
public interface IVirtualTaskRuntime
{
    /// <summary>
    /// Creates a new virtual task snapshot for the specified producer method and marks it as available for async tracking.
    /// </summary>
    /// <param name="sessionId">The execution session identifier that owns the virtual task lifecycle.</param>
    /// <param name="producerMethodIdentity">The fully qualified method identity that produced the task.</param>
    /// <param name="resultTypeDisplayName">The display name of the task result type for diagnostics and explainability.</param>
    /// <param name="cancellationToken">A token that cancels task creation when host policy aborts async orchestration.</param>
    /// <returns>A value task that resolves to the created virtual task snapshot.</returns>
    ValueTask<VirtualTaskSnapshot> CreateTaskAsync(
        string sessionId,
        string producerMethodIdentity,
        string resultTypeDisplayName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a terminal state transition to an existing virtual task and returns the updated snapshot.
    /// </summary>
    /// <param name="sessionId">The execution session identifier used for ownership and diagnostics checks.</param>
    /// <param name="taskId">The virtual task identifier to update.</param>
    /// <param name="status">The requested terminal status, typically <see cref="VirtualTaskStatus.Completed"/>, <see cref="VirtualTaskStatus.Faulted"/>, or <see cref="VirtualTaskStatus.Canceled"/>.</param>
    /// <param name="completionSummary">A concise summary explaining completion, fault, or cancellation details.</param>
    /// <param name="cancellationToken">A token that cancels transition work when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the updated virtual task snapshot.</returns>
    ValueTask<VirtualTaskSnapshot> CompleteTaskAsync(
        string sessionId,
        string taskId,
        VirtualTaskStatus status,
        string completionSummary,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers an await point for an activation and returns how control flow should proceed.
    /// </summary>
    /// <param name="request">The await-registration request describing activation identity and awaiter metadata.</param>
    /// <param name="cancellationToken">A token that cancels await registration when the host ends the step operation.</param>
    /// <returns>A value task that resolves to the await-registration result for stepping and diagnostics routing.</returns>
    ValueTask<AwaitRegistrationResult> RegisterAwaitAsync(
        AwaitRegistrationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the latest virtual task snapshot for a known task identifier.
    /// </summary>
    /// <param name="sessionId">The execution session identifier used to scope the lookup operation.</param>
    /// <param name="taskId">The virtual task identifier to read.</param>
    /// <param name="cancellationToken">A token that cancels lookup work when host policy requests stop.</param>
    /// <returns>A value task that resolves to the current virtual task snapshot.</returns>
    ValueTask<VirtualTaskSnapshot> GetTaskAsync(
        string sessionId,
        string taskId,
        CancellationToken cancellationToken);
}
