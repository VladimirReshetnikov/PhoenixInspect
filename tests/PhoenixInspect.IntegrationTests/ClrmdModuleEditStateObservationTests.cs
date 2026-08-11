using System.Buffers.Binary;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the pure projection boundary of host module edit-state evidence.</summary>
public sealed class ClrmdModuleEditStateObservationTests
{
    /// <summary>
    /// Freezes the issue ordinals because product requests and results encode them into canonical replay identities.
    /// New issues must append rather than re-numbering an existing wire value.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Value_issue_ordinals_preserve_the_frozen_replay_vocabulary()
    {
        Assert.Equal(0, (int)ClrmdValueIssue.None);
        Assert.Equal(1, (int)ClrmdValueIssue.SnapshotMismatch);
        Assert.Equal(2, (int)ClrmdValueIssue.ModuleUnavailable);
        Assert.Equal(3, (int)ClrmdValueIssue.MetadataUnavailable);
        Assert.Equal(4, (int)ClrmdValueIssue.ArtifactUnavailable);
        Assert.Equal(5, (int)ClrmdValueIssue.ArtifactInvalid);
        Assert.Equal(6, (int)ClrmdValueIssue.RuntimeUnsupported);
        Assert.Equal(7, (int)ClrmdValueIssue.ObjectUnavailable);
        Assert.Equal(8, (int)ClrmdValueIssue.FieldUnavailable);
        Assert.Equal(9, (int)ClrmdValueIssue.TypeUnavailable);
        Assert.Equal(10, (int)ClrmdValueIssue.MethodUnavailable);
        Assert.Equal(11, (int)ClrmdValueIssue.AmbiguousMatch);
        Assert.Equal(12, (int)ClrmdValueIssue.MethodBodyUnavailable);
        Assert.Equal(13, (int)ClrmdValueIssue.MethodBodyLayoutUnsupported);
        Assert.Equal(14, (int)ClrmdValueIssue.MethodHeaderUnsupported);
        Assert.Equal(15, (int)ClrmdValueIssue.MethodSectionUnsupported);
        Assert.Equal(16, (int)ClrmdValueIssue.MethodIdentityMismatch);
        Assert.Equal(17, (int)ClrmdValueIssue.MemberShapeUnsupported);
        Assert.Equal(18, (int)ClrmdValueIssue.TypeMismatch);
        Assert.Equal(19, (int)ClrmdValueIssue.MemoryUnavailable);
        Assert.Equal(20, (int)ClrmdValueIssue.InvalidData);
        Assert.Equal(21, (int)ClrmdValueIssue.LimitExceeded);
        Assert.Equal(22, (int)ClrmdValueIssue.RuntimeContractUnavailable);
        Assert.Equal(23, (int)ClrmdValueIssue.EditGenerationCounterUnderflow);
    }

    /// <summary>Proves enablement gates the counter-minus-one applied-generation interpretation.</summary>
    [Theory]
    [InlineData(0x9019u, 2ul, true, 1ul, true)]
    [InlineData(0x9019u, 1ul, true, 0ul, false)]
    [InlineData(0x9011u, 0ul, false, 0ul, false)]
    [InlineData(0x9011u, ulong.MaxValue, false, 0ul, false)]
    [Trait("Category", "Fast")]
    public void Exact_projection_interprets_the_counter_only_under_enablement(
        uint flags,
        ulong generationCounter,
        bool isEditEnabled,
        ulong appliedGenerationCount,
        bool hasAppliedEdits)
    {
        var module = CreateModule();
        var flagsMemory = MemoryReadResult.Create(
            module.Snapshot.MemorySourceId,
            0x1100,
            sizeof(uint),
            LittleEndian(flags));
        var counterMemory = MemoryReadResult.Create(
            module.Snapshot.MemorySourceId,
            0x1200,
            sizeof(ulong),
            LittleEndian(generationCounter));

        var result = ClrmdModuleEditStateObservation.Project(
            module,
            sizeof(ulong),
            flagsMemory,
            counterMemory);

        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        Assert.Equal(ClrmdValueIssue.None, result.Issue);
        var observation = Assert.IsType<ClrmdModuleEditStateObservation>(result.Value);
        Assert.Equal(flags, observation.ModuleFlags);
        Assert.Equal(generationCounter, observation.GenerationCounter);
        Assert.Equal(isEditEnabled, observation.IsEditEnabled);
        Assert.Equal(appliedGenerationCount, observation.AppliedGenerationCount);
        Assert.Equal(hasAppliedEdits, observation.HasAppliedEdits);
        Assert.Same(flagsMemory, observation.ModuleFlagsMemory);
        Assert.Same(counterMemory, observation.GenerationCounterMemory);
        Assert.Equal(2, result.Evidence.Length);
        Assert.Same(flagsMemory, result.Evidence[0]);
        Assert.Same(counterMemory, result.Evidence[1]);
    }

    /// <summary>Proves an enabled zero counter is invalid and cannot expose decoded module facts.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Enabled_zero_counter_is_a_typed_invalid_observation()
    {
        var module = CreateModule();
        var flagsMemory = MemoryReadResult.Create(
            module.Snapshot.MemorySourceId,
            0x1100,
            sizeof(uint),
            LittleEndian(ClrmdModuleEditStateObservation.EditEnabledFlag));
        var counterMemory = MemoryReadResult.Create(
            module.Snapshot.MemorySourceId,
            0x1200,
            sizeof(ulong),
            LittleEndian(0ul));

        var result = ClrmdModuleEditStateObservation.Project(
            module,
            sizeof(ulong),
            flagsMemory,
            counterMemory);

        Assert.Equal(ClrmdEvidenceStatus.Invalid, result.Status);
        Assert.Equal(ClrmdValueIssue.EditGenerationCounterUnderflow, result.Issue);
        var observation = Assert.IsType<ClrmdModuleEditStateObservation>(result.Value);
        Assert.Equal(ClrmdEvidenceStatus.Invalid, observation.Status);
        Assert.Null(observation.ModuleFlags);
        Assert.Null(observation.GenerationCounter);
        Assert.Null(observation.IsEditEnabled);
        Assert.Null(observation.AppliedGenerationCount);
        Assert.Null(observation.HasAppliedEdits);
        Assert.Same(flagsMemory, observation.ModuleFlagsMemory);
        Assert.Same(counterMemory, observation.GenerationCounterMemory);
    }

    /// <summary>Proves a short generation-counter read remains unavailable raw evidence, never an unedited value.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Partial_counter_is_a_typed_unavailable_observation()
    {
        var module = CreateModule();
        var flagsMemory = MemoryReadResult.Create(
            module.Snapshot.MemorySourceId,
            0x1100,
            sizeof(uint),
            LittleEndian(0x9019u));
        var counterMemory = MemoryReadResult.Create(
            module.Snapshot.MemorySourceId,
            0x1200,
            sizeof(ulong),
            [1, 0, 0, 0]);

        var result = ClrmdModuleEditStateObservation.Project(
            module,
            sizeof(ulong),
            flagsMemory,
            counterMemory);

        Assert.Equal(ClrmdEvidenceStatus.Unavailable, result.Status);
        Assert.Equal(ClrmdValueIssue.MemoryUnavailable, result.Issue);
        var observation = Assert.IsType<ClrmdModuleEditStateObservation>(result.Value);
        Assert.Null(observation.ModuleFlags);
        Assert.Null(observation.GenerationCounter);
        Assert.Null(observation.HasAppliedEdits);
        Assert.Equal(MemoryReadStatus.Partial, observation.GenerationCounterMemory!.Status);
        Assert.Equal(2, result.Evidence.Length);
        Assert.Same(flagsMemory, result.Evidence[0]);
        Assert.Same(counterMemory, result.Evidence[1]);
    }

    /// <summary>Proves foreign raw evidence is invalid rather than decoded under the selected module identity.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Foreign_memory_source_is_invalid()
    {
        var module = CreateModule();
        var flagsMemory = MemoryReadResult.Create(
            $"dump-sha256:{new string('b', 64)}",
            0x1100,
            sizeof(uint),
            LittleEndian(0x9019u));
        var counterMemory = MemoryReadResult.Create(
            module.Snapshot.MemorySourceId,
            0x1200,
            sizeof(ulong),
            LittleEndian(1ul));

        var result = ClrmdModuleEditStateObservation.Project(
            module,
            sizeof(ulong),
            flagsMemory,
            counterMemory);

        Assert.Equal(ClrmdEvidenceStatus.Invalid, result.Status);
        Assert.Equal(ClrmdValueIssue.InvalidData, result.Issue);
        Assert.Null(result.Value!.HasAppliedEdits);
    }

    private static ClrmdRuntimeModuleIdentity CreateModule() =>
        new(
            new ClrmdSnapshotIdentity(new string('a', 64)),
            AppDomainAddress: 0x100,
            ModuleAddress: 0x1_000,
            ImageBase: 0x10_000,
            ImageSize: 0x1_000);

    private static byte[] LittleEndian(uint value)
    {
        var bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(ulong value)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }
}
