using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents a single effect observation emitted during interpretation.
/// </summary>
/// <param name="Kind">Effect category.</param>
/// <param name="Code">Stable code for machine-readable diagnostics and telemetry.</param>
/// <param name="Details">Optional contextual detail suitable for explainability output.</param>
public readonly record struct EffectEvent(EffectKind Kind, string Code, string? Details = null);
