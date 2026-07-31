using System.Collections.Immutable;
using System.Runtime.InteropServices;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpDebugging;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

internal static class PreviewDemoPaths
{
    internal static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    internal static string SessionScript =>
        Path.Combine(RepositoryRoot, "eng", "demo-session.pi");

    internal static string ResolveExecutable()
    {
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Contoso.OrderService.exe"
            : "Contoso.OrderService";
        return Path.Combine(
            RepositoryRoot,
            "samples",
            "Contoso.OrderService",
            "bin",
            "Release",
            targetFramework,
            executableName);
    }
}

/// <summary>
/// Proves the published preview demo still answers what it claims to answer, against a real dump of the demo target.
/// </summary>
/// <remarks>
/// <para>
/// The demo is the first thing a reader runs, so it is the first thing that must not rot. This test captures the same
/// dump the demo captures and asserts the exact rendered answer of every expression the demo session evaluates,
/// including the one that deliberately has no answer.
/// </para>
/// <para>
/// It also asserts that the expressions it checks are exactly the expressions <c>eng/demo-session.pi</c> submits.
/// Editing the demo script without updating the expected answers fails here rather than silently shipping a demo
/// nobody verified.
/// </para>
/// </remarks>
public sealed class PreviewDemoIntegrationTests
{
    private const string RootExpression = "Contoso.OrderService.Diagnostics.ServiceState.Dispatcher";
    private const string RootIdentifier = "root";
    private const string StalledFrameMethod =
        "Contoso.OrderService.Dispatching.DispatchLoop.AwaitCarrierAssignment";
    private const string ContextFrameMethod = "Contoso.OrderService.Program.Main";

    /// <summary>The exact rendered answer the demo must produce for each static-field expression.</summary>
    private static readonly ImmutableArray<(string Expression, EvaluationSeverity Severity, string Value)>
        StaticFieldExpectations =
        [
            ("Contoso.OrderService.Diagnostics.ServiceState.BuildLabel",
                EvaluationSeverity.Exact, "\"2026.07.30-preview\""),
            ("Contoso.OrderService.Diagnostics.ServiceState.ProcessedOrderCount",
                EvaluationSeverity.Exact, "84213"),
            ("Contoso.OrderService.Diagnostics.ServiceState.LastFailureCode",
                EvaluationSeverity.Exact, "5031"),
            ("Contoso.OrderService.Diagnostics.ServiceState.OperatorNote",
                EvaluationSeverity.Exact, "null"),
        ];

    /// <summary>
    /// The exact rendered answer the demo must produce for each contextual static name, given the frame the process
    /// stalled under and a Portable PDB whose identity matches the module.
    /// </summary>
    /// <remarks>
    /// These are the same fields as the fully qualified expressions above, written the way the source writes them.
    /// Proving they agree is the point: name context adds reach, not a second set of answers.
    /// </remarks>
    private static readonly ImmutableArray<(string Expression, EvaluationSeverity Severity, string Value)>
        ContextualExpectations =
        [
            ("ServiceState.BuildLabel", EvaluationSeverity.Exact, "\"2026.07.30-preview\""),
            ("ServiceState.LastFailureCode", EvaluationSeverity.Exact, "5031"),
        ];

    /// <summary>The exact rendered answer the demo must produce for each root-relative expression.</summary>
    private static readonly ImmutableArray<(string Expression, EvaluationSeverity Severity, string Value)>
        RootRelativeExpectations =
        [
            ("root.Region", EvaluationSeverity.Exact, "\"eu-west-1\""),
            ("root.QueueDepth", EvaluationSeverity.Exact, "17"),
            ("root.CurrentBatch.BatchId", EvaluationSeverity.Exact, "\"batch-2026-07-30-0042\""),
            ("root.CurrentBatch.PendingCount", EvaluationSeverity.Exact, "96"),
            ("root.CurrentBatch.DestinationHub", EvaluationSeverity.Exact, "\"AMS-3\""),

            // Deeper chains prove there is no hop limit: three and four hops answer from the same evidence rules.
            ("root.CurrentBatch.Route.HubCode", EvaluationSeverity.Exact, "\"AMS-3-SOUTH-DOCK-07\""),
            ("root.CurrentBatch.Route.Corridor.Name", EvaluationSeverity.Exact, "\"NL-BE overnight corridor\""),
            ("root.LastFailure?.Code", EvaluationSeverity.Exact, "\"carrier-handoff-timeout\""),
            ("root.LastFailure?.Detail", EvaluationSeverity.Exact,
                "\"No carrier accepted batch-2026-07-30-0042 within the 30s hand-off window.\""),
            ("root.AssignedCarrier?.Name", EvaluationSeverity.Exact, "null"),
            ("root.AssignedCarrier?.Name ?? \"no carrier ever accepted the batch\"",
                EvaluationSeverity.Exact, "\"no carrier ever accepted the batch\""),

            // A conditional hop that meets an exact null short-circuits mid-chain, however deep the rest of the
            // chain would have gone.
            ("root.CurrentBatch.Escalation?.Review?.Owner ?? \"the batch was never escalated\"",
                EvaluationSeverity.Exact, "\"the batch was never escalated\""),

            // The demo shows a name the type does not declare. A stop with a stable diagnostic code is the correct
            // answer; a fabricated zero, empty string, or null would not be.
            ("root.RetryBudgetRemaining", EvaluationSeverity.Stopped, "No value was produced."),

            // Composed expressions: each chain is still evaluated by the frozen pipeline; arithmetic, comparison,
            // and slicing then fold over the exact values it produced.
            ("root.QueueDepth * 2 + 1", EvaluationSeverity.Exact, "35"),
            ("root.CurrentBatch.PendingCount > 50", EvaluationSeverity.Exact, "true"),
            ("root.CurrentBatch.BatchId[6..^5]", EvaluationSeverity.Exact, "\"2026-07-30\""),
        ];

    /// <summary>The exact rendered answer the demo must produce for each constant expression.</summary>
    private static readonly ImmutableArray<(string Expression, EvaluationSeverity Severity, string Value)>
        ConstantExpectations =
        [
            ("System.DayOfWeek.Monday", EvaluationSeverity.Exact, "DayOfWeek.Monday (1)"),
            ("Contoso.OrderService.Diagnostics.ServiceState.HandoffWindowSeconds", EvaluationSeverity.Exact, "30"),
            ("(86400 / 24) / 60", EvaluationSeverity.Exact, "60"),
            ("(\"carrier\" + \"-\" + \"handoff\").ToUpperInvariant()",
                EvaluationSeverity.Exact, "\"CARRIER-HANDOFF\""),
            ("\"batch-2026-07-30-0042\".Substring(6, 4)", EvaluationSeverity.Exact, "\"2026\""),
            ("\"AMS-3\".Contains('-')", EvaluationSeverity.Exact, "true"),
            ("\"batch-2026-07-30-0042\"[6..^5]", EvaluationSeverity.Exact, "\"2026-07-30\""),
            ("Math.Round(Math.PI, 4)", EvaluationSeverity.Exact, "3.1416"),

            // Stored static values compose into constant folding, including a Nullable with a fallback.
            ("Contoso.OrderService.Diagnostics.ServiceState.ProcessedOrderCount + 1",
                EvaluationSeverity.Exact, "84214"),
            ("Contoso.OrderService.Diagnostics.ServiceState.LastFailureCode ?? 0",
                EvaluationSeverity.Exact, "5031"),
        ];

    /// <summary>
    /// Proves the demo script and the expected answers describe the same session, without opening a dump.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "PreviewDemoV1")]
    public void Demo_script_submits_exactly_the_expressions_this_test_verifies()
    {
        var scriptPath = PreviewDemoPaths.SessionScript;
        Assert.True(File.Exists(scriptPath), $"Expected the demo session script at '{scriptPath}'.");

        var submitted = ImmutableArray.CreateBuilder<string>();
        var adoptedRoots = ImmutableArray.CreateBuilder<string>();
        foreach (var rawLine in File.ReadAllLines(scriptPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("eval ", StringComparison.Ordinal))
            {
                submitted.Add(line["eval ".Length..].Trim());
            }
            else if (line.StartsWith("root ", StringComparison.Ordinal))
            {
                adoptedRoots.Add(line["root ".Length..].Trim());
            }
        }

        var expected = StaticFieldExpectations
            .Concat(ContextualExpectations)
            .Concat(RootRelativeExpectations)
            .Concat(ConstantExpectations)
            .Select(static expectation => expectation.Expression)
            .ToImmutableArray();

        // Compared as joined text so a drift between the script and the expected answers reports which line differs.
        Assert.Equal(string.Join('\n', expected), string.Join('\n', submitted));
        Assert.Equal(RootExpression, Assert.Single(adoptedRoots));
    }

    /// <summary>
    /// Proves a real dump of the demo target answers every demo expression exactly as published, resolves the frame
    /// the process stalled in by name, and reaches the same object through an independent strong-handle search.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "PreviewDemoV1")]
    public void Preview_demo_dump_answers_every_published_expression()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");

        var dumpPath = Path.Combine(Path.GetTempPath(), $"preview-demo-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;

            AssertStaticFieldAnswers(session);
            AssertContextualAnswers(session);
            var root = AssertRootAdoption(session);
            AssertRootRelativeAnswers(session, root);
            AssertConstantAnswers(session);
            AssertStrongHandleSearchReachesTheSameObject(session);
            AssertStalledFrameIsNamed(session);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static void AssertStaticFieldAnswers(ClrmdDumpSession session)
    {
        foreach (var (expression, severity, value) in StaticFieldExpectations)
        {
            var report = ExpressionEvaluationService.EvaluateStaticField(session, expression, null, []);
            Assert.Equal(severity, report.Severity);
            Assert.Equal(value, report.Value);
        }
    }

    private static void AssertContextualAnswers(ClrmdDumpSession session)
    {
        var selector = RequireStalledContextFrame(session);
        var pdbPath = Path.ChangeExtension(PreviewDemoPaths.ResolveExecutable(), ".pdb");
        Assert.True(File.Exists(pdbPath), $"Expected the demo target's Portable PDB at '{pdbPath}'.");

        foreach (var (expression, severity, value) in ContextualExpectations)
        {
            var report = ExpressionEvaluationService.EvaluateStaticField(
                session,
                expression,
                selector,
                [pdbPath]);
            Assert.Equal(severity, report.Severity);
            Assert.Equal(value, report.Value);

            // Without the frame's imports the same name has nothing to bind against, which is what makes the
            // contextual answer a consequence of the evidence rather than of the spelling.
            var withoutContext = ExpressionEvaluationService.EvaluateStaticField(session, expression, null, []);
            Assert.NotEqual(EvaluationSeverity.Exact, withoutContext.Severity);
        }
    }

    private static DumpSelectedFrameSelector RequireStalledContextFrame(ClrmdDumpSession session)
    {
        var projection = DumpInspectionService.ProbeCallStacks(session, threadOrdinalsToProbe: 16);
        foreach (var thread in projection.Threads)
        {
            foreach (var frame in DumpInspectionService.LoadFrames(session, thread, maximumFrames: 8))
            {
                if (frame.Frame is { } identity &&
                    session.DescribeFrameMethod(identity).Value is { } method &&
                    method.DisplayName == ContextFrameMethod)
                {
                    return frame.Selector;
                }
            }
        }

        Assert.Fail($"No probed managed frame resolved to '{ContextFrameMethod}'.");
        return null!;
    }

    private static RootSelection AssertRootAdoption(ClrmdDumpSession session)
    {
        var report = ExpressionEvaluationService.EvaluateStaticField(session, RootExpression, null, []);
        Assert.Equal(EvaluationSeverity.Exact, report.Severity);
        Assert.StartsWith(
            "Contoso.OrderService.Fulfillment.ShipmentDispatcher @ 0x",
            report.Value,
            StringComparison.Ordinal);

        // The dispatcher is only reachable as an expression root because a static field's exact object value can be
        // promoted; the demo's whole second half depends on that promotion existing.
        Assert.NotNull(report.PromotableRoot);
        return report.PromotableRoot!;
    }

    private static void AssertRootRelativeAnswers(ClrmdDumpSession session, RootSelection root)
    {
        var policy = DumpExpressionPolicy.Create(
            DumpMethodEvaluationMode.Interpreted,
            instructionLimit: 100_000,
            logicalDepthLimit: 8,
            traversalLimit: CounterfactualMethodRequest.MaximumTraversalUnits);

        foreach (var (expression, severity, value) in RootRelativeExpectations)
        {
            var report = ExpressionEvaluationService.EvaluateRootRelative(
                session,
                expression,
                root,
                RootIdentifier,
                policy,
                DumpExpressionLanguageProfile.MemberChainV2);
            Assert.Equal(severity, report.Severity);
            Assert.Equal(value, report.Value);
        }
    }

    private static void AssertConstantAnswers(ClrmdDumpSession session)
    {
        foreach (var (expression, severity, value) in ConstantExpectations)
        {
            var report = ExpressionEvaluationService.EvaluateStaticField(session, expression, null, []);
            Assert.Equal(severity, report.Severity);
            Assert.Equal(value, report.Value);
        }
    }

    private static void AssertStrongHandleSearchReachesTheSameObject(ClrmdDumpSession session)
    {
        var search = DumpInspectionService.SearchObjects(
            session,
            "Contoso.OrderService.Fulfillment.ShipmentDispatcher",
            maximumMatches: 32,
            maximumHandlesScanned: ClrmdDumpSession.MaximumHandleScanCount);

        Assert.True(search.IsExhaustive);
        var match = Assert.Single(search.Rows);
        Assert.Equal("Contoso.OrderService.Fulfillment.ShipmentDispatcher", match.TypeName);
        Assert.Equal("Contoso.OrderService.dll", match.ModuleName);
    }

    private static void AssertStalledFrameIsNamed(ClrmdDumpSession session)
    {
        // Thread ordinals are not stable between runs, so the demo target's stalled frame is identified by name
        // rather than by position — which is exactly what naming frames from snapshot metadata makes possible.
        var projection = DumpInspectionService.ProbeCallStacks(session, threadOrdinalsToProbe: 16);
        var named = new List<string>();
        foreach (var thread in projection.Threads)
        {
            foreach (var frame in DumpInspectionService.LoadFrames(session, thread, maximumFrames: 8))
            {
                if (frame.Frame is { } identity)
                {
                    var method = session.DescribeFrameMethod(identity);
                    if (method.Value is { } resolved)
                    {
                        named.Add(resolved.DisplayName);
                    }
                }
            }
        }

        Assert.Contains(StalledFrameMethod, named);
        Assert.Contains("Contoso.OrderService.Dispatching.CarrierGateway.PollForAssignment", named);
    }
}
