using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Host-specific external thread identity.
/// </summary>
/// <param name="OsId">Operating-system thread identifier.</param>
public readonly record struct ExternalThreadId(uint OsId);
