namespace Interpreter.Abstractions;

/// <summary>
/// Represents the runtime classification for an exception signal observed during prototype interpretation.
/// </summary>
/// <remarks>
/// The classification intentionally separates machine-oriented signal categories from host UX behavior so the team can
/// iterate on exception policy independently from engine mechanics during the draft phase.
/// </remarks>
public enum ExceptionSignalKind
{
    /// <summary>
    /// Indicates that an exception object was thrown by the currently executing instruction.
    /// </summary>
    Throw,

    /// <summary>
    /// Indicates that an existing exception signal was re-thrown without creating a new exception object.
    /// </summary>
    Rethrow,

    /// <summary>
    /// Indicates that execution reached a catch handler boundary and consumed an in-flight exception signal.
    /// </summary>
    CatchHandlerEntered,

    /// <summary>
    /// Indicates that execution reached a finally or fault region while unwinding due to an exception signal.
    /// </summary>
    FinallyHandlerEntered,

    /// <summary>
    /// Indicates that unwinding propagated beyond the current root frame and terminated the session.
    /// </summary>
    Unhandled,
}

/// <summary>
/// Defines high-level handling dispositions returned by prototype exception-policy evaluation.
/// </summary>
/// <remarks>
/// Dispositions are intentionally coarse so host experiences can be validated before the repository commits to final
/// exception semantics or debugger parity guarantees.
/// </remarks>
public enum ExceptionHandlingDisposition
{
    /// <summary>
    /// Indicates that stepping should continue automatically after recording diagnostics for the signal.
    /// </summary>
    Continue,

    /// <summary>
    /// Indicates that the interpreter should stop and request an explicit host decision before proceeding.
    /// </summary>
    BreakForDecision,

    /// <summary>
    /// Indicates that execution should stop immediately and finalize the session as unsupported or unsafe.
    /// </summary>
    AbortExecution,
}

/// <summary>
/// Captures immutable details for one exception signal observed by the prototype execution loop.
/// </summary>
/// <param name="SessionId">Gets the owning execution session identifier used for diagnostics correlation.</param>
/// <param name="MethodIdentity">Gets the fully qualified method identity where the signal was observed.</param>
/// <param name="InstructionOffset">Gets the IL offset associated with the signal when the source instruction is known.</param>
/// <param name="SignalKind">Gets the runtime signal classification describing the exception lifecycle transition.</param>
/// <param name="ExceptionTypeDisplayName">Gets the display name of the exception type, such as <c>System.InvalidOperationException</c>.</param>
/// <param name="ExceptionMessage">Gets the optional exception message captured for explainability output.</param>
/// <remarks>
/// This record keeps exception transport host-neutral and avoids concrete runtime exception object dependencies while
/// the memory model and binding contracts are still under active design.
/// </remarks>
public sealed record ExceptionSignalDescriptor(
    string SessionId,
    string MethodIdentity,
    int? InstructionOffset,
    ExceptionSignalKind SignalKind,
    string ExceptionTypeDisplayName,
    string? ExceptionMessage);

/// <summary>
/// Describes the policy decision returned for one exception signal evaluation request.
/// </summary>
/// <param name="Disposition">Gets the handling disposition the interpreter should apply for the signal.</param>
/// <param name="StopReasonCode">Gets an optional machine-readable stop reason code when the disposition requests a stop.</param>
/// <param name="ExplainabilityNote">Gets a host-visible explanation describing why the policy produced this decision.</param>
/// <remarks>
/// This output intentionally mirrors existing stop-descriptor conventions so hosts can reuse routing logic
/// across different stop categories during prototype integration.
/// </remarks>
public sealed record ExceptionPolicyDecision(
    ExceptionHandlingDisposition Disposition,
    string? StopReasonCode,
    string ExplainabilityNote);

/// <summary>
/// Evaluates exception signals and returns deterministic handling decisions for prototype stepping and execution loops.
/// </summary>
/// <remarks>
/// This contract is draft-only and does not imply a final commitment about CLR-exact exception semantics.
/// It exists to explore explainable and cancellation-aware exception behavior in dump-backed sessions.
/// </remarks>
public interface IExceptionPolicy
{
    /// <summary>
    /// Evaluates one exception signal and determines the handling disposition for the active session.
    /// </summary>
    /// <param name="signal">The exception signal descriptor observed by the interpreter.</param>
    /// <param name="request">The parent execution request carrying session and cancellation context.</param>
    /// <param name="cancellationToken">A token used to cancel policy evaluation when the host requests stop.</param>
    /// <returns>A value task that resolves to the policy decision for the supplied exception signal.</returns>
    ValueTask<ExceptionPolicyDecision> EvaluateAsync(
        ExceptionSignalDescriptor signal,
        IExecutionRequest request,
        CancellationToken cancellationToken);
}
