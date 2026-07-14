using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>
/// Carries either one immutable admitted dump-query plan or the complete host-facing result explaining why parsing or
/// binding could not produce that plan.
/// </summary>
public sealed class DumpQueryPreparationResult
{
    private DumpQueryPreparationResult(
        DumpQueryPlan? plan,
        EvaluationResult<DumpQueryValue>? failure)
    {
        Plan = plan;
        Failure = failure;
    }

    /// <summary>Gets whether preparation produced an admitted plan.</summary>
    public bool IsSuccess => Plan is not null;

    /// <summary>Gets the immutable bound plan on success; otherwise, gets <see langword="null"/>.</summary>
    public DumpQueryPlan? Plan { get; }

    /// <summary>
    /// Gets the complete invalid or blocked result on failure; otherwise, gets <see langword="null"/>. The failure
    /// preserves parser, root-selection, and member-binding explanation available at the stopping boundary.
    /// </summary>
    public EvaluationResult<DumpQueryValue>? Failure { get; }

    internal static DumpQueryPreparationResult Success(DumpQueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new DumpQueryPreparationResult(plan, null);
    }

    internal static DumpQueryPreparationResult Failed(EvaluationResult<DumpQueryValue> failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new DumpQueryPreparationResult(null, failure);
    }
}
