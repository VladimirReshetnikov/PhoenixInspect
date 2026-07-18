using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Materializes and replays W7's predeclared sixteen-incident static-expression portfolio through fresh hidden
/// consumer processes.
/// </summary>
public sealed class W7MeaningfulSyntheticPortfolioIntegrationTests
{
    /// <summary>
    /// Requires one independent full dump per incident, byte-identical reports from two fresh consumers, exact
    /// fully-qualified poison controls, truthful synthetic evidence views, zero representative rows, and a unique
    /// threshold-qualified next design action that cannot be selected by enum order under a substantive tie.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    [Trait("Corpus", "W7MeaningfulSyntheticV1")]
    public void Predeclared_sixteen_incidents_select_one_qualified_post_W7_action()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var manifestPath = Path.Combine(
            repositoryRoot,
            "tests",
            "corpus",
            "w7-static-field-incidents-v1.json");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"w7-meaningful-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            var incidents = manifest.RootElement.GetProperty("incidents").EnumerateArray().ToArray();
            Assert.Equal(16, incidents.Length);
            MaterializeDumps(outputDirectory, incidents);

            var firstMachine = Path.Combine(outputDirectory, "first.w7.machine.json");
            var firstHuman = Path.Combine(outputDirectory, "first.w7.human.txt");
            var secondMachine = Path.Combine(outputDirectory, "second.w7.machine.json");
            var secondHuman = Path.Combine(outputDirectory, "second.w7.human.txt");
            RunPortfolio(repositoryRoot, manifestPath, outputDirectory, firstMachine, firstHuman);
            RunPortfolio(repositoryRoot, manifestPath, outputDirectory, secondMachine, secondHuman);

            Assert.Equal(File.ReadAllBytes(firstMachine), File.ReadAllBytes(secondMachine));
            Assert.Equal(File.ReadAllBytes(firstHuman), File.ReadAllBytes(secondHuman));
            AssertMachineReport(firstMachine);
            AssertHumanReport(firstHuman);
            AssertSubstantiveTieDefers(repositoryRoot, manifestPath, outputDirectory);
            AssertDesignedRowsCannotBePromoted(repositoryRoot, manifestPath, outputDirectory);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static void MaterializeDumps(string outputDirectory, JsonElement[] incidents)
    {
        var executable = W7TestTargetPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the W7 target at '{executable}'.");
        foreach (var incident in incidents)
        {
            var id = RequiredString(incident, "id");
            var arguments = incident.GetProperty("targetArguments")
                .EnumerateArray()
                .Select(static item => item.GetString()!)
                .ToArray();
            Assert.Equal(id, arguments[1]);
            using var target = TestTargetRunner.StartAndWaitReady(
                executable,
                arguments,
                isolatedDirectory: null);
            DumpWriter.WriteFullDump(target.Pid, Path.Combine(outputDirectory, $"{id}.dmp"));
        }
    }

    private static void AssertMachineReport(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("reportSchemaVersion").GetInt32());
        Assert.Equal("interpreter-w7-static-field-incidents-v1", root.GetProperty("portfolioId").GetString());
        Assert.Equal("designed-synthetic", root.GetProperty("evidenceKind").GetString());
        Assert.Equal("StaticFieldExpressionV1", root.GetProperty("languageProfile").GetString());
        Assert.True(root.GetProperty("predeclaredBeforeEvaluation").GetBoolean());
        Assert.False(root.GetProperty("claimsProductionReadiness").GetBoolean());
        Assert.Contains(
            "not external observations",
            root.GetProperty("evidenceScopeCaveat").GetString(),
            StringComparison.Ordinal);

        var rows = root.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(16, rows.Length);
        Assert.Equal(16, rows.Select(static row => row.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            16,
            rows.Select(static row => row.GetProperty("snapshotSha256").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            4,
            rows.Select(static row => row.GetProperty("shape").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, static row => Assert.True(row.GetProperty("matchesPredeclaredOutcome").GetBoolean()));

        AssertTerminal(rows, "request-qualified-scalar", "i32:2048376898");
        AssertTerminal(rows, "batch-imported-direct-field", "string:processing");
        AssertTerminal(rows, "coordinator-type-alias-owner", "string:coordinator-west");
        AssertTerminal(rows, "workflow-current-namespace-chain", "string:running");
        AssertTerminal(rows, "request-exact-null-reference", "null");
        AssertTerminal(rows, "batch-nullable-no-value", "nullable-int32:none");

        foreach (var id in new[]
                 {
                     "coordinator-frame-unavailable",
                     "workflow-pdb-partial",
                     "request-pdb-identity-conflict",
                     "batch-import-ambiguity",
                 })
        {
            var row = FindRow(rows, id);
            var control = row.GetProperty("control");
            Assert.Equal(0, control.GetProperty("poisonResolverCallCount").GetInt32());
            Assert.Equal("NotRequired", control.GetProperty("actual").GetProperty("context").GetString());
            Assert.StartsWith(
                "Exact",
                control.GetProperty("actual").GetProperty("value").GetString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(
            "Partial",
            FindRow(rows, "batch-partial-slot-bytes").GetProperty("actual").GetProperty("value").GetString());
        Assert.StartsWith(
            "Exact",
            FindRow(rows, "batch-partial-slot-bytes").GetProperty("captured").GetProperty("value").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "Conflict",
            FindRow(rows, "coordinator-target-header-conflict").GetProperty("actual").GetProperty("value").GetString());
        var absenceComparison = FindRow(rows, "workflow-field-absence-vs-invalid-signature")
            .GetProperty("comparison");
        Assert.Equal("ExactObject", absenceComparison.GetProperty("captured").GetProperty("value").GetString());
        Assert.Equal("Invalid", absenceComparison.GetProperty("actual").GetProperty("symbol").GetString());

        var syntaxStop = FindRow(rows, "workflow-valid-unadmitted-generic-call");
        Assert.Equal(0, syntaxStop.GetProperty("contextResolverCallCount").GetInt32());
        Assert.Equal("Unsupported", syntaxStop.GetProperty("actual").GetProperty("syntax").GetString());
        Assert.Equal("NotReached", syntaxStop.GetProperty("actual").GetProperty("context").GetString());

        var counts = root.GetProperty("rawCounts");
        Assert.Equal(16, counts.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(16, counts.GetProperty("distinctIncidents").GetInt32());
        Assert.Equal(4, counts.GetProperty("distinctApplicationShapes").GetInt32());
        Assert.Equal(16, counts.GetProperty("distinctSnapshots").GetInt32());
        Assert.Equal(6, counts.GetProperty("exactAnswers").GetInt32());
        Assert.Equal(16, counts.GetProperty("usefulAnswers").GetInt32());
        Assert.Equal(16, counts.GetProperty("decisionChangingAnswers").GetInt32());
        var representative = counts.GetProperty("representativeRows");
        Assert.Equal(0, representative.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(0, representative.GetProperty("distinctIncidents").GetInt32());
        Assert.Equal(0, representative.GetProperty("distinctApplicationShapes").GetInt32());

        var ranking = root.GetProperty("boundaryRanking").EnumerateArray().ToArray();
        Assert.Equal("BindingContextPrecision", ranking[0].GetProperty("boundary").GetString());
        Assert.Equal(4, ranking[0].GetProperty("independentIncidentCount").GetInt32());
        Assert.Equal(4, ranking[0].GetProperty("applicationShapeCount").GetInt32());
        Assert.Equal(4, ranking[0].GetProperty("decisionChangingQuestionCount").GetInt32());
        var decision = root.GetProperty("nextDecision");
        Assert.Equal("SelectedSyntheticDesignDecision", decision.GetProperty("status").GetString());
        Assert.Equal(
            "AddOneEvidenceBackedFramePdbImportAliasGenericRule",
            decision.GetProperty("selection").GetString());
        Assert.DoesNotContain('%', File.ReadAllText(path));
    }

    private static void AssertHumanReport(string path)
    {
        var report = File.ReadAllText(path);
        Assert.Contains("W7 static-field meaningful-synthetic portfolio report v1", report, StringComparison.Ordinal);
        Assert.Contains("questions=16; incidents=16; application-shapes=4; snapshots=16", report, StringComparison.Ordinal);
        Assert.Contains("exact=6; useful=16; decision-changing=16", report, StringComparison.Ordinal);
        Assert.Contains("representative-questions=0; representative-incidents=0; representative-shapes=0", report, StringComparison.Ordinal);
        Assert.Contains("decision-status=SelectedSyntheticDesignDecision", report, StringComparison.Ordinal);
        Assert.DoesNotContain("string:processing", report, StringComparison.Ordinal);
        Assert.DoesNotContain("i32:2048376898", report, StringComparison.Ordinal);
        Assert.DoesNotContain('%', report);
        Assert.Equal(
            16,
            report.Split(Environment.NewLine)
                .Count(static line => line.Contains(": syntax=", StringComparison.Ordinal)));
    }

    private static void AssertSubstantiveTieDefers(
        string repositoryRoot,
        string manifestPath,
        string outputDirectory)
    {
        var tiedManifest = Path.Combine(outputDirectory, "w7-tied-portfolio.json");
        File.WriteAllText(
            tiedManifest,
            File.ReadAllText(manifestPath).Replace(
                "\"postW7Boundary\": \"NestedReferenceSource\"",
                "\"postW7Boundary\": \"TargetIdentity\"",
                StringComparison.Ordinal));
        var machinePath = Path.Combine(outputDirectory, "w7-tied.machine.json");
        var result = RunHeadless(
            repositoryRoot,
            [
                "--w7-portfolio-manifest", tiedManifest,
                "--repository-root", repositoryRoot,
                "--dump-root", outputDirectory,
                "--machine-output", machinePath,
                "--human-output", Path.Combine(outputDirectory, "w7-tied.human.txt"),
            ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("W7_PORTFOLIO_OK:16:DeferredNoUniqueQualifiedBoundary", result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        using var document = JsonDocument.Parse(File.ReadAllBytes(machinePath));
        var decision = document.RootElement.GetProperty("nextDecision");
        Assert.Equal("DeferredNoUniqueQualifiedBoundary", decision.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, decision.GetProperty("selection").ValueKind);
    }

    private static void AssertDesignedRowsCannotBePromoted(
        string repositoryRoot,
        string manifestPath,
        string outputDirectory)
    {
        var promotedManifest = Path.Combine(outputDirectory, "w7-promoted-portfolio.json");
        File.WriteAllText(
            promotedManifest,
            File.ReadAllText(manifestPath).Replace(
                "\"evidenceKind\": \"designed-synthetic\"",
                "\"evidenceKind\": \"representative\"",
                StringComparison.Ordinal));
        var result = RunHeadless(
            repositoryRoot,
            [
                "--w7-portfolio-manifest", promotedManifest,
                "--repository-root", repositoryRoot,
                "--dump-root", outputDirectory,
                "--machine-output", Path.Combine(outputDirectory, "w7-promoted.machine.json"),
                "--human-output", Path.Combine(outputDirectory, "w7-promoted.human.txt"),
            ]);
        Assert.Equal(3, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.StandardOutput);
        Assert.Contains("W7_PORTFOLIO_INPUT_INVALID", result.StandardError, StringComparison.Ordinal);
    }

    private static void RunPortfolio(
        string repositoryRoot,
        string manifestPath,
        string outputDirectory,
        string machinePath,
        string humanPath)
    {
        var result = RunHeadless(
            repositoryRoot,
            [
                "--w7-portfolio-manifest", manifestPath,
                "--repository-root", repositoryRoot,
                "--dump-root", outputDirectory,
                "--machine-output", machinePath,
                "--human-output", humanPath,
            ]);
        Assert.True(
            result.ExitCode == 0,
            $"The W7 portfolio consumer exited with {result.ExitCode}. stdout='{result.StandardOutput}' stderr='{result.StandardError}'.");
        Assert.Equal("W7_PORTFOLIO_OK:16:SelectedSyntheticDesignDecision", result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
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
        if (!process.WaitForExit(240_000))
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
            throw new Xunit.Sdk.XunitException("The W7 headless consumer did not exit within its bound.");
        }
        return new ProcessResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult(),
            stderr.GetAwaiter().GetResult());
    }

    private static void AssertTerminal(JsonElement[] rows, string id, string expected) =>
        Assert.Equal(expected, FindRow(rows, id).GetProperty("actual").GetProperty("terminal").GetString());

    private static JsonElement FindRow(JsonElement[] rows, string id) =>
        Assert.Single(rows, row => string.Equals(row.GetProperty("id").GetString(), id, StringComparison.Ordinal));

    private static string RequiredString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString()
        ?? throw new Xunit.Sdk.XunitException($"The required '{propertyName}' string was absent.");

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
