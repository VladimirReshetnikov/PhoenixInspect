using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpDebugging;

/// <summary>Classifies a W5 method-acquisition failure without collapsing distinct evidence states.</summary>
public enum DumpMethodAcquisitionFailureKind
{
    /// <summary>A named module, type, method, field, object, body, or metadata range was absent.</summary>
    Missing = 1,

    /// <summary>More than one candidate matched a request that requires one exact answer.</summary>
    Ambiguous = 2,

    /// <summary>A deterministic traversal or raw read returned a nonempty but incomplete prefix.</summary>
    Partial = 3,

    /// <summary>Required evidence could not be obtained for a reason other than a proven named absence.</summary>
    Unavailable = 4,

    /// <summary>The evidence used a valid shape outside the deliberately admitted W5 capability.</summary>
    Unsupported = 5,

    /// <summary>Individually valid request and dump facts did not describe the same structural target.</summary>
    Incompatible = 6,

    /// <summary>Available evidence admitted competing structural answers.</summary>
    Conflict = 7,

    /// <summary>The request or captured evidence violated a required invariant.</summary>
    Invalid = 8,
}

/// <summary>Retains one bounded typed explanation for a failed W5 method acquisition.</summary>
public sealed record DumpMethodAcquisitionFailure
{
    internal DumpMethodAcquisitionFailure(
        DumpMethodAcquisitionFailureKind kind,
        string code,
        string message,
        ClrmdEvidenceStatus? evidenceStatus,
        ClrmdValueIssue? evidenceIssue)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 128)
        {
            throw new ArgumentException("A bounded stable acquisition code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > 2_048)
        {
            throw new ArgumentException("A bounded acquisition explanation is required.", nameof(message));
        }

        Kind = kind;
        Code = code;
        Message = message;
        EvidenceStatus = evidenceStatus;
        EvidenceIssue = evidenceIssue;
    }

    /// <summary>Gets the stable product-level failure category.</summary>
    public DumpMethodAcquisitionFailureKind Kind { get; }

    /// <summary>Gets the bounded machine-readable failure code.</summary>
    public string Code { get; }

    /// <summary>Gets the artifact-independent explanation that callers must not parse for behavior.</summary>
    public string Message { get; }

    /// <summary>Gets the adapter evidence status when the failure originated at an adapter operation.</summary>
    public ClrmdEvidenceStatus? EvidenceStatus { get; }

    /// <summary>Gets the adapter issue when the failure originated at an adapter operation.</summary>
    public ClrmdValueIssue? EvidenceIssue { get; }
}

/// <summary>Contains either one detached existing W4 binding or one typed W5 acquisition failure.</summary>
public sealed class DumpMethodAcquisitionResult
{
    private DumpMethodAcquisitionResult(
        CounterfactualDumpExecutionBinding? binding,
        DumpMethodAcquisitionFailure? failure)
    {
        Binding = binding;
        Failure = failure;
    }

    /// <summary>Gets whether acquisition produced a complete session-detached W4 binding.</summary>
    public bool IsSuccess => Binding is not null && Failure is null;

    /// <summary>Gets the detached existing W4 binding after success, or <see langword="null"/> after failure.</summary>
    public CounterfactualDumpExecutionBinding? Binding { get; }

    /// <summary>Gets the typed acquisition failure, or <see langword="null"/> after success.</summary>
    public DumpMethodAcquisitionFailure? Failure { get; }

    internal static DumpMethodAcquisitionResult Success(CounterfactualDumpExecutionBinding binding) =>
        new(binding ?? throw new ArgumentNullException(nameof(binding)), failure: null);

    internal static DumpMethodAcquisitionResult Failed(DumpMethodAcquisitionFailure failure) =>
        new(binding: null, failure ?? throw new ArgumentNullException(nameof(failure)));
}

/// <summary>
/// Owns every dump-to-W4 structural acquisition step for the one W5 method expression and returns only detached
/// evidence or a typed failure.
/// </summary>
/// <remarks>
/// The caller supplies an already-open dump and an issued W5 request; it supplies no structural handles, resolver,
/// graph, field vector, domain value, memory model, or machine state. The facade reuses the exact W4 resolver, binder,
/// model, and preparation candidate. It never opens a target-reported path and the successful binding has no live
/// ClrMD dependency, so the session may be disposed before preparation or execution.
/// </remarks>
public static class DumpMethodAcquisitionFacade
{
    /// <summary>Gets the only helper MethodDef name admitted by the current W5 method scenario.</summary>
    public const string SupportedHelperMethodName = "CombineMarkers";

    /// <summary>Gets the first required ordinary instance Int32 field name.</summary>
    public const string MarkerFieldName = "Marker";

    /// <summary>Gets the second required ordinary instance Int32 field name.</summary>
    public const string AlternateMarkerFieldName = "AlternateMarker";

    private const int MaximumRootMatches = 2;
    private const int MaximumHandlesScanned = 100_000;

    /// <summary>Acquires and detaches the complete W4 binding for one issued W5 method request.</summary>
    /// <param name="session">The already-open immutable dump session.</param>
    /// <param name="request">
    /// A classifier-issued request admitted as <see cref="DumpExpressionKind.CounterfactualMethod"/> and carrying the
    /// exact host-selected root and closed execution policy.
    /// </param>
    /// <returns>
    /// The existing detached W4 binding, or a typed missing, ambiguous, partial, unavailable, unsupported,
    /// incompatible, conflicting, or invalid acquisition failure. No execution occurs in this operation.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="request"/> is null.</exception>
    public static DumpMethodAcquisitionResult Acquire(
        ClrmdDumpSession session,
        DumpExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        return Acquire(new ClrmdDumpMethodEvidenceSource(session), request);
    }

    internal static DumpMethodAcquisitionResult Acquire(
        IDumpMethodEvidenceSource source,
        DumpExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        if (request.AdmittedKind != DumpExpressionKind.CounterfactualMethod ||
            request.MethodExpressionIdentity is null)
        {
            return Failed(
                DumpMethodAcquisitionFailureKind.Invalid,
                "W5_METHOD_REQUEST_REQUIRED",
                "Method acquisition requires a classifier-issued W5 method request.");
        }

        var binding = request.RootBinding;
        var root = binding.Root;
        if (binding.Status != Interpreter.Product.DumpQuery.DumpQueryRootBindingStatus.ExactObject || root is null)
        {
            return Failed(
                DumpMethodAcquisitionFailureKind.Invalid,
                "W5_ROOT_SELECTION_NOT_EXACT",
                "Method acquisition requires the exact root retained by the issued request.");
        }

        var snapshot = source.Snapshot;
        if (snapshot != binding.Snapshot || root.Snapshot != snapshot || root.Module.Identity.Snapshot != snapshot)
        {
            return Failed(
                DumpMethodAcquisitionFailureKind.Incompatible,
                "W5_ROOT_SNAPSHOT_MISMATCH",
                "The issued root and open dump do not identify the same immutable snapshot.",
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        var matchingModules = source.Modules
            .Where(candidate => candidate.Identity == root.Module.Identity)
            .ToImmutableArray();
        if (matchingModules.IsEmpty)
        {
            return Failed(
                DumpMethodAcquisitionFailureKind.Missing,
                "W5_MODULE_MISSING",
                "The exact runtime module retained by the issued root is absent from the open dump.",
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        if (matchingModules.Length != 1)
        {
            return Failed(
                DumpMethodAcquisitionFailureKind.Ambiguous,
                "W5_MODULE_AMBIGUOUS",
                "The open dump exposed more than one module with the exact selected runtime identity.",
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.AmbiguousMatch);
        }

        var module = matchingModules[0];
        var metadata = source.ReadModuleContentIdentity(module);
        if (metadata.Status != ClrmdEvidenceStatus.Exact || metadata.Value is null)
        {
            return FromEvidence(
                metadata.Status,
                metadata.Issue,
                "W5_METADATA_NOT_EXACT",
                "The selected runtime module did not yield one complete counted metadata image.");
        }

        var rootBody = source.ReadMethodBody(module, root.TypeName, DumpExpressionClassifier.SupportedMethodName);
        if (rootBody.Status != ClrmdEvidenceStatus.Exact || rootBody.Value is null)
        {
            return FromEvidence(
                rootBody.Status,
                rootBody.Issue,
                "W5_ROOT_METHOD_NOT_EXACT",
                "The exact directly declared zero-argument root MethodDef body could not be acquired.");
        }

        var helperBody = source.ReadMethodBody(module, root.TypeName, SupportedHelperMethodName);
        if (helperBody.Status != ClrmdEvidenceStatus.Exact || helperBody.Value is null)
        {
            return FromEvidence(
                helperBody.Status,
                helperBody.Issue,
                "W5_HELPER_METHOD_NOT_EXACT",
                "The exact admitted helper MethodDef body could not be acquired.");
        }

        var completeResolverResult = ClrmdDumpExecutionResolver.CreateMethodGraph(
            module,
            metadata,
            rootBody,
            ImmutableArray.Create(helperBody));
        if (!completeResolverResult.IsSuccess)
        {
            return FromResolution(completeResolverResult.Failure!);
        }

        var completeResolver = completeResolverResult.Value;
        var helperMethod = new MethodHandle(completeResolver.ModuleHandle, helperBody.Value.MetadataToken);
        var helperDefinitionResult = completeResolver.GetMethodDefinition(helperMethod);
        if (!helperDefinitionResult.IsSuccess)
        {
            return FromResolution(helperDefinitionResult.Failure!);
        }

        var signatureFailure = ValidateSignatures(completeResolver.RootMethodDefinition, helperDefinitionResult.Value, root);
        if (signatureFailure is not null)
        {
            return DumpMethodAcquisitionResult.Failed(signatureFailure);
        }

        ClrmdDumpExecutionResolver executionResolver;
        if (request.Policy.MethodMode == DumpMethodEvaluationMode.Interpreted)
        {
            executionResolver = completeResolver;
        }
        else
        {
            var rootOnlyResolver = ClrmdDumpExecutionResolver.Create(module, metadata, rootBody);
            if (!rootOnlyResolver.IsSuccess)
            {
                return FromResolution(rootOnlyResolver.Failure!);
            }

            executionResolver = rootOnlyResolver.Value;
        }

        var ownerSearch = source.FindStrongHandleObjectsByTypeName(
            root.TypeName,
            MaximumRootMatches,
            MaximumHandlesScanned);
        if (ownerSearch.Status != ClrmdEvidenceStatus.Exact)
        {
            return FromEvidence(
                ownerSearch.Status,
                ownerSearch.Issue,
                "W5_ROOT_REACQUISITION_NOT_EXACT",
                "The bounded strong-root traversal could not reproduce one exact selected owner.");
        }

        if (ownerSearch.Matches.Length != 1)
        {
            var kind = ownerSearch.Matches.IsEmpty
                ? DumpMethodAcquisitionFailureKind.Missing
                : DumpMethodAcquisitionFailureKind.Ambiguous;
            return Failed(
                kind,
                ownerSearch.Matches.IsEmpty ? "W5_ROOT_MISSING" : "W5_ROOT_AMBIGUOUS",
                ownerSearch.Matches.IsEmpty
                    ? "The exact selected root was absent from the exhaustive bounded traversal."
                    : "The bounded traversal found more than one root of the required exact runtime type.",
                ownerSearch.Matches.IsEmpty ? ClrmdEvidenceStatus.Unavailable : ClrmdEvidenceStatus.Conflict,
                ownerSearch.Matches.IsEmpty ? ClrmdValueIssue.ObjectUnavailable : ClrmdValueIssue.AmbiguousMatch);
        }

        var owner = ownerSearch.Matches[0];
        if (!SameRoot(owner, root))
        {
            return Failed(
                DumpMethodAcquisitionFailureKind.Incompatible,
                "W5_ROOT_REACQUISITION_CONFLICT",
                "The bounded traversal selected a different object or structural root identity than the issued request.",
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        var marker = source.ReadInt32Field(owner, MarkerFieldName);
        var markerFailure = ValidateFieldObservation(marker, "W5_MARKER_FIELD_NOT_USABLE");
        if (markerFailure is not null)
        {
            return DumpMethodAcquisitionResult.Failed(markerFailure);
        }

        var alternate = source.ReadInt32Field(owner, AlternateMarkerFieldName);
        var alternateFailure = ValidateFieldObservation(alternate, "W5_ALTERNATE_MARKER_FIELD_NOT_USABLE");
        if (alternateFailure is not null)
        {
            return DumpMethodAcquisitionResult.Failed(alternateFailure);
        }

        var markerEvidence = executionResolver.CorrelateInt32FieldObservation(ownerSearch, marker);
        if (!markerEvidence.IsSuccess)
        {
            return FromResolution(markerEvidence.Failure!);
        }

        var alternateEvidence = executionResolver.CorrelateInt32FieldObservation(ownerSearch, alternate);
        if (!alternateEvidence.IsSuccess)
        {
            return FromResolution(alternateEvidence.Failure!);
        }

        try
        {
            var modeled = request.Policy.MethodMode == DumpMethodEvaluationMode.Modeled;
            var result = CounterfactualDumpExecutionBinder.Bind(
                executionResolver,
                ImmutableArray.Create(markerEvidence.Value, alternateEvidence.Value),
                DumpExpressionPolicy.CounterfactualPolicyId,
                request.Policy.PolicyVersion,
                request.Policy.InstructionLimit,
                request.Policy.LogicalDepthLimit,
                request.Policy.TraversalLimit,
                DumpExpressionPolicy.ModelCatalogId,
                request.Policy.ModelCatalogVersion,
                modeled ? helperMethod : null,
                request.Policy.Assumptions,
                modeled ? new CombineMarkersModelRegistry(helperMethod) : null);
            return DumpMethodAcquisitionResult.Success(result);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Failed(
                DumpMethodAcquisitionFailureKind.Invalid,
                "W5_BINDING_INVALID",
                "The acquired structural evidence could not be adapted into the existing W4 preparation candidate.");
        }
    }

    private static DumpMethodAcquisitionFailure? ValidateSignatures(
        ResolvedMethodDefinition rootMethod,
        ResolvedMethodDefinition helperMethod,
        ClrmdHeapObjectInfo root)
    {
        var rootSignature = rootMethod.Signature;
        if (rootSignature.DeclaringType.MetadataToken != root.TypeMetadataToken ||
            rootSignature.CallingConvention != MethodCallingConventionKind.Default ||
            !rootSignature.HasImplicitThis ||
            rootSignature.HasExplicitThis ||
            rootSignature.GenericParameterCount != 0 ||
            !rootSignature.ParameterTypes.IsEmpty ||
            rootSignature.ReturnType != TypeSig.Int32)
        {
            return Failure(
                DumpMethodAcquisitionFailureKind.Incompatible,
                "W5_ROOT_METHOD_SIGNATURE_INCOMPATIBLE",
                "The selected root MethodDef is not the admitted directly declared instance Int32 method with zero arguments.");
        }

        var helperSignature = helperMethod.Signature;
        if (helperSignature.DeclaringType != rootSignature.DeclaringType ||
            helperSignature.CallingConvention != MethodCallingConventionKind.Default ||
            helperSignature.HasImplicitThis ||
            helperSignature.HasExplicitThis ||
            helperSignature.GenericParameterCount != 0 ||
            helperSignature.ParameterTypes.Length != 2 ||
            helperSignature.ParameterTypes.Any(static parameter => parameter != TypeSig.Int32) ||
            helperSignature.ReturnType != TypeSig.Int32)
        {
            return Failure(
                DumpMethodAcquisitionFailureKind.Incompatible,
                "W5_HELPER_METHOD_SIGNATURE_INCOMPATIBLE",
                "The selected helper MethodDef is not the admitted static Int32 pair-to-Int32 method.");
        }

        return null;
    }

    private static DumpMethodAcquisitionFailure? ValidateFieldObservation(
        ClrmdEvidenceResult<ClrmdInt32FieldObservation> observation,
        string code)
    {
        if (observation.Value is not null &&
            observation.Status is ClrmdEvidenceStatus.Exact or ClrmdEvidenceStatus.Partial or ClrmdEvidenceStatus.Unavailable &&
            observation.Issue is ClrmdValueIssue.None or ClrmdValueIssue.MemoryUnavailable)
        {
            return null;
        }

        return Failure(
            MapEvidence(observation.Status, observation.Issue),
            code,
            "A required ordinary instance Int32 field could not be selected and observed with correlatable evidence.",
            observation.Status,
            observation.Issue);
    }

    private static bool SameRoot(ClrmdHeapObjectInfo left, ClrmdHeapObjectInfo right) =>
        left.Snapshot == right.Snapshot &&
        left.Address == right.Address &&
        string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal) &&
        left.TypeMetadataToken == right.TypeMetadataToken &&
        left.MethodTable == right.MethodTable &&
        left.RootAddress == right.RootAddress &&
        string.Equals(left.RootKind, right.RootKind, StringComparison.Ordinal) &&
        left.Module.Identity == right.Module.Identity;

    private static DumpMethodAcquisitionResult FromEvidence(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        string code,
        string message) => DumpMethodAcquisitionResult.Failed(Failure(
        MapEvidence(status, issue),
        code,
        message,
        status,
        issue));

    private static DumpMethodAcquisitionResult FromResolution(ResolutionFailure failure) =>
        DumpMethodAcquisitionResult.Failed(Failure(
            failure.Kind switch
            {
                ResolutionFailureKind.Unavailable => DumpMethodAcquisitionFailureKind.Missing,
                ResolutionFailureKind.Unsupported => DumpMethodAcquisitionFailureKind.Unsupported,
                ResolutionFailureKind.Conflict => DumpMethodAcquisitionFailureKind.Conflict,
                _ => DumpMethodAcquisitionFailureKind.Invalid,
            },
            failure.Code,
            failure.Message));

    private static DumpMethodAcquisitionResult Failed(
        DumpMethodAcquisitionFailureKind kind,
        string code,
        string message,
        ClrmdEvidenceStatus? status = null,
        ClrmdValueIssue? issue = null) =>
        DumpMethodAcquisitionResult.Failed(Failure(kind, code, message, status, issue));

    private static DumpMethodAcquisitionFailure Failure(
        DumpMethodAcquisitionFailureKind kind,
        string code,
        string message,
        ClrmdEvidenceStatus? status = null,
        ClrmdValueIssue? issue = null) => new(kind, code, message, status, issue);

    private static DumpMethodAcquisitionFailureKind MapEvidence(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue)
    {
        if (status == ClrmdEvidenceStatus.Partial)
        {
            return DumpMethodAcquisitionFailureKind.Partial;
        }

        if (status == ClrmdEvidenceStatus.Invalid)
        {
            return DumpMethodAcquisitionFailureKind.Invalid;
        }

        if (status == ClrmdEvidenceStatus.Conflict)
        {
            return issue == ClrmdValueIssue.AmbiguousMatch
                ? DumpMethodAcquisitionFailureKind.Ambiguous
                : issue is ClrmdValueIssue.SnapshotMismatch or ClrmdValueIssue.TypeMismatch or
                    ClrmdValueIssue.MethodIdentityMismatch
                    ? DumpMethodAcquisitionFailureKind.Incompatible
                    : DumpMethodAcquisitionFailureKind.Conflict;
        }

        if (status == ClrmdEvidenceStatus.Unavailable)
        {
            return issue switch
            {
                ClrmdValueIssue.ModuleUnavailable or
                ClrmdValueIssue.MetadataUnavailable or
                ClrmdValueIssue.ObjectUnavailable or
                ClrmdValueIssue.FieldUnavailable or
                ClrmdValueIssue.TypeUnavailable or
                ClrmdValueIssue.MethodUnavailable or
                ClrmdValueIssue.MethodBodyUnavailable => DumpMethodAcquisitionFailureKind.Missing,
                ClrmdValueIssue.RuntimeUnsupported or
                ClrmdValueIssue.MethodBodyLayoutUnsupported or
                ClrmdValueIssue.MethodHeaderUnsupported or
                ClrmdValueIssue.MethodSectionUnsupported => DumpMethodAcquisitionFailureKind.Unsupported,
                _ => DumpMethodAcquisitionFailureKind.Unavailable,
            };
        }

        return DumpMethodAcquisitionFailureKind.Invalid;
    }

    private sealed class CombineMarkersModelRegistry(MethodHandle target) : IPureCallModelRegistry
    {
        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            if (candidate.Method != target)
            {
                return PureCallModelSelectionResult.NotApplicable("W4.Model.NotApplicable");
            }

            return PureCallModelSelectionResult.Selected(new CombineMarkersModel(candidate));
        }
    }

    private sealed class CombineMarkersModel : IPureCallModel
    {
        internal CombineMarkersModel(ResolvedMethodCallTarget target)
        {
            Descriptor = new PureCallModelDescriptor(
                new PureCallModelIdentity("w4.combine-markers", new PureCallModelVersion(1, 0, 0)),
                target,
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None);
        }

        public PureCallModelDescriptor Descriptor { get; }

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            return invocation.Arguments.Any(static argument =>
                    argument.Kind == PureCallModelArgumentKind.ExplainedUnknownInt32)
                ? PureCallModelOutcome.UnknownReturn()
                : PureCallModelOutcome.ExactReturn(unchecked(
                    invocation.Arguments[0].Int32Value!.Value +
                    invocation.Arguments[1].Int32Value!.Value));
        }
    }
}

internal interface IDumpMethodEvidenceSource
{
    ClrmdSnapshotIdentity Snapshot { get; }

    ImmutableArray<ClrmdModuleInfo> Modules { get; }

    ClrmdEvidenceResult<ModuleContentIdentity> ReadModuleContentIdentity(ClrmdModuleInfo module);

    ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
        ClrmdModuleInfo module,
        string typeName,
        string methodName);

    ClrmdHeapObjectSearchResult FindStrongHandleObjectsByTypeName(
        string typeName,
        int maximumMatches,
        int maximumHandlesScanned);

    ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
        ClrmdHeapObjectInfo owner,
        string fieldName);
}

internal sealed class ClrmdDumpMethodEvidenceSource(ClrmdDumpSession session) : IDumpMethodEvidenceSource
{
    private readonly ClrmdDumpSession session = session ?? throw new ArgumentNullException(nameof(session));

    public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

    public ImmutableArray<ClrmdModuleInfo> Modules => session.Modules;

    public ClrmdEvidenceResult<ModuleContentIdentity> ReadModuleContentIdentity(ClrmdModuleInfo module) =>
        session.ReadModuleContentIdentity(module);

    public ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
        ClrmdModuleInfo module,
        string typeName,
        string methodName) => session.ReadMethodBody(module, typeName, methodName);

    public ClrmdHeapObjectSearchResult FindStrongHandleObjectsByTypeName(
        string typeName,
        int maximumMatches,
        int maximumHandlesScanned) => session.FindStrongHandleObjectsByTypeName(
        typeName,
        maximumMatches,
        maximumHandlesScanned);

    public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
        ClrmdHeapObjectInfo owner,
        string fieldName) => session.ReadInt32Field(owner, fieldName);
}
