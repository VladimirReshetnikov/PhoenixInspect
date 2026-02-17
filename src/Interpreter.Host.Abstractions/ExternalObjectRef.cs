using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Host-specific external object identity, typically backed by runtime memory addresses.
/// </summary>
/// <param name="Address">Host-defined object address or stable object key.</param>
public readonly record struct ExternalObjectRef(ulong Address);
