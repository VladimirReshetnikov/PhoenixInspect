using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Identifies the first W7 evaluator stage that did not produce an exact continuation.</summary>
/// <remarks>
/// <see cref="Complete"/> means parsing, binding, runtime correlation, optional nullable-layout composition,
/// physical decoding, and required reference assignability all completed. A non-complete value identifies a typed
/// stop, not an exception or a claim that earlier evidence should be discarded.
/// </remarks>
public enum StaticFieldExpressionEvaluationStage
{
    /// <summary>The complete Roslyn parse was invalid or outside the admitted tree profile.</summary>
    Syntax = 1,

    /// <summary>Contextual, module, or declaration binding did not select one exact symbol.</summary>
    SymbolBinding = 2,

    /// <summary>The exact metadata declaration did not map to one exhaustive runtime declaration.</summary>
    RuntimeDeclaration = 3,

    /// <summary>A nullable declaration did not acquire and compose one exact specialized child layout.</summary>
    NullableLayout = 4,

    /// <summary>Ordinary-static storage acquisition or its fixed raw decoder stopped before an exact terminal.</summary>
    Storage = 5,

    /// <summary>A matched non-null raw target could not be proved assignable to the declared reference type.</summary>
    Assignability = 6,

    /// <summary>Every required stage completed and a final exact product observation is available.</summary>
    Complete = 7,
}

/// <summary>Classifies the terminal evidence disposition of one complete static-expression evaluation attempt.</summary>
/// <remarks>
/// The union deliberately spans syntax, symbol, and runtime layers so callers can branch once on disposition and
/// then inspect the stage-specific retained payload. Exact is valid only with <see cref="StaticFieldExpressionEvaluationStage.Complete"/>.
/// </remarks>
public enum StaticFieldExpressionEvaluationStatus
{
    /// <summary>Every required stage produced one exact result.</summary>
    Exact = 1,

    /// <summary>An exhaustive symbol search proved that no declaration exists.</summary>
    Absent = 2,

    /// <summary>A deterministic bound or incomplete byte source retained only a prefix of required evidence.</summary>
    Partial = 3,

    /// <summary>A required context, module, runtime structure, slot, or memory range was unavailable.</summary>
    Unavailable = 4,

    /// <summary>Multiple complete contextual, symbol, runtime, or storage interpretations remained.</summary>
    Ambiguous = 5,

    /// <summary>Exact facts from two participating layers disagreed.</summary>
    Conflict = 6,

    /// <summary>Syntax, metadata, addresses, or bytes violated an admitted structural invariant.</summary>
    Invalid = 7,

    /// <summary>Valid input or runtime behavior was outside the enabled W7 profile.</summary>
    Unsupported = 8,
}

/// <summary>
/// Retains the complete parse-to-value outcome of one W7 static-field expression without collapsing typed stops.
/// </summary>
/// <remarks>
/// This draft result is an immutable replay envelope. It retains each exact prefix rather than forcing callers to
/// repeat parsing, metadata binding, runtime catalog correlation, or nullable layout discovery. Mapping evidence is
/// kept as unrestricted metadata reads because those images can legitimately exceed the fixed value-decoder read
/// cap. The result contains no live ClrMD object, parser tree, reader, stream, or exception.
/// </remarks>
public sealed class StaticFieldExpressionEvaluationResult :
    IEquatable<StaticFieldExpressionEvaluationResult>
{
    private readonly ImmutableArray<MemoryReadResult> runtimeDeclarationEvidence;
    private readonly ImmutableArray<EvaluationDeterministicBound> runtimeDeclarationBounds;
    private readonly ImmutableArray<MemoryReadResult> nullableLayoutEvidence;
    private readonly ImmutableArray<EvaluationDeterministicBound> nullableLayoutBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    internal StaticFieldExpressionEvaluationResult(
        StaticFieldSyntaxOutcome syntax,
        StaticFieldSymbolBindingOutcome? symbolBinding,
        StaticFieldExpressionEvaluationStage stage,
        StaticFieldExpressionEvaluationStatus status,
        ClrmdValueIssue? runtimeIssue,
        ClrmdStaticRuntimeDeclarationMappingIdentity? runtimeDeclaration,
        ImmutableArray<MemoryReadResult> runtimeDeclarationEvidence,
        ImmutableArray<EvaluationDeterministicBound> runtimeDeclarationBounds,
        ClrmdStaticNullableRuntimeLayoutIdentity? rawNullableLayout,
        ImmutableArray<MemoryReadResult> nullableLayoutEvidence,
        ImmutableArray<EvaluationDeterministicBound> nullableLayoutBounds,
        StaticFieldNullableInt32RuntimeLayoutIdentity? nullableLayout,
        ClrmdStaticFieldValueObservation? hostObservation,
        StaticFieldObservation? observation,
        DumpObjectBinding? objectBinding,
        string? diagnosticCode,
        string? diagnosticMessage)
    {
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        SymbolBinding = symbolBinding;
        Stage = stage;
        Status = status;
        RuntimeIssue = runtimeIssue;
        RuntimeDeclaration = runtimeDeclaration;
        this.runtimeDeclarationEvidence = CopyReads(runtimeDeclarationEvidence, nameof(runtimeDeclarationEvidence));
        this.runtimeDeclarationBounds = CanonicalReplayEncoding.NormalizeBounds(
            runtimeDeclarationBounds,
            nameof(runtimeDeclarationBounds));
        RawNullableLayout = rawNullableLayout;
        this.nullableLayoutEvidence = CopyReads(nullableLayoutEvidence, nameof(nullableLayoutEvidence));
        this.nullableLayoutBounds = CanonicalReplayEncoding.NormalizeBounds(
            nullableLayoutBounds,
            nameof(nullableLayoutBounds));
        NullableLayout = nullableLayout;
        HostObservation = hostObservation;
        Observation = observation;
        ObjectBinding = objectBinding;
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;

        ValidateShape();
        var writer = new CanonicalReplayEncoding.Writer("static-field-expression-evaluation-result", 1);
        writer.WriteLengthPrefixedBytes(syntax.CanonicalBytes.AsSpan());
        WriteOptionalCanonical(writer, symbolBinding?.CanonicalBytes);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)status);
        writer.WriteBoolean(runtimeIssue.HasValue);
        if (runtimeIssue.HasValue)
        {
            writer.WriteInt32((int)runtimeIssue.Value);
        }
        WriteOptionalCanonical(writer, runtimeDeclaration?.CanonicalBytes);
        WriteReads(writer, this.runtimeDeclarationEvidence);
        WriteBounds(writer, this.runtimeDeclarationBounds);
        WriteOptionalCanonical(writer, rawNullableLayout?.CanonicalBytes);
        WriteReads(writer, this.nullableLayoutEvidence);
        WriteBounds(writer, this.nullableLayoutBounds);
        WriteOptionalCanonical(writer, nullableLayout?.CanonicalBytes);
        WriteOptionalCanonical(writer, hostObservation?.CanonicalBytes);
        WriteOptionalCanonical(writer, observation?.CanonicalBytes);
        WriteOptionalCanonical(writer, objectBinding?.CanonicalBytes);
        WriteOptionalString(writer, diagnosticCode);
        WriteOptionalString(writer, diagnosticMessage);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the sole complete Roslyn parse/projection outcome; parsing occurred exactly once.</summary>
    public StaticFieldSyntaxOutcome Syntax { get; }

    /// <summary>Gets the symbol-binding outcome when syntax was accepted; otherwise gets null.</summary>
    public StaticFieldSymbolBindingOutcome? SymbolBinding { get; }

    /// <summary>Gets the first stage that stopped, or <see cref="StaticFieldExpressionEvaluationStage.Complete"/>.</summary>
    public StaticFieldExpressionEvaluationStage Stage { get; }

    /// <summary>Gets the terminal cross-layer evidence disposition.</summary>
    public StaticFieldExpressionEvaluationStatus Status { get; }

    /// <summary>Gets the Host runtime issue for mapping, layout, storage, or assignability stops when applicable.</summary>
    public ClrmdValueIssue? RuntimeIssue { get; }

    /// <summary>Gets the exact exhaustive runtime TypeDef/FieldDef mapping when that stage completed.</summary>
    public ClrmdStaticRuntimeDeclarationMappingIdentity? RuntimeDeclaration { get; }

    /// <summary>Gets a defensive execution-ordered copy of raw reads reached during runtime declaration mapping.</summary>
    public ImmutableArray<MemoryReadResult> RuntimeDeclarationEvidence =>
        ImmutableArray.CreateRange(runtimeDeclarationEvidence);

    /// <summary>Gets an ordinally normalized copy of deterministic mapping bounds actually reached.</summary>
    public ImmutableArray<EvaluationDeterministicBound> RuntimeDeclarationBounds =>
        ImmutableArray.CreateRange(runtimeDeclarationBounds);

    /// <summary>Gets the complete metadata-blind runtime nullable child catalog when acquired.</summary>
    public ClrmdStaticNullableRuntimeLayoutIdentity? RawNullableLayout { get; }

    /// <summary>Gets a defensive execution-ordered copy of raw reads reached during nullable layout projection.</summary>
    public ImmutableArray<MemoryReadResult> NullableLayoutEvidence => ImmutableArray.CreateRange(nullableLayoutEvidence);

    /// <summary>Gets an ordinally normalized copy of nullable-layout bounds actually reached.</summary>
    public ImmutableArray<EvaluationDeterministicBound> NullableLayoutBounds =>
        ImmutableArray.CreateRange(nullableLayoutBounds);

    /// <summary>Gets Product's exact semantic nullable child-layout proof when required and composed.</summary>
    public StaticFieldNullableInt32RuntimeLayoutIdentity? NullableLayout { get; }

    /// <summary>Gets the physical storage/value observation when the fixed decoder was issued.</summary>
    public ClrmdStaticFieldValueObservation? HostObservation { get; }

    /// <summary>
    /// Gets the final Product observation for failed symbol binding or a fully composed exact-symbol physical result.
    /// </summary>
    public StaticFieldObservation? Observation { get; }

    /// <summary>
    /// Gets a common W2/W6 object binding only when an exact object-reference terminal was selected by the expression.
    /// </summary>
    public DumpObjectBinding? ObjectBinding { get; }

    /// <summary>Gets a stable evaluator-level diagnostic code only for a synthesized cross-layer stop.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets an artifact-independent evaluator-level explanation paired with <see cref="DiagnosticCode"/>.</summary>
    public string? DiagnosticMessage { get; }

    /// <summary>Gets a defensive copy of the complete versioned replay bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Determines content equality from the complete versioned replay bytes.</summary>
    /// <param name="other">The evaluator result to compare.</param>
    /// <returns>True only when every exact prefix, disposition, evidence read, bound, and diagnostic agrees.</returns>
    public bool Equals(StaticFieldExpressionEvaluationResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StaticFieldExpressionEvaluationResult);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private void ValidateShape()
    {
        if (!Enum.IsDefined(Stage) || !Enum.IsDefined(Status) ||
            (DiagnosticCode is null) != (DiagnosticMessage is null) ||
            DiagnosticCode is { Length: 0 } || DiagnosticMessage is { Length: 0 })
        {
            throw new ArgumentException("The evaluator disposition or paired diagnostic is invalid.");
        }
        if ((Syntax.Status == StaticFieldSyntaxStatus.Accepted) != (SymbolBinding is not null))
        {
            throw new ArgumentException("Exactly accepted syntax must retain one symbol-binding outcome.");
        }
        if (SymbolBinding is not null &&
            !SymbolBinding.Descriptor.Equals(Syntax.Descriptor))
        {
            throw new ArgumentException("Syntax and binding descriptors disagree.", nameof(SymbolBinding));
        }
        if (RuntimeDeclaration is not null && SymbolBinding?.Status != StaticFieldBindingStatus.Exact)
        {
            throw new ArgumentException("A runtime declaration requires one exact symbol binding.");
        }
        if (RawNullableLayout is not null && RuntimeDeclaration is null ||
            NullableLayout is not null && RawNullableLayout is null ||
            HostObservation is not null && RuntimeDeclaration is null)
        {
            throw new ArgumentException("The evaluator retained a stage payload without its exact prerequisite.");
        }
        if (Observation is not null && SymbolBinding is null ||
            ObjectBinding is not null && Observation is null)
        {
            throw new ArgumentException("Product observation and object-binding prerequisites are incomplete.");
        }
        if (Stage == StaticFieldExpressionEvaluationStage.Complete)
        {
            if (Status != StaticFieldExpressionEvaluationStatus.Exact ||
                RuntimeIssue is not null || HostObservation?.Status != ClrmdStaticFieldObservationStatus.Exact ||
                Observation?.HostObservation is null)
            {
                throw new ArgumentException("A complete evaluator result requires one exact fully composed terminal.");
            }
        }
        else if (Status == StaticFieldExpressionEvaluationStatus.Exact)
        {
            throw new ArgumentException("Only a complete evaluator result can be exact.");
        }
    }

    private static ImmutableArray<MemoryReadResult> CopyReads(
        ImmutableArray<MemoryReadResult> reads,
        string parameterName)
    {
        if (reads.IsDefault || reads.Any(static read => read is null))
        {
            throw new ArgumentException("An initialized raw-read array without null entries is required.", parameterName);
        }
        return ImmutableArray.CreateRange(reads);
    }

    private static void WriteOptionalCanonical(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<byte>? bytes)
    {
        writer.WriteBoolean(bytes.HasValue);
        if (bytes.HasValue)
        {
            writer.WriteLengthPrefixedBytes(bytes.Value.AsSpan());
        }
    }

    private static void WriteOptionalString(CanonicalReplayEncoding.Writer writer, string? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteString(value);
        }
    }

    private static void WriteReads(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<MemoryReadResult> reads)
    {
        writer.WriteInt32(reads.Length);
        foreach (var read in reads)
        {
            writer.WriteString(read.SourceId);
            writer.WriteUInt64(read.Address);
            writer.WriteInt32(read.RequestedLength);
            writer.WriteInt32((int)read.Status);
            writer.WriteLengthPrefixedBytes(read.Bytes.AsSpan());
        }
    }

    private static void WriteBounds(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        writer.WriteInt32(bounds.Length);
        foreach (var bound in bounds)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }
}
