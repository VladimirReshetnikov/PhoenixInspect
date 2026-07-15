using System.Collections.Immutable;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>Exercises W4.3 approximate-field transfer through the generic IL machine.</summary>
public sealed class ProvenanceFieldTransferTests
{
    private const string SourceSha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ImportedObjectSha256 =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    private static readonly ModuleHandle Module = ModuleHandle.FromContentIdentity(
        ModuleContentIdentity.FromMetadata(
            new Guid("00000000-0000-0000-0000-000000000432"),
            "ProvenanceFieldTransferTests-W4.3"u8),
        44,
        88);

    private static readonly MethodHandle Method = new(Module, 0x06000001);
    private static readonly TypeSig DeclaringType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.ProvenanceFieldFixture");
    private static readonly ResolvedField Int32Field = CreateField();

    /// <summary>Checks exact field loads remain provenance-free and policy-independent.</summary>
    [Theory]
    [InlineData(UnknownExecutionPolicy.ExactOnly)]
    [InlineData(UnknownExecutionPolicy.ExplainedInt32)]
    public void ExactFieldLoadTransfersOnceUnderEitherUnknownPolicy(UnknownExecutionPolicy policy)
    {
        var domain = new ProvenanceConcreteDomain();
        var memoryModel = new TestMemoryModel(
            (_, _) => MemoryLoadResult<ProvenanceConcreteValue>.Exact(domain.ConstInt32(41)));
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel, policy);
        var boundary = AdvanceToField(machine, domain.ObjectReference(1, DeclaringType), 4);

        var outcome = machine.StepOne(boundary.State, boundary.Operations);

        Assert.Equal(MachineRunStatus.Ready, outcome.Status);
        Assert.Equal(2, outcome.OperationalState.Budget.InstructionBudget);
        Assert.Same(TestMemory.Instance, outcome.State.Memory);
        var frame = Assert.Single(outcome.State.CallStack);
        Assert.Equal(6, frame.IlOffset);
        var value = Assert.Single(frame.EvalStack);
        Assert.True(domain.TryGetConstInt32(value, out var exact));
        Assert.Equal(41, exact);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(0, domain.InternedNodeCount);
        var executed = Assert.Single(outcome.Events);
        Assert.Equal(DebugEventKind.InstructionExecuted, executed.Kind);
        Assert.Equal(1, executed.IlOffset);
        Assert.Equal("LoadField", executed.Instruction);
        Assert.Null(executed.FieldEvidence);
    }

    /// <summary>Checks Partial and Unavailable evidence complete one truthful unknown-producing transfer.</summary>
    [Theory]
    [InlineData(EvaluationEvidenceStatus.Partial, 2)]
    [InlineData(EvaluationEvidenceStatus.Unavailable, 0)]
    public void ExplainedPolicyTransfersStructuredApproximationWithEvidenceEvent(
        EvaluationEvidenceStatus status,
        int observedLength)
    {
        var evidence = CreateEvidence(status, observedLength);
        var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var memoryModel = EvidenceMemory(evidence);
        var receiver = domain.Inner.ObjectReference(1, DeclaringType);
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel);
        var boundary = AdvanceToField(machine, receiver, 4);

        var outcome = machine.StepOne(boundary.State, boundary.Operations);

        Assert.Equal(MachineRunStatus.Ready, outcome.Status);
        Assert.Equal(2, outcome.OperationalState.Budget.InstructionBudget);
        Assert.Same(TestMemory.Instance, outcome.State.Memory);
        var frame = Assert.Single(outcome.State.CallStack);
        Assert.Equal(6, frame.IlOffset);
        var result = Assert.Single(frame.EvalStack);
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(result));
        Assert.False(domain.TryGetConstInt32(result, out _));
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(1, domain.CapabilityCallCount);
        Assert.Same(receiver, domain.LastReceiver);
        Assert.Same(evidence, domain.LastEvidence);

        Assert.Collection(
            outcome.Events,
            executed =>
            {
                Assert.Equal(DebugEventKind.InstructionExecuted, executed.Kind);
                Assert.Equal(Method, executed.Method);
                Assert.Equal(1, executed.IlOffset);
                Assert.Equal("LoadField", executed.Instruction);
                Assert.Null(executed.FieldEvidence);
            },
            precisionLost =>
            {
                Assert.Equal(DebugEventKind.ValuePrecisionLost, precisionLost.Kind);
                Assert.Equal(Method, precisionLost.Method);
                Assert.Equal(1, precisionLost.IlOffset);
                Assert.Equal("LoadField", precisionLost.Instruction);
                Assert.Same(evidence, precisionLost.FieldEvidence);
            });

        Assert.True(result.TryGetLineageRoot(out var root));
        var graph = domain.Inner.CaptureLineage(result);
        Assert.Equal(2, graph.Nodes.Length);
        var fieldNode = Assert.IsType<FieldLoadTransformLineageNode>(
            graph.Nodes.Single(node => node.Id == root));
        var origin = Assert.IsType<InputOriginLineageNode>(
            graph.Nodes.Single(node => node.Id == fieldNode.InputOrigin));
        Assert.Equal(ImportedObjectSha256, fieldNode.Receiver.Sha256);
        Assert.Equal(Int32Field, fieldNode.Field);
        Assert.Equal(origin.Id, Assert.Single(fieldNode.Dependencies));
        Assert.Equal(ProvenanceInputKind.ImportedField, origin.Origin.Kind);
        Assert.Equal(0, origin.Origin.OriginIndex);
        Assert.Equal(status, origin.Origin.Evidence);
        Assert.Equal(evidence.Sha256, origin.Origin.SourceKey.Sha256);
        Assert.Equal(evidence.ReasonCode, origin.Origin.ReasonCode);
        Assert.Equal(TypeSig.Int32, origin.StaticType);
    }

    /// <summary>Checks policy, legacy code-only results, and absent materialization capability all block atomically.</summary>
    [Fact]
    public void ApproximationRequiresPolicyStructuredEvidenceAndDomainCapability()
    {
        var evidence = CreateEvidence(EvaluationEvidenceStatus.Partial, 2);

        var exactOnlyDomain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var exactOnlyMemory = EvidenceMemory(evidence);
        var exactOnlyMachine = CreateMachine(
            exactOnlyDomain,
            CreateDefinition(),
            exactOnlyMemory,
            UnknownExecutionPolicy.ExactOnly);
        var exactOnlyBoundary = AdvanceToField(
            exactOnlyMachine,
            exactOnlyDomain.Inner.ObjectReference(1, DeclaringType),
            4);
        var exactOnly = exactOnlyMachine.StepOne(exactOnlyBoundary.State, exactOnlyBoundary.Operations);
        AssertAtomicStop(exactOnly, exactOnlyBoundary, MachineRunStatus.Blocked);
        Assert.Equal(1, exactOnlyMemory.LoadCount);
        Assert.Equal(0, exactOnlyDomain.CapabilityCallCount);
        Assert.Equal(0, exactOnlyDomain.Inner.InternedNodeCount);

        var codeOnlyDomain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var codeOnlyMemory = new TestMemoryModel(
            (_, _) => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                MemoryLoadKind.Partial,
                "MEM_FIELD_PARTIAL_CODE_ONLY"));
        var codeOnlyMachine = CreateMachine(codeOnlyDomain, CreateDefinition(), codeOnlyMemory);
        var codeOnlyBoundary = AdvanceToField(
            codeOnlyMachine,
            codeOnlyDomain.Inner.ObjectReference(1, DeclaringType),
            4);
        var codeOnly = codeOnlyMachine.StepOne(codeOnlyBoundary.State, codeOnlyBoundary.Operations);
        AssertAtomicStop(codeOnly, codeOnlyBoundary, MachineRunStatus.Blocked);
        Assert.Equal("MEM_FIELD_PARTIAL_CODE_ONLY", codeOnly.Failure!.Code);
        Assert.Equal(1, codeOnlyMemory.LoadCount);
        Assert.Equal(0, codeOnlyDomain.CapabilityCallCount);
        Assert.Equal(0, codeOnlyDomain.Inner.InternedNodeCount);

        var capabilityFreeDomain = new PrecisionOnlyDomain();
        var capabilityFreeMemory = EvidenceMemory(evidence);
        var capabilityFreeMachine = CreateMachine(
            capabilityFreeDomain,
            CreateDefinition(),
            capabilityFreeMemory);
        var capabilityFreeBoundary = AdvanceToField(
            capabilityFreeMachine,
            capabilityFreeDomain.Inner.ObjectReference(1, DeclaringType),
            4);
        var capabilityFree = capabilityFreeMachine.StepOne(
            capabilityFreeBoundary.State,
            capabilityFreeBoundary.Operations);
        AssertAtomicStop(capabilityFree, capabilityFreeBoundary, MachineRunStatus.Blocked);
        Assert.Equal(1, capabilityFreeMemory.LoadCount);
        Assert.Equal(0, capabilityFreeDomain.Inner.InternedNodeCount);
    }

    /// <summary>Checks Conflict remains blocked and Invalid remains invalid under both policies.</summary>
    [Theory]
    [InlineData(MemoryLoadKind.Conflict, MachineRunStatus.Blocked)]
    [InlineData(MemoryLoadKind.Invalid, MachineRunStatus.InvalidProgram)]
    public void ConflictAndInvalidNeverTransferUnderEitherPolicy(
        MemoryLoadKind kind,
        MachineRunStatus expectedStatus)
    {
        foreach (var policy in new[]
                 {
                     UnknownExecutionPolicy.ExactOnly,
                     UnknownExecutionPolicy.ExplainedInt32,
                 })
        {
            var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
            var memoryModel = new TestMemoryModel(
                (_, _) => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                    kind,
                    kind == MemoryLoadKind.Conflict ? "MEM_FIELD_CONFLICT" : "MEM_FIELD_INVALID"));
            var machine = CreateMachine(domain, CreateDefinition(), memoryModel, policy);
            var boundary = AdvanceToField(
                machine,
                domain.Inner.ObjectReference(1, DeclaringType),
                4);

            var outcome = machine.StepOne(boundary.State, boundary.Operations);

            AssertAtomicStop(outcome, boundary, expectedStatus);
            Assert.Equal(1, memoryModel.LoadCount);
            Assert.Equal(0, domain.CapabilityCallCount);
            Assert.Equal(0, domain.Inner.InternedNodeCount);
        }
    }

    /// <summary>Checks instruction availability takes precedence over memory and approximation capabilities.</summary>
    [Fact]
    public void ZeroBudgetPrecedesFieldEvidenceAndLeavesEveryObservableUnchanged()
    {
        var evidence = CreateEvidence(EvaluationEvidenceStatus.Partial, 2);
        var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var memoryModel = EvidenceMemory(evidence);
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel);
        var prepared = AdvanceToField(
            machine,
            domain.Inner.ObjectReference(1, DeclaringType),
            2);
        var exhaustedOperations = new MachineOperationalState(new BudgetState(0));
        var boundary = new FieldBoundary(prepared.State, exhaustedOperations);

        var outcome = machine.StepOne(boundary.State, boundary.Operations);

        AssertAtomicStop(outcome, boundary, MachineRunStatus.BudgetExhausted, expectsFailure: false);
        Assert.Equal(0, memoryModel.LoadCount);
        Assert.Equal(0, domain.CapabilityCallCount);
        Assert.Equal(0, domain.Inner.InternedNodeCount);
    }

    /// <summary>Checks an admitted approximate load consumes exactly the final available instruction unit.</summary>
    [Fact]
    public void ApproximateFieldLoadCanConsumeTheLastInstructionBudgetUnit()
    {
        var evidence = CreateEvidence(EvaluationEvidenceStatus.Unavailable, 0);
        var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var memoryModel = EvidenceMemory(evidence);
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel);
        var prepared = AdvanceToField(
            machine,
            domain.Inner.ObjectReference(1, DeclaringType),
            2);
        var operations = new MachineOperationalState(new BudgetState(1));

        var outcome = machine.StepOne(prepared.State, operations);

        Assert.Equal(MachineRunStatus.Ready, outcome.Status);
        Assert.Equal(0, outcome.OperationalState.Budget.InstructionBudget);
        Assert.Equal(6, Assert.Single(outcome.State.CallStack).IlOffset);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(1, domain.CapabilityCallCount);
        Assert.Equal(2, outcome.Events.Length);
    }

    /// <summary>Checks an unknown field result composes with arithmetic and preserves full deterministic event order.</summary>
    [Fact]
    public void ApproximateFieldResultFlowsThroughArithmeticAndReturn()
    {
        var evidence = CreateEvidence(EvaluationEvidenceStatus.Partial, 3);
        var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var memoryModel = EvidenceMemory(evidence);
        var machine = CreateMachine(domain, CreateDefinition(includeArithmetic: true), memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.Inner.ObjectReference(1, DeclaringType)),
            TestMemory.Instance);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var run = Run(machine, activation.State!, 5);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Equal(0, run.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Same(TestMemory.Instance, run.Outcome.State.Memory);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(1, domain.CapabilityCallCount);
        var result = run.Outcome.State.ReturnValue.Value;
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(result));
        Assert.True(result.TryGetLineageRoot(out var root));
        var graph = domain.Inner.CaptureLineage(result);
        Assert.Equal(3, graph.Nodes.Length);
        var binary = Assert.IsType<BinaryTransformLineageNode>(
            graph.Nodes.Single(node => node.Id == root));
        var field = Assert.IsType<FieldLoadTransformLineageNode>(
            graph.Nodes.Single(static node => node.Kind == LineageNodeKind.FieldLoadTransform));
        Assert.Equal(BinaryOp.Add, binary.Operation);
        Assert.Equal(field.Id, binary.Left.Predecessor);
        Assert.Null(binary.Left.ExactInt32);
        Assert.Equal(1, binary.Right.ExactInt32);
        Assert.Null(binary.Right.Predecessor);

        Assert.Equal(
            new[]
            {
                (DebugEventKind.InstructionExecuted, 0, "LoadArgument", (string?)null),
                (DebugEventKind.InstructionExecuted, 1, "LoadField", null),
                (DebugEventKind.ValuePrecisionLost, 1, "LoadField", evidence.Sha256),
                (DebugEventKind.InstructionExecuted, 6, "LoadInt32", null),
                (DebugEventKind.InstructionExecuted, 7, "Add", null),
                (DebugEventKind.InstructionExecuted, 8, "Return", null),
                (DebugEventKind.FramePopped, 8, "Return", null),
            },
            EventFingerprint(run.Events));
    }

    /// <summary>Checks Partial and Unavailable produce equal machine semantics but distinct explanations.</summary>
    [Fact]
    public void PartialAndUnavailableStatesAreSemanticallyEqualButLineageDistinct()
    {
        var partial = ExecuteOneApproximateField(
            CreateEvidence(EvaluationEvidenceStatus.Partial, 2));
        var unavailable = ExecuteOneApproximateField(
            CreateEvidence(EvaluationEvidenceStatus.Unavailable, 0));

        var comparer = new MachineStateSemanticComparer<ProvenanceConcreteValue, TestMemory>(partial.Domain);
        Assert.True(comparer.Equals(partial.Outcome.State, unavailable.Outcome.State));
        Assert.Equal(
            comparer.GetHashCode(partial.Outcome.State),
            comparer.GetHashCode(unavailable.Outcome.State));

        var partialValue = Assert.Single(Assert.Single(partial.Outcome.State.CallStack).EvalStack);
        var unavailableValue = Assert.Single(Assert.Single(unavailable.Outcome.State.CallStack).EvalStack);
        Assert.Equal(partialValue, unavailableValue);
        Assert.True(partialValue.TryGetLineageRoot(out var partialRoot));
        Assert.True(unavailableValue.TryGetLineageRoot(out var unavailableRoot));
        Assert.NotEqual(partialRoot, unavailableRoot);
        Assert.NotEqual(
            partial.Domain.CaptureLineage(partialValue).Sha256,
            unavailable.Domain.CaptureLineage(unavailableValue).Sha256);
    }

    /// <summary>Checks fresh domains, evidence objects, and machines reproduce lineage and execution fingerprints.</summary>
    [Fact]
    public void FreshMachinesReplayTheSameApproximateFieldExecution()
    {
        var first = ExecuteFresh();
        var second = ExecuteFresh();

        Assert.Equal(first.Root, second.Root);
        Assert.True(first.GraphBytes.AsSpan().SequenceEqual(second.GraphBytes.AsSpan()));
        Assert.Equal(first.GraphSha256, second.GraphSha256);
        Assert.Equal(first.RemainingBudget, second.RemainingBudget);
        Assert.True(first.Events.SequenceEqual(second.Events));
        Assert.Equal(first.Memory, second.Memory);

        static ReplayRun ExecuteFresh()
        {
            var evidence = CreateEvidence(EvaluationEvidenceStatus.Partial, 2);
            var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
            var memoryModel = EvidenceMemory(evidence);
            var machine = CreateMachine(domain, CreateDefinition(includeArithmetic: true), memoryModel);
            var activation = machine.ActivateRoot(
                Method,
                ImmutableArray.Create(domain.Inner.ObjectReference(7, DeclaringType)),
                TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);
            var run = Run(machine, activation.State!, 8);
            Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
            var result = run.Outcome.State.ReturnValue.Value;
            Assert.True(result.TryGetLineageRoot(out var root));
            var graph = domain.Inner.CaptureLineage(result);
            return new ReplayRun(
                root,
                graph.CanonicalBytes,
                graph.Sha256,
                run.Outcome.OperationalState.Budget.InstructionBudget,
                EventFingerprint(run.Events).ToImmutableArray(),
                run.Outcome.State.Memory);
        }
    }

    /// <summary>Checks malformed domain capability outputs and capability exceptions cannot partially transfer.</summary>
    [Theory]
    [InlineData(FieldCapabilityBehavior.Exact)]
    [InlineData(FieldCapabilityBehavior.Bottom)]
    [InlineData(FieldCapabilityBehavior.BareTop)]
    [InlineData(FieldCapabilityBehavior.WrongType)]
    [InlineData(FieldCapabilityBehavior.ForeignRoot)]
    [InlineData(FieldCapabilityBehavior.Null)]
    [InlineData(FieldCapabilityBehavior.Throw)]
    [InlineData(FieldCapabilityBehavior.UndefinedPrecision)]
    [InlineData(FieldCapabilityBehavior.ThrowingPrecision)]
    [InlineData(FieldCapabilityBehavior.ThrowingStaticType)]
    public void MaliciousApproximationCapabilityIsNormalizedAtomically(FieldCapabilityBehavior behavior)
    {
        var evidence = CreateEvidence(EvaluationEvidenceStatus.Partial, 2);
        var domain = new FieldDomainDouble(behavior);
        var memoryModel = EvidenceMemory(evidence);
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel);
        var boundary = AdvanceToField(
            machine,
            domain.Inner.ObjectReference(1, DeclaringType),
            4);
        var nodesBefore = domain.Inner.InternedNodeCount;

        var outcome = machine.StepOne(boundary.State, boundary.Operations);

        AssertAtomicStop(outcome, boundary, MachineRunStatus.InvalidProgram);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(1, domain.CapabilityCallCount);
        Assert.Equal(nodesBefore, domain.Inner.InternedNodeCount);
        if (behavior == FieldCapabilityBehavior.UndefinedPrecision)
        {
            Assert.Equal("EXEC_VALUE_PRECISION_INVALID", outcome.Failure!.Code);
        }
        else if (behavior is FieldCapabilityBehavior.ThrowingPrecision or FieldCapabilityBehavior.ThrowingStaticType)
        {
            Assert.Equal("EXEC_DOMAIN_FAILURE", outcome.Failure!.Code);
        }
    }

    /// <summary>Checks evidence for a different valid field cannot be substituted for the frozen instruction field.</summary>
    [Theory]
    [InlineData(UnknownExecutionPolicy.ExactOnly)]
    [InlineData(UnknownExecutionPolicy.ExplainedInt32)]
    public void MismatchedStructuredFieldEvidenceIsRejectedBeforeMaterialization(
        UnknownExecutionPolicy policy)
    {
        var evidence = CreateEvidence(
            EvaluationEvidenceStatus.Partial,
            2,
            CreateField(metadataToken: 0x04000002));
        var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var memoryModel = EvidenceMemory(evidence);
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel, policy);
        var boundary = AdvanceToField(
            machine,
            domain.Inner.ObjectReference(1, DeclaringType),
            4);

        var outcome = machine.StepOne(boundary.State, boundary.Operations);

        AssertAtomicStop(outcome, boundary, MachineRunStatus.InvalidProgram);
        Assert.Equal("EXEC_FIELD_EVIDENCE_MISMATCH", outcome.Failure!.Code);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(0, domain.CapabilityCallCount);
        Assert.Equal(0, domain.Inner.InternedNodeCount);
    }

    /// <summary>Checks exact typed-null behavior remains a target exception and never becomes precision loss.</summary>
    [Theory]
    [InlineData(UnknownExecutionPolicy.ExactOnly)]
    [InlineData(UnknownExecutionPolicy.ExplainedInt32)]
    public void TypedNullFieldLoadRemainsTargetExceptionUnderEitherPolicy(UnknownExecutionPolicy policy)
    {
        var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var target = new TargetExceptionInfo(TargetExceptionKind.NullReference, "MEM_NULL_RECEIVER");
        var memoryModel = new TestMemoryModel(
            (_, _) => MemoryLoadResult<ProvenanceConcreteValue>.ForTargetException(target));
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel, policy);
        var boundary = AdvanceToField(machine, domain.Inner.ConstNull(DeclaringType), 4);

        var outcome = machine.StepOne(boundary.State, boundary.Operations);

        Assert.Equal(MachineRunStatus.TargetException, outcome.Status);
        Assert.Equal(2, outcome.OperationalState.Budget.InstructionBudget);
        Assert.Same(TestMemory.Instance, outcome.State.Memory);
        Assert.Empty(outcome.State.CallStack);
        Assert.Equal(TargetExceptionKind.NullReference, outcome.TargetException!.Kind);
        Assert.Equal(Method, outcome.TargetException.Method);
        Assert.Equal(1, outcome.TargetException.IlOffset);
        var raised = Assert.Single(outcome.Events);
        Assert.Equal(DebugEventKind.TargetExceptionRaised, raised.Kind);
        Assert.Null(raised.FieldEvidence);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(0, domain.CapabilityCallCount);
        Assert.Equal(0, domain.Inner.InternedNodeCount);
    }

    private static ApproximateFieldRun ExecuteOneApproximateField(FieldLoadEvidence evidence)
    {
        var domain = new FieldDomainDouble(FieldCapabilityBehavior.Normal);
        var memoryModel = EvidenceMemory(evidence);
        var machine = CreateMachine(domain, CreateDefinition(), memoryModel);
        var boundary = AdvanceToField(
            machine,
            domain.Inner.ObjectReference(1, DeclaringType),
            4);
        var outcome = machine.StepOne(boundary.State, boundary.Operations);
        Assert.Equal(MachineRunStatus.Ready, outcome.Status);
        return new ApproximateFieldRun(domain.Inner, outcome);
    }

    private static IlMachine<ProvenanceConcreteValue, TestMemory> CreateMachine(
        IValueDomain<ProvenanceConcreteValue> domain,
        ResolvedMethodDefinition definition,
        TestMemoryModel memoryModel,
        UnknownExecutionPolicy policy = UnknownExecutionPolicy.ExplainedInt32) =>
        new(
            domain,
            new FixedResolver(definition, Int32Field),
            memoryModel,
            new InstructionBudgetPolicy(),
            policy);

    private static ResolvedMethodDefinition CreateDefinition(bool includeArithmetic = false)
    {
        var il = new List<byte>(9);
        il.Add(0x02); // ldarg.0
        il.AddRange([0x7B, 0x01, 0x00, 0x00, 0x04]); // ldfld 0x04000001
        if (includeArithmetic)
        {
            il.Add(0x17); // ldc.i4.1
            il.Add(0x58); // add
        }

        il.Add(0x2A); // ret
        return new ResolvedMethodDefinition(
            Method,
            IlBody.Create(includeArithmetic ? 2 : 1, il.ToArray()),
            new MethodSignatureShape(
                DeclaringType,
                MethodCallingConventionKind.Default,
                hasImplicitThis: true,
                hasExplicitThis: false,
                genericParameterCount: 0,
                ImmutableArray<TypeSig>.Empty,
                TypeSig.Int32,
                ImmutableArray<TypeSig>.Empty));
    }

    private static FieldBoundary AdvanceToField(
        IlMachine<ProvenanceConcreteValue, TestMemory> machine,
        ProvenanceConcreteValue receiver,
        long instructionBudget)
    {
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(receiver),
            TestMemory.Instance);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var state = activation.State!;
        var operations = new MachineOperationalState(new BudgetState(instructionBudget));
        var outcome = machine.StepOne(state, operations);
        Assert.Equal(MachineRunStatus.Ready, outcome.Status);
        Assert.Single(outcome.Events);
        state = outcome.State;
        operations = outcome.OperationalState;

        Assert.Equal(1, Assert.Single(state.CallStack).IlOffset);
        return new FieldBoundary(state, operations);
    }

    private static RunResult Run(
        IlMachine<ProvenanceConcreteValue, TestMemory> machine,
        MachineState<ProvenanceConcreteValue, TestMemory> initial,
        long instructionBudget)
    {
        var state = initial;
        var operations = new MachineOperationalState(new BudgetState(instructionBudget));
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        for (var step = 0; step < 64; step++)
        {
            var outcome = machine.StepOne(state, operations);
            events.AddRange(outcome.Events);
            if (outcome.Status != MachineRunStatus.Ready)
            {
                return new RunResult(outcome, events.ToImmutable());
            }

            state = outcome.State;
            operations = outcome.OperationalState;
        }

        throw new InvalidOperationException("The field-transfer fixture exceeded its deterministic step bound.");
    }

    private static void AssertAtomicStop(
        StepOutcome<ProvenanceConcreteValue, TestMemory> outcome,
        FieldBoundary boundary,
        MachineRunStatus expectedStatus,
        bool expectsFailure = true)
    {
        Assert.Equal(expectedStatus, outcome.Status);
        Assert.Same(boundary.State, outcome.State);
        Assert.Same(boundary.Operations, outcome.OperationalState);
        Assert.Same(TestMemory.Instance, outcome.State.Memory);
        Assert.Empty(outcome.Events);
        if (expectsFailure)
        {
            Assert.NotNull(outcome.Failure);
        }
        else
        {
            Assert.Null(outcome.Failure);
        }
        Assert.Null(outcome.TargetException);
    }

    private static (DebugEventKind Kind, int IlOffset, string Instruction, string? EvidenceSha256)[]
        EventFingerprint(IEnumerable<DebugEvent> events) =>
        events.Select(static item =>
                (item.Kind, item.IlOffset, item.Instruction, item.FieldEvidence?.Sha256))
            .ToArray();

    private static TestMemoryModel EvidenceMemory(FieldLoadEvidence evidence) =>
        new((_, _) => MemoryLoadResult<ProvenanceConcreteValue>.FromFieldEvidence(evidence));

    private static FieldLoadEvidence CreateEvidence(
        EvaluationEvidenceStatus status,
        int observedLength,
        ResolvedField? field = null)
    {
        var bytes = Enumerable.Range(1, observedLength).Select(static value => (byte)value).ToArray();
        return new FieldLoadEvidence(
            dependencyOrdinal: 0,
            field ?? Int32Field,
            status,
            status == EvaluationEvidenceStatus.Partial ? "W4.Field.Partial" : "W4.Field.Unavailable",
            SourceSha256,
            ImportedObjectSha256,
            address: 0x0000_0001_2345_6780UL,
            requestedLength: sizeof(int),
            bytes);
    }

    private static ResolvedField CreateField(int metadataToken = 0x04000001) =>
        new(
            new FieldHandle(Module, metadataToken),
            DeclaringType,
            TypeSig.Int32,
            isStatic: false,
            isLiteral: false,
            hasRva: false);

    private sealed record TestMemory : IPersistentMemoryState<TestMemory>
    {
        internal static TestMemory Instance { get; } = new();

        public TestMemory Fork() => this;
    }

    private sealed class TestMemoryModel(
        Func<ProvenanceConcreteValue, ResolvedField, MemoryLoadResult<ProvenanceConcreteValue>> load) :
        IMemoryModel<ProvenanceConcreteValue, TestMemory>
    {
        internal int LoadCount { get; private set; }

        public bool CanAllocate => false;

        public (ProvenanceConcreteValue objRef, TestMemory mem) NewObject(TestMemory mem, TypeSig type) =>
            throw new NotSupportedException();

        public (ProvenanceConcreteValue arrRef, TestMemory mem) NewArray(
            TestMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length) => throw new NotSupportedException();

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            TestMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field)
        {
            Assert.Same(TestMemory.Instance, mem);
            Assert.Equal(Int32Field, field);
            LoadCount++;
            return load(objRef, field);
        }

        public TestMemory StoreField(
            TestMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value) => throw new NotSupportedException();

        public ProvenanceConcreteValue LoadElement(
            TestMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index) => throw new NotSupportedException();

        public TestMemory StoreElement(
            TestMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value) => throw new NotSupportedException();
    }

    private sealed class FixedResolver(
        ResolvedMethodDefinition definition,
        ResolvedField field) : IResolutionServices
    {
        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
            ResolutionResult<ResolvedMethodDefinition>.Success(definition);

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken) =>
            ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Unsupported,
                "TEST_DIRECT_CALL_NOT_CONFIGURED",
                "This test resolver does not configure direct method calls.");

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
            ResolutionResult<ResolvedField>.Success(field);
    }

    private abstract class DelegatingPrecisionDomain : IValuePrecisionDomain<ProvenanceConcreteValue>
    {
        internal ProvenanceConcreteDomain Inner { get; } = new();

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

        public virtual TypeSig GetStaticType(ProvenanceConcreteValue value) => Inner.GetStaticType(value);

        public StackKind GetStackKind(ProvenanceConcreteValue value) => Inner.GetStackKind(value);

        public bool TryGetConstInt32(ProvenanceConcreteValue value, out int c) => Inner.TryGetConstInt32(value, out c);

        public ProvenanceConcreteValue ApplyBinary(
            BinaryOp op,
            ProvenanceConcreteValue a,
            ProvenanceConcreteValue b) => Inner.ApplyBinary(op, a, b);

        public virtual ValuePrecisionKind GetPrecision(ProvenanceConcreteValue value) => Inner.GetPrecision(value);
    }

    private sealed class PrecisionOnlyDomain : DelegatingPrecisionDomain;

    private sealed class FieldDomainDouble :
        DelegatingPrecisionDomain,
        IFieldLoadApproximationDomain<ProvenanceConcreteValue>
    {
        private readonly FieldCapabilityBehavior behavior;
        private readonly ProvenanceConcreteValue? cannedValue;

        internal FieldDomainDouble(FieldCapabilityBehavior behavior)
        {
            this.behavior = behavior;
            cannedValue = behavior switch
            {
                FieldCapabilityBehavior.WrongType => Inner.CreateInputUnknown(new ProvenanceInputOrigin(
                    ProvenanceInputKind.RequestArgument,
                    0,
                    EvaluationEvidenceStatus.Unavailable,
                    ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes("wrong-field-type")),
                    "W4.Test.WrongType",
                    TypeSig.Int64)),
                FieldCapabilityBehavior.ForeignRoot => new ProvenanceConcreteDomain().CreateInputUnknown(
                    new ProvenanceInputOrigin(
                        ProvenanceInputKind.RequestArgument,
                        0,
                        EvaluationEvidenceStatus.Unavailable,
                        ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes("foreign-field-root")),
                        "W4.Test.ForeignRoot",
                        TypeSig.Int32)),
                FieldCapabilityBehavior.UndefinedPrecision or
                FieldCapabilityBehavior.ThrowingPrecision or
                FieldCapabilityBehavior.ThrowingStaticType => Inner.CreateInputUnknown(new ProvenanceInputOrigin(
                    ProvenanceInputKind.RequestArgument,
                    0,
                    EvaluationEvidenceStatus.Unavailable,
                    ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes("malicious-field-validation")),
                    "W4.Test.Validation",
                    TypeSig.Int32)),
                _ => null,
            };
        }

        internal int CapabilityCallCount { get; private set; }

        internal ProvenanceConcreteValue? LastReceiver { get; private set; }

        internal FieldLoadEvidence? LastEvidence { get; private set; }

        public override TypeSig GetStaticType(ProvenanceConcreteValue value)
        {
            if (CapabilityCallCount != 0 && behavior == FieldCapabilityBehavior.ThrowingStaticType)
            {
                throw new InvalidOperationException("Deliberate static-type classification failure.");
            }

            return base.GetStaticType(value);
        }

        public override ValuePrecisionKind GetPrecision(ProvenanceConcreteValue value)
        {
            if (CapabilityCallCount != 0)
            {
                if (behavior == FieldCapabilityBehavior.ThrowingPrecision)
                {
                    throw new InvalidOperationException("Deliberate precision classification failure.");
                }

                if (behavior == FieldCapabilityBehavior.UndefinedPrecision)
                {
                    return (ValuePrecisionKind)int.MaxValue;
                }
            }

            return base.GetPrecision(value);
        }

        public ProvenanceConcreteValue CreateFieldLoadUnknown(
            ProvenanceConcreteValue receiver,
            FieldLoadEvidence evidence)
        {
            CapabilityCallCount++;
            LastReceiver = receiver;
            LastEvidence = evidence;
            return behavior switch
            {
                FieldCapabilityBehavior.Normal =>
                    ((IFieldLoadApproximationDomain<ProvenanceConcreteValue>)Inner)
                    .CreateFieldLoadUnknown(receiver, evidence),
                FieldCapabilityBehavior.Exact => Inner.ConstInt32(123),
                FieldCapabilityBehavior.Bottom => Inner.Bottom(TypeSig.Int32),
                FieldCapabilityBehavior.BareTop => Inner.Top(TypeSig.Int32),
                FieldCapabilityBehavior.WrongType or
                FieldCapabilityBehavior.ForeignRoot or
                FieldCapabilityBehavior.UndefinedPrecision or
                FieldCapabilityBehavior.ThrowingPrecision or
                FieldCapabilityBehavior.ThrowingStaticType => cannedValue!,
                FieldCapabilityBehavior.Null => null!,
                FieldCapabilityBehavior.Throw => throw new InvalidOperationException("Deliberate capability failure."),
                _ => throw new ArgumentOutOfRangeException(nameof(behavior)),
            };
        }
    }

    /// <summary>Enumerates adversarial approximation-capability behaviors used by the public theory.</summary>
    public enum FieldCapabilityBehavior
    {
        /// <summary>Delegates to the production provenance domain.</summary>
        Normal,

        /// <summary>Returns an exact value where an explained unknown is required.</summary>
        Exact,

        /// <summary>Returns lattice bottom.</summary>
        Bottom,

        /// <summary>Returns an ungrounded semantic top.</summary>
        BareTop,

        /// <summary>Returns an explained unknown of the wrong structural type.</summary>
        WrongType,

        /// <summary>Returns an explained unknown owned by another domain.</summary>
        ForeignRoot,

        /// <summary>Returns a null value despite the non-null capability contract.</summary>
        Null,

        /// <summary>Throws a deterministic capability exception.</summary>
        Throw,

        /// <summary>Returns a value whose precision classifier reports an undefined enum member.</summary>
        UndefinedPrecision,

        /// <summary>Returns a value whose precision classifier throws.</summary>
        ThrowingPrecision,

        /// <summary>Returns a value whose structural-type classifier throws.</summary>
        ThrowingStaticType,
    }

    private sealed record FieldBoundary(
        MachineState<ProvenanceConcreteValue, TestMemory> State,
        MachineOperationalState Operations);

    private sealed record RunResult(
        StepOutcome<ProvenanceConcreteValue, TestMemory> Outcome,
        ImmutableArray<DebugEvent> Events);

    private sealed record ApproximateFieldRun(
        ProvenanceConcreteDomain Domain,
        StepOutcome<ProvenanceConcreteValue, TestMemory> Outcome);

    private sealed record ReplayRun(
        LineageNodeId Root,
        ImmutableArray<byte> GraphBytes,
        string GraphSha256,
        long RemainingBudget,
        ImmutableArray<(DebugEventKind Kind, int IlOffset, string Instruction, string? EvidenceSha256)> Events,
        TestMemory Memory);
}
