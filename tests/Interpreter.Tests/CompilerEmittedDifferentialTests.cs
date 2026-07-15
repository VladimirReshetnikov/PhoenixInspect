using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Metadata.SRM;
using Interpreter.Product.DumpDebugging;
using Xunit;

namespace Interpreter.Tests;

/// <summary>
/// Differentially executes real optimized C# compiler output projected by SRM through the prototype and CoreCLR.
/// </summary>
public sealed class CompilerEmittedDifferentialTests
{
    /// <summary>Enumerates the admitted compiler-emitted arithmetic corpus, including unchecked overflow.</summary>
    /// <returns>Rows containing a private fixture MethodDef name and its boxed invocation arguments.</returns>
    public static IEnumerable<object[]> SupportedCases()
    {
        yield return [nameof(ReturnZero), Array.Empty<object>()];
        yield return [nameof(ReturnMinusOne), Array.Empty<object>()];
        yield return [nameof(ReturnMinusFive), Array.Empty<object>()];
        yield return [nameof(ReturnEight), Array.Empty<object>()];
        yield return [nameof(ReturnNine), Array.Empty<object>()];
        yield return [nameof(ReturnLarge), Array.Empty<object>()];
        yield return [nameof(Identity), new object[] { 37 }];
        yield return [nameof(Add), new object[] { 17, 25 }];
        yield return [nameof(Add), new object[] { int.MaxValue, 1 }];
        yield return [nameof(Subtract), new object[] { 17, 25 }];
        yield return [nameof(Subtract), new object[] { int.MinValue, 1 }];
        yield return [nameof(Multiply), new object[] { -7, 6 }];
        yield return [nameof(Multiply), new object[] { int.MaxValue, 2 }];
        yield return [nameof(Mixed), new object[] { 8, 5 }];
        yield return [nameof(Fifth), new object[] { 1, 2, 3, 4, 5 }];
        yield return [nameof(VoidNoOp), Array.Empty<object>()];
    }

    /// <summary>Checks that SRM-projected compiler output returns exactly the value produced by CoreCLR.</summary>
    /// <param name="methodName">The private optimized fixture MethodDef to resolve and execute.</param>
    /// <param name="arguments">The boxed Int32 arguments supplied to both execution engines.</param>
    [Theory]
    [MemberData(nameof(SupportedCases))]
    public void SrmProjectedInterpreterMatchesCoreClr(string methodName, object[] arguments)
    {
        var method = GetFixtureMethod(methodName);
        var expected = method.Invoke(null, arguments);
        var run = InterpretFresh(methodName, arguments);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Null(run.Outcome.Failure);
        Assert.Null(run.Outcome.TargetException);
        Assert.Empty(run.Outcome.State.CallStack);
        if (method.ReturnType == typeof(void))
        {
            Assert.Null(expected);
            Assert.False(run.Outcome.State.ReturnValue.HasValue);
        }
        else
        {
            Assert.True(run.Outcome.State.ReturnValue.HasValue);
            var domain = new ConcreteDomain();
            Assert.True(domain.TryGetConstInt32(run.Outcome.State.ReturnValue.Value, out var actual));
            Assert.Equal((int)expected!, actual);
        }
    }

    /// <summary>Checks that a real emitted call opcode rejects the complete body during activation.</summary>
    [Fact]
    public void ActualCompilerEmittedUnsupportedCallFailsClosedBeforeActivation()
    {
        using var module = OpenFixtureModule();
        var method = ResolveFixtureMethod(module, nameof(UnsupportedCall));
        var domain = new ConcreteDomain();
        var machine = CreateMachine(module, domain);

        var activation = machine.ActivateRoot(
            method,
            ImmutableArray.Create(domain.ConstInt32(-5)),
            ConcreteMemory.Empty);

        Assert.False(activation.IsSuccess);
        Assert.Null(activation.State);
        Assert.Equal(MachineRunStatus.Blocked, activation.Status);
        Assert.Equal("EXEC_UNSUPPORTED_OPCODE", activation.Failure!.Code);
    }

    /// <summary>Checks actual direct/adjusted getters and the null-receiver outcome against CoreCLR.</summary>
    /// <param name="methodName">The instance getter MethodDef name.</param>
    /// <param name="adjusted">Whether the getter adds one to the loaded field.</param>
    [Theory]
    [InlineData(nameof(DifferentialGetterFixture.Read), false)]
    [InlineData(nameof(DifferentialGetterFixture.ReadAdjusted), true)]
    public void SrmProjectedGetterAndNullOutcomeMatchCoreClr(string methodName, bool adjusted)
    {
        const int fieldValue = 41;
        var clrReceiver = new DifferentialGetterFixture(fieldValue);
        var expected = adjusted ? clrReceiver.ReadAdjusted() : clrReceiver.Read();
        using var module = OpenFixtureModule();
        var method = ResolveFixtureMethod(module, nameof(DifferentialGetterFixture), methodName);
        var definition = module.GetMethodDefinition(method);
        Assert.True(definition.IsSuccess, definition.Failure?.Code);
        var code = definition.Value.Body.CodeBytes;
        Assert.Equal(adjusted ? 9 : 7, code.Length);
        Assert.Equal(0x02, code[0]);
        Assert.Equal(0x7B, code[1]);
        var fieldToken = BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(2, sizeof(int)));
        Assert.Equal(0x2A, code[^1]);
        var field = module.ResolveField(method, fieldToken);
        Assert.True(field.IsSuccess, field.Failure?.Code);

        var domain = new ConcreteDomain();
        var memoryModel = new ConcreteMemoryModel(domain);
        var allocated = memoryModel.NewObject(
            ConcreteMemory.Empty,
            definition.Value.Signature.DeclaringType);
        var memory = memoryModel.StoreField(
            allocated.mem,
            allocated.objRef,
            field.Value,
            domain.ConstInt32(fieldValue));
        var machine = CreateMachine(module, domain, memoryModel);
        var run = Run(machine, method, ImmutableArray.Create(allocated.objRef), memory);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        AssertInt32(expected, run.Outcome.State.ReturnValue.Value);
        Assert.Equal(memory, run.Outcome.State.Memory);

        if (adjusted)
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                _ = InvokeAdjustedGetter(null!);
            });
        }
        else
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                _ = InvokeDirectGetter(null!);
            });
        }

        var nullRun = Run(
            machine,
            method,
            ImmutableArray.Create(domain.ConstNull(definition.Value.Signature.DeclaringType)),
            ConcreteMemory.Empty);
        Assert.Equal(MachineRunStatus.TargetException, nullRun.Outcome.Status);
        Assert.Equal(TargetExceptionKind.NullReference, nullRun.Outcome.TargetException!.Kind);
        Assert.Equal(nullRun.Outcome.TargetException, nullRun.Outcome.State.TerminalTargetException);
        Assert.Empty(nullRun.Outcome.State.CallStack);
        Assert.Equal(98, nullRun.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Single(
            nullRun.Events,
            item => item.Kind == DebugEventKind.TargetExceptionRaised);
        Assert.DoesNotContain(
            nullRun.Events,
            item => item.Kind == DebugEventKind.InstructionExecuted && item.IlOffset == 1);
    }

    /// <summary>
    /// Checks that both compiler-emitted exact typed-null getter shapes project one canonical product outcome,
    /// remain capability-free when their terminal latch is stepped again, and replay through fresh SRM objects.
    /// </summary>
    /// <param name="methodName">The direct or constant-adjusted instance getter MethodDef name.</param>
    [Theory]
    [InlineData(nameof(DifferentialGetterFixture.Read))]
    [InlineData(nameof(DifferentialGetterFixture.ReadAdjusted))]
    public void CompilerEmittedTypedNullOutcomeProjectsIdempotentlyAndReplaysFresh(string methodName)
    {
        using var firstModule = OpenFixtureModule();
        var first = ProjectCompilerEmittedTypedNull(firstModule, methodName);

        Assert.Equal(1, first.Capabilities.Resolver.MethodDefinitionCallCount);
        Assert.Equal(1, first.Capabilities.Resolver.FieldCallCount);
        Assert.True(first.Capabilities.Domain.CallCount > 0);
        Assert.Equal(1, first.Capabilities.Memory.LoadFieldCallCount);
        var firstTerminalOutcome = first.Execution.Transitions[^1];
        Assert.Equal(MachineRunStatus.TargetException, firstTerminalOutcome.Status);
        Assert.Equal(TargetExceptionKind.NullReference, first.Fragment.TargetException.Kind);
        Assert.Equal(firstTerminalOutcome.TargetException, first.Fragment.TargetException);
        Assert.Equal(2, first.Fragment.UsedInstructionUnits);

        var capabilityCounts = first.Capabilities.GetCounts();
        first.Capabilities.Poison();
        var repeatedOutcome = first.Machine.StepOne(
            firstTerminalOutcome.State,
            firstTerminalOutcome.OperationalState);

        Assert.Equal(MachineRunStatus.TargetException, repeatedOutcome.Status);
        Assert.Null(repeatedOutcome.Failure);
        Assert.Same(firstTerminalOutcome.State, repeatedOutcome.State);
        Assert.Same(firstTerminalOutcome.OperationalState, repeatedOutcome.OperationalState);
        Assert.Empty(repeatedOutcome.Events);
        Assert.Equal(capabilityCounts, first.Capabilities.GetCounts());
        Assert.Equal(1, first.Capabilities.Memory.LoadFieldCallCount);

        var repeatedProjection = CounterfactualTargetOutcomeProjector.Project(
            first.Machine,
            first.Execution.InitialState,
            first.Execution.InitialOperationalState,
            first.Execution.Transitions.Add(repeatedOutcome));
        Assert.True(repeatedProjection.IsSuccess, repeatedProjection.Failure?.Code);
        AssertFragmentEqual(first.Fragment, repeatedProjection.Fragment!);

        using var freshModule = OpenFixtureModule();
        var fresh = ProjectCompilerEmittedTypedNull(freshModule, methodName);

        Assert.Equal(1, fresh.Capabilities.Resolver.MethodDefinitionCallCount);
        Assert.Equal(1, fresh.Capabilities.Resolver.FieldCallCount);
        Assert.Equal(1, fresh.Capabilities.Memory.LoadFieldCallCount);
        Assert.NotSame(firstModule, freshModule);
        Assert.NotSame(first.Machine, fresh.Machine);
        Assert.NotSame(first.Capabilities.Domain, fresh.Capabilities.Domain);
        Assert.NotSame(first.Capabilities.Resolver, fresh.Capabilities.Resolver);
        Assert.NotSame(first.Capabilities.Memory, fresh.Capabilities.Memory);
        AssertFragmentEqual(first.Fragment, fresh.Fragment);
    }

    /// <summary>Checks the optimized compiler emits and execution admits the short fifth-argument encoding.</summary>
    [Fact]
    public void CompilerEmitsShortArgumentEncodingForFifthParameter()
    {
        using var module = OpenFixtureModule();
        var method = ResolveFixtureMethod(module, nameof(Fifth));
        var definition = module.GetMethodDefinition(method);
        Assert.True(definition.IsSuccess, definition.Failure?.Code);
        Assert.Equal(new byte[] { 0x0E, 0x04, 0x2A }, definition.Value.Body.CodeBytes.ToArray());
    }

    /// <summary>
    /// Checks canonical replay both through one frozen machine plan and through freshly reopened metadata sessions.
    /// </summary>
    [Fact]
    public void SameSessionAndFreshSessionReplayAreByteIdentical()
    {
        using var firstModule = OpenFixtureModule();
        var firstMethod = ResolveFixtureMethod(firstModule, nameof(Mixed));
        var firstDomain = new ConcreteDomain();
        var firstMachine = CreateMachine(firstModule, firstDomain);
        var arguments = ImmutableArray.Create(firstDomain.ConstInt32(8), firstDomain.ConstInt32(5));

        var first = Run(firstMachine, firstMethod, arguments);
        var sameSession = Run(firstMachine, firstMethod, arguments);

        using var freshModule = OpenFixtureModule();
        var freshMethod = ResolveFixtureMethod(freshModule, nameof(Mixed));
        var freshDomain = new ConcreteDomain();
        var freshMachine = CreateMachine(freshModule, freshDomain);
        var freshArguments = ImmutableArray.Create(freshDomain.ConstInt32(8), freshDomain.ConstInt32(5));
        var freshSession = Run(freshMachine, freshMethod, freshArguments);

        var canonical = SerializeCanonical(first);
        Assert.Equal(canonical, SerializeCanonical(sameSession));
        Assert.Equal(canonical, SerializeCanonical(freshSession));
    }

    private static DifferentialRun InterpretFresh(string methodName, object[] arguments)
    {
        using var module = OpenFixtureModule();
        var method = ResolveFixtureMethod(module, methodName);
        var domain = new ConcreteDomain();
        var machine = CreateMachine(module, domain);
        var values = arguments.Select(argument => argument is int integer
            ? domain.ConstInt32(integer)
            : throw new InvalidOperationException(
                $"Fixture argument type {argument.GetType().FullName} is outside the differential slice."))
            .ToImmutableArray();
        return Run(machine, method, values);
    }

    private static DifferentialRun Run(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        MethodHandle method,
        ImmutableArray<ConcreteValue> arguments,
        ConcreteMemory? memory = null)
    {
        var activation = machine.ActivateRoot(method, arguments, memory ?? ConcreteMemory.Empty);
        if (!activation.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Fixture activation failed with {activation.Status}/{activation.Failure?.Code}.");
        }

        var state = activation.State!;
        var operations = new MachineOperationalState(new BudgetState(100));
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        for (var step = 0; step < 100; step++)
        {
            var outcome = machine.StepOne(state, operations);
            events.AddRange(outcome.Events);
            if (outcome.Status != MachineRunStatus.Ready)
            {
                return new DifferentialRun(outcome, events.ToImmutable());
            }

            state = outcome.State;
            operations = outcome.OperationalState;
        }

        throw new InvalidOperationException("Fixture method did not stop within the deterministic step bound.");
    }

    private static ProjectedNullRun ProjectCompilerEmittedTypedNull(
        SrmMetadataModule module,
        string methodName)
    {
        var method = ResolveFixtureMethod(module, nameof(DifferentialGetterFixture), methodName);
        var definition = module.GetMethodDefinition(method);
        Assert.True(definition.IsSuccess, definition.Failure?.Code);

        var innerDomain = new ConcreteDomain();
        var capabilities = new PoisonableCapabilities(
            innerDomain,
            new MetadataResolutionServices(module));
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            capabilities.Domain,
            capabilities.Resolver,
            capabilities.Memory,
            new InstructionBudgetPolicy());
        var receiver = innerDomain.ConstNull(definition.Value.Signature.DeclaringType);
        var execution = RunToTargetException(machine, method, receiver);
        var projection = CounterfactualTargetOutcomeProjector.Project(
            machine,
            execution.InitialState,
            execution.InitialOperationalState,
            execution.Transitions);

        Assert.True(projection.IsSuccess, projection.Failure?.Code);
        Assert.NotNull(projection.Fragment);
        return new ProjectedNullRun(machine, capabilities, execution, projection.Fragment!);
    }

    private static CertifiedExecution RunToTargetException(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        MethodHandle method,
        ConcreteValue receiver)
    {
        var activation = machine.ActivateRoot(
            method,
            ImmutableArray.Create(receiver),
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var state = activation.State!;
        var operations = new MachineOperationalState(new BudgetState(InitialInstructionUnits));
        var initialState = state;
        var initialOperations = operations;
        var transitions = ImmutableArray.CreateBuilder<StepOutcome<ConcreteValue, ConcreteMemory>>();
        for (var step = 0; step < InitialInstructionUnits; step++)
        {
            var outcome = machine.StepOne(state, operations);
            transitions.Add(outcome);
            if (outcome.Status == MachineRunStatus.TargetException)
            {
                return new CertifiedExecution(
                    initialState,
                    initialOperations,
                    transitions.ToImmutable());
            }

            Assert.Equal(MachineRunStatus.Ready, outcome.Status);
            Assert.Null(outcome.Failure);
            state = outcome.State;
            operations = outcome.OperationalState;
        }

        throw new InvalidOperationException("Fixture getter did not reach its target exception within the step bound.");
    }

    private static void AssertFragmentEqual(
        CounterfactualTargetOutcomeFragment expected,
        CounterfactualTargetOutcomeFragment actual)
    {
        Assert.Equal(expected, actual);
        Assert.True(expected == actual);
        Assert.Equal(expected.GetHashCode(), actual.GetHashCode());
        Assert.Equal(expected.Sha256, actual.Sha256);
        Assert.Equal(expected.CanonicalBytes.ToArray(), actual.CanonicalBytes.ToArray());
    }

    private static IlMachine<ConcreteValue, ConcreteMemory> CreateMachine(
        SrmMetadataModule module,
        ConcreteDomain domain,
        ConcreteMemoryModel? memoryModel = null) =>
        new(
            domain,
            new MetadataResolutionServices(module),
            memoryModel ?? new ConcreteMemoryModel(domain),
            new InstructionBudgetPolicy());

    private static SrmMetadataModule OpenFixtureModule() =>
        SrmMetadataModule.LoadFromFile(typeof(CompilerEmittedDifferentialTests).Assembly.Location);

    private static MethodHandle ResolveFixtureMethod(SrmMetadataModule module, string methodName)
        => ResolveFixtureMethod(module, nameof(CompilerEmittedDifferentialTests), methodName);

    private static MethodHandle ResolveFixtureMethod(
        SrmMetadataModule module,
        string typeName,
        string methodName)
    {
        var token = module.FindMethodDefinition(typeName, methodName);
        Assert.True(token.IsSuccess, token.Failure?.Code);
        var method = module.GetMethodHandle(token.Value);
        Assert.True(method.IsSuccess, method.Failure?.Code);
        return method.Value;
    }

    private static byte[] SerializeCanonical(DifferentialRun run)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("status", run.Outcome.Status.ToString());
            writer.WriteNumber("budget", run.Outcome.OperationalState.Budget.InstructionBudget);
            writer.WriteStartObject("return");
            writer.WriteBoolean("hasValue", run.Outcome.State.ReturnValue.HasValue);
            if (run.Outcome.State.ReturnValue.HasValue)
            {
                var value = run.Outcome.State.ReturnValue.Value;
                writer.WriteString("kind", value.Kind.ToString());
                writer.WriteString("type", value.StaticType.ToString());
                if (new ConcreteDomain().TryGetConstInt32(value, out var integer))
                {
                    writer.WriteNumber("int32", integer);
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
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static MethodInfo GetFixtureMethod(string name) =>
        typeof(CompilerEmittedDifferentialTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Fixture method {name} was not found.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReturnZero() => 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReturnMinusOne() => -1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReturnMinusFive() => -5;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReturnEight() => 8;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReturnNine() => 9;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReturnLarge() => 100_000;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Identity(int value) => value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Add(int left, int right) => left + right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Subtract(int left, int right) => left - right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Multiply(int left, int right) => left * right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Mixed(int left, int right) => (left * 3) + right - 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Fifth(int first, int second, int third, int fourth, int fifth) => fifth;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void VoidNoOp()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int UnsupportedCall(int value) => Math.Abs(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int InvokeDirectGetter(DifferentialGetterFixture receiver) => receiver.Read();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int InvokeAdjustedGetter(DifferentialGetterFixture receiver) => receiver.ReadAdjusted();

    private static void AssertInt32(int expected, ConcreteValue actual)
    {
        var domain = new ConcreteDomain();
        Assert.True(domain.TryGetConstInt32(actual, out var value));
        Assert.Equal(expected, value);
    }

    private sealed class DifferentialGetterFixture
    {
        private readonly int value;

        internal DifferentialGetterFixture(int value)
        {
            this.value = value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal int Read() => value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal int ReadAdjusted() => value + 1;
    }

    private sealed class PoisonableCapabilities
    {
        internal PoisonableCapabilities(ConcreteDomain domain, IResolutionServices resolver)
        {
            Domain = new PoisonableDomain(domain);
            Resolver = new PoisonableResolver(resolver);
            Memory = new PoisonableMemoryModel(new ConcreteMemoryModel(domain));
        }

        internal PoisonableDomain Domain { get; }

        internal PoisonableResolver Resolver { get; }

        internal PoisonableMemoryModel Memory { get; }

        internal CapabilityCounts GetCounts() => new(
            Domain.CallCount,
            Resolver.MethodDefinitionCallCount,
            Resolver.MethodCallCount,
            Resolver.FieldCallCount,
            Memory.TotalCallCount);

        internal void Poison()
        {
            Domain.IsPoisoned = true;
            Resolver.IsPoisoned = true;
            Memory.IsPoisoned = true;
        }
    }

    private sealed class PoisonableDomain(ConcreteDomain inner) : IValueDomain<ConcreteValue>
    {
        internal int CallCount { get; private set; }

        internal bool IsPoisoned { get; set; }

        public ConcreteValue Bottom(TypeSig type) => Invoke(() => inner.Bottom(type));

        public bool IsBottom(ConcreteValue value) => Invoke(() => inner.IsBottom(value));

        public ConcreteValue Top(TypeSig type) => Invoke(() => inner.Top(type));

        public ConcreteValue DefaultValue(TypeSig type) => Invoke(() => inner.DefaultValue(type));

        public ConcreteValue ConstInt32(int value) => Invoke(() => inner.ConstInt32(value));

        public ConcreteValue Join(ConcreteValue a, ConcreteValue b) => Invoke(() => inner.Join(a, b));

        public bool IsLessThanOrEqual(ConcreteValue a, ConcreteValue b) =>
            Invoke(() => inner.IsLessThanOrEqual(a, b));

        public ConcreteValue Meet(ConcreteValue a, ConcreteValue b) => Invoke(() => inner.Meet(a, b));

        public ConcreteValue Widen(ConcreteValue prev, ConcreteValue next) =>
            Invoke(() => inner.Widen(prev, next));

        public TypeSig GetStaticType(ConcreteValue value) => Invoke(() => inner.GetStaticType(value));

        public StackKind GetStackKind(ConcreteValue value) => Invoke(() => inner.GetStackKind(value));

        public bool TryGetConstInt32(ConcreteValue value, out int c)
        {
            Demand();
            return inner.TryGetConstInt32(value, out c);
        }

        public ConcreteValue ApplyBinary(BinaryOp op, ConcreteValue a, ConcreteValue b) =>
            Invoke(() => inner.ApplyBinary(op, a, b));

        private T Invoke<T>(Func<T> action)
        {
            Demand();
            return action();
        }

        private void Demand()
        {
            Assert.False(IsPoisoned, "A terminal-latch re-step consulted the value domain.");
            CallCount++;
        }
    }

    private sealed class PoisonableResolver(IResolutionServices inner) : IResolutionServices
    {
        internal int MethodDefinitionCallCount { get; private set; }

        internal int MethodCallCount { get; private set; }

        internal int FieldCallCount { get; private set; }

        internal bool IsPoisoned { get; set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            Demand();
            MethodDefinitionCallCount++;
            return inner.GetMethodDefinition(method);
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Demand();
            MethodCallCount++;
            return inner.ResolveMethod(contextMethod, metadataToken);
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
        {
            Demand();
            FieldCallCount++;
            return inner.ResolveField(contextMethod, metadataToken);
        }

        private void Demand() =>
            Assert.False(IsPoisoned, "A terminal-latch re-step consulted metadata resolution.");
    }

    private sealed class PoisonableMemoryModel(ConcreteMemoryModel inner) :
        IMemoryModel<ConcreteValue, ConcreteMemory>
    {
        internal int TotalCallCount { get; private set; }

        internal int LoadFieldCallCount { get; private set; }

        internal bool IsPoisoned { get; set; }

        public bool CanAllocate
        {
            get
            {
                Demand();
                return inner.CanAllocate;
            }
        }

        public (ConcreteValue objRef, ConcreteMemory mem) NewObject(ConcreteMemory mem, TypeSig type)
        {
            Demand();
            return inner.NewObject(mem, type);
        }

        public (ConcreteValue arrRef, ConcreteMemory mem) NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ConcreteValue length)
        {
            Demand();
            return inner.NewArray(mem, elemType, length);
        }

        public MemoryLoadResult<ConcreteValue> LoadField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field)
        {
            Demand();
            LoadFieldCallCount++;
            return inner.LoadField(mem, objRef, field);
        }

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field,
            ConcreteValue value)
        {
            Demand();
            return inner.StoreField(mem, objRef, field, value);
        }

        public ConcreteValue LoadElement(ConcreteMemory mem, ConcreteValue arrRef, ConcreteValue index)
        {
            Demand();
            return inner.LoadElement(mem, arrRef, index);
        }

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ConcreteValue arrRef,
            ConcreteValue index,
            ConcreteValue value)
        {
            Demand();
            return inner.StoreElement(mem, arrRef, index, value);
        }

        private void Demand()
        {
            Assert.False(IsPoisoned, "A terminal-latch re-step consulted the memory model.");
            TotalCallCount++;
        }
    }

    private sealed record DifferentialRun(
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        ImmutableArray<DebugEvent> Events);

    private sealed record ProjectedNullRun(
        IlMachine<ConcreteValue, ConcreteMemory> Machine,
        PoisonableCapabilities Capabilities,
        CertifiedExecution Execution,
        CounterfactualTargetOutcomeFragment Fragment);

    private sealed record CertifiedExecution(
        MachineState<ConcreteValue, ConcreteMemory> InitialState,
        MachineOperationalState InitialOperationalState,
        ImmutableArray<StepOutcome<ConcreteValue, ConcreteMemory>> Transitions);

    private sealed record CapabilityCounts(
        int Domain,
        int MethodDefinitions,
        int Methods,
        int Fields,
        int Memory);

    private const int InitialInstructionUnits = 100;
}
