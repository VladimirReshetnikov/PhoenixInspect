using System.Collections.Concurrent;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Executes deterministic single-instruction transfers through a caller-supplied value domain while threading a
/// persistent memory snapshot unchanged until a memory-touching opcode is admitted.
/// </summary>
/// <typeparam name="TValue">The value-domain representation used by arguments, locals, and the evaluation stack.</typeparam>
/// <typeparam name="TMemory">The persistent memory snapshot representation threaded through machine state.</typeparam>
/// <remarks>
/// The current executable slice admits primitive integer constants, argument/local traffic, <c>add</c>, <c>sub</c>,
/// <c>mul</c>, <c>nop</c>, and <c>ret</c>. Every other opcode blocks explicitly without consuming instruction
/// budget or emitting an <see cref="DebugEventKind.InstructionExecuted"/> event.
/// </remarks>
public sealed class IlMachine<TValue, TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    private const int MaxAdmittedCodeBytes = 4096;
    private const int MaxAdmittedInstructions = 1024;

    /// <summary>
    /// Gets the largest argument, local, or evaluation-stack vector inspected by the bounded prototype machine.
    /// </summary>
    public const int MaximumFrameSlotCount = 1024;
    private readonly IValueDomain<TValue> _domain;
    private readonly IResolutionServices _resolver;
    private readonly IBudgetPolicy _budgetPolicy;
    private readonly ConcurrentDictionary<MethodHandle, Lazy<ResolutionResult<MethodBody>>> _bodyCache = new();
    private readonly ConcurrentDictionary<AdmissionCacheKey, Lazy<MethodAdmissionResult>> _admissionCache = new();
    private readonly object _sessionBindingGate = new();
    private AdmissionCacheKey? _sessionBinding;

    /// <summary>
    /// Creates a draft IL machine over explicit semantic capabilities.
    /// </summary>
    /// <param name="domain">The value lattice and primitive transfer implementation.</param>
    /// <param name="resolver">The structured method-body and metadata resolver.</param>
    /// <param name="budgetPolicy">The deterministic resource-consumption policy.</param>
    public IlMachine(
        IValueDomain<TValue> domain,
        IResolutionServices resolver,
        IBudgetPolicy budgetPolicy)
    {
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _budgetPolicy = budgetPolicy ?? throw new ArgumentNullException(nameof(budgetPolicy));
    }

    /// <summary>
    /// Attempts to execute exactly one instruction from the active frame.
    /// </summary>
    /// <param name="state">The immutable semantic state before the requested transition.</param>
    /// <param name="operationalState">Deterministic budget bookkeeping excluded from semantic equality.</param>
    /// <returns>
    /// A next state and events for a successful transfer, or the unchanged state with a structured reason when no
    /// instruction could run.
    /// </returns>
    public StepOutcome<TValue, TMemory> StepOne(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(operationalState);
        if (state.CallStack.IsDefault)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_CALL_STACK",
                    "Machine state contains an uninitialized call-stack array."));
        }

        if (state.Memory is null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DependencyResolution,
                    "EXEC_INVALID_MEMORY_STATE",
                    "Machine state contains a null persistent-memory snapshot."));
        }

        if (operationalState.Budget is null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_OPERATIONAL_STATE",
                    "Operational state contains no budget snapshot."));
        }

        if (state.CallStack.Length > 0 && state.ReturnValue.HasValue)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_STALE_RETURN_VALUE",
                    "An in-progress machine state cannot carry a terminal return value."));
        }

        if (state.CallStack.Length == 0)
        {
            return new StepOutcome<TValue, TMemory>(
                state,
                operationalState,
                MachineRunStatus.Completed,
                ImmutableArray<DebugEvent>.Empty);
        }

        if (state.CallStack.Length != 1)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_NESTED_FRAME_UNSUPPORTED",
                    "The admitted slice has no call or continuation semantics and therefore accepts exactly one root frame."));
        }

        var frame = state.CallStack[^1];
        if (frame is null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_FRAME",
                    "The root call-stack slot contains no frame."));
        }

        if (!MethodHandle.IsValidMetadataToken(frame.Method.MetadataToken))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_METHOD_HANDLE",
                    "The active frame does not identify a non-nil MethodDef token.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.Arguments.IsDefault || frame.Locals.IsDefault || frame.EvalStack.IsDefault)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidSlot,
                    "EXEC_INVALID_FRAME_SLOTS",
                    "The active frame contains an uninitialized argument, local, or evaluation-stack array.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.Arguments.Length > MaximumFrameSlotCount ||
            frame.Locals.Length > MaximumFrameSlotCount ||
            frame.EvalStack.Length > MaximumFrameSlotCount)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_FRAME_SLOT_LIMIT",
                    $"Arguments, locals, and evaluation stack are each limited to {MaximumFrameSlotCount} values.",
                    frame.Method,
                    frame.IlOffset));
        }

        var admissionKey = new AdmissionCacheKey(
            frame.Method,
            frame.Arguments.Length,
            frame.Locals.Length,
            frame.ReturnsValue);
        lock (_sessionBindingGate)
        {
            if (_sessionBinding is null)
            {
                _sessionBinding = admissionKey;
            }
            else if (_sessionBinding.Value != admissionKey)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.Blocked,
                    new ExecutionFailure(
                        ExecutionFailureKind.ResourceLimit,
                        "EXEC_MACHINE_SESSION_MISMATCH",
                        "One prototype machine snapshots exactly one root method and activation shape.",
                        frame.Method,
                        frame.IlOffset));
            }
        }

        var bodyResult = _bodyCache.GetOrAdd(
            frame.Method,
            method => new Lazy<ResolutionResult<MethodBody>>(
                () => _resolver.GetMethodBody(method),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if (!bodyResult.IsSuccess)
        {
            var resolutionFailure = bodyResult.Failure
                ?? new ResolutionFailure(ResolutionFailureKind.Invalid, "RESOLUTION_INVALID_RESULT", "Resolver returned an invalid default result.");
            var status = resolutionFailure.Kind == ResolutionFailureKind.Invalid
                ? MachineRunStatus.InvalidProgram
                : MachineRunStatus.Blocked;
            return Failed(
                state,
                operationalState,
                status,
                new ExecutionFailure(
                    ExecutionFailureKind.DependencyResolution,
                    resolutionFailure.Code,
                    resolutionFailure.Message,
                    frame.Method,
                    frame.IlOffset,
                    resolutionFailure));
        }

        var body = bodyResult.Value;
        if (body is null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DependencyResolution,
                    "RESOLUTION_NULL_BODY",
                    "Resolver reported success without a method body.",
                    frame.Method,
                    frame.IlOffset));
        }
        var admission = _admissionCache.GetOrAdd(
            admissionKey,
            key => new Lazy<MethodAdmissionResult>(
                () => ValidateMethod(
                    key.Method,
                    body,
                    key.ArgumentCount,
                    key.LocalCount,
                    key.ReturnsValue),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if (!admission.IsAdmitted)
        {
            return Failed(state, operationalState, admission.FailureStatus, admission.Failure!);
        }

        var boundaryFound = false;
        var expectedStackDepth = 0;
        foreach (var boundary in admission.InstructionBoundaries)
        {
            if (boundary.IlOffset == frame.IlOffset)
            {
                boundaryFound = true;
                expectedStackDepth = boundary.ExpectedStackDepth;
                break;
            }
        }

        if (!boundaryFound)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_INSTRUCTION_OFFSET",
                    $"IL offset {frame.IlOffset} is not an instruction boundary in the admitted method body.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.EvalStack.Length != expectedStackDepth)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidStack,
                    "EXEC_INVALID_ENTRY_STACK",
                    $"IL offset {frame.IlOffset} requires evaluation-stack depth {expectedStackDepth}, but the frame carries {frame.EvalStack.Length} value(s).",
                    frame.Method,
                    frame.IlOffset));
        }

        try
        {
            var shapeFailure = ValidateInt32FrameValues(frame);
            if (shapeFailure is not null)
            {
                return Failed(state, operationalState, MachineRunStatus.InvalidProgram, shapeFailure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_DOMAIN_SHAPE_FAILURE",
                    "The value domain could not classify the frame's admitted Int32 slots.",
                    frame.Method,
                    frame.IlOffset));
        }

        var decode = PrototypeDecoder.Decode(body.CodeBytes, frame.IlOffset, frame.Method);
        if (!decode.IsSuccess)
        {
            return Failed(state, operationalState, decode.Status, decode.Failure!);
        }

        var updatedBudget = operationalState.Budget;
        if (!_budgetPolicy.TryConsumeInstruction(ref updatedBudget, 1))
        {
            return new StepOutcome<TValue, TMemory>(
                state,
                operationalState,
                MachineRunStatus.BudgetExhausted,
                ImmutableArray<DebugEvent>.Empty);
        }

        try
        {
            return Execute(state, operationalState, frame, body, decode.Instruction, updatedBudget);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ArithmeticException or NotSupportedException)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_DOMAIN_FAILURE",
                    "The value domain rejected the admitted instruction transfer.",
                    frame.Method,
                    frame.IlOffset));
        }
    }

    /// <summary>
    /// Validates an entire body against the current straight-line arithmetic semantics slice.
    /// </summary>
    /// <param name="method">The definition handle used in structured diagnostics.</param>
    /// <param name="body">The immutable body and preserved admission facts to validate.</param>
    /// <param name="argumentCount">The number of arguments that frame seeding will provide.</param>
    /// <param name="localCount">The number of explicitly pre-seeded primitive locals.</param>
    /// <param name="returnsValue">Whether the method must finish with one return value on the stack.</param>
    /// <returns>An admitted instruction count or the first deterministic whole-body rejection.</returns>
    /// <remarks>
    /// Exception regions are rejected because handler transfer is not implemented. A local signature is admitted
    /// only for an explicit fixture that supplies at least one local slot; type/signature decoding remains a later
    /// slice. The validator also simulates stack depth and slot operands for this branch-free opcode set.
    /// </remarks>
    public MethodAdmissionResult ValidateMethod(
        MethodHandle method,
        MethodBody body,
        int argumentCount,
        int localCount,
        bool returnsValue)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (argumentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(argumentCount));
        }

        if (localCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(localCount));
        }

        if (argumentCount > MaximumFrameSlotCount || localCount > MaximumFrameSlotCount)
        {
            return Rejected(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_FRAME_SLOT_LIMIT",
                $"Argument and local counts are each limited to {MaximumFrameSlotCount} values.",
                method,
                0);
        }

        if (body.ExceptionRegionCount != 0)
        {
            if (body.ExceptionRegionCount < 0)
            {
                return Rejected(
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_EH_COUNT",
                    $"Method {method} declares negative exception-region count {body.ExceptionRegionCount}.",
                    method,
                    0);
            }

            return Rejected(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_EH_UNSUPPORTED",
                $"Method {method} contains {body.ExceptionRegionCount} exception region(s), but handler transfer is not admitted.",
                method,
                0);
        }

        if (body.CodeBytes.IsDefault)
        {
            return Rejected(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_CODE_BUFFER",
                $"Method {method} contains an uninitialized IL byte array.",
                method,
                0);
        }

        if (body.CodeBytes.Length > MaxAdmittedCodeBytes)
        {
            return Rejected(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_BODY_TOO_LARGE",
                $"Method body length {body.CodeBytes.Length} exceeds the bounded prototype limit of {MaxAdmittedCodeBytes} bytes.",
                method,
                0);
        }

        if (body.HasLocalSignature &&
            ((body.LocalSignatureToken & unchecked((int)0xFF000000)) != 0x11000000 ||
             (body.LocalSignatureToken & 0x00FFFFFF) == 0))
        {
            return Rejected(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_LOCAL_SIGNATURE",
                $"Local signature token 0x{body.LocalSignatureToken:X8} is not a StandAloneSig token.",
                method,
                0);
        }

        if (body.HasLocalSignature != (localCount > 0))
        {
            return Rejected(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_LOCAL_LAYOUT_UNAVAILABLE",
                body.HasLocalSignature
                    ? "The method declares locals, but no explicit primitive local fixture was seeded."
                    : "The frame seeds locals, but the method body carries no local signature evidence.",
                method,
                0);
        }

        if (body.MaxStack < 0)
        {
            return Rejected(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidStack,
                "EXEC_INVALID_MAXSTACK",
                $"Method {method} declares negative maxstack {body.MaxStack}.",
                method,
                0);
        }

        var offset = 0;
        var stackDepth = 0;
        var instructionCount = 0;
        var sawReturn = false;
        var boundaries = ImmutableArray.CreateBuilder<MethodInstructionBoundary>();
        while (offset < body.CodeBytes.Length)
        {
            if (instructionCount >= MaxAdmittedInstructions)
            {
                return Rejected(
                    MachineRunStatus.Blocked,
                    ExecutionFailureKind.UnsupportedInstruction,
                    "EXEC_TOO_MANY_INSTRUCTIONS",
                    $"Method body exceeds the bounded prototype limit of {MaxAdmittedInstructions} instructions.",
                    method,
                    offset);
            }

            boundaries.Add(new MethodInstructionBoundary(offset, stackDepth));
            var decode = PrototypeDecoder.Decode(body.CodeBytes, offset, method);
            if (!decode.IsSuccess)
            {
                return new MethodAdmissionResult(
                    false,
                    instructionCount,
                    boundaries.ToImmutable(),
                    decode.Status,
                    decode.Failure);
            }

            var instruction = decode.Instruction;
            switch (instruction.Kind)
            {
                case PrototypeInstructionKind.Nop:
                    break;

                case PrototypeInstructionKind.LoadArgument:
                    if ((uint)instruction.Operand >= (uint)argumentCount)
                    {
                        return Rejected(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidSlot,
                            "EXEC_INVALID_SLOT",
                            $"LoadArgument references slot {instruction.Operand}, but the fixture provides {argumentCount} arguments.",
                            method,
                            offset);
                    }

                    stackDepth++;
                    break;

                case PrototypeInstructionKind.LoadLocal:
                    if ((uint)instruction.Operand >= (uint)localCount)
                    {
                        return Rejected(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidSlot,
                            "EXEC_INVALID_SLOT",
                            $"LoadLocal references slot {instruction.Operand}, but the fixture provides {localCount} locals.",
                            method,
                            offset);
                    }

                    stackDepth++;
                    break;

                case PrototypeInstructionKind.StoreLocal:
                    if ((uint)instruction.Operand >= (uint)localCount)
                    {
                        return Rejected(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidSlot,
                            "EXEC_INVALID_SLOT",
                            $"StoreLocal references slot {instruction.Operand}, but the fixture provides {localCount} locals.",
                            method,
                            offset);
                    }

                    if (stackDepth < 1)
                    {
                        return Rejected(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidStack,
                            "EXEC_INVALID_STACK",
                            "StoreLocal requires one evaluation-stack value.",
                            method,
                            offset);
                    }

                    stackDepth--;
                    break;

                case PrototypeInstructionKind.LoadInt32:
                    stackDepth++;
                    break;

                case PrototypeInstructionKind.Add or PrototypeInstructionKind.Subtract or PrototypeInstructionKind.Multiply:
                    if (stackDepth < 2)
                    {
                        return Rejected(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidStack,
                            "EXEC_INVALID_STACK",
                            $"{instruction.Kind} requires two evaluation-stack values.",
                            method,
                            offset);
                    }

                    stackDepth--;
                    break;

                case PrototypeInstructionKind.Return:
                    var expectedDepth = returnsValue ? 1 : 0;
                    if (stackDepth != expectedDepth)
                    {
                        return Rejected(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidStack,
                            "EXEC_INVALID_STACK",
                            $"Return requires stack depth {expectedDepth}, but admission observed {stackDepth}.",
                            method,
                            offset);
                    }

                    if (offset + instruction.Size != body.CodeBytes.Length)
                    {
                        return Rejected(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidInstruction,
                            "EXEC_CODE_AFTER_RETURN",
                            "The straight-line prototype slice does not admit instructions after ret.",
                            method,
                            offset);
                    }

                    sawReturn = true;
                    break;
            }

            if (stackDepth > body.MaxStack)
            {
                return Rejected(
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.InvalidStack,
                    "EXEC_MAXSTACK_EXCEEDED",
                    $"Admission stack depth {stackDepth} exceeds declared maxstack {body.MaxStack}.",
                    method,
                    offset);
            }

            instructionCount++;
            offset = checked(offset + instruction.Size);
        }

        return sawReturn
            ? new MethodAdmissionResult(
                true,
                instructionCount,
                boundaries.ToImmutable(),
                MachineRunStatus.Ready,
                null)
            : Rejected(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_MISSING_RETURN",
                "The straight-line method body does not terminate in ret.",
                method,
                body.CodeBytes.Length);
    }

    private static MethodAdmissionResult Rejected(
        MachineRunStatus status,
        ExecutionFailureKind kind,
        string code,
        string message,
        MethodHandle method,
        int offset) =>
        new(
            false,
            0,
            ImmutableArray<MethodInstructionBoundary>.Empty,
            status,
            new ExecutionFailure(kind, code, message, method, offset));

    private StepOutcome<TValue, TMemory> Execute(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        MethodBody body,
        PrototypeInstruction instruction,
        BudgetState updatedBudget)
    {
        switch (instruction.Kind)
        {
            case PrototypeInstructionKind.Nop:
                return CompleteOrdinaryInstruction(state, operationalState, frame, instruction, frame.EvalStack, frame.Locals, updatedBudget);

            case PrototypeInstructionKind.LoadArgument:
                if ((uint)instruction.Operand >= (uint)frame.Arguments.Length)
                {
                    return InvalidSlot(state, operationalState, frame, instruction, "argument", frame.Arguments.Length);
                }

                return Push(state, operationalState, frame, body, instruction, frame.Arguments[instruction.Operand], updatedBudget);

            case PrototypeInstructionKind.LoadLocal:
                if ((uint)instruction.Operand >= (uint)frame.Locals.Length)
                {
                    return InvalidSlot(state, operationalState, frame, instruction, "local", frame.Locals.Length);
                }

                return Push(state, operationalState, frame, body, instruction, frame.Locals[instruction.Operand], updatedBudget);

            case PrototypeInstructionKind.StoreLocal:
                if ((uint)instruction.Operand >= (uint)frame.Locals.Length)
                {
                    return InvalidSlot(state, operationalState, frame, instruction, "local", frame.Locals.Length);
                }

                if (frame.EvalStack.Length == 0)
                {
                    return InvalidStack(state, operationalState, frame, instruction, "stloc requires one evaluation-stack value.");
                }

                var storedValue = frame.EvalStack[^1];
                return CompleteOrdinaryInstruction(
                    state,
                    operationalState,
                    frame,
                    instruction,
                    frame.EvalStack.RemoveAt(frame.EvalStack.Length - 1),
                    frame.Locals.SetItem(instruction.Operand, storedValue),
                    updatedBudget);

            case PrototypeInstructionKind.LoadInt32:
                return Push(state, operationalState, frame, body, instruction, _domain.ConstInt32(instruction.Operand), updatedBudget);

            case PrototypeInstructionKind.Add:
                return ApplyBinary(state, operationalState, frame, body, instruction, BinaryOp.Add, updatedBudget);

            case PrototypeInstructionKind.Subtract:
                return ApplyBinary(state, operationalState, frame, body, instruction, BinaryOp.Sub, updatedBudget);

            case PrototypeInstructionKind.Multiply:
                return ApplyBinary(state, operationalState, frame, body, instruction, BinaryOp.Mul, updatedBudget);

            case PrototypeInstructionKind.Return:
                return Return(state, operationalState, frame, instruction, updatedBudget);

            default:
                throw new InvalidOperationException($"Decoder admitted unknown instruction kind {instruction.Kind}.");
        }
    }

    private StepOutcome<TValue, TMemory> ApplyBinary(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        MethodBody body,
        PrototypeInstruction instruction,
        BinaryOp operation,
        BudgetState updatedBudget)
    {
        if (frame.EvalStack.Length < 2)
        {
            return InvalidStack(state, operationalState, frame, instruction, $"{instruction.Kind} requires two evaluation-stack values.");
        }

        var left = frame.EvalStack[^2];
        var right = frame.EvalStack[^1];
        var result = _domain.ApplyBinary(operation, left, right);
        if (_domain.GetStackKind(result) != StackKind.I4)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_NON_I4_RESULT",
                    $"{instruction.Kind} produced a non-I4 value in the admitted Int32 slice.",
                    frame.Method,
                    frame.IlOffset));
        }

        var stack = frame.EvalStack.RemoveRange(frame.EvalStack.Length - 2, 2).Add(result);
        if (stack.Length > body.MaxStack)
        {
            return InvalidStack(state, operationalState, frame, instruction, $"Transfer exceeds declared maxstack {body.MaxStack}.");
        }

        return CompleteOrdinaryInstruction(state, operationalState, frame, instruction, stack, frame.Locals, updatedBudget);
    }

    private StepOutcome<TValue, TMemory> Push(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        MethodBody body,
        PrototypeInstruction instruction,
        TValue value,
        BudgetState updatedBudget)
    {
        if (_domain.GetStackKind(value) != StackKind.I4)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_NON_I4_RESULT",
                    $"{instruction.Kind} produced a non-I4 value in the admitted Int32 slice.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.EvalStack.Length >= body.MaxStack)
        {
            return InvalidStack(state, operationalState, frame, instruction, $"Transfer exceeds declared maxstack {body.MaxStack}.");
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

    private static StepOutcome<TValue, TMemory> Return(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        PrototypeInstruction instruction,
        BudgetState updatedBudget)
    {
        var requiredDepth = frame.ReturnsValue ? 1 : 0;
        if (frame.EvalStack.Length != requiredDepth)
        {
            return InvalidStack(
                state,
                operationalState,
                frame,
                instruction,
                frame.ReturnsValue
                    ? "A value-returning method must execute ret with exactly one stack value."
                    : "A void method must execute ret with an empty evaluation stack.");
        }

        var returnValue = frame.ReturnsValue
            ? OptionalValue<TValue>.Some(frame.EvalStack[0])
            : OptionalValue<TValue>.None;
        var callStack = ImmutableArray<FrameState<TValue>>.Empty;

        var nextState = state with
        {
            CallStack = callStack,
            ReturnValue = returnValue,
        };
        var nextOperationalState = operationalState with { Budget = updatedBudget };
        var events = ImmutableArray.Create(
            ExecutedEvent(frame, instruction),
            new DebugEvent(DebugEventKind.FramePopped, frame.Method, frame.IlOffset, instruction.Kind.ToString()));

        return new StepOutcome<TValue, TMemory>(
            nextState,
            nextOperationalState,
            MachineRunStatus.Completed,
            events);
    }

    private ExecutionFailure? ValidateInt32FrameValues(FrameState<TValue> frame)
    {
        var failure = FindNonInt32Value(frame.Arguments, "argument", frame);
        failure ??= FindNonInt32Value(frame.Locals, "local", frame);
        failure ??= FindNonInt32Value(frame.EvalStack, "evaluation-stack", frame);
        return failure;
    }

    private ExecutionFailure? FindNonInt32Value(
        ImmutableArray<TValue> values,
        string slotKind,
        FrameState<TValue> frame)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (_domain.IsBottom(values[index]))
            {
                return new ExecutionFailure(
                    ExecutionFailureKind.InvalidSlot,
                    "EXEC_INFEASIBLE_VALUE",
                    $"The {slotKind} value at slot {index} is lattice bottom and cannot occur in an executable state.",
                    frame.Method,
                    frame.IlOffset);
            }

            if (_domain.GetStackKind(values[index]) != StackKind.I4)
            {
                return new ExecutionFailure(
                    ExecutionFailureKind.InvalidSlot,
                    "EXEC_NON_I4_VALUE",
                    $"The admitted Int32 slice requires I4 {slotKind} values, but slot {index} has a different stack kind.",
                    frame.Method,
                    frame.IlOffset);
            }
        }

        return null;
    }

    private static StepOutcome<TValue, TMemory> CompleteOrdinaryInstruction(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        PrototypeInstruction instruction,
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
        var nextOperationalState = operationalState with { Budget = updatedBudget };
        return new StepOutcome<TValue, TMemory>(
            nextState,
            nextOperationalState,
            MachineRunStatus.Ready,
            ImmutableArray.Create(ExecutedEvent(frame, instruction)));
    }

    private static StepOutcome<TValue, TMemory> InvalidSlot(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        PrototypeInstruction instruction,
        string slotKind,
        int slotCount) =>
        Failed(
            state,
            operationalState,
            MachineRunStatus.InvalidProgram,
            new ExecutionFailure(
                ExecutionFailureKind.InvalidSlot,
                "EXEC_INVALID_SLOT",
                $"{instruction.Kind} references {slotKind} slot {instruction.Operand}, but the frame has {slotCount} {slotKind} slots.",
                frame.Method,
                frame.IlOffset));

    private static StepOutcome<TValue, TMemory> InvalidStack(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrameState<TValue> frame,
        PrototypeInstruction instruction,
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

    private static StepOutcome<TValue, TMemory> Failed(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        MachineRunStatus status,
        ExecutionFailure failure) =>
        new(state, operationalState, status, ImmutableArray<DebugEvent>.Empty, failure);

    private static DebugEvent ExecutedEvent(FrameState<TValue> frame, PrototypeInstruction instruction) =>
        new(DebugEventKind.InstructionExecuted, frame.Method, frame.IlOffset, instruction.Kind.ToString());

    private enum PrototypeInstructionKind
    {
        Nop,
        LoadArgument,
        LoadLocal,
        StoreLocal,
        LoadInt32,
        Add,
        Subtract,
        Multiply,
        Return,
    }

    private readonly record struct PrototypeInstruction(PrototypeInstructionKind Kind, int Operand, int Size);

    private readonly record struct AdmissionCacheKey(
        MethodHandle Method,
        int ArgumentCount,
        int LocalCount,
        bool ReturnsValue);

    private readonly record struct DecodeResult(
        bool IsSuccess,
        PrototypeInstruction Instruction,
        MachineRunStatus Status,
        ExecutionFailure? Failure);

    private static class PrototypeDecoder
    {
        public static DecodeResult Decode(ImmutableArray<byte> code, int offset, MethodHandle method)
        {
            if ((uint)offset >= (uint)code.Length)
            {
                return Invalid(method, offset, "EXEC_INVALID_IL_OFFSET", $"IL offset {offset} is outside a {code.Length}-byte method body.");
            }

            var opcode = code[offset];
            return opcode switch
            {
                0x00 => Success(PrototypeInstructionKind.Nop),
                >= 0x02 and <= 0x05 => Success(PrototypeInstructionKind.LoadArgument, opcode - 0x02),
                >= 0x06 and <= 0x09 => Success(PrototypeInstructionKind.LoadLocal, opcode - 0x06),
                >= 0x0A and <= 0x0D => Success(PrototypeInstructionKind.StoreLocal, opcode - 0x0A),
                0x0E => OneByteOperand(PrototypeInstructionKind.LoadArgument),
                0x11 => OneByteOperand(PrototypeInstructionKind.LoadLocal),
                0x13 => OneByteOperand(PrototypeInstructionKind.StoreLocal),
                0x15 => Success(PrototypeInstructionKind.LoadInt32, -1),
                >= 0x16 and <= 0x1E => Success(PrototypeInstructionKind.LoadInt32, opcode - 0x16),
                0x1F => OneByteOperand(PrototypeInstructionKind.LoadInt32, signed: true),
                0x20 => Int32Operand(),
                0x2A => Success(PrototypeInstructionKind.Return),
                0x58 => Success(PrototypeInstructionKind.Add),
                0x59 => Success(PrototypeInstructionKind.Subtract),
                0x5A => Success(PrototypeInstructionKind.Multiply),
                _ => new DecodeResult(
                    false,
                    default,
                    MachineRunStatus.Blocked,
                    new ExecutionFailure(
                        ExecutionFailureKind.UnsupportedInstruction,
                        "EXEC_UNSUPPORTED_OPCODE",
                        $"Opcode 0x{opcode:X2} is outside the admitted arithmetic prototype slice.",
                        method,
                        offset)),
            };

            DecodeResult Success(PrototypeInstructionKind kind, int operand = 0) =>
                new(true, new PrototypeInstruction(kind, operand, 1), MachineRunStatus.Ready, null);

            DecodeResult OneByteOperand(PrototypeInstructionKind kind, bool signed = false)
            {
                if (offset + 1 >= code.Length)
                {
                    return Invalid(method, offset, "EXEC_TRUNCATED_INSTRUCTION", $"Opcode 0x{opcode:X2} is missing its one-byte operand.");
                }

                var operand = signed ? unchecked((int)(sbyte)code[offset + 1]) : code[offset + 1];
                return new DecodeResult(true, new PrototypeInstruction(kind, operand, 2), MachineRunStatus.Ready, null);
            }

            DecodeResult Int32Operand()
            {
                if (offset > code.Length - 5)
                {
                    return Invalid(method, offset, "EXEC_TRUNCATED_INSTRUCTION", "ldc.i4 is missing its four-byte operand.");
                }

                var operand = code[offset + 1]
                    | (code[offset + 2] << 8)
                    | (code[offset + 3] << 16)
                    | (code[offset + 4] << 24);
                return new DecodeResult(true, new PrototypeInstruction(PrototypeInstructionKind.LoadInt32, operand, 5), MachineRunStatus.Ready, null);
            }
        }

        private static DecodeResult Invalid(MethodHandle method, int offset, string code, string message) =>
            new(
                false,
                default,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    code,
                    message,
                    method,
                    offset));
    }
}
