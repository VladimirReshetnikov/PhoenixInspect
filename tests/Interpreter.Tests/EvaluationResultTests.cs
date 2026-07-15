using System.Collections.Immutable;
using System.Runtime.InteropServices;
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
        var context = EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            EvaluationEvidenceIdentity.CreateAvailable("dump-sha256:abc"),
            EvaluationEvidenceIdentity.Unavailable,
            EvaluationFallback.None,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("query.string.characters", 4096),
                new EvaluationDeterministicBound("dump.memory-read.bytes", 16_777_216)));
        var result = EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.Observation,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Partial,
            EvaluationEffectStatus.None,
            new TestValue("prefix"),
            context,
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
            "{\"semanticMode\":\"Observation\",\"completion\":\"Completed\",\"completeness\":\"Partial\",\"evidence\":\"Partial\",\"effects\":\"None\",\"context\":{\"sourceKind\":\"DumpSnapshot\",\"snapshot\":{\"availability\":\"Available\",\"sourceId\":\"dump-sha256:abc\"},\"module\":{\"availability\":\"Unavailable\",\"sourceId\":null},\"fallback\":{\"status\":\"None\",\"name\":\"none\"},\"bounds\":[{\"name\":\"dump.memory-read.bytes\",\"value\":16777216},{\"name\":\"query.string.characters\",\"value\":4096}]},\"value\":\"prefix\",\"provenance\":[{\"kind\":\"DumpMemory\",\"sourceId\":\"dump-sha256:abc\",\"address\":\"0x0000000000001234\",\"requestedLength\":8,\"observedLength\":4}],\"diagnostics\":[{\"code\":\"DUMP_MEMORY_UNAVAILABLE\",\"message\":\"Required dump-memory bytes are incomplete or unavailable.\"}]}",
            Encoding.UTF8.GetString(first));
    }

    /// <summary>Checks that the compatibility factory records neutral context explicitly rather than leaving it absent.</summary>
    [Fact]
    public void ResultFactoryWithoutEvidenceContextUsesExplicitNeutralContext()
    {
        var result = EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            EvaluationEffectStatus.None,
            null);

        Assert.Same(EvaluationEvidenceContext.Neutral, result.Context);
        Assert.Equal(EvaluationEvidenceSourceKind.None, result.Context.SourceKind);
        Assert.Equal(EvaluationIdentityAvailability.NotApplicable, result.Context.Snapshot.Availability);
        Assert.Equal(EvaluationIdentityAvailability.NotApplicable, result.Context.Module.Availability);
        Assert.Equal(EvaluationFallbackStatus.None, result.Context.Fallback.Status);
        Assert.Equal("none", result.Context.Fallback.Name);
        Assert.Empty(result.Context.Bounds);
    }

    /// <summary>Checks identity, fallback, and deterministic-bound invariants at the common result boundary.</summary>
    [Fact]
    public void EvidenceContextRejectsAmbiguousOrNonCanonicalInputs()
    {
        var snapshot = EvaluationEvidenceIdentity.CreateAvailable("dump-sha256:abc");
        var module = EvaluationEvidenceIdentity.CreateAvailable("module:v1:abc");
        var appliedFallback = EvaluationFallback.CreateApplied("whole-file-identity");

        Assert.Equal(EvaluationIdentityAvailability.Available, snapshot.Availability);
        Assert.Equal("module:v1:abc", module.SourceId);
        Assert.Equal(EvaluationFallbackStatus.Applied, appliedFallback.Status);
        Assert.Throws<ArgumentException>(() => EvaluationEvidenceIdentity.CreateAvailable("contains whitespace"));
        Assert.Throws<ArgumentException>(() => EvaluationFallback.CreateApplied("none"));
        Assert.Throws<ArgumentException>(() => EvaluationFallback.CreateApplied("Not-Canonical"));
        Assert.Throws<ArgumentException>(() => new EvaluationDeterministicBound("Invalid", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationDeterministicBound("valid.bound", -1));
        Assert.Throws<ArgumentException>(() => EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            EvaluationEvidenceIdentity.Unavailable,
            EvaluationEvidenceIdentity.Unavailable,
            EvaluationFallback.None));
        Assert.Throws<ArgumentException>(() => EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            snapshot,
            EvaluationEvidenceIdentity.NotApplicable,
            EvaluationFallback.None));
        Assert.Throws<ArgumentException>(() => EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.None,
            EvaluationEvidenceIdentity.NotApplicable,
            EvaluationEvidenceIdentity.NotApplicable,
            EvaluationFallback.None,
            ImmutableArray.Create(new EvaluationDeterministicBound("unexpected.bound", 1))));
        Assert.Throws<ArgumentException>(() => EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            snapshot,
            module,
            EvaluationFallback.None,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("duplicate.bound", 1),
                new EvaluationDeterministicBound("duplicate.bound", 2))));
        Assert.Throws<ArgumentException>(() => EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            snapshot,
            module,
            EvaluationFallback.None,
            ImmutableArray.CreateRange(new EvaluationDeterministicBound[] { null! })));
    }

    /// <summary>Checks that context fields and normalized bound order participate in replay fingerprints.</summary>
    [Fact]
    public void CanonicalReplayFingerprintIncludesEvidenceContext()
    {
        var snapshot = EvaluationEvidenceIdentity.CreateAvailable("dump-sha256:abc");
        var firstContext = EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            snapshot,
            EvaluationEvidenceIdentity.Unavailable,
            EvaluationFallback.None,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("z.bound", 2),
                new EvaluationDeterministicBound("a.bound", 1)));
        var reorderedContext = EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            snapshot,
            EvaluationEvidenceIdentity.Unavailable,
            EvaluationFallback.None,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("a.bound", 1),
                new EvaluationDeterministicBound("z.bound", 2)));
        var changedContext = EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            snapshot,
            EvaluationEvidenceIdentity.Unavailable,
            EvaluationFallback.None,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("a.bound", 1),
                new EvaluationDeterministicBound("z.bound", 3)));

        var first = CreateBlockedResult(firstContext);
        var reordered = CreateBlockedResult(reorderedContext);
        var changed = CreateBlockedResult(changedContext);

        Assert.Equal(
            EvaluationResultReplay.SerializeCanonical(first, static value => value.Text),
            EvaluationResultReplay.SerializeCanonical(reordered, static value => value.Text));
        Assert.Equal(
            EvaluationResultReplay.ComputeSha256(first, static value => value.Text),
            EvaluationResultReplay.ComputeSha256(reordered, static value => value.Text));
        Assert.NotEqual(
            EvaluationResultReplay.ComputeSha256(first, static value => value.Text),
            EvaluationResultReplay.ComputeSha256(changed, static value => value.Text));
    }

    /// <summary>
    /// Proves mutation of a public bounds array cannot reorder or replace the context's retained bounds or change a
    /// result's canonical replay bytes and fingerprint.
    /// </summary>
    [Fact]
    public void EvidenceContextBoundsProjectionCannotMutateCanonicalReplay()
    {
        var context = EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.Synthetic,
            EvaluationEvidenceIdentity.NotApplicable,
            EvaluationEvidenceIdentity.NotApplicable,
            EvaluationFallback.None,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("z.bound", 2),
                new EvaluationDeterministicBound("a.bound", 1)));
        var result = CreateBlockedResult(context);
        var expectedBounds = context.Bounds.ToArray();
        var expectedCanonical = EvaluationResultReplay.SerializeCanonical(
            result,
            static value => value.Text);
        var expectedSha256 = EvaluationResultReplay.ComputeSha256(
            result,
            static value => value.Text);

        var visibleBounds = context.Bounds;
        var visibleBacking = ImmutableCollectionsMarshal.AsArray(visibleBounds)!;
        visibleBacking[0] = new EvaluationDeterministicBound("mutated.bound", 99);

        Assert.Equal(expectedBounds, context.Bounds);
        Assert.Equal(
            expectedCanonical,
            EvaluationResultReplay.SerializeCanonical(result, static value => value.Text));
        Assert.Equal(
            expectedSha256,
            EvaluationResultReplay.ComputeSha256(result, static value => value.Text));
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

    private static EvaluationResult<TestValue> CreateBlockedResult(EvaluationEvidenceContext context) =>
        EvaluationResult<TestValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Unavailable,
            EvaluationEffectStatus.None,
            null,
            context);
}
