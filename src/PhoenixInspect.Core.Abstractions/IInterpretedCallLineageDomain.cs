using System.Collections.Immutable;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Adds the optional ability to preserve explained-unknown lineage across one admitted interpreted-call boundary.
/// </summary>
/// <typeparam name="TValue">The domain value representation.</typeparam>
/// <remarks>
/// The execution core probes for this draft W4.5 capability only after a direct call has been structurally admitted,
/// its metadata-ordered argument vector has been validated, and instruction/depth invariants have succeeded. Exact
/// <c>Int32</c> values pass through unchanged. Each explained-unknown argument receives a parameter-indexed call
/// transform, and an explained-unknown callee result receives one interpreted-return transform. Lineage remains an
/// explanatory axis: implementations must not inspect it to choose a concrete semantic result.
///
/// Argument transformation is deliberately batch-oriented. An implementation must validate the complete vector and
/// either return the complete transformed vector or fail without publishing a partial vector or partially interning
/// its call-boundary nodes. Returned values remain subject to the complete <see cref="IValueDomain{TValue}"/> shape
/// and <see cref="IValuePrecisionDomain{TValue}"/> precision contracts.
///
/// This interface is provisional during the conceptual-design phase. Domains that support exact execution only need
/// not implement it; the caller must report that missing optional capability rather than fabricating lineage.
/// </remarks>
public interface IInterpretedCallLineageDomain<TValue> : IValuePrecisionDomain<TValue>
{
    /// <summary>
    /// Applies the admitted direct-call boundary to a complete metadata-ordered argument vector atomically.
    /// </summary>
    /// <param name="callSite">The exact caller, call IL offset, and interpreted callee identity.</param>
    /// <param name="arguments">
    /// The initialized, exactly two-element metadata-ordered argument vector. Every current W4 value is an exact or
    /// owned explained-unknown structural <c>Int32</c>; the position in this vector is parameter index zero or one.
    /// </param>
    /// <returns>
    /// An equally sized immutable vector. Exact values are preserved unchanged, while each explained unknown refers
    /// to a canonical call-argument transform over its prior lineage root.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="callSite"/> is default or malformed; <paramref name="arguments"/> is default or does not
    /// contain exactly two values; or any value is null, bottom, unexplained, foreign, non-<c>Int32</c>, or otherwise
    /// violates the executable precision contract. No call-boundary node may be published when validation fails.
    /// </exception>
    ImmutableArray<TValue> TransformInterpretedCallArguments(
        DirectCallSiteIdentity callSite,
        ImmutableArray<TValue> arguments);

    /// <summary>Applies the admitted interpreted-return boundary to one validated callee result.</summary>
    /// <param name="callSite">
    /// The exact frozen call identity whose callee produced <paramref name="returnedValue"/>.
    /// </param>
    /// <param name="returnedValue">
    /// The exact or owned explained-unknown structural <c>Int32</c> result popped from the interpreted callee.
    /// </param>
    /// <returns>
    /// The exact input unchanged, or an explained unknown rooted at one canonical interpreted-return transform.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="returnedValue"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="callSite"/> is default or malformed, or <paramref name="returnedValue"/> is bottom,
    /// unexplained, foreign, non-<c>Int32</c>, or otherwise violates the executable precision contract.
    /// </exception>
    TValue TransformInterpretedReturn(
        DirectCallSiteIdentity callSite,
        TValue returnedValue);
}
