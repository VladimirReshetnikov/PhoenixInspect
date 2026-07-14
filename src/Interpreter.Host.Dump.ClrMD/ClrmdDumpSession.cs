using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Owns a ClrMD dump lifetime and projects bounded, provenance-bearing runtime and memory evidence.
/// </summary>
/// <remarks>
/// This remains a draft walking-skeleton adapter. It supports one CLR runtime per dump, same-toolchain CoreCLR layout
/// evidence, strong-handle object selection, primitive field locations, string-field reads, and counted IL bodies.
/// ClrMD objects do not escape this boundary. Loading hashes and parses the same read-only file stream, which remains
/// open for the session lifetime so path replacement cannot silently change the source behind its content identity.
/// ClrMD runtime projections may still probe target-reported full paths outside its file-locator seam; this in-process
/// adapter is therefore restricted to trusted generated/local fixtures until the external isolation and trusted-DAC
/// gate exists. Only reads retained through <see cref="Memory"/> are attributed as counted dump-byte evidence.
/// </remarks>
public sealed class ClrmdDumpSession : IDisposable
{
    private const int MaximumHandleMatches = 4_096;
    private const int MaximumHandleScanCount = 100_000;
    private const int MaximumRuntimeMethodScanCount = 10_000;
    private const int MaximumRuntimeInstanceFieldCount = 4_096;
    private const int MaximumRuntimeModuleCount = 4_096;
    private const int MaximumRuntimeTypeNameCharacters = 4_096;
    private const int MaximumRuntimeMethodNameCharacters = 1_024;
    private const int MaximumRuntimeFieldNameCharacters = 1_024;
    private const int MaximumRuntimeModuleFileNameCharacters = 1_024;
    private const int MaximumStringCharacters = 1_048_576;
    private const long MaximumExternalDumpFileSize = 8L * 1_024 * 1_024 * 1_024;
    private const long MaximumClrmdDumpCacheSize = 256L * 1_024 * 1_024;
    private readonly FileStream _dumpStream;
    private readonly DataTarget _dataTarget;
    private readonly ClrRuntime _runtime;
    private readonly ClrmdProcessMemoryReader _memory;
    private readonly IReadOnlyDictionary<ClrmdRuntimeModuleIdentity, ClrModule> _runtimeModules;
    private readonly IReadOnlyDictionary<(ulong AppDomainAddress, ulong ModuleAddress), ClrmdModuleInfo> _moduleInfos;
    private bool _disposed;

    private ClrmdDumpSession(
        FileStream dumpStream,
        DataTarget dataTarget,
        ClrRuntime runtime,
        ClrmdSnapshotIdentity snapshot,
        ClrmdProcessMemoryReader memory,
        ImmutableArray<ClrmdModuleInfo> modules,
        IReadOnlyDictionary<ClrmdRuntimeModuleIdentity, ClrModule> runtimeModules,
        IReadOnlyDictionary<(ulong AppDomainAddress, ulong ModuleAddress), ClrmdModuleInfo> moduleInfos)
    {
        _dumpStream = dumpStream;
        _dataTarget = dataTarget;
        _runtime = runtime;
        _memory = memory;
        Snapshot = snapshot;
        Modules = modules;
        _runtimeModules = runtimeModules;
        _moduleInfos = moduleInfos;
        TargetPlatform = dataTarget.DataReader.TargetPlatform.ToString();
        TargetArchitecture = dataTarget.DataReader.Architecture.ToString();
    }

    /// <summary>
    /// Gets the content identity of the loaded dump.
    /// </summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>
    /// Gets the target operating-system name reported by the dump reader.
    /// </summary>
    public string TargetPlatform { get; }

    /// <summary>
    /// Gets the target processor-architecture name reported by the dump reader.
    /// </summary>
    public string TargetArchitecture { get; }

    /// <summary>
    /// Gets the raw, counted process-memory evidence reader for the immutable dump.
    /// </summary>
    public IProcessMemoryReader Memory => _memory;

    /// <summary>
    /// Gets whether this session has replaced ClrMD's ambient/default locator with the no-acquisition locator.
    /// </summary>
    /// <remarks>
    /// This internal diagnostic exists for the adapter contract test. Product callers cannot replace the locator.
    /// </remarks>
    internal bool IsOfflineLocatorInstalled =>
        ReferenceEquals(_dataTarget.FileLocator, ClrmdOfflineFileLocator.Instance);

    /// <summary>
    /// Gets whether ClrMD uses the adapter's bounded dump cache with stack-derived caches disabled.
    /// </summary>
    /// <remarks>This internal diagnostic exists for the adapter contract test.</remarks>
    internal bool IsBoundedDumpCachePolicyEnforced =>
        _dataTarget.CacheOptions.MaxDumpCacheSize == MaximumClrmdDumpCacheSize &&
        !_dataTarget.CacheOptions.CacheStackTraces &&
        !_dataTarget.CacheOptions.CacheStackRoots;

    /// <summary>
    /// Gets managed module-instance evidence sorted by snapshot-scoped runtime identity.
    /// </summary>
    /// <remarks>The immutable catalog remains available after disposal; it performs no lazy dump reads.</remarks>
    public ImmutableArray<ClrmdModuleInfo> Modules { get; }

    /// <summary>
    /// Opens a caller-selected trusted/local dump through a structured evidence boundary.
    /// </summary>
    /// <param name="dumpPath">Path to the dump artifact.</param>
    /// <returns>
    /// An exact result owning an open session, or a typed unavailable, invalid, or unsupported-runtime result. The
    /// caller owns and must dispose the session carried by an exact result.
    /// </returns>
    /// <remarks>
    /// This typed opener does not make ClrMD a hostile-input sandbox. Arbitrary incident dumps remain unsupported
    /// until the documented no-network access-control worker and trusted-DAC policy are implemented.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="dumpPath"/> is empty or whitespace.</exception>
    public static ClrmdEvidenceResult<ClrmdDumpSession> Open(string dumpPath)
    {
        if (string.IsNullOrWhiteSpace(dumpPath))
        {
            throw new ArgumentException("Dump path is required.", nameof(dumpPath));
        }

        if (!File.Exists(dumpPath))
        {
            return ClrmdEvidenceResult<ClrmdDumpSession>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ArtifactUnavailable);
        }

        try
        {
            return ClrmdEvidenceResult<ClrmdDumpSession>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                Load(dumpPath));
        }
        catch (DumpArtifactLimitExceededException)
        {
            return ClrmdEvidenceResult<ClrmdDumpSession>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.LimitExceeded);
        }
        catch (NotSupportedException)
        {
            return ClrmdEvidenceResult<ClrmdDumpSession>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.RuntimeUnsupported);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ClrDiagnosticsException or
            ArgumentException or InvalidOperationException or OverflowException)
        {
            return ClrmdEvidenceResult<ClrmdDumpSession>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.ArtifactInvalid);
        }
        catch (IOException)
        {
            return ClrmdEvidenceResult<ClrmdDumpSession>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ArtifactUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return ClrmdEvidenceResult<ClrmdDumpSession>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ArtifactUnavailable);
        }
    }

    /// <summary>
    /// Loads a trusted/local dump fixture, validates that it contains exactly one CLR runtime, and builds a deterministic module catalog.
    /// </summary>
    /// <param name="dumpPath">Path to the immutable dump file to open.</param>
    /// <returns>A new session that owns the ClrMD data target and runtime.</returns>
    /// <exception cref="ArgumentException"><paramref name="dumpPath"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">The dump file does not exist.</exception>
    /// <exception cref="NotSupportedException">
    /// The dump exceeds the artifact-size bound, contains more than one CLR runtime, or exceeds the bounded
    /// runtime-module catalog limit.
    /// </exception>
    /// <exception cref="InvalidDataException">The dump contains no discoverable CLR runtime.</exception>
    public static ClrmdDumpSession Load(string dumpPath)
    {
        if (string.IsNullOrWhiteSpace(dumpPath))
        {
            throw new ArgumentException("Dump path is required.", nameof(dumpPath));
        }

        if (!File.Exists(dumpPath))
        {
            throw new FileNotFoundException("The dump file does not exist.", dumpPath);
        }

        var dumpStream = new FileStream(dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        DataTarget? dataTarget = null;
        ClrRuntime? runtime = null;

        try
        {
            if (dumpStream.Length > MaximumExternalDumpFileSize)
            {
                throw new DumpArtifactLimitExceededException(MaximumExternalDumpFileSize);
            }

            var snapshot = ComputeSnapshotIdentity(dumpStream);
            var cacheOptions = new CacheOptions
            {
                MaxDumpCacheSize = MaximumClrmdDumpCacheSize,
                CacheStackTraces = false,
                CacheStackRoots = false,
            };
            dataTarget = DataTarget.LoadDump(
                GetTargetFileName(dumpPath),
                dumpStream,
                cacheOptions,
                leaveOpen: true);
            dataTarget.FileLocator = ClrmdOfflineFileLocator.Instance;

            if (dataTarget.ClrVersions.Length == 0)
            {
                throw new InvalidDataException("The dump contains no discoverable CLR runtime.");
            }

            if (dataTarget.ClrVersions.Length != 1)
            {
                throw new NotSupportedException(
                    $"The walking-skeleton adapter requires one CLR runtime, but the dump contains {dataTarget.ClrVersions.Length}.");
            }

            runtime = dataTarget.ClrVersions[0].CreateRuntime();
            var projectedModules = new List<(ClrmdModuleInfo Info, ClrModule RuntimeModule)>();

            foreach (var module in runtime.EnumerateModules())
            {
                if (projectedModules.Count == MaximumRuntimeModuleCount)
                {
                    throw new NotSupportedException(
                        $"The dump exceeds the {MaximumRuntimeModuleCount}-module adapter catalog limit.");
                }

                var identity = new ClrmdRuntimeModuleIdentity(
                    snapshot,
                    module.AppDomain.Address,
                    module.Address,
                    module.ImageBase,
                    module.Size);

                var info = new ClrmdModuleInfo(
                    identity,
                    GetTargetFileName(module.Name),
                    string.IsNullOrWhiteSpace(module.Name) ? null : module.Name,
                    module.AppDomain.Id,
                    module.MetadataAddress,
                    module.MetadataLength,
                    module.Layout.ToString());

                projectedModules.Add((info, module));
            }

            projectedModules.Sort(static (left, right) => CompareModuleIdentity(left.Info.Identity, right.Info.Identity));

            var modules = projectedModules.Select(static pair => pair.Info).ToImmutableArray();
            var runtimeModules = projectedModules.ToDictionary(static pair => pair.Info.Identity, static pair => pair.RuntimeModule);
            var moduleInfos = projectedModules.ToDictionary(
                static pair => (pair.Info.Identity.AppDomainAddress, pair.Info.Identity.ModuleAddress),
                static pair => pair.Info);
            var memory = new ClrmdProcessMemoryReader(dataTarget.DataReader, snapshot.MemorySourceId);

            return new ClrmdDumpSession(
                dumpStream,
                dataTarget,
                runtime,
                snapshot,
                memory,
                modules,
                runtimeModules,
                moduleInfos);
        }
        catch
        {
            runtime?.Dispose();
            dataTarget?.Dispose();
            dumpStream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads and identifies a selected runtime module's complete metadata image directly from dump memory.
    /// </summary>
    /// <param name="module">A module instance selected from this session's immutable catalog.</param>
    /// <returns>
    /// Exact MVID, length, and SHA-256 identity with the raw metadata read, or a typed partial, unavailable,
    /// conflicting, or invalid result.
    /// </returns>
    public ClrmdEvidenceResult<ModuleContentIdentity> ReadModuleContentIdentity(ClrmdModuleInfo module)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(module);
        if (module.Identity.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (!_runtimeModules.ContainsKey(module.Identity))
        {
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        if (module.MetadataAddress == 0 || module.MetadataLength == 0)
        {
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MetadataUnavailable);
        }

        if (module.MetadataLength > (ulong)Memory.MaximumReadLength)
        {
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.LimitExceeded);
        }

        if (module.MetadataAddress > ulong.MaxValue - (module.MetadataLength - 1))
        {
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        var metadataRead = Memory.Read(module.MetadataAddress, checked((int)module.MetadataLength));
        var evidence = ImmutableArray.Create(metadataRead);
        if (metadataRead.Status != MemoryReadStatus.Exact)
        {
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                metadataRead.Status == MemoryReadStatus.Partial
                    ? ClrmdEvidenceStatus.Partial
                    : ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable,
                evidence: evidence);
        }

        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataRead.Bytes);
            var reader = provider.GetMetadataReader();
            var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
            var identity = ModuleContentIdentity.FromMetadata(mvid, metadataRead.Bytes.AsSpan());
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                identity,
                evidence);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: evidence);
        }
    }

    /// <summary>
    /// Finds every managed module instance whose target-reported file name matches the supplied display name.
    /// </summary>
    /// <param name="fileName">Simple target module file name, such as <c>Interpreter.TestTarget.dll</c>.</param>
    /// <returns>
    /// All matching instances in deterministic runtime-identity order. Multiple results are preserved because the same
    /// name can be loaded into multiple application domains or loader contexts. The immutable catalog remains usable
    /// after session disposal because this operation performs no further dump reads.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fileName"/> exceeds the deterministic lookup bound.
    /// </exception>
    public ImmutableArray<ClrmdModuleInfo> FindModulesByFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ImmutableArray<ClrmdModuleInfo>.Empty;
        }

        if (fileName.Length > MaximumRuntimeModuleFileNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileName),
                $"Runtime module file names are limited to {MaximumRuntimeModuleFileNameCharacters} characters.");
        }

        return Modules
            .Where(candidate => string.Equals(candidate.Name, fileName, StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();
    }

    /// <summary>
    /// Searches a bounded prefix of strong runtime handles for objects whose type name exactly matches the request.
    /// </summary>
    /// <param name="typeName">Full runtime type display name as reported by ClrMD.</param>
    /// <param name="maximumMatches">Maximum number of matches to retain before stopping traversal.</param>
    /// <param name="maximumHandlesScanned">Maximum number of runtime handles to inspect.</param>
    /// <returns>
    /// An exact result only when the handle enumeration was exhausted within both bounds; otherwise a bounded partial
    /// or invalid result retaining the matches found before traversal stopped.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="typeName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The requested type name or a supplied traversal bound exceeds the adapter's deterministic hard cap.
    /// </exception>
    public ClrmdHeapObjectSearchResult FindStrongHandleObjectsByTypeName(
        string typeName,
        int maximumMatches,
        int maximumHandlesScanned)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("A runtime type name is required.", nameof(typeName));
        }

        if (typeName.Length > MaximumRuntimeTypeNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(typeName),
                $"Runtime type names are limited to {MaximumRuntimeTypeNameCharacters} characters.");
        }

        if (maximumMatches <= 0 || maximumMatches > MaximumHandleMatches)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumMatches),
                $"The match bound must be between 1 and {MaximumHandleMatches}.");
        }

        if (maximumHandlesScanned <= 0 || maximumHandlesScanned > MaximumHandleScanCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumHandlesScanned),
                $"The handle-scan bound must be between 1 and {MaximumHandleScanCount}.");
        }

        var matches = new List<ClrmdHeapObjectInfo>();
        var evidence = ImmutableArray.CreateBuilder<MemoryReadResult>();
        var handlesScanned = 0;
        var matchLimitReached = false;
        var handleBudgetReached = false;
        var memoryIncomplete = false;
        var projectionIncomplete = false;
        var traversalConflict = false;
        var traversalInvalid = false;

        try
        {
            using var enumerator = _runtime.EnumerateHandles().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (handlesScanned == maximumHandlesScanned)
                {
                    handleBudgetReached = true;
                    break;
                }

                var handle = enumerator.Current;
                handlesScanned++;
                var obj = handle.Object;
                if (!handle.IsStrong || obj.IsNull)
                {
                    continue;
                }

                if (!obj.IsValid)
                {
                    traversalInvalid = true;
                    continue;
                }

                var type = obj.Type;
                if (type is null)
                {
                    traversalInvalid = true;
                    continue;
                }

                if (!string.Equals(type.Name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!_moduleInfos.TryGetValue((type.Module.AppDomain.Address, type.Module.Address), out var module))
                {
                    projectionIncomplete = true;
                    continue;
                }

                if (matches.Count == maximumMatches)
                {
                    matchLimitReached = true;
                    break;
                }

                var rootReferenceRead = Memory.Read(handle.Address, Memory.PointerSize);
                evidence.Add(rootReferenceRead);
                if (rootReferenceRead.Status != MemoryReadStatus.Exact)
                {
                    memoryIncomplete = true;
                    continue;
                }

                if (!TryDecodePointer(rootReferenceRead.Bytes.AsSpan(), Memory.PointerSize, out var rootedObjectAddress))
                {
                    traversalInvalid = true;
                    break;
                }

                if (rootedObjectAddress != obj.Address)
                {
                    traversalConflict = true;
                    break;
                }

                var methodTableRead = Memory.Read(obj.Address, Memory.PointerSize);
                evidence.Add(methodTableRead);
                if (methodTableRead.Status != MemoryReadStatus.Exact)
                {
                    memoryIncomplete = true;
                    continue;
                }

                if (!TryDecodePointer(methodTableRead.Bytes.AsSpan(), Memory.PointerSize, out var methodTable))
                {
                    traversalInvalid = true;
                    break;
                }

                if (methodTable != type.MethodTable)
                {
                    traversalConflict = true;
                    break;
                }

                matches.Add(new ClrmdHeapObjectInfo(
                    Snapshot,
                    obj.Address,
                    type.Name ?? typeName,
                    methodTable,
                    handle.Address,
                    handle.HandleKind.ToString(),
                    module,
                    ImmutableArray.Create(rootReferenceRead, methodTableRead)));

            }
        }
        catch (ClrDiagnosticsException)
        {
            traversalInvalid = true;
        }
        catch (InvalidDataException)
        {
            traversalInvalid = true;
        }
        catch (ArgumentOutOfRangeException)
        {
            traversalInvalid = true;
        }

        var orderedMatches = matches
            .OrderBy(static value => value.Address)
            .ThenBy(static value => value.RootAddress)
            .ToImmutableArray();

        var status = traversalInvalid
            ? ClrmdEvidenceStatus.Invalid
            : traversalConflict
                ? ClrmdEvidenceStatus.Conflict
            : handleBudgetReached || matchLimitReached || memoryIncomplete || projectionIncomplete
                ? ClrmdEvidenceStatus.Partial
                : ClrmdEvidenceStatus.Exact;
        var issue = traversalInvalid
            ? ClrmdValueIssue.InvalidData
            : traversalConflict
                ? ClrmdValueIssue.TypeMismatch
            : memoryIncomplete
                ? ClrmdValueIssue.MemoryUnavailable
            : projectionIncomplete
                ? ClrmdValueIssue.ModuleUnavailable
            : status == ClrmdEvidenceStatus.Partial
                ? ClrmdValueIssue.LimitExceeded
                : ClrmdValueIssue.None;

        return new ClrmdHeapObjectSearchResult(
            status,
            issue,
            handlesScanned,
            maximumHandlesScanned,
            maximumMatches,
            matchLimitReached,
            orderedMatches,
            evidence.ToImmutable());
    }

    /// <summary>
    /// Resolves a runtime field location for a selected dump object without collapsing evidence misses into a Boolean.
    /// </summary>
    /// <param name="obj">Object selected from this session.</param>
    /// <param name="fieldName">Exact metadata field name.</param>
    /// <returns>
    /// An exact field location, a partial limit result when the projected field catalog exceeds the adapter cap, or a
    /// typed unavailable, conflicting, or invalid result.
    /// </returns>
    /// <remarks>
    /// The adapter scans at most <c>4096</c> projected fields and rejects duplicate ordinal names. ClrMD/DAC may do
    /// internal work while materializing that catalog; arbitrary artifacts therefore remain behind the external
    /// isolation and trusted-DAC gate rather than treating this item cap as a parser sandbox.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fieldName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fieldName"/> exceeds the deterministic lookup bound.
    /// </exception>
    public ClrmdEvidenceResult<ClrmdInstanceFieldInfo> GetInstanceField(
        ClrmdHeapObjectInfo obj,
        string fieldName)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(obj);
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("A field name is required.", nameof(fieldName));
        }

        if (fieldName.Length > MaximumRuntimeFieldNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fieldName),
                $"Runtime field names are limited to {MaximumRuntimeFieldNameCharacters} characters.");
        }

        if (obj.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        try
        {
            var runtimeObject = _runtime.Heap.GetObject(obj.Address);
            var runtimeType = runtimeObject.Type;
            if (!runtimeObject.IsValid || runtimeType is null)
            {
                return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.ObjectUnavailable);
            }

            if (runtimeType.MethodTable != obj.MethodTable ||
                !string.Equals(runtimeType.Name, obj.TypeName, StringComparison.Ordinal))
            {
                return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch);
            }

            var fieldSelection = SelectInstanceField(runtimeType, fieldName);
            if (fieldSelection.Status != ClrmdEvidenceStatus.Exact)
            {
                return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                    fieldSelection.Status,
                    fieldSelection.Issue);
            }

            var runtimeField = fieldSelection.Value!;
            var address = runtimeField.GetAddress(obj.Address, interior: false);
            if (!IsRangeWithinExtent(obj.Address, runtimeObject.Size, address, runtimeField.Size))
            {
                return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData);
            }

            var field = new ClrmdInstanceFieldInfo(
                runtimeField.Name ?? fieldName,
                runtimeField.Token,
                address,
                runtimeField.Size,
                runtimeField.IsObjectReference,
                runtimeField.ElementType.ToString(),
                runtimeField.Type?.Name);
            return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                field);
        }
        catch (Exception exception) when (
            exception is ClrDiagnosticsException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }
    }

    /// <summary>
    /// Reads and decodes one Int32 instance field from counted raw dump-memory evidence.
    /// </summary>
    /// <param name="obj">Object selected from this session.</param>
    /// <param name="fieldName">Exact metadata name of an Int32 field.</param>
    /// <returns>
    /// An exact decoded value, or typed conflict/unavailable/invalid evidence. A short raw read is retained in a
    /// non-exact observation but is never decoded with a fabricated suffix.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fieldName"/> is empty or whitespace.</exception>
    public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
        ClrmdHeapObjectInfo obj,
        string fieldName)
    {
        var fieldResult = GetInstanceField(obj, fieldName);
        if (fieldResult.Status != ClrmdEvidenceStatus.Exact)
        {
            return ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                fieldResult.Status,
                fieldResult.Issue);
        }

        var field = fieldResult.Value!;
        if (field.Size != sizeof(int) || field.IsObjectReference ||
            !string.Equals(field.ElementType, nameof(ClrElementType.Int32), StringComparison.Ordinal))
        {
            return ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        MemoryReadResult memory;
        try
        {
            memory = Memory.Read(field.Address, sizeof(int));
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }
        var value = memory.Status == MemoryReadStatus.Exact
            ? BinaryPrimitives.ReadInt32LittleEndian(memory.Bytes.AsSpan())
            : (int?)null;
        var observation = new ClrmdInt32FieldObservation(field, memory, value);

        return memory.Status switch
        {
            MemoryReadStatus.Exact => ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                observation,
                ImmutableArray.Create(memory)),
            MemoryReadStatus.Partial => ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.MemoryUnavailable,
                observation,
                ImmutableArray.Create(memory)),
            _ => ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable,
                observation,
                ImmutableArray.Create(memory)),
        };
    }

    /// <summary>
    /// Reads a string instance field through counted raw dump-memory reads and reports exactness and provenance.
    /// </summary>
    /// <param name="obj">Object selected from this session.</param>
    /// <param name="fieldName">Exact metadata name of a <see cref="string"/> field.</param>
    /// <param name="maximumCharacters">Caller observation cap. Values above the adapter hard cap are still bounded.</param>
    /// <returns>
    /// An exact string or null observation, a known prefix with partial status, or an unavailable observation with a
    /// structured issue. No missing byte is interpreted as a default character, length, reference, or null.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fieldName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fieldName"/> exceeds the deterministic lookup bound or <paramref name="maximumCharacters"/>
    /// is negative.
    /// </exception>
    public ClrmdStringFieldObservation ReadStringField(
        ClrmdHeapObjectInfo obj,
        string fieldName,
        int maximumCharacters)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(obj);
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("A field name is required.", nameof(fieldName));
        }

        if (fieldName.Length > MaximumRuntimeFieldNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fieldName),
                $"Runtime field names are limited to {MaximumRuntimeFieldNameCharacters} characters.");
        }

        if (maximumCharacters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        }

        var evidence = ImmutableArray.CreateBuilder<MemoryReadResult>();
        try
        {
        if (obj.Snapshot != Snapshot)
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch,
                obj,
                fieldName,
                evidence);
        }

        var runtimeObject = _runtime.Heap.GetObject(obj.Address);
        var runtimeType = runtimeObject.Type;
        if (!runtimeObject.IsValid || runtimeType is null)
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ObjectUnavailable,
                obj,
                fieldName,
                evidence);
        }

        if (runtimeType.MethodTable != obj.MethodTable ||
            !string.Equals(runtimeType.Name, obj.TypeName, StringComparison.Ordinal))
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                obj,
                fieldName,
                evidence);
        }

        var fieldSelection = SelectInstanceField(runtimeType, fieldName);
        if (fieldSelection.Status != ClrmdEvidenceStatus.Exact)
        {
            return CreateStringObservation(
                fieldSelection.Status,
                fieldSelection.Issue,
                obj,
                fieldName,
                evidence);
        }

        var runtimeField = fieldSelection.Value!;
        var fieldAddress = runtimeField.GetAddress(obj.Address, interior: false);
        if (runtimeField.ElementType != ClrElementType.String ||
            !IsRangeWithinExtent(obj.Address, runtimeObject.Size, fieldAddress, Memory.PointerSize))
        {
            return CreateStringObservation(
                runtimeField.ElementType == ClrElementType.String
                    ? ClrmdEvidenceStatus.Invalid
                    : ClrmdEvidenceStatus.Conflict,
                runtimeField.ElementType == ClrElementType.String
                    ? ClrmdValueIssue.InvalidData
                    : ClrmdValueIssue.TypeMismatch,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress);
        }

        var referenceRead = Memory.Read(fieldAddress, Memory.PointerSize);
        evidence.Add(referenceRead);
        if (referenceRead.Status != MemoryReadStatus.Exact)
        {
            return CreateStringObservation(
                ToEvidenceStatus(referenceRead.Status),
                ClrmdValueIssue.MemoryUnavailable,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress);
        }

        if (!TryDecodePointer(referenceRead.Bytes.AsSpan(), Memory.PointerSize, out var stringAddress))
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress);
        }

        if (stringAddress == 0)
        {
            return new ClrmdStringFieldObservation(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                isNull: true,
                value: null,
                targetLength: null,
                obj.Address,
                fieldName,
                runtimeField.Token,
                fieldAddress,
                stringAddress: null,
                evidence.ToImmutable());
        }

        var methodTableRead = Memory.Read(stringAddress, Memory.PointerSize);
        evidence.Add(methodTableRead);
        if (methodTableRead.Status != MemoryReadStatus.Exact)
        {
            return CreateStringObservation(
                ToEvidenceStatus(methodTableRead.Status),
                ClrmdValueIssue.MemoryUnavailable,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress);
        }

        if (!TryDecodePointer(methodTableRead.Bytes.AsSpan(), Memory.PointerSize, out var methodTable))
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress);
        }

        var stringObject = _runtime.Heap.GetObject(stringAddress);
        if (!stringObject.IsValid || stringObject.Type is null)
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress);
        }

        if (!stringObject.Type.IsString || stringObject.Type.MethodTable != methodTable)
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress);
        }

        if (!TryAdd(stringAddress, (ulong)Memory.PointerSize, out var lengthAddress))
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress);
        }

        if (!IsRangeWithinExtent(stringAddress, stringObject.Size, lengthAddress, sizeof(int)))
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress);
        }

        var lengthRead = Memory.Read(lengthAddress, sizeof(int));
        evidence.Add(lengthRead);
        if (lengthRead.Status != MemoryReadStatus.Exact)
        {
            return CreateStringObservation(
                ToEvidenceStatus(lengthRead.Status),
                ClrmdValueIssue.MemoryUnavailable,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress);
        }

        var targetLength = BinaryPrimitives.ReadInt32LittleEndian(lengthRead.Bytes.AsSpan());
        if (targetLength < 0)
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress,
                targetLength);
        }

        var configuredCap = Math.Min(maximumCharacters, MaximumStringCharacters);
        var charactersToRead = Math.Min(targetLength, configuredCap);
        var wasLimited = charactersToRead < targetLength;

        if (!TryAdd(lengthAddress, sizeof(int), out var characterAddress))
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress,
                targetLength);
        }

        var targetCharacterBytes = (ulong)(uint)targetLength * sizeof(char);
        if (!IsRangeWithinExtent(stringAddress, stringObject.Size, characterAddress, targetCharacterBytes))
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence,
                runtimeField.Token,
                fieldAddress,
                stringAddress,
                targetLength);
        }

        var characterRead = Memory.Read(characterAddress, checked(charactersToRead * sizeof(char)));
        evidence.Add(characterRead);
        var completeCharacterBytes = characterRead.BytesRead & ~1;
        var value = DecodeLittleEndianUtf16(characterRead.Bytes.AsSpan(0, completeCharacterBytes));

        if (characterRead.Status == MemoryReadStatus.Exact && !wasLimited)
        {
            return new ClrmdStringFieldObservation(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                isNull: false,
                value,
                targetLength,
                obj.Address,
                fieldName,
                runtimeField.Token,
                fieldAddress,
                stringAddress,
                evidence.ToImmutable());
        }

        return new ClrmdStringFieldObservation(
            ClrmdEvidenceStatus.Partial,
            characterRead.Status == MemoryReadStatus.Exact && wasLimited
                ? ClrmdValueIssue.LimitExceeded
                : ClrmdValueIssue.MemoryUnavailable,
            isNull: false,
            value,
            targetLength,
            obj.Address,
            fieldName,
            runtimeField.Token,
            fieldAddress,
            stringAddress,
            evidence.ToImmutable());
        }
        catch (Exception exception) when (
            exception is ClrDiagnosticsException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return CreateStringObservation(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                obj,
                fieldName,
                evidence);
        }
    }

    /// <summary>
    /// Reads and normalizes one uniquely named managed method body solely from counted dump metadata and memory.
    /// </summary>
    /// <param name="module">Runtime module instance selected from this session.</param>
    /// <param name="typeName">Full runtime type display name.</param>
    /// <param name="methodName">Exact runtime method name; overload ambiguity is reported as conflict.</param>
    /// <returns>
    /// An exact dump-backed method body, or a typed unavailable, conflicting, partial, or invalid result retaining all
    /// raw reads completed before the failure. The executable normalized body is exposed only by an exact result. A
    /// runtime type whose method catalog exceeds the deterministic scan cap returns a partial limit result instead of
    /// treating the scanned prefix as exhaustive.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="typeName"/> or <paramref name="methodName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="typeName"/> or <paramref name="methodName"/> exceeds the deterministic lookup bound.
    /// </exception>
    public ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
        ClrmdModuleInfo module,
        string typeName,
        string methodName)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(module);
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("A runtime type name is required.", nameof(typeName));
        }

        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new ArgumentException("A runtime method name is required.", nameof(methodName));
        }

        if (typeName.Length > MaximumRuntimeTypeNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(typeName),
                $"Runtime type names are limited to {MaximumRuntimeTypeNameCharacters} characters.");
        }

        if (methodName.Length > MaximumRuntimeMethodNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(methodName),
                $"Runtime method names are limited to {MaximumRuntimeMethodNameCharacters} characters.");
        }

        if (module.Identity.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (!_runtimeModules.TryGetValue(module.Identity, out var runtimeModule))
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        ClrType? runtimeType;
        try
        {
            runtimeType = runtimeModule.GetTypeByName(typeName);
        }
        catch (Exception exception) when (
            exception is ClrDiagnosticsException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        if (runtimeType is null)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.TypeUnavailable);
        }

        ImmutableArray<ClrMethod> runtimeMethods;
        try
        {
            runtimeMethods = runtimeType.Methods;
        }
        catch (Exception exception) when (
            exception is ClrDiagnosticsException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        var methods = new List<ClrMethod>(capacity: 2);
        var methodsToScan = Math.Min(runtimeMethods.Length, MaximumRuntimeMethodScanCount);
        try
        {
            for (var index = 0; index < methodsToScan && methods.Count < 2; index++)
            {
                var candidate = runtimeMethods[index];
                if (string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                {
                    methods.Add(candidate);
                }
            }
        }
        catch (Exception exception) when (
            exception is ClrDiagnosticsException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        if (methods.Count == 2)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.AmbiguousMatch);
        }

        if (runtimeMethods.Length > MaximumRuntimeMethodScanCount)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded);
        }

        if (methods.Count == 0)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MethodUnavailable);
        }

        var method = methods[0];
        if ((method.MetadataToken & unchecked((int)0xFF000000)) != 0x06000000 ||
            (method.MetadataToken & 0x00FFFFFF) == 0)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        if (module.MetadataAddress == 0 || module.MetadataLength == 0)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MetadataUnavailable);
        }

        if (module.MetadataLength > (ulong)Memory.MaximumReadLength)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.LimitExceeded);
        }

        if (module.MetadataAddress > ulong.MaxValue - (module.MetadataLength - 1))
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        var metadataRead = Memory.Read(module.MetadataAddress, checked((int)module.MetadataLength));
        var metadataEvidence = ImmutableArray.Create(metadataRead);
        if (metadataRead.Status != MemoryReadStatus.Exact)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ToEvidenceStatus(metadataRead.Status),
                ClrmdValueIssue.MemoryUnavailable,
                evidence: metadataEvidence);
        }

        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataRead.Bytes);
            var reader = provider.GetMetadataReader();
            var rowId = method.MetadataToken & 0x00FFFFFF;
            if (rowId > reader.MethodDefinitions.Count)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: metadataEvidence);
            }

            var definition = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(rowId));
            var metadataDeclaringTypeToken = MetadataTokens.GetToken(definition.GetDeclaringType());
            if (!string.Equals(reader.GetString(definition.Name), methodName, StringComparison.Ordinal) ||
                runtimeType.MetadataToken != metadataDeclaringTypeToken ||
                method.Type.MetadataToken != metadataDeclaringTypeToken)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.MethodIdentityMismatch,
                    evidence: metadataEvidence);
            }

            var implementationAttributes = definition.ImplAttributes;
            if ((implementationAttributes & System.Reflection.MethodImplAttributes.CodeTypeMask) !=
                    System.Reflection.MethodImplAttributes.IL ||
                (implementationAttributes & System.Reflection.MethodImplAttributes.ManagedMask) !=
                    System.Reflection.MethodImplAttributes.Managed)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MethodBodyUnavailable,
                    evidence: metadataEvidence);
            }

            var relativeVirtualAddress = definition.RelativeVirtualAddress;
            if (relativeVirtualAddress == 0)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MethodBodyUnavailable,
                    evidence: metadataEvidence);
            }

            if (relativeVirtualAddress < 0)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: metadataEvidence);
            }

            if (runtimeModule.IsDynamic || !runtimeModule.IsPEFile ||
                runtimeModule.Layout is not (ModuleLayout.Mapped or ModuleLayout.Loaded) ||
                module.Identity.ImageBase == 0 || module.Identity.ImageSize == 0)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MethodBodyLayoutUnsupported,
                    evidence: metadataEvidence);
            }

            return ClrmdMethodBodyParser.Read(
                Memory,
                method.MetadataToken,
                relativeVirtualAddress,
                module.Identity.ImageBase,
                module.Identity.ImageSize,
                reader.GetTableRowCount(TableIndex.StandAloneSig),
                metadataEvidence);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: metadataEvidence);
        }
    }

    /// <summary>
    /// Releases the ClrMD runtime and underlying dump data target.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _memory.Dispose();
        try
        {
            _runtime.Dispose();
        }
        finally
        {
            try
            {
                _dataTarget.Dispose();
            }
            finally
            {
                _dumpStream.Dispose();
            }
        }
    }

    private static ClrmdEvidenceResult<ClrInstanceField> SelectInstanceField(
        ClrType runtimeType,
        string fieldName)
    {
        var fields = runtimeType.Fields;
        if (fields.IsDefault)
        {
            return ClrmdEvidenceResult<ClrInstanceField>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        if (fields.Length > MaximumRuntimeInstanceFieldCount)
        {
            return ClrmdEvidenceResult<ClrInstanceField>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded);
        }

        ClrInstanceField? match = null;
        foreach (var candidate in fields)
        {
            if (!string.Equals(candidate.Name, fieldName, StringComparison.Ordinal))
            {
                continue;
            }

            if (match is not null)
            {
                return ClrmdEvidenceResult<ClrInstanceField>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.AmbiguousMatch);
            }

            match = candidate;
        }

        return match is null
            ? ClrmdEvidenceResult<ClrInstanceField>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.FieldUnavailable)
            : ClrmdEvidenceResult<ClrInstanceField>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                match);
    }

    private static ClrmdSnapshotIdentity ComputeSnapshotIdentity(Stream dumpStream)
    {
        dumpStream.Position = 0;
        var snapshot = new ClrmdSnapshotIdentity(
            Convert.ToHexString(SHA256.HashData(dumpStream)).ToLowerInvariant());
        dumpStream.Position = 0;
        return snapshot;
    }

    private static int CompareModuleIdentity(ClrmdRuntimeModuleIdentity left, ClrmdRuntimeModuleIdentity right)
    {
        var appDomain = left.AppDomainAddress.CompareTo(right.AppDomainAddress);
        if (appDomain != 0)
        {
            return appDomain;
        }

        var module = left.ModuleAddress.CompareTo(right.ModuleAddress);
        if (module != 0)
        {
            return module;
        }

        var imageBase = left.ImageBase.CompareTo(right.ImageBase);
        return imageBase != 0 ? imageBase : left.ImageSize.CompareTo(right.ImageSize);
    }

    private static string GetTargetFileName(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return "<unnamed>";
        }

        var separator = targetPath.LastIndexOfAny(['/', '\\']);
        return separator >= 0 && separator + 1 < targetPath.Length
            ? targetPath[(separator + 1)..]
            : targetPath;
    }

    private static bool TryDecodePointer(ReadOnlySpan<byte> bytes, int pointerSize, out ulong value)
    {
        switch (pointerSize)
        {
            case sizeof(uint) when bytes.Length == sizeof(uint):
                value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                return true;
            case sizeof(ulong) when bytes.Length == sizeof(ulong):
                value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static bool TryAdd(ulong address, ulong offset, out ulong result)
    {
        if (address > ulong.MaxValue - offset)
        {
            result = 0;
            return false;
        }

        result = address + offset;
        return true;
    }

    private static bool IsRangeWithinExtent(
        ulong extentAddress,
        ulong extentSize,
        ulong rangeAddress,
        int rangeLength) =>
        rangeLength >= 0 && IsRangeWithinExtent(extentAddress, extentSize, rangeAddress, (ulong)rangeLength);

    private static bool IsRangeWithinExtent(
        ulong extentAddress,
        ulong extentSize,
        ulong rangeAddress,
        ulong rangeLength)
    {
        if (extentSize > ulong.MaxValue - extentAddress ||
            rangeAddress < extentAddress)
        {
            return false;
        }

        var offset = rangeAddress - extentAddress;
        return offset <= extentSize && rangeLength <= extentSize - offset;
    }

    private static ClrmdEvidenceStatus ToEvidenceStatus(MemoryReadStatus status) => status switch
    {
        MemoryReadStatus.Exact => ClrmdEvidenceStatus.Exact,
        MemoryReadStatus.Partial => ClrmdEvidenceStatus.Partial,
        _ => ClrmdEvidenceStatus.Unavailable,
    };

    private static string DecodeLittleEndianUtf16(ReadOnlySpan<byte> bytes)
    {
        if ((bytes.Length & 1) != 0)
        {
            throw new ArgumentException("UTF-16 evidence must contain complete two-byte code units.", nameof(bytes));
        }

        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        var characters = new char[bytes.Length / sizeof(char)];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = (char)BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.Slice(index * sizeof(char), sizeof(char)));
        }

        return new string(characters);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static ClrmdStringFieldObservation CreateStringObservation(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ClrmdHeapObjectInfo obj,
        string fieldName,
        ImmutableArray<MemoryReadResult>.Builder evidence,
        int? fieldMetadataToken = null,
        ulong? fieldAddress = null,
        ulong? stringAddress = null,
        int? targetLength = null) =>
        new(
            status,
            issue,
            isNull: false,
            value: null,
            targetLength,
            obj.Address,
            fieldName,
            fieldMetadataToken,
            fieldAddress,
            stringAddress,
            evidence.ToImmutable());

    private sealed class DumpArtifactLimitExceededException : NotSupportedException
    {
        internal DumpArtifactLimitExceededException(long maximumBytes)
            : base($"Dump artifacts are limited to {maximumBytes} bytes before hashing or ClrMD parsing.")
        {
        }
    }
}
