using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Validates and projects one complete certified W4 legacy-machine null-reference execution into a product fragment.
/// </summary>
/// <remarks>
/// Projection requires every exact core-issued outcome from activation through the first terminal transition. The
/// sequence may end there or include one certified idempotent re-step of that latch. Publicly constructible machine
/// records, naked terminal states, and caller-authored event transcripts are insufficient evidence. Prepared-graph
/// execution and every non-null-reference target outcome remain outside this draft conformance-only surface.
/// </remarks>
public static class CounterfactualTargetOutcomeProjector
{
    private const int MaximumConsumingTransitionCount = 4096;
    private const int MaximumCompleteTransitionCount = MaximumConsumingTransitionCount + 1;
    private const string NullReferenceCode = "TARGET_NULL_REFERENCE";

    private static readonly EvaluationDiagnostic OutcomeRequiredFailure = new(
        "W4.TargetException.OutcomeRequired",
        "A complete sequence of core-issued machine transitions is required.");

    private static readonly EvaluationDiagnostic LatchInvalidFailure = new(
        "W4.TargetException.LatchInvalid",
        "The terminal target-exception latch does not describe one valid machine transition.");

    private static readonly EvaluationDiagnostic KindUnsupportedFailure = new(
        "W4.TargetException.KindUnsupported",
        "Only the admitted exact null-reference target exception can be projected.");

    private static readonly EvaluationDiagnostic LocationInvalidFailure = new(
        "W4.TargetException.LocationInvalid",
        "The target exception does not carry one matching structural method and IL offset.");

    private static readonly EvaluationDiagnostic ExecutionModeUnsupportedFailure = new(
        "W4.TargetException.ExecutionModeUnsupported",
        "Only the legacy single-frame execution envelope can be projected.");

    private static readonly EvaluationDiagnostic AccountingInvalidFailure = new(
        "W4.TargetException.AccountingInvalid",
        "Instruction accounting does not describe one complete exact terminal execution.");

    private static readonly EvaluationDiagnostic EventTraceInvalidFailure = new(
        "W4.TargetException.EventTraceInvalid",
        "The certified event trace does not describe one bounded target-exception execution.");

    private static readonly EvaluationDiagnostic NullReferenceDiagnostic = new(
        "W4.TargetException.NullReference",
        "The admitted field load terminated with an exact null-reference target exception.");

    /// <summary>
    /// Projects the complete certified transition sequence for one exact legacy null-reference execution.
    /// </summary>
    /// <typeparam name="TValue">The machine value representation, which is not exposed by the fragment.</typeparam>
    /// <typeparam name="TMemory">The persistent memory representation preserved by every admitted transition.</typeparam>
    /// <param name="machine">
    /// The exact legacy machine instance that issued every transition in <paramref name="completeTransitions"/>.
    /// </param>
    /// <param name="initialState">The exact single-root semantic activation state before instruction zero.</param>
    /// <param name="initialOperationalState">
    /// The exact legacy operational envelope, including the initial instruction allowance.
    /// </param>
    /// <param name="completeTransitions">
    /// Every exact <see cref="IlMachine{TValue,TMemory}.StepOne"/> outcome from activation through the first target
    /// exception, optionally followed by exactly one certified idempotent re-step of the terminal latch.
    /// </param>
    /// <returns>
    /// A successful canonical fragment only when the complete issued sequence, latch, accounting, legacy mode, and
    /// event trace are mutually consistent; otherwise one stable payload-omitting diagnostic and no fragment.
    /// </returns>
    public static CounterfactualTargetOutcomeProjectionResult Project<TValue, TMemory>(
        IlMachine<TValue, TMemory> machine,
        MachineState<TValue, TMemory> initialState,
        MachineOperationalState initialOperationalState,
        ImmutableArray<StepOutcome<TValue, TMemory>> completeTransitions)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        if (machine is null ||
            initialState is null ||
            initialOperationalState is null ||
            completeTransitions.IsDefaultOrEmpty)
        {
            return Failed(OutcomeRequiredFailure);
        }

        if (completeTransitions.Length > MaximumCompleteTransitionCount)
        {
            return Failed(AccountingInvalidFailure);
        }

        var activationFailure = ValidateActivation(initialState, initialOperationalState, out var rootMethod);
        if (activationFailure is not null)
        {
            return Failed(activationFailure);
        }

        var initialInstructionUnits = initialOperationalState.Budget.InstructionBudget;
        if (initialInstructionUnits <= 0)
        {
            return Failed(AccountingInvalidFailure);
        }

        var currentState = initialState;
        var currentOperationalState = initialOperationalState;
        var events = ImmutableArray.CreateBuilder<DebugEvent>(
            Math.Min(completeTransitions.Length, MaximumConsumingTransitionCount));
        StepOutcome<TValue, TMemory>? firstTerminalOutcome = null;
        TargetExceptionInfo? targetException = null;

        for (var index = 0; index < completeTransitions.Length; index++)
        {
            var outcome = completeTransitions[index];
            if (outcome is null || outcome.State is null || outcome.OperationalState is null)
            {
                return Failed(OutcomeRequiredFailure);
            }

            if (targetException is not null)
            {
                if (index != completeTransitions.Length - 1)
                {
                    return Failed(LatchInvalidFailure);
                }

                var unionFailure = ValidateTargetOutcomeUnion(outcome, out var repeatedException);
                if (unionFailure is not null)
                {
                    return Failed(unionFailure);
                }

                var repeatedFailure = ValidateRepeatedStep(
                    currentState,
                    currentOperationalState,
                    outcome,
                    repeatedException!);
                if (repeatedFailure is not null)
                {
                    return Failed(repeatedFailure);
                }

                if (!outcome.IsMachineIssuedTransitionFrom(
                    machine,
                    currentState,
                    currentOperationalState))
                {
                    return Failed(OutcomeRequiredFailure);
                }

                currentState = outcome.State;
                currentOperationalState = outcome.OperationalState;
                continue;
            }

            if (outcome.Status == MachineRunStatus.Ready)
            {
                var readyFailure = ValidateReadyTransition(
                    currentState,
                    currentOperationalState,
                    outcome,
                    rootMethod);
                if (readyFailure is not null)
                {
                    return Failed(readyFailure);
                }

                if (!outcome.IsMachineIssuedTransitionFrom(
                    machine,
                    currentState,
                    currentOperationalState))
                {
                    return Failed(OutcomeRequiredFailure);
                }

                events.Add(outcome.Events[0]);
                currentState = outcome.State;
                currentOperationalState = outcome.OperationalState;
                continue;
            }

            var targetUnionFailure = ValidateTargetOutcomeUnion(outcome, out targetException);
            if (targetUnionFailure is not null)
            {
                return Failed(targetUnionFailure);
            }

            var firstTransitionFailure = ValidateFirstTransition(
                currentState,
                currentOperationalState,
                outcome,
                targetException!);
            if (firstTransitionFailure is not null)
            {
                return Failed(firstTransitionFailure);
            }

            if (!outcome.IsMachineIssuedTransitionFrom(
                machine,
                currentState,
                currentOperationalState))
            {
                return Failed(OutcomeRequiredFailure);
            }

            if (index < completeTransitions.Length - 2)
            {
                return Failed(LatchInvalidFailure);
            }

            events.Add(outcome.Events[0]);
            firstTerminalOutcome = outcome;
            currentState = outcome.State;
            currentOperationalState = outcome.OperationalState;
        }

        if (firstTerminalOutcome is null || targetException is null)
        {
            return Failed(KindUnsupportedFailure);
        }

        var remainingInstructionUnits = firstTerminalOutcome.OperationalState.Budget.InstructionBudget;
        if (remainingInstructionUnits < 0 || remainingInstructionUnits >= initialInstructionUnits)
        {
            return Failed(AccountingInvalidFailure);
        }

        var usedInstructionUnits = initialInstructionUnits - remainingInstructionUnits;
        if (usedInstructionUnits is <= 0 or > MaximumConsumingTransitionCount ||
            events.Count != usedInstructionUnits)
        {
            return Failed(AccountingInvalidFailure);
        }

        var completeEvents = events.ToImmutable();
        if (!IsValidCompleteTrace(
            completeEvents,
            rootMethod,
            targetException.Method!.Value,
            targetException.IlOffset!.Value))
        {
            return Failed(EventTraceInvalidFailure);
        }

        var fragment = new CounterfactualTargetOutcomeFragment(
            targetException,
            ImmutableArray.Create(rootMethod),
            initialInstructionUnits,
            usedInstructionUnits,
            remainingInstructionUnits,
            completeEvents,
            ImmutableArray.Create(NullReferenceDiagnostic));
        return CounterfactualTargetOutcomeProjectionResult.Succeeded(fragment);
    }

    private static EvaluationDiagnostic? ValidateActivation<TValue, TMemory>(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        out MethodHandle rootMethod)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        rootMethod = default;
        if (state.CallStack.IsDefault ||
            state.CallStack.Length != 1 ||
            state.CallStack[0] is not { } rootFrame ||
            state.TerminalTargetException is not null ||
            state.ReturnValue.HasValue ||
            state.Memory is null ||
            rootFrame.ReturnSite is not null ||
            rootFrame.Arguments.IsDefault ||
            rootFrame.Locals.IsDefault ||
            rootFrame.EvalStack.IsDefault)
        {
            return LatchInvalidFailure;
        }

        if (rootFrame.Method.Module == default ||
            !MethodHandle.IsValidMetadataToken(rootFrame.Method.MetadataToken) ||
            rootFrame.IlOffset != 0)
        {
            return LocationInvalidFailure;
        }

        if (!IsLegacyEnvelope(operationalState))
        {
            return ExecutionModeUnsupportedFailure;
        }

        rootMethod = rootFrame.Method;
        return null;
    }

    private static EvaluationDiagnostic? ValidateReadyTransition<TValue, TMemory>(
        MachineState<TValue, TMemory> priorState,
        MachineOperationalState priorOperationalState,
        StepOutcome<TValue, TMemory> outcome,
        MethodHandle rootMethod)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        if (outcome.Failure is not null || outcome.TargetException is not null)
        {
            return KindUnsupportedFailure;
        }

        if (priorState.CallStack.IsDefault ||
            priorState.CallStack.Length != 1 ||
            priorState.CallStack[0] is not { } priorFrame ||
            outcome.State.CallStack.IsDefault ||
            outcome.State.CallStack.Length != 1 ||
            outcome.State.CallStack[0] is not { } nextFrame ||
            priorState.TerminalTargetException is not null ||
            outcome.State.TerminalTargetException is not null ||
            priorState.ReturnValue.HasValue ||
            outcome.State.ReturnValue.HasValue ||
            priorFrame.ReturnSite is not null ||
            nextFrame.ReturnSite is not null ||
            priorFrame.Arguments.IsDefault ||
            priorFrame.Locals.IsDefault ||
            priorFrame.EvalStack.IsDefault ||
            nextFrame.Arguments.IsDefault ||
            nextFrame.Locals.IsDefault ||
            nextFrame.EvalStack.IsDefault ||
            priorState.Memory is null ||
            outcome.State.Memory is null ||
            !HasSameMemoryIdentity(priorState.Memory, outcome.State.Memory))
        {
            return LatchInvalidFailure;
        }

        if (priorFrame.Method != rootMethod ||
            nextFrame.Method != rootMethod ||
            priorFrame.IlOffset < 0 ||
            nextFrame.IlOffset <= priorFrame.IlOffset)
        {
            return LocationInvalidFailure;
        }

        if (!IsLegacyEnvelope(priorOperationalState) ||
            !IsLegacyEnvelope(outcome.OperationalState))
        {
            return ExecutionModeUnsupportedFailure;
        }

        var priorBudget = priorOperationalState.Budget.InstructionBudget;
        var nextBudget = outcome.OperationalState.Budget.InstructionBudget;
        if (priorBudget <= 0 || nextBudget != priorBudget - 1)
        {
            return AccountingInvalidFailure;
        }

        if (outcome.Events.IsDefault ||
            outcome.Events.Length != 1 ||
            !IsMatchingOrdinaryEvent(outcome.Events[0], rootMethod, priorFrame.IlOffset))
        {
            return EventTraceInvalidFailure;
        }

        return null;
    }

    private static EvaluationDiagnostic? ValidateTargetOutcomeUnion<TValue, TMemory>(
        StepOutcome<TValue, TMemory> outcome,
        out TargetExceptionInfo? targetException)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        targetException = null;
        if (outcome.Status != MachineRunStatus.TargetException || outcome.Failure is not null)
        {
            return KindUnsupportedFailure;
        }

        targetException = outcome.TargetException;
        if (targetException is null || outcome.State.TerminalTargetException is null)
        {
            return OutcomeRequiredFailure;
        }

        if (targetException.Kind != TargetExceptionKind.NullReference ||
            !string.Equals(targetException.Code, NullReferenceCode, StringComparison.Ordinal))
        {
            return KindUnsupportedFailure;
        }

        if (targetException.Method is not { } method ||
            method.Module == default ||
            !MethodHandle.IsValidMetadataToken(method.MetadataToken) ||
            targetException.IlOffset is not { } ilOffset ||
            ilOffset < 0)
        {
            return LocationInvalidFailure;
        }

        if (outcome.State.TerminalTargetException != targetException ||
            outcome.State.CallStack.IsDefault ||
            outcome.State.CallStack.Length != 0 ||
            outcome.State.ReturnValue.HasValue)
        {
            return LatchInvalidFailure;
        }

        return null;
    }

    private static EvaluationDiagnostic? ValidateFirstTransition<TValue, TMemory>(
        MachineState<TValue, TMemory> priorState,
        MachineOperationalState priorOperationalState,
        StepOutcome<TValue, TMemory> terminalOutcome,
        TargetExceptionInfo targetException)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        if (priorState.CallStack.IsDefault ||
            priorState.CallStack.Length != 1 ||
            priorState.CallStack[0] is not { } rootFrame ||
            priorState.TerminalTargetException is not null ||
            priorState.ReturnValue.HasValue ||
            rootFrame.ReturnSite is not null)
        {
            return LatchInvalidFailure;
        }

        if (rootFrame.Method != targetException.Method || rootFrame.IlOffset != targetException.IlOffset)
        {
            return LocationInvalidFailure;
        }

        if (rootFrame.Arguments.IsDefault ||
            rootFrame.Locals.IsDefault ||
            rootFrame.EvalStack.IsDefault ||
            priorState.Memory is null ||
            terminalOutcome.State.Memory is null ||
            !HasSameMemoryIdentity(priorState.Memory, terminalOutcome.State.Memory))
        {
            return LatchInvalidFailure;
        }

        if (!IsLegacyEnvelope(priorOperationalState) ||
            !IsLegacyEnvelope(terminalOutcome.OperationalState))
        {
            return ExecutionModeUnsupportedFailure;
        }

        var priorBudget = priorOperationalState.Budget.InstructionBudget;
        var terminalBudget = terminalOutcome.OperationalState.Budget.InstructionBudget;
        if (priorBudget <= 0 || terminalBudget != priorBudget - 1)
        {
            return AccountingInvalidFailure;
        }

        if (terminalOutcome.Events.IsDefault ||
            terminalOutcome.Events.Length != 1 ||
            !IsMatchingTerminalEvent(
                terminalOutcome.Events[0],
                targetException.Method!.Value,
                targetException.IlOffset!.Value))
        {
            return EventTraceInvalidFailure;
        }

        return null;
    }

    private static EvaluationDiagnostic? ValidateRepeatedStep<TValue, TMemory>(
        MachineState<TValue, TMemory> priorState,
        MachineOperationalState priorOperationalState,
        StepOutcome<TValue, TMemory> terminalOutcome,
        TargetExceptionInfo targetException)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        if (priorState.CallStack.IsDefault ||
            priorState.CallStack.Length != 0 ||
            priorState.ReturnValue.HasValue ||
            priorState.Memory is null ||
            priorState.TerminalTargetException != targetException ||
            !ReferenceEquals(priorState, terminalOutcome.State) ||
            !ReferenceEquals(priorOperationalState, terminalOutcome.OperationalState))
        {
            return LatchInvalidFailure;
        }

        if (!IsLegacyEnvelope(priorOperationalState) ||
            !IsLegacyEnvelope(terminalOutcome.OperationalState))
        {
            return ExecutionModeUnsupportedFailure;
        }

        if (terminalOutcome.Events.IsDefault || terminalOutcome.Events.Length != 0)
        {
            return EventTraceInvalidFailure;
        }

        return null;
    }

    private static bool IsLegacyEnvelope(MachineOperationalState state) =>
        state.Budget is not null &&
        state.ConfiguredMaximumLogicalCallDepth is null &&
        state.RequiredLogicalCallDepth is null &&
        state.ObservedLogicalDepthHighWater == 1 &&
        state.ActiveFrameDepthHighWater == 1 &&
        !state.ModelAttempts.IsDefault &&
        state.ModelAttempts.Length == 0 &&
        state.ModelInvocationCount == 0 &&
        state.CompletedModeledCallCount == 0;

    private static bool IsValidCompleteTrace(
        ImmutableArray<DebugEvent> events,
        MethodHandle rootMethod,
        MethodHandle targetMethod,
        int targetIlOffset)
    {
        if (rootMethod != targetMethod ||
            events.Length is < 1 or > MaximumConsumingTransitionCount)
        {
            return false;
        }

        var priorOffset = -1;
        for (var index = 0; index < events.Length; index++)
        {
            var item = events[index];
            if (item is null ||
                item.Method != rootMethod ||
                item.IlOffset <= priorOffset ||
                item.FieldEvidence is not null)
            {
                return false;
            }

            if (index == 0 && item.IlOffset != 0)
            {
                return false;
            }

            if (index == events.Length - 1)
            {
                return IsMatchingTerminalEvent(item, targetMethod, targetIlOffset);
            }

            if (!IsMatchingOrdinaryEvent(item, rootMethod, item.IlOffset) ||
                item.IlOffset >= targetIlOffset)
            {
                return false;
            }

            priorOffset = item.IlOffset;
        }

        return false;
    }

    private static bool IsMatchingOrdinaryEvent(DebugEvent? item, MethodHandle method, int ilOffset) =>
        item is not null &&
        item.Kind == DebugEventKind.InstructionExecuted &&
        item.Method == method &&
        item.IlOffset == ilOffset &&
        IsStableInstructionName(item.Instruction) &&
        item.FieldEvidence is null;

    private static bool IsMatchingTerminalEvent(DebugEvent? item, MethodHandle method, int ilOffset) =>
        item is not null &&
        item.Kind == DebugEventKind.TargetExceptionRaised &&
        item.Method == method &&
        item.IlOffset == ilOffset &&
        string.Equals(item.Instruction, "LoadField", StringComparison.Ordinal) &&
        item.FieldEvidence is null;

    private static bool IsStableInstructionName(string? instruction) => instruction is
        "Nop" or
        "LoadArgument" or
        "LoadLocal" or
        "StoreLocal" or
        "LoadInt32" or
        "Add" or
        "Subtract" or
        "Multiply" or
        "LoadField";

    private static bool HasSameMemoryIdentity<TMemory>(TMemory left, TMemory right) =>
        typeof(TMemory).IsValueType
            ? EqualityComparer<TMemory>.Default.Equals(left, right)
            : ReferenceEquals(left, right);

    private static CounterfactualTargetOutcomeProjectionResult Failed(EvaluationDiagnostic failure) =>
        CounterfactualTargetOutcomeProjectionResult.Failed(failure);
}
