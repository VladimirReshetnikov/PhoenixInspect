using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Provides a minimal deterministic budget policy suitable for draft interpreter integration tests.
/// </summary>
public sealed class InstructionBudgetPolicy : IBudgetPolicy
{
    /// <inheritdoc />
    public bool TryConsumeInstruction(ref BudgetState budget, int cost = 1)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), cost, "Instruction cost must be non-negative.");
        }

        if (budget.InstructionBudget < cost)
        {
            return false;
        }

        budget = budget with { InstructionBudget = budget.InstructionBudget - cost };
        return true;
    }

    /// <inheritdoc />
    public bool TryConsumeAllocation(ref BudgetState budget, long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Allocation cost must be non-negative.");
        }

        if (budget.AllocationBudget < bytes)
        {
            return false;
        }

        budget = budget with { AllocationBudget = budget.AllocationBudget - bytes };
        return true;
    }
}
