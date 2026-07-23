using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Generates one real full dump of the W8 truth-gate target and shares it across the runtime-acquisition gates.
/// </summary>
/// <remarks>
/// The fixture is draft physical scaffolding. Dump generation is hidden and headless exactly like the W8.1 oracles.
/// </remarks>
public sealed class W8V2RuntimeAcquisitionFixture : IDisposable
{
    /// <summary>Initializes the fixture by writing one full dump of the paused truth-gate target.</summary>
    public W8V2RuntimeAcquisitionFixture()
    {
        DumpPath = Path.Combine(Path.GetTempPath(), $"w8-v2-runtime-acquisition-{Guid.NewGuid():N}.dmp");
        var executable = W8V2RuntimeAcquisitionTests.RequireArtifact(W8TestTargetPaths.ResolveExecutable());
        using var target = TestTargetRunner.StartAndWaitReady(
            executable,
            ["--truth-gate", "generic-frame"],
            isolatedDirectory: null);
        DumpWriter.WriteFullDump(target.Pid, DumpPath);
    }

    /// <summary>Gets the complete path of the generated draft dump.</summary>
    public string DumpPath { get; }

    /// <summary>Deletes the generated draft dump.</summary>
    public void Dispose()
    {
        if (File.Exists(DumpPath))
        {
            File.Delete(DumpPath);
        }
    }
}

/// <summary>
/// Proves the W8.6c runtime acquisition adapter over one real full dump without any name, order, or global lookup.
/// </summary>
/// <remarks>
/// Every runtime construction is selected by definition module, TypeDef token, and ordered closed arguments projected
/// through the landed metadata authority. The metadata authority is composed from the real produced target module plus
/// one synthetic core module carrying the real <c>System.Runtime</c> assembly identity, because the pinned corelib is
/// rejected by the shared bounded ECMA signature grammar. These assertions are draft physical evidence.
/// </remarks>
public sealed class W8V2RuntimeAcquisitionTests : IClassFixture<W8V2RuntimeAcquisitionFixture>
{
    internal const string TargetNamespace = "Interpreter.W8TestTarget";
    internal const string NamedRvaNamespace = "Interpreter.W8NamedRvaTarget";

    private readonly W8V2RuntimeAcquisitionFixture fixture;

    /// <summary>Initializes the gate with the shared generated draft dump.</summary>
    /// <param name="fixture">The shared dump fixture.</param>
    public W8V2RuntimeAcquisitionTests(W8V2RuntimeAcquisitionFixture fixture) => this.fixture = fixture;

    /// <summary>
    /// Proves three closed constructions of one generic TypeDef select distinct method tables, distinct static slots,
    /// and distinct decoded values, that selection is order-independent, and that a never-constructed closed type
    /// produces the landed absent stop.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8V2RuntimeAcquisitionV1")]
    public void Constructed_slots_are_selected_by_ordered_arguments_and_decoded_from_copied_bytes()
    {
        using var world = W8V2RuntimeAcquisitionWorld.Open(fixture.DumpPath);
        var sentinelToken = world.TargetFieldToken("GenericSlot`1", "Sentinel");
        var genericSlotToken = world.TargetTypeToken("GenericSlot`1");

        var request = world.AcquireSentinel("RequestContext", sentinelToken, genericSlotToken);
        var batch = world.AcquireSentinel("BatchContext", sentinelToken, genericSlotToken);
        var value = world.AcquireSentinel("ValueContext", sentinelToken, genericSlotToken);

        foreach (var acquired in new[] { request, batch, value })
        {
            Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, acquired.Construction.ResultKind);
            Assert.Equal(StaticFieldV2RuntimeAcquisitionStage.None, acquired.Construction.Stage);
            Assert.Equal(StaticFieldV2RuntimeAcquisitionIssue.None, acquired.Construction.Issue);
            Assert.Equal(
                StaticFieldV2RuntimeConstructionSelectionKind.Exact,
                acquired.Construction.Selection!.ResultKind);
            Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, acquired.Slot!.ResultKind);
            Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, acquired.Slot.SlotOutcome!.ResultKind);
            Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, acquired.Value!.ResultKind);
            Assert.Equal(StaticFieldV2RuntimeValueResultKind.Exact, acquired.Value.ValueOutcome!.ResultKind);
            Assert.Equal(StaticFieldV2RuntimeValueKind.Int32, acquired.Value.ValueOutcome.ValueKind);
            Assert.Equal(sizeof(int), acquired.Value.RawBytes.Length);
            Assert.Equal(1, acquired.Probes.RuntimeConstructionCallCount);
            Assert.Equal(1, acquired.Probes.StaticSlotAcquisitionCallCount);
            Assert.Equal(1, acquired.Probes.MemoryReadCallCount);
            Assert.Equal(0, acquired.Probes.ThreadIdentityCallCount);
            Assert.Equal(0, acquired.Probes.ModuleContentCallCount);
            Assert.True(acquired.Construction.Candidates.Length >= 1);
            Assert.Equal(
                acquired.Construction.SameDefinitionEntryCount,
                acquired.Construction.Candidates.Length + acquired.Construction.UnprojectableCandidateCount);
            Assert.True(
                acquired.Construction.UnprojectableCandidateCount > 0,
                "A corelib-argument construction must be counted and excluded rather than fabricated.");
            Assert.Contains(
                StaticFieldV2RuntimeAcquisitionBoundary.UnprojectableConstructionArgumentsCountedAndExcluded,
                acquired.Construction.DeclaredBoundaries);
            Assert.Contains(
                StaticFieldV2RuntimeAcquisitionBoundary.CorelibPrimitiveCollapseNotModeled,
                acquired.Construction.DeclaredBoundaries);
        }

        var handles = new[] { request, batch, value }
            .Select(static acquired => acquired.Construction.Selection!.SelectedCandidate!.MethodTableAddress)
            .ToArray();
        Assert.Equal(3, handles.Distinct().Count());
        var addresses = new[] { request, batch, value }
            .Select(static acquired => acquired.Slot!.SlotAddress!.Value)
            .ToArray();
        Assert.Equal(3, addresses.Distinct().Count());
        Assert.Equal(0x11017A01L, request.Value!.ValueOutcome!.SignedValue);
        Assert.Equal(0x22027A01L, batch.Value!.ValueOutcome!.SignedValue);
        Assert.Equal(0x33037A01L, value.Value!.ValueOutcome!.SignedValue);

        var reversed = StaticFieldV2RuntimeConstructionBinder.SelectConstruction(
            StaticFieldV2RuntimeConstructionRequest.Create(
                request.Construction.Request.MetadataConstruction,
                request.Construction.Request.StorageStrategy,
                [.. request.Construction.Candidates.Reverse()]));
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, reversed.ResultKind);
        Assert.Equal(
            request.Construction.Selection!.SelectedCandidate!.Sha256,
            reversed.SelectedCandidate!.Sha256);

        var absent = world.AcquireSentinel("CoordinatorContext", sentinelToken, genericSlotToken);
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.NonExact, absent.Construction.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeAcquisitionStage.ConstructionSelection,
            absent.Construction.Stage);
        Assert.Equal(
            StaticFieldV2RuntimeAcquisitionIssue.ConstructionSelectionNonExact,
            absent.Construction.Issue);
        Assert.Equal(
            StaticFieldV2RuntimeConstructionSelectionKind.Absent,
            absent.Construction.Selection!.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeConstructionSelection.ConstructionAbsentCode,
            absent.Construction.DiagnosticCode);
        Assert.NotEmpty(absent.Construction.Candidates);
        Assert.True(absent.Construction.ExaminedEntryCount > absent.Construction.SameDefinitionEntryCount);
        Assert.Contains(
            absent.Construction.Candidates,
            candidate => candidate.Sha256 == request.Construction.Selection!.SelectedCandidate!.Sha256);

        var lateOracle = W8V2RuntimeAcquisitionWorld.ReadStaticInt32ThroughRuntime(
            fixture.DumpPath,
            request.Construction.Selection!.SelectedCandidate!.MethodTableAddress,
            sentinelToken);
        Assert.Equal(0x11017A01, lateOracle);
    }

    /// <summary>
    /// Proves a thread-relative slot selects one exact thread by its token-keyed frame predicate, acquires that
    /// thread's own slot address, and decodes the construction-specific value from the copied bytes.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8V2RuntimeAcquisitionV1")]
    public void Thread_relative_slot_selects_the_exact_thread_and_its_own_storage()
    {
        using var world = W8V2RuntimeAcquisitionWorld.Open(fixture.DumpPath);
        var probes = ExpressionV2CapabilityProbeSet.Create();
        var strategy = world.ClassifyTargetStrategy(
            world.TargetFieldToken("GenericSlot`1", "ThreadSentinel"),
            world.TargetTypeToken("GenericSlot`1"),
            threadStatic: true);
        Assert.Equal(StaticFieldV2StorageStrategy.ThreadRelativeSlot, strategy.Strategy);

        var construction = world.Session.AcquireConstruction(
            StaticFieldV2RuntimeConstructionAcquisitionRequest.Create(
                world.BindConstruction(
                    $"global::{TargetNamespace}.GenericSlot<global::{TargetNamespace}.RequestContext>.ThreadSentinel"),
                strategy,
                world.ChainPortfolio,
                world.Ancestry,
                world.Bindings,
                probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, construction.ResultKind);

        var slot = world.Session.AcquireStaticSlot(
            StaticFieldV2StaticSlotAcquisitionRequest.Create(
                strategy,
                sizeof(int),
                constructionSelection: construction.Selection,
                threadSelector: world.PausedFrameThreadSelector(),
                capabilityProbes: probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, slot.ResultKind);
        Assert.Equal(1, slot.CandidateThreadCount);
        var selectedThread = Assert.IsType<StaticFieldV2SelectedThreadIdentity>(slot.SelectedThread);
        Assert.NotEqual(0UL, selectedThread.ThreadObjectAddress);
        Assert.True(selectedThread.ManagedThreadId > 0);
        Assert.True(selectedThread.OperatingSystemThreadId > 0);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, slot.SlotOutcome!.ResultKind);
        Assert.Same(selectedThread, slot.SlotOutcome.Slot!.SelectedThread);

        var value = world.Session.AcquireValue(
            StaticFieldV2RuntimeValueAcquisitionRequest.Create(
                strategy,
                slot.SlotOutcome,
                MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int32),
                capabilityProbes: probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, value.ResultKind);
        Assert.Equal(0x11017A02L, value.ValueOutcome!.SignedValue);
        Assert.Equal(1, probes.RuntimeConstructionCallCount);
        Assert.Equal(1, probes.ThreadIdentityCallCount);
        Assert.Equal(1, probes.StaticSlotAcquisitionCallCount);
        Assert.Equal(1, probes.MemoryReadCallCount);
        Assert.Equal(0, probes.ModuleContentCallCount);
    }

    /// <summary>
    /// Proves a module-RVA field acquires module content, its exact FieldRVA row, and mapped geometry while performing
    /// zero runtime-construction and zero static-slot capability calls against poisoned probes.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8V2RuntimeAcquisitionV1")]
    public void Module_rva_field_performs_no_construction_or_slot_capability_call()
    {
        using var world = W8V2RuntimeAcquisitionWorld.Open(fixture.DumpPath);
        var probes = ExpressionV2CapabilityProbeSet.Create(
            runtimeConstruction: static () => throw new InvalidOperationException(
                "A module RVA must never acquire a runtime construction."),
            threadIdentity: static () => throw new InvalidOperationException(
                "A module RVA must never acquire a selected thread."),
            staticSlotAcquisition: static () => throw new InvalidOperationException(
                "A module RVA must never acquire a static storage slot."));
        var fieldToken = world.NamedRvaFieldToken("NamedRvaStorage", "NamedSentinel");
        var strategy = world.ClassifyNamedRvaStrategy(
            fieldToken,
            world.NamedRvaTypeToken("NamedRvaStorage"));
        Assert.Equal(StaticFieldV2StorageStrategy.ModuleRva, strategy.Strategy);

        var slot = world.Session.AcquireStaticSlot(
            StaticFieldV2StaticSlotAcquisitionRequest.Create(
                strategy,
                sizeof(int),
                declaringModuleBinding: world.NamedRvaBinding,
                capabilityProbes: probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, slot.ResultKind);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, slot.SlotOutcome!.ResultKind);
        Assert.Null(slot.SlotAddress);
        Assert.NotNull(slot.FieldRvaRowToken);
        Assert.Equal(0x1D, slot.FieldRvaRowToken!.Value >>> 24);
        Assert.NotEqual(0U, slot.MappedRelativeVirtualAddress!.Value);
        Assert.Equal(
            world.NamedRvaBinding.ImageBase + slot.MappedRelativeVirtualAddress!.Value,
            slot.MappedAddress!.Value);
        Assert.Equal(world.NamedRvaBinding.MetadataModule.ModuleContent, slot.SlotOutcome.Slot!.ModuleContent);

        var value = world.Session.AcquireValue(
            StaticFieldV2RuntimeValueAcquisitionRequest.Create(
                strategy,
                slot.SlotOutcome,
                MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int32),
                capabilityProbes: probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, value.ResultKind);
        Assert.Equal(0x21047A61L, value.ValueOutcome!.SignedValue);
        Assert.Equal(0, probes.RuntimeConstructionCallCount);
        Assert.Equal(0, probes.ThreadIdentityCallCount);
        Assert.Equal(0, probes.StaticSlotAcquisitionCallCount);
        Assert.Equal(1, probes.ModuleContentCallCount);
        Assert.Equal(1, probes.MemoryReadCallCount);
    }

    /// <summary>
    /// Proves a metadata literal performs zero calls of every runtime capability against a probe set whose every
    /// delegate throws, and that the frame-value root acquisition answers with its typed not-implemented stop.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8V2RuntimeAcquisitionV1")]
    public void Metadata_literal_and_frame_root_perform_no_runtime_capability_call()
    {
        using var world = W8V2RuntimeAcquisitionWorld.Open(fixture.DumpPath);
        var probes = ExpressionV2CapabilityProbeSet.Create(
            runtimeConstruction: static () => throw new InvalidOperationException("literal"),
            threadIdentity: static () => throw new InvalidOperationException("literal"),
            moduleContent: static () => throw new InvalidOperationException("literal"),
            staticSlotAcquisition: static () => throw new InvalidOperationException("literal"),
            memoryRead: static () => throw new InvalidOperationException("literal"));
        var strategy = world.ClassifyTargetStrategy(
            world.TargetFieldToken("GenericSlot`1", "Literal"),
            world.TargetTypeToken("GenericSlot`1"));
        Assert.Equal(StaticFieldV2StorageStrategy.MetadataLiteral, strategy.Strategy);

        var construction = world.Session.AcquireConstruction(
            StaticFieldV2RuntimeConstructionAcquisitionRequest.Create(
                world.BindConstruction(
                    $"global::{TargetNamespace}.GenericSlot<global::{TargetNamespace}.RequestContext>.Literal"),
                strategy,
                world.ChainPortfolio,
                world.Ancestry,
                world.Bindings,
                probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Unsupported, construction.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeAcquisitionStage.CapabilityVector, construction.Stage);
        Assert.Equal(
            StaticFieldV2RuntimeAcquisitionIssue.MetadataLiteralHasNoRuntimeStorage,
            construction.Issue);
        Assert.Empty(construction.Candidates);
        Assert.Equal(0, construction.ExaminedEntryCount);
        Assert.Equal(
            StaticFieldV2RuntimeConstructionSelectionKind.NotRequired,
            construction.Selection!.ResultKind);

        var slot = world.Session.AcquireStaticSlot(
            StaticFieldV2StaticSlotAcquisitionRequest.Create(strategy, sizeof(int), capabilityProbes: probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Unsupported, slot.ResultKind);
        Assert.Equal(
            StaticFieldV2StaticSlotAcquisitionOutcome.MetadataLiteralHasNoRuntimeStorageCode,
            slot.DiagnosticCode);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Unsupported, slot.SlotOutcome!.ResultKind);
        Assert.Equal(StaticFieldV2StaticSlotIssue.MetadataLiteralHasNoSlot, slot.SlotOutcome.Issue);
        Assert.Null(slot.SlotOutcome.Slot);

        var frame = world.Session.AcquireFrameValueRoot(
            StaticFieldV2FrameValueRootRequest.Create(
                world.PausedFrameThreadSelector(),
                frameOrdinal: 0,
                StaticFieldV2FrameValueRootKind.Local,
                rootOrdinal: 0,
                localName: "localRequest",
                capabilityProbes: probes));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Unsupported, frame.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeAcquisitionStage.FrameRoot, frame.Stage);
        Assert.Equal(
            StaticFieldV2RuntimeAcquisitionIssue.FrameAcquisitionNotImplemented,
            frame.Issue);
        Assert.Equal(
            StaticFieldV2FrameValueRootOutcome.FrameAcquisitionNotImplementedCode,
            frame.DiagnosticCode);
        Assert.Null(frame.RootAddress);
        Assert.Null(frame.RootWidth);
        Assert.Null(frame.IsLive);
        Assert.Empty(frame.RootBytes);
        Assert.Contains(
            StaticFieldV2RuntimeAcquisitionBoundary.FrameValueRootAcquisitionNotImplemented,
            frame.DeclaredBoundaries);
        Assert.Equal(0, probes.TotalCallCount);
        Assert.True(construction.CapabilityCallLedger.IsZero);
        Assert.True(slot.CapabilityCallLedger.IsZero);
        Assert.True(frame.CapabilityCallLedger.IsZero);
    }

    /// <summary>
    /// Proves closing the session, reopening the same dump, and rebinding the same expression produces byte-identical
    /// canonical construction, slot, and value draft outcomes.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8V2RuntimeAcquisitionV1")]
    public void Close_reopen_and_rebind_replays_byte_identically()
    {
        var first = AcquireReplaySubject();
        var second = AcquireReplaySubject();

        Assert.Equal(first.SnapshotSha256, second.SnapshotSha256);
        Assert.Equal(first.ConstructionSha256, second.ConstructionSha256);
        Assert.Equal(first.SlotSha256, second.SlotSha256);
        Assert.Equal(first.ValueSha256, second.ValueSha256);
        Assert.Equal(first.RawBytesSha256, second.RawBytesSha256);
        Assert.True(first.ConstructionBytes.AsSpan().SequenceEqual(second.ConstructionBytes.AsSpan()));
        Assert.True(first.SlotBytes.AsSpan().SequenceEqual(second.SlotBytes.AsSpan()));
        Assert.True(first.ValueBytes.AsSpan().SequenceEqual(second.ValueBytes.AsSpan()));
    }

    private ReplaySubject AcquireReplaySubject()
    {
        using var world = W8V2RuntimeAcquisitionWorld.Open(fixture.DumpPath);
        var acquired = world.AcquireSentinel(
            "RequestContext",
            world.TargetFieldToken("GenericSlot`1", "Sentinel"),
            world.TargetTypeToken("GenericSlot`1"));
        Assert.Equal(StaticFieldV2RuntimeAcquisitionResultKind.Exact, acquired.Value!.ResultKind);
        return new ReplaySubject(
            world.Session.Evidence.SnapshotSha256,
            acquired.Construction.Sha256,
            acquired.Slot!.Sha256,
            acquired.Value.Sha256,
            acquired.Value.RawBytesSha256,
            acquired.Construction.CanonicalBytes,
            acquired.Slot.CanonicalBytes,
            acquired.Value.CanonicalBytes);
    }

    private sealed record ReplaySubject(
        string SnapshotSha256,
        string ConstructionSha256,
        string SlotSha256,
        string ValueSha256,
        string RawBytesSha256,
        ImmutableArray<byte> ConstructionBytes,
        ImmutableArray<byte> SlotBytes,
        ImmutableArray<byte> ValueBytes);

    internal static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"The required W8 fixture artifact is missing: {path}");
        return path;
    }
}

/// <summary>Carries the three draft answers and the probe ledger of one acquired static field.</summary>
/// <remarks>This is draft physical scaffolding and not a product contract.</remarks>
internal sealed record AcquiredStaticField(
    StaticFieldV2RuntimeConstructionAcquisitionOutcome Construction,
    StaticFieldV2StaticSlotAcquisitionOutcome? Slot,
    StaticFieldV2RuntimeValueAcquisitionOutcome? Value,
    ExpressionV2CapabilityProbeSet Probes);

/// <summary>Composes the pinned dump session and the landed metadata authority the acquisition gates share.</summary>
/// <remarks>
/// The real target and named-RVA modules are produced from the metadata images copied out of the dump itself. The core
/// module is synthetic and carries the real <c>System.Runtime</c> assembly identity read from the shared framework, so
/// the target's real AssemblyRef resolves without fabricating any runtime fact. This is draft physical scaffolding.
/// </remarks>
internal sealed class W8V2RuntimeAcquisitionWorld : IDisposable
{
    private const int PublicClassAttributes = 0x0000_0001;
    private const ulong SyntheticCoreModuleAddress = 0x0000_7E00_0000_0001UL;

    private readonly ProducedRuntimeModule target;
    private readonly ProducedRuntimeModule namedRva;

    private W8V2RuntimeAcquisitionWorld(
        StaticFieldV2RuntimeAcquisitionSession session,
        ProducedRuntimeModule target,
        ProducedRuntimeModule namedRva,
        ImmutableArray<StaticFieldV2RuntimeModuleBinding> bindings,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        MetadataConstraintTargetResolutionPortfolioIdentity constraints)
    {
        Session = session;
        this.target = target;
        this.namedRva = namedRva;
        Bindings = bindings;
        ChainPortfolio = chainPortfolio;
        Ancestry = ancestry;
        Constraints = constraints;
    }

    internal StaticFieldV2RuntimeAcquisitionSession Session { get; }

    internal ImmutableArray<StaticFieldV2RuntimeModuleBinding> Bindings { get; }

    internal MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio { get; }

    internal MetadataAncestryAuthorityPortfolioIdentity Ancestry { get; }

    internal MetadataConstraintTargetResolutionPortfolioIdentity Constraints { get; }

    internal StaticFieldMetadataModuleIdentity TargetModule => target.MetadataModule;

    internal StaticFieldV2RuntimeModuleBinding TargetBinding => target.Binding;

    internal StaticFieldV2RuntimeModuleBinding NamedRvaBinding => namedRva.Binding;

    internal static W8V2RuntimeAcquisitionWorld Open(string dumpPath)
    {
        var session = StaticFieldV2RuntimeAcquisitionSession.Open(dumpPath);
        try
        {
            var target = ProducedRuntimeModule.Bind(
                session,
                W8V2RuntimeAcquisitionTests.RequireArtifact(W8TestTargetPaths.ResolveAssembly()));
            try
            {
                var namedRva = ProducedRuntimeModule.Bind(
                    session,
                    W8V2RuntimeAcquisitionTests.RequireArtifact(W8TestTargetPaths.ResolveNamedRvaAssembly()));
                try
                {
                    var core = BuildSyntheticCoreModule(session, target);
                    var compatibility = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                        [core.Compatibility, target.Outcome.Compatibility!]);
                    Assert.Equal(
                        MetadataDefinitionCompatibilityPortfolioResultKind.Exact,
                        compatibility.ResultKind);
                    var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                        compatibility,
                        [core.ChainCatalog, target.Outcome.ChainCatalog!]);
                    Assert.Equal(
                        MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact,
                        chainPortfolio.ResultKind);
                    var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                        chainPortfolio,
                        [core.Tables, target.Outcome.ReferenceTables!]);
                    Assert.Equal(
                        MetadataTypeReferenceResolutionPortfolioResultKind.Exact,
                        resolution.ResultKind);
                    var ancestry = MetadataAncestryAuthorityPortfolioIdentity.Create(resolution);
                    Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestry.ResultKind);
                    var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
                        resolution,
                        [core.Constraints, target.Outcome.GenericParameterConstraints!]);
                    Assert.Equal(
                        MetadataConstraintTargetResolutionPortfolioResultKind.Exact,
                        constraints.ResultKind);
                    return new W8V2RuntimeAcquisitionWorld(
                        session,
                        target,
                        namedRva,
                        [target.Binding, namedRva.Binding],
                        chainPortfolio,
                        ancestry,
                        constraints);
                }
                catch
                {
                    namedRva.Dispose();
                    throw;
                }
            }
            catch
            {
                target.Dispose();
                throw;
            }
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    internal static int ReadStaticInt32ThroughRuntime(string dumpPath, ulong methodTable, int fieldToken)
    {
        using var dataTarget = DataTarget.LoadDump(
            dumpPath,
            new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
        using var runtime = Assert.Single(dataTarget.ClrVersions).CreateRuntime();
        var type = Assert.IsAssignableFrom<ClrType>(runtime.GetTypeByMethodTable(methodTable));
        var field = Assert.Single(type.StaticFields, candidate => candidate.Token == fieldToken);
        return field.Read<int>(type.Module.AppDomain);
    }

    internal int TargetTypeToken(string typeName) =>
        FindTypeToken(target.Reader, W8V2RuntimeAcquisitionTests.TargetNamespace, typeName);

    internal int TargetFieldToken(string typeName, string fieldName) =>
        FindFieldToken(target.Reader, W8V2RuntimeAcquisitionTests.TargetNamespace, typeName, fieldName);

    internal int NamedRvaTypeToken(string typeName) =>
        FindTypeToken(namedRva.Reader, W8V2RuntimeAcquisitionTests.NamedRvaNamespace, typeName);

    internal int NamedRvaFieldToken(string typeName, string fieldName) =>
        FindFieldToken(namedRva.Reader, W8V2RuntimeAcquisitionTests.NamedRvaNamespace, typeName, fieldName);

    internal StaticFieldV2RuntimeThreadSelector PausedFrameThreadSelector() =>
        StaticFieldV2RuntimeThreadSelector.Create(
            TargetBinding.RuntimeModuleAddress,
            TargetTypeToken("GenericFrameOwner`1"),
            TargetMethodToken("GenericFrameOwner`1", "Run"));

    internal int TargetMethodToken(string typeName, string methodName) =>
        FindMethodToken(target.Reader, W8V2RuntimeAcquisitionTests.TargetNamespace, typeName, methodName);

    internal StaticFieldV2StorageStrategyOutcome ClassifyTargetStrategy(
        int fieldToken,
        int declaringTypeToken,
        bool threadStatic = false) =>
        Classify(target, fieldToken, declaringTypeToken, threadStatic);

    internal StaticFieldV2StorageStrategyOutcome ClassifyNamedRvaStrategy(int fieldToken, int declaringTypeToken) =>
        Classify(namedRva, fieldToken, declaringTypeToken, threadStatic: false);

    internal StaticFieldV2ClosedConstructionOutcome BindConstruction(string expressionText)
    {
        var parsed = StaticFieldV2ExpressionParser.Parse(expressionText);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, parsed.Status);
        var nameBinding = StaticFieldV2TypeNameBinder.BindExplicitRoute(parsed.Descriptor!, ChainPortfolio);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, nameBinding.ResultKind);
        var construction = StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
            nameBinding,
            Ancestry,
            Constraints);
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, construction.ResultKind);
        return construction;
    }

    internal AcquiredStaticField AcquireSentinel(string argumentTypeName, int fieldToken, int declaringTypeToken)
    {
        var probes = ExpressionV2CapabilityProbeSet.Create();
        var strategy = ClassifyTargetStrategy(fieldToken, declaringTypeToken);
        Assert.Equal(StaticFieldV2StorageStrategy.ConstructedSlot, strategy.Strategy);
        var construction = BindConstruction(
            $"global::{W8V2RuntimeAcquisitionTests.TargetNamespace}.GenericSlot<" +
            $"global::{W8V2RuntimeAcquisitionTests.TargetNamespace}.{argumentTypeName}>.Sentinel");
        var acquiredConstruction = Session.AcquireConstruction(
            StaticFieldV2RuntimeConstructionAcquisitionRequest.Create(
                construction,
                strategy,
                ChainPortfolio,
                Ancestry,
                Bindings,
                probes));
        if (acquiredConstruction.ResultKind != StaticFieldV2RuntimeAcquisitionResultKind.Exact)
        {
            return new AcquiredStaticField(acquiredConstruction, null, null, probes);
        }

        var slot = Session.AcquireStaticSlot(
            StaticFieldV2StaticSlotAcquisitionRequest.Create(
                strategy,
                sizeof(int),
                constructionSelection: acquiredConstruction.Selection,
                capabilityProbes: probes));
        if (slot.ResultKind != StaticFieldV2RuntimeAcquisitionResultKind.Exact)
        {
            return new AcquiredStaticField(acquiredConstruction, slot, null, probes);
        }

        var value = Session.AcquireValue(
            StaticFieldV2RuntimeValueAcquisitionRequest.Create(
                strategy,
                slot.SlotOutcome!,
                MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int32),
                capabilityProbes: probes));
        return new AcquiredStaticField(acquiredConstruction, slot, value, probes);
    }

    public void Dispose()
    {
        namedRva.Dispose();
        target.Dispose();
        Session.Dispose();
    }

    private static StaticFieldV2StorageStrategyOutcome Classify(
        ProducedRuntimeModule module,
        int fieldToken,
        int declaringTypeToken,
        bool threadStatic)
    {
        var fieldRow = Assert.Single(
            module.Outcome.FieldDefinitions!.Rows,
            row => row.FieldDefinitionToken == fieldToken);
        var declaringType = Assert.IsType<MetadataTypeDefinitionAuthorityIdentity>(
            module.Outcome.DefinitionAuthority!.ExactTypeDefinitionOrDefault(declaringTypeToken));
        return StaticFieldV2StorageStrategyBinder.ClassifyStrategy(
            StaticFieldV2StorageStrategyRequest.Create(fieldRow, declaringType, threadStatic));
    }

    private static SyntheticCoreModule BuildSyntheticCoreModule(
        StaticFieldV2RuntimeAcquisitionSession session,
        ProducedRuntimeModule target)
    {
        var assemblyDefinition = ReadSharedFrameworkAssemblyDefinition("System.Runtime.dll");
        var moduleInstance = StaticFieldModuleInstanceIdentity.Create(
            session.Evidence.SnapshotSha256,
            session.Evidence.PointerWidth,
            target.Binding.MetadataModule.Module.ApplicationDomainAddress,
            SyntheticCoreModuleAddress,
            imageBase: 0,
            imageSize: 0);
        var content = ModuleContentIdentity.FromDigest(
            mvid: Guid.Parse("7e007e00-7e00-7e00-7e00-7e007e007e00"),
            metadataLength: 4_096,
            metadataSha256: new string('7', 64));
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            generation: 0,
            name: "w8-v2-synthetic-core.dll",
            mvid: content.Mvid,
            encId: Guid.Empty,
            encBaseId: Guid.Empty);
        var module = StaticFieldMetadataModuleIdentity.ForManifestModule(
            moduleInstance,
            content,
            moduleDefinition,
            StaticFieldContainingAssemblyIdentity.Create(
                moduleInstance,
                content,
                moduleDefinition,
                assemblyDefinition));

        var namedTypes = new (string NamespaceName, string TypeName, int? Extends)[]
        {
            ("System", "Object", null),
            ("System", "ValueType", 0x0200_0002),
            ("System", "Enum", 0x0200_0003),
            ("System", "Delegate", 0x0200_0002),
            ("System", "MulticastDelegate", 0x0200_0005),
        };
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: moduleInstance,
                moduleContent: content,
                typeDefinitionsExamined: namedTypes.Length + 1,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: namedTypes.Length + 1,
                fieldDefinitionRowCount: 0));
        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(
            namedTypes.Length + 1);
        typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x0200_0001,
            fieldListRowId: 1,
            methodListRowId: 1,
            namespaceName: string.Empty,
            typeName: "<Module>",
            typeAttributes: 0,
            extendsMetadataToken: null));
        for (var index = 0; index < namedTypes.Length; index++)
        {
            typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                0x0200_0002 + index,
                fieldListRowId: 1,
                methodListRowId: 1,
                namespaceName: namedTypes[index].NamespaceName,
                typeName: namedTypes[index].TypeName,
                typeAttributes: PublicClassAttributes,
                extendsMetadataToken: namedTypes[index].Extends));
        }

        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            typeRows.MoveToImmutable(),
            pointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var referenceEnds = MetadataReferenceSourceEndIdentity.Create(sourceEnds);
        var tables = MetadataModuleReferenceTableSetIdentity.Create(
            referenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataTypeReferenceRowObservationIdentity>.Empty),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>.Empty),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataAssemblyFileRowObservationIdentity>.Empty),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataExportedTypeRowObservationIdentity>.Empty));
        Assert.True(tables.AllTablesExact);

        var candidateSlots = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity?>(
            authority.TypeDefinitions.Length);
        for (var index = 0; index < authority.TypeDefinitions.Length; index++)
        {
            candidateSlots.Add(null);
        }

        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            candidateSlots.MoveToImmutable());
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            compatibility,
            MetadataCompilerNameMappingCatalogIdentity.Create(authority));
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);
        var constraints = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(authority),
            ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity>.Empty);
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            constraints.ResultKind);
        return new SyntheticCoreModule(module, compatibility, chainCatalog, tables, constraints);
    }

    private static StaticFieldAssemblyDefinitionIdentity ReadSharedFrameworkAssemblyDefinition(string fileName)
    {
        var path = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, fileName);
        Assert.True(File.Exists(path), $"The shared framework artifact is missing: {path}");
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var definition = reader.GetAssemblyDefinition();
        return StaticFieldAssemblyDefinitionIdentity.Create(
            reader.GetString(definition.Name),
            definition.Version.Major,
            definition.Version.Minor,
            definition.Version.Build,
            definition.Version.Revision,
            reader.GetString(definition.Culture),
            (int)definition.Flags,
            (int)definition.HashAlgorithm,
            definition.PublicKey.IsNil
                ? ImmutableArray<byte>.Empty
                : ImmutableArray.Create(reader.GetBlobBytes(definition.PublicKey)));
    }

    private static int FindTypeToken(MetadataReader reader, string namespaceName, string typeName)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            if (string.Equals(reader.GetString(definition.Namespace), namespaceName, StringComparison.Ordinal) &&
                string.Equals(reader.GetString(definition.Name), typeName, StringComparison.Ordinal))
            {
                return MetadataTokens.GetToken(handle);
            }
        }

        throw new InvalidOperationException($"The fixture module declares no type {namespaceName}.{typeName}.");
    }

    private static int FindMethodToken(
        MetadataReader reader,
        string namespaceName,
        string typeName,
        string methodName)
    {
        var typeToken = FindTypeToken(reader, namespaceName, typeName);
        var definition = reader.GetTypeDefinition((TypeDefinitionHandle)MetadataTokens.Handle(typeToken));
        foreach (var handle in definition.GetMethods())
        {
            if (string.Equals(
                    reader.GetString(reader.GetMethodDefinition(handle).Name),
                    methodName,
                    StringComparison.Ordinal))
            {
                return MetadataTokens.GetToken(handle);
            }
        }

        throw new InvalidOperationException(
            $"The fixture type {namespaceName}.{typeName} declares no method {methodName}.");
    }

    private static int FindFieldToken(
        MetadataReader reader,
        string namespaceName,
        string typeName,
        string fieldName)
    {
        var typeToken = FindTypeToken(reader, namespaceName, typeName);
        var definition = reader.GetTypeDefinition((TypeDefinitionHandle)MetadataTokens.Handle(typeToken));
        foreach (var handle in definition.GetFields())
        {
            if (string.Equals(
                    reader.GetString(reader.GetFieldDefinition(handle).Name),
                    fieldName,
                    StringComparison.Ordinal))
            {
                return MetadataTokens.GetToken(handle);
            }
        }

        throw new InvalidOperationException(
            $"The fixture type {namespaceName}.{typeName} declares no field {fieldName}.");
    }

    private sealed record SyntheticCoreModule(
        StaticFieldMetadataModuleIdentity Module,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog,
        MetadataModuleReferenceTableSetIdentity Tables,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Constraints);

    private sealed class ProducedRuntimeModule : IDisposable
    {
        private readonly MetadataReaderProvider provider;

        private ProducedRuntimeModule(
            MetadataReaderProvider provider,
            MetadataReader reader,
            StaticFieldMetadataModuleIdentity metadataModule,
            MetadataModuleAcquisitionOutcome outcome,
            StaticFieldV2RuntimeModuleBinding binding)
        {
            this.provider = provider;
            Reader = reader;
            MetadataModule = metadataModule;
            Outcome = outcome;
            Binding = binding;
        }

        internal MetadataReader Reader { get; }

        internal StaticFieldMetadataModuleIdentity MetadataModule { get; }

        internal MetadataModuleAcquisitionOutcome Outcome { get; }

        internal StaticFieldV2RuntimeModuleBinding Binding { get; }

        internal static ProducedRuntimeModule Bind(
            StaticFieldV2RuntimeAcquisitionSession session,
            string artifactPath)
        {
            var artifact = ReadArtifactContent(artifactPath);
            var observation = Assert.Single(
                session.Modules.Where(candidate =>
                    candidate.MetadataLength == (ulong)artifact.Bytes.Length &&
                    ModuleContentIdentity
                        .FromMetadata(artifact.Content.Mvid, session.ReadModuleMetadata(candidate.ModuleAddress).AsSpan())
                        .Equals(artifact.Content)));
            var metadataBytes = session.ReadModuleMetadata(observation.ModuleAddress);
            var provider = MetadataReaderProvider.FromMetadataImage(metadataBytes);
            try
            {
                var reader = provider.GetMetadataReader();
                var metadataModule = MetadataAuthorityProducer.AcquireManifestModuleIdentity(
                    session.CreateModuleInstance(observation.ModuleAddress),
                    reader,
                    metadataBytes);
                var outcome = MetadataAuthorityProducer.AcquireModule(
                    MetadataModuleAcquisitionRequest.Create(metadataModule, () => provider.GetMetadataReader()));
                Assert.Equal(MetadataModuleAcquisitionResultKind.Exact, outcome.ResultKind);
                var binding = StaticFieldV2RuntimeModuleBinding.Create(
                    observation.ModuleAddress,
                    observation.ImageBase,
                    observation.ImageSize,
                    metadataModule);
                return new ProducedRuntimeModule(provider, reader, metadataModule, outcome, binding);
            }
            catch
            {
                provider.Dispose();
                throw;
            }
        }

        public void Dispose() => provider.Dispose();

        private static ArtifactContent ReadArtifactContent(string artifactPath)
        {
            using var stream = File.OpenRead(artifactPath);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var bytes = peReader.GetMetadata().GetContent();
            var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
            return new ArtifactContent(bytes, ModuleContentIdentity.FromMetadata(mvid, bytes.AsSpan()));
        }

        private sealed record ArtifactContent(ImmutableArray<byte> Bytes, ModuleContentIdentity Content);
    }
}
