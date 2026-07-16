using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

internal interface IDumpMemberChainEvidenceSource
{
    ClrmdSnapshotIdentity Snapshot { get; }

    int MaximumReadLength { get; }

    ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> CertifyDeclaredDataMember(
        ClrmdHeapObjectInfo root,
        string referenceFieldName,
        string terminalMemberName);

    ClrmdEvidenceResult<ClrmdObjectReferenceObservation> ReadObjectReference(
        ClrmdHeapObjectInfo root,
        ClrmdInstanceFieldInfo field);

    ClrmdEvidenceResult<ClrmdReferencedObjectInfo> ValidateReferencedObject(
        ClrmdDeclaredDataMemberCertificate certificate,
        ClrmdObjectReferenceObservation reference);

    ClrmdEvidenceResult<ClrmdInstanceFieldInfo> BindTerminalStorage(
        ClrmdDeclaredDataMemberCertificate certificate,
        ClrmdReferencedObjectInfo target);

    ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field);

    ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation> ReadNullableInt32Field(
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field);

    ClrmdStringFieldObservation ReadStringField(
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field,
        int maximumCharacters);
}

internal sealed class ClrmdDumpMemberChainEvidenceSource(ClrmdDumpSession session) :
    IDumpMemberChainEvidenceSource
{
    private readonly ClrmdDumpSession session = session ?? throw new ArgumentNullException(nameof(session));

    public ClrmdSnapshotIdentity Snapshot => session.Snapshot;

    public int MaximumReadLength => session.Memory.MaximumReadLength;

    public ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> CertifyDeclaredDataMember(
        ClrmdHeapObjectInfo root,
        string referenceFieldName,
        string terminalMemberName) =>
        session.CertifyDeclaredDataMember(root, referenceFieldName, terminalMemberName);

    public ClrmdEvidenceResult<ClrmdObjectReferenceObservation> ReadObjectReference(
        ClrmdHeapObjectInfo root,
        ClrmdInstanceFieldInfo field) =>
        session.ReadObjectReference(root, field);

    public ClrmdEvidenceResult<ClrmdReferencedObjectInfo> ValidateReferencedObject(
        ClrmdDeclaredDataMemberCertificate certificate,
        ClrmdObjectReferenceObservation reference) =>
        session.ValidateReferencedObject(certificate, reference);

    public ClrmdEvidenceResult<ClrmdInstanceFieldInfo> BindTerminalStorage(
        ClrmdDeclaredDataMemberCertificate certificate,
        ClrmdReferencedObjectInfo target) =>
        session.BindTerminalStorage(certificate, target);

    public ClrmdEvidenceResult<ClrmdInt32FieldObservation> ReadInt32Field(
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field) =>
        session.ReadInt32Field(target, field);

    public ClrmdEvidenceResult<ClrmdNullableInt32FieldObservation> ReadNullableInt32Field(
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field) =>
        session.ReadNullableInt32Field(target, field);

    public ClrmdStringFieldObservation ReadStringField(
        ClrmdReferencedObjectInfo target,
        ClrmdInstanceFieldInfo field,
        int maximumCharacters) =>
        session.ReadStringField(target, field, maximumCharacters);
}
