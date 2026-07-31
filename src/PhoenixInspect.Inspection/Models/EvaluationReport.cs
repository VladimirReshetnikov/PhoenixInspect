using System.Collections.Immutable;

namespace PhoenixInspect.Inspection;

/// <summary>
/// Classifies an evaluation outcome only for colour and grouping in the UI.
/// </summary>
/// <remarks>
/// This is a presentation grouping, not a new result axis. The report always carries the product's own status,
/// completeness, and evidence spellings verbatim so a viewer never has to trust the demo's summary.
/// </remarks>
public enum EvaluationSeverity
{
    /// <summary>Every required stage produced an exact result.</summary>
    Exact,

    /// <summary>An exhaustive search proved absence, which is a complete and useful answer.</summary>
    Absent,

    /// <summary>A bound or incomplete byte source retained only a prefix of the required evidence.</summary>
    Partial,

    /// <summary>Required evidence was missing, ambiguous, conflicting, invalid, or outside the enabled profile.</summary>
    Stopped,

    /// <summary>The expression itself was rejected before any evidence was bound.</summary>
    Rejected,
}

/// <summary>One raw memory read retained as evaluation provenance.</summary>
/// <param name="Address">The formatted target address.</param>
/// <param name="Requested">The requested byte count.</param>
/// <param name="Observed">The observed byte count.</param>
/// <param name="Status">The read status spelling.</param>
/// <param name="Preview">A bounded hexadecimal preview of the retained bytes.</param>
public sealed record MemoryReadRow(
    string Address,
    string Requested,
    string Observed,
    string Status,
    string Preview);

/// <summary>One deterministic bound that the producing operation actually applied.</summary>
/// <param name="Name">The canonical bound name.</param>
/// <param name="Value">The formatted bound value.</param>
public sealed record BoundRow(string Name, string Value);

/// <summary>One stable diagnostic code and explanation emitted by a non-exact result.</summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Message">The artifact-independent explanation.</param>
public sealed record DiagnosticRow(string Code, string Message);

/// <summary>
/// Carries the complete display projection of one expression evaluation, including the evidence that explains a
/// partial or unknown answer.
/// </summary>
public sealed record EvaluationReport
{
    /// <summary>Gets the raw expression text exactly as submitted.</summary>
    public required string Expression { get; init; }

    /// <summary>Gets the display name of the evaluation path that produced this report.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the presentation grouping used for colour and iconography.</summary>
    public required EvaluationSeverity Severity { get; init; }

    /// <summary>Gets the product's own terminal status spelling.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the stage or routing description reached by the pipeline.</summary>
    public required string Stage { get; init; }

    /// <summary>Gets the rendered value, or an explicit statement that no value was produced.</summary>
    public required string Value { get; init; }

    /// <summary>Gets the value kind spelling, or null when no value was produced.</summary>
    public string? ValueKind { get; init; }

    /// <summary>Gets the canonical replay digest of the complete result, when the contract exposes one.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Gets labelled facts describing how the expression bound to evidence.</summary>
    public ImmutableArray<PropertyRow> Facts { get; init; } = [];

    /// <summary>Gets raw memory reads retained by the evaluation, in execution order.</summary>
    public ImmutableArray<MemoryReadRow> MemoryReads { get; init; } = [];

    /// <summary>Gets only the deterministic bounds actually reached.</summary>
    public ImmutableArray<BoundRow> Bounds { get; init; } = [];

    /// <summary>Gets the stable diagnostics explaining a non-exact outcome.</summary>
    public ImmutableArray<DiagnosticRow> Diagnostics { get; init; } = [];

    /// <summary>Gets the wall-clock duration of the evaluation.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the object this expression evaluated to, when that object can be adopted as an instance-expression root.
    /// </summary>
    /// <remarks>
    /// This is carried on the report rather than on the panel so re-selecting an earlier result from history restores
    /// exactly the object that result observed, not whatever the most recent evaluation happened to produce.
    /// </remarks>
    public RootSelection? PromotableRoot { get; init; }
}
