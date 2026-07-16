using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Product.DumpDebugging;
using Xunit;

namespace Interpreter.Tests;

/// <summary>
/// Exercises W4.8's authoritative rooted runner across exact, degraded, bounded, cancelled, and failed execution.
/// </summary>
public sealed class CounterfactualMethodRunnerExecutionTests
{
    private const string DigestA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string DigestB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string SyntheticId = "fixture.synthetic.runner.v1";
    private const int ExpectedValue = 0x26AF37BD;
    private static readonly PureCallModelVersion Version = new(1, 0, 0);
    private static readonly MethodHandle Root = MethodGraphPlannerTests.Method(1);
    private static readonly MethodHandle Helper = MethodGraphPlannerTests.Method(2);
    private static readonly TypeSig Owner = MethodGraphPlannerTests.RootType;
    private static readonly ResolvedField FirstField = MethodGraphPlannerTests.Field(1, Owner);
    private static readonly ResolvedField SecondField = MethodGraphPlannerTests.Field(2, Owner);

    /// <summary>
    /// Proves null and foreign plan authority failures precede cancellation and every retained execution capability.
    /// </summary>
    [Fact]
    public void NullAndForeignPlansRejectBeforeCancellationAndRetainedCapabilities()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var owner = CreateFixture();
        var foreignRunner = new CounterfactualMethodRunner<ConcreteMemory>();
        var operationsBefore = owner.Resolver.Operations.ToArray();

        var missing = foreignRunner.Run(null, cancellation.Token);
        var foreign = foreignRunner.Run(owner.Plan, cancellation.Token);

        AssertFacadeRejection(missing, "W4.Request.PlanMissing");
        AssertFacadeRejection(foreign, "W4.Request.PlanForeign");
        Assert.Equal(operationsBefore, owner.Resolver.Operations);
        Assert.Equal(0, owner.Memory.LoadCount);
        Assert.Equal(0, owner.Model?.InvocationCount ?? 0);
    }

    /// <summary>
    /// Executes the complete interpreted graph exactly, checks every visible axis, and proves execution performs no
    /// metadata resolution, graph traversal, registry selection, or memory write after preparation.
    /// </summary>
    [Fact]
    public void ExactInterpretedRunCompletesWithoutRequeryOrMutation()
    {
        var fixture = CreateFixture();
        var operationsBefore = fixture.Resolver.Operations.ToArray();

        var result = fixture.Runner.Run(fixture.Plan);

        Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
        Assert.Equal(EvaluationCompleteness.Complete, result.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Exact, result.Evidence);
        Assert.Equal(EvaluationEffectStatus.None, result.Effects);
        Assert.Equal(CounterfactualExecutionValueKind.ExactReturn, result.Value!.Kind);
        Assert.Equal(ExpectedValue, result.Value.ExactInt32);
        Assert.Equal(CounterfactualExecutionOriginKind.RootedFacade, result.Context.Origin);
        Assert.Same(fixture.Plan.Request, result.Context.Request);
        Assert.Equal(fixture.Plan.Sha256, result.Context.PlanSha256);
        Assert.Equal(CounterfactualBoundStatus.Applied, result.Context.Accounting.InstructionStatus);
        Assert.Equal((10L, fixture.Plan.Request.InstructionLimit - 10),
            (result.Context.Accounting.InstructionUsed, result.Context.Accounting.InstructionRemaining));
        Assert.Equal((2, 2),
            (result.Context.Accounting.ObservedLogicalDepthHighWater,
                result.Context.Accounting.ActiveFrameDepthHighWater));
        Assert.Equal(CounterfactualBoundStatus.NotReached, result.Context.Accounting.LineageStatus);
        Assert.True(result.Context.CallTrace.SequenceEqual([Root, Helper]));
        Assert.Equal(10, result.Context.Events.Count(static item => item.Kind == DebugEventKind.InstructionExecuted));
        Assert.True(result.Context.ReachedFieldLoadOrdinals.SequenceEqual([0, 1]));
        Assert.Empty(result.Context.ModelAttempts);
        Assert.Single(result.Provenance);
        Assert.StartsWith("counterfactual-request-sha256:", result.Provenance[0].SourceId);
        Assert.Empty(result.Diagnostics);
        Assert.True(result.IsDeterministicReplay);
        Assert.Equal("8eb243d415e739a9101f0dd7b8e1cdb4de38208069caa495b8c1bd02a37b7d09", result.Sha256);
        AssertCanonicalIntegrity(result);
        Assert.Equal(operationsBefore, fixture.Resolver.Operations);
        Assert.Equal(2, fixture.Memory.LoadCount);
        Assert.Equal(0, fixture.Memory.WriteCount);
    }

    /// <summary>
    /// Proves each admitted non-exact field status completes with a typed unknown, reached aggregate, precision event,
    /// bounded canonical lineage, and no fabricated scalar fallback.
    /// </summary>
    [Theory]
    [InlineData(EvaluationEvidenceStatus.Partial)]
    [InlineData(EvaluationEvidenceStatus.Unavailable)]
    public void DegradedInterpretedRunCompletesWithCanonicalUnknown(EvaluationEvidenceStatus evidence)
    {
        var fixture = CreateFixture(firstEvidence: evidence);

        var result = fixture.Runner.Run(fixture.Plan);

        Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
        Assert.Equal(EvaluationCompleteness.Partial, result.Completeness);
        Assert.Equal(evidence, result.Evidence);
        Assert.Equal(CounterfactualExecutionValueKind.UnknownReturn, result.Value!.Kind);
        Assert.Equal(TypeSig.Int32, result.Value.StaticType);
        Assert.Null(result.Value.ExactInt32);
        var lineage = Assert.IsType<ProvenanceLineageGraph>(result.Value.Lineage);
        Assert.Equal(CounterfactualBoundStatus.Applied, result.Context.Accounting.LineageStatus);
        Assert.Equal(lineage.Nodes.Length, result.Context.Accounting.LineageNodeCount);
        Assert.True(lineage.Nodes.Length <= fixture.Plan.Request.LineageNodeCeiling);
        Assert.Single(result.Context.Events.Where(static item => item.Kind == DebugEventKind.ValuePrecisionLost));
        Assert.Equal(10, result.Context.Accounting.InstructionUsed);
        AssertCanonicalIntegrity(result);

        var replayDomain = new ProvenanceConcreteDomain();
        var replayed = replayDomain.ReplayLineage(lineage);
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, replayDomain.GetPrecision(replayed));
        Assert.Equal(lineage.Sha256, replayDomain.CaptureLineage(replayed).Sha256);
    }

    /// <summary>
    /// Distinguishes zero-unit exhaustion from a truthful positive prefix and retains only field evidence actually
    /// reached before the next denied instruction request.
    /// </summary>
    [Fact]
    public void InstructionExhaustionIsPathAccurateAtZeroAndAfterPrefix()
    {
        var zero = CreateFixture(instructionLimit: 0);
        var prefix = CreateFixture(
            instructionLimit: 2,
            firstEvidence: EvaluationEvidenceStatus.Partial);

        var zeroResult = zero.Runner.Run(zero.Plan);
        var prefixResult = prefix.Runner.Run(prefix.Plan);

        Assert.Equal(EvaluationCompletionStatus.BudgetExhausted, zeroResult.Completion);
        Assert.Equal(EvaluationCompleteness.None, zeroResult.Completeness);
        Assert.Null(zeroResult.Value);
        Assert.Equal(CounterfactualBoundStatus.Exhausted, zeroResult.Context.Accounting.InstructionStatus);
        Assert.Equal((0L, 0L),
            (zeroResult.Context.Accounting.InstructionUsed, zeroResult.Context.Accounting.InstructionRemaining));
        Assert.Empty(zeroResult.Context.Events);
        Assert.Empty(zeroResult.Context.ReachedFieldObservations);
        Assert.True(zeroResult.Context.CallTrace.SequenceEqual([Root]));

        Assert.Equal(EvaluationCompletionStatus.BudgetExhausted, prefixResult.Completion);
        Assert.Equal(EvaluationCompleteness.Partial, prefixResult.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Partial, prefixResult.Evidence);
        Assert.Equal(CounterfactualExecutionValueKind.ExecutionPrefix, prefixResult.Value!.Kind);
        Assert.Equal((2L, 0L),
            (prefixResult.Context.Accounting.InstructionUsed, prefixResult.Context.Accounting.InstructionRemaining));
        Assert.Equal(2, prefixResult.Context.Events.Count(static item =>
            item.Kind == DebugEventKind.InstructionExecuted));
        Assert.Single(prefixResult.Context.Events.Where(static item =>
            item.Kind == DebugEventKind.ValuePrecisionLost));
        Assert.True(prefixResult.Context.ReachedFieldLoadOrdinals.SequenceEqual([0]));
        Assert.Equal("W4.Budget.Instruction", Assert.Single(prefixResult.Diagnostics).Code);
        AssertCanonicalIntegrity(zeroResult);
        AssertCanonicalIntegrity(prefixResult);
    }

    /// <summary>
    /// Observes host cancellation only at ready boundaries, separating pre-instruction cancellation from a completed
    /// two-instruction field-load prefix and excluding both results from deterministic replay claims.
    /// </summary>
    [Fact]
    public void CancellationIsObservedOnlyAtReadyBoundaries()
    {
        using var beforeSource = new CancellationTokenSource();
        beforeSource.Cancel();
        var before = CreateFixture();
        using var prefixSource = new CancellationTokenSource();
        var prefix = CreateFixture(
            firstEvidence: EvaluationEvidenceStatus.Partial,
            cancelAfterLoad: (prefixSource, 1));

        var beforeResult = before.Runner.Run(before.Plan, beforeSource.Token);
        var prefixResult = prefix.Runner.Run(prefix.Plan, prefixSource.Token);

        Assert.Equal(EvaluationCompletionStatus.Cancelled, beforeResult.Completion);
        Assert.Equal(EvaluationCompleteness.None, beforeResult.Completeness);
        Assert.Null(beforeResult.Value);
        Assert.Equal(CounterfactualBoundStatus.NotReached, beforeResult.Context.Accounting.InstructionStatus);
        Assert.Null(beforeResult.Context.Accounting.InstructionUsed);
        Assert.Equal(CounterfactualBoundStatus.Applied, beforeResult.Context.Accounting.DepthStatus);
        Assert.True(beforeResult.Context.CallTrace.SequenceEqual([Root]));
        Assert.Equal(0, before.Memory.LoadCount);
        Assert.False(beforeResult.IsDeterministicReplay);

        Assert.Equal(EvaluationCompletionStatus.Cancelled, prefixResult.Completion);
        Assert.Equal(EvaluationCompleteness.Partial, prefixResult.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Partial, prefixResult.Evidence);
        Assert.Equal(CounterfactualExecutionValueKind.ExecutionPrefix, prefixResult.Value!.Kind);
        Assert.Equal(2, prefixResult.Context.Accounting.InstructionUsed);
        Assert.True(prefixResult.Context.ReachedFieldLoadOrdinals.SequenceEqual([0]));
        Assert.Equal(1, prefix.Memory.LoadCount);
        Assert.False(prefixResult.IsDeterministicReplay);
        Assert.Equal("W4.Execution.Cancelled", Assert.Single(prefixResult.Diagnostics).Code);
    }

    /// <summary>
    /// Preserves a reached conflict as blocked evidence and turns disagreement between exact frozen observation and
    /// runtime memory into invalidity, with neither failed load consuming budget nor emitting an execution event.
    /// </summary>
    [Fact]
    public void ConflictAndObservationMismatchStopAtomically()
    {
        var conflict = CreateFixture(firstEvidence: EvaluationEvidenceStatus.Conflict);
        var mismatch = CreateFixture(mismatchFirstExactLoad: true);

        var conflictResult = conflict.Runner.Run(conflict.Plan);
        var mismatchResult = mismatch.Runner.Run(mismatch.Plan);

        Assert.Equal(EvaluationCompletionStatus.Blocked, conflictResult.Completion);
        Assert.Equal(EvaluationEvidenceStatus.Conflict, conflictResult.Evidence);
        Assert.Equal(1, conflictResult.Context.Accounting.InstructionUsed);
        Assert.True(conflictResult.Context.ReachedFieldLoadOrdinals.SequenceEqual([0]));
        Assert.Single(conflictResult.Context.Events);
        Assert.Equal("W4.Evidence.FieldConflict", Assert.Single(conflictResult.Diagnostics).Code);

        Assert.Equal(EvaluationCompletionStatus.Invalid, mismatchResult.Completion);
        Assert.Equal(EvaluationEvidenceStatus.Invalid, mismatchResult.Evidence);
        Assert.Equal(1, mismatchResult.Context.Accounting.InstructionUsed);
        Assert.True(mismatchResult.Context.ReachedFieldLoadOrdinals.SequenceEqual([0]));
        Assert.Single(mismatchResult.Context.Events);
        Assert.Equal("W4.Replay.ExecutionInvalid", Assert.Single(mismatchResult.Diagnostics).Code);
        Assert.Null(conflictResult.Value);
        Assert.Null(mismatchResult.Value);
    }

    /// <summary>
    /// Executes the selected frozen model for exact and degraded inputs, retaining one model attempt, a logical depth
    /// of two without a helper frame, and exact versus modeled-return lineage as appropriate.
    /// </summary>
    [Fact]
    public void ModeledExactAndUnknownReturnsRetainAttemptAndDepthTruth()
    {
        var exact = CreateFixture(modeled: true);
        var unknown = CreateFixture(modeled: true, firstEvidence: EvaluationEvidenceStatus.Unavailable);

        var exactResult = exact.Runner.Run(exact.Plan);
        var unknownResult = unknown.Runner.Run(unknown.Plan);

        Assert.Equal(ExpectedValue, exactResult.Value!.ExactInt32);
        Assert.Equal(EvaluationCompleteness.Complete, exactResult.Completeness);
        Assert.Equal(CounterfactualExecutionValueKind.UnknownReturn, unknownResult.Value!.Kind);
        Assert.Equal(EvaluationEvidenceStatus.Unavailable, unknownResult.Evidence);
        foreach (var result in new[] { exactResult, unknownResult })
        {
            Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
            Assert.Equal(6, result.Context.Accounting.InstructionUsed);
            Assert.Equal((2, 1),
                (result.Context.Accounting.ObservedLogicalDepthHighWater,
                    result.Context.Accounting.ActiveFrameDepthHighWater));
            Assert.True(result.Context.CallTrace.SequenceEqual([Root, Helper]));
            var attempt = Assert.Single(result.Context.ModelAttempts);
            Assert.True(attempt.TransferCompleted);
            Assert.Equal(2, attempt.EnteredLogicalDepth);
            Assert.Equal(1, result.Context.ModelInvocationCount);
            Assert.Equal(1, result.Context.CompletedModeledCallCount);
            AssertCanonicalIntegrity(result);
        }

        Assert.Equal(1, exact.Model!.InvocationCount);
        Assert.Equal(1, unknown.Model!.InvocationCount);
        Assert.Equal(PureModelAttemptOutcomeKind.ExactReturn,
            Assert.Single(exactResult.Context.ModelAttempts).OutcomeKind);
        Assert.Equal(PureModelAttemptOutcomeKind.UnknownReturn,
            Assert.Single(unknownResult.Context.ModelAttempts).OutcomeKind);
    }

    /// <summary>
    /// Normalizes blocked, invalid, and throwing model outcomes into typed nontransferring results whose attempt and
    /// logical-depth evidence advance while semantic state, instruction budget, and call event remain unchanged.
    /// </summary>
    [Fact]
    public void ModelFailuresAreTypedAtomicAndPayloadOmitting()
    {
        var cases = new[]
        {
            (ModelBehavior.Blocked, EvaluationCompletionStatus.Blocked, "W4.Model.FixtureBlocked"),
            (ModelBehavior.Invalid, EvaluationCompletionStatus.Invalid, "W4.Model.FixtureInvalid"),
            (ModelBehavior.Throw, EvaluationCompletionStatus.Blocked, "W4.Model.Capability"),
        };

        foreach (var (behavior, completion, diagnostic) in cases)
        {
            var fixture = CreateFixture(modeled: true, modelBehavior: behavior);
            var result = fixture.Runner.Run(fixture.Plan);

            Assert.Equal(completion, result.Completion);
            Assert.Equal(EvaluationCompleteness.None, result.Completeness);
            Assert.Null(result.Value);
            Assert.Equal(4, result.Context.Accounting.InstructionUsed);
            Assert.Equal((2, 1),
                (result.Context.Accounting.ObservedLogicalDepthHighWater,
                    result.Context.Accounting.ActiveFrameDepthHighWater));
            Assert.True(result.Context.CallTrace.SequenceEqual([Root, Helper]));
            var attempt = Assert.Single(result.Context.ModelAttempts);
            Assert.False(attempt.TransferCompleted);
            Assert.Equal(1, result.Context.ModelInvocationCount);
            Assert.Equal(0, result.Context.CompletedModeledCallCount);
            Assert.Equal(4, result.Context.Events.Count(static item =>
                item.Kind == DebugEventKind.InstructionExecuted));
            Assert.DoesNotContain(result.Context.Events, static item =>
                item.Kind == DebugEventKind.InstructionExecuted && item.Instruction == "Call");
            Assert.Equal(diagnostic, Assert.Single(result.Diagnostics).Code);
            Assert.DoesNotContain("fixture model detail", result.Diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
            AssertCanonicalIntegrity(result);
        }
    }

    /// <summary>
    /// Reconstructs the entire runner graph with fresh issuers, domains, memory models, and resolvers and requires
    /// byte-identical request, plan, and result replay for interpreted and modeled dispositions.
    /// </summary>
    [Fact]
    public void FreshRunnerObjectsReplayCanonicalArtifactsAndGraphCost()
    {
        foreach (var modeled in new[] { false, true })
        {
            var first = CreateFixture(modeled: modeled, firstEvidence: EvaluationEvidenceStatus.Partial);
            var second = CreateFixture(modeled: modeled, firstEvidence: EvaluationEvidenceStatus.Partial);

            var firstResult = first.Runner.Run(first.Plan);
            var secondResult = second.Runner.Run(second.Plan);

            Assert.NotSame(first.Runner, second.Runner);
            Assert.NotSame(first.Plan, second.Plan);
            Assert.Equal(first.Plan.Request.Sha256, second.Plan.Request.Sha256);
            Assert.Equal(first.Plan.Sha256, second.Plan.Sha256);
            Assert.True(first.Plan.CanonicalBytes.AsSpan().SequenceEqual(second.Plan.CanonicalBytes.AsSpan()));
            Assert.Equal(firstResult.Sha256, secondResult.Sha256);
            Assert.True(firstResult.CanonicalBytes.AsSpan().SequenceEqual(secondResult.CanonicalBytes.AsSpan()));
            Assert.True(CounterfactualMethodRunner<ConcreteMemory>.TryCalculateDynamicInstructionCost(
                first.Plan.RuntimeGraph,
                long.MaxValue,
                out var dynamicCost));
            Assert.Equal(modeled ? 6 : 10, dynamicCost);
            Assert.True(CounterfactualMethodRunner<ConcreteMemory>.TryCalculateDynamicInstructionCost(
                first.Plan.RuntimeGraph,
                5,
                out var saturatedCost));
            Assert.Equal(5, saturatedCost);
        }
    }

    private static ExecutionFixture CreateFixture(
        long instructionLimit = 32,
        EvaluationEvidenceStatus firstEvidence = EvaluationEvidenceStatus.Exact,
        EvaluationEvidenceStatus secondEvidence = EvaluationEvidenceStatus.Exact,
        bool modeled = false,
        ModelBehavior modelBehavior = ModelBehavior.Functional,
        (CancellationTokenSource Source, int LoadCount)? cancelAfterLoad = null,
        bool mismatchFirstExactLoad = false)
    {
        var domain = new ProvenanceConcreteDomain();
        var resolver = CreateResolver();
        var observations = ImmutableArray.Create(
            CreateObservation(0, FirstField, firstEvidence),
            CreateObservation(1, SecondField, secondEvidence));
        var memory = new FixtureMemoryModel(
            domain,
            observations,
            cancelAfterLoad,
            mismatchFirstExactLoad);
        FixtureModel? model = null;
        FixtureRegistry? registry = null;
        if (modeled)
        {
            model = new FixtureModel(MethodGraphPlannerTests.Target(Helper), modelBehavior);
            registry = new FixtureRegistry(model);
        }

        var runner = new CounterfactualMethodRunner<ConcreteMemory>();
        var candidate = CounterfactualMethodPreparationCandidate<ConcreteMemory>.CreateSynthetic(
            SyntheticId,
            "root.selection.runner.v1",
            DigestA,
            Root,
            CounterfactualInputEvidence.CreateExactNonNullReceiver(Owner, "receiver.runner", DigestB),
            [],
            "policy.counterfactual.runner",
            Version,
            instructionLimit,
            logicalDepthLimit: 2,
            traversalLimit: 10,
            "catalog.counterfactual.runner",
            Version,
            modeled ? Helper : null,
            ["assume.read-only", "assume.counterfactual-not-historical"],
            resolver,
            domain,
            memory,
            ConcreteMemory.Empty,
            domain.ObjectReference(7, Owner),
            observations,
            registry);
        var preparation = runner.Prepare(candidate);
        Assert.True(preparation.IsSuccess, preparation.Failure?.Diagnostics[0].Code);
        return new ExecutionFixture(runner, preparation.Plan!, resolver, memory, registry, model);
    }

    private static MethodGraphPlannerTests.GraphResolver CreateResolver()
    {
        var rootDefinition = MethodGraphPlannerTests.RootDefinition(
            Root,
            MethodGraphPlannerTests.ExactRootBody(
                FirstField.Handle.MetadataToken,
                SecondField.Handle.MetadataToken,
                Helper.MetadataToken),
            maxStack: 2);
        var helperDefinition = MethodGraphPlannerTests.HelperDefinition(
            Helper,
            [0x02, 0x03, 0x58, 0x2A],
            maxStack: 2);
        var resolver = MethodGraphPlannerTests.Resolver(rootDefinition, helperDefinition);
        resolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        resolver.Fields[(Root, SecondField.Handle.MetadataToken)] = SecondField;
        resolver.Calls[(Root, Helper.MetadataToken)] = MethodGraphPlannerTests.Target(Helper);
        return resolver;
    }

    private static CounterfactualFieldObservation CreateObservation(
        int ordinal,
        ResolvedField field,
        EvaluationEvidenceStatus evidence)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, ordinal == 0 ? 0x13579BDF : 0x13579BDE);
        return evidence switch
        {
            EvaluationEvidenceStatus.Exact => CounterfactualFieldObservation.CreateExactInt32(
                ordinal,
                field,
                SourceSha256(),
                DigestB,
                checked(0x1000UL + ((ulong)ordinal * sizeof(int))),
                sizeof(int),
                bytes),
            EvaluationEvidenceStatus.Partial => CounterfactualFieldObservation.CreateNonExactInt32(
                ordinal,
                field,
                evidence,
                "W4.Field.Partial",
                SourceSha256(),
                DigestB,
                checked(0x1000UL + ((ulong)ordinal * sizeof(int))),
                sizeof(int),
                bytes[..2]),
            EvaluationEvidenceStatus.Unavailable => CounterfactualFieldObservation.CreateNonExactInt32(
                ordinal,
                field,
                evidence,
                "W4.Field.Unavailable",
                SourceSha256(),
                DigestB,
                checked(0x1000UL + ((ulong)ordinal * sizeof(int))),
                sizeof(int),
                []),
            EvaluationEvidenceStatus.Conflict or EvaluationEvidenceStatus.Invalid =>
                CounterfactualFieldObservation.CreateNonExactInt32(
                    ordinal,
                    field,
                    evidence,
                    $"W4.Field.{evidence}",
                    SourceSha256(),
                    DigestB,
                    checked(0x1000UL + ((ulong)ordinal * sizeof(int))),
                    sizeof(int),
                    bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(evidence)),
        };
    }

    private static string SourceSha256() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SyntheticId))).ToLowerInvariant();

    private static void AssertFacadeRejection(CounterfactualExecutionResult result, string code)
    {
        Assert.Equal(CounterfactualExecutionOriginKind.FacadeRejection, result.Context.Origin);
        Assert.Equal(EvaluationCompletionStatus.Invalid, result.Completion);
        Assert.Equal(EvaluationCompleteness.None, result.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Invalid, result.Evidence);
        Assert.Null(result.Value);
        Assert.Null(result.Context.Request);
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, result.Context.Accounting.InstructionStatus);
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, result.Context.Accounting.TraversalStatus);
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, result.Context.Accounting.DepthStatus);
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, result.Context.Accounting.LineageStatus);
        Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
        AssertCanonicalIntegrity(result);
    }

    private static void AssertCanonicalIntegrity(CounterfactualExecutionResult result)
    {
        Assert.Equal(result.Sha256, CounterfactualExecutionCanonicalCodec.ComputeSha256(result));
        Assert.True(result.CanonicalBytes.AsSpan().SequenceEqual(
            CounterfactualExecutionCanonicalCodec.SerializeCanonical(result).AsSpan()));
    }

    private sealed class ExecutionFixture(
        CounterfactualMethodRunner<ConcreteMemory> runner,
        CounterfactualMethodPlan<ConcreteMemory> plan,
        MethodGraphPlannerTests.GraphResolver resolver,
        FixtureMemoryModel memory,
        FixtureRegistry? registry,
        FixtureModel? model)
    {
        internal CounterfactualMethodRunner<ConcreteMemory> Runner { get; } = runner;

        internal CounterfactualMethodPlan<ConcreteMemory> Plan { get; } = plan;

        internal MethodGraphPlannerTests.GraphResolver Resolver { get; } = resolver;

        internal FixtureMemoryModel Memory { get; } = memory;

        internal FixtureRegistry? Registry { get; } = registry;

        internal FixtureModel? Model { get; } = model;

    }

    private enum ModelBehavior
    {
        Functional,
        Blocked,
        Invalid,
        Throw,
    }

    private sealed class FixtureRegistry(IPureCallModel model) : IPureCallModelRegistry
    {
        internal int SelectionCount { get; private set; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            SelectionCount++;
            return PureCallModelSelectionResult.Selected(model);
        }
    }

    private sealed class FixtureModel(
        ResolvedMethodCallTarget target,
        ModelBehavior behavior) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = new(
            new PureCallModelIdentity("w4.runner.model", Version),
            target,
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None);

        internal int InvocationCount { get; private set; }

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            InvocationCount++;
            return behavior switch
            {
                ModelBehavior.Functional when invocation.Arguments.Any(static item =>
                    item.Kind == PureCallModelArgumentKind.ExplainedUnknownInt32) =>
                    PureCallModelOutcome.UnknownReturn(),
                ModelBehavior.Functional => PureCallModelOutcome.ExactReturn(unchecked(
                    invocation.Arguments[0].Int32Value!.Value + invocation.Arguments[1].Int32Value!.Value)),
                ModelBehavior.Blocked => PureCallModelOutcome.Blocked("W4.Model.FixtureBlocked"),
                ModelBehavior.Invalid => PureCallModelOutcome.Invalid("W4.Model.FixtureInvalid"),
                ModelBehavior.Throw => throw new InvalidOperationException("fixture model detail must not escape"),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
    }

    private sealed class FixtureMemoryModel : IMemoryModel<ProvenanceConcreteValue, ConcreteMemory>
    {
        private readonly ProvenanceConcreteDomain domain;
        private readonly Dictionary<ResolvedField, CounterfactualFieldObservation> observations;
        private readonly (CancellationTokenSource Source, int LoadCount)? cancelAfterLoad;
        private readonly bool mismatchFirstExactLoad;

        internal FixtureMemoryModel(
            ProvenanceConcreteDomain domain,
            ImmutableArray<CounterfactualFieldObservation> observations,
            (CancellationTokenSource Source, int LoadCount)? cancelAfterLoad,
            bool mismatchFirstExactLoad)
        {
            this.domain = domain;
            this.observations = observations.ToDictionary(static item => item.Field);
            this.cancelAfterLoad = cancelAfterLoad;
            this.mismatchFirstExactLoad = mismatchFirstExactLoad;
        }

        public bool CanAllocate => false;

        internal int LoadCount { get; private set; }

        internal int WriteCount { get; private set; }

        public (ProvenanceConcreteValue objRef, ConcreteMemory mem) NewObject(ConcreteMemory mem, TypeSig type) =>
            throw new InvalidOperationException("The read-only runner cannot allocate an object.");

        public (ProvenanceConcreteValue arrRef, ConcreteMemory mem) NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length) =>
            throw new InvalidOperationException("The read-only runner cannot allocate an array.");

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field)
        {
            LoadCount++;
            if (cancelAfterLoad is { } cancellation && LoadCount == cancellation.LoadCount)
            {
                cancellation.Source.Cancel();
            }

            var observation = observations[field];
            if (mismatchFirstExactLoad && observation.DependencyOrdinal == 0)
            {
                return MemoryLoadResult<ProvenanceConcreteValue>.Exact(
                    domain.ConstInt32(observation.ExactInt32!.Value + 1));
            }

            return observation.EvidenceStatus switch
            {
                EvaluationEvidenceStatus.Exact => MemoryLoadResult<ProvenanceConcreteValue>.Exact(
                    domain.ConstInt32(observation.ExactInt32!.Value)),
                EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable =>
                    MemoryLoadResult<ProvenanceConcreteValue>.FromFieldEvidence(
                        observation.RuntimeFieldEvidence!),
                EvaluationEvidenceStatus.Conflict => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                    MemoryLoadKind.Conflict,
                    observation.ReasonCode!),
                EvaluationEvidenceStatus.Invalid => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                    MemoryLoadKind.Invalid,
                    observation.ReasonCode!),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value)
        {
            WriteCount++;
            throw new InvalidOperationException("The read-only runner cannot write a field.");
        }

        public ProvenanceConcreteValue LoadElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index) =>
            throw new InvalidOperationException("The closed runner cannot read an array element.");

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value)
        {
            WriteCount++;
            throw new InvalidOperationException("The read-only runner cannot write an array element.");
        }
    }
}
