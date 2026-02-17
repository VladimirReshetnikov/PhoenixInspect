using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents a resolved type token and its canonical signature.
/// </summary>
/// <param name="Type">Opaque type handle.</param>
/// <param name="Sig">Resolved type signature.</param>
public readonly record struct ResolvedType(TypeHandle Type, TypeSig Sig);
