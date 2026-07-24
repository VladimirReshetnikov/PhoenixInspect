using System.Security.Cryptography;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>Identifies the exact bytes of a complete artifact independently of its path or display name.</summary>
public sealed record ArtifactContentIdentity
{
    private ArtifactContentIdentity(long length, string sha256)
    {
        Length = length;
        Sha256 = sha256;
    }

    /// <summary>Gets the exact artifact length in bytes.</summary>
    public long Length { get; }

    /// <summary>Gets the lowercase SHA-256 digest of the complete artifact.</summary>
    public string Sha256 { get; }

    /// <summary>Hashes a complete readable, seekable artifact stream while preserving its current position.</summary>
    /// <param name="stream">The stream whose bytes from position zero through length form the artifact.</param>
    /// <returns>A path-independent length and SHA-256 identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is unreadable, unseekable, or empty.</exception>
    public static ArtifactContentIdentity FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("Artifact identity requires a readable, seekable stream.", nameof(stream));
        }

        if (stream.Length <= 0)
        {
            throw new ArgumentException("An artifact cannot be empty.", nameof(stream));
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            return new ArtifactContentIdentity(
                stream.Length,
                Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>Rehydrates a persisted complete-artifact identity.</summary>
    /// <param name="length">The positive artifact length.</param>
    /// <param name="sha256">A 64-character hexadecimal SHA-256 digest.</param>
    /// <returns>A validated identity with a canonical lowercase digest.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="sha256"/> is not a SHA-256 digest.</exception>
    public static ArtifactContentIdentity FromDigest(long length, string sha256)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Artifact length must be positive.");
        }

        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != SHA256.HashSizeInBytes * 2)
        {
            throw new ArgumentException("A 64-character SHA-256 digest is required.", nameof(sha256));
        }

        try
        {
            _ = Convert.FromHexString(sha256);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The artifact digest contains a non-hexadecimal character.", nameof(sha256), exception);
        }

        return new ArtifactContentIdentity(length, sha256.ToLowerInvariant());
    }
}
