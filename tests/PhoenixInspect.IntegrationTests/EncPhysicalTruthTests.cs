using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Diagnostics.Runtime;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Executes the first slice of the Edit-and-Continue plan's E1 physical truth gate over the changed-body profile:
/// base-image invariance and applied-state undetectability from the base image, the physical shape of the
/// compiler-emitted generation delta, and what the host runtime surface reports over the edited module.
/// </summary>
/// <remarks>
/// Every assertion is a retained observation over a real artifact — the payload files the pinned compiler wrote, or
/// a full dump of the genuinely edited process — never a restatement of runtime documentation. The remaining E1
/// probe questions (delta reachability in dump memory, applied-state detectability from runtime structures,
/// added-member profiles, added-static storage, effective-body location, Portable-PDB delta retention, and the
/// filtered-dump control) stay open for the following slices.
/// </remarks>
public sealed class EncPhysicalTruthTests
{
    /// <summary>
    /// Proves the mapped base image of the edited module is byte-identical to the payload baseline and still names
    /// generation zero with empty edit identifiers, and records what the host runtime surface reports over it.
    /// </summary>
    /// <remarks>
    /// This is the physical proof of the plan's central hazard: after a genuinely applied and executed edit, the
    /// base image alone cannot reveal that the module was edited, so any authority produced from it describes the
    /// pre-edit module without any distinguishing mark. The runtime surface probe shows the edited method remains
    /// enumerable under its baseline token, so token-anchored joins keep working while saying nothing about edits.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Edited_module_base_image_is_invariant_and_undetectably_generation_zero()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-truth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-truth-{Guid.NewGuid():N}.dmp");
        try
        {
            EncDeltaCompiler.WriteSmokePayload(payloadDirectory);
            var baselinePath = Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureBaseline.dll");
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
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            ImmutableArray<byte> diskMetadata;
            int baselineSentinelToken;
            using (var stream = File.OpenRead(baselinePath))
            using (var peReader = new PEReader(stream))
            {
                diskMetadata = peReader.GetMetadata().GetContent();
                baselineSentinelToken = FindSentinelToken(peReader.GetMetadataReader());
            }

            using var dataTarget = DataTarget.LoadDump(
                dumpPath,
                new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
            var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            try
            {
                var module = Assert.Single(
                    runtime.EnumerateModules(),
                    candidate => candidate.Name?.EndsWith(
                        "PhoenixInspect.EncFixtureBaseline.dll",
                        StringComparison.OrdinalIgnoreCase) == true);
                Assert.Equal((ulong)diskMetadata.Length, module.MetadataLength);

                // Probe 1, base-image invariance: the mapped metadata in the dump of the edited process is
                // byte-identical to the unedited on-disk baseline the process loaded.
                var mapped = new byte[diskMetadata.Length];
                Assert.Equal(
                    mapped.Length,
                    dataTarget.DataReader.Read(module.MetadataAddress, mapped));
                Assert.True(
                    diskMetadata.AsSpan().SequenceEqual(mapped),
                    "The edited module's mapped base metadata must be byte-identical to the unedited baseline.");

                // Probe 3, undetectability from the base image: the mapped Module row still names generation zero
                // with empty edit identifiers after a verified, executed edit, so nothing produced from the base
                // image alone can reveal that this module was edited.
                using var provider = MetadataReaderProvider.FromMetadataImage([.. mapped]);
                var mappedReader = provider.GetMetadataReader();
                var moduleRow = mappedReader.GetModuleDefinition();
                Assert.Equal(0, moduleRow.Generation);
                Assert.True(mappedReader.GetGuid(moduleRow.GenerationId) == Guid.Empty);
                Assert.True(mappedReader.GetGuid(moduleRow.BaseGenerationId) == Guid.Empty);
                Assert.Equal(0, mappedReader.GetTableRowCount(TableIndex.EncLog));
                Assert.Equal(0, mappedReader.GetTableRowCount(TableIndex.EncMap));

                // Probe 9, host runtime surface: the edited method remains enumerable under its baseline token, so
                // token-anchored joins keep resolving while carrying no mark of the edit.
                var probeType = module.GetTypeByName("PhoenixInspect.EncFixtureBaseline.Probe");
                Assert.NotNull(probeType);
                var sentinel = Assert.Single(
                    probeType.Methods,
                    method => string.Equals(method.Name, "Sentinel", StringComparison.Ordinal));
                Assert.Equal(baselineSentinelToken, sentinel.MetadataToken);
            }
            finally
            {
                runtime.Dispose();
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }

            if (Directory.Exists(payloadDirectory))
            {
                Directory.Delete(payloadDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Proves the compiler-emitted generation delta physically describes exactly the compiled edit: a
    /// generation-one module row over the same Mvid with a nonempty edit identifier, and edit-log and edit-map
    /// tables that name the baseline sentinel method and nothing else.
    /// </summary>
    /// <remarks>
    /// This is probe 4 of the truth gate, answerable without a dump because the delta blob itself is the artifact.
    /// The delta's own <c>EncLog</c>/<c>EncMap</c> rows are the physical vocabulary a later generation-aware
    /// composition must consume, so their exact shape for the simplest edit is frozen here.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Generation_delta_tables_describe_exactly_the_compiled_edit()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-delta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        try
        {
            EncDeltaCompiler.WriteSmokePayload(payloadDirectory);
            Guid baselineMvid;
            int baselineSentinelToken;
            int baselineAssemblyReferenceRows;
            int baselineTypeReferenceRows;
            using (var stream = File.OpenRead(
                Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureBaseline.dll")))
            using (var peReader = new PEReader(stream))
            {
                var baselineReader = peReader.GetMetadataReader();
                baselineMvid = baselineReader.GetGuid(baselineReader.GetModuleDefinition().Mvid);
                baselineSentinelToken = FindSentinelToken(baselineReader);
                baselineAssemblyReferenceRows = baselineReader.GetTableRowCount(TableIndex.AssemblyRef);
                baselineTypeReferenceRows = baselineReader.GetTableRowCount(TableIndex.TypeRef);
            }

            var deltaBytes = File.ReadAllBytes(
                Path.Combine(payloadDirectory, "generation-1.metadata-delta"));
            using var deltaProvider = MetadataReaderProvider.FromMetadataImage([.. deltaBytes]);
            var delta = deltaProvider.GetMetadataReader();

            var deltaModule = delta.GetModuleDefinition();
            Assert.Equal(1, deltaModule.Generation);
            Assert.Equal(baselineMvid, delta.GetGuid(deltaModule.Mvid));
            Assert.NotEqual(Guid.Empty, delta.GetGuid(deltaModule.GenerationId));

            // Measured, not assumed: the changed-body edit logs three default-operation rows, and only one of them
            // is a definition. The compiler's generation carries its own new AssemblyRef and TypeRef rows for the
            // references the edited body needs — their RIDs sit beyond the baseline's table ends, so a delta
            // extends reference tables as well as updating definitions — and the sentinel method row is the single
            // definition the edit touches. The edit map assigns exactly the same three rows.
            var logEntries = delta.GetEditAndContinueLogEntries().ToImmutableArray();
            Assert.Equal(3, logEntries.Length);
            Assert.All(logEntries, entry => Assert.Equal(EditAndContinueOperation.Default, entry.Operation));
            var updatedMethod = Assert.Single(
                logEntries,
                entry => entry.Handle.Kind == HandleKind.MethodDefinition);
            Assert.Equal(baselineSentinelToken, MetadataTokens.GetToken(updatedMethod.Handle));
            var addedAssemblyReference = Assert.Single(
                logEntries,
                entry => entry.Handle.Kind == HandleKind.AssemblyReference);
            Assert.True(
                MetadataTokens.GetRowNumber(addedAssemblyReference.Handle) > baselineAssemblyReferenceRows,
                "The delta's AssemblyRef row must extend past the baseline table end.");
            var addedTypeReference = Assert.Single(
                logEntries,
                entry => entry.Handle.Kind == HandleKind.TypeReference);
            Assert.True(
                MetadataTokens.GetRowNumber(addedTypeReference.Handle) > baselineTypeReferenceRows,
                "The delta's TypeRef row must extend past the baseline table end.");
            Assert.Equal(
                logEntries.Select(static entry => MetadataTokens.GetToken(entry.Handle)).Order(),
                delta.GetEditAndContinueMapEntries().Select(MetadataTokens.GetToken).Order());
        }
        finally
        {
            if (Directory.Exists(payloadDirectory))
            {
                Directory.Delete(payloadDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Measures the runtime surface and storage of an edit-added static member over a real dump: the added-member
    /// census against the baseline table ends, and the added slot's location and stored value where observable.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Added_static_member_census_and_storage_are_measured_over_the_edited_dump()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-added-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-added-{Guid.NewGuid():N}.dmp");
        try
        {
            EncDeltaCompiler.WriteAddedStaticPayload(payloadDirectory);
            int baselineFieldRows;
            int baselineMethodRows;
            using (var stream = File.OpenRead(
                Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureBaseline.dll")))
            using (var peReader = new PEReader(stream))
            {
                var baselineReader = peReader.GetMetadataReader();
                baselineFieldRows = baselineReader.GetTableRowCount(TableIndex.Field);
                baselineMethodRows = baselineReader.GetTableRowCount(TableIndex.MethodDef);
            }

            using (var target = TestTargetRunner.StartAndWaitReady(
                W8ShapeTargetPaths.RequireArtifact(
                    W8ShapeTargetPaths.ResolveExecutable("PhoenixInspect.EncTestTarget")),
                ["--truth-gate", "enc-added-static", "--payload", payloadDirectory],
                isolatedDirectory: null,
                additionalEnvironment: new Dictionary<string, string>
                {
                    ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "Debug",
                }))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            using var dataTarget = DataTarget.LoadDump(
                dumpPath,
                new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
            var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            try
            {
                var module = Assert.Single(
                    runtime.EnumerateModules(),
                    candidate => candidate.Name?.EndsWith(
                        "PhoenixInspect.EncFixtureBaseline.dll",
                        StringComparison.OrdinalIgnoreCase) == true);
                var probeType = module.GetTypeByName("PhoenixInspect.EncFixtureBaseline.Probe");
                Assert.NotNull(probeType);

                // Probe 5, measured: the process provably executed the added members before READY, yet the host
                // runtime surface over the dump reports the pre-edit census — the added static field is absent
                // from the type's static fields and the added accessors are absent from its methods, while the
                // baseline sentinel stays enumerable under its baseline token. Added-member census therefore
                // cannot come from this surface; it must come from the generation's own delta tables.
                Assert.Equal(0, baselineFieldRows);
                Assert.Equal(1, baselineMethodRows);
                Assert.Empty(probeType.StaticFields);
                Assert.DoesNotContain(
                    probeType.Methods,
                    method => string.Equals(method.Name, "SetAdded", StringComparison.Ordinal) ||
                        string.Equals(method.Name, "GetAdded", StringComparison.Ordinal));
                Assert.Contains(
                    probeType.Methods,
                    method => string.Equals(method.Name, "Sentinel", StringComparison.Ordinal) &&
                        method.MetadataToken == 0x06_00_0001);

                // Probe 6, typed evidence gap for this surface: with the added field invisible, no ClrStaticField
                // exists to supply the added slot's address, so the storage location must be answered by a later
                // slice from the runtime's edit structures rather than from this host surface.
            }
            finally
            {
                runtime.Dispose();
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }

            if (Directory.Exists(payloadDirectory))
            {
                Directory.Delete(payloadDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Measures the physical lineage chain of two stacked generations from their delta module rows alone: the
    /// generation numbers, the shared Mvid, the distinct edit identifiers, and the base-identifier pairing that
    /// joins each generation to its predecessor.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Stacked_generation_lineage_is_measured_from_the_delta_module_rows()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-stacked-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        try
        {
            EncDeltaCompiler.WriteStackedPayload(payloadDirectory);
            Guid baselineMvid;
            using (var stream = File.OpenRead(
                Path.Combine(payloadDirectory, "PhoenixInspect.EncFixtureBaseline.dll")))
            using (var peReader = new PEReader(stream))
            {
                var baselineReader = peReader.GetMetadataReader();
                baselineMvid = baselineReader.GetGuid(baselineReader.GetModuleDefinition().Mvid);
            }

            using var oneProvider = MetadataReaderProvider.FromMetadataImage(
                [.. File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.metadata-delta"))]);
            using var twoProvider = MetadataReaderProvider.FromMetadataImage(
                [.. File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-2.metadata-delta"))]);
            var one = oneProvider.GetMetadataReader();
            var two = twoProvider.GetMetadataReader();
            var oneModule = one.GetModuleDefinition();
            var twoModule = two.GetModuleDefinition();

            // Measured lineage: each generation shares the baseline Mvid and carries its own distinct nonempty
            // edit identifier; generation one's base identifier is empty because generation zero has no edit
            // identifier at all, and generation two's base identifier equals generation one's edit identifier
            // exactly. A lineage-chain join therefore pairs on (BaseGenerationId == predecessor's GenerationId)
            // with an empty-identifier boundary condition at the chain root.
            Assert.Equal(1, oneModule.Generation);
            Assert.Equal(2, twoModule.Generation);
            Assert.Equal(baselineMvid, one.GetGuid(oneModule.Mvid));
            Assert.Equal(baselineMvid, two.GetGuid(twoModule.Mvid));
            var generationOneId = one.GetGuid(oneModule.GenerationId);
            var generationTwoId = two.GetGuid(twoModule.GenerationId);
            Assert.NotEqual(Guid.Empty, generationOneId);
            Assert.NotEqual(Guid.Empty, generationTwoId);
            Assert.NotEqual(generationOneId, generationTwoId);
            Assert.Equal(Guid.Empty, one.GetGuid(oneModule.BaseGenerationId));
            Assert.Equal(generationOneId, two.GetGuid(twoModule.BaseGenerationId));
        }
        finally
        {
            if (Directory.Exists(payloadDirectory))
            {
                Directory.Delete(payloadDirectory, recursive: true);
            }
        }
    }

    private static int FindSentinelToken(MetadataReader reader)
    {
        foreach (var handle in reader.MethodDefinitions)
        {
            if (string.Equals(
                reader.GetString(reader.GetMethodDefinition(handle).Name),
                "Sentinel",
                StringComparison.Ordinal))
            {
                return MetadataTokens.GetToken(handle);
            }
        }

        throw new InvalidOperationException("The baseline must declare the sentinel method.");
    }
}
