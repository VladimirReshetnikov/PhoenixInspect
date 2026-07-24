using System.Collections.Immutable;
using System.Runtime.InteropServices;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the dump-free W7.2 selected-frame, Portable-PDB, additive-context, and consulted-fact contracts.
/// </summary>
/// <remarks>
/// These synthetic vectors are deliberately relational: snapshot, runtime module, counted metadata, CodeView/PDB
/// content id, MethodDef/MethodDebugInformation RID, containing local scope, active import chain, and consulted subset
/// must agree. The suite uses no dump, process, UI, local artifact path, or live reader.
/// </remarks>
public sealed class W7ContextContractTests
{
    private static readonly ImmutableArray<EvaluationDeterministicBound> FrameBounds =
    [
        new("context.frames.count", 64),
        new("context.threads.count", 32),
    ];

    private static readonly ImmutableArray<EvaluationDeterministicBound> PdbBounds =
    [
        new("context.imports.count", 128),
        new("context.pdb.bytes", 1_048_576),
        new("context.scopes.count", 64),
    ];

    /// <summary>
    /// Proves selected-frame values have canonical content equality and that every exact identity axis participates.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Selected_frame_identity_has_content_equality_and_complete_identity_axes()
    {
        var fixture = ContextFixture.Create();
        var replay = ContextFixture.Create().Frame;

        Assert.Equal(fixture.Frame, replay);
        Assert.Equal(fixture.Frame.GetHashCode(), replay.GetHashCode());
        Assert.Equal(fixture.Frame.Sha256, replay.Sha256);
        Assert.True(fixture.Frame.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));

        var movedInstruction = DumpSelectedFrameIdentity.Create(
            fixture.Selector,
            fixture.Frame.ManagedThreadId,
            fixture.Frame.RuntimeThreadAddress,
            fixture.Frame.StackPointer,
            fixture.RuntimeModule,
            fixture.ModuleContent,
            fixture.Frame.MethodDefinitionToken,
            fixture.Frame.DeclaringTypeDefinitionToken,
            fixture.Frame.DeclaringNamespace,
            DumpInstructionLocation.Create(fixture.Frame.Instruction.NativeInstructionPointer, 11));
        Assert.NotEqual(fixture.Frame, movedInstruction);

        var differentMethod = DumpSelectedFrameIdentity.Create(
            fixture.Selector,
            fixture.Frame.ManagedThreadId,
            fixture.Frame.RuntimeThreadAddress,
            fixture.Frame.StackPointer,
            fixture.RuntimeModule,
            fixture.ModuleContent,
            0x06000004,
            fixture.Frame.DeclaringTypeDefinitionToken,
            fixture.Frame.DeclaringNamespace,
            fixture.Frame.Instruction);
        Assert.NotEqual(fixture.Frame, differentMethod);

        var exported = fixture.Frame.CanonicalBytes;
        var exportedArray = ImmutableCollectionsMarshal.AsArray(exported)!;
        exportedArray[0] ^= 0xFF;
        Assert.True(fixture.Frame.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
    }

    /// <summary>
    /// Proves selectors, snapshots, metadata-token tables, and exact frame/module correlation reject malformed input.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Selected_frame_rejects_defaults_foreign_snapshots_and_wrong_token_tables()
    {
        var fixture = ContextFixture.Create();
        Assert.Throws<ArgumentException>(() => DumpSelectedFrameSelector.Create(default, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DumpSelectedFrameSelector.Create(fixture.Snapshot, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DumpSelectedFrameSelector.Create(fixture.Snapshot, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpInstructionLocation.Create(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpInstructionLocation.Create(1, -1));

        var foreignSnapshot = Snapshot('f');
        var foreignRuntimeModule = new ClrmdRuntimeModuleIdentity(
            foreignSnapshot,
            fixture.RuntimeModule.AppDomainAddress,
            fixture.RuntimeModule.ModuleAddress,
            fixture.RuntimeModule.ImageBase,
            fixture.RuntimeModule.ImageSize);
        Assert.Throws<ArgumentException>(() => DumpSelectedFrameIdentity.Create(
            fixture.Selector,
            1,
            0x7000,
            0x8000,
            foreignRuntimeModule,
            fixture.ModuleContent,
            0x06000003,
            0x02000002,
            "Synthetic.Incident",
            fixture.Frame.Instruction));

        Assert.Throws<ArgumentOutOfRangeException>(() => DumpSelectedFrameIdentity.Create(
            fixture.Selector,
            1,
            0x7000,
            0x8000,
            fixture.RuntimeModule,
            fixture.ModuleContent,
            0x02000003,
            0x02000002,
            "Synthetic.Incident",
            fixture.Frame.Instruction));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpSelectedFrameIdentity.Create(
            fixture.Selector,
            1,
            0x7000,
            0x8000,
            fixture.RuntimeModule,
            fixture.ModuleContent,
            0x06000003,
            0x06000002,
            "Synthetic.Incident",
            fixture.Frame.Instruction));
    }

    /// <summary>
    /// Proves every frame disposition carries source and reached bounds, while only Exact can expose a frame payload.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Selected_frame_observations_enforce_status_specific_no_payload_invariants()
    {
        var fixture = ContextFixture.Create();
        var exact = DumpSelectedFrameObservation.Exact(fixture.Frame, FrameBounds.Reverse().ToImmutableArray());
        Assert.Equal(DumpContextEvidenceStatus.Exact, exact.Status);
        Assert.Equal(DumpContextEvidenceIssue.None, exact.Issue);
        Assert.Same(fixture.Frame, exact.Frame);
        Assert.Equal(new[] { "context.frames.count", "context.threads.count" }, exact.ReachedBounds.Select(static b => b.Name));

        var observations = new[]
        {
            DumpSelectedFrameObservation.Partial(
                fixture.Selector,
                DumpContextEvidenceIssue.BoundReached,
                FrameBounds),
            DumpSelectedFrameObservation.Unavailable(
                fixture.Selector,
                DumpContextEvidenceIssue.FrameUnavailable,
                FrameBounds),
            DumpSelectedFrameObservation.Ambiguous(
                fixture.Selector,
                DumpContextEvidenceIssue.FrameAmbiguous,
                FrameBounds),
            DumpSelectedFrameObservation.Conflict(
                fixture.Selector,
                DumpContextEvidenceIssue.ModuleMismatch,
                FrameBounds),
            DumpSelectedFrameObservation.Invalid(
                fixture.Selector,
                DumpContextEvidenceIssue.InvalidFrame,
                FrameBounds),
            DumpSelectedFrameObservation.Unsupported(
                fixture.Selector,
                DumpContextEvidenceIssue.UnsupportedFrame,
                FrameBounds),
        };
        Assert.All(observations, observation =>
        {
            Assert.False(observation.HasExactFrame);
            Assert.Null(observation.Frame);
            Assert.Equal(fixture.Selector, observation.Selector);
            Assert.NotEmpty(observation.ReachedBounds);
        });

        Assert.Throws<ArgumentException>(() => DumpSelectedFrameObservation.Exact(
            fixture.Frame,
            default));
        Assert.Throws<ArgumentException>(() => DumpSelectedFrameObservation.Partial(
            fixture.Selector,
            DumpContextEvidenceIssue.BoundReached,
            default));
        Assert.Throws<ArgumentException>(() => DumpSelectedFrameObservation.Unavailable(
            fixture.Selector,
            DumpContextEvidenceIssue.PortablePdbUnavailable,
            FrameBounds));
        Assert.Throws<ArgumentException>(() => DumpSelectedFrameObservation.Conflict(
            fixture.Selector,
            DumpContextEvidenceIssue.PortablePdbIdentityMismatch,
            FrameBounds));
        Assert.Throws<ArgumentException>(() => DumpSelectedFrameObservation.Invalid(
            fixture.Selector,
            DumpContextEvidenceIssue.InvalidPortablePdb,
            FrameBounds));
        Assert.Throws<ArgumentException>(() => DumpSelectedFrameObservation.Partial(
            fixture.Selector,
            DumpContextEvidenceIssue.BoundReached,
            [new("context.frames.count", 64), new("context.frames.count", 128)]));
    }

    /// <summary>
    /// Proves CodeView/PDB identity is GUID plus stamp with portable age one and complete artifact content identity.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Portable_pdb_debug_and_content_identities_are_path_independent_and_complete()
    {
        var fixture = ContextFixture.Create();
        var replayDebug = DumpPortablePdbDebugIdentity.Create(fixture.DebugIdentity.Guid, fixture.DebugIdentity.Stamp);
        var changedStamp = DumpPortablePdbDebugIdentity.Create(fixture.DebugIdentity.Guid, fixture.DebugIdentity.Stamp + 1);
        Assert.Equal(fixture.DebugIdentity, replayDebug);
        Assert.NotEqual(fixture.DebugIdentity, changedStamp);
        Assert.Equal(1, fixture.DebugIdentity.Age);
        var zeroGuid = DumpPortablePdbDebugIdentity.Create(Guid.Empty, 1);
        Assert.Equal(Guid.Empty, zeroGuid.Guid);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DumpPortablePdbDebugIdentity.Create(fixture.DebugIdentity.Guid, 1, age: 2));

        var uppercaseContent = DumpPortablePdbContentIdentity.Create(
            fixture.PdbContent.ByteLength,
            fixture.PdbContent.Sha256.ToUpperInvariant());
        Assert.Equal(fixture.PdbContent, uppercaseContent);
        Assert.Equal(fixture.PdbContent.GetHashCode(), uppercaseContent.GetHashCode());
        Assert.DoesNotContain(
            typeof(DumpPortablePdbArtifactIdentity).GetProperties(),
            static property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("FileName", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Proves exact import facts retain all required kinds, raw payloads, Unicode spelling, deterministic order, and
    /// physical duplicate rules without reimplementing C# identifier admission.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Import_facts_preserve_raw_and_decoded_truth_with_deterministic_duplicate_rejection()
    {
        const int scopeToken = 0x35000001;
        var mutableInput = ImmutableArray.Create<byte>(0x01, 0x02, 0x03);
        var namespaceImport = DumpPortablePdbImportFact.NamespaceImport(
            scopeToken,
            0,
            rawKind: 1,
            "Synthetic.Services",
            mutableInput);
        var combiningAlias = DumpPortablePdbImportFact.TypeAlias(
            scopeToken,
            1,
            rawKind: 9,
            "A\u0301",
            "Synthetic.Services.RequestEnvelope",
            0x01000001,
            ImmutableArray<byte>.Empty);
        var supplementaryAlias = DumpPortablePdbImportFact.NamespaceAlias(
            scopeToken,
            2,
            rawKind: 7,
            "\U00010400Alias",
            "Synthetic.Services",
            ImmutableArray<byte>.Empty);
        var usingStatic = DumpPortablePdbImportFact.UsingStatic(
            scopeToken,
            3,
            rawKind: 3,
            "Synthetic.Helpers.ContextValues",
            0x01000002,
            ImmutableArray<byte>.Empty);
        var externAlias = DumpPortablePdbImportFact.ExternAlias(
            scopeToken,
            4,
            rawKind: 6,
            "vendor",
            ImmutableArray<byte>.Empty,
            0x23000001);
        var unsupported = DumpPortablePdbImportFact.UnsupportedRaw(
            scopeToken,
            5,
            rawKind: 0xFE,
            [0xFE, 0xAA, 0x55]);
        var assemblyNamespace = DumpPortablePdbImportFact.NamespaceImport(
            scopeToken,
            6,
            rawKind: 2,
            "Vendor.Services",
            ImmutableArray<byte>.Empty,
            assemblyReferenceToken: 0x23000002);
        var assemblyNamespaceAlias = DumpPortablePdbImportFact.NamespaceAlias(
            scopeToken,
            7,
            rawKind: 8,
            "VendorServices",
            "Vendor.Services",
            ImmutableArray<byte>.Empty,
            assemblyReferenceToken: 0x23000002);
        var importedAssemblyAlias = DumpPortablePdbImportFact.ExternAlias(
            scopeToken,
            8,
            rawKind: 5,
            "globalVendor",
            ImmutableArray<byte>.Empty);

        var scope = DumpPortablePdbImportScopeIdentity.Create(
            scopeToken,
            parentImportScopeToken: null,
            nestingDepth: 0,
            [
                importedAssemblyAlias,
                unsupported,
                namespaceImport,
                supplementaryAlias,
                combiningAlias,
                assemblyNamespaceAlias,
                externAlias,
                usingStatic,
                assemblyNamespace,
            ]);
        Assert.Equal(9, scope.Imports.Length);
        Assert.Equal(Enumerable.Range(0, 9), scope.Imports.Select(static import => import.Ordinal));
        Assert.Equal("A\u0301", scope.Imports[1].Alias);
        Assert.Equal("\U00010400Alias", scope.Imports[2].Alias);
        var retainedUnsupported = Assert.Single(scope.Imports, static import =>
            import.Kind == DumpPortablePdbImportKind.UnsupportedRaw);
        Assert.Equal(0xFE, retainedUnsupported.RawKind);
        Assert.Equal(0x23000002, assemblyNamespace.AssemblyReferenceToken);
        Assert.Equal(0x23000002, assemblyNamespaceAlias.AssemblyReferenceToken);
        Assert.Null(importedAssemblyAlias.AssemblyReferenceToken);
        Assert.Equal(0x01000001, combiningAlias.TargetTypeToken);

        Assert.Throws<ArgumentException>(() => DumpPortablePdbImportScopeIdentity.Create(
            scopeToken,
            parentImportScopeToken: null,
            nestingDepth: 0,
            [namespaceImport, namespaceImport]));

        ImmutableCollectionsMarshal.AsArray(mutableInput)![0] = 0xFF;
        Assert.Equal(0x01, namespaceImport.RawPayload[0]);
        var exported = namespaceImport.RawPayload;
        ImmutableCollectionsMarshal.AsArray(exported)![0] = 0xEE;
        Assert.Equal(0x01, namespaceImport.RawPayload[0]);

        var conflictingOrdinal = DumpPortablePdbImportFact.NamespaceImport(
            scopeToken,
            0,
            rawKind: 1,
            "Other.Namespace",
            ImmutableArray<byte>.Empty);
        Assert.Throws<ArgumentException>(() => DumpPortablePdbImportScopeIdentity.Create(
            scopeToken,
            null,
            0,
            [namespaceImport, conflictingOrdinal]));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbImportFact.UnsupportedRaw(
            scopeToken,
            6,
            0xFD,
            default));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpPortablePdbImportFact.NamespaceImport(
            0x32000001,
            0,
            1,
            "Synthetic",
            ImmutableArray<byte>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpPortablePdbImportFact.ExternAlias(
            scopeToken,
            6,
            6,
            "vendor",
            ImmutableArray<byte>.Empty,
            0x01000001));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpPortablePdbImportFact.NamespaceImport(
            scopeToken,
            7,
            rawKind: 2,
            "Synthetic",
            ImmutableArray<byte>.Empty));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbImportFact.UnsupportedRaw(
            scopeToken,
            7,
            rawKind: 9,
            ImmutableArray<byte>.Empty));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbImportFact.TypeAlias(
            scopeToken,
            7,
            rawKind: 7,
            "Alias",
            "Synthetic.Type",
            0x01000001,
            ImmutableArray<byte>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpPortablePdbImportFact.TypeAlias(
            scopeToken,
            7,
            rawKind: 9,
            "Alias",
            "Synthetic.Type",
            0x23000001,
            ImmutableArray<byte>.Empty));
    }

    /// <summary>
    /// Proves exact PDB context requires matching module/artifact ids, token RIDs, active ranges and parent chains,
    /// while competing exact aliases remain exact context rather than becoming context ambiguity.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Portable_pdb_context_validates_relations_and_retains_competing_exact_aliases()
    {
        var fixture = ContextFixture.Create(competingAliases: true);
        Assert.Equal(DumpContextEvidenceStatus.Exact, fixture.PdbObservation.Status);
        Assert.True(fixture.PdbObservation.HasExactFacts);
        Assert.Equal(2, fixture.PdbFacts.Imports.Count(static import => import.Kind == DumpPortablePdbImportKind.TypeAlias));
        Assert.All(
            fixture.PdbFacts.Imports.Where(static import => import.Kind == DumpPortablePdbImportKind.TypeAlias),
            static import => Assert.Equal("Envelope", import.Alias));

        Assert.Throws<ArgumentOutOfRangeException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            fixture.ModuleDebugIdentity,
            fixture.PdbArtifact,
            0x06000003,
            fixture.Document,
            fixture.LocalScopes,
            fixture.ImportScopes));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            fixture.ModuleDebugIdentity,
            fixture.PdbArtifact,
            0x31000004,
            fixture.Document,
            fixture.LocalScopes,
            fixture.ImportScopes));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            fixture.ModuleDebugIdentity,
            fixture.PdbArtifact,
            0x31000003,
            fixture.Document,
            default,
            fixture.ImportScopes));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            fixture.ModuleDebugIdentity,
            fixture.PdbArtifact,
            0x31000003,
            fixture.Document,
            fixture.LocalScopes,
            default));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            fixture.ModuleDebugIdentity,
            fixture.PdbArtifact,
            0x31000003,
            fixture.Document,
            [fixture.LocalScopes[0], fixture.LocalScopes[0]],
            fixture.ImportScopes));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            fixture.ModuleDebugIdentity,
            fixture.PdbArtifact,
            0x31000003,
            fixture.Document,
            fixture.LocalScopes,
            [fixture.ImportScopes[0], fixture.ImportScopes[0]]));

        var outsideScope = DumpPortablePdbLocalScopeIdentity.Create(
            0x32000001,
            0x06000003,
            0x35000001,
            startOffset: 20,
            length: 10,
            nestingDepth: 0);
        Assert.Throws<ArgumentException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            fixture.ModuleDebugIdentity,
            fixture.PdbArtifact,
            0x31000003,
            fixture.Document,
            [outsideScope],
            fixture.ImportScopes));

        var foreignModule = new ClrmdRuntimeModuleIdentity(
            Snapshot('f'),
            fixture.RuntimeModule.AppDomainAddress,
            fixture.RuntimeModule.ModuleAddress,
            fixture.RuntimeModule.ImageBase,
            fixture.RuntimeModule.ImageSize);
        var foreignModuleDebug = DumpModulePortablePdbDebugIdentity.Create(
            foreignModule,
            fixture.ModuleContent,
            fixture.DebugIdentity);
        Assert.Throws<ArgumentException>(() => DumpPortablePdbContextFacts.Acquire(
            fixture.Frame,
            foreignModuleDebug,
            fixture.PdbArtifact,
            0x31000003,
            fixture.Document,
            fixture.LocalScopes,
            fixture.ImportScopes));
    }

    /// <summary>
    /// Proves a GUID/stamp mismatch has exactly one legal disposition: conflict with no exact scope/import payload.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Portable_pdb_identity_mismatch_is_conflict_only_and_source_specific_issues_are_enforced()
    {
        var fixture = ContextFixture.Create();
        var mismatchingDebug = DumpPortablePdbDebugIdentity.Create(
            fixture.DebugIdentity.Guid,
            fixture.DebugIdentity.Stamp + 1);
        var mismatchSource = DumpPortablePdbEvidenceSource.ForCandidate(
            fixture.ModuleDebugIdentity,
            fixture.PdbContent,
            mismatchingDebug);
        var conflict = DumpPortablePdbObservation.Conflict(
            mismatchSource,
            DumpContextEvidenceIssue.PortablePdbIdentityMismatch,
            PdbBounds);
        Assert.Equal(DumpContextEvidenceStatus.Conflict, conflict.Status);
        Assert.Null(conflict.Facts);
        Assert.True(conflict.Source.HasIdentityMismatch);

        var moduleSource = DumpPortablePdbEvidenceSource.ForModule(fixture.ModuleDebugIdentity);
        var candidateWithoutDecodedId = DumpPortablePdbEvidenceSource.ForCandidate(
            fixture.ModuleDebugIdentity,
            fixture.PdbContent,
            observedDebugIdentity: null);
        var nonExact = new[]
        {
            DumpPortablePdbObservation.Partial(
                moduleSource,
                DumpContextEvidenceIssue.BoundReached,
                PdbBounds),
            DumpPortablePdbObservation.Unavailable(
                moduleSource,
                DumpContextEvidenceIssue.PortablePdbUnavailable,
                PdbBounds),
            DumpPortablePdbObservation.Ambiguous(
                moduleSource,
                DumpContextEvidenceIssue.PortablePdbAmbiguous,
                PdbBounds),
            DumpPortablePdbObservation.Invalid(
                candidateWithoutDecodedId,
                DumpContextEvidenceIssue.InvalidPortablePdb,
                PdbBounds),
            DumpPortablePdbObservation.Unsupported(
                candidateWithoutDecodedId,
                DumpContextEvidenceIssue.UnsupportedPortablePdb,
                PdbBounds),
        };
        Assert.All(nonExact, observation =>
        {
            Assert.False(observation.HasExactFacts);
            Assert.Null(observation.Facts);
            Assert.NotEmpty(observation.ReachedBounds);
        });

        Assert.Throws<ArgumentException>(() => DumpPortablePdbObservation.Partial(
            mismatchSource,
            DumpContextEvidenceIssue.SourceIncomplete,
            PdbBounds));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbObservation.Invalid(
            mismatchSource,
            DumpContextEvidenceIssue.InvalidPortablePdb,
            PdbBounds));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbObservation.Conflict(
            DumpPortablePdbEvidenceSource.ForCandidate(
                fixture.ModuleDebugIdentity,
                fixture.PdbContent,
                fixture.DebugIdentity),
            DumpContextEvidenceIssue.PortablePdbIdentityMismatch,
            PdbBounds));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForModule(fixture.ModuleDebugIdentity),
            DumpContextEvidenceIssue.FrameUnavailable,
            PdbBounds));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbObservation.Ambiguous(
            DumpPortablePdbEvidenceSource.ForModule(fixture.ModuleDebugIdentity),
            DumpContextEvidenceIssue.FrameAmbiguous,
            PdbBounds));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbObservation.Exact(
            fixture.PdbFacts,
            default));
    }

    /// <summary>
    /// Proves the acquired context is additive, snapshot/module consistent, and does not advance PDB acquisition past
    /// a failed selected-frame prerequisite.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Binding_context_is_additive_and_rejects_foreign_or_out_of_order_sources()
    {
        var fixture = ContextFixture.Create();
        var context = fixture.Context;
        Assert.Equal(DumpContextEvidenceStatus.Exact, context.SelectedFrame.Status);
        Assert.Equal(DumpContextEvidenceStatus.Exact, context.PortablePdb.Status);
        Assert.Equal(context, ContextFixture.Create().Context);

        var pdbUnavailable = DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForModule(fixture.ModuleDebugIdentity),
            DumpContextEvidenceIssue.PortablePdbUnavailable,
            PdbBounds);
        var frameOnly = DumpExpressionBindingContext.Acquire(
            fixture.Snapshot,
            fixture.FrameObservation,
            pdbUnavailable);
        Assert.Equal(DumpContextEvidenceStatus.Exact, frameOnly.SelectedFrame.Status);
        Assert.Equal(DumpContextEvidenceStatus.Unavailable, frameOnly.PortablePdb.Status);

        var missingFrame = DumpSelectedFrameObservation.Unavailable(
            fixture.Selector,
            DumpContextEvidenceIssue.FrameUnavailable,
            FrameBounds);
        var notReachedPdb = DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForSnapshot(fixture.Snapshot),
            DumpContextEvidenceIssue.PrerequisiteUnavailable,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        _ = DumpExpressionBindingContext.Acquire(fixture.Snapshot, missingFrame, notReachedPdb);
        Assert.Throws<ArgumentException>(() => DumpExpressionBindingContext.Acquire(
            fixture.Snapshot,
            missingFrame,
            pdbUnavailable));

        var foreignPdb = DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForSnapshot(Snapshot('f')),
            DumpContextEvidenceIssue.PrerequisiteUnavailable,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        Assert.Throws<ArgumentException>(() => DumpExpressionBindingContext.Acquire(
            fixture.Snapshot,
            fixture.FrameObservation,
            foreignPdb));
    }

    /// <summary>
    /// Proves fully qualified identity omits all context poison, current namespace omits PDB, imports include PDB
    /// provenance, exact-empty differs from unconsulted, and only the requested exact imports enter the identity.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Consulted_identity_contains_only_facts_actually_consulted()
    {
        var fixture = ContextFixture.Create();
        var poisonedPdb = DumpPortablePdbObservation.Invalid(
            DumpPortablePdbEvidenceSource.ForCandidate(
                fixture.ModuleDebugIdentity,
                fixture.PdbContent,
                observedDebugIdentity: null),
            DumpContextEvidenceIssue.InvalidPortablePdb,
            PdbBounds);
        var poisonedContext = DumpExpressionBindingContext.Acquire(
            fixture.Snapshot,
            fixture.FrameObservation,
            poisonedPdb);

        var fullyQualifiedExact = DumpConsultedBindingContextIdentity.ForFullyQualified(fixture.Context.Snapshot);
        var fullyQualifiedPoisoned = DumpConsultedBindingContextIdentity.ForFullyQualified(poisonedContext.Snapshot);
        Assert.Equal(fullyQualifiedExact, fullyQualifiedPoisoned);
        Assert.Null(fullyQualifiedExact.ConsultedFrameEvidence);

        var currentNamespaceExact = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            fixture.Context,
            currentNamespaceConsulted: true,
            importsConsulted: false,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
        var currentNamespacePoisoned = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            poisonedContext,
            currentNamespaceConsulted: true,
            importsConsulted: false,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
        Assert.Equal(currentNamespaceExact, currentNamespacePoisoned);
        Assert.Null(currentNamespaceExact.ImportEvidenceSource);
        Assert.False(currentNamespaceExact.ImportsConsulted);

        var selectedImport = fixture.PdbFacts.Imports[0];
        var importsOnly = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            fixture.Context,
            currentNamespaceConsulted: false,
            importsConsulted: true,
            [selectedImport]);
        Assert.True(importsOnly.ImportsConsulted);
        Assert.False(importsOnly.CurrentNamespaceConsulted);
        Assert.Single(importsOnly.ConsultedImports);
        Assert.Equal(fixture.FrameObservation, importsOnly.ConsultedFrameEvidence);
        Assert.Equal(fixture.PdbContent, importsOnly.ImportEvidenceSource!.CandidateContent);
        Assert.Throws<ArgumentException>(() => DumpConsultedBindingContextIdentity.FromAcquiredContext(
            fixture.Context,
            currentNamespaceConsulted: false,
            importsConsulted: true,
            [selectedImport, selectedImport]));

        var currentAndExactEmpty = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            fixture.Context,
            currentNamespaceConsulted: true,
            importsConsulted: true,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
        Assert.NotEqual(currentNamespaceExact, currentAndExactEmpty);
        Assert.Empty(currentAndExactEmpty.ConsultedImports);
        Assert.Equal(DumpContextEvidenceStatus.Exact, currentAndExactEmpty.ImportEvidenceStatus);

        var poisonedImports = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            poisonedContext,
            currentNamespaceConsulted: false,
            importsConsulted: true,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
        Assert.Empty(poisonedImports.ConsultedImports);
        Assert.Equal(DumpContextEvidenceStatus.Invalid, poisonedImports.ImportEvidenceStatus);
        Assert.NotEqual(importsOnly, poisonedImports);

        var firstMissingFrame = DumpSelectedFrameObservation.Unavailable(
            fixture.Selector,
            DumpContextEvidenceIssue.FrameUnavailable,
            FrameBounds);
        var secondSelector = DumpSelectedFrameSelector.Create(fixture.Snapshot, threadOrdinal: 3, frameOrdinal: 4);
        var secondMissingFrame = DumpSelectedFrameObservation.Unavailable(
            secondSelector,
            DumpContextEvidenceIssue.FrameUnavailable,
            FrameBounds);
        var pdbNotReached = DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForSnapshot(fixture.Snapshot),
            DumpContextEvidenceIssue.PrerequisiteUnavailable,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var firstBlockedContext = DumpExpressionBindingContext.Acquire(
            fixture.Snapshot,
            firstMissingFrame,
            pdbNotReached);
        var secondBlockedContext = DumpExpressionBindingContext.Acquire(
            fixture.Snapshot,
            secondMissingFrame,
            pdbNotReached);
        var firstBlockedConsultation = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            firstBlockedContext,
            currentNamespaceConsulted: false,
            importsConsulted: true,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
        var secondBlockedConsultation = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            secondBlockedContext,
            currentNamespaceConsulted: false,
            importsConsulted: true,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
        Assert.NotEqual(firstBlockedConsultation, secondBlockedConsultation);

        var unrelated = DumpPortablePdbImportFact.NamespaceImport(
            0x35000001,
            99,
            1,
            "Not.In.Context",
            ImmutableArray<byte>.Empty);
        Assert.Throws<ArgumentException>(() => DumpConsultedBindingContextIdentity.FromAcquiredContext(
            fixture.Context,
            false,
            true,
            [unrelated]));
        Assert.Throws<ArgumentException>(() => DumpConsultedBindingContextIdentity.FromAcquiredContext(
            poisonedContext,
            false,
            true,
            [selectedImport]));
        Assert.Throws<ArgumentException>(() => DumpConsultedBindingContextIdentity.FromAcquiredContext(
            fixture.Context,
            true,
            true,
            default));
        Assert.Throws<ArgumentException>(() => DumpConsultedBindingContextIdentity.FromAcquiredContext(
            fixture.Context,
            false,
            false,
            ImmutableArray<DumpPortablePdbImportFact>.Empty));
    }

    /// <summary>
    /// Proves document and import raw bytes are defensively copied and canonical identities react to one-field
    /// perturbations while replay-equivalent construction remains equal.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W7ContextContractsV1")]
    public void Portable_pdb_byte_payloads_and_context_canonical_identity_are_defensive()
    {
        var checksum = ImmutableArray.Create<byte>(0x10, 0x20, 0x30, 0x40);
        var document = DumpPortablePdbDocumentIdentity.Create(
            0x30000001,
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Guid.Parse("8829d00f-11b8-4213-878b-770e8597ac16"),
            checksum);
        ImmutableCollectionsMarshal.AsArray(checksum)![0] = 0xFF;
        Assert.Equal(0x10, document.Checksum[0]);
        var exportedChecksum = document.Checksum;
        ImmutableCollectionsMarshal.AsArray(exportedChecksum)![0] = 0xEE;
        Assert.Equal(0x10, document.Checksum[0]);
        Assert.Throws<ArgumentException>(() => DumpPortablePdbDocumentIdentity.Create(
            0x30000001,
            Guid.Empty,
            Guid.Parse("8829d00f-11b8-4213-878b-770e8597ac16"),
            default));
        var nilChecksum = DumpPortablePdbDocumentIdentity.Create(
            0x30000002,
            Guid.Empty,
            Guid.Empty,
            ImmutableArray<byte>.Empty);
        Assert.Empty(nilChecksum.Checksum);
        Assert.Throws<ArgumentException>(() => DumpPortablePdbDocumentIdentity.Create(
            0x30000002,
            Guid.Empty,
            Guid.Empty,
            [0x01]));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbDocumentIdentity.Create(
            0x30000002,
            Guid.Empty,
            Guid.Parse("8829d00f-11b8-4213-878b-770e8597ac16"),
            ImmutableArray<byte>.Empty));

        var first = ContextFixture.Create().Context;
        var replay = ContextFixture.Create().Context;
        var changed = ContextFixture.Create(instructionOffset: 11).Context;
        Assert.Equal(first, replay);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
        Assert.NotEqual(first, changed);
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.NotEqual(first.Sha256, changed.Sha256);
    }

    private static ClrmdSnapshotIdentity Snapshot(char digit) => new(new string(digit, 64));

    private sealed class ContextFixture
    {
        private ContextFixture(
            ClrmdSnapshotIdentity snapshot,
            ClrmdRuntimeModuleIdentity runtimeModule,
            ModuleContentIdentity moduleContent,
            DumpSelectedFrameSelector selector,
            DumpSelectedFrameIdentity frame,
            DumpSelectedFrameObservation frameObservation,
            DumpPortablePdbDebugIdentity debugIdentity,
            DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
            DumpPortablePdbContentIdentity pdbContent,
            DumpPortablePdbArtifactIdentity pdbArtifact,
            DumpPortablePdbDocumentIdentity document,
            ImmutableArray<DumpPortablePdbLocalScopeIdentity> localScopes,
            ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes,
            DumpPortablePdbContextFacts pdbFacts,
            DumpPortablePdbObservation pdbObservation,
            DumpExpressionBindingContext context)
        {
            Snapshot = snapshot;
            RuntimeModule = runtimeModule;
            ModuleContent = moduleContent;
            Selector = selector;
            Frame = frame;
            FrameObservation = frameObservation;
            DebugIdentity = debugIdentity;
            ModuleDebugIdentity = moduleDebugIdentity;
            PdbContent = pdbContent;
            PdbArtifact = pdbArtifact;
            Document = document;
            LocalScopes = localScopes;
            ImportScopes = importScopes;
            PdbFacts = pdbFacts;
            PdbObservation = pdbObservation;
            Context = context;
        }

        internal ClrmdSnapshotIdentity Snapshot { get; }

        internal ClrmdRuntimeModuleIdentity RuntimeModule { get; }

        internal ModuleContentIdentity ModuleContent { get; }

        internal DumpSelectedFrameSelector Selector { get; }

        internal DumpSelectedFrameIdentity Frame { get; }

        internal DumpSelectedFrameObservation FrameObservation { get; }

        internal DumpPortablePdbDebugIdentity DebugIdentity { get; }

        internal DumpModulePortablePdbDebugIdentity ModuleDebugIdentity { get; }

        internal DumpPortablePdbContentIdentity PdbContent { get; }

        internal DumpPortablePdbArtifactIdentity PdbArtifact { get; }

        internal DumpPortablePdbDocumentIdentity Document { get; }

        internal ImmutableArray<DumpPortablePdbLocalScopeIdentity> LocalScopes { get; }

        internal ImmutableArray<DumpPortablePdbImportScopeIdentity> ImportScopes { get; }

        internal DumpPortablePdbContextFacts PdbFacts { get; }

        internal DumpPortablePdbObservation PdbObservation { get; }

        internal DumpExpressionBindingContext Context { get; }

        internal static ContextFixture Create(bool competingAliases = false, int instructionOffset = 10)
        {
            var snapshot = Snapshot('a');
            var runtimeModule = new ClrmdRuntimeModuleIdentity(
                snapshot,
                AppDomainAddress: 0x1000,
                ModuleAddress: 0x2000,
                ImageBase: 0x400000,
                ImageSize: 0x18000);
            var moduleContent = ModuleContentIdentity.FromDigest(
                Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
                metadataLength: 24_576,
                new string('b', 64));
            var selector = DumpSelectedFrameSelector.Create(snapshot, threadOrdinal: 2, frameOrdinal: 4);
            var frame = DumpSelectedFrameIdentity.Create(
                selector,
                managedThreadId: 37,
                runtimeThreadAddress: 0x7000,
                stackPointer: 0x7FFF0000,
                runtimeModule,
                moduleContent,
                methodDefinitionToken: 0x06000003,
                declaringTypeDefinitionToken: 0x02000002,
                declaringNamespace: "Synthetic.Incident",
                DumpInstructionLocation.Create(0x401234, instructionOffset));
            var frameObservation = DumpSelectedFrameObservation.Exact(frame, FrameBounds);
            var debugIdentity = DumpPortablePdbDebugIdentity.Create(
                Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
                stamp: 0x5A17C0DE);
            var moduleDebugIdentity = DumpModulePortablePdbDebugIdentity.Create(
                runtimeModule,
                moduleContent,
                debugIdentity);
            var pdbContent = DumpPortablePdbContentIdentity.Create(
                byteLength: 31_744,
                new string('c', 64));
            var pdbArtifact = DumpPortablePdbArtifactIdentity.Create(pdbContent, debugIdentity);
            var document = DumpPortablePdbDocumentIdentity.Create(
                0x30000001,
                Guid.Parse("3f5162f8-07c6-11d3-9053-00c04fa302a1"),
                Guid.Parse("8829d00f-11b8-4213-878b-770e8597ac16"),
                [0x01, 0x23, 0x45, 0x67]);

            var imports = new List<DumpPortablePdbImportFact>
            {
                DumpPortablePdbImportFact.NamespaceImport(
                    0x35000001,
                    0,
                    1,
                    "Synthetic.Services",
                    [0x01, 0x10]),
                DumpPortablePdbImportFact.TypeAlias(
                    0x35000001,
                    1,
                    9,
                    "Envelope",
                    "Synthetic.Services.RequestEnvelope",
                    0x01000001,
                    [0x09, 0x20]),
                DumpPortablePdbImportFact.NamespaceAlias(
                    0x35000001,
                    2,
                    7,
                    "Services",
                    "Synthetic.Services",
                    [0x07, 0x30]),
                DumpPortablePdbImportFact.UsingStatic(
                    0x35000001,
                    3,
                    3,
                    "Synthetic.Helpers.ContextValues",
                    0x01000002,
                    [0x03, 0x40]),
                DumpPortablePdbImportFact.ExternAlias(
                    0x35000001,
                    4,
                    6,
                    "vendor",
                    [0x06, 0x50],
                    0x23000001),
                DumpPortablePdbImportFact.UnsupportedRaw(
                    0x35000001,
                    5,
                    0xFE,
                    [0xFE, 0x60]),
            };
            if (competingAliases)
            {
                imports.Add(DumpPortablePdbImportFact.TypeAlias(
                    0x35000001,
                    6,
                    9,
                    "Envelope",
                    "Other.Services.RequestEnvelope",
                    0x01000003,
                    [0x09, 0x70]));
            }

            var importScope = DumpPortablePdbImportScopeIdentity.Create(
                0x35000001,
                parentImportScopeToken: null,
                nestingDepth: 0,
                ImmutableArray.CreateRange(imports));
            var localScope = DumpPortablePdbLocalScopeIdentity.Create(
                0x32000001,
                0x06000003,
                0x35000001,
                startOffset: 0,
                length: 100,
                nestingDepth: 0);
            var localScopes = ImmutableArray.Create(localScope);
            var importScopes = ImmutableArray.Create(importScope);
            var pdbFacts = DumpPortablePdbContextFacts.Acquire(
                frame,
                moduleDebugIdentity,
                pdbArtifact,
                methodDebugInformationToken: 0x31000003,
                document,
                localScopes,
                importScopes);
            var pdbObservation = DumpPortablePdbObservation.Exact(pdbFacts, PdbBounds);
            var context = DumpExpressionBindingContext.Acquire(
                snapshot,
                frameObservation,
                pdbObservation);
            return new ContextFixture(
                snapshot,
                runtimeModule,
                moduleContent,
                selector,
                frame,
                frameObservation,
                debugIdentity,
                moduleDebugIdentity,
                pdbContent,
                pdbArtifact,
                document,
                localScopes,
                importScopes,
                pdbFacts,
                pdbObservation,
                context);
        }
    }
}
