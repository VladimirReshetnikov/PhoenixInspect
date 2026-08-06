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

    /// <summary>
    /// Measures whether the applied generation's three delta blobs are physically present in a full dump of the
    /// edited process, and where: each found copy's file offset is mapped back to its virtual address through the
    /// dump's own memory-range directory and classified against the managed heap's segments.
    /// </summary>
    /// <remarks>
    /// The fixture collects the transient payload arrays before pausing, so a surviving copy is the runtime's own
    /// retained one. The dump's memory ranges are read from the minidump header and Memory64List stream directly,
    /// because the pinned host reader exposes no address-space enumeration.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Applied_delta_blobs_are_located_in_the_edited_process_dump()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-reach-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-reach-{Guid.NewGuid():N}.dmp");
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
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var dumpBytes = File.ReadAllBytes(dumpPath);
            var ranges = ReadMemory64Ranges(dumpBytes);
            Assert.NotEmpty(ranges);

            using var dataTarget = DataTarget.LoadDump(
                dumpPath,
                new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
            var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            try
            {
                var report = new List<string>();
                foreach (var blobName in new[]
                         {
                             "generation-1.metadata-delta",
                             "generation-1.il-delta",
                             "generation-1.pdb-delta",
                         })
                {
                    var pattern = File.ReadAllBytes(Path.Combine(payloadDirectory, blobName));
                    Assert.True(pattern.Length > 8, $"{blobName} must be a substantive pattern.");
                    var addresses = new List<string>();
                    var searchOffset = 0;
                    while (true)
                    {
                        var found = dumpBytes.AsSpan(searchOffset).IndexOf(pattern);
                        if (found < 0)
                        {
                            break;
                        }

                        var fileOffset = searchOffset + found;
                        var address = MapFileOffsetToAddress(ranges, fileOffset);
                        var segment = address is { } mapped ? runtime.Heap.GetSegmentByAddress(mapped) : null;
                        addresses.Add(
                            address is { } value
                                ? $"0x{value:x}:{(segment is null ? "native" : "gc-heap")}"
                                : $"file:0x{fileOffset:x}:unmapped");
                        searchOffset = fileOffset + 1;
                    }

                    report.Add($"{blobName}({pattern.Length}b)=[{string.Join(", ", addresses)}]");
                }

                // Measured, with the applying frame dead and two forced blocking collections before the pause: the
                // metadata and Portable-PDB delta blobs are found only inside managed-heap ranges — dead-object
                // residue of this process's own payload arrays, whose liveness this probe does not claim — and no
                // byte-identical copy of either exists anywhere else in the captured address space. The runtime
                // therefore integrates the metadata delta into its own structures rather than retaining the blob,
                // so delta acquisition from a dump cannot rely on locating the original bytes; it must read the
                // runtime's edit structures. The eleven-byte IL delta is too short for uniqueness claims and is
                // recorded, not pinned, beyond its deterministic length.
                Assert.Equal(444, File.ReadAllBytes(
                    Path.Combine(payloadDirectory, "generation-1.metadata-delta")).Length);
                Assert.Equal(11, File.ReadAllBytes(
                    Path.Combine(payloadDirectory, "generation-1.il-delta")).Length);
                Assert.Equal(356, File.ReadAllBytes(
                    Path.Combine(payloadDirectory, "generation-1.pdb-delta")).Length);
                Assert.DoesNotContain(":native", report[0], StringComparison.Ordinal);
                Assert.DoesNotContain(":unmapped", report[0], StringComparison.Ordinal);
                Assert.DoesNotContain(":native", report[2], StringComparison.Ordinal);
                Assert.DoesNotContain(":unmapped", report[2], StringComparison.Ordinal);
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
    /// Measures where the edited method's effective IL physically lives — inside the mapped base image or in
    /// edit-allocated memory — and whether the pinned runtime's contract-descriptor vocabulary names the edit
    /// structures a later detection stride would need to read.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Edited_method_effective_body_and_descriptor_vocabulary_are_measured()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-body-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-body-{Guid.NewGuid():N}.dmp");
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
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var dumpBytes = File.ReadAllBytes(dumpPath);
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
                var sentinel = Assert.Single(
                    probeType.Methods,
                    method => string.Equals(method.Name, "Sentinel", StringComparison.Ordinal));

                var ilInfo = sentinel.GetILInfo();
                var bodyBytes = "<unread>";
                if (ilInfo is not null && ilInfo.Address != 0)
                {
                    var buffer = new byte[Math.Min(16, Math.Max(ilInfo.Length, 8))];
                    if (dataTarget.DataReader.Read(ilInfo.Address, buffer) == buffer.Length)
                    {
                        bodyBytes = Convert.ToHexString(buffer);
                    }
                }

                // Probe 7, host-surface half, measured: the runtime surface resolves the method's IL from the
                // mapped base image — the address sits inside the module extent and the bytes are the
                // generation-zero body loading the pre-edit sentinel — even though this process provably executed
                // the generation-one body before pausing. The effective edited body is not reachable through this
                // surface; like the census and metadata surfaces before it, the IL surface shows the pre-edit
                // world.
                Assert.NotNull(ilInfo);
                Assert.Equal(6, ilInfo.Length);
                Assert.InRange(ilInfo.Address, module.ImageBase, module.ImageBase + (ulong)module.Size - 1);
                Assert.StartsWith("2001C06E452A", bodyBytes, StringComparison.Ordinal);

                // Probe 3 direction, measured over the captured runtime binary: the pinned contract-descriptor
                // vocabulary names no edit structure — neither the edit module class nor an applied-changes count
                // appears anywhere in the dump's bytes — while the descriptor's DynamicMetadata field name does.
                // Applied-state detection therefore cannot come from the descriptor's declared vocabulary; the
                // remaining candidates are non-contract runtime structures and the dynamic-metadata field's
                // behavior over an edited module, both open for a later slice.
                Assert.True(
                    dumpBytes.AsSpan().IndexOf("EditAndContinueModule"u8) < 0,
                    "The pinned runtime's captured bytes must not name the edit module class.");
                Assert.True(
                    dumpBytes.AsSpan().IndexOf("ApplyChangesCount"u8) < 0,
                    "The pinned runtime's captured bytes must not name an applied-changes count.");
                Assert.True(
                    dumpBytes.AsSpan().IndexOf("DynamicMetadata"u8) >= 0,
                    "The pinned descriptor vocabulary must name the dynamic-metadata field.");
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
    /// Proves what a filtered capture physically loses: the same paused edited process is captured both fully and
    /// with the normal filtered type, and the delta copies locatable in the full capture are gone from the
    /// filtered one, so a filtered dump can only ever yield the typed unavailable answer for delta evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Filtered_capture_of_the_edited_process_loses_the_delta_evidence()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-filter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var fullDumpPath = Path.Combine(Path.GetTempPath(), $"enc-filter-full-{Guid.NewGuid():N}.dmp");
        var normalDumpPath = Path.Combine(Path.GetTempPath(), $"enc-filter-normal-{Guid.NewGuid():N}.dmp");
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
                DumpWriter.WriteFullDump(target.Pid, fullDumpPath);
                DumpWriter.WriteNormalDump(target.Pid, normalDumpPath);
            }

            var fullBytes = File.ReadAllBytes(fullDumpPath);
            var normalBytes = File.ReadAllBytes(normalDumpPath);
            Assert.True(
                normalBytes.Length < fullBytes.Length,
                "The filtered capture must be materially smaller than the full capture.");

            var metadataDelta = File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.metadata-delta"));
            var pdbDelta = File.ReadAllBytes(Path.Combine(payloadDirectory, "generation-1.pdb-delta"));

            // The full capture holds the locatable residue the reachability probe measured; the filtered capture
            // holds no copy of either substantive delta blob, so delta evidence over a filtered dump is not
            // degraded but absent, and the only honest disposition there is the typed unavailable one.
            Assert.True(fullBytes.AsSpan().IndexOf(metadataDelta) >= 0);
            Assert.True(fullBytes.AsSpan().IndexOf(pdbDelta) >= 0);
            Assert.True(normalBytes.AsSpan().IndexOf(metadataDelta) < 0);
            Assert.True(normalBytes.AsSpan().IndexOf(pdbDelta) < 0);
        }
        finally
        {
            foreach (var path in new[] { fullDumpPath, normalDumpPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            if (Directory.Exists(payloadDirectory))
            {
                Directory.Delete(payloadDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Hunts the applied-state detector in non-contract candidates: measures how the descriptor declares the
    /// module's dynamic-metadata field and compares that field's pointer between the edited payload module and an
    /// unedited module of the same dump.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Dynamic_metadata_field_is_measured_over_edited_and_unedited_modules()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-dyn-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-dyn-{Guid.NewGuid():N}.dmp");
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
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var dumpBytes = File.ReadAllBytes(dumpPath);
            var marker = "\"DynamicMetadata\""u8;
            var found = dumpBytes.AsSpan().IndexOf(marker);
            Assert.True(found >= 0, "The descriptor vocabulary names the dynamic-metadata field.");
            var start = Math.Max(0, found - 120);
            var snippet = System.Text.Encoding.ASCII.GetString(
                dumpBytes.AsSpan(start, Math.Min(360, dumpBytes.Length - start)));

            using var dataTarget = DataTarget.LoadDump(
                dumpPath,
                new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
            var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
            try
            {
                var edited = Assert.Single(
                    runtime.EnumerateModules(),
                    candidate => candidate.Name?.EndsWith(
                        "PhoenixInspect.EncFixtureBaseline.dll",
                        StringComparison.OrdinalIgnoreCase) == true);
                var unedited = Assert.Single(
                    runtime.EnumerateModules(),
                    candidate => candidate.Name?.EndsWith(
                        "PhoenixInspect.EncTestTarget.dll",
                        StringComparison.OrdinalIgnoreCase) == true);
                GC.KeepAlive(snippet);

                var buffer = new byte[sizeof(ulong)];
                ulong ReadPointerAt(ulong address)
                {
                    Assert.Equal(buffer.Length, dataTarget.DataReader.Read(address, buffer));
                    return BitConverter.ToUInt64(buffer);
                }

                uint ReadUInt32At(ulong address)
                {
                    Assert.Equal(buffer.Length, dataTarget.DataReader.Read(address, buffer));
                    return BitConverter.ToUInt32(buffer);
                }

                // Measured: the dynamic-metadata pointer is null for both modules, so that descriptor field does
                // not carry edit state and is a recorded dead end.
                Assert.Equal(0ul, ReadPointerAt(edited.Address + 744));
                Assert.Equal(0ul, ReadPointerAt(unedited.Address + 744));

                // The three-way flags comparison separates enablement from applied edits: the edited module, the
                // edit-enabled-but-never-edited comparator, and the plain optimized module.
                var comparator = Assert.Single(
                    runtime.EnumerateModules(),
                    candidate => candidate.Name?.EndsWith(
                        "PhoenixInspect.EncFixtureUnedited.dll",
                        StringComparison.OrdinalIgnoreCase) == true);
                // Measured three ways — the edited module, an edit-enabled comparator that was loaded and USED but
                // never edited, and a plain optimized module: the declared flags word marks enablement only, with
                // the edited module and the used comparator byte-identical there, so flags can ground a sound
                // conservative refusal (an enabled module's edits can never be excluded) but not applied-state
                // detection.
                var editedFlags = ReadUInt32At(edited.Address + 208);
                var comparatorFlags = ReadUInt32At(comparator.Address + 208);
                var uneditedFlags = ReadUInt32At(unedited.Address + 208);
                Assert.Equal(0x9019u, editedFlags);
                Assert.Equal(0x9019u, comparatorFlags);
                Assert.Equal(0x8811u, uneditedFlags);

                // The applied-state detector candidate: an undeclared Module counter one pointer past the declared
                // dynamic-metadata field reads one on the used-but-unedited comparator and two on the module with
                // one applied generation — one plus the applied-generation count. It survives the use control, so
                // the difference is caused by the edit; its confirmation over a stacked-generation profile, and
                // its version fragility as a non-contract offset, are recorded for the next slice.
                Assert.Equal(2ul, ReadPointerAt(edited.Address + 752));
                Assert.Equal(1ul, ReadPointerAt(comparator.Address + 752));

                // Secondary edit signatures, recorded rather than relied on: the declared per-module lookup maps
                // for TypeRef, manifest module references, and MethodDef remain unallocated on the used comparator
                // yet allocated on the edited module — consistent with the delta extending reference tables — but
                // a richer unedited workload could allocate them too, so they are corroboration, not detection.
                Assert.NotEqual(0ul, ReadPointerAt(edited.Address + 8));
                Assert.Equal(0ul, ReadPointerAt(comparator.Address + 8));
                Assert.NotEqual(0ul, ReadPointerAt(edited.Address + 376));
                Assert.Equal(0ul, ReadPointerAt(comparator.Address + 376));
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
    /// Confirms the applied-state counter tracks the generation count: over a process with two stacked applied
    /// generations, the edited module's counter reads three — one plus the applied generations — while the used
    /// unedited comparator stays at one.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Stacked_generations_advance_the_applied_state_counter()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-count-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-count-{Guid.NewGuid():N}.dmp");
        try
        {
            EncDeltaCompiler.WriteStackedPayload(payloadDirectory);
            using (var target = TestTargetRunner.StartAndWaitReady(
                W8ShapeTargetPaths.RequireArtifact(
                    W8ShapeTargetPaths.ResolveExecutable("PhoenixInspect.EncTestTarget")),
                ["--truth-gate", "enc-stacked", "--payload", payloadDirectory],
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
                var edited = Assert.Single(
                    runtime.EnumerateModules(),
                    candidate => candidate.Name?.EndsWith(
                        "PhoenixInspect.EncFixtureBaseline.dll",
                        StringComparison.OrdinalIgnoreCase) == true);
                var comparator = Assert.Single(
                    runtime.EnumerateModules(),
                    candidate => candidate.Name?.EndsWith(
                        "PhoenixInspect.EncFixtureUnedited.dll",
                        StringComparison.OrdinalIgnoreCase) == true);

                var buffer = new byte[sizeof(ulong)];
                ulong ReadPointerAt(ulong address)
                {
                    Assert.Equal(buffer.Length, dataTarget.DataReader.Read(address, buffer));
                    return BitConverter.ToUInt64(buffer);
                }

                Assert.Equal(3ul, ReadPointerAt(edited.Address + 752));
                Assert.Equal(1ul, ReadPointerAt(comparator.Address + 752));
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

    /// <summary>One captured memory range of the dump: its virtual start, size, and position in the file.</summary>
    private readonly record struct DumpMemoryRange(ulong StartAddress, ulong Size, ulong FileOffset);

    /// <summary>Reads the dump's Memory64List ranges directly from the minidump header and stream directory.</summary>
    private static ImmutableArray<DumpMemoryRange> ReadMemory64Ranges(byte[] dumpBytes)
    {
        Assert.Equal(0x504D_444Du, BitConverter.ToUInt32(dumpBytes, 0));
        var streamCount = BitConverter.ToUInt32(dumpBytes, 8);
        var directoryOffset = BitConverter.ToUInt32(dumpBytes, 12);
        for (var index = 0u; index < streamCount; index++)
        {
            var entryOffset = checked((int)(directoryOffset + (index * 12)));
            if (BitConverter.ToUInt32(dumpBytes, entryOffset) != 9)
            {
                continue;
            }

            var listOffset = checked((int)BitConverter.ToUInt32(dumpBytes, entryOffset + 8));
            var rangeCount = BitConverter.ToUInt64(dumpBytes, listOffset);
            var dataOffset = BitConverter.ToUInt64(dumpBytes, listOffset + 8);
            var builder = ImmutableArray.CreateBuilder<DumpMemoryRange>(checked((int)rangeCount));
            for (var range = 0ul; range < rangeCount; range++)
            {
                var rangeOffset = checked((int)(listOffset + 16 + ((long)range * 16)));
                var start = BitConverter.ToUInt64(dumpBytes, rangeOffset);
                var size = BitConverter.ToUInt64(dumpBytes, rangeOffset + 8);
                builder.Add(new DumpMemoryRange(start, size, dataOffset));
                dataOffset = checked(dataOffset + size);
            }

            return builder.MoveToImmutable();
        }

        return [];
    }

    private static ulong? MapFileOffsetToAddress(ImmutableArray<DumpMemoryRange> ranges, long fileOffset)
    {
        foreach (var range in ranges)
        {
            if ((ulong)fileOffset >= range.FileOffset && (ulong)fileOffset < range.FileOffset + range.Size)
            {
                return range.StartAddress + ((ulong)fileOffset - range.FileOffset);
            }
        }

        return null;
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
