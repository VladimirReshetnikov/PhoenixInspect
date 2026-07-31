using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

public sealed partial class ClrmdDumpSession
{
    private const int MaximumPortablePdbSequencePointCount = 65_536;

    private static readonly EvaluationDeterministicBound PortablePdbSequencePointBound =
        new("dump.context.portable-pdb-sequence-points", MaximumPortablePdbSequencePointCount);

    /// <summary>Gets the maximum sequence-point rows admitted while mapping one frame to a source line.</summary>
    public static EvaluationDeterministicBound PortablePdbSequencePointTraversalBound => PortablePdbSequencePointBound;

    /// <summary>
    /// Resolves the source document and line span a selected frame maps to through one identity-validated
    /// Portable PDB.
    /// </summary>
    /// <param name="selectedFrame">The independently typed selected-frame observation.</param>
    /// <param name="portablePdbCandidates">
    /// An explicitly initialized bounded array of caller-discovered local candidate paths. Paths are discovery hints
    /// only and never enter returned identities.
    /// </param>
    /// <returns>An exact mapped location or a typed non-exact observation scoped to this dump.</returns>
    /// <remarks>
    /// Candidate validation is identical to the expression-context path: the module's mapped CodeView GUID/stamp is
    /// read from counted dump memory, every existing candidate is completely hashed, and only a content-validated
    /// identity match may supply the mapping. The mapping itself uses the closest preceding non-hidden sequence
    /// point for the frame's IL offset, which is the standard debugger rule. The returned document path is the
    /// build-recorded string from the PDB; this operation neither probes the analysis machine for that file nor
    /// claims it exists.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="selectedFrame"/> is null.</exception>
    /// <exception cref="ArgumentException">The candidate array is default or contains a null/blank path.</exception>
    public DumpFrameSourceObservation ResolveFrameSourceLocation(
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

        var bounds = FrameSourceBounds();
        if (portablePdbCandidates.Length > MaximumPortablePdbCandidateCount)
        {
            return DumpFrameSourceObservation.Partial(
                selectedFrame.Selector,
                DumpContextEvidenceIssue.BoundReached,
                bounds);
        }

        return ResolveFrameSourceLocationWithResolver(
            selectedFrame,
            new PathPortablePdbArtifactResolver(portablePdbCandidates),
            bounds);
    }

    /// <summary>Resolves a frame's source location through one bounded host artifact resolver.</summary>
    /// <param name="selectedFrame">The independently typed selected-frame observation.</param>
    /// <param name="artifactResolver">
    /// The host capability that returns complete, partial, or unavailable candidate reads without selecting a winner.
    /// </param>
    /// <returns>An exact mapped location or a typed non-exact observation scoped to this dump.</returns>
    /// <remarks>
    /// The resolver is called only after an exact selected frame and counted mapped-PE CodeView evidence are
    /// available; a non-exact frame short-circuits to a typed unavailable observation without invoking it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public DumpFrameSourceObservation ResolveFrameSourceLocation(
        DumpSelectedFrameObservation selectedFrame,
        IDumpPortablePdbArtifactResolver artifactResolver)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(selectedFrame);
        ArgumentNullException.ThrowIfNull(artifactResolver);
        return ResolveFrameSourceLocationWithResolver(selectedFrame, artifactResolver, FrameSourceBounds());
    }

    private static ImmutableArray<EvaluationDeterministicBound> FrameSourceBounds() =>
        [
            MappedPeHeaderByteBound,
            MappedPeDebugDirectoryBound,
            MappedPeCodeViewByteBound,
            PortablePdbCandidateBound,
            PortablePdbByteBound,
            PortablePdbSequencePointBound,
        ];

    private DumpFrameSourceObservation ResolveFrameSourceLocationWithResolver(
        DumpSelectedFrameObservation selectedFrame,
        IDumpPortablePdbArtifactResolver artifactResolver,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        var selector = selectedFrame.Selector;
        if (selector.Snapshot != Snapshot)
        {
            return DumpFrameSourceObservation.Conflict(
                selector,
                DumpContextEvidenceIssue.SnapshotMismatch,
                bounds);
        }

        if (selectedFrame.Frame is not { } frame)
        {
            return DumpFrameSourceObservation.Unavailable(
                selector,
                DumpContextEvidenceIssue.PrerequisiteUnavailable,
                ImmutableArray<EvaluationDeterministicBound>.Empty);
        }

        // Candidate acquisition deliberately mirrors ReadExpressionPortablePdbContextCore instead of refactoring
        // it: the W7 expression-context path is frozen behavior evidence, and this projection must not change its
        // observable dispositions. Failures translate status/issue one-to-one into frame-source observations.
        if (!_moduleInfos.TryGetValue(
                (frame.RuntimeModule.AppDomainAddress, frame.RuntimeModule.ModuleAddress),
                out var moduleInfo) ||
            !_runtimeModules.TryGetValue(moduleInfo.Identity, out var runtimeModule))
        {
            return DumpFrameSourceObservation.Unavailable(
                selector,
                DumpContextEvidenceIssue.ModuleCorrelationUnavailable,
                bounds);
        }

        if (!frame.RuntimeModule.Equals(moduleInfo.Identity))
        {
            return DumpFrameSourceObservation.Conflict(
                selector,
                DumpContextEvidenceIssue.ModuleMismatch,
                bounds);
        }

        var moduleDebug = ReadModulePortablePdbDebugIdentity(runtimeModule, moduleInfo, frame.ModuleContent);
        if (moduleDebug.Status != DumpContextEvidenceStatus.Exact || moduleDebug.Identity is null)
        {
            return TranslateStatus(selector, moduleDebug.Status, moduleDebug.Issue, bounds);
        }

        var expected = moduleDebug.Identity;
        var candidates = ReadPortablePdbCandidates(
            artifactResolver,
            expected,
            bounds,
            out var candidateFailure);
        if (candidates.Length == 0)
        {
            if (candidateFailure is { } failure)
            {
                return TranslateStatus(selector, failure.Status, failure.Issue, bounds);
            }

            return DumpFrameSourceObservation.Unavailable(
                selector,
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
            var distinctMismatches = candidates
                .GroupBy(candidate => candidate.Artifact.Content.Sha256, StringComparer.Ordinal)
                .Count();
            return distinctMismatches == 1
                ? DumpFrameSourceObservation.Conflict(
                    selector,
                    DumpContextEvidenceIssue.PortablePdbIdentityMismatch,
                    bounds)
                : DumpFrameSourceObservation.Ambiguous(
                    selector,
                    DumpContextEvidenceIssue.PortablePdbAmbiguous,
                    bounds);
        }

        if (matching.Length != 1)
        {
            return DumpFrameSourceObservation.Ambiguous(
                selector,
                DumpContextEvidenceIssue.PortablePdbAmbiguous,
                bounds);
        }

        return ProjectFrameSourceLocation(selector, frame, expected, matching[0], bounds);
    }

    private static DumpFrameSourceObservation ProjectFrameSourceLocation(
        DumpSelectedFrameSelector selector,
        DumpSelectedFrameIdentity frame,
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
        PortablePdbCandidate candidate,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        try
        {
            using var pdbProvider = MetadataReaderProvider.FromPortablePdbImage(candidate.Bytes);
            var pdbReader = pdbProvider.GetMetadataReader();
            var methodRow = frame.MethodDefinitionToken & 0x00FF_FFFF;
            if ((frame.MethodDefinitionToken >>> 24) != 0x06 || methodRow == 0 ||
                methodRow > pdbReader.GetTableRowCount(TableIndex.MethodDebugInformation))
            {
                return DumpFrameSourceObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.SequencePointsUnavailable,
                    bounds);
            }

            var methodDebug = pdbReader.GetMethodDebugInformation(
                MetadataTokens.MethodDebugInformationHandle(methodRow));
            if (methodDebug.SequencePointsBlob.IsNil)
            {
                return DumpFrameSourceObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.SequencePointsUnavailable,
                    bounds);
            }

            // Sequence points are stored in ascending IL order, so the last retained candidate is the closest
            // preceding non-hidden point for the frame's IL offset.
            var pointCount = 0;
            SequencePoint? mapped = null;
            foreach (var point in methodDebug.GetSequencePoints())
            {
                if (++pointCount > MaximumPortablePdbSequencePointCount)
                {
                    return DumpFrameSourceObservation.Partial(
                        selector,
                        DumpContextEvidenceIssue.BoundReached,
                        bounds);
                }

                if (point.IsHidden || point.Offset > frame.Instruction.IlOffset)
                {
                    continue;
                }

                if (mapped is null || point.Offset >= mapped.Value.Offset)
                {
                    mapped = point;
                }
            }

            if (mapped is not { } chosen)
            {
                return DumpFrameSourceObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.SequencePointsUnavailable,
                    bounds);
            }

            var documentHandle = chosen.Document.IsNil ? methodDebug.Document : chosen.Document;
            if (documentHandle.IsNil)
            {
                return DumpFrameSourceObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.DocumentUnavailable,
                    bounds);
            }

            var documentRow = pdbReader.GetDocument(documentHandle);
            if (documentRow.Name.IsNil)
            {
                return DumpFrameSourceObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.DocumentUnavailable,
                    bounds);
            }

            var documentPath = pdbReader.GetString(documentRow.Name);
            if (string.IsNullOrWhiteSpace(documentPath))
            {
                return DumpFrameSourceObservation.Unavailable(
                    selector,
                    DumpContextEvidenceIssue.DocumentUnavailable,
                    bounds);
            }

            var checksumAlgorithm = documentRow.HashAlgorithm.IsNil
                ? Guid.Empty
                : pdbReader.GetGuid(documentRow.HashAlgorithm);
            var checksum = documentRow.Hash.IsNil
                ? ImmutableArray<byte>.Empty
                : ImmutableArray.CreateRange(pdbReader.GetBlobBytes(documentRow.Hash));
            var document = DumpPortablePdbDocumentIdentity.Create(
                MetadataTokens.GetToken(documentHandle),
                documentRow.Language.IsNil ? Guid.Empty : pdbReader.GetGuid(documentRow.Language),
                checksumAlgorithm,
                checksum);
            var location = DumpFrameSourceLocation.Create(
                frame,
                moduleDebugIdentity,
                candidate.Artifact,
                document,
                documentPath,
                chosen.Offset,
                chosen.Offset == frame.Instruction.IlOffset,
                chosen.StartLine,
                chosen.StartColumn,
                chosen.EndLine,
                chosen.EndColumn);
            return DumpFrameSourceObservation.Exact(location, bounds);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentException or
            InvalidOperationException or OverflowException)
        {
            return DumpFrameSourceObservation.Invalid(
                selector,
                DumpContextEvidenceIssue.InvalidPortablePdb,
                bounds);
        }
    }

    private static DumpFrameSourceObservation TranslateStatus(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> bounds) =>
        status switch
        {
            DumpContextEvidenceStatus.Partial => DumpFrameSourceObservation.Partial(selector, issue, bounds),
            DumpContextEvidenceStatus.Unavailable => DumpFrameSourceObservation.Unavailable(selector, issue, bounds),
            DumpContextEvidenceStatus.Ambiguous => DumpFrameSourceObservation.Ambiguous(selector, issue, bounds),
            DumpContextEvidenceStatus.Conflict => DumpFrameSourceObservation.Conflict(selector, issue, bounds),
            DumpContextEvidenceStatus.Invalid => DumpFrameSourceObservation.Invalid(selector, issue, bounds),
            _ => DumpFrameSourceObservation.Unsupported(selector, issue, bounds),
        };
}
