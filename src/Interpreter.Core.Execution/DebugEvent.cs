using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Classifies deterministic events emitted after successful machine-state transfers.
/// </summary>
public enum DebugEventKind
{
    /// <summary>An IL instruction was fully decoded and its semantic transfer completed.</summary>
    InstructionExecuted,

    /// <summary>A <c>ret</c> transfer removed an activation frame.</summary>
    FramePopped,
}

/// <summary>
/// Describes one deterministic event from a transfer that actually occurred.
/// </summary>
/// <param name="Kind">The stable semantic event category.</param>
/// <param name="Method">The method in which the transfer occurred.</param>
/// <param name="IlOffset">The offset of the instruction responsible for the event.</param>
/// <param name="Instruction">A stable instruction-family name for diagnostics and smoke fingerprints.</param>
/// <remarks>
/// Human-readable failure messages are not represented as instruction events. Decode, evidence, and validation
/// failures belong in <see cref="ExecutionFailure"/> so traces never claim an instruction ran when it did not.
/// </remarks>
public sealed record DebugEvent(
    DebugEventKind Kind,
    MethodHandle Method,
    int IlOffset,
    string Instruction);
