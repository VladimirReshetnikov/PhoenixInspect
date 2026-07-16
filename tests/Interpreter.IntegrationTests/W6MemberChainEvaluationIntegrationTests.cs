using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpDebugging;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises W6.5 frozen-plan evaluation over complex synthetic dump graphs without parser, declaration, or catalog
/// re-entry.
/// </summary>
public sealed class W6MemberChainEvaluationIntegrationTests
{
    /// <summary>
    /// Decodes exact direct/conditional string, Int32, and nullable Int32 terminals through descriptor-only adapter
    /// operations, retains property storage provenance, and reproduces result replay without declaration rebinding.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainEvaluationV1")]
    public void Exact_non_null_graphs_use_each_frozen_decoder_and_never_rebind_declarations()
    {
        CaptureGraph(
            "--synthetic-certificate-profiles",
            "SyntheticCertificateProfileProbe",
            "failed",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);

                var textPlan = Prepare(session, binding, "root.Direct.Text");
                var textSource = new CountingEvaluationSource(session);
                var text = DumpMemberChainEngine.Evaluate(textSource, textPlan);
                AssertExactString(text, "failed");
                Assert.Equal(0, textSource.CertificateCalls);
                Assert.Equal(1, textSource.ReferenceCalls);
                Assert.Equal(1, textSource.TargetCalls);
                Assert.Equal(1, textSource.StorageCalls);
                Assert.Equal(1, textSource.StringCalls);
                Assert.Equal(0, textSource.Int32Calls + textSource.NullableInt32Calls);

                var countPlan = Prepare(session, binding, "root.Direct?.Count ?? 0");
                var countSource = new CountingEvaluationSource(session);
                var count = DumpMemberChainEngine.Evaluate(countSource, countPlan);
                AssertExactInt32(count, 413);
                Assert.Equal(0, countSource.CertificateCalls);
                Assert.Equal(1, countSource.Int32Calls);
                Assert.DoesNotContain(
                    count.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");

                var nullablePlan = Prepare(session, binding, "root.Direct.OptionalCount");
                var nullableSource = new CountingEvaluationSource(session);
                var nullable = DumpMemberChainEngine.Evaluate(nullableSource, nullablePlan);
                AssertExactNull(nullable);
                Assert.Equal(1, nullableSource.NullableInt32Calls);

                var nullableFallbackPlan = Prepare(session, binding, "root.Direct.OptionalCount ?? 0");
                var nullableFallbackSource = new CountingEvaluationSource(session);
                var nullableFallback = DumpMemberChainEngine.Evaluate(
                    nullableFallbackSource,
                    nullableFallbackPlan);
                AssertExactInt32(nullableFallback, 0);
                Assert.Contains(
                    nullableFallback.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");

                var replaySource = new CountingEvaluationSource(session);
                var replay = DumpMemberChainEngine.Evaluate(replaySource, textPlan);
                Assert.Equal(
                    EvaluationResultReplay.ComputeSha256(
                        text,
                        static value => value.ToCanonicalReplayProjection()),
                    EvaluationResultReplay.ComputeSha256(
                        replay,
                        static value => value.ToCanonicalReplayProjection()));
                Assert.Equal(0, replaySource.CertificateCalls);
            });

        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            "failed",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);
                var propertyPlan = Prepare(session, binding, "root.Failure?.Code");
                Assert.Equal(ClrmdTerminalMemberKind.CertifiedProperty, propertyPlan.Certificate.TerminalMemberKind);
                Assert.NotNull(propertyPlan.Certificate.Property);
                var source = new CountingEvaluationSource(session);
                var result = DumpMemberChainEngine.Evaluate(source, propertyPlan);
                AssertExactString(result, "request-failed");
                Assert.Equal(0, source.CertificateCalls);
                Assert.Equal(1, source.StringCalls);
                Assert.Contains(
                    result.Provenance,
                    item => item.SourceId.StartsWith(
                        "dump-member-chain-terminal-storage:sha256:",
                        StringComparison.Ordinal));
            });
    }

    /// <summary>
    /// Proves an exact null intermediate short-circuits conditional access with optional fallback before target or
    /// leaf work, while direct access remains blocked and never applies its otherwise compatible fallback.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainEvaluationV1")]
    public void Exact_null_receiver_short_circuits_conditionals_and_blocks_direct_access()
    {
        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            "clear",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);

                var conditionalPlan = Prepare(session, binding, "root.Failure?.Code");
                var conditionalSource = new CountingEvaluationSource(session, poisonAfterReference: true);
                var conditional = DumpMemberChainEngine.Evaluate(conditionalSource, conditionalPlan);
                AssertExactNull(conditional);
                AssertReferenceOnly(conditionalSource);

                var fallbackPlan = Prepare(session, binding, "root.Failure?.Code ?? \"<none>\"");
                var fallbackSource = new CountingEvaluationSource(session, poisonAfterReference: true);
                var fallback = DumpMemberChainEngine.Evaluate(fallbackSource, fallbackPlan);
                AssertExactString(fallback, "<none>");
                AssertReferenceOnly(fallbackSource);
                Assert.Contains(
                    fallback.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");

                var directPlan = Prepare(session, binding, "root.Failure.Code ?? \"<none>\"");
                var directSource = new CountingEvaluationSource(session, poisonAfterReference: true);
                var direct = DumpMemberChainEngine.Evaluate(directSource, directPlan);
                Assert.Equal(EvaluationSemanticMode.DerivedQuery, direct.SemanticMode);
                Assert.Equal(EvaluationCompletionStatus.Blocked, direct.Completion);
                Assert.Equal(EvaluationCompleteness.None, direct.Completeness);
                Assert.Equal(EvaluationEvidenceStatus.Exact, direct.Evidence);
                Assert.Equal(EvaluationEffectStatus.None, direct.Effects);
                Assert.Null(direct.Value);
                Assert.Equal("QUERY_CHAIN_NULL_RECEIVER", Assert.Single(direct.Diagnostics).Code);
                AssertReferenceOnly(directSource);
                Assert.DoesNotContain(
                    direct.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");
            });
    }

    private static void AssertReferenceOnly(CountingEvaluationSource source)
    {
        Assert.Equal(0, source.CertificateCalls);
        Assert.Equal(1, source.ReferenceCalls);
        Assert.Equal(0, source.TargetCalls);
        Assert.Equal(0, source.StorageCalls);
        Assert.Equal(0, source.StringCalls + source.Int32Calls + source.NullableInt32Calls);
    }

    private static void AssertExactString(EvaluationResult<DumpQueryValue> result, string expected)
    {
        AssertExact(result);
        Assert.Equal(DumpQueryValueKind.String, result.Value!.Kind);
        Assert.Equal(expected, result.Value.StringValue);
    }

    private static void AssertExactInt32(EvaluationResult<DumpQueryValue> result, int expected)
    {
        AssertExact(result);
        Assert.Equal(DumpQueryValueKind.Int32, result.Value!.Kind);
        Assert.Equal(expected, result.Value.Int32Value);
    }

    private static void AssertExactNull(EvaluationResult<DumpQueryValue> result)
    {
        AssertExact(result);
        Assert.Equal(DumpQueryValueKind.Null, result.Value!.Kind);
    }

    private static void AssertExact(EvaluationResult<DumpQueryValue> result)
    {
        Assert.Equal(EvaluationSemanticMode.DerivedQuery, result.SemanticMode);
        Assert.Equal(EvaluationCompletionStatus.Completed, result.Completion);
        Assert.Equal(EvaluationCompleteness.Complete, result.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Exact, result.Evidence);
        Assert.Equal(EvaluationEffectStatus.None, result.Effects);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Context.Bounds, bound => bound.Name == "dump.memory-read.bytes");
    }

    private static DumpMemberChainPlan Prepare(
        ClrmdDumpSession session,
        DumpQueryRootBinding binding,
        string expression)
    {
        var request = Classify(binding, expression);
        var preparation = DumpMemberChainPreparationFacade.Prepare(session, request);
        Assert.True(preparation.IsSuccess, preparation.Failure?.Code);
        return Assert.IsType<DumpMemberChainPlan>(preparation.Plan);
    }

    private static DumpExpressionRequest Classify(DumpQueryRootBinding binding, string expression)
    {
        var classification = DumpExpressionClassifier.Classify(
            expression,
            binding,
            DumpExpressionPolicy.Create(
                DumpMethodEvaluationMode.Interpreted,
                instructionLimit: 100,
                logicalDepthLimit: 2,
                traversalLimit: 10),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        Assert.Equal(DumpExpressionClassificationStatus.Accepted, classification.Status);
        Assert.Equal(DumpExpressionKind.FixedDepthMemberChain, classification.Kind);
        return Assert.IsType<DumpExpressionRequest>(classification.Request);
    }

    private static void CaptureGraph(
        string command,
        string rootType,
        string state,
        Action<ClrmdDumpSession, ClrmdHeapObjectInfo> assertion)
    {
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w6-evaluation-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                       TestTargetPaths.ResolveExecutable(),
                       [command, "413", "-907", state],
                       isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            using var session = OpenExact(dumpPath);
            var search = session.FindStrongHandleObjectsByTypeName(
                rootType,
                maximumMatches: 2,
                maximumHandlesScanned: 100_000);
            Assert.Equal(ClrmdEvidenceStatus.Exact, search.Status);
            assertion(session, Assert.Single(search.Matches));
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static ClrmdDumpSession OpenExact(string dumpPath)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        return Assert.IsType<ClrmdDumpSession>(opened.Value);
    }

    private sealed class CountingEvaluationSource(
        ClrmdDumpSession session,
        bool poisonAfterReference = false) : IDumpMemberChainEvidenceSource
    {
        internal int CertificateCalls { get; private set; }

        internal int ReferenceCalls { get; private set; }

        internal int TargetCalls { get; private set; }

        internal int StorageCalls { get; private set; }

        internal int StringCalls { get; private set; }

        internal int Int32Calls { get; private set; }

        internal int NullableInt32Calls { get; private set; }

        public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

        public int MaximumReadLength => session.Memory.MaximumReadLength;

        public ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> CertifyDeclaredDataMember(
            ClrmdHeapObjectInfo root,
            string referenceFieldName,
            string terminalMemberName)
        {
            CertificateCalls++;
            throw new InvalidOperationException("Evaluation attempted declaration rebinding.");
        }

        public ClrmdEvidenceResult<ClrmdObjectReferenceObservation> ReadObjectReference(
            ClrmdHeapObjectInfo root,
            ClrmdInstanceFieldInfo field)
        {
            ReferenceCalls++;
            return session.ReadObjectReference(root, field);
        }

        public ClrmdEvidenceResult<ClrmdReferencedObjectInfo> ValidateReferencedObject(
            ClrmdDeclaredDataMemberCertificate certificate,
            ClrmdObjectReferenceObservation reference)
        {
            PoisonIfShortCircuited();
            TargetCalls++;
            return session.ValidateReferencedObject(certificate, reference);
        }

        public ClrmdEvidenceResult<ClrmdInstanceFieldInfo> BindTerminalStorage(
            ClrmdDeclaredDataMemberCertificate certificate,
            ClrmdReferencedObjectInfo target)
        {
            PoisonIfShortCircuited();
            StorageCalls++;
            return session.BindTerminalStorage(certificate, target);
        }

        public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field)
        {
            PoisonIfShortCircuited();
            Int32Calls++;
            return session.ReadInt32Field(target, field);
        }

        public ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation> ReadNullableInt32Field(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field)
        {
            PoisonIfShortCircuited();
            NullableInt32Calls++;
            return session.ReadNullableInt32Field(target, field);
        }

        public ClrmdStringFieldObservation ReadStringField(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field,
            int maximumCharacters)
        {
            PoisonIfShortCircuited();
            StringCalls++;
            return session.ReadStringField(target, field, maximumCharacters);
        }

        private void PoisonIfShortCircuited()
        {
            if (poisonAfterReference)
            {
                throw new InvalidOperationException("Null short-circuit reached a poisoned target or terminal operation.");
            }
        }
    }
}
