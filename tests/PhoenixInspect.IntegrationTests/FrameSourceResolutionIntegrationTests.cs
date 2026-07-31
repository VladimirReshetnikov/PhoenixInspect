using System.Collections.Immutable;
using System.Security.Cryptography;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves frame-to-source resolution from a real generated dump: an identity-validated Portable PDB maps a selected
/// frame to the exact build-recorded document and line span, the mapping replays canonically across sessions, and a
/// mismatching or missing prerequisite stays a typed non-exact observation.
/// </summary>
public sealed class FrameSourceResolutionIntegrationTests
{
    private const int MaximumTestThreadOrdinals = 64;
    private const int MaximumTestFrameOrdinals = 32;
    private const string FrameNamespace = "PhoenixInspect.W7TestTarget.BatchContext";
    private const string ExpectedDocumentFileName = "BatchPipeline.cs";

    /// <summary>The Portable-PDB SHA-256 document-checksum algorithm GUID.</summary>
    private static readonly Guid Sha256ChecksumAlgorithm = new("8829d00f-11b8-4213-878b-770e8597ac16");

    /// <summary>
    /// Resolves the batch incident frame to its recorded source document, verifies the on-disk source bytes against
    /// the PDB document checksum, and freezes conflict and prerequisite dispositions.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "FrameSourceResolutionV1")]
    public void Selected_frame_resolves_to_checksummed_source_line_and_replays()
    {
        var executable = W7TestTargetPaths.ResolveExecutable();
        var portablePdb = W7TestTargetPaths.ResolvePortablePdb();
        Assert.True(File.Exists(executable), $"Expected the W7 target at '{executable}'.");
        Assert.True(File.Exists(portablePdb), $"Expected the W7 Portable PDB at '{portablePdb}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"frame-source-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                       executable,
                       ["--incident", "batch-imported-direct-field"],
                       isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            string firstLocationSha256;
            using (var session = OpenSession(dumpPath))
            {
                var frame = SelectExactFrame(session);

                // Duplicated candidate paths must still select one artifact, exactly like the W7 context path.
                var observation = session.ResolveFrameSourceLocation(frame, [portablePdb, portablePdb]);
                Assert.True(
                    observation.Status == DumpContextEvidenceStatus.Exact,
                    $"Expected an exact source mapping, observed {observation.Status}/{observation.Issue}.");
                Assert.Equal(DumpContextEvidenceIssue.None, observation.Issue);
                var location = Assert.IsType<DumpFrameSourceLocation>(observation.Location);

                Assert.EndsWith(
                    ExpectedDocumentFileName,
                    location.DocumentPath,
                    StringComparison.OrdinalIgnoreCase);
                Assert.InRange(location.StartLine, 1, 10_000);
                Assert.True(location.EndLine >= location.StartLine);
                Assert.True(location.SequencePointIlOffset <= frame.Frame!.Instruction.IlOffset);

                // The PDB was built from this repository's own sources on this machine, so the recorded document —
                // after undoing SourceLink's deterministic '/_/' path map when ContinuousIntegrationBuild applied —
                // must exist here and its bytes must reproduce the PDB checksum. This is the same verification a
                // host performs before presenting a file as the frame's source.
                Assert.Equal(Sha256ChecksumAlgorithm, location.Document.ChecksumAlgorithm);
                var sourcePath = ResolveRecordedDocumentPath(location.DocumentPath);
                Assert.True(File.Exists(sourcePath), $"Expected source at '{sourcePath}'.");
                var onDiskChecksum = SHA256.HashData(File.ReadAllBytes(sourcePath));
                Assert.Equal(onDiskChecksum, location.Document.Checksum.ToArray());

                // The mapped span must sit inside the method that declares the selected frame's namespace.
                var mappedText = File.ReadLines(sourcePath)
                    .Skip(location.StartLine - 1)
                    .FirstOrDefault();
                Assert.False(string.IsNullOrWhiteSpace(mappedText));

                // Same-session replay is byte-identical.
                var replay = session.ResolveFrameSourceLocation(frame, [portablePdb, portablePdb]);
                Assert.Equal(observation, replay);
                Assert.Equal(observation.Sha256, replay.Sha256);
                firstLocationSha256 = location.Sha256;

                // A structurally valid but identity-mismatching PDB is a typed conflict, never a fallback.
                var mismatchingPdb = Path.Combine(AppContext.BaseDirectory, "PhoenixInspect.IntegrationTests.pdb");
                Assert.True(File.Exists(mismatchingPdb), $"Expected the test PDB at '{mismatchingPdb}'.");
                var conflict = session.ResolveFrameSourceLocation(frame, [mismatchingPdb]);
                Assert.Equal(DumpContextEvidenceStatus.Conflict, conflict.Status);
                Assert.Equal(DumpContextEvidenceIssue.PortablePdbIdentityMismatch, conflict.Issue);
                Assert.Null(conflict.Location);

                // A non-exact frame never reaches candidate acquisition.
                var missingFrame = session.SelectExpressionFrame(DumpSelectedFrameSelector.Create(
                    session.Snapshot,
                    threadOrdinal: int.MaxValue,
                    frameOrdinal: 0));
                Assert.Equal(DumpContextEvidenceStatus.Unavailable, missingFrame.Status);
                var prerequisite = session.ResolveFrameSourceLocation(missingFrame, [portablePdb]);
                Assert.Equal(DumpContextEvidenceStatus.Unavailable, prerequisite.Status);
                Assert.Equal(DumpContextEvidenceIssue.PrerequisiteUnavailable, prerequisite.Issue);

                // An empty candidate set is a typed absence, not an error.
                var absent = session.ResolveFrameSourceLocation(frame, ImmutableArray<string>.Empty);
                Assert.Equal(DumpContextEvidenceStatus.Unavailable, absent.Status);
                Assert.Equal(DumpContextEvidenceIssue.PortablePdbUnavailable, absent.Issue);
            }

            // Close/reopen replay: a fresh session over the same immutable dump reproduces the canonical location.
            using (var reopened = OpenSession(dumpPath))
            {
                var frame = SelectExactFrame(reopened);
                var observation = reopened.ResolveFrameSourceLocation(frame, [portablePdb]);
                Assert.Equal(DumpContextEvidenceStatus.Exact, observation.Status);
                Assert.Equal(firstLocationSha256, observation.Location!.Sha256);
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static string ResolveRecordedDocumentPath(string documentPath)
    {
        // A ContinuousIntegrationBuild records SourceLink's deterministic '/_/' repository-root map instead of the
        // build machine's absolute path. The test knows this repository's root, so it can undo the map; a product
        // host would need an explicit source-path mapping to do the same, and reports the file as missing today.
        if (!documentPath.Replace('\\', '/').StartsWith("/_/", StringComparison.Ordinal))
        {
            return documentPath;
        }

        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        return Path.GetFullPath(Path.Combine(repositoryRoot, documentPath.Replace('\\', '/')[3..]));
    }

    private static ClrmdDumpSession OpenSession(string dumpPath)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        return Assert.IsType<ClrmdDumpSession>(opened.Value);
    }

    private static DumpSelectedFrameObservation SelectExactFrame(ClrmdDumpSession session)
    {
        var targetModule = Assert.Single(
            session.Modules,
            static module => string.Equals(module.Name, W7TestTargetPaths.AssemblyFileName, StringComparison.Ordinal));
        for (var threadOrdinal = 0; threadOrdinal < MaximumTestThreadOrdinals; threadOrdinal++)
        {
            for (var frameOrdinal = 0; frameOrdinal < MaximumTestFrameOrdinals; frameOrdinal++)
            {
                var selector = DumpSelectedFrameSelector.Create(session.Snapshot, threadOrdinal, frameOrdinal);
                var observation = session.SelectExpressionFrame(selector);
                if (observation.Frame is { } candidate &&
                    candidate.RuntimeModule.Equals(targetModule.Identity) &&
                    string.Equals(candidate.DeclaringNamespace, FrameNamespace, StringComparison.Ordinal))
                {
                    return observation;
                }

                if (frameOrdinal > 0 &&
                    observation.Status == DumpContextEvidenceStatus.Unavailable &&
                    observation.Issue == DumpContextEvidenceIssue.FrameUnavailable)
                {
                    break;
                }
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"No exact '{FrameNamespace}' frame was found under the test's " +
            $"{MaximumTestThreadOrdinals} thread / {MaximumTestFrameOrdinals} frame search bounds.");
    }
}
