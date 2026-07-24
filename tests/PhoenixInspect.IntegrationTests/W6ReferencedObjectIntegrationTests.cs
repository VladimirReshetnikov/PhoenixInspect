using System.Buffers.Binary;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises W6.4 pointer observations, truthful non-root identity, alias provenance, and relative storage binding.
/// </summary>
public sealed class W6ReferencedObjectIntegrationTests
{
    /// <summary>
    /// Proves four- and eight-byte exact decoding plus prefix-preserving partial and unavailable observations without
    /// requiring a dump or fabricating a pointer suffix.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "W6ReferencedObjectV1")]
    public void Pointer_projection_supports_both_widths_and_never_fabricates_missing_bytes()
    {
        foreach (var pointerSize in new[] { sizeof(uint), sizeof(ulong) })
        {
            const ulong expected = 0x1234ABCD;
            var bytes = new byte[pointerSize];
            if (pointerSize == sizeof(uint))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)expected);
            }
            else
            {
                BinaryPrimitives.WriteUInt64LittleEndian(bytes, expected);
            }

            var field = CreateField(pointerSize);
            var exact = ClrmdObjectReferenceObservation.Project(
                field,
                pointerSize,
                MemoryReadResult.Create(field.Snapshot.MemorySourceId, field.Address, pointerSize, bytes));
            Assert.Equal(ClrmdEvidenceStatus.Exact, exact.Status);
            Assert.Equal(expected, exact.Value!.TargetAddress);
            Assert.True(exact.Value.IsExactNonNull);

            var nullBytes = new byte[pointerSize];
            var exactNull = ClrmdObjectReferenceObservation.Project(
                field,
                pointerSize,
                MemoryReadResult.Create(field.Snapshot.MemorySourceId, field.Address, pointerSize, nullBytes));
            Assert.Equal(ClrmdEvidenceStatus.Exact, exactNull.Status);
            Assert.True(exactNull.Value!.IsExactNull);

            var partial = ClrmdObjectReferenceObservation.Project(
                field,
                pointerSize,
                MemoryReadResult.Create(field.Snapshot.MemorySourceId, field.Address, pointerSize, bytes[..2]));
            Assert.Equal(ClrmdEvidenceStatus.Partial, partial.Status);
            Assert.Null(partial.Value!.TargetAddress);
            Assert.Equal(2, partial.Value.Memory.BytesRead);

            var unavailable = ClrmdObjectReferenceObservation.Project(
                field,
                pointerSize,
                MemoryReadResult.Create(field.Snapshot.MemorySourceId, field.Address, pointerSize, []));
            Assert.Equal(ClrmdEvidenceStatus.Unavailable, unavailable.Status);
            Assert.Null(unavailable.Value!.TargetAddress);
            Assert.Empty(unavailable.Value.Memory.Bytes);

            var headerBytes = new byte[pointerSize];
            if (pointerSize == sizeof(uint))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(headerBytes, (uint)expected);
            }
            else
            {
                BinaryPrimitives.WriteUInt64LittleEndian(headerBytes, expected);
            }

            var headerRead = MemoryReadResult.Create(
                field.Snapshot.MemorySourceId,
                address: expected,
                pointerSize,
                headerBytes);
            var matchingHeader = ClrmdDumpSession.ProjectObjectHeader(pointerSize, headerRead, expected);
            Assert.Equal(ClrmdEvidenceStatus.Exact, matchingHeader.Status);
            Assert.Equal(expected, matchingHeader.MethodTable);

            var conflictingHeader = ClrmdDumpSession.ProjectObjectHeader(pointerSize, headerRead, expected + 1);
            Assert.Equal(ClrmdEvidenceStatus.Conflict, conflictingHeader.Status);
            Assert.Equal(ClrmdValueIssue.TypeMismatch, conflictingHeader.Issue);
            Assert.Equal(expected, conflictingHeader.MethodTable);

            var zeroHeader = ClrmdDumpSession.ProjectObjectHeader(
                pointerSize,
                MemoryReadResult.Create(
                    field.Snapshot.MemorySourceId,
                    address: expected,
                    pointerSize,
                    new byte[pointerSize]),
                expected);
            Assert.Equal(ClrmdEvidenceStatus.Invalid, zeroHeader.Status);
            Assert.Equal(ClrmdValueIssue.InvalidData, zeroHeader.Issue);
            Assert.Null(zeroHeader.MethodTable);

            var partialHeader = ClrmdDumpSession.ProjectObjectHeader(
                pointerSize,
                MemoryReadResult.Create(
                    field.Snapshot.MemorySourceId,
                    address: expected,
                    pointerSize,
                    headerBytes[..2]),
                expected);
            Assert.Equal(ClrmdEvidenceStatus.Partial, partialHeader.Status);
            Assert.Equal(ClrmdValueIssue.MemoryUnavailable, partialHeader.Issue);
            Assert.Null(partialHeader.MethodTable);
        }
    }

    /// <summary>
    /// Validates exact non-null and null request references, binds terminal storage without another read, preserves
    /// alias-field provenance, and rejects an internally consistent runtime subtype as unsupported rather than conflict.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W6ReferencedObjectV1")]
    public void Real_graphs_validate_non_root_identity_null_alias_and_exact_type_rules()
    {
        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            "failed",
            (session, root) =>
            {
                var certificate = AssertCertificate(session, root, "Failure", "Code");
                var reference = session.ReadObjectReference(root, certificate.OuterField);
                Assert.Equal(ClrmdEvidenceStatus.Exact, reference.Status);
                Assert.True(reference.Value!.IsExactNonNull);
                Assert.Single(reference.Evidence);

                var targetResult = session.ValidateReferencedObject(certificate, reference.Value);
                Assert.Equal(ClrmdEvidenceStatus.Exact, targetResult.Status);
                var target = targetResult.Value!;
                Assert.Equal(certificate.DeclaredTarget.Name, target.TypeName);
                Assert.Equal(certificate.DeclaredTarget.MetadataToken, target.TypeMetadataToken);
                Assert.Equal(reference.Value.TargetAddress, target.Address);
                Assert.Equal(2, target.Evidence.Length);
                Assert.Equal(target.Address, target.Evidence[1].Address);
                Assert.DoesNotContain(
                    typeof(ClrmdReferencedObjectInfo).GetProperties(),
                    property => property.Name is "RootAddress" or "RootKind");

                var storage = session.BindTerminalStorage(certificate, target);
                Assert.Equal(ClrmdEvidenceStatus.Exact, storage.Status);
                Assert.Empty(storage.Evidence);
                Assert.Equal(target.Address, storage.Value!.OwnerAddress);
                Assert.Equal(
                    target.Address + (ulong)certificate.Storage.OffsetFromObject,
                    storage.Value.Address);
                Assert.True(storage.Value.Address >= target.Address);
                Assert.True(storage.Value.Address - target.Address < target.Size);

                var misaligned = CreateExactReference(certificate.OuterField, targetAddress: 1);
                var invalidPointer = session.ValidateReferencedObject(certificate, misaligned);
                Assert.Equal(ClrmdEvidenceStatus.Invalid, invalidPointer.Status);
                Assert.Equal(ClrmdValueIssue.InvalidData, invalidPointer.Issue);
                Assert.Single(invalidPointer.Evidence);

                var forgedOwner = new ClrmdHeapObjectInfo(
                    root.Snapshot,
                    root.Address + 8,
                    root.TypeName,
                    root.TypeMetadataToken,
                    root.MethodTable,
                    root.RootAddress,
                    root.RootKind,
                    root.Module,
                    root.Evidence);
                var conflictingOwner = session.ReadObjectReference(forgedOwner, certificate.OuterField);
                Assert.Equal(ClrmdEvidenceStatus.Conflict, conflictingOwner.Status);
                Assert.Equal(ClrmdValueIssue.TypeMismatch, conflictingOwner.Issue);
                Assert.Empty(conflictingOwner.Evidence);

                var foreignOwner = new ClrmdHeapObjectInfo(
                    new ClrmdSnapshotIdentity(new string('b', 64)),
                    root.Address,
                    root.TypeName,
                    root.TypeMetadataToken,
                    root.MethodTable,
                    root.RootAddress,
                    root.RootKind,
                    root.Module,
                    root.Evidence);
                var conflictingSnapshot = session.ReadObjectReference(foreignOwner, certificate.OuterField);
                Assert.Equal(ClrmdEvidenceStatus.Conflict, conflictingSnapshot.Status);
                Assert.Equal(ClrmdValueIssue.SnapshotMismatch, conflictingSnapshot.Issue);
                Assert.Empty(conflictingSnapshot.Evidence);

                var undersized = ForgeTarget(target, target.Address, size: 1, target.Selection);
                var invalidRange = session.BindTerminalStorage(certificate, undersized);
                Assert.Equal(ClrmdEvidenceStatus.Invalid, invalidRange.Status);
                Assert.Equal(ClrmdValueIssue.InvalidData, invalidRange.Issue);
                Assert.Empty(invalidRange.Evidence);

                var overflowSelection = CreateExactReference(certificate.OuterField, ulong.MaxValue);
                var overflowTarget = ForgeTarget(target, ulong.MaxValue, ulong.MaxValue, overflowSelection);
                var overflow = session.BindTerminalStorage(certificate, overflowTarget);
                Assert.Equal(ClrmdEvidenceStatus.Invalid, overflow.Status);
                Assert.Equal(ClrmdValueIssue.InvalidData, overflow.Issue);
                Assert.Empty(overflow.Evidence);
            });

        CaptureGraph(
            "--synthetic-request-pipeline",
            "SyntheticRequestPipelineProbe",
            "clear",
            (session, root) =>
            {
                var certificate = AssertCertificate(session, root, "Failure", "Code");
                var reference = session.ReadObjectReference(root, certificate.OuterField);
                Assert.Equal(ClrmdEvidenceStatus.Exact, reference.Status);
                Assert.True(reference.Value!.IsExactNull);
                var target = session.ValidateReferencedObject(certificate, reference.Value);
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, target.Status);
                Assert.Equal(ClrmdValueIssue.ObjectUnavailable, target.Issue);
                Assert.Single(target.Evidence);
            });

        CaptureGraph(
            "--synthetic-certificate-profiles",
            "SyntheticCertificateProfileProbe",
            "failed",
            (session, root) =>
            {
                var firstCertificate = AssertCertificate(session, root, "Direct", "Text");
                var aliasCertificate = AssertCertificate(session, root, "DirectAlias", "Text");
                var firstReference = session.ReadObjectReference(root, firstCertificate.OuterField).Value!;
                var aliasReference = session.ReadObjectReference(root, aliasCertificate.OuterField).Value!;
                Assert.Equal(firstReference.TargetAddress, aliasReference.TargetAddress);
                Assert.NotEqual(
                    firstReference.ToCanonicalReplayProjection(),
                    aliasReference.ToCanonicalReplayProjection());

                var firstTarget = session.ValidateReferencedObject(firstCertificate, firstReference);
                var aliasTarget = session.ValidateReferencedObject(aliasCertificate, aliasReference);
                Assert.Equal(ClrmdEvidenceStatus.Exact, firstTarget.Status);
                Assert.Equal(ClrmdEvidenceStatus.Exact, aliasTarget.Status);
                Assert.Equal(firstTarget.Value!.Address, aliasTarget.Value!.Address);
                Assert.Equal(
                    firstTarget.Value.Identity.ToCanonicalReplayProjection(),
                    aliasTarget.Value.Identity.ToCanonicalReplayProjection());
                Assert.NotEqual(
                    firstTarget.Value.ToCanonicalReplayProjection(),
                    aliasTarget.Value.ToCanonicalReplayProjection());

                var wrongSelection = session.ValidateReferencedObject(firstCertificate, aliasReference);
                Assert.Equal(ClrmdEvidenceStatus.Conflict, wrongSelection.Status);
                Assert.Equal(ClrmdValueIssue.TypeMismatch, wrongSelection.Issue);
                Assert.Single(wrongSelection.Evidence);

                var subtypeCertificate = AssertCertificate(session, root, "Polymorphic", "Value");
                var subtypeReference = session.ReadObjectReference(root, subtypeCertificate.OuterField);
                Assert.Equal(ClrmdEvidenceStatus.Exact, subtypeReference.Status);
                var subtype = session.ValidateReferencedObject(subtypeCertificate, subtypeReference.Value!);
                Assert.Equal(ClrmdEvidenceStatus.Unavailable, subtype.Status);
                Assert.Equal(ClrmdValueIssue.MemberShapeUnsupported, subtype.Issue);
                Assert.Equal(2, subtype.Evidence.Length);
            });
    }

    private static ClrmdInstanceFieldInfo CreateField(int pointerSize)
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
        return new ClrmdInstanceFieldInfo(
            snapshot,
            ownerAddress: 0x800,
            ownerMethodTable: 0x900,
            ownerTypeName: "Synthetic.Owner",
            name: "Target",
            metadataToken: 0x04000001,
            address: 0x1000,
            size: pointerSize,
            isObjectReference: true,
            elementType: "Class",
            fieldTypeName: "Synthetic.Target",
            nullableInt32Layout: null);
    }

    private static ClrmdObjectReferenceObservation CreateExactReference(
        ClrmdInstanceFieldInfo field,
        ulong targetAddress)
    {
        var bytes = new byte[field.Size];
        if (field.Size == sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked((uint)targetAddress));
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, targetAddress);
        }

        var result = ClrmdObjectReferenceObservation.Project(
            field,
            field.Size,
            MemoryReadResult.Create(field.Snapshot.MemorySourceId, field.Address, field.Size, bytes));
        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        return Assert.IsType<ClrmdObjectReferenceObservation>(result.Value);
    }

    private static ClrmdReferencedObjectInfo ForgeTarget(
        ClrmdReferencedObjectInfo source,
        ulong address,
        ulong size,
        ClrmdObjectReferenceObservation selection) =>
        new(
            source.Snapshot,
            address,
            source.TypeName,
            source.TypeMetadataToken,
            source.MethodTable,
            size,
            source.Module,
            selection,
            source.Evidence);

    private static ClrmdDeclaredDataMemberCertificate AssertCertificate(
        ClrmdDumpSession session,
        ClrmdHeapObjectInfo root,
        string referenceName,
        string terminalName)
    {
        var result = session.CertifyDeclaredDataMember(root, referenceName, terminalName);
        Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
        return Assert.IsType<ClrmdDeclaredDataMemberCertificate>(result.Value);
    }

    private static void CaptureGraph(
        string command,
        string rootType,
        string state,
        Action<ClrmdDumpSession, ClrmdHeapObjectInfo> assertion)
    {
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w6-reference-{Guid.NewGuid():N}.dmp");
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
}
