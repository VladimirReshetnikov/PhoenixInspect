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

    private sealed record DifferentialRun(
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        ImmutableArray<DebugEvent> Events);
}
