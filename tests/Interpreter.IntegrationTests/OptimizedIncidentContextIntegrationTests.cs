using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;
using Xunit.Abstractions;

namespace Interpreter.IntegrationTests;

internal static class OptimizedContextTestTargetPaths
{
    internal static string ResolveExecutable()
    {
        var testsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Interpreter.OptimizedContextTestTarget.exe"
            : "Interpreter.OptimizedContextTestTarget";

        return Path.Combine(
            testsRoot,
            "Interpreter.OptimizedContextTestTarget",
            "bin",
            "Release",
            targetFramework,
            executableName);
    }
}

/// <summary>
/// Measures raw and product-level discoverability for a source-controlled optimized Release incident model.
/// </summary>
/// <remarks>
/// This suite reports evidence from one generated fixture dump. It is deliberately not represented as evidence from
/// private production incidents. Its five axes stay in every denominator even when a context cannot be attributed or
/// supplied to the current strong-root-only query surface.
/// </remarks>
public sealed class OptimizedIncidentContextIntegrationTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Creates the suite with a payload-free output channel for the versioned canonical measurement report.
    /// </summary>
    /// <param name="output">The xUnit output channel that receives only aggregate and classified outcomes.</param>
    public OptimizedIncidentContextIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Captures a full dump while optimized <c>this</c>, argument, local, static, and explicit strong-root probes are
    /// live; measures raw ClrMD value/context visibility independently; and records typed product query outcomes.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "ModeledIncidentContextV1")]
    public void Optimized_release_dump_reports_all_predeclared_context_axes_without_dropping_unavailable_cases()
    {
        var targetExecutable = OptimizedContextTestTargetPaths.ResolveExecutable();
        Assert.True(
            File.Exists(targetExecutable),
            $"Expected the optimized Release context target at '{targetExecutable}'.");
        Assert.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            targetExecutable,
            StringComparison.OrdinalIgnoreCase);

        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"modeled-incident-context-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(targetExecutable))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var raw = OptimizedContextRawClrmdObserver.Observe(dumpPath);
            Assert.Equal(ModeledIncidentContextCorpus.Axes.Length, raw.Length);

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            Assert.Equal(ClrmdValueIssue.None, opened.Issue);
            using var session = opened.Value
                ?? throw new InvalidOperationException("The exact modeled-incident dump open carried no session.");
            Assert.Equal("Windows", session.TargetPlatform, ignoreCase: true);
            Assert.Equal("X64", session.TargetArchitecture, ignoreCase: true);

            var strongDefinition = ModeledIncidentContextCorpus.Axes.Single(
                static definition => definition.Axis == ModeledIncidentAxis.StrongRoot);
            var strongSearch = session.FindStrongHandleObjectsByTypeName(
                strongDefinition.RuntimeTypeName,
                maximumMatches: 8,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, strongSearch.Status);
            Assert.Equal(ClrmdValueIssue.None, strongSearch.Issue);
            var strongRoot = Assert.Single(strongSearch.Matches);

            var measurements = ImmutableArray.CreateBuilder<ModeledIncidentAxisMeasurement>(raw.Length);
            foreach (var observation in raw)
            {
                var query = DumpQueryEngine.Evaluate(
                    session,
                    "root.Marker",
                    "root",
                    observation.Definition.Axis == ModeledIncidentAxis.StrongRoot ? strongRoot : null);
                measurements.Add(CreateMeasurement(observation, query));
            }

            var report = new ModeledIncidentContextReport(measurements.MoveToImmutable());
            var canonical = report.ToCanonicalText();
            _output.WriteLine(canonical);

            Assert.Equal(5, report.RawMemberBytesNumerator);
            Assert.Equal(5, report.RawMemberBytesDenominator);
            Assert.Equal(1, report.RawContextNumerator);
            Assert.Equal(5, report.RawContextDenominator);
            Assert.Equal(1, report.ProductQueryNumerator);
            Assert.Equal(5, report.ProductQueryDenominator);

            Assert.Equal(ExpectedCanonicalReport, canonical);
            Assert.DoesNotContain('%', canonical);
            Assert.DoesNotContain(dumpPath, canonical, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(targetExecutable, canonical, StringComparison.OrdinalIgnoreCase);
            foreach (var definition in ModeledIncidentContextCorpus.Axes)
            {
                Assert.DoesNotContain(
                    definition.ExpectedMarker.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    canonical,
                    StringComparison.Ordinal);
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static ModeledIncidentAxisMeasurement CreateMeasurement(
        RawModeledIncidentObservation raw,
        EvaluationResult<DumpQueryValue> query)
    {
        Assert.Equal(EvaluationSemanticMode.DerivedQuery, query.SemanticMode);
        Assert.Equal(EvaluationEffectStatus.None, query.Effects);

        if (raw.Definition.Axis == ModeledIncidentAxis.StrongRoot)
        {
            Assert.Equal(EvaluationCompletionStatus.Completed, query.Completion);
            Assert.Equal(EvaluationCompleteness.Complete, query.Completeness);
            Assert.Equal(EvaluationEvidenceStatus.Exact, query.Evidence);
            Assert.NotNull(query.Value);
            Assert.Equal(DumpQueryValueKind.Int32, query.Value.Kind);
            Assert.Equal(raw.Definition.ExpectedMarker, query.Value.Int32Value);
            Assert.Empty(query.Diagnostics);
            return new ModeledIncidentAxisMeasurement(
                raw.Definition,
                raw.RawSelection,
                raw.RawMemberBytes,
                raw.RawContext,
                ProductQueryObservationKind.Exact,
                "none");
        }

        Assert.Equal(EvaluationCompletionStatus.Blocked, query.Completion);
        Assert.Equal(EvaluationCompleteness.None, query.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Unavailable, query.Evidence);
        Assert.Null(query.Value);
        var diagnostic = Assert.Single(query.Diagnostics);
        Assert.Equal("QUERY_ROOT_UNAVAILABLE", diagnostic.Code);
        return new ModeledIncidentAxisMeasurement(
            raw.Definition,
            raw.RawSelection,
            raw.RawMemberBytes,
            raw.RawContext,
            ProductQueryObservationKind.Unavailable,
            diagnostic.Code);
    }

    private const string ExpectedCanonicalReport =
        "schema=interpreter-modeled-incident-context-report/v1\n" +
        "corpus=generated-optimized-release-full-dump\n" +
        "scope=modeled-incident-not-private-production\n" +
        "corpus-composition=one-generated-dump-five-predeclared-axes\n" +
        "target-profile=net10.0-coreclr-windows-x64-release-optimized\n" +
        "capture-mechanism=diagnostics-client-full-dump\n" +
        "raw-stack-slot-observation=not-admitted-dotnet10-dac-safety\n" +
        "raw-member-bytes-numerator=5\n" +
        "raw-member-bytes-denominator=5\n" +
        "raw-context-attribution-numerator=1\n" +
        "raw-context-attribution-denominator=5\n" +
        "product-query-availability-numerator=1\n" +
        "product-query-availability-denominator=5\n" +
        "raw-context-this-numerator=0\n" +
        "raw-context-this-denominator=1\n" +
        "raw-context-argument-numerator=0\n" +
        "raw-context-argument-denominator=1\n" +
        "raw-context-local-numerator=0\n" +
        "raw-context-local-denominator=1\n" +
        "raw-context-static-numerator=0\n" +
        "raw-context-static-denominator=1\n" +
        "raw-context-strong-root-numerator=1\n" +
        "raw-context-strong-root-denominator=1\n" +
        "axis=this;selection=unique;member-bytes=exact;" +
        "raw-context=unavailable-stack-slot-observation-not-admitted;" +
        "product-query=unavailable;diagnostic=QUERY_ROOT_UNAVAILABLE\n" +
        "axis=argument;selection=unique;member-bytes=exact;" +
        "raw-context=unavailable-stack-slot-observation-not-admitted;" +
        "product-query=unavailable;diagnostic=QUERY_ROOT_UNAVAILABLE\n" +
        "axis=local;selection=unique;member-bytes=exact;" +
        "raw-context=unavailable-stack-slot-observation-not-admitted;" +
        "product-query=unavailable;diagnostic=QUERY_ROOT_UNAVAILABLE\n" +
        "axis=static;selection=unique;member-bytes=exact;" +
        "raw-context=unavailable-static-field-observation;" +
        "product-query=unavailable;diagnostic=QUERY_ROOT_UNAVAILABLE\n" +
        "axis=strong-root;selection=unique;member-bytes=exact;raw-context=exact-strong-handle;" +
        "product-query=exact;diagnostic=none\n";
}
