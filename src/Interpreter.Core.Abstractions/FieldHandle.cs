using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Opaque field identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable field identifier value.</param>
public readonly record struct FieldHandle(ulong Value);
