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

    /// <summary>Evaluates one closed-grammar expression over a caller-selected dump root.</summary>
    /// <param name="session">The immutable dump session from which <paramref name="root"/> was selected.</param>
    /// <param name="expression">Untrusted expression text subject to deterministic syntax and length bounds.</param>
    /// <param name="rootName">The exact case-sensitive identifier assigned to the supplied root.</param>
    /// <param name="root">
    /// The already selected root object, or <see langword="null"/> when root selection produced no exact object.
    /// Missing root evidence remains unavailable and is never reinterpreted as a null target value.
    /// </param>
    /// <returns>
    /// A multi-axis derived-query result. Exact null, partial or missing evidence, unsupported field types, and
    /// invalid syntax remain distinct outcomes with ordered provenance and stable secret-safe diagnostics.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static EvaluationResult<DumpQueryValue> Evaluate(
        ClrmdDumpSession session,
        string? expression,
        string? rootName,
        ClrmdHeapObjectInfo? root)
    {
        ArgumentNullException.ThrowIfNull(session);
        var parsed = DumpQueryParser.Parse(expression, rootName);
        if (!parsed.IsSuccess)
        {
            return CreateResult(
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
        AppendMemoryProvenance(provenance, root.Evidence);
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.RuntimeStructure,
            root.Snapshot.MemorySourceId,
            root.Address));

        var fieldResult = session.GetInstanceField(root, query.FieldName);
        if (fieldResult.Status != ClrmdEvidenceStatus.Exact)
        {
            var observation = fieldResult.ToObservationResult();
            AppendProvenance(provenance, observation.Provenance);
            return CreateResult(
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
                return InvalidCoalesceType(provenance);
            }

            return EvaluateInt32(session, root, query.FieldName, provenance);
        }

        if (string.Equals(field.ElementType, "String", StringComparison.Ordinal))
        {
            if (query.CoalesceLiteral is { Kind: not (DumpQueryLiteralKind.String or DumpQueryLiteralKind.Null) })
            {
                return InvalidCoalesceType(provenance);
            }

            return EvaluateString(session, root, query, provenance);
        }

        return CreateResult(
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
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var fieldRead = session.ReadInt32Field(root, fieldName);
        var observation = fieldRead.ToObservationResult();
        AppendProvenance(provenance, observation.Provenance);
        if (fieldRead.Status == ClrmdEvidenceStatus.Exact && fieldRead.Value?.Value is int value)
        {
            return CreateResult(
                EvaluationCompletionStatus.Completed,
                EvaluationCompleteness.Complete,
                EvaluationEvidenceStatus.Exact,
                DumpQueryValue.FromInt32(value),
                provenance.ToImmutable(),
                observation.Diagnostics);
        }

        return CreateResult(
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
        ImmutableArray<EvaluationProvenance>.Builder provenance)
    {
        var fieldRead = session.ReadStringField(root, query.FieldName, MaximumObservedStringCharacters);
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
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            observation.Evidence,
            null,
            provenance.ToImmutable(),
            observation.Diagnostics);
    }

    private static EvaluationResult<DumpQueryValue> InvalidCoalesceType(
        ImmutableArray<EvaluationProvenance>.Builder provenance) =>
        CreateResult(
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            null,
            provenance.ToImmutable(),
            ImmutableArray.Create(new EvaluationDiagnostic(
                "QUERY_COALESCE_TYPE_UNSUPPORTED",
                "The null-coalescing literal is incompatible with the selected field type.")));

    private static EvaluationResult<DumpQueryValue> CreateResult(
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
            provenance,
            diagnostics);

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
