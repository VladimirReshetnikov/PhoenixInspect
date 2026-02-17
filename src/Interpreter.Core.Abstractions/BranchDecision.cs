namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents a requested branch decision and optional supporting context.
/// </summary>
/// <param name="Kind">Requested branch-handling strategy.</param>
/// <param name="Info">Optional contextual information about the decision.</param>
public readonly record struct BranchDecision(BranchDecisionKind Kind, BranchInfo? Info = null);
