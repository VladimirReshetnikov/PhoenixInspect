using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Xunit;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises the dump execution adapter with counted synthetic memory evidence and no live process or dump capture.
/// </summary>
public sealed class ClrmdDumpExecutionResolverGraphTests
{
    private const int ExpectedMarker = 0x13579BDF;

    /// <summary>
    /// Proves graph issuance is atomic and input-order independent, retained bodies are canonical, and valid
    /// same-module MethodDefs omitted from the admitted graph remain body-free unavailable dependencies.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W4DumpResolverGraphV1")]
    public void Method_graph_is_atomic_canonical_and_body_free_for_omitted_targets()
    {
        var fixture = SyntheticFixture.Create();
        var graphResult = ClrmdDumpExecutionResolver.CreateMethodGraph(
            fixture.Module,
            fixture.MetadataIdentity,
            fixture.RootBody,
            ImmutableArray.Create(fixture.ExtraBody, fixture.HelperBody));

        var resolver = AssertSuccess(graphResult);
        Assert.Equal(resolver.RootMethod, resolver.Method);
        Assert.Same(resolver.RootMethodDefinition, resolver.MethodDefinition);
        Assert.Equal(fixture.RootToken, resolver.RootMethod.MetadataToken);
        var expectedMethods = new[]
        {
            fixture.RootToken,
            fixture.HelperToken,
            fixture.ExtraToken,
        }.OrderBy(static token => token).ToArray();
        Assert.Equal(
            expectedMethods,
            resolver.InterpretedMethods.Select(static method => method.MetadataToken).ToArray());
        Assert.All(resolver.InterpretedMethods, method => Assert.Equal(resolver.ModuleHandle, method.Module));
        Assert.All(
            resolver.InterpretedMethods,
            method => Assert.Equal(method, AssertSuccess(resolver.GetMethodDefinition(method)).Method));
        var reorderedResolver = AssertSuccess(ClrmdDumpExecutionResolver.CreateMethodGraph(
            fixture.Module,
            fixture.MetadataIdentity,
            fixture.RootBody,
            ImmutableArray.Create(fixture.HelperBody, fixture.ExtraBody)));
        Assert.Equal(
            resolver.InterpretedMethods.ToArray(),
            reorderedResolver.InterpretedMethods.ToArray());
        Assert.All(
            resolver.InterpretedMethods,
            method => Assert.Equal(
                AssertSuccess(resolver.GetMethodDefinition(method)),
                AssertSuccess(reorderedResolver.GetMethodDefinition(method))));

        var helperTarget = AssertSuccess(resolver.ResolveMethod(resolver.RootMethod, fixture.HelperToken));
        Assert.Equal(fixture.HelperToken, helperTarget.Method.MetadataToken);
        var markerField = AssertSuccess(resolver.ResolveField(resolver.RootMethod, fixture.MarkerFieldToken));
        Assert.Equal(fixture.MarkerFieldToken, markerField.Handle.MetadataToken);

        var rootOnly = AssertSuccess(ClrmdDumpExecutionResolver.Create(
            fixture.Module,
            fixture.MetadataIdentity,
            fixture.RootBody));
        Assert.Single(rootOnly.InterpretedMethods);
        Assert.Equal(rootOnly.RootMethod, rootOnly.InterpretedMethods[0]);
        var omittedHelper = new MethodHandle(rootOnly.ModuleHandle, fixture.HelperToken);
        var missingBody = rootOnly.GetMethodDefinition(omittedHelper);
        AssertFailure(
            missingBody,
            ResolutionFailureKind.Unavailable,
            "DUMP_EXEC_METHOD_BODY_UNAVAILABLE");
        Assert.Equal(
            fixture.HelperToken,
            AssertSuccess(rootOnly.ResolveMethod(rootOnly.RootMethod, fixture.HelperToken)).Method.MetadataToken);
        AssertFailure(
            rootOnly.ResolveMethod(omittedHelper, fixture.HelperToken),
            ResolutionFailureKind.Unavailable,
            "DUMP_EXEC_CALL_CONTEXT_BODY_UNAVAILABLE");
        AssertFailure(
            rootOnly.ResolveField(omittedHelper, fixture.MarkerFieldToken),
            ResolutionFailureKind.Unavailable,
            "DUMP_EXEC_FIELD_CONTEXT_BODY_UNAVAILABLE");

        var foreignModule = new ModuleHandle(
            rootOnly.ModuleHandle.High ^ 0x8000_0000_0000_0000UL,
            rootOnly.ModuleHandle.Low);
        var foreignMethod = new MethodHandle(foreignModule, fixture.RootToken);
        AssertFailure(
            rootOnly.GetMethodDefinition(foreignMethod),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_METHOD_MISMATCH");
        AssertFailure(
            rootOnly.ResolveMethod(foreignMethod, fixture.HelperToken),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_CALL_CONTEXT_MISMATCH");
        AssertFailure(
            rootOnly.ResolveField(foreignMethod, fixture.MarkerFieldToken),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_FIELD_CONTEXT_MISMATCH");
    }

    /// <summary>
    /// Proves graph construction rejects uninitialized, duplicate, metadata-conflicting, non-exact, and oversized
    /// body sets without publishing a partially usable resolver.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W4DumpResolverGraphV1")]
    public void Method_graph_rejects_duplicate_conflicting_and_nonexact_body_sets()
    {
        var fixture = SyntheticFixture.Create();

        AssertFailure(
            ClrmdDumpExecutionResolver.CreateMethodGraph(
                fixture.Module,
                fixture.MetadataIdentity,
                fixture.RootBody,
                default),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_METHOD_GRAPH_UNINITIALIZED");
        AssertFailure(
            ClrmdDumpExecutionResolver.CreateMethodGraph(
                fixture.Module,
                fixture.MetadataIdentity,
                fixture.RootBody,
                ImmutableArray.Create(fixture.RootBody)),
            ResolutionFailureKind.Conflict,
            "DUMP_EXEC_METHOD_GRAPH_DUPLICATE");

        var shiftedHelper = ShiftBodyRva(fixture.HelperBody, 1);
        AssertFailure(
            ClrmdDumpExecutionResolver.CreateMethodGraph(
                fixture.Module,
                fixture.MetadataIdentity,
                fixture.RootBody,
                ImmutableArray.Create(shiftedHelper)),
            ResolutionFailureKind.Conflict,
            "DUMP_EXEC_METHOD_RVA_CONFLICT");

        var conflictingMetadata = ReplaceCountedMetadataRead(
            fixture.HelperBody,
            MutateFirstByte(fixture.MetadataIdentity.Evidence[0]));
        AssertFailure(
            ClrmdDumpExecutionResolver.CreateMethodGraph(
                fixture.Module,
                fixture.MetadataIdentity,
                fixture.RootBody,
                ImmutableArray.Create(conflictingMetadata)),
            ResolutionFailureKind.Conflict,
            "DUMP_EXEC_METADATA_READ_CONFLICT");

        var partialHelper = ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
            ClrmdEvidenceStatus.Partial,
            ClrmdValueIssue.MemoryUnavailable,
            fixture.HelperBody.Value,
            fixture.HelperBody.Evidence,
            fixture.HelperBody.AppliedBounds);
        AssertFailure(
            ClrmdDumpExecutionResolver.CreateMethodGraph(
                fixture.Module,
                fixture.MetadataIdentity,
                fixture.RootBody,
                ImmutableArray.Create(partialHelper)),
            ResolutionFailureKind.Unavailable,
            "DUMP_EXEC_METHOD_NOT_EXACT");

        var overLimit = Enumerable.Repeat(
            fixture.HelperBody,
            ClrmdDumpExecutionResolver.MaximumInterpretedMethodCount).ToImmutableArray();
        AssertFailure(
            ClrmdDumpExecutionResolver.CreateMethodGraph(
                fixture.Module,
                fixture.MetadataIdentity,
                fixture.RootBody,
                overLimit),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_METHOD_GRAPH_LIMIT");
    }

    /// <summary>
    /// Proves Int32 correlation retains exact, partial, and unavailable counted reads without value invention while
    /// rejecting incoherent status tuples, foreign evidence, malformed owner searches, and unreferenced fields.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W4DumpResolverGraphV1")]
    public void Int32_correlation_preserves_exact_partial_and_unavailable_rows_and_fails_closed()
    {
        var fixture = SyntheticFixture.Create();
        var resolver = AssertSuccess(ClrmdDumpExecutionResolver.Create(
            fixture.Module,
            fixture.MetadataIdentity,
            fixture.RootBody));

        var exact = CreateFieldObservation(fixture, ClrmdEvidenceStatus.Exact);
        var partial = CreateFieldObservation(fixture, ClrmdEvidenceStatus.Partial);
        var unavailable = CreateFieldObservation(fixture, ClrmdEvidenceStatus.Unavailable);
        var exactObservation = exact.Value ??
            throw new InvalidOperationException("The exact synthetic row has no observation.");
        var partialObservation = partial.Value ??
            throw new InvalidOperationException("The partial synthetic row has no observation.");

        var exactEvidence = AssertSuccess(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, exact));
        var partialEvidence = AssertSuccess(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, partial));
        var unavailableEvidence = AssertSuccess(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, unavailable));

        Assert.Equal(ClrmdEvidenceStatus.Exact, exactEvidence.Status);
        Assert.Equal(ClrmdValueIssue.None, exactEvidence.Issue);
        Assert.Equal(ExpectedMarker, exactEvidence.ExactValue);
        Assert.Equal(sizeof(int), exactEvidence.Observation.Memory.BytesRead);
        Assert.Equal(ClrmdEvidenceStatus.Partial, partialEvidence.Status);
        Assert.Equal(ClrmdValueIssue.MemoryUnavailable, partialEvidence.Issue);
        Assert.Null(partialEvidence.ExactValue);
        Assert.Equal(2, partialEvidence.Observation.Memory.BytesRead);
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, unavailableEvidence.Status);
        Assert.Equal(ClrmdValueIssue.MemoryUnavailable, unavailableEvidence.Issue);
        Assert.Null(unavailableEvidence.ExactValue);
        Assert.Empty(unavailableEvidence.Observation.Memory.Bytes);
        Assert.Equal(exactEvidence.Field, partialEvidence.Field);
        Assert.Equal(exactEvidence.Field, unavailableEvidence.Field);
        Assert.Equal(exactEvidence.OwnerEvidenceIdentity, partialEvidence.OwnerEvidenceIdentity);
        Assert.Equal(exactEvidence.OwnerEvidenceIdentity, unavailableEvidence.OwnerEvidenceIdentity);
        Assert.Same(fixture.OwnerSearch, exactEvidence.OwnerSearch);

        var conflict = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Conflict,
            ClrmdValueIssue.AmbiguousMatch,
            exactObservation,
            exact.Evidence);
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, conflict),
            ResolutionFailureKind.Conflict,
            "DUMP_EXEC_FIELD_STATUS_REJECTED");

        var invalid = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Invalid,
            ClrmdValueIssue.InvalidData,
            exactObservation,
            exact.Evidence);
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, invalid),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_FIELD_STATUS_REJECTED");

        var wrongIssue = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Partial,
            ClrmdValueIssue.FieldUnavailable,
            partialObservation,
            partial.Evidence);
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, wrongIssue),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_FIELD_STATUS_TUPLE_INVALID");

        var exactMemoryWithoutValue = new ClrmdInt32FieldObservation(
            fixture.RuntimeField,
            exactObservation.Memory,
            value: null);
        var partialWithExactMemory = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Partial,
            ClrmdValueIssue.MemoryUnavailable,
            exactMemoryWithoutValue,
            ImmutableArray.Create(exactObservation.Memory));
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, partialWithExactMemory),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_FIELD_VALUE_EVIDENCE_INVALID");

        var wrongScalarObservation = new ClrmdInt32FieldObservation(
            fixture.RuntimeField,
            exactObservation.Memory,
            unchecked(ExpectedMarker + 1));
        var wrongScalar = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            wrongScalarObservation,
            ImmutableArray.Create(exactObservation.Memory));
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, wrongScalar),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_FIELD_VALUE_EVIDENCE_INVALID");

        var missingObservation = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Unavailable,
            ClrmdValueIssue.MemoryUnavailable);
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, missingObservation),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_FIELD_STATUS_TUPLE_INVALID");

        var foreignRead = MemoryReadResult.Create(
            "dump-sha256:" + new string('f', 64),
            fixture.RuntimeField.Address,
            sizeof(int),
            exactObservation.Memory.Bytes.AsSpan());
        var foreignObservation = new ClrmdInt32FieldObservation(
            fixture.RuntimeField,
            foreignRead,
            ExpectedMarker);
        var foreignEvidence = ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            foreignObservation,
            ImmutableArray.Create(foreignRead));
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, foreignEvidence),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_FIELD_READ_TUPLE_INVALID");

        var malformedOwnerSearch = new ClrmdHeapObjectSearchResult(
            fixture.OwnerSearch.Snapshot,
            fixture.OwnerSearch.TypeNameSelector,
            fixture.OwnerSearch.Status,
            fixture.OwnerSearch.Issue,
            handlesScanned: 0,
            maximumHandlesScanned: fixture.OwnerSearch.MaximumHandlesScanned,
            maximumMatches: fixture.OwnerSearch.MaximumMatches,
            matchLimitReached: fixture.OwnerSearch.MatchLimitReached,
            matches: fixture.OwnerSearch.Matches,
            evidence: fixture.OwnerSearch.Evidence);
        AssertFailure(
            resolver.CorrelateInt32FieldObservation(malformedOwnerSearch, exact),
            ResolutionFailureKind.Invalid,
            "DUMP_EXEC_OWNER_EVIDENCE_INVALID");

        var fieldFreeResolver = AssertSuccess(ClrmdDumpExecutionResolver.Create(
            fixture.Module,
            fixture.MetadataIdentity,
            fixture.HelperBody));
        AssertFailure(
            fieldFreeResolver.CorrelateInt32FieldObservation(fixture.OwnerSearch, exact),
            ResolutionFailureKind.Conflict,
            "DUMP_EXEC_FIELD_OPERAND_CONFLICT");

        var legacyResolver = AssertSuccess(ClrmdDumpExecutionResolver.Create(
            fixture.Module,
            fixture.MetadataIdentity,
            fixture.ExtraBody));
        var legacyExact = AssertSuccess(legacyResolver.CorrelateExactInt32Field(fixture.OwnerSearch, exact));
        Assert.Equal(ExpectedMarker, legacyExact.Value);
        Assert.Equal(exactEvidence.Field, legacyExact.Field);
    }

    private static ClrmdEvidenceResult<ClrmdInt32FieldObservation> CreateFieldObservation(
        SyntheticFixture fixture,
        ClrmdEvidenceStatus status)
    {
        Span<byte> exactBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(exactBytes, ExpectedMarker);
        ReadOnlySpan<byte> observedBytes = status switch
        {
            ClrmdEvidenceStatus.Exact => exactBytes,
            ClrmdEvidenceStatus.Partial => exactBytes[..2],
            ClrmdEvidenceStatus.Unavailable => ReadOnlySpan<byte>.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        var memory = MemoryReadResult.Create(
            fixture.Module.Identity.Snapshot.MemorySourceId,
            fixture.RuntimeField.Address,
            sizeof(int),
            observedBytes);
        var observation = new ClrmdInt32FieldObservation(
            fixture.RuntimeField,
            memory,
            status == ClrmdEvidenceStatus.Exact ? ExpectedMarker : null);
        return ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
            status,
            status == ClrmdEvidenceStatus.Exact
                ? ClrmdValueIssue.None
                : ClrmdValueIssue.MemoryUnavailable,
            observation,
            ImmutableArray.Create(memory));
    }

    private static ClrmdEvidenceResult<ClrmdMethodBodyInfo> ShiftBodyRva(
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> original,
        ulong delta)
    {
        var info = original.Value ?? throw new InvalidOperationException("Expected an exact method body.");
        var headerEvidence = info.HeaderEvidence
            .Select(read => ShiftRead(read, delta))
            .ToImmutableArray();
        var code = ShiftRead(info.Code, delta);
        var sections = info.ExtraSectionEvidence
            .Select(read => ShiftRead(read, delta))
            .ToImmutableArray();
        var shifted = new ClrmdMethodBodyInfo(
            info.MetadataToken,
            checked(info.RelativeVirtualAddress + (int)delta),
            checked(info.HeaderAddress + delta),
            info.HeaderKind,
            headerEvidence,
            checked(info.CodeAddress + delta),
            code,
            sections,
            info.Body);
        var evidence = ImmutableArray.CreateBuilder<MemoryReadResult>(original.Evidence.Length);
        evidence.Add(original.Evidence[0]);
        evidence.AddRange(headerEvidence);
        evidence.Add(code);
        evidence.AddRange(sections);
        return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            shifted,
            evidence.ToImmutable(),
            original.AppliedBounds);
    }

    private static MemoryReadResult ShiftRead(MemoryReadResult read, ulong delta) =>
        MemoryReadResult.Create(
            read.SourceId,
            checked(read.Address + delta),
            read.RequestedLength,
            read.Bytes.AsSpan());

    private static ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReplaceCountedMetadataRead(
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> original,
        MemoryReadResult replacement)
    {
        var evidence = original.Evidence.SetItem(0, replacement);
        return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
            original.Status,
            original.Issue,
            original.Value,
            evidence,
            original.AppliedBounds);
    }

    private static MemoryReadResult MutateFirstByte(MemoryReadResult original)
    {
        var bytes = original.Bytes.ToArray();
        bytes[0] ^= 0x01;
        return MemoryReadResult.Create(
            original.SourceId,
            original.Address,
            original.RequestedLength,
            bytes);
    }

    private static T AssertSuccess<T>(ResolutionResult<T> result)
    {
        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Null(result.Failure);
        return result.Value;
    }

    private static void AssertFailure<T>(
        ResolutionResult<T> result,
        ResolutionFailureKind kind,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(kind, result.Failure!.Kind);
        Assert.Equal(code, result.Failure.Code);
    }

    private sealed class SyntheticFixture
    {
        private const string RootName = "GetMarkerSummary";
        private const string HelperName = "CombineMarkers";
        private const string ExtraName = "GetMarker";
        private const string ProbeName = "DumpProbe";
        private const string MarkerName = "Marker";

        private SyntheticFixture(
            ClrmdModuleInfo module,
            ClrmdEvidenceResult<ModuleContentIdentity> metadataIdentity,
            ClrmdEvidenceResult<ClrmdMethodBodyInfo> rootBody,
            ClrmdEvidenceResult<ClrmdMethodBodyInfo> helperBody,
            ClrmdEvidenceResult<ClrmdMethodBodyInfo> extraBody,
            int rootToken,
            int helperToken,
            int extraToken,
            int markerFieldToken,
            ClrmdHeapObjectSearchResult ownerSearch,
            ClrmdInstanceFieldInfo runtimeField)
        {
            Module = module;
            MetadataIdentity = metadataIdentity;
            RootBody = rootBody;
            HelperBody = helperBody;
            ExtraBody = extraBody;
            RootToken = rootToken;
            HelperToken = helperToken;
            ExtraToken = extraToken;
            MarkerFieldToken = markerFieldToken;
            OwnerSearch = ownerSearch;
            RuntimeField = runtimeField;
        }

        internal ClrmdModuleInfo Module { get; }

        internal ClrmdEvidenceResult<ModuleContentIdentity> MetadataIdentity { get; }

        internal ClrmdEvidenceResult<ClrmdMethodBodyInfo> RootBody { get; }

        internal ClrmdEvidenceResult<ClrmdMethodBodyInfo> HelperBody { get; }

        internal ClrmdEvidenceResult<ClrmdMethodBodyInfo> ExtraBody { get; }

        internal int RootToken { get; }

        internal int HelperToken { get; }

        internal int ExtraToken { get; }

        internal int MarkerFieldToken { get; }

        internal ClrmdHeapObjectSearchResult OwnerSearch { get; }

        internal ClrmdInstanceFieldInfo RuntimeField { get; }

        internal static SyntheticFixture Create()
        {
            var executablePath = TestTargetPaths.ResolveExecutable();
            var assemblyPath = TestTargetPaths.ResolveAssembly(executablePath);
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var metadata = peReader.GetMetadataReader();
            var metadataBytes = peReader.GetMetadata().GetContent();
            var moduleDefinition = metadata.GetModuleDefinition();
            var mvid = metadata.GetGuid(moduleDefinition.Mvid);
            var contentIdentity = ModuleContentIdentity.FromMetadata(mvid, metadataBytes.AsSpan());
            var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
            const ulong imageBase = 0x0000_0001_0000_0000UL;
            var imageSize = checked((ulong)(peReader.PEHeaders.PEHeader?.SizeOfImage ??
                throw new BadImageFormatException("The synthetic fixture PE has no optional header.")));
            var runtimeIdentity = new ClrmdRuntimeModuleIdentity(
                snapshot,
                AppDomainAddress: 0x0000_0002_0000_0000UL,
                ModuleAddress: 0x0000_0002_0000_1000UL,
                ImageBase: imageBase,
                ImageSize: imageSize);
            const ulong metadataAddress = 0x0000_0003_0000_0000UL;
            var module = new ClrmdModuleInfo(
                runtimeIdentity,
                "Interpreter.TestTarget.dll",
                targetPathHint: null,
                appDomainId: 1,
                metadataAddress: metadataAddress,
                metadataLength: checked((ulong)metadataBytes.Length),
                layout: "SyntheticMapped");
            var metadataRead = MemoryReadResult.Create(
                snapshot.MemorySourceId,
                metadataAddress,
                metadataBytes.Length,
                metadataBytes.AsSpan());
            var metadataIdentity = ClrmdEvidenceResult<ModuleContentIdentity>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                contentIdentity,
                ImmutableArray.Create(metadataRead));

            var probeHandle = metadata.TypeDefinitions.Single(handle =>
            {
                var definition = metadata.GetTypeDefinition(handle);
                return string.Equals(metadata.GetString(definition.Name), ProbeName, StringComparison.Ordinal) &&
                    metadata.GetString(definition.Namespace).Length == 0;
            });
            var probe = metadata.GetTypeDefinition(probeHandle);
            var methodHandles = probe.GetMethods().ToDictionary(
                handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name),
                StringComparer.Ordinal);
            var fieldHandles = probe.GetFields().ToDictionary(
                handle => metadata.GetString(metadata.GetFieldDefinition(handle).Name),
                StringComparer.Ordinal);
            var rootHandle = methodHandles[RootName];
            var helperHandle = methodHandles[HelperName];
            var extraHandle = methodHandles[ExtraName];
            var markerHandle = fieldHandles[MarkerName];
            var standaloneSignatureRowCount = metadata.GetTableRowCount(TableIndex.StandAloneSig);
            var rootBody = CreateBody(
                peReader,
                metadata,
                rootHandle,
                module,
                metadataRead,
                standaloneSignatureRowCount);
            var helperBody = CreateBody(
                peReader,
                metadata,
                helperHandle,
                module,
                metadataRead,
                standaloneSignatureRowCount);
            var extraBody = CreateBody(
                peReader,
                metadata,
                extraHandle,
                module,
                metadataRead,
                standaloneSignatureRowCount);

            const ulong rootAddress = 0x0000_0004_0000_0000UL;
            const ulong ownerAddress = 0x0000_0004_0000_1000UL;
            const ulong methodTable = 0x0000_0004_0000_2000UL;
            var rootRead = CreatePointerRead(snapshot.MemorySourceId, rootAddress, ownerAddress);
            var objectRead = CreatePointerRead(snapshot.MemorySourceId, ownerAddress, methodTable);
            var ownerEvidence = ImmutableArray.Create(rootRead, objectRead);
            var owner = new ClrmdHeapObjectInfo(
                snapshot,
                ownerAddress,
                ProbeName,
                MetadataTokens.GetToken(probeHandle),
                methodTable,
                rootAddress,
                "Strong",
                module,
                ownerEvidence);
            var ownerSearch = new ClrmdHeapObjectSearchResult(
                snapshot,
                ProbeName,
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                handlesScanned: 1,
                maximumHandlesScanned: 8,
                maximumMatches: 2,
                matchLimitReached: false,
                matches: ImmutableArray.Create(owner),
                evidence: ownerEvidence);
            var runtimeField = new ClrmdInstanceFieldInfo(
                snapshot,
                ownerAddress,
                methodTable,
                ProbeName,
                MarkerName,
                MetadataTokens.GetToken(markerHandle),
                checked(ownerAddress + 0x20UL),
                sizeof(int),
                isObjectReference: false,
                elementType: "Int32",
                fieldTypeName: "System.Int32",
                nullableInt32Layout: null);

            return new SyntheticFixture(
                module,
                metadataIdentity,
                rootBody,
                helperBody,
                extraBody,
                MetadataTokens.GetToken(rootHandle),
                MetadataTokens.GetToken(helperHandle),
                MetadataTokens.GetToken(extraHandle),
                MetadataTokens.GetToken(markerHandle),
                ownerSearch,
                runtimeField);
        }

        private static ClrmdEvidenceResult<ClrmdMethodBodyInfo> CreateBody(
            PEReader peReader,
            MetadataReader metadata,
            MethodDefinitionHandle methodHandle,
            ClrmdModuleInfo module,
            MemoryReadResult metadataRead,
            int standaloneSignatureRowCount)
        {
            var definition = metadata.GetMethodDefinition(methodHandle);
            var methodBlock = peReader.GetMethodBody(definition.RelativeVirtualAddress);
            var codeBytes = methodBlock.GetILBytes() ??
                throw new BadImageFormatException("The synthetic fixture method has no IL bytes.");
            if (codeBytes.Length >= 64)
            {
                throw new InvalidOperationException("The synthetic resolver fixture requires a tiny method body.");
            }

            var headerAddress = checked(module.Identity.ImageBase + (uint)definition.RelativeVirtualAddress);
            Span<byte> headerByte = stackalloc byte[1];
            headerByte[0] = checked((byte)((codeBytes.Length << 2) | 0x02));
            var headerRead = MemoryReadResult.Create(
                module.Identity.Snapshot.MemorySourceId,
                headerAddress,
                headerByte.Length,
                headerByte);
            var codeRead = MemoryReadResult.Create(
                module.Identity.Snapshot.MemorySourceId,
                checked(headerAddress + 1),
                codeBytes.Length,
                codeBytes);
            var memory = new SequenceMemoryReader(
                module.Identity.Snapshot.MemorySourceId,
                ImmutableArray.Create(headerRead, codeRead));
            var result = ClrmdMethodBodyParser.Read(
                memory,
                MetadataTokens.GetToken(methodHandle),
                definition.RelativeVirtualAddress,
                module.Identity.ImageBase,
                module.Identity.ImageSize,
                standaloneSignatureRowCount,
                ImmutableArray.Create(metadataRead));
            if (result.Status != ClrmdEvidenceStatus.Exact || result.Value is null || !memory.ConsumedAll)
            {
                throw new InvalidOperationException("Could not construct an exact counted synthetic method body.");
            }

            return result;
        }

        private static MemoryReadResult CreatePointerRead(string sourceId, ulong address, ulong value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            return MemoryReadResult.Create(sourceId, address, bytes.Length, bytes);
        }
    }

    private sealed class SequenceMemoryReader : IProcessMemoryReader
    {
        private readonly ImmutableArray<MemoryReadResult> reads;
        private int index;

        internal SequenceMemoryReader(string sourceId, ImmutableArray<MemoryReadResult> reads)
        {
            SourceId = sourceId;
            this.reads = reads;
            MaximumReadLength = reads.Max(static read => read.RequestedLength);
        }

        public int PointerSize => sizeof(ulong);

        public int MaximumReadLength { get; }

        public string SourceId { get; }

        internal bool ConsumedAll => index == reads.Length;

        public MemoryReadResult Read(ulong address, int length)
        {
            if ((uint)index >= (uint)reads.Length)
            {
                throw new InvalidDataException("The parser requested an uncounted synthetic range.");
            }

            var read = reads[index++];
            if (read.Address != address || read.RequestedLength != length)
            {
                throw new InvalidDataException("The parser requested a different synthetic range.");
            }

            return read;
        }
    }
}
