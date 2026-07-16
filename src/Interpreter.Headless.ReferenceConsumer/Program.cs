using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpDebugging;
using Interpreter.Product.DumpQuery;

namespace Interpreter.Headless.ReferenceConsumer;

internal static class Program
{
    private const int MachineSchemaVersion = 1;
    private const int ManifestSchemaVersion = 1;
    private const int MaximumScenarios = 64;
    private const string GeneratedFixtureCaveat =
        "Generated fixture evidence views validate routing only; they are not representative incident observations.";
    private const string RepresentativeCorpusCaveat =
        "Representative designation comes from the predeclared incident manifest; the consumer does not independently verify provenance.";

    internal static int Main(string[] args)
    {
        if (UsefulnessPortfolioRunner.IsRequested(args))
        {
            return UsefulnessPortfolioRunner.Run(args);
        }

        try
        {
            var options = CommandLineOptions.Parse(args);
            var manifest = LoadManifest(options.ManifestPath);
            var dumpPath = ResolveDumpPath(options, manifest);
            var opened = ClrmdDumpSession.Open(dumpPath);
            if (opened.Status != ClrmdEvidenceStatus.Exact || opened.Value is null)
            {
                Console.Error.WriteLine($"W5_CONSUMER_DUMP_OPEN_FAILED:{opened.Status}:{opened.Issue}");
                return 4;
            }

            using var session = opened.Value;
            var rootSearch = session.FindStrongHandleObjectsByTypeName(
                manifest.Root.TypeName,
                manifest.Root.MaximumMatches,
                manifest.Root.MaximumHandlesScanned);
            var rootBinding = DumpQueryRootBinding.FromSearchResult(manifest.Root.Name, rootSearch);
            if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || rootBinding.Root is null)
            {
                Console.Error.WriteLine($"W5_CONSUMER_ROOT_NOT_EXACT:{rootBinding.Status}:{rootBinding.Issue}");
                return 4;
            }

            var rows = manifest.Scenarios
                .Select(scenario => RunScenario(session, rootBinding, scenario))
                .ToImmutableArray();
            WriteMachineReport(options.MachineOutputPath, manifest, session.Snapshot, rows);
            WriteHumanReport(options.HumanOutputPath, manifest, rows);
            Console.WriteLine($"W5_CONSUMER_OK:{rows.Length}");
            return 0;
        }
        catch (CommandLineException exception)
        {
            Console.Error.WriteLine($"W5_CONSUMER_ARGUMENT_INVALID:{exception.Message}");
            return 2;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or ArgumentException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"W5_CONSUMER_MANIFEST_INVALID:{exception.Message}");
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"W5_CONSUMER_OUTPUT_FAILED:{exception.GetType().Name}");
            return 5;
        }
    }

    private static ScenarioManifest LoadManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException("The named scenario manifest does not exist.");
        }

        var manifest = JsonSerializer.Deserialize<ScenarioManifest>(
            File.ReadAllBytes(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            }) ?? throw new InvalidDataException("The scenario manifest is empty.");
        manifest.Validate();
        return manifest;
    }

    private static string ResolveDumpPath(CommandLineOptions options, ScenarioManifest manifest)
    {
        var candidate = options.DumpPathOverride ?? manifest.DumpPath;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new InvalidDataException("A dump path is required in the manifest or command line.");
        }

        if (Path.IsPathFullyQualified(candidate))
        {
            return candidate;
        }

        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(options.ManifestPath))!;
        return Path.GetFullPath(Path.Combine(manifestDirectory, candidate));
    }

    private static ScenarioRow RunScenario(
        ClrmdDumpSession session,
        DumpQueryRootBinding root,
        ScenarioDefinition scenario)
    {
        var mode = ParseMode(scenario.MethodMode);
        var view = ParseView(scenario.FixtureEvidenceView);
        var policy = DumpExpressionPolicy.Create(
            mode,
            scenario.InstructionLimit,
            scenario.LogicalDepthLimit,
            scenario.TraversalLimit);
        OutcomeProjection? firstProjection = null;
        string? firstProjectionSha256 = null;
        DumpExpressionRequest? request = null;

        for (var repetition = 0; repetition < scenario.RepeatCount; repetition++)
        {
            using var cancellation = new CancellationTokenSource();
            if (scenario.CancelBeforeExecution)
            {
                cancellation.Cancel();
            }

            var outcome = EvaluateScenario(session, root, scenario, policy, view, cancellation.Token);
            request ??= outcome.Request;
            var projection = ProjectOutcome(outcome);
            var projectionBytes = SerializeOutcomeProjection(projection);
            var projectionSha256 = Hash(projectionBytes);
            if (firstProjection is null)
            {
                firstProjection = projection;
                firstProjectionSha256 = projectionSha256;
            }
            else if (!SerializeOutcomeProjection(firstProjection).AsSpan().SequenceEqual(projectionBytes))
            {
                throw new InvalidDataException(
                    $"Scenario '{scenario.Id}' did not replay identically within one open session.");
            }
        }

        return new ScenarioRow(
            scenario.Id,
            scenario.Expression,
            scenario.MethodMode,
            scenario.FixtureEvidenceView,
            scenario.RepeatCount,
            request?.Sha256,
            firstProjection!,
            firstProjectionSha256!);
    }

    private static DumpExpressionEvaluationOutcome EvaluateScenario(
        ClrmdDumpSession session,
        DumpQueryRootBinding root,
        ScenarioDefinition scenario,
        DumpExpressionPolicy policy,
        FixtureEvidenceView view,
        CancellationToken cancellationToken)
    {
        if (view == FixtureEvidenceView.Captured)
        {
            return DumpExpressionEvaluator.Evaluate(
                session,
                scenario.Expression,
                root,
                policy,
                cancellationToken);
        }

        var classification = DumpExpressionClassifier.Classify(scenario.Expression, root, policy);
        if (classification.Status != DumpExpressionClassificationStatus.Accepted)
        {
            return DumpExpressionEvaluationOutcome.FromClassificationFailure(classification);
        }

        if (classification.Kind != DumpExpressionKind.CounterfactualMethod)
        {
            throw new InvalidDataException(
                $"Scenario '{scenario.Id}' applies a generated method-evidence view to a W2 expression.");
        }

        return DumpExpressionEvaluator.EvaluateMethod(
            new FixtureEvidenceSource(session, view),
            classification.Request!,
            cancellationToken);
    }

    private static OutcomeProjection ProjectOutcome(DumpExpressionEvaluationOutcome outcome)
    {
        var request = outcome.Request;
        return outcome.Kind switch
        {
            DumpExpressionEvaluationOutcomeKind.DerivedQuery =>
                ProjectDerivedQuery(request!, outcome.DerivedQueryResult!),
            DumpExpressionEvaluationOutcomeKind.CounterfactualExecution =>
                ProjectCounterfactual(request!, outcome.CounterfactualExecutionResult!),
            DumpExpressionEvaluationOutcomeKind.CounterfactualPreparationFailure =>
                ProjectPreparationFailure(request!, outcome.CounterfactualPreparationFailure!),
            DumpExpressionEvaluationOutcomeKind.ClassificationFailure =>
                ProjectClassificationFailure(outcome.ClassificationFailure!),
            DumpExpressionEvaluationOutcomeKind.AcquisitionFailure =>
                ProjectAcquisitionFailure(request!, outcome.AcquisitionFailure!),
            _ => throw new InvalidDataException("The evaluator returned an unknown outcome case."),
        };
    }

    private static OutcomeProjection ProjectDerivedQuery(
        DumpExpressionRequest request,
        EvaluationResult<DumpQueryValue> result)
    {
        var canonical = EvaluationResultReplay.SerializeCanonical(
            result,
            static value => value.ToCanonicalReplayProjection());
        return new OutcomeProjection(
            DumpExpressionEvaluationOutcomeKind.DerivedQuery.ToString(),
            result.SemanticMode.ToString(),
            result.Completion.ToString(),
            result.Completeness.ToString(),
            result.Evidence.ToString(),
            result.Effects.ToString(),
            result.Value?.ToCanonicalReplayProjection(),
            CollectBounds(request, result.Context.Bounds),
            ProjectProvenance(result.Provenance),
            ProjectDiagnostics(result.Diagnostics),
            Hash(canonical),
            Convert.ToBase64String(canonical));
    }

    private static OutcomeProjection ProjectCounterfactual(
        DumpExpressionRequest request,
        CounterfactualExecutionResult result) => new(
        DumpExpressionEvaluationOutcomeKind.CounterfactualExecution.ToString(),
        result.SemanticMode.ToString(),
        result.Completion.ToString(),
        result.Completeness.ToString(),
        result.Evidence.ToString(),
        result.Effects.ToString(),
        ProjectCounterfactualValue(result.Value),
        CollectBounds(request, result.Context.EvidenceContext.Bounds),
        ProjectProvenance(result.Provenance),
        ProjectDiagnostics(result.Diagnostics),
        result.Sha256,
        Convert.ToBase64String(result.CanonicalBytes.AsSpan()));

    private static OutcomeProjection ProjectPreparationFailure(
        DumpExpressionRequest request,
        CounterfactualMethodPreparationFailure failure) => new(
        DumpExpressionEvaluationOutcomeKind.CounterfactualPreparationFailure.ToString(),
        failure.SemanticMode.ToString(),
        failure.Completion.ToString(),
        failure.Completeness.ToString(),
        failure.Evidence.ToString(),
        failure.Effects.ToString(),
        Value: null,
        CollectBounds(request, failure.Context.Bounds),
        ProjectProvenance(failure.Provenance),
        ProjectDiagnostics(failure.Diagnostics),
        UnderlyingArtifactSha256: null,
        UnderlyingCanonicalBase64: null);

    private static OutcomeProjection ProjectClassificationFailure(DumpExpressionClassification failure) => new(
        DumpExpressionEvaluationOutcomeKind.ClassificationFailure.ToString(),
        SemanticMode: null,
        Completion: null,
        Completeness: null,
        Evidence: null,
        Effects: null,
        Value: null,
        CollectBounds(failure.Request, ImmutableArray<EvaluationDeterministicBound>.Empty),
        ImmutableArray<ProvenanceProjection>.Empty,
        ImmutableArray.Create(new DiagnosticProjection(failure.DiagnosticCode!, failure.DiagnosticMessage!)),
        UnderlyingArtifactSha256: null,
        UnderlyingCanonicalBase64: null);

    private static OutcomeProjection ProjectAcquisitionFailure(
        DumpExpressionRequest request,
        DumpMethodAcquisitionFailure failure) => new(
        DumpExpressionEvaluationOutcomeKind.AcquisitionFailure.ToString(),
        SemanticMode: null,
        Completion: null,
        Completeness: null,
        failure.EvidenceStatus is { } status ? $"Adapter:{status}" : null,
        Effects: null,
        Value: null,
        CollectBounds(request, ImmutableArray<EvaluationDeterministicBound>.Empty),
        ImmutableArray<ProvenanceProjection>.Empty,
        ImmutableArray.Create(new DiagnosticProjection(failure.Code, failure.Message)),
        UnderlyingArtifactSha256: null,
        UnderlyingCanonicalBase64: null);

    private static string? ProjectCounterfactualValue(CounterfactualExecutionValue? value) => value?.Kind switch
    {
        null => null,
        CounterfactualExecutionValueKind.ExactReturn => $"i32:{value.ExactInt32}",
        CounterfactualExecutionValueKind.UnknownReturn => $"unknown:sha256:{value.Lineage!.Sha256}",
        CounterfactualExecutionValueKind.ExecutionPrefix => "execution-prefix",
        CounterfactualExecutionValueKind.TargetException => $"target-outcome:sha256:{value.TargetOutcome!.Sha256}",
        _ => throw new InvalidDataException("The W4 result returned an unknown value case."),
    };

    private static ImmutableArray<BoundProjection> CollectBounds(
        DumpExpressionRequest? request,
        ImmutableArray<EvaluationDeterministicBound> resultBounds)
    {
        var byName = new SortedDictionary<string, long>(StringComparer.Ordinal);
        if (request is not null)
        {
            Add(request.RootBinding.AppliedBounds);
            Add(request.ReachedBounds);
        }

        Add(resultBounds);
        return byName.Select(static pair => new BoundProjection(pair.Key, pair.Value)).ToImmutableArray();

        void Add(ImmutableArray<EvaluationDeterministicBound> bounds)
        {
            foreach (var bound in bounds)
            {
                if (byName.TryGetValue(bound.Name, out var prior) && prior != bound.Value)
                {
                    throw new InvalidDataException($"Bound '{bound.Name}' has conflicting values.");
                }

                byName[bound.Name] = bound.Value;
            }
        }
    }

    private static ImmutableArray<ProvenanceProjection> ProjectProvenance(
        ImmutableArray<EvaluationProvenance> provenance) => provenance
        .Select(static item => new ProvenanceProjection(
            item.Kind.ToString(),
            item.SourceId,
            item.Address,
            item.RequestedLength,
            item.ObservedLength))
        .ToImmutableArray();

    private static ImmutableArray<DiagnosticProjection> ProjectDiagnostics(
        ImmutableArray<EvaluationDiagnostic> diagnostics) => diagnostics
        .Select(static item => new DiagnosticProjection(item.Code, item.Message))
        .ToImmutableArray();

    private static byte[] SerializeOutcomeProjection(OutcomeProjection projection)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteOutcome(writer, projection);
        }

        return buffer.ToArray();
    }

    private static void WriteMachineReport(
        string path,
        ScenarioManifest manifest,
        ClrmdSnapshotIdentity snapshot,
        ImmutableArray<ScenarioRow> rows)
    {
        EnsureParentDirectory(path);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("machineSchemaVersion", MachineSchemaVersion);
        writer.WriteNumber("manifestSchemaVersion", manifest.SchemaVersion);
        writer.WriteString("corpusKind", manifest.CorpusKind);
        writer.WriteString("dumpSnapshotSha256", snapshot.Sha256);
        writer.WriteString("rootName", manifest.Root.Name);
        writer.WriteString("rootTypeName", manifest.Root.TypeName);
        writer.WriteString("fixtureCaveat", GetCorpusCaveat(manifest));
        writer.WriteStartArray("scenarios");
        foreach (var row in rows)
        {
            writer.WriteStartObject();
            writer.WriteString("id", row.Id);
            if (row.Expression is null)
            {
                writer.WriteNull("expression");
            }
            else
            {
                writer.WriteString("expression", row.Expression);
            }

            writer.WriteString("methodMode", row.MethodMode);
            writer.WriteString("fixtureEvidenceView", row.FixtureEvidenceView);
            writer.WriteNumber("repetitions", row.Repetitions);
            if (row.RequestSha256 is null)
            {
                writer.WriteNull("requestSha256");
            }
            else
            {
                writer.WriteString("requestSha256", row.RequestSha256);
            }

            writer.WriteString("outcomeProjectionSha256", row.OutcomeProjectionSha256);
            writer.WritePropertyName("outcome");
            WriteOutcome(writer, row.Outcome);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteOutcome(Utf8JsonWriter writer, OutcomeProjection projection)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", projection.Kind);
        WriteNullable(writer, "semanticMode", projection.SemanticMode);
        WriteNullable(writer, "completion", projection.Completion);
        WriteNullable(writer, "completeness", projection.Completeness);
        WriteNullable(writer, "evidence", projection.Evidence);
        WriteNullable(writer, "effects", projection.Effects);
        WriteNullable(writer, "value", projection.Value);
        writer.WriteStartArray("reachedBounds");
        foreach (var bound in projection.ReachedBounds)
        {
            writer.WriteStartObject();
            writer.WriteString("name", bound.Name);
            writer.WriteNumber("value", bound.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("provenance");
        foreach (var item in projection.Provenance)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", item.Kind);
            writer.WriteString("sourceId", item.SourceId);
            WriteNullableHex(writer, "address", item.Address);
            WriteNullable(writer, "requestedLength", item.RequestedLength);
            WriteNullable(writer, "observedLength", item.ObservedLength);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("diagnostics");
        foreach (var diagnostic in projection.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("message", diagnostic.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteNullable(writer, "underlyingArtifactSha256", projection.UnderlyingArtifactSha256);
        WriteNullable(writer, "underlyingCanonicalBase64", projection.UnderlyingCanonicalBase64);
        writer.WriteEndObject();
    }

    private static void WriteHumanReport(
        string path,
        ScenarioManifest manifest,
        ImmutableArray<ScenarioRow> rows)
    {
        EnsureParentDirectory(path);
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine("W5 expression-facade report v1");
        writer.WriteLine($"Corpus: {manifest.CorpusKind}");
        writer.WriteLine($"Caveat: {GetCorpusCaveat(manifest)}");
        foreach (var row in rows)
        {
            var outcome = row.Outcome;
            var bounds = outcome.ReachedBounds.IsEmpty
                ? "none"
                : string.Join(',', outcome.ReachedBounds.Select(static bound => $"{bound.Name}={bound.Value}"));
            var diagnostics = outcome.Diagnostics.IsEmpty
                ? "none"
                : string.Join(',', outcome.Diagnostics.Select(static item => item.Code));
            writer.WriteLine(
                $"{row.Id}: outcome={outcome.Kind}; semantic={outcome.SemanticMode ?? "none"}; " +
                $"completion={outcome.Completion ?? "none"}; completeness={outcome.Completeness ?? "none"}; " +
                $"evidence={outcome.Evidence ?? "none"}; effects={outcome.Effects ?? "none"}; " +
                $"value={outcome.Value ?? "none"}; bounds={bounds}; provenance={outcome.Provenance.Length}; " +
                $"diagnostics={diagnostics}; fixture-view={row.FixtureEvidenceView}; repetitions={row.Repetitions}");
        }
    }

    private static string GetCorpusCaveat(ScenarioManifest manifest) => manifest.CorpusKind switch
    {
        "GeneratedValidation" => GeneratedFixtureCaveat,
        "RepresentativeIncident" => RepresentativeCorpusCaveat,
        _ => throw new InvalidDataException($"Unknown corpus kind '{manifest.CorpusKind}'."),
    };

    private static void EnsureParentDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
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

    private static void WriteNullable(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteNumber(name, value.Value);
        }
    }

    private static void WriteNullableHex(Utf8JsonWriter writer, string name, ulong? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, $"0x{value.Value:X16}");
        }
    }

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static DumpMethodEvaluationMode ParseMode(string value) => value switch
    {
        nameof(DumpMethodEvaluationMode.Interpreted) => DumpMethodEvaluationMode.Interpreted,
        nameof(DumpMethodEvaluationMode.Modeled) => DumpMethodEvaluationMode.Modeled,
        _ => throw new InvalidDataException($"Unknown method mode '{value}'."),
    };

    private static FixtureEvidenceView ParseView(string value) => value switch
    {
        nameof(FixtureEvidenceView.Captured) => FixtureEvidenceView.Captured,
        nameof(FixtureEvidenceView.MarkerPartial) => FixtureEvidenceView.MarkerPartial,
        nameof(FixtureEvidenceView.MarkerUnavailable) => FixtureEvidenceView.MarkerUnavailable,
        nameof(FixtureEvidenceView.ModuleUnavailable) => FixtureEvidenceView.ModuleUnavailable,
        _ => throw new InvalidDataException($"Unknown fixture evidence view '{value}'."),
    };

    private sealed class FixtureEvidenceSource(
        ClrmdDumpSession session,
        FixtureEvidenceView view) : IDumpMethodEvidenceSource
    {
        public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

        public ImmutableArray<ClrmdModuleInfo> Modules => view == FixtureEvidenceView.ModuleUnavailable
            ? ImmutableArray<ClrmdModuleInfo>.Empty
            : session.Modules;

        public ClrmdEvidenceResult<ModuleContentIdentity> ReadModuleContentIdentity(ClrmdModuleInfo module) =>
            session.ReadModuleContentIdentity(module);

        public ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
            ClrmdModuleInfo module,
            string typeName,
            string methodName) => session.ReadMethodBody(module, typeName, methodName);

        public ClrmdHeapObjectSearchResult FindStrongHandleObjectsByTypeName(
            string typeName,
            int maximumMatches,
            int maximumHandlesScanned) => session.FindStrongHandleObjectsByTypeName(
            typeName,
            maximumMatches,
            maximumHandlesScanned);

        public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
            ClrmdHeapObjectInfo owner,
            string fieldName)
        {
            var exact = session.ReadInt32Field(owner, fieldName);
            if (view is not (FixtureEvidenceView.MarkerPartial or FixtureEvidenceView.MarkerUnavailable) ||
                !string.Equals(fieldName, DumpMethodAcquisitionFacade.MarkerFieldName, StringComparison.Ordinal))
            {
                return exact;
            }

            if (exact.Status != ClrmdEvidenceStatus.Exact || exact.Value is null)
            {
                return exact;
            }

            var prefixLength = view == FixtureEvidenceView.MarkerPartial ? 2 : 0;
            var memory = MemoryReadResult.Create(
                exact.Value.Memory.SourceId,
                exact.Value.Memory.Address,
                exact.Value.Memory.RequestedLength,
                exact.Value.Memory.Bytes.AsSpan(0, prefixLength));
            return ClrmdEvidenceResult<ClrmdInt32FieldObservation>.Create(
                view == FixtureEvidenceView.MarkerPartial
                    ? ClrmdEvidenceStatus.Partial
                    : ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable,
                new ClrmdInt32FieldObservation(exact.Value.Field, memory, value: null),
                ImmutableArray.Create(memory),
                exact.AppliedBounds);
        }
    }

    private enum FixtureEvidenceView
    {
        Captured,
        MarkerPartial,
        MarkerUnavailable,
        ModuleUnavailable,
    }

    private sealed record ScenarioRow(
        string Id,
        string? Expression,
        string MethodMode,
        string FixtureEvidenceView,
        int Repetitions,
        string? RequestSha256,
        OutcomeProjection Outcome,
        string OutcomeProjectionSha256);

    private sealed record OutcomeProjection(
        string Kind,
        string? SemanticMode,
        string? Completion,
        string? Completeness,
        string? Evidence,
        string? Effects,
        string? Value,
        ImmutableArray<BoundProjection> ReachedBounds,
        ImmutableArray<ProvenanceProjection> Provenance,
        ImmutableArray<DiagnosticProjection> Diagnostics,
        string? UnderlyingArtifactSha256,
        string? UnderlyingCanonicalBase64);

    private sealed record BoundProjection(string Name, long Value);

    private sealed record ProvenanceProjection(
        string Kind,
        string SourceId,
        ulong? Address,
        int? RequestedLength,
        int? ObservedLength);

    private sealed record DiagnosticProjection(string Code, string Message);

    private sealed class ScenarioManifest
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("dumpPath")]
        public string? DumpPath { get; init; }

        [JsonPropertyName("corpusKind")]
        public string CorpusKind { get; init; } = "GeneratedValidation";

        [JsonPropertyName("root")]
        public RootDefinition Root { get; init; } = null!;

        [JsonPropertyName("scenarios")]
        public ImmutableArray<ScenarioDefinition> Scenarios { get; init; }

        internal void Validate()
        {
            if (SchemaVersion != ManifestSchemaVersion)
            {
                throw new InvalidDataException($"Manifest schema version must be {ManifestSchemaVersion}.");
            }

            if (Root is null)
            {
                throw new InvalidDataException("The manifest root selector is required.");
            }

            _ = GetCorpusCaveat(this);

            Root.Validate();
            if (Scenarios.IsDefaultOrEmpty || Scenarios.Length > MaximumScenarios ||
                Scenarios.Any(static scenario => scenario is null))
            {
                throw new InvalidDataException($"A manifest requires 1 to {MaximumScenarios} scenarios.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scenario in Scenarios)
            {
                scenario.Validate();
                if (!ids.Add(scenario.Id))
                {
                    throw new InvalidDataException($"Scenario id '{scenario.Id}' is duplicated.");
                }
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

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > DumpExpressionRequest.MaximumRootNameCharacters ||
                string.IsNullOrWhiteSpace(TypeName) || TypeName.Length > 4_096 ||
                MaximumMatches is < 1 or > 4_096 ||
                MaximumHandlesScanned is < 1 or > 100_000)
            {
                throw new InvalidDataException("The root selector is missing or outside its deterministic bounds.");
            }
        }
    }

    private sealed class ScenarioDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("expression")]
        public string? Expression { get; init; }

        [JsonPropertyName("methodMode")]
        public string MethodMode { get; init; } = string.Empty;

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
            if (string.IsNullOrWhiteSpace(Id) || Id.Length > 128 ||
                !Id.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-'))
            {
                throw new InvalidDataException("Scenario ids require bounded lowercase ASCII identity text.");
            }

            if (Expression?.Length > DumpExpressionRequest.MaximumExpressionCharacters || RepeatCount is < 1 or > 4)
            {
                throw new InvalidDataException($"Scenario '{Id}' exceeds an expression or repetition bound.");
            }

            _ = ParseMode(MethodMode);
            _ = ParseView(FixtureEvidenceView);
            _ = DumpExpressionPolicy.Create(
                ParseMode(MethodMode),
                InstructionLimit,
                LogicalDepthLimit,
                TraversalLimit);
        }
    }

    private sealed record CommandLineOptions(
        string ManifestPath,
        string MachineOutputPath,
        string HumanOutputPath,
        string? DumpPathOverride)
    {
        internal static CommandLineOptions Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (args.Length == 0 || args.Length % 2 != 0)
            {
                throw new CommandLineException("Expected named option/value pairs.");
            }

            for (var index = 0; index < args.Length; index += 2)
            {
                if (args[index] is not ("--manifest" or "--machine-output" or "--human-output" or "--dump") ||
                    string.IsNullOrWhiteSpace(args[index + 1]) ||
                    !values.TryAdd(args[index], args[index + 1]))
                {
                    throw new CommandLineException("An option is unknown, duplicated, or has an empty value.");
                }
            }

            return new CommandLineOptions(
                Required("--manifest"),
                Required("--machine-output"),
                Required("--human-output"),
                values.GetValueOrDefault("--dump"));

            string Required(string name) => values.TryGetValue(name, out var value)
                ? value
                : throw new CommandLineException($"Required option '{name}' is missing.");
        }
    }

    private sealed class CommandLineException(string message) : ArgumentException(message);
}
