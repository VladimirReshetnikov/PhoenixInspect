namespace Interpreter.Domain.Concrete;

/// <summary>
/// Identifies one externally observed object using a bounded canonical evidence key.
/// </summary>
/// <remarks>
/// The value is supplied by the dump-preparation layer and is expected to bind snapshot, runtime/module context,
/// and exact rooted-object evidence. It is retained in persistent-memory equality and stable hashing; it is not a
/// target address alone and is never synthesized by the memory model.
/// </remarks>
public sealed record ImportedObjectEvidenceIdentity
{
    /// <summary>Gets the maximum admitted canonical evidence-identity length.</summary>
    public const int MaximumLength = 2048;

    /// <summary>Creates a validated external-object evidence identity.</summary>
    /// <param name="value">A non-empty bounded canonical identity produced by the preparation layer.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty, whitespace, or contains a control character.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> exceeds <see cref="MaximumLength"/> characters.
    /// </exception>
    public ImportedObjectEvidenceIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty external-object evidence identity is required.", nameof(value));
        }

        if (value.Length > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"External-object evidence identities are limited to {MaximumLength} characters.");
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "An external-object evidence identity cannot contain control characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the bounded canonical evidence identity.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical evidence identity.</summary>
    /// <returns><see cref="Value"/> for deterministic replay projections.</returns>
    public override string ToString() => Value;
}
