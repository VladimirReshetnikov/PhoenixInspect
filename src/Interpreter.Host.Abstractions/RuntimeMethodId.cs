using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Host-defined runtime method identity.
/// </summary>
/// <param name="Value">Runtime method identifier value.</param>
public readonly record struct RuntimeMethodId(ulong Value);
