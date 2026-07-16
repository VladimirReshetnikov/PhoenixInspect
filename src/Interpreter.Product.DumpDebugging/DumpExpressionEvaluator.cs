using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;

namespace Interpreter.Product.DumpDebugging;

/// <summary>Identifies the one populated case of a W5 expression-evaluation outcome.</summary>
public enum DumpExpressionEvaluationOutcomeKind
{
    /// <summary>The W2 direct-field or opt-in W6 member-chain path returned its complete derived-query result.</summary>
    DerivedQuery = 1,

    /// <summary>The existing W4 path returned its complete counterfactual-execution result.</summary>
    CounterfactualExecution = 2,

    /// <summary>W5 classification rejected the raw expression before evidence acquisition.</summary>
    ClassificationFailure = 3,

    /// <summary>W5 method acquisition returned one typed product failure before W4 preparation.</summary>
    AcquisitionFailure = 4,

    /// <summary>The existing W4 runner returned its complete value-free preparation failure.</summary>
    CounterfactualPreparationFailure = 5,
}

/// <summary>
/// Routes one expression outcome without flattening the complete existing W2, W4, and opt-in W6 result contracts.
/// </summary>
/// <remarks>
/// Exactly one payload property is populated. W2 and W6 retain <see cref="EvaluationResult{T}"/> with
/// <see cref="DumpQueryValue"/> and its <see cref="EvaluationSemanticMode.DerivedQuery"/> truth mode. W4 retains
/// either its complete <see cref="CounterfactualExecutionResult"/> or value-free preparation failure and its
/// counterfactual truth mode. Classification and acquisition failures do not invent either semantic mode.
/// </remarks>
public sealed class DumpExpressionEvaluationOutcome
{
    private DumpExpressionEvaluationOutcome(
        DumpExpressionEvaluationOutcomeKind kind,
        DumpExpressionRequest? request,
        EvaluationResult<DumpQueryValue>? derivedQueryResult,
        CounterfactualExecutionResult? counterfactualExecutionResult,
        DumpExpressionClassification? classificationFailure,
        DumpMethodAcquisitionFailure? acquisitionFailure,
        CounterfactualMethodPreparationFailure? counterfactualPreparationFailure)
    {
        Kind = kind;
        Request = request;
        DerivedQueryResult = derivedQueryResult;
        CounterfactualExecutionResult = counterfactualExecutionResult;
        ClassificationFailure = classificationFailure;
        AcquisitionFailure = acquisitionFailure;
        CounterfactualPreparationFailure = counterfactualPreparationFailure;
    }

    /// <summary>Gets the discriminator for the one populated payload.</summary>
    public DumpExpressionEvaluationOutcomeKind Kind { get; }

    /// <summary>
    /// Gets the bounded W5 request for every canonically issued input, or <see langword="null"/> when classification
    /// rejected an oversized expression, oversized root name, or non-exact root before issuance.
    /// </summary>
    public DumpExpressionRequest? Request { get; }

    /// <summary>Gets the complete W2 or opt-in W6 result only for <see cref="DumpExpressionEvaluationOutcomeKind.DerivedQuery"/>.</summary>
    public EvaluationResult<DumpQueryValue>? DerivedQueryResult { get; }

    /// <summary>
    /// Gets the complete existing W4 result only for
    /// <see cref="DumpExpressionEvaluationOutcomeKind.CounterfactualExecution"/>.
    /// </summary>
    public CounterfactualExecutionResult? CounterfactualExecutionResult { get; }

    /// <summary>
    /// Gets the complete rejected syntax classification only for
    /// <see cref="DumpExpressionEvaluationOutcomeKind.ClassificationFailure"/>.
    /// </summary>
    public DumpExpressionClassification? ClassificationFailure { get; }

    /// <summary>
    /// Gets the typed product acquisition failure only for
    /// <see cref="DumpExpressionEvaluationOutcomeKind.AcquisitionFailure"/>.
    /// </summary>
    public DumpMethodAcquisitionFailure? AcquisitionFailure { get; }

    /// <summary>
    /// Gets the complete existing W4 preparation failure only for
    /// <see cref="DumpExpressionEvaluationOutcomeKind.CounterfactualPreparationFailure"/>.
    /// </summary>
    public CounterfactualMethodPreparationFailure? CounterfactualPreparationFailure { get; }

    internal static DumpExpressionEvaluationOutcome FromDerivedQuery(
        DumpExpressionRequest request,
        EvaluationResult<DumpQueryValue> result) => new(
        DumpExpressionEvaluationOutcomeKind.DerivedQuery,
        request ?? throw new ArgumentNullException(nameof(request)),
        result ?? throw new ArgumentNullException(nameof(result)),
        counterfactualExecutionResult: null,
        classificationFailure: null,
        acquisitionFailure: null,
        counterfactualPreparationFailure: null);

    internal static DumpExpressionEvaluationOutcome FromCounterfactualExecution(
        DumpExpressionRequest request,
        CounterfactualExecutionResult result) => new(
        DumpExpressionEvaluationOutcomeKind.CounterfactualExecution,
        request ?? throw new ArgumentNullException(nameof(request)),
        derivedQueryResult: null,
        result ?? throw new ArgumentNullException(nameof(result)),
        classificationFailure: null,
        acquisitionFailure: null,
        counterfactualPreparationFailure: null);

    internal static DumpExpressionEvaluationOutcome FromClassificationFailure(
        DumpExpressionClassification classification) => new(
        DumpExpressionEvaluationOutcomeKind.ClassificationFailure,
        (classification ?? throw new ArgumentNullException(nameof(classification))).Request,
        derivedQueryResult: null,
        counterfactualExecutionResult: null,
        classification,
        acquisitionFailure: null,
        counterfactualPreparationFailure: null);

    internal static DumpExpressionEvaluationOutcome FromAcquisitionFailure(
        DumpExpressionRequest request,
        DumpMethodAcquisitionFailure failure) => new(
        DumpExpressionEvaluationOutcomeKind.AcquisitionFailure,
        request ?? throw new ArgumentNullException(nameof(request)),
        derivedQueryResult: null,
        counterfactualExecutionResult: null,
        classificationFailure: null,
        failure ?? throw new ArgumentNullException(nameof(failure)),
        counterfactualPreparationFailure: null);

    internal static DumpExpressionEvaluationOutcome FromCounterfactualPreparationFailure(
        DumpExpressionRequest request,
        CounterfactualMethodPreparationFailure failure) => new(
        DumpExpressionEvaluationOutcomeKind.CounterfactualPreparationFailure,
        request ?? throw new ArgumentNullException(nameof(request)),
        derivedQueryResult: null,
        counterfactualExecutionResult: null,
        classificationFailure: null,
        acquisitionFailure: null,
        failure ?? throw new ArgumentNullException(nameof(failure)));
}

/// <summary>Composes the W2 evaluator, W4 runner, and opt-in W6 chain engine behind one expression entry path.</summary>
/// <remarks>
/// This facade owns routing and lifecycle only. It does not define a common value lattice or result schema. A W2
/// expression is prepared and evaluated by <see cref="DumpQueryEngine"/>. The exact method expression is acquired by
/// <see cref="DumpMethodAcquisitionFacade"/> and prepared/executed by one existing W4 runner. An explicitly selected
/// W6 profile consumes only a frozen project-owned member-chain plan. Rejected syntax never reaches evidence binding.
/// </remarks>
public static class DumpExpressionEvaluator
{
    /// <summary>Classifies, routes, and evaluates one bounded expression against an already-open dump.</summary>
    /// <param name="session">The immutable dump session used by the selected existing evaluation path.</param>
    /// <param name="expression">Raw expression text retained without normalization by a canonical bounded request.</param>
    /// <param name="rootBinding">The exact host-selected root evidence used by both existing paths.</param>
    /// <param name="policy">The closed W5 policy; method limits are retained but unused by the W2 path.</param>
    /// <param name="cancellationToken">
    /// Cancellation observed by W4 at ready machine boundaries. The finite synchronous W2 operation retains its
    /// existing no-cancellation contract.
    /// </param>
    /// <returns>
    /// A strict routing union preserving the complete W2 result, W4 result/preparation failure, or typed W5
    /// classification/acquisition failure. No lowest-common-denominator projection is created.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/>, <paramref name="rootBinding"/>, or <paramref name="policy"/> is null.
    /// </exception>
    public static DumpExpressionEvaluationOutcome Evaluate(
        ClrmdDumpSession session,
        string? expression,
        DumpQueryRootBinding rootBinding,
        DumpExpressionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        return Evaluate(
            session,
            expression,
            rootBinding,
            policy,
            DumpExpressionLanguageProfile.FrozenW5,
            cancellationToken);
    }

    /// <summary>Classifies, routes, and evaluates one bounded expression under an explicit language profile.</summary>
    /// <param name="session">The immutable dump session used by the selected evaluation path.</param>
    /// <param name="expression">Raw expression text retained without normalization by the canonical request.</param>
    /// <param name="rootBinding">The exact host-selected root evidence used by every admitted path.</param>
    /// <param name="policy">The closed product policy retained unchanged across W2, W4, and W6 routing.</param>
    /// <param name="languageProfile">
    /// The frozen-W5 or opt-in fixed-depth W6 profile. Both use the same pinned complete Roslyn expression parse and
    /// differ only in versioned project-owned subset admission.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed by W4 at ready machine boundaries. Finite synchronous W2/W6 operations retain their
    /// existing no-cancellation contract.
    /// </param>
    /// <returns>
    /// A strict union preserving the complete W2/W6 derived-query result, W4 result/preparation failure, or typed
    /// classification/acquisition failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/>, <paramref name="rootBinding"/>, or <paramref name="policy"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="languageProfile"/> is not defined.</exception>
    public static DumpExpressionEvaluationOutcome Evaluate(
        ClrmdDumpSession session,
        string? expression,
        DumpQueryRootBinding rootBinding,
        DumpExpressionPolicy policy,
        DumpExpressionLanguageProfile languageProfile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rootBinding);
        ArgumentNullException.ThrowIfNull(policy);
        if (!Enum.IsDefined(languageProfile))
        {
            throw new ArgumentOutOfRangeException(nameof(languageProfile));
        }

        var classification = DumpExpressionClassifier.Classify(
            expression,
            rootBinding,
            policy,
            languageProfile);
        if (classification.Status != DumpExpressionClassificationStatus.Accepted)
        {
            return DumpExpressionEvaluationOutcome.FromClassificationFailure(classification);
        }

        var request = classification.Request!;
        if (classification.Kind == DumpExpressionKind.DerivedQuery)
        {
            var preparation = DumpQueryEngine.PrepareParsed(
                session,
                request.ParsedExpression!,
                request.ParserBounds,
                rootBinding);
            var result = preparation.IsSuccess
                ? DumpQueryEngine.Evaluate(session, preparation.Plan!)
                : preparation.Failure!;
            return DumpExpressionEvaluationOutcome.FromDerivedQuery(request, result);
        }

        if (classification.Kind == DumpExpressionKind.FixedDepthMemberChain)
        {
            return EvaluateMemberChain(new ClrmdDumpMemberChainEvidenceSource(session), request);
        }

        return EvaluateMethod(new ClrmdDumpMethodEvidenceSource(session), request, cancellationToken);
    }

    internal static DumpExpressionEvaluationOutcome EvaluateMemberChain(
        IDumpMemberChainEvidenceSource source,
        DumpExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        var preparation = DumpMemberChainPreparationFacade.Prepare(source, request);
        var result = preparation.IsSuccess
            ? DumpMemberChainEngine.Evaluate(source, preparation.Plan!)
            : DumpMemberChainEngine.FromPreparationFailure(
                source,
                request.RootBinding,
                request.ReachedBounds,
                request.Sha256,
                preparation.Failure!);
        return DumpExpressionEvaluationOutcome.FromDerivedQuery(request, result);
    }

    internal static DumpExpressionEvaluationOutcome EvaluateMethod(
        IDumpMethodEvidenceSource source,
        DumpExpressionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        var acquisition = DumpMethodAcquisitionFacade.Acquire(source, request);
        if (!acquisition.IsSuccess)
        {
            return DumpExpressionEvaluationOutcome.FromAcquisitionFailure(request, acquisition.Failure!);
        }

        var runner = new CounterfactualMethodRunner<CounterfactualDumpMemory>();
        var preparation = runner.Prepare(acquisition.Binding!.Candidate);
        if (!preparation.IsSuccess)
        {
            return DumpExpressionEvaluationOutcome.FromCounterfactualPreparationFailure(
                request,
                preparation.Failure!);
        }

        return DumpExpressionEvaluationOutcome.FromCounterfactualExecution(
            request,
            runner.Run(preparation.Plan!, cancellationToken));
    }
}
