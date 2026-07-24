using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace PhoenixInspect.Host.Dump.ClrMD;

public sealed partial class ClrmdDumpSession
{
    /// <summary>
    /// Reads one already-selected ordinary object-reference field without repeating runtime member selection.
    /// </summary>
    /// <param name="owner">The exact rooted owner against which <paramref name="field"/> was selected.</param>
    /// <param name="field">The immutable outer-field descriptor issued for <paramref name="owner"/>.</param>
    /// <returns>
    /// An exact null/non-null pointer observation, or a partial, unavailable, conflicting, or invalid result retaining
    /// the sole pointer-width read. Missing pointer suffix bytes are never interpreted as zero.
    /// </returns>
    /// <remarks>
    /// This W6 operation reads only the outer field slot. It performs no target validation, member lookup, or
    /// terminal read; callers must separately validate an exact non-null target.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="owner"/> or <paramref name="field"/> is <see langword="null"/>.
    /// </exception>
    public ClrmdEvidenceResult<ClrmdObjectReferenceObservation> ReadObjectReference(
        ClrmdHeapObjectInfo owner,
        ClrmdInstanceFieldInfo field)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(field);
        var ownerIssue = ValidateBoundFieldOwner(owner, field);
        if (ownerIssue != ClrmdValueIssue.None)
        {
            return ClrmdEvidenceResult<ClrmdObjectReferenceObservation>.Create(
                ClrmdEvidenceStatus.Conflict,
                ownerIssue);
        }

        if (!field.IsObjectReference || field.Size != Memory.PointerSize)
        {
            return ClrmdEvidenceResult<ClrmdObjectReferenceObservation>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        MemoryReadResult read;
        try
        {
            read = Memory.Read(field.Address, Memory.PointerSize);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdObjectReferenceObservation>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        return ClrmdObjectReferenceObservation.Project(field, Memory.PointerSize, read);
    }

    /// <summary>
    /// Validates one exact non-null reference observation as the exact declared intermediate type.
    /// </summary>
    /// <param name="certificate">The declaration certificate that issued the outer field and target TypeDef.</param>
    /// <param name="reference">The exact non-null pointer observation selecting the candidate object.</param>
    /// <returns>
    /// Exact non-root identity and extent, or a typed result retaining the pointer and any object-header read. An
    /// internally consistent different exact runtime type is unsupported; contradictory method-table facts conflict.
    /// </returns>
    /// <remarks>
    /// The descriptor deliberately gains no root handle facts. This operation performs no terminal member
    /// lookup and no terminal value read.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="certificate"/> or <paramref name="reference"/> is <see langword="null"/>.
    /// </exception>
    public ClrmdEvidenceResult<ClrmdReferencedObjectInfo> ValidateReferencedObject(
        ClrmdDeclaredDataMemberCertificate certificate,
        ClrmdObjectReferenceObservation reference)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(reference);
        var pointerEvidence = ImmutableArray.Create(reference.Memory);
        if (certificate.OuterField.Snapshot != Snapshot || reference.Field.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch,
                evidence: pointerEvidence);
        }

        if (!string.Equals(
                certificate.OuterField.ToCanonicalReplayProjection(),
                reference.Field.ToCanonicalReplayProjection(),
                StringComparison.Ordinal))
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                evidence: pointerEvidence);
        }

        if (reference.Status != ClrmdEvidenceStatus.Exact || reference.TargetAddress is null)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                reference.Status,
                reference.Issue,
                evidence: pointerEvidence);
        }

        if (reference.TargetAddress == 0)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ObjectUnavailable,
                evidence: pointerEvidence);
        }

        var targetAddress = reference.TargetAddress.Value;
        if (targetAddress % (ulong)Memory.PointerSize != 0 ||
            targetAddress > ulong.MaxValue - (ulong)(Memory.PointerSize - 1))
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: pointerEvidence);
        }

        ClrObject runtimeObject;
        ClrType? runtimeType;
        try
        {
            runtimeObject = _runtime.Heap.GetObject(targetAddress);
            runtimeType = runtimeObject.Type;
        }
        catch (Exception exception) when (
            exception is ClrDiagnosticsException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: pointerEvidence);
        }

        if (!runtimeObject.IsValid || runtimeType is null)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.ObjectUnavailable,
                evidence: pointerEvidence);
        }

        MemoryReadResult methodTableRead;
        try
        {
            methodTableRead = Memory.Read(targetAddress, Memory.PointerSize);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: pointerEvidence);
        }
        var evidence = pointerEvidence.Add(methodTableRead);
        var header = ProjectObjectHeader(Memory.PointerSize, methodTableRead, runtimeType.MethodTable);
        if (header.Status != ClrmdEvidenceStatus.Exact)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                header.Status,
                header.Issue,
                evidence: evidence);
        }

        var observedMethodTable = header.MethodTable!.Value;

        if (!_moduleInfos.TryGetValue(
                (runtimeType.Module.AppDomain.Address, runtimeType.Module.Address),
                out var runtimeModule))
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable,
                evidence: evidence);
        }

        if (runtimeModule.Identity != certificate.DeclaredTarget.RuntimeModule ||
            runtimeType.MetadataToken != certificate.DeclaredTarget.MetadataToken ||
            !string.Equals(runtimeType.Name, certificate.DeclaredTarget.Name, StringComparison.Ordinal))
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemberShapeUnsupported,
                evidence: evidence);
        }

        ulong objectSize;
        try
        {
            objectSize = runtimeObject.Size;
        }
        catch (Exception exception) when (
            exception is ClrDiagnosticsException or InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: evidence);
        }
        if (objectSize < (ulong)Memory.PointerSize || targetAddress > ulong.MaxValue - (objectSize - 1))
        {
            return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: evidence);
        }

        var target = new ClrmdReferencedObjectInfo(
            Snapshot,
            targetAddress,
            runtimeType.Name ?? certificate.DeclaredTarget.Name,
            runtimeType.MetadataToken,
            observedMethodTable,
            objectSize,
            runtimeModule,
            reference,
            evidence);
        return ClrmdEvidenceResult<ClrmdReferencedObjectInfo>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            target,
            evidence);
    }

    /// <summary>
    /// Materializes target-specific terminal storage solely from certified relative layout and exact object extent.
    /// </summary>
    /// <param name="certificate">The matching declared-member certificate.</param>
    /// <param name="target">The exact declared non-root target selected through the certificate's outer field.</param>
    /// <returns>An exact ordinary field descriptor, or conflict/invalid evidence with no partial descriptor.</returns>
    /// <remarks>
    /// This W6 operation performs no memory read and no ClrMD field/type catalog lookup. All address additions
    /// and outer/nested ranges are checked before a descriptor is issued.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="certificate"/> or <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public ClrmdEvidenceResult<ClrmdInstanceFieldInfo> BindTerminalStorage(
        ClrmdDeclaredDataMemberCertificate certificate,
        ClrmdReferencedObjectInfo target)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(target);
        if (certificate.OuterField.Snapshot != Snapshot || target.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (target.Module.Identity != certificate.DeclaredTarget.RuntimeModule ||
            target.TypeMetadataToken != certificate.DeclaredTarget.MetadataToken ||
            !string.Equals(target.TypeName, certificate.DeclaredTarget.Name, StringComparison.Ordinal) ||
            !target.Selection.IsExactNonNull ||
            target.Selection.TargetAddress != target.Address ||
            !string.Equals(
                target.Selection.Field.ToCanonicalReplayProjection(),
                certificate.OuterField.ToCanonicalReplayProjection(),
                StringComparison.Ordinal))
        {
            return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        var storage = certificate.Storage;
        if (!TryBindRange(
                target.Address,
                target.Size,
                storage.OffsetFromObject,
                storage.Size,
                out var storageAddress))
        {
            return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        ClrmdNullableInt32FieldLayout? nullableLayout = null;
        if (storage.NullableInt32Layout is { } relativeNullable)
        {
            if (!TryBindRange(
                    storageAddress,
                    (ulong)storage.Size,
                    relativeNullable.HasValueOffset,
                    relativeNullable.HasValueSize,
                    out var hasValueAddress) ||
                !TryBindRange(
                    storageAddress,
                    (ulong)storage.Size,
                    relativeNullable.ValueOffset,
                    relativeNullable.ValueSize,
                    out var valueAddress) ||
                !ClrmdNullableInt32FieldLayout.HasValidDistinctStorage(
                    storageAddress,
                    storage.Size,
                    relativeNullable.HasValueMetadataToken,
                    hasValueAddress,
                    relativeNullable.HasValueSize,
                    relativeNullable.ValueMetadataToken,
                    valueAddress,
                    relativeNullable.ValueSize))
            {
                return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData);
            }

            nullableLayout = new ClrmdNullableInt32FieldLayout(
                relativeNullable.HasValueMetadataToken,
                hasValueAddress,
                relativeNullable.HasValueSize,
                relativeNullable.ValueMetadataToken,
                valueAddress,
                relativeNullable.ValueSize);
        }

        var descriptor = new ClrmdInstanceFieldInfo(
            Snapshot,
            target.Address,
            target.MethodTable,
            target.TypeName,
            storage.Name,
            storage.MetadataToken,
            storageAddress,
            storage.Size,
            storage.IsObjectReference,
            storage.ElementType,
            storage.FieldTypeName,
            nullableLayout);
        return ClrmdEvidenceResult<ClrmdInstanceFieldInfo>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            descriptor);
    }

    private static bool TryBindRange(
        ulong ownerAddress,
        ulong ownerSize,
        int relativeOffset,
        int rangeSize,
        out ulong address)
    {
        address = 0;
        if (relativeOffset < 0 || rangeSize <= 0 ||
            (ulong)relativeOffset > ownerSize ||
            (ulong)rangeSize > ownerSize - (ulong)relativeOffset ||
            ownerAddress > ulong.MaxValue - (ulong)relativeOffset)
        {
            return false;
        }

        address = ownerAddress + (ulong)relativeOffset;
        return address <= ulong.MaxValue - (ulong)(rangeSize - 1);
    }

    internal static (
        ClrmdEvidenceStatus Status,
        ClrmdValueIssue Issue,
        ulong? MethodTable) ProjectObjectHeader(
        int pointerSize,
        MemoryReadResult read,
        ulong runtimeMethodTable)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (pointerSize is not (sizeof(uint) or sizeof(ulong)) ||
            read.RequestedLength != pointerSize ||
            runtimeMethodTable == 0)
        {
            return (ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, null);
        }

        if (read.Status != MemoryReadStatus.Exact)
        {
            return (
                read.Status == MemoryReadStatus.Partial
                    ? ClrmdEvidenceStatus.Partial
                    : ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable,
                null);
        }

        if (!TryDecodePointer(read.Bytes.AsSpan(), pointerSize, out var observedMethodTable) ||
            observedMethodTable == 0)
        {
            return (ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, null);
        }

        return observedMethodTable == runtimeMethodTable
            ? (ClrmdEvidenceStatus.Exact, ClrmdValueIssue.None, observedMethodTable)
            : (ClrmdEvidenceStatus.Conflict, ClrmdValueIssue.TypeMismatch, observedMethodTable);
    }
}
