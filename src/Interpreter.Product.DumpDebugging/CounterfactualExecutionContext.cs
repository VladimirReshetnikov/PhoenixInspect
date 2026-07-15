using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Carries the immutable structural, evidence, accounting, and transcript context for one counterfactual result.
/// </summary>
/// <remarks>
/// The context is deliberately observation-only: it exposes no resolver, model registry, value-domain capability,
/// memory model, persistent memory object, machine state, issuer token, or cancellation source. Rooted instances are
/// issued only from one canonical plan, so callers cannot independently substitute request, plan, accounting, or
/// structural projections. Standalone and pre-plan facade-rejection instances remain explicitly identity-free.
/// This is an unstable draft W4 projection and requires a schema revision before its admitted facts change.
/// </remarks>
public sealed class CounterfactualExecutionContext
{
    internal const string InstructionBoundName = "counterfactual.execution.instruction-units";
    internal const string LineageBoundName = "counterfactual.execution.lineage-nodes";
    internal const string LogicalDepthBoundName = "counterfactual.execution.logical-call-depth";
    internal const string TraversalBoundName = "counterfactual.preparation.traversal-units";

    private readonly ImmutableArray<MethodHandle> interpretedMethods;
    private readonly ImmutableArray<MethodHandle> modeledMethods;
    private readonly ImmutableArray<ResolvedField> plannedFields;
    private readonly ImmutableArray<CounterfactualPlanCallSite> callDispositions;
    private readonly ImmutableArray<CounterfactualFieldObservation> reachedFieldObservations;
    private readonly ImmutableArray<int> reachedFieldLoadOrdinals;
    private readonly ImmutableArray<PureModelAttempt> modelAttempts;
    private readonly ImmutableArray<MethodHandle> callTrace;
    private readonly ImmutableArray<DebugEvent> events;

    private CounterfactualExecutionContext(
        CounterfactualExecutionOriginKind origin,
        CounterfactualMethodRequest? request,
        int? planSchemaVersion,
        string? planSha256,
        EvaluationEvidenceContext evidenceContext,
        CounterfactualExecutionAccounting accounting,
        MethodHandle? rootMethod,
        ImmutableArray<MethodHandle> interpretedMethods,
        ImmutableArray<MethodHandle> modeledMethods,
        ImmutableArray<ResolvedField> plannedFields,
        ImmutableArray<CounterfactualPlanCallSite> callDispositions,
        ImmutableArray<CounterfactualFieldObservation> reachedFieldObservations,
        ImmutableArray<int> reachedFieldLoadOrdinals,
        ImmutableArray<PureModelAttempt> modelAttempts,
        int modelInvocationCount,
        int completedModeledCallCount,
        ImmutableArray<MethodHandle> callTrace,
        ImmutableArray<DebugEvent> events,
        EvaluationEvidenceStatus reachedEvidence,
        int executedInstructionCount,
        int activeFrameDepthAtEnd)
    {
        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        ArgumentNullException.ThrowIfNull(evidenceContext);
        ArgumentNullException.ThrowIfNull(accounting);
        if (!Enum.IsDefined(reachedEvidence) || executedInstructionCount < 0 || activeFrameDepthAtEnd < 0)
        {
            throw new ArgumentException("The derived transcript facts are malformed.");
        }

        Origin = origin;
        Request = request;
        PlanSchemaVersion = planSchemaVersion;
        PlanSha256 = planSha256;
        EvidenceContext = evidenceContext;
        Accounting = accounting;
        RootMethod = rootMethod;
        this.interpretedMethods = CopyAndRejectNull(interpretedMethods, nameof(interpretedMethods));
        this.modeledMethods = CopyAndRejectNull(modeledMethods, nameof(modeledMethods));
        this.plannedFields = CopyAndRejectNull(plannedFields, nameof(plannedFields));
        this.callDispositions = CopyAndRejectNull(callDispositions, nameof(callDispositions));
        this.reachedFieldObservations = CopyAndRejectNull(
            reachedFieldObservations,
            nameof(reachedFieldObservations));
        this.reachedFieldLoadOrdinals = CounterfactualCanonical.Copy(reachedFieldLoadOrdinals);
        this.modelAttempts = CopyAndRejectNull(modelAttempts, nameof(modelAttempts));
        ModelInvocationCount = modelInvocationCount;
        CompletedModeledCallCount = completedModeledCallCount;
        this.callTrace = CopyAndRejectNull(callTrace, nameof(callTrace));
        this.events = CopyAndRejectNull(events, nameof(events));
        ReachedEvidence = reachedEvidence;
        ExecutedInstructionCount = executedInstructionCount;
        ActiveFrameDepthAtEnd = activeFrameDepthAtEnd;
    }

    /// <summary>Gets whether this is a rooted result, standalone W4.7 projection, or identity-free facade rejection.</summary>
    public CounterfactualExecutionOriginKind Origin { get; }

    /// <summary>Gets the canonical rooted request, or <see langword="null"/> when no rooted plan exists.</summary>
    public CounterfactualMethodRequest? Request { get; }

    /// <summary>Gets the rooted plan schema version, or <see langword="null"/> when no rooted plan exists.</summary>
    public int? PlanSchemaVersion { get; }

    /// <summary>Gets the rooted plan SHA-256 identity, or <see langword="null"/> when no rooted plan exists.</summary>
    public string? PlanSha256 { get; }

    /// <summary>Gets the backend-neutral source, identity, fallback, and exactly applied-bound context.</summary>
    public EvaluationEvidenceContext EvidenceContext { get; }

    /// <summary>Gets explicit instruction, traversal, depth, lineage, and allocation accounting.</summary>
    public CounterfactualExecutionAccounting Accounting { get; }

    /// <summary>Gets the rooted MethodDef, or <see langword="null"/> when no rooted plan exists.</summary>
    public MethodHandle? RootMethod { get; }

    /// <summary>Gets a defensive copy of interpreted MethodDefs in frozen structural order.</summary>
    public ImmutableArray<MethodHandle> InterpretedMethods => CounterfactualCanonical.Copy(interpretedMethods);

    /// <summary>Gets a defensive copy of modeled MethodDefs in frozen structural order.</summary>
    public ImmutableArray<MethodHandle> ModeledMethods => CounterfactualCanonical.Copy(modeledMethods);

    /// <summary>Gets a defensive copy of planned structural field dependencies.</summary>
    public ImmutableArray<ResolvedField> PlannedFields => CounterfactualCanonical.Copy(plannedFields);

    /// <summary>Gets a defensive copy of frozen direct-call disposition projections.</summary>
    public ImmutableArray<CounterfactualPlanCallSite> CallDispositions =>
        CounterfactualCanonical.Copy(callDispositions);

    /// <summary>Gets a defensive copy of canonical plan observations actually reached by field loads.</summary>
    public ImmutableArray<CounterfactualFieldObservation> ReachedFieldObservations =>
        CounterfactualCanonical.Copy(reachedFieldObservations);

    /// <summary>Gets a defensive copy of reached field dependency ordinals in temporal load order.</summary>
    public ImmutableArray<int> ReachedFieldLoadOrdinals =>
        CounterfactualCanonical.Copy(reachedFieldLoadOrdinals);

    /// <summary>Gets a defensive copy of frozen-model invocation attempts in execution order.</summary>
    public ImmutableArray<PureModelAttempt> ModelAttempts => CounterfactualCanonical.Copy(modelAttempts);

    /// <summary>Gets the monotonic number of frozen-model capability invocations.</summary>
    public int ModelInvocationCount { get; }

    /// <summary>Gets the number of model attempts that atomically completed caller transfer.</summary>
    public int CompletedModeledCallCount { get; }

    /// <summary>
    /// Gets a defensive copy of the chronological dynamic invocation trace: the root followed by every entered
    /// interpreted or modeled call, with repeated methods retained.
    /// </summary>
    public ImmutableArray<MethodHandle> CallTrace => CounterfactualCanonical.Copy(callTrace);

    /// <summary>Gets a defensive copy of the bounded semantic event transcript.</summary>
    public ImmutableArray<DebugEvent> Events => CounterfactualCanonical.Copy(events);

    internal EvaluationEvidenceStatus ReachedEvidence { get; }

    internal int ExecutedInstructionCount { get; }

    internal int ActiveFrameDepthAtEnd { get; }

    internal static CounterfactualExecutionContext CreateRooted<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualExecutionAccounting accounting,
        ImmutableArray<CounterfactualFieldObservation> reachedFieldObservations,
        ImmutableArray<int> reachedFieldLoadOrdinals,
        ImmutableArray<PureModelAttempt> modelAttempts,
        int modelInvocationCount,
        int completedModeledCallCount,
        ImmutableArray<MethodHandle> callTrace,
        ImmutableArray<DebugEvent> events,
        EvaluationCompletionStatus completion = EvaluationCompletionStatus.Completed)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(accounting);
        if (!Enum.IsDefined(completion))
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        ValidateCanonicalPlan(plan);
        ValidateAccounting(plan, accounting);

        var observations = CopyAndRejectNull(reachedFieldObservations, nameof(reachedFieldObservations));
        var ordinals = CounterfactualCanonical.Copy(reachedFieldLoadOrdinals);
        var attempts = CopyAndRejectNull(modelAttempts, nameof(modelAttempts));
        var trace = CopyAndRejectNull(callTrace, nameof(callTrace));
        var copiedEvents = CopyAndRejectNull(events, nameof(events));
        ValidateCounters(attempts, modelInvocationCount, completedModeledCallCount);
        var transcript = ValidateRootedTranscript(
            plan,
            accounting,
            observations,
            ordinals,
            attempts,
            trace,
            copiedEvents,
            completion);
        var reachedEvidence = AggregateReachedEvidence(plan.Request, observations);
        var evidenceContext = CreateRootedEvidenceContext(plan.Request, accounting);

        return new CounterfactualExecutionContext(
            CounterfactualExecutionOriginKind.RootedFacade,
            plan.Request,
            plan.SchemaVersion,
            plan.Sha256,
            evidenceContext,
            accounting,
            plan.RootMethod,
            plan.InterpretedMethods,
            plan.ModeledMethods,
            plan.Fields,
            plan.CallSites,
            observations,
            ordinals,
            attempts,
            modelInvocationCount,
            completedModeledCallCount,
            trace,
            copiedEvents,
            reachedEvidence,
            transcript.ExecutedInstructions,
            transcript.ActiveFramesAtEnd);
    }

    internal static CounterfactualExecutionContext CreateStandaloneTargetOutcome(
        CounterfactualTargetOutcomeFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (!CounterfactualExecutionValue.IsCanonicalTargetOutcome(fragment))
        {
            throw new ArgumentException("A canonical W4.7 fragment is required.", nameof(fragment));
        }

        var accounting = CounterfactualExecutionAccounting.CreateStandaloneTargetOutcome(fragment);
        return new CounterfactualExecutionContext(
            CounterfactualExecutionOriginKind.StandaloneTargetOutcome,
            null,
            null,
            null,
            EvaluationEvidenceContext.Neutral,
            accounting,
            null,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            0,
            0,
            fragment.CallTrace,
            fragment.Events,
            EvaluationEvidenceStatus.Exact,
            checked((int)fragment.UsedInstructionUnits),
            0);
    }

    internal static CounterfactualExecutionContext CreateFacadeRejection() =>
        new(
            CounterfactualExecutionOriginKind.FacadeRejection,
            null,
            null,
            null,
            EvaluationEvidenceContext.Neutral,
            CounterfactualExecutionAccounting.CreateFacadeRejection(),
            null,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            0,
            0,
            [],
            [],
            EvaluationEvidenceStatus.Exact,
            0,
            0);

    private static EvaluationEvidenceContext CreateRootedEvidenceContext(
        CounterfactualMethodRequest request,
        CounterfactualExecutionAccounting accounting)
    {
        var bounds = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>(4);
        if (accounting.InstructionStatus is CounterfactualBoundStatus.Applied or CounterfactualBoundStatus.Exhausted)
        {
            bounds.Add(new EvaluationDeterministicBound(InstructionBoundName, request.InstructionLimit));
        }

        bounds.Add(new EvaluationDeterministicBound(TraversalBoundName, request.TraversalLimit));
        if (accounting.DepthStatus is CounterfactualBoundStatus.Applied or CounterfactualBoundStatus.Exhausted)
        {
            bounds.Add(new EvaluationDeterministicBound(LogicalDepthBoundName, request.LogicalDepthLimit));
        }

        if (accounting.LineageStatus is CounterfactualBoundStatus.Applied or CounterfactualBoundStatus.Exhausted)
        {
            bounds.Add(new EvaluationDeterministicBound(LineageBoundName, request.LineageNodeCeiling));
        }

        return EvaluationEvidenceContext.Create(
            request.EvidenceSource,
            request.SnapshotIdentity,
            request.ModuleIdentity,
            EvaluationFallback.None,
            bounds.ToImmutable());
    }

    private static void ValidateCanonicalPlan<TMemory>(CounterfactualMethodPlan<TMemory> plan)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        var requestBytes = plan.Request.CanonicalBytes;
        var planBytes = plan.CanonicalBytes;
        if (plan.SchemaVersion != CounterfactualMethodPlan<TMemory>.CanonicalSchemaVersion ||
            plan.Request.SchemaVersion != CounterfactualMethodRequest.CanonicalSchemaVersion ||
            plan.RootMethod != plan.Request.RootMethod ||
            !string.Equals(CounterfactualCanonical.Hash(requestBytes.AsSpan()), plan.Request.Sha256, StringComparison.Ordinal) ||
            !string.Equals(CounterfactualCanonical.Hash(planBytes.AsSpan()), plan.Sha256, StringComparison.Ordinal))
        {
            throw new ArgumentException("A rooted result requires one intact canonical request and issued plan.", nameof(plan));
        }
    }

    private static void ValidateAccounting<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualExecutionAccounting accounting)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        var request = plan.Request;
        if (accounting.InstructionStatus == CounterfactualBoundStatus.NotApplicable ||
            accounting.InstructionLimit != request.InstructionLimit ||
            accounting.TraversalStatus != CounterfactualBoundStatus.Applied ||
            accounting.TraversalLimit != plan.TraversalLimit ||
            accounting.TraversalUsed != plan.TraversalUsed ||
            accounting.TraversalRemaining != plan.TraversalRemaining ||
            !accounting.TraversalCharges.SequenceEqual(plan.TraversalCharges) ||
            accounting.RejectedTraversalCharge is not null ||
            accounting.DepthStatus is CounterfactualBoundStatus.NotApplicable or CounterfactualBoundStatus.Exhausted ||
            accounting.LogicalDepthLimit != request.LogicalDepthLimit ||
            accounting.RequiredLogicalDepth != plan.RequiredLogicalDepth ||
            accounting.LineageStatus == CounterfactualBoundStatus.NotApplicable ||
            accounting.LineageNodeCeiling != request.LineageNodeCeiling ||
            accounting.AllocationStatus != CounterfactualBoundStatus.NotApplicable)
        {
            throw new ArgumentException("Rooted accounting must derive every configured and prepared fact from its plan.", nameof(accounting));
        }
    }

    private static TranscriptFacts ValidateRootedTranscript<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualExecutionAccounting accounting,
        ImmutableArray<CounterfactualFieldObservation> observations,
        ImmutableArray<int> loadOrdinals,
        ImmutableArray<PureModelAttempt> attempts,
        ImmutableArray<MethodHandle> trace,
        ImmutableArray<DebugEvent> events,
        EvaluationCompletionStatus completion)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        ValidateReachedObservations(plan, observations, loadOrdinals, events, completion);
        ValidateModelAttempts(plan, accounting, attempts, events);
        var eventFacts = ValidateEvents(plan, events, !trace.IsEmpty);

        if (accounting.InstructionStatus == CounterfactualBoundStatus.NotReached)
        {
            Require(eventFacts.ExecutedInstructions == 0, "An unreached instruction bound cannot retain execution events.");
        }
        else
        {
            Require(accounting.InstructionUsed == eventFacts.ExecutedInstructions,
                "Instruction accounting must equal the executed or terminal-event count.");
        }

        if (trace.IsEmpty)
        {
            Require(observations.IsEmpty && loadOrdinals.IsEmpty && attempts.IsEmpty && events.IsEmpty,
                "An empty dynamic call trace cannot retain execution transcript entries.");
            Require(accounting.DepthStatus == CounterfactualBoundStatus.NotReached,
                "An empty dynamic call trace requires unreached depth accounting.");
            return new TranscriptFacts(eventFacts.ExecutedInstructions, 0);
        }

        Require(trace[0] == plan.RootMethod && trace.Skip(1).All(method => method != plan.RootMethod),
            "A dynamic call trace must begin with the root exactly once.");
        var interpreted = plan.InterpretedMethods;
        var modeled = plan.ModeledMethods;
        Require(trace.All(method => interpreted.Contains(method) || modeled.Contains(method)),
            "Every dynamic call-trace entry must belong to the frozen graph.");

        var expectedInterpreted = ImmutableArray.CreateBuilder<MethodHandle>();
        expectedInterpreted.Add(plan.RootMethod);
        expectedInterpreted.AddRange(events
            .Where(static item => item.Kind == DebugEventKind.FramePushed)
            .Select(static item => item.Method));
        Require(trace.Where(interpreted.Contains).SequenceEqual(expectedInterpreted),
            "Interpreted call-trace entries must exactly follow frame-entry events.");
        Require(trace.Where(modeled.Contains).SequenceEqual(attempts.Select(static item => item.CallSite.Callee)),
            "Modeled call-trace entries must exactly follow the model-attempt transcript.");

        Require(accounting.DepthStatus == CounterfactualBoundStatus.Applied,
            "A nonempty dynamic call trace requires applied depth accounting.");
        var logicalHighWater = Math.Max(eventFacts.ActiveFrameHighWater,
            attempts.IsEmpty ? 1 : attempts.Max(static item => item.EnteredLogicalDepth));
        Require(accounting.ActiveFrameDepthHighWater == eventFacts.ActiveFrameHighWater &&
            accounting.ObservedLogicalDepthHighWater == logicalHighWater,
            "Depth high-water facts must exactly match frame and modeled-boundary transcripts.");
        return new TranscriptFacts(eventFacts.ExecutedInstructions, eventFacts.ActiveFramesAtEnd);
    }

    private static void ValidateReachedObservations<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        ImmutableArray<CounterfactualFieldObservation> observations,
        ImmutableArray<int> loadOrdinals,
        ImmutableArray<DebugEvent> events,
        EvaluationCompletionStatus completion)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        var planObservations = plan.FieldObservations;
        var previous = -1;
        foreach (var observation in observations)
        {
            Require(observation.DependencyOrdinal > previous && observation.DependencyOrdinal < planObservations.Length,
                "Reached observations must use strictly increasing plan-relative ordinals.");
            var expected = planObservations[observation.DependencyOrdinal];
            Require(observation.Equals(expected) &&
                string.Equals(CounterfactualCanonical.Hash(observation.CanonicalBytes.AsSpan()), observation.Sha256, StringComparison.Ordinal),
                "A reached observation must be the intact canonical observation frozen by the plan.");
            previous = observation.DependencyOrdinal;
        }

        Require(loadOrdinals.All(ordinal => observations.Any(item => item.DependencyOrdinal == ordinal)),
            "Every temporal field-load ordinal must identify one retained reached observation.");
        Require(observations.All(item => loadOrdinals.Contains(item.DependencyOrdinal)),
            "Every retained reached observation must occur in the temporal field-load vector.");

        var observationByOrdinal = observations.ToDictionary(static item => item.DependencyOrdinal);
        var successfulLoads = loadOrdinals.Count(ordinal => observationByOrdinal[ordinal].EvidenceStatus is
            EvaluationEvidenceStatus.Exact or EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable);
        var executedLoads = events.Count(static item =>
            item.Kind == DebugEventKind.InstructionExecuted &&
            string.Equals(item.Instruction, "LoadField", StringComparison.Ordinal));
        var hasFinalNontransferringLoad = successfulLoads == executedLoads + 1 &&
            completion is EvaluationCompletionStatus.Blocked or EvaluationCompletionStatus.Invalid &&
            !loadOrdinals.IsEmpty &&
            observationByOrdinal[loadOrdinals[^1]].EvidenceStatus is
                EvaluationEvidenceStatus.Exact or EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable;
        Require(successfulLoads == executedLoads || hasFinalNontransferringLoad,
            "Successful reached field observations must match executed loads, except for one final atomic failure.");

        var transferredLoadOrdinals = hasFinalNontransferringLoad
            ? loadOrdinals.Take(loadOrdinals.Length - 1).ToImmutableArray()
            : loadOrdinals;

        var precisionEvidence = events
            .Where(static item => item.Kind == DebugEventKind.ValuePrecisionLost)
            .Select(static item => item.FieldEvidence!.Sha256)
            .ToArray();
        var expectedPrecision = transferredLoadOrdinals
            .Select(ordinal => observationByOrdinal[ordinal])
            .Where(static item => item.EvidenceStatus is EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable)
            .Select(static item => item.ApproximationEvidenceSha256!)
            .ToArray();
        Require(precisionEvidence.SequenceEqual(expectedPrecision),
            "Precision-loss events must exactly follow reached partial or unavailable observations.");

        var terminalEvidence = loadOrdinals
            .Select(ordinal => observationByOrdinal[ordinal])
            .Where(static item => item.EvidenceStatus is EvaluationEvidenceStatus.Conflict or EvaluationEvidenceStatus.Invalid)
            .ToArray();
        Require(terminalEvidence.Length <= 1 &&
            (terminalEvidence.Length == 0 || terminalEvidence[0].DependencyOrdinal == loadOrdinals[^1]),
            "Conflict or invalid field evidence must terminate at the final reached load.");
    }

    private static void ValidateModelAttempts<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        CounterfactualExecutionAccounting accounting,
        ImmutableArray<PureModelAttempt> attempts,
        ImmutableArray<DebugEvent> events)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        for (var index = 0; index < attempts.Length; index++)
        {
            var attempt = attempts[index];
            var call = plan.CallSites.SingleOrDefault(item =>
                item.Disposition == FrozenMethodCallDisposition.PureModel &&
                item.Caller == attempt.CallSite.Caller &&
                item.IlOffset == attempt.CallSite.CallIlOffset &&
                item.TargetMethod == attempt.CallSite.Callee);
            Require(call is not null &&
                string.Equals(call.ModelId, attempt.ModelIdentity.StableId, StringComparison.Ordinal) &&
                call.ModelVersion == attempt.ModelIdentity.Version &&
                call.Effects == EvaluationEffectStatus.None,
                "Every model attempt must exactly match one frozen modeled call disposition.");
            Require(attempt.EnteredLogicalDepth <= plan.RequiredLogicalDepth,
                "A model attempt cannot enter beyond the frozen graph's required logical depth.");
            Require(attempt.TransferCompleted || index == attempts.Length - 1,
                "A nontransferring model attempt must terminate the attempt transcript.");
        }

        foreach (var call in plan.CallSites.Where(static item => item.Disposition == FrozenMethodCallDisposition.PureModel))
        {
            var completed = attempts.Count(item => item.TransferCompleted &&
                item.CallSite.Caller == call.Caller &&
                item.CallSite.CallIlOffset == call.IlOffset &&
                item.CallSite.Callee == call.TargetMethod);
            var executed = events.Count(item =>
                item.Kind == DebugEventKind.InstructionExecuted &&
                item.Method == call.Caller &&
                item.IlOffset == call.IlOffset &&
                string.Equals(item.Instruction, "Call", StringComparison.Ordinal));
            Require(completed == executed,
                "Completed modeled attempts must exactly match executed modeled-call events.");
        }

        if (!attempts.IsEmpty)
        {
            Require(accounting.DepthStatus == CounterfactualBoundStatus.Applied &&
                accounting.ObservedLogicalDepthHighWater >= attempts.Max(static item => item.EnteredLogicalDepth),
                "Model attempts require truthful applied logical-depth accounting.");
        }
    }

    private static EventFacts ValidateEvents<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        ImmutableArray<DebugEvent> events,
        bool hasActivatedRoot)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        var interpreted = plan.InterpretedMethods;
        var frames = new Stack<MethodHandle>();
        if (hasActivatedRoot)
        {
            frames.Push(plan.RootMethod);
        }

        var activeHighWater = frames.Count;
        var executed = 0;
        for (var index = 0; index < events.Length; index++)
        {
            var item = events[index];
            Require(item.Method != default && interpreted.Contains(item.Method) && item.IlOffset >= 0 &&
                IsStableInstructionName(item.Instruction),
                "Every event must be a stable structural event from an interpreted method in the plan.");

            if (item.Kind == DebugEventKind.InstructionExecuted)
            {
                Require(frames.Count > 0 && frames.Peek() == item.Method,
                    "An executed instruction must belong to the currently active interpreted frame.");
                ValidateExecutedBoundary(plan, events, index, item);
                executed++;
                continue;
            }

            Require(item.Kind != DebugEventKind.TargetExceptionRaised,
                "The closed non-null rooted facade cannot retain a target-exception event.");
            Require(index > 0, "An ancillary event requires its immediately preceding executed instruction.");
            var previous = events[index - 1];
            Require(previous.Kind == DebugEventKind.InstructionExecuted,
                "An ancillary event must immediately follow one executed instruction event.");
            switch (item.Kind)
            {
                case DebugEventKind.FramePushed:
                    var call = plan.CallSites.SingleOrDefault(candidate =>
                        candidate.Disposition == FrozenMethodCallDisposition.Interpreted &&
                        candidate.Caller == previous.Method &&
                        candidate.IlOffset == previous.IlOffset &&
                        candidate.TargetMethod == item.Method);
                    Require(call is not null &&
                        string.Equals(previous.Instruction, "Call", StringComparison.Ordinal) &&
                        item.IlOffset == 0 && string.Equals(item.Instruction, "Entry", StringComparison.Ordinal),
                        "A frame-push event must exactly project one frozen interpreted call transfer.");
                    Require(frames.Count > 0 && frames.Peek() == previous.Method,
                        "An interpreted call must push below its currently active caller.");
                    frames.Push(item.Method);
                    activeHighWater = Math.Max(activeHighWater, frames.Count);
                    break;
                case DebugEventKind.FramePopped:
                    Require(item.Method == previous.Method && item.IlOffset == previous.IlOffset &&
                        string.Equals(previous.Instruction, "Return", StringComparison.Ordinal) &&
                        string.Equals(item.Instruction, "Return", StringComparison.Ordinal),
                        "A frame-pop event must immediately follow its method's executed return.");
                    Require(frames.Count > 0 && frames.Peek() == item.Method,
                        "A frame-pop event must remove the currently active interpreted method.");
                    frames.Pop();
                    break;
                case DebugEventKind.ValuePrecisionLost:
                    Require(item.Method == previous.Method && item.IlOffset == previous.IlOffset &&
                        string.Equals(previous.Instruction, "LoadField", StringComparison.Ordinal) &&
                        string.Equals(item.Instruction, "LoadField", StringComparison.Ordinal) &&
                        IsCanonicalFieldEvidence(item.FieldEvidence),
                        "A precision-loss event must immediately follow its canonical approximate field load.");
                    break;
                default:
                    throw new ArgumentException("The rooted event transcript contains an undefined event kind.");
            }
        }

        return new EventFacts(executed, activeHighWater, frames.Count);
    }

    private static void ValidateExecutedBoundary<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        ImmutableArray<DebugEvent> events,
        int index,
        DebugEvent item)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        var next = index + 1 < events.Length ? events[index + 1] : null;
        if (string.Equals(item.Instruction, "Call", StringComparison.Ordinal))
        {
            var call = plan.CallSites.SingleOrDefault(candidate =>
                candidate.Caller == item.Method && candidate.IlOffset == item.IlOffset);
            Require(call is not null, "An executed call event must identify one frozen call disposition.");
            var frozenCall = call ?? throw new ArgumentException("A frozen call disposition is required.");
            if (frozenCall.Disposition == FrozenMethodCallDisposition.Interpreted)
            {
                Require(next?.Kind == DebugEventKind.FramePushed && next.Method == frozenCall.TargetMethod,
                    "An executed interpreted call must immediately publish its callee frame entry.");
            }
            else
            {
                Require(next?.Kind != DebugEventKind.FramePushed,
                    "An opaque modeled call cannot publish an interpreted frame entry.");
            }
        }
        else if (string.Equals(item.Instruction, "Return", StringComparison.Ordinal))
        {
            Require(next?.Kind == DebugEventKind.FramePopped && next.Method == item.Method &&
                next.IlOffset == item.IlOffset,
                "An executed return must immediately publish its matching frame pop.");
        }
        else
        {
            Require(!string.Equals(item.Instruction, "Entry", StringComparison.Ordinal),
                "Entry is an ancillary frame event, not an executed IL instruction.");
        }
    }

    private static bool IsCanonicalFieldEvidence(FieldLoadEvidence? evidence)
    {
        if (evidence is null || evidence.EvidenceStatus is not
            (EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable))
        {
            return false;
        }

        return string.Equals(
            CounterfactualCanonical.Hash(evidence.CanonicalBytes.AsSpan()),
            evidence.Sha256,
            StringComparison.Ordinal);
    }

    private static bool IsStableInstructionName(string value) => value is
        "Nop" or "LoadArgument" or "LoadLocal" or "StoreLocal" or "LoadInt32" or
        "Add" or "Subtract" or "Multiply" or "LoadField" or "Call" or "Return" or "Entry";

    private static EvaluationEvidenceStatus AggregateReachedEvidence(
        CounterfactualMethodRequest request,
        ImmutableArray<CounterfactualFieldObservation> observations)
    {
        var aggregate = EvaluationEvidenceStatus.Exact;
        foreach (var status in request.Arguments.Select(static item => item.EvidenceStatus)
            .Concat(observations.Select(static item => item.EvidenceStatus)))
        {
            aggregate = WorseEvidence(aggregate, status);
        }

        return aggregate;
    }

    private static EvaluationEvidenceStatus WorseEvidence(
        EvaluationEvidenceStatus left,
        EvaluationEvidenceStatus right) => Rank(right) > Rank(left) ? right : left;

    private static int Rank(EvaluationEvidenceStatus value) => value switch
    {
        EvaluationEvidenceStatus.Exact => 0,
        EvaluationEvidenceStatus.Partial => 1,
        EvaluationEvidenceStatus.Unavailable => 2,
        EvaluationEvidenceStatus.Conflict => 3,
        EvaluationEvidenceStatus.Invalid => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static void ValidateCounters(
        ImmutableArray<PureModelAttempt> attempts,
        int invocationCount,
        int completedCount)
    {
        Require(invocationCount >= 0 && completedCount >= 0 && completedCount <= invocationCount &&
            attempts.Length == invocationCount &&
            attempts.Count(static attempt => attempt.TransferCompleted) == completedCount,
            "Model attempt counters must exactly agree with the retained attempt transcript.");
    }

    private static ImmutableArray<T> CopyAndRejectNull<T>(ImmutableArray<T> values, string parameterName)
    {
        var copied = CounterfactualCanonical.Copy(values);
        if (copied.Any(static value => value is null))
        {
            throw new ArgumentException("A counterfactual result projection cannot contain null entries.", parameterName);
        }

        return copied;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }

    private readonly record struct TranscriptFacts(int ExecutedInstructions, int ActiveFramesAtEnd);

    private readonly record struct EventFacts(
        int ExecutedInstructions,
        int ActiveFrameHighWater,
        int ActiveFramesAtEnd);
}
