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
            var firstMachine = Path.Combine(outputDirectory, "first.machine.json");
            var firstHuman = Path.Combine(outputDirectory, "first.human.txt");
            var secondMachine = Path.Combine(outputDirectory, "second.machine.json");
            var secondHuman = Path.Combine(outputDirectory, "second.human.txt");

            RunConsumer(repositoryRoot, manifestPath, dumpPath, firstMachine, firstHuman);
            RunConsumer(repositoryRoot, manifestPath, dumpPath, secondMachine, secondHuman);

            Assert.Equal(File.ReadAllBytes(firstMachine), File.ReadAllBytes(secondMachine));
            Assert.Equal(File.ReadAllBytes(firstHuman), File.ReadAllBytes(secondHuman));
            AssertMachineReport(firstMachine);
            AssertHumanReport(firstHuman);
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

    private static void RunConsumer(
        string repositoryRoot,
        string manifestPath,
        string dumpPath,
        string machineOutput,
        string humanOutput)
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
        process.StartInfo.ArgumentList.Add("--manifest");
        process.StartInfo.ArgumentList.Add(manifestPath);
        process.StartInfo.ArgumentList.Add("--dump");
        process.StartInfo.ArgumentList.Add(dumpPath);
        process.StartInfo.ArgumentList.Add("--machine-output");
        process.StartInfo.ArgumentList.Add(machineOutput);
        process.StartInfo.ArgumentList.Add("--human-output");
        process.StartInfo.ArgumentList.Add(humanOutput);
        process.StartInfo.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(60_000), "The headless reference consumer did not exit within its bound.");
        Assert.Equal(0, process.ExitCode);
        Assert.Equal("W5_CONSUMER_OK:9", stdout.GetAwaiter().GetResult().Trim());
        Assert.True(string.IsNullOrWhiteSpace(stderr.GetAwaiter().GetResult()));
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
}
