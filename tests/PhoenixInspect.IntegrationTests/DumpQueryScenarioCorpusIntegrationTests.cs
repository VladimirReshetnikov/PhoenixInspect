using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

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
            ExpectedValue.Int32(ExpectedMarker),
            ExpectedExplanation.Plan(ValueReadShape.Int32)),
        new(
            $"{CorpusVersion}.exact-string",
            "root.Message",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage),
            ExpectedExplanation.Plan(ValueReadShape.StringValue, observedString: true)),
        new(
            $"{CorpusVersion}.exact-null",
            "root.OptionalMessage",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Null(),
            ExpectedExplanation.Plan(ValueReadShape.StringNull)),
        new(
            $"{CorpusVersion}.string-coalesce-selected",
            "root.OptionalMessage ?? \"<missing>\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String("<missing>"),
            ExpectedExplanation.Plan(
                ValueReadShape.StringNull,
                stringLiteral: true,
                coalescingTransformation: true)),
        new(
            $"{CorpusVersion}.string-coalesce-unselected",
            "root.Message ?? \"<unused>\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage),
            ExpectedExplanation.Plan(
                ValueReadShape.StringValue,
                stringLiteral: true,
                observedString: true,
                coalescingTransformation: true)),
        new(
            $"{CorpusVersion}.string-coalesce-unpaired-surrogate-d800",
            "root.Message ?? \"\uD800\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage),
            ExpectedExplanation.Plan(
                ValueReadShape.StringValue,
                stringLiteral: true,
                observedString: true,
                coalescingTransformation: true)),
        new(
            $"{CorpusVersion}.string-coalesce-unpaired-surrogate-d801",
            "root.Message ?? \"\uD801\"",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.String(ExpectedMessage),
            ExpectedExplanation.Plan(
                ValueReadShape.StringValue,
                stringLiteral: true,
                observedString: true,
                coalescingTransformation: true)),
        new(
            $"{CorpusVersion}.nullable-int32-present",
            "root.PresentCount ?? -1",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Int32(ExpectedPresentCount),
            ExpectedExplanation.Plan(
                ValueReadShape.NullableValue,
                coalescingTransformation: true)),
        new(
            $"{CorpusVersion}.nullable-int32-present-direct",
            "root.PresentCount",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Int32(ExpectedPresentCount),
            ExpectedExplanation.Plan(ValueReadShape.NullableValue)),
        new(
            $"{CorpusVersion}.nullable-int32-null",
            "root.OptionalCount ?? -17",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Int32(-17),
            ExpectedExplanation.Plan(
                ValueReadShape.NullableNull,
                coalescingTransformation: true)),
        new(
            $"{CorpusVersion}.nullable-int32-null-direct",
            "root.OptionalCount",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Null(),
            ExpectedExplanation.Plan(ValueReadShape.NullableNull)),
        new(
            $"{CorpusVersion}.nullable-int32-null-coalesce-null",
            "root.OptionalCount ?? null",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.Null(),
            ExpectedExplanation.Plan(
                ValueReadShape.NullableNull,
                coalescingTransformation: true)),
        new(
            $"{CorpusVersion}.missing-member",
            "root.AbsentField",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            ExpectedValue.None(),
            ExpectedExplanation.MemberFailure(),
            "DUMP_FIELD_UNAVAILABLE"),
        new(
            $"{CorpusVersion}.wrong-case-member",
            "root.marker",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            ExpectedValue.None(),
            ExpectedExplanation.MemberFailure(),
            "DUMP_FIELD_UNAVAILABLE"),
        new(
            $"{CorpusVersion}.unavailable-root",
            "root.Marker",
            RootBindingSelection.Absent,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            ExpectedValue.None(),
            ExpectedExplanation.RootFailure(),
            "QUERY_ROOT_ABSENT"),
        new(
            $"{CorpusVersion}.partial-root",
            "root.Marker",
            RootBindingSelection.Partial,
            EvaluationCompletionStatus.BudgetExhausted,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Partial,
            ExpectedValue.None(),
            ExpectedExplanation.RootFailure(),
            "QUERY_ROOT_LIMIT_EXCEEDED"),
        new(
            $"{CorpusVersion}.unsupported-type",
            "root.Enabled",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            ExpectedExplanation.TypeFailure(),
            "QUERY_FIELD_TYPE_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.incompatible-coalescing",
            "root.Marker ?? 0",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            ExpectedExplanation.TypeFailure(),
            "QUERY_COALESCE_TYPE_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.null-conditional-rejected",
            "root?.Message",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            ExpectedExplanation.ParseFailure(ParserReach.Root),
            "QUERY_SYNTAX_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.invalid-syntax",
            "root.Message()",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            ExpectedExplanation.ParseFailure(ParserReach.Field),
            "QUERY_SYNTAX_UNSUPPORTED"),
        new(
            $"{CorpusVersion}.root-mismatch",
            "other.Marker",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            ExpectedValue.None(),
            ExpectedExplanation.ParseFailure(ParserReach.Root),
            "QUERY_ROOT_MISMATCH"),
        new(
            $"{CorpusVersion}.bounded-partial-string",
            "root.LongMessage",
            RootBindingSelection.Exact,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Partial,
            ExpectedValue.String(new string('x', 4096)),
            ExpectedExplanation.Plan(ValueReadShape.StringPartial, observedString: true),
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
            var (binding, search) = scenario.RootSelection switch
            {
                RootBindingSelection.Exact => (exactBinding, exactSearch),
                RootBindingSelection.Absent => (absentBinding, absentSearch),
                RootBindingSelection.Partial => (partialBinding, partialSearch),
                _ => throw new InvalidOperationException("The W2 corpus root-binding selection is invalid."),
            };
            var first = Execute(session, scenario.Expression, binding);
            var second = Execute(session, scenario.Expression, binding);

            AssertExpected(scenario, first, binding, search, session);
            AssertExpected(scenario, second, binding, search, session);
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
        DumpQueryPlan? plan = null;
        string? planProjection = null;
        string? planFingerprint = null;
        if (preparation.IsSuccess)
        {
            plan = preparation.Plan
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
            plan,
            resultBytes,
            resultFingerprint,
            planProjection,
            planFingerprint);
    }

    private static void AssertExpected(
        Scenario scenario,
        ScenarioExecution execution,
        DumpQueryRootBinding binding,
        ClrmdHeapObjectSearchResult search,
        ClrmdDumpSession session)
    {
        var result = execution.Result;
        Assert.Equal(EvaluationSemanticMode.DerivedQuery, result.SemanticMode);
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
            else
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

        AssertExplanation(scenario, execution, binding, search, session);
    }

    private static void AssertExplanation(
        Scenario scenario,
        ScenarioExecution execution,
        DumpQueryRootBinding binding,
        ClrmdHeapObjectSearchResult search,
        ClrmdDumpSession session)
    {
        var explanation = scenario.Explanation;
        var result = execution.Result;
        var expectsPlan = explanation.Stage == ExplanationStage.Plan;
        Assert.Equal(expectsPlan, execution.Plan is not null);
        Assert.Equal(expectsPlan, execution.PlanProjection is not null);
        Assert.Equal(expectsPlan, execution.PlanFingerprint is not null);

        Assert.Equal(EvaluationEvidenceSourceKind.DumpSnapshot, result.Context.SourceKind);
        Assert.Equal(EvaluationIdentityAvailability.Available, result.Context.Snapshot.Availability);
        Assert.Equal(session.Snapshot.MemorySourceId, result.Context.Snapshot.SourceId);
        Assert.Equal(
            binding.Root is null
                ? EvaluationIdentityAvailability.Unavailable
                : EvaluationIdentityAvailability.Available,
            result.Context.Module.Availability);
        Assert.Equal(binding.Root?.Module.Identity.SourceId, result.Context.Module.SourceId);
        Assert.Equal(EvaluationFallbackStatus.None, result.Context.Fallback.Status);

        AssertRootBindingMatchesSearch(scenario.RootSelection, binding, search);
        var expectedBounds = new Dictionary<string, long>(StringComparer.Ordinal);
        expectedBounds.Add("root-selection.maximum-handles", search.MaximumHandlesScanned);
        expectedBounds.Add("root-selection.maximum-matches", search.MaximumMatches);

        AddExpectedBound(expectedBounds, "query.expression.characters", 512);
        AddExpectedBound(expectedBounds, "query.root-name.characters", 64);
        if (explanation.ParserReach >= ParserReach.Field)
        {
            AddExpectedBound(expectedBounds, "query.field-name.characters", 64);
        }

        if (explanation.ParserReach == ParserReach.StringLiteral)
        {
            AddExpectedBound(expectedBounds, "query.string-literal.characters", 256);
        }

        if (explanation.Stage is ExplanationStage.MemberFailure or ExplanationStage.TypeFailure or ExplanationStage.Plan)
        {
            AddExpectedBound(
                expectedBounds,
                ClrmdDumpSession.InstanceFieldTraversalBound.Name,
                ClrmdDumpSession.InstanceFieldTraversalBound.Value);
        }

        var reachedMemoryRead = explanation.Stage != ExplanationStage.ParseFailure &&
            (!binding.Evidence.IsEmpty || explanation.ValueReadShape != ValueReadShape.None);
        if (reachedMemoryRead)
        {
            AddExpectedBound(expectedBounds, "dump.memory-read.bytes", session.Memory.MaximumReadLength);
        }

        if (explanation.ObservedString)
        {
            AddExpectedBound(expectedBounds, "query.observed-string.characters", 4096);
        }

        Assert.Equal(
            expectedBounds.Keys.Order(StringComparer.Ordinal),
            result.Context.Bounds.Select(static bound => bound.Name));
        Assert.All(result.Context.Bounds, bound => Assert.Equal(expectedBounds[bound.Name], bound.Value));

        var expectedProvenance = CreateExpectedProvenanceShape(explanation, binding);
        Assert.Equal(expectedProvenance, result.Provenance.Select(ClassifyProvenance));
        AssertProvenancePayload(scenario, execution, binding, search, session);
        AssertValueReadShape(scenario, execution, binding, session);
    }

    private static void AssertRootBindingMatchesSearch(
        RootBindingSelection selection,
        DumpQueryRootBinding binding,
        ClrmdHeapObjectSearchResult search)
    {
        var expectedSelector = selection == RootBindingSelection.Absent ? "AbsentDumpProbe" : "DumpProbe";
        var expectedSearchStatus = selection == RootBindingSelection.Partial
            ? ClrmdEvidenceStatus.Partial
            : ClrmdEvidenceStatus.Exact;
        var expectedBindingStatus = selection switch
        {
            RootBindingSelection.Exact => DumpQueryRootBindingStatus.ExactObject,
            RootBindingSelection.Absent => DumpQueryRootBindingStatus.ExhaustiveAbsence,
            RootBindingSelection.Partial => DumpQueryRootBindingStatus.Partial,
            _ => throw new ArgumentOutOfRangeException(nameof(selection)),
        };
        var expectedIssue = selection == RootBindingSelection.Partial
            ? ClrmdValueIssue.LimitExceeded
            : ClrmdValueIssue.None;
        var expectedMaximumHandles = selection == RootBindingSelection.Partial ? 1 : 100_000;

        Assert.Equal(expectedSelector, search.TypeNameSelector);
        Assert.Equal(expectedSearchStatus, search.Status);
        Assert.Equal(expectedIssue, search.Issue);
        Assert.Equal(expectedMaximumHandles, search.MaximumHandlesScanned);
        Assert.Equal(8, search.MaximumMatches);
        Assert.InRange(search.HandlesScanned, 0, expectedMaximumHandles);
        Assert.False(search.MatchLimitReached);
        Assert.Equal(search.Matches.Length, search.MatchesRetained);

        Assert.Equal(expectedBindingStatus, binding.Status);
        Assert.Equal(expectedIssue, binding.Issue);
        Assert.Equal(search.TypeNameSelector, binding.TypeNameSelector);
        Assert.Equal(search.Status, binding.SearchStatus);
        Assert.Equal(search.HandlesScanned, binding.HandlesScanned);
        Assert.Equal(search.MaximumHandlesScanned, binding.MaximumHandlesScanned);
        Assert.Equal(search.MaximumMatches, binding.MaximumMatches);
        Assert.Equal(search.MatchesRetained, binding.MatchesRetained);
        Assert.Equal(search.MatchLimitReached, binding.MatchLimitReached);
        Assert.Equal(search.Evidence.ToArray(), binding.Evidence.ToArray());
        Assert.Equal(
            new[] { "root-selection.maximum-handles", "root-selection.maximum-matches" },
            binding.AppliedBounds.Select(static bound => bound.Name));
        Assert.Equal(
            new long[] { expectedMaximumHandles, 8 },
            binding.AppliedBounds.Select(static bound => bound.Value));
    }

    private static void AssertProvenancePayload(
        Scenario scenario,
        ScenarioExecution execution,
        DumpQueryRootBinding binding,
        ClrmdHeapObjectSearchResult search,
        ClrmdDumpSession session)
    {
        var provenance = execution.Result.Provenance;
        if (scenario.Explanation.Stage == ExplanationStage.ParseFailure)
        {
            AssertNonRangeProvenance(
                provenance[0],
                EvaluationProvenanceKind.Policy,
                "dump-query:grammar-v1");
            AssertNonRangeProvenance(
                provenance[1],
                EvaluationProvenanceKind.Policy,
                ComputeRawInputProvenanceId(scenario.Expression, "root"));
            return;
        }

        var index = 0;
        foreach (var read in search.Evidence)
        {
            AssertMemoryRead(
                provenance[index++],
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.BytesRead);
        }

        AssertNonRangeProvenance(
            provenance[index++],
            EvaluationProvenanceKind.Policy,
            ComputeRootSelectionProvenanceId(binding, search));

        if (scenario.Explanation.Stage == ExplanationStage.Plan)
        {
            var plan = Assert.IsType<DumpQueryPlan>(execution.Plan);
            AssertRuntimeStructure(provenance[index++], binding.Root!, field: null);
            AssertRuntimeStructure(provenance[index++], binding.Root!, plan.Field);
            AssertNonRangeProvenance(
                provenance[index++],
                EvaluationProvenanceKind.Policy,
                $"dump-query-plan:sha256:{execution.PlanFingerprint}");
            index += ValueReadCount(scenario.Explanation.ValueReadShape);
            if (scenario.Explanation.CoalescingTransformation)
            {
                AssertNonRangeProvenance(
                    provenance[index++],
                    EvaluationProvenanceKind.Transformation,
                    "dump-query:null-coalesce-v1");
            }

            Assert.Equal(provenance.Length, index);
            return;
        }

        AssertNonRangeProvenance(
            provenance[index++],
            EvaluationProvenanceKind.Policy,
            ComputeParsedRequestProvenanceId(scenario.Expression));
        if (scenario.Explanation.Stage is ExplanationStage.MemberFailure or ExplanationStage.TypeFailure)
        {
            AssertRuntimeStructure(provenance[index++], binding.Root!, field: null);
        }

        if (scenario.Explanation.Stage == ExplanationStage.TypeFailure)
        {
            var parsed = DumpQueryParser.Parse(scenario.Expression, "root");
            var fieldName = Assert.IsType<ParsedDumpQuery>(parsed.Query).FieldName;
            var selectedField = session.GetInstanceField(binding.Root!, fieldName);
            Assert.Equal(ClrmdEvidenceStatus.Exact, selectedField.Status);
            AssertRuntimeStructure(provenance[index++], binding.Root!, selectedField.Value!);
        }

        Assert.Equal(provenance.Length, index);
    }

    private static void AssertNonRangeProvenance(
        EvaluationProvenance provenance,
        EvaluationProvenanceKind kind,
        string sourceId)
    {
        Assert.Equal(kind, provenance.Kind);
        Assert.Equal(sourceId, provenance.SourceId);
        Assert.Null(provenance.Address);
        Assert.Null(provenance.RequestedLength);
        Assert.Null(provenance.ObservedLength);
    }

    private static void AssertRuntimeStructure(
        EvaluationProvenance provenance,
        ClrmdHeapObjectInfo root,
        ClrmdInstanceFieldInfo? field)
    {
        Assert.Equal(EvaluationProvenanceKind.RuntimeStructure, provenance.Kind);
        Assert.Equal(root.Snapshot.MemorySourceId, provenance.SourceId);
        Assert.Equal(field?.Address ?? root.Address, provenance.Address);
        Assert.Equal(field?.Size, provenance.RequestedLength);
        Assert.Equal(field?.Size, provenance.ObservedLength);
    }

    private static string ComputeRawInputProvenanceId(string expression, string rootName)
    {
        var builder = new StringBuilder();
        AppendCanonicalString(builder, "dump-query-input-v1");
        AppendCanonicalString(builder, "value");
        AppendCanonicalString(builder, expression);
        AppendCanonicalString(builder, "value");
        AppendCanonicalString(builder, rootName);
        return $"dump-query-input:sha256:{ComputeSha256(builder)}";
    }

    private static string ComputeParsedRequestProvenanceId(string expression)
    {
        var parsed = DumpQueryParser.Parse(expression, "root");
        var query = Assert.IsType<ParsedDumpQuery>(parsed.Query);
        var builder = new StringBuilder();
        AppendCanonicalString(builder, "dump-query-request-v1");
        AppendCanonicalString(builder, query.RootName);
        AppendCanonicalString(builder, query.FieldName);
        if (query.CoalesceLiteral is null)
        {
            AppendCanonicalString(builder, "none");
        }
        else
        {
            AppendCanonicalString(builder, query.CoalesceLiteral.Kind.ToString());
            AppendCanonicalString(builder, query.CoalesceLiteral.Kind switch
            {
                DumpQueryLiteralKind.Null => string.Empty,
                DumpQueryLiteralKind.Int32 =>
                    query.CoalesceLiteral.Int32Value.ToString(CultureInfo.InvariantCulture),
                DumpQueryLiteralKind.String => query.CoalesceLiteral.StringValue!,
                _ => throw new InvalidOperationException("The W2 corpus parsed an invalid literal kind."),
            });
        }

        return $"dump-query-request:sha256:{ComputeSha256(builder)}";
    }

    private static string ComputeRootSelectionProvenanceId(
        DumpQueryRootBinding binding,
        ClrmdHeapObjectSearchResult search)
    {
        var builder = new StringBuilder();
        AppendCanonicalString(builder, "dump-query-root-selection-v1");
        AppendCanonicalString(builder, search.TypeNameSelector);
        AppendCanonicalString(builder, ((int)search.Status).ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, ((int)binding.Status).ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, ((int)binding.Issue).ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, search.HandlesScanned.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, search.MaximumHandlesScanned.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, search.MaximumMatches.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, search.MatchesRetained.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, search.MatchLimitReached ? "1" : "0");
        return $"dump-query-root-selection:sha256:{ComputeSha256(builder)}";
    }

    private static string ComputeSha256(StringBuilder builder) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();

    private static void AppendCanonicalString(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }

    private static void AddExpectedBound(
        Dictionary<string, long> bounds,
        string name,
        long value) => bounds.Add(name, value);

    private static ImmutableArray<string> CreateExpectedProvenanceShape(
        ExpectedExplanation explanation,
        DumpQueryRootBinding binding)
    {
        var expected = ImmutableArray.CreateBuilder<string>();
        if (explanation.Stage == ExplanationStage.ParseFailure)
        {
            expected.Add("grammar");
            expected.Add("input");
            return expected.ToImmutable();
        }

        expected.AddRange(Enumerable.Repeat("memory", binding.Evidence.Length));
        if (binding.TypeNameSelector is not null)
        {
            expected.Add("root-selection");
        }

        if (explanation.Stage == ExplanationStage.Plan)
        {
            expected.Add("runtime");
            expected.Add("runtime");
            expected.Add("plan");
            expected.AddRange(Enumerable.Repeat("memory", ValueReadCount(explanation.ValueReadShape)));
            if (explanation.CoalescingTransformation)
            {
                expected.Add("coalesce");
            }

            return expected.ToImmutable();
        }

        expected.Add("request");
        if (explanation.Stage is ExplanationStage.MemberFailure or ExplanationStage.TypeFailure)
        {
            expected.Add("runtime");
        }

        if (explanation.Stage == ExplanationStage.TypeFailure)
        {
            expected.Add("runtime");
        }

        return expected.ToImmutable();
    }

    private static string ClassifyProvenance(EvaluationProvenance provenance) => provenance.Kind switch
    {
        EvaluationProvenanceKind.DumpMemory => "memory",
        EvaluationProvenanceKind.RuntimeStructure => "runtime",
        EvaluationProvenanceKind.Transformation when string.Equals(
            provenance.SourceId,
            "dump-query:null-coalesce-v1",
            StringComparison.Ordinal) => "coalesce",
        EvaluationProvenanceKind.Policy when string.Equals(
            provenance.SourceId,
            "dump-query:grammar-v1",
            StringComparison.Ordinal) => "grammar",
        EvaluationProvenanceKind.Policy when provenance.SourceId.StartsWith(
            "dump-query-input:sha256:",
            StringComparison.Ordinal) => "input",
        EvaluationProvenanceKind.Policy when provenance.SourceId.StartsWith(
            "dump-query-request:sha256:",
            StringComparison.Ordinal) => "request",
        EvaluationProvenanceKind.Policy when provenance.SourceId.StartsWith(
            "dump-query-root-selection:sha256:",
            StringComparison.Ordinal) => "root-selection",
        EvaluationProvenanceKind.Policy when provenance.SourceId.StartsWith(
            "dump-query-plan:sha256:",
            StringComparison.Ordinal) => "plan",
        _ => $"unexpected:{provenance.Kind}:{provenance.SourceId}",
    };

    private static void AssertValueReadShape(
        Scenario scenario,
        ScenarioExecution execution,
        DumpQueryRootBinding binding,
        ClrmdDumpSession session)
    {
        var memorySourceId = session.Memory.SourceId;
        var shape = scenario.Explanation.ValueReadShape;
        var valueReads = execution.Result.Provenance
            .Where(static item => item.Kind == EvaluationProvenanceKind.DumpMemory)
            .Skip(binding.Evidence.Length)
            .ToArray();
        Assert.Equal(ValueReadCount(shape), valueReads.Length);
        if (shape == ValueReadShape.None)
        {
            return;
        }

        var plan = Assert.IsType<DumpQueryPlan>(execution.Plan);
        if (shape is ValueReadShape.NullableNull or ValueReadShape.NullableValue)
        {
            var layout = Assert.IsType<ClrmdNullableInt32FieldLayout>(plan.Field.NullableInt32Layout);
            AssertMemoryRead(valueReads[0], memorySourceId, layout.HasValueAddress, sizeof(byte));
            Assert.DoesNotContain(valueReads, read => read.Address == layout.ValueAddress && shape == ValueReadShape.NullableNull);
            if (shape == ValueReadShape.NullableValue)
            {
                AssertMemoryRead(valueReads[1], memorySourceId, layout.ValueAddress, sizeof(int));
            }

            return;
        }

        AssertMemoryRead(
            valueReads[0],
            memorySourceId,
            plan.Field.Address,
            shape == ValueReadShape.Int32 ? sizeof(int) : plan.Field.Size);

        if (shape is ValueReadShape.StringValue or ValueReadShape.StringPartial)
        {
            var stringAddress = Assert.IsType<ulong>(valueReads[1].Address);
            var independentObservation = session.ReadStringField(binding.Root!, plan.Field, 4096);
            Assert.Equal(stringAddress, independentObservation.StringAddress);
            Assert.Equal(
                independentObservation.Evidence.Select(static read => (ulong?)read.Address),
                valueReads.Select(static read => read.Address));
            Assert.NotEqual(plan.Field.Address, stringAddress);
            AssertMemoryRead(valueReads[1], memorySourceId, stringAddress, sizeof(ulong));
            AssertMemoryRead(valueReads[2], memorySourceId, stringAddress + sizeof(ulong), sizeof(int));
            var expectedCharacterBytes = shape == ValueReadShape.StringPartial
                ? 4096 * sizeof(char)
                : Assert.IsType<DumpQueryValue>(execution.Result.Value).StringValue!.Length * sizeof(char);
            AssertMemoryRead(
                valueReads[3],
                memorySourceId,
                stringAddress + sizeof(ulong) + sizeof(int),
                expectedCharacterBytes);
        }
    }

    private static void AssertMemoryRead(
        EvaluationProvenance read,
        string sourceId,
        ulong address,
        int requestedLength,
        int? observedLength = null)
    {
        Assert.Equal(EvaluationProvenanceKind.DumpMemory, read.Kind);
        Assert.Equal(sourceId, read.SourceId);
        Assert.Equal(address, read.Address);
        Assert.Equal(requestedLength, read.RequestedLength);
        Assert.Equal(observedLength ?? requestedLength, read.ObservedLength);
    }

    private static int ValueReadCount(ValueReadShape shape) => shape switch
    {
        ValueReadShape.None => 0,
        ValueReadShape.Int32 or ValueReadShape.StringNull or ValueReadShape.NullableNull => 1,
        ValueReadShape.NullableValue => 2,
        ValueReadShape.StringValue or ValueReadShape.StringPartial => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private enum ExplanationStage
    {
        ParseFailure,
        RootFailure,
        MemberFailure,
        TypeFailure,
        Plan,
    }

    private enum ParserReach
    {
        Root,
        Field,
        StringLiteral,
    }

    private enum ValueReadShape
    {
        None,
        Int32,
        StringNull,
        StringValue,
        StringPartial,
        NullableNull,
        NullableValue,
    }

    private sealed record ExpectedExplanation(
        ExplanationStage Stage,
        ParserReach ParserReach,
        ValueReadShape ValueReadShape,
        bool ObservedString,
        bool CoalescingTransformation)
    {
        internal static ExpectedExplanation Plan(
            ValueReadShape valueReadShape,
            bool stringLiteral = false,
            bool observedString = false,
            bool coalescingTransformation = false) => new(
                ExplanationStage.Plan,
                stringLiteral ? ParserReach.StringLiteral : ParserReach.Field,
                valueReadShape,
                observedString,
                coalescingTransformation);

        internal static ExpectedExplanation ParseFailure(ParserReach parserReach) => new(
            ExplanationStage.ParseFailure,
            parserReach,
            ValueReadShape.None,
            ObservedString: false,
            CoalescingTransformation: false);

        internal static ExpectedExplanation RootFailure() => new(
            ExplanationStage.RootFailure,
            ParserReach.Field,
            ValueReadShape.None,
            ObservedString: false,
            CoalescingTransformation: false);

        internal static ExpectedExplanation MemberFailure() => new(
            ExplanationStage.MemberFailure,
            ParserReach.Field,
            ValueReadShape.None,
            ObservedString: false,
            CoalescingTransformation: false);

        internal static ExpectedExplanation TypeFailure() => new(
            ExplanationStage.TypeFailure,
            ParserReach.Field,
            ValueReadShape.None,
            ObservedString: false,
            CoalescingTransformation: false);
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
        ExpectedExplanation Explanation,
        string? DiagnosticCode = null);

    private sealed record ExpectedValue(
        DumpQueryValueKind? Kind,
        int? Int32Value,
        string? StringValue)
    {
        internal static ExpectedValue None() => new(null, null, null);

        internal static ExpectedValue Null() => new(DumpQueryValueKind.Null, null, null);

        internal static ExpectedValue Int32(int value) => new(DumpQueryValueKind.Int32, value, null);

        internal static ExpectedValue String(string value) => new(DumpQueryValueKind.String, null, value);
    }

    private sealed record ScenarioExecution(
        EvaluationResult<DumpQueryValue> Result,
        DumpQueryPlan? Plan,
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
