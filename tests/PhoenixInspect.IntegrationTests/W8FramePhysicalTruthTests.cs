using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Microsoft.Diagnostics.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Probes selected-frame, variable-location, and closed generic-context evidence in real W8 full dumps.
/// </summary>
/// <remarks>
/// This draft W8.1 gate is intentionally independent of product binding and evaluation. It selects frames by raw
/// module metadata and tokens, detaches native context bytes before closing ClrMD, and treats legacy frame/value
/// interfaces only as an evidence probe. No result in this suite is a public frame-value contract.
/// </remarks>
public sealed class W8FramePhysicalTruthTests
{
    private const int MaximumRuntimeThreads = 4_096;
    private const int MaximumFramesPerThread = 256;
    private const int MaximumRuntimeModules = 256;
    private const int MaximumMetadataBytes = 64 * 1_024 * 1_024;
    private const int MaximumActiveScopes = 256;
    private const int MaximumActiveVariables = 256;
    private const int MaximumActiveConstants = 256;
    private const int MaximumMemoryHomeBytes = 64;
    private const int MaximumRuntimeTypeDepth = 16;
    private const int MaximumRuntimeTypeArguments = 64;
    private const int MaximumFrameStringCharacters = 256;
    private const int NotImplementedHResult = unchecked((int)0x80004001);
    private const int NoInterfaceHResult = unchecked((int)0x80004002);
    private const int BadFormatHResult = unchecked((int)0x8007000b);
    private static readonly ImmutableArray<string> Profiles =
        [
            "generic-frame",
            "method-generic-frame",
            "optimized-frame",
            "slot-reuse-frame",
            "lexical-frame",
            "query-frame",
        ];

    private readonly ITestOutputHelper output;

    /// <summary>Creates the draft physical-truth suite with a canonical, payload-bounded diagnostic channel.</summary>
    /// <param name="output">The xUnit output channel used for one canonical disposition matrix per profile.</param>
    public W8FramePhysicalTruthTests(ITestOutputHelper output) => this.output = output;

    /// <summary>
    /// Proves six meaningful selected frames retain exact raw frame/code/context attribution across close/reopen and
    /// freezes each legacy argument, local, declaring-type argument, method argument, and exact-token disposition.
    /// </summary>
    /// <remarks>
    /// The draft caveat is material: a register location without a register identity, a zero/split location, a failed
    /// token query, or a canonical/zero substitute is never promoted to exact evidence by this test.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8FramePhysicalTruthV1")]
    public void Full_dumps_freeze_selected_frame_context_locations_and_generic_arguments_without_fallback()
    {
        var executable = RequireArtifact(W8TestTargetPaths.ResolveExecutable());
        var assemblyPath = RequireArtifact(W8TestTargetPaths.ResolveAssembly());
        var pdbPath = RequireArtifact(W8TestTargetPaths.ResolvePortablePdb());
        var catalog = W8FrameMetadataCatalog.Read(assemblyPath, pdbPath);

        foreach (var profile in Profiles)
        {
            var dumpPath = Path.Combine(
                Path.GetTempPath(),
                $"w8-frame-{profile}-{Guid.NewGuid():N}.dmp");
            try
            {
                using (var target = TestTargetRunner.StartAndWaitReady(
                    executable,
                    ["--truth-gate", profile],
                    isolatedDirectory: null))
                {
                    DumpWriter.WriteFullDump(target.Pid, dumpPath);
                }

                var expected = catalog.Profiles[profile];
                var first = Observe(dumpPath, catalog, expected);
                var reopened = Observe(dumpPath, catalog, expected);

                Assert.Equal(profile, first.Profile);
                Assert.Equal(catalog.ModuleVersionId, first.ModuleVersionId);
                Assert.Equal(expected.MethodDefinitionToken, first.MethodDefinitionToken);
                Assert.Equal(expected.DeclaringTypeDefinitionToken, first.DeclaringTypeDefinitionToken);
                Assert.NotEqual(0UL, first.ThreadAddress);
                Assert.True(first.ManagedThreadId > 0);
                Assert.NotEqual(0U, first.OSThreadId);
                Assert.NotEqual(0UL, first.InstructionPointer);
                Assert.NotEqual(0UL, first.StackPointer);
                Assert.NotEqual(0UL, first.MethodDescriptor);
                Assert.NotEqual(0UL, first.NativeCode);
                Assert.True(first.IlOffset >= 0);
                Assert.NotEmpty(first.ContextBytes);
                Assert.Equal(ExpectedContextSize(first.Architecture), first.ContextBytes.Length);
                Assert.Equal(Sha256(first.ContextBytes.AsSpan()), first.ContextSha256);
                Assert.Contains(
                    first.IlToNativeMap,
                    item => item.StartAddress <= first.InstructionPointer &&
                        first.InstructionPointer < item.EndAddress);
                Assert.Equal(expected.Parameters.Length, first.Parameters.Length);
                Assert.True(first.ActiveScopes.Length is > 0 and <= MaximumActiveScopes);
                Assert.True(first.ActiveVariables.Length <= MaximumActiveVariables);
                Assert.True(first.ActiveConstants.Length <= MaximumActiveConstants);
                Assert.Equal(W8FrameProbeDisposition.Exact, first.Legacy.SurfaceDisposition);
                Assert.Equal(0, first.Legacy.FrameHResult);
                Assert.Equal(expected.MethodDefinitionToken, first.Legacy.MethodDefinitionToken);
                Assert.Equal(catalog.ModuleVersionId, first.Legacy.MethodModuleVersionId);
                Assert.DoesNotContain(
                    first.Roots,
                    static root => root.LocationKind == W8FrameLocationKind.Unknown &&
                        root.Disposition == W8FrameProbeDisposition.Exact);
                Assert.DoesNotContain(
                    first.GenericArguments,
                    static argument => argument.Identity.Disposition == W8FrameProbeDisposition.Exact &&
                        (argument.Identity.TypeDefinitionToken == 0 ||
                         argument.Identity.ModuleVersionId == Guid.Empty));
                AssertGenericExpectations(first, catalog, expected);
                AssertRootCoverage(first, expected);

                Assert.Equal(first.Architecture, reopened.Architecture);
                Assert.True(
                    first.ContextBytes.SequenceEqual(reopened.ContextBytes),
                    DescribeContextDifferences(first.ContextBytes, reopened.ContextBytes));
                Assert.Equal(first.ContextSha256, reopened.ContextSha256);
                Assert.True(
                    first.CanonicalLines.SequenceEqual(reopened.CanonicalLines),
                    DescribeCanonicalDifferences(first.CanonicalLines, reopened.CanonicalLines));
                output.WriteLine(string.Join(Environment.NewLine, first.CanonicalLines));
            }
            finally
            {
                if (File.Exists(dumpPath))
                {
                    File.Delete(dumpPath);
                }
            }
        }
    }

    private static W8FrameSnapshotObservation Observe(
        string dumpPath,
        W8FrameMetadataCatalog catalog,
        W8FrameExpectedProfile expected)
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
        var module = SelectModule(runtime, dataTarget.DataReader, catalog.ModuleVersionId);
        var selected = SelectFrame(runtime, module, catalog, expected);
        var frame = selected.Frame;
        var method = Assert.IsType<ClrMethod>(frame.Method);
        var declaringType = Assert.IsAssignableFrom<ClrType>(method.Type);
        var publicContextBytes = frame.Context.ToArray();
        Assert.Equal(ExpectedContextSize(dataTarget.DataReader.Architecture), publicContextBytes.Length);
        var ilOffset = method.GetILOffset(frame.InstructionPointer);
        Assert.True(ilOffset >= 0);
        var code = ObserveCode(runtime, method, frame.InstructionPointer, ilOffset);
        var scopes = catalog.ReadActiveScopes(expected, ilOffset);
        var methodVariableNames = catalog.ReadMethodVariableNames(expected);
        Assert.True(expected.InactiveVariableNames.IsSubsetOf(methodVariableNames));
        var inactiveVariables = expected.InactiveVariableNames
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                Assert.DoesNotContain(scopes.Variables, variable =>
                    string.Equals(variable.Name, name, StringComparison.Ordinal));
                return new W8FrameLexicalVariableDisposition(
                    name,
                    W8FrameProbeDisposition.LexicallyInactive);
            })
            .ToImmutableArray();
        var legacy = W8FrameLegacyInteropProbe.Observe(
            runtime,
            selected.Thread,
            frame.InstructionPointer,
            frame.StackPointer,
            dataTarget.DataReader.Architecture);
        Assert.Equal(W8FrameProbeDisposition.Exact, legacy.SurfaceDisposition);
        Assert.Equal(publicContextBytes.Length, legacy.ContextBytes.Length);
        var dbi = W8FrameDbiProbe.Observe(
            runtime,
            dataTarget.DataReader,
            method.MethodDesc,
            legacy);
        var contextBytes = legacy.ContextBytes;
        var contextSha256 = legacy.ContextSha256;
        Assert.Equal(Sha256(contextBytes.AsSpan()), contextSha256);
        var constructionOracle = W8CdacRuntimeConstructionOracle.Open(dataTarget, clrInfo);
        var roots = ProjectRoots(
            runtime,
            dataTarget.DataReader,
            module,
            catalog,
            expected,
            scopes.Variables,
            legacy,
            constructionOracle);
        var declaringTypeArguments = ObserveDeclaringTypeArguments(
            runtime,
            dataTarget.DataReader,
            module,
            declaringType,
            catalog.ModuleVersionId,
            expected,
            constructionOracle);
        var genericArguments = ProjectGenericArguments(
            runtime,
            module,
            catalog.ModuleVersionId,
            expected,
            legacy,
            declaringTypeArguments,
            dbi);

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add(
            $"profile|{expected.Profile}|{dataTarget.DataReader.Architecture}|{contextBytes.Length}|" +
            contextSha256);
        lines.Add(
            $"frame|{selected.Thread.Address:x16}|{selected.Thread.ManagedThreadId}|" +
            $"{selected.Thread.OSThreadId}|{frame.InstructionPointer:x16}|{frame.StackPointer:x16}|" +
            $"{module.Address:x16}|{catalog.ModuleVersionId:D}|{declaringType.MetadataToken:x8}|" +
            $"{method.MetadataToken:x8}|{ilOffset:x8}");
        lines.Add(
            $"code|{method.MethodDesc:x16}|{method.NativeCode:x16}|{method.CompilationType}|" +
            $"{method.HotColdInfo.HotStart:x16}|{method.HotColdInfo.HotSize}|" +
            $"{method.HotColdInfo.ColdStart:x16}|{method.HotColdInfo.ColdSize}|" + code.MapSha256);
        foreach (var map in code.Map)
        {
            lines.Add(
                $"map|{map.IlOffset:x8}|{map.StartAddress:x16}|{map.EndAddress:x16}");
        }

        foreach (var parameter in expected.Parameters)
        {
            lines.Add(
                $"parameter|{parameter.SequenceNumber}|{parameter.ParameterToken:x8}|" +
                $"{parameter.Name}|{parameter.TypeShape}");
        }

        foreach (var scope in scopes.Scopes)
        {
            lines.Add(
                $"scope|{scope.Token:x8}|{scope.StartOffset:x8}|{scope.EndOffset:x8}");
        }

        foreach (var variable in scopes.Variables)
        {
            lines.Add(
                $"pdb-variable|{variable.ScopeToken:x8}|{variable.Index}|{variable.Name}|" +
                $"{variable.Attributes}");
        }

        foreach (var variable in inactiveVariables)
        {
            lines.Add($"pdb-inactive|{variable.Name}|{variable.Disposition}");
        }

        foreach (var constant in scopes.Constants)
        {
            lines.Add(
                $"pdb-constant|{constant.ScopeToken:x8}|{constant.Name}|{constant.SignatureSha256}");
        }

        foreach (var root in roots)
        {
            lines.Add(
                $"root|{root.Kind}|{root.Name}|{root.Index}|{root.Disposition}|" +
                $"{root.LocationKind}|{root.Size}|{root.DeclaredPayloadWidth}|" +
                $"{root.Address:x16}|{root.RawBytesSha256}|{root.DeclaredTypeShape}|" +
                $"{root.DecodedKind}|{root.DecodedScalar:x16}|{root.DecodedText}|" +
                FormatRuntimeType(root.RuntimeType));
        }

        foreach (var argument in genericArguments)
        {
            lines.Add(
                $"generic|{argument.Kind}|{argument.Index}|{argument.Identity.Disposition}|" +
                $"{argument.Identity.HResult:x8}|{argument.Identity.ModuleVersionId:D}|" +
                $"{argument.Identity.TypeDefinitionToken:x8}");
        }

        for (var index = 0; index < declaringTypeArguments.Length; index++)
        {
            var identity = declaringTypeArguments[index];
            lines.Add(
                $"dictionary-generic|{index}|{identity.Disposition}|{identity.HResult:x8}|" +
                $"{identity.ModuleVersionId:D}|{identity.TypeDefinitionToken:x8}");
        }

        lines.Add(
            $"dbi|{dbi.Disposition}|{dbi.FactoryHResult:x8}|{dbi.ResolveHResult:x8}|" +
            $"{dbi.RawToken:x16}|{dbi.ResolvedToken:x16}|{dbi.EnumerateHResult:x8}|" +
            $"{dbi.DeclaringTypeArgumentCount}|{dbi.Arguments.Length}|{dbi.Code}");
        for (var index = 0; index < dbi.Arguments.Length; index++)
        {
            var argument = dbi.Arguments[index];
            lines.Add(
                $"dbi-generic|{index}|{argument.ElementType:x8}|{argument.MetadataToken:x8}|" +
                $"{argument.AssemblyAddress:x16}|{argument.TypeHandle:x16}");
        }

        foreach (var legacyLine in legacy.CanonicalLines)
        {
            lines.Add("legacy|" + legacyLine);
        }

        return new W8FrameSnapshotObservation(
            expected.Profile,
            dataTarget.DataReader.Architecture,
            contextBytes,
            contextSha256,
            selected.Thread.Address,
            selected.Thread.ManagedThreadId,
            selected.Thread.OSThreadId,
            frame.InstructionPointer,
            frame.StackPointer,
            module.Address,
            catalog.ModuleVersionId,
            declaringType.MetadataToken,
            method.MetadataToken,
            ilOffset,
            method.MethodDesc,
            method.NativeCode,
            code.Map,
            expected.Parameters,
            scopes.Scopes,
            scopes.Variables,
            scopes.Constants,
            inactiveVariables,
            legacy,
            dbi,
            roots,
            declaringTypeArguments,
            genericArguments,
            lines.ToImmutable());
    }

    private static ClrModule SelectModule(
        ClrRuntime runtime,
        IMemoryReader memory,
        Guid expectedModuleVersionId)
    {
        var modules = runtime.EnumerateModules().Take(MaximumRuntimeModules + 1).ToArray();
        Assert.True(
            modules.Length <= MaximumRuntimeModules,
            $"Runtime-module traversal exceeded {MaximumRuntimeModules}; cap-plus-one observed {modules.Length}.");
        var matches = modules.Where(module =>
                TryReadModuleVersionId(memory, module, out var mvid) && mvid == expectedModuleVersionId)
            .ToArray();
        return Assert.Single(matches);
    }

    private static bool TryReadModuleVersionId(
        IMemoryReader memory,
        ClrModule module,
        out Guid moduleVersionId)
    {
        moduleVersionId = Guid.Empty;
        if (module.MetadataAddress == 0 ||
            module.MetadataLength == 0 ||
            module.MetadataLength > MaximumMetadataBytes)
        {
            return false;
        }

        var bytes = new byte[checked((int)module.MetadataLength)];
        if (memory.Read(module.MetadataAddress, bytes) != bytes.Length)
        {
            return false;
        }

        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.CreateRange(bytes));
            var reader = provider.GetMetadataReader();
            moduleVersionId = reader.GetGuid(reader.GetModuleDefinition().Mvid);
            return moduleVersionId != Guid.Empty;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static SelectedRawFrame SelectFrame(
        ClrRuntime runtime,
        ClrModule selectedModule,
        W8FrameMetadataCatalog catalog,
        W8FrameExpectedProfile expected)
    {
        var threads = runtime.Threads
            .OrderBy(static thread => thread.Address)
            .ThenBy(static thread => thread.ManagedThreadId)
            .ThenBy(static thread => thread.OSThreadId)
            .Take(MaximumRuntimeThreads + 1)
            .ToArray();
        Assert.True(
            threads.Length <= MaximumRuntimeThreads,
            $"Runtime-thread traversal exceeded {MaximumRuntimeThreads}; cap-plus-one observed {threads.Length}.");
        var matches = new List<SelectedRawFrame>();
        foreach (var thread in threads)
        {
            var frames = thread
                .EnumerateStackTrace(includeContext: true, maxFrames: MaximumFramesPerThread + 1)
                .Take(MaximumFramesPerThread + 1)
                .ToArray();
            Assert.True(
                frames.Length <= MaximumFramesPerThread,
                $"Frame traversal exceeded {MaximumFramesPerThread}; cap-plus-one observed {frames.Length}.");
            foreach (var frame in frames)
            {
                if (frame.Kind != ClrStackFrameKind.ManagedMethod ||
                    frame.Method is not { } method ||
                    method.Type is not { } type ||
                    method.MetadataToken != expected.MethodDefinitionToken ||
                    type.MetadataToken != expected.DeclaringTypeDefinitionToken ||
                    type.Module.Address != selectedModule.Address)
                {
                    continue;
                }

                var candidateIlOffset = method.GetILOffset(frame.InstructionPointer);
                if (candidateIlOffset < 0)
                {
                    continue;
                }

                var activeNames = catalog.ReadActiveScopes(expected, candidateIlOffset)
                    .Variables
                    .Select(static variable => variable.Name)
                    .ToImmutableHashSet(StringComparer.Ordinal);
                if (!expected.RequiredActiveVariableNames.IsSubsetOf(activeNames))
                {
                    continue;
                }

                matches.Add(new SelectedRawFrame(thread, frame));
            }
        }

        return Assert.Single(matches);
    }

    private static W8FrameCodeObservation ObserveCode(
        ClrRuntime runtime,
        ClrMethod method,
        ulong instructionPointer,
        int ilOffset)
    {
        var byInstruction = Assert.IsType<ClrMethod>(runtime.GetMethodByInstructionPointer(instructionPointer));
        Assert.Equal(method.MethodDesc, byInstruction.MethodDesc);
        Assert.Equal(method.MetadataToken, byInstruction.MetadataToken);
        Assert.Equal(method.Type.MethodTable, byInstruction.Type.MethodTable);
        var map = method.ILOffsetMap
            .Take(4_097)
            .Select(static item => new W8FrameCodeMap(item.ILOffset, item.StartAddress, item.EndAddress))
            .ToImmutableArray();
        Assert.True(map.Length <= 4_096);
        Assert.NotEmpty(map);
        Assert.Contains(
            map,
            item => item.StartAddress <= instructionPointer && instructionPointer < item.EndAddress);
        Assert.Equal(ilOffset, method.GetILOffset(instructionPointer));
        var mapText = string.Join(
            "\n",
            map.Select(static item => $"{item.IlOffset:x8}|{item.StartAddress:x16}|{item.EndAddress:x16}"));
        return new W8FrameCodeObservation(map, Sha256(Encoding.UTF8.GetBytes(mapText)));
    }

    private static ImmutableArray<W8FrameRootObservation> ProjectRoots(
        ClrRuntime runtime,
        IMemoryReader memory,
        ClrModule selectedModule,
        W8FrameMetadataCatalog catalog,
        W8FrameExpectedProfile expected,
        ImmutableArray<W8FramePdbVariable> activeVariables,
        W8FrameLegacyObservation legacy,
        W8CdacRuntimeConstructionOracle constructionOracle)
    {
        var roots = ImmutableArray.CreateBuilder<W8FrameRootObservation>();
        var expectedArgumentCount = expected.Parameters.Length + (expected.HasThis ? 1 : 0);
        if (legacy.ArgumentCountHResult == 0 && legacy.Arguments.Length == expectedArgumentCount)
        {
            var argumentOffset = 0;
            if (expected.HasThis)
            {
                roots.Add(ProjectRoot(
                    runtime,
                    memory,
                    selectedModule,
                    catalog,
                    expected,
                    constructionOracle,
                    "this",
                    "this",
                    0,
                    expected.ThisTypeShape,
                    legacy.Arguments[0].Value));
                argumentOffset = 1;
            }

            foreach (var parameter in expected.Parameters)
            {
                roots.Add(ProjectRoot(
                    runtime,
                    memory,
                    selectedModule,
                    catalog,
                    expected,
                    constructionOracle,
                    "parameter",
                    parameter.Name,
                    parameter.SequenceNumber - 1 + argumentOffset,
                    parameter.TypeShape,
                    legacy.Arguments[parameter.SequenceNumber - 1 + argumentOffset].Value));
            }
        }
        else
        {
            if (expected.HasThis)
            {
                roots.Add(UnavailableRoot("this", "this", 0, expected.ThisTypeShape));
            }

            foreach (var parameter in expected.Parameters)
            {
                roots.Add(UnavailableRoot(
                    "parameter",
                    parameter.Name,
                    parameter.SequenceNumber - 1 + (expected.HasThis ? 1 : 0),
                    parameter.TypeShape));
            }
        }

        foreach (var variable in activeVariables
            .GroupBy(static item => item.Index)
            .Select(static group => group.Single())
            .OrderBy(static item => item.Index))
        {
            if (legacy.LocalCountHResult == 0 && variable.Index < legacy.Locals.Length)
            {
                roots.Add(ProjectRoot(
                    runtime,
                    memory,
                    selectedModule,
                    catalog,
                    expected,
                    constructionOracle,
                    "local",
                    variable.Name,
                    variable.Index,
                    variable.DeclaredTypeShape,
                    legacy.Locals[variable.Index].Value));
            }
            else
            {
                roots.Add(UnavailableRoot(
                    "local",
                    variable.Name,
                    variable.Index,
                    variable.DeclaredTypeShape));
            }
        }

        return roots.ToImmutable();
    }

    private static W8FrameRootObservation ProjectRoot(
        ClrRuntime runtime,
        IMemoryReader memory,
        ClrModule selectedModule,
        W8FrameMetadataCatalog catalog,
        W8FrameExpectedProfile expected,
        W8CdacRuntimeConstructionOracle constructionOracle,
        string kind,
        string name,
        int index,
        string declaredTypeShape,
        W8FrameValueObservation value)
    {
        var declaredType = W8FrameDeclaredType.Parse(declaredTypeShape);
        var declaredPayloadWidth = DeclaredPayloadWidth(declaredType, memory.PointerSize);
        if (value.Disposition != W8FrameProbeDisposition.Exact ||
            value.LocationKind != W8FrameLocationKind.Memory ||
            value.Locations.Length != 1 ||
            value.Size == 0 ||
            value.Size > MaximumMemoryHomeBytes)
        {
            return new W8FrameRootObservation(
                kind,
                name,
                index,
                value.Disposition,
                value.LocationKind,
                value.Size,
                declaredPayloadWidth,
                0,
                string.Empty,
                declaredTypeShape,
                W8FrameDecodedValueKind.NotDecoded,
                0,
                string.Empty,
                null,
                null);
        }

        var address = value.Locations[0].Argument;
        var bytes = new byte[checked((int)value.Size)];
        var read = memory.Read(address, bytes);
        if (read != bytes.Length)
        {
            return new W8FrameRootObservation(
                kind,
                name,
                index,
                W8FrameProbeDisposition.Unavailable,
                value.LocationKind,
                value.Size,
                declaredPayloadWidth,
                address,
                string.Empty,
                declaredTypeShape,
                W8FrameDecodedValueKind.NotDecoded,
                0,
                string.Empty,
                null,
                null);
        }

        var decodedKind = W8FrameDecodedValueKind.NotDecoded;
        ulong decodedScalar = 0;
        var decodedText = string.Empty;
        W8FrameRuntimeTypeIdentity? runtimeType = null;
        byte? pointedByte = null;
        switch (declaredType.Kind)
        {
            case W8FrameDeclaredTypeKind.Int32:
                Assert.Equal(4UL, value.Size);
                Assert.Equal(4, declaredPayloadWidth);
                AssertLegacyValueType(
                    runtime,
                    memory,
                    catalog,
                    selectedModule,
                    declaredType,
                    value.Type);
                decodedKind = W8FrameDecodedValueKind.Int32;
                decodedScalar = unchecked((uint)BinaryPrimitives.ReadInt32LittleEndian(bytes));
                break;

            case W8FrameDeclaredTypeKind.Byte:
                Assert.Equal(1UL, value.Size);
                Assert.Equal(1, declaredPayloadWidth);
                AssertLegacyValueType(
                    runtime,
                    memory,
                    catalog,
                    selectedModule,
                    declaredType,
                    value.Type);
                decodedKind = W8FrameDecodedValueKind.Byte;
                decodedScalar = bytes[0];
                break;

            case W8FrameDeclaredTypeKind.ValueType:
                Assert.Equal(checked((ulong)Align(declaredPayloadWidth, memory.PointerSize)), value.Size);
                AssertLegacyValueType(
                    runtime,
                    memory,
                    catalog,
                    selectedModule,
                    declaredType,
                    value.Type);
                decodedKind = W8FrameDecodedValueKind.ValueInt32;
                decodedScalar = unchecked((uint)BinaryPrimitives.ReadInt32LittleEndian(bytes));
                break;

            case W8FrameDeclaredTypeKind.Pointer:
                Assert.Equal((ulong)memory.PointerSize, value.Size);
                decodedKind = W8FrameDecodedValueKind.Pointer;
                decodedScalar = ReadPointer(bytes, memory.PointerSize);
                Assert.NotEqual(0UL, decodedScalar);
                Span<byte> pointee = stackalloc byte[1];
                Assert.Equal(1, memory.Read(decodedScalar, pointee));
                pointedByte = pointee[0];
                break;

            case W8FrameDeclaredTypeKind.Reference:
                Assert.Equal((ulong)memory.PointerSize, value.Size);
                decodedScalar = ReadPointer(bytes, memory.PointerSize);
                if (decodedScalar == 0)
                {
                    decodedKind = W8FrameDecodedValueKind.NullReference;
                    break;
                }

                decodedKind = W8FrameDecodedValueKind.Reference;
                runtimeType = ReadRuntimeTypeIdentity(
                    runtime,
                    memory,
                    constructionOracle,
                    decodedScalar,
                    depth: 0);
                AssertDeclaredReferenceMatches(
                    catalog,
                    selectedModule,
                    expected,
                    name,
                    declaredType,
                    runtimeType);
                if (runtimeType.Namespace == "System" && runtimeType.Name == "String")
                {
                    decodedText = ReadString(memory, decodedScalar);
                }

                break;

            default:
                throw new Xunit.Sdk.XunitException(
                    $"Declared frame type {declaredTypeShape} has no bounded value decoder.");
        }

        return new W8FrameRootObservation(
            kind,
            name,
            index,
            W8FrameProbeDisposition.Exact,
            value.LocationKind,
            value.Size,
            declaredPayloadWidth,
            address,
            Sha256(bytes),
            declaredTypeShape,
            decodedKind,
            decodedScalar,
            decodedText,
            runtimeType,
            pointedByte);
    }

    private static W8FrameRootObservation UnavailableRoot(
        string kind,
        string name,
        int index,
        string declaredTypeShape) =>
        new(
            kind,
            name,
            index,
            W8FrameProbeDisposition.Unavailable,
            W8FrameLocationKind.NotReported,
            0,
            0,
            0,
            string.Empty,
            declaredTypeShape,
            W8FrameDecodedValueKind.NotDecoded,
            0,
            string.Empty,
            null,
            null);

    private static int DeclaredPayloadWidth(W8FrameDeclaredType type, int pointerSize) => type.Kind switch
    {
        W8FrameDeclaredTypeKind.Byte => 1,
        W8FrameDeclaredTypeKind.Int32 => 4,
        W8FrameDeclaredTypeKind.ValueType => 4,
        W8FrameDeclaredTypeKind.Pointer or W8FrameDeclaredTypeKind.Reference => pointerSize,
        _ => throw new Xunit.Sdk.XunitException($"Declared type form {type.Form} has no width."),
    };

    private static int Align(int value, int alignment) =>
        checked((value + alignment - 1) / alignment * alignment);

    private static ulong ReadPointer(ReadOnlySpan<byte> bytes, int pointerSize) => pointerSize switch
    {
        4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        8 => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
        _ => throw new Xunit.Sdk.XunitException($"Pointer width {pointerSize} is not admitted."),
    };

    private static ulong ReadPointer(IMemoryReader memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[8];
        var target = bytes[..memory.PointerSize];
        Assert.Equal(target.Length, memory.Read(address, target));
        return ReadPointer(target, memory.PointerSize);
    }

    private static void AssertLegacyValueType(
        ClrRuntime runtime,
        IMemoryReader memory,
        W8FrameMetadataCatalog catalog,
        ClrModule selectedModule,
        W8FrameDeclaredType declaredType,
        W8FrameTypeIdentity? legacyType)
    {
        var identity = Assert.IsType<W8FrameTypeIdentity>(legacyType);
        Assert.Equal(W8FrameProbeDisposition.Exact, identity.Disposition);
        Assert.NotEqual(Guid.Empty, identity.ModuleVersionId);
        Assert.NotEqual(0, identity.TypeDefinitionToken);
        if (declaredType.Kind is W8FrameDeclaredTypeKind.Byte or W8FrameDeclaredTypeKind.Int32)
        {
            var module = SelectModuleByVersionId(runtime, memory, identity.ModuleVersionId);
            var metadata = ReadRuntimeMetadataType(memory, module, identity.TypeDefinitionToken);
            Assert.Equal("System", metadata.Namespace);
            Assert.Equal(
                declaredType.Kind == W8FrameDeclaredTypeKind.Byte ? "Byte" : "Int32",
                metadata.Name);
            return;
        }

        Assert.Equal(W8FrameDeclaredTypeKind.ValueType, declaredType.Kind);
        if (declaredType.Form == "TypeDef")
        {
            Assert.Equal(selectedModule.Address, SelectModuleByVersionId(
                runtime,
                memory,
                identity.ModuleVersionId).Address);
            Assert.Equal(declaredType.Token, identity.TypeDefinitionToken);
            return;
        }

        var expectedName = catalog.GetTypeName(declaredType.Token);
        var actualModule = SelectModuleByVersionId(runtime, memory, identity.ModuleVersionId);
        var actualName = ReadRuntimeMetadataType(memory, actualModule, identity.TypeDefinitionToken);
        Assert.Equal(expectedName.Namespace, actualName.Namespace);
        Assert.Equal(expectedName.Name, actualName.Name);
    }

    private static W8FrameRuntimeTypeIdentity ReadRuntimeTypeIdentity(
        ClrRuntime runtime,
        IMemoryReader memory,
        W8CdacRuntimeConstructionOracle constructionOracle,
        ulong objectAddress,
        int depth)
    {
        var methodTable = ReadPointer(memory, objectAddress);
        Assert.NotEqual(0UL, methodTable);
        return ReadRuntimeTypeHandleIdentity(runtime, memory, constructionOracle, methodTable, depth);
    }

    private static W8FrameRuntimeTypeIdentity ReadRuntimeTypeHandleIdentity(
        ClrRuntime runtime,
        IMemoryReader memory,
        W8CdacRuntimeConstructionOracle constructionOracle,
        ulong typeHandle,
        int depth)
    {
        Assert.True(depth < MaximumRuntimeTypeDepth);
        var shape = constructionOracle.ReadTypeShape(typeHandle);
        if (shape.IsArray)
        {
            return new W8FrameRuntimeTypeIdentity(
                typeHandle,
                Guid.Empty,
                0,
                string.Empty,
                string.Empty,
                shape.IsSzArray ? "SZArray" : "Array",
                true,
                shape.IsSzArray,
                shape.ArrayRank,
                ReadRuntimeTypeHandleIdentity(
                    runtime,
                    memory,
                    constructionOracle,
                    shape.ElementOrParameterTypeHandle,
                    depth + 1),
                ImmutableArray<W8FrameRuntimeTypeIdentity>.Empty);
        }

        if (shape.Kind == W8CdacTypeShapeKind.TypeDescriptor)
        {
            return new W8FrameRuntimeTypeIdentity(
                typeHandle,
                Guid.Empty,
                0,
                string.Empty,
                string.Empty,
                $"TypeDescriptor-{shape.ElementType:x2}",
                false,
                false,
                0,
                shape.ElementOrParameterTypeHandle == 0
                    ? null
                    : ReadRuntimeTypeHandleIdentity(
                        runtime,
                        memory,
                        constructionOracle,
                        shape.ElementOrParameterTypeHandle,
                        depth + 1),
                ImmutableArray<W8FrameRuntimeTypeIdentity>.Empty);
        }

        var module = SelectModuleByAddress(runtime, shape.ModuleAddress);
        var metadata = ReadRuntimeMetadataType(memory, module, shape.TypeDefToken);
        Assert.True(shape.TypeArgumentHandles.Length <= MaximumRuntimeTypeArguments);
        var arguments = shape.TypeArgumentHandles
            .Select(handle => ReadRuntimeTypeHandleIdentity(
                runtime,
                memory,
                constructionOracle,
                handle,
                depth + 1))
            .ToImmutableArray();
        return new W8FrameRuntimeTypeIdentity(
            typeHandle,
            metadata.ModuleVersionId,
            shape.TypeDefToken,
            metadata.AssemblyName,
            metadata.Namespace,
            metadata.Name,
            false,
            false,
            0,
            null,
            arguments);
    }

    private static void AssertDeclaredReferenceMatches(
        W8FrameMetadataCatalog catalog,
        ClrModule selectedModule,
        W8FrameExpectedProfile expected,
        string rootName,
        W8FrameDeclaredType declared,
        W8FrameRuntimeTypeIdentity actual)
    {
        switch (declared.Form)
        {
            case "SzArray":
            case "Array":
                Assert.True(actual.IsArray);
                Assert.Equal(declared.Form == "SzArray", actual.IsSzArray);
                Assert.Equal(declared.ArrayRank, actual.ArrayRank);
                AssertDeclaredReferenceMatches(
                    catalog,
                    selectedModule,
                    expected,
                    rootName,
                    Assert.IsType<W8FrameDeclaredType>(declared.ElementType),
                    Assert.IsType<W8FrameRuntimeTypeIdentity>(actual.ElementType));
                return;

            case "ThisTypeDef":
            case "TypeDef":
                Assert.False(actual.IsArray);
                Assert.Equal(selectedModule.Address, actual.TypeHandle == 0
                    ? 0UL
                    : SelectModuleAddressByVersionId(
                        actual.ModuleVersionId,
                        selectedModule,
                        catalog.ModuleVersionId));
                Assert.Equal(declared.Token, actual.TypeDefinitionToken);
                return;

            case "Constructed":
                Assert.False(actual.IsArray);
                Assert.Equal(selectedModule.Address, SelectModuleAddressByVersionId(
                    actual.ModuleVersionId,
                    selectedModule,
                    catalog.ModuleVersionId));
                Assert.Equal(declared.Token, actual.TypeDefinitionToken);
                Assert.Equal(declared.TypeArguments.Length, actual.TypeArguments.Length);
                for (var index = 0; index < declared.TypeArguments.Length; index++)
                {
                    AssertDeclaredReferenceMatches(
                        catalog,
                        selectedModule,
                        expected,
                        rootName,
                        declared.TypeArguments[index],
                        actual.TypeArguments[index]);
                }

                return;

            case "TypeRef":
                var typeName = catalog.GetTypeName(declared.Token);
                if (rootName.EndsWith("Witness", StringComparison.Ordinal) &&
                    typeName.Namespace == "System" &&
                    typeName.Name == "Type")
                {
                    Assert.Equal("System", actual.Namespace);
                    Assert.Equal("RuntimeType", actual.Name);
                    return;
                }

                Assert.Equal(typeName.Namespace, actual.Namespace);
                Assert.Equal(typeName.Name, actual.Name);
                return;

            case "String":
                Assert.Equal("System", actual.Namespace);
                Assert.Equal("String", actual.Name);
                return;

            case "VAR":
                Assert.Equal(selectedModule.Address, SelectModuleAddressByVersionId(
                    actual.ModuleVersionId,
                    selectedModule,
                    catalog.ModuleVersionId));
                Assert.Equal(expected.ExpectedGenericArguments[declared.GenericIndex], actual.TypeDefinitionToken);
                return;

            case "MVAR":
                Assert.Equal(selectedModule.Address, SelectModuleAddressByVersionId(
                    actual.ModuleVersionId,
                    selectedModule,
                    catalog.ModuleVersionId));
                Assert.Equal(
                    expected.ExpectedGenericArguments[
                        expected.DeclaringTypeGenericArity + declared.GenericIndex],
                    actual.TypeDefinitionToken);
                return;

            case "Byte":
                Assert.Equal("System", actual.Namespace);
                Assert.Equal("Byte", actual.Name);
                return;

            default:
                throw new Xunit.Sdk.XunitException(
                    $"Declared reference form {declared.Form} has no exact runtime matcher.");
        }
    }

    private static ulong SelectModuleAddressByVersionId(
        Guid actual,
        ClrModule selectedModule,
        Guid expected)
    {
        Assert.Equal(expected, actual);
        return selectedModule.Address;
    }

    private static ClrModule SelectModuleByAddress(ClrRuntime runtime, ulong address)
    {
        var modules = runtime.EnumerateModules().Take(MaximumRuntimeModules + 1).ToArray();
        Assert.True(modules.Length <= MaximumRuntimeModules);
        return Assert.Single(modules, module => module.Address == address);
    }

    private static ClrModule SelectModuleByVersionId(
        ClrRuntime runtime,
        IMemoryReader memory,
        Guid moduleVersionId)
    {
        var modules = runtime.EnumerateModules().Take(MaximumRuntimeModules + 1).ToArray();
        Assert.True(modules.Length <= MaximumRuntimeModules);
        return Assert.Single(modules, module =>
            TryReadModuleVersionId(memory, module, out var candidate) && candidate == moduleVersionId);
    }

    private static W8FrameRuntimeMetadataType ReadRuntimeMetadataType(
        IMemoryReader memory,
        ClrModule module,
        int typeDefinitionToken)
    {
        Assert.NotEqual(0, typeDefinitionToken);
        Assert.True(module.MetadataAddress != 0 && module.MetadataLength is > 0 and <= MaximumMetadataBytes);
        var bytes = new byte[checked((int)module.MetadataLength)];
        Assert.Equal(bytes.Length, memory.Read(module.MetadataAddress, bytes));
        using var provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.CreateRange(bytes));
        var reader = provider.GetMetadataReader();
        var handle = MetadataTokens.TypeDefinitionHandle(typeDefinitionToken & 0x00ff_ffff);
        var row = reader.GetTypeDefinition(handle);
        return new W8FrameRuntimeMetadataType(
            reader.GetGuid(reader.GetModuleDefinition().Mvid),
            reader.IsAssembly
                ? reader.GetString(reader.GetAssemblyDefinition().Name)
                : string.Empty,
            reader.GetString(row.Namespace),
            reader.GetString(row.Name));
    }

    private static string ReadString(IMemoryReader memory, ulong objectAddress)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        Assert.Equal(4, memory.Read(checked(objectAddress + (ulong)memory.PointerSize), lengthBytes));
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        Assert.InRange(length, 0, MaximumFrameStringCharacters);
        var bytes = new byte[checked(length * sizeof(char))];
        Assert.Equal(
            bytes.Length,
            memory.Read(checked(objectAddress + (ulong)memory.PointerSize + sizeof(int)), bytes));
        return Encoding.Unicode.GetString(bytes);
    }

    private static string FormatRuntimeType(W8FrameRuntimeTypeIdentity? type)
    {
        if (type is null)
        {
            return "none";
        }

        if (type.IsArray)
        {
            return $"array:{(type.IsSzArray ? "sz" : "md")}:{type.ArrayRank}:" +
                FormatRuntimeType(type.ElementType);
        }

        var arguments = type.TypeArguments.Length == 0
            ? string.Empty
            : $"<{string.Join(",", type.TypeArguments.Select(FormatRuntimeType))}>";
        return $"{type.ModuleVersionId:D}:{type.TypeDefinitionToken:x8}:" +
            $"{type.AssemblyName}:{type.Namespace}:{type.Name}{arguments}";
    }

    private static ImmutableArray<W8FrameClosedGenericArgument> ProjectGenericArguments(
        ClrRuntime runtime,
        ClrModule selectedModule,
        Guid selectedModuleVersionId,
        W8FrameExpectedProfile expected,
        W8FrameLegacyObservation legacy,
        ImmutableArray<W8FrameTypeIdentity> declaringTypeArguments,
        W8FrameDbiObservation dbi)
    {
        if (dbi.Disposition == W8FrameProbeDisposition.Exact)
        {
            return ProjectDbiGenericArguments(
                runtime,
                selectedModule,
                selectedModuleVersionId,
                expected,
                dbi);
        }

        var result = ImmutableArray.CreateBuilder<W8FrameClosedGenericArgument>();
        for (var index = 0; index < expected.DeclaringTypeGenericArity; index++)
        {
            var identity = index < declaringTypeArguments.Length
                ? declaringTypeArguments[index]
                : MissingTypeIdentity(legacy.FrameTypeArgumentCountHResult);
            result.Add(new W8FrameClosedGenericArgument("VAR", index, identity));
        }

        for (var index = 0; index < expected.MethodGenericArity; index++)
        {
            W8FrameTypeIdentity identity;
            if (index < legacy.MethodTypeArguments.Length)
            {
                identity = legacy.MethodTypeArguments[index].Identity;
            }
            else
            {
                var frameIndex = expected.DeclaringTypeGenericArity + index;
                identity = frameIndex < legacy.FrameTypeArguments.Length
                    ? legacy.FrameTypeArguments[frameIndex].Identity
                    : MissingTypeIdentity(legacy.MethodTypeArgumentCountHResult);
            }

            result.Add(new W8FrameClosedGenericArgument("MVAR", index, identity));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<W8FrameClosedGenericArgument> ProjectDbiGenericArguments(
        ClrRuntime runtime,
        ClrModule selectedModule,
        Guid selectedModuleVersionId,
        W8FrameExpectedProfile expected,
        W8FrameDbiObservation dbi)
    {
        var expectedCount = checked(
            expected.DeclaringTypeGenericArity + expected.MethodGenericArity);
        Assert.Equal(expectedCount, dbi.Arguments.Length);
        Assert.Equal((uint)expected.DeclaringTypeGenericArity, dbi.DeclaringTypeArgumentCount);

        var result = ImmutableArray.CreateBuilder<W8FrameClosedGenericArgument>(expectedCount);
        for (var index = 0; index < dbi.Arguments.Length; index++)
        {
            var argument = dbi.Arguments[index];
            Assert.NotEqual(0UL, argument.TypeHandle);
            var type = Assert.IsAssignableFrom<ClrType>(
                runtime.GetTypeByMethodTable(argument.TypeHandle));
            Assert.Equal(argument.TypeHandle, type.MethodTable);
            Assert.Equal(selectedModule.Address, type.Module.Address);
            Assert.Equal(expected.ExpectedGenericArguments[index], type.MetadataToken);
            Assert.Equal(type.MetadataToken, argument.MetadataToken);

            var isDeclaringTypeArgument = index < expected.DeclaringTypeGenericArity;
            var localIndex = isDeclaringTypeArgument
                ? index
                : index - expected.DeclaringTypeGenericArity;
            result.Add(new W8FrameClosedGenericArgument(
                isDeclaringTypeArgument ? "VAR" : "MVAR",
                localIndex,
                new W8FrameTypeIdentity(
                    W8FrameProbeDisposition.Exact,
                    0,
                    selectedModuleVersionId,
                    type.MetadataToken)));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<W8FrameTypeIdentity> ObserveDeclaringTypeArguments(
        ClrRuntime runtime,
        IMemoryReader memory,
        ClrModule selectedModule,
        ClrType declaringType,
        Guid selectedModuleVersionId,
        W8FrameExpectedProfile expected,
        W8CdacRuntimeConstructionOracle oracle)
    {
        if (expected.DeclaringTypeGenericArity == 0)
        {
            return ImmutableArray<W8FrameTypeIdentity>.Empty;
        }

        Assert.NotEqual(0UL, declaringType.MethodTable);
        Assert.True(oracle.TryReadMethodTableIdentity(declaringType.MethodTable, out var identity));
        var exact = Assert.IsType<W8CdacMethodTableIdentity>(identity);
        Assert.Equal(declaringType.MethodTable, exact.TypeHandle);
        Assert.Equal(selectedModule.Address, exact.ModuleAddress);
        Assert.Equal(expected.DeclaringTypeDefinitionToken, exact.TypeDefToken);
        Assert.Equal(expected.DeclaringTypeGenericArity, exact.TypeArgumentHandles.Length);
        var result = ImmutableArray.CreateBuilder<W8FrameTypeIdentity>(exact.TypeArgumentHandles.Length);
        for (var index = 0; index < exact.TypeArgumentHandles.Length; index++)
        {
            var argumentHandle = exact.TypeArgumentHandles[index];
            Assert.NotEqual(0UL, argumentHandle);
            Assert.True(oracle.TryReadMethodTableIdentity(argumentHandle, out var argumentIdentity));
            var argument = Assert.IsType<W8CdacMethodTableIdentity>(argumentIdentity);
            Assert.NotEqual(0, argument.TypeDefToken);
            var isExpectedIdentity = argument.ModuleAddress == selectedModule.Address &&
                argument.TypeDefToken == expected.ExpectedGenericArguments[index];
            var observed = ReadRuntimeTypeHandleIdentity(
                runtime,
                memory,
                oracle,
                argumentHandle,
                depth: 0);
            if (!isExpectedIdentity)
            {
                Assert.Equal("System", observed.Namespace);
                Assert.Equal("__Canon", observed.Name);
                Assert.NotEqual(Guid.Empty, observed.ModuleVersionId);
                Assert.NotEqual(0, observed.TypeDefinitionToken);
            }

            result.Add(new W8FrameTypeIdentity(
                isExpectedIdentity
                    ? W8FrameProbeDisposition.Exact
                    : W8FrameProbeDisposition.CanonicalSubstitution,
                0,
                isExpectedIdentity
                    ? selectedModuleVersionId
                    : observed.ModuleVersionId,
                isExpectedIdentity ? argument.TypeDefToken : observed.TypeDefinitionToken));
        }

        return result.ToImmutable();
    }

    private static W8FrameTypeIdentity MissingTypeIdentity(int hresult) =>
        new(
            hresult == unchecked((int)0x80004001)
                ? W8FrameProbeDisposition.NotImplemented
                : W8FrameProbeDisposition.Unavailable,
            hresult,
            Guid.Empty,
            0);

    private static void AssertGenericExpectations(
        W8FrameSnapshotObservation observation,
        W8FrameMetadataCatalog catalog,
        W8FrameExpectedProfile expected)
    {
        Assert.Equal(NotImplementedHResult, observation.Legacy.FrameTypeArgumentCountHResult);
        Assert.Equal(NotImplementedHResult, observation.Legacy.MethodTypeArgumentCountHResult);
        Assert.Empty(observation.Legacy.FrameTypeArguments);
        Assert.Empty(observation.Legacy.MethodTypeArguments);
        Assert.Equal(
            expected.DeclaringTypeGenericArity + expected.MethodGenericArity,
            observation.GenericArguments.Length);
        for (var index = 0; index < observation.GenericArguments.Length; index++)
        {
            var actual = observation.GenericArguments[index];
            var expectedType = expected.ExpectedGenericArguments[index];
            if (actual.Identity.Disposition == W8FrameProbeDisposition.Exact)
            {
                Assert.Equal(catalog.ModuleVersionId, actual.Identity.ModuleVersionId);
                Assert.Equal(expectedType, actual.Identity.TypeDefinitionToken);
            }
            else if (actual.Identity.Disposition == W8FrameProbeDisposition.CanonicalSubstitution)
            {
                Assert.NotEqual(Guid.Empty, actual.Identity.ModuleVersionId);
                Assert.NotEqual(0, actual.Identity.TypeDefinitionToken);
            }
            else
            {
                Assert.Equal(Guid.Empty, actual.Identity.ModuleVersionId);
                Assert.Equal(0, actual.Identity.TypeDefinitionToken);
            }
        }

        if (expected.DeclaringTypeGenericArity + expected.MethodGenericArity == 0)
        {
            Assert.Empty(observation.GenericArguments);
        }

        switch (expected.Profile)
        {
            case "generic-frame":
                AssertDbiFactoryNonAdmission(observation.Dbi);
                Assert.Collection(
                    observation.GenericArguments,
                    argument =>
                    {
                        Assert.Equal("VAR", argument.Kind);
                        Assert.Equal(
                            W8FrameProbeDisposition.CanonicalSubstitution,
                            argument.Identity.Disposition);
                    },
                    argument =>
                    {
                        Assert.Equal("MVAR", argument.Kind);
                        Assert.Equal(W8FrameProbeDisposition.NotImplemented, argument.Identity.Disposition);
                        Assert.Equal(NotImplementedHResult, argument.Identity.HResult);
                    });
                var dictionaryArgument = Assert.Single(observation.DeclaringTypeDictionaryArguments);
                Assert.Equal(
                    W8FrameProbeDisposition.CanonicalSubstitution,
                    dictionaryArgument.Disposition);
                AssertExactGenericToken(observation.Legacy, expectedSize: 8);
                break;
            case "method-generic-frame":
                AssertDbiFactoryNonAdmission(observation.Dbi);
                Assert.All(
                    observation.GenericArguments,
                    argument =>
                    {
                        Assert.Equal("MVAR", argument.Kind);
                        Assert.Equal(W8FrameProbeDisposition.NotImplemented, argument.Identity.Disposition);
                        Assert.Equal(NotImplementedHResult, argument.Identity.HResult);
                    });
                AssertExactGenericToken(observation.Legacy, expectedSize: 4);
                break;
            case "optimized-frame":
            case "slot-reuse-frame":
            case "lexical-frame":
                AssertZeroLocationGenericToken(observation.Legacy);
                AssertDbiTokenLocationNonAdmission(observation.Dbi);
                break;
            case "query-frame":
                Assert.Equal(0, observation.Legacy.ExactGenericTokenInterfaceHResult);
                Assert.Equal(BadFormatHResult, observation.Legacy.ExactGenericTokenHResult);
                Assert.Null(observation.Legacy.ExactGenericToken);
                Assert.Equal(W8FrameProbeDisposition.Failed, observation.Dbi.Disposition);
                Assert.Equal(BadFormatHResult, observation.Dbi.FactoryHResult);
                Assert.Equal(BadFormatHResult, observation.Dbi.ResolveHResult);
                Assert.Equal(BadFormatHResult, observation.Dbi.EnumerateHResult);
                Assert.Equal(0UL, observation.Dbi.RawToken);
                Assert.Equal(0UL, observation.Dbi.ResolvedToken);
                Assert.Empty(observation.Dbi.Arguments);
                Assert.Equal("exact-token-call-unavailable", observation.Dbi.Code);
                break;
            default:
                throw new Xunit.Sdk.XunitException(
                    $"Profile {expected.Profile} has no frozen generic-context-token disposition.");
        }
    }

    private static void AssertDbiFactoryNonAdmission(W8FrameDbiObservation dbi)
    {
        Assert.Equal(W8FrameProbeDisposition.NoInterface, dbi.Disposition);
        Assert.Equal(NoInterfaceHResult, dbi.FactoryHResult);
        Assert.Equal(NoInterfaceHResult, dbi.ResolveHResult);
        Assert.Equal(NoInterfaceHResult, dbi.EnumerateHResult);
        Assert.NotEqual(0UL, dbi.RawToken);
        Assert.Equal(0UL, dbi.ResolvedToken);
        Assert.Equal(0U, dbi.DeclaringTypeArgumentCount);
        Assert.Empty(dbi.Arguments);
        Assert.Equal("dbi-factory-declined-interface", dbi.Code);
    }

    private static void AssertDbiTokenLocationNonAdmission(W8FrameDbiObservation dbi)
    {
        Assert.Equal(W8FrameProbeDisposition.Unavailable, dbi.Disposition);
        Assert.Equal(NoInterfaceHResult, dbi.FactoryHResult);
        Assert.Equal(NoInterfaceHResult, dbi.ResolveHResult);
        Assert.Equal(NoInterfaceHResult, dbi.EnumerateHResult);
        Assert.Equal(0UL, dbi.RawToken);
        Assert.Equal(0UL, dbi.ResolvedToken);
        Assert.Equal(0U, dbi.DeclaringTypeArgumentCount);
        Assert.Empty(dbi.Arguments);
        Assert.Equal("exact-token-location-unavailable", dbi.Code);
    }

    private static void AssertExactGenericToken(W8FrameLegacyObservation legacy, ulong expectedSize)
    {
        Assert.Equal(0, legacy.ExactGenericTokenInterfaceHResult);
        Assert.Equal(0, legacy.ExactGenericTokenHResult);
        var token = Assert.IsType<W8FrameValueObservation>(legacy.ExactGenericToken);
        Assert.Equal(W8FrameProbeDisposition.Exact, token.Disposition);
        Assert.Equal(W8FrameLocationKind.Memory, token.LocationKind);
        Assert.Equal(expectedSize, token.Size);
        var location = Assert.Single(token.Locations);
        Assert.Equal(0U, location.Flags);
        Assert.NotEqual(0UL, location.Argument);
    }

    private static void AssertZeroLocationGenericToken(W8FrameLegacyObservation legacy)
    {
        Assert.Equal(0, legacy.ExactGenericTokenInterfaceHResult);
        Assert.Equal(0, legacy.ExactGenericTokenHResult);
        var token = Assert.IsType<W8FrameValueObservation>(legacy.ExactGenericToken);
        Assert.Equal(W8FrameProbeDisposition.Unavailable, token.Disposition);
        Assert.Equal(W8FrameLocationKind.ZeroLocations, token.LocationKind);
        Assert.Equal(0UL, token.Size);
        Assert.Empty(token.Locations);
    }

    private static void AssertRootCoverage(
        W8FrameSnapshotObservation observation,
        W8FrameExpectedProfile expected)
    {
        if (expected.HasThis)
        {
            Assert.Single(observation.Roots, static root => root.Kind == "this");
        }

        Assert.Equal(
            expected.Parameters.Length,
            observation.Roots.Count(static root => root.Kind == "parameter"));
        Assert.Equal(
            observation.ActiveVariables.Select(static item => item.Index).Distinct().Count(),
            observation.Roots.Count(static root => root.Kind == "local"));
        Assert.All(
            observation.Roots,
            static root => Assert.Equal(W8FrameProbeDisposition.Exact, root.Disposition));
        Assert.All(
            expected.ReferenceParameterNames,
            name => Assert.Contains(
                observation.Roots,
                root => root.Kind == "parameter" && string.Equals(root.Name, name, StringComparison.Ordinal)));
        Assert.All(
            expected.ValueParameterNames,
            name => Assert.Contains(
                observation.Roots,
                root => root.Kind == "parameter" && string.Equals(root.Name, name, StringComparison.Ordinal)));
        Assert.Equal(expected.InactiveVariableNames.Count, observation.InactiveVariables.Length);
        Assert.All(
            observation.InactiveVariables,
            variable =>
            {
                Assert.Equal(W8FrameProbeDisposition.LexicallyInactive, variable.Disposition);
                Assert.Contains(variable.Name, expected.InactiveVariableNames);
                Assert.DoesNotContain(observation.ActiveVariables, active =>
                    string.Equals(active.Name, variable.Name, StringComparison.Ordinal));
                Assert.DoesNotContain(observation.Roots, root =>
                    string.Equals(root.Name, variable.Name, StringComparison.Ordinal));
            });
        Assert.All(
            observation.Roots.Where(static root => root.Disposition == W8FrameProbeDisposition.Exact),
            static root =>
            {
                Assert.Equal(W8FrameLocationKind.Memory, root.LocationKind);
                Assert.True(root.DeclaredPayloadWidth > 0);
                Assert.NotEqual(0UL, root.Address);
                Assert.Equal(64, root.RawBytesSha256.Length);
                Assert.NotEqual(W8FrameDecodedValueKind.NotDecoded, root.DecodedKind);
                if (root.DecodedKind == W8FrameDecodedValueKind.Reference)
                {
                    var runtimeType = Assert.IsType<W8FrameRuntimeTypeIdentity>(root.RuntimeType);
                    Assert.NotEqual(0UL, runtimeType.TypeHandle);
                }
            });
        AssertDecodedRootValues(observation, expected);
    }

    private static void AssertDecodedRootValues(
        W8FrameSnapshotObservation observation,
        W8FrameExpectedProfile expected)
    {
        const int input = unchecked((int)0x81A2B3C4);
        AssertTextRoot(observation, "parameter", "profile", expected.Profile);
        if (expected.ValueParameterNames.Contains("number"))
        {
            AssertInt32Root(observation, "parameter", "number", input);
        }

        switch (expected.Profile)
        {
            case "generic-frame":
                AssertValueInt32Root(observation, "parameter", "value", 0x1D017A01);
                AssertValueInt32Root(observation, "local", "localValue", 0x1D017A01);
                AssertInt32Root(observation, "local", "localNumber", input ^ 0x1D017A01);
                AssertSameReference(observation, ("this", "this"), ("local", "localThis"));
                AssertSameReference(
                    observation,
                    ("parameter", "methodValue"),
                    ("local", "localMethodValue"));
                AssertSameReference(
                    observation,
                    ("parameter", "request"),
                    ("local", "localOwnerValue"),
                    ("local", "localRequest"));
                break;

            case "method-generic-frame":
                AssertInt32Root(observation, "parameter", "rvaLength", Encoding.UTF8.GetByteCount(
                    "w8-rva-materialization"));
                AssertInt32Root(observation, "local", "localNumber", input ^ 0x11017A01);
                AssertInt32Root(observation, "local", "localImported", unchecked((int)0xBB0B7A01));
                AssertSameReference(
                    observation,
                    ("parameter", "request"),
                    ("local", "localRequest"));
                break;

            case "optimized-frame":
                var folded = unchecked((input ^ "request-17".Length) - 0x1234);
                AssertInt32Root(observation, "local", "foldedNumber", folded);
                AssertInt32Root(
                    observation,
                    "local",
                    "projectedNumber",
                    folded ^ "request-17".Length);
                AssertSameReference(
                    observation,
                    ("parameter", "request"),
                    ("local", "selectedReference"));
                break;

            case "slot-reuse-frame":
                AssertInt32Root(
                    observation,
                    "local",
                    "activeSlot",
                    input ^ unchecked((int)0xE5057A05));
                break;

            case "lexical-frame":
                var deconstructed = input ^ 0x1C027A01;
                var localFunction = deconstructed ^ 0x17;
                AssertInt32Root(observation, "local", "deconstructionNumber", deconstructed);
                AssertInt32Root(observation, "local", "rangeLocal", 0);
                AssertInt32Root(observation, "local", "localFunctionResult", localFunction);
                AssertInt32Root(observation, "local", "genericImportedLocal", 0);
                AssertInt32Root(
                    observation,
                    "local",
                    "nonGenericImportedLocal",
                    unchecked((int)0xBB0B7A02));
                AssertTextRoot(observation, "local", "deconstructionText", "request-17");
                AssertTextRoot(observation, "local", "patternLocal", "request-17");
                AssertSameReference(
                    observation,
                    ("local", "ordinaryLocal"),
                    ("local", "foreachLocal"));
                var fixedLocal = FindRoot(observation, "local", "fixedLocal");
                Assert.Equal(W8FrameDecodedValueKind.Pointer, fixedLocal.DecodedKind);
                Assert.Equal((byte)0x17, fixedLocal.PointedByte);
                break;

            case "query-frame":
                AssertTextRoot(observation, "local", "retainedName", "request-17");
                break;

            default:
                throw new Xunit.Sdk.XunitException(
                    $"Profile {expected.Profile} has no decoded root-value oracle.");
        }
    }

    private static void AssertInt32Root(
        W8FrameSnapshotObservation observation,
        string kind,
        string name,
        int expected)
    {
        var root = FindRoot(observation, kind, name);
        Assert.Equal(W8FrameDecodedValueKind.Int32, root.DecodedKind);
        Assert.Equal(unchecked((uint)expected), root.DecodedScalar);
    }

    private static void AssertValueInt32Root(
        W8FrameSnapshotObservation observation,
        string kind,
        string name,
        int expected)
    {
        var root = FindRoot(observation, kind, name);
        Assert.Equal(W8FrameDecodedValueKind.ValueInt32, root.DecodedKind);
        Assert.Equal(unchecked((uint)expected), root.DecodedScalar);
    }

    private static void AssertTextRoot(
        W8FrameSnapshotObservation observation,
        string kind,
        string name,
        string expected)
    {
        var root = FindRoot(observation, kind, name);
        Assert.Equal(W8FrameDecodedValueKind.Reference, root.DecodedKind);
        Assert.Equal(expected, root.DecodedText);
    }

    private static void AssertSameReference(
        W8FrameSnapshotObservation observation,
        params (string Kind, string Name)[] roots)
    {
        Assert.True(roots.Length >= 2);
        var values = roots.Select(root => FindRoot(observation, root.Kind, root.Name)).ToArray();
        Assert.All(values, static value => Assert.Equal(W8FrameDecodedValueKind.Reference, value.DecodedKind));
        Assert.NotEqual(0UL, values[0].DecodedScalar);
        Assert.All(values, value => Assert.Equal(values[0].DecodedScalar, value.DecodedScalar));
    }

    private static W8FrameRootObservation FindRoot(
        W8FrameSnapshotObservation observation,
        string kind,
        string name) =>
        Assert.Single(observation.Roots, root =>
            string.Equals(root.Kind, kind, StringComparison.Ordinal) &&
            string.Equals(root.Name, name, StringComparison.Ordinal));

    private static int ExpectedContextSize(Architecture architecture) => architecture switch
    {
        Architecture.X64 => 1232,
        Architecture.X86 => 716,
        Architecture.Arm => 416,
        Architecture.Arm64 => 912,
        _ when (int)architecture == 9 => 544,
        _ when (int)architecture == 6 => 1312,
        _ => throw new Xunit.Sdk.XunitException($"Architecture {architecture} has no frozen context layout."),
    };

    private static string DescribeContextDifferences(
        ImmutableArray<byte> first,
        ImmutableArray<byte> second)
    {
        var differences = first.Zip(second)
            .Select(static (pair, index) => (pair.First, pair.Second, Index: index))
            .Where(static item => item.First != item.Second)
            .Take(64)
            .Select(static item => $"{item.Index:x4}:{item.First:x2}>{item.Second:x2}");
        return "Detached context differed at " + string.Join(",", differences);
    }

    private static string DescribeCanonicalDifferences(
        ImmutableArray<string> first,
        ImmutableArray<string> second)
    {
        var length = Math.Max(first.Length, second.Length);
        for (var index = 0; index < length; index++)
        {
            var left = index < first.Length ? first[index] : "<absent>";
            var right = index < second.Length ? second[index] : "<absent>";
            if (!string.Equals(left, right, StringComparison.Ordinal))
            {
                return $"Canonical line {index} differed. First: {left} Second: {right}";
            }
        }

        return "Canonical evidence lengths differed.";
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        CanonicalReplayEncoding.ComputeSha256(bytes);

    private static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"Required W8 artifact was not found: {path}");
        return path;
    }

    private sealed record SelectedRawFrame(ClrThread Thread, ClrStackFrame Frame);

    private sealed record W8FrameCodeObservation(
        ImmutableArray<W8FrameCodeMap> Map,
        string MapSha256);
}

internal sealed record W8FrameSnapshotObservation(
    string Profile,
    Architecture Architecture,
    ImmutableArray<byte> ContextBytes,
    string ContextSha256,
    ulong ThreadAddress,
    int ManagedThreadId,
    uint OSThreadId,
    ulong InstructionPointer,
    ulong StackPointer,
    ulong ModuleAddress,
    Guid ModuleVersionId,
    int DeclaringTypeDefinitionToken,
    int MethodDefinitionToken,
    int IlOffset,
    ulong MethodDescriptor,
    ulong NativeCode,
    ImmutableArray<W8FrameCodeMap> IlToNativeMap,
    ImmutableArray<W8FrameParameter> Parameters,
    ImmutableArray<W8FramePdbScope> ActiveScopes,
    ImmutableArray<W8FramePdbVariable> ActiveVariables,
    ImmutableArray<W8FramePdbConstant> ActiveConstants,
    ImmutableArray<W8FrameLexicalVariableDisposition> InactiveVariables,
    W8FrameLegacyObservation Legacy,
    W8FrameDbiObservation Dbi,
    ImmutableArray<W8FrameRootObservation> Roots,
    ImmutableArray<W8FrameTypeIdentity> DeclaringTypeDictionaryArguments,
    ImmutableArray<W8FrameClosedGenericArgument> GenericArguments,
    ImmutableArray<string> CanonicalLines);

internal sealed record W8FrameCodeMap(int IlOffset, ulong StartAddress, ulong EndAddress);

internal sealed record W8FrameRootObservation(
    string Kind,
    string Name,
    int Index,
    W8FrameProbeDisposition Disposition,
    W8FrameLocationKind LocationKind,
    ulong Size,
    int DeclaredPayloadWidth,
    ulong Address,
    string RawBytesSha256,
    string DeclaredTypeShape,
    W8FrameDecodedValueKind DecodedKind,
    ulong DecodedScalar,
    string DecodedText,
    W8FrameRuntimeTypeIdentity? RuntimeType,
    byte? PointedByte);

internal enum W8FrameDecodedValueKind
{
    NotDecoded,
    Byte,
    Int32,
    ValueInt32,
    Reference,
    NullReference,
    Pointer,
}

internal sealed record W8FrameRuntimeTypeIdentity(
    ulong TypeHandle,
    Guid ModuleVersionId,
    int TypeDefinitionToken,
    string AssemblyName,
    string Namespace,
    string Name,
    bool IsArray,
    bool IsSzArray,
    int ArrayRank,
    W8FrameRuntimeTypeIdentity? ElementType,
    ImmutableArray<W8FrameRuntimeTypeIdentity> TypeArguments);

internal enum W8FrameDeclaredTypeKind
{
    Reference,
    Byte,
    Int32,
    ValueType,
    Pointer,
}

internal sealed record W8FrameDeclaredType(
    W8FrameDeclaredTypeKind Kind,
    string Form,
    int Token,
    int GenericIndex,
    int ArrayRank,
    W8FrameDeclaredType? ElementType,
    ImmutableArray<W8FrameDeclaredType> TypeArguments)
{
    private const int MaximumParseDepth = 16;

    internal static W8FrameDeclaredType Parse(string text) => Parse(text, 0);

    private static W8FrameDeclaredType Parse(string text, int depth)
    {
        if (depth >= MaximumParseDepth)
        {
            throw new Xunit.Sdk.XunitException("Declared frame-type depth reached its fixed bound.");
        }

        if (text.StartsWith("this:TypeDef:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.Reference, "ThisTypeDef", ParseToken(text[13..]));
        }

        if (text == "value:Int32")
        {
            return Leaf(W8FrameDeclaredTypeKind.Int32, "Int32");
        }

        if (text == "value:Byte")
        {
            return Leaf(W8FrameDeclaredTypeKind.Byte, "Byte");
        }

        if (text.StartsWith("value:TypeDef:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.ValueType, "TypeDef", ParseToken(text[14..]));
        }

        if (text.StartsWith("value:TypeRef:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.ValueType, "TypeRef", ParseToken(text[14..]));
        }

        if (text.StartsWith("pointer:", StringComparison.Ordinal))
        {
            return new W8FrameDeclaredType(
                W8FrameDeclaredTypeKind.Pointer,
                "Pointer",
                0,
                -1,
                0,
                Parse(text[8..], depth + 1),
                ImmutableArray<W8FrameDeclaredType>.Empty);
        }

        if (text.StartsWith("szarray:", StringComparison.Ordinal))
        {
            return new W8FrameDeclaredType(
                W8FrameDeclaredTypeKind.Reference,
                "SzArray",
                0,
                -1,
                1,
                Parse(text[8..], depth + 1),
                ImmutableArray<W8FrameDeclaredType>.Empty);
        }

        if (text.StartsWith("array:", StringComparison.Ordinal))
        {
            var separator = text.IndexOf(':', 6);
            Assert.True(separator > 6);
            return new W8FrameDeclaredType(
                W8FrameDeclaredTypeKind.Reference,
                "Array",
                0,
                -1,
                int.Parse(text.AsSpan(6, separator - 6), System.Globalization.CultureInfo.InvariantCulture),
                Parse(text[(separator + 1)..], depth + 1),
                ImmutableArray<W8FrameDeclaredType>.Empty);
        }

        if (text.StartsWith("constructed:", StringComparison.Ordinal))
        {
            var body = text[12..];
            var open = body.IndexOf('<');
            Assert.True(open > 0 && body.EndsWith('>'));
            var head = Parse(body[..open], depth + 1);
            var arguments = SplitTypeArguments(body.AsSpan(open + 1, body.Length - open - 2))
                .Select(argument => Parse(argument, depth + 1))
                .ToImmutableArray();
            return head with { Form = "Constructed", TypeArguments = arguments };
        }

        if (text.StartsWith("reference:TypeDef:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.Reference, "TypeDef", ParseToken(text[18..]));
        }

        if (text.StartsWith("reference:TypeRef:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.Reference, "TypeRef", ParseToken(text[18..]));
        }

        if (text.StartsWith("reference:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.Reference, text[10..]);
        }

        if (text.StartsWith("VAR:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.Reference, "VAR", genericIndex: ParseIndex(text[4..]));
        }

        if (text.StartsWith("MVAR:", StringComparison.Ordinal))
        {
            return Leaf(W8FrameDeclaredTypeKind.Reference, "MVAR", genericIndex: ParseIndex(text[5..]));
        }

        throw new Xunit.Sdk.XunitException($"Declared frame type {text} is outside the probe grammar.");
    }

    private static W8FrameDeclaredType Leaf(
        W8FrameDeclaredTypeKind kind,
        string form,
        int token = 0,
        int genericIndex = -1) =>
        new(kind, form, token, genericIndex, 0, null, ImmutableArray<W8FrameDeclaredType>.Empty);

    private static int ParseToken(string text) =>
        Convert.ToInt32(text, 16);

    private static int ParseIndex(string text) =>
        int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static ImmutableArray<string> SplitTypeArguments(ReadOnlySpan<char> text)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < text.Length; index++)
        {
            depth += text[index] switch
            {
                '<' => 1,
                '>' => -1,
                _ => 0,
            };
            Assert.True(depth >= 0);
            if (text[index] == ',' && depth == 0)
            {
                result.Add(text[start..index].ToString());
                start = index + 1;
            }
        }

        Assert.Equal(0, depth);
        result.Add(text[start..].ToString());
        return result.ToImmutable();
    }
}

internal sealed record W8FrameClosedGenericArgument(
    string Kind,
    int Index,
    W8FrameTypeIdentity Identity);

internal sealed record W8FramePdbScope(int Token, int StartOffset, int EndOffset);

internal sealed record W8FramePdbVariable(
    int ScopeToken,
    int Index,
    string Name,
    LocalVariableAttributes Attributes,
    string DeclaredTypeShape);

internal sealed record W8FramePdbConstant(
    int ScopeToken,
    string Name,
    string SignatureSha256);

internal sealed record W8FrameLexicalVariableDisposition(
    string Name,
    W8FrameProbeDisposition Disposition);

internal sealed record W8FrameActiveScopes(
    ImmutableArray<W8FramePdbScope> Scopes,
    ImmutableArray<W8FramePdbVariable> Variables,
    ImmutableArray<W8FramePdbConstant> Constants);

internal sealed record W8FrameParameter(
    int ParameterToken,
    int SequenceNumber,
    string Name,
    string TypeShape);

internal sealed record W8FrameMetadataTypeName(int Token, string Namespace, string Name);

internal sealed record W8FrameRuntimeMetadataType(
    Guid ModuleVersionId,
    string AssemblyName,
    string Namespace,
    string Name);

internal sealed record W8FrameExpectedProfile(
    string Profile,
    int DeclaringTypeDefinitionToken,
    int MethodDefinitionToken,
    bool HasThis,
    int DeclaringTypeGenericArity,
    int MethodGenericArity,
    string ThisTypeShape,
    ImmutableArray<int> ExpectedGenericArguments,
    ImmutableArray<string> LocalTypeShapes,
    ImmutableArray<W8FrameParameter> Parameters,
    ImmutableHashSet<string> ReferenceParameterNames,
    ImmutableHashSet<string> ValueParameterNames,
    ImmutableHashSet<string> RequiredActiveVariableNames,
    ImmutableHashSet<string> InactiveVariableNames);

internal sealed class W8FrameMetadataCatalog
{
    private readonly byte[] pdbBytes;
    private readonly ImmutableDictionary<int, W8FrameMetadataTypeName> typeNames;

    private W8FrameMetadataCatalog(
        Guid moduleVersionId,
        ImmutableDictionary<string, W8FrameExpectedProfile> profiles,
        byte[] pdbBytes,
        ImmutableDictionary<int, W8FrameMetadataTypeName> typeNames)
    {
        ModuleVersionId = moduleVersionId;
        Profiles = profiles;
        this.pdbBytes = pdbBytes;
        this.typeNames = typeNames;
    }

    internal Guid ModuleVersionId { get; }

    internal ImmutableDictionary<string, W8FrameExpectedProfile> Profiles { get; }

    internal static W8FrameMetadataCatalog Read(string assemblyPath, string pdbPath)
    {
        var pdbBytes = File.ReadAllBytes(pdbPath);
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var reader = peReader.GetMetadataReader();
        var moduleVersionId = reader.GetGuid(reader.GetModuleDefinition().Mvid);
        Assert.NotEqual(Guid.Empty, moduleVersionId);

        var requestContext = FindType(reader, "PhoenixInspect.W8TestTarget", "RequestContext");
        var batchContext = FindType(reader, "PhoenixInspect.W8TestTarget", "BatchContext");
        var definitions = new[]
        {
            CreateProfile(
                peReader,
                reader,
                "generic-frame",
                "PhoenixInspect.W8TestTarget",
                "GenericFrameOwner`1",
                "Run",
                parameterCount: 5,
                declaringTypeGenericArity: 1,
                methodGenericArity: 1,
                [requestContext, batchContext],
                ["profile", "methodValue", "request"],
                ["value", "number"],
                ["localThis", "methodTypeWitness"]),
            CreateProfile(
                peReader,
                reader,
                "method-generic-frame",
                "PhoenixInspect.W8TestTarget",
                "W8FrameProbe",
                "Run",
                parameterCount: 5,
                declaringTypeGenericArity: 0,
                methodGenericArity: 2,
                [requestContext, batchContext],
                ["profile", "request", "importedNested"],
                ["number", "rvaLength"],
                ["localRequest", "methodWitness"]),
            CreateProfile(
                peReader,
                reader,
                "optimized-frame",
                "PhoenixInspect.W8TestTarget",
                "OptimizedFrameProfile",
                "Run",
                parameterCount: 3,
                declaringTypeGenericArity: 0,
                methodGenericArity: 0,
                [],
                ["profile", "request"],
                ["number"],
                ["foldedNumber", "selectedReference", "projectedNumber"]),
            CreateProfile(
                peReader,
                reader,
                "slot-reuse-frame",
                "PhoenixInspect.W8TestTarget",
                "SlotReuseProfile",
                "Run",
                parameterCount: 2,
                declaringTypeGenericArity: 0,
                methodGenericArity: 0,
                [],
                ["profile"],
                ["number"],
                ["activeSlot"],
                ["inactiveSlot"]),
            CreateProfile(
                peReader,
                reader,
                "lexical-frame",
                "PhoenixInspect.W8TestTarget.LexicalEvidence",
                "LexicalCatalogProbe",
                "Run",
                parameterCount: 3,
                declaringTypeGenericArity: 0,
                methodGenericArity: 0,
                [],
                ["profile", "request"],
                ["number"],
                ["ordinaryLocal", "fixedLocal", "nonGenericNestedLocal"]),
            CreateProfile(
                peReader,
                reader,
                "query-frame",
                "PhoenixInspect.W8TestTarget",
                "QueryRangeProfile",
                "PauseFromRange",
                parameterCount: 2,
                declaringTypeGenericArity: 0,
                methodGenericArity: 0,
                [],
                ["profile", "queryRangeVariable"],
                [],
                ["retainedName"]),
        };
        var typeNames = reader.TypeDefinitions
            .Select(handle =>
            {
                var row = reader.GetTypeDefinition(handle);
                return new W8FrameMetadataTypeName(
                    MetadataTokens.GetToken(handle),
                    reader.GetString(row.Namespace),
                    reader.GetString(row.Name));
            })
            .Concat(reader.TypeReferences.Select(handle =>
            {
                var row = reader.GetTypeReference(handle);
                return new W8FrameMetadataTypeName(
                    MetadataTokens.GetToken(handle),
                    reader.GetString(row.Namespace),
                    reader.GetString(row.Name));
            }))
            .ToImmutableDictionary(static item => item.Token);
        return new W8FrameMetadataCatalog(
            moduleVersionId,
            definitions.ToImmutableDictionary(static item => item.Profile, StringComparer.Ordinal),
            pdbBytes,
            typeNames);
    }

    internal W8FrameMetadataTypeName GetTypeName(int token) => typeNames[token];

    internal W8FrameActiveScopes ReadActiveScopes(W8FrameExpectedProfile expected, int ilOffset)
    {
        using var stream = new MemoryStream(pdbBytes, writable: false);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream, MetadataStreamOptions.LeaveOpen);
        var reader = provider.GetMetadataReader();
        var methodHandle = MetadataTokens.MethodDefinitionHandle(
            expected.MethodDefinitionToken & 0x00ff_ffff);
        var activeRows = reader.GetLocalScopes(methodHandle)
            .Select(handle => (Handle: handle, Scope: reader.GetLocalScope(handle)))
            .Where(row => row.Scope.StartOffset <= ilOffset && ilOffset < row.Scope.EndOffset)
            .OrderBy(static row => row.Scope.StartOffset)
            .ThenByDescending(static row => row.Scope.EndOffset)
            .ThenBy(static row => MetadataTokens.GetRowNumber(row.Handle))
            .Take(MaximumActiveScopesForCatalog + 1)
            .ToArray();
        Assert.True(activeRows.Length <= MaximumActiveScopesForCatalog);

        var scopes = ImmutableArray.CreateBuilder<W8FramePdbScope>(activeRows.Length);
        var variables = ImmutableArray.CreateBuilder<W8FramePdbVariable>();
        var constants = ImmutableArray.CreateBuilder<W8FramePdbConstant>();
        foreach (var row in activeRows)
        {
            var scopeToken = MetadataTokens.GetToken(row.Handle);
            scopes.Add(new W8FramePdbScope(
                scopeToken,
                row.Scope.StartOffset,
                row.Scope.EndOffset));
            foreach (var handle in row.Scope.GetLocalVariables())
            {
                var variable = reader.GetLocalVariable(handle);
                variables.Add(new W8FramePdbVariable(
                    scopeToken,
                    variable.Index,
                    reader.GetString(variable.Name),
                    variable.Attributes,
                    expected.LocalTypeShapes[variable.Index]));
            }

            foreach (var handle in row.Scope.GetLocalConstants())
            {
                var constant = reader.GetLocalConstant(handle);
                constants.Add(new W8FramePdbConstant(
                    scopeToken,
                    reader.GetString(constant.Name),
                    CanonicalReplayEncoding.ComputeSha256(
                        reader.GetBlobBytes(constant.Signature))));
            }
        }

        Assert.True(variables.Count <= MaximumVariablesForCatalog);
        Assert.True(constants.Count <= MaximumConstantsForCatalog);
        return new W8FrameActiveScopes(
            scopes.ToImmutable(),
            variables.ToImmutable(),
            constants.ToImmutable());
    }

    internal ImmutableHashSet<string> ReadMethodVariableNames(W8FrameExpectedProfile expected)
    {
        using var stream = new MemoryStream(pdbBytes, writable: false);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream, MetadataStreamOptions.LeaveOpen);
        var reader = provider.GetMetadataReader();
        var methodHandle = MetadataTokens.MethodDefinitionHandle(
            expected.MethodDefinitionToken & 0x00ff_ffff);
        var variables = reader.GetLocalScopes(methodHandle)
            .SelectMany(handle => reader.GetLocalScope(handle).GetLocalVariables())
            .Take(MaximumVariablesForCatalog + 1)
            .Select(handle => reader.GetString(reader.GetLocalVariable(handle).Name))
            .ToArray();
        Assert.True(variables.Length <= MaximumVariablesForCatalog);
        return variables.ToImmutableHashSet(StringComparer.Ordinal);
    }

    private const int MaximumActiveScopesForCatalog = 256;
    private const int MaximumVariablesForCatalog = 256;
    private const int MaximumConstantsForCatalog = 256;

    private static W8FrameExpectedProfile CreateProfile(
        PEReader peReader,
        MetadataReader reader,
        string profile,
        string typeNamespace,
        string typeName,
        string methodName,
        int parameterCount,
        int declaringTypeGenericArity,
        int methodGenericArity,
        IReadOnlyList<TypeDefinitionHandle> expectedGenericArguments,
        IReadOnlyList<string> referenceParameterNames,
        IReadOnlyList<string> valueParameterNames,
        IReadOnlyList<string> requiredActiveVariableNames,
        IReadOnlyList<string>? inactiveVariableNames = null)
    {
        var typeHandle = FindType(reader, typeNamespace, typeName);
        var type = reader.GetTypeDefinition(typeHandle);
        var methods = type.GetMethods().Where(handle =>
        {
            var method = reader.GetMethodDefinition(handle);
            return string.Equals(reader.GetString(method.Name), methodName, StringComparison.Ordinal) &&
                method.GetParameters().Count(candidate =>
                    reader.GetParameter(candidate).SequenceNumber > 0) == parameterCount &&
                method.GetGenericParameters().Count == methodGenericArity;
        }).ToArray();
        var methodHandle = Assert.Single(methods);
        var method = reader.GetMethodDefinition(methodHandle);
        Assert.Equal(declaringTypeGenericArity, type.GetGenericParameters().Count);
        var signature = method.DecodeSignature(new W8FrameSignatureShapeProvider(reader), genericContext: null);
        var localTypes = method.RelativeVirtualAddress == 0
            ? ImmutableArray<string>.Empty
            : DecodeLocalTypes(peReader, reader, method);
        var parameters = method.GetParameters()
            .Select(handle => (Handle: handle, Row: reader.GetParameter(handle)))
            .Where(static item => item.Row.SequenceNumber > 0)
            .OrderBy(static item => item.Row.SequenceNumber)
            .Select(item => new W8FrameParameter(
                MetadataTokens.GetToken(item.Handle),
                item.Row.SequenceNumber,
                reader.GetString(item.Row.Name),
                signature.ParameterTypes[item.Row.SequenceNumber - 1]))
            .ToImmutableArray();
        Assert.Equal(parameterCount, parameters.Length);
        return new W8FrameExpectedProfile(
            profile,
            MetadataTokens.GetToken(typeHandle),
            MetadataTokens.GetToken(methodHandle),
            (method.Attributes & MethodAttributes.Static) == 0,
            declaringTypeGenericArity,
            methodGenericArity,
            (method.Attributes & MethodAttributes.Static) == 0
                ? $"this:TypeDef:{MetadataTokens.GetToken(typeHandle):x8}"
                : string.Empty,
            expectedGenericArguments
                .Select(static handle => MetadataTokens.GetToken(handle))
                .ToImmutableArray(),
            localTypes,
            parameters,
            referenceParameterNames.ToImmutableHashSet(StringComparer.Ordinal),
            valueParameterNames.ToImmutableHashSet(StringComparer.Ordinal),
            requiredActiveVariableNames.ToImmutableHashSet(StringComparer.Ordinal),
            (inactiveVariableNames ?? []).ToImmutableHashSet(StringComparer.Ordinal));
    }

    private static ImmutableArray<string> DecodeLocalTypes(
        PEReader peReader,
        MetadataReader reader,
        MethodDefinition method)
    {
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        return body.LocalSignature.IsNil
            ? ImmutableArray<string>.Empty
            : reader.GetStandaloneSignature(body.LocalSignature)
                .DecodeLocalSignature(new W8FrameSignatureShapeProvider(reader), genericContext: null);
    }

    private static TypeDefinitionHandle FindType(
        MetadataReader reader,
        string expectedNamespace,
        string expectedName)
    {
        var matches = reader.TypeDefinitions.Where(handle =>
        {
            var type = reader.GetTypeDefinition(handle);
            return string.Equals(reader.GetString(type.Namespace), expectedNamespace, StringComparison.Ordinal) &&
                string.Equals(reader.GetString(type.Name), expectedName, StringComparison.Ordinal);
        }).ToArray();
        return Assert.Single(matches);
    }
}

internal sealed class W8FrameSignatureShapeProvider : ISignatureTypeProvider<string, object?>
{
    private readonly MetadataReader reader;

    internal W8FrameSignatureShapeProvider(MetadataReader reader) => this.reader = reader;

    public string GetArrayType(string elementType, ArrayShape shape) =>
        $"array:{shape.Rank}:{elementType}";

    public string GetByReferenceType(string elementType) => "byref:" + elementType;

    public string GetFunctionPointerType(MethodSignature<string> signature) => "function-pointer";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        $"constructed:{genericType}<{string.Join(",", typeArguments)}>";

    public string GetGenericMethodParameter(object? genericContext, int index) => $"MVAR:{index}";

    public string GetGenericTypeParameter(object? genericContext, int index) => $"VAR:{index}";

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
        $"modified:{(isRequired ? "required" : "optional")}:{modifier}:{unmodifiedType}";

    public string GetPinnedType(string elementType) => "pinned:" + elementType;

    public string GetPointerType(string elementType) => "pointer:" + elementType;

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Object or PrimitiveTypeCode.String => "reference:" + typeCode,
        PrimitiveTypeCode.Void => "void",
        _ => "value:" + typeCode,
    };

    public string GetSZArrayType(string elementType) => "szarray:" + elementType;

    public string GetTypeFromDefinition(
        MetadataReader metadataReader,
        TypeDefinitionHandle handle,
        byte rawTypeKind)
    {
        var definition = metadataReader.GetTypeDefinition(handle);
        return IsValueType(definition.BaseType)
            ? $"value:TypeDef:{MetadataTokens.GetToken(handle):x8}"
            : $"reference:TypeDef:{MetadataTokens.GetToken(handle):x8}";
    }

    public string GetTypeFromReference(
        MetadataReader metadataReader,
        TypeReferenceHandle handle,
        byte rawTypeKind)
    {
        var reference = metadataReader.GetTypeReference(handle);
        var ns = metadataReader.GetString(reference.Namespace);
        var name = metadataReader.GetString(reference.Name);
        return ns == "System" && (name == "ValueType" || name == "Enum")
            ? $"value:TypeRef:{MetadataTokens.GetToken(handle):x8}"
            : $"reference:TypeRef:{MetadataTokens.GetToken(handle):x8}";
    }

    public string GetTypeFromSpecification(
        MetadataReader metadataReader,
        object? genericContext,
        TypeSpecificationHandle handle,
        byte rawTypeKind) =>
        metadataReader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    private bool IsValueType(EntityHandle baseType)
    {
        if (baseType.Kind == HandleKind.TypeReference)
        {
            var reference = reader.GetTypeReference((TypeReferenceHandle)baseType);
            return reader.GetString(reference.Namespace) == "System" &&
                reader.GetString(reference.Name) is "ValueType" or "Enum";
        }

        return false;
    }
}
