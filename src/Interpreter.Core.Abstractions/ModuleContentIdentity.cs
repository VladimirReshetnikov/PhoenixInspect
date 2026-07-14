using System.Security.Cryptography;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Identifies the exact metadata image associated with one managed module.
/// </summary>
/// <remarks>
/// An MVID is useful runtime metadata, but it is neither collision-resistant nor guaranteed to change when a
/// producer rewrites an image incorrectly. This identity therefore binds the MVID to the byte length and SHA-256
/// digest of the complete ECMA-335 metadata image. It intentionally excludes paths and display names.
/// </remarks>
public sealed record ModuleContentIdentity
{
    private ModuleContentIdentity(Guid mvid, int metadataLength, string metadataSha256)
    {
        Mvid = mvid;
        MetadataLength = metadataLength;
        MetadataSha256 = metadataSha256;
    }

    /// <summary>Gets the module version identifier decoded from the metadata module row.</summary>
    public Guid Mvid { get; }

    /// <summary>Gets the exact number of bytes in the hashed metadata image.</summary>
    public int MetadataLength { get; }

    /// <summary>Gets the lowercase SHA-256 digest of the complete metadata image.</summary>
    public string MetadataSha256 { get; }

    /// <summary>Creates an identity directly from an observed metadata image.</summary>
    /// <param name="mvid">The MVID decoded from the same metadata bytes.</param>
    /// <param name="metadata">The complete ECMA-335 metadata image, beginning at its metadata root.</param>
    /// <returns>A path-independent, collision-resistant metadata identity.</returns>
    /// <exception cref="ArgumentException"><paramref name="metadata"/> is empty.</exception>
    public static ModuleContentIdentity FromMetadata(Guid mvid, ReadOnlySpan<byte> metadata)
    {
        if (metadata.IsEmpty)
        {
            throw new ArgumentException("A module metadata image cannot be empty.", nameof(metadata));
        }

        return new ModuleContentIdentity(
            mvid,
            metadata.Length,
            Convert.ToHexString(SHA256.HashData(metadata)).ToLowerInvariant());
    }

    /// <summary>Rehydrates an identity from a persisted digest without reopening an artifact.</summary>
    /// <param name="mvid">The module version identifier.</param>
    /// <param name="metadataLength">The positive metadata-image length.</param>
    /// <param name="metadataSha256">A 64-character hexadecimal SHA-256 digest.</param>
    /// <returns>A validated identity with a canonical lowercase digest.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="metadataLength"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="metadataSha256"/> is not a SHA-256 digest.</exception>
    public static ModuleContentIdentity FromDigest(
        Guid mvid,
        int metadataLength,
        string metadataSha256)
    {
        if (metadataLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadataLength), "Metadata length must be positive.");
        }

        if (string.IsNullOrWhiteSpace(metadataSha256) || metadataSha256.Length != SHA256.HashSizeInBytes * 2)
        {
            throw new ArgumentException("A 64-character SHA-256 digest is required.", nameof(metadataSha256));
        }

        try
        {
            if (Convert.FromHexString(metadataSha256).Length != SHA256.HashSizeInBytes)
            {
                throw new ArgumentException("A 64-character SHA-256 digest is required.", nameof(metadataSha256));
            }
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The metadata digest contains a non-hexadecimal character.", nameof(metadataSha256), exception);
        }

        return new ModuleContentIdentity(mvid, metadataLength, metadataSha256.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that independently acquired runtime and artifact evidence identify the same metadata bytes.
    /// </summary>
    /// <param name="other">The independently acquired identity to compare.</param>
    /// <returns>The current identity on success, or a structured conflict naming the mismatched identity field.</returns>
    public ResolutionResult<ModuleContentIdentity> VerifyMatches(ModuleContentIdentity other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Mvid != other.Mvid)
        {
            return ResolutionResult<ModuleContentIdentity>.Failed(
                ResolutionFailureKind.Conflict,
                "MODULE_MVID_CONFLICT",
                "Runtime and artifact metadata report different module version identifiers.");
        }

        if (MetadataLength != other.MetadataLength)
        {
            return ResolutionResult<ModuleContentIdentity>.Failed(
                ResolutionFailureKind.Conflict,
                "MODULE_METADATA_LENGTH_CONFLICT",
                $"Runtime and artifact metadata lengths differ ({MetadataLength} versus {other.MetadataLength}).");
        }

        if (!string.Equals(MetadataSha256, other.MetadataSha256, StringComparison.Ordinal))
        {
            return ResolutionResult<ModuleContentIdentity>.Failed(
                ResolutionFailureKind.Conflict,
                "MODULE_METADATA_HASH_CONFLICT",
                "Runtime and artifact metadata SHA-256 digests differ.");
        }

        return ResolutionResult<ModuleContentIdentity>.Success(this);
    }
}
