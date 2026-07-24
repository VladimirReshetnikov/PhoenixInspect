using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Projects adapter-local evidence into the common host-facing observation envelope.</summary>
public static class ClrmdEvaluationResultExtensions
{
    /// <summary>
    /// Preserves the distinction between an exact null string, an exact non-null string, a evidence-backed prefix, and
    /// missing string evidence in the common observation envelope.
    /// </summary>
    /// <param name="observation">The bounded string-field observation to project.</param>
    /// <returns>
    /// A complete observation for exact null or non-null values, a partial observation only when a prefix is
    /// present, or a valueless observation for unavailable evidence.
    /// </returns>
    public static EvaluationResult<ClrmdStringFieldObservation> ToObservationResult(
        this ClrmdStringFieldObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var provenance = observation.Evidence
            .Select(static read => new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.BytesRead))
            .ToImmutableArray();
        var diagnostics = observation.Issue == ClrmdValueIssue.None
            ? ImmutableArray<EvaluationDiagnostic>.Empty
            : ImmutableArray.Create(new EvaluationDiagnostic(
                GetDiagnosticCode(observation.Issue),
                GetDiagnosticMessage(observation.Issue)));
        var completeness = observation.Status == ClrmdEvidenceStatus.Exact
            ? EvaluationCompleteness.Complete
            : observation.Status == ClrmdEvidenceStatus.Partial && observation.Value is not null
                ? EvaluationCompleteness.Partial
                : EvaluationCompleteness.None;

        return EvaluationResult<ClrmdStringFieldObservation>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            completeness,
            MapEvidence(observation.Status),
            EvaluationEffectStatus.None,
            completeness == EvaluationCompleteness.None ? null : observation,
            provenance,
            diagnostics);
    }

    /// <summary>
    /// Projects adapter evidence status, diagnostics, and counted memory ranges into a neutral observation envelope.
    /// </summary>
    /// <typeparam name="TValue">The immutable adapter value projection.</typeparam>
    /// <param name="result">The adapter result to project.</param>
    /// <returns>
    /// An observation result with no effects and stable issue diagnostics. For integer and nullable-integer field
    /// observations, retained partial bytes do not constitute a scalar answer unless the integer was decoded; an
    /// exactly observed nullable absence is itself a complete answer. Operation-specific deterministic bounds remain
    /// available through <see cref="ClrmdEvidenceResult{TValue}.AppliedBounds"/> for the caller to place in its evidence
    /// context.
    /// </returns>
    public static EvaluationResult<TValue> ToObservationResult<TValue>(this ClrmdEvidenceResult<TValue> result)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(result);
        var hasAnswer = result.Value switch
        {
            ClrmdInt32FieldObservation integerObservation => integerObservation.Value is not null,
            ClrmdNullableInt32FieldObservation nullableObservation =>
                nullableObservation.IsNull || nullableObservation.Value is not null,
            _ => result.HasValue,
        };
        return ProjectEvidenceResult(result, hasAnswer);
    }

    private static EvaluationResult<TValue> ProjectEvidenceResult<TValue>(
        ClrmdEvidenceResult<TValue> result,
        bool hasAnswer)
        where TValue : class
    {
        var provenance = result.Evidence
            .Select(static read => new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.BytesRead))
            .ToImmutableArray();
        var diagnostics = result.Issue == ClrmdValueIssue.None
            ? ImmutableArray<EvaluationDiagnostic>.Empty
            : ImmutableArray.Create(new EvaluationDiagnostic(
                GetDiagnosticCode(result.Issue),
                GetDiagnosticMessage(result.Issue)));

        var completeness = result.Status == ClrmdEvidenceStatus.Exact && hasAnswer
            ? EvaluationCompleteness.Complete
            : hasAnswer
                ? EvaluationCompleteness.Partial
                : EvaluationCompleteness.None;

        return EvaluationResult<TValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            completeness,
            MapEvidence(result.Status),
            EvaluationEffectStatus.None,
            completeness == EvaluationCompleteness.None ? null : result.Value,
            provenance,
            diagnostics);
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

    private static string GetDiagnosticCode(ClrmdValueIssue issue) => issue switch
    {
        ClrmdValueIssue.SnapshotMismatch => "DUMP_SNAPSHOT_MISMATCH",
        ClrmdValueIssue.ModuleUnavailable => "DUMP_MODULE_UNAVAILABLE",
        ClrmdValueIssue.MetadataUnavailable => "DUMP_METADATA_UNAVAILABLE",
        ClrmdValueIssue.ArtifactUnavailable => "DUMP_ARTIFACT_UNAVAILABLE",
        ClrmdValueIssue.ArtifactInvalid => "DUMP_ARTIFACT_INVALID",
        ClrmdValueIssue.RuntimeUnsupported => "DUMP_RUNTIME_UNSUPPORTED",
        ClrmdValueIssue.ObjectUnavailable => "DUMP_OBJECT_UNAVAILABLE",
        ClrmdValueIssue.FieldUnavailable => "DUMP_FIELD_UNAVAILABLE",
        ClrmdValueIssue.TypeUnavailable => "DUMP_TYPE_UNAVAILABLE",
        ClrmdValueIssue.MethodUnavailable => "DUMP_METHOD_UNAVAILABLE",
        ClrmdValueIssue.AmbiguousMatch => "DUMP_AMBIGUOUS_MATCH",
        ClrmdValueIssue.MethodBodyUnavailable => "DUMP_METHOD_BODY_UNAVAILABLE",
        ClrmdValueIssue.MethodBodyLayoutUnsupported => "DUMP_METHOD_BODY_LAYOUT_UNSUPPORTED",
        ClrmdValueIssue.MethodHeaderUnsupported => "DUMP_METHOD_HEADER_UNSUPPORTED",
        ClrmdValueIssue.MethodSectionUnsupported => "DUMP_METHOD_SECTION_UNSUPPORTED",
        ClrmdValueIssue.MethodIdentityMismatch => "DUMP_METHOD_IDENTITY_MISMATCH",
        ClrmdValueIssue.TypeMismatch => "DUMP_TYPE_MISMATCH",
        ClrmdValueIssue.MemoryUnavailable => "DUMP_MEMORY_UNAVAILABLE",
        ClrmdValueIssue.InvalidData => "DUMP_INVALID_DATA",
        ClrmdValueIssue.LimitExceeded => "DUMP_LIMIT_EXCEEDED",
        ClrmdValueIssue.None => throw new ArgumentOutOfRangeException(nameof(issue)),
        _ => throw new ArgumentOutOfRangeException(nameof(issue)),
    };

    private static string GetDiagnosticMessage(ClrmdValueIssue issue) => issue switch
    {
        ClrmdValueIssue.SnapshotMismatch => "Evidence belongs to a different immutable dump snapshot.",
        ClrmdValueIssue.ModuleUnavailable => "The selected runtime module is unavailable.",
        ClrmdValueIssue.MetadataUnavailable => "The selected runtime module has no complete metadata image.",
        ClrmdValueIssue.ArtifactUnavailable => "The dump artifact could not be opened.",
        ClrmdValueIssue.ArtifactInvalid => "The dump artifact is structurally invalid.",
        ClrmdValueIssue.RuntimeUnsupported => "The dump runtime configuration is outside the supported profile.",
        ClrmdValueIssue.ObjectUnavailable => "The selected runtime object is unavailable.",
        ClrmdValueIssue.FieldUnavailable => "The requested runtime field is unavailable.",
        ClrmdValueIssue.TypeUnavailable => "The requested runtime type is unavailable.",
        ClrmdValueIssue.MethodUnavailable => "The requested runtime method is unavailable.",
        ClrmdValueIssue.AmbiguousMatch => "More than one runtime candidate matched the request.",
        ClrmdValueIssue.MethodBodyUnavailable => "The runtime method has no supported IL body evidence.",
        ClrmdValueIssue.MethodBodyLayoutUnsupported =>
            "The runtime module layout cannot yet map a MethodDef RVA to counted target memory.",
        ClrmdValueIssue.MethodHeaderUnsupported =>
            "The dump method header uses an extensible encoding outside the current profile.",
        ClrmdValueIssue.MethodSectionUnsupported =>
            "The dump method body declares an unsupported extra-section kind.",
        ClrmdValueIssue.MethodIdentityMismatch =>
            "Runtime-selected method identity conflicts with counted dump metadata.",
        ClrmdValueIssue.TypeMismatch => "The selected runtime evidence has an incompatible type.",
        ClrmdValueIssue.MemoryUnavailable => "Required dump-memory bytes are incomplete or unavailable.",
        ClrmdValueIssue.InvalidData => "Captured runtime evidence violates a supported layout invariant.",
        ClrmdValueIssue.LimitExceeded => "A deterministic evidence bound truncated the operation.",
        ClrmdValueIssue.None => throw new ArgumentOutOfRangeException(nameof(issue)),
        _ => throw new ArgumentOutOfRangeException(nameof(issue)),
    };
}
