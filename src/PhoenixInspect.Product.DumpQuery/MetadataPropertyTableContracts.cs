using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete authority-joined Property declaration-table proof.</summary>
/// <remarks>Every non-exact or invalid result exposes no derived Property row prefix.</remarks>
public enum MetadataPropertyTableResultKind
{
    /// <summary>Every physical Property row, declaring owner, and ownership block is exact.</summary>
    Exact = 1,

    /// <summary>A required complete source operation stopped before exact projection.</summary>
    NonExact = 2,

    /// <summary>Complete supplied evidence contradicted source or ownership invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one Property declaration-table proof.</summary>
/// <remarks>The issue remains available without retaining any derived row candidate.</remarks>
public enum MetadataPropertyTableIssue
{
    /// <summary>No issue applies to an exact complete catalog.</summary>
    None = 0,

    /// <summary>The complete definition-authority prerequisite stopped without an exact catalog.</summary>
    DefinitionAuthorityNonExact = 1,

    /// <summary>The complete definition-authority prerequisite retained contradictory evidence.</summary>
    DefinitionAuthorityInvalid = 2,

    /// <summary>The declaration-side source ends describe a different exact source end than the authority.</summary>
    DeclaredMemberSourceEndMismatch = 3,

    /// <summary>The exact source Property table end crossed the admitted complete row count.</summary>
    TableRowBoundReached = 4,

    /// <summary>The supplied Property observations were unavailable or stopped before the exact source end.</summary>
    TableIncomplete = 5,

    /// <summary>The supplied Property observation count exceeded the exact source end.</summary>
    TableRowCountConflict = 6,

    /// <summary>The supplied Property tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 7,

    /// <summary>A physical Property observation belonged to another metadata module.</summary>
    SourceModuleMismatch = 8,

    /// <summary>The image redirects Property ownership through a PropertyPtr table this composition does not model.</summary>
    PropertyPointerIndirectionNotModeled = 9,

    /// <summary>A Property row carried an empty metadata name.</summary>
    NameEmpty = 10,

    /// <summary>One complete Property name crossed its admitted character cap.</summary>
    NameBoundReached = 11,

    /// <summary>A Property row carried an uninitialized PropertySig blob.</summary>
    SignatureUninitialized = 12,

    /// <summary>One complete PropertySig blob crossed its admitted byte cap.</summary>
    SignatureBoundReached = 13,

    /// <summary>A Property row carried attribute bits outside the admitted ECMA set.</summary>
    PropertyAttributesNotAdmitted = 14,

    /// <summary>A Property owner named a row outside the exact TypeDef source end.</summary>
    DeclaringTypeDefinitionOutOfRange = 15,

    /// <summary>A Property owner was not issued by the complete definition authority.</summary>
    DeclaringTypeDefinitionNotIssued = 16,

    /// <summary>One declaring type owned two separated runs of Property rows.</summary>
    OwnershipBlockNotContiguous = 17,

    /// <summary>More ownership blocks were observed than the exact PropertyMap end can account for.</summary>
    OwnershipBlockCountConflict = 18,

    /// <summary>One declaring type declared two properties with the same name and signature.</summary>
    DuplicatePropertySignature = 19,
}

/// <summary>Freezes the physical columns of one Property table row.</summary>
/// <remarks>
/// The declaring owner is retained raw and validated only by the catalog, because ownership is provable only against
/// a complete definition authority this observation does not carry.
/// <para>
/// Two deliberate non-claims. The PropertySig blob is retained undecoded, because the shared bounded signature
/// grammar has no Property form and nothing here asserts the property's type. And the row carries no accessibility
/// at all, because the physical Property table has none — accessibility lives on the accessor MethodDefs the
/// MethodSemantics table associates, and that table is not modeled by this composition.
/// </para>
/// </remarks>
public sealed class MetadataPropertyRowObservationIdentity : IEquatable<MetadataPropertyRowObservationIdentity>
{
    /// <summary>Gets the maximum admitted complete Property name length in characters.</summary>
    public const int MaximumNameCharacterCount = 1_024;

    /// <summary>Gets the declared bound name for <see cref="MaximumNameCharacterCount"/>.</summary>
    public const string MaximumNameCharacterCountBoundName = "metadata-v2.property-name.characters";

    /// <summary>Gets the maximum admitted complete PropertySig blob length.</summary>
    public const int MaximumSignatureByteCount = 2_048;

    /// <summary>Gets the declared bound name for <see cref="MaximumSignatureByteCount"/>.</summary>
    public const string MaximumSignatureByteCountBoundName = "metadata-v2.property-signature.bytes";

    private const string CanonicalDomain = "metadata-v2-property-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signatureBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataPropertyRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int propertyToken,
        int attributes,
        string name,
        ImmutableArray<byte> signatureBytes,
        int declaringTypeDefinitionToken)
    {
        MetadataModule = metadataModule;
        PropertyToken = propertyToken;
        Attributes = attributes;
        Name = name;
        this.signatureBytes = signatureBytes;
        DeclaringTypeDefinitionToken = declaringTypeDefinitionToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(propertyToken);
        writer.WriteInt32(attributes);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(signatureBytes.AsSpan());
        writer.WriteInt32(declaringTypeDefinitionToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module that declared this Property row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the non-nil Property token.</summary>
    public int PropertyToken { get; }

    /// <summary>Gets the exact physical Property attribute bits.</summary>
    public int Attributes { get; }

    /// <summary>Gets the exact metadata name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the complete undecoded PropertySig blob.</summary>
    public ImmutableArray<byte> SignatureBytes => ExpressionV2ContractEncoding.Copy(signatureBytes);

    /// <summary>Gets the complete PropertySig blob without copying, for internal validation only.</summary>
    internal ImmutableArray<byte> SignatureBytesCore => signatureBytes;

    /// <summary>Gets the raw declaring TypeDef token, validated only by the complete catalog.</summary>
    public int DeclaringTypeDefinitionToken { get; }

    /// <summary>Gets a defensive copy of the canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact physical Property-row observation.</summary>
    /// <param name="metadataModule">The exact declaring metadata module.</param>
    /// <param name="propertyToken">A non-nil Property token.</param>
    /// <param name="attributes">The exact Property attribute bits.</param>
    /// <param name="name">The exact metadata name.</param>
    /// <param name="signatureBytes">The complete initialized PropertySig blob.</param>
    /// <param name="declaringTypeDefinitionToken">The raw declaring TypeDef token.</param>
    /// <returns>An immutable Property-row observation.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied token is out of range.</exception>
    /// <exception cref="ArgumentException">The name or signature blob is uninitialized.</exception>
    public static MetadataPropertyRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int propertyToken,
        int attributes,
        string name,
        ImmutableArray<byte> signatureBytes,
        int declaringTypeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        ArgumentNullException.ThrowIfNull(name);
        CanonicalReplayEncoding.ValidateMetadataToken(propertyToken, 0x17, nameof(propertyToken));
        if (CanonicalReplayEncoding.MetadataTokenRowId(declaringTypeDefinitionToken) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(declaringTypeDefinitionToken));
        }

        if (signatureBytes.IsDefault)
        {
            throw new ArgumentException(
                "An initialized PropertySig blob is required.",
                nameof(signatureBytes));
        }

        return new MetadataPropertyRowObservationIdentity(
            metadataModule,
            propertyToken,
            attributes,
            name,
            ImmutableArray.CreateRange(signatureBytes),
            declaringTypeDefinitionToken);
    }

    /// <summary>Determines content equality from canonical physical row bytes.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> when every retained physical column is equal.</returns>
    public bool Equals(MetadataPropertyRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical Property-row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataPropertyRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical content.</summary>
    /// <returns>A hash code for this observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete Property row joined to its authority-issued declaring TypeDef.</summary>
/// <remarks>
/// The derived row is minted only by <see cref="MetadataPropertyTableCatalogIdentity"/> after the complete table has
/// proved its ownership blocks, names, signatures, and attribute bits. Its decoded attribute properties are pure
/// bit readings and assert nothing about accessibility, which the physical table does not carry.
/// </remarks>
public sealed class MetadataPropertyTableRowIdentity : IEquatable<MetadataPropertyTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-property-table-row";
    private const int CanonicalSchemaVersion = 1;
    private const int PropertySpecialName = 0x0200;
    private const int PropertyRuntimeSpecialName = 0x0400;
    private const int PropertyHasDefault = 0x1000;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataPropertyTableRowIdentity(
        MetadataPropertyRowObservationIdentity observation,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition)
    {
        Observation = observation;
        DeclaringTypeDefinition = declaringTypeDefinition;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(observation.Sha256, nameof(observation));
        writer.WriteSha256(declaringTypeDefinition.Sha256, nameof(declaringTypeDefinition));
        writer.WriteInt32(declaringTypeDefinition.TypeDefinitionToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact physical Property-row observation this row derives from.</summary>
    public MetadataPropertyRowObservationIdentity Observation { get; }

    /// <summary>Gets the authority-issued declaring TypeDef that owns this property.</summary>
    public MetadataTypeDefinitionAuthorityIdentity DeclaringTypeDefinition { get; }

    /// <summary>Gets the non-nil Property token.</summary>
    public int PropertyToken => Observation.PropertyToken;

    /// <summary>Gets the exact metadata name.</summary>
    public string Name => Observation.Name;

    /// <summary>Gets the exact physical Property attribute bits.</summary>
    public int Attributes => Observation.Attributes;

    /// <summary>Gets whether the physical SpecialName bit is set.</summary>
    public bool IsSpecialName => (Attributes & PropertySpecialName) != 0;

    /// <summary>Gets whether the physical RTSpecialName bit is set.</summary>
    public bool IsRuntimeSpecialName => (Attributes & PropertyRuntimeSpecialName) != 0;

    /// <summary>Gets whether the physical HasDefault bit is set.</summary>
    public bool HasDefault => (Attributes & PropertyHasDefault) != 0;

    /// <summary>Gets a defensive copy of the canonical derived row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Determines content equality from canonical derived row bytes.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> when observation and declaring TypeDef are equal.</returns>
    public bool Equals(MetadataPropertyTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests derived Property-row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataPropertyTableRowIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical content.</summary>
    /// <returns>A hash code for this derived row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataPropertyTableRowIdentity Create(
        object mintCapability,
        MetadataPropertyRowObservationIdentity observation,
        MetadataTypeDefinitionAuthorityIdentity declaringTypeDefinition)
    {
        if (!MetadataPropertyTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A derived Property row is minted only by its own complete table catalog.",
                nameof(mintCapability));
        }

        return new MetadataPropertyTableRowIdentity(observation, declaringTypeDefinition);
    }
}

/// <summary>Freezes one complete, authority-joined Property declaration-table projection.</summary>
/// <remarks>
/// The complete Property table is enumerable and its exact end is already carried by the declaration-side source
/// ends, so completeness here is the ordinary count, contiguity, and module proof. Ownership is the part that is not
/// ordinary: PropertyMap (0x15) has no read-side projection at all, so each row's owner arrives per-row and the only
/// derivable ownership invariants are proved instead of a PropertyMap row walk.
/// <para>
/// Those two invariants are deliberately weaker than a row walk, and deliberately not weaker than the truth. Each
/// distinct owner must occupy exactly one contiguous run of Property RIDs, which is what a PropertyList range column
/// means; but owners are not required to appear in ascending order, because ECMA-335 II.24.2.6 does not list
/// PropertyMap among the sorted tables. The observed block count must not exceed the exact PropertyMap end, and the
/// comparison is an inequality rather than an equality, because a PropertyMap row owning a zero-length run is legal
/// and physically invisible from this side.
/// </para>
/// <para>
/// Stated non-claims: this catalog asserts no PropertyMap RID sequence, decodes no PropertySig, and derives no
/// accessibility. MethodSemantics (0x18) is not modeled by this composition, so nothing here can say which MethodDefs
/// accessed a property or how visible it was.
/// </para>
/// </remarks>
public sealed class MetadataPropertyTableCatalogIdentity : IEquatable<MetadataPropertyTableCatalogIdentity>
{
    /// <summary>Gets the maximum admitted complete Property table row count.</summary>
    public const int MaximumPropertyRowCount = StaticFieldV2Limits.MaximumPropertyRowCount;

    private const string CanonicalDomain = "metadata-v2-property-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private const int AdmittedPropertyAttributeMask = 0x0200 | 0x0400 | 0x1000;

    private static readonly object RowMintCapability = new();

    private readonly ImmutableArray<MetadataPropertyTableRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataPropertyTableCatalogIdentity(
        MetadataPropertyTableResultKind resultKind,
        MetadataPropertyTableIssue issue,
        MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataPropertyTableRowIdentity> rows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        DeclaredMemberSourceEnds = declaredMemberSourceEnds;
        DefinitionAuthority = definitionAuthority;
        this.rows = rows;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(declaredMemberSourceEnds.CanonicalBytes.AsSpan());
        writer.WriteSha256(definitionAuthority.Sha256, nameof(definitionAuthority));
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete Property table is exact, non-exact, or invalid.</summary>
    public MetadataPropertyTableResultKind ResultKind { get; }

    /// <summary>Gets the typed issue, or none for an exact result.</summary>
    public MetadataPropertyTableIssue Issue { get; }

    /// <summary>Gets the declaration-side source ends supplying every exact table end.</summary>
    public MetadataDeclaredMemberSourceEndIdentity DeclaredMemberSourceEnds { get; }

    /// <summary>Gets the complete definition authority supplying every declaring owner.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets a defensive RID-order copy of exact derived Property rows, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataPropertyTableRowIdentity> Rows => ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the reached bound for a cap-plus-one or propagated non-exact result, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied count or cap-plus-one observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the Property or prerequisite token related to the issue, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical catalog.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete authority-joined Property declaration-table projection.</summary>
    /// <param name="declaredMemberSourceEnds">The declaration-side source-end extension supplying every exact end.</param>
    /// <param name="definitionAuthority">The complete physical definition-authority prerequisite.</param>
    /// <param name="observations">
    /// Every physical Property row in RID order; default denotes unavailable acquisition when the source is non-empty.
    /// </param>
    /// <returns>An exact catalog, a prefix-free non-exact stop, or a factless invalid result.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static MetadataPropertyTableCatalogIdentity Create(
        MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataPropertyRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(declaredMemberSourceEnds);
        ArgumentNullException.ThrowIfNull(definitionAuthority);

        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.NonExact)
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.NonExact,
                MetadataPropertyTableIssue.DefinitionAuthorityNonExact,
                definitionAuthority.ReachedBound,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.Invalid)
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.Invalid,
                MetadataPropertyTableIssue.DefinitionAuthorityInvalid,
                null,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }
        if (!declaredMemberSourceEnds.DefinitionSourceEnds.Equals(definitionAuthority.SourceEnds))
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.Invalid,
                MetadataPropertyTableIssue.DeclaredMemberSourceEndMismatch,
                null,
                0,
                null);
        }

        var sourceCount = declaredMemberSourceEnds.PropertyRowCount;
        if (sourceCount > MaximumPropertyRowCount)
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.NonExact,
                MetadataPropertyTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.PropertyRowCountBoundName,
                    MaximumPropertyRowCount),
                MaximumPropertyRowCount + 1,
                null);
        }

        // An image that redirects Property ownership through PropertyPtr is an unmodeled shape, which is an
        // unavailable acquisition rather than a contradiction - the same posture the landed pointer catalog takes.
        if (declaredMemberSourceEnds.PropertyPointerRowCount != 0)
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.NonExact,
                MetadataPropertyTableIssue.PropertyPointerIndirectionNotModeled,
                null,
                declaredMemberSourceEnds.PropertyPointerRowCount,
                null);
        }

        if (observations.IsDefault && sourceCount == 0)
        {
            observations = ImmutableArray<MetadataPropertyRowObservationIdentity>.Empty;
        }
        if (observations.IsDefault || observations.Length < sourceCount)
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.NonExact,
                MetadataPropertyTableIssue.TableIncomplete,
                null,
                observations.IsDefault ? 0 : observations.Length,
                null);
        }
        if (observations.Length > sourceCount)
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.Invalid,
                MetadataPropertyTableIssue.TableRowCountConflict,
                null,
                observations.Length,
                null);
        }

        var copied = observations.IsEmpty
            ? ImmutableArray<MetadataPropertyRowObservationIdentity>.Empty
            : ImmutableArray.CreateRange(observations);
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null || observation.PropertyToken != (0x1700_0000 | checked(index + 1)))
            {
                return Stop(
                    declaredMemberSourceEnds,
                    definitionAuthority,
                    MetadataPropertyTableResultKind.Invalid,
                    MetadataPropertyTableIssue.PhysicalOrderInvalid,
                    null,
                    copied.Length,
                    observation?.PropertyToken);
            }
            if (!observation.MetadataModule.Equals(declaredMemberSourceEnds.SourceModule))
            {
                return Stop(
                    declaredMemberSourceEnds,
                    definitionAuthority,
                    MetadataPropertyTableResultKind.Invalid,
                    MetadataPropertyTableIssue.SourceModuleMismatch,
                    null,
                    copied.Length,
                    observation.PropertyToken);
            }
        }

        var owners = new MetadataTypeDefinitionAuthorityIdentity[copied.Length];
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (ValidateRow(observation) is { } rowIssue)
            {
                return Stop(
                    declaredMemberSourceEnds,
                    definitionAuthority,
                    rowIssue is MetadataPropertyTableIssue.NameBoundReached
                        or MetadataPropertyTableIssue.SignatureBoundReached
                        ? MetadataPropertyTableResultKind.NonExact
                        : MetadataPropertyTableResultKind.Invalid,
                    rowIssue,
                    BoundFor(rowIssue),
                    rowIssue == MetadataPropertyTableIssue.NameBoundReached
                        ? observation.Name.Length
                        : observation.SignatureBytesCore.Length,
                    observation.PropertyToken);
            }

            if (!CanonicalReplayEncoding.IsMetadataTokenForTable(
                    observation.DeclaringTypeDefinitionToken,
                    0x02) ||
                CanonicalReplayEncoding.MetadataTokenRowId(observation.DeclaringTypeDefinitionToken) >
                    declaredMemberSourceEnds.DefinitionSourceEnds.TypeDefinitionRowCount)
            {
                return Stop(
                    declaredMemberSourceEnds,
                    definitionAuthority,
                    MetadataPropertyTableResultKind.Invalid,
                    MetadataPropertyTableIssue.DeclaringTypeDefinitionOutOfRange,
                    null,
                    copied.Length,
                    observation.PropertyToken);
            }

            var issued = definitionAuthority.ExactTypeDefinitionOrDefault(observation.DeclaringTypeDefinitionToken);
            if (issued is null)
            {
                return Stop(
                    declaredMemberSourceEnds,
                    definitionAuthority,
                    MetadataPropertyTableResultKind.Invalid,
                    MetadataPropertyTableIssue.DeclaringTypeDefinitionNotIssued,
                    null,
                    copied.Length,
                    observation.PropertyToken);
            }

            owners[index] = issued;
        }

        if (ValidateOwnership(copied, declaredMemberSourceEnds, out var ownershipIssue, out var ownershipToken))
        {
            return Stop(
                declaredMemberSourceEnds,
                definitionAuthority,
                MetadataPropertyTableResultKind.Invalid,
                ownershipIssue,
                null,
                copied.Length,
                ownershipToken);
        }

        var minted = ImmutableArray.CreateBuilder<MetadataPropertyTableRowIdentity>(copied.Length);
        for (var index = 0; index < copied.Length; index++)
        {
            minted.Add(MetadataPropertyTableRowIdentity.Create(RowMintCapability, copied[index], owners[index]));
        }

        return new MetadataPropertyTableCatalogIdentity(
            MetadataPropertyTableResultKind.Exact,
            MetadataPropertyTableIssue.None,
            declaredMemberSourceEnds,
            definitionAuthority,
            minted.MoveToImmutable(),
            null,
            0,
            null);
    }

    /// <summary>Finds the exact derived row for one physical Property token.</summary>
    /// <param name="propertyToken">The physical Property token to look up.</param>
    /// <returns>The exact derived row, or null for a non-exact catalog or an unknown token.</returns>
    public MetadataPropertyTableRowIdentity? FindRow(int propertyToken)
    {
        if (ResultKind != MetadataPropertyTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(propertyToken, 0x17))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(propertyToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Projects the exact derived rows declared by one authority-issued declaring TypeDef.</summary>
    /// <param name="declaringType">The authority-issued TypeDef whose declared Property rows are requested.</param>
    /// <returns>
    /// The derived rows in ascending Property RID order, or an empty array for a non-exact catalog or a TypeDef that
    /// this catalog's own definition authority did not issue.
    /// </returns>
    public ImmutableArray<MetadataPropertyTableRowIdentity> RowsForDeclaringTypeOrEmpty(
        MetadataTypeDefinitionAuthorityIdentity declaringType)
    {
        if (ResultKind != MetadataPropertyTableResultKind.Exact || declaringType is null)
        {
            return ImmutableArray<MetadataPropertyTableRowIdentity>.Empty;
        }

        var issued = DefinitionAuthority.ExactTypeDefinitionOrDefault(declaringType.TypeDefinitionToken);
        if (issued is null || !issued.Equals(declaringType))
        {
            return ImmutableArray<MetadataPropertyTableRowIdentity>.Empty;
        }

        var projected = ImmutableArray.CreateBuilder<MetadataPropertyTableRowIdentity>();
        foreach (var row in rows)
        {
            if (row.DeclaringTypeDefinition.TypeDefinitionToken == declaringType.TypeDefinitionToken)
            {
                projected.Add(row);
            }
        }

        return projected.ToImmutable();
    }

    /// <summary>Tests canonical equality between two complete Property table projections.</summary>
    /// <param name="other">The other catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataPropertyTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete Property table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataPropertyTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical content.</summary>
    /// <returns>A hash code for this catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    private static EvaluationDeterministicBound? BoundFor(MetadataPropertyTableIssue issue) => issue switch
    {
        MetadataPropertyTableIssue.NameBoundReached => new EvaluationDeterministicBound(
            MetadataPropertyRowObservationIdentity.MaximumNameCharacterCountBoundName,
            MetadataPropertyRowObservationIdentity.MaximumNameCharacterCount),
        MetadataPropertyTableIssue.SignatureBoundReached => new EvaluationDeterministicBound(
            MetadataPropertyRowObservationIdentity.MaximumSignatureByteCountBoundName,
            MetadataPropertyRowObservationIdentity.MaximumSignatureByteCount),
        _ => null,
    };

    private static MetadataPropertyTableIssue? ValidateRow(MetadataPropertyRowObservationIdentity observation)
    {
        if (observation.Name.Length == 0)
        {
            return MetadataPropertyTableIssue.NameEmpty;
        }
        if (observation.Name.Length > MetadataPropertyRowObservationIdentity.MaximumNameCharacterCount)
        {
            return MetadataPropertyTableIssue.NameBoundReached;
        }
        if (observation.SignatureBytesCore.IsDefaultOrEmpty)
        {
            return MetadataPropertyTableIssue.SignatureUninitialized;
        }
        if (observation.SignatureBytesCore.Length > MetadataPropertyRowObservationIdentity.MaximumSignatureByteCount)
        {
            return MetadataPropertyTableIssue.SignatureBoundReached;
        }

        // ECMA-335 II.23.1.14 defines exactly three Property flags; a compiler that sets none is ordinary, so the
        // test is for bits outside the admitted set rather than for membership in it.
        return (observation.Attributes & ~AdmittedPropertyAttributeMask) != 0
            ? MetadataPropertyTableIssue.PropertyAttributesNotAdmitted
            : null;
    }

    private static bool ValidateOwnership(
        ImmutableArray<MetadataPropertyRowObservationIdentity> observations,
        MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
        out MetadataPropertyTableIssue issue,
        out int? relatedMetadataToken)
    {
        issue = MetadataPropertyTableIssue.None;
        relatedMetadataToken = null;

        var startedOwners = new HashSet<int>();
        var signatures = new HashSet<(int Owner, string Name, string Signature)>();
        var blockCount = 0;
        var currentOwner = 0;
        foreach (var observation in observations)
        {
            var owner = observation.DeclaringTypeDefinitionToken;
            if (owner != currentOwner)
            {
                // Every owner starts exactly one run. Meeting an owner again after leaving its run means the table
                // gave it two separated blocks, which no PropertyList range column can express.
                if (!startedOwners.Add(owner))
                {
                    issue = MetadataPropertyTableIssue.OwnershipBlockNotContiguous;
                    relatedMetadataToken = observation.PropertyToken;
                    return true;
                }

                currentOwner = owner;
                blockCount++;
            }

            var key = (owner, observation.Name, Convert.ToHexString(observation.SignatureBytesCore.AsSpan()));
            if (!signatures.Add(key))
            {
                // ECMA-335 II.22.34 keys a property on Parent, Name, and Signature, so overloaded indexers that
                // legally share one name are admitted and only a repeated signature is the contradiction.
                issue = MetadataPropertyTableIssue.DuplicatePropertySignature;
                relatedMetadataToken = observation.PropertyToken;
                return true;
            }
        }

        // An inequality, not an equality: a PropertyMap row owning a zero-length run is legal and invisible here.
        if (blockCount > declaredMemberSourceEnds.PropertyMapRowCount ||
            (declaredMemberSourceEnds.PropertyMapRowCount == 0 && observations.Length > 0))
        {
            issue = MetadataPropertyTableIssue.OwnershipBlockCountConflict;
            relatedMetadataToken = null;
            return true;
        }

        return false;
    }

    private static MetadataPropertyTableCatalogIdentity Stop(
        MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataPropertyTableResultKind resultKind,
        MetadataPropertyTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            declaredMemberSourceEnds,
            definitionAuthority,
            ImmutableArray<MetadataPropertyTableRowIdentity>.Empty,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}
