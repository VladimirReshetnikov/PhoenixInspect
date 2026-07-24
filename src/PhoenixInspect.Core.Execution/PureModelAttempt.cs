using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Core.Execution;

/// <summary>
/// Classifies the observable operational result of invoking one frozen pure-model capability.
/// </summary>
/// <remarks>
/// These values describe an invocation attempt, not a semantic machine event. Exact and unknown outcomes may either
/// complete the caller transfer or fail during engine-owned result materialization. Blocked, invalid, capability-
/// failure, and malformed outcomes never complete a transfer. This W4.6 contract remains subject to refinement
/// when the implementation grows beyond its closed two-<see cref="int"/> pure-model profile.
/// </remarks>
public enum PureModelAttemptOutcomeKind
{
    /// <summary>The model returned an exact integer relation.</summary>
    ExactReturn = 1,

    /// <summary>The model returned an unknown integer relation requiring engine-owned lineage.</summary>
    UnknownReturn = 2,

    /// <summary>The model reported a bounded payload-omitting limitation.</summary>
    Blocked = 3,

    /// <summary>The model reported that the immutable invocation facts were invalid.</summary>
    Invalid = 4,

    /// <summary>The frozen runtime capability threw instead of returning a typed outcome.</summary>
    CapabilityFailure = 5,

    /// <summary>The frozen runtime capability returned no outcome or an undefined outcome shape.</summary>
    MalformedOutcome = 6,
}

/// <summary>
/// Records one immutable, payload-omitting invocation of a frozen pure model in deterministic operational state.
/// </summary>
/// <remarks>
/// An attempt is appended only after control enters the model capability. Preflight, graph-invariant, and instruction-
/// budget failures therefore create no attempt. A failed attempt may advance logical-depth high water while leaving
/// semantic state, persistent memory, budget, events, and active-frame high water unchanged. The record carries no
/// model return payload, exception text, domain value, memory reference, registry, resolver, or target body.
///
/// This is provisional early-development evidence rather than a stable diagnostic-output or serialization API. Consumers should
/// treat the complete ordered vector on <see cref="MachineOperationalState"/> as part of deterministic replay.
/// </remarks>
public sealed record PureModelAttempt
{
    /// <summary>Maximum length accepted for one payload-omitting diagnostic code retained by an attempt.</summary>
    public const int MaximumStableCodeLength = 128;

    /// <summary>Creates one validated immutable modeled-call attempt.</summary>
    /// <param name="callSite">The exact frozen caller, call offset, and opaque modeled target.</param>
    /// <param name="modelIdentity">The stable identifier and semantic version frozen during graph preparation.</param>
    /// <param name="enteredLogicalDepth">
    /// The rooted logical boundary entered by this invocation, counting the root method as depth one.
    /// </param>
    /// <param name="outcomeKind">The closed operational classification of the model invocation and typed outcome.</param>
    /// <param name="transferCompleted">
    /// Whether the engine atomically advanced the caller and published the modeled return value.
    /// </param>
    /// <param name="stableCode">
    /// A bounded ASCII <c>W4.Model.*</c> or <c>EXEC_*</c> code for a nontransferring attempt; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The call site or model identity is default/malformed, or the completion and diagnostic facts disagree.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="enteredLogicalDepth"/> is smaller than the first possible modeled boundary, or
    /// <paramref name="outcomeKind"/> is undefined.
    /// </exception>
    public PureModelAttempt(
        DirectCallSiteIdentity callSite,
        PureCallModelIdentity modelIdentity,
        int enteredLogicalDepth,
        PureModelAttemptOutcomeKind outcomeKind,
        bool transferCompleted,
        string? stableCode)
    {
        if (callSite.Caller == default ||
            callSite.Callee == default ||
            callSite.CallIlOffset < 0 ||
            callSite.Caller.Module != callSite.Callee.Module)
        {
            throw new ArgumentException(
                "A pure-model attempt requires one valid same-module direct-call identity.",
                nameof(callSite));
        }

        if (string.IsNullOrEmpty(modelIdentity.StableId))
        {
            throw new ArgumentException(
                "A pure-model attempt requires one initialized frozen model identity.",
                nameof(modelIdentity));
        }

        if (enteredLogicalDepth < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enteredLogicalDepth),
                "A modeled call enters a logical boundary below its active caller.");
        }

        if (!Enum.IsDefined(outcomeKind))
        {
            throw new ArgumentOutOfRangeException(nameof(outcomeKind));
        }

        var isReturn = outcomeKind is
            PureModelAttemptOutcomeKind.ExactReturn or
            PureModelAttemptOutcomeKind.UnknownReturn;
        if (transferCompleted && !isReturn)
        {
            throw new ArgumentException(
                "Only an exact or unknown typed model return can complete a caller transfer.",
                nameof(transferCompleted));
        }

        if (transferCompleted == (stableCode is not null))
        {
            throw new ArgumentException(
                "A completed attempt has no failure code, while a nontransferring attempt requires one.",
                nameof(stableCode));
        }

        if (stableCode is not null && !IsStableCode(stableCode))
        {
            throw new ArgumentException(
                $"An attempt code must be bounded ASCII in the W4.Model or EXEC family and contain at most {MaximumStableCodeLength} characters.",
                nameof(stableCode));
        }

        CallSite = callSite;
        ModelIdentity = modelIdentity;
        EnteredLogicalDepth = enteredLogicalDepth;
        OutcomeKind = outcomeKind;
        TransferCompleted = transferCompleted;
        StableCode = stableCode;
    }

    /// <summary>Gets the exact frozen structural direct-call identity.</summary>
    public DirectCallSiteIdentity CallSite { get; }

    /// <summary>Gets the stable identity and version of the model frozen during graph preparation.</summary>
    public PureCallModelIdentity ModelIdentity { get; }

    /// <summary>Gets the rooted logical depth entered for this invocation without implying a frame push.</summary>
    public int EnteredLogicalDepth { get; }

    /// <summary>Gets the closed operational outcome classification.</summary>
    public PureModelAttemptOutcomeKind OutcomeKind { get; }

    /// <summary>Gets whether caller advancement and modeled-result publication completed atomically.</summary>
    public bool TransferCompleted { get; }

    /// <summary>Gets the bounded diagnostic code for a nontransfer, or <see langword="null"/> after transfer.</summary>
    public string? StableCode { get; }

    private static bool IsStableCode(string value)
    {
        if (value.Length is < 1 or > MaximumStableCodeLength)
        {
            return false;
        }

        if (value.StartsWith("W4.Model.", StringComparison.Ordinal))
        {
            var segmentLength = 0;
            foreach (var character in value)
            {
                if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    segmentLength++;
                    continue;
                }

                if (character != '.' || segmentLength == 0)
                {
                    return false;
                }

                segmentLength = 0;
            }

            return segmentLength > 0;
        }

        if (!value.StartsWith("EXEC_", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = value.AsSpan("EXEC_".Length);
        if (suffix.IsEmpty || suffix[0] == '_' || suffix[^1] == '_')
        {
            return false;
        }

        var previousWasSeparator = false;
        foreach (var character in suffix)
        {
            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                previousWasSeparator = false;
                continue;
            }

            if (character != '_' || previousWasSeparator)
            {
                return false;
            }

            previousWasSeparator = true;
        }

        return true;
    }
}
