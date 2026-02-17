using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Represents host-provided initial frame values used to seed interpreter execution.
/// </summary>
/// <param name="ThisObject">Optional instance receiver for instance methods.</param>
/// <param name="Arguments">Ordered argument values.</param>
/// <param name="LocalsByName">Best-effort local values keyed by display name.</param>
public sealed record FrameSeed(
    ExternalObjectRef? ThisObject,
    IReadOnlyList<ExternalValue> Arguments,
    IReadOnlyDictionary<string, ExternalValue> LocalsByName);
