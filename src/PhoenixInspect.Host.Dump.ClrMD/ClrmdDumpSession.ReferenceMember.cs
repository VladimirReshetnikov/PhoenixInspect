using System.Collections.Immutable;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Freezes the declaration facts of one directly declared object-reference field without naming a terminal member.
/// </summary>
/// <remarks>
/// This certificate is the intermediate-hop analogue of <see cref="ClrmdDeclaredDataMemberCertificate"/>: it
/// certifies only the outer reference FieldDef and the exact declared target TypeDef, so a member-chain evaluator
/// can resolve one link of an arbitrary-depth chain without pretending the next member is a decodable terminal.
/// Construction reads complete module metadata and never reads the reference value or a referenced object.
/// </remarks>
public sealed class ClrmdDeclaredReferenceMemberCertificate
{
    private const string CanonicalVersion = "clrmd-declared-reference-member-v1";

    private readonly ImmutableArray<byte> outerFieldSignature;
    private readonly string canonicalProjection;

    internal ClrmdDeclaredReferenceMemberCertificate(
        string rootTypeName,
        int rootTypeMetadataToken,
        ClrmdInstanceFieldInfo outerField,
        ImmutableArray<byte> outerFieldSignature,
        ClrmdDeclaredTypeInfo declaredTarget)
    {
        RootTypeName = rootTypeName ?? throw new ArgumentNullException(nameof(rootTypeName));
        RootTypeMetadataToken = rootTypeMetadataToken;
        OuterField = outerField ?? throw new ArgumentNullException(nameof(outerField));
        this.outerFieldSignature = outerFieldSignature;
        DeclaredTarget = declaredTarget ?? throw new ArgumentNullException(nameof(declaredTarget));
        canonicalProjection = CreateCanonicalProjection();
        Sha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalProjection))).ToLowerInvariant();
    }

    /// <summary>Gets the exact runtime type name of the owner the field was certified against.</summary>
    public string RootTypeName { get; }

    /// <summary>Gets the non-nil TypeDef token of the owner type.</summary>
    public int RootTypeMetadataToken { get; }

    /// <summary>Gets the immutable descriptor of the certified outer reference field.</summary>
    public ClrmdInstanceFieldInfo OuterField { get; }

    /// <summary>Gets an immutable copy of the exact outer FieldDef signature blob.</summary>
    public ImmutableArray<byte> OuterFieldSignature =>
        ImmutableArray.CreateRange(outerFieldSignature.AsSpan().ToArray());

    /// <summary>Gets the exact declared target TypeDef the reference field stores.</summary>
    public ClrmdDeclaredTypeInfo DeclaredTarget { get; }

    /// <summary>Gets the lowercase SHA-256 identity of the canonical certificate projection.</summary>
    public string Sha256 { get; }

    /// <summary>Produces the injective versioned representation of the certified reference declaration.</summary>
    /// <returns>A deterministic length-delimited projection of every retained identity.</returns>
    public string ToCanonicalReplayProjection() => canonicalProjection;

    private string CreateCanonicalProjection()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, RootTypeName);
        Append(builder, RootTypeMetadataToken.ToString("x8", CultureInfo.InvariantCulture));
        Append(builder, OuterField.ToCanonicalReplayProjection());
        Append(builder, Convert.ToHexString(outerFieldSignature.AsSpan()));
        Append(builder, DeclaredTarget.RuntimeModule.SourceId);
        Append(builder, DeclaredTarget.ModuleContent.MetadataSha256);
        Append(builder, DeclaredTarget.MetadataToken.ToString("x8", CultureInfo.InvariantCulture));
        Append(builder, DeclaredTarget.Name);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }
}

public sealed partial class ClrmdDumpSession
{
    /// <summary>
    /// Certifies one directly declared object-reference field as an intermediate member-chain hop.
    /// </summary>
    /// <param name="root">The exact host-selected owner object from this session.</param>
    /// <param name="referenceFieldName">The ordinal name of the directly declared reference FieldDef.</param>
    /// <returns>
    /// An exact immutable reference-declaration certificate, or a typed partial, unavailable, conflicting, or
    /// invalid result retaining every counted read completed before failure.
    /// </returns>
    /// <remarks>
    /// The admission rules deliberately mirror the outer-reference prefix of
    /// <see cref="CertifyDeclaredDataMember"/>: the field must be a directly declared, uniquely named, ordinary
    /// instance field whose signature names a non-generic same-module TypeDef that agrees with the runtime type
    /// catalog. The operation never reads the reference value, validates a referenced object, or names a terminal.
    /// The frozen pair certification is mirrored rather than shared so its dispositions cannot change.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceFieldName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The member name exceeds the deterministic name bound.</exception>
    public ClrmdEvidenceResult<ClrmdDeclaredReferenceMemberCertificate> CertifyDeclaredReferenceMember(
        ClrmdHeapObjectInfo root,
        string referenceFieldName)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(root);
        ValidateDeclaredMemberName(referenceFieldName, nameof(referenceFieldName));

        if (root.Snapshot != Snapshot)
        {
            return ReferenceFailure(ClrmdEvidenceStatus.Conflict, ClrmdValueIssue.SnapshotMismatch);
        }

        var outerResult = GetInstanceField(root, referenceFieldName);
        if (outerResult.Status != ClrmdEvidenceStatus.Exact || outerResult.Value is null)
        {
            return ReferenceFailure(
                outerResult.Status,
                outerResult.Issue,
                appliedBounds: outerResult.AppliedBounds);
        }

        var outerField = outerResult.Value;
        if (!outerField.IsObjectReference)
        {
            return ReferenceFailure(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.TypeMismatch,
                appliedBounds: outerResult.AppliedBounds);
        }

        if (!_runtimeModules.TryGetValue(root.Module.Identity, out var runtimeModule))
        {
            return ReferenceFailure(ClrmdEvidenceStatus.Unavailable, ClrmdValueIssue.ModuleUnavailable);
        }

        var metadataResult = ReadCompleteMetadata(root.Module);
        if (metadataResult.Status != ClrmdEvidenceStatus.Exact || metadataResult.Value is null)
        {
            return ReferenceFailure(
                metadataResult.Status,
                metadataResult.Issue,
                evidence: metadataResult.Evidence,
                appliedBounds: outerResult.AppliedBounds);
        }

        var metadataImage = metadataResult.Value;
        var evidence = metadataResult.Evidence;
        var appliedBounds = outerResult.AppliedBounds;
        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataImage.Bytes);
            var reader = provider.GetMetadataReader();
            var rootTypeHandle = RequireTypeDefinition(reader, root.TypeMetadataToken);
            if (rootTypeHandle.IsNil)
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence,
                    appliedBounds);
            }

            var rootDefinition = reader.GetTypeDefinition(rootTypeHandle);
            var metadataRootName = GetFullTypeName(reader, rootTypeHandle);
            if (!string.Equals(metadataRootName, root.TypeName, StringComparison.Ordinal))
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence,
                    appliedBounds);
            }

            var outerHandle = RequireFieldDefinition(reader, outerField.MetadataToken);
            if (outerHandle.IsNil)
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence,
                    appliedBounds);
            }

            var outerDefinition = reader.GetFieldDefinition(outerHandle);
            if (outerDefinition.GetDeclaringType() != rootTypeHandle ||
                !string.Equals(reader.GetString(outerDefinition.Name), referenceFieldName, StringComparison.Ordinal))
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence,
                    appliedBounds);
            }

            var outerNameCount = rootDefinition.GetFields().Count(handle =>
                string.Equals(
                    reader.GetString(reader.GetFieldDefinition(handle).Name),
                    referenceFieldName,
                    StringComparison.Ordinal));
            if (outerNameCount != 1)
            {
                return ReferenceFailure(
                    outerNameCount == 0 ? ClrmdEvidenceStatus.Unavailable : ClrmdEvidenceStatus.Conflict,
                    outerNameCount == 0 ? ClrmdValueIssue.FieldUnavailable : ClrmdValueIssue.AmbiguousMatch,
                    evidence,
                    appliedBounds);
            }

            if (!IsOrdinaryInstanceField(outerDefinition.Attributes))
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemberShapeUnsupported,
                    evidence,
                    appliedBounds);
            }

            var signatureProvider = new DeclaredMemberSignatureProvider(reader);
            var outerType = outerDefinition.DecodeSignature(signatureProvider, genericContext: null);
            if (outerType.Kind != DeclaredSignatureKind.TypeDefinition ||
                outerType.Handle.Kind != HandleKind.TypeDefinition)
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemberShapeUnsupported,
                    evidence,
                    appliedBounds);
            }

            var targetTypeHandle = (TypeDefinitionHandle)outerType.Handle;
            var targetDefinition = reader.GetTypeDefinition(targetTypeHandle);
            if (targetDefinition.GetGenericParameters().Count != 0)
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemberShapeUnsupported,
                    evidence,
                    appliedBounds);
            }

            var targetTypeToken = MetadataTokens.GetToken(targetTypeHandle);
            var targetTypeName = GetFullTypeName(reader, targetTypeHandle);
            var runtimeTarget = runtimeModule.GetTypeByName(targetTypeName);
            if (runtimeTarget is null)
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.TypeUnavailable,
                    evidence,
                    appliedBounds);
            }

            if (runtimeTarget.MetadataToken != targetTypeToken ||
                !string.Equals(runtimeTarget.Name, targetTypeName, StringComparison.Ordinal))
            {
                return ReferenceFailure(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence,
                    appliedBounds);
            }

            var moduleContent = ModuleContentIdentity.FromMetadata(
                reader.GetGuid(reader.GetModuleDefinition().Mvid),
                metadataImage.Bytes.AsSpan());
            var declaredType = new ClrmdDeclaredTypeInfo(
                root.Module.Identity,
                moduleContent,
                targetTypeToken,
                targetTypeName);
            var certificate = new ClrmdDeclaredReferenceMemberCertificate(
                root.TypeName,
                root.TypeMetadataToken,
                outerField,
                ImmutableArray.Create(reader.GetBlobBytes(outerDefinition.Signature)),
                declaredType);
            return ClrmdEvidenceResult<ClrmdDeclaredReferenceMemberCertificate>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                certificate,
                evidence,
                appliedBounds);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or
            ArgumentOutOfRangeException or OverflowException)
        {
            return ReferenceFailure(ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, evidence, appliedBounds);
        }
    }

    /// <summary>
    /// Projects a validated referenced object into the compatibility shape consumed by existing instance engines,
    /// so a member-chain evaluator can continue from an intermediate hop.
    /// </summary>
    /// <param name="target">The exact validated referenced object from this session.</param>
    /// <returns>
    /// An exact direct-address projection after the runtime object, method table, TypeDef, name, and module still
    /// agree with the validated identity; otherwise a typed unavailable, conflict, or invalid result. The projection
    /// uses zero for the legacy handle slot and the non-handle marker <c>ChainReferencedObject</c>.
    /// </returns>
    /// <remarks>
    /// The operation performs one direct heap lookup at the already validated address and reuses the reference and
    /// header reads carried by <paramref name="target"/> as detached evidence. The caller must retain the chain-step
    /// provenance separately.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public ClrmdEvidenceResult<ClrmdHeapObjectInfo> ProjectReferencedObjectForInstanceEvaluation(
        ClrmdReferencedObjectInfo target)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(target);
        if (target.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        Microsoft.Diagnostics.Runtime.ClrObject runtimeObject;
        Microsoft.Diagnostics.Runtime.ClrType? runtimeType;
        try
        {
            runtimeObject = _runtime.Heap.GetObject(target.Address);
            runtimeType = runtimeObject.Type;
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception) || exception is OverflowException)
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        if (!runtimeObject.IsValid || runtimeType is null)
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.ObjectUnavailable);
        }

        if (runtimeType.MethodTable != target.MethodTable ||
            runtimeType.MetadataToken != target.TypeMetadataToken ||
            !string.Equals(runtimeType.Name, target.TypeName, StringComparison.Ordinal))
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        var result = new ClrmdHeapObjectInfo(
            Snapshot,
            target.Address,
            target.TypeName,
            target.TypeMetadataToken,
            target.MethodTable,
            rootAddress: 0,
            rootKind: nameof(ClrmdHeapObjectSelectionKind.ChainReferencedObject),
            target.Module,
            target.Evidence,
            ClrmdHeapObjectSelectionKind.ChainReferencedObject);
        return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            result,
            target.Evidence);
    }

    private static ClrmdEvidenceResult<ClrmdDeclaredReferenceMemberCertificate> ReferenceFailure(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ImmutableArray<MemoryReadResult> evidence = default,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds = default) =>
        ClrmdEvidenceResult<ClrmdDeclaredReferenceMemberCertificate>.Create(
            status,
            issue,
            value: null,
            evidence.IsDefault ? ImmutableArray<MemoryReadResult>.Empty : evidence,
            appliedBounds.IsDefault ? ImmutableArray<EvaluationDeterministicBound>.Empty : appliedBounds);
}
