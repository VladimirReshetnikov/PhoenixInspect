using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Identifies the memory region that should be conservatively invalidated.
/// </summary>
/// <param name="Kind">Region-kind selector.</param>
/// <param name="Payload">Optional region-specific payload (for example an object reference).</param>
public readonly record struct HavocRegion(HavocRegionKind Kind, object? Payload = null);
