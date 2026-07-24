using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Product.DumpQuery;

internal enum StaticFieldMetadataImageStatus
{
    Exact = 1,
    Partial = 2,
    Unavailable = 3,
    Conflict = 4,
    Invalid = 5,
}

internal sealed class StaticFieldMetadataModuleInput
{
    internal StaticFieldMetadataModuleInput(
        StaticFieldModuleInstanceIdentity module,
        int runtimeOrdinal,
        ulong assemblyAddress)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentOutOfRangeException.ThrowIfNegative(runtimeOrdinal);
        Module = module;
        RuntimeOrdinal = runtimeOrdinal;
        AssemblyAddress = assemblyAddress;
    }

    internal StaticFieldModuleInstanceIdentity Module { get; }

    internal int RuntimeOrdinal { get; }

    internal ulong AssemblyAddress { get; }
}

internal sealed class StaticFieldMetadataImageObservation
{
    private StaticFieldMetadataImageObservation(
        StaticFieldMetadataImageStatus status,
        ImmutableArray<byte> bytes,
        ImmutableArray<ModuleContentIdentity> contents)
    {
        Status = status;
        Bytes = bytes;
        Contents = contents;
    }

    internal StaticFieldMetadataImageStatus Status { get; }

    internal ImmutableArray<byte> Bytes { get; }

    internal ImmutableArray<ModuleContentIdentity> Contents { get; }

    internal ModuleContentIdentity? ExactContent =>
        Status == StaticFieldMetadataImageStatus.Exact ? Contents[0] : null;

    internal static StaticFieldMetadataImageObservation Exact(
        ImmutableArray<byte> bytes,
        ModuleContentIdentity content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (bytes.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An exact observation requires initialized metadata bytes.", nameof(bytes));
        }

        var detached = ImmutableArray.CreateRange(bytes);
        var computed = ModuleContentIdentity.FromMetadata(content.Mvid, detached.AsSpan());
        if (!computed.Equals(content))
        {
            throw new ArgumentException(
                "The supplied metadata bytes do not reproduce the complete content identity.",
                nameof(content));
        }

        return new StaticFieldMetadataImageObservation(
            StaticFieldMetadataImageStatus.Exact,
            detached,
            ImmutableArray.Create(content));
    }

    internal static StaticFieldMetadataImageObservation Partial() =>
        NonExact(StaticFieldMetadataImageStatus.Partial, ImmutableArray<ModuleContentIdentity>.Empty);

    internal static StaticFieldMetadataImageObservation Unavailable() =>
        NonExact(StaticFieldMetadataImageStatus.Unavailable, ImmutableArray<ModuleContentIdentity>.Empty);

    internal static StaticFieldMetadataImageObservation Invalid(ModuleContentIdentity? observedContent = null) =>
        NonExact(
            StaticFieldMetadataImageStatus.Invalid,
            observedContent is null
                ? ImmutableArray<ModuleContentIdentity>.Empty
                : ImmutableArray.Create(observedContent));

    internal static StaticFieldMetadataImageObservation Conflict(
        ImmutableArray<ModuleContentIdentity> competingContents)
    {
        if (competingContents.IsDefault || competingContents.Any(static content => content is null))
        {
            throw new ArgumentException("Initialized competing content identities are required.", nameof(competingContents));
        }

        var normalized = competingContents
            .Distinct()
            .OrderBy(static content => content.MetadataSha256, StringComparer.Ordinal)
            .ThenBy(static content => content.MetadataLength)
            .ThenBy(static content => content.Mvid)
            .ToImmutableArray();
        if (normalized.Length < 2)
        {
            throw new ArgumentException("A conflict requires at least two distinct complete identities.", nameof(competingContents));
        }

        return NonExact(StaticFieldMetadataImageStatus.Conflict, normalized);
    }

    private static StaticFieldMetadataImageObservation NonExact(
        StaticFieldMetadataImageStatus status,
        ImmutableArray<ModuleContentIdentity> contents) =>
        new(status, ImmutableArray<byte>.Empty, contents);
}

internal interface IStaticFieldMetadataBindingSource
{
    ClrmdSnapshotIdentity Snapshot { get; }

    int PointerWidth { get; }

    ImmutableArray<StaticFieldMetadataModuleInput> Modules { get; }

    StaticFieldMetadataModuleInput? CoreLibraryModule { get; }

    StaticFieldMetadataImageObservation ReadMetadata(StaticFieldMetadataModuleInput module);
}

internal sealed class ClrmdStaticFieldMetadataBindingSource : IStaticFieldMetadataBindingSource
{
    private readonly ClrmdDumpSession session;
    private readonly IReadOnlyDictionary<string, ClrmdModuleInfo> modulesByKey;
    private readonly Dictionary<string, StaticFieldMetadataImageObservation> observationsByKey =
        new(StringComparer.Ordinal);

    internal ClrmdStaticFieldMetadataBindingSource(ClrmdDumpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
        Snapshot = session.Snapshot;
        PointerWidth = session.Memory.PointerSize;

        var inputs = ImmutableArray.CreateBuilder<StaticFieldMetadataModuleInput>(session.Modules.Length);
        var mappings = new Dictionary<string, ClrmdModuleInfo>(StringComparer.Ordinal);
        StaticFieldMetadataModuleInput? coreLibrary = null;
        var selectedCoreLibrary = session.BaseClassLibraryModule;
        for (var ordinal = 0; ordinal < session.Modules.Length; ordinal++)
        {
            var sourceModule = session.Modules[ordinal];
            var identity = sourceModule.Identity;
            var projected = new StaticFieldMetadataModuleInput(
                StaticFieldModuleInstanceIdentity.Create(
                    Snapshot.Sha256,
                    PointerWidth,
                    identity.AppDomainAddress,
                    identity.ModuleAddress,
                    identity.ImageBase,
                    identity.ImageSize),
                ordinal,
                sourceModule.AssemblyAddress);
            inputs.Add(projected);
            mappings.Add(projected.Module.Sha256, sourceModule);
            if (selectedCoreLibrary is not null && sourceModule.Identity.Equals(selectedCoreLibrary.Identity))
            {
                coreLibrary = projected;
            }
        }

        Modules = inputs.MoveToImmutable();
        modulesByKey = mappings;
        CoreLibraryModule = coreLibrary;
    }

    public ClrmdSnapshotIdentity Snapshot { get; }

    public int PointerWidth { get; }

    public ImmutableArray<StaticFieldMetadataModuleInput> Modules { get; }

    public StaticFieldMetadataModuleInput? CoreLibraryModule { get; }

    public StaticFieldMetadataImageObservation ReadMetadata(StaticFieldMetadataModuleInput module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (observationsByKey.TryGetValue(module.Module.Sha256, out var cached))
        {
            return cached;
        }

        var observation = ReadMetadataCore(module);
        observationsByKey.Add(module.Module.Sha256, observation);
        return observation;
    }

    private StaticFieldMetadataImageObservation ReadMetadataCore(StaticFieldMetadataModuleInput module)
    {
        if (!modulesByKey.TryGetValue(module.Module.Sha256, out var sourceModule))
        {
            return StaticFieldMetadataImageObservation.Unavailable();
        }

        var result = session.ReadModuleContentIdentity(sourceModule);
        if (result.Status != ClrmdEvidenceStatus.Exact || result.Value is null)
        {
            return result.Status switch
            {
                ClrmdEvidenceStatus.Partial => StaticFieldMetadataImageObservation.Partial(),
                ClrmdEvidenceStatus.Unavailable => StaticFieldMetadataImageObservation.Unavailable(),
                ClrmdEvidenceStatus.Conflict => StaticFieldMetadataImageObservation.Invalid(),
                ClrmdEvidenceStatus.Invalid => StaticFieldMetadataImageObservation.Invalid(),
                _ => StaticFieldMetadataImageObservation.Invalid(),
            };
        }

        if (result.Evidence.Length != 1 ||
            result.Evidence[0].Status != MemoryReadStatus.Exact ||
            result.Evidence[0].RequestedLength != result.Evidence[0].BytesRead)
        {
            return StaticFieldMetadataImageObservation.Invalid(result.Value);
        }

        try
        {
            return StaticFieldMetadataImageObservation.Exact(result.Evidence[0].Bytes, result.Value);
        }
        catch (ArgumentException)
        {
            return StaticFieldMetadataImageObservation.Invalid(result.Value);
        }
    }
}
