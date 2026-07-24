using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Core.Execution;

/// <summary>
/// Provides a minimal deterministic budget policy suitable for interpreter integration tests.
/// </summary>
public sealed class InstructionBudgetPolicy : IBudgetPolicy
{
    /// <inheritdoc />
    public bool TryConsumeInstruction(ref BudgetState budget, int cost = 1)
    {
        ArgumentNullException.ThrowIfNull(budget);
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
}
