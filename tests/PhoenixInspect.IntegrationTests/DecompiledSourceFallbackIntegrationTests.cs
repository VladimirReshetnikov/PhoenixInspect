using System.Reflection.Metadata;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the decompiled-source fallback against a real dump, in both trigger cases: a frame whose module has no
/// matching Portable PDB on this machine, and a frame whose PDB matches but whose recorded document paths exist
/// nowhere here. Each is presented as C# decompiled by the ILSpy engine from an on-disk assembly admitted only
/// after its complete metadata content identity reproduced the dump module's — and each is honestly labelled as a
/// reconstruction, never as the original source.
/// </summary>
public sealed class DecompiledSourceFallbackIntegrationTests
{
    private const string CoreLibModuleName = "System.Private.CoreLib.dll";
    private const string DemoModuleName = "Contoso.OrderService.dll";

    /// <summary>Runs both trigger dispositions over one captured demo-target dump.</summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "DecompiledSourceFallbackV1")]
    public void Frames_without_pdb_or_sources_decompile_from_validated_assemblies()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"decompile-fallback-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;

            // A frame in System.Private.CoreLib: the shared framework ships no Portable PDB beside the assembly,
            // so resolution cannot name a verified source — exactly the situation the fallback exists for.
            var coreLibFrame = RequireFrameInModule(session, CoreLibModuleName);
            var decompiled = SourceNavigationService.ResolveFrameSource(session, coreLibFrame, []);
            Assert.True(decompiled.IsResolved);
            Assert.Equal(SourceContentVerification.DecompiledFromValidatedAssembly, decompiled.Verification);
            Assert.True(decompiled.Lines.Length > 10, $"Expected decompiled lines, got {decompiled.Lines.Length}.");
            Assert.Contains("decompiled", decompiled.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(decompiled.Facts, static fact => fact.Name == "Engine");
            Assert.Contains(decompiled.Facts, static fact => fact.Name == "Metadata identity");
            Assert.EndsWith(".dll", decompiled.DocumentPath, StringComparison.OrdinalIgnoreCase);

            // The frame's own method name appears in the decompiled type, so the reconstruction demonstrably
            // contains the code the frame was executing.
            var methodDisplay = session.DescribeFrameMethod(
                session.SelectExpressionFrame(coreLibFrame.Selector).Frame!).Value?.DisplayName;
            Assert.False(string.IsNullOrEmpty(methodDisplay));
            var methodShortName = methodDisplay![(methodDisplay.LastIndexOf('.') + 1)..];
            Assert.Contains(
                decompiled.Lines,
                line => line.Text.Contains(methodShortName, StringComparison.Ordinal));

            // The demo module has a matching PDB whose recorded document paths exist nowhere — the "matching
            // sources can't be found" case. That is a property of the demo target's own build, not of this machine:
            // its project pins ContinuousIntegrationBuild, so the PDB records SourceLink's deterministic '/_/'
            // repository-root map rather than whatever absolute paths the builder happened to have.
            //
            // The precondition is asserted from the artifact rather than assumed, because when it does not hold the
            // frame resolves to real on-disk source and the assertion below fails as a bare Verification mismatch
            // that says nothing about why. Reading it here names the cause instead.
            AssertRecordedDocumentsExistNowhere(Path.ChangeExtension(executable, ".pdb"));

            var demoFrame = RequireFrameInModule(session, DemoModuleName);
            var demoDecompiled = SourceNavigationService.ResolveFrameSource(session, demoFrame, []);
            Assert.Equal(
                SourceContentVerification.DecompiledFromValidatedAssembly,
                demoDecompiled.Verification);
            Assert.Contains(demoDecompiled.Facts, static fact => fact.Name == "Why not source");
            var demoMethod = session.DescribeFrameMethod(
                session.SelectExpressionFrame(demoFrame.Selector).Frame!).Value?.DisplayName;
            Assert.False(string.IsNullOrEmpty(demoMethod));
            var demoShortName = demoMethod![(demoMethod.LastIndexOf('.') + 1)..];
            Assert.Contains(
                demoDecompiled.Lines,
                line => line.Text.Contains(demoShortName, StringComparison.Ordinal));

            // The method-signature heuristic locates the frame's method inside the decompiled type, so the
            // highlighted line is the method the frame was executing.
            Assert.True(
                demoDecompiled.StartLine > 0,
                "Expected the decompiled view to locate the frame's method line.");
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    /// <summary>
    /// Asserts every document the demo target's Portable PDB records is unfindable on this machine, which is the
    /// physical precondition of the "matching PDB, missing sources" fallback trigger.
    /// </summary>
    /// <param name="pdbPath">The Portable PDB beside the demo target's assembly.</param>
    private static void AssertRecordedDocumentsExistNowhere(string pdbPath)
    {
        Assert.True(File.Exists(pdbPath), $"Expected the demo target's Portable PDB at '{pdbPath}'.");
        using var stream = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();
        Assert.NotEmpty(reader.Documents);

        foreach (var handle in reader.Documents)
        {
            var recorded = reader.GetString(reader.GetDocument(handle).Name);
            Assert.False(
                File.Exists(recorded),
                $"The demo target's PDB records '{recorded}', which exists on this machine, so the frame resolves "
                + "to verified source and the decompiled-source fallback never triggers. The demo project pins "
                + "ContinuousIntegrationBuild precisely so its documents stay deterministic '/_/' paths that exist "
                + "nowhere; a build that dropped that property is the cause, not this test.");
        }
    }

    private static CallStackFrameNode RequireFrameInModule(ClrmdDumpSession session, string moduleName)
    {
        var seen = new List<string>();
        var projection = DumpInspectionService.ProbeCallStacks(session, threadOrdinalsToProbe: 16);
        foreach (var thread in projection.Threads)
        {
            // LoadFrames walks the complete bounded stack from frame #0 downward.
            foreach (var frame in DumpInspectionService.LoadFrames(session, thread, maximumFrames: 16))
            {
                if (frame.Frame is not { } identity)
                {
                    seen.Add($"{frame.Header}: no identity");
                    continue;
                }

                var module = session.Modules.FirstOrDefault(
                    candidate => candidate.Identity == identity.RuntimeModule);
                seen.Add($"{frame.Header}: module {module?.Name ?? "<unmatched>"}");
                if (module is not null &&
                    string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return frame;
                }
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"No probed managed frame resolved into '{moduleName}'. Seen:\n{string.Join('\n', seen)}");
    }
}
