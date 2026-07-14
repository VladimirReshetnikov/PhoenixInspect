using System.Collections.Immutable;

namespace Interpreter.Core.Execution;

/// <summary>
/// Reports whether a complete method body belongs to the currently executable IL slice.
/// </summary>
/// <param name="IsAdmitted">Whether every instruction and required body feature is supported.</param>
/// <param name="InstructionCount">The number of fully decoded instructions on success.</param>
/// <param name="InstructionBoundaries">
/// The admitted offsets and evaluation-stack types derived by simulating the method from entry.
/// </param>
/// <param name="FailureStatus">The machine status to use when admission fails.</param>
/// <param name="Failure">The structured body/feature failure on rejection.</param>
/// <remarks>
/// Admission is intentionally whole-body. A supported prefix followed by one unsupported instruction is rejected
/// before instruction zero can consume budget, emit events, or mutate semantic state.
/// </remarks>
public sealed record MethodAdmissionResult(
    bool IsAdmitted,
    int InstructionCount,
    ImmutableArray<MethodInstructionBoundary> InstructionBoundaries,
    MachineRunStatus FailureStatus,
    ExecutionFailure? Failure);

/// <summary>
/// Describes one legal instruction-entry boundary derived by whole-body admission.
/// </summary>
/// <param name="IlOffset">The zero-based byte offset of the decoded instruction.</param>
/// <param name="ExpectedStackTypes">
/// The ordered bottom-to-top structural types required before the instruction executes.
/// </param>
public readonly record struct MethodInstructionBoundary(
    int IlOffset,
    ImmutableArray<Interpreter.Core.Abstractions.TypeSig> ExpectedStackTypes)
{
    /// <summary>Gets the evaluation-stack depth implied by <see cref="ExpectedStackTypes"/>.</summary>
    public int ExpectedStackDepth => ExpectedStackTypes.IsDefault ? -1 : ExpectedStackTypes.Length;
}
