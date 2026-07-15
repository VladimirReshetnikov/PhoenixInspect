namespace Interpreter.Core.Abstractions;

/// <summary>Classifies the declared semantic confidence of one pure call model.</summary>
/// <remarks>
/// A descriptor records capability independently of scenario admission. Preparation policy decides which declared
/// classifications it admits; in particular, the closed W4 scenario admits only <see cref="Exact"/> and must reject
/// every weaker classification rather than silently changing semantics.
/// </remarks>
public enum PureCallModelConfidence
{
    /// <summary>The model implements the exact admitted relation for every supported argument shape.</summary>
    Exact = 0,

    /// <summary>The model returns a useful approximation without claiming the exact admitted relation.</summary>
    BestEffort = 1,

    /// <summary>The model implements only a documented subset of the target relation or admitted argument space.</summary>
    Partial = 2,

    /// <summary>The model cannot preserve semantics for the target's structural data-layout requirements.</summary>
    UnsupportedLayout = 3,
}

/// <summary>
/// Freezes the identity, exact structural target, confidence, and normalized effects of one pure call model.
/// </summary>
/// <remarks>
/// Target selection is content-based over <see cref="ResolvedMethodCallTarget.Method"/> and
/// <see cref="ResolvedMethodCallTarget.Signature"/>. Display names are absent. A descriptor may retain
/// <see cref="EvaluationEffectStatus.Unsupported"/> so preparation can report an explicit effect rejection, but
/// <see cref="EvaluationEffectStatus.VirtualOnly"/> and <see cref="EvaluationEffectStatus.Modeled"/> are not
/// normalized W4 pure-model declarations and are rejected at construction.
/// </remarks>
public sealed record PureCallModelDescriptor
{
    /// <summary>Creates one immutable structural pure-model descriptor.</summary>
    /// <param name="identity">The stable model identifier and exact semantic version.</param>
    /// <param name="target">The exact body-independent MethodDef and call signature covered by the model.</param>
    /// <param name="confidence">The model's declared semantic confidence, independent of scenario admission.</param>
    /// <param name="effects">The normalized effect declaration.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="identity"/> is default, <paramref name="target"/> has a default MethodDef, or
    /// <paramref name="effects"/> is not <see cref="EvaluationEffectStatus.None"/> or
    /// <see cref="EvaluationEffectStatus.Unsupported"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="confidence"/> is undefined.</exception>
    public PureCallModelDescriptor(
        PureCallModelIdentity identity,
        ResolvedMethodCallTarget target,
        PureCallModelConfidence confidence,
        EvaluationEffectStatus effects)
    {
        if (!identity.IsInitialized)
        {
            throw new ArgumentException("A pure-model descriptor requires a non-default identity.", nameof(identity));
        }

        ArgumentNullException.ThrowIfNull(target);
        if (target.Method == default)
        {
            throw new ArgumentException(
                "A pure-model descriptor requires an exact non-default structural target.",
                nameof(target));
        }

        if (!Enum.IsDefined(confidence))
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        if (effects is not (EvaluationEffectStatus.None or EvaluationEffectStatus.Unsupported))
        {
            throw new ArgumentException(
                "The closed W4 model contract accepts only normalized None or Unsupported effects.",
                nameof(effects));
        }

        Identity = identity;
        Target = target;
        Confidence = confidence;
        Effects = effects;
    }

    /// <summary>Gets the stable model identifier and exact semantic version.</summary>
    public PureCallModelIdentity Identity { get; }

    /// <summary>Gets the exact body-independent MethodDef and signature covered by the model.</summary>
    public ResolvedMethodCallTarget Target { get; }

    /// <summary>Gets the model's semantic confidence classification.</summary>
    public PureCallModelConfidence Confidence { get; }

    /// <summary>Gets the normalized effect declaration independently of model use.</summary>
    public EvaluationEffectStatus Effects { get; }
}
