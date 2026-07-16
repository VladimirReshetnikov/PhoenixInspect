using System.Globalization;
using System.Text;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies the deliberately closed value set exposed by the first dump-query slice.</summary>
/// <remarks>
/// This draft surface intentionally admits only null, <see cref="int"/>, and <see cref="string"/>. Adding another
/// kind requires an explicit evidence decoder, canonical replay projection, and scenario tests.
/// </remarks>
public enum DumpQueryValueKind
{
    /// <summary>An exact null from a supported reference or nullable-value field.</summary>
    Null,

    /// <summary>An exactly decoded signed 32-bit integer.</summary>
    Int32,

    /// <summary>An exact string or an explicitly partial string prefix.</summary>
    String,
}

/// <summary>
/// Carries one immutable value from the bounded dump-query domain without exposing target data through
/// <see cref="ToString"/>.
/// </summary>
public sealed class DumpQueryValue
{
    private DumpQueryValue(DumpQueryValueKind kind, int? int32Value, string? stringValue)
    {
        Kind = kind;
        Int32Value = int32Value;
        StringValue = stringValue;
    }

    /// <summary>Gets the active member of the closed dump-query value union.</summary>
    public DumpQueryValueKind Kind { get; }

    /// <summary>Gets the integer when <see cref="Kind"/> is <see cref="DumpQueryValueKind.Int32"/>.</summary>
    public int? Int32Value { get; }

    /// <summary>
    /// Gets the string or known prefix when <see cref="Kind"/> is <see cref="DumpQueryValueKind.String"/>.
    /// Exact null from a supported reference or nullable-value field is represented by
    /// <see cref="DumpQueryValueKind.Null"/>, never by this property's null value.
    /// </summary>
    public string? StringValue { get; }

    /// <summary>
    /// Produces a culture-independent, injective projection for
    /// <see cref="Interpreter.Core.Abstractions.EvaluationResultReplay"/>.
    /// </summary>
    /// <returns>
    /// A tagged canonical projection. Strings are encoded as UTF-16 code units so even an explicitly partial
    /// prefix ending in an unpaired surrogate remains replay-distinct.
    /// </returns>
    /// <remarks>The projection contains target-derived data and is not a diagnostic-output format.</remarks>
    public string ToCanonicalReplayProjection()
    {
        if (Kind == DumpQueryValueKind.Null)
        {
            return "null";
        }

        if (Kind == DumpQueryValueKind.Int32)
        {
            return $"i32:{Int32Value!.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        var value = StringValue!;
        var builder = new StringBuilder(4 + (value.Length * 4));
        builder.Append("s16:");
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>Returns a shape-only description that omits the target-derived value.</summary>
    /// <returns>The active kind and, for strings, only the UTF-16 length.</returns>
    public override string ToString() => Kind switch
    {
        DumpQueryValueKind.Null => "Null",
        DumpQueryValueKind.Int32 => "Int32(value omitted)",
        DumpQueryValueKind.String => $"String(length={StringValue!.Length})",
        _ => throw new InvalidOperationException("The dump-query value kind is invalid."),
    };

    internal static DumpQueryValue FromNull() => new(DumpQueryValueKind.Null, null, null);

    internal static DumpQueryValue FromInt32(int value) => new(DumpQueryValueKind.Int32, value, null);

    internal static DumpQueryValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DumpQueryValue(DumpQueryValueKind.String, null, value);
    }
}
