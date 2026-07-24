using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

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
            new ClrmdSnapshotIdentity(new string('0', 64)),
            ownerAddress: 0x1000,
            ownerMethodTable: 0x2000,
            ownerTypeName: "Fixture",
            name: "Marker",
            metadataToken: 0x04000001,
            address: memory.Address,
            size: sizeof(int),
            isObjectReference: false,
            elementType: "Int32",
            fieldTypeName: "System.Int32",
            nullableInt32Layout: null);
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
            new ClrmdSnapshotIdentity(new string('0', 64)),
            ownerAddress: 0x2000,
            ownerMethodTable: 0x3000,
            ownerTypeName: "Fixture",
            name: "Marker",
            metadataToken: 0x04000001,
            address: memory.Address,
            size: sizeof(int),
            isObjectReference: false,
            elementType: "Int32",
            fieldTypeName: "System.Int32",
            nullableInt32Layout: null);
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

    /// <summary>
    /// Checks that an exact false nullable flag is a complete null answer and does not require a payload read.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_nullable_false_flag_projects_a_complete_null_without_payload_evidence()
    {
        var field = CreateNullableField();
        var flag = MemoryReadResult.Create(
            field.Snapshot.MemorySourceId,
            field.NullableInt32Layout!.HasValueAddress,
            sizeof(byte),
            new byte[] { 0 });
        var observation = new ClrmdNullableInt32FieldObservation(
            field,
            flag,
            valueMemory: null,
            hasValue: false,
            value: null);
        var adapterResult = ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            observation,
            ImmutableArray.Create(flag));

        var result = adapterResult.ToObservationResult();

        Assert.True(observation.IsNull);
        Assert.Null(observation.ValueMemory);
        Assert.Equal(EvaluationCompleteness.Complete, result.Completeness);
        Assert.Same(observation, result.Value);
        Assert.Single(result.Provenance);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    /// Checks that a true nullable flag plus partial payload remains explanatory evidence without a scalar answer.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Partial_nullable_payload_retains_wrapper_but_projects_no_answer()
    {
        var field = CreateNullableField();
        var layout = field.NullableInt32Layout!;
        var flag = MemoryReadResult.Create(
            field.Snapshot.MemorySourceId,
            layout.HasValueAddress,
            sizeof(byte),
            new byte[] { 1 });
        var payload = MemoryReadResult.Create(
            field.Snapshot.MemorySourceId,
            layout.ValueAddress,
            sizeof(int),
            new byte[] { 0x34, 0x12 });
        var observation = new ClrmdNullableInt32FieldObservation(
            field,
            flag,
            payload,
            hasValue: true,
            value: null);
        var adapterResult = ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Partial,
            ClrmdValueIssue.MemoryUnavailable,
            observation,
            ImmutableArray.Create(flag, payload));

        var result = adapterResult.ToObservationResult();

        Assert.Same(observation, adapterResult.Value);
        Assert.False(observation.IsNull);
        Assert.Equal(EvaluationCompleteness.None, result.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Partial, result.Evidence);
        Assert.Null(result.Value);
        Assert.Equal(2, result.Provenance.Length);
        Assert.Equal("DUMP_MEMORY_UNAVAILABLE", Assert.Single(result.Diagnostics).Code);
    }

    private static ClrmdInstanceFieldInfo CreateNullableField()
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('1', 64));
        return new ClrmdInstanceFieldInfo(
            snapshot,
            ownerAddress: 0x4000,
            ownerMethodTable: 0x5000,
            ownerTypeName: "Fixture",
            name: "OptionalMarker",
            metadataToken: 0x04000002,
            address: 0x4020,
            size: 8,
            isObjectReference: false,
            elementType: "Struct",
            fieldTypeName: "System.Nullable<System.Int32>",
            nullableInt32Layout: new ClrmdNullableInt32FieldLayout(
                HasValueMetadataToken: 0x04000001,
                HasValueAddress: 0x4020,
                HasValueSize: sizeof(byte),
                ValueMetadataToken: 0x04000002,
                ValueAddress: 0x4024,
                ValueSize: sizeof(int)));
    }

    private static EvaluationResult<TValue> ProjectGenerically<TValue>(ClrmdEvidenceResult<TValue> result)
        where TValue : class =>
        result.ToObservationResult();
}
