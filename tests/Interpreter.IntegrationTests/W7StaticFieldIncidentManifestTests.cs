using System.Text.Json;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Freezes the W7 target/PDB contract and sixteen independent synthetic incident definitions before target creation.
/// </summary>
/// <remarks>
/// The manifest is designed evidence only. It makes no claim about observations from external applications and does
/// not allow an evidence view to invent an exact fact absent from its future full dump and matching Portable PDB.
/// The dump-free stage seam is deliberately test-owned until W7.2 introduces the production immutable contracts.
/// </remarks>
public sealed class W7StaticFieldIncidentManifestTests
{
    private static readonly string[] StageNames = ["syntax", "context", "symbol", "storage", "value"];

    /// <summary>
    /// Verifies every planned incident has complete acquisition and control inputs, a distinct full snapshot, an
    /// explicit first boundary, balanced application-shape coverage, and a valid typed stage progression.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Manifest_freezes_sixteen_independent_meaningful_incidents()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(ResolveManifestPath()));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("interpreter-w7-static-field-incidents-v1", root.GetProperty("corpusId").GetString());
        Assert.Equal("designed-synthetic", root.GetProperty("evidenceKind").GetString());
        Assert.Equal("StaticFieldExpressionV1", root.GetProperty("languageProfile").GetString());

        var target = root.GetProperty("targetContract");
        Assert.Equal(
            "tests/Interpreter.W7TestTarget/Interpreter.W7TestTarget.csproj",
            target.GetProperty("projectPath").GetString());
        Assert.Equal("net10.0", target.GetProperty("targetFramework").GetString());
        Assert.Equal("Release", target.GetProperty("configuration").GetString());
        Assert.True(target.GetProperty("optimize").GetBoolean());
        Assert.Equal("portable", target.GetProperty("debugType").GetString());
        Assert.True(target.GetProperty("headlessProcess").GetBoolean());
        Assert.Equal(
            new[]
            {
                "tests/Interpreter.W7TestTarget/Program.cs",
                "tests/Interpreter.W7TestTarget/StaticValues.cs",
                "tests/Interpreter.W7TestTarget/RequestPipeline.cs",
                "tests/Interpreter.W7TestTarget/BatchPipeline.cs",
                "tests/Interpreter.W7TestTarget/CoordinatorPipeline.cs",
                "tests/Interpreter.W7TestTarget/WorkflowPipeline.cs",
            },
            ReadStrings(target.GetProperty("sourceInputs")));
        Assert.EndsWith(
            ".pdb",
            target.GetProperty("portablePdbArtifact").GetString(),
            StringComparison.Ordinal);

        var companions = root.GetProperty("companionArtifactContracts").EnumerateArray().ToArray();
        Assert.Equal(2, companions.Length);
        var companionIds = companions.Select(static item => RequiredString(item, "id")).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new[] { "duplicate-qualified-type", "portable-pdb-identity-conflict" },
            companionIds.Order(StringComparer.Ordinal));
        foreach (var companion in companions)
        {
            Assert.EndsWith(".csproj", RequiredString(companion, "projectPath"), StringComparison.Ordinal);
            Assert.NotEmpty(ReadStrings(companion.GetProperty("sourceInputs")));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(companion, "artifactPath")));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(companion, "acquisition")));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(companion, "purpose")));
        }

        var taxonomy = ReadStrings(root.GetProperty("statusTaxonomy"));
        Assert.Equal(
            new[] { "Exact", "Partial", "Unavailable", "Ambiguous", "Conflict", "Invalid", "Unsupported" },
            taxonomy);

        var incidents = root.GetProperty("incidents").EnumerateArray().ToArray();
        Assert.Equal(16, incidents.Length);
        Assert.Equal(16, incidents.Select(static incident => RequiredString(incident, "id")).Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, 16),
            incidents.Select(static incident => incident.GetProperty("ordinal").GetInt32()));
        Assert.Equal(
            16,
            incidents.Select(static incident => string.Join('\0', ReadStrings(incident.GetProperty("targetArguments"))))
                .Distinct(StringComparer.Ordinal)
                .Count());

        foreach (var shape in new[] { "Request", "Batch", "Coordinator", "Workflow" })
        {
            Assert.Equal(4, incidents.Count(incident => RequiredString(incident, "shape") == shape));
        }

        var observedStatuses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var incident in incidents)
        {
            Assert.Equal("independent-full-dump", RequiredString(incident, "snapshotKind"));
            var arguments = ReadStrings(incident.GetProperty("targetArguments"));
            Assert.True(arguments.Length >= 2);
            Assert.Equal("--incident", arguments[0]);
            Assert.Equal(RequiredString(incident, "id"), arguments[1]);

            var artifactInputs = ReadStrings(incident.GetProperty("artifactInputs"));
            Assert.Equal(artifactInputs.Length, artifactInputs.Distinct(StringComparer.Ordinal).Count());
            Assert.All(artifactInputs, id => Assert.Contains(id, companionIds));
            Assert.Contains(
                RequiredString(incident, "evidenceView"),
                new[] { "exact", "truncate-static-slot", "replace-method-table", "invalidate-field-signature" });
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(incident, "expression")));
            Assert.Contains(
                RequiredString(incident, "suffixProfile"),
                new[] { "None", "W2DirectFieldV1", "W6ConditionalChainV1", "W6MemberChainV1" });
            Assert.True(incident.GetProperty("contextRequest").TryGetProperty("pdbMode", out _));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(incident, "usefulness")));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(incident, "decisionImpact")));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(incident, "firstBoundary")));
            Assert.Contains(
                RequiredString(incident, "postW7Boundary"),
                new[]
                {
                    "None", "BindingContextPrecision", "NestedReferenceSource", "TargetIdentity",
                    "RepeatedZeroArgumentMethod", "ResultExplanation",
                });

            var expected = incident.GetProperty("expected");
            AssertExpectedShape(expected);
            AssertValidStageProgression(expected, RequiredString(incident, "firstBoundary"));
            AssertTerminalShape(incident.GetProperty("expectedTerminal"), RequiredString(expected, "value"));
            foreach (var stage in StageNames)
            {
                var status = RequiredString(expected, stage);
                if (taxonomy.Contains(status, StringComparer.Ordinal))
                {
                    observedStatuses.Add(status);
                }
                else if (status.StartsWith("Exact", StringComparison.Ordinal))
                {
                    observedStatuses.Add("Exact");
                }
            }

            if (incident.TryGetProperty("comparisonProjection", out var comparison))
            {
                Assert.Equal("invalidate-field-signature", RequiredString(comparison, "evidenceView"));
                Assert.StartsWith("global::", RequiredString(comparison, "expression"), StringComparison.Ordinal);
                Assert.Equal(
                    "invalid-declaration-distinct-from-exhaustive-absence",
                    RequiredString(comparison, "relationship"));
                var comparisonExpected = comparison.GetProperty("expected");
                AssertExpectedShape(comparisonExpected);
                AssertValidStageProgression(
                    comparisonExpected,
                    RequiredString(comparison, "firstBoundary"));
                foreach (var stage in StageNames)
                {
                    var status = RequiredString(comparisonExpected, stage);
                    if (taxonomy.Contains(status, StringComparer.Ordinal))
                    {
                        observedStatuses.Add(status);
                    }
                    else if (status.StartsWith("Exact", StringComparison.Ordinal))
                    {
                        observedStatuses.Add("Exact");
                    }
                }
            }

            var controlExpression = incident.GetProperty("controlExpression");
            var controlExpected = incident.GetProperty("controlExpected");
            var relationship = RequiredString(incident, "controlRelationship");
            if (controlExpression.ValueKind == JsonValueKind.Null)
            {
                Assert.Equal(JsonValueKind.Null, controlExpected.ValueKind);
                Assert.Equal("none", relationship);
            }
            else
            {
                Assert.StartsWith("global::", controlExpression.GetString(), StringComparison.Ordinal);
                Assert.Equal(JsonValueKind.Object, controlExpected.ValueKind);
                AssertExpectedShape(controlExpected);
                Assert.Equal("Exact", RequiredString(controlExpected, "syntax"));
                Assert.Equal("NotRequired", RequiredString(controlExpected, "context"));
                Assert.Equal("Exact", RequiredString(controlExpected, "symbol"));
                Assert.Equal("Exact", RequiredString(controlExpected, "storage"));
                Assert.StartsWith("Exact", RequiredString(controlExpected, "value"), StringComparison.Ordinal);
                Assert.Contains(
                    relationship,
                    new[] { "same-symbol-and-value", "fully-qualified-exact-baseline" });
                AssertTerminalShape(
                    incident.GetProperty("controlExpectedTerminal"),
                    RequiredString(controlExpected, "value"));
            }
        }

        Assert.Equal(taxonomy, observedStatuses.OrderBy(status => Array.IndexOf(taxonomy, status)));
        var comparisonIncident = Assert.Single(
            incidents,
            static incident => incident.TryGetProperty("comparisonProjection", out _));
        Assert.Equal(
            "workflow-field-absence-vs-invalid-signature",
            RequiredString(comparisonIncident, "id"));
        Assert.Equal("Unavailable", RequiredString(comparisonIncident.GetProperty("expected"), "symbol"));
        Assert.Equal(
            "Invalid",
            RequiredString(
                comparisonIncident.GetProperty("comparisonProjection").GetProperty("expected"),
                "symbol"));
        Assert.Contains(
            incidents,
            static item => RequiredString(item.GetProperty("expected"), "value") == "ExactNull");
        Assert.Contains(
            incidents,
            static item => RequiredString(item.GetProperty("expected"), "storage") == "Unavailable");
        Assert.Contains(
            incidents,
            static item => RequiredString(item, "suffixProfile") == "W2DirectFieldV1" &&
                RequiredString(item.GetProperty("expected"), "value").StartsWith("Exact", StringComparison.Ordinal));
        Assert.Contains(incidents, static item => RequiredString(item, "suffixProfile") == "W6ConditionalChainV1");
        Assert.Contains(incidents, static item => RequiredString(item, "suffixProfile") == "W6MemberChainV1");
        Assert.Equal(
            "global::Interpreter.W7TestTarget.StaticValues.Counter",
            RequiredString(incidents[0], "expression"));
        Assert.Equal(
            6,
            incidents.Count(static incident =>
                RequiredString(incident, "postW7Boundary") == "None"));
        Assert.Equal(
            4,
            incidents.Count(static incident =>
                RequiredString(incident, "postW7Boundary") == "BindingContextPrecision"));

        var contextPoisonControls = incidents.Where(static incident =>
            RequiredString(incident, "controlRelationship") == "fully-qualified-exact-baseline").ToArray();
        Assert.Equal(4, contextPoisonControls.Length);
        Assert.All(
            contextPoisonControls,
            static incident => Assert.Equal(
                "fully-qualified-exact-baseline",
                RequiredString(incident, "controlRelationship")));

        var identityConflict = Assert.Single(
            incidents,
            static incident => RequiredString(incident, "id") == "request-pdb-identity-conflict");
        Assert.Equal(new[] { "portable-pdb-identity-conflict" }, ReadStrings(identityConflict.GetProperty("artifactInputs")));
        var duplicateDefinition = Assert.Single(
            incidents,
            static incident => RequiredString(incident, "id") == "coordinator-duplicate-qualified-definition");
        Assert.Equal(new[] { "duplicate-qualified-type" }, ReadStrings(duplicateDefinition.GetProperty("artifactInputs")));
        Assert.Contains("--load-companion", ReadStrings(duplicateDefinition.GetProperty("targetArguments")));
    }

    /// <summary>
    /// Executes a dump-free, capability-injected draft pipeline to prove each typed boundary stops before every later
    /// capability and that a fully qualified request never consults poisoned frame/PDB/import capabilities.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Dump_free_stage_seam_distinguishes_context_and_value_outcomes()
    {
        var vectors = new[]
        {
            DraftVector.QualifiedWithPoisonedContext(),
            DraftVector.ContextualExact(),
            DraftVector.Stopping("selected-frame-unavailable", DraftStage.SelectedFrame, DraftOutcome.Unavailable),
            DraftVector.Stopping("portable-pdb-partial", DraftStage.PdbIdentity, DraftOutcome.Partial),
            DraftVector.Stopping("portable-pdb-conflict", DraftStage.PdbIdentity, DraftOutcome.Conflict),
            DraftVector.Stopping("import-payload-invalid", DraftStage.ImportScope, DraftOutcome.Invalid),
            DraftVector.Stopping("symbol-ambiguous", DraftStage.Symbol, DraftOutcome.Ambiguous),
            DraftVector.Stopping("storage-unavailable", DraftStage.Storage, DraftOutcome.Unavailable, requiresContext: false),
            DraftVector.Stopping("value-partial", DraftStage.Value, DraftOutcome.Partial, requiresContext: false),
            DraftVector.Stopping("exact-null", DraftStage.Value, DraftOutcome.ExactNull, requiresContext: false),
            DraftVector.Stopping("syntax-unsupported", DraftStage.Syntax, DraftOutcome.Unsupported),
        };

        foreach (var vector in vectors)
        {
            var probe = new DraftCapabilityProbe(vector.Name, vector.Outcomes);
            var result = ExecuteDraftPipeline(vector.RequiresContext, probe);
            Assert.Equal(vector.Name, result.Name);
            Assert.Equal(vector.TerminalStage, result.Stage);
            Assert.Equal(vector.TerminalOutcome, result.Outcome);
            Assert.Equal(vector.ExpectedCalls, probe.Calls);
        }

        var outcomes = vectors.Select(static vector => vector.TerminalOutcome).ToHashSet();
        Assert.Contains(DraftOutcome.ExactNull, outcomes);
        Assert.Contains(DraftOutcome.Partial, outcomes);
        Assert.Contains(DraftOutcome.Unavailable, outcomes);
        Assert.Contains(DraftOutcome.Ambiguous, outcomes);
        Assert.Contains(DraftOutcome.Conflict, outcomes);
        Assert.Contains(DraftOutcome.Invalid, outcomes);
        Assert.Contains(DraftOutcome.Unsupported, outcomes);

        var qualified = vectors[0];
        Assert.False(qualified.RequiresContext);
        Assert.DoesNotContain(DraftStage.SelectedFrame, qualified.ExpectedCalls);
        Assert.DoesNotContain(DraftStage.PdbIdentity, qualified.ExpectedCalls);
        Assert.DoesNotContain(DraftStage.ImportScope, qualified.ExpectedCalls);
    }

    private static DraftPipelineResult ExecuteDraftPipeline(bool requiresContext, DraftCapabilityProbe probe)
    {
        var syntax = probe.Observe(DraftStage.Syntax);
        if (syntax != DraftOutcome.Exact)
        {
            return new DraftPipelineResult(probe.Name, DraftStage.Syntax, syntax);
        }

        if (requiresContext)
        {
            foreach (var stage in new[] { DraftStage.SelectedFrame, DraftStage.PdbIdentity, DraftStage.ImportScope })
            {
                var context = probe.Observe(stage);
                if (context != DraftOutcome.Exact)
                {
                    return new DraftPipelineResult(probe.Name, stage, context);
                }
            }
        }

        foreach (var stage in new[] { DraftStage.Symbol, DraftStage.Storage, DraftStage.Value })
        {
            var outcome = probe.Observe(stage);
            if (outcome != DraftOutcome.Exact || stage == DraftStage.Value)
            {
                return new DraftPipelineResult(probe.Name, stage, outcome);
            }
        }

        throw new InvalidOperationException("The draft pipeline did not produce a terminal outcome.");
    }

    private static void AssertExpectedShape(JsonElement expected)
    {
        Assert.Equal(
            StageNames.Order(StringComparer.Ordinal),
            expected.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal));
        Assert.Contains(RequiredString(expected, "syntax"), new[] { "Exact", "Invalid", "Unsupported" });
        Assert.Contains(
            RequiredString(expected, "context"),
            new[]
            {
                "NotRequired", "Exact", "ExactNamespaceImport", "ExactTypeAlias", "ExactCurrentNamespace",
                "Partial", "Unavailable", "Conflict", "Invalid", "NotReached",
            });
        Assert.Contains(
            RequiredString(expected, "symbol"),
            new[] { "Exact", "Unavailable", "Ambiguous", "Conflict", "Invalid", "Unsupported", "NotReached" });
        Assert.Contains(
            RequiredString(expected, "storage"),
            new[] { "Exact", "Partial", "Unavailable", "Conflict", "Invalid", "Unsupported", "NotReached" });
        Assert.Contains(
            RequiredString(expected, "value"),
            new[]
            {
                "ExactInt32", "ExactString", "ExactNullableNoValue", "ExactNull", "ExactObject", "Partial",
                "Unavailable", "Conflict", "Invalid", "Unsupported", "NotReached",
            });
    }

    private static void AssertTerminalShape(JsonElement terminal, string expectedValue)
    {
        if (expectedValue.StartsWith("Exact", StringComparison.Ordinal))
        {
            Assert.Equal(JsonValueKind.String, terminal.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(terminal.GetString()));
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, terminal.ValueKind);
        }
    }

    private static void AssertValidStageProgression(JsonElement expected, string firstBoundary)
    {
        var syntax = RequiredString(expected, "syntax");
        var context = RequiredString(expected, "context");
        var symbol = RequiredString(expected, "symbol");
        var storage = RequiredString(expected, "storage");
        var value = RequiredString(expected, "value");

        if (syntax != "Exact")
        {
            Assert.Equal(
                new[] { "NotReached", "NotReached", "NotReached", "NotReached" },
                new[] { context, symbol, storage, value });
            Assert.StartsWith("syntax-", firstBoundary, StringComparison.Ordinal);
            return;
        }

        if (context != "NotRequired" && !context.StartsWith("Exact", StringComparison.Ordinal))
        {
            Assert.Equal(
                new[] { "NotReached", "NotReached", "NotReached" },
                new[] { symbol, storage, value });
            Assert.True(
                firstBoundary.StartsWith("selected-frame-", StringComparison.Ordinal) ||
                firstBoundary.StartsWith("portable-pdb-", StringComparison.Ordinal));
            return;
        }

        if (symbol != "Exact")
        {
            Assert.Equal(new[] { "NotReached", "NotReached" }, new[] { storage, value });
            Assert.True(
                firstBoundary.StartsWith("symbol-", StringComparison.Ordinal) ||
                firstBoundary.StartsWith("metadata-", StringComparison.Ordinal) ||
                firstBoundary.EndsWith("-absent", StringComparison.Ordinal));
            return;
        }

        if (storage != "Exact")
        {
            Assert.Equal("NotReached", value);
            Assert.StartsWith("static-slot-", firstBoundary, StringComparison.Ordinal);
            return;
        }

        Assert.NotEqual("NotReached", value);
        Assert.True(
            firstBoundary.StartsWith("value-", StringComparison.Ordinal) ||
            firstBoundary.StartsWith("static-value-", StringComparison.Ordinal) ||
            firstBoundary.StartsWith("reference-target-", StringComparison.Ordinal));
    }

    private static string[] ReadStrings(JsonElement array) =>
        array.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToArray();

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = string.IsNullOrEmpty(propertyName) ? element.GetString() : element.GetProperty(propertyName).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new Xunit.Sdk.XunitException(
                string.IsNullOrEmpty(propertyName)
                    ? "A required string value was missing."
                    : $"The required '{propertyName}' string was missing.")
            : value;
    }

    private static string ResolveManifestPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Interpreter.sln")))
        {
            current = current.Parent;
        }

        var root = current?.FullName
            ?? throw new DirectoryNotFoundException("The repository root could not be located from the test output.");
        return Path.Combine(root, "tests", "corpus", "w7-static-field-incidents-v1.json");
    }

    private enum DraftStage
    {
        Syntax,
        SelectedFrame,
        PdbIdentity,
        ImportScope,
        Symbol,
        Storage,
        Value,
    }

    private enum DraftOutcome
    {
        Exact,
        ExactNull,
        Partial,
        Unavailable,
        Ambiguous,
        Conflict,
        Invalid,
        Unsupported,
    }

    private sealed record DraftPipelineResult(string Name, DraftStage Stage, DraftOutcome Outcome);

    private sealed record DraftVector(
        string Name,
        bool RequiresContext,
        IReadOnlyDictionary<DraftStage, DraftOutcome> Outcomes,
        DraftStage TerminalStage,
        DraftOutcome TerminalOutcome,
        DraftStage[] ExpectedCalls)
    {
        internal static DraftVector QualifiedWithPoisonedContext() => new(
            "qualified-context-poison",
            RequiresContext: false,
            CreateOutcomes(
                (DraftStage.SelectedFrame, DraftOutcome.Conflict),
                (DraftStage.PdbIdentity, DraftOutcome.Invalid),
                (DraftStage.ImportScope, DraftOutcome.Ambiguous)),
            DraftStage.Value,
            DraftOutcome.Exact,
            [DraftStage.Syntax, DraftStage.Symbol, DraftStage.Storage, DraftStage.Value]);

        internal static DraftVector ContextualExact() => new(
            "contextual-exact",
            RequiresContext: true,
            CreateOutcomes(),
            DraftStage.Value,
            DraftOutcome.Exact,
            Enum.GetValues<DraftStage>());

        internal static DraftVector Stopping(
            string name,
            DraftStage terminalStage,
            DraftOutcome terminalOutcome,
            bool requiresContext = true)
        {
            var calls = Enum.GetValues<DraftStage>()
                .Where(stage => requiresContext || stage is not (
                    DraftStage.SelectedFrame or DraftStage.PdbIdentity or DraftStage.ImportScope))
                .TakeWhile(stage => stage <= terminalStage)
                .ToArray();
            return new DraftVector(
                name,
                requiresContext,
                CreateOutcomes((terminalStage, terminalOutcome)),
                terminalStage,
                terminalOutcome,
                calls);
        }

        private static IReadOnlyDictionary<DraftStage, DraftOutcome> CreateOutcomes(
            params (DraftStage Stage, DraftOutcome Outcome)[] overrides)
        {
            var outcomes = Enum.GetValues<DraftStage>()
                .ToDictionary(static stage => stage, static _ => DraftOutcome.Exact);
            foreach (var (stage, outcome) in overrides)
            {
                outcomes[stage] = outcome;
            }

            return outcomes;
        }
    }

    private sealed class DraftCapabilityProbe
    {
        private readonly IReadOnlyDictionary<DraftStage, DraftOutcome> _outcomes;

        internal DraftCapabilityProbe(
            string name,
            IReadOnlyDictionary<DraftStage, DraftOutcome> outcomes)
        {
            Name = name;
            _outcomes = outcomes;
        }

        internal string Name { get; }

        internal List<DraftStage> Calls { get; } = [];

        internal DraftOutcome Observe(DraftStage stage)
        {
            Calls.Add(stage);
            return _outcomes[stage];
        }
    }
}
