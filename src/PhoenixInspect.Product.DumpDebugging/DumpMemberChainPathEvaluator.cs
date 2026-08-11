using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>
/// Evaluates one admitted arbitrary-depth member chain by resolving each intermediate reference hop and then
/// evaluating the final two-member tail through the frozen fixed-depth pipeline.
/// </summary>
/// <remarks>
/// Every intermediate hop uses the same certified evidence steps as the frozen two-member chain: the reference
/// field is certified from complete counted metadata, its pointer is read once, the referenced object is validated
/// against the exact declared type, and only then does the walk continue. A <c>?.</c> separator whose receiver is
/// exactly null short-circuits the whole expression to the coalescing fallback, mirroring V1 semantics. Chain depth
/// is bounded only by the front end's expression-length and node-count limits — there is no hop-count limit.
/// </remarks>
public static class DumpMemberChainPathEvaluator
{
    private const string RawMemoryReadBoundName = "dump.memory-read.bytes";
    private const string CoalesceProvenanceId = "dump-member-chain-path:null-coalesce-v1";

    /// <summary>Evaluates one accepted chain-path request against the session that issued its root.</summary>
    /// <param name="session">The open immutable dump session containing the request's exact root.</param>
    /// <param name="request">The accepted arbitrary-depth chain request produced by the product classifier.</param>
    /// <returns>
    /// A no-effect derived-query result preserving independent completion, completeness, evidence, value, context,
    /// ordered provenance, and diagnostic axes.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EvaluationResult<DumpQueryValue> Evaluate(
        ClrmdDumpSession session,
        DumpExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        var identity = request.MemberChainPathIdentity;
        if (request.AdmittedKind != DumpExpressionKind.MemberChainPath || identity is null)
        {
            return Failure(
                session,
                request,
                ClrmdEvidenceStatus.Invalid,
                "QUERY_CHAIN_PATH_REQUEST_INVALID",
                "Path evaluation requires one accepted MemberChainV2 chain request.",
                CreateBaseProvenance(request),
                rawMemoryReadReached: false,
                stepBounds: ImmutableArray<EvaluationDeterministicBound>.Empty);
        }

        var admission = session.ReadModuleEditAdmission();
        if (!admission.IsAdmitted)
        {
            var admissionProvenance = CreateBaseProvenance(request);
            ModuleEditAdmissionPolicy.AppendProvenance(admissionProvenance, admission);
            return Failure(
                session,
                request,
                admission.Status,
                ModuleEditAdmissionPolicy.Code(admission),
                ModuleEditAdmissionPolicy.Message(admission),
                admissionProvenance,
                rawMemoryReadReached: !admission.Evidence.IsEmpty,
                stepBounds: [],
                completion: ModuleEditAdmissionPolicy.Completion(admission));
        }

        var provenance = CreateBaseProvenance(request);
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-member-chain-path:sha256:{identity.Sha256}"));
        var stepBounds = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>();
        var rootBinding = request.RootBinding;
        if (rootBinding.Root is not { } current || session.Snapshot != rootBinding.Snapshot)
        {
            return Failure(
                session,
                request,
                ClrmdEvidenceStatus.Conflict,
                "DUMP_SNAPSHOT_MISMATCH",
                "The chain-path request belongs to a different immutable dump snapshot.",
                provenance,
                rawMemoryReadReached: false,
                stepBounds.ToImmutable());
        }

        var hops = identity.Hops;
        var anyRead = false;
        for (var index = 0; index < hops.Length - 2; index++)
        {
            var hop = hops[index];
            var certificateResult = session.CertifyDeclaredReferenceMember(current, hop.Name);
            MergeStepBounds(stepBounds, certificateResult.AppliedBounds);
            AppendMemoryProvenance(provenance, certificateResult.Evidence);
            anyRead |= !certificateResult.Evidence.IsDefaultOrEmpty;
            if (certificateResult.Status != ClrmdEvidenceStatus.Exact || certificateResult.Value is null)
            {
                return Failure(
                    session,
                    request,
                    certificateResult.Status,
                    StageCode("BIND", certificateResult.Status),
                    $"Declaration binding for chain member '{hop.Name}' stopped: "
                    + StageMessage(certificateResult.Status, certificateResult.Issue),
                    provenance,
                    anyRead,
                    stepBounds.ToImmutable());
            }

            var certificate = certificateResult.Value;
            provenance.Add(HashedStructureProvenance(
                "dump-member-chain-path-certificate",
                certificate.ToCanonicalReplayProjection(),
                certificate.OuterField.Address));

            var referenceResult = session.ReadObjectReference(current, certificate.OuterField);
            AppendMemoryProvenance(provenance, referenceResult.Evidence);
            anyRead = true;
            if (referenceResult.Status != ClrmdEvidenceStatus.Exact || referenceResult.Value is null)
            {
                return Failure(
                    session,
                    request,
                    referenceResult.Status,
                    StageCode("REFERENCE", referenceResult.Status),
                    $"The reference value of chain member '{hop.Name}' could not be read exactly: "
                    + StageMessage(referenceResult.Status, referenceResult.Issue),
                    provenance,
                    anyRead,
                    stepBounds.ToImmutable());
            }

            var reference = referenceResult.Value;
            if (reference.IsExactNull)
            {
                if (!hops[index + 1].IsConditionalAccess)
                {
                    return Failure(
                        session,
                        request,
                        ClrmdEvidenceStatus.Exact,
                        "QUERY_CHAIN_NULL_RECEIVER",
                        "Direct member access cannot continue through an exact null intermediate reference.",
                        provenance,
                        anyRead,
                        stepBounds.ToImmutable(),
                        completion: EvaluationCompletionStatus.Blocked);
                }

                return ExactNullShortCircuit(session, request, identity, provenance, stepBounds.ToImmutable());
            }

            if (!reference.IsExactNonNull)
            {
                return Failure(
                    session,
                    request,
                    ClrmdEvidenceStatus.Invalid,
                    "QUERY_CHAIN_REFERENCE_INVALID",
                    "An exact reference observation did not contain a supported null or non-null pointer state.",
                    provenance,
                    anyRead,
                    stepBounds.ToImmutable());
            }

            var targetResult = session.ValidateReferencedObject(certificate, reference);
            AppendCumulativeTail(provenance, targetResult.Evidence, alreadyRetained: 1);
            if (targetResult.Status != ClrmdEvidenceStatus.Exact || targetResult.Value is null)
            {
                var (code, message) =
                    targetResult is { Status: ClrmdEvidenceStatus.Unavailable, Issue: ClrmdValueIssue.MemberShapeUnsupported }
                        ? ("QUERY_CHAIN_RUNTIME_TYPE_UNSUPPORTED",
                            "The exact referenced runtime type differs from the frozen declared type supported by this profile.")
                        : (StageCode("TARGET", targetResult.Status),
                            $"The object referenced by chain member '{hop.Name}' could not be validated: "
                            + StageMessage(targetResult.Status, targetResult.Issue));
                return Failure(
                    session,
                    request,
                    targetResult.Status,
                    code,
                    message,
                    provenance,
                    anyRead,
                    stepBounds.ToImmutable());
            }

            var target = targetResult.Value;
            provenance.Add(HashedStructureProvenance(
                "dump-member-chain-path-target",
                target.Identity.ToCanonicalReplayProjection(),
                target.Address));

            var projection = session.ProjectReferencedObjectForInstanceEvaluation(target);
            if (projection.Status != ClrmdEvidenceStatus.Exact || projection.Value is null)
            {
                return Failure(
                    session,
                    request,
                    projection.Status,
                    StageCode("TARGET", projection.Status),
                    $"The validated object of chain member '{hop.Name}' could not be projected for the next hop: "
                    + StageMessage(projection.Status, projection.Issue),
                    provenance,
                    anyRead,
                    stepBounds.ToImmutable());
            }

            current = projection.Value;
        }

        // The remaining two hops are exactly one frozen fixed-depth chain over the resolved intermediate object.
        var tailExpression = BuildTailExpression(identity);
        var tailBinding = DumpQueryRootBinding.FromExactObject(rootBinding.Name, current);
        var tailOutcome = DumpExpressionEvaluator.Evaluate(
            session,
            tailExpression,
            tailBinding,
            request.Policy,
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        if (tailOutcome.DerivedQueryResult is not { } tailResult)
        {
            return Failure(
                session,
                request,
                ClrmdEvidenceStatus.Invalid,
                "QUERY_CHAIN_PATH_TAIL_INVALID",
                "The synthesized two-member tail was not admitted by the frozen chain grammar.",
                provenance,
                anyRead,
                stepBounds.ToImmutable());
        }

        return MergePrefix(request, provenance, stepBounds.ToImmutable(), tailResult);
    }

    private static string BuildTailExpression(DumpMemberChainPathIdentity identity)
    {
        var hops = identity.Hops;
        var reference = hops[^2];
        var terminal = hops[^1];
        var builder = new StringBuilder();
        builder.Append(identity.RootName);
        builder.Append('.');
        builder.Append(reference.Name);
        builder.Append(terminal.IsConditionalAccess ? "?." : ".");
        builder.Append(terminal.Name);
        if (identity.FallbackLiteralText is { } literalText)
        {
            builder.Append(" ?? ");
            builder.Append(literalText);
        }

        return builder.ToString();
    }

    private static EvaluationResult<DumpQueryValue> ExactNullShortCircuit(
        ClrmdDumpSession session,
        DumpExpressionRequest request,
        DumpMemberChainPathIdentity identity,
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        ImmutableArray<EvaluationDeterministicBound> stepBounds)
    {
        if (identity.FallbackKind != DumpMemberChainFallbackKind.None)
        {
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.Transformation,
                CoalesceProvenanceId));
        }

        var value = identity.FallbackKind switch
        {
            DumpMemberChainFallbackKind.None or DumpMemberChainFallbackKind.Null => DumpQueryValue.FromNull(),
            DumpMemberChainFallbackKind.Int32 => DumpQueryValue.FromInt32(identity.Int32Fallback!.Value),
            DumpMemberChainFallbackKind.String => DumpQueryValue.FromString(identity.StringFallback!),
            _ => throw new InvalidOperationException("The admitted chain-path fallback is invalid."),
        };
        return EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            value,
            CreateContext(session, request, stepBounds, rawMemoryReadReached: true),
            provenance.ToImmutable(),
            ImmutableArray<EvaluationDiagnostic>.Empty);
    }

    private static EvaluationResult<DumpQueryValue> MergePrefix(
        DumpExpressionRequest request,
        ImmutableArray<EvaluationProvenance>.Builder prefixProvenance,
        ImmutableArray<EvaluationDeterministicBound> stepBounds,
        EvaluationResult<DumpQueryValue> tail)
    {
        var mergedBounds = MergeBoundSets(
            tail.Context.Bounds,
            stepBounds,
            request.ReachedBounds,
            request.RootBinding.AppliedBounds);
        var context = EvaluationEvidenceContext.Create(
            tail.Context.SourceKind,
            tail.Context.Snapshot,
            tail.Context.Module,
            tail.Context.Fallback,
            mergedBounds);
        prefixProvenance.AddRange(tail.Provenance);
        return EvaluationResult<DumpQueryValue>.Create(
            tail.SemanticMode,
            tail.Completion,
            tail.Completeness,
            tail.Evidence,
            tail.Effects,
            tail.Value,
            context,
            prefixProvenance.ToImmutable(),
            tail.Diagnostics);
    }

    private static EvaluationResult<DumpQueryValue> Failure(
        ClrmdDumpSession session,
        DumpExpressionRequest request,
        ClrmdEvidenceStatus status,
        string code,
        string message,
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        bool rawMemoryReadReached,
        ImmutableArray<EvaluationDeterministicBound> stepBounds,
        EvaluationCompletionStatus? completion = null) =>
        EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            completion ?? (status == ClrmdEvidenceStatus.Invalid
                ? EvaluationCompletionStatus.Invalid
                : EvaluationCompletionStatus.Blocked),
            EvaluationCompleteness.None,
            MapEvidence(status),
            EvaluationEffectStatus.None,
            value: null,
            CreateContext(session, request, stepBounds, rawMemoryReadReached),
            provenance.ToImmutable(),
            ImmutableArray.Create(new EvaluationDiagnostic(code, message)));

    private static EvaluationEvidenceContext CreateContext(
        ClrmdDumpSession session,
        DumpExpressionRequest request,
        ImmutableArray<EvaluationDeterministicBound> stepBounds,
        bool rawMemoryReadReached)
    {
        var additions = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>();
        if (rawMemoryReadReached)
        {
            additions.Add(new EvaluationDeterministicBound(RawMemoryReadBoundName, session.Memory.MaximumReadLength));
        }

        var bounds = MergeBoundSets(
            request.RootBinding.AppliedBounds,
            request.ReachedBounds,
            stepBounds,
            additions.ToImmutable());
        var root = request.RootBinding.Root;
        var module = root is not null && root.Snapshot == session.Snapshot
            ? EvaluationEvidenceIdentity.CreateAvailable(root.Module.Identity.SourceId)
            : EvaluationEvidenceIdentity.Unavailable;
        return EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            EvaluationEvidenceIdentity.CreateAvailable(session.Snapshot.MemorySourceId),
            module,
            EvaluationFallback.None,
            bounds);
    }

    private static ImmutableArray<EvaluationProvenance>.Builder CreateBaseProvenance(
        DumpExpressionRequest request)
    {
        var provenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
        if (request.RootBinding.Root is { } root)
        {
            AppendMemoryProvenance(provenance, request.RootBinding.Evidence);
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.RuntimeStructure,
                root.Snapshot.MemorySourceId,
                root.Address));
        }

        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-member-chain-path-request:sha256:{request.Sha256}"));
        return provenance;
    }

    private static void MergeStepBounds(
        ImmutableArray<EvaluationDeterministicBound>.Builder accumulator,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        if (bounds.IsDefault)
        {
            return;
        }

        foreach (var bound in bounds)
        {
            if (!accumulator.Any(existing => string.Equals(existing.Name, bound.Name, StringComparison.Ordinal)))
            {
                accumulator.Add(bound);
            }
        }
    }

    private static ImmutableArray<EvaluationDeterministicBound> MergeBoundSets(
        params ImmutableArray<EvaluationDeterministicBound>[] groups)
    {
        var bounds = new Dictionary<string, EvaluationDeterministicBound>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            if (group.IsDefault)
            {
                continue;
            }

            foreach (var bound in group)
            {
                bounds[bound.Name] = bound;
            }
        }

        return bounds.Values.OrderBy(static bound => bound.Name, StringComparer.Ordinal).ToImmutableArray();
    }

    private static void AppendMemoryProvenance(
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        ImmutableArray<MemoryReadResult> evidence)
    {
        if (evidence.IsDefault)
        {
            return;
        }

        foreach (var read in evidence)
        {
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.BytesRead));
        }
    }

    private static void AppendCumulativeTail(
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        ImmutableArray<MemoryReadResult> cumulativeEvidence,
        int alreadyRetained)
    {
        if (cumulativeEvidence.IsDefaultOrEmpty || alreadyRetained >= cumulativeEvidence.Length)
        {
            return;
        }

        foreach (var read in cumulativeEvidence[alreadyRetained..])
        {
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.BytesRead));
        }
    }

    private static EvaluationProvenance HashedStructureProvenance(
        string domain,
        string projection,
        ulong address) =>
        new(
            EvaluationProvenanceKind.RuntimeStructure,
            $"{domain}:sha256:{Hash(projection)}",
            address);

    private static string StageCode(string stage, ClrmdEvidenceStatus status) =>
        $"QUERY_CHAIN_PATH_{stage}_{status.ToString().ToUpperInvariant()}";

    private static string StageMessage(ClrmdEvidenceStatus status, ClrmdValueIssue issue) => issue switch
    {
        ClrmdValueIssue.MemoryUnavailable => "required bytes are incomplete or unavailable.",
        ClrmdValueIssue.SnapshotMismatch => "the evidence belongs to a different immutable snapshot.",
        ClrmdValueIssue.TypeMismatch => "the evidence conflicts with the declared chain type identity.",
        ClrmdValueIssue.MemberShapeUnsupported => "the member shape is outside the admitted chain profile.",
        ClrmdValueIssue.FieldUnavailable => "the named member is not declared by the receiver's exact type.",
        ClrmdValueIssue.AmbiguousMatch => "more than one declared member matched the name.",
        ClrmdValueIssue.ModuleUnavailable => "the declaring module is unavailable in the snapshot.",
        ClrmdValueIssue.TypeUnavailable => "the declared target type is unavailable in the runtime catalog.",
        ClrmdValueIssue.LimitExceeded => "a deterministic observation bound retained only a prefix.",
        ClrmdValueIssue.InvalidData => "the evidence violates a supported layout invariant.",
        ClrmdValueIssue.ObjectUnavailable => "the referenced object is unavailable in the snapshot.",
        _ => status switch
        {
            ClrmdEvidenceStatus.Partial => "the evidence is incomplete.",
            ClrmdEvidenceStatus.Unavailable => "the required evidence is unavailable.",
            ClrmdEvidenceStatus.Conflict => "available evidence disagrees.",
            _ => "the evidence is structurally invalid.",
        },
    };

    private static EvaluationEvidenceStatus MapEvidence(ClrmdEvidenceStatus status) => status switch
    {
        ClrmdEvidenceStatus.Exact => EvaluationEvidenceStatus.Exact,
        ClrmdEvidenceStatus.Partial => EvaluationEvidenceStatus.Partial,
        ClrmdEvidenceStatus.Unavailable => EvaluationEvidenceStatus.Unavailable,
        ClrmdEvidenceStatus.Conflict => EvaluationEvidenceStatus.Conflict,
        _ => EvaluationEvidenceStatus.Invalid,
    };

    private static string Hash(string projection) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(projection))).ToLowerInvariant();
}
