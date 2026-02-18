using System.Collections.Immutable;

namespace Interpreter.Core.Execution;

/// <summary>
/// Represents the result of performing one interpreter micro-step.
/// </summary>
/// <typeparam name="TValue">Abstract value representation carried in the returned state.</typeparam>
/// <typeparam name="TMemory">Abstract memory representation carried in the returned state.</typeparam>
/// <param name="State">Updated machine state after step processing.</param>
/// <param name="StopReason">Reason execution is currently stopped.</param>
/// <param name="Events">Deterministic event stream emitted during the step.</param>
public sealed record StepOutcome<TValue, TMemory>(
    MachineState<TValue, TMemory> State,
    StopReason StopReason,
    ImmutableArray<DebugEvent> Events);
