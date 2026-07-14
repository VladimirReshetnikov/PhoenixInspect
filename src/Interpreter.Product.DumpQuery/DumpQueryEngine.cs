using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>
/// Evaluates one bounded, read-only root-field query against an immutable ClrMD dump session.
/// </summary>
/// <remarks>
/// This is a draft W2 product slice, not a general expression evaluator. Its grammar is exactly one ordinal,
/// case-sensitive root identifier, <c>.</c>, one instance-field identifier, and optionally <c>??</c> followed by
/// a null, Int32, or bounded string literal. An exact null may be read from the selected field; null-conditional
/// root access is not admitted because this slice requires an exact non-null root object. It performs no user-code
/// execution, calls, indexers, arithmetic, assignments, or chained traversal.
/// </remarks>
public static class DumpQueryEngine
{
    private const int MaximumObservedStringCharacters = 4096;
    private const string GrammarProvenanceId = "dump-query:grammar-v1";
    private const string CoalesceProvenanceId = "dump-query:null-coalesce-v1";
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
    private static readonly EvaluationDeterministicBound ObservedStringLengthBound = new(
        "query.observed-string.characters",
        MaximumObservedStringCharacters);
    private static readonly ImmutableArray<EvaluationDeterministicBound> EngineBounds =
        ImmutableArray.Create(
            ExpressionLengthBound,
            RootNameLengthBound,
            FieldNameLengthBound,
            StringLiteralLengthBound,
            ObservedStringLengthBound);

    /// <summary>Evaluates one closed-grammar expression over a caller-selected dump root.</summary>
    /// <param name="session">The immutable dump session from which <paramref name="root"/> was selected.</param>
    /// <param name="expression">Untrusted expression text subject to deterministic syntax and length bounds.</param>
    /// <param name="rootName">The exact case-sensitive identifier assigned to the supplied root.</param>
    /// <param name="root">
    /// The already selected root object, or <see langword="null"/> when root selection produced no exact object.
    /// Missing root evidence remains unavailable and is never reinterpreted as a null target value.
    /// </param>
    /// <param name="upstreamBounds">
    /// Optional immutable bounds that the caller actually enforced before this operation, such as strong-handle scan
    /// and retained-match caps used to select <paramref name="root"/>. The engine adds only parser, identifier,
    /// literal, observed-string, and raw-read bounds whose guarded operation this execution path actually reaches.
    /// Callers must not report intended or unenforced policies; a default array means no upstream bound is claimed.
    /// </param>
    /// <returns>
    /// A multi-axis derived-query result. Exact null, partial or missing evidence, unsupported field types, and
    /// invalid syntax remain distinct outcomes with ordered provenance and stable secret-safe diagnostics.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="upstreamBounds"/> contains a null entry, a duplicate name, or a name reserved by an
    /// engine- or adapter-applied bound.
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
        var parsed = DumpQueryParser.Parse(expression, rootName);
        var context = CreateEvidenceContext(
            session,
            root,
            upstreamBounds,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            parsed.AppliedBounds,
            rawMemoryReadBoundApplied: false,
            observedStringBoundApplied: false);
        if (!parsed.IsSuccess)
        {
            return CreateResult(
                context,
                EvaluationCompletionStatus.Invalid,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Exact,
                null,
                ImmutableArray.Create(new EvaluationProvenance(
                    EvaluationProvenanceKind.Policy,
                    GrammarProvenanceId)),
                ImmutableArray.Create(new EvaluationDiagnostic(
                    parsed.DiagnosticCode!,
                    parsed.DiagnosticMessage!)));
        }

        if (root is null)
        {
            return CreateResult(
                context,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Unavailable,
                null,
                ImmutableArray<EvaluationProvenance>.Empty,
                ImmutableArray.Create(new EvaluationDiagnostic(
                    "QUERY_ROOT_UNAVAILABLE",
                    "No exact root object is available for the dump query.")));
        }

        var query = parsed.Query!;
        var provenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
        var rootEvidenceBelongsToSession = RootEvidenceBelongsToSession(session, root);
        if (rootEvidenceBelongsToSession)
        {
            AppendMemoryProvenance(provenance, root.Evidence);
        }

        var rootMemoryReadBoundApplied =
            rootEvidenceBelongsToSession && ReturnedOutcomeReachedRawMemoryRead(root.Evidence);
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.RuntimeStructure,
            root.Snapshot.MemorySourceId,
            root.Address));

        var fieldResult = session.GetInstanceField(root, query.FieldName);
        context = CreateEvidenceContext(
            session,
            root,
            upstreamBounds,
            fieldResult.AppliedBounds,
            parsed.AppliedBounds,
            rawMemoryReadBoundApplied: rootMemoryReadBoundApplied,
            observedStringBoundApplied: false);
        if (fieldResult.Status != ClrmdEvidenceStatus.Exact)
        {
            var observation = fieldResult.ToObservationResult();
            AppendProvenance(provenance, observation.Provenance);
            return CreateResult(
                context,
                EvaluationCompletionStatus.Blocked,
                EvaluationCompleteness.None,
                observation.Evidence,
                null,
                provenance.ToImmutable(),
                observation.Diagnostics);
        }

        var field = fieldResult.Value!;
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.RuntimeStructure,
            root.Snapshot.MemorySourceId,
            field.Address,
            field.Size,
            field.Size));

        if (string.Equals(field.ElementType, "Int32", StringComparison.Ordinal))
        {
            if (query.CoalesceLiteral is not null)
            {
                return InvalidCoalesceType(context, provenance);
            }

            return EvaluateInt32(
                session,
                root,
                query.FieldName,
                upstreamBounds,
                fieldResult.AppliedBounds,
                parsed.AppliedBounds,
                rootMemoryReadBoundApplied,
                provenance);
        }

        if (string.Equals(field.ElementType, "String", StringComparison.Ordinal))
        {
            if (query.CoalesceLiteral is { Kind: not (DumpQueryLiteralKind.String or DumpQueryLiteralKind.Null) })
            {
                return InvalidCoalesceType(context, provenance);
            }

            return EvaluateString(
                session,
                root,
                query,
                upstreamBounds,
                fieldResult.AppliedBounds,
                parsed.AppliedBounds,
                rootMemoryReadBoundApplied,
                provenance);
        }

        return CreateResult(
            context,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            null,
            provenance.ToImmutable(),
            ImmutableArray.Create(new EvaluationDiagnostic(
                "QUERY_FIELD_TYPE_UNSUPPORTED",
                "The selected field type is outside the supported Int32 and string query domain.")));
    }

    private static EvaluationResult<DumpQueryValue> EvaluateInt32(
        ClrmdDumpSession session,
        ClrmdHeapObjectInfo root,
        string fieldName,
        ImmutableArray<EvaluationDeterministicBound> upstreamBounds,
        ImmutableArray<EvaluationDeterministicBound> adapterBounds,
        DumpQueryParserBounds parserBounds,
        bool rootMemoryReadBoundApplied,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var fieldRead = session.ReadInt32Field(root, fieldName);
        var context = CreateEvidenceContext(
            session,
            root,
            upstreamBounds,
            adapterBounds,
            parserBounds,
            rawMemoryReadBoundApplied:
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

    private static EvaluationResult<DumpQueryValue> EvaluateString(
        ClrmdDumpSession session,
        ClrmdHeapObjectInfo root,
        ParsedDumpQuery query,
        ImmutableArray<EvaluationDeterministicBound> upstreamBounds,
        ImmutableArray<EvaluationDeterministicBound> adapterBounds,
        DumpQueryParserBounds parserBounds,
        bool rootMemoryReadBoundApplied,
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var fieldRead = session.ReadStringField(root, query.FieldName, MaximumObservedStringCharacters);
        var context = CreateEvidenceContext(
            session,
            root,
            upstreamBounds,
            adapterBounds,
            parserBounds,
            rawMemoryReadBoundApplied:
                rootMemoryReadBoundApplied || ReturnedOutcomeReachedRawMemoryRead(fieldRead.Evidence),
            observedStringBoundApplied: fieldRead.TargetLength is >= 0);
        var observation = fieldRead.ToObservationResult();
        AppendProvenance(provenance, observation.Provenance);

        if (fieldRead.Status == ClrmdEvidenceStatus.Exact)
        {
            DumpQueryValue value;
            if (fieldRead.IsNull && query.CoalesceLiteral is { } literal)
            {
                value = literal.Kind == DumpQueryLiteralKind.Null
                    ? DumpQueryValue.FromNull()
                    : DumpQueryValue.FromString(literal.StringValue!);
            }
            else if (fieldRead.IsNull)
            {
                value = DumpQueryValue.FromNull();
            }
            else
            {
                value = DumpQueryValue.FromString(fieldRead.Value!);
            }

            if (query.CoalesceLiteral is not null)
            {
                provenance.Add(new EvaluationProvenance(
                    EvaluationProvenanceKind.Transformation,
                    CoalesceProvenanceId));
            }

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

    private static bool ReturnedOutcomeReachedRawMemoryRead(
        ImmutableArray<Interpreter.Host.Abstractions.MemoryReadResult> evidence)
    {
        // ClrmdProcessMemoryReader turns every expected backend read failure into a MemoryReadResult. The admitted
        // primitive and string paths validate their sizes and address ranges before calling it. Consequently, every
        // returned outcome that reached Memory.Read retains at least one read; disposal or an unexpected runtime
        // exception escapes instead of producing an EvaluationResult whose applied bounds could be understated.
        return !evidence.IsEmpty;
    }

    private static bool RootEvidenceBelongsToSession(
        ClrmdDumpSession session,
        ClrmdHeapObjectInfo root) =>
        root.Snapshot == session.Snapshot &&
        root.Module.Identity.Snapshot == session.Snapshot &&
        root.Evidence.All(read =>
            string.Equals(read.SourceId, session.Memory.SourceId, StringComparison.Ordinal));

    private static void AppendMemoryProvenance(
        ImmutableArray<EvaluationProvenance>.Builder builder,
        ImmutableArray<Interpreter.Host.Abstractions.MemoryReadResult> evidence)
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
        ImmutableArray<EvaluationProvenance> evidence)
    {
        builder.AddRange(evidence);
    }
}
