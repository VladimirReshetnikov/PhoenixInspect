using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Metadata.SRM;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Freezes the compiler-emitted W4 call-closure fixture while documenting the exact boundary of the W3 machine.
/// </summary>
/// <remarks>
/// These tests are deliberately disk-based and fast: they establish stable MethodDef and FieldDef relationships,
/// CoreCLR semantics, and fail-closed W3 behavior before dump-backed W4 execution is introduced.
/// </remarks>
public sealed class W4GateFixtureTests
{
    private const string CallerName = "GetMarkerSummary";
    private const string HelperName = "CombineMarkers";
    private const int ExpectedMarker = 0x13579BDF;
    private const int ExpectedAlternateMarker = 0x13579BDE;
    private const int ExpectedSummary = 0x26AF37BD;
    private const int SecondFieldLoadOffset = 7;
    private const int CallOffset = 12;

    /// <summary>
    /// Verifies that optimized C# emits the complete W4 gate closure with only two field loads, one direct call,
    /// one arithmetic helper, and returns at the exact byte offsets fixed by the design gate.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void SrmEmitsTheExactW4CallClosure()
    {
        var targetAssemblyPath = ResolveTargetAssemblyPath();
        var peGate = ReadPeGateMetadata(targetAssemblyPath);

        using var module = SrmMetadataModule.LoadFromFile(targetAssemblyPath);
        var caller = ResolveMethod(module, CallerName);
        var helper = ResolveMethod(module, HelperName);
        var expectedCallerCode = CreateExpectedCallerCode(
            peGate.MarkerToken,
            peGate.AlternateMarkerToken,
            peGate.HelperToken);

        Assert.Equal(expectedCallerCode, caller.Body.CodeBytes.ToArray());
        Assert.Equal(new byte[] { 0x02, 0x03, 0x58, 0x2A }, helper.Body.CodeBytes.ToArray());
        Assert.Equal(0x28, caller.Body.CodeBytes[CallOffset]);
        Assert.Equal(helper.Method.MetadataToken, ReadToken(caller.Body.CodeBytes, CallOffset + 1));
    }

    /// <summary>
    /// Verifies that every InlineField and InlineMethod operand resolves to the intended same-module definition and
    /// that SRM preserves the exact caller/helper signatures and tiny-body header facts.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void SrmResolvesExactTokensSignaturesAndBodyFacts()
    {
        var targetAssemblyPath = ResolveTargetAssemblyPath();
        var peGate = ReadPeGateMetadata(targetAssemblyPath);

        Assert.True((peGate.CallerImplAttributes & MethodImplAttributes.NoInlining) != 0);
        Assert.True((peGate.HelperImplAttributes & MethodImplAttributes.NoInlining) != 0);

        using var module = SrmMetadataModule.LoadFromFile(targetAssemblyPath);
        var caller = ResolveMethod(module, CallerName);
        var helper = ResolveMethod(module, HelperName);
        var code = caller.Body.CodeBytes;
        var markerToken = ReadToken(code, 2);
        var alternateMarkerToken = ReadToken(code, 8);
        var helperToken = ReadToken(code, CallOffset + 1);

        Assert.Equal(peGate.CallerToken, caller.Method.MetadataToken);
        Assert.Equal(peGate.HelperToken, helper.Method.MetadataToken);
        Assert.Equal(peGate.MarkerToken, markerToken);
        Assert.Equal(peGate.AlternateMarkerToken, alternateMarkerToken);
        Assert.True(FieldHandle.IsValidMetadataToken(markerToken));
        Assert.True(FieldHandle.IsValidMetadataToken(alternateMarkerToken));
        Assert.True(MethodHandle.IsValidMetadataToken(helperToken));

        var marker = module.ResolveField(caller.Method, markerToken);
        var alternateMarker = module.ResolveField(caller.Method, alternateMarkerToken);
        var callTarget = module.GetMethodHandle(helperToken);
        Assert.True(marker.IsSuccess, marker.Failure?.Code);
        Assert.True(alternateMarker.IsSuccess, alternateMarker.Failure?.Code);
        Assert.True(callTarget.IsSuccess, callTarget.Failure?.Code);
        Assert.Equal(helper.Method, callTarget.Value);
        AssertExactInstanceInt32Field(marker.Value, caller.Signature.DeclaringType, markerToken);
        AssertExactInstanceInt32Field(
            alternateMarker.Value,
            caller.Signature.DeclaringType,
            alternateMarkerToken);
        Assert.NotEqual(marker.Value.Handle, alternateMarker.Value.Handle);

        AssertExactSignature(caller.Signature, hasImplicitThis: true, expectedParameterCount: 0);
        AssertExactSignature(helper.Signature, hasImplicitThis: false, expectedParameterCount: 2);
        Assert.Equal(caller.Signature.DeclaringType, helper.Signature.DeclaringType);
        AssertExactTinyBody(
            caller.Body,
            CreateExpectedCallerCode(markerToken, alternateMarkerToken, helperToken));
        AssertExactTinyBody(helper.Body, [0x02, 0x03, 0x58, 0x2A]);
    }

    /// <summary>
    /// Verifies the fixed summary result on CoreCLR and separately checks the static helper with independent inputs,
    /// so the oracle does not accidentally depend only on the constructor's alternate-marker relation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CoreClrReturnsTheExactSummaryForFixedAndIndependentInputs()
    {
        var probeType = LoadProbeType(ResolveTargetAssemblyPath());
        var constructor = probeType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(int), typeof(string)],
            modifiers: null) ??
            throw new InvalidOperationException("Could not find the DumpProbe constructor.");
        var caller = GetMethod(probeType, CallerName, BindingFlags.Instance | BindingFlags.NonPublic);
        var helper = GetMethod(probeType, HelperName, BindingFlags.Static | BindingFlags.NonPublic);
        var probe = constructor.Invoke([ExpectedMarker, "w4-coreclr-gate"]);

        Assert.Equal(ExpectedMarker, GetField(probeType, "Marker").GetValue(probe));
        Assert.Equal(ExpectedAlternateMarker, GetField(probeType, "AlternateMarker").GetValue(probe));
        Assert.Equal(ExpectedSummary, Assert.IsType<int>(caller.Invoke(probe, parameters: null)));

        const int independentMarker = 0x10203040;
        const int independentAlternateMarker = -0x01020304;
        var expectedIndependentSummary = unchecked(independentMarker + independentAlternateMarker);
        Assert.Equal(
            expectedIndependentSummary,
            Assert.IsType<int>(helper.Invoke(null, [independentMarker, independentAlternateMarker])));
    }

    /// <summary>
    /// Verifies that W3 executes the arithmetic helper exactly but rejects the caller before activation at its second
    /// field load, leaving no caller state, no memory observation, and the later call opcode unexecuted.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void W3ExecutesHelperButRejectsCallerBeforeActivation()
    {
        using var module = SrmMetadataModule.LoadFromFile(ResolveTargetAssemblyPath());
        var helper = ResolveMethod(module, HelperName);
        var helperDomain = new ConcreteDomain();
        var helperRun = RunToStop(
            CreateMachine(module, helperDomain),
            helper.Method,
            ImmutableArray.Create(
                helperDomain.ConstInt32(ExpectedMarker),
                helperDomain.ConstInt32(ExpectedAlternateMarker)));

        Assert.Equal(MachineRunStatus.Completed, helperRun.Outcome.Status);
        Assert.Null(helperRun.Outcome.Failure);
        Assert.Null(helperRun.Outcome.TargetException);
        Assert.Empty(helperRun.Outcome.State.CallStack);
        Assert.True(helperRun.Outcome.State.ReturnValue.HasValue);
        Assert.True(
            helperDomain.TryGetConstInt32(helperRun.Outcome.State.ReturnValue.Value, out var helperResult));
        Assert.Equal(ExpectedSummary, helperResult);
        Assert.Equal(96, helperRun.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Equal(
            new[]
            {
                (DebugEventKind.InstructionExecuted, 0, "LoadArgument"),
                (DebugEventKind.InstructionExecuted, 1, "LoadArgument"),
                (DebugEventKind.InstructionExecuted, 2, "Add"),
                (DebugEventKind.InstructionExecuted, 3, "Return"),
                (DebugEventKind.FramePopped, 3, "Return"),
            },
            helperRun.Events.Select(item => (item.Kind, item.IlOffset, item.Instruction)).ToArray());

        var caller = ResolveMethod(module, CallerName);
        Assert.Equal(0x28, caller.Body.CodeBytes[CallOffset]);
        var callerDomain = new ConcreteDomain();
        var innerResolution = new MetadataResolutionServices(module);
        var countingResolution = new CountingResolutionServices(innerResolution);
        var countingMemory = new CountingMemoryModel(new ConcreteMemoryModel(callerDomain));
        var callerMachine = new IlMachine<ConcreteValue, ConcreteMemory>(
            callerDomain,
            countingResolution,
            countingMemory,
            new InstructionBudgetPolicy());
        var activation = callerMachine.ActivateRoot(
            caller.Method,
            ImmutableArray.Create(callerDomain.ConstNull(caller.Signature.DeclaringType)),
            ConcreteMemory.Empty);

        Assert.False(activation.IsSuccess);
        Assert.Null(activation.State);
        Assert.Equal(MachineRunStatus.Blocked, activation.Status);
        var failure = Assert.IsType<ExecutionFailure>(activation.Failure);
        Assert.Equal(ExecutionFailureKind.UnsupportedInstruction, failure.Kind);
        Assert.Equal("EXEC_MULTIPLE_FIELD_LOADS_UNSUPPORTED", failure.Code);
        Assert.Equal(caller.Method, failure.Method);
        Assert.Equal(SecondFieldLoadOffset, failure.IlOffset);
        Assert.Equal(1, countingResolution.GetMethodDefinitionCount);
        Assert.Equal(1, countingResolution.ResolveFieldCount);
        Assert.Equal(0, countingMemory.LoadFieldCount);
    }

    private static string ResolveTargetAssemblyPath() =>
        TestTargetPaths.ResolveAssembly(TestTargetPaths.ResolveExecutable());

    private static PeGateMetadata ReadPeGateMetadata(string targetAssemblyPath)
    {
        using var stream = File.OpenRead(targetAssemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var probeHandle = reader.TypeDefinitions.Single(handle =>
        {
            var definition = reader.GetTypeDefinition(handle);
            return reader.GetString(definition.Name) == "DumpProbe" &&
                reader.GetString(definition.Namespace).Length == 0;
        });
        var probe = reader.GetTypeDefinition(probeHandle);
        var markerToken = 0;
        var alternateMarkerToken = 0;
        foreach (var fieldHandle in probe.GetFields())
        {
            var fieldName = reader.GetString(reader.GetFieldDefinition(fieldHandle).Name);
            if (fieldName == "Marker")
            {
                markerToken = MetadataTokens.GetToken(fieldHandle);
            }
            else if (fieldName == "AlternateMarker")
            {
                alternateMarkerToken = MetadataTokens.GetToken(fieldHandle);
            }
        }

        var callerToken = 0;
        var helperToken = 0;
        var callerImplAttributes = default(MethodImplAttributes);
        var helperImplAttributes = default(MethodImplAttributes);
        foreach (var methodHandle in probe.GetMethods())
        {
            var method = reader.GetMethodDefinition(methodHandle);
            var methodName = reader.GetString(method.Name);
            if (methodName == CallerName)
            {
                callerToken = MetadataTokens.GetToken(methodHandle);
                callerImplAttributes = method.ImplAttributes;
            }
            else if (methodName == HelperName)
            {
                helperToken = MetadataTokens.GetToken(methodHandle);
                helperImplAttributes = method.ImplAttributes;
            }
        }

        Assert.NotEqual(0, markerToken);
        Assert.NotEqual(0, alternateMarkerToken);
        Assert.NotEqual(0, callerToken);
        Assert.NotEqual(0, helperToken);
        return new PeGateMetadata(
            markerToken,
            alternateMarkerToken,
            callerToken,
            helperToken,
            callerImplAttributes,
            helperImplAttributes);
    }

    private static Type LoadProbeType(string targetAssemblyPath) =>
        Assembly.LoadFile(Path.GetFullPath(targetAssemblyPath)).GetType("DumpProbe", throwOnError: true)!;

    private static FieldInfo GetField(Type probeType, string fieldName) =>
        probeType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException($"Could not find DumpProbe.{fieldName}.");

    private static MethodInfo GetMethod(Type probeType, string methodName, BindingFlags flags) =>
        probeType.GetMethod(methodName, flags) ??
        throw new InvalidOperationException($"Could not find DumpProbe.{methodName}.");

    private static ResolvedMethodDefinition ResolveMethod(SrmMetadataModule module, string methodName)
    {
        var token = module.FindMethodDefinition("DumpProbe", methodName);
        Assert.True(token.IsSuccess, token.Failure?.Code);
        var handle = module.GetMethodHandle(token.Value);
        Assert.True(handle.IsSuccess, handle.Failure?.Code);
        var definition = module.GetMethodDefinition(handle.Value);
        Assert.True(definition.IsSuccess, definition.Failure?.Code);
        return definition.Value;
    }

    private static byte[] CreateExpectedCallerCode(int markerToken, int alternateMarkerToken, int helperToken)
    {
        var code = new byte[18];
        code[0] = 0x02;
        code[1] = 0x7B;
        BinaryPrimitives.WriteInt32LittleEndian(code.AsSpan(2, sizeof(int)), markerToken);
        code[6] = 0x02;
        code[7] = 0x7B;
        BinaryPrimitives.WriteInt32LittleEndian(code.AsSpan(8, sizeof(int)), alternateMarkerToken);
        code[CallOffset] = 0x28;
        BinaryPrimitives.WriteInt32LittleEndian(code.AsSpan(CallOffset + 1, sizeof(int)), helperToken);
        code[17] = 0x2A;
        return code;
    }

    private static int ReadToken(ImmutableArray<byte> code, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset, sizeof(int)));

    private static void AssertExactInstanceInt32Field(
        ResolvedField field,
        TypeSig declaringType,
        int metadataToken)
    {
        Assert.Equal(metadataToken, field.Handle.MetadataToken);
        Assert.Equal(declaringType.Module, field.Handle.Module);
        Assert.Equal(declaringType, field.DeclaringType);
        Assert.Equal(TypeSig.Int32, field.FieldType);
        Assert.False(field.IsStatic);
        Assert.False(field.IsLiteral);
        Assert.False(field.HasRva);
    }

    private static void AssertExactSignature(
        MethodSignatureShape signature,
        bool hasImplicitThis,
        int expectedParameterCount)
    {
        Assert.Equal(MethodCallingConventionKind.Default, signature.CallingConvention);
        Assert.Equal(hasImplicitThis, signature.HasImplicitThis);
        Assert.False(signature.HasExplicitThis);
        Assert.Equal(0, signature.GenericParameterCount);
        Assert.Equal(expectedParameterCount, signature.ParameterTypes.Length);
        Assert.All(signature.ParameterTypes, parameter => Assert.Equal(TypeSig.Int32, parameter));
        Assert.Equal(TypeSig.Int32, signature.ReturnType);
        Assert.Empty(signature.LocalTypes);
    }

    private static void AssertExactTinyBody(
        Interpreter.Core.Abstractions.MethodBody body,
        byte[] expectedCode)
    {
        Assert.Equal(8, body.MaxStack);
        Assert.Equal(expectedCode, body.CodeBytes.ToArray());
        Assert.False(body.LocalVariablesInitialized);
        Assert.Equal(0, body.LocalSignatureToken);
        Assert.False(body.HasLocalSignature);
        Assert.Equal(0, body.ExceptionRegionCount);
    }

    private static IlMachine<ConcreteValue, ConcreteMemory> CreateMachine(
        SrmMetadataModule module,
        ConcreteDomain domain) =>
        new(
            domain,
            new MetadataResolutionServices(module),
            new ConcreteMemoryModel(domain),
            new InstructionBudgetPolicy());

    private static (
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        ImmutableArray<DebugEvent> Events) RunToStop(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        MethodHandle method,
        ImmutableArray<ConcreteValue> arguments)
    {
        var activation = machine.ActivateRoot(method, arguments, ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var state = activation.State!;
        var operationalState = new MachineOperationalState(new BudgetState(100));
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

        throw new InvalidOperationException("The W4 gate helper did not stop within 100 deterministic steps.");
    }

    private sealed record PeGateMetadata(
        int MarkerToken,
        int AlternateMarkerToken,
        int CallerToken,
        int HelperToken,
        MethodImplAttributes CallerImplAttributes,
        MethodImplAttributes HelperImplAttributes);

    private sealed class CountingResolutionServices(IResolutionServices inner) : IResolutionServices
    {
        internal int GetMethodDefinitionCount { get; private set; }

        internal int ResolveFieldCount { get; private set; }

        ResolutionResult<ResolvedMethodDefinition> IResolutionServices.GetMethodDefinition(MethodHandle method)
        {
            GetMethodDefinitionCount++;
            return inner.GetMethodDefinition(method);
        }

        ResolutionResult<ResolvedField> IResolutionServices.ResolveField(
            MethodHandle contextMethod,
            int metadataToken)
        {
            ResolveFieldCount++;
            return inner.ResolveField(contextMethod, metadataToken);
        }
    }

    private sealed class CountingMemoryModel(ConcreteMemoryModel inner) :
        IMemoryModel<ConcreteValue, ConcreteMemory>
    {
        internal int LoadFieldCount { get; private set; }

        bool IMemoryModel<ConcreteValue, ConcreteMemory>.CanAllocate => inner.CanAllocate;

        (ConcreteValue objRef, ConcreteMemory mem)
            IMemoryModel<ConcreteValue, ConcreteMemory>.NewObject(ConcreteMemory mem, TypeSig type) =>
            inner.NewObject(mem, type);

        (ConcreteValue arrRef, ConcreteMemory mem)
            IMemoryModel<ConcreteValue, ConcreteMemory>.NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ConcreteValue length) =>
            inner.NewArray(mem, elemType, length);

        MemoryLoadResult<ConcreteValue> IMemoryModel<ConcreteValue, ConcreteMemory>.LoadField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field)
        {
            LoadFieldCount++;
            return inner.LoadField(mem, objRef, field);
        }

        ConcreteMemory IMemoryModel<ConcreteValue, ConcreteMemory>.StoreField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field,
            ConcreteValue value) =>
            inner.StoreField(mem, objRef, field, value);

        ConcreteValue IMemoryModel<ConcreteValue, ConcreteMemory>.LoadElement(
            ConcreteMemory mem,
            ConcreteValue arrRef,
            ConcreteValue index) =>
            inner.LoadElement(mem, arrRef, index);

        ConcreteMemory IMemoryModel<ConcreteValue, ConcreteMemory>.StoreElement(
            ConcreteMemory mem,
            ConcreteValue arrRef,
            ConcreteValue index,
            ConcreteValue value) =>
            inner.StoreElement(mem, arrRef, index, value);
    }
}
