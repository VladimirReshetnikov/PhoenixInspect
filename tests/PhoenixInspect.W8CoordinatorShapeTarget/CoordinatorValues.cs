namespace PhoenixInspect.W8CoordinatorShapeTarget.Values;

/// <summary>Names the closed value-type argument reached through a namespace alias.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public readonly struct RegionCode : IEquatable<RegionCode>
{
    /// <summary>Initializes the value-type argument witness.</summary>
    /// <param name="code">The retained fixture code.</param>
    public RegionCode(int code) => Code = code;

    /// <summary>Gets the retained fixture code.</summary>
    public int Code { get; }

    /// <summary>Tests fixture-code equality.</summary>
    /// <param name="other">The other value-type argument witness.</param>
    /// <returns><see langword="true"/> only for an identical fixture code.</returns>
    public bool Equals(RegionCode other) => Code == other.Code;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RegionCode other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Code;

    /// <summary>Tests fixture-code equality.</summary>
    /// <param name="left">The left witness.</param>
    /// <param name="right">The right witness.</param>
    /// <returns><see langword="true"/> only for an identical fixture code.</returns>
    public static bool operator ==(RegionCode left, RegionCode right) => left.Equals(right);

    /// <summary>Tests fixture-code inequality.</summary>
    /// <param name="left">The left witness.</param>
    /// <param name="right">The right witness.</param>
    /// <returns><see langword="true"/> only for a differing fixture code.</returns>
    public static bool operator !=(RegionCode left, RegionCode right) => !left.Equals(right);
}
