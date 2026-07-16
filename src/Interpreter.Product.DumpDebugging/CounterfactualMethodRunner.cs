using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Owns issuer authority for preparing and executing rooted counterfactual method plans.
/// </summary>
/// <typeparam name="TMemory">The persistent memory snapshot type privately retained by successful plans.</typeparam>
/// <remarks>
/// The current draft validates raw host data without throwing for ordinary malformed inputs, invokes graph preparation
/// exactly once, and executes only a complete privately issued plan. Execution uses a fresh machine, the plan's frozen
/// runtime domain and memory binding, deterministic instruction accounting, and a resolver that rejects any accidental
/// execution-time resolution. Plans are privately stamped by this runner instance so null and foreign inputs are
/// rejected before cancellation, plan data, or retained capabilities are consulted.
/// </remarks>
public sealed class CounterfactualMethodRunner<TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    private const string TraversalBoundName = "counterfactual.preparation.traversal-units";
    private const string LogicalDepthBoundName = "counterfactual.execution.logical-call-depth";
    private readonly object issuer = new();

    /// <summary>Creates one independent preparation issuer with no ambient resolver, memory, or policy state.</summary>
    public CounterfactualMethodRunner()
    {
    }

    /// <summary>
    /// Prepares one complete frozen rooted-method plan or returns one stable multi-axis failure.
    /// </summary>
    /// <param name="candidate">
    /// The raw candidate snapshot. Null, malformed request data, missing bindings, incompatible evidence, planner
    /// failures, insufficient depth, and materialization failures are projected rather than thrown.
    /// </param>
    /// <returns>
    /// A strict union containing either one runner-issued plan or one value-free failure. Graph discovery runs at
    /// most once; a zero traversal limit exhausts before resolver or registry capability use; an interpreted-only
    /// request never queries an optional registry.
    /// </returns>
    public CounterfactualMethodPreparationResult<TMemory> Prepare(
        CounterfactualMethodPreparationCandidate<TMemory>? candidate)
    {
        if (candidate is null)
        {
            return RawFailure(
                "W4.Request.CandidateMissing",
                "A counterfactual preparation candidate is required.");
        }

        CounterfactualMethodRequest request;
        try
        {
            request = CounterfactualMethodRequest.CreateValidated(
                candidate.EvidenceSource,
                candidate.SyntheticEvidenceId,
                candidate.SnapshotIdentity!,
                candidate.ModuleIdentity!,
                candidate.RootSelectionId!,
                candidate.RootEvidenceSha256!,
                candidate.RootMethod,
                candidate.ReceiverEvidence!,
                candidate.Arguments,
                candidate.PolicyId!,
                candidate.PolicyVersion,
                candidate.InstructionLimit,
                candidate.LogicalDepthLimit,
                candidate.TraversalLimit,
                candidate.ModelCatalogId!,
                candidate.ModelCatalogVersion,
                candidate.RequiredModelTarget,
                candidate.Assumptions);
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return RawFailure(
                "W4.Request.Invalid",
                "The proposed counterfactual request fields are malformed or mutually incompatible.");
        }

        if (!TryValidateBindings(candidate, request, out var bindingCode, out var bindingMessage))
        {
            return Failure(
                EvaluationCompletionStatus.Invalid,
                EvaluationEvidenceStatus.Invalid,
                EvaluationEffectStatus.None,
                CreateContext(request, includeTraversal: false, includeLogicalDepth: false),
                request,
                traversalAccounting: null,
                coreFailure: null,
                bindingCode,
                bindingMessage);
        }

        MethodGraphPreparationResult graphPreparation;
        var planner = new MethodGraphPlanner(candidate.Resolver!);
        if (request.RequiredModelTarget is { } modeledTarget)
        {
            graphPreparation = planner.RequirePureModel(
                request.RootMethod,
                modeledTarget,
                candidate.ModelRegistry!,
                request.TraversalLimit);
        }
        else
        {
            graphPreparation = planner.Prepare(request.RootMethod, request.TraversalLimit);
        }

        if (!graphPreparation.IsSuccess)
        {
            if (!IsCoherentPlannerFailure(request, graphPreparation))
            {
                return PlannerResultInvalid(request, graphPreparation.TraversalAccounting);
            }

            return GraphFailure(request, graphPreparation);
        }

        if (!IsCoherentPlannerSuccess(request, graphPreparation))
        {
            return PlannerResultInvalid(request, graphPreparation.TraversalAccounting);
        }

        var graph = graphPreparation.Plan!;
        var accounting = graphPreparation.TraversalAccounting!;
        if (request.LogicalDepthLimit < graph.RequiredLogicalDepth)
        {
            return Failure(
                EvaluationCompletionStatus.BudgetExhausted,
                EvaluationEvidenceStatus.Exact,
                EvaluationEffectStatus.None,
                CreateContext(request, includeTraversal: true, includeLogicalDepth: true),
                request,
                accounting,
                coreFailure: null,
                "W4.Budget.CallDepth",
                "The configured logical call-depth bound is smaller than the complete frozen graph requires.");
        }

        if (!HasCompatibleRootSignature(request, graph))
        {
            return Failure(
                EvaluationCompletionStatus.Invalid,
                EvaluationEvidenceStatus.Invalid,
                EvaluationEffectStatus.None,
                CreateContext(request, includeTraversal: true, includeLogicalDepth: true),
                request,
                accounting,
                coreFailure: null,
                "W4.Request.RootSignature",
                "The rooted receiver and arguments are incompatible with the frozen root method signature.");
        }

        try
        {
            ValidateObservations(request, graph, candidate.FieldObservations);
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return Failure(
                EvaluationCompletionStatus.Invalid,
                EvaluationEvidenceStatus.Invalid,
                EvaluationEffectStatus.None,
                CreateContext(request, includeTraversal: true, includeLogicalDepth: true),
                request,
                accounting,
                coreFailure: null,
                "W4.Evidence.FieldObservationInvalid",
                "Field observations are incomplete, misordered, or incompatible with the frozen graph and receiver.");
        }

        CounterfactualRuntimeBundle<TMemory> materializedBundle;
        try
        {
            var input = CounterfactualMethodExecutionInput<TMemory>.CreateValidated(
                request,
                candidate.Resolver!,
                candidate.Domain!,
                candidate.MemoryModel!,
                candidate.InitialMemory!,
                candidate.Receiver!,
                candidate.FieldObservations,
                candidate.ModelRegistry);
            materializedBundle = input.RuntimeBundle.MaterializeRootArguments();
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return Failure(
                EvaluationCompletionStatus.Invalid,
                EvaluationEvidenceStatus.Invalid,
                EvaluationEffectStatus.None,
                CreateContext(request, includeTraversal: true, includeLogicalDepth: true),
                request,
                accounting,
                coreFailure: null,
                "W4.Request.ArgumentMaterialization",
                "The validated request inputs could not be materialized in the bound provenance domain.");
        }

        try
        {
            var plan = CounterfactualMethodPlan<TMemory>.Issue(
                issuer,
                request,
                graph,
                accounting,
                materializedBundle);
            return CounterfactualMethodPreparationResult<TMemory>.Succeeded(plan);
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return Failure(
                EvaluationCompletionStatus.Invalid,
                EvaluationEvidenceStatus.Invalid,
                EvaluationEffectStatus.None,
                CreateContext(request, includeTraversal: true, includeLogicalDepth: true),
                request,
                accounting,
                coreFailure: null,
                "W4.Replay.PlanInvalid",
                "The validated preparation facts disagreed at the private plan-issuance boundary.");
        }
    }

    /// <summary>
    /// Executes one complete runner-issued plan under its canonical instruction and logical-depth bounds.
    /// </summary>
    /// <param name="plan">
    /// The exact plan instance issued by this runner. A null or foreign plan is rejected before cancellation is
    /// observed and before any public plan fact or private runtime binding is read.
    /// </param>
    /// <param name="cancellationToken">
    /// Host cancellation observed only at ready machine boundaries. A completed terminal transition and its mandatory
    /// certified idempotent re-step take precedence over cancellation that arrives after the terminal transfer.
    /// </param>
    /// <returns>
    /// One canonical rooted result, or the fixed identity-free facade-rejection result for a null or foreign plan.
    /// Ordinary resolver, memory, model, and value-domain capability failures are normalized into stable result axes
    /// and diagnostics rather than exposing exception-controlled payload.
    /// </returns>
    /// <remarks>
    /// This is a draft W4 product facade, not a general-purpose machine host. The run is read-only, branchless, finite,
    /// and bound to the exact graph, evidence, model capabilities, and provenance domain frozen during preparation.
    /// Its public and canonical shape remains unstable until the conceptual-design phase closes.
    /// </remarks>
    public CounterfactualExecutionResult Run(
        CounterfactualMethodPlan<TMemory>? plan,
        CancellationToken cancellationToken = default)
    {
        if (plan is null)
        {
            return CounterfactualExecutionResult.CreateFacadeRejection(
                [new EvaluationDiagnostic(
                    "W4.Request.PlanMissing",
                    "A runner-issued counterfactual method plan is required.")]);
        }

        // IsIssuedBy is deliberately the only plan operation before authority is established.
        if (!plan.IsIssuedBy(issuer))
        {
            return CounterfactualExecutionResult.CreateFacadeRejection(
                [new EvaluationDiagnostic(
                    "W4.Request.PlanForeign",
                    "The counterfactual method plan was not issued by this runner.")]);
        }

        var request = plan.Request;
        var graph = plan.RuntimeGraph;
        var saturation = request.InstructionLimit == long.MaxValue
            ? long.MaxValue
            : request.InstructionLimit + 1;
        if (!TryCalculateDynamicInstructionCost(graph, saturation, out var dynamicInstructionCost) ||
            dynamicInstructionCost <= 0)
        {
            return CreatePreactivationInvalid(
                plan,
                "W4.Replay.DynamicGraphInvalid",
                "The issued graph did not produce one finite acyclic dynamic instruction bound.");
        }

        var bundle = plan.RuntimeBundle;
        CounterfactualRecordingMemoryModel<TMemory> recordingMemory;
        IlMachine<ProvenanceConcreteValue, TMemory> machine;
        try
        {
            recordingMemory = new CounterfactualRecordingMemoryModel<TMemory>(
                bundle.MemoryModel,
                bundle.Receiver,
                bundle.Domain,
                plan.RuntimeFieldObservations);
            machine = new IlMachine<ProvenanceConcreteValue, TMemory>(
                bundle.Domain,
                RejectingExecutionResolver.Instance,
                recordingMemory,
                new InstructionBudgetPolicy(),
                UnknownExecutionPolicy.ExplainedInt32);
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return CreatePreactivationInvalid(
                plan,
                "W4.Replay.RuntimeBindingInvalid",
                "The issued plan's private runtime binding was not internally coherent.");
        }

        MachineActivationResult<ProvenanceConcreteValue, TMemory> activation;
        try
        {
            activation = machine.ActivatePreparedGraph(
                graph,
                request.LogicalDepthLimit,
                bundle.RootArguments,
                bundle.InitialMemory);
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return CreatePreactivationStopped(
                plan,
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Exact,
                "W4.Unknown.ActivationCapability",
                "A runtime capability failed during prepared root activation.");
        }

        var activationFailure = ValidateActivation(activation, graph, bundle);
        if (activationFailure is not null)
        {
            var completion = activation.Status == MachineRunStatus.Blocked
                ? EvaluationCompletionStatus.Blocked
                : EvaluationCompletionStatus.Invalid;
            var evidence = completion == EvaluationCompletionStatus.Invalid
                ? EvaluationEvidenceStatus.Invalid
                : EvaluationEvidenceStatus.Exact;
            return CreatePreactivationStopped(
                plan,
                completion,
                evidence,
                activationFailure.Code,
                activationFailure.Message);
        }

        var state = activation.State!;
        MachineOperationalState operationalState;
        try
        {
            operationalState = machine.CreatePreparedOperationalState(new BudgetState(request.InstructionLimit));
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return CreatePreactivationInvalid(
                plan,
                "W4.Replay.OperationalStateInvalid",
                "The activated machine could not create its prepared operational envelope.");
        }

        var callTrace = ImmutableArray.CreateBuilder<MethodHandle>();
        callTrace.Add(graph.Root);
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        long stepRequests = 0;
        var maximumStepRequests = SaturatingAdd(
            Math.Min(dynamicInstructionCost, request.InstructionLimit),
            1,
            long.MaxValue);

        while (true)
        {
            if (state.CallStack.IsDefault || state.Memory is null || operationalState.Budget is null)
            {
                return CreateExecutionInvalid(
                    plan,
                    recordingMemory,
                    operationalState,
                    callTrace,
                    events,
                    "W4.Replay.StateEnvelopeInvalid",
                    "Prepared execution produced an uninitialized state or operational envelope.");
            }

            if (cancellationToken.IsCancellationRequested &&
                state.CallStack.Length > 0 &&
                state.TerminalTargetException is null)
            {
                var used = request.InstructionLimit - operationalState.Budget.InstructionBudget;
                var instructionStatus = stepRequests == 0
                    ? CounterfactualBoundStatus.NotReached
                    : CounterfactualBoundStatus.Applied;
                return CreateRootedResult(
                    plan,
                    EvaluationCompletionStatus.Cancelled,
                    used == 0 ? EvaluationCompleteness.None : EvaluationCompleteness.Partial,
                    AggregateReachedEvidence(request, recordingMemory.ReachedObservations),
                    used == 0 ? null : CounterfactualExecutionValue.CreateExecutionPrefix(),
                    instructionStatus,
                    instructionStatus == CounterfactualBoundStatus.NotReached ? null : used,
                    instructionStatus == CounterfactualBoundStatus.NotReached
                        ? null
                        : operationalState.Budget.InstructionBudget,
                    operationalState.ObservedLogicalDepthHighWater,
                    operationalState.ActiveFrameDepthHighWater,
                    CounterfactualBoundStatus.NotReached,
                    lineageNodeCount: null,
                    recordingMemory.ReachedObservations,
                    recordingMemory.ReachedLoadOrdinals,
                    operationalState.ModelAttempts,
                    operationalState.ModelInvocationCount,
                    operationalState.CompletedModeledCallCount,
                    callTrace.ToImmutable(),
                    events.ToImmutable(),
                    diagnostics:
                    [
                        new EvaluationDiagnostic(
                            "W4.Execution.Cancelled",
                            "Host cancellation was observed at a ready machine boundary."),
                    ]);
            }

            if (stepRequests >= maximumStepRequests)
            {
                return CreateExecutionInvalid(
                    plan,
                    recordingMemory,
                    operationalState,
                    callTrace,
                    events,
                    "W4.Replay.StepBoundExceeded",
                    "Prepared execution exceeded the graph-derived finite transition bound.");
            }

            var priorState = state;
            var priorOperationalState = operationalState;
            StepOutcome<ProvenanceConcreteValue, TMemory> outcome;
            try
            {
                outcome = machine.StepOne(priorState, priorOperationalState);
            }
            catch (Exception exception) when (IsOrdinaryFailure(exception))
            {
                return CreateExecutionStopped(
                    plan,
                    recordingMemory,
                    priorOperationalState,
                    callTrace,
                    events,
                    EvaluationCompletionStatus.Blocked,
                    "W4.Execution.Capability",
                    "A runtime capability failed while requesting one prepared machine transition.");
            }

            stepRequests++;
            if (!outcome.IsMachineIssuedTransitionFrom(machine, priorState, priorOperationalState))
            {
                return CreateExecutionInvalid(
                    plan,
                    recordingMemory,
                    priorOperationalState,
                    callTrace,
                    events,
                    "W4.Replay.TransitionUncertified",
                    "The machine transition was not certified for the exact supplied state and operational envelope.");
            }

            var transitionFailure = ValidateMachineTransition(
                graph,
                request.LogicalDepthLimit,
                priorState,
                priorOperationalState,
                outcome);
            if (transitionFailure is not null)
            {
                return CreateExecutionInvalid(
                    plan,
                    recordingMemory,
                    priorOperationalState,
                    callTrace,
                    events,
                    transitionFailure.Code,
                    transitionFailure.Message);
            }

            AppendDynamicEntries(priorOperationalState, outcome, callTrace, events);
            state = outcome.State;
            operationalState = outcome.OperationalState;

            switch (outcome.Status)
            {
                case MachineRunStatus.Ready:
                    continue;

                case MachineRunStatus.Completed:
                    return CompleteRootedExecution(
                        plan,
                        machine,
                        recordingMemory,
                        state,
                        operationalState,
                        callTrace,
                        events);

                case MachineRunStatus.BudgetExhausted:
                    return CreateBudgetExhausted(
                        plan,
                        recordingMemory,
                        operationalState,
                        callTrace,
                        events);

                case MachineRunStatus.Blocked:
                    return CreateMachineFailure(
                        plan,
                        recordingMemory,
                        operationalState,
                        callTrace,
                        events,
                        outcome,
                        EvaluationCompletionStatus.Blocked);

                case MachineRunStatus.InvalidProgram:
                    return CreateMachineFailure(
                        plan,
                        recordingMemory,
                        operationalState,
                        callTrace,
                        events,
                        outcome,
                        EvaluationCompletionStatus.Invalid);

                case MachineRunStatus.TargetException:
                    return CreateExecutionInvalid(
                        plan,
                        recordingMemory,
                        priorOperationalState,
                        callTrace,
                        RemoveLastTransitionEvents(events, outcome.Events.Length),
                        "W4.TargetException.RootedUnsupported",
                        "The closed non-null rooted facade cannot retain a target-exception transition.");

                default:
                    return CreateExecutionInvalid(
                        plan,
                        recordingMemory,
                        priorOperationalState,
                        callTrace,
                        RemoveLastTransitionEvents(events, outcome.Events.Length),
                        "W4.Replay.StatusInvalid",
                        "Prepared execution returned an undefined machine status.");
            }
        }
    }

    private static ProjectionFailure? ValidateActivation(
        MachineActivationResult<ProvenanceConcreteValue, TMemory> activation,
        FrozenMethodGraphPlan graph,
        CounterfactualRuntimeBundle<TMemory> bundle)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(bundle);

        if (!activation.IsSuccess)
        {
            if (activation.State is not null || activation.Failure is null ||
                activation.Status is not (MachineRunStatus.Blocked or MachineRunStatus.InvalidProgram))
            {
                return new ProjectionFailure(
                    "W4.Replay.ActivationUnionInvalid",
                    "Prepared activation returned an incoherent state, status, and failure union.");
            }

            return activation.Status == MachineRunStatus.Blocked
                ? new ProjectionFailure(
                    "W4.Unknown.ActivationBlocked",
                    "Prepared root activation was blocked by a required runtime capability.")
                : new ProjectionFailure(
                    "W4.Replay.ActivationInvalid",
                    "Prepared root activation rejected an internally inconsistent runtime binding.");
        }

        var state = activation.State!;
        if (activation.Status != MachineRunStatus.Ready || activation.Failure is not null ||
            state.CallStack.IsDefault || state.CallStack.Length != 1 ||
            state.CallStack[0] is not { } rootFrame ||
            state.ReturnValue.HasValue || state.TerminalTargetException is not null ||
            state.Memory is null || !HasSameMemoryIdentity(state.Memory, bundle.InitialMemory) ||
            rootFrame.Method != graph.Root || rootFrame.IlOffset != 0 || rootFrame.ReturnSite is not null ||
            rootFrame.Arguments.IsDefault || rootFrame.Locals.IsDefault || rootFrame.EvalStack.IsDefault ||
            !rootFrame.EvalStack.IsEmpty || rootFrame.Arguments.Length != bundle.RootArguments.Length)
        {
            return new ProjectionFailure(
                "W4.Replay.ActivationStateInvalid",
                "Prepared activation did not produce the exact single-root initial state required by the issued plan.");
        }

        for (var index = 0; index < rootFrame.Arguments.Length; index++)
        {
            if (!ReferenceEquals(rootFrame.Arguments[index], bundle.RootArguments[index]))
            {
                return new ProjectionFailure(
                    "W4.Replay.ActivationArgumentsInvalid",
                    "Prepared activation did not retain the exact materialized rooted argument vector.");
            }
        }

        return null;
    }

    private static ProjectionFailure? ValidateMachineTransition(
        FrozenMethodGraphPlan graph,
        int logicalDepthLimit,
        MachineState<ProvenanceConcreteValue, TMemory> priorState,
        MachineOperationalState priorOperationalState,
        StepOutcome<ProvenanceConcreteValue, TMemory> outcome)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(priorState);
        ArgumentNullException.ThrowIfNull(priorOperationalState);
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.State is null || outcome.OperationalState is null || outcome.Events.IsDefault ||
            outcome.Events.Any(static item => item is null) || priorState.Memory is null || outcome.State.Memory is null ||
            !HasSameMemoryIdentity(priorState.Memory, outcome.State.Memory) ||
            priorState.CallStack.IsDefault || outcome.State.CallStack.IsDefault ||
            priorOperationalState.Budget is null || outcome.OperationalState.Budget is null)
        {
            return InvalidTransition("Envelope", "The machine transition returned an uninitialized or memory-changing envelope.");
        }

        var priorEnvelopeFailure = ValidateOperationalEnvelope(
            graph,
            logicalDepthLimit,
            priorOperationalState);
        var nextEnvelopeFailure = ValidateOperationalEnvelope(
            graph,
            logicalDepthLimit,
            outcome.OperationalState);
        if (priorEnvelopeFailure is not null || nextEnvelopeFailure is not null)
        {
            return priorEnvelopeFailure ?? nextEnvelopeFailure;
        }

        var priorAttempts = priorOperationalState.ModelAttempts;
        var nextAttempts = outcome.OperationalState.ModelAttempts;
        if (nextAttempts.Length < priorAttempts.Length || nextAttempts.Length > priorAttempts.Length + 1 ||
            !nextAttempts.Take(priorAttempts.Length).SequenceEqual(priorAttempts))
        {
            return InvalidTransition("ModelAttempt", "A transition did not preserve the exact model-attempt prefix.");
        }

        var attemptDelta = nextAttempts.Length - priorAttempts.Length;
        var invocationDelta = outcome.OperationalState.ModelInvocationCount -
            priorOperationalState.ModelInvocationCount;
        var completedDelta = outcome.OperationalState.CompletedModeledCallCount -
            priorOperationalState.CompletedModeledCallCount;
        if (attemptDelta != invocationDelta || completedDelta is < 0 or > 1 || completedDelta > attemptDelta ||
            outcome.OperationalState.ObservedLogicalDepthHighWater <
                priorOperationalState.ObservedLogicalDepthHighWater ||
            outcome.OperationalState.ActiveFrameDepthHighWater <
                priorOperationalState.ActiveFrameDepthHighWater ||
            outcome.OperationalState.ObservedLogicalDepthHighWater -
                priorOperationalState.ObservedLogicalDepthHighWater > 1 ||
            outcome.OperationalState.ActiveFrameDepthHighWater -
                priorOperationalState.ActiveFrameDepthHighWater > 1)
        {
            return InvalidTransition("OperationalDelta", "A transition returned an impossible model counter or depth delta.");
        }

        var priorBudget = priorOperationalState.Budget.InstructionBudget;
        var nextBudget = outcome.OperationalState.Budget.InstructionBudget;
        switch (outcome.Status)
        {
            case MachineRunStatus.Ready:
            case MachineRunStatus.Completed:
                if (outcome.Failure is not null || outcome.TargetException is not null ||
                    priorBudget <= 0 || nextBudget != priorBudget - 1 ||
                    !IsOrdinaryTransferEventVector(outcome.Events) ||
                    !ValidateSuccessfulModelDelta(nextAttempts, attemptDelta, completedDelta) ||
                    !ValidateFrameDelta(priorState, outcome.State, outcome.Events, outcome.Status))
                {
                    return InvalidTransition("SuccessfulTransfer", "A ready or completed transition violated its atomic transfer contract.");
                }

                if (outcome.Status == MachineRunStatus.Ready &&
                    (outcome.State.CallStack.IsEmpty || outcome.State.ReturnValue.HasValue ||
                     outcome.State.TerminalTargetException is not null))
                {
                    return InvalidTransition("ReadyUnion", "A ready transition returned a terminal machine state.");
                }

                if (outcome.Status == MachineRunStatus.Completed &&
                    (!outcome.State.CallStack.IsEmpty || !outcome.State.ReturnValue.HasValue ||
                     outcome.State.TerminalTargetException is not null))
                {
                    return InvalidTransition("CompletedUnion", "A completed transition did not return one normal root value.");
                }

                return null;

            case MachineRunStatus.BudgetExhausted:
                if (outcome.Failure is not null || outcome.TargetException is not null || !outcome.Events.IsEmpty ||
                    priorBudget != 0 || nextBudget != 0 || attemptDelta != 0 || completedDelta != 0 ||
                    !ReferenceEquals(priorState, outcome.State) ||
                    !ReferenceEquals(priorOperationalState, outcome.OperationalState))
                {
                    return InvalidTransition("Budget", "Instruction exhaustion did not preserve the exact pre-instruction state and envelope.");
                }

                return null;

            case MachineRunStatus.Blocked:
            case MachineRunStatus.InvalidProgram:
                if (outcome.Failure is null || outcome.TargetException is not null || !outcome.Events.IsEmpty ||
                    nextBudget != priorBudget || !ReferenceEquals(priorState, outcome.State) ||
                    !ValidateFailedModelDelta(
                        priorOperationalState,
                        outcome.OperationalState,
                        nextAttempts,
                        attemptDelta,
                        completedDelta))
                {
                    return InvalidTransition("Failure", "A blocked or invalid transition changed semantic state, budget, events, or model accounting.");
                }

                return null;

            case MachineRunStatus.TargetException:
                if (outcome.Failure is not null || outcome.TargetException is null ||
                    outcome.State.TerminalTargetException != outcome.TargetException ||
                    !outcome.State.CallStack.IsEmpty || outcome.State.ReturnValue.HasValue ||
                    priorBudget <= 0 || nextBudget != priorBudget - 1 || attemptDelta != 0 || completedDelta != 0 ||
                    outcome.Events.Length != 1 ||
                    outcome.Events[0].Kind != DebugEventKind.TargetExceptionRaised)
                {
                    return InvalidTransition("TargetException", "A target-exception transition violated its terminal union or accounting contract.");
                }

                return null;

            default:
                return InvalidTransition("Status", "The machine transition returned an undefined status.");
        }
    }

    private static ProjectionFailure? ValidateOperationalEnvelope(
        FrozenMethodGraphPlan graph,
        int logicalDepthLimit,
        MachineOperationalState state)
    {
        if (state.Budget is null || state.Budget.InstructionBudget < 0 || state.ModelAttempts.IsDefault ||
            state.ModelAttempts.Any(static item => item is null) ||
            state.ModelInvocationCount != state.ModelAttempts.Length ||
            state.CompletedModeledCallCount != state.ModelAttempts.Count(static item => item.TransferCompleted) ||
            state.ConfiguredMaximumLogicalCallDepth != logicalDepthLimit ||
            state.RequiredLogicalCallDepth != graph.RequiredLogicalDepth ||
            state.ObservedLogicalDepthHighWater is < 1 ||
            state.ObservedLogicalDepthHighWater > graph.RequiredLogicalDepth ||
            state.ActiveFrameDepthHighWater is < 1 ||
            state.ActiveFrameDepthHighWater > state.ObservedLogicalDepthHighWater)
        {
            return InvalidTransition("OperationalEnvelope", "Prepared operational accounting disagreed with the frozen graph or configured depth.");
        }

        return null;
    }

    private static bool ValidateSuccessfulModelDelta(
        ImmutableArray<PureModelAttempt> attempts,
        int attemptDelta,
        int completedDelta) =>
        attemptDelta == 0 && completedDelta == 0 ||
        attemptDelta == 1 && completedDelta == 1 && attempts[^1].TransferCompleted;

    private static bool ValidateFailedModelDelta(
        MachineOperationalState prior,
        MachineOperationalState next,
        ImmutableArray<PureModelAttempt> attempts,
        int attemptDelta,
        int completedDelta)
    {
        if (attemptDelta == 0)
        {
            return completedDelta == 0 && ReferenceEquals(prior, next);
        }

        return attemptDelta == 1 && completedDelta == 0 && !attempts[^1].TransferCompleted &&
            ReferenceEquals(prior.Budget, next.Budget) &&
            next.ActiveFrameDepthHighWater == prior.ActiveFrameDepthHighWater &&
            next.ObservedLogicalDepthHighWater == Math.Max(
                prior.ObservedLogicalDepthHighWater,
                attempts[^1].EnteredLogicalDepth);
    }

    private static bool IsOrdinaryTransferEventVector(ImmutableArray<DebugEvent> events)
    {
        if (events.Length is < 1 or > 2 || events[0].Kind != DebugEventKind.InstructionExecuted ||
            events[0].FieldEvidence is not null)
        {
            return false;
        }

        return events.Length == 1 || events[1].Kind is
            DebugEventKind.FramePushed or DebugEventKind.FramePopped or DebugEventKind.ValuePrecisionLost;
    }

    private static bool ValidateFrameDelta(
        MachineState<ProvenanceConcreteValue, TMemory> prior,
        MachineState<ProvenanceConcreteValue, TMemory> next,
        ImmutableArray<DebugEvent> events,
        MachineRunStatus status)
    {
        var expectedDelta = events.Length == 2
            ? events[1].Kind switch
            {
                DebugEventKind.FramePushed => 1,
                DebugEventKind.FramePopped => -1,
                _ => 0,
            }
            : 0;
        if (next.CallStack.Length - prior.CallStack.Length != expectedDelta)
        {
            return false;
        }

        return status == MachineRunStatus.Completed
            ? expectedDelta == -1
            : !next.CallStack.IsEmpty;
    }

    private static void AppendDynamicEntries(
        MachineOperationalState priorOperationalState,
        StepOutcome<ProvenanceConcreteValue, TMemory> outcome,
        ImmutableArray<MethodHandle>.Builder callTrace,
        ImmutableArray<DebugEvent>.Builder events)
    {
        for (var index = priorOperationalState.ModelAttempts.Length;
             index < outcome.OperationalState.ModelAttempts.Length;
             index++)
        {
            callTrace.Add(outcome.OperationalState.ModelAttempts[index].CallSite.Callee);
        }

        foreach (var item in outcome.Events)
        {
            events.Add(item);
            if (item.Kind == DebugEventKind.FramePushed)
            {
                callTrace.Add(item.Method);
            }
        }
    }

    private static CounterfactualExecutionResult CompleteRootedExecution(
        CounterfactualMethodPlan<TMemory> plan,
        IlMachine<ProvenanceConcreteValue, TMemory> machine,
        CounterfactualRecordingMemoryModel<TMemory> recordingMemory,
        MachineState<ProvenanceConcreteValue, TMemory> state,
        MachineOperationalState operationalState,
        ImmutableArray<MethodHandle>.Builder callTrace,
        ImmutableArray<DebugEvent>.Builder events)
    {
        var bundle = plan.RuntimeBundle;
        if (state.CallStack.IsDefault || !state.CallStack.IsEmpty || !state.ReturnValue.HasValue ||
            state.TerminalTargetException is not null || state.Memory is null ||
            !HasSameMemoryIdentity(state.Memory, bundle.InitialMemory))
        {
            return CreateExecutionInvalid(
                plan,
                recordingMemory,
                operationalState,
                callTrace,
                events,
                "W4.Replay.TerminalStateInvalid",
                "Completed execution did not retain one normal read-only root return.");
        }

        var reachedLoadCount = recordingMemory.ReachedLoadOrdinals.Length;
        var internedNodeCount = bundle.Domain.InternedNodeCount;
        StepOutcome<ProvenanceConcreteValue, TMemory> repeated;
        try
        {
            repeated = machine.StepOne(state, operationalState);
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return CreateExecutionInvalid(
                plan,
                recordingMemory,
                operationalState,
                callTrace,
                events,
                "W4.Replay.TerminalRestepFailed",
                "The mandatory terminal idempotence check failed inside a runtime capability.");
        }

        if (!repeated.IsMachineIssuedTransitionFrom(machine, state, operationalState) ||
            repeated.Status != MachineRunStatus.Completed || repeated.Failure is not null ||
            repeated.TargetException is not null || !ReferenceEquals(repeated.State, state) ||
            !ReferenceEquals(repeated.OperationalState, operationalState) || repeated.Events.IsDefault ||
            !repeated.Events.IsEmpty || recordingMemory.ReachedLoadOrdinals.Length != reachedLoadCount ||
            bundle.Domain.InternedNodeCount != internedNodeCount)
        {
            return CreateExecutionInvalid(
                plan,
                recordingMemory,
                operationalState,
                callTrace,
                events,
                "W4.Replay.TerminalRestepInvalid",
                "The completed machine did not produce one certified identity-preserving terminal re-step.");
        }

        var evidence = AggregateReachedEvidence(plan.Request, recordingMemory.ReachedObservations);
        CounterfactualExecutionValue value;
        EvaluationCompleteness completeness;
        CounterfactualBoundStatus lineageStatus;
        int? lineageNodeCount;
        try
        {
            var returned = state.ReturnValue.Value;
            switch (bundle.Domain.GetPrecision(returned))
            {
                case ValuePrecisionKind.Exact when
                    bundle.Domain.TryGetConstInt32(returned, out var exact) &&
                    evidence == EvaluationEvidenceStatus.Exact:
                    value = CounterfactualExecutionValue.CreateExactInt32(exact);
                    completeness = EvaluationCompleteness.Complete;
                    lineageStatus = CounterfactualBoundStatus.NotReached;
                    lineageNodeCount = null;
                    break;

                case ValuePrecisionKind.ExplainedUnknown when
                    evidence is EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable:
                    var lineage = bundle.Domain.CaptureLineage(returned);
                    if (lineage.Nodes.Length > plan.Request.LineageNodeCeiling)
                    {
                        return CreateExecutionInvalid(
                            plan,
                            recordingMemory,
                            operationalState,
                            callTrace,
                            events,
                            "W4.Replay.LineageCeilingInvalid",
                            "The derived finite lineage ceiling did not cover the completed return explanation.");
                    }

                    value = CounterfactualExecutionValue.CreateUnknownInt32(lineage);
                    completeness = EvaluationCompleteness.Partial;
                    lineageStatus = CounterfactualBoundStatus.Applied;
                    lineageNodeCount = lineage.Nodes.Length;
                    break;

                default:
                    return CreateExecutionInvalid(
                        plan,
                        recordingMemory,
                        operationalState,
                        callTrace,
                        events,
                        "W4.Replay.TerminalValueInvalid",
                        "The completed return precision, structural type, and reached evidence aggregate disagree.");
            }
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            return CreateExecutionInvalid(
                plan,
                recordingMemory,
                operationalState,
                callTrace,
                events,
                "W4.Replay.TerminalValueInvalid",
                "The completed return could not be projected as one canonical exact or explained-unknown Int32 value.");
        }

        return CreateRootedResult(
            plan,
            EvaluationCompletionStatus.Completed,
            completeness,
            evidence,
            value,
            CounterfactualBoundStatus.Applied,
            CountExecutedInstructions(events),
            plan.Request.InstructionLimit - CountExecutedInstructions(events),
            operationalState.ObservedLogicalDepthHighWater,
            operationalState.ActiveFrameDepthHighWater,
            lineageStatus,
            lineageNodeCount,
            recordingMemory.ReachedObservations,
            recordingMemory.ReachedLoadOrdinals,
            operationalState.ModelAttempts,
            operationalState.ModelInvocationCount,
            operationalState.CompletedModeledCallCount,
            callTrace.ToImmutable(),
            events.ToImmutable(),
            []);
    }

    private static CounterfactualExecutionResult CreateBudgetExhausted(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualRecordingMemoryModel<TMemory> recordingMemory,
        MachineOperationalState operationalState,
        ImmutableArray<MethodHandle>.Builder callTrace,
        ImmutableArray<DebugEvent>.Builder events)
    {
        var used = CountExecutedInstructions(events);
        return CreateRootedResult(
            plan,
            EvaluationCompletionStatus.BudgetExhausted,
            used == 0 ? EvaluationCompleteness.None : EvaluationCompleteness.Partial,
            AggregateReachedEvidence(plan.Request, recordingMemory.ReachedObservations),
            used == 0 ? null : CounterfactualExecutionValue.CreateExecutionPrefix(),
            CounterfactualBoundStatus.Exhausted,
            used,
            0,
            operationalState.ObservedLogicalDepthHighWater,
            operationalState.ActiveFrameDepthHighWater,
            CounterfactualBoundStatus.NotReached,
            null,
            recordingMemory.ReachedObservations,
            recordingMemory.ReachedLoadOrdinals,
            operationalState.ModelAttempts,
            operationalState.ModelInvocationCount,
            operationalState.CompletedModeledCallCount,
            callTrace.ToImmutable(),
            events.ToImmutable(),
            [new EvaluationDiagnostic(
                "W4.Budget.Instruction",
                "The configured instruction-unit bound was exhausted at a ready machine boundary.")]);
    }

    private static CounterfactualExecutionResult CreateMachineFailure(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualRecordingMemoryModel<TMemory> recordingMemory,
        MachineOperationalState operationalState,
        ImmutableArray<MethodHandle>.Builder callTrace,
        ImmutableArray<DebugEvent>.Builder events,
        StepOutcome<ProvenanceConcreteValue, TMemory> outcome,
        EvaluationCompletionStatus completion)
    {
        var reached = recordingMemory.ReachedObservations;
        var evidence = completion == EvaluationCompletionStatus.Invalid
            ? EvaluationEvidenceStatus.Invalid
            : AggregateReachedEvidence(plan.Request, reached);
        var failure = outcome.Failure!;
        var effects = string.Equals(failure.Code, "W4.Model.EffectUnsupported", StringComparison.Ordinal)
            ? EvaluationEffectStatus.Unsupported
            : EvaluationEffectStatus.None;
        var diagnostic = CreateRuntimeFailureDiagnostic(completion, evidence, effects, operationalState);
        return CreateRootedResult(
            plan,
            completion,
            EvaluationCompleteness.None,
            evidence,
            null,
            CounterfactualBoundStatus.Applied,
            CountExecutedInstructions(events),
            plan.Request.InstructionLimit - CountExecutedInstructions(events),
            operationalState.ObservedLogicalDepthHighWater,
            operationalState.ActiveFrameDepthHighWater,
            CounterfactualBoundStatus.NotReached,
            null,
            reached,
            recordingMemory.ReachedLoadOrdinals,
            operationalState.ModelAttempts,
            operationalState.ModelInvocationCount,
            operationalState.CompletedModeledCallCount,
            callTrace.ToImmutable(),
            events.ToImmutable(),
            [diagnostic],
            effects);
    }

    private static EvaluationDiagnostic CreateRuntimeFailureDiagnostic(
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        MachineOperationalState operationalState)
    {
        if (effects == EvaluationEffectStatus.Unsupported)
        {
            return new EvaluationDiagnostic(
                "W4.Model.EffectUnsupported",
                "The entered call requires an effect unsupported by the read-only counterfactual profile.");
        }

        if (evidence == EvaluationEvidenceStatus.Conflict)
        {
            return new EvaluationDiagnostic(
                "W4.Evidence.FieldConflict",
                "A reached field observation conflicted with the frozen runtime evidence.");
        }

        if (!operationalState.ModelAttempts.IsEmpty &&
            operationalState.ModelAttempts[^1] is { TransferCompleted: false } attempt)
        {
            var code = attempt.StableCode!.StartsWith("W4.Model.", StringComparison.Ordinal)
                ? attempt.StableCode
                : attempt.OutcomeKind == PureModelAttemptOutcomeKind.Invalid
                    ? "W4.Model.Invalid"
                    : "W4.Model.Capability";
            return new EvaluationDiagnostic(
                code,
                completion == EvaluationCompletionStatus.Invalid
                    ? "The frozen pure-model attempt returned an invalid bounded outcome."
                    : "The frozen pure-model attempt could not complete one atomic caller transfer.");
        }

        return completion == EvaluationCompletionStatus.Invalid
            ? new EvaluationDiagnostic(
                "W4.Replay.ExecutionInvalid",
                "Prepared execution detected an invalid structural or runtime invariant.")
            : new EvaluationDiagnostic(
                "W4.Unknown.ExecutionBlocked",
                "Prepared execution could not continue through a required runtime capability.");
    }

    private static CounterfactualExecutionResult CreatePreactivationInvalid(
        CounterfactualMethodPlan<TMemory> plan,
        string code,
        string message) =>
        CreatePreactivationStopped(
            plan,
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            code,
            message);

    private static CounterfactualExecutionResult CreatePreactivationStopped(
        CounterfactualMethodPlan<TMemory> plan,
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence,
        string code,
        string message) =>
        CreateRootedResult(
            plan,
            completion,
            EvaluationCompleteness.None,
            evidence,
            null,
            CounterfactualBoundStatus.NotReached,
            null,
            null,
            null,
            null,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [],
            [],
            [new EvaluationDiagnostic(code, message)]);

    private static CounterfactualExecutionResult CreateExecutionStopped(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualRecordingMemoryModel<TMemory> recordingMemory,
        MachineOperationalState operationalState,
        ImmutableArray<MethodHandle>.Builder callTrace,
        ImmutableArray<DebugEvent>.Builder events,
        EvaluationCompletionStatus completion,
        string code,
        string message)
    {
        var used = CountExecutedInstructions(events);
        return CreateRootedResult(
            plan,
            completion,
            EvaluationCompleteness.None,
            AggregateReachedEvidence(plan.Request, recordingMemory.ReachedObservations),
            null,
            CounterfactualBoundStatus.Applied,
            used,
            plan.Request.InstructionLimit - used,
            operationalState.ObservedLogicalDepthHighWater,
            operationalState.ActiveFrameDepthHighWater,
            CounterfactualBoundStatus.NotReached,
            null,
            recordingMemory.ReachedObservations,
            recordingMemory.ReachedLoadOrdinals,
            operationalState.ModelAttempts,
            operationalState.ModelInvocationCount,
            operationalState.CompletedModeledCallCount,
            callTrace.ToImmutable(),
            events.ToImmutable(),
            [new EvaluationDiagnostic(code, message)]);
    }

    private static CounterfactualExecutionResult CreateExecutionInvalid(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualRecordingMemoryModel<TMemory> recordingMemory,
        MachineOperationalState operationalState,
        IEnumerable<MethodHandle> callTrace,
        IEnumerable<DebugEvent> events,
        string code,
        string message)
    {
        var frozenEvents = events.ToImmutableArray();
        var used = CountExecutedInstructions(frozenEvents);
        return CreateRootedResult(
            plan,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Invalid,
            null,
            CounterfactualBoundStatus.Applied,
            used,
            plan.Request.InstructionLimit - used,
            operationalState.ObservedLogicalDepthHighWater,
            operationalState.ActiveFrameDepthHighWater,
            CounterfactualBoundStatus.NotReached,
            null,
            recordingMemory.ReachedObservations,
            recordingMemory.ReachedLoadOrdinals,
            operationalState.ModelAttempts,
            operationalState.ModelInvocationCount,
            operationalState.CompletedModeledCallCount,
            callTrace.ToImmutableArray(),
            frozenEvents,
            [new EvaluationDiagnostic(code, message)]);
    }

    private static CounterfactualExecutionResult CreateRootedResult(
        CounterfactualMethodPlan<TMemory> plan,
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        CounterfactualExecutionValue? value,
        CounterfactualBoundStatus instructionStatus,
        long? instructionUsed,
        long? instructionRemaining,
        int? observedLogicalDepthHighWater,
        int? activeFrameDepthHighWater,
        CounterfactualBoundStatus lineageStatus,
        int? lineageNodeCount,
        ImmutableArray<CounterfactualFieldObservation> reachedFieldObservations,
        ImmutableArray<int> reachedFieldLoadOrdinals,
        ImmutableArray<PureModelAttempt> modelAttempts,
        int modelInvocationCount,
        int completedModeledCallCount,
        ImmutableArray<MethodHandle> callTrace,
        ImmutableArray<DebugEvent> events,
        ImmutableArray<EvaluationDiagnostic> diagnostics,
        EvaluationEffectStatus effects = EvaluationEffectStatus.None) =>
        CounterfactualExecutionResult.CreateRooted(
            plan,
            completion,
            completeness,
            evidence,
            effects,
            value,
            instructionStatus,
            instructionUsed,
            instructionRemaining,
            observedLogicalDepthHighWater,
            activeFrameDepthHighWater,
            lineageStatus,
            lineageNodeCount,
            reachedFieldObservations,
            reachedFieldLoadOrdinals,
            modelAttempts,
            modelInvocationCount,
            completedModeledCallCount,
            callTrace,
            events,
            RequestProvenance(plan.Request),
            diagnostics);

    private static EvaluationEvidenceStatus AggregateReachedEvidence(
        CounterfactualMethodRequest request,
        ImmutableArray<CounterfactualFieldObservation> observations)
    {
        var aggregate = EvaluationEvidenceStatus.Exact;
        foreach (var status in request.Arguments.Select(static item => item.EvidenceStatus)
            .Concat(observations.Select(static item => item.EvidenceStatus)))
        {
            if (EvidenceRank(status) > EvidenceRank(aggregate))
            {
                aggregate = status;
            }
        }

        return aggregate;
    }

    private static int EvidenceRank(EvaluationEvidenceStatus status) => status switch
    {
        EvaluationEvidenceStatus.Exact => 0,
        EvaluationEvidenceStatus.Partial => 1,
        EvaluationEvidenceStatus.Unavailable => 2,
        EvaluationEvidenceStatus.Conflict => 3,
        EvaluationEvidenceStatus.Invalid => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static ImmutableArray<DebugEvent> RemoveLastTransitionEvents(
        ImmutableArray<DebugEvent>.Builder events,
        int transitionEventCount)
    {
        if (transitionEventCount < 0 || transitionEventCount > events.Count)
        {
            return [];
        }

        return events.Take(events.Count - transitionEventCount).ToImmutableArray();
    }

    private static long CountExecutedInstructions(IEnumerable<DebugEvent> events) =>
        events.LongCount(static item => item.Kind == DebugEventKind.InstructionExecuted);

    private static bool HasSameMemoryIdentity(TMemory left, TMemory right) =>
        typeof(TMemory).IsValueType
            ? EqualityComparer<TMemory>.Default.Equals(left, right)
            : ReferenceEquals(left, right);

    private static long SaturatingAdd(long left, long right, long saturation)
    {
        if (left < 0 || right < 0 || saturation <= 0)
        {
            return 0;
        }

        if (left >= saturation || right >= saturation || left > saturation - right)
        {
            return saturation;
        }

        return left + right;
    }

    private static ProjectionFailure InvalidTransition(string suffix, string message) =>
        new($"W4.Replay.Transition{suffix}Invalid", message);

    private sealed record ProjectionFailure(string Code, string Message);

    private sealed class RejectingExecutionResolver : IResolutionServices
    {
        internal static readonly RejectingExecutionResolver Instance = new();

        private RejectingExecutionResolver()
        {
        }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
            ResolutionResult<ResolvedMethodDefinition>.Failed(
                ResolutionFailureKind.Invalid,
                "EXEC_PREPARED_RESOLUTION_FORBIDDEN",
                "Prepared execution cannot resolve a method definition.");

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken) =>
            ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Invalid,
                "EXEC_PREPARED_RESOLUTION_FORBIDDEN",
                "Prepared execution cannot resolve a method operand.");

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
            ResolutionResult<ResolvedField>.Failed(
                ResolutionFailureKind.Invalid,
                "EXEC_PREPARED_RESOLUTION_FORBIDDEN",
                "Prepared execution cannot resolve a field operand.");
    }

    internal static bool TryCalculateDynamicInstructionCost(
        FrozenMethodGraphPlan graph,
        long saturation,
        out long cost)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (saturation <= 0)
        {
            cost = 0;
            return false;
        }

        var colors = new Dictionary<MethodHandle, byte>();
        var memoized = new Dictionary<MethodHandle, long>();

        bool TryVisit(MethodHandle method, out long methodCost)
        {
            if (memoized.TryGetValue(method, out methodCost))
            {
                return true;
            }

            colors.TryGetValue(method, out var color);
            if (color == 1 || !graph.TryGetNode(method, out var node) || node is null ||
                !node.Admission.IsAdmitted || node.Admission.InstructionCount <= 0)
            {
                methodCost = 0;
                return false;
            }

            colors[method] = 1;
            methodCost = Math.Min(node.Admission.InstructionCount, saturation);
            foreach (var call in node.CallSites)
            {
                if (call.Caller != method || call.Effects != EvaluationEffectStatus.None)
                {
                    methodCost = 0;
                    return false;
                }

                if (call.Disposition == FrozenMethodCallDisposition.PureModel)
                {
                    if (!graph.TryGetModeledLeaf(call.Target.Method, out var leaf) || leaf is null)
                    {
                        methodCost = 0;
                        return false;
                    }

                    continue;
                }

                if (call.Disposition != FrozenMethodCallDisposition.Interpreted ||
                    !TryVisit(call.Target.Method, out var calleeCost))
                {
                    methodCost = 0;
                    return false;
                }

                methodCost = SaturatingAdd(methodCost, calleeCost, saturation);
            }

            colors[method] = 2;
            memoized[method] = methodCost;
            return true;
        }

        if (!TryVisit(graph.Root, out cost) ||
            graph.Nodes.Any(node => !memoized.ContainsKey(node.Method)) ||
            graph.ModeledLeaves.Any(leaf => graph.Nodes.Any(node => node.Method == leaf.Method)))
        {
            cost = 0;
            return false;
        }

        return true;
    }

    private static bool TryValidateBindings(
        CounterfactualMethodPreparationCandidate<TMemory> candidate,
        CounterfactualMethodRequest request,
        out string code,
        out string message)
    {
        if (candidate.Resolver is null || candidate.Domain is null || candidate.MemoryModel is null ||
            candidate.InitialMemory is null || candidate.Receiver is null)
        {
            code = "W4.Request.BindingMissing";
            message = "Resolver, domain, memory, initial snapshot, and exact receiver bindings are required.";
            return false;
        }

        if (candidate.Domain.InternedNodeCount != 0)
        {
            code = "W4.Request.DomainNotFresh";
            message = "Preparation requires a fresh provenance domain without unrelated lineage nodes.";
            return false;
        }

        var receiver = candidate.Receiver;
        if (receiver.SemanticValue.Kind != ConcreteValueKind.ObjectReference ||
            receiver.SemanticValue.StaticType != request.Receiver.StaticType ||
            !receiver.SemanticValue.TryGetReferenceId(out var referenceId) ||
            referenceId <= 0 ||
            receiver.TryGetLineageRoot(out _))
        {
            code = "W4.Request.ReceiverBinding";
            message = "The operational receiver must be one exact non-null reference of the request receiver type.";
            return false;
        }

        if (request.RequiredModelTarget.HasValue && candidate.ModelRegistry is null)
        {
            code = "W4.Request.ModelRegistryMissing";
            message = "A request with a required modeled target must bind one structural model registry.";
            return false;
        }

        try
        {
            _ = CounterfactualMethodExecutionInput<TMemory>.ValidateObservationBinding(
                request,
                candidate.FieldObservations);
        }
        catch (ArgumentException)
        {
            code = "W4.Evidence.FieldObservationInvalid";
            message = "The field-observation binding is malformed or is not correlated to the request receiver.";
            return false;
        }

        code = null!;
        message = null!;
        return true;
    }

    private static bool HasCompatibleRootSignature(
        CounterfactualMethodRequest request,
        FrozenMethodGraphPlan graph)
    {
        if (!graph.TryGetNode(graph.Root, out var rootNode) || rootNode is null)
        {
            return false;
        }

        var signature = rootNode.Definition.Signature;
        return graph.Root == request.RootMethod &&
            request.Arguments.IsEmpty &&
            signature.CallingConvention == MethodCallingConventionKind.Default &&
            signature.HasImplicitThis &&
            !signature.HasExplicitThis &&
            signature.GenericParameterCount == 0 &&
            signature.DeclaringType == request.Receiver.StaticType &&
            signature.ReturnType == TypeSig.Int32 &&
            signature.ParameterTypes.SequenceEqual(request.Arguments.Select(static argument => argument.StaticType));
    }

    private static void ValidateObservations(
        CounterfactualMethodRequest request,
        FrozenMethodGraphPlan graph,
        ImmutableArray<CounterfactualFieldObservation> observations)
    {
        var copied = CounterfactualMethodExecutionInput<TMemory>.ValidateObservationBinding(request, observations);
        if (copied.Length != graph.Fields.Length)
        {
            throw new ArgumentException("The field-observation catalog is incomplete.", nameof(observations));
        }

        for (var index = 0; index < copied.Length; index++)
        {
            if (copied[index].DependencyOrdinal != index ||
                copied[index].Field != graph.Fields[index] ||
                copied[index].Field.DeclaringType != request.Receiver.StaticType)
            {
                throw new ArgumentException(
                    "Field observations must exactly follow the frozen graph's dependency order.",
                    nameof(observations));
            }
        }
    }

    private static CounterfactualMethodPreparationResult<TMemory> GraphFailure(
        CounterfactualMethodRequest request,
        MethodGraphPreparationResult graphPreparation)
    {
        var coreFailure = graphPreparation.Failure;
        var completion = graphPreparation.Status switch
        {
            MachineRunStatus.BudgetExhausted => EvaluationCompletionStatus.BudgetExhausted,
            MachineRunStatus.Blocked => EvaluationCompletionStatus.Blocked,
            MachineRunStatus.InvalidProgram => EvaluationCompletionStatus.Invalid,
            _ => EvaluationCompletionStatus.Invalid,
        };
        var stageEvidence = GraphFailureEvidence(graphPreparation.Status, coreFailure);
        var effects = string.Equals(
            coreFailure?.Code,
            "W4.Model.EffectUnsupported",
            StringComparison.Ordinal)
            ? EvaluationEffectStatus.Unsupported
            : EvaluationEffectStatus.None;
        var (code, message) = GraphDiagnostic(graphPreparation.Status, coreFailure);

        return Failure(
            completion,
            stageEvidence,
            effects,
            CreateContext(request, includeTraversal: true, includeLogicalDepth: false),
            request,
            graphPreparation.TraversalAccounting,
            NormalizeCoreFailure(coreFailure, code, message),
            code,
            message);
    }

    private static EvaluationEvidenceStatus GraphFailureEvidence(
        MachineRunStatus status,
        ExecutionFailure? failure)
    {
        if (status == MachineRunStatus.InvalidProgram)
        {
            return EvaluationEvidenceStatus.Invalid;
        }

        if (failure?.ResolutionFailure?.Kind == ResolutionFailureKind.Conflict)
        {
            return EvaluationEvidenceStatus.Conflict;
        }

        if (failure?.ResolutionFailure?.Kind == ResolutionFailureKind.Unavailable)
        {
            return EvaluationEvidenceStatus.Unavailable;
        }

        return EvaluationEvidenceStatus.Exact;
    }

    private static (string Code, string Message) GraphDiagnostic(
        MachineRunStatus status,
        ExecutionFailure? failure)
    {
        if (status == MachineRunStatus.BudgetExhausted)
        {
            return ("W4.Budget.Traversal", "The graph-preparation traversal bound was exhausted.");
        }

        if (status == MachineRunStatus.InvalidProgram)
        {
            return ("W4.Evidence.ProgramInvalid", "Graph preparation found invalid structural evidence.");
        }

        if (failure?.ResolutionFailure?.Kind == ResolutionFailureKind.Conflict)
        {
            return ("W4.Evidence.ProgramConflict", "Graph preparation encountered conflicting structural evidence.");
        }

        if (failure?.ResolutionFailure?.Kind == ResolutionFailureKind.Unavailable)
        {
            return ("W4.Evidence.ProgramUnavailable", "Graph preparation requires structural evidence that is unavailable.");
        }

        if (failure?.Code == "W4.Model.EffectUnsupported")
        {
            return ("W4.Model.EffectUnsupported", "The selected model declares an effect this read-only slice cannot represent.");
        }

        return ("W4.Admission.Unsupported", "The exact graph is outside the admitted counterfactual execution slice.");
    }

    private static bool IsCoherentPlannerSuccess(
        CounterfactualMethodRequest request,
        MethodGraphPreparationResult preparation)
    {
        var graph = preparation.Plan;
        var accounting = preparation.TraversalAccounting;
        return graph is not null &&
            preparation.Status == MachineRunStatus.Ready &&
            preparation.Failure is null &&
            accounting is not null &&
            accounting.Limit == request.TraversalLimit &&
            !accounting.IsExhausted &&
            accounting.RejectedCharge is null &&
            accounting.Used == graph.TraversalUnitCount &&
            accounting.Remaining == accounting.Limit - accounting.Used;
    }

    private static bool IsCoherentPlannerFailure(
        CounterfactualMethodRequest request,
        MethodGraphPreparationResult preparation)
    {
        var accounting = preparation.TraversalAccounting;
        var failure = preparation.Failure;
        if (preparation.Plan is not null || failure is null || accounting is null ||
            accounting.Limit != request.TraversalLimit ||
            accounting.Remaining != accounting.Limit - accounting.Used ||
            (preparation.Status == MachineRunStatus.BudgetExhausted) != accounting.IsExhausted)
        {
            return false;
        }

        return preparation.Status switch
        {
            MachineRunStatus.BudgetExhausted =>
                failure.Kind == ExecutionFailureKind.ResourceLimit &&
                string.Equals(failure.Code, "W4.Budget.Traversal", StringComparison.Ordinal),
            MachineRunStatus.Blocked => failure.ResolutionFailure?.Kind != ResolutionFailureKind.Invalid,
            MachineRunStatus.InvalidProgram => true,
            _ => false,
        };
    }

    private static CounterfactualMethodPreparationResult<TMemory> PlannerResultInvalid(
        CounterfactualMethodRequest request,
        MethodGraphTraversalAccounting? accounting) =>
        Failure(
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            EvaluationEffectStatus.None,
            CreateContext(request, includeTraversal: true, includeLogicalDepth: false),
            request,
            accounting,
            coreFailure: null,
            "W4.Admission.PlannerResultInvalid",
            "The graph planner returned an incoherent plan, status, failure, or traversal-accounting tuple.");

    private static ExecutionFailure? NormalizeCoreFailure(
        ExecutionFailure? failure,
        string productCode,
        string productMessage)
    {
        if (failure is null)
        {
            return null;
        }

        var resolutionFailure = failure.ResolutionFailure is { } resolution
            ? new ResolutionFailure(resolution.Kind, productCode, productMessage)
            : null;
        return new ExecutionFailure(
            failure.Kind,
            productCode,
            productMessage,
            failure.Method,
            failure.IlOffset,
            resolutionFailure);
    }

    private static EvaluationEvidenceContext CreateContext(
        CounterfactualMethodRequest request,
        bool includeTraversal,
        bool includeLogicalDepth)
    {
        var bounds = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>(2);
        if (includeTraversal)
        {
            bounds.Add(new EvaluationDeterministicBound(TraversalBoundName, request.TraversalLimit));
        }

        if (includeLogicalDepth)
        {
            bounds.Add(new EvaluationDeterministicBound(LogicalDepthBoundName, request.LogicalDepthLimit));
        }

        return EvaluationEvidenceContext.Create(
            request.EvidenceSource,
            request.SnapshotIdentity,
            request.ModuleIdentity,
            EvaluationFallback.None,
            bounds.ToImmutable());
    }

    private static ImmutableArray<EvaluationProvenance> RequestProvenance(CounterfactualMethodRequest request) =>
        ImmutableArray.Create(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"counterfactual-request-sha256:{request.Sha256}"));

    private static CounterfactualMethodPreparationResult<TMemory> RawFailure(string code, string message) =>
        CounterfactualMethodPreparationResult<TMemory>.Failed(new CounterfactualMethodPreparationFailure(
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            EvaluationEffectStatus.None,
            EvaluationEvidenceContext.Neutral,
            requestSha256: null,
            traversalAccounting: null,
            coreFailure: null,
            provenance: ImmutableArray<EvaluationProvenance>.Empty,
            diagnostics: ImmutableArray.Create(new EvaluationDiagnostic(code, message))));

    private static CounterfactualMethodPreparationResult<TMemory> Failure(
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        EvaluationEvidenceContext context,
        CounterfactualMethodRequest request,
        MethodGraphTraversalAccounting? traversalAccounting,
        ExecutionFailure? coreFailure,
        string code,
        string message) =>
        CounterfactualMethodPreparationResult<TMemory>.Failed(new CounterfactualMethodPreparationFailure(
            completion,
            evidence,
            effects,
            context,
            request.Sha256,
            traversalAccounting,
            coreFailure,
            RequestProvenance(request),
            ImmutableArray.Create(new EvaluationDiagnostic(code, message))));

    private static bool IsOrdinaryFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or OverflowException;
}
