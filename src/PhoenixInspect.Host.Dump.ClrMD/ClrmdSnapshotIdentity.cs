using System.Security.Cryptography;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Identifies the immutable contents of a dump used by a ClrMD session.</summary>
/// <remarks>
/// Local paths are intentionally excluded. Reopening the same dump through a different path therefore retains the
/// same identity, while independently captured dumps cannot alias merely because target virtual addresses repeat.
/// Equality is the digest alone: the live-attach flag is provenance annotation, so a product layer that
/// round-trips only the digest reconstructs an equal identity. Dump-file digests and live-session digests cannot
/// alias, because their preimages are disjoint.
/// </remarks>
public readonly struct ClrmdSnapshotIdentity : IEquatable<ClrmdSnapshotIdentity>
{
    /// <summary>Creates a validated snapshot identity from a complete dump-file digest.</summary>
    /// <param name="sha256">A 64-character hexadecimal SHA-256 digest of the complete dump file.</param>
    /// <exception cref="ArgumentException"><paramref name="sha256"/> is not a SHA-256 digest.</exception>
    public ClrmdSnapshotIdentity(string sha256)
        : this(sha256, isLiveAttach: false)
    {
    }

    /// <summary>Creates a validated snapshot identity for a dump or a live-attach session.</summary>
    /// <param name="sha256">
    /// A 64-character hexadecimal SHA-256 digest: of the complete dump file, or of the live session's canonical
    /// identity material (machine, process, process start time, attach time).
    /// </param>
    /// <param name="isLiveAttach">Whether this identity names a suspended live-attach session.</param>
    /// <exception cref="ArgumentException"><paramref name="sha256"/> is not a SHA-256 digest.</exception>
    public ClrmdSnapshotIdentity(string sha256, bool isLiveAttach)
    {
        IsLiveAttach = isLiveAttach;
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != SHA256.HashSizeInBytes * 2)
        {
            throw new ArgumentException("A 64-character dump SHA-256 digest is required.", nameof(sha256));
        }

        try
        {
            _ = Convert.FromHexString(sha256);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The dump digest contains a non-hexadecimal character.", nameof(sha256), exception);
        }

        Sha256 = sha256.ToLowerInvariant();
    }

    /// <summary>Gets the lowercase SHA-256 digest of the complete dump file or the live session identity.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Gets whether this identity names a suspended live-attach session rather than an immutable dump. A live
    /// process has no content identity; the digest names the session, so reads replay within it but a fresh
    /// attach to the same process is a different snapshot.
    /// </summary>
    public bool IsLiveAttach { get; }

    /// <summary>Gets the provenance identifier used by raw memory-read evidence.</summary>
    public string MemorySourceId => IsLiveAttach ? $"live-attach-sha256:{Sha256}" : $"dump-sha256:{Sha256}";

    /// <summary>Compares two identities for digest equality.</summary>
    public static bool operator ==(ClrmdSnapshotIdentity left, ClrmdSnapshotIdentity right) => left.Equals(right);

    /// <summary>Compares two identities for digest inequality.</summary>
    public static bool operator !=(ClrmdSnapshotIdentity left, ClrmdSnapshotIdentity right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(ClrmdSnapshotIdentity other) =>
        string.Equals(Sha256, other.Sha256, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ClrmdSnapshotIdentity other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        Sha256 is null ? 0 : StringComparer.Ordinal.GetHashCode(Sha256);
}
