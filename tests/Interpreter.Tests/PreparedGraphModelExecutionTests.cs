using System.Collections.Immutable;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Exercises W4.6c frozen pure-model dispatch, atomic caller transfer, operational attempts, and split depth facts.
/// </summary>
public sealed class PreparedGraphModelExecutionTests
{
    private static readonly ModuleHandle Module = new(
        0xC601020304050607,
        0xC608091011121314);
    private static readonly TypeSig RootType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.ModeledRoot");
    private static readonly TypeSig MiddleType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000002,
        "Interpreter.Tests.ModeledMiddle");
    private static readonly TypeSig ModelType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000003,
        "Interpreter.Tests.OpaqueModelTarget");
    private static readonly MethodHandle Root = Method(1);
    private static readonly MethodHandle Middle = Method(2);
    private static readonly MethodHandle ModelTarget = Method(3);
    private static readonly ResolvedField FirstField = Field(1);
    private static readonly ResolvedField SecondField = Field(2);
    private static readonly PureCallModelIdentity ModelIdentity = new(
        "w4.combine-markers",
        new PureCallModelVersion(1, 0, 0));

    /// <summary>
    /// Proves an exact model executes once from frozen capability alone, atomically replaces two arguments with one
    /// result, preserves memory, consumes one instruction, emits no frame event, and records logical depth two while
    /// active-frame high water remains one.
    /// </summary>
    [Fact]
    public void ExactModelTransfersInCallerWithoutFrameResolverRegistryOrDescriptorReread()
    {
        var fixture = PrepareSingle(ModelBehavior.SumExact);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver);
        var memory = new TestMemory();
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            maximumLogicalCallDepth: 2,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(4));

        var firstLoad = machine.StepOne(activation.State!, operations);
        var secondLoad = machine.StepOne(firstLoad.State, firstLoad.OperationalState);
        var call = machine.StepOne(secondLoad.State, secondLoad.OperationalState);

        Assert.Equal(MachineRunStatus.Ready, call.Status);
        Assert.Same(memory, call.State.Memory);
        var caller = Assert.Single(call.State.CallStack);
        Assert.Equal(7, caller.IlOffset);
        AssertInt32(domain, 3, Assert.Single(caller.EvalStack));
        Assert.Collection(
            call.Events,
            item => AssertEvent(item, DebugEventKind.InstructionExecuted, Root, 2, "Call"));
        Assert.DoesNotContain(
            call.Events,
            item => item.Kind is DebugEventKind.FramePushed or DebugEventKind.FramePopped);
        Assert.Equal(1, call.OperationalState.ModelInvocationCount);
        Assert.Equal(1, call.OperationalState.CompletedModeledCallCount);
        Assert.Equal(2, call.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(1, call.OperationalState.ActiveFrameDepthHighWater);
        var attempt = Assert.Single(call.OperationalState.ModelAttempts);
        AssertAttempt(attempt, Root, 2, ModelTarget, 2, PureModelAttemptOutcomeKind.ExactReturn, true, null);
        var invocation = Assert.Single(fixture.Model.Invocations);
        Assert.Equal(new DirectCallSiteIdentity(Root, 2, ModelTarget), invocation.CallSite);
        Assert.Equal(PureCallModelUnknownPolicy.ExactOnly, invocation.UnknownPolicy);
        Assert.Equal(1, invocation.Arguments[0].Int32Value);
        Assert.Equal(2, invocation.Arguments[1].Int32Value);

        var completed = machine.StepOne(call.State, call.OperationalState);

        Assert.Equal(MachineRunStatus.Completed, completed.Status);
        Assert.Same(memory, completed.State.Memory);
        AssertInt32(domain, 3, completed.State.ReturnValue.Value);
        Assert.Equal(0, completed.OperationalState.Budget.InstructionBudget);
        Assert.Equal(1, fixture.Model.InvocationCount);
        fixture.AssertNoExternalPreparationCapabilityWasRead();
    }

    /// <summary>
    /// Proves a terminal modeled graph rejects an inflated active-frame high-water mark instead of accepting an
    /// operational witness that falsely claims a model frame existed.
    /// </summary>
    [Fact]
    public void CompletedModeledGraphRejectsInflatedActiveFrameHighWater()
    {
        var fixture = PrepareSingle(ModelBehavior.SumExact);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver);
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            maximumLogicalCallDepth: 2,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var run = Run(
            machine,
            activation.State!,
            machine.CreatePreparedOperationalState(new BudgetState(4)));
        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        var forgedOperations = run.Outcome.OperationalState with
        {
            ActiveFrameDepthHighWater = 2,
        };

        var rejected = machine.StepOne(run.Outcome.State, forgedOperations);

        Assert.Equal(MachineRunStatus.InvalidProgram, rejected.Status);
        Assert.Equal("EXEC_CALL_DEPTH_INVARIANT", rejected.Failure?.Code);
        Assert.Same(run.Outcome.State, rejected.State);
        Assert.Same(forgedOperations, rejected.OperationalState);
        Assert.Empty(rejected.Events);
        Assert.Equal(1, fixture.Model.InvocationCount);
        fixture.AssertNoExternalPreparationCapabilityWasRead();
    }

    /// <summary>
    /// Proves repeated modeled edges append ordered attempts and complete twice without allocating model frames.
    /// </summary>
    [Fact]
    public void RepeatedModeledCallsRetainOrderedAttemptsAndMonotonicCounters()
    {
        var fixture = PrepareSingle(ModelBehavior.SumExact, repeated: true);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver);
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();

        var run = Run(machine, activation.State!, machine.CreatePreparedOperationalState(new BudgetState(8)));

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        AssertInt32(domain, 6, run.Outcome.State.ReturnValue.Value);
        Assert.Equal(2, fixture.Model.InvocationCount);
        Assert.Equal(2, run.Outcome.OperationalState.ModelInvocationCount);
        Assert.Equal(2, run.Outcome.OperationalState.CompletedModeledCallCount);
        Assert.Collection(
            run.Outcome.OperationalState.ModelAttempts,
            attempt => AssertAttempt(
                attempt,
                Root,
                2,
                ModelTarget,
                2,
                PureModelAttemptOutcomeKind.ExactReturn,
                true,
                null),
            attempt => AssertAttempt(
                attempt,
                Root,
                9,
                ModelTarget,
                2,
                PureModelAttemptOutcomeKind.ExactReturn,
                true,
                null));
        Assert.Equal(2, run.Outcome.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(1, run.Outcome.OperationalState.ActiveFrameDepthHighWater);
        Assert.DoesNotContain(run.Events, item => item.Kind == DebugEventKind.FramePushed);
        fixture.AssertNoExternalPreparationCapabilityWasRead();
    }

    /// <summary>
    /// Proves a model nested under one interpreted helper enters logical depth three while only two real frames ever
    /// exist, and the split facts remain valid after both interpreted frames unwind.
    /// </summary>
    [Fact]
    public void NestedInterpretedCallerSeparatesLogicalAndActiveFrameHighWater()
    {
        var fixture = PrepareNested(ModelBehavior.SumExact);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver);
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            3,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();

        var run = Run(machine, activation.State!, machine.CreatePreparedOperationalState(new BudgetState(8)));

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        AssertInt32(domain, 3, run.Outcome.State.ReturnValue.Value);
        Assert.Equal(3, run.Outcome.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(2, run.Outcome.OperationalState.ActiveFrameDepthHighWater);
        var attempt = Assert.Single(run.Outcome.OperationalState.ModelAttempts);
        AssertAttempt(
            attempt,
            Middle,
            2,
            ModelTarget,
            3,
            PureModelAttemptOutcomeKind.ExactReturn,
            true,
            null);
        Assert.Single(run.Events, item => item.Kind == DebugEventKind.FramePushed);
        Assert.Equal(2, run.Events.Count(item => item.Kind == DebugEventKind.FramePopped));
        fixture.AssertNoExternalPreparationCapabilityWasRead();
    }

    /// <summary>
    /// Proves the provenance domain grounds a model-produced unknown in mixed and all-unknown admitted arguments,
    /// while the non-generic model sees only typed exact/unknown atoms.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownModelTransfersWithEngineOwnedLineage(bool bothArgumentsUnknown)
    {
        var fixture = PrepareFieldSingle(ModelBehavior.Unknown);
        var domain = new ProvenanceConcreteDomain();
        var exactSecond = domain.ConstInt32(9);
        var machine = Machine(
            domain,
            fixture.Resolver,
            UnknownExecutionPolicy.ExplainedInt32,
            new FieldEvidenceMemoryModel<ProvenanceConcreteValue>(exactSecond, bothArgumentsUnknown));
        var memory = new TestMemory();
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.ObjectReference(1, RootType)),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();

        var run = Run(machine, activation.State!, machine.CreatePreparedOperationalState(new BudgetState(6)));

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Same(memory, run.Outcome.State.Memory);
        var result = run.Outcome.State.ReturnValue.Value;
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(result));
        Assert.True(result.TryGetLineageRoot(out _));
        var graph = domain.CaptureLineage(result);
        Assert.IsType<ModeledReturnTransformLineageNode>(
            graph.Nodes.Single(node => node.Id == graph.Root));
        Assert.Equal(bothArgumentsUnknown ? 7 : 4, graph.Nodes.Length);
        var invocation = Assert.Single(fixture.Model.Invocations);
        Assert.Equal(PureCallModelArgumentKind.ExplainedUnknownInt32, invocation.Arguments[0].Kind);
        Assert.Equal(
            bothArgumentsUnknown
                ? PureCallModelArgumentKind.ExplainedUnknownInt32
                : PureCallModelArgumentKind.ExactInt32,
            invocation.Arguments[1].Kind);
        Assert.Equal(PureCallModelUnknownPolicy.ExplainedInt32, invocation.UnknownPolicy);
        var attempt = Assert.Single(run.Outcome.OperationalState.ModelAttempts);
        AssertAttempt(
            attempt,
            Root,
            12,
            ModelTarget,
            2,
            PureModelAttemptOutcomeKind.UnknownReturn,
            true,
            null);
        Assert.Equal(1, run.Outcome.OperationalState.ActiveFrameDepthHighWater);
        fixture.AssertNoExternalPreparationCapabilityWasRead();
    }

    /// <summary>
    /// Proves instruction-budget exhaustion before the call boundary neither enters nor records a model capability.
    /// </summary>
    [Fact]
    public void PrebudgetFailureCreatesNoAttemptAndDoesNotInvokeModel()
    {
        var fixture = PrepareSingle(ModelBehavior.SumExact);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver);
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(2));
        var first = machine.StepOne(activation.State!, operations);
        var second = machine.StepOne(first.State, first.OperationalState);

        var exhausted = machine.StepOne(second.State, second.OperationalState);

        Assert.Equal(MachineRunStatus.BudgetExhausted, exhausted.Status);
        Assert.Same(second.State, exhausted.State);
        Assert.Same(second.OperationalState, exhausted.OperationalState);
        Assert.Empty(exhausted.Events);
        Assert.Empty(exhausted.OperationalState.ModelAttempts);
        Assert.Equal(0, exhausted.OperationalState.ModelInvocationCount);
        Assert.Equal(0, fixture.Model.InvocationCount);
        Assert.Equal(1, exhausted.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(1, exhausted.OperationalState.ActiveFrameDepthHighWater);
        fixture.AssertNoExternalPreparationCapabilityWasRead();
    }

    /// <summary>
    /// Proves every typed or malformed model failure records exactly one payload-omitting attempt while semantic state,
    /// memory, budget, events, and active-frame depth remain unchanged.
    /// </summary>
    [Theory]
    [InlineData((int)ModelBehavior.Blocked, MachineRunStatus.Blocked, PureModelAttemptOutcomeKind.Blocked, "W4.Model.Limitation")]
    [InlineData((int)ModelBehavior.Invalid, MachineRunStatus.InvalidProgram, PureModelAttemptOutcomeKind.Invalid, "W4.Model.InvocationInvalid")]
    [InlineData((int)ModelBehavior.Throw, MachineRunStatus.Blocked, PureModelAttemptOutcomeKind.CapabilityFailure, "W4.Model.Capability")]
    [InlineData((int)ModelBehavior.Null, MachineRunStatus.InvalidProgram, PureModelAttemptOutcomeKind.MalformedOutcome, "W4.Model.OutcomeInvalid")]
    [InlineData((int)ModelBehavior.Unknown, MachineRunStatus.Blocked, PureModelAttemptOutcomeKind.UnknownReturn, "W4.Model.Limitation")]
    public void NontransferringModelAttemptChangesOnlyOperationalAttemptEvidence(
        int behavior,
        MachineRunStatus expectedStatus,
        PureModelAttemptOutcomeKind expectedAttemptKind,
        string expectedCode)
    {
        var fixture = PrepareSingle((ModelBehavior)behavior);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver, UnknownExecutionPolicy.ExplainedInt32);
        var memory = new TestMemory();
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(5));
        var first = machine.StepOne(activation.State!, operations);
        var second = machine.StepOne(first.State, first.OperationalState);

        var failed = machine.StepOne(second.State, second.OperationalState);

        Assert.Equal(expectedStatus, failed.Status);
        Assert.Equal(expectedCode, failed.Failure?.Code);
        Assert.Same(second.State, failed.State);
        Assert.Same(memory, failed.State.Memory);
        Assert.Equal(second.OperationalState.Budget, failed.OperationalState.Budget);
        Assert.Empty(failed.Events);
        Assert.Equal(1, failed.OperationalState.ModelInvocationCount);
        Assert.Equal(0, failed.OperationalState.CompletedModeledCallCount);
        Assert.Equal(2, failed.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(1, failed.OperationalState.ActiveFrameDepthHighWater);
        var attempt = Assert.Single(failed.OperationalState.ModelAttempts);
        AssertAttempt(
            attempt,
            Root,
            2,
            ModelTarget,
            2,
            expectedAttemptKind,
            false,
            expectedCode);
        Assert.Equal(1, fixture.Model.InvocationCount);
        fixture.AssertNoExternalPreparationCapabilityWasRead();
    }

    /// <summary>
    /// Proves a failed modeled boundary is latched operationally: stepping the unchanged boundary again fails closed
    /// before capability entry and cannot append a second attempt after the nontransfer.
    /// </summary>
    [Fact]
    public void NontransferringAttemptCannotBeFollowedByRetryAtSameBoundary()
    {
        var fixture = PrepareSingle(ModelBehavior.Blocked);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver);
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(5));
        var first = machine.StepOne(activation.State!, operations);
        var second = machine.StepOne(first.State, first.OperationalState);
        var initialFailure = machine.StepOne(second.State, second.OperationalState);
        Assert.Equal(MachineRunStatus.Blocked, initialFailure.Status);
        Assert.Equal(1, fixture.Model.InvocationCount);

        var retry = machine.StepOne(initialFailure.State, initialFailure.OperationalState);

        Assert.Equal(MachineRunStatus.InvalidProgram, retry.Status);
        Assert.Equal("EXEC_MODEL_ATTEMPT_INVARIANT", retry.Failure?.Code);
        Assert.Same(initialFailure.State, retry.State);
        Assert.Same(initialFailure.OperationalState, retry.OperationalState);
        Assert.Single(retry.OperationalState.ModelAttempts);
        Assert.Equal(1, retry.OperationalState.ModelInvocationCount);
        Assert.Equal(1, fixture.Model.InvocationCount);
        Assert.Empty(retry.Events);
    }

    /// <summary>
    /// Proves an explained unknown cannot transfer when the semantic domain omits the optional modeled-call lineage
    /// capability, even though the model itself returns a typed unknown normally.
    /// </summary>
    [Fact]
    public void MissingModeledLineageCapabilityRecordsAtomicUnknownFailure()
    {
        var fixture = PrepareFieldSingle(ModelBehavior.Unknown);
        var domain = new PrecisionOnlyDomain();
        var secondArgument = domain.ConstInt32(7);
        var machine = Machine(
            domain,
            fixture.Resolver,
            UnknownExecutionPolicy.ExplainedInt32,
            new FieldEvidenceMemoryModel<ProvenanceConcreteValue>(secondArgument, secondUnknown: false));
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.ObjectReference(1, RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(7));
        var beforeCall = AdvanceReady(machine, activation.State!, operations, stepCount: 4);

        var failed = machine.StepOne(beforeCall.State, beforeCall.OperationalState);

        Assert.Equal(MachineRunStatus.Blocked, failed.Status);
        Assert.Equal("EXEC_MODEL_LINEAGE_UNAVAILABLE", failed.Failure?.Code);
        Assert.Same(beforeCall.State, failed.State);
        Assert.Equal(beforeCall.OperationalState.Budget, failed.OperationalState.Budget);
        var attempt = Assert.Single(failed.OperationalState.ModelAttempts);
        Assert.Equal(PureModelAttemptOutcomeKind.UnknownReturn, attempt.OutcomeKind);
        Assert.False(attempt.TransferCompleted);
        Assert.Equal("EXEC_MODEL_LINEAGE_UNAVAILABLE", attempt.StableCode);
    }

    /// <summary>
    /// Proves exceptions and malformed values from the optional lineage capability normalize after model entry without
    /// mutating semantic state or committing the call's provisional budget decrement.
    /// </summary>
    [Theory]
    [InlineData((int)LineageBehavior.Throw, MachineRunStatus.Blocked, "EXEC_DOMAIN_FAILURE")]
    [InlineData((int)LineageBehavior.Exact, MachineRunStatus.InvalidProgram, "EXEC_MODEL_LINEAGE_INVALID")]
    [InlineData((int)LineageBehavior.Foreign, MachineRunStatus.InvalidProgram, "EXEC_MODEL_LINEAGE_INVALID")]
    public void InvalidModeledLineageResultIsAtomic(
        int behavior,
        MachineRunStatus expectedStatus,
        string expectedCode)
    {
        var fixture = PrepareFieldSingle(ModelBehavior.Unknown);
        var domain = new ConfigurableLineageDomain((LineageBehavior)behavior);
        var secondArgument = domain.ConstInt32(7);
        var machine = Machine(
            domain,
            fixture.Resolver,
            UnknownExecutionPolicy.ExplainedInt32,
            new FieldEvidenceMemoryModel<ProvenanceConcreteValue>(secondArgument, secondUnknown: false));
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.ObjectReference(1, RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(7));
        var beforeCall = AdvanceReady(machine, activation.State!, operations, stepCount: 4);

        var failed = machine.StepOne(beforeCall.State, beforeCall.OperationalState);

        Assert.Equal(expectedStatus, failed.Status);
        Assert.Equal(expectedCode, failed.Failure?.Code);
        Assert.Same(beforeCall.State, failed.State);
        Assert.Equal(beforeCall.OperationalState.Budget, failed.OperationalState.Budget);
        Assert.Empty(failed.Events);
        var attempt = Assert.Single(failed.OperationalState.ModelAttempts);
        Assert.Equal(PureModelAttemptOutcomeKind.UnknownReturn, attempt.OutcomeKind);
        Assert.False(attempt.TransferCompleted);
        Assert.Equal(expectedCode, attempt.StableCode);
    }

    /// <summary>
    /// Proves post-invocation exact-result domain failures retain an exact-return attempt without transferring or
    /// consuming the caller instruction.
    /// </summary>
    [Theory]
    [InlineData((int)ExactResultBehavior.Throw, MachineRunStatus.Blocked, "EXEC_DOMAIN_FAILURE")]
    [InlineData((int)ExactResultBehavior.WrongValue, MachineRunStatus.InvalidProgram, "EXEC_MODEL_RESULT_INVALID")]
    public void ExactResultMaterializationFailureIsAtomic(
        int behavior,
        MachineRunStatus expectedStatus,
        string expectedCode)
    {
        var fixture = PrepareSingle(ModelBehavior.SumExact);
        var domain = new ConfigurableExactDomain();
        var arguments = ImmutableArray.Create(domain.DefaultValue(RootType));
        var machine = Machine(domain, fixture.Resolver);
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            arguments,
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(5));
        var first = machine.StepOne(activation.State!, operations);
        var second = machine.StepOne(first.State, first.OperationalState);
        domain.ResultBehavior = (ExactResultBehavior)behavior;

        var failed = machine.StepOne(second.State, second.OperationalState);

        Assert.Equal(expectedStatus, failed.Status);
        Assert.Equal(expectedCode, failed.Failure?.Code);
        Assert.Same(second.State, failed.State);
        Assert.Equal(second.OperationalState.Budget, failed.OperationalState.Budget);
        Assert.Empty(failed.Events);
        var attempt = Assert.Single(failed.OperationalState.ModelAttempts);
        Assert.Equal(PureModelAttemptOutcomeKind.ExactReturn, attempt.OutcomeKind);
        Assert.False(attempt.TransferCompleted);
        Assert.Equal(expectedCode, attempt.StableCode);
    }

    /// <summary>Proves default and contradictory modeled-attempt envelopes fail before any instruction or model entry.</summary>
    [Theory]
    [InlineData((int)AttemptTamper.DefaultVector)]
    [InlineData((int)AttemptTamper.InvocationCount)]
    [InlineData((int)AttemptTamper.CompletedCount)]
    [InlineData((int)AttemptTamper.ForeignIdentity)]
    [InlineData((int)AttemptTamper.WrongCallSite)]
    [InlineData((int)AttemptTamper.WrongEnteredDepth)]
    [InlineData((int)AttemptTamper.AttemptHighWaterMismatch)]
    [InlineData((int)AttemptTamper.FutureCompleted)]
    [InlineData((int)AttemptTamper.FutureFailure)]
    public void TamperedAttemptEnvelopeIsRejectedBeforeTransfer(int tamper)
    {
        var fixture = PrepareSingle(ModelBehavior.SumExact);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, fixture.Resolver);
        var activation = machine.ActivatePreparedGraph(
            fixture.Graph,
            2,
            ImmutableArray.Create(domain.DefaultValue(RootType)),
            new TestMemory());
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        fixture.PoisonExternalPreparationCapabilities();
        var operations = machine.CreatePreparedOperationalState(new BudgetState(5));
        var first = machine.StepOne(activation.State!, operations);
        var second = machine.StepOne(first.State, first.OperationalState);
        operations = Tamper(second.OperationalState, (AttemptTamper)tamper);

        var failed = machine.StepOne(second.State, operations);

        Assert.Equal(MachineRunStatus.InvalidProgram, failed.Status);
        Assert.Equal("EXEC_MODEL_ATTEMPT_INVARIANT", failed.Failure?.Code);
        Assert.Same(second.State, failed.State);
        Assert.Same(operations, failed.OperationalState);
        Assert.Empty(failed.Events);
        Assert.Equal(0, fixture.Model.InvocationCount);
    }

    /// <summary>Proves canonical attempt construction rejects bare prefixes and malformed separators.</summary>
    [Theory]
    [InlineData("W4.Model.")]
    [InlineData("W4.Model.Bad..Code")]
    [InlineData("EXEC_")]
    [InlineData("EXEC__BAD")]
    [InlineData("EXEC_bad")]
    [InlineData("EXEC_BAD_")]
    public void AttemptRejectsMalformedStableCode(string stableCode)
    {
        Assert.Throws<ArgumentException>(() => new PureModelAttempt(
            new DirectCallSiteIdentity(Root, 2, ModelTarget),
            ModelIdentity,
            2,
            PureModelAttemptOutcomeKind.ExactReturn,
            transferCompleted: false,
            stableCode));
    }

    private static PreparedFixture PrepareSingle(ModelBehavior behavior, bool repeated = false)
    {
        var resolver = new GraphResolver();
        resolver.Definitions.Add(
            Root,
            Definition(
                Root,
                RootSignature(),
                repeated ? RepeatedConstantCallBody(ModelTarget) : ConstantCallBody(ModelTarget),
                repeated ? 3 : 2));
        resolver.Calls.Add((Root, ModelTarget.MetadataToken), Target(ModelTarget, ModelType));
        resolver.ForbiddenDefinitions.Add(ModelTarget);
        return Prepare(resolver, behavior);
    }

    private static PreparedFixture PrepareFieldSingle(ModelBehavior behavior)
    {
        var resolver = new GraphResolver();
        resolver.Definitions.Add(
            Root,
            Definition(
                Root,
                RootSignature(),
                FieldCallBody(ModelTarget),
                maxStack: 2));
        resolver.Fields.Add((Root, FirstField.Handle.MetadataToken), FirstField);
        resolver.Fields.Add((Root, SecondField.Handle.MetadataToken), SecondField);
        resolver.Calls.Add((Root, ModelTarget.MetadataToken), Target(ModelTarget, ModelType));
        resolver.ForbiddenDefinitions.Add(ModelTarget);
        return Prepare(resolver, behavior);
    }

    private static PreparedFixture PrepareNested(ModelBehavior behavior)
    {
        var resolver = new GraphResolver();
        resolver.Definitions.Add(Root, Definition(Root, RootSignature(), ConstantCallBody(Middle), 2));
        resolver.Definitions.Add(Middle, Definition(Middle, Signature(MiddleType), ForwardingBody(ModelTarget), 2));
        resolver.Calls.Add((Root, Middle.MetadataToken), Target(Middle, MiddleType));
        resolver.Calls.Add((Middle, ModelTarget.MetadataToken), Target(ModelTarget, ModelType));
        resolver.ForbiddenDefinitions.Add(ModelTarget);
        return Prepare(resolver, behavior);
    }

    private static PreparedFixture Prepare(GraphResolver resolver, ModelBehavior behavior)
    {
        var target = Target(ModelTarget, ModelType);
        var model = new RecordingModel(
            new PureCallModelDescriptor(
                ModelIdentity,
                target,
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None),
            behavior);
        var registry = new RecordingRegistry(model);
        var preparation = new MethodGraphPlanner(resolver)
            .RequirePureModel(Root, ModelTarget, registry);
        Assert.True(preparation.IsSuccess, preparation.Failure?.Code);
        Assert.Equal(0, resolver.DefinitionCount(ModelTarget));
        return new PreparedFixture(preparation.Plan!, resolver, registry, model);
    }

    private static IlMachine<TValue, TestMemory> Machine<TValue>(
        IValueDomain<TValue> domain,
        IResolutionServices resolver,
        UnknownExecutionPolicy policy = UnknownExecutionPolicy.ExactOnly,
        IMemoryModel<TValue, TestMemory>? memoryModel = null) =>
        new(
            domain,
            resolver,
            memoryModel ?? new ThrowingMemoryModel<TValue>(),
            new InstructionBudgetPolicy(),
            policy);

    private static StepOutcome<TValue, TestMemory> AdvanceReady<TValue>(
        IlMachine<TValue, TestMemory> machine,
        MachineState<TValue, TestMemory> state,
        MachineOperationalState operations,
        int stepCount)
    {
        StepOutcome<TValue, TestMemory>? outcome = null;
        for (var index = 0; index < stepCount; index++)
        {
            outcome = machine.StepOne(state, operations);
            Assert.Equal(MachineRunStatus.Ready, outcome.Status);
            state = outcome.State;
            operations = outcome.OperationalState;
        }

        return outcome!;
    }

    private static RunResult<TValue> Run<TValue>(
        IlMachine<TValue, TestMemory> machine,
        MachineState<TValue, TestMemory> state,
        MachineOperationalState operations)
    {
        var outcomes = ImmutableArray.CreateBuilder<StepOutcome<TValue, TestMemory>>();
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        StepOutcome<TValue, TestMemory> outcome;
        do
        {
            outcome = machine.StepOne(state, operations);
            outcomes.Add(outcome);
            events.AddRange(outcome.Events);
            state = outcome.State;
            operations = outcome.OperationalState;
        }
        while (outcome.Status == MachineRunStatus.Ready && outcomes.Count < 50);

        Assert.True(outcomes.Count < 50, "Modeled prepared execution did not terminate within the test ceiling.");
        return new RunResult<TValue>(outcome, outcomes.ToImmutable(), events.ToImmutable());
    }

    private static MachineOperationalState Tamper(
        MachineOperationalState operations,
        AttemptTamper tamper)
    {
        var validFailure = new PureModelAttempt(
            new DirectCallSiteIdentity(Root, 2, ModelTarget),
            ModelIdentity,
            2,
            PureModelAttemptOutcomeKind.Blocked,
            false,
            "W4.Model.Limitation");
        return tamper switch
        {
            AttemptTamper.DefaultVector => operations with { ModelAttempts = default },
            AttemptTamper.InvocationCount => operations with { ModelInvocationCount = 1 },
            AttemptTamper.CompletedCount => operations with { CompletedModeledCallCount = 1 },
            AttemptTamper.ForeignIdentity => operations with
            {
                ModelAttempts = ImmutableArray.Create(new PureModelAttempt(
                    new DirectCallSiteIdentity(Root, 2, ModelTarget),
                    new PureCallModelIdentity("w4.foreign", new PureCallModelVersion(1, 0, 0)),
                    2,
                    PureModelAttemptOutcomeKind.Blocked,
                    false,
                    "W4.Model.Limitation")),
                ModelInvocationCount = 1,
                ObservedLogicalDepthHighWater = 2,
            },
            AttemptTamper.WrongCallSite => operations with
            {
                ModelAttempts = ImmutableArray.Create(new PureModelAttempt(
                    new DirectCallSiteIdentity(Root, 9, ModelTarget),
                    ModelIdentity,
                    2,
                    PureModelAttemptOutcomeKind.Blocked,
                    false,
                    "W4.Model.Limitation")),
                ModelInvocationCount = 1,
                ObservedLogicalDepthHighWater = 2,
            },
            AttemptTamper.WrongEnteredDepth => operations with
            {
                ModelAttempts = ImmutableArray.Create(new PureModelAttempt(
                    new DirectCallSiteIdentity(Root, 2, ModelTarget),
                    ModelIdentity,
                    3,
                    PureModelAttemptOutcomeKind.Blocked,
                    false,
                    "W4.Model.Limitation")),
                ModelInvocationCount = 1,
                ObservedLogicalDepthHighWater = 3,
            },
            AttemptTamper.AttemptHighWaterMismatch => operations with
            {
                ModelAttempts = ImmutableArray.Create(validFailure),
                ModelInvocationCount = 1,
            },
            AttemptTamper.FutureCompleted => operations with
            {
                ModelAttempts = ImmutableArray.Create(new PureModelAttempt(
                    new DirectCallSiteIdentity(Root, 2, ModelTarget),
                    ModelIdentity,
                    2,
                    PureModelAttemptOutcomeKind.ExactReturn,
                    true,
                    null)),
                ModelInvocationCount = 1,
                CompletedModeledCallCount = 1,
                ObservedLogicalDepthHighWater = 2,
            },
            AttemptTamper.FutureFailure => operations with
            {
                ModelAttempts = ImmutableArray.Create(validFailure),
                ModelInvocationCount = 1,
                ObservedLogicalDepthHighWater = 2,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(tamper)),
        };
    }

    private static ProvenanceConcreteValue Unknown(
        ProvenanceConcreteDomain domain,
        string source,
        int index) =>
        domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            index,
            EvaluationEvidenceStatus.Partial,
            ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes(source)),
            "W4.Model.Partial",
            TypeSig.Int32));

    private static MethodHandle Method(int row) => new(Module, 0x06000000 | row);

    private static ResolvedField Field(int row) => new(
        new FieldHandle(Module, 0x04000000 | row),
        RootType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    private static ResolvedMethodCallTarget Target(MethodHandle method, TypeSig declaringType) =>
        new(method, Signature(declaringType));

    private static MethodCallSignatureShape RootSignature() => new(
        RootType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: true,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray<TypeSig>.Empty,
        TypeSig.Int32);

    private static MethodCallSignatureShape Signature(TypeSig declaringType) => new(
        declaringType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: false,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray.Create(TypeSig.Int32, TypeSig.Int32),
        TypeSig.Int32);

    private static ResolvedMethodDefinition Definition(
        MethodHandle method,
        MethodCallSignatureShape signature,
        byte[] code,
        int maxStack) =>
        new(
            method,
            IlBody.Create(maxStack, code),
            new MethodSignatureShape(signature, ImmutableArray<TypeSig>.Empty));

    private static byte[] ForwardingBody(MethodHandle target)
    {
        var code = new List<byte> { 0x02, 0x03 };
        EmitToken(code, 0x28, target.MetadataToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] ConstantCallBody(MethodHandle target)
    {
        var code = new List<byte> { 0x17, 0x18 };
        EmitToken(code, 0x28, target.MetadataToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] RepeatedConstantCallBody(MethodHandle target)
    {
        var code = new List<byte> { 0x17, 0x18 };
        EmitToken(code, 0x28, target.MetadataToken);
        code.Add(0x17);
        code.Add(0x18);
        EmitToken(code, 0x28, target.MetadataToken);
        code.Add(0x58);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] FieldCallBody(MethodHandle target)
    {
        var code = new List<byte> { 0x02 };
        EmitToken(code, 0x7B, FirstField.Handle.MetadataToken);
        code.Add(0x02);
        EmitToken(code, 0x7B, SecondField.Handle.MetadataToken);
        EmitToken(code, 0x28, target.MetadataToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static void EmitToken(List<byte> code, byte opcode, int token)
    {
        code.Add(opcode);
        code.Add((byte)token);
        code.Add((byte)(token >> 8));
        code.Add((byte)(token >> 16));
        code.Add((byte)(token >> 24));
    }

    private static void AssertAttempt(
        PureModelAttempt attempt,
        MethodHandle caller,
        int ilOffset,
        MethodHandle callee,
        int depth,
        PureModelAttemptOutcomeKind outcome,
        bool completed,
        string? stableCode)
    {
        Assert.Equal(new DirectCallSiteIdentity(caller, ilOffset, callee), attempt.CallSite);
        Assert.Equal(ModelIdentity, attempt.ModelIdentity);
        Assert.Equal(depth, attempt.EnteredLogicalDepth);
        Assert.Equal(outcome, attempt.OutcomeKind);
        Assert.Equal(completed, attempt.TransferCompleted);
        Assert.Equal(stableCode, attempt.StableCode);
    }

    private static void AssertEvent(
        DebugEvent item,
        DebugEventKind kind,
        MethodHandle method,
        int offset,
        string operation)
    {
        Assert.Equal(kind, item.Kind);
        Assert.Equal(method, item.Method);
        Assert.Equal(offset, item.IlOffset);
        Assert.Equal(operation, item.Instruction);
    }

    private static void AssertInt32(ConcreteDomain domain, int expected, ConcreteValue actual)
    {
        Assert.True(domain.TryGetConstInt32(actual, out var value));
        Assert.Equal(expected, value);
    }

    private enum ModelBehavior
    {
        SumExact,
        Unknown,
        Blocked,
        Invalid,
        Throw,
        Null,
    }

    private enum LineageBehavior
    {
        Throw,
        Exact,
        Foreign,
    }

    private enum ExactResultBehavior
    {
        Normal,
        Throw,
        WrongValue,
    }

    private enum AttemptTamper
    {
        DefaultVector,
        InvocationCount,
        CompletedCount,
        ForeignIdentity,
        WrongCallSite,
        WrongEnteredDepth,
        AttemptHighWaterMismatch,
        FutureCompleted,
        FutureFailure,
    }

    private sealed record PreparedFixture(
        FrozenMethodGraphPlan Graph,
        GraphResolver Resolver,
        RecordingRegistry Registry,
        RecordingModel Model)
    {
        private int? operationCount;
        private int? selectionCount;
        private int? descriptorReadCount;

        internal void PoisonExternalPreparationCapabilities()
        {
            operationCount = Resolver.OperationCount;
            selectionCount = Registry.SelectionCount;
            descriptorReadCount = Model.DescriptorReadCount;
            Resolver.ThrowOnUse = true;
            Registry.ThrowOnUse = true;
            Model.ThrowOnDescriptorRead = true;
        }

        internal void AssertNoExternalPreparationCapabilityWasRead()
        {
            Assert.Equal(operationCount, Resolver.OperationCount);
            Assert.Equal(selectionCount, Registry.SelectionCount);
            Assert.Equal(descriptorReadCount, Model.DescriptorReadCount);
        }
    }

    private sealed record RunResult<TValue>(
        StepOutcome<TValue, TestMemory> Outcome,
        ImmutableArray<StepOutcome<TValue, TestMemory>> Outcomes,
        ImmutableArray<DebugEvent> Events);

    private sealed class RecordingModel(
        PureCallModelDescriptor descriptor,
        ModelBehavior behavior) : IPureCallModel
    {
        private readonly PureCallModelDescriptor descriptor = descriptor;

        public PureCallModelDescriptor Descriptor
        {
            get
            {
                DescriptorReadCount++;
                if (ThrowOnDescriptorRead)
                {
                    throw new InvalidOperationException("Execution must not reread the frozen model descriptor.");
                }

                return descriptor;
            }
        }

        internal int DescriptorReadCount { get; private set; }

        internal bool ThrowOnDescriptorRead { get; set; }

        internal int InvocationCount => Invocations.Count;

        internal List<PureCallModelInvocation> Invocations { get; } = [];

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            Invocations.Add(invocation);
            return behavior switch
            {
                ModelBehavior.SumExact => PureCallModelOutcome.ExactReturn(
                    invocation.Arguments.Sum(argument => argument.Int32Value!.Value)),
                ModelBehavior.Unknown => PureCallModelOutcome.UnknownReturn(),
                ModelBehavior.Blocked => PureCallModelOutcome.Blocked("W4.Model.Limitation"),
                ModelBehavior.Invalid => PureCallModelOutcome.Invalid("W4.Model.InvocationInvalid"),
                ModelBehavior.Throw => throw new SyntheticCapabilityException(),
                ModelBehavior.Null => null!,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
    }

    private sealed class RecordingRegistry(IPureCallModel model) : IPureCallModelRegistry
    {
        internal int SelectionCount { get; private set; }

        internal bool ThrowOnUse { get; set; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            SelectionCount++;
            if (ThrowOnUse)
            {
                throw new InvalidOperationException("Execution must not consult the model registry.");
            }

            return PureCallModelSelectionResult.Selected(model);
        }
    }

    private sealed class GraphResolver : IResolutionServices
    {
        internal Dictionary<MethodHandle, ResolvedMethodDefinition> Definitions { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedMethodCallTarget> Calls { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedField> Fields { get; } = [];

        internal HashSet<MethodHandle> ForbiddenDefinitions { get; } = [];

        internal Dictionary<MethodHandle, int> DefinitionCounts { get; } = [];

        internal int OperationCount { get; private set; }

        internal bool ThrowOnUse { get; set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            Count();
            DefinitionCounts[method] = DefinitionCounts.GetValueOrDefault(method) + 1;
            if (ForbiddenDefinitions.Contains(method))
            {
                throw new InvalidOperationException("The opaque model target body must never be acquired.");
            }

            return Definitions.TryGetValue(method, out var definition)
                ? ResolutionResult<ResolvedMethodDefinition>.Success(definition)
                : ResolutionResult<ResolvedMethodDefinition>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_METHOD_UNAVAILABLE",
                    "Synthetic method definition was not configured.");
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
        {
            Count();
            return Fields.TryGetValue((contextMethod, metadataToken), out var field)
                ? ResolutionResult<ResolvedField>.Success(field)
                : ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_FIELD_UNAVAILABLE",
                    "Synthetic field target was not configured.");
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Count();
            return Calls.TryGetValue((contextMethod, metadataToken), out var target)
                ? ResolutionResult<ResolvedMethodCallTarget>.Success(target)
                : ResolutionResult<ResolvedMethodCallTarget>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_CALL_UNAVAILABLE",
                    "Synthetic call target was not configured.");
        }

        internal int DefinitionCount(MethodHandle method) => DefinitionCounts.GetValueOrDefault(method);

        private void Count()
        {
            OperationCount++;
            if (ThrowOnUse)
            {
                throw new InvalidOperationException("Execution must not resolve frozen metadata again.");
            }
        }
    }

    private sealed class TestMemory : IPersistentMemoryState<TestMemory>
    {
        public TestMemory Fork() => this;
    }

    private sealed class ThrowingMemoryModel<TValue> : IMemoryModel<TValue, TestMemory>
    {
        public bool CanAllocate => false;

        public (TValue objRef, TestMemory mem) NewObject(TestMemory mem, TypeSig type) =>
            throw new InvalidOperationException("Modeled-call tests admit no memory operation.");

        public (TValue arrRef, TestMemory mem) NewArray(TestMemory mem, TypeSig elemType, TValue length) =>
            throw new InvalidOperationException("Modeled-call tests admit no memory operation.");

        public MemoryLoadResult<TValue> LoadField(TestMemory mem, TValue objRef, ResolvedField field) =>
            throw new InvalidOperationException("Modeled-call tests admit no memory operation.");

        public TestMemory StoreField(TestMemory mem, TValue objRef, ResolvedField field, TValue value) =>
            throw new InvalidOperationException("Modeled-call tests admit no memory operation.");

        public TValue LoadElement(TestMemory mem, TValue arrRef, TValue index) =>
            throw new InvalidOperationException("Modeled-call tests admit no memory operation.");

        public TestMemory StoreElement(TestMemory mem, TValue arrRef, TValue index, TValue value) =>
            throw new InvalidOperationException("Modeled-call tests admit no memory operation.");
    }

    private sealed class FieldEvidenceMemoryModel<TValue>(TValue exactSecond, bool secondUnknown) :
        IMemoryModel<TValue, TestMemory>
    {
        public bool CanAllocate => false;

        public (TValue objRef, TestMemory mem) NewObject(TestMemory mem, TypeSig type) =>
            throw new InvalidOperationException("Field-backed model tests admit no allocation.");

        public (TValue arrRef, TestMemory mem) NewArray(TestMemory mem, TypeSig elemType, TValue length) =>
            throw new InvalidOperationException("Field-backed model tests admit no allocation.");

        public MemoryLoadResult<TValue> LoadField(TestMemory mem, TValue objRef, ResolvedField field) =>
            field.Handle == FirstField.Handle
                ? MemoryLoadResult<TValue>.FromFieldEvidence(Evidence(0, FirstField, partial: true))
                : field.Handle == SecondField.Handle
                    ? secondUnknown
                        ? MemoryLoadResult<TValue>.FromFieldEvidence(Evidence(1, SecondField, partial: false))
                        : MemoryLoadResult<TValue>.Exact(exactSecond)
                    : MemoryLoadResult<TValue>.NonExact(MemoryLoadKind.Invalid, "TEST_FIELD_UNEXPECTED");

        public TestMemory StoreField(TestMemory mem, TValue objRef, ResolvedField field, TValue value) =>
            throw new InvalidOperationException("Field-backed model tests admit no memory mutation.");

        public TValue LoadElement(TestMemory mem, TValue arrRef, TValue index) =>
            throw new InvalidOperationException("Field-backed model tests admit no array access.");

        public TestMemory StoreElement(TestMemory mem, TValue arrRef, TValue index, TValue value) =>
            throw new InvalidOperationException("Field-backed model tests admit no array access.");

        private static FieldLoadEvidence Evidence(int ordinal, ResolvedField field, bool partial) =>
            new(
                ordinal,
                field,
                partial ? EvaluationEvidenceStatus.Partial : EvaluationEvidenceStatus.Unavailable,
                partial ? "W4.Field.Partial" : "W4.Field.Unavailable",
                new string('a', 64),
                new string('b', 64),
                0x0000_0001_2345_0000UL + checked((ulong)ordinal * 0x10UL),
                sizeof(int),
                partial ? new byte[] { 1, 2 } : []);
    }

    private class PrecisionOnlyDomain : IFieldLoadApproximationDomain<ProvenanceConcreteValue>
    {
        protected ProvenanceConcreteDomain Inner { get; } = new();

        internal ProvenanceConcreteValue Unknown(string source, int index) =>
            PreparedGraphModelExecutionTests.Unknown(Inner, source, index);

        internal ProvenanceConcreteValue ObjectReference(long id, TypeSig type) =>
            Inner.ObjectReference(id, type);

        public ProvenanceConcreteValue Bottom(TypeSig type) => Inner.Bottom(type);
        public bool IsBottom(ProvenanceConcreteValue value) => Inner.IsBottom(value);
        public ProvenanceConcreteValue Top(TypeSig type) => Inner.Top(type);
        public ProvenanceConcreteValue DefaultValue(TypeSig type) => Inner.DefaultValue(type);
        public ProvenanceConcreteValue ConstInt32(int value) => Inner.ConstInt32(value);
        public ProvenanceConcreteValue Join(ProvenanceConcreteValue a, ProvenanceConcreteValue b) => Inner.Join(a, b);
        public bool IsLessThanOrEqual(ProvenanceConcreteValue a, ProvenanceConcreteValue b) =>
            Inner.IsLessThanOrEqual(a, b);
        public ProvenanceConcreteValue Meet(ProvenanceConcreteValue a, ProvenanceConcreteValue b) => Inner.Meet(a, b);
        public ProvenanceConcreteValue Widen(ProvenanceConcreteValue prev, ProvenanceConcreteValue next) =>
            Inner.Widen(prev, next);
        public TypeSig GetStaticType(ProvenanceConcreteValue value) => Inner.GetStaticType(value);
        public StackKind GetStackKind(ProvenanceConcreteValue value) => Inner.GetStackKind(value);
        public bool TryGetConstInt32(ProvenanceConcreteValue value, out int c) => Inner.TryGetConstInt32(value, out c);
        public ProvenanceConcreteValue ApplyBinary(
            BinaryOp op,
            ProvenanceConcreteValue a,
            ProvenanceConcreteValue b) =>
            Inner.ApplyBinary(op, a, b);
        public ValuePrecisionKind GetPrecision(ProvenanceConcreteValue value) => Inner.GetPrecision(value);
        public ProvenanceConcreteValue CreateFieldLoadUnknown(
            ProvenanceConcreteValue receiver,
            FieldLoadEvidence evidence) =>
            Inner.CreateFieldLoadUnknown(receiver, evidence);
    }

    private sealed class ConfigurableLineageDomain(LineageBehavior behavior) :
        PrecisionOnlyDomain,
        IPureCallModelLineageDomain<ProvenanceConcreteValue>
    {
        public ProvenanceConcreteValue CreateModeledReturnUnknown(
            DirectCallSiteIdentity callSite,
            PureCallModelIdentity modelIdentity,
            ImmutableArray<ProvenanceConcreteValue> arguments) =>
            behavior switch
            {
                LineageBehavior.Throw => throw new SyntheticCapabilityException(),
                LineageBehavior.Exact => Inner.ConstInt32(0),
                LineageBehavior.Foreign => PreparedGraphModelExecutionTests.Unknown(
                    new ProvenanceConcreteDomain(),
                    "foreign",
                    0),
                _ => throw new ArgumentOutOfRangeException(),
            };
    }

    private sealed class ConfigurableExactDomain : IValueDomain<ConcreteValue>
    {
        private readonly ConcreteDomain inner = new();

        internal ExactResultBehavior ResultBehavior { get; set; }

        public ConcreteValue Bottom(TypeSig type) => inner.Bottom(type);
        public bool IsBottom(ConcreteValue value) => inner.IsBottom(value);
        public ConcreteValue Top(TypeSig type) => inner.Top(type);
        public ConcreteValue DefaultValue(TypeSig type) => inner.DefaultValue(type);
        public ConcreteValue ConstInt32(int value) => ResultBehavior switch
        {
            ExactResultBehavior.Throw => throw new SyntheticCapabilityException(),
            ExactResultBehavior.WrongValue => inner.ConstInt32(unchecked(value + 1)),
            _ => inner.ConstInt32(value),
        };
        public ConcreteValue Join(ConcreteValue a, ConcreteValue b) => inner.Join(a, b);
        public bool IsLessThanOrEqual(ConcreteValue a, ConcreteValue b) => inner.IsLessThanOrEqual(a, b);
        public ConcreteValue Meet(ConcreteValue a, ConcreteValue b) => inner.Meet(a, b);
        public ConcreteValue Widen(ConcreteValue prev, ConcreteValue next) => inner.Widen(prev, next);
        public TypeSig GetStaticType(ConcreteValue value) => inner.GetStaticType(value);
        public StackKind GetStackKind(ConcreteValue value) => inner.GetStackKind(value);
        public bool TryGetConstInt32(ConcreteValue value, out int c) => inner.TryGetConstInt32(value, out c);
        public ConcreteValue ApplyBinary(BinaryOp op, ConcreteValue a, ConcreteValue b) => inner.ApplyBinary(op, a, b);
    }

    private sealed class SyntheticCapabilityException : Exception
    {
    }
}
