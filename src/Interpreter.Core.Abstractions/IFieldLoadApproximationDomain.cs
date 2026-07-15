namespace Interpreter.Core.Abstractions;

/// <summary>
/// Adds the optional ability to create one explained unknown from validated non-exact field-load evidence.
/// </summary>
/// <typeparam name="TValue">The domain value representation.</typeparam>
/// <remarks>
/// The execution core probes for this capability only when an ordinary instance <c>Int32</c> load supplies
/// canonical <see cref="FieldLoadEvidence"/> and the caller explicitly enables explained-unknown execution.
/// Implementations must preserve the receiver and evidence as explanatory lineage and must not inspect partial
/// bytes or a reason code to choose a concrete scalar. Returned values remain subject to the complete
/// <see cref="IValueDomain{TValue}"/> shape and <see cref="IValuePrecisionDomain{TValue}"/> precision contracts.
///
/// This interface is provisional during the conceptual-design phase and intentionally leaves domains that support
/// only exact execution source compatible.
/// </remarks>
public interface IFieldLoadApproximationDomain<TValue> : IValuePrecisionDomain<TValue>
{
    /// <summary>Creates an explained unknown <c>Int32</c> result for one validated approximate field load.</summary>
    /// <param name="receiver">
    /// The exact receiver value consumed by the field load. The implementation may use its stable explanatory
    /// identity but must not mutate it.
    /// </param>
    /// <param name="evidence">
    /// Canonical partial or unavailable evidence whose field descriptor matches the frozen instruction.
    /// </param>
    /// <returns>
    /// A non-bottom value of structural type <see cref="TypeSig.Int32"/>, stack kind <see cref="StackKind.I4"/>, and
    /// precision <see cref="ValuePrecisionKind.ExplainedUnknown"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="receiver"/> or <paramref name="evidence"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The receiver is malformed, foreign, non-exact, or incompatible with <paramref name="evidence"/>.
    /// </exception>
    TValue CreateFieldLoadUnknown(TValue receiver, FieldLoadEvidence evidence);
}
