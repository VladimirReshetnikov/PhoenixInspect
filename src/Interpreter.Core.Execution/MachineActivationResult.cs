using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Reports whether metadata-derived legacy or prepared-graph root activation produced a valid initial semantic state.
/// </summary>
/// <typeparam name="TValue">The value-domain representation carried by the state.</typeparam>
/// <typeparam name="TMemory">The persistent-memory snapshot representation.</typeparam>
/// <param name="State">The initialized root state on success; otherwise <see langword="null"/>.</param>
/// <param name="Status">
/// <see cref="MachineRunStatus.Ready"/> on success, or the structured blocked/invalid status on failure.
/// </param>
/// <param name="Failure">The activation or admission failure when no state was created.</param>
/// <remarks>
/// Activation consumes no instruction budget and emits no execution event. Legacy activation resolves and freezes its
/// complete method shape; prepared-graph activation consumes the already retained root plan without re-resolution.
/// Both validate supplied receiver/argument values, derive local defaults, and fix IL offset zero plus an empty
/// evaluation stack before exposing a state. The result shape is provisional during conceptual design.
/// </remarks>
public sealed record MachineActivationResult<TValue, TMemory>(
    MachineState<TValue, TMemory>? State,
    MachineRunStatus Status,
    ExecutionFailure? Failure)
    where TMemory : IPersistentMemoryState<TMemory>
{
    /// <summary>Gets whether activation created a ready root state.</summary>
    public bool IsSuccess => Status == MachineRunStatus.Ready && State is not null && Failure is null;
}
