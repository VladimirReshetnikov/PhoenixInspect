using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Represents the immutable state consumed and produced by low-level interpreter micro-steps.
/// </summary>
/// <typeparam name="TValue">The domain value representation carried by frames and terminal results.</typeparam>
/// <typeparam name="TMemory">The persistent memory snapshot representation.</typeparam>
/// <param name="CallStack">Ordered frames where the last item is active.</param>
/// <param name="Memory">The current memory snapshot.</param>
/// <param name="ReturnValue">The root return value after successful completion, or none for void/incomplete runs.</param>
/// <remarks>
/// Record equality is structural only at the field level and is not semantic equality: in particular,
/// <see cref="ImmutableArray{T}"/> compares backing storage by identity. Use
/// <see cref="MachineStateSemanticComparer{TValue,TMemory}"/> when comparing independently materialized states.
/// </remarks>
public sealed record MachineState<TValue, TMemory>(
    ImmutableArray<FrameState<TValue>> CallStack,
    TMemory Memory,
    OptionalValue<TValue> ReturnValue)
    where TMemory : IPersistentMemoryState<TMemory>
{
    /// <summary>
    /// Gets structured target-exception termination information, or <see langword="null"/> for ready and normally
    /// completed states.
    /// </summary>
    /// <remarks>
    /// A target-terminated state has an empty call stack and no return value. It is a terminal latch rather than a
    /// resumable pre-instruction snapshot; stepping it again reports the same terminal condition without consuming
    /// budget or emitting a second event.
    /// </remarks>
    public TargetExceptionInfo? TerminalTargetException { get; init; }

    /// <summary>
    /// Creates an initial machine state with no terminal return value.
    /// </summary>
    /// <param name="rootFrame">The single root activation to execute.</param>
    /// <param name="memory">The initial persistent memory snapshot.</param>
    /// <returns>A ready machine state containing exactly <paramref name="rootFrame"/>.</returns>
    public static MachineState<TValue, TMemory> Create(
        FrameState<TValue> rootFrame,
        TMemory memory)
    {
        ArgumentNullException.ThrowIfNull(rootFrame);
        ArgumentNullException.ThrowIfNull(memory);
        return new MachineState<TValue, TMemory>(
            ImmutableArray.Create(rootFrame),
            memory,
            OptionalValue<TValue>.None);
    }
}
