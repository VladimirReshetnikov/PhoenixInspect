using Interpreter.Metadata.Abstractions;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Maps host runtime method identity to metadata identity and token.
/// </summary>
/// <param name="RuntimeId">Host runtime method identity.</param>
/// <param name="Module">Metadata module identity.</param>
/// <param name="MethodToken">Method-definition metadata token when known.</param>
public sealed record RuntimeMethodInfo(RuntimeMethodId RuntimeId, ModuleId Module, int MethodToken);
