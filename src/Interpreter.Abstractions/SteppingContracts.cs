namespace Interpreter.Abstractions;

/// <summary>
/// Identifies the high-level stepping action requested by a host during a virtual debugging session.
/// </summary>
/// <remarks>
/// This enumeration is a draft control-plane contract used to validate host interaction patterns.
/// The values are intentionally minimal and may be expanded once statement mapping and exception policy are finalized.
/// </remarks>
public enum StepCommandKind
{
    /// <summary>
    /// Requests that execution advances into the next callable boundary when one is encountered.
    /// </summary>
    Into,

    /// <summary>
    /// Requests that execution advances to the next statement boundary in the current frame, treating callees as opaque work.
    /// </summary>
    Over,

    /// <summary>
    /// Requests that execution continues until the current frame returns to its caller.
    /// </summary>
    Out,
}

/// <summary>
/// Captures canonical reasons why a step operation produced a result.
/// </summary>
/// <remarks>
/// Stop reasons are intentionally host-readable rather than interpreter-internal so that UX layers can render deterministic
/// explanations without relying on implementation-specific exception types or state-machine details.
/// </remarks>
public enum StepStopReason
{
    /// <summary>
    /// Indicates that execution reached the next statement boundary for the requested stepping command.
    /// </summary>
    StepCompleted,

    /// <summary>
    /// Indicates that execution suspended because policy required a host decision for ambiguity or nondeterministic behavior.
    /// </summary>
    DecisionRequired,

    /// <summary>
    /// Indicates that execution suspended because one or more configured budgets were exhausted.
    /// </summary>
    BudgetExceeded,

    /// <summary>
    /// Indicates that execution suspended due to an exception signal observed by the stepping control plane.
    /// </summary>
    ExceptionObserved,

    /// <summary>
    /// Indicates that execution reached the terminal state of the root frame and the session is complete.
    /// </summary>
    SessionCompleted,
}

/// <summary>
/// Describes a source-oriented location associated with an instruction offset in the current draft debug map model.
/// </summary>
/// <param name="DocumentPath">Gets the source or synthetic document path used for UI rendering.</param>
/// <param name="StartLine">Gets the one-based starting line index of the mapped span.</param>
/// <param name="StartColumn">Gets the one-based starting column index of the mapped span.</param>
/// <param name="EndLine">Gets the one-based ending line index of the mapped span.</param>
/// <param name="EndColumn">Gets the one-based ending column index of the mapped span.</param>
/// <param name="IsHidden">Gets a value indicating whether the span should be treated as hidden non-user code.</param>
/// <remarks>
/// This plain data object intentionally avoids dependency on Roslyn text primitives so the prototype remains host-neutral.
/// </remarks>
public sealed record SourceSpanDescriptor(
    string DocumentPath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    bool IsHidden);

/// <summary>
/// Defines the immutable payload for requesting one prototype stepping action.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier whose machine state should be advanced.</param>
/// <param name="Command">Gets the stepping command to perform for this operation.</param>
/// <param name="InstructionBudget">Gets the maximum number of micro-steps allowed for this command invocation.</param>
/// <param name="CaptureEvents">Gets a value indicating whether event details should be returned in the result payload.</param>
/// <remarks>
/// The request is scoped to a single command to keep host orchestration explicit while we compare stepping loop designs.
/// </remarks>
public sealed record StepRequest(
    string SessionId,
    StepCommandKind Command,
    int InstructionBudget,
    bool CaptureEvents);

/// <summary>
/// Represents a lightweight snapshot of one frame in the current virtual call stack.
/// </summary>
/// <param name="MethodIdentity">Gets the fully qualified method identity for the frame.</param>
/// <param name="InstructionOffset">Gets the current IL instruction offset for the frame instruction pointer.</param>
/// <param name="StatementId">Gets a draft statement identifier derived from the active debug map, when available.</param>
/// <param name="SourceSpan">Gets the optional source span associated with the current instruction offset.</param>
/// <remarks>
/// This data object is intentionally serializable-by-convention and keeps frame reporting independent from concrete runtime state.
/// </remarks>
public sealed record ExecutionFrameSnapshot(
    string MethodIdentity,
    int InstructionOffset,
    string? StatementId,
    SourceSpanDescriptor? SourceSpan);

/// <summary>
/// Captures one structured stepping event emitted while processing a step command.
/// </summary>
/// <param name="EventName">Gets the canonical event name, such as <c>FramePushed</c> or <c>UnknownValueCreated</c>.</param>
/// <param name="Payload">Gets event-specific key/value data intended for diagnostics and explainability rendering.</param>
/// <remarks>
/// Event payloads remain stringly typed in the draft phase so we can evolve semantics quickly across architecture iterations.
/// </remarks>
public sealed record StepEventDescriptor(
    string EventName,
    IReadOnlyDictionary<string, string> Payload);

/// <summary>
/// Represents the immutable result payload produced by a prototype stepping command.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier for correlation with request and diagnostics artifacts.</param>
/// <param name="StopReason">Gets the control-plane reason that caused stepping to return to the host.</param>
/// <param name="CallStack">Gets the current call stack snapshot ordered from root frame to active frame.</param>
/// <param name="Events">Gets the optional ordered events observed while evaluating this stepping command.</param>
/// <param name="ExplainabilityNotes">Gets notes describing conservative decisions, unknown propagation, or policy guardrails.</param>
/// <remarks>
/// The result shape intentionally favors host explainability over compactness during conceptual design.
/// </remarks>
public sealed record StepResultSnapshot(
    string SessionId,
    StepStopReason StopReason,
    IReadOnlyList<ExecutionFrameSnapshot> CallStack,
    IReadOnlyList<StepEventDescriptor> Events,
    IReadOnlyList<string> ExplainabilityNotes);

/// <summary>
/// Defines a prototype control-plane contract that advances an existing execution session by one stepping command.
/// </summary>
/// <remarks>
/// This interface is intentionally separate from full-run execution contracts so hosts can compose run/step experiences
/// independently while the architecture is still exploratory.
/// </remarks>
public interface IExecutionStepper
{
    /// <summary>
    /// Executes one stepping command against an existing session and returns an immutable snapshot of the new state boundary.
    /// </summary>
    /// <param name="request">The stepping request describing command intent, session identity, and command-level budget.</param>
    /// <param name="cancellationToken">A token used to stop command processing when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the resulting step snapshot and explainability context.</returns>
    ValueTask<StepResultSnapshot> StepAsync(StepRequest request, CancellationToken cancellationToken);
}
