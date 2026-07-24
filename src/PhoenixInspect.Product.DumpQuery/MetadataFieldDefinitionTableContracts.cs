using System.Collections.Immutable;
using System.Reflection;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies the physical FieldDef accessibility encoded by the low three FieldAttributes bits.</summary>
/// <remarks>
/// The draft discriminator is a pure physical decoding of the reserved three-bit field-access mask. It makes no
/// compiler-visibility, evaluator-admission, or source-addressability assertion.
/// </remarks>
public enum MetadataFieldAccessibility
{
    /// <summary>The field is referenceable only from the declaring module's own metadata.</summary>
    CompilerControlled = 0,

    /// <summary>The field is referenceable only from the declaring type.</summary>
    Private = 1,

    /// <summary>The field is referenceable from derived types inside the declaring assembly.</summary>
    FamilyAndAssembly = 2,

    /// <summary>The field is referenceable from anywhere inside the declaring assembly.</summary>
    Assembly = 3,

    /// <summary>The field is referenceable from derived types in any assembly.</summary>
    Family = 4,

    /// <summary>The field is referenceable from derived types or from anywhere inside the declaring assembly.</summary>
    FamilyOrAssembly = 5,

    /// <summary>The field is referenceable from anywhere.</summary>
    Public = 6,
}

/// <summary>Classifies one complete authority-joined FieldDef declaration-table draft proof.</summary>
/// <remarks>Every non-exact or invalid draft result exposes no derived FieldDef row prefix.</remarks>
public enum MetadataFieldDefinitionTableResultKind
{
    /// <summary>Every physical FieldDef row, declaring owner, and signature grammar fact is exact.</summary>
    Exact = 1,

    /// <summary>A required complete source or signature operation stopped before exact projection.</summary>
    NonExact = 2,

    /// <summary>Complete supplied evidence contradicted source, ownership, or signature invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one FieldDef declaration-table draft proof.</summary>
/// <remarks>The draft issue remains available without retaining any derived row candidate.</remarks>
public enum MetadataFieldDefinitionTableIssue
{
    /// <summary>No issue applies to an exact complete catalog.</summary>
    None = 0,

    /// <summary>The exact source FieldDef table end crossed the admitted complete row count.</summary>
    TableRowBoundReached = 1,

    /// <summary>The supplied FieldDef observations were unavailable or stopped before the exact source end.</summary>
    TableIncomplete = 2,

    /// <summary>The supplied FieldDef observation count exceeded the exact source end.</summary>
    TableRowCountConflict = 3,

    /// <summary>The supplied FieldDef tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 4,

    /// <summary>A physical FieldDef observation belonged to another metadata module.</summary>
    SourceMismatch = 5,

    /// <summary>The complete definition-authority prerequisite stopped without an exact authority catalog.</summary>
    DefinitionAuthorityNonExact = 6,

    /// <summary>The complete definition-authority prerequisite retained contradictory evidence.</summary>
    DefinitionAuthorityInvalid = 7,

    /// <summary>One physical FieldDef token was owned by no authority-issued TypeDef.</summary>
    DeclaringTypeDefinitionMissing = 8,

    /// <summary>Authority FieldDef ownership did not cover the complete token set exactly once.</summary>
    OwnershipCoverageMismatch = 9,

    /// <summary>A complete FieldDef signature was structurally invalid or trailed its shared-grammar source end.</summary>
    SignatureInvalid = 10,

    /// <summary>One exact FieldDef signature crossed a shared bounded-grammar cap.</summary>
    SignatureBoundReached = 11,
}

/// <summary>Freezes physical columns needed for one FieldDef declaration draft projection.</summary>
/// <remarks>
/// This sealed draft observation retains every physical FieldDef table column after heap decoding, including the
/// complete field-signature blob. It carries no caller-authored declaring TypeDef; ownership is derived only from the
/// complete physical definition authority. It makes no constant, layout, marshalling, or RVA-content assertion.
/// </remarks>
public sealed class MetadataFieldDefinitionRowObservationIdentity :
    IEquatable<MetadataFieldDefinitionRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-fielddef-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private const int FieldAccessibilityMask = 0x0007;
    private const int ReservedFieldAccessibility = 0x0007;
    private readonly ImmutableArray<byte> signatureBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataFieldDefinitionRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int fieldDefinitionToken,
        int attributes,
        string name,
        ImmutableArray<byte> signatureBytes)
    {
        MetadataModule = metadataModule;
        FieldDefinitionToken = fieldDefinitionToken;
        Attributes = attributes;
        Name = name;
        this.signatureBytes = signatureBytes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(fieldDefinitionToken);
        writer.WriteInt32(attributes);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(signatureBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the physical draft FieldDef row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact non-nil physical draft FieldDef token.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the exact raw physical draft FieldAttributes bits.</summary>
    public int Attributes { get; }

    /// <summary>Gets the exact decoded non-empty physical draft FieldDef name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the complete physical draft FieldDef signature blob.</summary>
    public ImmutableArray<byte> SignatureBytes => ExpressionV2ContractEncoding.Copy(signatureBytes);

    /// <summary>Gets a defensive copy of the versioned canonical draft observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft observation.</summary>
    public string Sha256 { get; }

    internal ImmutableArray<byte> SignatureBytesCore => signatureBytes;

    /// <summary>Creates one physical-column-only FieldDef row draft observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the physical row.</param>
    /// <param name="fieldDefinitionToken">The exact non-nil FieldDef token.</param>
    /// <param name="attributes">
    /// The exact raw FieldAttributes bits. The reserved three-bit accessibility encoding is rejected because no
    /// physical field can name it.
    /// </param>
    /// <param name="name">The exact decoded bounded non-empty FieldDef name.</param>
    /// <param name="signatureBytes">
    /// The complete initialized FieldDef signature blob, bounded by the shared TypeSpec byte cap. Grammar validity is
    /// established later by the complete catalog and is not asserted here.
    /// </param>
    /// <returns>A sealed immutable physical draft row with no caller-authored declaring type.</returns>
    public static MetadataFieldDefinitionRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int fieldDefinitionToken,
        int attributes,
        string name,
        ImmutableArray<byte> signatureBytes)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        if (attributes is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attributes),
                "The raw FieldAttributes value must fit its physical unsigned 16-bit column.");
        }
        if ((attributes & FieldAccessibilityMask) == ReservedFieldAccessibility)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attributes),
                "The raw FieldAttributes accessibility must be one of the seven declared physical encodings.");
        }
        if (signatureBytes.IsDefault ||
            signatureBytes.Length > StaticFieldV2Limits.MaximumTypeSpecificationByteCount)
        {
            throw new ArgumentException(
                "An initialized FieldDef signature of at most " +
                $"{StaticFieldV2Limits.MaximumTypeSpecificationByteCount} bytes is required.",
                nameof(signatureBytes));
        }

        return new MetadataFieldDefinitionRowObservationIdentity(
            metadataModule,
            fieldDefinitionToken,
            attributes,
            name,
            ExpressionV2ContractEncoding.Copy(signatureBytes));
    }

    /// <summary>Tests canonical equality between two physical FieldDef draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataFieldDefinitionRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataFieldDefinitionRowObservationIdentity);

    /// <summary>Computes a hash code from the immutable physical FieldDef draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one FieldDef declaration after exact ownership and shared-grammar draft validation.</summary>
/// <remarks>
/// This sealed draft identity can be issued only by the complete catalog. Its declaring TypeDef is the authority-issued
/// identity that physically owns the FieldDef, and its signature was consumed completely by the shared Core grammar.
/// Every exposed flag is a pure decoding of the retained physical FieldAttributes column.
/// </remarks>
public sealed class MetadataFieldDefinitionTableRowIdentity :
    IEquatable<MetadataFieldDefinitionTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-fielddef-table-row";
    private const int CanonicalSchemaVersion = 1;
    private const int FieldAccessibilityMask = 0x0007;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataFieldDefinitionTableRowIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataFieldDefinitionRowObservationIdentity observation,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        DeclaringTypeDefinition = declaringTypeDefinition;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(observation.CanonicalBytes.AsSpan());
        writer.WriteString(declaringTypeDefinition.Sha256);
        writer.WriteInt32(declaringTypeDefinition.TypeDefinitionToken);
        writer.WriteInt32((int)DeclaredAccessibility);
        writer.WriteBoolean(IsStatic);
        writer.WriteBoolean(IsInitOnly);
        writer.WriteBoolean(IsLiteral);
        writer.WriteBoolean(HasFieldRva);
        writer.WriteBoolean(HasDefault);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source-end draft identity governing the physical row and its declaring type.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical FieldDef draft observation.</summary>
    public MetadataFieldDefinitionRowObservationIdentity Observation { get; }

    /// <summary>Gets the authority-issued TypeDef that physically owns this exact draft FieldDef.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the exact physical draft FieldDef token.</summary>
    public int FieldDefinitionToken => Observation.FieldDefinitionToken;

    /// <summary>Gets the exact decoded non-empty physical draft FieldDef name.</summary>
    public string Name => Observation.Name;

    /// <summary>Gets the exact raw physical draft FieldAttributes bits.</summary>
    public int Attributes => Observation.Attributes;

    /// <summary>Gets a defensive copy of the complete exact draft FieldDef signature.</summary>
    public ImmutableArray<byte> SignatureBytes => Observation.SignatureBytes;

    /// <summary>Gets the exact declaring TypeDef token derived from complete physical draft ownership.</summary>
    public int DeclaringTypeDefinitionToken => DeclaringTypeDefinition.TypeDefinitionToken;

    /// <summary>Gets whether the physical draft row has FieldAttributes.Static.</summary>
    public bool IsStatic => (Attributes & (int)FieldAttributes.Static) != 0;

    /// <summary>Gets whether the physical draft row is an instance declaration.</summary>
    public bool IsInstance => !IsStatic;

    /// <summary>Gets whether the physical draft row has FieldAttributes.InitOnly.</summary>
    public bool IsInitOnly => (Attributes & (int)FieldAttributes.InitOnly) != 0;

    /// <summary>Gets whether the physical draft row has FieldAttributes.Literal.</summary>
    public bool IsLiteral => (Attributes & (int)FieldAttributes.Literal) != 0;

    /// <summary>Gets whether the physical draft row has FieldAttributes.HasFieldRVA.</summary>
    public bool HasFieldRva => (Attributes & (int)FieldAttributes.HasFieldRVA) != 0;

    /// <summary>Gets whether the physical draft row has FieldAttributes.HasDefault.</summary>
    public bool HasDefault => (Attributes & (int)FieldAttributes.HasDefault) != 0;

    /// <summary>Gets the physical draft accessibility decoded from the low three FieldAttributes bits.</summary>
    public MetadataFieldAccessibility DeclaredAccessibility => (Attributes & FieldAccessibilityMask) switch
    {
        0x0000 => MetadataFieldAccessibility.CompilerControlled,
        0x0001 => MetadataFieldAccessibility.Private,
        0x0002 => MetadataFieldAccessibility.FamilyAndAssembly,
        0x0003 => MetadataFieldAccessibility.Assembly,
        0x0004 => MetadataFieldAccessibility.Family,
        0x0005 => MetadataFieldAccessibility.FamilyOrAssembly,
        _ => MetadataFieldAccessibility.Public,
    };

    /// <summary>Gets a defensive copy of the versioned canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row.</summary>
    public string Sha256 { get; }

    internal static MetadataFieldDefinitionTableRowIdentity Create(
        object mintCapability,
        MetadataSourceEndIdentity sourceEnds,
        MetadataFieldDefinitionRowObservationIdentity observation,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition,
        BoundedEcmaSignatureCertificate certificate)
    {
        if (!MetadataFieldDefinitionTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A derived FieldDef row requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(declaringTypeDefinition);
        if (!declaringTypeDefinition.FieldDefinitionTokens.Contains(observation.FieldDefinitionToken))
        {
            throw new ArgumentException(
                "The authority-issued declaring TypeDef must physically own the FieldDef.",
                nameof(declaringTypeDefinition));
        }
        if (certificate.Form != BoundedEcmaSignatureForm.Field ||
            certificate.SignatureByteCount != observation.SignatureBytesCore.Length ||
            certificate.Counters.ConsumedByteCount != observation.SignatureBytesCore.Length)
        {
            throw new ArgumentException("A matching exact shared-grammar FieldDef draft certificate is required.");
        }

        return new MetadataFieldDefinitionTableRowIdentity(sourceEnds, observation, declaringTypeDefinition);
    }

    /// <summary>Tests canonical equality between two complete FieldDef declaration draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataFieldDefinitionTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete FieldDef declaration draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataFieldDefinitionTableRowIdentity);

    /// <summary>Computes a hash code from the immutable complete FieldDef declaration draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes a complete authority-joined FieldDef declaration-table draft projection.</summary>
/// <remarks>
/// This sealed draft catalog requires an exact physical definition authority and exact RID-complete FieldDef rows. It
/// proves that authority TypeDef ownership covers every FieldDef exactly once and validates every signature with the
/// shared bounded Core grammar before issuing any row, so every stop remains prefix-free.
/// </remarks>
public sealed class MetadataFieldDefinitionTableCatalogIdentity :
    IEquatable<MetadataFieldDefinitionTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-fielddef-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private static readonly BoundedEcmaSignatureLimits SignatureLimits = new(
        StaticFieldV2Limits.MaximumTypeSpecificationByteCount,
        StaticFieldV2Limits.MaximumTypeSpecificationDepth,
        StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
        StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
        StaticFieldV2Limits.MaximumGenericParameterCount,
        StaticFieldV2Limits.MaximumParameterCount,
        StaticFieldV2Limits.MaximumLocalCount,
        StaticFieldV2Limits.MaximumArrayRank);

    private readonly ImmutableArray<MetadataFieldDefinitionTableRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataFieldDefinitionTableCatalogIdentity(
        MetadataFieldDefinitionTableResultKind resultKind,
        MetadataFieldDefinitionTableIssue issue,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataFieldDefinitionTableRowIdentity> rows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        DefinitionAuthority = definitionAuthority;
        this.rows = rows;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(definitionAuthority.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft FieldDef table is exact, non-exact, or invalid.</summary>
    public MetadataFieldDefinitionTableResultKind ResultKind { get; }

    /// <summary>Gets the typed draft issue, or none for an exact result.</summary>
    public MetadataFieldDefinitionTableIssue Issue { get; }

    /// <summary>Gets the complete physical definition-authority draft catalog supplying every declaring owner.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets the exact source ends inherited from the physical definition-authority draft catalog.</summary>
    public MetadataSourceEndIdentity SourceEnds => DefinitionAuthority.SourceEnds;

    /// <summary>Gets a defensive RID-order copy of exact derived FieldDef draft rows, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataFieldDefinitionTableRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the reached draft bound for a cap-plus-one or propagated non-exact result, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related draft supplied count or cap-plus-one observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the FieldDef or prerequisite token related to the draft issue, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft catalog.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete authority-joined FieldDef declaration-table draft projection.</summary>
    /// <param name="definitionAuthority">The complete physical definition-authority draft prerequisite.</param>
    /// <param name="observations">
    /// Every physical FieldDef row in RID order; default denotes unavailable acquisition when the source is non-empty.
    /// </param>
    /// <returns>An exact catalog, a prefix-free non-exact stop, or a factless invalid draft result.</returns>
    public static MetadataFieldDefinitionTableCatalogIdentity Create(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataFieldDefinitionRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(definitionAuthority);
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.NonExact)
        {
            return NonExact(
                definitionAuthority,
                MetadataFieldDefinitionTableIssue.DefinitionAuthorityNonExact,
                definitionAuthority.ReachedBound,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.Invalid)
        {
            return Invalid(
                definitionAuthority,
                MetadataFieldDefinitionTableIssue.DefinitionAuthorityInvalid,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }

        var sourceCount = definitionAuthority.SourceEnds.FieldDefinitionRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumFieldDefinitionRowCount)
        {
            return NonExact(
                definitionAuthority,
                MetadataFieldDefinitionTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.FieldDefinitionRowCountBoundName,
                    StaticFieldV2Limits.MaximumFieldDefinitionRowCount),
                StaticFieldV2Limits.MaximumFieldDefinitionRowCount + 1,
                null);
        }
        if (observations.IsDefault && sourceCount == 0)
        {
            observations = ImmutableArray<MetadataFieldDefinitionRowObservationIdentity>.Empty;
        }
        if (observations.IsDefault || observations.Length < sourceCount)
        {
            return NonExact(
                definitionAuthority,
                MetadataFieldDefinitionTableIssue.TableIncomplete,
                null,
                observations.IsDefault ? 0 : observations.Length,
                null);
        }
        if (observations.Length > sourceCount)
        {
            return Invalid(
                definitionAuthority,
                MetadataFieldDefinitionTableIssue.TableRowCountConflict,
                observations.Length,
                null);
        }

        var copied = observations.IsEmpty
            ? ImmutableArray<MetadataFieldDefinitionRowObservationIdentity>.Empty
            : ImmutableArray.CreateRange(observations);
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null || observation.FieldDefinitionToken != (0x0400_0000 | checked(index + 1)))
            {
                return Invalid(
                    definitionAuthority,
                    MetadataFieldDefinitionTableIssue.PhysicalOrderInvalid,
                    copied.Length,
                    observation?.FieldDefinitionToken);
            }
        }
        foreach (var observation in copied)
        {
            if (!observation.MetadataModule.Equals(definitionAuthority.SourceEnds.SourceModule))
            {
                return Invalid(
                    definitionAuthority,
                    MetadataFieldDefinitionTableIssue.SourceMismatch,
                    copied.Length,
                    observation.FieldDefinitionToken);
            }
        }

        var ownershipIssue = BuildOwnershipMap(
            definitionAuthority,
            sourceCount,
            out var declaringTypes,
            out var ownershipRelatedToken);
        if (ownershipIssue != MetadataFieldDefinitionTableIssue.None)
        {
            return Invalid(definitionAuthority, ownershipIssue, sourceCount, ownershipRelatedToken);
        }

        var certificates = new BoundedEcmaSignatureCertificate[copied.Length];
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            var decode = BoundedEcmaSignatureProjection.Decode(
                observation.SignatureBytesCore.AsSpan(),
                BoundedEcmaSignatureForm.Field,
                SignatureLimits);
            if (decode.Kind == BoundedEcmaSignatureDecodeKind.BoundReached)
            {
                var mapped = MapSignatureBound(decode.ReachedBound, decode.Counters);
                return NonExact(
                    definitionAuthority,
                    MetadataFieldDefinitionTableIssue.SignatureBoundReached,
                    mapped.Bound,
                    mapped.ObservedCount,
                    observation.FieldDefinitionToken);
            }
            if (decode.Kind == BoundedEcmaSignatureDecodeKind.Invalid ||
                decode.Certificate is not { } certificate ||
                certificate.Form != BoundedEcmaSignatureForm.Field ||
                certificate.SignatureByteCount != observation.SignatureBytesCore.Length ||
                certificate.Counters.ConsumedByteCount != observation.SignatureBytesCore.Length)
            {
                return Invalid(
                    definitionAuthority,
                    MetadataFieldDefinitionTableIssue.SignatureInvalid,
                    observation.SignatureBytesCore.Length,
                    observation.FieldDefinitionToken);
            }
            certificates[index] = certificate;
        }

        var derived = ImmutableArray.CreateBuilder<MetadataFieldDefinitionTableRowIdentity>(copied.Length);
        for (var index = 0; index < copied.Length; index++)
        {
            derived.Add(MetadataFieldDefinitionTableRowIdentity.Create(
                RowMintCapability,
                definitionAuthority.SourceEnds,
                copied[index],
                declaringTypes[index]!,
                certificates[index]));
        }
        return new MetadataFieldDefinitionTableCatalogIdentity(
            MetadataFieldDefinitionTableResultKind.Exact,
            MetadataFieldDefinitionTableIssue.None,
            definitionAuthority,
            derived.MoveToImmutable(),
            null,
            0,
            null);
    }

    /// <summary>Finds the exact derived draft row for one physical FieldDef token.</summary>
    /// <param name="fieldDefinitionToken">The physical FieldDef token to look up.</param>
    /// <returns>The exact derived draft row, or null for a non-exact catalog or an unknown token.</returns>
    public MetadataFieldDefinitionTableRowIdentity? FindRow(int fieldDefinitionToken)
    {
        if (ResultKind != MetadataFieldDefinitionTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(fieldDefinitionToken, 0x04))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(fieldDefinitionToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Projects the exact derived draft rows owned by one authority-issued declaring TypeDef.</summary>
    /// <param name="declaringType">The authority-issued TypeDef whose declared FieldDef rows are requested.</param>
    /// <returns>
    /// The derived draft rows in declared ownership order, or an empty array for a non-exact catalog or a TypeDef that
    /// this catalog's own definition authority did not issue.
    /// </returns>
    public ImmutableArray<MetadataFieldDefinitionTableRowIdentity> RowsForDeclaringTypeOrEmpty(
        MetadataTypeDefinitionAuthorityIdentity declaringType)
    {
        if (ResultKind != MetadataFieldDefinitionTableResultKind.Exact || declaringType is null)
        {
            return ImmutableArray<MetadataFieldDefinitionTableRowIdentity>.Empty;
        }

        var issued = DefinitionAuthority.ExactTypeDefinitionOrDefault(declaringType.TypeDefinitionToken);
        if (issued is null || !issued.Equals(declaringType))
        {
            return ImmutableArray<MetadataFieldDefinitionTableRowIdentity>.Empty;
        }

        var owned = issued.FieldDefinitionTokens;
        var projected = ImmutableArray.CreateBuilder<MetadataFieldDefinitionTableRowIdentity>(owned.Length);
        foreach (var token in owned)
        {
            if (FindRow(token) is not { } row)
            {
                return ImmutableArray<MetadataFieldDefinitionTableRowIdentity>.Empty;
            }
            projected.Add(row);
        }
        return projected.MoveToImmutable();
    }

    /// <summary>Tests canonical equality between two complete FieldDef table draft projections.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataFieldDefinitionTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete FieldDef table draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataFieldDefinitionTableCatalogIdentity);

    /// <summary>Computes a hash code from the immutable complete FieldDef table draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    private static MetadataFieldDefinitionTableIssue BuildOwnershipMap(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        int fieldCount,
        out MetadataTypeDefinitionAuthorityIdentity?[] declaringTypes,
        out int? relatedMetadataToken)
    {
        declaringTypes = new MetadataTypeDefinitionAuthorityIdentity?[fieldCount];
        relatedMetadataToken = null;
        foreach (var typeDefinition in definitionAuthority.TypeDefinitions)
        {
            foreach (var fieldToken in typeDefinition.FieldDefinitionTokens)
            {
                if (!CanonicalReplayEncoding.IsMetadataTokenForTable(fieldToken, 0x04))
                {
                    relatedMetadataToken = fieldToken;
                    return MetadataFieldDefinitionTableIssue.OwnershipCoverageMismatch;
                }

                var rowId = CanonicalReplayEncoding.MetadataTokenRowId(fieldToken);
                if (rowId <= 0 || rowId > fieldCount || declaringTypes[rowId - 1] is not null)
                {
                    relatedMetadataToken = fieldToken;
                    return MetadataFieldDefinitionTableIssue.OwnershipCoverageMismatch;
                }
                declaringTypes[rowId - 1] = typeDefinition;
            }
        }
        for (var index = 0; index < declaringTypes.Length; index++)
        {
            if (declaringTypes[index] is null)
            {
                relatedMetadataToken = 0x0400_0000 | checked(index + 1);
                return MetadataFieldDefinitionTableIssue.DeclaringTypeDefinitionMissing;
            }
        }
        return MetadataFieldDefinitionTableIssue.None;
    }

    private static SignatureBound MapSignatureBound(
        BoundedEcmaSignatureBoundKind boundKind,
        BoundedEcmaSignatureCounters counters)
    {
        var (name, limit, observed) = boundKind switch
        {
            BoundedEcmaSignatureBoundKind.ByteCount => (
                ExpressionV2ContractLimits.TypeSpecificationByteCountBoundName,
                StaticFieldV2Limits.MaximumTypeSpecificationByteCount,
                counters.InputByteCount),
            BoundedEcmaSignatureBoundKind.RecursiveDepth => (
                ExpressionV2ContractLimits.TypeSpecificationDepthBoundName,
                StaticFieldV2Limits.MaximumTypeSpecificationDepth,
                counters.MaximumObservedDepth),
            BoundedEcmaSignatureBoundKind.AggregateTypeCount => (
                ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName,
                StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
                counters.AggregateTypeCount),
            BoundedEcmaSignatureBoundKind.AggregateGenericArgumentCount => (
                ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
                StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
                counters.AggregateGenericArgumentCount),
            BoundedEcmaSignatureBoundKind.GenericParameterCount => (
                ExpressionV2ContractLimits.GenericParameterCountBoundName,
                StaticFieldV2Limits.MaximumGenericParameterCount,
                counters.MaximumDeclaredGenericParameterCount),
            BoundedEcmaSignatureBoundKind.ParameterCount => (
                ExpressionV2ContractLimits.ParameterCountBoundName,
                StaticFieldV2Limits.MaximumParameterCount,
                counters.MaximumDeclaredParameterCount),
            BoundedEcmaSignatureBoundKind.ArrayRank => (
                ExpressionV2ContractLimits.ArrayRankBoundName,
                StaticFieldV2Limits.MaximumArrayRank,
                counters.MaximumDeclaredArrayRank),
            _ => throw new InvalidOperationException("The shared grammar reported an inapplicable FieldDef bound."),
        };
        return new SignatureBound(
            new EvaluationDeterministicBound(name, limit),
            Math.Min(Math.Max(observed, limit + 1), limit + 1));
    }

    private readonly record struct SignatureBound(EvaluationDeterministicBound Bound, int ObservedCount);

    private static MetadataFieldDefinitionTableCatalogIdentity NonExact(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataFieldDefinitionTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            MetadataFieldDefinitionTableResultKind.NonExact,
            issue,
            definitionAuthority,
            ImmutableArray<MetadataFieldDefinitionTableRowIdentity>.Empty,
            reachedBound,
            observedCount,
            relatedMetadataToken);

    private static MetadataFieldDefinitionTableCatalogIdentity Invalid(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataFieldDefinitionTableIssue issue,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            MetadataFieldDefinitionTableResultKind.Invalid,
            issue,
            definitionAuthority,
            ImmutableArray<MetadataFieldDefinitionTableRowIdentity>.Empty,
            null,
            observedCount,
            relatedMetadataToken);
}
