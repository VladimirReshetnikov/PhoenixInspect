using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Exercises exact W4.5 prepared-graph activation, interpreted frame transfer, return sites, and call-depth facts.
/// </summary>
public sealed class PreparedGraphExecutionTests
{
    private static readonly ModuleHandle Module = new(
        0xB501020304050607,
        0xB508091011121314);
    private static readonly TypeSig RootType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.PreparedGraphRoot");
    private static readonly TypeSig HelperType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000002,
        "Interpreter.Tests.PreparedGraphHelper");
    private static readonly MethodHandle Root = Method(1);
    private static readonly MethodHandle Helper = Method(2);
    private static readonly MethodHandle SecondHelper = Method(3);
    private static readonly MethodHandle Legacy = Method(50);
    private static readonly ResolvedField FirstField = Field(1);
    private static readonly ResolvedField SecondField = Field(2);

    /// <summary>
    /// Proves the exact two-field root/helper workflow executes ten separately budgeted instructions, returns the
    /// concrete sum, preserves persistent memory, reports depth two, and never consults metadata after preparation.
    /// </summary>
    [Fact]
    public void ExactRootHelperWorkflowRunsTenInstructionsWithoutReresolutionOrMemoryMutation()
    {
        var resolver = Resolver(
            RootDefinition(Root, ExactRootBody(Helper)),
            HelperDefinition(Helper, AddBody()));
        resolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        resolver.Fields[(Root, SecondField.Handle.MetadataToken)] = SecondField;
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        var graph = Prepare(resolver, Root);
        var resolutionCount = resolver.TotalCallCount;
        resolver.ThrowOnUse = true;
        var domain = new ConcreteDomain();
        var memoryModel = new ConcreteMemoryModel(domain);
        var (receiver, allocatedMemory) = memoryModel.NewObject(ConcreteMemory.Empty, RootType);
        var memory = memoryModel.StoreField(
            memoryModel.StoreField(allocatedMemory, receiver, FirstField, domain.ConstInt32(17)),
            receiver,
            SecondField,
            domain.ConstInt32(25));
        var machine = Machine(domain, resolver, memoryModel);

        var activation = machine.ActivatePreparedGraph(
            graph,
            maximumLogicalCallDepth: 2,
            ImmutableArray.Create(receiver),
            memory);

        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        Assert.Null(Assert.Single(activation.State!.CallStack).ReturnSite);
        var run = Run(machine, activation.State, instructionBudget: 10);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Empty(run.Outcome.State.CallStack);
        AssertInt32(42, run.Outcome.State.ReturnValue.Value);
        Assert.Same(memory, run.Outcome.State.Memory);
        Assert.Equal(0, run.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Equal(2, run.Outcome.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(2, run.Outcome.OperationalState.ActiveFrameDepthHighWater);
        Assert.Equal(10, run.Events.Count(static item => item.Kind == DebugEventKind.InstructionExecuted));
        Assert.Equal(2, run.Events.Count(static item => item.Kind == DebugEventKind.FramePopped));
        Assert.Single(run.Events, static item => item.Kind == DebugEventKind.FramePushed);
        Assert.All(run.Outcomes, outcome => Assert.Same(memory, outcome.State.Memory));
        Assert.Equal(resolutionCount, resolver.TotalCallCount);
    }

    /// <summary>
    /// Proves a direct call advances and suspends its caller, freezes the exact return boundary, seeds the callee
    /// arguments, and later returns as a distinct step with truthful event ordering.
    /// </summary>
    [Fact]
    public void CallAndNestedReturnExposeExactSnapshotsAndOrderedEvents()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var operations = machine.CreatePreparedOperationalState(new BudgetState(20));
        var state = activation.State!;

        var firstConstant = machine.StepOne(state, operations);
        var secondConstant = machine.StepOne(firstConstant.State, firstConstant.OperationalState);
        var call = machine.StepOne(secondConstant.State, secondConstant.OperationalState);

        Assert.Equal(MachineRunStatus.Ready, call.Status);
        Assert.Equal(2, call.State.CallStack.Length);
        var caller = call.State.CallStack[0];
        var callee = call.State.CallStack[1];
        Assert.Equal(Root, caller.Method);
        Assert.Equal(7, caller.IlOffset);
        Assert.Empty(caller.EvalStack);
        Assert.Null(caller.ReturnSite);
        Assert.Equal(Helper, callee.Method);
        Assert.Equal(0, callee.IlOffset);
        Assert.Empty(callee.Locals);
        Assert.Empty(callee.EvalStack);
        AssertInt32(1, callee.Arguments[0]);
        AssertInt32(2, callee.Arguments[1]);
        Assert.Equal(
            new FrameReturnSite(new DirectCallSiteIdentity(Root, 2, Helper), 7),
            callee.ReturnSite);
        Assert.Equal(2, call.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(2, call.OperationalState.ActiveFrameDepthHighWater);
        Assert.Collection(
            call.Events,
            item => AssertEvent(item, DebugEventKind.InstructionExecuted, Root, 2, "Call"),
            item => AssertEvent(item, DebugEventKind.FramePushed, Helper, 0, "Entry"));

        var helperArg0 = machine.StepOne(call.State, call.OperationalState);
        var helperArg1 = machine.StepOne(helperArg0.State, helperArg0.OperationalState);
        var helperAdd = machine.StepOne(helperArg1.State, helperArg1.OperationalState);
        var helperReturn = machine.StepOne(helperAdd.State, helperAdd.OperationalState);

        Assert.Equal(MachineRunStatus.Ready, helperReturn.Status);
        var resumed = Assert.Single(helperReturn.State.CallStack);
        Assert.Equal(Root, resumed.Method);
        Assert.Equal(7, resumed.IlOffset);
        AssertInt32(3, Assert.Single(resumed.EvalStack));
        Assert.Null(resumed.ReturnSite);
        Assert.Collection(
            helperReturn.Events,
            item => AssertEvent(item, DebugEventKind.InstructionExecuted, Helper, 3, "Return"),
            item => AssertEvent(item, DebugEventKind.FramePopped, Helper, 3, "Return"));
    }

    /// <summary>
    /// Proves a three-method interpreted chain reaches, retains, and reports logical and active-frame depth three.
    /// </summary>
    [Fact]
    public void ThreeMethodChainRecordsDepthThreeHighWaterAfterUnwinding()
    {
        var resolver = Resolver(
            RootDefinition(Root, ConstantCallBody(Helper)),
            HelperDefinition(Helper, ForwardingCallBody(SecondHelper)),
            HelperDefinition(SecondHelper, AddBody()));
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        resolver.Calls[(Helper, SecondHelper.MetadataToken)] = Target(SecondHelper);
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 3);

        var run = Run(machine, activation.State!, instructionBudget: 12);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        AssertInt32(3, run.Outcome.State.ReturnValue.Value);
        Assert.Equal(3, run.Outcome.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(3, run.Outcome.OperationalState.ActiveFrameDepthHighWater);
        Assert.Equal(2, run.Events.Count(static item => item.Kind == DebugEventKind.FramePushed));
        Assert.Equal(3, run.Events.Count(static item => item.Kind == DebugEventKind.FramePopped));
        Assert.Contains(
            run.Outcomes,
            outcome => outcome.State.CallStack.Length == 3 &&
                outcome.OperationalState.ObservedLogicalDepthHighWater == 3);
    }

    /// <summary>Proves nonpositive configured logical depth is invalid before a root frame can be created.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveLogicalDepthRejectsActivation(int maximumLogicalCallDepth)
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var (receiver, memory) = CreateReceiver(domain);

        var activation = machine.ActivatePreparedGraph(
            graph,
            maximumLogicalCallDepth,
            ImmutableArray.Create(receiver),
            memory);

        AssertActivationFailure(activation, MachineRunStatus.InvalidProgram, "EXEC_CALL_DEPTH_LIMIT_INVALID");
    }

    /// <summary>
    /// Proves a configured bound smaller than the graph's prepared longest path exhausts depth before activation.
    /// </summary>
    [Fact]
    public void InsufficientLogicalDepthExhaustsBeforeActivation()
    {
        var resolver = Resolver(
            RootDefinition(Root, ConstantCallBody(Helper)),
            HelperDefinition(Helper, ForwardingCallBody(SecondHelper)),
            HelperDefinition(SecondHelper, AddBody()));
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        resolver.Calls[(Helper, SecondHelper.MetadataToken)] = Target(SecondHelper);
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var (receiver, memory) = CreateReceiver(domain);

        var activation = machine.ActivatePreparedGraph(
            graph,
            maximumLogicalCallDepth: 2,
            ImmutableArray.Create(receiver),
            memory);

        AssertActivationFailure(activation, MachineRunStatus.BudgetExhausted, "EXEC_CALL_DEPTH_EXHAUSTED");
        Assert.Equal(3, graph.RequiredLogicalDepth);
    }

    /// <summary>Proves zero instruction budget at a call boundary does not advance, push, account, or emit.</summary>
    [Fact]
    public void ZeroBudgetBeforeCallLeavesCallerAndDepthFactsUnchanged()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var first = machine.StepOne(
            activation.State!,
            machine.CreatePreparedOperationalState(new BudgetState(2)));
        var atCall = machine.StepOne(first.State, first.OperationalState);
        Assert.Equal(2, Assert.Single(atCall.State.CallStack).IlOffset);
        var exhausted = atCall.OperationalState;

        var outcome = machine.StepOne(atCall.State, exhausted);

        AssertBudgetExhaustedWithoutTransfer(atCall.State, exhausted, outcome);
        Assert.Equal(1, outcome.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(1, outcome.OperationalState.ActiveFrameDepthHighWater);
    }

    /// <summary>Proves zero instruction budget at a nested <c>ret</c> does not pop or publish a helper result.</summary>
    [Fact]
    public void ZeroBudgetBeforeNestedReturnLeavesBothFramesUnchanged()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var runToReturn = RunReadySteps(
            machine,
            activation.State!,
            machine.CreatePreparedOperationalState(new BudgetState(7)),
            stepCount: 6);
        var helper = runToReturn.State.CallStack[^1];
        Assert.Equal(Helper, helper.Method);
        Assert.Equal(3, helper.IlOffset);
        var exhausted = runToReturn.OperationalState with { Budget = new BudgetState(0) };

        var outcome = machine.StepOne(runToReturn.State, exhausted);

        AssertBudgetExhaustedWithoutTransfer(runToReturn.State, exhausted, outcome);
        Assert.Equal(2, outcome.State.CallStack.Length);
        Assert.False(outcome.State.ReturnValue.HasValue);
    }

    /// <summary>
    /// Proves repeated calls to one shared helper retain distinct return sites and preserve the caller's pre-argument
    /// stack prefix across the second call.
    /// </summary>
    [Fact]
    public void RepeatedSharedHelperCallsRetainDistinctReturnSitesAndCallerPrefix()
    {
        var resolver = Resolver(
            RootDefinition(Root, RepeatedCallBody(Helper)),
            HelperDefinition(Helper, AddBody()));
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        var graph = Prepare(resolver, Root);
        Assert.Equal(2, graph.CallSites.Length);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var operations = machine.CreatePreparedOperationalState(new BudgetState(20));
        var state = activation.State!;

        state = machine.StepOne(state, operations).State;
        var secondConstant = machine.StepOne(state, operations with { Budget = new BudgetState(19) });
        var firstCall = machine.StepOne(secondConstant.State, secondConstant.OperationalState);
        var firstSite = firstCall.State.CallStack[^1].ReturnSite!;
        var firstReturn = RunReadySteps(
            machine,
            firstCall.State,
            firstCall.OperationalState,
            stepCount: 4);
        var afterThirdConstant = machine.StepOne(firstReturn.State, firstReturn.OperationalState);
        var beforeSecondCall = machine.StepOne(afterThirdConstant.State, afterThirdConstant.OperationalState);
        var secondCall = machine.StepOne(beforeSecondCall.State, beforeSecondCall.OperationalState);
        var secondSite = secondCall.State.CallStack[^1].ReturnSite!;

        Assert.Equal((2, 7), (firstSite.CallSite.CallIlOffset, firstSite.CallerResumeIlOffset));
        Assert.Equal((9, 14), (secondSite.CallSite.CallIlOffset, secondSite.CallerResumeIlOffset));
        Assert.NotEqual(firstSite, secondSite);
        var suspendedCaller = secondCall.State.CallStack[0];
        Assert.Equal(14, suspendedCaller.IlOffset);
        AssertInt32(3, Assert.Single(suspendedCaller.EvalStack));

        var final = Run(machine, secondCall.State, secondCall.OperationalState);

        Assert.Equal(MachineRunStatus.Completed, final.Outcome.Status);
        AssertInt32(10, final.Outcome.State.ReturnValue.Value);
        Assert.Equal(2, final.Outcome.OperationalState.ObservedLogicalDepthHighWater);
    }

    /// <summary>
    /// Proves forged return-site identity and caller-prefix state fail atomically after budget availability is known.
    /// </summary>
    [Fact]
    public void ForgedReturnSiteOrCallerPrefixFailsAtomically()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var atCall = RunReadySteps(
            machine,
            activation.State!,
            machine.CreatePreparedOperationalState(new BudgetState(20)),
            stepCount: 3);
        var operations = atCall.OperationalState;
        var callee = atCall.State.CallStack[1];
        var forgedSite = new FrameReturnSite(
            new DirectCallSiteIdentity(Root, 3, Helper),
            callerResumeIlOffset: 7);
        var wrongSiteState = atCall.State with
        {
            CallStack = atCall.State.CallStack.SetItem(1, callee with { ReturnSite = forgedSite }),
        };
        AssertNoTransfer(
            wrongSiteState,
            operations,
            machine.StepOne(wrongSiteState, operations),
            "EXEC_CALL_RETURN_SITE_INVALID");

        var caller = atCall.State.CallStack[0];
        var wrongPrefixState = atCall.State with
        {
            CallStack = atCall.State.CallStack.SetItem(
                0,
                caller with { EvalStack = ImmutableArray.Create(domain.ConstInt32(99)) }),
        };
        AssertNoTransfer(
            wrongPrefixState,
            operations,
            machine.StepOne(wrongPrefixState, operations),
            "EXEC_CALL_RETURN_SITE_INVALID");
    }

    /// <summary>
    /// Proves forged depth/high-water facts fail atomically, while exhausted instruction budget has precedence and
    /// exposes no invariant diagnostic or event.
    /// </summary>
    [Fact]
    public void ForgedDepthFactsFailAtomicallyAfterInstructionBudgetPrecheck()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var operations = machine.CreatePreparedOperationalState(new BudgetState(20));
        var forgedFutureDepth = operations with
        {
            ObservedLogicalDepthHighWater = 2,
            ActiveFrameDepthHighWater = 2,
        };
        AssertNoTransfer(
            activation.State!,
            forgedFutureDepth,
            machine.StepOne(activation.State!, forgedFutureDepth),
            "EXEC_CALL_DEPTH_INVARIANT");

        var atCall = RunReadySteps(
            machine,
            activation.State!,
            operations,
            stepCount: 3);
        var forged = atCall.OperationalState with
        {
            ObservedLogicalDepthHighWater = 1,
            ActiveFrameDepthHighWater = 2,
        };

        AssertNoTransfer(
            atCall.State,
            forged,
            machine.StepOne(atCall.State, forged),
            "EXEC_CALL_DEPTH_INVARIANT");

        var overDepthState = atCall.State with
        {
            CallStack = atCall.State.CallStack.Add(atCall.State.CallStack[^1]),
        };
        AssertNoTransfer(
            overDepthState,
            atCall.OperationalState,
            machine.StepOne(overDepthState, atCall.OperationalState),
            "EXEC_CALL_DEPTH_INVARIANT");

        var excessiveHighWater = atCall.OperationalState with
        {
            ObservedLogicalDepthHighWater = 3,
            ActiveFrameDepthHighWater = 3,
        };
        AssertNoTransfer(
            atCall.State,
            excessiveHighWater,
            machine.StepOne(atCall.State, excessiveHighWater),
            "EXEC_CALL_DEPTH_INVARIANT");

        var exhausted = forged with { Budget = new BudgetState(0) };
        var budgetOutcome = machine.StepOne(atCall.State, exhausted);
        AssertBudgetExhaustedWithoutTransfer(atCall.State, exhausted, budgetOutcome);
    }

    /// <summary>
    /// Proves a completed helper call remains a depth-two witness after its frame is popped, so both observed high
    /// waters cannot be forged back to the root-only value before the caller returns.
    /// </summary>
    [Fact]
    public void ReturnedCallPreventsBothDepthHighWatersFromBeingForgedDown()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var afterHelperReturn = RunReadySteps(
            machine,
            activation.State!,
            machine.CreatePreparedOperationalState(new BudgetState(20)),
            stepCount: 7);
        Assert.Single(afterHelperReturn.State.CallStack);
        Assert.Equal(7, afterHelperReturn.State.CallStack[0].IlOffset);
        Assert.Equal(2, afterHelperReturn.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(2, afterHelperReturn.OperationalState.ActiveFrameDepthHighWater);
        var forged = afterHelperReturn.OperationalState with
        {
            ObservedLogicalDepthHighWater = 1,
            ActiveFrameDepthHighWater = 1,
        };

        AssertNoTransfer(
            afterHelperReturn.State,
            forged,
            machine.StepOne(afterHelperReturn.State, forged),
            "EXEC_CALL_DEPTH_INVARIANT");
    }

    /// <summary>
    /// Proves exhausted instruction budget precedes nonterminal envelope validation for default call-stack, missing
    /// memory, and stale-return forgeries, without laundering any malformed state into a transfer.
    /// </summary>
    [Fact]
    public void ZeroBudgetPrecedesMalformedNonterminalStateEnvelopeValidation()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var state = activation.State!;
        var operations = machine.CreatePreparedOperationalState(new BudgetState(0));
        var malformedStates = new[]
        {
            state with { CallStack = default },
            state with { Memory = null! },
            state with { ReturnValue = OptionalValue<ConcreteValue>.Some(domain.ConstInt32(99)) },
        };

        foreach (var malformed in malformedStates)
        {
            AssertBudgetExhaustedWithoutTransfer(
                malformed,
                operations,
                machine.StepOne(malformed, operations));
        }
    }

    /// <summary>Proves a forged active MethodDef outside the graph is rejected without spending budget or emitting.</summary>
    [Fact]
    public void ForgedActiveMethodFailsAtomically()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var rootFrame = Assert.Single(activation.State!.CallStack);
        var forged = activation.State with
        {
            CallStack = ImmutableArray.Create(rootFrame with { Method = Method(99) }),
        };
        var operations = machine.CreatePreparedOperationalState(new BudgetState(10));

        AssertNoTransfer(
            forged,
            operations,
            machine.StepOne(forged, operations),
            "EXEC_CALL_PLAN_INVALID");
    }

    /// <summary>
    /// Proves one machine instance cannot mix legacy and prepared-graph sessions in either activation order.
    /// </summary>
    [Fact]
    public void LegacyAndPreparedGraphSessionsAreMutuallyExclusiveInEitherOrder()
    {
        var resolver = ConstantCallResolver();
        resolver.Definitions[Legacy] = LegacyDefinition();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var memoryModel = new ConcreteMemoryModel(domain);
        var (receiver, memory) = CreateReceiver(domain);

        var preparedFirst = Machine(domain, resolver, memoryModel);
        Assert.True(preparedFirst.ActivatePreparedGraph(
            graph,
            2,
            ImmutableArray.Create(receiver),
            memory).IsSuccess);
        AssertActivationFailure(
            preparedFirst.ActivateRoot(Legacy, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty),
            MachineRunStatus.Blocked,
            "EXEC_MACHINE_SESSION_MISMATCH");

        var legacyFirst = Machine(domain, resolver, memoryModel);
        Assert.True(legacyFirst.ActivateRoot(
            Legacy,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty).IsSuccess);
        AssertActivationFailure(
            legacyFirst.ActivatePreparedGraph(
                graph,
                2,
                ImmutableArray.Create(receiver),
                memory),
            MachineRunStatus.Blocked,
            "EXEC_MACHINE_SESSION_MISMATCH");
    }

    /// <summary>
    /// Proves legacy call-free execution rejects every prepared-only return-site and depth fact rather than carrying
    /// an impossible multi-frame transcript through an otherwise valid legacy instruction.
    /// </summary>
    [Fact]
    public void LegacyExecutionRejectsPreparedOnlyReplayFacts()
    {
        var resolver = Resolver(LegacyDefinition());
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = machine.ActivateRoot(
            Legacy,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = new MachineOperationalState(new BudgetState(10));
        var root = Assert.Single(activation.State!.CallStack);
        var forgedReturnSite = activation.State with
        {
            CallStack = ImmutableArray.Create(
                root with
                {
                    ReturnSite = new FrameReturnSite(
                        new DirectCallSiteIdentity(Legacy, 0, Helper),
                        callerResumeIlOffset: 1),
                }),
        };
        AssertNoTransfer(
            forgedReturnSite,
            operations,
            machine.StepOne(forgedReturnSite, operations),
            "EXEC_EXECUTION_MODE_STATE_MISMATCH");

        foreach (var forgedOperations in new[]
        {
            operations with { ConfiguredMaximumLogicalCallDepth = 1 },
            operations with { RequiredLogicalCallDepth = 1 },
            operations with
            {
                ObservedLogicalDepthHighWater = 2,
                ActiveFrameDepthHighWater = 2,
            },
        })
        {
            AssertNoTransfer(
                activation.State,
                forgedOperations,
                machine.StepOne(activation.State, forgedOperations),
                "EXEC_EXECUTION_MODE_STATE_MISMATCH");
        }
    }

    /// <summary>
    /// Proves equal prepared-graph activations reuse one resolver-free session while a different sufficient logical
    /// depth configuration cannot silently replace the bound session policy.
    /// </summary>
    [Fact]
    public void RepeatedPreparedActivationRequiresTheSameLogicalDepthConfiguration()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var resolutionCount = resolver.TotalCallCount;
        resolver.ThrowOnUse = true;
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var (receiver, memory) = CreateReceiver(domain);
        var arguments = ImmutableArray.Create(receiver);

        var first = machine.ActivatePreparedGraph(graph, 2, arguments, memory);
        var equal = machine.ActivatePreparedGraph(graph, 2, arguments, memory);
        var differentDepth = machine.ActivatePreparedGraph(graph, 3, arguments, memory);

        Assert.True(first.IsSuccess, first.Failure?.Code);
        Assert.True(equal.IsSuccess, equal.Failure?.Code);
        AssertActivationFailure(
            differentDepth,
            MachineRunStatus.Blocked,
            "EXEC_MACHINE_SESSION_MISMATCH");
        Assert.Equal(resolutionCount, resolver.TotalCallCount);
    }

    /// <summary>
    /// Proves the machine-created operational envelope freezes configured and required depth separately, and that
    /// missing or forged replay facts cannot enter prepared execution.
    /// </summary>
    [Fact]
    public void PreparedOperationalStateFreezesConfiguredAndRequiredDepthFacts()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 3);
        var budget = new BudgetState(10);
        var operations = machine.CreatePreparedOperationalState(budget);

        Assert.Same(budget, operations.Budget);
        Assert.Equal(3, operations.ConfiguredMaximumLogicalCallDepth);
        Assert.Equal(2, operations.RequiredLogicalCallDepth);
        Assert.Equal(1, operations.ObservedLogicalDepthHighWater);
        Assert.Equal(1, operations.ActiveFrameDepthHighWater);

        var manual = new MachineOperationalState(new BudgetState(10));
        AssertNoTransfer(
            activation.State!,
            manual,
            machine.StepOne(activation.State!, manual),
            "EXEC_CALL_DEPTH_INVARIANT");

        foreach (var forged in new[]
        {
            operations with { ConfiguredMaximumLogicalCallDepth = 2 },
            operations with { RequiredLogicalCallDepth = 3 },
        })
        {
            AssertNoTransfer(
                activation.State!,
                forged,
                machine.StepOne(activation.State!, forged),
                "EXEC_CALL_DEPTH_INVARIANT");
        }
    }

    /// <summary>
    /// Proves an empty completed call stack is terminal only when it retains the root's exact typed result.
    /// </summary>
    [Fact]
    public void CompletedStateRequiresTheRootExactTypedResult()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var completed = Run(machine, activation.State!, instructionBudget: 8).Outcome;
        Assert.Equal(MachineRunStatus.Completed, completed.Status);
        Assert.Empty(completed.State.CallStack);
        var missing = completed.State with { ReturnValue = OptionalValue<ConcreteValue>.None };
        var wrongType = completed.State with
        {
            ReturnValue = OptionalValue<ConcreteValue>.Some(domain.DefaultValue(TypeSig.Boolean)),
        };

        AssertNoTransfer(
            missing,
            completed.OperationalState,
            machine.StepOne(missing, completed.OperationalState),
            "EXEC_CALL_TERMINAL_RESULT_INVALID");
        AssertNoTransfer(
            wrongType,
            completed.OperationalState,
            machine.StepOne(wrongType, completed.OperationalState),
            "EXEC_VALUE_TYPE_MISMATCH");
    }

    /// <summary>
    /// Proves a forged completed-depth transcript is rejected before terminal value validation can invoke and be
    /// masked by a throwing value-domain capability.
    /// </summary>
    [Fact]
    public void CompletedDepthInvariantPrecedesTerminalDomainCapability()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ThrowingConcreteDomain();
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            resolver,
            new ConcreteMemoryModel(domain.Inner),
            new InstructionBudgetPolicy());
        var (receiver, memory) = CreateReceiver(domain.Inner);
        var activation = machine.ActivatePreparedGraph(
            graph,
            2,
            ImmutableArray.Create(receiver),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var completed = Run(machine, activation.State!, instructionBudget: 8).Outcome;
        Assert.Equal(MachineRunStatus.Completed, completed.Status);
        var forgedDepth = completed.OperationalState with
        {
            ObservedLogicalDepthHighWater = 1,
            ActiveFrameDepthHighWater = 1,
        };
        domain.ThrowOnStaticType = true;

        AssertNoTransfer(
            completed.State,
            forgedDepth,
            machine.StepOne(completed.State, forgedDepth),
            "EXEC_CALL_DEPTH_INVARIANT");
    }

    /// <summary>
    /// Proves a prepared target latch must identify an admitted null-receiver field load and must retain the rooted
    /// high-water witness established by every earlier interpreted call in its deterministic instruction prefix.
    /// </summary>
    [Fact]
    public void TargetTerminalLatchRequiresFieldBoundaryAndRootedDepthWitness()
    {
        var constantResolver = Resolver(
            RootDefinition(Root, ExactRootBody(Helper)),
            HelperDefinition(Helper, AddBody()));
        constantResolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        constantResolver.Fields[(Root, SecondField.Handle.MetadataToken)] = SecondField;
        constantResolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        var constantGraph = Prepare(constantResolver, Root);
        var constantDomain = new ConcreteDomain();
        var constantMachine = Machine(
            constantDomain,
            constantResolver,
            new ConcreteMemoryModel(constantDomain));
        var constantActivation = ActivateWithReceiver(
            constantMachine,
            constantDomain,
            constantGraph,
            maximumLogicalCallDepth: 2);
        var forgedInstructionLatch = constantActivation.State! with
        {
            CallStack = ImmutableArray<FrameState<ConcreteValue>>.Empty,
            TerminalTargetException = new TargetExceptionInfo(
                TargetExceptionKind.NullReference,
                "FORGED_NON_FIELD_TARGET",
                Helper,
                ilOffset: 2),
        };
        var constantOperations = constantMachine.CreatePreparedOperationalState(new BudgetState(20));
        AssertNoTransfer(
            forgedInstructionLatch,
            constantOperations,
            constantMachine.StepOne(forgedInstructionLatch, constantOperations),
            "EXEC_INVALID_TARGET_TERMINATION");

        var forgedEarlyFieldLatch = constantActivation.State with
        {
            CallStack = ImmutableArray<FrameState<ConcreteValue>>.Empty,
            TerminalTargetException = new TargetExceptionInfo(
                TargetExceptionKind.NullReference,
                "FORGED_EARLY_FIELD_TARGET",
                Root,
                ilOffset: 1),
        };
        var forgedFutureDepth = constantOperations with
        {
            ObservedLogicalDepthHighWater = 2,
            ActiveFrameDepthHighWater = 2,
        };
        AssertNoTransfer(
            forgedEarlyFieldLatch,
            forgedFutureDepth,
            constantMachine.StepOne(forgedEarlyFieldLatch, forgedFutureDepth),
            "EXEC_CALL_DEPTH_INVARIANT");

        var resolver = Resolver(
            RootAfterCallFieldDefinition(Root, Helper),
            HelperDefinition(Helper, AddBody()));
        resolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = machine.ActivatePreparedGraph(
            graph,
            2,
            ImmutableArray.Create(domain.ConstNull(RootType)),
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var target = Run(machine, activation.State!, instructionBudget: 20).Outcome;

        Assert.Equal(MachineRunStatus.TargetException, target.Status);
        Assert.Equal(Root, target.TargetException!.Method);
        Assert.Equal(9, target.TargetException.IlOffset);
        Assert.Equal(2, target.OperationalState.ObservedLogicalDepthHighWater);
        Assert.Equal(2, target.OperationalState.ActiveFrameDepthHighWater);
        var forgedDepth = target.OperationalState with
        {
            ObservedLogicalDepthHighWater = 1,
            ActiveFrameDepthHighWater = 1,
        };
        AssertNoTransfer(
            target.State,
            forgedDepth,
            machine.StepOne(target.State, forgedDepth),
            "EXEC_CALL_DEPTH_INVARIANT");
    }

    /// <summary>
    /// Proves a memory model's arbitrary stable Invalid reason cannot impersonate the VM's capability-exception code
    /// and thereby launder a semantic invalid result into the prepared path's blocked classification.
    /// </summary>
    [Fact]
    public void InvalidMemoryResultCannotImpersonateCapabilityFailure()
    {
        var resolver = Resolver(
            RootDefinition(Root, ExactRootBody(Helper)),
            HelperDefinition(Helper, AddBody()));
        resolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        resolver.Fields[(Root, SecondField.Handle.MetadataToken)] = SecondField;
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var innerMemoryModel = new ConcreteMemoryModel(domain);
        var memoryModel = new ThrowingFieldMemoryModel(
            innerMemoryModel,
            invalidFailureCode: "EXEC_MEMORY_MODEL_FAILURE");
        var machine = Machine(domain, resolver, memoryModel);
        var (receiver, memory) = innerMemoryModel.NewObject(ConcreteMemory.Empty, RootType);
        var activation = machine.ActivatePreparedGraph(
            graph,
            2,
            ImmutableArray.Create(receiver),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = machine.CreatePreparedOperationalState(new BudgetState(10));
        var atField = machine.StepOne(activation.State!, operations);
        Assert.Equal(MachineRunStatus.Ready, atField.Status);

        AssertNoTransfer(
            atField.State,
            atField.OperationalState,
            machine.StepOne(atField.State, atField.OperationalState),
            "EXEC_MEMORY_MODEL_FAILURE");
    }

    /// <summary>
    /// Proves throwing value-domain capabilities are normalized to blocked prepared outcomes during activation,
    /// resumed-frame validation, and an ordinary arithmetic transfer, with every attempted step remaining atomic.
    /// </summary>
    [Fact]
    public void ThrowingValueDomainCapabilitiesAreBlockedAndAtomic()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var activationDomain = new ThrowingConcreteDomain { ThrowOnStaticType = true };
        var activationMachine = new IlMachine<ConcreteValue, ConcreteMemory>(
            activationDomain,
            resolver,
            new ConcreteMemoryModel(activationDomain.Inner),
            new InstructionBudgetPolicy());
        var (activationReceiver, activationMemory) = CreateReceiver(activationDomain.Inner);

        var failedActivation = activationMachine.ActivatePreparedGraph(
            graph,
            2,
            ImmutableArray.Create(activationReceiver),
            activationMemory);

        AssertActivationFailure(
            failedActivation,
            MachineRunStatus.Blocked,
            "EXEC_DOMAIN_ACTIVATION_FAILURE");

        var domain = new ThrowingConcreteDomain();
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            resolver,
            new ConcreteMemoryModel(domain.Inner),
            new InstructionBudgetPolicy());
        var (receiver, memory) = CreateReceiver(domain.Inner);
        var activation = machine.ActivatePreparedGraph(
            graph,
            2,
            ImmutableArray.Create(receiver),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = machine.CreatePreparedOperationalState(new BudgetState(20));
        domain.ThrowOnStaticType = true;

        AssertBlockedWithoutTransfer(
            activation.State!,
            operations,
            machine.StepOne(activation.State!, operations),
            "EXEC_DOMAIN_FAILURE");

        domain.ThrowOnStaticType = false;
        var beforeAdd = RunReadySteps(
            machine,
            activation.State!,
            operations,
            stepCount: 5);
        Assert.Equal(Helper, beforeAdd.State.CallStack[^1].Method);
        Assert.Equal(2, beforeAdd.State.CallStack[^1].IlOffset);
        domain.ThrowOnBinary = true;

        AssertBlockedWithoutTransfer(
            beforeAdd.State,
            beforeAdd.OperationalState,
            machine.StepOne(beforeAdd.State, beforeAdd.OperationalState),
            "EXEC_DOMAIN_FAILURE");
    }

    /// <summary>
    /// Proves a throwing memory-model capability at an ordinary prepared <c>ldfld</c> is blocked without advancing
    /// the root, spending its available instruction unit, changing memory, or emitting an execution event.
    /// </summary>
    [Fact]
    public void ThrowingMemoryModelCapabilityIsBlockedAndAtomic()
    {
        var resolver = Resolver(
            RootDefinition(Root, ExactRootBody(Helper)),
            HelperDefinition(Helper, AddBody()));
        resolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        resolver.Fields[(Root, SecondField.Handle.MetadataToken)] = SecondField;
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var innerMemoryModel = new ConcreteMemoryModel(domain);
        var memoryModel = new ThrowingFieldMemoryModel(innerMemoryModel);
        var machine = Machine(domain, resolver, memoryModel);
        var (receiver, allocatedMemory) = innerMemoryModel.NewObject(ConcreteMemory.Empty, RootType);
        var memory = innerMemoryModel.StoreField(
            innerMemoryModel.StoreField(
                allocatedMemory,
                receiver,
                FirstField,
                domain.ConstInt32(17)),
            receiver,
            SecondField,
            domain.ConstInt32(25));
        var activation = machine.ActivatePreparedGraph(
            graph,
            2,
            ImmutableArray.Create(receiver),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = machine.CreatePreparedOperationalState(new BudgetState(10));
        var atField = machine.StepOne(activation.State!, operations);
        Assert.Equal(MachineRunStatus.Ready, atField.Status);
        Assert.Equal(1, Assert.Single(atField.State.CallStack).IlOffset);
        Assert.Same(memory, atField.State.Memory);

        var failure = machine.StepOne(atField.State, atField.OperationalState);

        AssertBlockedWithoutTransfer(
            atField.State,
            atField.OperationalState,
            failure,
            "EXEC_MEMORY_MODEL_FAILURE");
        Assert.Same(memory, failure.State.Memory);
        Assert.Equal(1, memoryModel.LoadCount);
    }

    /// <summary>
    /// Proves semantic machine-state equality includes structural return-site content while ignoring independent
    /// immutable-array materialization.
    /// </summary>
    [Fact]
    public void SemanticComparerIncludesReturnSiteIdentity()
    {
        var resolver = ConstantCallResolver();
        var graph = Prepare(resolver, Root);
        var domain = new ConcreteDomain();
        var machine = Machine(domain, resolver, new ConcreteMemoryModel(domain));
        var activation = ActivateWithReceiver(machine, domain, graph, maximumLogicalCallDepth: 2);
        var call = RunReadySteps(
            machine,
            activation.State!,
            machine.CreatePreparedOperationalState(new BudgetState(20)),
            stepCount: 3);
        var original = call.State;
        var originalCallee = original.CallStack[1];
        var equalSite = new FrameReturnSite(
            new DirectCallSiteIdentity(Root, 2, Helper),
            callerResumeIlOffset: 7);
        var equal = original with
        {
            CallStack = ImmutableArray.Create(
                original.CallStack[0] with
                {
                    Arguments = original.CallStack[0].Arguments
                        .Select(static item => item)
                        .ToImmutableArray(),
                },
                originalCallee with
                {
                    Arguments = originalCallee.Arguments
                        .Select(static item => item)
                        .ToImmutableArray(),
                    ReturnSite = equalSite,
                }),
        };
        var different = equal with
        {
            CallStack = equal.CallStack.SetItem(
                1,
                equal.CallStack[1] with
                {
                    ReturnSite = new FrameReturnSite(
                        new DirectCallSiteIdentity(Root, 3, Helper),
                        callerResumeIlOffset: 7),
                }),
        };
        var comparer = new MachineStateSemanticComparer<ConcreteValue, ConcreteMemory>(domain);

        Assert.True(comparer.Equals(original, equal));
        Assert.Equal(comparer.GetHashCode(original), comparer.GetHashCode(equal));
        Assert.False(comparer.Equals(original, different));
        Assert.False(comparer.Equals(different, original));
    }

    private static GraphResolver ConstantCallResolver()
    {
        var resolver = Resolver(
            RootDefinition(Root, ConstantCallBody(Helper)),
            HelperDefinition(Helper, AddBody()));
        resolver.Calls[(Root, Helper.MetadataToken)] = Target(Helper);
        return resolver;
    }

    private static GraphResolver Resolver(params ResolvedMethodDefinition[] definitions)
    {
        var resolver = new GraphResolver();
        foreach (var definition in definitions)
        {
            resolver.Definitions.Add(definition.Method, definition);
        }

        return resolver;
    }

    private static FrozenMethodGraphPlan Prepare(GraphResolver resolver, MethodHandle root)
    {
        var result = new MethodGraphPlanner(resolver).Prepare(root);
        Assert.True(result.IsSuccess, result.Failure?.Code);
        return result.Plan!;
    }

    private static IlMachine<ConcreteValue, ConcreteMemory> Machine(
        ConcreteDomain domain,
        IResolutionServices resolver,
        IMemoryModel<ConcreteValue, ConcreteMemory> memoryModel) =>
        new(domain, resolver, memoryModel, new InstructionBudgetPolicy());

    private static MachineActivationResult<ConcreteValue, ConcreteMemory> ActivateWithReceiver(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        ConcreteDomain domain,
        FrozenMethodGraphPlan graph,
        int maximumLogicalCallDepth)
    {
        var (receiver, memory) = CreateReceiver(domain);
        var activation = machine.ActivatePreparedGraph(
            graph,
            maximumLogicalCallDepth,
            ImmutableArray.Create(receiver),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        return activation;
    }

    private static (ConcreteValue Receiver, ConcreteMemory Memory) CreateReceiver(ConcreteDomain domain)
    {
        var memoryModel = new ConcreteMemoryModel(domain);
        return memoryModel.NewObject(ConcreteMemory.Empty, RootType);
    }

    private static RunResult Run(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        MachineState<ConcreteValue, ConcreteMemory> state,
        int instructionBudget) =>
        Run(
            machine,
            state,
            machine.CreatePreparedOperationalState(new BudgetState(instructionBudget)));

    private static RunResult Run(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        MachineState<ConcreteValue, ConcreteMemory> state,
        MachineOperationalState operations)
    {
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        var outcomes = ImmutableArray.CreateBuilder<StepOutcome<ConcreteValue, ConcreteMemory>>();
        StepOutcome<ConcreteValue, ConcreteMemory> outcome;
        do
        {
            outcome = machine.StepOne(state, operations);
            outcomes.Add(outcome);
            events.AddRange(outcome.Events);
            state = outcome.State;
            operations = outcome.OperationalState;
        }
        while (outcome.Status == MachineRunStatus.Ready && outcomes.Count < 100);

        Assert.True(outcomes.Count < 100, "Prepared execution did not terminate within the test ceiling.");
        return new RunResult(outcome, events.ToImmutable(), outcomes.ToImmutable());
    }

    private static StepOutcome<ConcreteValue, ConcreteMemory> RunReadySteps(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        MachineState<ConcreteValue, ConcreteMemory> state,
        MachineOperationalState operations,
        int stepCount)
    {
        StepOutcome<ConcreteValue, ConcreteMemory>? outcome = null;
        for (var index = 0; index < stepCount; index++)
        {
            outcome = machine.StepOne(state, operations);
            Assert.Equal(MachineRunStatus.Ready, outcome.Status);
            state = outcome.State;
            operations = outcome.OperationalState;
        }

        return outcome!;
    }

    private static void AssertActivationFailure(
        MachineActivationResult<ConcreteValue, ConcreteMemory> activation,
        MachineRunStatus status,
        string code)
    {
        Assert.False(activation.IsSuccess);
        Assert.Null(activation.State);
        Assert.Equal(status, activation.Status);
        Assert.Equal(code, Assert.IsType<ExecutionFailure>(activation.Failure).Code);
    }

    private static void AssertNoTransfer(
        MachineState<ConcreteValue, ConcreteMemory> state,
        MachineOperationalState operations,
        StepOutcome<ConcreteValue, ConcreteMemory> outcome,
        string code) =>
        AssertFailedWithoutTransfer(
            state,
            operations,
            outcome,
            MachineRunStatus.InvalidProgram,
            code);

    private static void AssertBlockedWithoutTransfer(
        MachineState<ConcreteValue, ConcreteMemory> state,
        MachineOperationalState operations,
        StepOutcome<ConcreteValue, ConcreteMemory> outcome,
        string code) =>
        AssertFailedWithoutTransfer(
            state,
            operations,
            outcome,
            MachineRunStatus.Blocked,
            code);

    private static void AssertFailedWithoutTransfer(
        MachineState<ConcreteValue, ConcreteMemory> state,
        MachineOperationalState operations,
        StepOutcome<ConcreteValue, ConcreteMemory> outcome,
        MachineRunStatus status,
        string code)
    {
        Assert.Same(state, outcome.State);
        Assert.Same(operations, outcome.OperationalState);
        Assert.Equal(status, outcome.Status);
        Assert.Equal(code, Assert.IsType<ExecutionFailure>(outcome.Failure).Code);
        Assert.Empty(outcome.Events);
    }

    private static void AssertBudgetExhaustedWithoutTransfer(
        MachineState<ConcreteValue, ConcreteMemory> state,
        MachineOperationalState operations,
        StepOutcome<ConcreteValue, ConcreteMemory> outcome)
    {
        Assert.Same(state, outcome.State);
        Assert.Same(operations, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.BudgetExhausted, outcome.Status);
        Assert.Null(outcome.Failure);
        Assert.Empty(outcome.Events);
    }

    private static void AssertEvent(
        DebugEvent item,
        DebugEventKind kind,
        MethodHandle method,
        int ilOffset,
        string instruction)
    {
        Assert.Equal(kind, item.Kind);
        Assert.Equal(method, item.Method);
        Assert.Equal(ilOffset, item.IlOffset);
        Assert.Equal(instruction, item.Instruction);
    }

    private static void AssertInt32(int expected, ConcreteValue actual)
    {
        var domain = new ConcreteDomain();
        Assert.True(domain.TryGetConstInt32(actual, out var value));
        Assert.Equal(expected, value);
    }

    private static MethodHandle Method(int row) => new(Module, 0x06000000 | row);

    private static ResolvedField Field(int row) => new(
        new FieldHandle(Module, 0x04000000 | row),
        RootType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    private static ResolvedMethodCallTarget Target(MethodHandle method) =>
        new(method, HelperSignature());

    private static MethodCallSignatureShape RootSignature() => new(
        RootType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: true,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray<TypeSig>.Empty,
        TypeSig.Int32);

    private static MethodCallSignatureShape HelperSignature() => new(
        HelperType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: false,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray.Create(TypeSig.Int32, TypeSig.Int32),
        TypeSig.Int32);

    private static ResolvedMethodDefinition RootDefinition(MethodHandle method, byte[] code) =>
        Definition(method, RootSignature(), code, maxStack: 3);

    private static ResolvedMethodDefinition RootAfterCallFieldDefinition(
        MethodHandle method,
        MethodHandle helper)
    {
        var code = new List<byte> { 0x02, 0x17, 0x18 };
        EmitToken(code, 0x28, helper.MetadataToken);
        code.Add(0x0A);
        EmitToken(code, 0x7B, FirstField.Handle.MetadataToken);
        code.Add(0x2A);
        return new ResolvedMethodDefinition(
            method,
            IlBody.Create(
                maxStack: 3,
                code.ToArray(),
                localVariablesInitialized: true,
                localSignatureToken: 0x11000001),
            new MethodSignatureShape(
                RootSignature(),
                ImmutableArray.Create(TypeSig.Int32)));
    }

    private static ResolvedMethodDefinition HelperDefinition(MethodHandle method, byte[] code) =>
        Definition(method, HelperSignature(), code, maxStack: 2);

    private static ResolvedMethodDefinition LegacyDefinition()
    {
        var signature = new MethodCallSignatureShape(
            HelperType,
            MethodCallingConventionKind.Default,
            hasImplicitThis: false,
            hasExplicitThis: false,
            genericParameterCount: 0,
            ImmutableArray<TypeSig>.Empty,
            TypeSig.Int32);
        return Definition(Legacy, signature, [0x16, 0x2A], maxStack: 1);
    }

    private static ResolvedMethodDefinition Definition(
        MethodHandle method,
        MethodCallSignatureShape signature,
        byte[] code,
        int maxStack) =>
        new(
            method,
            IlBody.Create(maxStack, code),
            new MethodSignatureShape(signature, ImmutableArray<TypeSig>.Empty));

    private static byte[] ExactRootBody(MethodHandle helper)
    {
        var code = new List<byte> { 0x02 };
        EmitToken(code, 0x7B, FirstField.Handle.MetadataToken);
        code.Add(0x02);
        EmitToken(code, 0x7B, SecondField.Handle.MetadataToken);
        EmitToken(code, 0x28, helper.MetadataToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] ConstantCallBody(MethodHandle helper)
    {
        var code = new List<byte> { 0x17, 0x18 };
        EmitToken(code, 0x28, helper.MetadataToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] ForwardingCallBody(MethodHandle helper)
    {
        var code = new List<byte> { 0x02, 0x03 };
        EmitToken(code, 0x28, helper.MetadataToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] RepeatedCallBody(MethodHandle helper)
    {
        var code = new List<byte> { 0x17, 0x18 };
        EmitToken(code, 0x28, helper.MetadataToken);
        code.Add(0x19);
        code.Add(0x1A);
        EmitToken(code, 0x28, helper.MetadataToken);
        code.Add(0x58);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] AddBody() => [0x02, 0x03, 0x58, 0x2A];

    private static void EmitToken(List<byte> code, byte opcode, int token)
    {
        code.Add(opcode);
        code.Add((byte)token);
        code.Add((byte)(token >> 8));
        code.Add((byte)(token >> 16));
        code.Add((byte)(token >> 24));
    }

    private sealed record RunResult(
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        ImmutableArray<DebugEvent> Events,
        ImmutableArray<StepOutcome<ConcreteValue, ConcreteMemory>> Outcomes);

    private sealed class ThrowingConcreteDomain : IValueDomain<ConcreteValue>
    {
        internal ConcreteDomain Inner { get; } = new();

        internal bool ThrowOnStaticType { get; set; }

        internal bool ThrowOnBinary { get; set; }

        public ConcreteValue Bottom(TypeSig type) => Inner.Bottom(type);

        public bool IsBottom(ConcreteValue value) => Inner.IsBottom(value);

        public ConcreteValue Top(TypeSig type) => Inner.Top(type);

        public ConcreteValue DefaultValue(TypeSig type) => Inner.DefaultValue(type);

        public ConcreteValue ConstInt32(int value) => Inner.ConstInt32(value);

        public ConcreteValue Join(ConcreteValue a, ConcreteValue b) => Inner.Join(a, b);

        public bool IsLessThanOrEqual(ConcreteValue a, ConcreteValue b) =>
            Inner.IsLessThanOrEqual(a, b);

        public ConcreteValue Meet(ConcreteValue a, ConcreteValue b) => Inner.Meet(a, b);

        public ConcreteValue Widen(ConcreteValue prev, ConcreteValue next) => Inner.Widen(prev, next);

        public TypeSig GetStaticType(ConcreteValue value) => ThrowOnStaticType
            ? throw new SyntheticCapabilityException()
            : Inner.GetStaticType(value);

        public StackKind GetStackKind(ConcreteValue value) => Inner.GetStackKind(value);

        public bool TryGetConstInt32(ConcreteValue value, out int c) =>
            Inner.TryGetConstInt32(value, out c);

        public ConcreteValue ApplyBinary(BinaryOp op, ConcreteValue a, ConcreteValue b) => ThrowOnBinary
            ? throw new SyntheticCapabilityException()
            : Inner.ApplyBinary(op, a, b);
    }

    private sealed class ThrowingFieldMemoryModel : IMemoryModel<ConcreteValue, ConcreteMemory>
    {
        private readonly ConcreteMemoryModel inner;
        private readonly string? invalidFailureCode;

        internal ThrowingFieldMemoryModel(
            ConcreteMemoryModel inner,
            string? invalidFailureCode = null)
        {
            this.inner = inner;
            this.invalidFailureCode = invalidFailureCode;
        }

        internal int LoadCount { get; private set; }

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
            LoadCount++;
            if (invalidFailureCode is not null)
            {
                return MemoryLoadResult<ConcreteValue>.NonExact(
                    MemoryLoadKind.Invalid,
                    invalidFailureCode);
            }

            throw new SyntheticCapabilityException();
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
    }

    private sealed class SyntheticCapabilityException : Exception
    {
    }

    private sealed class GraphResolver : IResolutionServices
    {
        internal Dictionary<MethodHandle, ResolvedMethodDefinition> Definitions { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedField> Fields { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedMethodCallTarget> Calls { get; } = [];

        internal bool ThrowOnUse { get; set; }

        internal int TotalCallCount { get; private set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            CountOrThrow();
            return Definitions.TryGetValue(method, out var definition)
                ? ResolutionResult<ResolvedMethodDefinition>.Success(definition)
                : ResolutionResult<ResolvedMethodDefinition>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_METHOD_UNAVAILABLE",
                    "Synthetic method definition was not configured.");
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
        {
            CountOrThrow();
            return Fields.TryGetValue((contextMethod, metadataToken), out var field)
                ? ResolutionResult<ResolvedField>.Success(field)
                : ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_FIELD_UNAVAILABLE",
                    "Synthetic field descriptor was not configured.");
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            CountOrThrow();
            return Calls.TryGetValue((contextMethod, metadataToken), out var target)
                ? ResolutionResult<ResolvedMethodCallTarget>.Success(target)
                : ResolutionResult<ResolvedMethodCallTarget>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_CALL_UNAVAILABLE",
                    "Synthetic call target was not configured.");
        }

        private void CountOrThrow()
        {
            if (ThrowOnUse)
            {
                throw new InvalidOperationException("Prepared execution must not resolve metadata again.");
            }

            TotalCallCount++;
        }
    }
}
