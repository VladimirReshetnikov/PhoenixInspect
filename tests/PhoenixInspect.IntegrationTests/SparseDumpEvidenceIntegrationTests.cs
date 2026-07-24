using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Microsoft.Diagnostics.NETCore.Client;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises byte-accurate raw-memory classification against a real heap-sparse minidump.</summary>
public sealed class SparseDumpEvidenceIntegrationTests
{
    private const int ProbeReadLength = 64;

    /// <summary>
    /// Captures normal and full dumps from one isolated fixture process, selects the rooted probe from the full
    /// snapshot, and verifies that the normal dump reports the omitted object range without synthesizing bytes.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Normal_dump_reports_an_omitted_heap_object_range_without_zero_filling()
    {
        var targetExecutablePath = TestTargetPaths.ResolveExecutable();
        Assert.True(File.Exists(targetExecutablePath));
        var fixtureId = Guid.NewGuid().ToString("N");
        var normalDumpPath = Path.Combine(Path.GetTempPath(), $"sparse-evidence-normal-{fixtureId}.dmp");
        var fullDumpPath = Path.Combine(Path.GetTempPath(), $"sparse-evidence-full-{fixtureId}.dmp");

        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(targetExecutablePath))
            {
                var diagnosticsClient = new DiagnosticsClient(target.Pid);
                diagnosticsClient.WriteDump(DumpType.Normal, normalDumpPath, logDumpGeneration: false);
                DumpWriter.WriteFullDump(target.Pid, fullDumpPath);
            }

            var fullOpen = ClrmdDumpSession.Open(fullDumpPath);
            var normalOpen = ClrmdDumpSession.Open(normalDumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, fullOpen.Status);
            Assert.Equal(ClrmdEvidenceStatus.Exact, normalOpen.Status);
            using var fullSession = fullOpen.Value
                ?? throw new InvalidOperationException("The exact full-dump open result carried no session.");
            using var normalSession = normalOpen.Value
                ?? throw new InvalidOperationException("The exact normal-dump open result carried no session.");
            Assert.NotEqual(fullSession.Snapshot, normalSession.Snapshot);

            var objectSearch = fullSession.FindStrongHandleObjectsByTypeName(
                "DumpProbe",
                maximumMatches: 2,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, objectSearch.Status);
            var probe = Assert.Single(objectSearch.Matches);
            var fullRead = fullSession.Memory.Read(probe.Address, ProbeReadLength);
            Assert.Equal(MemoryReadStatus.Exact, fullRead.Status);
            Assert.Equal(ProbeReadLength, fullRead.BytesRead);

            var sparseRead = normalSession.Memory.Read(probe.Address, ProbeReadLength);
            Assert.True(
                sparseRead.Status is MemoryReadStatus.Partial or MemoryReadStatus.Unavailable,
                $"Expected an omitted or truncated heap range, but the normal dump returned {sparseRead.Status}.");
            Assert.Equal(normalSession.Memory.SourceId, sparseRead.SourceId);
            Assert.Equal(probe.Address, sparseRead.Address);
            Assert.Equal(ProbeReadLength, sparseRead.RequestedLength);
            Assert.Equal(sparseRead.BytesRead, sparseRead.Bytes.Length);
            Assert.Equal(ProbeReadLength - sparseRead.BytesRead, sparseRead.MissingByteCount);
            Assert.InRange(sparseRead.BytesRead, 0, ProbeReadLength - 1);
            if (sparseRead.Status == MemoryReadStatus.Unavailable)
            {
                Assert.Empty(sparseRead.Bytes);
            }
        }
        finally
        {
            DeleteIfPresent(normalDumpPath);
            DeleteIfPresent(fullDumpPath);
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
