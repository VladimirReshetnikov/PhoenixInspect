using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Core.Execution;

/// <summary>
/// Classifies a machine step that could not execute an instruction.
/// </summary>
public enum ExecutionFailureKind
{
    /// <summary>A method body or other required dependency could not be resolved.</summary>
    DependencyResolution,

    /// <summary>The current IL offset is outside the body or not decodable.</summary>
    InvalidInstruction,

    /// <summary>The decoded opcode is outside the admitted prototype slice.</summary>
    UnsupportedInstruction,

    /// <summary>The evaluation stack does not satisfy the instruction's contract.</summary>
    InvalidStack,

    /// <summary>An argument or local slot operand is outside the seeded frame layout.</summary>
    InvalidSlot,

    /// <summary>A value-domain operation rejected an otherwise decoded transfer.</summary>
    DomainFailure,

    /// <summary>A memory-model capability returned invalid evidence or rejected an admitted transfer.</summary>
    MemoryFailure,

    /// <summary>A deterministic input or execution resource cap was exceeded.</summary>
    ResourceLimit,
}

/// <summary>
/// Provides a structured diagnostic for a machine step that executed no instruction.
/// </summary>
/// <param name="Kind">The stable failure category.</param>
/// <param name="Code">A stable machine-readable diagnostic code.</param>
/// <param name="Message">A human-readable explanation.</param>
/// <param name="Method">The active method definition when known.</param>
/// <param name="IlOffset">The active IL offset when known.</param>
/// <param name="ResolutionFailure">The originating dependency failure, when applicable.</param>
public sealed record ExecutionFailure(
    ExecutionFailureKind Kind,
    string Code,
    string Message,
    MethodHandle? Method = null,
    int? IlOffset = null,
    ResolutionFailure? ResolutionFailure = null);

internal static class ResolutionFailureDiagnostics
{
    internal static ResolutionFailure Normalize(ResolutionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ResolutionFailure(
            failure.Kind,
            failure.Code,
            "The resolver reported a structured dependency failure.");
    }
}
