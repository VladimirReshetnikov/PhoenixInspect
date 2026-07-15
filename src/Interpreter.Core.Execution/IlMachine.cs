using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Executes deterministic single-instruction transfers from either a legacy W3 method plan or an opt-in prepared W4
/// direct-call graph.
/// </summary>
/// <typeparam name="TValue">The value-domain representation used by frame slots and return values.</typeparam>
/// <typeparam name="TMemory">The persistent memory snapshot threaded through machine state.</typeparam>
/// <remarks>
/// The shared scenario-derived instruction profile contains primitive Int32 constants, arguments, initialized locals,
/// unchecked add/subtract/multiply, ordinary instance-Int32 <c>ldfld</c>, <c>nop</c>, and <c>ret</c>. Legacy activation
/// retains W3's call-free, one-field getter boundary. <see cref="ActivatePreparedGraph"/> instead consumes a graph whose
/// complete method, field, and direct-call closure was already resolved and typed; execution never re-resolves it and
/// can push/pop structurally frozen interpreted frames. Field loads remain exact by default and may continue from
/// canonical partial or unavailable evidence only through the explicit unknown policy and approximation-domain
/// capability. Interpreted call boundaries bypass lineage work for exact values; explained <c>Int32</c> arguments
/// and returns require
/// <see cref="IInterpretedCallLineageDomain{TValue}"/> to preserve canonical boundary explanations. Unsupported bodies
/// or incomplete graphs execute no prefix.
/// </remarks>
public sealed partial class IlMachine<TValue, TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    /// <summary>
    /// Gets the largest argument, local, or evaluation-stack vector inspected by the bounded prototype machine.
    /// </summary>
    public const int MaximumFrameSlotCount = ExecutionLimits.MaximumFrameSlotCount;

    private readonly IValueDomain<TValue> _domain;
    private readonly IResolutionServices _resolver;
    private readonly IMemoryModel<TValue, TMemory> _memoryModel;
    private readonly IBudgetPolicy _budgetPolicy;
    private readonly UnknownExecutionPolicy _unknownExecutionPolicy;
    private readonly object _sessionGate = new();
    private MethodHandle? _sessionMethod;
    private Lazy<PlanPreparationResult>? _sessionPlan;

    /// <summary>
    /// Creates a draft IL machine over explicit value, metadata, memory, and budget capabilities.
    /// </summary>
    /// <param name="domain">The value lattice, primitive operations, and deterministic default provider.</param>
    /// <param name="resolver">The atomic method-definition and contextual field resolver.</param>
    /// <param name="memoryModel">The persistent memory capability invoked by admitted memory instructions.</param>
    /// <param name="budgetPolicy">The deterministic instruction-consumption policy.</param>
    /// <param name="unknownExecutionPolicy">
    /// Whether validated explanatory <see cref="int"/> unknowns may enter and remain in executable state.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="unknownExecutionPolicy"/> is undefined.
    /// </exception>
    public IlMachine(
        IValueDomain<TValue> domain,
        IResolutionServices resolver,
        IMemoryModel<TValue, TMemory> memoryModel,
        IBudgetPolicy budgetPolicy,
        UnknownExecutionPolicy unknownExecutionPolicy = UnknownExecutionPolicy.ExactOnly)
    {
        if (!Enum.IsDefined(unknownExecutionPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(unknownExecutionPolicy));
        }

        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _memoryModel = memoryModel ?? throw new ArgumentNullException(nameof(memoryModel));
        _budgetPolicy = budgetPolicy ?? throw new ArgumentNullException(nameof(budgetPolicy));
        _unknownExecutionPolicy = unknownExecutionPolicy;
    }

    /// <summary>
    /// Creates one root frame from a resolved method signature and caller-supplied receiver/argument values.
    /// </summary>
    /// <param name="method">The content-derived MethodDef identity to activate.</param>
    /// <param name="arguments">
    /// Ordered domain values. An instance method requires its exact receiver at slot zero, followed by explicit
    /// parameters; a static method accepts only its explicit parameters.
    /// </param>
    /// <param name="memory">The initial persistent-memory snapshot, already prepared for any external evidence.</param>
    /// <returns>
    /// A ready initial state, or a structured admission/activation failure. No instruction budget is involved and no
    /// execution event is emitted by this operation.
    /// </returns>
    public MachineActivationResult<TValue, TMemory> ActivateRoot(
        MethodHandle method,
        ImmutableArray<TValue> arguments,
        TMemory memory)
    {
        if (!MethodHandle.IsValidMetadataToken(method.MetadataToken))
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_METHOD_HANDLE",
                "Root activation requires a non-nil MethodDef handle.",
                method,
                0);
        }

        if (arguments.IsDefault)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidSlot,
                "EXEC_INVALID_ARGUMENT_VECTOR",
                "Root activation received an uninitialized argument array.",
                method,
                0);
        }

        if (memory is null)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.MemoryFailure,
                "EXEC_INVALID_MEMORY_STATE",
                "Root activation requires a non-null persistent-memory snapshot.",
                method,
                0);
        }

        if (arguments.Length > MaximumFrameSlotCount)
        {
            return ActivationFailed(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_FRAME_SLOT_LIMIT",
                $"Argument vectors are limited to {MaximumFrameSlotCount} values.",
                method,
                0);
        }

        var prepared = GetSessionPlan(method);
        if (!prepared.IsSuccess)
        {
            return new MachineActivationResult<TValue, TMemory>(null, prepared.Status, prepared.Failure);
        }

        var plan = prepared.Plan!;
        if (arguments.Length != plan.ArgumentTypes.Length)
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidSlot,
                "EXEC_ARGUMENT_SHAPE_MISMATCH",
                $"Metadata requires {plan.ArgumentTypes.Length} argument value(s), but activation supplied {arguments.Length}.",
                method,
                0);
        }

        try
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                var failure = ValidateValue(
                    arguments[index],
                    plan.ArgumentTypes[index],
                    "argument",
                    index,
                    method,
                    0,
                    plan.Definition.Signature.HasImplicitThis && index == 0
                        ? ValuePrecisionRequirement.Exact
                        : ValuePrecisionRequirement.Executable);
                if (failure is not null)
                {
                    return new MachineActivationResult<TValue, TMemory>(
                        null,
                        MachineRunStatus.InvalidProgram,
                        failure);
                }
            }

            var locals = ImmutableArray.CreateBuilder<TValue>(plan.Definition.Signature.LocalTypes.Length);
            for (var index = 0; index < plan.Definition.Signature.LocalTypes.Length; index++)
            {
                var type = plan.Definition.Signature.LocalTypes[index];
                var value = _domain.DefaultValue(type);
                var failure = ValidateValue(
                    value,
                    type,
                    "initialized local",
                    index,
                    method,
                    0,
                    ValuePrecisionRequirement.Exact);
                if (failure is not null)
                {
                    return new MachineActivationResult<TValue, TMemory>(
                        null,
                        MachineRunStatus.InvalidProgram,
                        failure);
                }

                locals.Add(value);
            }

            var frame = new FrameState<TValue>(
                method,
                0,
                arguments,
                locals.ToImmutable(),
                ImmutableArray<TValue>.Empty);
            return new MachineActivationResult<TValue, TMemory>(
                MachineState<TValue, TMemory>.Create(frame, memory),
                MachineRunStatus.Ready,
                null);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return ActivationFailed(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DomainFailure,
                "EXEC_DOMAIN_ACTIVATION_FAILURE",
                "The value domain rejected metadata-derived root activation.",
                method,
                0);
        }
    }

    /// <summary>
    /// Validates an already resolved definition against the complete W3 profile without executing it.
    /// </summary>
    /// <param name="definition">The atomic body/signature/local definition to inspect.</param>
    /// <returns>Typed instruction-boundary evidence or the first deterministic whole-body rejection.</returns>
    /// <remarks>
    /// This diagnostic entry point uses the machine's field resolver but does not bind its one-method execution
    /// session. Normal callers should use <see cref="ActivateRoot"/>, which freezes the admitted plan for execution.
    /// </remarks>
    public MethodAdmissionResult ValidateMethod(ResolvedMethodDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        try
        {
            var result = MethodPlanBuilder.Build(definition, _resolver);
            return result.IsSuccess
                ? result.Plan!.Admission
                : new MethodAdmissionResult(
                    false,
                    0,
                    ImmutableArray<MethodInstructionBoundary>.Empty,
                    result.Status,
                    result.Failure);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return new MethodAdmissionResult(
                false,
                0,
                ImmutableArray<MethodInstructionBoundary>.Empty,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DependencyResolution,
                    "EXEC_RESOLVER_FAILURE",
                    "The metadata resolver rejected whole-body admission.",
                    definition.Method,
                    0));
        }
    }

    /// <summary>
    /// Attempts to execute exactly one instruction from the active frame.
    /// </summary>
    /// <param name="state">The immutable semantic state before the requested transition.</param>
    /// <param name="operationalState">Deterministic budget bookkeeping excluded from semantic equality.</param>
    /// <returns>
    /// A successor and events for a successful transfer; a terminal target exception for the admitted null-field
    /// boundary; or the unchanged state with a structured reason when no instruction could execute.
    /// </returns>
    public StepOutcome<TValue, TMemory> StepOne(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(operationalState);

        return StepOneCore(state, operationalState)
            .CertifyTransitionFrom(this, state, operationalState);
    }

    private StepOutcome<TValue, TMemory> StepOneCore(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState)
    {
        if (TryGetPreparedGraphSession(out var preparedGraph, out var maximumLogicalCallDepth))
        {
            return StepPreparedGraph(
                state,
                operationalState,
                preparedGraph!,
                maximumLogicalCallDepth);
        }

        var envelopeFailure = ValidateStateEnvelope(state, operationalState);
        if (envelopeFailure is not null)
        {
            return Failed(state, operationalState, MachineRunStatus.InvalidProgram, envelopeFailure);
        }

        var legacyEnvelopeFailure = ValidateLegacyExecutionEnvelope(state, operationalState);
        if (legacyEnvelopeFailure is not null)
        {
            return Failed(state, operationalState, MachineRunStatus.InvalidProgram, legacyEnvelopeFailure);
        }

        if (state.TerminalTargetException is { } terminalTargetException)
        {
            return new StepOutcome<TValue, TMemory>(
                state,
                operationalState,
                MachineRunStatus.TargetException,
                ImmutableArray<DebugEvent>.Empty,
                Failure: null,
                TargetException: terminalTargetException);
        }

        if (state.CallStack.Length == 0)
        {
            return new StepOutcome<TValue, TMemory>(
                state,
                operationalState,
                MachineRunStatus.Completed,
                ImmutableArray<DebugEvent>.Empty);
        }

        if (state.CallStack.Length != 1)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_NESTED_FRAME_UNSUPPORTED",
                    "The call-free W3 profile accepts exactly one root frame."));
        }

        var frame = state.CallStack[0];
        if (frame is null)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_FRAME",
                    "The root call-stack slot contains no frame."));
        }

        if (!MethodHandle.IsValidMetadataToken(frame.Method.MetadataToken))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_METHOD_HANDLE",
                    "The active frame does not identify a non-nil MethodDef token.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.Arguments.IsDefault || frame.Locals.IsDefault || frame.EvalStack.IsDefault)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidSlot,
                    "EXEC_INVALID_FRAME_SLOTS",
                    "The active frame contains an uninitialized slot array.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.Arguments.Length > MaximumFrameSlotCount ||
            frame.Locals.Length > MaximumFrameSlotCount ||
            frame.EvalStack.Length > MaximumFrameSlotCount)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_FRAME_SLOT_LIMIT",
                    $"Arguments, locals, and evaluation stack are each limited to {MaximumFrameSlotCount} values.",
                    frame.Method,
                    frame.IlOffset));
        }

        var prepared = GetSessionPlan(frame.Method);
        if (!prepared.IsSuccess)
        {
            return Failed(state, operationalState, prepared.Status, prepared.Failure!);
        }

        var plan = prepared.Plan!;
        if (!plan.TryGetInstruction(frame.IlOffset, out var instruction) ||
            !plan.TryGetBoundary(frame.IlOffset, out var boundary))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_INSTRUCTION_OFFSET",
                    "The current IL offset is not an instruction boundary in the frozen plan.",
                    frame.Method,
                    frame.IlOffset));
        }

        var updatedBudget = operationalState.Budget;
        try
        {
            if (!_budgetPolicy.TryConsumeInstruction(ref updatedBudget, 1))
            {
                return new StepOutcome<TValue, TMemory>(
                    state,
                    operationalState,
                    MachineRunStatus.BudgetExhausted,
                    ImmutableArray<DebugEvent>.Empty);
            }
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_BUDGET_POLICY_FAILURE",
                    "The budget policy rejected instruction-consumption bookkeeping.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.Arguments.Length != plan.ArgumentTypes.Length ||
            frame.Locals.Length != plan.Definition.Signature.LocalTypes.Length)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidSlot,
                    "EXEC_FRAME_SHAPE_MISMATCH",
                    "Frame argument or local count disagrees with the frozen metadata shape.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (frame.EvalStack.Length != boundary.ExpectedStackTypes.Length)
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.InvalidStack,
                    "EXEC_INVALID_ENTRY_STACK",
                    $"The frozen boundary requires stack depth {boundary.ExpectedStackTypes.Length}, but the frame carries {frame.EvalStack.Length}.",
                    frame.Method,
                    frame.IlOffset));
        }

        ExecutionFailure? shapeFailure;
        try
        {
            shapeFailure = ValidateFrameValues(frame, plan, boundary);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_DOMAIN_FAILURE",
                    "The value domain rejected active-frame validation.",
                    frame.Method,
                    frame.IlOffset));
        }

        if (shapeFailure is not null)
        {
            return Failed(state, operationalState, MachineRunStatus.InvalidProgram, shapeFailure);
        }

        try
        {
            return Execute(
                state,
                operationalState,
                frame,
                plan,
                instruction,
                updatedBudget,
                MachineRunStatus.InvalidProgram);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return Failed(
                state,
                operationalState,
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DomainFailure,
                    "EXEC_DOMAIN_FAILURE",
                    "The value domain rejected the admitted instruction transfer.",
                    frame.Method,
                    frame.IlOffset));
        }
    }

    private PlanPreparationResult GetSessionPlan(MethodHandle method)
    {
        Lazy<PlanPreparationResult> lazy;
        lock (_sessionGate)
        {
            if (_preparedGraph is not null)
            {
                return PlanPreparationResult.Failed(
                    MachineRunStatus.Blocked,
                    new ExecutionFailure(
                        ExecutionFailureKind.ResourceLimit,
                        "EXEC_MACHINE_SESSION_MISMATCH",
                        "One bounded machine cannot mix legacy and prepared-graph execution sessions.",
                        method,
                        0));
            }

            if (_sessionMethod is null)
            {
                _sessionMethod = method;
                _sessionPlan = new Lazy<PlanPreparationResult>(
                    () => PreparePlan(method),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }
            else if (_sessionMethod.Value != method)
            {
                return PlanPreparationResult.Failed(
                    MachineRunStatus.Blocked,
                    new ExecutionFailure(
                        ExecutionFailureKind.ResourceLimit,
                        "EXEC_MACHINE_SESSION_MISMATCH",
                        "One bounded machine snapshots exactly one root MethodDef.",
                        method,
                        0));
            }

            lazy = _sessionPlan!;
        }

        return lazy.Value;
    }

    private PlanPreparationResult PreparePlan(MethodHandle method)
    {
        try
        {
            var definitionResult = _resolver.GetMethodDefinition(method);
            if (!definitionResult.IsSuccess)
            {
                var failure = definitionResult.Failure ?? new ResolutionFailure(
                    ResolutionFailureKind.Invalid,
                    "RESOLUTION_INVALID_RESULT",
                    "Method resolver returned an invalid default result.");
                var sanitizedFailure = ResolutionFailureDiagnostics.Sanitize(failure);
                return PlanPreparationResult.Failed(
                    failure.Kind == ResolutionFailureKind.Invalid
                        ? MachineRunStatus.InvalidProgram
                        : MachineRunStatus.Blocked,
                    new ExecutionFailure(
                        ExecutionFailureKind.DependencyResolution,
                        failure.Code,
                        "Method-definition resolution did not produce an executable definition.",
                        method,
                        0,
                        sanitizedFailure));
            }

            var definition = definitionResult.Value;
            if (definition.Method != method)
            {
                return PlanPreparationResult.Failed(
                    MachineRunStatus.InvalidProgram,
                    new ExecutionFailure(
                        ExecutionFailureKind.DependencyResolution,
                        "EXEC_METHOD_IDENTITY_CONFLICT",
                        "Resolver returned a method definition with a different structural identity.",
                        method,
                        0));
            }

            return MethodPlanBuilder.Build(definition, _resolver);
        }
        catch (Exception exception) when (IsCapabilityException(exception))
        {
            return PlanPreparationResult.Failed(
                MachineRunStatus.InvalidProgram,
                new ExecutionFailure(
                    ExecutionFailureKind.DependencyResolution,
                    "EXEC_RESOLVER_FAILURE",
                    "The metadata resolver rejected method-plan preparation.",
                    method,
                    0));
        }
    }

    private ExecutionFailure? ValidateFrameValues(
        FrameState<TValue> frame,
        AdmittedMethodPlan plan,
        MethodInstructionBoundary boundary)
    {
        for (var index = 0; index < frame.Arguments.Length; index++)
        {
            var failure = ValidateValue(
                frame.Arguments[index],
                plan.ArgumentTypes[index],
                "argument",
                index,
                frame.Method,
                frame.IlOffset,
                plan.Definition.Signature.HasImplicitThis && index == 0
                    ? ValuePrecisionRequirement.Exact
                    : ValuePrecisionRequirement.Executable);
            if (failure is not null)
            {
                return failure;
            }
        }

        for (var index = 0; index < frame.Locals.Length; index++)
        {
            var failure = ValidateValue(
                frame.Locals[index],
                plan.Definition.Signature.LocalTypes[index],
                "local",
                index,
                frame.Method,
                frame.IlOffset,
                ValuePrecisionRequirement.Executable);
            if (failure is not null)
            {
                return failure;
            }
        }

        for (var index = 0; index < frame.EvalStack.Length; index++)
        {
            var failure = ValidateValue(
                frame.EvalStack[index],
                boundary.ExpectedStackTypes[index],
                "evaluation-stack",
                index,
                frame.Method,
                frame.IlOffset,
                ValuePrecisionRequirement.Executable);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private ExecutionFailure? ValidateValue(
        TValue value,
        TypeSig expectedType,
        string slotKind,
        int index,
        MethodHandle method,
        int ilOffset,
        ValuePrecisionRequirement precisionRequirement)
    {
        if (value is null)
        {
            return InvalidValue("EXEC_NULL_DOMAIN_VALUE", "contains a null domain value");
        }

        if (_domain.IsBottom(value))
        {
            return InvalidValue("EXEC_INFEASIBLE_VALUE", "is lattice bottom and cannot occur in executable state");
        }

        var actualType = _domain.GetStaticType(value);
        if (!Equals(actualType, expectedType))
        {
            return InvalidValue("EXEC_VALUE_TYPE_MISMATCH", "does not match the metadata-projected structural type");
        }

        var expectedKind = GetExpectedStackKind(expectedType);
        if (_domain.GetStackKind(value) != expectedKind)
        {
            return InvalidValue("EXEC_VALUE_STACK_KIND_MISMATCH", "does not match the metadata-projected stack category");
        }

        if (_domain is IValuePrecisionDomain<TValue> precisionDomain)
        {
            var precision = precisionDomain.GetPrecision(value);
            if (!Enum.IsDefined(precision))
            {
                return InvalidValue(
                    "EXEC_VALUE_PRECISION_INVALID",
                    "has an undefined value-precision classification");
            }

            if (precision == ValuePrecisionKind.UnexplainedUnknown)
            {
                return InvalidValue(
                    "EXEC_UNEXPLAINED_UNKNOWN",
                    "is semantic top without a validated explanatory lineage root");
            }

            if (precisionRequirement == ValuePrecisionRequirement.ExplainedUnknown &&
                precision != ValuePrecisionKind.ExplainedUnknown)
            {
                return InvalidValue(
                    "EXEC_FIELD_APPROXIMATION_PRECISION_INVALID",
                    "is not the explained unknown required from approximate field-load evidence");
            }

            if (precision == ValuePrecisionKind.ExplainedUnknown &&
                (precisionRequirement == ValuePrecisionRequirement.Exact ||
                 _unknownExecutionPolicy != UnknownExecutionPolicy.ExplainedInt32))
            {
                return InvalidValue(
                    "EXEC_NON_EXACT_ARGUMENT",
                    "is not exact at a boundary configured for exact execution");
            }

            if (precision == ValuePrecisionKind.ExplainedUnknown && !Equals(expectedType, TypeSig.Int32))
            {
                return InvalidValue(
                    "EXEC_UNKNOWN_TYPE_UNSUPPORTED",
                    "is an explained unknown outside the admitted Int32 execution profile");
            }
        }
        else if (Equals(expectedType, TypeSig.Int32) && !_domain.TryGetConstInt32(value, out _))
        {
            return InvalidValue("EXEC_NON_EXACT_ARGUMENT", "is not an exact Int32 value required by concrete activation");
        }

        return null;

        ExecutionFailure InvalidValue(string code, string reason) =>
            new(
                ExecutionFailureKind.InvalidSlot,
                code,
                $"The {slotKind} value at slot {index} {reason}.",
                method,
                ilOffset);
    }

    private static StackKind GetExpectedStackKind(TypeSig type) =>
        Equals(type, TypeSig.Int32)
            ? StackKind.I4
            : type.IsMetadataTypeDefinition
                ? StackKind.Ref
                : throw new NotSupportedException("The type is outside the admitted W3 stack-kind closure.");

    private static ExecutionFailure? ValidateStateEnvelope(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState)
    {
        if (state.CallStack.IsDefault)
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_CALL_STACK",
                "Machine state contains an uninitialized call-stack array.");
        }

        if (state.Memory is null)
        {
            return new ExecutionFailure(
                ExecutionFailureKind.MemoryFailure,
                "EXEC_INVALID_MEMORY_STATE",
                "Machine state contains a null persistent-memory snapshot.");
        }

        if (operationalState.Budget is null)
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_OPERATIONAL_STATE",
                "Operational state contains no budget snapshot.");
        }

        if (state.CallStack.Length > 0 && state.ReturnValue.HasValue)
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_STALE_RETURN_VALUE",
                "An in-progress machine state cannot carry a terminal return value.");
        }

        if (state.TerminalTargetException is not null &&
            (state.CallStack.Length != 0 || state.ReturnValue.HasValue))
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_TARGET_TERMINATION",
                "A target-terminated state must have an empty call stack and no return value.");
        }


        return null;
    }

    private static ExecutionFailure? ValidateLegacyExecutionEnvelope(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState)
    {
        if (operationalState.ConfiguredMaximumLogicalCallDepth is not null ||
            operationalState.RequiredLogicalCallDepth is not null ||
            operationalState.ModelAttempts.IsDefault ||
            !operationalState.ModelAttempts.IsEmpty ||
            operationalState.ModelInvocationCount != 0 ||
            operationalState.CompletedModeledCallCount != 0 ||
            operationalState.ObservedLogicalDepthHighWater != 1 ||
            operationalState.ActiveFrameDepthHighWater != 1 ||
            state.CallStack.Any(frame => frame?.ReturnSite is not null))
        {
            return new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_EXECUTION_MODE_STATE_MISMATCH",
                "A legacy call-free execution state cannot carry prepared-graph return sites or depth facts.");
        }

        return null;
    }

    private static MachineActivationResult<TValue, TMemory> ActivationFailed(
        MachineRunStatus status,
        ExecutionFailureKind kind,
        string code,
        string message,
        MethodHandle method,
        int offset) =>
        new(null, status, new ExecutionFailure(kind, code, message, method, offset));

    private static StepOutcome<TValue, TMemory> Failed(
        MachineState<TValue, TMemory> state,
        MachineOperationalState operationalState,
        MachineRunStatus status,
        ExecutionFailure failure) =>
        new(state, operationalState, status, ImmutableArray<DebugEvent>.Empty, failure);

    private static bool IsCapabilityException(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException;

    private enum ValuePrecisionRequirement
    {
        Executable,
        Exact,
        ExplainedUnknown,
    }
}
