using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Materializes W6.6's predeclared twenty-four-incident portfolio and validates its append-only usefulness report and
/// post-W6 decision floor through hidden processes.
/// </summary>
public sealed class W6MeaningfulSyntheticPortfolioIntegrationTests
{
    /// <summary>
    /// Uses one fresh target, full dump, and consumer per incident across four graph shapes, then requires two fresh
    /// portfolio processes to produce byte-identical raw-count reports and the same uniquely qualified next action.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainV1")]
    [Trait("Corpus", "W6MeaningfulSyntheticV3")]
    public void Predeclared_twenty_four_incidents_select_one_qualified_post_W6_action()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var portfolioPath = Path.Combine(
            repositoryRoot,
            "tests",
            "corpus",
            "w6-usefulness-meaningful-synthetic-v3.json");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"w6-meaningful-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            using var portfolio = JsonDocument.Parse(File.ReadAllBytes(portfolioPath));
            var incidents = portfolio.RootElement.GetProperty("incidents").EnumerateArray().ToArray();
            Assert.Equal(24, incidents.Length);
            Assert.Equal(24, incidents.Select(GetIncidentId).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                4,
                incidents.Select(static incident => incident.GetProperty("applicationShape").GetString())
                    .Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                4,
                incidents.Select(static incident => incident.GetProperty("targetRootTypeName").GetString())
                    .Distinct(StringComparer.Ordinal).Count());

            var snapshots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var incident in incidents)
            {
                MaterializeIncident(repositoryRoot, outputDirectory, incident, snapshots);
            }

            Assert.Equal(24, snapshots.Count);
            var firstMachine = Path.Combine(outputDirectory, "first.w6-usefulness.machine.json");
            var firstHuman = Path.Combine(outputDirectory, "first.w6-usefulness.human.txt");
            var secondMachine = Path.Combine(outputDirectory, "second.w6-usefulness.machine.json");
            var secondHuman = Path.Combine(outputDirectory, "second.w6-usefulness.human.txt");
            RunPortfolio(repositoryRoot, portfolioPath, outputDirectory, firstMachine, firstHuman);
            RunPortfolio(repositoryRoot, portfolioPath, outputDirectory, secondMachine, secondHuman);

            Assert.Equal(File.ReadAllBytes(firstMachine), File.ReadAllBytes(secondMachine));
            Assert.Equal(File.ReadAllBytes(firstHuman), File.ReadAllBytes(secondHuman));
            AssertMachineReport(firstMachine);
            AssertHumanReport(firstHuman);
            AssertSubstantiveTieDefers(repositoryRoot, portfolioPath, outputDirectory);
            AssertDesignedRowsCannotBePromoted(repositoryRoot, portfolioPath, outputDirectory);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static void MaterializeIncident(
        string repositoryRoot,
        string outputDirectory,
        JsonElement incident,
        HashSet<string> snapshots)
    {
        var id = GetIncidentId(incident);
        var arguments = incident.GetProperty("targetArguments")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();
        var dumpPath = Path.Combine(outputDirectory, $"{id}.dmp");
        using (var target = TestTargetRunner.StartAndWaitReady(
                   TestTargetPaths.ResolveExecutable(),
                   arguments,
                   isolatedDirectory: null))
        {
            DumpWriter.WriteFullDump(target.Pid, dumpPath);
        }

        var manifestPath = Path.Combine(outputDirectory, $"{id}.scenario.json");
        WriteScenarioManifest(manifestPath, incident);
        var machinePath = Path.Combine(outputDirectory, incident.GetProperty("reportPath").GetString()!);
        var humanPath = Path.Combine(outputDirectory, "synthetic-w6-reports", $"{id}.human.txt");
        RunConsumer(repositoryRoot, manifestPath, dumpPath, machinePath, humanPath);
        AssertIncidentReport(machinePath, humanPath, incident, snapshots);
    }

    private static void WriteScenarioManifest(string path, JsonElement incident)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 2);
        writer.WriteString("corpusKind", "SyntheticIncident");
        writer.WriteString("dumpPath", "__w6_synthetic_dump_override__");
        var root = incident.GetProperty("root");
        writer.WriteStartObject("root");
        writer.WriteString("name", root.GetProperty("name").GetString());
        writer.WriteString("typeName", root.GetProperty("typeName").GetString());
        writer.WriteNumber("maximumMatches", root.GetProperty("maximumMatches").GetInt32());
        writer.WriteNumber("maximumHandlesScanned", root.GetProperty("maximumHandlesScanned").GetInt32());
        writer.WriteString("fixtureEvidenceView", root.GetProperty("fixtureEvidenceView").GetString());
        writer.WriteEndObject();
        writer.WriteStartArray("scenarios");
        incident.GetProperty("scenario").WriteTo(writer);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void AssertIncidentReport(
        string machinePath,
        string humanPath,
        JsonElement incident,
        HashSet<string> snapshots)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(machinePath));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("machineSchemaVersion").GetInt32());
        Assert.Equal("SyntheticIncident", root.GetProperty("corpusKind").GetString());
        Assert.Contains(
            "not external observations",
            root.GetProperty("fixtureCaveat").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(incident.GetProperty("root").GetProperty("typeName").GetString(), root.GetProperty("rootTypeName").GetString());
        var snapshot = root.GetProperty("dumpSnapshotSha256").GetString()!;
        Assert.Equal(64, snapshot.Length);
        Assert.True(snapshots.Add(snapshot), "Each W6 incident requires a distinct dump snapshot.");
        Assert.Equal(
            incident.GetProperty("root").GetProperty("expectedBindingStatus").GetString(),
            root.GetProperty("rootSelection").GetProperty("bindingStatus").GetString());

        var scenario = Assert.Single(root.GetProperty("scenarios").EnumerateArray());
        var expected = incident.GetProperty("expected");
        var outcome = scenario.GetProperty("outcome");
        Assert.Equal(expected.GetProperty("outcomeKind").GetString(), outcome.GetProperty("kind").GetString());
        AssertNullable(expected, outcome, "semanticMode");
        AssertNullable(expected, outcome, "completion");
        AssertNullable(expected, outcome, "completeness");
        AssertNullable(expected, outcome, "evidence");
        AssertNullable(expected, outcome, "value");
        Assert.Equal(
            expected.GetProperty("diagnosticCodes").EnumerateArray().Select(static item => item.GetString()).ToArray(),
            outcome.GetProperty("diagnostics").EnumerateArray()
                .Select(static item => item.GetProperty("code").GetString()).ToArray());

        var human = File.ReadAllText(humanPath);
        Assert.Contains("W6 expression-facade report v2", human, StringComparison.Ordinal);
        var targetArguments = incident.GetProperty("targetArguments").EnumerateArray()
            .Select(static item => item.GetString()!)
            .Skip(1)
            .ToArray();
        Assert.All(targetArguments, value => Assert.DoesNotContain(value, human, StringComparison.Ordinal));
        if (expected.GetProperty("value").ValueKind == JsonValueKind.String)
        {
            var canonicalValue = expected.GetProperty("value").GetString()!;
            if (canonicalValue.StartsWith("s16:", StringComparison.Ordinal) ||
                canonicalValue.StartsWith("i32:", StringComparison.Ordinal))
            {
                Assert.DoesNotContain(canonicalValue, human, StringComparison.Ordinal);
            }
        }
    }

    private static void AssertNullable(JsonElement expected, JsonElement actual, string name)
    {
        var expectedValue = expected.GetProperty(name);
        var actualValue = actual.GetProperty(name);
        if (expectedValue.ValueKind == JsonValueKind.Null)
        {
            Assert.Equal(JsonValueKind.Null, actualValue.ValueKind);
        }
        else
        {
            Assert.Equal(expectedValue.GetString(), actualValue.GetString());
        }
    }

    private static void AssertMachineReport(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(3, root.GetProperty("usefulnessReportSchemaVersion").GetInt32());
        Assert.Equal(3, root.GetProperty("portfolioSchemaVersion").GetInt32());
        Assert.Equal("w6-usefulness-meaningful-synthetic-v3", root.GetProperty("portfolioId").GetString());
        Assert.Equal("SyntheticIncident", root.GetProperty("corpusKind").GetString());
        Assert.True(root.GetProperty("predeclaredBeforeEvaluation").GetBoolean());
        Assert.False(root.GetProperty("claimsProductionReadiness").GetBoolean());
        Assert.Contains(
            "not external observations",
            root.GetProperty("evidenceScopeCaveat").GetString(),
            StringComparison.Ordinal);
        var reports = root.GetProperty("evaluationReports").EnumerateArray().ToArray();
        Assert.Equal(24, reports.Length);
        Assert.Equal(
            24,
            reports.Select(static report => report.GetProperty("dumpSnapshotSha256").GetString())
                .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            4,
            reports.Select(static report => report.GetProperty("targetRootTypeName").GetString())
                .Distinct(StringComparer.Ordinal).Count());

        var questions = root.GetProperty("questions").EnumerateArray().ToArray();
        Assert.Equal(24, questions.Length);
        Assert.Equal(17, questions.Count(static question => question.GetProperty("admission").GetString() == "W6"));
        Assert.Equal(1, questions.Count(static question => question.GetProperty("admission").GetString() == "ContextRejected"));
        Assert.Equal(6, questions.Count(static question => question.GetProperty("admission").GetString() == "Unsupported"));
        var counts = root.GetProperty("rawCounts").GetProperty("allRows");
        Assert.Equal(24, counts.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(24, counts.GetProperty("distinctIncidents").GetInt32());
        Assert.Equal(4, counts.GetProperty("distinctApplicationShapes").GetInt32());
        Assert.Equal(4, counts.GetProperty("distinctTargetRootTypes").GetInt32());
        AssertRatio(counts.GetProperty("admission").GetProperty("admitted"), 17, 24);
        Assert.Equal(17, counts.GetProperty("admission").GetProperty("w6").GetInt32());
        Assert.Equal(1, counts.GetProperty("admission").GetProperty("contextRejected").GetInt32());
        Assert.Equal(6, counts.GetProperty("admission").GetProperty("unsupported").GetInt32());
        AssertRatio(counts.GetProperty("exactAnswers"), 10, 24);
        AssertRatio(counts.GetProperty("usefulAnswers"), 12, 24);
        AssertRatio(counts.GetProperty("decisionChangingAnswers"), 12, 24);
        var outcomes = counts.GetProperty("outcomeComposition");
        Assert.Equal(10, outcomes.GetProperty("Exact").GetInt32());
        Assert.Equal(2, outcomes.GetProperty("Partial").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Blocked").GetInt32());
        Assert.Equal(3, outcomes.GetProperty("Unavailable").GetInt32());
        Assert.Equal(6, outcomes.GetProperty("Unsupported").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Conflicting").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Invalid").GetInt32());
        var representative = root.GetProperty("rawCounts").GetProperty("representativeRows");
        Assert.Equal(0, representative.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(0, representative.GetProperty("distinctIncidents").GetInt32());
        Assert.Equal(0, representative.GetProperty("distinctApplicationShapes").GetInt32());

        var gate = root.GetProperty("portfolioGate");
        Assert.Equal("SatisfiedSyntheticValidation", gate.GetProperty("status").GetString());
        Assert.Equal(24, gate.GetProperty("qualifyingIncidentCount").GetInt32());
        Assert.Equal(4, gate.GetProperty("qualifyingApplicationShapeCount").GetInt32());
        Assert.Empty(gate.GetProperty("missingConditions").EnumerateArray());
        var decision = root.GetProperty("nextDecision");
        Assert.Equal("SelectedSyntheticDesignDecision", decision.GetProperty("status").GetString());
        Assert.Equal(
            "AdmitOneConcreteContextAcquisitionScenario",
            decision.GetProperty("selection").GetString());
        var ranking = decision.GetProperty("boundaryRanking").EnumerateArray().ToArray();
        Assert.Equal("RootContextAttribution", ranking[0].GetProperty("boundary").GetString());
        Assert.Equal(6, ranking[0].GetProperty("independentIncidentCount").GetInt32());
        Assert.Equal(4, ranking[0].GetProperty("applicationShapeCount").GetInt32());
        Assert.Equal(6, ranking[0].GetProperty("decisionChangingQuestionCount").GetInt32());
        Assert.Equal(0, ranking[0].GetProperty("usefulQuestionCount").GetInt32());
        Assert.Equal(0, ranking[0].GetProperty("exactEvidenceQuestionCount").GetInt32());
        Assert.DoesNotContain('%', File.ReadAllText(path));
    }

    private static void AssertHumanReport(string path)
    {
        var report = File.ReadAllText(path);
        Assert.Contains("W6 usefulness portfolio report v3", report, StringComparison.Ordinal);
        Assert.Contains("questions=24; incidents=24; application-shapes=4; target-root-types=4", report, StringComparison.Ordinal);
        Assert.Contains("W6=17; context-rejected=1; unsupported=6", report, StringComparison.Ordinal);
        Assert.Contains("Representative rows: questions=0; incidents=0; application-shapes=0", report, StringComparison.Ordinal);
        Assert.Contains("SatisfiedSyntheticValidation", report, StringComparison.Ordinal);
        Assert.Contains("selection=AdmitOneConcreteContextAcquisitionScenario", report, StringComparison.Ordinal);
        Assert.DoesNotContain('%', report);
        Assert.Equal(
            24,
            report.Split(Environment.NewLine)
                .Count(static line => line.Contains(": admission=", StringComparison.Ordinal)));
    }

    private static void RunConsumer(
        string repositoryRoot,
        string manifestPath,
        string dumpPath,
        string machinePath,
        string humanPath)
    {
        var result = RunHeadless(
            repositoryRoot,
            [
                "--manifest", manifestPath,
                "--dump", dumpPath,
                "--machine-output", machinePath,
                "--human-output", humanPath,
            ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("W6_CONSUMER_OK:1", result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    private static void RunPortfolio(
        string repositoryRoot,
        string portfolioPath,
        string reportRoot,
        string machinePath,
        string humanPath)
    {
        var result = RunHeadless(
            repositoryRoot,
            [
                "--portfolio-manifest", portfolioPath,
                "--report-root", reportRoot,
                "--machine-output", machinePath,
                "--human-output", humanPath,
            ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "W6_USEFULNESS_OK:24:SatisfiedSyntheticValidation:SelectedSyntheticDesignDecision",
            result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    private static void AssertSubstantiveTieDefers(
        string repositoryRoot,
        string portfolioPath,
        string reportRoot)
    {
        var tiedPortfolio = Path.Combine(reportRoot, "w6-tied-portfolio.json");
        var text = File.ReadAllText(portfolioPath).Replace(
            "\"dominantBoundary\": \"RootContextAttribution\"",
            "\"dominantBoundary\": \"None\"",
            StringComparison.Ordinal);
        File.WriteAllText(tiedPortfolio, text);
        var machinePath = Path.Combine(reportRoot, "w6-tied.machine.json");
        var humanPath = Path.Combine(reportRoot, "w6-tied.human.txt");
        var result = RunHeadless(
            repositoryRoot,
            [
                "--portfolio-manifest", tiedPortfolio,
                "--report-root", reportRoot,
                "--machine-output", machinePath,
                "--human-output", humanPath,
            ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "W6_USEFULNESS_OK:24:SatisfiedSyntheticValidation:DeferredNoUniqueQualifiedBoundary",
            result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        using var document = JsonDocument.Parse(File.ReadAllBytes(machinePath));
        var decision = document.RootElement.GetProperty("nextDecision");
        Assert.Equal("DeferredNoUniqueQualifiedBoundary", decision.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, decision.GetProperty("selection").ValueKind);
    }

    private static void AssertDesignedRowsCannotBePromoted(
        string repositoryRoot,
        string portfolioPath,
        string reportRoot)
    {
        var promotedPortfolio = Path.Combine(reportRoot, "w6-promoted-portfolio.json");
        var text = File.ReadAllText(portfolioPath).Replace(
            "\"corpusKind\": \"SyntheticIncident\"",
            "\"corpusKind\": \"RepresentativeIncident\"",
            StringComparison.Ordinal);
        File.WriteAllText(promotedPortfolio, text);
        var result = RunHeadless(
            repositoryRoot,
            [
                "--portfolio-manifest", promotedPortfolio,
                "--report-root", reportRoot,
                "--machine-output", Path.Combine(reportRoot, "w6-promoted.machine.json"),
                "--human-output", Path.Combine(reportRoot, "w6-promoted.human.txt"),
            ]);
        Assert.Equal(3, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.StandardOutput);
        Assert.Contains("W6_USEFULNESS_INPUT_INVALID", result.StandardError, StringComparison.Ordinal);
    }

    private static ProcessResult RunHeadless(string repositoryRoot, IReadOnlyList<string> arguments)
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
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(180_000))
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

            throw new Xunit.Sdk.XunitException("The W6 headless process did not exit within its bound.");
        }

        return new ProcessResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult(),
            stderr.GetAwaiter().GetResult());
    }

    private static void AssertRatio(JsonElement ratio, int numerator, int denominator)
    {
        Assert.Equal(numerator, ratio.GetProperty("numerator").GetInt32());
        Assert.Equal(denominator, ratio.GetProperty("denominator").GetInt32());
    }

    private static string GetIncidentId(JsonElement incident) => incident.GetProperty("incidentId").GetString()!;

    private static string ResolveRepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string ResolveConsumerExecutable(string repositoryRoot)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Interpreter.Headless.ReferenceConsumer.exe"
            : "Interpreter.Headless.ReferenceConsumer";
        return Path.Combine(
            repositoryRoot,
            "src",
            "Interpreter.Headless.ReferenceConsumer",
            "bin",
            configuration,
            targetFramework,
            fileName);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
