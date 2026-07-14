using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Verifies that adapter provenance and host-facing answer completeness remain independent.</summary>
public sealed class ClrmdEvaluationResultExtensionsTests
{
    /// <summary>
    /// Checks that a short or wholly unavailable integer read retains its evidence without presenting the
    /// observation wrapper as a partial scalar answer.
    /// </summary>
    /// <param name="bytesRead">Number of the requested four bytes supplied by the modeled dump reader.</param>
    /// <param name="adapterStatus">Adapter evidence status implied by <paramref name="bytesRead"/>.</param>
    /// <param name="expectedEvidence">Common-envelope evidence status expected after projection.</param>
    [Theory]
    [Trait("Category", "Fast")]
    [InlineData(0, ClrmdEvidenceStatus.Unavailable, EvaluationEvidenceStatus.Unavailable)]
    [InlineData(2, ClrmdEvidenceStatus.Partial, EvaluationEvidenceStatus.Partial)]
    public void Non_exact_integer_bytes_retain_evidence_without_claiming_an_answer(
        int bytesRead,
        ClrmdEvidenceStatus adapterStatus,
        EvaluationEvidenceStatus expectedEvidence)
    {
        var bytes = Enumerable.Range(1, bytesRead).Select(static value => (byte)value).ToArray();
        var memory = MemoryReadResult.Create("dump-sha256:fixture", 0x1020, sizeof(int), bytes);
        var field = new ClrmdInstanceFieldInfo(
            "Marker",
            metadataToken: 0x04000001,
            address: memory.Address,
            size: sizeof(int),
            isObjectReference: false,
            elementType: "Int32",
            fieldTypeName: "System.Int32");
        var observation = new ClrmdInt32FieldObservation(field, memory, value: null);
        var adapterResult = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            adapterStatus,
            ClrmdValueIssue.MemoryUnavailable,
            observation,
            ImmutableArray.Create(memory));

        var result = ProjectGenerically(adapterResult);

        Assert.Same(observation, adapterResult.Value);
        Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
        Assert.Equal(EvaluationCompleteness.None, result.Completeness);
        Assert.Equal(expectedEvidence, result.Evidence);
        Assert.Null(result.Value);
        var provenance = Assert.Single(result.Provenance);
        Assert.Equal(memory.SourceId, provenance.SourceId);
        Assert.Equal(memory.Address, provenance.Address);
        Assert.Equal(memory.RequestedLength, provenance.RequestedLength);
        Assert.Equal(memory.BytesRead, provenance.ObservedLength);
        Assert.Equal("DUMP_MEMORY_UNAVAILABLE", Assert.Single(result.Diagnostics).Code);
    }

    /// <summary>Checks that an exact four-byte integer remains a complete decoded observation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_integer_bytes_project_a_complete_decoded_answer()
    {
        var memory = MemoryReadResult.Create(
            "dump-sha256:fixture",
            0x2040,
            sizeof(int),
            new byte[] { 0x78, 0x56, 0x34, 0x12 });
        var field = new ClrmdInstanceFieldInfo(
            "Marker",
            metadataToken: 0x04000001,
            address: memory.Address,
            size: sizeof(int),
            isObjectReference: false,
            elementType: "Int32",
            fieldTypeName: "System.Int32");
        var observation = new ClrmdInt32FieldObservation(field, memory, 0x12345678);
        var adapterResult = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            observation,
            ImmutableArray.Create(memory));

        var result = adapterResult.ToObservationResult();

        Assert.Equal(EvaluationCompleteness.Complete, result.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Exact, result.Evidence);
        var projected = Assert.IsType<ClrmdInt32FieldObservation>(result.Value);
        Assert.Same(observation, projected);
        Assert.Equal(0x12345678, projected.Value);
        Assert.Empty(result.Diagnostics);
    }

    private static EvaluationResult<TValue> ProjectGenerically<TValue>(ClrmdEvidenceResult<TValue> result)
        where TValue : class =>
        result.ToObservationResult();
}
