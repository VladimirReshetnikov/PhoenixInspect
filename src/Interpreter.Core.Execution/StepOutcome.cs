using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Represents the complete result of requesting one low-level machine transition.
/// </summary>
/// <typeparam name="TValue">The domain value representation carried by returned state.</typeparam>
/// <typeparam name="TMemory">The persistent memory representation carried by returned state.</typeparam>
/// <param name="State">The resulting state; it is unchanged when no instruction executed.</param>
/// <param name="OperationalState">Updated deterministic bookkeeping; unchanged when no instruction executed.</param>
/// <param name="Status">Whether the machine can continue, completed, exhausted budget, or became blocked/invalid.</param>
/// <param name="Events">Structured deterministic events emitted only for transfers that actually occurred.</param>
/// <param name="Failure">A structured failure when <paramref name="Status"/> is blocked or invalid.</param>
/// <param name="TargetException">
/// Structured target-exception information when <paramref name="Status"/> is
/// <see cref="MachineRunStatus.TargetException"/>.
/// </param>
public sealed record StepOutcome<TValue, TMemory>(
    MachineState<TValue, TMemory> State,
    MachineOperationalState OperationalState,
    MachineRunStatus Status,
    ImmutableArray<DebugEvent> Events,
    ExecutionFailure? Failure = null,
    TargetExceptionInfo? TargetException = null)
    where TMemory : IPersistentMemoryState<TMemory>;
