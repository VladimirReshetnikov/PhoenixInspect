namespace Interpreter.Types;

/// <summary>
/// Represents a draft method signature used by abstraction-layer contracts during the concept phase.
/// </summary>
/// <param name="ReturnType">The declared return type.</param>
/// <param name="ParameterTypes">Ordered parameter type signatures.</param>
public sealed record MethodSig(TypeSig ReturnType, IReadOnlyList<TypeSig> ParameterTypes);
