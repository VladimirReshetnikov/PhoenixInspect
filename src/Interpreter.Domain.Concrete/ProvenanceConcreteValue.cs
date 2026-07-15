namespace Interpreter.Domain.Concrete;

/// <summary>
/// Carries a concrete-domain semantic value and, only for an explained unknown, an optional lineage root.
/// </summary>
/// <remarks>
/// Equality and hashing deliberately compare only <see cref="SemanticValue"/>. Consequently two same-typed unknowns
/// with different explanations remain one semantic lattice top while <see cref="TryGetLineageRoot"/> exposes their
/// distinct evidence channel for replay and presentation.
/// </remarks>
public sealed class ProvenanceConcreteValue : IEquatable<ProvenanceConcreteValue>
{
    internal ProvenanceConcreteValue(ConcreteValue semanticValue, LineageNodeId? lineageRoot = null)
    {
        SemanticValue = semanticValue ?? throw new ArgumentNullException(nameof(semanticValue));
        if (lineageRoot is { } root && !root.IsValid)
        {
            throw new ArgumentException("A lineage root cannot be default.", nameof(lineageRoot));
        }

        if (lineageRoot.HasValue && semanticValue.Kind != ConcreteValueKind.Unknown)
        {
            throw new ArgumentException("Only semantic top may carry an explanatory lineage root.", nameof(lineageRoot));
        }

        LineageRoot = lineageRoot;
    }

    /// <summary>Gets the lineage-independent lifted-flat concrete value.</summary>
    public ConcreteValue SemanticValue { get; }

    internal LineageNodeId? LineageRoot { get; }

    /// <summary>Attempts to get the explanation root transported with this semantic value.</summary>
    /// <param name="root">Receives the content-addressed root on success; otherwise default.</param>
    /// <returns><see langword="true"/> only for a provenance-bearing unknown.</returns>
    public bool TryGetLineageRoot(out LineageNodeId root)
    {
        if (LineageRoot is { } value)
        {
            root = value;
            return true;
        }

        root = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(ProvenanceConcreteValue? other) =>
        ReferenceEquals(this, other) || other is not null && SemanticValue == other.SemanticValue;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ProvenanceConcreteValue);

    /// <inheritdoc />
    public override int GetHashCode() => SemanticValue.GetHashCode();

    /// <summary>Compares two values by semantic value only.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the semantic values are equal, regardless of lineage.</returns>
    public static bool operator ==(ProvenanceConcreteValue? left, ProvenanceConcreteValue? right) => Equals(left, right);

    /// <summary>Compares two values for semantic inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the semantic values differ.</returns>
    public static bool operator !=(ProvenanceConcreteValue? left, ProvenanceConcreteValue? right) => !Equals(left, right);

    /// <summary>Returns the payload-safe semantic diagnostic representation without a lineage identifier.</summary>
    /// <returns><see cref="SemanticValue"/>'s deterministic representation.</returns>
    public override string ToString() => SemanticValue.ToString();
}
