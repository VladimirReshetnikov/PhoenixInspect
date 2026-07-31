using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpDebugging;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the arbitrary-depth member-chain path against a real dump: deep chains answer exactly, a direct hop
/// through an exact null is a typed stop, a conditional hop short-circuits mid-chain, the frozen V1 profile still
/// rejects deeper chains, and answers replay deterministically.
/// </summary>
public sealed class MemberChainPathIntegrationTests
{
    private const string RootExpression = "Contoso.OrderService.Diagnostics.ServiceState.Dispatcher";
    private const string RootIdentifier = "root";

    /// <summary>Runs every chain-path disposition over one captured demo-target dump.</summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "MemberChainPathV1")]
    public void Deep_chains_answer_short_circuit_and_stay_rejected_under_v1()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"chain-path-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;
            var rootReport = ExpressionEvaluationService.EvaluateStaticField(session, RootExpression, null, []);
            Assert.Equal(EvaluationSeverity.Exact, rootReport.Severity);
            var root = rootReport.PromotableRoot!;
            var policy = DumpExpressionPolicy.Create(
                DumpMethodEvaluationMode.Interpreted,
                instructionLimit: 100_000,
                logicalDepthLimit: 8,
                traversalLimit: CounterfactualMethodRequest.MaximumTraversalUnits);

            EvaluationReport EvaluateV2(string expression) => ExpressionEvaluationService.EvaluateRootRelative(
                session, expression, root, RootIdentifier, policy, DumpExpressionLanguageProfile.MemberChainV2);

            // Three and four direct hops answer exactly, and the four-hop Int32 terminal proves the terminal
            // decoders work behind resolved intermediates.
            Assert.Equal(
                (EvaluationSeverity.Exact, "\"AMS-3-SOUTH-DOCK-07\""),
                Project(EvaluateV2("root.CurrentBatch.Route.HubCode")));
            Assert.Equal(
                (EvaluationSeverity.Exact, "\"NL-BE overnight corridor\""),
                Project(EvaluateV2("root.CurrentBatch.Route.Corridor.Name")));
            Assert.Equal(
                (EvaluationSeverity.Exact, "11"),
                Project(EvaluateV2("root.CurrentBatch.Route.Corridor.SegmentCount")));

            // The same deep answer replays byte-for-byte within the session.
            Assert.Equal(
                EvaluateV2("root.CurrentBatch.Route.Corridor.Name").Sha256,
                EvaluateV2("root.CurrentBatch.Route.Corridor.Name").Sha256);

            // An exact null intermediate is a typed stop through '.', and a mid-chain short-circuit through '?.'.
            var blocked = EvaluateV2("root.CurrentBatch.Escalation.Review.Owner");
            Assert.Equal(EvaluationSeverity.Stopped, blocked.Severity);
            Assert.Contains(
                blocked.Diagnostics,
                static diagnostic => diagnostic.Code == "QUERY_CHAIN_NULL_RECEIVER");
            Assert.Equal(
                (EvaluationSeverity.Exact, "\"the batch was never escalated\""),
                Project(EvaluateV2(
                    "root.CurrentBatch.Escalation?.Review?.Owner ?? \"the batch was never escalated\"")));

            // A name an intermediate type does not declare stays a typed stop, not a fabricated default.
            var undeclared = EvaluateV2("root.CurrentBatch.Route.Wormhole.Name");
            Assert.Equal(EvaluationSeverity.Stopped, undeclared.Severity);

            // The frozen V1 profile still rejects any chain deeper than two hops.
            var underV1 = ExpressionEvaluationService.EvaluateRootRelative(
                session,
                "root.CurrentBatch.Route.HubCode",
                root,
                RootIdentifier,
                policy,
                DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
            Assert.Equal(EvaluationSeverity.Rejected, underV1.Severity);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static (EvaluationSeverity Severity, string Value) Project(EvaluationReport report) =>
        (report.Severity, report.Value);
}
