using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Verifies that content-identified dump objects cannot cross immutable session boundaries.</summary>
public sealed class ForeignSnapshotIsolationIntegrationTests
{
    /// <summary>
    /// Captures two independent snapshots of one stable fixture process and proves that session B rejects an object
    /// selected from session A before performing any field-memory read.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Field_reads_reject_an_object_from_an_independent_snapshot_without_reading_memory()
    {
        var targetExecutablePath = TestTargetPaths.ResolveExecutable();
        Assert.True(File.Exists(targetExecutablePath));
        var fixtureId = Guid.NewGuid().ToString("N");
        var firstDumpPath = Path.Combine(Path.GetTempPath(), $"snapshot-isolation-a-{fixtureId}.dmp");
        var secondDumpPath = Path.Combine(Path.GetTempPath(), $"snapshot-isolation-b-{fixtureId}.dmp");

        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(targetExecutablePath))
            {
                DumpWriter.WriteFullDump(target.Pid, firstDumpPath);
                DumpWriter.WriteFullDump(target.Pid, secondDumpPath);
            }

            var firstOpen = ClrmdDumpSession.Open(firstDumpPath);
            var secondOpen = ClrmdDumpSession.Open(secondDumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, firstOpen.Status);
            Assert.Equal(ClrmdEvidenceStatus.Exact, secondOpen.Status);
            using var firstSession = firstOpen.Value
                ?? throw new InvalidOperationException("The first exact dump-open result carried no session.");
            using var secondSession = secondOpen.Value
                ?? throw new InvalidOperationException("The second exact dump-open result carried no session.");

            Assert.Matches("^[0-9a-f]{64}$", firstSession.Snapshot.Sha256);
            Assert.Matches("^[0-9a-f]{64}$", secondSession.Snapshot.Sha256);
            Assert.NotEqual(firstSession.Snapshot.Sha256, secondSession.Snapshot.Sha256);

            var foreignModule = Assert.Single(firstSession.FindModulesByFileName("Interpreter.TestTarget.dll"));
            var moduleIdentity = secondSession.ReadModuleContentIdentity(foreignModule);
            Assert.Equal(ClrmdEvidenceStatus.Conflict, moduleIdentity.Status);
            Assert.Equal(ClrmdValueIssue.SnapshotMismatch, moduleIdentity.Issue);
            Assert.False(moduleIdentity.HasValue);
            Assert.Empty(moduleIdentity.Evidence);

            var objectSearch = firstSession.FindStrongHandleObjectsByTypeName(
                "DumpProbe",
                maximumMatches: 2,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, objectSearch.Status);
            var foreignObject = Assert.Single(objectSearch.Matches);
            Assert.Equal(firstSession.Snapshot, foreignObject.Snapshot);
            Assert.NotEqual(secondSession.Snapshot, foreignObject.Snapshot);

            var field = secondSession.GetInstanceField(foreignObject, "Marker");
            Assert.Equal(ClrmdEvidenceStatus.Conflict, field.Status);
            Assert.Equal(ClrmdValueIssue.SnapshotMismatch, field.Issue);
            Assert.False(field.HasValue);
            Assert.Empty(field.Evidence);
            Assert.Empty(field.AppliedBounds);

            var integer = secondSession.ReadInt32Field(foreignObject, "Marker");
            Assert.Equal(ClrmdEvidenceStatus.Conflict, integer.Status);
            Assert.Equal(ClrmdValueIssue.SnapshotMismatch, integer.Issue);
            Assert.False(integer.HasValue);
            Assert.Empty(integer.Evidence);
            Assert.Empty(integer.AppliedBounds);

            var text = secondSession.ReadStringField(foreignObject, "Message", maximumCharacters: 1024);
            Assert.Equal(ClrmdEvidenceStatus.Conflict, text.Status);
            Assert.Equal(ClrmdValueIssue.SnapshotMismatch, text.Issue);
            Assert.False(text.IsNull);
            Assert.Null(text.Value);
            Assert.Empty(text.Evidence);

            var query = DumpQueryEngine.Evaluate(
                secondSession,
                "root.Marker",
                "root",
                foreignObject,
                ImmutableArray.Create(
                    new EvaluationDeterministicBound(
                        "root-selection.maximum-handles",
                        objectSearch.MaximumHandlesScanned),
                    new EvaluationDeterministicBound(
                        "root-selection.maximum-matches",
                        objectSearch.MaximumMatches)));
            Assert.Equal(EvaluationCompletionStatus.Blocked, query.Completion);
            Assert.Equal(EvaluationEvidenceStatus.Conflict, query.Evidence);
            Assert.Equal(secondSession.Snapshot.MemorySourceId, query.Context.Snapshot.SourceId);
            Assert.Equal(EvaluationIdentityAvailability.Unavailable, query.Context.Module.Availability);
            Assert.Null(query.Context.Module.SourceId);
            Assert.Equal(EvaluationFallbackStatus.None, query.Context.Fallback.Status);
            Assert.DoesNotContain(
                query.Context.Bounds,
                static bound => bound.Name is "dump.instance-fields.traversed" or "dump.memory-read.bytes");
            Assert.DoesNotContain(
                query.Provenance,
                static provenance => provenance.Kind == EvaluationProvenanceKind.DumpMemory);
            Assert.Equal("DUMP_SNAPSHOT_MISMATCH", Assert.Single(query.Diagnostics).Code);
        }
        finally
        {
            DeleteIfPresent(firstDumpPath);
            DeleteIfPresent(secondDumpPath);
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
