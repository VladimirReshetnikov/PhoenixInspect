using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Opaque method identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable method identifier value.</param>
public readonly record struct MethodHandle(ulong Value);
