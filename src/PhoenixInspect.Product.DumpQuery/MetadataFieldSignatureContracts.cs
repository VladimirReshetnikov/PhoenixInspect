using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Freezes the shared Core Field-form certificate projected into the public draft metadata contract.</summary>
/// <remarks>
/// This sealed draft identity binds full FieldSig consumption, the exact source bytes, the projected root, and every
/// Core counter needed to audit which bounded grammar form produced the result.
/// </remarks>
public sealed class MetadataFieldSignatureCertificateIdentity :
    IEquatable<MetadataFieldSignatureCertificateIdentity>
{
    private const string CanonicalDomain = "metadata-v2-field-signature-certificate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataFieldSignatureCertificateIdentity(
        string signatureBytesSha256,
        string rootSha256,
        int signatureByteCount,
        int header,
        int aggregateTypeCount,
        int aggregateGenericArgumentCount,
        int maximumObservedDepth,
        int projectedNodeCount,
        int maximumDeclaredGenericParameterCount,
        int maximumDeclaredParameterCount,
        int maximumDeclaredArrayRank)
    {
        SignatureBytesSha256 = signatureBytesSha256;
        RootSha256 = rootSha256;
        SignatureByteCount = signatureByteCount;
        Header = header;
        AggregateTypeCount = aggregateTypeCount;
        AggregateGenericArgumentCount = aggregateGenericArgumentCount;
        MaximumObservedDepth = maximumObservedDepth;
        ProjectedNodeCount = projectedNodeCount;
        MaximumDeclaredGenericParameterCount = maximumDeclaredGenericParameterCount;
        MaximumDeclaredParameterCount = maximumDeclaredParameterCount;
        MaximumDeclaredArrayRank = maximumDeclaredArrayRank;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(signatureBytesSha256, nameof(signatureBytesSha256));
        writer.WriteSha256(rootSha256, nameof(rootSha256));
        writer.WriteInt32(signatureByteCount);
        writer.WriteInt32(header);
        writer.WriteInt32(aggregateTypeCount);
        writer.WriteInt32(aggregateGenericArgumentCount);
        writer.WriteInt32(maximumObservedDepth);
        writer.WriteInt32(projectedNodeCount);
        writer.WriteInt32(maximumDeclaredGenericParameterCount);
        writer.WriteInt32(maximumDeclaredParameterCount);
        writer.WriteInt32(maximumDeclaredArrayRank);
        writer.WriteBoolean(true);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the SHA-256 digest of the complete draft FieldSig bytes.</summary>
    public string SignatureBytesSha256 { get; }

    /// <summary>Gets the canonical digest of the complete draft projected root.</summary>
    public string RootSha256 { get; }

    /// <summary>Gets the exact complete draft FieldSig byte count.</summary>
    public int SignatureByteCount { get; }

    /// <summary>Gets the exact Core Field-form draft header, always <c>0x06</c>.</summary>
    public int Header { get; }

    /// <summary>Gets the complete Core aggregate draft type-node count.</summary>
    public int AggregateTypeCount { get; }

    /// <summary>Gets the complete Core aggregate draft generic-argument count.</summary>
    public int AggregateGenericArgumentCount { get; }

    /// <summary>Gets the greatest Core recursive draft type depth observed.</summary>
    public int MaximumObservedDepth { get; }

    /// <summary>Gets the complete Core draft event-node count, including ARRAY shape events.</summary>
    public int ProjectedNodeCount { get; }

    /// <summary>Gets the greatest draft function-pointer generic-parameter count declared in the FieldSig.</summary>
    public int MaximumDeclaredGenericParameterCount { get; }

    /// <summary>Gets the greatest draft function-pointer parameter count declared in the FieldSig.</summary>
    public int MaximumDeclaredParameterCount { get; }

    /// <summary>Gets the greatest draft multidimensional-array rank declared in the FieldSig.</summary>
    public int MaximumDeclaredArrayRank { get; }

    /// <summary>Gets the explicit full-source-end observation retained by this exact draft certificate.</summary>
    public bool SourceEndObserved => true;

    /// <summary>Gets a defensive copy of the versioned canonical draft certificate bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft certificate.</summary>
    public string Sha256 { get; }

    internal static MetadataFieldSignatureCertificateIdentity Create(
        ImmutableArray<byte> signatureBytes,
        MetadataTypeSignatureNode root,
        BoundedEcmaSignatureCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (signatureBytes.IsDefaultOrEmpty ||
            certificate.Form != BoundedEcmaSignatureForm.Field ||
            certificate.Header != 0x06 ||
            certificate.SignatureByteCount != signatureBytes.Length ||
            certificate.Counters.InputByteCount != signatureBytes.Length ||
            certificate.Counters.ConsumedByteCount != signatureBytes.Length ||
            certificate.Counters.AggregateTypeCount is < 1 or > StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount ||
            certificate.Counters.AggregateGenericArgumentCount is < 0 or > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount ||
            certificate.Counters.MaximumObservedDepth is < 1 or > StaticFieldV2Limits.MaximumTypeSpecificationDepth ||
            certificate.Counters.ProjectedNodeCount < 1 ||
            certificate.Counters.MaximumDeclaredGenericParameterCount is < 0 or > StaticFieldV2Limits.MaximumGenericParameterCount ||
            certificate.Counters.MaximumDeclaredParameterCount is < 0 or > StaticFieldV2Limits.MaximumParameterCount ||
            certificate.Counters.MaximumDeclaredArrayRank is < 0 or > StaticFieldV2Limits.MaximumArrayRank)
        {
            throw new ArgumentException("A complete exact Core Field-form draft certificate is required.", nameof(certificate));
        }

        return new MetadataFieldSignatureCertificateIdentity(
            CanonicalReplayEncoding.ComputeSha256(signatureBytes.AsSpan()),
            root.Sha256,
            signatureBytes.Length,
            certificate.Header,
            certificate.Counters.AggregateTypeCount,
            certificate.Counters.AggregateGenericArgumentCount,
            certificate.Counters.MaximumObservedDepth,
            certificate.Counters.ProjectedNodeCount,
            certificate.Counters.MaximumDeclaredGenericParameterCount,
            certificate.Counters.MaximumDeclaredParameterCount,
            certificate.Counters.MaximumDeclaredArrayRank);
    }

    /// <summary>Tests whether another public draft certificate has identical canonical content.</summary>
    /// <param name="other">The other draft certificate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataFieldSignatureCertificateIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests an arbitrary object for canonical equality with this public draft certificate.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for the same canonical draft certificate.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataFieldSignatureCertificateIdentity);

    /// <summary>Computes a deterministic hash code from the canonical draft certificate bytes.</summary>
    /// <returns>A deterministic draft hash code.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Anchors one exact fully consumed FieldSig projection to its FieldDef and source-end draft identities.</summary>
/// <remarks>
/// The sealed canonical draft identity exists only after shared Core Field-form validation and exact resolution of
/// every referenced token. It never represents a retained prefix.
/// </remarks>
public sealed class MetadataFieldSignatureIdentity : IEquatable<MetadataFieldSignatureIdentity>
{
    private const string CanonicalDomain = "metadata-v2-field-signature-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signatureBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataFieldSignatureIdentity(
        StaticFieldDefinitionIdentity fieldDefinition,
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<byte> signatureBytes,
        MetadataFieldSignatureCertificateIdentity certificate,
        MetadataTypeSignatureNode root)
    {
        FieldDefinition = fieldDefinition;
        SourceEnds = sourceEnds;
        this.signatureBytes = signatureBytes;
        Certificate = certificate;
        Root = root;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(fieldDefinition.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(signatureBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(certificate.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(root.CanonicalBytes.AsSpan());
        writer.WriteBoolean(true);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact FieldDef draft identity that owns the signature.</summary>
    public StaticFieldDefinitionIdentity FieldDefinition { get; }

    /// <summary>Gets the exact metadata-table source-end draft identity used for every row check.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive copy of the complete exact draft FieldSig bytes.</summary>
    public ImmutableArray<byte> SignatureBytes => ExpressionV2ContractEncoding.Copy(signatureBytes);

    /// <summary>Gets the exact shared-Core Field-form draft certificate.</summary>
    public MetadataFieldSignatureCertificateIdentity Certificate { get; }

    /// <summary>Gets the complete resolved draft type-signature root.</summary>
    public MetadataTypeSignatureNode Root { get; }

    /// <summary>Gets the explicit source-end observation retained by this exact draft identity.</summary>
    public bool SourceEndObserved => true;

    /// <summary>Gets a defensive copy of the versioned canonical draft field-signature identity.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft field-signature identity.</summary>
    public string Sha256 { get; }

    internal static MetadataFieldSignatureIdentity Create(
        StaticFieldDefinitionIdentity fieldDefinition,
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeSignatureNode root,
        MetadataFieldSignatureCertificateIdentity certificate)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(certificate);
        var signatureBytes = fieldDefinition.Signature;
        var rowId = fieldDefinition.FieldDefinitionToken & 0x00FF_FFFF;
        if (!fieldDefinition.DeclaringType.MetadataModule.Equals(sourceEnds.SourceModule) ||
            !sourceEnds.ContainsTypeDefinitionToken(fieldDefinition.DeclaringType.TypeDefinitionToken) ||
            rowId <= 0 ||
            rowId > sourceEnds.FieldDefinitionRowCount ||
            certificate.SignatureByteCount != signatureBytes.Length ||
            !string.Equals(
                certificate.SignatureBytesSha256,
                CanonicalReplayEncoding.ComputeSha256(signatureBytes.AsSpan()),
                StringComparison.Ordinal) ||
            !string.Equals(certificate.RootSha256, root.Sha256, StringComparison.Ordinal))
        {
            throw new ArgumentException("The exact draft FieldSig anchors do not identify one complete source row and projection.");
        }
        return new MetadataFieldSignatureIdentity(
            fieldDefinition,
            sourceEnds,
            ExpressionV2ContractEncoding.Copy(signatureBytes),
            certificate,
            root);
    }

    /// <summary>Tests whether another exact draft FieldSig identity has identical canonical content.</summary>
    /// <param name="other">The other draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataFieldSignatureIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests an arbitrary object for canonical equality with this exact draft FieldSig identity.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for the same canonical draft identity.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataFieldSignatureIdentity);

    /// <summary>Computes a deterministic hash code from the canonical exact draft FieldSig bytes.</summary>
    /// <returns>A deterministic draft hash code.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes an exact, incomplete, bounded, or invalid FieldSig decode as one prefix-free public draft outcome.</summary>
/// <remarks>
/// The sealed canonical draft outcome always anchors the requested FieldDef and source ends. Only its exact alternative
/// exposes a <see cref="MetadataFieldSignatureIdentity"/>; every other alternative exposes no tree or certificate.
/// </remarks>
public sealed class MetadataFieldSignatureDecodeOutcome : IEquatable<MetadataFieldSignatureDecodeOutcome>
{
    private const string CanonicalDomain = "metadata-v2-field-signature-decode-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataFieldSignatureDecodeOutcome(
        MetadataSignatureDecodeResultKind kind,
        StaticFieldDefinitionIdentity fieldDefinition,
        MetadataSourceEndIdentity sourceEnds,
        MetadataFieldSignatureIdentity? identity,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        string? nonExactCode,
        string? invalidCode)
    {
        Kind = kind;
        FieldDefinition = fieldDefinition;
        SourceEnds = sourceEnds;
        Identity = identity;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        NonExactCode = nonExactCode;
        InvalidCode = invalidCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteLengthPrefixedBytes(fieldDefinition.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteBoolean(identity is not null);
        if (identity is not null)
        {
            writer.WriteLengthPrefixedBytes(identity.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(reachedBound is not null);
        if (reachedBound is not null)
        {
            writer.WriteString(reachedBound.Name);
            writer.WriteInt64(reachedBound.Value);
        }
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, nonExactCode);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, invalidCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact closed draft outcome kind.</summary>
    public MetadataSignatureDecodeResultKind Kind { get; }

    /// <summary>Gets the requested exact physical FieldDef draft identity.</summary>
    public StaticFieldDefinitionIdentity FieldDefinition { get; }

    /// <summary>Gets the exact metadata source-end draft identity governing the operation.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the complete exact draft identity, or null for every stop.</summary>
    public MetadataFieldSignatureIdentity? Identity { get; }

    /// <summary>Gets the projected draft root only for an exact result.</summary>
    public MetadataTypeSignatureNode? Root => Identity?.Root;

    /// <summary>Gets the Core Field-form draft certificate only for an exact result.</summary>
    public MetadataFieldSignatureCertificateIdentity? Certificate => Identity?.Certificate;

    /// <summary>Gets the reached draft operation bound only for a cap-plus-one result.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets exactly the draft bound limit plus one, otherwise zero.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the stable draft incomplete-evidence code only for a non-bound non-exact result.</summary>
    public string? NonExactCode { get; }

    /// <summary>Gets the stable draft invalid-decode code only for an invalid result.</summary>
    public string? InvalidCode { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Decodes one exact FieldDef's complete FieldSig under the shared bounded Core draft grammar.</summary>
    /// <param name="fieldDefinition">The exact FieldDef draft identity and complete signature bytes.</param>
    /// <param name="sourceEnds">The exact draft source ends that must contain the physical FieldDef row.</param>
    /// <param name="tokenCatalog">The detached draft token-resolution catalog for the same source ends.</param>
    /// <returns>
    /// A canonical prefix-free draft result. Exact results alone expose an identity, certificate, and projected root;
    /// incomplete, bounded, and invalid results retain none of those derived objects.
    /// </returns>
    public static MetadataFieldSignatureDecodeOutcome Decode(
        StaticFieldDefinitionIdentity fieldDefinition,
        MetadataSourceEndIdentity sourceEnds,
        MetadataSignatureTokenResolutionCatalog tokenCatalog)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(tokenCatalog);
        if (!fieldDefinition.DeclaringType.MetadataModule.Equals(sourceEnds.SourceModule) ||
            !tokenCatalog.SourceEnds.Equals(sourceEnds))
        {
            throw new ArgumentException("FieldSig draft anchors and token facts must belong to the same exact source ends.");
        }

        var rowId = fieldDefinition.FieldDefinitionToken & 0x00FF_FFFF;
        if (!sourceEnds.ContainsTypeDefinitionToken(fieldDefinition.DeclaringType.TypeDefinitionToken))
        {
            return Invalid(fieldDefinition, sourceEnds, "W8_FIELD_SIGNATURE_SOURCE_OWNER_OUT_OF_RANGE");
        }
        if (rowId <= 0 || rowId > sourceEnds.FieldDefinitionRowCount)
        {
            return Invalid(fieldDefinition, sourceEnds, "W8_FIELD_SIGNATURE_SOURCE_TOKEN_OUT_OF_RANGE");
        }

        var signatureBytes = fieldDefinition.Signature;
        var projection = MetadataSignatureProjectionAdapter.Decode(
            signatureBytes,
            BoundedEcmaSignatureForm.Field,
            tokenCatalog);
        if (projection.ReachedBound is not null)
        {
            return BoundReached(
                fieldDefinition,
                sourceEnds,
                projection.ReachedBound,
                projection.ObservedCount);
        }
        if (projection.Kind == MetadataSignatureDecodeResultKind.NonExact)
        {
            return Incomplete(fieldDefinition, sourceEnds, projection.Code!);
        }
        if (projection.Kind == MetadataSignatureDecodeResultKind.Invalid)
        {
            return Invalid(fieldDefinition, sourceEnds, projection.Code!);
        }

        var certificate = MetadataFieldSignatureCertificateIdentity.Create(
            signatureBytes,
            projection.Root!,
            projection.CoreCertificate!.Value);
        var identity = MetadataFieldSignatureIdentity.Create(
            fieldDefinition,
            sourceEnds,
            projection.Root!,
            certificate);
        return new MetadataFieldSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.Exact,
            fieldDefinition,
            sourceEnds,
            identity,
            null,
            0,
            null,
            null);
    }

    private static MetadataFieldSignatureDecodeOutcome BoundReached(
        StaticFieldDefinitionIdentity fieldDefinition,
        MetadataSourceEndIdentity sourceEnds,
        EvaluationDeterministicBound reachedBound,
        int observedCount)
    {
        if (reachedBound.Value >= int.MaxValue || observedCount != reachedBound.Value + 1)
        {
            throw new ArgumentException("A declared draft FieldSig bound and its cap-plus-one observation are required.");
        }
        return new MetadataFieldSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.NonExact,
            fieldDefinition,
            sourceEnds,
            null,
            reachedBound,
            observedCount,
            null,
            null);
    }

    private static MetadataFieldSignatureDecodeOutcome Incomplete(
        StaticFieldDefinitionIdentity fieldDefinition,
        MetadataSourceEndIdentity sourceEnds,
        string code)
    {
        ExpressionV2ContractEncoding.RequireDiagnosticCode(code, nameof(code));
        return new MetadataFieldSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.NonExact,
            fieldDefinition,
            sourceEnds,
            null,
            null,
            0,
            code,
            null);
    }

    private static MetadataFieldSignatureDecodeOutcome Invalid(
        StaticFieldDefinitionIdentity fieldDefinition,
        MetadataSourceEndIdentity sourceEnds,
        string code)
    {
        ExpressionV2ContractEncoding.RequireDiagnosticCode(code, nameof(code));
        return new MetadataFieldSignatureDecodeOutcome(
            MetadataSignatureDecodeResultKind.Invalid,
            fieldDefinition,
            sourceEnds,
            null,
            null,
            0,
            null,
            code);
    }

    /// <summary>Tests whether another public draft FieldSig outcome has identical canonical content.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataFieldSignatureDecodeOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests an arbitrary object for canonical equality with this public draft FieldSig outcome.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for the same canonical draft outcome.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataFieldSignatureDecodeOutcome);

    /// <summary>Computes a deterministic hash code from the canonical public draft outcome bytes.</summary>
    /// <returns>A deterministic draft hash code.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}
