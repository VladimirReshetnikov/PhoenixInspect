using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Carries one canonical, inspect-only product result for bounded counterfactual method execution.
/// </summary>
/// <remarks>
/// The ordinary evaluation axes remain independent and explicit, but only truthful cross-axis rows are issuable. This
/// non-generic envelope exposes no runtime value or capability: its closed value union contains only an exact integer,
/// a canonical explained-unknown integer lineage, an unchanged W4.7 target fragment, or a payload-free execution
/// prefix. Construction is assembly-owned during this unstable draft W4 phase; changing an admitted row or canonical
/// field requires a new schema version.
/// </remarks>
public sealed class CounterfactualExecutionResult
{
    /// <summary>Gets the only canonical schema version emitted by the draft W4 execution facade.</summary>
    public const int CanonicalSchemaVersion = 1;

    private readonly ImmutableArray<EvaluationProvenance> provenance;
    private readonly ImmutableArray<EvaluationDiagnostic> diagnostics;
    private readonly ImmutableArray<byte> canonicalBytes;

    private CounterfactualExecutionResult(
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        CounterfactualExecutionValue? value,
        CounterfactualExecutionContext context,
        ImmutableArray<EvaluationProvenance> provenance,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        ValidateEnum(completion, nameof(completion));
        ValidateEnum(completeness, nameof(completeness));
        ValidateEnum(evidence, nameof(evidence));
        ValidateEnum(effects, nameof(effects));
        ArgumentNullException.ThrowIfNull(context);

        var copiedProvenance = CopyAndRejectNull(provenance, nameof(provenance));
        var copiedDiagnostics = CopyAndRejectNull(diagnostics, nameof(diagnostics));
        if (completion != EvaluationCompletionStatus.Completed && copiedDiagnostics.IsEmpty)
        {
            throw new ArgumentException(
                "Every non-completed counterfactual result requires at least one stable diagnostic.",
                nameof(diagnostics));
        }

        ValidateOriginAndAxes(
            completion,
            completeness,
            evidence,
            effects,
            value,
            context,
            copiedProvenance,
            copiedDiagnostics);

        SchemaVersion = CanonicalSchemaVersion;
        SemanticMode = EvaluationSemanticMode.CounterfactualExecution;
        Completion = completion;
        Completeness = completeness;
        Evidence = evidence;
        Effects = effects;
        Value = value;
        Context = context;
        this.provenance = copiedProvenance;
        this.diagnostics = copiedDiagnostics;
        IsDeterministicReplay = completion != EvaluationCompletionStatus.Cancelled;
        canonicalBytes = CounterfactualExecutionCanonicalCodec.Encode(this);
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the version of the canonical product-result schema.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the truth mode, always <see cref="EvaluationSemanticMode.CounterfactualExecution"/>.</summary>
    public EvaluationSemanticMode SemanticMode { get; }

    /// <summary>Gets how the bounded counterfactual request stopped.</summary>
    public EvaluationCompletionStatus Completion { get; }

    /// <summary>
    /// Gets whether a complete answer, a partial unknown return or execution prefix, or no value portion is present.
    /// </summary>
    public EvaluationCompleteness Completeness { get; }

    /// <summary>Gets the aggregate quality and compatibility of evidence reached by this result.</summary>
    public EvaluationEvidenceStatus Evidence { get; }

    /// <summary>Gets the independently classified effect behavior represented by this result.</summary>
    public EvaluationEffectStatus Effects { get; }

    /// <summary>Gets the closed product value projection, or <see langword="null"/> when no value portion exists.</summary>
    public CounterfactualExecutionValue? Value { get; }

    /// <summary>Gets the immutable origin, evidence, accounting, structural-plan, and transcript context.</summary>
    public CounterfactualExecutionContext Context { get; }

    /// <summary>Gets a defensive copy of ordered content-identified provenance entries.</summary>
    public ImmutableArray<EvaluationProvenance> Provenance => CounterfactualCanonical.Copy(provenance);

    /// <summary>Gets a defensive copy of ordered stable payload-safe diagnostics.</summary>
    public ImmutableArray<EvaluationDiagnostic> Diagnostics => CounterfactualCanonical.Copy(diagnostics);

    /// <summary>
    /// Gets whether the result is eligible for deterministic replay comparison; this is false exactly for host
    /// cancellation, whose observation point is intentionally nondeterministic.
    /// </summary>
    public bool IsDeterministicReplay { get; }

    /// <summary>Gets a defensive copy of the domain-separated schema-v1 canonical result bytes.</summary>
    /// <remarks>The bytes are replay material and are not automatically safe for telemetry.</remarks>
    public ImmutableArray<byte> CanonicalBytes => CounterfactualCanonical.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    internal static CounterfactualExecutionResult CreateRooted<TMemory>(
        CounterfactualMethodPlan<TMemory> plan,
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        CounterfactualExecutionValue? value,
        CounterfactualBoundStatus instructionStatus,
        long? instructionUsed,
        long? instructionRemaining,
        int? observedLogicalDepthHighWater,
        int? activeFrameDepthHighWater,
        CounterfactualBoundStatus lineageStatus,
        int? lineageNodeCount,
        ImmutableArray<CounterfactualFieldObservation> reachedFieldObservations,
        ImmutableArray<int> reachedFieldLoadOrdinals,
        ImmutableArray<PureModelAttempt> modelAttempts,
        int modelInvocationCount,
        int completedModeledCallCount,
        ImmutableArray<MethodHandle> callTrace,
        ImmutableArray<DebugEvent> events,
        ImmutableArray<EvaluationProvenance> provenance = default,
        ImmutableArray<EvaluationDiagnostic> diagnostics = default)
        where TMemory : IPersistentMemoryState<TMemory>
    {
        ArgumentNullException.ThrowIfNull(plan);
        var accounting = CounterfactualExecutionAccounting.CreateRooted(
            plan,
            instructionStatus,
            instructionUsed,
            instructionRemaining,
            observedLogicalDepthHighWater,
            activeFrameDepthHighWater,
            lineageStatus,
            lineageNodeCount);
        var context = CounterfactualExecutionContext.CreateRooted(
            plan,
            accounting,
            reachedFieldObservations,
            reachedFieldLoadOrdinals,
            modelAttempts,
            modelInvocationCount,
            completedModeledCallCount,
            callTrace,
            events,
            completion);
        return new CounterfactualExecutionResult(
            completion,
            completeness,
            evidence,
            effects,
            value,
            context,
            provenance,
            diagnostics);
    }

    internal static CounterfactualExecutionResult CreateFacadeRejection(
        ImmutableArray<EvaluationDiagnostic> diagnostics) =>
        new(
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Invalid,
            EvaluationEffectStatus.None,
            null,
            CounterfactualExecutionContext.CreateFacadeRejection(),
            [],
            diagnostics);

    internal static CounterfactualExecutionResult CreateStandaloneTargetOutcome(
        CounterfactualTargetOutcomeFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (!CounterfactualExecutionValue.IsCanonicalTargetOutcome(fragment))
        {
            throw new ArgumentException("A canonical certified W4.7 target-outcome fragment is required.", nameof(fragment));
        }

        return new CounterfactualExecutionResult(
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateTargetOutcome(fragment),
            CounterfactualExecutionContext.CreateStandaloneTargetOutcome(fragment),
            [],
            fragment.Diagnostics);
    }

    private static void ValidateOriginAndAxes(
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        CounterfactualExecutionValue? value,
        CounterfactualExecutionContext context,
        ImmutableArray<EvaluationProvenance> provenance,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        switch (context.Origin)
        {
            case CounterfactualExecutionOriginKind.RootedFacade:
                ValidateRooted(completion, completeness, evidence, effects, value, context);
                break;
            case CounterfactualExecutionOriginKind.StandaloneTargetOutcome:
                ValidateStandalone(completion, completeness, evidence, effects, value, context, provenance, diagnostics);
                break;
            case CounterfactualExecutionOriginKind.FacadeRejection:
                ValidateFacadeRejection(completion, completeness, evidence, effects, value, context, provenance);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(context));
        }
    }

    private static void ValidateRooted(
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        CounterfactualExecutionValue? value,
        CounterfactualExecutionContext context)
    {
        var accounting = context.Accounting;
        Require(context.Request is not null && context.PlanSchemaVersion is not null && context.PlanSha256 is not null,
            "A rooted result requires one canonical request and plan identity.");
        var validEvidence = evidence == context.ReachedEvidence ||
            completion == EvaluationCompletionStatus.Invalid &&
            evidence == EvaluationEvidenceStatus.Invalid &&
            context.ReachedEvidence is EvaluationEvidenceStatus.Exact or
                EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable;
        Require(validEvidence,
            "A rooted result's evidence axis must equal reached evidence, except for an explicit invalid-outcome override.");
        Require(accounting.TraversalStatus == CounterfactualBoundStatus.Applied &&
            accounting.DepthStatus is CounterfactualBoundStatus.NotReached or CounterfactualBoundStatus.Applied,
            "A rooted issued plan requires applied traversal and truthful activation-depth accounting.");

        switch (completion)
        {
            case EvaluationCompletionStatus.Completed:
                Require(accounting.InstructionStatus == CounterfactualBoundStatus.Applied &&
                    accounting.InstructionUsed > 0 && accounting.DepthStatus == CounterfactualBoundStatus.Applied &&
                    context.ActiveFrameDepthAtEnd == 0,
                    "Normal completion requires applied nonzero instruction/depth accounting and a fully popped stack.");
                ValidateCompletedValue(completeness, evidence, effects, value, accounting);
                break;
            case EvaluationCompletionStatus.BudgetExhausted:
                Require(effects == EvaluationEffectStatus.None &&
                    evidence is EvaluationEvidenceStatus.Exact or EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable &&
                    accounting.InstructionStatus == CounterfactualBoundStatus.Exhausted,
                    "Instruction-budget exhaustion requires a reached evidence aggregate, no effects, and an exhausted instruction bound.");
                if (accounting.InstructionUsed == 0)
                {
                    Require(completeness == EvaluationCompleteness.None && value is null &&
                        context.ExecutedInstructionCount == 0,
                        "Zero-unit exhaustion carries no execution-prefix value.");
                }
                else
                {
                    Require(completeness == EvaluationCompleteness.Partial &&
                        value?.Kind == CounterfactualExecutionValueKind.ExecutionPrefix,
                        "Positive instruction-budget exhaustion carries exactly one execution-prefix value.");
                }
                break;
            case EvaluationCompletionStatus.Cancelled:
                Require(effects == EvaluationEffectStatus.None &&
                    evidence is EvaluationEvidenceStatus.Exact or EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable,
                    "Cancellation retains only reached non-conflicting evidence and no effects in the closed W4 profile.");
                if (accounting.InstructionStatus == CounterfactualBoundStatus.NotReached)
                {
                    Require(completeness == EvaluationCompleteness.None && value is null &&
                        context.ExecutedInstructionCount == 0,
                        "Cancellation before the first instruction carries no prefix.");
                }
                else
                {
                    Require(accounting.InstructionStatus == CounterfactualBoundStatus.Applied &&
                        accounting.InstructionUsed > 0 && completeness == EvaluationCompleteness.Partial &&
                        value?.Kind == CounterfactualExecutionValueKind.ExecutionPrefix,
                        "Cancellation after execution begins requires applied instruction accounting and a prefix.");
                }
                break;
            case EvaluationCompletionStatus.Blocked:
                Require(completeness == EvaluationCompleteness.None && value is null &&
                    evidence is EvaluationEvidenceStatus.Exact or EvaluationEvidenceStatus.Partial or
                        EvaluationEvidenceStatus.Unavailable or EvaluationEvidenceStatus.Conflict &&
                    effects is EvaluationEffectStatus.None or EvaluationEffectStatus.Unsupported &&
                    accounting.InstructionStatus is CounterfactualBoundStatus.NotReached or CounterfactualBoundStatus.Applied,
                    "A blocked result carries no value, preserves reached evidence, and cannot claim exhausted instructions.");
                break;
            case EvaluationCompletionStatus.Invalid:
                Require(completeness == EvaluationCompleteness.None && value is null &&
                    evidence is EvaluationEvidenceStatus.Exact or EvaluationEvidenceStatus.Partial or
                        EvaluationEvidenceStatus.Unavailable or EvaluationEvidenceStatus.Invalid &&
                    effects == EvaluationEffectStatus.None &&
                    accounting.InstructionStatus is CounterfactualBoundStatus.NotReached or CounterfactualBoundStatus.Applied,
                    "An invalid result carries no value, excludes conflict evidence, and has no effects or exhausted instructions.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(completion));
        }
    }

    private static void ValidateCompletedValue(
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        CounterfactualExecutionValue? value,
        CounterfactualExecutionAccounting accounting)
    {
        Require(effects == EvaluationEffectStatus.None, "A completed exact or unknown W4 return has no effects.");
        switch (value?.Kind)
        {
            case CounterfactualExecutionValueKind.ExactReturn:
                Require(completeness == EvaluationCompleteness.Complete && evidence == EvaluationEvidenceStatus.Exact &&
                    accounting.LineageStatus == CounterfactualBoundStatus.NotReached,
                    "An exact return is complete, exactly evidenced, and requires no lineage materialization.");
                break;
            case CounterfactualExecutionValueKind.UnknownReturn:
                Require(completeness == EvaluationCompleteness.Partial &&
                    evidence is EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable &&
                    accounting.LineageStatus == CounterfactualBoundStatus.Applied &&
                    accounting.LineageNodeCount == value.Lineage!.Nodes.Length,
                    "An unknown return is partial and retains exact applied lineage accounting.");
                break;
            default:
                throw new ArgumentException("A rooted normal completion requires an exact or explained-unknown return value.");
        }
    }

    private static void ValidateStandalone(
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        CounterfactualExecutionValue? value,
        CounterfactualExecutionContext context,
        ImmutableArray<EvaluationProvenance> provenance,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        var fragment = value?.TargetOutcome;
        Require(completion == EvaluationCompletionStatus.Completed &&
            completeness == EvaluationCompleteness.Complete &&
            evidence == EvaluationEvidenceStatus.Exact && effects == EvaluationEffectStatus.None &&
            value?.Kind == CounterfactualExecutionValueKind.TargetException && fragment is not null &&
            CounterfactualExecutionValue.IsCanonicalTargetOutcome(fragment),
            "A standalone result is exactly the complete exact/no-effect target-outcome row.");
        var canonicalFragment = fragment ?? throw new ArgumentException("A standalone target fragment is required.");
        var accounting = context.Accounting;
        Require(context.Request is null && context.PlanSchemaVersion is null && context.PlanSha256 is null &&
            context.RootMethod is null && ReferenceEquals(context.EvidenceContext, EvaluationEvidenceContext.Neutral) &&
            context.InterpretedMethods.IsEmpty && context.ModeledMethods.IsEmpty && context.PlannedFields.IsEmpty &&
            context.CallDispositions.IsEmpty && context.ReachedFieldObservations.IsEmpty &&
            context.ReachedFieldLoadOrdinals.IsEmpty && context.ModelAttempts.IsEmpty &&
            context.ModelInvocationCount == 0 && context.CompletedModeledCallCount == 0 &&
            accounting.InstructionStatus == CounterfactualBoundStatus.Applied &&
            accounting.InstructionLimit == canonicalFragment.InitialInstructionUnits &&
            accounting.InstructionUsed == canonicalFragment.UsedInstructionUnits &&
            accounting.InstructionRemaining == canonicalFragment.RemainingInstructionUnits &&
            accounting.TraversalStatus == CounterfactualBoundStatus.NotApplicable &&
            accounting.DepthStatus == CounterfactualBoundStatus.NotApplicable &&
            accounting.LineageStatus == CounterfactualBoundStatus.NotApplicable &&
            accounting.AllocationStatus == CounterfactualBoundStatus.NotApplicable &&
            context.CallTrace.SequenceEqual(canonicalFragment.CallTrace) &&
            context.Events.SequenceEqual(canonicalFragment.Events) &&
            provenance.IsEmpty && diagnostics.SequenceEqual(canonicalFragment.Diagnostics),
            "Every standalone context, accounting, transcript, and diagnostic fact must derive from the nested fragment.");
    }

    private static void ValidateFacadeRejection(
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        CounterfactualExecutionValue? value,
        CounterfactualExecutionContext context,
        ImmutableArray<EvaluationProvenance> provenance)
    {
        var accounting = context.Accounting;
        Require(completion == EvaluationCompletionStatus.Invalid &&
            completeness == EvaluationCompleteness.None && evidence == EvaluationEvidenceStatus.Invalid &&
            effects == EvaluationEffectStatus.None && value is null && provenance.IsEmpty &&
            context.Request is null && context.PlanSchemaVersion is null && context.PlanSha256 is null &&
            context.RootMethod is null && ReferenceEquals(context.EvidenceContext, EvaluationEvidenceContext.Neutral) &&
            context.InterpretedMethods.IsEmpty && context.ModeledMethods.IsEmpty && context.PlannedFields.IsEmpty &&
            context.CallDispositions.IsEmpty && context.ReachedFieldObservations.IsEmpty &&
            context.ReachedFieldLoadOrdinals.IsEmpty && context.ModelAttempts.IsEmpty && context.CallTrace.IsEmpty &&
            context.Events.IsEmpty && context.ModelInvocationCount == 0 && context.CompletedModeledCallCount == 0 &&
            accounting.InstructionStatus == CounterfactualBoundStatus.NotApplicable &&
            accounting.TraversalStatus == CounterfactualBoundStatus.NotApplicable &&
            accounting.DepthStatus == CounterfactualBoundStatus.NotApplicable &&
            accounting.LineageStatus == CounterfactualBoundStatus.NotApplicable &&
            accounting.AllocationStatus == CounterfactualBoundStatus.NotApplicable,
            "A facade rejection must be a truthful invalid, identity-free, all-not-applicable result.");
    }

    private static ImmutableArray<T> CopyAndRejectNull<T>(ImmutableArray<T> values, string parameterName)
    {
        var copied = CounterfactualCanonical.Copy(values);
        if (copied.Any(static value => value is null))
        {
            throw new ArgumentException("Result projections cannot contain null entries.", parameterName);
        }

        return copied;
    }

    private static void ValidateEnum<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}
