using System.Buffers.Binary;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Carries the complete canonical product projection of one validated exact target-exception machine transition.
/// </summary>
/// <remarks>
/// The fragment deliberately answers only which terminal outcome one admitted activation produced. It contains no
/// snapshot, root, request, plan, traversal, or historical-execution identity. This is a draft W4 conformance
/// surface; broader exception kinds require an explicit schema extension and executable evidence.
/// </remarks>
public sealed class CounterfactualTargetOutcomeFragment :
    IEquatable<CounterfactualTargetOutcomeFragment>
{
    /// <summary>Gets the only canonical schema version emitted by the W4 target-outcome projector.</summary>
    public const int CanonicalSchemaVersion = 1;

    private readonly ImmutableArray<MethodHandle> _callTrace;
    private readonly ImmutableArray<DebugEvent> _events;
    private readonly ImmutableArray<EvaluationDiagnostic> _diagnostics;
    private readonly ImmutableArray<byte> _canonicalBytes;

    internal CounterfactualTargetOutcomeFragment(
        TargetExceptionInfo targetException,
        ImmutableArray<MethodHandle> callTrace,
        long initialInstructionUnits,
        long usedInstructionUnits,
        long remainingInstructionUnits,
        ImmutableArray<DebugEvent> events,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(targetException);

        SchemaVersion = CanonicalSchemaVersion;
        SemanticMode = EvaluationSemanticMode.CounterfactualExecution;
        Completion = EvaluationCompletionStatus.Completed;
        Completeness = EvaluationCompleteness.Complete;
        Evidence = EvaluationEvidenceStatus.Exact;
        Effects = EvaluationEffectStatus.None;
        TerminalStatus = MachineRunStatus.TargetException;
        TargetException = targetException;
        _callTrace = Copy(callTrace);
        InitialInstructionUnits = initialInstructionUnits;
        UsedInstructionUnits = usedInstructionUnits;
        RemainingInstructionUnits = remainingInstructionUnits;
        _events = Copy(events);
        _diagnostics = Copy(diagnostics);
        _canonicalBytes = CounterfactualTargetOutcomeCanonicalCodec.Encode(this);
        Sha256 = CounterfactualTargetOutcomeCanonicalCodec.Hash(_canonicalBytes.AsSpan());
    }

    /// <summary>Gets the version of the canonical binary fragment schema.</summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gets the truth mode, always <see cref="EvaluationSemanticMode.CounterfactualExecution"/>.
    /// </summary>
    public EvaluationSemanticMode SemanticMode { get; }

    /// <summary>Gets the terminal request status, always <see cref="EvaluationCompletionStatus.Completed"/>.</summary>
    public EvaluationCompletionStatus Completion { get; }

    /// <summary>Gets answer coverage, always <see cref="EvaluationCompleteness.Complete"/>.</summary>
    public EvaluationCompleteness Completeness { get; }

    /// <summary>Gets evidence quality, always <see cref="EvaluationEvidenceStatus.Exact"/>.</summary>
    public EvaluationEvidenceStatus Evidence { get; }

    /// <summary>Gets represented effects, always <see cref="EvaluationEffectStatus.None"/>.</summary>
    public EvaluationEffectStatus Effects { get; }

    /// <summary>Gets the machine terminal status, always <see cref="MachineRunStatus.TargetException"/>.</summary>
    public MachineRunStatus TerminalStatus { get; }

    /// <summary>Gets the exact located null-reference target exception validated by the projector.</summary>
    public TargetExceptionInfo TargetException { get; }

    /// <summary>
    /// Gets the root-to-leaf structural method trace retained before the terminal state discarded its call stack.
    /// </summary>
    /// <remarks>The W4.7 conformance profile contains exactly one legacy root method.</remarks>
    public ImmutableArray<MethodHandle> CallTrace => Copy(_callTrace);

    /// <summary>Gets the configured instruction units at the beginning of the complete transcript.</summary>
    public long InitialInstructionUnits { get; }

    /// <summary>Gets the instruction units consumed by the complete transcript, including the throwing load.</summary>
    public long UsedInstructionUnits { get; }

    /// <summary>Gets the instruction units remaining after the terminal transition.</summary>
    public long RemainingInstructionUnits { get; }

    /// <summary>
    /// Gets a defensive copy of the bounded complete semantic event transcript ending in the target exception.
    /// </summary>
    public ImmutableArray<DebugEvent> Events => Copy(_events);

    /// <summary>Gets a defensive copy of the stable product diagnostics describing the terminal outcome.</summary>
    public ImmutableArray<EvaluationDiagnostic> Diagnostics => Copy(_diagnostics);

    /// <summary>
    /// Gets a defensive copy of the domain-separated schema-v1 binary canonical representation.
    /// </summary>
    /// <remarks>The bytes are replay material and are not automatically safe for telemetry.</remarks>
    public ImmutableArray<byte> CanonicalBytes => Copy(_canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <inheritdoc />
    public bool Equals(CounterfactualTargetOutcomeFragment? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        _canonicalBytes.AsSpan().SequenceEqual(other._canonicalBytes.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CounterfactualTargetOutcomeFragment);

    /// <inheritdoc />
    public override int GetHashCode() =>
        BinaryPrimitives.ReadInt32BigEndian(Convert.FromHexString(Sha256));

    /// <summary>Compares two fragments by canonical content.</summary>
    /// <param name="left">The first fragment.</param>
    /// <param name="right">The second fragment.</param>
    /// <returns><see langword="true"/> when both references are null or their canonical bytes are identical.</returns>
    public static bool operator ==(
        CounterfactualTargetOutcomeFragment? left,
        CounterfactualTargetOutcomeFragment? right) =>
        EqualityComparer<CounterfactualTargetOutcomeFragment>.Default.Equals(left, right);

    /// <summary>Compares two fragments for canonical-content inequality.</summary>
    /// <param name="left">The first fragment.</param>
    /// <param name="right">The second fragment.</param>
    /// <returns><see langword="true"/> when exactly one is null or their canonical bytes differ.</returns>
    public static bool operator !=(
        CounterfactualTargetOutcomeFragment? left,
        CounterfactualTargetOutcomeFragment? right) => !(left == right);

    private static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty
            ? ImmutableArray<T>.Empty
            : ImmutableArray.CreateRange(values.AsSpan().ToArray());
}
