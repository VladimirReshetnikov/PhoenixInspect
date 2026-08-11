using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Evaluates one completely prepared W6 member-chain plan without reparsing or repeating member lookup.</summary>
/// <remarks>
/// This engine reads the frozen outer reference once, validates an exact non-null target, derives terminal
/// storage only from certified relative layout, and delegates decoding to existing descriptor-consuming adapter
/// operations. It never invokes a certified getter or performs compiler binding.
/// </remarks>
public static class DumpMemberChainEngine
{
    private const int MaximumObservedStringCharacters = 4_096;
    private const string RawMemoryReadBoundName = "dump.memory-read.bytes";
    private const string ObservedStringBoundName = "query.observed-string.characters";
    private const string CoalesceProvenanceId = "dump-member-chain:null-coalesce-v1";

    /// <summary>Evaluates one immutable member-chain plan against the session that issued its evidence.</summary>
    /// <param name="session">The open immutable dump session containing the plan's exact root.</param>
    /// <param name="plan">The complete plan issued by W6 preparation before either value read.</param>
    /// <returns>
    /// A no-effect derived-query result preserving independent completion, completeness, evidence, value, context,
    /// ordered provenance, and diagnostic axes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/> or <paramref name="plan"/> is <see langword="null"/>.
    /// </exception>
    public static EvaluationResult<DumpQueryValue> Evaluate(
        ClrmdDumpSession session,
        DumpMemberChainPlan plan)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(plan);
        var admission = session.ReadModuleEditAdmission();
        var source = new ClrmdDumpMemberChainEvidenceSource(session);
        if (!admission.IsAdmitted)
        {
            return FromPreparationFailure(
                source,
                plan.RootBinding,
                plan.RequestBounds,
                plan.RequestSha256,
                new DumpMemberChainPreparationFailure(
                    ModuleEditAdmissionPolicy.Code(admission),
                    ModuleEditAdmissionPolicy.Message(admission),
                    admission.Status,
                    admission.Issue,
                    admission.Evidence,
                    appliedBounds: [],
                    moduleEditAdmission: admission));
        }

        return Evaluate(source, plan);
    }

    internal static EvaluationResult<DumpQueryValue> Evaluate(
        IDumpMemberChainEvidenceSource source,
        DumpMemberChainPlan plan)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(plan);
        var provenance = CreateBaseProvenance(source, plan);
        if (source.Snapshot != plan.RootBinding.Snapshot)
        {
            return CreateResult(
                source,
                plan,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Conflict,
                value: null,
                provenance,
                "DUMP_SNAPSHOT_MISMATCH",
                "The member-chain plan belongs to a different immutable dump snapshot.",
                rawMemoryReadReached: false,
                observedStringBoundReached: false);
        }

        var root = plan.RootBinding.Root!;
        var referenceResult = source.ReadObjectReference(root, plan.Certificate.OuterField);
        AppendMemoryProvenance(provenance, referenceResult.Evidence);
        if (referenceResult.Value is { } retainedReference)
        {
            provenance.Add(CreateHashedStructureProvenance(
                "dump-member-chain-reference",
                retainedReference.ToCanonicalReplayProjection(),
                retainedReference.Memory.Address,
                retainedReference.Memory.RequestedLength,
                retainedReference.Memory.BytesRead));
        }

        if (referenceResult.Status != ClrmdEvidenceStatus.Exact || referenceResult.Value is null)
        {
            return NonExact(
                source,
                plan,
                referenceResult.Status,
                referenceResult.Issue,
                provenance,
                "REFERENCE",
                rawMemoryReadReached: !referenceResult.Evidence.IsEmpty);
        }

        var reference = referenceResult.Value;
        if (reference.IsExactNull)
        {
            if (plan.AccessKind == DumpMemberChainAccessKind.Direct)
            {
                return CreateResult(
                    source,
                    plan,
                    EvaluationCompletionStatus.Blocked,
                    EvaluationCompleteness.None,
                    EvaluationEvidenceStatus.Exact,
                    value: null,
                    provenance,
                    "QUERY_CHAIN_NULL_RECEIVER",
                    "Direct member access cannot continue through an exact null intermediate reference.",
                    rawMemoryReadReached: true,
                    observedStringBoundReached: false);
            }

            var value = ApplyExactNull(plan, provenance);
            return CreateResult(
                source,
                plan,
                EvaluationCompletionStatus.Completed,
                EvaluationCompleteness.Complete,
                EvaluationEvidenceStatus.Exact,
                value,
                provenance,
                diagnosticCode: null,
                diagnosticMessage: null,
                rawMemoryReadReached: true,
                observedStringBoundReached: false);
        }

        if (!reference.IsExactNonNull)
        {
            return CreateResult(
                source,
                plan,
                EvaluationCompletionStatus.Invalid,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Invalid,
                value: null,
                provenance,
                "QUERY_CHAIN_REFERENCE_INVALID",
                "An exact reference observation did not contain a supported null or non-null pointer state.",
                rawMemoryReadReached: true,
                observedStringBoundReached: false);
        }

        var targetResult = source.ValidateReferencedObject(plan.Certificate, reference);
        AppendCumulativeTail(provenance, targetResult.Evidence, referenceResult.Evidence.Length);
        if (targetResult.Value is { } retainedTarget)
        {
            provenance.Add(CreateHashedStructureProvenance(
                "dump-member-chain-target",
                retainedTarget.Identity.ToCanonicalReplayProjection(),
                retainedTarget.Address));
            provenance.Add(CreateHashedStructureProvenance(
                "dump-member-chain-selection",
                retainedTarget.ToCanonicalReplayProjection(),
                retainedTarget.Address));
        }

        if (targetResult.Status != ClrmdEvidenceStatus.Exact || targetResult.Value is null)
        {
            if (targetResult.Issue == ClrmdValueIssue.MemberShapeUnsupported &&
                targetResult.Status == ClrmdEvidenceStatus.Unavailable)
            {
                return CreateResult(
                    source,
                    plan,
                    EvaluationCompletionStatus.Blocked,
                    EvaluationCompleteness.None,
                    EvaluationEvidenceStatus.Exact,
                    value: null,
                    provenance,
                    "QUERY_CHAIN_RUNTIME_TYPE_UNSUPPORTED",
                    "The exact referenced runtime type differs from the frozen declared type supported by this profile.",
                    rawMemoryReadReached: true,
                    observedStringBoundReached: false);
            }

            return NonExact(
                source,
                plan,
                targetResult.Status,
                targetResult.Issue,
                provenance,
                "TARGET",
                rawMemoryReadReached: true);
        }

        var target = targetResult.Value;
        var storageResult = source.BindTerminalStorage(plan.Certificate, target);
        AppendMemoryProvenance(provenance, storageResult.Evidence);
        if (storageResult.Status != ClrmdEvidenceStatus.Exact || storageResult.Value is null)
        {
            return NonExact(
                source,
                plan,
                storageResult.Status,
                storageResult.Issue,
                provenance,
                "STORAGE",
                rawMemoryReadReached: true);
        }

        var field = storageResult.Value;
        provenance.Add(CreateHashedStructureProvenance(
            "dump-member-chain-terminal-storage",
            field.ToCanonicalReplayProjection(),
            field.Address,
            field.Size,
            field.Size));

        return plan.Certificate.Decoder switch
        {
            ClrmdTerminalDecoderKind.String => EvaluateString(source, plan, target, field, provenance),
            ClrmdTerminalDecoderKind.Int32 => EvaluateInt32(source, plan, target, field, provenance),
            ClrmdTerminalDecoderKind.NullableInt32 =>
                EvaluateNullableInt32(source, plan, target, field, provenance),
            _ => CreateResult(
                source,
                plan,
                EvaluationCompletionStatus.Invalid,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Invalid,
                value: null,
                provenance,
                "QUERY_CHAIN_DECODER_INVALID",
                "The frozen terminal decoder is outside the admitted W6 profile.",
                rawMemoryReadReached: true,
                observedStringBoundReached: false),
        };
    }

    internal static EvaluationResult<DumpQueryValue> FromPreparationFailure(
        IDumpMemberChainEvidenceSource source,
        DumpQueryRootBinding rootBinding,
        ImmutableArray<EvaluationDeterministicBound> requestBounds,
        string requestSha256,
        DumpMemberChainPreparationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rootBinding);
        ArgumentNullException.ThrowIfNull(requestSha256);
        ArgumentNullException.ThrowIfNull(failure);
        var provenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
        if (source.Snapshot == rootBinding.Snapshot)
        {
            AppendMemoryProvenance(provenance, rootBinding.Evidence);
        }

        if (DumpQueryEngine.CreateRootSelectionProvenance(rootBinding) is { } rootSelection)
        {
            provenance.Add(rootSelection);
        }

        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-member-chain-request:sha256:{requestSha256}"));
        AppendMemoryProvenance(provenance, failure.Evidence);
        var bounds = MergeBounds(rootBinding.AppliedBounds, requestBounds, failure.AppliedBounds);
        var context = CreateContext(
            source,
            rootBinding,
            bounds,
            rawMemoryReadReached: !rootBinding.Evidence.IsEmpty || !failure.Evidence.IsEmpty,
            observedStringBoundReached: false);
        return EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            failure.Status == ClrmdEvidenceStatus.Invalid
                ? EvaluationCompletionStatus.Invalid
                : EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            MapEvidence(failure.Status),
            EvaluationEffectStatus.None,
            value: null,
            context,
            provenance.ToImmutable(),
            ImmutableArray.Create(new EvaluationDiagnostic(failure.Code, failure.Message)));
    }

    private static EvaluationResult<DumpQueryValue> EvaluateInt32(
        IDumpMemberChainEvidenceSource source,
        DumpMemberChainPlan plan,
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var result = source.ReadInt32Field(target, field);
        AppendMemoryProvenance(provenance, result.Evidence);
        if (result.Status == ClrmdEvidenceStatus.Exact && result.Value?.Value is int value)
        {
            return CreateResult(
                source,
                plan,
                EvaluationCompletionStatus.Completed,
                EvaluationCompleteness.Complete,
                EvaluationEvidenceStatus.Exact,
                DumpQueryValue.FromInt32(value),
                provenance,
                diagnosticCode: null,
                diagnosticMessage: null,
                rawMemoryReadReached: true,
                observedStringBoundReached: false);
        }

        return NonExact(
            source,
            plan,
            result.Status == ClrmdEvidenceStatus.Exact ? ClrmdEvidenceStatus.Invalid : result.Status,
            result.Status == ClrmdEvidenceStatus.Exact ? ClrmdValueIssue.InvalidData : result.Issue,
            provenance,
            "TERMINAL",
            rawMemoryReadReached: true);
    }

    private static EvaluationResult<DumpQueryValue> EvaluateNullableInt32(
        IDumpMemberChainEvidenceSource source,
        DumpMemberChainPlan plan,
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var result = source.ReadNullableInt32Field(target, field);
        AppendMemoryProvenance(provenance, result.Evidence);
        if (result.Status != ClrmdEvidenceStatus.Exact || result.Value is null)
        {
            return NonExact(
                source,
                plan,
                result.Status == ClrmdEvidenceStatus.Exact ? ClrmdEvidenceStatus.Invalid : result.Status,
                result.Status == ClrmdEvidenceStatus.Exact ? ClrmdValueIssue.InvalidData : result.Issue,
                provenance,
                "TERMINAL",
                rawMemoryReadReached: true);
        }

        DumpQueryValue value;
        if (result.Value.IsNull)
        {
            value = ApplyExactNull(plan, provenance);
        }
        else if (result.Value.Value is int scalar)
        {
            value = DumpQueryValue.FromInt32(scalar);
        }
        else
        {
            return CreateResult(
                source,
                plan,
                EvaluationCompletionStatus.Invalid,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Invalid,
                value: null,
                provenance,
                "QUERY_CHAIN_TERMINAL_INVALID",
                "The exact nullable Int32 terminal did not contain a supported null or scalar state.",
                rawMemoryReadReached: true,
                observedStringBoundReached: false);
        }

        return CreateResult(
            source,
            plan,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            value,
            provenance,
            diagnosticCode: null,
            diagnosticMessage: null,
            rawMemoryReadReached: true,
            observedStringBoundReached: false);
    }

    private static EvaluationResult<DumpQueryValue> EvaluateString(
        IDumpMemberChainEvidenceSource source,
        DumpMemberChainPlan plan,
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var result = source.ReadStringField(target, field, MaximumObservedStringCharacters);
        AppendMemoryProvenance(provenance, result.Evidence);
        var observedStringBoundReached = result.TargetLength is >= 0;
        if (result.Status == ClrmdEvidenceStatus.Exact)
        {
            var value = result.IsNull
                ? ApplyExactNull(plan, provenance)
                : result.Value is { } text
                    ? DumpQueryValue.FromString(text)
                    : null;
            if (value is null)
            {
                return CreateResult(
                    source,
                    plan,
                    EvaluationCompletionStatus.Invalid,
                    EvaluationCompleteness.None,
                    EvaluationEvidenceStatus.Invalid,
                    value: null,
                    provenance,
                    "QUERY_CHAIN_TERMINAL_INVALID",
                    "The exact string terminal did not contain a supported null or string state.",
                    rawMemoryReadReached: true,
                    observedStringBoundReached);
            }

            return CreateResult(
                source,
                plan,
                EvaluationCompletionStatus.Completed,
                EvaluationCompleteness.Complete,
                EvaluationEvidenceStatus.Exact,
                value,
                provenance,
                diagnosticCode: null,
                diagnosticMessage: null,
                rawMemoryReadReached: true,
                observedStringBoundReached);
        }

        if (result.Status == ClrmdEvidenceStatus.Partial && result.Value is { } prefix)
        {
            return CreateResult(
                source,
                plan,
                result.Issue == ClrmdValueIssue.LimitExceeded
                    ? EvaluationCompletionStatus.Completed
                    : EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.Partial,
                EvaluationEvidenceStatus.Partial,
                DumpQueryValue.FromString(prefix),
                provenance,
                DiagnosticCode("TERMINAL", result.Status, result.Issue),
                DiagnosticMessage("terminal storage", result.Status, result.Issue),
                rawMemoryReadReached: true,
                observedStringBoundReached);
        }

        return NonExact(
            source,
            plan,
            result.Status,
            result.Issue,
            provenance,
            "TERMINAL",
            rawMemoryReadReached: true,
            observedStringBoundReached);
    }

    private static DumpQueryValue ApplyExactNull(
        DumpMemberChainPlan plan,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        if (plan.FallbackKind != DumpMemberChainFallbackKind.None)
        {
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.Transformation,
                CoalesceProvenanceId));
        }

        return plan.FallbackKind switch
        {
            DumpMemberChainFallbackKind.None or DumpMemberChainFallbackKind.Null => DumpQueryValue.FromNull(),
            DumpMemberChainFallbackKind.Int32 => DumpQueryValue.FromInt32(plan.Int32Fallback!.Value),
            DumpMemberChainFallbackKind.String => DumpQueryValue.FromString(plan.StringFallback!),
            _ => throw new InvalidOperationException("The frozen member-chain fallback is invalid."),
        };
    }

    private static EvaluationResult<DumpQueryValue> NonExact(
        IDumpMemberChainEvidenceSource source,
        DumpMemberChainPlan plan,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        string stage,
        bool rawMemoryReadReached,
        bool observedStringBoundReached = false) =>
        CreateResult(
            source,
            plan,
            status == ClrmdEvidenceStatus.Invalid
                ? EvaluationCompletionStatus.Invalid
                : EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            MapEvidence(status),
            value: null,
            provenance,
            DiagnosticCode(stage, status, issue),
            DiagnosticMessage(stage.ToLowerInvariant(), status, issue),
            rawMemoryReadReached,
            observedStringBoundReached);

    private static EvaluationResult<DumpQueryValue> CreateResult(
        IDumpMemberChainEvidenceSource source,
        DumpMemberChainPlan plan,
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        DumpQueryValue? value,
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        string? diagnosticCode,
        string? diagnosticMessage,
        bool rawMemoryReadReached,
        bool observedStringBoundReached)
    {
        var bounds = MergeBounds(
            plan.RootBinding.AppliedBounds,
            plan.RequestBounds,
            plan.PreparationBounds);
        var context = CreateContext(
            source,
            plan.RootBinding,
            bounds,
            rawMemoryReadReached,
            observedStringBoundReached);
        var diagnostics = diagnosticCode is null
            ? ImmutableArray<EvaluationDiagnostic>.Empty
            : ImmutableArray.Create(new EvaluationDiagnostic(diagnosticCode, diagnosticMessage!));
        return EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            completion,
            completeness,
            evidence,
            EvaluationEffectStatus.None,
            value,
            context,
            provenance.ToImmutable(),
            diagnostics);
    }

    private static EvaluationEvidenceContext CreateContext(
        IDumpMemberChainEvidenceSource source,
        DumpQueryRootBinding rootBinding,
        ImmutableArray<EvaluationDeterministicBound> baseBounds,
        bool rawMemoryReadReached,
        bool observedStringBoundReached)
    {
        var additions = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>(2);
        if (rawMemoryReadReached)
        {
            additions.Add(new EvaluationDeterministicBound(RawMemoryReadBoundName, source.MaximumReadLength));
        }

        if (observedStringBoundReached)
        {
            additions.Add(new EvaluationDeterministicBound(
                ObservedStringBoundName,
                MaximumObservedStringCharacters));
        }

        var bounds = MergeBounds(baseBounds, additions.ToImmutable());
        var root = rootBinding.Root;
        var module = source.Snapshot == rootBinding.Snapshot && root is not null &&
            root.Snapshot == source.Snapshot && root.Module.Identity.Snapshot == source.Snapshot
                ? EvaluationEvidenceIdentity.CreateAvailable(root.Module.Identity.SourceId)
                : EvaluationEvidenceIdentity.Unavailable;
        return EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            EvaluationEvidenceIdentity.CreateAvailable(source.Snapshot.MemorySourceId),
            module,
            EvaluationFallback.None,
            bounds);
    }

    private static ImmutableArray<EvaluationProvenance>.Builder CreateBaseProvenance(
        IDumpMemberChainEvidenceSource source,
        DumpMemberChainPlan plan)
    {
        var provenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
        if (source.Snapshot == plan.RootBinding.Snapshot)
        {
            AppendMemoryProvenance(provenance, plan.RootBinding.Evidence);
        }

        if (DumpQueryEngine.CreateRootSelectionProvenance(plan.RootBinding) is { } selection)
        {
            provenance.Add(selection);
        }

        var root = plan.RootBinding.Root!;
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.RuntimeStructure,
            root.Snapshot.MemorySourceId,
            root.Address));
        AppendMemoryProvenance(provenance, plan.PreparationEvidence);
        provenance.Add(CreateHashedStructureProvenance(
            "dump-member-chain-certificate",
            plan.Certificate.ToCanonicalReplayProjection(),
            plan.Certificate.OuterField.Address));
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-member-chain-request:sha256:{plan.RequestSha256}"));
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-member-chain-expression:sha256:{plan.ExpressionIdentitySha256}"));
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-member-chain-plan:sha256:{plan.Sha256}"));
        return provenance;
    }

    private static EvaluationProvenance CreateHashedStructureProvenance(
        string domain,
        string projection,
        ulong address,
        int? requestedLength = null,
        int? observedLength = null) =>
        new(
            EvaluationProvenanceKind.RuntimeStructure,
            $"{domain}:sha256:{Hash(projection)}",
            address,
            requestedLength,
            observedLength);

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

        AppendMemoryProvenance(provenance, cumulativeEvidence[alreadyRetained..]);
    }

    private static ImmutableArray<EvaluationDeterministicBound> MergeBounds(
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
                if (bounds.TryGetValue(bound.Name, out var existing) && existing.Value != bound.Value)
                {
                    throw new InvalidOperationException(
                        $"The frozen plan contains conflicting values for deterministic bound '{bound.Name}'.");
                }

                bounds[bound.Name] = bound;
            }
        }

        return bounds.Values.OrderBy(static bound => bound.Name, StringComparer.Ordinal).ToImmutableArray();
    }

    private static EvaluationEvidenceStatus MapEvidence(ClrmdEvidenceStatus status) => status switch
    {
        ClrmdEvidenceStatus.Exact => EvaluationEvidenceStatus.Exact,
        ClrmdEvidenceStatus.Partial => EvaluationEvidenceStatus.Partial,
        ClrmdEvidenceStatus.Unavailable => EvaluationEvidenceStatus.Unavailable,
        ClrmdEvidenceStatus.Conflict => EvaluationEvidenceStatus.Conflict,
        ClrmdEvidenceStatus.Invalid => EvaluationEvidenceStatus.Invalid,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static string DiagnosticCode(
        string stage,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue) =>
        $"QUERY_CHAIN_{stage}_{(issue == ClrmdValueIssue.LimitExceeded ? "LIMIT_EXCEEDED" : status.ToString().ToUpperInvariant())}";

    private static string DiagnosticMessage(
        string stage,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue) => issue switch
    {
        ClrmdValueIssue.MemoryUnavailable =>
            $"Required {stage} bytes are incomplete or unavailable.",
        ClrmdValueIssue.SnapshotMismatch =>
            $"The {stage} evidence belongs to a different immutable snapshot.",
        ClrmdValueIssue.TypeMismatch =>
            $"The {stage} evidence conflicts with the frozen member-chain type identity.",
        ClrmdValueIssue.MemberShapeUnsupported =>
            $"The exact {stage} shape is outside the admitted fixed-depth profile.",
        ClrmdValueIssue.LimitExceeded =>
            $"A deterministic {stage} observation bound retained only a prefix.",
        ClrmdValueIssue.InvalidData =>
            $"The {stage} evidence violates a supported layout invariant.",
        _ => status switch
        {
            ClrmdEvidenceStatus.Partial => $"The {stage} evidence is incomplete.",
            ClrmdEvidenceStatus.Unavailable => $"The required {stage} evidence is unavailable.",
            ClrmdEvidenceStatus.Conflict => $"Available {stage} evidence disagrees.",
            ClrmdEvidenceStatus.Invalid => $"The {stage} evidence is structurally invalid.",
            _ => $"The {stage} operation returned an incoherent exact result.",
        },
    };

    private static string Hash(string projection) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(projection))).ToLowerInvariant();
}
