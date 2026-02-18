using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Represents the mutable virtual machine state consumed and produced by interpreter micro-steps.
/// </summary>
/// <typeparam name="TValue">Abstract value representation carried by evaluation stacks.</typeparam>
/// <typeparam name="TMemory">Abstract memory snapshot representation for the active execution session.</typeparam>
/// <param name="CallStack">Ordered call stack where the last frame is the currently executing frame.</param>
/// <param name="Memory">Current abstract memory snapshot.</param>
/// <param name="Budget">Remaining execution budget constraints.</param>
public sealed record MachineState<TValue, TMemory>(
    ImmutableArray<FrameState<TValue>> CallStack,
    TMemory Memory,
    BudgetState Budget);
