using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Host-defined runtime module identity.
/// </summary>
/// <param name="Value">Runtime module identifier value.</param>
public readonly record struct RuntimeModuleId(ulong Value);
