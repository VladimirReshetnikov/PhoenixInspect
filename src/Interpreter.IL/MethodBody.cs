namespace Interpreter.IL;

/// <summary>
/// Represents a draft IL method body abstraction used by resolver and metadata contracts.
/// </summary>
/// <param name="MaxStack">The declared maximum evaluation stack depth.</param>
/// <param name="CodeBytes">Raw IL bytes for the method body.</param>
public sealed record MethodBody(int MaxStack, ReadOnlyMemory<byte> CodeBytes);
