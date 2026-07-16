namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines one deterministic, non-generic pure model selected and frozen before modeled-call execution.
/// </summary>
/// <remarks>
/// The capability receives only <see cref="PureCallModelInvocation"/>. It has no interpreter-domain value, lineage,
/// memory, dump/session, ambient-context, target-invocation, or diagnostic-message channel. Implementations must be
/// deterministic for equal invocations and must associate behavior changes with a new
/// <see cref="PureCallModelDescriptor.Identity"/> version.
/// </remarks>
public interface IPureCallModel
{
    /// <summary>Gets the immutable structural identity, target, confidence, and normalized effects of this model.</summary>
    PureCallModelDescriptor Descriptor { get; }

    /// <summary>Computes one typed semantic outcome without mutating target or interpreter state.</summary>
    /// <param name="invocation">The immutable typed call-site, argument, and policy facts.</param>
    /// <returns>One exact, unknown, blocked, or invalid typed outcome.</returns>
    PureCallModelOutcome Invoke(PureCallModelInvocation invocation);
}

/// <summary>Classifies the deterministic result of structural pure-model selection.</summary>
public enum PureCallModelSelectionKind
{
    /// <summary>Exactly one capability was selected for the requested structural target.</summary>
    Selected = 1,

    /// <summary>The registry has no model for the exact structural target.</summary>
    NotApplicable = 2,

    /// <summary>Selection was understood but could not proceed under available model capability or policy.</summary>
    Blocked = 3,

    /// <summary>The registry or its structural selection facts were invalid.</summary>
    Invalid = 4,
}

/// <summary>
/// Represents one immutable selected capability or one payload-omitting structural selection failure.
/// </summary>
/// <remarks>
/// A selected capability remains available for freezing into an opaque modeled graph leaf. Its object reference is
/// operational capability, not canonical content; graph equality must instead use its immutable descriptor. Every
/// non-selected result carries a bounded stable <c>W4.Model.*</c> code and no host-controlled explanation.
/// </remarks>
public sealed class PureCallModelSelectionResult
{
    /// <summary>Maximum number of ASCII characters in a stable model-selection failure code.</summary>
    public const int MaximumStableCodeLength = PureCallModelText.MaximumStableCodeLength;

    private PureCallModelSelectionResult(
        PureCallModelSelectionKind kind,
        IPureCallModel? model,
        string? stableCode)
    {
        Kind = kind;
        Model = model;
        StableCode = stableCode;
    }

    /// <summary>Gets the selected, not-applicable, blocked, or invalid result category.</summary>
    public PureCallModelSelectionKind Kind { get; }

    /// <summary>Gets the selected capability, or <see langword="null"/> for every non-selected result.</summary>
    public IPureCallModel? Model { get; }

    /// <summary>
    /// Gets the stable failure code of at most <see cref="MaximumStableCodeLength"/> ASCII characters, or
    /// <see langword="null"/> for a selected result.
    /// </summary>
    public string? StableCode { get; }

    /// <summary>Gets a value indicating whether this result contains exactly one selected capability.</summary>
    public bool IsSelected => Kind == PureCallModelSelectionKind.Selected && Model is not null;

    /// <summary>Creates one successful structural selection.</summary>
    /// <param name="model">The exact capability to freeze into the prepared modeled leaf.</param>
    /// <returns>An immutable selected result without a failure code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public static PureCallModelSelectionResult Selected(IPureCallModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new PureCallModelSelectionResult(PureCallModelSelectionKind.Selected, model, null);
    }

    /// <summary>Creates one result indicating that the exact structural target has no registered model.</summary>
    /// <param name="stableCode">A bounded canonical code in the <c>W4.Model.*</c> family.</param>
    /// <returns>An immutable not-applicable result without a capability.</returns>
    /// <exception cref="ArgumentException"><paramref name="stableCode"/> is not a bounded canonical model code.</exception>
    public static PureCallModelSelectionResult NotApplicable(string stableCode) =>
        Failed(PureCallModelSelectionKind.NotApplicable, stableCode);

    /// <summary>Creates one result indicating that structural selection was blocked.</summary>
    /// <param name="stableCode">A bounded canonical code in the <c>W4.Model.*</c> family.</param>
    /// <returns>An immutable blocked result without a capability.</returns>
    /// <exception cref="ArgumentException"><paramref name="stableCode"/> is not a bounded canonical model code.</exception>
    public static PureCallModelSelectionResult Blocked(string stableCode) =>
        Failed(PureCallModelSelectionKind.Blocked, stableCode);

    /// <summary>Creates one result indicating that registry selection facts were invalid.</summary>
    /// <param name="stableCode">A bounded canonical code in the <c>W4.Model.*</c> family.</param>
    /// <returns>An immutable invalid result without a capability.</returns>
    /// <exception cref="ArgumentException"><paramref name="stableCode"/> is not a bounded canonical model code.</exception>
    public static PureCallModelSelectionResult Invalid(string stableCode) =>
        Failed(PureCallModelSelectionKind.Invalid, stableCode);

    private static PureCallModelSelectionResult Failed(
        PureCallModelSelectionKind kind,
        string stableCode)
    {
        PureCallModelText.ValidateStableCode(stableCode, nameof(stableCode));
        return new PureCallModelSelectionResult(kind, null, stableCode);
    }
}

/// <summary>
/// Selects a scenario-owned pure model only by one exact body-independent structural call target.
/// </summary>
/// <remarks>
/// The target contains the MethodDef and complete signature; no display name is provided. Returning
/// <see cref="PureCallModelSelectionKind.NotApplicable"/> permits a caller whose frozen policy allows interpretation
/// to retain that disposition, while a caller requiring a model must block. W4 admits a scenario-narrow registry,
/// not plugin packs, precedence chains, or runtime name lookup. Selection occurs during preparation and the selected
/// capability must be frozen rather than queried again during execution.
/// </remarks>
public interface IPureCallModelRegistry
{
    /// <summary>Selects at most one model for the exact structural MethodDef and call signature.</summary>
    /// <param name="target">The exact body-independent target resolved at the direct call site.</param>
    /// <returns>A selected capability or one bounded non-selected result.</returns>
    PureCallModelSelectionResult Select(ResolvedMethodCallTarget target);
}
