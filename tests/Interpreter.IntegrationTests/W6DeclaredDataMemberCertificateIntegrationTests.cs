using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Exercises W6.3 declaration and terminal-storage certificates against complex source-controlled synthetic dumps.
/// </summary>
public sealed class W6DeclaredDataMemberCertificateIntegrationTests
{
    /// <summary>
    /// Certifies the request, batch, and coordinator property/field portfolio, proves canonical replay and detached
    /// immutability, and verifies preparation never reads the receiver reference or leaf storage.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6DeclaredDataMemberCertificateV1")]
    public void Complex_graphs_freeze_declared_property_and_direct_field_storage_without_value_reads()
    {
        ClrmdHeapObjectInfo? requestRoot = null;
        var detached = ImmutableArray.CreateBuilder<ClrmdDeclaredDataMemberCertificate>();

        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            session =>
            {
                requestRoot = FindExactRoot(session, "SyntheticRequestPipelineProbe");
                detached.Add(AssertProperty(
                    session,
                    requestRoot,
                    "Failure",
                    "Code",
                    ClrmdTerminalDecoderKind.String));
                detached.Add(AssertProperty(
                    session,
                    requestRoot,
                    "CurrentRequest",
                    "Status",
                    ClrmdTerminalDecoderKind.String));

                var missing = session.CertifyDeclaredDataMember(requestRoot, "CurrentRequest", "ToString");
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, missing.Status);
                Assert.Equal(ClrmdValueIssue.FieldUnavailable, missing.Issue);
                Assert.Single(missing.Evidence);

                var nonReference = session.CertifyDeclaredDataMember(requestRoot, "Marker", "Value");
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, nonReference.Status);
                Assert.Equal(ClrmdValueIssue.TypeMismatch, nonReference.Issue);
                Assert.Empty(nonReference.Evidence);

                var unsupportedArray = session.CertifyDeclaredDataMember(requestRoot, "RetryMarkers", "Length");
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, unsupportedArray.Status);
                Assert.Equal(ClrmdValueIssue.TypeMismatch, unsupportedArray.Issue);
                Assert.Single(unsupportedArray.Evidence);
            });

        CaptureGraph(
            "--synthetic-batch-pipeline",
            "SyntheticBatchPipelineProbe",
            session =>
            {
                var root = FindExactRoot(session, "SyntheticBatchPipelineProbe");
                detached.Add(AssertProperty(
                    session,
                    root,
                    "LastFailure",
                    "Code",
                    ClrmdTerminalDecoderKind.String));
                detached.Add(AssertProperty(
                    session,
                    root,
                    "Progress",
                    "State",
                    ClrmdTerminalDecoderKind.String));
                detached.Add(AssertProperty(
                    session,
                    root,
                    "Progress",
                    "CompletedPartitions",
                    ClrmdTerminalDecoderKind.Int32));

                var foreign = session.CertifyDeclaredDataMember(
                    Assert.IsType<ClrmdHeapObjectInfo>(requestRoot),
                    "Failure",
                    "Code");
                Assert.Equal(ClrmdEvidenceStatus.Conflict, foreign.Status);
                Assert.Equal(ClrmdValueIssue.SnapshotMismatch, foreign.Issue);
                Assert.Empty(foreign.Evidence);
            });

        CaptureGraph(
            "--synthetic-coordinator-pipeline",
            "SyntheticCoordinatorPipelineProbe",
            session =>
            {
                var root = FindExactRoot(session, "SyntheticCoordinatorPipelineProbe");
                detached.Add(AssertDirectField(
                    session,
                    root,
                    "CurrentTask",
                    "state",
                    ClrmdTerminalDecoderKind.String));

                var unsupportedReferenceLeaf = session.CertifyDeclaredDataMember(root, "ActiveShard", "Health");
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, unsupportedReferenceLeaf.Status);
                Assert.Equal(ClrmdValueIssue.TypeMismatch, unsupportedReferenceLeaf.Issue);
                Assert.Single(unsupportedReferenceLeaf.Evidence);
            });

        CaptureGraph(
            "--synthetic-certificate-profiles",
            "SyntheticCertificateProfileProbe",
            session =>
            {
                var root = FindExactRoot(session, "SyntheticCertificateProfileProbe");
                detached.Add(AssertDirectField(
                    session,
                    root,
                    "Direct",
                    "Text",
                    ClrmdTerminalDecoderKind.String));
                detached.Add(AssertDirectField(
                    session,
                    root,
                    "Direct",
                    "Count",
                    ClrmdTerminalDecoderKind.Int32));
                var directNullable = AssertDirectField(
                    session,
                    root,
                    "Direct",
                    "OptionalCount",
                    ClrmdTerminalDecoderKind.NullableInt32);
                Assert.NotNull(directNullable.Storage.NullableInt32Layout);
                detached.Add(directNullable);

                var propertyNullable = AssertProperty(
                    session,
                    root,
                    "AutoNullable",
                    "OptionalValue",
                    ClrmdTerminalDecoderKind.NullableInt32);
                Assert.NotNull(propertyNullable.Storage.NullableInt32Layout);
                detached.Add(propertyNullable);

                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Computed", "Value"),
                    ClrmdValueIssue.MethodBodyUnavailable,
                    minimumEvidenceReads: 4);
                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Indexed", "Item"),
                    ClrmdValueIssue.TypeMismatch,
                    minimumEvidenceReads: 1);
                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Static", "Value"),
                    ClrmdValueIssue.TypeMismatch,
                    minimumEvidenceReads: 1);
                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Inherited", "Value"),
                    ClrmdValueIssue.FieldUnavailable,
                    minimumEvidenceReads: 1);
                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Unsupported", "Value"),
                    ClrmdValueIssue.TypeMismatch,
                    minimumEvidenceReads: 1);
                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Mismatched", "Value"),
                    ClrmdValueIssue.TypeMismatch,
                    minimumEvidenceReads: 1);
                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Call", "Value"),
                    ClrmdValueIssue.MethodBodyUnavailable,
                    minimumEvidenceReads: 4);
                AssertRejected(
                    session.CertifyDeclaredDataMember(root, "Virtual", "Value"),
                    ClrmdValueIssue.TypeMismatch,
                    minimumEvidenceReads: 1);
            });

        Assert.Equal(10, detached.Count);
        Assert.Equal(10, detached.Select(item => item.ToCanonicalReplayProjection()).Distinct().Count());
        var diskMetadata = ReadDiskMetadataIdentity();
        Assert.All(detached, certificate =>
        {
            Assert.False(string.IsNullOrWhiteSpace(certificate.ToCanonicalReplayProjection()));
            Assert.True(certificate.DeclaredTarget.ModuleContent.MetadataLength > 0);
            Assert.Equal(64, certificate.DeclaredTarget.ModuleContent.MetadataSha256.Length);
            Assert.True(certificate.Storage.OffsetFromObject >= IntPtr.Size);
            Assert.True(certificate.Storage.Size > 0);
            Assert.Equal(diskMetadata, certificate.DeclaredTarget.ModuleContent);
        });
    }

    private static ClrmdDeclaredDataMemberCertificate AssertProperty(
        ClrmdDumpSession session,
        ClrmdHeapObjectInfo root,
        string receiver,
        string terminal,
        ClrmdTerminalDecoderKind decoder)
    {
        var first = session.CertifyDeclaredDataMember(root, receiver, terminal);
        Assert.True(
            first.Status == ClrmdEvidenceStatus.Exact,
            $"Certificate failed with {first.Status}/{first.Issue} after {first.Evidence.Length} reads.");
        Assert.Equal(ClrmdValueIssue.None, first.Issue);
        var certificate = Assert.IsType<ClrmdDeclaredDataMemberCertificate>(first.Value);
        Assert.Equal(ClrmdTerminalMemberKind.CertifiedProperty, certificate.TerminalMemberKind);
        Assert.Equal(decoder, certificate.Decoder);
        Assert.NotNull(certificate.Property);
        Assert.Equal(certificate.TerminalMemberToken, certificate.Property.PropertyToken);
        Assert.Equal(certificate.Property.GetterToken, certificate.Property.GetterBody.MetadataToken);
        Assert.NotEqual(certificate.TerminalMemberToken, certificate.Storage.MetadataToken);
        Assert.All(first.Evidence, read => Assert.Equal(MemoryReadStatus.Exact, read.Status));
        Assert.DoesNotContain(first.Evidence, read => RangeContains(read, certificate.OuterField.Address));
        Assert.True(first.Evidence.Length >= 4);

        var replay = session.CertifyDeclaredDataMember(root, receiver, terminal);
        Assert.Equal(ClrmdEvidenceStatus.Exact, replay.Status);
        Assert.Equal(
            certificate.ToCanonicalReplayProjection(),
            replay.Value!.ToCanonicalReplayProjection());
        return certificate;
    }

    private static void AssertRejected(
        ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> result,
        ClrmdValueIssue issue,
        int minimumEvidenceReads)
    {
        Assert.Equal(ClrmdEvidenceStatus.Unavailable, result.Status);
        Assert.Equal(issue, result.Issue);
        Assert.Null(result.Value);
        Assert.True(result.Evidence.Length >= minimumEvidenceReads);
        Assert.All(result.Evidence, read => Assert.Equal(MemoryReadStatus.Exact, read.Status));
    }

    private static ClrmdDeclaredDataMemberCertificate AssertDirectField(
        ClrmdDumpSession session,
        ClrmdHeapObjectInfo root,
        string receiver,
        string terminal,
        ClrmdTerminalDecoderKind decoder)
    {
        var result = session.CertifyDeclaredDataMember(root, receiver, terminal);
        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        var certificate = Assert.IsType<ClrmdDeclaredDataMemberCertificate>(result.Value);
        Assert.Equal(ClrmdTerminalMemberKind.Field, certificate.TerminalMemberKind);
        Assert.Equal(decoder, certificate.Decoder);
        Assert.Null(certificate.Property);
        Assert.Equal(certificate.TerminalMemberToken, certificate.Storage.MetadataToken);
        Assert.Single(result.Evidence);
        Assert.Equal(root.Module.MetadataAddress, result.Evidence[0].Address);
        Assert.Equal(checked((int)root.Module.MetadataLength), result.Evidence[0].RequestedLength);
        Assert.DoesNotContain(result.Evidence, read => RangeContains(read, certificate.OuterField.Address));
        return certificate;
    }

    private static bool RangeContains(MemoryReadResult read, ulong address) =>
        read.RequestedLength > 0 &&
        address >= read.Address &&
        address - read.Address < (ulong)read.RequestedLength;

    private static ClrmdHeapObjectInfo FindExactRoot(ClrmdDumpSession session, string typeName)
    {
        var search = session.FindStrongHandleObjectsByTypeName(
            typeName,
            maximumMatches: 2,
            maximumHandlesScanned: 100_000);
        Assert.Equal(ClrmdEvidenceStatus.Exact, search.Status);
        return Assert.Single(search.Matches);
    }

    private static void CaptureGraph(
        string command,
        string rootType,
        Action<ClrmdDumpSession> assertion)
    {
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w6-certificate-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                       TestTargetPaths.ResolveExecutable(),
                       [command, "413", "-907", "failed"],
                       isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            using var session = OpenExact(dumpPath);
            Assert.Contains(session.Modules, module =>
                string.Equals(module.Name, "Interpreter.TestTarget.dll", StringComparison.Ordinal));
            assertion(session);
            Assert.Equal(rootType, FindExactRoot(session, rootType).TypeName);
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

    private static ModuleContentIdentity ReadDiskMetadataIdentity()
    {
        using var stream = File.OpenRead(TestTargetPaths.ResolveAssembly(TestTargetPaths.ResolveExecutable()));
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadata().GetContent();
        var reader = peReader.GetMetadataReader();
        return ModuleContentIdentity.FromMetadata(
            reader.GetGuid(reader.GetModuleDefinition().Mvid),
            metadata.AsSpan());
    }
}
