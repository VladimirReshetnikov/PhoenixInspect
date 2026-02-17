using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents mutable interpreter execution limits used to bound runtime cost.
/// </summary>
/// <param name="InstructionBudget">Remaining instruction budget.</param>
/// <param name="AllocationBudget">Remaining allocation budget in abstract units.</param>
/// <param name="MaxCallDepth">Remaining call-depth allowance.</param>
/// <param name="MaxForks">Remaining branch-fork allowance.</param>
public sealed record BudgetState(long InstructionBudget, long AllocationBudget, int MaxCallDepth, int MaxForks);
