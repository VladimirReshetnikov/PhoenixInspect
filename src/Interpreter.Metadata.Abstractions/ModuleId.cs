using Interpreter.Core.Abstractions;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Identifies managed-module content independently of where an artifact happened to be found.
/// </summary>
/// <remarks>
/// Display names and paths are intentionally excluded from equality. They are evidence and lookup hints, not
/// content identity; the same image copied to another directory must retain the same identifier.
/// </remarks>
public sealed record ModuleId
{
    /// <summary>Creates a canonical artifact identity from metadata and complete artifact content.</summary>
    /// <param name="contentIdentity">The exact metadata-image identity.</param>
    /// <param name="peStamp">The optional PE timestamp and image-size tuple.</param>
    /// <param name="artifactIdentity">The exact complete-artifact identity when a PE artifact is available.</param>
    public ModuleId(
        ModuleContentIdentity contentIdentity,
        (uint TimeDateStamp, uint ImageSize)? peStamp = null,
        ArtifactContentIdentity? artifactIdentity = null)
    {
        ContentIdentity = contentIdentity ?? throw new ArgumentNullException(nameof(contentIdentity));
        PeStamp = peStamp;
        ArtifactIdentity = artifactIdentity;
    }

    /// <summary>Gets the exact, path-independent metadata identity.</summary>
    public ModuleContentIdentity ContentIdentity { get; }

    /// <summary>Gets the module version identifier embedded in <see cref="ContentIdentity"/>.</summary>
    public Guid Mvid => ContentIdentity.Mvid;

    /// <summary>Gets optional PE timestamp and image-size evidence.</summary>
    public (uint TimeDateStamp, uint ImageSize)? PeStamp { get; }

    /// <summary>Gets the exact complete-artifact identity when the module came from a PE artifact.</summary>
    public ArtifactContentIdentity? ArtifactIdentity { get; }
}
