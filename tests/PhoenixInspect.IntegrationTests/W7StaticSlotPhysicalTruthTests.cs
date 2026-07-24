using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the W7 static-slot physical truth gate against the unchanged optimized Release fixture.
/// </summary>
/// <remarks>
/// This gate proves the physical metadata-to-slot chain before any W7 product behavior is introduced. ClrMD
/// convenience reads and heap enumeration occur only after project-owned raw reads establish the pointer and target
/// method table.
/// </remarks>
public sealed class W7StaticSlotPhysicalTruthTests
{
    private const string Expression =
        "global::PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root";
    private const int MaximumRuntimeModules = 256;
    private const int MaximumRuntimeDomains = 16;
    private const int MaximumRuntimeStaticFields = 256;
    private const int MaximumMetadataTypes = 4_096;
    private const int MaximumMetadataFieldsOnType = 1_024;
    private const int MaximumSyntaxNodesAndTokens = 256;
    private const int MaximumSyntaxDepth = 64;

    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular,
        preprocessorSymbols: Array.Empty<string>());

    /// <summary>
    /// Proves one bounded Roslyn tree, counted metadata, one exact runtime declaration/domain, a stable nonzero slot,
    /// and exact project-owned pointer/header reads all identify the same optimized-fixture object.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W7StaticSlotPhysicalTruthV1")]
    public void Counted_metadata_and_raw_memory_prove_the_initialized_static_object()
    {
        var syntaxProjection = ParseAndProject(Expression);
        Assert.True(syntaxProjection.HasGlobalQualifier);
        Assert.Equal(
            new[]
            {
                "PhoenixInspect",
                "OptimizedContextTestTarget",
                "StaticContextProbe",
                "Root",
            },
            syntaxProjection.Segments);

        var targetExecutable = OptimizedContextTestTargetPaths.ResolveExecutable();
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w7-static-slot-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(targetExecutable))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var firstObservation = ObservePhysicalTruth(dumpPath);

            // ObservePhysicalTruth owns and disposes every ClrMD runtime and project session before returning. The
            // replay therefore cannot reuse an object graph, cached field, slot, or open dump handle from the first
            // observation: it must independently rebind the complete chain in a fresh DataTarget and session.
            var replayObservation = ObservePhysicalTruth(dumpPath);

            Assert.Equal(firstObservation.ModuleIdentity, replayObservation.ModuleIdentity);
            Assert.Equal(firstObservation.ModuleMvid, replayObservation.ModuleMvid);
            Assert.Equal(firstObservation.TypeToken, replayObservation.TypeToken);
            Assert.Equal(firstObservation.FieldToken, replayObservation.FieldToken);
            Assert.Equal(firstObservation.SlotAddress, replayObservation.SlotAddress);
            Assert.Equal(firstObservation.TraversalCounts, replayObservation.TraversalCounts);
            AssertRawReferenceChainsEqual(firstObservation.ReferenceChain, replayObservation.ReferenceChain);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    /// <summary>
    /// Proves the expected fully qualified, simple-name, and alias spellings are bounded member-access trees and that
    /// malformed neighboring text remains a Roslyn syntax error rather than entering a text-level splitter.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Roslyn_tree_probe_freezes_qualified_and_contextual_spellings()
    {
        var qualified = ParseAndProject(Expression);
        var simple = ParseAndProject("StaticContextProbe.Root");
        var alias = ParseAndProject("Probe.Root");

        Assert.True(qualified.HasGlobalQualifier);
        Assert.Equal(4, qualified.Segments.Length);
        Assert.False(simple.HasGlobalQualifier);
        Assert.Equal(new[] { "StaticContextProbe", "Root" }, simple.Segments);
        Assert.False(alias.HasGlobalQualifier);
        Assert.Equal(new[] { "Probe", "Root" }, alias.Segments);

        var malformed = SyntaxFactory.ParseExpression(
            "global::PhoenixInspect.StaticContextProbe.",
            offset: 0,
            options: ParseOptions,
            consumeFullText: true);
        Assert.Contains(
            malformed.GetDiagnostics(),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static PhysicalTruthObservation ObservePhysicalTruth(string dumpPath)
    {
        var dataTargetOptions = new DataTargetOptions
        {
            CacheOptions = new CacheOptions
            {
                MaxDumpCacheSize = 256L * 1_024 * 1_024,
                CacheStackRoots = false,
                CacheStackTraces = false,
            },
            FileLocator = ClrmdOfflineFileLocator.Instance,
        };
        using var dataTarget = DataTarget.LoadDump(dumpPath, dataTargetOptions);
        var clrInfo = Assert.Single(dataTarget.ClrVersions);
        using var runtime = clrInfo.CreateRuntime();

        var runtimeModules = EnumerateBounded(
            runtime.EnumerateModules(),
            MaximumRuntimeModules,
            "runtime modules");
        var runtimeModule = Assert.Single(runtimeModules, static candidate => string.Equals(
            Path.GetFileName(candidate.Name),
            ModeledIncidentContextCorpus.TargetAssemblyName,
            StringComparison.OrdinalIgnoreCase));

        var runtimeDomains = EnumerateBounded(
            runtime.AppDomains,
            MaximumRuntimeDomains,
            "runtime application domains");
        var runtimeDomain = Assert.Single(
            runtimeDomains,
            candidate => candidate.Address == runtimeModule.AppDomain.Address);
        var runtimeType = Assert.IsAssignableFrom<ClrType>(
            runtimeModule.GetTypeByName(ModeledIncidentContextCorpus.StaticHolderTypeName));

        var runtimeStaticFields = EnumerateBounded(
            runtimeType.StaticFields,
            MaximumRuntimeStaticFields,
            "runtime static fields on the bound type");
        var runtimeField = Assert.Single(
            runtimeStaticFields,
            candidate => string.Equals(
                candidate.Name,
                ModeledIncidentContextCorpus.StaticFieldName,
                StringComparison.Ordinal));

        Assert.Same(runtimeModule, runtimeType.Module);
        Assert.Equal(runtimeModule.AppDomain.Address, runtimeDomain.Address);
        Assert.Equal(FieldAttributes.Static, runtimeField.Attributes & FieldAttributes.Static);
        Assert.True(
            (runtimeField.Attributes & (FieldAttributes.Literal | FieldAttributes.HasFieldRVA)) == 0);
        Assert.True(runtimeField.IsObjectReference);
        Assert.Equal(runtimeType.MetadataToken, runtimeField.Type?.MetadataToken);

        var slotAddress = runtimeField.GetAddress(runtimeDomain);
        Assert.NotEqual(0UL, slotAddress);
        Assert.Equal(slotAddress, runtimeField.GetAddress(runtimeDomain));
        Assert.True(runtimeField.IsInitialized(runtimeDomain));

        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var session = Assert.IsType<ClrmdDumpSession>(opened.Value);
        var module = Assert.Single(session.FindModulesByFileName(
            ModeledIncidentContextCorpus.TargetAssemblyName));
        Assert.Equal(runtimeModule.Address, module.Identity.ModuleAddress);
        Assert.Equal(runtimeModule.AppDomain.Address, module.Identity.AppDomainAddress);
        Assert.Equal(runtimeModule.ImageBase, module.Identity.ImageBase);
        Assert.Equal(runtimeModule.Size, module.Identity.ImageSize);

        var metadataResult = session.ReadModuleContentIdentity(module);
        Assert.Equal(ClrmdEvidenceStatus.Exact, metadataResult.Status);
        Assert.Equal(ClrmdValueIssue.None, metadataResult.Issue);
        var moduleContent = Assert.IsType<PhoenixInspect.Core.Abstractions.ModuleContentIdentity>(metadataResult.Value);
        var metadataRead = Assert.Single(metadataResult.Evidence);
        Assert.Equal(MemoryReadStatus.Exact, metadataRead.Status);
        Assert.Equal(module.MetadataAddress, metadataRead.Address);
        Assert.Equal(checked((int)module.MetadataLength), metadataRead.RequestedLength);
        Assert.Equal(moduleContent.MetadataLength, metadataRead.BytesRead);

        using var metadataProvider = MetadataReaderProvider.FromMetadataImage(metadataRead.Bytes);
        var metadata = metadataProvider.GetMetadataReader();
        Assert.Equal(moduleContent.Mvid, metadata.GetGuid(metadata.GetModuleDefinition().Mvid));

        var metadataTypes = EnumerateBounded(
            metadata.TypeDefinitions,
            MaximumMetadataTypes,
            "metadata type definitions");
        var typeHandle = Assert.Single(metadataTypes, handle => IsExactTopLevelType(
            metadata,
            handle,
            "PhoenixInspect.OptimizedContextTestTarget",
            "StaticContextProbe"));
        var typeDefinition = metadata.GetTypeDefinition(typeHandle);
        Assert.Empty(typeDefinition.GetGenericParameters());
        Assert.True((typeDefinition.Attributes & TypeAttributes.Interface) == 0);

        var metadataFields = EnumerateBounded(
            typeDefinition.GetFields(),
            MaximumMetadataFieldsOnType,
            "metadata fields on the bound type");
        var fieldHandle = Assert.Single(metadataFields, handle => string.Equals(
            metadata.GetString(metadata.GetFieldDefinition(handle).Name),
            ModeledIncidentContextCorpus.StaticFieldName,
            StringComparison.Ordinal));
        var fieldDefinition = metadata.GetFieldDefinition(fieldHandle);
        Assert.Equal(FieldAttributes.Static, fieldDefinition.Attributes & FieldAttributes.Static);
        Assert.True(
            (fieldDefinition.Attributes & (FieldAttributes.Literal | FieldAttributes.HasFieldRVA)) == 0);
        Assert.DoesNotContain(
            fieldDefinition.GetCustomAttributes(),
            handle => IsNamedAttribute(metadata, handle, "System", "ThreadStaticAttribute"));

        var signature = metadata.GetBlobReader(fieldDefinition.Signature);
        Assert.Equal(SignatureKind.Field, signature.ReadSignatureHeader().Kind);
        Assert.Equal(SignatureTypeCode.TypeHandle, signature.ReadSignatureTypeCode());
        Assert.Equal(typeHandle, signature.ReadTypeHandle());
        Assert.Equal(0, signature.RemainingBytes);

        Assert.Equal(MetadataTokens.GetToken(typeHandle), runtimeType.MetadataToken);
        Assert.Equal(MetadataTokens.GetToken(fieldHandle), runtimeField.Token);
        Assert.Equal(runtimeField.Token, MetadataTokens.GetToken(fieldHandle));

        var referenceChain = ReadRawReferenceChain(session, slotAddress, runtimeType.MethodTable);
        Assert.Equal(2, referenceChain.Evidence.Length);
        Assert.Equal(slotAddress, referenceChain.Evidence[0].Address);
        Assert.Equal(referenceChain.ObjectAddress, referenceChain.Evidence[1].Address);
        Assert.Equal(runtimeType.MethodTable, referenceChain.MethodTable);

        // These are deliberately late equality oracles. Neither supplies the asserted slot, pointer, or header.
        var oracleObject = runtimeField.ReadObject(runtimeDomain);
        Assert.True(oracleObject.IsValid);
        Assert.Equal(referenceChain.ObjectAddress, oracleObject.Address);
        Assert.Equal(runtimeType.MetadataToken, oracleObject.Type?.MetadataToken);
        Assert.Equal(runtimeType.MethodTable, oracleObject.Type?.MethodTable);

        var heapMatches = runtime.Heap.EnumerateObjects(carefully: true)
            .Where(candidate => string.Equals(
                candidate.Type?.Name,
                ModeledIncidentContextCorpus.StaticHolderTypeName,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        var heapObject = Assert.Single(heapMatches);
        Assert.Equal(referenceChain.ObjectAddress, heapObject.Address);
        Assert.Equal(runtimeType.MethodTable, heapObject.Type?.MethodTable);

        return new PhysicalTruthObservation(
            module.Identity,
            moduleContent.Mvid,
            runtimeType.MetadataToken,
            runtimeField.Token,
            slotAddress,
            referenceChain,
            new TraversalCounts(
                runtimeModules.Length,
                runtimeDomains.Length,
                runtimeStaticFields.Length,
                metadataTypes.Length,
                metadataFields.Length));
    }

    private static ImmutableArray<T> EnumerateBounded<T>(
        IEnumerable<T> source,
        int maximumCount,
        string evidenceName)
    {
        var observed = source.Take(checked(maximumCount + 1)).ToImmutableArray();
        Assert.True(
            observed.Length <= maximumCount,
            $"The {evidenceName} traversal exceeded its explicit cap of {maximumCount}; " +
            $"the cap-plus-one probe observed at least {observed.Length} items.");
        Assert.NotEmpty(observed);
        return observed;
    }

    private static StaticSyntaxProjection ParseAndProject(string expression)
    {
        var syntax = SyntaxFactory.ParseExpression(
            expression,
            offset: 0,
            options: ParseOptions,
            consumeFullText: true);
        Assert.DoesNotContain(
            syntax.GetDiagnostics(),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Equal(expression, syntax.ToFullString());
        Assert.True(
            syntax.DescendantNodesAndTokensAndSelf(descendIntoTrivia: false).Count() <=
            MaximumSyntaxNodesAndTokens);
        Assert.True(GetMaximumSyntaxDepth(syntax) <= MaximumSyntaxDepth);

        var segments = ImmutableArray.CreateBuilder<string>();
        var hasGlobalQualifier = FlattenName(syntax, segments);
        Assert.InRange(segments.Count, 2, 32);
        return new StaticSyntaxProjection(hasGlobalQualifier, segments.ToImmutable());
    }

    private static bool FlattenName(SyntaxNode node, ImmutableArray<string>.Builder segments)
    {
        switch (node)
        {
            case MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: var receiver,
                Name: IdentifierNameSyntax member,
            }:
                var hasGlobalQualifier = FlattenName(receiver, segments);
                segments.Add(member.Identifier.ValueText);
                return hasGlobalQualifier;
            case AliasQualifiedNameSyntax
            {
                Alias.Identifier.RawKind: (int)SyntaxKind.GlobalKeyword,
                Name: IdentifierNameSyntax globalMember,
            }:
                segments.Add(globalMember.Identifier.ValueText);
                return true;
            case IdentifierNameSyntax identifier:
                segments.Add(identifier.Identifier.ValueText);
                return false;
            default:
                throw new Xunit.Sdk.XunitException(
                    $"The W7 truth-gate expression produced unsupported node kind {node.Kind()}.");
        }
    }

    private static int GetMaximumSyntaxDepth(SyntaxNode syntax) =>
        syntax.DescendantNodesAndSelf(descendIntoTrivia: false)
            .Max(static node => node.Ancestors().Count() + 1);

    private static RawReferenceChain ReadRawReferenceChain(
        ClrmdDumpSession session,
        ulong slotAddress,
        ulong expectedMethodTable)
    {
        var slotRead = session.Memory.Read(slotAddress, session.Memory.PointerSize);
        Assert.Equal(MemoryReadStatus.Exact, slotRead.Status);
        var objectAddress = DecodePointer(slotRead, session.Memory.PointerSize);
        Assert.NotEqual(0UL, objectAddress);

        var methodTableRead = session.Memory.Read(objectAddress, session.Memory.PointerSize);
        Assert.Equal(MemoryReadStatus.Exact, methodTableRead.Status);
        var methodTable = DecodePointer(methodTableRead, session.Memory.PointerSize);
        Assert.NotEqual(0UL, methodTable);
        Assert.Equal(expectedMethodTable, methodTable);

        return new RawReferenceChain(
            objectAddress,
            methodTable,
            ImmutableArray.Create(Detach(slotRead), Detach(methodTableRead)));
    }

    private static DetachedMemoryRead Detach(MemoryReadResult read) => new(
        read.SourceId,
        read.Address,
        read.RequestedLength,
        read.Status,
        ImmutableArray.Create(read.Bytes.AsSpan().ToArray()));

    private static void AssertRawReferenceChainsEqual(
        RawReferenceChain expected,
        RawReferenceChain actual)
    {
        Assert.Equal(expected.ObjectAddress, actual.ObjectAddress);
        Assert.Equal(expected.MethodTable, actual.MethodTable);
        Assert.Equal(expected.Evidence.Length, actual.Evidence.Length);

        for (var index = 0; index < expected.Evidence.Length; index++)
        {
            var expectedRead = expected.Evidence[index];
            var actualRead = actual.Evidence[index];
            Assert.Equal(expectedRead.SourceId, actualRead.SourceId);
            Assert.Equal(expectedRead.Address, actualRead.Address);
            Assert.Equal(expectedRead.RequestedLength, actualRead.RequestedLength);
            Assert.Equal(expectedRead.Status, actualRead.Status);
            Assert.True(expectedRead.Bytes.AsSpan().SequenceEqual(actualRead.Bytes.AsSpan()));
        }
    }

    private static ulong DecodePointer(MemoryReadResult read, int pointerSize) => pointerSize switch
    {
        sizeof(uint) when read.Bytes.Length == sizeof(uint) =>
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(read.Bytes.AsSpan()),
        sizeof(ulong) when read.Bytes.Length == sizeof(ulong) =>
            System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(read.Bytes.AsSpan()),
        _ => throw new Xunit.Sdk.XunitException("The raw pointer read has an invalid target width."),
    };

    private static bool IsExactTopLevelType(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        string expectedNamespace,
        string expectedName)
    {
        var definition = reader.GetTypeDefinition(handle);
        return definition.GetDeclaringType().IsNil &&
            string.Equals(reader.GetString(definition.Namespace), expectedNamespace, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(definition.Name), expectedName, StringComparison.Ordinal);
    }

    private static bool IsNamedAttribute(
        MetadataReader reader,
        CustomAttributeHandle handle,
        string expectedNamespace,
        string expectedName)
    {
        var attribute = reader.GetCustomAttribute(handle);
        EntityHandle typeHandle = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference(
                (MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition(
                (MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default,
        };

        return typeHandle.Kind switch
        {
            HandleKind.TypeDefinition => IsNamedType(
                reader,
                reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle).Namespace,
                reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle).Name,
                expectedNamespace,
                expectedName),
            HandleKind.TypeReference => IsNamedType(
                reader,
                reader.GetTypeReference((TypeReferenceHandle)typeHandle).Namespace,
                reader.GetTypeReference((TypeReferenceHandle)typeHandle).Name,
                expectedNamespace,
                expectedName),
            _ => false,
        };
    }

    private static bool IsNamedType(
        MetadataReader reader,
        StringHandle namespaceHandle,
        StringHandle nameHandle,
        string expectedNamespace,
        string expectedName) =>
        string.Equals(reader.GetString(namespaceHandle), expectedNamespace, StringComparison.Ordinal) &&
        string.Equals(reader.GetString(nameHandle), expectedName, StringComparison.Ordinal);

    private sealed record StaticSyntaxProjection(
        bool HasGlobalQualifier,
        ImmutableArray<string> Segments);

    private sealed record PhysicalTruthObservation(
        ClrmdRuntimeModuleIdentity ModuleIdentity,
        Guid ModuleMvid,
        int TypeToken,
        int FieldToken,
        ulong SlotAddress,
        RawReferenceChain ReferenceChain,
        TraversalCounts TraversalCounts);

    private sealed record TraversalCounts(
        int RuntimeModules,
        int RuntimeDomains,
        int RuntimeStaticFields,
        int MetadataTypes,
        int MetadataFields);

    private sealed record RawReferenceChain(
        ulong ObjectAddress,
        ulong MethodTable,
        ImmutableArray<DetachedMemoryRead> Evidence);

    private sealed record DetachedMemoryRead(
        string SourceId,
        ulong Address,
        int RequestedLength,
        MemoryReadStatus Status,
        ImmutableArray<byte> Bytes);
}
