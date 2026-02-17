namespace Interpreter.Core.Abstractions;

/// <summary>
/// Opaque module identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable module identifier value.</param>
public readonly record struct ModuleHandle(ulong Value);
