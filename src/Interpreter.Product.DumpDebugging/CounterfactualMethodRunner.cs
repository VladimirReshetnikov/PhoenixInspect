using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Owns issuer authority for preparing and, in later W4.8 slices, executing rooted counterfactual method plans.
/// </summary>
/// <typeparam name="TMemory">The persistent memory snapshot type privately retained by successful plans.</typeparam>
/// <remarks>
/// The current draft exposes preparation only. It validates raw host data without throwing for ordinary malformed
/// inputs, invokes graph preparation exactly once, and performs no memory access, machine activation, or IL
/// instruction execution. Plans are privately stamped by this runner instance so a future execution method can
/// reject foreign or forged plans before consulting their retained capabilities.
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
            SanitizeCoreFailure(coreFailure, code, message),
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

    private static ExecutionFailure? SanitizeCoreFailure(
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
