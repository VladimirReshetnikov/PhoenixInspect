using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete physical GenericParamConstraint-table draft proof.</summary>
/// <remarks>
/// This draft result describes physical table authority only. Even an exact result retains the Constraint column as
/// an unresolved TypeDefOrRef token and makes no target-semantic claim.
/// </remarks>
public enum MetadataGenericParameterConstraintPhysicalTableResultKind
{
    /// <summary>The complete physical table and every GenericParam owner join are exact.</summary>
    Exact = 1,

    /// <summary>Complete acquisition or an authority prerequisite was unavailable or bounded.</summary>
    NonExact = 2,

    /// <summary>The supplied complete-table claim or an authority prerequisite was contradictory.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of a physical GenericParamConstraint-table draft proof.</summary>
/// <remarks>Every non-exact or invalid result is prefix-free and exposes no derived exact row.</remarks>
public enum MetadataGenericParameterConstraintPhysicalTableIssue
{
    /// <summary>No issue applies to an exact complete table.</summary>
    None = 0,

    /// <summary>The supplied source ends differ from the retained GenericParam authority source.</summary>
    GenericParameterAuthoritySourceMismatch = 1,

    /// <summary>The GenericParam authority prerequisite stopped non-exactly.</summary>
    GenericParameterAuthorityNonExact = 2,

    /// <summary>The GenericParam authority prerequisite is invalid.</summary>
    GenericParameterAuthorityInvalid = 3,

    /// <summary>The exact GenericParamConstraint source end crossed the admitted complete-row bound.</summary>
    TableRowBoundReached = 4,

    /// <summary>The supplied observations were unavailable or stopped before the exact source end.</summary>
    TableIncomplete = 5,

    /// <summary>The supplied observation count exceeded the exact source end.</summary>
    TableRowCountConflict = 6,

    /// <summary>At least one observation was absent from the claimed complete physical table.</summary>
    PhysicalRowMissing = 7,

    /// <summary>The GenericParamConstraint tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 8,

    /// <summary>At least one physical observation belonged to another metadata module.</summary>
    SourceModuleMismatch = 9,

    /// <summary>An Owner token belonged to a table other than GenericParam.</summary>
    OwnerTokenKindInvalid = 10,

    /// <summary>An Owner GenericParam token was nil or lay beyond the exact source end.</summary>
    OwnerTokenOutOfRange = 11,

    /// <summary>Physical rows were not ordered by nondecreasing GenericParam Owner RID.</summary>
    OwnerOrderInvalid = 12,

    /// <summary>A Constraint token belonged to a table outside the TypeDefOrRef coded-token domain.</summary>
    ConstraintTokenKindInvalid = 13,

    /// <summary>A Constraint TypeDefOrRef token was nil or lay beyond its exact table source end.</summary>
    ConstraintTokenOutOfRange = 14,

    /// <summary>Two physical rows repeated the same exact Owner and unresolved Constraint token pair.</summary>
    DuplicateOwnerConstraint = 15,
}

/// <summary>Freezes only the three physical columns observed from one GenericParamConstraint row.</summary>
/// <remarks>
/// This sealed draft observation contains no caller-created GenericParam owner, owner group, declaring type, or
/// resolved target. The catalog alone may join its raw Owner token to exact GenericParam authority.
/// </remarks>
public sealed class MetadataGenericParameterConstraintRowObservationIdentity :
    IEquatable<MetadataGenericParameterConstraintRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-genericparamconstraint-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterConstraintRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int genericParameterConstraintToken,
        int ownerGenericParameterToken,
        int constraintMetadataToken)
    {
        MetadataModule = metadataModule;
        GenericParameterConstraintToken = genericParameterConstraintToken;
        OwnerGenericParameterToken = ownerGenericParameterToken;
        ConstraintMetadataToken = constraintMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(genericParameterConstraintToken);
        writer.WriteInt32(ownerGenericParameterToken);
        writer.WriteInt32(constraintMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the observed physical row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact non-nil GenericParamConstraint token identifying the physical row.</summary>
    public int GenericParameterConstraintToken { get; }

    /// <summary>Gets the unchanged raw Owner-column GenericParam token.</summary>
    public int OwnerGenericParameterToken { get; }

    /// <summary>Gets the unchanged raw Constraint-column TypeDefOrRef token.</summary>
    public int ConstraintMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one physical-column-only GenericParamConstraint row draft observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the physical row.</param>
    /// <param name="genericParameterConstraintToken">The exact non-nil GenericParamConstraint row token.</param>
    /// <param name="ownerGenericParameterToken">
    /// The raw Owner-column token, retained without trusting its table or range.
    /// </param>
    /// <param name="constraintMetadataToken">
    /// The raw Constraint-column token, retained without resolving its TypeDefOrRef target.
    /// </param>
    /// <returns>A sealed immutable physical observation with no derived owner or target fact.</returns>
    public static MetadataGenericParameterConstraintRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int genericParameterConstraintToken,
        int ownerGenericParameterToken,
        int constraintMetadataToken)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(
            genericParameterConstraintToken,
            0x2C,
            nameof(genericParameterConstraintToken));
        return new MetadataGenericParameterConstraintRowObservationIdentity(
            metadataModule,
            genericParameterConstraintToken,
            ownerGenericParameterToken,
            constraintMetadataToken);
    }

    /// <summary>Tests canonical equality between two physical GenericParamConstraint draft observations.</summary>
    /// <param name="other">The other physical draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical physical-column draft content.</returns>
    public bool Equals(MetadataGenericParameterConstraintRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical GenericParamConstraint draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataGenericParameterConstraintRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from immutable physical-column draft content.</summary>
    /// <returns>A hash code for this physical GenericParamConstraint observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one GenericParamConstraint row after complete source and GenericParam-authority validation.</summary>
/// <remarks>
/// This sealed draft identity has no public issuer. It retains the authority-issued GenericParam row and owner group,
/// but deliberately leaves the Constraint column as an unresolved physical TypeDefOrRef token. Canonical authority
/// references are fixed-size SHA-256 values so row size does not grow with an owner's arity or the authority catalog.
/// </remarks>
public sealed class MetadataGenericParameterConstraintTableRowIdentity :
    IEquatable<MetadataGenericParameterConstraintTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-genericparamconstraint-table-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterConstraintTableRowIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterConstraintRowObservationIdentity observation,
        MetadataGenericParameterTableRowIdentity ownerParameter,
        MetadataGenericParameterAuthorityOwnerGroupIdentity ownerGroup)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        OwnerParameter = ownerParameter;
        OwnerGroup = ownerGroup;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteSha256(observation.Sha256, nameof(observation));
        writer.WriteSha256(ownerParameter.Sha256, nameof(ownerParameter));
        writer.WriteSha256(ownerGroup.Sha256, nameof(ownerGroup));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source ends against which this complete row was validated.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical-column-only observation.</summary>
    public MetadataGenericParameterConstraintRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact authority-issued GenericParam row selected by the physical Owner token.</summary>
    public MetadataGenericParameterTableRowIdentity OwnerParameter { get; }

    /// <summary>Gets the exact authority-issued TypeDef or MethodDef owner group containing that parameter.</summary>
    public MetadataGenericParameterAuthorityOwnerGroupIdentity OwnerGroup { get; }

    /// <summary>Gets the exact GenericParamConstraint row token forwarded from the physical observation.</summary>
    public int GenericParameterConstraintToken => Observation.GenericParameterConstraintToken;

    /// <summary>Gets the exact authority-resolved GenericParam Owner token.</summary>
    public int OwnerGenericParameterToken => OwnerParameter.GenericParameterToken;

    /// <summary>Gets the validated but deliberately unresolved physical TypeDefOrRef Constraint token.</summary>
    public int ConstraintMetadataToken => Observation.ConstraintMetadataToken;

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two exact GenericParamConstraint table-row draft identities.</summary>
    /// <param name="other">The other exact draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterConstraintTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests exact GenericParamConstraint row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterConstraintTableRowIdentity);

    /// <summary>Computes a deterministic hash code from immutable exact row draft content.</summary>
    /// <returns>A hash code for this exact GenericParamConstraint row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataGenericParameterConstraintTableRowIdentity Create(
        object mintCapability,
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterConstraintRowObservationIdentity observation,
        MetadataGenericParameterTableRowIdentity ownerParameter,
        MetadataGenericParameterAuthorityOwnerGroupIdentity ownerGroup)
    {
        if (!MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An exact GenericParamConstraint row requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(ownerParameter);
        ArgumentNullException.ThrowIfNull(ownerGroup);
        return new MetadataGenericParameterConstraintTableRowIdentity(
            sourceEnds,
            observation,
            ownerParameter,
            ownerGroup);
    }
}

/// <summary>Freezes one complete source-anchored physical GenericParamConstraint table.</summary>
/// <remarks>
/// This sealed draft catalog resolves every physical Owner token exclusively through one exact
/// <see cref="MetadataGenericParameterAuthorityCatalogIdentity"/>. It validates TypeDefOrRef token kind and source
/// range without resolving target semantics. Non-exact and invalid outcomes retain no derived row prefix.
/// </remarks>
public sealed class MetadataGenericParameterConstraintPhysicalTableCatalogIdentity :
    IEquatable<MetadataGenericParameterConstraintPhysicalTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-genericparamconstraint-physical-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity> rows;
    private readonly ImmutableDictionary<int, ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>>
        rowsByOwnerToken;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterConstraintPhysicalTableCatalogIdentity(
        MetadataGenericParameterConstraintPhysicalTableResultKind resultKind,
        MetadataGenericParameterConstraintPhysicalTableIssue issue,
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterAuthorityCatalogIdentity genericParameterAuthority,
        ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity> rows,
        ImmutableDictionary<int, ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>> rowsByOwnerToken,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        GenericParameterAuthority = genericParameterAuthority;
        this.rows = ExpressionV2ContractEncoding.Copy(rows);
        this.rowsByOwnerToken = rowsByOwnerToken;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteSha256(genericParameterAuthority.Sha256, nameof(genericParameterAuthority));
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this complete physical-table draft is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterConstraintPhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed draft table issue, or none for an exact result.</summary>
    public MetadataGenericParameterConstraintPhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact metadata source ends governing the complete physical table.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the sole GenericParam authority used to resolve every physical Owner token.</summary>
    public MetadataGenericParameterAuthorityCatalogIdentity GenericParameterAuthority { get; }

    /// <summary>Gets exact rows in physical RID order, or an initialized empty array otherwise.</summary>
    public ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets a prerequisite or complete-table bound only for a prefix-free non-exact result.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied, prerequisite, or cap-plus-one row count, otherwise zero.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete source-anchored physical GenericParamConstraint-table draft proof.</summary>
    /// <param name="sourceEnds">The exact complete-table ends for the metadata source.</param>
    /// <param name="genericParameterAuthority">
    /// The authority-owned GenericParam catalog that alone may resolve physical Owner tokens.
    /// </param>
    /// <param name="observations">
    /// Every physical GenericParamConstraint row in RID order; default denotes unavailable complete acquisition.
    /// </param>
    /// <returns>An exact complete table, a prefix-free non-exact stop, or a factless invalid draft result.</returns>
    public static MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Create(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterAuthorityCatalogIdentity genericParameterAuthority,
        ImmutableArray<MetadataGenericParameterConstraintRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(genericParameterAuthority);
        var observedCount = observations.IsDefault ? 0 : observations.Length;

        if (!sourceEnds.Equals(genericParameterAuthority.SourceEnds))
        {
            return Invalid(
                sourceEnds,
                genericParameterAuthority,
                MetadataGenericParameterConstraintPhysicalTableIssue.GenericParameterAuthoritySourceMismatch,
                observedCount);
        }

        if (genericParameterAuthority.ResultKind == MetadataGenericParameterProofResultKind.Invalid)
        {
            return Invalid(
                sourceEnds,
                genericParameterAuthority,
                MetadataGenericParameterConstraintPhysicalTableIssue.GenericParameterAuthorityInvalid,
                genericParameterAuthority.ObservedCount);
        }

        if (genericParameterAuthority.ResultKind != MetadataGenericParameterProofResultKind.Exact)
        {
            return NonExact(
                sourceEnds,
                genericParameterAuthority,
                MetadataGenericParameterConstraintPhysicalTableIssue.GenericParameterAuthorityNonExact,
                genericParameterAuthority.ReachedBound,
                genericParameterAuthority.ObservedCount);
        }

        var sourceCount = sourceEnds.GenericParameterConstraintRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount)
        {
            return NonExact(
                sourceEnds,
                genericParameterAuthority,
                MetadataGenericParameterConstraintPhysicalTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName,
                    StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount),
                StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount + 1);
        }

        if (observations.IsDefault || observedCount < sourceCount)
        {
            return NonExact(
                sourceEnds,
                genericParameterAuthority,
                MetadataGenericParameterConstraintPhysicalTableIssue.TableIncomplete,
                null,
                observedCount);
        }

        if (observedCount > sourceCount)
        {
            return Invalid(
                sourceEnds,
                genericParameterAuthority,
                MetadataGenericParameterConstraintPhysicalTableIssue.TableRowCountConflict,
                observedCount);
        }

        if (sourceCount == 0)
        {
            return ExactEmpty(sourceEnds, genericParameterAuthority);
        }

        var copied = ExpressionV2ContractEncoding.Copy(observations);
        var resolved = new ResolvedObservation[copied.Length];
        var seenConstraints = new HashSet<OwnerConstraintTuple>();
        var previousOwnerRowId = 0;
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null)
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.PhysicalRowMissing,
                    copied.Length);
            }

            if (observation.GenericParameterConstraintToken != (0x2C00_0000 | checked(index + 1)))
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.PhysicalOrderInvalid,
                    copied.Length);
            }

            if (!observation.MetadataModule.Equals(sourceEnds.SourceModule))
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.SourceModuleMismatch,
                    copied.Length);
            }

            if (!TokenHasTable(observation.OwnerGenericParameterToken, 0x2A))
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.OwnerTokenKindInvalid,
                    copied.Length);
            }

            var ownerRowId = CanonicalReplayEncoding.MetadataTokenRowId(observation.OwnerGenericParameterToken);
            if (ownerRowId <= 0 || ownerRowId > sourceEnds.GenericParameterRowCount)
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.OwnerTokenOutOfRange,
                    copied.Length);
            }

            var ownerParameter = genericParameterAuthority.FindGenericParameter(
                observation.OwnerGenericParameterToken);
            var ownerGroup = genericParameterAuthority.ExactOwnerGroupOrDefault(
                observation.OwnerGenericParameterToken);
            if (ownerParameter is null || ownerGroup is null ||
                !ownerGroup.ExactParameters.Any(parameter => parameter.Equals(ownerParameter)))
            {
                throw new InvalidOperationException(
                    "Exact GenericParam authority must resolve every in-range physical GenericParam token exactly once.");
            }

            if (ownerRowId < previousOwnerRowId)
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.OwnerOrderInvalid,
                    copied.Length);
            }
            previousOwnerRowId = ownerRowId;

            if (!IsTypeDefOrRefTable(observation.ConstraintMetadataToken))
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.ConstraintTokenKindInvalid,
                    copied.Length);
            }

            if (!sourceEnds.ContainsTypeDefOrRefToken(observation.ConstraintMetadataToken))
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.ConstraintTokenOutOfRange,
                    copied.Length);
            }

            if (!seenConstraints.Add(new OwnerConstraintTuple(
                    observation.OwnerGenericParameterToken,
                    observation.ConstraintMetadataToken)))
            {
                return Invalid(
                    sourceEnds,
                    genericParameterAuthority,
                    MetadataGenericParameterConstraintPhysicalTableIssue.DuplicateOwnerConstraint,
                    copied.Length);
            }

            resolved[index] = new ResolvedObservation(observation, ownerParameter, ownerGroup);
        }

        var rowBuilder =
            ImmutableArray.CreateBuilder<MetadataGenericParameterConstraintTableRowIdentity>(resolved.Length);
        var groupedRows = new Dictionary<int, List<MetadataGenericParameterConstraintTableRowIdentity>>();
        foreach (var value in resolved)
        {
            var row = MetadataGenericParameterConstraintTableRowIdentity.Create(
                RowMintCapability,
                sourceEnds,
                value.Observation,
                value.OwnerParameter,
                value.OwnerGroup);
            rowBuilder.Add(row);
            if (!groupedRows.TryGetValue(row.OwnerGenericParameterToken, out var group))
            {
                group = [];
                groupedRows.Add(row.OwnerGenericParameterToken, group);
            }
            group.Add(row);
        }

        var exactRows = rowBuilder.MoveToImmutable();
        var rowsByOwnerBuilder = ImmutableDictionary.CreateBuilder<
            int,
            ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>>();
        foreach (var group in groupedRows)
        {
            rowsByOwnerBuilder.Add(group.Key, group.Value.ToImmutableArray());
        }

        return new MetadataGenericParameterConstraintPhysicalTableCatalogIdentity(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            MetadataGenericParameterConstraintPhysicalTableIssue.None,
            sourceEnds,
            genericParameterAuthority,
            exactRows,
            rowsByOwnerBuilder.ToImmutable(),
            null,
            0);
    }

    /// <summary>Gets exact draft constraint rows for one exact authority-issued GenericParam row.</summary>
    /// <param name="ownerParameter">The exact authority-issued GenericParam row whose constraints are requested.</param>
    /// <returns>
    /// A defensive physical-order row copy, or an initialized empty array when the catalog is not exact, the parameter
    /// belongs to another authority, or the exact owner has no physical constraint row.
    /// </returns>
    public ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity> RowsForOwnerOrEmpty(
        MetadataGenericParameterTableRowIdentity ownerParameter)
    {
        ArgumentNullException.ThrowIfNull(ownerParameter);
        if (ResultKind != MetadataGenericParameterConstraintPhysicalTableResultKind.Exact)
        {
            return ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>.Empty;
        }

        var selected = GenericParameterAuthority.FindGenericParameter(ownerParameter.GenericParameterToken);
        if (selected is null || !selected.Equals(ownerParameter))
        {
            return ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>.Empty;
        }

        return rowsByOwnerToken.TryGetValue(ownerParameter.GenericParameterToken, out var ownerRows)
            ? ExpressionV2ContractEncoding.Copy(ownerRows)
            : ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>.Empty;
    }

    /// <summary>Tests canonical equality between two complete GenericParamConstraint-table draft proofs.</summary>
    /// <param name="other">The other complete physical draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterConstraintPhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete GenericParamConstraint-table draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataGenericParameterConstraintPhysicalTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable complete-table draft content.</summary>
    /// <returns>A hash code for this complete physical GenericParamConstraint catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);

    private static MetadataGenericParameterConstraintPhysicalTableCatalogIdentity ExactEmpty(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterAuthorityCatalogIdentity genericParameterAuthority) =>
        new(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            MetadataGenericParameterConstraintPhysicalTableIssue.None,
            sourceEnds,
            genericParameterAuthority,
            ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>.Empty,
            ImmutableDictionary<int, ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>>.Empty,
            null,
            0);

    private static MetadataGenericParameterConstraintPhysicalTableCatalogIdentity NonExact(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterAuthorityCatalogIdentity genericParameterAuthority,
        MetadataGenericParameterConstraintPhysicalTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            MetadataGenericParameterConstraintPhysicalTableResultKind.NonExact,
            issue,
            sourceEnds,
            genericParameterAuthority,
            ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>.Empty,
            ImmutableDictionary<int, ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>>.Empty,
            reachedBound,
            observedCount);

    private static MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Invalid(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterAuthorityCatalogIdentity genericParameterAuthority,
        MetadataGenericParameterConstraintPhysicalTableIssue issue,
        int observedCount) =>
        new(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Invalid,
            issue,
            sourceEnds,
            genericParameterAuthority,
            ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>.Empty,
            ImmutableDictionary<int, ImmutableArray<MetadataGenericParameterConstraintTableRowIdentity>>.Empty,
            null,
            observedCount);

    private static bool TokenHasTable(int metadataToken, byte tableIndex) =>
        (metadataToken & unchecked((int)0xFF00_0000)) == tableIndex << 24;

    private static bool IsTypeDefOrRefTable(int metadataToken)
    {
        var table = metadataToken & unchecked((int)0xFF00_0000);
        return table is 0x0100_0000 or 0x0200_0000 or 0x1B00_0000;
    }

    private readonly record struct OwnerConstraintTuple(
        int OwnerGenericParameterToken,
        int ConstraintMetadataToken);

    private readonly record struct ResolvedObservation(
        MetadataGenericParameterConstraintRowObservationIdentity Observation,
        MetadataGenericParameterTableRowIdentity OwnerParameter,
        MetadataGenericParameterAuthorityOwnerGroupIdentity OwnerGroup);
}
