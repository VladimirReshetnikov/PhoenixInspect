namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents the one deterministic execution limit consumed by the current interpreter slice.
/// </summary>
/// <param name="InstructionBudget">Remaining instruction budget.</param>
/// <remarks>
/// Allocation, call-depth, fork, and traversal budgets are introduced only with the operations that consume them;
/// carrying unenforced limit fields would create a false safety contract.
/// </remarks>
public sealed record BudgetState(long InstructionBudget);
