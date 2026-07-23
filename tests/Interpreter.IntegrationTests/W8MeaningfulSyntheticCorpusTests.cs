using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace Interpreter.IntegrationTests;

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
    /// Materializes one independent full dump per executable request-shape incident, evaluates it through the composed
    /// V2 pipeline over the produced metadata authority, and compares the produced twelve axes to the predeclared row.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV1")]
    public void Executable_request_shape_incidents_reach_their_predeclared_axes()
    {
        var manifest = W8CorpusManifest.Load();
        var executable = manifest.Incidents
            .Where(static incident => incident.RunnerExecutionStatus == "executed")
            .ToArray();
        Assert.NotEmpty(executable);
        Assert.All(executable, incident => Assert.Equal("Request", incident.Shape));

        var disagreements = new List<string>();
        foreach (var incident in executable)
        {
            using var snapshot = W8CorpusSnapshot.Materialize(incident);
            using var world = W8CorpusEvaluationWorld.Open(snapshot.DumpPath, incident.Shape);
            var produced = world.Evaluate(incident.Expression, incident.ReadWidth);

            if (!produced.Result.Axes.Equals(incident.PredeclaredAxes))
            {
                disagreements.Add(
                    $"{incident.Id}: predeclared {Describe(incident.PredeclaredAxes)} but produced " +
                    $"{Describe(produced.Result.Axes)}.");
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
            var baseline = world.Evaluate(incident.Expression, incident.ReadWidth);
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

    private static W8CorpusEvaluation ApplyCounterfactual(W8CorpusEvaluationWorld world, W8CorpusIncident incident) =>
        incident.CounterfactualAction switch
        {
            "withhold-runtime-evidence" => world.EvaluateWithoutRuntimeEvidence(incident.Expression),
            "substitute-closed-type-argument" => world.Evaluate(
                incident.CounterfactualExpression ?? incident.Expression,
                incident.ReadWidth),
            "select-different-thread" => world.EvaluateWithoutRuntimeEvidence(incident.Expression),
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

    internal W8CorpusIncident(JsonElement element)
    {
        Id = W8CorpusManifest.RequiredString(element, "id");
        Ordinal = element.GetProperty("ordinal").GetInt32();
        Shape = W8CorpusManifest.RequiredString(element, "shape");
        PlannedQuestion = W8CorpusManifest.RequiredString(element, "plannedQuestion");
        SnapshotKind = W8CorpusManifest.RequiredString(element, "snapshotKind");
        SnapshotIdentity = W8CorpusManifest.RequiredString(element, "snapshotIdentity");
        Expression = W8CorpusManifest.RequiredString(element, "expression");
        LanguageProfile = W8CorpusManifest.RequiredString(element, "languageProfile");
        ExpectedFirstBoundary = W8CorpusManifest.RequiredString(element, "expectedFirstBoundary");
        SupportsSuccessorCategory = W8CorpusManifest.RequiredString(element, "supportsSuccessorCategory");
        Representative = element.GetProperty("representative").GetBoolean();

        var target = element.GetProperty("target");
        AssemblyName = W8CorpusManifest.RequiredString(target, "assemblyName");
        TruthGateProfile = W8CorpusManifest.RequiredString(target, "truthGateProfile");
        TargetArguments = W8CorpusManifest.ReadStrings(target.GetProperty("targetArguments"));

        ArtifactInputs = W8CorpusManifest.ReadStrings(element.GetProperty("inputs").GetProperty("artifactInputs"));
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

    /// <summary>Gets the explicitly selected language profile.</summary>
    public string LanguageProfile { get; }

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

    /// <summary>Gets the counted read width the predeclared terminal implies.</summary>
    public int ReadWidth => ExpectedTerminal is { } terminal && terminal.StartsWith("i64:", StringComparison.Ordinal)
        ? sizeof(long)
        : sizeof(int);

    /// <summary>Gets the substituted expression a closed-argument counterfactual evaluates, when one applies.</summary>
    public string? CounterfactualExpression => CounterfactualAction == "substitute-closed-type-argument"
        ? SubstituteClosedArgument()
        : null;

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
/// The authority is composed from the produced target module plus one synthetic core module carrying the real
/// <c>System.Runtime</c> assembly identity, because the pinned corelib is rejected by the shared bounded ECMA
/// signature grammar. This scaffolding is draft physical evidence, not a product discovery rule.
/// </remarks>
internal sealed class W8CorpusEvaluationWorld : IDisposable
{
    private const int PublicClassAttributes = 0x0000_0001;
    private const ulong SyntheticCoreModuleAddress = 0x0000_7E00_0000_0002UL;

    private readonly MetadataReaderProvider provider;
    private readonly DataTarget rawReadTarget;

    private W8CorpusEvaluationWorld(
        StaticFieldV2RuntimeAcquisitionSession session,
        DataTarget rawReadTarget,
        MetadataReaderProvider provider,
        MetadataModuleAcquisitionOutcome outcome,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        StaticFieldV2RuntimeModuleBinding binding,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        MetadataConstraintTargetResolutionPortfolioIdentity constraints)
    {
        Session = session;
        this.rawReadTarget = rawReadTarget;
        this.provider = provider;
        Outcome = outcome;
        FieldCatalogs = fieldCatalogs;
        Bindings = [binding];
        ChainPortfolio = chainPortfolio;
        Ancestry = ancestry;
        Constraints = constraints;
    }

    internal StaticFieldV2RuntimeAcquisitionSession Session { get; }

    internal MetadataModuleAcquisitionOutcome Outcome { get; }

    internal ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs { get; }

    internal ImmutableArray<StaticFieldV2RuntimeModuleBinding> Bindings { get; }

    internal MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio { get; }

    internal MetadataAncestryAuthorityPortfolioIdentity Ancestry { get; }

    internal MetadataConstraintTargetResolutionPortfolioIdentity Constraints { get; }

    internal static W8CorpusEvaluationWorld Open(string dumpPath, string shape)
    {
        var assemblyName = shape switch
        {
            "Request" => "Interpreter.W8RequestShapeTarget",
            "Batch" => "Interpreter.W8BatchShapeTarget",
            "Coordinator" => "Interpreter.W8CoordinatorShapeTarget",
            "Workflow" => "Interpreter.W8WorkflowShapeTarget",
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "The shape is not one of the four."),
        };

        var session = StaticFieldV2RuntimeAcquisitionSession.Open(dumpPath);
        try
        {
            var rawReadTarget = DataTarget.LoadDump(
                dumpPath,
                new DataTargetOptions { FileLocator = ClrmdOfflineFileLocator.Instance });
            try
            {
                var artifact = ReadArtifactContent(
                    W8ShapeTargetPaths.RequireArtifact(W8ShapeTargetPaths.ResolveAssembly(assemblyName)));
                var observation = Assert.Single(session.Modules.Where(candidate =>
                    candidate.MetadataLength == (ulong)artifact.Bytes.Length &&
                    ModuleContentIdentity
                        .FromMetadata(artifact.Mvid, session.ReadModuleMetadata(candidate.ModuleAddress).AsSpan())
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

                    var core = BuildSyntheticCoreModule(session, metadataModule);
                    var compatibility = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                        [core.Compatibility, outcome.Compatibility!]);
                    var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
                        compatibility,
                        [core.ChainCatalog, outcome.ChainCatalog!]);
                    var resolution = MetadataTypeReferenceResolutionPortfolioIdentity.Create(
                        chainPortfolio,
                        [core.Tables, outcome.ReferenceTables!]);
                    var ancestry = MetadataAncestryAuthorityPortfolioIdentity.Create(resolution);
                    var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
                        resolution,
                        [core.Constraints, outcome.GenericParameterConstraints!]);
                    Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestry.ResultKind);
                    Assert.Equal(
                        MetadataConstraintTargetResolutionPortfolioResultKind.Exact,
                        constraints.ResultKind);

                    return new W8CorpusEvaluationWorld(
                        session,
                        rawReadTarget,
                        provider,
                        outcome,
                        [core.FieldCatalog, outcome.FieldDefinitions!],
                        binding,
                        chainPortfolio,
                        ancestry,
                        constraints);
                }
                catch
                {
                    provider.Dispose();
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

    internal W8CorpusEvaluation Evaluate(string expression, int readWidth)
    {
        var probes = ExpressionV2CapabilityProbeSet.Create();
        ulong? acquiredSlotAddress = null;
        var evidence = StaticFieldV2RuntimeEvidenceSource.Create(
            constructionCandidates: (construction, strategy) => Session.AcquireConstruction(
                StaticFieldV2RuntimeConstructionAcquisitionRequest.Create(
                    construction,
                    strategy,
                    ChainPortfolio,
                    Ancestry,
                    Bindings,
                    probes)).Candidates,
            slotFacts: (strategy, selection) =>
            {
                var slot = Session.AcquireStaticSlot(
                    StaticFieldV2StaticSlotAcquisitionRequest.Create(
                        strategy,
                        readWidth,
                        constructionSelection: selection,
                        capabilityProbes: probes));
                if (slot.ResultKind != StaticFieldV2RuntimeAcquisitionResultKind.Exact)
                {
                    return null;
                }

                acquiredSlotAddress = slot.SlotAddress;
                return StaticFieldV2RuntimeSlotFacts.Create(
                    readWidth,
                    slot.SlotAddress,
                    slot.SelectedThread,
                    fieldRvaRowToken: slot.FieldRvaRowToken,
                    mappedRelativeVirtualAddress: slot.MappedRelativeVirtualAddress,
                    mappedAddress: slot.MappedAddress);
            },
            rawMemoryRead: ReadRawBytes);

        var result = StaticFieldV2ExpressionPipeline.Evaluate(StaticFieldV2ExpressionRequest.Create(
            expression,
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            Ancestry,
            Constraints,
            FieldCatalogs,
            runtimeEvidence: evidence,
            capabilityProbes: probes));
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
            capabilityProbes: probes));
        return new W8CorpusEvaluation(result, null);
    }

    public void Dispose()
    {
        provider.Dispose();
        rawReadTarget.Dispose();
        Session.Dispose();
    }

    private ImmutableArray<byte> ReadRawBytes(ulong address, int width)
    {
        var buffer = new byte[width];
        var read = rawReadTarget.DataReader.Read(address, buffer);
        return read == width ? [.. buffer] : ImmutableArray<byte>.Empty;
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
        Assert.Equal(MetadataGenericParameterConstraintPhysicalTableResultKind.Exact, constraints.ResultKind);
        var fieldCatalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            ImmutableArray<MetadataFieldDefinitionRowObservationIdentity>.Empty);
        return new SyntheticCoreModule(module, compatibility, chainCatalog, tables, constraints, fieldCatalog);
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
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog);
}
