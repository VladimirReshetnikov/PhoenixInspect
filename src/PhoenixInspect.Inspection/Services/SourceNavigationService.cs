using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Inspection;

/// <summary>
/// Projects a selected frame's Portable-PDB source resolution into an honestly labelled display result, including
/// on-disk checksum verification before any file content is presented as the frame's source.
/// </summary>
/// <remarks>
/// The adapter answers which document and line the build recorded; this service answers whether the analysis
/// machine actually has that file with exactly the bytes the build consumed. Content is surfaced only when the
/// document checksum reproduces, because presenting a similar-looking file as the captured code would fabricate
/// evidence. Everything else — a missing file, a mismatching file, an unverifiable document — is a typed, explained
/// limit, mirroring how every other panel treats a non-exact answer.
/// </remarks>
public static class SourceNavigationService
{
    /// <summary>The maximum on-disk source-file size read for display and verification.</summary>
    public const int MaximumSourceFileBytes = 10 * 1024 * 1024;

    /// <summary>The Portable-PDB SHA-256 document-checksum algorithm GUID.</summary>
    private static readonly Guid Sha256Algorithm = new("8829d00f-11b8-4213-878b-770e8597ac16");

    /// <summary>The Portable-PDB SHA-1 document-checksum algorithm GUID.</summary>
    private static readonly Guid Sha1Algorithm = new("406ea660-64cf-4c82-b6f0-42d48172a799");

    /// <summary>
    /// Derives Portable-PDB candidate paths from target-side module path hints that exist on this machine.
    /// </summary>
    /// <param name="session">The open dump session.</param>
    /// <returns>The existing derived paths, possibly empty.</returns>
    /// <remarks>
    /// A hint is a target-side string, never identity: every offered path is still content-validated against the
    /// module's CodeView record before anything binds through it. This mirrors the console host's <c>pdb auto</c>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static ImmutableArray<string> DiscoverPortablePdbCandidates(ClrmdDumpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Modules
            .Select(static module => module.TargetPathHint)
            .Where(static hint => !string.IsNullOrEmpty(hint))
            .Select(static hint => Path.ChangeExtension(hint!, ".pdb"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToImmutableArray();
    }

    /// <summary>Resolves one frame to its verified source view.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="frame">The frame node whose selector reproduces the frame.</param>
    /// <param name="explicitCandidates">Caller-supplied Portable-PDB candidate paths, possibly empty.</param>
    /// <returns>A display projection that never presents unverified content as the frame's source.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static SourceViewResult ResolveFrameSource(
        ClrmdDumpSession session,
        CallStackFrameNode frame,
        ImmutableArray<string> explicitCandidates)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        var discovered = DiscoverPortablePdbCandidates(session);
        var candidates = explicitCandidates
            .Concat(discovered)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(64)
            .ToImmutableArray();

        var observation = session.SelectExpressionFrame(frame.Selector);
        var resolution = session.ResolveFrameSourceLocation(observation, candidates);
        if (resolution.Location is not { } location)
        {
            return new SourceViewResult
            {
                IsResolved = false,
                Title = frame.Header,
                Summary = ExplainFailure(resolution, candidates.Length),
                Sha256 = resolution.Sha256,
                Facts = FailureFacts(resolution, explicitCandidates.Length, discovered.Length),
            };
        }

        var fileName = SafeFileName(location.DocumentPath);
        var (verification, lines, verificationSummary) = VerifyAndRead(location);
        var spanText = location.StartLine == location.EndLine
            ? $"line {location.StartLine}"
            : $"lines {location.StartLine}–{location.EndLine}";
        return new SourceViewResult
        {
            IsResolved = true,
            Title = $"{fileName} — {spanText}",
            Summary = verificationSummary,
            DocumentPath = location.DocumentPath,
            StartLine = location.StartLine,
            EndLine = location.EndLine,
            Verification = verification,
            Lines = lines,
            Sha256 = resolution.Sha256,
            Facts = LocationFacts(location, verificationSummary, explicitCandidates.Length, discovered.Length),
        };
    }

    private static (SourceContentVerification Verification, ImmutableArray<SourceLineRow> Lines, string Summary)
        VerifyAndRead(DumpFrameSourceLocation location)
    {
        if (!File.Exists(location.DocumentPath))
        {
            return (
                SourceContentVerification.FileMissing,
                [],
                $"The build recorded {location.DocumentPath}, and no file exists at that path on this machine. "
                + "The mapping itself is exact; only the local file is missing.");
        }

        byte[] bytes;
        try
        {
            var info = new FileInfo(location.DocumentPath);
            if (info.Length > MaximumSourceFileBytes)
            {
                return (
                    SourceContentVerification.FileTooLarge,
                    [],
                    $"A file exists at the recorded path but exceeds the {MaximumSourceFileBytes:N0}-byte display "
                    + "bound, so its content is not shown.");
            }

            bytes = File.ReadAllBytes(location.DocumentPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (
                SourceContentVerification.FileMissing,
                [],
                "A file exists at the recorded path but could not be read on this machine, so no content is shown.");
        }

        var algorithm = location.Document.ChecksumAlgorithm;
        var recorded = location.Document.Checksum;
        byte[]? computed = null;
        if (algorithm == Sha256Algorithm)
        {
            computed = SHA256.HashData(bytes);
        }
        else if (algorithm == Sha1Algorithm)
        {
            computed = SHA1.HashData(bytes);
        }

        if (computed is null || recorded.IsEmpty)
        {
            return (
                SourceContentVerification.ChecksumUnavailable,
                [],
                "The PDB document carries no checksum this host can verify, so the local file cannot be proven to "
                + "be the build's source and its content is deliberately not shown.");
        }

        if (!computed.AsSpan().SequenceEqual(recorded.AsSpan()))
        {
            return (
                SourceContentVerification.ContentMismatch,
                [],
                "A file exists at the recorded path, but its bytes do not reproduce the PDB's document checksum. "
                + "It is not the source the build consumed, so its content is deliberately not shown.");
        }

        return (
            SourceContentVerification.VerifiedExact,
            ReadLines(bytes, location.StartLine, location.EndLine),
            "The on-disk file reproduces the PDB's document checksum: this is, byte for byte, the source the "
            + "captured build was compiled from.");
    }

    private static ImmutableArray<SourceLineRow> ReadLines(byte[] bytes, int startLine, int endLine)
    {
        var lines = ImmutableArray.CreateBuilder<SourceLineRow>();
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var number = 0;
        while (reader.ReadLine() is { } text)
        {
            number++;
            lines.Add(new SourceLineRow(number, text, number >= startLine && number <= endLine));
        }

        return lines.ToImmutable();
    }

    private static ImmutableArray<PropertyRow> LocationFacts(
        DumpFrameSourceLocation location,
        string verificationSummary,
        int explicitCount,
        int discoveredCount)
    {
        var span = $"{location.StartLine},{location.StartColumn} – {location.EndLine},{location.EndColumn}";
        var offsetDisposition = location.IsExactIlOffsetMatch
            ? "the frame IL offset sits exactly on the mapped sequence point"
            : "the frame IL offset falls inside the mapped sequence point's span";
        return
        [
            new PropertyRow("Resolution", "Document", location.DocumentPath, "The path the build recorded in the PDB."),
            new PropertyRow("Resolution", "Mapped span", span, offsetDisposition),
            new PropertyRow(
                "Resolution",
                "Sequence point IL offset",
                DisplayFormatting.Count(location.SequencePointIlOffset)),
            new PropertyRow(
                "Resolution",
                "Portable PDB",
                DisplayFormatting.ShortDigest(location.Artifact.Content.Sha256),
                $"Identity-validated against the module's CodeView record; {explicitCount} explicit and "
                + $"{discoveredCount} hint-derived candidates were offered."),
            new PropertyRow(
                "Verification",
                "Document checksum",
                DisplayFormatting.HexPreview(location.Document.Checksum, 32),
                verificationSummary),
        ];
    }

    private static ImmutableArray<PropertyRow> FailureFacts(
        DumpFrameSourceObservation resolution,
        int explicitCount,
        int discoveredCount) =>
        [
            new PropertyRow("Resolution", "Status", resolution.Status.ToString()),
            new PropertyRow("Resolution", "Issue", resolution.Issue.ToString()),
            new PropertyRow(
                "Resolution",
                "Candidates",
                $"{explicitCount} explicit · {discoveredCount} hint-derived"),
        ];

    private static string ExplainFailure(DumpFrameSourceObservation resolution, int candidateCount) =>
        resolution.Issue switch
        {
            DumpContextEvidenceIssue.PrerequisiteUnavailable =>
                "The selected frame is not exact, so no source mapping was attempted.",
            DumpContextEvidenceIssue.PortablePdbUnavailable when candidateCount == 0 =>
                "No Portable-PDB candidate was offered or discovered. Offer one in the evaluation options, or place "
                + "the build's PDB next to the target assembly.",
            DumpContextEvidenceIssue.PortablePdbUnavailable =>
                "No offered Portable-PDB candidate could be read on this machine.",
            DumpContextEvidenceIssue.PortablePdbIdentityMismatch =>
                "An offered Portable PDB is well formed but does not match this module's CodeView identity. A file "
                + "with the right name is not evidence; only the identity-matching PDB can name the source.",
            DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable =>
                "The mapped module carries no Portable-PDB CodeView identity, so no candidate can be validated.",
            DumpContextEvidenceIssue.SequencePointsUnavailable =>
                "The matching PDB has no non-hidden sequence point at or before this frame's IL offset, so no "
                + "source line can be named for it.",
            DumpContextEvidenceIssue.DocumentUnavailable =>
                "The mapped sequence point does not name a source document.",
            _ =>
                $"Source resolution stopped: {DisplayFormatting.Humanize(resolution.Status.ToString())} — "
                + $"{DisplayFormatting.Humanize(resolution.Issue.ToString())}.",
        };

    private static string SafeFileName(string documentPath)
    {
        try
        {
            var name = Path.GetFileName(documentPath);
            return string.IsNullOrEmpty(name) ? documentPath : name;
        }
        catch (ArgumentException)
        {
            return documentPath;
        }
    }
}
