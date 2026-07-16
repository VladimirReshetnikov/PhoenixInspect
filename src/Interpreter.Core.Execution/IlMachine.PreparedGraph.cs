using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

public sealed partial class IlMachine<TValue, TMemory>
{
    private FrozenMethodGraphPlan? _preparedGraph;
    private int _preparedMaximumLogicalCallDepth;

    /// <summary>
    /// Activates the root of one already admitted direct-call graph without resolving or decoding any dependency.
    /// </summary>
    /// <param name="graph">
    /// The complete immutable graph produced by <see cref="MethodGraphPlanner"/>. The first successful activation
    /// binds this machine instance to the graph for the remainder of its bounded session.
    /// </param>
    /// <param name="maximumLogicalCallDepth">
    /// The configured maximum rooted call depth, counting the root at depth one. It must cover the graph's prepared
    /// <see cref="FrozenMethodGraphPlan.RequiredLogicalDepth"/> fact before activation may create a frame.
    /// </param>
    /// <param name="arguments">
    /// Ordered root domain values, including an implicit receiver at slot zero when required by metadata.
    /// </param>
    /// <param name="memory">The immutable initial persistent-memory snapshot.</param>
    /// <returns>
    /// A ready single-root state; a deterministic pre-activation depth exhaustion; or a structured invalid/blocked
    /// result. This operation consumes no instruction budget, emits no event, and never invokes the resolver, registry,
    /// target body, or selected model. Opaque modeled leaves remain dormant until their exact call edge executes.
    /// </returns>
    /// <remarks>
    /// This opt-in W4.5/W4.6 prototype path is mutually exclusive with legacy <see cref="ActivateRoot"/> execution on
    /// the same machine. Interpreted edges push frames; pure-model edges invoke only the capability frozen into their
    /// opaque leaf and publish a typed result in the caller without a frame. Public shape and policy remain draft-phase
    /// contracts subject to later product-facade refinement.
    /// </remarks>
    public MachineActivationResult<TValue, TMemory> ActivatePreparedGraph(
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth,
        ImmutableArray<TValue> arguments,
        TMemory memory)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (maximumLogicalCallDepth <= 0)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_CALL_DEPTH_LIMIT_INVALID",
                "Maximum logical call depth must count at least the root frame.",
                graph.Root,
                0);
        }

        if (arguments.IsDefault)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidSlot,
                "EXEC_INVALID_ARGUMENT_VECTOR",
                "Prepared-graph activation received an uninitialized argument array.",
                graph.Root,
                0);
        }

        if (memory is null)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.MemoryFailure,
                "EXEC_INVALID_MEMORY_STATE",
                "Prepared-graph activation requires a non-null persistent-memory snapshot.",
                graph.Root,
                0);
        }

        if (arguments.Length > MaximumFrameSlotCount)
        {
            return ActivationFailed(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_FRAME_SLOT_LIMIT",
                $"Argument vectors are limited to {MaximumFrameSlotCount} values.",
                graph.Root,
                0);
        }

        if (graph.RequiredLogicalDepth > maximumLogicalCallDepth)
        {
            return ActivationFailed(
                MachineRunStatus.BudgetExhausted,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_CALL_DEPTH_EXHAUSTED",
                "The configured maximum logical call depth is smaller than the complete prepared graph requires.",
                graph.Root,
                0);
        }

        if (!graph.TryGetAdmittedMethodPlan(graph.Root, out var plan) || plan is null)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_CALL_PLAN_INVALID",
                "The prepared graph does not retain an admitted runtime plan for its root MethodDef.",
                graph.Root,
                0);
        }

        if (arguments.Length != plan.ArgumentTypes.Length)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidSlot,
                "EXEC_ARGUMENT_SHAPE_MISMATCH",
                $"Metadata requires {plan.ArgumentTypes.Length} argument value(s), but activation supplied {arguments.Length}.",
                graph.Root,
                0);
        }

        var compatibilityFailure = CheckPreparedGraphCompatibility(graph, maximumLogicalCallDepth);
        if (compatibilityFailure is not null)
        {
            return new MachineActivationResult<TValue, TMemory>(
                null,
                MachineRunStatus.Blocked,
                compatibilityFailure);
        }

        try
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                var failure = ValidateValue(
                    arguments[index],
                    plan.ArgumentTypes[index],
                    "argument",
                    index,
                    graph.Root,
                    0,
                    plan.Definition.Signature.HasImplicitThis && index == 0
                        ? ValuePrecisionRequirement.Exact
                        : ValuePrecisionRequirement.Executable);
                if (failure is not null)
                {
                    return new MachineActivationResult<TValue, TMemory>(
                        null,
                        MachineRunStatus.InvalidProgram,
                        failure);
                }
            }

            var localsResult = CreateInitializedLocals(plan, graph.Root, 0);
            if (localsResult.Failure is not null)
            {
                return new MachineActivationResult<TValue, TMemory>(
                    null,
                    MachineRunStatus.InvalidProgram,
                    localsResult.Failure);
            }

            var bindingFailure = BindPreparedGraph(graph, maximumLogicalCallDepth);
            if (bindingFailure is not null)
            {
                return new MachineActivationResult<TValue, TMemory>(
                    null,
                    MachineRunStatus.Blocked,
                    bindingFailure);
            }

            var frame = new FrameState<TValue>(
                graph.Root,
                0,
                arguments,
                localsResult.Locals,
                ImmutableArray<TValue>.Empty);
            return new MachineActivationResult<TValue, TMemory>(
                MachineState<TValue, TMemory>.Create(frame, memory),
                MachineRunStatus.Ready,
                null);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return ActivationFailed(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DomainFailure,
                "EXEC_DOMAIN_ACTIVATION_FAILURE",
                "The value domain capability failed during metadata-derived prepared-graph activation.",
                graph.Root,
                0);
        }
    }

    /// <summary>
    /// Creates the replayable operational envelope for the prepared graph already bound to this machine.
    /// </summary>
    /// <param name="budget">The caller-selected immutable instruction budget, retained without consumption.</param>
    /// <returns>
    /// A fresh envelope containing the exact configured/required logical-depth pair, root-level high-water facts,
    /// and <paramref name="budget"/> unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="budget"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No prepared graph has been activated on this machine.</exception>
    /// <remarks>
    /// This draft factory prevents product and test callers from reconstructing session policy out of band. The
    /// returned value remains immutable and every step revalidates it against the bound graph.
    /// </remarks>
    public MachineOperationalState CreatePreparedOperationalState(BudgetState budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        lock (_sessionGate)
        {
            if (_preparedGraph is null)
            {
                throw new InvalidOperationException(
                    "A prepared operational state requires a successfully bound prepared-graph session.");
            }

            return new MachineOperationalState(budget)
            {
                ModelAttempts = ImmutableArray<PureModelAttempt>.Empty,
                ModelInvocationCount = 0,
                CompletedModeledCallCount = 0,
                ConfiguredMaximumLogicalCallDepth = _preparedMaximumLogicalCallDepth,
                RequiredLogicalCallDepth = _preparedGraph.RequiredLogicalDepth,
                ObservedLogicalDepthHighWater = 1,
                ActiveFrameDepthHighWater = 1,
            };
        }
    }

    private ExecutionFailure? CheckPreparedGraphCompatibility(
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth)
    {
        lock (_sessionGate)
        {
            return CheckPreparedGraphCompatibilityUnderLock(graph, maximumLogicalCallDepth);
        }
    }

    private ExecutionFailure? CheckPreparedGraphCompatibilityUnderLock(
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth) =>
        _sessionMethod is not null ||
        _preparedGraph is not null &&
        (!_preparedGraph.Equals(graph) ||
         _preparedMaximumLogicalCallDepth != maximumLogicalCallDepth)
            ? new ExecutionFailure(
                ExecutionFailureKind.ResourceLimit,
                "EXEC_MACHINE_SESSION_MISMATCH",
                "One bounded machine cannot mix execution modes, prepared graphs, or logical-depth configurations.",
                graph.Root,
                0)
            : null;

    private ExecutionFailure? BindPreparedGraph(
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth)
    {
        lock (_sessionGate)
        {
            var compatibilityFailure = CheckPreparedGraphCompatibilityUnderLock(graph, maximumLogicalCallDepth);
            if (compatibilityFailure is not null)
            {
                return compatibilityFailure;
            }

            _preparedGraph ??= graph;
            _preparedMaximumLogicalCallDepth = maximumLogicalCallDepth;
            return null;
        }
    }

    private bool TryGetPreparedGraphSession(
        out FrozenMethodGraphPlan? graph,
        out int maximumLogicalCallDepth)
    {
        lock (_sessionGate)
        {
            graph = _preparedGraph;
            maximumLogicalCallDepth = _preparedMaximumLogicalCallDepth;
            return graph is not null;
        }
    }

    private StepOutcome<TValue, TMemory> StepPreparedGraph(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth)
    {
        if (operationalState.Budget is null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_OPERATIONAL_STATE",
                    "Prepared operational state contains no instruction-budget snapshot.",
                    graph.Root,
                    0));
        }

        if (state.TerminalTargetException is not null ||
            !state.CallStack.IsDefault && state.CallStack.Length == 0)
        {
            return CompletePreparedTerminalStep(
                state,
                operationalState,
                graph,
                maximumLogicalCallDepth);
        }

        var activeFrame = state.CallStack.IsDefaultOrEmpty ? null : state.CallStack[^1];
        var failureMethod = activeFrame?.Method ?? graph.Root;
        var failureOffset = activeFrame?.IlOffset ?? 0;
        var updatedBudget = operationalState.Budget;
        try
        {
            if (!_budgetPolicy.TryConsumeInstruction(ref updatedBudget, 1))
            {
                return new StepOutcome<TValue, TMemory>(
                    state,
                    operationalState,
                    MachineRunStatus.BudgetExhausted,
                    ImmutableArray<DebugEvent>.Empty);
            }
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_BUDGET_POLICY_FAILURE",
                    "The instruction-budget capability failed during prepared execution bookkeeping.",
                    failureMethod,
                    failureOffset));
        }

        var envelopeFailure = ValidateStateEnvelope(state, operationalState);
        if (envelopeFailure is not null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                envelopeFailure);
        }

        PreparedGraphStepContext context;
        try
        {
            var invariantFailure = ValidatePreparedGraphState(
                state,
                operationalState,
                graph,
                maximumLogicalCallDepth,
                out context);
            if (invariantFailure is not null)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    invariantFailure);
            }
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_DOMAIN_FAILURE",
                    "The value-domain capability failed during prepared-graph frame validation.",
                    failureMethod,
                    failureOffset));
        }

        try
        {
            if (context.Instruction.Kind == AdmittedInstructionKind.Call)
            {
                if (!TryGetFrozenCallSite(graph, context, out var frozenCallSite) || frozenCallSite is null)
                {
                    return Failed(
                        state,
                        operationalState,
                        MachineRunStatus.InvalidProgram,
                        PlanInvalid(
                            context.Frame.Method,
                            context.Frame.IlOffset,
                            "The admitted call instruction has no unique correlated frozen call edge."));
                }

                if (frozenCallSite.Disposition == FrozenMethodCallDisposition.PureModel &&
                    operationalState.ModelAttempts.LastOrDefault() is { TransferCompleted: false } priorFailure &&
                    priorFailure.CallSite == new DirectCallSiteIdentity(
                        frozenCallSite.Caller,
                        frozenCallSite.IlOffset,
                        frozenCallSite.Target.Method))
                {
                    return Failed(
                        state,
                        operationalState,
                        MachineRunStatus.InvalidProgram,
                        ModelAttemptInvariantFailure(
                            context.Frame.Method,
                            context.Frame.IlOffset,
                            "A nontransferring modeled-call attempt latches its exact current boundary and cannot be followed by another attempt."));
                }

                return frozenCallSite.Disposition switch
                {
                    FrozenMethodCallDisposition.Interpreted => ExecutePreparedCall(
                        state,
                        operationalState,
                        graph,
                        maximumLogicalCallDepth,
                        context,
                        frozenCallSite,
                        updatedBudget),
                    FrozenMethodCallDisposition.PureModel => ExecutePreparedModeledCall(
                        state,
                        operationalState,
                        graph,
                        maximumLogicalCallDepth,
                        context,
                        frozenCallSite,
                        updatedBudget),
                    _ => Failed(
                        state,
                        operationalState,
                        MachineRunStatus.InvalidProgram,
                        PlanInvalid(
                            context.Frame.Method,
                            context.Frame.IlOffset,
                            "The frozen call edge has an undefined execution disposition.")),
                };
            }

            if (context.Instruction.Kind == AdmittedInstructionKind.Return && state.CallStack.Length > 1)
            {
                return ExecutePreparedNestedReturn(
                    state,
                    operationalState,
                    graph,
                    context,
                    updatedBudget);
            }

            var outcome = Execute(
                state,
                operationalState,
                context.Frame,
                context.Plan,
                context.Instruction,
                updatedBudget,
                MachineRunStatus.Blocked);
            return outcome;
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_DOMAIN_FAILURE",
                    "The value-domain capability failed during the admitted prepared-graph transfer.",
                    context.Frame.Method,
                    context.Frame.IlOffset));
        }
    }

    private StepOutcome<TValue, TMemory> CompletePreparedTerminalStep(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth)
    {
        var envelopeFailure = ValidateStateEnvelope(state, operationalState);
        if (envelopeFailure is not null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                envelopeFailure);
        }

        var depthFailure = ValidatePreparedDepthEnvelope(
            state,
            operationalState,
            graph,
            maximumLogicalCallDepth,
            graph.Root,
            0);
        if (depthFailure is not null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                depthFailure);
        }

        if (state.TerminalTargetException is { } targetException)
        {
            var stampedMethod = targetException.Method;
            var stampedOffset = targetException.IlOffset;
            if (stampedMethod is not { } method ||
                stampedOffset is not { } offset ||
                method != graph.Root ||
                !graph.TryGetAdmittedMethodPlan(method, out var targetPlan) ||
                targetPlan is null ||
                !targetPlan.TryGetInstruction(offset, out var targetInstruction) ||
                targetInstruction.Kind != AdmittedInstructionKind.LoadField ||
                targetException.Kind != TargetExceptionKind.NullReference)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    new ExecutionFailure(
                        ExecutionFailureKind.InvalidInstruction,
                        "EXEC_INVALID_TARGET_TERMINATION",
                        "A prepared target-exception latch does not identify a null-receiver field load in the bound graph.",
                        stampedMethod ?? graph.Root,
                        stampedOffset ?? 0));
            }

            var targetDepths = CalculateRootTargetWitnessedDepths(graph, offset);
            if (targetDepths is null ||
                operationalState.ObservedLogicalDepthHighWater != targetDepths.Value.Logical ||
                operationalState.ActiveFrameDepthHighWater != targetDepths.Value.Active)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    new ExecutionFailure(
                        ExecutionFailureKind.InvalidInstruction,
                        "EXEC_CALL_DEPTH_INVARIANT",
                        "A prepared target-exception latch does not retain the exact logical and active rooted prefix depths needed to reach its field load.",
                        method,
                        offset));
            }

            return new StepOutcome<TValue, TMemory>(
                state,
                operationalState,
                MachineRunStatus.TargetException,
                ImmutableArray<DebugEvent>.Empty,
                Failure: null,
                TargetException: targetException);
        }

        if (!graph.TryGetAdmittedMethodPlan(graph.Root, out var rootPlan) || rootPlan is null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                PlanInvalid(graph.Root, 0, "The completed prepared state has no retained root method plan."));
        }

        var requiredActiveFrameDepth = CalculateRelativeActiveFrameDepth(
            graph,
            graph.Root,
            new Dictionary<MethodHandle, int>());
        if (operationalState.ObservedLogicalDepthHighWater != graph.RequiredLogicalDepth ||
            operationalState.ActiveFrameDepthHighWater != requiredActiveFrameDepth)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_CALL_DEPTH_INVARIANT",
                    "Completed branchless graph execution does not retain the exact logical-boundary and interpreted-frame high-water marks required by every frozen call edge.",
                    graph.Root,
                    rootPlan.Definition.Body.CodeBytes.Length));
        }

        if (!state.ReturnValue.HasValue || rootPlan.Definition.Signature.ReturnType == TypeSig.Void)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidStack,
                    "EXEC_CALL_TERMINAL_RESULT_INVALID",
                    "Completed prepared execution requires the root's typed executable return value.",
                    graph.Root,
                    rootPlan.Definition.Body.CodeBytes.Length));
        }

        try
        {
            var valueFailure = ValidateValue(
                state.ReturnValue.Value,
                rootPlan.Definition.Signature.ReturnType,
                "terminal return",
                0,
                graph.Root,
                rootPlan.Definition.Body.CodeBytes.Length,
                ValuePrecisionRequirement.Executable);
            if (valueFailure is not null)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    valueFailure);
            }
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_DOMAIN_FAILURE",
                    "The value-domain capability failed while validating the prepared terminal result.",
                    graph.Root,
                    rootPlan.Definition.Body.CodeBytes.Length));
        }

        return new StepOutcome<TValue, TMemory>(
            state,
            operationalState,
            MachineRunStatus.Completed,
            ImmutableArray<DebugEvent>.Empty);
    }

    private static ExecutionFailure? ValidatePreparedDepthEnvelope(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth,
        MethodHandle locationMethod,
        int locationOffset)
    {
        var currentDepth = state.CallStack.IsDefault ? 0 : state.CallStack.Length;
        if (operationalState.ModelAttempts.IsDefault ||
            operationalState.ModelInvocationCount < 0 ||
            operationalState.CompletedModeledCallCount < 0 ||
            operationalState.CompletedModeledCallCount > operationalState.ModelInvocationCount ||
            operationalState.ModelAttempts.Length != operationalState.ModelInvocationCount ||
            operationalState.ModelAttempts.Count(attempt => attempt is not null && attempt.TransferCompleted) !=
                operationalState.CompletedModeledCallCount)
        {
            return ModelAttemptInvariantFailure(
                locationMethod,
                locationOffset,
                "Modeled-call attempt storage and counters are not initialized or internally consistent.");
        }

        var maximumAttemptedDepth = 1;
        foreach (var attempt in operationalState.ModelAttempts)
        {
            if (attempt is null || !ValidateModelAttemptAgainstGraph(graph, attempt))
            {
                return ModelAttemptInvariantFailure(
                    locationMethod,
                    locationOffset,
                    "A modeled-call attempt disagrees with every frozen pure-model edge or rooted depth.");
            }

            maximumAttemptedDepth = Math.Max(maximumAttemptedDepth, attempt.EnteredLogicalDepth);
        }

        if (operationalState.ObservedLogicalDepthHighWater < maximumAttemptedDepth)
        {
            return ModelAttemptInvariantFailure(
                locationMethod,
                locationOffset,
                "Modeled-call attempt depth is not covered by the retained logical-depth high water.");
        }

        if ((!operationalState.ModelAttempts.IsEmpty || !graph.ModeledLeaves.IsEmpty) &&
            !ValidateModelAttemptChronology(state, graph, operationalState.ModelAttempts))
        {
            return ModelAttemptInvariantFailure(
                locationMethod,
                locationOffset,
                "Modeled-call attempts are not the exact chronological prefix represented by machine state.");
        }

        var witnessedDepths = state.CallStack.IsDefault
            ? default
            : CalculateMinimumWitnessedDepths(state, graph);
        var minimumObservedLogicalDepth = Math.Max(
            maximumAttemptedDepth,
            Math.Max(1, Math.Max(currentDepth, witnessedDepths.Logical)));
        var minimumObservedActiveDepth = Math.Max(
            1,
            Math.Max(currentDepth, witnessedDepths.Active));
        if (maximumLogicalCallDepth < graph.RequiredLogicalDepth ||
            operationalState.ConfiguredMaximumLogicalCallDepth != maximumLogicalCallDepth ||
            operationalState.RequiredLogicalCallDepth != graph.RequiredLogicalDepth ||
            currentDepth > maximumLogicalCallDepth ||
            currentDepth > graph.RequiredLogicalDepth ||
            currentDepth > 0 &&
            (operationalState.ObservedLogicalDepthHighWater != minimumObservedLogicalDepth ||
             operationalState.ActiveFrameDepthHighWater != minimumObservedActiveDepth) ||
            currentDepth == 0 &&
            (operationalState.ObservedLogicalDepthHighWater < minimumObservedLogicalDepth ||
             operationalState.ActiveFrameDepthHighWater < minimumObservedActiveDepth) ||
            operationalState.ObservedLogicalDepthHighWater > graph.RequiredLogicalDepth ||
            operationalState.ActiveFrameDepthHighWater > graph.RequiredLogicalDepth ||
            operationalState.ObservedLogicalDepthHighWater > maximumLogicalCallDepth ||
            operationalState.ActiveFrameDepthHighWater > maximumLogicalCallDepth)
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_CALL_DEPTH_INVARIANT",
                "Runtime frames and configured, logical, active, required, or observed depth facts disagree with the bound prepared graph.",
                locationMethod,
                locationOffset);
        }

        return null;
    }

    private static (int Logical, int Active) CalculateMinimumWitnessedDepths(
        MachineState<TValue, TMemory> state,
        FrozenMethodGraphPlan graph)
    {
        var logicalMinimum = state.CallStack.Length;
        var activeMinimum = state.CallStack.Length;
        var relativeLogicalDepths = new Dictionary<MethodHandle, int>();
        var relativeActiveDepths = new Dictionary<MethodHandle, int>();
        for (var frameIndex = 0; frameIndex < state.CallStack.Length; frameIndex++)
        {
            var frame = state.CallStack[frameIndex];
            if (frame is null || !graph.TryGetNode(frame.Method, out var node) || node is null)
            {
                continue;
            }

            int? pendingCallOffset = frameIndex + 1 < state.CallStack.Length
                ? state.CallStack[frameIndex + 1]?.ReturnSite?.CallSite.CallIlOffset
                : null;
            foreach (var callSite in node.CallSites)
            {
                if (callSite.IlOffset >= frame.IlOffset || callSite.IlOffset == pendingCallOffset)
                {
                    continue;
                }

                logicalMinimum = Math.Max(
                    logicalMinimum,
                    checked(
                        frameIndex + 1 +
                        CalculateRelativeLogicalDepth(
                            graph,
                            callSite.Target.Method,
                            relativeLogicalDepths)));
                activeMinimum = Math.Max(
                    activeMinimum,
                    checked(
                        frameIndex + 1 +
                        CalculateRelativeActiveFrameDepth(
                            graph,
                            callSite.Target.Method,
                            relativeActiveDepths)));
            }
        }

        return (logicalMinimum, activeMinimum);
    }

    private static (int Logical, int Active)? CalculateRootTargetWitnessedDepths(
        FrozenMethodGraphPlan graph,
        int targetOffset)
    {
        if (!graph.TryGetNode(graph.Root, out var root) || root is null)
        {
            return null;
        }

        var witnessedLogicalDepth = 1;
        var witnessedActiveDepth = 1;
        var relativeLogicalDepths = new Dictionary<MethodHandle, int>();
        var relativeActiveDepths = new Dictionary<MethodHandle, int>();
        foreach (var callSite in root.CallSites)
        {
            if (callSite.IlOffset < targetOffset)
            {
                witnessedLogicalDepth = Math.Max(
                    witnessedLogicalDepth,
                    checked(1 + CalculateRelativeLogicalDepth(
                        graph,
                        callSite.Target.Method,
                        relativeLogicalDepths)));
                witnessedActiveDepth = Math.Max(
                    witnessedActiveDepth,
                    checked(1 + CalculateRelativeActiveFrameDepth(
                        graph,
                        callSite.Target.Method,
                        relativeActiveDepths)));
            }
        }

        return (witnessedLogicalDepth, witnessedActiveDepth);
    }

    private static int CalculateRelativeLogicalDepth(
        FrozenMethodGraphPlan graph,
        MethodHandle method,
        Dictionary<MethodHandle, int> cache)
    {
        if (cache.TryGetValue(method, out var cached))
        {
            return cached;
        }

        if (!graph.TryGetNode(method, out var node) || node is null)
        {
            return 1;
        }

        var childDepth = 0;
        foreach (var callSite in node.CallSites)
        {
            childDepth = Math.Max(
                childDepth,
                CalculateRelativeLogicalDepth(graph, callSite.Target.Method, cache));
        }

        var depth = checked(childDepth + 1);
        cache.Add(method, depth);
        return depth;
    }

    private static int CalculateRelativeActiveFrameDepth(
        FrozenMethodGraphPlan graph,
        MethodHandle method,
        Dictionary<MethodHandle, int> cache)
    {
        if (cache.TryGetValue(method, out var cached))
        {
            return cached;
        }

        if (!graph.TryGetNode(method, out var node) || node is null)
        {
            return 0;
        }

        var childDepth = 0;
        foreach (var callSite in node.CallSites)
        {
            childDepth = Math.Max(
                childDepth,
                CalculateRelativeActiveFrameDepth(graph, callSite.Target.Method, cache));
        }

        var depth = checked(childDepth + 1);
        cache.Add(method, depth);
        return depth;
    }

    private static bool ValidateModelAttemptAgainstGraph(
        FrozenMethodGraphPlan graph,
        PureModelAttempt attempt)
    {
        var matchedEdge = graph.CallSites.SingleOrDefault(site =>
            site.Caller == attempt.CallSite.Caller &&
            site.IlOffset == attempt.CallSite.CallIlOffset &&
            site.Target.Method == attempt.CallSite.Callee &&
            site.Disposition == FrozenMethodCallDisposition.PureModel);
        return matchedEdge is not null &&
            matchedEdge.ModelDescriptor is not null &&
            matchedEdge.ModelDescriptor.Identity == attempt.ModelIdentity &&
            graph.TryGetModeledLeaf(attempt.CallSite.Callee, out var leaf) &&
            leaf is not null &&
            leaf.Descriptor.Identity == attempt.ModelIdentity &&
            IsPossibleModeledAttemptDepth(graph, graph.Root, 1, attempt);
    }

    private static bool ValidateModelAttemptChronology(
        MachineState<TValue, TMemory> state,
        FrozenMethodGraphPlan graph,
        ImmutableArray<PureModelAttempt> attempts)
    {
        var expected = new List<ExpectedModeledCall>();
        if (!TryCollectExpectedModeledCallPrefix(state, graph, expected))
        {
            return false;
        }

        var attemptIndex = 0;
        for (var expectedIndex = 0; expectedIndex < expected.Count; expectedIndex++)
        {
            var modeledCall = expected[expectedIndex];
            if (attemptIndex >= attempts.Length)
            {
                return !modeledCall.Completed && expectedIndex == expected.Count - 1;
            }

            var attempt = attempts[attemptIndex];
            if (attempt is null || !modeledCall.Matches(attempt))
            {
                return false;
            }

            if (modeledCall.Completed)
            {
                if (!attempt.TransferCompleted)
                {
                    return false;
                }

                attemptIndex++;
                continue;
            }

            return !attempt.TransferCompleted && attemptIndex == attempts.Length - 1;
        }

        return attemptIndex == attempts.Length;
    }

    private static bool TryCollectExpectedModeledCallPrefix(
        MachineState<TValue, TMemory> state,
        FrozenMethodGraphPlan graph,
        List<ExpectedModeledCall> expected)
    {
        if (state.CallStack.IsDefault)
        {
            return false;
        }

        if (state.CallStack.Length == 0)
        {
            if (state.ReturnValue.HasValue)
            {
                return TryCollectCompleteModeledCalls(graph, graph.Root, 1, expected);
            }

            if (state.TerminalTargetException is not { IlOffset: { } targetOffset } ||
                !graph.TryGetNode(graph.Root, out var targetRoot) ||
                targetRoot is null)
            {
                return false;
            }

            foreach (var callSite in targetRoot.CallSites)
            {
                if (callSite.IlOffset >= targetOffset ||
                    !TryCollectCompletedCall(graph, callSite, 1, expected))
                {
                    return callSite.IlOffset >= targetOffset;
                }
            }

            return true;
        }

        return TryCollectActiveModeledCallPrefix(state, graph, frameIndex: 0, expected);
    }

    private static bool TryCollectActiveModeledCallPrefix(
        MachineState<TValue, TMemory> state,
        FrozenMethodGraphPlan graph,
        int frameIndex,
        List<ExpectedModeledCall> expected)
    {
        if (frameIndex < 0 || frameIndex >= state.CallStack.Length)
        {
            return false;
        }

        var frame = state.CallStack[frameIndex];
        if (frame is null ||
            !graph.TryGetNode(frame.Method, out var node) ||
            node is null)
        {
            return false;
        }

        FrozenMethodCallSite? pendingInterpretedCall = null;
        if (frameIndex + 1 < state.CallStack.Length)
        {
            var callee = state.CallStack[frameIndex + 1];
            var returnSite = callee?.ReturnSite;
            if (callee is null ||
                returnSite is null ||
                returnSite.CallSite.Caller != frame.Method ||
                returnSite.CallSite.Callee != callee.Method)
            {
                return false;
            }

            pendingInterpretedCall = node.CallSites.SingleOrDefault(callSite =>
                callSite.IlOffset == returnSite.CallSite.CallIlOffset &&
                callSite.Target.Method == callee.Method &&
                callSite.Disposition == FrozenMethodCallDisposition.Interpreted);
            if (pendingInterpretedCall is null)
            {
                return false;
            }
        }

        foreach (var callSite in node.CallSites)
        {
            if (ReferenceEquals(callSite, pendingInterpretedCall))
            {
                return TryCollectActiveModeledCallPrefix(
                    state,
                    graph,
                    checked(frameIndex + 1),
                    expected);
            }

            if (callSite.IlOffset < frame.IlOffset)
            {
                if (!TryCollectCompletedCall(graph, callSite, checked(frameIndex + 1), expected))
                {
                    return false;
                }

                continue;
            }

            if (frameIndex == state.CallStack.Length - 1 &&
                callSite.IlOffset == frame.IlOffset &&
                callSite.Disposition == FrozenMethodCallDisposition.PureModel)
            {
                return TryAddExpectedModeledCall(
                    callSite,
                    checked(frameIndex + 2),
                    completed: false,
                    expected);
            }

            return true;
        }

        return pendingInterpretedCall is null;
    }

    private static bool TryCollectCompletedCall(
        FrozenMethodGraphPlan graph,
        FrozenMethodCallSite callSite,
        int callerDepth,
        List<ExpectedModeledCall> expected) =>
        callSite.Disposition == FrozenMethodCallDisposition.PureModel
            ? TryAddExpectedModeledCall(
                callSite,
                checked(callerDepth + 1),
                completed: true,
                expected)
            : TryCollectCompleteModeledCalls(
                graph,
                callSite.Target.Method,
                checked(callerDepth + 1),
                expected);

    private static bool TryCollectCompleteModeledCalls(
        FrozenMethodGraphPlan graph,
        MethodHandle method,
        int methodDepth,
        List<ExpectedModeledCall> expected)
    {
        if (!graph.TryGetNode(method, out var node) || node is null)
        {
            return false;
        }

        foreach (var callSite in node.CallSites)
        {
            if (!TryCollectCompletedCall(graph, callSite, methodDepth, expected))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAddExpectedModeledCall(
        FrozenMethodCallSite callSite,
        int enteredLogicalDepth,
        bool completed,
        List<ExpectedModeledCall> expected)
    {
        if (callSite.Disposition != FrozenMethodCallDisposition.PureModel ||
            callSite.ModelDescriptor is null)
        {
            return false;
        }

        expected.Add(new ExpectedModeledCall(
            new DirectCallSiteIdentity(
                callSite.Caller,
                callSite.IlOffset,
                callSite.Target.Method),
            callSite.ModelDescriptor.Identity,
            enteredLogicalDepth,
            completed));
        return true;
    }

    private static bool IsPossibleModeledAttemptDepth(
        FrozenMethodGraphPlan graph,
        MethodHandle method,
        int methodDepth,
        PureModelAttempt attempt)
    {
        if (!graph.TryGetNode(method, out var node) || node is null)
        {
            return false;
        }

        foreach (var callSite in node.CallSites)
        {
            if (callSite.Disposition == FrozenMethodCallDisposition.PureModel)
            {
                if (callSite.Caller == attempt.CallSite.Caller &&
                    callSite.IlOffset == attempt.CallSite.CallIlOffset &&
                    callSite.Target.Method == attempt.CallSite.Callee &&
                    attempt.EnteredLogicalDepth == checked(methodDepth + 1))
                {
                    return true;
                }

                continue;
            }

            if (IsPossibleModeledAttemptDepth(
                graph,
                callSite.Target.Method,
                checked(methodDepth + 1),
                attempt))
            {
                return true;
            }
        }

        return false;
    }

    private static ExecutionFailure ModelAttemptInvariantFailure(
        MethodHandle method,
        int ilOffset,
        string message) =>
        new(
            ExecutionFailureKind.InvalidInstruction,
            "EXEC_MODEL_ATTEMPT_INVARIANT",
            message,
            method,
            ilOffset);

    private ExecutionFailure? ValidatePreparedGraphState(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth,
        out PreparedGraphStepContext context)
    {
        context = default;
        var activeFrame = state.CallStack[^1];
        var locationMethod = activeFrame?.Method ?? graph.Root;
        var locationOffset = activeFrame?.IlOffset ?? 0;
        var depthFailure = ValidatePreparedDepthEnvelope(
            state,
            operationalState,
            graph,
            maximumLogicalCallDepth,
            locationMethod,
            locationOffset);
        if (depthFailure is not null)
        {
            return depthFailure;
        }

        for (var frameIndex = 0; frameIndex < state.CallStack.Length; frameIndex++)
        {
            var frame = state.CallStack[frameIndex];
            if (frame is null || !MethodHandle.IsValidMetadataToken(frame.Method.MetadataToken))
            {
                return PlanInvalid(locationMethod, locationOffset, "The prepared call stack contains an invalid frame identity.");
            }

            if (!graph.TryGetAdmittedMethodPlan(frame.Method, out var plan) ||
                plan is null ||
                plan.Definition.Method != frame.Method)
            {
                return PlanInvalid(
                    frame.Method,
                    frame.IlOffset,
                    "A prepared frame identifies no retained admitted method plan.");
            }

            if (frameIndex == 0)
            {
                if (frame.Method != graph.Root)
                {
                    return PlanInvalid(
                        frame.Method,
                        frame.IlOffset,
                        "The prepared call stack does not begin with the graph's exact root MethodDef.");
                }

                if (frame.ReturnSite is not null)
                {
                    return ReturnSiteInvalid(
                        frame.Method,
                        frame.IlOffset,
                        "The prepared graph root cannot carry a caller return site.");
                }
            }
            else
            {
                var caller = state.CallStack[frameIndex - 1];
                if (caller is null || frame.ReturnSite is null)
                {
                    return ReturnSiteInvalid(
                        frame.Method,
                        frame.IlOffset,
                        "Every non-root prepared frame requires one exact immediate-caller return site.");
                }

                var returnSiteFailure = ValidateReturnSite(
                    graph,
                    caller,
                    frame,
                    frame.ReturnSite);
                if (returnSiteFailure is not null)
                {
                    return returnSiteFailure;
                }
            }

            if (frame.Arguments.IsDefault || frame.Locals.IsDefault || frame.EvalStack.IsDefault)
            {
                return PlanInvalid(frame.Method, frame.IlOffset, "A prepared frame contains an uninitialized slot vector.");
            }

            if (frame.Arguments.Length > MaximumFrameSlotCount ||
                frame.Locals.Length > MaximumFrameSlotCount ||
                frame.EvalStack.Length > MaximumFrameSlotCount)
            {
                return new ExecutionFailure(
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_FRAME_SLOT_LIMIT",
                    $"Arguments, locals, and evaluation stack are each limited to {MaximumFrameSlotCount} values.",
                    frame.Method,
                    frame.IlOffset);
            }

            if (frame.Arguments.Length != plan.ArgumentTypes.Length ||
                frame.Locals.Length != plan.Definition.Signature.LocalTypes.Length)
            {
                return PlanInvalid(
                    frame.Method,
                    frame.IlOffset,
                    "A prepared frame's method, arguments, or locals disagree with its retained admitted plan.");
            }

            var slotFailure = ValidatePreparedFrameSlots(frame, plan);
            if (slotFailure is not null)
            {
                return slotFailure;
            }

            if (frameIndex < state.CallStack.Length - 1)
            {
                var callee = state.CallStack[frameIndex + 1]!;
                if (callee.ReturnSite is null)
                {
                    return ReturnSiteInvalid(
                        frame.Method,
                        frame.IlOffset,
                        "A suspended caller has a nested frame without an exact return site.");
                }

                var suspensionFailure = ValidateSuspendedCaller(graph, frame, plan, callee.ReturnSite);
                if (suspensionFailure is not null)
                {
                    return suspensionFailure;
                }

                continue;
            }

            if (!plan.TryGetInstruction(frame.IlOffset, out var instruction) ||
                !plan.TryGetBoundary(frame.IlOffset, out var boundary))
            {
                return PlanInvalid(
                    frame.Method,
                    frame.IlOffset,
                    "The active IL offset is not a typed instruction boundary in the retained plan.");
            }

            if (frame.EvalStack.Length != boundary.ExpectedStackTypes.Length)
            {
                return new ExecutionFailure(
                    ExecutionFailureKind.InvalidStack,
                    "EXEC_INVALID_ENTRY_STACK",
                    $"The frozen boundary requires stack depth {boundary.ExpectedStackTypes.Length}, but the active frame carries {frame.EvalStack.Length}.",
                    frame.Method,
                    frame.IlOffset);
            }

            for (var stackIndex = 0; stackIndex < frame.EvalStack.Length; stackIndex++)
            {
                var valueFailure = ValidateValue(
                    frame.EvalStack[stackIndex],
                    boundary.ExpectedStackTypes[stackIndex],
                    "evaluation-stack",
                    stackIndex,
                    frame.Method,
                    frame.IlOffset,
                    ValuePrecisionRequirement.Executable);
                if (valueFailure is not null)
                {
                    return valueFailure;
                }
            }

            context = new PreparedGraphStepContext(frame, plan, instruction, boundary);
        }

        return context.Frame is null
            ? PlanInvalid(graph.Root, 0, "Prepared execution did not identify one active frame.")
            : null;
    }

    private ExecutionFailure? ValidatePreparedFrameSlots(
        FrameState<TValue> frame,
        AdmittedMethodPlan plan)
    {
        for (var index = 0; index < frame.Arguments.Length; index++)
        {
            var failure = ValidateValue(
                frame.Arguments[index],
                plan.ArgumentTypes[index],
                "argument",
                index,
                frame.Method,
                frame.IlOffset,
                plan.Definition.Signature.HasImplicitThis && index == 0
                    ? ValuePrecisionRequirement.Exact
                    : ValuePrecisionRequirement.Executable);
            if (failure is not null)
            {
                return failure;
            }
        }

        for (var index = 0; index < frame.Locals.Length; index++)
        {
            var failure = ValidateValue(
                frame.Locals[index],
                plan.Definition.Signature.LocalTypes[index],
                "local",
                index,
                frame.Method,
                frame.IlOffset,
                ValuePrecisionRequirement.Executable);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private ExecutionFailure? ValidateReturnSite(
        FrozenMethodGraphPlan graph,
        FrameState<TValue> caller,
        FrameState<TValue> callee,
        FrameReturnSite returnSite)
    {
        var identity = returnSite.CallSite;
        if (identity.Caller != caller.Method ||
            identity.Callee != callee.Method ||
            caller.IlOffset != returnSite.CallerResumeIlOffset ||
            !graph.TryGetAdmittedMethodPlan(caller.Method, out var callerPlan) ||
            callerPlan is null ||
            !callerPlan.TryGetInstruction(identity.CallIlOffset, out var callInstruction) ||
            callInstruction.Kind != AdmittedInstructionKind.Call ||
            callInstruction.CallTarget is null ||
            callInstruction.CallTarget.Method != callee.Method ||
            checked(identity.CallIlOffset + callInstruction.Size) != returnSite.CallerResumeIlOffset ||
            !graph.CallSites.Any(site =>
                site.Caller == caller.Method &&
                site.IlOffset == identity.CallIlOffset &&
                site.Target.Method == callee.Method &&
                site.MetadataToken == callInstruction.Operand &&
                site.Disposition == FrozenMethodCallDisposition.Interpreted))
        {
            return ReturnSiteInvalid(
                callee.Method,
                callee.IlOffset,
                "A nested frame's return site disagrees with its immediate caller or frozen direct-call edge.");
        }

        return null;
    }

    private ExecutionFailure? ValidateSuspendedCaller(
        FrozenMethodGraphPlan graph,
        FrameState<TValue> caller,
        AdmittedMethodPlan callerPlan,
        FrameReturnSite returnSite)
    {
        var callOffset = returnSite.CallSite.CallIlOffset;
        if (!callerPlan.TryGetInstruction(callOffset, out var instruction) ||
            instruction.Kind != AdmittedInstructionKind.Call ||
            instruction.CallTarget is null ||
            !callerPlan.TryGetBoundary(callOffset, out var callBoundary) ||
            !callerPlan.TryGetBoundary(returnSite.CallerResumeIlOffset, out var continuationBoundary))
        {
            return ReturnSiteInvalid(
                caller.Method,
                caller.IlOffset,
                "A suspended caller's return site does not identify retained call and continuation boundaries.");
        }

        var parameterTypes = instruction.CallTarget.Signature.ParameterTypes;
        var prefixLength = callBoundary.ExpectedStackTypes.Length - parameterTypes.Length;
        if (prefixLength < 0 ||
            caller.EvalStack.Length != prefixLength ||
            continuationBoundary.ExpectedStackTypes.Length != prefixLength + 1 ||
            continuationBoundary.ExpectedStackTypes[^1] != instruction.CallTarget.Signature.ReturnType)
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidStack,
                "EXEC_CALL_RETURN_SITE_INVALID",
                "A suspended caller does not carry the exact pre-argument stack prefix required by its call site.",
                caller.Method,
                callOffset);
        }

        for (var index = 0; index < prefixLength; index++)
        {
            var failure = ValidateValue(
                caller.EvalStack[index],
                callBoundary.ExpectedStackTypes[index],
                "suspended evaluation-stack",
                index,
                caller.Method,
                callOffset,
                ValuePrecisionRequirement.Executable);
            if (failure is not null)
            {
                return failure;
            }
        }

        return graph.TryGetAdmittedMethodPlan(returnSite.CallSite.Callee, out _) &&
            graph.CallSites.Any(site =>
                site.Caller == caller.Method &&
                site.IlOffset == callOffset &&
                site.Target.Method == returnSite.CallSite.Callee &&
                site.Disposition == FrozenMethodCallDisposition.Interpreted)
            ? null
            : PlanInvalid(
                caller.Method,
                callOffset,
                "A suspended caller targets no interpreted method in the prepared graph.");
    }

    private static bool TryGetFrozenCallSite(
        FrozenMethodGraphPlan graph,
        PreparedGraphStepContext context,
        out FrozenMethodCallSite? frozenCallSite)
    {
        frozenCallSite = null;
        var target = context.Instruction.CallTarget;
        if (target is null ||
            !graph.TryGetNode(context.Frame.Method, out var node) ||
            node is null)
        {
            return false;
        }

        foreach (var candidate in node.CallSites)
        {
            if (candidate.Caller != context.Frame.Method ||
                candidate.IlOffset != context.Frame.IlOffset ||
                candidate.MetadataToken != context.Instruction.Operand ||
                candidate.Target != target)
            {
                continue;
            }

            if (frozenCallSite is not null)
            {
                frozenCallSite = null;
                return false;
            }

            frozenCallSite = candidate;
        }

        return frozenCallSite is not null;
    }

    private StepOutcome<TValue, TMemory> ExecutePreparedModeledCall(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth,
        PreparedGraphStepContext context,
        FrozenMethodCallSite frozenCallSite,
        BudgetState updatedBudget)
    {
        var frame = context.Frame;
        var instruction = context.Instruction;
        var target = instruction.CallTarget;
        if (target is null ||
            frozenCallSite.Caller != frame.Method ||
            frozenCallSite.IlOffset != frame.IlOffset ||
            frozenCallSite.MetadataToken != instruction.Operand ||
            frozenCallSite.Disposition != FrozenMethodCallDisposition.PureModel ||
            frozenCallSite.Effects != EvaluationEffectStatus.None ||
            frozenCallSite.ModelDescriptor is null ||
            frozenCallSite.Target != target ||
            !graph.TryGetModeledLeaf(target.Method, out var leaf) ||
            leaf is null ||
            leaf.Target != target ||
            leaf.Descriptor != frozenCallSite.ModelDescriptor ||
            leaf.Descriptor.Confidence != PureCallModelConfidence.Exact ||
            leaf.Effects != EvaluationEffectStatus.None ||
            graph.TryGetAdmittedMethodPlan(target.Method, out _))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                PlanInvalid(
                    frame.Method,
                    frame.IlOffset,
                    "The pure-model call edge, opaque leaf, and frozen descriptor do not form one exclusive plan."));
        }

        var signature = target.Signature;
        if (signature.HasImplicitThis ||
            signature.HasExplicitThis ||
            signature.GenericParameterCount != 0 ||
            signature.ParameterTypes.Length != 2 ||
            signature.ParameterTypes.Any(type => type != TypeSig.Int32) ||
            signature.ReturnType != TypeSig.Int32)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                ModelPreflightInvalid(
                    "EXEC_MODEL_INVOCATION_INVALID",
                    "The frozen pure-model edge is outside the closed static two-Int32-to-Int32 invocation profile.",
                    frame.Method,
                    frame.IlOffset));
        }

        var enteredLogicalDepth = checked(state.CallStack.Length + 1);
        if (enteredLogicalDepth > maximumLogicalCallDepth ||
            enteredLogicalDepth > graph.RequiredLogicalDepth)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_CALL_DEPTH_INVARIANT",
                    "A prepared model invocation would exceed the frozen graph's logical-depth invariant.",
                    frame.Method,
                    frame.IlOffset));
        }

        var parameterCount = signature.ParameterTypes.Length;
        var prefixLength = frame.EvalStack.Length - parameterCount;
        var resumeOffset = checked(frame.IlOffset + instruction.Size);
        if (prefixLength < 0 ||
            !context.Plan.TryGetBoundary(resumeOffset, out var continuationBoundary) ||
            continuationBoundary.ExpectedStackTypes.Length != prefixLength + 1 ||
            continuationBoundary.ExpectedStackTypes[^1] != TypeSig.Int32 ||
            continuationBoundary.ExpectedStackTypes
                .Take(prefixLength)
                .Where((type, index) => type != context.Boundary.ExpectedStackTypes[index])
                .Any() ||
            continuationBoundary.ExpectedStackTypes.Length > context.Plan.Definition.Body.MaxStack)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                ModelPreflightInvalid(
                    "EXEC_MODEL_INVOCATION_INVALID",
                    "The modeled call has no exact frozen caller continuation for one Int32 result.",
                    frame.Method,
                    frame.IlOffset));
        }

        var arguments = frame.EvalStack
            .Skip(prefixLength)
            .ToImmutableArray();
        var modelArguments = ImmutableArray.CreateBuilder<PureCallModelArgument>(parameterCount);
        var containsExplainedUnknown = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            if (_domain.TryGetConstInt32(arguments[index], out var exactValue))
            {
                modelArguments.Add(PureCallModelArgument.ExactInt32(exactValue));
                continue;
            }

            if (_unknownExecutionPolicy == UnknownExecutionPolicy.ExplainedInt32 &&
                _domain is IValuePrecisionDomain<TValue> precisionDomain &&
                precisionDomain.GetPrecision(arguments[index]) == ValuePrecisionKind.ExplainedUnknown)
            {
                containsExplainedUnknown = true;
                modelArguments.Add(PureCallModelArgument.ExplainedUnknownInt32());
                continue;
            }

            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                ModelPreflightInvalid(
                    "EXEC_MODEL_ARGUMENT_INVALID",
                    "A pure-model argument is neither one exact Int32 nor an admitted explained Int32 unknown.",
                    frame.Method,
                    frame.IlOffset));
        }

        PureCallModelInvocation invocation;
        try
        {
            invocation = new PureCallModelInvocation(
                new DirectCallSiteIdentity(frame.Method, frame.IlOffset, target.Method),
                modelArguments.ToImmutable(),
                _unknownExecutionPolicy == UnknownExecutionPolicy.ExplainedInt32
                    ? PureCallModelUnknownPolicy.ExplainedInt32
                    : PureCallModelUnknownPolicy.ExactOnly);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                ModelPreflightInvalid(
                    "EXEC_MODEL_INVOCATION_INVALID",
                    "The frozen model invocation facts could not form the closed typed invocation value.",
                    frame.Method,
                    frame.IlOffset));
        }

        PureCallModelOutcome? outcome;
        try
        {
            outcome = leaf.RuntimeModel.Invoke(invocation);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                leaf.Descriptor.Identity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.CapabilityFailure,
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DomainFailure,
                "W4.Model.Capability",
                "The frozen pure-model capability failed without exposing exception-controlled payload.");
        }

        if (outcome is null)
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                leaf.Descriptor.Identity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.MalformedOutcome,
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DomainFailure,
                "W4.Model.OutcomeInvalid",
                "The frozen pure-model capability returned no typed outcome.");
        }

        switch (outcome.Kind)
        {
            case PureCallModelOutcomeKind.Blocked
                when outcome.StableCode is not null &&
                     outcome.Int32Value is null &&
                     outcome.ReturnType is null:
                return FailedModelAttempt(
                    state,
                    operationalState,
                    frozenCallSite,
                    leaf.Descriptor.Identity,
                    enteredLogicalDepth,
                    PureModelAttemptOutcomeKind.Blocked,
                    MachineRunStatus.Blocked,
                    ExecutionFailureKind.UnsupportedInstruction,
                    outcome.StableCode,
                    "The frozen pure model reported a payload-omitting capability limitation.");

            case PureCallModelOutcomeKind.Invalid
                when outcome.StableCode is not null &&
                     outcome.Int32Value is null &&
                     outcome.ReturnType is null:
                return FailedModelAttempt(
                    state,
                    operationalState,
                    frozenCallSite,
                    leaf.Descriptor.Identity,
                    enteredLogicalDepth,
                    PureModelAttemptOutcomeKind.Invalid,
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.DomainFailure,
                    outcome.StableCode,
                    "The frozen pure model rejected the immutable typed invocation facts.");

            case PureCallModelOutcomeKind.ExactReturn
                when outcome.Int32Value is { } exactReturn &&
                     outcome.StableCode is null &&
                     outcome.ReturnType == TypeSig.Int32:
                return CompleteExactModeledCall(
                    state,
                    operationalState,
                    context,
                    frozenCallSite,
                    leaf.Descriptor.Identity,
                    enteredLogicalDepth,
                    prefixLength,
                    resumeOffset,
                    updatedBudget,
                    exactReturn);

            case PureCallModelOutcomeKind.UnknownReturn
                when outcome.Int32Value is null &&
                     outcome.StableCode is null &&
                     outcome.ReturnType == TypeSig.Int32:
                return CompleteUnknownModeledCall(
                    state,
                    operationalState,
                    context,
                    frozenCallSite,
                    leaf.Descriptor.Identity,
                    enteredLogicalDepth,
                    arguments,
                    containsExplainedUnknown,
                    prefixLength,
                    resumeOffset,
                    updatedBudget);

            default:
                return FailedModelAttempt(
                    state,
                    operationalState,
                    frozenCallSite,
                    leaf.Descriptor.Identity,
                    enteredLogicalDepth,
                    PureModelAttemptOutcomeKind.MalformedOutcome,
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.DomainFailure,
                    "W4.Model.OutcomeInvalid",
                    "The frozen pure model returned an undefined or internally inconsistent typed outcome.");
        }
    }

    private StepOutcome<TValue, TMemory> CompleteExactModeledCall(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        PreparedGraphStepContext context,
        FrozenMethodCallSite frozenCallSite,
        PureCallModelIdentity modelIdentity,
        int enteredLogicalDepth,
        int prefixLength,
        int resumeOffset,
        BudgetState updatedBudget,
        int exactReturn)
    {
        TValue result;
        try
        {
            result = _domain.ConstInt32(exactReturn);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                modelIdentity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.ExactReturn,
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DomainFailure,
                "EXEC_DOMAIN_FAILURE",
                "The value domain failed while materializing an exact modeled return.");
        }

        try
        {
            var resultFailure = ValidateValue(
                result,
                TypeSig.Int32,
                "modeled return",
                0,
                context.Frame.Method,
                context.Frame.IlOffset,
                ValuePrecisionRequirement.Exact);
            if (resultFailure is not null ||
                !_domain.TryGetConstInt32(result, out var validatedReturn) ||
                validatedReturn != exactReturn)
            {
                return FailedModelAttempt(
                    state,
                    operationalState,
                    frozenCallSite,
                    modelIdentity,
                    enteredLogicalDepth,
                    PureModelAttemptOutcomeKind.ExactReturn,
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_MODEL_RESULT_INVALID",
                    "The value domain did not reproduce the model's exact typed return.");
            }
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                modelIdentity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.ExactReturn,
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DomainFailure,
                "EXEC_DOMAIN_FAILURE",
                "The value domain failed while validating an exact modeled return.");
        }

        return CompleteModeledCallTransfer(
            state,
            operationalState,
            context,
            frozenCallSite,
            modelIdentity,
            enteredLogicalDepth,
            PureModelAttemptOutcomeKind.ExactReturn,
            prefixLength,
            resumeOffset,
            updatedBudget,
            result);
    }

    private StepOutcome<TValue, TMemory> CompleteUnknownModeledCall(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        PreparedGraphStepContext context,
        FrozenMethodCallSite frozenCallSite,
        PureCallModelIdentity modelIdentity,
        int enteredLogicalDepth,
        ImmutableArray<TValue> arguments,
        bool containsExplainedUnknown,
        int prefixLength,
        int resumeOffset,
        BudgetState updatedBudget)
    {
        if (!containsExplainedUnknown)
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                modelIdentity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.UnknownReturn,
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "W4.Model.Limitation",
                "A model cannot introduce an ungrounded unknown from wholly exact arguments.");
        }

        if (_domain is not IPureCallModelLineageDomain<TValue> lineageDomain)
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                modelIdentity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.UnknownReturn,
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DomainFailure,
                "EXEC_MODEL_LINEAGE_UNAVAILABLE",
                "An unknown modeled return requires the pure-model lineage capability.");
        }

        TValue result;
        try
        {
            result = lineageDomain.CreateModeledReturnUnknown(
                new DirectCallSiteIdentity(
                    frozenCallSite.Caller,
                    frozenCallSite.IlOffset,
                    frozenCallSite.Target.Method),
                modelIdentity,
                arguments);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                modelIdentity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.UnknownReturn,
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DomainFailure,
                "EXEC_DOMAIN_FAILURE",
                "The pure-model lineage capability failed without exposing exception-controlled payload.");
        }

        try
        {
            var resultFailure = ValidateValue(
                result,
                TypeSig.Int32,
                "modeled return",
                0,
                context.Frame.Method,
                context.Frame.IlOffset,
                ValuePrecisionRequirement.Executable);
            if (resultFailure is not null ||
                lineageDomain.GetPrecision(result) != ValuePrecisionKind.ExplainedUnknown ||
                _domain.TryGetConstInt32(result, out _))
            {
                return FailedModelAttempt(
                    state,
                    operationalState,
                    frozenCallSite,
                    modelIdentity,
                    enteredLogicalDepth,
                    PureModelAttemptOutcomeKind.UnknownReturn,
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_MODEL_LINEAGE_INVALID",
                    "The pure-model lineage capability returned no locally valid explained Int32 unknown.");
            }
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return FailedModelAttempt(
                state,
                operationalState,
                frozenCallSite,
                modelIdentity,
                enteredLogicalDepth,
                PureModelAttemptOutcomeKind.UnknownReturn,
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DomainFailure,
                "EXEC_MODEL_LINEAGE_INVALID",
                "The pure-model lineage result was not a locally owned executable value.");
        }

        return CompleteModeledCallTransfer(
            state,
            operationalState,
            context,
            frozenCallSite,
            modelIdentity,
            enteredLogicalDepth,
            PureModelAttemptOutcomeKind.UnknownReturn,
            prefixLength,
            resumeOffset,
            updatedBudget,
            result);
    }

    private StepOutcome<TValue, TMemory> CompleteModeledCallTransfer(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        PreparedGraphStepContext context,
        FrozenMethodCallSite frozenCallSite,
        PureCallModelIdentity modelIdentity,
        int enteredLogicalDepth,
        PureModelAttemptOutcomeKind outcomeKind,
        int prefixLength,
        int resumeOffset,
        BudgetState updatedBudget,
        TValue result)
    {
        var resumedFrame = context.Frame with
        {
            IlOffset = resumeOffset,
            EvalStack = context.Frame.EvalStack
                .RemoveRange(prefixLength, context.Frame.EvalStack.Length - prefixLength)
                .Add(result),
        };
        var nextState = state with
        {
            CallStack = state.CallStack.SetItem(state.CallStack.Length - 1, resumedFrame),
        };
        var nextOperationalState = AppendModelAttempt(
            operationalState,
            frozenCallSite,
            modelIdentity,
            enteredLogicalDepth,
            outcomeKind,
            transferCompleted: true,
            stableCode: null) with
        {
            Budget = updatedBudget,
        };
        return new StepOutcome<TValue, TMemory>(
            nextState,
            nextOperationalState,
            MachineRunStatus.Ready,
            ImmutableArray.Create(ExecutedEvent(context.Frame, context.Instruction)));
    }

    private static StepOutcome<TValue, TMemory> FailedModelAttempt(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodCallSite frozenCallSite,
        PureCallModelIdentity modelIdentity,
        int enteredLogicalDepth,
        PureModelAttemptOutcomeKind outcomeKind,
        MachineRunStatus status,
        ExecutionFailureKind failureKind,
        string stableCode,
        string message)
    {
        var nextOperationalState = AppendModelAttempt(
            operationalState,
            frozenCallSite,
            modelIdentity,
            enteredLogicalDepth,
            outcomeKind,
            transferCompleted: false,
            stableCode);
        return Failed(
            state,
            nextOperationalState,
            status,
            new ExecutionFailure(
                failureKind,
                stableCode,
                message,
                frozenCallSite.Caller,
                frozenCallSite.IlOffset));
    }

    private static MachineOperationalState AppendModelAttempt(
        MachineOperationalState operationalState,
        FrozenMethodCallSite frozenCallSite,
        PureCallModelIdentity modelIdentity,
        int enteredLogicalDepth,
        PureModelAttemptOutcomeKind outcomeKind,
        bool transferCompleted,
        string? stableCode)
    {
        var attempt = new PureModelAttempt(
            new DirectCallSiteIdentity(
                frozenCallSite.Caller,
                frozenCallSite.IlOffset,
                frozenCallSite.Target.Method),
            modelIdentity,
            enteredLogicalDepth,
            outcomeKind,
            transferCompleted,
            stableCode);
        return operationalState with
        {
            ModelAttempts = operationalState.ModelAttempts.Add(attempt),
            ModelInvocationCount = checked(operationalState.ModelInvocationCount + 1),
            CompletedModeledCallCount = checked(
                operationalState.CompletedModeledCallCount + (transferCompleted ? 1 : 0)),
            ObservedLogicalDepthHighWater = Math.Max(
                operationalState.ObservedLogicalDepthHighWater,
                enteredLogicalDepth),
        };
    }

    private static ExecutionFailure ModelPreflightInvalid(
        string code,
        string message,
        MethodHandle method,
        int ilOffset) =>
        new(
            ExecutionFailureKind.InvalidInstruction,
            code,
            message,
            method,
            ilOffset);

    private StepOutcome<TValue, TMemory> ExecutePreparedCall(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth,
        PreparedGraphStepContext context,
        FrozenMethodCallSite frozenCallSite,
        BudgetState updatedBudget)
    {
        var frame = context.Frame;
        var instruction = context.Instruction;
        var target = instruction.CallTarget;
        if (target is null ||
            frozenCallSite.Caller != frame.Method ||
            frozenCallSite.IlOffset != frame.IlOffset ||
            frozenCallSite.Disposition != FrozenMethodCallDisposition.Interpreted ||
            frozenCallSite.Target != target ||
            frame.EvalStack.Length < target.Signature.ParameterTypes.Length ||
            !graph.TryGetAdmittedMethodPlan(target.Method, out var calleePlan) ||
            calleePlan is null ||
            calleePlan.Definition.Signature.CallSignature != target.Signature)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                PlanInvalid(frame.Method, frame.IlOffset, "The admitted call instruction has no correlated callee plan."));
        }

        var nextDepth = checked(state.CallStack.Length + 1);
        if (nextDepth > maximumLogicalCallDepth || nextDepth > graph.RequiredLogicalDepth)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_CALL_DEPTH_INVARIANT",
                    "A prepared call would exceed the graph's already validated logical-depth invariant.",
                    frame.Method,
                    frame.IlOffset));
        }

        var parameterCount = target.Signature.ParameterTypes.Length;
        var arguments = frame.EvalStack
            .Skip(frame.EvalStack.Length - parameterCount)
            .ToImmutableArray();
        var localsResult = CreateInitializedLocals(calleePlan, frame.Method, frame.IlOffset);
        if (localsResult.Failure is not null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                localsResult.Failure);
        }

        var callSite = new DirectCallSiteIdentity(
            frozenCallSite.Caller,
            frozenCallSite.IlOffset,
            frozenCallSite.Target.Method);
        var resumeOffset = checked(frame.IlOffset + instruction.Size);
        var calleeArguments = arguments;
        if (arguments.Any(IsExplainedUnknown))
        {
            if (_domain is not IInterpretedCallLineageDomain<TValue> lineageDomain)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.Blocked,
                    CallLineageFailure(
                        "EXEC_CALL_LINEAGE_UNAVAILABLE",
                        "Explained-unknown call arguments require the interpreted-call lineage capability.",
                        frame.Method,
                        frame.IlOffset));
            }

            try
            {
                calleeArguments = lineageDomain.TransformInterpretedCallArguments(callSite, arguments);
            }
            catch (Exception exception) when (IsCapabilityException(exception))
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.Blocked,
                    CallLineageFailure(
                        "EXEC_DOMAIN_FAILURE",
                        "The interpreted-call lineage capability failed while transforming the complete argument batch.",
                        frame.Method,
                        frame.IlOffset));
            }

            var transformFailure = ValidateCallLineageValues(
                arguments,
                calleeArguments,
                target.Signature.ParameterTypes,
                frame.Method,
                frame.IlOffset,
                "call argument");
            if (transformFailure is not null)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    transformFailure);
            }
        }

        var suspendedCaller = frame with
        {
            IlOffset = resumeOffset,
            EvalStack = frame.EvalStack.RemoveRange(frame.EvalStack.Length - parameterCount, parameterCount),
        };
        var callee = new FrameState<TValue>(
            target.Method,
            0,
            calleeArguments,
            localsResult.Locals,
            ImmutableArray<TValue>.Empty)
        {
            ReturnSite = new FrameReturnSite(callSite, resumeOffset),
        };
        var nextState = state with
        {
            CallStack = state.CallStack
                .SetItem(state.CallStack.Length - 1, suspendedCaller)
                .Add(callee),
        };
        var nextOperationalState = operationalState with
        {
            Budget = updatedBudget,
            ObservedLogicalDepthHighWater = Math.Max(
                operationalState.ObservedLogicalDepthHighWater,
                nextDepth),
            ActiveFrameDepthHighWater = Math.Max(
                operationalState.ActiveFrameDepthHighWater,
                nextDepth),
        };
        return new StepOutcome<TValue, TMemory>(
            nextState,
            nextOperationalState,
            MachineRunStatus.Ready,
            ImmutableArray.Create(
                ExecutedEvent(frame, instruction),
                new DebugEvent(
                    DebugEventKind.FramePushed,
                    target.Method,
                    0,
                    "Entry")));
    }

    private StepOutcome<TValue, TMemory> ExecutePreparedNestedReturn(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        PreparedGraphStepContext context,
        BudgetState updatedBudget)
    {
        var frame = context.Frame;
        var instruction = context.Instruction;
        if (frame.ReturnSite is null || frame.EvalStack.Length != 1)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                ReturnSiteInvalid(
                    frame.Method,
                    frame.IlOffset,
                    "A value-returning nested frame requires one exact return site and one result."));
        }

        var result = frame.EvalStack[0];
        var caller = state.CallStack[^2];
        if (caller is null ||
            !graph.TryGetAdmittedMethodPlan(caller.Method, out var callerPlan) ||
            callerPlan is null ||
            !callerPlan.TryGetBoundary(caller.IlOffset, out var continuationBoundary))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                PlanInvalid(frame.Method, frame.IlOffset, "The nested return has no retained caller continuation plan."));
        }

        var callerStackLength = checked(caller.EvalStack.Length + 1);
        if (callerStackLength > callerPlan.Definition.Body.MaxStack ||
            callerStackLength != continuationBoundary.ExpectedStackTypes.Length)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidStack,
                    "EXEC_CALL_RETURN_SITE_INVALID",
                    "The helper result does not complete the caller's frozen continuation stack.",
                    caller.Method,
                    caller.IlOffset));
        }

        var callerResult = result;
        if (IsExplainedUnknown(result))
        {
            if (_domain is not IInterpretedCallLineageDomain<TValue> lineageDomain)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.Blocked,
                    CallLineageFailure(
                        "EXEC_CALL_LINEAGE_UNAVAILABLE",
                        "An explained-unknown helper result requires the interpreted-call lineage capability.",
                        frame.Method,
                        frame.IlOffset));
            }

            try
            {
                callerResult = lineageDomain.TransformInterpretedReturn(frame.ReturnSite.CallSite, result);
            }
            catch (Exception exception) when (IsCapabilityException(exception))
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.Blocked,
                    CallLineageFailure(
                        "EXEC_DOMAIN_FAILURE",
                        "The interpreted-call lineage capability failed while transforming the helper result.",
                        frame.Method,
                        frame.IlOffset));
            }

            var transformFailure = ValidateCallLineageValues(
                ImmutableArray.Create(result),
                ImmutableArray.Create(callerResult),
                ImmutableArray.Create(TypeSig.Int32),
                frame.Method,
                frame.IlOffset,
                "interpreted return");
            if (transformFailure is not null)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    transformFailure);
            }
        }

        var callerStack = caller.EvalStack.Add(callerResult);

        for (var index = 0; index < callerStack.Length; index++)
        {
            var valueFailure = ValidateValue(
                callerStack[index],
                continuationBoundary.ExpectedStackTypes[index],
                "returned evaluation-stack",
                index,
                caller.Method,
                caller.IlOffset,
                ValuePrecisionRequirement.Executable);
            if (valueFailure is not null)
            {
                return Failed(state, operationalState, MachineRunStatus.InvalidProgram, valueFailure);
            }
        }

        var resumedCaller = caller with { EvalStack = callerStack };
        var nextStack = state.CallStack.RemoveAt(state.CallStack.Length - 1);
        nextStack = nextStack.SetItem(nextStack.Length - 1, resumedCaller);
        var nextState = state with { CallStack = nextStack };
        return new StepOutcome<TValue, TMemory>(
            nextState,
            operationalState with { Budget = updatedBudget },
            MachineRunStatus.Ready,
            ImmutableArray.Create(
                ExecutedEvent(frame, instruction),
                new DebugEvent(
                    DebugEventKind.FramePopped,
                    frame.Method,
                    frame.IlOffset,
                    instruction.Kind.ToString())));
    }

    private InitializedLocalsResult CreateInitializedLocals(
        AdmittedMethodPlan plan,
        MethodHandle failureMethod,
        int failureOffset)
    {
        var locals = ImmutableArray.CreateBuilder<TValue>(plan.Definition.Signature.LocalTypes.Length);
        for (var index = 0; index < plan.Definition.Signature.LocalTypes.Length; index++)
        {
            var type = plan.Definition.Signature.LocalTypes[index];
            var value = _domain.DefaultValue(type);
            var failure = ValidateValue(
                value,
                type,
                "initialized local",
                index,
                failureMethod,
                failureOffset,
                ValuePrecisionRequirement.Exact);
            if (failure is not null)
            {
                return new InitializedLocalsResult(default, failure);
            }

            locals.Add(value);
        }

        return new InitializedLocalsResult(locals.ToImmutable(), null);
    }

    private bool IsExplainedUnknown(TValue value) =>
        _domain is IValuePrecisionDomain<TValue> precisionDomain &&
        precisionDomain.GetPrecision(value) == ValuePrecisionKind.ExplainedUnknown;

    private ExecutionFailure? ValidateCallLineageValues(
        ImmutableArray<TValue> inputs,
        ImmutableArray<TValue> outputs,
        ImmutableArray<TypeSig> expectedTypes,
        MethodHandle method,
        int ilOffset,
        string boundaryName)
    {
        if (inputs.IsDefault ||
            outputs.IsDefault ||
            expectedTypes.IsDefault ||
            outputs.Length != inputs.Length ||
            expectedTypes.Length != inputs.Length)
        {
            return CallLineageInvalid(
                method,
                ilOffset,
                $"The {boundaryName} lineage capability returned a default or incorrectly sized vector.");
        }

        for (var index = 0; index < outputs.Length; index++)
        {
            ExecutionFailure? valueFailure;
            try
            {
                valueFailure = ValidateValue(
                    outputs[index],
                    expectedTypes[index],
                    $"transformed {boundaryName}",
                    index,
                    method,
                    ilOffset,
                    ValuePrecisionRequirement.Executable);
            }
            catch (ArgumentException)
            {
                return CallLineageInvalid(
                    method,
                    ilOffset,
                    $"The transformed {boundaryName} at index {index} is not a locally owned executable value.");
            }

            if (valueFailure is not null ||
                !_domain.IsLessThanOrEqual(inputs[index], outputs[index]) ||
                !_domain.IsLessThanOrEqual(outputs[index], inputs[index]))
            {
                return CallLineageInvalid(
                    method,
                    ilOffset,
                    $"The transformed {boundaryName} at index {index} does not preserve its validated semantic value.");
            }
        }

        return null;
    }

    private static ExecutionFailure CallLineageFailure(
        string code,
        string message,
        MethodHandle method,
        int ilOffset) =>
        new(
            ExecutionFailureKind.DomainFailure,
            code,
            message,
            method,
            ilOffset);

    private static ExecutionFailure CallLineageInvalid(
        MethodHandle method,
        int ilOffset,
        string message) =>
        new(
            ExecutionFailureKind.DomainFailure,
            "EXEC_CALL_LINEAGE_INVALID",
            message,
            method,
            ilOffset);

    private static ExecutionFailure PlanInvalid(
        MethodHandle method,
        int offset,
        string message) =>
        new(
            ExecutionFailureKind.DependencyResolution,
            "EXEC_CALL_PLAN_INVALID",
            message,
            method,
            offset);

    private static ExecutionFailure ReturnSiteInvalid(
        MethodHandle method,
        int offset,
        string message) =>
        new(
            ExecutionFailureKind.InvalidInstruction,
            "EXEC_CALL_RETURN_SITE_INVALID",
            message,
            method,
            offset);

    private readonly record struct PreparedGraphStepContext(
        FrameState<TValue> Frame,
        AdmittedMethodPlan Plan,
        AdmittedInstruction Instruction,
        MethodInstructionBoundary Boundary);

    private readonly record struct ExpectedModeledCall(
        DirectCallSiteIdentity CallSite,
        PureCallModelIdentity ModelIdentity,
        int EnteredLogicalDepth,
        bool Completed)
    {
        internal bool Matches(PureModelAttempt attempt) =>
            attempt.CallSite == CallSite &&
            attempt.ModelIdentity == ModelIdentity &&
            attempt.EnteredLogicalDepth == EnteredLogicalDepth;
    }

    private readonly record struct InitializedLocalsResult(
        ImmutableArray<TValue> Locals,
        ExecutionFailure? Failure);
}
