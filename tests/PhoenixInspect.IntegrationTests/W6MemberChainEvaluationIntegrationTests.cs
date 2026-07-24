using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises W6.5 frozen-plan evaluation over complex synthetic dump graphs without parser, declaration, or catalog
/// re-entry.
/// </summary>
public sealed class W6MemberChainEvaluationIntegrationTests
{
    /// <summary>
    /// Proves partial/unavailable references never become null or select a fallback, exact runtime subtypes remain an
    /// exact-evidence unsupported result, and target/storage conflict or invalid views stop before a terminal read.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainEvaluationV1")]
    public void Reference_target_and_storage_uncertainty_stop_at_their_exact_boundaries()
    {
        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            "clear",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);
                var plan = Prepare(session, binding, "root.Failure?.Code ?? \"<none>\"");

                var partialSource = new EvidenceViewSource(session, EvidenceView.ReferencePartial);
                var partial = DumpMemberChainEngine.Evaluate(partialSource, plan);
                AssertBlockedWithoutValue(partial, EvaluationEvidenceStatus.Partial);
                Assert.Equal("QUERY_CHAIN_REFERENCE_PARTIAL", Assert.Single(partial.Diagnostics).Code);
                Assert.Equal(1, partialSource.ReferenceCalls);
                Assert.Equal(0, partialSource.TargetCalls + partialSource.StorageCalls + partialSource.TerminalCalls);
                Assert.DoesNotContain(
                    partial.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");

                var unavailableSource = new EvidenceViewSource(session, EvidenceView.ReferenceUnavailable);
                var unavailable = DumpMemberChainEngine.Evaluate(unavailableSource, plan);
                AssertBlockedWithoutValue(unavailable, EvaluationEvidenceStatus.Unavailable);
                Assert.Equal("QUERY_CHAIN_REFERENCE_UNAVAILABLE", Assert.Single(unavailable.Diagnostics).Code);
                Assert.Equal(1, unavailableSource.ReferenceCalls);
                Assert.Equal(0, unavailableSource.TargetCalls + unavailableSource.StorageCalls + unavailableSource.TerminalCalls);
                Assert.DoesNotContain(
                    unavailable.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");
            });

        CaptureGraph(
            "--synthetic-certificate-profiles",
            "SyntheticCertificateProfileProbe",
            "failed",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);
                var subtypePlan = Prepare(session, binding, "root.Polymorphic.Value");
                var subtypeSource = new EvidenceViewSource(session, EvidenceView.None);
                var subtype = DumpMemberChainEngine.Evaluate(subtypeSource, subtypePlan);
                AssertBlockedWithoutValue(subtype, EvaluationEvidenceStatus.Exact);
                Assert.Equal("QUERY_CHAIN_RUNTIME_TYPE_UNSUPPORTED", Assert.Single(subtype.Diagnostics).Code);
                Assert.Equal(1, subtypeSource.TargetCalls);
                Assert.Equal(0, subtypeSource.StorageCalls + subtypeSource.TerminalCalls);

                var ordinaryPlan = Prepare(session, binding, "root.Direct.Text");
                foreach (var (view, evidence, code) in new[]
                {
                    (EvidenceView.TargetConflict, EvaluationEvidenceStatus.Conflict, "QUERY_CHAIN_TARGET_CONFLICT"),
                    (EvidenceView.TargetInvalid, EvaluationEvidenceStatus.Invalid, "QUERY_CHAIN_TARGET_INVALID"),
                    (EvidenceView.StorageConflict, EvaluationEvidenceStatus.Conflict, "QUERY_CHAIN_STORAGE_CONFLICT"),
                    (EvidenceView.StorageInvalid, EvaluationEvidenceStatus.Invalid, "QUERY_CHAIN_STORAGE_INVALID"),
                })
                {
                    var source = new EvidenceViewSource(session, view);
                    var result = DumpMemberChainEngine.Evaluate(source, ordinaryPlan);
                    Assert.Equal(EvaluationCompleteness.None, result.Completeness);
                    Assert.Equal(evidence, result.Evidence);
                    Assert.Null(result.Value);
                    Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
                    Assert.Equal(0, source.TerminalCalls);
                }
            });
    }

    /// <summary>
    /// Distinguishes a deterministic observation-cap prefix from a memory-limited prefix and complete absence, while
    /// proving a compatible string fallback is not selected for any non-exact terminal observation.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainEvaluationV1")]
    public void Partial_and_unavailable_strings_preserve_prefix_completion_without_coalescing()
    {
        CaptureGraph(
            "--synthetic-certificate-profiles",
            "SyntheticCertificateProfileProbe",
            "failed",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);
                var plan = Prepare(session, binding, "root.Direct.Text ?? \"<none>\"");

                var limitedSource = new EvidenceViewSource(session, EvidenceView.StringPartialLimit);
                var limited = DumpMemberChainEngine.Evaluate(limitedSource, plan);
                Assert.Equal(EvaluationCompletionStatus.Completed, limited.Completion);
                Assert.Equal(EvaluationCompleteness.Partial, limited.Completeness);
                Assert.Equal(EvaluationEvidenceStatus.Partial, limited.Evidence);
                Assert.Equal("fa", limited.Value!.StringValue);
                Assert.Equal("QUERY_CHAIN_TERMINAL_LIMIT_EXCEEDED", Assert.Single(limited.Diagnostics).Code);
                Assert.DoesNotContain(
                    limited.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");

                var memorySource = new EvidenceViewSource(session, EvidenceView.StringPartialMemory);
                var memory = DumpMemberChainEngine.Evaluate(memorySource, plan);
                Assert.Equal(EvaluationCompletionStatus.Blocked, memory.Completion);
                Assert.Equal(EvaluationCompleteness.Partial, memory.Completeness);
                Assert.Equal(EvaluationEvidenceStatus.Partial, memory.Evidence);
                Assert.Equal("fa", memory.Value!.StringValue);
                Assert.Equal("QUERY_CHAIN_TERMINAL_PARTIAL", Assert.Single(memory.Diagnostics).Code);
                Assert.DoesNotContain(
                    memory.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");

                var unavailableSource = new EvidenceViewSource(session, EvidenceView.StringUnavailable);
                var unavailable = DumpMemberChainEngine.Evaluate(unavailableSource, plan);
                AssertBlockedWithoutValue(unavailable, EvaluationEvidenceStatus.Unavailable);
                Assert.Equal("QUERY_CHAIN_TERMINAL_UNAVAILABLE", Assert.Single(unavailable.Diagnostics).Code);
                Assert.DoesNotContain(
                    unavailable.Provenance,
                    item => item.SourceId == "dump-member-chain:null-coalesce-v1");
            });
    }

    /// <summary>
    /// Proves explicit W6 facade routing returns the unchanged derived-query envelope, preparation failures remain
    /// typed result rows, and the default frozen-W5 overload continues to reject the same member-chain syntax.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainEvaluationV1")]
    public void Product_facade_routes_only_explicit_W6_profile_and_preserves_legacy_default()
    {
        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            "failed",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);
                var policy = CreatePolicy();
                const string expression = "root.Failure?.Code";

                var legacy = DumpExpressionEvaluator.Evaluate(
                    session,
                    expression,
                    binding,
                    policy);
                Assert.Equal(DumpExpressionEvaluationOutcomeKind.ClassificationFailure, legacy.Kind);
                Assert.Equal(DumpExpressionClassificationStatus.Unsupported, legacy.ClassificationFailure!.Status);
                Assert.Null(legacy.DerivedQueryResult);

                var routed = DumpExpressionEvaluator.Evaluate(
                    session,
                    expression,
                    binding,
                    policy,
                    DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
                Assert.Equal(DumpExpressionEvaluationOutcomeKind.DerivedQuery, routed.Kind);
                Assert.Equal(DumpExpressionKind.FixedDepthMemberChain, routed.Request!.AdmittedKind);
                AssertExactString(routed.DerivedQueryResult!, "request-failed");
                Assert.Null(routed.ClassificationFailure);
                Assert.Null(routed.CounterfactualExecutionResult);

                var plan = Prepare(session, binding, expression);
                var direct = DumpMemberChainEngine.Evaluate(session, plan);
                Assert.Equal(
                    EvaluationResultReplay.ComputeSha256(
                        direct,
                        static value => value.ToCanonicalReplayProjection()),
                    EvaluationResultReplay.ComputeSha256(
                        routed.DerivedQueryResult!,
                        static value => value.ToCanonicalReplayProjection()));

                var missing = DumpExpressionEvaluator.Evaluate(
                    session,
                    "root.Failure?.DoesNotExist",
                    binding,
                    policy,
                    DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
                Assert.Equal(DumpExpressionEvaluationOutcomeKind.DerivedQuery, missing.Kind);
                var failure = Assert.IsType<EvaluationResult<DumpQueryValue>>(missing.DerivedQueryResult);
                Assert.Equal(EvaluationSemanticMode.DerivedQuery, failure.SemanticMode);
                Assert.Equal(EvaluationCompletionStatus.Blocked, failure.Completion);
                Assert.Equal(EvaluationCompleteness.None, failure.Completeness);
                Assert.Equal(EvaluationEvidenceStatus.Unavailable, failure.Evidence);
                Assert.Null(failure.Value);
                Assert.Equal("QUERY_CHAIN_BIND_UNAVAILABLE", Assert.Single(failure.Diagnostics).Code);
            });
    }

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

    private static void AssertBlockedWithoutValue(
        EvaluationResult<DumpQueryValue> result,
        EvaluationEvidenceStatus evidence)
    {
        Assert.Equal(EvaluationSemanticMode.DerivedQuery, result.SemanticMode);
        Assert.Equal(EvaluationCompletionStatus.Blocked, result.Completion);
        Assert.Equal(EvaluationCompleteness.None, result.Completeness);
        Assert.Equal(evidence, result.Evidence);
        Assert.Equal(EvaluationEffectStatus.None, result.Effects);
        Assert.Null(result.Value);
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
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        Assert.Equal(DumpExpressionClassificationStatus.Accepted, classification.Status);
        Assert.Equal(DumpExpressionKind.FixedDepthMemberChain, classification.Kind);
        return Assert.IsType<DumpExpressionRequest>(classification.Request);
    }

    private static DumpExpressionPolicy CreatePolicy() =>
        DumpExpressionPolicy.Create(
            DumpMethodEvaluationMode.Interpreted,
            instructionLimit: 100,
            logicalDepthLimit: 2,
            traversalLimit: 10);

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

    private enum EvidenceView
    {
        None,
        ReferencePartial,
        ReferenceUnavailable,
        TargetConflict,
        TargetInvalid,
        StorageConflict,
        StorageInvalid,
        StringPartialLimit,
        StringPartialMemory,
        StringUnavailable,
    }

    private sealed class EvidenceViewSource(
        ClrmdDumpSession session,
        EvidenceView view) : IDumpMemberChainEvidenceSource
    {
        internal int ReferenceCalls { get; private set; }

        internal int TargetCalls { get; private set; }

        internal int StorageCalls { get; private set; }

        internal int TerminalCalls { get; private set; }

        public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

        public int MaximumReadLength => session.Memory.MaximumReadLength;

        public ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> CertifyDeclaredDataMember(
            ClrmdHeapObjectInfo root,
            string referenceFieldName,
            string terminalMemberName) =>
            throw new InvalidOperationException("Evaluation attempted declaration rebinding.");

        public ClrmdEvidenceResult<ClrmdObjectReferenceObservation> ReadObjectReference(
            ClrmdHeapObjectInfo root,
            ClrmdInstanceFieldInfo field)
        {
            ReferenceCalls++;
            var exact = session.ReadObjectReference(root, field);
            if (view is not (EvidenceView.ReferencePartial or EvidenceView.ReferenceUnavailable))
            {
                return exact;
            }

            var bytes = view == EvidenceView.ReferencePartial
                ? exact.Value!.Memory.Bytes.AsSpan(0, Math.Min(2, exact.Value.Memory.BytesRead)).ToArray()
                : [];
            return ClrmdObjectReferenceObservation.Project(
                field,
                field.Size,
                PhoenixInspect.Host.Abstractions.MemoryReadResult.Create(
                    field.Snapshot.MemorySourceId,
                    field.Address,
                    field.Size,
                    bytes));
        }

        public ClrmdEvidenceResult<ClrmdReferencedObjectInfo> ValidateReferencedObject(
            ClrmdDeclaredDataMemberCertificate certificate,
            ClrmdObjectReferenceObservation reference)
        {
            TargetCalls++;
            if (view == EvidenceView.TargetConflict)
            {
                return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence: [reference.Memory]);
            }

            if (view == EvidenceView.TargetInvalid)
            {
                return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: [reference.Memory]);
            }

            return session.ValidateReferencedObject(certificate, reference);
        }

        public ClrmdEvidenceResult<ClrmdInstanceFieldInfo> BindTerminalStorage(
            ClrmdDeclaredDataMemberCertificate certificate,
            ClrmdReferencedObjectInfo target)
        {
            StorageCalls++;
            if (view == EvidenceView.StorageConflict)
            {
                return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch);
            }

            if (view == EvidenceView.StorageInvalid)
            {
                return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData);
            }

            return session.BindTerminalStorage(certificate, target);
        }

        public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field)
        {
            TerminalCalls++;
            return session.ReadInt32Field(target, field);
        }

        public ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation> ReadNullableInt32Field(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field)
        {
            TerminalCalls++;
            return session.ReadNullableInt32Field(target, field);
        }

        public ClrmdStringFieldObservation ReadStringField(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field,
            int maximumCharacters)
        {
            TerminalCalls++;
            if (view == EvidenceView.StringUnavailable)
            {
                return new ClrmdStringFieldObservation(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemoryUnavailable,
                    isNull: false,
                    value: null,
                    targetLength: null,
                    target.Address,
                    field.Name,
                    field.MetadataToken,
                    field.Address,
                    stringAddress: null,
                    evidence: []);
            }

            if (view is EvidenceView.StringPartialLimit or EvidenceView.StringPartialMemory)
            {
                var limited = session.ReadStringField(target, field, maximumCharacters: 2);
                Assert.Equal(ClrmdEvidenceStatus.Partial, limited.Status);
                Assert.Equal(ClrmdValueIssue.LimitExceeded, limited.Issue);
                return view == EvidenceView.StringPartialLimit
                    ? limited
                    : new ClrmdStringFieldObservation(
                        ClrmdEvidenceStatus.Partial,
                        ClrmdValueIssue.MemoryUnavailable,
                        isNull: false,
                        limited.Value,
                        limited.TargetLength,
                        limited.ObjectAddress,
                        limited.FieldName,
                        limited.FieldMetadataToken,
                        limited.FieldAddress,
                        limited.StringAddress,
                        limited.Evidence);
            }

            return session.ReadStringField(target, field, maximumCharacters);
        }
    }
}
