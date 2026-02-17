using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Applies budget-consumption policies for deterministic, bounded execution.
/// </summary>
public interface IBudgetPolicy
{
    /// <summary>
    /// Tries to consume instruction budget for a pending execution action.
    /// </summary>
    /// <param name="budget">Current mutable budget state.</param>
    /// <param name="cost">Instruction cost to consume.</param>
    /// <returns><see langword="true"/> when consumption succeeds; otherwise <see langword="false"/>.</returns>
    bool TryConsumeInstruction(ref BudgetState budget, int cost = 1);

    /// <summary>
    /// Tries to consume allocation budget for a pending allocation action.
    /// </summary>
    /// <param name="budget">Current mutable budget state.</param>
    /// <param name="bytes">Requested allocation size in abstract bytes.</param>
    /// <returns><see langword="true"/> when consumption succeeds; otherwise <see langword="false"/>.</returns>
    bool TryConsumeAllocation(ref BudgetState budget, long bytes);
}
