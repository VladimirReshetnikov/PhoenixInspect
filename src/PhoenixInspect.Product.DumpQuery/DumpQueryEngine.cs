using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>
/// Prepares and evaluates one bounded, read-only root-field query against an immutable ClrMD dump session.
/// </summary>
/// <remarks>
/// This is a W2 product slice, not a general expression evaluator. Its grammar is exactly one ordinal,
/// case-sensitive root identifier, <c>.</c>, one instance-field identifier, and optionally <c>??</c> followed by
/// a null, Int32, or bounded string literal. Preparation selects the root and field once into an immutable plan;
/// evaluation decodes that selected field without repeating member binding. Every product query is classified as
/// <see cref="EvaluationSemanticMode.DerivedQuery"/> because it applies host root/member binding over adapter
/// observations. The underlying adapter reads remain <see cref="EvaluationSemanticMode.Observation"/> results.
/// </remarks>
public static class DumpQueryEngine
{
    private const int MaximumObservedStringCharacters = 4096;
    private const string GrammarProvenanceId = "dump-query:grammar-v1";
    private const string CoalesceProvenanceId = "dump-query:null-coalesce-v1";
    private const string RootSelectionProvenanceVersion = "dump-query-root-selection-v1";
    private const string RawMemoryReadBoundName = "dump.memory-read.bytes";

    private static readonly EvaluationDeterministicBound ExpressionLengthBound = new(
        "query.expression.characters",
        DumpQueryParser.MaximumExpressionLength);
    private static readonly EvaluationDeterministicBound RootNameLengthBound = new(
        "query.root-name.characters",
        DumpQueryParser.MaximumIdentifierLength);
    private static readonly EvaluationDeterministicBound FieldNameLengthBound = new(
        "query.field-name.characters",
        DumpQueryParser.MaximumIdentifierLength);
    private static readonly EvaluationDeterministicBound StringLiteralLengthBound = new(
        "query.string-literal.characters",
        DumpQueryParser.MaximumStringLiteralLength);
    private static readonly EvaluationDeterministicBound SyntaxNodeTokenCountBound = new(
        "query.syntax.nodes-plus-tokens",
        CSharpExpressionFrontEnd.MaximumNodeTokenCount);
    private static readonly EvaluationDeterministicBound SyntaxDepthBound = new(
        "query.syntax.depth",
        CSharpExpressionFrontEnd.MaximumSyntaxDepth);
    private static readonly EvaluationDeterministicBound ObservedStringLengthBound = new(
        "query.observed-string.characters",
        MaximumObservedStringCharacters);
    private static readonly ImmutableArray<EvaluationDeterministicBound> EngineBounds =
        ImmutableArray.Create(
            ExpressionLengthBound,
            RootNameLengthBound,
            FieldNameLengthBound,
            StringLiteralLengthBound,
            SyntaxNodeTokenCountBound,
            SyntaxDepthBound,
            ObservedStringLengthBound);

    /// <summary>
    /// Classifies one expression against the exact W2 syntax without binding runtime evidence or opening a dump.
    /// </summary>
    /// <param name="expression">Expression text, including missing or oversized input that requires a stable rejection.</param>
    /// <param name="rootName">The case-sensitive host-selected root identifier expected by the expression.</param>
    /// <returns>
    /// A complete syntax-only admission result carrying a stable diagnostic on rejection and only the deterministic
    /// parser bounds actually reached. Success does not assert that a field exists or that later evidence is exact.
    /// </returns>
    /// <remarks>
    /// This method reuses the W2 parser directly so product facades cannot drift into a second field-expression
    /// grammar. It performs no root validation beyond syntax, no member selection, and no memory read.
    /// </remarks>
    public static DumpQuerySyntaxClassification ClassifySyntax(string? expression, string? rootName)
    {
        var parsed = DumpQueryParser.Parse(expression, rootName);
        return new DumpQuerySyntaxClassification(
            parsed.IsSuccess,
            parsed.DiagnosticCode,
            parsed.DiagnosticMessage,
            ProjectParserBounds(parsed.AppliedBounds));
    }

    internal static ImmutableArray<EvaluationDeterministicBound> ProjectParserBounds(
        DumpQueryParserBounds parserBounds)
    {
        var bounds = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>(EngineBounds.Length);
        AddParserBounds(bounds, parserBounds);
        return bounds.ToImmutable();
    }

    /// <summary>Parses and binds one closed-grammar expression into an immutable object-specific query plan.</summary>
    /// <param name="session">The immutable dump session against which root and member evidence are bound.</param>
    /// <param name="expression">Expression text subject to deterministic syntax and length bounds.</param>
    /// <param name="rootBinding">
    /// Typed host root-selection evidence. Only <see cref="DumpQueryRootBindingStatus.ExactObject"/> can produce a
    /// plan; every other status produces a blocked result that preserves its distinct evidence and applied bounds.
    /// Search-backed bindings additionally retain the exact type-name predicate, adapter status, and traversal counters
    /// in deterministic policy provenance.
    /// </param>
    /// <returns>
    /// A successful immutable plan whose member descriptor was selected once, or a complete invalid/blocked result
    /// explaining the parser, root, member, or type boundary that prevented preparation.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/> or <paramref name="rootBinding"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The root binding contains a duplicate bound or a bound name reserved by the product or dump adapter.
    /// </exception>
    public static DumpQueryPreparationResult Prepare(
        ClrmdDumpSession session,
        string? expression,
        DumpQueryRootBinding rootBinding)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rootBinding);
        ValidateUpstreamBounds(rootBinding.AppliedBounds);

        var parsed = DumpQueryParser.Parse(expression, rootBinding.Name);
        return PrepareCore(session, parsed, rootBinding, expression);
    }

    internal static DumpQueryPreparationResult PrepareParsed(
        ClrmdDumpSession session,
        ParsedExpressionDescriptor expression,
        DumpQueryParserBounds parserBounds,
        DumpQueryRootBinding rootBinding)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(rootBinding);
        ValidateUpstreamBounds(rootBinding.AppliedBounds);
        return PrepareCore(
            session,
            new DumpQueryParseResult(
                expression.ToDumpQuery(),
                DiagnosticCode: null,
                DiagnosticMessage: null,
                parserBounds),
            rootBinding,
            rawExpression: null);
    }

    internal static DumpQueryPreparationResult PrepareStaticSuffix(
        ClrmdDumpSession session,
        StaticFieldExpressionDescriptor descriptor,
        StaticFieldCandidateShape shape,
        DumpQueryRootBinding rootBinding)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(rootBinding);
        if (shape.SuffixShape != StaticFieldSuffixShape.DirectMember ||
            rootBinding.ObjectBinding is null ||
            !descriptor.CandidateShapes.Contains(shape))
        {
            throw new ArgumentException(
                "Static direct-suffix preparation requires the selected descriptor shape and authoritative object binding.",
                nameof(shape));
        }

        var member = descriptor.Segments[shape.StaticFieldSegmentIndex + 1].DecodedIdentifier;
        var literal = shape.FallbackKind switch
        {
            StaticFieldFallbackKind.None => null,
            StaticFieldFallbackKind.Null => new DumpQueryLiteral(DumpQueryLiteralKind.Null, 0, null),
            StaticFieldFallbackKind.Int32 => new DumpQueryLiteral(
                DumpQueryLiteralKind.Int32,
                shape.Int32Fallback!.Value,
                null),
            StaticFieldFallbackKind.String => new DumpQueryLiteral(
                DumpQueryLiteralKind.String,
                0,
                shape.StringFallback!),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        var parsed = new DumpQueryParseResult(
            new ParsedDumpQuery(rootBinding.Name!, member, literal),
            DiagnosticCode: null,
            DiagnosticMessage: null,
            DumpQueryParserBounds.None);
        return PrepareCore(session, parsed, rootBinding, rawExpression: null);
    }

    private static DumpQueryPreparationResult PrepareCore(
        ClrmdDumpSession session,
        DumpQueryParseResult parsed,
        DumpQueryRootBinding rootBinding,
        string? rawExpression)
    {
        var rootEvidenceBelongsToSession = RootEvidenceBelongsToSession(session, rootBinding);
        var rootMemoryReadBoundApplied =
            rootEvidenceBelongsToSession && ReturnedOutcomeReachedRawMemoryRead(rootBinding.Evidence);
        var context = CreateEvidenceContext(
            session,
            rootBinding.Root,
            rootBinding.AppliedBounds,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            parsed.AppliedBounds,
            rawMemoryReadBoundApplied: false,
            observedStringBoundApplied: false);
        if (!parsed.IsSuccess)
        {
            var parseProvenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
            parseProvenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.Policy,
                GrammarProvenanceId));
            if (TryCreateRawRequestProvenanceId(rawExpression, rootBinding.Name, out var rawRequestProvenanceId))
            {
                parseProvenance.Add(new EvaluationProvenance(
                    EvaluationProvenanceKind.Policy,
                    rawRequestProvenanceId));
            }

            return DumpQueryPreparationResult.Failed(CreateResult(
                context,
                EvaluationCompletionStatus.Invalid,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Exact,
                null,
                parseProvenance.ToImmutable(),
                ImmutableArray.Create(new EvaluationDiagnostic(
                    parsed.DiagnosticCode!,
                    parsed.DiagnosticMessage!))));
        }

        var query = parsed.Query!;
        context = CreateEvidenceContext(
            session,
            rootBinding.Root,
            rootBinding.AppliedBounds,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            parsed.AppliedBounds,
            rawMemoryReadBoundApplied: rootMemoryReadBoundApplied,
            observedStringBoundApplied: false);
        var provenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
        if (rootEvidenceBelongsToSession)
        {
            AppendMemoryProvenance(provenance, rootBinding.Evidence);
        }

        AppendRootSelectionProvenance(provenance, rootBinding);

        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            CreateParsedRequestProvenanceId(query)));

        if (rootBinding.Snapshot != session.Snapshot)
        {
            return DumpQueryPreparationResult.Failed(CreateResult(
                context,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Conflict,
                null,
                provenance.ToImmutable(),
                ImmutableArray.Create(new EvaluationDiagnostic(
                    "DUMP_SNAPSHOT_MISMATCH",
                    "The root binding belongs to a different immutable dump snapshot."))));
        }

        if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || rootBinding.Root is null)
        {
            return DumpQueryPreparationResult.Failed(CreateRootBindingFailure(context, rootBinding, provenance));
        }

        var root = rootBinding.Root;
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.RuntimeStructure,
            root.Snapshot.MemorySourceId,
            root.Address));

        var fieldResult = session.GetInstanceField(root, query.FieldName);
        context = CreateEvidenceContext(
            session,
            root,
            rootBinding.AppliedBounds,
            fieldResult.AppliedBounds,
            parsed.AppliedBounds,
            rawMemoryReadBoundApplied: rootMemoryReadBoundApplied,
            observedStringBoundApplied: false);
        if (fieldResult.Status != ClrmdEvidenceStatus.Exact)
        {
            var observation = fieldResult.ToObservationResult();
            AppendProvenance(provenance, observation.Provenance);
            return DumpQueryPreparationResult.Failed(CreateResult(
                context,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                observation.Evidence,
                null,
                provenance.ToImmutable(),
                observation.Diagnostics));
        }

        var field = fieldResult.Value!;
        provenance.Add(CreateFieldProvenance(root, field));
        var fieldKind = ClassifyField(field);
        if (fieldKind is null)
        {
            return DumpQueryPreparationResult.Failed(CreateResult(
                context,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Exact,
                null,
                provenance.ToImmutable(),
                ImmutableArray.Create(new EvaluationDiagnostic(
                    "QUERY_FIELD_TYPE_UNSUPPORTED",
                    "The selected field type is outside the supported Int32, nullable Int32, and string query domain."))));
        }

        if (!CoalesceIsCompatible(fieldKind.Value, query.CoalesceLiteral))
        {
            return DumpQueryPreparationResult.Failed(InvalidCoalesceType(context, provenance));
        }

        var plan = new DumpQueryPlan(
            rootBinding,
            field,
            fieldKind.Value,
            query.CoalesceLiteral,
            parsed.AppliedBounds,
            fieldResult.AppliedBounds);
        return DumpQueryPreparationResult.Success(plan);
    }

    /// <summary>Evaluates an already prepared plan without repeating root or member selection.</summary>
    /// <param name="session">The immutable dump session that must contain the plan's bound object and field.</param>
    /// <param name="plan">The exact object-specific plan returned by <see cref="Prepare"/>.</param>
    /// <returns>
    /// A multi-axis derived-query result. Every outcome includes the plan fingerprint and, for a search-backed root,
    /// the canonical selection predicate/status/counters as policy provenance; exact null, partial or missing evidence,
    /// and decoded scalar/string answers remain distinct.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/> or <paramref name="plan"/> is <see langword="null"/>.
    /// </exception>
    public static EvaluationResult<DumpQueryValue> Evaluate(ClrmdDumpSession session, DumpQueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(plan);

        var root = plan.RootBinding.Root!;
        var rootEvidenceBelongsToSession = RootEvidenceBelongsToSession(session, plan.RootBinding);
        var rootMemoryReadBoundApplied =
            rootEvidenceBelongsToSession && ReturnedOutcomeReachedRawMemoryRead(plan.RootBinding.Evidence);
        var provenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
        if (rootEvidenceBelongsToSession)
        {
            AppendMemoryProvenance(provenance, plan.RootBinding.Evidence);
        }

        AppendRootSelectionProvenance(provenance, plan.RootBinding);

        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.RuntimeStructure,
            root.Snapshot.MemorySourceId,
            root.Address));
        provenance.Add(CreateFieldProvenance(root, plan.Field));
        provenance.Add(new EvaluationProvenance(EvaluationProvenanceKind.Policy, plan.ProvenanceId));

        return plan.FieldKind switch
        {
            DumpQueryPlanFieldKind.Int32 => EvaluateInt32(
                session,
                plan,
                rootMemoryReadBoundApplied,
                provenance),
            DumpQueryPlanFieldKind.NullableInt32 => EvaluateNullableInt32(
                session,
                plan,
                rootMemoryReadBoundApplied,
                provenance),
            DumpQueryPlanFieldKind.String => EvaluateString(
                session,
                plan,
                rootMemoryReadBoundApplied,
                provenance),
            _ => throw new InvalidOperationException("The bound dump-query field kind is invalid."),
        };
    }

    /// <summary>Evaluates one closed-grammar expression over a caller-selected dump root.</summary>
    /// <param name="session">The immutable dump session from which <paramref name="root"/> was selected.</param>
    /// <param name="expression">Expression text subject to deterministic syntax and length bounds.</param>
    /// <param name="rootName">The exact case-sensitive identifier assigned to the supplied root.</param>
    /// <param name="root">
    /// The already selected root object, or <see langword="null"/> when no exact root is available. New callers should
    /// prefer <see cref="DumpQueryRootBinding.FromSearchResult"/> so absence and non-exact evidence remain distinct.
    /// </param>
    /// <param name="upstreamBounds">Bounds actually applied before this operation; default means none are claimed.</param>
    /// <returns>The preparation failure or evaluated immutable plan result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="upstreamBounds"/> contains a null, duplicate, or reserved bound name.
    /// </exception>
    public static EvaluationResult<DumpQueryValue> Evaluate(
        ClrmdDumpSession session,
        string? expression,
        string? rootName,
        ClrmdHeapObjectInfo? root,
        ImmutableArray<EvaluationDeterministicBound> upstreamBounds = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateUpstreamBounds(upstreamBounds);
        var binding = root is null
            ? DumpQueryRootBinding.CreateUnavailable(rootName, session.Snapshot)
            : DumpQueryRootBinding.FromExactObject(rootName, root, upstreamBounds);
        var preparation = Prepare(session, expression, binding);
        return preparation.IsSuccess
            ? Evaluate(session, preparation.Plan!)
            : preparation.Failure!;
    }

    private static EvaluationResult<DumpQueryValue> EvaluateInt32(
        ClrmdDumpSession session,
        DumpQueryPlan plan,
        bool rootMemoryReadBoundApplied,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var root = plan.RootBinding.Root!;
        var fieldRead = session.ReadInt32Field(root, plan.Field);
        var context = CreatePlanContext(
            session,
            plan,
            rootMemoryReadBoundApplied || ReturnedOutcomeReachedRawMemoryRead(fieldRead.Evidence),
            observedStringBoundApplied: false);
        var observation = fieldRead.ToObservationResult();
        AppendProvenance(provenance, observation.Provenance);
        if (fieldRead.Status == ClrmdEvidenceStatus.Exact && fieldRead.Value?.Value is int value)
        {
            return CreateResult(
                context,
                EvaluationCompletionStatus.Completed,
                EvaluationCompleteness.Complete,
                EvaluationEvidenceStatus.Exact,
                DumpQueryValue.FromInt32(value),
                provenance.ToImmutable(),
                observation.Diagnostics);
        }

        return CreateResult(
            context,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            observation.Evidence,
            null,
            provenance.ToImmutable(),
            observation.Diagnostics);
    }

    private static EvaluationResult<DumpQueryValue> EvaluateNullableInt32(
        ClrmdDumpSession session,
        DumpQueryPlan plan,
        bool rootMemoryReadBoundApplied,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var root = plan.RootBinding.Root!;
        var fieldRead = session.ReadNullableInt32Field(root, plan.Field);
        var context = CreatePlanContext(
            session,
            plan,
            rootMemoryReadBoundApplied || ReturnedOutcomeReachedRawMemoryRead(fieldRead.Evidence),
            observedStringBoundApplied: false);
        var observation = fieldRead.ToObservationResult();
        AppendProvenance(provenance, observation.Provenance);
        if (fieldRead.Status != ClrmdEvidenceStatus.Exact || fieldRead.Value is not { } value)
        {
            return CreateResult(
                context,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                observation.Evidence,
                null,
                provenance.ToImmutable(),
                observation.Diagnostics);
        }

        DumpQueryValue result;
        if (value.IsNull)
        {
            result = plan.CoalesceLiteral switch
            {
                { Kind: DumpQueryLiteralKind.Int32 } literal => DumpQueryValue.FromInt32(literal.Int32Value),
                _ => DumpQueryValue.FromNull(),
            };
        }
        else if (value.Value is int decoded)
        {
            result = DumpQueryValue.FromInt32(decoded);
        }
        else
        {
            return CreateResult(
                context,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Invalid,
                null,
                provenance.ToImmutable(),
                ImmutableArray.Create(new EvaluationDiagnostic(
                    "QUERY_NULLABLE_INT32_INVALID",
                    "The exact nullable Int32 observation did not contain a supported null or scalar state.")));
        }

        AppendCoalesceProvenance(provenance, plan);
        return CreateResult(
            context,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            result,
            provenance.ToImmutable(),
            observation.Diagnostics);
    }

    private static EvaluationResult<DumpQueryValue> EvaluateString(
        ClrmdDumpSession session,
        DumpQueryPlan plan,
        bool rootMemoryReadBoundApplied,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var root = plan.RootBinding.Root!;
        var fieldRead = session.ReadStringField(root, plan.Field, MaximumObservedStringCharacters);
        var context = CreatePlanContext(
            session,
            plan,
            rootMemoryReadBoundApplied || ReturnedOutcomeReachedRawMemoryRead(fieldRead.Evidence),
            observedStringBoundApplied: fieldRead.TargetLength is >= 0);
        var observation = fieldRead.ToObservationResult();
        AppendProvenance(provenance, observation.Provenance);

        if (fieldRead.Status == ClrmdEvidenceStatus.Exact)
        {
            DumpQueryValue value;
            if (fieldRead.IsNull && plan.CoalesceLiteral is { Kind: DumpQueryLiteralKind.String } literal)
            {
                value = DumpQueryValue.FromString(literal.StringValue!);
            }
            else if (fieldRead.IsNull)
            {
                value = DumpQueryValue.FromNull();
            }
            else
            {
                value = DumpQueryValue.FromString(fieldRead.Value!);
            }

            AppendCoalesceProvenance(provenance, plan);
            return CreateResult(
                context,
                EvaluationCompletionStatus.Completed,
                EvaluationCompleteness.Complete,
                EvaluationEvidenceStatus.Exact,
                value,
                provenance.ToImmutable(),
                observation.Diagnostics);
        }

        if (fieldRead.Status == ClrmdEvidenceStatus.Partial && fieldRead.Value is not null)
        {
            return CreateResult(
                context,
                fieldRead.Issue == ClrmdValueIssue.LimitExceeded
                    ? EvaluationCompletionStatus.Completed
                    : EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.Partial,
                EvaluationEvidenceStatus.Partial,
                DumpQueryValue.FromString(fieldRead.Value),
                provenance.ToImmutable(),
                observation.Diagnostics);
        }

        return CreateResult(
            context,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            observation.Evidence,
            null,
            provenance.ToImmutable(),
            observation.Diagnostics);
    }

    private static EvaluationResult<DumpQueryValue> CreateRootBindingFailure(
        EvaluationEvidenceContext context,
        DumpQueryRootBinding binding,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var (completion, evidence, code, message) = binding.Status switch
        {
            DumpQueryRootBindingStatus.ExhaustiveAbsence => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Unavailable,
                "QUERY_ROOT_ABSENT",
                "An exhaustive root search found no matching object."),
            DumpQueryRootBindingStatus.Partial when binding.Issue == ClrmdValueIssue.LimitExceeded => (
                EvaluationCompletionStatus.BudgetExhausted,
                EvaluationEvidenceStatus.Partial,
                "QUERY_ROOT_LIMIT_EXCEEDED",
                "A deterministic root-search bound was exhausted before unique selection was proven."),
            DumpQueryRootBindingStatus.Partial when binding.Issue == ClrmdValueIssue.MemoryUnavailable => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Partial,
                "QUERY_ROOT_MEMORY_PARTIAL",
                "Incomplete dump-memory evidence prevented unique root selection."),
            DumpQueryRootBindingStatus.Partial when binding.Issue == ClrmdValueIssue.ModuleUnavailable => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Partial,
                "QUERY_ROOT_MODULE_PARTIAL",
                "Incomplete runtime-module evidence prevented unique root selection."),
            DumpQueryRootBindingStatus.Partial => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Partial,
                "QUERY_ROOT_PARTIAL",
                "Root selection ended with incomplete evidence and cannot choose a unique object."),
            DumpQueryRootBindingStatus.Unavailable when binding.Issue == ClrmdValueIssue.RuntimeUnsupported => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Unavailable,
                "QUERY_ROOT_RUNTIME_UNAVAILABLE",
                "The dump runtime cannot provide a root in the supported query profile."),
            DumpQueryRootBindingStatus.Unavailable => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Unavailable,
                "QUERY_ROOT_UNAVAILABLE",
                "No exact root object is available for the dump query."),
            DumpQueryRootBindingStatus.Conflict when binding.Issue == ClrmdValueIssue.AmbiguousMatch => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Conflict,
                "QUERY_ROOT_AMBIGUOUS",
                "More than one root matched a query that requires unique selection."),
            DumpQueryRootBindingStatus.Conflict when binding.Issue == ClrmdValueIssue.TypeMismatch => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Conflict,
                "QUERY_ROOT_TYPE_CONFLICT",
                "Root-selection evidence conflicts with the requested runtime type."),
            DumpQueryRootBindingStatus.Conflict => (
                EvaluationCompletionStatus.Blocked,
                EvaluationEvidenceStatus.Conflict,
                "QUERY_ROOT_CONFLICT",
                "Root selection was ambiguous or incompatible with the requested query context."),
            DumpQueryRootBindingStatus.Invalid => (
                EvaluationCompletionStatus.Invalid,
                EvaluationEvidenceStatus.Invalid,
                "QUERY_ROOT_INVALID",
                "Captured root-selection evidence violates a supported runtime invariant."),
            _ => throw new InvalidOperationException("An exact root binding cannot produce a binding failure."),
        };
        return CreateResult(
            context,
            completion,
            EvaluationCompleteness.None,
            evidence,
            null,
            provenance.ToImmutable(),
            ImmutableArray.Create(new EvaluationDiagnostic(code, message)));
    }

    private static DumpQueryPlanFieldKind? ClassifyField(ClrmdInstanceFieldInfo field)
    {
        if (field.IsNullableInt32)
        {
            return DumpQueryPlanFieldKind.NullableInt32;
        }

        if (string.Equals(field.ElementType, "Int32", StringComparison.Ordinal))
        {
            return DumpQueryPlanFieldKind.Int32;
        }

        return string.Equals(field.ElementType, "String", StringComparison.Ordinal)
            ? DumpQueryPlanFieldKind.String
            : null;
    }

    private static bool CoalesceIsCompatible(
        DumpQueryPlanFieldKind fieldKind,
        DumpQueryLiteral? literal) => literal is null || fieldKind switch
    {
        DumpQueryPlanFieldKind.Int32 => false,
        DumpQueryPlanFieldKind.NullableInt32 =>
            literal.Kind is DumpQueryLiteralKind.Int32 or DumpQueryLiteralKind.Null,
        DumpQueryPlanFieldKind.String =>
            literal.Kind is DumpQueryLiteralKind.String or DumpQueryLiteralKind.Null,
        _ => false,
    };

    private static EvaluationResult<DumpQueryValue> InvalidCoalesceType(
        EvaluationEvidenceContext context,
        ImmutableArray<EvaluationProvenance>.Builder provenance) =>
        CreateResult(
            context,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            null,
            provenance.ToImmutable(),
            ImmutableArray.Create(new EvaluationDiagnostic(
                "QUERY_COALESCE_TYPE_UNSUPPORTED",
                "The null-coalescing literal is incompatible with the selected field type.")));

    private static void AppendCoalesceProvenance(
        ImmutableArray<EvaluationProvenance>.Builder provenance,
        DumpQueryPlan plan)
    {
        if (plan.HasCoalesce)
        {
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.Transformation,
                CoalesceProvenanceId));
        }
    }

    private static EvaluationResult<DumpQueryValue> CreateResult(
        EvaluationEvidenceContext context,
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness,
        EvaluationEvidenceStatus evidence,
        DumpQueryValue? value,
        ImmutableArray<EvaluationProvenance> provenance,
        ImmutableArray<EvaluationDiagnostic> diagnostics) =>
        EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            completion,
            completeness,
            evidence,
            EvaluationEffectStatus.None,
            value,
            context,
            provenance,
            diagnostics);

    private static EvaluationEvidenceContext CreatePlanContext(
        ClrmdDumpSession session,
        DumpQueryPlan plan,
        bool rawMemoryReadBoundApplied,
        bool observedStringBoundApplied) =>
        CreateEvidenceContext(
            session,
            plan.RootBinding.Root,
            plan.RootBinding.AppliedBounds,
            plan.FieldSelectionBounds,
            plan.ParserBounds,
            rawMemoryReadBoundApplied,
            observedStringBoundApplied);

    private static EvaluationEvidenceContext CreateEvidenceContext(
        ClrmdDumpSession session,
        ClrmdHeapObjectInfo? root,
        ImmutableArray<EvaluationDeterministicBound> upstreamBounds,
        ImmutableArray<EvaluationDeterministicBound> adapterBounds,
        DumpQueryParserBounds parserBounds,
        bool rawMemoryReadBoundApplied,
        bool observedStringBoundApplied)
    {
        var bounds = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>(
            EngineBounds.Length +
            1 +
            (upstreamBounds.IsDefault ? 0 : upstreamBounds.Length) +
            (adapterBounds.IsDefault ? 0 : adapterBounds.Length));
        if (!upstreamBounds.IsDefault)
        {
            bounds.AddRange(upstreamBounds);
        }

        if (!adapterBounds.IsDefault)
        {
            bounds.AddRange(adapterBounds);
        }

        AddParserBounds(bounds, parserBounds);
        if (rawMemoryReadBoundApplied)
        {
            bounds.Add(new EvaluationDeterministicBound(
                RawMemoryReadBoundName,
                session.Memory.MaximumReadLength));
        }

        if (observedStringBoundApplied)
        {
            bounds.Add(ObservedStringLengthBound);
        }

        var module = root is not null &&
            root.Snapshot == session.Snapshot &&
            root.Module.Identity.Snapshot == session.Snapshot
                ? EvaluationEvidenceIdentity.CreateAvailable(root.Module.Identity.SourceId)
                : EvaluationEvidenceIdentity.Unavailable;
        return EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            EvaluationEvidenceIdentity.CreateAvailable(session.Snapshot.MemorySourceId),
            module,
            EvaluationFallback.None,
            bounds.ToImmutable());
    }

    private static void AddParserBounds(
        ImmutableArray<EvaluationDeterministicBound>.Builder bounds,
        DumpQueryParserBounds parserBounds)
    {
        if ((parserBounds & DumpQueryParserBounds.ExpressionLength) != 0)
        {
            bounds.Add(ExpressionLengthBound);
        }

        if ((parserBounds & DumpQueryParserBounds.RootNameLength) != 0)
        {
            bounds.Add(RootNameLengthBound);
        }

        if ((parserBounds & DumpQueryParserBounds.FieldNameLength) != 0)
        {
            bounds.Add(FieldNameLengthBound);
        }

        if ((parserBounds & DumpQueryParserBounds.StringLiteralLength) != 0)
        {
            bounds.Add(StringLiteralLengthBound);
        }

        if ((parserBounds & DumpQueryParserBounds.SyntaxNodeTokenCount) != 0)
        {
            bounds.Add(SyntaxNodeTokenCountBound);
        }

        if ((parserBounds & DumpQueryParserBounds.SyntaxDepth) != 0)
        {
            bounds.Add(SyntaxDepthBound);
        }
    }

    private static void ValidateUpstreamBounds(
        ImmutableArray<EvaluationDeterministicBound> upstreamBounds)
    {
        if (upstreamBounds.IsDefault)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bound in upstreamBounds)
        {
            if (bound is null)
            {
                throw new ArgumentException(
                    "Upstream deterministic bounds cannot contain null entries.",
                    nameof(upstreamBounds));
            }

            if (string.Equals(
                    bound.Name,
                    ClrmdDumpSession.InstanceFieldTraversalBound.Name,
                    StringComparison.Ordinal) ||
                string.Equals(bound.Name, RawMemoryReadBoundName, StringComparison.Ordinal) ||
                EngineBounds.Any(engineBound =>
                    string.Equals(engineBound.Name, bound.Name, StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    $"The upstream deterministic bound name '{bound.Name}' is reserved by the dump-query operation.",
                    nameof(upstreamBounds));
            }

            if (!names.Add(bound.Name))
            {
                throw new ArgumentException(
                    $"The upstream deterministic bound name '{bound.Name}' occurs more than once.",
                    nameof(upstreamBounds));
            }
        }
    }

    private static string CreateParsedRequestProvenanceId(ParsedDumpQuery query)
    {
        var builder = new StringBuilder();
        AppendCanonicalString(builder, "dump-query-request-v1");
        AppendCanonicalString(builder, query.RootName);
        AppendCanonicalString(builder, query.FieldName);
        if (query.CoalesceLiteral is null)
        {
            AppendCanonicalString(builder, "none");
        }
        else
        {
            AppendCanonicalString(builder, query.CoalesceLiteral.Kind.ToString());
            AppendCanonicalString(builder, query.CoalesceLiteral.Kind switch
            {
                DumpQueryLiteralKind.Null => string.Empty,
                DumpQueryLiteralKind.Int32 =>
                    query.CoalesceLiteral.Int32Value.ToString(CultureInfo.InvariantCulture),
                DumpQueryLiteralKind.String => query.CoalesceLiteral.StringValue!,
                _ => throw new InvalidOperationException("The parsed literal kind is invalid."),
            });
        }

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
        return $"dump-query-request:sha256:{digest}";
    }

    private static bool TryCreateRawRequestProvenanceId(
        string? expression,
        string? rootName,
        out string provenanceId)
    {
        if (expression?.Length > DumpQueryParser.MaximumExpressionLength ||
            rootName?.Length > DumpQueryParser.MaximumIdentifierLength)
        {
            provenanceId = string.Empty;
            return false;
        }

        var builder = new StringBuilder();
        AppendCanonicalString(builder, "dump-query-input-v1");
        AppendCanonicalString(builder, expression is null ? "null" : "value");
        AppendCanonicalString(builder, expression ?? string.Empty);
        AppendCanonicalString(builder, rootName is null ? "null" : "value");
        AppendCanonicalString(builder, rootName ?? string.Empty);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
        provenanceId = $"dump-query-input:sha256:{digest}";
        return true;
    }

    private static void AppendCanonicalString(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }

    internal static EvaluationProvenance? CreateRootSelectionProvenance(DumpQueryRootBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.ObjectBinding is { } objectBinding)
        {
            return new EvaluationProvenance(
                EvaluationProvenanceKind.Policy,
                $"dump-object-binding:sha256:{objectBinding.Sha256}");
        }
        if (binding.TypeNameSelector is null)
        {
            return null;
        }

        if (binding.SearchStatus is null ||
            binding.HandlesScanned is null ||
            binding.MaximumHandlesScanned is null ||
            binding.MaximumMatches is null ||
            binding.MatchesRetained is null ||
            binding.MatchLimitReached is null)
        {
            throw new InvalidOperationException(
                "A search-backed root binding must retain the complete selector status and traversal counters.");
        }

        var builder = new StringBuilder();
        AppendCanonicalString(builder, RootSelectionProvenanceVersion);
        AppendCanonicalString(builder, binding.TypeNameSelector);
        AppendCanonicalString(builder, ((int)binding.SearchStatus.Value).ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, ((int)binding.Status).ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, ((int)binding.Issue).ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, binding.HandlesScanned.Value.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, binding.MaximumHandlesScanned.Value.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, binding.MaximumMatches.Value.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, binding.MatchesRetained.Value.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalString(builder, binding.MatchLimitReached.Value ? "1" : "0");
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
        return new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"dump-query-root-selection:sha256:{digest}");
    }

    private static void AppendRootSelectionProvenance(
        ImmutableArray<EvaluationProvenance>.Builder builder,
        DumpQueryRootBinding binding)
    {
        if (CreateRootSelectionProvenance(binding) is { } provenance)
        {
            builder.Add(provenance);
        }
    }

    private static EvaluationProvenance CreateFieldProvenance(
        ClrmdHeapObjectInfo root,
        ClrmdInstanceFieldInfo field) => new(
            EvaluationProvenanceKind.RuntimeStructure,
            root.Snapshot.MemorySourceId,
            field.Address,
            field.Size,
            field.Size);

    private static bool ReturnedOutcomeReachedRawMemoryRead(
        ImmutableArray<PhoenixInspect.Host.Abstractions.MemoryReadResult> evidence) => !evidence.IsEmpty;

    private static bool RootEvidenceBelongsToSession(
        ClrmdDumpSession session,
        DumpQueryRootBinding binding)
    {
        if (binding.Snapshot != session.Snapshot)
        {
            return false;
        }

        if (binding.Root is { } root &&
            (root.Snapshot != session.Snapshot || root.Module.Identity.Snapshot != session.Snapshot))
        {
            return false;
        }

        return binding.Evidence.All(read =>
            string.Equals(read.SourceId, session.Memory.SourceId, StringComparison.Ordinal));
    }

    private static void AppendMemoryProvenance(
        ImmutableArray<EvaluationProvenance>.Builder builder,
        ImmutableArray<PhoenixInspect.Host.Abstractions.MemoryReadResult> evidence)
    {
        foreach (var read in evidence)
        {
            builder.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.BytesRead));
        }
    }

    private static void AppendProvenance(
        ImmutableArray<EvaluationProvenance>.Builder builder,
        ImmutableArray<EvaluationProvenance> evidence) => builder.AddRange(evidence);
}
