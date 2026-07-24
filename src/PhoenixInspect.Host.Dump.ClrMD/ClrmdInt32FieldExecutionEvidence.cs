using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Freezes one exact dump object, metadata field, runtime storage descriptor, and exact or explained-missing Int32
/// memory observation admitted for counterfactual execution.
/// </summary>
/// <remarks>
/// Instances are issued only by <see cref="ClrmdDumpExecutionResolver.CorrelateInt32FieldObservation"/> after the
/// owner selection, runtime layout, metadata identity, target address, raw-read provenance, evidence disposition,
/// and optional scalar have been validated as one coherent tuple. Partial and unavailable evidence never exposes an
/// invented value. This draft W4 adapter remains limited to ordinary instance <see cref="int"/> fields declared by
/// the root receiver type.
/// </remarks>
public sealed class ClrmdInt32FieldExecutionEvidence
{
    internal ClrmdInt32FieldExecutionEvidence(
        ClrmdRuntimeModuleIdentity runtimeModule,
        ResolvedMethodDefinition rootMethod,
        ResolvedField field,
        ClrmdHeapObjectSearchResult ownerSearch,
        ClrmdHeapObjectInfo owner,
        ClrmdInt32FieldObservation observation,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        int? exactValue)
    {
        RuntimeModule = runtimeModule;
        RootMethod = rootMethod;
        Field = field;
        OwnerSearch = ownerSearch;
        Owner = owner;
        Observation = observation;
        Status = status;
        Issue = issue;
        ExactValue = exactValue;
        OwnerEvidenceIdentity = ClrmdExactInt32FieldExecutionEvidence.CreateOwnerEvidenceIdentity(
            runtimeModule,
            owner.Address,
            owner.MethodTable);
    }

    /// <summary>Gets the snapshot-scoped runtime module whose counted metadata projected the descriptors.</summary>
    public ClrmdRuntimeModuleIdentity RuntimeModule { get; }

    /// <summary>Gets the immutable exact root method and activation signature used to correlate the owner type.</summary>
    public ResolvedMethodDefinition RootMethod { get; }

    /// <summary>Gets the same-module ordinary instance Int32 FieldDef correlated with runtime storage.</summary>
    public ResolvedField Field { get; }

    /// <summary>Gets the exact bounded strong-handle search that uniquely selected <see cref="Owner"/>.</summary>
    public ClrmdHeapObjectSearchResult OwnerSearch { get; }

    /// <summary>Gets the uniquely selected snapshot object whose instance field was observed.</summary>
    public ClrmdHeapObjectInfo Owner { get; }

    /// <summary>Gets the runtime field descriptor and its sole counted four-byte-range memory read.</summary>
    public ClrmdInt32FieldObservation Observation { get; }

    /// <summary>Gets whether the counted field read was exact, partial, or unavailable.</summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>
    /// Gets <see cref="ClrmdValueIssue.None"/> for exact evidence or
    /// <see cref="ClrmdValueIssue.MemoryUnavailable"/> for a partial or unavailable read.
    /// </summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>
    /// Gets the exact little-endian Int32 decoded from four complete bytes, or <see langword="null"/> when
    /// <see cref="Status"/> is partial or unavailable.
    /// </summary>
    public int? ExactValue { get; }

    /// <summary>
    /// Gets the bounded versioned identity of the selected dump object for an explicit later import boundary.
    /// </summary>
    /// <remarks>
    /// This existing owner identity binds snapshot, runtime-module instance, object address, and method table. A
    /// product request that claims complete root-selection replay must additionally bind the search predicate,
    /// traversal bounds, root-slot identity, and raw selection reads.
    /// </remarks>
    public string OwnerEvidenceIdentity { get; }
}
