namespace Interpreter.Core.Execution;

/// <summary>
/// Describes whether the low-level IL machine can continue running after a micro-step request.
/// </summary>
/// <remarks>
/// This enum deliberately excludes debugger-session concepts such as Step Complete or Decision Needed. Those are
/// control-plane pause reasons layered over one or more machine transitions, not semantic outcomes of an IL
/// instruction.
/// </remarks>
public enum MachineRunStatus
{
    /// <summary>The last instruction completed and another machine step may be requested.</summary>
    Ready,

    /// <summary>The root activation returned and the machine has a terminal result.</summary>
    Completed,

    /// <summary>No instruction ran because deterministic execution budget was exhausted.</summary>
    BudgetExhausted,

    /// <summary>Execution cannot continue because required evidence or opcode support is unavailable.</summary>
    Blocked,

    /// <summary>The supplied method body or machine state violates an executable IL invariant.</summary>
    InvalidProgram,
}
