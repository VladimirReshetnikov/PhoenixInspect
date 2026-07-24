using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one complete physical GenericParam-table draft proof.</summary>
/// <remarks>The draft result is prefix-free unless every source row is coherently represented.</remarks>
public enum MetadataGenericParameterPhysicalTableResultKind
{
    /// <summary>The complete physical table and every owner group are exact.</summary>
    Exact = 1,

    /// <summary>Complete acquisition was unavailable or crossed the admitted global row bound.</summary>
    NonExact = 2,

    /// <summary>The supplied complete-table claim contradicted source ends or physical row invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one physical GenericParam-table draft proof.</summary>
/// <remarks>Each non-exact or invalid draft result exposes no row or owner prefix.</remarks>
public enum MetadataGenericParameterPhysicalTableIssue
{
    /// <summary>No issue applies to an exact complete table.</summary>
    None = 0,

    /// <summary>The exact GenericParam source end crossed the admitted complete row count.</summary>
    TableRowBoundReached = 1,

    /// <summary>The supplied observations were unavailable or stopped before the exact source end.</summary>
    TableIncomplete = 2,

    /// <summary>The supplied observation count exceeded the exact source end.</summary>
    TableRowCountConflict = 3,

    /// <summary>At least one observation was absent from the claimed complete physical table.</summary>
    PhysicalRowMissing = 4,

    /// <summary>The GenericParam tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 5,

    /// <summary>At least one physical observation belonged to another metadata module.</summary>
    SourceModuleMismatch = 6,

    /// <summary>An owner token belonged to a table other than TypeDef or MethodDef.</summary>
    OwnerTokenKindInvalid = 7,

    /// <summary>An owner token was nil or lay beyond its exact TypeDef or MethodDef source end.</summary>
    OwnerTokenOutOfRange = 8,

    /// <summary>The raw flags used an undefined variance combination or bits outside the admitted known-bit mask.</summary>
    FlagsInvalid = 9,

    /// <summary>Two physical rows selected the same exact owner and Number.</summary>
    DuplicateOwnerNumber = 10,

    /// <summary>One exact owner's Number values did not cover every position from zero without gaps.</summary>
    OwnerNumberCoverageInvalid = 11,

    /// <summary>Two physical rows selected the same exact owner and decoded name.</summary>
    DuplicateOwnerName = 12,
}

/// <summary>Records the physical Owner/Number ordering profile of an exact GenericParam-table draft proof.</summary>
/// <remarks>The draft profile records noncanonical order without rejecting otherwise coherent complete evidence.</remarks>
public enum MetadataGenericParameterPhysicalOrderProfile
{
    /// <summary>No ordering profile is available because the table proof is not exact.</summary>
    Unavailable = 0,

    /// <summary>Physical rows are ordered by the TypeOrMethodDef coded Owner and then Number.</summary>
    EcmaOwnerThenNumber = 1,

    /// <summary>Every owner group is coherent, but physical rows do not follow Owner/Number order.</summary>
    Unsorted = 2,
}

/// <summary>Freezes one exact source-module, owner-kind, and owner-token tuple.</summary>
/// <remarks>
/// This sealed draft identity contains no legacy TypeDef or MethodDef object. It can be minted only while a complete
/// physical GenericParam catalog validates the owner token against exact source ends.
/// </remarks>
public sealed class MetadataGenericParameterOwnerTokenIdentity :
    IEquatable<MetadataGenericParameterOwnerTokenIdentity>
{
    private const string CanonicalDomain = "metadata-v2-genericparam-owner-token";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterOwnerTokenIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataGenericParameterOwnerKind kind,
        int ownerMetadataToken)
    {
        SourceModule = sourceModule;
        Kind = kind;
        OwnerMetadataToken = ownerMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)kind);
        writer.WriteInt32(ownerMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the owner token.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets whether the exact owner token denotes a TypeDef or MethodDef row.</summary>
    public MetadataGenericParameterOwnerKind Kind { get; }

    /// <summary>Gets the exact non-nil TypeDef or MethodDef owner token.</summary>
    public int OwnerMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two exact owner-token draft identities.</summary>
    /// <param name="other">The other draft identity.</param>
    /// <returns><see langword="true"/> only for byte-identical source, kind, and token content.</returns>
    public bool Equals(MetadataGenericParameterOwnerTokenIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests exact owner-token draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an identity with byte-identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterOwnerTokenIdentity);

    /// <summary>Computes a hash code from the immutable exact owner-token draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataGenericParameterOwnerTokenIdentity Create(
        object mintCapability,
        StaticFieldMetadataModuleIdentity sourceModule,
        MetadataGenericParameterOwnerKind kind,
        int ownerMetadataToken)
    {
        if (!MetadataGenericParameterPhysicalTableCatalogIdentity.OwnsOwnerMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An exact GenericParam owner token requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceModule);
        return new MetadataGenericParameterOwnerTokenIdentity(sourceModule, kind, ownerMetadataToken);
    }
}

/// <summary>Freezes only the physical columns observed from one GenericParam row.</summary>
/// <remarks>
/// This sealed draft observation carries no legacy owner object, derived owner group, declared arity, or argument
/// binding. The raw Number and flags are retained exactly for complete-table validation and later interpretation.
/// </remarks>
public sealed class MetadataGenericParameterRowObservationIdentity :
    IEquatable<MetadataGenericParameterRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-genericparam-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int genericParameterToken,
        int number,
        int flags,
        int ownerMetadataToken,
        string name)
    {
        MetadataModule = metadataModule;
        GenericParameterToken = genericParameterToken;
        Number = number;
        Flags = flags;
        OwnerMetadataToken = ownerMetadataToken;
        Name = name;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(genericParameterToken);
        writer.WriteInt32(number);
        writer.WriteInt32(flags);
        writer.WriteInt32(ownerMetadataToken);
        writer.WriteString(name);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the observed physical row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact non-nil GenericParam token identifying the physical row.</summary>
    public int GenericParameterToken { get; }

    /// <summary>Gets the raw unsigned 16-bit Number column.</summary>
    public int Number { get; }

    /// <summary>Gets the raw unsigned 16-bit flags column without semantic reinterpretation.</summary>
    public int Flags { get; }

    /// <summary>Gets the raw TypeDef-or-MethodDef owner token.</summary>
    public int OwnerMetadataToken { get; }

    /// <summary>Gets the decoded name, including empty when heap-index provenance cannot distinguish its origin.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one physical-column-only GenericParam row draft observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the row.</param>
    /// <param name="genericParameterToken">The exact non-nil GenericParam token.</param>
    /// <param name="number">The raw unsigned 16-bit Number column.</param>
    /// <param name="flags">The raw unsigned 16-bit flags column.</param>
    /// <param name="ownerMetadataToken">The raw decoded owner token retained for catalog validation.</param>
    /// <param name="name">The decoded name; empty is retained without inferring the original heap index.</param>
    /// <returns>A sealed immutable physical-row draft observation with no derived owner fact.</returns>
    public static MetadataGenericParameterRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int genericParameterToken,
        int number,
        int flags,
        int ownerMetadataToken,
        string name)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(
            genericParameterToken,
            0x2A,
            nameof(genericParameterToken));
        if (number is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }
        if (flags is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }
        ExpressionV2ContractEncoding.RequireText(
            name,
            nameof(name),
            StaticFieldMetadataTextLimits.MaximumTextLength,
            allowEmpty: true);

        return new MetadataGenericParameterRowObservationIdentity(
            metadataModule,
            genericParameterToken,
            number,
            flags,
            ownerMetadataToken,
            name);
    }

    /// <summary>Tests canonical equality between two physical GenericParam draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical physical-column draft content.</returns>
    public bool Equals(MetadataGenericParameterRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical GenericParam draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterRowObservationIdentity);

    /// <summary>Computes a hash code from the immutable physical-row draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one GenericParam row after complete source, owner, and group validation.</summary>
/// <remarks>This sealed draft identity can be minted only by an exact physical-table catalog.</remarks>
public sealed class MetadataGenericParameterTableRowIdentity :
    IEquatable<MetadataGenericParameterTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-genericparam-table-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterTableRowIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterRowObservationIdentity observation,
        MetadataGenericParameterOwnerTokenIdentity owner)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        Owner = owner;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(observation.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(owner.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source ends against which this row was validated.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical-column-only observation.</summary>
    public MetadataGenericParameterRowObservationIdentity Observation { get; }

    /// <summary>Gets the exact source, kind, and owner-token tuple derived for this row.</summary>
    public MetadataGenericParameterOwnerTokenIdentity Owner { get; }

    /// <summary>Gets the exact GenericParam token forwarded from the physical observation.</summary>
    public int GenericParameterToken => Observation.GenericParameterToken;

    /// <summary>Gets the exact validated zero-based owner position.</summary>
    public int Number => Observation.Number;

    /// <summary>Gets the unchanged raw unsigned 16-bit flags.</summary>
    public int Flags => Observation.Flags;

    /// <summary>Gets the exact decoded name, including empty.</summary>
    public string Name => Observation.Name;

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two exact GenericParam table-row draft identities.</summary>
    /// <param name="other">The other exact draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests exact GenericParam table-row draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterTableRowIdentity);

    /// <summary>Computes a hash code from immutable exact GenericParam table-row draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataGenericParameterTableRowIdentity Create(
        object mintCapability,
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterRowObservationIdentity observation,
        MetadataGenericParameterOwnerTokenIdentity owner)
    {
        if (!MetadataGenericParameterPhysicalTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An exact GenericParam table row requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(owner);
        return new MetadataGenericParameterTableRowIdentity(sourceEnds, observation, owner);
    }
}

/// <summary>Freezes a complete source-anchored physical GenericParam table and its exact owner groups.</summary>
/// <remarks>
/// This sealed draft catalog validates complete physical RID coverage, groups all rows by exact owner tuple, and
/// proves Number coverage independently of physical adjacency. Unsorted coherent tables remain exact and record that
/// profile. Global table size is bounded; no per-owner semantic arity cap is applied at this physical layer.
/// </remarks>
public sealed class MetadataGenericParameterPhysicalTableCatalogIdentity :
    IEquatable<MetadataGenericParameterPhysicalTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-genericparam-physical-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object OwnerMintCapability = new();
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataGenericParameterTableRowIdentity> rows;
    private readonly ImmutableArray<MetadataGenericParameterOwnerTokenIdentity> owners;
    private readonly ImmutableDictionary<OwnerTuple, ImmutableArray<MetadataGenericParameterTableRowIdentity>> ownerRows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataGenericParameterPhysicalTableCatalogIdentity(
        MetadataGenericParameterPhysicalTableResultKind resultKind,
        MetadataGenericParameterPhysicalTableIssue issue,
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterPhysicalOrderProfile orderProfile,
        ImmutableArray<MetadataGenericParameterTableRowIdentity> rows,
        ImmutableArray<MetadataGenericParameterOwnerTokenIdentity> owners,
        ImmutableDictionary<OwnerTuple, ImmutableArray<MetadataGenericParameterTableRowIdentity>> ownerRows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        OrderProfile = orderProfile;
        this.rows = rows;
        this.owners = owners;
        this.ownerRows = ownerRows;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)orderProfile);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, owners, static owner => owner.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete physical-table draft proof is exact, non-exact, or invalid.</summary>
    public MetadataGenericParameterPhysicalTableResultKind ResultKind { get; }

    /// <summary>Gets the typed physical-table draft issue, or none for an exact result.</summary>
    public MetadataGenericParameterPhysicalTableIssue Issue { get; }

    /// <summary>Gets the exact metadata source ends governing the complete GenericParam table.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the recorded physical Owner/Number ordering profile.</summary>
    public MetadataGenericParameterPhysicalOrderProfile OrderProfile { get; }

    /// <summary>Gets a defensive copy of exact rows in physical RID order, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataGenericParameterTableRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets a defensive copy of exact owner tuples in coded-owner order, or an empty array otherwise.</summary>
    public ImmutableArray<MetadataGenericParameterOwnerTokenIdentity> Owners =>
        ExpressionV2ContractEncoding.Copy(owners);

    /// <summary>Gets the global row bound only after a cap-plus-one source observation.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied or cap-plus-one row count, otherwise zero.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete source-anchored physical GenericParam-table draft proof.</summary>
    /// <param name="sourceEnds">The exact source ends for the metadata module.</param>
    /// <param name="observations">
    /// Every physical GenericParam row in RID order; default denotes unavailable acquisition for a non-empty table.
    /// </param>
    /// <returns>An exact catalog, a prefix-free non-exact stop, or a factless invalid draft result.</returns>
    public static MetadataGenericParameterPhysicalTableCatalogIdentity Create(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataGenericParameterRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var sourceCount = sourceEnds.GenericParameterRowCount;
        if (sourceCount > StaticFieldV2Limits.MaximumGenericParameterRowCount)
        {
            return NonExact(
                sourceEnds,
                MetadataGenericParameterPhysicalTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.GenericParameterRowCountBoundName,
                    StaticFieldV2Limits.MaximumGenericParameterRowCount),
                StaticFieldV2Limits.MaximumGenericParameterRowCount + 1);
        }

        var observedCount = observations.IsDefault ? 0 : observations.Length;
        if (sourceCount > 0 && observedCount < sourceCount)
        {
            return NonExact(
                sourceEnds,
                MetadataGenericParameterPhysicalTableIssue.TableIncomplete,
                null,
                observedCount);
        }
        if (observedCount > sourceCount)
        {
            return Invalid(
                sourceEnds,
                MetadataGenericParameterPhysicalTableIssue.TableRowCountConflict,
                observedCount);
        }
        if (sourceCount == 0)
        {
            return ExactEmpty(sourceEnds);
        }

        var copied = ExpressionV2ContractEncoding.Copy(observations);
        var validated = new ValidatedObservation[copied.Length];
        var groups = new Dictionary<OwnerTuple, List<ValidatedObservation>>();
        var orderProfile = MetadataGenericParameterPhysicalOrderProfile.EcmaOwnerThenNumber;
        long previousOwnerKey = -1;
        var previousNumber = -1;
        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation is null)
            {
                return Invalid(
                    sourceEnds,
                    MetadataGenericParameterPhysicalTableIssue.PhysicalRowMissing,
                    copied.Length);
            }
            if (observation.GenericParameterToken != (0x2A00_0000 | checked(index + 1)))
            {
                return Invalid(
                    sourceEnds,
                    MetadataGenericParameterPhysicalTableIssue.PhysicalOrderInvalid,
                    copied.Length);
            }
            if (!observation.MetadataModule.Equals(sourceEnds.SourceModule))
            {
                return Invalid(
                    sourceEnds,
                    MetadataGenericParameterPhysicalTableIssue.SourceModuleMismatch,
                    copied.Length);
            }
            if (!TryClassifyOwner(observation.OwnerMetadataToken, out var ownerKind))
            {
                return Invalid(
                    sourceEnds,
                    MetadataGenericParameterPhysicalTableIssue.OwnerTokenKindInvalid,
                    copied.Length);
            }
            if (!OwnerTokenIsInRange(sourceEnds, ownerKind, observation.OwnerMetadataToken))
            {
                return Invalid(
                    sourceEnds,
                    MetadataGenericParameterPhysicalTableIssue.OwnerTokenOutOfRange,
                    copied.Length);
            }
            if ((observation.Flags & ~0x3F) != 0 || (observation.Flags & 0x03) == 0x03)
            {
                return Invalid(
                    sourceEnds,
                    MetadataGenericParameterPhysicalTableIssue.FlagsInvalid,
                    copied.Length);
            }

            var ownerTuple = new OwnerTuple(sourceEnds.SourceModule, ownerKind, observation.OwnerMetadataToken);
            var ownerKey = TypeOrMethodDefinitionCodedIndex(ownerKind, observation.OwnerMetadataToken);
            if (ownerKey < previousOwnerKey ||
                ownerKey == previousOwnerKey && observation.Number < previousNumber)
            {
                orderProfile = MetadataGenericParameterPhysicalOrderProfile.Unsorted;
            }
            previousOwnerKey = ownerKey;
            previousNumber = observation.Number;

            var value = new ValidatedObservation(observation, ownerTuple, ownerKey);
            validated[index] = value;
            if (!groups.TryGetValue(ownerTuple, out var group))
            {
                group = [];
                groups.Add(ownerTuple, group);
            }
            group.Add(value);
        }

        var orderedGroups = groups
            .OrderBy(static pair => pair.Value[0].OwnerCodedIndex)
            .ToArray();
        foreach (var groupPair in orderedGroups)
        {
            var group = groupPair.Value;
            var numbers = new HashSet<int>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in group)
            {
                if (!numbers.Add(row.Observation.Number))
                {
                    return Invalid(
                        sourceEnds,
                        MetadataGenericParameterPhysicalTableIssue.DuplicateOwnerNumber,
                        copied.Length);
                }
                if (!names.Add(row.Observation.Name))
                {
                    return Invalid(
                        sourceEnds,
                        MetadataGenericParameterPhysicalTableIssue.DuplicateOwnerName,
                        copied.Length);
                }
            }

            var orderedNumbers = numbers.Order().ToArray();
            for (var position = 0; position < orderedNumbers.Length; position++)
            {
                if (orderedNumbers[position] != position)
                {
                    return Invalid(
                        sourceEnds,
                        MetadataGenericParameterPhysicalTableIssue.OwnerNumberCoverageInvalid,
                        copied.Length);
                }
            }
        }

        var ownerBuilder = ImmutableArray.CreateBuilder<MetadataGenericParameterOwnerTokenIdentity>(orderedGroups.Length);
        var ownerIdentities = new Dictionary<OwnerTuple, MetadataGenericParameterOwnerTokenIdentity>();
        foreach (var group in orderedGroups)
        {
            var owner = MetadataGenericParameterOwnerTokenIdentity.Create(
                OwnerMintCapability,
                group.Key.SourceModule,
                group.Key.Kind,
                group.Key.OwnerMetadataToken);
            ownerBuilder.Add(owner);
            ownerIdentities.Add(group.Key, owner);
        }

        var rowBuilder = ImmutableArray.CreateBuilder<MetadataGenericParameterTableRowIdentity>(validated.Length);
        var groupedRows = new Dictionary<OwnerTuple, MetadataGenericParameterTableRowIdentity[]>();
        foreach (var group in groups)
        {
            groupedRows.Add(group.Key, new MetadataGenericParameterTableRowIdentity[group.Value.Count]);
        }
        foreach (var value in validated)
        {
            var row = MetadataGenericParameterTableRowIdentity.Create(
                RowMintCapability,
                sourceEnds,
                value.Observation,
                ownerIdentities[value.Owner]);
            rowBuilder.Add(row);
            groupedRows[value.Owner][row.Number] = row;
        }
        var exactRows = rowBuilder.MoveToImmutable();

        var ownerRowsBuilder = ImmutableDictionary.CreateBuilder<
            OwnerTuple,
            ImmutableArray<MetadataGenericParameterTableRowIdentity>>();
        foreach (var group in orderedGroups)
        {
            ownerRowsBuilder.Add(
                group.Key,
                ImmutableArray.Create(groupedRows[group.Key]));
        }

        return new MetadataGenericParameterPhysicalTableCatalogIdentity(
            MetadataGenericParameterPhysicalTableResultKind.Exact,
            MetadataGenericParameterPhysicalTableIssue.None,
            sourceEnds,
            orderProfile,
            exactRows,
            ownerBuilder.MoveToImmutable(),
            ownerRowsBuilder.ToImmutable(),
            null,
            0);
    }

    /// <summary>Gets exact rows for one valid owner token in Number order, or an empty initialized array.</summary>
    /// <param name="kind">Whether the owner token denotes TypeDef or MethodDef.</param>
    /// <param name="ownerMetadataToken">The non-nil owner token within this catalog's exact source end.</param>
    /// <returns>
    /// A defensive row-array copy for an owner present in an exact draft catalog; otherwise an initialized empty array.
    /// </returns>
    public ImmutableArray<MetadataGenericParameterTableRowIdentity> RowsForOwnerOrEmpty(
        MetadataGenericParameterOwnerKind kind,
        int ownerMetadataToken)
    {
        if (kind is not MetadataGenericParameterOwnerKind.TypeDefinition and
            not MetadataGenericParameterOwnerKind.MethodDefinition)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (!OwnerTokenHasKind(kind, ownerMetadataToken))
        {
            throw new ArgumentOutOfRangeException(nameof(ownerMetadataToken));
        }
        if (!OwnerTokenIsInRange(SourceEnds, kind, ownerMetadataToken))
        {
            throw new ArgumentOutOfRangeException(nameof(ownerMetadataToken));
        }
        if (ResultKind != MetadataGenericParameterPhysicalTableResultKind.Exact)
        {
            return ImmutableArray<MetadataGenericParameterTableRowIdentity>.Empty;
        }

        var key = new OwnerTuple(SourceEnds.SourceModule, kind, ownerMetadataToken);
        return ownerRows.TryGetValue(key, out var value)
            ? ExpressionV2ContractEncoding.Copy(value)
            : ImmutableArray<MetadataGenericParameterTableRowIdentity>.Empty;
    }

    /// <summary>Tests canonical equality between two physical GenericParam-table draft proofs.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataGenericParameterPhysicalTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical GenericParam-table draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataGenericParameterPhysicalTableCatalogIdentity);

    /// <summary>Computes a hash code from immutable physical GenericParam-table draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsOwnerMintCapability(object? capability) =>
        ReferenceEquals(capability, OwnerMintCapability);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    private static MetadataGenericParameterPhysicalTableCatalogIdentity ExactEmpty(
        MetadataSourceEndIdentity sourceEnds) =>
        new(
            MetadataGenericParameterPhysicalTableResultKind.Exact,
            MetadataGenericParameterPhysicalTableIssue.None,
            sourceEnds,
            MetadataGenericParameterPhysicalOrderProfile.EcmaOwnerThenNumber,
            ImmutableArray<MetadataGenericParameterTableRowIdentity>.Empty,
            ImmutableArray<MetadataGenericParameterOwnerTokenIdentity>.Empty,
            ImmutableDictionary<OwnerTuple, ImmutableArray<MetadataGenericParameterTableRowIdentity>>.Empty,
            null,
            0);

    private static MetadataGenericParameterPhysicalTableCatalogIdentity NonExact(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterPhysicalTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            MetadataGenericParameterPhysicalTableResultKind.NonExact,
            issue,
            sourceEnds,
            MetadataGenericParameterPhysicalOrderProfile.Unavailable,
            ImmutableArray<MetadataGenericParameterTableRowIdentity>.Empty,
            ImmutableArray<MetadataGenericParameterOwnerTokenIdentity>.Empty,
            ImmutableDictionary<OwnerTuple, ImmutableArray<MetadataGenericParameterTableRowIdentity>>.Empty,
            reachedBound,
            observedCount);

    private static MetadataGenericParameterPhysicalTableCatalogIdentity Invalid(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterPhysicalTableIssue issue,
        int observedCount) =>
        new(
            MetadataGenericParameterPhysicalTableResultKind.Invalid,
            issue,
            sourceEnds,
            MetadataGenericParameterPhysicalOrderProfile.Unavailable,
            ImmutableArray<MetadataGenericParameterTableRowIdentity>.Empty,
            ImmutableArray<MetadataGenericParameterOwnerTokenIdentity>.Empty,
            ImmutableDictionary<OwnerTuple, ImmutableArray<MetadataGenericParameterTableRowIdentity>>.Empty,
            null,
            observedCount);

    private static bool TryClassifyOwner(int ownerMetadataToken, out MetadataGenericParameterOwnerKind kind)
    {
        var table = ownerMetadataToken & unchecked((int)0xFF00_0000);
        if (table == 0x0200_0000)
        {
            kind = MetadataGenericParameterOwnerKind.TypeDefinition;
            return true;
        }
        if (table == 0x0600_0000)
        {
            kind = MetadataGenericParameterOwnerKind.MethodDefinition;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool OwnerTokenHasKind(MetadataGenericParameterOwnerKind kind, int ownerMetadataToken) =>
        kind switch
        {
            MetadataGenericParameterOwnerKind.TypeDefinition =>
                CanonicalReplayEncoding.IsMetadataTokenForTable(ownerMetadataToken, 0x02),
            MetadataGenericParameterOwnerKind.MethodDefinition =>
                CanonicalReplayEncoding.IsMetadataTokenForTable(ownerMetadataToken, 0x06),
            _ => false,
        };

    private static bool OwnerTokenIsInRange(
        MetadataSourceEndIdentity sourceEnds,
        MetadataGenericParameterOwnerKind kind,
        int ownerMetadataToken)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(ownerMetadataToken);
        return rowId > 0 && kind switch
        {
            MetadataGenericParameterOwnerKind.TypeDefinition => rowId <= sourceEnds.TypeDefinitionRowCount,
            MetadataGenericParameterOwnerKind.MethodDefinition => rowId <= sourceEnds.MethodDefinitionRowCount,
            _ => false,
        };
    }

    private static long TypeOrMethodDefinitionCodedIndex(
        MetadataGenericParameterOwnerKind kind,
        int ownerMetadataToken)
    {
        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(ownerMetadataToken);
        var tag = kind == MetadataGenericParameterOwnerKind.MethodDefinition ? 1 : 0;
        return ((long)(uint)rowId << 1) | (uint)tag;
    }

    private readonly record struct OwnerTuple(
        StaticFieldMetadataModuleIdentity SourceModule,
        MetadataGenericParameterOwnerKind Kind,
        int OwnerMetadataToken);

    private readonly record struct ValidatedObservation(
        MetadataGenericParameterRowObservationIdentity Observation,
        OwnerTuple Owner,
        long OwnerCodedIndex);
}
