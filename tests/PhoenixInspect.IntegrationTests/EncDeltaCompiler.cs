using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Produces the edited-process fixture payload with the pinned compiler: one Debug-configuration baseline assembly
/// plus real <c>EmitDifference</c> delta blobs the target process applies through <c>MetadataUpdater.ApplyUpdate</c>.
/// </summary>
/// <remarks>
/// This is enabling infrastructure for the Edit-and-Continue plan's E1 truth gate, not a probe: it manufactures the
/// same three-blob generation payload the runtime's edit path consumes in production, so every byte the fixture
/// applies is compiler truth rather than hand-authored metadata. The baseline is compiled with
/// <see cref="OptimizationLevel.Debug"/> deterministically, because the runtime's <c>DOTNET_MODIFIABLE_ASSEMBLIES</c>
/// gate admits Debug-configuration assemblies, and the local-signature and EnC-map providers read the baseline's own
/// portable PDB and method bodies rather than assuming shapes.
/// </remarks>
internal static class EncDeltaCompiler
{
    /// <summary>The Portable-PDB custom-debug-information kind identifying the EnC local slot map.</summary>
    private static readonly Guid EncLocalSlotMapKind = new("755F52A4-91C5-45BE-B4B8-209571BAB4D2");

    /// <summary>The Portable-PDB custom-debug-information kind identifying the EnC lambda and closure map.</summary>
    private static readonly Guid EncLambdaAndClosureMapKind = new("A643004C-0240-496F-A783-30D64F4979DE");

    private const string BaselineSource = """
        namespace PhoenixInspect.EncFixtureBaseline;

        /// <summary>Owns the sentinel the edited-process fixture reads before and after the applied delta.</summary>
        public static class Probe
        {
            /// <summary>Returns the baseline sentinel value.</summary>
            public static int Sentinel() => 0x45_6E_C0_01;
        }
        """;

    private const string GenerationOneSource = """
        namespace PhoenixInspect.EncFixtureBaseline;

        /// <summary>Owns the sentinel the edited-process fixture reads before and after the applied delta.</summary>
        public static class Probe
        {
            /// <summary>Returns the generation-one sentinel value.</summary>
            public static int Sentinel() => 0x45_6E_C0_02;
        }
        """;

    /// <summary>
    /// Writes the smoke payload: the baseline assembly, its portable PDB, and the generation-one delta triple.
    /// </summary>
    /// <param name="payloadDirectory">The directory that will hold the payload files.</param>
    internal static void WriteSmokePayload(string payloadDirectory)
    {
        var baselineCompilation = Compile("PhoenixInspect.EncFixtureBaseline", BaselineSource);
        using var imageStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var emitted = baselineCompilation.Emit(
            imageStream,
            pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        Assert.True(
            emitted.Success,
            "The pinned compiler must emit the baseline: " + string.Join("; ", emitted.Diagnostics));
        var imageBytes = imageStream.ToArray();
        var pdbBytes = pdbStream.ToArray();
        File.WriteAllBytes(
            Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureBaseline.dll"),
            imageBytes);
        File.WriteAllBytes(
            Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureBaseline.pdb"),
            pdbBytes);

        using var peReader = new PEReader(ImmutableArray.Create(imageBytes));
        using var pdbProvider = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(pdbBytes));
        var pdbReader = pdbProvider.GetMetadataReader();
        var moduleMetadata = ModuleMetadata.CreateFromImage(ImmutableArray.Create(imageBytes));
        var baseline = EmitBaseline.CreateInitialBaseline(
            baselineCompilation,
            moduleMetadata,
            method => ReadEncDebugInformation(pdbReader, method),
            method => ReadLocalSignature(peReader, method),
            hasPortableDebugInformation: true);

        var generationOneCompilation = Compile("PhoenixInspect.EncFixtureBaseline", GenerationOneSource);
        var baselineSentinel = RequireSentinel(baselineCompilation);
        var generationOneSentinel = RequireSentinel(generationOneCompilation);

        using var metadataDelta = new MemoryStream();
        using var ilDelta = new MemoryStream();
        using var pdbDelta = new MemoryStream();
        var difference = generationOneCompilation.EmitDifference(
            baseline,
            [new SemanticEdit(SemanticEditKind.Update, baselineSentinel, generationOneSentinel)],
            static _ => false,
            metadataDelta,
            ilDelta,
            pdbDelta,
            CancellationToken.None);
        Assert.True(
            difference.Success,
            "The pinned compiler must emit the delta: " + string.Join("; ", difference.Diagnostics));
        Assert.NotEmpty(difference.UpdatedMethods);
        File.WriteAllBytes(Path.Combine(payloadDirectory, "generation-1.metadata-delta"), metadataDelta.ToArray());
        File.WriteAllBytes(Path.Combine(payloadDirectory, "generation-1.il-delta"), ilDelta.ToArray());
        File.WriteAllBytes(Path.Combine(payloadDirectory, "generation-1.pdb-delta"), pdbDelta.ToArray());
    }

    private static CSharpCompilation Compile(string assemblyName, string source) =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            TrustedPlatformReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                deterministic: true));

    private static ImmutableArray<MetadataReference> TrustedPlatformReferences()
    {
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in trusted.Split(Path.PathSeparator))
        {
            var fileName = Path.GetFileName(path);
            if (string.Equals(fileName, "System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "System.Runtime.dll", StringComparison.OrdinalIgnoreCase))
            {
                selected.TryAdd(fileName, path);
            }
        }

        Assert.True(
            selected.ContainsKey("System.Private.CoreLib.dll"),
            "The test host's trusted platform assemblies must include the core library.");
        return
        [
            .. selected.Values
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ];
    }

    private static ISymbol RequireSentinel(CSharpCompilation compilation)
    {
        var probe = compilation.GetTypeByMetadataName("PhoenixInspect.EncFixtureBaseline.Probe");
        Assert.NotNull(probe);
        return Assert.Single(probe.GetMembers("Sentinel"));
    }

    /// <summary>Reads the method's EnC maps from the baseline's own portable PDB, empty when none are recorded.</summary>
    private static EditAndContinueMethodDebugInformation ReadEncDebugInformation(
        MetadataReader pdbReader,
        MethodDefinitionHandle method)
    {
        var slotMap = ReadCustomDebugInformation(pdbReader, method, EncLocalSlotMapKind);
        var lambdaMap = ReadCustomDebugInformation(pdbReader, method, EncLambdaAndClosureMapKind);
        return EditAndContinueMethodDebugInformation.Create(slotMap, lambdaMap);
    }

    private static ImmutableArray<byte> ReadCustomDebugInformation(
        MetadataReader pdbReader,
        MethodDefinitionHandle method,
        Guid kind)
    {
        foreach (var handle in pdbReader.GetCustomDebugInformation(method))
        {
            var information = pdbReader.GetCustomDebugInformation(handle);
            if (pdbReader.GetGuid(information.Kind) == kind)
            {
                return pdbReader.GetBlobContent(information.Value);
            }
        }

        return default;
    }

    /// <summary>Reads the method's local-variable signature handle from the baseline image's own method body.</summary>
    private static StandaloneSignatureHandle ReadLocalSignature(PEReader peReader, MethodDefinitionHandle method)
    {
        var metadataReader = peReader.GetMetadataReader();
        var definition = metadataReader.GetMethodDefinition(method);
        if (definition.RelativeVirtualAddress == 0)
        {
            return default;
        }

        var body = peReader.GetMethodBody(definition.RelativeVirtualAddress);
        return (StandaloneSignatureHandle)body.LocalSignature;
    }
}
