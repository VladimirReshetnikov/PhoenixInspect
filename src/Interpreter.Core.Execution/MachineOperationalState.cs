using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Carries deterministic execution bookkeeping that must not participate in semantic-state equality or joins.
/// </summary>
/// <param name="Budget">The remaining instruction allowance enforced by the current slice.</param>
/// <remarks>
/// Future analysis fixpoints must compare and join <see cref="MachineState{TValue,TMemory}"/> through an explicit
/// semantic comparer, while excluding this envelope. Keeping decreasing budgets separate prevents traversal
/// history from blocking convergence. Step transcripts should still record this operational state explicitly.
/// </remarks>
public sealed record MachineOperationalState(BudgetState Budget);
