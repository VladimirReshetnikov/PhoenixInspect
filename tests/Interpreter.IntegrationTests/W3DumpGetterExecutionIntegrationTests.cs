using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Metadata.SRM;
using Xunit;
using MethodBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises W3's end-to-end path from counted dump metadata and object bytes through exact evidence import and IL
/// execution, with the local PE used only as a post-acquisition oracle.
/// </summary>
public sealed class W3DumpGetterExecutionIntegrationTests
{
    private const int ExpectedMarker = 0x13579BDF;
    private const string DirectGetterName = "GetMarker";
    private const string AdjustedGetterName = "GetAdjustedMarker";
    private const string DuplicatedGetterName = "GetDuplicatedMarker";

    /// <summary>
    /// Verifies that imported-object identity is deterministic, provenance-sensitive, and fixed-width before the
    /// concrete memory model applies its bounded evidence-key contract.
    /// </summary>
    [Fact]
    public void Imported_owner_evidence_identity_is_fixed_width_and_snapshot_scoped()
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('a', SHA256.HashSizeInBytes * 2));
        var runtimeModule = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: ulong.MaxValue,
            ModuleAddress: ulong.MaxValue - 1,
            ImageBase: ulong.MaxValue - 2,
            ImageSize: ulong.MaxValue - 3);

        var first = ClrmdExactInt32FieldExecutionEvidence.CreateOwnerEvidenceIdentity(
            runtimeModule,
            ownerAddress: ulong.MaxValue - 4,
            ownerMethodTable: ulong.MaxValue - 5);
        var repeated = ClrmdExactInt32FieldExecutionEvidence.CreateOwnerEvidenceIdentity(
            runtimeModule,
            ownerAddress: ulong.MaxValue - 4,
            ownerMethodTable: ulong.MaxValue - 5);
        var differentOwner = ClrmdExactInt32FieldExecutionEvidence.CreateOwnerEvidenceIdentity(
            runtimeModule,
            ownerAddress: ulong.MaxValue - 6,
            ownerMethodTable: ulong.MaxValue - 5);
        var differentSnapshot = ClrmdExactInt32FieldExecutionEvidence.CreateOwnerEvidenceIdentity(
            runtimeModule with
            {
                Snapshot = new ClrmdSnapshotIdentity(new string('b', SHA256.HashSizeInBytes * 2)),
            },
            ownerAddress: ulong.MaxValue - 4,
            ownerMethodTable: ulong.MaxValue - 5);

        Assert.Equal(first, repeated);
        Assert.NotEqual(first, differentOwner);
        Assert.NotEqual(first, differentSnapshot);
        Assert.Matches("^clrmd-imported-object:v1:sha256:[0-9a-f]{64}$", first);
        Assert.InRange(first.Length, 1, ImportedObjectEvidenceIdentity.MaximumLength);
        Assert.DoesNotContain(runtimeModule.SourceId, first, StringComparison.Ordinal);
        _ = new ImportedObjectEvidenceIdentity(first);
    }

    /// <summary>
    /// Verifies that immutable snapshot-scoped metadata descriptors and one exact imported field observation execute
    /// the real compiler-emitted <c>ldfld</c> getters deterministically, while partial field bytes fail before import.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Counted_dump_getters_execute_after_exact_metadata_and_field_correlation()
    {
        var targetExecutablePath = TestTargetPaths.ResolveExecutable();
        var targetAssemblyPath = TestTargetPaths.ResolveAssembly(targetExecutablePath);
        Assert.True(File.Exists(targetExecutablePath), $"Expected test target executable at '{targetExecutablePath}'.");
        Assert.True(File.Exists(targetAssemblyPath), $"Expected test target assembly at '{targetAssemblyPath}'.");

        var dumpPath = Path.Combine(Path.GetTempPath(), $"w3-dump-getter-{Guid.NewGuid():N}.dmp");
        try
        {
            using var target = TestTargetRunner.StartAndWaitReady(targetExecutablePath);
            DumpWriter.WriteFullDump(target.Pid, dumpPath);

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            Assert.Equal(ClrmdValueIssue.None, opened.Issue);
            using var session = opened.Value ??
                throw new InvalidOperationException("Exact dump-open result carried no session.");

            var module = Assert.Single(session.FindModulesByFileName("Interpreter.TestTarget.dll"));
            var metadataIdentity = session.ReadModuleContentIdentity(module);
            Assert.Equal(ClrmdEvidenceStatus.Exact, metadataIdentity.Status);
            Assert.Equal(ClrmdValueIssue.None, metadataIdentity.Issue);
            var metadataRead = Assert.Single(metadataIdentity.Evidence);
            AssertExactRead(
                metadataRead,
                session.Snapshot.MemorySourceId,
                module.MetadataAddress,
                checked((int)module.MetadataLength));

            var directBody = session.ReadMethodBody(module, "DumpProbe", DirectGetterName);
            var adjustedBody = session.ReadMethodBody(module, "DumpProbe", AdjustedGetterName);
            var duplicatedBody = session.ReadMethodBody(module, "DumpProbe", DuplicatedGetterName);
            AssertExactMethodBody(directBody, session.Snapshot);
            AssertExactMethodBody(adjustedBody, session.Snapshot);
            AssertExactMethodBody(duplicatedBody, session.Snapshot);

            var directResolver = AssertResolver(
                ClrmdDumpExecutionResolver.Create(module, metadataIdentity, directBody));
            var adjustedResolver = AssertResolver(
                ClrmdDumpExecutionResolver.Create(module, metadataIdentity, adjustedBody));
            var duplicatedResolver = AssertResolver(
                ClrmdDumpExecutionResolver.Create(module, metadataIdentity, duplicatedBody));

            var nonExactMethod = ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.MemoryUnavailable,
                directBody.Value,
                directBody.Evidence,
                directBody.AppliedBounds);
            var rejectedMethodResolver = ClrmdDumpExecutionResolver.Create(
                module,
                metadataIdentity,
                nonExactMethod);
            Assert.False(rejectedMethodResolver.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Unavailable, rejectedMethodResolver.Failure!.Kind);
            Assert.Equal("DUMP_EXEC_METHOD_NOT_EXACT", rejectedMethodResolver.Failure.Code);

            var directInfo = directBody.Value!;
            var conflictingNormalizedBody = MethodBody.Create(
                directInfo.MaxStack + 1,
                directInfo.Body.CodeBytes.AsSpan(),
                directInfo.LocalVariablesInitialized,
                directInfo.LocalSignatureToken,
                directInfo.ExceptionRegionCount);
            var conflictingBodyInfo = new ClrmdMethodBodyInfo(
                directInfo.MetadataToken,
                directInfo.RelativeVirtualAddress,
                directInfo.HeaderAddress,
                directInfo.HeaderKind,
                directInfo.HeaderEvidence,
                directInfo.CodeAddress,
                directInfo.Code,
                directInfo.ExtraSectionEvidence,
                conflictingNormalizedBody);
            var conflictingBody = ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                conflictingBodyInfo,
                directBody.Evidence,
                directBody.AppliedBounds);
            var rejectedConflictingBody = ClrmdDumpExecutionResolver.Create(
                module,
                metadataIdentity,
                conflictingBody);
            Assert.False(rejectedConflictingBody.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Conflict, rejectedConflictingBody.Failure!.Kind);
            Assert.Equal(
                "DUMP_EXEC_METHOD_PHYSICAL_REPLAY_CONFLICT",
                rejectedConflictingBody.Failure.Code);

            var expectedModuleHandle = ModuleHandle.FromRuntimeEvidenceIdentity(
                metadataIdentity.Value!,
                module.Identity.SourceId);
            Assert.Equal(expectedModuleHandle, directResolver.ModuleHandle);
            Assert.Equal(expectedModuleHandle, adjustedResolver.ModuleHandle);
            Assert.Equal(module.Identity, directResolver.Module.Identity);
            Assert.Equal(metadataIdentity.Value, directResolver.ContentIdentity);
            Assert.Same(directBody.Value!.Body, directResolver.MethodDefinition.Body);
            Assert.Same(adjustedBody.Value!.Body, adjustedResolver.MethodDefinition.Body);
            Assert.Equal(TypeSig.Int32, directResolver.MethodDefinition.Signature.ReturnType);
            Assert.True(directResolver.MethodDefinition.Signature.HasImplicitThis);
            Assert.Empty(directResolver.MethodDefinition.Signature.ParameterTypes);
            Assert.Empty(directResolver.MethodDefinition.Signature.LocalTypes);

            var directFieldToken = AssertGetterIl(directResolver.MethodDefinition.Body, adjusted: false);
            var adjustedFieldToken = AssertGetterIl(adjustedResolver.MethodDefinition.Body, adjusted: true);
            var duplicatedFieldToken = AssertDuplicatedGetterIl(duplicatedResolver.MethodDefinition.Body);
            Assert.Equal(directFieldToken, adjustedFieldToken);
            Assert.Equal(directFieldToken, duplicatedFieldToken);

            var objectSearch = session.FindStrongHandleObjectsByTypeName(
                "DumpProbe",
                maximumMatches: 2,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, objectSearch.Status);
            Assert.Equal(ClrmdValueIssue.None, objectSearch.Issue);
            var owner = Assert.Single(objectSearch.Matches);
            Assert.Equal(module.Identity, owner.Module.Identity);
            Assert.Equal(
                directResolver.MethodDefinition.Signature.DeclaringType.MetadataToken,
                owner.TypeMetadataToken);
            Assert.All(objectSearch.Evidence, read =>
                AssertExactRead(
                    read,
                    session.Snapshot.MemorySourceId,
                    read.Address,
                    read.RequestedLength));

            var invalidPointerEvidence = ImmutableArray.Create(
                MemoryReadResult.Create(
                    session.Snapshot.MemorySourceId,
                    owner.RootAddress,
                    owner.Evidence[0].RequestedLength,
                    new byte[owner.Evidence[0].RequestedLength]),
                MemoryReadResult.Create(
                    session.Snapshot.MemorySourceId,
                    owner.Address,
                    owner.Evidence[1].RequestedLength,
                    new byte[owner.Evidence[1].RequestedLength]));
            var invalidPointerOwner = new ClrmdHeapObjectInfo(
                owner.Snapshot,
                owner.Address,
                owner.TypeName,
                owner.TypeMetadataToken,
                owner.MethodTable,
                owner.RootAddress,
                owner.RootKind,
                owner.Module,
                invalidPointerEvidence);
            var invalidPointerSearch = ReplaceSearchOwner(objectSearch, invalidPointerOwner);

            var conflictingTypeOwner = new ClrmdHeapObjectInfo(
                owner.Snapshot,
                owner.Address,
                owner.TypeName,
                checked(owner.TypeMetadataToken + 1),
                owner.MethodTable,
                owner.RootAddress,
                owner.RootKind,
                owner.Module,
                owner.Evidence);
            var conflictingTypeSearch = ReplaceSearchOwner(objectSearch, conflictingTypeOwner);

            var markerObservation = session.ReadInt32Field(owner, "Marker");
            Assert.Equal(ClrmdEvidenceStatus.Exact, markerObservation.Status);
            Assert.Equal(ClrmdValueIssue.None, markerObservation.Issue);
            var observedMarker = markerObservation.Value ??
                throw new InvalidOperationException("Exact Int32 result carried no observation.");
            Assert.Equal(ExpectedMarker, observedMarker.Value);
            Assert.Equal(directFieldToken, observedMarker.Field.MetadataToken);
            Assert.Equal(owner.Address, observedMarker.Field.OwnerAddress);
            Assert.Equal(owner.MethodTable, observedMarker.Field.OwnerMethodTable);
            Assert.Equal(owner.TypeName, observedMarker.Field.OwnerTypeName);
            Assert.Equal("Int32", observedMarker.Field.ElementType);
            Assert.Equal("System.Int32", observedMarker.Field.FieldTypeName);
            AssertExactRead(
                observedMarker.Memory,
                session.Snapshot.MemorySourceId,
                observedMarker.Field.Address,
                sizeof(int));

            var directEvidence = AssertEvidence(
                directResolver.CorrelateExactInt32Field(objectSearch, markerObservation));
            var adjustedEvidence = AssertEvidence(
                adjustedResolver.CorrelateExactInt32Field(objectSearch, markerObservation));
            Assert.Equal(directFieldToken, directEvidence.Field.Handle.MetadataToken);
            Assert.Equal(directResolver.ModuleHandle, directEvidence.Field.Handle.Module);
            Assert.Equal(directResolver.MethodDefinition.Signature.DeclaringType, directEvidence.Field.DeclaringType);
            Assert.Equal(TypeSig.Int32, directEvidence.Field.FieldType);
            Assert.False(directEvidence.Field.IsStatic);
            Assert.False(directEvidence.Field.IsLiteral);
            Assert.False(directEvidence.Field.HasRva);
            Assert.Equal(directEvidence.Field, adjustedEvidence.Field);
            Assert.Equal(ExpectedMarker, directEvidence.Value);

            var rejectedDuplicatedGetter = duplicatedResolver.CorrelateExactInt32Field(
                objectSearch,
                markerObservation);
            Assert.False(rejectedDuplicatedGetter.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Conflict, rejectedDuplicatedGetter.Failure!.Kind);
            Assert.Equal(
                "DUMP_EXEC_FIELD_OPERAND_COUNT_CONFLICT",
                rejectedDuplicatedGetter.Failure.Code);

            var rejectedPointerEvidence = directResolver.CorrelateExactInt32Field(
                invalidPointerSearch,
                markerObservation);
            Assert.False(rejectedPointerEvidence.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Invalid, rejectedPointerEvidence.Failure!.Kind);
            Assert.Equal("DUMP_EXEC_OWNER_EVIDENCE_INVALID", rejectedPointerEvidence.Failure.Code);

            var rejectedRuntimeType = directResolver.CorrelateExactInt32Field(
                conflictingTypeSearch,
                markerObservation);
            Assert.False(rejectedRuntimeType.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Conflict, rejectedRuntimeType.Failure!.Kind);
            Assert.Equal("DUMP_EXEC_FIELD_METADATA_TYPE_CONFLICT", rejectedRuntimeType.Failure.Code);

            var alternateObservation = session.ReadInt32Field(owner, "AlternateMarker");
            Assert.Equal(ClrmdEvidenceStatus.Exact, alternateObservation.Status);
            Assert.NotEqual(directFieldToken, alternateObservation.Value!.Field.MetadataToken);
            var rejectedAlternateField = directResolver.CorrelateExactInt32Field(
                objectSearch,
                alternateObservation);
            Assert.False(rejectedAlternateField.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Conflict, rejectedAlternateField.Failure!.Kind);
            Assert.Equal("DUMP_EXEC_FIELD_OPERAND_CONFLICT", rejectedAlternateField.Failure.Code);

            var partialMemory = MemoryReadResult.Create(
                observedMarker.Memory.SourceId,
                observedMarker.Memory.Address,
                sizeof(int),
                observedMarker.Memory.Bytes.AsSpan(0, sizeof(int) - 1));
            var partialObservation = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.MemoryUnavailable,
                new ClrmdInt32FieldObservation(observedMarker.Field, partialMemory, value: null),
                ImmutableArray.Create(partialMemory),
                markerObservation.AppliedBounds);
            var rejectedPartial = directResolver.CorrelateExactInt32Field(objectSearch, partialObservation);
            Assert.False(rejectedPartial.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Unavailable, rejectedPartial.Failure!.Kind);
            Assert.Equal("DUMP_EXEC_FIELD_NOT_EXACT", rejectedPartial.Failure.Code);

            var missingImportRun = Execute(directResolver, directEvidence, importField: false);
            Assert.Equal(MachineRunStatus.Blocked, missingImportRun.Outcome.Status);
            Assert.Equal("MEMORY_IMPORTED_FIELD_UNAVAILABLE", missingImportRun.Outcome.Failure!.Code);
            Assert.False(missingImportRun.Outcome.State.ReturnValue.HasValue);
            Assert.Equal(1, missingImportRun.LoadFieldCount);
            AssertResolutionCounts(missingImportRun, methodDefinitions: 1, fields: 1);
            Assert.Equal(missingImportRun.InitialMemory, missingImportRun.Outcome.State.Memory);
            Assert.Equal(31, missingImportRun.Outcome.OperationalState.Budget.InstructionBudget);
            var missingImportEvent = Assert.Single(missingImportRun.Events);
            Assert.Equal(
                (DebugEventKind.InstructionExecuted, 0, "LoadArgument"),
                (missingImportEvent.Kind, missingImportEvent.IlOffset, missingImportEvent.Instruction));

            var sameMachineFixture = new GetterMachineFixture(directResolver, directEvidence, importField: true);
            var directRun = sameMachineFixture.Run();
            AssertCompletedInt32(directRun, ExpectedMarker);
            AssertResolutionCounts(directRun, methodDefinitions: 1, fields: 1);
            Assert.Equal(29, directRun.Outcome.OperationalState.Budget.InstructionBudget);
            AssertEvents(
                directRun,
                (DebugEventKind.InstructionExecuted, 0, "LoadArgument"),
                (DebugEventKind.InstructionExecuted, 1, "LoadField"),
                (DebugEventKind.InstructionExecuted, 6, "Return"),
                (DebugEventKind.FramePopped, 6, "Return"));
            var sameMachineReplay = sameMachineFixture.Run();
            AssertResolutionCounts(sameMachineReplay, methodDefinitions: 1, fields: 1);
            AssertEquivalent(directRun, sameMachineReplay);
            AssertCanonicalReplay(directRun, sameMachineReplay);

            var adjustedRun = Execute(adjustedResolver, adjustedEvidence, importField: true);
            AssertCompletedInt32(adjustedRun, unchecked(ExpectedMarker + 1));
            AssertResolutionCounts(adjustedRun, methodDefinitions: 1, fields: 1);
            Assert.Equal(27, adjustedRun.Outcome.OperationalState.Budget.InstructionBudget);
            AssertEvents(
                adjustedRun,
                (DebugEventKind.InstructionExecuted, 0, "LoadArgument"),
                (DebugEventKind.InstructionExecuted, 1, "LoadField"),
                (DebugEventKind.InstructionExecuted, 6, "LoadInt32"),
                (DebugEventKind.InstructionExecuted, 7, "Add"),
                (DebugEventKind.InstructionExecuted, 8, "Return"),
                (DebugEventKind.FramePopped, 8, "Return"));

            var nullRun = ExecuteTypedNull(directResolver);
            Assert.Equal(MachineRunStatus.Ready, nullRun.BeforeLoad.Status);
            var loadArgumentEvent = Assert.Single(nullRun.BeforeLoad.Events);
            Assert.Equal(DebugEventKind.InstructionExecuted, loadArgumentEvent.Kind);
            Assert.Equal(0, loadArgumentEvent.IlOffset);
            Assert.Equal(31, nullRun.BeforeLoad.OperationalState.Budget.InstructionBudget);
            Assert.Equal(MachineRunStatus.TargetException, nullRun.Outcome.Status);
            Assert.Null(nullRun.Outcome.Failure);
            Assert.Equal(TargetExceptionKind.NullReference, nullRun.Outcome.TargetException!.Kind);
            Assert.Equal(directResolver.Method, nullRun.Outcome.TargetException.Method);
            Assert.Equal(1, nullRun.Outcome.TargetException.IlOffset);
            var targetExceptionEvent = Assert.Single(nullRun.Outcome.Events);
            Assert.Equal(DebugEventKind.TargetExceptionRaised, targetExceptionEvent.Kind);
            Assert.Equal(1, targetExceptionEvent.IlOffset);
            Assert.Equal(30, nullRun.Outcome.OperationalState.Budget.InstructionBudget);
            Assert.Empty(nullRun.Outcome.State.CallStack);
            Assert.False(nullRun.Outcome.State.ReturnValue.HasValue);
            Assert.Equal(nullRun.Outcome.TargetException, nullRun.Outcome.State.TerminalTargetException);
            Assert.Same(nullRun.BeforeLoad.State.Memory, nullRun.Outcome.State.Memory);
            Assert.Equal(1, nullRun.LoadFieldCount);
            Assert.Equal(1, nullRun.GetMethodDefinitionCount);
            Assert.Equal(1, nullRun.ResolveFieldCount);
            Assert.Equal(MachineRunStatus.TargetException, nullRun.RepeatedOutcome.Status);
            Assert.Same(nullRun.Outcome.State, nullRun.RepeatedOutcome.State);
            Assert.Same(nullRun.Outcome.OperationalState, nullRun.RepeatedOutcome.OperationalState);
            Assert.Equal(nullRun.Outcome.TargetException, nullRun.RepeatedOutcome.TargetException);
            Assert.Empty(nullRun.RepeatedOutcome.Events);
            Assert.Null(nullRun.RepeatedOutcome.Failure);
            Assert.Equal(1, nullRun.LoadFieldCount);
            Assert.Equal(1, nullRun.GetMethodDefinitionCount);
            Assert.Equal(1, nullRun.ResolveFieldCount);

            var replayResolver = AssertResolver(
                ClrmdDumpExecutionResolver.Create(module, metadataIdentity, directBody));
            var replayEvidence = AssertEvidence(
                replayResolver.CorrelateExactInt32Field(objectSearch, markerObservation));
            var replayRun = Execute(replayResolver, replayEvidence, importField: true);
            AssertEquivalent(directRun, replayRun);
            AssertCanonicalReplay(directRun, replayRun);
            Assert.Equal(directResolver.Method, replayResolver.Method);
            Assert.Equal(directResolver.MethodDefinition.Signature, replayResolver.MethodDefinition.Signature);
            Assert.Equal(directResolver.ResolveField(directResolver.Method, directFieldToken).Value, replayEvidence.Field);
            Assert.Equal(directEvidence.OwnerEvidenceIdentity, replayEvidence.OwnerEvidenceIdentity);

            // The complete local PE is opened only after dump metadata, body bytes, runtime descriptors, field bytes,
            // and executable memory have all been fixed. It is an independent equality oracle, never resolver input.
            using var diskModule = SrmMetadataModule.LoadFromFile(targetAssemblyPath);
            var diskIdentityMatch = metadataIdentity.Value!.VerifyMatches(diskModule.Id.ContentIdentity);
            Assert.True(diskIdentityMatch.IsSuccess, diskIdentityMatch.Failure?.Code);
            Assert.NotEqual(diskModule.ModuleHandle, directResolver.ModuleHandle);

            var directOracle = ReadDiskOracle(diskModule, DirectGetterName, adjusted: false);
            var adjustedOracle = ReadDiskOracle(diskModule, AdjustedGetterName, adjusted: true);
            Assert.Equal(directOracle.MethodToken, directResolver.Method.MetadataToken);
            Assert.Equal(adjustedOracle.MethodToken, adjustedResolver.Method.MetadataToken);
            Assert.Equal(directOracle.FieldToken, directFieldToken);
            Assert.Equal(adjustedOracle.FieldToken, adjustedFieldToken);
            Assert.Equal(directOracle.Body.CodeBytes.ToArray(), directResolver.MethodDefinition.Body.CodeBytes.ToArray());
            Assert.Equal(adjustedOracle.Body.CodeBytes.ToArray(), adjustedResolver.MethodDefinition.Body.CodeBytes.ToArray());
            Assert.Equal(
                directOracle.Definition.Signature.DeclaringType.MetadataToken,
                directResolver.MethodDefinition.Signature.DeclaringType.MetadataToken);
            Assert.Equal(TypeSig.Int32, directOracle.Field.FieldType);
            Assert.Equal(directOracle.Field.Handle.MetadataToken, directEvidence.Field.Handle.MetadataToken);
            AssertCoreClrGetterOracle(targetAssemblyPath);

            session.Dispose();
            var reopened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, reopened.Status);
            Assert.Equal(ClrmdValueIssue.None, reopened.Issue);
            using var replaySession = reopened.Value ??
                throw new InvalidOperationException("Exact reopened dump result carried no session.");
            Assert.Equal(module.Identity.Snapshot, replaySession.Snapshot);

            var reboundModule = Assert.Single(
                replaySession.FindModulesByFileName("Interpreter.TestTarget.dll"));
            Assert.Equal(module.Identity, reboundModule.Identity);
            var reboundMetadata = replaySession.ReadModuleContentIdentity(reboundModule);
            Assert.Equal(ClrmdEvidenceStatus.Exact, reboundMetadata.Status);
            Assert.Equal(metadataIdentity.Value, reboundMetadata.Value);
            var reboundDirectBody = replaySession.ReadMethodBody(
                reboundModule,
                "DumpProbe",
                DirectGetterName);
            var reboundAdjustedBody = replaySession.ReadMethodBody(
                reboundModule,
                "DumpProbe",
                AdjustedGetterName);
            AssertExactMethodBody(reboundDirectBody, replaySession.Snapshot);
            AssertExactMethodBody(reboundAdjustedBody, replaySession.Snapshot);

            var reboundSearch = replaySession.FindStrongHandleObjectsByTypeName(
                "DumpProbe",
                maximumMatches: 2,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, reboundSearch.Status);
            var reboundOwner = Assert.Single(reboundSearch.Matches);
            Assert.Equal(owner.Address, reboundOwner.Address);
            Assert.Equal(owner.MethodTable, reboundOwner.MethodTable);
            Assert.Equal(owner.RootAddress, reboundOwner.RootAddress);
            var reboundObservation = replaySession.ReadInt32Field(reboundOwner, "Marker");
            Assert.Equal(ClrmdEvidenceStatus.Exact, reboundObservation.Status);
            Assert.Equal(ExpectedMarker, reboundObservation.Value!.Value);
            Assert.Equal(observedMarker.Field.MetadataToken, reboundObservation.Value.Field.MetadataToken);
            Assert.Equal(observedMarker.Field.Address, reboundObservation.Value.Field.Address);

            var reboundDirectResolver = AssertResolver(
                ClrmdDumpExecutionResolver.Create(reboundModule, reboundMetadata, reboundDirectBody));
            var reboundAdjustedResolver = AssertResolver(
                ClrmdDumpExecutionResolver.Create(reboundModule, reboundMetadata, reboundAdjustedBody));
            var reboundDirectEvidence = AssertEvidence(
                reboundDirectResolver.CorrelateExactInt32Field(reboundSearch, reboundObservation));
            var reboundAdjustedEvidence = AssertEvidence(
                reboundAdjustedResolver.CorrelateExactInt32Field(reboundSearch, reboundObservation));
            var reboundDirectRun = Execute(
                reboundDirectResolver,
                reboundDirectEvidence,
                importField: true);
            var reboundAdjustedRun = Execute(
                reboundAdjustedResolver,
                reboundAdjustedEvidence,
                importField: true);

            Assert.Equal(directResolver.ModuleHandle, reboundDirectResolver.ModuleHandle);
            Assert.Equal(directResolver.Method, reboundDirectResolver.Method);
            Assert.Equal(adjustedResolver.Method, reboundAdjustedResolver.Method);
            Assert.Equal(directEvidence.Field, reboundDirectEvidence.Field);
            Assert.Equal(directEvidence.OwnerEvidenceIdentity, reboundDirectEvidence.OwnerEvidenceIdentity);
            AssertEquivalent(directRun, reboundDirectRun);
            AssertEquivalent(adjustedRun, reboundAdjustedRun);
            AssertCanonicalReplay(directRun, reboundDirectRun);
            AssertCanonicalReplay(adjustedRun, reboundAdjustedRun);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static ClrmdDumpExecutionResolver AssertResolver(
        ResolutionResult<ClrmdDumpExecutionResolver> result)
    {
        Assert.True(result.IsSuccess, result.Failure?.Code);
        return result.Value;
    }

    private static ClrmdExactInt32FieldExecutionEvidence AssertEvidence(
        ResolutionResult<ClrmdExactInt32FieldExecutionEvidence> result)
    {
        Assert.True(result.IsSuccess, result.Failure?.Code);
        return result.Value;
    }

    private static ClrmdHeapObjectSearchResult ReplaceSearchOwner(
        ClrmdHeapObjectSearchResult original,
        ClrmdHeapObjectInfo replacement) =>
        new(
            original.Snapshot,
            original.TypeNameSelector,
            original.Status,
            original.Issue,
            original.HandlesScanned,
            original.MaximumHandlesScanned,
            original.MaximumMatches,
            original.MatchLimitReached,
            ImmutableArray.Create(replacement),
            replacement.Evidence);

    private static void AssertExactMethodBody(
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> result,
        ClrmdSnapshotIdentity snapshot)
    {
        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        Assert.Equal(ClrmdValueIssue.None, result.Issue);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Evidence);
        Assert.All(result.Evidence, read =>
            AssertExactRead(read, snapshot.MemorySourceId, read.Address, read.RequestedLength));
    }

    private static void AssertExactRead(
        MemoryReadResult read,
        string sourceId,
        ulong address,
        int requestedLength)
    {
        Assert.Equal(MemoryReadStatus.Exact, read.Status);
        Assert.Equal(sourceId, read.SourceId);
        Assert.Equal(address, read.Address);
        Assert.Equal(requestedLength, read.RequestedLength);
        Assert.Equal(requestedLength, read.BytesRead);
        Assert.Equal(0, read.MissingByteCount);
    }

    private static int AssertGetterIl(MethodBody body, bool adjusted)
    {
        var code = body.CodeBytes;
        Assert.Equal(adjusted ? 9 : 7, code.Length);
        Assert.Equal(0x02, code[0]);
        Assert.Equal(0x7B, code[1]);
        var fieldToken = BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(2, sizeof(int)));
        Assert.True(FieldHandle.IsValidMetadataToken(fieldToken));
        if (adjusted)
        {
            Assert.Equal(0x17, code[6]);
            Assert.Equal(0x58, code[7]);
            Assert.Equal(0x2A, code[8]);
        }
        else
        {
            Assert.Equal(0x2A, code[6]);
        }

        return fieldToken;
    }

    private static int AssertDuplicatedGetterIl(MethodBody body)
    {
        var code = body.CodeBytes;
        Assert.Equal(14, code.Length);
        Assert.Equal(0x02, code[0]);
        Assert.Equal(0x7B, code[1]);
        var firstFieldToken = BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(2, sizeof(int)));
        Assert.True(FieldHandle.IsValidMetadataToken(firstFieldToken));
        Assert.Equal(0x02, code[6]);
        Assert.Equal(0x7B, code[7]);
        var secondFieldToken = BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(8, sizeof(int)));
        Assert.Equal(firstFieldToken, secondFieldToken);
        Assert.Equal(0x58, code[12]);
        Assert.Equal(0x2A, code[13]);
        return firstFieldToken;
    }

    private static GetterRun Execute(
        ClrmdDumpExecutionResolver resolver,
        ClrmdExactInt32FieldExecutionEvidence evidence,
        bool importField) =>
        new GetterMachineFixture(resolver, evidence, importField).Run();

    private static NullGetterRun ExecuteTypedNull(ClrmdDumpExecutionResolver resolver)
    {
        var domain = new ConcreteDomain();
        var countingMemory = new CountingMemoryModel(new ConcreteMemoryModel(domain));
        var countingResolution = new CountingResolutionServices(resolver);
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            countingResolution,
            countingMemory,
            new InstructionBudgetPolicy());
        var activation = machine.ActivateRoot(
            resolver.Method,
            ImmutableArray.Create(domain.ConstNull(resolver.MethodDefinition.Signature.DeclaringType)),
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var operationalState = new MachineOperationalState(new BudgetState(32));
        var beforeLoad = machine.StepOne(activation.State!, operationalState);
        Assert.Equal(MachineRunStatus.Ready, beforeLoad.Status);
        var outcome = machine.StepOne(beforeLoad.State, beforeLoad.OperationalState);
        var repeatedOutcome = machine.StepOne(outcome.State, outcome.OperationalState);
        Assert.Equal(0, countingResolution.ResolveMethodCount);
        return new NullGetterRun(
            beforeLoad,
            outcome,
            repeatedOutcome,
            countingMemory.LoadFieldCount,
            countingResolution.GetMethodDefinitionCount,
            countingResolution.ResolveFieldCount);
    }

    private static void AssertCompletedInt32(GetterRun run, int expected)
    {
        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Null(run.Outcome.Failure);
        Assert.Null(run.Outcome.TargetException);
        Assert.Empty(run.Outcome.State.CallStack);
        Assert.True(run.Outcome.State.ReturnValue.HasValue);
        var domain = new ConcreteDomain();
        Assert.True(domain.TryGetConstInt32(run.Outcome.State.ReturnValue.Value, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(1, run.Outcome.State.Memory.ImportedObjectCount);
        Assert.Equal(1, run.LoadFieldCount);
        Assert.Equal(run.InitialMemory, run.Outcome.State.Memory);
    }

    private static void AssertResolutionCounts(
        GetterRun run,
        int methodDefinitions,
        int fields)
    {
        Assert.Equal(methodDefinitions, run.GetMethodDefinitionCount);
        Assert.Equal(fields, run.ResolveFieldCount);
    }

    private static void AssertEquivalent(GetterRun expected, GetterRun actual)
    {
        Assert.Equal(expected.Outcome.Status, actual.Outcome.Status);
        Assert.Equal(expected.Outcome.Failure, actual.Outcome.Failure);
        Assert.Equal(expected.Outcome.TargetException, actual.Outcome.TargetException);
        Assert.Equal(expected.Outcome.OperationalState, actual.Outcome.OperationalState);
        Assert.Equal(expected.InitialMemory, actual.InitialMemory);
        Assert.Equal(expected.Outcome.State.Memory, actual.Outcome.State.Memory);
        Assert.Equal(expected.LoadFieldCount, actual.LoadFieldCount);
        Assert.Equal(expected.GetMethodDefinitionCount, actual.GetMethodDefinitionCount);
        Assert.Equal(expected.ResolveFieldCount, actual.ResolveFieldCount);
        Assert.Equal(expected.MemoryProjection, actual.MemoryProjection);
        Assert.Equal(expected.Outcome.State.ReturnValue.HasValue, actual.Outcome.State.ReturnValue.HasValue);
        if (expected.Outcome.State.ReturnValue.HasValue)
        {
            Assert.Equal(expected.Outcome.State.ReturnValue.Value, actual.Outcome.State.ReturnValue.Value);
        }

        Assert.Equal(expected.Events.Length, actual.Events.Length);
        for (var index = 0; index < expected.Events.Length; index++)
        {
            Assert.Equal(expected.Events[index], actual.Events[index]);
        }
    }

    private static void AssertEvents(
        GetterRun run,
        params (DebugEventKind Kind, int IlOffset, string Instruction)[] expected)
    {
        Assert.Equal(expected.Length, run.Events.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Kind, run.Events[index].Kind);
            Assert.Equal(expected[index].IlOffset, run.Events[index].IlOffset);
            Assert.Equal(expected[index].Instruction, run.Events[index].Instruction);
        }
    }

    private static void AssertCanonicalReplay(GetterRun expected, GetterRun actual)
    {
        var expectedBytes = SerializeCanonical(expected);
        var actualBytes = SerializeCanonical(actual);
        Assert.Equal(expectedBytes, actualBytes);
        var expectedHash = Convert.ToHexString(SHA256.HashData(expectedBytes)).ToLowerInvariant();
        var actualHash = Convert.ToHexString(SHA256.HashData(actualBytes)).ToLowerInvariant();
        Assert.Matches("^[0-9a-f]{64}$", expectedHash);
        Assert.Equal(expectedHash, actualHash);
    }

    private static byte[] SerializeCanonical(GetterRun run)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("status", run.Outcome.Status.ToString());
            writer.WriteNumber("budget", run.Outcome.OperationalState.Budget.InstructionBudget);
            writer.WriteNumber("loadFieldCount", run.LoadFieldCount);
            writer.WriteStartObject("resolution");
            writer.WriteNumber("methodDefinitionCount", run.GetMethodDefinitionCount);
            writer.WriteNumber("fieldCount", run.ResolveFieldCount);
            writer.WriteEndObject();
            writer.WriteStartObject("memory");
            writer.WriteNumber("objectCount", run.Outcome.State.Memory.ObjectCount);
            writer.WriteNumber("importedObjectCount", run.Outcome.State.Memory.ImportedObjectCount);
            writer.WriteNumber("arrayCount", run.Outcome.State.Memory.ArrayCount);
            writer.WriteString("ownerEvidenceIdentity", run.MemoryProjection.OwnerEvidenceIdentity);
            WriteType(writer, "receiverType", run.MemoryProjection.ReceiverType);
            writer.WriteStartObject("field");
            writer.WriteNumber("moduleHigh", run.MemoryProjection.Field.Handle.Module.High);
            writer.WriteNumber("moduleLow", run.MemoryProjection.Field.Handle.Module.Low);
            writer.WriteNumber("metadataToken", run.MemoryProjection.Field.Handle.MetadataToken);
            WriteType(writer, "declaringType", run.MemoryProjection.Field.DeclaringType);
            WriteType(writer, "fieldType", run.MemoryProjection.Field.FieldType);
            writer.WriteBoolean("isStatic", run.MemoryProjection.Field.IsStatic);
            writer.WriteBoolean("isLiteral", run.MemoryProjection.Field.IsLiteral);
            writer.WriteBoolean("hasRva", run.MemoryProjection.Field.HasRva);
            writer.WriteBoolean("hasImportedValue", run.MemoryProjection.HasImportedField);
            if (run.MemoryProjection.HasImportedField)
            {
                writer.WriteNumber("int32", run.MemoryProjection.FieldValue);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartObject("return");
            writer.WriteBoolean("hasValue", run.Outcome.State.ReturnValue.HasValue);
            if (run.Outcome.State.ReturnValue.HasValue)
            {
                var value = run.Outcome.State.ReturnValue.Value;
                writer.WriteString("kind", value.Kind.ToString());
                writer.WriteNumber("moduleHigh", value.StaticType.Module?.High ?? 0);
                writer.WriteNumber("moduleLow", value.StaticType.Module?.Low ?? 0);
                writer.WriteNumber("typeToken", value.StaticType.MetadataToken);
                writer.WriteString("intrinsic", value.StaticType.IntrinsicKind?.ToString());
                if (new ConcreteDomain().TryGetConstInt32(value, out var integer))
                {
                    writer.WriteNumber("int32", integer);
                }
            }

            writer.WriteEndObject();
            writer.WriteStartArray("events");
            foreach (var item in run.Events)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", item.Kind.ToString());
                writer.WriteNumber("moduleHigh", item.Method.Module.High);
                writer.WriteNumber("moduleLow", item.Method.Module.Low);
                writer.WriteNumber("methodToken", item.Method.MetadataToken);
                writer.WriteNumber("ilOffset", item.IlOffset);
                writer.WriteString("instruction", item.Instruction);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteType(Utf8JsonWriter writer, string propertyName, TypeSig type)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteString("kind", type.Kind.ToString());
        writer.WriteString("intrinsic", type.IntrinsicKind?.ToString());
        writer.WriteNumber("moduleHigh", type.Module?.High ?? 0);
        writer.WriteNumber("moduleLow", type.Module?.Low ?? 0);
        writer.WriteNumber("metadataToken", type.MetadataToken);
        writer.WriteString("displayName", type.DisplayName);
        if (type.ElementType is { } elementType)
        {
            WriteType(writer, "elementType", elementType);
        }

        writer.WriteEndObject();
    }

    private static DiskGetterOracle ReadDiskOracle(
        SrmMetadataModule module,
        string methodName,
        bool adjusted)
    {
        var token = module.FindMethodDefinition("DumpProbe", methodName);
        Assert.True(token.IsSuccess, token.Failure?.Code);
        var method = module.GetMethodHandle(token.Value);
        Assert.True(method.IsSuccess, method.Failure?.Code);
        var definition = module.GetMethodDefinition(method.Value);
        Assert.True(definition.IsSuccess, definition.Failure?.Code);
        var fieldToken = AssertGetterIl(definition.Value.Body, adjusted);
        var field = module.ResolveField(method.Value, fieldToken);
        Assert.True(field.IsSuccess, field.Failure?.Code);
        return new DiskGetterOracle(
            token.Value,
            definition.Value.Body,
            definition.Value,
            fieldToken,
            field.Value);
    }

    private static void AssertCoreClrGetterOracle(string targetAssemblyPath)
    {
        var assembly = Assembly.LoadFile(Path.GetFullPath(targetAssemblyPath));
        var probeType = assembly.GetType("DumpProbe", throwOnError: true)!;
        var constructor = probeType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(int), typeof(string)],
            modifiers: null) ??
            throw new InvalidOperationException("The CoreCLR oracle could not find the DumpProbe constructor.");
        var probe = constructor.Invoke([ExpectedMarker, "coreclr-getter-oracle"]);
        var directGetter = probeType.GetMethod(
            DirectGetterName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("The CoreCLR oracle could not find the direct getter.");
        var adjustedGetter = probeType.GetMethod(
            AdjustedGetterName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("The CoreCLR oracle could not find the adjusted getter.");

        Assert.Equal(ExpectedMarker, directGetter.Invoke(probe, parameters: null));
        Assert.Equal(unchecked(ExpectedMarker + 1), adjustedGetter.Invoke(probe, parameters: null));

        var openGetterType = typeof(Func<,>).MakeGenericType(probeType, typeof(int));
        var openGetter = directGetter.CreateDelegate(openGetterType);
        var invocation = Assert.Throws<TargetInvocationException>(
            () => openGetter.DynamicInvoke(new object?[] { null }));
        Assert.IsType<NullReferenceException>(invocation.InnerException);
    }

    private sealed class GetterMachineFixture
    {
        private readonly ConcreteValue receiver;
        private readonly ConcreteMemory initialMemory;
        private readonly CountingMemoryModel countingMemory;
        private readonly CountingResolutionServices countingResolution;
        private readonly IlMachine<ConcreteValue, ConcreteMemory> machine;
        private readonly PreparedMemoryProjection memoryProjection;
        private readonly MethodHandle method;

        internal GetterMachineFixture(
            ClrmdDumpExecutionResolver resolver,
            ClrmdExactInt32FieldExecutionEvidence evidence,
            bool importField)
        {
            var domain = new ConcreteDomain();
            var concreteMemory = new ConcreteMemoryModel(domain);
            var evidenceIdentity = new ImportedObjectEvidenceIdentity(evidence.OwnerEvidenceIdentity);
            (receiver, var memory) = concreteMemory.ImportObject(
                ConcreteMemory.Empty,
                evidence.Method.Signature.DeclaringType,
                evidenceIdentity);
            Assert.True(concreteMemory.TryGetImportedObjectEvidenceIdentity(
                memory,
                receiver,
                out var retainedIdentity));
            Assert.Equal(evidenceIdentity, retainedIdentity);
            if (importField)
            {
                memory = concreteMemory.ImportField(
                    memory,
                    receiver,
                    evidence.Field,
                    domain.ConstInt32(evidence.Value));
            }

            initialMemory = memory;
            memoryProjection = new PreparedMemoryProjection(
                evidence.OwnerEvidenceIdentity,
                evidence.Method.Signature.DeclaringType,
                evidence.Field,
                importField,
                evidence.Value);
            countingMemory = new CountingMemoryModel(concreteMemory);
            countingResolution = new CountingResolutionServices(resolver);
            method = resolver.Method;
            machine = new IlMachine<ConcreteValue, ConcreteMemory>(
                domain,
                countingResolution,
                countingMemory,
                new InstructionBudgetPolicy());
        }

        internal GetterRun Run()
        {
            countingMemory.ResetLoadFieldCount();
            var activation = machine.ActivateRoot(
                method,
                ImmutableArray.Create(receiver),
                initialMemory);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);

            var state = activation.State!;
            var operationalState = new MachineOperationalState(new BudgetState(32));
            var events = ImmutableArray.CreateBuilder<DebugEvent>();
            for (var step = 0; step < 32; step++)
            {
                var outcome = machine.StepOne(state, operationalState);
                events.AddRange(outcome.Events);
                if (outcome.Status != MachineRunStatus.Ready)
                {
                    Assert.Equal(0, countingResolution.ResolveMethodCount);
                    return new GetterRun(
                        outcome,
                        events.ToImmutable(),
                        countingMemory.LoadFieldCount,
                        countingResolution.GetMethodDefinitionCount,
                        countingResolution.ResolveFieldCount,
                        initialMemory,
                        memoryProjection);
                }

                state = outcome.State;
                operationalState = outcome.OperationalState;
            }

            throw new InvalidOperationException("The compiler-emitted getter exceeded its deterministic step bound.");
        }
    }

    private sealed class CountingResolutionServices : IResolutionServices
    {
        private readonly IResolutionServices inner;

        internal CountingResolutionServices(IResolutionServices inner)
        {
            this.inner = inner;
        }

        internal int GetMethodDefinitionCount { get; private set; }

        internal int ResolveMethodCount { get; private set; }

        internal int ResolveFieldCount { get; private set; }

        ResolutionResult<ResolvedMethodDefinition> IResolutionServices.GetMethodDefinition(MethodHandle method)
        {
            GetMethodDefinitionCount++;
            return inner.GetMethodDefinition(method);
        }

        ResolutionResult<ResolvedMethodCallTarget> IResolutionServices.ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            ResolveMethodCount++;
            return inner.ResolveMethod(contextMethod, metadataToken);
        }

        ResolutionResult<ResolvedField> IResolutionServices.ResolveField(
            MethodHandle contextMethod,
            int metadataToken)
        {
            ResolveFieldCount++;
            return inner.ResolveField(contextMethod, metadataToken);
        }
    }

    private sealed class CountingMemoryModel : IMemoryModel<ConcreteValue, ConcreteMemory>
    {
        private readonly ConcreteMemoryModel inner;

        internal CountingMemoryModel(ConcreteMemoryModel inner)
        {
            this.inner = inner;
        }

        internal int LoadFieldCount { get; private set; }

        public bool CanAllocate => inner.CanAllocate;

        public (ConcreteValue objRef, ConcreteMemory mem) NewObject(ConcreteMemory mem, TypeSig type) =>
            inner.NewObject(mem, type);

        public (ConcreteValue arrRef, ConcreteMemory mem) NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ConcreteValue length) =>
            inner.NewArray(mem, elemType, length);

        public MemoryLoadResult<ConcreteValue> LoadField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field)
        {
            LoadFieldCount++;
            return inner.LoadField(mem, objRef, field);
        }

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field,
            ConcreteValue value) =>
            inner.StoreField(mem, objRef, field, value);

        public ConcreteValue LoadElement(
            ConcreteMemory mem,
            ConcreteValue arrRef,
            ConcreteValue index) =>
            inner.LoadElement(mem, arrRef, index);

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ConcreteValue arrRef,
            ConcreteValue index,
            ConcreteValue value) =>
            inner.StoreElement(mem, arrRef, index, value);

        internal void ResetLoadFieldCount() => LoadFieldCount = 0;
    }

    private sealed record GetterRun(
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        ImmutableArray<DebugEvent> Events,
        int LoadFieldCount,
        int GetMethodDefinitionCount,
        int ResolveFieldCount,
        ConcreteMemory InitialMemory,
        PreparedMemoryProjection MemoryProjection);

    private sealed record NullGetterRun(
        StepOutcome<ConcreteValue, ConcreteMemory> BeforeLoad,
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        StepOutcome<ConcreteValue, ConcreteMemory> RepeatedOutcome,
        int LoadFieldCount,
        int GetMethodDefinitionCount,
        int ResolveFieldCount);

    private sealed record PreparedMemoryProjection(
        string OwnerEvidenceIdentity,
        TypeSig ReceiverType,
        ResolvedField Field,
        bool HasImportedField,
        int FieldValue);

    private sealed record DiskGetterOracle(
        int MethodToken,
        MethodBody Body,
        ResolvedMethodDefinition Definition,
        int FieldToken,
        ResolvedField Field);
}
