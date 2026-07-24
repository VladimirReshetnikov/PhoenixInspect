using System.Globalization;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Identifies one three-part semantic version of a pure call model without prerelease or build metadata.
/// </summary>
/// <remarks>
/// W4 freezes a model version into prepared-call identity. Numeric components are deliberately bounded so canonical
/// projections cannot contain unbounded version text. The closed W4 contract does not assign compatibility meaning
/// to component changes; a different component vector is simply a different model identity.
/// </remarks>
public readonly record struct PureCallModelVersion
{
    /// <summary>Gets the greatest admitted numeric value for one semantic-version component.</summary>
    public const int MaximumComponent = ushort.MaxValue;

    /// <summary>Creates one bounded three-part semantic version.</summary>
    /// <param name="major">The nonnegative major component.</param>
    /// <param name="minor">The nonnegative minor component.</param>
    /// <param name="patch">The nonnegative patch component.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A component is outside zero through <see cref="MaximumComponent"/>.
    /// </exception>
    public PureCallModelVersion(int major, int minor, int patch)
    {
        ValidateComponent(major, nameof(major));
        ValidateComponent(minor, nameof(minor));
        ValidateComponent(patch, nameof(patch));

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>Gets the major semantic-version component.</summary>
    public int Major { get; }

    /// <summary>Gets the minor semantic-version component.</summary>
    public int Minor { get; }

    /// <summary>Gets the patch semantic-version component.</summary>
    public int Patch { get; }

    /// <summary>Formats the three numeric components without culture-dependent separators or digits.</summary>
    /// <returns>The canonical <c>major.minor.patch</c> representation.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Major}.{Minor}.{Patch}");

    private static void ValidateComponent(int component, string parameterName)
    {
        if (component is < 0 or > MaximumComponent)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                component,
                $"A model version component must be between zero and {MaximumComponent}.");
        }
    }
}

/// <summary>
/// Combines one bounded canonical model identifier with the exact semantic version selected during preparation.
/// </summary>
/// <remarks>
/// The stable identifier uses lowercase ASCII alphanumeric segments separated by one period, hyphen, or underscore.
/// It is an identity, never a display name or lookup hint. Runtime model selection must additionally match the exact
/// structural target carried by <see cref="PureCallModelDescriptor"/>.
/// </remarks>
public readonly record struct PureCallModelIdentity
{
    /// <summary>Gets the maximum admitted stable model-identifier length.</summary>
    public const int MaximumStableIdLength = 128;

    /// <summary>Creates one versioned pure-model identity.</summary>
    /// <param name="stableId">The bounded canonical lowercase ASCII model identifier.</param>
    /// <param name="version">The exact three-part semantic version.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="stableId"/> is empty, too long, noncanonical, or contains non-ASCII characters.
    /// </exception>
    public PureCallModelIdentity(string stableId, PureCallModelVersion version)
    {
        PureCallModelText.ValidateStableId(stableId, nameof(stableId));
        StableId = stableId;
        Version = version;
    }

    /// <summary>Gets the canonical lowercase ASCII model identifier.</summary>
    public string StableId { get; }

    /// <summary>Gets the exact semantic version selected with this model.</summary>
    public PureCallModelVersion Version { get; }

    /// <summary>Formats the stable identifier and semantic version without using a display name.</summary>
    /// <returns>The canonical <c>stable-id@major.minor.patch</c> representation.</returns>
    public override string ToString() => StableId is null
        ? string.Empty
        : $"{StableId}@{Version}";

    internal bool IsInitialized => StableId is not null;
}

internal static class PureCallModelText
{
    internal const int MaximumStableCodeLength = 128;
    private const string StableCodePrefix = "W4.Model.";

    internal static void ValidateStableId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > PureCallModelIdentity.MaximumStableIdLength)
        {
            throw new ArgumentException(
                $"A model identifier must contain 1 to {PureCallModelIdentity.MaximumStableIdLength} characters.",
                parameterName);
        }

        static bool IsLowerAlphaNumeric(char character) =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9';
        static bool IsSeparator(char character) => character is '.' or '-' or '_';

        if (!IsLowerAlphaNumeric(value[0]) || !IsLowerAlphaNumeric(value[^1]))
        {
            throw new ArgumentException(
                "A model identifier must begin and end with a lowercase ASCII letter or digit.",
                parameterName);
        }

        var previousWasSeparator = false;
        foreach (var character in value)
        {
            if (IsLowerAlphaNumeric(character))
            {
                previousWasSeparator = false;
                continue;
            }

            if (!IsSeparator(character) || previousWasSeparator)
            {
                throw new ArgumentException(
                    "A model identifier requires lowercase ASCII alphanumeric segments separated by one period, hyphen, or underscore.",
                    parameterName);
            }

            previousWasSeparator = true;
        }
    }

    internal static void ValidateStableCode(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumStableCodeLength ||
            !value.StartsWith(StableCodePrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"A model code must use the W4.Model family and contain at most {MaximumStableCodeLength} characters.",
                parameterName);
        }

        static bool IsAlphaNumeric(char character) =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';

        var segmentLength = 0;
        foreach (var character in value)
        {
            if (IsAlphaNumeric(character))
            {
                segmentLength++;
                continue;
            }

            if (character != '.' || segmentLength == 0)
            {
                throw new ArgumentException(
                    "A model code requires nonempty ASCII alphanumeric segments separated by periods.",
                    parameterName);
            }

            segmentLength = 0;
        }

        if (segmentLength == 0)
        {
            throw new ArgumentException(
                "A model code must end with an ASCII alphanumeric segment.",
                parameterName);
        }
    }
}
