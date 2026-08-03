using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Freezes the watch entry point every host's Watch window renders by: the lexical routing rule between the two
/// implemented expression paths, and the end-to-end behavior against a real dump — a root-mentioning watch answers
/// through the adopted root, everything else answers through the static-field path.
/// </summary>
public sealed class WatchEvaluationIntegrationTests
{
    /// <summary>Proves the standalone-token rule, including the documented member-access and literal cases.</summary>
    /// <param name="expression">The raw watch expression.</param>
    /// <param name="identifier">The root identifier.</param>
    /// <param name="expected">Whether the identifier is referenced as a standalone token.</param>
    [Theory]
    [InlineData("root", "root", true)]
    [InlineData("root.Name", "root", true)]
    [InlineData("(root)", "root", true)]
    [InlineData("1 + root.Depth", "root", true)]
    [InlineData("TimeSpan.FromMilliseconds(root.A.Max())", "root", true)]
    [InlineData("$\"{root.Depth} queued\"", "root", true)]
    [InlineData("myroot.Name", "root", false)]
    [InlineData("root2.Name", "root", false)]
    [InlineData("foo.root.Name", "root", false)]
    [InlineData("@root.Name", "root", false)]
    [InlineData("Some.Type.Field", "root", false)]
    [InlineData("", "root", false)]
    [InlineData("root", "", false)]
    public void Root_identifier_reference_is_a_standalone_token_scan(
        string expression,
        string identifier,
        bool expected) =>
        Assert.Equal(expected, ExpressionEvaluationService.ReferencesIdentifier(expression, identifier));

    /// <summary>
    /// Proves watch routing against a real dump: a promoted static-field object becomes the root, a
    /// root-mentioning watch answers through it, a constant watch folds, a static watch binds, and the same
    /// root-mentioning watch without an adopted root is an explained non-answer rather than a guess.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Watches_route_by_context_and_answer_from_evidence()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"watch-routing-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;

            var promoted = ExpressionEvaluationService.EvaluateStaticField(
                session,
                "Contoso.OrderService.Diagnostics.ServiceState.Dispatcher",
                contextSelector: null,
                portablePdbCandidates: []);
            Assert.NotNull(promoted.PromotableRoot);

            var withRoot = new WatchEvaluationContext { Root = promoted.PromotableRoot };
            var rootWatch = ExpressionEvaluationService.EvaluateWatch(
                session, "root.RecentDispatchDurationsMs.Max()", withRoot);
            Assert.Equal(EvaluationSeverity.Exact, rootWatch.Severity);

            var mixedWatch = ExpressionEvaluationService.EvaluateWatch(
                session, "TimeSpan.FromMilliseconds(root.RecentDispatchDurationsMs.Max()).TotalSeconds", withRoot);
            Assert.Equal(EvaluationSeverity.Exact, mixedWatch.Severity);

            var constantWatch = ExpressionEvaluationService.EvaluateWatch(session, "(2 + 3) * 4", withRoot);
            Assert.Equal(EvaluationSeverity.Exact, constantWatch.Severity);
            Assert.Equal("20", constantWatch.Value);

            // Compound values carry structured children, so a host can expand them like Visual Studio.
            var arrayWatch = ExpressionEvaluationService.EvaluateWatch(
                session, "root.RecentDispatchDurationsMs.ToArray()", withRoot);
            Assert.Equal(EvaluationSeverity.Exact, arrayWatch.Severity);
            Assert.True(arrayWatch.Children.Length >= 1);
            Assert.Equal("[0]", arrayWatch.Children[0].Name);

            var tupleWatch = ExpressionEvaluationService.EvaluateWatch(
                session, "(peak: root.RecentDispatchDurationsMs.Max(), unit: \"ms\")", withRoot);
            Assert.Equal(EvaluationSeverity.Exact, tupleWatch.Severity);
            Assert.Equal(["peak", "unit"], tupleWatch.Children.Select(static child => child.Name).ToArray());
            Assert.Equal("\"ms\"", tupleWatch.Children[1].Value);

            var staticWatch = ExpressionEvaluationService.EvaluateWatch(
                session, "Contoso.OrderService.Diagnostics.ServiceState.Dispatcher", withRoot);
            Assert.Equal("Static field expression", staticWatch.Path);

            // Without an adopted root the same expression is an explained non-answer, never a fabricated one.
            var orphanWatch = ExpressionEvaluationService.EvaluateWatch(
                session, "root.RecentDispatchDurationsMs.Max()", new WatchEvaluationContext());
            Assert.NotEqual(EvaluationSeverity.Exact, orphanWatch.Severity);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }
}
