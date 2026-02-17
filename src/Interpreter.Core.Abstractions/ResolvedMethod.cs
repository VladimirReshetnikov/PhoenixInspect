using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents a resolved method token and associated metadata.
/// </summary>
/// <param name="Definition">Opaque method-definition handle.</param>
/// <param name="Signature">Resolved method signature.</param>
/// <param name="CalleeGenericContext">Resolved callee generic context for invocation.</param>
/// <param name="DeclaringType">Declaring type handle.</param>
public readonly record struct ResolvedMethod(
    MethodHandle Definition,
    MethodSig Signature,
    GenericContext CalleeGenericContext,
    TypeHandle DeclaringType);
