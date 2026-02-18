namespace Interpreter.Core.Execution;

/// <summary>
/// Indicates why micro-step execution stopped after a step request.
/// </summary>
public enum StopReason
{
    /// <summary>
    /// Indicates execution can continue with subsequent steps.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates execution reached completion (for example, the root frame returned).
    /// </summary>
    Completed,

    /// <summary>
    /// Indicates execution stopped because budget constraints were exhausted.
    /// </summary>
    BudgetExceeded,

    /// <summary>
    /// Indicates execution stopped due to an interpreter fault or missing dependency.
    /// </summary>
    Faulted,
}
