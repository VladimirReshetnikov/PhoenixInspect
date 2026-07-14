namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Projects canonical module identity together with non-identity display and artifact-location evidence.
/// </summary>
/// <remarks>
/// This class is deliberately not a value-equality identity type. Consumers compare <see cref="Id"/> when they
/// mean module identity; <see cref="Name"/> and <see cref="PathHint"/> are mutable-world observations used only to
/// locate or explain evidence. Any artifact found through a path hint must be validated against <see cref="Id"/>.
/// </remarks>
public sealed class ModuleDescriptor
{
    /// <summary>Creates a non-identity descriptor for a canonical module identifier.</summary>
    /// <param name="id">The path-independent module identity.</param>
    /// <param name="name">An optional metadata or runtime display name.</param>
    /// <param name="pathHint">An optional artifact path that does not participate in identity.</param>
    public ModuleDescriptor(ModuleId id, string? name = null, string? pathHint = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name;
        PathHint = pathHint;
    }

    /// <summary>Gets the canonical, path-independent identity.</summary>
    public ModuleId Id { get; }

    /// <summary>Gets an optional display name that must not be used as an identity key.</summary>
    public string? Name { get; }

    /// <summary>Gets an optional artifact-location hint that must be revalidated before use.</summary>
    public string? PathHint { get; }
}
