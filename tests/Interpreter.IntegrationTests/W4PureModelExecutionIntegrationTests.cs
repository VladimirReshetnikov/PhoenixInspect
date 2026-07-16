using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Metadata.SRM;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Executes the compiler-emitted W4 caller through its body-free CombineMarkers pure model and freezes exact,
/// explained-unknown, operational-attempt, and same/fresh-session conformance evidence.
/// </summary>
public sealed class W4PureModelExecutionIntegrationTests
{
    private const string CallerName = "GetMarkerSummary";
    private const string HelperName = "CombineMarkers";
    private const int ExpectedMarker = 0x13579BDF;
    private const int ExpectedAlternateMarker = 0x13579BDE;
    private const int ExpectedSummary = 0x26AF37BD;
    private const int CallOffset = 12;
    private const string ExpectedTestTargetSha256 =
        "1eee4384cc891aa7908b7b425b0626e66cd1ddd08bac11a4ad26d917d378e05a";
    private const string ExpectedMixedGraphSha256 =
        "0fc4508aa7681102e7be1eb0fa95f391ef7ac29df01d57009d58faf7f27d4e7d";
    private const string ExpectedBothUnknownGraphSha256 =
        "9bc0f1dc6a8cd38b520d6454ca62179337b2ccb7bb3d90c1a44ba8b3f7b00db2";

    private static readonly string EvidenceSourceSha256 = HashUtf8(
        "W4.6d compiler-emitted pure-model execution evidence source");
    private static readonly string ImportedObjectSha256 = HashUtf8(
        "W4.6d compiler-emitted pure-model imported receiver");
    private static readonly PureCallModelIdentity ModelIdentity = new(
        "w4.combine-markers",
        new PureCallModelVersion(1, 0, 0));

    /// <summary>
    /// Proves exact compiler values cross one body-free modeled boundary, agree with the existing interpreter and
    /// CoreCLR fixture oracle, consume six caller instructions, and never acquire or execute a helper frame or body.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void ExactCompilerEmittedCallExecutesBodyFreeModelAndAgreesWithCoreClr()
    {
        using var fixture = PreparedFixture.Create();

        var run = Execute(fixture, MarkerObservation.Exact, MarkerObservation.Exact);
        var interpretedResult = ExecuteInterpreted(
            fixture,
            MarkerObservation.Exact,
            MarkerObservation.Exact);

        Assert.Equal(ExpectedSummary, ReadCoreClrOracle());
        Assert.Equal(ValuePrecisionKind.Exact, interpretedResult.Precision);
        Assert.Equal(ExpectedSummary, interpretedResult.ExactResult);
        Assert.Equal(interpretedResult.ExactResult, run.ExactResult);
        Assert.Equal(ExpectedSummary, run.ExactResult);
        Assert.Null(run.Graph);
        Assert.Equal(PureCallModelUnknownPolicy.ExactOnly, run.Invocation.UnknownPolicy);
        Assert.Equal(
            new[]
            {
                PureCallModelArgument.ExactInt32(ExpectedMarker),
                PureCallModelArgument.ExactInt32(ExpectedAlternateMarker),
            },
            run.Invocation.Arguments.ToArray());
        Assert.Equal(PureModelAttemptOutcomeKind.ExactReturn, Assert.Single(run.Attempts).OutcomeKind);
        AssertExecutionBoundary(fixture, run, expectedPrecisionLosses: 0);
    }

    /// <summary>
    /// Proves one partial marker and one exact marker produce only the four required canonical nodes: input origin,
    /// field transform, parameter-zero call transform, and the modeled return with parameter one embedded exactly.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void MixedCompilerEvidenceCreatesFourNodeModeledReturnGraph()
    {
        using var fixture = PreparedFixture.Create();

        var run = Execute(fixture, MarkerObservation.Partial, MarkerObservation.Exact);
        var interpretedResult = ExecuteInterpreted(
            fixture,
            MarkerObservation.Partial,
            MarkerObservation.Exact);
        var graph = Assert.IsType<ProvenanceLineageGraph>(run.Graph);

        Assert.Null(run.ExactResult);
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, interpretedResult.Precision);
        Assert.Null(interpretedResult.ExactResult);
        AssertSha256(ExpectedMixedGraphSha256, graph.Sha256);
        Assert.Equal(4, graph.Nodes.Length);
        Assert.Equal(4, run.InternedNodeCount);
        Assert.Equal(
            new[]
            {
                LineageNodeKind.InputOrigin,
                LineageNodeKind.FieldLoadTransform,
                LineageNodeKind.CallArgumentTransform,
                LineageNodeKind.ModeledReturnTransform,
            },
            graph.Nodes.Select(static node => node.Kind).Order().ToArray());

        var origin = Assert.Single(graph.Nodes.OfType<InputOriginLineageNode>());
        var fieldLoad = Assert.Single(graph.Nodes.OfType<FieldLoadTransformLineageNode>());
        var callArgument = Assert.Single(graph.Nodes.OfType<CallArgumentTransformLineageNode>());
        var modeledReturn = Assert.Single(graph.Nodes.OfType<ModeledReturnTransformLineageNode>());

        Assert.Equal(ProvenanceInputKind.ImportedField, origin.Origin.Kind);
        Assert.Equal(fixture.MarkerOrdinal, origin.Origin.OriginIndex);
        Assert.Equal(EvaluationEvidenceStatus.Partial, origin.Origin.Evidence);
        Assert.Equal(fixture.MarkerField, fieldLoad.Field);
        Assert.Equal(origin.Id, fieldLoad.InputOrigin);
        Assert.Equal(fixture.CallSite, callArgument.CallSite);
        Assert.Equal(0, callArgument.ParameterIndex);
        Assert.Equal(fieldLoad.Id, callArgument.Predecessor);

        Assert.Equal(fixture.CallSite, modeledReturn.CallSite);
        Assert.Equal(ModelIdentity, modeledReturn.ModelIdentity);
        Assert.Equal(LineageOperandKind.Unknown, modeledReturn.Arguments[0].Kind);
        Assert.Equal(callArgument.Id, modeledReturn.Arguments[0].Predecessor);
        Assert.Null(modeledReturn.Arguments[0].ExactInt32);
        Assert.Equal(LineageOperandKind.ExactInt32, modeledReturn.Arguments[1].Kind);
        Assert.Equal(ExpectedAlternateMarker, modeledReturn.Arguments[1].ExactInt32);
        Assert.Null(modeledReturn.Arguments[1].Predecessor);
        Assert.Equal(modeledReturn.Id, graph.Root);

        Assert.Equal(PureCallModelUnknownPolicy.ExplainedInt32, run.Invocation.UnknownPolicy);
        Assert.Equal(PureCallModelArgumentKind.ExplainedUnknownInt32, run.Invocation.Arguments[0].Kind);
        Assert.Equal(PureCallModelArgument.ExactInt32(ExpectedAlternateMarker), run.Invocation.Arguments[1]);
        Assert.Equal(PureModelAttemptOutcomeKind.UnknownReturn, Assert.Single(run.Attempts).OutcomeKind);
        AssertCanonicalGraph(graph);
        AssertExecutionBoundary(fixture, run, expectedPrecisionLosses: 1);
    }

    /// <summary>
    /// Proves partial and unavailable marker inputs create the ordered seven-node modeled graph and reproduce its
    /// terminal semantics, canonical bytes, root, digest, attempt projection, and frozen execution dependencies in
    /// repeated machines and a fresh SRM module, registry, model, domain, and machine.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void DualUnknownModeledGraphReplaysAcrossSameAndFreshSessions()
    {
        using var fixture = PreparedFixture.Create();
        var first = Execute(fixture, MarkerObservation.Partial, MarkerObservation.Unavailable);
        var repeated = Execute(fixture, MarkerObservation.Partial, MarkerObservation.Unavailable);
        var interpretedResult = ExecuteInterpreted(
            fixture,
            MarkerObservation.Partial,
            MarkerObservation.Unavailable);

        using var freshFixture = PreparedFixture.Create();
        var fresh = Execute(freshFixture, MarkerObservation.Partial, MarkerObservation.Unavailable);
        var graph = Assert.IsType<ProvenanceLineageGraph>(first.Graph);

        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, interpretedResult.Precision);
        Assert.Null(interpretedResult.ExactResult);
        AssertSha256(ExpectedBothUnknownGraphSha256, graph.Sha256);
        Assert.Equal(7, graph.Nodes.Length);
        Assert.Equal(7, first.InternedNodeCount);
        Assert.Equal(2, graph.Nodes.Count(static node => node.Kind == LineageNodeKind.InputOrigin));
        Assert.Equal(2, graph.Nodes.Count(static node => node.Kind == LineageNodeKind.FieldLoadTransform));
        Assert.Equal(2, graph.Nodes.Count(static node => node.Kind == LineageNodeKind.CallArgumentTransform));
        Assert.Single(graph.Nodes, static node => node.Kind == LineageNodeKind.ModeledReturnTransform);

        var origins = graph.Nodes
            .OfType<InputOriginLineageNode>()
            .ToDictionary(static node => node.Id);
        var fieldLoads = graph.Nodes
            .OfType<FieldLoadTransformLineageNode>()
            .ToDictionary(static node => node.Field.Handle);
        var callArguments = graph.Nodes
            .OfType<CallArgumentTransformLineageNode>()
            .ToDictionary(static node => node.ParameterIndex);
        var modeledReturn = Assert.Single(graph.Nodes.OfType<ModeledReturnTransformLineageNode>());

        Assert.Equal(fixture.CallSite, callArguments[0].CallSite);
        Assert.Equal(fixture.CallSite, callArguments[1].CallSite);
        Assert.Equal(fieldLoads[fixture.MarkerField.Handle].Id, callArguments[0].Predecessor);
        Assert.Equal(fieldLoads[fixture.AlternateMarkerField.Handle].Id, callArguments[1].Predecessor);
        Assert.Equal(LineageOperandKind.Unknown, modeledReturn.Arguments[0].Kind);
        Assert.Equal(callArguments[0].Id, modeledReturn.Arguments[0].Predecessor);
        Assert.Equal(LineageOperandKind.Unknown, modeledReturn.Arguments[1].Kind);
        Assert.Equal(callArguments[1].Id, modeledReturn.Arguments[1].Predecessor);
        Assert.Equal(
            new[] { callArguments[0].Id, callArguments[1].Id },
            modeledReturn.Dependencies.ToArray());
        Assert.Equal(ModelIdentity, modeledReturn.ModelIdentity);
        Assert.Equal(modeledReturn.Id, graph.Root);

        var markerOrigin = origins[fieldLoads[fixture.MarkerField.Handle].InputOrigin];
        var alternateOrigin = origins[fieldLoads[fixture.AlternateMarkerField.Handle].InputOrigin];
        Assert.Equal(fixture.MarkerOrdinal, markerOrigin.Origin.OriginIndex);
        Assert.Equal(EvaluationEvidenceStatus.Partial, markerOrigin.Origin.Evidence);
        Assert.Equal(fixture.AlternateMarkerOrdinal, alternateOrigin.Origin.OriginIndex);
        Assert.Equal(EvaluationEvidenceStatus.Unavailable, alternateOrigin.Origin.Evidence);

        Assert.Equal(fixture.Plan, freshFixture.Plan);
        AssertReplayEquivalent(first, repeated);
        AssertReplayEquivalent(first, fresh);
        AssertExecutionBoundary(fixture, first, expectedPrecisionLosses: 2);
        AssertExecutionBoundary(fixture, repeated, expectedPrecisionLosses: 2);
        AssertExecutionBoundary(freshFixture, fresh, expectedPrecisionLosses: 2);
        AssertCanonicalGraph(graph);

        var replayDomain = new ProvenanceConcreteDomain();
        var replayedValue = replayDomain.ReplayLineage(graph);
        var replayedGraph = replayDomain.CaptureLineage(replayedValue);
        AssertCanonicalReplay(graph, replayedGraph);
    }

    private static ScenarioRun Execute(
        PreparedFixture fixture,
        MarkerObservation marker,
        MarkerObservation alternateMarker)
    {
        var domain = new ProvenanceConcreteDomain();
        var receiver = domain.ObjectReference(
            0x46C,
            fixture.CallerNode.Definition.Signature.DeclaringType);
        var memory = FixtureMemory.Instance;
        var memoryModel = new EvidenceMemoryModel(
            domain,
            receiver,
            fixture,
            marker,
            alternateMarker);
        var admitsUnknown = marker != MarkerObservation.Exact ||
            alternateMarker != MarkerObservation.Exact;
        var machine = new IlMachine<ProvenanceConcreteValue, FixtureMemory>(
            domain,
            fixture.Resolution,
            memoryModel,
            new InstructionBudgetPolicy(),
            admitsUnknown ? UnknownExecutionPolicy.ExplainedInt32 : UnknownExecutionPolicy.ExactOnly);

        var resolutionBeforeExecution = fixture.Resolution.Counts;
        var selectionCountBeforeExecution = fixture.Registry.SelectionCount;
        var descriptorReadsBeforeExecution = fixture.Model.DescriptorReadCount;
        var invocationsBeforeExecution = fixture.Model.Invocations.Count;
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
        Assert.Same(memory, result.Outcome.State.Memory);
        Assert.Equal(2, memoryModel.LoadedFields.Count);
        Assert.Equal(
            new[] { fixture.MarkerField.Handle, fixture.AlternateMarkerField.Handle },
            memoryModel.LoadedFields.ToArray());
        Assert.Equal(resolutionBeforeExecution, fixture.Resolution.Counts);
        Assert.Equal(selectionCountBeforeExecution, fixture.Registry.SelectionCount);
        Assert.Equal(descriptorReadsBeforeExecution, fixture.Model.DescriptorReadCount);
        Assert.Equal(invocationsBeforeExecution + 1, fixture.Model.Invocations.Count);

        var returnedValue = result.Outcome.State.ReturnValue.Value;
        int? exactResult = null;
        ProvenanceLineageGraph? graph = null;
        if (admitsUnknown)
        {
            Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(returnedValue));
            Assert.False(domain.TryGetConstInt32(returnedValue, out _));
            graph = domain.CaptureLineage(returnedValue);
            Assert.Equal(domain.InternedNodeCount, graph.Nodes.Length);
        }
        else
        {
            Assert.Equal(ValuePrecisionKind.Exact, domain.GetPrecision(returnedValue));
            Assert.True(domain.TryGetConstInt32(returnedValue, out var exact));
            exactResult = exact;
            Assert.Equal(0, domain.InternedNodeCount);
        }

        return new ScenarioRun(
            exactResult,
            graph,
            domain.InternedNodeCount,
            result.Outcome.OperationalState,
            ProjectAttempts(result.Outcome.OperationalState.ModelAttempts),
            EventFingerprint(result.Events),
            memoryModel.LoadedFields.ToImmutableArray(),
            fixture.Model.Invocations[^1],
            resolutionBeforeExecution,
            fixture.Resolution.Counts,
            selectionCountBeforeExecution,
            fixture.Registry.SelectionCount,
            descriptorReadsBeforeExecution,
            fixture.Model.DescriptorReadCount,
            invocationsBeforeExecution,
            fixture.Model.Invocations.Count);
    }

    private static InterpretedResult ExecuteInterpreted(
        PreparedFixture fixture,
        MarkerObservation marker,
        MarkerObservation alternateMarker)
    {
        var preparation = new MethodGraphPlanner(new MetadataResolutionServices(fixture.Module))
            .Prepare(fixture.Caller);
        Assert.True(preparation.IsSuccess, preparation.Failure?.Code);
        var plan = Assert.IsType<FrozenMethodGraphPlan>(preparation.Plan);

        var domain = new ProvenanceConcreteDomain();
        var receiver = domain.ObjectReference(
            0x46C,
            fixture.CallerNode.Definition.Signature.DeclaringType);
        var memory = FixtureMemory.Instance;
        var memoryModel = new EvidenceMemoryModel(
            domain,
            receiver,
            fixture,
            marker,
            alternateMarker);
        var admitsUnknown = marker != MarkerObservation.Exact ||
            alternateMarker != MarkerObservation.Exact;
        var machine = new IlMachine<ProvenanceConcreteValue, FixtureMemory>(
            domain,
            new MetadataResolutionServices(fixture.Module),
            memoryModel,
            new InstructionBudgetPolicy(),
            admitsUnknown ? UnknownExecutionPolicy.ExplainedInt32 : UnknownExecutionPolicy.ExactOnly);
        var activation = machine.ActivatePreparedGraph(
            plan,
            maximumLogicalCallDepth: 2,
            ImmutableArray.Create(receiver),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var result = RunToStop(machine, activation);
        Assert.Equal(MachineRunStatus.Completed, result.Outcome.Status);
        Assert.True(result.Outcome.State.ReturnValue.HasValue);
        Assert.Same(memory, result.Outcome.State.Memory);
        var returnedValue = result.Outcome.State.ReturnValue.Value;
        var precision = domain.GetPrecision(returnedValue);
        var exactResult = domain.TryGetConstInt32(returnedValue, out var exact)
            ? exact
            : (int?)null;
        Assert.Equal(2, memoryModel.LoadedFields.Count);
        return new InterpretedResult(precision, exactResult);
    }

    private static void AssertExecutionBoundary(
        PreparedFixture fixture,
        ScenarioRun run,
        int expectedPrecisionLosses)
    {
        Assert.Equal(94, run.OperationalState.Budget.InstructionBudget);
        Assert.Equal(2, run.OperationalState.ConfiguredMaximumLogicalCallDepth);
        Assert.Equal(2, run.OperationalState.RequiredLogicalCallDepth);
        Assert.Equal(2, run.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(1, run.OperationalState.ActiveFrameDepthHighWater);
        Assert.Equal(1, run.OperationalState.ModelInvocationCount);
        Assert.Equal(1, run.OperationalState.CompletedModeledCallCount);

        var attempt = Assert.Single(run.OperationalState.ModelAttempts);
        Assert.Equal(fixture.CallSite, attempt.CallSite);
        Assert.Equal(ModelIdentity, attempt.ModelIdentity);
        Assert.Equal(2, attempt.EnteredLogicalDepth);
        Assert.True(attempt.TransferCompleted);
        Assert.Null(attempt.StableCode);

        Assert.Equal(fixture.CallSite, run.Invocation.CallSite);
        Assert.Equal(run.ResolutionBeforeExecution, run.ResolutionAfterExecution);
        Assert.Equal(run.SelectionCountBeforeExecution, run.SelectionCountAfterExecution);
        Assert.Equal(run.DescriptorReadsBeforeExecution, run.DescriptorReadsAfterExecution);
        Assert.Equal(run.ModelInvocationsBeforeExecution + 1, run.ModelInvocationsAfterExecution);

        var executed = run.Events
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
                (fixture.Caller, 17),
            },
            executed);
        Assert.DoesNotContain(run.Events, item => item.Method == fixture.Helper);
        Assert.DoesNotContain(run.Events, static item => item.Kind == DebugEventKind.FramePushed);
        Assert.Single(run.Events, static item => item.Kind == DebugEventKind.FramePopped);
        Assert.Equal(
            expectedPrecisionLosses,
            run.Events.Count(static item => item.Kind == DebugEventKind.ValuePrecisionLost));
        Assert.Equal(
            new[] { fixture.MarkerField.Handle, fixture.AlternateMarkerField.Handle },
            run.LoadedFields.ToArray());
    }

    private static void AssertReplayEquivalent(ScenarioRun expected, ScenarioRun actual)
    {
        Assert.Equal(expected.ExactResult, actual.ExactResult);
        Assert.Equal(expected.InternedNodeCount, actual.InternedNodeCount);
        Assert.Equal(expected.Invocation, actual.Invocation);
        Assert.True(expected.Attempts.SequenceEqual(actual.Attempts));
        Assert.Equal(expected.OperationalState.Budget, actual.OperationalState.Budget);
        Assert.Equal(
            expected.OperationalState.ConfiguredMaximumLogicalCallDepth,
            actual.OperationalState.ConfiguredMaximumLogicalCallDepth);
        Assert.Equal(
            expected.OperationalState.RequiredLogicalCallDepth,
            actual.OperationalState.RequiredLogicalCallDepth);
        Assert.Equal(
            expected.OperationalState.ObservedLogicalDepthHighWater,
            actual.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(
            expected.OperationalState.ActiveFrameDepthHighWater,
            actual.OperationalState.ActiveFrameDepthHighWater);
        Assert.Equal(
            expected.OperationalState.ModelInvocationCount,
            actual.OperationalState.ModelInvocationCount);
        Assert.Equal(
            expected.OperationalState.CompletedModeledCallCount,
            actual.OperationalState.CompletedModeledCallCount);
        Assert.True(expected.Events.SequenceEqual(actual.Events));
        Assert.True(expected.LoadedFields.SequenceEqual(actual.LoadedFields));
        Assert.Equal(actual.ResolutionBeforeExecution, actual.ResolutionAfterExecution);
        Assert.Equal(actual.SelectionCountBeforeExecution, actual.SelectionCountAfterExecution);
        Assert.Equal(actual.DescriptorReadsBeforeExecution, actual.DescriptorReadsAfterExecution);
        Assert.Equal(actual.ModelInvocationsBeforeExecution + 1, actual.ModelInvocationsAfterExecution);

        Assert.NotNull(expected.Graph);
        Assert.NotNull(actual.Graph);
        AssertCanonicalReplay(expected.Graph, actual.Graph);
    }

    private static void AssertCanonicalReplay(
        ProvenanceLineageGraph expected,
        ProvenanceLineageGraph actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Root, actual.Root);
        Assert.Equal(expected.Sha256, actual.Sha256);
        Assert.True(expected.CanonicalBytes.AsSpan().SequenceEqual(actual.CanonicalBytes.AsSpan()));
        Assert.Equal(
            expected.Nodes.Select(static node => node.Id),
            actual.Nodes.Select(static node => node.Id));
        for (var index = 0; index < expected.Nodes.Length; index++)
        {
            Assert.True(expected.Nodes[index].CanonicalBytes.AsSpan().SequenceEqual(
                actual.Nodes[index].CanonicalBytes.AsSpan()));
        }
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

    private static ImmutableArray<ModelAttemptProjection> ProjectAttempts(
        ImmutableArray<PureModelAttempt> attempts) =>
        attempts.Select(static attempt => new ModelAttemptProjection(
            attempt.CallSite,
            attempt.ModelIdentity,
            attempt.EnteredLogicalDepth,
            attempt.OutcomeKind,
            attempt.TransferCompleted,
            attempt.StableCode)).ToImmutableArray();

    private static ImmutableArray<ExpectedEvent> EventFingerprint(IEnumerable<DebugEvent> events) =>
        events.Select(static item => new ExpectedEvent(
            item.Kind,
            item.Method,
            item.IlOffset,
            item.Instruction,
            item.FieldEvidence?.Sha256)).ToImmutableArray();

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
            "The W4.6d compiler-emitted modeled graph did not stop within 100 deterministic steps.");
    }

    private static int ReadCoreClrOracle()
    {
        var probeType = Assembly.LoadFile(Path.GetFullPath(ResolveTargetAssemblyPath()))
            .GetType("DumpProbe", throwOnError: true)!;
        var constructor = probeType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(int), typeof(string)],
            modifiers: null) ??
            throw new InvalidOperationException("Could not find the DumpProbe constructor.");
        var probe = constructor.Invoke([ExpectedMarker, "w4.6d-coreclr-oracle"]);
        var caller = probeType.GetMethod(
            CallerName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"Could not find DumpProbe.{CallerName}.");
        return Assert.IsType<int>(caller.Invoke(probe, parameters: null));
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
                0x0000_0001_2345_6800UL + checked((ulong)ordinal * 0x10UL),
                sizeof(int),
                observedBytes);
        }
    }

    private sealed class PreparedFixture : IDisposable
    {
        private PreparedFixture(
            SrmMetadataModule module,
            CountingResolutionServices resolution,
            CompilerRegistry registry,
            CompilerModel model,
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
            Registry = registry;
            Model = model;
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

        internal SrmMetadataModule Module { get; }

        internal CountingResolutionServices Resolution { get; }

        internal CompilerRegistry Registry { get; }

        internal CompilerModel Model { get; }

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
                var resolution = new CountingResolutionServices(
                    new MetadataResolutionServices(module),
                    helper);
                var registry = new CompilerRegistry();
                var result = new MethodGraphPlanner(resolution)
                    .RequirePureModel(caller, helper, registry);
                Assert.True(result.IsSuccess, result.Failure?.Code);
                var plan = Assert.IsType<FrozenMethodGraphPlan>(result.Plan);
                Assert.True(plan.TryGetNode(caller, out var callerNode));
                Assert.NotNull(callerNode);
                Assert.False(plan.TryGetNode(helper, out _));

                var leaf = Assert.Single(plan.ModeledLeaves);
                Assert.Equal(helper, leaf.Method);
                Assert.Equal(ModelIdentity, leaf.Descriptor.Identity);
                Assert.Equal(PureCallModelConfidence.Exact, leaf.Descriptor.Confidence);
                Assert.Equal(EvaluationEffectStatus.None, leaf.Effects);
                Assert.Equal(5, plan.TraversalUnitCount);
                Assert.Equal(2, plan.RequiredLogicalDepth);
                Assert.Equal(new ResolutionCounts(1, 2, 1), resolution.Counts);
                Assert.Equal(1, registry.SelectionCount);
                Assert.Equal(leaf.Target, Assert.Single(registry.Targets));

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
                    registry,
                    Assert.IsType<CompilerModel>(registry.SelectedModel),
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

    private sealed class CompilerRegistry : IPureCallModelRegistry
    {
        internal int SelectionCount { get; private set; }

        internal List<ResolvedMethodCallTarget> Targets { get; } = [];

        internal IPureCallModel? SelectedModel { get; private set; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            SelectionCount++;
            Targets.Add(target);
            var descriptor = new PureCallModelDescriptor(
                ModelIdentity,
                target,
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None);
            var model = new CompilerModel(descriptor);
            SelectedModel = model;
            return PureCallModelSelectionResult.Selected(model);
        }
    }

    private sealed class CompilerModel(PureCallModelDescriptor descriptor) : IPureCallModel
    {
        private readonly PureCallModelDescriptor descriptor = descriptor;

        public PureCallModelDescriptor Descriptor
        {
            get
            {
                DescriptorReadCount++;
                return descriptor;
            }
        }

        internal int DescriptorReadCount { get; private set; }

        internal List<PureCallModelInvocation> Invocations { get; } = [];

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            Invocations.Add(invocation);
            if (invocation.Arguments.Any(static argument =>
                    argument.Kind == PureCallModelArgumentKind.ExplainedUnknownInt32))
            {
                return PureCallModelOutcome.UnknownReturn();
            }

            return PureCallModelOutcome.ExactReturn(unchecked(
                invocation.Arguments[0].Int32Value!.Value +
                invocation.Arguments[1].Int32Value!.Value));
        }
    }

    private sealed class CountingResolutionServices(
        IResolutionServices inner,
        MethodHandle forbiddenDefinition) : IResolutionServices
    {
        internal ResolutionCounts Counts { get; private set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            if (method == forbiddenDefinition)
            {
                throw new InvalidOperationException(
                    "The selected compiler-emitted pure-model target body must remain opaque.");
            }

            Counts = Counts with { MethodDefinitions = Counts.MethodDefinitions + 1 };
            return inner.GetMethodDefinition(method);
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Counts = Counts with { Methods = Counts.Methods + 1 };
            return inner.ResolveMethod(contextMethod, metadataToken);
        }

        public ResolutionResult<ResolvedField> ResolveField(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Counts = Counts with { Fields = Counts.Fields + 1 };
            return inner.ResolveField(contextMethod, metadataToken);
        }
    }

    private readonly record struct ResolutionCounts(int MethodDefinitions, int Fields, int Methods);

    private readonly record struct InterpretedResult(
        ValuePrecisionKind Precision,
        int? ExactResult);

    private sealed record ScenarioRun(
        int? ExactResult,
        ProvenanceLineageGraph? Graph,
        int InternedNodeCount,
        MachineOperationalState OperationalState,
        ImmutableArray<ModelAttemptProjection> Attempts,
        ImmutableArray<ExpectedEvent> Events,
        ImmutableArray<FieldHandle> LoadedFields,
        PureCallModelInvocation Invocation,
        ResolutionCounts ResolutionBeforeExecution,
        ResolutionCounts ResolutionAfterExecution,
        int SelectionCountBeforeExecution,
        int SelectionCountAfterExecution,
        int DescriptorReadsBeforeExecution,
        int DescriptorReadsAfterExecution,
        int ModelInvocationsBeforeExecution,
        int ModelInvocationsAfterExecution);

    private readonly record struct ModelAttemptProjection(
        DirectCallSiteIdentity CallSite,
        PureCallModelIdentity ModelIdentity,
        int EnteredLogicalDepth,
        PureModelAttemptOutcomeKind OutcomeKind,
        bool TransferCompleted,
        string? StableCode);

    private readonly record struct ExpectedEvent(
        DebugEventKind Kind,
        MethodHandle Method,
        int IlOffset,
        string Instruction,
        string? FieldEvidenceSha256);
}
