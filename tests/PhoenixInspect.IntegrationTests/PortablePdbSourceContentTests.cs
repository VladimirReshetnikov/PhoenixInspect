using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the embedded-source and SourceLink readers against a real Roslyn-emitted Portable PDB, and the SourceLink
/// document-map rules against the pattern forms the specification defines.
/// </summary>
public sealed class PortablePdbSourceContentTests
{
    private const string DocumentPath = "/_/src/Probe/ProbeEntry.cs";

    private const string SourceLinkJson =
        """{"documents":{"/_/*":"https://example.test/raw/*","/_/src/*":"https://example.test/src/*"}}""";

    /// <summary>A source small enough that the compiler embeds it uncompressed (format header zero).</summary>
    private const string SmallSource =
        "internal static class ProbeEntry\n{\n    internal static int Answer() => 41 + 1;\n}\n";

    /// <summary>Embedded source and SourceLink JSON round-trip out of the emitted PDB byte-for-byte.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void EmbeddedSourceAndSourceLinkRoundTripFromEmittedPdb()
    {
        var pdb = EmitPortablePdb(SmallSource);

        var embedded = PortablePdbSourceContent.TryReadEmbeddedSource(pdb, DocumentPath, 1 << 20);
        Assert.NotNull(embedded);
        Assert.Equal(SmallSource, DecodeUtf8(embedded!));

        // The embedded bytes must reproduce the PDB's own document checksum — the exact property the navigation
        // path verifies before showing them. (The blob carries the encoded bytes including the UTF-8 preamble,
        // which is also what the compiler checksummed.)
        Assert.Equal(
            Convert.ToHexString(ReadDocumentChecksum(pdb)),
            Convert.ToHexString(SHA256.HashData(embedded!)));

        Assert.Equal(SourceLinkJson, PortablePdbSourceContent.TryReadSourceLinkJson(pdb));
        Assert.Null(PortablePdbSourceContent.TryReadEmbeddedSource(pdb, "/_/src/Probe/Other.cs", 1 << 20));
    }

    /// <summary>A source large enough to be Deflate-compressed still round-trips exactly.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CompressedEmbeddedSourceRoundTripsFromEmittedPdb()
    {
        var builder = new StringBuilder("internal static class ProbeEntry\n{\n");
        for (var index = 0; index < 200; index++)
        {
            builder.Append("    internal static int Answer").Append(index).Append("() => ").Append(index).Append(";\n");
        }

        var largeSource = builder.Append("}\n").ToString();
        var pdb = EmitPortablePdb(largeSource);
        var embedded = PortablePdbSourceContent.TryReadEmbeddedSource(pdb, DocumentPath, 1 << 20);
        Assert.NotNull(embedded);
        Assert.Equal(largeSource, DecodeUtf8(embedded!));

        // A bound below the uncompressed size must reject the blob instead of truncating it.
        Assert.Null(PortablePdbSourceContent.TryReadEmbeddedSource(pdb, DocumentPath, maximumBytes: 16));
    }

    /// <summary>The document map picks the most specific pattern and normalizes separators into the URL.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void SourceLinkDocumentMapFollowsSpecificationRules()
    {
        const string map =
            """
            {"documents":{
                "/_/*":"https://example.test/raw/*",
                "/_/src/*":"https://example.test/src/*",
                "C:\\work\\repo\\*":"https://example.test/win/*",
                "/exact/File.cs":"https://example.test/exact"
            }}
            """;

        Assert.Equal(
            "https://example.test/src/Probe/ProbeEntry.cs",
            PortablePdbSourceContent.TryMapSourceLinkUrl(map, "/_/src/Probe/ProbeEntry.cs"));
        Assert.Equal(
            "https://example.test/raw/docs/Guide.cs",
            PortablePdbSourceContent.TryMapSourceLinkUrl(map, "/_/docs/Guide.cs"));
        Assert.Equal(
            "https://example.test/win/inner/Deep.cs",
            PortablePdbSourceContent.TryMapSourceLinkUrl(map, @"C:\work\repo\inner\Deep.cs"));
        Assert.Equal(
            "https://example.test/exact",
            PortablePdbSourceContent.TryMapSourceLinkUrl(map, "/exact/File.cs"));
        Assert.Null(PortablePdbSourceContent.TryMapSourceLinkUrl(map, @"D:\elsewhere\Missing.cs"));
        Assert.Null(PortablePdbSourceContent.TryMapSourceLinkUrl("not json", "/_/x.cs"));
        Assert.Null(PortablePdbSourceContent.TryMapSourceLinkUrl("{}", "/_/x.cs"));
    }

    /// <summary>Artifact relocation admits only a file whose complete bytes reproduce the expected digest.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void FindArtifactBytesRequiresTheExactContentDigest()
    {
        var pdb = EmitPortablePdb(SmallSource);
        var path = Path.Combine(Path.GetTempPath(), $"pdb-content-{Guid.NewGuid():N}.pdb");
        try
        {
            File.WriteAllBytes(path, pdb);
            var digest = Convert.ToHexString(SHA256.HashData(pdb)).ToLowerInvariant();

            var found = PortablePdbSourceContent.FindArtifactBytes(
                [Path.Combine(Path.GetTempPath(), "does-not-exist.pdb"), path], digest, 1 << 24);
            Assert.NotNull(found);
            Assert.Equal(pdb, found);

            Assert.Null(PortablePdbSourceContent.FindArtifactBytes([path], new string('0', 64), 1 << 24));
            Assert.Null(PortablePdbSourceContent.FindArtifactBytes([path], digest, maximumBytes: 4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Emits a tiny assembly whose standalone Portable PDB embeds the source and the SourceLink map.</summary>
    private static byte[] EmitPortablePdb(string source)
    {
        var text = SourceText.From(source, Encoding.UTF8, SourceHashAlgorithm.Sha256);
        var tree = CSharpSyntaxTree.ParseText(text, path: DocumentPath);
        var compilation = CSharpCompilation.Create(
            "PortablePdbSourceContentProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));

        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        using var sourceLinkStream = new MemoryStream(Encoding.UTF8.GetBytes(SourceLinkJson));
        var emitted = compilation.Emit(
            peStream,
            pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb),
            sourceLinkStream: sourceLinkStream,
            embeddedTexts: [EmbeddedText.FromSource(DocumentPath, text)]);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        return pdbStream.ToArray();
    }

    /// <summary>Decodes UTF-8 content bytes, tolerating an encoded byte-order mark.</summary>
    private static string DecodeUtf8(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');

    /// <summary>Reads the sole document's recorded SHA-256 checksum out of the emitted PDB.</summary>
    private static byte[] ReadDocumentChecksum(byte[] pdb)
    {
        using var provider = System.Reflection.Metadata.MetadataReaderProvider.FromPortablePdbStream(
            new MemoryStream(pdb, writable: false));
        var reader = provider.GetMetadataReader();
        var handle = Assert.Single(reader.Documents);
        var document = reader.GetDocument(handle);
        return reader.GetBlobBytes(document.Hash);
    }
}
