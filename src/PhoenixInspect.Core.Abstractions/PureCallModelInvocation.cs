using System.Collections.Immutable;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>Classifies one immutable typed argument presented to a pure call model.</summary>
public enum PureCallModelArgumentKind
{
    /// <summary>The argument is one exact signed 32-bit integer.</summary>
    ExactInt32 = 1,

    /// <summary>The argument is an admitted explained unknown of structural type <see cref="int"/>.</summary>
    ExplainedUnknownInt32 = 2,
}

/// <summary>
/// Represents one immutable, lineage-free <see cref="int"/> argument atom at the pure-model boundary.
/// </summary>
/// <remarks>
/// An unknown atom communicates only typed unknownness. It deliberately carries no domain value, lineage identity,
/// evidence reason, memory reference, dump/session capability, target delegate, or host-controlled text. The engine
/// retains those concerns outside the model invocation and constructs any modeled-return lineage itself.
/// </remarks>
public readonly record struct PureCallModelArgument
{
    private PureCallModelArgument(PureCallModelArgumentKind kind, int? int32Value)
    {
        Kind = kind;
        Int32Value = int32Value;
    }

    /// <summary>Gets whether this atom contains one exact integer or one explained typed unknown.</summary>
    public PureCallModelArgumentKind Kind { get; }

    /// <summary>Gets the exact integer payload, or <see langword="null"/> for an explained unknown.</summary>
    public int? Int32Value { get; }

    /// <summary>Creates one exact signed 32-bit argument atom.</summary>
    /// <param name="value">The exact integer payload.</param>
    /// <returns>An immutable exact argument.</returns>
    public static PureCallModelArgument ExactInt32(int value) =>
        new(PureCallModelArgumentKind.ExactInt32, value);

    /// <summary>Creates one lineage-free explained-unknown signed 32-bit argument atom.</summary>
    /// <returns>An immutable typed-unknown argument.</returns>
    public static PureCallModelArgument ExplainedUnknownInt32() =>
        new(PureCallModelArgumentKind.ExplainedUnknownInt32, null);

    internal bool HasValidShape => Kind switch
    {
        PureCallModelArgumentKind.ExactInt32 => Int32Value.HasValue,
        PureCallModelArgumentKind.ExplainedUnknownInt32 => Int32Value is null,
        _ => false,
    };
}

/// <summary>Controls which immutable argument precision may cross a pure-model invocation boundary.</summary>
public enum PureCallModelUnknownPolicy
{
    /// <summary>Every argument must be exact.</summary>
    ExactOnly = 0,

    /// <summary>Exact and admitted explained-unknown <see cref="int"/> arguments are accepted.</summary>
    ExplainedInt32 = 1,
}

/// <summary>
/// Supplies one pure model with the complete immutable W4 call-site, argument, and unknown-policy facts.
/// </summary>
/// <remarks>
/// The invocation is intentionally non-generic and contains exactly two metadata-ordered <see cref="int"/> atoms.
/// It cannot expose interpreter values, provenance, persistent memory, dump/session services, ambient time or
/// environment, an invocable target, or exception text. A pure model therefore returns a semantic outcome while the
/// engine retains control of state transfer, accounting, effects, and explanations.
/// </remarks>
public sealed class PureCallModelInvocation : IEquatable<PureCallModelInvocation>
{
    /// <summary>Creates one validated immutable pure-model invocation.</summary>
    /// <param name="callSite">The exact same-module caller, IL offset, and structural model target.</param>
    /// <param name="arguments">Exactly two metadata-ordered exact or explained-unknown integer atoms.</param>
    /// <param name="unknownPolicy">The precision policy already validated by the engine.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="callSite"/> is default or malformed; <paramref name="arguments"/> is default, incorrectly
    /// sized, or contains a malformed atom; or an unknown atom is supplied under
    /// <see cref="PureCallModelUnknownPolicy.ExactOnly"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unknownPolicy"/> is undefined.</exception>
    public PureCallModelInvocation(
        DirectCallSiteIdentity callSite,
        ImmutableArray<PureCallModelArgument> arguments,
        PureCallModelUnknownPolicy unknownPolicy)
    {
        if (callSite.Caller == default ||
            callSite.Callee == default ||
            callSite.CallIlOffset < 0 ||
            callSite.Caller.Module != callSite.Callee.Module)
        {
            throw new ArgumentException(
                "A pure-model invocation requires one valid same-module direct-call identity.",
                nameof(callSite));
        }

        if (!Enum.IsDefined(unknownPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(unknownPolicy));
        }

        if (arguments.IsDefault || arguments.Length != 2)
        {
            throw new ArgumentException(
                "The closed W4 pure-model profile requires exactly two initialized argument atoms.",
                nameof(arguments));
        }

        if (arguments.Any(static argument => !argument.HasValidShape))
        {
            throw new ArgumentException(
                "A pure-model invocation contains a malformed argument discriminated union.",
                nameof(arguments));
        }

        if (unknownPolicy == PureCallModelUnknownPolicy.ExactOnly &&
            arguments.Any(static argument =>
                argument.Kind == PureCallModelArgumentKind.ExplainedUnknownInt32))
        {
            throw new ArgumentException(
                "An exact-only pure-model invocation cannot contain an unknown argument.",
                nameof(arguments));
        }

        CallSite = callSite;
        Arguments = ImmutableArray.CreateRange(arguments.AsSpan().ToArray());
        UnknownPolicy = unknownPolicy;
    }

    /// <summary>Gets the exact caller, IL offset, and same-module structural target.</summary>
    public DirectCallSiteIdentity CallSite { get; }

    /// <summary>Gets the complete immutable metadata-ordered two-argument vector.</summary>
    public ImmutableArray<PureCallModelArgument> Arguments { get; }

    /// <summary>Gets the unknown-input policy validated before model invocation.</summary>
    public PureCallModelUnknownPolicy UnknownPolicy { get; }

    /// <inheritdoc />
    public bool Equals(PureCallModelInvocation? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        CallSite == other.CallSite &&
        UnknownPolicy == other.UnknownPolicy &&
        Arguments.SequenceEqual(other.Arguments);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PureCallModelInvocation);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = CallSite.GetHashCode();
        hash = unchecked((hash * 397) ^ (int)UnknownPolicy);
        foreach (var argument in Arguments)
        {
            hash = unchecked((hash * 397) ^ argument.GetHashCode());
        }

        return hash;
    }
}
