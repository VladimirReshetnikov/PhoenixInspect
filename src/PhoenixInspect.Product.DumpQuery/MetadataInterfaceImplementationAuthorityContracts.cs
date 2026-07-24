using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete authority-joined InterfaceImpl-table proof.</summary>
/// <remarks>
/// This discriminator separates a complete exact physical table from an acquisition stop and contradictory
/// physical evidence. Even an exact result retains the Interface column as an unresolved TypeDefOrRef token and makes
/// no interface-semantic claim.
/// </remarks>
public enum MetadataInterfaceImplementationTableResultKind
{
    /// <summary>Every physical InterfaceImpl row and its authority-issued implementing TypeDef is exact.</summary>
    Exact = 1,

    /// <summary>Complete acquisition or the definition-authority prerequisite was unavailable or bounded.</summary>
    NonExact = 2,

    /// <summary>The supplied complete-table claim or its prerequisite retained contradictory evidence.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one InterfaceImpl-table proof.</summary>
/// <remarks>
/// The issue remains explicit when no row prefix is exposed, so later composition never has to infer why the
/// complete implementation table was unavailable.
/// </remarks>
public enum MetadataInterfaceImplementationTableIssue
{
    /// <summary>No issue applies to an exact complete catalog.</summary>
    None = 0,

    /// <summary>The exact source InterfaceImpl table end crossed the admitted complete row count.</summary>
    TableRowBoundReached = 1,

    /// <summary>The supplied observations were unavailable or stopped before the exact source end.</summary>
    TableIncomplete = 2,

    /// <summary>The supplied observation count exceeded the exact InterfaceImpl source end.</summary>
    TableRowCountConflict = 3,

    /// <summary>The supplied InterfaceImpl tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 4,

    /// <summary>At least one physical InterfaceImpl observation belonged to another metadata module.</summary>
    SourceMismatch = 5,

    /// <summary>The complete definition-authority prerequisite stopped without an exact authority catalog.</summary>
    DefinitionAuthorityNonExact = 6,

    /// <summary>The complete definition-authority prerequisite retained contradictory evidence.</summary>
    DefinitionAuthorityInvalid = 7,

    /// <summary>A physical Class-column token named no authority-issued TypeDef.</summary>
    ImplementingTypeDefinitionMissing = 8,

    /// <summary>An Interface-column token was outside the TypeDefOrRef domain or beyond its source end.</summary>
    InterfaceTokenOutOfRange = 9,

    /// <summary>Two physical rows repeated the same implementing TypeDef and unresolved Interface token pair.</summary>
    DuplicateSemanticEdge = 10,
}

/// <summary>Freezes only the three physical columns observed from one InterfaceImpl table row.</summary>
/// <remarks>
/// This sealed observation carries the source module, the physical row token, the raw Class-column TypeDef
/// token, and the raw Interface-column TypeDefOrRef token. It contains no caller-asserted authority row, resolved
/// interface target, decoded signature, or transitive implementation claim.
/// </remarks>
public sealed class MetadataInterfaceImplementationRowObservationIdentity :
    IEquatable<MetadataInterfaceImplementationRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interfaceimpl-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int interfaceImplementationToken,
        int implementingTypeDefinitionToken,
        int interfaceMetadataToken)
    {
        MetadataModule = metadataModule;
        InterfaceImplementationToken = interfaceImplementationToken;
        ImplementingTypeDefinitionToken = implementingTypeDefinitionToken;
        InterfaceMetadataToken = interfaceMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(interfaceImplementationToken);
        writer.WriteInt32(implementingTypeDefinitionToken);
        writer.WriteInt32(interfaceMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the observed physical row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact non-nil InterfaceImpl token identifying the physical row.</summary>
    public int InterfaceImplementationToken { get; }

    /// <summary>Gets the unchanged raw Class-column TypeDef token of the physical row.</summary>
    public int ImplementingTypeDefinitionToken { get; }

    /// <summary>Gets the unchanged raw Interface-column TypeDefOrRef token of the physical row.</summary>
    public int InterfaceMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one physical-column-only InterfaceImpl row observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the physical row.</param>
    /// <param name="interfaceImplementationToken">The exact non-nil InterfaceImpl table token and RID.</param>
    /// <param name="implementingTypeDefinitionToken">The raw Class-column TypeDef token, retained unresolved.</param>
    /// <param name="interfaceMetadataToken">
    /// The raw Interface-column TypeDefOrRef token, retained without resolving its target.
    /// </param>
    /// <returns>A sealed immutable observation containing no derived owner or target fact.</returns>
    public static MetadataInterfaceImplementationRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int interfaceImplementationToken,
        int implementingTypeDefinitionToken,
        int interfaceMetadataToken)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(
            interfaceImplementationToken,
            0x09,
            nameof(interfaceImplementationToken));
        CanonicalReplayEncoding.ValidateMetadataToken(
            implementingTypeDefinitionToken,
            0x02,
            nameof(implementingTypeDefinitionToken));
        return new MetadataInterfaceImplementationRowObservationIdentity(
            metadataModule,
            interfaceImplementationToken,
            implementingTypeDefinitionToken,
            interfaceMetadataToken);
    }

    /// <summary>Tests canonical equality between two physical InterfaceImpl observations.</summary>
    /// <param name="other">The other physical observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical InterfaceImpl equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataInterfaceImplementationRowObservationIdentity);

    /// <summary>Computes a deterministic hash code from immutable physical-column content.</summary>
    /// <returns>A hash code for this physical InterfaceImpl observation.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one InterfaceImpl row after complete source and definition-authority validation.</summary>
/// <remarks>
/// This sealed identity has no public issuer. It retains the authority-issued implementing TypeDef, but
/// deliberately leaves the Interface column as an unresolved physical TypeDefOrRef token. Canonical authority
/// references are fixed-size SHA-256 values so row size never grows with the authority catalog.
/// </remarks>
public sealed class MetadataInterfaceImplementationTableRowIdentity :
    IEquatable<MetadataInterfaceImplementationTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interfaceimpl-table-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationTableRowIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataInterfaceImplementationRowObservationIdentity observation,
        MetadataTypeDefinitionAuthorityIdentity implementingTypeDefinition)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        ImplementingTypeDefinition = implementingTypeDefinition;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceEnds.Sha256, nameof(sourceEnds));
        writer.WriteSha256(observation.Sha256, nameof(observation));
        writer.WriteSha256(implementingTypeDefinition.Sha256, nameof(implementingTypeDefinition));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source ends against which this complete row was validated.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical-column-only observation.</summary>
    public MetadataInterfaceImplementationRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact authority-issued TypeDef selected by the physical Class column.</summary>
    public MetadataTypeDefinitionAuthorityIdentity ImplementingTypeDefinition { get; }

    /// <summary>Gets the exact metadata module owning this complete row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => Observation.MetadataModule;

    /// <summary>Gets the exact InterfaceImpl row token forwarded from the physical observation.</summary>
    public int InterfaceImplementationToken => Observation.InterfaceImplementationToken;

    /// <summary>Gets the exact authority-resolved implementing TypeDef token.</summary>
    public int ImplementingTypeDefinitionToken => ImplementingTypeDefinition.TypeDefinitionToken;

    /// <summary>Gets the validated but deliberately unresolved physical TypeDefOrRef Interface token.</summary>
    public int InterfaceMetadataToken => Observation.InterfaceMetadataToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical row bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two exact InterfaceImpl table-row identities.</summary>
    /// <param name="other">The other exact row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests exact InterfaceImpl row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataInterfaceImplementationTableRowIdentity);

    /// <summary>Computes a deterministic hash code from immutable exact row content.</summary>
    /// <returns>A hash code for this exact InterfaceImpl row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataInterfaceImplementationTableRowIdentity Create(
        object mintCapability,
        MetadataSourceEndIdentity sourceEnds,
        MetadataInterfaceImplementationRowObservationIdentity observation,
        MetadataTypeDefinitionAuthorityIdentity implementingTypeDefinition)
    {
        if (!MetadataInterfaceImplementationTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An exact InterfaceImpl row requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(implementingTypeDefinition);
        if (implementingTypeDefinition.TypeDefinitionToken != observation.ImplementingTypeDefinitionToken)
        {
            throw new ArgumentException(
                "The authority-issued implementing TypeDef must match the physical Class column.",
                nameof(implementingTypeDefinition));
        }

        return new MetadataInterfaceImplementationTableRowIdentity(
            sourceEnds,
            observation,
            implementingTypeDefinition);
    }
}

/// <summary>Freezes one complete source-anchored physical InterfaceImpl table projection.</summary>
/// <remarks>
/// This sealed catalog resolves every physical Class column exclusively through one exact
/// <see cref="MetadataDefinitionAuthorityCatalogIdentity"/>. It validates Interface-column token kind and source range
/// and rejects a repeated implementing-TypeDef and Interface-token pair without resolving any target semantics.
/// Non-exact and invalid outcomes retain no derived row prefix.
/// </remarks>
public sealed class MetadataInterfaceImplementationTableCatalogIdentity :
    IEquatable<MetadataInterfaceImplementationTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interfaceimpl-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataInterfaceImplementationTableRowIdentity> rows;
    private readonly ImmutableDictionary<int, ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>>
        rowsByImplementingToken;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationTableCatalogIdentity(
        MetadataInterfaceImplementationTableResultKind resultKind,
        MetadataInterfaceImplementationTableIssue issue,
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataInterfaceImplementationTableRowIdentity> rows,
        ImmutableDictionary<int, ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>>
            rowsByImplementingToken,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        DefinitionAuthority = definitionAuthority;
        this.rows = ExpressionV2ContractEncoding.Copy(rows);
        this.rowsByImplementingToken = rowsByImplementingToken;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(definitionAuthority.Sha256, nameof(definitionAuthority));
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this complete physical table is exact, non-exact, or invalid.</summary>
    public MetadataInterfaceImplementationTableResultKind ResultKind { get; }

    /// <summary>Gets the typed table issue, or none for an exact result.</summary>
    public MetadataInterfaceImplementationTableIssue Issue { get; }

    /// <summary>Gets the complete physical definition-authority catalog supplying every implementing owner.</summary>
    public MetadataDefinitionAuthorityCatalogIdentity DefinitionAuthority { get; }

    /// <summary>Gets the exact source ends inherited from the physical definition-authority catalog.</summary>
    public MetadataSourceEndIdentity SourceEnds => DefinitionAuthority.SourceEnds;

    /// <summary>Gets a defensive RID-order copy of exact derived rows, or an initialized empty array otherwise.</summary>
    public ImmutableArray<MetadataInterfaceImplementationTableRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the propagated or complete-table bound only for a prefix-free non-exact result.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied, prerequisite, or cap-plus-one row count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the InterfaceImpl or prerequisite token related to the issue, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical catalog bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete authority-joined physical InterfaceImpl-table projection.</summary>
    /// <param name="definitionAuthority">The complete physical definition-authority prerequisite.</param>
    /// <param name="observations">
    /// Every physical InterfaceImpl row in RID order; a default array denotes unavailable complete acquisition.
    /// </param>
    /// <returns>An exact complete catalog, a prefix-free non-exact stop, or a factless invalid result.</returns>
    public static MetadataInterfaceImplementationTableCatalogIdentity Create(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(definitionAuthority);
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.NonExact)
        {
            return NonExact(
                definitionAuthority,
                MetadataInterfaceImplementationTableIssue.DefinitionAuthorityNonExact,
                definitionAuthority.ReachedBound,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }
        if (definitionAuthority.ResultKind == MetadataDefinitionAuthorityResultKind.Invalid)
        {
            return Invalid(
                definitionAuthority,
                MetadataInterfaceImplementationTableIssue.DefinitionAuthorityInvalid,
                definitionAuthority.ObservedCount,
                definitionAuthority.RelatedMetadataToken);
        }

        var sourceEnds = definitionAuthority.SourceEnds;
        var sourceCount = sourceEnds.InterfaceImplementationRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumInterfaceImplementationRowCount)
        {
            return NonExact(
                definitionAuthority,
                MetadataInterfaceImplementationTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.InterfaceImplementationRowCountBoundName,
                    StaticFieldV2Limits.MaximumInterfaceImplementationRowCount),
                StaticFieldV2Limits.MaximumInterfaceImplementationRowCount + 1,
                null);
        }
        if (observations.IsDefault && sourceCount == 0)
        {
            observations = ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>.Empty;
        }
        if (observations.IsDefault || observations.Length < sourceCount)
        {
            return NonExact(
                definitionAuthority,
                MetadataInterfaceImplementationTableIssue.TableIncomplete,
                null,
                observations.IsDefault ? 0 : observations.Length,
                null);
        }
        if (observations.Length > sourceCount)
        {
            return Invalid(
                definitionAuthority,
                MetadataInterfaceImplementationTableIssue.TableRowCountConflict,
                observations.Length,
                null);
        }

        var copied = ExpressionV2ContractEncoding.Copy(observations);
        var owners = new MetadataTypeDefinitionAuthorityIdentity[copied.Length];
        var seenEdges = new HashSet<ImplementationEdgeTuple>();
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null ||
                observation.InterfaceImplementationToken != (0x0900_0000 | checked(index + 1)))
            {
                return Invalid(
                    definitionAuthority,
                    MetadataInterfaceImplementationTableIssue.PhysicalOrderInvalid,
                    copied.Length,
                    observation?.InterfaceImplementationToken);
            }
            if (!observation.MetadataModule.Equals(sourceEnds.SourceModule))
            {
                return Invalid(
                    definitionAuthority,
                    MetadataInterfaceImplementationTableIssue.SourceMismatch,
                    copied.Length,
                    observation.InterfaceImplementationToken);
            }

            var owner = definitionAuthority.ExactTypeDefinitionOrDefault(
                observation.ImplementingTypeDefinitionToken);
            if (owner is null)
            {
                return Invalid(
                    definitionAuthority,
                    MetadataInterfaceImplementationTableIssue.ImplementingTypeDefinitionMissing,
                    copied.Length,
                    observation.ImplementingTypeDefinitionToken);
            }
            if (!sourceEnds.ContainsTypeDefOrRefToken(observation.InterfaceMetadataToken))
            {
                return Invalid(
                    definitionAuthority,
                    MetadataInterfaceImplementationTableIssue.InterfaceTokenOutOfRange,
                    copied.Length,
                    observation.InterfaceMetadataToken);
            }
            if (!seenEdges.Add(new ImplementationEdgeTuple(
                    observation.ImplementingTypeDefinitionToken,
                    observation.InterfaceMetadataToken)))
            {
                return Invalid(
                    definitionAuthority,
                    MetadataInterfaceImplementationTableIssue.DuplicateSemanticEdge,
                    copied.Length,
                    observation.InterfaceImplementationToken);
            }

            owners[index] = owner;
        }

        var derived = ImmutableArray.CreateBuilder<MetadataInterfaceImplementationTableRowIdentity>(copied.Length);
        var grouped = new Dictionary<int, List<MetadataInterfaceImplementationTableRowIdentity>>();
        for (var index = 0; index < copied.Length; index++)
        {
            var row = MetadataInterfaceImplementationTableRowIdentity.Create(
                RowMintCapability,
                sourceEnds,
                copied[index],
                owners[index]);
            derived.Add(row);
            if (!grouped.TryGetValue(row.ImplementingTypeDefinitionToken, out var group))
            {
                group = [];
                grouped.Add(row.ImplementingTypeDefinitionToken, group);
            }
            group.Add(row);
        }

        var rowsByOwner = ImmutableDictionary.CreateBuilder<
            int,
            ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>>();
        foreach (var group in grouped)
        {
            rowsByOwner.Add(group.Key, group.Value.ToImmutableArray());
        }

        return new MetadataInterfaceImplementationTableCatalogIdentity(
            MetadataInterfaceImplementationTableResultKind.Exact,
            MetadataInterfaceImplementationTableIssue.None,
            definitionAuthority,
            derived.MoveToImmutable(),
            rowsByOwner.ToImmutable(),
            null,
            0,
            null);
    }

    /// <summary>Finds the exact derived row for one physical InterfaceImpl token.</summary>
    /// <param name="interfaceImplementationToken">The physical InterfaceImpl token to look up.</param>
    /// <returns>The exact derived row, or null for a non-exact catalog or an unknown token.</returns>
    public MetadataInterfaceImplementationTableRowIdentity? FindRow(int interfaceImplementationToken)
    {
        if (ResultKind != MetadataInterfaceImplementationTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(interfaceImplementationToken, 0x09))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(interfaceImplementationToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    /// <summary>Projects the exact derived rows declared by one authority-issued implementing TypeDef.</summary>
    /// <param name="implementingType">The authority-issued TypeDef whose InterfaceImpl rows are requested.</param>
    /// <returns>
    /// The derived rows in physical RID order, or an initialized empty array for a non-exact catalog, a TypeDef
    /// this catalog's own definition authority did not issue, or an owner with no physical InterfaceImpl row.
    /// </returns>
    public ImmutableArray<MetadataInterfaceImplementationTableRowIdentity> RowsForImplementingTypeOrEmpty(
        MetadataTypeDefinitionAuthorityIdentity implementingType)
    {
        if (ResultKind != MetadataInterfaceImplementationTableResultKind.Exact || implementingType is null)
        {
            return ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>.Empty;
        }

        var issued = DefinitionAuthority.ExactTypeDefinitionOrDefault(implementingType.TypeDefinitionToken);
        if (issued is null || !issued.Equals(implementingType))
        {
            return ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>.Empty;
        }

        return rowsByImplementingToken.TryGetValue(implementingType.TypeDefinitionToken, out var owned)
            ? ExpressionV2ContractEncoding.Copy(owned)
            : ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>.Empty;
    }

    /// <summary>Tests canonical equality between two complete InterfaceImpl-table catalogs.</summary>
    /// <param name="other">The other complete physical catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete InterfaceImpl-table equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataInterfaceImplementationTableCatalogIdentity);

    /// <summary>Computes a deterministic hash code from immutable complete-table content.</summary>
    /// <returns>A hash code for this complete physical InterfaceImpl catalog.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);

    private static MetadataInterfaceImplementationTableCatalogIdentity NonExact(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataInterfaceImplementationTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            MetadataInterfaceImplementationTableResultKind.NonExact,
            issue,
            definitionAuthority,
            ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>.Empty,
            ImmutableDictionary<int, ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>>.Empty,
            reachedBound,
            observedCount,
            relatedMetadataToken);

    private static MetadataInterfaceImplementationTableCatalogIdentity Invalid(
        MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
        MetadataInterfaceImplementationTableIssue issue,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            MetadataInterfaceImplementationTableResultKind.Invalid,
            issue,
            definitionAuthority,
            ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>.Empty,
            ImmutableDictionary<int, ImmutableArray<MetadataInterfaceImplementationTableRowIdentity>>.Empty,
            null,
            observedCount,
            relatedMetadataToken);

    private readonly record struct ImplementationEdgeTuple(
        int ImplementingTypeDefinitionToken,
        int InterfaceMetadataToken);
}

/// <summary>Classifies the physical form of one resolved InterfaceImpl edge.</summary>
/// <remarks>
/// The values mirror <see cref="MetadataImmediateBaseEdgeKind"/> without its nil form, because the physical Interface
/// column is never nil. Every value is a complete derived row fact rather than a portfolio failure.
/// </remarks>
public enum MetadataInterfaceImplementationEdgeKind
{
    /// <summary>The Interface column names a TypeDef in the same module.</summary>
    TypeDefinition = 1,

    /// <summary>The Interface column names a TypeRef whose portfolio resolution is exact.</summary>
    TypeReference = 2,

    /// <summary>The Interface column names a TypeRef whose portfolio resolution retained a typed stop.</summary>
    TypeReferenceUnresolved = 3,

    /// <summary>The Interface column names a TypeSpec generic instantiation retained undecoded.</summary>
    TypeSpecification = 4,
}

/// <summary>Freezes one guarded authority-derived resolved InterfaceImpl edge.</summary>
/// <remarks>
/// The edge joins one authority-issued InterfaceImpl row to its exact named interface target or to a retained
/// unresolved TypeRef or undecoded TypeSpec row. Decoding a generic interface instantiation belongs to the later
/// construction work and never happens here.
/// </remarks>
public sealed class MetadataInterfaceImplementationEdgeAuthorityIdentity :
    IEquatable<MetadataInterfaceImplementationEdgeAuthorityIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interfaceimpl-edge";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationEdgeAuthorityIdentity(
        MetadataInterfaceImplementationTableRowIdentity implementationRow,
        MetadataInterfaceImplementationEdgeKind kind,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeReferenceResolutionIdentity? referenceResolution,
        MetadataTypeSpecificationPhysicalRowIdentity? genericInterfaceRow)
    {
        ImplementationRow = implementationRow;
        Kind = kind;
        TargetModule = targetModule;
        TargetTypeDefinition = targetTypeDefinition;
        ReferenceResolution = referenceResolution;
        GenericInterfaceRow = genericInterfaceRow;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(implementationRow.Sha256, nameof(implementationRow));
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetModule?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, targetTypeDefinition?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, referenceResolution?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, genericInterfaceRow?.Sha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged authority-issued InterfaceImpl row.</summary>
    public MetadataInterfaceImplementationTableRowIdentity ImplementationRow { get; }

    /// <summary>Gets the exact metadata module owning the physical Interface column.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => ImplementationRow.SourceModule;

    /// <summary>Gets the authority-issued TypeDef declaring this edge.</summary>
    public MetadataTypeDefinitionAuthorityIdentity ImplementingTypeDefinition =>
        ImplementationRow.ImplementingTypeDefinition;

    /// <summary>Gets the exact InterfaceImpl row token forwarded from the row.</summary>
    public int InterfaceImplementationToken => ImplementationRow.InterfaceImplementationToken;

    /// <summary>Gets the validated raw physical TypeDefOrRef Interface token.</summary>
    public int InterfaceMetadataToken => ImplementationRow.InterfaceMetadataToken;

    /// <summary>Gets the complete typed physical form of this edge.</summary>
    public MetadataInterfaceImplementationEdgeKind Kind { get; }

    /// <summary>Gets the exact interface target module for a named resolved edge, otherwise null.</summary>
    public StaticFieldMetadataModuleIdentity? TargetModule { get; }

    /// <summary>Gets the exact authority-issued interface TypeDef for a named resolved edge, otherwise null.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? TargetTypeDefinition { get; }

    /// <summary>Gets the retained TypeRef resolution row for a reference-formed edge, otherwise null.</summary>
    public MetadataTypeReferenceResolutionIdentity? ReferenceResolution { get; }

    /// <summary>Gets the retained undecoded physical TypeSpec row for a generic interface, otherwise null.</summary>
    public MetadataTypeSpecificationPhysicalRowIdentity? GenericInterfaceRow { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical edge bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical edge bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two resolved InterfaceImpl edges.</summary>
    /// <param name="other">The other edge.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationEdgeAuthorityIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests resolved InterfaceImpl edge equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an edge with identical canonical content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataInterfaceImplementationEdgeAuthorityIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical content.</summary>
    /// <returns>A hash code for this resolved InterfaceImpl edge.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataInterfaceImplementationEdgeAuthorityIdentity Create(
        object mintCapability,
        MetadataInterfaceImplementationTableRowIdentity implementationRow,
        MetadataInterfaceImplementationEdgeKind kind,
        StaticFieldMetadataModuleIdentity? targetModule,
        MetadataTypeDefinitionAuthorityIdentity? targetTypeDefinition,
        MetadataTypeReferenceResolutionIdentity? referenceResolution,
        MetadataTypeSpecificationPhysicalRowIdentity? genericInterfaceRow)
    {
        if (!MetadataInterfaceImplementationPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An InterfaceImpl edge requires the interface-implementation portfolio's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(implementationRow);
        var named = kind is MetadataInterfaceImplementationEdgeKind.TypeDefinition or
            MetadataInterfaceImplementationEdgeKind.TypeReference;
        if (named == (targetModule is null) || named == (targetTypeDefinition is null))
        {
            throw new ArgumentException(
                "An InterfaceImpl edge retains a named target exactly when its kind is a resolved definition.",
                nameof(kind));
        }

        return new MetadataInterfaceImplementationEdgeAuthorityIdentity(
            implementationRow,
            kind,
            targetModule,
            targetTypeDefinition,
            referenceResolution,
            genericInterfaceRow);
    }
}

/// <summary>Classifies the terminal of one bounded transitive interface closure.</summary>
/// <remarks>
/// Every terminal is a complete derived fact including retained generic interfaces, cycles, and reached bounds.
/// Only <see cref="Complete"/> proves the closure enumerated every implemented interface.
/// </remarks>
public enum MetadataInterfaceImplementationClosureTerminalKind
{
    /// <summary>Every direct, inherited, and interface-extended edge resolved to a named definition.</summary>
    Complete = 1,

    /// <summary>An Interface column or a base edge named a TypeRef whose resolution retained a typed stop.</summary>
    UnresolvedReferenceReached = 2,

    /// <summary>An Interface column or a base edge named a TypeSpec generic instantiation retained undecoded.</summary>
    GenericInterfaceReached = 3,

    /// <summary>The traversal revisited one physical TypeDef coordinate already on its own path.</summary>
    CycleDetected = 4,

    /// <summary>The traversal reached the declared ancestry depth bound plus one.</summary>
    DepthBoundReached = 5,
}

/// <summary>Freezes one guarded bounded transitive interface closure for a single TypeDef.</summary>
/// <remarks>
/// The closure unions the subject's direct InterfaceImpl edges, the edges of every interface it transitively
/// extends, and the edges of every type on the subject's bounded ancestry chain. It performs no generic
/// substitution and stops contributing at the first typed terminal it records.
/// </remarks>
public sealed class MetadataInterfaceImplementationClosureIdentity :
    IEquatable<MetadataInterfaceImplementationClosureIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interfaceimpl-closure";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> interfaceEdges;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationClosureIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity subject,
        ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> interfaceEdges,
        MetadataInterfaceImplementationClosureTerminalKind terminalKind,
        EvaluationDeterministicBound? reachedBound,
        int? relatedMetadataToken)
    {
        SourceModule = sourceModule;
        Subject = subject;
        this.interfaceEdges = ExpressionV2ContractEncoding.Copy(interfaceEdges);
        TerminalKind = terminalKind;
        ReachedBound = reachedBound;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(sourceModule.Sha256, nameof(sourceModule));
        writer.WriteSha256(subject.Sha256, nameof(subject));
        writer.WriteInt32(interfaceEdges.Length);
        foreach (var edge in interfaceEdges)
        {
            writer.WriteSha256(edge.Sha256, nameof(interfaceEdges));
        }
        writer.WriteInt32((int)terminalKind);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module owning the subject of this closure.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the authority-issued subject TypeDef of this closure.</summary>
    public MetadataTypeDefinitionAuthorityIdentity Subject { get; }

    /// <summary>Gets the exact subject TypeDef token of this closure.</summary>
    public int SubjectTypeDefinitionToken => Subject.TypeDefinitionToken;

    /// <summary>Gets a defensive discovery-order copy of one edge per distinct resolved interface.</summary>
    public ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> InterfaceEdges =>
        ExpressionV2ContractEncoding.Copy(interfaceEdges);

    /// <summary>Gets the complete typed terminal of this bounded closure.</summary>
    public MetadataInterfaceImplementationClosureTerminalKind TerminalKind { get; }

    /// <summary>Gets the declared ancestry depth bound only after a cap-plus-one traversal.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the terminal-related metadata token, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical closure bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical closure bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests whether this closure proved one named interface coordinate.</summary>
    /// <param name="interfaceModule">The exact metadata module defining the candidate interface.</param>
    /// <param name="interfaceTypeDefinitionToken">The candidate interface TypeDef token in that module.</param>
    /// <returns><see langword="true"/> only when a resolved edge names that exact coordinate.</returns>
    public bool ContainsInterface(
        StaticFieldMetadataModuleIdentity interfaceModule,
        int interfaceTypeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(interfaceModule);
        foreach (var edge in interfaceEdges)
        {
            if (edge.TargetModule is { } module &&
                edge.TargetTypeDefinition is { } target &&
                module.Equals(interfaceModule) &&
                target.TypeDefinitionToken == interfaceTypeDefinitionToken)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Tests canonical equality between two bounded interface closures.</summary>
    /// <param name="other">The other closure.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationClosureIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests bounded interface closure equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a closure with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataInterfaceImplementationClosureIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical content.</summary>
    /// <returns>A hash code for this bounded interface closure.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataInterfaceImplementationClosureIdentity Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataTypeDefinitionAuthorityIdentity subject,
        ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> interfaceEdges,
        MetadataInterfaceImplementationClosureTerminalKind terminalKind,
        EvaluationDeterministicBound? reachedBound,
        int? relatedMetadataToken)
    {
        if (!MetadataInterfaceImplementationPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An interface closure requires the interface-implementation portfolio's private mint capability.",
                nameof(mintCapability));
        }
        if ((terminalKind == MetadataInterfaceImplementationClosureTerminalKind.DepthBoundReached) ==
            (reachedBound is null))
        {
            throw new ArgumentException(
                "An interface closure retains a reached bound exactly when its terminal is the depth bound.",
                nameof(reachedBound));
        }

        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(subject);
        return new MetadataInterfaceImplementationClosureIdentity(
            sourceModule,
            subject,
            interfaceEdges,
            terminalKind,
            reachedBound,
            relatedMetadataToken);
    }
}

/// <summary>Classifies one three-valued answer to a bounded interface-implementation question.</summary>
/// <remarks>
/// <see cref="No"/> is returned only over a complete closure. Every incomplete closure answers
/// <see cref="Unprovable"/> so no consumer can read an acquisition stop as a proved negative.
/// </remarks>
public enum MetadataInterfaceImplementationAnswer
{
    /// <summary>The bounded closure proved the named interface coordinate.</summary>
    Yes = 1,

    /// <summary>The complete bounded closure proved the named interface coordinate absent.</summary>
    No = 2,

    /// <summary>The portfolio stopped or the bounded closure was incomplete.</summary>
    Unprovable = 3,
}

/// <summary>Freezes one guarded per-module entry of the interface-implementation portfolio.</summary>
/// <remarks>
/// The entry retains its complete InterfaceImpl catalog and ancestry lineage, one RID-ordered resolved edge per
/// physical row, and one bounded transitive closure per authority-issued TypeDef.
/// </remarks>
public sealed class MetadataInterfaceImplementationPortfolioModuleIdentity :
    IEquatable<MetadataInterfaceImplementationPortfolioModuleIdentity>
{
    private const string CanonicalDomain = "metadata-v2-interfaceimpl-portfolio-module";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> edges;
    private readonly ImmutableArray<MetadataInterfaceImplementationClosureIdentity> closures;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationPortfolioModuleIdentity(
        MetadataInterfaceImplementationTableCatalogIdentity implementationCatalog,
        MetadataAncestryAuthorityModuleIdentity ancestryEntry,
        ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> edges,
        ImmutableArray<MetadataInterfaceImplementationClosureIdentity> closures)
    {
        ImplementationCatalog = implementationCatalog;
        AncestryEntry = ancestryEntry;
        this.edges = ExpressionV2ContractEncoding.Copy(edges);
        this.closures = ExpressionV2ContractEncoding.Copy(closures);

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(implementationCatalog.Sha256, nameof(implementationCatalog));
        writer.WriteSha256(ancestryEntry.Sha256, nameof(ancestryEntry));
        writer.WriteInt32(edges.Length);
        foreach (var edge in edges)
        {
            writer.WriteSha256(edge.Sha256, nameof(edges));
        }
        writer.WriteInt32(closures.Length);
        foreach (var closure in closures)
        {
            writer.WriteSha256(closure.Sha256, nameof(closures));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the retained complete physical InterfaceImpl catalog for this module.</summary>
    public MetadataInterfaceImplementationTableCatalogIdentity ImplementationCatalog { get; }

    /// <summary>Gets the retained per-module ancestry entry lineage.</summary>
    public MetadataAncestryAuthorityModuleIdentity AncestryEntry { get; }

    /// <summary>Gets the exact metadata module owning every row in this entry.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule => AncestryEntry.SourceModule;

    /// <summary>Gets a defensive RID-order copy of every resolved InterfaceImpl edge.</summary>
    public ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> Edges =>
        ExpressionV2ContractEncoding.Copy(edges);

    /// <summary>Gets a defensive RID-order copy of every bounded transitive interface closure.</summary>
    public ImmutableArray<MetadataInterfaceImplementationClosureIdentity> Closures =>
        ExpressionV2ContractEncoding.Copy(closures);

    /// <summary>Gets a defensive copy of the fixed-reference canonical entry bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical entry bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Finds one resolved InterfaceImpl edge by physical row token.</summary>
    /// <param name="interfaceImplementationToken">The non-nil physical InterfaceImpl token.</param>
    /// <returns>The complete edge, or null when the token is not an issued InterfaceImpl row.</returns>
    public MetadataInterfaceImplementationEdgeAuthorityIdentity? FindEdge(int interfaceImplementationToken)
    {
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(interfaceImplementationToken, 0x09))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(interfaceImplementationToken);
        return rowId > 0 && rowId <= edges.Length ? edges[rowId - 1] : null;
    }

    /// <summary>Finds one bounded transitive interface closure by TypeDef token.</summary>
    /// <param name="typeDefinitionToken">The non-nil TypeDef token.</param>
    /// <returns>The complete closure, or null when the token is not an issued TypeDef.</returns>
    public MetadataInterfaceImplementationClosureIdentity? FindClosure(int typeDefinitionToken)
    {
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(typeDefinitionToken, 0x02))
        {
            return null;
        }
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinitionToken);
        return rowId > 0 && rowId <= closures.Length ? closures[rowId - 1] : null;
    }

    /// <summary>Tests canonical equality between two per-module interface-implementation entries.</summary>
    /// <param name="other">The other entry.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationPortfolioModuleIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests per-module interface-implementation entry equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an entry with identical canonical content.</returns>
    public override bool Equals(object? obj) =>
        Equals(obj as MetadataInterfaceImplementationPortfolioModuleIdentity);

    /// <summary>Computes a deterministic hash code from fixed-reference canonical content.</summary>
    /// <returns>A hash code for this per-module interface-implementation entry.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataInterfaceImplementationPortfolioModuleIdentity Create(
        object mintCapability,
        MetadataInterfaceImplementationTableCatalogIdentity implementationCatalog,
        MetadataAncestryAuthorityModuleIdentity ancestryEntry,
        ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> edges,
        ImmutableArray<MetadataInterfaceImplementationClosureIdentity> closures)
    {
        if (!MetadataInterfaceImplementationPortfolioIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A per-module interface entry requires the interface-implementation portfolio's mint capability.",
                nameof(mintCapability));
        }
        return new MetadataInterfaceImplementationPortfolioModuleIdentity(
            implementationCatalog,
            ancestryEntry,
            edges,
            closures);
    }
}

/// <summary>Classifies one normalized multi-module interface-implementation portfolio.</summary>
/// <remarks>Every stop is prefix-free; exact entries use the ancestry portfolio's module order.</remarks>
public enum MetadataInterfaceImplementationPortfolioResultKind
{
    /// <summary>Every module has one complete RID-ordered edge vector and one closure per TypeDef.</summary>
    Exact = 1,

    /// <summary>A prerequisite, missing vector slot, or non-exact InterfaceImpl catalog prevented completion.</summary>
    NonExact = 2,

    /// <summary>Complete inputs contradicted the ancestry portfolio or one another.</summary>
    Invalid = 3,
}

/// <summary>Identifies the deterministic issue for one interface-implementation portfolio.</summary>
/// <remarks>The issue values keep prerequisite, vector-shape, catalog, and lineage stops distinct.</remarks>
public enum MetadataInterfaceImplementationPortfolioIssue
{
    /// <summary>No issue applies to an exact portfolio.</summary>
    None = 0,

    /// <summary>The ancestry authority portfolio prerequisite was non-exact.</summary>
    AncestryPortfolioNonExact = 1,

    /// <summary>The ancestry authority portfolio prerequisite was invalid.</summary>
    AncestryPortfolioInvalid = 2,

    /// <summary>The supplied InterfaceImpl-catalog vector was default rather than explicitly initialized.</summary>
    CatalogVectorUninitialized = 3,

    /// <summary>The supplied InterfaceImpl-catalog vector reached the shared module bound plus one.</summary>
    ModuleCountBoundReached = 4,

    /// <summary>The supplied InterfaceImpl-catalog vector was shorter than the exact ancestry portfolio.</summary>
    CatalogSlotsIncomplete = 5,

    /// <summary>The supplied InterfaceImpl-catalog vector was longer than the exact ancestry portfolio.</summary>
    CatalogSlotCountConflict = 6,

    /// <summary>An initialized InterfaceImpl-catalog vector slot contained no catalog.</summary>
    CatalogMissing = 7,

    /// <summary>A supplied InterfaceImpl catalog stopped non-exactly.</summary>
    ImplementationCatalogNonExact = 8,

    /// <summary>A supplied InterfaceImpl catalog retained contradictory evidence.</summary>
    ImplementationCatalogInvalid = 9,

    /// <summary>More than one supplied InterfaceImpl catalog named the same exact metadata module.</summary>
    DuplicateSourceModule = 10,

    /// <summary>A supplied InterfaceImpl catalog named no module in the exact ancestry portfolio.</summary>
    SourceModuleNotInPortfolio = 11,

    /// <summary>An InterfaceImpl catalog's source ends differed from its module's authority source ends.</summary>
    ImplementationSourceEndsMismatch = 12,
}

/// <summary>Resolves every InterfaceImpl row and derives bounded transitive interface closures.</summary>
/// <remarks>
/// The portfolio is the sole issuer of interface-implementation rows. Same-module TypeDef interfaces select
/// their authority row directly, TypeRef interfaces consume the exact resolution portfolio carried by the ancestry
/// portfolio, and TypeSpec interfaces retain their guarded signature row undecoded. Each closure unions the subject's
/// own edges, the edges of every interface it transitively extends, and the edges of every type on the subject's
/// bounded ancestry chain; the traversal is bounded and its terminal is a complete typed fact.
/// </remarks>
public sealed class MetadataInterfaceImplementationPortfolioIdentity :
    IEquatable<MetadataInterfaceImplementationPortfolioIdentity>
{
    /// <summary>Gets the shared module-count cap.</summary>
    public const int MaximumModuleCount = ExpressionV2ContractLimits.MaximumModuleCount;

    /// <summary>Gets the shared bounded interface-traversal depth cap.</summary>
    public const int MaximumClosureDepth = ExpressionV2ContractLimits.MaximumConstructedAncestryDepth;

    private const string CanonicalDomain = "metadata-v2-interfaceimpl-portfolio";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataInterfaceImplementationPortfolioModuleIdentity> entries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataInterfaceImplementationPortfolioIdentity(
        MetadataInterfaceImplementationPortfolioResultKind resultKind,
        MetadataInterfaceImplementationPortfolioIssue issue,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataInterfaceImplementationPortfolioModuleIdentity> entries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        string? relatedModuleSha256)
    {
        ResultKind = resultKind;
        Issue = issue;
        AncestryPortfolio = ancestryPortfolio;
        this.entries = ExpressionV2ContractEncoding.Copy(entries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedModuleSha256 = relatedModuleSha256;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        writer.WriteInt32(entries.Length);
        foreach (var entry in entries)
        {
            writer.WriteSha256(entry.Sha256, nameof(entries));
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, relatedModuleSha256);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this normalized interface-implementation portfolio is exact, non-exact, or invalid.</summary>
    public MetadataInterfaceImplementationPortfolioResultKind ResultKind { get; }

    /// <summary>Gets the typed portfolio issue, or none for an exact portfolio.</summary>
    public MetadataInterfaceImplementationPortfolioIssue Issue { get; }

    /// <summary>Gets the retained ancestry authority portfolio prerequisite.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets the exact portfolio snapshot digest, or null for an empty portfolio or any stop.</summary>
    public string? SnapshotSha256 =>
        ResultKind == MetadataInterfaceImplementationPortfolioResultKind.Exact
            ? AncestryPortfolio.SnapshotSha256
            : null;

    /// <summary>Gets a defensive module-order copy of exact entries, or an empty array for a stop.</summary>
    public ImmutableArray<MetadataInterfaceImplementationPortfolioModuleIdentity> Entries =>
        ExpressionV2ContractEncoding.Copy(entries);

    /// <summary>Gets the propagated or module-count bound, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied, prerequisite, or cap-plus-one observation count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the issue-related metadata-module digest, otherwise null.</summary>
    public string? RelatedModuleSha256 { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical portfolio bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical portfolio bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one normalized multi-module interface-implementation portfolio.</summary>
    /// <param name="ancestryPortfolio">The exact ancestry portfolio defining modules, authority, and base chains.</param>
    /// <param name="implementationCatalogs">One complete InterfaceImpl catalog for every ancestry-portfolio module.</param>
    /// <returns>An exact complete portfolio or a prefix-free typed stop.</returns>
    public static MetadataInterfaceImplementationPortfolioIdentity Create(
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataInterfaceImplementationTableCatalogIdentity> implementationCatalogs)
    {
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        if (ancestryPortfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.NonExact)
        {
            return Stopped(
                MetadataInterfaceImplementationPortfolioResultKind.NonExact,
                MetadataInterfaceImplementationPortfolioIssue.AncestryPortfolioNonExact,
                ancestryPortfolio,
                observedCount: ancestryPortfolio.ObservedCount,
                relatedModuleSha256: ancestryPortfolio.RelatedModuleSha256);
        }
        if (ancestryPortfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid)
        {
            return Stopped(
                MetadataInterfaceImplementationPortfolioResultKind.Invalid,
                MetadataInterfaceImplementationPortfolioIssue.AncestryPortfolioInvalid,
                ancestryPortfolio,
                observedCount: ancestryPortfolio.ObservedCount,
                relatedModuleSha256: ancestryPortfolio.RelatedModuleSha256);
        }
        if (implementationCatalogs.IsDefault)
        {
            return Stopped(
                MetadataInterfaceImplementationPortfolioResultKind.NonExact,
                MetadataInterfaceImplementationPortfolioIssue.CatalogVectorUninitialized,
                ancestryPortfolio);
        }
        if (implementationCatalogs.Length > MaximumModuleCount)
        {
            return Stopped(
                MetadataInterfaceImplementationPortfolioResultKind.NonExact,
                MetadataInterfaceImplementationPortfolioIssue.ModuleCountBoundReached,
                ancestryPortfolio,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.ModuleCountBoundName,
                    MaximumModuleCount),
                MaximumModuleCount + 1);
        }

        var ancestryEntries = ancestryPortfolio.Entries;
        if (implementationCatalogs.Length < ancestryEntries.Length)
        {
            return Stopped(
                MetadataInterfaceImplementationPortfolioResultKind.NonExact,
                MetadataInterfaceImplementationPortfolioIssue.CatalogSlotsIncomplete,
                ancestryPortfolio,
                observedCount: implementationCatalogs.Length);
        }
        if (implementationCatalogs.Length > ancestryEntries.Length)
        {
            return Stopped(
                MetadataInterfaceImplementationPortfolioResultKind.Invalid,
                MetadataInterfaceImplementationPortfolioIssue.CatalogSlotCountConflict,
                ancestryPortfolio,
                observedCount: implementationCatalogs.Length);
        }
        if (implementationCatalogs.IsEmpty)
        {
            return Exact(ancestryPortfolio, []);
        }
        if (implementationCatalogs.Any(static catalog => catalog is null))
        {
            return Stopped(
                MetadataInterfaceImplementationPortfolioResultKind.NonExact,
                MetadataInterfaceImplementationPortfolioIssue.CatalogMissing,
                ancestryPortfolio,
                observedCount: implementationCatalogs.Length);
        }

        var ordered = implementationCatalogs.ToArray();
        Array.Sort(ordered, static (left, right) =>
        {
            var moduleComparison = left.SourceEnds.SourceModule.CanonicalBytes.AsSpan().SequenceCompareTo(
                right.SourceEnds.SourceModule.CanonicalBytes.AsSpan());
            return moduleComparison != 0
                ? moduleComparison
                : StringComparer.Ordinal.Compare(left.Sha256, right.Sha256);
        });
        foreach (var catalog in ordered)
        {
            if (catalog.ResultKind == MetadataInterfaceImplementationTableResultKind.NonExact)
            {
                return Stopped(
                    MetadataInterfaceImplementationPortfolioResultKind.NonExact,
                    MetadataInterfaceImplementationPortfolioIssue.ImplementationCatalogNonExact,
                    ancestryPortfolio,
                    observedCount: implementationCatalogs.Length,
                    relatedModuleSha256: catalog.SourceEnds.SourceModule.Sha256);
            }
        }
        foreach (var catalog in ordered)
        {
            if (catalog.ResultKind == MetadataInterfaceImplementationTableResultKind.Invalid)
            {
                return Stopped(
                    MetadataInterfaceImplementationPortfolioResultKind.Invalid,
                    MetadataInterfaceImplementationPortfolioIssue.ImplementationCatalogInvalid,
                    ancestryPortfolio,
                    observedCount: implementationCatalogs.Length,
                    relatedModuleSha256: catalog.SourceEnds.SourceModule.Sha256);
            }
        }

        var catalogsByModule = new Dictionary<
            StaticFieldMetadataModuleIdentity,
            MetadataInterfaceImplementationTableCatalogIdentity>();
        foreach (var catalog in ordered)
        {
            var sourceModule = catalog.SourceEnds.SourceModule;
            if (!catalogsByModule.TryAdd(sourceModule, catalog))
            {
                return Stopped(
                    MetadataInterfaceImplementationPortfolioResultKind.Invalid,
                    MetadataInterfaceImplementationPortfolioIssue.DuplicateSourceModule,
                    ancestryPortfolio,
                    observedCount: implementationCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
            var ancestryEntry = ancestryEntries.FirstOrDefault(entry => entry.SourceModule.Equals(sourceModule));
            if (ancestryEntry is null)
            {
                return Stopped(
                    MetadataInterfaceImplementationPortfolioResultKind.Invalid,
                    MetadataInterfaceImplementationPortfolioIssue.SourceModuleNotInPortfolio,
                    ancestryPortfolio,
                    observedCount: implementationCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
            if (!catalog.SourceEnds.Equals(ancestryEntry.ResolutionEntry.ChainEntry.ChainCatalog.SourceEnds))
            {
                return Stopped(
                    MetadataInterfaceImplementationPortfolioResultKind.Invalid,
                    MetadataInterfaceImplementationPortfolioIssue.ImplementationSourceEndsMismatch,
                    ancestryPortfolio,
                    observedCount: implementationCatalogs.Length,
                    relatedModuleSha256: sourceModule.Sha256);
            }
        }

        var edgesByModule = new Dictionary<StaticFieldMetadataModuleIdentity, ModuleEdgeIndex>();
        foreach (var ancestryEntry in ancestryEntries)
        {
            var catalog = catalogsByModule[ancestryEntry.SourceModule];
            var typeDefinitions = ancestryEntry.ResolutionEntry.ChainEntry.ChainCatalog
                .DefinitionAuthority.TypeDefinitions;
            var rows = catalog.Rows;
            var moduleEdges = ImmutableArray.CreateBuilder<MetadataInterfaceImplementationEdgeAuthorityIdentity>(
                rows.Length);
            foreach (var row in rows)
            {
                moduleEdges.Add(ResolveEdge(ancestryEntry.ResolutionEntry, typeDefinitions, row));
            }
            edgesByModule.Add(
                ancestryEntry.SourceModule,
                new ModuleEdgeIndex(catalog, typeDefinitions, moduleEdges.MoveToImmutable()));
        }

        var entries = ImmutableArray.CreateBuilder<MetadataInterfaceImplementationPortfolioModuleIdentity>(
            ancestryEntries.Length);
        foreach (var ancestryEntry in ancestryEntries)
        {
            var index = edgesByModule[ancestryEntry.SourceModule];
            var closures = ImmutableArray.CreateBuilder<MetadataInterfaceImplementationClosureIdentity>(
                index.TypeDefinitions.Length);
            foreach (var typeDefinition in index.TypeDefinitions)
            {
                closures.Add(BuildClosure(
                    ancestryPortfolio,
                    edgesByModule,
                    ancestryEntry.SourceModule,
                    typeDefinition));
            }
            entries.Add(MetadataInterfaceImplementationPortfolioModuleIdentity.Create(
                RowMintCapability,
                index.Catalog,
                ancestryEntry,
                index.Edges,
                closures.MoveToImmutable()));
        }
        return Exact(ancestryPortfolio, entries.MoveToImmutable());
    }

    /// <summary>Looks up one bounded transitive interface closure by module and non-nil TypeDef token.</summary>
    /// <param name="sourceModule">The exact loaded metadata module.</param>
    /// <param name="typeDefinitionToken">The exact TypeDef token within that module.</param>
    /// <returns>The complete closure, or null when the portfolio stopped, the module is absent, or the token was not issued.</returns>
    public MetadataInterfaceImplementationClosureIdentity? ExactImplementedInterfacesOrDefault(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        if (ResultKind != MetadataInterfaceImplementationPortfolioResultKind.Exact)
        {
            return null;
        }
        var entry = entries.FirstOrDefault(candidate => candidate.SourceModule.Equals(sourceModule));
        return entry?.FindClosure(typeDefinitionToken);
    }

    /// <summary>Answers whether one TypeDef implements one named interface over a bounded closure.</summary>
    /// <param name="sourceModule">The exact metadata module defining the subject TypeDef.</param>
    /// <param name="typeDefinitionToken">The exact subject TypeDef token within that module.</param>
    /// <param name="interfaceModule">The exact metadata module defining the candidate interface.</param>
    /// <param name="interfaceTypeDefinitionToken">The candidate interface TypeDef token within that module.</param>
    /// <returns>
    /// A typed three-valued answer; <see cref="MetadataInterfaceImplementationAnswer.No"/> only over a complete
    /// bounded closure, and <see cref="MetadataInterfaceImplementationAnswer.Unprovable"/> for every stop.
    /// </returns>
    public MetadataInterfaceImplementationAnswer Implements(
        StaticFieldMetadataModuleIdentity sourceModule,
        int typeDefinitionToken,
        StaticFieldMetadataModuleIdentity interfaceModule,
        int interfaceTypeDefinitionToken)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(interfaceModule);
        if (ExactImplementedInterfacesOrDefault(sourceModule, typeDefinitionToken) is not { } closure)
        {
            return MetadataInterfaceImplementationAnswer.Unprovable;
        }
        if (closure.ContainsInterface(interfaceModule, interfaceTypeDefinitionToken))
        {
            return MetadataInterfaceImplementationAnswer.Yes;
        }
        return closure.TerminalKind == MetadataInterfaceImplementationClosureTerminalKind.Complete
            ? MetadataInterfaceImplementationAnswer.No
            : MetadataInterfaceImplementationAnswer.Unprovable;
    }

    /// <summary>Tests canonical equality between two interface-implementation portfolios.</summary>
    /// <param name="other">The other portfolio.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataInterfaceImplementationPortfolioIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests interface-implementation portfolio equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a portfolio with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataInterfaceImplementationPortfolioIdentity);

    /// <summary>Computes a deterministic hash code from immutable canonical portfolio content.</summary>
    /// <returns>A hash code for this canonical interface-implementation portfolio.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) => ReferenceEquals(capability, RowMintCapability);

    private static MetadataInterfaceImplementationEdgeAuthorityIdentity ResolveEdge(
        MetadataTypeReferenceResolutionModuleIdentity resolutionEntry,
        ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> typeDefinitions,
        MetadataInterfaceImplementationTableRowIdentity row)
    {
        var token = row.InterfaceMetadataToken;
        if (CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x02))
        {
            var target = typeDefinitions[CanonicalReplayEncoding.MetadataTokenRowId(token) - 1];
            return MetadataInterfaceImplementationEdgeAuthorityIdentity.Create(
                RowMintCapability,
                row,
                MetadataInterfaceImplementationEdgeKind.TypeDefinition,
                resolutionEntry.SourceModule,
                target,
                null,
                null);
        }
        if (CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x01))
        {
            var resolution = resolutionEntry.FindResolution(token)!;
            return resolution.Disposition == MetadataTypeReferenceResolutionDispositionKind.Resolved
                ? MetadataInterfaceImplementationEdgeAuthorityIdentity.Create(
                    RowMintCapability,
                    row,
                    MetadataInterfaceImplementationEdgeKind.TypeReference,
                    resolution.TargetModule,
                    resolution.TargetTypeDefinition,
                    resolution,
                    null)
                : MetadataInterfaceImplementationEdgeAuthorityIdentity.Create(
                    RowMintCapability,
                    row,
                    MetadataInterfaceImplementationEdgeKind.TypeReferenceUnresolved,
                    null,
                    null,
                    resolution,
                    null);
        }
        var genericInterfaceRow = resolutionEntry.ReferenceTables.TypeSpecifications.FindRow(token)!;
        return MetadataInterfaceImplementationEdgeAuthorityIdentity.Create(
            RowMintCapability,
            row,
            MetadataInterfaceImplementationEdgeKind.TypeSpecification,
            null,
            null,
            null,
            genericInterfaceRow);
    }

    private static MetadataInterfaceImplementationClosureIdentity BuildClosure(
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        Dictionary<StaticFieldMetadataModuleIdentity, ModuleEdgeIndex> edgesByModule,
        StaticFieldMetadataModuleIdentity subjectModule,
        MetadataTypeDefinitionAuthorityIdentity subject)
    {
        var walk = new ClosureWalk(edgesByModule);
        var subjectKey = (subjectModule.Sha256, subject.TypeDefinitionToken);
        walk.Expand(subjectModule, subject, [subjectKey], 0);

        var chain = ancestryPortfolio.ExactAncestryChainOrDefault(subjectModule, subject.TypeDefinitionToken)!;
        foreach (var baseEdge in chain.Edges)
        {
            var baseModule = baseEdge.SourceModule;
            var baseType = baseEdge.Owner;
            walk.Expand(
                baseModule,
                baseType,
                [subjectKey, (baseModule.Sha256, baseType.TypeDefinitionToken)],
                0);
        }
        switch (chain.TerminalKind)
        {
            case MetadataAncestryChainTerminalKind.UnresolvedReferenceReached:
                walk.Note(
                    MetadataInterfaceImplementationClosureTerminalKind.UnresolvedReferenceReached,
                    null,
                    chain.RelatedMetadataToken);
                break;
            case MetadataAncestryChainTerminalKind.GenericBaseReached:
                walk.Note(
                    MetadataInterfaceImplementationClosureTerminalKind.GenericInterfaceReached,
                    null,
                    chain.RelatedMetadataToken);
                break;
            case MetadataAncestryChainTerminalKind.CycleDetected:
                walk.Note(
                    MetadataInterfaceImplementationClosureTerminalKind.CycleDetected,
                    null,
                    chain.RelatedMetadataToken);
                break;
            case MetadataAncestryChainTerminalKind.DepthBoundReached:
                walk.Note(
                    MetadataInterfaceImplementationClosureTerminalKind.DepthBoundReached,
                    chain.ReachedBound,
                    chain.RelatedMetadataToken);
                break;
            default:
                break;
        }

        return MetadataInterfaceImplementationClosureIdentity.Create(
            RowMintCapability,
            subjectModule,
            subject,
            walk.DiscoveredEdges,
            walk.TerminalKind,
            walk.ReachedBound,
            walk.RelatedMetadataToken);
    }

    private static MetadataInterfaceImplementationPortfolioIdentity Exact(
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataInterfaceImplementationPortfolioModuleIdentity> entries) =>
        new(
            MetadataInterfaceImplementationPortfolioResultKind.Exact,
            MetadataInterfaceImplementationPortfolioIssue.None,
            ancestryPortfolio,
            entries,
            null,
            0,
            null);

    private static MetadataInterfaceImplementationPortfolioIdentity Stopped(
        MetadataInterfaceImplementationPortfolioResultKind resultKind,
        MetadataInterfaceImplementationPortfolioIssue issue,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        EvaluationDeterministicBound? reachedBound = null,
        int observedCount = 0,
        string? relatedModuleSha256 = null) =>
        new(
            resultKind,
            issue,
            ancestryPortfolio,
            [],
            reachedBound,
            observedCount,
            relatedModuleSha256);

    private sealed class ModuleEdgeIndex
    {
        private readonly Dictionary<int, List<MetadataInterfaceImplementationEdgeAuthorityIdentity>> byOwner = [];

        internal ModuleEdgeIndex(
            MetadataInterfaceImplementationTableCatalogIdentity catalog,
            ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> typeDefinitions,
            ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> edges)
        {
            Catalog = catalog;
            TypeDefinitions = typeDefinitions;
            Edges = edges;
            foreach (var edge in edges)
            {
                var owner = edge.ImplementationRow.ImplementingTypeDefinitionToken;
                if (!byOwner.TryGetValue(owner, out var group))
                {
                    group = [];
                    byOwner.Add(owner, group);
                }
                group.Add(edge);
            }
        }

        internal MetadataInterfaceImplementationTableCatalogIdentity Catalog { get; }

        internal ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> TypeDefinitions { get; }

        internal ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> Edges { get; }

        internal IReadOnlyList<MetadataInterfaceImplementationEdgeAuthorityIdentity> EdgesForOwner(
            int typeDefinitionToken) =>
            byOwner.TryGetValue(typeDefinitionToken, out var group) ? group : [];
    }

    private sealed class ClosureWalk(
        Dictionary<StaticFieldMetadataModuleIdentity, ModuleEdgeIndex> edgesByModule)
    {
        private readonly ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity>.Builder discovered =
            ImmutableArray.CreateBuilder<MetadataInterfaceImplementationEdgeAuthorityIdentity>();
        private readonly HashSet<(string, int)> discoveredKeys = [];

        internal MetadataInterfaceImplementationClosureTerminalKind TerminalKind { get; private set; } =
            MetadataInterfaceImplementationClosureTerminalKind.Complete;

        internal EvaluationDeterministicBound? ReachedBound { get; private set; }

        internal int? RelatedMetadataToken { get; private set; }

        internal ImmutableArray<MetadataInterfaceImplementationEdgeAuthorityIdentity> DiscoveredEdges =>
            discovered.ToImmutable();

        internal void Note(
            MetadataInterfaceImplementationClosureTerminalKind terminalKind,
            EvaluationDeterministicBound? reachedBound,
            int? relatedMetadataToken)
        {
            if (TerminalKind != MetadataInterfaceImplementationClosureTerminalKind.Complete)
            {
                return;
            }
            TerminalKind = terminalKind;
            ReachedBound = reachedBound;
            RelatedMetadataToken = relatedMetadataToken;
        }

        internal void Expand(
            StaticFieldMetadataModuleIdentity module,
            MetadataTypeDefinitionAuthorityIdentity type,
            HashSet<(string, int)> path,
            int depth)
        {
            if (!edgesByModule.TryGetValue(module, out var index))
            {
                return;
            }
            foreach (var edge in index.EdgesForOwner(type.TypeDefinitionToken))
            {
                switch (edge.Kind)
                {
                    case MetadataInterfaceImplementationEdgeKind.TypeReferenceUnresolved:
                        Note(
                            MetadataInterfaceImplementationClosureTerminalKind.UnresolvedReferenceReached,
                            null,
                            edge.InterfaceMetadataToken);
                        continue;
                    case MetadataInterfaceImplementationEdgeKind.TypeSpecification:
                        Note(
                            MetadataInterfaceImplementationClosureTerminalKind.GenericInterfaceReached,
                            null,
                            edge.InterfaceMetadataToken);
                        continue;
                    default:
                        break;
                }

                var targetModule = edge.TargetModule!;
                var target = edge.TargetTypeDefinition!;
                var key = (targetModule.Sha256, target.TypeDefinitionToken);
                if (path.Contains(key))
                {
                    Note(
                        MetadataInterfaceImplementationClosureTerminalKind.CycleDetected,
                        null,
                        target.TypeDefinitionToken);
                    continue;
                }
                if (discoveredKeys.Contains(key))
                {
                    continue;
                }
                if (depth + 1 > MaximumClosureDepth)
                {
                    Note(
                        MetadataInterfaceImplementationClosureTerminalKind.DepthBoundReached,
                        new EvaluationDeterministicBound(
                            ExpressionV2ContractLimits.ConstructedAncestryDepthBoundName,
                            MaximumClosureDepth),
                        target.TypeDefinitionToken);
                    continue;
                }

                discoveredKeys.Add(key);
                discovered.Add(edge);
                path.Add(key);
                Expand(targetModule, target, path, depth + 1);
                path.Remove(key);
            }
        }
    }
}
