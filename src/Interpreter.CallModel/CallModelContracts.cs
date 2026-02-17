using Interpreter.Abstractions;

namespace Interpreter.CallModel;

/// <summary>
/// Represents the coarse-grained behavioral class assigned to a call target while interpreting a method body.
/// </summary>
/// <remarks>
/// This classification is intentionally conservative in the prototype phase and is expected to be refined when
/// call-shape analysis, effect summaries, and host policy integration become more concrete.
/// </remarks>
public enum CallTargetKind
{
    /// <summary>
    /// Indicates that the call target is fully known and can be resolved to a deterministic method identity.
    /// </summary>
    Resolved,

    /// <summary>
    /// Indicates that the call target is known only by a partial descriptor and must be interpreted with bounded uncertainty.
    /// </summary>
    PartiallyResolved,

    /// <summary>
    /// Indicates that the call target cannot be resolved in the current context and requires unknown-propagation behavior.
    /// </summary>
    Unknown,
}

/// <summary>
/// Describes side-effect classes that a call may exhibit in prototype interpretation.
/// </summary>
/// <remarks>
/// The effect model is a draft abstraction used to connect call analysis with explainability and cancellation-aware policies,
/// not a finalized semantic commitment for production behavior.
/// </remarks>
public enum CallEffectKind
{
    /// <summary>
    /// Indicates that the call is considered observationally pure in the current abstract model.
    /// </summary>
    None,

    /// <summary>
    /// Indicates that the call may mutate managed heap state tracked by the prototype memory model.
    /// </summary>
    HeapMutation,

    /// <summary>
    /// Indicates that the call may interact with external runtime state that is not reproducible from dump artifacts alone.
    /// </summary>
    ExternalInteraction,

    /// <summary>
    /// Indicates that effect behavior is unknown and should be treated as maximally conservative by downstream components.
    /// </summary>
    Unknown,
}

/// <summary>
/// Captures the current call-classification request emitted by the interpreter core for a single IL call instruction.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier used to correlate call decisions with diagnostic artifacts.</param>
/// <param name="CallerMethodIdentity">Gets the fully qualified identity of the method currently being interpreted.</param>
/// <param name="InstructionOffset">Gets the IL offset of the call instruction under analysis.</param>
/// <param name="RawTargetDescriptor">Gets a draft textual descriptor for the call target derived from metadata and stack state.</param>
/// <remarks>
/// The payload is intentionally string-heavy in early prototyping so design reviews can focus on dependency seams and
/// decision responsibilities before introducing rigid symbolic model objects.
/// </remarks>
public sealed record CallSiteDescriptor(
    string SessionId,
    string CallerMethodIdentity,
    int InstructionOffset,
    string RawTargetDescriptor);

/// <summary>
/// Represents the classification outcome for one call-site analysis operation.
/// </summary>
/// <param name="TargetKind">Gets the resolved call-target category used by the execution engine for branching behavior.</param>
/// <param name="CandidateMethodIdentities">Gets candidate method identities that may be invoked when target resolution is ambiguous.</param>
/// <param name="Effects">Gets the effect categories inferred for the call, ordered from most to least certain.</param>
/// <param name="Rationale">Gets a human-readable rationale explaining how the classification was derived.</param>
/// <remarks>
/// This result type is intended to preserve explainability from the first prototype iteration and should evolve in tandem
/// with call/effects architecture proposals.
/// </remarks>
public sealed record CallSiteClassification(
    CallTargetKind TargetKind,
    IReadOnlyList<string> CandidateMethodIdentities,
    IReadOnlyList<CallEffectKind> Effects,
    string Rationale);

/// <summary>
/// Defines the prototype service contract that classifies call instructions into target and effect categories.
/// </summary>
/// <remarks>
/// Implementations may use metadata-only heuristics, host-provided summaries, or conservative fallback policies.
/// The API shape is draft-only and may change as the call model matures.
/// </remarks>
public interface ICallSiteClassifier
{
    /// <summary>
    /// Classifies the specified call site and returns a deterministic classification record for the current prototype policy.
    /// </summary>
    /// <param name="request">The call-site descriptor produced by the interpreter core for the instruction being processed.</param>
    /// <param name="executionRequest">The parent execution request used for cancellation context and session-level policy decisions.</param>
    /// <param name="cancellationToken">A token that aborts classification work when host cancellation is requested.</param>
    /// <returns>
    /// A value task that resolves to a call-site classification containing target-kind, effect categories, and an explainable rationale.
    /// </returns>
    ValueTask<CallSiteClassification> ClassifyAsync(
        CallSiteDescriptor request,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes the high-level dynamic binder operation recognized from compiler-emitted DLR call-site patterns.
/// </summary>
/// <remarks>
/// The prototype currently focuses on enough operation categories to model <c>dynamic</c> member calls and conversions.
/// Additional operation kinds should be introduced only when design proposals define the corresponding interpretation rules.
/// </remarks>
public enum DynamicOperationKind
{
    /// <summary>
    /// Indicates a dynamic instance or static member invocation operation.
    /// </summary>
    InvokeMember,

    /// <summary>
    /// Indicates a dynamic invocation of a delegate or callable value.
    /// </summary>
    Invoke,

    /// <summary>
    /// Indicates a dynamic conversion operation from one runtime type to another.
    /// </summary>
    Convert,

    /// <summary>
    /// Indicates a dynamic binary operator dispatch.
    /// </summary>
    BinaryOperation,

    /// <summary>
    /// Indicates a dynamic unary operator dispatch.
    /// </summary>
    UnaryOperation,
}

/// <summary>
/// Captures per-argument binder policy flags that influence dynamic overload resolution semantics.
/// </summary>
/// <param name="UseCompileTimeType">Gets whether binding should use the compile-time argument type instead of runtime type.</param>
/// <param name="IsNamedArgument">Gets whether the argument is passed by name and therefore participates in name-based reordering.</param>
/// <param name="ArgumentName">Gets the named-argument identifier when <paramref name="IsNamedArgument"/> is true.</param>
/// <param name="IsStaticTypeReceiver">Gets whether this argument encodes a static receiver represented by a type token/value.</param>
/// <param name="IsRef">Gets whether the argument is passed by reference.</param>
/// <param name="IsOut">Gets whether the argument is passed as an out-only reference.</param>
/// <remarks>
/// This record intentionally models only a conservative subset of C# runtime-binder argument flags so the prototype can
/// validate call lifting and explainability behavior before committing to complete DLR parity.
/// </remarks>
public sealed record DynamicArgumentPolicy(
    bool UseCompileTimeType,
    bool IsNamedArgument,
    string? ArgumentName,
    bool IsStaticTypeReceiver,
    bool IsRef,
    bool IsOut);

/// <summary>
/// Describes one dynamic dispatch operation that was lifted from DLR call-site IL into a semantic call-model request.
/// </summary>
/// <param name="SessionId">Gets the execution session identifier used for diagnostics and policy scoping.</param>
/// <param name="CallerMethodIdentity">Gets the fully qualified method identity containing the dynamic call site.</param>
/// <param name="InstructionOffset">Gets the IL offset where the lifted dynamic dispatch originates.</param>
/// <param name="OperationKind">Gets the dynamic operation kind derived from binder metadata.</param>
/// <param name="MemberName">Gets the target member name for member-oriented operations such as <see cref="DynamicOperationKind.InvokeMember"/>.</param>
/// <param name="CallingContextType">Gets the binder context type used for accessibility and overload-resolution rules.</param>
/// <param name="CompileTimeArgumentTypes">Gets the compile-time argument type display names captured from call-site delegate shape.</param>
/// <param name="RuntimeArgumentTypes">Gets runtime argument type display names reconstructed from dump state when available.</param>
/// <param name="ArgumentPolicies">Gets argument-policy descriptors aligned by index with the argument type arrays.</param>
/// <remarks>
/// The request remains string-based so host tooling can iterate on diagnostics and target-selection UX while type-system
/// abstractions and metadata binding contracts are still in design.
/// </remarks>
public sealed record DynamicDispatchRequest(
    string SessionId,
    string CallerMethodIdentity,
    int InstructionOffset,
    DynamicOperationKind OperationKind,
    string? MemberName,
    string CallingContextType,
    IReadOnlyList<string> CompileTimeArgumentTypes,
    IReadOnlyList<string?> RuntimeArgumentTypes,
    IReadOnlyList<DynamicArgumentPolicy> ArgumentPolicies);

/// <summary>
/// Defines outcome categories for one dynamic dispatch resolution attempt.
/// </summary>
/// <remarks>
/// These categories deliberately separate deterministic success from bounded uncertainty so stepping and explainability
/// flows can surface why virtual execution could or could not enter a specific overload.
/// </remarks>
public enum DynamicDispatchOutcome
{
    /// <summary>
    /// Indicates that one target method was selected deterministically for this dynamic dispatch.
    /// </summary>
    Resolved,

    /// <summary>
    /// Indicates that multiple plausible targets remain and host policy should choose between conservative fallback strategies.
    /// </summary>
    Ambiguous,

    /// <summary>
    /// Indicates that no valid target could be resolved from currently available argument and metadata evidence.
    /// </summary>
    Unresolved,
}

/// <summary>
/// Represents the result of dynamic dispatch analysis returned to interpreter core and host stepping layers.
/// </summary>
/// <param name="Outcome">Gets the dynamic dispatch resolution outcome category.</param>
/// <param name="SelectedMethodIdentity">Gets the selected target method identity when <paramref name="Outcome"/> is <see cref="DynamicDispatchOutcome.Resolved"/>.</param>
/// <param name="CandidateMethodIdentities">Gets candidate target method identities considered by the resolver in evaluation order.</param>
/// <param name="Rationale">Gets a concise rationale explaining the evidence and policy that produced this result.</param>
/// <param name="UsedRuntimeTypeFallback">Gets whether runtime argument type evidence had to fall back to compile-time types for one or more arguments.</param>
/// <remarks>
/// The shape is intentionally explainability-first and can evolve alongside richer candidate scoring metadata in later milestones.
/// </remarks>
public sealed record DynamicDispatchResolution(
    DynamicDispatchOutcome Outcome,
    string? SelectedMethodIdentity,
    IReadOnlyList<string> CandidateMethodIdentities,
    string Rationale,
    bool UsedRuntimeTypeFallback);

/// <summary>
/// Defines a prototype service contract that resolves lifted dynamic operations into deterministic or conservatively bounded call targets.
/// </summary>
/// <remarks>
/// Implementations may rely on metadata-only heuristics, Roslyn-assisted symbol resolution, or hybrid strategies while
/// dynamic-call architecture proposals are validated. This interface remains draft and should not be treated as a stable API.
/// </remarks>
public interface IDynamicDispatchResolver
{
    /// <summary>
    /// Resolves a lifted dynamic dispatch request and returns selected target metadata or bounded unresolved outcomes.
    /// </summary>
    /// <param name="request">The dynamic dispatch request describing binder semantics, argument typing evidence, and call-site context.</param>
    /// <param name="executionRequest">The parent execution request carrying session policy and cancellation constraints.</param>
    /// <param name="cancellationToken">A token used to cancel expensive target-discovery work when execution stops.</param>
    /// <returns>
    /// A value task that resolves to a dynamic dispatch resolution containing selected method identity, candidate targets,
    /// and explainability rationale for host-facing diagnostics.
    /// </returns>
    ValueTask<DynamicDispatchResolution> ResolveAsync(
        DynamicDispatchRequest request,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);
}
