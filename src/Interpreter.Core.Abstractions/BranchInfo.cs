using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Provides additional context for branch decisions.
/// </summary>
/// <param name="Description">Human-readable description of the branch condition or rationale.</param>
/// <param name="Payload">Optional opaque payload for advanced consumers.</param>
public sealed record BranchInfo(string Description, object? Payload = null);
