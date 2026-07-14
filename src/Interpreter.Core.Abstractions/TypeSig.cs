namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents the minimum type evidence required by the E1 value-domain and persistent-memory contracts.
/// </summary>
/// <remarks>
/// This draft type intentionally remains small until executable signature decoding requires shape, modifiers,
/// generic arguments, or assembly identity. It must not be used as a production metadata binding key.
/// </remarks>
public sealed record TypeSig
{
    /// <summary>Gets the maximum admitted diagnostic type-name length.</summary>
    public const int MaximumDisplayNameLength = 1024;

    /// <summary>Creates bounded draft type evidence.</summary>
    /// <param name="displayName">
    /// A deterministic diagnostic type name supplied by a trusted fixture seeder; it is not yet a full ECMA-335
    /// signature identity.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="displayName"/> exceeds <see cref="MaximumDisplayNameLength"/> characters.
    /// </exception>
    public TypeSig(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A non-empty deterministic type display name is required.", nameof(displayName));
        }

        if (displayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayName),
                $"Type display names are limited to {MaximumDisplayNameLength} characters.");
        }

        DisplayName = displayName;
    }

    /// <summary>Gets the bounded deterministic diagnostic type name.</summary>
    public string DisplayName { get; }
}
