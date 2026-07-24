using System.Collections.Immutable;
using PhoenixInspect.Host.Dump.ClrMD;
using Microsoft.Diagnostics.Runtime;

namespace PhoenixInspect.IntegrationTests;

internal sealed record RawModeledIncidentObservation(
    ModeledIncidentAxisDefinition Definition,
    RawSelectionObservationKind RawSelection,
    RawMemberBytesObservationKind RawMemberBytes,
    RawContextAttributionKind RawContext);

internal static class OptimizedContextRawClrmdObserver
{
    private const int MaximumHeapObjectsScanned = 1_000_000;
    private const int MaximumHandlesScanned = 100_000;
    private const long MaximumDumpCacheBytes = 256L * 1_024 * 1_024;

    internal static ImmutableArray<RawModeledIncidentObservation> Observe(string dumpPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpPath);

        var cacheOptions = new CacheOptions
        {
            MaxDumpCacheSize = MaximumDumpCacheBytes,
            CacheStackRoots = false,
            CacheStackTraces = false,
        };
        var dataTargetOptions = new DataTargetOptions
        {
            CacheOptions = cacheOptions,
            FileLocator = ClrmdOfflineFileLocator.Instance,
        };
        using var dumpStream = new FileStream(
            dumpPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.RandomAccess);
        using var dataTarget = DataTarget.LoadDump(
            Path.GetFileName(dumpPath),
            dumpStream,
            leaveOpen: true,
            dataTargetOptions);

        if (dataTarget.ClrVersions.Length != 1)
        {
            throw new InvalidDataException(
                $"The modeled incident fixture requires one CLR runtime, but found {dataTarget.ClrVersions.Length}.");
        }

        using var runtime = dataTarget.ClrVersions[0].CreateRuntime();
        var discovered = DiscoverUniqueProbeObjects(runtime);
        var staticContext = ObserveStaticFieldContext(runtime, discovered);
        var strongContext = ObserveStrongHandleContext(runtime, discovered);

        return ModeledIncidentContextCorpus.Axes
            .Select(definition => new RawModeledIncidentObservation(
                definition,
                discovered[definition.Axis].SelectionStatus,
                discovered[definition.Axis].MemberBytesStatus,
                definition.Axis switch
                {
                    ModeledIncidentAxis.This or ModeledIncidentAxis.Argument or ModeledIncidentAxis.Local =>
                        RawContextAttributionKind.UnavailableStackSlotObservationNotAdmitted,
                    ModeledIncidentAxis.Static => staticContext,
                    ModeledIncidentAxis.StrongRoot => strongContext,
                    _ => throw new ArgumentOutOfRangeException(nameof(definition)),
                }))
            .ToImmutableArray();
    }

    private static IReadOnlyDictionary<ModeledIncidentAxis, DiscoveredProbe> DiscoverUniqueProbeObjects(
        ClrRuntime runtime)
    {
        var definitionsByType = ModeledIncidentContextCorpus.Axes.ToDictionary(
            static definition => definition.RuntimeTypeName,
            StringComparer.Ordinal);
        var candidates = ModeledIncidentContextCorpus.Axes.ToDictionary(
            static definition => definition.Axis,
            static _ => new List<ClrObject>());

        var scanned = 0;
        var truncated = false;
        using (var enumerator = runtime.Heap.EnumerateObjects(carefully: true).GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                if (scanned == MaximumHeapObjectsScanned)
                {
                    truncated = true;
                    break;
                }

                scanned++;
                var candidate = enumerator.Current;
                var typeName = candidate.Type?.Name;
                if (typeName is not null && definitionsByType.TryGetValue(typeName, out var definition))
                {
                    candidates[definition.Axis].Add(candidate);
                }
            }
        }

        var result = new Dictionary<ModeledIncidentAxis, DiscoveredProbe>();
        foreach (var definition in ModeledIncidentContextCorpus.Axes)
        {
            if (truncated)
            {
                result.Add(definition.Axis, new DiscoveredProbe(
                    RawSelectionObservationKind.Partial,
                    RawMemberBytesObservationKind.Unavailable,
                    default));
                continue;
            }

            var axisCandidates = candidates[definition.Axis];
            if (axisCandidates.Count == 0)
            {
                result.Add(definition.Axis, new DiscoveredProbe(
                    RawSelectionObservationKind.Unavailable,
                    RawMemberBytesObservationKind.Unavailable,
                    default));
                continue;
            }

            if (axisCandidates.Count != 1)
            {
                result.Add(definition.Axis, new DiscoveredProbe(
                    RawSelectionObservationKind.Ambiguous,
                    RawMemberBytesObservationKind.Conflict,
                    default));
                continue;
            }

            var probe = axisCandidates[0];
            if (!probe.TryReadField<int>(ModeledIncidentContextCorpus.MarkerFieldName, out var marker))
            {
                result.Add(definition.Axis, new DiscoveredProbe(
                    RawSelectionObservationKind.Unique,
                    RawMemberBytesObservationKind.Invalid,
                    default));
                continue;
            }

            result.Add(
                definition.Axis,
                marker == definition.ExpectedMarker
                    ? new DiscoveredProbe(
                        RawSelectionObservationKind.Unique,
                        RawMemberBytesObservationKind.Exact,
                        probe)
                    : new DiscoveredProbe(
                        RawSelectionObservationKind.Unique,
                        RawMemberBytesObservationKind.Conflict,
                        default));
        }

        return result;
    }

    private static RawContextAttributionKind ObserveStaticFieldContext(
        ClrRuntime runtime,
        IReadOnlyDictionary<ModeledIncidentAxis, DiscoveredProbe> discovered)
    {
        var rawStatic = discovered[ModeledIncidentAxis.Static];
        if (rawStatic.MemberBytesStatus != RawMemberBytesObservationKind.Exact)
        {
            return ToContextFailure(rawStatic.MemberBytesStatus);
        }

        var modules = runtime.EnumerateModules()
            .Where(static module => string.Equals(
                Path.GetFileName(module.Name),
                ModeledIncidentContextCorpus.TargetAssemblyName,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (modules.Length == 0)
        {
            return RawContextAttributionKind.UnavailableStaticFieldObservation;
        }

        if (modules.Length != 1)
        {
            return RawContextAttributionKind.Conflict;
        }

        var holder = modules[0].GetTypeByName(ModeledIncidentContextCorpus.StaticHolderTypeName);
        var field = holder?.GetStaticFieldByName(ModeledIncidentContextCorpus.StaticFieldName);
        if (field is null)
        {
            return RawContextAttributionKind.UnavailableStaticFieldObservation;
        }

        if (!field.IsInitialized(modules[0].AppDomain))
        {
            return RawContextAttributionKind.UnavailableStaticFieldObservation;
        }

        var staticObject = field.ReadObject(modules[0].AppDomain);
        if (!staticObject.IsValid)
        {
            return RawContextAttributionKind.UnavailableStaticFieldObservation;
        }

        return staticObject.Address == rawStatic.Object.Address
            ? RawContextAttributionKind.ExactStaticField
            : RawContextAttributionKind.Conflict;
    }

    private static RawContextAttributionKind ObserveStrongHandleContext(
        ClrRuntime runtime,
        IReadOnlyDictionary<ModeledIncidentAxis, DiscoveredProbe> discovered)
    {
        var rawStrong = discovered[ModeledIncidentAxis.StrongRoot];
        if (rawStrong.MemberBytesStatus != RawMemberBytesObservationKind.Exact)
        {
            return ToContextFailure(rawStrong.MemberBytesStatus);
        }

        var scanned = 0;
        var matches = 0;
        using var enumerator = runtime.EnumerateHandles().GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (scanned == MaximumHandlesScanned)
            {
                return RawContextAttributionKind.Unavailable;
            }

            scanned++;
            var handle = enumerator.Current;
            if (handle.HandleKind == ClrHandleKind.Strong &&
                handle.IsStrong &&
                handle.Object.IsValid &&
                handle.Object.Address == rawStrong.Object.Address)
            {
                matches++;
            }
        }

        return matches switch
        {
            0 => RawContextAttributionKind.Unavailable,
            1 => RawContextAttributionKind.ExactStrongHandle,
            _ => RawContextAttributionKind.Conflict,
        };
    }

    private static RawContextAttributionKind ToContextFailure(RawMemberBytesObservationKind value) => value switch
    {
        RawMemberBytesObservationKind.Unavailable => RawContextAttributionKind.Unavailable,
        RawMemberBytesObservationKind.Conflict => RawContextAttributionKind.Conflict,
        RawMemberBytesObservationKind.Invalid => RawContextAttributionKind.Invalid,
        RawMemberBytesObservationKind.Exact => throw new ArgumentException(
            "An exact value does not map to a context failure.",
            nameof(value)),
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private readonly record struct DiscoveredProbe(
        RawSelectionObservationKind SelectionStatus,
        RawMemberBytesObservationKind MemberBytesStatus,
        ClrObject Object);
}
