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

            // A non-exact declared edit state validates nothing: the chain refuses it as its own typed stop
            // rather than counting an unavailable truth as zero applied generations.
            var unavailableState = MetadataEditLineageChainOutcome.Compose(
                baselineMvid,
                [one],
                StaticFieldV2ModuleEditStateOutcome.IssueStop(
                    StaticFieldV2ModuleEditStateResultKind.Unavailable,
                    StaticFieldV2ModuleEditStateIssue.DescriptorFieldAbsent,
                    0x7000_0000));
            Assert.Equal(MetadataEditLineageChainResultKind.Invalid, unavailableState.ResultKind);
            Assert.Equal(MetadataEditLineageChainIssue.EditStateNotExact, unavailableState.Issue);

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

    /// <summary>
    /// A pure-Insert generation's retained log describes exactly the compiled member additions, and a chain mixing
    /// generations from two different edit histories refuses at the pair join.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Insert_generation_rows_and_mixed_history_chains_are_typed()
    {
        var addedDirectory = Path.Combine(Path.GetTempPath(), $"enc-acq-add-{Guid.NewGuid():N}");
        var stackedDirectory = Path.Combine(Path.GetTempPath(), $"enc-acq-stack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(addedDirectory);
        Directory.CreateDirectory(stackedDirectory);
        try
        {
            EncDeltaCompiler.WriteAddedStaticPayload(addedDirectory);
            EncDeltaCompiler.WriteStackedPayload(stackedDirectory);
            var addedOne = MetadataEditGenerationOutcome.Acquire(
                1,
                [.. File.ReadAllBytes(Path.Combine(addedDirectory, "generation-1.metadata-delta"))]);
            var stackedTwo = MetadataEditGenerationOutcome.Acquire(
                2,
                [.. File.ReadAllBytes(Path.Combine(stackedDirectory, "generation-2.metadata-delta"))]);
            Assert.Equal(MetadataEditGenerationResultKind.Exact, addedOne.ResultKind);
            Assert.Equal(MetadataEditGenerationResultKind.Exact, stackedTwo.ResultKind);

            // Measured, the complete Insert vocabulary: the generation extends the reference tables, then logs the
            // parent TypeDef with the AddField operation followed by the added Field row, the parent twice with
            // AddMethod followed by each added Method row, and the added setter with AddParameter followed by its
            // Param row. These operation-paired rows are exactly what E4's generation-aware projection consumes.
            ImmutableArray<MetadataEditLogRow> expectedLog =
            [
                new MetadataEditLogRow(0x23_00_0002, 0),
                new MetadataEditLogRow(0x01_00_0007, 0),
                new MetadataEditLogRow(0x02_00_0002, 2),
                new MetadataEditLogRow(0x04_00_0001, 0),
                new MetadataEditLogRow(0x02_00_0002, 1),
                new MetadataEditLogRow(0x06_00_0002, 0),
                new MetadataEditLogRow(0x02_00_0002, 1),
                new MetadataEditLogRow(0x06_00_0003, 0),
                new MetadataEditLogRow(0x06_00_0002, 3),
                new MetadataEditLogRow(0x08_00_0001, 0),
            ];
            Assert.Equal(
                string.Join(",", expectedLog.Select(static row => $"{row.Token:x8}:{row.Operation}")),
                string.Join(",", addedOne.EditLogRows.Select(static row => $"{row.Token:x8}:{row.Operation}")));

            // Two histories of the same baseline share its module version identifier yet carry different edit
            // identifiers, so a chain mixing them refuses at the pair join rather than composing a fiction.
            Assert.Equal(addedOne.ModuleVersionId, stackedTwo.ModuleVersionId);
            Assert.NotEqual(addedOne.EditId, stackedTwo.EditBaseId);
            var mixed = MetadataEditLineageChainOutcome.Compose(
                addedOne.ModuleVersionId,
                [addedOne, stackedTwo]);
            Assert.Equal(MetadataEditLineageChainResultKind.Invalid, mixed.ResultKind);
            Assert.Equal(MetadataEditLineageChainIssue.ChainPairMismatch, mixed.Issue);
        }
        finally
        {
            Directory.Delete(addedDirectory, recursive: true);
            Directory.Delete(stackedDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Discharges the E3 exit gate's filtered-dump clause: over a filtered capture the edit state acquires as the
    /// typed unavailable stop, and a chain declared against that state refuses before any catalog is issued.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Filtered_capture_stops_the_chain_before_any_catalog()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-acq-filter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-acq-filter-{Guid.NewGuid():N}.dmp");
        try
        {
            EncDeltaCompiler.WriteSmokePayload(payloadDirectory);
            using (var target = TestTargetRunner.StartAndWaitReady(
                W8ShapeTargetPaths.RequireArtifact(
                    W8ShapeTargetPaths.ResolveExecutable("PhoenixInspect.EncTestTarget")),
                ["--truth-gate", "enc-smoke", "--payload", payloadDirectory],
                isolatedDirectory: null,
                additionalEnvironment: new Dictionary<string, string>
                {
                    ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "Debug",
                }))
            {
                DumpWriter.WriteNormalDump(target.Pid, dumpPath);
            }

            // Measured, and stronger than the clause requires: the filtered capture drops the memory the contract
            // descriptor itself lives in, so the acquisition session refuses to open at its first physical read.
            // No edit state can be acquired and no catalog can be issued over a filtered capture at all; the typed
            // acquisition exception is the stop, and it fires before any delta byte is consulted.
            var refused = Assert.Throws<StaticFieldV2RuntimeAcquisitionException>(
                () => StaticFieldV2RuntimeAcquisitionSession.Open(dumpPath));
            Assert.Contains("snapshot read", refused.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }

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
