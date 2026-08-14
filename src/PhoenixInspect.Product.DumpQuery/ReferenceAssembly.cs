using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>
/// One caller-referenced assembly whose metadata participates in expression evaluation: literal fields, enum
/// declarations, and type names resolve from its physical metadata exactly as they do from dump modules.
/// </summary>
/// <remarks>
/// A reference carries an optional extern alias with the language's scoping rule: an aliased reference is
/// reachable only through its alias qualifier, while an unaliased reference joins the global scope beside the
/// session's modules. The metadata bytes are read once at load and retained immutably, so every evaluation scans
/// the same content and the content digest names it in provenance.
/// </remarks>
public sealed class ReferenceAssembly
{
    private readonly ImmutableArray<byte> metadataBytes;

    private ReferenceAssembly(
        string displayName,
        string? alias,
        ImmutableArray<byte> metadataBytes,
        string metadataSha256)
    {
        DisplayName = displayName;
        Alias = alias;
        this.metadataBytes = metadataBytes;
        MetadataSha256 = metadataSha256;
    }

    /// <summary>Gets the display name of the reference, the file name it was loaded from.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the extern alias restricting how the reference is reached, or null for the global scope.</summary>
    public string? Alias { get; }

    /// <summary>Gets the lowercase SHA-256 digest of the retained metadata content.</summary>
    public string MetadataSha256 { get; }

    /// <summary>Gets a defensive copy of the retained metadata content.</summary>
    public ImmutableArray<byte> MetadataBytes => metadataBytes;

    internal ImmutableArray<byte> MetadataBytesCore => metadataBytes;

    /// <summary>Loads one reference assembly's metadata from a file.</summary>
    /// <param name="path">The assembly file path.</param>
    /// <param name="alias">The extern alias to assign, or null for the global scope.</param>
    /// <param name="error">The typed load error when loading fails, otherwise null.</param>
    /// <returns>The loaded reference, or null with <paramref name="error"/> set.</returns>
    /// <exception cref="ArgumentException">The path is null or whitespace, or the alias is empty.</exception>
    public static ReferenceAssembly? TryLoad(string path, string? alias, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (alias is not null && string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("An alias, when supplied, must be nonempty.", nameof(alias));
        }

        if (!File.Exists(path))
        {
            error = $"The referenced assembly does not exist: {path}";
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                error = $"The referenced file carries no managed metadata: {path}";
                return null;
            }

            var bytes = peReader.GetMetadata().GetContent();
            error = null;
            return new ReferenceAssembly(
                Path.GetFileName(path),
                alias,
                bytes,
                CanonicalReplayEncoding.ComputeSha256(bytes.AsSpan()));
        }
        catch (BadImageFormatException)
        {
            error = $"The referenced file is not a readable assembly image: {path}";
            return null;
        }
        catch (IOException exception)
        {
            error = $"The referenced assembly could not be read: {exception.Message}";
            return null;
        }
    }
}
