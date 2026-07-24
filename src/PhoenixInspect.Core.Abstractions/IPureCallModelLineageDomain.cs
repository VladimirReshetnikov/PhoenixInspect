using System.Collections.Immutable;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Adds the optional ability to ground one pure model's explained-unknown return in its admitted direct-call inputs.
/// </summary>
/// <typeparam name="TValue">The interpreter domain value representation.</typeparam>
/// <remarks>
/// The execution core probes for this draft W4.6 capability only after the frozen pure model has returned
/// <see cref="PureCallModelOutcomeKind.UnknownReturn"/> and the model outcome, call site, and original
/// metadata-ordered argument vector have otherwise passed structural validation. Implementations atomically preserve
/// each outcome-relevant explained argument through a parameter-indexed direct-call transform and create one modeled
/// return transform over the complete two-argument relation. Exact arguments are embedded in that return transform
/// and do not receive degenerate lineage nodes.
///
/// The operation must validate the complete vector before publishing any lineage. A model cannot introduce an
/// ungrounded unknown: at least one argument must already be an owned explained-unknown structural <c>Int32</c>.
/// Lineage remains an explanatory axis and must not influence the semantic result chosen by the model.
///
/// This interface is provisional during the conceptual-design phase. Domains that do not implement it cannot execute
/// a modeled unknown return; callers must report the missing optional capability rather than fabricate provenance.
/// </remarks>
public interface IPureCallModelLineageDomain<TValue> : IValuePrecisionDomain<TValue>
{
    /// <summary>
    /// Creates one explained-unknown modeled return atomically from the original admitted direct-call arguments.
    /// </summary>
    /// <param name="callSite">The exact frozen caller, call IL offset, and body-free modeled target identity.</param>
    /// <param name="modelIdentity">The stable identifier and exact semantic version of the frozen pure model.</param>
    /// <param name="arguments">
    /// The initialized, exactly two-element metadata-ordered argument vector presented to the model. Every value is
    /// an exact or owned explained-unknown structural <c>Int32</c>, and at least one value is explained unknown.
    /// </param>
    /// <returns>
    /// A structural <c>Int32</c> unknown whose root is one canonical modeled-return transform. Its unknown operands
    /// refer to parameter-indexed direct-call transforms created in the same atomic operation; exact operands are
    /// embedded canonically without allocating lineage nodes.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="callSite"/> or <paramref name="modelIdentity"/> is default or malformed;
    /// <paramref name="arguments"/> is default or does not contain exactly two values; every argument is exact; or a
    /// value is null, bottom, unexplained, foreign, non-<c>Int32</c>, or otherwise violates executable precision.
    /// No modeled-call lineage node may be published when validation fails.
    /// </exception>
    TValue CreateModeledReturnUnknown(
        DirectCallSiteIdentity callSite,
        PureCallModelIdentity modelIdentity,
        ImmutableArray<TValue> arguments);
}
