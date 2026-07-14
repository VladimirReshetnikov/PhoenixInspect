using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises the versioned W2 product-query corpus independently of the W1 dump-evidence omnibus scenario.
/// </summary>
public sealed class DumpQueryScenarioCorpusIntegrationTests
{
    private const string CorpusVersion = "w2-root-field-v1";
    private const int ExpectedMarker = 0x13579BDF;
    private const int ExpectedPresentCount = 73;
    private const string ExpectedMessage = "dump-memory-evidence:\uD83D\uDE80 exact rooted string";

    private static readonly ImmutableArray<Scenario> Scenarios =
    [
        new(
            $"{CorpusVersion}.exact-int32",
            "root.Marker",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Int32(ExpectedMarker)),
        new(
            $"{CorpusVersion}.exact-string",
            "root.Message",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage)),
        new(
            $"{CorpusVersion}.exact-null",
            "root.OptionalMessage",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Null()),
        new(
            $"{CorpusVersion}.string-coalesce-selected",
            "root.OptionalMessage ?? \"<missing>\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String("<missing>")),
        new(
            $"{CorpusVersion}.string-coalesce-unselected",
            "root.Message ?? \"<unused>\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage)),
        new(
            $"{CorpusVersion}.string-coalesce-unpaired-surrogate-d800",
            "root.Message ?? \"\uD800\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage)),
        new(
            $"{CorpusVersion}.string-coalesce-unpaired-surrogate-d801",
            "root.Message ?? \"\uD801\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage)),
        new(
            $"{CorpusVersion}.nullable-int32-present",
            "root.PresentCount ?? -1",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Int32(ExpectedPresentCount)),
        new(
            $"{CorpusVersion}.nullable-int32-present-direct",
            "root.PresentCount",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Int32(ExpectedPresentCount)),
        new(
            $"{CorpusVersion}.nullable-int32-null",
            "root.OptionalCount ?? -17",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Int32(-17)),
        new(
            $"{CorpusVersion}.nullable-int32-null-direct",
            "root.OptionalCount",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Null()),
        new(
            $"{CorpusVersion}.nullable-int32-null-coalesce-null",
            "root.OptionalCount ?? null",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Null()),
        new(
            $"{CorpusVersion}.missing-member",
            "root.AbsentField",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            ExpectedValue.None(),
            "DUMP_FIELD_UNAVAILABLE"),
        new(
            $"{CorpusVersion}.wrong-case-member",
            "root.marker",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            ExpectedValue.None(),
            "DUMP_FIELD_UNAVAILABLE"),
        new(
            $"{CorpusVersion}.unavailable-root",
            "root.Marker",
            RootBindingSelection.Absent,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            ExpectedValue.None(),
            "QUERY_ROOT_ABSENT"),
        new(
            $"{CorpusVersion}.partial-root",
            "root.Marker",
            RootBindingSelection.Partial,
            EvaluationCompletionStatus.BudgetExhausted,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Partial,
            ExpectedValue.None(),
            "QUERY_ROOT_LIMIT_EXCEEDED"),
        new(
            $"{CorpusVersion}.unsupported-type",
            "root.Enabled",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            "QUERY_FIELD_TYPE_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.incompatible-coalescing",
            "root.Marker ?? 0",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            "QUERY_COALESCE_TYPE_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.null-conditional-rejected",
            "root?.Message",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            "QUERY_SYNTAX_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.invalid-syntax",
            "root.Message()",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            "QUERY_SYNTAX_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.root-mismatch",
            "other.Marker",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            "QUERY_ROOT_MISMATCH"),
        new(
            $"{CorpusVersion}.bounded-partial-string",
            "root.LongMessage",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Partial,
            ExpectedValue.BoundedString(4096),
            "DUMP_LIMIT_EXCEEDED"),
    ];

    /// <summary>
    /// Proves the complete admitted and rejected W2 corpus is byte-identical when repeated against one session and
    /// after the same dump is closed, reopened, and rebound to newly projected root evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W2RootFieldV1")]
    public void Versioned_query_corpus_replays_across_repeated_and_reopened_sessions()
    {
        Assert.True(Scenarios.Length >= 10);
        Assert.Equal(Scenarios.Length, Scenarios.Select(static scenario => scenario.Id).Distinct().Count());
        Assert.True(Scenarios.Select(static scenario => scenario.Expression).Distinct().Count() >= 10);

        var targetExecutablePath = TestTargetPaths.ResolveExecutable();
        Assert.True(File.Exists(targetExecutablePath));
        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"interpreter-{CorpusVersion}-{Guid.NewGuid():N}.dmp");

        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(targetExecutablePath))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            ImmutableDictionary<string, ReplayEvidence> baseline;
            var firstOpen = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, firstOpen.Status);
            using (var firstSession = firstOpen.Value
                ?? throw new InvalidOperationException("The exact W2 corpus dump-open result carried no session."))
            {
                baseline = RunCorpus(firstSession);
            }

            var replayOpen = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, replayOpen.Status);
            using var replaySession = replayOpen.Value
                ?? throw new InvalidOperationException("The exact W2 replay dump-open result carried no session.");
            var replay = RunCorpus(replaySession);

            Assert.Equal(baseline.Keys.Order(StringComparer.Ordinal), replay.Keys.Order(StringComparer.Ordinal));
            foreach (var scenario in Scenarios)
            {
                var expected = baseline[scenario.Id];
                var actual = replay[scenario.Id];
                Assert.Equal(expected.ResultBytes, actual.ResultBytes);
                Assert.Equal(expected.ResultFingerprint, actual.ResultFingerprint);
                Assert.Equal(expected.PlanProjection, actual.PlanProjection);
                Assert.Equal(expected.PlanFingerprint, actual.PlanFingerprint);
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static ImmutableDictionary<string, ReplayEvidence> RunCorpus(ClrmdDumpSession session)
    {
        var exactSearch = session.FindStrongHandleObjectsByTypeName(
            "DumpProbe",
            maximumMatches: 8,
            maximumHandlesScanned: 100_000);
        Assert.Equal(ClrmdEvidenceStatus.Exact, exactSearch.Status);
        _ = Assert.Single(exactSearch.Matches);
        var exactBinding = DumpQueryRootBinding.FromSearchResult("root", exactSearch);
        Assert.Equal(DumpQueryRootBindingStatus.ExactObject, exactBinding.Status);
        Assert.Equal(session.Snapshot, exactBinding.Snapshot);

        var absentSearch = session.FindStrongHandleObjectsByTypeName(
            "AbsentDumpProbe",
            maximumMatches: 8,
            maximumHandlesScanned: 100_000);
        Assert.Equal(ClrmdEvidenceStatus.Exact, absentSearch.Status);
        Assert.Empty(absentSearch.Matches);
        var absentBinding = DumpQueryRootBinding.FromSearchResult("root", absentSearch);
        Assert.Equal(DumpQueryRootBindingStatus.ExhaustiveAbsence, absentBinding.Status);
        Assert.Equal(session.Snapshot, absentBinding.Snapshot);

        var partialSearch = session.FindStrongHandleObjectsByTypeName(
            "DumpProbe",
            maximumMatches: 8,
            maximumHandlesScanned: 1);
        Assert.Equal(ClrmdEvidenceStatus.Partial, partialSearch.Status);
        Assert.Equal(ClrmdValueIssue.LimitExceeded, partialSearch.Issue);
        Assert.InRange(partialSearch.Matches.Length, 0, 1);
        var partialBinding = DumpQueryRootBinding.FromSearchResult("root", partialSearch);
        Assert.Equal(DumpQueryRootBindingStatus.Partial, partialBinding.Status);
        Assert.Equal(session.Snapshot, partialBinding.Snapshot);
        Assert.Null(partialBinding.Root);

        var results = ImmutableDictionary.CreateBuilder<string, ReplayEvidence>(StringComparer.Ordinal);
        foreach (var scenario in Scenarios)
        {
            var binding = scenario.RootSelection switch
            {
                RootBindingSelection.Exact => exactBinding,
                RootBindingSelection.Absent => absentBinding,
                RootBindingSelection.Partial => partialBinding,
                _ => throw new InvalidOperationException("The W2 corpus root-binding selection is invalid."),
            };
            var first = Execute(session, scenario.Expression, binding);
            var second = Execute(session, scenario.Expression, binding);

            AssertExpected(scenario, first.Result);
            AssertExpected(scenario, second.Result);
            Assert.Equal(first.ResultBytes, second.ResultBytes);
            Assert.Equal(first.ResultFingerprint, second.ResultFingerprint);
            Assert.Equal(first.PlanProjection, second.PlanProjection);
            Assert.Equal(first.PlanFingerprint, second.PlanFingerprint);

            results.Add(
                scenario.Id,
                new ReplayEvidence(
                    first.ResultBytes,
                    first.ResultFingerprint,
                    first.PlanProjection,
                    first.PlanFingerprint));
        }

        var resultFingerprints = results.Values.Select(static evidence => evidence.ResultFingerprint).ToArray();
        Assert.Equal(resultFingerprints.Length, resultFingerprints.Distinct(StringComparer.Ordinal).Count());
        var planFingerprints = results.Values
            .Select(static evidence => evidence.PlanFingerprint)
            .OfType<string>()
            .ToArray();
        Assert.Equal(planFingerprints.Length, planFingerprints.Distinct(StringComparer.Ordinal).Count());

        return results.ToImmutable();
    }

    private static ScenarioExecution Execute(
        ClrmdDumpSession session,
        string expression,
        DumpQueryRootBinding binding)
    {
        var preparation = DumpQueryEngine.Prepare(session, expression, binding);
        EvaluationResult<DumpQueryValue> result;
        string? planProjection = null;
        string? planFingerprint = null;
        if (preparation.IsSuccess)
        {
            var plan = preparation.Plan
                ?? throw new InvalidOperationException("A successful W2 preparation carried no immutable query plan.");
            Assert.Null(preparation.Failure);
            planProjection = plan.ToCanonicalReplayProjection();
            planFingerprint = plan.ComputeSha256();
            Assert.Matches("^[0-9a-f]{64}$", planFingerprint);
            result = DumpQueryEngine.Evaluate(session, plan);
            Assert.Equal(plan.SemanticMode, result.SemanticMode);
            Assert.Contains(
                result.Provenance,
                item => item.Kind == EvaluationProvenanceKind.Policy &&
                    string.Equals(
                        item.SourceId,
                        $"dump-query-plan:sha256:{planFingerprint}",
                        StringComparison.Ordinal));
        }
        else
        {
            Assert.Null(preparation.Plan);
            result = preparation.Failure
                ?? throw new InvalidOperationException("A failed W2 preparation carried no result envelope.");
        }

        var resultBytes = EvaluationResultReplay.SerializeCanonical(
            result,
            static value => value.ToCanonicalReplayProjection());
        var resultFingerprint = EvaluationResultReplay.ComputeSha256(
            result,
            static value => value.ToCanonicalReplayProjection());
        Assert.Matches("^[0-9a-f]{64}$", resultFingerprint);
        return new ScenarioExecution(
            result,
            resultBytes,
            resultFingerprint,
            planProjection,
            planFingerprint);
    }

    private static void AssertExpected(Scenario scenario, EvaluationResult<DumpQueryValue> result)
    {
        Assert.True(result.SemanticMode is EvaluationSemanticMode.Observation or EvaluationSemanticMode.DerivedQuery);
        Assert.Equal(scenario.Completion, result.Completion);
        Assert.Equal(scenario.Completeness, result.Completeness);
        Assert.Equal(scenario.Evidence, result.Evidence);
        Assert.Equal(EvaluationEffectStatus.None, result.Effects);

        if (scenario.Value.Kind is null)
        {
            Assert.Null(result.Value);
        }
        else
        {
            var value = Assert.IsType<DumpQueryValue>(result.Value);
            Assert.Equal(scenario.Value.Kind, value.Kind);
            Assert.Equal(scenario.Value.Int32Value, value.Int32Value);
            if (scenario.Value.StringValue is not null)
            {
                Assert.Equal(scenario.Value.StringValue, value.StringValue);
            }

            if (scenario.Value.StringLength is int expectedLength)
            {
                Assert.NotNull(value.StringValue);
                Assert.Equal(expectedLength, value.StringValue.Length);
            }
            else if (scenario.Value.StringValue is null)
            {
                Assert.Null(value.StringValue);
            }
        }

        if (scenario.DiagnosticCode is null)
        {
            Assert.Empty(result.Diagnostics);
        }
        else
        {
            Assert.Equal(scenario.DiagnosticCode, Assert.Single(result.Diagnostics).Code);
        }
    }

    private enum RootBindingSelection
    {
        Exact,
        Absent,
        Partial,
    }

    private sealed record Scenario(
        string Id,
        string Expression,
        RootBindingSelection RootSelection,
        EvaluationCompletionStatus Completion,
        EvaluationCompleteness Completeness,
        EvaluationEvidenceStatus Evidence,
        ExpectedValue Value,
        string? DiagnosticCode = null);

    private sealed record ExpectedValue(
        DumpQueryValueKind? Kind,
        int? Int32Value,
        string? StringValue,
        int? StringLength)
    {
        internal static ExpectedValue None() => new(null, null, null, null);

        internal static ExpectedValue Null() => new(DumpQueryValueKind.Null, null, null, null);

        internal static ExpectedValue Int32(int value) => new(DumpQueryValueKind.Int32, value, null, null);

        internal static ExpectedValue String(string value) => new(DumpQueryValueKind.String, null, value, null);

        internal static ExpectedValue BoundedString(int length) =>
            new(DumpQueryValueKind.String, null, null, length);
    }

    private sealed record ScenarioExecution(
        EvaluationResult<DumpQueryValue> Result,
        byte[] ResultBytes,
        string ResultFingerprint,
        string? PlanProjection,
        string? PlanFingerprint);

    private sealed record ReplayEvidence(
        byte[] ResultBytes,
        string ResultFingerprint,
        string? PlanProjection,
        string? PlanFingerprint);
}
