using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Exercises metadata-derived activation, typed whole-body admission, concrete transfers, and replay boundaries.
/// </summary>
public sealed class IlMachineTests
{
    private static readonly ModuleHandle Module = ModuleHandle.FromContentIdentity(
        ModuleContentIdentity.FromMetadata(
            new Guid("00000000-0000-0000-0000-000000000123"),
            "IlMachineTests-W3"u8),
        10,
        20);

    private static readonly MethodHandle Method = new(Module, 0x06000001);
    private static readonly TypeSig DeclaringType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.ExecutionFixture");
    private static readonly ResolvedField Int32Field = new(
        new FieldHandle(Module, 0x04000001),
        DeclaringType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    /// <summary>
    /// Checks that activation derives argument count, initialized locals, and return disposition from metadata.
    /// </summary>
    [Fact]
    public void ActivationDerivesFrameShapeAndArithmeticProducesRootResult()
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
        var definition = CreateDefinition(body, parameterCount: 2, localCount: 1, returnsValue: true);
        var domain = new ConcreteDomain();
        var resolver = new FixedResolver(definition);
        var memoryModel = new CountingMemoryModel(domain);
        var machine = CreateMachine(domain, resolver, memoryModel);

        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstInt32(4), domain.ConstInt32(5)),
            ConcreteMemory.Empty);

        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var frame = Assert.Single(activation.State!.CallStack);
        Assert.Equal(2, frame.Arguments.Length);
        Assert.Single(frame.Locals);
        AssertInt32(0, frame.Locals[0]);
        Assert.Empty(frame.EvalStack);
        Assert.False(activation.State.ReturnValue.HasValue);
        Assert.Equal(0, memoryModel.LoadCount);

        var run = Run(machine, activation.State, instructionBudget: 100);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        Assert.Empty(run.Outcome.State.CallStack);
        AssertInt32(12, run.Outcome.State.ReturnValue.Value);
        Assert.Equal(90, run.Outcome.OperationalState.Budget.InstructionBudget);
        Assert.Equal(10, run.Events.Count(item => item.Kind == DebugEventKind.InstructionExecuted));
        Assert.Single(run.Events, item => item.Kind == DebugEventKind.FramePopped);
        Assert.Equal(1, resolver.MethodDefinitionCallCount);
        Assert.Equal(0, resolver.FieldCallCount);
    }

    /// <summary>
    /// Checks the admitted <c>nop</c> transfer as an observable budgeted instruction before a metadata-derived void
    /// return, including its exact successor offset and event truthfulness.
    /// </summary>
    [Fact]
    public void NopExecutesAsOneBudgetedEventBeforeVoidReturn()
    {
        var definition = CreateDefinition(IlBody.Create(0, [0x00, 0x2A]), returnsValue: false);
        var domain = new ConcreteDomain();
        var resolver = new FixedResolver(definition);
        var memoryModel = new CountingMemoryModel(domain);
        var machine = CreateMachine(domain, resolver, memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var initialState = activation.State!;

        var first = machine.StepOne(
            initialState,
            new MachineOperationalState(new BudgetState(2)));

        Assert.Equal(MachineRunStatus.Ready, first.Status);
        Assert.Equal(1, first.OperationalState.Budget.InstructionBudget);
        Assert.Equal(1, Assert.Single(first.State.CallStack).IlOffset);
        Assert.Same(initialState.Memory, first.State.Memory);
        var nopEvent = Assert.Single(first.Events);
        Assert.Equal(
            (DebugEventKind.InstructionExecuted, 0, "Nop"),
            (nopEvent.Kind, nopEvent.IlOffset, nopEvent.Instruction));

        var second = machine.StepOne(first.State, first.OperationalState);

        Assert.Equal(MachineRunStatus.Completed, second.Status);
        Assert.Equal(0, second.OperationalState.Budget.InstructionBudget);
        Assert.Empty(second.State.CallStack);
        Assert.False(second.State.ReturnValue.HasValue);
        Assert.Collection(
            second.Events,
            item => Assert.Equal(DebugEventKind.InstructionExecuted, item.Kind),
            item => Assert.Equal(DebugEventKind.FramePopped, item.Kind));
        Assert.Equal(1, resolver.MethodDefinitionCallCount);
        Assert.Equal(0, resolver.FieldCallCount);
        Assert.Equal(0, memoryModel.LoadCount);
    }

    /// <summary>Checks compact, short, and long argument/local slot encodings against metadata-derived vectors.</summary>
    [Fact]
    public void ShortAndLongSlotEncodingsExecuteProjectedSlots()
    {
        var domain = new ConcreteDomain();
        var arguments = Enumerable.Range(0, 5).Select(domain.ConstInt32).ToImmutableArray();

        var shortArgument = CreateDefinition(
            IlBody.Create(1, [0x0E, 0x04, 0x2A]),
            parameterCount: 5,
            returnsValue: true);
        var shortArgumentMachine = CreateMachine(
            domain,
            new FixedResolver(shortArgument),
            new CountingMemoryModel(domain));
        var shortArgumentActivation = shortArgumentMachine.ActivateRoot(Method, arguments, ConcreteMemory.Empty);
        Assert.True(shortArgumentActivation.IsSuccess, shortArgumentActivation.Failure?.Code);
        AssertInt32(4, Run(shortArgumentMachine, shortArgumentActivation.State!, 10).Outcome.State.ReturnValue.Value);

        var longArgument = CreateDefinition(
            IlBody.Create(1, [0xFE, 0x09, 0x04, 0x00, 0x2A]),
            parameterCount: 5,
            returnsValue: true);
        var longArgumentMachine = CreateMachine(
            domain,
            new FixedResolver(longArgument),
            new CountingMemoryModel(domain));
        var longArgumentActivation = longArgumentMachine.ActivateRoot(Method, arguments, ConcreteMemory.Empty);
        Assert.True(longArgumentActivation.IsSuccess, longArgumentActivation.Failure?.Code);
        AssertInt32(4, Run(longArgumentMachine, longArgumentActivation.State!, 10).Outcome.State.ReturnValue.Value);

        var shortLocals = CreateDefinition(
            IlBody.Create(
                1,
                [0x17, 0x13, 0x04, 0x11, 0x04, 0x2A],
                localVariablesInitialized: true,
                localSignatureToken: 0x11000001),
            localCount: 5,
            returnsValue: true);
        var shortLocalMachine = CreateMachine(
            domain,
            new FixedResolver(shortLocals),
            new CountingMemoryModel(domain));
        var shortLocalActivation = shortLocalMachine.ActivateRoot(
            Method,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        Assert.True(shortLocalActivation.IsSuccess, shortLocalActivation.Failure?.Code);
        AssertInt32(1, Run(shortLocalMachine, shortLocalActivation.State!, 10).Outcome.State.ReturnValue.Value);

        var longLocals = CreateDefinition(
            IlBody.Create(
                1,
                [0x18, 0xFE, 0x0E, 0x00, 0x01, 0xFE, 0x0C, 0x00, 0x01, 0x2A],
                localVariablesInitialized: true,
                localSignatureToken: 0x11000001),
            localCount: 257,
            returnsValue: true);
        var longLocalMachine = CreateMachine(
            domain,
            new FixedResolver(longLocals),
            new CountingMemoryModel(domain));
        var longLocalActivation = longLocalMachine.ActivateRoot(
            Method,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        Assert.True(longLocalActivation.IsSuccess, longLocalActivation.Failure?.Code);
        Assert.Equal(257, Assert.Single(longLocalActivation.State!.CallStack).Locals.Length);
        AssertInt32(2, Run(longLocalMachine, longLocalActivation.State, 10).Outcome.State.ReturnValue.Value);
    }

    /// <summary>Checks that caller values must match the complete metadata-projected argument vector exactly.</summary>
    [Fact]
    public void ActivationRejectsWrongCountNonExactInt32AndWrongStructuralType()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x2A]),
            parameterCount: 1,
            returnsValue: true);
        var domain = new ConcreteDomain();

        var wrongCount = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain))
            .ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);
        AssertActivationFailure(wrongCount, MachineRunStatus.InvalidProgram, "EXEC_ARGUMENT_SHAPE_MISMATCH");

        var nonExact = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain))
            .ActivateRoot(
                Method,
                ImmutableArray.Create(domain.Top(TypeSig.Int32)),
                ConcreteMemory.Empty);
        AssertActivationFailure(nonExact, MachineRunStatus.InvalidProgram, "EXEC_NON_EXACT_ARGUMENT");

        var wrongType = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain))
            .ActivateRoot(
                Method,
                ImmutableArray.Create(domain.DefaultValue(TypeSig.Boolean)),
                ConcreteMemory.Empty);
        AssertActivationFailure(wrongType, MachineRunStatus.InvalidProgram, "EXEC_VALUE_TYPE_MISMATCH");

        Assert.Throws<ArgumentException>(() => new MethodHandle(default, 0x06000001));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MethodHandle(Module, 0x0A000001));
    }

    /// <summary>Checks both direct and adjusted getters through one frozen typed field and one memory-model load.</summary>
    /// <param name="adjust">Whether the getter adds one after loading the field.</param>
    /// <param name="expected">The expected terminal Int32 result.</param>
    /// <param name="instructionCount">The expected number of ordinary executed instructions.</param>
    [Theory]
    [InlineData(false, 41, 3)]
    [InlineData(true, 42, 5)]
    public void ImportedFieldGetterLoadsExactlyOnceWithoutChangingMemory(
        bool adjust,
        int expected,
        int instructionCount)
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust),
            returnsValue: true,
            hasImplicitThis: true);
        var domain = new ConcreteDomain();
        var resolver = new FixedResolver(definition, Int32Field);
        var memoryModel = new CountingMemoryModel(domain);
        var imported = memoryModel.ImportObject(
            ConcreteMemory.Empty,
            DeclaringType,
            new ImportedObjectEvidenceIdentity("snapshot:test;object:root"));
        var memory = memoryModel.ImportField(
            imported.mem,
            imported.objRef,
            Int32Field,
            domain.ConstInt32(41));
        var machine = CreateMachine(domain, resolver, memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(imported.objRef),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var run = Run(machine, activation.State!, instructionBudget: 20);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        AssertInt32(expected, run.Outcome.State.ReturnValue.Value);
        Assert.Same(memory, run.Outcome.State.Memory);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(1, resolver.FieldCallCount);
        Assert.Equal(instructionCount, run.Events.Count(item => item.Kind == DebugEventKind.InstructionExecuted));
    }

    /// <summary>
    /// Checks the remaining admitted one-constant E2 arithmetic adjustments so every allowed operator is exercised
    /// after a real typed memory load rather than only in the static E1 profile.
    /// </summary>
    /// <param name="operatorOpcode">The admitted <c>sub</c> or <c>mul</c> opcode.</param>
    /// <param name="expected">The expected result after applying the operator to field value 41 and constant 2.</param>
    [Theory]
    [InlineData((byte)0x59, 39)]
    [InlineData((byte)0x5A, 82)]
    public void AdjustedGetterExercisesEveryAdmittedArithmeticOperator(byte operatorOpcode, int expected)
    {
        var definition = CreateDefinition(
            IlBody.Create(
                2,
                [
                    0x02,
                    0x7B, 0x01, 0x00, 0x00, 0x04,
                    0x18,
                    operatorOpcode,
                    0x2A,
                ]),
            returnsValue: true,
            hasImplicitThis: true);
        var domain = new ConcreteDomain();
        var resolver = new FixedResolver(definition, Int32Field);
        var memoryModel = new CountingMemoryModel(domain);
        var imported = memoryModel.ImportObject(
            ConcreteMemory.Empty,
            DeclaringType,
            new ImportedObjectEvidenceIdentity("snapshot:test;object:adjusted-operators"));
        var memory = memoryModel.ImportField(
            imported.mem,
            imported.objRef,
            Int32Field,
            domain.ConstInt32(41));
        var machine = CreateMachine(domain, resolver, memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(imported.objRef),
            memory);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);

        var run = Run(machine, activation.State!, instructionBudget: 10);

        Assert.Equal(MachineRunStatus.Completed, run.Outcome.Status);
        AssertInt32(expected, run.Outcome.State.ReturnValue.Value);
        Assert.Same(memory, run.Outcome.State.Memory);
        Assert.Equal(1, memoryModel.LoadCount);
        Assert.Equal(1, resolver.FieldCallCount);
        Assert.Equal(5, run.Events.Count(item => item.Kind == DebugEventKind.InstructionExecuted));
    }

    /// <summary>
    /// Checks the explicit terminal null-reference boundary: one budget unit, no ordinary execution event, no state change.
    /// </summary>
    [Fact]
    public void NullFieldReceiverProducesStructuredTargetException()
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust: false),
            returnsValue: true,
            hasImplicitThis: true);
        var domain = new ConcreteDomain();
        var resolver = new FixedResolver(definition, Int32Field);
        var memoryModel = new CountingMemoryModel(domain);
        var machine = CreateMachine(domain, resolver, memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = new MachineOperationalState(new BudgetState(10));

        var loadReceiver = machine.StepOne(activation.State!, operations);
        Assert.Equal(MachineRunStatus.Ready, loadReceiver.Status);
        var targetException = machine.StepOne(loadReceiver.State, loadReceiver.OperationalState);

        Assert.Empty(targetException.State.CallStack);
        Assert.Same(loadReceiver.State.Memory, targetException.State.Memory);
        Assert.False(targetException.State.ReturnValue.HasValue);
        Assert.Equal(8, targetException.OperationalState.Budget.InstructionBudget);
        Assert.Equal(MachineRunStatus.TargetException, targetException.Status);
        Assert.Null(targetException.Failure);
        Assert.NotNull(targetException.TargetException);
        Assert.Equal(TargetExceptionKind.NullReference, targetException.TargetException!.Kind);
        Assert.Equal(Method, targetException.TargetException.Method);
        Assert.Equal(1, targetException.TargetException.IlOffset);
        Assert.Equal(targetException.TargetException, targetException.State.TerminalTargetException);
        var raised = Assert.Single(targetException.Events);
        Assert.Equal(DebugEventKind.TargetExceptionRaised, raised.Kind);
        Assert.Equal(1, raised.IlOffset);
        Assert.DoesNotContain(
            targetException.Events,
            item => item.Kind == DebugEventKind.InstructionExecuted);
        Assert.Equal(1, memoryModel.LoadCount);

        var repeated = machine.StepOne(targetException.State, targetException.OperationalState);
        Assert.Same(targetException.State, repeated.State);
        Assert.Same(targetException.OperationalState, repeated.OperationalState);
        Assert.Equal(MachineRunStatus.TargetException, repeated.Status);
        Assert.Equal(targetException.TargetException, repeated.TargetException);
        Assert.Empty(repeated.Events);
        Assert.Equal(1, memoryModel.LoadCount);
    }

    /// <summary>Checks that pre-stamped target-exception information cannot name a different execution location.</summary>
    [Fact]
    public void TargetExceptionLocationConflictDoesNotTransferOrConsumeBudget()
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust: false),
            returnsValue: true,
            hasImplicitThis: true);
        var domain = new ConcreteDomain();
        var memoryModel = new CountingMemoryModel(
            domain,
            (_, _, _) => MemoryLoadResult<ConcreteValue>.ForTargetException(
                new TargetExceptionInfo(
                    TargetExceptionKind.NullReference,
                    "TARGET_NULL_REFERENCE",
                    Method,
                    ilOffset: 2)));
        var machine = CreateMachine(domain, new FixedResolver(definition, Int32Field), memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = new MachineOperationalState(new BudgetState(10));
        var loadReceiver = machine.StepOne(activation.State!, operations);

        var outcome = machine.StepOne(loadReceiver.State, loadReceiver.OperationalState);

        AssertNoTransfer(
            loadReceiver.State,
            loadReceiver.OperationalState,
            outcome,
            MachineRunStatus.InvalidProgram,
            "EXEC_TARGET_EXCEPTION_LOCATION_CONFLICT");
        Assert.Equal(1, memoryModel.LoadCount);
    }

    /// <summary>
    /// Checks typed memory evidence inability and invalidity without state, budget, memory, or event mutation.
    /// </summary>
    /// <param name="kind">The memory load classification returned by the injected capability.</param>
    /// <param name="expectedStatus">The machine status corresponding to the classification.</param>
    [Theory]
    [InlineData(MemoryLoadKind.Partial, MachineRunStatus.Blocked)]
    [InlineData(MemoryLoadKind.Unavailable, MachineRunStatus.Blocked)]
    [InlineData(MemoryLoadKind.Conflict, MachineRunStatus.Blocked)]
    [InlineData(MemoryLoadKind.Invalid, MachineRunStatus.InvalidProgram)]
    public void NonExactMemoryResultsDoNotTransfer(MemoryLoadKind kind, MachineRunStatus expectedStatus)
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust: false),
            returnsValue: true,
            hasImplicitThis: true);
        var domain = new ConcreteDomain();
        var inner = new ConcreteMemoryModel(domain);
        var allocated = inner.NewObject(ConcreteMemory.Empty, DeclaringType);
        var memoryModel = new CountingMemoryModel(
            domain,
            (_, _, _) => MemoryLoadResult<ConcreteValue>.NonExact(kind, $"TEST_{kind.ToString().ToUpperInvariant()}"));
        var machine = CreateMachine(domain, new FixedResolver(definition, Int32Field), memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(allocated.objRef),
            allocated.mem);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = new MachineOperationalState(new BudgetState(10));
        var loadReceiver = machine.StepOne(activation.State!, operations);

        var outcome = machine.StepOne(loadReceiver.State, loadReceiver.OperationalState);

        Assert.Same(loadReceiver.State, outcome.State);
        Assert.Same(loadReceiver.OperationalState, outcome.OperationalState);
        Assert.Equal(expectedStatus, outcome.Status);
        Assert.Equal($"TEST_{kind.ToString().ToUpperInvariant()}", outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
        Assert.Equal(1, memoryModel.LoadCount);
    }

    /// <summary>Checks that imported objects never fabricate a default for a field lacking exact evidence.</summary>
    [Fact]
    public void MissingImportedFieldEvidenceBlocksAtLoadWithoutFabricatingZero()
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust: false),
            returnsValue: true,
            hasImplicitThis: true);
        var domain = new ConcreteDomain();
        var memoryModel = new CountingMemoryModel(domain);
        var imported = memoryModel.ImportObject(
            ConcreteMemory.Empty,
            DeclaringType,
            new ImportedObjectEvidenceIdentity("snapshot:test;object:missing-field"));
        var machine = CreateMachine(domain, new FixedResolver(definition, Int32Field), memoryModel);
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(imported.objRef),
            imported.mem);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = new MachineOperationalState(new BudgetState(10));
        var loadReceiver = machine.StepOne(activation.State!, operations);

        var outcome = machine.StepOne(loadReceiver.State, loadReceiver.OperationalState);

        Assert.Same(loadReceiver.State, outcome.State);
        Assert.Same(loadReceiver.OperationalState, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.Blocked, outcome.Status);
        Assert.Equal("MEMORY_IMPORTED_FIELD_UNAVAILABLE", outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
    }

    /// <summary>Checks typed boundary vectors and field resolution are frozen before execution begins.</summary>
    [Fact]
    public void TypedAdmissionFreezesCompleteBoundaryVectorsAndResolvedField()
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust: true),
            returnsValue: true,
            hasImplicitThis: true);
        var domain = new ConcreteDomain();
        var resolver = new FixedResolver(definition, Int32Field);
        var memoryModel = new CountingMemoryModel(domain);
        var machine = CreateMachine(domain, resolver, memoryModel);

        var admission = machine.ValidateMethod(definition);

        Assert.True(admission.IsAdmitted, admission.Failure?.Code);
        Assert.Equal(5, admission.InstructionCount);
        Assert.Collection(
            admission.InstructionBoundaries,
            boundary => AssertBoundary(boundary, 0),
            boundary => AssertBoundary(boundary, 1, DeclaringType),
            boundary => AssertBoundary(boundary, 6, TypeSig.Int32),
            boundary => AssertBoundary(boundary, 7, TypeSig.Int32, TypeSig.Int32),
            boundary => AssertBoundary(boundary, 8, TypeSig.Int32));
        Assert.Equal(1, resolver.FieldCallCount);
        Assert.Equal(0, resolver.MethodDefinitionCallCount);
        Assert.Equal(0, memoryModel.LoadCount);
    }

    /// <summary>Checks a supported prefix followed by an unsupported opcode rejects activation before instruction zero.</summary>
    [Fact]
    public void SupportedPrefixAndUnsupportedSuffixRejectCompleteActivation()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x17, 0x28, 0, 0, 0, 0, 0x2A]),
            returnsValue: true);
        var domain = new ConcreteDomain();
        var memoryModel = new CountingMemoryModel(domain);
        var machine = CreateMachine(domain, new FixedResolver(definition), memoryModel);

        var activation = machine.ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);

        AssertActivationFailure(activation, MachineRunStatus.Blocked, "EXEC_UNSUPPORTED_OPCODE");
        Assert.Equal(0, memoryModel.LoadCount);
    }

    /// <summary>Checks the closed E2 profile rejects a second field load without observing the resolver twice.</summary>
    [Fact]
    public void MultipleFieldLoadsRejectBeforeSecondFieldResolutionObservation()
    {
        var body = IlBody.Create(
            2,
            [
                0x02,
                0x7B, 0x01, 0, 0, 0x04,
                0x02,
                0x7B, 0x01, 0, 0, 0x04,
                0x58,
                0x2A,
            ]);
        var definition = CreateDefinition(body, returnsValue: true, hasImplicitThis: true);
        var resolver = new FixedResolver(definition, Int32Field);
        var domain = new ConcreteDomain();
        var machine = CreateMachine(domain, resolver, new CountingMemoryModel(domain));

        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);

        AssertActivationFailure(
            activation,
            MachineRunStatus.Blocked,
            "EXEC_MULTIPLE_FIELD_LOADS_UNSUPPORTED");
        Assert.Equal(1, resolver.FieldCallCount);
    }

    /// <summary>Checks instance arithmetic and decorated getter bodies cannot expand the two closed W3 profiles.</summary>
    [Fact]
    public void InstanceNonGetterAndDecoratedGetterShapesAreRejected()
    {
        var domain = new ConcreteDomain();
        var instanceArithmetic = CreateDefinition(
            IlBody.Create(1, [0x16, 0x2A]),
            returnsValue: true,
            hasImplicitThis: true);
        var instanceMachine = CreateMachine(
            domain,
            new FixedResolver(instanceArithmetic),
            new CountingMemoryModel(domain));
        var instanceActivation = instanceMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);
        AssertActivationFailure(
            instanceActivation,
            MachineRunStatus.Blocked,
            "EXEC_INSTANCE_PROFILE_UNSUPPORTED");

        var decoratedGetter = CreateDefinition(
            IlBody.Create(
                1,
                [0x00, 0x02, 0x7B, 0x01, 0, 0, 0x04, 0x2A]),
            returnsValue: true,
            hasImplicitThis: true);
        var getterMachine = CreateMachine(
            domain,
            new FixedResolver(decoratedGetter, Int32Field),
            new CountingMemoryModel(domain));
        var getterActivation = getterMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);
        AssertActivationFailure(
            getterActivation,
            MachineRunStatus.Blocked,
            "EXEC_FIELD_GETTER_SHAPE_UNSUPPORTED");
    }

    /// <summary>
    /// Checks that alternate encodings of receiver slot zero do not silently broaden the exact compiler-emitted E2
    /// <c>ldarg.0</c> profile merely because typed decoding normalizes them to the same argument index.
    /// </summary>
    /// <param name="code">A getter body using either short or long explicit slot-zero encoding.</param>
    [Theory]
    [InlineData(new byte[] { 0x0E, 0x00, 0x7B, 0x01, 0x00, 0x00, 0x04, 0x2A })]
    [InlineData(new byte[] { 0xFE, 0x09, 0x00, 0x00, 0x7B, 0x01, 0x00, 0x00, 0x04, 0x2A })]
    public void AlternateReceiverEncodingsAreRejectedByExactGetterProfile(byte[] code)
    {
        var domain = new ConcreteDomain();
        var definition = CreateDefinition(
            IlBody.Create(1, code),
            returnsValue: true,
            hasImplicitThis: true);
        var resolver = new FixedResolver(definition, Int32Field);
        var machine = CreateMachine(domain, resolver, new CountingMemoryModel(domain));

        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);

        AssertActivationFailure(
            activation,
            MachineRunStatus.Blocked,
            "EXEC_FIELD_GETTER_SHAPE_UNSUPPORTED");
        Assert.Equal(1, resolver.FieldCallCount);
    }

    /// <summary>Enumerates structural whole-body failures that must precede activation or execution.</summary>
    /// <returns>Method definition facts and the expected stable rejection.</returns>
    public static IEnumerable<object[]> InvalidAdmissionCases()
    {
        yield return [
            IlBody.Create(1, [0x1F]), 0, 0, true, MachineRunStatus.InvalidProgram, "EXEC_TRUNCATED_INSTRUCTION"];
        yield return [
            IlBody.Create(1, [0x02, 0x2A]), 0, 0, true, MachineRunStatus.InvalidProgram, "EXEC_INVALID_SLOT"];
        yield return [
            IlBody.Create(1, [0x16]), 0, 0, true, MachineRunStatus.InvalidProgram, "EXEC_MISSING_RETURN"];
        yield return [
            IlBody.Create(0, [0x2A, 0x00]), 0, 0, false, MachineRunStatus.InvalidProgram, "EXEC_CODE_AFTER_RETURN"];
        yield return [
            IlBody.Create(0, [0x16, 0x2A]), 0, 0, true, MachineRunStatus.InvalidProgram, "EXEC_MAXSTACK_EXCEEDED"];
        yield return [
            IlBody.Create(
                IlMachine<ConcreteValue, ConcreteMemory>.MaximumFrameSlotCount + 1,
                [0x2A]),
            0,
            0,
            false,
            MachineRunStatus.Blocked,
            "EXEC_MAXSTACK_LIMIT"];
        yield return [
            IlBody.Create(
                1,
                [0x06, 0x2A],
                localVariablesInitialized: false,
                localSignatureToken: 0x11000001),
            0,
            1,
            true,
            MachineRunStatus.Blocked,
            "EXEC_UNINITIALIZED_LOCALS_UNSUPPORTED"];
        yield return [
            IlBody.Create(0, [0x2A], exceptionRegionCount: 1),
            0,
            0,
            false,
            MachineRunStatus.Blocked,
            "EXEC_EH_UNSUPPORTED"];
    }

    /// <summary>Checks representative malformed or unsupported bodies through the public admission surface.</summary>
    /// <param name="body">The immutable body to validate.</param>
    /// <param name="parameterCount">The projected explicit Int32 parameter count.</param>
    /// <param name="localCount">The projected initialized Int32 local count.</param>
    /// <param name="returnsValue">Whether metadata projects an Int32 return.</param>
    /// <param name="expectedStatus">The stable rejection status.</param>
    /// <param name="expectedCode">The stable rejection code.</param>
    [Theory]
    [MemberData(nameof(InvalidAdmissionCases))]
    public void InvalidBodiesFailWholeBodyAdmission(
        IlBody body,
        int parameterCount,
        int localCount,
        bool returnsValue,
        MachineRunStatus expectedStatus,
        string expectedCode)
    {
        var definition = CreateDefinition(body, parameterCount, localCount, returnsValue);
        var domain = new ConcreteDomain();
        var machine = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain));

        var admission = machine.ValidateMethod(definition);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(expectedStatus, admission.FailureStatus);
        Assert.Equal(expectedCode, admission.Failure!.Code);
        Assert.Empty(admission.InstructionBoundaries);
        Assert.Equal(0, admission.InstructionCount);
    }

    /// <summary>Checks unresolved field metadata rejects a getter before activation and before memory observation.</summary>
    [Fact]
    public void UnresolvedFieldRejectsActivationBeforeMemoryObservation()
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust: false),
            returnsValue: true,
            hasImplicitThis: true);
        var resolver = new FixedResolver(
            ResolutionResult<ResolvedMethodDefinition>.Success(definition),
            ResolutionResult<ResolvedField>.Failed(
                ResolutionFailureKind.Unavailable,
                "META_FIELD_NOT_CAPTURED",
                "Field metadata was not captured."));
        var domain = new ConcreteDomain();
        var memoryModel = new CountingMemoryModel(domain);
        var machine = CreateMachine(domain, resolver, memoryModel);

        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);

        AssertActivationFailure(activation, MachineRunStatus.Blocked, "META_FIELD_NOT_CAPTURED");
        Assert.Equal(1, resolver.FieldCallCount);
        Assert.Equal(0, memoryModel.LoadCount);
    }

    /// <summary>Checks missing method evidence is preserved as a structured activation inability.</summary>
    [Fact]
    public void MissingMethodDefinitionProducesStructuredDependencyFailure()
    {
        var resolver = FixedResolver.Failure(
            ResolutionFailureKind.Unavailable,
            "META_METHOD_NOT_CAPTURED",
            "Method evidence was not captured.");
        var domain = new ConcreteDomain();
        var machine = CreateMachine(domain, resolver, new CountingMemoryModel(domain));

        var activation = machine.ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);

        AssertActivationFailure(activation, MachineRunStatus.Blocked, "META_METHOD_NOT_CAPTURED");
        Assert.Equal(ExecutionFailureKind.DependencyResolution, activation.Failure!.Kind);
        Assert.Equal(ResolutionFailureKind.Unavailable, activation.Failure.ResolutionFailure!.Kind);

        var changingTextResolver = FixedResolver.Failure(
            ResolutionFailureKind.Unavailable,
            "META_METHOD_NOT_CAPTURED",
            "A session-specific explanation that must not enter machine state.");
        var repeated = CreateMachine(domain, changingTextResolver, new CountingMemoryModel(domain))
            .ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);
        Assert.Equal(activation.Failure, repeated.Failure);
    }

    /// <summary>Checks unexpected ordinary capability exceptions become stable failures without partial transfers.</summary>
    [Fact]
    public void UnexpectedResolverDomainAndMemoryExceptionsAreNormalized()
    {
        var domain = new ConcreteDomain();
        var resolverMachine = CreateMachine(
            domain,
            new ThrowingResolver(),
            new CountingMemoryModel(domain));
        var resolverActivation = resolverMachine.ActivateRoot(
            Method,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        AssertActivationFailure(
            resolverActivation,
            MachineRunStatus.InvalidProgram,
            "EXEC_RESOLVER_FAILURE");

        var getterDefinition = CreateDefinition(
            CreateGetterBody(adjust: false),
            returnsValue: true,
            hasImplicitThis: true);
        var throwingMemory = new CountingMemoryModel(
            domain,
            (_, _, _) => throw new SyntheticCapabilityException());
        var memoryMachine = CreateMachine(
            domain,
            new FixedResolver(getterDefinition, Int32Field),
            throwingMemory);
        var memoryActivation = memoryMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);
        Assert.True(memoryActivation.IsSuccess, memoryActivation.Failure?.Code);
        var memoryOperations = new MachineOperationalState(new BudgetState(10));
        var loadReceiver = memoryMachine.StepOne(memoryActivation.State!, memoryOperations);
        var memoryOutcome = memoryMachine.StepOne(loadReceiver.State, loadReceiver.OperationalState);
        AssertNoTransfer(
            loadReceiver.State,
            loadReceiver.OperationalState,
            memoryOutcome,
            MachineRunStatus.InvalidProgram,
            "EXEC_MEMORY_MODEL_FAILURE");

        var arithmeticDefinition = CreateDefinition(
            IlBody.Create(2, [0x17, 0x18, 0x58, 0x2A]),
            returnsValue: true);
        var throwingDomain = new ThrowingArithmeticDomain();
        var domainMachine = new IlMachine<ConcreteValue, ConcreteMemory>(
            throwingDomain,
            new FixedResolver(arithmeticDefinition),
            new ConcreteMemoryModel(domain),
            new InstructionBudgetPolicy());
        var domainActivation = domainMachine.ActivateRoot(
            Method,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        Assert.True(domainActivation.IsSuccess, domainActivation.Failure?.Code);
        var domainOperations = new MachineOperationalState(new BudgetState(10));
        var firstConstant = domainMachine.StepOne(domainActivation.State!, domainOperations);
        var secondConstant = domainMachine.StepOne(firstConstant.State, firstConstant.OperationalState);
        var domainOutcome = domainMachine.StepOne(secondConstant.State, secondConstant.OperationalState);
        AssertNoTransfer(
            secondConstant.State,
            secondConstant.OperationalState,
            domainOutcome,
            MachineRunStatus.InvalidProgram,
            "EXEC_DOMAIN_FAILURE");
    }

    /// <summary>Checks field-step capability failures identify whether memory was actually called.</summary>
    [Fact]
    public void FieldStepKeepsDomainAndMemoryFailureOriginsTruthful()
    {
        var definition = CreateDefinition(
            CreateGetterBody(adjust: false),
            returnsValue: true,
            hasImplicitThis: true);
        var concreteDomain = new ConcreteDomain();

        var precheckDomain = new SelectiveClassificationDomain();
        var precheckMemory = new CountingMemoryModel(concreteDomain);
        var precheckObject = precheckMemory.NewObject(ConcreteMemory.Empty, DeclaringType);
        var precheckStored = precheckMemory.StoreField(
            precheckObject.mem,
            precheckObject.objRef,
            Int32Field,
            concreteDomain.ConstInt32(7));
        var precheckMachine = new IlMachine<ConcreteValue, ConcreteMemory>(
            precheckDomain,
            new FixedResolver(definition, Int32Field),
            precheckMemory,
            new InstructionBudgetPolicy());
        var precheckActivation = precheckMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(precheckObject.objRef),
            precheckStored);
        Assert.True(precheckActivation.IsSuccess, precheckActivation.Failure?.Code);
        var precheckOperations = new MachineOperationalState(new BudgetState(10));
        var precheckReceiver = precheckMachine.StepOne(precheckActivation.State!, precheckOperations);
        precheckDomain.ThrowForEveryStaticType = true;

        var precheckOutcome = precheckMachine.StepOne(
            precheckReceiver.State,
            precheckReceiver.OperationalState);

        AssertNoTransfer(
            precheckReceiver.State,
            precheckReceiver.OperationalState,
            precheckOutcome,
            MachineRunStatus.InvalidProgram,
            "EXEC_DOMAIN_FAILURE");
        Assert.Equal(ExecutionFailureKind.DomainFailure, precheckOutcome.Failure!.Kind);
        Assert.Equal(0, precheckMemory.LoadCount);

        var postcheckDomain = new SelectiveClassificationDomain();
        var postcheckMemory = new CountingMemoryModel(concreteDomain);
        var postcheckObject = postcheckMemory.NewObject(ConcreteMemory.Empty, DeclaringType);
        var postcheckStored = postcheckMemory.StoreField(
            postcheckObject.mem,
            postcheckObject.objRef,
            Int32Field,
            concreteDomain.ConstInt32(9));
        var postcheckMachine = new IlMachine<ConcreteValue, ConcreteMemory>(
            postcheckDomain,
            new FixedResolver(definition, Int32Field),
            postcheckMemory,
            new InstructionBudgetPolicy());
        var postcheckActivation = postcheckMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(postcheckObject.objRef),
            postcheckStored);
        Assert.True(postcheckActivation.IsSuccess, postcheckActivation.Failure?.Code);
        var postcheckOperations = new MachineOperationalState(new BudgetState(10));
        var postcheckReceiver = postcheckMachine.StepOne(postcheckActivation.State!, postcheckOperations);
        postcheckDomain.ThrowForInt32StaticType = true;

        var postcheckOutcome = postcheckMachine.StepOne(
            postcheckReceiver.State,
            postcheckReceiver.OperationalState);

        AssertNoTransfer(
            postcheckReceiver.State,
            postcheckReceiver.OperationalState,
            postcheckOutcome,
            MachineRunStatus.InvalidProgram,
            "EXEC_DOMAIN_FAILURE");
        Assert.Equal(ExecutionFailureKind.DomainFailure, postcheckOutcome.Failure!.Kind);
        Assert.Equal(1, postcheckMemory.LoadCount);
    }

    /// <summary>Checks one machine snapshots the first complete definition and cannot be changed mid-session.</summary>
    [Fact]
    public void ResolverCannotChangeDefinitionDuringOneMachineSession()
    {
        var firstDefinition = CreateDefinition(IlBody.Create(1, [0x16, 0x2A]), returnsValue: true);
        var laterDefinition = CreateDefinition(IlBody.Create(0, [0x2A]), returnsValue: false);
        var resolver = new FlippingResolver(firstDefinition, laterDefinition);
        var domain = new ConcreteDomain();
        var machine = CreateMachine(domain, resolver, new CountingMemoryModel(domain));

        var firstActivation = machine.ActivateRoot(
            Method,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        Assert.True(firstActivation.IsSuccess, firstActivation.Failure?.Code);
        var firstRun = Run(machine, firstActivation.State!, instructionBudget: 10);
        Assert.Equal(MachineRunStatus.Completed, firstRun.Outcome.Status);
        AssertInt32(0, firstRun.Outcome.State.ReturnValue.Value);

        var repeatedActivation = machine.ActivateRoot(
            Method,
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);
        Assert.True(repeatedActivation.IsSuccess, repeatedActivation.Failure?.Code);
        var repeatedRun = Run(machine, repeatedActivation.State!, instructionBudget: 10);

        AssertInt32(0, repeatedRun.Outcome.State.ReturnValue.Value);
        Assert.Equal(1, resolver.CallCount);
    }

    /// <summary>Checks a bounded machine rejects a second root MethodDef without resolving or caching it.</summary>
    [Fact]
    public void MachineSessionRejectsSecondRootMethod()
    {
        var definition = CreateDefinition(IlBody.Create(0, [0x2A]), returnsValue: false);
        var resolver = new FixedResolver(definition);
        var domain = new ConcreteDomain();
        var machine = CreateMachine(domain, resolver, new CountingMemoryModel(domain));
        var first = machine.ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);
        Assert.True(first.IsSuccess, first.Failure?.Code);

        var second = machine.ActivateRoot(
            new MethodHandle(Module, 0x06000002),
            ImmutableArray<ConcreteValue>.Empty,
            ConcreteMemory.Empty);

        AssertActivationFailure(second, MachineRunStatus.Blocked, "EXEC_MACHINE_SESSION_MISMATCH");
        Assert.Equal(1, resolver.MethodDefinitionCallCount);
    }

    /// <summary>Checks budget exhaustion executes no instruction and emits no event.</summary>
    [Fact]
    public void ExhaustedBudgetDoesNotClaimReturnExecuted()
    {
        var definition = CreateDefinition(IlBody.Create(0, [0x2A]), returnsValue: false);
        var domain = new ConcreteDomain();
        var machine = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain));
        var activation = machine.ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = new MachineOperationalState(new BudgetState(0));

        var outcome = machine.StepOne(activation.State!, operations);

        Assert.Same(activation.State, outcome.State);
        Assert.Same(operations, outcome.OperationalState);
        Assert.Equal(MachineRunStatus.BudgetExhausted, outcome.Status);
        Assert.Empty(outcome.Events);
        Assert.Null(outcome.Failure);
    }

    /// <summary>Checks forged resumed state against the frozen frame and typed boundary shape.</summary>
    [Fact]
    public void ForgedFrameShapeAndTypedBoundaryAreRejectedWithoutTransfer()
    {
        var definition = CreateDefinition(IlBody.Create(1, [0x16, 0x2A]), returnsValue: true);
        var domain = new ConcreteDomain();
        var machine = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain));
        var activation = machine.ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var operations = new MachineOperationalState(new BudgetState(10));

        var invalidOffset = ReplaceFrame(activation.State!, frame => frame with { IlOffset = 2 });
        AssertNoTransfer(
            invalidOffset,
            operations,
            machine.StepOne(invalidOffset, operations),
            MachineRunStatus.InvalidProgram,
            "EXEC_INVALID_INSTRUCTION_OFFSET");

        var wrongDepth = ReplaceFrame(
            activation.State!,
            frame => frame with { EvalStack = ImmutableArray.Create(domain.ConstInt32(1)) });
        AssertNoTransfer(
            wrongDepth,
            operations,
            machine.StepOne(wrongDepth, operations),
            MachineRunStatus.InvalidProgram,
            "EXEC_INVALID_ENTRY_STACK");

        var afterConstant = machine.StepOne(activation.State!, operations);
        Assert.Equal(MachineRunStatus.Ready, afterConstant.Status);
        var wrongType = ReplaceFrame(
            afterConstant.State,
            frame => frame with { EvalStack = ImmutableArray.Create(domain.ConstNull(DeclaringType)) });
        AssertNoTransfer(
            wrongType,
            afterConstant.OperationalState,
            machine.StepOne(wrongType, afterConstant.OperationalState),
            MachineRunStatus.InvalidProgram,
            "EXEC_VALUE_TYPE_MISMATCH");
    }

    /// <summary>Checks malformed state envelopes fail before metadata resolution or instruction execution.</summary>
    [Fact]
    public void MalformedStateEnvelopeFailsClosed()
    {
        var definition = CreateDefinition(IlBody.Create(0, [0x2A]), returnsValue: false);
        var domain = new ConcreteDomain();

        var defaultResolver = new FixedResolver(definition);
        var defaultMachine = CreateMachine(domain, defaultResolver, new CountingMemoryModel(domain));
        var defaultState = new MachineState<ConcreteValue, ConcreteMemory>(
            default,
            ConcreteMemory.Empty,
            OptionalValue<ConcreteValue>.None);
        var operations = new MachineOperationalState(new BudgetState(10));
        var defaultOutcome = defaultMachine.StepOne(defaultState, operations);
        AssertNoTransfer(
            defaultState,
            operations,
            defaultOutcome,
            MachineRunStatus.InvalidProgram,
            "EXEC_INVALID_CALL_STACK");
        Assert.Equal(0, defaultResolver.MethodDefinitionCallCount);

        var resolver = new FixedResolver(definition);
        var machine = CreateMachine(domain, resolver, new CountingMemoryModel(domain));
        var activation = machine.ActivateRoot(Method, ImmutableArray<ConcreteValue>.Empty, ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        Assert.Equal(1, resolver.MethodDefinitionCallCount);

        var nested = activation.State! with
        {
            CallStack = activation.State.CallStack.Add(activation.State.CallStack[0]),
        };
        AssertNoTransfer(
            nested,
            operations,
            machine.StepOne(nested, operations),
            MachineRunStatus.InvalidProgram,
            "EXEC_NESTED_FRAME_UNSUPPORTED");

        var defaultArguments = ReplaceFrame(
            activation.State,
            frame => frame with { Arguments = default });
        AssertNoTransfer(
            defaultArguments,
            operations,
            machine.StepOne(defaultArguments, operations),
            MachineRunStatus.InvalidProgram,
            "EXEC_INVALID_FRAME_SLOTS");

        var staleReturn = activation.State with
        {
            ReturnValue = OptionalValue<ConcreteValue>.Some(domain.ConstInt32(1)),
        };
        AssertNoTransfer(
            staleReturn,
            operations,
            machine.StepOne(staleReturn, operations),
            MachineRunStatus.InvalidProgram,
            "EXEC_STALE_RETURN_VALUE");
    }

    /// <summary>Checks semantic equality across independently materialized immutable arrays and memory snapshots.</summary>
    [Fact]
    public void SemanticComparerUsesDomainAndSequenceEquality()
    {
        var definition = CreateDefinition(
            IlBody.Create(1, [0x02, 0x2A]),
            parameterCount: 1,
            returnsValue: true);
        var domain = new ConcreteDomain();
        var firstMachine = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain));
        var secondMachine = CreateMachine(domain, new FixedResolver(definition), new CountingMemoryModel(domain));
        var first = firstMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstInt32(7)),
            ConcreteMemory.Empty).State!;
        var second = secondMachine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstInt32(7)),
            ConcreteMemory.Empty).State!;
        var comparer = new MachineStateSemanticComparer<ConcreteValue, ConcreteMemory>(domain);

        Assert.NotEqual(first, second);
        Assert.True(comparer.Equals(first, second));
        Assert.Equal(comparer.GetHashCode(first), comparer.GetHashCode(second));

        var invalid = new MachineState<ConcreteValue, ConcreteMemory>(
            default,
            ConcreteMemory.Empty,
            OptionalValue<ConcreteValue>.None);
        Assert.False(comparer.Equals(first, invalid));
        Assert.False(comparer.Equals(invalid, first));

        var wrongType = ReplaceFrame(
            second,
            frame => frame with
            {
                Arguments = ImmutableArray.Create(domain.DefaultValue(TypeSig.Boolean)),
            });
        Assert.False(comparer.Equals(first, wrongType));
        Assert.False(comparer.Equals(wrongType, first));
    }

    private static IlMachine<ConcreteValue, ConcreteMemory> CreateMachine(
        ConcreteDomain domain,
        IResolutionServices resolver,
        IMemoryModel<ConcreteValue, ConcreteMemory> memoryModel) =>
        new(domain, resolver, memoryModel, new InstructionBudgetPolicy());

    private static ResolvedMethodDefinition CreateDefinition(
        IlBody body,
        int parameterCount = 0,
        int localCount = 0,
        bool returnsValue = false,
        bool hasImplicitThis = false,
        MethodHandle? method = null) =>
        new(
            method ?? Method,
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

    private static IlBody CreateGetterBody(bool adjust) => adjust
        ? IlBody.Create(
            2,
            [
                0x02,                   // ldarg.0
                0x7B, 0x01, 0, 0, 0x04, // ldfld 0x04000001
                0x17,                   // ldc.i4.1
                0x58,                   // add
                0x2A,                   // ret
            ])
        : IlBody.Create(
            1,
            [
                0x02,                   // ldarg.0
                0x7B, 0x01, 0, 0, 0x04, // ldfld 0x04000001
                0x2A,                   // ret
            ]);

    private static MachineState<ConcreteValue, ConcreteMemory> ReplaceFrame(
        MachineState<ConcreteValue, ConcreteMemory> state,
        Func<FrameState<ConcreteValue>, FrameState<ConcreteValue>> replace) =>
        state with { CallStack = ImmutableArray.Create(replace(state.CallStack[0])) };

    private static RunResult Run(
        IlMachine<ConcreteValue, ConcreteMemory> machine,
        MachineState<ConcreteValue, ConcreteMemory> initialState,
        long instructionBudget)
    {
        var state = initialState;
        var operations = new MachineOperationalState(new BudgetState(instructionBudget));
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        for (var step = 0; step < 100; step++)
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

        throw new InvalidOperationException("Machine did not stop within the deterministic test bound.");
    }

    private static void AssertActivationFailure(
        MachineActivationResult<ConcreteValue, ConcreteMemory> activation,
        MachineRunStatus expectedStatus,
        string expectedCode)
    {
        Assert.False(activation.IsSuccess);
        Assert.Null(activation.State);
        Assert.Equal(expectedStatus, activation.Status);
        Assert.Equal(expectedCode, activation.Failure!.Code);
    }

    private static void AssertNoTransfer(
        MachineState<ConcreteValue, ConcreteMemory> state,
        MachineOperationalState operations,
        StepOutcome<ConcreteValue, ConcreteMemory> outcome,
        MachineRunStatus expectedStatus,
        string expectedCode)
    {
        Assert.Same(state, outcome.State);
        Assert.Same(operations, outcome.OperationalState);
        Assert.Equal(expectedStatus, outcome.Status);
        Assert.Equal(expectedCode, outcome.Failure!.Code);
        Assert.Empty(outcome.Events);
    }

    private static void AssertBoundary(
        MethodInstructionBoundary boundary,
        int expectedOffset,
        params TypeSig[] expectedTypes)
    {
        Assert.Equal(expectedOffset, boundary.IlOffset);
        Assert.Equal(expectedTypes, boundary.ExpectedStackTypes);
    }

    private static void AssertInt32(int expected, ConcreteValue actual)
    {
        var domain = new ConcreteDomain();
        Assert.True(domain.TryGetConstInt32(actual, out var value));
        Assert.Equal(expected, value);
    }

    private sealed record RunResult(
        StepOutcome<ConcreteValue, ConcreteMemory> Outcome,
        ImmutableArray<DebugEvent> Events);

    private sealed class FixedResolver : IResolutionServices
    {
        private readonly ResolutionResult<ResolvedMethodDefinition> methodResult;
        private readonly ResolutionResult<ResolvedField> fieldResult;

        internal FixedResolver(ResolvedMethodDefinition definition, ResolvedField? field = null)
            : this(
                ResolutionResult<ResolvedMethodDefinition>.Success(definition),
                field is null
                    ? ResolutionResult<ResolvedField>.Failed(
                        ResolutionFailureKind.Unavailable,
                        "META_FIELD_NOT_CONFIGURED",
                        "This fixture did not configure a field operand.")
                    : ResolutionResult<ResolvedField>.Success(field))
        {
        }

        internal FixedResolver(
            ResolutionResult<ResolvedMethodDefinition> methodResult,
            ResolutionResult<ResolvedField> fieldResult)
        {
            this.methodResult = methodResult;
            this.fieldResult = fieldResult;
        }

        internal int MethodDefinitionCallCount { get; private set; }

        internal int FieldCallCount { get; private set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            MethodDefinitionCallCount++;
            return methodResult;
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
        {
            FieldCallCount++;
            return fieldResult;
        }

        internal static FixedResolver Failure(ResolutionFailureKind kind, string code, string message) =>
            new(
                ResolutionResult<ResolvedMethodDefinition>.Failed(kind, code, message),
                ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "META_FIELD_NOT_CONFIGURED",
                    "This fixture did not configure a field operand."));
    }

    private sealed class FlippingResolver : IResolutionServices
    {
        private readonly ResolvedMethodDefinition first;
        private readonly ResolvedMethodDefinition later;

        internal FlippingResolver(ResolvedMethodDefinition first, ResolvedMethodDefinition later)
        {
            this.first = first;
            this.later = later;
        }

        internal int CallCount { get; private set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            CallCount++;
            return ResolutionResult<ResolvedMethodDefinition>.Success(CallCount == 1 ? first : later);
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
            ResolutionResult<ResolvedField>.Failed(
                ResolutionFailureKind.Unavailable,
                "META_FIELD_NOT_CONFIGURED",
                "This fixture did not configure a field operand.");
    }

    private sealed class ThrowingResolver : IResolutionServices
    {
        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
            throw new SyntheticCapabilityException();

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
            throw new SyntheticCapabilityException();
    }

    private sealed class ThrowingArithmeticDomain : IValueDomain<ConcreteValue>
    {
        private readonly ConcreteDomain inner = new();

        public ConcreteValue Bottom(TypeSig type) => inner.Bottom(type);

        public bool IsBottom(ConcreteValue value) => inner.IsBottom(value);

        public ConcreteValue Top(TypeSig type) => inner.Top(type);

        public ConcreteValue DefaultValue(TypeSig type) => inner.DefaultValue(type);

        public ConcreteValue ConstInt32(int value) => inner.ConstInt32(value);

        public ConcreteValue Join(ConcreteValue a, ConcreteValue b) => inner.Join(a, b);

        public bool IsLessThanOrEqual(ConcreteValue a, ConcreteValue b) => inner.IsLessThanOrEqual(a, b);

        public ConcreteValue Meet(ConcreteValue a, ConcreteValue b) => inner.Meet(a, b);

        public ConcreteValue Widen(ConcreteValue prev, ConcreteValue next) => inner.Widen(prev, next);

        public TypeSig GetStaticType(ConcreteValue value) => inner.GetStaticType(value);

        public StackKind GetStackKind(ConcreteValue value) => inner.GetStackKind(value);

        public bool TryGetConstInt32(ConcreteValue value, out int c) => inner.TryGetConstInt32(value, out c);

        public ConcreteValue ApplyBinary(BinaryOp op, ConcreteValue a, ConcreteValue b) =>
            throw new SyntheticCapabilityException();
    }

    private sealed class SelectiveClassificationDomain : IValueDomain<ConcreteValue>
    {
        private readonly ConcreteDomain inner = new();

        internal bool ThrowForEveryStaticType { get; set; }

        internal bool ThrowForInt32StaticType { get; set; }

        public ConcreteValue Bottom(TypeSig type) => inner.Bottom(type);

        public bool IsBottom(ConcreteValue value) => inner.IsBottom(value);

        public ConcreteValue Top(TypeSig type) => inner.Top(type);

        public ConcreteValue DefaultValue(TypeSig type) => inner.DefaultValue(type);

        public ConcreteValue ConstInt32(int value) => inner.ConstInt32(value);

        public ConcreteValue Join(ConcreteValue a, ConcreteValue b) => inner.Join(a, b);

        public bool IsLessThanOrEqual(ConcreteValue a, ConcreteValue b) => inner.IsLessThanOrEqual(a, b);

        public ConcreteValue Meet(ConcreteValue a, ConcreteValue b) => inner.Meet(a, b);

        public ConcreteValue Widen(ConcreteValue prev, ConcreteValue next) => inner.Widen(prev, next);

        public TypeSig GetStaticType(ConcreteValue value)
        {
            var type = inner.GetStaticType(value);
            if (ThrowForEveryStaticType || ThrowForInt32StaticType && type == TypeSig.Int32)
            {
                throw new SyntheticCapabilityException();
            }

            return type;
        }

        public StackKind GetStackKind(ConcreteValue value) => inner.GetStackKind(value);

        public bool TryGetConstInt32(ConcreteValue value, out int c) => inner.TryGetConstInt32(value, out c);

        public ConcreteValue ApplyBinary(BinaryOp op, ConcreteValue a, ConcreteValue b) =>
            inner.ApplyBinary(op, a, b);
    }

    private sealed class SyntheticCapabilityException : Exception
    {
    }

    private sealed class CountingMemoryModel : IMemoryModel<ConcreteValue, ConcreteMemory>
    {
        private readonly ConcreteMemoryModel inner;
        private readonly Func<ConcreteMemory, ConcreteValue, ResolvedField, MemoryLoadResult<ConcreteValue>>? load;

        internal CountingMemoryModel(
            ConcreteDomain domain,
            Func<ConcreteMemory, ConcreteValue, ResolvedField, MemoryLoadResult<ConcreteValue>>? load = null)
        {
            inner = new ConcreteMemoryModel(domain);
            this.load = load;
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
            return load is null ? inner.LoadField(mem, objRef, field) : load(mem, objRef, field);
        }

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field,
            ConcreteValue value) =>
            inner.StoreField(mem, objRef, field, value);

        public ConcreteValue LoadElement(ConcreteMemory mem, ConcreteValue arrRef, ConcreteValue index) =>
            inner.LoadElement(mem, arrRef, index);

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ConcreteValue arrRef,
            ConcreteValue index,
            ConcreteValue value) =>
            inner.StoreElement(mem, arrRef, index, value);

        internal (ConcreteValue objRef, ConcreteMemory mem) ImportObject(
            ConcreteMemory mem,
            TypeSig type,
            ImportedObjectEvidenceIdentity identity) =>
            inner.ImportObject(mem, type, identity);

        internal ConcreteMemory ImportField(
            ConcreteMemory mem,
            ConcreteValue objRef,
            ResolvedField field,
            ConcreteValue value) =>
            inner.ImportField(mem, objRef, field, value);
    }
}
