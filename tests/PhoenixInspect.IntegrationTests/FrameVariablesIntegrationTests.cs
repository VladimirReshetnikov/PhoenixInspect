using System.Collections.Immutable;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves frame-variable decoding against a real dump of the demo target: parameters pair signature types with
/// Param-row names, local slots decode from the method body's local signature, slot names attach only through the
/// identity-validated Portable PDB, and an instance frame lists its receiver as <c>this</c>.
/// </summary>
public sealed class FrameVariablesIntegrationTests
{
    /// <summary>Decodes parameters and locals for known demo-target frames, with and without the PDB.</summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "FrameVariablesV1")]
    public void Frame_parameters_and_local_slots_decode_with_pdb_names()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        var pdbPath = Path.ChangeExtension(executable, ".pdb");
        Assert.True(File.Exists(pdbPath), $"Expected the demo target's Portable PDB at '{pdbPath}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"frame-variables-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;

            // Program.Main(string[] args): a static frame with a known parameter and a known named local.
            var main = RequireFrameNamed(session, "Contoso.OrderService.Program.Main");
            var withPdb = session.DescribeFrameVariables(main.Frame!, [pdbPath]);
            Assert.Equal(ClrmdEvidenceStatus.Exact, withPdb.Status);
            var variables = withPdb.Value!;

            var args = Assert.Single(variables.Parameters);
            Assert.Equal("args", args.Name);
            Assert.Equal("string[]", args.TypeDisplay);
            Assert.False(args.IsThis);

            Assert.NotEmpty(variables.LocalSlots);
            Assert.Null(variables.LocalSlotsNote);
            Assert.Null(variables.LocalNamesNote);
            Assert.Equal(
                variables.LocalSlots.Length,
                variables.LocalSlots.Select(static slot => slot.Slot).Distinct().Count());
            var dispatcher = Assert.Single(variables.LocalSlots, static slot => slot.Name == "dispatcher");
            Assert.Equal("ShipmentDispatcher", dispatcher.TypeDisplay);
            Assert.True(dispatcher.IsInScopeAtCurrentOffset);
            Assert.False(dispatcher.IsDebuggerHidden);

            // Without a candidate, the same slots keep their types and honestly say why they have no names.
            var withoutPdb = session.DescribeFrameVariables(main.Frame!, ImmutableArray<string>.Empty);
            Assert.Equal(ClrmdEvidenceStatus.Exact, withoutPdb.Status);
            Assert.Equal(variables.LocalSlots.Length, withoutPdb.Value!.LocalSlots.Length);
            Assert.All(withoutPdb.Value.LocalSlots, static slot => Assert.Null(slot.Name));
            Assert.NotNull(withoutPdb.Value.LocalNamesNote);

            // The lambda body compiled into a display class is an instance method: its receiver lists as 'this'.
            var lambda = RequireFrameNamed(session, "<StartPollingWorkers>b__0");
            var lambdaVariables = session.DescribeFrameVariables(lambda.Frame!, [pdbPath]);
            Assert.Equal(ClrmdEvidenceStatus.Exact, lambdaVariables.Status);
            var receiver = lambdaVariables.Value!.Parameters[0];
            Assert.True(receiver.IsThis);
            Assert.Equal("this", receiver.Name);

            // The Locals-pane projection carries the same facts as display rows plus the honest value statement.
            var projection = DumpInspectionService.DescribeFrameVariables(
                session,
                main,
                [pdbPath]);
            Assert.Contains(projection.Rows, static row => row is { Kind: "Parameter", Name: "args" });
            Assert.Contains(projection.Rows, static row => row is { Kind: "Local", Name: "dispatcher" });
            Assert.Contains("Values are deliberately not shown", projection.Summary, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static CallStackFrameNode RequireFrameNamed(ClrmdDumpSession session, string displayNameFragment)
    {
        var seen = new List<string>();
        foreach (var thread in DumpInspectionService.ProbeCallStacks(session, threadOrdinalsToProbe: 16).Threads)
        {
            foreach (var frame in DumpInspectionService.LoadFrames(session, thread, maximumFrames: 16))
            {
                if (frame.Frame is not { } identity)
                {
                    continue;
                }

                var named = session.DescribeFrameMethod(identity).Value;
                if (named is null)
                {
                    continue;
                }

                seen.Add(named.DisplayName);
                if (named.DisplayName.Contains(displayNameFragment, StringComparison.Ordinal))
                {
                    return frame;
                }
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"No probed frame matched '{displayNameFragment}'. Seen:\n{string.Join('\n', seen)}");
    }
}
