using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Carries a typed ClrMD adapter result without collapsing partial or missing evidence into a Boolean.
/// </summary>
/// <typeparam name="T">Reference type used for the selected runtime evidence.</typeparam>
/// <remarks>
/// A non-exact result may still carry a value when that value preserves useful partial provenance. For example, a
/// method can be selected uniquely while its nested raw code read is partial. Consumers must inspect
/// <see cref="Status"/> rather than treating <see cref="Value"/> presence as exactness.
/// </remarks>
public sealed class ClrmdEvidenceResult<T>
    where T : class
{
    private ClrmdEvidenceResult(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        T? value,
        ImmutableArray<MemoryReadResult> evidence,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds)
    {
        Status = status;
        Issue = issue;
        Value = value;
        Evidence = evidence;
        AppliedBounds = appliedBounds;
    }

    /// <summary>
    /// Gets the evidence-selection status.
    /// </summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>
    /// Gets the stable adapter reason code; exact results use <see cref="ClrmdValueIssue.None"/>.
    /// </summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>
    /// Gets the selected evidence or partial provenance when one is available.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets whether this result carries a value. This does not imply that <see cref="Status"/> is exact.
    /// </summary>
    public bool HasValue => Value is not null;

    /// <summary>
    /// Gets ordered raw memory reads retained even when partial bytes prevented construction of <see cref="Value"/>.
    /// </summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }

    /// <summary>
    /// Gets only deterministic bounds whose guarded adapter operation was reached while producing this result.
    /// Configured limits on unvisited paths are absent, and entries are normalized into ordinal name order.
    /// </summary>
    public ImmutableArray<EvaluationDeterministicBound> AppliedBounds { get; }

    internal static ClrmdEvidenceResult<T> Create(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        T? value = null,
        ImmutableArray<MemoryReadResult> evidence = default,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds = default)
    {
        if ((status == ClrmdEvidenceStatus.Exact) != (issue == ClrmdValueIssue.None))
        {
            throw new ArgumentException("Only exact evidence results may use the None issue code.", nameof(issue));
        }

        if (status == ClrmdEvidenceStatus.Exact && value is null)
        {
            throw new ArgumentNullException(nameof(value), "An exact evidence result requires a value.");
        }

        var normalizedBounds = appliedBounds.IsDefault
            ? ImmutableArray<EvaluationDeterministicBound>.Empty
            : appliedBounds;
        if (normalizedBounds.Any(static bound => bound is null))
        {
            throw new ArgumentException("Applied bounds cannot contain null entries.", nameof(appliedBounds));
        }

        normalizedBounds = normalizedBounds
            .OrderBy(static bound => bound.Name, StringComparer.Ordinal)
            .ToImmutableArray();
        for (var index = 1; index < normalizedBounds.Length; index++)
        {
            if (string.Equals(normalizedBounds[index - 1].Name, normalizedBounds[index].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"The applied bound name '{normalizedBounds[index].Name}' occurs more than once.",
                    nameof(appliedBounds));
            }
        }

        return new ClrmdEvidenceResult<T>(
            status,
            issue,
            value,
            evidence.IsDefault ? ImmutableArray<MemoryReadResult>.Empty : evidence,
            normalizedBounds);
    }
}
