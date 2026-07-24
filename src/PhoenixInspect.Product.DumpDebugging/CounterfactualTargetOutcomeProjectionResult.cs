using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>
/// Represents either one validated counterfactual target-exception fragment or one stable projection failure.
/// </summary>
/// <remarks>
/// This draft discriminated result prevents callers from mistaking an arbitrary terminal-looking machine value for
/// a product outcome. A successful value exists only after the projector validates the complete prior-to-terminal
/// transition and its bounded event transcript.
/// </remarks>
public sealed class CounterfactualTargetOutcomeProjectionResult
{
    private CounterfactualTargetOutcomeProjectionResult(
        CounterfactualTargetOutcomeFragment? fragment,
        EvaluationDiagnostic? failure)
    {
        Fragment = fragment;
        Failure = failure;
    }

    /// <summary>Gets whether this result contains one validated target-outcome fragment.</summary>
    public bool IsSuccess => Fragment is not null;

    /// <summary>Gets the validated immutable fragment, or <see langword="null"/> when projection failed.</summary>
    public CounterfactualTargetOutcomeFragment? Fragment { get; }

    /// <summary>Gets the stable payload-omitting projection failure, or <see langword="null"/> on success.</summary>
    public EvaluationDiagnostic? Failure { get; }

    internal static CounterfactualTargetOutcomeProjectionResult Succeeded(
        CounterfactualTargetOutcomeFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        return new CounterfactualTargetOutcomeProjectionResult(fragment, null);
    }

    internal static CounterfactualTargetOutcomeProjectionResult Failed(EvaluationDiagnostic failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new CounterfactualTargetOutcomeProjectionResult(null, failure);
    }
}
