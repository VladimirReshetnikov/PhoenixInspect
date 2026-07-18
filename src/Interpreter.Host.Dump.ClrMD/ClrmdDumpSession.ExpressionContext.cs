using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Host.Dump.ClrMD;

public sealed partial class ClrmdDumpSession
{
    private const int MaximumSelectedFrameThreadCount = 4_096;
    private const int MaximumSelectedFrameCountPerThread = 4_096;
    private const int MaximumMappedPeHeaderByteLength = 64 * 1_024;
    private const int MaximumMappedPeDebugDirectoryEntryCount = 256;
    private const int MaximumMappedPeCodeViewByteLength = 4_096;
    private const int MaximumPortablePdbCandidateCount = 64;
    private const int MaximumPortablePdbByteLength = 64 * 1_024 * 1_024;
    private const int MaximumPortablePdbLocalScopeCount = 65_536;
    private const int MaximumPortablePdbImportScopeCount = 4_096;
    private const int MaximumPortablePdbImportCount = 4_096;

    private static readonly EvaluationDeterministicBound SelectedFrameThreadBound =
        new("dump.context.threads", MaximumSelectedFrameThreadCount);
    private static readonly EvaluationDeterministicBound SelectedFrameBound =
        new("dump.context.managed-frames-per-thread", MaximumSelectedFrameCountPerThread);
    private static readonly EvaluationDeterministicBound MappedPeHeaderByteBound =
        new("dump.context.mapped-pe-header-bytes", MaximumMappedPeHeaderByteLength);
    private static readonly EvaluationDeterministicBound MappedPeDebugDirectoryBound =
        new("dump.context.mapped-pe-debug-directory-entries", MaximumMappedPeDebugDirectoryEntryCount);
    private static readonly EvaluationDeterministicBound MappedPeCodeViewByteBound =
        new("dump.context.mapped-pe-codeview-bytes", MaximumMappedPeCodeViewByteLength);
    private static readonly EvaluationDeterministicBound PortablePdbCandidateBound =
        new("dump.context.portable-pdb-candidates", MaximumPortablePdbCandidateCount);
    private static readonly EvaluationDeterministicBound PortablePdbByteBound =
        new("dump.context.portable-pdb-bytes", MaximumPortablePdbByteLength);
    private static readonly EvaluationDeterministicBound PortablePdbLocalScopeBound =
        new("dump.context.portable-pdb-local-scopes", MaximumPortablePdbLocalScopeCount);
    private static readonly EvaluationDeterministicBound PortablePdbImportScopeBound =
        new("dump.context.portable-pdb-import-scopes", MaximumPortablePdbImportScopeCount);
    private static readonly EvaluationDeterministicBound PortablePdbImportBound =
        new("dump.context.portable-pdb-imports", MaximumPortablePdbImportCount);

    /// <summary>Gets the maximum managed-thread count admitted by selected-frame acquisition.</summary>
    public static EvaluationDeterministicBound SelectedFrameThreadTraversalBound => SelectedFrameThreadBound;

    /// <summary>Gets the maximum managed-frame count admitted for one selected thread.</summary>
    public static EvaluationDeterministicBound SelectedFrameTraversalBound => SelectedFrameBound;

    /// <summary>Gets the maximum PE-header offset/extent admitted while locating mapped debug-directory evidence.</summary>
    public static EvaluationDeterministicBound MappedPeHeaderTraversalBound => MappedPeHeaderByteBound;

    /// <summary>Gets the maximum mapped PE debug-directory entry count admitted for one selected module.</summary>
    public static EvaluationDeterministicBound MappedPeDebugDirectoryTraversalBound => MappedPeDebugDirectoryBound;

    /// <summary>Gets the maximum complete CodeView payload length admitted for one mapped debug-directory entry.</summary>
    public static EvaluationDeterministicBound MappedPeCodeViewTraversalBound => MappedPeCodeViewByteBound;

    /// <summary>Gets the maximum number of caller-supplied Portable-PDB candidates examined for one frame.</summary>
    public static EvaluationDeterministicBound PortablePdbCandidateTraversalBound => PortablePdbCandidateBound;

    /// <summary>Gets the maximum complete byte length admitted for one Portable-PDB candidate.</summary>
    public static EvaluationDeterministicBound PortablePdbArtifactByteBound => PortablePdbByteBound;

    /// <summary>Gets the maximum LocalScope-table rows admitted by the W7 Portable-PDB projection.</summary>
    public static EvaluationDeterministicBound PortablePdbLocalScopeTraversalBound => PortablePdbLocalScopeBound;

    /// <summary>Gets the maximum active ImportScope-chain length admitted by the W7 projection.</summary>
    public static EvaluationDeterministicBound PortablePdbImportScopeTraversalBound => PortablePdbImportScopeBound;

    /// <summary>Gets the maximum decoded imports admitted across the active ImportScope chain.</summary>
    public static EvaluationDeterministicBound PortablePdbImportTraversalBound => PortablePdbImportBound;

    /// <summary>
    /// Selects and correlates one managed frame by bounded snapshot-scoped thread/frame ordinals.
    /// </summary>
    /// <param name="selector">The immutable snapshot and zero-based producer ordinals to select.</param>
    /// <returns>
    /// An exact frame identity or a typed partial, unavailable, conflicting, invalid, or unsupported observation.
    /// </returns>
    /// <remarks>
    /// Threads are ordered by runtime-thread address and managed id; frames retain ClrMD stack order after excluding
    /// non-managed frames. Exact correlation checks runtime MethodDef/TypeDef/module facts against complete metadata
    /// read from dump memory. No PDB or analysis-machine artifact is opened by this operation.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
    public DumpSelectedFrameObservation SelectExpressionFrame(DumpSelectedFrameSelector selector)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(selector);
        var bounds = ImmutableArray.Create(SelectedFrameThreadBound, SelectedFrameBound);
        if (selector.Snapshot != Snapshot)
        {
            return DumpSelectedFrameObservation.Conflict(
                selector,
                DumpContextEvidenceIssue.SnapshotMismatch,
                bounds);
        }

        try
        {
            var threads = _runtime.Threads
                .OrderBy(static thread => thread.Address)
                .ThenBy(static thread => thread.ManagedThreadId)
                .ThenBy(static thread => thread.OSThreadId)
                .ToArray();
            if (threads.Length > MaximumSelectedFrameThreadCount)
            {
                return DumpSelectedFrameObservation.Partial(
                    selector,
                    DumpContextEvidenceIssue.BoundReached,
                    bounds);
            }

            if (selector.ThreadOrdinal >= threads.Length)
            {
                return DumpSelectedFrameObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.FrameUnavailable,
                    bounds);
            }

            var thread = threads[selector.ThreadOrdinal];
            var frames = thread
                .EnumerateStackTrace(includeContext: false, maxFrames: MaximumSelectedFrameCountPerThread + 1)
                .Where(static frame => frame.Kind == ClrStackFrameKind.ManagedMethod)
                .ToArray();
            if (frames.Length > MaximumSelectedFrameCountPerThread)
            {
                return DumpSelectedFrameObservation.Partial(
                    selector,
                    DumpContextEvidenceIssue.BoundReached,
                    bounds);
            }

            if (selector.FrameOrdinal >= frames.Length)
            {
                return DumpSelectedFrameObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.FrameUnavailable,
                    bounds);
            }

            var frame = frames[selector.FrameOrdinal];
            if (frame.Method is not { } method || method.Type is not { } declaringType)
            {
                return DumpSelectedFrameObservation.Unsupported(
                    selector,
                    DumpContextEvidenceIssue.UnsupportedFrame,
                    bounds);
            }

            if (frame.InstructionPointer == 0 || frame.StackPointer == 0 ||
                thread.Address == 0 || thread.ManagedThreadId <= 0)
            {
                return DumpSelectedFrameObservation.Invalid(
                    selector,
                    DumpContextEvidenceIssue.InvalidFrame,
                    bounds);
            }

            var ilOffset = method.GetILOffset(frame.InstructionPointer);
            if (ilOffset < 0)
            {
                return DumpSelectedFrameObservation.Partial(
                    selector,
                    DumpContextEvidenceIssue.InstructionLocationUnavailable,
                    bounds);
            }

            var runtimeModule = declaringType.Module;
            if (runtimeModule is null ||
                !_moduleInfos.TryGetValue(
                    (runtimeModule.AppDomain.Address, runtimeModule.Address),
                    out var moduleInfo))
            {
                return DumpSelectedFrameObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.ModuleCorrelationUnavailable,
                    bounds);
            }

            var metadata = ReadCompleteMetadata(moduleInfo);
            if (metadata.Status != ClrmdEvidenceStatus.Exact || metadata.Value is null)
            {
                return metadata.Status == ClrmdEvidenceStatus.Partial
                    ? DumpSelectedFrameObservation.Partial(
                        selector,
                        DumpContextEvidenceIssue.SourceIncomplete,
                        bounds)
                    : DumpSelectedFrameObservation.Unavailable(
                        selector,
                        DumpContextEvidenceIssue.ModuleCorrelationUnavailable,
                        bounds);
            }

            using var provider = MetadataReaderProvider.FromMetadataImage(metadata.Value.Bytes);
            var reader = provider.GetMetadataReader();
            if (!TryGetMethodAndDeclaringType(
                    reader,
                    method.MetadataToken,
                    declaringType.MetadataToken,
                    out var typeDefinition,
                    out var declaringNamespace))
            {
                return DumpSelectedFrameObservation.Conflict(
                    selector,
                    DumpContextEvidenceIssue.ModuleMismatch,
                    bounds);
            }

            var moduleContent = ModuleContentIdentity.FromMetadata(
                reader.GetGuid(reader.GetModuleDefinition().Mvid),
                metadata.Value.Bytes.AsSpan());
            var identity = DumpSelectedFrameIdentity.Create(
                selector,
                checked((uint)thread.ManagedThreadId),
                thread.Address,
                frame.StackPointer,
                moduleInfo.Identity,
                moduleContent,
                method.MetadataToken,
                MetadataTokens.GetToken(typeDefinition),
                declaringNamespace,
                DumpInstructionLocation.Create(frame.InstructionPointer, ilOffset));
            return DumpSelectedFrameObservation.Exact(identity, bounds);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentException or
            InvalidOperationException or OverflowException or ClrDiagnosticsException)
        {
            return DumpSelectedFrameObservation.Invalid(
                selector,
                DumpContextEvidenceIssue.InvalidFrame,
                bounds);
        }
    }

    /// <summary>
    /// Acquires exact mapped-module debug identity, one identity-matching Portable PDB, and active scope/import facts.
    /// </summary>
    /// <param name="selectedFrame">The independently typed selected-frame observation.</param>
    /// <param name="portablePdbCandidates">
    /// An explicitly initialized bounded array of caller-discovered local candidate paths. Paths are discovery hints
    /// only and never enter returned identities.
    /// </param>
    /// <returns>An exact or typed non-exact Portable-PDB observation scoped to this dump.</returns>
    /// <remarks>
    /// The module CodeView GUID/stamp is read from counted mapped-image bytes. Every existing candidate is completely
    /// hashed and its Portable-PDB content id must match before scope/import projection. Enumeration order and file
    /// names never choose among distinct matching artifacts. Caveat: this draft accepts only standalone Portable PDBs
    /// and the bounded import-definition subset represented by <see cref="DumpPortablePdbImportFact"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="selectedFrame"/> is null.</exception>
    /// <exception cref="ArgumentException">The candidate array is default or contains a null/blank path.</exception>
    public DumpPortablePdbObservation ReadExpressionPortablePdbContext(
        DumpSelectedFrameObservation selectedFrame,
        ImmutableArray<string> portablePdbCandidates)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(selectedFrame);
        if (portablePdbCandidates.IsDefault)
        {
            throw new ArgumentException(
                "An explicitly initialized Portable-PDB candidate array is required.",
                nameof(portablePdbCandidates));
        }

        if (portablePdbCandidates.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Portable-PDB candidate paths cannot be null or blank.",
                nameof(portablePdbCandidates));
        }

        var bounds = ImmutableArray.Create(
            MappedPeHeaderByteBound,
            MappedPeDebugDirectoryBound,
            MappedPeCodeViewByteBound,
            PortablePdbCandidateBound,
            PortablePdbByteBound,
            PortablePdbLocalScopeBound,
            PortablePdbImportScopeBound,
            PortablePdbImportBound);
        if (selectedFrame.Selector.Snapshot != Snapshot)
        {
            return DumpPortablePdbObservation.Conflict(
                DumpPortablePdbEvidenceSource.ForSnapshot(Snapshot),
                DumpContextEvidenceIssue.SnapshotMismatch,
                bounds);
        }

        if (selectedFrame.Frame is not { } frame)
        {
            return DumpPortablePdbObservation.Unavailable(
                DumpPortablePdbEvidenceSource.ForSnapshot(Snapshot),
                DumpContextEvidenceIssue.PrerequisiteUnavailable,
                ImmutableArray<EvaluationDeterministicBound>.Empty);
        }

        if (portablePdbCandidates.Length > MaximumPortablePdbCandidateCount)
        {
            return DumpPortablePdbObservation.Partial(
                DumpPortablePdbEvidenceSource.ForSnapshot(Snapshot),
                DumpContextEvidenceIssue.BoundReached,
                bounds);
        }

        return ReadExpressionPortablePdbContextCore(frame, portablePdbCandidates, bounds);
    }

    /// <summary>Acquires the complete additive selected-frame and Portable-PDB context for one expression bind.</summary>
    /// <param name="selector">The snapshot-scoped selected-frame ordinal request.</param>
    /// <param name="portablePdbCandidates">Initialized caller-discovered PDB candidate paths, possibly empty.</param>
    /// <returns>A validated additive context retaining independent frame and PDB dispositions.</returns>
    /// <remarks>
    /// PDB acquisition does not advance beyond the snapshot when frame selection is non-exact. Exact fully qualified
    /// binding may subsequently choose not to consult either source.
    /// </remarks>
    public DumpExpressionBindingContext AcquireExpressionBindingContext(
        DumpSelectedFrameSelector selector,
        ImmutableArray<string> portablePdbCandidates)
    {
        var frame = SelectExpressionFrame(selector);
        var pdb = ReadExpressionPortablePdbContext(frame, portablePdbCandidates);
        return DumpExpressionBindingContext.Acquire(Snapshot, frame, pdb);
    }

    private DumpPortablePdbObservation ReadExpressionPortablePdbContextCore(
        DumpSelectedFrameIdentity frame,
        ImmutableArray<string> candidatePaths,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        if (!_moduleInfos.TryGetValue(
                (frame.RuntimeModule.AppDomainAddress, frame.RuntimeModule.ModuleAddress),
                out var moduleInfo) ||
            !_runtimeModules.TryGetValue(moduleInfo.Identity, out var runtimeModule))
        {
            return DumpPortablePdbObservation.Unavailable(
                DumpPortablePdbEvidenceSource.ForSnapshot(Snapshot),
                DumpContextEvidenceIssue.ModuleCorrelationUnavailable,
                bounds);
        }

        if (!frame.RuntimeModule.Equals(moduleInfo.Identity))
        {
            return DumpPortablePdbObservation.Conflict(
                DumpPortablePdbEvidenceSource.ForSnapshot(Snapshot),
                DumpContextEvidenceIssue.ModuleMismatch,
                bounds);
        }

        var moduleDebug = ReadModulePortablePdbDebugIdentity(runtimeModule, moduleInfo, frame.ModuleContent);
        if (moduleDebug.Status != DumpContextEvidenceStatus.Exact || moduleDebug.Identity is null)
        {
            var source = DumpPortablePdbEvidenceSource.ForSnapshot(Snapshot);
            return moduleDebug.Status switch
            {
                DumpContextEvidenceStatus.Partial => DumpPortablePdbObservation.Partial(
                    source, moduleDebug.Issue, bounds),
                DumpContextEvidenceStatus.Unavailable => DumpPortablePdbObservation.Unavailable(
                    source, moduleDebug.Issue, bounds),
                DumpContextEvidenceStatus.Invalid => DumpPortablePdbObservation.Invalid(
                    source, moduleDebug.Issue, bounds),
                DumpContextEvidenceStatus.Unsupported => DumpPortablePdbObservation.Unsupported(
                    source, moduleDebug.Issue, bounds),
                _ => DumpPortablePdbObservation.Conflict(source, moduleDebug.Issue, bounds),
            };
        }

        var expected = moduleDebug.Identity;
        var candidates = ReadPortablePdbCandidates(candidatePaths, expected, out var candidateFailure);
        if (candidates.Length == 0)
        {
            if (candidateFailure is { } failure)
            {
                return failure;
            }

            return DumpPortablePdbObservation.Unavailable(
                DumpPortablePdbEvidenceSource.ForModule(expected),
                DumpContextEvidenceIssue.PortablePdbUnavailable,
                bounds);
        }

        var matching = candidates
            .Where(candidate => candidate.Artifact.DebugIdentity.Equals(expected.DebugIdentity))
            .GroupBy(candidate => candidate.Artifact.Content.Sha256, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static candidate => candidate.Artifact.Content.Sha256, StringComparer.Ordinal)
            .ToArray();
        if (matching.Length == 0)
        {
            var mismatches = candidates
                .GroupBy(candidate => candidate.Artifact.Content.Sha256, StringComparer.Ordinal)
                .Select(static group => group.First())
                .ToArray();
            if (mismatches.Length != 1)
            {
                return DumpPortablePdbObservation.Ambiguous(
                    DumpPortablePdbEvidenceSource.ForModule(expected),
                    DumpContextEvidenceIssue.PortablePdbAmbiguous,
                    bounds);
            }

            var mismatch = mismatches[0];
            return DumpPortablePdbObservation.Conflict(
                DumpPortablePdbEvidenceSource.ForCandidate(
                    expected,
                    mismatch.Artifact.Content,
                    mismatch.Artifact.DebugIdentity),
                DumpContextEvidenceIssue.PortablePdbIdentityMismatch,
                bounds);
        }

        if (matching.Length != 1)
        {
            return DumpPortablePdbObservation.Ambiguous(
                DumpPortablePdbEvidenceSource.ForModule(expected),
                DumpContextEvidenceIssue.PortablePdbAmbiguous,
                bounds);
        }

        return ProjectPortablePdbContext(frame, expected, matching[0], moduleInfo, bounds);
    }

    private ModuleDebugReadResult ReadModulePortablePdbDebugIdentity(
        ClrModule runtimeModule,
        ClrmdModuleInfo moduleInfo,
        ModuleContentIdentity moduleContent)
    {
        if (runtimeModule.IsDynamic || !runtimeModule.IsPEFile ||
            runtimeModule.Layout is not (ModuleLayout.Mapped or ModuleLayout.Loaded))
        {
            return ModuleDebugReadResult.Unsupported(DumpContextEvidenceIssue.UnsupportedPortablePdb);
        }

        if (moduleInfo.Identity.ImageBase == 0 || moduleInfo.Identity.ImageSize < 64)
        {
            return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
        }

        var dosRead = Memory.Read(moduleInfo.Identity.ImageBase, 64);
        if (dosRead.Status != MemoryReadStatus.Exact)
        {
            return dosRead.Status == MemoryReadStatus.Partial
                ? ModuleDebugReadResult.Partial(DumpContextEvidenceIssue.SourceIncomplete)
                : ModuleDebugReadResult.Unavailable(DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable);
        }

        try
        {
            var dos = dosRead.Bytes.AsSpan();
            if (BinaryPrimitives.ReadUInt16LittleEndian(dos) != 0x5A4D)
            {
                return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
            }

            var peHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(dos.Slice(0x3C, sizeof(int)));
            if (peHeaderOffset < 64 || peHeaderOffset > MaximumMappedPeHeaderByteLength - 24 ||
                (ulong)peHeaderOffset > moduleInfo.Identity.ImageSize - 24)
            {
                return peHeaderOffset > MaximumMappedPeHeaderByteLength - 24
                    ? ModuleDebugReadResult.Partial(DumpContextEvidenceIssue.BoundReached)
                    : ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
            }

            var coffRead = Memory.Read(
                checked(moduleInfo.Identity.ImageBase + (ulong)peHeaderOffset),
                24);
            if (coffRead.Status != MemoryReadStatus.Exact)
            {
                return IncompleteMappedPeRead(coffRead.Status);
            }

            var coff = coffRead.Bytes.AsSpan();
            if (BinaryPrimitives.ReadUInt32LittleEndian(coff) != 0x0000_4550)
            {
                return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
            }

            var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(coff.Slice(20, sizeof(ushort)));
            if (optionalHeaderSize == 0 || optionalHeaderSize > MaximumMappedPeHeaderByteLength - 24 ||
                (ulong)peHeaderOffset + 24UL + optionalHeaderSize > moduleInfo.Identity.ImageSize)
            {
                return optionalHeaderSize > MaximumMappedPeHeaderByteLength - 24
                    ? ModuleDebugReadResult.Partial(DumpContextEvidenceIssue.BoundReached)
                    : ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
            }

            var optionalRead = Memory.Read(
                checked(moduleInfo.Identity.ImageBase + (ulong)peHeaderOffset + 24UL),
                optionalHeaderSize);
            if (optionalRead.Status != MemoryReadStatus.Exact)
            {
                return IncompleteMappedPeRead(optionalRead.Status);
            }

            var optional = optionalRead.Bytes.AsSpan();
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(optional);
            var dataDirectoryOffset = magic switch
            {
                0x10B => 96,
                0x20B => 112,
                _ => -1,
            };
            if (dataDirectoryOffset < 0 || optional.Length < dataDirectoryOffset + (7 * 8))
            {
                return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
            }

            var declaredImageSize = BinaryPrimitives.ReadUInt32LittleEndian(optional.Slice(56, sizeof(uint)));
            var directoryCountOffset = dataDirectoryOffset - sizeof(uint);
            var directoryCount = BinaryPrimitives.ReadUInt32LittleEndian(
                optional.Slice(directoryCountOffset, sizeof(uint)));
            if (declaredImageSize == 0 || declaredImageSize != moduleInfo.Identity.ImageSize || directoryCount <= 6)
            {
                return directoryCount <= 6
                    ? ModuleDebugReadResult.Unavailable(DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable)
                    : ModuleDebugReadResult.Conflict(DumpContextEvidenceIssue.ModuleMismatch);
            }

            var debugDirectory = optional.Slice(dataDirectoryOffset + (6 * 8), 8);
            var debugDirectoryRva = BinaryPrimitives.ReadUInt32LittleEndian(debugDirectory);
            var debugDirectorySize = BinaryPrimitives.ReadUInt32LittleEndian(debugDirectory.Slice(4));
            if (debugDirectoryRva == 0 || debugDirectorySize == 0)
            {
                return ModuleDebugReadResult.Unavailable(
                    DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable);
            }

            const int debugDirectoryEntrySize = 28;
            if (debugDirectorySize % debugDirectoryEntrySize != 0 ||
                debugDirectoryRva > moduleInfo.Identity.ImageSize ||
                debugDirectorySize > moduleInfo.Identity.ImageSize - debugDirectoryRva)
            {
                return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
            }

            var entryCount = checked((int)(debugDirectorySize / debugDirectoryEntrySize));
            if (entryCount > MaximumMappedPeDebugDirectoryEntryCount || debugDirectorySize > Memory.MaximumReadLength)
            {
                return ModuleDebugReadResult.Partial(DumpContextEvidenceIssue.BoundReached);
            }

            var directoryRead = Memory.Read(
                checked(moduleInfo.Identity.ImageBase + debugDirectoryRva),
                checked((int)debugDirectorySize));
            if (directoryRead.Status != MemoryReadStatus.Exact)
            {
                return IncompleteMappedPeRead(directoryRead.Status);
            }

            var portableEntries = new List<DumpPortablePdbDebugIdentity>();
            var entries = directoryRead.Bytes.AsSpan();
            for (var index = 0; index < entryCount; index++)
            {
                var entry = entries.Slice(index * debugDirectoryEntrySize, debugDirectoryEntrySize);
                var type = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(12, sizeof(uint)));
                if (type != 2)
                {
                    continue;
                }

                var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(16, sizeof(uint)));
                var dataRva = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(20, sizeof(uint)));
                if (dataSize < 24 || dataSize > MaximumMappedPeCodeViewByteLength)
                {
                    return dataSize > MaximumMappedPeCodeViewByteLength
                        ? ModuleDebugReadResult.Partial(DumpContextEvidenceIssue.BoundReached)
                        : ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
                }

                if (dataRva == 0 || dataRva > moduleInfo.Identity.ImageSize ||
                    dataSize > moduleInfo.Identity.ImageSize - dataRva)
                {
                    return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
                }

                var codeViewRead = Memory.Read(
                    checked(moduleInfo.Identity.ImageBase + dataRva),
                    checked((int)dataSize));
                if (codeViewRead.Status != MemoryReadStatus.Exact)
                {
                    return IncompleteMappedPeRead(codeViewRead.Status);
                }

                var codeView = codeViewRead.Bytes.AsSpan();
                if (BinaryPrimitives.ReadUInt32LittleEndian(codeView) != 0x5344_5352)
                {
                    continue;
                }

                var age = BinaryPrimitives.ReadInt32LittleEndian(codeView.Slice(20, sizeof(int)));
                if (age == 1)
                {
                    var stamp = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(4, sizeof(uint)));
                    portableEntries.Add(DumpPortablePdbDebugIdentity.Create(
                        new Guid(codeView.Slice(4, 16)),
                        stamp,
                        age));
                }
            }

            if (portableEntries.Count == 0)
            {
                return ModuleDebugReadResult.Unavailable(
                    DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable);
            }

            if (portableEntries.Count != 1)
            {
                return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
            }

            return ModuleDebugReadResult.Exact(DumpModulePortablePdbDebugIdentity.Create(
                moduleInfo.Identity,
                moduleContent,
                portableEntries[0]));
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentException or IOException)
        {
            return ModuleDebugReadResult.Invalid(DumpContextEvidenceIssue.InvalidModuleDebugIdentity);
        }
    }

    private static ModuleDebugReadResult IncompleteMappedPeRead(MemoryReadStatus status) =>
        status == MemoryReadStatus.Partial
            ? ModuleDebugReadResult.Partial(DumpContextEvidenceIssue.SourceIncomplete)
            : ModuleDebugReadResult.Unavailable(DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable);

    private static ImmutableArray<PortablePdbCandidate> ReadPortablePdbCandidates(
        ImmutableArray<string> paths,
        DumpModulePortablePdbDebugIdentity expected,
        out DumpPortablePdbObservation? failure)
    {
        var candidates = ImmutableArray.CreateBuilder<PortablePdbCandidate>();
        var invalidSources = new List<DumpPortablePdbEvidenceSource>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaximumPortablePdbByteLength || info.Length > int.MaxValue)
                {
                    failure = DumpPortablePdbObservation.Partial(
                        DumpPortablePdbEvidenceSource.ForModule(expected),
                        DumpContextEvidenceIssue.BoundReached,
                        ImmutableArray.Create(PortablePdbCandidateBound, PortablePdbByteBound));
                    return ImmutableArray<PortablePdbCandidate>.Empty;
                }

                var bytes = ImmutableArray.CreateRange(File.ReadAllBytes(path));
                var content = DumpPortablePdbContentIdentity.Create(
                    bytes.Length,
                    Convert.ToHexString(SHA256.HashData(bytes.AsSpan())).ToLowerInvariant());
                try
                {
                    using var provider = MetadataReaderProvider.FromPortablePdbImage(bytes);
                    var reader = provider.GetMetadataReader();
                    var debugHeader = reader.DebugMetadataHeader ??
                        throw new BadImageFormatException("A Portable PDB requires a debug metadata header.");
                    var contentId = new BlobContentId(debugHeader.Id);
                    var debugIdentity = DumpPortablePdbDebugIdentity.Create(contentId.Guid, contentId.Stamp);
                    candidates.Add(new PortablePdbCandidate(
                        bytes,
                        DumpPortablePdbArtifactIdentity.Create(content, debugIdentity)));
                }
                catch (Exception exception) when (
                    exception is BadImageFormatException or ArgumentException or InvalidOperationException)
                {
                    invalidSources.Add(DumpPortablePdbEvidenceSource.ForCandidate(expected, content, null));
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                continue;
            }
        }

        if (candidates.Count == 0 && invalidSources.Count == 1)
        {
            failure = DumpPortablePdbObservation.Invalid(
                invalidSources[0],
                DumpContextEvidenceIssue.InvalidPortablePdb,
                ImmutableArray.Create(PortablePdbCandidateBound, PortablePdbByteBound));
        }
        else if (candidates.Count == 0 && invalidSources.Count > 1)
        {
            failure = DumpPortablePdbObservation.Ambiguous(
                DumpPortablePdbEvidenceSource.ForModule(expected),
                DumpContextEvidenceIssue.PortablePdbAmbiguous,
                ImmutableArray.Create(PortablePdbCandidateBound, PortablePdbByteBound));
        }
        else
        {
            failure = null;
        }

        return candidates.ToImmutable();
    }

    private DumpPortablePdbObservation ProjectPortablePdbContext(
        DumpSelectedFrameIdentity frame,
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
        PortablePdbCandidate candidate,
        ClrmdModuleInfo moduleInfo,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        var source = DumpPortablePdbEvidenceSource.ForCandidate(
            moduleDebugIdentity,
            candidate.Artifact.Content,
            candidate.Artifact.DebugIdentity);
        var metadata = ReadCompleteMetadata(moduleInfo);
        if (metadata.Status != ClrmdEvidenceStatus.Exact || metadata.Value is null)
        {
            return metadata.Status == ClrmdEvidenceStatus.Partial
                ? DumpPortablePdbObservation.Partial(
                    source,
                    DumpContextEvidenceIssue.SourceIncomplete,
                    bounds)
                : DumpPortablePdbObservation.Unavailable(
                    source,
                    DumpContextEvidenceIssue.ModuleCorrelationUnavailable,
                    bounds);
        }

        try
        {
            using var moduleProvider = MetadataReaderProvider.FromMetadataImage(metadata.Value.Bytes);
            var moduleReader = moduleProvider.GetMetadataReader();
            var observedModuleContent = ModuleContentIdentity.FromMetadata(
                moduleReader.GetGuid(moduleReader.GetModuleDefinition().Mvid),
                metadata.Value.Bytes.AsSpan());
            if (!observedModuleContent.Equals(frame.ModuleContent))
            {
                return DumpPortablePdbObservation.Conflict(
                    source,
                    DumpContextEvidenceIssue.ModuleMismatch,
                    bounds);
            }

            using var pdbProvider = MetadataReaderProvider.FromPortablePdbImage(candidate.Bytes);
            var pdbReader = pdbProvider.GetMetadataReader();
            var methodRow = frame.MethodDefinitionToken & 0x00FF_FFFF;
            if ((frame.MethodDefinitionToken >>> 24) != 0x06 || methodRow == 0 ||
                methodRow > pdbReader.GetTableRowCount(TableIndex.MethodDebugInformation))
            {
                return DumpPortablePdbObservation.Unavailable(
                    source,
                    DumpContextEvidenceIssue.ScopeUnavailable,
                    bounds);
            }

            var methodHandle = MetadataTokens.MethodDefinitionHandle(methodRow);
            var localScopeHandles = pdbReader.GetLocalScopes(methodHandle).ToArray();
            if (localScopeHandles.Length > MaximumPortablePdbLocalScopeCount)
            {
                return DumpPortablePdbObservation.Partial(
                    source,
                    DumpContextEvidenceIssue.BoundReached,
                    bounds);
            }

            var activeRows = localScopeHandles
                .Select(handle => (Handle: handle, Scope: pdbReader.GetLocalScope(handle)))
                .Where(row => row.Scope.StartOffset <= frame.Instruction.IlOffset &&
                    frame.Instruction.IlOffset < row.Scope.EndOffset)
                .OrderBy(static row => row.Scope.StartOffset)
                .ThenByDescending(static row => row.Scope.EndOffset)
                .ThenBy(static row => MetadataTokens.GetRowNumber(row.Handle))
                .ToArray();
            if (activeRows.Length == 0)
            {
                return DumpPortablePdbObservation.Unavailable(
                    source,
                    DumpContextEvidenceIssue.ScopeUnavailable,
                    bounds);
            }

            for (var index = 1; index < activeRows.Length; index++)
            {
                var outer = activeRows[index - 1].Scope;
                var inner = activeRows[index].Scope;
                if (inner.StartOffset < outer.StartOffset || inner.EndOffset > outer.EndOffset)
                {
                    return DumpPortablePdbObservation.Ambiguous(
                        source,
                        DumpContextEvidenceIssue.ScopeAmbiguous,
                        bounds);
                }
            }

            var localScopes = ImmutableArray.CreateBuilder<DumpPortablePdbLocalScopeIdentity>(activeRows.Length);
            for (var index = 0; index < activeRows.Length; index++)
            {
                var row = activeRows[index];
                localScopes.Add(DumpPortablePdbLocalScopeIdentity.Create(
                    MetadataTokens.GetToken(row.Handle),
                    frame.MethodDefinitionToken,
                    row.Scope.ImportScope.IsNil ? null : MetadataTokens.GetToken(row.Scope.ImportScope),
                    row.Scope.StartOffset,
                    row.Scope.Length,
                    index));
            }

            var importHandle = activeRows
                .Select(static row => row.Scope.ImportScope)
                .LastOrDefault(static handle => !handle.IsNil);
            var importRows = new List<(ImportScopeHandle Handle, ImportScope Scope)>();
            var seenImportScopes = new HashSet<int>();
            while (!importHandle.IsNil)
            {
                if (importRows.Count == MaximumPortablePdbImportScopeCount)
                {
                    return DumpPortablePdbObservation.Partial(
                        source,
                        DumpContextEvidenceIssue.BoundReached,
                        bounds);
                }

                var token = MetadataTokens.GetToken(importHandle);
                if (!seenImportScopes.Add(token))
                {
                    return DumpPortablePdbObservation.Invalid(
                        source,
                        DumpContextEvidenceIssue.InvalidScope,
                        bounds);
                }

                var scope = pdbReader.GetImportScope(importHandle);
                importRows.Add((importHandle, scope));
                importHandle = scope.Parent;
            }

            importRows.Reverse();
            var importScopes = ImmutableArray.CreateBuilder<DumpPortablePdbImportScopeIdentity>(importRows.Count);
            var importCount = 0;
            for (var index = 0; index < importRows.Count; index++)
            {
                var row = importRows[index];
                var imports = DecodeImports(
                    pdbReader,
                    moduleReader,
                    row.Handle,
                    row.Scope,
                    ref importCount);
                if (importCount > MaximumPortablePdbImportCount)
                {
                    return DumpPortablePdbObservation.Partial(
                        source,
                        DumpContextEvidenceIssue.BoundReached,
                        bounds);
                }

                importScopes.Add(DumpPortablePdbImportScopeIdentity.Create(
                    MetadataTokens.GetToken(row.Handle),
                    row.Scope.Parent.IsNil ? null : MetadataTokens.GetToken(row.Scope.Parent),
                    index,
                    imports));
            }

            var methodDebugHandle = MetadataTokens.MethodDebugInformationHandle(methodRow);
            var methodDebug = pdbReader.GetMethodDebugInformation(methodDebugHandle);
            DumpPortablePdbDocumentIdentity? document = null;
            if (!methodDebug.Document.IsNil)
            {
                var documentRow = pdbReader.GetDocument(methodDebug.Document);
                var algorithm = documentRow.HashAlgorithm.IsNil
                    ? Guid.Empty
                    : pdbReader.GetGuid(documentRow.HashAlgorithm);
                var checksum = documentRow.Hash.IsNil
                    ? ImmutableArray<byte>.Empty
                    : ImmutableArray.CreateRange(pdbReader.GetBlobBytes(documentRow.Hash));
                document = DumpPortablePdbDocumentIdentity.Create(
                    MetadataTokens.GetToken(methodDebug.Document),
                    documentRow.Language.IsNil ? Guid.Empty : pdbReader.GetGuid(documentRow.Language),
                    algorithm,
                    checksum);
            }

            var facts = DumpPortablePdbContextFacts.Acquire(
                frame,
                moduleDebugIdentity,
                candidate.Artifact,
                MetadataTokens.GetToken(methodDebugHandle),
                document,
                localScopes.ToImmutable(),
                importScopes.ToImmutable());
            return DumpPortablePdbObservation.Exact(facts, bounds);
        }
        catch (UnsupportedImportProjectionException)
        {
            return DumpPortablePdbObservation.Unsupported(
                source,
                DumpContextEvidenceIssue.UnsupportedScope,
                bounds);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentException or
            InvalidOperationException or OverflowException)
        {
            return DumpPortablePdbObservation.Invalid(
                source,
                DumpContextEvidenceIssue.InvalidScope,
                bounds);
        }
    }

    private static ImmutableArray<DumpPortablePdbImportFact> DecodeImports(
        MetadataReader pdbReader,
        MetadataReader moduleReader,
        ImportScopeHandle scopeHandle,
        ImportScope scope,
        ref int totalImportCount)
    {
        if (scope.ImportsBlob.IsNil)
        {
            return ImmutableArray<DumpPortablePdbImportFact>.Empty;
        }

        var completePayload = pdbReader.GetBlobBytes(scope.ImportsBlob);
        var reader = pdbReader.GetBlobReader(scope.ImportsBlob);
        var imports = ImmutableArray.CreateBuilder<DumpPortablePdbImportFact>();
        var scopeToken = MetadataTokens.GetToken(scopeHandle);
        while (reader.RemainingBytes > 0)
        {
            if (totalImportCount == MaximumPortablePdbImportCount)
            {
                totalImportCount++;
                return imports.ToImmutable();
            }

            var start = reader.Offset;
            var rawKindValue = reader.ReadCompressedInteger();
            if ((uint)rawKindValue > byte.MaxValue)
            {
                throw new UnsupportedImportProjectionException();
            }

            var rawKind = (byte)rawKindValue;
            var ordinal = imports.Count;
            DumpPortablePdbImportFact fact;
            switch (rawKind)
            {
                case 1:
                {
                    var targetNamespace = ReadImportString(pdbReader, ref reader);
                    fact = DumpPortablePdbImportFact.NamespaceImport(
                        scopeToken, ordinal, rawKind, targetNamespace,
                        SliceImportPayload(completePayload, start, reader.Offset));
                    break;
                }
                case 2:
                {
                    var assemblyToken = ReadAssemblyReferenceToken(ref reader);
                    var targetNamespace = ReadImportString(pdbReader, ref reader);
                    fact = DumpPortablePdbImportFact.NamespaceImport(
                        scopeToken, ordinal, rawKind, targetNamespace,
                        SliceImportPayload(completePayload, start, reader.Offset), assemblyToken);
                    break;
                }
                case 3:
                {
                    var typeToken = ReadTypeDefinitionOrReferenceToken(ref reader);
                    fact = DumpPortablePdbImportFact.UsingStatic(
                        scopeToken, ordinal, rawKind,
                        GetImportedTypeName(moduleReader, typeToken),
                        typeToken,
                        SliceImportPayload(completePayload, start, reader.Offset));
                    break;
                }
                case 4:
                {
                    _ = ReadImportString(pdbReader, ref reader);
                    _ = ReadImportString(pdbReader, ref reader);
                    fact = DumpPortablePdbImportFact.UnsupportedRaw(
                        scopeToken, ordinal, rawKind,
                        SliceImportPayload(completePayload, start, reader.Offset));
                    break;
                }
                case 5:
                {
                    var alias = ReadImportString(pdbReader, ref reader);
                    fact = DumpPortablePdbImportFact.ExternAlias(
                        scopeToken, ordinal, rawKind, alias,
                        SliceImportPayload(completePayload, start, reader.Offset));
                    break;
                }
                case 6:
                {
                    var alias = ReadImportString(pdbReader, ref reader);
                    var assemblyToken = ReadAssemblyReferenceToken(ref reader);
                    fact = DumpPortablePdbImportFact.ExternAlias(
                        scopeToken, ordinal, rawKind, alias,
                        SliceImportPayload(completePayload, start, reader.Offset), assemblyToken);
                    break;
                }
                case 7:
                {
                    var alias = ReadImportString(pdbReader, ref reader);
                    var targetNamespace = ReadImportString(pdbReader, ref reader);
                    fact = DumpPortablePdbImportFact.NamespaceAlias(
                        scopeToken, ordinal, rawKind, alias, targetNamespace,
                        SliceImportPayload(completePayload, start, reader.Offset));
                    break;
                }
                case 8:
                {
                    var alias = ReadImportString(pdbReader, ref reader);
                    var assemblyToken = ReadAssemblyReferenceToken(ref reader);
                    var targetNamespace = ReadImportString(pdbReader, ref reader);
                    fact = DumpPortablePdbImportFact.NamespaceAlias(
                        scopeToken, ordinal, rawKind, alias, targetNamespace,
                        SliceImportPayload(completePayload, start, reader.Offset), assemblyToken);
                    break;
                }
                case 9:
                {
                    var alias = ReadImportString(pdbReader, ref reader);
                    var typeToken = ReadTypeDefinitionOrReferenceToken(ref reader);
                    fact = DumpPortablePdbImportFact.TypeAlias(
                        scopeToken, ordinal, rawKind, alias,
                        GetImportedTypeName(moduleReader, typeToken),
                        typeToken,
                        SliceImportPayload(completePayload, start, reader.Offset));
                    break;
                }
                default:
                    fact = DumpPortablePdbImportFact.UnsupportedRaw(
                        scopeToken,
                        ordinal,
                        rawKind,
                        ImmutableArray.CreateRange(completePayload.AsSpan(start).ToArray()));
                    reader.Offset = completePayload.Length;
                    break;
            }

            imports.Add(fact);
            totalImportCount++;
        }

        return imports.ToImmutable();
    }

    private static string ReadImportString(MetadataReader reader, ref BlobReader blobReader)
    {
        var offset = blobReader.ReadCompressedInteger();
        if (offset < 0)
        {
            throw new BadImageFormatException("A Portable-PDB import string has a negative heap offset.");
        }

        var bytes = reader.GetBlobBytes(MetadataTokens.BlobHandle(offset));
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes);
    }

    private static int ReadAssemblyReferenceToken(ref BlobReader reader)
    {
        var row = reader.ReadCompressedInteger();
        if (row <= 0 || row > 0x00FF_FFFF)
        {
            throw new BadImageFormatException("A Portable-PDB import has an invalid AssemblyRef row id.");
        }

        return 0x23000000 | row;
    }

    private static int ReadTypeDefinitionOrReferenceToken(ref BlobReader reader)
    {
        var coded = reader.ReadCompressedInteger();
        if (coded <= 0)
        {
            throw new BadImageFormatException("A Portable-PDB import has a nil TypeDefOrRef coded index.");
        }

        var row = coded >>> 2;
        var table = (coded & 3) switch
        {
            0 => 0x02000000,
            1 => 0x01000000,
            2 => 0x1B000000,
            _ => throw new BadImageFormatException("A Portable-PDB import has an invalid TypeDefOrRef tag."),
        };
        if (row <= 0 || row > 0x00FF_FFFF)
        {
            throw new BadImageFormatException("A Portable-PDB import has an invalid TypeDefOrRef row id.");
        }

        return table | row;
    }

    private static string GetImportedTypeName(MetadataReader reader, int token)
    {
        var row = token & 0x00FF_FFFF;
        string namespaceName;
        string typeName;
        switch (token >>> 24)
        {
            case 0x01 when row <= reader.TypeReferences.Count:
            {
                var reference = reader.GetTypeReference(MetadataTokens.TypeReferenceHandle(row));
                namespaceName = reader.GetString(reference.Namespace);
                typeName = reader.GetString(reference.Name);
                break;
            }
            case 0x02 when row <= reader.TypeDefinitions.Count:
            {
                var definition = reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(row));
                namespaceName = reader.GetString(definition.Namespace);
                typeName = reader.GetString(definition.Name);
                break;
            }
            case 0x1B when row <= reader.GetTableRowCount(TableIndex.TypeSpec):
                return "TypeSpec";
            default:
                throw new BadImageFormatException("A Portable-PDB import type token exceeds its module table.");
        }

        return namespaceName.Length == 0 ? typeName : $"{namespaceName}.{typeName}";
    }

    private static ImmutableArray<byte> SliceImportPayload(byte[] completePayload, int start, int end)
    {
        if (start < 0 || end <= start || end > completePayload.Length)
        {
            throw new BadImageFormatException("A Portable-PDB import payload range is invalid.");
        }

        return ImmutableArray.CreateRange(completePayload.AsSpan(start, end - start).ToArray());
    }

    private static bool TryGetMethodAndDeclaringType(
        MetadataReader reader,
        int methodToken,
        int runtimeTypeToken,
        out TypeDefinitionHandle declaringType,
        out string declaringNamespace)
    {
        declaringType = default;
        declaringNamespace = string.Empty;
        var methodRow = methodToken & 0x00FF_FFFF;
        var typeRow = runtimeTypeToken & 0x00FF_FFFF;
        if ((methodToken >>> 24) != 0x06 || methodRow == 0 || methodRow > reader.MethodDefinitions.Count ||
            (runtimeTypeToken >>> 24) != 0x02 || typeRow == 0 || typeRow > reader.TypeDefinitions.Count)
        {
            return false;
        }

        var method = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(methodRow));
        declaringType = method.GetDeclaringType();
        if (MetadataTokens.GetRowNumber(declaringType) != typeRow)
        {
            return false;
        }

        declaringNamespace = reader.GetString(reader.GetTypeDefinition(declaringType).Namespace);
        return true;
    }

    private sealed record PortablePdbCandidate(
        ImmutableArray<byte> Bytes,
        DumpPortablePdbArtifactIdentity Artifact);

    private sealed class UnsupportedImportProjectionException : Exception
    {
    }

    private readonly record struct ModuleDebugReadResult(
        DumpContextEvidenceStatus Status,
        DumpContextEvidenceIssue Issue,
        DumpModulePortablePdbDebugIdentity? Identity)
    {
        internal static ModuleDebugReadResult Exact(DumpModulePortablePdbDebugIdentity identity) =>
            new(DumpContextEvidenceStatus.Exact, DumpContextEvidenceIssue.None, identity);

        internal static ModuleDebugReadResult Partial(DumpContextEvidenceIssue issue) =>
            new(DumpContextEvidenceStatus.Partial, issue, null);

        internal static ModuleDebugReadResult Unavailable(DumpContextEvidenceIssue issue) =>
            new(DumpContextEvidenceStatus.Unavailable, issue, null);

        internal static ModuleDebugReadResult Conflict(DumpContextEvidenceIssue issue) =>
            new(DumpContextEvidenceStatus.Conflict, issue, null);

        internal static ModuleDebugReadResult Invalid(DumpContextEvidenceIssue issue) =>
            new(DumpContextEvidenceStatus.Invalid, issue, null);

        internal static ModuleDebugReadResult Unsupported(DumpContextEvidenceIssue issue) =>
            new(DumpContextEvidenceStatus.Unsupported, issue, null);
    }
}
