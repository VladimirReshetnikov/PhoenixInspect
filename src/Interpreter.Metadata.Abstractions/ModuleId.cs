using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Stable module identity used by metadata backends and runtime bridges.
/// </summary>
/// <param name="Mvid">Module version identifier.</param>
/// <param name="Name">Optional module display name.</param>
/// <param name="PathHint">Optional module path hint.</param>
/// <param name="PeStamp">Optional PE timestamp/image-size tuple for disambiguation.</param>
public readonly record struct ModuleId(
    Guid Mvid,
    string? Name = null,
    string? PathHint = null,
    (uint TimeDateStamp, uint ImageSize)? PeStamp = null);
