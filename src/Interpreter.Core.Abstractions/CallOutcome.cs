namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents the result of attempting to model a call site.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
/// <param name="Kind">Outcome kind.</param>
/// <param name="ReturnValue">Optional return value when <see cref="CallOutcomeKind.Returned"/>.</param>
/// <param name="ThrownException">Optional exception value when <see cref="CallOutcomeKind.Threw"/>.</param>
/// <param name="Memory">Optional updated memory snapshot.</param>
/// <param name="Effects">Optional side-effect summary.</param>
/// <param name="Forks">Optional forked outcomes.</param>
/// <param name="DecisionInfo">Optional decision context for interactive branching.</param>
public sealed record CallOutcome<TValue, TMem>(
    CallOutcomeKind Kind,
    TValue? ReturnValue = default,
    TValue? ThrownException = default,
    TMem? Memory = default,
    EffectSummary? Effects = null,
    IReadOnlyList<CallOutcome<TValue, TMem>>? Forks = null,
    BranchInfo? DecisionInfo = null);
