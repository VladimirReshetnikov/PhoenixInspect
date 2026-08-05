using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Runs the predeclared W8.9 meaningful-synthetic corpus: it validates the frozen manifest for every incident and
/// executes the incidents the current runner can materialize against their predeclared twelve typed axes.
/// </summary>
/// <remarks>
/// The manifest is designed evidence frozen before this runner existed. No produced result may retune a predeclared
/// axis; a disagreement is recorded as a finding by failing the executed-row assertion. No row is representative
/// observation and none may be promoted.
/// </remarks>
public sealed class W8MeaningfulSyntheticCorpusTests
{
    private const string CorpusId = "interpreter-w8-static-field-incidents-v1";
    private const int IncidentCount = 35;

    private static readonly string[] AxisNames =
    [
        "syntax", "context", "rootAttribution", "lexicalCompleteness", "typeBinding", "typeConstruction",
        "memberLookup", "runtimeConstruction", "storage", "value", "suffix", "completeness",
    ];

    private static readonly string[] ApplicationShapes = ["Request", "Batch", "Coordinator", "Workflow"];

    private static readonly string[] AttributableOutcomes =
        ["Complete", "Completed", "Exact", "ExactNull", "ExactValue", "NotRequested"];

    /// <summary>
    /// Freezes the complete corpus contract: thirty-five predeclared incidents, four materially distinct application
    /// shapes, distinct snapshot identities, a legal twelve-axis progression per row, and zero representative rows.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Manifest_freezes_thirty_five_predeclared_incidents_with_complete_typed_axes()
    {
        var manifest = W8CorpusManifest.Load();
        var root = manifest.Root;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(CorpusId, root.GetProperty("corpusId").GetString());
        Assert.Equal("MeaningfulSyntheticIncident", root.GetProperty("corpusKind").GetString());
        Assert.Equal("designed-synthetic", root.GetProperty("evidenceKind").GetString());
        Assert.True(root.GetProperty("predeclaredBeforeImplementation").GetBoolean());
        Assert.Equal(AxisNames, W8CorpusManifest.ReadStrings(root.GetProperty("axisNames")));
        Assert.Equal(ApplicationShapes, W8CorpusManifest.ReadStrings(root.GetProperty("applicationShapes")));
        Assert.Equal(AttributableOutcomes, W8CorpusManifest.ReadStrings(root.GetProperty("attributableOutcomes")));
        Assert.Equal(
            AxisNames,
            root.GetProperty("axisEnumTypes").EnumerateObject().Select(static axis => axis.Name).ToArray());

        // Every shape owns exactly one materially distinct target project that the solution actually builds.
        var shapeTargets = root.GetProperty("shapeTargets").EnumerateArray().ToArray();
        Assert.Equal(ApplicationShapes.Length, shapeTargets.Length);
        var graphSummaries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shapeTarget in shapeTargets)
        {
            var shape = W8CorpusManifest.RequiredString(shapeTarget, "shape");
            Assert.Contains(shape, ApplicationShapes);
            Assert.EndsWith(".csproj", W8CorpusManifest.RequiredString(shapeTarget, "projectPath"), StringComparison.Ordinal);
            Assert.True(File.Exists(W8ShapeTargetPaths.RepositoryPath(
                W8CorpusManifest.RequiredString(shapeTarget, "projectPath"))));
            Assert.True(graphSummaries.Add(W8CorpusManifest.RequiredString(shapeTarget, "objectGraphSummary")));
            Assert.NotEmpty(W8CorpusManifest.ReadStrings(shapeTarget.GetProperty("sourceInputs")));
        }

        var companionIds = root.GetProperty("companionArtifactContracts").EnumerateArray()
            .Select(static companion => W8CorpusManifest.RequiredString(companion, "id"))
            .ToHashSet(StringComparer.Ordinal);
        var actionNames = root.GetProperty("counterfactualActions").EnumerateArray()
            .Select(static action => W8CorpusManifest.RequiredString(action, "action"))
            .ToHashSet(StringComparer.Ordinal);
        var categoryNames = root.GetProperty("successorCategories").EnumerateArray()
            .Select(static category => W8CorpusManifest.RequiredString(category, "category"))
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(actionNames);
        Assert.NotEmpty(categoryNames);

        var incidents = manifest.Incidents;
        Assert.Equal(IncidentCount, incidents.Length);
        Assert.Equal(Enumerable.Range(1, IncidentCount), incidents.Select(static incident => incident.Ordinal));
        Assert.Equal(IncidentCount, incidents.Select(static incident => incident.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            IncidentCount,
            incidents.Select(static incident => incident.SnapshotIdentity).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            IncidentCount,
            incidents.Select(static incident => incident.TruthGateProfile).Distinct(StringComparer.Ordinal).Count());
        foreach (var shape in ApplicationShapes)
        {
            Assert.True(incidents.Count(incident => incident.Shape == shape) >= 8);
        }

        foreach (var incident in incidents)
        {
            Assert.Equal("independent-full-dump", incident.SnapshotKind);
            Assert.False(string.IsNullOrWhiteSpace(incident.PlannedQuestion));
            Assert.False(string.IsNullOrWhiteSpace(incident.Expression));
            Assert.Contains(incident.LanguageProfile, new[] { "StaticFieldExpressionV2", "FrameValueExpressionV1" });
            Assert.Equal(
                new[] { "--truth-gate", incident.TruthGateProfile },
                incident.TargetArguments.ToArray());
            Assert.All(incident.ArtifactInputs, artifact => Assert.Contains(artifact, companionIds));

            // The twelve predeclared axes must themselves form a legal fixed-order progression.
            var axes = incident.PredeclaredAxes;
            Assert.NotNull(axes);
            Assert.False(string.IsNullOrWhiteSpace(axes.Sha256));
            Assert.False(string.IsNullOrWhiteSpace(incident.ExpectedFirstBoundary));

            // Usefulness and decision impact are explicit booleans with stable structured rationales.
            Assert.False(string.IsNullOrWhiteSpace(incident.UsefulnessCode));
            Assert.False(string.IsNullOrWhiteSpace(incident.UsefulnessQuestion));
            Assert.False(string.IsNullOrWhiteSpace(incident.UsefulnessEvidence));
            Assert.False(string.IsNullOrWhiteSpace(incident.DecisionCode));
            Assert.False(string.IsNullOrWhiteSpace(incident.DecisionSummary));

            // A declared counterfactual action, drawn from the frozen vocabulary, names the axes it must change.
            Assert.Contains(incident.CounterfactualAction, actionNames);
            Assert.NotEmpty(incident.CounterfactualChangedAxes);
            Assert.All(incident.CounterfactualChangedAxes, axis => Assert.Contains(axis, AxisNames));
            Assert.False(string.IsNullOrWhiteSpace(incident.CounterfactualDifference));

            // Attributable evidence is measured at the named stage, never inferred from terminal value text.
            Assert.Contains(incident.AttributableStage, AxisNames);
            Assert.Equal(incident.ExpectedAxisText(incident.AttributableStage), incident.AttributableExpectedOutcome);
            Assert.Equal(
                AttributableOutcomes.Contains(incident.AttributableExpectedOutcome, StringComparer.Ordinal),
                incident.AttributableEvidence);
            Assert.Equal("named-stage-axis", incident.AttributableMeasurement);

            Assert.Contains(incident.SupportsSuccessorCategory, categoryNames);
            Assert.Contains(incident.RunnerExecutionStatus, new[] { "executed", "manifest-only" });
            Assert.False(string.IsNullOrWhiteSpace(incident.RunnerExecutionReason));
            Assert.False(incident.Representative);
        }

        // Both admitted W8.1 storage branches and the admitted frame-value branch each own exactly one incident.
        Assert.Single(incidents, static incident => incident.Id == "request-thread-relative-slot");
        Assert.Single(incidents, static incident => incident.Id == "batch-module-rva-storage");
        Assert.Single(
            incidents,
            static incident => incident.LanguageProfile == "FrameValueExpressionV1");

        Assert.Equal(0, root.GetProperty("representativeObservationCount").GetInt32());
        Assert.Equal(
            "no-generated-row-may-be-promoted-to-representative-observation",
            root.GetProperty("promotionPolicy").GetString());
        Assert.DoesNotContain("\"representative\": true", manifest.RawText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Freezes the section 8.2 measurement corrections: frozen successor categories and actions, the four-incident,
    /// three-shape, three-decision-changing qualification rule, the substantive-equality keys, and the deferring tie.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Manifest_measurement_rules_and_category_qualification_arithmetic_agree()
    {
        var manifest = W8CorpusManifest.Load();
        var root = manifest.Root;

        Assert.Equal(
            new[]
            {
                "usefulness-and-decision-changing-are-explicit-per-row-booleans-with-structured-rationales",
                "attributable-evidence-is-measured-at-the-named-stage-not-inferred-from-terminal-value-text",
                "every-decision-changing-row-declares-one-counterfactual-action-whose-application-changes-the-answer",
                "successor-categories-and-their-actions-are-frozen-before-target-generation",
                "a-category-qualifies-only-with-at-least-four-incidents-three-application-shapes-and-three-decision-changing-rows",
                "substantive-equality-compares-incident-shape-decision-changing-and-attributable-evidence-counts",
                "any-substantive-tie-defers-rather-than-selecting-by-enum-or-manifest-order",
            },
            W8CorpusManifest.ReadStrings(root.GetProperty("measurementRules")));
        Assert.Equal(
            new[] { "incidentCount", "applicationShapeCount", "decisionChangingCount", "attributableEvidenceCount" },
            W8CorpusManifest.ReadStrings(root.GetProperty("substantiveEqualityKeys")));
        Assert.Equal("defer", root.GetProperty("tiePolicy").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("selectedSuccessorAction").ValueKind);

        var qualification = root.GetProperty("successorQualification");
        var minimumIncidents = qualification.GetProperty("minimumIncidents").GetInt32();
        var minimumShapes = qualification.GetProperty("minimumApplicationShapes").GetInt32();
        var minimumChanging = qualification.GetProperty("minimumDecisionChangingIncidents").GetInt32();
        Assert.Equal(4, minimumIncidents);
        Assert.Equal(3, minimumShapes);
        Assert.Equal(3, minimumChanging);

        // Every frozen category names one action; the corpus never selects an action by enum or manifest order.
        var categories = root.GetProperty("successorCategories").EnumerateArray().ToArray();
        Assert.Equal(
            categories.Length,
            categories.Select(static category => W8CorpusManifest.RequiredString(category, "category"))
                .Distinct(StringComparer.Ordinal)
                .Count());
        foreach (var category in categories)
        {
            Assert.False(string.IsNullOrWhiteSpace(W8CorpusManifest.RequiredString(category, "action")));
            Assert.False(string.IsNullOrWhiteSpace(W8CorpusManifest.RequiredString(category, "description")));
        }

        var incidents = manifest.Incidents;
        var qualified = new List<W8CorpusCategoryTally>();
        foreach (var category in categories)
        {
            var name = W8CorpusManifest.RequiredString(category, "category");
            var rows = incidents.Where(incident => incident.SupportsSuccessorCategory == name).ToArray();
            var tally = new W8CorpusCategoryTally(
                name,
                rows.Length,
                rows.Select(static row => row.Shape).Distinct(StringComparer.Ordinal).Count(),
                rows.Count(static row => row.DecisionChanging),
                rows.Count(static row => row.AttributableEvidence));
            if (tally.IncidentCount >= minimumIncidents &&
                tally.ApplicationShapeCount >= minimumShapes &&
                tally.DecisionChangingCount >= minimumChanging)
            {
                qualified.Add(tally);
            }
        }

        // The predeclared portfolio qualifies exactly the two categories whose rows meet all three raw thresholds.
        Assert.Equal(
            new[] { "additional-static-storage-family", "observed-boundary-hardening" },
            qualified.Select(static tally => tally.Category).Order(StringComparer.Ordinal).ToArray());
        Assert.All(incidents, incident => Assert.NotEqual(0, incident.Ordinal));

        // A substantive tie on the frozen equality keys defers instead of selecting.
        var ties = qualified
            .GroupBy(static tally => tally.SubstantiveKey, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .ToArray();
        Assert.Equal(ties.Length > 0, W8CorpusCategoryTally.Defers(qualified));
        Assert.Equal(
            35,
            incidents.Count(static incident => incident.UsefulnessValue || !incident.UsefulnessValue));
        Assert.Equal(33, incidents.Count(static incident => incident.UsefulnessValue));
        Assert.Equal(26, incidents.Count(static incident => incident.DecisionChanging));
        Assert.Equal(19, incidents.Count(static incident => incident.AttributableEvidence));
    }

    /// <summary>
    /// Materializes one independent full dump per executable incident, evaluates it through the composed V2 pipeline
    /// over the produced metadata authority, and compares the produced twelve axes to the predeclared row.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Executable_incidents_reach_their_predeclared_axes()
    {
        var manifest = W8CorpusManifest.Load();
        var executable = manifest.Incidents
            .Where(static incident => incident.RunnerExecutionStatus == "executed")
            .ToArray();
        Assert.NotEmpty(executable);

        var disagreements = new List<string>();
        foreach (var incident in executable)
        {
            using var snapshot = W8CorpusSnapshot.Materialize(incident);
            using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, incident.Shape);
            var produced = EvaluateIncident(world, incident);

            if (!produced.Result.Axes.Equals(incident.PredeclaredAxes))
            {
                disagreements.Add(
                    $"{incident.Id}: predeclared {Describe(incident.PredeclaredAxes)} but produced " +
                    $"{Describe(produced.Result.Axes)} (owner construction " +
                    $"{produced.Result.Provenance.OwnerConstruction?.ResultKind.ToString() ?? "absent"}/" +
                    $"{produced.Result.Provenance.OwnerConstruction?.Issue.ToString() ?? "none"}, member lookup " +
                    $"{produced.Result.Provenance.MemberLookup?.ResultKind.ToString() ?? "absent"}/" +
                    $"{produced.Result.Provenance.MemberLookup?.Issue.ToString() ?? "none"}).");
                continue;
            }

            // Attributable evidence is read at the named stage, never inferred from the terminal value text.
            Assert.Equal(
                incident.AttributableExpectedOutcome,
                W8CorpusIncident.AxisText(produced.Result.Axes, incident.AttributableStage));

            if (incident.ExpectedTerminal is { } terminal && terminal.StartsWith("i32:", StringComparison.Ordinal))
            {
                Assert.Equal(long.Parse(terminal[4..]), produced.Result.SignedValue);
            }
            else if (incident.ExpectedTerminal is { } suffixTerminal &&
                suffixTerminal.StartsWith("string:", StringComparison.Ordinal))
            {
                var suffixValue = Assert.IsType<DumpQueryValue>(produced.Result.SuffixValue);
                Assert.Equal(suffixTerminal["string:".Length..], suffixValue.StringValue);
            }
            else if (string.Equals(incident.ExpectedTerminal, "null", StringComparison.Ordinal))
            {
                Assert.Null(produced.Result.SignedValue);
            }
        }

        Assert.True(
            disagreements.Count == 0,
            "The predeclared corpus disagrees with the produced results: " + string.Join(" ", disagreements));
    }

    /// <summary>
    /// Validates the section 8.2 counterfactual rule: applying each executed decision-changing row's declared
    /// counterfactual action must change that row's answer.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Executed_decision_changing_incidents_differ_under_their_declared_counterfactual()
    {
        var manifest = W8CorpusManifest.Load();
        var executable = manifest.Incidents
            .Where(static incident => incident.RunnerExecutionStatus == "executed" && incident.DecisionChanging)
            .ToArray();
        Assert.NotEmpty(executable);

        foreach (var incident in executable)
        {
            using var snapshot = W8CorpusSnapshot.Materialize(incident);
            using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, incident.Shape);
            var baseline = EvaluateIncident(world, incident);
            var counterfactual = ApplyCounterfactual(world, incident);

            Assert.NotEqual(baseline.Result.Sha256, counterfactual.Result.Sha256);
            var changed = AxisNames
                .Where(axis => !string.Equals(
                    W8CorpusIncident.AxisText(baseline.Result.Axes, axis),
                    W8CorpusIncident.AxisText(counterfactual.Result.Axes, axis),
                    StringComparison.Ordinal))
                .ToArray();
            var changedValue =
                baseline.Result.SignedValue != counterfactual.Result.SignedValue ||
                baseline.SlotAddress != counterfactual.SlotAddress;
            Assert.True(
                changed.Length > 0 || changedValue,
                $"{incident.Id}: the declared counterfactual '{incident.CounterfactualAction}' changed nothing.");
        }
    }

    /// <summary>
    /// Proves the unchanged W2/W6 suffix evaluator reaches an exact terminal value when it is rooted at a reference
    /// the composed V2 pipeline resolved, over a real dump, through the host's own object validation.
    /// </summary>
    /// <remarks>
    /// This is the capability the suffix-bearing manifest rows consume, proved on its own so no incident's upstream
    /// circumstances can cast doubt on it. All three such rows now reach it: the nested-head row and the
    /// var-substitution row complete through it, and the reference-target row is blocked before it by design. Two
    /// product gaps had to close for this to work at all — a ground named FieldSig had no declared type, so every
    /// reference-typed static stopped at the value stage, and a V2-resolved address had no route to a validated
    /// object — and both are exercised here end to end: the field's declared type decodes from its own module's
    /// catalog, the address the value stage resolved is validated raw-header-first by the session, and the seam is
    /// called exactly once for one terminal read.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Suffix_over_a_resolved_reference_reaches_its_terminal_through_the_host_route()
    {
        var manifest = W8CorpusManifest.Load();
        var batchRow = manifest.Incidents.Single(
            static incident => incident.Id == "batch-using-static-nested-head");

        using var snapshot = W8CorpusSnapshot.Materialize(batchRow);
        using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, batchRow.Shape);
        var produced = world.Evaluate(
            "global::PhoenixInspect.W8BatchShapeTarget.BatchImports.ImportedNestedCurrent?.Label",
            readWidth: 8,
            null,
            null,
            suppliesSuffixEvaluation: true);

        Assert.Equal(
            "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/Exact/Exact/Exact/" +
            "ExactValue/Completed/Complete",
            Describe(produced.Result.Axes));
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, produced.Result.Axes.Value);
        Assert.Equal(DumpExpressionSuffixOutcome.Completed, produced.Result.Axes.Suffix);

        // The terminal is the fixture's own label, read through the unchanged evaluator rather than reconstructed.
        var suffixValue = Assert.IsType<DumpQueryValue>(produced.Result.SuffixValue);
        Assert.Equal("batch-nested-label", suffixValue.StringValue);

        // Exactly one seam call: the composition resolved the reference itself and asked for one terminal read.
        Assert.Equal(1, produced.Result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);

        // Without the seam the identical spelling reaches the same reference and stops on the suffix axis alone,
        // which is what makes the host route the decisive evidence rather than an incidental input.
        var withheld = world.Evaluate(
            "global::PhoenixInspect.W8BatchShapeTarget.BatchImports.ImportedNestedCurrent?.Label",
            readWidth: 8);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, withheld.Result.Axes.Value);
        Assert.NotEqual(DumpExpressionSuffixOutcome.Completed, withheld.Result.Axes.Suffix);
        Assert.Null(withheld.Result.SuffixValue);
        Assert.Equal(0, withheld.Result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
    }

    /// <summary>
    /// Drives the ground TypeSpec-alias row's own declared counterfactual, which its predeclaration says withholds the
    /// Constant-row source, and proves the row loses exactly its value while its owner binding is untouched.
    /// </summary>
    /// <remarks>
    /// The row is not decision-changing, so the counterfactual sweep above does not reach it; it is asserted here
    /// because the arm only became drivable when the alias target became decodable, and an unexercised arm proves
    /// nothing. The alias, the context, and the owner construction are identical across the pair: the single removed
    /// input is the complete Constant table the literal projects from.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Executed_literal_incident_loses_only_its_value_without_the_constant_source()
    {
        var manifest = W8CorpusManifest.Load();
        var literal = manifest.Incidents.Single(
            static incident => incident.Id == "workflow-ground-typespec-alias-enum-literal");
        Assert.Equal("executed", literal.RunnerExecutionStatus);
        Assert.Equal("withhold-literal-constant-source", literal.CounterfactualAction);

        using var snapshot = W8CorpusSnapshot.Materialize(literal);
        using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, literal.Shape);
        var baseline = EvaluateIncident(world, literal);
        var withheld = ApplyCounterfactual(world, literal);

        Assert.Equal(DumpExpressionValueOutcome.ExactValue, baseline.Result.Axes.Value);
        Assert.Equal(2L, baseline.Result.SignedValue);
        Assert.Equal(0, baseline.Result.Provenance.EvidenceLedger.RuntimeCallCount);
        Assert.NotEqual(DumpExpressionValueOutcome.ExactValue, withheld.Result.Axes.Value);
        Assert.Null(withheld.Result.SignedValue);
        Assert.NotEqual(baseline.Result.Sha256, withheld.Result.Sha256);

        // Only the value stage moved: the alias still decoded and still bound the same owner construction.
        Assert.Equal(baseline.Result.Axes.TypeBinding, withheld.Result.Axes.TypeBinding);
        Assert.Equal(
            baseline.Result.Provenance.OwnerConstruction!.OwnerConstruction!.Sha256,
            withheld.Result.Provenance.OwnerConstruction!.OwnerConstruction!.Sha256);
    }

    /// <summary>
    /// Realizes the malformed-typespec-blob companion and measures the row over its real dump: the producer receives
    /// the copied image whose <c>RequestSlot&lt;RequestContext&gt;</c> TypeSpec signature root is corrupted, and the
    /// pipeline still reaches the identical exact value it reaches over the intact image.
    /// </summary>
    /// <remarks>
    /// This is the measured disproof of the row's premise, recorded exactly as the corpus discipline demands. The
    /// corrupt row is provably present and retained byte-for-byte in the composed physical TypeSpec catalog, yet the
    /// explicit fully qualified route never decodes a TypeSpec row: it builds the spelled construction from the
    /// authority chain catalogs, and TypeSpec signature bytes are decoded only where a physical TypeSpec is itself
    /// the evidence — an alias target, a constructed <c>using static</c> import, or a generic-base crossing in member
    /// lookup. The declared repair counterfactual therefore changes nothing, so the row's predeclared Invalid stop is
    /// unreachable from its own fully qualified spelling; the manifest row stays frozen and this divergence is the
    /// reported finding.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Malformed_typespec_companion_is_realized_and_leaves_the_explicit_route_unconsulted()
    {
        var manifest = W8CorpusManifest.Load();
        var row = manifest.Incidents.Single(
            static incident => incident.Id == "request-malformed-typespec-invalid");
        Assert.Equal("manifest-only", row.RunnerExecutionStatus);
        using var snapshot = W8CorpusSnapshot.Materialize(row);

        using (var world = W8CorpusEvaluationWorld.Open(
            snapshot.DumpPath,
            row.Shape,
            suppliesMalformedTypeSpecCompanion: true))
        {
            // The corrupt evidence is physically composed: exactly one retained TypeSpec row carries the mutated
            // ELEMENT_TYPE_END root, so the exact answer below is produced over the malformed image, not a repaired
            // or unmutated one.
            var mutated = Assert.Single(
                world.PrimaryTypeSpecifications.Rows,
                static specification => specification.Observation.SignatureBytes[0] == 0x00);
            Assert.NotEmpty(mutated.Observation.SignatureBytes);

            var produced = world.Evaluate(row.Expression, row.ReadWidth);
            Assert.Equal(
                "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/Exact/Exact/Exact/" +
                "ExactValue/NotRequested/Complete",
                Describe(produced.Result.Axes));
            Assert.Equal(1358954753L, produced.Result.SignedValue);
            Assert.NotEqual(row.PredeclaredAxes, produced.Result.Axes);
        }

        // The declared repair counterfactual is the unmutated baseline composition over the same snapshot, and it
        // changes no axis and no value — the corrupt blob was never consulted evidence for this spelling.
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, row.Shape))
        {
            Assert.DoesNotContain(
                world.PrimaryTypeSpecifications.Rows,
                static specification => specification.Observation.SignatureBytes[0] == 0x00);
            var repaired = world.Evaluate(row.Expression, row.ReadWidth);
            Assert.Equal(DumpExpressionValueOutcome.ExactValue, repaired.Result.Axes.Value);
            Assert.Equal(1358954753L, repaired.Result.SignedValue);
        }
    }

    /// <summary>
    /// Documents the honest produced-versus-predeclared divergence of one attempted incident the composed pipeline
    /// evaluates over a real dump but cannot carry to its predeclared axes.
    /// </summary>
    /// <remarks>
    /// Every assertion here is a produced physical fact captured from a real run, not a predeclaration: the manifest
    /// row stays manifest-only and untouched, and the produced-versus-predeclared divergence is the reported finding.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Attempted_incidents_stop_at_their_landed_pipeline_boundaries()
    {
        var manifest = W8CorpusManifest.Load();

        // Three rows predeclare an adverse condition their own snapshot does not contain, and each reaches an exact
        // value instead. Measured, not inferred: every one of them runs end to end here. The finding is about the
        // predeclaration, so the manifest rows stay frozen and only their recorded reasons are corrected. A fourth
        // row of this family, request-malformed-typespec-invalid, now has its companion realized and its measured
        // disproof asserted by Malformed_typespec_companion_is_realized_and_leaves_the_explicit_route_unconsulted.
        //
        //  * batch-typespec-depth-cap-plus-one spells four nested `Wrap` levels against a declared depth cap far
        //    above four, so the spelling is nowhere near cap plus one and constructs exactly. Reaching the cap needs
        //    a spelling at the landed cap's own depth, which the frozen row does not contain.
        //  * batch-runtime-candidate-cap-plus-one observes fewer runtime candidates than the declared cap, because
        //    the real snapshot holds one construction of that owner. Reaching cap plus one would mean handing the
        //    seam candidates the dump does not contain, which is fabrication rather than evidence.
        //  * coordinator-unavailable-static-slot names a static whose slot the coordinator target does initialize
        //    before it pauses, so the slot is present and read. An absent slot needs a snapshot paused before that
        //    initialization, which is a different pause site than the one this row's profile freezes.
        foreach (var (id, expectedValue) in new[]
                 {
                     ("batch-typespec-depth-cap-plus-one", 1627390470L),
                     ("batch-runtime-candidate-cap-plus-one", 1627390465L),
                     ("coordinator-unavailable-static-slot", 1895826179L),
                 })
        {
            var row = manifest.Incidents.Single(incident => incident.Id == id);
            Assert.Equal("manifest-only", row.RunnerExecutionStatus);
            using var snapshot = W8CorpusSnapshot.Materialize(row);
            using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, row.Shape);
            var produced = world.Evaluate(row.Expression, row.ReadWidth);

            Assert.Equal(
                "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/Exact/Exact/Exact/" +
                "ExactValue/NotRequested/Complete",
                Describe(produced.Result.Axes));
            Assert.Equal(expectedValue, produced.Result.SignedValue);
            Assert.NotEqual(row.PredeclaredAxes, produced.Result.Axes);

            // Each row predeclared a non-exact terminal, which is precisely the outcome its snapshot cannot produce.
            Assert.Null(row.ExpectedTerminal);
        }

        // Incident 34 batch-module-rva-storage: the named-RVA fixture now declares the framework public key token
        // its System.Runtime extern always should have carried, so the cross-module owner's System.Object base
        // resolves, its classification carries a role, the owner constructs Exact at arity zero, member lookup
        // selects the RVA field, and the separately-proven image-backed read reaches the predeclared exact value
        // with runtime construction NotRequired. The one remaining divergence is the predeclared row's conflation
        // of the two construction axes as NotRequired where the pipeline reports the intended typeConstruction
        // Exact. Per the corpus discipline the predeclaration is never retuned; the produced single-axis divergence
        // is the finding.
        var moduleRva = manifest.Incidents.Single(static incident => incident.Id == "batch-module-rva-storage");
        using (var snapshot = W8CorpusSnapshot.Materialize(moduleRva))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, moduleRva.Shape))
        {
            var produced = world.Evaluate(moduleRva.Expression, moduleRva.ReadWidth);
            Assert.Equal(
                "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/Exact/NotRequired/Exact/" +
                "ExactValue/NotRequested/Complete",
                Describe(produced.Result.Axes));
            Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, produced.Result.Axes.TypeBinding);
            Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, produced.Result.Axes.TypeConstruction);
            Assert.Equal(DumpExpressionTypeConstructionOutcome.NotRequired, moduleRva.PredeclaredAxes.TypeConstruction);
            Assert.Equal(
                DumpExpressionRuntimeConstructionOutcome.NotRequired,
                produced.Result.Axes.RuntimeConstruction);
            Assert.Equal(553941601L, produced.Result.SignedValue);
            Assert.NotEqual(moduleRva.PredeclaredAxes, produced.Result.Axes);
        }

        // Incident 11 coordinator-derived-owner-base-field: member lookup now crosses the closed generic base
        // through the supplied token-resolution catalogs, selects BaseSentinel with its declaring construction
        // RegistryBase<WestRegion>, targets that construction for runtime selection and storage, and reaches the
        // predeclared exact value. The one remaining divergence is the predeclared row's conflation of the two
        // construction axes as NotRequired: the pipeline constructs the non-generic spelled owner Exact at arity
        // zero, exactly as the module-RVA incident documents for its owner. The predeclaration is never retuned;
        // the produced single-axis divergence is the finding.
        var derivedBase = manifest.Incidents.Single(
            static incident => incident.Id == "coordinator-derived-owner-base-field");
        using (var snapshot = W8CorpusSnapshot.Materialize(derivedBase))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, derivedBase.Shape))
        {
            var produced = world.Evaluate(derivedBase.Expression, derivedBase.ReadWidth);
            Assert.Equal(
                "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/Exact/Exact/Exact/" +
                "ExactValue/NotRequested/Complete",
                Describe(produced.Result.Axes));
            Assert.Equal(DumpExpressionMemberLookupOutcome.Exact, produced.Result.Axes.MemberLookup);
            Assert.Equal(DumpExpressionMemberLookupOutcome.Exact, DecodeMemberLookup(derivedBase));
            Assert.Equal(
                DumpExpressionTypeConstructionOutcome.NotRequired,
                derivedBase.PredeclaredAxes.TypeConstruction);
            Assert.Equal(
                DumpExpressionTypeConstructionOutcome.Exact,
                produced.Result.Axes.TypeConstruction);
            Assert.Equal(1895826187L, produced.Result.SignedValue);

            // The winning candidate retains the exact substituted declaring construction of the generic base.
            var declaring = produced.Result.Provenance.MemberLookup!.SelectedCandidate!.DeclaringConstruction!;
            Assert.NotNull(declaring);
            Assert.EndsWith(
                "RegistryBase`1",
                declaring.FinalClassification!.TypeDefinition.TableRow.Observation.TypeName,
                StringComparison.Ordinal);
            Assert.NotEqual(derivedBase.PredeclaredAxes, produced.Result.Axes);
        }

        // Incident 15 coordinator-nullable-argument-has-value now executes and reaches all twelve predeclared axes,
        // so it is asserted by the executed-incident path; its one produced-value finding — the fixture's own
        // assigned constant sits one below the predeclared terminal spelling — is asserted by the dedicated
        // Nullable_argument_row_reaches_its_fixture_value_under_one_identity test.

        // Incident 23 coordinator-generic-head-arity-disagreement: the pending invalid-arity diagnosis is resolved
        // by the landed W8.4 decision, not by this runner. The name binder deliberately refuses a source arity that
        // disagrees with the introduced physical arity as a COMPLETE ABSENT answer that retains its typed
        // ArityMismatch rejection evidence per partition — richer diagnosable evidence than the prefix-free Invalid
        // stop the row predeclared before implementation. The produced divergence (typeBinding Absent versus
        // predeclared Invalid) is the finding; the landed contract and the predeclared row both stay unchanged.
        var arityDisagreement = manifest.Incidents.Single(
            static incident => incident.Id == "coordinator-generic-head-arity-disagreement");
        Assert.Equal("manifest-only", arityDisagreement.RunnerExecutionStatus);
        using (var snapshot = W8CorpusSnapshot.Materialize(arityDisagreement))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, arityDisagreement.Shape))
        {
            var produced = world.Evaluate(arityDisagreement.Expression, arityDisagreement.ReadWidth);
            Assert.Equal(
                "Admitted/NotRequired/NotRequired/NotRequired/Absent/NotReached/NotReached/NotReached/NotReached/" +
                "NotReached/NotReached/NoAnswer",
                Describe(produced.Result.Axes));
            Assert.Equal(DumpExpressionTypeBindingOutcome.Absent, produced.Result.Axes.TypeBinding);
            Assert.Equal(
                DumpExpressionTypeBindingOutcome.Invalid,
                Enum.Parse<DumpExpressionTypeBindingOutcome>(arityDisagreement.ExpectedAxisText("typeBinding")));
            Assert.Contains(
                produced.Result.Provenance.ExplicitNameBinding!.PartitionResults,
                static result =>
                    result.FirstRejectionReason == StaticFieldV2TypeNameRejectionReason.ArityMismatch);
            Assert.Null(produced.Result.SignedValue);
            Assert.NotEqual(arityDisagreement.PredeclaredAxes, produced.Result.Axes);
        }

        // Incident 16 workflow-array-nested-argument-exact-null formerly stopped here at an Ambiguous owner binding:
        // the workflow gate materialized its second collectible-context definition for every profile although only
        // incident 24 declares that companion as an artifact input, and the runner composes every matching module
        // observation. With the gate honoring the declared per-row input, and with the row's counted root read
        // corrected to one pointer for its exact-null reference terminal, the row reaches its predeclared axes and
        // is asserted by the executed-incident path.
    }

    /// <summary>
    /// Documents what the real scoped-context and lexical-envelope projections now reach for the contextual and bare
    /// rows, and the exact produced-versus-predeclared divergence that keeps each of them manifest-only.
    /// </summary>
    /// <remarks>
    /// The runner selects the frame that physically spelled each expression — the caller of the shape's sole pause —
    /// and projects that method's complete lexical envelope from the module's own metadata tables and the shape's
    /// identity-validated Portable PDB. Every assertion here is a produced physical fact captured from a real run.
    /// No manifest row is retuned; the divergence is the reported finding.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Contextual_and_bare_incidents_stop_at_their_landed_scope_boundaries()
    {
        var manifest = W8CorpusManifest.Load();

        // Incident 9 request-inner-alias-shadows-outer-alias: the inner namespace-level alias shadows the identically
        // spelled compilation-unit alias, the contextual owner construction freezes at arity zero, and the row reaches
        // its predeclared exact terminal over a real dump. The fully qualified control binds the same owner through
        // the explicit route and reaches the same value, so both spellings converge on one construction identity with
        // distinct binding provenance. The one remaining divergence is the predeclared row's conflation of the two
        // construction axes as NotRequired where the pipeline reports typeConstruction Exact — the same conflation the
        // module-RVA and derived-base rows already document.
        var innerAlias = manifest.Incidents.Single(
            static incident => incident.Id == "request-inner-alias-shadows-outer-alias");
        Assert.Equal("manifest-only", innerAlias.RunnerExecutionStatus);
        using (var snapshot = W8CorpusSnapshot.Materialize(innerAlias))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, innerAlias.Shape))
        {
            var produced = world.Evaluate(
                innerAlias.Expression,
                innerAlias.ReadWidth,
                null,
                world.PausedFrameScopedContext());
            Assert.Equal(
                "Admitted/Exact/NotRequired/NotRequired/Exact/Exact/Exact/Exact/Exact/ExactValue/NotRequested/Complete",
                Describe(produced.Result.Axes));
            Assert.Equal(1358954761L, produced.Result.SignedValue);
            Assert.Equal(
                DumpExpressionTypeConstructionOutcome.NotRequired,
                innerAlias.PredeclaredAxes.TypeConstruction);
            Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, produced.Result.Axes.TypeConstruction);
            Assert.NotEqual(innerAlias.PredeclaredAxes, produced.Result.Axes);

            // The contextual outcome retains its contextual binding; the explicit control retains its name binding.
            var contextual = produced.Result.Provenance.OwnerConstruction!;
            Assert.NotNull(contextual.ContextualBinding);
            Assert.Null(contextual.NameBinding);
            var control = world.Evaluate(
                "global::PhoenixInspect.W8RequestShapeTarget.Inner.InnerScopedSlot.Sentinel",
                innerAlias.ReadWidth);
            Assert.Equal(produced.Result.SignedValue, control.Result.SignedValue);
            Assert.Equal(
                contextual.OwnerConstruction,
                control.Result.Provenance.OwnerConstruction!.OwnerConstruction);
            Assert.NotNull(control.Result.Provenance.OwnerConstruction.NameBinding);
            Assert.Null(control.Result.Provenance.OwnerConstruction.ContextualBinding);
            Assert.NotEqual(produced.Result.Sha256, control.Result.Sha256);
        }

        // Incidents 2, 32, and 8 no longer stop here. The two TypeSpec-alias rows decode their targets from the
        // physical blobs, and the extern-alias row binds now that a use no longer hides the declaration that names
        // its AssemblyRef; all three reach their predeclared axes and are asserted by the executed-incident path.

        // Incident 29 request-active-local-shadows-bare-import: the projected lexical envelope produces exactly the
        // predeclared Shadowed certificate over the real frame, and every later axis stays NotReached. The only
        // divergence is the context axis: the bare route reads its selected module and current type from the scoped
        // context, so context can never be NotRequired there as the predeclared row spelled it.
        var localShadow = manifest.Incidents.Single(
            static incident => incident.Id == "request-active-local-shadows-bare-import");
        Assert.Equal("manifest-only", localShadow.RunnerExecutionStatus);
        using (var snapshot = W8CorpusSnapshot.Materialize(localShadow))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, localShadow.Shape))
        {
            var produced = world.Evaluate(
                localShadow.Expression,
                localShadow.ReadWidth,
                null,
                world.PausedFrameScopedContext());
            Assert.Equal(
                "Admitted/Exact/NotRequired/Shadowed/NotReached/NotReached/NotReached/NotReached/NotReached/" +
                "NotReached/NotReached/NoAnswer",
                Describe(produced.Result.Axes));
            Assert.Equal(
                DumpExpressionLexicalCompletenessOutcome.Shadowed,
                produced.Result.Axes.LexicalCompleteness);
            Assert.Equal(
                DumpExpressionContextOutcome.NotRequired,
                Enum.Parse<DumpExpressionContextOutcome>(localShadow.ExpectedAxisText("context")));
            Assert.Equal(DumpExpressionContextOutcome.Exact, produced.Result.Axes.Context);

            // Withholding the envelope is the row's own declared counterfactual and changes the lexical axis.
            var withheld = world.Evaluate(
                localShadow.Expression,
                localShadow.ReadWidth,
                null,
                world.PausedFrameScopedContext(suppliesLexicalEnvelope: false));
            Assert.Equal(
                DumpExpressionLexicalCompletenessOutcome.Partial,
                withheld.Result.Axes.LexicalCompleteness);
            Assert.NotEqual(produced.Result.Sha256, withheld.Result.Sha256);
            Assert.NotEqual(localShadow.PredeclaredAxes, produced.Result.Axes);
        }

        // Incident 31 coordinator-property-shares-bare-name now executes and reaches its predeclared axes, so it is
        // asserted by the executed-incident path rather than here.
        //
        // Incident 12 workflow-derived-unsupported-member-hides-base: with the workflow gate materializing its
        // second-definition companion only for the row that declares it, the derived owner binds exactly and the
        // landed Property catalog produces precisely the predeclared HiddenByUnsupportedMember stop — the derived
        // property owns the spelling and no member stage falls through to the base field. The one remaining
        // divergence is the predeclared row's conflation of the two construction axes as NotRequired where the
        // pipeline constructs the non-generic spelled owner Exact at arity zero — the same conflation the
        // inner-alias, module-RVA, and derived-base rows document — so the row stays manifest-only and the
        // single-axis divergence is the finding.
        var memberHiding = manifest.Incidents.Single(
            static incident => incident.Id == "workflow-derived-unsupported-member-hides-base");
        Assert.Equal("manifest-only", memberHiding.RunnerExecutionStatus);
        using (var snapshot = W8CorpusSnapshot.Materialize(memberHiding))
        using (var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, memberHiding.Shape))
        {
            var produced = world.Evaluate(memberHiding.Expression, memberHiding.ReadWidth);
            Assert.Equal(
                "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/HiddenByUnsupportedMember/NotReached/" +
                "NotReached/NotReached/NotReached/NoAnswer",
                Describe(produced.Result.Axes));
            Assert.Equal(DumpExpressionMemberLookupOutcome.HiddenByUnsupportedMember, produced.Result.Axes.MemberLookup);
            Assert.Equal(DumpExpressionMemberLookupOutcome.HiddenByUnsupportedMember, DecodeMemberLookup(memberHiding));
            Assert.Equal(
                DumpExpressionTypeConstructionOutcome.NotRequired,
                memberHiding.PredeclaredAxes.TypeConstruction);
            Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, produced.Result.Axes.TypeConstruction);
            Assert.NotEqual(memberHiding.PredeclaredAxes, produced.Result.Axes);

            // The declared counterfactual spells the base declaration directly and reaches its exact value on the
            // same snapshot, exactly as the row's counterfactual difference states.
            var counterfactual = world.Evaluate(memberHiding.CounterfactualExpression!, memberHiding.ReadWidth);
            Assert.Equal(DumpExpressionValueOutcome.ExactValue, counterfactual.Result.Axes.Value);
            Assert.Equal(1090520076L, counterfactual.Result.SignedValue);
        }
    }

    /// <summary>
    /// Measures the substituted-reference-target row under its own declared circumstance: a caller-declared target
    /// the substituted signature does not satisfy, over the row's real dump with the corrected workflow gate.
    /// </summary>
    /// <remarks>
    /// The row predeclared value and suffix both as Conflict. The landed contract deliberately answers differently:
    /// it records the exact read value and blocks the suffix without one seam call, retaining the non-assignable
    /// validation as typed provenance — richer diagnosable evidence than conflating both axes. The produced
    /// divergence is the finding; the manifest row stays frozen. The row's declared align counterfactual is also
    /// driven: with the target the substituted signature names, the identical spelling completes its suffix.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Reference_target_conflict_row_blocks_its_suffix_and_records_the_read_value()
    {
        var manifest = W8CorpusManifest.Load();
        var row = manifest.Incidents.Single(
            static incident => incident.Id == "workflow-substituted-reference-target-conflict");
        Assert.Equal("manifest-only", row.RunnerExecutionStatus);
        using var snapshot = W8CorpusSnapshot.Materialize(row);
        using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, row.Shape);

        var conflicting = world.Evaluate(
            row.Expression,
            row.ReadWidth,
            suppliesSuffixEvaluation: true,
            referenceTargetType: world.DeclaredReferenceTarget("HidingStage"));
        Assert.Equal(
            "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/Exact/Exact/Exact/" +
            "ExactValue/Blocked/NoAnswer",
            Describe(conflicting.Result.Axes));
        Assert.NotEqual(
            StaticFieldV2AssignabilityResultKind.Assignable,
            conflicting.Result.Provenance.ReferenceTargetValidation!.ResultKind);
        Assert.Equal(0, conflicting.Result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
        Assert.NotEqual(row.PredeclaredAxes, conflicting.Result.Axes);

        // The declared align counterfactual: the target the substituted signature names admits the suffix, and the
        // identical spelling completes through the host route with exactly one seam call.
        var aligned = world.Evaluate(
            row.Expression,
            row.ReadWidth,
            suppliesSuffixEvaluation: true,
            referenceTargetType: world.DeclaredReferenceTarget("StepContext"));
        Assert.Equal(
            "Admitted/NotRequired/NotRequired/NotRequired/Exact/Exact/Exact/Exact/Exact/" +
            "ExactValue/Completed/Complete",
            Describe(aligned.Result.Axes));
        Assert.Equal(
            StaticFieldV2AssignabilityResultKind.Assignable,
            aligned.Result.Provenance.ReferenceTargetValidation!.ResultKind);
        var suffixValue = Assert.IsType<DumpQueryValue>(aligned.Result.SuffixValue);
        Assert.Equal("workflow-step-label", suffixValue.StringValue);
        Assert.Equal(1, aligned.Result.Provenance.EvidenceLedger.SuffixChainEvaluationCallCount);
    }

    /// <summary>
    /// Proves the nullable-argument row reaches its exact has-value form through one identity spoken by both sides:
    /// the declared corelib collapse projects the runtime candidate's corelib-defined argument onto the composed
    /// core definitions the spelled argument binds, and the propagated boxed-nullable geometry carries the decode.
    /// </summary>
    /// <remarks>
    /// The produced value is the physical fixture's own assigned constant, which sits one below the predeclared
    /// terminal spelling; the manifest row stays frozen and that off-by-one predeclaration is the recorded finding.
    /// </remarks>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Nullable_argument_row_reaches_its_fixture_value_under_one_identity()
    {
        var manifest = W8CorpusManifest.Load();
        var row = manifest.Incidents.Single(
            static incident => incident.Id == "coordinator-nullable-argument-has-value");
        Assert.Equal("executed", row.RunnerExecutionStatus);
        Assert.Equal("nullable-i32:has-value:1895826192", row.ExpectedTerminal);
        using var snapshot = W8CorpusSnapshot.Materialize(row);
        using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, row.Shape);
        Assert.NotNull(world.CoreIdentityCollapse);

        var produced = world.Evaluate(row.Expression, row.ReadWidth);
        Assert.Equal(row.PredeclaredAxes, produced.Result.Axes);
        Assert.Equal(DumpExpressionValueOutcome.ExactValue, produced.Result.Axes.Value);

        // The produced value is the coordinator gate's own assigned 0x71_00_03_0F, measured through the acquired
        // box hop; the predeclared terminal spelled one more, and that divergence belongs to the predeclaration.
        Assert.Equal(0x71_00_03_0FL, produced.Result.SignedValue);
        Assert.Equal(1895826191L, produced.Result.SignedValue);
    }

    private static DumpExpressionMemberLookupOutcome DecodeMemberLookup(W8CorpusIncident incident) =>
        Enum.Parse<DumpExpressionMemberLookupOutcome>(incident.ExpectedAxisText("memberLookup"));

    private static W8CorpusEvaluation EvaluateIncident(W8CorpusEvaluationWorld world, W8CorpusIncident incident) =>
        incident.LanguageProfile == "FrameValueExpressionV1"
            ? world.EvaluateFrameValue(incident.Expression, incident.ReadWidth)
            : world.Evaluate(
                incident.Expression,
                incident.ReadWidth,
                incident.RequestsPausedFrameThread ? world.PausedFrameThreadSelector() : null,
                RequestsScopedContext(incident)
                    ? world.PausedFrameScopedContext(ContextMode(incident))
                    : null,
                suppliesSuffixEvaluation: incident.SuffixProfile != "None");

    // A contextual spelling under a declared selected frame consults the scoped-context seam; a fully qualified
    // spelling never does, exactly as the frozen route rule states, so the seam is not even supplied there.
    private static bool RequestsScopedContext(W8CorpusIncident incident) =>
        incident.LanguageProfile == "StaticFieldExpressionV2" &&
        incident.SelectedFrameMode != "none" &&
        !incident.Expression.StartsWith("global::", StringComparison.Ordinal);

    private static W8CorpusContextMode ContextMode(W8CorpusIncident incident) =>
        incident.SelectedFrameMode == "requested-but-absent" ? W8CorpusContextMode.FrameAbsent
        : incident.PortablePdbInput == "partial" ? W8CorpusContextMode.TruncatedPdb
        : incident.PortablePdbInput == "conflicting" ? W8CorpusContextMode.MismatchedPdb
        : W8CorpusContextMode.Exact;

    private static W8CorpusEvaluation ApplyCounterfactual(W8CorpusEvaluationWorld world, W8CorpusIncident incident) =>
        incident.CounterfactualAction switch
        {
            "withhold-runtime-evidence" => world.EvaluateWithoutRuntimeEvidence(incident.Expression),

            // The contextual route's own counterfactual: the same spelling with no scoped-context seam at all, which
            // must stop on the context axis rather than silently resolving the name some other way.
            "withhold-scoped-context" => world.Evaluate(
                incident.Expression,
                incident.ReadWidth,
                incident.RequestsPausedFrameThread ? world.PausedFrameThreadSelector() : null),
            "evaluate-fully-qualified-control" => world.Evaluate(
                incident.ControlExpression!,
                incident.ReadWidth),

            // Restoring the poisoned Portable-PDB companion re-runs the same spelling under the exact context
            // acquisition, so the recorded difference is the context poison and nothing else.
            "restore-complete-pdb-bytes" or "restore-matching-module-identity" => world.Evaluate(
                incident.Expression,
                incident.ReadWidth,
                incident.RequestsPausedFrameThread ? world.PausedFrameThreadSelector() : null,
                world.PausedFrameScopedContext()),
            "substitute-closed-type-argument" => world.Evaluate(
                incident.CounterfactualExpression ?? incident.Expression,
                incident.ReadWidth),

            // A property owns the spelling the incident asks for; the counterfactual asks for the field instead, by
            // its own declared name. The reachable field is a different member, not a fallback for the same one.
            "request-declared-field-instead-of-property" => world.Evaluate(
                incident.CounterfactualExpression!,
                incident.ReadWidth,
                incident.RequestsPausedFrameThread ? world.PausedFrameThreadSelector() : null,
                incident.RequestsPausedFrameThread ? world.PausedFrameScopedContext() : null),

            // The bare route's own counterfactual: the same spelling under the same exact scoped context, with the
            // caller-owned lexical seam withheld so no envelope can be acquired at all.
            "withhold-lexical-envelope" => world.Evaluate(
                incident.Expression,
                incident.ReadWidth,
                incident.RequestsPausedFrameThread ? world.PausedFrameThreadSelector() : null,
                world.PausedFrameScopedContext(suppliesLexicalEnvelope: false)),
            "select-different-thread" => world.Evaluate(
                incident.Expression,
                incident.ReadWidth,
                world.WorkerParkThreadSelector()),

            // The literal route's own counterfactual: the same spelling under the same exact context with no Constant
            // catalog at all, so the value axis has no source to project and stops instead of reading storage.
            "withhold-literal-constant-source" => world.Evaluate(
                incident.Expression,
                incident.ReadWidth,
                incident.RequestsPausedFrameThread ? world.PausedFrameThreadSelector() : null,
                world.PausedFrameScopedContext(),
                suppliesConstantCatalogs: false),

            // The frame-slot counterfactual selects the probe's alternate memory-homed local, which the workflow probe
            // seeds to a different value at a different exact frame home, changing both the value and the slot address.
            "select-different-frame-slot" => world.EvaluateFrameValue(
                incident.Expression,
                incident.ReadWidth,
                localNameOverride: "alternateLocal"),
            _ => throw new InvalidOperationException(
                $"The runner cannot yet apply the declared counterfactual '{incident.CounterfactualAction}'."),
        };

    private static string Describe(DumpExpressionV2OutcomeAxes axes) =>
        string.Join('/', AxisNames.Select(axis => W8CorpusIncident.AxisText(axes, axis)));
}

/// <summary>Tallies one frozen successor category over the predeclared rows that support it.</summary>
/// <param name="Category">The frozen category identifier.</param>
/// <param name="IncidentCount">The raw count of supporting incidents.</param>
/// <param name="ApplicationShapeCount">The raw count of distinct supporting application shapes.</param>
/// <param name="DecisionChangingCount">The raw count of supporting decision-changing rows.</param>
/// <param name="AttributableEvidenceCount">The raw count of supporting rows with attributable stage evidence.</param>
public sealed record W8CorpusCategoryTally(
    string Category,
    int IncidentCount,
    int ApplicationShapeCount,
    int DecisionChangingCount,
    int AttributableEvidenceCount)
{
    /// <summary>Gets the frozen substantive-equality key of this tally.</summary>
    public string SubstantiveKey =>
        $"{IncidentCount}:{ApplicationShapeCount}:{DecisionChangingCount}:{AttributableEvidenceCount}";

    /// <summary>Decides whether the qualified set must defer instead of selecting one action.</summary>
    /// <param name="qualified">Every qualified category tally.</param>
    /// <returns><see langword="true"/> when no single tally is strictly ahead on the frozen keys.</returns>
    public static bool Defers(IReadOnlyList<W8CorpusCategoryTally> qualified)
    {
        if (qualified.Count == 0)
        {
            return true;
        }

        var best = qualified
            .OrderByDescending(static tally => tally.IncidentCount)
            .ThenByDescending(static tally => tally.ApplicationShapeCount)
            .ThenByDescending(static tally => tally.DecisionChangingCount)
            .ThenByDescending(static tally => tally.AttributableEvidenceCount)
            .First();
        return qualified.Count(tally => string.Equals(tally.SubstantiveKey, best.SubstantiveKey, StringComparison.Ordinal)) > 1;
    }
}

/// <summary>Freezes one predeclared corpus incident exactly as the manifest spells it.</summary>
public sealed class W8CorpusIncident
{
    private readonly Dictionary<string, string> expectedAxes;
    private readonly string? counterfactualExpression;

    internal W8CorpusIncident(JsonElement element)
    {
        Id = W8CorpusManifest.RequiredString(element, "id");
        Ordinal = element.GetProperty("ordinal").GetInt32();
        Shape = W8CorpusManifest.RequiredString(element, "shape");
        PlannedQuestion = W8CorpusManifest.RequiredString(element, "plannedQuestion");
        SnapshotKind = W8CorpusManifest.RequiredString(element, "snapshotKind");
        SnapshotIdentity = W8CorpusManifest.RequiredString(element, "snapshotIdentity");
        Expression = W8CorpusManifest.RequiredString(element, "expression");
        ControlExpression = element.GetProperty("controlExpression").ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty("controlExpression").GetString();
        LanguageProfile = W8CorpusManifest.RequiredString(element, "languageProfile");
        SuffixProfile = W8CorpusManifest.RequiredString(element, "suffixProfile");
        ExpectedFirstBoundary = W8CorpusManifest.RequiredString(element, "expectedFirstBoundary");
        SupportsSuccessorCategory = W8CorpusManifest.RequiredString(element, "supportsSuccessorCategory");
        Representative = element.GetProperty("representative").GetBoolean();

        var target = element.GetProperty("target");
        AssemblyName = W8CorpusManifest.RequiredString(target, "assemblyName");
        TruthGateProfile = W8CorpusManifest.RequiredString(target, "truthGateProfile");
        TargetArguments = W8CorpusManifest.ReadStrings(target.GetProperty("targetArguments"));

        var inputs = element.GetProperty("inputs");
        ArtifactInputs = W8CorpusManifest.ReadStrings(inputs.GetProperty("artifactInputs"));
        SelectedFrameMode = W8CorpusManifest.RequiredString(inputs.GetProperty("selectedFrame"), "mode");
        PortablePdbInput = W8CorpusManifest.RequiredString(inputs, "portablePdb");
        ExpectedTerminal = element.GetProperty("expectedTerminal").ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty("expectedTerminal").GetString();

        expectedAxes = element.GetProperty("expectedAxes")
            .EnumerateObject()
            .ToDictionary(static axis => axis.Name, static axis => axis.Value.GetString()!, StringComparer.Ordinal);

        var usefulness = element.GetProperty("usefulness");
        UsefulnessValue = usefulness.GetProperty("value").GetBoolean();
        UsefulnessCode = W8CorpusManifest.RequiredString(usefulness.GetProperty("rationale"), "code");
        UsefulnessQuestion = W8CorpusManifest.RequiredString(usefulness.GetProperty("rationale"), "question");
        UsefulnessEvidence = W8CorpusManifest.RequiredString(usefulness.GetProperty("rationale"), "evidence");

        var decision = element.GetProperty("decisionChanging");
        DecisionChanging = decision.GetProperty("value").GetBoolean();
        DecisionCode = W8CorpusManifest.RequiredString(decision.GetProperty("rationale"), "code");
        DecisionSummary = W8CorpusManifest.RequiredString(decision.GetProperty("rationale"), "summary");
        var counterfactual = decision.GetProperty("counterfactual");
        CounterfactualAction = W8CorpusManifest.RequiredString(counterfactual, "action");
        counterfactualExpression = counterfactual.TryGetProperty("counterfactualExpression", out var substitute)
            ? substitute.GetString()
            : null;
        CounterfactualChangedAxes = W8CorpusManifest.ReadStrings(counterfactual.GetProperty("expectedChangedAxes"));
        CounterfactualDifference = W8CorpusManifest.RequiredString(counterfactual, "expectedDifference");

        var attributable = element.GetProperty("attributableEvidence");
        AttributableStage = W8CorpusManifest.RequiredString(attributable, "stage");
        AttributableExpectedOutcome = W8CorpusManifest.RequiredString(attributable, "expectedOutcome");
        AttributableEvidence = attributable.GetProperty("attributable").GetBoolean();
        AttributableMeasurement = W8CorpusManifest.RequiredString(attributable, "measurement");

        var execution = element.GetProperty("runnerExecution");
        RunnerExecutionStatus = W8CorpusManifest.RequiredString(execution, "status");
        RunnerExecutionReason = W8CorpusManifest.RequiredString(execution, "reason");
    }

    /// <summary>Gets the predeclared incident identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the predeclared incident ordinal.</summary>
    public int Ordinal { get; }

    /// <summary>Gets the predeclared application shape.</summary>
    public string Shape { get; }

    /// <summary>Gets the plan question this incident transcribes.</summary>
    public string PlannedQuestion { get; }

    /// <summary>Gets the predeclared snapshot kind.</summary>
    public string SnapshotKind { get; }

    /// <summary>Gets the predeclared distinct snapshot identity.</summary>
    public string SnapshotIdentity { get; }

    /// <summary>Gets the predeclared expression text.</summary>
    public string Expression { get; }

    /// <summary>Gets the predeclared fully qualified control expression, or null when the row declares none.</summary>
    public string? ControlExpression { get; }

    /// <summary>Gets the explicitly selected language profile.</summary>
    public string LanguageProfile { get; }

    /// <summary>Gets the declared suffix profile, or <c>None</c> when the spelling carries no suffix.</summary>
    public string SuffixProfile { get; }

    /// <summary>Gets the predeclared expected first boundary.</summary>
    public string ExpectedFirstBoundary { get; }

    /// <summary>Gets the successor category this incident would support.</summary>
    public string SupportsSuccessorCategory { get; }

    /// <summary>Gets whether the incident is a representative observation; the corpus forbids it.</summary>
    public bool Representative { get; }

    /// <summary>Gets the target assembly name that materializes the incident.</summary>
    public string AssemblyName { get; }

    /// <summary>Gets the truth-gate profile that materializes the incident.</summary>
    public string TruthGateProfile { get; }

    /// <summary>Gets the complete target invocation arguments.</summary>
    public ImmutableArray<string> TargetArguments { get; }

    /// <summary>Gets the companion artifact inputs the incident needs.</summary>
    public ImmutableArray<string> ArtifactInputs { get; }

    /// <summary>Gets the predeclared selected-frame input mode of the incident.</summary>
    public string SelectedFrameMode { get; }

    /// <summary>Gets the predeclared Portable-PDB input disposition of the incident.</summary>
    public string PortablePdbInput { get; }

    /// <summary>Gets whether the row declares the paused truth-gate frame's thread as a required input.</summary>
    public bool RequestsPausedFrameThread => SelectedFrameMode == "managed-thread-and-frame";

    /// <summary>Gets the predeclared terminal answer text, or null when no answer is expected.</summary>
    public string? ExpectedTerminal { get; }

    /// <summary>Gets the explicit usefulness boolean.</summary>
    public bool UsefulnessValue { get; }

    /// <summary>Gets the stable usefulness rationale code.</summary>
    public string UsefulnessCode { get; }

    /// <summary>Gets the investigator question the incident answers.</summary>
    public string UsefulnessQuestion { get; }

    /// <summary>Gets the evidence the usefulness rationale names.</summary>
    public string UsefulnessEvidence { get; }

    /// <summary>Gets the explicit decision-changing boolean.</summary>
    public bool DecisionChanging { get; }

    /// <summary>Gets the stable decision rationale code.</summary>
    public string DecisionCode { get; }

    /// <summary>Gets the decision rationale summary.</summary>
    public string DecisionSummary { get; }

    /// <summary>Gets the declared counterfactual action.</summary>
    public string CounterfactualAction { get; }

    /// <summary>Gets the axes the declared counterfactual must change.</summary>
    public ImmutableArray<string> CounterfactualChangedAxes { get; }

    /// <summary>Gets the declared counterfactual difference summary.</summary>
    public string CounterfactualDifference { get; }

    /// <summary>Gets the named stage at which attributable evidence is measured.</summary>
    public string AttributableStage { get; }

    /// <summary>Gets the expected axis outcome at the named attributable stage.</summary>
    public string AttributableExpectedOutcome { get; }

    /// <summary>Gets whether the named stage carries attributable evidence.</summary>
    public bool AttributableEvidence { get; }

    /// <summary>Gets the declared attributable-evidence measurement rule.</summary>
    public string AttributableMeasurement { get; }

    /// <summary>Gets whether this runner executes the incident or carries it as manifest-only.</summary>
    public string RunnerExecutionStatus { get; }

    /// <summary>Gets why the runner executes or defers the incident.</summary>
    public string RunnerExecutionReason { get; }

    /// <summary>Gets the counted read width the row's own root implies.</summary>
    /// <remarks>
    /// A row that requests a suffix reads its root as a reference, so the counted root read is one pointer wide; the
    /// terminal's own width belongs to the suffix evaluation rather than to the root. A row whose terminal is the
    /// exact null reference also reads its root as a reference, so its counted width is one pointer too. Only a row
    /// whose terminal is a primitive root takes its width from that terminal.
    /// </remarks>
    public int ReadWidth =>
        !string.Equals(SuffixProfile, "None", StringComparison.Ordinal) ? sizeof(ulong)
        : string.Equals(ExpectedTerminal, "null", StringComparison.Ordinal) ? sizeof(ulong)
        : ExpectedTerminal is { } nullable && nullable.StartsWith("nullable-i32:", StringComparison.Ordinal)
            ? 2 * sizeof(int)
        : ExpectedTerminal is { } terminal && terminal.StartsWith("i64:", StringComparison.Ordinal) ? sizeof(long)
        : sizeof(int);

    /// <summary>Gets the expression this incident's counterfactual evaluates in place of its own, when one applies.</summary>
    /// <remarks>
    /// A closed-argument substitution is a mechanical edit of the incident's own spelling and stays derived. Every
    /// other action naming a different member supplies that member in the manifest, so the runner never invents one.
    /// </remarks>
    public string? CounterfactualExpression => CounterfactualAction == "substitute-closed-type-argument"
        ? SubstituteClosedArgument()
        : counterfactualExpression;

    /// <summary>Gets the sealed twelve-axis aggregate the manifest predeclares for this incident.</summary>
    public DumpExpressionV2OutcomeAxes PredeclaredAxes => DumpExpressionV2OutcomeAxes.Create(
        Enum.Parse<DumpExpressionSyntaxStatus>(expectedAxes["syntax"]),
        Enum.Parse<DumpExpressionContextOutcome>(expectedAxes["context"]),
        Enum.Parse<DumpExpressionRootAttributionOutcome>(expectedAxes["rootAttribution"]),
        Enum.Parse<DumpExpressionLexicalCompletenessOutcome>(expectedAxes["lexicalCompleteness"]),
        Enum.Parse<DumpExpressionTypeBindingOutcome>(expectedAxes["typeBinding"]),
        Enum.Parse<DumpExpressionTypeConstructionOutcome>(expectedAxes["typeConstruction"]),
        Enum.Parse<DumpExpressionMemberLookupOutcome>(expectedAxes["memberLookup"]),
        Enum.Parse<DumpExpressionRuntimeConstructionOutcome>(expectedAxes["runtimeConstruction"]),
        Enum.Parse<DumpExpressionStorageOutcome>(expectedAxes["storage"]),
        Enum.Parse<DumpExpressionValueOutcome>(expectedAxes["value"]),
        Enum.Parse<DumpExpressionSuffixOutcome>(expectedAxes["suffix"]),
        Enum.Parse<DumpExpressionCompletenessOutcome>(expectedAxes["completeness"]));

    /// <summary>Reads the predeclared outcome text of one named axis.</summary>
    /// <param name="axis">The named axis.</param>
    /// <returns>The predeclared outcome spelling.</returns>
    public string ExpectedAxisText(string axis) => expectedAxes[axis];

    /// <summary>Reads the produced outcome text of one named axis.</summary>
    /// <param name="axes">The produced aggregate.</param>
    /// <param name="axis">The named axis.</param>
    /// <returns>The produced outcome spelling.</returns>
    public static string AxisText(DumpExpressionV2OutcomeAxes axes, string axis) => axis switch
    {
        "syntax" => axes.Syntax.ToString(),
        "context" => axes.Context.ToString(),
        "rootAttribution" => axes.RootAttribution.ToString(),
        "lexicalCompleteness" => axes.LexicalCompleteness.ToString(),
        "typeBinding" => axes.TypeBinding.ToString(),
        "typeConstruction" => axes.TypeConstruction.ToString(),
        "memberLookup" => axes.MemberLookup.ToString(),
        "runtimeConstruction" => axes.RuntimeConstruction.ToString(),
        "storage" => axes.Storage.ToString(),
        "value" => axes.Value.ToString(),
        "suffix" => axes.Suffix.ToString(),
        "completeness" => axes.Completeness.ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "The axis is not one of the twelve."),
    };

    private string SubstituteClosedArgument() => Id switch
    {
        "request-distinct-construction-slots" => Expression.Replace(
            ".BatchContext>",
            ".RequestContext>",
            StringComparison.Ordinal),
        "request-absent-runtime-construction" => Expression.Replace(
            ".NeverConstructedContext>",
            ".RequestContext>",
            StringComparison.Ordinal),
        "coordinator-four-coexisting-constructions" => Expression.Replace(
            ".NorthRegion>",
            ".SouthRegion>",
            StringComparison.Ordinal),
        "batch-nested-per-segment-arity" => Expression.Replace(
            ".BatchValue>",
            ".BatchKey>",
            StringComparison.Ordinal),

        // The different closed argument the action names is the shape's own other materialized construction: the
        // array argument holds exact null, so the completed label changes, and the non-array argument holds the
        // live step, so the null value changes.
        "workflow-var-substitution-conditional-chain" => Expression.Replace(
            ".StepContext>",
            ".StepContext[]>",
            StringComparison.Ordinal),
        "workflow-array-nested-argument-exact-null" => Expression.Replace(
            ".StepContext[]>",
            ".StepContext>",
            StringComparison.Ordinal),
        _ => Expression,
    };
}

/// <summary>Loads the predeclared W8.9 corpus manifest exactly once per test.</summary>
public sealed class W8CorpusManifest
{
    private readonly JsonDocument document;

    private W8CorpusManifest(JsonDocument document, string rawText)
    {
        this.document = document;
        RawText = rawText;
        Incidents =
        [
            .. document.RootElement.GetProperty("incidents")
                .EnumerateArray()
                .Select(static element => new W8CorpusIncident(element)),
        ];
    }

    /// <summary>Gets the manifest root element.</summary>
    public JsonElement Root => document.RootElement;

    /// <summary>Gets the exact manifest text.</summary>
    public string RawText { get; }

    /// <summary>Gets every predeclared incident in ordinal order.</summary>
    public ImmutableArray<W8CorpusIncident> Incidents { get; }

    /// <summary>Loads the manifest from the repository corpus directory.</summary>
    /// <returns>The parsed predeclared manifest.</returns>
    public static W8CorpusManifest Load()
    {
        var path = W8ShapeTargetPaths.RepositoryPath("tests/corpus/w8-static-field-incidents-v1.json");
        var text = File.ReadAllText(path);
        return new W8CorpusManifest(JsonDocument.Parse(text), text);
    }

    /// <summary>Reads a required non-empty string property.</summary>
    /// <param name="element">The owning element.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The non-empty string value.</returns>
    public static string RequiredString(JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetString();
        Assert.False(string.IsNullOrWhiteSpace(value), $"The manifest property '{name}' must be a non-empty string.");
        return value!;
    }

    /// <summary>Reads a string array property.</summary>
    /// <param name="element">The array element.</param>
    /// <returns>The read strings.</returns>
    public static ImmutableArray<string> ReadStrings(JsonElement element) =>
        [.. element.EnumerateArray().Select(static item => item.GetString()!)];
}

/// <summary>Resolves the repository-relative artifacts the corpus runner materializes.</summary>
internal static class W8ShapeTargetPaths
{
    internal static string RepositoryPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    internal static string ResolveExecutable(string assemblyName)
    {
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? assemblyName + ".exe"
            : assemblyName;
        return Path.Combine(ResolveOutputDirectory(assemblyName), fileName);
    }

    internal static string ResolveAssembly(string assemblyName) =>
        Path.Combine(ResolveOutputDirectory(assemblyName), assemblyName + ".dll");

    /// <summary>
    /// Resolves a referenced companion assembly from the consuming shape target's own output directory.
    /// </summary>
    /// <remarks>
    /// The companion must be the copy the target process actually loads, not the copy in the companion project's own
    /// output. The two are byte-identical only while both are freshly built; after an incremental build they can
    /// differ, and resolving the wrong one makes the module silently fail content matching, so the composition drops
    /// it and the incident's answer degrades instead of failing loudly.
    /// </remarks>
    /// <param name="shapeAssemblyName">The shape target whose output directory the process runs from.</param>
    /// <param name="companionAssemblyName">The referenced companion assembly to resolve.</param>
    /// <returns>The complete path of the companion copy the shape target loads.</returns>
    internal static string ResolveCompanionAssembly(string shapeAssemblyName, string companionAssemblyName) =>
        Path.Combine(ResolveOutputDirectory(shapeAssemblyName), companionAssemblyName + ".dll");

    internal static string ResolveNamedRvaFixture() =>
        RepositoryPath("tests/PhoenixInspect.W8TestTarget/NamedRvaFixture/PhoenixInspect.W8NamedRvaTarget.dll");

    internal static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"The required corpus artifact is missing: {path}");
        return path;
    }

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string ResolveOutputDirectory(string assemblyName)
    {
        var testsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        return Path.Combine(testsRoot, assemblyName, "bin", "Release", targetFramework);
    }
}

/// <summary>Materializes exactly one independent hidden full dump for one predeclared incident.</summary>
internal sealed class W8CorpusSnapshot : IDisposable
{
    private W8CorpusSnapshot(string dumpPath) => DumpPath = dumpPath;

    internal string DumpPath { get; }

    internal static W8CorpusSnapshot Materialize(W8CorpusIncident incident)
    {
        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"w8-corpus-{incident.Id}-{Guid.NewGuid():N}.dmp");
        var executable = W8ShapeTargetPaths.RequireArtifact(
            W8ShapeTargetPaths.ResolveExecutable(incident.AssemblyName));
        using (var target = TestTargetRunner.StartAndWaitReady(
            executable,
            incident.TargetArguments,
            isolatedDirectory: null))
        {
            DumpWriter.WriteFullDump(target.Pid, dumpPath);
        }

        return new W8CorpusSnapshot(dumpPath);
    }

    public void Dispose()
    {
        if (File.Exists(DumpPath))
        {
            File.Delete(DumpPath);
        }
    }
}

/// <summary>Carries one produced pipeline result together with the physical facts the runner metered.</summary>
/// <param name="Result">The produced pipeline result.</param>
/// <param name="SlotAddress">The exact static slot address the runner acquired, when one exists.</param>
internal readonly record struct W8CorpusEvaluation(StaticFieldV2ExpressionResult Result, ulong? SlotAddress);

/// <summary>
/// Composes the produced metadata authority of one shape target over one real full dump and evaluates predeclared
/// expressions through the unchanged V2 pipeline.
/// </summary>
/// <remarks>
/// The authority is composed from every produced module the shape's incidents reference plus one synthetic core module
/// carrying the real <c>System.Runtime</c> assembly identity, because the pinned corelib is rejected by the shared
/// bounded ECMA signature grammar. The batch shape additionally binds the referenced named-RVA module that owns the
/// module-RVA storage. This scaffolding is physical evidence, not a product discovery rule.
/// </remarks>
/// <summary>Names the declared context-acquisition circumstances one corpus incident evaluates under.</summary>
internal enum W8CorpusContextMode
{
    /// <summary>The paused truth-gate frame and the shape's own Portable PDB are acquired exactly.</summary>
    Exact = 1,

    /// <summary>The declared selected frame is requested from the session and is genuinely absent.</summary>
    FrameAbsent = 2,

    /// <summary>The truncated Portable-PDB companion is read and only a pre-ImportScope prefix is observed.</summary>
    TruncatedPdb = 3,

    /// <summary>The identity-mismatching Portable-PDB companion from another shape target is read.</summary>
    MismatchedPdb = 4,
}

internal sealed class W8CorpusEvaluationWorld : IDisposable
{
    private const int PublicClassAttributes = 0x0000_0001;
    private const ulong SyntheticCoreModuleAddress = 0x0000_7E00_0000_0002UL;

    private readonly ImmutableArray<ProducedModule> modules;
    private readonly DataTarget rawReadTarget;
    private readonly string shape;
    private readonly string dumpPath;
    private ClrRuntime? layoutRuntime;

    private W8CorpusEvaluationWorld(
        StaticFieldV2RuntimeAcquisitionSession session,
        DataTarget rawReadTarget,
        string shape,
        string dumpPath,
        ImmutableArray<ProducedModule> modules,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        ImmutableArray<MetadataConstantTableCatalogIdentity> constantCatalogs,
        ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        MetadataConstraintTargetResolutionPortfolioIdentity constraints,
        ImmutableArray<MetadataSignatureTokenResolutionCatalog> tokenResolutionCatalogs)
    {
        Session = session;
        this.rawReadTarget = rawReadTarget;
        this.shape = shape;
        this.dumpPath = dumpPath;
        this.modules = modules;
        FieldCatalogs = fieldCatalogs;
        ConstantCatalogs = constantCatalogs;
        PropertyCatalogs = propertyCatalogs;
        Bindings = [.. modules.Select(static module => module.Binding)];
        ChainPortfolio = chainPortfolio;
        Ancestry = ancestry;
        Constraints = constraints;
        TokenResolutionCatalogs = tokenResolutionCatalogs;
    }

    internal StaticFieldV2RuntimeAcquisitionSession Session { get; }

    internal ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs { get; }

    internal ImmutableArray<MetadataConstantTableCatalogIdentity> ConstantCatalogs { get; }

    internal ImmutableArray<MetadataPropertyTableCatalogIdentity> PropertyCatalogs { get; }

    internal ImmutableArray<StaticFieldV2RuntimeModuleBinding> Bindings { get; }

    internal MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio { get; }

    internal MetadataAncestryAuthorityPortfolioIdentity Ancestry { get; }

    internal MetadataConstraintTargetResolutionPortfolioIdentity Constraints { get; }

    internal ImmutableArray<MetadataSignatureTokenResolutionCatalog> TokenResolutionCatalogs { get; }

    /// <summary>Gets the primary module's composed physical TypeSpec table, which retains raw signature bytes.</summary>
    internal MetadataTypeSpecificationPhysicalTableCatalogIdentity PrimaryTypeSpecifications =>
        Primary.Outcome.TypeSpecifications!;

    /// <summary>
    /// Gets the declared corelib identity collapse: the snapshot's real corelib module collapses onto the composed
    /// exact core module, so a runtime corelib argument and a spelled core name produce one identity.
    /// </summary>
    internal StaticFieldV2CoreIdentityCollapse? CoreIdentityCollapse { get; private set; }

    /// <summary>
    /// Builds a caller-declared reference-target construction for one of the primary module's top-level non-generic
    /// classes, from the same composed classification chain the pipeline itself consumes.
    /// </summary>
    /// <param name="typeName">The metadata type name inside the shape's own namespace.</param>
    /// <returns>The exact closed construction of that definition at arity zero.</returns>
    internal MetadataClosedTypeIdentity DeclaredReferenceTarget(string typeName)
    {
        var token = FindTypeToken(Primary.Reader, PrimaryNamespace(), typeName);
        var classification = Ancestry.ExactClassificationOrDefault(Primary.MetadataModule, token);
        Assert.NotNull(classification);
        return MetadataClosedTypeIdentity.ConstructNamed([classification], []);
    }

    private ProducedModule Primary => modules[0];

    internal static W8CorpusEvaluationWorld Open(
        string dumpPath,
        string shape,
        bool suppliesMalformedTypeSpecCompanion = false)
    {
        var artifacts = ShapeArtifacts(shape);
        var session = StaticFieldV2RuntimeAcquisitionSession.Open(dumpPath);
        try
        {
            var rawReadTarget = DataTarget.LoadDump(
                dumpPath,
                new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
            try
            {
                var produced = ImmutableArray.CreateBuilder<ProducedModule>();
                try
                {
                    var isPrimary = true;
                    foreach (var (artifactPath, required) in artifacts)
                    {
                        var artifact = ReadArtifactContent(W8ShapeTargetPaths.RequireArtifact(artifactPath));
                        var observations = session.Modules
                            .Where(candidate =>
                                candidate.MetadataLength == (ulong)artifact.Bytes.Length &&
                                ModuleContentIdentity
                                    .FromMetadata(
                                        artifact.Mvid,
                                        session.ReadModuleMetadata(candidate.ModuleAddress).AsSpan())
                                    .Equals(artifact.Content))
                            .OrderBy(static candidate => candidate.ModuleAddress)
                            .ToArray();

                        // A referenced companion is composed only for the profiles whose own execution actually
                        // loaded it. Its absence from a snapshot is a physical fact of that profile, never a reason
                        // to fabricate an authority entry; the shape's own module always remains required.
                        Assert.True(
                            observations.Length > 0 || !required,
                            $"The snapshot loads no module matching the required artifact {artifactPath}.");
                        foreach (var observation in observations)
                        {
                            produced.Add(ProducedModule.Bind(
                                session,
                                observation,
                                isPrimary && suppliesMalformedTypeSpecCompanion
                                    ? RealizeMalformedTypeSpecCompanion
                                    : null));
                        }

                        isPrimary = false;
                    }

                    var core = BuildSyntheticCoreModule(session, produced[0].MetadataModule);
                    var compatibility = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                        [core.Compatibility, .. produced.Select(static module => module.Outcome.Compatibility!)]);
                    var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                        compatibility,
                        [core.ChainCatalog, .. produced.Select(static module => module.Outcome.ChainCatalog!)]);
                    var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                        chainPortfolio,
                        [core.Tables, .. produced.Select(static module => module.Outcome.ReferenceTables!)]);
                    var ancestry = MetadataAncestryAuthorityPortfolioIdentity.Create(resolution);
                    var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
                        resolution,
                        [
                            core.Constraints,
                            .. produced.Select(static module => module.Outcome.GenericParameterConstraints!),
                        ]);
                    Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestry.ResultKind);
                    Assert.Equal(
                        MetadataConstraintTargetResolutionPortfolioResultKind.Exact,
                        constraints.ResultKind);

                    ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs =
                    [
                        core.FieldCatalog,
                        .. produced.Select(static module => module.Outcome.FieldDefinitions!),
                    ];
                    var corelibModuleAddress = FindCorelibModuleAddress(session);
                    var world = new W8CorpusEvaluationWorld(
                        session,
                        rawReadTarget,
                        shape,
                        dumpPath,
                        produced.ToImmutable(),
                        fieldCatalogs,
                        [
                            core.ConstantCatalog,
                            .. produced.Select(static module => module.Outcome.Constants!),
                        ],
                        [
                            core.PropertyCatalog,
                            .. produced.Select(static module => module.Outcome.Properties!),
                        ],
                        chainPortfolio,
                        ancestry,
                        constraints,
                        BuildTokenResolutionCatalogs(ancestry, fieldCatalogs));

                    // The snapshot's real corelib module collapses onto the composed exact core module, so a runtime
                    // corelib argument and a spelled core name produce one identity. A snapshot with no readable
                    // corelib module simply declares no collapse and keeps the prior unprojectable behavior.
                    if (corelibModuleAddress != 0)
                    {
                        world.CoreIdentityCollapse = StaticFieldV2CoreIdentityCollapse.Create(
                            corelibModuleAddress,
                            core.Module);
                    }

                    return world;
                }
                catch
                {
                    foreach (var module in produced)
                    {
                        module.Dispose();
                    }

                    throw;
                }
            }
            catch
            {
                rawReadTarget.Dispose();
                throw;
            }
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Supplies the caller-owned suffix seam by rooting the unchanged W2/W6 evaluator at the reference the pipeline
    /// resolved, exactly as a host would.
    /// </summary>
    /// <remarks>
    /// Every step acquires evidence rather than asserting it. The address arrives from the composition's own resolved
    /// static reference; the session validates the object there raw-header-first, so a value the pipeline believed
    /// was a reference but is not stops as typed host evidence instead of becoming a fabricated root. The typed
    /// object binding records the honest provenance — an object this host supplied across a boundary — and the query
    /// the seam prepares is the descriptor's own suffix, spelled from its segments rather than from the incident.
    /// </remarks>
    /// <returns>The caller-owned suffix seam.</returns>
    internal StaticFieldV2SuffixEvaluationSource PausedFrameSuffixEvaluation() =>
        StaticFieldV2SuffixEvaluationSource.Create(EvaluateSuffix);

    private EvaluationResult<DumpQueryValue> EvaluateSuffix(StaticFieldV2SuffixEvaluationRequest request)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var suffixSession = opened.Value!;

        var validated = suffixSession.ValidateExactObjectAtAddress(
            request.ReferenceAddress,
            suffixSession.Memory.PointerSize);
        Assert.Equal(ClrmdEvidenceStatus.Exact, validated.Status);
        var reference = validated.Value!;

        var projected = suffixSession.ProjectExactObjectForInstanceEvaluation(reference);
        Assert.Equal(ClrmdEvidenceStatus.Exact, projected.Status);

        var identity = DumpObjectIdentity.FromExactObject(reference);
        var binding = DumpObjectBinding.Create(
            identity,
            DumpObjectProvenance.FromHostSuppliedExactObject(
                DumpHostSuppliedObjectSourceIdentity.Create(identity, "w8CorpusSuffixRoot")));
        var rootBinding = DumpQueryRootBinding.FromObjectBinding("suffixRoot", projected.Value!, binding);

        var preparation = DumpQueryEngine.Prepare(suffixSession, SpellSuffix(request.Suffix), rootBinding);
        return preparation.IsSuccess
            ? DumpQueryEngine.Evaluate(suffixSession, preparation.Plan!)
            : preparation.Failure!;
    }

    /// <summary>Spells one frozen suffix descriptor as the unchanged W2 query text its own segments describe.</summary>
    /// <remarks>
    /// The first hop is always spelled direct even when the source wrote <c>?.</c>, because the composition already
    /// discharged that question: it resolved the reference, proved it non-null, and only then called this seam — an
    /// exact-null root takes the unchanged no-read path and never arrives here. The W2 grammar refuses conditional
    /// access on a host-selected root for the same reason, so spelling it would contradict evidence the caller holds.
    /// Later hops keep their own access kind, which is where conditional access still decides something.
    /// </remarks>
    /// <param name="suffix">The descriptor the parser froze.</param>
    /// <returns>The query text rooted at the seam's compatibility root name.</returns>
    private static string SpellSuffix(DumpExpressionSuffixDescriptor suffix)
    {
        var text = new StringBuilder("suffixRoot");
        for (var index = 0; index < suffix.Segments.Length; index++)
        {
            var segment = suffix.Segments[index];
            text.Append(
                    index > 0 && segment.AccessKind == DumpExpressionSuffixAccessKind.Conditional ? "?." : ".")
                .Append(segment.Identifier.DecodedText);
        }

        return suffix.FallbackKind switch
        {
            DumpExpressionFallbackKind.None => text.ToString(),
            DumpExpressionFallbackKind.Null => text.Append(" ?? null").ToString(),
            DumpExpressionFallbackKind.Int32 => text
                .Append(" ?? ")
                .Append(suffix.Int32Fallback!.Value.ToString(CultureInfo.InvariantCulture))
                .ToString(),
            _ => text.Append(" ?? \"").Append(suffix.StringFallback).Append('"').ToString(),
        };
    }

    /// <summary>
    /// Builds the scoped-context seam for one incident the way the host would: a real frame selection and
    /// Portable-PDB read over the incident's own dump, mapped into the typed acquisition envelope. A frame the
    /// incident declares as requested-but-absent is actually requested from the session and its unavailable
    /// observation becomes the typed acquisition stop; a declared truncated or identity-mismatching Portable-PDB
    /// companion is actually read through the session's own resolver path and its partial or conflicting
    /// observation becomes the stop. Nothing is fabricated.
    /// </summary>
    /// <param name="mode">The incident's declared context-acquisition circumstances.</param>
    /// <param name="suppliesLexicalEnvelope">
    /// Whether the seam can also project the selected method's lexical envelope. Withholding it is the bare route's
    /// declared counterfactual, not a runner limitation.
    /// </param>
    /// <returns>A caller-owned seam producing one typed scoped-context acquisition per call.</returns>
    internal StaticFieldV2ScopedContextSource PausedFrameScopedContext(
        W8CorpusContextMode mode = W8CorpusContextMode.Exact,
        bool suppliesLexicalEnvelope = true) =>
        StaticFieldV2ScopedContextSource.CreateFromAcquisition(
            () => AcquireScopedContext(mode),
            suppliesLexicalEnvelope ? AcquireLexicalEnvelope : null);

    private StaticFieldV2ScopedContextAcquisition AcquireScopedContext(W8CorpusContextMode mode)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var contextSession = opened.Value!;

        if (mode == W8CorpusContextMode.FrameAbsent)
        {
            var absent = contextSession.SelectExpressionFrame(DumpSelectedFrameSelector.Create(
                contextSession.Snapshot,
                threadOrdinal: int.MaxValue,
                frameOrdinal: 0));
            Assert.NotEqual(DumpContextEvidenceStatus.Exact, absent.Status);
            return StaticFieldV2ScopedContextAcquisition.Stopped(
                StaticFieldV2ScopedContextAcquisitionDisposition.Unavailable);
        }

        var frame = SelectSpellingFrame(contextSession);
        if (frame is null)
        {
            return StaticFieldV2ScopedContextAcquisition.Stopped(
                StaticFieldV2ScopedContextAcquisitionDisposition.Unavailable);
        }

        var pdbPath = ShapePortablePdbPath();
        var observation = mode switch
        {
            // The truncated companion is realized through the session's own resolver seam: the real shape PDB is
            // read but only a prefix ending before the ImportScope table is observed, so the session's partial
            // classification is the physical evidence.
            W8CorpusContextMode.TruncatedPdb => contextSession.ReadExpressionPortablePdbContext(
                frame,
                new TruncatedPortablePdbResolver(W8ShapeTargetPaths.RequireArtifact(pdbPath))),

            // The identity-mismatching companion is the request shape's real PDB, whose CodeView identity cannot
            // match this shape's module, exactly as the companion contract declares.
            W8CorpusContextMode.MismatchedPdb => contextSession.ReadExpressionPortablePdbContext(
                frame,
                [
                    W8ShapeTargetPaths.RequireArtifact(Path.ChangeExtension(
                        W8ShapeTargetPaths.ResolveAssembly("PhoenixInspect.W8RequestShapeTarget"),
                        ".pdb")),
                ]),
            _ => contextSession.ReadExpressionPortablePdbContext(
                frame,
                [W8ShapeTargetPaths.RequireArtifact(pdbPath)]),
        };
        if (observation.Status != DumpContextEvidenceStatus.Exact ||
            observation.Facts is not DumpPortablePdbContextFacts facts)
        {
            return StaticFieldV2ScopedContextAcquisition.Stopped(observation.Status switch
            {
                DumpContextEvidenceStatus.Conflict =>
                    StaticFieldV2ScopedContextAcquisitionDisposition.Conflict,
                DumpContextEvidenceStatus.Unavailable =>
                    StaticFieldV2ScopedContextAcquisitionDisposition.Unavailable,
                DumpContextEvidenceStatus.Invalid =>
                    StaticFieldV2ScopedContextAcquisitionDisposition.Invalid,
                _ => StaticFieldV2ScopedContextAcquisitionDisposition.Partial,
            });
        }

        // The selected declaring type is the spelling frame's own physical owner, never a hardcoded one: the
        // contextual and bare routes resolve current-type members against exactly the type the frame is inside.
        var classification = Ancestry.ExactClassificationOrDefault(
            Primary.MetadataModule,
            frame.Frame!.DeclaringTypeDefinitionToken);
        if (classification is null)
        {
            return StaticFieldV2ScopedContextAcquisition.Stopped(
                StaticFieldV2ScopedContextAcquisitionDisposition.Partial);
        }

        // The same catalogs member lookup consumes also decode a TypeSpec alias target, so an alias that names a
        // closed construction reaches it from the physical blob the compiler wrote rather than from the spelling.
        return StaticFieldV2ScopedContextAcquisition.Exact(StaticFieldV2ScopedContextRequest.Create(
            Primary.MetadataModule,
            facts.ImportScopes,
            classification.TypeDefinition,
            Ancestry,
            Ancestry.ResolutionPortfolio,
            TokenResolutionCatalogs));
    }

    /// <summary>
    /// Projects the selected frame's complete lexical envelope from the module's own physical metadata tables and
    /// the shape's identity-validated Portable PDB, exactly as a host would before answering a bare spelling.
    /// </summary>
    /// <returns>The typed observation, or a factless unavailable one when the spelling frame cannot be selected.</returns>
    private DumpSelectedMethodLexicalObservation AcquireLexicalEnvelope()
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var contextSession = opened.Value!;
        var frame = SelectSpellingFrame(contextSession);
        Assert.NotNull(frame);

        var pdbPath = W8ShapeTargetPaths.RequireArtifact(ShapePortablePdbPath());
        var observation = contextSession.ReadExpressionPortablePdbContext(frame!, [pdbPath]);
        if (observation.Status != DumpContextEvidenceStatus.Exact ||
            observation.Facts is not DumpPortablePdbContextFacts facts)
        {
            return DumpSelectedMethodLexicalObservation.Unavailable(
                frame!.Frame!,
                DumpContextEvidenceIssue.PrerequisiteUnavailable,
                []);
        }

        return W8CorpusLexicalEnvelope.Project(
            Primary.Reader,
            W8ShapeTargetPaths.RequireArtifact(PrimaryArtifactPath()),
            pdbPath,
            facts);
    }

    private string ShapePortablePdbPath() => Path.ChangeExtension(PrimaryArtifactPath(), ".pdb");

    private string PrimaryArtifactPath() =>
        W8ShapeTargetPaths.ResolveAssembly("PhoenixInspect.W8" + shape + "ShapeTarget");

    /// <summary>
    /// Realizes the malformed-typespec-blob companion contract: the producer receives a copied metadata image whose
    /// <c>RequestSlot&lt;RequestContext&gt;</c> TypeSpec signature root byte is overwritten with the invalid
    /// <c>ELEMENT_TYPE_END</c>, while the original image bytes stay untouched on disk and in the dump.
    /// </summary>
    /// <remarks>
    /// The mutation is explicit test-owned signature corruption, exactly as the companion contract's acquisition
    /// declares. It targets the one physical TypeSpec row the incident's spelling names, is applied to a copy so the
    /// repair counterfactual is simply the unmutated baseline composition, and locates the blob by its complete
    /// length-prefixed byte sequence, which must occur exactly once in the image so nothing else is corrupted.
    /// </remarks>
    /// <param name="metadataBytes">The primary module's metadata image as read from the dump.</param>
    /// <returns>The copied image with the one mutated signature byte.</returns>
    private static ImmutableArray<byte> RealizeMalformedTypeSpecCompanion(ImmutableArray<byte> metadataBytes)
    {
        using var provider = MetadataReaderProvider.FromMetadataImage(metadataBytes);
        var reader = provider.GetMetadataReader();
        var requestSlotRow = MetadataTokens.GetRowNumber(FindTypeDefinitionHandle(
            reader, "PhoenixInspect.W8RequestShapeTarget", "RequestSlot`1"));
        var requestContextRow = MetadataTokens.GetRowNumber(FindTypeDefinitionHandle(
            reader, "PhoenixInspect.W8RequestShapeTarget", "RequestContext"));

        var blob = FindConstructionTypeSpecificationBlob(reader, requestSlotRow, requestContextRow);

        // The blob is located physically: its complete length-prefixed byte sequence must appear exactly once in
        // the image, so the single mutated byte is provably the root element of the intended signature.
        Assert.True(blob.Length < 0x80, "The fixture TypeSpec blob must carry a single-byte length prefix.");
        byte[] pattern = [(byte)blob.Length, .. blob];
        var image = metadataBytes.ToArray();
        var occurrence = -1;
        for (var index = 0; index <= image.Length - pattern.Length; index++)
        {
            if (image.AsSpan(index, pattern.Length).SequenceEqual(pattern))
            {
                Assert.True(occurrence < 0, "The fixture TypeSpec blob must occur exactly once in the image.");
                occurrence = index;
            }
        }

        Assert.True(occurrence >= 0, "The fixture TypeSpec blob was not found in the metadata image.");
        image[occurrence + 1] = 0x00;
        return [.. image];
    }

    /// <summary>Finds the one TypeSpec blob spelling the closed single-argument fixture construction.</summary>
    /// <param name="reader">The metadata reader over the unmutated image.</param>
    /// <param name="genericDefinitionRow">The TypeDef row number of the generic definition head.</param>
    /// <param name="argumentDefinitionRow">The TypeDef row number of the closed class argument.</param>
    /// <returns>The complete signature bytes of the matching physical TypeSpec row.</returns>
    private static ImmutableArray<byte> FindConstructionTypeSpecificationBlob(
        MetadataReader reader,
        int genericDefinitionRow,
        int argumentDefinitionRow)
    {
        // GENERICINST CLASS <head> 1 CLASS <argument>, with both coded indices naming same-module TypeDefs; this is
        // the exact shape the pinned compiler emits for the fixture spelling, decoded rather than pattern-guessed.
        byte[] expected =
        [
            0x15,
            0x12,
            .. CompressTypeDefOrRef(genericDefinitionRow),
            0x01,
            0x12,
            .. CompressTypeDefOrRef(argumentDefinitionRow),
        ];
        var matches = new List<ImmutableArray<byte>>();
        for (var rowId = 1; rowId <= reader.GetTableRowCount(TableIndex.TypeSpec); rowId++)
        {
            var signature = reader.GetBlobBytes(
                reader.GetTypeSpecification(MetadataTokens.TypeSpecificationHandle(rowId)).Signature);
            if (signature.AsSpan().SequenceEqual(expected))
            {
                matches.Add([.. signature]);
            }
        }

        return Assert.Single(matches);
    }

    /// <summary>Encodes one same-module TypeDef row as an ECMA-335 compressed TypeDefOrRef coded index.</summary>
    /// <param name="rowNumber">The one-based TypeDef row number.</param>
    /// <returns>The compressed coded-index bytes.</returns>
    private static ImmutableArray<byte> CompressTypeDefOrRef(int rowNumber)
    {
        var value = (uint)(rowNumber << 2);
        Assert.True(value <= 0x3FFF, "The fixture TypeDef row must fit a two-byte compressed coded index.");
        return value <= 0x7F ? [(byte)value] : [(byte)(0x80 | (value >> 8)), (byte)value];
    }

    /// <summary>Finds one exact TypeDef handle by namespace and name.</summary>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="namespaceName">The declaring namespace.</param>
    /// <param name="typeName">The metadata type name.</param>
    /// <returns>The single matching TypeDef handle.</returns>
    private static TypeDefinitionHandle FindTypeDefinitionHandle(
        MetadataReader reader,
        string namespaceName,
        string typeName) =>
        Assert.Single(reader.TypeDefinitions, handle =>
        {
            var definition = reader.GetTypeDefinition(handle);
            return reader.GetString(definition.Name) == typeName &&
                reader.GetString(definition.Namespace) == namespaceName;
        });

    /// <summary>
    /// Realizes the truncated-portable-pdb companion contract: the real shape PDB is resolved but only a short
    /// prefix ending before the ImportScope table is observed, so the session classifies the read as partial
    /// source evidence rather than this runner asserting partiality.
    /// </summary>
    private sealed class TruncatedPortablePdbResolver(string pdbPath) : IDumpPortablePdbArtifactResolver
    {
        public ImmutableArray<DumpPortablePdbArtifactRead> Resolve(
            DumpPortablePdbArtifactResolutionRequest request)
        {
            var bytes = ImmutableArray.CreateRange(File.ReadAllBytes(pdbPath));
            return [DumpPortablePdbArtifactRead.Partial("corpus:truncated-portable-pdb", bytes.Length, bytes[..113])];
        }
    }

    /// <summary>
    /// Selects the frame that physically spelled the incident's expression: the caller of the shape's sole pause.
    /// </summary>
    /// <remarks>
    /// Every truth-gate profile enters its pause from exactly one probe or gate method, and that caller frame is the
    /// one whose ImportScope chain, aliases, <c>using static</c> imports, current type, and active locals the
    /// contextual and bare routes must read. Selecting the pause frame itself would resolve names inside the pause
    /// helper, which declares none of them. The caller is found physically — one frame outward on the same thread —
    /// rather than from a per-profile table, so no incident's frame is declared by this runner.
    /// </remarks>
    private DumpSelectedFrameObservation? SelectSpellingFrame(ClrmdDumpSession contextSession)
    {
        var pauseMethodToken = FindMethodToken(Primary.Reader, PrimaryNamespace(), shape + "Pause", "WaitForDump");
        const int maximumThreadOrdinals = 64;
        const int maximumFrameOrdinals = 16;
        for (var threadOrdinal = 0; threadOrdinal < maximumThreadOrdinals; threadOrdinal++)
        {
            for (var frameOrdinal = 0; frameOrdinal < maximumFrameOrdinals; frameOrdinal++)
            {
                var observation = contextSession.SelectExpressionFrame(DumpSelectedFrameSelector.Create(
                    contextSession.Snapshot,
                    threadOrdinal,
                    frameOrdinal));
                if (observation.Frame is { } candidate &&
                    candidate.RuntimeModule.ModuleAddress == Primary.Binding.RuntimeModuleAddress &&
                    candidate.MethodDefinitionToken == pauseMethodToken)
                {
                    var caller = contextSession.SelectExpressionFrame(DumpSelectedFrameSelector.Create(
                        contextSession.Snapshot,
                        threadOrdinal,
                        frameOrdinal + 1));
                    return caller.Frame is { } callerFrame &&
                        callerFrame.RuntimeModule.ModuleAddress == Primary.Binding.RuntimeModuleAddress
                        ? caller
                        : null;
                }

                if (frameOrdinal > 0 &&
                    observation.Status == DumpContextEvidenceStatus.Unavailable &&
                    observation.Issue == DumpContextEvidenceIssue.FrameUnavailable)
                {
                    break;
                }
            }
        }
        return null;
    }

    /// <summary>Evaluates one predeclared expression through the unchanged composed V2 pipeline.</summary>
    /// <param name="expression">The predeclared expression text.</param>
    /// <param name="readWidth">The counted read width in bytes.</param>
    /// <param name="threadSelector">The declared selected-thread predicate, or null when the row declares none.</param>
    /// <param name="scopedContext">The incident's scoped-context seam, or null when the row declares none.</param>
    /// <param name="suppliesConstantCatalogs">
    /// Whether the complete Constant tables are supplied. Only the declared literal counterfactual withholds them.
    /// </param>
    /// <param name="suppliesSuffixEvaluation">
    /// Whether the dump-backed W2/W6 suffix seam is supplied. A row whose spelling carries no suffix never needs it.
    /// </param>
    /// <param name="referenceTargetType">The declared reference target a conflict row validates against, or null.</param>
    /// <returns>The produced evaluation together with the acquired physical address, when one exists.</returns>
    internal W8CorpusEvaluation Evaluate(
        string expression,
        int readWidth,
        StaticFieldV2RuntimeThreadSelector? threadSelector = null,
        StaticFieldV2ScopedContextSource? scopedContext = null,
        bool suppliesConstantCatalogs = true,
        bool suppliesSuffixEvaluation = false,
        MetadataClosedTypeIdentity? referenceTargetType = null)
    {
        var probes = ExpressionV2CapabilityProbeSet.Create();
        ulong? acquiredSlotAddress = null;
        StaticFieldV2ClosedConstructionOutcome? metadataConstruction = null;
        var evidence = StaticFieldV2RuntimeEvidenceSource.Create(
            constructionCandidates: (construction, strategy) =>
            {
                metadataConstruction = construction;
                return Session.AcquireConstruction(
                    StaticFieldV2RuntimeConstructionAcquisitionRequest.Create(
                        construction,
                        strategy,
                        ChainPortfolio,
                        Ancestry,
                        Bindings,
                        probes,
                        CoreIdentityCollapse)).Candidates;
            },
            slotFacts: (strategy, selection) =>
            {
                var facts = AcquireSlotFacts(
                    strategy,
                    selection,
                    metadataConstruction,
                    readWidth,
                    threadSelector,
                    probes);
                if (facts is not null)
                {
                    acquiredSlotAddress = facts.SlotAddress ?? facts.MappedAddress;
                }

                return facts;
            },
            rawMemoryRead: ReadRawBytes);

        var result = StaticFieldV2ExpressionPipeline.Evaluate(StaticFieldV2ExpressionRequest.Create(
            expression,
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            Ancestry,
            Constraints,
            FieldCatalogs,
            scopedContext: scopedContext,
            runtimeEvidence: evidence,
            suffixEvaluation: suppliesSuffixEvaluation ? PausedFrameSuffixEvaluation() : null,
            constantCatalogs: suppliesConstantCatalogs ? ConstantCatalogs : default,
            propertyCatalogs: PropertyCatalogs,
            referenceTargetType: referenceTargetType,
            capabilityProbes: probes,
            signatureTokenResolutionCatalogs: TokenResolutionCatalogs));
        return new W8CorpusEvaluation(result, acquiredSlotAddress);
    }

    internal W8CorpusEvaluation EvaluateWithoutRuntimeEvidence(string expression)
    {
        var probes = ExpressionV2CapabilityProbeSet.Create();
        var result = StaticFieldV2ExpressionPipeline.Evaluate(StaticFieldV2ExpressionRequest.Create(
            expression,
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            Ancestry,
            Constraints,
            FieldCatalogs,
            constantCatalogs: ConstantCatalogs,
            propertyCatalogs: PropertyCatalogs,
            capabilityProbes: probes,
            signatureTokenResolutionCatalogs: TokenResolutionCatalogs));
        return new W8CorpusEvaluation(result, null);
    }

    /// <summary>
    /// Evaluates one <c>FrameValueExpressionV1</c> expression through the composed frame-value entry point, wiring the
    /// caller-owned frame-root evidence seam to the real <c>AcquireFrameValueRoot</c> over the pinned dump session. The
    /// pipeline projects the descriptor root and never references the acquisition session; this runner supplies the
    /// selected paused frame, its Portable-PDB local scopes, and the counted decode of the copied bytes as frame
    /// evidence, exactly as the host would.
    /// </summary>
    /// <param name="expression">The predeclared frame-value expression text.</param>
    /// <param name="readWidth">The counted read width in bytes the predeclared terminal implies.</param>
    /// <param name="localNameOverride">A different declared local name to select for a frame-slot counterfactual.</param>
    /// <returns>The produced evaluation together with the acquired frame-home address, when one exists.</returns>
    internal W8CorpusEvaluation EvaluateFrameValue(
        string expression,
        int readWidth,
        string? localNameOverride = null)
    {
        var probes = ExpressionV2CapabilityProbeSet.Create();
        var selector = FrameSelector("FrameValueProbe", "Run");
        var rows = ReadFrameLocalScopeRows("FrameValueProbe", "Run");
        ulong? acquiredAddress = null;
        var frameSeam = StaticFieldV2FrameRootEvaluationSource.Create(request =>
        {
            StaticFieldV2FrameValueRootKind rootKind;
            string? resolvedLocalName;
            if (request.RootKind == FrameValueV1RootKind.This)
            {
                rootKind = StaticFieldV2FrameValueRootKind.This;
                resolvedLocalName = null;
            }
            else
            {
                rootKind = StaticFieldV2FrameValueRootKind.Local;
                resolvedLocalName = localNameOverride ?? request.Identifier!.DecodedText;
            }

            var outcome = Session.AcquireFrameValueRoot(
                StaticFieldV2FrameValueRootRequest.Create(
                    selector,
                    frameOrdinal: 0,
                    rootKind,
                    rootOrdinal: 0,
                    resolvedLocalName,
                    resolvedLocalName is null ? default : rows,
                    probes));
            acquiredAddress = outcome.RootAddress;
            return MapFrameRootOutcome(outcome, rootKind, readWidth);
        });

        var result = StaticFieldV2ExpressionPipeline.EvaluateFrameValue(StaticFieldV2ExpressionRequest.Create(
            expression,
            DumpExpressionProfileKind.FrameValueExpressionV1,
            Ancestry,
            Constraints,
            FieldCatalogs,
            constantCatalogs: ConstantCatalogs,
            propertyCatalogs: PropertyCatalogs,
            capabilityProbes: probes,
            frameRootEvaluation: frameSeam));
        return new W8CorpusEvaluation(result, acquiredAddress);
    }

    private static StaticFieldV2FrameRootEvaluationResult MapFrameRootOutcome(
        StaticFieldV2FrameValueRootOutcome outcome,
        StaticFieldV2FrameValueRootKind rootKind,
        int readWidth)
    {
        if (outcome.ResultKind == StaticFieldV2RuntimeAcquisitionResultKind.Exact)
        {
            var bytes = outcome.RootBytes;
            var value = DumpQueryValue.FromInt32(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, readWidth)));
            return StaticFieldV2FrameRootEvaluationResult.Exact(
                rootKind,
                outcome.RootAddress!.Value,
                outcome.RootWidth!.Value,
                bytes,
                value);
        }

        return outcome.Issue switch
        {
            StaticFieldV2RuntimeAcquisitionIssue.FrameRegisterHomeNotAdmitted =>
                StaticFieldV2FrameRootEvaluationResult.Stop(
                    StaticFieldV2FrameRootDisposition.RegisterHomeNotAdmitted, outcome.DiagnosticCode),
            StaticFieldV2RuntimeAcquisitionIssue.FrameGenericArgumentNotAdmitted =>
                StaticFieldV2FrameRootEvaluationResult.Stop(
                    StaticFieldV2FrameRootDisposition.GenericArgumentNotAdmitted, outcome.DiagnosticCode),
            StaticFieldV2RuntimeAcquisitionIssue.SelectedThreadAmbiguous =>
                StaticFieldV2FrameRootEvaluationResult.Stop(StaticFieldV2FrameRootDisposition.ContextAmbiguous),
            StaticFieldV2RuntimeAcquisitionIssue.SelectedThreadAbsent or
            StaticFieldV2RuntimeAcquisitionIssue.SelectedFrameAbsent or
            StaticFieldV2RuntimeAcquisitionIssue.FrameInstructionOffsetUnavailable =>
                StaticFieldV2FrameRootEvaluationResult.Stop(StaticFieldV2FrameRootDisposition.ContextUnavailable),
            StaticFieldV2RuntimeAcquisitionIssue.FrameLocalNameAmbiguous =>
                StaticFieldV2FrameRootEvaluationResult.Stop(StaticFieldV2FrameRootDisposition.RootAmbiguous),
            StaticFieldV2RuntimeAcquisitionIssue.FrameLocalLexicallyInactive =>
                StaticFieldV2FrameRootEvaluationResult.Stop(StaticFieldV2FrameRootDisposition.RootShadowed),
            _ => StaticFieldV2FrameRootEvaluationResult.Stop(StaticFieldV2FrameRootDisposition.RootUnavailable),
        };
    }

    private ImmutableArray<StaticFieldV2FramePortablePdbLocalRow> ReadFrameLocalScopeRows(
        string typeName,
        string methodName)
    {
        var methodToken = FindMethodToken(Primary.Reader, PrimaryNamespace(), typeName, methodName);
        var pdbPath = Path.ChangeExtension(
            W8ShapeTargetPaths.ResolveAssembly("PhoenixInspect.W8" + shape + "ShapeTarget"),
            ".pdb");
        using var stream = File.OpenRead(W8ShapeTargetPaths.RequireArtifact(pdbPath));
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();
        var methodHandle = MetadataTokens.MethodDefinitionHandle(methodToken & 0x00ff_ffff);
        var builder = ImmutableArray.CreateBuilder<StaticFieldV2FramePortablePdbLocalRow>();
        foreach (var scopeHandle in reader.GetLocalScopes(methodHandle))
        {
            var scope = reader.GetLocalScope(scopeHandle);
            foreach (var variableHandle in scope.GetLocalVariables())
            {
                var variable = reader.GetLocalVariable(variableHandle);
                builder.Add(StaticFieldV2FramePortablePdbLocalRow.Create(
                    reader.GetString(variable.Name),
                    variable.Index,
                    scope.StartOffset,
                    scope.EndOffset));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Builds the declared selected-thread predicate of the shape's paused truth-gate frame.</summary>
    /// <returns>The token-keyed thread selector of the pause frame.</returns>
    internal StaticFieldV2RuntimeThreadSelector PausedFrameThreadSelector() =>
        FrameSelector(shape + "Pause", "WaitForDump");

    /// <summary>Builds the declared alternative-thread predicate of the request shape's parked worker frame.</summary>
    /// <returns>The token-keyed thread selector of the worker park frame.</returns>
    internal StaticFieldV2RuntimeThreadSelector WorkerParkThreadSelector()
    {
        Assert.Equal("Request", shape);
        return FrameSelector("RequestThreadWorker", "Park");
    }

    public void Dispose()
    {
        foreach (var module in modules)
        {
            module.Dispose();
        }

        layoutRuntime?.Dispose();
        rawReadTarget.Dispose();
        Session.Dispose();
    }

    /// <summary>
    /// Acquires the physical nullable geometry of the selected construction's static field from the dump itself,
    /// exactly as the W8.1 storage truth gate proved it: the static slot holds one pointer to the boxed value, the
    /// box's own header must name the field type's method table as its witness, and the payload begins one pointer
    /// past that header with the <c>hasValue</c> and <c>value</c> child offsets read from the runtime's field rows.
    /// </summary>
    /// <param name="selection">The exact runtime construction selection, or null when none exists.</param>
    /// <param name="fieldDefinitionToken">The exact FieldDef token whose static storage is being read.</param>
    /// <param name="readWidth">The counted payload read width the layout must fill.</param>
    /// <param name="slotAddress">The acquired static slot address holding the box pointer, or null.</param>
    /// <returns>The layout and exact payload address, or null when the field's type is not a nullable value type.</returns>
    private (StaticFieldV2NullableLayoutFact Layout, ulong PayloadAddress)? AcquireNullableGeometry(
        StaticFieldV2RuntimeConstructionSelection? selection,
        int fieldDefinitionToken,
        int readWidth,
        ulong? slotAddress)
    {
        if (selection?.SelectedCandidate is not { } candidate || slotAddress is not { } slot)
        {
            return null;
        }

        layoutRuntime ??= rawReadTarget.ClrVersions[0].CreateRuntime();
        if (layoutRuntime.GetTypeByMethodTable(candidate.MethodTableAddress) is not { } ownerType)
        {
            return null;
        }

        var fieldType = ownerType.StaticFields
            .FirstOrDefault(field => field.Token == fieldDefinitionToken)?.Type;
        if (fieldType is not { IsValueType: true } ||
            fieldType.Fields.Length != 2)
        {
            return null;
        }

        var hasValueField = fieldType.Fields.FirstOrDefault(
            static field => string.Equals(field.Name, "hasValue", StringComparison.Ordinal));
        var valueField = fieldType.Fields.FirstOrDefault(
            static field => string.Equals(field.Name, "value", StringComparison.Ordinal));
        if (hasValueField is null || valueField is null || valueField.Size <= 0)
        {
            return null;
        }

        // The box hop is acquired, never assumed: the slot must hold a nonzero pointer, and the object there must
        // name the field type's own method table before its payload address is trusted.
        var pointerSize = rawReadTarget.DataReader.PointerSize;
        var boxPointerBytes = ReadRawBytes(slot, pointerSize);
        if (boxPointerBytes.Length != pointerSize)
        {
            return null;
        }

        var boxAddress = ReadPointer(boxPointerBytes, pointerSize);
        if (boxAddress == 0)
        {
            return null;
        }

        var headerBytes = ReadRawBytes(boxAddress, pointerSize);
        if (headerBytes.Length != pointerSize ||
            ReadPointer(headerBytes, pointerSize) != fieldType.MethodTable)
        {
            return null;
        }

        return (
            StaticFieldV2NullableLayoutFact.Create(
                storageByteCount: readWidth,
                hasValueOffset: hasValueField.Offset,
                valueOffset: valueField.Offset,
                valueByteCount: valueField.Size),
            checked(boxAddress + (ulong)pointerSize));
    }

    private static ulong ReadPointer(ImmutableArray<byte> bytes, int pointerSize) =>
        pointerSize == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan())
            : BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan());

    private ImmutableArray<byte> ReadRawBytes(ulong address, int width)
    {
        var buffer = new byte[width];
        var read = rawReadTarget.DataReader.Read(address, buffer);
        return read == width ? [.. buffer] : ImmutableArray<byte>.Empty;
    }

    /// <summary>Finds the snapshot's corelib module by its own physical metadata module name.</summary>
    /// <param name="session">The opened runtime acquisition session.</param>
    /// <returns>The corelib runtime module address, or zero when no readable corelib module exists.</returns>
    private static ulong FindCorelibModuleAddress(StaticFieldV2RuntimeAcquisitionSession session)
    {
        foreach (var observation in session.Modules)
        {
            var metadataBytes = session.ReadModuleMetadata(observation.ModuleAddress);
            if (metadataBytes.IsDefaultOrEmpty)
            {
                continue;
            }

            using var provider = MetadataReaderProvider.FromMetadataImage(metadataBytes);
            var reader = provider.GetMetadataReader();
            if (string.Equals(
                reader.GetString(reader.GetModuleDefinition().Name),
                "System.Private.CoreLib.dll",
                StringComparison.Ordinal))
            {
                return observation.ModuleAddress;
            }
        }

        return 0;
    }

    private static ArtifactContent ReadArtifactContent(string artifactPath)
    {
        using var stream = File.OpenRead(artifactPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var bytes = peReader.GetMetadata().GetContent();
        var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
        return new ArtifactContent(bytes, mvid, ModuleContentIdentity.FromMetadata(mvid, bytes.AsSpan()));
    }

    private static ImmutableArray<(string Path, bool Required)> ShapeArtifacts(string shape) =>
        shape switch
        {
            "Request" => [(W8ShapeTargetPaths.ResolveAssembly("PhoenixInspect.W8RequestShapeTarget"), true)],

            // The batch shape references the named-RVA module that owns the module-RVA storage, and the forwarder
            // and alias modules its two same-level imports converge through; every referenced module is composed so
            // the owner types those incidents name are present in each portfolio they consult.
            "Batch" =>
            [
                (W8ShapeTargetPaths.ResolveAssembly("PhoenixInspect.W8BatchShapeTarget"), true),
                (W8ShapeTargetPaths.ResolveNamedRvaFixture(), true),
                (W8ShapeTargetPaths.ResolveCompanionAssembly(
                    "PhoenixInspect.W8BatchShapeTarget",
                    "PhoenixInspect.W8ForwarderTarget"), false),
                (W8ShapeTargetPaths.ResolveCompanionAssembly(
                    "PhoenixInspect.W8BatchShapeTarget",
                    "PhoenixInspect.W8AliasTarget"), false),
            ],
            "Coordinator" => [(W8ShapeTargetPaths.ResolveAssembly("PhoenixInspect.W8CoordinatorShapeTarget"), true)],

            // The workflow shape's extern-alias incident names a type the alias module declares, so that referenced
            // module is composed alongside the shape's own.
            "Workflow" =>
            [
                (W8ShapeTargetPaths.ResolveAssembly("PhoenixInspect.W8WorkflowShapeTarget"), true),
                (W8ShapeTargetPaths.ResolveCompanionAssembly(
                    "PhoenixInspect.W8WorkflowShapeTarget",
                    "PhoenixInspect.W8AliasTarget"), false),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "The shape is not one of the four."),
        };

    private string PrimaryNamespace() => "PhoenixInspect.W8" + shape + "ShapeTarget";

    private StaticFieldV2RuntimeThreadSelector FrameSelector(string typeName, string methodName) =>
        StaticFieldV2RuntimeThreadSelector.Create(
            Primary.Binding.RuntimeModuleAddress,
            FindTypeToken(Primary.Reader, PrimaryNamespace(), typeName),
            FindMethodToken(Primary.Reader, PrimaryNamespace(), typeName, methodName));

    private StaticFieldV2RuntimeSlotFacts? AcquireSlotFacts(
        StaticFieldV2StorageStrategyOutcome strategy,
        StaticFieldV2RuntimeConstructionSelection? selection,
        StaticFieldV2ClosedConstructionOutcome? metadataConstruction,
        int readWidth,
        StaticFieldV2RuntimeThreadSelector? threadSelector,
        ExpressionV2CapabilityProbeSet probes)
    {
        var effectiveStrategy = strategy;
        var effectiveSelection = selection;
        StaticFieldV2RuntimeThreadSelector? selector = null;
        if (strategy.Strategy == StaticFieldV2StorageStrategy.ConstructedSlot &&
            threadSelector is not null &&
            metadataConstruction is not null &&
            FieldCarriesThreadStaticAttribute(strategy.Request.FieldRow))
        {
            // The pipeline classifies without the physical CustomAttribute table; the runner decodes the real
            // ThreadStatic marker row of the produced module and reclassifies for the session acquisition alone.
            // The slot request rejects a construction selection classified under a different strategy, so the runner
            // re-selects the same closed construction under the reclassified thread-relative strategy.
            effectiveStrategy = StaticFieldV2StorageStrategyBinder.ClassifyStrategy(
                StaticFieldV2StorageStrategyRequest.Create(
                    strategy.Request.FieldRow,
                    strategy.Request.DeclaringTypeDefinition,
                    threadStaticAttributeSuppliedByCaller: true));
            Assert.Equal(StaticFieldV2StorageStrategy.ThreadRelativeSlot, effectiveStrategy.Strategy);
            var reacquired = Session.AcquireConstruction(
                StaticFieldV2RuntimeConstructionAcquisitionRequest.Create(
                    metadataConstruction,
                    effectiveStrategy,
                    ChainPortfolio,
                    Ancestry,
                    Bindings,
                    probes,
                    CoreIdentityCollapse));
            if (reacquired.ResultKind != StaticFieldV2RuntimeAcquisitionResultKind.Exact)
            {
                return null;
            }

            effectiveSelection = reacquired.Selection;
            selector = threadSelector;
        }

        StaticFieldV2RuntimeModuleBinding? declaringBinding = null;
        if (strategy.Strategy == StaticFieldV2StorageStrategy.ModuleRva)
        {
            declaringBinding = BindingFor(strategy.Request.FieldRow);
        }

        var slot = Session.AcquireStaticSlot(
            StaticFieldV2StaticSlotAcquisitionRequest.Create(
                effectiveStrategy,
                readWidth,
                constructionSelection: effectiveSelection,
                threadSelector: selector,
                declaringModuleBinding: declaringBinding,
                capabilityProbes: probes));
        if (slot.ResultKind != StaticFieldV2RuntimeAcquisitionResultKind.Exact)
        {
            return null;
        }

        // The pipeline's own strategy never carries the selected-thread fact: the CustomAttribute table is a declared
        // coverage boundary there, so its constructed-slot plan admits the exact address alone. A nullable static's
        // geometry is acquired from the dump itself — the box hop the W8.1 truth gate proved — so the supplied slot
        // address is the payload the raw read must copy and the layout carries the runtime's own child offsets.
        var nullableGeometry = AcquireNullableGeometry(
            effectiveSelection,
            strategy.Request.FieldRow.FieldDefinitionToken,
            readWidth,
            slot.SlotAddress);
        return StaticFieldV2RuntimeSlotFacts.Create(
            readWidth,
            nullableGeometry?.PayloadAddress ?? slot.SlotAddress,
            selectedThread: null,
            moduleContent: declaringBinding?.MetadataModule.ModuleContent,
            fieldRvaRowToken: slot.FieldRvaRowToken,
            mappedRelativeVirtualAddress: slot.MappedRelativeVirtualAddress,
            mappedAddress: slot.MappedAddress,
            nullableLayout: nullableGeometry?.Layout);
    }

    private bool FieldCarriesThreadStaticAttribute(MetadataFieldDefinitionTableRowIdentity fieldRow)
    {
        var reader = ReaderFor(fieldRow);
        var handle = (FieldDefinitionHandle)MetadataTokens.Handle(fieldRow.FieldDefinitionToken);
        foreach (var attributeHandle in reader.GetFieldDefinition(handle).GetCustomAttributes())
        {
            var constructor = reader.GetCustomAttribute(attributeHandle).Constructor;
            string? namespaceName;
            string? typeName;
            switch (constructor.Kind)
            {
                case HandleKind.MemberReference:
                    var parent = reader.GetMemberReference((MemberReferenceHandle)constructor).Parent;
                    if (parent.Kind != HandleKind.TypeReference)
                    {
                        continue;
                    }

                    var typeReference = reader.GetTypeReference((TypeReferenceHandle)parent);
                    namespaceName = reader.GetString(typeReference.Namespace);
                    typeName = reader.GetString(typeReference.Name);
                    break;
                case HandleKind.MethodDefinition:
                    var declaringType = reader.GetTypeDefinition(
                        reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType());
                    namespaceName = reader.GetString(declaringType.Namespace);
                    typeName = reader.GetString(declaringType.Name);
                    break;
                default:
                    continue;
            }

            if (string.Equals(namespaceName, "System", StringComparison.Ordinal) &&
                string.Equals(typeName, "ThreadStaticAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private MetadataReader ReaderFor(MetadataFieldDefinitionTableRowIdentity fieldRow)
    {
        foreach (var module in modules)
        {
            if (module.MetadataModule.Equals(fieldRow.SourceEnds.SourceModule))
            {
                return module.Reader;
            }
        }

        throw new InvalidOperationException("The field row names a module this world did not produce.");
    }

    private StaticFieldV2RuntimeModuleBinding BindingFor(MetadataFieldDefinitionTableRowIdentity fieldRow)
    {
        foreach (var module in modules)
        {
            if (module.MetadataModule.Equals(fieldRow.SourceEnds.SourceModule))
            {
                return module.Binding;
            }
        }

        throw new InvalidOperationException("The field row names a module this world did not produce.");
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

        throw new InvalidOperationException($"The shape module declares no type {namespaceName}.{typeName}.");
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
            $"The shape type {namespaceName}.{typeName} declares no method {methodName}.");
    }

    private static readonly BoundedEcmaSignatureLimits TokenScanLimits = new(
        StaticFieldV2Limits.MaximumTypeSpecificationByteCount,
        StaticFieldV2Limits.MaximumTypeSpecificationDepth,
        StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
        StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
        StaticFieldV2Limits.MaximumGenericParameterCount,
        StaticFieldV2Limits.MaximumParameterCount,
        StaticFieldV2Limits.MaximumLocalCount,
        StaticFieldV2Limits.MaximumArrayRank);

    /// <summary>
    /// Builds one signature token-resolution catalog per portfolio module that retains a decodable TypeSpec blob,
    /// supplying exactly the entries those blobs reference. This is the host-side acquisition role: the catalogs are
    /// derived from the already-produced authority portfolios and the physical blob bytes, so member lookup can
    /// decode a retained generic base — and the context projection a TypeSpec alias target — without any new
    /// metadata read.
    /// </summary>
    /// <remarks>
    /// Both consumers are served from one catalog per module. A generic base blob is reached through the module's
    /// base edges; an alias target is any row of the module's own complete TypeSpec table, and which of those rows an
    /// ImportScope actually names is not known until the context is projected, so every row contributes its
    /// referenced tokens. Over-supplying is safe: an entry no blob references is never consulted, while a missing
    /// entry would turn an exact decode into a typed stop.
    /// </remarks>
    private static ImmutableArray<MetadataSignatureTokenResolutionCatalog> BuildTokenResolutionCatalogs(
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs)
    {
        var catalogs = ImmutableArray.CreateBuilder<MetadataSignatureTokenResolutionCatalog>();
        foreach (var entry in ancestry.Entries)
        {
            var module = entry.SourceModule;
            var tokens = new SortedSet<int>();
            foreach (var baseEdge in entry.BaseEdges)
            {
                if (baseEdge.GenericBaseRow is { } baseRow)
                {
                    CollectSignatureTokens(baseRow.Observation.SignatureBytes, tokens);
                }
            }
            foreach (var typeSpecificationRow in entry.ResolutionEntry.ReferenceTables.TypeSpecifications.Rows)
            {
                CollectSignatureTokens(typeSpecificationRow.Observation.SignatureBytes, tokens);
            }
            foreach (var fieldCatalog in fieldCatalogs)
            {
                if (!fieldCatalog.SourceEnds.SourceModule.Equals(module))
                {
                    continue;
                }
                foreach (var fieldRow in fieldCatalog.Rows)
                {
                    CollectSignatureTokens(fieldRow.SignatureBytes, tokens, BoundedEcmaSignatureForm.Field);
                }
            }
            if (tokens.Count == 0)
            {
                continue;
            }

            var entries = ImmutableArray.CreateBuilder<MetadataSignatureTokenResolutionEntry>();
            foreach (var token in tokens)
            {
                if (ResolveTokenEntry(ancestry, module, token) is { } resolved)
                {
                    entries.Add(resolved);
                }
            }
            var authority = entry.ResolutionEntry.ChainEntry.ChainCatalog.DefinitionAuthority;
            catalogs.Add(MetadataSignatureTokenResolutionCatalog.Create(
                authority.SourceEnds,
                entries.ToImmutable()));
        }
        return catalogs.ToImmutable();
    }

    private static void CollectSignatureTokens(
        ImmutableArray<byte> signatureBytes,
        SortedSet<int> tokens,
        BoundedEcmaSignatureForm form = BoundedEcmaSignatureForm.TypeSpecification)
    {
        var sink = new TokenCollectingSink(tokens);
        BoundedEcmaSignatureProjection.Decode(
            signatureBytes.AsSpan(),
            form,
            TokenScanLimits,
            sink);
    }

    private static MetadataSignatureTokenResolutionEntry? ResolveTokenEntry(
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        StaticFieldMetadataModuleIdentity module,
        int token) =>
        (token >> 24) switch
        {
            0x02 when BuildClassificationChain(ancestry, module, token) is { IsDefault: false } chain =>
                MetadataSignatureTokenResolutionEntry.Named(
                    MetadataTypeDefOrRefTargetIdentity.FromTypeDefinition(chain[^1]),
                    chain),
            0x01 when ancestry.ResolutionPortfolio.ExactResolutionOrDefault(module, token) is
                {
                    Disposition: MetadataTypeReferenceResolutionDispositionKind.Resolved,
                    TargetModule: { } targetModule,
                    TargetTypeDefinition: { } targetDefinition,
                } resolution &&
                BuildClassificationChain(ancestry, targetModule, targetDefinition.TypeDefinitionToken) is
                { IsDefault: false } targetChain =>
                MetadataSignatureTokenResolutionEntry.Named(
                    MetadataTypeDefOrRefTargetIdentity.FromTypeReference(resolution, targetChain[^1]),
                    targetChain),
            0x1B => MetadataSignatureTokenResolutionEntry.TypeSpecification(
                MetadataTypeSpecificationRowReferenceIdentity.Create(module, token)),
            _ => null,
        };

    private static ImmutableArray<MetadataTypeDefinitionSemanticClassificationIdentity> BuildClassificationChain(
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionToken)
    {
        var chain = new List<MetadataTypeDefinitionSemanticClassificationIdentity>();
        var current = ancestry.ExactClassificationOrDefault(module, typeDefinitionToken);
        while (current is not null)
        {
            chain.Insert(0, current);
            if (current.TypeDefinition.EnclosingTypeDefinitionToken is not { } enclosing)
            {
                return [.. chain];
            }
            current = ancestry.ExactClassificationOrDefault(module, enclosing);
        }
        return default;
    }

    private sealed class TokenCollectingSink : IBoundedEcmaSignatureNodeSink
    {
        private readonly SortedSet<int> tokens;

        internal TokenCollectingSink(SortedSet<int> tokens) => this.tokens = tokens;

        public void Add(in BoundedEcmaSignatureNodeEvent node)
        {
            if (node.MetadataToken != 0)
            {
                tokens.Add(node.MetadataToken);
            }
        }
    }

    private static SyntheticCoreModule BuildSyntheticCoreModule(
        StaticFieldV2RuntimeAcquisitionSession session,
        StaticFieldMetadataModuleIdentity targetModule)
    {
        var assemblyDefinition = ReadSharedFrameworkAssemblyDefinition("System.Runtime.dll");
        var moduleInstance = StaticFieldModuleInstanceIdentity.Create(
            session.Evidence.SnapshotSha256,
            session.Evidence.PointerWidth,
            targetModule.Module.ApplicationDomainAddress,
            SyntheticCoreModuleAddress,
            imageBase: 0,
            imageSize: 0);
        var content = ModuleContentIdentity.FromDigest(
            mvid: Guid.Parse("7e007e00-7e00-7e00-7e00-7e007e007e02"),
            metadataLength: 4_096,
            metadataSha256: new string('e', 64));
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            generation: 0,
            name: "w8-corpus-synthetic-core.dll",
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

        // Int32 and Nullable`1 are declared alongside the role definitions because closed nullable spellings such
        // as global::System.Nullable<global::System.Int32> must bind their argument and their nullable head to one
        // exact core definition each; both extend ValueType so ancestry classifies them as value types.
        var namedTypes = new (string NamespaceName, string TypeName, int? Extends)[]
        {
            ("System", "Object", null),
            ("System", "ValueType", 0x0200_0002),
            ("System", "Enum", 0x0200_0003),
            ("System", "Delegate", 0x0200_0002),
            ("System", "MulticastDelegate", 0x0200_0005),
            ("System", "Int32", 0x0200_0003),
            ("System", "Nullable`1", 0x0200_0003),
        };
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: moduleInstance,
                moduleContent: content,
                typeDefinitionsExamined: namedTypes.Length + 1,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: namedTypes.Length + 1,
                fieldDefinitionRowCount: 0,
                genericParameterRowCount: 1,
                declaredMemberRowCounts: StaticFieldModuleDeclaredMemberRowCounts.Create(
                    constantRowCount: 0,
                    propertyMapRowCount: 0,
                    propertyPointerRowCount: 0)));
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
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [
                MetadataGenericParameterRowObservationIdentity.Create(
                    module,
                    genericParameterToken: 0x2A_00_0001,
                    number: 0,
                    flags: 0,
                    ownerMetadataToken: 0x0200_0008,
                    name: "T"),
            ]);
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
        Assert.Equal(MetadataGenericParameterConstraintPhysicalTableResultKind.Exact, constraints.ResultKind);
        var fieldCatalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            ImmutableArray<MetadataFieldDefinitionRowObservationIdentity>.Empty);
        var declaredMemberSourceEnds = MetadataDeclaredMemberSourceEndIdentity.Create(sourceEnds);
        var constantCatalog = MetadataConstantTableCatalogIdentity.Create(
            declaredMemberSourceEnds,
            fieldCatalog,
            ImmutableArray<MetadataConstantRowObservationIdentity>.Empty);
        Assert.Equal(MetadataConstantTableResultKind.Exact, constantCatalog.ResultKind);
        var propertyCatalog = MetadataPropertyTableCatalogIdentity.Create(
            declaredMemberSourceEnds,
            authority,
            ImmutableArray<MetadataPropertyRowObservationIdentity>.Empty);
        Assert.Equal(MetadataPropertyTableResultKind.Exact, propertyCatalog.ResultKind);
        return new SyntheticCoreModule(
            module,
            compatibility,
            chainCatalog,
            tables,
            constraints,
            fieldCatalog,
            constantCatalog,
            propertyCatalog);
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

    private sealed record ArtifactContent(ImmutableArray<byte> Bytes, Guid Mvid, ModuleContentIdentity Content);

    private sealed record SyntheticCoreModule(
        StaticFieldMetadataModuleIdentity Module,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity Compatibility,
        MetadataNamedTypeDefinitionChainCatalogIdentity ChainCatalog,
        MetadataModuleReferenceTableSetIdentity Tables,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Constraints,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog,
        MetadataConstantTableCatalogIdentity ConstantCatalog,
        MetadataPropertyTableCatalogIdentity PropertyCatalog);

    /// <summary>Produces the composed metadata authority of one real module observed in the dump.</summary>
    /// <remarks>This is physical scaffolding and not a product contract.</remarks>
    private sealed class ProducedModule : IDisposable
    {
        private readonly MetadataReaderProvider provider;

        private ProducedModule(
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

        internal static ProducedModule Bind(
            StaticFieldV2RuntimeAcquisitionSession session,
            StaticFieldV2RuntimeModuleObservation observation,
            Func<ImmutableArray<byte>, ImmutableArray<byte>>? metadataMutation = null)
        {
            var metadataBytes = session.ReadModuleMetadata(observation.ModuleAddress);
            if (metadataMutation is not null)
            {
                metadataBytes = metadataMutation(metadataBytes);
            }

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
                return new ProducedModule(provider, reader, metadataModule, outcome, binding);
            }
            catch
            {
                provider.Dispose();
                throw;
            }
        }

        public void Dispose() => provider.Dispose();
    }
}
