using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves W5.2's product-owned acquisition, typed failures, deterministic ordering, and detached execution against a
/// real generated dump.
/// </summary>
public sealed class W5MethodAcquisitionFacadeIntegrationTests
{
    private const string ProbeTypeName = "DumpProbe";
    private const int ExpectedSummary = 0x26AF37BD;

    /// <summary>
    /// Acquires both execution realizations from only an open dump and issued request, poisons and disposes the
    /// acquisition source, then prepares and executes both detached existing W4 candidates.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W5ExpressionFacadeV1")]
    public void Facade_owns_ordered_acquisition_and_execution_remains_detached()
    {
        var executablePath = TestTargetPaths.ResolveExecutable();
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w5-acquisition-{Guid.NewGuid():N}.dmp");
        var acquired = ImmutableArray.CreateBuilder<(
            DumpMethodEvaluationMode Mode,
            DumpMethodAcquisitionResult Result,
            CountingEvidenceSource Source)>();

        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executablePath))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            using (var session = OpenExact(dumpPath))
            {
                var rootSearch = session.FindStrongHandleObjectsByTypeName(
                    ProbeTypeName,
                    maximumMatches: 2,
                    maximumHandlesScanned: 100_000);
                Assert.Equal(ClrmdEvidenceStatus.Exact, rootSearch.Status);
                Assert.Single(rootSearch.Matches);
                var rootBinding = DumpQueryRootBinding.FromSearchResult("root", rootSearch);

                foreach (var mode in new[]
                         {
                             DumpMethodEvaluationMode.Interpreted,
                             DumpMethodEvaluationMode.Modeled,
                         })
                {
                    var classification = DumpExpressionClassifier.Classify(
                        "root.GetMarkerSummary()",
                        rootBinding,
                        CreatePolicy(mode));
                    Assert.Equal(DumpExpressionClassificationStatus.Accepted, classification.Status);
                    var source = new CountingEvidenceSource(session);
                    var acquisition = DumpMethodAcquisitionFacade.Acquire(source, classification.Request!);
                    Assert.True(acquisition.IsSuccess, acquisition.Failure?.Code);
                    Assert.Equal(
                        ExpectedAcquisitionOrder.AsSpan().ToArray(),
                        source.Operations.AsSpan().ToArray());
                    source.Poisoned = true;
                    acquired.Add((mode, acquisition, source));
                }

                AssertTypedFailures(session, rootBinding);
            }

            foreach (var row in acquired)
            {
                var callCountBeforeExecution = row.Source.Operations.Length;
                var runner = new CounterfactualMethodRunner<CounterfactualDumpMemory>();
                var preparation = runner.Prepare(row.Result.Binding!.Candidate);
                Assert.True(preparation.IsSuccess, preparation.Failure?.Diagnostics[0].Code);
                var result = runner.Run(preparation.Plan!);

                Assert.Equal(EvaluationSemanticMode.CounterfactualExecution, result.SemanticMode);
                Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
                Assert.Equal(EvaluationCompleteness.Complete, result.Completeness);
                Assert.Equal(EvaluationEvidenceStatus.Exact, result.Evidence);
                Assert.Equal(EvaluationEffectStatus.None, result.Effects);
                Assert.Equal(CounterfactualExecutionValueKind.ExactReturn, result.Value!.Kind);
                Assert.Equal(ExpectedSummary, result.Value.ExactInt32);
                Assert.Equal(
                    row.Mode == DumpMethodEvaluationMode.Interpreted ? 10 : 6,
                    result.Context.Accounting.InstructionUsed);
                Assert.Equal(
                    row.Mode == DumpMethodEvaluationMode.Interpreted ? 0 : 1,
                    result.Context.ModelInvocationCount);
                Assert.Equal(callCountBeforeExecution, row.Source.Operations.Length);
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

    private static readonly ImmutableArray<string> ExpectedAcquisitionOrder =
    [
        "snapshot",
        "modules",
        "metadata",
        $"method:{DumpExpressionClassifier.SupportedMethodName}",
        $"method:{DumpMethodAcquisitionFacade.SupportedHelperMethodName}",
        $"roots:{ProbeTypeName}",
        $"field:{DumpMethodAcquisitionFacade.MarkerFieldName}",
        $"field:{DumpMethodAcquisitionFacade.AlternateMarkerFieldName}",
    ];

    private static void AssertTypedFailures(ClrmdDumpSession session, DumpQueryRootBinding exactRoot)
    {
        var request = Assert.IsType<DumpExpressionRequest>(DumpExpressionClassifier.Classify(
            "root.GetMarkerSummary()",
            exactRoot,
            CreatePolicy(DumpMethodEvaluationMode.Interpreted)).Request);
        var cases = new[]
        {
            (AcquisitionFault.MissingModule, DumpMethodAcquisitionFailureKind.Missing),
            (AcquisitionFault.PartialMetadata, DumpMethodAcquisitionFailureKind.Partial),
            (AcquisitionFault.InvalidMetadata, DumpMethodAcquisitionFailureKind.Invalid),
            (AcquisitionFault.UnsupportedHelperBody, DumpMethodAcquisitionFailureKind.Unsupported),
            (AcquisitionFault.AmbiguousRoot, DumpMethodAcquisitionFailureKind.Ambiguous),
            (AcquisitionFault.MissingField, DumpMethodAcquisitionFailureKind.Missing),
            (AcquisitionFault.UnavailableField, DumpMethodAcquisitionFailureKind.Unavailable),
            (AcquisitionFault.ConflictingField, DumpMethodAcquisitionFailureKind.Conflict),
        };

        foreach (var (fault, expectedKind) in cases)
        {
            var result = DumpMethodAcquisitionFacade.Acquire(new FaultingEvidenceSource(session, fault), request);
            Assert.False(result.IsSuccess);
            Assert.Null(result.Binding);
            Assert.Equal(expectedKind, result.Failure!.Kind);
            Assert.False(string.IsNullOrWhiteSpace(result.Failure.Code));
            Assert.False(string.IsNullOrWhiteSpace(result.Failure.Message));
        }

        var original = exactRoot.Root!;
        var incompatibleObject = new ClrmdHeapObjectInfo(
            original.Snapshot,
            original.Address + 8,
            original.TypeName,
            original.TypeMetadataToken,
            original.MethodTable,
            original.RootAddress,
            original.RootKind,
            original.Module,
            original.Evidence);
        var incompatibleBinding = DumpQueryRootBinding.FromExactObject(
            "root",
            incompatibleObject,
            exactRoot.AppliedBounds);
        var incompatibleRequest = Assert.IsType<DumpExpressionRequest>(DumpExpressionClassifier.Classify(
            "root.GetMarkerSummary()",
            incompatibleBinding,
            CreatePolicy(DumpMethodEvaluationMode.Interpreted)).Request);
        var incompatible = DumpMethodAcquisitionFacade.Acquire(
            new FaultingEvidenceSource(session, AcquisitionFault.None),
            incompatibleRequest);
        Assert.False(incompatible.IsSuccess);
        Assert.Equal(DumpMethodAcquisitionFailureKind.Incompatible, incompatible.Failure!.Kind);
        Assert.Equal("W5_ROOT_REACQUISITION_CONFLICT", incompatible.Failure.Code);

        var fieldRequest = Assert.IsType<DumpExpressionRequest>(DumpExpressionClassifier.Classify(
            "root.Marker",
            exactRoot,
            CreatePolicy(DumpMethodEvaluationMode.Interpreted)).Request);
        var wrongGrammar = DumpMethodAcquisitionFacade.Acquire(
            new FaultingEvidenceSource(session, AcquisitionFault.None),
            fieldRequest);
        Assert.False(wrongGrammar.IsSuccess);
        Assert.Equal(DumpMethodAcquisitionFailureKind.Invalid, wrongGrammar.Failure!.Kind);
        Assert.Equal("W5_METHOD_REQUEST_REQUIRED", wrongGrammar.Failure.Code);
    }

    private static DumpExpressionPolicy CreatePolicy(DumpMethodEvaluationMode mode) =>
        DumpExpressionPolicy.Create(
            mode,
            instructionLimit: 100,
            logicalDepthLimit: 2,
            traversalLimit: 10);

    private static ClrmdDumpSession OpenExact(string path)
    {
        var opened = ClrmdDumpSession.Open(path);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        return opened.Value ?? throw new InvalidOperationException("Exact dump open returned no session.");
    }

    private sealed class CountingEvidenceSource(ClrmdDumpSession session) : IDumpMethodEvidenceSource
    {
        private readonly ClrmdDumpSession session = session ?? throw new ArgumentNullException(nameof(session));
        private readonly ImmutableArray<string>.Builder operations = ImmutableArray.CreateBuilder<string>();

        internal bool Poisoned { get; set; }

        internal ImmutableArray<string> Operations => operations.ToImmutable();

        public ClrmdSnapshotIdentity Snapshot
        {
            get
            {
                Touch("snapshot");
                return session.Snapshot;
            }
        }

        public ImmutableArray<ClrmdModuleInfo> Modules
        {
            get
            {
                Touch("modules");
                return session.Modules;
            }
        }

        public ClrmdEvidenceResult<ModuleContentIdentity> ReadModuleContentIdentity(ClrmdModuleInfo module)
        {
            Touch("metadata");
            return session.ReadModuleContentIdentity(module);
        }

        public ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
            ClrmdModuleInfo module,
            string typeName,
            string methodName)
        {
            Touch($"method:{methodName}");
            return session.ReadMethodBody(module, typeName, methodName);
        }

        public ClrmdHeapObjectSearchResult FindStrongHandleObjectsByTypeName(
            string typeName,
            int maximumMatches,
            int maximumHandlesScanned)
        {
            Touch($"roots:{typeName}");
            return session.FindStrongHandleObjectsByTypeName(typeName, maximumMatches, maximumHandlesScanned);
        }

        public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
            ClrmdHeapObjectInfo owner,
            string fieldName)
        {
            Touch($"field:{fieldName}");
            return session.ReadInt32Field(owner, fieldName);
        }

        private void Touch(string operation)
        {
            if (Poisoned)
            {
                throw new InvalidOperationException($"Acquisition source was touched after poison: {operation}.");
            }

            operations.Add(operation);
        }
    }

    private enum AcquisitionFault
    {
        None,
        MissingModule,
        PartialMetadata,
        InvalidMetadata,
        UnsupportedHelperBody,
        AmbiguousRoot,
        MissingField,
        UnavailableField,
        ConflictingField,
    }

    private sealed class FaultingEvidenceSource(
        ClrmdDumpSession session,
        AcquisitionFault fault) : IDumpMethodEvidenceSource
    {
        public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

        public ImmutableArray<ClrmdModuleInfo> Modules => fault == AcquisitionFault.MissingModule
            ? ImmutableArray<ClrmdModuleInfo>.Empty
            : session.Modules;

        public ClrmdEvidenceResult<ModuleContentIdentity> ReadModuleContentIdentity(ClrmdModuleInfo module) =>
            fault switch
            {
                AcquisitionFault.PartialMetadata => ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                    ClrmdEvidenceStatus.Partial,
                    ClrmdValueIssue.LimitExceeded),
                AcquisitionFault.InvalidMetadata => ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData),
                _ => session.ReadModuleContentIdentity(module),
            };

        public ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
            ClrmdModuleInfo module,
            string typeName,
            string methodName) =>
            fault == AcquisitionFault.UnsupportedHelperBody &&
            string.Equals(
                methodName,
                DumpMethodAcquisitionFacade.SupportedHelperMethodName,
                StringComparison.Ordinal)
                ? ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MethodHeaderUnsupported)
                : session.ReadMethodBody(module, typeName, methodName);

        public ClrmdHeapObjectSearchResult FindStrongHandleObjectsByTypeName(
            string typeName,
            int maximumMatches,
            int maximumHandlesScanned)
        {
            var result = session.FindStrongHandleObjectsByTypeName(typeName, maximumMatches, maximumHandlesScanned);
            if (fault != AcquisitionFault.AmbiguousRoot)
            {
                return result;
            }

            var match = Assert.Single(result.Matches);
            return new ClrmdHeapObjectSearchResult(
                result.Snapshot,
                result.TypeNameSelector,
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                result.HandlesScanned,
                result.MaximumHandlesScanned,
                result.MaximumMatches,
                matchLimitReached: false,
                ImmutableArray.Create(match, match),
                result.Evidence);
        }

        public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
            ClrmdHeapObjectInfo owner,
            string fieldName)
        {
            if (!string.Equals(fieldName, DumpMethodAcquisitionFacade.MarkerFieldName, StringComparison.Ordinal))
            {
                return session.ReadInt32Field(owner, fieldName);
            }

            return fault switch
            {
                AcquisitionFault.MissingField => ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.FieldUnavailable),
                AcquisitionFault.UnavailableField => ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemoryUnavailable),
                AcquisitionFault.ConflictingField => ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.InvalidData),
                _ => session.ReadInt32Field(owner, fieldName),
            };
        }
    }
}
