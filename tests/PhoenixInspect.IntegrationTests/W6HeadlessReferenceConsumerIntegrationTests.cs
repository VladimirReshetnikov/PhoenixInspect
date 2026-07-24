using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the append-only W6 manifest and report schema through independently launched, no-window target and
/// consumer processes over fresh generated dumps.
/// </summary>
public sealed class W6HeadlessReferenceConsumerIntegrationTests
{
    /// <summary>
    /// Replays the four exact W5-selected questions, all typed root-selection outcomes, a typed preparation miss, and
    /// unsupported complete syntax through independent snapshots and reopened consumer sessions.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainV1")]
    [Trait("Corpus", "W6MemberChainGeneratedV2")]
    public void Generated_member_chain_rows_replay_through_fresh_headless_processes()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var corpusPath = Path.Combine(
            repositoryRoot,
            "tests",
            "corpus",
            "w6-member-chain-generated-conformance-v2.json");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"w6-consumer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            using var corpus = JsonDocument.Parse(File.ReadAllBytes(corpusPath));
            var corpusRoot = corpus.RootElement;
            Assert.Equal(2, corpusRoot.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("GeneratedValidation", corpusRoot.GetProperty("corpusKind").GetString());
            var scenarios = corpusRoot.GetProperty("scenarios").EnumerateArray().ToArray();
            Assert.Equal(11, scenarios.Length);
            Assert.Equal(11, scenarios.Select(GetId).Distinct(StringComparer.Ordinal).Count());

            var snapshots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scenario in scenarios)
            {
                RunIndependentScenario(repositoryRoot, outputDirectory, scenario, snapshots);
            }

            Assert.Equal(11, snapshots.Count);
            Assert.Equal(
                ["batch-failed", "request-failed", "running", "running"],
                scenarios
                    .Where(static scenario => scenario.TryGetProperty("expectedValue", out _))
                    .Select(static scenario => scenario.GetProperty("expectedValue").GetString()!)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToArray());
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static void RunIndependentScenario(
        string repositoryRoot,
        string outputDirectory,
        JsonElement scenario,
        HashSet<string> snapshots)
    {
        var id = GetId(scenario);
        var executablePath = TestTargetPaths.ResolveExecutable();
        var targetArguments = scenario.GetProperty("targetArguments")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();
        var dumpPath = Path.Combine(outputDirectory, $"{id}.dmp");
        using (var target = TestTargetRunner.StartAndWaitReady(executablePath, targetArguments, isolatedDirectory: null))
        {
            DumpWriter.WriteFullDump(target.Pid, dumpPath);
        }

        var manifestPath = Path.Combine(outputDirectory, $"{id}.manifest.json");
        WriteScenarioManifest(manifestPath, scenario);
        var firstMachine = Path.Combine(outputDirectory, $"{id}.first.machine.json");
        var firstHuman = Path.Combine(outputDirectory, $"{id}.first.human.txt");
        var secondMachine = Path.Combine(outputDirectory, $"{id}.second.machine.json");
        var secondHuman = Path.Combine(outputDirectory, $"{id}.second.human.txt");
        RunConsumer(repositoryRoot, manifestPath, dumpPath, firstMachine, firstHuman);
        RunConsumer(repositoryRoot, manifestPath, dumpPath, secondMachine, secondHuman);

        Assert.Equal(File.ReadAllBytes(firstMachine), File.ReadAllBytes(secondMachine));
        Assert.Equal(File.ReadAllBytes(firstHuman), File.ReadAllBytes(secondHuman));
        AssertMachineReport(firstMachine, scenario, snapshots);
        AssertHumanReport(firstHuman, scenario);
    }

    private static void WriteScenarioManifest(string path, JsonElement scenario)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 2);
        writer.WriteString("corpusKind", "GeneratedValidation");
        writer.WriteString("dumpPath", "__w6_generated_dump_override__");
        writer.WriteStartObject("root");
        writer.WriteString("name", "root");
        writer.WriteString("typeName", scenario.GetProperty("rootType").GetString());
        writer.WriteNumber("maximumMatches", 2);
        writer.WriteNumber("maximumHandlesScanned", 100_000);
        writer.WriteString(
            "fixtureEvidenceView",
            scenario.GetProperty("rootFixtureEvidenceView").GetString());
        writer.WriteEndObject();
        writer.WriteStartArray("scenarios");
        writer.WriteStartObject();
        writer.WriteString("id", GetId(scenario));
        writer.WriteString("expression", scenario.GetProperty("expression").GetString());
        writer.WriteString("methodMode", "Interpreted");
        writer.WriteString("languageProfile", "FixedDepthMemberChainV1");
        writer.WriteNumber("instructionLimit", 100);
        writer.WriteNumber("logicalDepthLimit", 2);
        writer.WriteNumber("traversalLimit", 10);
        writer.WriteString("fixtureEvidenceView", "Captured");
        writer.WriteBoolean("cancelBeforeExecution", false);
        writer.WriteNumber("repeatCount", 2);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void AssertMachineReport(
        string path,
        JsonElement expected,
        HashSet<string> snapshots)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("machineSchemaVersion").GetInt32());
        Assert.Equal(2, root.GetProperty("manifestSchemaVersion").GetInt32());
        Assert.Equal("GeneratedValidation", root.GetProperty("corpusKind").GetString());
        Assert.Contains("not representative", root.GetProperty("fixtureCaveat").GetString(), StringComparison.Ordinal);
        Assert.Equal("root", root.GetProperty("rootName").GetString());
        Assert.Equal(expected.GetProperty("rootType").GetString(), root.GetProperty("rootTypeName").GetString());
        var snapshot = root.GetProperty("dumpSnapshotSha256").GetString()!;
        Assert.Equal(64, snapshot.Length);
        Assert.True(snapshots.Add(snapshot), "Every generated conformance row requires a fresh snapshot.");

        var selection = root.GetProperty("rootSelection");
        Assert.Equal("root", selection.GetProperty("name").GetString());
        Assert.Equal(expected.GetProperty("rootType").GetString(), selection.GetProperty("typeNameSelector").GetString());
        Assert.Equal(
            expected.GetProperty("expectedRootBindingStatus").GetString(),
            selection.GetProperty("bindingStatus").GetString());
        Assert.Equal(expected.GetProperty("expectedRootIssue").GetString(), selection.GetProperty("issue").GetString());
        Assert.Equal(
            expected.GetProperty("expectedAdapterStatus").GetString(),
            selection.GetProperty("adapterSearchStatus").GetString());
        Assert.Equal(100_000, selection.GetProperty("maximumHandlesScanned").GetInt32());
        Assert.Equal(2, selection.GetProperty("maximumMatches").GetInt32());
        Assert.False(selection.GetProperty("matchLimitReached").GetBoolean());
        Assert.Equal(
            expected.GetProperty("rootFixtureEvidenceView").GetString(),
            selection.GetProperty("fixtureEvidenceView").GetString());

        var row = Assert.Single(root.GetProperty("scenarios").EnumerateArray());
        Assert.Equal(GetId(expected), row.GetProperty("id").GetString());
        Assert.Equal(expected.GetProperty("expression").GetString(), row.GetProperty("expression").GetString());
        Assert.Equal("FixedDepthMemberChainV1", row.GetProperty("languageProfile").GetString());
        Assert.Equal("Captured", row.GetProperty("fixtureEvidenceView").GetString());
        Assert.Equal(2, row.GetProperty("repetitions").GetInt32());
        Assert.Equal(64, row.GetProperty("outcomeProjectionSha256").GetString()!.Length);
        var outcome = row.GetProperty("outcome");
        if (expected.TryGetProperty("expectedValue", out var expectedValue))
        {
            AssertExactOutcome(row, outcome, expectedValue.GetString()!);
        }
        else if (expected.GetProperty("expectedRootBindingStatus").GetString() != "ExactObject")
        {
            AssertRootClassificationFailure(row, outcome, expected.GetProperty("expectedDiagnostic").GetString()!);
        }
        else
        {
            AssertExactRootFailure(row, outcome, expected);
        }
    }

    private static void AssertExactOutcome(JsonElement row, JsonElement outcome, string expectedValue)
    {
        Assert.Equal(64, row.GetProperty("requestSha256").GetString()!.Length);
        Assert.Equal("DerivedQuery", outcome.GetProperty("kind").GetString());
        Assert.Equal("DerivedQuery", outcome.GetProperty("semanticMode").GetString());
        Assert.Equal("Completed", outcome.GetProperty("completion").GetString());
        Assert.Equal("Complete", outcome.GetProperty("completeness").GetString());
        Assert.Equal("Exact", outcome.GetProperty("evidence").GetString());
        Assert.Equal("None", outcome.GetProperty("effects").GetString());
        Assert.Equal(ToUtf16Projection(expectedValue), outcome.GetProperty("value").GetString());
        Assert.Empty(outcome.GetProperty("diagnostics").EnumerateArray());
        Assert.NotEmpty(outcome.GetProperty("reachedBounds").EnumerateArray());
        var provenance = outcome.GetProperty("provenance").EnumerateArray().ToArray();
        Assert.Contains(
            provenance,
            static item => item.GetProperty("sourceId").GetString()!.StartsWith(
                "dump-member-chain-certificate:sha256:",
                StringComparison.Ordinal));
        Assert.Contains(
            provenance,
            static item => item.GetProperty("sourceId").GetString()!.StartsWith(
                "dump-member-chain-reference:sha256:",
                StringComparison.Ordinal));
        Assert.Contains(
            provenance,
            static item => item.GetProperty("sourceId").GetString()!.StartsWith(
                "dump-member-chain-target:sha256:",
                StringComparison.Ordinal));
        Assert.Contains(
            provenance,
            static item => item.GetProperty("sourceId").GetString()!.StartsWith(
                "dump-member-chain-terminal-storage:sha256:",
                StringComparison.Ordinal));
    }

    private static void AssertRootClassificationFailure(JsonElement row, JsonElement outcome, string diagnostic)
    {
        Assert.Equal(JsonValueKind.Null, row.GetProperty("requestSha256").ValueKind);
        Assert.Equal("ClassificationFailure", outcome.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, outcome.GetProperty("semanticMode").ValueKind);
        Assert.Equal(JsonValueKind.Null, outcome.GetProperty("value").ValueKind);
        Assert.Empty(outcome.GetProperty("provenance").EnumerateArray());
        var diagnosticRow = Assert.Single(outcome.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(diagnostic, diagnosticRow.GetProperty("code").GetString());
    }

    private static void AssertExactRootFailure(JsonElement row, JsonElement outcome, JsonElement expected)
    {
        var expectedKind = expected.GetProperty("expectedOutcomeKind").GetString();
        Assert.Equal(expectedKind, outcome.GetProperty("kind").GetString());
        if (expectedKind == "ClassificationFailure")
        {
            Assert.Equal(64, row.GetProperty("requestSha256").GetString()!.Length);
            Assert.Equal(JsonValueKind.Null, outcome.GetProperty("semanticMode").ValueKind);
        }
        else
        {
            Assert.Equal(64, row.GetProperty("requestSha256").GetString()!.Length);
            Assert.Equal("DerivedQuery", outcome.GetProperty("semanticMode").GetString());
            Assert.Equal(expected.GetProperty("expectedCompletion").GetString(), outcome.GetProperty("completion").GetString());
            Assert.Equal(
                expected.GetProperty("expectedCompleteness").GetString(),
                outcome.GetProperty("completeness").GetString());
            Assert.Equal(expected.GetProperty("expectedEvidence").GetString(), outcome.GetProperty("evidence").GetString());
        }

        Assert.Equal(JsonValueKind.Null, outcome.GetProperty("value").ValueKind);
        var diagnostic = Assert.Single(outcome.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(expected.GetProperty("expectedDiagnostic").GetString(), diagnostic.GetProperty("code").GetString());
    }

    private static void AssertHumanReport(string path, JsonElement expected)
    {
        var report = File.ReadAllText(path);
        Assert.Contains("W6 expression-facade report v2", report, StringComparison.Ordinal);
        Assert.Contains(
            $"binding={expected.GetProperty("expectedRootBindingStatus").GetString()}",
            report,
            StringComparison.Ordinal);
        Assert.Contains(
            $"fixture-view={expected.GetProperty("rootFixtureEvidenceView").GetString()}",
            report,
            StringComparison.Ordinal);
        Assert.Contains("profile=FixedDepthMemberChainV1", report, StringComparison.Ordinal);
        if (expected.TryGetProperty("expectedValue", out var value))
        {
            var expectedValue = value.GetString()!;
            Assert.Contains($"value=String(length={expectedValue.Length})", report, StringComparison.Ordinal);
            Assert.DoesNotContain(expectedValue, report, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(
                expected.GetProperty("expectedDiagnostic").GetString()!,
                report,
                StringComparison.Ordinal);
        }
    }

    private static void RunConsumer(
        string repositoryRoot,
        string manifestPath,
        string dumpPath,
        string machineOutput,
        string humanOutput)
    {
        var consumer = ResolveConsumerExecutable(repositoryRoot);
        var wrapper = Path.Combine(repositoryRoot, "eng", "Invoke-HeadlessProcess.ps1");
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
        foreach (var argument in new[]
        {
            "--manifest",
            manifestPath,
            "--dump",
            dumpPath,
            "--machine-output",
            machineOutput,
            "--human-output",
            humanOutput,
        })
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

            throw new Xunit.Sdk.XunitException($"The W6 reference consumer did not exit for '{manifestPath}'.");
        }

        Assert.Equal(0, process.ExitCode);
        Assert.Equal("W6_CONSUMER_OK:1", stdout.GetAwaiter().GetResult().Trim());
        Assert.True(string.IsNullOrWhiteSpace(stderr.GetAwaiter().GetResult()));
    }

    private static string ToUtf16Projection(string value)
    {
        var builder = new StringBuilder(4 + (value.Length * 4));
        builder.Append("s16:");
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string GetId(JsonElement scenario) => scenario.GetProperty("id").GetString()!;

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
}
