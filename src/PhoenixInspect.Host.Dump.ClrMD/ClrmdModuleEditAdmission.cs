using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Classifies whether base-image semantic evidence may be consulted for one dump session.</summary>
public enum ClrmdModuleEditAdmissionDisposition
{
    /// <summary>Every loaded managed module was observed exactly and has no applied edit generation.</summary>
    ExactUnedited = 1,

    /// <summary>At least one loaded managed module has an exactly observed applied edit generation.</summary>
    EditedModulesNotComposed = 2,

    /// <summary>At least one module's edit state could not be established exactly.</summary>
    Unavailable = 3,

    /// <summary>At least one module's edit-state evidence violated an admitted physical invariant.</summary>
    Invalid = 4,
}

/// <summary>
/// Retains the cached full-session decision that gates every base-image semantic evaluation path.
/// </summary>
/// <remarks>
/// The scan always visits the complete immutable module catalog in catalog order. Its disposition precedence is
/// <see cref="ClrmdModuleEditAdmissionDisposition.Invalid"/>, then
/// <see cref="ClrmdModuleEditAdmissionDisposition.EditedModulesNotComposed"/>, then
/// <see cref="ClrmdModuleEditAdmissionDisposition.Unavailable"/>, independent of the order in which modules expose
/// those states. <see cref="StoppedModule"/> identifies the first module in catalog order at the winning precedence.
/// </remarks>
public sealed class ClrmdModuleEditAdmission
{
    internal ClrmdModuleEditAdmission(
        ClrmdModuleEditAdmissionDisposition disposition,
        ClrmdModuleInfo? stoppedModule,
        ClrmdModuleEditStateObservation? stoppedObservation,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        int inspectedModuleCount,
        int totalModuleCount,
        ImmutableArray<MemoryReadResult> evidence)
    {
        if (!Enum.IsDefined(disposition) || !Enum.IsDefined(status) || !Enum.IsDefined(issue) ||
            inspectedModuleCount < 0 || totalModuleCount < 0 || inspectedModuleCount != totalModuleCount ||
            disposition == ClrmdModuleEditAdmissionDisposition.ExactUnedited &&
                (totalModuleCount == 0 || stoppedModule is not null || stoppedObservation is not null ||
                 status != ClrmdEvidenceStatus.Exact || issue != ClrmdValueIssue.None) ||
            disposition == ClrmdModuleEditAdmissionDisposition.EditedModulesNotComposed &&
                (stoppedModule is null || stoppedObservation is not { Status: ClrmdEvidenceStatus.Exact,
                    HasAppliedEdits: true } || status != ClrmdEvidenceStatus.Exact || issue != ClrmdValueIssue.None) ||
            (disposition is ClrmdModuleEditAdmissionDisposition.Unavailable or
                ClrmdModuleEditAdmissionDisposition.Invalid) &&
                (status == ClrmdEvidenceStatus.Exact || issue == ClrmdValueIssue.None))
        {
            throw new ArgumentException("The module edit-state admission shape is incoherent.");
        }

        Disposition = disposition;
        StoppedModule = stoppedModule;
        StoppedObservation = stoppedObservation;
        Status = status;
        Issue = issue;
        InspectedModuleCount = inspectedModuleCount;
        TotalModuleCount = totalModuleCount;
        Evidence = evidence.IsDefault ? [] : evidence;
    }

    /// <summary>Gets the conservative full-session admission disposition.</summary>
    public ClrmdModuleEditAdmissionDisposition Disposition { get; }

    /// <summary>Gets whether base-image metadata, storage, objects, and method bodies may be consulted.</summary>
    public bool IsAdmitted => Disposition == ClrmdModuleEditAdmissionDisposition.ExactUnedited;

    /// <summary>Gets the first module at the winning refusal precedence, or null for a zero-module stop.</summary>
    public ClrmdModuleInfo? StoppedModule { get; }

    /// <summary>Gets the retained module observation at the winning refusal precedence, when available.</summary>
    public ClrmdModuleEditStateObservation? StoppedObservation { get; }

    /// <summary>Gets the evidence status paired with the winning disposition.</summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>Gets the Host issue paired with the winning disposition.</summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>Gets the number of catalog modules inspected before the decision was cached.</summary>
    public int InspectedModuleCount { get; }

    /// <summary>Gets the immutable catalog module count.</summary>
    public int TotalModuleCount { get; }

    /// <summary>Gets all physical edit-state reads reached, in catalog and execution order.</summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }
}

public sealed partial class ClrmdDumpSession
{
    private readonly object _moduleEditAdmissionCacheGate = new();
    private ClrmdModuleEditAdmission? _moduleEditAdmissionCache;

    /// <summary>
    /// Reads and caches the conservative full-session admission required before base-image semantic evaluation.
    /// </summary>
    /// <returns>
    /// Exact-unedited only when every catalog module has exact physical edit-state evidence and zero applied
    /// generations. A zero-module session is unavailable. Every other state is a typed fail-closed disposition.
    /// </returns>
    public ClrmdModuleEditAdmission ReadModuleEditAdmission()
    {
        ThrowIfDisposed();
        lock (_moduleEditAdmissionCacheGate)
        {
            return _moduleEditAdmissionCache ??= AcquireModuleEditAdmission();
        }
    }

    private ClrmdModuleEditAdmission AcquireModuleEditAdmission()
    {
        if (Modules.IsEmpty)
        {
            return new ClrmdModuleEditAdmission(
                ClrmdModuleEditAdmissionDisposition.Unavailable,
                stoppedModule: null,
                stoppedObservation: null,
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable,
                inspectedModuleCount: 0,
                totalModuleCount: 0,
                evidence: []);
        }

        var evidence = ImmutableArray.CreateBuilder<MemoryReadResult>();
        (ClrmdModuleInfo Module, ClrmdModuleEditStateObservation? Observation,
            ClrmdEvidenceStatus Status, ClrmdValueIssue Issue)? invalid = null;
        (ClrmdModuleInfo Module, ClrmdModuleEditStateObservation Observation)? edited = null;
        (ClrmdModuleInfo Module, ClrmdModuleEditStateObservation? Observation,
            ClrmdEvidenceStatus Status, ClrmdValueIssue Issue)? unavailable = null;

        foreach (var module in Modules)
        {
            var result = ReadModuleEditState(module);
            evidence.AddRange(result.Evidence);
            if (result.Status == ClrmdEvidenceStatus.Invalid ||
                result.Status == ClrmdEvidenceStatus.Exact && result.Value is null)
            {
                invalid ??= (
                    module,
                    result.Value,
                    ClrmdEvidenceStatus.Invalid,
                    result.Issue == ClrmdValueIssue.None ? ClrmdValueIssue.InvalidData : result.Issue);
            }
            else if (result.Status == ClrmdEvidenceStatus.Exact && result.Value is { HasAppliedEdits: true })
            {
                edited ??= (module, result.Value);
            }
            else if (result.Status != ClrmdEvidenceStatus.Exact || result.Value is null ||
                     result.Value.HasAppliedEdits is null)
            {
                unavailable ??= (
                    module,
                    result.Value,
                    result.Status == ClrmdEvidenceStatus.Exact
                        ? ClrmdEvidenceStatus.Unavailable
                        : result.Status,
                    result.Issue == ClrmdValueIssue.None ? ClrmdValueIssue.RuntimeContractUnavailable : result.Issue);
            }
        }

        if (invalid is { } invalidStop)
        {
            return new ClrmdModuleEditAdmission(
                ClrmdModuleEditAdmissionDisposition.Invalid,
                invalidStop.Module,
                invalidStop.Observation,
                invalidStop.Status,
                invalidStop.Issue,
                Modules.Length,
                Modules.Length,
                evidence.ToImmutable());
        }

        if (edited is { } editedStop)
        {
            return new ClrmdModuleEditAdmission(
                ClrmdModuleEditAdmissionDisposition.EditedModulesNotComposed,
                editedStop.Module,
                editedStop.Observation,
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                Modules.Length,
                Modules.Length,
                evidence.ToImmutable());
        }

        if (unavailable is { } unavailableStop)
        {
            return new ClrmdModuleEditAdmission(
                ClrmdModuleEditAdmissionDisposition.Unavailable,
                unavailableStop.Module,
                unavailableStop.Observation,
                unavailableStop.Status,
                unavailableStop.Issue,
                Modules.Length,
                Modules.Length,
                evidence.ToImmutable());
        }

        return new ClrmdModuleEditAdmission(
            ClrmdModuleEditAdmissionDisposition.ExactUnedited,
            stoppedModule: null,
            stoppedObservation: null,
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            Modules.Length,
            Modules.Length,
            evidence.ToImmutable());
    }
}
