using System.Collections.Immutable;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Freezes the front-end-shared Inspection API surface that every host (desktop shell, console) renders by rather
/// than re-implementing: Portable-PDB candidate parsing and bounding, the module-filter contract, and the single
/// statement of which source-verification outcomes permit showing content.
/// </summary>
public sealed class InspectionApiSurfaceTests
{
    /// <summary>Proves candidate parsing trims, drops blanks, and enforces the adapter bound.</summary>
    [Fact]
    public void Candidate_parsing_is_trimmed_and_bounded()
    {
        Assert.Empty(SourceNavigationService.ParseCandidateList(null));
        Assert.Empty(SourceNavigationService.ParseCandidateList("   \r\n \n"));

        var parsed = SourceNavigationService.ParseCandidateList(
            "  C:\\a\\one.pdb  \r\n\r\nC:\\b\\two.pdb\n   \nC:\\c\\three.pdb");
        Assert.Equal(["C:\\a\\one.pdb", "C:\\b\\two.pdb", "C:\\c\\three.pdb"], parsed.ToArray());

        var overLimit = string.Join(
            '\n',
            Enumerable.Range(0, SourceNavigationService.MaximumPortablePdbCandidates + 10)
                .Select(static index => $"C:\\pdb\\{index}.pdb"));
        Assert.Equal(
            SourceNavigationService.MaximumPortablePdbCandidates,
            SourceNavigationService.ParseCandidateList(overLimit).Length);
    }

    /// <summary>Proves the module-filter contract: blank admits all, otherwise name or path hint, case-folded.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("console", true)]
    [InlineData("CONSOLE", true)]
    [InlineData("app\\System", true)]
    [InlineData("kernel32", false)]
    public void Module_filter_matches_name_or_path_hint(string? filter, bool expected)
    {
        var row = new ModuleRow(
            Module: null!,
            Name: "System.Console.dll",
            AppDomainId: 1,
            MetadataAddress: "0x0",
            MetadataLength: "0",
            Layout: "Loaded",
            TargetPathHint: "C:\\app\\System.Console.dll");
        Assert.Equal(expected, row.Matches(filter));
    }

    /// <summary>Proves the one displayability statement front ends render source content by.</summary>
    [Theory]
    [InlineData(SourceContentVerification.VerifiedExact, true)]
    [InlineData(SourceContentVerification.EmbeddedInMatchingPdb, true)]
    [InlineData(SourceContentVerification.SourceLinkVerified, true)]
    [InlineData(SourceContentVerification.DecompiledFromValidatedAssembly, true)]
    [InlineData(SourceContentVerification.NotApplicable, false)]
    [InlineData(SourceContentVerification.ContentMismatch, false)]
    [InlineData(SourceContentVerification.FileMissing, false)]
    [InlineData(SourceContentVerification.ChecksumUnavailable, false)]
    [InlineData(SourceContentVerification.FileTooLarge, false)]
    public void Content_displayability_follows_verification(SourceContentVerification verification, bool expected)
    {
        var result = new SourceViewResult
        {
            IsResolved = false,
            Title = "probe",
            Summary = "probe",
            Verification = verification,
        };
        Assert.Equal(expected, result.IsContentDisplayable);
    }

    /// <summary>
    /// Proves the host-facing evaluation options carry the product's own caps and defaults, so a front end needs
    /// no product-layer reference to offer or clamp them.
    /// </summary>
    [Fact]
    public void Root_relative_options_reexport_product_caps_and_defaults()
    {
        Assert.Equal(
            Product.DumpDebugging.CounterfactualMethodRequest.MaximumLogicalCallDepth,
            ExpressionEvaluationService.MaximumLogicalCallDepth);
        Assert.Equal(
            Product.DumpDebugging.CounterfactualMethodRequest.MaximumTraversalUnits,
            ExpressionEvaluationService.MaximumTraversalUnits);

        var defaults = new RootRelativeEvaluationOptions();
        Assert.True(defaults.AdmitMemberChain);
        Assert.False(defaults.UseModeledMethods);
        Assert.Equal(RootRelativeEvaluationOptions.DefaultInstructionLimit, defaults.InstructionLimit);
        Assert.Equal(RootRelativeEvaluationOptions.DefaultLogicalDepthLimit, defaults.LogicalDepthLimit);
        Assert.Equal(ExpressionEvaluationService.MaximumTraversalUnits, defaults.TraversalLimit);
    }

    /// <summary>
    /// Proves the assembled candidate list keeps explicit paths first, deduplicates case-insensitively against
    /// discovery, and stays within the bound, using a real dump session for the discovery half.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Assembled_candidates_merge_explicit_first_and_deduplicate()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"candidate-merge-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;

            var (discovered, probed) = SourceNavigationService.ProbePortablePdbCandidates(session);
            Assert.True(probed >= discovered.Length);
            Assert.Equal(
                discovered.ToArray(),
                SourceNavigationService.DiscoverPortablePdbCandidates(session).ToArray());

            ImmutableArray<string> explicitCandidates = ["C:\\explicit\\first.pdb", "C:\\explicit\\second.pdb"];
            var assembled = SourceNavigationService.AssemblePortablePdbCandidates(session, explicitCandidates);
            Assert.True(assembled.Length >= explicitCandidates.Length);
            Assert.Equal(explicitCandidates.ToArray(), assembled.Take(explicitCandidates.Length).ToArray());
            Assert.True(assembled.Length <= SourceNavigationService.MaximumPortablePdbCandidates);
            Assert.Equal(assembled.Length, assembled.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            // Re-offering a discovered path explicitly (in different casing) must not duplicate it.
            if (discovered.Length > 0)
            {
                var reoffered = SourceNavigationService.AssemblePortablePdbCandidates(
                    session, [discovered[0].ToUpperInvariant()]);
                Assert.Equal(assembled.Length - explicitCandidates.Length, reoffered.Length);
            }
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
