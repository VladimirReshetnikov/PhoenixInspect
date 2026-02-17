using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Aggregates effect categories and supporting effect events for an operation.
/// </summary>
/// <param name="Kinds">Bitwise union of all effect categories captured in <paramref name="Events"/>.</param>
/// <param name="Events">Ordered effect events describing concrete observations.</param>
public sealed record EffectSummary(EffectKind Kinds, IReadOnlyList<EffectEvent> Events);
