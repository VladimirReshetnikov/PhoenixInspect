namespace Interpreter.Types;

/// <summary>
/// Represents a draft type signature used by abstraction-layer contracts during the concept phase.
/// </summary>
/// <param name="DisplayName">A human-readable type name used for diagnostics and debugging.</param>
public sealed record TypeSig(string DisplayName);
