using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies one complete source-anchored TypeDef-table draft proof.</summary>
/// <remarks>
/// The draft discriminator separates an exact complete table from a prefix-free acquisition or pointer-catalog
/// prerequisite and contradictory supplied evidence. It is not a compatibility commitment.
/// </remarks>
public enum MetadataTypeDefinitionTableResultKind
{
    /// <summary>The complete physical TypeDef table is exact and its list intervals were derived.</summary>
    Exact = 1,

    /// <summary>The complete table was unavailable, crossed a row bound, or requires a pointer-row catalog.</summary>
    NonExact = 2,

    /// <summary>The supplied complete-table claim contradicts exact source ends or physical row invariants.</summary>
    Invalid = 3,
}

/// <summary>Identifies the typed disposition of one TypeDef-table draft proof.</summary>
/// <remarks>
/// The draft issue is retained even when no usable row prefix is exposed, allowing a later host to distinguish an
/// acquisition stop from malformed complete evidence without inferring missing rows.
/// </remarks>
public enum MetadataTypeDefinitionTableIssue
{
    /// <summary>No issue applies to an exact complete table.</summary>
    None = 0,

    /// <summary>The exact source table crossed the admitted complete TypeDef-row count.</summary>
    TableRowBoundReached = 1,

    /// <summary>The supplied observations were default or stopped before the exact source end.</summary>
    TableIncomplete = 2,

    /// <summary>The supplied observation count exceeded the exact source end.</summary>
    TableRowCountConflict = 3,

    /// <summary>The supplied TypeDef tokens did not form the exact physical RID sequence.</summary>
    PhysicalOrderInvalid = 4,

    /// <summary>At least one physical row observation belonged to another metadata module.</summary>
    SourceModuleMismatch = 5,

    /// <summary>A TypeDef Extends token lay outside an exact TypeDef, TypeRef, or TypeSpec source end.</summary>
    ExtendsTokenOutOfRange = 6,

    /// <summary>A physical FieldList start lay outside the exact Field table's start-or-end domain.</summary>
    FieldListStartOutOfRange = 7,

    /// <summary>Physical TypeDef order made FieldList starts decrease.</summary>
    FieldListOrderInvalid = 8,

    /// <summary>The complete Field table could not be owned from RID one under the physical null-run rules.</summary>
    FieldListCoverageInvalid = 9,

    /// <summary>A physical MethodList start lay outside the exact MethodDef table's start-or-end domain.</summary>
    MethodListStartOutOfRange = 10,

    /// <summary>Physical TypeDef order made MethodList starts decrease.</summary>
    MethodListOrderInvalid = 11,

    /// <summary>The complete MethodDef table could not be owned from RID one under the physical null-run rules.</summary>
    MethodListCoverageInvalid = 12,

    /// <summary>The TypeDef source end omitted the required module pseudo-type row.</summary>
    RequiredModuleTypeDefinitionMissing = 13,

    /// <summary>FieldPtr or MethodPtr rows require the immediately following pointer-row catalog checkpoint.</summary>
    MemberPointerTableCatalogRequired = 14,
}

/// <summary>Freezes only the physical columns observed from one TypeDef row.</summary>
/// <remarks>
/// This sealed draft observation deliberately carries no derived FieldList or MethodList end, enclosing type,
/// GenericParam arity, semantic classification, or compiler-name interpretation. Those facts require complete table
/// proofs and cannot be asserted through this identity.
/// </remarks>
public sealed class MetadataTypeDefinitionRowObservationIdentity :
    IEquatable<MetadataTypeDefinitionRowObservationIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typedef-row-observation";
    private const int CanonicalSchemaVersion = 1;
    private const int MaximumListStartRowId = 0x0100_0000;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefinitionRowObservationIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeDefinitionToken,
        int fieldListRowId,
        int methodListRowId,
        string namespaceName,
        string typeName,
        int typeAttributes,
        int? extendsMetadataToken)
    {
        MetadataModule = metadataModule;
        TypeDefinitionToken = typeDefinitionToken;
        FieldListRowId = fieldListRowId;
        MethodListRowId = methodListRowId;
        NamespaceName = namespaceName;
        TypeName = typeName;
        TypeAttributes = typeAttributes;
        ExtendsMetadataToken = extendsMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(typeDefinitionToken);
        writer.WriteInt32(fieldListRowId);
        writer.WriteInt32(methodListRowId);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteInt32(typeAttributes);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, extendsMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the observed physical row.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact non-nil TypeDef token observed for this row.</summary>
    public int TypeDefinitionToken { get; }

    /// <summary>Gets the physical TypeDef.FieldList starting row identifier.</summary>
    public int FieldListRowId { get; }

    /// <summary>Gets the physical TypeDef.MethodList starting row identifier.</summary>
    public int MethodListRowId { get; }

    /// <summary>Gets the exact decoded namespace string, including an empty namespace.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the exact decoded metadata name without interpreting compiler arity spelling.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact raw TypeAttributes bits from the physical row.</summary>
    public int TypeAttributes { get; }

    /// <summary>Gets the decoded TypeDef, TypeRef, or TypeSpec Extends token, or null for a nil coded index.</summary>
    public int? ExtendsMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one physical-column-only TypeDef row draft observation.</summary>
    /// <param name="metadataModule">The exact metadata module containing the row.</param>
    /// <param name="typeDefinitionToken">The exact non-nil TypeDef token.</param>
    /// <param name="fieldListRowId">The physical FieldList starting row identifier.</param>
    /// <param name="methodListRowId">The physical MethodList starting row identifier.</param>
    /// <param name="namespaceName">The exact decoded namespace string, including empty.</param>
    /// <param name="typeName">The exact decoded metadata name.</param>
    /// <param name="typeAttributes">The exact raw TypeAttributes bits.</param>
    /// <param name="extendsMetadataToken">The decoded Extends token, or null for nil.</param>
    /// <returns>
    /// A sealed immutable draft observation that contains no caller-asserted list ends, nesting, or generic arity.
    /// </returns>
    public static MetadataTypeDefinitionRowObservationIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeDefinitionToken,
        int fieldListRowId,
        int methodListRowId,
        string namespaceName,
        string typeName,
        int typeAttributes,
        int? extendsMetadataToken)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidateMetadataToken(typeDefinitionToken, 0x02, nameof(typeDefinitionToken));
        ValidateListStart(fieldListRowId, nameof(fieldListRowId));
        ValidateListStart(methodListRowId, nameof(methodListRowId));
        ExpressionV2ContractEncoding.RequireText(
            namespaceName,
            nameof(namespaceName),
            StaticFieldMetadataTextLimits.MaximumTextLength,
            allowEmpty: true);
        ExpressionV2ContractEncoding.RequireText(
            typeName,
            nameof(typeName),
            StaticFieldMetadataTextLimits.MaximumTextLength);
        if (extendsMetadataToken.HasValue &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x01) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x02) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x1B))
        {
            throw new ArgumentOutOfRangeException(nameof(extendsMetadataToken));
        }

        return new MetadataTypeDefinitionRowObservationIdentity(
            metadataModule,
            typeDefinitionToken,
            fieldListRowId,
            methodListRowId,
            namespaceName,
            typeName,
            typeAttributes,
            extendsMetadataToken);
    }

    /// <summary>Tests canonical equality between two physical-column-only draft observations.</summary>
    /// <param name="other">The other draft observation.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeDefinitionRowObservationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft canonical equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an observation with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeDefinitionRowObservationIdentity);

    /// <summary>Computes a hash code from the immutable physical-observation draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateListStart(int rowId, string parameterName)
    {
        if (rowId < 0 || rowId > MaximumListStartRowId)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>Freezes one TypeDef row after complete-table derivation of its member-list intervals.</summary>
/// <remarks>
/// This sealed draft identity can be minted only by the complete TypeDef catalog. It retains its physical observation
/// and exact source ends, while still making no nesting, GenericParam, or semantic-kind assertion.
/// </remarks>
public sealed class MetadataTypeDefinitionTableRowIdentity : IEquatable<MetadataTypeDefinitionTableRowIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typedef-table-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefinitionTableRowIdentity(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionRowObservationIdentity observation,
        int fieldListRowId,
        int fieldListEndExclusiveRowId,
        int methodListRowId,
        int methodListEndExclusiveRowId)
    {
        SourceEnds = sourceEnds;
        Observation = observation;
        FieldListRowId = fieldListRowId;
        FieldListEndExclusiveRowId = fieldListEndExclusiveRowId;
        MethodListRowId = methodListRowId;
        MethodListEndExclusiveRowId = methodListEndExclusiveRowId;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(observation.CanonicalBytes.AsSpan());
        writer.WriteInt32(fieldListRowId);
        writer.WriteInt32(fieldListEndExclusiveRowId);
        writer.WriteInt32(methodListRowId);
        writer.WriteInt32(methodListEndExclusiveRowId);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact table ends from which both exclusive list ends were derived.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets the unchanged physical-column-only TypeDef row observation.</summary>
    public MetadataTypeDefinitionRowObservationIdentity Observation { get; }

    /// <summary>Gets the next physical FieldList run start, or exact Field-table end plus one at a null or absent successor.</summary>
    public int FieldListEndExclusiveRowId { get; }

    /// <summary>Gets the next physical MethodList run start, or exact MethodDef-table end plus one at a null or absent successor.</summary>
    public int MethodListEndExclusiveRowId { get; }

    /// <summary>Gets the exact TypeDef token forwarded from the physical observation.</summary>
    public int TypeDefinitionToken => Observation.TypeDefinitionToken;

    /// <summary>Gets the complete-table-derived effective FieldList start, including an empty leading or trailing null-row interval.</summary>
    public int FieldListRowId { get; }

    /// <summary>Gets the complete-table-derived effective MethodList start, including an empty leading or trailing null-row interval.</summary>
    public int MethodListRowId { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two complete-table-derived draft rows.</summary>
    /// <param name="other">The other derived draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeDefinitionTableRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete-table-derived draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeDefinitionTableRowIdentity);

    /// <summary>Computes a hash code from the immutable complete-table-derived draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataTypeDefinitionTableRowIdentity Create(
        object mintCapability,
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionRowObservationIdentity observation,
        int fieldListRowId,
        int fieldListEndExclusiveRowId,
        int methodListRowId,
        int methodListEndExclusiveRowId)
    {
        if (!MetadataTypeDefinitionTableCatalogIdentity.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A derived TypeDef table row requires the creating catalog's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(sourceEnds);
        ArgumentNullException.ThrowIfNull(observation);
        if (fieldListRowId <= 0 || fieldListEndExclusiveRowId < fieldListRowId)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldListRowId));
        }
        if (methodListRowId <= 0 || methodListEndExclusiveRowId < methodListRowId)
        {
            throw new ArgumentOutOfRangeException(nameof(methodListRowId));
        }

        return new MetadataTypeDefinitionTableRowIdentity(
            sourceEnds,
            observation,
            fieldListRowId,
            fieldListEndExclusiveRowId,
            methodListRowId,
            methodListEndExclusiveRowId);
    }
}

/// <summary>Freezes one complete source-anchored TypeDef table and its derived member-list intervals.</summary>
/// <remarks>
/// This sealed draft catalog requires exact physical RID coverage. Incomplete and bounded acquisition retains no row
/// prefix; contradictory evidence retains no derived row facts. The exact result is the sole issuer of derived list
/// ends in this contract family. It is a complete-table foundation for later TypeDef authority, not the final global
/// TypeDef identity or a replacement for the legacy construction contracts in this checkpoint. A detected FieldPtr
/// or MethodPtr table produces a prefix-free non-exact result until the immediately following W8.2b pointer catalog.
/// </remarks>
public sealed class MetadataTypeDefinitionTableCatalogIdentity :
    IEquatable<MetadataTypeDefinitionTableCatalogIdentity>
{
    private const string CanonicalDomain = "metadata-v2-typedef-table-catalog";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<MetadataTypeDefinitionTableRowIdentity> rows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataTypeDefinitionTableCatalogIdentity(
        MetadataTypeDefinitionTableResultKind resultKind,
        MetadataTypeDefinitionTableIssue issue,
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeDefinitionTableRowIdentity> rows,
        EvaluationDeterministicBound? reachedBound,
        int observedCount)
    {
        ResultKind = resultKind;
        Issue = issue;
        SourceEnds = sourceEnds;
        this.rows = rows;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, rows, static row => row.CanonicalBytes);
        MetadataGenericParameterOwnerDeclarationIdentity.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete draft table proof is exact, non-exact, or invalid.</summary>
    public MetadataTypeDefinitionTableResultKind ResultKind { get; }

    /// <summary>Gets the typed draft table issue, or none for an exact result.</summary>
    public MetadataTypeDefinitionTableIssue Issue { get; }

    /// <summary>Gets the exact source ends against which complete physical coverage was checked.</summary>
    public MetadataSourceEndIdentity SourceEnds { get; }

    /// <summary>Gets a defensive copy of exact derived rows, or an initialized empty array for every other result.</summary>
    public ImmutableArray<MetadataTypeDefinitionTableRowIdentity> Rows =>
        ExpressionV2ContractEncoding.Copy(rows);

    /// <summary>Gets the TypeDef row-count bound only after a cap-plus-one table observation.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>
    /// Gets the issue-related count: cap-plus-one, supplied TypeDef observations, or detected pointer-table rows.
    /// </summary>
    public int ObservedCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical draft bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete source-anchored TypeDef-table draft proof.</summary>
    /// <param name="sourceEnds">The exact table ends for the source metadata module.</param>
    /// <param name="observations">
    /// Every physical TypeDef row in RID order; a default array denotes unavailable complete acquisition.
    /// </param>
    /// <returns>
    /// An exact catalog with derived intervals, a prefix-free non-exact stop, or a factless invalid draft result.
    /// </returns>
    public static MetadataTypeDefinitionTableCatalogIdentity Create(
        MetadataSourceEndIdentity sourceEnds,
        ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> observations)
    {
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var sourceCount = sourceEnds.TypeDefinitionRowCount;
        if (sourceCount == 0)
        {
            return Invalid(
                sourceEnds,
                MetadataTypeDefinitionTableIssue.RequiredModuleTypeDefinitionMissing,
                observations.IsDefault ? 0 : observations.Length);
        }
        if (sourceCount > StaticFieldV2Limits.MaximumTypeDefinitionRowCount)
        {
            return NonExact(
                sourceEnds,
                MetadataTypeDefinitionTableIssue.TableRowBoundReached,
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.TypeDefinitionRowCountBoundName,
                    StaticFieldV2Limits.MaximumTypeDefinitionRowCount),
                StaticFieldV2Limits.MaximumTypeDefinitionRowCount + 1);
        }
        if (sourceEnds.FieldPointerRowCount > 0 || sourceEnds.MethodPointerRowCount > 0)
        {
            return NonExact(
                sourceEnds,
                MetadataTypeDefinitionTableIssue.MemberPointerTableCatalogRequired,
                null,
                checked(sourceEnds.FieldPointerRowCount + sourceEnds.MethodPointerRowCount));
        }

        if (observations.IsDefault || observations.Length < sourceCount)
        {
            return NonExact(
                sourceEnds,
                MetadataTypeDefinitionTableIssue.TableIncomplete,
                null,
                observations.IsDefault ? 0 : observations.Length);
        }

        if (observations.Length > sourceCount)
        {
            return Invalid(
                sourceEnds,
                MetadataTypeDefinitionTableIssue.TableRowCountConflict,
                observations.Length);
        }

        var copied = ImmutableArray.CreateRange(observations);
        for (var index = 0; index < copied.Length; index++)
        {
            var row = copied[index];
            if (row is null || row.TypeDefinitionToken != (0x0200_0000 | checked(index + 1)))
            {
                return Invalid(sourceEnds, MetadataTypeDefinitionTableIssue.PhysicalOrderInvalid, copied.Length);
            }
            if (!row.MetadataModule.Equals(sourceEnds.SourceModule))
            {
                return Invalid(sourceEnds, MetadataTypeDefinitionTableIssue.SourceModuleMismatch, copied.Length);
            }
            if (row.ExtendsMetadataToken is { } extendsToken && !sourceEnds.ContainsTypeDefOrRefToken(extendsToken))
            {
                return Invalid(sourceEnds, MetadataTypeDefinitionTableIssue.ExtendsTokenOutOfRange, copied.Length);
            }
        }

        var fieldListIssue = ValidateListCoverage(
            copied,
            sourceEnds.FieldDefinitionRowCount,
            static row => row.FieldListRowId,
            MetadataTypeDefinitionTableIssue.FieldListStartOutOfRange,
            MetadataTypeDefinitionTableIssue.FieldListOrderInvalid,
            MetadataTypeDefinitionTableIssue.FieldListCoverageInvalid);
        if (fieldListIssue != MetadataTypeDefinitionTableIssue.None)
        {
            return Invalid(sourceEnds, fieldListIssue, copied.Length);
        }

        var methodListIssue = ValidateListCoverage(
            copied,
            sourceEnds.MethodDefinitionRowCount,
            static row => row.MethodListRowId,
            MetadataTypeDefinitionTableIssue.MethodListStartOutOfRange,
            MetadataTypeDefinitionTableIssue.MethodListOrderInvalid,
            MetadataTypeDefinitionTableIssue.MethodListCoverageInvalid);
        if (methodListIssue != MetadataTypeDefinitionTableIssue.None)
        {
            return Invalid(sourceEnds, methodListIssue, copied.Length);
        }

        var fieldIntervals = DeriveListIntervals(
            copied,
            sourceEnds.FieldDefinitionRowCount,
            static row => row.FieldListRowId);
        var methodIntervals = DeriveListIntervals(
            copied,
            sourceEnds.MethodDefinitionRowCount,
            static row => row.MethodListRowId);
        var rows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionTableRowIdentity>(copied.Length);
        for (var index = 0; index < copied.Length; index++)
        {
            rows.Add(MetadataTypeDefinitionTableRowIdentity.Create(
                RowMintCapability,
                sourceEnds,
                copied[index],
                fieldIntervals[index].Start,
                fieldIntervals[index].EndExclusive,
                methodIntervals[index].Start,
                methodIntervals[index].EndExclusive));
        }

        return new MetadataTypeDefinitionTableCatalogIdentity(
            MetadataTypeDefinitionTableResultKind.Exact,
            MetadataTypeDefinitionTableIssue.None,
            sourceEnds,
            rows.MoveToImmutable(),
            null,
            0);
    }

    /// <summary>Tests canonical equality between two complete TypeDef-table draft proofs.</summary>
    /// <param name="other">The other draft catalog.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(MetadataTypeDefinitionTableCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete TypeDef-table draft equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a catalog with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataTypeDefinitionTableCatalogIdentity);

    /// <summary>Computes a hash code from the immutable complete-table draft bytes.</summary>
    /// <returns>A deterministic hash code for canonical draft content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal MetadataTypeDefinitionTableRowIdentity? ExactRowOrDefault(int typeDefinitionToken)
    {
        if (ResultKind != MetadataTypeDefinitionTableResultKind.Exact ||
            !CanonicalReplayEncoding.IsMetadataTokenForTable(typeDefinitionToken, 0x02))
        {
            return null;
        }

        var rowId = CanonicalReplayEncoding.MetadataTokenRowId(typeDefinitionToken);
        return rowId > 0 && rowId <= rows.Length ? rows[rowId - 1] : null;
    }

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    private static MetadataTypeDefinitionTableIssue ValidateListCoverage(
        ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> observations,
        int targetRowCount,
        Func<MetadataTypeDefinitionRowObservationIdentity, int> selectStart,
        MetadataTypeDefinitionTableIssue outOfRangeIssue,
        MetadataTypeDefinitionTableIssue orderIssue,
        MetadataTypeDefinitionTableIssue coverageIssue)
    {
        var seenNonNull = false;
        var seenPostRunNull = false;
        var previousNonNullStart = 0;
        for (var index = 0; index < observations.Length; index++)
        {
            var start = selectStart(observations[index]);
            if (start > checked(targetRowCount + 1))
            {
                return outOfRangeIssue;
            }

            if (start == 0)
            {
                if (seenNonNull)
                {
                    seenPostRunNull = true;
                }
                continue;
            }

            if (!seenNonNull)
            {
                if (targetRowCount > 0 && start != 1)
                {
                    return coverageIssue;
                }
                seenNonNull = true;
            }
            else if (start < previousNonNullStart)
            {
                return orderIssue;
            }

            if (seenPostRunNull && start != checked(targetRowCount + 1))
            {
                return coverageIssue;
            }

            previousNonNullStart = start;
        }

        return targetRowCount == 0 || seenNonNull
            ? MetadataTypeDefinitionTableIssue.None
            : coverageIssue;
    }

    private static ImmutableArray<DerivedListInterval> DeriveListIntervals(
        ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> observations,
        int targetRowCount,
        Func<MetadataTypeDefinitionRowObservationIdentity, int> selectStart)
    {
        var intervals = new DerivedListInterval[observations.Length];
        var seenNonNull = false;
        var targetEndExclusive = checked(targetRowCount + 1);
        for (var index = 0; index < observations.Length; index++)
        {
            var physicalStart = selectStart(observations[index]);
            if (physicalStart == 0)
            {
                var emptyStart = seenNonNull ? targetEndExclusive : 1;
                intervals[index] = new DerivedListInterval(emptyStart, emptyStart);
                continue;
            }

            seenNonNull = true;
            var endExclusive = index + 1 < observations.Length
                ? selectStart(observations[index + 1])
                : targetEndExclusive;
            if (endExclusive == 0)
            {
                endExclusive = targetEndExclusive;
            }
            intervals[index] = new DerivedListInterval(physicalStart, endExclusive);
        }

        return ImmutableArray.Create(intervals);
    }

    private readonly record struct DerivedListInterval(int Start, int EndExclusive);

    private static MetadataTypeDefinitionTableCatalogIdentity NonExact(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionTableIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount) =>
        new(
            MetadataTypeDefinitionTableResultKind.NonExact,
            issue,
            sourceEnds,
            ImmutableArray<MetadataTypeDefinitionTableRowIdentity>.Empty,
            reachedBound,
            observedCount);

    private static MetadataTypeDefinitionTableCatalogIdentity Invalid(
        MetadataSourceEndIdentity sourceEnds,
        MetadataTypeDefinitionTableIssue issue,
        int observedCount) =>
        new(
            MetadataTypeDefinitionTableResultKind.Invalid,
            issue,
            sourceEnds,
            ImmutableArray<MetadataTypeDefinitionTableRowIdentity>.Empty,
            null,
            observedCount);
}
