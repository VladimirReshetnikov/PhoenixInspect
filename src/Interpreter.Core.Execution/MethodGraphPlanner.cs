using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Resolves, admits, validates, and freezes the complete W4 direct-call closure rooted at one MethodDef.
/// </summary>
/// <remarks>
/// Each <see cref="Prepare"/> call owns fresh first-result caches. Discovery is root-first depth-first traversal in
/// increasing call-site offset order; successful public vectors are subsequently canonicalized by structural
/// identity. Preparation performs no IL execution, memory access, or <see cref="IlMachine{TValue, TMemory}"/>
/// activation, and a rejection never exposes a partial graph.
/// </remarks>
public sealed class MethodGraphPlanner
{
    private readonly IResolutionServices _resolutionServices;

    /// <summary>Creates a planner over one metadata/body resolution capability.</summary>
    /// <param name="resolutionServices">
    /// The resolver used for root and reachable method definitions plus contextual field and direct-call operands.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="resolutionServices"/> is <see langword="null"/>.</exception>
    public MethodGraphPlanner(IResolutionServices resolutionServices)
    {
        ArgumentNullException.ThrowIfNull(resolutionServices);
        _resolutionServices = resolutionServices;
    }

    /// <summary>Prepares one complete immutable W4 graph, resolving the root definition rather than accepting one.</summary>
    /// <param name="root">The exact root MethodDef identity.</param>
    /// <returns>
    /// A ready result containing one validated graph, or a structured blocked, invalid, or traversal-exhausted result
    /// whose <see cref="MethodGraphPreparationResult.Plan"/> is <see langword="null"/>.
    /// </returns>
    public MethodGraphPreparationResult Prepare(MethodHandle root)
    {
        if (root == default)
        {
            return Failed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_CALL_GRAPH_INVALID",
                "A method graph requires a non-default root MethodDef.",
                root,
                0);
        }

        try
        {
            return new PreparationSession(_resolutionServices).Prepare(root);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_RESOLVER_FAILURE",
                "The metadata resolver rejected method-graph preparation.",
                root,
                0);
        }
    }

    private static MethodGraphPreparationResult Failed(
        MachineRunStatus status,
        ExecutionFailureKind kind,
        string code,
        string message,
        MethodHandle method,
        int offset) =>
        MethodGraphPreparationResult.Failed(
            status,
            new ExecutionFailure(kind, code, message, method == default ? null : method, offset));

    private static MethodGraphPreparationResult Conflict(
        string code,
        string message,
        MethodHandle method,
        int offset)
    {
        var conflict = new ResolutionFailure(ResolutionFailureKind.Conflict, code, message);
        return MethodGraphPreparationResult.Failed(
            MachineRunStatus.Blocked,
            new ExecutionFailure(
                ExecutionFailureKind.DependencyResolution,
                code,
                message,
                method,
                offset,
                ResolutionFailureDiagnostics.Sanitize(conflict)));
    }

    private static bool IsCapabilityException(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException;

    private sealed class PreparationSession : IMethodPlanResolutionContext
    {
        private const int MaximumMethodCount = 64;
        private const int MaximumTraversalUnitCount = 1024;

        private readonly IResolutionServices _resolver;
        private readonly Dictionary<MethodHandle, ResolutionResult<ResolvedMethodDefinition>> _definitionCache = [];
        private readonly Dictionary<FieldRequest, ResolutionResult<ResolvedField>> _fieldRequestCache = [];
        private readonly Dictionary<CallRequest, ResolutionResult<ResolvedMethodCallTarget>> _callRequestCache = [];
        private readonly HashSet<(MethodHandle ContextMethod, int IlOffset)> _chargedCallEdges = [];
        private readonly Dictionary<MethodHandle, VisitState> _visitStates = [];
        private readonly Dictionary<MethodHandle, AdmittedMethodPlan> _admittedPlans = [];
        private readonly Dictionary<FieldHandle, ResolvedField> _structuralFields = [];
        private readonly List<FrozenMethodCallSite> _callSites = [];
        private MethodGraphPreparationResult? _terminalFailure;
        private int _traversalUnitCount;

        internal PreparationSession(IResolutionServices resolver)
        {
            _resolver = resolver;
        }

        internal MethodGraphPreparationResult Prepare(MethodHandle root)
        {
            var discoveryFailure = Visit(root, W4GraphMethodRole.Root, incomingCall: null);
            if (discoveryFailure is not null)
            {
                return discoveryFailure;
            }

            if (_terminalFailure is not null)
            {
                return _terminalFailure;
            }

            return Freeze(root);
        }

        public ResolutionResult<ResolvedField> ResolveField(
            MethodHandle contextMethod,
            int ilOffset,
            int metadataToken)
        {
            var request = new FieldRequest(contextMethod, metadataToken);
            if (!_fieldRequestCache.TryGetValue(request, out var result))
            {
                result = InvokeResolver(
                    () => _resolver.ResolveField(contextMethod, metadataToken),
                    "Field resolver threw while preparing the frozen graph.");
                _fieldRequestCache.Add(request, result);
            }

            if (!result.IsSuccess || _terminalFailure is not null)
            {
                return result;
            }

            var field = result.Value;
            if (_structuralFields.TryGetValue(field.Handle, out var previous))
            {
                if (previous != field)
                {
                    _terminalFailure = Conflict(
                        "EXEC_FIELD_DESCRIPTOR_CONFLICT",
                        "Equal structural FieldDef identities resolved to conflicting descriptors.",
                        contextMethod,
                        ilOffset);
                    return ResolutionResult<ResolvedField>.Failed(
                        ResolutionFailureKind.Conflict,
                        "EXEC_FIELD_DESCRIPTOR_CONFLICT",
                        "A structural field dependency conflicted with its first frozen descriptor.");
                }

                return result;
            }

            if (!TryChargeTraversal(contextMethod, ilOffset))
            {
                return ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Unsupported,
                    "EXEC_CALL_GRAPH_TRAVERSAL_LIMIT",
                    "The fixed graph-preparation traversal limit was exhausted.");
            }

            _structuralFields.Add(field.Handle, field);
            return result;
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int ilOffset,
            int metadataToken)
        {
            if (_chargedCallEdges.Add((contextMethod, ilOffset)) &&
                !TryChargeTraversal(contextMethod, ilOffset))
            {
                return ResolutionResult<ResolvedMethodCallTarget>.Failed(
                    ResolutionFailureKind.Unsupported,
                    "EXEC_CALL_GRAPH_TRAVERSAL_LIMIT",
                    "The fixed graph-preparation traversal limit was exhausted.");
            }

            var request = new CallRequest(contextMethod, metadataToken);
            if (_callRequestCache.TryGetValue(request, out var cached))
            {
                return cached;
            }

            var result = InvokeResolver(
                () => _resolver.ResolveMethod(contextMethod, metadataToken),
                "Direct-method resolver threw while preparing the frozen graph.");
            _callRequestCache.Add(request, result);
            return result;
        }

        private MethodGraphPreparationResult? Visit(
            MethodHandle method,
            W4GraphMethodRole role,
            FrozenMethodCallSite? incomingCall)
        {
            if (_visitStates.TryGetValue(method, out var existingState))
            {
                if (existingState == VisitState.Visiting)
                {
                    return Cycle(incomingCall!);
                }

                return ValidateDefinitionCorrelation(method, incomingCall);
            }

            if (_visitStates.Count >= MaximumMethodCount)
            {
                var location = IncomingLocation(method, incomingCall);
                return Failed(
                    MachineRunStatus.Blocked,
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_CALL_GRAPH_METHOD_LIMIT",
                    $"A frozen method graph is limited to {MaximumMethodCount} distinct methods.",
                    location.Method,
                    location.Offset);
            }

            if (!TryChargeTraversal(
                    incomingCall?.Caller ?? method,
                    incomingCall?.IlOffset ?? 0))
            {
                return _terminalFailure;
            }

            _visitStates.Add(method, VisitState.Visiting);
            var definitionResult = GetMethodDefinition(method);
            if (!definitionResult.IsSuccess)
            {
                var location = IncomingLocation(method, incomingCall);
                return FromResolutionFailure(
                    definitionResult.Failure,
                    location.Method,
                    location.Offset,
                    "Method-definition resolution did not produce an executable definition.");
            }

            var definition = definitionResult.Value;
            if (definition.Method != method)
            {
                return incomingCall is null
                    ? Conflict(
                        "EXEC_METHOD_IDENTITY_CONFLICT",
                        "Resolver returned a root method definition with a different structural identity.",
                        method,
                        0)
                    : DefinitionConflict(incomingCall);
            }

            if (incomingCall is not null && definition.Signature.CallSignature != incomingCall.Target.Signature)
            {
                return DefinitionConflict(incomingCall);
            }

            var build = MethodPlanBuilder.BuildGraph(definition, role, this);
            if (_terminalFailure is not null)
            {
                return _terminalFailure;
            }

            if (!build.IsSuccess)
            {
                return MethodGraphPreparationResult.Failed(build.Status, build.Failure!);
            }

            var plan = build.Plan!;
            _admittedPlans.Add(method, plan);
            var outgoing = plan.Instructions
                .Where(instruction => instruction.Kind == AdmittedInstructionKind.Call)
                .OrderBy(instruction => instruction.IlOffset)
                .Select(instruction => new FrozenMethodCallSite(
                    method,
                    instruction.IlOffset,
                    instruction.Operand,
                    instruction.CallTarget!))
                .ToImmutableArray();
            _callSites.AddRange(outgoing);

            foreach (var callSite in outgoing)
            {
                var correlationFailure = ValidateDefinitionCorrelation(callSite.Target.Method, callSite);
                if (correlationFailure is not null)
                {
                    return correlationFailure;
                }

                if (_visitStates.TryGetValue(callSite.Target.Method, out var targetState) &&
                    targetState == VisitState.Visiting)
                {
                    return Cycle(callSite);
                }

                var childFailure = Visit(callSite.Target.Method, W4GraphMethodRole.Callee, callSite);
                if (childFailure is not null)
                {
                    return childFailure;
                }
            }

            _visitStates[method] = VisitState.Complete;
            return null;
        }

        private MethodGraphPreparationResult? ValidateDefinitionCorrelation(
            MethodHandle method,
            FrozenMethodCallSite? incomingCall)
        {
            if (incomingCall is null || !_admittedPlans.TryGetValue(method, out var existingPlan))
            {
                return null;
            }

            return existingPlan.Definition.Method == incomingCall.Target.Method &&
                existingPlan.Definition.Signature.CallSignature == incomingCall.Target.Signature
                ? null
                : DefinitionConflict(incomingCall);
        }

        private ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            if (_definitionCache.TryGetValue(method, out var cached))
            {
                return cached;
            }

            var result = InvokeResolver(
                () => _resolver.GetMethodDefinition(method),
                "Method-definition resolver threw while preparing the frozen graph.");
            _definitionCache.Add(method, result);
            return result;
        }

        private MethodGraphPreparationResult Freeze(MethodHandle root)
        {
            var canonicalCalls = _callSites
                .OrderBy(callSite => callSite.Caller, MethodHandleCanonicalComparer.Instance)
                .ThenBy(callSite => callSite.IlOffset)
                .ThenBy(callSite => callSite.Target.Method, MethodHandleCanonicalComparer.Instance)
                .ToImmutableArray();
            var canonicalFields = _structuralFields.Values
                .OrderBy(field => field.Handle, FieldHandleCanonicalComparer.Instance)
                .ToImmutableArray();
            var canonicalNodes = _admittedPlans
                .OrderBy(pair => pair.Key, MethodHandleCanonicalComparer.Instance)
                .Select(pair =>
                {
                    var fields = pair.Value.Instructions
                        .Where(instruction => instruction.Field is not null)
                        .Select(instruction => instruction.Field!)
                        .DistinctBy(field => field.Handle)
                        .OrderBy(field => field.Handle, FieldHandleCanonicalComparer.Instance)
                        .ToImmutableArray();
                    var calls = canonicalCalls
                        .Where(callSite => callSite.Caller == pair.Key)
                        .ToImmutableArray();
                    return new FrozenMethodGraphNode(pair.Value, fields, calls);
                })
                .ToImmutableArray();
            var requiredDepth = CalculateRequiredDepth(root, canonicalCalls);

            var validationFailure = ValidateFrozenGraph(
                root,
                canonicalNodes,
                canonicalFields,
                canonicalCalls,
                requiredDepth,
                _traversalUnitCount);
            if (validationFailure is not null)
            {
                return validationFailure;
            }

            try
            {
                return MethodGraphPreparationResult.Success(
                    new FrozenMethodGraphPlan(
                        root,
                        canonicalNodes,
                        canonicalFields,
                        canonicalCalls,
                        requiredDepth,
                        _traversalUnitCount));
            }
            catch (Exception exception) when (IsCapabilityException(exception))
            {
                return GraphInvalid(root, 0, "Canonical graph construction rejected its validated inputs.");
            }
        }

        private static MethodGraphPreparationResult? ValidateFrozenGraph(
            MethodHandle root,
            ImmutableArray<FrozenMethodGraphNode> nodes,
            ImmutableArray<ResolvedField> fields,
            ImmutableArray<FrozenMethodCallSite> callSites,
            int requiredDepth,
            int traversalUnits)
        {
            if (nodes.IsDefaultOrEmpty || fields.IsDefault || callSites.IsDefault ||
                nodes.Length > MaximumMethodCount ||
                traversalUnits != nodes.Length + fields.Length + callSites.Length ||
                traversalUnits > MaximumTraversalUnitCount ||
                !nodes.Any(node => node.Method == root) ||
                !IsStrictlyOrdered(nodes.Select(node => node.Method), MethodHandleCanonicalComparer.Instance) ||
                !IsStrictlyOrdered(fields.Select(field => field.Handle), FieldHandleCanonicalComparer.Instance))
            {
                return GraphInvalid(root, 0, "The frozen graph violates cardinality, uniqueness, or canonical-order invariants.");
            }

            var nodeMap = nodes.ToDictionary(node => node.Method);
            var edgeKeys = new HashSet<(MethodHandle Caller, int Offset)>();
            var fieldGroups = nodes
                .SelectMany(node => node.Fields)
                .GroupBy(field => field.Handle)
                .ToArray();
            if (fieldGroups.Any(group => group.Skip(1).Any(field => field != group.First())))
            {
                return GraphInvalid(root, 0, "Equal structural FieldDef identities carry conflicting node descriptors.");
            }

            var expectedGlobalFields = nodes
                .SelectMany(node => node.Fields)
                .DistinctBy(field => field.Handle)
                .OrderBy(field => field.Handle, FieldHandleCanonicalComparer.Instance);
            if (!fields.SequenceEqual(expectedGlobalFields))
            {
                return GraphInvalid(root, 0, "The global field vector is not the canonical union of node field dependencies.");
            }

            FrozenMethodCallSite? previousCallSite = null;
            foreach (var node in nodes)
            {
                if (!node.Admission.IsAdmitted ||
                    node.Admission.Failure is not null ||
                    node.Definition.Method != node.Method ||
                    !node.CallSites.SequenceEqual(callSites.Where(site => site.Caller == node.Method)))
                {
                    return GraphInvalid(node.Method, 0, "A frozen node disagrees with its definition, admission, or outgoing-edge projection.");
                }

                var expectedFields = node.RuntimePlan.Instructions
                    .Where(instruction => instruction.Field is not null)
                    .Select(instruction => instruction.Field!)
                    .DistinctBy(field => field.Handle)
                    .OrderBy(field => field.Handle, FieldHandleCanonicalComparer.Instance);
                if (!node.Fields.SequenceEqual(expectedFields))
                {
                    return GraphInvalid(node.Method, 0, "A frozen node's field projection disagrees with admitted instructions.");
                }
            }

            foreach (var callSite in callSites)
            {
                if (previousCallSite is not null &&
                    (MethodHandleCanonicalComparer.Instance.Compare(previousCallSite.Caller, callSite.Caller) > 0 ||
                     previousCallSite.Caller == callSite.Caller && previousCallSite.IlOffset >= callSite.IlOffset))
                {
                    return GraphInvalid(callSite.Caller, callSite.IlOffset, "The global call-site vector is not in strict canonical caller-and-offset order.");
                }

                previousCallSite = callSite;
                if (!edgeKeys.Add((callSite.Caller, callSite.IlOffset)) ||
                    !nodeMap.TryGetValue(callSite.Caller, out var caller) ||
                    !nodeMap.TryGetValue(callSite.Target.Method, out var target) ||
                    callSite.Target.Method.Module != callSite.Caller.Module ||
                    callSite.Target.Method.MetadataToken != callSite.MetadataToken ||
                    target.Definition.Signature.CallSignature != callSite.Target.Signature ||
                    !caller.RuntimePlan.TryGetInstruction(callSite.IlOffset, out var instruction) ||
                    instruction.Kind != AdmittedInstructionKind.Call ||
                    instruction.Operand != callSite.MetadataToken ||
                    instruction.CallTarget?.Method != callSite.Target.Method ||
                    instruction.CallTarget.Signature != callSite.Target.Signature)
                {
                    return GraphInvalid(callSite.Caller, callSite.IlOffset, "A direct-call edge violates identity, signature, or admitted-instruction agreement.");
                }
            }

            var reachable = new HashSet<MethodHandle>();
            var active = new HashSet<MethodHandle>();
            if (!VisitForValidation(root) || reachable.Count != nodes.Length ||
                CalculateRequiredDepth(root, callSites) != requiredDepth)
            {
                return GraphInvalid(root, 0, "The frozen graph is cyclic, unreachable, or has an incorrect longest-path depth.");
            }

            return null;

            bool VisitForValidation(MethodHandle method)
            {
                if (!active.Add(method))
                {
                    return false;
                }

                if (reachable.Add(method))
                {
                    foreach (var callSite in callSites.Where(site => site.Caller == method))
                    {
                        if (!VisitForValidation(callSite.Target.Method))
                        {
                            return false;
                        }
                    }
                }

                _ = active.Remove(method);
                return true;
            }
        }

        private static int CalculateRequiredDepth(
            MethodHandle root,
            ImmutableArray<FrozenMethodCallSite> callSites)
        {
            var memo = new Dictionary<MethodHandle, int>();
            return Calculate(root);

            int Calculate(MethodHandle method)
            {
                if (memo.TryGetValue(method, out var depth))
                {
                    return depth;
                }

                var childDepth = 0;
                foreach (var callSite in callSites.Where(site => site.Caller == method))
                {
                    childDepth = Math.Max(childDepth, Calculate(callSite.Target.Method));
                }

                depth = checked(childDepth + 1);
                memo.Add(method, depth);
                return depth;
            }
        }

        private static bool IsStrictlyOrdered<T>(IEnumerable<T> values, IComparer<T> comparer)
        {
            using var enumerator = values.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return true;
            }

            var previous = enumerator.Current;
            while (enumerator.MoveNext())
            {
                if (comparer.Compare(previous, enumerator.Current) >= 0)
                {
                    return false;
                }

                previous = enumerator.Current;
            }

            return true;
        }

        private bool TryChargeTraversal(MethodHandle method, int offset)
        {
            if (_traversalUnitCount >= MaximumTraversalUnitCount)
            {
                _terminalFailure ??= Failed(
                    MachineRunStatus.Blocked,
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_CALL_GRAPH_TRAVERSAL_LIMIT",
                    $"Graph preparation is limited to {MaximumTraversalUnitCount} traversal units.",
                    method,
                    offset);
                return false;
            }

            _traversalUnitCount++;
            return true;
        }

        private static ResolutionResult<T> InvokeResolver<T>(
            Func<ResolutionResult<T>> operation,
            string failureMessage)
        {
            try
            {
                return operation();
            }
            catch (Exception exception) when (IsCapabilityException(exception))
            {
                return ResolutionResult<T>.Failed(
                    ResolutionFailureKind.Unsupported,
                    "EXEC_RESOLVER_FAILURE",
                    failureMessage);
            }
        }

        private static MethodGraphPreparationResult FromResolutionFailure(
            ResolutionFailure? failure,
            MethodHandle method,
            int offset,
            string message)
        {
            failure ??= new ResolutionFailure(
                ResolutionFailureKind.Invalid,
                "RESOLUTION_INVALID_RESULT",
                "Resolver returned an invalid default result.");
            return MethodGraphPreparationResult.Failed(
                failure.Kind == ResolutionFailureKind.Invalid
                    ? MachineRunStatus.InvalidProgram
                    : MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.DependencyResolution,
                    failure.Code,
                    message,
                    method,
                    offset,
                    ResolutionFailureDiagnostics.Sanitize(failure)));
        }

        private static MethodGraphPreparationResult Cycle(FrozenMethodCallSite incomingCall) =>
            Failed(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_CALL_CYCLE_UNSUPPORTED",
                "The interpreted direct-call graph must be acyclic.",
                incomingCall.Caller,
                incomingCall.IlOffset);

        private static MethodGraphPreparationResult DefinitionConflict(FrozenMethodCallSite incomingCall) =>
            Conflict(
                "EXEC_CALL_TARGET_DEFINITION_CONFLICT",
                "The loaded callee definition disagrees with its frozen direct-call identity or signature.",
                incomingCall.Caller,
                incomingCall.IlOffset);

        private static MethodGraphPreparationResult GraphInvalid(
            MethodHandle method,
            int offset,
            string message) =>
            Failed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_CALL_GRAPH_INVALID",
                message,
                method,
                offset);

        private static (MethodHandle Method, int Offset) IncomingLocation(
            MethodHandle target,
            FrozenMethodCallSite? incomingCall) =>
            incomingCall is null
                ? (target, 0)
                : (incomingCall.Caller, incomingCall.IlOffset);

        private readonly record struct FieldRequest(MethodHandle ContextMethod, int MetadataToken);

        private readonly record struct CallRequest(MethodHandle ContextMethod, int MetadataToken);

        private enum VisitState
        {
            Visiting,
            Complete,
        }
    }
}
