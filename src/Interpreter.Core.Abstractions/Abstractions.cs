using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Opaque module identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable module identifier value.</param>
public readonly record struct ModuleHandle(ulong Value);

/// <summary>
/// Opaque type identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable type identifier value.</param>
public readonly record struct TypeHandle(ulong Value);

/// <summary>
/// Opaque method identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable method identifier value.</param>
public readonly record struct MethodHandle(ulong Value);

/// <summary>
/// Opaque field identity used by the interpreter to avoid coupling to concrete metadata backends.
/// </summary>
/// <param name="Value">Session-stable field identifier value.</param>
public readonly record struct FieldHandle(ulong Value);

/// <summary>
/// Categorizes side effects observed or inferred while interpreting a method.
/// </summary>
[Flags]
public enum EffectKind
{
    None = 0,
    Alloc = 1 << 0,
    ReadEnv = 1 << 1,
    WriteEnv = 1 << 2,
    Time = 1 << 3,
    Random = 1 << 4,
    Threading = 1 << 5,
    Native = 1 << 6,
    Reflection = 1 << 7,
    Throw = 1 << 8,
    Unsupported = 1 << 9,
}

/// <summary>
/// Represents a single effect observation emitted during interpretation.
/// </summary>
/// <param name="Kind">Effect category.</param>
/// <param name="Code">Stable code for machine-readable diagnostics and telemetry.</param>
/// <param name="Details">Optional contextual detail suitable for explainability output.</param>
public readonly record struct EffectEvent(EffectKind Kind, string Code, string? Details = null);

/// <summary>
/// Aggregates effect categories and supporting effect events for an operation.
/// </summary>
/// <param name="Kinds">Bitwise union of all effect categories captured in <paramref name="Events"/>.</param>
/// <param name="Events">Ordered effect events describing concrete observations.</param>
public sealed record EffectSummary(EffectKind Kinds, IReadOnlyList<EffectEvent> Events);

/// <summary>
/// Describes the provenance of an unknown or unconstrained value produced by interpretation.
/// </summary>
public enum UnknownOriginKind
{
    MissingData,
    UnsupportedIL,
    ExternalCall,
    NativeCall,
    EnvRead,
    BudgetExceeded,
    AmbiguousDispatch,
}

/// <summary>
/// Associates unknown-value provenance with optional explanatory detail.
/// </summary>
/// <param name="Kind">High-level origin category for the unknown value.</param>
/// <param name="Detail">Optional free-form detail to support user-facing explanations.</param>
public readonly record struct UnknownOrigin(UnknownOriginKind Kind, string? Detail = null);

/// <summary>
/// Defines severity levels for interpreter diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Represents a stable, structured diagnostic emitted by abstraction-layer components.
/// </summary>
/// <param name="Severity">Diagnostic severity.</param>
/// <param name="Code">Stable, machine-readable code such as INTP0012.</param>
/// <param name="Message">Human-readable message describing the condition.</param>
public readonly record struct Diagnostic(DiagnosticSeverity Severity, string Code, string Message);

/// <summary>
/// Receives diagnostics emitted by interpreter subsystems.
/// </summary>
public interface IDiagnosticSink
{
    /// <summary>
    /// Records a diagnostic raised by a core component.
    /// </summary>
    /// <param name="diagnostic">Diagnostic payload to report.</param>
    void Report(in Diagnostic diagnostic);
}

/// <summary>
/// Represents mutable interpreter execution limits used to bound runtime cost.
/// </summary>
/// <param name="InstructionBudget">Remaining instruction budget.</param>
/// <param name="AllocationBudget">Remaining allocation budget in abstract units.</param>
/// <param name="MaxCallDepth">Remaining call-depth allowance.</param>
/// <param name="MaxForks">Remaining branch-fork allowance.</param>
public sealed record BudgetState(long InstructionBudget, long AllocationBudget, int MaxCallDepth, int MaxForks);

/// <summary>
/// Applies budget-consumption policies for deterministic, bounded execution.
/// </summary>
public interface IBudgetPolicy
{
    /// <summary>
    /// Tries to consume instruction budget for a pending execution action.
    /// </summary>
    /// <param name="budget">Current mutable budget state.</param>
    /// <param name="cost">Instruction cost to consume.</param>
    /// <returns><see langword="true"/> when consumption succeeds; otherwise <see langword="false"/>.</returns>
    bool TryConsumeInstruction(ref BudgetState budget, int cost = 1);

    /// <summary>
    /// Tries to consume allocation budget for a pending allocation action.
    /// </summary>
    /// <param name="budget">Current mutable budget state.</param>
    /// <param name="bytes">Requested allocation size in abstract bytes.</param>
    /// <returns><see langword="true"/> when consumption succeeds; otherwise <see langword="false"/>.</returns>
    bool TryConsumeAllocation(ref BudgetState budget, long bytes);
}

/// <summary>
/// Defines branch-handling decisions that a policy or model can request from the engine.
/// </summary>
public enum BranchDecisionKind
{
    TakeTrue,
    TakeFalse,
    Fork,
    StopForUserChoice,
    JoinBoth,
}

/// <summary>
/// Provides additional context for branch decisions.
/// </summary>
/// <param name="Description">Human-readable description of the branch condition or rationale.</param>
/// <param name="Payload">Optional opaque payload for advanced consumers.</param>
public sealed record BranchInfo(string Description, object? Payload = null);

/// <summary>
/// Represents a requested branch decision and optional supporting context.
/// </summary>
/// <param name="Kind">Requested branch-handling strategy.</param>
/// <param name="Info">Optional contextual information about the decision.</param>
public readonly record struct BranchDecision(BranchDecisionKind Kind, BranchInfo? Info = null);

/// <summary>
/// Describes null-state information for a value-domain instance.
/// </summary>
public enum Nullness
{
    NotApplicable,
    Null,
    NonNull,
    MaybeNull,
}

/// <summary>
/// Represents the evaluation-stack category of a value.
/// </summary>
public enum StackKind
{
    I4,
    I8,
    R4,
    R8,
    NativeInt,
    Ref,
    ByRef,
    ValueType,
}

/// <summary>
/// Supported unary operations for value-domain interpretation.
/// </summary>
public enum UnaryOp
{
    Neg,
    Not,
}

/// <summary>
/// Supported binary operations for value-domain interpretation.
/// </summary>
public enum BinaryOp
{
    Add,
    Sub,
    Mul,
    Div,
    Rem,
    And,
    Or,
    Xor,
    Shl,
    Shr,
    ShrUn,
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
}

/// <summary>
/// Supported primitive conversion operations for value-domain interpretation.
/// </summary>
public enum ConvOp
{
    I1,
    U1,
    I2,
    U2,
    I4,
    U4,
    I8,
    U8,
    R4,
    R8,
    NativeInt,
    NativeUInt,
}

/// <summary>
/// Defines the minimum value-domain contract required by the interpreter core.
/// </summary>
/// <typeparam name="TValue">Concrete value representation used by a domain implementation.</typeparam>
public interface IValueDomain<TValue>
{
    /// <summary>Creates a top/unknown value for a specific static type and provenance.</summary>
    TValue Top(TypeSig type, UnknownOrigin origin);

    /// <summary>Creates a null reference value for a specific reference type.</summary>
    TValue ConstNull(TypeSig refType);

    /// <summary>Creates an <see cref="int"/> constant value.</summary>
    TValue ConstInt32(int value);

    /// <summary>Creates a <see cref="long"/> constant value.</summary>
    TValue ConstInt64(long value);

    /// <summary>Creates a <see cref="bool"/> constant value.</summary>
    TValue ConstBool(bool value);

    /// <summary>Creates a string constant value.</summary>
    TValue ConstString(string value);

    /// <summary>Creates a fresh unknown value with explicit provenance.</summary>
    TValue FreshUnknown(TypeSig type, UnknownOrigin origin);

    /// <summary>Computes the least upper bound of two values.</summary>
    TValue Join(TValue a, TValue b);

    /// <summary>Computes a widening between two values for fixpoint convergence.</summary>
    TValue Widen(TValue prev, TValue next);

    /// <summary>Gets the static type associated with a value.</summary>
    TypeSig GetStaticType(TValue value);

    /// <summary>Gets the evaluation-stack category of a value.</summary>
    StackKind GetStackKind(TValue value);

    /// <summary>Gets the null-state classification for a value.</summary>
    Nullness GetNullness(TValue value);

    /// <summary>Refines a value toward a non-null assumption.</summary>
    TValue RefineNonNull(TValue value);

    /// <summary>Refines a value toward a null assumption.</summary>
    TValue RefineNull(TValue value);

    /// <summary>Tries to extract an <see cref="int"/> constant from a value.</summary>
    bool TryGetConstInt32(TValue value, out int c);

    /// <summary>Tries to extract a <see cref="bool"/> constant from a value.</summary>
    bool TryGetConstBool(TValue value, out bool b);

    /// <summary>Applies a unary operation.</summary>
    TValue ApplyUnary(UnaryOp op, TValue v);

    /// <summary>Applies a binary operation.</summary>
    TValue ApplyBinary(BinaryOp op, TValue a, TValue b);

    /// <summary>Converts a value to a target primitive representation.</summary>
    TValue Convert(ConvOp op, TValue v, bool checkedOverflow);

    /// <summary>Boxes a value to an object representation.</summary>
    TValue Box(TValue v, TypeSig boxedType);

    /// <summary>Unboxes a value to a target value type representation.</summary>
    TValue UnboxAny(TValue boxed, TypeSig targetType);
}

/// <summary>
/// Defines havoc scope kinds for conservative memory invalidation.
/// </summary>
public enum HavocRegionKind
{
    Everything,
    ReachableFromArgs,
    SpecificObject,
    SpecificAddressable,
}

/// <summary>
/// Identifies the memory region that should be conservatively invalidated.
/// </summary>
/// <param name="Kind">Region-kind selector.</param>
/// <param name="Payload">Optional region-specific payload (for example an object reference).</param>
public readonly record struct HavocRegion(HavocRegionKind Kind, object? Payload = null);

/// <summary>
/// Defines the minimum memory model contract required by the interpreter core.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
public interface IMemoryModel<TValue, TMem>
{
    /// <summary>Gets a value indicating whether the model supports object/array allocation.</summary>
    bool CanAllocate { get; }

    /// <summary>Allocates a new object instance.</summary>
    (TValue objRef, TMem mem) NewObject(TMem mem, TypeSig type);

    /// <summary>Allocates a new array instance.</summary>
    (TValue arrRef, TMem mem) NewArray(TMem mem, TypeSig elemType, TValue length);

    /// <summary>Loads a field value.</summary>
    TValue LoadField(TMem mem, TValue objRef, FieldHandle field);

    /// <summary>Stores a field value.</summary>
    TMem StoreField(TMem mem, TValue objRef, FieldHandle field, TValue value);

    /// <summary>Loads an array element value.</summary>
    TValue LoadElement(TMem mem, TValue arrRef, TValue index);

    /// <summary>Stores an array element value.</summary>
    TMem StoreElement(TMem mem, TValue arrRef, TValue index, TValue value);

    /// <summary>Creates a by-reference value targeting an object field.</summary>
    TValue MakeByRefToField(TMem mem, TValue objRef, FieldHandle field);

    /// <summary>Creates a by-reference value targeting a local slot.</summary>
    TValue MakeByRefToLocal(int localIndex);

    /// <summary>Loads through a by-reference value.</summary>
    TValue LoadIndirect(TMem mem, TValue byRef);

    /// <summary>Stores through a by-reference value.</summary>
    TMem StoreIndirect(TMem mem, TValue byRef, TValue value);

    /// <summary>Conservatively invalidates a memory region.</summary>
    TMem Havoc(TMem mem, HavocRegion region, UnknownOrigin origin);
}

/// <summary>
/// Represents a resolved type token and its canonical signature.
/// </summary>
/// <param name="Type">Opaque type handle.</param>
/// <param name="Sig">Resolved type signature.</param>
public readonly record struct ResolvedType(TypeHandle Type, TypeSig Sig);

/// <summary>
/// Represents a resolved field token and associated metadata.
/// </summary>
/// <param name="Field">Opaque field handle.</param>
/// <param name="Sig">Resolved field signature.</param>
/// <param name="DeclaringType">Declaring type handle.</param>
public readonly record struct ResolvedField(FieldHandle Field, FieldSig Sig, TypeHandle DeclaringType);

/// <summary>
/// Represents a resolved method token and associated metadata.
/// </summary>
/// <param name="Definition">Opaque method-definition handle.</param>
/// <param name="Signature">Resolved method signature.</param>
/// <param name="CalleeGenericContext">Resolved callee generic context for invocation.</param>
/// <param name="DeclaringType">Declaring type handle.</param>
public readonly record struct ResolvedMethod(
    MethodHandle Definition,
    MethodSig Signature,
    GenericContext CalleeGenericContext,
    TypeHandle DeclaringType);

/// <summary>
/// Provides VM-facing token resolution and method-body lookup services.
/// </summary>
public interface IResolutionServices
{
    /// <summary>Resolves a metadata type token in module context.</summary>
    ResolvedType ResolveType(ModuleHandle module, int metadataToken, GenericContext ctx);

    /// <summary>Resolves a metadata field token in module context.</summary>
    ResolvedField ResolveField(ModuleHandle module, int metadataToken, GenericContext ctx);

    /// <summary>Resolves a metadata method token in module context.</summary>
    ResolvedMethod ResolveMethod(ModuleHandle module, int metadataToken, GenericContext ctx);

    /// <summary>Tries to retrieve a method body for interpretation.</summary>
    bool TryGetMethodBody(MethodHandle method, out MethodBody body);

    /// <summary>Resolves virtual/interface dispatch for a runtime receiver type.</summary>
    MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType);
}

/// <summary>
/// Categorizes IL call-site instruction forms.
/// </summary>
public enum CallKind
{
    Call,
    CallVirt,
    NewObj,
}

/// <summary>
/// Represents a resolved call-site envelope provided to call models.
/// </summary>
/// <param name="Kind">IL call instruction category.</param>
/// <param name="Module">Calling module handle for provenance.</param>
/// <param name="MethodToken">Original metadata token operand.</param>
/// <param name="Target">Resolved call target metadata.</param>
/// <param name="ConstrainedType">Optional constrained type for <c>constrained.</c> callvirt forms.</param>
public readonly record struct CallSite(
    CallKind Kind,
    ModuleHandle Module,
    int MethodToken,
    ResolvedMethod Target,
    TypeSig? ConstrainedType = null);

/// <summary>
/// Categorizes outcomes returned by call-model handlers.
/// </summary>
public enum CallOutcomeKind
{
    NotHandled,
    Returned,
    Threw,
    Forked,
    StopForDecision,
}

/// <summary>
/// Represents the result of attempting to model a call site.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
/// <param name="Kind">Outcome kind.</param>
/// <param name="ReturnValue">Optional return value when <see cref="CallOutcomeKind.Returned"/>.</param>
/// <param name="ThrownException">Optional exception value when <see cref="CallOutcomeKind.Threw"/>.</param>
/// <param name="Memory">Optional updated memory snapshot.</param>
/// <param name="Effects">Optional side-effect summary.</param>
/// <param name="Forks">Optional forked outcomes.</param>
/// <param name="DecisionInfo">Optional decision context for interactive branching.</param>
public sealed record CallOutcome<TValue, TMem>(
    CallOutcomeKind Kind,
    TValue? ReturnValue = default,
    TValue? ThrownException = default,
    TMem? Memory = default,
    EffectSummary? Effects = null,
    IReadOnlyList<CallOutcome<TValue, TMem>>? Forks = null,
    BranchInfo? DecisionInfo = null);

/// <summary>
/// Provides call-model handlers with all services required to evaluate a call without depending on VM internals.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
/// <param name="Domain">Value-domain operations.</param>
/// <param name="MemoryModel">Memory-model operations.</param>
/// <param name="Resolver">Metadata/token resolution service.</param>
/// <param name="Diagnostics">Diagnostic sink for explainability output.</param>
/// <param name="Budget">Current budget state.</param>
/// <param name="Memory">Current memory snapshot.</param>
/// <param name="Site">Call-site metadata envelope.</param>
/// <param name="Arguments">Call argument values in invocation order.</param>
/// <param name="Services">Optional host/service locator for advanced call models.</param>
public sealed record CallModelContext<TValue, TMem>(
    IValueDomain<TValue> Domain,
    IMemoryModel<TValue, TMem> MemoryModel,
    IResolutionServices Resolver,
    IDiagnosticSink Diagnostics,
    BudgetState Budget,
    TMem Memory,
    CallSite Site,
    IReadOnlyList<TValue> Arguments,
    IServiceProvider? Services = null);

/// <summary>
/// Attempts to model a method call as a semantic intrinsic or lifted behavior.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
public interface ICallModel<TValue, TMem>
{
    /// <summary>
    /// Attempts to model a call-site invocation.
    /// </summary>
    /// <param name="ctx">Call-model evaluation context.</param>
    /// <returns>A modeled outcome or <see cref="CallOutcomeKind.NotHandled"/> when the model does not apply.</returns>
    CallOutcome<TValue, TMem> TryCall(CallModelContext<TValue, TMem> ctx);
}
