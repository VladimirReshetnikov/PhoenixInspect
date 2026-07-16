using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Interpreter.Headless.ReferenceConsumer;

internal static class UsefulnessPortfolioRunner
{
    private const int PortfolioSchemaVersion = 1;
    private const int UsefulnessReportSchemaVersion = 1;
    private const int EvaluationReportSchemaVersion = 1;
    private const int MinimumRepresentativeIncidents = 10;
    private const int MinimumRepresentativeApplicationShapes = 2;
    private const int MaximumEvaluationReports = 128;
    private const int MaximumQuestions = 512;
    private const string GeneratedCaveat =
        "Generated validation rows exercise the runner only; they do not count as representative incident evidence.";

    internal static bool IsRequested(string[] args) =>
        args.Contains("--portfolio-manifest", StringComparer.Ordinal);

    internal static int Run(string[] args)
    {
        try
        {
            var options = PortfolioCommandLineOptions.Parse(args);
            var manifest = LoadManifest(options.ManifestPath);
            var reports = LoadEvaluationReports(manifest, options);
            var rows = JoinQuestions(manifest, reports);
            var allRows = RawAggregate.Create(rows);
            var representativeRows = manifest.CorpusKind == nameof(CorpusKind.RepresentativeIncident)
                ? allRows
                : RawAggregate.Create(ImmutableArray<UsefulnessRow>.Empty);
            var gate = EvaluateRepresentativeGate(manifest, rows, representativeRows);
            var decision = EvaluateNextDecision(rows, gate);

            WriteMachineReport(
                options.MachineOutputPath,
                manifest,
                reports.Values.OrderBy(static report => report.Id, StringComparer.Ordinal).ToImmutableArray(),
                rows,
                allRows,
                representativeRows,
                gate,
                decision);
            WriteHumanReport(
                options.HumanOutputPath,
                manifest,
                rows,
                allRows,
                representativeRows,
                gate,
                decision);
            Console.WriteLine($"W5_USEFULNESS_OK:{rows.Length}:{gate.Status}");
            return 0;
        }
        catch (PortfolioCommandLineException exception)
        {
            Console.Error.WriteLine($"W5_USEFULNESS_ARGUMENT_INVALID:{exception.Message}");
            return 2;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"W5_USEFULNESS_INPUT_INVALID:{exception.Message}");
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"W5_USEFULNESS_OUTPUT_FAILED:{exception.GetType().Name}");
            return 5;
        }
    }

    private static PortfolioManifest LoadManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException("The named usefulness manifest does not exist.");
        }

        var manifest = JsonSerializer.Deserialize<PortfolioManifest>(
            File.ReadAllBytes(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            }) ?? throw new InvalidDataException("The usefulness manifest is empty.");
        manifest.Validate();
        return manifest;
    }

    private static ImmutableDictionary<string, EvaluationReport> LoadEvaluationReports(
        PortfolioManifest manifest,
        PortfolioCommandLineOptions options)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, EvaluationReport>(StringComparer.Ordinal);
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(options.ManifestPath))!;
        var reportRoot = options.ReportRoot is null
            ? manifestDirectory
            : Path.GetFullPath(options.ReportRoot);
        foreach (var definition in manifest.EvaluationReports)
        {
            var path = Path.IsPathFullyQualified(definition.Path)
                ? definition.Path
                : Path.Combine(reportRoot, definition.Path);
            var report = EvaluationReport.Load(definition.Id, path);
            if (!string.Equals(report.CorpusKind, manifest.CorpusKind, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Evaluation report '{definition.Id}' is '{report.CorpusKind}' but the portfolio is " +
                    $"'{manifest.CorpusKind}'; corpus kinds cannot be promoted or mixed.");
            }

            if (!builder.TryAdd(definition.Id, report))
            {
                throw new InvalidDataException($"Evaluation report id '{definition.Id}' is duplicated.");
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<UsefulnessRow> JoinQuestions(
        PortfolioManifest manifest,
        ImmutableDictionary<string, EvaluationReport> reports)
    {
        var rows = ImmutableArray.CreateBuilder<UsefulnessRow>(manifest.Questions.Length);
        foreach (var question in manifest.Questions)
        {
            if (!reports.TryGetValue(question.EvaluationReportId, out var report))
            {
                throw new InvalidDataException(
                    $"Question '{question.QuestionId}' names unknown evaluation report '{question.EvaluationReportId}'.");
            }

            if (!report.Scenarios.TryGetValue(question.ScenarioId, out var scenario))
            {
                throw new InvalidDataException(
                    $"Question '{question.QuestionId}' names unknown scenario '{question.ScenarioId}'.");
            }

            if (!string.Equals(question.ExpressionRequested, scenario.Expression, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Question '{question.QuestionId}' does not exactly match its evaluated expression.");
            }

            var admission = DeriveAdmission(scenario);
            var outcome = DeriveProductOutcome(scenario);
            var boundary = ParseEnum<FirstBoundaryKind>(question.FirstBoundary.Kind, "first-boundary kind");
            var blocker = ParseEnum<DominantBlocker>(question.DominantBlocker, "dominant blocker");
            ValidateQuestionAgainstOutcome(question, scenario, admission, outcome, boundary, blocker);
            rows.Add(new UsefulnessRow(
                question.IncidentId,
                question.ApplicationShape,
                question.QuestionId,
                question.UserTask,
                question.ExpressionRequested,
                question.RequiredContextKind,
                question.ContextAttribution,
                question.RequiredMemberEvidence,
                question.EvaluationReportId,
                report.DumpSnapshotSha256,
                question.ScenarioId,
                admission,
                scenario.Kind,
                scenario.SemanticMode,
                scenario.Completion,
                scenario.Completeness,
                scenario.Evidence,
                outcome,
                question.FirstBoundary.Kind,
                question.FirstBoundary.Code,
                question.FirstBoundary.Explanation,
                question.ManualObjectWalkingOperationsKnown,
                question.ManualObjectWalkingOperations,
                question.AnswerUsefulForInvestigation,
                question.AnswerChangedNextDecision,
                question.DecisionImpactExplanation,
                blocker,
                scenario.DiagnosticCodes));
        }

        return rows.ToImmutable();
    }

    private static AdmissionPath DeriveAdmission(EvaluationScenario scenario) => scenario.Kind switch
    {
        "DerivedQuery" => AdmissionPath.W2,
        "CounterfactualExecution" or "CounterfactualPreparationFailure" or "AcquisitionFailure" =>
            AdmissionPath.W4,
        "ClassificationFailure" => AdmissionPath.Unsupported,
        _ => throw new InvalidDataException($"Scenario '{scenario.Id}' has unknown outcome kind '{scenario.Kind}'."),
    };

    private static ProductOutcome DeriveProductOutcome(EvaluationScenario scenario)
    {
        if (scenario.Kind == "ClassificationFailure")
        {
            return ProductOutcome.Unsupported;
        }

        if (scenario.Kind == "AcquisitionFailure")
        {
            if (scenario.DiagnosticCodes.Any(static code =>
                    code.Contains("AMBIGUOUS", StringComparison.Ordinal) ||
                    code.Contains("CONFLICT", StringComparison.Ordinal)))
            {
                return ProductOutcome.Conflicting;
            }

            if (scenario.DiagnosticCodes.Any(static code => code.Contains("INVALID", StringComparison.Ordinal)))
            {
                return ProductOutcome.Invalid;
            }

            return ProductOutcome.Unavailable;
        }

        if (scenario.Kind == "CounterfactualPreparationFailure" &&
            scenario.DiagnosticCodes.Any(static code => code.Contains("UNSUPPORTED", StringComparison.Ordinal)))
        {
            return ProductOutcome.Unsupported;
        }

        if (scenario.Completion == "Completed" &&
            scenario.Completeness == "Complete" &&
            scenario.Evidence == "Exact")
        {
            return ProductOutcome.Exact;
        }

        if (scenario.Completion == "Completed" && scenario.Evidence == "Partial")
        {
            return ProductOutcome.Partial;
        }

        if (scenario.Completion == "Completed" &&
            (scenario.Evidence == "Unavailable" ||
             scenario.Value?.StartsWith("unknown:", StringComparison.Ordinal) == true))
        {
            return ProductOutcome.Unknown;
        }

        if (scenario.Completion is "BudgetExhausted" or "Cancelled")
        {
            return ProductOutcome.Unknown;
        }

        return ProductOutcome.Invalid;
    }

    private static void ValidateQuestionAgainstOutcome(
        QuestionDefinition question,
        EvaluationScenario scenario,
        AdmissionPath admission,
        ProductOutcome outcome,
        FirstBoundaryKind boundary,
        DominantBlocker blocker)
    {
        if (admission == AdmissionPath.Unsupported && outcome != ProductOutcome.Unsupported)
        {
            throw new InvalidDataException($"Question '{question.QuestionId}' has inconsistent unsupported routing.");
        }

        if (outcome == ProductOutcome.Exact &&
            ParseEnum<EvidenceState>(question.RequiredMemberEvidence, "member-evidence state") != EvidenceState.Exact)
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' cannot label an exact answer with non-exact member evidence.");
        }

        if (boundary == FirstBoundaryKind.None &&
            outcome is ProductOutcome.Unsupported or ProductOutcome.Unavailable or ProductOutcome.Conflicting or ProductOutcome.Invalid)
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' must name the first boundary for its terminal outcome.");
        }

        if (boundary != FirstBoundaryKind.None && string.IsNullOrWhiteSpace(question.FirstBoundary.Explanation))
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' must explain its first named boundary.");
        }

        if (question.AnswerChangedNextDecision && string.IsNullOrWhiteSpace(question.DecisionImpactExplanation))
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' must explain a claimed decision change.");
        }

        if (!question.ManualObjectWalkingOperationsKnown && !question.ManualObjectWalkingOperations.IsEmpty)
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' supplies manual operations while declaring them unknown.");
        }

        if (blocker == DominantBlocker.None && boundary != FirstBoundaryKind.None &&
            outcome is ProductOutcome.Unsupported or ProductOutcome.Unavailable)
        {
            throw new InvalidDataException(
                $"Question '{question.QuestionId}' must rank a blocker for an unsupported or unavailable outcome.");
        }

        if ((admission == AdmissionPath.W2 && scenario.SemanticMode != "DerivedQuery") ||
            (admission == AdmissionPath.W4 && scenario.SemanticMode is not ("CounterfactualExecution" or null)))
        {
            throw new InvalidDataException($"Question '{question.QuestionId}' has inconsistent semantic routing.");
        }
    }

    private static RepresentativeGate EvaluateRepresentativeGate(
        PortfolioManifest manifest,
        ImmutableArray<UsefulnessRow> rows,
        RawAggregate representativeRows)
    {
        var missing = ImmutableArray.CreateBuilder<string>();
        if (manifest.CorpusKind != nameof(CorpusKind.RepresentativeIncident))
        {
            missing.Add("The portfolio is generated runner validation, not representative incident evidence.");
        }

        if (!manifest.PredeclaredBeforeEvaluation)
        {
            missing.Add("The portfolio was not declared before evaluation.");
        }

        if (representativeRows.DistinctIncidents < MinimumRepresentativeIncidents)
        {
            missing.Add(
                $"Representative incident count is {representativeRows.DistinctIncidents}; " +
                $"the gate requires at least {MinimumRepresentativeIncidents}.");
        }

        if (representativeRows.DistinctApplicationShapes < MinimumRepresentativeApplicationShapes)
        {
            missing.Add(
                $"Representative application-shape count is {representativeRows.DistinctApplicationShapes}; " +
                $"the gate requires at least {MinimumRepresentativeApplicationShapes}.");
        }

        var status = missing.Count == 0
            ? RepresentativeGateStatus.Satisfied
            : manifest.CorpusKind == nameof(CorpusKind.GeneratedValidation)
                ? RepresentativeGateStatus.OpenMissingRepresentativeCorpus
                : RepresentativeGateStatus.OpenInsufficientRepresentativeBreadth;
        return new RepresentativeGate(
            status,
            MinimumRepresentativeIncidents,
            MinimumRepresentativeApplicationShapes,
            representativeRows.DistinctIncidents,
            representativeRows.DistinctApplicationShapes,
            representativeRows.TotalQuestions,
            missing.ToImmutable());
    }

    private static NextDecision EvaluateNextDecision(
        ImmutableArray<UsefulnessRow> rows,
        RepresentativeGate gate)
    {
        if (gate.Status != RepresentativeGateStatus.Satisfied)
        {
            return new NextDecision(
                NextDecisionStatus.DeferredRepresentativeGateOpen,
                SelectedDecision: null,
                "No successor is admitted until the representative raw-count baseline satisfies the W5.5 gate.",
                ImmutableArray<BlockerRanking>.Empty);
        }

        var rankings = Enum.GetValues<DominantBlocker>()
            .Where(static blocker => blocker != DominantBlocker.None)
            .Select(blocker =>
            {
                var blockedRows = rows.Where(row => row.DominantBlocker == blocker).ToImmutableArray();
                return new BlockerRanking(
                    blocker,
                    blockedRows.Select(static row => row.IncidentId).Distinct(StringComparer.Ordinal).Count(),
                    blockedRows.Count(static row => row.AnswerUsefulForInvestigation),
                    blockedRows.Count(static row => row.AnswerChangedNextDecision),
                    blockedRows.Count(static row =>
                        row.ContextAttribution == nameof(EvidenceState.Exact) &&
                        row.RequiredMemberEvidence == nameof(EvidenceState.Exact)));
            })
            .OrderByDescending(static item => item.IndependentIncidentCount)
            .ThenByDescending(static item => item.DecisionChangingQuestionCount)
            .ThenByDescending(static item => item.UsefulQuestionCount)
            .ThenByDescending(static item => item.ExactEvidenceQuestionCount)
            .ThenBy(static item => item.Blocker)
            .ToImmutableArray();
        var leader = rankings[0];
        var selected = leader.IndependentIncidentCount == 0
            ? PostW5Decision.StopFeatureExpansion
            : leader.Blocker switch
            {
                DominantBlocker.ContextAcquisition => PostW5Decision.AdmitContextAcquisitionScenario,
                DominantBlocker.MemberNavigation => PostW5Decision.AdmitFixedDepthMemberChain,
                DominantBlocker.ExecutionBody => PostW5Decision.AdmitOneOpcodeOrModelFamily,
                DominantBlocker.Consumption => PostW5Decision.ImproveReferenceHostExplanation,
                DominantBlocker.ProductThesis => PostW5Decision.StopFeatureExpansion,
                _ => throw new InvalidDataException("The blocker ranking returned an unknown category."),
            };
        return new NextDecision(
            NextDecisionStatus.Selected,
            selected,
            "The selection follows independent-incident frequency, decision impact, usefulness, exact-evidence availability, then stable category order.",
            rankings);
    }

    private static void WriteMachineReport(
        string path,
        PortfolioManifest manifest,
        ImmutableArray<EvaluationReport> reports,
        ImmutableArray<UsefulnessRow> rows,
        RawAggregate allRows,
        RawAggregate representativeRows,
        RepresentativeGate gate,
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
        writer.WriteString("generatedValidationCaveat", GeneratedCaveat);
        writer.WriteBoolean("claimsProductionReadiness", false);
        writer.WriteStartArray("evaluationReports");
        foreach (var report in reports)
        {
            writer.WriteStartObject();
            writer.WriteString("id", report.Id);
            writer.WriteString("corpusKind", report.CorpusKind);
            writer.WriteString("corpusCaveat", report.CorpusCaveat);
            writer.WriteString("dumpSnapshotSha256", report.DumpSnapshotSha256);
            writer.WriteNumber("scenarioCount", report.Scenarios.Count);
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
        writer.WritePropertyName("allRows");
        WriteAggregate(writer, allRows);
        writer.WritePropertyName("representativeRows");
        WriteAggregate(writer, representativeRows);
        writer.WriteEndObject();
        writer.WritePropertyName("representativeGate");
        WriteGate(writer, gate);
        writer.WritePropertyName("nextDecision");
        WriteDecision(writer, decision);
        writer.WriteEndObject();
    }

    private static void WriteRow(Utf8JsonWriter writer, UsefulnessRow row)
    {
        writer.WriteStartObject();
        writer.WriteString("incidentId", row.IncidentId);
        writer.WriteString("applicationShape", row.ApplicationShape);
        writer.WriteString("questionId", row.QuestionId);
        writer.WriteString("userTask", row.UserTask);
        writer.WriteString("expressionRequested", row.ExpressionRequested);
        writer.WriteString("requiredContextKind", row.RequiredContextKind);
        writer.WriteString("contextAttribution", row.ContextAttribution);
        writer.WriteString("requiredMemberEvidence", row.RequiredMemberEvidence);
        writer.WriteString("evaluationReportId", row.EvaluationReportId);
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
        writer.WriteBoolean("manualObjectWalkingOperationsKnown", row.ManualObjectWalkingOperationsKnown);
        writer.WriteStartArray("manualObjectWalkingOperations");
        foreach (var operation in row.ManualObjectWalkingOperations)
        {
            writer.WriteStringValue(operation);
        }

        writer.WriteEndArray();
        writer.WriteBoolean("answerUsefulForInvestigation", row.AnswerUsefulForInvestigation);
        writer.WriteBoolean("answerChangedNextDecision", row.AnswerChangedNextDecision);
        WriteNullable(writer, "decisionImpactExplanation", row.DecisionImpactExplanation);
        writer.WriteString("dominantBlocker", row.DominantBlocker.ToString());
        writer.WriteStartArray("diagnosticCodes");
        foreach (var code in row.DiagnosticCodes)
        {
            writer.WriteStringValue(code);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteAggregate(Utf8JsonWriter writer, RawAggregate aggregate)
    {
        writer.WriteStartObject();
        writer.WriteNumber("totalQuestions", aggregate.TotalQuestions);
        writer.WriteNumber("distinctIncidents", aggregate.DistinctIncidents);
        writer.WriteNumber("distinctApplicationShapes", aggregate.DistinctApplicationShapes);
        writer.WriteStartObject("admission");
        WriteRatio(writer, "admitted", aggregate.AdmittedQuestions, aggregate.TotalQuestions);
        writer.WriteNumber("w2", aggregate.W2Questions);
        writer.WriteNumber("w4", aggregate.W4Questions);
        writer.WriteNumber("unsupported", aggregate.UnsupportedAdmissionQuestions);
        writer.WriteEndObject();
        WriteRatio(writer, "exactAnswers", aggregate.ExactAnswers, aggregate.TotalQuestions);
        WriteRatio(
            writer,
            "usefulPartialOrUnknownAnswers",
            aggregate.UsefulPartialOrUnknownAnswers,
            aggregate.PartialOrUnknownAnswers);
        WriteRatio(
            writer,
            "decisionChangingUsefulness",
            aggregate.DecisionChangingAnswers,
            aggregate.TotalQuestions);
        writer.WriteStartObject("outcomeComposition");
        foreach (var item in aggregate.OutcomeComposition)
        {
            writer.WriteNumber(item.Key, item.Value);
        }

        writer.WriteEndObject();
        writer.WriteStartObject("acquisitionFailureComposition");
        foreach (var item in aggregate.AcquisitionFailureComposition)
        {
            writer.WriteNumber(item.Key, item.Value);
        }

        writer.WriteEndObject();
        writer.WriteStartObject("blockerComposition");
        foreach (var item in aggregate.BlockerComposition)
        {
            writer.WriteNumber(item.Key, item.Value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteRatio(Utf8JsonWriter writer, string name, int numerator, int denominator)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("numerator", numerator);
        writer.WriteNumber("denominator", denominator);
        writer.WriteEndObject();
    }

    private static void WriteGate(Utf8JsonWriter writer, RepresentativeGate gate)
    {
        writer.WriteStartObject();
        writer.WriteString("status", gate.Status.ToString());
        writer.WriteNumber("minimumRepresentativeIncidents", gate.MinimumRepresentativeIncidents);
        writer.WriteNumber("minimumRepresentativeApplicationShapes", gate.MinimumRepresentativeApplicationShapes);
        writer.WriteNumber("representativeIncidentCount", gate.RepresentativeIncidentCount);
        writer.WriteNumber("representativeApplicationShapeCount", gate.RepresentativeApplicationShapeCount);
        writer.WriteNumber("representativeQuestionCount", gate.RepresentativeQuestionCount);
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
        writer.WriteString("status", decision.Status.ToString());
        WriteNullable(writer, "selection", decision.SelectedDecision?.ToString());
        writer.WriteString("rationale", decision.Rationale);
        writer.WriteStartArray("blockerRanking");
        foreach (var item in decision.BlockerRanking)
        {
            writer.WriteStartObject();
            writer.WriteString("blocker", item.Blocker.ToString());
            writer.WriteNumber("independentIncidentCount", item.IndependentIncidentCount);
            writer.WriteNumber("usefulQuestionCount", item.UsefulQuestionCount);
            writer.WriteNumber("decisionChangingQuestionCount", item.DecisionChangingQuestionCount);
            writer.WriteNumber("exactEvidenceQuestionCount", item.ExactEvidenceQuestionCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteHumanReport(
        string path,
        PortfolioManifest manifest,
        ImmutableArray<UsefulnessRow> rows,
        RawAggregate allRows,
        RawAggregate representativeRows,
        RepresentativeGate gate,
        NextDecision decision)
    {
        EnsureParentDirectory(path);
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine("W5 usefulness portfolio report v1");
        writer.WriteLine($"Portfolio: {manifest.PortfolioId}; corpus={manifest.CorpusKind}; predeclared={manifest.PredeclaredBeforeEvaluation}");
        writer.WriteLine($"Caveat: {GeneratedCaveat}");
        foreach (var row in rows)
        {
            writer.WriteLine(
                $"{row.IncidentId}/{row.QuestionId}: admission={row.Admission}; outcome={row.ProductOutcome}; " +
                $"mode={row.SemanticMode ?? "none"}; context={row.ContextAttribution}; member={row.RequiredMemberEvidence}; " +
                $"useful={row.AnswerUsefulForInvestigation}; changed-decision={row.AnswerChangedNextDecision}; " +
                $"boundary={row.FirstBoundaryKind}; blocker={row.DominantBlocker}");
        }

        WriteHumanCounts(writer, "all", allRows);
        WriteHumanCounts(writer, "representative", representativeRows);
        writer.WriteLine(
            $"Representative gate: status={gate.Status}; incidents={gate.RepresentativeIncidentCount}/{gate.MinimumRepresentativeIncidents}; " +
            $"application-shapes={gate.RepresentativeApplicationShapeCount}/{gate.MinimumRepresentativeApplicationShapes}; " +
            $"questions={gate.RepresentativeQuestionCount}");
        foreach (var condition in gate.MissingConditions)
        {
            writer.WriteLine($"Gate missing: {condition}");
        }

        writer.WriteLine(
            $"Next decision: status={decision.Status}; selection={decision.SelectedDecision?.ToString() ?? "none"}; " +
            $"rationale={decision.Rationale}");
    }

    private static void WriteHumanCounts(StreamWriter writer, string label, RawAggregate aggregate)
    {
        writer.WriteLine(
            $"Raw counts ({label}): questions={aggregate.TotalQuestions}; incidents={aggregate.DistinctIncidents}; " +
            $"application-shapes={aggregate.DistinctApplicationShapes}; admitted={aggregate.AdmittedQuestions}/{aggregate.TotalQuestions}; " +
            $"exact={aggregate.ExactAnswers}/{aggregate.TotalQuestions}; " +
            $"useful-partial-or-unknown={aggregate.UsefulPartialOrUnknownAnswers}/{aggregate.PartialOrUnknownAnswers}; " +
            $"decision-changing={aggregate.DecisionChangingAnswers}/{aggregate.TotalQuestions}");
        writer.WriteLine(
            $"Admission composition ({label}): W2={aggregate.W2Questions}; W4={aggregate.W4Questions}; " +
            $"unsupported={aggregate.UnsupportedAdmissionQuestions}");
        writer.WriteLine(
            $"Outcome composition ({label}): " +
            string.Join(';', aggregate.OutcomeComposition.Select(static item => $"{item.Key}={item.Value}")));
        writer.WriteLine(
            $"Acquisition failures ({label}): " +
            (aggregate.AcquisitionFailureComposition.Count == 0
                ? "none"
                : string.Join(';', aggregate.AcquisitionFailureComposition.Select(static item => $"{item.Key}={item.Value}"))));
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
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

    private static T ParseEnum<T>(string value, string label)
        where T : struct, Enum => Enum.TryParse<T>(value, ignoreCase: false, out var parsed) &&
            string.Equals(parsed.ToString(), value, StringComparison.Ordinal)
                ? parsed
                : throw new InvalidDataException($"Unknown {label} '{value}'.");

    private static bool IsBoundedIdentity(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-');

    private enum CorpusKind
    {
        GeneratedValidation,
        RepresentativeIncident,
    }

    private enum AdmissionPath
    {
        W2,
        W4,
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
        Unknown,
        Unavailable,
        Unsupported,
        Conflicting,
        Invalid,
    }

    private enum FirstBoundaryKind
    {
        None,
        Syntax,
        ContextAcquisition,
        MemberEvidence,
        MethodAcquisition,
        Execution,
        Consumption,
    }

    private enum DominantBlocker
    {
        None,
        ContextAcquisition,
        MemberNavigation,
        ExecutionBody,
        Consumption,
        ProductThesis,
    }

    private enum RepresentativeGateStatus
    {
        Satisfied,
        OpenMissingRepresentativeCorpus,
        OpenInsufficientRepresentativeBreadth,
    }

    private enum NextDecisionStatus
    {
        Selected,
        DeferredRepresentativeGateOpen,
    }

    private enum PostW5Decision
    {
        AdmitContextAcquisitionScenario,
        AdmitFixedDepthMemberChain,
        AdmitOneOpcodeOrModelFamily,
        ImproveReferenceHostExplanation,
        StopFeatureExpansion,
    }

    private sealed record UsefulnessRow(
        string IncidentId,
        string ApplicationShape,
        string QuestionId,
        string UserTask,
        string ExpressionRequested,
        string RequiredContextKind,
        string ContextAttribution,
        string RequiredMemberEvidence,
        string EvaluationReportId,
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
        bool ManualObjectWalkingOperationsKnown,
        ImmutableArray<string> ManualObjectWalkingOperations,
        bool AnswerUsefulForInvestigation,
        bool AnswerChangedNextDecision,
        string? DecisionImpactExplanation,
        DominantBlocker DominantBlocker,
        ImmutableArray<string> DiagnosticCodes);

    private sealed record RepresentativeGate(
        RepresentativeGateStatus Status,
        int MinimumRepresentativeIncidents,
        int MinimumRepresentativeApplicationShapes,
        int RepresentativeIncidentCount,
        int RepresentativeApplicationShapeCount,
        int RepresentativeQuestionCount,
        ImmutableArray<string> MissingConditions);

    private sealed record NextDecision(
        NextDecisionStatus Status,
        PostW5Decision? SelectedDecision,
        string Rationale,
        ImmutableArray<BlockerRanking> BlockerRanking);

    private sealed record BlockerRanking(
        DominantBlocker Blocker,
        int IndependentIncidentCount,
        int UsefulQuestionCount,
        int DecisionChangingQuestionCount,
        int ExactEvidenceQuestionCount);

    private sealed class RawAggregate
    {
        private RawAggregate(
            int totalQuestions,
            int distinctIncidents,
            int distinctApplicationShapes,
            int admittedQuestions,
            int w2Questions,
            int w4Questions,
            int unsupportedAdmissionQuestions,
            int exactAnswers,
            int partialOrUnknownAnswers,
            int usefulPartialOrUnknownAnswers,
            int decisionChangingAnswers,
            ImmutableSortedDictionary<string, int> outcomeComposition,
            ImmutableSortedDictionary<string, int> acquisitionFailureComposition,
            ImmutableSortedDictionary<string, int> blockerComposition)
        {
            TotalQuestions = totalQuestions;
            DistinctIncidents = distinctIncidents;
            DistinctApplicationShapes = distinctApplicationShapes;
            AdmittedQuestions = admittedQuestions;
            W2Questions = w2Questions;
            W4Questions = w4Questions;
            UnsupportedAdmissionQuestions = unsupportedAdmissionQuestions;
            ExactAnswers = exactAnswers;
            PartialOrUnknownAnswers = partialOrUnknownAnswers;
            UsefulPartialOrUnknownAnswers = usefulPartialOrUnknownAnswers;
            DecisionChangingAnswers = decisionChangingAnswers;
            OutcomeComposition = outcomeComposition;
            AcquisitionFailureComposition = acquisitionFailureComposition;
            BlockerComposition = blockerComposition;
        }

        internal int TotalQuestions { get; }

        internal int DistinctIncidents { get; }

        internal int DistinctApplicationShapes { get; }

        internal int AdmittedQuestions { get; }

        internal int W2Questions { get; }

        internal int W4Questions { get; }

        internal int UnsupportedAdmissionQuestions { get; }

        internal int ExactAnswers { get; }

        internal int PartialOrUnknownAnswers { get; }

        internal int UsefulPartialOrUnknownAnswers { get; }

        internal int DecisionChangingAnswers { get; }

        internal ImmutableSortedDictionary<string, int> OutcomeComposition { get; }

        internal ImmutableSortedDictionary<string, int> AcquisitionFailureComposition { get; }

        internal ImmutableSortedDictionary<string, int> BlockerComposition { get; }

        internal static RawAggregate Create(ImmutableArray<UsefulnessRow> rows) => new(
            rows.Length,
            rows.Select(static row => row.IncidentId).Distinct(StringComparer.Ordinal).Count(),
            rows.Select(static row => row.ApplicationShape).Distinct(StringComparer.Ordinal).Count(),
            rows.Count(static row => row.Admission != AdmissionPath.Unsupported),
            rows.Count(static row => row.Admission == AdmissionPath.W2),
            rows.Count(static row => row.Admission == AdmissionPath.W4),
            rows.Count(static row => row.Admission == AdmissionPath.Unsupported),
            rows.Count(static row => row.ProductOutcome == ProductOutcome.Exact),
            rows.Count(static row => row.ProductOutcome is ProductOutcome.Partial or ProductOutcome.Unknown),
            rows.Count(static row =>
                row.ProductOutcome is ProductOutcome.Partial or ProductOutcome.Unknown &&
                row.AnswerUsefulForInvestigation),
            rows.Count(static row => row.AnswerChangedNextDecision),
            CountBy(rows, static row => row.ProductOutcome.ToString()),
            CountAcquisitionFailures(rows),
            CountBy(rows, static row => row.DominantBlocker.ToString()));

        private static ImmutableSortedDictionary<string, int> CountAcquisitionFailures(
            ImmutableArray<UsefulnessRow> rows)
        {
            var builder = ImmutableSortedDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            foreach (var row in rows.Where(static row => row.EvaluationOutcomeKind == "AcquisitionFailure"))
            {
                foreach (var code in row.DiagnosticCodes)
                {
                    builder[code] = builder.GetValueOrDefault(code) + 1;
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableSortedDictionary<string, int> CountBy(
            ImmutableArray<UsefulnessRow> rows,
            Func<UsefulnessRow, string> selector)
        {
            var builder = ImmutableSortedDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = selector(row);
                builder[key] = builder.GetValueOrDefault(key) + 1;
            }

            return builder.ToImmutable();
        }
    }

    private sealed class EvaluationReport
    {
        private EvaluationReport(
            string id,
            string corpusKind,
            string corpusCaveat,
            string dumpSnapshotSha256,
            ImmutableDictionary<string, EvaluationScenario> scenarios)
        {
            Id = id;
            CorpusKind = corpusKind;
            CorpusCaveat = corpusCaveat;
            DumpSnapshotSha256 = dumpSnapshotSha256;
            Scenarios = scenarios;
        }

        internal string Id { get; }

        internal string CorpusKind { get; }

        internal string CorpusCaveat { get; }

        internal string DumpSnapshotSha256 { get; }

        internal ImmutableDictionary<string, EvaluationScenario> Scenarios { get; }

        internal static EvaluationReport Load(string id, string path)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"Evaluation report '{id}' does not exist.");
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.GetProperty("machineSchemaVersion").GetInt32() != EvaluationReportSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Evaluation report '{id}' must use machine schema {EvaluationReportSchemaVersion}.");
            }

            var snapshot = RequiredString(root, "dumpSnapshotSha256");
            if (snapshot.Length != 64 || !snapshot.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException($"Evaluation report '{id}' has an invalid snapshot identity.");
            }

            var corpusKind = RequiredString(root, "corpusKind");
            _ = ParseEnum<CorpusKind>(corpusKind, "evaluation-report corpus kind");
            var corpusCaveat = RequiredString(root, "fixtureCaveat");

            var scenarios = ImmutableDictionary.CreateBuilder<string, EvaluationScenario>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("scenarios").EnumerateArray())
            {
                var scenario = EvaluationScenario.FromJson(element);
                if (!scenarios.TryAdd(scenario.Id, scenario))
                {
                    throw new InvalidDataException(
                        $"Evaluation report '{id}' duplicates scenario '{scenario.Id}'.");
                }
            }

            if (scenarios.Count is < 1 or > 64)
            {
                throw new InvalidDataException($"Evaluation report '{id}' has an invalid scenario count.");
            }

            return new EvaluationReport(id, corpusKind, corpusCaveat, snapshot, scenarios.ToImmutable());
        }
    }

    private sealed record EvaluationScenario(
        string Id,
        string? Expression,
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

        [JsonPropertyName("evaluationReports")]
        public ImmutableArray<EvaluationReportDefinition> EvaluationReports { get; init; }

        [JsonPropertyName("questions")]
        public ImmutableArray<QuestionDefinition> Questions { get; init; }

        internal void Validate()
        {
            if (SchemaVersion != PortfolioSchemaVersion)
            {
                throw new InvalidDataException($"Portfolio schema version must be {PortfolioSchemaVersion}.");
            }

            if (!IsBoundedIdentity(PortfolioId) || string.IsNullOrWhiteSpace(DeclaredPurpose) ||
                DeclaredPurpose.Length > 4_096)
            {
                throw new InvalidDataException("The portfolio identity or declared purpose is invalid.");
            }

            _ = ParseEnum<CorpusKind>(CorpusKind, "corpus kind");
            if (CorpusKind == nameof(UsefulnessPortfolioRunner.CorpusKind.RepresentativeIncident) &&
                !PredeclaredBeforeEvaluation)
            {
                throw new InvalidDataException("Representative incident portfolios must be predeclared.");
            }

            if (EvaluationReports.IsDefaultOrEmpty || EvaluationReports.Length > MaximumEvaluationReports ||
                EvaluationReports.Any(static report => report is null))
            {
                throw new InvalidDataException(
                    $"A portfolio requires 1 to {MaximumEvaluationReports} evaluation reports.");
            }

            if (Questions.IsDefaultOrEmpty || Questions.Length > MaximumQuestions ||
                Questions.Any(static question => question is null))
            {
                throw new InvalidDataException($"A portfolio requires 1 to {MaximumQuestions} questions.");
            }

            var reportIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var report in EvaluationReports)
            {
                report.Validate();
                if (!reportIds.Add(report.Id))
                {
                    throw new InvalidDataException($"Evaluation report id '{report.Id}' is duplicated.");
                }
            }

            var questionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var question in Questions)
            {
                question.Validate();
                if (!questionIds.Add(question.QuestionId))
                {
                    throw new InvalidDataException($"Question id '{question.QuestionId}' is duplicated.");
                }
            }
        }
    }

    private sealed class EvaluationReportDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        internal void Validate()
        {
            if (!IsBoundedIdentity(Id) || string.IsNullOrWhiteSpace(Path) || Path.Length > 4_096)
            {
                throw new InvalidDataException("An evaluation-report reference is invalid.");
            }
        }
    }

    private sealed class QuestionDefinition
    {
        [JsonPropertyName("incidentId")]
        public string IncidentId { get; init; } = string.Empty;

        [JsonPropertyName("applicationShape")]
        public string ApplicationShape { get; init; } = string.Empty;

        [JsonPropertyName("questionId")]
        public string QuestionId { get; init; } = string.Empty;

        [JsonPropertyName("userTask")]
        public string UserTask { get; init; } = string.Empty;

        [JsonPropertyName("expressionRequested")]
        public string ExpressionRequested { get; init; } = string.Empty;

        [JsonPropertyName("requiredContextKind")]
        public string RequiredContextKind { get; init; } = string.Empty;

        [JsonPropertyName("contextAttribution")]
        public string ContextAttribution { get; init; } = string.Empty;

        [JsonPropertyName("requiredMemberEvidence")]
        public string RequiredMemberEvidence { get; init; } = string.Empty;

        [JsonPropertyName("evaluationReportId")]
        public string EvaluationReportId { get; init; } = string.Empty;

        [JsonPropertyName("scenarioId")]
        public string ScenarioId { get; init; } = string.Empty;

        [JsonPropertyName("firstBoundary")]
        public BoundaryDefinition FirstBoundary { get; init; } = null!;

        [JsonPropertyName("manualObjectWalkingOperationsKnown")]
        public bool ManualObjectWalkingOperationsKnown { get; init; }

        [JsonPropertyName("manualObjectWalkingOperations")]
        public ImmutableArray<string> ManualObjectWalkingOperations { get; init; }

        [JsonPropertyName("answerUsefulForInvestigation")]
        public bool AnswerUsefulForInvestigation { get; init; }

        [JsonPropertyName("answerChangedNextDecision")]
        public bool AnswerChangedNextDecision { get; init; }

        [JsonPropertyName("decisionImpactExplanation")]
        public string? DecisionImpactExplanation { get; init; }

        [JsonPropertyName("dominantBlocker")]
        public string DominantBlocker { get; init; } = string.Empty;

        internal void Validate()
        {
            if (!IsBoundedIdentity(IncidentId) || !IsBoundedIdentity(ApplicationShape) ||
                !IsBoundedIdentity(QuestionId) || !IsBoundedIdentity(EvaluationReportId) ||
                !IsBoundedIdentity(ScenarioId))
            {
                throw new InvalidDataException("A question contains invalid bounded identity text.");
            }

            if (string.IsNullOrWhiteSpace(UserTask) || UserTask.Length > 4_096 ||
                string.IsNullOrWhiteSpace(ExpressionRequested) || ExpressionRequested.Length > 4_096 ||
                string.IsNullOrWhiteSpace(RequiredContextKind) || RequiredContextKind.Length > 1_024)
            {
                throw new InvalidDataException($"Question '{QuestionId}' has missing or overlong task text.");
            }

            _ = ParseEnum<EvidenceState>(ContextAttribution, "context-attribution state");
            _ = ParseEnum<EvidenceState>(RequiredMemberEvidence, "member-evidence state");
            _ = ParseEnum<DominantBlocker>(DominantBlocker, "dominant blocker");
            if (FirstBoundary is null)
            {
                throw new InvalidDataException($"Question '{QuestionId}' requires a first-boundary record.");
            }

            FirstBoundary.Validate();
            if (ManualObjectWalkingOperations.IsDefault || ManualObjectWalkingOperations.Length > 64 ||
                ManualObjectWalkingOperations.Any(static operation =>
                    string.IsNullOrWhiteSpace(operation) || operation.Length > 1_024))
            {
                throw new InvalidDataException($"Question '{QuestionId}' has invalid manual-operation evidence.");
            }

            if (DecisionImpactExplanation?.Length > 2_048)
            {
                throw new InvalidDataException($"Question '{QuestionId}' has an overlong decision explanation.");
            }
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
            var kind = ParseEnum<FirstBoundaryKind>(Kind, "first-boundary kind");
            if (Code?.Length > 256 || Explanation?.Length > 2_048 ||
                kind == FirstBoundaryKind.None && (Code is not null || Explanation is not null))
            {
                throw new InvalidDataException("A first-boundary record is invalid.");
            }
        }
    }

    private sealed record PortfolioCommandLineOptions(
        string ManifestPath,
        string MachineOutputPath,
        string HumanOutputPath,
        string? ReportRoot)
    {
        internal static PortfolioCommandLineOptions Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (args.Length == 0 || args.Length % 2 != 0)
            {
                throw new PortfolioCommandLineException("Expected named option/value pairs.");
            }

            for (var index = 0; index < args.Length; index += 2)
            {
                if (args[index] is not (
                        "--portfolio-manifest" or "--machine-output" or "--human-output" or "--report-root") ||
                    string.IsNullOrWhiteSpace(args[index + 1]) ||
                    !values.TryAdd(args[index], args[index + 1]))
                {
                    throw new PortfolioCommandLineException(
                        "An option is unknown, duplicated, or has an empty value.");
                }
            }

            return new PortfolioCommandLineOptions(
                Required("--portfolio-manifest"),
                Required("--machine-output"),
                Required("--human-output"),
                values.GetValueOrDefault("--report-root"));

            string Required(string name) => values.TryGetValue(name, out var value)
                ? value
                : throw new PortfolioCommandLineException($"Required option '{name}' is missing.");
        }
    }

    private sealed class PortfolioCommandLineException(string message) : ArgumentException(message);
}
