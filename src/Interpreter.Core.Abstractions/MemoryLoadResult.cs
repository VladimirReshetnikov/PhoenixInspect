namespace Interpreter.Core.Abstractions;

/// <summary>
/// Classifies the evidence outcome of one typed persistent-memory load.
/// </summary>
public enum MemoryLoadKind
{
    /// <summary>The request is invalid or violates the memory model's structural contract.</summary>
    Invalid,

    /// <summary>The memory model produced one exact value of the resolved field type.</summary>
    Exact,

    /// <summary>Some relevant bytes or facts exist, but they do not determine an exact value.</summary>
    Partial,

    /// <summary>The required memory evidence was not captured or imported.</summary>
    Unavailable,

    /// <summary>Available observations disagree and cannot produce one semantic value.</summary>
    Conflict,

    /// <summary>The target operation raises a modeled target exception instead of producing a value.</summary>
    TargetException,
}

/// <summary>
/// Identifies the closed target-exception vocabulary admitted by the W3 execution contract.
/// </summary>
public enum TargetExceptionKind
{
    /// <summary>A field load was applied to an exact typed null receiver.</summary>
    NullReference,
}

/// <summary>
/// Carries a structured target exception classification and, after VM stamping, its execution location.
/// </summary>
/// <remarks>
/// Memory models create unlocated values because they do not own instruction identity. The machine uses
/// <see cref="WithLocation"/> to add the admitted method and IL offset without copying host exception text.
/// </remarks>
public sealed record TargetExceptionInfo
{
    /// <summary>Gets the largest admitted stable target-exception code length.</summary>
    public const int MaximumCodeLength = 128;

    /// <summary>Creates structured target-exception information.</summary>
    /// <param name="kind">The closed target-exception classification.</param>
    /// <param name="code">A bounded stable machine-readable code.</param>
    /// <param name="method">The admitted method identity, or <see langword="null"/> before VM stamping.</param>
    /// <param name="ilOffset">The nonnegative IL offset, or <see langword="null"/> before VM stamping.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is undefined, or <paramref name="ilOffset"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> is empty or too long, only one location component is supplied, or the method handle
    /// is invalid.
    /// </exception>
    public TargetExceptionInfo(
        TargetExceptionKind kind,
        string code,
        MethodHandle? method = null,
        int? ilOffset = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ValidateCode(code, nameof(code));
        if (method.HasValue != ilOffset.HasValue)
        {
            throw new ArgumentException("Target exception method and IL offset must be supplied together.");
        }

        if (method is { } methodHandle &&
            (methodHandle.Module == default || !MethodHandle.IsValidMetadataToken(methodHandle.MetadataToken)))
        {
            throw new ArgumentException("Target exception location requires a valid method handle.", nameof(method));
        }

        if (ilOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ilOffset), "An IL offset cannot be negative.");
        }

        Kind = kind;
        Code = code;
        Method = method;
        IlOffset = ilOffset;
    }

    /// <summary>Gets the closed target-exception classification.</summary>
    public TargetExceptionKind Kind { get; }

    /// <summary>Gets the bounded stable machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Gets the admitted method identity after VM stamping, or <see langword="null"/> before it.</summary>
    public MethodHandle? Method { get; }

    /// <summary>Gets the admitted IL offset after VM stamping, or <see langword="null"/> before it.</summary>
    public int? IlOffset { get; }

    /// <summary>Adds an admitted execution location to unlocated target-exception information.</summary>
    /// <param name="method">The exact admitted method identity.</param>
    /// <param name="ilOffset">The nonnegative admitted instruction offset.</param>
    /// <returns>An equal classification and code carrying the supplied execution location.</returns>
    /// <exception cref="InvalidOperationException">This value already has a location.</exception>
    public TargetExceptionInfo WithLocation(MethodHandle method, int ilOffset)
    {
        if (Method.HasValue)
        {
            throw new InvalidOperationException("Target exception information already carries an execution location.");
        }

        return new TargetExceptionInfo(Kind, Code, method, ilOffset);
    }

    internal static void ValidateCode(string code, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaximumCodeLength)
        {
            throw new ArgumentException(
                $"A non-empty stable code of at most {MaximumCodeLength} characters is required.",
                parameterName);
        }
    }
}

/// <summary>
/// Represents an exact typed memory value, a typed evidence inability, or a modeled target exception.
/// </summary>
/// <typeparam name="TValue">The value-domain representation returned only by <see cref="MemoryLoadKind.Exact"/>.</typeparam>
/// <remarks>
/// The default value is <see cref="MemoryLoadKind.Invalid"/> without a code and is never intentionally produced.
/// Factory methods enforce that only exact results carry values, only partial or unavailable results created by
/// <see cref="FromFieldEvidence"/> carry structured field evidence, and only target-exception results carry
/// structured exception information. Existing code-only non-exact results remain valid and carry no field evidence.
/// </remarks>
public readonly struct MemoryLoadResult<TValue>
{
    private readonly TValue? value;

    private MemoryLoadResult(
        MemoryLoadKind kind,
        TValue? value,
        string? failureCode,
        TargetExceptionInfo? exception,
        FieldLoadEvidence? fieldEvidence)
    {
        Kind = kind;
        this.value = value;
        FailureCode = failureCode;
        Exception = exception;
        FieldEvidence = fieldEvidence;
    }

    /// <summary>Gets the exact/evidence/exception outcome classification.</summary>
    public MemoryLoadKind Kind { get; }

    /// <summary>
    /// Gets the bounded stable code for non-exact outcomes, including the target-exception code.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets structured target-exception information only for <see cref="MemoryLoadKind.TargetException"/>.
    /// </summary>
    public TargetExceptionInfo? Exception { get; }

    /// <summary>
    /// Gets canonical field-load evidence only for structured <see cref="MemoryLoadKind.Partial"/> or
    /// <see cref="MemoryLoadKind.Unavailable"/> results.
    /// </summary>
    /// <remarks>
    /// Code-only values produced by <see cref="NonExact"/> return <see langword="null"/>. Consumers must not infer
    /// read geometry or imported-object identity from <see cref="FailureCode"/> alone.
    /// </remarks>
    public FieldLoadEvidence? FieldEvidence { get; }

    /// <summary>Gets the exact value.</summary>
    /// <exception cref="InvalidOperationException">The result is not <see cref="MemoryLoadKind.Exact"/>.</exception>
    public TValue Value => Kind == MemoryLoadKind.Exact
        ? value!
        : throw new InvalidOperationException("A non-exact memory load result has no value.");

    /// <summary>Creates a result carrying one exact typed value.</summary>
    /// <param name="value">The exact value returned by the memory model.</param>
    /// <returns>An exact result with no failure code or target exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static MemoryLoadResult<TValue> Exact(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MemoryLoadResult<TValue>(MemoryLoadKind.Exact, value, null, null, null);
    }

    /// <summary>Creates a non-exact evidence or invalidity result.</summary>
    /// <param name="kind">A partial, unavailable, conflict, or invalid classification.</param>
    /// <param name="code">A bounded stable machine-readable code.</param>
    /// <returns>A result carrying no value and no target-exception information.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is undefined, exact, or target exception.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="code"/> is empty or too long.</exception>
    public static MemoryLoadResult<TValue> NonExact(MemoryLoadKind kind, string code)
    {
        if (!Enum.IsDefined(kind) || kind is MemoryLoadKind.Exact or MemoryLoadKind.TargetException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "A non-exact result requires Partial, Unavailable, Conflict, or Invalid.");
        }

        TargetExceptionInfo.ValidateCode(code, nameof(code));
        return new MemoryLoadResult<TValue>(kind, default, code, null, null);
    }

    /// <summary>Creates a partial or unavailable result directly from canonical field-load evidence.</summary>
    /// <param name="fieldEvidence">The complete validated partial or unavailable field observation.</param>
    /// <returns>
    /// A value-free result whose kind is derived from <see cref="FieldLoadEvidence.EvidenceStatus"/>, whose
    /// <see cref="FailureCode"/> equals <see cref="FieldLoadEvidence.ReasonCode"/>, and whose
    /// <see cref="FieldEvidence"/> is <paramref name="fieldEvidence"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="fieldEvidence"/> is <see langword="null"/>.</exception>
    public static MemoryLoadResult<TValue> FromFieldEvidence(FieldLoadEvidence fieldEvidence)
    {
        ArgumentNullException.ThrowIfNull(fieldEvidence);
        var kind = fieldEvidence.EvidenceStatus switch
        {
            EvaluationEvidenceStatus.Partial => MemoryLoadKind.Partial,
            EvaluationEvidenceStatus.Unavailable => MemoryLoadKind.Unavailable,
            _ => throw new ArgumentException(
                "Structured field-load evidence must be Partial or Unavailable.",
                nameof(fieldEvidence)),
        };

        return new MemoryLoadResult<TValue>(
            kind,
            default,
            fieldEvidence.ReasonCode,
            null,
            fieldEvidence);
    }

    /// <summary>Creates a modeled target-exception result carrying no ordinary field value.</summary>
    /// <param name="exception">The structured target exception, optionally already stamped by a VM.</param>
    /// <returns>A target-exception result whose failure code equals <paramref name="exception"/>'s stable code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static MemoryLoadResult<TValue> ForTargetException(TargetExceptionInfo exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new MemoryLoadResult<TValue>(
            MemoryLoadKind.TargetException,
            default,
            exception.Code,
            exception,
            null);
    }
}
