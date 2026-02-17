namespace Interpreter.Core.Abstractions;

/// <summary>
/// Associates unknown-value provenance with optional explanatory detail.
/// </summary>
/// <param name="Kind">High-level origin category for the unknown value.</param>
/// <param name="Detail">Optional free-form detail to support user-facing explanations.</param>
public readonly record struct UnknownOrigin(UnknownOriginKind Kind, string? Detail = null);
