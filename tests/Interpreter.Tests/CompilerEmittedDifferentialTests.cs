using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Differentially executes real optimized C# compiler output through the prototype and the active CoreCLR.
/// </summary>
public sealed class CompilerEmittedDifferentialTests
{
    private static readonly MethodHandle FixtureHandle = new(
        ModuleHandle.FromContentIdentity(
            ModuleContentIdentity.FromMetadata(
                new Guid("00000000-0000-0000-0000-000000000456"),
                "CompilerEmittedDifferentialTests"u8),
            30,
            40),
        0x06000001);

    /// <summary>Enumerates supported compiler-emitted method/argument scenarios, including unchecked overflow.</summary>
    /// <returns>Rows containing a private fixture method name and its boxed invocation arguments.</returns>
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
        yield return [nameof(VoidNoOp), Array.Empty<object>()];
    }

    /// <summary>Checks that an admitted compiler-emitted method returns exactly the value produced by CoreCLR.</summary>
    /// <param name="methodName">The private optimized fixture method to inspect and execute.</param>
    /// <param name="arguments">The boxed Int32 arguments supplied to both execution engines.</param>
    [Theory]
    [MemberData(nameof(SupportedCases))]
    public void InterpreterMatchesCoreClrForActualCompilerEmittedMethod(string methodName, object[] arguments)
    {
        var method = GetFixtureMethod(methodName);
        var expected = method.Invoke(null, arguments);
        var outcome = Interpret(method, arguments);

        Assert.Equal(MachineRunStatus.Completed, outcome.Status);
        Assert.Null(outcome.Failure);
        Assert.Empty(outcome.State.CallStack);
        if (method.ReturnType == typeof(void))
        {
            Assert.Null(expected);
            Assert.False(outcome.State.ReturnValue.HasValue);
        }
        else
        {
            Assert.True(outcome.State.ReturnValue.HasValue);
            var domain = new ConcreteDomain();
            Assert.True(domain.TryGetConstInt32(outcome.State.ReturnValue.Value, out var actual));
            Assert.Equal((int)expected!, actual);
        }
    }

    /// <summary>Checks that a real emitted call opcode rejects the complete body before its prefix can execute.</summary>
    [Fact]
    public void ActualCompilerEmittedUnsupportedCallFailsClosedBeforeItsSupportedPrefix()
    {
        var method = GetFixtureMethod(nameof(UnsupportedCall));
        var reflectionBody = method.GetMethodBody()!;
        var body = CreateBody(reflectionBody);
        var domain = new ConcreteDomain();
        var resolver = new FixedResolver(body);
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            resolver,
            new InstructionBudgetPolicy());
        var frame = new FrameState<ConcreteValue>(
            FixtureHandle,
            0,
            ImmutableArray.Create(domain.ConstInt32(-5)),
            ImmutableArray<ConcreteValue>.Empty,
            ImmutableArray<ConcreteValue>.Empty,
            true);
        var state = MachineState<ConcreteValue, ConcreteMemory>.Create(frame, ConcreteMemory.Empty);
        var operations = new MachineOperationalState(new BudgetState(100));

        var outcome = machine.StepOne(state, operations);

        Assert.Same(state, outcome.State);
        Assert.Same(operations, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.Blocked, outcome.Status);
        Assert.Equal("EXEC_UNSUPPORTED_OPCODE", outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
    }

    private static StepOutcome<ConcreteValue, ConcreteMemory> Interpret(MethodInfo method, object[] arguments)
    {
        var reflectionBody = method.GetMethodBody()
            ?? throw new InvalidOperationException($"Fixture method {method.Name} has no IL body.");
        var body = CreateBody(reflectionBody);
        var domain = new ConcreteDomain();
        var locals = reflectionBody.LocalVariables.Select(local => local.LocalType == typeof(int)
            ? domain.ConstInt32(0)
            : throw new InvalidOperationException($"Fixture local type {local.LocalType} is outside the differential slice."));
        var frame = new FrameState<ConcreteValue>(
            FixtureHandle,
            0,
            arguments.Select(argument => argument is int integer
                ? domain.ConstInt32(integer)
                : throw new InvalidOperationException($"Fixture argument {argument.GetType()} is outside the differential slice.")).ToImmutableArray(),
            locals.ToImmutableArray(),
            ImmutableArray<ConcreteValue>.Empty,
            method.ReturnType != typeof(void));
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            new FixedResolver(body),
            new InstructionBudgetPolicy());
        var state = MachineState<ConcreteValue, ConcreteMemory>.Create(frame, ConcreteMemory.Empty);
        var operations = new MachineOperationalState(new BudgetState(100));

        for (var step = 0; step < 100; step++)
        {
            var outcome = machine.StepOne(state, operations);
            if (outcome.Status != MachineRunStatus.Ready)
            {
                return outcome;
            }

            state = outcome.State;
            operations = outcome.OperationalState;
        }

        throw new InvalidOperationException($"Fixture method {method.Name} did not stop within the safety bound.");
    }

    private static IlBody CreateBody(System.Reflection.MethodBody body) => IlBody.Create(
        body.MaxStackSize,
        body.GetILAsByteArray() ?? throw new InvalidOperationException("Reflection returned no IL byte array."),
        body.InitLocals,
        body.LocalSignatureMetadataToken,
        body.ExceptionHandlingClauses.Count);

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
    private static void VoidNoOp()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int UnsupportedCall(int value) => Math.Abs(value);

    private sealed class FixedResolver(IlBody body) : IResolutionServices
    {
        public ResolutionResult<IlBody> GetMethodBody(MethodHandle method) => ResolutionResult<IlBody>.Success(body);
    }
}
