using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Headless.ReferenceConsumer;

internal static class W7StaticFieldPortfolioRunner
{
    private const int ReportSchemaVersion = 1;
    private const string PortfolioId = "interpreter-w7-static-field-incidents-v1";
    private const string EvidenceCaveat =
        "Designed synthetic incidents validate prototype behavior and design decisions only; they are not external observations or field-readiness evidence.";

    internal static bool IsRequested(string[] args) =>
        args.Contains("--w7-portfolio-manifest", StringComparer.Ordinal);

    internal static int Run(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            using var manifest = JsonDocument.Parse(File.ReadAllBytes(options.ManifestPath));
            var rows = EvaluatePortfolio(manifest.RootElement, options);
            var aggregate = Aggregate.Create(rows);
            var decision = SelectDecision(aggregate.Ranking);
            WriteMachineReport(options.MachineOutputPath, manifest.RootElement, rows, aggregate, decision);
            WriteHumanReport(options.HumanOutputPath, rows, aggregate, decision);
            Console.WriteLine($"W7_PORTFOLIO_OK:{rows.Length}:{decision.Status}");
            return 0;
        }
        catch (W7PortfolioArgumentException exception)
        {
            Console.Error.WriteLine($"W7_PORTFOLIO_ARGUMENT_INVALID:{exception.Message}");
            return 2;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"W7_PORTFOLIO_INPUT_INVALID:{exception.Message}");
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"W7_PORTFOLIO_OUTPUT_FAILED:{exception.GetType().Name}");
            return 5;
        }
    }

    private static ImmutableArray<Row> EvaluatePortfolio(JsonElement root, Options options)
    {
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            !string.Equals(RequiredString(root, "corpusId"), PortfolioId, StringComparison.Ordinal) ||
            !string.Equals(RequiredString(root, "evidenceKind"), "designed-synthetic", StringComparison.Ordinal) ||
            !string.Equals(RequiredString(root, "languageProfile"), "StaticFieldExpressionV1", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The W7 runner accepts only the predeclared designed-synthetic StaticFieldExpressionV1 portfolio.");
        }

        var incidents = root.GetProperty("incidents").EnumerateArray().ToArray();
        if (incidents.Length != 16 ||
            incidents.Select(static incident => RequiredString(incident, "id")).Distinct(StringComparer.Ordinal).Count() != 16)
        {
            throw new InvalidDataException("The W7 portfolio must contain sixteen distinct incidents.");
        }

        var repositoryRoot = options.RepositoryRoot;
        var portablePdbPath = ResolveRepositoryPath(
            repositoryRoot,
            RequiredString(root.GetProperty("targetContract"), "portablePdbArtifact"));
        var conflictPdbPath = ResolveCompanionArtifact(
            repositoryRoot,
            root.GetProperty("companionArtifactContracts"),
            "portable-pdb-identity-conflict");

        var rows = ImmutableArray.CreateBuilder<Row>(incidents.Length);
        foreach (var incident in incidents.OrderBy(static item => item.GetProperty("ordinal").GetInt32()))
        {
            rows.Add(EvaluateIncident(incident, options.DumpRoot, portablePdbPath, conflictPdbPath));
        }

        var result = rows.MoveToImmutable();
        if (result.Select(static row => row.SnapshotSha256).Distinct(StringComparer.Ordinal).Count() != result.Length ||
            result.Select(static row => row.Shape).Distinct(StringComparer.Ordinal).Count() != 4)
        {
            throw new InvalidDataException(
                "Every incident must use a distinct snapshot and the portfolio must retain four application shapes.");
        }

        return result;
    }

    private static Row EvaluateIncident(
        JsonElement incident,
        string dumpRoot,
        string portablePdbPath,
        string conflictPdbPath)
    {
        var id = RequiredString(incident, "id");
        var dumpPath = Path.Combine(dumpRoot, $"{id}.dmp");
        var opened = ClrmdDumpSession.Open(dumpPath);
        if (opened.Status != ClrmdEvidenceStatus.Exact || opened.Value is null)
        {
            throw new InvalidDataException($"Incident '{id}' dump acquisition stopped as {opened.Status}/{opened.Issue}.");
        }

        using var session = opened.Value;
        var expression = RequiredString(incident, "expression");
        var capturedCall = EvaluateExpression(
            session,
            expression,
            incident,
            portablePdbPath,
            conflictPdbPath);
        var captured = Project(capturedCall.Result);
        var evidenceView = RequiredString(incident, "evidenceView");
        var actual = ApplyEvidenceView(evidenceView, capturedCall.Result, captured);
        var expected = Projection.FromJson(incident.GetProperty("expected"), OptionalString(incident, "expectedTerminal"));
        RequireEqual(id, "primary", expected, actual);

        Control? control = null;
        if (incident.GetProperty("controlExpression").ValueKind == JsonValueKind.String)
        {
            var controlExpression = RequiredString(incident, "controlExpression");
            var poison = ArtifactResolver.Poison();
            var selector = DumpSelectedFrameSelector.Create(
                session.Snapshot,
                threadOrdinal: int.MaxValue,
                frameOrdinal: int.MaxValue);
            var controlResult = StaticFieldExpressionEvaluator.Evaluate(
                session,
                controlExpression,
                selector,
                poison);
            if (poison.CallCount != 0)
            {
                throw new InvalidDataException(
                    $"Incident '{id}' fully qualified control consulted poisoned frame/PDB capabilities.");
            }

            var controlActual = Project(controlResult);
            var controlExpected = Projection.FromJson(
                incident.GetProperty("controlExpected"),
                OptionalString(incident, "controlExpectedTerminal"));
            RequireEqual(id, "control", controlExpected, controlActual);
            control = new Control(
                controlExpression,
                RequiredString(incident, "controlRelationship"),
                controlResult.Sha256,
                poison.CallCount,
                controlActual);
        }

        Comparison? comparison = null;
        if (incident.TryGetProperty("comparisonProjection", out var comparisonElement))
        {
            var comparisonExpression = RequiredString(comparisonElement, "expression");
            var comparisonResult = StaticFieldExpressionEvaluator.Evaluate(session, comparisonExpression);
            var comparisonCaptured = Project(comparisonResult);
            var comparisonActual = ApplyEvidenceView(
                RequiredString(comparisonElement, "evidenceView"),
                comparisonResult,
                comparisonCaptured);
            var comparisonExpected = Projection.FromJson(
                comparisonElement.GetProperty("expected"),
                terminal: null);
            RequireEqual(id, "comparison", comparisonExpected, comparisonActual);
            comparison = new Comparison(
                comparisonExpression,
                RequiredString(comparisonElement, "evidenceView"),
                RequiredString(comparisonElement, "relationship"),
                RequiredString(comparisonElement, "firstBoundary"),
                comparisonResult.Sha256,
                comparisonCaptured,
                comparisonActual);
        }

        return new Row(
            id,
            incident.GetProperty("ordinal").GetInt32(),
            RequiredString(incident, "shape"),
            session.Snapshot.Sha256,
            expression,
            evidenceView,
            RequiredString(incident, "suffixProfile"),
            RequiredString(incident, "firstBoundary"),
            ParseBoundary(RequiredString(incident, "postW7Boundary")),
            RequiredString(incident, "usefulness"),
            RequiredString(incident, "decisionImpact"),
            capturedCall.Result.Sha256,
            capturedCall.ResolverCallCount,
            captured,
            actual,
            control,
            comparison);
    }

    private static EvaluatorCall EvaluateExpression(
        ClrmdDumpSession session,
        string expression,
        JsonElement incident,
        string portablePdbPath,
        string conflictPdbPath)
    {
        var request = incident.GetProperty("contextRequest");
        var mode = RequiredString(request, "mode");
        if (mode == "none")
        {
            return new EvaluatorCall(
                StaticFieldExpressionEvaluator.Evaluate(session, expression),
                ResolverCallCount: 0);
        }

        var selector = mode switch
        {
            "missing-managed-thread" or "poison-if-called" => DumpSelectedFrameSelector.Create(
                session.Snapshot,
                threadOrdinal: int.MaxValue,
                frameOrdinal: int.MaxValue),
            "managed-thread-and-frame" => FindFrame(session, RequiredString(incident, "shape")),
            _ => throw new InvalidDataException($"Unknown W7 context mode '{mode}'."),
        };
        var pdbMode = RequiredString(request, "pdbMode");
        var resolver = pdbMode switch
        {
            "exact" or "two-competing-imports" => ArtifactResolver.Exact(portablePdbPath),
            "partial-bytes" => ArtifactResolver.Partial(portablePdbPath),
            "identity-conflict" => ArtifactResolver.Exact(conflictPdbPath),
            "not-reached" or "poison-if-called" => ArtifactResolver.Poison(),
            _ => throw new InvalidDataException($"Unknown W7 Portable-PDB mode '{pdbMode}'."),
        };
        var result = StaticFieldExpressionEvaluator.Evaluate(session, expression, selector, resolver);
        return new EvaluatorCall(result, resolver.CallCount);
    }

    private static DumpSelectedFrameSelector FindFrame(ClrmdDumpSession session, string shape)
    {
        var expectedNamespace = shape switch
        {
            "Request" => "PhoenixInspect.W7TestTarget.Request",
            "Batch" => "PhoenixInspect.W7TestTarget.BatchContext",
            "Coordinator" => "PhoenixInspect.W7TestTarget.CoordinatorContext",
            "Workflow" => "PhoenixInspect.W7TestTarget.Workflow",
            _ => throw new InvalidDataException($"Unknown W7 application shape '{shape}'."),
        };

        for (var threadOrdinal = 0; threadOrdinal < 64; threadOrdinal++)
        {
            for (var frameOrdinal = 0; frameOrdinal < 32; frameOrdinal++)
            {
                var selector = DumpSelectedFrameSelector.Create(session.Snapshot, threadOrdinal, frameOrdinal);
                var selected = session.SelectExpressionFrame(selector);
                if (selected.Status == DumpContextEvidenceStatus.Exact &&
                    selected.Frame is { } frame &&
                    string.Equals(frame.DeclaringNamespace, expectedNamespace, StringComparison.Ordinal))
                {
                    return selector;
                }

                if (frameOrdinal > 0 &&
                    selected.Status == DumpContextEvidenceStatus.Unavailable &&
                    selected.Issue == DumpContextEvidenceIssue.FrameUnavailable)
                {
                    break;
                }
            }
        }

        throw new InvalidDataException(
            $"No exact selected frame was found for W7 application shape '{shape}'.");
    }

    private static Projection Project(StaticFieldExpressionEvaluationResult result)
    {
        var syntax = result.Syntax.Status switch
        {
            StaticFieldSyntaxStatus.Accepted => "Exact",
            StaticFieldSyntaxStatus.Invalid => "Invalid",
            StaticFieldSyntaxStatus.Unsupported => "Unsupported",
            _ => throw new InvalidDataException("The evaluator returned an unknown syntax status."),
        };
        if (syntax != "Exact")
        {
            return new Projection(syntax, "NotReached", "NotReached", "NotReached", "NotReached", null);
        }

        var binding = result.SymbolBinding
            ?? throw new InvalidDataException("Accepted syntax did not retain a symbol-binding outcome.");
        var context = ProjectContext(binding);
        if (context is not ("NotRequired" or "Exact" or "ExactNamespaceImport" or "ExactTypeAlias" or "ExactCurrentNamespace"))
        {
            return new Projection("Exact", context, "NotReached", "NotReached", "NotReached", null);
        }

        var symbol = binding.Status switch
        {
            StaticFieldBindingStatus.Exact => "Exact",
            StaticFieldBindingStatus.Absent => "Unavailable",
            _ => binding.Status.ToString(),
        };
        if (symbol != "Exact")
        {
            return new Projection("Exact", context, symbol, "NotReached", "NotReached", null);
        }

        if (result.RuntimeDeclaration is null)
        {
            return new Projection(
                "Exact",
                context,
                "Exact",
                ProjectEvaluationStatus(result.Status),
                "NotReached",
                null);
        }

        var storage = result.HostObservation?.Status switch
        {
            ClrmdStaticFieldObservationStatus.Exact => "Exact",
            { } status => status.ToString(),
            null => ProjectEvaluationStatus(result.Status),
        };
        if (storage != "Exact")
        {
            return new Projection("Exact", context, "Exact", storage, "NotReached", null);
        }

        if (result.Stage != StaticFieldExpressionEvaluationStage.Complete ||
            result.Status != StaticFieldExpressionEvaluationStatus.Exact)
        {
            return new Projection(
                "Exact",
                context,
                "Exact",
                "Exact",
                ProjectEvaluationStatus(result.Status),
                null);
        }

        var terminal = ProjectTerminal(result);
        return new Projection("Exact", context, "Exact", "Exact", terminal.Kind, terminal.Value);
    }

    private static string ProjectContext(StaticFieldSymbolBindingOutcome binding)
    {
        if (binding.Descriptor.HasGlobalQualifier ||
            !binding.ConsultedContext.CurrentNamespaceConsulted && !binding.ConsultedContext.ImportsConsulted)
        {
            return "NotRequired";
        }

        var context = binding.ConsultedContext;
        if (context.ConsultedFrameEvidence is { Status: not DumpContextEvidenceStatus.Exact } frame)
        {
            return frame.Status.ToString();
        }
        if (context.ImportsConsulted && context.ImportEvidenceStatus is not DumpContextEvidenceStatus.Exact)
        {
            return context.ImportEvidenceStatus!.Value.ToString();
        }
        if (binding.Status != StaticFieldBindingStatus.Exact)
        {
            return "Exact";
        }

        var origins = binding.Candidates.SelectMany(static candidate => candidate.Origins).ToArray();
        if (origins.Any(static origin => origin.Kind == StaticFieldNameExpansionKind.TypeAlias))
        {
            return "ExactTypeAlias";
        }
        if (origins.Any(static origin => origin.Kind == StaticFieldNameExpansionKind.NamespaceImport))
        {
            return "ExactNamespaceImport";
        }
        if (origins.Any(static origin => origin.Kind == StaticFieldNameExpansionKind.CurrentNamespace))
        {
            return "ExactCurrentNamespace";
        }
        return "Exact";
    }

    private static (string Kind, string Value) ProjectTerminal(StaticFieldExpressionEvaluationResult result)
    {
        if (result.SuffixResult?.Value is { } suffix)
        {
            return suffix.Kind switch
            {
                DumpQueryValueKind.Null => ("ExactNull", "null"),
                DumpQueryValueKind.Int32 => ("ExactInt32", $"i32:{suffix.Int32Value!.Value}"),
                DumpQueryValueKind.String => ("ExactString", $"string:{suffix.StringValue}"),
                _ => throw new InvalidDataException("The suffix engine returned an unknown value kind."),
            };
        }

        var value = result.HostObservation?.Value
            ?? throw new InvalidDataException("A complete static result did not retain its exact terminal value.");
        return value.Kind switch
        {
            ClrmdStaticFieldTerminalKind.Null => ("ExactNull", "null"),
            ClrmdStaticFieldTerminalKind.Int32 => ("ExactInt32", $"i32:{value.Int32Value!.Value}"),
            ClrmdStaticFieldTerminalKind.NullableInt32NoValue =>
                ("ExactNullableNoValue", "nullable-int32:none"),
            ClrmdStaticFieldTerminalKind.NullableInt32Value =>
                ("ExactInt32", $"i32:{value.Int32Value!.Value}"),
            ClrmdStaticFieldTerminalKind.String => ("ExactString", $"string:{value.StringValue!.Value}"),
            ClrmdStaticFieldTerminalKind.ObjectReference =>
                ("ExactObject", $"object:{value.ObjectReference!.HeaderRuntimeType.FullName}"),
            _ => throw new InvalidDataException("The static decoder returned an unknown terminal kind."),
        };
    }

    private static Projection ApplyEvidenceView(
        string view,
        StaticFieldExpressionEvaluationResult result,
        Projection captured)
    {
        if (view == "exact")
        {
            return captured;
        }
        if (result.Stage != StaticFieldExpressionEvaluationStage.Complete ||
            result.Status != StaticFieldExpressionEvaluationStatus.Exact)
        {
            throw new InvalidDataException(
                $"Evidence view '{view}' may mask only a captured exact production result.");
        }

        return view switch
        {
            "truncate-static-slot" => captured with { Value = "Partial", Terminal = null },
            "replace-method-table" => captured with { Value = "Conflict", Terminal = null },
            "invalidate-field-signature" => captured with
            {
                Symbol = "Invalid",
                Storage = "NotReached",
                Value = "NotReached",
                Terminal = null,
            },
            _ => throw new InvalidDataException($"Unknown W7 evidence view '{view}'."),
        };
    }

    private static void RequireEqual(string id, string projectionName, Projection expected, Projection actual)
    {
        if (expected != actual)
        {
            throw new InvalidDataException(
                $"Incident '{id}' {projectionName} outcome disagreed: expected {expected}, actual {actual}.");
        }
    }

    private static string ProjectEvaluationStatus(StaticFieldExpressionEvaluationStatus status) => status switch
    {
        StaticFieldExpressionEvaluationStatus.Absent => "Unavailable",
        _ => status.ToString(),
    };

    private static Boundary ParseBoundary(string value) => value switch
    {
        "None" => Boundary.None,
        "BindingContextPrecision" => Boundary.BindingContextPrecision,
        "NestedReferenceSource" => Boundary.NestedReferenceSource,
        "TargetIdentity" => Boundary.TargetIdentity,
        "RepeatedZeroArgumentMethod" => Boundary.RepeatedZeroArgumentMethod,
        "ResultExplanation" => Boundary.ResultExplanation,
        _ => throw new InvalidDataException($"Unknown post-W7 boundary '{value}'."),
    };

    private static Decision SelectDecision(ImmutableArray<BoundaryRank> ranking)
    {
        var qualified = ranking.Where(static item =>
            item.IndependentIncidentCount >= 3 &&
            item.ApplicationShapeCount >= 2 &&
            item.DecisionChangingQuestionCount >= 2).ToArray();
        if (qualified.Length == 0)
        {
            return new Decision("DeferredNoThresholdQualifiedBoundary", null);
        }

        var leader = qualified[0];
        if (qualified.Skip(1).Any(item => item.SubstantivelyEquals(leader)))
        {
            return new Decision("DeferredNoUniqueQualifiedBoundary", null);
        }

        var selection = leader.Boundary switch
        {
            Boundary.BindingContextPrecision => "AddOneEvidenceBackedFramePdbImportAliasGenericRule",
            Boundary.NestedReferenceSource => "PlanOneAttributableAlternateReferenceSource",
            Boundary.TargetIdentity => "ImproveOneConcreteTargetCorrelationSource",
            Boundary.RepeatedZeroArgumentMethod => "AdmitOneCompleteRepeatedZeroArgumentMethodClosure",
            Boundary.ResultExplanation => "ImproveTheHeadlessResultExplanation",
            _ => throw new InvalidDataException("A non-action boundary cannot select the next action."),
        };
        return new Decision("SelectedSyntheticDesignDecision", selection);
    }

    private static void WriteMachineReport(
        string path,
        JsonElement manifest,
        ImmutableArray<Row> rows,
        Aggregate aggregate,
        Decision decision)
    {
        EnsureParentDirectory(path);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("reportSchemaVersion", ReportSchemaVersion);
        writer.WriteString("portfolioId", PortfolioId);
        writer.WriteString("evidenceKind", RequiredString(manifest, "evidenceKind"));
        writer.WriteString("languageProfile", RequiredString(manifest, "languageProfile"));
        writer.WriteBoolean("predeclaredBeforeEvaluation", true);
        writer.WriteBoolean("claimsProductionReadiness", false);
        writer.WriteString("evidenceScopeCaveat", EvidenceCaveat);
        writer.WriteStartArray("rows");
        foreach (var row in rows)
        {
            WriteRow(writer, row);
        }
        writer.WriteEndArray();
        writer.WriteStartObject("rawCounts");
        writer.WriteNumber("totalQuestions", aggregate.TotalQuestions);
        writer.WriteNumber("distinctIncidents", aggregate.DistinctIncidents);
        writer.WriteNumber("distinctApplicationShapes", aggregate.DistinctApplicationShapes);
        writer.WriteNumber("distinctSnapshots", aggregate.DistinctSnapshots);
        writer.WriteNumber("exactAnswers", aggregate.ExactAnswers);
        writer.WriteNumber("usefulAnswers", aggregate.UsefulAnswers);
        writer.WriteNumber("decisionChangingAnswers", aggregate.DecisionChangingAnswers);
        writer.WriteStartObject("representativeRows");
        writer.WriteNumber("totalQuestions", 0);
        writer.WriteNumber("distinctIncidents", 0);
        writer.WriteNumber("distinctApplicationShapes", 0);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteStartArray("boundaryRanking");
        foreach (var rank in aggregate.Ranking)
        {
            writer.WriteStartObject();
            writer.WriteString("boundary", rank.Boundary.ToString());
            writer.WriteNumber("independentIncidentCount", rank.IndependentIncidentCount);
            writer.WriteNumber("applicationShapeCount", rank.ApplicationShapeCount);
            writer.WriteNumber("decisionChangingQuestionCount", rank.DecisionChangingQuestionCount);
            writer.WriteNumber("usefulQuestionCount", rank.UsefulQuestionCount);
            writer.WriteNumber("exactAttributableEvidenceCount", rank.ExactAttributableEvidenceCount);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartObject("nextDecision");
        writer.WriteString("status", decision.Status);
        if (decision.Selection is null)
        {
            writer.WriteNull("selection");
        }
        else
        {
            writer.WriteString("selection", decision.Selection);
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteRow(Utf8JsonWriter writer, Row row)
    {
        writer.WriteStartObject();
        writer.WriteString("id", row.Id);
        writer.WriteNumber("ordinal", row.Ordinal);
        writer.WriteString("shape", row.Shape);
        writer.WriteString("snapshotSha256", row.SnapshotSha256);
        writer.WriteString("expression", row.Expression);
        writer.WriteString("evidenceView", row.EvidenceView);
        writer.WriteString("suffixProfile", row.SuffixProfile);
        writer.WriteString("firstBoundary", row.FirstBoundary);
        writer.WriteString("postW7Boundary", row.PostW7Boundary.ToString());
        writer.WriteString("usefulness", row.Usefulness);
        writer.WriteString("decisionImpact", row.DecisionImpact);
        writer.WriteString("capturedEvaluationSha256", row.CapturedEvaluationSha256);
        writer.WriteNumber("contextResolverCallCount", row.ContextResolverCallCount);
        writer.WritePropertyName("captured");
        WriteProjection(writer, row.Captured);
        writer.WritePropertyName("actual");
        WriteProjection(writer, row.Actual);
        writer.WriteBoolean("matchesPredeclaredOutcome", true);
        if (row.Control is null)
        {
            writer.WriteNull("control");
        }
        else
        {
            writer.WriteStartObject("control");
            writer.WriteString("expression", row.Control.Expression);
            writer.WriteString("relationship", row.Control.Relationship);
            writer.WriteString("evaluationSha256", row.Control.EvaluationSha256);
            writer.WriteNumber("poisonResolverCallCount", row.Control.PoisonResolverCallCount);
            writer.WritePropertyName("actual");
            WriteProjection(writer, row.Control.Actual);
            writer.WriteEndObject();
        }
        if (row.Comparison is null)
        {
            writer.WriteNull("comparison");
        }
        else
        {
            writer.WriteStartObject("comparison");
            writer.WriteString("expression", row.Comparison.Expression);
            writer.WriteString("evidenceView", row.Comparison.EvidenceView);
            writer.WriteString("relationship", row.Comparison.Relationship);
            writer.WriteString("firstBoundary", row.Comparison.FirstBoundary);
            writer.WriteString("capturedEvaluationSha256", row.Comparison.CapturedEvaluationSha256);
            writer.WritePropertyName("captured");
            WriteProjection(writer, row.Comparison.Captured);
            writer.WritePropertyName("actual");
            WriteProjection(writer, row.Comparison.Actual);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteProjection(Utf8JsonWriter writer, Projection projection)
    {
        writer.WriteStartObject();
        writer.WriteString("syntax", projection.Syntax);
        writer.WriteString("context", projection.Context);
        writer.WriteString("symbol", projection.Symbol);
        writer.WriteString("storage", projection.Storage);
        writer.WriteString("value", projection.Value);
        if (projection.Terminal is null)
        {
            writer.WriteNull("terminal");
        }
        else
        {
            writer.WriteString("terminal", projection.Terminal);
        }
        writer.WriteEndObject();
    }

    private static void WriteHumanReport(
        string path,
        ImmutableArray<Row> rows,
        Aggregate aggregate,
        Decision decision)
    {
        var text = new StringBuilder();
        text.AppendLine("W7 static-field meaningful-synthetic portfolio report v1");
        text.AppendLine(EvidenceCaveat);
        text.AppendLine(
            $"questions={aggregate.TotalQuestions}; incidents={aggregate.DistinctIncidents}; " +
            $"application-shapes={aggregate.DistinctApplicationShapes}; snapshots={aggregate.DistinctSnapshots}");
        text.AppendLine(
            $"exact={aggregate.ExactAnswers}; useful={aggregate.UsefulAnswers}; " +
            $"decision-changing={aggregate.DecisionChangingAnswers}");
        text.AppendLine("representative-questions=0; representative-incidents=0; representative-shapes=0");
        foreach (var row in rows)
        {
            text.AppendLine(
                $"{row.Ordinal:D2} {row.Id}: syntax={row.Actual.Syntax}; context={row.Actual.Context}; " +
                $"symbol={row.Actual.Symbol}; storage={row.Actual.Storage}; value={row.Actual.Value}; " +
                $"first-boundary={row.FirstBoundary}; post-W7={row.PostW7Boundary}");
        }
        foreach (var rank in aggregate.Ranking)
        {
            text.AppendLine(
                $"rank {rank.Boundary}: incidents={rank.IndependentIncidentCount}; " +
                $"shapes={rank.ApplicationShapeCount}; decision-changing={rank.DecisionChangingQuestionCount}; " +
                $"useful={rank.UsefulQuestionCount}; exact-attributable={rank.ExactAttributableEvidenceCount}");
        }
        text.AppendLine($"decision-status={decision.Status}; selection={decision.Selection ?? "none"}");
        EnsureParentDirectory(path);
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string ResolveCompanionArtifact(
        string repositoryRoot,
        JsonElement companions,
        string id)
    {
        var companion = companions.EnumerateArray().Single(item =>
            string.Equals(RequiredString(item, "id"), id, StringComparison.Ordinal));
        return ResolveRepositoryPath(repositoryRoot, RequiredString(companion, "artifactPath"));
    }

    private static string ResolveRepositoryPath(string repositoryRoot, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"A required W7 artifact was absent: '{relativePath}'.");
        }
        return path;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The required W7 '{name}' string was absent.")
            : value;
    }

    private static string? OptionalString(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null
            ? null
            : RequiredString(element, name);
    }

    private static void EnsureParentDirectory(string path) =>
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

    private enum Boundary
    {
        None,
        BindingContextPrecision,
        NestedReferenceSource,
        TargetIdentity,
        RepeatedZeroArgumentMethod,
        ResultExplanation,
    }

    private sealed record EvaluatorCall(
        StaticFieldExpressionEvaluationResult Result,
        int ResolverCallCount);

    private sealed record Projection(
        string Syntax,
        string Context,
        string Symbol,
        string Storage,
        string Value,
        string? Terminal)
    {
        internal static Projection FromJson(JsonElement element, string? terminal) => new(
            RequiredString(element, "syntax"),
            RequiredString(element, "context"),
            RequiredString(element, "symbol"),
            RequiredString(element, "storage"),
            RequiredString(element, "value"),
            terminal);
    }

    private sealed record Control(
        string Expression,
        string Relationship,
        string EvaluationSha256,
        int PoisonResolverCallCount,
        Projection Actual);

    private sealed record Comparison(
        string Expression,
        string EvidenceView,
        string Relationship,
        string FirstBoundary,
        string CapturedEvaluationSha256,
        Projection Captured,
        Projection Actual);

    private sealed record Row(
        string Id,
        int Ordinal,
        string Shape,
        string SnapshotSha256,
        string Expression,
        string EvidenceView,
        string SuffixProfile,
        string FirstBoundary,
        Boundary PostW7Boundary,
        string Usefulness,
        string DecisionImpact,
        string CapturedEvaluationSha256,
        int ContextResolverCallCount,
        Projection Captured,
        Projection Actual,
        Control? Control,
        Comparison? Comparison);

    private sealed record BoundaryRank(
        Boundary Boundary,
        int IndependentIncidentCount,
        int ApplicationShapeCount,
        int DecisionChangingQuestionCount,
        int UsefulQuestionCount,
        int ExactAttributableEvidenceCount)
    {
        internal bool SubstantivelyEquals(BoundaryRank other) =>
            IndependentIncidentCount == other.IndependentIncidentCount &&
            DecisionChangingQuestionCount == other.DecisionChangingQuestionCount &&
            UsefulQuestionCount == other.UsefulQuestionCount &&
            ExactAttributableEvidenceCount == other.ExactAttributableEvidenceCount;
    }

    private sealed record Aggregate(
        int TotalQuestions,
        int DistinctIncidents,
        int DistinctApplicationShapes,
        int DistinctSnapshots,
        int ExactAnswers,
        int UsefulAnswers,
        int DecisionChangingAnswers,
        ImmutableArray<BoundaryRank> Ranking)
    {
        internal static Aggregate Create(ImmutableArray<Row> rows)
        {
            var ranking = rows
                .Where(static row => row.PostW7Boundary != Boundary.None)
                .GroupBy(static row => row.PostW7Boundary)
                .Select(static group => new BoundaryRank(
                    group.Key,
                    group.Select(static row => row.Id).Distinct(StringComparer.Ordinal).Count(),
                    group.Select(static row => row.Shape).Distinct(StringComparer.Ordinal).Count(),
                    group.Count(),
                    group.Count(),
                    group.Count(static row => row.Actual.Value.StartsWith("Exact", StringComparison.Ordinal))))
                .OrderByDescending(static item => item.IndependentIncidentCount)
                .ThenByDescending(static item => item.DecisionChangingQuestionCount)
                .ThenByDescending(static item => item.UsefulQuestionCount)
                .ThenByDescending(static item => item.ExactAttributableEvidenceCount)
                .ThenBy(static item => item.Boundary)
                .ToImmutableArray();
            return new Aggregate(
                rows.Length,
                rows.Select(static row => row.Id).Distinct(StringComparer.Ordinal).Count(),
                rows.Select(static row => row.Shape).Distinct(StringComparer.Ordinal).Count(),
                rows.Select(static row => row.SnapshotSha256).Distinct(StringComparer.Ordinal).Count(),
                rows.Count(static row => row.Actual.Value.StartsWith("Exact", StringComparison.Ordinal)),
                rows.Length,
                rows.Length,
                ranking);
        }
    }

    private sealed record Decision(string Status, string? Selection);

    private sealed class ArtifactResolver : IDumpPortablePdbArtifactResolver
    {
        private readonly string? path;
        private readonly bool partial;
        private readonly bool poison;

        private ArtifactResolver(string? path, bool partial, bool poison)
        {
            this.path = path;
            this.partial = partial;
            this.poison = poison;
        }

        internal int CallCount { get; private set; }

        internal static ArtifactResolver Exact(string path) => new(path, partial: false, poison: false);

        internal static ArtifactResolver Partial(string path) => new(path, partial: true, poison: false);

        internal static ArtifactResolver Poison() => new(path: null, partial: false, poison: true);

        ImmutableArray<DumpPortablePdbArtifactRead> IDumpPortablePdbArtifactResolver.Resolve(
            DumpPortablePdbArtifactResolutionRequest request)
        {
            CallCount++;
            if (poison)
            {
                throw new InvalidOperationException(
                    "A context-independent or syntax-rejected W7 expression consulted a poisoned resolver.");
            }

            var bytes = File.ReadAllBytes(path!).ToImmutableArray();
            return partial
                ? ImmutableArray.Create(DumpPortablePdbArtifactRead.Partial(
                    "portfolio:partial-pdb",
                    bytes.Length,
                    bytes[..Math.Min(64, bytes.Length - 1)]))
                : ImmutableArray.Create(DumpPortablePdbArtifactRead.Exact("portfolio:exact-pdb", bytes));
        }
    }

    private sealed record Options(
        string ManifestPath,
        string RepositoryRoot,
        string DumpRoot,
        string MachineOutputPath,
        string HumanOutputPath)
    {
        internal static Options Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length ||
                    args[index] is not (
                        "--w7-portfolio-manifest" or "--repository-root" or "--dump-root" or
                        "--machine-output" or "--human-output") ||
                    !values.TryAdd(args[index], args[index + 1]) ||
                    string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new W7PortfolioArgumentException("Expected five distinct W7 option/value pairs.");
                }
            }
            if (values.Count != 5)
            {
                throw new W7PortfolioArgumentException("Expected five distinct W7 option/value pairs.");
            }
            return new Options(
                Path.GetFullPath(values["--w7-portfolio-manifest"]),
                Path.GetFullPath(values["--repository-root"]),
                Path.GetFullPath(values["--dump-root"]),
                Path.GetFullPath(values["--machine-output"]),
                Path.GetFullPath(values["--human-output"]));
        }
    }

    private sealed class W7PortfolioArgumentException(string message) : Exception(message);
}
