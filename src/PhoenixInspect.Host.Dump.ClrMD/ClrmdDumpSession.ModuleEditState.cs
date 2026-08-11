using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

public sealed partial class ClrmdDumpSession
{
    private readonly object _moduleEditStateCacheGate = new();
    private readonly Dictionary<ClrmdRuntimeModuleIdentity, ClrmdEvidenceResult<ClrmdModuleEditStateObservation>>
        _moduleEditStateCache = [];

    /// <summary>
    /// Reads one catalog module's physical Edit-and-Continue enablement and applied-generation state.
    /// </summary>
    /// <param name="module">A module selected from this session's immutable <see cref="Modules"/> catalog.</param>
    /// <returns>
    /// An exact observation when both descriptor-derived memory reads are complete and coherent; otherwise a typed
    /// unavailable, conflicting, or invalid result that also carries the non-exact observation and any attempted raw
    /// reads. Callers enforcing base-image authority must fail closed for every status other than
    /// <see cref="ClrmdEvidenceStatus.Exact"/>.
    /// </returns>
    /// <remarks>
    /// The flags address is the module structure plus the runtime contract descriptor's <c>Module.Flags</c> offset.
    /// The generation counter is one target pointer past its <c>Module.DynamicMetadata</c> offset, as measured by the
    /// E1 physical-truth fixture. Descriptor absence never prevents the containing session from opening.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The session has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> is <see langword="null"/>.</exception>
    public ClrmdEvidenceResult<ClrmdModuleEditStateObservation> ReadModuleEditState(ClrmdModuleInfo module)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(module);

        if (module.Identity.Snapshot != Snapshot)
        {
            return ClrmdModuleEditStateObservation.Stop(
                module.Identity,
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (!_runtimeModules.ContainsKey(module.Identity) || module.Identity.ModuleAddress == 0)
        {
            return ClrmdModuleEditStateObservation.Stop(
                module.Identity,
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        lock (_moduleEditStateCacheGate)
        {
            if (_moduleEditStateCache.TryGetValue(module.Identity, out var cached))
            {
                return cached;
            }

            var acquired = AcquireModuleEditState(module.Identity);
            _moduleEditStateCache.Add(module.Identity, acquired);
            return acquired;
        }
    }

    /// <summary>
    /// Performs the sole physical acquisition for a catalog module. The public entry point serializes and caches this
    /// operation, so repeated Watch/Immediate evaluations in one immutable or suspended session reuse the same object
    /// and never multiply target reads.
    /// </summary>
    private ClrmdEvidenceResult<ClrmdModuleEditStateObservation> AcquireModuleEditState(
        ClrmdRuntimeModuleIdentity module)
    {

        if (_runtimeContractDescriptorRead.Status != ClrmdEvidenceStatus.Exact ||
            _runtimeContractDescriptorRead.Descriptor is not { } descriptor)
        {
            return ClrmdModuleEditStateObservation.Stop(
                module,
                _runtimeContractDescriptorRead.Status,
                _runtimeContractDescriptorRead.Issue);
        }

        if (!descriptor.TryGetFieldOffset("Module", "Flags", out var moduleFlagsOffset) ||
            !descriptor.TryGetFieldOffset("Module", "DynamicMetadata", out var dynamicMetadataOffset))
        {
            return ClrmdModuleEditStateObservation.Stop(
                module,
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.RuntimeContractUnavailable);
        }

        if (_memory.PointerSize != descriptor.PointerSize ||
            !TryAdd(module.ModuleAddress, moduleFlagsOffset, out var moduleFlagsAddress) ||
            !TryAdd(dynamicMetadataOffset, descriptor.PointerSize, out var generationCounterOffset) ||
            !TryAdd(module.ModuleAddress, generationCounterOffset, out var generationCounterAddress) ||
            moduleFlagsAddress > ulong.MaxValue - (sizeof(uint) - 1UL) ||
            generationCounterAddress > ulong.MaxValue - checked((ulong)(descriptor.PointerSize - 1)))
        {
            return ClrmdModuleEditStateObservation.Stop(
                module,
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        var moduleFlagsMemory = _memory.Read(moduleFlagsAddress, sizeof(uint));
        if (moduleFlagsMemory.Status != MemoryReadStatus.Exact)
        {
            return ClrmdModuleEditStateObservation.Stop(
                module,
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable,
                moduleFlagsMemory,
                generationCounterMemory: null,
                ImmutableArray.Create(moduleFlagsMemory));
        }

        var generationCounterMemory = _memory.Read(generationCounterAddress, descriptor.PointerSize);
        return ClrmdModuleEditStateObservation.Project(
            module,
            descriptor.PointerSize,
            moduleFlagsMemory,
            generationCounterMemory);
    }

    private static bool TryAdd(ulong address, int offset, out ulong sum)
    {
        if (offset < 0 || address > ulong.MaxValue - checked((ulong)offset))
        {
            sum = 0;
            return false;
        }

        sum = address + checked((ulong)offset);
        return true;
    }

    private static bool TryAdd(int left, int right, out int sum)
    {
        if (left < 0 || right < 0 || left > int.MaxValue - right)
        {
            sum = 0;
            return false;
        }

        sum = left + right;
        return true;
    }
}
