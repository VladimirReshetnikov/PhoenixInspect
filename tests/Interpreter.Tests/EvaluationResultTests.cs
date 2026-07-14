using System.Collections.Immutable;
using System.Text;
using Interpreter.Core.Abstractions;
using Xunit;

namespace Interpreter.Tests;

/// <summary>Exercises the independent result axes, invariants, provenance, and canonical replay envelope.</summary>
public sealed class EvaluationResultTests
{
    /// <summary>Checks that result axes remain independent and serialize in one canonical order.</summary>
    [Fact]
    public void PartialObservationHasStableCanonicalReplayEnvelope()
    {
        var result = EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Partial,
            EvaluationEffectStatus.None,
            new TestValue("prefix"),
            ImmutableArray.Create(new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                "dump-sha256:abc",
                0x1234,
                8,
                4)),
            ImmutableArray.Create(new EvaluationDiagnostic(
                "DUMP_MEMORY_UNAVAILABLE",
                "Required dump-memory bytes are incomplete or unavailable.")));

        var first = EvaluationResultReplay.SerializeCanonical(result, static value => value.Text);
        var second = EvaluationResultReplay.SerializeCanonical(result, static value => value.Text);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", EvaluationResultReplay.ComputeSha256(
            result,
            static value => value.Text));
        Assert.Equal(
            "{\"semanticMode\":\"Observation\",\"completion\":\"Completed\",\"completeness\":\"Partial\",\"evidence\":\"Partial\",\"effects\":\"None\",\"value\":\"prefix\",\"provenance\":[{\"kind\":\"DumpMemory\",\"sourceId\":\"dump-sha256:abc\",\"address\":\"0x0000000000001234\",\"requestedLength\":8,\"observedLength\":4}],\"diagnostics\":[{\"code\":\"DUMP_MEMORY_UNAVAILABLE\",\"message\":\"Required dump-memory bytes are incomplete or unavailable.\"}]}",
            Encoding.UTF8.GetString(first));
    }

    /// <summary>Checks that contradictory result-axis combinations cannot cross the host boundary.</summary>
    [Fact]
    public void ResultEnvelopeRejectsContradictoryAxesAndInvalidProvenance()
    {
        Assert.Throws<ArgumentNullException>(() => EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            null));
        Assert.Throws<ArgumentException>(() => EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            EvaluationEffectStatus.None,
            new TestValue("unexpected")));
        Assert.Throws<ArgumentException>(() => EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Partial,
            EvaluationEffectStatus.VirtualOnly,
            new TestValue("unexpected")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationProvenance(
            EvaluationProvenanceKind.DumpMemory,
            "dump-sha256:abc",
            requestedLength: 1,
            observedLength: 2));
        Assert.Throws<ArgumentNullException>(() => EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Partial,
            EvaluationEffectStatus.None,
            null));
        Assert.Throws<ArgumentOutOfRangeException>(() => EvaluationResult<TestValue>.Create(
            (EvaluationSemanticMode)int.MaxValue,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            EvaluationEffectStatus.None,
            null));
        Assert.Throws<ArgumentException>(() => new EvaluationProvenance(
            EvaluationProvenanceKind.DumpMemory,
            "dump-sha256:abc"));
        Assert.Throws<ArgumentException>(() => EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            EvaluationEffectStatus.None,
            null,
            ImmutableArray.CreateRange(new EvaluationProvenance[] { null! })));
        Assert.Throws<ArgumentException>(() => EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            EvaluationEffectStatus.None,
            null,
            diagnostics: ImmutableArray.CreateRange(new EvaluationDiagnostic[] { null! })));

        var exactReasonForBlockedQuery = EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            null);
        Assert.Equal(EvaluationEvidenceStatus.Exact, exactReasonForBlockedQuery.Evidence);
        Assert.Equal(EvaluationCompleteness.None, exactReasonForBlockedQuery.Completeness);
    }

    private sealed record TestValue(string Text);
}
