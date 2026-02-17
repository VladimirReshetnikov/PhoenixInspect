namespace Interpreter.Types;

/// <summary>
/// Represents a draft generic instantiation context used for token/signature resolution in prototype abstractions.
/// </summary>
/// <param name="TypeArguments">Type-level generic arguments for the active context.</param>
/// <param name="MethodArguments">Method-level generic arguments for the active context.</param>
public sealed record GenericContext(
    IReadOnlyList<TypeSig> TypeArguments,
    IReadOnlyList<TypeSig> MethodArguments);
