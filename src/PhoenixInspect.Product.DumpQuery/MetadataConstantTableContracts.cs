using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete source-anchored Constant declaration-table proof.</summary>
/// <remarks>Every non-exact or invalid result exposes no derived Constant row prefix.</remarks>
public enum MetadataConstantTableResultKind
{
    /// <summary>Every physical Constant row, parent range, and value encoding is exact.</summary>
    Exact = 1,

    /// <summary>A required complete source operation stopped before exact projection.</summary>
    NonExact = 2,

    /// <summary>Complete supplied evidence contradicted source, parent, or value invariants.</summary>
    Invalid = 3,
}

/// <summary>Records whether the complete Constant table was physically sorted by its parent coded index.</summary>
/// <remarks>
/// ECMA-335 II.24.2.6 requires the Constant table to be sorted by Parent, so an unsorted image violates the
/// specification. The profile records that fact without rejecting the image, because no proof in this catalog depends
/// on the order: every row is reached from its parent, and completeness is proved by count agreement and by parent
/// uniqueness rather than by any ordering.
/// </remarks>
public enum MetadataConstantParentOrderProfile
{
    /// <summary>No complete row set was projected, so no order was observed.</summary>
    Unavailable = 0,

    /// <summary>Parent coded indexes ascend across the complete table, as the specification requires.</summary>
    EcmaParentSorted = 1,

    /// <summary>Parent coded indexes do not ascend; the image violates the specification's sort requirement.</summary>
    Unsorted = 2,
}

/// <summary>Names why a requested field's Constant row is present or absent.</summary>
/// <remarks>
/// Over an exact catalog <see cref="AbsentByDeclaredAttributes"/> is a proven negative rather than a failed lookup:
/// the catalog has already shown that every FieldDef declaring a default value owns exactly one Constant row, so a
/// field without one declares no default value at all.
/// </remarks>
public enum MetadataConstantDisposition
{
    /// <summary>The field declares a default value and its exact Constant row was returned.</summary>
    Present = 1,

    /// <summary>The field declares no default value, proven over the complete bidirectional pairing.</summary>
    AbsentByDeclaredAttributes = 2,

    /// <summary>The requested row was not issued by this catalog's own FieldDef prerequisite.</summary>
    OwnerNotIssuedByThisCatalog = 3,

    /// <summary>The catalog is non-exact, so it can prove neither presence nor absence.</summary>
    CatalogNonExact = 4,
}

/// <summary>Identifies the typed disposition of one Constant declaration-table proof.</summary>
/// <remarks>The issue remains available without retaining any derived row candidate.</remarks>
public enum MetadataConstantTableIssue
{
    /// <summary>No issue applies to an exact complete catalog.</summary>
    None = 0,

    /// <summary>The complete FieldDef prerequisite stopped without an exact catalog.</summary>
    FieldDefinitionCatalogNonExact = 1,

    /// <summary>The complete FieldDef prerequisite retained contradictory evidence.</summary>
    FieldDefinitionCatalogInvalid = 2,

    /// <summary>The declaration-side source ends describe a different exact source end than the FieldDef catalog.</summary>
    DeclaredMemberSourceEndMismatch = 3,

    /// <summary>The exact source Constant table end crossed the admitted complete row count.</summary>
    TableRowBoundReached = 4,

    /// <summary>The supplied Constant observations were unavailable or stopped before the exact source end.</summary>
    TableIncomplete = 5,

    /// <summary>The supplied Constant observation count exceeded the exact source end.</summary>
    TableRowCountConflict = 6,

    /// <summary>The supplied Constant tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 7,

    /// <summary>A physical Constant observation belonged to another metadata module.</summary>
    SourceModuleMismatch = 8,

    /// <summary>A Constant parent named a table outside the HasConstant coded index.</summary>
    ParentTokenKindInvalid = 9,

    /// <summary>A Constant parent named a row outside its own table's exact source end.</summary>
    ParentTokenOutOfRange = 10,

    /// <summary>Two complete Constant rows claimed the same physical parent.</summary>
    DuplicateParentConstant = 11,

    /// <summary>A Constant row carried a type code outside the admitted ECMA encoding set.</summary>
    ConstantTypeCodeNotAdmitted = 12,

    /// <summary>A Constant row carried an uninitialized value blob.</summary>
    ConstantValueBlobUninitialized = 13,

    /// <summary>One complete Constant value blob crossed its admitted byte cap.</summary>
    ConstantValueBlobBoundReached = 14,

    /// <summary>A Constant value blob width disagreed with the width its type code fixes.</summary>
    ConstantValueWidthInvalid = 15,

    /// <summary>A null-reference Constant carried a non-zero value byte.</summary>
    NullReferenceValueNonZero = 16,

    /// <summary>A Field-parented Constant row named a FieldDef that declares no default value.</summary>
    FieldParentWithoutDefaultFlag = 17,

    /// <summary>A FieldDef declaring a default value owned no Field-parented Constant row.</summary>
    FieldDefaultFlagWithoutConstantRow = 18,

    /// <summary>A literal FieldDef declared no default value, which ECMA-335 II.22.15 forbids.</summary>
    FieldLiteralWithoutDefaultFlag = 19,
}

/// <summary>Freezes the physical columns of one Constant table row.</summary>
/// <remarks>
/// The parent token is retained raw and validated only by the catalog, because a foreign-table reference is provable
/// only against complete source ends this observation does not carry.
/// <para>
/// The one reserved padding byte at ECMA-335 II.22.9 offset 1 is not exposed by the shared metadata reader and is
/// therefore neither observed nor asserted here. This observation carries no caller-authored parent and makes no
/// claim about the row's position in the table.
/// </para>
/// </remarks>
public sealed class MetadataConstantRowObservationIdentity : IEquatable<MetadataConstantRowObservationIdentity>
{
    /// <summary>Gets the maximum admitted complete Constant value blob length.</summary>
    public const int MaximumConstantValueByteCount = 65_536;

    /// <summary>Gets the declared bound name for <see cref="MaximumConstantValueByteCount"/>.</summary>
    public const string MaximumConstantValueByteCountBoundName = "metadata-v2.constant-value.bytes";

    private const string CanonicalDomain = "metadata-v2-constant-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> constantValueBlob;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataConstantRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int constantToken,
        int constantTypeCode,
        int parentMetadataToken,
        ImmutableArray<byte> constantValueBlob)
    {
        MetadataModule = metadataModule;
        ConstantToken = constantToken;
        ConstantTypeCode = constantTypeCode;
        ParentMetadataToken = parentMetadataToken;
        this.constantValueBlob = constantValueBlob;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(constantToken);
        writer.WriteInt32(constantTypeCode);
        writer.WriteInt32(parentMetadataToken);
        writer.WriteLengthPrefixedBytes(constantValueBlob.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module that declared this Constant row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the non-nil Constant token.</summary>
    public int ConstantToken { get; }

    /// <summary>Gets the raw ECMA-335 II.22.9 Constant type code byte.</summary>
    public int ConstantTypeCode { get; }

    /// <summary>Gets the raw HasConstant parent token, validated only by the complete catalog.</summary>
    public int ParentMetadataToken { get; }

    /// <summary>Gets a defensive copy of the complete Constant value blob.</summary>
    public ImmutableArray<byte> ConstantValueBlob => ExpressionV2ContractEncoding.Copy(constantValueBlob);

    /// <summary>Gets the complete Constant value blob without copying, for internal validation only.</summary>
    internal ImmutableArray<byte> ConstantValueBlobCore => constantValueBlob;

    /// <summary>Gets a defensive copy of the canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact physical Constant-row observation.</summary>
    /// <param name="metadataModule">The exact declaring metadata module.</param>
    /// <param name="constantToken">A non-nil Constant token.</param>
    /// <param name="constantTypeCode">The raw Constant type code byte.</param>
    /// <param name="parentMetadataToken">The raw HasConstant parent token.</param>
    /// <param name="constantValueBlob">The complete initialized Constant value blob.</param>
    /// <returns>An immutable Constant-row observation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="metadataModule"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied token or type code is out of range.</exception>
    /// <exception cref="ArgumentException">The value blob is uninitialized.</exception>
    public static MetadataConstantRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int constantToken,
        int constantTypeCode,
        int parentMetadataToken,
        ImmutableArray<byte> constantValueBlob)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(constantToken, 0x0B, nameof(constantToken));
        if (constantTypeCode is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(constantTypeCode));
        }

        if (CanonicalReplayEncoding.MetadataTokenRowId(parentMetadataToken) <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parentMetadataToken));
        }

        if (constantValueBlob.IsDefault)
        {
            throw new ArgumentException(
                "An initialized Constant value blob is required.",
                nameof(constantValueBlob));
        }

        return new MetadataConstantRowObservationIdentity(
            metadataModule,
            constantToken,
            constantTypeCode,
            parentMetadataToken,
            ImmutableArray.CreateRange(constantValueBlob));
    }

    /// <summary>Determines content equality from canonical physical row bytes.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> when every retained physical column is equal.</returns>
    public bool Equals(MetadataConstantRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical Constant-row observation equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a observation with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataConstantRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical content.</summary>
    /// <returns>A hash code for this observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete Constant row joined to its decoded parent kind and declaring FieldDef row.</summary>
/// <remarks>
/// The derived row is minted only by <see cref="MetadataConstantTableCatalogIdentity"/> after the complete table has
/// proved its parent ranges, parent uniqueness, value encodings, and the bidirectional FieldDef pairing. A Param- or
/// Property-parented row carries no joined declaring row, because neither of those tables is consumed here.
/// </remarks>
public sealed class MetadataConstantTableRowIdentity : IEquatable<MetadataConstantTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-constant-table-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataConstantTableRowIdentity(
        MetadataConstantRowObservationIdentity observation,
        MetadataConstantParentKind parentKind,
        MetadataFieldDefinitionTableRowIdentity? declaringFieldRow)
    {
        Observation = observation;
        ParentKind = parentKind;
        DeclaringFieldRow = declaringFieldRow;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(observation.Sha256, nameof(observation));
        writer.WriteInt32((int)parentKind);
        writer.WriteInt32(observation.ParentMetadataToken);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, declaringFieldRow?.Sha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact physical Constant-row observation this row derives from.</summary>
    public MetadataConstantRowObservationIdentity Observation { get; }

    /// <summary>Gets the decoded HasConstant parent table.</summary>
    public MetadataConstantParentKind ParentKind { get; }

    /// <summary>Gets the declaring FieldDef row for a Field-parented row, otherwise null.</summary>
    public MetadataFieldDefinitionTableRowIdentity? DeclaringFieldRow { get; }

    /// <summary>Gets the non-nil Constant token.</summary>
    public int ConstantToken => Observation.ConstantToken;

    /// <summary>Gets the raw HasConstant parent token.</summary>
    public int ParentMetadataToken => Observation.ParentMetadataToken;

    /// <summary>Gets the raw ECMA Constant type code byte.</summary>
    public int ConstantTypeCode => Observation.ConstantTypeCode;

    /// <summary>Gets a defensive copy of the complete Constant value blob.</summary>
    public ImmutableArray<byte> ConstantValueBlob => Observation.ConstantValueBlob;

    /// <summary>Gets the complete Constant value byte count.</summary>
    public int ConstantValueByteCount => Observation.ConstantValueBlobCore.Length;

    /// <summary>Gets the complete Constant value blob without copying, for internal projection only.</summary>
    internal ImmutableArray<byte> ConstantValueBlobCore => Observation.ConstantValueBlobCore;

    /// <summary>Gets a defensive copy of the canonical derived row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Determines content equality from canonical derived row bytes.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> when observation, parent kind, and declaring row are equal.</returns>
    public bool Equals(MetadataConstantTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests derived Constant row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataConstantTableRowIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical content.</summary>
    /// <returns>A hash code for this row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataConstantTableRowIdentity Create(
        object mintCapability,
        MetadataConstantRowObservationIdentity observation,
        MetadataConstantParentKind parentKind,
        MetadataFieldDefinitionTableRowIdentity? declaringFieldRow)
    {
        if (!MetadataConstantTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A derived Constant row is minted only by its own complete table catalog.",
                nameof(mintCapability));
        }

        return new MetadataConstantTableRowIdentity(observation, parentKind, declaringFieldRow);
    }
}

/// <summary>Freezes one complete, source-anchored Constant declaration-table projection.</summary>
/// <remarks>
/// The shared metadata reader projects no Constant rows, so a complete table is never walked: every row is reached
/// from its parent, and every Constant row has exactly one parent among FieldDef, Param, and Property. Completeness
/// is therefore proved by three independent facts rather than by traversal — the collected row count agrees with an
/// exact end this catalog did not produce, the collected tokens form the contiguous physical RID sequence, and no two
/// rows claim one parent.
/// <para>
/// The bidirectional FieldDef pairing is what turns that into a claimable absence: over an exact catalog, a FieldDef
/// declaring a default value owns exactly one Constant row and a FieldDef declaring none owns no row, so a field
/// without a row is proven to declare no default value rather than merely to have no row here.
/// </para>
/// <para>
/// Param- and Property-parented rows are validated physically — parent kind, source range, parent uniqueness, type
/// code, and value width — but are not paired against their own default-value attribute bits, because neither the
/// Param table nor property default values are consumed by this composition.
/// </para>
/// </remarks>
public sealed class MetadataConstantTableCatalogIdentity : IEquatable<MetadataConstantTableCatalogIdentity>
{
    /// <summary>Gets the maximum admitted complete Constant table row count.</summary>
    public const int MaximumConstantRowCount = StaticFieldV2Limits.MaximumConstantRowCount;

    private const string CanonicalDomain = "metadata-v2-constant-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private const int ElementTypeBoolean = 0x02;
    private const int ElementTypeChar = 0x03;
    private const int ElementTypeInt8 = 0x04;
    private const int ElementTypeUInt8 = 0x05;
    private const int ElementTypeInt16 = 0x06;
    private const int ElementTypeUInt16 = 0x07;
    private const int ElementTypeInt32 = 0x08;
    private const int ElementTypeUInt32 = 0x09;
    private const int ElementTypeSingle = 0x0C;
    private const int ElementTypeString = 0x0E;
    private const int ElementTypeClass = 0x12;

    private static readonly object RowMintCapability = new();

    private readonly ImmutableArray<MetadataConstantTableRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataConstantTableCatalogIdentity(
        MetadataConstantTableResultKind resultKind,
        MetadataConstantTableIssue issue,
        MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
        MetadataFieldDefinitionTableCatalogIdentity fieldDefinitions,
        ImmutableArray<MetadataConstantTableRowIdentity> rows,
        MetadataConstantParentOrderProfile parentOrderProfile,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        DeclaredMemberSourceEnds = declaredMemberSourceEnds;
        FieldDefinitions = fieldDefinitions;
        this.rows = rows;
        ParentOrderProfile = parentOrderProfile;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(declaredMemberSourceEnds.CanonicalBytes.AsSpan());
        writer.WriteSha256(fieldDefinitions.Sha256, nameof(fieldDefinitions));
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        writer.WriteInt32((int)parentOrderProfile);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete Constant table is exact, non-exact, or invalid.</summary>
    public MetadataConstantTableResultKind ResultKind { get; }

    /// <summary>Gets the typed issue, or none for an exact result.</summary>
    public MetadataConstantTableIssue Issue { get; }

    /// <summary>Gets the declaration-side source ends supplying every exact table end.</summary>
    public MetadataDeclaredMemberSourceEndIdentity DeclaredMemberSourceEnds { get; }

    /// <summary>Gets the complete FieldDef prerequisite this catalog pairs against.</summary>
    public MetadataFieldDefinitionTableCatalogIdentity FieldDefinitions { get; }

    /// <summary>Gets a defensive RID-order copy of exact derived Constant rows, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataConstantTableRowIdentity> Rows => ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the observed physical parent-order profile of the complete table.</summary>
    public MetadataConstantParentOrderProfile ParentOrderProfile { get; }

    /// <summary>Gets the reached bound for a cap-plus-one or propagated non-exact result, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied count or cap-plus-one observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the Constant or prerequisite token related to the issue, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical catalog.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete source-anchored Constant declaration-table projection.</summary>
    /// <param name="declaredMemberSourceEnds">The declaration-side source-end extension supplying every exact end.</param>
    /// <param name="fieldDefinitions">The complete FieldDef prerequisite the pairing proof runs against.</param>
    /// <param name="observations">
    /// Every physical Constant row in RID order, collected from every HasConstant parent table; default denotes
    /// unavailable acquisition when the source is non-empty.
    /// </param>
    /// <returns>An exact catalog, a prefix-free non-exact stop, or a factless invalid result.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static MetadataConstantTableCatalogIdentity Create(
        MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
        MetadataFieldDefinitionTableCatalogIdentity fieldDefinitions,
        ImmutableArray<MetadataConstantRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(declaredMemberSourceEnds);
        ArgumentNullException.ThrowIfNull(fieldDefinitions);

        if (fieldDefinitions.ResultKind == MetadataFieldDefinitionTableResultKind.NonExact)
        {
            return Stop(
                declaredMemberSourceEnds,
                fieldDefinitions,
                MetadataConstantTableResultKind.NonExact,
                MetadataConstantTableIssue.FieldDefinitionCatalogNonExact,
                fieldDefinitions.ReachedBound,
                fieldDefinitions.ObservedCount,
                fieldDefinitions.RelatedMetadataToken);
        }
        if (fieldDefinitions.ResultKind == MetadataFieldDefinitionTableResultKind.Invalid)
        {
            return Stop(
                declaredMemberSourceEnds,
                fieldDefinitions,
                MetadataConstantTableResultKind.Invalid,
                MetadataConstantTableIssue.FieldDefinitionCatalogInvalid,
                null,
                fieldDefinitions.ObservedCount,
                fieldDefinitions.RelatedMetadataToken);
        }
        if (!declaredMemberSourceEnds.DefinitionSourceEnds.Equals(fieldDefinitions.SourceEnds))
        {
            return Stop(
                declaredMemberSourceEnds,
                fieldDefinitions,
                MetadataConstantTableResultKind.Invalid,
                MetadataConstantTableIssue.DeclaredMemberSourceEndMismatch,
                null,
                0,
                null);
        }

        var sourceCount = declaredMemberSourceEnds.ConstantRowCount;
        if (sourceCount > MaximumConstantRowCount)
        {
            return Stop(
                declaredMemberSourceEnds,
                fieldDefinitions,
                MetadataConstantTableResultKind.NonExact,
                MetadataConstantTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ConstantRowCountBoundName,
                    MaximumConstantRowCount),
                MaximumConstantRowCount + 1,
                null);
        }
        if (observations.IsDefault && sourceCount == 0)
        {
            observations = ImmutableArray<MetadataConstantRowObservationIdentity>.Empty;
        }
        if (observations.IsDefault || observations.Length < sourceCount)
        {
            return Stop(
                declaredMemberSourceEnds,
                fieldDefinitions,
                MetadataConstantTableResultKind.NonExact,
                MetadataConstantTableIssue.TableIncomplete,
                null,
                observations.IsDefault ? 0 : observations.Length,
                null);
        }
        if (observations.Length > sourceCount)
        {
            return Stop(
                declaredMemberSourceEnds,
                fieldDefinitions,
                MetadataConstantTableResultKind.Invalid,
                MetadataConstantTableIssue.TableRowCountConflict,
                null,
                observations.Length,
                null);
        }

        var copied = observations.IsEmpty
            ? ImmutableArray<MetadataConstantRowObservationIdentity>.Empty
            : ImmutableArray.CreateRange(observations);
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null || observation.ConstantToken != (0x0B00_0000 | checked(index + 1)))
            {
                return Stop(
                    declaredMemberSourceEnds,
                    fieldDefinitions,
                    MetadataConstantTableResultKind.Invalid,
                    MetadataConstantTableIssue.PhysicalOrderInvalid,
                    null,
                    copied.Length,
                    observation?.ConstantToken);
            }
            if (!observation.MetadataModule.Equals(declaredMemberSourceEnds.SourceModule))
            {
                return Stop(
                    declaredMemberSourceEnds,
                    fieldDefinitions,
                    MetadataConstantTableResultKind.Invalid,
                    MetadataConstantTableIssue.SourceModuleMismatch,
                    null,
                    copied.Length,
                    observation.ConstantToken);
            }
        }

        var parentKinds = new MetadataConstantParentKind[copied.Length];
        var claimedParents = new HashSet<int>();
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (!IsHasConstantTable(observation.ParentMetadataToken))
            {
                return Stop(
                    declaredMemberSourceEnds,
                    fieldDefinitions,
                    MetadataConstantTableResultKind.Invalid,
                    MetadataConstantTableIssue.ParentTokenKindInvalid,
                    null,
                    copied.Length,
                    observation.ConstantToken);
            }
            if (!declaredMemberSourceEnds.ContainsHasConstantParentToken(
                    observation.ParentMetadataToken,
                    out var parentKind))
            {
                return Stop(
                    declaredMemberSourceEnds,
                    fieldDefinitions,
                    MetadataConstantTableResultKind.Invalid,
                    MetadataConstantTableIssue.ParentTokenOutOfRange,
                    null,
                    copied.Length,
                    observation.ConstantToken);
            }
            if (!claimedParents.Add(observation.ParentMetadataToken))
            {
                return Stop(
                    declaredMemberSourceEnds,
                    fieldDefinitions,
                    MetadataConstantTableResultKind.Invalid,
                    MetadataConstantTableIssue.DuplicateParentConstant,
                    null,
                    copied.Length,
                    observation.ConstantToken);
            }

            parentKinds[index] = parentKind;
            if (ValidateValue(observation) is { } valueIssue)
            {
                return Stop(
                    declaredMemberSourceEnds,
                    fieldDefinitions,
                    valueIssue == MetadataConstantTableIssue.ConstantValueBlobBoundReached
                        ? MetadataConstantTableResultKind.NonExact
                        : MetadataConstantTableResultKind.Invalid,
                    valueIssue,
                    valueIssue == MetadataConstantTableIssue.ConstantValueBlobBoundReached
                        ? new EvaluationDeterministicBound(
                            MetadataConstantRowObservationIdentity.MaximumConstantValueByteCountBoundName,
                            MetadataConstantRowObservationIdentity.MaximumConstantValueByteCount)
                        : null,
                    observation.ConstantValueBlobCore.Length,
                    observation.ConstantToken);
            }
        }

        if (PairAgainstFieldDefinitions(
                copied,
                parentKinds,
                fieldDefinitions,
                out var declaringFieldRows,
                out var pairingIssue,
                out var pairingToken))
        {
            return Stop(
                declaredMemberSourceEnds,
                fieldDefinitions,
                MetadataConstantTableResultKind.Invalid,
                pairingIssue,
                null,
                copied.Length,
                pairingToken);
        }

        var minted = ImmutableArray.CreateBuilder<MetadataConstantTableRowIdentity>(copied.Length);
        for (var index = 0; index < copied.Length; index++)
        {
            minted.Add(MetadataConstantTableRowIdentity.Create(
                RowMintCapability,
                copied[index],
                parentKinds[index],
                declaringFieldRows[index]));
        }

        return new MetadataConstantTableCatalogIdentity(
            MetadataConstantTableResultKind.Exact,
            MetadataConstantTableIssue.None,
            declaredMemberSourceEnds,
            fieldDefinitions,
            minted.MoveToImmutable(),
            ObserveParentOrder(copied),
            null,
            0,
            null);
    }

    /// <summary>Finds the exact derived row for one physical Constant token.</summary>
    /// <param name="constantToken">The physical Constant token to look up.</param>
    /// <returns>The exact derived row, or null for a non-exact catalog or an unknown token.</returns>
    public MetadataConstantTableRowIdentity? FindRow(int constantToken)
    {
        if (ResultKind != MetadataConstantTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(constantToken, 0x0B))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(constantToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>
    /// Decides whether one FieldDef row declares a default value, and returns its exact Constant row when it does.
    /// </summary>
    /// <param name="fieldRow">The FieldDef row whose declared default value is requested.</param>
    /// <param name="constantRow">The exact Constant row on <see cref="MetadataConstantDisposition.Present"/>.</param>
    /// <returns>The typed disposition of the requested field.</returns>
    /// <remarks>
    /// The requested row must be the identical row this catalog's own FieldDef prerequisite issued, which is what
    /// closes the question a caller-supplied fact can never close: whether the returned value belongs to the field
    /// that was asked about.
    /// </remarks>
    public MetadataConstantDisposition DispositionForField(
        MetadataFieldDefinitionTableRowIdentity fieldRow,
        out MetadataConstantTableRowIdentity? constantRow)
    {
        constantRow = null;
        if (ResultKind != MetadataConstantTableResultKind.Exact)
        {
            return MetadataConstantDisposition.CatalogNonExact;
        }
        if (fieldRow is null ||
            FieldDefinitions.FindRow(fieldRow.Observation.FieldDefinitionToken) is not { } issued ||
            !issued.Equals(fieldRow))
        {
            return MetadataConstantDisposition.OwnerNotIssuedByThisCatalog;
        }

        foreach (var row in rows)
        {
            if (row.ParentKind == MetadataConstantParentKind.FieldDefinition &&
                row.ParentMetadataToken == fieldRow.Observation.FieldDefinitionToken)
            {
                constantRow = row;
                return MetadataConstantDisposition.Present;
            }
        }

        return MetadataConstantDisposition.AbsentByDeclaredAttributes;
    }

    /// <summary>Tests canonical equality between two complete Constant table projections.</summary>
    /// <param name="other">The other catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataConstantTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete Constant table projection equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataConstantTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical content.</summary>
    /// <returns>A hash code for this catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    private static bool IsHasConstantTable(int parentMetadataToken) =>
        (parentMetadataToken & unchecked((int)0xFF00_0000)) is 0x0400_0000 or 0x0800_0000 or 0x1700_0000;

    private static MetadataConstantTableIssue? ValidateValue(MetadataConstantRowObservationIdentity observation)
    {
        if (!IsAdmittedConstantTypeCode(observation.ConstantTypeCode))
        {
            return MetadataConstantTableIssue.ConstantTypeCodeNotAdmitted;
        }

        var blob = observation.ConstantValueBlobCore;
        if (blob.IsDefault)
        {
            return MetadataConstantTableIssue.ConstantValueBlobUninitialized;
        }
        if (blob.Length > MetadataConstantRowObservationIdentity.MaximumConstantValueByteCount)
        {
            return MetadataConstantTableIssue.ConstantValueBlobBoundReached;
        }

        if (observation.ConstantTypeCode == ElementTypeString)
        {
            // A string constant is little-endian UTF-16, so only its evenness is fixed by the encoding.
            return blob.Length % 2 == 0 ? null : MetadataConstantTableIssue.ConstantValueWidthInvalid;
        }

        if (blob.Length != ConstantWidth(observation.ConstantTypeCode))
        {
            return MetadataConstantTableIssue.ConstantValueWidthInvalid;
        }

        if (observation.ConstantTypeCode == ElementTypeClass)
        {
            foreach (var value in blob)
            {
                if (value != 0)
                {
                    return MetadataConstantTableIssue.NullReferenceValueNonZero;
                }
            }
        }

        return null;
    }

    private static bool PairAgainstFieldDefinitions(
        ImmutableArray<MetadataConstantRowObservationIdentity> observations,
        MetadataConstantParentKind[] parentKinds,
        MetadataFieldDefinitionTableCatalogIdentity fieldDefinitions,
        out MetadataFieldDefinitionTableRowIdentity?[] declaringFieldRows,
        out MetadataConstantTableIssue issue,
        out int? relatedMetadataToken)
    {
        declaringFieldRows = new MetadataFieldDefinitionTableRowIdentity?[observations.Length];
        issue = MetadataConstantTableIssue.None;
        relatedMetadataToken = null;

        var pairedFieldTokens = new HashSet<int>();
        for (var index = 0; index < observations.Length; index++)
        {
            if (parentKinds[index] != MetadataConstantParentKind.FieldDefinition)
            {
                continue;
            }

            var parentToken = observations[index].ParentMetadataToken;
            if (fieldDefinitions.FindRow(parentToken) is not { } fieldRow || !fieldRow.HasDefault)
            {
                issue = MetadataConstantTableIssue.FieldParentWithoutDefaultFlag;
                relatedMetadataToken = observations[index].ConstantToken;
                return true;
            }

            declaringFieldRows[index] = fieldRow;
            pairedFieldTokens.Add(parentToken);
        }

        // The reverse direction is what makes an absent row a proven negative rather than an unfound one.
        foreach (var fieldRow in fieldDefinitions.Rows)
        {
            var fieldToken = fieldRow.Observation.FieldDefinitionToken;
            if (fieldRow.HasDefault && !pairedFieldTokens.Contains(fieldToken))
            {
                issue = MetadataConstantTableIssue.FieldDefaultFlagWithoutConstantRow;
                relatedMetadataToken = fieldToken;
                return true;
            }
            if (fieldRow.IsLiteral && !fieldRow.HasDefault)
            {
                issue = MetadataConstantTableIssue.FieldLiteralWithoutDefaultFlag;
                relatedMetadataToken = fieldToken;
                return true;
            }
        }

        return false;
    }

    private static MetadataConstantParentOrderProfile ObserveParentOrder(
        ImmutableArray<MetadataConstantRowObservationIdentity> observations)
    {
        if (observations.IsEmpty)
        {
            return MetadataConstantParentOrderProfile.Unavailable;
        }

        for (var index = 1; index < observations.Length; index++)
        {
            if (HasConstantCodedIndex(observations[index].ParentMetadataToken) <
                HasConstantCodedIndex(observations[index - 1].ParentMetadataToken))
            {
                return MetadataConstantParentOrderProfile.Unsorted;
            }
        }

        return MetadataConstantParentOrderProfile.EcmaParentSorted;
    }

    private static long HasConstantCodedIndex(int parentMetadataToken)
    {
        var tag = (parentMetadataToken & unchecked((int)0xFF00_0000)) switch
        {
            0x0400_0000 => 0L,
            0x0800_0000 => 1L,
            _ => 2L,
        };
        return ((long)CanonicalReplayEncoding.MetadataTokenRowId(parentMetadataToken) << 2) | tag;
    }

    private static bool IsAdmittedConstantTypeCode(int typeCode) =>
        typeCode is >= ElementTypeBoolean and <= ElementTypeString or ElementTypeClass;

    private static int ConstantWidth(int typeCode) => typeCode switch
    {
        ElementTypeBoolean or ElementTypeInt8 or ElementTypeUInt8 => 1,
        ElementTypeChar or ElementTypeInt16 or ElementTypeUInt16 => 2,
        ElementTypeInt32 or ElementTypeUInt32 or ElementTypeSingle or ElementTypeClass => 4,
        _ => 8,
    };

    private static MetadataConstantTableCatalogIdentity Stop(
        MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
        MetadataFieldDefinitionTableCatalogIdentity fieldDefinitions,
        MetadataConstantTableResultKind resultKind,
        MetadataConstantTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            declaredMemberSourceEnds,
            fieldDefinitions,
            ImmutableArray<MetadataConstantTableRowIdentity>.Empty,
            MetadataConstantParentOrderProfile.Unavailable,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}
