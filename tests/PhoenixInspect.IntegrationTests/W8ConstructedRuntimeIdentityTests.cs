using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PhoenixInspect.Host.Dump.ClrMD;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves W8 construction identity from one real full dump without interpreting runtime display names.
/// </summary>
/// <remarks>
/// This W8.1 gate uses an SRM metadata oracle only for expected tokens. The runtime candidates originate from
/// the module's descriptor-driven available-type table and remain keyed by exact type-handle address.
/// </remarks>
public sealed class W8ConstructedRuntimeIdentityTests
{
    private const string TargetNamespace = "PhoenixInspect.W8TestTarget";
    private const string AliasNamespace = "PhoenixInspect.W8AliasTarget";
    private const int MaximumMetadataBytes = 16 * 1_024 * 1_024;
    private const int MaximumRuntimeModules = 256;
    private const int MaximumStaticFields = 256;
    private const int MaximumTypeTopologyDepth = 16;

    /// <summary>
    /// Proves class, value-type, interface, and nested constructions retain distinct handles, exact ordered
    /// arguments, construction-specific static slots and values, and byte-identical close/reopen observations.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8ConstructedRuntimeIdentityV1")]
    public void Full_dump_descriptor_proves_exact_ordered_constructions_and_distinct_storage()
    {
        var executable = RequireArtifact(W8TestTargetPaths.ResolveExecutable());
        var assembly = RequireArtifact(W8TestTargetPaths.ResolveAssembly());
        var aliasAssembly = RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly());
        var catalog = W8MetadataCatalog.Read(assembly, aliasAssembly);
        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"w8-constructed-runtime-{Guid.NewGuid():N}.dmp");

        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                executable,
                ["--truth-gate", "generic-frame"],
                isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var first = Observe(dumpPath, catalog);
            var reopened = Observe(dumpPath, catalog);

            Assert.Equal(first.DescriptorAddress, reopened.DescriptorAddress);
            Assert.Equal(first.DescriptorJsonSha256, reopened.DescriptorJsonSha256);
            Assert.Equal(first.PointerDataSha256, reopened.PointerDataSha256);
            Assert.Equal(first.ModuleAddress, reopened.ModuleAddress);
            AssertCanonicalReplay(first.CanonicalLines, reopened.CanonicalLines);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static void AssertCanonicalReplay(
        ImmutableArray<string> expected,
        ImmutableArray<string> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.True(
                string.Equals(expected[index], actual[index], StringComparison.Ordinal),
                $"Canonical replay differs at line {index}:{Environment.NewLine}" +
                $"expected: {expected[index]}{Environment.NewLine}" +
                $"actual:   {actual[index]}");
        }
    }

    private static W8ConstructionObservation Observe(
        string dumpPath,
        W8MetadataCatalog catalog)
    {
        var options = new DataTargetOptions
        {
            CacheOptions = new CacheOptions
            {
                MaxDumpCacheSize = 256L * 1_024 * 1_024,
                CacheStackRoots = false,
                CacheStackTraces = false,
            },
            FileLocator = ClrmdOfflineFileLocator.Instance,
        };
        using var dataTarget = DataTarget.LoadDump(dumpPath, options);
        var clrInfo = Assert.Single(dataTarget.ClrVersions);
        Assert.Equal(10, clrInfo.Version.Major);
        using var runtime = clrInfo.CreateRuntime();
        var modules = runtime.EnumerateModules().Take(MaximumRuntimeModules + 1).ToArray();
        Assert.True(
            modules.Length <= MaximumRuntimeModules,
            $"Runtime-module traversal exceeded {MaximumRuntimeModules}; cap-plus-one observed {modules.Length}.");
        var targetModule = Assert.Single(modules, static candidate => string.Equals(
            Path.GetFileName(candidate.Name),
            W8TestTargetPaths.AssemblyFileName,
            StringComparison.OrdinalIgnoreCase));
        var aliasModules = modules.Where(static candidate => string.Equals(
            Path.GetFileName(candidate.Name),
            W8TestTargetPaths.AliasAssemblyFileName,
            StringComparison.OrdinalIgnoreCase))
            .OrderBy(static module => module.Address)
            .ToArray();
        Assert.Equal(2, aliasModules.Length);
        Assert.Equal(2, aliasModules.Select(static module => module.Address).Distinct().Count());
        Assert.Equal(2, aliasModules.Select(static module => module.AssemblyAddress).Distinct().Count());
        var aliasModuleMvids = aliasModules
            .Select(module => ReadModuleMvid(dataTarget.DataReader, module))
            .ToArray();
        Assert.Single(aliasModuleMvids.Distinct());
        var coreLibraryModule = Assert.Single(modules, static candidate => string.Equals(
            Path.GetFileName(candidate.Name),
            "System.Private.CoreLib.dll",
            StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(0UL, targetModule.Address);
        Assert.DoesNotContain(aliasModules, static module => module.Address == 0);
        Assert.NotEqual(0UL, coreLibraryModule.Address);
        var coreLibraryPath = Assert.IsType<string>(coreLibraryModule.Name);
        var int32Metadata = W8MetadataCatalog.ReadStandaloneType(
            RequireArtifact(coreLibraryPath),
            "System",
            "Int32");

        var oracle = W8CdacRuntimeConstructionOracle.Open(dataTarget, clrInfo);
        // The release/10.0 producer fixes bit zero. Its reader consumes only the pointer-width bit,
        // so this assertion freezes emitted fixture evidence without rejecting reserved-bit extensions.
        Assert.Equal(1u, oracle.Descriptor.Flags & 1u);
        Assert.Equal(sizeof(ulong), oracle.Descriptor.PointerSize);
        Assert.Equal(W8CdacEndianness.Little, oracle.Descriptor.Endianness);
        Assert.Equal(0, oracle.Descriptor.DescriptorVersion);
        Assert.Equal(1, oracle.Descriptor.LoaderContractVersion);
        Assert.Equal(1, oracle.Descriptor.RuntimeTypeSystemContractVersion);
        Assert.NotEqual(0UL, oracle.Descriptor.DescriptorAddress);
        Assert.InRange(
            oracle.Descriptor.DescriptorSize,
            1U,
            (uint)W8CdacRuntimeConstructionOracle.MaximumDescriptorBytes);

        var availableTables = modules
            .Where(static module => module.Address != 0)
            .GroupBy(static module => module.Address)
            .Select(group => oracle.ReadAvailableTypes(group.Key))
            .OrderBy(static table => table.ModuleAddress)
            .ToImmutableArray();
        Assert.NotEmpty(availableTables);
        Assert.Contains(availableTables, static table => table.TableAddress != 0);
        foreach (var table in availableTables)
        {
            Assert.Equal(table.DeclaredCount, checked((uint)table.Entries.Length));
            Assert.Equal(
                table.Entries.Length,
                table.Entries.Select(static entry => entry.ChainNodeAddress).Distinct().Count());
        }

        var candidateBuilder = ImmutableArray.CreateBuilder<W8CdacConstructionCandidate>();
        foreach (var table in availableTables)
        {
            foreach (var entry in table.Entries)
            {
                if (oracle.TryReadMethodTableIdentity(entry.TypeHandle, out var identity))
                {
                    candidateBuilder.Add(new W8CdacConstructionCandidate(
                        table.ModuleAddress,
                        table.TableAddress,
                        table.BucketCount,
                        table.DeclaredCount,
                        entry,
                        Assert.IsType<W8CdacMethodTableIdentity>(identity)));
                }
            }
        }

        var candidates = candidateBuilder.ToImmutable();
        Assert.NotEmpty(candidates);
        Assert.Equal(
            candidates.Length,
            candidates.Select(static candidate => candidate.Identity.TypeHandle).Distinct().Count());

        var requestType = Named(targetModule.Address, catalog.RequestContext);
        var batchType = Named(targetModule.Address, catalog.BatchContext);
        var valueType = Named(targetModule.Address, catalog.ValueContext);
        var coordinatorType = Named(targetModule.Address, catalog.CoordinatorContext);
        var workflowType = Named(targetModule.Address, catalog.WorkflowContext);
        var int32Type = Named(coreLibraryModule.Address, int32Metadata);
        var requestVectorType = Array(isSzArray: true, rank: 1, requestType);
        var requestMatrixType = Array(isSzArray: false, rank: 2, requestType);
        var int32MatrixType = Array(isSzArray: false, rank: 2, int32Type);

        var requestSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericSlot,
            requestType);
        var batchSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericSlot,
            batchType);
        var valueSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericSlot,
            valueType);
        var requestVectorSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericSlot,
            requestVectorType);
        var requestMatrixSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericSlot,
            requestMatrixType);
        var int32MatrixSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericSlot,
            int32MatrixType);
        var coordinatorStructSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericStructSlot,
            coordinatorType);
        var workflowInterfaceSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericInterfaceSlot,
            workflowType);
        var requestInterfaceSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.GenericInterfaceSlot,
            requestType);
        var nestedSlot = FindExactConstruction(
            oracle,
            candidates,
            targetModule.Address,
            catalog.NestedInnerSlot,
            requestType,
            batchType);
        var aliasModuleAddresses = aliasModules
            .Select(static module => module.Address)
            .ToHashSet();
        var externalSlot = Assert.Single(candidates.Where(candidate =>
            aliasModuleAddresses.Contains(candidate.Identity.ModuleAddress) &&
            candidate.Identity.TypeDefToken == catalog.ExternalSlot.TypeToken &&
            ArgumentsMatch(
                oracle,
                candidate.Identity,
                [Named(candidate.Identity.ModuleAddress, catalog.ExternalRequestContext)])));
        var defaultAliasModule = Assert.Single(
            aliasModules,
            module => module.Address == externalSlot.Identity.ModuleAddress);
        var externalType = Named(defaultAliasModule.Address, catalog.ExternalRequestContext);
        var externalInterfaceSlot = FindExactConstruction(
            oracle,
            candidates,
            defaultAliasModule.Address,
            catalog.ExternalInterfaceSlot,
            externalType);
        var collectibleSlotMatches = candidates.Where(candidate =>
        {
            if (candidate.Identity.ModuleAddress != targetModule.Address ||
                candidate.Identity.TypeDefToken != catalog.GenericSlot.TypeToken ||
                candidate.Identity.TypeArgumentHandles.Length != 1)
            {
                return false;
            }

            var argumentShape = oracle.ReadTypeShape(candidate.Identity.TypeArgumentHandles[0]);
            return argumentShape.Kind == W8CdacTypeShapeKind.MethodTable &&
                !argumentShape.IsArray &&
                aliasModuleAddresses.Contains(argumentShape.ModuleAddress) &&
                argumentShape.TypeDefToken == catalog.ExternalRequestContext.TypeToken &&
                argumentShape.TypeArgumentHandles.IsEmpty &&
                candidate.LoaderModuleAddress == argumentShape.ModuleAddress &&
                candidate.LoaderModuleAddress != candidate.Identity.ModuleAddress;
        }).ToArray();
        var collectibleSlot = Assert.Single(collectibleSlotMatches);
        var collectibleArgument = oracle.ReadTypeShape(
            Assert.Single(collectibleSlot.Identity.TypeArgumentHandles));
        var collectibleAliasModule = Assert.Single(
            aliasModules,
            module => module.Address == collectibleArgument.ModuleAddress);
        Assert.NotEqual(defaultAliasModule.Address, collectibleAliasModule.Address);
        Assert.NotEqual(defaultAliasModule.AssemblyAddress, collectibleAliasModule.AssemblyAddress);
        Assert.NotEqual(defaultAliasModule.LoaderAllocator, collectibleAliasModule.LoaderAllocator);
        Assert.NotEqual(0UL, collectibleAliasModule.LoaderAllocator);
        Assert.Equal(collectibleAliasModule.Address, collectibleSlot.LoaderModuleAddress);

        var defaultExternalRuntimeType = Assert.IsAssignableFrom<ClrType>(runtime.GetTypeByMethodTable(
            Assert.Single(externalSlot.Identity.TypeArgumentHandles)));
        var collectibleExternalRuntimeType = Assert.IsAssignableFrom<ClrType>(runtime.GetTypeByMethodTable(
            collectibleArgument.TypeHandle));
        Assert.Equal(defaultAliasModule.Address, defaultExternalRuntimeType.Module.Address);
        Assert.Equal(collectibleAliasModule.Address, collectibleExternalRuntimeType.Module.Address);
        Assert.False(defaultExternalRuntimeType.IsCollectible);
        Assert.True(collectibleExternalRuntimeType.IsCollectible);
        Assert.Equal(0UL, defaultExternalRuntimeType.AssemblyLoadContextAddress);
        Assert.NotEqual(0UL, collectibleExternalRuntimeType.AssemblyLoadContextAddress);
        Assert.NotEqual(
            defaultExternalRuntimeType.AssemblyLoadContextAddress,
            collectibleExternalRuntimeType.AssemblyLoadContextAddress);
        Assert.NotEqual(0UL, collectibleExternalRuntimeType.LoaderAllocatorHandle);

        var selected = new[]
        {
            (Label: "class-request", Candidate: requestSlot),
            (Label: "class-batch", Candidate: batchSlot),
            (Label: "class-value", Candidate: valueSlot),
            (Label: "class-request-vector", Candidate: requestVectorSlot),
            (Label: "class-request-matrix", Candidate: requestMatrixSlot),
            (Label: "class-int32-matrix", Candidate: int32MatrixSlot),
            (Label: "value-coordinator", Candidate: coordinatorStructSlot),
            (Label: "interface-workflow", Candidate: workflowInterfaceSlot),
            (Label: "interface-request", Candidate: requestInterfaceSlot),
            (Label: "nested-request-batch", Candidate: nestedSlot),
            (Label: "external-class", Candidate: externalSlot),
            (Label: "external-interface", Candidate: externalInterfaceSlot),
            (Label: "collectible-external-class", Candidate: collectibleSlot),
        };
        Assert.Equal(
            selected.Length,
            selected.Select(static item => item.Candidate.Identity.TypeHandle).Distinct().Count());
        Assert.Equal(
            requestSlot.Identity.TypeArgumentHandles[0],
            nestedSlot.Identity.TypeArgumentHandles[0]);
        Assert.Equal(
            batchSlot.Identity.TypeArgumentHandles[0],
            nestedSlot.Identity.TypeArgumentHandles[1]);
        Assert.NotEqual(
            nestedSlot.Identity.TypeArgumentHandles[0],
            nestedSlot.Identity.TypeArgumentHandles[1]);

        var reversedNested = candidates.Where(candidate =>
            candidate.Identity.ModuleAddress == targetModule.Address &&
            candidate.Identity.TypeDefToken == catalog.NestedInnerSlot.TypeToken &&
            ArgumentsMatch(
                oracle,
                candidate.Identity,
                [batchType, requestType]))
            .ToArray();
        Assert.Empty(reversedNested);

        var storage = new[]
        {
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "class-request",
                requestSlot.Identity,
                catalog.GenericSlot.SentinelFieldToken,
                0x11017A01),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "class-batch",
                batchSlot.Identity,
                catalog.GenericSlot.SentinelFieldToken,
                0x22027A01),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "class-value",
                valueSlot.Identity,
                catalog.GenericSlot.SentinelFieldToken,
                0x33037A01),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "class-request-vector",
                requestVectorSlot.Identity,
                catalog.GenericSlot.SentinelFieldToken,
                0x44047A01),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "class-request-matrix",
                requestMatrixSlot.Identity,
                catalog.GenericSlot.SentinelFieldToken,
                0x44047A02),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "class-int32-matrix",
                int32MatrixSlot.Identity,
                catalog.GenericSlot.SentinelFieldToken,
                0x44047A03),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "value-coordinator",
                coordinatorStructSlot.Identity,
                catalog.GenericStructSlot.SentinelFieldToken,
                0x55057A01),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "interface-workflow",
                workflowInterfaceSlot.Identity,
                catalog.GenericInterfaceSlot.SentinelFieldToken,
                0x66067A01),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "interface-request",
                requestInterfaceSlot.Identity,
                catalog.GenericInterfaceSlot.SentinelFieldToken,
                0x66067A02),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "nested-request-batch",
                nestedSlot.Identity,
                catalog.NestedInnerSlot.SentinelFieldToken,
                0x77077A01),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "external-class",
                externalSlot.Identity,
                catalog.ExternalSlot.SentinelFieldToken,
                unchecked((int)0xCC0C7A01)),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "external-interface",
                externalInterfaceSlot.Identity,
                catalog.ExternalInterfaceSlot.SentinelFieldToken,
                unchecked((int)0xDD0D7A01)),
            ObserveStaticInt32(
                runtime,
                dataTarget.DataReader,
                "collectible-external-class",
                collectibleSlot.Identity,
                catalog.GenericSlot.SentinelFieldToken,
                unchecked((int)0xCE0E7A01)),
        };
        Assert.Equal(
            storage.Length,
            storage.Select(static item => item.Address).Distinct().Count());

        var genericSlotCandidates = candidates
            .Where(candidate => candidate.Identity.ModuleAddress == targetModule.Address &&
                candidate.Identity.TypeDefToken == catalog.GenericSlot.TypeToken)
            .OrderBy(static candidate => candidate.Identity.TypeHandle)
            .ToArray();
        Assert.True(genericSlotCandidates.Length >= 6);
        Assert.NotEqual(
            collectibleSlot.LoaderModuleAddress,
            collectibleSlot.Identity.ModuleAddress);

        var accounting = oracle.ReadAccounting;
        var availableEntryCount = availableTables.Sum(static table => table.Entries.Length);
        Assert.True(accounting.ReadCount > availableEntryCount);
        Assert.True(accounting.ByteCount > oracle.Descriptor.DescriptorSize);

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add(
            $"descriptor|{oracle.Descriptor.DescriptorAddress:x16}|{oracle.Descriptor.Flags:x8}|" +
            $"{oracle.Descriptor.DescriptorSize}|{oracle.Descriptor.JsonSha256}|" +
            $"{oracle.Descriptor.PointerDataCount}|{oracle.Descriptor.PointerDataSha256}");
        for (var index = 0; index < aliasModules.Length; index++)
        {
            var module = aliasModules[index];
            lines.Add(
                $"alias-module|{module.Address:x16}|{module.AssemblyAddress:x16}|" +
                $"{module.LoaderAllocator:x16}|{module.MetadataAddress:x16}|" +
                $"{module.MetadataLength}|{aliasModuleMvids[index]:D}");
        }

        lines.Add(
            $"load-context|default|{defaultAliasModule.Address:x16}|" +
            $"{defaultExternalRuntimeType.AssemblyLoadContextAddress:x16}|" +
            $"{defaultExternalRuntimeType.LoaderAllocatorHandle:x16}");
        lines.Add(
            $"load-context|collectible|{collectibleAliasModule.Address:x16}|" +
            $"{collectibleExternalRuntimeType.AssemblyLoadContextAddress:x16}|" +
            $"{collectibleExternalRuntimeType.LoaderAllocatorHandle:x16}");
        foreach (var table in availableTables)
        {
            lines.Add(
                $"available|{table.ModuleAddress:x16}|{table.TableAddress:x16}|{table.BucketCount}|" +
                $"{table.DeclaredCount}|{table.Entries.Length}");
        }

        foreach (var item in selected.OrderBy(static item => item.Label, StringComparer.Ordinal))
        {
            lines.Add(FormatCandidate(item.Label, item.Candidate));
            lines.Add(
                $"topology|{item.Label}|" +
                FormatTopology(oracle, item.Candidate.Identity.TypeHandle));
        }

        foreach (var candidate in genericSlotCandidates)
        {
            lines.Add(FormatCandidate("all-class", candidate));
        }

        foreach (var item in storage.OrderBy(static item => item.Label, StringComparer.Ordinal))
        {
            lines.Add($"storage|{item.Label}|{item.Address:x16}|{item.Value:x8}");
        }

        lines.Add($"reads|{accounting.ReadCount}|{accounting.ByteCount}");
        return new W8ConstructionObservation(
            oracle.Descriptor.DescriptorAddress,
            oracle.Descriptor.JsonSha256,
            oracle.Descriptor.PointerDataSha256,
            targetModule.Address,
            lines.ToImmutable());
    }

    private static W8CdacConstructionCandidate FindExactConstruction(
        W8CdacRuntimeConstructionOracle oracle,
        ImmutableArray<W8CdacConstructionCandidate> candidates,
        ulong moduleAddress,
        W8MetadataType owner,
        params W8ExpectedTypeNode[] arguments)
    {
        var matches = candidates.Where(candidate =>
            candidate.Identity.ModuleAddress == moduleAddress &&
            candidate.Identity.TypeDefToken == owner.TypeToken &&
            ArgumentsMatch(oracle, candidate.Identity, arguments))
            .ToArray();
        return Assert.Single(matches);
    }

    private static bool ArgumentsMatch(
        W8CdacRuntimeConstructionOracle oracle,
        W8CdacMethodTableIdentity identity,
        IReadOnlyList<W8ExpectedTypeNode> arguments)
    {
        if (identity.TypeArgumentHandles.Length != arguments.Count)
        {
            return false;
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            if (!MatchesType(
                    oracle,
                    identity.TypeArgumentHandles[index],
                    arguments[index],
                    depth: 1))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesType(
        W8CdacRuntimeConstructionOracle oracle,
        ulong typeHandle,
        W8ExpectedTypeNode expected,
        int depth)
    {
        if (depth > MaximumTypeTopologyDepth)
        {
            return false;
        }

        var shape = oracle.ReadTypeShape(typeHandle);
        switch (expected)
        {
            case W8ExpectedNamedType named:
                if (shape.Kind != W8CdacTypeShapeKind.MethodTable ||
                    shape.IsArray ||
                    shape.ModuleAddress != named.ModuleAddress ||
                    shape.TypeDefToken != named.TypeDefToken ||
                    shape.TypeArgumentHandles.Length != named.TypeArguments.Length)
                {
                    return false;
                }

                for (var index = 0; index < named.TypeArguments.Length; index++)
                {
                    if (!MatchesType(
                            oracle,
                            shape.TypeArgumentHandles[index],
                            named.TypeArguments[index],
                            checked(depth + 1)))
                    {
                        return false;
                    }
                }

                return true;
            case W8ExpectedArrayType array:
                return shape.Kind == W8CdacTypeShapeKind.MethodTable &&
                    shape.IsArray &&
                    shape.IsSzArray == array.IsSzArray &&
                    shape.ArrayRank == array.Rank &&
                    MatchesType(
                        oracle,
                        shape.ElementOrParameterTypeHandle,
                        array.ElementType,
                        checked(depth + 1));
            default:
                throw new InvalidOperationException("Unknown expected W8 type topology.");
        }
    }

    private static W8StaticInt32Observation ObserveStaticInt32(
        ClrRuntime runtime,
        IMemoryReader memory,
        string label,
        W8CdacMethodTableIdentity identity,
        int fieldToken,
        int expectedValue)
    {
        var type = Assert.IsAssignableFrom<ClrType>(runtime.GetTypeByMethodTable(identity.TypeHandle));
        Assert.Equal(identity.TypeHandle, type.MethodTable);
        Assert.Equal(identity.ModuleAddress, type.Module.Address);
        Assert.Equal(identity.TypeDefToken, type.MetadataToken);

        var fields = type.StaticFields.Take(MaximumStaticFields + 1).ToArray();
        Assert.True(
            fields.Length <= MaximumStaticFields,
            $"Static-field traversal exceeded {MaximumStaticFields}; cap-plus-one observed {fields.Length}.");
        var field = Assert.Single(fields, candidate => candidate.Token == fieldToken);
        Assert.True(field.IsInitialized(type.Module.AppDomain));
        var address = field.GetAddress(type.Module.AppDomain);
        Assert.NotEqual(0UL, address);

        var bytes = new byte[sizeof(int)];
        var observed = memory.Read(address, bytes);
        Assert.Equal(bytes.Length, observed);
        var value = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        Assert.Equal(expectedValue, value);
        return new W8StaticInt32Observation(label, address, value);
    }

    private static Guid ReadModuleMvid(IMemoryReader memory, ClrModule module)
    {
        Assert.NotEqual(0UL, module.MetadataAddress);
        Assert.InRange(module.MetadataLength, 1UL, checked((ulong)MaximumMetadataBytes));
        var bytes = new byte[checked((int)module.MetadataLength)];
        var observed = memory.Read(module.MetadataAddress, bytes);
        Assert.Equal(bytes.Length, observed);
        using var provider = MetadataReaderProvider.FromMetadataImage(
            ImmutableArray.CreateRange(bytes));
        var metadata = provider.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }

    private static string FormatCandidate(string label, W8CdacConstructionCandidate candidate) =>
        $"type|{label}|{candidate.LoaderModuleAddress:x16}|{candidate.TableAddress:x16}|" +
        $"{candidate.Entry.BucketIndex}|{candidate.Entry.ChainNodeAddress:x16}|" +
        $"{candidate.Identity.TypeHandle:x16}|{candidate.Identity.ModuleAddress:x16}|" +
        $"{candidate.Identity.TypeDefToken:x8}|{candidate.Identity.PerInstInfoAddress:x16}|" +
        $"{candidate.Identity.DictionaryCount}|{candidate.Identity.FinalDictionaryAddress:x16}|" +
        string.Join(",", candidate.Identity.TypeArgumentHandles.Select(static handle => $"{handle:x16}"));

    private static string FormatTopology(
        W8CdacRuntimeConstructionOracle oracle,
        ulong typeHandle) => FormatTopology(
            oracle,
            typeHandle,
            depth: 0,
            new HashSet<ulong>());

    private static string FormatTopology(
        W8CdacRuntimeConstructionOracle oracle,
        ulong typeHandle,
        int depth,
        HashSet<ulong> path)
    {
        if (depth > MaximumTypeTopologyDepth || !path.Add(typeHandle))
        {
            throw new InvalidOperationException("The runtime type topology exceeded its depth or cycle bound.");
        }

        try
        {
            var shape = oracle.ReadTypeShape(typeHandle);
            if (shape.Kind == W8CdacTypeShapeKind.TypeDescriptor)
            {
                var parameter = shape.ElementOrParameterTypeHandle == 0
                    ? string.Empty
                    : $"[{FormatTopology(oracle, shape.ElementOrParameterTypeHandle, checked(depth + 1), path)}]";
                return $"desc:{shape.TypeHandle:x16}:{shape.ElementType:x2}{parameter}";
            }

            if (shape.IsArray)
            {
                var arrayKind = shape.IsSzArray ? "sz" : $"md{shape.ArrayRank}";
                return $"{arrayKind}:{shape.TypeHandle:x16}[" +
                    FormatTopology(
                        oracle,
                        shape.ElementOrParameterTypeHandle,
                        checked(depth + 1),
                        path) +
                    "]";
            }

            var arguments = shape.TypeArgumentHandles.Length == 0
                ? string.Empty
                : "<" + string.Join(
                    ",",
                    shape.TypeArgumentHandles.Select(argument => FormatTopology(
                        oracle,
                        argument,
                        checked(depth + 1),
                        path))) + ">";
            return $"mt:{shape.TypeHandle:x16}:{shape.ModuleAddress:x16}:{shape.TypeDefToken:x8}{arguments}";
        }
        finally
        {
            _ = path.Remove(typeHandle);
        }
    }

    private static W8ExpectedNamedType Named(
        ulong moduleAddress,
        W8MetadataType type,
        params W8ExpectedTypeNode[] arguments) => new(
            moduleAddress,
            type.TypeToken,
            arguments.ToImmutableArray());

    private static W8ExpectedArrayType Array(
        bool isSzArray,
        int rank,
        W8ExpectedTypeNode elementType) => new(isSzArray, rank, elementType);

    private abstract record W8ExpectedTypeNode;

    private sealed record W8ExpectedNamedType(
        ulong ModuleAddress,
        int TypeDefToken,
        ImmutableArray<W8ExpectedTypeNode> TypeArguments) : W8ExpectedTypeNode;

    private sealed record W8ExpectedArrayType(
        bool IsSzArray,
        int Rank,
        W8ExpectedTypeNode ElementType) : W8ExpectedTypeNode;

    private static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"Required W8 artifact was not found: {path}");
        return path;
    }

    private sealed record W8ConstructionObservation(
        ulong DescriptorAddress,
        string DescriptorJsonSha256,
        string PointerDataSha256,
        ulong ModuleAddress,
        ImmutableArray<string> CanonicalLines);

    private sealed record W8StaticInt32Observation(string Label, ulong Address, int Value);

    private sealed record W8MetadataType(int TypeToken, int SentinelFieldToken);

    private sealed record W8MetadataCatalog(
        W8MetadataType GenericSlot,
        W8MetadataType GenericStructSlot,
        W8MetadataType GenericInterfaceSlot,
        W8MetadataType NestedInnerSlot,
        W8MetadataType RequestContext,
        W8MetadataType BatchContext,
        W8MetadataType CoordinatorContext,
        W8MetadataType WorkflowContext,
        W8MetadataType ValueContext,
        W8MetadataType ExternalSlot,
        W8MetadataType ExternalInterfaceSlot,
        W8MetadataType ExternalRequestContext)
    {
        internal static W8MetadataCatalog Read(string assemblyPath, string aliasAssemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var metadata = peReader.GetMetadataReader();

            var genericSlot = ReadType(
                metadata,
                FindTopLevelType(metadata, TargetNamespace, "GenericSlot`1"),
                "Sentinel");
            var genericStructSlot = ReadType(
                metadata,
                FindTopLevelType(metadata, TargetNamespace, "GenericStructSlot`1"),
                "Sentinel");
            var genericInterfaceSlot = ReadType(
                metadata,
                FindTopLevelType(metadata, TargetNamespace, "IGenericInterfaceSlot`1"),
                "Sentinel");
            var outer = FindTopLevelType(metadata, TargetNamespace, "Outer`1");
            var middle = FindNestedType(metadata, outer, "Middle");
            var inner = FindNestedType(metadata, middle, "Inner`1");
            using var aliasStream = File.OpenRead(aliasAssemblyPath);
            using var aliasPeReader = new PEReader(aliasStream, PEStreamOptions.LeaveOpen);
            var aliasMetadata = aliasPeReader.GetMetadataReader();
            return new W8MetadataCatalog(
                genericSlot,
                genericStructSlot,
                genericInterfaceSlot,
                ReadType(metadata, inner, "Count"),
                ReadType(metadata, FindTopLevelType(metadata, TargetNamespace, "RequestContext"), null),
                ReadType(metadata, FindTopLevelType(metadata, TargetNamespace, "BatchContext"), null),
                ReadType(metadata, FindTopLevelType(metadata, TargetNamespace, "CoordinatorContext"), null),
                ReadType(metadata, FindTopLevelType(metadata, TargetNamespace, "WorkflowContext"), null),
                ReadType(metadata, FindTopLevelType(metadata, TargetNamespace, "ValueContext"), null),
                ReadType(
                    aliasMetadata,
                    FindTopLevelType(aliasMetadata, AliasNamespace, "ExternalSlot\u00601"),
                    "Sentinel"),
                ReadType(
                    aliasMetadata,
                    FindTopLevelType(aliasMetadata, AliasNamespace, "IExternalInterfaceSlot\u00601"),
                    "Sentinel"),
                ReadType(
                    aliasMetadata,
                    FindTopLevelType(aliasMetadata, AliasNamespace, "ExternalRequestContext"),
                    null));
        }

        internal static W8MetadataType ReadStandaloneType(
            string assemblyPath,
            string typeNamespace,
            string typeName)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var metadata = peReader.GetMetadataReader();
            return ReadType(
                metadata,
                FindTopLevelType(metadata, typeNamespace, typeName),
                null);
        }

        private static W8MetadataType ReadType(
            MetadataReader metadata,
            TypeDefinitionHandle handle,
            string? fieldName)
        {
            var fieldToken = 0;
            if (fieldName is not null)
            {
                var definition = metadata.GetTypeDefinition(handle);
                var fields = definition.GetFields()
                    .Where(candidate => string.Equals(
                        metadata.GetString(metadata.GetFieldDefinition(candidate).Name),
                        fieldName,
                        StringComparison.Ordinal))
                    .ToArray();
                fieldToken = MetadataTokens.GetToken(Assert.Single(fields));
            }

            return new W8MetadataType(MetadataTokens.GetToken(handle), fieldToken);
        }

        private static TypeDefinitionHandle FindTopLevelType(
            MetadataReader metadata,
            string expectedNamespace,
            string expectedName)
        {
            var matches = metadata.TypeDefinitions.Where(handle =>
            {
                var definition = metadata.GetTypeDefinition(handle);
                return definition.GetDeclaringType().IsNil &&
                    string.Equals(
                        metadata.GetString(definition.Namespace),
                        expectedNamespace,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        metadata.GetString(definition.Name),
                        expectedName,
                        StringComparison.Ordinal);
            }).ToArray();
            return Assert.Single(matches);
        }

        private static TypeDefinitionHandle FindNestedType(
            MetadataReader metadata,
            TypeDefinitionHandle declaringType,
            string expectedName)
        {
            var matches = metadata.GetTypeDefinition(declaringType)
                .GetNestedTypes()
                .Where(handle => string.Equals(
                    metadata.GetString(metadata.GetTypeDefinition(handle).Name),
                    expectedName,
                    StringComparison.Ordinal))
                .ToArray();
            return Assert.Single(matches);
        }
    }
}
