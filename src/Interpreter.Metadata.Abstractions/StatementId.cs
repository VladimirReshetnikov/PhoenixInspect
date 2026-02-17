using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Stable identifier for debugger statements within a method.
/// </summary>
/// <param name="Value">Statement identifier value.</param>
public readonly record struct StatementId(int Value);
