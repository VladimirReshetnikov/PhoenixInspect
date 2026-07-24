using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Freezes the dump-free W2, W5, and W6 compatibility identities that W7 must preserve while adding a parallel
/// static-expression request, binding, plan, and result family.
/// </summary>
public sealed class W7LegacyCompatibilityGoldenTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// Freezes one meaningful W2 string query's complete canonical projection and one FrozenW5 request's exact
    /// canonical bytes, while proving both identities retain the counted strong-handle selection.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void W2_plan_and_W5_request_retain_their_exact_legacy_identities()
    {
        var fixture = CreateFixture();
        const string expression = "root.Summary ?? \"<unavailable>\"";
        var parsed = DumpQueryParser.Parse(expression, "root");
        Assert.True(parsed.IsSuccess, parsed.DiagnosticCode);
        var query = Assert.IsType<ParsedDumpQuery>(parsed.Query);
        var field = CreateField(
            fixture,
            "Summary",
            metadataToken: 0x04000001,
            address: fixture.Root.Address + 0x20,
            fieldTypeName: "System.String");
        var plan = new DumpQueryPlan(
            fixture.Binding,
            field,
            DumpQueryPlanFieldKind.String,
            query.CoalesceLiteral,
            parsed.AppliedBounds,
            ImmutableArray.Create(ClrmdDumpSession.InstanceFieldTraversalBound));

        var expectedProjection = EncodeProjection(
            "dump-query-plan-v1",
            "root",
            SnapshotDigest,
            fixture.Module.Identity.SourceId,
            Hex(fixture.Root.Address),
            Hex(fixture.Root.MethodTable),
            field.Name,
            field.MetadataToken.ToString(CultureInfo.InvariantCulture),
            Hex(field.Address),
            field.Size.ToString(CultureInfo.InvariantCulture),
            "1",
            field.ElementType,
            field.FieldTypeName!,
            field.ToCanonicalReplayProjection(),
            DumpQueryPlanFieldKind.String.ToString(),
            EvaluationSemanticMode.DerivedQuery.ToString(),
            ((int)parsed.AppliedBounds).ToString(CultureInfo.InvariantCulture),
            "2",
            "root-selection.maximum-handles",
            "4096",
            "root-selection.maximum-matches",
            "2",
            "1",
            ClrmdDumpSession.InstanceFieldTraversalBound.Name,
            ClrmdDumpSession.InstanceFieldTraversalBound.Value.ToString(CultureInfo.InvariantCulture),
            DumpQueryLiteralKind.String.ToString(),
            "<unavailable>");
        Assert.Equal(expectedProjection, plan.ToCanonicalReplayProjection());

        var policy = CreatePolicy();
        var classification = DumpExpressionClassifier.Classify(
            expression,
            fixture.Binding,
            policy,
            DumpExpressionLanguageProfile.FrozenW5);
        Assert.Equal(DumpExpressionClassificationStatus.Accepted, classification.Status);
        Assert.Equal(DumpExpressionKind.DerivedQuery, classification.Kind);
        var request = Assert.IsType<DumpExpressionRequest>(classification.Request);

        AssertGolden("W2_PLAN_SHA256", plan.ComputeSha256());
        AssertGolden("W5_REQUEST_BASE64", Convert.ToBase64String(request.CanonicalBytes.AsSpan()));
        AssertGolden("W5_REQUEST_SHA256", request.Sha256);
    }

    /// <summary>
    /// Freezes the exact W6 request and expression bytes plus the detached member-chain plan projection and digest,
    /// including its declaration certificate, relative storage, preparation evidence, and unread-value invariants.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void W6_request_expression_and_plan_retain_their_exact_legacy_identities()
    {
        var fixture = CreateFixture();
        const string expression = "root.Failure?.Code ?? \"<none>\"";
        var classification = DumpExpressionClassifier.Classify(
            expression,
            fixture.Binding,
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        Assert.Equal(DumpExpressionClassificationStatus.Accepted, classification.Status);
        Assert.Equal(DumpExpressionKind.FixedDepthMemberChain, classification.Kind);
        var request = Assert.IsType<DumpExpressionRequest>(classification.Request);
        var expressionIdentity = Assert.IsType<DumpMemberChainExpressionIdentity>(
            request.MemberChainExpressionIdentity);
        var certificate = CreateCertificate(fixture);
        var preparationEvidence = ImmutableArray.Create(MemoryReadResult.Create(
            fixture.Root.Snapshot.MemorySourceId,
            fixture.Module.MetadataAddress,
            requestedLength: 12,
            [0x42, 0x53, 0x4a, 0x42, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00]));
        var preparationBounds = ImmutableArray.Create(
            new EvaluationDeterministicBound("module-metadata.maximum-bytes", 536_870_912),
            new EvaluationDeterministicBound("type-declaration.maximum-fields", 1_024));
        var plan = new DumpMemberChainPlan(
            fixture.Binding,
            certificate,
            DumpMemberChainAccessKind.Conditional,
            DumpMemberChainFallbackKind.String,
            int32Fallback: null,
            stringFallback: "<none>",
            request.CanonicalBytes,
            request.Sha256,
            expressionIdentity.CanonicalBytes,
            expressionIdentity.Sha256,
            request.ReachedBounds,
            preparationEvidence,
            preparationBounds);

        var expectedProjection = EncodeProjection(
            "dump-member-chain-plan-v1",
            Convert.ToHexString(request.CanonicalBytes.AsSpan()),
            request.Sha256,
            Convert.ToHexString(expressionIdentity.CanonicalBytes.AsSpan()),
            expressionIdentity.Sha256,
            certificate.ToCanonicalReplayProjection(),
            DumpMemberChainAccessKind.Conditional.ToString(),
            DumpMemberChainFallbackKind.String.ToString(),
            "none",
            "value",
            "<none>",
            EvaluationSemanticMode.DerivedQuery.ToString(),
            "reference-and-terminal-values-not-read-during-preparation",
            request.ReachedBounds.Length.ToString(CultureInfo.InvariantCulture),
            request.ReachedBounds.SelectMany(static bound => new[]
            {
                bound.Name,
                bound.Value.ToString(CultureInfo.InvariantCulture),
            }),
            preparationEvidence.Length.ToString(CultureInfo.InvariantCulture),
            preparationEvidence.SelectMany(static read => new[]
            {
                read.SourceId,
                Hex(read.Address),
                read.RequestedLength.ToString(CultureInfo.InvariantCulture),
                read.Status.ToString(),
                Convert.ToHexString(read.Bytes.AsSpan()),
            }),
            preparationBounds.Length.ToString(CultureInfo.InvariantCulture),
            preparationBounds.SelectMany(static bound => new[]
            {
                bound.Name,
                bound.Value.ToString(CultureInfo.InvariantCulture),
            }));
        Assert.Equal(expectedProjection, plan.ToCanonicalReplayProjection());
        Assert.False(plan.ReferenceValueReadDuringPreparation);
        Assert.False(plan.TerminalValueReadDuringPreparation);
        Assert.Equal(request.Sha256, plan.RequestSha256);
        Assert.Equal(expressionIdentity.Sha256, plan.ExpressionIdentitySha256);

        AssertGolden("W6_REQUEST_BASE64", Convert.ToBase64String(request.CanonicalBytes.AsSpan()));
        AssertGolden("W6_REQUEST_SHA256", request.Sha256);
        AssertGolden(
            "W6_EXPRESSION_BASE64",
            Convert.ToBase64String(expressionIdentity.CanonicalBytes.AsSpan()));
        AssertGolden("W6_EXPRESSION_SHA256", expressionIdentity.Sha256);
        AssertGolden("W6_PLAN_SHA256", plan.Sha256);
    }

    /// <summary>
    /// Freezes the exact strong-handle root-selection counters, evidence, applied bounds, and canonical policy
    /// provenance used by legacy W2/W5/W6 results.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Strong_handle_selection_retains_counters_evidence_and_canonical_provenance()
    {
        var fixture = CreateFixture();
        var binding = fixture.Binding;
        Assert.Equal(DumpQueryRootBindingStatus.ExactObject, binding.Status);
        Assert.Same(fixture.Root, binding.Root);
        Assert.Equal("Synthetic.Legacy.RequestPipeline", binding.TypeNameSelector);
        Assert.Equal(ClrmdEvidenceStatus.Exact, binding.SearchStatus);
        Assert.Equal(37, binding.HandlesScanned);
        Assert.Equal(4096, binding.MaximumHandlesScanned);
        Assert.Equal(2, binding.MaximumMatches);
        Assert.Equal(1, binding.MatchesRetained);
        Assert.False(binding.MatchLimitReached);
        Assert.Equal(fixture.Root.Evidence.ToArray(), binding.Evidence.ToArray());
        Assert.Equal(
            new[] { "root-selection.maximum-handles", "root-selection.maximum-matches" },
            binding.AppliedBounds.Select(static bound => bound.Name).ToArray());
        Assert.Equal(new long[] { 4096, 2 }, binding.AppliedBounds.Select(static bound => bound.Value).ToArray());

        var provenance = Assert.IsType<EvaluationProvenance>(
            DumpQueryEngine.CreateRootSelectionProvenance(binding));
        Assert.Equal(EvaluationProvenanceKind.Policy, provenance.Kind);
        Assert.Null(provenance.Address);
        Assert.Null(provenance.RequestedLength);
        Assert.Null(provenance.ObservedLength);
        var expectedCanonical = EncodeProjection(
            "dump-query-root-selection-v1",
            binding.TypeNameSelector!,
            ((int)binding.SearchStatus!.Value).ToString(CultureInfo.InvariantCulture),
            ((int)binding.Status).ToString(CultureInfo.InvariantCulture),
            ((int)binding.Issue).ToString(CultureInfo.InvariantCulture),
            binding.HandlesScanned!.Value.ToString(CultureInfo.InvariantCulture),
            binding.MaximumHandlesScanned!.Value.ToString(CultureInfo.InvariantCulture),
            binding.MaximumMatches!.Value.ToString(CultureInfo.InvariantCulture),
            binding.MatchesRetained!.Value.ToString(CultureInfo.InvariantCulture),
            binding.MatchLimitReached!.Value ? "1" : "0");
        var expectedSourceId = $"dump-query-root-selection:sha256:{Hash(expectedCanonical)}";
        Assert.Equal(expectedSourceId, provenance.SourceId);
        AssertGolden("ROOT_SELECTION_SOURCE_ID", provenance.SourceId);
    }

    /// <summary>
    /// Reconstructs all five modeled incident axes without a dump and proves the explicit schema-v1 compatibility
    /// projection remains byte-identical to its existing named golden, including a separately frozen digest.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Modeled_incident_schema_v1_projection_remains_byte_exact()
    {
        var measurements = ModeledIncidentContextCorpus.Axes.Select(definition =>
        {
            var rawContext = definition.Axis switch
            {
                ModeledIncidentAxis.Static => RawContextAttributionKind.ExactStaticField,
                ModeledIncidentAxis.StrongRoot => RawContextAttributionKind.ExactStrongHandle,
                _ => RawContextAttributionKind.UnavailableStackSlotObservationNotAdmitted,
            };
            var product = definition.Axis == ModeledIncidentAxis.StrongRoot
                ? ProductQueryObservationKind.Exact
                : ProductQueryObservationKind.Unavailable;
            return new ModeledIncidentAxisMeasurement(
                definition,
                RawSelectionObservationKind.Unique,
                RawMemberBytesObservationKind.Exact,
                rawContext,
                product,
                product == ProductQueryObservationKind.Exact ? "none" : "QUERY_ROOT_UNAVAILABLE");
        }).ToImmutableArray();
        var report = new ModeledIncidentContextReport(measurements);
        var actualBytes = Encoding.UTF8.GetBytes(report.ToCanonicalText());
        var expectedField = typeof(OptimizedIncidentContextIntegrationTests).GetField(
            "ExpectedV1CanonicalReport",
            BindingFlags.NonPublic | BindingFlags.Static);
        var expectedText = Assert.IsType<string>(expectedField?.GetRawConstantValue());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedText);

        Assert.Equal(expectedBytes, actualBytes);
        AssertGolden("MODELED_V1_SHA256", Hash(actualBytes));
    }

    private static LegacyFixture CreateFixture()
    {
        var snapshot = new ClrmdSnapshotIdentity(SnapshotDigest);
        var runtimeModule = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: 0x1010,
            ModuleAddress: 0x2020,
            ImageBase: 0x400000,
            ImageSize: 0x9000);
        var module = new ClrmdModuleInfo(
            runtimeModule,
            "Synthetic.Legacy.dll",
            targetPathHint: null,
            appDomainId: 7,
            metadataAddress: 0x500000,
            metadataLength: 0x3000,
            layout: "Mapped");
        const ulong rootAddress = 0x700000;
        const ulong rootSlot = 0x720000;
        const ulong methodTable = 0x710000;
        var evidence = ImmutableArray.Create(
            MemoryReadResult.Create(
                snapshot.MemorySourceId,
                rootSlot,
                requestedLength: sizeof(ulong),
                [0x00, 0x00, 0x70, 0x00, 0x00, 0x00, 0x00, 0x00]),
            MemoryReadResult.Create(
                snapshot.MemorySourceId,
                rootAddress,
                requestedLength: sizeof(ulong),
                [0x00, 0x00, 0x71, 0x00, 0x00, 0x00, 0x00, 0x00]));
        var root = new ClrmdHeapObjectInfo(
            snapshot,
            rootAddress,
            "Synthetic.Legacy.RequestPipeline",
            typeMetadataToken: 0x02000002,
            methodTable,
            rootSlot,
            "Strong",
            module,
            evidence);
        var search = new ClrmdHeapObjectSearchResult(
            snapshot,
            root.TypeName,
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            handlesScanned: 37,
            maximumHandlesScanned: 4096,
            maximumMatches: 2,
            matchLimitReached: false,
            ImmutableArray.Create(root),
            evidence);
        return new LegacyFixture(module, root, DumpQueryRootBinding.FromSearchResult("root", search));
    }

    private static ClrmdDeclaredDataMemberCertificate CreateCertificate(LegacyFixture fixture)
    {
        var outerField = CreateField(
            fixture,
            "Failure",
            metadataToken: 0x04000002,
            address: fixture.Root.Address + 0x28,
            fieldTypeName: "Synthetic.Legacy.FailureDetail",
            elementType: "Object");
        var moduleContent = ModuleContentIdentity.FromMetadata(
            Guid.Parse("12345678-1234-5678-9abc-def012345678"),
            [0x42, 0x53, 0x4a, 0x42, 0x01, 0x00, 0x01, 0x00, 0x0c, 0x00, 0x00, 0x00]);
        var declaredTarget = new ClrmdDeclaredTypeInfo(
            fixture.Module.Identity,
            moduleContent,
            metadataToken: 0x02000003,
            "Synthetic.Legacy.FailureDetail");
        var storage = new ClrmdRelativeFieldInfo(
            metadataToken: 0x04000005,
            "Code",
            offsetFromObject: 0x18,
            size: sizeof(ulong),
            isObjectReference: true,
            elementType: "String",
            fieldTypeName: "System.String",
            signature: ImmutableArray.Create<byte>(0x06, 0x0e),
            nullableInt32Layout: null);
        return new ClrmdDeclaredDataMemberCertificate(
            fixture.Root.TypeName,
            fixture.Root.TypeMetadataToken,
            outerField,
            outerFieldSignature: ImmutableArray.Create<byte>(0x06, 0x12, 0x0c),
            declaredTarget,
            terminalMemberName: "Code",
            ClrmdTerminalMemberKind.Field,
            terminalMemberToken: storage.MetadataToken,
            ClrmdTerminalDecoderKind.String,
            storage,
            property: null);
    }

    private static ClrmdInstanceFieldInfo CreateField(
        LegacyFixture fixture,
        string name,
        int metadataToken,
        ulong address,
        string fieldTypeName,
        string elementType = "String") => new(
        fixture.Root.Snapshot,
        fixture.Root.Address,
        fixture.Root.MethodTable,
        fixture.Root.TypeName,
        name,
        metadataToken,
        address,
        size: sizeof(ulong),
        isObjectReference: true,
        elementType,
        fieldTypeName,
        nullableInt32Layout: null);

    private static DumpExpressionPolicy CreatePolicy() => DumpExpressionPolicy.Create(
        DumpMethodEvaluationMode.Interpreted,
        instructionLimit: 1_024,
        logicalDepthLimit: 4,
        traversalLimit: 256);

    private static string EncodeProjection(params object[] parts)
    {
        var values = parts.SelectMany(static part => part switch
        {
            string value => [value],
            IEnumerable<string> values => values,
            _ => throw new ArgumentException("Canonical projection parts must be strings or string sequences."),
        });
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            foreach (var character in value)
            {
                builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string Hex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);

    private static void AssertGolden(string name, string actual)
    {
        var expected = name switch
        {
            "W2_PLAN_SHA256" => "fe7ef99d1c6e1281b9aa22a4339b807a84fef61dbc5ad912731e9d2b5e912761",
            "W5_REQUEST_BASE64" =>
                "AAAAGnc1LWR1bXAtZXhwcmVzc2lvbi1yZXF1ZXN0AAAAAQAAAAEAAAAfcm9vdC5TdW1tYXJ5ID8/ICI8dW5hdmFpbGFibGU+IgAAAAEAAAABAAAAAQAAAARyb290ASNFZ4mrze8BI0VniavN7wEjRWeJq83vASNFZ4mrze8AAAAAAAAAAAAAAAAAcAAAAAAAIFN5bnRoZXRpYy5MZWdhY3kuUmVxdWVzdFBpcGVsaW5lAgAAAgAAAAAAcQAAAAAAAAByAAAAAAAGU3Ryb25nAAAAlGNscm1kLW1vZHVsZTp2MTowMTIzNDU2Nzg5YWJjZGVmMDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWYwMTIzNDU2Nzg5YWJjZGVmOjAwMDAwMDAwMDAwMDEwMTA6MDAwMDAwMDAwMDAwMjAyMDowMDAwMDAwMDAwNDAwMDAwOjAwMDAwMDAwMDAwMDkwMDAAAAABAAAAIFN5bnRoZXRpYy5MZWdhY3kuUmVxdWVzdFBpcGVsaW5lAAAAAQAAAAAAAAABAAAAJQAAAAEAABAAAAAAAQAAAAIAAAABAAAAAQAAAAEAAAAAAAAAAgAAAB5yb290LXNlbGVjdGlvbi5tYXhpbXVtLWhhbmRsZXMAAAAAAAAQAAAAAB5yb290LXNlbGVjdGlvbi5tYXhpbXVtLW1hdGNoZXMAAAAAAAAAAgAAAAIAAABMZHVtcC1zaGEyNTY6MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWYwMTIzNDU2Nzg5YWJjZGVmMDEyMzQ1Njc4OWFiY2RlZgAAAAAAcgAAAAAACAAAAAAAAAAIAABwAAAAAAAAAABMZHVtcC1zaGEyNTY6MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWYwMTIzNDU2Nzg5YWJjZGVmMDEyMzQ1Njc4OWFiY2RlZgAAAAAAcAAAAAAACAAAAAAAAAAIAABxAAAAAAAAAAAEAAAAG3F1ZXJ5LmV4cHJlc3Npb24uY2hhcmFjdGVycwAAAAAAAAIAAAAAG3F1ZXJ5LmZpZWxkLW5hbWUuY2hhcmFjdGVycwAAAAAAAABAAAAAGnF1ZXJ5LnJvb3QtbmFtZS5jaGFyYWN0ZXJzAAAAAAAAAEAAAAAfcXVlcnkuc3RyaW5nLWxpdGVyYWwuY2hhcmFjdGVycwAAAAAAAAEAAAAAygAAABl3NS1kdW1wLWV4cHJlc3Npb24tcG9saWN5AAAAAQAAAAEAAAAAAAAEAAAAAAQAAAEAAAAAGnBvbGljeS5jb3VudGVyZmFjdHVhbC5kdW1wAAAAAQAAAAAAAAAAAAAAG2NhdGFsb2cuY291bnRlcmZhY3R1YWwuZHVtcAAAAAEAAAAAAAAAAAAAAAIAAAAQYXNzdW1lLnJlYWQtb25seQAAACRhc3N1bWUuY291bnRlcmZhY3R1YWwtbm90LWhpc3RvcmljYWwAAAAA",
            "W5_REQUEST_SHA256" => "d1464fab8cc933b3a9c6ae3ae23b1b4218489e36928c4123625fda09f758b3fd",
            "W6_REQUEST_BASE64" =>
                "AAAAGnc2LWR1bXAtZXhwcmVzc2lvbi1yZXF1ZXN0AAAAAQAAAAIAAAAYUm9zbHluQ1NoYXJwRXhwcmVzc2lvblYxAAAAAQAAAB5yb290LkZhaWx1cmU/LkNvZGUgPz8gIjxub25lPiIAAAABAAAAAwAAAAEAAAAEcm9vdAEjRWeJq83vASNFZ4mrze8BI0VniavN7wEjRWeJq83vAAAAAAAAAAAAAAAAAHAAAAAAACBTeW50aGV0aWMuTGVnYWN5LlJlcXVlc3RQaXBlbGluZQIAAAIAAAAAAHEAAAAAAAAAcgAAAAAABlN0cm9uZwAAAJRjbHJtZC1tb2R1bGU6djE6MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWYwMTIzNDU2Nzg5YWJjZGVmMDEyMzQ1Njc4OWFiY2RlZjowMDAwMDAwMDAwMDAxMDEwOjAwMDAwMDAwMDAwMDIwMjA6MDAwMDAwMDAwMDQwMDAwMDowMDAwMDAwMDAwMDA5MDAwAAAAAQAAACBTeW50aGV0aWMuTGVnYWN5LlJlcXVlc3RQaXBlbGluZQAAAAEAAAAAAAAAAQAAACUAAAABAAAQAAAAAAEAAAACAAAAAQAAAAEAAAABAAAAAAAAAAIAAAAecm9vdC1zZWxlY3Rpb24ubWF4aW11bS1oYW5kbGVzAAAAAAAAEAAAAAAecm9vdC1zZWxlY3Rpb24ubWF4aW11bS1tYXRjaGVzAAAAAAAAAAIAAAACAAAATGR1bXAtc2hhMjU2OjAxMjM0NTY3ODlhYmNkZWYwMTIzNDU2Nzg5YWJjZGVmMDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWYAAAAAAHIAAAAAAAgAAAAAAAAACAAAcAAAAAAAAAAATGR1bXAtc2hhMjU2OjAxMjM0NTY3ODlhYmNkZWYwMTIzNDU2Nzg5YWJjZGVmMDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWYAAAAAAHAAAAAAAAgAAAAAAAAACAAAcQAAAAAAAAAABgAAABtxdWVyeS5leHByZXNzaW9uLmNoYXJhY3RlcnMAAAAAAAACAAAAABtxdWVyeS5maWVsZC1uYW1lLmNoYXJhY3RlcnMAAAAAAAAAQAAAABpxdWVyeS5yb290LW5hbWUuY2hhcmFjdGVycwAAAAAAAABAAAAAH3F1ZXJ5LnN0cmluZy1saXRlcmFsLmNoYXJhY3RlcnMAAAAAAAABAAAAABJxdWVyeS5zeW50YXguZGVwdGgAAAAAAAAAQAAAAB5xdWVyeS5zeW50YXgubm9kZXMtcGx1cy10b2tlbnMAAAAAAAABAAAAAMoAAAAZdzUtZHVtcC1leHByZXNzaW9uLXBvbGljeQAAAAEAAAABAAAAAAAABAAAAAAEAAABAAAAABpwb2xpY3kuY291bnRlcmZhY3R1YWwuZHVtcAAAAAEAAAAAAAAAAAAAABtjYXRhbG9nLmNvdW50ZXJmYWN0dWFsLmR1bXAAAAABAAAAAAAAAAAAAAACAAAAEGFzc3VtZS5yZWFkLW9ubHkAAAAkYXNzdW1lLmNvdW50ZXJmYWN0dWFsLW5vdC1oaXN0b3JpY2FsAAAAAAAAAAEAAAD3AAAAH3c2LWR1bXAtbWVtYmVyLWNoYWluLWV4cHJlc3Npb24AAAABAAAAGFJvc2x5bkNTaGFycEV4cHJlc3Npb25WMQAAAB1NaWNyb3NvZnQuQ29kZUFuYWx5c2lzLkNTaGFycAAAAAU1LjMuMAAAAAhDU2hhcnAxNAAAAgAAAAEAAAAAQAAAAEAAAAEAAAAAF0ZpeGVkRGVwdGhNZW1iZXJDaGFpblYxAAAAHnJvb3QuRmFpbHVyZT8uQ29kZSA/PyAiPG5vbmU+IgAAAARyb290AAAAB0ZhaWx1cmUAAAAEQ29kZQAAAAIAAAADAAAABjxub25lPg==",
            "W6_REQUEST_SHA256" => "d7e11e0b3b1e7f98bea516df02405bd7fa7bf1183ccbc704071df4ba83e9a430",
            "W6_EXPRESSION_BASE64" =>
                "AAAAH3c2LWR1bXAtbWVtYmVyLWNoYWluLWV4cHJlc3Npb24AAAABAAAAGFJvc2x5bkNTaGFycEV4cHJlc3Npb25WMQAAAB1NaWNyb3NvZnQuQ29kZUFuYWx5c2lzLkNTaGFycAAAAAU1LjMuMAAAAAhDU2hhcnAxNAAAAgAAAAEAAAAAQAAAAEAAAAEAAAAAF0ZpeGVkRGVwdGhNZW1iZXJDaGFpblYxAAAAHnJvb3QuRmFpbHVyZT8uQ29kZSA/PyAiPG5vbmU+IgAAAARyb290AAAAB0ZhaWx1cmUAAAAEQ29kZQAAAAIAAAADAAAABjxub25lPg==",
            "W6_EXPRESSION_SHA256" => "a29ff54560f674d0b9c5cf209d5a7276c8760574b0401ebe3e53db9f1246fccb",
            "W6_PLAN_SHA256" => "3262ec820c0c18004e38a13b55e98b958cde7c087e7d40c481961446766dcbb9",
            "ROOT_SELECTION_SOURCE_ID" =>
                "dump-query-root-selection:sha256:c85e4dd0b0622c247c04c34a233824ed6496967c05eb88b4eacd13b3df921fde",
            "MODELED_V1_SHA256" => "2ac2135c510b1467bccdb7e619fa9c2a874285db0847330c633a8e87fcc5cdc5",
            _ => throw new Xunit.Sdk.XunitException($"{name}={actual}"),
        };
        Assert.Equal(expected, actual);
    }

    private sealed record LegacyFixture(
        ClrmdModuleInfo Module,
        ClrmdHeapObjectInfo Root,
        DumpQueryRootBinding Binding);
}
