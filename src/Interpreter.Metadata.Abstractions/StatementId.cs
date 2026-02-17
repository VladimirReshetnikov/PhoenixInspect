namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Stable identifier for debugger statements within a method.
/// </summary>
/// <param name="Value">Statement identifier value.</param>
public readonly record struct StatementId(int Value);
