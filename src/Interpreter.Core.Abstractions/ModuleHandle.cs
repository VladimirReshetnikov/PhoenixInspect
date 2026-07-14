using System.Buffers.Binary;
using System.Security.Cryptography;

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
/// truncated digest as security evidence.
/// </remarks>
public readonly record struct ModuleHandle(ulong High, ulong Low)
{
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
    /// Formats the complete handle using a fixed-width, culture-independent hexadecimal representation.
    /// </summary>
    /// <returns>The stable 32-character hexadecimal handle.</returns>
    public override string ToString() => $"{High:X16}{Low:X16}";
}
