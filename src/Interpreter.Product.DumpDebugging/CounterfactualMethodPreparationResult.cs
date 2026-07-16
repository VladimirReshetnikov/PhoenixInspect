using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Projects one stable, value-free failure from the rooted counterfactual preparation boundary.
/// </summary>
/// <remarks>
/// Preparation never claims a partial value. Its failure keeps the public result axes independent, identifies a
/// validated request when one existed, preserves exact graph-traversal accounting after discovery began, and may
/// retain the normalized core planning failure for structural diagnosis. Capability objects and exception payloads
/// are never exposed.
/// </remarks>
public sealed class CounterfactualMethodPreparationFailure
{
    private readonly ImmutableArray<EvaluationProvenance> provenance;
    private readonly ImmutableArray<EvaluationDiagnostic> diagnostics;

    internal CounterfactualMethodPreparationFailure(
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        EvaluationEvidenceContext context,
        string? requestSha256,
        MethodGraphTraversalAccounting? traversalAccounting,
        ExecutionFailure? coreFailure,
        ImmutableArray<EvaluationProvenance> provenance,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        if (completion == EvaluationCompletionStatus.Completed)
        {
            throw new ArgumentException("A preparation failure cannot report normal completion.", nameof(completion));
        }

        if (!Enum.IsDefined(completion) || !Enum.IsDefined(evidence) || !Enum.IsDefined(effects))
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        if (effects is not (EvaluationEffectStatus.None or EvaluationEffectStatus.Unsupported))
        {
            throw new ArgumentException(
                "Preparation can report only no effects or one unsupported requested effect.",
                nameof(effects));
        }

        ArgumentNullException.ThrowIfNull(context);
        if (requestSha256 is not null)
        {
            CounterfactualCanonical.ValidateSha256(requestSha256, nameof(requestSha256));
        }

        SemanticMode = EvaluationSemanticMode.CounterfactualExecution;
        Completion = completion;
        Completeness = EvaluationCompleteness.None;
        Evidence = evidence;
        Effects = effects;
        Context = context;
        RequestSha256 = requestSha256;
        TraversalAccounting = traversalAccounting;
        CoreFailure = coreFailure;
        this.provenance = CopyOrEmpty(provenance, nameof(provenance));
        this.diagnostics = CopyOrEmpty(diagnostics, nameof(diagnostics));
    }

    /// <summary>Gets the counterfactual-execution truth mode shared by every rooted preparation outcome.</summary>
    public EvaluationSemanticMode SemanticMode { get; }

    /// <summary>Gets whether preparation was blocked, invalid, cancelled, or exhausted a deterministic budget.</summary>
    public EvaluationCompletionStatus Completion { get; }

    /// <summary>Gets <see cref="EvaluationCompleteness.None"/> because preparation failures carry no answer value.</summary>
    public EvaluationCompleteness Completeness { get; }

    /// <summary>Gets the aggregate quality of the evidence reached before preparation stopped.</summary>
    public EvaluationEvidenceStatus Evidence { get; }

    /// <summary>Gets whether an unsupported modeled effect prevented graph preparation.</summary>
    public EvaluationEffectStatus Effects { get; }

    /// <summary>
    /// Gets explicit evidence identities and only the deterministic preparation bounds whose guarded stages were
    /// actually reached.
    /// </summary>
    public EvaluationEvidenceContext Context { get; }

    /// <summary>
    /// Gets the validated canonical request digest, or <see langword="null"/> when raw request validation failed.
    /// </summary>
    public string? RequestSha256 { get; }

    /// <summary>
    /// Gets exact discovery accounting after graph preparation entered a traversal session, or
    /// <see langword="null"/> before that boundary.
    /// </summary>
    public MethodGraphTraversalAccounting? TraversalAccounting { get; }

    /// <summary>Gets the originating graph-planner failure when planning was reached, or null otherwise.</summary>
    public ExecutionFailure? CoreFailure { get; }

    /// <summary>Gets a defensive copy of ordered, content-identified preparation provenance.</summary>
    public ImmutableArray<EvaluationProvenance> Provenance => CounterfactualCanonical.Copy(provenance);

    /// <summary>Gets a defensive copy of stable payload-omitting preparation diagnostics.</summary>
    public ImmutableArray<EvaluationDiagnostic> Diagnostics => CounterfactualCanonical.Copy(diagnostics);

    private static ImmutableArray<T> CopyOrEmpty<T>(ImmutableArray<T> values, string parameterName)
        where T : class
    {
        var normalized = values.IsDefault ? ImmutableArray<T>.Empty : values;
        if (normalized.Any(static value => value is null))
        {
            throw new ArgumentException("Failure projections cannot contain null entries.", parameterName);
        }

        return CounterfactualCanonical.Copy(normalized);
    }
}

/// <summary>
/// Represents exactly one issuer-certified rooted counterfactual plan or one nonthrowing preparation failure.
/// </summary>
/// <typeparam name="TMemory">The persistent memory snapshot type privately retained by a successful plan.</typeparam>
/// <remarks>
/// This strict union prevents a caller from combining a plan with stale failure facts. Success is possible only
/// through one <see cref="CounterfactualMethodRunner{TMemory}"/> instance after request, binding, graph, depth,
/// signature, field-observation, and argument-materialization validation all succeed.
/// </remarks>
public sealed class CounterfactualMethodPreparationResult<TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    private CounterfactualMethodPreparationResult(
        CounterfactualMethodPlan<TMemory>? plan,
        CounterfactualMethodPreparationFailure? failure)
    {
        if ((plan is null) == (failure is null))
        {
            throw new ArgumentException("A preparation result requires exactly one plan or failure.");
        }

        Plan = plan;
        Failure = failure;
    }

    /// <summary>Gets whether preparation produced one complete issuer-certified plan.</summary>
    public bool IsSuccess => Plan is not null;

    /// <summary>Gets the complete immutable plan, or <see langword="null"/> after any failure.</summary>
    public CounterfactualMethodPlan<TMemory>? Plan { get; }

    /// <summary>Gets the stable multi-axis failure, or <see langword="null"/> on success.</summary>
    public CounterfactualMethodPreparationFailure? Failure { get; }

    internal static CounterfactualMethodPreparationResult<TMemory> Succeeded(
        CounterfactualMethodPlan<TMemory> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new CounterfactualMethodPreparationResult<TMemory>(plan, null);
    }

    internal static CounterfactualMethodPreparationResult<TMemory> Failed(
        CounterfactualMethodPreparationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new CounterfactualMethodPreparationResult<TMemory>(null, failure);
    }
}
