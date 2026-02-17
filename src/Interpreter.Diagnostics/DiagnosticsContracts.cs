using Interpreter.Abstractions;

namespace Interpreter.Diagnostics;

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
    /// <param name="sessionId">The execution session identifier used for correlating diagnostic streams.</param>
    /// <param name="eventName">The canonical event name, for example <c>UnknownValueCreated</c> or <c>BudgetExceeded</c>.</param>
    /// <param name="payload">A shallow key/value payload with event-specific diagnostic properties.</param>
    void Record(string sessionId, string eventName, IReadOnlyDictionary<string, string> payload);

    /// <summary>
    /// Records a terminal summary for the specified execution result.
    /// </summary>
    /// <param name="result">The final or intermediate execution result produced by the interpreter engine.</param>
    void RecordSummary(IExecutionResult result);
}
