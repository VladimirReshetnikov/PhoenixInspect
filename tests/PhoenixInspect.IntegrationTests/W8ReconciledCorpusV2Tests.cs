using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Verifies the derived W8.9 v2 execution reconciliation without changing the frozen v1 predeclaration.
/// </summary>
/// <remarks>
/// V1 remains the historical designed-evidence record. V2 is deliberately narrower: it content-addresses v1 and
/// freezes only the thirteen outcomes that the existing physical corpus produced differently. The twenty-two v1
/// rows that already executed are inherited, so the v2 execution view contains thirty-five executable baselines.
/// </remarks>
public sealed class W8ReconciledCorpusV2Tests
{
    private static readonly string[] AxisNames =
    [
        "syntax", "context", "rootAttribution", "lexicalCompleteness", "typeBinding", "typeConstruction",
        "memberLookup", "runtimeConstruction", "storage", "value", "suffix", "completeness",
    ];

    /// <summary>
    /// Proves v2 is a content-addressed overlay over exactly the thirteen v1 manifest-only rows, never a rewritten
    /// copy that can silently erase the original produced-versus-predeclared findings.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W8MeaningfulSyntheticV2")]
    public void Manifest_preserves_v1_and_reconciles_exactly_its_thirteen_divergent_rows()
    {
        var v1 = W8CorpusManifest.Load();
        using var v2 = W8ReconciledCorpusV2Manifest.Load();

        Assert.Equal(2, v2.SchemaVersion);
        Assert.Equal("interpreter-w8-static-field-incidents-v2", v2.CorpusId);
        Assert.Equal("interpreter-w8-static-field-incidents-v1", v2.BaseCorpusId);
        Assert.Equal(1, v2.BaseSchemaVersion);
        Assert.Equal(v2.BaseSha256, W8ReconciledCorpusV2Manifest.ComputeBaseSha256());

        var v1ManifestOnly = v1.Incidents
            .Where(static incident => incident.RunnerExecutionStatus == "manifest-only")
            .OrderBy(static incident => incident.Ordinal)
            .ToArray();
        Assert.Equal(13, v1ManifestOnly.Length);
        Assert.Equal(
            v1ManifestOnly.Select(static incident => incident.Id),
            v2.Corrections.Select(static correction => correction.Id));
        Assert.Equal(
            v1ManifestOnly.Select(static incident => incident.Ordinal),
            v2.Corrections.Select(static correction => correction.Ordinal));

        var execution = v2.Root.GetProperty("runnerExecution");
        Assert.Equal("executable", W8CorpusManifest.RequiredString(execution, "status"));
        Assert.Equal(35, execution.GetProperty("expectedIncidentCount").GetInt32());
        Assert.Equal(22, execution.GetProperty("inheritedExecutedIncidentCount").GetInt32());
        Assert.Equal(13, execution.GetProperty("correctedExecutedIncidentCount").GetInt32());
        Assert.Equal(0, execution.GetProperty("expectedManifestOnlyIncidentCount").GetInt32());
        Assert.Equal(35, v1.Incidents.Count(static row => row.RunnerExecutionStatus == "executed") + v2.Corrections.Length);

        var dispositionKinds = W8CorpusManifest.ReadStrings(
            v2.Root.GetProperty("counterfactualDispositionKinds"));
        foreach (var correction in v2.Corrections)
        {
            var v1Row = Assert.Single(v1ManifestOnly, row => row.Id == correction.Id);
            Assert.NotEqual(v1Row.PredeclaredAxes, correction.ExpectedProducedAxes);
            Assert.NotEmpty(correction.DivergentAxes);
            Assert.Equal(
                correction.DivergentAxes.ToArray(),
                AxisNames.Where(axis => !string.Equals(
                    W8CorpusIncident.AxisText(v1Row.PredeclaredAxes, axis),
                    W8CorpusIncident.AxisText(correction.ExpectedProducedAxes, axis),
                    StringComparison.Ordinal)).ToArray());
            Assert.Contains(correction.CounterfactualDisposition, dispositionKinds);
            Assert.False(string.IsNullOrWhiteSpace(correction.Evidence));
        }

        Assert.Equal(0, v2.Root.GetProperty("representativeObservationCount").GetInt32());
        Assert.DoesNotContain("\"representative\": true", v2.RawText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Materializes all thirty-five rows from their unchanged v1 target invocations and proves their produced axes
    /// equal either their already-verified v1 axes or the v2 correction. Inheritance supplies expectations only:
    /// every row owns a fresh independent full dump in this v2 execution.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W8MeaningfulSyntheticV2")]
    public void All_thirty_five_rows_materialize_and_reach_their_reconciled_produced_axes()
    {
        var v1 = W8CorpusManifest.Load();
        using var v2 = W8ReconciledCorpusV2Manifest.Load();
        var corrections = v2.Corrections.ToDictionary(static correction => correction.Id, StringComparer.Ordinal);

        var executed = 0;
        foreach (var incident in v1.Incidents)
        {
            corrections.TryGetValue(incident.Id, out var correction);
            using var snapshot = W8CorpusSnapshot.Materialize(incident);
            using var world = W8CorpusEvaluationWorld.Open(
                snapshot.DumpPath,
                incident.Shape,
                correction?.MalformedTypeSpecCompanion ?? false);
            var produced = Evaluate(world, incident, correction);
            var expectedAxes = correction?.ExpectedProducedAxes ?? incident.PredeclaredAxes;

            Assert.True(
                expectedAxes.Equals(produced.Result.Axes),
                $"{incident.Id}: expected {Describe(expectedAxes)} but produced " +
                $"{Describe(produced.Result.Axes)}.");

            var terminal = correction?.ExpectedProducedTerminal ?? incident.ExpectedTerminal;
            if (terminal is not null &&
                terminal.StartsWith("i32:", StringComparison.Ordinal))
            {
                Assert.Equal(
                    long.Parse(terminal["i32:".Length..], CultureInfo.InvariantCulture),
                    produced.Result.SignedValue);
            }
            else if (terminal is not null && terminal.StartsWith("i64:", StringComparison.Ordinal))
            {
                Assert.Equal(
                    long.Parse(terminal["i64:".Length..], CultureInfo.InvariantCulture),
                    produced.Result.SignedValue);
            }
            else if (terminal is not null && terminal.StartsWith("enum-i32:", StringComparison.Ordinal))
            {
                Assert.Equal(
                    long.Parse(terminal["enum-i32:".Length..], CultureInfo.InvariantCulture),
                    produced.Result.SignedValue);
            }
            else if (terminal is not null && terminal.StartsWith("string:", StringComparison.Ordinal))
            {
                Assert.Equal(
                    terminal["string:".Length..],
                    Assert.IsType<DumpQueryValue>(produced.Result.SuffixValue).StringValue);
            }
            else if (string.Equals(terminal, "null", StringComparison.Ordinal))
            {
                Assert.Null(produced.Result.SignedValue);
            }

            executed++;
        }

        Assert.Equal(35, executed);
    }

    /// <summary>
    /// Freezes two deterministic v2 report views. The report distinguishes baseline execution closure from the
    /// counterfactual dispositions that still require owner decisions or a new physical circumstance.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W8MeaningfulSyntheticV2")]
    public void Two_independently_loaded_manifests_emit_byte_identical_reconciliation_reports()
    {
        using var firstManifest = W8ReconciledCorpusV2Manifest.Load();
        using var secondManifest = W8ReconciledCorpusV2Manifest.Load();
        var first = W8ReconciledCorpusV2Report.Build(firstManifest);
        var second = W8ReconciledCorpusV2Report.Build(secondManifest);

        Assert.Equal(first.MachineReport, second.MachineReport, StringComparer.Ordinal);
        Assert.Equal(first.HumanReport, second.HumanReport, StringComparer.Ordinal);
        Assert.Equal(35, first.ExecutedCount);
        Assert.Equal(0, first.ManifestOnlyCount);
        Assert.Equal(13, first.CorrectedCount);
        Assert.Equal(7, first.OwnerDispositionCount);
        Assert.Equal(2, first.NewCircumstanceCount);
        Assert.Equal(45, first.DivergentAxisCount);

        Assert.Equal(
            "1975aa5860d76bad3b96bcd72e2db05d8f13d40977f0fb473075fa44b113f264",
            Digest(first.MachineReport));
        Assert.Equal(
            "2318defd62dc375c654eb8d862e84c4fa459fc40ca7b7a595bcf037a4bea6979",
            Digest(first.HumanReport));
    }

    private static W8CorpusEvaluation Evaluate(
        W8CorpusEvaluationWorld world,
        W8CorpusIncident incident,
        W8ReconciledCorpusV2Correction? correction)
    {
        if (incident.LanguageProfile == "FrameValueExpressionV1")
        {
            return world.EvaluateFrameValue(incident.Expression, incident.ReadWidth);
        }

        Assert.Equal("StaticFieldExpressionV2", incident.LanguageProfile);
        var requestsContext = incident.SelectedFrameMode != "none" &&
            !incident.Expression.StartsWith("global::", StringComparison.Ordinal);
        var contextMode = incident.SelectedFrameMode == "requested-but-absent"
            ? W8CorpusContextMode.FrameAbsent
            : incident.PortablePdbInput == "partial"
                ? W8CorpusContextMode.TruncatedPdb
                : incident.PortablePdbInput == "conflicting"
                    ? W8CorpusContextMode.MismatchedPdb
                    : W8CorpusContextMode.Exact;
        var referenceTarget = correction?.ReferenceTargetType is { } typeName
            ? world.DeclaredReferenceTarget(typeName)
            : null;
        return world.Evaluate(
            incident.Expression,
            incident.ReadWidth,
            incident.RequestsPausedFrameThread ? world.PausedFrameThreadSelector() : null,
            requestsContext ? world.PausedFrameScopedContext(contextMode) : null,
            suppliesSuffixEvaluation: incident.SuffixProfile != "None",
            referenceTargetType: referenceTarget);
    }

    private static string Describe(DumpExpressionV2OutcomeAxes axes) =>
        string.Join('/', AxisNames.Select(axis => W8CorpusIncident.AxisText(axes, axis)));

    private static string Digest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

/// <summary>Loads the content-addressed v2 execution-reconciliation overlay.</summary>
internal sealed class W8ReconciledCorpusV2Manifest : IDisposable
{
    private const string RelativePath = "tests/corpus/w8-static-field-incidents-v2.json";
    private readonly JsonDocument document;

    private W8ReconciledCorpusV2Manifest(JsonDocument document, string rawText)
    {
        this.document = document;
        RawText = rawText;
        var root = document.RootElement;
        SchemaVersion = root.GetProperty("schemaVersion").GetInt32();
        CorpusId = W8CorpusManifest.RequiredString(root, "corpusId");
        var baseCorpus = root.GetProperty("baseCorpus");
        BaseCorpusId = W8CorpusManifest.RequiredString(baseCorpus, "corpusId");
        BaseSchemaVersion = baseCorpus.GetProperty("schemaVersion").GetInt32();
        BaseSha256 = W8CorpusManifest.RequiredString(baseCorpus, "sha256");
        Corrections =
        [
            .. root.GetProperty("corrections").EnumerateArray()
                .Select(static correction => new W8ReconciledCorpusV2Correction(correction)),
        ];
    }

    internal JsonElement Root => document.RootElement;

    internal int SchemaVersion { get; }

    internal string CorpusId { get; }

    internal string BaseCorpusId { get; }

    internal int BaseSchemaVersion { get; }

    internal string BaseSha256 { get; }

    internal string RawText { get; }

    internal ImmutableArray<W8ReconciledCorpusV2Correction> Corrections { get; }

    internal static W8ReconciledCorpusV2Manifest Load()
    {
        var text = File.ReadAllText(W8ShapeTargetPaths.RepositoryPath(RelativePath));
        return new W8ReconciledCorpusV2Manifest(JsonDocument.Parse(text), text);
    }

    internal static string ComputeBaseSha256()
    {
        var bytes = File.ReadAllBytes(W8ShapeTargetPaths.RepositoryPath(
            "tests/corpus/w8-static-field-incidents-v1.json"));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public void Dispose() => document.Dispose();
}

/// <summary>One measured correction layered over a frozen v1 incident identity.</summary>
internal sealed class W8ReconciledCorpusV2Correction
{
    internal W8ReconciledCorpusV2Correction(JsonElement element)
    {
        Id = W8CorpusManifest.RequiredString(element, "id");
        Ordinal = element.GetProperty("ordinal").GetInt32();
        DivergentAxes = W8CorpusManifest.ReadStrings(element.GetProperty("divergentAxes"));
        ExpectedProducedAxes = ParseAxes(element.GetProperty("expectedProducedAxes"));
        ExpectedProducedFirstBoundary = W8CorpusManifest.RequiredString(element, "expectedProducedFirstBoundary");
        ExpectedProducedTerminal = element.GetProperty("expectedProducedTerminal").ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty("expectedProducedTerminal").GetString();
        CounterfactualDisposition = W8CorpusManifest.RequiredString(element, "counterfactualDisposition");
        Evidence = W8CorpusManifest.RequiredString(element, "evidence");
        if (element.TryGetProperty("evaluationOverrides", out var overrides))
        {
            MalformedTypeSpecCompanion = overrides.TryGetProperty("malformedTypeSpecCompanion", out var malformed) &&
                malformed.GetBoolean();
            ReferenceTargetType = overrides.TryGetProperty("referenceTargetType", out var target)
                ? target.GetString()
                : null;
        }
    }

    internal string Id { get; }

    internal int Ordinal { get; }

    internal ImmutableArray<string> DivergentAxes { get; }

    internal DumpExpressionV2OutcomeAxes ExpectedProducedAxes { get; }

    internal string ExpectedProducedFirstBoundary { get; }

    internal string? ExpectedProducedTerminal { get; }

    internal string CounterfactualDisposition { get; }

    internal string Evidence { get; }

    internal bool MalformedTypeSpecCompanion { get; }

    internal string? ReferenceTargetType { get; }

    private static DumpExpressionV2OutcomeAxes ParseAxes(JsonElement axes) =>
        DumpExpressionV2OutcomeAxes.Create(
            Enum.Parse<DumpExpressionSyntaxStatus>(W8CorpusManifest.RequiredString(axes, "syntax")),
            Enum.Parse<DumpExpressionContextOutcome>(W8CorpusManifest.RequiredString(axes, "context")),
            Enum.Parse<DumpExpressionRootAttributionOutcome>(W8CorpusManifest.RequiredString(axes, "rootAttribution")),
            Enum.Parse<DumpExpressionLexicalCompletenessOutcome>(W8CorpusManifest.RequiredString(axes, "lexicalCompleteness")),
            Enum.Parse<DumpExpressionTypeBindingOutcome>(W8CorpusManifest.RequiredString(axes, "typeBinding")),
            Enum.Parse<DumpExpressionTypeConstructionOutcome>(W8CorpusManifest.RequiredString(axes, "typeConstruction")),
            Enum.Parse<DumpExpressionMemberLookupOutcome>(W8CorpusManifest.RequiredString(axes, "memberLookup")),
            Enum.Parse<DumpExpressionRuntimeConstructionOutcome>(W8CorpusManifest.RequiredString(axes, "runtimeConstruction")),
            Enum.Parse<DumpExpressionStorageOutcome>(W8CorpusManifest.RequiredString(axes, "storage")),
            Enum.Parse<DumpExpressionValueOutcome>(W8CorpusManifest.RequiredString(axes, "value")),
            Enum.Parse<DumpExpressionSuffixOutcome>(W8CorpusManifest.RequiredString(axes, "suffix")),
            Enum.Parse<DumpExpressionCompletenessOutcome>(W8CorpusManifest.RequiredString(axes, "completeness")));
}

/// <summary>Deterministic machine and human reports over the v2 reconciliation overlay.</summary>
internal sealed class W8ReconciledCorpusV2Report
{
    private W8ReconciledCorpusV2Report(
        string corpusId,
        string baseCorpusId,
        string baseSha256,
        int correctedCount,
        int divergentAxisCount,
        ImmutableSortedDictionary<string, int> dispositions)
    {
        CorrectedCount = correctedCount;
        DivergentAxisCount = divergentAxisCount;
        ExecutedCount = 35;
        ManifestOnlyCount = 0;
        OwnerDispositionCount = dispositions
            .Where(static pair => pair.Key.Contains("owner-disposition-required", StringComparison.Ordinal))
            .Sum(static pair => pair.Value);
        NewCircumstanceCount = dispositions.GetValueOrDefault(
            "new-physical-circumstance-or-owner-disposition-required");

        var machine = new List<string>
        {
            $"corpus.id={corpusId}",
            "corpus.schema-version=2",
            $"base.id={baseCorpusId}",
            $"base.sha256={baseSha256}",
            "runner.executed=35",
            "runner.manifest-only=0",
            "runner.inherited=22",
            $"runner.corrected={Number(correctedCount)}",
            $"correction.divergent-axis-count={Number(divergentAxisCount)}",
        };
        machine.AddRange(dispositions.Select(static pair =>
            $"counterfactual.{pair.Key}={Number(pair.Value)}"));
        machine.Add("representative.count=0");
        MachineReport = string.Join('\n', machine) + '\n';

        HumanReport =
            "W8 meaningful-synthetic execution reconciliation v2\n" +
            $"Base: {baseCorpusId} at SHA-256 {baseSha256}\n" +
            $"Baseline execution: 35 executed, 0 manifest-only (22 inherited, {Number(correctedCount)} corrected)\n" +
            $"Corrected produced-axis differences: {Number(divergentAxisCount)}\n" +
            $"Counterfactual owner dispositions: {Number(OwnerDispositionCount)}; new physical circumstances: " +
            $"{Number(NewCircumstanceCount)}\n" +
            "Representative observations: 0\n";
    }

    internal int ExecutedCount { get; }

    internal int ManifestOnlyCount { get; }

    internal int CorrectedCount { get; }

    internal int DivergentAxisCount { get; }

    internal int OwnerDispositionCount { get; }

    internal int NewCircumstanceCount { get; }

    internal string MachineReport { get; }

    internal string HumanReport { get; }

    internal static W8ReconciledCorpusV2Report Build(W8ReconciledCorpusV2Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var dispositions = manifest.Corrections
            .GroupBy(static correction => correction.CounterfactualDisposition, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(
                static group => group.Key,
                static group => group.Count(),
                StringComparer.Ordinal);
        return new W8ReconciledCorpusV2Report(
            manifest.CorpusId,
            manifest.BaseCorpusId,
            manifest.BaseSha256,
            manifest.Corrections.Length,
            manifest.Corrections.Sum(static correction => correction.DivergentAxes.Length),
            dispositions);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
