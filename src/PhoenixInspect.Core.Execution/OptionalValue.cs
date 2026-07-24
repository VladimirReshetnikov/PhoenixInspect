namespace PhoenixInspect.Core.Execution;

/// <summary>
/// Represents the presence or absence of a domain value without reserving <see langword="null"/> or
/// <see langword="default"/> as a sentinel.
/// </summary>
/// <typeparam name="TValue">The domain value representation.</typeparam>
public readonly struct OptionalValue<TValue> : IEquatable<OptionalValue<TValue>>
{
    private readonly TValue? _value;

    private OptionalValue(TValue value)
    {
        HasValue = true;
        _value = value;
    }

    /// <summary>Gets a value indicating whether this instance contains a domain value.</summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the contained domain value.
    /// </summary>
    /// <exception cref="InvalidOperationException">No value is present.</exception>
    public TValue Value => HasValue
        ? _value!
        : throw new InvalidOperationException("No value is present.");

    /// <summary>Gets an empty optional value.</summary>
    public static OptionalValue<TValue> None => default;

    /// <summary>
    /// Creates an optional value containing <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The domain value to contain.</param>
    /// <returns>A present optional value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static OptionalValue<TValue> Some(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new OptionalValue<TValue>(value);
    }

    /// <inheritdoc />
    public bool Equals(OptionalValue<TValue> other) =>
        HasValue == other.HasValue &&
        (!HasValue || EqualityComparer<TValue>.Default.Equals(_value!, other._value!));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OptionalValue<TValue> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HasValue ? HashCode.Combine(true, _value) : 0;

    /// <summary>Compares two optional domain values for structural equality.</summary>
    public static bool operator ==(OptionalValue<TValue> left, OptionalValue<TValue> right) => left.Equals(right);

    /// <summary>Compares two optional domain values for structural inequality.</summary>
    public static bool operator !=(OptionalValue<TValue> left, OptionalValue<TValue> right) => !left.Equals(right);
}
