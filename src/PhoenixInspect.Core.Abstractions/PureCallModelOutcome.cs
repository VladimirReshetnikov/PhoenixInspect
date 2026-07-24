namespace PhoenixInspect.Core.Abstractions;

/// <summary>Classifies the closed typed outcome returned by a pure call-model capability.</summary>
public enum PureCallModelOutcomeKind
{
    /// <summary>The model proved one exact signed 32-bit return value.</summary>
    ExactReturn = 1,

    /// <summary>The model produced a typed unknown whose lineage must be grounded by the engine.</summary>
    UnknownReturn = 2,

    /// <summary>The model could not represent the admitted input under its supported capability.</summary>
    Blocked = 3,

    /// <summary>The model classified the supplied invocation facts as structurally invalid.</summary>
    Invalid = 4,
}

/// <summary>
/// Represents one immutable, typed, payload-omitting result from a pure call model.
/// </summary>
/// <remarks>
/// Exact and unknown outcomes describe only the structural <see cref="int"/> return relation. An unknown outcome
/// cannot carry or invent provenance; after validating that at least one input is explained unknown, the engine owns
/// construction of the modeled-return lineage. Blocked and invalid outcomes carry only a bounded stable
/// <c>W4.Model.*</c> code. Human-readable or exception-controlled text never crosses this boundary.
/// </remarks>
public sealed record PureCallModelOutcome
{
    /// <summary>Maximum number of ASCII characters in a stable model failure code.</summary>
    public const int MaximumStableCodeLength = PureCallModelText.MaximumStableCodeLength;

    private PureCallModelOutcome(
        PureCallModelOutcomeKind kind,
        int? int32Value,
        string? stableCode)
    {
        Kind = kind;
        Int32Value = int32Value;
        StableCode = stableCode;
    }

    /// <summary>Gets the closed typed outcome category.</summary>
    public PureCallModelOutcomeKind Kind { get; }

    /// <summary>Gets the exact return payload, or <see langword="null"/> for every non-exact outcome.</summary>
    public int? Int32Value { get; }

    /// <summary>
    /// Gets the stable failure code of at most <see cref="MaximumStableCodeLength"/> ASCII characters, or
    /// <see langword="null"/> for exact and unknown returns.
    /// </summary>
    public string? StableCode { get; }

    /// <summary>
    /// Gets the structural return type for exact and unknown outcomes, or <see langword="null"/> when no value exists.
    /// </summary>
    public TypeSig? ReturnType => Kind is
        PureCallModelOutcomeKind.ExactReturn or PureCallModelOutcomeKind.UnknownReturn
            ? TypeSig.Int32
            : null;

    /// <summary>Creates one exact signed 32-bit return outcome.</summary>
    /// <param name="value">The exact return payload.</param>
    /// <returns>An immutable exact-return outcome.</returns>
    public static PureCallModelOutcome ExactReturn(int value) =>
        new(PureCallModelOutcomeKind.ExactReturn, value, null);

    /// <summary>Creates one structural signed 32-bit unknown-return outcome without lineage.</summary>
    /// <returns>An immutable typed-unknown outcome.</returns>
    public static PureCallModelOutcome UnknownReturn() =>
        new(PureCallModelOutcomeKind.UnknownReturn, null, null);

    /// <summary>Creates one payload-omitting blocked outcome.</summary>
    /// <param name="stableCode">A bounded canonical code in the <c>W4.Model.*</c> family.</param>
    /// <returns>An immutable blocked outcome.</returns>
    /// <exception cref="ArgumentException"><paramref name="stableCode"/> is not a bounded canonical model code.</exception>
    public static PureCallModelOutcome Blocked(string stableCode)
    {
        PureCallModelText.ValidateStableCode(stableCode, nameof(stableCode));
        return new PureCallModelOutcome(PureCallModelOutcomeKind.Blocked, null, stableCode);
    }

    /// <summary>Creates one payload-omitting invalid outcome.</summary>
    /// <param name="stableCode">A bounded canonical code in the <c>W4.Model.*</c> family.</param>
    /// <returns>An immutable invalid outcome.</returns>
    /// <exception cref="ArgumentException"><paramref name="stableCode"/> is not a bounded canonical model code.</exception>
    public static PureCallModelOutcome Invalid(string stableCode)
    {
        PureCallModelText.ValidateStableCode(stableCode, nameof(stableCode));
        return new PureCallModelOutcome(PureCallModelOutcomeKind.Invalid, null, stableCode);
    }
}
