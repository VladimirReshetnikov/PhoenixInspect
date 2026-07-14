using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

/// <summary>Classifies what kind of truth an externally visible evaluation result asserts.</summary>
public enum EvaluationSemanticMode
{
    /// <summary>A fact decoded directly from snapshot or artifact evidence.</summary>
    Observation,

    /// <summary>A deterministic read-only calculation over observations without executing user IL.</summary>
    DerivedQuery,

    /// <summary>A result of interpreting admitted IL from recovered or explicitly assumed state.</summary>
    CounterfactualExecution,

    /// <summary>A may/must statement over a set of possible states.</summary>
    AbstractAnalysis,
}

/// <summary>Classifies how the evaluation request itself stopped.</summary>
public enum EvaluationCompletionStatus
{
    /// <summary>The request reached its normal terminal boundary.</summary>
    Completed,

    /// <summary>A supported continuation requires evidence or capability that is unavailable.</summary>
    Blocked,

    /// <summary>A deterministic execution budget was exhausted.</summary>
    BudgetExhausted,

    /// <summary>The host cancelled the request.</summary>
    Cancelled,

    /// <summary>The request or its admitted inputs violated a structural invariant.</summary>
    Invalid,
}

/// <summary>Classifies how much of the requested answer was produced.</summary>
public enum EvaluationCompleteness
{
    /// <summary>The complete requested answer is present.</summary>
    Complete,

    /// <summary>A useful, explicitly bounded subset or prefix is present.</summary>
    Partial,

    /// <summary>No value portion of the requested answer is present.</summary>
    None,
}

/// <summary>Classifies the quality and compatibility of evidence supporting an evaluation.</summary>
public enum EvaluationEvidenceStatus
{
    /// <summary>All required evidence was exact and mutually compatible.</summary>
    Exact,

    /// <summary>Only a trustworthy prefix or subset of required evidence was available.</summary>
    Partial,

    /// <summary>Required evidence was absent.</summary>
    Unavailable,

    /// <summary>Available evidence sources disagreed or selection was ambiguous.</summary>
    Conflict,

    /// <summary>Captured evidence violated a supported structural invariant.</summary>
    Invalid,
}

/// <summary>Classifies effects represented by an evaluation result independently of its value.</summary>
public enum EvaluationEffectStatus
{
    /// <summary>No target or virtual writes occurred.</summary>
    None,

    /// <summary>Writes occurred only in an interpreter-owned overlay.</summary>
    VirtualOnly,

    /// <summary>One or more effects were represented through an explicit semantic model.</summary>
    Modeled,

    /// <summary>The requested effect could not be represented safely.</summary>
    Unsupported,
}

/// <summary>Classifies one ordered provenance entry.</summary>
public enum EvaluationProvenanceKind
{
    /// <summary>A counted read from one immutable dump-memory source.</summary>
    DumpMemory,

    /// <summary>A runtime structure decoded by a named adapter.</summary>
    RuntimeStructure,

    /// <summary>Bytes or metadata decoded from a separately acquired artifact.</summary>
    Artifact,

    /// <summary>An explicit deterministic policy input.</summary>
    Policy,

    /// <summary>A deterministic transformation over earlier evidence.</summary>
    Transformation,
}

/// <summary>Records one ordered source range or transformation supporting a result.</summary>
public sealed record EvaluationProvenance
{
    /// <summary>Creates a bounded provenance entry.</summary>
    /// <param name="kind">The source or transformation category.</param>
    /// <param name="sourceId">A stable content/source identity, never an implicit local path.</param>
    /// <param name="address">The target virtual address for a memory range, when applicable.</param>
    /// <param name="requestedLength">The requested byte count for a memory range, when applicable.</param>
    /// <param name="observedLength">The observed byte count for a memory range, when applicable.</param>
    public EvaluationProvenance(
        EvaluationProvenanceKind kind,
        string sourceId,
        ulong? address = null,
        int? requestedLength = null,
        int? observedLength = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("A stable provenance source identity is required.", nameof(sourceId));
        }

        if (requestedLength is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedLength));
        }

        if (observedLength is < 0 ||
            requestedLength is int requested && observedLength is int observed && observed > requested)
        {
            throw new ArgumentOutOfRangeException(nameof(observedLength));
        }

        if (kind == EvaluationProvenanceKind.DumpMemory &&
            (address is null || requestedLength is null || observedLength is null))
        {
            throw new ArgumentException(
                "Dump-memory provenance requires an address plus requested and observed byte counts.",
                nameof(kind));
        }

        Kind = kind;
        SourceId = sourceId;
        Address = address;
        RequestedLength = requestedLength;
        ObservedLength = observedLength;
    }

    /// <summary>Gets the source or transformation category.</summary>
    public EvaluationProvenanceKind Kind { get; }

    /// <summary>Gets the stable content/source identity.</summary>
    public string SourceId { get; }

    /// <summary>Gets the target address for a memory range, when applicable.</summary>
    public ulong? Address { get; }

    /// <summary>Gets the requested byte count, when applicable.</summary>
    public int? RequestedLength { get; }

    /// <summary>Gets the observed byte count, when applicable.</summary>
    public int? ObservedLength { get; }
}

/// <summary>Provides one stable machine-readable reason and secret-safe human explanation.</summary>
public sealed record EvaluationDiagnostic
{
    /// <summary>Creates a diagnostic whose behavior never depends on parsing its message.</summary>
    /// <param name="code">A non-empty stable reason code.</param>
    /// <param name="message">A non-empty secret-safe human explanation.</param>
    public EvaluationDiagnostic(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A stable diagnostic code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A diagnostic explanation is required.", nameof(message));
        }

        Code = code;
        Message = message;
    }

    /// <summary>Gets the stable machine-readable reason code.</summary>
    public string Code { get; }

    /// <summary>Gets the secret-safe human explanation.</summary>
    public string Message { get; }
}

/// <summary>
/// Carries a host-facing value without collapsing semantic mode, completion, completeness, evidence, effects,
/// provenance, or diagnostics into one status flag.
/// </summary>
/// <typeparam name="TValue">The immutable reference-type value projection carried by the result.</typeparam>
public sealed class EvaluationResult<TValue>
    where TValue : class
{
    private EvaluationResult(
        EvaluationSemanticMode semanticMode,
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        EvaluationEvidenceContext context,
        TValue? value,
        ImmutableArray<EvaluationProvenance> provenance,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        SemanticMode = semanticMode;
        Completion = completion;
        Completeness = completeness;
        Evidence = evidence;
        Effects = effects;
        Context = context;
        Value = value;
        Provenance = provenance;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the truth mode asserted by this result.</summary>
    public EvaluationSemanticMode SemanticMode { get; }

    /// <summary>Gets how the request itself stopped.</summary>
    public EvaluationCompletionStatus Completion { get; }

    /// <summary>Gets how much of the requested answer is present.</summary>
    public EvaluationCompleteness Completeness { get; }

    /// <summary>Gets the supporting-evidence classification.</summary>
    public EvaluationEvidenceStatus Evidence { get; }

    /// <summary>Gets the independently classified effect behavior.</summary>
    public EvaluationEffectStatus Effects { get; }

    /// <summary>
    /// Gets the explicit backend-neutral evidence context, including neutral context for results without an external
    /// evidence source.
    /// </summary>
    public EvaluationEvidenceContext Context { get; }

    /// <summary>Gets the complete or partial value projection, when present.</summary>
    public TValue? Value { get; }

    /// <summary>Gets ordered, content-identified provenance entries.</summary>
    public ImmutableArray<EvaluationProvenance> Provenance { get; }

    /// <summary>Gets ordered stable diagnostics.</summary>
    public ImmutableArray<EvaluationDiagnostic> Diagnostics { get; }

    /// <summary>Creates a validated multi-axis result envelope with explicit neutral evidence context.</summary>
    /// <param name="semanticMode">The result's truth mode.</param>
    /// <param name="completion">How the request stopped.</param>
    /// <param name="completeness">How much of the answer is present.</param>
    /// <param name="evidence">The supporting-evidence classification.</param>
    /// <param name="effects">The independently classified effect behavior.</param>
    /// <param name="value">The complete or partial value, or null when completeness is none.</param>
    /// <param name="provenance">Ordered provenance entries; a default array is normalized to empty.</param>
    /// <param name="diagnostics">Ordered diagnostics; a default array is normalized to empty.</param>
    /// <returns>
    /// A result whose axes satisfy the active read-only result invariants and whose <see cref="Context"/> is
    /// <see cref="EvaluationEvidenceContext.Neutral"/>.
    /// </returns>
    public static EvaluationResult<TValue> Create(
        EvaluationSemanticMode semanticMode,
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        TValue? value,
        ImmutableArray<EvaluationProvenance> provenance = default,
        ImmutableArray<EvaluationDiagnostic> diagnostics = default) =>
        Create(
            semanticMode,
            completion,
            completeness,
            evidence,
            effects,
            value,
            EvaluationEvidenceContext.Neutral,
            provenance,
            diagnostics);

    /// <summary>Creates a validated multi-axis result envelope with an explicit evidence context.</summary>
    /// <param name="semanticMode">The result's truth mode.</param>
    /// <param name="completion">How the request stopped.</param>
    /// <param name="completeness">How much of the answer is present.</param>
    /// <param name="evidence">The supporting-evidence classification.</param>
    /// <param name="effects">The independently classified effect behavior.</param>
    /// <param name="value">The complete or partial value, or null when completeness is none.</param>
    /// <param name="context">
    /// The immutable evidence source, identity, fallback, and applied-bound context. Use
    /// <see cref="EvaluationEvidenceContext.Neutral"/> when no external evidence context applies.
    /// </param>
    /// <param name="provenance">Ordered provenance entries; a default array is normalized to empty.</param>
    /// <param name="diagnostics">Ordered diagnostics; a default array is normalized to empty.</param>
    /// <returns>A result whose axes and explicit context satisfy the active read-only result invariants.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static EvaluationResult<TValue> Create(
        EvaluationSemanticMode semanticMode,
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        TValue? value,
        EvaluationEvidenceContext context,
        ImmutableArray<EvaluationProvenance> provenance = default,
        ImmutableArray<EvaluationDiagnostic> diagnostics = default)
    {
        if (!Enum.IsDefined(semanticMode))
        {
            throw new ArgumentOutOfRangeException(nameof(semanticMode));
        }

        if (!Enum.IsDefined(completion))
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        if (!Enum.IsDefined(completeness))
        {
            throw new ArgumentOutOfRangeException(nameof(completeness));
        }

        if (!Enum.IsDefined(evidence))
        {
            throw new ArgumentOutOfRangeException(nameof(evidence));
        }

        if (!Enum.IsDefined(effects))
        {
            throw new ArgumentOutOfRangeException(nameof(effects));
        }

        ArgumentNullException.ThrowIfNull(context);

        if (semanticMode is EvaluationSemanticMode.Observation or EvaluationSemanticMode.DerivedQuery &&
            effects != EvaluationEffectStatus.None)
        {
            throw new ArgumentException("Observation and derived-query results cannot claim effects.", nameof(effects));
        }

        if (completeness is EvaluationCompleteness.Complete or EvaluationCompleteness.Partial && value is null)
        {
            throw new ArgumentNullException(nameof(value), "A complete or partial result requires a value projection.");
        }

        if (completeness == EvaluationCompleteness.None && value is not null)
        {
            throw new ArgumentException("A result with no answer cannot carry a value projection.", nameof(value));
        }

        if (completion != EvaluationCompletionStatus.Completed && completeness == EvaluationCompleteness.Complete)
        {
            throw new ArgumentException("Only a normally completed request may carry a complete answer.", nameof(completeness));
        }

        var normalizedProvenance = provenance.IsDefault
            ? ImmutableArray<EvaluationProvenance>.Empty
            : provenance;
        if (normalizedProvenance.Any(static item => item is null))
        {
            throw new ArgumentException("Provenance entries cannot be null.", nameof(provenance));
        }

        var normalizedDiagnostics = diagnostics.IsDefault
            ? ImmutableArray<EvaluationDiagnostic>.Empty
            : diagnostics;
        if (normalizedDiagnostics.Any(static item => item is null))
        {
            throw new ArgumentException("Diagnostic entries cannot be null.", nameof(diagnostics));
        }

        return new EvaluationResult<TValue>(
            semanticMode,
            completion,
            completeness,
            evidence,
            effects,
            context,
            value,
            normalizedProvenance,
            normalizedDiagnostics);
    }
}
