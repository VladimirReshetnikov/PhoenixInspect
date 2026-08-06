using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the E3 delta acquisition contracts over real compiler output: each generation's physical facts decode
/// from its supplied blob with prefix-free typed stops, the lineage chain composes by the measured pairing rule,
/// and a chain that disagrees with the physically acquired edit state is a typed conflict.
/// </summary>
/// <remarks>
/// Every blob here is genuine <c>EmitDifference</c> output from the pinned compiler, produced by the same payload
/// writer the edited-process fixture applies; no delta byte is hand-authored. The E1 disposition proves the
/// runtime retains no delta blob in a dump, so acquisition is caller-supplied by design, and what these contracts
/// add is the physical validation the caller cannot assert.
/// </remarks>
public sealed class EncDeltaAcquisitionTests
{
    /// <summary>A real stacked payload composes to an exact two-generation chain that joins the edit state.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Stacked_generations_compose_an_exact_lineage_chain()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-acq-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        try
        {
            EncDeltaCompiler.WriteStackedPayload(payloadDirectory);
            var baselineMvid = ReadBaselineMvid(payloadDirectory);
            var one = MetadataEditGenerationOutcome.Acquire(
                1,
                [.. File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.metadata-delta"))]);
            var two = MetadataEditGenerationOutcome.Acquire(
                2,
                [.. File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-2.metadata-delta"))]);
            Assert.Equal(MetadataEditGenerationResultKind.Exact, one.ResultKind);
            Assert.Equal(MetadataEditGenerationResultKind.Exact, two.ResultKind);

            // The measured three-row body-edit shape: each generation's log names its updated method and the
            // reference rows its body carries, and the map assigns the same rows.
            Assert.Equal(3, one.EditLogRows.Length);
            Assert.Equal(3, one.EditMapTokens.Length);
            Assert.Contains(one.EditLogRows, static row => row.Token == 0x06_00_0001);

            var chain = MetadataEditLineageChainOutcome.Compose(
                baselineMvid,
                [one, two],
                StaticFieldV2ModuleEditStateOutcome.IssueExact(
                    runtimeModuleAddress: 0x7000_0000,
                    moduleFlags: 0x9019,
                    generationCounter: 3));
            Assert.Equal(MetadataEditLineageChainResultKind.Exact, chain.ResultKind);
            Assert.Equal(2, chain.GenerationCount);
            Assert.Equal(one.EditId, chain.Generations[1].EditBaseId);
        }
        finally
        {
            Directory.Delete(payloadDirectory, recursive: true);
        }
    }

    /// <summary>Every poisoned input produces its own prefix-free typed stop.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Poisoned_delta_inputs_produce_their_typed_stops()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-acq-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        try
        {
            EncDeltaCompiler.WriteStackedPayload(payloadDirectory);
            var baselineMvid = ReadBaselineMvid(payloadDirectory);
            ImmutableArray<byte> oneBytes =
                [.. File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.metadata-delta"))];

            // A truncated blob is unreadable, not partially decoded.
            var truncated = MetadataEditGenerationOutcome.Acquire(1, oneBytes[..32]);
            Assert.Equal(MetadataEditGenerationResultKind.Invalid, truncated.ResultKind);
            Assert.Equal(MetadataEditGenerationIssue.BlobUnreadable, truncated.Issue);

            // A blob supplied at the wrong ordinal contradicts its own Module row.
            var misordered = MetadataEditGenerationOutcome.Acquire(2, oneBytes);
            Assert.Equal(MetadataEditGenerationResultKind.Invalid, misordered.ResultKind);
            Assert.Equal(MetadataEditGenerationIssue.GenerationNumberMismatch, misordered.Issue);

            // A chain refuses a non-exact generation before joining anything.
            var one = MetadataEditGenerationOutcome.Acquire(1, oneBytes);
            var withPoisoned = MetadataEditLineageChainOutcome.Compose(baselineMvid, [truncated]);
            Assert.Equal(MetadataEditLineageChainResultKind.Invalid, withPoisoned.ResultKind);
            Assert.Equal(MetadataEditLineageChainIssue.GenerationNotExact, withPoisoned.Issue);

            // A foreign baseline identity refuses the whole chain.
            var foreign = MetadataEditLineageChainOutcome.Compose(Guid.NewGuid(), [one]);
            Assert.Equal(MetadataEditLineageChainResultKind.Invalid, foreign.ResultKind);
            Assert.Equal(MetadataEditLineageChainIssue.ForeignModuleVersionId, foreign.Issue);

            // A chain that disagrees with the physically acquired counter is a conflict, never a preference.
            var conflicted = MetadataEditLineageChainOutcome.Compose(
                baselineMvid,
                [one],
                StaticFieldV2ModuleEditStateOutcome.IssueExact(
                    runtimeModuleAddress: 0x7000_0000,
                    moduleFlags: 0x9019,
                    generationCounter: 3));
            Assert.Equal(MetadataEditLineageChainResultKind.Conflict, conflicted.ResultKind);
            Assert.Equal(
                MetadataEditLineageChainIssue.GenerationCountDisagreesWithEditState,
                conflicted.Issue);
        }
        finally
        {
            Directory.Delete(payloadDirectory, recursive: true);
        }
    }

    private static Guid ReadBaselineMvid(string payloadDirectory)
    {
        using var stream = File.OpenRead(
            Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureBaseline.dll"));
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        return reader.GetGuid(reader.GetModuleDefinition().Mvid);
    }
}
