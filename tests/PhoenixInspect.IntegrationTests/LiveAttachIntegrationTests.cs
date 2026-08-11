using System.Diagnostics;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves live-process attach end to end: the session suspends a real running target, the same expression surface
/// that answers over dumps answers over the suspended process — static fields, promoted roots, root-relative
/// chains, LINQ, and completion catalogs — the session identity states its live provenance, and disposing the
/// session resumes the target.
/// </summary>
public sealed class LiveAttachIntegrationTests
{
    /// <summary>Attaches to the running demo target and inspects it with the full expression surface.</summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Live_attach_answers_the_same_surface_and_resumes_on_dispose()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        using var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null);

        var attached = ClrmdDumpSession.AttachToProcess(target.Pid);
        Assert.Equal(ClrmdEvidenceStatus.Exact, attached.Status);
        using (var session = attached.Value!)
        {
            Assert.True(session.IsLiveAttach);
            Assert.Equal(target.Pid, session.TargetProcessId);
            Assert.StartsWith("live-attach-sha256:", session.Snapshot.MemorySourceId, StringComparison.Ordinal);
            Assert.NotEmpty(session.Modules);

            // Live attach and dump sessions share the same descriptor-derived, immutable edit-state acquisition.
            // This ordinary target is unedited, and repeated consumers must reuse the sole retained observation.
            var targetModule = Assert.Single(
                session.Modules,
                static module => string.Equals(
                    module.Name,
                    "Contoso.OrderService.dll",
                    StringComparison.OrdinalIgnoreCase));
            var editState = session.ReadModuleEditState(targetModule);
            Assert.Equal(ClrmdEvidenceStatus.Exact, editState.Status);
            Assert.Equal(false, editState.Value!.HasAppliedEdits);
            Assert.Same(editState, session.ReadModuleEditState(targetModule));
            Assert.Same(
                editState.Value.GenerationCounterMemory,
                session.ReadModuleEditState(targetModule).Value!.GenerationCounterMemory);

            // The static-field pipeline binds from live metadata exactly as from a dump.
            var counter = ExpressionEvaluationService.EvaluateStaticField(
                session,
                "Contoso.OrderService.Diagnostics.ServiceState.ProcessedOrderCount",
                contextSelector: null,
                portablePdbCandidates: []);
            Assert.Equal(EvaluationSeverity.Exact, counter.Severity);

            // A static object promotes to a root and the root-relative pipeline reads the live heap.
            var promoted = ExpressionEvaluationService.EvaluateStaticField(
                session,
                "Contoso.OrderService.Diagnostics.ServiceState.Dispatcher",
                contextSelector: null,
                portablePdbCandidates: []);
            Assert.NotNull(promoted.PromotableRoot);

            var context = new WatchEvaluationContext { Root = promoted.PromotableRoot };
            var chain = ExpressionEvaluationService.EvaluateWatch(
                session, "root.CurrentBatch.BatchId", context);
            Assert.Equal(EvaluationSeverity.Exact, chain.Severity);

            var query = ExpressionEvaluationService.EvaluateWatch(
                session,
                "(from ms in root.RecentDispatchDurationsMs where ms > 0 orderby ms descending select ms).First()",
                context);
            Assert.Equal(EvaluationSeverity.Exact, query.Severity);

            // Because the target is suspended, live reads replay within the session.
            var replay = ExpressionEvaluationService.EvaluateWatch(
                session, "root.CurrentBatch.BatchId", context);
            Assert.Equal(chain.Value, replay.Value);
            Assert.Equal(chain.Sha256, replay.Sha256);

            // Completion catalogs realize from the live runtime type catalog and metadata.
            var catalog = ExpressionCompletionService.BuildCatalog(session, promoted.PromotableRoot, "root");
            Assert.Contains(catalog.RootMembers, static item => item.Text == "RecentDispatchDurationsMs");
            Assert.Contains("Contoso.OrderService.Diagnostics.ServiceState", catalog.TypeFullNames);

            // The live snapshot projection states the suspension and the session-identity semantics.
            var projection = DumpInspectionService.LoadLiveSnapshot(session);
            Assert.Contains(
                projection.Properties,
                static row => row.Group == "Live session" && row.Name == "Process");
        }

        // Disposing detached and resumed the target: it must still be alive after the session ends.
        using var resumed = Process.GetProcessById(target.Pid);
        Assert.False(resumed.HasExited);
    }
}
