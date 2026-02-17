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
/// The effect model is a draft abstraction used to connect call analysis with explainability and budget policies,
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
    /// <param name="executionRequest">The parent execution request used for budget context and session-level policy decisions.</param>
    /// <param name="cancellationToken">A token that aborts classification work when host cancellation or budget termination is requested.</param>
    /// <returns>
    /// A value task that resolves to a call-site classification containing target-kind, effect categories, and an explainable rationale.
    /// </returns>
    ValueTask<CallSiteClassification> ClassifyAsync(
        CallSiteDescriptor request,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);
}
