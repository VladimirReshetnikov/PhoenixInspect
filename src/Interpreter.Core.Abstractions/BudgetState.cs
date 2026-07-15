namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents the one deterministic execution limit consumed by the current interpreter slice.
/// </summary>
/// <param name="InstructionBudget">Remaining instruction budget.</param>
/// <remarks>
/// W4.5 maximum logical call depth is a prepared nonconsumable bound, not a decreasing budget, and is therefore kept
/// with the bound graph session while observed high-water facts live in the operational envelope. Allocation, fork,
/// and configurable traversal budgets are introduced only with operations that consume them; carrying unenforced
/// counters here would create a false safety contract.
/// </remarks>
public sealed record BudgetState(long InstructionBudget);
