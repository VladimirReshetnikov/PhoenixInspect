using Interpreter.Abstractions;

namespace Interpreter.Diagnostics;

/// <summary>
/// Captures a structured prototype diagnostic event emitted during interpretation.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier used to correlate this event with other traces.</param>
/// <param name="EventName">Gets the canonical event name, for example <c>UnknownValueCreated</c> or <c>BudgetExceeded</c>.</param>
/// <param name="InstructionOffset">Gets the optional IL offset associated with this event when known.</param>
/// <param name="Payload">Gets a shallow key/value payload with event-specific diagnostic properties.</param>
/// <remarks>
/// This plain data object is intentionally lightweight to keep prototype diagnostics transport and persistence concerns decoupled.
/// </remarks>
public sealed record ExecutionDiagnosticEvent(
    string SessionId,
    string EventName,
    int? InstructionOffset,
    IReadOnlyDictionary<string, string> Payload);

/// <summary>
/// Describes host-configurable filtering rules for prototype diagnostic emission.
/// </summary>
/// <param name="IncludeVerboseEvents">Gets a value indicating whether verbose diagnostic events should be included in event streams.</param>
/// <param name="IncludedEventNames">Gets explicit event names that should always be emitted even when broad filtering is active.</param>
/// <remarks>
/// Filtering semantics are draft-only and will likely move into richer policy objects after host integration experiments.
/// </remarks>
public sealed record DiagnosticsFilterOptions(
    bool IncludeVerboseEvents,
    IReadOnlySet<string> IncludedEventNames);

/// <summary>
/// Publishes structured diagnostic events emitted by prototype interpreter components.
/// </summary>
/// <remarks>
/// Event names and payload shape are still exploratory and may change rapidly while we validate explainability expectations
/// with hosts and documentation reviews.
/// </remarks>
public interface IExecutionDiagnosticsSink
{
    /// <summary>
    /// Records a structured event associated with a specific execution session.
    /// </summary>
    /// <param name="diagnosticEvent">The event payload describing what happened and how it should be correlated.</param>
    void Record(ExecutionDiagnosticEvent diagnosticEvent);

    /// <summary>
    /// Records a terminal summary for the specified execution result.
    /// </summary>
    /// <param name="result">The final or intermediate execution result produced by the interpreter engine.</param>
    void RecordSummary(IExecutionResult result);
}
