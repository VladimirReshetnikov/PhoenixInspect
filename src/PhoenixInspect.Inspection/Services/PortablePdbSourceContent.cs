using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhoenixInspect.Inspection;

/// <summary>
/// Reads embedded-source and SourceLink facts out of one identity-matched Portable PDB, so a frame whose recorded
/// source file is absent from the analysis machine can still be shown the code the build actually consumed.
/// </summary>
/// <remarks>
/// Every member operates on complete PDB bytes that the caller has already content-hashed against the artifact
/// identity the dump validated, so nothing here trusts a file path. Embedded source is compiler-written content
/// inside that artifact; a SourceLink mapping is likewise deterministic PDB content, though what a URL serves today
/// is not — which is why downloaded bytes are shown only after they reproduce the document checksum.
/// </remarks>
public static class PortablePdbSourceContent
{
    /// <summary>The Portable-PDB custom-debug-information kind that carries an embedded source blob.</summary>
    private static readonly Guid EmbeddedSourceKind = new("0e8a571b-6926-466e-b4ad-8ab04611f5fe");

    /// <summary>The Portable-PDB custom-debug-information kind that carries the SourceLink JSON document map.</summary>
    private static readonly Guid SourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");

    /// <summary>
    /// Finds the candidate file whose complete bytes reproduce the expected artifact digest and returns those bytes.
    /// </summary>
    /// <param name="candidatePaths">Local candidate paths, in offer order.</param>
    /// <param name="expectedSha256">The lowercase SHA-256 the dump-validated artifact identity recorded.</param>
    /// <param name="maximumBytes">The largest artifact admitted for re-reading.</param>
    /// <returns>The matching artifact bytes, or null when no candidate reproduces the digest.</returns>
    /// <remarks>
    /// The artifact was already validated once during frame resolution, but files can change between calls, so the
    /// bytes are re-read and re-hashed here rather than trusting that a path still holds the same content.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="candidatePaths"/> is null.</exception>
    public static byte[]? FindArtifactBytes(
        IEnumerable<string> candidatePaths,
        string expectedSha256,
        int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(candidatePaths);
        if (string.IsNullOrEmpty(expectedSha256))
        {
            return null;
        }

        foreach (var path in candidatePaths)
        {
            byte[] bytes;
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > maximumBytes)
                {
                    continue;
                }

                bytes = File.ReadAllBytes(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                continue;
            }

            var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (string.Equals(digest, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return bytes;
            }
        }

        return null;
    }

    /// <summary>Reads the source the compiler embedded for one document, when the PDB carries it.</summary>
    /// <param name="portablePdbBytes">The complete bytes of the identity-matched Portable PDB.</param>
    /// <param name="documentPath">The exact build-recorded document path to look up.</param>
    /// <param name="maximumBytes">The largest uncompressed source admitted.</param>
    /// <returns>The embedded source bytes, or null when the document has no embedded source within the bound.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static byte[]? TryReadEmbeddedSource(byte[] portablePdbBytes, string documentPath, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(portablePdbBytes);
        ArgumentNullException.ThrowIfNull(documentPath);
        try
        {
            using var provider = MetadataReaderProvider.FromPortablePdbStream(
                new MemoryStream(portablePdbBytes, writable: false));
            var reader = provider.GetMetadataReader();
            var documentHandle = FindDocument(reader, documentPath);
            if (documentHandle.IsNil)
            {
                return null;
            }

            foreach (var handle in reader.GetCustomDebugInformation(documentHandle))
            {
                var information = reader.GetCustomDebugInformation(handle);
                if (reader.GetGuid(information.Kind) != EmbeddedSourceKind)
                {
                    continue;
                }

                return DecodeEmbeddedSourceBlob(reader.GetBlobBytes(information.Value), maximumBytes);
            }

            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>Reads the module-level SourceLink JSON document map, when the PDB carries one.</summary>
    /// <param name="portablePdbBytes">The complete bytes of the identity-matched Portable PDB.</param>
    /// <returns>The UTF-8 decoded SourceLink JSON, or null when the PDB records none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="portablePdbBytes"/> is null.</exception>
    public static string? TryReadSourceLinkJson(byte[] portablePdbBytes)
    {
        ArgumentNullException.ThrowIfNull(portablePdbBytes);
        try
        {
            using var provider = MetadataReaderProvider.FromPortablePdbStream(
                new MemoryStream(portablePdbBytes, writable: false));
            var reader = provider.GetMetadataReader();
            foreach (var handle in reader.GetCustomDebugInformation(EntityHandle.ModuleDefinition))
            {
                var information = reader.GetCustomDebugInformation(handle);
                if (reader.GetGuid(information.Kind) == SourceLinkKind)
                {
                    return Encoding.UTF8.GetString(reader.GetBlobBytes(information.Value));
                }
            }

            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>Maps one build-recorded document path to a URL through a SourceLink document map.</summary>
    /// <param name="sourceLinkJson">The SourceLink JSON read from the PDB.</param>
    /// <param name="documentPath">The exact build-recorded document path.</param>
    /// <returns>The mapped URL, or null when no pattern in the map covers the path.</returns>
    /// <remarks>
    /// Patterns follow the SourceLink specification: an entry key either names a document exactly or ends with one
    /// <c>*</c> wildcard; the value substitutes the matched remainder (with backslashes normalized to forward
    /// slashes) for its own <c>*</c>. When several wildcard entries match, the longest prefix wins.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static string? TryMapSourceLinkUrl(string sourceLinkJson, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(sourceLinkJson);
        ArgumentNullException.ThrowIfNull(documentPath);
        try
        {
            using var parsed = JsonDocument.Parse(sourceLinkJson);
            if (!parsed.RootElement.TryGetProperty("documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? bestUrl = null;
            var bestPrefixLength = -1;
            foreach (var entry in documents.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String || entry.Value.GetString() is not { } urlPattern)
                {
                    continue;
                }

                var pattern = entry.Name;
                var wildcard = pattern.IndexOf('*');
                if (wildcard < 0)
                {
                    if (string.Equals(pattern, documentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // An exact entry is more specific than any wildcard; take it immediately.
                        return urlPattern;
                    }

                    continue;
                }

                var prefix = pattern[..wildcard];
                var suffix = pattern[(wildcard + 1)..];
                if (documentPath.Length < prefix.Length + suffix.Length ||
                    !documentPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !documentPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                    prefix.Length <= bestPrefixLength)
                {
                    continue;
                }

                var matched = documentPath[prefix.Length..^suffix.Length].Replace('\\', '/');
                bestUrl = urlPattern.Replace("*", matched, StringComparison.Ordinal);
                bestPrefixLength = prefix.Length;
            }

            return bestUrl;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Finds the document row whose name equals the build-recorded path, ordinally.</summary>
    private static DocumentHandle FindDocument(MetadataReader reader, string documentPath)
    {
        foreach (var handle in reader.Documents)
        {
            var document = reader.GetDocument(handle);
            if (!document.Name.IsNil &&
                string.Equals(reader.GetString(document.Name), documentPath, StringComparison.Ordinal))
            {
                return handle;
            }
        }

        return default;
    }

    /// <summary>
    /// Decodes one embedded-source blob: a little-endian int32 format header (zero for raw content, otherwise the
    /// uncompressed size of the Deflate-compressed remainder) followed by the content bytes.
    /// </summary>
    private static byte[]? DecodeEmbeddedSourceBlob(byte[] blob, int maximumBytes)
    {
        if (blob.Length < sizeof(int))
        {
            return null;
        }

        var format = BinaryPrimitives.ReadInt32LittleEndian(blob);
        if (format == 0)
        {
            var raw = blob[sizeof(int)..];
            return raw.Length <= maximumBytes ? raw : null;
        }

        if (format < 0 || format > maximumBytes)
        {
            return null;
        }

        try
        {
            using var deflate = new DeflateStream(
                new MemoryStream(blob, sizeof(int), blob.Length - sizeof(int), writable: false),
                CompressionMode.Decompress);
            var uncompressed = new byte[format];
            deflate.ReadExactly(uncompressed);
            // The declared size is part of the blob contract; trailing extra content would mean a malformed blob.
            return deflate.ReadByte() < 0 ? uncompressed : null;
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            return null;
        }
    }
}
