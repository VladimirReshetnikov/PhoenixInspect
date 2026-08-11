using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Projects the Host's one cached edit-state decision without reimplementing its precedence.</summary>
internal static class ModuleEditAdmissionPolicy
{
    internal static string Code(ClrmdModuleEditAdmission admission) => admission.Disposition switch
    {
        ClrmdModuleEditAdmissionDisposition.EditedModulesNotComposed =>
            "DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED",
        ClrmdModuleEditAdmissionDisposition.Unavailable => "DUMP_MODULE_EDIT_STATE_UNAVAILABLE",
        ClrmdModuleEditAdmissionDisposition.Invalid => "DUMP_MODULE_EDIT_STATE_INVALID",
        _ => throw new ArgumentException("An admitted session has no refusal diagnostic.", nameof(admission)),
    };

    internal static string Message(ClrmdModuleEditAdmission admission) => admission.Disposition switch
    {
        ClrmdModuleEditAdmissionDisposition.EditedModulesNotComposed =>
            "At least one loaded module has applied Edit-and-Continue generations, which this evaluator does not compose.",
        ClrmdModuleEditAdmissionDisposition.Unavailable =>
            "The edit state of every loaded module could not be established exactly, so base-image authority is unavailable.",
        ClrmdModuleEditAdmissionDisposition.Invalid =>
            "A loaded module's physical edit-state evidence violated the supported runtime contract.",
        _ => throw new ArgumentException("An admitted session has no refusal diagnostic.", nameof(admission)),
    };

    internal static EvaluationEvidenceStatus Evidence(ClrmdModuleEditAdmission admission) =>
        admission.Disposition switch
        {
            ClrmdModuleEditAdmissionDisposition.EditedModulesNotComposed => EvaluationEvidenceStatus.Exact,
            ClrmdModuleEditAdmissionDisposition.Unavailable => admission.Status switch
            {
                ClrmdEvidenceStatus.Partial => EvaluationEvidenceStatus.Partial,
                ClrmdEvidenceStatus.Conflict => EvaluationEvidenceStatus.Conflict,
                ClrmdEvidenceStatus.Invalid => EvaluationEvidenceStatus.Invalid,
                _ => EvaluationEvidenceStatus.Unavailable,
            },
            ClrmdModuleEditAdmissionDisposition.Invalid => EvaluationEvidenceStatus.Invalid,
            _ => throw new ArgumentException("An admitted session has no refusal evidence status.", nameof(admission)),
        };

    internal static EvaluationCompletionStatus Completion(ClrmdModuleEditAdmission admission) =>
        admission.Disposition == ClrmdModuleEditAdmissionDisposition.Invalid
            ? EvaluationCompletionStatus.Invalid
            : EvaluationCompletionStatus.Blocked;

    internal static void AppendProvenance(
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        ClrmdModuleEditAdmission admission)
    {
        foreach (var read in admission.Evidence)
        {
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.Bytes.Length));
        }

        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-module-edit-admission:{(int)admission.Disposition}:{admission.InspectedModuleCount}"));
    }
}
