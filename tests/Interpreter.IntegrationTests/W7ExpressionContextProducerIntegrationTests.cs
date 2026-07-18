using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

internal static class W7TestTargetPaths
{
    internal const string AssemblyFileName = "Interpreter.W7TestTarget.dll";

    internal static string ResolveExecutable()
    {
        var testsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Interpreter.W7TestTarget.exe"
            : "Interpreter.W7TestTarget";
        return Path.Combine(
            testsRoot,
            "Interpreter.W7TestTarget",
            "bin",
            "Release",
            targetFramework,
            executableName);
    }

    internal static string ResolvePortablePdb()
    {
        var directory = Path.GetDirectoryName(ResolveExecutable())
            ?? throw new InvalidOperationException("Could not determine the W7 target directory.");
        return Path.Combine(directory, "Interpreter.W7TestTarget.pdb");
    }
}

/// <summary>
/// Exercises the real W7 selected-frame, mapped-PE debug identity, Portable-PDB scope/import, and contextual metadata
/// binder pipeline over independent optimized synthetic dumps.
/// </summary>
public sealed class W7ExpressionContextProducerIntegrationTests
{
    private const int MaximumTestThreadOrdinals = 64;
    private const int MaximumTestFrameOrdinals = 32;

    /// <summary>
    /// Proves exact, partial, and unavailable resolver reads are immutable and reject structurally contradictory
    /// lengths before any dump or artifact capability is involved.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Artifact_read_contract_freezes_complete_and_incomplete_prefixes()
    {
        var source = ImmutableArray.Create<byte>(0x42, 0x53, 0x4A, 0x42);
        var exact = DumpPortablePdbArtifactRead.Exact("synthetic:exact-contract", source);
        var partial = DumpPortablePdbArtifactRead.Partial(
            "synthetic:partial-contract",
            declaredByteLength: 11,
            source[..2]);
        var unavailable = DumpPortablePdbArtifactRead.Unavailable("synthetic:unavailable-contract");

        Assert.Equal(DumpPortablePdbArtifactReadStatus.Exact, exact.Status);
        Assert.Equal(4, exact.DeclaredByteLength);
        Assert.Equal(source.ToArray(), exact.Bytes.ToArray());
        Assert.Equal(DumpPortablePdbArtifactReadStatus.Partial, partial.Status);
        Assert.Equal(11, partial.DeclaredByteLength);
        Assert.Equal(source[..2].ToArray(), partial.Bytes.ToArray());
        Assert.Equal(DumpPortablePdbArtifactReadStatus.Unavailable, unavailable.Status);
        Assert.Null(unavailable.DeclaredByteLength);
        Assert.Empty(unavailable.Bytes);

        Assert.Throws<ArgumentException>(() => DumpPortablePdbArtifactRead.Exact("source", default));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbArtifactRead.Exact(" ", source));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbArtifactRead.Exact(new string('s', 4_097), source));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpPortablePdbArtifactRead.Partial(
            "source", 0, ImmutableArray<byte>.Empty));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbArtifactRead.Partial(
            "source", source.Length, source));
        Assert.Throws<ArgumentException>(() => DumpPortablePdbArtifactRead.Partial(
            "source", source.Length + 1, default));
    }

    /// <summary>
    /// Proves namespace-import, type-alias, and current-namespace lookup from real generated dumps, with close/reopen
    /// canonical replay and no path-derived PDB identity.
    /// </summary>
    /// <param name="incidentId">The independently launched synthetic incident.</param>
    /// <param name="frameNamespace">The exact lexical namespace of the selected caller frame.</param>
    /// <param name="expression">The complete contextual static expression parsed once by Roslyn.</param>
    /// <param name="expectedFieldName">The directly declared static field expected from metadata binding.</param>
    /// <param name="expectedExpansion">The contextual name rule expected to contribute the selected symbol.</param>
    /// <param name="expectedImportKind">
    /// The required PDB import kind, or null when the selected-frame declaring namespace supplies the candidate.
    /// </param>
    /// <param name="expectedImportText">The required decoded namespace target or alias text, when applicable.</param>
    [Theory]
    [InlineData(
        "batch-imported-direct-field",
        "Interpreter.W7TestTarget.BatchContext",
        "BatchStatics.Root.State",
        "Root",
        StaticFieldNameExpansionKind.NamespaceImport,
        DumpPortablePdbImportKind.Namespace,
        "Interpreter.W7TestTarget.Batch")]
    [InlineData(
        "coordinator-type-alias-owner",
        "Interpreter.W7TestTarget.CoordinatorContext",
        "CoordinatorValues.Root.Owner?.Name",
        "Root",
        StaticFieldNameExpansionKind.TypeAlias,
        DumpPortablePdbImportKind.TypeAlias,
        "CoordinatorValues")]
    [InlineData(
        "workflow-current-namespace-chain",
        "Interpreter.W7TestTarget.Workflow",
        "WorkflowStatics.Root.CurrentAttempt.Status",
        "Root",
        StaticFieldNameExpansionKind.CurrentNamespace,
        null,
        null)]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W7ExpressionContextProducerV1")]
    public void Real_frame_and_portable_pdb_context_bind_and_replay(
        string incidentId,
        string frameNamespace,
        string expression,
        string expectedFieldName,
        StaticFieldNameExpansionKind expectedExpansion,
        DumpPortablePdbImportKind? expectedImportKind,
        string? expectedImportText)
    {
        var executable = W7TestTargetPaths.ResolveExecutable();
        var portablePdb = W7TestTargetPaths.ResolvePortablePdb();
        Assert.True(File.Exists(executable), $"Expected the W7 target at '{executable}'.");
        Assert.True(File.Exists(portablePdb), $"Expected the W7 Portable PDB at '{portablePdb}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w7-context-{incidentId}-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                       executable,
                       ["--incident", incidentId],
                       isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var first = Observe(
                dumpPath,
                portablePdb,
                frameNamespace,
                expression,
                expectedImportKind,
                expectedImportText,
                verifyMismatchingPdb: expectedExpansion == StaticFieldNameExpansionKind.NamespaceImport);
            var replay = Observe(
                dumpPath,
                portablePdb,
                frameNamespace,
                expression,
                expectedImportKind,
                expectedImportText,
                verifyMismatchingPdb: false);

            Assert.Equal(expectedFieldName, first.FieldName);
            Assert.Contains(expectedExpansion.ToString(), first.ExpansionKinds, StringComparison.Ordinal);
            Assert.Equal(first, replay);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static ContextObservation Observe(
        string dumpPath,
        string portablePdb,
        string frameNamespace,
        string expression,
        DumpPortablePdbImportKind? expectedImportKind,
        string? expectedImportText,
        bool verifyMismatchingPdb)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var session = Assert.IsType<ClrmdDumpSession>(opened.Value);
        var targetModule = Assert.Single(
            session.Modules,
            static module => string.Equals(module.Name, W7TestTargetPaths.AssemblyFileName, StringComparison.Ordinal));
        var frame = SelectExactFrame(session, targetModule.Identity, frameNamespace);

        if (verifyMismatchingPdb)
        {
            var mismatchingPdb = Path.Combine(AppContext.BaseDirectory, "Interpreter.IntegrationTests.pdb");
            Assert.True(File.Exists(mismatchingPdb), $"Expected the test PDB at '{mismatchingPdb}'.");
            var conflict = session.ReadExpressionPortablePdbContext(frame, [mismatchingPdb]);
            Assert.True(
                conflict.Status == DumpContextEvidenceStatus.Conflict,
                $"Expected PDB identity conflict, observed {conflict.Status}/{conflict.Issue}; " +
                $"mapped image 0x{targetModule.Identity.ImageBase:X}+0x{targetModule.Identity.ImageSize:X}.");
            Assert.Equal(DumpContextEvidenceIssue.PortablePdbIdentityMismatch, conflict.Issue);
            Assert.True(conflict.Source.HasIdentityMismatch);

            VerifyResolverDispositions(session, frame, portablePdb, targetModule.Identity);
        }

        var pdb = session.ReadExpressionPortablePdbContext(frame, [portablePdb, portablePdb]);
        Assert.True(
            pdb.Status == DumpContextEvidenceStatus.Exact,
            $"Expected exact PDB context, observed {pdb.Status}/{pdb.Issue}; " +
            $"mapped image 0x{targetModule.Identity.ImageBase:X}+0x{targetModule.Identity.ImageSize:X}.");
        Assert.Equal(DumpContextEvidenceIssue.None, pdb.Issue);
        var facts = Assert.IsType<DumpPortablePdbContextFacts>(pdb.Facts);
        Assert.Equal(frame.Frame!.Sha256, facts.SelectedFrame.Sha256);
        Assert.NotEmpty(facts.LocalScopes);
        Assert.NotEmpty(facts.ImportScopes);
        Assert.Equal(facts.Artifact.DebugIdentity, facts.ModuleDebugIdentity.DebugIdentity);
        Assert.True(facts.Artifact.Content.ByteLength > 0);

        if (expectedImportKind is { } importKind)
        {
            var import = Assert.Single(facts.Imports, item =>
                item.Kind == importKind &&
                (string.Equals(item.Target, expectedImportText, StringComparison.Ordinal) ||
                 string.Equals(item.Alias, expectedImportText, StringComparison.Ordinal)));
            Assert.NotEmpty(import.RawPayload);
        }

        var context = DumpExpressionBindingContext.Acquire(session.Snapshot, frame, pdb);
        var syntax = StaticFieldExpressionParser.Parse(expression);
        var descriptor = Assert.IsType<StaticFieldExpressionDescriptor>(syntax.Descriptor);
        var binding = StaticFieldContextualBinder.Bind(session, descriptor, context);
        Assert.Equal(StaticFieldBindingStatus.Exact, binding.Status);
        Assert.Equal(StaticFieldBindingIssue.None, binding.Issue);
        var declaration = Assert.IsType<StaticFieldSymbolDeclarationIdentity>(binding.SelectedDeclaration);
        Assert.Equal(targetModule.Identity.ModuleAddress, declaration.Module.ModuleAddress);
        Assert.Equal(targetModule.Identity.AppDomainAddress, declaration.Module.ApplicationDomainAddress);
        Assert.Equal(targetModule.Identity.ImageBase, declaration.Module.ImageBase);
        Assert.Equal(targetModule.Identity.ImageSize, declaration.Module.ImageSize);

        return new ContextObservation(
            frame.Sha256,
            pdb.Sha256,
            context.Sha256,
            binding.Sha256,
            declaration.FieldName,
            string.Join(
                ",",
                binding.Expansions.Select(static expansion => expansion.Kind).Distinct().Order()));
    }

    private static void VerifyResolverDispositions(
        ClrmdDumpSession session,
        DumpSelectedFrameObservation frame,
        string portablePdb,
        ClrmdRuntimeModuleIdentity targetModule)
    {
        var bytes = ImmutableArray.CreateRange(File.ReadAllBytes(portablePdb));
        var partialResolver = new DelegateArtifactResolver(_ =>
            [DumpPortablePdbArtifactRead.Partial("synthetic:partial", bytes.Length, bytes[..113])]);
        var partial = session.ReadExpressionPortablePdbContext(frame, partialResolver);
        Assert.Equal(DumpContextEvidenceStatus.Partial, partial.Status);
        Assert.Equal(DumpContextEvidenceIssue.SourceIncomplete, partial.Issue);
        AssertResolutionRequest(partialResolver, targetModule);

        var overBoundResolver = new DelegateArtifactResolver(request =>
            [DumpPortablePdbArtifactRead.Partial(
                "synthetic:over-bound",
                request.ByteBound.Value + 1,
                ImmutableArray<byte>.Empty)]);
        var overBound = session.ReadExpressionPortablePdbContext(frame, overBoundResolver);
        Assert.Equal(DumpContextEvidenceStatus.Partial, overBound.Status);
        Assert.Equal(DumpContextEvidenceIssue.BoundReached, overBound.Issue);

        var unavailableResolver = new DelegateArtifactResolver(_ =>
            [DumpPortablePdbArtifactRead.Unavailable("synthetic:unavailable")]);
        var unavailable = session.ReadExpressionPortablePdbContext(frame, unavailableResolver);
        Assert.Equal(DumpContextEvidenceStatus.Unavailable, unavailable.Status);
        Assert.Equal(DumpContextEvidenceIssue.PortablePdbUnavailable, unavailable.Issue);

        var mixedResolver = new DelegateArtifactResolver(_ =>
            [
                DumpPortablePdbArtifactRead.Exact("synthetic:exact", bytes),
                DumpPortablePdbArtifactRead.Unavailable("synthetic:unavailable-peer"),
            ]);
        var mixed = session.ReadExpressionPortablePdbContext(frame, mixedResolver);
        Assert.Equal(DumpContextEvidenceStatus.Partial, mixed.Status);
        Assert.Equal(DumpContextEvidenceIssue.SourceIncomplete, mixed.Issue);

        var firstExact = session.ReadExpressionPortablePdbContext(
            frame,
            new DelegateArtifactResolver(_ =>
                [DumpPortablePdbArtifactRead.Exact("synthetic:first-store", bytes)]));
        var duplicateExact = session.ReadExpressionPortablePdbContext(
            frame,
            new DelegateArtifactResolver(_ =>
                [
                    DumpPortablePdbArtifactRead.Exact("synthetic:second-store", bytes),
                    DumpPortablePdbArtifactRead.Exact("synthetic:third-store", bytes),
                ]));
        Assert.Equal(DumpContextEvidenceStatus.Exact, firstExact.Status);
        Assert.Equal(firstExact.Sha256, duplicateExact.Sha256);

        var defaultResolver = new DelegateArtifactResolver(_ => default);
        var invalidDefault = session.ReadExpressionPortablePdbContext(frame, defaultResolver);
        Assert.Equal(DumpContextEvidenceStatus.Invalid, invalidDefault.Status);
        Assert.Equal(DumpContextEvidenceIssue.InvalidPortablePdb, invalidDefault.Issue);

        var overCountResolver = new DelegateArtifactResolver(request =>
            Enumerable.Range(0, checked((int)request.CandidateBound.Value) + 1)
                .Select(index => DumpPortablePdbArtifactRead.Unavailable($"synthetic:over-count:{index}"))
                .ToImmutableArray());
        var overCount = session.ReadExpressionPortablePdbContext(frame, overCountResolver);
        Assert.Equal(DumpContextEvidenceStatus.Partial, overCount.Status);
        Assert.Equal(DumpContextEvidenceIssue.BoundReached, overCount.Issue);

        var unavailableException = session.ReadExpressionPortablePdbContext(
            frame,
            new DelegateArtifactResolver(_ => throw new IOException("synthetic resolver failure")));
        Assert.Equal(DumpContextEvidenceStatus.Unavailable, unavailableException.Status);
        Assert.Equal(DumpContextEvidenceIssue.PortablePdbUnavailable, unavailableException.Issue);

        var invalidException = session.ReadExpressionPortablePdbContext(
            frame,
            new DelegateArtifactResolver(_ => throw new InvalidOperationException("synthetic resolver defect")));
        Assert.Equal(DumpContextEvidenceStatus.Invalid, invalidException.Status);
        Assert.Equal(DumpContextEvidenceIssue.InvalidPortablePdb, invalidException.Issue);

        var missingFrame = session.SelectExpressionFrame(DumpSelectedFrameSelector.Create(
            session.Snapshot,
            threadOrdinal: int.MaxValue,
            frameOrdinal: 0));
        Assert.Equal(DumpContextEvidenceStatus.Unavailable, missingFrame.Status);
        var poisonResolver = new DelegateArtifactResolver(_ =>
            throw new InvalidOperationException("The resolver must not run without an exact frame."));
        var prerequisite = session.ReadExpressionPortablePdbContext(missingFrame, poisonResolver);
        Assert.Equal(DumpContextEvidenceStatus.Unavailable, prerequisite.Status);
        Assert.Equal(DumpContextEvidenceIssue.PrerequisiteUnavailable, prerequisite.Issue);
        Assert.Null(poisonResolver.LastRequest);
    }

    private static void AssertResolutionRequest(
        DelegateArtifactResolver resolver,
        ClrmdRuntimeModuleIdentity targetModule)
    {
        var request = Assert.IsType<DumpPortablePdbArtifactResolutionRequest>(resolver.LastRequest);
        Assert.Equal(targetModule, request.ExpectedModule.RuntimeModule);
        Assert.Equal(ClrmdDumpSession.PortablePdbCandidateTraversalBound, request.CandidateBound);
        Assert.Equal(ClrmdDumpSession.PortablePdbArtifactByteBound, request.ByteBound);
    }

    private static DumpSelectedFrameObservation SelectExactFrame(
        ClrmdDumpSession session,
        ClrmdRuntimeModuleIdentity targetModule,
        string frameNamespace)
    {
        for (var threadOrdinal = 0; threadOrdinal < MaximumTestThreadOrdinals; threadOrdinal++)
        {
            for (var frameOrdinal = 0; frameOrdinal < MaximumTestFrameOrdinals; frameOrdinal++)
            {
                var selector = DumpSelectedFrameSelector.Create(session.Snapshot, threadOrdinal, frameOrdinal);
                var observation = session.SelectExpressionFrame(selector);
                if (observation.Frame is { } candidate &&
                    candidate.RuntimeModule.Equals(targetModule) &&
                    string.Equals(candidate.DeclaringNamespace, frameNamespace, StringComparison.Ordinal))
                {
                    return observation;
                }

                if (frameOrdinal > 0 &&
                    observation.Status == DumpContextEvidenceStatus.Unavailable &&
                    observation.Issue == DumpContextEvidenceIssue.FrameUnavailable)
                {
                    break;
                }
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"No exact '{frameNamespace}' W7 frame was found under the test's " +
            $"{MaximumTestThreadOrdinals} thread / {MaximumTestFrameOrdinals} frame search bounds.");
    }

    private sealed record ContextObservation(
        string FrameSha256,
        string PortablePdbSha256,
        string ContextSha256,
        string BindingSha256,
        string FieldName,
        string ExpansionKinds);

    private sealed class DelegateArtifactResolver(
        Func<DumpPortablePdbArtifactResolutionRequest, ImmutableArray<DumpPortablePdbArtifactRead>> resolve)
        : IDumpPortablePdbArtifactResolver
    {
        internal DumpPortablePdbArtifactResolutionRequest? LastRequest { get; private set; }

        ImmutableArray<DumpPortablePdbArtifactRead> IDumpPortablePdbArtifactResolver.Resolve(
            DumpPortablePdbArtifactResolutionRequest request)
        {
            LastRequest = request;
            return resolve(request);
        }
    }
}
