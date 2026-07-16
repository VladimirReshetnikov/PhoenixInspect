using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Metadata.SRM;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Executes the compiler-emitted W4 caller/helper graph with structured non-exact field evidence and freezes the
/// resulting interpreted-call lineage, operational transcript, and same/fresh-session replay facts.
/// </summary>
public sealed class W4CallLineageIntegrationTests
{
    private const string CallerName = "GetMarkerSummary";
    private const string HelperName = "CombineMarkers";
    private const int ExpectedMarker = 0x13579BDF;
    private const int ExpectedAlternateMarker = 0x13579BDE;
    private const int CallOffset = 12;
    private const string ExpectedTestTargetSha256 =
        "35922edc1898aaaf3942a4edcb3d2045eac67d7e69b08fd2f56a68e2be30f153";
    private const string ExpectedMixedGraphSha256 =
        "99f99db8a130095b9d14e453371304078663ea28948ca26f02c47337e897b6d6";
    private const string ExpectedBothUnknownGraphSha256 =
        "d63d6e626ddb3df72a4eee6654cf368f71992ba00768ea58b8e1f620f734b35d";

    private static readonly string EvidenceSourceSha256 = HashUtf8(
        "W4.5b compiler-emitted integration evidence source");
    private static readonly string ImportedObjectSha256 = HashUtf8(
        "W4.5b compiler-emitted imported receiver");

    /// <summary>
    /// Proves that one partial marker crosses parameter zero and the interpreted return while the exact alternate
    /// marker remains node-free, with the exact compiler stack order retained in the arithmetic transform.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CompilerEmittedCallCarriesOneUnknownAcrossArgumentAndReturnBoundaries()
    {
        using var fixture = PreparedFixture.Create();

        var run = Execute(
            fixture,
            MarkerObservation.Partial,
            MarkerObservation.Exact);

        AssertSha256(ExpectedMixedGraphSha256, run.Graph.Sha256);
        Assert.Equal(5, run.Graph.Nodes.Length);
        Assert.Equal(5, run.InternedNodeCount);
        Assert.Equal(
            new[]
            {
                LineageNodeKind.InputOrigin,
                LineageNodeKind.BinaryTransform,
                LineageNodeKind.FieldLoadTransform,
                LineageNodeKind.CallArgumentTransform,
                LineageNodeKind.InterpretedReturnTransform,
            },
            run.Graph.Nodes.Select(static node => node.Kind).Order().ToArray());

        var origin = Assert.IsType<InputOriginLineageNode>(
            run.Graph.Nodes.Single(static node => node.Kind == LineageNodeKind.InputOrigin));
        var fieldLoad = Assert.IsType<FieldLoadTransformLineageNode>(
            run.Graph.Nodes.Single(static node => node.Kind == LineageNodeKind.FieldLoadTransform));
        var callArgument = Assert.IsType<CallArgumentTransformLineageNode>(
            run.Graph.Nodes.Single(static node => node.Kind == LineageNodeKind.CallArgumentTransform));
        var binary = Assert.IsType<BinaryTransformLineageNode>(
            run.Graph.Nodes.Single(static node => node.Kind == LineageNodeKind.BinaryTransform));
        var returned = Assert.IsType<InterpretedReturnTransformLineageNode>(
            run.Graph.Nodes.Single(static node => node.Kind == LineageNodeKind.InterpretedReturnTransform));

        Assert.Equal(ProvenanceInputKind.ImportedField, origin.Origin.Kind);
        Assert.Equal(fixture.MarkerOrdinal, origin.Origin.OriginIndex);
        Assert.Equal(EvaluationEvidenceStatus.Partial, origin.Origin.Evidence);
        Assert.Equal(TypeSig.Int32, origin.Origin.StaticType);
        Assert.Equal(fixture.MarkerField, fieldLoad.Field);
        Assert.Equal(origin.Id, fieldLoad.InputOrigin);

        Assert.Equal(fixture.CallSite, callArgument.CallSite);
        Assert.Equal(0, callArgument.ParameterIndex);
        Assert.Equal(fieldLoad.Id, callArgument.Predecessor);
        Assert.Equal(BinaryOp.Add, binary.Operation);
        Assert.Equal(callArgument.Id, binary.Left.Predecessor);
        Assert.Null(binary.Left.ExactInt32);
        Assert.Null(binary.Right.Predecessor);
        Assert.Equal(ExpectedAlternateMarker, binary.Right.ExactInt32);

        Assert.Equal(fixture.CallSite, returned.CallSite);
        Assert.Equal(fixture.Helper, returned.Callee);
        Assert.Equal(binary.Id, returned.Predecessor);
        Assert.Equal(returned.Id, run.Graph.Root);
        AssertCanonicalGraph(run.Graph);
    }

    /// <summary>
    /// Proves that both non-exact compiler-emitted field values retain distinct ordered call-argument transforms and
    /// reproduce the complete graph, transcript, and operational accounting in the same and a fresh SRM session.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CompilerEmittedCallLineageReplaysAcrossSameAndFreshSessions()
    {
        using var fixture = PreparedFixture.Create();
        var first = Execute(
            fixture,
            MarkerObservation.Partial,
            MarkerObservation.Unavailable);
        var repeated = Execute(
            fixture,
            MarkerObservation.Partial,
            MarkerObservation.Unavailable);

        using var freshFixture = PreparedFixture.Create();
        var fresh = Execute(
            freshFixture,
            MarkerObservation.Partial,
            MarkerObservation.Unavailable);

        AssertSha256(ExpectedBothUnknownGraphSha256, first.Graph.Sha256);
        Assert.Equal(8, first.Graph.Nodes.Length);
        Assert.Equal(8, first.InternedNodeCount);
        Assert.Equal(2, first.Graph.Nodes.Count(static node => node.Kind == LineageNodeKind.InputOrigin));
        Assert.Equal(2, first.Graph.Nodes.Count(static node => node.Kind == LineageNodeKind.FieldLoadTransform));
        Assert.Equal(2, first.Graph.Nodes.Count(static node => node.Kind == LineageNodeKind.CallArgumentTransform));
        Assert.Single(first.Graph.Nodes, static node => node.Kind == LineageNodeKind.BinaryTransform);
        Assert.Single(first.Graph.Nodes, static node => node.Kind == LineageNodeKind.InterpretedReturnTransform);

        var fieldLoads = first.Graph.Nodes
            .OfType<FieldLoadTransformLineageNode>()
            .ToDictionary(static node => node.Field.Handle);
        var origins = first.Graph.Nodes
            .OfType<InputOriginLineageNode>()
            .ToDictionary(static node => node.Id);
        var arguments = first.Graph.Nodes
            .OfType<CallArgumentTransformLineageNode>()
            .ToDictionary(static node => node.ParameterIndex);
        var binary = Assert.Single(first.Graph.Nodes.OfType<BinaryTransformLineageNode>());
        var returned = Assert.Single(first.Graph.Nodes.OfType<InterpretedReturnTransformLineageNode>());

        Assert.Equal(fixture.CallSite, arguments[0].CallSite);
        Assert.Equal(fixture.CallSite, arguments[1].CallSite);
        Assert.Equal(fieldLoads[fixture.MarkerField.Handle].Id, arguments[0].Predecessor);
        Assert.Equal(fieldLoads[fixture.AlternateMarkerField.Handle].Id, arguments[1].Predecessor);
        Assert.Equal(arguments[0].Id, binary.Left.Predecessor);
        Assert.Equal(arguments[1].Id, binary.Right.Predecessor);
        Assert.Null(binary.Left.ExactInt32);
        Assert.Null(binary.Right.ExactInt32);
        Assert.Equal(binary.Id, returned.Predecessor);
        Assert.Equal(fixture.CallSite, returned.CallSite);
        Assert.Equal(returned.Id, first.Graph.Root);

        var markerOrigin = origins[fieldLoads[fixture.MarkerField.Handle].InputOrigin];
        var alternateOrigin = origins[fieldLoads[fixture.AlternateMarkerField.Handle].InputOrigin];
        Assert.Equal(fixture.MarkerOrdinal, markerOrigin.Origin.OriginIndex);
        Assert.Equal(EvaluationEvidenceStatus.Partial, markerOrigin.Origin.Evidence);
        Assert.Equal(fixture.AlternateMarkerOrdinal, alternateOrigin.Origin.OriginIndex);
        Assert.Equal(EvaluationEvidenceStatus.Unavailable, alternateOrigin.Origin.Evidence);

        Assert.Equal(fixture.Plan, freshFixture.Plan);
        AssertCanonicalReplay(first, repeated);
        AssertCanonicalReplay(first, fresh);

        var replayDomain = new ProvenanceConcreteDomain();
        var replayedValue = replayDomain.ReplayLineage(first.Graph);
        var replayedGraph = replayDomain.CaptureLineage(replayedValue);
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, replayDomain.GetPrecision(replayedValue));
        Assert.Equal(first.Graph.Root, replayedGraph.Root);
        Assert.Equal(first.Graph.Sha256, replayedGraph.Sha256);
        Assert.True(first.Graph.CanonicalBytes.AsSpan().SequenceEqual(replayedGraph.CanonicalBytes.AsSpan()));
        Assert.Equal(
            first.Graph.Nodes.Select(static node => node.Id),
            replayedGraph.Nodes.Select(static node => node.Id));
        AssertCanonicalGraph(first.Graph);
        AssertCanonicalGraph(replayedGraph);
    }

    private static ScenarioRun Execute(
        PreparedFixture fixture,
        MarkerObservation marker,
        MarkerObservation alternateMarker)
    {
        var domain = new ProvenanceConcreteDomain();
        var receiver = domain.ObjectReference(
            0x45B,
            fixture.CallerNode.Definition.Signature.DeclaringType);
        var memory = FixtureMemory.Instance;
        var memoryModel = new EvidenceMemoryModel(
            domain,
            receiver,
            fixture,
            marker,
            alternateMarker);
        var machine = new IlMachine<ProvenanceConcreteValue, FixtureMemory>(
            domain,
            fixture.Resolution,
            memoryModel,
            new InstructionBudgetPolicy(),
            UnknownExecutionPolicy.ExplainedInt32);
        var resolutionBeforeExecution = fixture.Resolution.Counts;
        var activation = machine.ActivatePreparedGraph(
            fixture.Plan,
            maximumLogicalCallDepth: 2,
            ImmutableArray.Create(receiver),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var result = RunToStop(machine, activation);

        Assert.Equal(MachineRunStatus.Completed, result.Outcome.Status);
        Assert.Null(result.Outcome.Failure);
        Assert.Null(result.Outcome.TargetException);
        Assert.Empty(result.Outcome.State.CallStack);
        Assert.True(result.Outcome.State.ReturnValue.HasValue);
        var returnedValue = result.Outcome.State.ReturnValue.Value;
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(returnedValue));
        Assert.False(domain.TryGetConstInt32(returnedValue, out _));
        Assert.Same(memory, result.Outcome.State.Memory);
        Assert.Equal(2, memoryModel.LoadedFields.Count);
        Assert.Equal(
            new[] { fixture.MarkerField.Handle, fixture.AlternateMarkerField.Handle },
            memoryModel.LoadedFields.ToArray());
        Assert.Equal(resolutionBeforeExecution, fixture.Resolution.Counts);

        Assert.Equal(90, result.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Equal(2, result.Outcome.OperationalState.ConfiguredMaximumLogicalCallDepth);
        Assert.Equal(2, result.Outcome.OperationalState.RequiredLogicalCallDepth);
        Assert.Equal(2, result.Outcome.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(2, result.Outcome.OperationalState.ActiveFrameDepthHighWater);

        var executed = result.Events
            .Where(static item => item.Kind == DebugEventKind.InstructionExecuted)
            .Select(static item => (item.Method, item.IlOffset))
            .ToArray();
        Assert.Equal(
            new[]
            {
                (fixture.Caller, 0),
                (fixture.Caller, 1),
                (fixture.Caller, 6),
                (fixture.Caller, 7),
                (fixture.Caller, CallOffset),
                (fixture.Helper, 0),
                (fixture.Helper, 1),
                (fixture.Helper, 2),
                (fixture.Helper, 3),
                (fixture.Caller, 17),
            },
            executed);
        var expectedEvents = ExpectedEvents(fixture, memoryModel, marker, alternateMarker);
        var actualEvents = EventFingerprint(result.Events);
        Assert.Equal(expectedEvents.Length, actualEvents.Length);
        for (var index = 0; index < expectedEvents.Length; index++)
        {
            Assert.Equal(expectedEvents[index], actualEvents[index]);
        }

        var graph = domain.CaptureLineage(returnedValue);
        Assert.Equal(domain.InternedNodeCount, graph.Nodes.Length);
        return new ScenarioRun(
            graph,
            domain.InternedNodeCount,
            result.Outcome.OperationalState,
            EventFingerprint(result.Events),
            memoryModel.LoadedFields.ToImmutableArray(),
            resolutionBeforeExecution,
            fixture.Resolution.Counts);
    }

    private static ImmutableArray<ExpectedEvent> ExpectedEvents(
        PreparedFixture fixture,
        EvidenceMemoryModel memory,
        MarkerObservation marker,
        MarkerObservation alternateMarker)
    {
        var expected = ImmutableArray.CreateBuilder<ExpectedEvent>();
        AddInstruction(expected, fixture.Caller, 0, "LoadArgument");
        AddInstruction(expected, fixture.Caller, 1, "LoadField");
        AddPrecisionIfNeeded(expected, fixture.Caller, 1, memory.MarkerEvidence, marker);
        AddInstruction(expected, fixture.Caller, 6, "LoadArgument");
        AddInstruction(expected, fixture.Caller, 7, "LoadField");
        AddPrecisionIfNeeded(
            expected,
            fixture.Caller,
            7,
            memory.AlternateMarkerEvidence,
            alternateMarker);
        AddInstruction(expected, fixture.Caller, CallOffset, "Call");
        expected.Add(new ExpectedEvent(DebugEventKind.FramePushed, fixture.Helper, 0, "Entry", null));
        AddInstruction(expected, fixture.Helper, 0, "LoadArgument");
        AddInstruction(expected, fixture.Helper, 1, "LoadArgument");
        AddInstruction(expected, fixture.Helper, 2, "Add");
        AddInstruction(expected, fixture.Helper, 3, "Return");
        expected.Add(new ExpectedEvent(DebugEventKind.FramePopped, fixture.Helper, 3, "Return", null));
        AddInstruction(expected, fixture.Caller, 17, "Return");
        expected.Add(new ExpectedEvent(DebugEventKind.FramePopped, fixture.Caller, 17, "Return", null));
        return expected.ToImmutable();
    }

    private static void AddInstruction(
        ImmutableArray<ExpectedEvent>.Builder events,
        MethodHandle method,
        int ilOffset,
        string instruction) =>
        events.Add(new ExpectedEvent(
            DebugEventKind.InstructionExecuted,
            method,
            ilOffset,
            instruction,
            null));

    private static void AddPrecisionIfNeeded(
        ImmutableArray<ExpectedEvent>.Builder events,
        MethodHandle method,
        int ilOffset,
        FieldLoadEvidence? evidence,
        MarkerObservation observation)
    {
        if (observation == MarkerObservation.Exact)
        {
            Assert.Null(evidence);
            return;
        }

        Assert.NotNull(evidence);
        events.Add(new ExpectedEvent(
            DebugEventKind.ValuePrecisionLost,
            method,
            ilOffset,
            "LoadField",
            evidence.Sha256));
    }

    private static ImmutableArray<ExpectedEvent> EventFingerprint(IEnumerable<DebugEvent> events) =>
        events.Select(static item => new ExpectedEvent(
            item.Kind,
            item.Method,
            item.IlOffset,
            item.Instruction,
            item.FieldEvidence?.Sha256)).ToImmutableArray();

    private static void AssertCanonicalReplay(ScenarioRun expected, ScenarioRun actual)
    {
        Assert.NotSame(expected.Graph, actual.Graph);
        Assert.Equal(expected.Graph.Root, actual.Graph.Root);
        Assert.Equal(expected.Graph.Sha256, actual.Graph.Sha256);
        Assert.True(expected.Graph.CanonicalBytes.AsSpan().SequenceEqual(actual.Graph.CanonicalBytes.AsSpan()));
        Assert.Equal(
            expected.Graph.Nodes.Select(static node => node.Id),
            actual.Graph.Nodes.Select(static node => node.Id));
        for (var index = 0; index < expected.Graph.Nodes.Length; index++)
        {
            Assert.True(expected.Graph.Nodes[index].CanonicalBytes.AsSpan().SequenceEqual(
                actual.Graph.Nodes[index].CanonicalBytes.AsSpan()));
        }

        Assert.Equal(expected.InternedNodeCount, actual.InternedNodeCount);
        Assert.Equal(expected.OperationalState, actual.OperationalState);
        Assert.Equal(expected.Events.Length, actual.Events.Length);
        for (var index = 0; index < expected.Events.Length; index++)
        {
            Assert.Equal(expected.Events[index], actual.Events[index]);
        }

        Assert.Equal(expected.LoadedFields.Length, actual.LoadedFields.Length);
        for (var index = 0; index < expected.LoadedFields.Length; index++)
        {
            Assert.Equal(expected.LoadedFields[index], actual.LoadedFields[index]);
        }
        Assert.Equal(expected.ResolutionBeforeExecution, actual.ResolutionBeforeExecution);
        Assert.Equal(actual.ResolutionBeforeExecution, actual.ResolutionAfterExecution);
    }

    private static void AssertCanonicalGraph(ProvenanceLineageGraph graph)
    {
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(graph.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            graph.Sha256);
        Assert.All(
            graph.Nodes,
            static node => Assert.Equal(
                Convert.ToHexString(SHA256.HashData(node.CanonicalBytes.AsSpan())).ToLowerInvariant(),
                node.Id.Sha256));
    }

    private static (
        StepOutcome<ProvenanceConcreteValue, FixtureMemory> Outcome,
        ImmutableArray<DebugEvent> Events) RunToStop(
        IlMachine<ProvenanceConcreteValue, FixtureMemory> machine,
        MachineActivationResult<ProvenanceConcreteValue, FixtureMemory> activation)
    {
        var state = activation.State!;
        var operationalState = machine.CreatePreparedOperationalState(new BudgetState(100));
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        for (var step = 0; step < 100; step++)
        {
            var outcome = machine.StepOne(state, operationalState);
            events.AddRange(outcome.Events);
            if (outcome.Status != MachineRunStatus.Ready)
            {
                return (outcome, events.ToImmutable());
            }

            state = outcome.State;
            operationalState = outcome.OperationalState;
        }

        throw new InvalidOperationException(
            "The W4.5b compiler-emitted graph did not stop within 100 deterministic steps.");
    }

    private static string ResolveTargetAssemblyPath() =>
        TestTargetPaths.ResolveAssembly(TestTargetPaths.ResolveExecutable());

    private static MethodHandle ResolveMethodHandle(SrmMetadataModule module, string methodName)
    {
        var token = module.FindMethodDefinition("DumpProbe", methodName);
        Assert.True(token.IsSuccess, token.Failure?.Code);
        var handle = module.GetMethodHandle(token.Value);
        Assert.True(handle.IsSuccess, handle.Failure?.Code);
        return handle.Value;
    }

    private static int ReadToken(ImmutableArray<byte> code, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset, sizeof(int)));

    private static void AssertSha256(string expected, string actual) => Assert.True(
        string.Equals(expected, actual, StringComparison.Ordinal),
        $"Expected SHA-256 '{expected}', actual '{actual}'.");

    private static string HashUtf8(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private enum MarkerObservation
    {
        Exact,
        Partial,
        Unavailable,
    }

    private sealed record FixtureMemory : IPersistentMemoryState<FixtureMemory>
    {
        internal static FixtureMemory Instance { get; } = new();

        public FixtureMemory Fork() => this;
    }

    private sealed class EvidenceMemoryModel : IMemoryModel<ProvenanceConcreteValue, FixtureMemory>
    {
        private readonly ProvenanceConcreteDomain domain;
        private readonly ProvenanceConcreteValue receiver;
        private readonly PreparedFixture fixture;
        private readonly MarkerObservation marker;
        private readonly MarkerObservation alternateMarker;

        internal EvidenceMemoryModel(
            ProvenanceConcreteDomain domain,
            ProvenanceConcreteValue receiver,
            PreparedFixture fixture,
            MarkerObservation marker,
            MarkerObservation alternateMarker)
        {
            this.domain = domain;
            this.receiver = receiver;
            this.fixture = fixture;
            this.marker = marker;
            this.alternateMarker = alternateMarker;
            MarkerEvidence = CreateEvidence(
                fixture.MarkerOrdinal,
                fixture.MarkerField,
                marker,
                ExpectedMarker);
            AlternateMarkerEvidence = CreateEvidence(
                fixture.AlternateMarkerOrdinal,
                fixture.AlternateMarkerField,
                alternateMarker,
                ExpectedAlternateMarker);
        }

        internal List<FieldHandle> LoadedFields { get; } = [];

        internal FieldLoadEvidence? MarkerEvidence { get; }

        internal FieldLoadEvidence? AlternateMarkerEvidence { get; }

        public bool CanAllocate => false;

        public (ProvenanceConcreteValue objRef, FixtureMemory mem) NewObject(
            FixtureMemory mem,
            TypeSig type) => throw new NotSupportedException();

        public (ProvenanceConcreteValue arrRef, FixtureMemory mem) NewArray(
            FixtureMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length) => throw new NotSupportedException();

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            FixtureMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field)
        {
            Assert.Same(FixtureMemory.Instance, mem);
            Assert.Same(receiver, objRef);
            LoadedFields.Add(field.Handle);
            if (field.Handle == fixture.MarkerField.Handle)
            {
                return Load(marker, MarkerEvidence, ExpectedMarker);
            }

            if (field.Handle == fixture.AlternateMarkerField.Handle)
            {
                return Load(alternateMarker, AlternateMarkerEvidence, ExpectedAlternateMarker);
            }

            throw new InvalidOperationException("The emitted W4 graph requested an unexpected field dependency.");
        }

        public FixtureMemory StoreField(
            FixtureMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value) => throw new NotSupportedException();

        public ProvenanceConcreteValue LoadElement(
            FixtureMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index) => throw new NotSupportedException();

        public FixtureMemory StoreElement(
            FixtureMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value) => throw new NotSupportedException();

        private MemoryLoadResult<ProvenanceConcreteValue> Load(
            MarkerObservation observation,
            FieldLoadEvidence? evidence,
            int exact) => observation switch
        {
            MarkerObservation.Exact => MemoryLoadResult<ProvenanceConcreteValue>.Exact(
                domain.ConstInt32(exact)),
            MarkerObservation.Partial or MarkerObservation.Unavailable =>
                MemoryLoadResult<ProvenanceConcreteValue>.FromFieldEvidence(evidence!),
            _ => throw new InvalidOperationException("The field observation fixture is undefined."),
        };

        private static FieldLoadEvidence? CreateEvidence(
            int ordinal,
            ResolvedField field,
            MarkerObservation observation,
            int exact)
        {
            if (observation == MarkerObservation.Exact)
            {
                return null;
            }

            Span<byte> exactBytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(exactBytes, exact);
            var observedBytes = observation == MarkerObservation.Partial
                ? exactBytes[..2]
                : ReadOnlySpan<byte>.Empty;
            return new FieldLoadEvidence(
                ordinal,
                field,
                observation == MarkerObservation.Partial
                    ? EvaluationEvidenceStatus.Partial
                    : EvaluationEvidenceStatus.Unavailable,
                observation == MarkerObservation.Partial
                    ? "W4.Integration.Partial"
                    : "W4.Integration.Unavailable",
                EvidenceSourceSha256,
                ImportedObjectSha256,
                0x0000_0001_2345_6700UL + checked((ulong)ordinal * 0x10UL),
                sizeof(int),
                observedBytes);
        }
    }

    private sealed class PreparedFixture : IDisposable
    {
        private PreparedFixture(
            SrmMetadataModule module,
            CountingResolutionServices resolution,
            FrozenMethodGraphPlan plan,
            MethodHandle caller,
            MethodHandle helper,
            FrozenMethodGraphNode callerNode,
            ResolvedField markerField,
            ResolvedField alternateMarkerField,
            int markerOrdinal,
            int alternateMarkerOrdinal)
        {
            Module = module;
            Resolution = resolution;
            Plan = plan;
            Caller = caller;
            Helper = helper;
            CallerNode = callerNode;
            MarkerField = markerField;
            AlternateMarkerField = alternateMarkerField;
            MarkerOrdinal = markerOrdinal;
            AlternateMarkerOrdinal = alternateMarkerOrdinal;
            CallSite = new DirectCallSiteIdentity(caller, CallOffset, helper);
        }

        private SrmMetadataModule Module { get; }

        internal CountingResolutionServices Resolution { get; }

        internal FrozenMethodGraphPlan Plan { get; }

        internal MethodHandle Caller { get; }

        internal MethodHandle Helper { get; }

        internal FrozenMethodGraphNode CallerNode { get; }

        internal ResolvedField MarkerField { get; }

        internal ResolvedField AlternateMarkerField { get; }

        internal int MarkerOrdinal { get; }

        internal int AlternateMarkerOrdinal { get; }

        internal DirectCallSiteIdentity CallSite { get; }

        internal static PreparedFixture Create()
        {
            var targetAssemblyPath = ResolveTargetAssemblyPath();
            Assert.Equal(
                ExpectedTestTargetSha256,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(targetAssemblyPath))).ToLowerInvariant());
            var module = SrmMetadataModule.LoadFromFile(targetAssemblyPath);
            try
            {
                var caller = ResolveMethodHandle(module, CallerName);
                var helper = ResolveMethodHandle(module, HelperName);
                var resolution = new CountingResolutionServices(new MetadataResolutionServices(module));
                var result = new MethodGraphPlanner(resolution).Prepare(caller);
                Assert.True(result.IsSuccess, result.Failure?.Code);
                var plan = Assert.IsType<FrozenMethodGraphPlan>(result.Plan);
                Assert.True(plan.TryGetNode(caller, out var callerNode));
                Assert.NotNull(callerNode);

                var markerToken = ReadToken(callerNode.Definition.Body.CodeBytes, 2);
                var alternateMarkerToken = ReadToken(callerNode.Definition.Body.CodeBytes, 8);
                var markerField = plan.Fields.Single(field => field.Handle.MetadataToken == markerToken);
                var alternateMarkerField = plan.Fields.Single(
                    field => field.Handle.MetadataToken == alternateMarkerToken);
                var markerOrdinal = plan.Fields.IndexOf(markerField);
                var alternateMarkerOrdinal = plan.Fields.IndexOf(alternateMarkerField);
                Assert.True(markerOrdinal >= 0);
                Assert.True(alternateMarkerOrdinal >= 0);
                Assert.NotEqual(markerOrdinal, alternateMarkerOrdinal);

                return new PreparedFixture(
                    module,
                    resolution,
                    plan,
                    caller,
                    helper,
                    callerNode,
                    markerField,
                    alternateMarkerField,
                    markerOrdinal,
                    alternateMarkerOrdinal);
            }
            catch
            {
                module.Dispose();
                throw;
            }
        }

        public void Dispose() => Module.Dispose();
    }

    private sealed class CountingResolutionServices(IResolutionServices inner) : IResolutionServices
    {
        internal ResolutionCounts Counts { get; private set; }

        ResolutionResult<ResolvedMethodDefinition> IResolutionServices.GetMethodDefinition(MethodHandle method)
        {
            Counts = Counts with { MethodDefinitions = Counts.MethodDefinitions + 1 };
            return inner.GetMethodDefinition(method);
        }

        ResolutionResult<ResolvedMethodCallTarget> IResolutionServices.ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Counts = Counts with { Methods = Counts.Methods + 1 };
            return inner.ResolveMethod(contextMethod, metadataToken);
        }

        ResolutionResult<ResolvedField> IResolutionServices.ResolveField(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Counts = Counts with { Fields = Counts.Fields + 1 };
            return inner.ResolveField(contextMethod, metadataToken);
        }
    }

    private readonly record struct ResolutionCounts(int MethodDefinitions, int Fields, int Methods);

    private sealed record ScenarioRun(
        ProvenanceLineageGraph Graph,
        int InternedNodeCount,
        MachineOperationalState OperationalState,
        ImmutableArray<ExpectedEvent> Events,
        ImmutableArray<FieldHandle> LoadedFields,
        ResolutionCounts ResolutionBeforeExecution,
        ResolutionCounts ResolutionAfterExecution);

    private readonly record struct ExpectedEvent(
        DebugEventKind Kind,
        MethodHandle Method,
        int IlOffset,
        string Instruction,
        string? FieldEvidenceSha256);
}
