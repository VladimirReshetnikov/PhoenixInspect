using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Freezes the exact source location a selected managed frame maps to through one identity-validated Portable PDB.
/// </summary>
/// <remarks>
/// The location is entirely build-time evidence: the document path is the string the compiler recorded in the
/// Portable PDB, not a probed analysis-machine path, and the document checksum names the exact source bytes the
/// build consumed. Whether a file with that content exists on the analysis machine is a separate host concern that
/// this contract deliberately does not answer. The mapped line span comes from the closest preceding non-hidden
/// sequence point, which is the standard debugger mapping; <see cref="IsExactIlOffsetMatch"/> records whether the
/// frame's IL offset sat exactly on that sequence point or inside its span.
/// </remarks>
public sealed class DumpFrameSourceLocation : IEquatable<DumpFrameSourceLocation>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpFrameSourceLocation(
        DumpSelectedFrameIdentity frame,
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
        DumpPortablePdbArtifactIdentity artifact,
        DumpPortablePdbDocumentIdentity document,
        string documentPath,
        int sequencePointIlOffset,
        bool isExactIlOffsetMatch,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        Frame = frame;
        ModuleDebugIdentity = moduleDebugIdentity;
        Artifact = artifact;
        Document = document;
        DocumentPath = documentPath;
        SequencePointIlOffset = sequencePointIlOffset;
        IsExactIlOffsetMatch = isExactIlOffsetMatch;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;

        var writer = new CanonicalReplayEncoding.Writer("dump-frame-source-location", 1);
        writer.WriteLengthPrefixedBytes(frame.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(moduleDebugIdentity.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(artifact.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(document.CanonicalBytes.AsSpan());
        writer.WriteString(documentPath);
        writer.WriteInt32(sequencePointIlOffset);
        writer.WriteBoolean(isExactIlOffsetMatch);
        writer.WriteInt32(startLine);
        writer.WriteInt32(startColumn);
        writer.WriteInt32(endLine);
        writer.WriteInt32(endColumn);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact selected frame this location was resolved for.</summary>
    public DumpSelectedFrameIdentity Frame { get; }

    /// <summary>Gets the mapped-module CodeView identity the Portable PDB was validated against.</summary>
    public DumpModulePortablePdbDebugIdentity ModuleDebugIdentity { get; }

    /// <summary>Gets the identity of the sole matching Portable-PDB artifact that supplied the mapping.</summary>
    public DumpPortablePdbArtifactIdentity Artifact { get; }

    /// <summary>Gets the path-independent identity of the mapped document row.</summary>
    public DumpPortablePdbDocumentIdentity Document { get; }

    /// <summary>Gets the document path exactly as the build recorded it in the Portable PDB.</summary>
    /// <remarks>
    /// This is the build machine's spelling. It is deterministic PDB content, not an analysis-machine probe result,
    /// and it may name a directory that does not exist where the dump is being inspected.
    /// </remarks>
    public string DocumentPath { get; }

    /// <summary>Gets the IL start offset of the mapped sequence point.</summary>
    public int SequencePointIlOffset { get; }

    /// <summary>
    /// Gets whether the frame's IL offset equals <see cref="SequencePointIlOffset"/> rather than falling inside the
    /// span of the closest preceding non-hidden sequence point.
    /// </summary>
    public bool IsExactIlOffsetMatch { get; }

    /// <summary>Gets the one-based start line of the mapped span.</summary>
    public int StartLine { get; }

    /// <summary>Gets the one-based start column of the mapped span.</summary>
    public int StartColumn { get; }

    /// <summary>Gets the one-based end line of the mapped span; it is never before <see cref="StartLine"/>.</summary>
    public int EndLine { get; }

    /// <summary>Gets the one-based end column of the mapped span.</summary>
    public int EndColumn { get; }

    /// <summary>Gets a defensive copy of the versioned canonical location bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a validated frame source location.</summary>
    /// <param name="frame">The exact selected frame.</param>
    /// <param name="moduleDebugIdentity">The mapped-module CodeView identity the artifact matched.</param>
    /// <param name="artifact">The matching Portable-PDB artifact identity.</param>
    /// <param name="document">The mapped document-row identity.</param>
    /// <param name="documentPath">The non-blank document path recorded by the build.</param>
    /// <param name="sequencePointIlOffset">The non-negative IL start offset of the mapped sequence point.</param>
    /// <param name="isExactIlOffsetMatch">Whether the frame IL offset equals the sequence-point start offset.</param>
    /// <param name="startLine">The one-based mapped start line.</param>
    /// <param name="startColumn">The one-based mapped start column.</param>
    /// <param name="endLine">The one-based mapped end line, at or after <paramref name="startLine"/>.</param>
    /// <param name="endColumn">
    /// The one-based mapped end column, after <paramref name="startColumn"/> when the span is one line.
    /// </param>
    /// <returns>An immutable, canonically encoded source location.</returns>
    /// <exception cref="ArgumentNullException">A required identity is null.</exception>
    /// <exception cref="ArgumentException">The document path is blank or the span is not a valid forward span.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An offset, line, or column is outside its valid range.</exception>
    public static DumpFrameSourceLocation Create(
        DumpSelectedFrameIdentity frame,
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
        DumpPortablePdbArtifactIdentity artifact,
        DumpPortablePdbDocumentIdentity document,
        string documentPath,
        int sequencePointIlOffset,
        bool isExactIlOffsetMatch,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(moduleDebugIdentity);
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(document);
        DumpContextContractEncoding.ValidateRequiredText(documentPath, nameof(documentPath));
        if (sequencePointIlOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequencePointIlOffset),
                "A sequence-point IL offset cannot be negative.");
        }

        if (isExactIlOffsetMatch && frame.Instruction.IlOffset != sequencePointIlOffset)
        {
            throw new ArgumentException(
                "An exact IL-offset match requires the frame offset to equal the sequence-point offset.",
                nameof(isExactIlOffsetMatch));
        }

        if (startLine < 1 || endLine < startLine)
        {
            throw new ArgumentOutOfRangeException(nameof(endLine), "A mapped span requires forward one-based lines.");
        }

        if (startColumn < 1 || endColumn < 1 || (startLine == endLine && endColumn <= startColumn))
        {
            throw new ArgumentOutOfRangeException(
                nameof(endColumn),
                "A mapped span requires forward one-based columns.");
        }

        return new DumpFrameSourceLocation(
            frame,
            moduleDebugIdentity,
            artifact,
            document,
            documentPath,
            sequencePointIlOffset,
            isExactIlOffsetMatch,
            startLine,
            startColumn,
            endLine,
            endColumn);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The location to compare.</param>
    /// <returns><see langword="true"/> when every replay-significant field is equal.</returns>
    public bool Equals(DumpFrameSourceLocation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpFrameSourceLocation);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>
/// Carries one typed frame-to-source resolution outcome: an exact mapped location, or the first boundary that
/// prevented one.
/// </summary>
/// <remarks>
/// Only <see cref="DumpContextEvidenceStatus.Exact"/> carries <see cref="Location"/>. Every other status retains the
/// attempted selector, a stable issue, and the reached bounds, so a missing PDB, an identity mismatch, and a method
/// without sequence points remain distinguishable instead of collapsing into one generic failure.
/// </remarks>
public sealed class DumpFrameSourceObservation : IEquatable<DumpFrameSourceObservation>
{
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpFrameSourceObservation(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpSelectedFrameSelector selector,
        DumpFrameSourceLocation? location,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        DumpContextContractEncoding.ValidateStatusIssue(status, issue);
        DumpContextContractEncoding.ValidateFrameSourceStatusIssue(status, issue);
        if ((status == DumpContextEvidenceStatus.Exact) != (location is not null))
        {
            throw new ArgumentException(
                "Exactly the Exact status must carry a frame source location.",
                nameof(location));
        }

        if (location is not null && !location.Frame.Selector.Equals(selector))
        {
            throw new ArgumentException(
                "An exact location must satisfy the observation selector.",
                nameof(location));
        }

        Status = status;
        Issue = issue;
        Selector = selector;
        Location = location;
        this.reachedBounds = CanonicalReplayEncoding.Copy(reachedBounds);

        var writer = new CanonicalReplayEncoding.Writer("dump-frame-source-observation", 1);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(selector.CanonicalBytes.AsSpan());
        DumpContextContractEncoding.WriteBounds(writer, reachedBounds);
        writer.WriteBoolean(location is not null);
        if (location is not null)
        {
            writer.WriteLengthPrefixedBytes(location.CanonicalBytes.AsSpan());
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the typed evidence precision and disposition.</summary>
    public DumpContextEvidenceStatus Status { get; }

    /// <summary>Gets the stable first-boundary issue; exact evidence always uses <c>None</c>.</summary>
    public DumpContextEvidenceIssue Issue { get; }

    /// <summary>Gets the snapshot-scoped selector attempted by the producer.</summary>
    public DumpSelectedFrameSelector Selector { get; }

    /// <summary>Gets the exact location only when <see cref="Status"/> is <c>Exact</c>; otherwise gets null.</summary>
    public DumpFrameSourceLocation? Location { get; }

    /// <summary>Gets whether this observation carries one complete mapped location.</summary>
    public bool HasExactLocation => Location is not null;

    /// <summary>Gets a defensive copy of the deterministic bounds reached by this resolution path.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds =>
        CanonicalReplayEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the versioned canonical observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact observation carrying one complete mapped location.</summary>
    /// <param name="location">The complete mapped source location.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An exact observation whose issue is <c>None</c>.</returns>
    public static DumpFrameSourceObservation Exact(
        DumpFrameSourceLocation location,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Create(
            DumpContextEvidenceStatus.Exact,
            DumpContextEvidenceIssue.None,
            location.Frame.Selector,
            location,
            reachedBounds);
    }

    /// <summary>Creates a partial observation without a mapped location.</summary>
    /// <param name="selector">The selector whose bounded resolution was incomplete.</param>
    /// <param name="issue">A partial-status issue such as a reached bound or incomplete source.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>A partial observation carrying no location payload.</returns>
    public static DumpFrameSourceObservation Partial(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Partial, issue, selector, null, reachedBounds);

    /// <summary>Creates an unavailable observation without a mapped location.</summary>
    /// <param name="selector">The selector whose required source was unavailable.</param>
    /// <param name="issue">An unavailable-status issue naming the first missing source.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An unavailable observation carrying no location payload.</returns>
    public static DumpFrameSourceObservation Unavailable(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unavailable, issue, selector, null, reachedBounds);

    /// <summary>Creates an ambiguous observation without selecting among competing artifacts.</summary>
    /// <param name="selector">The selector for which multiple matching artifacts remained.</param>
    /// <param name="issue">An ambiguity issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An ambiguous observation carrying no location payload.</returns>
    public static DumpFrameSourceObservation Ambiguous(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Ambiguous, issue, selector, null, reachedBounds);

    /// <summary>Creates a conflict observation without exposing either identity as exact.</summary>
    /// <param name="selector">The selector associated with the conflicting evidence.</param>
    /// <param name="issue">A conflict-status issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>A conflict observation carrying no location payload.</returns>
    public static DumpFrameSourceObservation Conflict(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Conflict, issue, selector, null, reachedBounds);

    /// <summary>Creates an invalid observation without retaining malformed evidence.</summary>
    /// <param name="selector">The selector associated with the invalid source.</param>
    /// <param name="issue">An invalid-status issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An invalid observation carrying no location payload.</returns>
    public static DumpFrameSourceObservation Invalid(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Invalid, issue, selector, null, reachedBounds);

    /// <summary>Creates an unsupported observation without manufacturing a mapped location.</summary>
    /// <param name="selector">The selector associated with the unsupported representation.</param>
    /// <param name="issue">An unsupported-status issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An unsupported observation carrying no location payload.</returns>
    public static DumpFrameSourceObservation Unsupported(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unsupported, issue, selector, null, reachedBounds);

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The frame-source observation to compare.</param>
    /// <returns><see langword="true"/> when status, selector, bounds, and exact payload are equal.</returns>
    public bool Equals(DumpFrameSourceObservation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpFrameSourceObservation);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private static DumpFrameSourceObservation Create(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpSelectedFrameSelector selector,
        DumpFrameSourceLocation? location,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var normalizedBounds = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, nameof(reachedBounds));
        return new DumpFrameSourceObservation(status, issue, selector, location, normalizedBounds);
    }
}
