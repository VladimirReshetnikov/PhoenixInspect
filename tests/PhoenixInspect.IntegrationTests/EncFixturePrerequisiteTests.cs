using Microsoft.Diagnostics.Runtime;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the edited-process fixture prerequisites of the Edit-and-Continue plan work end to end before its entry
/// gate: the pinned compiler produces a real <c>EmitDifference</c> delta, the hidden target applies it through the
/// runtime's own <c>MetadataUpdater.ApplyUpdate</c> path under the modifiable-assemblies gate, and a full dump of
/// the edited process captures with the baseline module loaded.
/// </summary>
/// <remarks>
/// This is enabling infrastructure, not the E1 truth gate: no probe question from the plan is answered here. The
/// target prints READY only after it has verified the pre-edit sentinel and observed the post-edit sentinel from
/// the applied delta, so a successful start is itself the proof that the edit was genuinely applied and executed.
/// </remarks>
public sealed class EncFixturePrerequisiteTests
{
    /// <summary>
    /// Generates the payload with the pinned compiler, runs the fixture to a verified edited pause, captures one
    /// full dump, and confirms the dump holds the edited process's baseline module.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Edited_process_fixture_applies_a_real_delta_and_captures_a_full_dump()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-payload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-smoke-{Guid.NewGuid():N}.dmp");
        try
        {
            EncDeltaCompiler.WriteSmokePayload(payloadDirectory);
            var executable = W8ShapeTargetPaths.RequireArtifact(
                W8ShapeTargetPaths.ResolveExecutable("PhoenixInspect.EncTestTarget"));
            using (var target = TestTargetRunner.StartAndWaitReady(
                executable,
                ["--truth-gate", "enc-smoke", "--payload", payloadDirectory],
                isolatedDirectory: null,
                additionalEnvironment: new Dictionary<string, string>
                {
                    ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "Debug",
                }))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            using var dataTarget = DataTarget.LoadDump(
                dumpPath,
                new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
            var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            try
            {
                Assert.Contains(
                    runtime.EnumerateModules(),
                    module => module.Name?.EndsWith(
                        "PhoenixInspect.EncFixtureBaseline.dll",
                        StringComparison.OrdinalIgnoreCase) == true);
            }
            finally
            {
                runtime.Dispose();
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }

            if (Directory.Exists(payloadDirectory))
            {
                Directory.Delete(payloadDirectory, recursive: true);
            }
        }
    }
}
