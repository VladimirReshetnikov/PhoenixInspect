namespace Interpreter.Core.Execution;

/// <summary>
/// Enumerates deterministic debug events emitted by draft micro-step execution.
/// </summary>
public enum DebugEventKind
{
    /// <summary>
    /// Indicates that an instruction was decoded and executed successfully.
    /// </summary>
    InstructionExecuted,

    /// <summary>
    /// Indicates that the active frame was removed from the call stack.
    /// </summary>
    FramePopped,
}

/// <summary>
/// Represents a single deterministic execution event emitted by a micro-step.
/// </summary>
/// <param name="Kind">The semantic category of emitted event.</param>
/// <param name="Detail">Optional human-readable detail used by diagnostics and assertions.</param>
public sealed record DebugEvent(DebugEventKind Kind, string? Detail = null);
