using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Identifies a managed module by a deterministic digest of its validated content identity.
/// </summary>
/// <param name="High">The most-significant 64 bits of the identity digest.</param>
/// <param name="Low">The least-significant 64 bits of the identity digest.</param>
/// <remarks>
/// This draft handle intentionally excludes file-system paths and process-local hash codes. A metadata backend
/// should derive it from exact metadata content, PE identity fields, and complete artifact content when a PE is
/// available, so copying the same image does not change traces while patched method bodies cannot alias. The compact
/// value is an execution-core key; artifact binding retains the complete identities rather than treating this
/// truncated digest as an authenticity or validation claim.
/// </remarks>
public readonly record struct ModuleHandle(ulong High, ulong Low)
{
    private static ReadOnlySpan<byte> RuntimeEvidenceDomain =>
        "Interpreter.ModuleHandle.RuntimeEvidence.v1"u8;

    /// <summary>Gets the maximum admitted stable runtime-module source-identity length.</summary>
    public const int MaximumRuntimeEvidenceSourceIdLength = 2048;

    /// <summary>
    /// Creates a deterministic module handle from exact metadata, PE fields, and optional complete artifact content.
    /// </summary>
    /// <param name="contentIdentity">The validated MVID, metadata length, and complete metadata digest.</param>
    /// <param name="timeDateStamp">The PE COFF timestamp.</param>
    /// <param name="imageSize">The declared loaded image size.</param>
    /// <param name="artifactIdentity">
    /// The complete PE length and digest when available. Omit only for synthetic or non-artifact evidence keys that
    /// are never used to authenticate method-body bytes.
    /// </param>
    /// <returns>A path-independent handle derived from the supplied image identity.</returns>
    public static ModuleHandle FromContentIdentity(
        ModuleContentIdentity contentIdentity,
        uint timeDateStamp,
        uint imageSize,
        ArtifactContentIdentity? artifactIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(contentIdentity);
        var metadataDigest = Convert.FromHexString(contentIdentity.MetadataSha256);
        Span<byte> identity = stackalloc byte[101];
        identity.Clear();
        _ = contentIdentity.Mvid.TryWriteBytes(identity);
        BinaryPrimitives.WriteUInt32BigEndian(identity[16..], timeDateStamp);
        BinaryPrimitives.WriteUInt32BigEndian(identity[20..], imageSize);
        BinaryPrimitives.WriteUInt32BigEndian(identity[24..], checked((uint)contentIdentity.MetadataLength));
        metadataDigest.CopyTo(identity[28..]);
        identity[60] = artifactIdentity is null ? (byte)0 : (byte)1;
        if (artifactIdentity is not null)
        {
            BinaryPrimitives.WriteInt64BigEndian(identity[61..], artifactIdentity.Length);
            Convert.FromHexString(artifactIdentity.Sha256).CopyTo(identity[69..]);
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.TryHashData(identity, digest, out _);
        return new ModuleHandle(
            BinaryPrimitives.ReadUInt64BigEndian(digest),
            BinaryPrimitives.ReadUInt64BigEndian(digest[sizeof(ulong)..]));
    }

    /// <summary>
    /// Creates a snapshot-scoped execution handle from exact metadata and a stable runtime-module source identity.
    /// </summary>
    /// <param name="contentIdentity">The validated MVID, metadata length, and complete metadata digest.</param>
    /// <param name="stableSourceId">
    /// A bounded canonical runtime-module identity that includes the snapshot/loader provenance required to prevent
    /// target addresses or display names from aliasing across observations.
    /// </param>
    /// <returns>
    /// A domain-separated deterministic handle suitable for dump-grounded execution identity. This operation does
    /// not pretend that a runtime source identity is a complete PE <see cref="ArtifactContentIdentity"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="contentIdentity"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="stableSourceId"/> is empty, whitespace, or contains control characters.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="stableSourceId"/> exceeds <see cref="MaximumRuntimeEvidenceSourceIdLength"/> characters.
    /// </exception>
    public static ModuleHandle FromRuntimeEvidenceIdentity(
        ModuleContentIdentity contentIdentity,
        string stableSourceId)
    {
        ArgumentNullException.ThrowIfNull(contentIdentity);
        if (string.IsNullOrWhiteSpace(stableSourceId))
        {
            throw new ArgumentException(
                "A non-empty stable runtime-module source identity is required.",
                nameof(stableSourceId));
        }

        if (stableSourceId.Length > MaximumRuntimeEvidenceSourceIdLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stableSourceId),
                $"Runtime-module source identities are limited to {MaximumRuntimeEvidenceSourceIdLength} characters.");
        }

        if (stableSourceId.Any(char.IsControl))
        {
            throw new ArgumentException(
                "A runtime-module source identity cannot contain control characters.",
                nameof(stableSourceId));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(RuntimeEvidenceDomain);

        Span<byte> numeric = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(numeric, RuntimeEvidenceDomain.Length);
        hash.AppendData(numeric);

        Span<byte> mvid = stackalloc byte[16];
        _ = contentIdentity.Mvid.TryWriteBytes(mvid);
        hash.AppendData(mvid);

        BinaryPrimitives.WriteInt32BigEndian(numeric, contentIdentity.MetadataLength);
        hash.AppendData(numeric);
        hash.AppendData(Convert.FromHexString(contentIdentity.MetadataSha256));

        var sourceBytes = Encoding.UTF8.GetBytes(stableSourceId);
        BinaryPrimitives.WriteInt32BigEndian(numeric, sourceBytes.Length);
        hash.AppendData(numeric);
        hash.AppendData(sourceBytes);

        var digest = hash.GetHashAndReset();
        return new ModuleHandle(
            BinaryPrimitives.ReadUInt64BigEndian(digest),
            BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(sizeof(ulong))));
    }

    /// <summary>
    /// Formats the complete handle using a fixed-width, culture-independent hexadecimal representation.
    /// </summary>
    /// <returns>The stable 32-character hexadecimal handle.</returns>
    public override string ToString() => $"{High:X16}{Low:X16}";
}
