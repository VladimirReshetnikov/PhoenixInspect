using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Runs W5.4's checked-in manifest through the reference consumer in fresh headless processes.</summary>
public sealed class W5HeadlessReferenceConsumerIntegrationTests
{
    private const int ExpectedMarker = 0x13579BDF;
    private const int ExpectedSummary = 0x26AF37BD;

    /// <summary>
    /// Generates one dump, runs the complete nine-row corpus twice through separate processes and reopened sessions,
    /// requires byte-identical reports, and validates every required scenario and explanation axis.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W5ExpressionFacadeV1")]
    [Trait("Corpus", "W5UsefulnessGeneratedV1")]
    public void Checked_in_manifest_replays_through_fresh_headless_consumer_processes()
    {
        var executablePath = TestTargetPaths.ResolveExecutable();
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w5-consumer-{Guid.NewGuid():N}.dmp");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"w5-consumer-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executablePath))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var repositoryRoot = ResolveRepositoryRoot();
            var manifestPath = Path.Combine(
                repositoryRoot,
                "tests",
                "corpus",
                "w5-expression-facade-v1.json");
            Assert.True(File.Exists(manifestPath));
            var firstMachine = Path.Combine(outputDirectory, "w5-expression-facade.machine.json");
            var firstHuman = Path.Combine(outputDirectory, "first.human.txt");
            var secondMachine = Path.Combine(outputDirectory, "second.machine.json");
            var secondHuman = Path.Combine(outputDirectory, "second.human.txt");

            RunConsumer(repositoryRoot, manifestPath, dumpPath, firstMachine, firstHuman);
            RunConsumer(repositoryRoot, manifestPath, dumpPath, secondMachine, secondHuman);

            Assert.Equal(File.ReadAllBytes(firstMachine), File.ReadAllBytes(secondMachine));
            Assert.Equal(File.ReadAllBytes(firstHuman), File.ReadAllBytes(secondHuman));
            AssertMachineReport(firstMachine);
            AssertHumanReport(firstHuman);

            var portfolioManifest = Path.Combine(
                repositoryRoot,
                "tests",
                "corpus",
                "w5-usefulness-generated-validation-v1.json");
            var firstUsefulnessMachine = Path.Combine(outputDirectory, "first.usefulness.machine.json");
            var firstUsefulnessHuman = Path.Combine(outputDirectory, "first.usefulness.human.txt");
            var secondUsefulnessMachine = Path.Combine(outputDirectory, "second.usefulness.machine.json");
            var secondUsefulnessHuman = Path.Combine(outputDirectory, "second.usefulness.human.txt");
            RunUsefulnessConsumer(
                repositoryRoot,
                portfolioManifest,
                outputDirectory,
                firstUsefulnessMachine,
                firstUsefulnessHuman);
            RunUsefulnessConsumer(
                repositoryRoot,
                portfolioManifest,
                outputDirectory,
                secondUsefulnessMachine,
                secondUsefulnessHuman);

            Assert.Equal(File.ReadAllBytes(firstUsefulnessMachine), File.ReadAllBytes(secondUsefulnessMachine));
            Assert.Equal(File.ReadAllBytes(firstUsefulnessHuman), File.ReadAllBytes(secondUsefulnessHuman));
            AssertUsefulnessMachineReport(firstUsefulnessMachine);
            AssertUsefulnessHumanReport(firstUsefulnessHuman);
            AssertGeneratedReportCannotBePromoted(repositoryRoot, portfolioManifest, outputDirectory);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Materializes the predeclared twelve-incident synthetic portfolio as independent dump snapshots, evaluates
    /// one question per fresh consumer process, and requires the portfolio runner to select the recurring
    /// fixed-depth member-navigation boundary without promoting the results to representative evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W5MeaningfulSyntheticV2")]
    public void Meaningful_synthetic_incidents_select_the_recurring_design_boundary_headlessly()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var executablePath = TestTargetPaths.ResolveExecutable();
        var portfolioManifest = Path.Combine(
            repositoryRoot,
            "tests",
            "corpus",
            "w5-usefulness-meaningful-synthetic-v2.json");
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"w5-meaningful-synthetic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            using var portfolioDocument = JsonDocument.Parse(File.ReadAllBytes(portfolioManifest));
            var definitions = portfolioDocument.RootElement
                .GetProperty("evaluationReports")
                .EnumerateArray()
                .ToArray();
            Assert.Equal(12, definitions.Length);
            var snapshotIdentities = new HashSet<string>(StringComparer.Ordinal);
            var rootTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                var reportId = definition.GetProperty("id").GetString()!;
                var reportRelativePath = definition.GetProperty("path").GetString()!;
                var fixture = definition.GetProperty("syntheticFixture");
                var arguments = fixture.GetProperty("targetArguments")
                    .EnumerateArray()
                    .Select(static item => item.GetString()!)
                    .ToArray();
                var dumpPath = Path.Combine(outputDirectory, $"{reportId}.dmp");
                using (var target = TestTargetRunner.StartAndWaitReady(
                           executablePath,
                           arguments,
                           isolatedDirectory: null))
                {
                    DumpWriter.WriteFullDump(target.Pid, dumpPath);
                }

                var scenarioManifest = Path.Combine(outputDirectory, $"{reportId}.scenario.json");
                WriteSyntheticScenarioManifest(scenarioManifest, fixture);
                var machineReport = Path.Combine(outputDirectory, reportRelativePath);
                var humanReport = Path.Combine(outputDirectory, $"{reportId}.human.txt");
                RunConsumer(
                    repositoryRoot,
                    scenarioManifest,
                    dumpPath,
                    machineReport,
                    humanReport,
                    expectedScenarioCount: 1);
                AssertSyntheticIncidentReport(machineReport, fixture, snapshotIdentities, rootTypes);
            }

            Assert.Equal(12, snapshotIdentities.Count);
            Assert.Equal(
                ["SyntheticBatchPipelineProbe", "SyntheticRequestPipelineProbe"],
                rootTypes.OrderBy(static type => type, StringComparer.Ordinal));

            var firstMachine = Path.Combine(outputDirectory, "first.meaningful.machine.json");
            var firstHuman = Path.Combine(outputDirectory, "first.meaningful.human.txt");
            var secondMachine = Path.Combine(outputDirectory, "second.meaningful.machine.json");
            var secondHuman = Path.Combine(outputDirectory, "second.meaningful.human.txt");
            RunUsefulnessConsumer(
                repositoryRoot,
                portfolioManifest,
                outputDirectory,
                firstMachine,
                firstHuman,
                expectedRowCount: 12,
                expectedStatus: "SatisfiedSyntheticValidation");
            RunUsefulnessConsumer(
                repositoryRoot,
                portfolioManifest,
                outputDirectory,
                secondMachine,
                secondHuman,
                expectedRowCount: 12,
                expectedStatus: "SatisfiedSyntheticValidation");

            Assert.Equal(File.ReadAllBytes(firstMachine), File.ReadAllBytes(secondMachine));
            Assert.Equal(File.ReadAllBytes(firstHuman), File.ReadAllBytes(secondHuman));
            AssertMeaningfulSyntheticMachineReport(firstMachine);
            AssertMeaningfulSyntheticHumanReport(firstHuman);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static void RunConsumer(
        string repositoryRoot,
        string manifestPath,
        string dumpPath,
        string machineOutput,
        string humanOutput,
        int expectedScenarioCount = 9)
    {
        var result = RunHeadlessConsumer(
            repositoryRoot,
            [
                "--manifest",
                manifestPath,
                "--dump",
                dumpPath,
                "--machine-output",
                machineOutput,
                "--human-output",
                humanOutput,
            ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"W5_CONSUMER_OK:{expectedScenarioCount}", result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    private static void RunUsefulnessConsumer(
        string repositoryRoot,
        string portfolioManifest,
        string reportRoot,
        string machineOutput,
        string humanOutput,
        int expectedRowCount = 9,
        string expectedStatus = "OpenGeneratedValidationOnly")
    {
        Assert.True(File.Exists(portfolioManifest), portfolioManifest);
        var result = RunHeadlessConsumer(
            repositoryRoot,
            [
                "--portfolio-manifest",
                portfolioManifest,
                "--report-root",
                reportRoot,
                "--machine-output",
                machineOutput,
                "--human-output",
                humanOutput,
            ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $"W5_USEFULNESS_OK:{expectedRowCount}:{expectedStatus}",
            result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    private static ProcessResult RunHeadlessConsumer(
        string repositoryRoot,
        IReadOnlyList<string> arguments)
    {
        var consumer = ResolveConsumerExecutable(repositoryRoot);
        var wrapper = Path.Combine(repositoryRoot, "eng", "Invoke-HeadlessProcess.ps1");
        Assert.True(File.Exists(consumer), consumer);
        Assert.True(File.Exists(wrapper), wrapper);
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
        process.StartInfo.ArgumentList.Add(wrapper);
        process.StartInfo.ArgumentList.Add(consumer);
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

            throw new Xunit.Sdk.XunitException(
                $"The headless reference consumer did not exit within its bound. Arguments: " +
                string.Join(' ', arguments));
        }

        return new ProcessResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult(),
            stderr.GetAwaiter().GetResult());
    }

    private static void WriteSyntheticScenarioManifest(string path, JsonElement fixture)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("corpusKind", "SyntheticIncident");
        writer.WriteString("dumpPath", "__synthetic_incident_dump__");
        writer.WritePropertyName("root");
        fixture.GetProperty("root").WriteTo(writer);
        writer.WriteStartArray("scenarios");
        fixture.GetProperty("scenario").WriteTo(writer);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void AssertSyntheticIncidentReport(
        string path,
        JsonElement fixture,
        HashSet<string> snapshotIdentities,
        HashSet<string> rootTypes)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal("SyntheticIncident", root.GetProperty("corpusKind").GetString());
        Assert.Contains(
            "not external observations",
            root.GetProperty("fixtureCaveat").GetString(),
            StringComparison.Ordinal);
        var expectedRoot = fixture.GetProperty("root");
        Assert.Equal(expectedRoot.GetProperty("name").GetString(), root.GetProperty("rootName").GetString());
        Assert.Equal(expectedRoot.GetProperty("typeName").GetString(), root.GetProperty("rootTypeName").GetString());
        var snapshot = root.GetProperty("dumpSnapshotSha256").GetString()!;
        Assert.Equal(64, snapshot.Length);
        Assert.True(snapshotIdentities.Add(snapshot), "Every synthetic incident must use a distinct dump snapshot.");
        _ = rootTypes.Add(root.GetProperty("rootTypeName").GetString()!);

        var scenario = Assert.Single(root.GetProperty("scenarios").EnumerateArray());
        var expectedScenario = fixture.GetProperty("scenario");
        Assert.Equal(expectedScenario.GetProperty("id").GetString(), scenario.GetProperty("id").GetString());
        Assert.Equal(
            expectedScenario.GetProperty("expression").GetString(),
            scenario.GetProperty("expression").GetString());
        var outcome = scenario.GetProperty("outcome");
        var expectedProductOutcome = fixture.GetProperty("expectedProductOutcome").GetString();
        switch (expectedProductOutcome)
        {
            case "Exact":
                Assert.Equal("Completed", outcome.GetProperty("completion").GetString());
                Assert.Equal("Exact", outcome.GetProperty("evidence").GetString());
                break;
            case "Partial":
                Assert.Equal("Completed", outcome.GetProperty("completion").GetString());
                Assert.Equal("Partial", outcome.GetProperty("evidence").GetString());
                break;
            case "Unknown":
                Assert.True(
                    outcome.GetProperty("completion").GetString() is "Completed" or "BudgetExhausted");
                break;
            case "Unavailable":
                Assert.Equal("AcquisitionFailure", outcome.GetProperty("kind").GetString());
                break;
            case "Unsupported":
                Assert.Equal("ClassificationFailure", outcome.GetProperty("kind").GetString());
                break;
            default:
                throw new Xunit.Sdk.XunitException($"Unexpected synthetic outcome '{expectedProductOutcome}'.");
        }

        var expectedValue = fixture.GetProperty("expectedValue");
        var expectedPrefix = fixture.GetProperty("expectedValuePrefix");
        if (expectedValue.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expectedValue.GetString(), outcome.GetProperty("value").GetString());
        }
        else if (expectedPrefix.ValueKind == JsonValueKind.String)
        {
            Assert.StartsWith(
                expectedPrefix.GetString()!,
                outcome.GetProperty("value").GetString(),
                StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, outcome.GetProperty("value").ValueKind);
        }
    }

    private static void AssertMachineReport(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("machineSchemaVersion").GetInt32());
        Assert.Equal(1, root.GetProperty("manifestSchemaVersion").GetInt32());
        Assert.Equal(64, root.GetProperty("dumpSnapshotSha256").GetString()!.Length);
        Assert.Equal("root", root.GetProperty("rootName").GetString());
        Assert.Equal("DumpProbe", root.GetProperty("rootTypeName").GetString());
        Assert.Equal("GeneratedValidation", root.GetProperty("corpusKind").GetString());
        Assert.Contains("not representative", root.GetProperty("fixtureCaveat").GetString(), StringComparison.Ordinal);
        var scenarios = root.GetProperty("scenarios").EnumerateArray().ToArray();
        Assert.Equal(9, scenarios.Length);
        var byId = scenarios.ToDictionary(
            static scenario => scenario.GetProperty("id").GetString()!,
            StringComparer.Ordinal);

        AssertOutcome(
            byId["w2-exact-field"],
            "DerivedQuery",
            "DerivedQuery",
            "Completed",
            "Complete",
            "Exact",
            $"i32:{ExpectedMarker}");
        Assert.Equal(2, byId["w2-exact-field"].GetProperty("repetitions").GetInt32());
        AssertOutcome(
            byId["w4-exact-interpreted"],
            "CounterfactualExecution",
            "CounterfactualExecution",
            "Completed",
            "Complete",
            "Exact",
            $"i32:{ExpectedSummary}");
        AssertOutcome(
            byId["w4-exact-modeled"],
            "CounterfactualExecution",
            "CounterfactualExecution",
            "Completed",
            "Complete",
            "Exact",
            $"i32:{ExpectedSummary}");
        Assert.Equal(2, byId["w4-exact-modeled"].GetProperty("repetitions").GetInt32());

        var partial = byId["w4-partial-marker"].GetProperty("outcome");
        Assert.Equal("Completed", partial.GetProperty("completion").GetString());
        Assert.Equal("Partial", partial.GetProperty("completeness").GetString());
        Assert.Equal("Partial", partial.GetProperty("evidence").GetString());
        Assert.StartsWith("unknown:sha256:", partial.GetProperty("value").GetString(), StringComparison.Ordinal);
        var unavailable = byId["w4-unavailable-marker"].GetProperty("outcome");
        Assert.Equal("Completed", unavailable.GetProperty("completion").GetString());
        Assert.Equal("Partial", unavailable.GetProperty("completeness").GetString());
        Assert.Equal("Unavailable", unavailable.GetProperty("evidence").GetString());
        Assert.StartsWith("unknown:sha256:", unavailable.GetProperty("value").GetString(), StringComparison.Ordinal);

        var acquisition = byId["method-acquisition-failure"].GetProperty("outcome");
        Assert.Equal("AcquisitionFailure", acquisition.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, acquisition.GetProperty("semanticMode").ValueKind);
        Assert.Equal("Adapter:Unavailable", acquisition.GetProperty("evidence").GetString());
        Assert.Equal("W5_MODULE_MISSING", acquisition.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        var unsupported = byId["unsupported-expression"].GetProperty("outcome");
        Assert.Equal("ClassificationFailure", unsupported.GetProperty("kind").GetString());
        Assert.Equal("QUERY_SYNTAX_UNSUPPORTED", unsupported.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        var budget = byId["instruction-budget-exhausted"].GetProperty("outcome");
        Assert.Equal("BudgetExhausted", budget.GetProperty("completion").GetString());
        Assert.Equal("None", budget.GetProperty("completeness").GetString());
        var cancelled = byId["cancel-before-execution"].GetProperty("outcome");
        Assert.Equal("Cancelled", cancelled.GetProperty("completion").GetString());
        Assert.Equal("None", cancelled.GetProperty("completeness").GetString());

        foreach (var scenario in scenarios)
        {
            Assert.Equal(64, scenario.GetProperty("outcomeProjectionSha256").GetString()!.Length);
            Assert.True(scenario.GetProperty("outcome").GetProperty("reachedBounds").GetArrayLength() > 0);
        }
    }

    private static void AssertOutcome(
        JsonElement scenario,
        string kind,
        string semanticMode,
        string completion,
        string completeness,
        string evidence,
        string value)
    {
        var outcome = scenario.GetProperty("outcome");
        Assert.Equal(kind, outcome.GetProperty("kind").GetString());
        Assert.Equal(semanticMode, outcome.GetProperty("semanticMode").GetString());
        Assert.Equal(completion, outcome.GetProperty("completion").GetString());
        Assert.Equal(completeness, outcome.GetProperty("completeness").GetString());
        Assert.Equal(evidence, outcome.GetProperty("evidence").GetString());
        Assert.Equal("None", outcome.GetProperty("effects").GetString());
        Assert.Equal(value, outcome.GetProperty("value").GetString());
        Assert.Equal(64, outcome.GetProperty("underlyingArtifactSha256").GetString()!.Length);
        Assert.False(string.IsNullOrWhiteSpace(outcome.GetProperty("underlyingCanonicalBase64").GetString()));
    }

    private static void AssertHumanReport(string path)
    {
        var report = File.ReadAllText(path);
        Assert.Contains("W5 expression-facade report v1", report, StringComparison.Ordinal);
        Assert.Contains("Caveat:", report, StringComparison.Ordinal);
        Assert.Contains("semantic=", report, StringComparison.Ordinal);
        Assert.Contains("completion=", report, StringComparison.Ordinal);
        Assert.Contains("completeness=", report, StringComparison.Ordinal);
        Assert.Contains("evidence=", report, StringComparison.Ordinal);
        Assert.Contains("effects=", report, StringComparison.Ordinal);
        Assert.Contains("value=", report, StringComparison.Ordinal);
        Assert.Contains("bounds=", report, StringComparison.Ordinal);
        Assert.Contains("provenance=", report, StringComparison.Ordinal);
        Assert.Contains("diagnostics=", report, StringComparison.Ordinal);
        Assert.Equal(9, report.Split(Environment.NewLine).Count(static line => line.Contains(": outcome=", StringComparison.Ordinal)));
    }

    private static void AssertUsefulnessMachineReport(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("usefulnessReportSchemaVersion").GetInt32());
        Assert.Equal(2, root.GetProperty("portfolioSchemaVersion").GetInt32());
        Assert.Equal("w5-usefulness-generated-validation-v1", root.GetProperty("portfolioId").GetString());
        Assert.Equal("GeneratedValidation", root.GetProperty("corpusKind").GetString());
        Assert.True(root.GetProperty("predeclaredBeforeEvaluation").GetBoolean());
        Assert.False(root.GetProperty("claimsProductionReadiness").GetBoolean());
        Assert.Contains(
            "do not count toward meaningful synthetic validation",
            root.GetProperty("evidenceScopeCaveat").GetString(),
            StringComparison.Ordinal);
        var evaluationReport = Assert.Single(root.GetProperty("evaluationReports").EnumerateArray());
        Assert.Equal("GeneratedValidation", evaluationReport.GetProperty("corpusKind").GetString());
        Assert.Equal(9, evaluationReport.GetProperty("scenarioCount").GetInt32());

        var questions = root.GetProperty("questions").EnumerateArray().ToArray();
        Assert.Equal(9, questions.Length);
        var byId = questions.ToDictionary(
            static question => question.GetProperty("questionId").GetString()!,
            StringComparer.Ordinal);
        Assert.Equal("W2", byId["generated-w2-exact-field"].GetProperty("admission").GetString());
        Assert.Equal("Exact", byId["generated-w2-exact-field"].GetProperty("productOutcome").GetString());
        Assert.Equal("W4", byId["generated-w4-partial-marker"].GetProperty("admission").GetString());
        Assert.Equal("Partial", byId["generated-w4-partial-marker"].GetProperty("productOutcome").GetString());
        Assert.Equal("Unknown", byId["generated-w4-unavailable-marker"].GetProperty("productOutcome").GetString());
        Assert.Equal(
            "Unavailable",
            byId["generated-method-acquisition-failure"].GetProperty("productOutcome").GetString());
        Assert.Equal(
            "Unsupported",
            byId["generated-unsupported-expression"].GetProperty("admission").GetString());
        Assert.Equal(
            "Unsupported",
            byId["generated-unsupported-expression"].GetProperty("productOutcome").GetString());

        var allRows = root.GetProperty("rawCounts").GetProperty("allRows");
        Assert.Equal(9, allRows.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(1, allRows.GetProperty("distinctIncidents").GetInt32());
        Assert.Equal(1, allRows.GetProperty("distinctApplicationShapes").GetInt32());
        AssertRatio(allRows.GetProperty("admission").GetProperty("admitted"), 8, 9);
        Assert.Equal(1, allRows.GetProperty("admission").GetProperty("w2").GetInt32());
        Assert.Equal(7, allRows.GetProperty("admission").GetProperty("w4").GetInt32());
        Assert.Equal(1, allRows.GetProperty("admission").GetProperty("unsupported").GetInt32());
        AssertRatio(allRows.GetProperty("exactAnswers"), 3, 9);
        AssertRatio(allRows.GetProperty("usefulPartialOrUnknownAnswers"), 0, 4);
        AssertRatio(allRows.GetProperty("decisionChangingUsefulness"), 0, 9);
        var outcomes = allRows.GetProperty("outcomeComposition");
        Assert.Equal(3, outcomes.GetProperty("Exact").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Partial").GetInt32());
        Assert.Equal(3, outcomes.GetProperty("Unknown").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Unavailable").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Unsupported").GetInt32());
        Assert.Equal(
            1,
            allRows.GetProperty("acquisitionFailureComposition").GetProperty("W5_MODULE_MISSING").GetInt32());

        var qualifying = root.GetProperty("rawCounts").GetProperty("gateQualifyingRows");
        Assert.Equal(0, qualifying.GetProperty("totalQuestions").GetInt32());
        AssertRatio(qualifying.GetProperty("admission").GetProperty("admitted"), 0, 0);
        AssertRatio(qualifying.GetProperty("exactAnswers"), 0, 0);
        var representative = root.GetProperty("rawCounts").GetProperty("representativeRows");
        Assert.Equal(0, representative.GetProperty("totalQuestions").GetInt32());
        AssertRatio(representative.GetProperty("admission").GetProperty("admitted"), 0, 0);
        AssertRatio(representative.GetProperty("exactAnswers"), 0, 0);
        var gate = root.GetProperty("portfolioGate");
        Assert.Equal("OpenGeneratedValidationOnly", gate.GetProperty("status").GetString());
        Assert.Equal(10, gate.GetProperty("minimumQualifyingIncidents").GetInt32());
        Assert.Equal(2, gate.GetProperty("minimumQualifyingApplicationShapes").GetInt32());
        Assert.Equal(0, gate.GetProperty("qualifyingIncidentCount").GetInt32());
        Assert.Equal(0, gate.GetProperty("qualifyingApplicationShapeCount").GetInt32());
        Assert.Equal(0, gate.GetProperty("qualifyingQuestionCount").GetInt32());
        Assert.NotEmpty(gate.GetProperty("missingConditions").EnumerateArray());
        var decision = root.GetProperty("nextDecision");
        Assert.Equal("DeferredPortfolioGateOpen", decision.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, decision.GetProperty("selection").ValueKind);
        Assert.Empty(decision.GetProperty("blockerRanking").EnumerateArray());

        var text = File.ReadAllText(path);
        Assert.DoesNotContain('%', text);
        Assert.DoesNotContain("percentage", text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertUsefulnessHumanReport(string path)
    {
        var report = File.ReadAllText(path);
        Assert.Contains("W5 usefulness portfolio report v2", report, StringComparison.Ordinal);
        Assert.Contains("Raw counts (all):", report, StringComparison.Ordinal);
        Assert.Contains("admitted=8/9", report, StringComparison.Ordinal);
        Assert.Contains("exact=3/9", report, StringComparison.Ordinal);
        Assert.Contains("Raw counts (gate-qualifying): questions=0", report, StringComparison.Ordinal);
        Assert.Contains("Raw counts (representative): questions=0", report, StringComparison.Ordinal);
        Assert.Contains("OpenGeneratedValidationOnly", report, StringComparison.Ordinal);
        Assert.Contains("selection=none", report, StringComparison.Ordinal);
        Assert.DoesNotContain('%', report);
        Assert.Equal(
            9,
            report.Split(Environment.NewLine)
                .Count(static line => line.Contains(": admission=", StringComparison.Ordinal)));
    }

    private static void AssertMeaningfulSyntheticMachineReport(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("usefulnessReportSchemaVersion").GetInt32());
        Assert.Equal(2, root.GetProperty("portfolioSchemaVersion").GetInt32());
        Assert.Equal("w5-usefulness-meaningful-synthetic-v2", root.GetProperty("portfolioId").GetString());
        Assert.Equal("SyntheticIncident", root.GetProperty("corpusKind").GetString());
        Assert.True(root.GetProperty("predeclaredBeforeEvaluation").GetBoolean());
        Assert.False(root.GetProperty("claimsProductionReadiness").GetBoolean());
        Assert.Contains(
            "not external observations",
            root.GetProperty("evidenceScopeCaveat").GetString(),
            StringComparison.Ordinal);
        var evaluationReports = root.GetProperty("evaluationReports").EnumerateArray().ToArray();
        Assert.Equal(12, evaluationReports.Length);
        Assert.Equal(
            12,
            evaluationReports.Select(static report => report.GetProperty("dumpSnapshotSha256").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            evaluationReports,
            static report => Assert.Equal("SyntheticIncident", report.GetProperty("corpusKind").GetString()));

        var questions = root.GetProperty("questions").EnumerateArray().ToArray();
        Assert.Equal(12, questions.Length);
        Assert.Equal(2, questions.Count(static question => question.GetProperty("admission").GetString() == "W2"));
        Assert.Equal(6, questions.Count(static question => question.GetProperty("admission").GetString() == "W4"));
        Assert.Equal(
            4,
            questions.Count(static question => question.GetProperty("admission").GetString() == "Unsupported"));

        var rawCounts = root.GetProperty("rawCounts");
        AssertMeaningfulSyntheticAggregate(rawCounts.GetProperty("allRows"));
        AssertMeaningfulSyntheticAggregate(rawCounts.GetProperty("gateQualifyingRows"));
        var representative = rawCounts.GetProperty("representativeRows");
        Assert.Equal(0, representative.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(0, representative.GetProperty("distinctIncidents").GetInt32());
        Assert.Equal(0, representative.GetProperty("distinctApplicationShapes").GetInt32());

        var gate = root.GetProperty("portfolioGate");
        Assert.Equal("SatisfiedSyntheticValidation", gate.GetProperty("status").GetString());
        Assert.Equal(10, gate.GetProperty("minimumQualifyingIncidents").GetInt32());
        Assert.Equal(2, gate.GetProperty("minimumQualifyingApplicationShapes").GetInt32());
        Assert.Equal(12, gate.GetProperty("qualifyingIncidentCount").GetInt32());
        Assert.Equal(2, gate.GetProperty("qualifyingApplicationShapeCount").GetInt32());
        Assert.Equal(12, gate.GetProperty("qualifyingQuestionCount").GetInt32());
        Assert.Empty(gate.GetProperty("missingConditions").EnumerateArray());

        var decision = root.GetProperty("nextDecision");
        Assert.Equal("SelectedSyntheticDesignDecision", decision.GetProperty("status").GetString());
        Assert.Equal("AdmitFixedDepthMemberChain", decision.GetProperty("selection").GetString());
        var ranking = decision.GetProperty("blockerRanking").EnumerateArray().ToArray();
        Assert.Equal("MemberNavigation", ranking[0].GetProperty("blocker").GetString());
        Assert.Equal(4, ranking[0].GetProperty("independentIncidentCount").GetInt32());
        Assert.Equal("ContextAcquisition", ranking[1].GetProperty("blocker").GetString());
        Assert.Equal(3, ranking[1].GetProperty("independentIncidentCount").GetInt32());
        Assert.Equal("ExecutionBody", ranking[2].GetProperty("blocker").GetString());
        Assert.Equal(1, ranking[2].GetProperty("independentIncidentCount").GetInt32());

        var text = File.ReadAllText(path);
        Assert.DoesNotContain('%', text);
        Assert.DoesNotContain("percentage", text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertMeaningfulSyntheticAggregate(JsonElement aggregate)
    {
        Assert.Equal(12, aggregate.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(12, aggregate.GetProperty("distinctIncidents").GetInt32());
        Assert.Equal(2, aggregate.GetProperty("distinctApplicationShapes").GetInt32());
        var admission = aggregate.GetProperty("admission");
        AssertRatio(admission.GetProperty("admitted"), 8, 12);
        Assert.Equal(2, admission.GetProperty("w2").GetInt32());
        Assert.Equal(6, admission.GetProperty("w4").GetInt32());
        Assert.Equal(4, admission.GetProperty("unsupported").GetInt32());
        AssertRatio(aggregate.GetProperty("exactAnswers"), 4, 12);
        AssertRatio(aggregate.GetProperty("usefulPartialOrUnknownAnswers"), 2, 3);
        AssertRatio(aggregate.GetProperty("decisionChangingUsefulness"), 6, 12);
        var outcomes = aggregate.GetProperty("outcomeComposition");
        Assert.Equal(4, outcomes.GetProperty("Exact").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Partial").GetInt32());
        Assert.Equal(2, outcomes.GetProperty("Unknown").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("Unavailable").GetInt32());
        Assert.Equal(4, outcomes.GetProperty("Unsupported").GetInt32());
        Assert.Equal(
            1,
            aggregate.GetProperty("acquisitionFailureComposition").GetProperty("W5_MODULE_MISSING").GetInt32());
        var blockers = aggregate.GetProperty("blockerComposition");
        Assert.Equal(4, blockers.GetProperty("MemberNavigation").GetInt32());
        Assert.Equal(3, blockers.GetProperty("ContextAcquisition").GetInt32());
        Assert.Equal(1, blockers.GetProperty("ExecutionBody").GetInt32());
        Assert.Equal(4, blockers.GetProperty("None").GetInt32());
    }

    private static void AssertMeaningfulSyntheticHumanReport(string path)
    {
        var report = File.ReadAllText(path);
        Assert.Contains("W5 usefulness portfolio report v2", report, StringComparison.Ordinal);
        Assert.Contains("not external observations", report, StringComparison.Ordinal);
        Assert.Contains("Raw counts (all): questions=12; incidents=12; application-shapes=2", report, StringComparison.Ordinal);
        Assert.Contains("Raw counts (gate-qualifying): questions=12", report, StringComparison.Ordinal);
        Assert.Contains("Raw counts (representative): questions=0", report, StringComparison.Ordinal);
        Assert.Contains("SatisfiedSyntheticValidation", report, StringComparison.Ordinal);
        Assert.Contains("selection=AdmitFixedDepthMemberChain", report, StringComparison.Ordinal);
        Assert.DoesNotContain('%', report);
        Assert.Equal(
            12,
            report.Split(Environment.NewLine)
                .Count(static line => line.Contains(": admission=", StringComparison.Ordinal)));
    }

    private static void AssertGeneratedReportCannotBePromoted(
        string repositoryRoot,
        string portfolioManifest,
        string reportRoot)
    {
        var promotedManifest = Path.Combine(reportRoot, "attempted-promoted-portfolio.json");
        var text = File.ReadAllText(portfolioManifest).Replace(
            "\"corpusKind\": \"GeneratedValidation\"",
            "\"corpusKind\": \"RepresentativeIncident\"",
            StringComparison.Ordinal);
        File.WriteAllText(promotedManifest, text);
        var result = RunHeadlessConsumer(
            repositoryRoot,
            [
                "--portfolio-manifest",
                promotedManifest,
                "--report-root",
                reportRoot,
                "--machine-output",
                Path.Combine(reportRoot, "attempted-promoted.machine.json"),
                "--human-output",
                Path.Combine(reportRoot, "attempted-promoted.human.txt"),
            ]);
        Assert.Equal(3, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.StandardOutput);
        Assert.Contains("cannot be promoted or mixed", result.StandardError, StringComparison.Ordinal);
    }

    private static void AssertRatio(JsonElement ratio, int numerator, int denominator)
    {
        Assert.Equal(numerator, ratio.GetProperty("numerator").GetInt32());
        Assert.Equal(denominator, ratio.GetProperty("denominator").GetInt32());
    }

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
