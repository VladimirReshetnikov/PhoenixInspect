using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises W5.1's dump-free expression classification and canonical request boundary.</summary>
public sealed class W5ExpressionClassifierTests
{
    /// <summary>Proves that the facade admits exactly the unchanged W2 form and one exact W5 method form.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Classifier_admits_the_closed_field_and_method_grammars()
    {
        var root = CreateRootBinding();
        var policy = CreatePolicy(DumpMethodEvaluationMode.Interpreted);

        var field = DumpExpressionClassifier.Classify("root.Marker", root, policy);
        var methodNamedField = DumpExpressionClassifier.Classify("root.GetMarkerSummary", root, policy);
        var method = DumpExpressionClassifier.Classify("root.GetMarkerSummary()", root, policy);

        AssertAccepted(field, DumpExpressionKind.DerivedQuery);
        Assert.Null(field.Request!.MethodExpressionIdentity);
        Assert.Equal("root.Marker", field.Request.Expression);
        Assert.Equal(
            ["query.expression.characters", "query.field-name.characters", "query.root-name.characters"],
            field.Request.ReachedBounds.Select(static bound => bound.Name));

        AssertAccepted(methodNamedField, DumpExpressionKind.DerivedQuery);
        Assert.Null(methodNamedField.Request!.MethodExpressionIdentity);

        AssertAccepted(method, DumpExpressionKind.CounterfactualMethod);
        Assert.NotNull(method.Request!.MethodExpressionIdentity);
        Assert.Equal("root", method.Request.MethodExpressionIdentity.RootName);
        Assert.Equal("root.GetMarkerSummary()", method.Request.MethodExpressionIdentity.Expression);
        Assert.Equal(1, DumpMethodExpressionIdentity.CanonicalSchemaVersion);
        Assert.Equal(64, method.Request.MethodExpressionIdentity.Sha256.Length);
    }

    /// <summary>
    /// Proves exact spelling, root, casing, punctuation, whitespace, and complete-consumption behavior for the method
    /// expression, including rejection of a supported prefix followed by any suffix.
    /// </summary>
    /// <param name="expression">A near miss outside the one admitted method expression.</param>
    [Theory]
    [Trait("Category", "Fast")]
    [InlineData("Root.GetMarkerSummary()")]
    [InlineData("root.getMarkerSummary()")]
    [InlineData("root.GetMarkerSummary ()")]
    [InlineData("root .GetMarkerSummary()")]
    [InlineData(" root.GetMarkerSummary()")]
    [InlineData("root.GetMarkerSummary() ")]
    [InlineData("root.GetMarkerSummary(1)")]
    [InlineData("root.GetMarkerSummary() + 1")]
    [InlineData("root.GetMarkerSummary().Other")]
    [InlineData("root.GetMarkerSummary()??0")]
    [InlineData("root.Marker()")]
    public void Classifier_rejects_every_non_exact_method_spelling(string expression)
    {
        var result = DumpExpressionClassifier.Classify(
            expression,
            CreateRootBinding(),
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));

        Assert.NotEqual(DumpExpressionClassificationStatus.Accepted, result.Status);
        Assert.Null(result.Kind);
        Assert.NotNull(result.Request);
        Assert.Null(result.Request.MethodExpressionIdentity);
        Assert.False(string.IsNullOrWhiteSpace(result.DiagnosticCode));
        Assert.False(string.IsNullOrWhiteSpace(result.DiagnosticMessage));
    }

    /// <summary>Proves stable invalid versus unsupported outcomes and bounded canonical issuance.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Classifier_distinguishes_invalid_unsupported_and_noncanonical_inputs()
    {
        var root = CreateRootBinding();
        var policy = CreatePolicy(DumpMethodEvaluationMode.Interpreted);

        var missing = DumpExpressionClassifier.Classify(null, root, policy);
        Assert.Equal(DumpExpressionClassificationStatus.Invalid, missing.Status);
        Assert.Equal("QUERY_EXPRESSION_REQUIRED", missing.DiagnosticCode);
        Assert.NotNull(missing.Request);
        Assert.Empty(missing.Request.ReachedBounds);

        var mismatch = DumpExpressionClassifier.Classify("other.Marker", root, policy);
        Assert.Equal(DumpExpressionClassificationStatus.Invalid, mismatch.Status);
        Assert.Equal("QUERY_ROOT_MISMATCH", mismatch.DiagnosticCode);
        Assert.NotNull(mismatch.Request);

        var expanded = DumpExpressionClassifier.Classify("root.Marker + 1", root, policy);
        Assert.Equal(DumpExpressionClassificationStatus.Unsupported, expanded.Status);
        Assert.Equal("QUERY_SYNTAX_UNSUPPORTED", expanded.DiagnosticCode);
        Assert.NotNull(expanded.Request);

        var oversized = DumpExpressionClassifier.Classify(
            new string('x', DumpExpressionRequest.MaximumExpressionCharacters + 1),
            root,
            policy);
        Assert.Equal(DumpExpressionClassificationStatus.Invalid, oversized.Status);
        Assert.Equal("QUERY_EXPRESSION_TOO_LONG", oversized.DiagnosticCode);
        Assert.Null(oversized.Request);

        var unavailableRoot = DumpQueryRootBinding.CreateUnavailable("root", root.Snapshot);
        var nonExact = DumpExpressionClassifier.Classify("root.Marker", unavailableRoot, policy);
        Assert.Equal(DumpExpressionClassificationStatus.Invalid, nonExact.Status);
        Assert.Equal("W5_ROOT_SELECTION_NOT_EXACT", nonExact.DiagnosticCode);
        Assert.Null(nonExact.Request);

        var invalidName = DumpExpressionClassifier.Classify(
            "bad-root.GetMarkerSummary()",
            CreateRootBinding("bad-root"),
            policy);
        Assert.Equal(DumpExpressionClassificationStatus.Invalid, invalidName.Status);
        Assert.Equal("QUERY_ROOT_NAME_INVALID", invalidName.DiagnosticCode);
        Assert.NotNull(invalidName.Request);

        var longName = new string('r', DumpExpressionRequest.MaximumRootNameCharacters + 1);
        var oversizedRoot = DumpExpressionClassifier.Classify(
            $"{longName}.GetMarkerSummary()",
            CreateRootBinding(longName),
            policy);
        Assert.Equal(DumpExpressionClassificationStatus.Invalid, oversizedRoot.Status);
        Assert.Equal("QUERY_ROOT_NAME_TOO_LONG", oversizedRoot.DiagnosticCode);
        Assert.Null(oversizedRoot.Request);
    }

    /// <summary>Proves that W5 syntax admission is the existing W2 parser outcome rather than a parallel grammar.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Syntax_classification_preserves_w2_diagnostics_and_reached_bounds()
    {
        var admitted = DumpQueryEngine.ClassifySyntax("root.Optional ?? \"fallback\"", "root");
        Assert.True(admitted.IsSupported);
        Assert.Null(admitted.DiagnosticCode);
        Assert.Null(admitted.DiagnosticMessage);
        Assert.Equal(
            [
                ("query.expression.characters", 512L),
                ("query.root-name.characters", 64L),
                ("query.field-name.characters", 64L),
                ("query.string-literal.characters", 256L),
            ],
            admitted.AppliedBounds.Select(static bound => (bound.Name, bound.Value)));

        var rejected = DumpQueryEngine.ClassifySyntax("root.Optional ?? true", "root");
        Assert.False(rejected.IsSupported);
        Assert.Equal("QUERY_LITERAL_UNSUPPORTED", rejected.DiagnosticCode);
        Assert.Equal(
            ["query.expression.characters", "query.root-name.characters", "query.field-name.characters"],
            rejected.AppliedBounds.Select(static bound => bound.Name));
    }

    /// <summary>
    /// Proves byte-for-byte request replay in repeated and fresh object/policy instances and discriminates every
    /// product choice that can change later routing or execution.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Canonical_request_replays_across_fresh_instances_and_discriminates_inputs()
    {
        var first = AssertAcceptedRequest(
            "root.GetMarkerSummary()",
            CreateRootBinding(),
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));
        var repeated = AssertAcceptedRequest(
            "root.GetMarkerSummary()",
            CreateRootBinding(),
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));
        var modeled = AssertAcceptedRequest(
            "root.GetMarkerSummary()",
            CreateRootBinding(),
            CreatePolicy(DumpMethodEvaluationMode.Modeled));
        var differentRoot = AssertAcceptedRequest(
            "root.GetMarkerSummary()",
            CreateRootBinding(objectAddress: 0x1001),
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));
        var field = AssertAcceptedRequest(
            "root.Marker",
            CreateRootBinding(),
            CreatePolicy(DumpMethodEvaluationMode.Interpreted));

        Assert.Equal(first.CanonicalBytes.AsSpan().ToArray(), repeated.CanonicalBytes.AsSpan().ToArray());
        Assert.Equal(first.Sha256, repeated.Sha256);
        Assert.Equal(
            first.MethodExpressionIdentity!.CanonicalBytes.AsSpan().ToArray(),
            repeated.MethodExpressionIdentity!.CanonicalBytes.AsSpan().ToArray());
        Assert.Equal(first.MethodExpressionIdentity.Sha256, repeated.MethodExpressionIdentity.Sha256);
        Assert.NotEqual(first.Sha256, modeled.Sha256);
        Assert.NotEqual(first.Sha256, differentRoot.Sha256);
        Assert.NotEqual(first.Sha256, field.Sha256);
        Assert.NotEqual(first.Policy.Sha256, modeled.Policy.Sha256);
    }

    /// <summary>Proves policy caps are enforced before a request can claim them.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Policy_rejects_unrepresentable_modes_and_limits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpExpressionPolicy.Create(
            (DumpMethodEvaluationMode)0,
            instructionLimit: 100,
            logicalDepthLimit: 2,
            traversalLimit: 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpExpressionPolicy.Create(
            DumpMethodEvaluationMode.Interpreted,
            instructionLimit: -1,
            logicalDepthLimit: 2,
            traversalLimit: 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpExpressionPolicy.Create(
            DumpMethodEvaluationMode.Interpreted,
            instructionLimit: 100,
            logicalDepthLimit: CounterfactualMethodRequest.MaximumLogicalCallDepth + 1,
            traversalLimit: 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpExpressionPolicy.Create(
            DumpMethodEvaluationMode.Interpreted,
            instructionLimit: 100,
            logicalDepthLimit: 2,
            traversalLimit: CounterfactualMethodRequest.MaximumTraversalUnits + 1));
    }

    private static DumpExpressionPolicy CreatePolicy(DumpMethodEvaluationMode mode) =>
        DumpExpressionPolicy.Create(
            mode,
            instructionLimit: 100,
            logicalDepthLimit: 2,
            traversalLimit: 10);

    private static DumpExpressionRequest AssertAcceptedRequest(
        string expression,
        DumpQueryRootBinding root,
        DumpExpressionPolicy policy)
    {
        var result = DumpExpressionClassifier.Classify(expression, root, policy);
        Assert.Equal(DumpExpressionClassificationStatus.Accepted, result.Status);
        return Assert.IsType<DumpExpressionRequest>(result.Request);
    }

    private static void AssertAccepted(DumpExpressionClassification result, DumpExpressionKind kind)
    {
        Assert.Equal(DumpExpressionClassificationStatus.Accepted, result.Status);
        Assert.Equal(kind, result.Kind);
        Assert.Equal(kind, result.Request!.AdmittedKind);
        Assert.Null(result.DiagnosticCode);
        Assert.Null(result.DiagnosticMessage);
    }

    private static DumpQueryRootBinding CreateRootBinding(ulong objectAddress = 0x1000) =>
        CreateRootBinding("root", objectAddress);

    private static DumpQueryRootBinding CreateRootBinding(string rootName, ulong objectAddress = 0x1000)
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
        var moduleIdentity = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: 0x2000,
            ModuleAddress: 0x3000,
            ImageBase: 0x4000,
            ImageSize: 0x5000);
        var module = new ClrmdModuleInfo(
            moduleIdentity,
            "PhoenixInspect.TestTarget.dll",
            targetPathHint: null,
            appDomainId: 1,
            metadataAddress: 0x6000,
            metadataLength: 0x7000,
            layout: "SyntheticMapped");
        var rootSlotRead = MemoryReadResult.Create(
            snapshot.MemorySourceId,
            address: 0x8000,
            requestedLength: sizeof(ulong),
            BitConverter.GetBytes(objectAddress));
        var methodTableRead = MemoryReadResult.Create(
            snapshot.MemorySourceId,
            objectAddress,
            requestedLength: sizeof(ulong),
            BitConverter.GetBytes(0x9000UL));
        var root = new ClrmdHeapObjectInfo(
            snapshot,
            objectAddress,
            "DumpProbe",
            typeMetadataToken: 0x02000002,
            methodTable: 0x9000,
            rootAddress: 0x8000,
            rootKind: "Strong",
            module,
            ImmutableArray.Create(rootSlotRead, methodTableRead));
        return DumpQueryRootBinding.FromExactObject(
            rootName,
            root,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("root-selection.maximum-handles", 100_000),
                new EvaluationDeterministicBound("root-selection.maximum-matches", 2)));
    }
}
