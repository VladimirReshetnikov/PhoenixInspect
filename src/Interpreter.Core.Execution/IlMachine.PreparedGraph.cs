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
    /// result. This operation consumes no instruction budget, emits no event, and never invokes the resolver.
    /// </returns>
    /// <remarks>
    /// This opt-in W4.5 prototype path is mutually exclusive with legacy <see cref="ActivateRoot"/> execution on the
    /// same machine. Public shape and policy remain draft-phase contracts subject to later product-facade refinement.
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
                return ExecutePreparedCall(
                    state,
                    operationalState,
                    graph,
                    maximumLogicalCallDepth,
                    context,
                    updatedBudget);
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

            var targetDepth = CalculateRootTargetWitnessedLogicalDepth(graph, offset);
            if (targetDepth is null ||
                operationalState.ObservedLogicalDepthHighWater != targetDepth.Value ||
                operationalState.ActiveFrameDepthHighWater != targetDepth.Value)
            {
                return Failed(
                    state,
                    operationalState,
                    MachineRunStatus.InvalidProgram,
                    new ExecutionFailure(
                        ExecutionFailureKind.InvalidInstruction,
                        "EXEC_CALL_DEPTH_INVARIANT",
                        "A prepared target-exception latch does not retain the exact rooted prefix depth needed to reach its field load.",
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

        if (operationalState.ObservedLogicalDepthHighWater < graph.RequiredLogicalDepth ||
            operationalState.ActiveFrameDepthHighWater < graph.RequiredLogicalDepth)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_CALL_DEPTH_INVARIANT",
                    "Completed branchless graph execution did not retain the depth required by every frozen call edge.",
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
                    "Completed prepared execution requires the root's exact typed return value.",
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
        var minimumWitnessedDepth = state.CallStack.IsDefault
            ? 0
            : CalculateMinimumWitnessedLogicalDepth(state, graph);
        var minimumObservedDepth = Math.Max(1, Math.Max(currentDepth, minimumWitnessedDepth));
        if (maximumLogicalCallDepth < graph.RequiredLogicalDepth ||
            operationalState.ConfiguredMaximumLogicalCallDepth != maximumLogicalCallDepth ||
            operationalState.RequiredLogicalCallDepth != graph.RequiredLogicalDepth ||
            currentDepth > maximumLogicalCallDepth ||
            currentDepth > graph.RequiredLogicalDepth ||
            currentDepth > 0 &&
            (operationalState.ObservedLogicalDepthHighWater != minimumObservedDepth ||
             operationalState.ActiveFrameDepthHighWater != minimumObservedDepth) ||
            currentDepth == 0 &&
            (operationalState.ObservedLogicalDepthHighWater < minimumObservedDepth ||
             operationalState.ActiveFrameDepthHighWater < minimumObservedDepth) ||
            operationalState.ObservedLogicalDepthHighWater > graph.RequiredLogicalDepth ||
            operationalState.ActiveFrameDepthHighWater > graph.RequiredLogicalDepth ||
            operationalState.ObservedLogicalDepthHighWater > maximumLogicalCallDepth ||
            operationalState.ActiveFrameDepthHighWater > maximumLogicalCallDepth ||
            operationalState.ObservedLogicalDepthHighWater != operationalState.ActiveFrameDepthHighWater)
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_CALL_DEPTH_INVARIANT",
                "Runtime frames and configured, required, or observed depth facts disagree with the bound prepared graph.",
                locationMethod,
                locationOffset);
        }

        return null;
    }

    private static int CalculateMinimumWitnessedLogicalDepth(
        MachineState<TValue, TMemory> state,
        FrozenMethodGraphPlan graph)
    {
        var minimum = state.CallStack.Length;
        var relativeDepths = new Dictionary<MethodHandle, int>();
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

                minimum = Math.Max(
                    minimum,
                    checked(
                        frameIndex + 1 +
                        CalculateRelativeLogicalDepth(graph, callSite.Target.Method, relativeDepths)));
            }
        }

        return minimum;
    }

    private static int? CalculateRootTargetWitnessedLogicalDepth(
        FrozenMethodGraphPlan graph,
        int targetOffset)
    {
        if (!graph.TryGetNode(graph.Root, out var root) || root is null)
        {
            return null;
        }

        var witnessedDepth = 1;
        var relativeDepths = new Dictionary<MethodHandle, int>();
        foreach (var callSite in root.CallSites)
        {
            if (callSite.IlOffset < targetOffset)
            {
                witnessedDepth = Math.Max(
                    witnessedDepth,
                    checked(1 + CalculateRelativeLogicalDepth(graph, callSite.Target.Method, relativeDepths)));
            }
        }

        return witnessedDepth;
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
                site.MetadataToken == callInstruction.Operand))
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

        return graph.TryGetAdmittedMethodPlan(returnSite.CallSite.Callee, out _)
            ? null
            : PlanInvalid(caller.Method, callOffset, "A suspended caller targets a method absent from the prepared graph.");
    }

    private StepOutcome<TValue, TMemory> ExecutePreparedCall(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth,
        PreparedGraphStepContext context,
        BudgetState updatedBudget)
    {
        var frame = context.Frame;
        var instruction = context.Instruction;
        var target = instruction.CallTarget;
        if (target is null ||
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
        if (arguments.Any(IsExplainedUnknown))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_CALL_LINEAGE_UNAVAILABLE",
                    "Explained-unknown call arguments require the W4.5b call-lineage capability.",
                    frame.Method,
                    frame.IlOffset));
        }

        var localsResult = CreateInitializedLocals(calleePlan, frame.Method, frame.IlOffset);
        if (localsResult.Failure is not null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                localsResult.Failure);
        }

        var callSite = new DirectCallSiteIdentity(frame.Method, frame.IlOffset, target.Method);
        var resumeOffset = checked(frame.IlOffset + instruction.Size);
        var suspendedCaller = frame with
        {
            IlOffset = resumeOffset,
            EvalStack = frame.EvalStack.RemoveRange(frame.EvalStack.Length - parameterCount, parameterCount),
        };
        var callee = new FrameState<TValue>(
            target.Method,
            0,
            arguments,
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
        if (IsExplainedUnknown(result))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_CALL_LINEAGE_UNAVAILABLE",
                    "An explained-unknown helper result requires the W4.5b interpreted-return lineage capability.",
                    frame.Method,
                    frame.IlOffset));
        }

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

        var callerStack = caller.EvalStack.Add(result);
        if (callerStack.Length > callerPlan.Definition.Body.MaxStack ||
            callerStack.Length != continuationBoundary.ExpectedStackTypes.Length)
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

    private readonly record struct InitializedLocalsResult(
        ImmutableArray<TValue> Locals,
        ExecutionFailure? Failure);
}
