using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

public sealed partial class IlMachine<TValue, TMemory>
{
    private StepOutcome<TValue, TMemory> Execute(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedMethodPlan plan,
        AdmittedInstruction instruction,
        BudgetState updatedBudget)
    {
        return instruction.Kind switch
        {
            AdmittedInstructionKind.Nop => CompleteOrdinaryInstruction(
                state,
                operationalState,
                frame,
                instruction,
                frame.EvalStack,
                frame.Locals,
                updatedBudget),
            AdmittedInstructionKind.LoadArgument => Push(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                frame.Arguments[instruction.Operand],
                plan.ArgumentTypes[instruction.Operand],
                updatedBudget),
            AdmittedInstructionKind.LoadLocal => Push(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                frame.Locals[instruction.Operand],
                plan.Definition.Signature.LocalTypes[instruction.Operand],
                updatedBudget),
            AdmittedInstructionKind.StoreLocal => StoreLocal(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                updatedBudget),
            AdmittedInstructionKind.LoadInt32 => Push(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                _domain.ConstInt32(instruction.Operand),
                TypeSig.Int32,
                updatedBudget),
            AdmittedInstructionKind.Add => ApplyBinary(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                BinaryOp.Add,
                updatedBudget),
            AdmittedInstructionKind.Subtract => ApplyBinary(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                BinaryOp.Sub,
                updatedBudget),
            AdmittedInstructionKind.Multiply => ApplyBinary(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                BinaryOp.Mul,
                updatedBudget),
            AdmittedInstructionKind.LoadField => LoadField(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                updatedBudget),
            AdmittedInstructionKind.Return => Return(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                updatedBudget),
            _ => throw new InvalidOperationException(
                $"Frozen plan contains unknown instruction kind {instruction.Kind}."),
        };
    }

    private StepOutcome<TValue, TMemory> StoreLocal(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedMethodPlan plan,
        AdmittedInstruction instruction,
        BudgetState updatedBudget)
    {
        if ((uint)instruction.Operand >= (uint)frame.Locals.Length)
        {
            return InvalidSlot(state, operationalState, frame, instruction, "local", frame.Locals.Length);
        }

        if (frame.EvalStack.Length == 0)
        {
            return InvalidStack(
                state,
                operationalState,
                frame,
                instruction,
                "stloc requires one evaluation-stack value.");
        }

        var value = frame.EvalStack[^1];
        var expectedType = plan.Definition.Signature.LocalTypes[instruction.Operand];
        var failure = ValidateValue(
            value,
            expectedType,
            "stored local",
            instruction.Operand,
            frame.Method,
            frame.IlOffset,
            ValuePrecisionRequirement.Executable);
        if (failure is not null)
        {
            return Failed(state, operationalState, MachineRunStatus.InvalidProgram, failure);
        }

        return CompleteOrdinaryInstruction(
            state,
            operationalState,
            frame,
            instruction,
            frame.EvalStack.RemoveAt(frame.EvalStack.Length - 1),
            frame.Locals.SetItem(instruction.Operand, value),
            updatedBudget);
    }

    private StepOutcome<TValue, TMemory> ApplyBinary(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedMethodPlan plan,
        AdmittedInstruction instruction,
        BinaryOp operation,
        BudgetState updatedBudget)
    {
        if (frame.EvalStack.Length < 2)
        {
            return InvalidStack(
                state,
                operationalState,
                frame,
                instruction,
                $"{instruction.Kind} requires two evaluation-stack values.");
        }

        var left = frame.EvalStack[^2];
        var right = frame.EvalStack[^1];
        var result = _domain.ApplyBinary(operation, left, right);
        var failure = ValidateValue(
            result,
            TypeSig.Int32,
            "arithmetic result",
            0,
            frame.Method,
            frame.IlOffset,
            ValuePrecisionRequirement.Executable);
        if (failure is not null)
        {
            return Failed(state, operationalState, MachineRunStatus.InvalidProgram, failure);
        }

        var stack = frame.EvalStack.RemoveRange(frame.EvalStack.Length - 2, 2).Add(result);
        if (stack.Length > plan.Definition.Body.MaxStack)
        {
            return InvalidStack(
                state,
                operationalState,
                frame,
                instruction,
                "Arithmetic transfer exceeds the method's declared maximum stack depth.");
        }

        return CompleteOrdinaryInstruction(
            state,
            operationalState,
            frame,
            instruction,
            stack,
            frame.Locals,
            updatedBudget);
    }

    private StepOutcome<TValue, TMemory> LoadField(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedMethodPlan plan,
        AdmittedInstruction instruction,
        BudgetState updatedBudget)
    {
        if (instruction.Field is not { } field)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DependencyResolution,
                    "EXEC_FIELD_PLAN_INVALID",
                    "The frozen ldfld instruction contains no resolved field descriptor.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.EvalStack.Length == 0)
        {
            return InvalidStack(
                state,
                operationalState,
                frame,
                instruction,
                "ldfld requires one receiver value.");
        }

        var receiver = frame.EvalStack[^1];
        MemoryLoadResult<TValue> load;
        try
        {
            load = _memoryModel.LoadField(state.Memory, receiver, field);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.MemoryFailure,
                    "EXEC_MEMORY_MODEL_FAILURE",
                    "The memory model rejected the admitted field-load transfer.",
                    frame.Method,
                    frame.IlOffset));
        }

        switch (load.Kind)
        {
            case MemoryLoadKind.Exact:
                var value = load.Value;
                var failure = ValidateValue(
                    value,
                    field.FieldType,
                    "field result",
                    0,
                    frame.Method,
                    frame.IlOffset,
                    ValuePrecisionRequirement.Exact);
                if (failure is not null)
                {
                    return Failed(state, operationalState, MachineRunStatus.InvalidProgram, failure);
                }

                var stack = frame.EvalStack.SetItem(frame.EvalStack.Length - 1, value);
                if (stack.Length > plan.Definition.Body.MaxStack)
                {
                    return InvalidStack(
                        state,
                        operationalState,
                        frame,
                        instruction,
                        "ldfld transfer exceeds the method's declared maximum stack depth.");
                }

                return CompleteOrdinaryInstruction(
                    state,
                    operationalState,
                    frame,
                    instruction,
                    stack,
                    frame.Locals,
                    updatedBudget);

            case MemoryLoadKind.TargetException:
                if (load.Exception is not { } targetException)
                {
                    return Failed(
                        state,
                        operationalState,
                        MachineRunStatus.InvalidProgram,
                        new ExecutionFailure(
                            ExecutionFailureKind.MemoryFailure,
                            "EXEC_TARGET_EXCEPTION_INVALID",
                            "The memory model returned target-exception classification without structured information.",
                            frame.Method,
                            frame.IlOffset));
                }

                if (targetException.Method is { } stampedMethod &&
                    (stampedMethod != frame.Method || targetException.IlOffset != frame.IlOffset))
                {
                    return Failed(
                        state,
                        operationalState,
                        MachineRunStatus.InvalidProgram,
                        new ExecutionFailure(
                            ExecutionFailureKind.MemoryFailure,
                            "EXEC_TARGET_EXCEPTION_LOCATION_CONFLICT",
                            "The memory model returned target-exception information for a different execution location.",
                            frame.Method,
                            frame.IlOffset));
                }

                var exception = targetException.Method.HasValue
                    ? targetException
                    : targetException.WithLocation(frame.Method, frame.IlOffset);
                var terminatedState = state with
                {
                    CallStack = ImmutableArray<FrameState<TValue>>.Empty,
                    ReturnValue = OptionalValue<TValue>.None,
                    TerminalTargetException = exception,
                };
                return new StepOutcome<TValue, TMemory>(
                    terminatedState,
                    operationalState with { Budget = updatedBudget },
                    MachineRunStatus.TargetException,
                    ImmutableArray.Create(
                        new DebugEvent(
                            DebugEventKind.TargetExceptionRaised,
                            frame.Method,
                            frame.IlOffset,
                            instruction.Kind.ToString())),
                    Failure: null,
                    TargetException: exception);

            case MemoryLoadKind.Partial or
                 MemoryLoadKind.Unavailable or
                 MemoryLoadKind.Conflict:
                return MemoryFailed(state, operationalState, frame, load, MachineRunStatus.Blocked);

            case MemoryLoadKind.Invalid:
                return MemoryFailed(state, operationalState, frame, load, MachineRunStatus.InvalidProgram);

            default:
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    new ExecutionFailure(
                        ExecutionFailureKind.MemoryFailure,
                        "EXEC_MEMORY_RESULT_INVALID",
                        "The memory model returned an unknown load classification.",
                        frame.Method,
                        frame.IlOffset));
        }
    }

    private StepOutcome<TValue, TMemory> Push(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedMethodPlan plan,
        AdmittedInstruction instruction,
        TValue value,
        TypeSig expectedType,
        BudgetState updatedBudget)
    {
        var failure = ValidateValue(
            value,
            expectedType,
            "pushed result",
            0,
            frame.Method,
            frame.IlOffset,
            ValuePrecisionRequirement.Executable);
        if (failure is not null)
        {
            return Failed(state, operationalState, MachineRunStatus.InvalidProgram, failure);
        }

        if (frame.EvalStack.Length >= plan.Definition.Body.MaxStack)
        {
            return InvalidStack(
                state,
                operationalState,
                frame,
                instruction,
                "Push transfer exceeds the method's declared maximum stack depth.");
        }

        return CompleteOrdinaryInstruction(
            state,
            operationalState,
            frame,
            instruction,
            frame.EvalStack.Add(value),
            frame.Locals,
            updatedBudget);
    }

    private StepOutcome<TValue, TMemory> Return(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedMethodPlan plan,
        AdmittedInstruction instruction,
        BudgetState updatedBudget)
    {
        var returnsValue = !Equals(plan.Definition.Signature.ReturnType, TypeSig.Void);
        var requiredDepth = returnsValue ? 1 : 0;
        if (frame.EvalStack.Length != requiredDepth)
        {
            return InvalidStack(
                state,
                operationalState,
                frame,
                instruction,
                returnsValue
                    ? "A value-returning method must execute ret with exactly one stack value."
                    : "A void method must execute ret with an empty evaluation stack.");
        }

        var returnValue = returnsValue
            ? OptionalValue<TValue>.Some(frame.EvalStack[0])
            : OptionalValue<TValue>.None;
        var nextState = state with
        {
            CallStack = ImmutableArray<FrameState<TValue>>.Empty,
            ReturnValue = returnValue,
        };
        var nextOperationalState = operationalState with { Budget = updatedBudget };
        var events = ImmutableArray.Create(
            ExecutedEvent(frame, instruction),
            new DebugEvent(
                DebugEventKind.FramePopped,
                frame.Method,
                frame.IlOffset,
                instruction.Kind.ToString()));

        return new StepOutcome<TValue, TMemory>(
            nextState,
            nextOperationalState,
            MachineRunStatus.Completed,
            events);
    }

    private static StepOutcome<TValue, TMemory> CompleteOrdinaryInstruction(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedInstruction instruction,
        ImmutableArray<TValue> evalStack,
        ImmutableArray<TValue> locals,
        BudgetState updatedBudget)
    {
        var nextFrame = frame with
        {
            IlOffset = checked(frame.IlOffset + instruction.Size),
            EvalStack = evalStack,
            Locals = locals,
        };
        var nextState = state with
        {
            CallStack = state.CallStack.SetItem(state.CallStack.Length - 1, nextFrame),
        };
        return new StepOutcome<TValue, TMemory>(
            nextState,
            operationalState with { Budget = updatedBudget },
            MachineRunStatus.Ready,
            ImmutableArray.Create(ExecutedEvent(frame, instruction)));
    }

    private static StepOutcome<TValue, TMemory> InvalidSlot(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedInstruction instruction,
        string slotKind,
        int slotCount) =>
        Failed(
            state,
            operationalState,
            MachineRunStatus.InvalidProgram,
            new ExecutionFailure(
                ExecutionFailureKind.InvalidSlot,
                "EXEC_INVALID_SLOT",
                $"{instruction.Kind} references {slotKind} slot {instruction.Operand}, but the frame has {slotCount} {slotKind} slot(s).",
                frame.Method,
                frame.IlOffset));

    private static StepOutcome<TValue, TMemory> InvalidStack(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        AdmittedInstruction instruction,
        string message) =>
        Failed(
            state,
            operationalState,
            MachineRunStatus.InvalidProgram,
            new ExecutionFailure(
                ExecutionFailureKind.InvalidStack,
                "EXEC_INVALID_STACK",
                message,
                frame.Method,
                frame.IlOffset));

    private static StepOutcome<TValue, TMemory> MemoryFailed(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        MemoryLoadResult<TValue> load,
        MachineRunStatus status) =>
        Failed(
            state,
            operationalState,
            status,
            new ExecutionFailure(
                ExecutionFailureKind.MemoryFailure,
                load.FailureCode ?? "EXEC_MEMORY_EVIDENCE_UNAVAILABLE",
                "The admitted field load could not obtain one exact typed memory value.",
                frame.Method,
                frame.IlOffset));

    private static DebugEvent ExecutedEvent(
        FrameState<TValue> frame,
        AdmittedInstruction instruction) =>
        new(
            DebugEventKind.InstructionExecuted,
            frame.Method,
            frame.IlOffset,
            instruction.Kind.ToString());
}
