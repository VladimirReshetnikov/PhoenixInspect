using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one physical member-pointer table represented by a draft row observation.</summary>
/// <remarks>The draft discriminator follows the metadata table numbers and is not a compatibility commitment.</remarks>
public enum MetadataMemberPointerTableKind
{
    /// <summary>The FieldPtr table whose rows redirect TypeDef.FieldList entries to FieldDef rows.</summary>
    Field = 3,

    /// <summary>The MethodPtr table whose rows redirect TypeDef.MethodList entries to MethodDef rows.</summary>
    Method = 5,
}

/// <summary>Classifies one complete source-anchored member-pointer catalog draft proof.</summary>
/// <remarks>The draft result is prefix-free whenever complete exact mapping evidence is unavailable.</remarks>
public enum MetadataMemberPointerTableResultKind
{
    /// <summary>Both pointer domains are exact, including direct domains represented by absent pointer tables.</summary>
    Exact = 1,

    /// <summary>At least one present pointer table was unavailable, incomplete, or crossed its admitted row bound.</summary>
    NonExact = 2,

    /// <summary>The supplied complete-table claim contradicted source ends or pointer-row invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one complete member-pointer catalog draft proof.</summary>
/// <remarks>Every non-exact or invalid draft result retains no pointer-row prefix.</remarks>
public enum MetadataMemberPointerTableIssue
{
    /// <summary>No issue applies to an exact complete catalog.</summary>
    None = 0,

    /// <summary>The exact FieldPtr source end crossed the admitted complete row count.</summary>
    FieldPointerRowBoundReached = 1,

    /// <summary>The exact MethodPtr source end crossed the admitted complete row count.</summary>
    MethodPointerRowBoundReached = 2,

    /// <summary>The FieldPtr observations were unavailable or stopped before the exact source end.</summary>
    FieldPointerTableIncomplete = 3,

    /// <summary>The MethodPtr observations were unavailable or stopped before the exact source end.</summary>
    MethodPointerTableIncomplete = 4,

    /// <summary>The supplied FieldPtr observation count exceeded the exact source end.</summary>
    FieldPointerRowCountConflict = 5,

    /// <summary>The supplied MethodPtr observation count exceeded the exact source end.</summary>
    MethodPointerRowCountConflict = 6,

    /// <summary>A present FieldPtr table did not have one row for every FieldDef row.</summary>
    FieldPointerDefinitionCoverageConflict = 7,

    /// <summary>A present MethodPtr table did not have one row for every MethodDef row.</summary>
    MethodPointerDefinitionCoverageConflict = 8,

    /// <summary>The FieldPtr observations did not form the exact physical RID sequence.</summary>
    FieldPointerPhysicalOrderInvalid = 9,

    /// <summary>The MethodPtr observations did not form the exact physical RID sequence.</summary>
    MethodPointerPhysicalOrderInvalid = 10,

    /// <summary>A FieldPtr observation belonged to another metadata module.</summary>
    FieldPointerSourceModuleMismatch = 11,

    /// <summary>A MethodPtr observation belonged to another metadata module.</summary>
    MethodPointerSourceModuleMismatch = 12,

    /// <summary>A FieldPtr target used a metadata token table other than FieldDef.</summary>
    FieldPointerTargetTableInvalid = 13,

    /// <summary>A MethodPtr target used a metadata token table other than MethodDef.</summary>
    MethodPointerTargetTableInvalid = 14,

    /// <summary>A FieldPtr target lay outside the exact non-empty FieldDef source end.</summary>
    FieldPointerTargetOutOfRange = 15,

    /// <summary>A MethodPtr target lay outside the exact non-empty MethodDef source end.</summary>
    MethodPointerTargetOutOfRange = 16,

    /// <summary>Two FieldPtr rows selected the same FieldDef target.</summary>
    FieldPointerTargetDuplicate = 17,

    /// <summary>Two MethodPtr rows selected the same MethodDef target.</summary>
    MethodPointerTargetDuplicate = 18,
}

/// <summary>Freezes the two physical columns observed from one member-pointer row.</summary>
/// <remarks>
/// This sealed draft observation records source identity and raw tokens only. Complete-table permutation evidence is
/// represented by a catalog-issued row identity rather than asserted through this physical observation.
/// </remarks>
public sealed class MetadataMemberPointerRowObservationIdentity :
    IEquatable<MetadataMemberPointerRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-member-pointer-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataMemberPointerRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int pointerMetadataToken,
        int targetDefinitionMetadataToken)
    {
        MetadataModule = metadataModule;
        PointerMetadataToken = pointerMetadataToken;
        TargetDefinitionMetadataToken = targetDefinitionMetadataToken;
        TableKind = CanonicalReplayEncoding.IsMetadataTokenForTable(pointerMetadataToken, 0x03)
            ? MetadataMemberPointerTableKind.Field
            : MetadataMemberPointerTableKind.Method;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(pointerMetadataToken);
        writer.WriteInt32(targetDefinitionMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the observed physical pointer row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets whether the observed physical row came from FieldPtr or MethodPtr.</summary>
    public MetadataMemberPointerTableKind TableKind { get; }

    /// <summary>Gets the exact non-nil FieldPtr or MethodPtr token identifying the physical row.</summary>
    public int PointerMetadataToken { get; }

    /// <summary>Gets the raw target metadata token retained for complete-catalog validation.</summary>
    public int TargetDefinitionMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one physical member-pointer row draft observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the row.</param>
    /// <param name="pointerMetadataToken">The exact non-nil FieldPtr or MethodPtr token.</param>
    /// <param name="targetDefinitionMetadataToken">
    /// The raw target token; its table and exact source range are checked only by the complete catalog.
    /// </param>
    /// <returns>A sealed immutable physical-row draft observation with no caller-asserted permutation fact.</returns>
    public static MetadataMemberPointerRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int pointerMetadataToken,
        int targetDefinitionMetadataToken)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(pointerMetadataToken, 0x03) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(pointerMetadataToken, 0x05))
        {
            throw new ArgumentOutOfRangeException(nameof(pointerMetadataToken));
        }
        if (CanonicalReplayEncoding.MetadataTokenRowId(pointerMetadataToken) <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointerMetadataToken));
        }

        return new MetadataMemberPointerRowObservationIdentity(
            metadataModule,
            pointerMetadataToken,
            targetDefinitionMetadataToken);
    }

    /// <summary>Tests canonical equality between two physical member-pointer draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataMemberPointerRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataMemberPointerRowObservationIdentity);

    /// <summary>Computes a hash code from the immutable physical-row draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one member-pointer row after complete permutation validation.</summary>
/// <remarks>This sealed draft identity can be minted only by the complete source-anchored pointer catalog.</remarks>
public sealed class MetadataMemberPointerTableRowIdentity :
    IEquatable<MetadataMemberPointerTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-member-pointer-table-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataMemberPointerTableRowIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataMemberPointerRowObservationIdentity observation)
    {
        SourceEnds = sourceEnds;
        Observation = observation;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(observation.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source ends against which this pointer row was validated.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical pointer-row observation.</summary>
    public MetadataMemberPointerRowObservationIdentity Observation { get; }

    /// <summary>Gets whether this exact row belongs to FieldPtr or MethodPtr.</summary>
    public MetadataMemberPointerTableKind TableKind => Observation.TableKind;

    /// <summary>Gets the exact physical FieldPtr or MethodPtr row token.</summary>
    public int PointerMetadataToken => Observation.PointerMetadataToken;

    /// <summary>Gets the exact resolved FieldDef or MethodDef target token.</summary>
    public int TargetDefinitionMetadataToken => Observation.TargetDefinitionMetadataToken;

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two complete-table-derived pointer draft rows.</summary>
    /// <param name="other">The other derived draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataMemberPointerTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete-table-derived pointer draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataMemberPointerTableRowIdentity);

    /// <summary>Computes a hash code from the immutable complete-table-derived pointer draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataMemberPointerTableRowIdentity Create(
        object mintCapability,
        MetadataSourceEndIdentity sourceEnds,
        MetadataMemberPointerRowObservationIdentity observation)
    {
        if (!MetadataMemberPointerTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A derived member-pointer row requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(observation);
        return new MetadataMemberPointerTableRowIdentity(sourceEnds, observation);
    }
}

/// <summary>Freezes complete source-anchored FieldPtr and MethodPtr table mappings.</summary>
/// <remarks>
/// This sealed draft catalog proves every present pointer table is a complete permutation of its definition table.
/// An absent pointer table is an exact direct list domain. Every other result is prefix-free and exposes no rows.
/// </remarks>
public sealed class MetadataMemberPointerTableCatalogIdentity :
    IEquatable<MetadataMemberPointerTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-member-pointer-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataMemberPointerTableRowIdentity> fieldRows;
    private readonly ImmutableArray<MetadataMemberPointerTableRowIdentity> methodRows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataMemberPointerTableCatalogIdentity(
        MetadataMemberPointerTableResultKind resultKind,
        MetadataMemberPointerTableIssue issue,
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataMemberPointerTableRowIdentity> fieldRows,
        ImmutableArray<MetadataMemberPointerTableRowIdentity> methodRows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        this.fieldRows = fieldRows;
        this.methodRows = methodRows;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, fieldRows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, methodRows, static row => row.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether both member-pointer table domains are exact, non-exact, or invalid.</summary>
    public MetadataMemberPointerTableResultKind ResultKind { get; }

    /// <summary>Gets the typed draft catalog issue, or none for an exact result.</summary>
    public MetadataMemberPointerTableIssue Issue { get; }

    /// <summary>Gets the exact metadata source ends governing both member-pointer tables.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive copy of exact FieldPtr rows, or an initialized empty array otherwise.</summary>
    public ImmutableArray<MetadataMemberPointerTableRowIdentity> FieldRows =>
        ExpressionV2ContractEncoding.Copy(fieldRows);

    /// <summary>Gets a defensive copy of exact MethodPtr rows, or an initialized empty array otherwise.</summary>
    public ImmutableArray<MetadataMemberPointerTableRowIdentity> MethodRows =>
        ExpressionV2ContractEncoding.Copy(methodRows);

    /// <summary>Gets the pointer-row bound only after a cap-plus-one source observation.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the issue-related supplied, exact-source, or cap-plus-one row count.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete source-anchored member-pointer catalog draft proof.</summary>
    /// <param name="sourceEnds">The exact source ends for the metadata module.</param>
    /// <param name="fieldObservations">
    /// Every physical FieldPtr row in RID order; default denotes unavailable acquisition when the table is present.
    /// </param>
    /// <param name="methodObservations">
    /// Every physical MethodPtr row in RID order; default denotes unavailable acquisition when the table is present.
    /// </param>
    /// <returns>An exact catalog, a prefix-free non-exact stop, or a factless invalid draft result.</returns>
    public static MetadataMemberPointerTableCatalogIdentity Create(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> fieldObservations,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> methodObservations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);

        if (sourceEnds.FieldPointerRowCount > StaticFieldV2Limits.MaximumFieldPointerRowCount)
        {
            return NonExact(
                sourceEnds,
                MetadataMemberPointerTableIssue.FieldPointerRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.FieldPointerRowCountBoundName,
                    StaticFieldV2Limits.MaximumFieldPointerRowCount),
                StaticFieldV2Limits.MaximumFieldPointerRowCount + 1);
        }
        if (sourceEnds.MethodPointerRowCount > StaticFieldV2Limits.MaximumMethodPointerRowCount)
        {
            return NonExact(
                sourceEnds,
                MetadataMemberPointerTableIssue.MethodPointerRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.MethodPointerRowCountBoundName,
                    StaticFieldV2Limits.MaximumMethodPointerRowCount),
                StaticFieldV2Limits.MaximumMethodPointerRowCount + 1);
        }

        if (sourceEnds.FieldPointerRowCount > 0 &&
            sourceEnds.FieldPointerRowCount != sourceEnds.FieldDefinitionRowCount)
        {
            return Invalid(
                sourceEnds,
                MetadataMemberPointerTableIssue.FieldPointerDefinitionCoverageConflict,
                sourceEnds.FieldPointerRowCount);
        }
        if (sourceEnds.MethodPointerRowCount > 0 &&
            sourceEnds.MethodPointerRowCount != sourceEnds.MethodDefinitionRowCount)
        {
            return Invalid(
                sourceEnds,
                MetadataMemberPointerTableIssue.MethodPointerDefinitionCoverageConflict,
                sourceEnds.MethodPointerRowCount);
        }

        var countIssue = ValidateObservationCounts(
            sourceEnds,
            fieldObservations,
            methodObservations,
            out var countResultKind,
            out var observedCount);
        if (countIssue != MetadataMemberPointerTableIssue.None)
        {
            return countResultKind == MetadataMemberPointerTableResultKind.NonExact
                ? NonExact(sourceEnds, countIssue, null, observedCount)
                : Invalid(sourceEnds, countIssue, observedCount);
        }

        var copiedFields = sourceEnds.FieldPointerRowCount == 0
            ? ImmutableArray<MetadataMemberPointerRowObservationIdentity>.Empty
            : ExpressionV2ContractEncoding.Copy(fieldObservations);
        var copiedMethods = sourceEnds.MethodPointerRowCount == 0
            ? ImmutableArray<MetadataMemberPointerRowObservationIdentity>.Empty
            : ExpressionV2ContractEncoding.Copy(methodObservations);

        var fieldIssue = ValidateRows(
            sourceEnds,
            copiedFields,
            MetadataMemberPointerTableKind.Field,
            0x03,
            0x04,
            sourceEnds.FieldDefinitionRowCount);
        if (fieldIssue != MetadataMemberPointerTableIssue.None)
        {
            return Invalid(sourceEnds, fieldIssue, copiedFields.Length);
        }

        var methodIssue = ValidateRows(
            sourceEnds,
            copiedMethods,
            MetadataMemberPointerTableKind.Method,
            0x05,
            0x06,
            sourceEnds.MethodDefinitionRowCount);
        if (methodIssue != MetadataMemberPointerTableIssue.None)
        {
            return Invalid(sourceEnds, methodIssue, copiedMethods.Length);
        }

        var fields = ImmutableArray.CreateBuilder<MetadataMemberPointerTableRowIdentity>(copiedFields.Length);
        foreach (var observation in copiedFields)
        {
            fields.Add(MetadataMemberPointerTableRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }
        var methods = ImmutableArray.CreateBuilder<MetadataMemberPointerTableRowIdentity>(copiedMethods.Length);
        foreach (var observation in copiedMethods)
        {
            methods.Add(MetadataMemberPointerTableRowIdentity.Create(RowMintCapability, sourceEnds, observation));
        }

        return new MetadataMemberPointerTableCatalogIdentity(
            MetadataMemberPointerTableResultKind.Exact,
            MetadataMemberPointerTableIssue.None,
            sourceEnds,
            fields.MoveToImmutable(),
            methods.MoveToImmutable(),
            null,
            0);
    }

    /// <summary>Tests canonical equality between two complete member-pointer catalog draft proofs.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataMemberPointerTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete member-pointer catalog draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataMemberPointerTableCatalogIdentity);

    /// <summary>Computes a hash code from immutable complete member-pointer catalog draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<int> ResolveFieldDefinitionTokens(int startRowId, int endExclusiveRowId) =>
        ResolveDefinitionTokens(
            startRowId,
            endExclusiveRowId,
            sourceEndsPointerCount: SourceEnds.FieldPointerRowCount,
            definitionRowCount: SourceEnds.FieldDefinitionRowCount,
            definitionTable: 0x04,
            fieldRows);

    internal ImmutableArray<int> ResolveMethodDefinitionTokens(int startRowId, int endExclusiveRowId) =>
        ResolveDefinitionTokens(
            startRowId,
            endExclusiveRowId,
            sourceEndsPointerCount: SourceEnds.MethodPointerRowCount,
            definitionRowCount: SourceEnds.MethodDefinitionRowCount,
            definitionTable: 0x06,
            methodRows);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    private static MetadataMemberPointerTableIssue ValidateObservationCounts(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> fieldObservations,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> methodObservations,
        out MetadataMemberPointerTableResultKind resultKind,
        out int observedCount)
    {
        var fieldCount = fieldObservations.IsDefault ? 0 : fieldObservations.Length;
        if (sourceEnds.FieldPointerRowCount > 0 && fieldCount < sourceEnds.FieldPointerRowCount)
        {
            resultKind = MetadataMemberPointerTableResultKind.NonExact;
            observedCount = fieldCount;
            return MetadataMemberPointerTableIssue.FieldPointerTableIncomplete;
        }
        if (fieldCount > sourceEnds.FieldPointerRowCount)
        {
            resultKind = MetadataMemberPointerTableResultKind.Invalid;
            observedCount = fieldCount;
            return MetadataMemberPointerTableIssue.FieldPointerRowCountConflict;
        }

        var methodCount = methodObservations.IsDefault ? 0 : methodObservations.Length;
        if (sourceEnds.MethodPointerRowCount > 0 && methodCount < sourceEnds.MethodPointerRowCount)
        {
            resultKind = MetadataMemberPointerTableResultKind.NonExact;
            observedCount = methodCount;
            return MetadataMemberPointerTableIssue.MethodPointerTableIncomplete;
        }
        if (methodCount > sourceEnds.MethodPointerRowCount)
        {
            resultKind = MetadataMemberPointerTableResultKind.Invalid;
            observedCount = methodCount;
            return MetadataMemberPointerTableIssue.MethodPointerRowCountConflict;
        }

        resultKind = MetadataMemberPointerTableResultKind.Exact;
        observedCount = 0;
        return MetadataMemberPointerTableIssue.None;
    }

    private static MetadataMemberPointerTableIssue ValidateRows(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataMemberPointerRowObservationIdentity> observations,
        MetadataMemberPointerTableKind tableKind,
        int pointerTable,
        int targetTable,
        int targetRowCount)
    {
        var targets = new HashSet<int>();
        for (var index = 0; index < observations.Length; index++)
        {
            var observation = observations[index];
            var physicalOrderIssue = tableKind == MetadataMemberPointerTableKind.Field
                ? MetadataMemberPointerTableIssue.FieldPointerPhysicalOrderInvalid
                : MetadataMemberPointerTableIssue.MethodPointerPhysicalOrderInvalid;
            if (observation is null ||
                observation.TableKind != tableKind ||
                observation.PointerMetadataToken != (pointerTable << 24 | checked(index + 1)))
            {
                return physicalOrderIssue;
            }

            if (!observation.MetadataModule.Equals(sourceEnds.SourceModule))
            {
                return tableKind == MetadataMemberPointerTableKind.Field
                    ? MetadataMemberPointerTableIssue.FieldPointerSourceModuleMismatch
                    : MetadataMemberPointerTableIssue.MethodPointerSourceModuleMismatch;
            }

            if (!CanonicalReplayEncoding.IsMetadataTokenForTable(
                    observation.TargetDefinitionMetadataToken,
                    checked((byte)targetTable)))
            {
                return tableKind == MetadataMemberPointerTableKind.Field
                    ? MetadataMemberPointerTableIssue.FieldPointerTargetTableInvalid
                    : MetadataMemberPointerTableIssue.MethodPointerTargetTableInvalid;
            }

            var targetRowId = CanonicalReplayEncoding.MetadataTokenRowId(
                observation.TargetDefinitionMetadataToken);
            if (targetRowId <= 0 || targetRowId > targetRowCount)
            {
                return tableKind == MetadataMemberPointerTableKind.Field
                    ? MetadataMemberPointerTableIssue.FieldPointerTargetOutOfRange
                    : MetadataMemberPointerTableIssue.MethodPointerTargetOutOfRange;
            }

            if (!targets.Add(observation.TargetDefinitionMetadataToken))
            {
                return tableKind == MetadataMemberPointerTableKind.Field
                    ? MetadataMemberPointerTableIssue.FieldPointerTargetDuplicate
                    : MetadataMemberPointerTableIssue.MethodPointerTargetDuplicate;
            }
        }

        return MetadataMemberPointerTableIssue.None;
    }

    private static ImmutableArray<int> ResolveDefinitionTokens(
        int startRowId,
        int endExclusiveRowId,
        int sourceEndsPointerCount,
        int definitionRowCount,
        int definitionTable,
        ImmutableArray<MetadataMemberPointerTableRowIdentity> pointerRows)
    {
        if (startRowId <= 0 ||
            endExclusiveRowId < startRowId ||
            endExclusiveRowId > checked((sourceEndsPointerCount > 0 ? sourceEndsPointerCount : definitionRowCount) + 1))
        {
            throw new ArgumentOutOfRangeException(nameof(startRowId));
        }

        var builder = ImmutableArray.CreateBuilder<int>(endExclusiveRowId - startRowId);
        for (var rowId = startRowId; rowId < endExclusiveRowId; rowId++)
        {
            builder.Add(sourceEndsPointerCount > 0
                ? pointerRows[rowId - 1].TargetDefinitionMetadataToken
                : definitionTable << 24 | rowId);
        }
        return builder.MoveToImmutable();
    }

    private static MetadataMemberPointerTableCatalogIdentity NonExact(
        MetadataSourceEndIdentity sourceEnds,
        MetadataMemberPointerTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            MetadataMemberPointerTableResultKind.NonExact,
            issue,
            sourceEnds,
            ImmutableArray<MetadataMemberPointerTableRowIdentity>.Empty,
            ImmutableArray<MetadataMemberPointerTableRowIdentity>.Empty,
            reachedBound,
            observedCount);

    private static MetadataMemberPointerTableCatalogIdentity Invalid(
        MetadataSourceEndIdentity sourceEnds,
        MetadataMemberPointerTableIssue issue,
        int observedCount) =>
        new(
            MetadataMemberPointerTableResultKind.Invalid,
            issue,
            sourceEnds,
            ImmutableArray<MetadataMemberPointerTableRowIdentity>.Empty,
            ImmutableArray<MetadataMemberPointerTableRowIdentity>.Empty,
            null,
            observedCount);
}
