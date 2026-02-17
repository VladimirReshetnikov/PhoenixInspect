namespace Interpreter.Types;

/// <summary>
/// Represents a draft type signature used by abstraction-layer contracts during the concept phase.
/// </summary>
/// <param name="DisplayName">A human-readable type name used for diagnostics and debugging.</param>
public sealed record TypeSig(string DisplayName);

/// <summary>
/// Represents a draft field signature used by abstraction-layer contracts during the concept phase.
/// </summary>
/// <param name="FieldType">The field value type.</param>
public sealed record FieldSig(TypeSig FieldType);

/// <summary>
/// Represents a draft method signature used by abstraction-layer contracts during the concept phase.
/// </summary>
/// <param name="ReturnType">The declared return type.</param>
/// <param name="ParameterTypes">Ordered parameter type signatures.</param>
public sealed record MethodSig(TypeSig ReturnType, IReadOnlyList<TypeSig> ParameterTypes);

/// <summary>
/// Represents a draft generic instantiation context used for token/signature resolution in prototype abstractions.
/// </summary>
/// <param name="TypeArguments">Type-level generic arguments for the active context.</param>
/// <param name="MethodArguments">Method-level generic arguments for the active context.</param>
public sealed record GenericContext(
    IReadOnlyList<TypeSig> TypeArguments,
    IReadOnlyList<TypeSig> MethodArguments);
