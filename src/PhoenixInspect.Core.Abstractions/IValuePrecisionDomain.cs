namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Classifies whether a domain value is exact or represents semantic top with or without a usable explanation.
/// </summary>
public enum ValuePrecisionKind
{
    /// <summary>The value denotes one exact runtime value and requires no unknown lineage.</summary>
    Exact,

    /// <summary>The value is semantic top and carries a validated explanatory lineage root.</summary>
    ExplainedUnknown,

    /// <summary>The value is semantic top but has no validated explanation suitable for execution.</summary>
    UnexplainedUnknown,
}

/// <summary>
/// Adds execution-boundary precision classification to a value domain without widening the minimum
/// <see cref="IValueDomain{TValue}"/> contract.
/// </summary>
/// <typeparam name="TValue">The domain value representation being classified.</typeparam>
/// <remarks>
/// Lattice operations still treat every same-typed unknown as canonical semantic top. This optional capability
/// exists only so an execution engine can distinguish a provenance-bearing unknown from an ungrounded top created
/// by pure lattice algebra. Implementations must not inspect an explanation to select a concrete semantic result.
/// </remarks>
public interface IValuePrecisionDomain<TValue> : IValueDomain<TValue>
{
    /// <summary>Classifies one non-bottom domain value for execution-boundary validation.</summary>
    /// <param name="value">The value to classify.</param>
    /// <returns>
    /// <see cref="ValuePrecisionKind.Exact"/> for one exact value,
    /// <see cref="ValuePrecisionKind.ExplainedUnknown"/> for a grounded semantic top, or
    /// <see cref="ValuePrecisionKind.UnexplainedUnknown"/> for an ungrounded semantic top.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is malformed, foreign to the implementation, or is lattice bottom.
    /// </exception>
    ValuePrecisionKind GetPrecision(TValue value);
}
