namespace Interpreter.Core.Abstractions;

/// <summary>
/// Classifies a failed attempt to resolve metadata or another execution dependency.
/// </summary>
public enum ResolutionFailureKind
{
    /// <summary>The requested evidence was not captured or is otherwise unavailable.</summary>
    Unavailable,

    /// <summary>The active prototype backend does not implement the requested capability.</summary>
    Unsupported,

    /// <summary>The request or underlying metadata is structurally invalid.</summary>
    Invalid,

    /// <summary>Available evidence admits multiple incompatible answers.</summary>
    Conflict,
}

/// <summary>Describes why a dependency resolution operation could not produce a value.</summary>
public sealed record ResolutionFailure
{
    private const int MaximumCodeLength = 128;
    private const int MaximumMessageLength = 2_048;

    /// <summary>Creates a bounded structured resolution failure.</summary>
    /// <param name="kind">The stable failure classification used by execution policy.</param>
    /// <param name="code">A stable machine-readable diagnostic code.</param>
    /// <param name="message">A human-readable explanation that must not be parsed for behavior.</param>
    public ResolutionFailure(ResolutionFailureKind kind, string code, string message)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > MaximumCodeLength)
        {
            throw new ArgumentException("A bounded stable resolution code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > MaximumMessageLength)
        {
            throw new ArgumentException("A bounded resolution explanation is required.", nameof(message));
        }

        Kind = kind;
        Code = code;
        Message = message;
    }

    /// <summary>Gets the stable failure classification used by execution policy.</summary>
    public ResolutionFailureKind Kind { get; }

    /// <summary>Gets the stable machine-readable diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the human-readable explanation that callers must not parse for behavior.</summary>
    public string Message { get; }
}

/// <summary>
/// Represents either a successfully resolved value or a structured, explainable failure.
/// </summary>
/// <typeparam name="T">The resolved value type.</typeparam>
/// <remarks>
/// This result replaces boolean <c>Try*</c> contracts at the VM/metadata seam so unavailable evidence,
/// unsupported prototype surface, malformed input, and conflicting evidence remain distinguishable. The default
/// value is an invalid result and should not be produced intentionally.
/// </remarks>
public readonly struct ResolutionResult<T>
{
    private readonly T? _value;

    private ResolutionResult(T value)
    {
        IsSuccess = true;
        _value = value;
        Failure = null;
    }

    private ResolutionResult(ResolutionFailure failure)
    {
        IsSuccess = false;
        _value = default;
        Failure = failure;
    }

    /// <summary>Gets a value indicating whether resolution produced a usable value.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the structured failure when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public ResolutionFailure? Failure { get; }

    /// <summary>
    /// Gets the resolved value.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result represents a failure.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed resolution result has no value.");

    /// <summary>
    /// Creates a successful resolution result.
    /// </summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException">The supplied result value is <see langword="null"/>.</exception>
    public static ResolutionResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ResolutionResult<T>(value);
    }

    /// <summary>
    /// Creates a failed resolution result.
    /// </summary>
    /// <param name="kind">The stable failure classification.</param>
    /// <param name="code">A stable machine-readable diagnostic code.</param>
    /// <param name="message">A human-readable explanation.</param>
    /// <returns>A failed result that preserves the supplied diagnostic.</returns>
    public static ResolutionResult<T> Failed(ResolutionFailureKind kind, string code, string message) =>
        new(new ResolutionFailure(kind, code, message));
}
