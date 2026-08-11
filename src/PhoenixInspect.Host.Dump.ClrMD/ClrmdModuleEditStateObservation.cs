using System.Buffers.Binary;
using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Carries one module's physically observed Edit-and-Continue enablement and applied-generation state.
/// </summary>
/// <remarks>
/// The runtime contract descriptor supplies the offsets of <c>Module.Flags</c> and
/// <c>Module.DynamicMetadata</c>. The raw generation counter is read one target pointer past the latter field, as
/// established by the E1 physical-truth fixture. Only an exact observation exposes decoded facts. In particular,
/// callers must treat a non-exact <see cref="Status"/> as unknown and fail closed rather than interpreting nullable
/// properties as an unedited module.
/// </remarks>
public sealed class ClrmdModuleEditStateObservation
{
    /// <summary>
    /// The module-flags bit measured to identify a module loaded with Edit-and-Continue enabled.
    /// </summary>
    public const uint EditEnabledFlag = 0x8;

    private ClrmdModuleEditStateObservation(
        ClrmdRuntimeModuleIdentity module,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        uint? moduleFlags,
        ulong? generationCounter,
        MemoryReadResult? moduleFlagsMemory,
        MemoryReadResult? generationCounterMemory)
    {
        if ((status == ClrmdEvidenceStatus.Exact) != (issue == ClrmdValueIssue.None))
        {
            throw new ArgumentException("Only exact edit-state observations may use the None issue code.", nameof(issue));
        }

        if (status == ClrmdEvidenceStatus.Exact &&
            (moduleFlags is null || generationCounter is null ||
             moduleFlagsMemory?.Status != MemoryReadStatus.Exact ||
             generationCounterMemory?.Status != MemoryReadStatus.Exact))
        {
            throw new ArgumentException("An exact edit-state observation requires both exact physical reads.");
        }

        if (status != ClrmdEvidenceStatus.Exact && (moduleFlags is not null || generationCounter is not null))
        {
            throw new ArgumentException("A non-exact edit-state observation cannot expose decoded module facts.");
        }

        Module = module;
        Status = status;
        Issue = issue;
        ModuleFlags = moduleFlags;
        GenerationCounter = generationCounter;
        ModuleFlagsMemory = moduleFlagsMemory;
        GenerationCounterMemory = generationCounterMemory;
    }

    /// <summary>Gets the snapshot-scoped runtime identity of the observed module.</summary>
    public ClrmdRuntimeModuleIdentity Module { get; }

    /// <summary>Gets whether the required physical facts were exact, unavailable, conflicting, or invalid.</summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>Gets the typed issue of a non-exact observation.</summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>
    /// Gets the exact raw <c>Module.Flags</c> word, or <see langword="null"/> when the observation is non-exact.
    /// </summary>
    public uint? ModuleFlags { get; }

    /// <summary>
    /// Gets the exact raw pointer-sized generation counter, or <see langword="null"/> when the observation is
    /// non-exact. On a module without <see cref="EditEnabledFlag"/>, this slot is retained but not interpreted.
    /// </summary>
    public ulong? GenerationCounter { get; }

    /// <summary>
    /// Gets whether exact flags prove the module was loaded edit-enabled, or <see langword="null"/> for a non-exact
    /// observation.
    /// </summary>
    public bool? IsEditEnabled => ModuleFlags is uint flags ? (flags & EditEnabledFlag) != 0 : null;

    /// <summary>
    /// Gets the applied-generation count, or <see langword="null"/> for a non-exact observation. The count is the
    /// generation counter minus one only under edit enablement; a disabled module has count zero because the runtime
    /// cannot admit an edit and the same physical slot is not interpreted as a counter.
    /// </summary>
    public ulong? AppliedGenerationCount =>
        IsEditEnabled switch
        {
            true => GenerationCounter!.Value - 1,
            false => 0,
            null => null,
        };

    /// <summary>
    /// Gets whether exact evidence proves at least one applied edit, or <see langword="null"/> when edit state is
    /// unavailable or invalid.
    /// </summary>
    public bool? HasAppliedEdits => AppliedGenerationCount is ulong count ? count != 0 : null;

    /// <summary>
    /// Gets the counted four-byte flags read when that read was attempted, including partial or unavailable evidence.
    /// </summary>
    public MemoryReadResult? ModuleFlagsMemory { get; }

    /// <summary>
    /// Gets the counted pointer-width generation-counter read when that read was attempted, including partial or
    /// unavailable evidence.
    /// </summary>
    public MemoryReadResult? GenerationCounterMemory { get; }

    internal static ClrmdEvidenceResult<ClrmdModuleEditStateObservation> Project(
        ClrmdRuntimeModuleIdentity module,
        int pointerSize,
        MemoryReadResult moduleFlagsMemory,
        MemoryReadResult generationCounterMemory)
    {
        ArgumentNullException.ThrowIfNull(moduleFlagsMemory);
        ArgumentNullException.ThrowIfNull(generationCounterMemory);

        var evidence = ImmutableArray.Create(moduleFlagsMemory, generationCounterMemory);
        if (pointerSize is not (sizeof(uint) or sizeof(ulong)) ||
            moduleFlagsMemory.SourceId != module.Snapshot.MemorySourceId ||
            generationCounterMemory.SourceId != module.Snapshot.MemorySourceId ||
            moduleFlagsMemory.RequestedLength != sizeof(uint) ||
            generationCounterMemory.RequestedLength != pointerSize)
        {
            return Stop(
                module,
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                moduleFlagsMemory,
                generationCounterMemory,
                evidence);
        }

        if (moduleFlagsMemory.Status != MemoryReadStatus.Exact ||
            generationCounterMemory.Status != MemoryReadStatus.Exact)
        {
            return Stop(
                module,
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable,
                moduleFlagsMemory,
                generationCounterMemory,
                evidence);
        }

        var moduleFlags = BinaryPrimitives.ReadUInt32LittleEndian(moduleFlagsMemory.Bytes.AsSpan());
        var generationCounter = pointerSize == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(generationCounterMemory.Bytes.AsSpan())
            : BinaryPrimitives.ReadUInt64LittleEndian(generationCounterMemory.Bytes.AsSpan());
        if ((moduleFlags & EditEnabledFlag) != 0 && generationCounter == 0)
        {
            return Stop(
                module,
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.EditGenerationCounterUnderflow,
                moduleFlagsMemory,
                generationCounterMemory,
                evidence);
        }

        var observation = new ClrmdModuleEditStateObservation(
            module,
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            moduleFlags,
            generationCounter,
            moduleFlagsMemory,
            generationCounterMemory);
        return ClrmdEvidenceResult<ClrmdModuleEditStateObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            observation,
            evidence);
    }

    internal static ClrmdEvidenceResult<ClrmdModuleEditStateObservation> Stop(
        ClrmdRuntimeModuleIdentity module,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        MemoryReadResult? moduleFlagsMemory = null,
        MemoryReadResult? generationCounterMemory = null,
        ImmutableArray<MemoryReadResult> evidence = default)
    {
        if (status == ClrmdEvidenceStatus.Exact || issue == ClrmdValueIssue.None)
        {
            throw new ArgumentException("An edit-state stop requires a non-exact status and issue.", nameof(status));
        }

        var observation = new ClrmdModuleEditStateObservation(
            module,
            status,
            issue,
            null,
            null,
            moduleFlagsMemory,
            generationCounterMemory);
        return ClrmdEvidenceResult<ClrmdModuleEditStateObservation>.Create(
            status,
            issue,
            observation,
            evidence.IsDefault ? ImmutableArray<MemoryReadResult>.Empty : evidence);
    }
}
