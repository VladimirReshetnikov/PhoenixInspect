using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.IL;

namespace Interpreter.Core.Execution;

/// <summary>
/// Executes draft IL machine micro-steps using resolver-provided method bodies.
/// </summary>
/// <typeparam name="TValue">Abstract value representation used by frame evaluation stacks.</typeparam>
/// <typeparam name="TMemory">Abstract memory representation threaded through machine state.</typeparam>
public sealed class IlMachine<TValue, TMemory>
{
    private const byte RetOpcode = 0x2A;
    private readonly IResolutionServices _resolver;
    private readonly IBudgetPolicy _budgetPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="IlMachine{TValue, TMemory}"/> class.
    /// </summary>
    /// <param name="resolver">Resolver used to map method handles to method bodies.</param>
    /// <param name="budgetPolicy">Budget policy used to consume bounded execution resources.</param>
    public IlMachine(IResolutionServices resolver, IBudgetPolicy budgetPolicy)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _budgetPolicy = budgetPolicy ?? throw new ArgumentNullException(nameof(budgetPolicy));
    }

    /// <summary>
    /// Executes exactly one instruction for the currently active frame.
    /// </summary>
    /// <param name="state">Input machine state prior to micro-step execution.</param>
    /// <returns>
    /// A <see cref="StepOutcome{TValue, TMemory}"/> containing updated state, stop reason, and deterministic debug events.
    /// </returns>
    /// <remarks>
    /// This draft-phase implementation intentionally supports only the single-byte <c>ret</c> opcode required by the
    /// ret-only integration seam. Additional opcodes and semantic checks are intentionally deferred.
    /// </remarks>
    public StepOutcome<TValue, TMemory> StepOne(MachineState<TValue, TMemory> state)
    {
        if (state.CallStack.Length == 0)
        {
            return new StepOutcome<TValue, TMemory>(state, StopReason.Completed, ImmutableArray<DebugEvent>.Empty);
        }

        var updatedBudget = state.Budget;
        if (!_budgetPolicy.TryConsumeInstruction(ref updatedBudget, 1))
        {
            return new StepOutcome<TValue, TMemory>(
                state,
                StopReason.BudgetExceeded,
                ImmutableArray.Create(new DebugEvent(DebugEventKind.InstructionExecuted, "budget exhausted before instruction decode")));
        }

        var frame = state.CallStack[^1];
        if (!_resolver.TryGetMethodBody(frame.Method, out MethodBody body))
        {
            return new StepOutcome<TValue, TMemory>(
                state with { Budget = updatedBudget },
                StopReason.Faulted,
                ImmutableArray.Create(new DebugEvent(DebugEventKind.InstructionExecuted, "method body missing")));
        }

        if ((uint)frame.IlOffset >= (uint)body.CodeBytes.Length)
        {
            return new StepOutcome<TValue, TMemory>(
                state with { Budget = updatedBudget },
                StopReason.Faulted,
                ImmutableArray.Create(new DebugEvent(DebugEventKind.InstructionExecuted, $"invalid IL offset {frame.IlOffset}")));
        }

        var opcode = body.CodeBytes.Span[frame.IlOffset];
        if (opcode != RetOpcode)
        {
            return new StepOutcome<TValue, TMemory>(
                state with { Budget = updatedBudget },
                StopReason.Faulted,
                ImmutableArray.Create(new DebugEvent(DebugEventKind.InstructionExecuted, $"unsupported opcode 0x{opcode:X2}")));
        }

        var newStack = state.CallStack.RemoveAt(state.CallStack.Length - 1);
        var nextState = state with { CallStack = newStack, Budget = updatedBudget };
        var events = ImmutableArray.Create(
            new DebugEvent(DebugEventKind.InstructionExecuted, $"ret @{frame.IlOffset}"),
            new DebugEvent(DebugEventKind.FramePopped, $"method-handle:{frame.Method.Value}"));

        return new StepOutcome<TValue, TMemory>(
            nextState,
            newStack.Length == 0 ? StopReason.Completed : StopReason.Running,
            events);
    }
}
