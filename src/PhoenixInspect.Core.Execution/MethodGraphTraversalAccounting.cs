using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Core.Execution;

/// <summary>
/// Classifies one deterministic unit charged while discovering a frozen method graph.
/// </summary>
/// <remarks>
/// The values are stable canonical categories. A charge records discovery work only; it does not imply that an IL
/// instruction executed, that memory was read, or that a complete graph was produced.
/// </remarks>
public enum MethodGraphTraversalChargeKind
{
    /// <summary>A previously unseen method was admitted as an interpreted graph node.</summary>
    InterpretedMethod = 0,

    /// <summary>A previously unseen structural field dependency was retained by the graph.</summary>
    FieldDependency = 1,

    /// <summary>A previously unseen caller-and-offset direct-call edge was inspected.</summary>
    DirectCallEdge = 2,

    /// <summary>A previously unseen structural method target was retained as an opaque modeled leaf.</summary>
    ModeledLeaf = 3,
}

/// <summary>
/// Identifies one consumed or rejected traversal unit in deterministic discovery order.
/// </summary>
/// <remarks>
/// Instances are issued by <see cref="MethodGraphPlanner"/> and are immutable. <see cref="Method"/> denotes the
/// discovered method for method and model charges, and the containing method for field and edge charges. A field
/// charge additionally carries its resolved structural <see cref="Field"/>. The raw token and IL offset identify the
/// request site without introducing display names or resolver-owned text into replay facts.
/// </remarks>
public sealed class MethodGraphTraversalCharge : IEquatable<MethodGraphTraversalCharge>
{
    internal MethodGraphTraversalCharge(
        int ordinal,
        MethodGraphTraversalChargeKind kind,
        MethodHandle method,
        FieldHandle? field,
        int ilOffset,
        int rawMetadataToken)
    {
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (method == default)
        {
            throw new ArgumentException("A traversal charge requires a non-default structural method.", nameof(method));
        }

        if (ilOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ilOffset));
        }

        if (rawMetadataToken == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawMetadataToken));
        }

        if ((kind == MethodGraphTraversalChargeKind.FieldDependency) != field.HasValue ||
            field == default(FieldHandle))
        {
            throw new ArgumentException(
                "A field dependency requires exactly one non-default structural field handle.",
                nameof(field));
        }

        Ordinal = ordinal;
        Kind = kind;
        Method = method;
        Field = field;
        IlOffset = ilOffset;
        RawMetadataToken = rawMetadataToken;
    }

    /// <summary>Gets the zero-based position of this unit in discovery order.</summary>
    public int Ordinal { get; }

    /// <summary>Gets the stable category of discovery work charged by this unit.</summary>
    public MethodGraphTraversalChargeKind Kind { get; }

    /// <summary>
    /// Gets the discovered method for method/model charges, or the containing method for field/edge charges.
    /// </summary>
    public MethodHandle Method { get; }

    /// <summary>Gets the resolved structural field for a field charge, or <see langword="null"/> otherwise.</summary>
    public FieldHandle? Field { get; }

    /// <summary>Gets the IL request offset, or zero for root-method discovery.</summary>
    public int IlOffset { get; }

    /// <summary>Gets the raw metadata token associated with the discovery request.</summary>
    public int RawMetadataToken { get; }

    /// <inheritdoc />
    public bool Equals(MethodGraphTraversalCharge? other) =>
        other is not null &&
        Ordinal == other.Ordinal &&
        Kind == other.Kind &&
        Method == other.Method &&
        Field == other.Field &&
        IlOffset == other.IlOffset &&
        RawMetadataToken == other.RawMetadataToken;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MethodGraphTraversalCharge);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Ordinal, Kind, Method, Field, IlOffset, RawMetadataToken);
}

/// <summary>
/// Reports configured, consumed, remaining, and rejected traversal work for one graph-preparation session.
/// </summary>
/// <remarks>
/// <see cref="Charges"/> contains only units that were consumed. When discovery attempted one additional unit after
/// consuming the configured limit, <see cref="RejectedCharge"/> identifies that first unconsumed subject and
/// <see cref="IsExhausted"/> is true. Merely consuming the last available unit is not exhaustion when graph discovery
/// completes without attempting another unit.
/// </remarks>
public sealed class MethodGraphTraversalAccounting : IEquatable<MethodGraphTraversalAccounting>
{
    private readonly ImmutableArray<MethodGraphTraversalCharge> _charges;

    internal MethodGraphTraversalAccounting(
        int limit,
        ImmutableArray<MethodGraphTraversalCharge> charges,
        MethodGraphTraversalCharge? rejectedCharge)
    {
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        if (charges.IsDefault)
        {
            throw new ArgumentException("Traversal charges must be initialized.", nameof(charges));
        }

        if (charges.Length > limit ||
            charges.Where((charge, index) => charge is null || charge.Ordinal != index).Any())
        {
            throw new ArgumentException(
                "Consumed traversal charges must fit the limit and use contiguous zero-based ordinals.",
                nameof(charges));
        }

        if (rejectedCharge is not null &&
            (charges.Length != limit || rejectedCharge.Ordinal != charges.Length))
        {
            throw new ArgumentException(
                "A rejected traversal charge must immediately follow a fully consumed limit.",
                nameof(rejectedCharge));
        }

        Limit = limit;
        _charges = ImmutableArray.CreateRange(charges.ToArray());
        RejectedCharge = rejectedCharge;
    }

    /// <summary>Gets the maximum number of traversal units configured for this preparation session.</summary>
    public int Limit { get; }

    /// <summary>Gets the number of units actually consumed before preparation completed or stopped.</summary>
    public int Used => _charges.Length;

    /// <summary>Gets the number of configured units that were not consumed.</summary>
    public int Remaining => Limit - Used;

    /// <summary>Gets a value indicating whether an attempted unit was rejected after the limit was consumed.</summary>
    public bool IsExhausted => RejectedCharge is not null;

    /// <summary>Gets a defensive immutable copy of consumed units in deterministic discovery order.</summary>
    public ImmutableArray<MethodGraphTraversalCharge> Charges =>
        ImmutableArray.CreateRange(_charges.ToArray());

    /// <summary>
    /// Gets the first structurally identified unit that was not consumed after exhaustion, or <see langword="null"/>
    /// when preparation stopped for another reason or completed within its limit.
    /// </summary>
    public MethodGraphTraversalCharge? RejectedCharge { get; }

    /// <inheritdoc />
    public bool Equals(MethodGraphTraversalAccounting? other) =>
        other is not null &&
        Limit == other.Limit &&
        _charges.SequenceEqual(other._charges) &&
        Equals(RejectedCharge, other.RejectedCharge);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MethodGraphTraversalAccounting);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Limit);
        foreach (var charge in _charges)
        {
            hash.Add(charge);
        }

        hash.Add(RejectedCharge);
        return hash.ToHashCode();
    }
}
