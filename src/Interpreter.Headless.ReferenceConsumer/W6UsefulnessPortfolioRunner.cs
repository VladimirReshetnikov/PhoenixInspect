using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Interpreter.Headless.ReferenceConsumer;

internal static class W6UsefulnessPortfolioRunner
{
    private const int PortfolioSchemaVersion = 3;
    private const int EvaluationReportSchemaVersion = 2;
    private const int UsefulnessReportSchemaVersion = 3;
    private const int RequiredIncidentCount = 24;
    private const int RequiredApplicationShapeCount = 4;
    private const int MinimumQualifyingIncidents = 10;
    private const int MinimumQualifyingApplicationShapes = 2;
    private const int MinimumBoundaryIncidents = 3;
    private const int MinimumBoundaryApplicationShapes = 2;
    private const int MinimumDecisionChangingQuestions = 2;
    private const string EvidenceCaveat =
        "Designed synthetic incidents validate prototype behavior and design decisions only; they are not external observations or field-readiness evidence.";

    internal static bool IsSchemaThreeManifest(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.TryGetProperty("schemaVersion", out var version) &&
                version.ValueKind == JsonValueKind.Number &&
                version.GetInt32() == PortfolioSchemaVersion;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static int Run(
        string manifestPath,
        string? reportRoot,
        string machineOutputPath,
        string humanOutputPath)
    {
        try
        {
            var manifest = LoadManifest(manifestPath);
            var reports = LoadReports(manifest, manifestPath, reportRoot);
            var rows = JoinAndValidate(manifest, reports);
            var aggregate = Aggregate.Create(rows);
            var gate = EvaluateGate(manifest, aggregate);
            var decision = EvaluateDecision(rows, gate);
            WriteMachineReport(machineOutputPath, manifest, rows, aggregate, gate, decision);
            WriteHumanReport(humanOutputPath, manifest, rows, aggregate, gate, decision);
            Console.WriteLine($"W6_USEFULNESS_OK:{rows.Length}:{gate.Status}:{decision.Status}");
            return 0;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"W6_USEFULNESS_INPUT_INVALID:{exception.Message}");
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"W6_USEFULNESS_OUTPUT_FAILED:{exception.GetType().Name}");
            return 5;
        }
    }

    private static PortfolioManifest LoadManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException("The named W6 usefulness manifest does not exist.");
        }

        var manifest = JsonSerializer.Deserialize<PortfolioManifest>(
            File.ReadAllBytes(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            }) ?? throw new InvalidDataException("The W6 usefulness manifest is empty.");
        manifest.Validate();
        return manifest;
    }

    private static ImmutableDictionary<string, EvaluationReport> LoadReports(
        PortfolioManifest manifest,
        string manifestPath,
        string? reportRoot)
    {
        var baseDirectory = reportRoot is null
            ? Path.GetDirectoryName(Path.GetFullPath(manifestPath))!
            : Path.GetFullPath(reportRoot);
        var reports = ImmutableDictionary.CreateBuilder<string, EvaluationReport>(StringComparer.Ordinal);
        foreach (var incident in manifest.Incidents)
        {
            var path = Path.IsPathFullyQualified(incident.ReportPath)
                ? incident.ReportPath
                : Path.Combine(baseDirectory, incident.ReportPath);
            var report = EvaluationReport.Load(incident.IncidentId, path);
            if (!reports.TryAdd(incident.IncidentId, report))
            {
                throw new InvalidDataException($"Incident report '{incident.IncidentId}' is duplicated.");
            }
        }

        if (reports.Values.Select(static report => report.SnapshotSha256)
                .Distinct(StringComparer.Ordinal).Count() != reports.Count)
        {
            throw new InvalidDataException("Every W6 synthetic incident requires an independent dump snapshot.");
        }

        return reports.ToImmutable();
    }

    private static ImmutableArray<PortfolioRow> JoinAndValidate(
        PortfolioManifest manifest,
        ImmutableDictionary<string, EvaluationReport> reports)
    {
        var rows = ImmutableArray.CreateBuilder<PortfolioRow>(manifest.Incidents.Length);
        foreach (var incident in manifest.Incidents)
        {
            var report = reports[incident.IncidentId];
            if (!string.Equals(report.CorpusKind, manifest.CorpusKind, StringComparison.Ordinal) ||
                !string.Equals(report.RootName, incident.Root.Name, StringComparison.Ordinal) ||
                !string.Equals(report.RootTypeName, incident.Root.TypeName, StringComparison.Ordinal) ||
                !string.Equals(report.RootBindingStatus, incident.Root.ExpectedBindingStatus, StringComparison.Ordinal) ||
                !string.Equals(report.RootFixtureEvidenceView, incident.Root.FixtureEvidenceView, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Incident '{incident.IncidentId}' does not reproduce its predeclared root selection.");
            }

            if (!report.Scenarios.TryGetValue(incident.Scenario.Id, out var scenario) ||
                !string.Equals(scenario.Expression, incident.Scenario.Expression, StringComparison.Ordinal) ||
                !string.Equals(scenario.LanguageProfile, incident.Scenario.LanguageProfile, StringComparison.Ordinal) ||
                !string.Equals(scenario.FixtureEvidenceView, incident.Scenario.FixtureEvidenceView, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Incident '{incident.IncidentId}' does not reproduce its predeclared scenario.");
            }

            var admission = DeriveAdmission(scenario);
            var outcome = DeriveProductOutcome(report, scenario);
            ValidateExpected(incident, scenario, outcome);
            var blocker = ParseEnum<DominantBoundary>(incident.Question.DominantBoundary, "dominant boundary");
            var boundary = ParseEnum<FirstBoundaryKind>(incident.Question.FirstBoundary.Kind, "first boundary");
            ValidateQuestion(incident, scenario, outcome, blocker, boundary);
            rows.Add(new PortfolioRow(
                incident.IncidentId,
                incident.ApplicationShape,
                incident.TargetRootTypeName,
                incident.Question.QuestionId,
                incident.Question.UserTask,
                incident.Scenario.Expression,
                incident.Question.RequiredContextKind,
                incident.Question.ContextAttribution,
                incident.Question.RequiredMemberEvidence,
                report.SnapshotSha256,
                incident.Scenario.Id,
                admission,
                scenario.Kind,
                scenario.SemanticMode,
                scenario.Completion,
                scenario.Completeness,
                scenario.Evidence,
                outcome,
                incident.Question.FirstBoundary.Kind,
                incident.Question.FirstBoundary.Code,
                incident.Question.FirstBoundary.Explanation,
                incident.Question.ManualObjectWalkingOperations,
                incident.Question.AnswerUsefulForInvestigation,
                incident.Question.AnswerChangedNextDecision,
                incident.Question.DecisionImpactExplanation,
                blocker,
                scenario.DiagnosticCodes));
        }

        return rows.ToImmutable();
    }

    private static AdmissionPath DeriveAdmission(EvaluationScenario scenario)
    {
        if (!string.Equals(scenario.LanguageProfile, "FixedDepthMemberChainV1", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Scenario '{scenario.Id}' did not retain the W6 language profile.");
        }

        if (scenario.Kind == "DerivedQuery")
        {
            return AdmissionPath.W6;
        }

        if (scenario.Kind != "ClassificationFailure")
        {
            throw new InvalidDataException(
                $"Scenario '{scenario.Id}' has unknown W6 outcome kind '{scenario.Kind}'.");
        }

        return scenario.DiagnosticCodes.Contains("W5_ROOT_SELECTION_NOT_EXACT", StringComparer.Ordinal)
            ? AdmissionPath.ContextRejected
            : AdmissionPath.Unsupported;
    }

    private static ProductOutcome DeriveProductOutcome(
        EvaluationReport report,
        EvaluationScenario scenario)
    {
        if (scenario.Kind == "ClassificationFailure")
        {
            return report.RootBindingStatus switch
            {
                "ExhaustiveAbsence" or "Partial" or "Unavailable" => ProductOutcome.Unavailable,
                "Conflict" => ProductOutcome.Conflicting,
                "Invalid" => ProductOutcome.Invalid,
                _ => ProductOutcome.Unsupported,
            };
        }

        if (scenario.Evidence == "Conflict" ||
            scenario.DiagnosticCodes.Any(static code => code.Contains("CONFLICT", StringComparison.Ordinal)))
        {
            return ProductOutcome.Conflicting;
        }

        if (scenario.Evidence == "Invalid" ||
            scenario.DiagnosticCodes.Any(static code => code.Contains("INVALID", StringComparison.Ordinal)))
        {
            return ProductOutcome.Invalid;
        }

        if (scenario.Completion == "Completed" &&
            scenario.Completeness == "Complete" &&
            scenario.Evidence == "Exact")
        {
            return ProductOutcome.Exact;
        }

        if (scenario.Evidence == "Partial")
        {
            return ProductOutcome.Partial;
        }

        if (scenario.Evidence == "Unavailable")
        {
            return ProductOutcome.Unavailable;
        }

        if (scenario.Completion == "Blocked" && scenario.Evidence == "Exact")
        {
            return ProductOutcome.Blocked;
        }

        return ProductOutcome.Invalid;
    }

    private static void ValidateExpected(
        IncidentDefinition incident,
        EvaluationScenario scenario,
        ProductOutcome outcome)
    {
        var expected = incident.Expected;
        if (!string.Equals(expected.OutcomeKind, scenario.Kind, StringComparison.Ordinal) ||
            !string.Equals(expected.SemanticMode, scenario.SemanticMode, StringComparison.Ordinal) ||
            !string.Equals(expected.Completion, scenario.Completion, StringComparison.Ordinal) ||
            !string.Equals(expected.Completeness, scenario.Completeness, StringComparison.Ordinal) ||
            !string.Equals(expected.Evidence, scenario.Evidence, StringComparison.Ordinal) ||
            !string.Equals(expected.Value, scenario.Value, StringComparison.Ordinal) ||
            outcome != ParseEnum<ProductOutcome>(expected.ProductOutcome, "expected product outcome") ||
            !expected.DiagnosticCodes.SequenceEqual(scenario.DiagnosticCodes, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Incident '{incident.IncidentId}' did not reproduce its predeclared W6 outcome.");
        }
    }

    private static void ValidateQuestion(
        IncidentDefinition incident,
        EvaluationScenario scenario,
        ProductOutcome outcome,
        DominantBoundary blocker,
        FirstBoundaryKind boundary)
    {
        var question = incident.Question;
        if (boundary == FirstBoundaryKind.None &&
            outcome is ProductOutcome.Unavailable or ProductOutcome.Unsupported or ProductOutcome.Conflicting or
                ProductOutcome.Invalid)
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' must name its first stopping boundary.");
        }

        if (boundary != FirstBoundaryKind.None && string.IsNullOrWhiteSpace(question.FirstBoundary.Explanation))
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' must explain its first stopping boundary.");
        }

        if (question.AnswerChangedNextDecision && string.IsNullOrWhiteSpace(question.DecisionImpactExplanation))
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' must explain its claimed decision impact.");
        }

        if (blocker != DominantBoundary.None && boundary == FirstBoundaryKind.None)
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' ranks a boundary without naming where evaluation stopped.");
        }

        if (outcome == ProductOutcome.Exact &&
            question.RequiredMemberEvidence != nameof(EvidenceState.Exact))
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' labels an exact answer with non-exact member evidence.");
        }

        if (scenario.Kind == "ClassificationFailure" && scenario.Value is not null)
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' retained a value for a classification failure.");
        }
    }

    private static PortfolioGate EvaluateGate(PortfolioManifest manifest, Aggregate aggregate)
    {
        var missing = ImmutableArray.CreateBuilder<string>();
        if (!manifest.PredeclaredBeforeEvaluation)
        {
            missing.Add("The portfolio was not declared before evaluation.");
        }

        if (aggregate.DistinctIncidents < MinimumQualifyingIncidents)
        {
            missing.Add($"At least {MinimumQualifyingIncidents} independent incidents are required.");
        }

        if (aggregate.DistinctApplicationShapes < MinimumQualifyingApplicationShapes)
        {
            missing.Add($"At least {MinimumQualifyingApplicationShapes} application shapes are required.");
        }

        return new PortfolioGate(
            missing.Count == 0 ? "SatisfiedSyntheticValidation" : "OpenInsufficientBreadth",
            MinimumQualifyingIncidents,
            MinimumQualifyingApplicationShapes,
            aggregate.DistinctIncidents,
            aggregate.DistinctApplicationShapes,
            aggregate.TotalQuestions,
            missing.ToImmutable());
    }

    private static NextDecision EvaluateDecision(
        ImmutableArray<PortfolioRow> rows,
        PortfolioGate gate)
    {
        if (gate.Status != "SatisfiedSyntheticValidation")
        {
            return new NextDecision(
                "DeferredPortfolioGateOpen",
                null,
                "The meaningful multi-shape breadth gate remains open.",
                ImmutableArray<BoundaryRanking>.Empty);
        }

        var rankings = Enum.GetValues<DominantBoundary>()
            .Where(static boundary => boundary != DominantBoundary.None)
            .Select(boundary =>
            {
                var selected = rows.Where(row => row.DominantBoundary == boundary).ToImmutableArray();
                return new BoundaryRanking(
                    boundary,
                    selected.Select(static row => row.IncidentId).Distinct(StringComparer.Ordinal).Count(),
                    selected.Select(static row => row.ApplicationShape).Distinct(StringComparer.Ordinal).Count(),
                    selected.Count(static row => row.AnswerChangedNextDecision),
                    selected.Count(static row => row.AnswerUsefulForInvestigation),
                    selected.Count(static row =>
                        row.ContextAttribution == nameof(EvidenceState.Exact) &&
                        row.RequiredMemberEvidence == nameof(EvidenceState.Exact)));
            })
            .OrderByDescending(static item => item.IndependentIncidentCount)
            .ThenByDescending(static item => item.DecisionChangingQuestionCount)
            .ThenByDescending(static item => item.UsefulQuestionCount)
            .ThenByDescending(static item => item.ExactEvidenceQuestionCount)
            .ThenBy(static item => item.Boundary)
            .ToImmutableArray();
        var leader = rankings[0];
        var runnerUp = rankings[1];
        var unique = leader.SubstantiveRank != runnerUp.SubstantiveRank;
        var clearsFloor = leader.IndependentIncidentCount >= MinimumBoundaryIncidents &&
            leader.ApplicationShapeCount >= MinimumBoundaryApplicationShapes &&
            leader.DecisionChangingQuestionCount >= MinimumDecisionChangingQuestions;
        if (!unique || !clearsFloor)
        {
            return new NextDecision(
                "DeferredNoUniqueQualifiedBoundary",
                null,
                "No unique recurring boundary clears the predeclared incident, shape, and decision-impact floor.",
                rankings);
        }

        var selection = leader.Boundary switch
        {
            DominantBoundary.RootContextAttribution => "AdmitOneConcreteContextAcquisitionScenario",
            DominantBoundary.ThirdMemberHop => "AdmitOneBoundedDepthThreeScenario",
            DominantBoundary.CollectionNavigation => "AdmitOneIndexedCollectionShape",
            DominantBoundary.ZeroArgumentMethod => "AdmitOneRepeatedMethodDependencyClosure",
            DominantBoundary.TerminalMemberShape => "AdmitOneTerminalMemberShape",
            DominantBoundary.TerminalValueType => "AdmitOneTerminalValueDecoder",
            DominantBoundary.ResultExplanation => "ImproveHeadlessResultExplanation",
            DominantBoundary.ProductThesis => "StopFeatureExpansion",
            _ => throw new InvalidDataException("The W6 boundary ranking returned an unknown category."),
        };
        return new NextDecision(
            "SelectedSyntheticDesignDecision",
            selection,
            "The unique leader clears every predeclared raw-count floor; the selection advances prototype design only.",
            rankings);
    }

    private static void WriteMachineReport(
        string path,
        PortfolioManifest manifest,
        ImmutableArray<PortfolioRow> rows,
        Aggregate aggregate,
        PortfolioGate gate,
        NextDecision decision)
    {
        EnsureParentDirectory(path);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("usefulnessReportSchemaVersion", UsefulnessReportSchemaVersion);
        writer.WriteNumber("portfolioSchemaVersion", manifest.SchemaVersion);
        writer.WriteString("portfolioId", manifest.PortfolioId);
        writer.WriteString("corpusKind", manifest.CorpusKind);
        writer.WriteBoolean("predeclaredBeforeEvaluation", manifest.PredeclaredBeforeEvaluation);
        writer.WriteString("declaredPurpose", manifest.DeclaredPurpose);
        writer.WriteString("evidenceScopeCaveat", EvidenceCaveat);
        writer.WriteBoolean("claimsProductionReadiness", false);
        writer.WriteStartArray("evaluationReports");
        foreach (var row in rows)
        {
            writer.WriteStartObject();
            writer.WriteString("incidentId", row.IncidentId);
            writer.WriteString("dumpSnapshotSha256", row.DumpSnapshotSha256);
            writer.WriteString("applicationShape", row.ApplicationShape);
            writer.WriteString("targetRootTypeName", row.TargetRootTypeName);
            writer.WriteString("scenarioId", row.ScenarioId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("questions");
        foreach (var row in rows)
        {
            WriteRow(writer, row);
        }

        writer.WriteEndArray();
        writer.WriteStartObject("rawCounts");
        WriteAggregate(writer, aggregate);
        writer.WriteStartObject("representativeRows");
        writer.WriteNumber("totalQuestions", 0);
        writer.WriteNumber("distinctIncidents", 0);
        writer.WriteNumber("distinctApplicationShapes", 0);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WritePropertyName("portfolioGate");
        WriteGate(writer, gate);
        writer.WritePropertyName("nextDecision");
        WriteDecision(writer, decision);
        writer.WriteEndObject();
    }

    private static void WriteRow(Utf8JsonWriter writer, PortfolioRow row)
    {
        writer.WriteStartObject();
        writer.WriteString("incidentId", row.IncidentId);
        writer.WriteString("applicationShape", row.ApplicationShape);
        writer.WriteString("targetRootTypeName", row.TargetRootTypeName);
        writer.WriteString("questionId", row.QuestionId);
        writer.WriteString("userTask", row.UserTask);
        writer.WriteString("expressionRequested", row.ExpressionRequested);
        writer.WriteString("requiredContextKind", row.RequiredContextKind);
        writer.WriteString("contextAttribution", row.ContextAttribution);
        writer.WriteString("requiredMemberEvidence", row.RequiredMemberEvidence);
        writer.WriteString("dumpSnapshotSha256", row.DumpSnapshotSha256);
        writer.WriteString("scenarioId", row.ScenarioId);
        writer.WriteString("admission", row.Admission.ToString());
        writer.WriteString("evaluationOutcomeKind", row.EvaluationOutcomeKind);
        WriteNullable(writer, "semanticMode", row.SemanticMode);
        WriteNullable(writer, "completion", row.Completion);
        WriteNullable(writer, "completeness", row.Completeness);
        WriteNullable(writer, "evidence", row.Evidence);
        writer.WriteString("productOutcome", row.ProductOutcome.ToString());
        writer.WriteStartObject("firstBoundary");
        writer.WriteString("kind", row.FirstBoundaryKind);
        WriteNullable(writer, "code", row.FirstBoundaryCode);
        WriteNullable(writer, "explanation", row.FirstBoundaryExplanation);
        writer.WriteEndObject();
        writer.WriteStartArray("manualObjectWalkingOperations");
        foreach (var operation in row.ManualObjectWalkingOperations)
        {
            writer.WriteStringValue(operation);
        }

        writer.WriteEndArray();
        writer.WriteBoolean("answerUsefulForInvestigation", row.AnswerUsefulForInvestigation);
        writer.WriteBoolean("answerChangedNextDecision", row.AnswerChangedNextDecision);
        WriteNullable(writer, "decisionImpactExplanation", row.DecisionImpactExplanation);
        writer.WriteString("dominantBoundary", row.DominantBoundary.ToString());
        writer.WriteStartArray("diagnosticCodes");
        foreach (var code in row.DiagnosticCodes)
        {
            writer.WriteStringValue(code);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteAggregate(Utf8JsonWriter writer, Aggregate aggregate)
    {
        writer.WriteStartObject("allRows");
        writer.WriteNumber("totalQuestions", aggregate.TotalQuestions);
        writer.WriteNumber("distinctIncidents", aggregate.DistinctIncidents);
        writer.WriteNumber("distinctApplicationShapes", aggregate.DistinctApplicationShapes);
        writer.WriteNumber("distinctTargetRootTypes", aggregate.DistinctTargetRootTypes);
        writer.WriteStartObject("admission");
        WriteRatio(writer, "admitted", aggregate.W6Questions, aggregate.TotalQuestions);
        writer.WriteNumber("w6", aggregate.W6Questions);
        writer.WriteNumber("contextRejected", aggregate.ContextRejectedQuestions);
        writer.WriteNumber("unsupported", aggregate.UnsupportedQuestions);
        writer.WriteEndObject();
        WriteRatio(writer, "exactAnswers", aggregate.ExactAnswers, aggregate.TotalQuestions);
        WriteRatio(writer, "usefulAnswers", aggregate.UsefulAnswers, aggregate.TotalQuestions);
        WriteRatio(writer, "decisionChangingAnswers", aggregate.DecisionChangingAnswers, aggregate.TotalQuestions);
        writer.WriteStartObject("outcomeComposition");
        foreach (var item in aggregate.OutcomeComposition)
        {
            writer.WriteNumber(item.Key, item.Value);
        }

        writer.WriteEndObject();
        writer.WriteStartObject("boundaryComposition");
        foreach (var item in aggregate.BoundaryComposition)
        {
            writer.WriteNumber(item.Key, item.Value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteGate(Utf8JsonWriter writer, PortfolioGate gate)
    {
        writer.WriteStartObject();
        writer.WriteString("status", gate.Status);
        writer.WriteNumber("minimumQualifyingIncidents", gate.MinimumQualifyingIncidents);
        writer.WriteNumber("minimumQualifyingApplicationShapes", gate.MinimumQualifyingApplicationShapes);
        writer.WriteNumber("qualifyingIncidentCount", gate.QualifyingIncidentCount);
        writer.WriteNumber("qualifyingApplicationShapeCount", gate.QualifyingApplicationShapeCount);
        writer.WriteNumber("qualifyingQuestionCount", gate.QualifyingQuestionCount);
        writer.WriteStartArray("missingConditions");
        foreach (var condition in gate.MissingConditions)
        {
            writer.WriteStringValue(condition);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteDecision(Utf8JsonWriter writer, NextDecision decision)
    {
        writer.WriteStartObject();
        writer.WriteString("status", decision.Status);
        WriteNullable(writer, "selection", decision.Selection);
        writer.WriteString("rationale", decision.Rationale);
        writer.WriteNumber("minimumBoundaryIncidents", MinimumBoundaryIncidents);
        writer.WriteNumber("minimumBoundaryApplicationShapes", MinimumBoundaryApplicationShapes);
        writer.WriteNumber("minimumDecisionChangingQuestions", MinimumDecisionChangingQuestions);
        writer.WriteStartArray("boundaryRanking");
        foreach (var item in decision.Ranking)
        {
            writer.WriteStartObject();
            writer.WriteString("boundary", item.Boundary.ToString());
            writer.WriteNumber("independentIncidentCount", item.IndependentIncidentCount);
            writer.WriteNumber("applicationShapeCount", item.ApplicationShapeCount);
            writer.WriteNumber("decisionChangingQuestionCount", item.DecisionChangingQuestionCount);
            writer.WriteNumber("usefulQuestionCount", item.UsefulQuestionCount);
            writer.WriteNumber("exactEvidenceQuestionCount", item.ExactEvidenceQuestionCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteHumanReport(
        string path,
        PortfolioManifest manifest,
        ImmutableArray<PortfolioRow> rows,
        Aggregate aggregate,
        PortfolioGate gate,
        NextDecision decision)
    {
        EnsureParentDirectory(path);
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine("W6 usefulness portfolio report v3");
        writer.WriteLine(
            $"Portfolio: {manifest.PortfolioId}; corpus={manifest.CorpusKind}; " +
            $"predeclared={manifest.PredeclaredBeforeEvaluation}");
        writer.WriteLine($"Caveat: {EvidenceCaveat}");
        foreach (var row in rows)
        {
            writer.WriteLine(
                $"{row.IncidentId}/{row.QuestionId}: admission={row.Admission}; outcome={row.ProductOutcome}; " +
                $"context={row.ContextAttribution}; member={row.RequiredMemberEvidence}; " +
                $"useful={row.AnswerUsefulForInvestigation}; changed-decision={row.AnswerChangedNextDecision}; " +
                $"boundary={row.FirstBoundaryKind}; dominant={row.DominantBoundary}");
        }

        writer.WriteLine(
            $"Raw counts: questions={aggregate.TotalQuestions}; incidents={aggregate.DistinctIncidents}; " +
            $"application-shapes={aggregate.DistinctApplicationShapes}; target-root-types={aggregate.DistinctTargetRootTypes}; " +
            $"W6={aggregate.W6Questions}; context-rejected={aggregate.ContextRejectedQuestions}; " +
            $"unsupported={aggregate.UnsupportedQuestions}; exact={aggregate.ExactAnswers}; useful={aggregate.UsefulAnswers}; " +
            $"decision-changing={aggregate.DecisionChangingAnswers}");
        writer.WriteLine("Representative rows: questions=0; incidents=0; application-shapes=0");
        writer.WriteLine(
            $"Portfolio gate: status={gate.Status}; incidents={gate.QualifyingIncidentCount}/{gate.MinimumQualifyingIncidents}; " +
            $"application-shapes={gate.QualifyingApplicationShapeCount}/{gate.MinimumQualifyingApplicationShapes}");
        writer.WriteLine(
            $"Next decision: status={decision.Status}; selection={decision.Selection ?? "none"}; " +
            $"rationale={decision.Rationale}");
    }

    private static void WriteRatio(Utf8JsonWriter writer, string name, int numerator, int denominator)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("numerator", numerator);
        writer.WriteNumber("denominator", denominator);
        writer.WriteEndObject();
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    private static void EnsureParentDirectory(string path) =>
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

    private static T ParseEnum<T>(string value, string label)
        where T : struct, Enum => Enum.TryParse<T>(value, ignoreCase: false, out var parsed) &&
            string.Equals(parsed.ToString(), value, StringComparison.Ordinal)
                ? parsed
                : throw new InvalidDataException($"Unknown {label} '{value}'.");

    private static bool IsIdentity(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-');

    private enum AdmissionPath
    {
        W6,
        ContextRejected,
        Unsupported,
    }

    private enum EvidenceState
    {
        Exact,
        Partial,
        Unavailable,
        Conflicting,
        Invalid,
    }

    private enum ProductOutcome
    {
        Exact,
        Partial,
        Blocked,
        Unavailable,
        Unsupported,
        Conflicting,
        Invalid,
    }

    private enum FirstBoundaryKind
    {
        None,
        Syntax,
        RootSelection,
        ReferenceEvidence,
        TargetValidation,
        TerminalEvidence,
    }

    private enum DominantBoundary
    {
        None,
        RootContextAttribution,
        ThirdMemberHop,
        CollectionNavigation,
        ZeroArgumentMethod,
        TerminalMemberShape,
        TerminalValueType,
        ResultExplanation,
        ProductThesis,
    }

    private sealed record PortfolioRow(
        string IncidentId,
        string ApplicationShape,
        string TargetRootTypeName,
        string QuestionId,
        string UserTask,
        string ExpressionRequested,
        string RequiredContextKind,
        string ContextAttribution,
        string RequiredMemberEvidence,
        string DumpSnapshotSha256,
        string ScenarioId,
        AdmissionPath Admission,
        string EvaluationOutcomeKind,
        string? SemanticMode,
        string? Completion,
        string? Completeness,
        string? Evidence,
        ProductOutcome ProductOutcome,
        string FirstBoundaryKind,
        string? FirstBoundaryCode,
        string? FirstBoundaryExplanation,
        ImmutableArray<string> ManualObjectWalkingOperations,
        bool AnswerUsefulForInvestigation,
        bool AnswerChangedNextDecision,
        string? DecisionImpactExplanation,
        DominantBoundary DominantBoundary,
        ImmutableArray<string> DiagnosticCodes);

    private sealed record PortfolioGate(
        string Status,
        int MinimumQualifyingIncidents,
        int MinimumQualifyingApplicationShapes,
        int QualifyingIncidentCount,
        int QualifyingApplicationShapeCount,
        int QualifyingQuestionCount,
        ImmutableArray<string> MissingConditions);

    private sealed record NextDecision(
        string Status,
        string? Selection,
        string Rationale,
        ImmutableArray<BoundaryRanking> Ranking);

    private sealed record BoundaryRanking(
        DominantBoundary Boundary,
        int IndependentIncidentCount,
        int ApplicationShapeCount,
        int DecisionChangingQuestionCount,
        int UsefulQuestionCount,
        int ExactEvidenceQuestionCount)
    {
        internal (int, int, int, int) SubstantiveRank =>
            (IndependentIncidentCount, DecisionChangingQuestionCount, UsefulQuestionCount, ExactEvidenceQuestionCount);
    }

    private sealed record Aggregate(
        int TotalQuestions,
        int DistinctIncidents,
        int DistinctApplicationShapes,
        int DistinctTargetRootTypes,
        int W6Questions,
        int ContextRejectedQuestions,
        int UnsupportedQuestions,
        int ExactAnswers,
        int UsefulAnswers,
        int DecisionChangingAnswers,
        ImmutableSortedDictionary<string, int> OutcomeComposition,
        ImmutableSortedDictionary<string, int> BoundaryComposition)
    {
        internal static Aggregate Create(ImmutableArray<PortfolioRow> rows) => new(
            rows.Length,
            rows.Select(static row => row.IncidentId).Distinct(StringComparer.Ordinal).Count(),
            rows.Select(static row => row.ApplicationShape).Distinct(StringComparer.Ordinal).Count(),
            rows.Select(static row => row.TargetRootTypeName).Distinct(StringComparer.Ordinal).Count(),
            rows.Count(static row => row.Admission == AdmissionPath.W6),
            rows.Count(static row => row.Admission == AdmissionPath.ContextRejected),
            rows.Count(static row => row.Admission == AdmissionPath.Unsupported),
            rows.Count(static row => row.ProductOutcome == ProductOutcome.Exact),
            rows.Count(static row => row.AnswerUsefulForInvestigation),
            rows.Count(static row => row.AnswerChangedNextDecision),
            CountBy(rows, static row => row.ProductOutcome.ToString()),
            CountBy(rows, static row => row.DominantBoundary.ToString()));

        private static ImmutableSortedDictionary<string, int> CountBy(
            ImmutableArray<PortfolioRow> rows,
            Func<PortfolioRow, string> selector)
        {
            var result = ImmutableSortedDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = selector(row);
                result[key] = result.GetValueOrDefault(key) + 1;
            }

            return result.ToImmutable();
        }
    }

    private sealed class EvaluationReport
    {
        private EvaluationReport(
            string corpusKind,
            string snapshotSha256,
            string rootName,
            string rootTypeName,
            string rootBindingStatus,
            string rootFixtureEvidenceView,
            ImmutableDictionary<string, EvaluationScenario> scenarios)
        {
            CorpusKind = corpusKind;
            SnapshotSha256 = snapshotSha256;
            RootName = rootName;
            RootTypeName = rootTypeName;
            RootBindingStatus = rootBindingStatus;
            RootFixtureEvidenceView = rootFixtureEvidenceView;
            Scenarios = scenarios;
        }

        internal string CorpusKind { get; }

        internal string SnapshotSha256 { get; }

        internal string RootName { get; }

        internal string RootTypeName { get; }

        internal string RootBindingStatus { get; }

        internal string RootFixtureEvidenceView { get; }

        internal ImmutableDictionary<string, EvaluationScenario> Scenarios { get; }

        internal static EvaluationReport Load(string id, string path)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"Evaluation report for '{id}' does not exist.");
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.GetProperty("machineSchemaVersion").GetInt32() != EvaluationReportSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Evaluation report for '{id}' must use machine schema {EvaluationReportSchemaVersion}.");
            }

            var snapshot = RequiredString(root, "dumpSnapshotSha256");
            if (snapshot.Length != 64 || !snapshot.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException($"Evaluation report for '{id}' has an invalid snapshot identity.");
            }

            var rootSelection = root.GetProperty("rootSelection");
            var scenarios = ImmutableDictionary.CreateBuilder<string, EvaluationScenario>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("scenarios").EnumerateArray())
            {
                var scenario = EvaluationScenario.FromJson(element);
                if (!scenarios.TryAdd(scenario.Id, scenario))
                {
                    throw new InvalidDataException(
                        $"Evaluation report for '{id}' duplicates scenario '{scenario.Id}'.");
                }
            }

            return new EvaluationReport(
                RequiredString(root, "corpusKind"),
                snapshot,
                RequiredString(root, "rootName"),
                RequiredString(root, "rootTypeName"),
                RequiredString(rootSelection, "bindingStatus"),
                RequiredString(rootSelection, "fixtureEvidenceView"),
                scenarios.ToImmutable());
        }
    }

    private sealed record EvaluationScenario(
        string Id,
        string? Expression,
        string LanguageProfile,
        string FixtureEvidenceView,
        string Kind,
        string? SemanticMode,
        string? Completion,
        string? Completeness,
        string? Evidence,
        string? Value,
        ImmutableArray<string> DiagnosticCodes)
    {
        internal static EvaluationScenario FromJson(JsonElement element)
        {
            var outcome = element.GetProperty("outcome");
            return new EvaluationScenario(
                RequiredString(element, "id"),
                NullableString(element, "expression"),
                RequiredString(element, "languageProfile"),
                RequiredString(element, "fixtureEvidenceView"),
                RequiredString(outcome, "kind"),
                NullableString(outcome, "semanticMode"),
                NullableString(outcome, "completion"),
                NullableString(outcome, "completeness"),
                NullableString(outcome, "evidence"),
                NullableString(outcome, "value"),
                outcome.GetProperty("diagnostics")
                    .EnumerateArray()
                    .Select(static diagnostic => RequiredString(diagnostic, "code"))
                    .ToImmutableArray());
        }
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"Property '{name}' must be a non-empty string.");
    }

    private static string? NullableString(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => throw new InvalidDataException($"Property '{name}' must be a string or null."),
        };
    }

    private sealed class PortfolioManifest
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("portfolioId")]
        public string PortfolioId { get; init; } = string.Empty;

        [JsonPropertyName("corpusKind")]
        public string CorpusKind { get; init; } = string.Empty;

        [JsonPropertyName("predeclaredBeforeEvaluation")]
        public bool PredeclaredBeforeEvaluation { get; init; }

        [JsonPropertyName("declaredPurpose")]
        public string DeclaredPurpose { get; init; } = string.Empty;

        [JsonPropertyName("incidents")]
        public ImmutableArray<IncidentDefinition> Incidents { get; init; }

        internal void Validate()
        {
            if (SchemaVersion != PortfolioSchemaVersion ||
                !string.Equals(CorpusKind, "SyntheticIncident", StringComparison.Ordinal) ||
                !PredeclaredBeforeEvaluation ||
                !IsIdentity(PortfolioId) ||
                string.IsNullOrWhiteSpace(DeclaredPurpose) ||
                DeclaredPurpose.Length > 4_096 ||
                Incidents.IsDefault ||
                Incidents.Length != RequiredIncidentCount ||
                Incidents.Any(static incident => incident is null))
            {
                throw new InvalidDataException("The W6 portfolio header or fixed incident count is invalid.");
            }

            var incidentIds = new HashSet<string>(StringComparer.Ordinal);
            var questionIds = new HashSet<string>(StringComparer.Ordinal);
            var reportPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var incident in Incidents)
            {
                incident.Validate();
                if (!incidentIds.Add(incident.IncidentId) ||
                    !questionIds.Add(incident.Question.QuestionId) ||
                    !reportPaths.Add(incident.ReportPath))
                {
                    throw new InvalidDataException("W6 incident, question, and report identities must be unique.");
                }
            }

            if (Incidents.Select(static incident => incident.ApplicationShape)
                    .Distinct(StringComparer.Ordinal).Count() != RequiredApplicationShapeCount ||
                Incidents.Select(static incident => incident.TargetRootTypeName)
                    .Distinct(StringComparer.Ordinal).Count() != RequiredApplicationShapeCount)
            {
                throw new InvalidDataException("The W6 portfolio requires four distinct application and root shapes.");
            }
        }
    }

    private sealed class IncidentDefinition
    {
        [JsonPropertyName("incidentId")]
        public string IncidentId { get; init; } = string.Empty;

        [JsonPropertyName("applicationShape")]
        public string ApplicationShape { get; init; } = string.Empty;

        [JsonPropertyName("targetRootTypeName")]
        public string TargetRootTypeName { get; init; } = string.Empty;

        [JsonPropertyName("targetArguments")]
        public ImmutableArray<string> TargetArguments { get; init; }

        [JsonPropertyName("reportPath")]
        public string ReportPath { get; init; } = string.Empty;

        [JsonPropertyName("root")]
        public RootDefinition Root { get; init; } = null!;

        [JsonPropertyName("scenario")]
        public ScenarioDefinition Scenario { get; init; } = null!;

        [JsonPropertyName("expected")]
        public ExpectedDefinition Expected { get; init; } = null!;

        [JsonPropertyName("question")]
        public QuestionDefinition Question { get; init; } = null!;

        internal void Validate()
        {
            if (!IsIdentity(IncidentId) || !IsIdentity(ApplicationShape) ||
                string.IsNullOrWhiteSpace(TargetRootTypeName) || TargetRootTypeName.Length > 4_096 ||
                TargetArguments.IsDefaultOrEmpty || TargetArguments.Length > 16 ||
                TargetArguments.Any(static argument => string.IsNullOrWhiteSpace(argument) || argument.Length > 4_096) ||
                string.IsNullOrWhiteSpace(ReportPath) || ReportPath.Length > 4_096 ||
                Root is null || Scenario is null || Expected is null || Question is null)
            {
                throw new InvalidDataException($"Incident '{IncidentId}' is missing bounded fixture data.");
            }

            Root.Validate();
            Scenario.Validate();
            Expected.Validate();
            Question.Validate();
            if (!string.Equals(IncidentId, Question.QuestionId, StringComparison.Ordinal) ||
                !string.Equals(Scenario.Id, Question.QuestionId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Incident '{IncidentId}' must use one shared incident/question/scenario identity.");
            }
        }
    }

    private sealed class RootDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("typeName")]
        public string TypeName { get; init; } = string.Empty;

        [JsonPropertyName("maximumMatches")]
        public int MaximumMatches { get; init; }

        [JsonPropertyName("maximumHandlesScanned")]
        public int MaximumHandlesScanned { get; init; }

        [JsonPropertyName("fixtureEvidenceView")]
        public string FixtureEvidenceView { get; init; } = string.Empty;

        [JsonPropertyName("expectedBindingStatus")]
        public string ExpectedBindingStatus { get; init; } = string.Empty;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128 ||
                string.IsNullOrWhiteSpace(TypeName) || TypeName.Length > 4_096 ||
                MaximumMatches is < 1 or > 4_096 || MaximumHandlesScanned is < 1 or > 100_000 ||
                FixtureEvidenceView is not ("Captured" or "Partial" or "Unavailable" or "Conflict" or "Invalid") ||
                ExpectedBindingStatus is not (
                    "ExactObject" or "ExhaustiveAbsence" or "Partial" or "Unavailable" or "Conflict" or "Invalid"))
            {
                throw new InvalidDataException("A W6 root selector is outside its deterministic contract.");
            }
        }
    }

    private sealed class ScenarioDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("expression")]
        public string Expression { get; init; } = string.Empty;

        [JsonPropertyName("methodMode")]
        public string MethodMode { get; init; } = string.Empty;

        [JsonPropertyName("languageProfile")]
        public string LanguageProfile { get; init; } = string.Empty;

        [JsonPropertyName("instructionLimit")]
        public long InstructionLimit { get; init; }

        [JsonPropertyName("logicalDepthLimit")]
        public int LogicalDepthLimit { get; init; }

        [JsonPropertyName("traversalLimit")]
        public int TraversalLimit { get; init; }

        [JsonPropertyName("fixtureEvidenceView")]
        public string FixtureEvidenceView { get; init; } = string.Empty;

        [JsonPropertyName("cancelBeforeExecution")]
        public bool CancelBeforeExecution { get; init; }

        [JsonPropertyName("repeatCount")]
        public int RepeatCount { get; init; }

        internal void Validate()
        {
            if (!IsIdentity(Id) || string.IsNullOrWhiteSpace(Expression) || Expression.Length > 512 ||
                MethodMode is not ("Interpreted" or "Modeled") ||
                LanguageProfile != "FixedDepthMemberChainV1" ||
                FixtureEvidenceView is not (
                    "Captured" or "ReferencePartial" or "ReferenceUnavailable" or "TargetConflict" or
                    "TargetInvalid" or "StringPartialLimit") ||
                InstructionLimit is < 0 or > 1_000_000 || LogicalDepthLimit is < 0 or > 256 ||
                TraversalLimit is < 0 or > 1_000_000 || RepeatCount is < 1 or > 4)
            {
                throw new InvalidDataException($"Scenario '{Id}' is outside the W6 deterministic contract.");
            }
        }
    }

    private sealed class ExpectedDefinition
    {
        [JsonPropertyName("outcomeKind")]
        public string OutcomeKind { get; init; } = string.Empty;

        [JsonPropertyName("semanticMode")]
        public string? SemanticMode { get; init; }

        [JsonPropertyName("completion")]
        public string? Completion { get; init; }

        [JsonPropertyName("completeness")]
        public string? Completeness { get; init; }

        [JsonPropertyName("evidence")]
        public string? Evidence { get; init; }

        [JsonPropertyName("value")]
        public string? Value { get; init; }

        [JsonPropertyName("productOutcome")]
        public string ProductOutcome { get; init; } = string.Empty;

        [JsonPropertyName("diagnosticCodes")]
        public ImmutableArray<string> DiagnosticCodes { get; init; }

        internal void Validate()
        {
            if (OutcomeKind is not ("DerivedQuery" or "ClassificationFailure") ||
                SemanticMode is not (null or "DerivedQuery") ||
                Completion is not (null or "Completed" or "Blocked" or "Invalid") ||
                Completeness is not (null or "Complete" or "Partial" or "None") ||
                Evidence is not (null or "Exact" or "Partial" or "Unavailable" or "Conflict" or "Invalid") ||
                Value?.Length > 4_096 ||
                DiagnosticCodes.IsDefault || DiagnosticCodes.Length > 8 ||
                DiagnosticCodes.Any(static code => string.IsNullOrWhiteSpace(code) || code.Length > 256))
            {
                throw new InvalidDataException("A W6 expected outcome is outside its bounded contract.");
            }

            _ = ParseEnum<ProductOutcome>(ProductOutcome, "expected product outcome");
        }
    }

    private sealed class QuestionDefinition
    {
        [JsonPropertyName("questionId")]
        public string QuestionId { get; init; } = string.Empty;

        [JsonPropertyName("userTask")]
        public string UserTask { get; init; } = string.Empty;

        [JsonPropertyName("requiredContextKind")]
        public string RequiredContextKind { get; init; } = string.Empty;

        [JsonPropertyName("contextAttribution")]
        public string ContextAttribution { get; init; } = string.Empty;

        [JsonPropertyName("requiredMemberEvidence")]
        public string RequiredMemberEvidence { get; init; } = string.Empty;

        [JsonPropertyName("firstBoundary")]
        public BoundaryDefinition FirstBoundary { get; init; } = null!;

        [JsonPropertyName("manualObjectWalkingOperations")]
        public ImmutableArray<string> ManualObjectWalkingOperations { get; init; }

        [JsonPropertyName("answerUsefulForInvestigation")]
        public bool AnswerUsefulForInvestigation { get; init; }

        [JsonPropertyName("answerChangedNextDecision")]
        public bool AnswerChangedNextDecision { get; init; }

        [JsonPropertyName("decisionImpactExplanation")]
        public string? DecisionImpactExplanation { get; init; }

        [JsonPropertyName("dominantBoundary")]
        public string DominantBoundary { get; init; } = string.Empty;

        internal void Validate()
        {
            if (!IsIdentity(QuestionId) || string.IsNullOrWhiteSpace(UserTask) || UserTask.Length > 4_096 ||
                string.IsNullOrWhiteSpace(RequiredContextKind) || RequiredContextKind.Length > 1_024 ||
                FirstBoundary is null ||
                ManualObjectWalkingOperations.IsDefaultOrEmpty || ManualObjectWalkingOperations.Length > 16 ||
                ManualObjectWalkingOperations.Any(static operation =>
                    string.IsNullOrWhiteSpace(operation) || operation.Length > 1_024) ||
                DecisionImpactExplanation?.Length > 2_048)
            {
                throw new InvalidDataException($"Question '{QuestionId}' is outside its bounded contract.");
            }

            _ = ParseEnum<EvidenceState>(ContextAttribution, "context attribution");
            _ = ParseEnum<EvidenceState>(RequiredMemberEvidence, "required member evidence");
            _ = ParseEnum<DominantBoundary>(DominantBoundary, "dominant boundary");
            FirstBoundary.Validate();
        }
    }

    private sealed class BoundaryDefinition
    {
        [JsonPropertyName("kind")]
        public string Kind { get; init; } = string.Empty;

        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; init; }

        internal void Validate()
        {
            var kind = ParseEnum<FirstBoundaryKind>(Kind, "first boundary");
            if (Code?.Length > 256 || Explanation?.Length > 2_048 ||
                kind == FirstBoundaryKind.None && (Code is not null || Explanation is not null))
            {
                throw new InvalidDataException("A W6 first-boundary record is invalid.");
            }
        }
    }
}
