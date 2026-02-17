using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents a resolved field token and associated metadata.
/// </summary>
/// <param name="Field">Opaque field handle.</param>
/// <param name="Sig">Resolved field signature.</param>
/// <param name="DeclaringType">Declaring type handle.</param>
public readonly record struct ResolvedField(FieldHandle Field, FieldSig Sig, TypeHandle DeclaringType);
