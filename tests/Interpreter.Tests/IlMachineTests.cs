using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Exercises the E1 arithmetic machine's successful transfers, admission barriers, diagnostics, and determinism.
/// </summary>
public sealed class IlMachineTests
{
    private static readonly MethodHandle Method = new(
        ModuleHandle.FromContentIdentity(
            ModuleContentIdentity.FromMetadata(
                new Guid("00000000-0000-0000-0000-000000000123"),
                "IlMachineTests"u8),
            10,
            20),
        0x06000001);

    /// <summary>Checks argument/local traffic and add/subtract/multiply through an observable root return value.</summary>
    [Fact]
    public void ScenarioDerivedArgumentsLocalsAndArithmeticProduceRootResult()
    {
        var body = IlBody.Create(
            2,
            [
                0x02,       // ldarg.0
                0x18,       // ldc.i4.2
                0x5A,       // mul
                0x0A,       // stloc.0
                0x06,       // ldloc.0
                0x03,       // ldarg.1
                0x58,       // add
                0x17,       // ldc.i4.1
                0x59,       // sub
                0x2A,       // ret
            ],
            localVariablesInitialized: true,
            localSignatureToken: 0x11000001);
        var run = Run(body, [4, 5], localCount: 1, returnsValue: true);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Empty(run.Outcome.State.CallStack);
        Assert.True(run.Outcome.State.ReturnValue.HasValue);
        AssertInt32(12, run.Outcome.State.ReturnValue.Value);
        Assert.Equal(90, run.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Equal(10, run.Events.Count(item => item.Kind == DebugEventKind.InstructionExecuted));
        Assert.Single(run.Events, item => item.Kind == DebugEventKind.FramePopped);
    }

    /// <summary>Checks whole-body rejection of a supported prefix followed by an unsupported instruction.</summary>
    [Fact]
    public void SupportedPrefixAndUnsupportedSuffixAreRejectedBeforeInstructionZero()
    {
        var body = IlBody.Create(1, [0x17, 0x28, 0, 0, 0, 0, 0x2A]);
        var context = CreateContext(body, [], localCount: 0, returnsValue: true);

        var outcome = context.Machine.StepOne(context.State, context.OperationalState);

        AssertNoTransfer(context, outcome, MachineRunStatus.Blocked, "EXEC_UNSUPPORTED_OPCODE");
    }

    /// <summary>Checks structured missing-evidence failure without budget consumption or false events.</summary>
    [Fact]
    public void MissingBodyProducesStructuredDependencyFailureWithoutFalseExecutionEvent()
    {
        var resolver = FixedResolver.Failure(
            ResolutionFailureKind.Unavailable,
            "META_BODY_NOT_CAPTURED",
            "Body evidence was not captured.");
        var context = CreateContext(IlBody.Create(0, [0x2A]), [], 0, false, resolver);

        var outcome = context.Machine.StepOne(context.State, context.OperationalState);

        AssertNoTransfer(context, outcome, MachineRunStatus.Blocked, "META_BODY_NOT_CAPTURED");
        Assert.Equal(ExecutionFailureKind.DependencyResolution, outcome.Failure!.Kind);
        Assert.Equal(ResolutionFailureKind.Unavailable, outcome.Failure.ResolutionFailure!.Kind);
    }

    /// <summary>Checks that preserved exception-region evidence blocks execution before instruction zero.</summary>
    [Fact]
    public void ExceptionRegionsAreRejectedBeforeBudgetOrEvents()
    {
        var body = IlBody.Create(0, [0x2A], exceptionRegionCount: 1);
        var context = CreateContext(body, [], 0, false);

        var outcome = context.Machine.StepOne(context.State, context.OperationalState);

        AssertNoTransfer(context, outcome, MachineRunStatus.Blocked, "EXEC_EH_UNSUPPORTED");
    }

    /// <summary>Checks that a ret-looking operand byte cannot be used as an executable instruction offset.</summary>
    [Fact]
    public void OperandByteThatLooksLikeRetIsNotAnInstructionBoundary()
    {
        var body = IlBody.Create(1, [0x20, 0x2A, 0x00, 0x00, 0x00, 0x2A]);
        var context = CreateContext(body, [], 0, true, initialOffset: 1);

        var outcome = context.Machine.StepOne(context.State, context.OperationalState);

        AssertNoTransfer(context, outcome, MachineRunStatus.InvalidProgram, "EXEC_INVALID_INSTRUCTION_OFFSET");
    }

    /// <summary>Checks current frame stack depth against admission's expected depth at each decoded boundary.</summary>
    [Fact]
    public void SeededStackMustMatchWholeBodyDepthAtCurrentBoundary()
    {
        var body = IlBody.Create(0, [0x00, 0x2A]);
        var domain = new ConcreteDomain();
        var context = CreateContext(
            body,
            [],
            0,
            false,
            initialStack: ImmutableArray.Create(domain.ConstInt32(123)),
            domain: domain);

        var outcome = context.Machine.StepOne(context.State, context.OperationalState);

        AssertNoTransfer(context, outcome, MachineRunStatus.InvalidProgram, "EXEC_INVALID_ENTRY_STACK");
    }

    /// <summary>Checks that multiple frames are rejected until calls and continuation metadata are implemented.</summary>
    [Fact]
    public void NestedFramesFailClosedUntilCallContinuationsExist()
    {
        var body = IlBody.Create(0, [0x2A]);
        var resolver = new FixedResolver(body);
        var context = CreateContext(body, [], 0, false, resolver);
        var secondFrame = context.State.CallStack[0];
        var nestedState = context.State with { CallStack = context.State.CallStack.Add(secondFrame) };

        var outcome = context.Machine.StepOne(nestedState, context.OperationalState);

        Assert.Same(nestedState, outcome.State);
        Assert.Same(context.OperationalState, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.InvalidProgram, outcome.Status);
        Assert.Equal("EXEC_NESTED_FRAME_UNSUPPORTED", outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
        Assert.Equal(0, resolver.CallCount);
    }

    /// <summary>Checks that budget exhaustion executes no instruction and emits no execution event.</summary>
    [Fact]
    public void ExhaustedBudgetDoesNotClaimRetExecuted()
    {
        var body = IlBody.Create(0, [0x2A]);
        var context = CreateContext(body, [], 0, false, instructionBudget: 0);

        var outcome = context.Machine.StepOne(context.State, context.OperationalState);

        Assert.Same(context.State, outcome.State);
        Assert.Same(context.OperationalState, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.BudgetExhausted, outcome.Status);
        Assert.Empty(outcome.Events);
        Assert.Null(outcome.Failure);
    }

    /// <summary>Checks the deterministic byte-size admission cap for untrusted method bodies.</summary>
    [Fact]
    public void AdmissionCapsUntrustedMethodBodySize()
    {
        var bytes = Enumerable.Repeat((byte)0x00, 4096).Append((byte)0x2A).ToArray();
        var body = IlBody.Create(0, bytes);
        var context = CreateContext(body, [], 0, false);

        var outcome = context.Machine.StepOne(context.State, context.OperationalState);

        AssertNoTransfer(context, outcome, MachineRunStatus.Blocked, "EXEC_BODY_TOO_LARGE");
    }

    /// <summary>Checks malformed public record shapes are rejected structurally instead of throwing.</summary>
    [Fact]
    public void DefaultArraysAndNegativeBodyFactsProduceStructuredInvalidProgram()
    {
        var validContext = CreateContext(IlBody.Create(0, [0x2A]), [], 0, false);
        var defaultStackState = new MachineState<ConcreteValue, ConcreteMemory>(
            default,
            ConcreteMemory.Empty,
            OptionalValue<ConcreteValue>.None);
        var defaultStackOutcome = validContext.Machine.StepOne(defaultStackState, validContext.OperationalState);
        Assert.Equal(MachineRunStatus.InvalidProgram, defaultStackOutcome.Status);
        Assert.Equal("EXEC_INVALID_CALL_STACK", defaultStackOutcome.Failure!.Code);
        Assert.Empty(defaultStackOutcome.Events);

        var malformedFrame = validContext.State.CallStack[0] with { Arguments = default };
        var malformedFrameState = validContext.State with
        {
            CallStack = ImmutableArray.Create(malformedFrame),
        };
        var malformedFrameOutcome = validContext.Machine.StepOne(malformedFrameState, validContext.OperationalState);
        Assert.Equal(MachineRunStatus.InvalidProgram, malformedFrameOutcome.Status);
        Assert.Equal("EXEC_INVALID_FRAME_SLOTS", malformedFrameOutcome.Failure!.Code);
        Assert.Empty(malformedFrameOutcome.Events);

        var defaultCodeContext = CreateContext(new IlBody(0, default, false, 0, 0), [], 0, false);
        var defaultCodeOutcome = defaultCodeContext.Machine.StepOne(
            defaultCodeContext.State,
            defaultCodeContext.OperationalState);
        AssertNoTransfer(defaultCodeContext, defaultCodeOutcome, MachineRunStatus.InvalidProgram, "EXEC_INVALID_CODE_BUFFER");

        var negativeEhContext = CreateContext(new IlBody(0, ImmutableArray.Create((byte)0x2A), false, 0, -1), [], 0, false);
        var negativeEhOutcome = negativeEhContext.Machine.StepOne(
            negativeEhContext.State,
            negativeEhContext.OperationalState);
        AssertNoTransfer(negativeEhContext, negativeEhOutcome, MachineRunStatus.InvalidProgram, "EXEC_INVALID_EH_COUNT");
    }

    /// <summary>Checks that malformed in-progress state and default method handles never reach a resolver.</summary>
    [Fact]
    public void StaleTerminalValueAndInvalidMethodHandleFailBeforeResolution()
    {
        var body = IlBody.Create(0, [0x2A]);
        var staleResolver = new FixedResolver(body);
        var staleContext = CreateContext(body, [], 0, false, staleResolver);
        var staleState = staleContext.State with
        {
            ReturnValue = OptionalValue<ConcreteValue>.Some(new ConcreteDomain().ConstInt32(1)),
        };

        var staleOutcome = staleContext.Machine.StepOne(staleState, staleContext.OperationalState);

        Assert.Same(staleState, staleOutcome.State);
        Assert.Same(staleContext.OperationalState, staleOutcome.OperationalState);
        Assert.Equal(MachineRunStatus.InvalidProgram, staleOutcome.Status);
        Assert.Equal("EXEC_STALE_RETURN_VALUE", staleOutcome.Failure!.Code);
        Assert.Empty(staleOutcome.Events);
        Assert.Equal(0, staleResolver.CallCount);

        var invalidResolver = new FixedResolver(body);
        var invalidContext = CreateContext(body, [], 0, false, invalidResolver);
        var invalidState = invalidContext.State with
        {
            CallStack = ImmutableArray.Create(invalidContext.State.CallStack[0] with { Method = default }),
        };

        var invalidOutcome = invalidContext.Machine.StepOne(invalidState, invalidContext.OperationalState);

        Assert.Same(invalidState, invalidOutcome.State);
        Assert.Same(invalidContext.OperationalState, invalidOutcome.OperationalState);
        Assert.Equal(MachineRunStatus.InvalidProgram, invalidOutcome.Status);
        Assert.Equal("EXEC_INVALID_METHOD_HANDLE", invalidOutcome.Failure!.Code);
        Assert.Empty(invalidOutcome.Events);
        Assert.Equal(0, invalidResolver.CallCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MethodHandle(Method.Module, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MethodHandle(Method.Module, 0x0A000001));
    }

    /// <summary>Checks that a resolution success can never smuggle a null reference across the capability seam.</summary>
    [Fact]
    public void ResolutionSuccessRejectsNullValues()
    {
        Assert.Throws<ArgumentNullException>(() => ResolutionResult<IlBody>.Success(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResolutionFailure(
            (ResolutionFailureKind)int.MaxValue,
            "INVALID",
            "invalid"));
        Assert.Throws<ArgumentException>(() => new ResolutionFailure(
            ResolutionFailureKind.Invalid,
            " ",
            "invalid"));

        Assert.Throws<ArgumentNullException>(ConsumeNullBudget);

        static void ConsumeNullBudget()
        {
            BudgetState nullBudget = null!;
            _ = new InstructionBudgetPolicy().TryConsumeInstruction(ref nullBudget);
        }
    }

    /// <summary>Checks that plug-in exception payloads never enter structured execution diagnostics.</summary>
    [Fact]
    public void DomainFailuresDoNotEchoExceptionPayloads()
    {
        const string canary = "secret-domain-exception-canary";
        var shapeContext = CreateContext(
            IlBody.Create(1, [0x02, 0x2A]),
            [1],
            localCount: 0,
            returnsValue: true,
            domain: new ThrowingDomain(canary, throwDuringShapeClassification: true));

        var shapeOutcome = shapeContext.Machine.StepOne(shapeContext.State, shapeContext.OperationalState);

        Assert.Equal(MachineRunStatus.InvalidProgram, shapeOutcome.Status);
        Assert.Equal("EXEC_DOMAIN_SHAPE_FAILURE", shapeOutcome.Failure!.Code);
        Assert.DoesNotContain(canary, shapeOutcome.Failure.Message, StringComparison.Ordinal);

        var transferContext = CreateContext(
            IlBody.Create(2, [0x17, 0x18, 0x58, 0x2A]),
            [],
            localCount: 0,
            returnsValue: true,
            domain: new ThrowingDomain(canary, throwDuringShapeClassification: false));
        var first = transferContext.Machine.StepOne(transferContext.State, transferContext.OperationalState);
        var second = transferContext.Machine.StepOne(first.State, first.OperationalState);
        var transferOutcome = transferContext.Machine.StepOne(second.State, second.OperationalState);

        Assert.Equal(MachineRunStatus.InvalidProgram, transferOutcome.Status);
        Assert.Equal("EXEC_DOMAIN_FAILURE", transferOutcome.Failure!.Code);
        Assert.DoesNotContain(canary, transferOutcome.Failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Checks that string and I8 fixture values cannot enter the exact Int32 execution slice.</summary>
    [Fact]
    public void NonInt32SeedValuesFailClosedBeforeAnyPrefixExecutes()
    {
        var domain = new ConcreteDomain();
        var body = IlBody.Create(1, [0x02, 0x2A]);
        var resolver = new FixedResolver(body);
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            resolver,
            new InstructionBudgetPolicy());
        foreach (var invalidValue in new[] { domain.ConstString("not-an-int32"), domain.ConstInt64(1) })
        {
            var frame = new FrameState<ConcreteValue>(
                Method,
                0,
                ImmutableArray.Create(invalidValue),
                ImmutableArray<ConcreteValue>.Empty,
                ImmutableArray<ConcreteValue>.Empty,
                true);
            var state = MachineState<ConcreteValue, ConcreteMemory>.Create(frame, ConcreteMemory.Empty);
            var operationalState = new MachineOperationalState(new BudgetState(100));

            var outcome = machine.StepOne(state, operationalState);

            Assert.Same(state, outcome.State);
            Assert.Same(operationalState, outcome.OperationalState);
            Assert.Equal(MachineRunStatus.InvalidProgram, outcome.Status);
            Assert.Equal("EXEC_NON_I4_VALUE", outcome.Failure!.Code);
            Assert.Empty(outcome.Events);
        }
    }

    /// <summary>Checks that lattice bottom never enters an executable frame or consumes budget.</summary>
    [Fact]
    public void InfeasibleFrameValuesFailClosedBeforeAnyInstructionExecutes()
    {
        var domain = new ConcreteDomain();
        var bottom = domain.Bottom(ConcreteDomain.Int32Type);

        var argumentContext = CreateContext(IlBody.Create(1, [0x02, 0x2A]), [0], 0, true, domain: domain);
        var argumentState = argumentContext.State with
        {
            CallStack = ImmutableArray.Create(argumentContext.State.CallStack[0] with
            {
                Arguments = ImmutableArray.Create(bottom),
            }),
        };

        var localBody = IlBody.Create(
            1,
            [0x06, 0x2A],
            localVariablesInitialized: true,
            localSignatureToken: 0x11000001);
        var localContext = CreateContext(localBody, [], 1, true, domain: domain);
        var localState = localContext.State with
        {
            CallStack = ImmutableArray.Create(localContext.State.CallStack[0] with
            {
                Locals = ImmutableArray.Create(bottom),
            }),
        };

        var stackContext = CreateContext(
            IlBody.Create(1, [0x16, 0x2A]),
            [],
            0,
            true,
            initialOffset: 1,
            initialStack: ImmutableArray.Create(bottom),
            domain: domain);

        foreach (var (context, state) in new[]
        {
            (argumentContext, argumentState),
            (localContext, localState),
            (stackContext, stackContext.State),
        })
        {
            var outcome = context.Machine.StepOne(state, context.OperationalState);
            Assert.Same(state, outcome.State);
            Assert.Same(context.OperationalState, outcome.OperationalState);
            Assert.Equal(MachineRunStatus.InvalidProgram, outcome.Status);
            Assert.Equal("EXEC_INFEASIBLE_VALUE", outcome.Failure!.Code);
            Assert.Empty(outcome.Events);
        }
    }

    /// <summary>Checks deterministic bounds before the resolver or value domain can scan hostile frame vectors.</summary>
    [Fact]
    public void OversizedFrameVectorsAreRejectedBeforeResolution()
    {
        var body = IlBody.Create(0, [0x2A]);
        var resolver = new FixedResolver(body);
        var context = CreateContext(body, [], 0, false, resolver);
        var domain = new ConcreteDomain();
        var oversizedArguments = Enumerable.Repeat(
            domain.ConstInt32(0),
            IlMachine<ConcreteValue, ConcreteMemory>.MaximumFrameSlotCount + 1).ToImmutableArray();
        var state = context.State with
        {
            CallStack = ImmutableArray.Create(context.State.CallStack[0] with { Arguments = oversizedArguments }),
        };

        var outcome = context.Machine.StepOne(state, context.OperationalState);

        Assert.Same(state, outcome.State);
        Assert.Same(context.OperationalState, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.Blocked, outcome.Status);
        Assert.Equal(ExecutionFailureKind.ResourceLimit, outcome.Failure!.Kind);
        Assert.Equal("EXEC_FRAME_SLOT_LIMIT", outcome.Failure.Code);
        Assert.Empty(outcome.Events);
        Assert.Equal(0, resolver.CallCount);
    }

    /// <summary>Checks every malformed whole-body boundary named by the active testing strategy.</summary>
    [Fact]
    public void MalformedBodiesFailWholeBodyAdmissionWithoutExecutingPrefixes()
    {
        var tooManyInstructions = Enumerable.Repeat((byte)0x00, 1025).Append((byte)0x2A).ToArray();
        var cases = new[]
        {
            (IlBody.Create(1, [0x1F]), Array.Empty<int>(), 0, true, MachineRunStatus.InvalidProgram, "EXEC_TRUNCATED_INSTRUCTION"),
            (IlBody.Create(1, [0x02, 0x2A]), Array.Empty<int>(), 0, true, MachineRunStatus.InvalidProgram, "EXEC_INVALID_SLOT"),
            (IlBody.Create(1, [0x16]), Array.Empty<int>(), 0, true, MachineRunStatus.InvalidProgram, "EXEC_MISSING_RETURN"),
            (IlBody.Create(0, [0x2A, 0x00]), Array.Empty<int>(), 0, false, MachineRunStatus.InvalidProgram, "EXEC_CODE_AFTER_RETURN"),
            (IlBody.Create(0, [0x16, 0x2A]), Array.Empty<int>(), 0, true, MachineRunStatus.InvalidProgram, "EXEC_MAXSTACK_EXCEEDED"),
            (IlBody.Create(0, [0x2A], localVariablesInitialized: true, localSignatureToken: 0x11000001), Array.Empty<int>(), 0, false, MachineRunStatus.Blocked, "EXEC_LOCAL_LAYOUT_UNAVAILABLE"),
            (IlBody.Create(0, [0x2A], localVariablesInitialized: true, localSignatureToken: 0x11000000), Array.Empty<int>(), 1, false, MachineRunStatus.InvalidProgram, "EXEC_INVALID_LOCAL_SIGNATURE"),
            (IlBody.Create(0, [0x2A], localVariablesInitialized: true, localSignatureToken: 0x12000001), Array.Empty<int>(), 1, false, MachineRunStatus.InvalidProgram, "EXEC_INVALID_LOCAL_SIGNATURE"),
            (IlBody.Create(0, tooManyInstructions), Array.Empty<int>(), 0, false, MachineRunStatus.Blocked, "EXEC_TOO_MANY_INSTRUCTIONS"),
        };

        foreach (var (body, arguments, localCount, returnsValue, status, code) in cases)
        {
            var context = CreateContext(body, arguments, localCount, returnsValue);
            var outcome = context.Machine.StepOne(context.State, context.OperationalState);
            AssertNoTransfer(context, outcome, status, code);
        }
    }

    /// <summary>Checks that one machine snapshots its first resolved body and cannot be changed mid-run.</summary>
    [Fact]
    public void ResolverCannotChangeMethodBodyDuringOneMachineSession()
    {
        var stableBody = IlBody.Create(1, [0x16, 0x2A]);
        var resolver = new FlippingResolver(stableBody, IlBody.Create(0, [0x28]));
        var context = CreateContext(stableBody, [], 0, true, resolver);

        var first = context.Machine.StepOne(context.State, context.OperationalState);
        Assert.Equal(MachineRunStatus.Ready, first.Status);
        var second = context.Machine.StepOne(first.State, first.OperationalState);

        Assert.Equal(MachineRunStatus.Completed, second.Status);
        Assert.Null(second.Failure);
        Assert.Equal(1, resolver.CallCount);
        AssertInt32(0, second.State.ReturnValue.Value);
    }

    /// <summary>Checks that a long-lived prototype machine cannot retain bodies for unbounded method identities.</summary>
    [Fact]
    public void MachineSessionRejectsASecondRootMethodWithoutGrowingItsCaches()
    {
        var body = IlBody.Create(1, [0x16, 0x2A]);
        var resolver = new FixedResolver(body);
        var context = CreateContext(body, [], 0, true, resolver);
        var first = context.Machine.StepOne(context.State, context.OperationalState);
        Assert.Equal(MachineRunStatus.Ready, first.Status);
        Assert.Equal(1, resolver.CallCount);

        var otherMethod = new MethodHandle(Method.Module, 0x06000002);
        var otherState = context.State with
        {
            CallStack = ImmutableArray.Create(context.State.CallStack[0] with { Method = otherMethod }),
        };
        var rejected = context.Machine.StepOne(otherState, context.OperationalState);

        Assert.Equal(MachineRunStatus.Blocked, rejected.Status);
        Assert.Equal(ExecutionFailureKind.ResourceLimit, rejected.Failure!.Kind);
        Assert.Equal("EXEC_MACHINE_SESSION_MISMATCH", rejected.Failure.Code);
        Assert.Empty(rejected.Events);
        Assert.Equal(1, resolver.CallCount);
    }

    /// <summary>Checks domain/sequence semantic equality for independently materialized immutable states.</summary>
    [Fact]
    public void SemanticComparerUsesSequenceAndDomainEquivalenceInsteadOfBackingArrayIdentity()
    {
        var domain = new ConcreteDomain();
        var first = CreateContext(IlBody.Create(1, [0x16, 0x2A]), [1], 0, true, domain: domain).State;
        var second = CreateContext(IlBody.Create(1, [0x16, 0x2A]), [1], 0, true, domain: domain).State;
        var comparer = new MachineStateSemanticComparer<ConcreteValue, ConcreteMemory>(domain);

        Assert.NotEqual(first, second);
        Assert.True(comparer.Equals(first, second));
        Assert.Equal(comparer.GetHashCode(first), comparer.GetHashCode(second));
    }

    /// <summary>Checks that uninitialized immutable arrays never alias valid empty semantic state.</summary>
    [Fact]
    public void SemanticComparerDistinguishesUninitializedArraysFromValidEmptyState()
    {
        var domain = new ConcreteDomain();
        var comparer = new MachineStateSemanticComparer<ConcreteValue, ConcreteMemory>(domain);
        var validTerminal = new MachineState<ConcreteValue, ConcreteMemory>(
            ImmutableArray<FrameState<ConcreteValue>>.Empty,
            ConcreteMemory.Empty,
            OptionalValue<ConcreteValue>.None);
        var invalidTerminal = new MachineState<ConcreteValue, ConcreteMemory>(
            default,
            ConcreteMemory.Empty,
            OptionalValue<ConcreteValue>.None);

        Assert.False(comparer.Equals(validTerminal, invalidTerminal));
        Assert.False(comparer.Equals(invalidTerminal, validTerminal));

        var validFrame = CreateContext(IlBody.Create(1, [0x2A]), [], 0, false).State.CallStack[0];
        var invalidFrame = validFrame with { Arguments = default };
        var validState = validTerminal with { CallStack = ImmutableArray.Create(validFrame) };
        var invalidState = validTerminal with { CallStack = ImmutableArray.Create(invalidFrame) };

        Assert.False(comparer.Equals(validState, invalidState));
        var nullFrameState = validTerminal with
        {
            CallStack = ImmutableArray.CreateRange(new FrameState<ConcreteValue>[] { null! }),
        };
        var otherNullFrameState = validTerminal with
        {
            CallStack = ImmutableArray.CreateRange(new FrameState<ConcreteValue>[] { null! }),
        };
        Assert.False(comparer.Equals(nullFrameState, otherNullFrameState));
        var nullValueFrame = validFrame with
        {
            Arguments = ImmutableArray.CreateRange(new ConcreteValue[] { null! }),
        };
        var otherNullValueFrame = validFrame with
        {
            Arguments = ImmutableArray.CreateRange(new ConcreteValue[] { null! }),
        };
        Assert.False(comparer.Equals(
            validTerminal with { CallStack = ImmutableArray.Create(nullValueFrame) },
            validTerminal with { CallStack = ImmutableArray.Create(otherNullValueFrame) }));
        Assert.Throws<ArgumentNullException>(() => OptionalValue<ConcreteValue>.Some(null!));
        Assert.Throws<ArgumentException>(() => new TypeSig(null!));
        Assert.Throws<ArgumentException>(() => new TypeSig("   "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TypeSig(
            new string('T', TypeSig.MaximumDisplayNameLength + 1)));
    }

    /// <summary>Checks byte-identical canonical transcripts for repeated execution with equal inputs.</summary>
    [Fact]
    public void RepeatedRunsHaveEqualSemanticStateBudgetAndEventTranscript()
    {
        var body = IlBody.Create(2, [0x02, 0x03, 0x58, 0x1A, 0x59, 0x2A]);
        var first = Run(body, [20, 5], 0, true);
        var second = Run(body, [20, 5], 0, true);
        var comparer = new MachineStateSemanticComparer<ConcreteValue, ConcreteMemory>(new ConcreteDomain());

        Assert.True(comparer.Equals(first.Outcome.State, second.Outcome.State));
        Assert.Equal(first.Outcome.OperationalState, second.Outcome.OperationalState);
        Assert.Equal(SerializeCanonicalTranscript(first), SerializeCanonicalTranscript(second));
    }

    private static RunResult Run(IlBody body, int[] arguments, int localCount, bool returnsValue)
    {
        var context = CreateContext(body, arguments, localCount, returnsValue);
        var state = context.State;
        var operationalState = context.OperationalState;
        var events = ImmutableArray.CreateBuilder<DebugEvent>();

        for (var step = 0; step < 100; step++)
        {
            var outcome = context.Machine.StepOne(state, operationalState);
            events.AddRange(outcome.Events);
            state = outcome.State;
            operationalState = outcome.OperationalState;
            if (outcome.Status != MachineRunStatus.Ready)
            {
                return new RunResult(outcome, events.ToImmutable());
            }
        }

        throw new InvalidOperationException("Machine did not stop within the test safety bound.");
    }

    private static MachineContext CreateContext(
        IlBody body,
        int[] arguments,
        int localCount,
        bool returnsValue,
        IResolutionServices? resolver = null,
        int initialOffset = 0,
        long instructionBudget = 100,
        ImmutableArray<ConcreteValue> initialStack = default,
        IValueDomain<ConcreteValue>? domain = null)
    {
        domain ??= new ConcreteDomain();
        resolver ??= new FixedResolver(body);
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            resolver,
            new InstructionBudgetPolicy());
        var frame = new FrameState<ConcreteValue>(
            Method,
            initialOffset,
            arguments.Select(domain.ConstInt32).ToImmutableArray(),
            Enumerable.Repeat(domain.ConstInt32(0), localCount).ToImmutableArray(),
            initialStack.IsDefault ? ImmutableArray<ConcreteValue>.Empty : initialStack,
            returnsValue);
        var state = MachineState<ConcreteValue, ConcreteMemory>.Create(frame, ConcreteMemory.Empty);
        var operationalState = new MachineOperationalState(new BudgetState(instructionBudget));
        return new MachineContext(machine, state, operationalState);
    }

    private static void AssertNoTransfer(
        MachineContext context,
        StepOutcome<ConcreteValue, ConcreteMemory> outcome,
        MachineRunStatus expectedStatus,
        string expectedCode)
    {
        Assert.Same(context.State, outcome.State);
        Assert.Same(context.OperationalState, outcome.OperationalState);
        Assert.Equal(expectedStatus, outcome.Status);
        Assert.Equal(expectedCode, outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
    }

    private static void AssertInt32(int expected, ConcreteValue actual)
    {
        var domain = new ConcreteDomain();
        Assert.True(domain.TryGetConstInt32(actual, out var value));
        Assert.Equal(expected, value);
    }

    private static byte[] SerializeCanonicalTranscript(RunResult run)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("status", run.Outcome.Status.ToString());
            writer.WriteStartObject("budget");
            writer.WriteNumber("instructions", run.Outcome.OperationalState.Budget.InstructionBudget);
            writer.WriteEndObject();
            writer.WriteStartObject("return");
            writer.WriteBoolean("hasValue", run.Outcome.State.ReturnValue.HasValue);
            if (run.Outcome.State.ReturnValue.HasValue)
            {
                var value = run.Outcome.State.ReturnValue.Value;
                writer.WriteString("kind", value.Kind.ToString());
                writer.WriteString("type", value.StaticType.DisplayName);
                var domain = new ConcreteDomain();
                if (domain.TryGetConstInt32(value, out var int32))
                {
                    writer.WriteNumber("int32", int32);
                }
            }

            writer.WriteEndObject();
            writer.WriteStartArray("events");
            foreach (var item in run.Events)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", item.Kind.ToString());
                writer.WriteNumber("moduleHigh", item.Method.Module.High);
                writer.WriteNumber("moduleLow", item.Method.Module.Low);
                writer.WriteNumber("methodToken", item.Method.MetadataToken);
                writer.WriteNumber("ilOffset", item.IlOffset);
                writer.WriteString("instruction", item.Instruction);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (run.Outcome.Failure is null)
            {
                writer.WriteNull("failure");
            }
            else
            {
                writer.WriteStartObject("failure");
                writer.WriteString("kind", run.Outcome.Failure.Kind.ToString());
                writer.WriteString("code", run.Outcome.Failure.Code);
                if (run.Outcome.Failure.IlOffset is int failureOffset)
                {
                    writer.WriteNumber("ilOffset", failureOffset);
                }
                else
                {
                    writer.WriteNull("ilOffset");
                }

                if (run.Outcome.Failure.Method is MethodHandle method)
                {
                    writer.WriteNumber("moduleHigh", method.Module.High);
                    writer.WriteNumber("moduleLow", method.Module.Low);
                    writer.WriteNumber("methodToken", method.MetadataToken);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private sealed record MachineContext(
        IlMachine<ConcreteValue, ConcreteMemory> Machine,
        MachineState<ConcreteValue, ConcreteMemory> State,
        MachineOperationalState OperationalState);

    private sealed record RunResult(
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        ImmutableArray<DebugEvent> Events);

    private sealed class ThrowingDomain : IValueDomain<ConcreteValue>
    {
        private readonly ConcreteDomain inner = new();
        private readonly string exceptionMessage;
        private readonly bool throwDuringShapeClassification;

        internal ThrowingDomain(string exceptionMessage, bool throwDuringShapeClassification)
        {
            this.exceptionMessage = exceptionMessage;
            this.throwDuringShapeClassification = throwDuringShapeClassification;
        }

        public ConcreteValue Bottom(TypeSig type) => inner.Bottom(type);

        public bool IsBottom(ConcreteValue value) => inner.IsBottom(value);

        public ConcreteValue Top(TypeSig type) => inner.Top(type);

        public ConcreteValue ConstInt32(int value) => inner.ConstInt32(value);

        public ConcreteValue Join(ConcreteValue a, ConcreteValue b) => inner.Join(a, b);

        public bool IsLessThanOrEqual(ConcreteValue a, ConcreteValue b) => inner.IsLessThanOrEqual(a, b);

        public ConcreteValue Meet(ConcreteValue a, ConcreteValue b) => inner.Meet(a, b);

        public ConcreteValue Widen(ConcreteValue prev, ConcreteValue next) => inner.Widen(prev, next);

        public TypeSig GetStaticType(ConcreteValue value) => inner.GetStaticType(value);

        public StackKind GetStackKind(ConcreteValue value) => throwDuringShapeClassification
            ? throw new InvalidOperationException(exceptionMessage)
            : inner.GetStackKind(value);

        public bool TryGetConstInt32(ConcreteValue value, out int c) => inner.TryGetConstInt32(value, out c);

        public ConcreteValue ApplyBinary(BinaryOp op, ConcreteValue a, ConcreteValue b) =>
            throwDuringShapeClassification
                ? inner.ApplyBinary(op, a, b)
                : throw new InvalidOperationException(exceptionMessage);
    }

    private sealed class FixedResolver : IResolutionServices
    {
        private readonly ResolutionResult<IlBody> result;

        public FixedResolver(IlBody body)
        {
            result = ResolutionResult<IlBody>.Success(body);
        }

        private FixedResolver(ResolutionResult<IlBody> result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ResolutionResult<IlBody> GetMethodBody(MethodHandle method)
        {
            CallCount++;
            return result;
        }

        public static FixedResolver Failure(ResolutionFailureKind kind, string code, string message) =>
            new(ResolutionResult<IlBody>.Failed(kind, code, message));
    }

    private sealed class FlippingResolver : IResolutionServices
    {
        private readonly IlBody first;
        private readonly IlBody later;

        public FlippingResolver(IlBody first, IlBody later)
        {
            this.first = first;
            this.later = later;
        }

        public int CallCount { get; private set; }

        public ResolutionResult<IlBody> GetMethodBody(MethodHandle method)
        {
            CallCount++;
            return ResolutionResult<IlBody>.Success(CallCount == 1 ? first : later);
        }
    }
}
