using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Metadata.SRM;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises the first real dump-memory evidence slice against a generated full process dump.
/// </summary>
public sealed partial class DumpMemoryEvidenceIntegrationTests
{
    private const int ExpectedMarker = 0x13579BDF;
    private const string ExpectedMessage = "dump-memory-evidence:\uD83D\uDE80 exact rooted string";

    /// <summary>
    /// Verifies stable runtime identity plus primitive, string, metadata, and IL bytes sourced from dump memory.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Full_dump_recovers_rooted_values_with_counted_memory_evidence()
    {
        var targetExecutablePath = TestTargetPaths.ResolveExecutable();
        var targetAssemblyPath = TestTargetPaths.ResolveAssembly(targetExecutablePath);
        Assert.True(File.Exists(targetExecutablePath), $"Expected test target executable at '{targetExecutablePath}'.");
        Assert.True(File.Exists(targetAssemblyPath), $"Expected test target assembly at '{targetAssemblyPath}'.");
        using var diskArtifactMetadata = SrmMetadataModule.LoadFromFile(targetAssemblyPath);
        var diskMethodTokenResult = diskArtifactMetadata.FindMethodDefinition("Program", "RetOnly");
        Assert.True(diskMethodTokenResult.IsSuccess, diskMethodTokenResult.Failure?.Code);
        var diskMethodToken = diskMethodTokenResult.Value;
        var methodHandleResult = diskArtifactMetadata.GetMethodHandle(diskMethodToken);
        Assert.True(methodHandleResult.IsSuccess, methodHandleResult.Failure?.Code);
        var methodHandle = methodHandleResult.Value;
        var diskMethodBodyResult = diskArtifactMetadata.GetMethodBody(methodHandle);
        Assert.True(diskMethodBodyResult.IsSuccess, diskMethodBodyResult.Failure?.Code);
        var diskMethodBody = diskMethodBodyResult.Value;
        Assert.Equal(new byte[] { 0x2A }, diskMethodBody.CodeBytes.ToArray());

        var dumpPath = Path.Combine(Path.GetTempPath(), $"dump-evidence-{Guid.NewGuid():N}.dmp");
        try
        {
            using var targetRunner = TestTargetRunner.StartAndWaitReady(targetExecutablePath);
            DumpWriter.WriteFullDump(targetRunner.Pid, dumpPath);

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            Assert.Equal(ClrmdValueIssue.None, opened.Issue);
            using var session = opened.Value ?? throw new InvalidOperationException("Exact dump-open result carried no session.");
            Assert.True(session.IsOfflineLocatorInstalled);
            Assert.True(session.IsBoundedDumpCachePolicyEnforced);
            Assert.Matches("^[0-9a-f]{64}$", session.Snapshot.Sha256);
            Assert.Equal(session.Snapshot.MemorySourceId, session.Memory.SourceId);
            Assert.True(session.Memory.PointerSize is sizeof(uint) or sizeof(ulong));

            var module = Assert.Single(session.FindModulesByFileName("Interpreter.TestTarget.dll"));
            Assert.Equal(session.Snapshot, module.Identity.Snapshot);
            Assert.NotEqual(0UL, module.Identity.AppDomainAddress);
            Assert.NotEqual(0UL, module.Identity.ModuleAddress);
            Assert.NotEqual(0UL, module.Identity.ImageBase);
            Assert.NotEqual(0UL, module.Identity.ImageSize);
            Assert.NotNull(module.TargetPathHint);
            Assert.EndsWith("Interpreter.TestTarget.dll", module.TargetPathHint, StringComparison.OrdinalIgnoreCase);

            Assert.NotEqual(0UL, module.MetadataAddress);
            Assert.InRange(module.MetadataLength, 4UL, (ulong)session.Memory.MaximumReadLength);
            var dumpContentIdentity = session.ReadModuleContentIdentity(module);
            Assert.Equal(ClrmdEvidenceStatus.Exact, dumpContentIdentity.Status);
            Assert.Equal(ClrmdValueIssue.None, dumpContentIdentity.Issue);
            Assert.NotNull(dumpContentIdentity.Value);
            var metadata = Assert.Single(dumpContentIdentity.Evidence);
            Assert.Equal(MemoryReadStatus.Exact, metadata.Status);
            Assert.Equal(session.Memory.SourceId, metadata.SourceId);
            Assert.Equal(new byte[] { 0x42, 0x53, 0x4A, 0x42 }, metadata.Bytes.AsSpan(0, 4).ToArray());
            Assert.Equal(diskArtifactMetadata.Id.Mvid, dumpContentIdentity.Value.Mvid);
            var artifactBinding = dumpContentIdentity.Value.VerifyMatches(diskArtifactMetadata.Id.ContentIdentity);
            Assert.True(artifactBinding.IsSuccess, artifactBinding.Failure?.Code);
            using var wrongArtifactMetadata = SrmMetadataModule.LoadFromFile(
                typeof(DumpMemoryEvidenceIntegrationTests).Assembly.Location);
            var wrongArtifactBinding = dumpContentIdentity.Value.VerifyMatches(
                wrongArtifactMetadata.Id.ContentIdentity);
            Assert.False(wrongArtifactBinding.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Conflict, wrongArtifactBinding.Failure!.Kind);
            Assert.Equal("MODULE_MVID_CONFLICT", wrongArtifactBinding.Failure.Code);
            var metadataObservation = dumpContentIdentity.ToObservationResult();
            Assert.Equal(EvaluationSemanticMode.Observation, metadataObservation.SemanticMode);
            Assert.Equal(EvaluationCompletionStatus.Completed, metadataObservation.Completion);
            Assert.Equal(EvaluationCompleteness.Complete, metadataObservation.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, metadataObservation.Evidence);
            Assert.Equal(EvaluationEffectStatus.None, metadataObservation.Effects);
            Assert.Single(metadataObservation.Provenance);
            Assert.Empty(metadataObservation.Diagnostics);
            Assert.Matches(
                "^[0-9a-f]{64}$",
                EvaluationResultReplay.ComputeSha256(
                    metadataObservation,
                    static identity => $"{identity.Mvid:N}:{identity.MetadataLength}:{identity.MetadataSha256}"));

            var cappedSearch = session.FindStrongHandleObjectsByTypeName(
                "DumpProbe",
                maximumMatches: 8,
                maximumHandlesScanned: 1);
            Assert.Equal(ClrmdEvidenceStatus.Partial, cappedSearch.Status);
            Assert.Equal(ClrmdValueIssue.LimitExceeded, cappedSearch.Issue);
            Assert.Equal(1, cappedSearch.HandlesScanned);
            Assert.InRange(cappedSearch.Matches.Length, 0, 1);

            var objectSearch = session.FindStrongHandleObjectsByTypeName(
                "DumpProbe",
                maximumMatches: 8,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, objectSearch.Status);
            Assert.Equal(ClrmdValueIssue.None, objectSearch.Issue);
            var probe = Assert.Single(objectSearch.Matches);
            Assert.Equal(session.Snapshot, probe.Snapshot);
            Assert.Equal(module.Identity, probe.Module.Identity);
            Assert.Equal("DumpProbe", probe.TypeName);
            Assert.NotEqual(0UL, probe.RootAddress);
            Assert.NotEqual(0UL, probe.Address);
            Assert.NotEqual(0UL, probe.MethodTable);
            Assert.Equal(2, probe.Evidence.Length);
            Assert.All(probe.Evidence, read => Assert.Equal(MemoryReadStatus.Exact, read.Status));
            Assert.Equal(probe.RootAddress, probe.Evidence[0].Address);
            Assert.Equal(probe.Address, probe.Evidence[1].Address);
            Assert.Equal(session.Memory.SourceId, probe.Evidence[0].SourceId);
            Assert.Equal(objectSearch.Evidence.ToArray(), probe.Evidence.ToArray());

            var rootSelectionBounds = ImmutableArray.Create(
                new EvaluationDeterministicBound(
                    "root-selection.maximum-handles",
                    objectSearch.MaximumHandlesScanned),
                new EvaluationDeterministicBound(
                    "root-selection.maximum-matches",
                    objectSearch.MaximumMatches));

            var markerQuery = DumpQueryEngine.Evaluate(
                session,
                "root.Marker",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationSemanticMode.DerivedQuery, markerQuery.SemanticMode);
            Assert.Equal(EvaluationCompletionStatus.Completed, markerQuery.Completion);
            Assert.Equal(EvaluationCompleteness.Complete, markerQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, markerQuery.Evidence);
            Assert.Equal(EvaluationEffectStatus.None, markerQuery.Effects);
            Assert.Equal(DumpQueryValueKind.Int32, markerQuery.Value!.Kind);
            Assert.Equal(ExpectedMarker, markerQuery.Value.Int32Value);
            Assert.Empty(markerQuery.Diagnostics);
            Assert.Equal(EvaluationProvenanceKind.DumpMemory, markerQuery.Provenance[0].Kind);
            Assert.Contains(markerQuery.Provenance, item => item.Kind == EvaluationProvenanceKind.RuntimeStructure);
            Assert.DoesNotContain(ExpectedMarker.ToString(), markerQuery.Value.ToString(), StringComparison.Ordinal);
            var markerFingerprint = EvaluationResultReplay.ComputeSha256(
                markerQuery,
                static value => value.ToCanonicalReplayProjection());
            Assert.Equal(
                markerFingerprint,
                EvaluationResultReplay.ComputeSha256(
                    DumpQueryEngine.Evaluate(session, "root.Marker", "root", probe, rootSelectionBounds),
                    static value => value.ToCanonicalReplayProjection()));

            var messageQuery = DumpQueryEngine.Evaluate(
                session,
                "root.Message",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Completed, messageQuery.Completion);
            Assert.Equal(EvaluationCompleteness.Complete, messageQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, messageQuery.Evidence);
            Assert.Equal(DumpQueryValueKind.String, messageQuery.Value!.Kind);
            Assert.Equal(ExpectedMessage, messageQuery.Value.StringValue);
            Assert.DoesNotContain(ExpectedMessage, messageQuery.Value.ToString(), StringComparison.Ordinal);

            var coalescedQuery = DumpQueryEngine.Evaluate(
                session,
                "root.OptionalMessage ?? \"<missing>\"",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Completed, coalescedQuery.Completion);
            Assert.Equal(EvaluationCompleteness.Complete, coalescedQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, coalescedQuery.Evidence);
            Assert.Equal("<missing>", coalescedQuery.Value!.StringValue);
            Assert.Equal(EvaluationProvenanceKind.Transformation, coalescedQuery.Provenance[^1].Kind);

            var nullQuery = DumpQueryEngine.Evaluate(
                session,
                "root.OptionalMessage",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Completed, nullQuery.Completion);
            Assert.Equal(EvaluationCompleteness.Complete, nullQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, nullQuery.Evidence);
            Assert.Equal(DumpQueryValueKind.Null, nullQuery.Value!.Kind);

            var partialStringQuery = DumpQueryEngine.Evaluate(
                session,
                "root.LongMessage ?? \"must-not-mask-partial-evidence\"",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Completed, partialStringQuery.Completion);
            Assert.Equal(EvaluationCompleteness.Partial, partialStringQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Partial, partialStringQuery.Evidence);
            Assert.Equal(DumpQueryValueKind.String, partialStringQuery.Value!.Kind);
            Assert.Equal(4096, partialStringQuery.Value.StringValue!.Length);
            Assert.All(partialStringQuery.Value.StringValue, character => Assert.Equal('x', character));
            Assert.DoesNotContain(
                partialStringQuery.Provenance,
                item => item.Kind == EvaluationProvenanceKind.Transformation);
            Assert.Equal("DUMP_LIMIT_EXCEEDED", Assert.Single(partialStringQuery.Diagnostics).Code);

            var absentQuery = DumpQueryEngine.Evaluate(
                session,
                "root.AbsentField ?? \"must-not-mask-missing-evidence\"",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Blocked, absentQuery.Completion);
            Assert.Equal(EvaluationCompleteness.None, absentQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Unavailable, absentQuery.Evidence);
            Assert.Null(absentQuery.Value);
            Assert.Equal("DUMP_FIELD_UNAVAILABLE", Assert.Single(absentQuery.Diagnostics).Code);

            var caseSensitiveQuery = DumpQueryEngine.Evaluate(
                session,
                "root.marker",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Blocked, caseSensitiveQuery.Completion);
            Assert.Equal(EvaluationEvidenceStatus.Unavailable, caseSensitiveQuery.Evidence);
            Assert.Equal("DUMP_FIELD_UNAVAILABLE", Assert.Single(caseSensitiveQuery.Diagnostics).Code);

            var missingRootQuery = DumpQueryEngine.Evaluate(session, "root.Marker", "root", root: null);
            Assert.Equal(EvaluationCompletionStatus.Blocked, missingRootQuery.Completion);
            Assert.Equal(EvaluationCompleteness.None, missingRootQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Unavailable, missingRootQuery.Evidence);
            Assert.Equal("QUERY_ROOT_UNAVAILABLE", Assert.Single(missingRootQuery.Diagnostics).Code);

            var unsupportedTypeQuery = DumpQueryEngine.Evaluate(
                session,
                "root.Enabled",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Blocked, unsupportedTypeQuery.Completion);
            Assert.Equal(EvaluationCompleteness.None, unsupportedTypeQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, unsupportedTypeQuery.Evidence);
            Assert.Equal("QUERY_FIELD_TYPE_UNSUPPORTED", Assert.Single(unsupportedTypeQuery.Diagnostics).Code);

            var invalidCoalesceQuery = DumpQueryEngine.Evaluate(
                session,
                "root.Marker ?? 0",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Invalid, invalidCoalesceQuery.Completion);
            Assert.Equal(EvaluationCompleteness.None, invalidCoalesceQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, invalidCoalesceQuery.Evidence);
            Assert.Equal("QUERY_COALESCE_TYPE_UNSUPPORTED", Assert.Single(invalidCoalesceQuery.Diagnostics).Code);

            var nullConditionalQuery = DumpQueryEngine.Evaluate(
                session,
                "root?.Message",
                "root",
                probe,
                rootSelectionBounds);
            Assert.Equal(EvaluationCompletionStatus.Invalid, nullConditionalQuery.Completion);
            Assert.Equal(EvaluationCompleteness.None, nullConditionalQuery.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, nullConditionalQuery.Evidence);
            Assert.Null(nullConditionalQuery.Value);
            Assert.Equal(EvaluationProvenanceKind.Policy, Assert.Single(nullConditionalQuery.Provenance).Kind);
            Assert.Equal("QUERY_SYNTAX_UNSUPPORTED", Assert.Single(nullConditionalQuery.Diagnostics).Code);

            AssertDumpQueryContext(markerQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(messageQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(coalescedQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(nullQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(partialStringQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(absentQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(caseSensitiveQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(unsupportedTypeQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(invalidCoalesceQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(nullConditionalQuery, session, module.Identity.SourceId, rootSelectionBounds);
            AssertDumpQueryContext(
                missingRootQuery,
                session,
                expectedModuleSourceId: null,
                ImmutableArray<EvaluationDeterministicBound>.Empty);
            Assert.Throws<ArgumentException>(() => DumpQueryEngine.Evaluate(
                session,
                "root.Marker",
                "root",
                probe,
                ImmutableArray.Create(new EvaluationDeterministicBound(
                    "query.expression.characters",
                    1))));

            var marker = session.ReadInt32Field(probe, "Marker");
            Assert.Equal(ClrmdEvidenceStatus.Exact, marker.Status);
            Assert.Equal(ClrmdValueIssue.None, marker.Issue);
            Assert.NotNull(marker.Value);
            Assert.Equal(ExpectedMarker, marker.Value.Value);
            Assert.Equal(MemoryReadStatus.Exact, marker.Value.Memory.Status);
            Assert.Equal(sizeof(int), marker.Value.Memory.BytesRead);
            Assert.Equal(session.Memory.SourceId, marker.Value.Memory.SourceId);
            Assert.Equal(marker.Value.Field.Address, marker.Value.Memory.Address);

            var absentField = session.GetInstanceField(probe, "AbsentField");
            Assert.Equal(ClrmdEvidenceStatus.Unavailable, absentField.Status);
            Assert.Equal(ClrmdValueIssue.FieldUnavailable, absentField.Issue);
            Assert.False(absentField.HasValue);

            var typeConflict = session.ReadInt32Field(probe, "Message");
            Assert.Equal(ClrmdEvidenceStatus.Conflict, typeConflict.Status);
            Assert.Equal(ClrmdValueIssue.TypeMismatch, typeConflict.Issue);

            var message = session.ReadStringField(probe, "Message", maximumCharacters: 1024);
            Assert.Equal(ClrmdEvidenceStatus.Exact, message.Status);
            Assert.Equal(ClrmdValueIssue.None, message.Issue);
            Assert.False(message.IsNull);
            Assert.Equal(ExpectedMessage, message.Value);
            Assert.Equal(ExpectedMessage.Length, message.TargetLength);
            Assert.NotEmpty(message.Evidence);
            Assert.All(message.Evidence, read =>
            {
                Assert.Equal(MemoryReadStatus.Exact, read.Status);
                Assert.Equal(session.Memory.SourceId, read.SourceId);
            });

            var boundedMessage = session.ReadStringField(probe, "Message", maximumCharacters: 8);
            Assert.Equal(ClrmdEvidenceStatus.Partial, boundedMessage.Status);
            Assert.Equal(ClrmdValueIssue.LimitExceeded, boundedMessage.Issue);
            Assert.Equal("dump-mem", boundedMessage.Value);
            Assert.Equal(ExpectedMessage.Length, boundedMessage.TargetLength);

            var surrogatePrefix = session.ReadStringField(probe, "Message", maximumCharacters: 22);
            Assert.Equal(ClrmdEvidenceStatus.Partial, surrogatePrefix.Status);
            Assert.Equal(ClrmdValueIssue.LimitExceeded, surrogatePrefix.Issue);
            Assert.Equal(ExpectedMessage[..22], surrogatePrefix.Value);
            Assert.Equal('\uD83D', surrogatePrefix.Value![^1]);

            var nullMessage = session.ReadStringField(probe, "OptionalMessage", maximumCharacters: 32);
            Assert.Equal(ClrmdEvidenceStatus.Exact, nullMessage.Status);
            Assert.Equal(ClrmdValueIssue.None, nullMessage.Issue);
            Assert.True(nullMessage.IsNull);
            Assert.Null(nullMessage.Value);

            var methodBody = session.ReadMethodBody(module, "Program", "RetOnly");
            Assert.Equal(ClrmdEvidenceStatus.Exact, methodBody.Status);
            Assert.Equal(ClrmdValueIssue.None, methodBody.Issue);
            var dumpMethod = methodBody.Value ??
                throw new InvalidOperationException("Exact dump method-body result carried no value.");
            Assert.Equal(diskMethodToken, dumpMethod.MetadataToken);
            Assert.True(dumpMethod.RelativeVirtualAddress > 0);
            Assert.Equal(
                module.Identity.ImageBase + (ulong)dumpMethod.RelativeVirtualAddress,
                dumpMethod.HeaderAddress);
            Assert.Equal(ClrmdMethodHeaderKind.Tiny, dumpMethod.HeaderKind);
            var methodHeader = Assert.Single(dumpMethod.HeaderEvidence);
            Assert.Equal(MemoryReadStatus.Exact, methodHeader.Status);
            Assert.Equal(session.Memory.SourceId, methodHeader.SourceId);
            Assert.Equal(dumpMethod.HeaderAddress, methodHeader.Address);
            Assert.Equal(new byte[] { 0x06 }, methodHeader.Bytes.ToArray());
            Assert.Equal(dumpMethod.HeaderAddress + 1, dumpMethod.CodeAddress);
            Assert.Equal(MemoryReadStatus.Exact, dumpMethod.Code.Status);
            Assert.Equal(session.Memory.SourceId, dumpMethod.Code.SourceId);
            Assert.Equal(new byte[] { 0x2A }, dumpMethod.Code.Bytes.ToArray());
            Assert.Empty(dumpMethod.ExtraSectionEvidence);
            Assert.All(methodBody.Evidence, read =>
            {
                Assert.Equal(MemoryReadStatus.Exact, read.Status);
                Assert.Equal(session.Memory.SourceId, read.SourceId);
            });

            var dumpBackedMethodBody = dumpMethod.Body;
            Assert.Equal(diskMethodBody.MaxStack, dumpBackedMethodBody.MaxStack);
            Assert.Equal(diskMethodBody.CodeBytes.ToArray(), dumpBackedMethodBody.CodeBytes.ToArray());
            Assert.Equal(
                diskMethodBody.LocalVariablesInitialized,
                dumpBackedMethodBody.LocalVariablesInitialized);
            Assert.Equal(diskMethodBody.LocalSignatureToken, dumpBackedMethodBody.LocalSignatureToken);
            Assert.Equal(diskMethodBody.ExceptionRegionCount, dumpBackedMethodBody.ExceptionRegionCount);

            AssertFatMethodBodyEvidence(session, module, targetAssemblyPath);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.ReadMethodBody(module, new string('T', 4_097), "RetOnly"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.ReadMethodBody(module, "Program", new string('M', 1_025)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.FindModulesByFileName(new string('M', 1_025)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.FindStrongHandleObjectsByTypeName(new string('T', 4_097), 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.GetInstanceField(probe, new string('F', 1_025)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.ReadStringField(probe, new string('F', 1_025), maximumCharacters: 1));

            var domain = new ConcreteDomain();
            var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
                domain,
                new SingleMethodResolver(methodHandle, dumpBackedMethodBody),
                new InstructionBudgetPolicy());
            var frame = new FrameState<ConcreteValue>(
                Method: methodHandle,
                IlOffset: 0,
                Arguments: ImmutableArray<ConcreteValue>.Empty,
                Locals: ImmutableArray<ConcreteValue>.Empty,
                EvalStack: ImmutableArray<ConcreteValue>.Empty,
                ReturnsValue: false);
            var state = MachineState<ConcreteValue, ConcreteMemory>.Create(frame, ConcreteMemory.Empty);
            var operationalState = new MachineOperationalState(
                new BudgetState(InstructionBudget: 4));
            var outcome = machine.StepOne(state, operationalState);
            Assert.Equal(MachineRunStatus.Completed, outcome.Status);
            Assert.Null(outcome.Failure);
            Assert.Empty(outcome.State.CallStack);
            Assert.False(outcome.State.ReturnValue.HasValue);
            Assert.Equal(3, outcome.OperationalState.Budget.InstructionBudget);
            Assert.Collection(
                outcome.Events,
                item => Assert.Equal(DebugEventKind.InstructionExecuted, item.Kind),
                item => Assert.Equal(DebugEventKind.FramePopped, item.Kind));

            var absentMethod = session.ReadMethodBody(module, "Program", "AbsentMethod");
            Assert.Equal(ClrmdEvidenceStatus.Unavailable, absentMethod.Status);
            Assert.Equal(ClrmdValueIssue.MethodUnavailable, absentMethod.Issue);

            var unavailable = session.Memory.Read(ulong.MaxValue - 15, 16);
            Assert.Equal(MemoryReadStatus.Unavailable, unavailable.Status);
            Assert.Empty(unavailable.Bytes);
            Assert.Equal(16, unavailable.MissingByteCount);

            var sessionOwnedMemory = session.Memory;
            session.Dispose();
            Assert.Equal(
                module.Identity,
                Assert.Single(session.FindModulesByFileName("Interpreter.TestTarget.dll")).Identity);
            Assert.Throws<ObjectDisposedException>(() => sessionOwnedMemory.Read(module.MetadataAddress, 1));
            session.Dispose();

            var trustedDacPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscordaccore.dll");
            Assert.True(File.Exists(trustedDacPath), "The supported Windows runner must carry its matching DAC.");
            var brokeredStream = new FileStream(dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var brokeredOpen = ClrmdDumpSession.OpenBrokered(
                brokeredStream,
                "inherited-dump",
                trustedDacPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, brokeredOpen.Status);
            using var brokeredSession = brokeredOpen.Value
                ?? throw new InvalidOperationException("Exact brokered dump-open result carried no session.");
            Assert.True(brokeredSession.UsesExplicitDac);
            Assert.True(brokeredSession.IsOfflineLocatorInstalled);
            Assert.Equal(session.Snapshot, brokeredSession.Snapshot);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    /// <summary>
    /// Verifies exact, partial, unavailable, and zero-length classification without zero-filled missing bytes.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Memory_read_results_preserve_only_observed_prefix_bytes()
    {
        var exact = MemoryReadResult.Create("fixture", 0x1000, 2, new byte[] { 0x12, 0x34 });
        var partial = MemoryReadResult.Create("fixture", 0x2000, 4, new byte[] { 0x56, 0x78 });
        var unavailable = MemoryReadResult.Create("fixture", 0x3000, 4, ReadOnlySpan<byte>.Empty);
        var empty = MemoryReadResult.Create("fixture", ulong.MaxValue, 0, ReadOnlySpan<byte>.Empty);

        Assert.Equal(MemoryReadStatus.Exact, exact.Status);
        Assert.Equal(new byte[] { 0x12, 0x34 }, exact.Bytes.ToArray());

        Assert.Equal(MemoryReadStatus.Partial, partial.Status);
        Assert.Equal(2, partial.BytesRead);
        Assert.Equal(2, partial.MissingByteCount);
        Assert.Equal(new byte[] { 0x56, 0x78 }, partial.Bytes.ToArray());

        Assert.Equal(MemoryReadStatus.Unavailable, unavailable.Status);
        Assert.Empty(unavailable.Bytes);
        Assert.Equal(4, unavailable.MissingByteCount);

        Assert.Equal(MemoryReadStatus.Exact, empty.Status);
        Assert.Empty(empty.Bytes);
    }

    /// <summary>Verifies that missing and malformed external dumps produce typed open results rather than exceptions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void External_dump_open_classifies_missing_and_malformed_artifacts()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-dump-{Guid.NewGuid():N}.dmp");
        var missing = ClrmdDumpSession.Open(missingPath);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, missing.Status);
        Assert.Equal(ClrmdValueIssue.ArtifactUnavailable, missing.Issue);
        Assert.False(missing.HasValue);
        var missingObservation = missing.ToObservationResult();
        Assert.Equal(EvaluationSemanticMode.Observation, missingObservation.SemanticMode);
        Assert.Equal(EvaluationCompletionStatus.Completed, missingObservation.Completion);
        Assert.Equal(EvaluationCompleteness.None, missingObservation.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Unavailable, missingObservation.Evidence);
        Assert.Equal("DUMP_ARTIFACT_UNAVAILABLE", Assert.Single(missingObservation.Diagnostics).Code);

        var oversizedPath = Path.Combine(Path.GetTempPath(), $"oversized-dump-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var oversizedStream = new FileStream(oversizedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                oversizedStream.SetLength((8L * 1_024 * 1_024 * 1_024) + 1);
            }

            var oversized = ClrmdDumpSession.Open(oversizedPath);
            Assert.Equal(ClrmdEvidenceStatus.Unavailable, oversized.Status);
            Assert.Equal(ClrmdValueIssue.LimitExceeded, oversized.Issue);
            Assert.False(oversized.HasValue);
            Assert.ThrowsAny<NotSupportedException>(() => ClrmdDumpSession.Load(oversizedPath));
        }
        finally
        {
            File.Delete(oversizedPath);
        }

        var malformedPath = Path.Combine(Path.GetTempPath(), $"malformed-dump-{Guid.NewGuid():N}.dmp");
        try
        {
            File.WriteAllBytes(malformedPath, "not a process dump"u8.ToArray());
            var malformed = ClrmdDumpSession.Open(malformedPath);
            Assert.Equal(ClrmdEvidenceStatus.Invalid, malformed.Status);
            Assert.Equal(ClrmdValueIssue.ArtifactInvalid, malformed.Issue);
            Assert.False(malformed.HasValue);
            var malformedObservation = malformed.ToObservationResult();
            Assert.Equal(EvaluationCompleteness.None, malformedObservation.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Invalid, malformedObservation.Evidence);
            Assert.Equal("DUMP_ARTIFACT_INVALID", Assert.Single(malformedObservation.Diagnostics).Code);
        }
        finally
        {
            File.Delete(malformedPath);
        }
    }

    private static void AssertDumpQueryContext(
        EvaluationResult<DumpQueryValue> result,
        ClrmdDumpSession session,
        string? expectedModuleSourceId,
        ImmutableArray<EvaluationDeterministicBound> upstreamBounds)
    {
        Assert.Equal(EvaluationEvidenceSourceKind.DumpSnapshot, result.Context.SourceKind);
        Assert.Equal(EvaluationIdentityAvailability.Available, result.Context.Snapshot.Availability);
        Assert.Equal(session.Snapshot.MemorySourceId, result.Context.Snapshot.SourceId);
        if (expectedModuleSourceId is null)
        {
            Assert.Equal(EvaluationIdentityAvailability.Unavailable, result.Context.Module.Availability);
            Assert.Null(result.Context.Module.SourceId);
        }
        else
        {
            Assert.Equal(EvaluationIdentityAvailability.Available, result.Context.Module.Availability);
            Assert.Equal(expectedModuleSourceId, result.Context.Module.SourceId);
        }

        Assert.Equal(EvaluationFallbackStatus.None, result.Context.Fallback.Status);
        Assert.Equal("none", result.Context.Fallback.Name);

        var expectedBounds = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["dump.memory-read.bytes"] = session.Memory.MaximumReadLength,
            ["query.expression.characters"] = 512,
            ["query.field-name.characters"] = 64,
            ["query.observed-string.characters"] = 4096,
            ["query.root-name.characters"] = 64,
            ["query.string-literal.characters"] = 256,
        };
        foreach (var bound in upstreamBounds)
        {
            expectedBounds.Add(bound.Name, bound.Value);
        }

        Assert.Equal(
            expectedBounds.Keys.Order(StringComparer.Ordinal),
            result.Context.Bounds.Select(static bound => bound.Name));
        Assert.All(
            result.Context.Bounds,
            bound => Assert.Equal(expectedBounds[bound.Name], bound.Value));
    }

    private sealed class SingleMethodResolver : IResolutionServices
    {
        private readonly MethodHandle _method;
        private readonly MethodBody _body;

        public SingleMethodResolver(MethodHandle method, MethodBody body)
        {
            _method = method;
            _body = body;
        }

        public ResolutionResult<MethodBody> GetMethodBody(MethodHandle method) => method == _method
            ? ResolutionResult<MethodBody>.Success(_body)
            : ResolutionResult<MethodBody>.Failed(
                ResolutionFailureKind.Invalid,
                "TEST_METHOD_MISMATCH",
                "The dump-backed fixture resolver accepts exactly one content-identified method.");
    }
}
