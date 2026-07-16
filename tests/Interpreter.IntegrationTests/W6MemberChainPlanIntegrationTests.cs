using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpDebugging;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises complete W6 member-chain preparation, capability-separated value reads, canonical replay, aliases, and
/// decoder-aware coalescing over source-controlled synthetic object graphs.
/// </summary>
public sealed class W6MemberChainPlanIntegrationTests
{
    /// <summary>
    /// Proves that preparation can use one declaration-certificate call while every reference/target/terminal
    /// operation is poisoned, and freezes a reproducible plan whose evidence excludes the outer reference slot.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainPlanV1")]
    public void Preparation_freezes_complete_identity_without_any_value_read_capability_use()
    {
        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            "failed",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);
                var request = Classify(binding, "root.Failure?.Code ?? \"<none>\"");
                var source = new PoisoningPreparationSource(session);

                var first = DumpMemberChainPreparationFacade.Prepare(source, request);
                Assert.True(first.IsSuccess);
                Assert.Null(first.Failure);
                var plan = Assert.IsType<DumpMemberChainPlan>(first.Plan);
                Assert.Equal(1, source.CertificateCalls);
                Assert.Equal(0, source.ValueOperationCalls);
                Assert.False(plan.ReferenceValueReadDuringPreparation);
                Assert.False(plan.TerminalValueReadDuringPreparation);
                Assert.Equal(EvaluationSemanticMode.DerivedQuery, plan.SemanticMode);
                Assert.Equal(request.Sha256, plan.RequestSha256);
                Assert.Equal(request.MemberChainExpressionIdentity!.Sha256, plan.ExpressionIdentitySha256);
                Assert.True(request.CanonicalBytes.AsSpan().SequenceEqual(plan.RequestCanonicalBytes.AsSpan()));
                Assert.True(request.MemberChainExpressionIdentity.CanonicalBytes.AsSpan().SequenceEqual(
                    plan.ExpressionIdentityCanonicalBytes.AsSpan()));
                Assert.Equal("Failure", plan.Certificate.OuterField.Name);
                Assert.Equal("Code", plan.Certificate.TerminalMemberName);
                Assert.Equal(ClrmdTerminalDecoderKind.String, plan.Certificate.Decoder);
                Assert.NotEmpty(plan.PreparationEvidence);
                Assert.NotEmpty(plan.PreparationBounds);
                Assert.DoesNotContain(
                    plan.PreparationEvidence,
                    read => RangesOverlap(
                        read.Address,
                        read.RequestedLength,
                        plan.Certificate.OuterField.Address,
                        plan.Certificate.OuterField.Size));

                var second = DumpMemberChainPreparationFacade.Prepare(source, request);
                Assert.True(second.IsSuccess);
                Assert.Equal(2, source.CertificateCalls);
                Assert.Equal(0, source.ValueOperationCalls);
                Assert.Equal(plan.Sha256, second.Plan!.Sha256);
                Assert.Equal(
                    plan.ToCanonicalReplayProjection(),
                    second.Plan.ToCanonicalReplayProjection());
            });
    }

    /// <summary>
    /// Proves alias fields remain plan-distinct, compatible conditional/nullable integer fallbacks bind, incompatible
    /// direct-Int32 coalescing and missing terminals expose typed failures, and no failure leaks a partial plan.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6MemberChainPlanV1")]
    public void Plans_preserve_alias_paths_and_reject_incompatible_or_missing_terminal_shapes()
    {
        CaptureGraph(
            "--synthetic-certificate-profiles",
            "SyntheticCertificateProfileProbe",
            "failed",
            (session, root) =>
            {
                var binding = DumpQueryRootBinding.FromExactObject("root", root);
                var direct = Prepare(session, Classify(binding, "root.Direct.Text"));
                var alias = Prepare(session, Classify(binding, "root.DirectAlias.Text"));
                Assert.NotEqual(direct.Sha256, alias.Sha256);
                Assert.NotEqual(
                    direct.ToCanonicalReplayProjection(),
                    alias.ToCanonicalReplayProjection());
                Assert.NotEqual(direct.Certificate.OuterField.Address, alias.Certificate.OuterField.Address);

                var conditionalInt = Prepare(session, Classify(binding, "root.Direct?.Count ?? 0"));
                Assert.Equal(DumpMemberChainAccessKind.Conditional, conditionalInt.AccessKind);
                Assert.Equal(DumpMemberChainFallbackKind.Int32, conditionalInt.FallbackKind);
                Assert.Equal(0, conditionalInt.Int32Fallback);
                Assert.Equal(ClrmdTerminalDecoderKind.Int32, conditionalInt.Certificate.Decoder);

                var nullableInt = Prepare(session, Classify(binding, "root.Direct.OptionalCount ?? 0"));
                Assert.Equal(DumpMemberChainAccessKind.Direct, nullableInt.AccessKind);
                Assert.Equal(ClrmdTerminalDecoderKind.NullableInt32, nullableInt.Certificate.Decoder);

                var incompatible = DumpMemberChainPreparationFacade.Prepare(
                    session,
                    Classify(binding, "root.Direct.Count ?? 0"));
                Assert.False(incompatible.IsSuccess);
                Assert.Null(incompatible.Plan);
                Assert.Equal("QUERY_CHAIN_COALESCE_TYPE_UNSUPPORTED", incompatible.Failure!.Code);
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, incompatible.Failure.Status);
                Assert.Equal(ClrmdValueIssue.MemberShapeUnsupported, incompatible.Failure.Issue);

                var missing = DumpMemberChainPreparationFacade.Prepare(
                    session,
                    Classify(binding, "root.Direct.DoesNotExist"));
                Assert.False(missing.IsSuccess);
                Assert.Null(missing.Plan);
                Assert.Equal("QUERY_CHAIN_BIND_UNAVAILABLE", missing.Failure!.Code);
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, missing.Failure.Status);
                Assert.Equal(ClrmdValueIssue.FieldUnavailable, missing.Failure.Issue);
            });
    }

    private static DumpMemberChainPlan Prepare(ClrmdDumpSession session, DumpExpressionRequest request)
    {
        var result = DumpMemberChainPreparationFacade.Prepare(session, request);
        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Null(result.Failure);
        return Assert.IsType<DumpMemberChainPlan>(result.Plan);
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

    private static bool RangesOverlap(ulong leftAddress, int leftLength, ulong rightAddress, int rightLength)
    {
        if (leftLength <= 0 || rightLength <= 0 ||
            leftAddress > ulong.MaxValue - (ulong)(leftLength - 1) ||
            rightAddress > ulong.MaxValue - (ulong)(rightLength - 1))
        {
            return true;
        }

        var leftEnd = leftAddress + (ulong)(leftLength - 1);
        var rightEnd = rightAddress + (ulong)(rightLength - 1);
        return leftAddress <= rightEnd && rightAddress <= leftEnd;
    }

    private static void CaptureGraph(
        string command,
        string rootType,
        string state,
        Action<ClrmdDumpSession, ClrmdHeapObjectInfo> assertion)
    {
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w6-plan-{Guid.NewGuid():N}.dmp");
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

    private sealed class PoisoningPreparationSource(ClrmdDumpSession session) : IDumpMemberChainEvidenceSource
    {
        private readonly ClrmdDumpSession session = session;

        internal int CertificateCalls { get; private set; }

        internal int ValueOperationCalls { get; private set; }

        public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

        public ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> CertifyDeclaredDataMember(
            ClrmdHeapObjectInfo root,
            string referenceFieldName,
            string terminalMemberName)
        {
            CertificateCalls++;
            return session.CertifyDeclaredDataMember(root, referenceFieldName, terminalMemberName);
        }

        public ClrmdEvidenceResult<ClrmdObjectReferenceObservation> ReadObjectReference(
            ClrmdHeapObjectInfo root,
            ClrmdInstanceFieldInfo field) =>
            Poison<ClrmdEvidenceResult<ClrmdObjectReferenceObservation>>();

        public ClrmdEvidenceResult<ClrmdReferencedObjectInfo> ValidateReferencedObject(
            ClrmdDeclaredDataMemberCertificate certificate,
            ClrmdObjectReferenceObservation reference) =>
            Poison<ClrmdEvidenceResult<ClrmdReferencedObjectInfo>>();

        public ClrmdEvidenceResult<ClrmdInstanceFieldInfo> BindTerminalStorage(
            ClrmdDeclaredDataMemberCertificate certificate,
            ClrmdReferencedObjectInfo target) =>
            Poison<ClrmdEvidenceResult<ClrmdInstanceFieldInfo>>();

        public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field) =>
            Poison<ClrmdEvidenceResult<ClrmdInt32FieldObservation>>();

        public ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation> ReadNullableInt32Field(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field) =>
            Poison<ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation>>();

        public ClrmdStringFieldObservation ReadStringField(
            ClrmdReferencedObjectInfo target,
            ClrmdInstanceFieldInfo field,
            int maximumCharacters) =>
            Poison<ClrmdStringFieldObservation>();

        private T Poison<T>()
        {
            ValueOperationCalls++;
            throw new InvalidOperationException("Preparation attempted a poisoned evaluation operation.");
        }
    }
}
