using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Opaque type identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable type identifier value.</param>
public readonly record struct TypeHandle(ulong Value);
