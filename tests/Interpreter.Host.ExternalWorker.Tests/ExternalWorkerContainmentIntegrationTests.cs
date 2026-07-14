using System.Diagnostics;
using System.Text.Json;
using Interpreter.Host.ExternalWorker;
using Xunit;

namespace Interpreter.Host.ExternalWorker.Tests;

/// <summary>Exercises the installed one-shot worker across the real Windows containment boundary.</summary>
public sealed class ExternalWorkerContainmentIntegrationTests
{
    private const string RunnerProcessName = "Interpreter.Host.ExternalWorker.Runner";
    private const string SecretCanary = "WorkerSecretCanary_91fdb988";

    /// <summary>
    /// Requires a malformed artifact to reach managed worker code inside an AppContainer, return one normalized
    /// response with complete containment attestation, exit, and leave no request directory or worker process behind.
    /// </summary>
    [Fact]
    public void MalformedArtifactRunsInsideCompleteOneShotContainmentAndLeavesNoWorker()
    {
        Assert.True(OperatingSystem.IsWindows());
        Assert.Equal(
            System.Runtime.InteropServices.Architecture.X64,
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);

        var runnerPath = Path.Combine(
            AppContext.BaseDirectory,
            "external-worker-runner",
            "Interpreter.Host.ExternalWorker.Runner.exe");
        Assert.True(File.Exists(runnerPath), $"Expected the built external-worker runner at '{runnerPath}'.");

        var existingWorkerIds = SnapshotWorkerProcessIds();
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"interpreter-worker-test-{SecretCanary}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var artifactPath = Path.Combine(directory, $"{SecretCanary}.dmp");
        File.WriteAllBytes(
            artifactPath,
            [
                (byte)'M', (byte)'D', (byte)'M', (byte)'P',
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
            ]);

        try
        {
            var broker = new WindowsExternalWorkerBroker(runnerPath);
            var request = new ExternalDumpQueryRequest(
                $"Modeled.{SecretCanary}",
                "root",
                $"root.{SecretCanary}");
            var result = broker.Evaluate(
                artifactPath,
                request);

            Assert.Equal("WORKER_ARTIFACT_REJECTED", result.Response.Code);
            Assert.Equal(ExternalWorkerOutcome.ArtifactRejected, result.Response.Outcome);
            Assert.Equal(
                "The inherited dump artifact is unavailable, invalid, unsupported, or exceeds a bound.",
                result.Response.Message);
            Assert.Equal(ExternalWorkerOperation.DumpQuery, result.Telemetry.Operation);
            Assert.Equal(ExternalWorkerOutcome.ArtifactRejected, result.Telemetry.Outcome);
            Assert.Equal(ExternalWorkerResourceBucket.WithinLimits, result.Telemetry.ResourceBucket);
            Assert.Equal("windows-appcontainer-job-v1", result.Telemetry.ContainmentProfile);
            Assert.Equal(1_000, result.Response.AppliedBounds.MaximumNetworkProbeMilliseconds);

            var telemetryJson = JsonSerializer.Serialize(result.Telemetry);
            Assert.DoesNotContain(SecretCanary, telemetryJson, StringComparison.Ordinal);
            Assert.DoesNotContain(artifactPath, telemetryJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(request.RootTypeName, telemetryJson, StringComparison.Ordinal);
            Assert.DoesNotContain(request.Expression, telemetryJson, StringComparison.Ordinal);

            AssertCompleteContainment(result.Response.Attestation);
            Assert.True(
                existingWorkerIds.SetEquals(SnapshotWorkerProcessIds()),
                "The one-shot broker left a worker process behind.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertCompleteContainment(ExternalWorkerContainmentAttestation attestation)
    {
        Assert.True(attestation.AppContainerToken);
        Assert.True(attestation.JobMembership);
        Assert.Equal(1u, attestation.JobActiveProcessLimit);
        Assert.Equal(1_610_612_736, attestation.JobProcessMemoryBytes);
        Assert.Equal(1_610_612_736, attestation.JobMemoryBytes);
        Assert.Equal(600_000_000, attestation.JobProcessUserTimeTicks);
        Assert.True(attestation.ZeroCapabilityLaunch);
        Assert.True(attestation.ExactHandleListLaunch);
        Assert.True(attestation.AtomicJobLaunch);
        Assert.True(attestation.ChildProcessDenied);
        Assert.True(attestation.DiagnosticsDisabled);
        Assert.Equal(ExternalWorkerScratchStatus.Established, attestation.ScratchStatus);
        Assert.True(attestation.EnvironmentCleared);
        Assert.True(attestation.NetworkDenied);
        Assert.True(attestation.HeadlessErrorPolicy);
        Assert.True(attestation.ArtifactReadOnly);
        Assert.True(attestation.TrustedDacPinned);
    }

    private static HashSet<int> SnapshotWorkerProcessIds()
    {
        using var current = Process.GetCurrentProcess();
        return Process.GetProcessesByName(RunnerProcessName)
            .Where(process => process.Id != current.Id)
            .Select(static process =>
            {
                using (process)
                {
                    return process.Id;
                }
            })
            .ToHashSet();
    }
}
