using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Validates the W8.9 decision candidate derived from the immutable v1 and v2 corpus records.</summary>
public sealed class W8PortfolioDecisionCandidateTests
{
    private static readonly string[] AxisNames =
    [
        "syntax", "context", "rootAttribution", "lexicalCompleteness", "typeBinding", "typeConstruction",
        "memberLookup", "runtimeConstruction", "storage", "value", "suffix", "completeness",
    ];

    private static readonly string[] AttributableOutcomes =
        ["Complete", "Completed", "Exact", "ExactNull", "ExactValue", "NotRequested"];

    private static readonly string[] SuccessfulAxisOutcomes =
        [.. AttributableOutcomes, "NotRequired"];

    /// <summary>
    /// Recomputes every corrected aggregate from the two content-addressed corpus inputs, proves each effective first
    /// boundary follows deterministically from its produced axes, and validates the unique proposed category.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W8MeaningfulSyntheticDecisionCandidate")]
    public void Decision_candidate_recomputes_measured_metrics_and_proposes_the_unique_leader()
    {
        var v1 = W8CorpusManifest.Load();
        using var v2 = W8ReconciledCorpusV2Manifest.Load();
        using var candidate = W8PortfolioDecisionCandidate.Load();
        Assert.Equal(1, candidate.Root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "interpreter-w8-static-field-portfolio-decision-candidate-v1",
            W8CorpusManifest.RequiredString(candidate.Root, "candidateId"));
        Assert.Equal(
            "derived-designed-synthetic-decision-candidate",
            W8CorpusManifest.RequiredString(candidate.Root, "evidenceKind"));

        var evidenceInputs = candidate.Root.GetProperty("evidenceInputs").EnumerateArray().ToArray();
        Assert.Equal(2, evidenceInputs.Length);
        AssertEvidenceInput(
            evidenceInputs[0],
            "frozen-predeclaration",
            "tests/corpus/w8-static-field-incidents-v1.json",
            W8CorpusManifest.RequiredString(v1.Root, "corpusId"),
            W8ReconciledCorpusV2Manifest.ComputeBaseSha256());
        AssertEvidenceInput(
            evidenceInputs[1],
            "produced-outcome-reconciliation",
            "tests/corpus/w8-static-field-incidents-v2.json",
            v2.CorpusId,
            W8PortfolioDecisionCandidate.ComputeSha256("tests/corpus/w8-static-field-incidents-v2.json"));
        Assert.Equal("64bd03319c774b16a4e49dca0c85c43059f8f7d220873ecc8f31c6774842ff37",
            W8CorpusManifest.RequiredString(evidenceInputs[0], "sha256"));
        Assert.Equal("468c78076bf3e149b395647fc1557c234d25399d48d42b425824b70bd413d35a",
            W8CorpusManifest.RequiredString(evidenceInputs[1], "sha256"));
        Assert.Equal("a6b35b67d35c00449dac632dc61ed4b269e9bfd679552a1e8dbea4cc34a20450", candidate.Sha256);

        var corrections = v2.Corrections.ToDictionary(static row => row.Id, StringComparer.Ordinal);
        var dispositions = candidate.ProposedDispositions.ToDictionary(static row => row.Id, StringComparer.Ordinal);
        var ownerRequired = v2.Corrections
            .Where(static row => row.CounterfactualDisposition.Contains(
                "owner-disposition-required",
                StringComparison.Ordinal))
            .OrderBy(static row => row.Ordinal)
            .ToArray();
        Assert.Equal(
            ownerRequired.Select(static row => (row.Id, row.Ordinal)).ToArray(),
            candidate.ProposedDispositions.OrderBy(static row => row.Ordinal)
                .Select(static row => (row.Id, row.Ordinal)).ToArray());
        Assert.Equal(7, dispositions.Count);
        Assert.Equal(5, dispositions.Values.Count(static row => row.Disposition == "retired-disproved-premise"));
        Assert.Equal(
            2,
            dispositions.Values.Count(static row => row.Disposition == "deferred-unrealized-physical-counterfactual"));
        Assert.All(dispositions.Values, static row => Assert.False(row.DecisionChanging));
        Assert.All(dispositions.Values, static row => Assert.False(string.IsNullOrWhiteSpace(row.Reason)));

        var verifiedElements = candidate.Root.GetProperty("verifiedCorrectedCounterfactuals").EnumerateArray().ToArray();
        var expectedEvidenceTests = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["workflow-derived-unsupported-member-hides-base"] =
                "W8MeaningfulSyntheticCorpusTests.Contextual_and_bare_incidents_stop_at_their_landed_scope_boundaries",
            ["coordinator-generic-head-arity-disagreement"] =
                "W8PortfolioDecisionCandidateTests.Derived_arity_and_complete_pdb_counterfactuals_change_their_measured_answers",
            ["workflow-substituted-reference-target-conflict"] =
                "W8MeaningfulSyntheticCorpusTests.Reference_target_conflict_row_blocks_its_suffix_and_records_the_read_value",
            ["batch-incomplete-local-catalogs"] =
                "W8PortfolioDecisionCandidateTests.Derived_arity_and_complete_pdb_counterfactuals_change_their_measured_answers",
        };
        Assert.Equal(expectedEvidenceTests.Keys, verifiedElements.Select(static item =>
            W8CorpusManifest.RequiredString(item, "id")));
        foreach (var verified in verifiedElements)
        {
            var id = W8CorpusManifest.RequiredString(verified, "id");
            Assert.True(verified.GetProperty("decisionChanging").GetBoolean());
            Assert.Equal(expectedEvidenceTests[id], W8CorpusManifest.RequiredString(verified, "evidenceTest"));
            Assert.Equal(corrections[id].Ordinal, verified.GetProperty("ordinal").GetInt32());
        }

        var nonDecisionIds = W8CorpusManifest.ReadStrings(
            candidate.Root.GetProperty("confirmedNonDecisionChangingCorrections"));
        Assert.Equal(
            new[]
            {
                "request-inner-alias-shadows-outer-alias",
                "coordinator-derived-owner-base-field",
            },
            nonDecisionIds.ToArray());
        Assert.All(nonDecisionIds, id => Assert.False(
            Assert.Single(v1.Incidents, row => row.Id == id).DecisionChanging));
        var partitionedCorrectionIds = dispositions.Keys
            .Concat(expectedEvidenceTests.Keys)
            .Concat(nonDecisionIds)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(corrections.Keys.Order(StringComparer.Ordinal).ToArray(), partitionedCorrectionIds);
        Assert.Equal(partitionedCorrectionIds.Length, partitionedCorrectionIds.Distinct(StringComparer.Ordinal).Count());

        var effective = v1.Incidents.Select(incident =>
        {
            corrections.TryGetValue(incident.Id, out var correction);
            dispositions.TryGetValue(incident.Id, out var disposition);
            var axes = correction?.ExpectedProducedAxes ?? incident.PredeclaredAxes;
            var expectedBoundary = correction?.ExpectedProducedFirstBoundary ?? incident.ExpectedFirstBoundary;
            var derivedBoundary = FirstBoundary(axes);
            Assert.Equal(expectedBoundary, derivedBoundary);
            return new EffectiveRow(
                incident,
                axes,
                derivedBoundary,
                disposition?.Useful ?? incident.UsefulnessValue,
                disposition?.DecisionChanging ?? incident.DecisionChanging,
                AttributableOutcomes.Contains(
                    W8CorpusIncident.AxisText(axes, incident.AttributableStage),
                    StringComparer.Ordinal));
        }).ToArray();

        Assert.Equal(35, effective.Length);
        var metrics = candidate.Root.GetProperty("candidatePortfolioMetrics");
        Assert.Equal(
            "conditional-on-unapproved-proposed-dispositions",
            W8CorpusManifest.RequiredString(metrics, "calculationBasis"));
        var inheritedExecuted = v1.Incidents.Count(static row => row.RunnerExecutionStatus == "executed");
        var v1ManifestOnly = v1.Incidents.Count(static row => row.RunnerExecutionStatus == "manifest-only");
        Assert.Equal(v1ManifestOnly, corrections.Count);
        var derivedExecuted = inheritedExecuted + corrections.Count;
        var derivedManifestOnly = v1ManifestOnly - corrections.Count;
        var derivedRepresentative = v1.Incidents.Count(static row => row.Representative);
        Assert.Equal(35, derivedExecuted);
        Assert.Equal(0, derivedManifestOnly);
        Assert.Equal(0, derivedRepresentative);
        Assert.Equal(derivedExecuted, metrics.GetProperty("executedBaselineCount").GetInt32());
        Assert.Equal(derivedManifestOnly, metrics.GetProperty("manifestOnlyBaselineCount").GetInt32());
        Assert.Equal(derivedRepresentative, metrics.GetProperty("representativeObservationCount").GetInt32());
        Assert.Equal(effective.Length, metrics.GetProperty("incidentCount").GetInt32());
        Assert.Equal(effective.Select(static row => row.Incident.Shape).Distinct(StringComparer.Ordinal).Count(),
            metrics.GetProperty("applicationShapeCount").GetInt32());
        Assert.Equal(effective.Count(static row => row.Useful), metrics.GetProperty("usefulCount").GetInt32());
        Assert.Equal(effective.Count(static row => row.DecisionChanging),
            metrics.GetProperty("decisionChangingCount").GetInt32());
        Assert.Equal(effective.Count(static row => row.Attributable),
            metrics.GetProperty("attributableEvidenceCount").GetInt32());
        Assert.Equal(effective.Count(static row => row.FirstBoundary == "none"),
            metrics.GetProperty("exactOrNoBoundaryCount").GetInt32());
        Assert.Equal(effective.Count(static row => row.FirstBoundary != "none"),
            metrics.GetProperty("firstBoundaryCount").GetInt32());
        Assert.Equal(33, effective.Count(static row => row.Useful));
        Assert.Equal(19, effective.Count(static row => row.DecisionChanging));
        Assert.Equal(25, effective.Count(static row => row.Attributable));
        Assert.Equal(24, effective.Count(static row => row.FirstBoundary == "none"));
        Assert.Equal(11, effective.Count(static row => row.FirstBoundary != "none"));

        var recordedBoundaries = candidate.Root.GetProperty("firstBoundaryCounts")
            .EnumerateObject()
            .ToDictionary(static item => item.Name, static item => item.Value.GetInt32(), StringComparer.Ordinal);
        var derivedBoundaries = effective
            .GroupBy(static row => row.FirstBoundary, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        Assert.Equal(
            recordedBoundaries.OrderBy(static pair => pair.Key, StringComparer.Ordinal).ToArray(),
            derivedBoundaries.OrderBy(static pair => pair.Key, StringComparer.Ordinal).ToArray());

        var categories = effective
            .GroupBy(static row => row.Incident.SupportsSuccessorCategory, StringComparer.Ordinal)
            .Select(static group => new DerivedCategory(
                group.Key,
                group.Count(),
                group.Select(static row => row.Incident.Shape).Distinct(StringComparer.Ordinal).Count(),
                group.Count(static row => row.Useful),
                group.Count(static row => row.DecisionChanging),
                group.Count(static row => row.Attributable),
                group.Count(static row => row.FirstBoundary != "none")))
            .OrderBy(static category => category.Category, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(candidate.Categories.Length, categories.Length);
        foreach (var category in categories)
        {
            Assert.Equal(category, Assert.Single(candidate.Categories, item => item.Category == category.Category));
        }

        var qualification = candidate.Root.GetProperty("qualification");
        var minimumIncidents = qualification.GetProperty("minimumIncidents").GetInt32();
        var minimumShapes = qualification.GetProperty("minimumApplicationShapes").GetInt32();
        var minimumDecisions = qualification.GetProperty("minimumDecisionChangingIncidents").GetInt32();
        var qualified = categories.Where(category =>
                category.IncidentCount >= minimumIncidents &&
                category.ApplicationShapeCount >= minimumShapes &&
                category.DecisionChangingCount >= minimumDecisions)
            .ToArray();
        Assert.Equal(
            W8CorpusManifest.ReadStrings(qualification.GetProperty("qualifiedCategories")).ToArray(),
            qualified.Select(static category => category.Category).ToArray());

        var ranked = qualified
            .OrderByDescending(static category => category.IncidentCount)
            .ThenByDescending(static category => category.ApplicationShapeCount)
            .ThenByDescending(static category => category.DecisionChangingCount)
            .ThenByDescending(static category => category.AttributableEvidenceCount)
            .ToArray();
        var leaders = ranked.Where(category => category.SubstantiveKey == ranked[0].SubstantiveKey).ToArray();
        Assert.Single(leaders);
        Assert.False(candidate.Root.TryGetProperty("decisionAuthority", out _));
        Assert.False(candidate.Root.TryGetProperty("finalDecision", out _));
        Assert.False(candidate.Root.TryGetProperty("counterfactualOwnerDispositions", out _));
        var proposal = candidate.Root.GetProperty("candidateSelection");
        Assert.Equal(
            "computed-under-proposals-pending-owner-approval",
            W8CorpusManifest.RequiredString(proposal, "status"));
        Assert.False(proposal.GetProperty("tieDefers").GetBoolean());
        Assert.Equal(leaders[0].Category, W8CorpusManifest.RequiredString(proposal, "proposedCategory"));
        Assert.Equal("observed-boundary-hardening", leaders[0].Category);
        Assert.Equal("14:4:7:9", leaders[0].SubstantiveKey);
        Assert.Equal("incidentCount", W8CorpusManifest.RequiredString(proposal, "decisiveKey"));

        var categoryAction = v1.Root.GetProperty("successorCategories").EnumerateArray()
            .Single(item => W8CorpusManifest.RequiredString(item, "category") == leaders[0].Category);
        Assert.Equal(
            W8CorpusManifest.RequiredString(categoryAction, "action"),
            W8CorpusManifest.RequiredString(proposal, "proposedAction"));
        Assert.Equal(
            "proposed-for-post-w8-planning-not-implemented-by-w8",
            W8CorpusManifest.RequiredString(proposal, "implementationDisposition"));

        var limits = candidate.Root.GetProperty("scopeLimits");
        Assert.False(limits.GetProperty("ownerAuthorityClaimed").GetBoolean());
        Assert.False(limits.GetProperty("w8_9ClosureClaimed").GetBoolean());
        Assert.False(limits.GetProperty("representativeEvidenceClaimed").GetBoolean());
        Assert.False(limits.GetProperty("proposedSuccessorImplementedByW8").GetBoolean());
        Assert.False(limits.GetProperty("w8_10ClosureClaimed").GetBoolean());
    }

    /// <summary>
    /// Executes the two corrected decision counterfactuals that v2 identified as derivable from the same dump, so
    /// their decision-changing credit rests on measured evidence rather than the v1 prediction.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticDecisionCandidate")]
    public void Derived_arity_and_complete_pdb_counterfactuals_change_their_measured_answers()
    {
        var v1 = W8CorpusManifest.Load();
        using var v2 = W8ReconciledCorpusV2Manifest.Load();

        var arity = Assert.Single(v1.Incidents, static row =>
            row.Id == "coordinator-generic-head-arity-disagreement");
        var arityCorrection = Assert.Single(v2.Corrections, row => row.Id == arity.Id);
        using (var snapshot = W8CorpusSnapshot.Materialize(arity))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, arity.Shape))
        {
            var baseline = world.Evaluate(arity.Expression, arity.ReadWidth);
            Assert.Equal(arityCorrection.ExpectedProducedAxes, baseline.Result.Axes);
            var aligned = world.Evaluate(
                "global::PhoenixInspect.W8CoordinatorShapeTarget.Registry<" +
                "global::PhoenixInspect.W8CoordinatorShapeTarget.NorthRegion>.Sentinel",
                arity.ReadWidth);
            Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, aligned.Result.Axes.TypeBinding);
            Assert.Equal(DumpExpressionValueOutcome.ExactValue, aligned.Result.Axes.Value);
            Assert.Equal(DumpExpressionCompletenessOutcome.Complete, aligned.Result.Axes.Completeness);
            Assert.Equal(0x71_00_03_01L, aligned.Result.SignedValue);
            Assert.NotEqual(baseline.Result.Sha256, aligned.Result.Sha256);
        }

        var partialPdb = Assert.Single(v1.Incidents, static row => row.Id == "batch-incomplete-local-catalogs");
        var partialPdbCorrection = Assert.Single(v2.Corrections, row => row.Id == partialPdb.Id);
        using (var snapshot = W8CorpusSnapshot.Materialize(partialPdb))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, partialPdb.Shape))
        {
            var thread = world.PausedFrameThreadSelector();
            var baseline = world.Evaluate(
                partialPdb.Expression,
                partialPdb.ReadWidth,
                thread,
                world.PausedFrameScopedContext(W8CorpusContextMode.TruncatedPdb));
            Assert.Equal(partialPdbCorrection.ExpectedProducedAxes, baseline.Result.Axes);
            var complete = world.Evaluate(
                partialPdb.Expression,
                partialPdb.ReadWidth,
                thread,
                world.PausedFrameScopedContext());
            Assert.Equal(DumpExpressionContextOutcome.Exact, complete.Result.Axes.Context);
            Assert.Equal(DumpExpressionValueOutcome.ExactValue, complete.Result.Axes.Value);
            Assert.Equal(DumpExpressionCompletenessOutcome.Complete, complete.Result.Axes.Completeness);
            Assert.Equal(0x61_00_02_1EL, complete.Result.SignedValue);
            Assert.NotEqual(baseline.Result.Sha256, complete.Result.Sha256);
        }
    }

    /// <summary>Freezes byte-identical machine and human reports over the content-addressed decision candidate.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W8MeaningfulSyntheticDecisionCandidate")]
    public void Two_fresh_consumers_emit_byte_identical_decision_candidate_reports()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var candidatePath = W8ShapeTargetPaths.RepositoryPath(
            "tests/corpus/w8-static-field-portfolio-decision-candidate-v1.json");
        var reportRoot = Path.Combine(
            Path.GetTempPath(),
            $"phoenixinspect-w8-decision-candidate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportRoot);
        try
        {
            var firstMachinePath = Path.Combine(reportRoot, "first.machine.txt");
            var firstHumanPath = Path.Combine(reportRoot, "first.human.txt");
            var secondMachinePath = Path.Combine(reportRoot, "second.machine.txt");
            var secondHumanPath = Path.Combine(reportRoot, "second.human.txt");

            RunSuccessfulDecisionCandidateConsumer(
                repositoryRoot,
                candidatePath,
                firstMachinePath,
                firstHumanPath);
            RunSuccessfulDecisionCandidateConsumer(
                repositoryRoot,
                candidatePath,
                secondMachinePath,
                secondHumanPath);

            var firstMachine = File.ReadAllBytes(firstMachinePath);
            var secondMachine = File.ReadAllBytes(secondMachinePath);
            var firstHuman = File.ReadAllBytes(firstHumanPath);
            var secondHuman = File.ReadAllBytes(secondHumanPath);
            Assert.True(firstMachine.AsSpan().SequenceEqual(secondMachine));
            Assert.True(firstHuman.AsSpan().SequenceEqual(secondHuman));
            Assert.Equal(
                "ad171092970320f569d7c9fa03a13aa46e4f93df63c81e2c5a56f5905e0b4c09",
                Digest(firstMachine));
            Assert.Equal(
                "be3dcbaa7af47d66cc41e960b6fb12bb1c7259d9ca1c86a94c8fabc6db899bcd",
                Digest(firstHuman));

            var machineText = Encoding.UTF8.GetString(firstMachine);
            var humanText = Encoding.UTF8.GetString(firstHuman);
            Assert.DoesNotContain("i32:", machineText, StringComparison.Ordinal);
            Assert.DoesNotContain("0x", humanText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(reportRoot, recursive: true);
        }
    }

    /// <summary>Rejects path collisions before the immutable candidate or either report path can be changed.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W8MeaningfulSyntheticDecisionCandidate")]
    public void Colliding_candidate_and_report_paths_fail_without_writing()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var sourceCandidatePath = W8ShapeTargetPaths.RepositoryPath(
            "tests/corpus/w8-static-field-portfolio-decision-candidate-v1.json");
        var reportRoot = Path.Combine(
            Path.GetTempPath(),
            $"phoenixinspect-w8-decision-candidate-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportRoot);
        try
        {
            var candidatePath = Path.Combine(reportRoot, "candidate.json");
            File.Copy(sourceCandidatePath, candidatePath);
            var candidateBytes = File.ReadAllBytes(candidatePath);
            var sharedOutputPath = Path.Combine(reportRoot, "shared.txt");
            var shared = RunDecisionCandidateConsumer(
                repositoryRoot,
                candidatePath,
                sharedOutputPath,
                sharedOutputPath);
            Assert.Equal(2, shared.ExitCode);
            Assert.Contains("W8_DECISION_CANDIDATE_ARGUMENT_INVALID:", shared.StandardError, StringComparison.Ordinal);
            Assert.True(string.IsNullOrWhiteSpace(shared.StandardOutput), shared.StandardOutput);
            Assert.False(File.Exists(sharedOutputPath));
            Assert.True(candidateBytes.AsSpan().SequenceEqual(File.ReadAllBytes(candidatePath)));

            var otherOutputPath = Path.Combine(reportRoot, "other.txt");
            var inputCollision = RunDecisionCandidateConsumer(
                repositoryRoot,
                candidatePath,
                candidatePath,
                otherOutputPath);
            Assert.Equal(2, inputCollision.ExitCode);
            Assert.Contains(
                "W8_DECISION_CANDIDATE_ARGUMENT_INVALID:",
                inputCollision.StandardError,
                StringComparison.Ordinal);
            Assert.True(string.IsNullOrWhiteSpace(inputCollision.StandardOutput), inputCollision.StandardOutput);
            Assert.False(File.Exists(otherOutputPath));
            Assert.True(candidateBytes.AsSpan().SequenceEqual(File.ReadAllBytes(candidatePath)));
        }
        finally
        {
            Directory.Delete(reportRoot, recursive: true);
        }
    }

    private static void RunSuccessfulDecisionCandidateConsumer(
        string repositoryRoot,
        string candidatePath,
        string machinePath,
        string humanPath)
    {
        var outcome = RunDecisionCandidateConsumer(repositoryRoot, candidatePath, machinePath, humanPath);
        Assert.True(
            outcome.ExitCode == 0,
            $"The W8 decision-candidate consumer exited with {outcome.ExitCode}. " +
            $"stdout='{outcome.StandardOutput}' stderr='{outcome.StandardError}'.");
        Assert.Equal(
            "W8_DECISION_CANDIDATE_OK:a6b35b67d35c00449dac632dc61ed4b269e9bfd679552a1e8dbea4cc34a20450",
            outcome.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(outcome.StandardError), outcome.StandardError);
    }

    private static ConsumerOutcome RunDecisionCandidateConsumer(
        string repositoryRoot,
        string candidatePath,
        string machinePath,
        string humanPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                ErrorDialog = false,
            },
        };
        process.StartInfo.ArgumentList.Add("-NoLogo");
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "eng", "Invoke-HeadlessProcess.ps1"));
        process.StartInfo.ArgumentList.Add(ResolveConsumerExecutable(repositoryRoot));
        process.StartInfo.ArgumentList.Add("--w8-decision-candidate");
        process.StartInfo.ArgumentList.Add(candidatePath);
        process.StartInfo.ArgumentList.Add("--machine-output");
        process.StartInfo.ArgumentList.Add(machinePath);
        process.StartInfo.ArgumentList.Add("--human-output");
        process.StartInfo.ArgumentList.Add(humanPath);
        process.StartInfo.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";

        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(10_000);
            }
            catch (InvalidOperationException)
            {
                // The process exited between the bounded wait and cleanup attempt.
            }

            throw new Xunit.Sdk.XunitException(
                "The W8 decision-candidate headless consumer did not exit within its bound.");
        }

        var standardOutput = stdout.GetAwaiter().GetResult();
        var standardError = stderr.GetAwaiter().GetResult();
        return new ConsumerOutcome(process.ExitCode, standardOutput, standardError);
    }

    private static string ResolveRepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string ResolveConsumerExecutable(string repositoryRoot)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "PhoenixInspect.Headless.ReferenceConsumer.exe"
            : "PhoenixInspect.Headless.ReferenceConsumer";
        return Path.Combine(
            repositoryRoot,
            "src",
            "PhoenixInspect.Headless.ReferenceConsumer",
            "bin",
            configuration,
            targetFramework,
            fileName);
    }

    private static string FirstBoundary(DumpExpressionV2OutcomeAxes axes)
    {
        foreach (var axis in AxisNames)
        {
            var outcome = W8CorpusIncident.AxisText(axes, axis);
            if (SuccessfulAxisOutcomes.Contains(outcome, StringComparer.Ordinal) || outcome == "Admitted")
            {
                continue;
            }

            return (axis, outcome) switch
            {
                ("context", "Conflict") => "context-conflict",
                ("context", "Partial") => "context-partial",
                ("context", "Unavailable") => "context-unavailable",
                ("lexicalCompleteness", "Shadowed") => "lexical-shadowed",
                ("memberLookup", "HiddenByUnsupportedMember") => "member-hidden-by-unsupported-member",
                ("runtimeConstruction", "Absent") => "runtime-construction-absent",
                ("suffix", "Blocked") => "suffix-blocked",
                ("typeBinding", "Absent") => "type-binding-absent",
                ("typeBinding", "Ambiguous") => "type-binding-ambiguous",
                _ => throw new InvalidOperationException(
                    $"The produced first boundary {axis}/{outcome} has no deterministic W8.9 spelling."),
            };
        }

        return "none";
    }

    private static string Digest(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static void AssertEvidenceInput(
        JsonElement input,
        string role,
        string path,
        string corpusId,
        string sha256)
    {
        Assert.Equal(role, W8CorpusManifest.RequiredString(input, "role"));
        Assert.Equal(path, W8CorpusManifest.RequiredString(input, "path"));
        Assert.Equal(corpusId, W8CorpusManifest.RequiredString(input, "corpusId"));
        Assert.Equal(sha256, W8CorpusManifest.RequiredString(input, "sha256"));
    }

    private sealed record EffectiveRow(
        W8CorpusIncident Incident,
        DumpExpressionV2OutcomeAxes Axes,
        string FirstBoundary,
        bool Useful,
        bool DecisionChanging,
        bool Attributable);

    private sealed record ConsumerOutcome(int ExitCode, string StandardOutput, string StandardError);
}

/// <summary>One proposed disposition of a v1 counterfactual claim, pending owner approval.</summary>
internal sealed record W8ProposedCounterfactualDisposition(
    string Id,
    int Ordinal,
    string Disposition,
    bool Useful,
    bool DecisionChanging,
    string Reason);

/// <summary>One recomputed successor-category tally.</summary>
internal sealed record DerivedCategory(
    string Category,
    int IncidentCount,
    int ApplicationShapeCount,
    int UsefulCount,
    int DecisionChangingCount,
    int AttributableEvidenceCount,
    int FirstBoundaryCount)
{
    internal string SubstantiveKey =>
        $"{IncidentCount}:{ApplicationShapeCount}:{DecisionChangingCount}:{AttributableEvidenceCount}";
}

/// <summary>Loads the content-addressed W8.9 portfolio decision candidate.</summary>
internal sealed class W8PortfolioDecisionCandidate : IDisposable
{
    private const string RelativePath = "tests/corpus/w8-static-field-portfolio-decision-candidate-v1.json";
    private readonly JsonDocument document;

    private W8PortfolioDecisionCandidate(JsonDocument document, string sha256)
    {
        this.document = document;
        Sha256 = sha256;
        var root = document.RootElement;
        ProposedDispositions =
        [
            .. root.GetProperty("proposedCounterfactualDispositions").EnumerateArray().Select(static item =>
                new W8ProposedCounterfactualDisposition(
                    W8CorpusManifest.RequiredString(item, "id"),
                    item.GetProperty("ordinal").GetInt32(),
                    W8CorpusManifest.RequiredString(item, "disposition"),
                    item.GetProperty("useful").GetBoolean(),
                    item.GetProperty("decisionChanging").GetBoolean(),
                    W8CorpusManifest.RequiredString(item, "reason"))),
        ];
        Categories =
        [
            .. root.GetProperty("candidateCategoryMetrics").EnumerateArray().Select(static item =>
                new DerivedCategory(
                    W8CorpusManifest.RequiredString(item, "category"),
                    item.GetProperty("incidentCount").GetInt32(),
                    item.GetProperty("applicationShapeCount").GetInt32(),
                    item.GetProperty("usefulCount").GetInt32(),
                    item.GetProperty("decisionChangingCount").GetInt32(),
                    item.GetProperty("attributableEvidenceCount").GetInt32(),
                    item.GetProperty("firstBoundaryCount").GetInt32())),
        ];
        foreach (var (record, element) in Categories.Zip(root.GetProperty("candidateCategoryMetrics").EnumerateArray()))
        {
            Assert.Equal(record.SubstantiveKey, W8CorpusManifest.RequiredString(element, "substantiveKey"));
        }
    }

    internal JsonElement Root => document.RootElement;

    internal string Sha256 { get; }

    internal ImmutableArray<W8ProposedCounterfactualDisposition> ProposedDispositions { get; }

    internal ImmutableArray<DerivedCategory> Categories { get; }

    internal static W8PortfolioDecisionCandidate Load()
    {
        var path = W8ShapeTargetPaths.RepositoryPath(RelativePath);
        var bytes = File.ReadAllBytes(path);
        return new W8PortfolioDecisionCandidate(
            JsonDocument.Parse(bytes),
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    internal static string ComputeSha256(string relativePath)
    {
        var bytes = File.ReadAllBytes(W8ShapeTargetPaths.RepositoryPath(relativePath));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public void Dispose() => document.Dispose();
}
