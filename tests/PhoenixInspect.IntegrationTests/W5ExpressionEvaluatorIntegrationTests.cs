using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Proves W5.3's mode-preserving routing union against one real generated dump.</summary>
public sealed class W5ExpressionEvaluatorIntegrationTests
{
    private const string ProbeTypeName = "DumpProbe";
    private const int ExpectedMarker = 0x13579BDF;
    private const int ExpectedSummary = 0x26AF37BD;

    /// <summary>
    /// Compares facade results with the unchanged W2 and existing W4 paths, preserves degraded unknowns, and proves
    /// classification, preparation-budget, execution-budget, and cancellation routing without result flattening.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W5ExpressionFacadeV1")]
    public void Evaluator_preserves_complete_w2_w4_and_failure_cases()
    {
        var executablePath = TestTargetPaths.ResolveExecutable();
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w5-evaluator-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executablePath))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            using var session = OpenExact(dumpPath);
            var rootSearch = session.FindStrongHandleObjectsByTypeName(
                ProbeTypeName,
                maximumMatches: 2,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, rootSearch.Status);
            Assert.Single(rootSearch.Matches);
            var root = DumpQueryRootBinding.FromSearchResult("root", rootSearch);

            AssertUnchangedW2(session, root);
            AssertExactW4Modes(session, root);
            AssertDegradedW4Modes(session, root);
            AssertFailureAndOperationalRouting(session, root);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static void AssertUnchangedW2(ClrmdDumpSession session, DumpQueryRootBinding root)
    {
        var directPreparation = DumpQueryEngine.Prepare(session, "root.Marker", root);
        Assert.True(directPreparation.IsSuccess);
        var direct = DumpQueryEngine.Evaluate(session, directPreparation.Plan!);
        var outcome = DumpExpressionEvaluator.Evaluate(
            session,
            "root.Marker",
            root,
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));

        AssertOnly(outcome, DumpExpressionEvaluationOutcomeKind.DerivedQuery);
        var routed = outcome.DerivedQueryResult!;
        Assert.Equal(EvaluationSemanticMode.DerivedQuery, routed.SemanticMode);
        Assert.Equal(DumpQueryValueKind.Int32, routed.Value!.Kind);
        Assert.Equal(ExpectedMarker, routed.Value.Int32Value);
        Assert.Equal(
            EvaluationResultReplay.SerializeCanonical(direct, static value => value.ToCanonicalReplayProjection()),
            EvaluationResultReplay.SerializeCanonical(routed, static value => value.ToCanonicalReplayProjection()));
        Assert.Equal(
            EvaluationResultReplay.ComputeSha256(direct, static value => value.ToCanonicalReplayProjection()),
            EvaluationResultReplay.ComputeSha256(routed, static value => value.ToCanonicalReplayProjection()));
    }

    private static void AssertExactW4Modes(ClrmdDumpSession session, DumpQueryRootBinding root)
    {
        foreach (var mode in new[]
                 {
                     DumpMethodEvaluationMode.Interpreted,
                     DumpMethodEvaluationMode.Modeled,
                 })
        {
            var policy = CreatePolicy(mode);
            var classification = DumpExpressionClassifier.Classify("root.GetMarkerSummary()", root, policy);
            var acquisition = DumpMethodAcquisitionFacade.Acquire(session, classification.Request!);
            Assert.True(acquisition.IsSuccess, acquisition.Failure?.Code);
            var directRunner = new CounterfactualMethodRunner<CounterfactualDumpMemory>();
            var directPreparation = directRunner.Prepare(acquisition.Binding!.Candidate);
            Assert.True(directPreparation.IsSuccess, directPreparation.Failure?.Diagnostics[0].Code);
            var direct = directRunner.Run(directPreparation.Plan!);

            var outcome = DumpExpressionEvaluator.Evaluate(
                session,
                "root.GetMarkerSummary()",
                root,
                policy);
            AssertOnly(outcome, DumpExpressionEvaluationOutcomeKind.CounterfactualExecution);
            var routed = outcome.CounterfactualExecutionResult!;
            Assert.Equal(EvaluationSemanticMode.CounterfactualExecution, routed.SemanticMode);
            Assert.Equal(EvaluationCompletionStatus.Completed, routed.Completion);
            Assert.Equal(EvaluationCompleteness.Complete, routed.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, routed.Evidence);
            Assert.Equal(CounterfactualExecutionValueKind.ExactReturn, routed.Value!.Kind);
            Assert.Equal(ExpectedSummary, routed.Value.ExactInt32);
            Assert.Equal(mode == DumpMethodEvaluationMode.Interpreted ? 0 : 1, routed.Context.ModelInvocationCount);
            Assert.Equal(direct.Sha256, routed.Sha256);
            Assert.True(direct.CanonicalBytes.AsSpan().SequenceEqual(routed.CanonicalBytes.AsSpan()));
        }
    }

    private static void AssertDegradedW4Modes(ClrmdDumpSession session, DumpQueryRootBinding root)
    {
        foreach (var status in new[] { ClrmdEvidenceStatus.Partial, ClrmdEvidenceStatus.Unavailable })
        {
            foreach (var mode in new[]
                     {
                         DumpMethodEvaluationMode.Interpreted,
                         DumpMethodEvaluationMode.Modeled,
                     })
            {
                var classification = DumpExpressionClassifier.Classify(
                    "root.GetMarkerSummary()",
                    root,
                    CreatePolicy(mode));
                var outcome = DumpExpressionEvaluator.EvaluateMethod(
                    new DegradedMarkerEvidenceSource(session, status),
                    classification.Request!);

                AssertOnly(outcome, DumpExpressionEvaluationOutcomeKind.CounterfactualExecution);
                var result = outcome.CounterfactualExecutionResult!;
                Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
                Assert.Equal(EvaluationCompleteness.Partial, result.Completeness);
                Assert.Equal(
                    status == ClrmdEvidenceStatus.Partial
                        ? EvaluationEvidenceStatus.Partial
                        : EvaluationEvidenceStatus.Unavailable,
                    result.Evidence);
                Assert.Equal(CounterfactualExecutionValueKind.UnknownReturn, result.Value!.Kind);
                Assert.Null(result.Value.ExactInt32);
                Assert.NotNull(result.Value.Lineage);
                Assert.Equal(mode == DumpMethodEvaluationMode.Interpreted ? 0 : 1, result.Context.ModelInvocationCount);
            }
        }
    }

    private static void AssertFailureAndOperationalRouting(
        ClrmdDumpSession session,
        DumpQueryRootBinding root)
    {
        var missingField = DumpExpressionEvaluator.Evaluate(
            session,
            "root.FieldThatDoesNotExist",
            root,
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));
        AssertOnly(missingField, DumpExpressionEvaluationOutcomeKind.DerivedQuery);
        Assert.Equal(EvaluationSemanticMode.DerivedQuery, missingField.DerivedQueryResult!.SemanticMode);
        Assert.Equal(EvaluationCompletionStatus.Blocked, missingField.DerivedQueryResult.Completion);
        Assert.Equal(EvaluationCompleteness.None, missingField.DerivedQueryResult.Completeness);

        var unsupported = DumpExpressionEvaluator.Evaluate(
            session,
            "root.GetMarkerSummary() + 1",
            root,
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));
        AssertOnly(unsupported, DumpExpressionEvaluationOutcomeKind.ClassificationFailure);
        Assert.Equal(DumpExpressionClassificationStatus.Unsupported, unsupported.ClassificationFailure!.Status);
        Assert.Equal("QUERY_SYNTAX_UNSUPPORTED", unsupported.ClassificationFailure.DiagnosticCode);

        var selected = root.Root!;
        var absentModuleIdentity = selected.Module.Identity with
        {
            ModuleAddress = selected.Module.Identity.ModuleAddress + 1,
        };
        var absentModule = new ClrmdModuleInfo(
            absentModuleIdentity,
            selected.Module.Name,
            selected.Module.TargetPathHint,
            selected.Module.AppDomainId,
            selected.Module.MetadataAddress,
            selected.Module.MetadataLength,
            selected.Module.Layout);
        var absentModuleRoot = new ClrmdHeapObjectInfo(
            selected.Snapshot,
            selected.Address,
            selected.TypeName,
            selected.TypeMetadataToken,
            selected.MethodTable,
            selected.RootAddress,
            selected.RootKind,
            absentModule,
            selected.Evidence);
        var acquisitionFailure = DumpExpressionEvaluator.Evaluate(
            session,
            "root.GetMarkerSummary()",
            DumpQueryRootBinding.FromExactObject("root", absentModuleRoot, root.AppliedBounds),
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));
        AssertOnly(acquisitionFailure, DumpExpressionEvaluationOutcomeKind.AcquisitionFailure);
        Assert.Equal(DumpMethodAcquisitionFailureKind.Missing, acquisitionFailure.AcquisitionFailure!.Kind);
        Assert.Equal("W5_MODULE_MISSING", acquisitionFailure.AcquisitionFailure.Code);

        var insufficientDepth = DumpExpressionEvaluator.Evaluate(
            session,
            "root.GetMarkerSummary()",
            root,
            DumpExpressionPolicy.Create(
                DumpMethodEvaluationMode.Interpreted,
                instructionLimit: 100,
                logicalDepthLimit: 1,
                traversalLimit: 10));
        AssertOnly(insufficientDepth, DumpExpressionEvaluationOutcomeKind.CounterfactualPreparationFailure);
        Assert.Equal(
            EvaluationSemanticMode.CounterfactualExecution,
            insufficientDepth.CounterfactualPreparationFailure!.SemanticMode);
        Assert.Equal(
            EvaluationCompletionStatus.BudgetExhausted,
            insufficientDepth.CounterfactualPreparationFailure.Completion);

        var zeroInstructionBudget = DumpExpressionEvaluator.Evaluate(
            session,
            "root.GetMarkerSummary()",
            root,
            DumpExpressionPolicy.Create(
                DumpMethodEvaluationMode.Interpreted,
                instructionLimit: 0,
                logicalDepthLimit: 2,
                traversalLimit: 10));
        AssertOnly(zeroInstructionBudget, DumpExpressionEvaluationOutcomeKind.CounterfactualExecution);
        Assert.Equal(
            EvaluationCompletionStatus.BudgetExhausted,
            zeroInstructionBudget.CounterfactualExecutionResult!.Completion);
        Assert.Equal(EvaluationCompleteness.None, zeroInstructionBudget.CounterfactualExecutionResult.Completeness);
        Assert.Null(zeroInstructionBudget.CounterfactualExecutionResult.Value);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = DumpExpressionEvaluator.Evaluate(
            session,
            "root.GetMarkerSummary()",
            root,
            CreatePolicy(DumpMethodEvaluationMode.Interpreted),
            cancellation.Token);
        AssertOnly(cancelled, DumpExpressionEvaluationOutcomeKind.CounterfactualExecution);
        Assert.Equal(EvaluationCompletionStatus.Cancelled, cancelled.CounterfactualExecutionResult!.Completion);
        Assert.Equal(EvaluationCompleteness.None, cancelled.CounterfactualExecutionResult.Completeness);
        Assert.Null(cancelled.CounterfactualExecutionResult.Value);
    }

    private static void AssertOnly(
        DumpExpressionEvaluationOutcome outcome,
        DumpExpressionEvaluationOutcomeKind expectedKind)
    {
        Assert.Equal(expectedKind, outcome.Kind);
        Assert.Equal(expectedKind == DumpExpressionEvaluationOutcomeKind.DerivedQuery, outcome.DerivedQueryResult is not null);
        Assert.Equal(
            expectedKind == DumpExpressionEvaluationOutcomeKind.CounterfactualExecution,
            outcome.CounterfactualExecutionResult is not null);
        Assert.Equal(
            expectedKind == DumpExpressionEvaluationOutcomeKind.ClassificationFailure,
            outcome.ClassificationFailure is not null);
        Assert.Equal(
            expectedKind == DumpExpressionEvaluationOutcomeKind.AcquisitionFailure,
            outcome.AcquisitionFailure is not null);
        Assert.Equal(
            expectedKind == DumpExpressionEvaluationOutcomeKind.CounterfactualPreparationFailure,
            outcome.CounterfactualPreparationFailure is not null);
    }

    private static DumpExpressionPolicy CreatePolicy(DumpMethodEvaluationMode mode) =>
        DumpExpressionPolicy.Create(
            mode,
            instructionLimit: 100,
            logicalDepthLimit: 2,
            traversalLimit: 10);

    private static ClrmdDumpSession OpenExact(string path)
    {
        var opened = ClrmdDumpSession.Open(path);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        return opened.Value ?? throw new InvalidOperationException("Exact dump open returned no session.");
    }

    private sealed class DegradedMarkerEvidenceSource(
        ClrmdDumpSession session,
        ClrmdEvidenceStatus degradedStatus) : IDumpMethodEvidenceSource
    {
        public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

        public ImmutableArray<ClrmdModuleInfo> Modules => session.Modules;

        public ClrmdEvidenceResult<ModuleContentIdentity> ReadModuleContentIdentity(ClrmdModuleInfo module) =>
            session.ReadModuleContentIdentity(module);

        public ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
            ClrmdModuleInfo module,
            string typeName,
            string methodName) => session.ReadMethodBody(module, typeName, methodName);

        public ClrmdHeapObjectSearchResult FindStrongHandleObjectsByTypeName(
            string typeName,
            int maximumMatches,
            int maximumHandlesScanned) => session.FindStrongHandleObjectsByTypeName(
            typeName,
            maximumMatches,
            maximumHandlesScanned);

        public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
            ClrmdHeapObjectInfo owner,
            string fieldName)
        {
            var exact = session.ReadInt32Field(owner, fieldName);
            if (!string.Equals(fieldName, DumpMethodAcquisitionFacade.MarkerFieldName, StringComparison.Ordinal))
            {
                return exact;
            }

            Assert.Equal(ClrmdEvidenceStatus.Exact, exact.Status);
            var exactObservation = Assert.IsType<ClrmdInt32FieldObservation>(exact.Value);
            var prefixLength = degradedStatus switch
            {
                ClrmdEvidenceStatus.Partial => 2,
                ClrmdEvidenceStatus.Unavailable => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(degradedStatus)),
            };
            var memory = MemoryReadResult.Create(
                exactObservation.Memory.SourceId,
                exactObservation.Memory.Address,
                exactObservation.Memory.RequestedLength,
                exactObservation.Memory.Bytes.AsSpan(0, prefixLength));
            var degraded = new ClrmdInt32FieldObservation(exactObservation.Field, memory, value: null);
            return ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                degradedStatus,
                ClrmdValueIssue.MemoryUnavailable,
                degraded,
                ImmutableArray.Create(memory),
                exact.AppliedBounds);
        }
    }
}
