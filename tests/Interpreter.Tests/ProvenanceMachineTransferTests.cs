using System.Collections.Immutable;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>Exercises provenance-bearing unknowns through the unchanged W3 instruction handlers.</summary>
public sealed class ProvenanceMachineTransferTests
{
    private static readonly ModuleHandle Module = ModuleHandle.FromContentIdentity(
        ModuleContentIdentity.FromMetadata(
            new Guid("00000000-0000-0000-0000-000000000424"),
            "ProvenanceMachineTransferTests-W4.2"u8),
        42,
        84);

    private static readonly MethodHandle Method = new(Module, 0x06000001);
    private static readonly TypeSig DeclaringType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.ProvenanceExecutionFixture");
    private static readonly ResolvedField Int32Field = new(
        new FieldHandle(Module, 0x04000001),
        DeclaringType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    /// <summary>Checks unknown movement through nop, compact slots, multiply, a local, and root return.</summary>
    [Fact]
    public void ExplainedUnknownFlowsThroughSharedTransportArithmeticAndReturnHandlers()
    {
        var definition = CreateDefinition(
            IlBody.Create(
                2,
                [
                    0x00, // nop
                    0x02, // ldarg.0
                    0x16, // ldc.i4.0
                    0x5A, // mul
                    0x0A, // stloc.0
                    0x06, // ldloc.0
                    0x2A, // ret
                ],
                localVariablesInitialized: true,
                localSignatureToken: 0x11000001),
            parameterCount: 1,
            localCount: 1,
            returnsValue: true);
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "transport", 0);
        Assert.True(unknown.TryGetLineageRoot(out var originRoot));
        var memoryModel = ExactMemory(domain, 0);
        var machine = CreateMachine(domain, definition, memoryModel);

        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(unknown),
            TestMemory.Instance);

        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        Assert.True(domain.TryGetConstInt32(Assert.Single(activation.State!.CallStack).Locals[0], out var initialized));
        Assert.Equal(0, initialized);
        var run = Run(machine, activation.State, instructionBudget: 10);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Equal(3, run.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Same(TestMemory.Instance, run.Outcome.State.Memory);
        Assert.Equal(7, run.Events.Count(static item => item.Kind == DebugEventKind.InstructionExecuted));
        Assert.Single(run.Events, static item => item.Kind == DebugEventKind.FramePopped);
        var result = run.Outcome.State.ReturnValue.Value;
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(result));
        Assert.False(domain.TryGetConstInt32(result, out _));
        Assert.True(result.TryGetLineageRoot(out var resultRoot));
        Assert.NotEqual(originRoot, resultRoot);
        var graph = domain.CaptureLineage(result);
        Assert.Equal(2, graph.Nodes.Length);
        var binary = Assert.IsType<BinaryTransformLineageNode>(graph.Nodes.Single(node => node.Id == resultRoot));
        Assert.Equal(BinaryOp.Mul, binary.Operation);
        Assert.Equal(originRoot, binary.Left.Predecessor);
        Assert.Equal(0, binary.Right.ExactInt32);
        Assert.Equal(0, memoryModel.LoadCount);
    }

    /// <summary>Checks all admitted arithmetic operations and both exact/unknown operand positions.</summary>
    [Theory]
    [InlineData(BinaryOp.Add, 0x58)]
    [InlineData(BinaryOp.Sub, 0x59)]
    [InlineData(BinaryOp.Mul, 0x5A)]
    public void MixedUnknownArithmeticPreservesOperandOrderAndNeverFabricates(
        BinaryOp operation,
        byte opcode)
    {
        var definition = CreateDefinition(
            IlBody.Create(2, [0x02, 0x03, opcode, 0x2A]),
            parameterCount: 2,
            returnsValue: true);

        foreach (var unknownOnLeft in new[] { true, false })
        {
            var domain = new ProvenanceConcreteDomain();
            var unknown = Unknown(domain, unknownOnLeft ? "left" : "right", unknownOnLeft ? 0 : 1);
            Assert.True(unknown.TryGetLineageRoot(out var originRoot));
            var exact = domain.ConstInt32(operation == BinaryOp.Mul ? 0 : 1);
            var arguments = unknownOnLeft
                ? ImmutableArray.Create(unknown, exact)
                : ImmutableArray.Create(exact, unknown);
            var machine = CreateMachine(domain, definition, ExactMemory(domain, 0));
            var activation = machine.ActivateRoot(Method, arguments, TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);

            var run = Run(machine, activation.State!, instructionBudget: 4);

            Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
            Assert.Equal(0, run.Outcome.OperationalState.Budget.InstructionBudget);
            var result = run.Outcome.State.ReturnValue.Value;
            Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(result));
            Assert.False(domain.TryGetConstInt32(result, out _));
            var binary = Assert.IsType<BinaryTransformLineageNode>(
                domain.CaptureLineage(result).Nodes.Single(static node => node.Kind == LineageNodeKind.BinaryTransform));
            Assert.Equal(operation, binary.Operation);
            Assert.Equal(unknownOnLeft ? originRoot : null, binary.Left.Predecessor);
            Assert.Equal(unknownOnLeft ? null : originRoot, binary.Right.Predecessor);
            Assert.Equal(unknownOnLeft ? null : exact.SemanticValue.PayloadForTests(), binary.Left.ExactInt32);
            Assert.Equal(unknownOnLeft ? exact.SemanticValue.PayloadForTests() : null, binary.Right.ExactInt32);
        }
    }

    /// <summary>Checks distinct and repeated unknown operands both remain unknown through the shared binary handler.</summary>
    [Fact]
    public void UnknownUnknownArithmeticRetainsEveryOrderedOriginWithoutAlgebraicFabrication()
    {
        var definition = CreateDefinition(
            IlBody.Create(2, [0x02, 0x03, 0x59, 0x2A]),
            parameterCount: 2,
            returnsValue: true);

        foreach (var repeatSameOrigin in new[] { false, true })
        {
            var domain = new ProvenanceConcreteDomain();
            var left = Unknown(domain, "unknown-left", 0);
            var right = repeatSameOrigin ? left : Unknown(domain, "unknown-right", 1);
            Assert.True(left.TryGetLineageRoot(out var leftRoot));
            Assert.True(right.TryGetLineageRoot(out var rightRoot));
            var machine = CreateMachine(domain, definition, ExactMemory(domain, 0));
            var activation = machine.ActivateRoot(
                Method,
                ImmutableArray.Create(left, right),
                TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);

            var run = Run(machine, activation.State!, instructionBudget: 4);

            Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
            var result = run.Outcome.State.ReturnValue.Value;
            Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(result));
            Assert.False(domain.TryGetConstInt32(result, out _));
            var binary = Assert.IsType<BinaryTransformLineageNode>(
                domain.CaptureLineage(result).Nodes.Single(static node => node.Kind == LineageNodeKind.BinaryTransform));
            Assert.Equal(BinaryOp.Sub, binary.Operation);
            Assert.Equal(leftRoot, binary.Left.Predecessor);
            Assert.Equal(rightRoot, binary.Right.Predecessor);
            Assert.Equal(repeatSameOrigin ? 1 : 2, binary.Dependencies.Length);
        }
    }

    /// <summary>Checks semantic state equality and hashing ignore two different explanation roots.</summary>
    [Fact]
    public void MachineSemanticComparerIgnoresLineageWhileCanonicalExplanationsRemainDistinct()
    {
        var domain = new ProvenanceConcreteDomain();
        var first = Unknown(domain, "semantic-first", 0);
        var second = Unknown(domain, "semantic-second", 0);
        Assert.Equal(first, second);
        Assert.True(first.TryGetLineageRoot(out var firstRoot));
        Assert.True(second.TryGetLineageRoot(out var secondRoot));
        Assert.NotEqual(firstRoot, secondRoot);
        Assert.False(domain.CaptureLineage(first).CanonicalBytes.AsSpan().SequenceEqual(
            domain.CaptureLineage(second).CanonicalBytes.AsSpan()));
        var firstState = MachineState<ProvenanceConcreteValue, TestMemory>.Create(
            new FrameState<ProvenanceConcreteValue>(
                Method,
                0,
                ImmutableArray.Create(first),
                ImmutableArray<ProvenanceConcreteValue>.Empty,
                ImmutableArray<ProvenanceConcreteValue>.Empty),
            TestMemory.Instance);
        var secondState = MachineState<ProvenanceConcreteValue, TestMemory>.Create(
            new FrameState<ProvenanceConcreteValue>(
                Method,
                0,
                ImmutableArray.Create(second),
                ImmutableArray<ProvenanceConcreteValue>.Empty,
                ImmutableArray<ProvenanceConcreteValue>.Empty),
            TestMemory.Instance);
        var comparer = new MachineStateSemanticComparer<ProvenanceConcreteValue, TestMemory>(domain);

        Assert.True(comparer.Equals(firstState, secondState));
        Assert.Equal(comparer.GetHashCode(firstState), comparer.GetHashCode(secondState));
    }

    /// <summary>Checks compact, short, and long argument/local encodings transport an explanation unchanged.</summary>
    [Fact]
    public void EveryRetainedSlotEncodingTransportsTheSameLineageRoot()
    {
        var cases = new[]
        {
            new SlotCase(IlBody.Create(1, [0x02, 0x2A]), ParameterCount: 1, LocalCount: 0, UnknownArgument: 0),
            new SlotCase(IlBody.Create(1, [0x0E, 0x04, 0x2A]), ParameterCount: 5, LocalCount: 0, UnknownArgument: 4),
            new SlotCase(IlBody.Create(1, [0xFE, 0x09, 0x04, 0x00, 0x2A]), ParameterCount: 5, LocalCount: 0, UnknownArgument: 4),
            new SlotCase(
                IlBody.Create(1, [0x02, 0x13, 0x04, 0x11, 0x04, 0x2A], true, 0x11000001),
                ParameterCount: 1,
                LocalCount: 5,
                UnknownArgument: 0),
            new SlotCase(
                IlBody.Create(1, [0x02, 0xFE, 0x0E, 0x00, 0x01, 0xFE, 0x0C, 0x00, 0x01, 0x2A], true, 0x11000001),
                ParameterCount: 1,
                LocalCount: 257,
                UnknownArgument: 0),
        };

        foreach (var item in cases)
        {
            var domain = new ProvenanceConcreteDomain();
            var unknown = Unknown(domain, $"slot-{item.ParameterCount}-{item.LocalCount}", item.UnknownArgument);
            Assert.True(unknown.TryGetLineageRoot(out var expectedRoot));
            var arguments = Enumerable.Range(0, item.ParameterCount)
                .Select(index => index == item.UnknownArgument ? unknown : domain.ConstInt32(index))
                .ToImmutableArray();
            var definition = CreateDefinition(
                item.Body,
                item.ParameterCount,
                item.LocalCount,
                returnsValue: true);
            var machine = CreateMachine(domain, definition, ExactMemory(domain, 0));
            var activation = machine.ActivateRoot(Method, arguments, TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);

            var run = Run(machine, activation.State!, instructionBudget: 20);

            Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
            var result = run.Outcome.State.ReturnValue.Value;
            Assert.True(result.TryGetLineageRoot(out var actualRoot));
            Assert.Equal(expectedRoot, actualRoot);
            Assert.Equal(1, domain.InternedNodeCount);
        }
    }

    /// <summary>Checks exact-only, bottom, bare-top, foreign-root, wrong-type, and receiver activation boundaries.</summary>
    [Fact]
    public void ActivationAdmitsOnlyPolicyEnabledOwnedExplainedInt32Parameters()
    {
        var staticDefinition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x2A]),
            parameterCount: 1,
            returnsValue: true);
        var domain = new ProvenanceConcreteDomain();
        var explained = Unknown(domain, "activation", 0);
        var exactOnly = CreateMachine(
            domain,
            staticDefinition,
            ExactMemory(domain, 0),
            UnknownExecutionPolicy.ExactOnly);
        AssertActivationFailure(
            exactOnly.ActivateRoot(Method, ImmutableArray.Create(explained), TestMemory.Instance),
            "EXEC_NON_EXACT_ARGUMENT");

        var explainedMachine = CreateMachine(domain, staticDefinition, ExactMemory(domain, 0));
        Assert.True(explainedMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(explained),
            TestMemory.Instance).IsSuccess);
        AssertActivationFailure(
            CreateMachine(domain, staticDefinition, ExactMemory(domain, 0)).ActivateRoot(
                Method,
                ImmutableArray.Create(domain.Top(TypeSig.Int32)),
                TestMemory.Instance),
            "EXEC_UNEXPLAINED_UNKNOWN");
        AssertActivationFailure(
            CreateMachine(domain, staticDefinition, ExactMemory(domain, 0)).ActivateRoot(
                Method,
                ImmutableArray.Create(domain.Bottom(TypeSig.Int32)),
                TestMemory.Instance),
            "EXEC_INFEASIBLE_VALUE");

        var foreignDomain = new ProvenanceConcreteDomain();
        var foreign = Unknown(foreignDomain, "foreign", 0);
        AssertActivationFailure(
            CreateMachine(domain, staticDefinition, ExactMemory(domain, 0)).ActivateRoot(
                Method,
                ImmutableArray.Create(foreign),
                TestMemory.Instance),
            "EXEC_DOMAIN_ACTIVATION_FAILURE");

        var wrongType = domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            0,
            EvaluationEvidenceStatus.Unavailable,
            Source("wrong-type"),
            "W4.Input.Unavailable",
            TypeSig.Int64));
        AssertActivationFailure(
            CreateMachine(domain, staticDefinition, ExactMemory(domain, 0)).ActivateRoot(
                Method,
                ImmutableArray.Create(wrongType),
                TestMemory.Instance),
            "EXEC_VALUE_TYPE_MISMATCH");

        var instanceDefinition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x7B, 0x01, 0, 0, 0x04, 0x2A]),
            returnsValue: true,
            hasImplicitThis: true);
        var unknownReceiver = domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.Receiver,
            0,
            EvaluationEvidenceStatus.Unavailable,
            Source("receiver"),
            "W4.Receiver.Unavailable",
            DeclaringType));
        AssertActivationFailure(
            CreateMachine(domain, instanceDefinition, ExactMemory(domain, 1)).ActivateRoot(
                Method,
                ImmutableArray.Create(unknownReceiver),
                TestMemory.Instance),
            "EXEC_NON_EXACT_ARGUMENT");

        Assert.Throws<ArgumentOutOfRangeException>(() => new IlMachine<ProvenanceConcreteValue, TestMemory>(
            domain,
            new FixedResolver(staticDefinition, Int32Field),
            ExactMemory(domain, 0),
            new InstructionBudgetPolicy(),
            (UnknownExecutionPolicy)int.MaxValue));
    }

    /// <summary>Checks forged ungrounded resumed state fails without mutating budget, events, memory, or lineage.</summary>
    [Fact]
    public void ResumedBoundaryRejectsUngroundedUnknownAtomically()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x2A]),
            parameterCount: 1,
            returnsValue: true);
        var domain = new ProvenanceConcreteDomain();
        var machine = CreateMachine(domain, definition, ExactMemory(domain, 0));
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(Unknown(domain, "resume", 0)),
            TestMemory.Instance);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var state = activation.State!;
        var forgedFrame = state.CallStack[0] with
        {
            Arguments = ImmutableArray.Create(domain.Top(TypeSig.Int32)),
        };
        var forged = state with { CallStack = ImmutableArray.Create(forgedFrame) };
        var operations = new MachineOperationalState(new BudgetState(3));
        var nodesBefore = domain.InternedNodeCount;

        var outcome = machine.StepOne(forged, operations);

        Assert.Same(forged, outcome.State);
        Assert.Same(operations, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.InvalidProgram, outcome.Status);
        Assert.Equal("EXEC_UNEXPLAINED_UNKNOWN", outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
        Assert.Equal(nodesBefore, domain.InternedNodeCount);
    }

    /// <summary>Checks exact-only policy is revalidated when a locally explained value is forged into resumed state.</summary>
    [Fact]
    public void ExactOnlyPolicyRejectsExplainedUnknownOnResumeAtomically()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x2A]),
            parameterCount: 1,
            returnsValue: true);
        var domain = new ProvenanceConcreteDomain();
        var machine = CreateMachine(
            domain,
            definition,
            ExactMemory(domain, 0),
            UnknownExecutionPolicy.ExactOnly);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstInt32(7)),
            TestMemory.Instance);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var explained = Unknown(domain, "exact-only-resume", 0);
        var frame = activation.State!.CallStack[0] with
        {
            Arguments = ImmutableArray.Create(explained),
        };
        var forged = activation.State with { CallStack = ImmutableArray.Create(frame) };
        var operations = new MachineOperationalState(new BudgetState(2));
        var nodesBefore = domain.InternedNodeCount;

        var outcome = machine.StepOne(forged, operations);

        Assert.Same(forged, outcome.State);
        Assert.Same(operations, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.InvalidProgram, outcome.Status);
        Assert.Equal("EXEC_NON_EXACT_ARGUMENT", outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
        Assert.Equal(nodesBefore, domain.InternedNodeCount);
    }

    /// <summary>Checks invalid and throwing precision capabilities normalize at activation and resumed boundaries.</summary>
    [Fact]
    public void PrecisionCapabilityFailuresAreNormalizedWithoutTransfer()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x2A]),
            parameterCount: 1,
            returnsValue: true);

        foreach (var behavior in new[] { PrecisionBehavior.Undefined, PrecisionBehavior.Throw })
        {
            var activationDomain = new SwitchablePrecisionDomain { Behavior = behavior };
            var activationMachine = CreateMachine(
                activationDomain,
                definition,
                ExactMemory(activationDomain.Inner, 0));
            AssertActivationFailure(
                activationMachine.ActivateRoot(
                    Method,
                    ImmutableArray.Create(activationDomain.Inner.ConstInt32(1)),
                    TestMemory.Instance),
                behavior == PrecisionBehavior.Undefined
                    ? "EXEC_VALUE_PRECISION_INVALID"
                    : "EXEC_DOMAIN_ACTIVATION_FAILURE");

            var resumedDomain = new SwitchablePrecisionDomain();
            var resumedMachine = CreateMachine(
                resumedDomain,
                definition,
                ExactMemory(resumedDomain.Inner, 0));
            var activation = resumedMachine.ActivateRoot(
                Method,
                ImmutableArray.Create(resumedDomain.Inner.ConstInt32(1)),
                TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);
            resumedDomain.Behavior = behavior;
            var operations = new MachineOperationalState(new BudgetState(2));

            var outcome = resumedMachine.StepOne(activation.State!, operations);

            Assert.Same(activation.State, outcome.State);
            Assert.Same(operations, outcome.OperationalState);
            Assert.Equal(MachineRunStatus.InvalidProgram, outcome.Status);
            Assert.Equal(
                behavior == PrecisionBehavior.Undefined ? "EXEC_VALUE_PRECISION_INVALID" : "EXEC_DOMAIN_FAILURE",
                outcome.Failure!.Code);
            Assert.Empty(outcome.Events);
            Assert.Equal(0, resumedDomain.Inner.InternedNodeCount);
        }
    }

    /// <summary>Checks instruction exhaustion wins over bare, bottom, and foreign value invariants pairwise.</summary>
    [Fact]
    public void InstructionAvailabilityPrecedesResumedValueInvariantChecks()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x2A]),
            parameterCount: 1,
            returnsValue: true);
        var domain = new ProvenanceConcreteDomain();
        var machine = CreateMachine(domain, definition, ExactMemory(domain, 0));
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(Unknown(domain, "precedence-valid", 0)),
            TestMemory.Instance);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var foreignDomain = new ProvenanceConcreteDomain();
        var invalidValues = new[]
        {
            domain.Top(TypeSig.Int32),
            domain.Bottom(TypeSig.Int32),
            Unknown(foreignDomain, "precedence-foreign", 0),
        };

        foreach (var invalid in invalidValues)
        {
            var frame = activation.State!.CallStack[0] with
            {
                Arguments = ImmutableArray.Create(invalid),
            };
            var forged = activation.State with { CallStack = ImmutableArray.Create(frame) };
            var operations = new MachineOperationalState(new BudgetState(0));
            var nodesBefore = domain.InternedNodeCount;

            var exhausted = machine.StepOne(forged, operations);

            Assert.Same(forged, exhausted.State);
            Assert.Same(operations, exhausted.OperationalState);
            Assert.Equal(MachineRunStatus.BudgetExhausted, exhausted.Status);
            Assert.Null(exhausted.Failure);
            Assert.Empty(exhausted.Events);
            Assert.Equal(nodesBefore, domain.InternedNodeCount);
        }

        var wrongShapeFrame = activation.State!.CallStack[0] with
        {
            Arguments = ImmutableArray<ProvenanceConcreteValue>.Empty,
        };
        var wrongShape = activation.State with { CallStack = ImmutableArray.Create(wrongShapeFrame) };
        var wrongShapeOperations = new MachineOperationalState(new BudgetState(0));

        var wrongShapeExhausted = machine.StepOne(wrongShape, wrongShapeOperations);

        Assert.Same(wrongShape, wrongShapeExhausted.State);
        Assert.Same(wrongShapeOperations, wrongShapeExhausted.OperationalState);
        Assert.Equal(MachineRunStatus.BudgetExhausted, wrongShapeExhausted.Status);
        Assert.Empty(wrongShapeExhausted.Events);
    }

    /// <summary>Checks pre-arithmetic budget exhaustion cannot create a transform or claim an event.</summary>
    [Fact]
    public void PreInstructionBudgetExhaustionLeavesUnknownArithmeticUnattempted()
    {
        var definition = CreateDefinition(
            IlBody.Create(2, [0x02, 0x03, 0x58, 0x2A]),
            parameterCount: 2,
            returnsValue: true);
        var domain = new ProvenanceConcreteDomain();
        var machine = CreateMachine(domain, definition, ExactMemory(domain, 0));
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(Unknown(domain, "budget-left", 0), Unknown(domain, "budget-right", 1)),
            TestMemory.Instance);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var first = machine.StepOne(activation.State!, new MachineOperationalState(new BudgetState(2)));
        Assert.Equal(MachineRunStatus.Ready, first.Status);
        var second = machine.StepOne(first.State, first.OperationalState);
        Assert.Equal(MachineRunStatus.Ready, second.Status);
        Assert.Equal(0, second.OperationalState.Budget.InstructionBudget);
        var nodesBefore = domain.InternedNodeCount;

        var exhausted = machine.StepOne(second.State, second.OperationalState);

        Assert.Same(second.State, exhausted.State);
        Assert.Same(second.OperationalState, exhausted.OperationalState);
        Assert.Equal(MachineRunStatus.BudgetExhausted, exhausted.Status);
        Assert.Empty(exhausted.Events);
        Assert.Equal(nodesBefore, domain.InternedNodeCount);
    }

    /// <summary>Checks fresh domains and machines reproduce root IDs, graph bytes, events, budgets, and memory.</summary>
    [Fact]
    public void FreshObjectExecutionReplaysUnknownLineageAndAccountingByteForByte()
    {
        var definition = CreateDefinition(
            IlBody.Create(2, [0x02, 0x17, 0x58, 0x2A]),
            parameterCount: 1,
            returnsValue: true);

        var first = ExecuteFresh(definition);
        var second = ExecuteFresh(definition);

        Assert.Equal(first.Root, second.Root);
        Assert.True(first.Graph.AsSpan().SequenceEqual(second.Graph.AsSpan()));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.RemainingBudget, second.RemainingBudget);
        Assert.True(first.Events.SequenceEqual(second.Events));
        Assert.Equal(first.Memory, second.Memory);

        static ReplayRun ExecuteFresh(ResolvedMethodDefinition definition)
        {
            var domain = new ProvenanceConcreteDomain();
            var machine = CreateMachine(domain, definition, ExactMemory(domain, 0));
            var activation = machine.ActivateRoot(
                Method,
                ImmutableArray.Create(Unknown(domain, "fresh-replay", 0)),
                TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);
            var run = Run(machine, activation.State!, 4);
            Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
            var result = run.Outcome.State.ReturnValue.Value;
            Assert.True(result.TryGetLineageRoot(out var root));
            var graph = domain.CaptureLineage(result);
            return new ReplayRun(
                root,
                graph.CanonicalBytes,
                graph.Sha256,
                run.Outcome.OperationalState.Budget.InstructionBudget,
                run.Events.Select(static item => (item.Kind, item.Method, item.IlOffset, item.Instruction)).ToImmutableArray(),
                run.Outcome.State.Memory);
        }
    }

    /// <summary>Checks exact E2 field execution remains read-only and non-exact observations remain W4.3 stops.</summary>
    [Fact]
    public void ExactE2FieldLoadWorksInSecondDomainWhileNonExactEvidenceStillBlocks()
    {
        var definition = CreateDefinition(
            IlBody.Create(
                2,
                [
                    0x02,
                    0x7B, 0x01, 0, 0, 0x04,
                    0x17,
                    0x58,
                    0x2A,
                ]),
            returnsValue: true,
            hasImplicitThis: true);

        var exactDomain = new ProvenanceConcreteDomain();
        var exactMemory = ExactMemory(exactDomain, 41);
        var exactMachine = CreateMachine(exactDomain, definition, exactMemory);
        var exactActivation = exactMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(exactDomain.ObjectReference(1, DeclaringType)),
            TestMemory.Instance);
        Assert.True(exactActivation.IsSuccess, exactActivation.Failure?.Code);

        var exactRun = Run(exactMachine, exactActivation.State!, 5);

        Assert.Equal(MachineRunStatus.Completed, exactRun.Outcome.Status);
        Assert.True(exactDomain.TryGetConstInt32(exactRun.Outcome.State.ReturnValue.Value, out var exactValue));
        Assert.Equal(42, exactValue);
        Assert.Equal(1, exactMemory.LoadCount);
        Assert.Equal(0, exactDomain.InternedNodeCount);
        Assert.Same(TestMemory.Instance, exactRun.Outcome.State.Memory);

        foreach (var kind in new[] { MemoryLoadKind.Partial, MemoryLoadKind.Unavailable })
        {
            var domain = new ProvenanceConcreteDomain();
            var memory = new TestMemoryModel(
                _ => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                    kind,
                    kind == MemoryLoadKind.Partial ? "MEM_PARTIAL" : "MEM_UNAVAILABLE"));
            var machine = CreateMachine(domain, definition, memory);
            var activation = machine.ActivateRoot(
                Method,
                ImmutableArray.Create(domain.ObjectReference(1, DeclaringType)),
                TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);
            var loadArgument = machine.StepOne(
                activation.State!,
                new MachineOperationalState(new BudgetState(4)));
            Assert.Equal(MachineRunStatus.Ready, loadArgument.Status);
            var beforeLoad = loadArgument.State;
            var operationsBeforeLoad = loadArgument.OperationalState;

            var blocked = machine.StepOne(beforeLoad, operationsBeforeLoad);

            Assert.Same(beforeLoad, blocked.State);
            Assert.Same(operationsBeforeLoad, blocked.OperationalState);
            Assert.Equal(MachineRunStatus.Blocked, blocked.Status);
            Assert.Equal(kind == MemoryLoadKind.Partial ? "MEM_PARTIAL" : "MEM_UNAVAILABLE", blocked.Failure!.Code);
            Assert.Empty(blocked.Events);
            Assert.Equal(1, memory.LoadCount);
            Assert.Equal(0, domain.InternedNodeCount);
        }
    }

    /// <summary>Checks an adapter cannot mislabel an explained or bare unknown as an exact field result.</summary>
    [Fact]
    public void ExactFieldResultClassificationCannotSmuggleApproximateValues()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x7B, 0x01, 0, 0, 0x04, 0x2A]),
            returnsValue: true,
            hasImplicitThis: true);

        foreach (var explained in new[] { false, true })
        {
            var domain = new ProvenanceConcreteDomain();
            var approximate = explained
                ? Unknown(domain, "dishonest-exact-field", 0)
                : domain.Top(TypeSig.Int32);
            var memory = new TestMemoryModel(
                _ => MemoryLoadResult<ProvenanceConcreteValue>.Exact(approximate));
            var machine = CreateMachine(domain, definition, memory);
            var activation = machine.ActivateRoot(
                Method,
                ImmutableArray.Create(domain.ObjectReference(1, DeclaringType)),
                TestMemory.Instance);
            Assert.True(activation.IsSuccess, activation.Failure?.Code);
            var first = machine.StepOne(
                activation.State!,
                new MachineOperationalState(new BudgetState(3)));
            Assert.Equal(MachineRunStatus.Ready, first.Status);
            var nodesBefore = domain.InternedNodeCount;

            var rejected = machine.StepOne(first.State, first.OperationalState);

            Assert.Same(first.State, rejected.State);
            Assert.Same(first.OperationalState, rejected.OperationalState);
            Assert.Equal(MachineRunStatus.InvalidProgram, rejected.Status);
            Assert.Equal(
                explained ? "EXEC_NON_EXACT_ARGUMENT" : "EXEC_UNEXPLAINED_UNKNOWN",
                rejected.Failure!.Code);
            Assert.Empty(rejected.Events);
            Assert.Equal(1, memory.LoadCount);
            Assert.Equal(nodesBefore, domain.InternedNodeCount);
        }
    }

    private static IlMachine<ProvenanceConcreteValue, TestMemory> CreateMachine(
        IValueDomain<ProvenanceConcreteValue> domain,
        ResolvedMethodDefinition definition,
        TestMemoryModel memoryModel,
        UnknownExecutionPolicy policy = UnknownExecutionPolicy.ExplainedInt32) =>
        new(domain, new FixedResolver(definition, Int32Field), memoryModel, new InstructionBudgetPolicy(), policy);

    private static ResolvedMethodDefinition CreateDefinition(
        IlBody body,
        int parameterCount = 0,
        int localCount = 0,
        bool returnsValue = false,
        bool hasImplicitThis = false) =>
        new(
            Method,
            body,
            new MethodSignatureShape(
                DeclaringType,
                MethodCallingConventionKind.Default,
                hasImplicitThis,
                hasExplicitThis: false,
                genericParameterCount: 0,
                Enumerable.Repeat(TypeSig.Int32, parameterCount).ToImmutableArray(),
                returnsValue ? TypeSig.Int32 : TypeSig.Void,
                Enumerable.Repeat(TypeSig.Int32, localCount).ToImmutableArray()));

    private static ProvenanceConcreteValue Unknown(
        ProvenanceConcreteDomain domain,
        string source,
        int index) =>
        domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            index,
            EvaluationEvidenceStatus.Unavailable,
            Source(source),
            "W4.Input.Unavailable",
            TypeSig.Int32));

    private static ProvenanceSourceKey Source(string text) =>
        ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes(text));

    private static TestMemoryModel ExactMemory(ProvenanceConcreteDomain domain, int value) =>
        new(_ => MemoryLoadResult<ProvenanceConcreteValue>.Exact(domain.ConstInt32(value)));

    private static RunResult Run(
        IlMachine<ProvenanceConcreteValue, TestMemory> machine,
        MachineState<ProvenanceConcreteValue, TestMemory> initial,
        long instructionBudget)
    {
        var state = initial;
        var operations = new MachineOperationalState(new BudgetState(instructionBudget));
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        for (var step = 0; step < 1024; step++)
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

        throw new InvalidOperationException("The machine exceeded the deterministic test step bound.");
    }

    private static void AssertActivationFailure(
        MachineActivationResult<ProvenanceConcreteValue, TestMemory> result,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.State);
        Assert.Equal(MachineRunStatus.InvalidProgram, result.Status);
        Assert.Equal(code, result.Failure!.Code);
    }

    private sealed record TestMemory : IPersistentMemoryState<TestMemory>
    {
        internal static TestMemory Instance { get; } = new();

        public TestMemory Fork() => this;
    }

    private sealed class TestMemoryModel(
        Func<ProvenanceConcreteValue, MemoryLoadResult<ProvenanceConcreteValue>> load) :
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
            Assert.Equal(Int32Field.Handle, field.Handle);
            LoadCount++;
            return load(objRef);
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

    private enum PrecisionBehavior
    {
        Normal,
        Undefined,
        Throw,
    }

    private sealed class SwitchablePrecisionDomain : IValuePrecisionDomain<ProvenanceConcreteValue>
    {
        internal ProvenanceConcreteDomain Inner { get; } = new();

        internal PrecisionBehavior Behavior { get; set; }

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
            ProvenanceConcreteValue b) => Inner.ApplyBinary(op, a, b);

        public ValuePrecisionKind GetPrecision(ProvenanceConcreteValue value) => Behavior switch
        {
            PrecisionBehavior.Normal => Inner.GetPrecision(value),
            PrecisionBehavior.Undefined => (ValuePrecisionKind)int.MaxValue,
            PrecisionBehavior.Throw => throw new InvalidOperationException("test-only capability failure"),
            _ => throw new InvalidOperationException("test-only invalid behavior"),
        };
    }

    private sealed record SlotCase(
        IlBody Body,
        int ParameterCount,
        int LocalCount,
        int UnknownArgument);

    private sealed record RunResult(
        StepOutcome<ProvenanceConcreteValue, TestMemory> Outcome,
        ImmutableArray<DebugEvent> Events);

    private sealed record ReplayRun(
        LineageNodeId Root,
        ImmutableArray<byte> Graph,
        string Fingerprint,
        long RemainingBudget,
        ImmutableArray<(DebugEventKind Kind, MethodHandle Method, int IlOffset, string Instruction)> Events,
        TestMemory Memory);
}

internal static class ProvenanceMachineTestExtensions
{
    internal static int PayloadForTests(this ConcreteValue value)
    {
        var domain = new ConcreteDomain();
        if (!domain.TryGetConstInt32(value, out var result))
        {
            throw new InvalidOperationException("The test expected an exact Int32 semantic value.");
        }

        return result;
    }
}
