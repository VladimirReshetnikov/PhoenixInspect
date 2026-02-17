namespace Interpreter.Core.Abstractions;

/// <summary>
/// Describes null-state information for a value-domain instance.
/// </summary>
public enum Nullness
{
    NotApplicable,
    Null,
    NonNull,
    MaybeNull,
}
