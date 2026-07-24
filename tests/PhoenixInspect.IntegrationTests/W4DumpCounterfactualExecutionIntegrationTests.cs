using System.Collections.Immutable;
using System.Reflection;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Metadata.SRM;
using PhoenixInspect.Product.DumpDebugging;
using Xunit;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Closes W4's generated-dump product seam across exact, degraded, interpreted, modeled, detached, and reopened
/// execution without using the disk PE as acquisition input.
/// </summary>
public sealed class W4DumpCounterfactualExecutionIntegrationTests
{
    private const string TargetModuleName = "PhoenixInspect.TestTarget.dll";
    private const string ProbeTypeName = "DumpProbe";
    private const string RootMethodName = "GetMarkerSummary";
    private const string HelperMethodName = "CombineMarkers";
    private const string MarkerFieldName = "Marker";
    private const string AlternateMarkerFieldName = "AlternateMarker";
    private const int ExpectedMarker = 0x13579BDF;
    private const int ExpectedAlternateMarker = 0x13579BDE;
    private const int ExpectedSummary = 0x26AF37BD;
    private static readonly PureCallModelVersion Version = new(1, 0, 0);

    /// <summary>
    /// Generates one full dump, executes six detached product rows, reopens and rebinds every row, requires exact
    /// canonical replay, and consults CoreCLR and disk metadata only after dump-grounded execution is fixed.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W4GeneratedDumpExecutionV1")]
    public void Generated_dump_executes_six_detached_rows_and_replays_after_reopen()
    {
        var targetExecutablePath = TestTargetPaths.ResolveExecutable();
        var targetAssemblyPath = TestTargetPaths.ResolveAssembly(targetExecutablePath);
        Assert.True(File.Exists(targetExecutablePath));
        Assert.True(File.Exists(targetAssemblyPath));
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w4-counterfactual-{Guid.NewGuid():N}.dmp");

        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(targetExecutablePath))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            DumpAcquisition firstAcquisition;
            using (var firstSession = OpenExact(dumpPath))
            {
                firstAcquisition = Acquire(firstSession);
            }

            var firstExecutions = ExecuteAll(firstAcquisition);

            DumpAcquisition replayAcquisition;
            using (var reopenedSession = OpenExact(dumpPath))
            {
                Assert.Equal(firstAcquisition.Snapshot, reopenedSession.Snapshot);
                replayAcquisition = Acquire(reopenedSession);
            }

            var replayExecutions = ExecuteAll(replayAcquisition);

            Assert.Equal(firstAcquisition.Snapshot, replayAcquisition.Snapshot);
            Assert.Equal(firstAcquisition.RuntimeModule, replayAcquisition.RuntimeModule);
            Assert.Equal(firstAcquisition.MetadataIdentity, replayAcquisition.MetadataIdentity);
            Assert.Equal(firstAcquisition.RootMethod, replayAcquisition.RootMethod);
            Assert.Equal(firstAcquisition.HelperMethod, replayAcquisition.HelperMethod);
            Assert.True(firstAcquisition.RootCode.AsSpan().SequenceEqual(replayAcquisition.RootCode.AsSpan()));
            Assert.True(firstAcquisition.HelperCode.AsSpan().SequenceEqual(replayAcquisition.HelperCode.AsSpan()));
            Assert.Equal(6, firstExecutions.Length);
            Assert.Equal(6, replayExecutions.Length);
            for (var index = 0; index < firstExecutions.Length; index++)
            {
                AssertCanonicalReplay(firstExecutions[index], replayExecutions[index]);
            }

            // The complete PE and an executing CoreCLR object are deliberately late independent oracles. Neither
            // participates in resolver construction, field binding, preparation, or execution above.
            AssertLateDiskOracle(targetAssemblyPath, firstAcquisition);
            Assert.Equal(ExpectedSummary, ReadCoreClrOracle(targetAssemblyPath));
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static DumpAcquisition Acquire(ClrmdDumpSession session)
    {
        var module = Assert.Single(session.FindModulesByFileName(TargetModuleName));
        var metadataIdentity = session.ReadModuleContentIdentity(module);
        Assert.Equal(ClrmdEvidenceStatus.Exact, metadataIdentity.Status);
        Assert.Equal(ClrmdValueIssue.None, metadataIdentity.Issue);
        Assert.NotNull(metadataIdentity.Value);
        Assert.Single(metadataIdentity.Evidence);

        var rootBody = session.ReadMethodBody(module, ProbeTypeName, RootMethodName);
        var helperBody = session.ReadMethodBody(module, ProbeTypeName, HelperMethodName);
        AssertExactBody(rootBody, session.Snapshot);
        AssertExactBody(helperBody, session.Snapshot);
        var interpretedResolver = AssertSuccess(ClrmdDumpExecutionResolver.CreateMethodGraph(
            module,
            metadataIdentity,
            rootBody,
            ImmutableArray.Create(helperBody)));
        var modeledResolver = AssertSuccess(ClrmdDumpExecutionResolver.Create(
            module,
            metadataIdentity,
            rootBody));
        Assert.Equal(interpretedResolver.RootMethod, modeledResolver.RootMethod);
        var helperMethod = new MethodHandle(interpretedResolver.ModuleHandle, helperBody.Value!.MetadataToken);
        Assert.Equal(helperMethod, AssertSuccess(
            interpretedResolver.GetMethodDefinition(helperMethod)).Method);
        AssertFailure(
            modeledResolver.GetMethodDefinition(helperMethod),
            ResolutionFailureKind.Unavailable,
            "DUMP_EXEC_METHOD_BODY_UNAVAILABLE");

        var ownerSearch = session.FindStrongHandleObjectsByTypeName(
            ProbeTypeName,
            maximumMatches: 2,
            maximumHandlesScanned: 100_000);
        Assert.Equal(ClrmdEvidenceStatus.Exact, ownerSearch.Status);
        Assert.Equal(ClrmdValueIssue.None, ownerSearch.Issue);
        var owner = Assert.Single(ownerSearch.Matches);
        Assert.Equal(module.Identity, owner.Module.Identity);
        Assert.Equal(
            interpretedResolver.RootMethodDefinition.Signature.DeclaringType.MetadataToken,
            owner.TypeMetadataToken);

        var exactMarker = session.ReadInt32Field(owner, MarkerFieldName);
        var exactAlternate = session.ReadInt32Field(owner, AlternateMarkerFieldName);
        AssertExactField(exactMarker, ExpectedMarker, session.Snapshot);
        AssertExactField(exactAlternate, ExpectedAlternateMarker, session.Snapshot);
        var partialMarker = ReadDegradedView(
            session.Memory,
            exactMarker,
            ClrmdEvidenceStatus.Partial);
        var unavailableMarker = ReadDegradedView(
            session.Memory,
            exactMarker,
            ClrmdEvidenceStatus.Unavailable);

        var rows = ImmutableArray.CreateBuilder<DumpLaneBinding>(6);
        AddRows(interpretedResolver, modeled: false);
        AddRows(modeledResolver, modeled: true);
        return new DumpAcquisition(
            session.Snapshot,
            module.Identity,
            metadataIdentity.Value!,
            interpretedResolver.RootMethod,
            helperMethod,
            rootBody.Value!.Body.CodeBytes,
            helperBody.Value!.Body.CodeBytes,
            rows.MoveToImmutable());

        void AddRows(ClrmdDumpExecutionResolver resolver, bool modeled)
        {
            var alternateEvidence = AssertSuccess(resolver.CorrelateInt32FieldObservation(
                ownerSearch,
                exactAlternate));
            AddRow("exact", exactMarker, alternateEvidence);
            AddRow("partial", partialMarker, alternateEvidence);
            AddRow("unavailable", unavailableMarker, alternateEvidence);

            void AddRow(
                string evidenceName,
                ClrmdEvidenceResult<ClrmdInt32FieldObservation> markerObservation,
                ClrmdInt32FieldExecutionEvidence alternateEvidence)
            {
                var markerEvidence = AssertSuccess(resolver.CorrelateInt32FieldObservation(
                    ownerSearch,
                    markerObservation));
                var registry = modeled ? new SumModelRegistry() : null;
                var binding = CounterfactualDumpExecutionBinder.Bind(
                    resolver,
                    ImmutableArray.Create(alternateEvidence, markerEvidence),
                    "policy.counterfactual.dump",
                    Version,
                    instructionLimit: 100,
                    logicalDepthLimit: 2,
                    traversalLimit: 10,
                    "catalog.counterfactual.dump",
                    Version,
                    modeled ? helperMethod : null,
                    ["assume.read-only", "assume.counterfactual-not-historical"],
                    registry);
                rows.Add(new DumpLaneBinding(
                    $"{(modeled ? "modeled" : "interpreted")}-{evidenceName}",
                    modeled,
                    markerEvidence.Status,
                    binding));
            }
        }
    }

    private static ImmutableArray<DumpLaneExecution> ExecuteAll(DumpAcquisition acquisition)
    {
        var results = ImmutableArray.CreateBuilder<DumpLaneExecution>(acquisition.Lanes.Length);
        foreach (var lane in acquisition.Lanes)
        {
            var runner = new CounterfactualMethodRunner<CounterfactualDumpMemory>();
            var preparation = runner.Prepare(lane.Binding.Candidate);
            Assert.True(preparation.IsSuccess, preparation.Failure?.Diagnostics[0].Code);
            var plan = preparation.Plan!;
            var result = runner.Run(plan);
            AssertLane(lane, plan, result);
            results.Add(new DumpLaneExecution(lane, plan, result));
        }

        return results.MoveToImmutable();
    }

    private static void AssertLane(
        DumpLaneBinding lane,
        CounterfactualMethodPlan<CounterfactualDumpMemory> plan,
        CounterfactualExecutionResult result)
    {
        var expectedEvidence = lane.MarkerStatus switch
        {
            ClrmdEvidenceStatus.Exact => EvaluationEvidenceStatus.Exact,
            ClrmdEvidenceStatus.Partial => EvaluationEvidenceStatus.Partial,
            ClrmdEvidenceStatus.Unavailable => EvaluationEvidenceStatus.Unavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(lane)),
        };
        Assert.Equal(EvaluationEvidenceSourceKind.DumpSnapshot, plan.Request.EvidenceSource);
        Assert.Equal(lane.Binding.RootSelectionId, plan.Request.RootSelectionId);
        Assert.Equal(lane.Binding.RootEvidenceSha256, plan.Request.RootEvidenceSha256);
        Assert.Equal(lane.Binding.Memory.RootEvidenceSha256, plan.Request.Receiver.EvidenceSha256);
        Assert.Equal(2, plan.FieldObservations.Length);
        Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
        Assert.Equal(
            lane.MarkerStatus == ClrmdEvidenceStatus.Exact
                ? EvaluationCompleteness.Complete
                : EvaluationCompleteness.Partial,
            result.Completeness);
        Assert.Equal(expectedEvidence, result.Evidence);
        Assert.Equal(EvaluationEffectStatus.None, result.Effects);
        Assert.Equal(lane.Modeled ? 6 : 10, result.Context.Accounting.InstructionUsed);
        Assert.Equal(2, result.Context.Accounting.ObservedLogicalDepthHighWater);
        Assert.Equal(lane.Modeled ? 1 : 2, result.Context.Accounting.ActiveFrameDepthHighWater);
        Assert.True(result.Context.ReachedFieldLoadOrdinals.SequenceEqual([0, 1]));
        Assert.Equal(lane.Modeled ? 1 : 0, result.Context.ModelInvocationCount);
        Assert.Equal(lane.Modeled ? 1 : 0, result.Context.CompletedModeledCallCount);
        Assert.True(result.IsDeterministicReplay);
        Assert.Empty(result.Diagnostics);
        if (lane.MarkerStatus == ClrmdEvidenceStatus.Exact)
        {
            Assert.Equal(CounterfactualExecutionValueKind.ExactReturn, result.Value!.Kind);
            Assert.Equal(ExpectedSummary, result.Value.ExactInt32);
            Assert.Null(result.Value.Lineage);
        }
        else
        {
            Assert.Equal(CounterfactualExecutionValueKind.UnknownReturn, result.Value!.Kind);
            Assert.Null(result.Value.ExactInt32);
            Assert.NotNull(result.Value.Lineage);
            Assert.Equal(
                result.Value.Lineage!.Nodes.Length,
                result.Context.Accounting.LineageNodeCount);
        }

        Assert.Equal(result.Sha256, CounterfactualExecutionCanonicalCodec.ComputeSha256(result));
        Assert.True(result.CanonicalBytes.AsSpan().SequenceEqual(
            CounterfactualExecutionCanonicalCodec.SerializeCanonical(result).AsSpan()));
    }

    private static void AssertCanonicalReplay(
        DumpLaneExecution first,
        DumpLaneExecution replay)
    {
        Assert.Equal(first.Lane.Name, replay.Lane.Name);
        Assert.Equal(first.Lane.Modeled, replay.Lane.Modeled);
        Assert.Equal(first.Lane.MarkerStatus, replay.Lane.MarkerStatus);
        Assert.Equal(first.Lane.Binding.RootSelectionId, replay.Lane.Binding.RootSelectionId);
        Assert.Equal(first.Lane.Binding.RootEvidenceSha256, replay.Lane.Binding.RootEvidenceSha256);
        Assert.Equal(first.Lane.Binding.Memory.Sha256, replay.Lane.Binding.Memory.Sha256);
        Assert.True(first.Lane.Binding.Memory.CanonicalBytes.AsSpan().SequenceEqual(
            replay.Lane.Binding.Memory.CanonicalBytes.AsSpan()));
        Assert.Equal(first.Plan.Request.Sha256, replay.Plan.Request.Sha256);
        Assert.True(first.Plan.Request.CanonicalBytes.AsSpan().SequenceEqual(
            replay.Plan.Request.CanonicalBytes.AsSpan()));
        Assert.Equal(first.Plan.Sha256, replay.Plan.Sha256);
        Assert.True(first.Plan.CanonicalBytes.AsSpan().SequenceEqual(replay.Plan.CanonicalBytes.AsSpan()));
        Assert.Equal(first.Result.Sha256, replay.Result.Sha256);
        Assert.True(first.Result.CanonicalBytes.AsSpan().SequenceEqual(replay.Result.CanonicalBytes.AsSpan()));
    }

    private static ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadDegradedView(
        IProcessMemoryReader memory,
        ClrmdEvidenceResult<ClrmdInt32FieldObservation> exact,
        ClrmdEvidenceStatus status)
    {
        Assert.Equal(ClrmdEvidenceStatus.Exact, exact.Status);
        var exactObservation = exact.Value ??
            throw new InvalidOperationException("Exact field evidence carried no observation.");
        var prefixLength = status switch
        {
            ClrmdEvidenceStatus.Partial => 2,
            ClrmdEvidenceStatus.Unavailable => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        var view = new PrefixReadView(
            memory,
            exactObservation.Field.Address,
            sizeof(int),
            prefixLength);
        var read = view.Read(exactObservation.Field.Address, sizeof(int));
        Assert.True(view.Consumed);
        Assert.Equal(
            status == ClrmdEvidenceStatus.Partial ? MemoryReadStatus.Partial : MemoryReadStatus.Unavailable,
            read.Status);
        return ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            status,
            ClrmdValueIssue.MemoryUnavailable,
            new ClrmdInt32FieldObservation(exactObservation.Field, read, value: null),
            ImmutableArray.Create(read),
            exact.AppliedBounds);
    }

    private static void AssertLateDiskOracle(string assemblyPath, DumpAcquisition acquisition)
    {
        using var module = SrmMetadataModule.LoadFromFile(assemblyPath);
        var rootToken = AssertSuccess(module.FindMethodDefinition(ProbeTypeName, RootMethodName));
        var helperToken = AssertSuccess(module.FindMethodDefinition(ProbeTypeName, HelperMethodName));
        var root = AssertSuccess(module.GetMethodDefinition(AssertSuccess(module.GetMethodHandle(rootToken))));
        var helper = AssertSuccess(module.GetMethodDefinition(AssertSuccess(module.GetMethodHandle(helperToken))));
        Assert.Equal(acquisition.RootMethod.MetadataToken, root.Method.MetadataToken);
        Assert.Equal(acquisition.HelperMethod.MetadataToken, helper.Method.MetadataToken);
        Assert.True(acquisition.RootCode.AsSpan().SequenceEqual(root.Body.CodeBytes.AsSpan()));
        Assert.True(acquisition.HelperCode.AsSpan().SequenceEqual(helper.Body.CodeBytes.AsSpan()));
        Assert.True(acquisition.MetadataIdentity.VerifyMatches(module.Id.ContentIdentity).IsSuccess);
    }

    private static int ReadCoreClrOracle(string assemblyPath)
    {
        var probeType = Assembly.LoadFile(Path.GetFullPath(assemblyPath))
            .GetType(ProbeTypeName, throwOnError: true)!;
        var constructor = probeType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(int), typeof(string)],
            modifiers: null) ?? throw new InvalidOperationException("Could not find the DumpProbe constructor.");
        var probe = constructor.Invoke([ExpectedMarker, "w4-generated-dump-coreclr-oracle"]);
        var method = probeType.GetMethod(
            RootMethodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Could not find the W4 root method.");
        return Assert.IsType<int>(method.Invoke(probe, parameters: null));
    }

    private static ClrmdDumpSession OpenExact(string dumpPath)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        Assert.Equal(ClrmdValueIssue.None, opened.Issue);
        return opened.Value ?? throw new InvalidOperationException("Exact dump open carried no session.");
    }

    private static void AssertExactBody(
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> body,
        ClrmdSnapshotIdentity snapshot)
    {
        Assert.Equal(ClrmdEvidenceStatus.Exact, body.Status);
        Assert.Equal(ClrmdValueIssue.None, body.Issue);
        Assert.NotNull(body.Value);
        Assert.NotEmpty(body.Evidence);
        Assert.All(body.Evidence, read =>
        {
            Assert.Equal(snapshot.MemorySourceId, read.SourceId);
            Assert.Equal(MemoryReadStatus.Exact, read.Status);
            Assert.Equal(read.RequestedLength, read.BytesRead);
        });
    }

    private static void AssertExactField(
        ClrmdEvidenceResult<ClrmdInt32FieldObservation> field,
        int expected,
        ClrmdSnapshotIdentity snapshot)
    {
        Assert.Equal(ClrmdEvidenceStatus.Exact, field.Status);
        Assert.Equal(ClrmdValueIssue.None, field.Issue);
        var observation = field.Value ?? throw new InvalidOperationException("Exact field carried no observation.");
        Assert.Equal(expected, observation.Value);
        Assert.Equal(snapshot.MemorySourceId, observation.Memory.SourceId);
        Assert.Equal(MemoryReadStatus.Exact, observation.Memory.Status);
        Assert.Equal(sizeof(int), observation.Memory.BytesRead);
    }

    private static T AssertSuccess<T>(ResolutionResult<T> result)
    {
        Assert.True(result.IsSuccess, result.Failure?.Code);
        return result.Value;
    }

    private static void AssertFailure<T>(
        ResolutionResult<T> result,
        ResolutionFailureKind kind,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(kind, result.Failure!.Kind);
        Assert.Equal(code, result.Failure.Code);
    }

    private sealed record DumpAcquisition(
        ClrmdSnapshotIdentity Snapshot,
        ClrmdRuntimeModuleIdentity RuntimeModule,
        ModuleContentIdentity MetadataIdentity,
        MethodHandle RootMethod,
        MethodHandle HelperMethod,
        ImmutableArray<byte> RootCode,
        ImmutableArray<byte> HelperCode,
        ImmutableArray<DumpLaneBinding> Lanes);

    private sealed record DumpLaneBinding(
        string Name,
        bool Modeled,
        ClrmdEvidenceStatus MarkerStatus,
        CounterfactualDumpExecutionBinding Binding);

    private sealed record DumpLaneExecution(
        DumpLaneBinding Lane,
        CounterfactualMethodPlan<CounterfactualDumpMemory> Plan,
        CounterfactualExecutionResult Result);

    private sealed class PrefixReadView : IProcessMemoryReader
    {
        private readonly IProcessMemoryReader inner;
        private readonly ulong expectedAddress;
        private readonly int expectedLength;
        private readonly int prefixLength;

        internal PrefixReadView(
            IProcessMemoryReader inner,
            ulong expectedAddress,
            int expectedLength,
            int prefixLength)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (expectedLength <= 0 || prefixLength < 0 || prefixLength >= expectedLength)
            {
                throw new ArgumentOutOfRangeException(nameof(prefixLength));
            }

            this.expectedAddress = expectedAddress;
            this.expectedLength = expectedLength;
            this.prefixLength = prefixLength;
        }

        public int PointerSize => inner.PointerSize;

        public int MaximumReadLength => inner.MaximumReadLength;

        public string SourceId => inner.SourceId;

        internal bool Consumed { get; private set; }

        public MemoryReadResult Read(ulong address, int length)
        {
            if (Consumed || address != expectedAddress || length != expectedLength)
            {
                throw new InvalidDataException("The degraded view received an unexpected or repeated field read.");
            }

            var exact = inner.Read(address, length);
            if (exact.Status != MemoryReadStatus.Exact || exact.BytesRead != length)
            {
                throw new InvalidDataException("The degraded view requires one exact underlying dump read.");
            }

            Consumed = true;
            return MemoryReadResult.Create(
                exact.SourceId,
                exact.Address,
                exact.RequestedLength,
                exact.Bytes.AsSpan(0, prefixLength));
        }
    }

    private sealed class SumModelRegistry : IPureCallModelRegistry
    {
        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            var descriptor = new PureCallModelDescriptor(
                new PureCallModelIdentity("w4.combine-markers", Version),
                target,
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None);
            return PureCallModelSelectionResult.Selected(new SumModel(descriptor));
        }
    }

    private sealed class SumModel(PureCallModelDescriptor descriptor) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = descriptor;

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            return invocation.Arguments.Any(static argument =>
                    argument.Kind == PureCallModelArgumentKind.ExplainedUnknownInt32)
                ? PureCallModelOutcome.UnknownReturn()
                : PureCallModelOutcome.ExactReturn(unchecked(
                    invocation.Arguments[0].Int32Value!.Value +
                    invocation.Arguments[1].Int32Value!.Value));
        }
    }
}
