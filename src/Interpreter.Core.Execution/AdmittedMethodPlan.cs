using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

internal enum AdmittedInstructionKind
{
    Nop,
    LoadArgument,
    LoadLocal,
    StoreLocal,
    LoadInt32,
    Add,
    Subtract,
    Multiply,
    LoadField,
    Return,
}

internal readonly record struct AdmittedInstruction(
    int IlOffset,
    AdmittedInstructionKind Kind,
    int Operand,
    int Size,
    ResolvedField? Field = null);

internal sealed record AdmittedMethodPlan(
    ResolvedMethodDefinition Definition,
    ImmutableArray<TypeSig> ArgumentTypes,
    ImmutableArray<AdmittedInstruction> Instructions,
    MethodAdmissionResult Admission)
{
    internal bool TryGetInstruction(int ilOffset, out AdmittedInstruction instruction)
    {
        foreach (var candidate in Instructions)
        {
            if (candidate.IlOffset == ilOffset)
            {
                instruction = candidate;
                return true;
            }
        }

        instruction = default;
        return false;
    }

    internal bool TryGetBoundary(int ilOffset, out MethodInstructionBoundary boundary)
    {
        foreach (var candidate in Admission.InstructionBoundaries)
        {
            if (candidate.IlOffset == ilOffset)
            {
                boundary = candidate;
                return true;
            }
        }

        boundary = default;
        return false;
    }
}

internal readonly record struct PlanPreparationResult(
    AdmittedMethodPlan? Plan,
    MachineRunStatus Status,
    ExecutionFailure? Failure)
{
    internal bool IsSuccess => Plan is not null && Status == MachineRunStatus.Ready && Failure is null;

    internal static PlanPreparationResult Success(AdmittedMethodPlan plan) =>
        new(plan, MachineRunStatus.Ready, null);

    internal static PlanPreparationResult Failed(MachineRunStatus status, ExecutionFailure failure) =>
        new(null, status, failure);
}
