namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines branch-handling decisions that a policy or model can request from the engine.
/// </summary>
public enum BranchDecisionKind
{
    TakeTrue,
    TakeFalse,
    Fork,
    StopForUserChoice,
    JoinBoth,
}
