using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Discharges the W8.9 section 8.2 requirement that two fresh hidden consumers emit byte-identical machine and human
/// portfolio reports over the predeclared corpus, with human output restricted to shape-only counts and verdicts so
/// no unstable address or fixture-specific value can leak.
/// </summary>
/// <remarks>
/// The report is a pure deterministic function of the frozen manifest. It runs in the Fast lane because it consults
/// only the predeclared corpus metadata, never a materialized dump or a produced runtime value.
/// </remarks>
public sealed class W8CorpusPortfolioReportTests
{
    /// <summary>
    /// Proves two independently loaded consumers emit byte-identical machine and human reports and freezes each
    /// report's canonical digest.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Two_fresh_consumers_emit_byte_identical_machine_and_human_reports()
    {
        var first = W8CorpusPortfolioReport.Build(W8CorpusManifest.Load());
        var second = W8CorpusPortfolioReport.Build(W8CorpusManifest.Load());

        Assert.Equal(first.MachineReport, second.MachineReport, StringComparer.Ordinal);
        Assert.Equal(first.HumanReport, second.HumanReport, StringComparer.Ordinal);
        Assert.True(
            Encoding.UTF8.GetBytes(first.MachineReport).AsSpan()
                .SequenceEqual(Encoding.UTF8.GetBytes(second.MachineReport)));
        Assert.True(
            Encoding.UTF8.GetBytes(first.HumanReport).AsSpan()
                .SequenceEqual(Encoding.UTF8.GetBytes(second.HumanReport)));

        // The report summarizes the manifest, so its digests are re-frozen whenever a row's runner-execution status
        // legitimately changes. That status is runner bookkeeping, never a predeclared axis: this pair records
        // twenty-one executed and fourteen manifest-only rows. The two rows that became drivable are the workflow
        // var-substitution and array-argument rows, whose shared blocker was the workflow gate materializing its
        // second-definition companion for every profile although only one row declares that companion as an input;
        // the array row additionally needed its counted root read corrected to one pointer for a reference terminal.
        // Both reach their predeclared axes unchanged, so no predeclared axis moved.
        Assert.Equal(
            "bdb9bb478b274ed2719e9210397edce2a4fc065eac30d8ef8ea5f30d06dfe34d",
            Digest(first.MachineReport));
        Assert.Equal(
            "91fa1debc991cdadf07584d9a17993c871fcefc4b6f5adb49e62bbd1d5adbae6",
            Digest(first.HumanReport));
        Assert.Equal(21, first.ExecutedCount);
        Assert.Equal(14, first.ManifestOnlyCount);
    }

    /// <summary>
    /// Proves the report's aggregates equal the raw counts the corpus runner independently asserts, so the report
    /// cannot silently drift from the portfolio it summarizes.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Portfolio_report_counts_match_the_predeclared_corpus()
    {
        var report = W8CorpusPortfolioReport.Build(W8CorpusManifest.Load());

        Assert.Equal(35, report.IncidentCount);
        Assert.Equal(0, report.RepresentativeCount);
        Assert.Equal(33, report.UsefulCount);
        Assert.Equal(26, report.DecisionChangingCount);
        Assert.Equal(19, report.AttributableCount);
        Assert.Equal(4, report.ShapeCounts.Count);
        Assert.All(report.ShapeCounts.Values, count => Assert.True(count >= 8));
        Assert.Equal(35, report.ShapeCounts.Values.Sum());
        Assert.Equal(
            new[] { "additional-static-storage-family", "observed-boundary-hardening" },
            report.QualifiedCategories.ToArray());

        // The report's tie verdict must equal an independent recomputation over its own qualified tallies, whatever
        // that verdict is; the exact value is pinned by the frozen report golden rather than hardcoded here.
        var qualifiedTallies = report.Categories
            .Where(tally => report.QualifiedCategories.Contains(tally.Category))
            .ToArray();
        Assert.Equal(W8CorpusCategoryTally.Defers(qualifiedTallies), report.TieDefers);
        Assert.Equal(35, report.ExecutedCount + report.ManifestOnlyCount);
    }

    /// <summary>
    /// Proves neither report leaks an unstable runtime value: no hexadecimal address, no decoded terminal spelling,
    /// and no fixture-specific expression text appears in either report.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reports_expose_only_shape_only_counts_and_verdicts()
    {
        var manifest = W8CorpusManifest.Load();
        var report = W8CorpusPortfolioReport.Build(manifest);

        foreach (var text in new[] { report.MachineReport, report.HumanReport })
        {
            Assert.DoesNotContain("0x", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("i32:", text, StringComparison.Ordinal);
            Assert.DoesNotContain("i64:", text, StringComparison.Ordinal);
            foreach (var incident in manifest.Incidents)
            {
                Assert.DoesNotContain(incident.Expression, text, StringComparison.Ordinal);
                if (incident.ExpectedTerminal is { } terminal)
                {
                    Assert.DoesNotContain(terminal, text, StringComparison.Ordinal);
                }
            }
        }
    }

    private static string Digest(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>
/// Emits deterministic machine and human portfolio reports over the predeclared W8.9 corpus manifest, as the
/// section 8.2 dual-consumer requirement demands.
/// </summary>
/// <remarks>
/// Every field is a count or a verdict derived from the frozen manifest; the emitter records no address, decoded
/// value, or expression text so two fresh consumers produce byte-identical output.
/// </remarks>
public sealed class W8CorpusPortfolioReport
{
    private const char Newline = '\n';

    private W8CorpusPortfolioReport(
        string corpusId,
        int schemaVersion,
        int incidentCount,
        int representativeCount,
        int usefulCount,
        int decisionChangingCount,
        int attributableCount,
        int executedCount,
        int manifestOnlyCount,
        ImmutableSortedDictionary<string, int> shapeCounts,
        ImmutableArray<W8CorpusCategoryTally> categories,
        ImmutableArray<string> qualifiedCategories,
        bool tieDefers)
    {
        CorpusId = corpusId;
        SchemaVersion = schemaVersion;
        IncidentCount = incidentCount;
        RepresentativeCount = representativeCount;
        UsefulCount = usefulCount;
        DecisionChangingCount = decisionChangingCount;
        AttributableCount = attributableCount;
        ExecutedCount = executedCount;
        ManifestOnlyCount = manifestOnlyCount;
        ShapeCounts = shapeCounts;
        Categories = categories;
        QualifiedCategories = qualifiedCategories;
        TieDefers = tieDefers;
        MachineReport = BuildMachineReport();
        HumanReport = BuildHumanReport();
    }

    /// <summary>Gets the frozen corpus identifier.</summary>
    public string CorpusId { get; }

    /// <summary>Gets the frozen manifest schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the total predeclared incident count.</summary>
    public int IncidentCount { get; }

    /// <summary>Gets the representative-observation count, which must be zero.</summary>
    public int RepresentativeCount { get; }

    /// <summary>Gets the count of incidents predeclared useful.</summary>
    public int UsefulCount { get; }

    /// <summary>Gets the count of incidents predeclared decision-changing.</summary>
    public int DecisionChangingCount { get; }

    /// <summary>Gets the count of incidents with predeclared attributable evidence.</summary>
    public int AttributableCount { get; }

    /// <summary>Gets the count of incidents the runner executes end to end today.</summary>
    public int ExecutedCount { get; }

    /// <summary>Gets the count of incidents that remain manifest-only today.</summary>
    public int ManifestOnlyCount { get; }

    /// <summary>Gets the incident count per application shape in shape-name order.</summary>
    public ImmutableSortedDictionary<string, int> ShapeCounts { get; }

    /// <summary>Gets the per-successor-category tally in category-name order.</summary>
    public ImmutableArray<W8CorpusCategoryTally> Categories { get; }

    /// <summary>Gets the qualified successor categories in category-name order.</summary>
    public ImmutableArray<string> QualifiedCategories { get; }

    /// <summary>Gets whether the qualified set defers rather than selecting one successor action.</summary>
    public bool TieDefers { get; }

    /// <summary>Gets the canonical machine report as a deterministic newline-delimited key/value document.</summary>
    public string MachineReport { get; }

    /// <summary>Gets the canonical human report as deterministic shape-only prose.</summary>
    public string HumanReport { get; }

    /// <summary>Builds one portfolio report from the frozen corpus manifest.</summary>
    /// <param name="manifest">The predeclared corpus manifest.</param>
    /// <returns>A deterministic report over the manifest's counts and verdicts.</returns>
    public static W8CorpusPortfolioReport Build(W8CorpusManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var root = manifest.Root;
        var incidents = manifest.Incidents;

        var shapeCounts = incidents
            .GroupBy(static incident => incident.Shape, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(
                static group => group.Key,
                static group => group.Count(),
                StringComparer.Ordinal);

        var qualification = root.GetProperty("successorQualification");
        var minimumIncidents = qualification.GetProperty("minimumIncidents").GetInt32();
        var minimumShapes = qualification.GetProperty("minimumApplicationShapes").GetInt32();
        var minimumChanging = qualification.GetProperty("minimumDecisionChangingIncidents").GetInt32();

        var categories = root.GetProperty("successorCategories").EnumerateArray()
            .Select(category => W8CorpusManifest.RequiredString(category, "category"))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                var rows = incidents.Where(incident => incident.SupportsSuccessorCategory == name).ToArray();
                return new W8CorpusCategoryTally(
                    name,
                    rows.Length,
                    rows.Select(static row => row.Shape).Distinct(StringComparer.Ordinal).Count(),
                    rows.Count(static row => row.DecisionChanging),
                    rows.Count(static row => row.AttributableEvidence));
            })
            .ToImmutableArray();

        var qualified = categories
            .Where(tally =>
                tally.IncidentCount >= minimumIncidents &&
                tally.ApplicationShapeCount >= minimumShapes &&
                tally.DecisionChangingCount >= minimumChanging)
            .ToImmutableArray();

        return new W8CorpusPortfolioReport(
            W8CorpusManifest.RequiredString(root, "corpusId"),
            root.GetProperty("schemaVersion").GetInt32(),
            incidents.Length,
            incidents.Count(static incident => incident.Representative),
            incidents.Count(static incident => incident.UsefulnessValue),
            incidents.Count(static incident => incident.DecisionChanging),
            incidents.Count(static incident => incident.AttributableEvidence),
            incidents.Count(static incident => incident.RunnerExecutionStatus == "executed"),
            incidents.Count(static incident => incident.RunnerExecutionStatus != "executed"),
            shapeCounts,
            categories,
            [.. qualified.Select(static tally => tally.Category).OrderBy(static name => name, StringComparer.Ordinal)],
            W8CorpusCategoryTally.Defers(qualified));
    }

    private string BuildMachineReport()
    {
        var lines = new List<string>
        {
            Line("corpus.id", CorpusId),
            Line("corpus.incident-count", IncidentCount),
            Line("corpus.representative-count", RepresentativeCount),
            Line("corpus.schema-version", SchemaVersion),
            Line("metric.attributable", AttributableCount),
            Line("metric.decision-changing", DecisionChangingCount),
            Line("metric.useful", UsefulCount),
            Line("runner.executed", ExecutedCount),
            Line("runner.manifest-only", ManifestOnlyCount),
        };
        foreach (var shape in ShapeCounts)
        {
            lines.Add(Line($"shape.{shape.Key}", shape.Value));
        }
        foreach (var tally in Categories)
        {
            lines.Add(Line($"category.{tally.Category}.incidents", tally.IncidentCount));
            lines.Add(Line($"category.{tally.Category}.shapes", tally.ApplicationShapeCount));
            lines.Add(Line($"category.{tally.Category}.decision-changing", tally.DecisionChangingCount));
            lines.Add(Line($"category.{tally.Category}.attributable", tally.AttributableEvidenceCount));
            lines.Add(Line(
                $"category.{tally.Category}.qualified",
                QualifiedCategories.Contains(tally.Category) ? "true" : "false"));
        }
        lines.Add(Line("selection.qualified", string.Join(",", QualifiedCategories)));
        lines.Add(Line("selection.tie-defers", TieDefers ? "true" : "false"));
        lines.Add(Line("selection.selected-action", "none"));
        return string.Join(Newline, lines) + Newline;
    }

    private string BuildHumanReport()
    {
        var builder = new StringBuilder();
        builder.Append("W8 meaningful-synthetic portfolio report").Append(Newline);
        builder.Append("Corpus: ").Append(CorpusId).Append(" (schema ").Append(Number(SchemaVersion)).Append(')')
            .Append(Newline);
        builder.Append("Incidents: ").Append(Number(IncidentCount)).Append(" across ")
            .Append(Number(ShapeCounts.Count)).Append(" shapes; representative observations: ")
            .Append(Number(RepresentativeCount)).Append(Newline);
        builder.Append("Shape distribution: ")
            .Append(string.Join(", ", ShapeCounts.Select(shape => $"{shape.Key} {Number(shape.Value)}")))
            .Append(Newline);
        builder.Append("Decision metrics: ").Append(Number(UsefulCount)).Append(" useful, ")
            .Append(Number(DecisionChangingCount)).Append(" decision-changing, ")
            .Append(Number(AttributableCount)).Append(" attributable").Append(Newline);
        builder.Append("Runner execution: ").Append(Number(ExecutedCount)).Append(" executed, ")
            .Append(Number(ManifestOnlyCount)).Append(" manifest-only").Append(Newline);
        builder.Append("Successor categories:").Append(Newline);
        foreach (var tally in Categories)
        {
            builder.Append("  ").Append(tally.Category).Append(": ")
                .Append(Number(tally.IncidentCount)).Append(" incidents, ")
                .Append(Number(tally.ApplicationShapeCount)).Append(" shapes, ")
                .Append(Number(tally.DecisionChangingCount)).Append(" decision-changing, ")
                .Append(Number(tally.AttributableEvidenceCount)).Append(" attributable -> ")
                .Append(QualifiedCategories.Contains(tally.Category) ? "qualified" : "not qualified")
                .Append(Newline);
        }
        builder.Append("Qualified: ")
            .Append(QualifiedCategories.IsEmpty ? "none" : string.Join(", ", QualifiedCategories))
            .Append(Newline);
        builder.Append(TieDefers
                ? "Tie policy: defers (no single category is strictly ahead on the frozen keys)"
                : "Tie policy: selects the single leading category")
            .Append(Newline);
        builder.Append("Selected successor action: none (deferred)").Append(Newline);
        return builder.ToString();
    }

    private static string Line(string key, int value) => $"{key}={Number(value)}";

    private static string Line(string key, string value) => $"{key}={value}";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
