using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>
/// Reports whether one expression belongs to the closed W2 syntax without binding a root, selecting a field, or
/// reading dump evidence.
/// </summary>
/// <remarks>
/// This draft product contract deliberately exposes only admission, stable diagnostics, and the deterministic bounds
/// actually reached. It does not expose the parser's internal tree and does not widen the W2 grammar. A successful
/// result says only that later W2 preparation may attempt evidence binding; it is not an evaluation result.
/// </remarks>
public sealed class DumpQuerySyntaxClassification
{
    private readonly ImmutableArray<EvaluationDeterministicBound> appliedBounds;

    internal DumpQuerySyntaxClassification(
        bool isSupported,
        string? diagnosticCode,
        string? diagnosticMessage,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds)
    {
        IsSupported = isSupported;
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;
        this.appliedBounds = ImmutableArray.CreateRange(appliedBounds.AsSpan().ToArray());
    }

    /// <summary>
    /// Gets whether the expression is admitted by the exact W2 grammar. Admission performs no member binding and
    /// makes no claim that the requested field exists or can be observed.
    /// </summary>
    public bool IsSupported { get; }

    /// <summary>
    /// Gets the stable parser diagnostic code for a rejected expression, or <see langword="null"/> after admission.
    /// </summary>
    public string? DiagnosticCode { get; }

    /// <summary>
    /// Gets the artifact-independent parser explanation for a rejected expression, or <see langword="null"/> after
    /// admission.
    /// </summary>
    public string? DiagnosticMessage { get; }

    /// <summary>
    /// Gets a defensive copy of only the deterministic syntax bounds reached before admission or rejection.
    /// </summary>
    public ImmutableArray<EvaluationDeterministicBound> AppliedBounds =>
        ImmutableArray.CreateRange(appliedBounds.AsSpan().ToArray());
}
