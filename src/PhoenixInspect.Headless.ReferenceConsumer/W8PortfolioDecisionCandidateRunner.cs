using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhoenixInspect.Headless.ReferenceConsumer;

/// <summary>
/// Emits deterministic machine and human reports over the content-addressed W8.9 portfolio decision candidate.
/// </summary>
internal static class W8PortfolioDecisionCandidateRunner
{
    private const string CandidateId = "interpreter-w8-static-field-portfolio-decision-candidate-v1";
    private const string CandidateSha256 = "a6b35b67d35c00449dac632dc61ed4b269e9bfd679552a1e8dbea4cc34a20450";

    internal static bool IsRequested(string[] args) =>
        args.Contains("--w8-decision-candidate", StringComparer.Ordinal);

    internal static int Run(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            var candidateBytes = File.ReadAllBytes(options.CandidatePath);
            using var candidate = JsonDocument.Parse(candidateBytes);
            var reports = BuildReports(candidate.RootElement, Hash(candidateBytes));
            WriteReports(options, reports);
            Console.WriteLine($"W8_DECISION_CANDIDATE_OK:{Hash(candidateBytes)}");
            return 0;
        }
        catch (W8DecisionCandidateArgumentException exception)
        {
            Console.Error.WriteLine($"W8_DECISION_CANDIDATE_ARGUMENT_INVALID:{exception.Message}");
            return 2;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException or
                ArgumentOutOfRangeException or KeyNotFoundException)
        {
            Console.Error.WriteLine($"W8_DECISION_CANDIDATE_INPUT_INVALID:{exception.Message}");
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"W8_DECISION_CANDIDATE_OUTPUT_FAILED:{exception.GetType().Name}");
            return 5;
        }
    }

    private static Reports BuildReports(JsonElement root, string candidateSha256)
    {
        if (!string.Equals(candidateSha256, CandidateSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The W8 decision-candidate bytes do not match the frozen version-one identity '{CandidateSha256}'.");
        }
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            !string.Equals(RequiredString(root, "candidateId"), CandidateId, StringComparison.Ordinal) ||
            !string.Equals(
                RequiredString(root, "evidenceKind"),
                "derived-designed-synthetic-decision-candidate",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The W8 decision-candidate runner accepts only the version-one designed-synthetic candidate record.");
        }
        foreach (var prohibitedProperty in new[]
                 {
                     "decisionAuthority",
                     "finalDecision",
                     "counterfactualOwnerDispositions",
                 })
        {
            if (root.TryGetProperty(prohibitedProperty, out _))
            {
                throw new InvalidDataException(
                    $"The W8 decision candidate must not carry the final/authority property '{prohibitedProperty}'.");
            }
        }

        var inputs = root.GetProperty("evidenceInputs").EnumerateArray().ToArray();
        if (inputs.Length != 2)
        {
            throw new InvalidDataException("The W8 decision candidate must name exactly two evidence inputs.");
        }

        var metrics = root.GetProperty("candidatePortfolioMetrics");
        if (!string.Equals(
                RequiredString(metrics, "calculationBasis"),
                "conditional-on-unapproved-proposed-dispositions",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The W8 decision-candidate metrics must remain conditional on the unapproved proposed dispositions.");
        }
        var candidate = root.GetProperty("candidateSelection");
        if (!string.Equals(
                RequiredString(candidate, "status"),
                "computed-under-proposals-pending-owner-approval",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The W8 decision candidate must remain pending owner approval.");
        }
        var scopeLimits = root.GetProperty("scopeLimits");
        foreach (var falseClaim in new[]
                 {
                     "ownerAuthorityClaimed",
                     "w8_9ClosureClaimed",
                     "representativeEvidenceClaimed",
                     "proposedSuccessorImplementedByW8",
                     "w8_10ClosureClaimed",
                 })
        {
            if (scopeLimits.GetProperty(falseClaim).GetBoolean())
            {
                throw new InvalidDataException(
                    $"The W8 decision-candidate scope limit '{falseClaim}' must remain false.");
            }
        }
        var categories = root.GetProperty("candidateCategoryMetrics")
            .EnumerateArray()
            .Select(static category => ReadCategory(category))
            .OrderBy(static category => category.Name, StringComparer.Ordinal)
            .ToArray();
        if (categories.Length == 0 ||
            categories.Select(static category => category.Name).Distinct(StringComparer.Ordinal).Count() !=
                categories.Length)
        {
            throw new InvalidDataException("The W8 decision candidate must name distinct successor categories.");
        }

        var machine = new List<string>
        {
            $"candidate.id={RequiredString(root, "candidateId")}",
            $"candidate.sha256={candidateSha256}",
            $"input.v1.sha256={RequiredString(inputs[0], "sha256")}",
            $"input.v2.sha256={RequiredString(inputs[1], "sha256")}",
            $"candidate.metric.calculation-basis={RequiredString(metrics, "calculationBasis")}",
            $"candidate.metric.incidents={Number(metrics.GetProperty("incidentCount").GetInt32())}",
            $"candidate.metric.useful={Number(metrics.GetProperty("usefulCount").GetInt32())}",
            $"candidate.metric.decision-changing={Number(metrics.GetProperty("decisionChangingCount").GetInt32())}",
            $"candidate.metric.attributable={Number(metrics.GetProperty("attributableEvidenceCount").GetInt32())}",
            $"candidate.metric.first-boundary={Number(metrics.GetProperty("firstBoundaryCount").GetInt32())}",
            $"candidate.metric.executed={Number(metrics.GetProperty("executedBaselineCount").GetInt32())}",
            $"candidate.metric.manifest-only={Number(metrics.GetProperty("manifestOnlyBaselineCount").GetInt32())}",
            $"candidate.proposal.counterfactual.retired={Number(metrics.GetProperty("retiredCounterfactualCount").GetInt32())}",
            $"candidate.proposal.counterfactual.deferred={Number(metrics.GetProperty("deferredCounterfactualCount").GetInt32())}",
        };
        foreach (var category in categories)
        {
            machine.Add($"candidate.category.{category.Name}.key={category.SubstantiveKey}");
        }

        machine.Add($"candidate.status={RequiredString(candidate, "status")}");
        machine.Add($"candidate.tie-defers={candidate.GetProperty("tieDefers").GetBoolean().ToString().ToLowerInvariant()}");
        machine.Add($"candidate.category={RequiredString(candidate, "proposedCategory")}");
        machine.Add($"candidate.action={RequiredString(candidate, "proposedAction")}");
        machine.Add("candidate.owner-authority=false");
        machine.Add("candidate.implemented-by-w8=false");
        machine.Add("representative.count=0");
        var machineReport = string.Join('\n', machine) + '\n';

        var humanReport =
            "W8.9 meaningful-synthetic portfolio decision candidate\n" +
            $"Candidate record: {candidateSha256}\n" +
            $"Baselines: {Number(metrics.GetProperty("executedBaselineCount").GetInt32())} executed, " +
            $"{Number(metrics.GetProperty("manifestOnlyBaselineCount").GetInt32())} manifest-only; representative observations: 0\n" +
            $"Candidate metrics under unapproved proposed dispositions: {Number(metrics.GetProperty("usefulCount").GetInt32())} useful, " +
            $"{Number(metrics.GetProperty("decisionChangingCount").GetInt32())} decision-changing, " +
            $"{Number(metrics.GetProperty("attributableEvidenceCount").GetInt32())} attributable, " +
            $"{Number(metrics.GetProperty("firstBoundaryCount").GetInt32())} with a first boundary\n" +
            $"Proposed counterfactual dispositions: {Number(metrics.GetProperty("retiredCounterfactualCount").GetInt32())} retired, " +
            $"{Number(metrics.GetProperty("deferredCounterfactualCount").GetInt32())} deferred\n" +
            $"Proposed future category: {RequiredString(candidate, "proposedCategory")} " +
            $"({RequiredString(candidate, "winningSubstantiveKey")}); no substantive tie\n" +
            $"Proposed action: {RequiredString(candidate, "proposedAction")}\n" +
            "Owner authority: pending; W8 implementation: not implemented; W8.9/W8.10 closure: not claimed\n";
        return new Reports(machineReport, humanReport);
    }

    private static Category ReadCategory(JsonElement category)
    {
        var result = new Category(
            RequiredString(category, "category"),
            category.GetProperty("incidentCount").GetInt32(),
            category.GetProperty("applicationShapeCount").GetInt32(),
            category.GetProperty("decisionChangingCount").GetInt32(),
            category.GetProperty("attributableEvidenceCount").GetInt32());
        if (!string.Equals(
                result.SubstantiveKey,
                RequiredString(category, "substantiveKey"),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The W8 category '{result.Name}' substantive key does not match its component counts.");
        }

        return result;
    }

    private static void WriteReports(Options options, Reports reports)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.MachineOutputPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(options.HumanOutputPath)!);
        FileStream? machine = null;
        FileStream? human = null;
        var machineCreated = false;
        var humanCreated = false;
        try
        {
            machine = new FileStream(
                options.MachineOutputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            machineCreated = true;
            human = new FileStream(
                options.HumanOutputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            humanCreated = true;

            WriteReport(machine, reports.MachineReport);
            WriteReport(human, reports.HumanReport);
        }
        catch
        {
            machine?.Dispose();
            human?.Dispose();
            if (machineCreated)
            {
                File.Delete(options.MachineOutputPath);
            }
            if (humanCreated)
            {
                File.Delete(options.HumanOutputPath);
            }
            throw;
        }
        finally
        {
            machine?.Dispose();
            human?.Dispose();
        }
    }

    private static void WriteReport(Stream stream, string report)
    {
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true);
        writer.Write(report);
        writer.Flush();
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The required W8 decision-candidate '{name}' string was absent.")
            : value;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record Reports(string MachineReport, string HumanReport);

    private sealed record Category(
        string Name,
        int IncidentCount,
        int ApplicationShapeCount,
        int DecisionChangingCount,
        int AttributableEvidenceCount)
    {
        internal string SubstantiveKey =>
            $"{IncidentCount}:{ApplicationShapeCount}:{DecisionChangingCount}:{AttributableEvidenceCount}";
    }

    private sealed record Options(string CandidatePath, string MachineOutputPath, string HumanOutputPath)
    {
        internal static Options Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length ||
                    args[index] is not ("--w8-decision-candidate" or "--machine-output" or "--human-output") ||
                    !values.TryAdd(args[index], args[index + 1]) ||
                    string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new W8DecisionCandidateArgumentException(
                        "Expected three distinct W8 decision-candidate option/value pairs.");
                }
            }

            if (values.Count != 3)
            {
                throw new W8DecisionCandidateArgumentException(
                    "Expected three distinct W8 decision-candidate option/value pairs.");
            }

            var candidatePath = Path.GetFullPath(values["--w8-decision-candidate"]);
            var machineOutputPath = Path.GetFullPath(values["--machine-output"]);
            var humanOutputPath = Path.GetFullPath(values["--human-output"]);
            var pathComparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            if (new[] { candidatePath, machineOutputPath, humanOutputPath }
                    .Distinct(pathComparer)
                    .Count() != 3)
            {
                throw new W8DecisionCandidateArgumentException(
                    "The candidate, machine-output, and human-output paths must be pairwise distinct.");
            }
            foreach (var outputPath in new[] { machineOutputPath, humanOutputPath })
            {
                if (File.Exists(outputPath) || Directory.Exists(outputPath))
                {
                    throw new W8DecisionCandidateArgumentException(
                        $"Refusing to overwrite the existing decision-candidate output path '{outputPath}'.");
                }
            }

            return new Options(candidatePath, machineOutputPath, humanOutputPath);
        }
    }

    private sealed class W8DecisionCandidateArgumentException(string message) : Exception(message);
}
