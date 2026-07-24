using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Domain.Concrete;

/// <summary>
/// Classifies values in the lifted-flat concrete domain.
/// </summary>
public enum ConcreteValueKind
{
    /// <summary>The infeasible lattice element.</summary>
    Bottom,

    /// <summary>A typed value whose concrete payload is unavailable.</summary>
    Unknown,

    /// <summary>The null object reference.</summary>
    Null,

    /// <summary>A known 32-bit CLI integer-stack value.</summary>
    Int32,

    /// <summary>A known 64-bit CLI integer-stack value.</summary>
    Int64,

    /// <summary>A known Boolean value represented in the CLI I4 stack category.</summary>
    Boolean,

    /// <summary>A known immutable string value.</summary>
    String,

    /// <summary>A deterministic reference to an object in <see cref="ConcreteMemory"/>.</summary>
    ObjectReference,

    /// <summary>A deterministic reference to an array in <see cref="ConcreteMemory"/>.</summary>
    ArrayReference,


}

/// <summary>
/// Represents a deeply immutable value in the concrete validation domain.
/// </summary>
/// <remarks>
/// The domain is "concrete" for known payloads but deliberately lifted with typed bottom and unknown elements so it
/// implements the same lattice contract exercised by future partial domains. Construction is controlled by
/// <see cref="ConcreteDomain"/> and <see cref="ConcreteMemoryModel"/> to preserve kind/payload invariants.
/// Unknown provenance is deliberately not part of this validation domain's semantic values: every static type has
/// one canonical top element, preserving structural equality and the commutative lattice laws. Production domains
/// should carry explanations in a separate evidence channel rather than contaminating semantic-state equality.
/// </remarks>
public sealed record ConcreteValue
{
    internal ConcreteValue(
        ConcreteValueKind kind,
        TypeSig staticType,
        object? payload = null)
    {
        Kind = kind;
        StaticType = staticType ?? throw new ArgumentNullException(nameof(staticType));
        Payload = payload;
    }

    /// <summary>Gets the semantic payload classification.</summary>
    public ConcreteValueKind Kind { get; }

    /// <summary>Gets the static CLI type associated with the value.</summary>
    public TypeSig StaticType { get; }

    internal object? Payload { get; }

    /// <summary>
    /// Tries to expose the deterministic heap identity of an object or array reference.
    /// </summary>
    /// <param name="referenceId">The positive allocation identifier on success.</param>
    /// <returns><see langword="true"/> for object and array references; otherwise <see langword="false"/>.</returns>
    public bool TryGetReferenceId(out long referenceId)
    {
        if (Kind is ConcreteValueKind.ObjectReference or ConcreteValueKind.ArrayReference && Payload is long id)
        {
            referenceId = id;
            return true;
        }

        referenceId = default;
        return false;
    }

    /// <summary>
    /// Formats the semantic value without process-local object identities or raw target payloads.
    /// </summary>
    /// <returns>A deterministic diagnostic representation.</returns>
    public override string ToString() => Kind switch
    {
        ConcreteValueKind.Bottom => $"bottom<{StaticType.DisplayName}>",
        ConcreteValueKind.Unknown => $"unknown<{StaticType.DisplayName}>",
        ConcreteValueKind.Null => $"null<{StaticType.DisplayName}>",
        ConcreteValueKind.String => $"string(length={((string?)Payload)?.Length ?? 0})",
        ConcreteValueKind.ObjectReference => $"object#{Payload}<{StaticType.DisplayName}>",
        ConcreteValueKind.ArrayReference => $"array#{Payload}<{StaticType.DisplayName}>",
        _ => $"{Kind}<{StaticType.DisplayName}>",
    };
}
